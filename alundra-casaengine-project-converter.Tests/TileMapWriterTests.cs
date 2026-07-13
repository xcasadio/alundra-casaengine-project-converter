using AlundraCasaEngineProjectConverter;
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

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, report);

            Assert.Equal(2, report.Counters["Maps"]);
            Assert.Empty(report.Errors);

            var map0TileMapPath = Path.Combine(outputDirectory, "Maps", "map_0.tileMap");
            var map0TileSetPath = Path.Combine(outputDirectory, "Maps", "map_0.tileset");
            Assert.True(File.Exists(map0TileMapPath));
            Assert.True(File.Exists(map0TileSetPath));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "map_0.tmj")));

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

            var map5TileSetPath = Path.Combine(outputDirectory, "Maps", "map_5.tileset");
            Assert.True(File.Exists(map5TileSetPath));

            var animatedTileSetData = new TileSetData();
            animatedTileSetData.Load(JObject.Parse(File.ReadAllText(map5TileSetPath)));
            Assert.IsType<AnimatedTileData>(animatedTileSetData.Tiles[0]);
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

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();
            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());

            var report = new ConversionReport();
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: new[] { 1 }, report);

            Assert.Equal(1, report.Counters["Maps"]);
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Maps", "map_0.tileMap")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "map_1.tileMap")));
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
                "tilecount": 1,
                "columns": 1,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 24,
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
                "height": 1,
                "tilewidth": 24,
                "tileheight": 16,
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
