using System.Linq;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers PlayerSetupWriter (docs/plan-conversion-totale.md E2): Entities/Alundra/Alundra.gameMode,
/// Data/Alundra.buttonsMapping, and (through WorldWriter) every world's
/// player_startup_settings_asset_id pointing at the gameMode.
/// </summary>
public class PlayerSetupWriterTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void WriteGameMode_WritesTheHeroPawnAndControllerClass()
    {
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var heroEntityAssetId = Guid.NewGuid();
            var prefabAssetIdsByBankKey = new Dictionary<string, Guid> { ["alundra_0"] = heroEntityAssetId };
            var report = new ConversionReport();

            var gameModeAssetId = PlayerSetupWriter.WriteGameMode(outputDirectory, prefabAssetIdsByBankKey, report);

            Assert.Empty(report.Errors);
            // Deterministic: same id every run, so the 483 worlds can reference it stably.
            Assert.Equal(Ids.For("gameMode:alundra"), gameModeAssetId);

            var relativePath = Path.Combine("Entities", "Alundra", "Alundra.gameMode");
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Assert.True(File.Exists(fullPath));

            var assetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos, info => info.FileName == relativePath);
            Assert.Equal("AlundraPlayer", assetInfo.Name);
            Assert.Equal(gameModeAssetId, assetInfo.Id);

            var document = JObject.Parse(File.ReadAllText(fullPath));
            Assert.Equal(gameModeAssetId.ToString(), (string?)document["id"]);
            Assert.Equal(heroEntityAssetId.ToString(), (string?)document["default_pawn_asset_id"]);
            Assert.Equal("AlundraPlayerController", (string?)document["player_controller_class"]);

            // Round-trips through the engine's own loader.
            var settings = new CasaEngine.Framework.Gameplay.PlayerStartupSettings();
            settings.Load(document);
            Assert.Equal(gameModeAssetId, settings.Id);
            Assert.Equal(heroEntityAssetId, settings.DefaultPawnAssetId);
            Assert.Equal("AlundraPlayerController", settings.PlayerControllerClass);
            Assert.Equal("AIController", settings.AIControllerClass); // engine default, left untouched.
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteGameMode_WithoutTheHeroBank_ReportsAnErrorAndWritesNothing()
    {
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            var gameModeAssetId = PlayerSetupWriter.WriteGameMode(
                outputDirectory, new Dictionary<string, Guid>(), report);

            Assert.Equal(Guid.Empty, gameModeAssetId);
            Assert.Single(report.Errors);
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Entities", "Alundra", "Alundra.gameMode")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteButtonsMapping_WritesTheNineDigitalActionsAndTheFourLeftStickAxes()
    {
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            var buttonsMappingAssetId = PlayerSetupWriter.WriteButtonsMapping(outputDirectory, report);

            Assert.Equal(Ids.For("buttonsMapping:alundra"), buttonsMappingAssetId);
            Assert.Equal(1, report.Counters["PlayerSetup.ButtonsMappings"]);
            Assert.Equal(13, report.Counters["PlayerSetup.ButtonsMappingActions"]);

            var relativePath = Path.Combine("Data", "Alundra.buttonsMapping");
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Assert.True(File.Exists(fullPath));

            var assetInfo = Assert.Single(
                EditorAssetCatalogService.AssetInfos, info => info.FileName == relativePath);
            Assert.Equal("AlundraButtons", assetInfo.Name);

            var document = JObject.Parse(File.ReadAllText(fullPath));

            // Round-trips through the engine's own loader.
            var buttonsMapping = new CasaEngine.Framework.Input.ButtonsMapping();
            buttonsMapping.Load(document);
            Assert.Equal(buttonsMappingAssetId, buttonsMapping.Id);
            Assert.Equal(13, buttonsMapping.Buttons.Count);

            AssertBinding(buttonsMapping, "MoveUp", Microsoft.Xna.Framework.Input.Keys.Up, Microsoft.Xna.Framework.Input.Buttons.DPadUp);
            AssertBinding(buttonsMapping, "MoveDown", Microsoft.Xna.Framework.Input.Keys.Down, Microsoft.Xna.Framework.Input.Buttons.DPadDown);
            AssertBinding(buttonsMapping, "MoveLeft", Microsoft.Xna.Framework.Input.Keys.Left, Microsoft.Xna.Framework.Input.Buttons.DPadLeft);
            AssertBinding(buttonsMapping, "MoveRight", Microsoft.Xna.Framework.Input.Keys.Right, Microsoft.Xna.Framework.Input.Buttons.DPadRight);
            // PSX Cross (PlayerManager.cs:413).
            AssertBinding(buttonsMapping, "Jump", Microsoft.Xna.Framework.Input.Keys.Space, Microsoft.Xna.Framework.Input.Buttons.A);
            // PSX Square (PlayerManager.cs:549/964).
            AssertBinding(buttonsMapping, "Attack", Microsoft.Xna.Framework.Input.Keys.X, Microsoft.Xna.Framework.Input.Buttons.X);
            // PSX Circle (PlayerManager.cs:1904).
            AssertBinding(buttonsMapping, "UseItem", Microsoft.Xna.Framework.Input.Keys.C, Microsoft.Xna.Framework.Input.Buttons.B);
            // PSX Triangle (PlayerManager.cs:430).
            AssertBinding(buttonsMapping, "Sprint", Microsoft.Xna.Framework.Input.Keys.LeftShift, Microsoft.Xna.Framework.Input.Buttons.Y);
            AssertBinding(buttonsMapping, "Menu", Microsoft.Xna.Framework.Input.Keys.Escape, Microsoft.Xna.Framework.Input.Buttons.Start);

            // The nine original actions stay digital...
            Assert.All(
                buttonsMapping.Buttons.Where(b => !b.Name.EndsWith("Stick", System.StringComparison.Ordinal)),
                button => Assert.Equal(
                    CasaEngine.Engine.Input.ButtonBehaviors.DigitalInput, button.ButtonBehavior));

            // ...and the four left-stick actions are analog (user report, 2026-08-26: the stick moved
            // nothing because only the D-pad was bound). An InputMapping is either digital or analog and
            // never both, which is exactly why these are separate actions rather than extra slots on the
            // four Move* entries; AlundraPlayerController.ActionBits aliases them onto the same PSX bits.
            AssertLeftStickBinding(buttonsMapping, "MoveUpStick", CasaEngine.Engine.Input.AnalogAxis.LeftStickY, invert: false);
            AssertLeftStickBinding(buttonsMapping, "MoveDownStick", CasaEngine.Engine.Input.AnalogAxis.LeftStickY, invert: true);
            AssertLeftStickBinding(buttonsMapping, "MoveRightStick", CasaEngine.Engine.Input.AnalogAxis.LeftStickX, invert: false);
            AssertLeftStickBinding(buttonsMapping, "MoveLeftStick", CasaEngine.Engine.Input.AnalogAxis.LeftStickX, invert: true);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertWorlds_AfterPlayerSetup_EveryWorldReferencesTheSameGameMode()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteHeroSpriteFixture(inputDirectory);
            WriteMapFixture(inputDirectory, 4);
            WriteMapFixture(inputDirectory, 5);

            var mapLocations = new Dictionary<int, Readers.MapLocation>
            {
                [4] = new Readers.MapLocation("TestZone", "Test Map-4"),
                [5] = new Readers.MapLocation("TestZone", "Test Map-5"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            var prefabAssetIdsByBankKey = SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);
            var gameModeAssetId = PlayerSetupWriter.WriteGameMode(outputDirectory, prefabAssetIdsByBankKey, report);
            PlayerSetupWriter.WriteButtonsMapping(outputDirectory, report);

            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { 4, 5 }, mapLocations, gameModeAssetId, report);

            Assert.Empty(report.Errors);
            Assert.NotEqual(Guid.Empty, gameModeAssetId);
            Assert.Equal(2, report.Counters["Worlds"]);

            foreach (var mapName in new[] { "Test Map-4", "Test Map-5" })
            {
                var worldPath = Path.Combine(outputDirectory, "Maps", "TestZone", mapName, $"{mapName}.world");
                var worldDocument = JObject.Parse(File.ReadAllText(worldPath));
                var world = new World();
                world.Load(worldDocument);
                Assert.Equal(gameModeAssetId, world.PlayerStartupSettingsAssetId);
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

    /// <summary>
    /// One half-axis of the left stick. InputMapping.Update reads the axis, negates it when Invert is
    /// set, and reports Pressed once the result reaches DeadZone - so one axis needs two mappings, one
    /// per direction, and MonoGame's LeftStickY being positive UP is why up is not inverted.
    /// The dead zone is pinned at 0.5 on purpose: a real stick's gate is circular, so at 45 degrees each
    /// axis only reaches about 0.707 - the digital bindings' own 0.75 placeholder would leave the four
    /// cardinals working while making every DIAGONAL physically unreachable.
    /// </summary>
    private static void AssertLeftStickBinding(
        CasaEngine.Framework.Input.ButtonsMapping buttonsMapping, string actionName,
        CasaEngine.Engine.Input.AnalogAxis expectedAxis, bool invert)
    {
        var binding = Assert.Single(buttonsMapping.Buttons, button => button.Name == actionName);
        Assert.Equal(CasaEngine.Engine.Input.ButtonBehaviors.AnalogInput, binding.ButtonBehavior);
        Assert.Equal(expectedAxis, binding.AnalogAxis);
        Assert.Equal(invert, binding.Invert);
        Assert.Equal(0.5f, binding.DeadZone);
        Assert.True(binding.DeadZone < 0.707f, "a dead zone at or above 0.707 makes stick diagonals unreachable");
    }

    private static void AssertBinding(
        CasaEngine.Framework.Input.ButtonsMapping buttonsMapping, string actionName,
        Microsoft.Xna.Framework.Input.Keys key, Microsoft.Xna.Framework.Input.Buttons alternativeGamePadButton)
    {
        var binding = Assert.Single(buttonsMapping.Buttons, button => button.Name == actionName);
        Assert.Equal(key, binding.KeyButton.Key);
        Assert.Equal(alternativeGamePadButton, binding.AlternativeKeyButton.GamePadButton);
    }

    // Minimal map_alundra.json with one hero record (Sector5Id 0 - "Alundra" in EntityNames.csv row
    // 0) and no animations, just enough for SpriteWriter to emit the Entities/Alundra/Alundra.entity
    // prefab that PlayerSetupWriter's "alundra_0" lookup needs.
    private static void WriteHeroSpriteFixture(string inputDirectory)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

        File.WriteAllText(
            Path.Combine(dataDirectory, "map_alundra.json"),
            """
            {
                "SpriteInfo": {
                    "SpriteRecords": [
                        {
                            "Header": {
                                "Sector5Id": 0, "MoreFlags": 0, "CanPickup": 0, "FlagsPortraitShadowType": 0,
                                "ProgramLoad": 0, "ProgramTick": 0, "ProgramTouch": 0, "ProgramDeactivate": 0,
                                "ProgramInteract": 0,
                                "OffsetX": 0, "OffsetY": 0, "OffsetZ": 0, "SizeX": 0, "SizeY": 0, "SizeZ": 0,
                                "Contents": 0
                            },
                            "AnimSets": []
                        }
                    ],
                    "SpriteEffectRecords": []
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
                "SpriteInfo": {
                    "Entities": { "Entities": [] },
                    "MapEvents": { "Records": [] }
                }
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
