using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 1: converts the Tiled export of each Alundra map (data/tiled/map_N.tmj) into CasaEngine
/// texture/tileset/tileMap assets, via the engine's own TiledMapImporter. Output files are
/// grouped under Maps/{Zone}/{MapName}-{id}/tilemap/ per maps.json - see
/// <see cref="MapLocation"/>, which owns that layout - and named "{MapName}-{id}" so the many maps
/// sharing an identical display name (e.g. 130 maps named "Inoa (inner)") don't collide.
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

        // The importer derives its whole output (.tileMap, .tileset, .texture and the tileset PNG)
        // from the destination .tmj's folder and base name, so pointing it at
        // MapLocation.TiledMapRelativePath is what lands the tilemap assets in the map's tilemap/
        // subfolder.
        var destinationTmjPath = Path.Combine(outputDirectory, location.TiledMapRelativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationTmjPath)!;
        Directory.CreateDirectory(destinationDirectory);

        PurgeStaleTilemapFiles(destinationDirectory, report);

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

            if (string.Equals(Path.GetExtension(createdAssetFileName), ".tileset", StringComparison.OrdinalIgnoreCase))
            {
                CountAnimatedTiles(report, Path.Combine(outputDirectory, createdAssetFileName));
            }
        }

        report.Increment("Maps");
    }

    /// <summary>
    /// Pre-cleans the map's tilemap/ directory before this run's Phase 1 outputs land in it. The
    /// engine's TiledMapImporter suffixes ("_2", "_3"...) any imported texture whose destination
    /// name already exists on disk and isn't the same source (avoidExistingDestinationFileCollisions),
    /// so a stale copy from a previous run makes every re-run add one more generation of tileset PNG
    /// + .texture wrapper that nothing ever removes. This directory is owned exclusively by Phase 1
    /// (tmj/tileset/tileMap/png/texture are all rewritten every run), so a targeted delete of every
    /// file in it - never a recursive sweep, never touching subdirectories - is safe. Same shape as
    /// AlundraDataExtractor's DeleteStaleBgm.
    /// </summary>
    private static void PurgeStaleTilemapFiles(string tilemapDirectory, ConversionReport report)
    {
        var purgedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(tilemapDirectory))
        {
            File.Delete(filePath);
            purgedCount++;
        }

        if (purgedCount > 0)
        {
            report.Increment("Phase1.StalePagesPurged", purgedCount);
        }
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

    /// <summary>
    /// The engine's TiledMapImporter already turns each Tiled tile's native "animation" property
    /// (baked by the analyser's TiledMapExporter from g_tileAnimDescriptorTable + the map's
    /// SpriteMapEntries) into AnimatedTileData entries when it writes the .tileset asset - see
    /// CasaEngine.EditorServices/EditorAssetImportService.cs CreateTileSetData(). This just counts
    /// how many of a map's tileset entries came out animated, for reporting.
    /// </summary>
    private static void CountAnimatedTiles(ConversionReport report, string tileSetFullPath)
    {
        var tileSetData = new TileSetData();
        tileSetData.Load(JObject.Parse(File.ReadAllText(tileSetFullPath)));

        var animatedTileCount = tileSetData.Tiles.Count(tile => tile is AnimatedTileData);
        if (animatedTileCount > 0)
        {
            report.Increment("TileSets.AnimatedTiles", animatedTileCount);
        }
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
