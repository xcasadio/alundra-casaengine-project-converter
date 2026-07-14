using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 1: converts the Tiled export of each Alundra map (data/tiled/map_N.tmj) into CasaEngine
/// texture/tileset/tileMap assets, via the engine's own TiledMapImporter. Output files are
/// grouped under Maps/{Zone}/ per maps.json, named "{MapName}-{id}" so the many maps sharing an
/// identical display name (e.g. 130 maps named "Inoa (inner)") don't collide.
/// </summary>
public static class TileMapWriter
{
    public static void ConvertMaps(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, mapLocations, report);
        }
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var sourceTmjPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.tmj");
        if (!File.Exists(sourceTmjPath))
        {
            report.Warnings.Add($"map_{mapIndex}: source Tiled map not found at '{sourceTmjPath}'.");
            return;
        }

        var location = ResolveLocation(mapIndex, mapLocations, report);

        var mapsDirectory = Path.Combine(outputDirectory, "Maps", location.ZoneFolder);
        Directory.CreateDirectory(mapsDirectory);

        var destinationTmjPath = Path.Combine(mapsDirectory, $"{location.FileBaseName}.tmj");
        File.Copy(sourceTmjPath, destinationTmjPath, overwrite: true);

        TiledMapImportResult result;
        try
        {
            result = EditorAssetImportService.ImportTiledMap(sourceTmjPath, destinationTmjPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: Tiled import failed - {exception.Message}");
            return;
        }

        foreach (var warning in result.Warnings)
        {
            report.Warnings.Add($"map_{mapIndex}: {warning}");
        }

        foreach (var createdAssetFileName in result.CreatedAssetFileNames)
        {
            CountCreatedAsset(report, createdAssetFileName);
        }

        report.Increment("Maps");
    }

    internal static MapLocation ResolveLocation(
        int mapIndex, IReadOnlyDictionary<int, MapLocation> mapLocations, ConversionReport report)
    {
        if (mapLocations.TryGetValue(mapIndex, out var location))
        {
            return location;
        }

        report.Warnings.Add($"map_{mapIndex}: not listed in maps.json; placed under 'Uncategorized'.");
        return new MapLocation("Uncategorized", $"map_{mapIndex}");
    }

    private static void CountCreatedAsset(ConversionReport report, string relativeFileName)
    {
        var extension = Path.GetExtension(relativeFileName).TrimStart('.').ToLowerInvariant();
        var counterName = extension switch
        {
            "tilemap" => "Assets.TileMap",
            "tileset" => "Assets.TileSet",
            "texture" => "Assets.Texture",
            _ => $"Assets.{extension}",
        };
        report.Increment(counterName);
    }
}
