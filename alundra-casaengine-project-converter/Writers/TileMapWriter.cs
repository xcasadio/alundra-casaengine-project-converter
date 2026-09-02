using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Configuration;
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

        PreSeedAssetCatalog(sourceTmjPath, location, mapIndex, report);

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

    /// <summary>
    /// Makes Phase 1's five per-map catalog ids (tmj, .tileset, raw PNG, .texture wrapper,
    /// .tileMap) deterministic across runs, without touching the engine: EnsureAssetInfo
    /// (EditorAssetImportService.cs:857-876) first looks up an EXISTING catalog entry by exact
    /// FileName before minting a fresh Guid, and SerializeAsset overwrites the written "name" from
    /// that same entry (:878-888) - so pre-seeding an entry whose FileName and Name are
    /// byte-identical to what the engine would itself compute makes the engine reuse our
    /// Ids.For-derived id and name instead of a random one.
    ///
    /// The one value not derivable from MapLocation or the .tmj's own top-level fields is the
    /// tileset PNG's file name: every Alundra .tmj references its tileset EXTERNALLY (a "source"
    /// pointing at a sibling .tsj, no "image" on the .tmj's own tileset entry), so this reads the
    /// SOURCE .tmj (mirroring the engine, which resolves against sourceFilePath too - the .tsj
    /// lives beside the input tmj, never beside the copied destination one), follows "source" to
    /// the .tsj and takes the file name of ITS "image". If the .tmj does not reference EXACTLY one
    /// tileset image (zero, several, or an unreadable/embedded-without-image .tsj), seeding is
    /// skipped for this map and counted: the import then falls back to Guid.NewGuid, visibly (the
    /// double-export oracle catches it) rather than silently.
    /// </summary>
    private static void PreSeedAssetCatalog(
        string sourceTmjPath,
        MapLocation location,
        int mapIndex,
        ConversionReport report)
    {
        if (!TryResolveSingleTilesetImageFileName(sourceTmjPath, out var pngFileName, out var skipReason))
        {
            report.Increment("Phase1.IdSeedingSkipped");
            report.Warnings.Add($"map_{mapIndex}: Phase 1 asset id pre-seeding skipped - {skipReason}.");
            return;
        }

        var mapBaseName = location.FileBaseName;
        var tmjRelativePath = location.TiledMapRelativePath;
        var tileSetRelativePath = Path.Combine(location.TileMapDirectory, $"{mapBaseName}{Constants.FileNameExtensions.TileSet}");
        var rawTextureRelativePath = Path.Combine(location.TileMapDirectory, pngFileName);
        var wrapperRelativePath = Path.ChangeExtension(rawTextureRelativePath, Constants.FileNameExtensions.Texture);
        var tileMapRelativePath = location.TileMapRelativePath;

        AddCatalogEntry("tmj:" + tmjRelativePath, tmjRelativePath, Path.GetFileName(tmjRelativePath));
        AddCatalogEntry("tileset-doc:" + tileSetRelativePath, tileSetRelativePath, $"{mapBaseName}_TileSet");
        AddCatalogEntry("texture-raw:" + rawTextureRelativePath, rawTextureRelativePath, $"{mapBaseName}_{pngFileName}");
        AddCatalogEntry(
            "texture-wrapper:" + wrapperRelativePath,
            wrapperRelativePath,
            $"{mapBaseName}_{Path.GetFileNameWithoutExtension(pngFileName)}");
        AddCatalogEntry("tilemap-doc:" + tileMapRelativePath, tileMapRelativePath, mapBaseName);
    }

    private static void AddCatalogEntry(string idKey, string relativeFileName, string name)
    {
        EditorAssetCatalogService.Add(new AssetInfo(Ids.For(idKey)) { Name = name, FileName = relativeFileName });
    }

    /// <summary>
    /// Reads the SOURCE .tmj's single tileset entry and, following an external "source" to its
    /// .tsj (Alundra's only shape - see the caller's doc), returns the file name of that tileset's
    /// image. Mirrors the JSON navigation of
    /// CasaEngine.EditorServices.Tiled.TiledMapImporter.ReadTilesetReferenceJson (:317-343): read
    /// "tilesets[0]", follow its "source" relative to the owning file, then read "image" off
    /// whichever object ends up holding it (the .tsj, or the tileset object itself when embedded).
    /// </summary>
    private static bool TryResolveSingleTilesetImageFileName(string sourceTmjPath, out string pngFileName, out string skipReason)
    {
        pngFileName = string.Empty;

        JObject mapObject;
        try
        {
            mapObject = JObject.Parse(File.ReadAllText(sourceTmjPath));
        }
        catch (Exception exception)
        {
            skipReason = $"failed to parse '{sourceTmjPath}' ({exception.Message})";
            return false;
        }

        if (mapObject["tilesets"] is not JArray tilesetArray || tilesetArray.Count != 1)
        {
            skipReason = $"expected exactly one tileset, found {(mapObject["tilesets"] as JArray)?.Count ?? 0}";
            return false;
        }

        if (tilesetArray[0] is not JObject tilesetObject)
        {
            skipReason = "tileset entry is not an object";
            return false;
        }

        var tilesetRoot = tilesetObject;
        var source = (string?)tilesetObject["source"];
        if (!string.IsNullOrWhiteSpace(source))
        {
            var tsjPath = Path.Combine(
                Path.GetDirectoryName(sourceTmjPath) ?? string.Empty,
                source.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(tsjPath))
            {
                skipReason = $"external tileset '{tsjPath}' not found";
                return false;
            }

            try
            {
                tilesetRoot = JObject.Parse(File.ReadAllText(tsjPath));
            }
            catch (Exception exception)
            {
                skipReason = $"failed to parse external tileset '{tsjPath}' ({exception.Message})";
                return false;
            }
        }

        var image = (string?)tilesetRoot["image"];
        if (string.IsNullOrWhiteSpace(image))
        {
            skipReason = "tileset has no 'image'";
            return false;
        }

        pngFileName = Path.GetFileName(image.Replace('/', Path.DirectorySeparatorChar));
        skipReason = string.Empty;
        return true;
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
