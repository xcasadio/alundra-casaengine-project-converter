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

        EditorAssetWriterService.SaveAsset(tileMapRelativePath, tileMapData);

        report.Increment("Maps.CellMetadata");
        report.Increment("Cells.WallTileStacks", cellMetadata.WallTiles.Count);
    }
}
