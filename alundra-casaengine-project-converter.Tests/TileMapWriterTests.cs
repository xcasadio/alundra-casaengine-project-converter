using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class TileMapWriterTests
{
    // Minimal 8-byte PNG signature: enough for the importer's file-copy pipeline, which does not
    // decode image content. Not related to any Alundra asset.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertMaps_WithStaticAndAnimatedFixtures_ProducesReloadableAssets()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);

            WriteStaticMapFixture(tiledDirectory, mapIndex: 0);
            WriteAnimatedMapFixture(tiledDirectory, mapIndex: 5);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [0] = new MapLocation("TestZone", "Static Map-0"),
                [5] = new MapLocation("TestZone", "Animated Map-5"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Equal(2, report.Counters["Maps"]);
            Assert.Empty(report.Errors);

            var map0TileMapPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Static Map-0", "tilemap", "Static Map-0.tileMap");
            var map0TileSetPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Static Map-0", "tilemap", "Static Map-0.tileset");
            Assert.True(File.Exists(map0TileMapPath));
            Assert.True(File.Exists(map0TileSetPath));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Static Map-0", "tilemap", "Static Map-0.tmj")));

            var tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(map0TileMapPath)));
            Assert.Equal(2, tileMapData.MapSize.Width);
            Assert.Equal(2, tileMapData.MapSize.Height);
            Assert.Single(tileMapData.ObjectLayers);
            Assert.Equal("Entities", tileMapData.ObjectLayers[0].Name);
            Assert.Single(tileMapData.ObjectLayers[0].Objects);

            var tileSetData = new TileSetData();
            tileSetData.Load(JObject.Parse(File.ReadAllText(map0TileSetPath)));
            Assert.Equal(2, tileSetData.Tiles.Count);

            var map5TileSetPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Animated Map-5", "tilemap", "Animated Map-5.tileset");
            Assert.True(File.Exists(map5TileSetPath));

            var animatedTileSetData = new TileSetData();
            animatedTileSetData.Load(JObject.Parse(File.ReadAllText(map5TileSetPath)));
            var animatedTile = Assert.IsType<AnimatedTileData>(animatedTileSetData.Tiles[0]);
            var staticTile = Assert.IsType<StaticTileData>(animatedTileSetData.Tiles[1]);
            Assert.NotNull(staticTile);

            // Duration math: the analyser bakes duration_ms straight into the Tiled fixture (no
            // further PSX-tick conversion happens in the converter), so the round-tripped frame
            // duration must equal it exactly.
            Assert.Equal(2, animatedTile.Frames.Count);
            Assert.All(animatedTile.Frames, frame => Assert.Equal(160, frame.DurationMilliseconds));

            // TileSets.AnimatedTiles counts every AnimatedTileData entry across the corpus; the
            // static map contributes none, the animated map contributes exactly its one animated tile
            // (tile id outside the animated range - tile 1 here - stays a StaticTileData and is not
            // counted).
            Assert.Equal(1, report.Counters["TileSets.AnimatedTiles"]);
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
    public void ConvertMaps_WithMapFilter_ConvertsOnlyRequestedMaps()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);

            WriteStaticMapFixture(tiledDirectory, mapIndex: 0);
            WriteStaticMapFixture(tiledDirectory, mapIndex: 1);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [0] = new MapLocation("TestZone", "First Map-0"),
                [1] = new MapLocation("TestZone", "Second Map-1"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();
            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: new[] { 1 }, mapLocations, report);

            Assert.Equal(1, report.Counters["Maps"]);
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "First Map-0", "tilemap", "First Map-0.tileMap")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Second Map-1", "tilemap", "Second Map-1.tileMap")));
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
    public void ConvertMaps_WithStaleGenerationsInTilemapDirectory_PurgesThemAndCountsThem()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);
            WriteStaticMapFixture(tiledDirectory, mapIndex: 0);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [0] = new MapLocation("TestZone", "Static Map-0"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();
            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            // Seed the destination tilemap/ directory as if a previous in-place run had already left
            // behind the exact orphan generation D-N-2 targets: a suffixed tileset PNG + .texture
            // wrapper the engine's Tiled importer produces on a collision (fact 1), plus a stale
            // .tmj from an earlier run at the same path.
            var tilemapDirectory = Path.Combine(outputDirectory, "Maps", "TestZone", "Static Map-0", "tilemap");
            Directory.CreateDirectory(tilemapDirectory);
            var staleFileNames = new[]
            {
                "Static Map-0_tileset_2.png",
                "Static Map-0_tileset_2.texture",
                "stale-leftover.tmj",
            };
            foreach (var staleFileName in staleFileNames)
            {
                File.WriteAllBytes(Path.Combine(tilemapDirectory, staleFileName), FakePngBytes);
            }

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Equal(1, report.Counters["Maps"]);
            Assert.Empty(report.Errors);
            Assert.Equal(staleFileNames.Length, report.Counters["Phase1.StalePagesPurged"]);

            foreach (var staleFileName in staleFileNames)
            {
                Assert.False(
                    File.Exists(Path.Combine(tilemapDirectory, staleFileName)),
                    $"stale file '{staleFileName}' should have been purged.");
            }

            // This run's fresh outputs, including the destination .tmj itself - the mutation-killer
            // for a purge moved after the .tmj copy: that ordering would delete the freshly-copied
            // .tmj right along with the stale files (D-N-2's own worked example).
            Assert.True(File.Exists(Path.Combine(tilemapDirectory, "Static Map-0.tmj")));
            Assert.True(File.Exists(Path.Combine(tilemapDirectory, "Static Map-0.tileMap")));
            Assert.True(File.Exists(Path.Combine(tilemapDirectory, "Static Map-0.tileset")));
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
    public void ConvertMaps_TwiceOverSameInput_ProducesIdenticalDeterministicIds()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory1 = CreateTempDirectory();
        var outputDirectory2 = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);
            WriteStaticMapFixture(tiledDirectory, mapIndex: 0);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [0] = new MapLocation("TestZone", "Static Map-0"),
            };

            var catalog1 = ConvertAndReadCatalog(inputDirectory, outputDirectory1, mapLocations);
            var catalog2 = ConvertAndReadCatalog(inputDirectory, outputDirectory2, mapLocations);

            const string mapBaseName = "Static Map-0";
            var tilemapDirectory = Path.Combine("Maps", "TestZone", mapBaseName, "tilemap");
            var tmjRelativePath = Path.Combine(tilemapDirectory, $"{mapBaseName}.tmj");
            var tileSetRelativePath = Path.Combine(tilemapDirectory, $"{mapBaseName}.tileset");
            var rawTextureRelativePath = Path.Combine(tilemapDirectory, "map_0_tileset.png");
            var wrapperRelativePath = Path.ChangeExtension(rawTextureRelativePath, ".texture");
            var tileMapRelativePath = Path.Combine(tilemapDirectory, $"{mapBaseName}.tileMap");

            // Phase 1's five per-map ids, plus the two texture ids they alias (raw PNG + wrapper
            // are the same two entries under D-N-5's own prefixes) - all seven equal across the two
            // independent runs, and all seven equal to the Ids.For value recomputed right here.
            AssertIdenticalAndDeterministic(catalog1, catalog2, tmjRelativePath, "tmj:" + tmjRelativePath);
            AssertIdenticalAndDeterministic(catalog1, catalog2, tileSetRelativePath, "tileset-doc:" + tileSetRelativePath);
            AssertIdenticalAndDeterministic(catalog1, catalog2, rawTextureRelativePath, "texture-raw:" + rawTextureRelativePath);
            AssertIdenticalAndDeterministic(catalog1, catalog2, wrapperRelativePath, "texture-wrapper:" + wrapperRelativePath);
            AssertIdenticalAndDeterministic(catalog1, catalog2, tileMapRelativePath, "tilemap-doc:" + tileMapRelativePath);

            // Regression guard for the Name spec (D-N-4): the pre-seeded "name" fields must match
            // exactly what the pre-change engine naming produced, byte for byte.
            Assert.Equal($"{mapBaseName}.tmj", catalog1[tmjRelativePath].Name);
            Assert.Equal($"{mapBaseName}_TileSet", catalog1[tileSetRelativePath].Name);
            Assert.Equal($"{mapBaseName}_map_0_tileset.png", catalog1[rawTextureRelativePath].Name);
            Assert.Equal($"{mapBaseName}_map_0_tileset", catalog1[wrapperRelativePath].Name);
            Assert.Equal(mapBaseName, catalog1[tileMapRelativePath].Name);

            Assert.False(catalog1.ContainsKey("Phase1.IdSeedingSkipped"));

            var tileSetJson = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory1, tileSetRelativePath)));
            Assert.Equal(catalog1[tileSetRelativePath].Id.ToString(), (string)tileSetJson["id"]!);
            var tileMapJson = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory1, tileMapRelativePath)));
            Assert.Equal(catalog1[tileMapRelativePath].Id.ToString(), (string)tileMapJson["id"]!);
            var wrapperJson = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory1, wrapperRelativePath)));
            Assert.Equal(catalog1[wrapperRelativePath].Id.ToString(), (string)wrapperJson["id"]!);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory1, recursive: true);
            Directory.Delete(outputDirectory2, recursive: true);
        }
    }

    [Fact]
    public void ConvertMaps_WithTmjReferencingTwoTilesets_SkipsSeedingAndCounts()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);
            WriteTwoTilesetMapFixture(tiledDirectory, mapIndex: 0);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [0] = new MapLocation("TestZone", "Two Tilesets Map-0"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();
            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Equal(1, report.Counters["Maps"]);
            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Phase1.IdSeedingSkipped"]);
            Assert.Contains(report.Warnings, warning => warning.Contains("Phase 1 asset id pre-seeding skipped", StringComparison.Ordinal));

            // The import itself still succeeds (falls back to Guid.NewGuid for this map only).
            Assert.True(File.Exists(Path.Combine(
                outputDirectory, "Maps", "TestZone", "Two Tilesets Map-0", "tilemap", "Two Tilesets Map-0.tileMap")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void AssertIdenticalAndDeterministic(
        Dictionary<string, (Guid Id, string Name)> catalog1,
        Dictionary<string, (Guid Id, string Name)> catalog2,
        string relativeFileName,
        string idKey)
    {
        var expectedId = Ids.For(idKey);
        Assert.Equal(expectedId, catalog1[relativeFileName].Id);
        Assert.Equal(expectedId, catalog2[relativeFileName].Id);
    }

    private static Dictionary<string, (Guid Id, string Name)> ConvertAndReadCatalog(
        string inputDirectory, string outputDirectory, IReadOnlyDictionary<int, MapLocation> mapLocations)
    {
        EngineEnvironment.ProjectPath = outputDirectory;
        EditorAssetCatalogService.Clear();
        ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

        var report = new ConversionReport();
        TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
        Assert.Empty(report.Errors);

        var catalogPath = Path.Combine(outputDirectory, "AssetInfos.json");
        var assetInfosArray = (JArray)JObject.Parse(File.ReadAllText(catalogPath))["asset_infos"]!;
        var catalog = new Dictionary<string, (Guid Id, string Name)>();
        foreach (var entry in assetInfosArray)
        {
            var fileName = (string)entry["file_name"]!;
            catalog[fileName] = (Guid.Parse((string)entry["id"]!), (string)entry["name"]!);
        }

        return catalog;
    }

    private static void WriteTwoTilesetMapFixture(string tiledDirectory, int mapIndex)
    {
        var baseName = $"map_{mapIndex}";
        File.WriteAllBytes(Path.Combine(tiledDirectory, $"{baseName}_tileset.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(tiledDirectory, $"{baseName}_tileset_b.png"), FakePngBytes);

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
                "imageheight": 16
            }
            """.Replace("INDEX", mapIndex.ToString()));

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}_tileset_b.tsj"),
            """
            {
                "type": "tileset",
                "name": "tileset_b",
                "tilewidth": 24,
                "tileheight": 16,
                "tilecount": 1,
                "columns": 1,
                "image": "map_INDEX_tileset_b.png",
                "imagewidth": 24,
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
                "width": 1,
                "height": 1,
                "tilewidth": 24,
                "tileheight": 16,
                "tilesets": [
                    { "firstgid": 1, "source": "map_INDEX_tileset.tsj" },
                    { "firstgid": 2, "source": "map_INDEX_tileset_b.tsj" }
                ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 1,
                        "height": 1,
                        "data": [1]
                    }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));
    }

    [Fact]
    public void ConvertMaps_WithoutMapLocation_FallsBackToUncategorized()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
            Directory.CreateDirectory(tiledDirectory);

            WriteStaticMapFixture(tiledDirectory, mapIndex: 7);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();
            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(
                inputDirectory, outputDirectory, mapFilter: null, new Dictionary<int, MapLocation>(), report);

            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "Uncategorized", "map_7", "tilemap", "map_7.tileMap")));
            Assert.Contains(report.Warnings, warning => warning.Contains("not listed in maps.json", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteStaticMapFixture(string tiledDirectory, int mapIndex)
    {
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
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 2,
                        "height": 2,
                        "data": [1, 2, 2, 1]
                    },
                    {
                        "type": "objectgroup",
                        "name": "Entities",
                        "objects": [
                            { "id": 1, "name": "Entity_0", "type": "Entity", "x": 24, "y": 16, "width": 24, "height": 16, "properties": [] }
                        ]
                    }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));
    }

    private static void WriteAnimatedMapFixture(string tiledDirectory, int mapIndex)
    {
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
                "imageheight": 16,
                "tiles": [
                    {
                        "id": 0,
                        "animation": [
                            { "tileid": 0, "duration": 160 },
                            { "tileid": 0, "duration": 160 }
                        ]
                    }
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
                "height": 2,
                "tilewidth": 24,
                "tileheight": 16,
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 1,
                        "height": 2,
                        "data": [1, 2]
                    }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
