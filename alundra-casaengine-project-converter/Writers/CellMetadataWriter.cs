using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 2: merges per-cell gameplay metadata (Walkability, GroundProperty, Slope, Height, Flags,
/// raw TileId/Palette/Tile, wall tile stacks) into the TileMapData.CustomProperties of the
/// .tileMap assets produced by Phase 1. Map-level fields (Gravity, ZViscosity, SlideEffectId,
/// BalanceLevel) are already carried over by Phase 1 via the Tiled map's own custom properties,
/// so they are not duplicated here.
/// </summary>
public static class CellMetadataWriter
{
    private const string CustomPropertyKey = "AlundraCells";
    private const string WallPlacementsCustomPropertyKey = "AlundraWallPlacements";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

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

        CheckStacksCoveredInvariant(report);
    }

    /// <summary>
    /// WallPlacements.StacksCovered and Cells.WallTileStacks are both incremented, per map, by the
    /// exact same value (cellMetadata.WallTiles.Count - see ConvertMap below) so they must always be
    /// equal, whether this run covered every map or only a --maps subset. A mismatch means one of
    /// the two counters silently skipped a map the other one processed.
    /// </summary>
    private static void CheckStacksCoveredInvariant(ConversionReport report)
    {
        var stacksCovered = report.Counters.GetValueOrDefault("WallPlacements.StacksCovered");
        var wallTileStacks = report.Counters.GetValueOrDefault("Cells.WallTileStacks");
        if (stacksCovered != wallTileStacks)
        {
            report.Errors.Add(
                $"WallPlacements: invariant 'WallPlacements.StacksCovered' is {stacksCovered}, expected {wallTileStacks} (Cells.WallTileStacks).");
        }
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var companionPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.alundra.json");
        var nativeMapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var tileMapRelativePath = location.TileMapRelativePath;
        var tileMapFullPath = Path.Combine(outputDirectory, tileMapRelativePath);

        if (!File.Exists(companionPath))
        {
            report.Warnings.Add($"map_{mapIndex}: companion file not found at '{companionPath}'.");
            return;
        }

        if (!File.Exists(nativeMapPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{nativeMapPath}'.");
            return;
        }

        if (!File.Exists(tileMapFullPath))
        {
            report.Warnings.Add($"map_{mapIndex}: tileMap asset not found at '{tileMapFullPath}' (run Phase 1 first).");
            return;
        }

        CellMetadataDocument cellMetadata;
        try
        {
            cellMetadata = CellMetadataReader.Read(companionPath, nativeMapPath, mapIndex);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read cell metadata - {exception.Message}");
            return;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFullPath)));

        tileMapData.CustomProperties[CustomPropertyKey] = JsonSerializer.Serialize(cellMetadata, SerializerOptions);

        WriteWallPlacements(inputDirectory, mapIndex, cellMetadata, tileMapData, report);

        EditorAssetWriterService.SaveAsset(tileMapRelativePath, tileMapData);

        report.Increment("Maps.CellMetadata");
        report.Increment("Cells.WallTileStacks", cellMetadata.WallTiles.Count);
    }

    /// <summary>
    /// Replays the exporter's floor/wall tile packing (see <see cref="WallPlacementReplayer"/>) to
    /// predict every baked wall tile's (plane, x, y, gid), verifies each prediction against the
    /// flat layers Phase 1 already imported into <paramref name="tileMapData"/>, and - only once
    /// every prediction has been confirmed - stores the result as the "AlundraWallPlacements"
    /// custom property. A verification mismatch or a replay failure is a hard error (report.Errors),
    /// never a warning: it means this map's wall tiles cannot be reliably stripped from the flat
    /// layers and depth-sorted at gameplay time.
    /// </summary>
    private static void WriteWallPlacements(
        string inputDirectory,
        int mapIndex,
        CellMetadataDocument cellMetadata,
        TileMapData tileMapData,
        ConversionReport report)
    {
        var companionPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.alundra.json");
        var tilesetPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}_tileset.tsj");
        var mapTmjPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.tmj");

        if (!File.Exists(tilesetPath) || !File.Exists(mapTmjPath))
        {
            report.Warnings.Add(
                $"map_{mapIndex}: tileset or Tiled map not found; AlundraWallPlacements not written.");
            return;
        }

        try
        {
            var (width, height) = CellMetadataReader.ReadMapSize(companionPath);
            var (gidByRawTileId, firstGid) = TileSetGidMapReader.Read(tilesetPath, mapTmjPath);

            var replayResult = WallPlacementReplayer.Replay(
                mapIndex, width, height, cellMetadata.TileId, cellMetadata.Height, cellMetadata.WallTiles, gidByRawTileId);

            var document = replayResult.Document;

            if (document.Count != replayResult.ExpectedEmitted)
            {
                report.Errors.Add(
                    $"map_{mapIndex}: wall placement count mismatch - emitted {document.Count}, " +
                    $"independently expected {replayResult.ExpectedEmitted}.");
            }

            VerifyPlacements(tileMapData, document, firstGid, mapIndex, report);

            tileMapData.CustomProperties[WallPlacementsCustomPropertyKey] = JsonSerializer.Serialize(document, SerializerOptions);

            report.Increment("WallPlacements.Emitted", document.Count);
            report.Increment("WallPlacements.StacksCovered", replayResult.StacksCovered);

            if (document.Plane.Length > 0 && document.Plane.Max() > 0)
            {
                report.Increment("WallPlacements.MultiPlaneMaps");
            }
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to compute wall placements - {exception.Message}");
        }
    }

    /// <summary>
    /// Confirms every predicted wall tile placement actually landed where and as predicted in the
    /// imported .tileMap's flat layers, comparing against the local (0-based) tile id the engine's
    /// own Tiled importer stores (gid - firstgid; -1 for an empty cell - TileMapLayerData.EmptyTileId).
    /// </summary>
    private static void VerifyPlacements(
        TileMapData tileMapData, WallPlacementDocument document, int firstGid, int mapIndex, ConversionReport report)
    {
        for (var i = 0; i < document.Count; i++)
        {
            var plane = document.Plane[i];
            var x = document.X[i];
            var y = document.Y[i];
            var expectedLocalTileId = document.Gid[i] - firstGid;

            if (plane < 0 || plane >= tileMapData.Layers.Count)
            {
                report.Errors.Add(
                    $"map_{mapIndex}: wall placement verification failed - predicted plane {plane} does not " +
                    $"exist ({tileMapData.Layers.Count} layers) for cell ({document.CellX[i]},{document.CellY[i]}) " +
                    $"stack index {document.StackIndex[i]}.");
                continue;
            }

            int actualLocalTileId;
            try
            {
                actualLocalTileId = tileMapData.GetTileId(plane, x, y);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"map_{mapIndex}: wall placement verification failed - could not read plane {plane} at ({x},{y}) " +
                    $"for cell ({document.CellX[i]},{document.CellY[i]}) stack index {document.StackIndex[i]}: {exception.Message}");
                continue;
            }

            if (actualLocalTileId != expectedLocalTileId)
            {
                report.Errors.Add(
                    $"map_{mapIndex}: wall placement verification failed at plane {plane} ({x},{y}) for cell " +
                    $"({document.CellX[i]},{document.CellY[i]}) stack index {document.StackIndex[i]} - expected " +
                    $"local tile id {expectedLocalTileId} (gid {document.Gid[i]}), found {actualLocalTileId}.");
            }
        }
    }
}
