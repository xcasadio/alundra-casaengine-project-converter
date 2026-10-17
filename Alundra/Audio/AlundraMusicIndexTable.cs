#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Audio;

/// <summary>
/// Loads <c>Maps/music-index.json</c> once (from <see cref="EngineEnvironment.ProjectPath"/>, the same
/// project-root resolution <see cref="AlundraSoundBank"/> already uses) - the RAW republication of the
/// original's <c>g_defaultSoundOffsetList</c> (docs/plan-e11c-musique.md, slice C1, D-C-2), written by
/// the converter's <c>WorldWriter.WriteMusicIndex</c> next to <c>world-index.json</c>. 483 entries, one
/// per <c>map_id</c>: raw values <c>0</c>/<c>45</c>/<c>-1</c>/a real music index, exactly as the
/// original array carries them - see <see cref="ResolvePlaybackDirective(int)"/> for what each value
/// means.
///
/// Degraded mode: missing/unreadable/unparsable file logs one warning at construction and then every
/// lookup misses (<see cref="TryGetRawIndex"/> returns false, <see cref="ResolvePlaybackDirective(int)"/>
/// returns <see cref="MusicPlaybackDirective.NoOp"/>) - same shape as <see cref="AlundraSoundBank"/>'s
/// own degraded mode.
/// </summary>
public sealed class AlundraMusicIndexTable
{
    private const string DataDirectoryName = "Maps";
    private const string FileName = "music-index.json";

    /// <summary>Raw value meaning "stop the old sequence, load nothing new" (fact 1.1,
    /// <c>SoundManager.cs:5183</c>) - 0x2D in the original's own hex.</summary>
    private const int StopValue = 45;

    private readonly Dictionary<int, int> _rawIndexByMapId = new();

    /// <summary>Loads from <c>Maps/music-index.json</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraMusicIndexTable() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads from <c>Maps/music-index.json</c> under <paramref name="projectPath"/> - the
    /// overload tests use to point at a temporary fixture directory instead of the real project.</summary>
    public AlundraMusicIndexTable(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraMusicIndexTable: '{filePath}' not found; every map resolves to no music "
                    + "(degraded mode).");
                return;
            }

            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(json);

            if (parsed == null)
            {
                Logs.WriteWarning(
                    $"AlundraMusicIndexTable: '{filePath}' parsed to nothing; every map resolves to no "
                    + "music (degraded mode).");
                return;
            }

            foreach (var (key, rawIndex) in parsed)
            {
                if (int.TryParse(key, out var mapId))
                {
                    _rawIndexByMapId[mapId] = rawIndex;
                }
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraMusicIndexTable: failed to load '{filePath}' ({ex.Message}); every map resolves "
                + "to no music (degraded mode).");
            _rawIndexByMapId.Clear();
        }
    }

    /// <summary>The exact table entry for <paramref name="mapId"/>, unresolved (still <c>0</c>/<c>45</c>/
    /// <c>-1</c>/a real index) - what <see cref="AlundraMusicPlayer"/>'s own "same as currently playing"
    /// guard compares against (fact 1.1's second row compares the RAW table value, not a resolved one -
    /// see that class's own doc for why). False for an id absent from the table (degraded mode, or an
    /// id outside 0..482) - callers treat that exactly like <see cref="MusicPlaybackDirective.NoOp"/>.</summary>
    public bool TryGetRawIndex(int mapId, out int rawIndex) => _rawIndexByMapId.TryGetValue(mapId, out rawIndex);

    /// <summary>
    /// Port of <c>LoadMapSoundsCore</c>'s own value interpretation (fact 1.1, <c>SoundManager.cs</c>
    /// lines 5168/5183/5186) - NOT the "same as currently playing" guard (fact 1.1's second row), which
    /// is session state this pure table has no business holding (D-C-6) and which <see cref="AlundraMusicPlayer"/>
    /// applies itself, against <paramref name="rawIndex"/> directly, before calling this method. "Play"
    /// here means LOADS the track (<c>-1</c> remaps to track 1, B7 of docs/plan-bgm-demarrage-binaire.md)
    /// - it does NOT start a voice: loading never plays (B6/B7) - see <see cref="AlundraMusicPlayer"/>'s
    /// own class doc for the sequence-state model that actually starts one.
    /// </summary>
    public static MusicPlaybackDirective ResolvePlaybackDirective(int rawIndex)
    {
        if (rawIndex == 0)
        {
            return MusicPlaybackDirective.NoOp;
        }

        if (rawIndex == StopValue)
        {
            return MusicPlaybackDirective.Stop;
        }

        return MusicPlaybackDirective.Play(rawIndex == -1 ? 1 : rawIndex);
    }

    /// <summary>Convenience: looks <paramref name="mapId"/> up and resolves it in one call - the exact
    /// production API <see cref="AlundraMusicPlayer.PlayMapMusic"/> itself calls, and the one T3
    /// (docs/plan-e11c-musique.md, slice C1) drives directly. An id absent from the table (degraded
    /// mode) resolves to <see cref="MusicPlaybackDirective.NoOp"/>, the same outcome a real raw
    /// <c>0</c> entry would produce - "no data" and "explicitly no music" are indistinguishable to a
    /// caller that only wants to know whether to touch the currently playing voice.</summary>
    public MusicPlaybackDirective ResolvePlaybackDirective(int mapId, out int rawIndex)
    {
        if (!TryGetRawIndex(mapId, out rawIndex))
        {
            rawIndex = 0;
            return MusicPlaybackDirective.NoOp;
        }

        return ResolvePlaybackDirective(rawIndex);
    }
}
