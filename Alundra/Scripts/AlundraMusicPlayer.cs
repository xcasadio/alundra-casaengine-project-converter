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

namespace Alundra.Scripts;

/// <summary>
/// This session's background-music playback seam (docs/plan-bgm-demarrage-binaire.md, T2.1) - the
/// equivalent of the original's <c>LoadMapSounds</c>/<c>LoadMapSequence</c> pair for BGM, called once
/// per map entry (see <see cref="AlundraWorldProxy.InstallAudioSystems"/>, item 4 of the plan's own
/// contract). Same shape as <see cref="IAlundraSoundPlayer"/>: the vocabulary is a plain map id, not
/// an <see cref="AudioService"/> one, so a fake can stand in for a test.
///
/// <para>Models the executable's own sequence state (B4-B8): a sequence is CHARGÉE (an index) or none
/// (closed), and, independently, PLAYS or not (a live voice) - loading never plays (B6/B7). The two
/// session fields below are the direct ports of the executable's own globals:
/// <see cref="CurrentMapSoundIndex"/> (<c>g_currentMapSoundIndex</c>, RAW, never remapped, B7's own
/// <c>0x80049cf4</c>) and <see cref="ResetSoundFlag"/> (<c>g_resetSoundFlag</c>, B8), consumed at the
/// frame-close site (<see cref="AlundraWorldProxy.Update"/>, P3) by <see cref="AlundraBgmFadeDirector.StopAllSound"/>.</para>
/// </summary>
public interface IAlundraMusicPlayer
{
    /// <summary>Port of <c>LoadMapSounds</c>/<c>LoadMapSequenceCore</c> (B10/B7) - a no-op when the
    /// table has no entry, when the raw value is <c>0</c>, or when it is the SAME raw value as
    /// <see cref="CurrentMapSoundIndex"/> (fact 1.1's guard). Otherwise stops and closes whatever is
    /// loaded; when the raw value is not <c>45</c> (stop), loads the track (<c>-1</c> remaps to track 1,
    /// B7), sets <see cref="CurrentMapSoundIndex"/> to the RAW value, and arms <see cref="ResetSoundFlag"/>.
    /// NEVER starts a voice by itself (B6/B7): the frame-close site's own <see cref="AlundraBgmFadeDirector.StopAllSound"/>
    /// call does that, once, at the end of the entry's first frame (P3).</summary>
    void PlayMapMusic(int mapId);

    /// <summary>Stops whatever this session's music voice currently is, if any, and clears every piece
    /// of session state: <see cref="CurrentMapSoundIndex"/> back to <c>0</c>, the loaded track closed,
    /// <see cref="ResetSoundFlag"/> cleared (nothing left to consume at the next frame close), and any
    /// warp departure <see cref="HandleWarpDeparture"/> had recorded but not yet evaluated, dropped.
    /// Not called by production code (nothing in this DLL needs an unconditional full reset outside
    /// <see cref="PlayMapMusic"/>'s own paths) - exposed for symmetry with <see cref="IAlundraSoundPlayer"/>
    /// and so a test can force silence between assertions.</summary>
    void StopMusic();

    /// <summary>
    /// B13 (docs/plan-bgm-demarrage-binaire.md, D8): backs opcode 0xA7's own raw-index semantics -
    /// DELIBERATELY NOT <see cref="PlayMapMusic"/>'s own table-driven guard/remap
    /// (<see cref="AlundraMusicIndexTable.ResolvePlaybackDirective"/> is never called here, per the
    /// plan's explicit "do not reuse" - the per-map table's own -1/45 remap has no equivalent in this
    /// raw opcode). <paramref name="rawIndex"/> &lt; 0 is ignored outright (unreachable in practice -
    /// 0xA7's own operand is an unsigned byte); <c>0</c> stops and closes the old track, leaving
    /// <see cref="ResetSoundFlag"/> untouched; &gt; 0 stops and closes the old track, loads the new one,
    /// sets <see cref="CurrentMapSoundIndex"/> to <paramref name="rawIndex"/> and clears
    /// <see cref="ResetSoundFlag"/> (P8 - the streaming path's own reset-flag clear, B13's second
    /// branch). NEVER starts a voice (B13: "Ni PlaySeq, ni SetSeqVolume" - the chargement is instant in
    /// this port, but still silent, exactly like the original's own load-only semantics).
    /// </summary>
    void PlayFromRawIndex(int rawIndex);

