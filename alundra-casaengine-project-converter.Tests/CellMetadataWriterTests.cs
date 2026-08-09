using System.Text.Json;
using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class CellMetadataWriterTests
{
    // Minimal 8-byte PNG signature: enough for the importer's file-copy pipeline, which does not
    // decode image content. Not related to any Alundra asset.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void ConvertMaps_MergesCompanionAndNativeCellDataIntoTileMapCustomProperties()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, mapIndex: 0);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            CellMetadataWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Maps.CellMetadata"]);
            Assert.Equal(1, report.Counters["Cells.WallTileStacks"]);

            var tileMapPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Test Map-0", "tilemap", "Test Map-0.tileMap");
            var tileMapDocument = JObject.Parse(File.ReadAllText(tileMapPath));
            var customProperties = Assert.IsType<JObject>(tileMapDocument["custom_properties"]);
            var cellsJson = (string?)customProperties["AlundraCells"];
            Assert.NotNull(cellsJson);

            var cells = JsonSerializer.Deserialize<CellMetadataDocument>(cellsJson!, DeserializeOptions)!;

            Assert.Equal(0, cells.MapIndex);
            Assert.Equal(4, cells.CellCount);
            Assert.Equal(new[] { 0, 5, 0, 0 }, cells.Walkability);
            Assert.Equal(new[] { 0, 2, 0, 0 }, cells.GroundProperty);
            Assert.Equal(new[] { 4, 0, 0, 0 }, cells.Slope);
            Assert.Equal(new[] { 0, 3, 0, 0 }, cells.Height);
            Assert.Equal(new[] { 100, 200, 100, 100 }, cells.TileId);
            Assert.Equal(new[] { 1, 2, 1, 1 }, cells.Palette);
            Assert.Equal(new[] { 1, 2, 1, 1 }, cells.Tile);
            Assert.Equal(new[] { 262144, 1, 0, 0 }, cells.Flags);
            Assert.Equal(new[] { -1, 0, -1, -1 }, cells.WallTilesOffset);

            var wallTileStack = Assert.Single(cells.WallTiles);
            Assert.Equal("1", wallTileStack.Key);
            Assert.Equal(-8, wallTileStack.Value.Offset);
            Assert.Equal(new[] { 33099, 33109, 33119 }, wallTileStack.Value.Tiles);

            // The .tileMap must still round-trip through the engine's own loader.
            var tileMapData = new TileMapData();
            tileMapData.Load(tileMapDocument);
            Assert.Equal(cellsJson, tileMapData.CustomProperties["AlundraCells"]);

            // Phase 1's own map-level custom properties (from the Tiled map's "properties" array)
            // must survive the Phase 2 rewrite untouched.
            Assert.Equal("128", tileMapData.CustomProperties["Gravity"]);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertMaps_WithoutTileMapAsset_WarnsAndSkips()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, mapIndex: 0);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);

            // Phase 1 intentionally not run: no .tileMap asset exists yet.
            CellMetadataWriter.ConvertMaps(
                inputDirectory, outputDirectory, mapFilter: new[] { 0 }, new Dictionary<int, MapLocation>(), report);

            Assert.False(report.Counters.ContainsKey("Maps.CellMetadata"));
            Assert.Contains(report.Warnings, warning => warning.Contains("tileMap asset not found", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteMapFixture(string inputDirectory, int mapIndex)
    {
        var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(tiledDirectory);

        var baseName = $"map_{mapIndex}";
        File.WriteAllBytes(Path.Combine(tiledDirectory, $"{baseName}_tileset.png"), FakePngBytes);

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}_tileset.tsj"),
            """
            {
                "type": "tileset",
                "name": "tileset",
                "tilewidth": 24,
                "tileheight": 16,
                "tilecount": 2,
                "columns": 2,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 48,
                "imageheight": 16
            }
            """.Replace("INDEX", mapIndex.ToString()));

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}.tmj"),
            """
            {
                "type": "map",
                "orientation": "orthogonal",
                "infinite": false,
                "width": 2,
                "height": 2,
                "tilewidth": 24,
                "tileheight": 16,
                "properties": [ { "name": "Gravity", "type": "int", "value": 128 } ],
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 2,
                        "height": 2,
                        "data": [1, 2, 1, 1]
                    }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}.alundra.json"),
            """
            {
                "MapIndex": 0,
                "MapId": 0,
                "Width": 2,
                "Height": 2,
                "TileWidth": 24,
                "TileHeight": 16,
                "CellOrder": "y * Width + x",
                "Cells": [
                    { "Index": 0, "X": 0, "Y": 0, "Walkability": 0, "GroundProperty": 0, "Slope": 4, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 262144 },
                    { "Index": 1, "X": 1, "Y": 0, "Walkability": 5, "GroundProperty": 2, "Slope": 0, "Height": 3, "WallTilesOffset": 0, "TileId": 200, "Palette": 2, "Tile": 2, "Flags": 1 },
                    { "Index": 2, "X": 0, "Y": 1, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 },
                    { "Index": 3, "X": 1, "Y": 1, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 }
                ]
            }
            """);

        File.WriteAllText(
            Path.Combine(dataDirectory, $"{baseName}.json"),
            """
            {
                "Map": {
                    "MapTiles": [
                        { "TileX": 0, "TileY": 0 },
                        { "TileX": 1, "TileY": 0, "WallTiles": { "TileX": 1, "TileY": 0, "Offset": -8, "Count": 3, "Tiles": [33099, 33109, 33119] } },
                        { "TileX": 0, "TileY": 1 },
                        { "TileX": 1, "TileY": 1 }
                    ]
                }
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
