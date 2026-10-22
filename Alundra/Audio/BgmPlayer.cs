#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Alundra.Transitions;
using Alundra.Maps;

namespace Alundra.Audio;

/// <summary>
/// Implements <see cref="IMusicPlayer"/> - since E19.L-b3 (ADR-0051) the engine's music player
/// (<see cref="AudioService.Music"/>) streams each track as a <see cref="SoundAsset"/> on
/// <see cref="AudioBusNames.Music"/>, at full volume, straight from the already-exported <c>Musics/*.wav</c>
/// (resolved through <c>Musics/bgm-manifest.json</c>'s own <c>SoundIndex -&gt; AssetId</c>, the SAME manifest
/// <c>AudioWriter</c> already writes). The manifest also says whether the track loops (<c>looping</c>) and
/// where it comes back to (<c>loop_start_sample</c>, the sample frame of the sequence's loop marker): the file
/// holds the intro and the first pass up to the end of the loop, and the engine goes back to that sample at the
/// end of the data - a one-shot just ends. This replaces Route B of docs/plan-e11c-musique.md §2.2 (D-C-1),
/// <see cref="AudioService.PlayClip"/> looping the whole file. No fade-in (D-C-3, fact 1.3): the original's own
/// volume ramp is mathematically a no-op (each tick computes <c>127 - (-12) = 139</c>, clamped to
/// <c>0x7F</c>), so the track is already effectively at full volume on tick one - porting a fade would
/// be LESS faithful, not more.
/// </summary>
/// <remarks>
/// <para><b>D-C-6, the point that decided this class's whole shape</b>: the guard state below
/// (<see cref="_currentMapSoundIndex"/>) is the port of the original's <c>g_currentMapSoundIndex</c>, a
/// GLOBAL that survives every map change by construction. <see cref="WorldProxy"/> is rebuilt
/// per world (<c>WorldProxy.cs</c>, field initializers), and so is its own
/// <see cref="SoundPlayer"/> (<see cref="WorldProxy.InstallAudioSystems"/>) - porting the
/// guard into an object built the same way would make it vacuous BY CONSTRUCTION: a fresh instance has
/// nothing to guard, so the request would fire with or without the guard and the mutation "remove the
/// guard" could never be caught by a test. So THIS type is instead a SESSION-SCOPED SINGLETON
/// (<see cref="Instance"/>) - <see cref="WorldProxy.InstallAudioSystems"/> re-points its
/// <see cref="AudioService"/> reference on every world install (a live game's <c>AudioSystemComponent</c>
/// does not change instance across a map change, but re-attaching costs nothing and keeps this class
/// honest about where its data actually comes from) WITHOUT touching <see cref="_currentMapSoundIndex"/>
/// or the currently owned voice - exactly the survival <c>g_currentMapSoundIndex</c> gets for free from
/// being a global. A test resets this shared state with <see cref="ResetForTests"/> (T1 bis, docs/plan-e11c-musique.md,
/// slice C1) - the harness's own fake player cannot see this guard at all (it stands in for
/// <see cref="BgmPlayer"/> entirely), so T1 bis drives THIS singleton directly instead.</para>
///
/// <para><b>Voice ownership (D-C-5)</b>: the voice is played with <c>owner: this</c> - THIS SINGLETON,
/// never <c>owner: world</c> - so <c>World.Clear</c>'s own <c>StopVoicesOwnedBy(world)</c> never touches
/// it. That is deliberate, not an oversight: the 389/390 pair share raw index 25, and the original never
/// restarts or cuts the track crossing between them - only <see cref="SoundPlayer"/>'s SFX voices
/// move to <c>owner: world</c> in this same slice (fixing the real defect of fact 1.7, D-C-5). The
/// plan's own §4 item 3 literally reads "owner: world" for this class - written before D-C-5 replaced an
/// earlier, broken "everything owns world" draft (see the plan's own §3, D-C-5's parenthetical: that
/// earlier draft cut the music crossing 389→390 and left the guard blocking any retry, silently). §4
/// was never re-edited to match (the plan's own §5: "the present version was NOT re-reviewed") - this
/// class follows D-C-5/D-C-6, the decisions actually reasoned through, not that stale line.</para>
/// </remarks>
public sealed class BgmPlayer : IMusicPlayer
{
    private const string BgmManifestRelativePath = "Musics/bgm-manifest.json";

