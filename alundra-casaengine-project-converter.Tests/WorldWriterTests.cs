using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace AlundraCasaEngineProjectConverter.Tests;

public class WorldWriterTests
{
    // Minimal 8-byte PNG signature: enough for the importer's file-copy pipeline, which does not
    // decode image content. Not related to any Alundra asset.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private const int NewGameMapIndex = 389;

    [Theory]
    // The documented New Game spawn: docs/demarrage-nouvelle-partie.md section 2.1 gives tile
    // (33, 59, 0) -> pixels (804, 952), and guidelines section 2.3 turns that into world Y = -952.
    [InlineData(33, 59, 0, 804, 952, 804f, -952f)]
    // Elevation goes up the screen, i.e. up in a Y-up frame: +tileZ * 16.
    [InlineData(33, 59, 2, 804, 952, 804f, -920f)]
    [InlineData(0, 0, 0, 12, 8, 12f, -8f)]
    public void ResolveTileCentreSpawn_ConvertsTileCoordinatesToTheCasaEngineFrame(
        int tileX, int tileY, int tileZ, int expectedPixelX, int expectedPixelY, float expectedWorldX, float expectedWorldY)
    {
        var spawn = WorldWriter.ResolveTileCentreSpawn(tileX, tileY, tileZ);

        Assert.Equal(expectedPixelX, spawn.PixelX);
        Assert.Equal(expectedPixelY, spawn.PixelY);
        Assert.Equal(expectedWorldX, spawn.WorldX);
        Assert.Equal(expectedWorldY, spawn.WorldY);
        Assert.Equal(0f, spawn.WorldZ);
    }

