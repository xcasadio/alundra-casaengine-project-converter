using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Every converted world must declare Alundra's simulation space: X/Y is the ground plane, Z is
/// elevation, which is SimulationSpacePolicyNames.TopDownElevation. Asserted both on the raw JSON
/// (the key the engine looks for) and through World.Load (the value it ends up with).
/// </summary>
public class WorldSpacePolicyTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertWorlds_DeclaresTheTopDownElevationSpacePolicy()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, mapIndex: 4);
            var mapLocations = new Dictionary<int, MapLocation> { [4] = new MapLocation("TestZone", "Test Map-4") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { 4 }, mapLocations, Ids.For("test:gameMode"), report);

            Assert.Empty(report.Errors);

            var worldDocument = JObject.Parse(File.ReadAllText(
                Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-4", "Test Map-4.world")));
            Assert.Equal(SimulationSpacePolicyNames.TopDownElevation, (string?)worldDocument["space_policy"]);

            var world = new World();
            world.Load(worldDocument);
            Assert.Equal(SimulationSpacePolicyNames.TopDownElevation, world.SpacePolicyName);

            // The name really is one the engine can build a policy from.
            Assert.IsType<TopDownElevationSimulationSpacePolicy>(
                SimulationSpacePolicy.CreateByName(world.SpacePolicyName));
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
            Path.Combine(dataDirectory, $"{baseName}.json"),
            """
            {
                "Info": { "Portals": [] },
                "Map": { "MapTiles": [] },
                "SpriteInfo": {
                    "Entities": { "Entities": [] },
                    "MapEvents": { "Records": [] }
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