    /// <summary>The one session-scoped instance every <see cref="WorldProxy"/> shares (D-C-6) -
    /// never <see langword="new"/>'d per world.</summary>
    public static readonly BgmPlayer Instance = new();

    private AudioService? _audioService;
    private MusicIndexTable? _table;

    /// <summary>One streaming sound per track, built once per attach from the manifest (E19.L-b3).</summary>
    private Dictionary<int, SoundAsset>? _assetBySoundIndex;

    /// <summary>Port of <c>g_currentMapSoundIndex</c> (B7's own <c>0x80049cf4</c>) - the RAW value last
    /// written by <see cref="PlayMapMusic"/>/<see cref="PlayFromRawIndex"/>, NEVER remapped (0 initially,
    /// same as <c>SoundManager.cs:335</c>'s own reset). Compared directly against a new map's RAW table
    /// entry (fact 1.1's own guard, before any <c>-1</c> remap) - see
    /// <see cref="MusicIndexTable.ResolvePlaybackDirective(int)"/>'s own doc for why the
    /// remap-then-compare order matters. Left untouched by the <c>45</c> (stop) path, exactly like the
    /// original: <c>LoadMapSequenceCore</c> is the only site that writes it on the map-entry path, and
    /// the <c>45</c> branch never reaches that write (<c>SoundManager.cs:5183</c> returns before it).</summary>
    private int _currentMapSoundIndex;

    /// <summary>The track index this session has LOADED (rewound if not playing), or
    /// <see langword="null"/> when the sequence is closed - the direct port of the executable's own
    /// "a sequence is open or it is not" state (B6/B7). Independent of whether a voice is currently
    /// alive: loading never plays (B6).</summary>
    private int? _loadedTrackIndex;

    /// <summary>Port of <c>g_resetSoundFlag</c> (B8) - see <see cref="IMusicPlayer.ResetSoundFlag"/>.</summary>
    private bool _resetSoundFlag;

    /// <summary>F2 (docs/plan-bgm-demarrage-binaire.md): a warp departure's MUSIC half, recorded by
    /// <see cref="HandleWarpDeparture"/> instead of acted on immediately - the binary's own main loop
    /// (<c>0x8002c3f4-0x8002c45c</c>) runs the frame function first (whose <c>HandleMapSoundStreaming</c>
    /// consumes <c>g_resetSoundFlag</c>, B9) and only AFTER it exits does <c>0x8002c46c</c> call
    /// <c>HandleMapSoundEffects</c>, the departure's music half. So the decision below runs at the
    /// frame-close site (<see cref="WorldProxy.Update"/>), AFTER the reset-flag consumption, not
    /// mid-frame when the departure is requested. <see langword="null"/> when no departure is pending.</summary>
    private (int DestinationMapId, bool WarpSoundIsSilent)? _pendingWarpDeparture;

    /// <summary>The track this session is playing (the engine music player's handle), or none. B4's "a live voice is
    /// left alone" is <see cref="IsCurrentVoiceAlive"/> on it: a one-shot stays alive until the music player's next
    /// update has seen its stream end (D-E19-106).</summary>
    private MusicTrackHandle _currentTrack;

    private SoundAsset? _currentAsset;

    private BgmPlayer()
    {
    }

    /// <summary>Re-points this session-scoped instance at the current world's own <see cref="AudioService"/>
    /// and reloads <c>Musics/bgm-manifest.json</c> - called by <see cref="WorldProxy.InstallAudioSystems"/>
    /// on every world install. Deliberately does NOT touch <see cref="_currentMapSoundIndex"/> or
    /// <see cref="_currentTrack"/> (D-C-6) - those survive across this call exactly like the original's
    /// global survives a map change.</summary>
    public void AttachToWorld(AudioService? audioService, string projectPath)
    {
        _audioService = audioService;
        _table = new MusicIndexTable(projectPath);
        _assetBySoundIndex = LoadBgmManifest(projectPath);
    }

    public void PlayMapMusic(int mapId)
    {
        if (_table == null || !_table.TryGetRawIndex(mapId, out var rawIndex))
        {
            return; // no table (never attached) or no entry: degraded, same as a real "0" (fact 1.1)
        }

        PlayFromTableIndex(rawIndex);
    }

