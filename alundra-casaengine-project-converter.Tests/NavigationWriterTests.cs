using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers E4.a's navigation layer (docs/plan-e4-deplacement-scripte.md "E4.a", D5): the shared
/// "Navigation" tileset (2 tiles, W/B) and the per-map "Navigation" layer appended to every .tileMap
/// whose AlundraCells source exists. Mask fact (2026-08-24 pre-check, plan revised before this
/// tranche): M = 0x40 - none of the intro's scripted walkers (map 389 banks 25/146/161) carry
/// ClassB/ClassA (their SpriteRecord.Header.MoreFlags is 0x80, Collidable only) - so only Walkability
/// bit 6 blocks a cell; GroundProperty &lt;&lt; 8 never intersects 0x40 and is folded into the formula
/// only for fidelity with the documented expression.
/// </summary>
public class NavigationWriterTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertMaps_FormulaOnSyntheticCells_UsesMask0x40()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            // 1-wide, 3-tall synthetic map:
            //  cell (0,0): Walkability 0x40 (bit 6 set)            -> masked 0x40 -> B
            //  cell (0,1): Walkability 1   (bit 0 only)            -> masked 0    -> W
            //  cell (0,2): GroundProperty 128 (gp&lt;&lt;8 = 0x8000)   -> masked 0    -> W (class
            //              restrictions never live in the grid - the revised plan's own note: gp&lt;&lt;8
            //              can never intersect 0x40)
            WriteMapFixture(inputDirectory, mapIndex: 0);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            NavigationWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Navigation.Layers"]);
            Assert.Equal(2, report.Counters["Navigation.WalkableCells"]);
            Assert.Equal(1, report.Counters["Navigation.BlockedCells"]);

            var tileMapPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-0", "tilemap", "Test Map-0.tileMap");
            var tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));

            var navigationLayer = Assert.Single(tileMapData.Layers, layer => layer.Name == "Navigation");
            Assert.Equal("grid", navigationLayer.CustomProperties["navigation.role"]);
            Assert.Equal("false", navigationLayer.CustomProperties["navigation.defaultWalkable"]);
            Assert.Equal(TileMapDepthRole.CollisionOnly, navigationLayer.Depth.Role);
            Assert.False(navigationLayer.Depth.ShouldRenderTiles);

            // Tile ids: 0 = W, 1 = B (see BuildTileNode).
            Assert.Equal(new[] { 1, 0, 0 }, navigationLayer.tiles);

            // Every cell of this layer points at the Navigation tileset, appended after the map's own
            // native tileset (index 0).
            Assert.Equal(2, tileMapData.TileSetDataAssetIds.Count);
            Assert.All(Enumerable.Range(0, 3), x => Assert.Equal(1, navigationLayer.GetTileSourceIndex(x)));
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
    public void ConvertMaps_WritesASharedTilesetWithExactlyTwoTiles()
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
            NavigationWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Navigation.TileSets"]);

            var tileSetPath = Path.Combine(outputDirectory, "Data", "Navigation.tileset");
            Assert.True(File.Exists(tileSetPath));

            var tileSetData = new TileSetData();
            tileSetData.Load(JObject.Parse(File.ReadAllText(tileSetPath)));

            Assert.Equal(2, tileSetData.Tiles.Count);
            Assert.Equal(24, tileSetData.TileSize.Width);
            Assert.Equal(16, tileSetData.TileSize.Height);

            var walkableTile = Assert.Single(tileSetData.Tiles, t => t.CustomProperties["navigation.walkable"] == "true");
            var blockedTile = Assert.Single(tileSetData.Tiles, t => t.CustomProperties["navigation.walkable"] == "false");
            Assert.NotEqual(walkableTile.Id, blockedTile.Id);

            // Registered in the catalog like every other writer's assets.
            var assetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos, info => info.FileName == Path.Combine("Data", "Navigation.tileset"));
            Assert.Equal("Navigation", assetInfo.Name);
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
    public void ConvertMaps_WithoutCompanionFile_CountsMapAsWithoutCellsAndAddsNoLayer()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, mapIndex: 0);
            var mapLocations = new Dictionary<int, MapLocation> { [0] = new MapLocation("TestZone", "Test Map-0") };

            // Delete the companion the navigation layer needs, keeping the tileMap Phase 1 already
            // wrote - this map must be skipped (no layer), not fail the run.
            File.Delete(Path.Combine(inputDirectory, "data", "tiled", "map_0.alundra.json"));

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            NavigationWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.False(report.Counters.ContainsKey("Navigation.Layers"));
            Assert.Equal(1, report.Counters["Navigation.MapsWithoutCells"]);

            var tileMapPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-0", "tilemap", "Test Map-0.tileMap");
            var tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
            Assert.DoesNotContain(tileMapData.Layers, layer => layer.Name == "Navigation");
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
    /// Real map 389 (the intro map): cell (18,57) - Walkability 0 - must be W; the map's real B-cell
    /// count under M = 0x40 is asserted at its ACTUAL value (0 - the intro's walls are height-based,
    /// never encoded in Walkability/GroundProperty, a documented deviation, not an assumption).
    /// Skips when data-extracted/ is not present, same convention as SpriteBankReaderAnimSetsTests.
    /// </summary>
    [Fact]
    public void ConvertMaps_OnTheReal389Map_TheNavigationLayerMatchesTheRealCellData()
    {
        var realFiles = FindReal389Files();
        if (realFiles is null)
        {
            return; // data-extracted/ not present in this environment; nothing to assert against.
        }

        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            var tiledDirectory = Path.Combine(dataDirectory, "tiled");
            Directory.CreateDirectory(tiledDirectory);

            File.Copy(realFiles.Value.NativeMapJson, Path.Combine(dataDirectory, "map_389.json"));
            File.Copy(realFiles.Value.Tmj, Path.Combine(tiledDirectory, "map_389.tmj"));
            File.Copy(realFiles.Value.TilesetTsj, Path.Combine(tiledDirectory, "map_389_tileset.tsj"));
            File.Copy(realFiles.Value.TilesetPng, Path.Combine(tiledDirectory, "map_389_tileset.png"));
            File.Copy(realFiles.Value.AlundraJson, Path.Combine(tiledDirectory, "map_389.alundra.json"));

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [389] = new MapLocation("The Klark", "Ship Klark (beginning)-389"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: new[] { 389 }, mapLocations, report);
            NavigationWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: new[] { 389 }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Navigation.Layers"]);

            // Independently derived from data-extracted/data/tiled/map_389.alundra.json: every one of
            // the map's 3120 cells has (Walkability | GroundProperty&lt;&lt;8) &amp; 0x40 == 0 -
            // Walkability never sets bit 6 anywhere on this map (its walls are height-based, outside
            // what this grid encodes - documented deviation, see the class doc).
            Assert.Equal(3120, report.Counters["Navigation.WalkableCells"]);
            Assert.Equal(0, report.Counters["Navigation.BlockedCells"]);

            var tileMapPath = Path.Combine(
                outputDirectory, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap", "Ship Klark (beginning)-389.tileMap");
            var tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));

            var navigationLayerIndex = tileMapData.Layers.FindIndex(layer => layer.Name == "Navigation");
            Assert.True(navigationLayerIndex >= 0);
            var navigationLayer = tileMapData.Layers[navigationLayerIndex];

            Assert.Equal("grid", navigationLayer.CustomProperties["navigation.role"]);
            Assert.Equal(TileMapDepthRole.CollisionOnly, navigationLayer.Depth.Role);

            // Cell (18,57) -> W (tile id 0).
            Assert.Equal(0, tileMapData.GetTileId(navigationLayerIndex, 18, 57));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static (string NativeMapJson, string Tmj, string TilesetTsj, string TilesetPng, string AlundraJson)? FindReal389Files()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var dataExtracted = Path.Combine(directory.FullName, "data-extracted");
            var nativeMapJson = Path.Combine(dataExtracted, "data", "map_389.json");
            var tmj = Path.Combine(dataExtracted, "data", "tiled", "map_389.tmj");
            var tilesetTsj = Path.Combine(dataExtracted, "data", "tiled", "map_389_tileset.tsj");
            var tilesetPng = Path.Combine(dataExtracted, "data", "tiled", "map_389_tileset.png");
            var alundraJson = Path.Combine(dataExtracted, "data", "tiled", "map_389.alundra.json");

            if (File.Exists(nativeMapJson) && File.Exists(tmj) && File.Exists(tilesetTsj)
                && File.Exists(tilesetPng) && File.Exists(alundraJson))
            {
                return (nativeMapJson, tmj, tilesetTsj, tilesetPng, alundraJson);
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Same fixture shape as CellMetadataWriterTests.WriteMapFixture, narrowed to a 1x3 map so the
    /// three synthetic mask cases (bit 6 walkability, plain walkability bit, ground property alone)
    /// each get their own cell.
    /// </summary>
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
                "tilecount": 1,
                "columns": 1,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 24,
                "imageheight": 16,
                "tiles": [
                    { "id": 0, "properties": [ { "name": "TileId", "type": "int", "value": 100 } ] }
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
                        "data": [1, 1, 1]
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
                    { "Index": 0, "X": 0, "Y": 0, "Walkability": 64, "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 },
                    { "Index": 1, "X": 0, "Y": 1, "Walkability": 1,  "GroundProperty": 0, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 },
                    { "Index": 2, "X": 0, "Y": 2, "Walkability": 0,  "GroundProperty": 128, "Slope": 0, "Height": 0, "WallTilesOffset": -1, "TileId": 100, "Palette": 1, "Tile": 1, "Flags": 0 }
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
                        { "TileX": 0, "TileY": 1 },
                        { "TileX": 0, "TileY": 2 }
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