    /// <summary>
    /// B5 (docs/plan-bgm-demarrage-binaire.md): port of the original's own <c>InitializeBgm</c> - an
    /// ARRÊT, never a relaunch. Stops the current voice, if any; the loaded track (if any) stays loaded,
    /// rewound. Called by <c>LoadBgm(0)</c> and the master fade machine's own swap tick
    /// (<see cref="AlundraBgmFadeDirector"/>).
    /// </summary>
    void StopSequence();

    /// <summary>
    /// B4 (docs/plan-bgm-demarrage-binaire.md): port of the original's own <c>PlaySeq</c>. A live voice
    /// is left alone (no restart); a loaded-but-silent track starts a fresh voice from the top; no track
    /// loaded is a no-op. The ONLY method in this seam that can ever start a voice - called by
    /// <see cref="AlundraBgmFadeDirector.StopAllSound"/>, the executable's own sole BGM-playing call site
    /// (B1/B2).
    /// </summary>
    void PlaySequence();

    /// <summary>Port of <c>g_currentMapSoundIndex</c> (B7's own <c>0x80049cf4</c>) - the RAW value
    /// <see cref="PlayMapMusic"/>/<see cref="PlayFromRawIndex"/> last wrote, NEVER remapped (a
    /// <c>-1</c> map stays <c>-1</c> here even though its loaded track is 1). <c>0</c> initially.</summary>
    int CurrentMapSoundIndex { get; }

    /// <summary>Port of <c>g_resetSoundFlag</c> (B8) - armed by <see cref="PlayMapMusic"/> (B7's own
    /// <c>0x80049d10</c>), consumed at the frame-close site (P3) right before
    /// <c>SoundPlayer.FlushFrameSounds</c>, which calls <see cref="AlundraBgmFadeDirector.StopAllSound"/>
    /// when set, then <see cref="ClearResetSoundFlag"/>.</summary>
    bool ResetSoundFlag { get; }

    /// <summary>Clears <see cref="ResetSoundFlag"/> - B8's own writers (<c>LoadBgm</c>, the frame-close
    /// consumer, and, additively, <see cref="PlayFromRawIndex"/>'s own positive branch, P8).</summary>
    void ClearResetSoundFlag();
}