    /// <summary>The per-map table's OWN raw-index handling (B10/B7) - guarded on "0 or same index
    /// already current" and remapped through <see cref="MusicIndexTable.ResolvePlaybackDirective"/>.
    /// NOT the same method as the public <see cref="PlayFromRawIndex"/> (B13, opcode 0xA7's own
    /// semantics, which never guards and never remaps) - see that member's own doc on why the two must
    /// not be merged. Loads only: NEVER starts a voice (B6/B7) - see this interface's own class doc.</summary>
    private void PlayFromTableIndex(int rawIndex)
    {
        if (rawIndex == 0 || rawIndex == _currentMapSoundIndex)
        {
            return; // B10: total short-circuit on 0, or the raw index already current - nothing touched
        }

        StopCurrentVoice();
        _loadedTrackIndex = null; // B10: "arrêt et fermeture de l'ancienne" - unconditional past this point

        var directive = MusicIndexTable.ResolvePlaybackDirective(rawIndex);
        if (directive.Kind != MusicPlaybackDirectiveKind.Play)
        {
            return; // 45 (Stop): nothing new loaded, CurrentMapSoundIndex left untouched (B10/B7)
        }

        _loadedTrackIndex = directive.PlayIndex; // B7: -1 remaps to track 1 here
        _currentMapSoundIndex = rawIndex; // B7 (0x80049cf4): the RAW value, never remapped
        _resetSoundFlag = true; // B7 (0x80049d10): consumed at the frame-close site (P3)
    }

    public void StopMusic()
    {
        StopCurrentVoice();
        _loadedTrackIndex = null;
        _currentMapSoundIndex = 0;
        _resetSoundFlag = false; // F6: a full reset also clears whatever B7 armed - nothing to consume.
        _pendingWarpDeparture = null; // F6: and whatever F2's own HandleWarpDeparture had recorded.
    }

    /// <inheritdoc cref="IMusicPlayer.PlayFromRawIndex"/>
    public void PlayFromRawIndex(int rawIndex)
    {
        if (rawIndex < 0)
        {
            return; // fact 3: ignored outright (unreachable from 0xA7 - unsigned byte operand)
        }

        StopCurrentVoice();
        _loadedTrackIndex = null;
        _currentMapSoundIndex = rawIndex; // B13: 0 too ("index courant <- 0")

        if (rawIndex == 0)
        {
            return; // B13: ResetSoundFlag left untouched on this branch
        }

        _loadedTrackIndex = rawIndex; // B13: instant load, no remap, NEVER starts a voice
        ClearResetSoundFlag(); // P8: the streaming path's own reset-flag clear (deviation declared in P8)
    }

    /// <inheritdoc cref="IMusicPlayer.StopSequence"/>
    public void StopSequence()
    {
        StopCurrentVoice(); // B5: an arrêt - the loaded track, if any, stays loaded, rewound
    }

    /// <inheritdoc cref="IMusicPlayer.PlaySequence"/>
    public void PlaySequence()
    {
        if (IsCurrentVoiceAlive)
        {
            return; // B4: a live voice is left alone - PlaySeq never touches position
        }

        if (_loadedTrackIndex is { } trackIndex)
        {
            StartVoice(trackIndex); // B4: loaded-but-silent - starts a fresh voice from the top
        }
    }

    /// <inheritdoc cref="IMusicPlayer.CurrentMapSoundIndex"/>
    public int CurrentMapSoundIndex => _currentMapSoundIndex;

    /// <inheritdoc cref="IMusicPlayer.ResetSoundFlag"/>
    public bool ResetSoundFlag => _resetSoundFlag;

    /// <inheritdoc cref="IMusicPlayer.ClearResetSoundFlag"/>
    public void ClearResetSoundFlag() => _resetSoundFlag = false;

