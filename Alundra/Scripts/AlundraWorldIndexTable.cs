#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// T4 (docs/plan-transitions-carte.md §1.1.e/§3): the FORWARD half of <c>Maps/world-index.json</c> -
/// map id to world path - the direction <see cref="AlundraWarpDirector"/> needs to resolve
/// <c>g_desiredMap</c> into the string <c>GameManager.SetWorldToLoad(string)</c> takes. Every existing
/// reader of this same file (<see cref="MapEventProgramLoader"/>, <c>BackdropLoader</c>,
/// <c>AlundraDialogueStringsLoader</c>) goes the OTHER way (a world's own trailing "-{mapId}" name to
/// its map id, to find sibling data next to that world) - none of them expose the raw path itself, so
/// this is a new, minimal reader rather than a fourth copy of theirs.
///
/// §1.1.e (verified): the file's values are already exactly the <c>file_name</c>
/// <c>AssetCatalog.GetByFileName</c>/<c>GameManager.SetWorldToLoad(string)</c> expect
/// (<c>WorldWriter.cs:501-527</c>) - <see cref="Resolve"/> hands the raw string straight through, no
/// path recombination needed (unlike <see cref="MapEventProgramLoader"/>, which still has to rebuild a
/// full events-file path relative to it).
///
/// Degraded mode (same shape as <see cref="AlundraMusicIndexTable"/>): a missing/unparsable file logs
/// one warning at construction and every lookup then misses.
/// </summary>
public sealed class AlundraWorldIndexTable
{
    private const string DataDirectoryName = "Maps";
    private const string FileName = "world-index.json";

    private readonly Dictionary<int, string> _pathByMapId = new();

    /// <summary>Loads from <c>Maps/world-index.json</c> under <paramref name="projectPath"/> - the
    /// overload tests use to point at a temporary fixture directory instead of the real project.</summary>
    public AlundraWorldIndexTable(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraWorldIndexTable: '{filePath}' not found; no map id resolves to a world path "
                    + "(degraded mode).");
                return;
            }

            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (parsed == null)
            {
                Logs.WriteWarning(
                    $"AlundraWorldIndexTable: '{filePath}' parsed to nothing; no map id resolves to a "
                    + "world path (degraded mode).");
                return;
            }

            foreach (var (key, path) in parsed)
            {
                if (int.TryParse(key, out var mapId))
                {
                    _pathByMapId[mapId] = path;
                }
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraWorldIndexTable: failed to load '{filePath}' ({ex.Message}); no map id resolves "
                + "to a world path (degraded mode).");
            _pathByMapId.Clear();
        }
    }

    /// <summary>Loads from <c>Maps/world-index.json</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraWorldIndexTable() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>The raw <c>file_name</c>-shaped path for <paramref name="mapId"/> (§1.1.e), or null when
    /// absent (degraded mode, or an id outside the table) - callers treat that as "no world to request",
    /// same shape as <see cref="AlundraMusicIndexTable.TryGetRawIndex"/>'s own miss.</summary>
    public string? Resolve(int mapId) => _pathByMapId.TryGetValue(mapId, out var path) ? path : null;
}