/// <summary>
/// Implements <see cref="IAlundraMusicPlayer"/> - Route B of docs/plan-e11c-musique.md §2.2 (D-C-1):
/// <see cref="AudioService.PlayClip"/> on <see cref="AudioBusNames.Music"/>, looped, at full volume,
/// straight from the already-exported <c>Musics/*.wav</c> (resolved through
/// <c>Musics/bgm-manifest.json</c>'s own <c>SoundIndex -&gt; AssetId</c>, the SAME manifest
/// <c>AudioWriter</c> already writes - no new export). No fade-in (D-C-3, fact 1.3): the original's own
/// volume ramp is mathematically a no-op (each tick computes <c>127 - (-12) = 139</c>, clamped to
/// <c>0x7F</c>), so the track is already effectively at full volume on tick one - porting a fade would
/// be LESS faithful, not more.
/// </summary>
/// <remarks>
/// <para><b>D-C-6, the point that decided this class's whole shape</b>: the guard state below
/// (<see cref="_currentMapSoundIndex"/>) is the port of the original's <c>g_currentMapSoundIndex</c>, a
/// GLOBAL that survives every map change by construction. <see cref="AlundraWorldProxy"/> is rebuilt
/// per world (<c>AlundraWorldProxy.cs</c>, field initializers), and so is its own
/// <see cref="AlundraSoundPlayer"/> (<see cref="AlundraWorldProxy.InstallAudioSystems"/>) - porting the
/// guard into an object built the same way would make it vacuous BY CONSTRUCTION: a fresh instance has
/// nothing to guard, so the request would fire with or without the guard and the mutation "remove the
/// guard" could never be caught by a test. So THIS type is instead a SESSION-SCOPED SINGLETON
/// (<see cref="Instance"/>) - <see cref="AlundraWorldProxy.InstallAudioSystems"/> re-points its
/// <see cref="AudioService"/> reference on every world install (a live game's <c>AudioSystemComponent</c>
/// does not change instance across a map change, but re-attaching costs nothing and keeps this class
/// honest about where its data actually comes from) WITHOUT touching <see cref="_currentMapSoundIndex"/>
/// or the currently owned voice - exactly the survival <c>g_currentMapSoundIndex</c> gets for free from
/// being a global. A test resets this shared state with <see cref="ResetForTests"/> (T1 bis, docs/plan-e11c-musique.md,
/// slice C1) - the harness's own fake player cannot see this guard at all (it stands in for
/// <see cref="AlundraMusicPlayer"/> entirely), so T1 bis drives THIS singleton directly instead.</para>
///
/// <para><b>Voice ownership (D-C-5)</b>: the voice is played with <c>owner: this</c> - THIS SINGLETON,
/// never <c>owner: world</c> - so <c>World.Clear</c>'s own <c>StopVoicesOwnedBy(world)</c> never touches
/// it. That is deliberate, not an oversight: the 389/390 pair share raw index 25, and the original never
/// restarts or cuts the track crossing between them - only <see cref="AlundraSoundPlayer"/>'s SFX voices
/// move to <c>owner: world</c> in this same slice (fixing the real defect of fact 1.7, D-C-5). The
/// plan's own §4 item 3 literally reads "owner: world" for this class - written before D-C-5 replaced an
/// earlier, broken "everything owns world" draft (see the plan's own §3, D-C-5's parenthetical: that
/// earlier draft cut the music crossing 389→390 and left the guard blocking any retry, silently). §4
/// was never re-edited to match (the plan's own §5: "the present version was NOT re-reviewed") - this
/// class follows D-C-5/D-C-6, the decisions actually reasoned through, not that stale line.</para>
/// </remarks>
public sealed class AlundraMusicPlayer : IAlundraMusicPlayer
{
    private const string BgmManifestRelativePath = "Musics/bgm-manifest.json";

    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares (D-C-6) -
    /// never <see langword="new"/>'d per world.</summary>
    public static readonly AlundraMusicPlayer Instance = new();

    private AudioService? _audioService;
    private AlundraMusicIndexTable? _table;
    private Dictionary<int, Guid>? _assetIdBySoundIndex;

    /// <summary>Port of <c>g_currentMapSoundIndex</c> (B7's own <c>0x80049cf4</c>) - the RAW value last
    /// written by <see cref="PlayMapMusic"/>/<see cref="PlayFromRawIndex"/>, NEVER remapped (0 initially,
    /// same as <c>SoundManager.cs:335</c>'s own reset). Compared directly against a new map's RAW table
    /// entry (fact 1.1's own guard, before any <c>-1</c> remap) - see
    /// <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective(int)"/>'s own doc for why the
    /// remap-then-compare order matters. Left untouched by the <c>45</c> (stop) path, exactly like the
    /// original: <c>LoadMapSequenceCore</c> is the only site that writes it on the map-entry path, and
    /// the <c>45</c> branch never reaches that write (<c>SoundManager.cs:5183</c> returns before it).</summary>
    private int _currentMapSoundIndex;

    /// <summary>The track index this session has LOADED (rewound if not playing), or
    /// <see langword="null"/> when the sequence is closed - the direct port of the executable's own
    /// "a sequence is open or it is not" state (B6/B7). Independent of whether a voice is currently
    /// alive: loading never plays (B6).</summary>
    private int? _loadedTrackIndex;

    /// <summary>Port of <c>g_resetSoundFlag</c> (B8) - see <see cref="IAlundraMusicPlayer.ResetSoundFlag"/>.</summary>
    private bool _resetSoundFlag;

