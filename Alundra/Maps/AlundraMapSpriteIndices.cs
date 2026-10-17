#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// E20.e0a1 (docs/plan-e20-heros.md section 1.16.1, ADR-0063): the indices of the map page of the sprite table that are valid on each map, read from <c>Data/map-sprite-indices.json</c> (E20.e0x, ADR-0062: per map
/// <c>"N"</c>, the sorted indices 0 to 255 whose sprite record exists; <c>[]</c> for the 65 maps that have none). <c>SpawnWarpEntity</c> (<c>0x80039F58</c>) validates an index against the table of the map in play: a number
/// is valid on a map only if that map's table holds it (the binary answers the same on the 483 maps, proved by the oracle of the annex <c>docs/plan-e20-annexe/e20-e0a</c>, 2 507 pairs). The global page needs no file: its valid indices are the 151
/// bank keys of the catalog.
/// <para>
/// Read once, the first time the interface asks (never at the load of a world: no existing world sees a warning). Degraded mode like <see cref="SpriteRecordCatalog"/>: a missing or unreadable file logs one warning and answers
/// "valid" for every index of 0 to 255 - the bank key of the catalog alone then decides, so a project without the file keeps spawning what its catalog holds.
/// </para>
/// </summary>
public sealed class AlundraMapSpriteIndices
{
    private const string DataDirectoryName = "Data";
    private const string FileName = "map-sprite-indices.json";

    /// <summary>Highest index of a table (<c>0xFF</c>): the table of a map holds 256 entries, the count word of the binary.</summary>
    private const int LastIndex = 255;

    private readonly Dictionary<int, HashSet<int>> _validByMap = new();

    /// <summary>Loads from <c>Data/map-sprite-indices.json</c> under <paramref name="projectPath"/>.</summary>
    public AlundraMapSpriteIndices(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraMapSpriteIndices: '{filePath}' not found; the indices of the map page are checked against the catalog only (degraded mode, map-sprite-indices).");
                return;
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(filePath));
            if (parsed == null)
            {
                Logs.WriteWarning($"AlundraMapSpriteIndices: '{filePath}' parsed to nothing; degraded mode (map-sprite-indices).");
                return;
            }

            foreach (var (key, indices) in parsed)
            {
                if (int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var map))
                {
                    _validByMap[map] = indices.ToHashSet();
                }
            }

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"AlundraMapSpriteIndices: failed to load '{filePath}' ({ex.Message}); degraded mode (map-sprite-indices).");
            _validByMap.Clear();
        }
    }

    /// <summary>Test seam: a table built in memory (loaded, whatever the maps it holds).</summary>
    internal AlundraMapSpriteIndices(IReadOnlyDictionary<int, IReadOnlyList<int>> indicesByMap)
    {
        foreach (var (map, indices) in indicesByMap)
        {
            _validByMap[map] = indices.ToHashSet();
        }

        IsLoaded = true;
    }

    /// <summary>True when the file was read; false in degraded mode (every index of 0 to 255 is then valid on every map).</summary>
    public bool IsLoaded { get; }

    /// <summary>
    /// Whether the table of map <paramref name="mapIndex"/> holds <paramref name="index"/>: false outside 0 to 255, false for a map the file does not know, and in degraded mode true for every index of 0 to 255.
    /// </summary>
    public bool IsValid(int mapIndex, int index)
    {
        if (index < 0 || index > LastIndex)
        {
            return false;
        }

        if (!IsLoaded)
        {
            return true;
        }

        return _validByMap.TryGetValue(mapIndex, out var valid) && valid.Contains(index);
    }

    /// <summary>Session cache keyed by project path, like <see cref="SpriteRecordCatalog.GetOrCreate"/>: the file is read once per project.</summary>
    private static readonly Dictionary<string, AlundraMapSpriteIndices> SessionCacheByProjectPath = new();

    /// <summary>The cached table of <paramref name="projectPath"/>, read on the first request.</summary>
    public static AlundraMapSpriteIndices GetOrCreate(string projectPath)
    {
        lock (SessionCacheByProjectPath)
        {
            if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var indices))
            {
                indices = new AlundraMapSpriteIndices(projectPath);
                SessionCacheByProjectPath[projectPath] = indices;
            }

            return indices;
        }
    }

    /// <summary>Test-only: clears the session cache (same seam as <see cref="SpriteRecordCatalog"/>).</summary>
    internal static void ResetForTests()
    {
        lock (SessionCacheByProjectPath)
        {
            SessionCacheByProjectPath.Clear();
        }
    }
}