    /// <summary>
    /// B17/P7/F2 (docs/plan-bgm-demarrage-binaire.md, D8): the BGM half of <c>HandleMapSoundEffects</c> at
    /// warp departure - NEVER loads the destination track (that only happens when the destination map's
    /// own <see cref="PlayMapMusic"/> runs at arrival). Records the request only - the actual decision
    /// (arm the fade, stop outright, or do nothing) runs later, at the frame-close site
    /// (<see cref="EvaluatePendingWarpDeparture"/>), AFTER <c>g_resetSoundFlag</c>'s own consumption
    /// (B9) - see <see cref="_pendingWarpDeparture"/>'s own doc for why. Called by
    /// <see cref="WarpDirector"/>'s own two departure sites in place of a <see cref="PlayMapMusic"/>
    /// call (P7's own "le départ ne charge plus rien"). A second call before the pending one is evaluated
    /// overwrites it - the original has exactly one departure per frame, so this cannot happen in practice.
    /// </summary>
    /// <param name="destinationMapId">The map id the warp targets - resolved through the SAME table
    /// <see cref="PlayMapMusic"/> uses, at evaluation time.</param>
    /// <param name="warpSoundIsSilent">Whether the departing warp sound is "nul" (id 0, an unresolved
    /// manifest record, or <c>SeqNum == -1</c> and <c>MaxVoices == 0</c> - B17's own test, computed by
    /// the caller against <see cref="SfxBank"/>).</param>
    internal void HandleWarpDeparture(int destinationMapId, bool warpSoundIsSilent)
    {
        if (_table == null)
        {
            return; // degraded: never attached, nothing to evaluate against (same shape as PlayMapMusic's own miss)
        }

        _pendingWarpDeparture = (destinationMapId, warpSoundIsSilent);
    }

    /// <summary>
    /// F2 (docs/plan-bgm-demarrage-binaire.md): evaluates and clears whatever
    /// <see cref="HandleWarpDeparture"/> recorded this frame, with EXACTLY that method's own decision
    /// rule (B17) - only moved later in time. Called by the frame-close site
    /// (<see cref="WorldProxy.Update"/>), right after the reset-flag block, AFTER
    /// <c>SoundPlayer.FlushFrameSounds()</c> (mirrors the original's own "after the frame" ordering,
    /// B9/B17 - before or after that flush is equivalent, since the two touch disjoint state). No
    /// allocation, no LINQ, no closure: a nullable-tuple field read and, on the rare non-null path, one
    /// table lookup and one virtual call.
    /// </summary>
    internal void EvaluatePendingWarpDeparture()
    {
        if (_pendingWarpDeparture is not { } pending)
        {
            return;
        }

        _pendingWarpDeparture = null;

        if (_table == null)
        {
            return; // degraded: no table attached, same shape as PlayMapMusic's own miss
        }

        var targetRawIndex = _table.TryGetRawIndex(pending.DestinationMapId, out var raw) ? raw : _currentMapSoundIndex;
        if (targetRawIndex == 0)
        {
            targetRawIndex = _currentMapSoundIndex; // B17: "ou l'index courant s'il vaut 0"
        }

        if (targetRawIndex == _currentMapSoundIndex)
        {
            return; // B17: index égal - rien pour la musique (the destination plays on its own arrival)
        }

        if (pending.WarpSoundIsSilent)
        {
            BgmFadeDirector.Instance.ArmFadeForWarpDeparture(); // B17: drapeau de reset intact
        }
        else
        {
            BgmFadeDirector.Instance.LoadBgm(0); // B17: arrêt, drapeau effacé
        }
    }

    private void StartVoice(int soundIndex)
    {
        if (_audioService == null || _assetBySoundIndex == null)
        {
            return;
        }

        if (!_assetBySoundIndex.TryGetValue(soundIndex, out var asset))
        {
            return;
        }

        // D-C-3: full volume, no fade - see this class's own doc for why a ramp would be unfaithful.
        _currentTrack = _audioService.Music.Play(asset, 0f, owner: this);
        _currentAsset = _currentTrack.IsValid ? asset : null;
    }

    private void StopCurrentVoice()
    {
        if (_audioService != null && _currentTrack.IsValid)
        {
            _audioService.Music.Stop(_currentTrack);
        }

        _currentTrack = MusicTrackHandle.None;
        _currentAsset = null;
    }

