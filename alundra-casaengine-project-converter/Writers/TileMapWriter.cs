using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 1: converts the Tiled export of each Alundra map (data/tiled/map_N.tmj) into CasaEngine
/// texture/tileset/tileMap assets, via the engine's own TiledMapImporter.
/// </summary>
public static class TileMapWriter
{
    public static void ConvertMaps(
        string inputDirectory, string outputDirectory, IReadOnlyList<int>? mapFilter, ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, report);
        }
    }

    private static void ConvertMap(string inputDirectory, string outputDirectory, int mapIndex, ConversionReport report)
    {
        var sourceTmjPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.tmj");
        if (!File.Exists(sourceTmjPath))
        {
            report.Warnings.Add($"map_{mapIndex}: source Tiled map not found at '{sourceTmjPath}'.");
            return;
        }

        var mapsDirectory = Path.Combine(outputDirectory, "Maps");
        Directory.CreateDirectory(mapsDirectory);

        var destinationTmjPath = Path.Combine(mapsDirectory, $"map_{mapIndex}.tmj");
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

    private static IReadOnlyList<int> DiscoverMapIndices(string inputDirectory)
    {
        var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
        if (!Directory.Exists(tiledDirectory))
        {
            return Array.Empty<int>();
        }

        var indices = new List<int>();
        foreach (var filePath in Directory.EnumerateFiles(tiledDirectory, "map_*.tmj"))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.StartsWith("map_", StringComparison.Ordinal)
                && int.TryParse(fileName.AsSpan(4), out var mapIndex))
            {
                indices.Add(mapIndex);
            }
        }

        return indices;
    }
}
