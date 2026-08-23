using System.Linq;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers E3.d's converter change (docs/plan-e3-collisions.md "Convertisseur"): ONLY the hero's own
/// prefab - map_alundra.json's Sector5Id 0 record (<c>bank.IsAlundraBank &amp;&amp; bank.Sector5Id == 0</c>,
/// SpriteBankReader.cs:187/:211) - gets a <see cref="CharacterControllerComponent"/>; every other
/// prefab (a regular map bank here, the other 394 real prefabs) gets none.
/// </summary>
public class SpriteWriterCharacterControllerTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_OnlyTheHeroBank_GetsACharacterControllerComponentWithTheExportedSettings()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

            // Hero bank: Sector5Id 0 inside map_alundra.json -> bank.IsAlundraBank && Sector5Id == 0.
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_alundra.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 0 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 100 }
                                        ] } },
                                        { "Delay": 1, "Images": null }
                                    ] },
                                    null, null, null
                                ] } ]
                            }
                        ],
                        "SpriteEffectRecords": []
                    }
                }
                """);

            // Non-hero bank: a regular map's own Sector5Id (never IsAlundraBank), same shape otherwise -
            // must NOT get a CharacterControllerComponent.
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 55 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 550 }
                                        ] } },
                                        { "Delay": 1, "Images": null }
                                    ] },
                                    null, null, null
                                ] } ]
                            }
                        ]
                    }
                }
                """);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            var prefabAssetIdsByBankKey = SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Entities.Prefabs"]);

            var heroAssetId = prefabAssetIdsByBankKey["alundra_0"];
            var nonHeroAssetId = prefabAssetIdsByBankKey["55"];

            var heroAssetInfo = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == heroAssetId);
            var nonHeroAssetInfo = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == nonHeroAssetId);

            // The hero bank folder name falls back to its own bank key (no name catalog in this
            // fixture) - real corpus data names it "Alundra" (docs/plan-e3-collisions.md), but the
            // selector under test only reads bank.IsAlundraBank/Sector5Id, not the folder name.
            var heroEntity = LoadEntity(outputDirectory, heroAssetInfo.FileName);
            var nonHeroEntity = LoadEntity(outputDirectory, nonHeroAssetInfo.FileName);

            var heroController = Assert.Single(
                heroEntity.Components, c => c is CharacterControllerComponent);
            var controller = Assert.IsType<CharacterControllerComponent>(heroController);

            Assert.Equal(7.5f, controller.Settings.Radius);
            Assert.Equal(32f, controller.Settings.Height);
            Assert.Equal(0.5f, controller.Settings.SkinWidth);
            Assert.Equal(3f, controller.Settings.StepHeight);
            Assert.Equal(4f, controller.Settings.GroundSnapDistance);
            Assert.Equal(0f, controller.Settings.Gravity);
            Assert.Equal(0f, controller.Settings.MaxFallSpeed);
            Assert.Equal(0u, controller.Settings.WalkabilityMask);
            Assert.Equal(CharacterControlMode.Player, controller.ControlMode);

            Assert.DoesNotContain(nonHeroEntity.Components, c => c is CharacterControllerComponent);

            // Same values re-read straight from the raw JSON (settings/control_mode round-trip,
            // independent of Entity.Load) - guards against a silently-dropped node the way
            // CharacterControllerComponent's save path used to drop it before E3.d.0 (moteur).
            var heroDocument = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, heroAssetInfo.FileName)));
            var controllerNode = Assert.Single(
                (JArray)heroDocument["components"]!, node => (string?)node["type"] == nameof(CharacterControllerComponent));
            var settingsNode = controllerNode["settings"]!;
            Assert.Equal(7.5f, (float)settingsNode["radius"]!);
            Assert.Equal(32f, (float)settingsNode["height"]!);
            Assert.Equal(0.5f, (float)settingsNode["skin_width"]!);
            Assert.Equal(3f, (float)settingsNode["step_height"]!);
            Assert.Equal(4f, (float)settingsNode["ground_snap_distance"]!);
            Assert.Equal(0f, (float)settingsNode["gravity"]!);
            Assert.Equal(0f, (float)settingsNode["max_fall_speed"]!);
            Assert.Equal(0u, (uint)settingsNode["walkability_mask"]!);
            Assert.Equal("Player", (string?)controllerNode["control_mode"]);

            var nonHeroDocument = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, nonHeroAssetInfo.FileName)));
            Assert.DoesNotContain(
                (JArray)nonHeroDocument["components"]!, node => (string?)node["type"] == nameof(CharacterControllerComponent));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static Entity LoadEntity(string outputDirectory, string relativePath)
    {
        var document = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, relativePath)));
        var entity = new Entity();
        entity.Load(document);
        return entity;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
