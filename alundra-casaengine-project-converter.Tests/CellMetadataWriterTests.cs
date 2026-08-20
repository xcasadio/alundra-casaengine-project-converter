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

            // Every one of this fixture's wall stack tiles lands out of the 2x2 map's bounds (their
            // computed targetY is 6, 7 and 8), so the replay must emit zero placements without
            // raising any verification error - not skip the property, not throw.
            var placementsJson = (string?)customProperties["AlundraWallPlacements"];
            Assert.NotNull(placementsJson);
            var placements = JsonSerializer.Deserialize<WallPlacementDocument>(placementsJson!, DeserializeOptions)!;
            Assert.Equal(0, placements.MapIndex);
            Assert.Equal(0, placements.Count);
            Assert.Equal(1, report.Counters["WallPlacements.StacksCovered"]);
            Assert.Equal(0, report.Counters["WallPlacements.Emitted"]);

            // Cell (1,0) has Height 3, but its floor's computed target (sourceY 0 - height 3 = -3) is
            // out of bounds - so, like the wall stack above, the property must still be written (as an
            // empty document), never skipped, and the count must stay zero.
            var floorPlacementsJson = (string?)customProperties["AlundraFloorPlacements"];
            Assert.NotNull(floorPlacementsJson);
            var floorPlacements = JsonSerializer.Deserialize<FloorPlacementDocument>(floorPlacementsJson!, DeserializeOptions)!;
            Assert.Equal(0, floorPlacements.MapIndex);
            Assert.Equal(0, floorPlacements.Count);
            Assert.Equal(0, report.Counters["FloorPlacements.Emitted"]);
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

    [Fact]
    public void ConvertMaps_WhenImportedTileMapDisagreesWithReplay_ReportsVerificationError()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMismatchedMapFixture(inputDirectory, mapIndex: 0);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            CellMetadataWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            // The replay predicts local tile id 5 (gid 6, raw id 500) at plane 0, (0,2), but the
            // fixture's Render_0 layer deliberately stores local tile id 1 (gid 2) there instead:
            // an artificial drift the "any mismatch is an error" contract must catch.
            Assert.Contains(report.Errors, error =>
                error.Contains("wall placement verification failed", StringComparison.Ordinal)
                && error.Contains("expected local tile id 5", StringComparison.Ordinal)
                && error.Contains("found 1", StringComparison.Ordinal));
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
    public void ConvertMaps_ElevatedFloorTile_EmitsAndVerifiesFloorPlacement()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteElevatedFloorMapFixture(inputDirectory, mapIndex: 0, mismatchedGid: false);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            CellMetadataWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);

            var tileMapPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Test Map-0", "tilemap", "Test Map-0.tileMap");
            var tileMapDocument = JObject.Parse(File.ReadAllText(tileMapPath));
            var customProperties = Assert.IsType<JObject>(tileMapDocument["custom_properties"]);

            var floorPlacementsJson = (string?)customProperties["AlundraFloorPlacements"];
            Assert.NotNull(floorPlacementsJson);
            var floorPlacements = JsonSerializer.Deserialize<FloorPlacementDocument>(floorPlacementsJson!, DeserializeOptions)!;

            // Cell (0,1)'s floor (Height 1, raw id 400) collides with cell (0,0)'s flat floor (raw id
            // 100) on plane 0's target (0,0), so it lands on plane 1 - exercising both the elevated-only
            // filter and the first-free-plane collision rule at once. Because (0,0) now holds that
            // elevated placement, the CLOSURE pass promotes cell (0,0)'s own flat (Height 0) floor on
            // plane 0 too - both entries at the same (x,y), on different planes.
            Assert.Equal(2, floorPlacements.Count);
            Assert.Equal(new[] { 0, 0 }, floorPlacements.CellX);
            Assert.Equal(new[] { 1, 0 }, floorPlacements.CellY);
            Assert.Equal(new[] { 1, 0 }, floorPlacements.Plane);
            Assert.Equal(new[] { 0, 0 }, floorPlacements.X);
            Assert.Equal(new[] { 0, 0 }, floorPlacements.Y);
            Assert.Equal(new[] { 2, 1 }, floorPlacements.Gid);
            Assert.Equal(2, report.Counters["FloorPlacements.Emitted"]);
            Assert.Equal(1, report.Counters["FloorPlacements.ConflictEmitted"]);
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
    public void ConvertMaps_WhenElevatedFloorDisagreesWithReplay_ReportsFloorVerificationError()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteElevatedFloorMapFixture(inputDirectory, mapIndex: 0, mismatchedGid: true);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            CellMetadataWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Contains(report.Errors, error =>
                error.Contains("floor placement verification failed", StringComparison.Ordinal)
                && error.Contains("expected local tile id 1", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 1-wide, 2-tall map: cell (0,0) is a flat floor (Height 0, raw id 100 -&gt; local tile 0, gid 1);
    /// cell (0,1) is an elevated floor (Height 1, raw id 400 -&gt; local tile 1, gid 2) whose target
    /// (sourceY 1 - height 1 = 0) collides with cell (0,0)'s floor on plane 0, so it is pushed to plane
    /// 1. When <paramref name="mismatchedGid"/> is true, Render_1's stored gid at (0,0) is deliberately
    /// wrong (3 instead of 2) to exercise the verification-error path.
    /// </summary>
    private static void WriteElevatedFloorMapFixture(string inputDirectory, int mapIndex, bool mismatchedGid)
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
                "tilecount": 3,
                "columns": 3,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 72,
                "imageheight": 16,
                "tiles": [
                    { "id": 0, "properties": [ { "name": "TileId", "type": "int", "value": 100 } ] },
                    { "id": 1, "properties": [ { "name": "TileId", "type": "int", "value": 400 } ] },
                    { "id": 2, "properties": [ { "name": "TileId", "type": "int", "value": 999 } ] }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));

        var plane1Cell0 = mismatchedGid ? 3 : 2;

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}.tmj"),
            $$"""
            {
                "type": "map",
                "orientation": "orthogonal",
                "infinite": false,
                "width": 1,
                "height": 2,
                "tilewidth": 24,
                "tileheight": 16,
                "properties": [ { "name": "Gravity", "type": "int", "value": 128 } ],
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 1,
                        "height": 2,
                        "data": [1, 0]
                    },
                    {
                        "type": "tilelayer",
                        "name": "Render_1",
                        "width": 1,
                        "height": 2,
                        "data": [{{plane1Cell0}}, 0]
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
                "Width": 1,
                "Height": 2,
                "TileWidth": 24,
                "TileHeight": 16,
                "CellOrder": "y * Width + x",
                "Cells": [
                    { "Index": 0, "X": 0, "Y": 0, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 },
                    { "Index": 1, "X": 0, "Y": 1, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 1, "WallTilesOffset": -1, "TileId": 400, "Palette": 1, "Tile": 1, "Flags": 0 }
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
                        { "TileX": 0, "TileY": 1 }
                    ]
                }
            }
            """);
    }

    private static void WriteMismatchedMapFixture(string inputDirectory, int mapIndex)
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
                "tilecount": 10,
                "columns": 5,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 120,
                "imageheight": 32,
                "tiles": [
                    { "id": 5, "properties": [ { "name": "TileId", "type": "int", "value": 500 } ] }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));

        // Replay predicts the wall tile (raw id 500, cell (0,1), stack index 0) lands at plane 0,
        // (0,2) as local tile id 5 (gid 6). The layer below deliberately stores local tile id 1
        // (gid 2) at that position instead, to exercise the verification error path.
        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}.tmj"),
            """
            {
                "type": "map",
                "orientation": "orthogonal",
                "infinite": false,
                "width": 1,
                "height": 3,
                "tilewidth": 24,
                "tileheight": 16,
                "properties": [ { "name": "Gravity", "type": "int", "value": 128 } ],
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 1,
                        "height": 3,
                        "data": [0, 0, 2]
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
                "Width": 1,
                "Height": 3,
                "TileWidth": 24,
                "TileHeight": 16,
                "CellOrder": "y * Width + x",
                "Cells": [
                    { "Index": 0, "X": 0, "Y": 0, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 65535, "Palette": -1, "Tile": -1, "Flags": 0 },
                    { "Index": 1, "X": 0, "Y": 1, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": 0,  "TileId": 65535, "Palette": -1, "Tile": -1, "Flags": 0 },
                    { "Index": 2, "X": 0, "Y": 2, "Walkability": 0, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 65535, "Palette": -1, "Tile": -1, "Flags": 0 }
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
                        { "TileX": 0, "TileY": 1, "WallTiles": { "TileX": 0, "TileY": 1, "Offset": 0, "Count": 1, "Tiles": [500] } },
                        { "TileX": 0, "TileY": 2 }
                    ]
                }
            }
            """);
    }

    private static void WriteMapFixture(string inputDirectory, int mapIndex)
    {
        var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(tiledDirectory);

        var baseName = $"map_{mapIndex}";
        File.WriteAllBytes(Path.Combine(tiledDirectory, $"{baseName}_tileset.png"), FakePngBytes);

        // "TileId" tile properties for every raw id this fixture's cells reference (100 and 200 as
        // floor tiles, 33099/33109/33119 as the wall stack), matching what CreateTilesetJson writes
        // (TiledMapExporter.cs:143) - the wall placement replay resolves a gid for every raw id it
        // touches, even ones whose target ends up out of bounds, exactly like AddRendererTile's own
        // call site resolves the gid before its bounds check runs.
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
                "imageheight": 16,
                "tiles": [
                    { "id": 0, "properties": [ { "name": "TileId", "type": "int", "value": 100 } ] },
                    { "id": 1, "properties": [ { "name": "TileId", "type": "int", "value": 200 } ] },
                    { "id": 2, "properties": [ { "name": "TileId", "type": "int", "value": 33099 } ] },
                    { "id": 3, "properties": [ { "name": "TileId", "type": "int", "value": 33109 } ] },
                    { "id": 4, "properties": [ { "name": "TileId", "type": "int", "value": 33119 } ] }
                ]
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