    /// <summary>T1 bis (docs/plan-e11c-musique.md, slice C1): true while this session's music track is
    /// still alive on <see cref="_audioService"/> - the "and the voice is still alive, not restarted"
    /// half of the guard proof, which a request count alone cannot show (a stopped-then-replayed voice
    /// would look identical to a request count). A one-shot whose stream ended stays alive until the
    /// engine's next <c>Music.Update</c> drops it (E19.L-b3, D-E19-106).</summary>
    internal bool IsCurrentVoiceAlive => _audioService != null && _audioService.Music.IsAlive(_currentTrack);

    /// <summary>Test-only accessor (T1/T1 bis/T4): the track handle currently owned by this session
    /// director, so a test can compare it before and after without this class exposing it on its public
    /// <see cref="IMusicPlayer"/> surface.</summary>
    internal MusicTrackHandle CurrentVoiceForTests => _currentTrack;

    /// <summary>Test-only (E19.L-b3): the sound asset of the track being played - its streaming flag, loop flag, loop
    /// start, bus and volume - or <see langword="null"/> when nothing plays.</summary>
    internal SoundAsset? CurrentAssetForTests => _currentAsset;

    /// <summary>Test-only (T1 bis): clears every piece of session state so tests do not leak into each
    /// other through this singleton - the "moyen de le réinitialiser en test" D-C-6 asks the slice to
    /// name.</summary>
    internal void ResetForTests()
    {
        _audioService = null;
        _table = null; // F5 (docs/plan-bgm-demarrage-binaire.md): the music-index table itself, too.
        _assetBySoundIndex = null;
        _currentTrack = MusicTrackHandle.None;
        _currentAsset = null;
        _currentMapSoundIndex = 0;
        _loadedTrackIndex = null;
        _resetSoundFlag = false;
        _pendingWarpDeparture = null; // F2/F5: no warp departure survives into the next test.
    }

    private static Dictionary<int, SoundAsset>? LoadBgmManifest(string projectPath)
    {
        var filePath = Path.Combine(projectPath, "Musics", "bgm-manifest.json");

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"BgmPlayer: '{filePath}' not found; no background music can be resolved "
                    + "(degraded mode).");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var entries = JsonSerializer.Deserialize<List<BgmManifestEntry>>(json, SerializerOptions);
            if (entries == null)
            {
                Logs.WriteWarning(
                    $"BgmPlayer: '{filePath}' parsed to nothing; no background music can be "
                    + "resolved (degraded mode).");
                return null;
            }

            var bySoundIndex = new Dictionary<int, SoundAsset>();
            var withoutLoopFields = 0;
            foreach (var entry in entries)
            {
                if (!Guid.TryParse(entry.AssetId, out var assetGuid))
                {
                    continue;
                }

                // ADR-0003 fallback: a manifest from before the loop fields (no 'looping') loops the whole file, from
                // sample 0 - the whole-file loop of the previous route.
                if (entry.Looping == null)
                {
                    withoutLoopFields++;
                }

                var looping = entry.Looping ?? true;
                bySoundIndex[entry.SoundIndex] = new SoundAsset
                {
                    Name = $"bgm_{entry.SoundIndex:D3}",
                    AudioFileAssetId = assetGuid,
                    IsStreaming = true,
                    IsLooped = looping,
                    LoopStartSample = looping ? entry.LoopStartSample ?? 0 : 0,
                    BusName = AudioBusNames.Music,
                    Volume = AudioVoiceParameters.MaxVolume,
                };
            }

            if (withoutLoopFields > 0)
            {
                Logs.WriteWarning(
                    $"BgmPlayer: '{filePath}' has no 'looping' field for {withoutLoopFields} track(s) "
                    + "(an export from before the loop points); they loop the whole file from sample 0. "
                    + "Export the project again for the loop points.");
            }

            return bySoundIndex;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"BgmPlayer: failed to load '{filePath}' ({ex.Message}); no background music "
                + "can be resolved (degraded mode).");
            return null;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new();

    // Field names match AudioWriter's own JSON contract exactly (snake_case - see BgmEntry).
    private sealed class BgmManifestEntry
    {
        [JsonPropertyName("sound_index")] public int SoundIndex { get; set; }
        [JsonPropertyName("asset_id")] public string? AssetId { get; set; }
        [JsonPropertyName("looping")] public bool? Looping { get; set; }
        [JsonPropertyName("loop_start_sample")] public long? LoopStartSample { get; set; }
    }
}