    [Fact]
    public void ConvertWorlds_WritesAWorldThatRoundTripsThroughTheEngine()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, NewGameMapIndex);
            var mapLocations = new Dictionary<int, MapLocation>
            {
                [NewGameMapIndex] = new MapLocation("The Klark", "Ship Klark (beginning)-389"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            // Narrowed run: the global invariants (483 worlds, 9 631 enabled entities, ...) only
            // hold for the whole data-extracted corpus, so WorldWriter skips them when --maps is
            // given, which is exactly what a fixture is.
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { NewGameMapIndex }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Worlds"]);
            Assert.Equal(1, report.Counters["Worlds.PlayerStartFromSaveData"]);
            Assert.False(report.Counters.ContainsKey("Worlds.PlayerStartPlaceholders"));

            var worldRelativePath = Path.Combine(
                "Maps", "The Klark", "Ship Klark (beginning)-389", "Ship Klark (beginning)-389.world");
            var worldFullPath = Path.Combine(outputDirectory, worldRelativePath);
            Assert.True(File.Exists(worldFullPath));

            // world-index.json points at exactly the file that was written, and at the same string
            // the asset catalog registered (GameManager resolves a world name by ordinal lookup on
            // that string).
            var index = JObject.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Maps", "world-index.json")));
            Assert.Equal(worldRelativePath, (string?)index["389"]);

            var worldAssetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos, assetInfo => assetInfo.FileName == worldRelativePath);
            Assert.Equal("Ship Klark (beginning)-389", worldAssetInfo.Name);

            // FirstWorldLoaded is derived, not hard-coded: it must be the path just produced.
            var projectSettings = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "AlundraGame.json")));
            Assert.Equal(worldRelativePath, (string?)projectSettings["FirstWorldLoaded"]);

            // Round-trip through the engine's own loaders. World.Load parses the document
            // headlessly; the entities inside an entity_reference are normally materialised by
            // AssetContentManager (which needs a running CasaEngineGame), so they are loaded here
            // with Entity.Load directly - that is the same code path, minus the asset manager.
            var worldDocument = JObject.Parse(File.ReadAllText(worldFullPath));
            var world = new World();
            world.Load(worldDocument);
            Assert.Equal(worldAssetInfo.Id, world.Id);
            Assert.Equal(Guid.Empty, world.PlayerStartupSettingsAssetId);

            var entityNodes = (JArray)worldDocument["entity_references"]!;
            Assert.Equal(3, entityNodes.Count);

            // The camera is a reference to the single shared asset, and it comes first:
            // DefaultRuntimeViewBootstrapper keeps the first entity carrying a CameraComponent.
            var cameraAssetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos,
                assetInfo => assetInfo.FileName == Path.Combine("Entities", "AlundraCamera.entity"));
            var cameraReferenceNode = (JObject)entityNodes[0];
            Assert.Equal(cameraAssetInfo.Id.ToString(), (string?)cameraReferenceNode["asset_id"]);
            Assert.Equal("camera", (string?)cameraReferenceNode["name"]);
            Assert.Null(cameraReferenceNode["entity"]);
            Assert.NotNull(cameraReferenceNode["initial_local_transform"]);

            // The engine reads the reference back the way it was written.
            var entityReference = new EntityReference();
            entityReference.Load(cameraReferenceNode);
            Assert.Equal(cameraAssetInfo.Id, entityReference.AssetId);
            Assert.Equal("camera", entityReference.Name);
            Assert.Equal(Vector3.One, entityReference.InitialLocalTransform.Scale);

            var tileMapEntity = LoadEntity(entityNodes, "tileMap");
            var tileMapComponent = Assert.IsType<TileMapComponent>(tileMapEntity.RootComponent);

            var tileMapDocument = JObject.Parse(File.ReadAllText(
                Path.Combine(
                    outputDirectory,
                    "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
                    "Ship Klark (beginning)-389.tileMap")));
            Assert.Equal(Guid.Parse((string)tileMapDocument["id"]!), tileMapComponent.TileMapDataAssetId);
            Assert.NotEqual(Guid.Empty, tileMapComponent.TileMapDataAssetId);

            var playerStartEntity = LoadEntity(entityNodes, "PlayerStart");
            var playerStartComponent = Assert.IsType<PlayerStartComponent>(playerStartEntity.RootComponent);
            Assert.Equal(804f, playerStartComponent.LocalPosition.X);
            Assert.Equal(-952f, playerStartComponent.LocalPosition.Y);
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
    public void ConvertWorlds_ForAMapWithoutAKnownSpawn_PlacesThePlayerStartAtTheMapCentreAndSaysSo()
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
            WorldWriter.ConvertWorlds(inputDirectory, outputDirectory, mapFilter: new[] { 4 }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Worlds.PlayerStartPlaceholders"]);

            // The invariant counters count source objects, not written entities: enabled entities
            // only, portals with a real DestMapId only, non-null records only.
            Assert.Equal(3, report.Counters["Worlds.Entities"]);
            Assert.Equal(2, report.Counters["Worlds.EntitiesEnabled"]);
            Assert.Equal(2, report.Counters["Worlds.Portals"]);
            Assert.Equal(1, report.Counters["Worlds.MapEvents"]);

            // The fixture map is 2 x 2 tiles of 24 x 16 px, so its centre is (24, -16).
            var worldDocument = JObject.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-4", "Test Map-4.world")));
            var playerStartEntity = LoadEntity((JArray)worldDocument["entity_references"]!, "PlayerStart");
            Assert.Equal(24f, playerStartEntity.RootComponent!.LocalPosition.X);
            Assert.Equal(-16f, playerStartEntity.RootComponent.LocalPosition.Y);

            // Map 389 was not converted, so the project must stay pointed at nothing rather than at
            // a world that does not exist.
            var projectSettings = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "AlundraGame.json")));
            Assert.Equal(string.Empty, (string?)projectSettings["FirstWorldLoaded"]);
            Assert.Contains(report.Warnings, warning => warning.Contains("FirstWorldLoaded is left empty", StringComparison.Ordinal));
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
    /// The shared camera asset. Its values are the pixel-perfect checklist of
    /// CasaEngineMonogame/docs/engine/rendering-2d-3d-spaces.md: Camera2dComponent, integer Zoom,
    /// PixelSnap on - and a Target at the centre of an Alundra map, which is the same point for all
    /// 483 of them (52 x 60 tiles of 24 x 16 px).
    /// </summary>
    [Fact]
    public void ConvertWorlds_WritesOneSharedCamera2dEntityForEveryWorld()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, NewGameMapIndex);
            WriteMapFixture(inputDirectory, mapIndex: 4);
            var mapLocations = new Dictionary<int, MapLocation>
            {
                [NewGameMapIndex] = new MapLocation("The Klark", "Ship Klark (beginning)-389"),
                [4] = new MapLocation("TestZone", "Test Map-4"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { NewGameMapIndex, 4 }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Worlds"]);
            // One asset for two worlds - that is the whole point.
            Assert.Equal(1, report.Counters["Worlds.SharedCameras"]);

            var cameraRelativePath = Path.Combine("Entities", "AlundraCamera.entity");
            var cameraFullPath = Path.Combine(outputDirectory, cameraRelativePath);
            Assert.True(File.Exists(cameraFullPath));

            var cameraAssetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos, assetInfo => assetInfo.FileName == cameraRelativePath);
            Assert.Equal("AlundraCamera", cameraAssetInfo.Name);

            var cameraDocument = JObject.Parse(File.ReadAllText(cameraFullPath));
            Assert.Equal(cameraAssetInfo.Id.ToString(), (string?)cameraDocument["id"]);
            Assert.Equal("Camera2dComponent", (string?)cameraDocument["root_component"]!["type"]);

            // Loaded through the engine, not just read as JSON: the point is that Camera2dComponent
            // gets its framing back, which the component this replaced could not do.
            var cameraEntity = new Entity();
            cameraEntity.Load(cameraDocument);
            var camera = Assert.IsType<Camera2dComponent>(cameraEntity.RootComponent);
            Assert.Equal(new Vector3(624f, -480f, 0f), camera.Target);
            Assert.Equal(1f, camera.Zoom);
            Assert.True(camera.PixelSnap);

            // Both worlds point at that one asset, and both point at it first.
            foreach (var worldRelativePath in new[]
                     {
                         Path.Combine("Maps", "The Klark", "Ship Klark (beginning)-389", "Ship Klark (beginning)-389.world"),
                         Path.Combine("Maps", "TestZone", "Test Map-4", "Test Map-4.world"),
                     })
            {
                var worldDocument = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, worldRelativePath)));
                var firstReference = (JObject)((JArray)worldDocument["entity_references"]!)[0];
                Assert.Equal(cameraAssetInfo.Id.ToString(), (string?)firstReference["asset_id"]);
                Assert.Equal("camera", (string?)firstReference["name"]);
            }
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
    public void ConvertWorlds_WithoutTileMapAsset_WarnsAndSkips()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, mapIndex: 4);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);

            // Phase 1 intentionally not run: no .tileMap asset exists yet.
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { 4 }, new Dictionary<int, MapLocation>(), report);

            Assert.Empty(report.Errors);
            Assert.False(report.Counters.ContainsKey("Worlds"));
            Assert.Contains(report.Warnings, warning => warning.Contains("no world written", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static Entity LoadEntity(JArray entityReferenceNodes, string entityName)
    {
        foreach (var entityReferenceNode in entityReferenceNodes)
        {
            // References to a separate .entity asset carry no inline entity - see
            // EntityReference.Load - so only the inlined ones can be materialised from the world.
            if (entityReferenceNode["entity"] is not JObject entityNode)
            {
                continue;
            }

            if ((string?)entityNode["name"] != entityName)
            {
                continue;
            }

            var entity = new Entity();
            entity.Load(entityNode);
            return entity;
        }

        Assert.Fail($"No entity named '{entityName}' in the world.");
        return null!;
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

        // Two enabled entities out of three records, two real portals out of three slots, one map
        // event: enough to prove the counters filter on IsEnabled / DestMapId / null.
        File.WriteAllText(
            Path.Combine(dataDirectory, $"{baseName}.json"),
            """
            {
                "Info": {
                    "Portals": [
                        { "X1": 5, "Y1": 6, "X2": 6, "Y2": 6, "DestMapId": 3, "DestTileX": 5, "DestTileY": 59, "ZLevel": 0, "Flags": 20482 },
                        { "X1": 0, "Y1": 0, "X2": 0, "Y2": 0, "DestMapId": 7, "DestTileX": 0, "DestTileY": 0, "ZLevel": 0, "Flags": 0 },
                        { "X1": 0, "Y1": 0, "X2": 0, "Y2": 0, "DestMapId": 0, "DestTileX": 0, "DestTileY": 0, "ZLevel": 0, "Flags": 0 }
                    ]
                },
                "Map": { "MapTiles": [] },
                "SpriteInfo": {
                    "Entities": {
                        "Entities": [
                            { "IsEnabled": 1, "SpriteTableIndex": 205, "XPos": 8, "YPos": 36, "Height": 10 },
                            { "IsEnabled": 1, "SpriteTableIndex": 12, "XPos": 4, "YPos": 4, "Height": 0 },
                            { "IsEnabled": 0, "SpriteTableIndex": 13, "XPos": 6, "YPos": 6, "Height": 0 },
                            null
                        ]
                    },
                    "MapEvents": {
                        "Records": [
                            { "X1": 0, "Y1": 0, "X2": 51, "Y2": 59, "EventCodesBIndex": 129, "Ub1": 0, "Ub2": 0, "Ub3": 0 },
                            null
                        ]
                    }
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
