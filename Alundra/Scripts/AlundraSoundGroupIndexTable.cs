#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// Loads <c>Maps/sound-group-index.json</c> once (from <see cref="EngineEnvironment.ProjectPath"/>, the
/// same project-root resolution <see cref="AlundraSoundBank"/>/<see cref="AlundraMusicIndexTable"/>
/// already use) - the RAW republication of the original's <c>VabIndexByMapId</c>
/// (docs/plan-e11b-opcodes-audio.md, slice B3, D-B-7), written by the converter's
/// <c>WorldWriter.WriteSoundGroupIndex</c> next to <c>music-index.json</c>. 483 entries, one per
/// <c>map_id</c>: the map's own VAB group id (0..76), exactly as the original array carries it - see
/// <see cref="AlundraSoundBank.TryResolve"/> for what the value is compared against.
///
/// Degraded mode: missing/unreadable/unparsable file logs one warning at construction and then every
/// lookup misses (<see cref="TryGetGroup"/> returns false) - same shape as
/// <see cref="AlundraMusicIndexTable"/>'s own degraded mode. A miss is NOT an error at the call site: a
/// world's own <see cref="AlundraWorldProxy.InstallAudioSystems"/> simply installs its
/// <see cref="AlundraSoundPlayer"/> with no group, the exact pre-B3 shape (D-E11-6's own deviation),
/// so redirection never fires and the polyphony ceiling behaves exactly as it did before this table
/// existed.
/// </summary>
public sealed class AlundraSoundGroupIndexTable
{
    private const string DataDirectoryName = "Maps";
    private const string FileName = "sound-group-index.json";

    private readonly Dictionary<int, int> _groupByMapId = new();

    /// <summary>Loads from <c>Maps/sound-group-index.json</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraSoundGroupIndexTable() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads from <c>Maps/sound-group-index.json</c> under <paramref name="projectPath"/> - the
    /// overload tests use to point at a temporary fixture directory instead of the real project.</summary>
    public AlundraSoundGroupIndexTable(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraSoundGroupIndexTable: '{filePath}' not found; every map resolves to no sound "
                    + "group (degraded mode).");
                return;
            }

            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(json);

            if (parsed == null)
            {
                Logs.WriteWarning(
                    $"AlundraSoundGroupIndexTable: '{filePath}' parsed to nothing; every map resolves to "
                    + "no sound group (degraded mode).");
                return;
            }

            foreach (var (key, groupId) in parsed)
            {
                if (int.TryParse(key, out var mapId))
                {
                    _groupByMapId[mapId] = groupId;
                }
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraSoundGroupIndexTable: failed to load '{filePath}' ({ex.Message}); every map "
                + "resolves to no sound group (degraded mode).");
            _groupByMapId.Clear();
        }
    }

    /// <summary>The map's own VAB group id (<see cref="AlundraSoundBank.TryResolve"/>'s own
    /// <c>soundGroup</c> parameter), or false for an id absent from the table (degraded mode, or an id
    /// outside 0..482) - <see cref="AlundraWorldProxy.InstallAudioSystems"/> treats that exactly like
    /// "no group", the pre-B3 shape.</summary>
    public bool TryGetGroup(int mapId, out int groupId) => _groupByMapId.TryGetValue(mapId, out groupId);

    /// <summary>
    /// Session-scoped cache keyed by project path - same shape and rationale as
    /// <see cref="AlundraSoundBank.SessionCacheByProjectPath"/>: two consecutive worlds over the SAME
    /// project read <c>Maps/sound-group-index.json</c> only once, while two DIFFERENT projects never
    /// share a cached table.
    /// </summary>
    private static readonly Dictionary<string, AlundraSoundGroupIndexTable> SessionCacheByProjectPath = new();

    /// <summary>Returns the cached table for <paramref name="projectPath"/>, loading it once on the
    /// first request and reusing it for every later one - see
    /// <see cref="SessionCacheByProjectPath"/>'s own doc.</summary>
    public static AlundraSoundGroupIndexTable GetOrCreate(string projectPath)
    {
        if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var table))
        {
            table = new AlundraSoundGroupIndexTable(projectPath);
            SessionCacheByProjectPath[projectPath] = table;
        }

        return table;
    }

    /// <summary>Test-only: clears the session cache so tests do not leak a table loaded by one test
    /// into another through <see cref="GetOrCreate"/> - same seam as
    /// <see cref="AlundraSoundBank.ResetForTests"/>.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();
}
