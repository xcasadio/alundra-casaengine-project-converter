using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers the SpriteTableIndex -&gt; bank key -&gt; prefab asset id resolution
/// <see cref="EntityPrefabLinkWriter"/> writes into each map's .tileMap "Entities" records. The
/// bank-index cross-check (map 389, Entity_0, SpriteTableIndex 25, "alundra_25") is documented on
/// EntityPrefabLinkWriter itself and reproduced here as the isMapSprite == false case.
/// </summary>
public class EntityPrefabLinkWriterTests
{
    // Minimal 8-byte PNG signature: enough for the importer's file-copy pipeline, which does not
    // decode image content. Not related to any Alundra asset.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void LinkMaps_ResolvesGlobalAndMapLocalRecordsAndLeavesUnresolvedOnesAlone()
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

            var globalPrefabId = Guid.NewGuid();
            var mapLocalPrefabId = Guid.NewGuid();
            var prefabAssetIdsByBankKey = new Dictionary<string, Guid>
            {
                ["alundra_25"] = globalPrefabId, // isMapSprite == false: SpriteDirection 64 (0x40)
                ["7"] = mapLocalPrefabId, // isMapSprite == true: SpriteDirection 0xC0 (0x40 | 0x80)
            };

            // mapFilter is non-null (a subset run) so the full-run PrefabLinks invariant check is
            // skipped - this fixture's map deliberately carries unresolved records to exercise that
            // path, which would otherwise trip the invariant.
            EntityPrefabLinkWriter.LinkMaps(
                inputDirectory, outputDirectory, mapFilter: new[] { 0 }, mapLocations, prefabAssetIdsByBankKey, report);

            Assert.Equal(1, report.Counters["Maps.PrefabLinks"]);
            Assert.Equal(2, report.Counters["PrefabLinks.Resolved"]);
            Assert.Equal(2, report.Counters["PrefabLinks.Unresolved"]);

            var tileMapPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Test Map-0", "tilemap", "Test Map-0.tileMap");
            var tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));

            var entitiesLayer = Assert.Single(
                tileMapData.ObjectLayers, layer => layer.Name == "Entities");
            Assert.Equal(4, entitiesLayer.Objects.Count);

            var globalRecord = entitiesLayer.Objects.Single(o => o.Name == "Entity_0");
            Assert.Equal(globalPrefabId.ToString(), globalRecord.CustomProperties["PrefabAssetId"]);

            var mapLocalRecord = entitiesLayer.Objects.Single(o => o.Name == "Entity_1");
            Assert.Equal(mapLocalPrefabId.ToString(), mapLocalRecord.CustomProperties["PrefabAssetId"]);

            // Disabled record: no known bank either, but expected, so no warning was raised for it.
            var disabledRecord = entitiesLayer.Objects.Single(o => o.Name == "Entity_2");
            Assert.False(disabledRecord.CustomProperties.ContainsKey("PrefabAssetId"));

            // Enabled record referencing an unknown bank: unexpected, so a warning was raised.
            var unknownRecord = entitiesLayer.Objects.Single(o => o.Name == "Entity_3");
            Assert.False(unknownRecord.CustomProperties.ContainsKey("PrefabAssetId"));

            Assert.Contains(
                report.Warnings,
                warning => warning.Contains("Entity_3", StringComparison.Ordinal)
                           && warning.Contains("no known prefab", StringComparison.Ordinal));
            Assert.DoesNotContain(
                report.Warnings, warning => warning.Contains("Entity_2", StringComparison.Ordinal));
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
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 2,
                        "height": 2,
                        "data": [1, 2, 1, 1]
                    },
                    {
                        "type": "objectgroup",
                        "name": "Entities",
                        "objects": [
                            {
                                "id": 1, "name": "Entity_0", "type": "Entity",
                                "x": 0, "y": 0, "width": 24, "height": 16,
                                "properties": [
                                    { "name": "IsEnabled", "type": "int", "value": 1 },
                                    { "name": "SpriteDirection", "type": "int", "value": 64 },
                                    { "name": "SpriteTableIndex", "type": "int", "value": 25 }
                                ]
                            },
                            {
                                "id": 2, "name": "Entity_1", "type": "Entity",
                                "x": 24, "y": 0, "width": 24, "height": 16,
                                "properties": [
                                    { "name": "IsEnabled", "type": "int", "value": 1 },
                                    { "name": "SpriteDirection", "type": "int", "value": 192 },
                                    { "name": "SpriteTableIndex", "type": "int", "value": 7 }
                                ]
                            },
                            {
                                "id": 3, "name": "Entity_2", "type": "Entity",
                                "x": 0, "y": 16, "width": 24, "height": 16,
                                "properties": [
                                    { "name": "IsEnabled", "type": "int", "value": 0 },
                                    { "name": "SpriteDirection", "type": "int", "value": 0 },
                                    { "name": "SpriteTableIndex", "type": "int", "value": 511 }
                                ]
                            },
                            {
                                "id": 4, "name": "Entity_3", "type": "Entity",
                                "x": 24, "y": 16, "width": 24, "height": 16,
                                "properties": [
                                    { "name": "IsEnabled", "type": "int", "value": 1 },
                                    { "name": "SpriteDirection", "type": "int", "value": 64 },
                                    { "name": "SpriteTableIndex", "type": "int", "value": 511 }
                                ]
                            }
                        ]
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