    /// <summary>F2 (docs/plan-bgm-demarrage-binaire.md): a warp departure's MUSIC half, recorded by
    /// <see cref="HandleWarpDeparture"/> instead of acted on immediately - the binary's own main loop
    /// (<c>0x8002c3f4-0x8002c45c</c>) runs the frame function first (whose <c>HandleMapSoundStreaming</c>
    /// consumes <c>g_resetSoundFlag</c>, B9) and only AFTER it exits does <c>0x8002c46c</c> call
    /// <c>HandleMapSoundEffects</c>, the departure's music half. So the decision below runs at the
    /// frame-close site (<see cref="AlundraWorldProxy.Update"/>), AFTER the reset-flag consumption, not
    /// mid-frame when the departure is requested. <see langword="null"/> when no departure is pending.</summary>
    private (int DestinationMapId, bool WarpSoundIsSilent)? _pendingWarpDeparture;

    private AudioVoiceHandle _currentVoice;

    private AlundraMusicPlayer()
    {
    }

    /// <summary>Re-points this session-scoped instance at the current world's own <see cref="AudioService"/>
    /// and reloads <c>Musics/bgm-manifest.json</c> - called by <see cref="AlundraWorldProxy.InstallAudioSystems"/>
    /// on every world install. Deliberately does NOT touch <see cref="_currentMapSoundIndex"/> or
    /// <see cref="_currentVoice"/> (D-C-6) - those survive across this call exactly like the original's
    /// global survives a map change.</summary>
    public void AttachToWorld(AudioService? audioService, string projectPath)
    {
        _audioService = audioService;
        _table = new AlundraMusicIndexTable(projectPath);
        _assetIdBySoundIndex = LoadBgmManifest(projectPath);
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
    /// already current" and remapped through <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective"/>.
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

        var directive = AlundraMusicIndexTable.ResolvePlaybackDirective(rawIndex);
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

    /// <inheritdoc cref="IAlundraMusicPlayer.PlayFromRawIndex"/>
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

    /// <inheritdoc cref="IAlundraMusicPlayer.StopSequence"/>
    public void StopSequence()
    {
        StopCurrentVoice(); // B5: an arrêt - the loaded track, if any, stays loaded, rewound
    }

    /// <inheritdoc cref="IAlundraMusicPlayer.PlaySequence"/>
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

    /// <inheritdoc cref="IAlundraMusicPlayer.CurrentMapSoundIndex"/>
    public int CurrentMapSoundIndex => _currentMapSoundIndex;

    /// <inheritdoc cref="IAlundraMusicPlayer.ResetSoundFlag"/>
    public bool ResetSoundFlag => _resetSoundFlag;

    /// <inheritdoc cref="IAlundraMusicPlayer.ClearResetSoundFlag"/>
    public void ClearResetSoundFlag() => _resetSoundFlag = false;

    /// <summary>
    /// B17/P7/F2 (docs/plan-bgm-demarrage-binaire.md, D8): the BGM half of <c>HandleMapSoundEffects</c> at
    /// warp departure - NEVER loads the destination track (that only happens when the destination map's
    /// own <see cref="PlayMapMusic"/> runs at arrival). Records the request only - the actual decision
    /// (arm the fade, stop outright, or do nothing) runs later, at the frame-close site
    /// (<see cref="EvaluatePendingWarpDeparture"/>), AFTER <c>g_resetSoundFlag</c>'s own consumption
    /// (B9) - see <see cref="_pendingWarpDeparture"/>'s own doc for why. Called by
    /// <see cref="AlundraWarpDirector"/>'s own two departure sites in place of a <see cref="PlayMapMusic"/>
    /// call (P7's own "le départ ne charge plus rien"). A second call before the pending one is evaluated
    /// overwrites it - the original has exactly one departure per frame, so this cannot happen in practice.
    /// </summary>
    /// <param name="destinationMapId">The map id the warp targets - resolved through the SAME table
    /// <see cref="PlayMapMusic"/> uses, at evaluation time.</param>
    /// <param name="warpSoundIsSilent">Whether the departing warp sound is "nul" (id 0, an unresolved
    /// manifest record, or <c>SeqNum == -1</c> and <c>MaxVoices == 0</c> - B17's own test, computed by
    /// the caller against <see cref="AlundraSoundBank"/>).</param>
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
    /// (<see cref="AlundraWorldProxy.Update"/>), right after the reset-flag block, AFTER
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
            AlundraBgmFadeDirector.Instance.ArmFadeForWarpDeparture(); // B17: drapeau de reset intact
        }
        else
        {
            AlundraBgmFadeDirector.Instance.LoadBgm(0); // B17: arrêt, drapeau effacé
        }
    }

    private void StartVoice(int soundIndex)
    {
        if (_audioService == null || _assetIdBySoundIndex == null)
        {
            return;
        }

        if (!_assetIdBySoundIndex.TryGetValue(soundIndex, out var assetId))
        {
            return;
        }

        var clipProvider = _audioService.ClipProvider;
        if (clipProvider == null)
        {
            return;
        }

        var clip = clipProvider.GetClip(assetId);
        if (clip == null)
        {
            return;
        }

        // D-C-3: full volume, no fade - see this class's own doc for why a ramp would be unfaithful.
        var parameters = new AudioVoiceParameters(AudioVoiceParameters.MaxVolume, 0f, 0f, isLooped: true);
        _currentVoice = _audioService.PlayClip(clip, AudioBusNames.Music, parameters, owner: this);
    }

    private void StopCurrentVoice()
    {
        if (_audioService != null && _currentVoice.IsValid)
        {
            _audioService.Stop(_currentVoice);
        }

        _currentVoice = AudioVoiceHandle.None;
    }

    /// <summary>T1 bis (docs/plan-e11c-musique.md, slice C1): true while this session's music voice is
    /// still alive on <see cref="_audioService"/> - the "and the voice is still alive, not restarted"
    /// half of the guard proof, which a request count alone cannot show (a stopped-then-replayed voice
    /// would look identical to a request count).</summary>
    internal bool IsCurrentVoiceAlive => _audioService != null && _audioService.IsAlive(_currentVoice);

    /// <summary>Test-only accessor (T1/T1 bis/T4): the voice handle currently owned by this session
    /// director, so a test can look its bus/liveness up on the real <see cref="AudioService"/> without
    /// this class exposing that handle on its public <see cref="IAlundraMusicPlayer"/> surface.</summary>
    internal AudioVoiceHandle CurrentVoiceForTests => _currentVoice;

    /// <summary>Test-only (T1 bis): clears every piece of session state so tests do not leak into each
    /// other through this singleton - the "moyen de le réinitialiser en test" D-C-6 asks the slice to
    /// name.</summary>
    internal void ResetForTests()
    {
        _audioService = null;
        _table = null; // F5 (docs/plan-bgm-demarrage-binaire.md): the music-index table itself, too.
        _assetIdBySoundIndex = null;
        _currentVoice = AudioVoiceHandle.None;
        _currentMapSoundIndex = 0;
        _loadedTrackIndex = null;
        _resetSoundFlag = false;
        _pendingWarpDeparture = null; // F2/F5: no warp departure survives into the next test.
    }

    private static Dictionary<int, Guid>? LoadBgmManifest(string projectPath)
    {
        var filePath = Path.Combine(projectPath, "Musics", "bgm-manifest.json");

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraMusicPlayer: '{filePath}' not found; no background music can be resolved "
                    + "(degraded mode).");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var entries = JsonSerializer.Deserialize<List<BgmManifestEntry>>(json, SerializerOptions);
            if (entries == null)
            {
                Logs.WriteWarning(
                    $"AlundraMusicPlayer: '{filePath}' parsed to nothing; no background music can be "
                    + "resolved (degraded mode).");
                return null;
            }

            var bySoundIndex = new Dictionary<int, Guid>();
            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.AssetId, out var assetGuid))
                {
                    bySoundIndex[entry.SoundIndex] = assetGuid;
                }
            }

            return bySoundIndex;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraMusicPlayer: failed to load '{filePath}' ({ex.Message}); no background music "
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
    }
}
