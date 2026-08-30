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
/// This session's background-music playback seam (docs/plan-e11c-musique.md, slice C1) - the
/// equivalent of the original's <c>LoadMapSounds</c>/<c>LoadMapSequence</c> pair for BGM, called once
/// per map entry (see <see cref="AlundraWorldProxy.InstallAudioSystems"/>, item 4 of the plan's own
/// contract). Same shape as <see cref="IAlundraSoundPlayer"/>: the vocabulary is a plain map id, not
/// an <see cref="AudioService"/> one, so a fake can stand in for a test.
/// </summary>
public interface IAlundraMusicPlayer
{
    /// <summary>Requests the background music for <paramref name="mapId"/> - a no-op when the table has
    /// no entry, when the raw value is <c>0</c> (fact 1.1: total short-circuit), or when it is the SAME
    /// raw value as the one already resolved and playing (the index guard, fact 1.1's second row -
    /// D-C-6 is the whole reason this state lives where it does, see <see cref="AlundraMusicPlayer"/>'s
    /// own class doc).</summary>
    void PlayMapMusic(int mapId);

    /// <summary>Stops whatever this session's music voice currently is, if any, and clears the guard so
    /// the next <see cref="PlayMapMusic"/> call is never suppressed by a stale comparison. Not called by
    /// production code in this slice (nothing in C1 needs an unconditional stop outside
    /// <see cref="PlayMapMusic"/>'s own <c>45</c>/guard-miss paths) - exposed for symmetry with
    /// <see cref="IAlundraSoundPlayer"/> and so a test can force silence between assertions.</summary>
    void StopMusic();
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
/// (<see cref="_lastResolvedIndex"/>) is the port of the original's <c>g_currentMapSoundIndex</c>, a
/// GLOBAL that survives every map change by construction. <see cref="AlundraWorldProxy"/> is rebuilt
/// per world (<c>AlundraWorldProxy.cs</c>, field initializers), and so is its own
/// <see cref="AlundraSoundPlayer"/> (<see cref="AlundraWorldProxy.InstallAudioSystems"/>) - porting the
/// guard into an object built the same way would make it vacuous BY CONSTRUCTION: a fresh instance has
/// nothing to guard, so the request would fire with or without the guard and the mutation "remove the
/// guard" could never be caught by a test. So THIS type is instead a SESSION-SCOPED SINGLETON
/// (<see cref="Instance"/>) - <see cref="AlundraWorldProxy.InstallAudioSystems"/> re-points its
/// <see cref="AudioService"/> reference on every world install (a live game's <c>AudioSystemComponent</c>
/// does not change instance across a map change, but re-attaching costs nothing and keeps this class
/// honest about where its data actually comes from) WITHOUT touching <see cref="_lastResolvedIndex"/>
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

    /// <summary>Port of <c>g_currentMapSoundIndex</c> - the RAW value of whatever is currently resolved
    /// (0 initially, same as <c>SoundManager.cs:335</c>'s own reset). Compared directly against a new
    /// map's RAW table entry (fact 1.1's own guard, before any <c>-1</c> remap) - see
    /// <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective(int)"/>'s own doc for why the
    /// remap-then-compare order matters. Left untouched by the <c>45</c> (stop) path, exactly like the
    /// original: <c>LoadMapSequenceCore</c> is the only site that writes <c>g_currentMapSoundIndex</c>,
    /// and the <c>45</c> branch never reaches it (<c>SoundManager.cs:5183</c> returns before calling
    /// <c>LoadMapSequence</c>).</summary>
    private int _lastResolvedIndex;

    private AudioVoiceHandle _currentVoice;

    private AlundraMusicPlayer()
    {
    }

    /// <summary>Re-points this session-scoped instance at the current world's own <see cref="AudioService"/>
    /// and reloads <c>Musics/bgm-manifest.json</c> - called by <see cref="AlundraWorldProxy.InstallAudioSystems"/>
    /// on every world install. Deliberately does NOT touch <see cref="_lastResolvedIndex"/> or
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

        PlayFromRawIndex(rawIndex);
    }

    private void PlayFromRawIndex(int rawIndex)
    {
        if (rawIndex == 0)
        {
            return; // fact 1.1, first row: total short-circuit, the guard itself is never consulted
        }

        if (rawIndex == _lastResolvedIndex)
        {
            return; // fact 1.1, second row: same track already playing - do not restart it
        }

        StopCurrentVoice();

        var directive = AlundraMusicIndexTable.ResolvePlaybackDirective(rawIndex);
        if (directive.Kind != MusicPlaybackDirectiveKind.Play)
        {
            return; // 45 (Stop): the old voice is already stopped above, nothing new to load
        }

        _lastResolvedIndex = directive.PlayIndex;
        StartVoice(directive.PlayIndex);
    }

    public void StopMusic()
    {
        StopCurrentVoice();
        _lastResolvedIndex = 0;
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
        _assetIdBySoundIndex = null;
        _currentVoice = AudioVoiceHandle.None;
        _lastResolvedIndex = 0;
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
