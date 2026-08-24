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
/// Covers E4.a's converter change (docs/plan-e4-deplacement-scripte.md "E4.a"): every prefab with a
/// positive body box - not just the hero - now gets a <see cref="CharacterControllerComponent"/>,
/// sized from ITS OWN header box, in "Script" control mode. Follows
/// SpriteWriterBodyPrefabTests.RunConversion's convention of feeding a synthetic one-bank fixture the
/// REAL header numbers of a real bank, rather than loading the multi-megabyte real map file.
/// </summary>
public class SpriteWriterNpcCharacterControllerTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_ANonHeroBankWithABodyBox_GetsAControllerSizedFromItsOwnBox()
    {
        // Real header, map 389 bank 146 (data-extracted/data/map_389.json, SpriteInfo.SpriteRecords,
        // Sector5Id 146 - one of the intro's scripted walkers, records 11/12): OffsetX -9, OffsetY -6,
        // OffsetZ 0, SizeX 18, SizeY 12, SizeZ 32. Radius = min(SizeX, SizeY)/2 = min(18,12)/2 = 6;
        // Height = max(SizeZ, 2*Radius) = max(32, 12) = 32.
        RunConversion(
            sector5Id: 146,
            bodyBox: """ "OffsetX": -9, "OffsetY": -6, "OffsetZ": 0, "SizeX": 18, "SizeY": 12, "SizeZ": 32 """,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.BodyPrefabs"]);
                Assert.Equal(1, report.Counters["Entities.CharacterControllers"]);
                Assert.False(report.Counters.ContainsKey("Entities.CharacterControllersSkippedDegenerateBody"));

                var entity = LoadOnlyEntity(outputDirectory);

                var controller = Assert.IsType<CharacterControllerComponent>(
                    Assert.Single(entity.Components, c => c is CharacterControllerComponent));

                Assert.Equal(6f, controller.Settings.Radius);
                Assert.Equal(32f, controller.Settings.Height);
                Assert.Equal(0.5f, controller.Settings.SkinWidth);
                Assert.Equal(3f, controller.Settings.StepHeight);
                Assert.Equal(4f, controller.Settings.GroundSnapDistance);
                Assert.Equal(0f, controller.Settings.Gravity);
                Assert.Equal(0f, controller.Settings.MaxFallSpeed);
                Assert.Equal(0u, controller.Settings.WalkabilityMask);
                Assert.Equal(CharacterControlMode.Script, controller.ControlMode);

                // Settings/control_mode round-trip through the raw JSON too (same guard as
                // SpriteWriterCharacterControllerTests, against a silently-dropped node).
                var entityPath = Assert.Single(
                    Directory.GetFiles(Path.Combine(outputDirectory, "Entities"), "*.entity", SearchOption.AllDirectories));
                var document = JObject.Parse(File.ReadAllText(entityPath));
                var controllerNode = Assert.Single(
                    (JArray)document["components"]!, node => (string?)node["type"] == nameof(CharacterControllerComponent));
                Assert.Equal(6f, (float)controllerNode["settings"]!["radius"]!);
                Assert.Equal("Script", (string?)controllerNode["control_mode"]);
            });
    }

    [Fact]
    public void ConvertSprites_ASpriteOnlyBank_GetsNoCharacterController()
    {
        RunConversion(
            sector5Id: 147,
            bodyBox: null,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.SpriteOnlyPrefabs"]);
                Assert.False(report.Counters.ContainsKey("Entities.CharacterControllers"));

                var entity = LoadOnlyEntity(outputDirectory);
                Assert.DoesNotContain(entity.Components, c => c is CharacterControllerComponent);
            });
    }

    [Fact]
    public void ConvertSprites_ADegenerateSubPixelBodyBox_SkipsTheControllerWithAWarning()
    {
        // Real header, map_alundra.json bank Sector5Id 244: a 1x1x1 pixel body box. Radius would be
        // min(1,1)/2 = 0.5, exactly equal to the fixed 0.5 skin width every other NPC gets -
        // CharacterControllerSettings.Validate requires SkinWidth STRICTLY less than Radius, so this
        // one real bank is left without a controller (still gets its CollisionComponent).
        RunConversion(
            sector5Id: 244,
            bodyBox: """ "OffsetX": 0, "OffsetY": 0, "OffsetZ": 0, "SizeX": 1, "SizeY": 1, "SizeZ": 1 """,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.BodyPrefabs"]);
                Assert.False(report.Counters.ContainsKey("Entities.CharacterControllers"));
                Assert.Equal(1, report.Counters["Entities.CharacterControllersSkippedDegenerateBody"]);
                Assert.Contains(report.Warnings, warning => warning.Contains("too small for a character controller", StringComparison.Ordinal));

                var entity = LoadOnlyEntity(outputDirectory);
                Assert.DoesNotContain(entity.Components, c => c is CharacterControllerComponent);
                Assert.Contains(entity.Components, c => c is DepthSortable2DComponent);
            });
    }

    private static Entity LoadOnlyEntity(string outputDirectory)
    {
        var entityPath = Assert.Single(
            Directory.GetFiles(Path.Combine(outputDirectory, "Entities"), "*.entity", SearchOption.AllDirectories));
        var document = JObject.Parse(File.ReadAllText(entityPath));
        var entity = new Entity();
        entity.Load(document);
        return entity;
    }

    /// <summary>
    /// Same one-bank-fixture convention as SpriteWriterBodyPrefabTests.RunConversion: a synthetic
    /// map_0.json carrying exactly one non-hero SpriteRecord, fed the real header numbers named at
    /// each call site.
    /// </summary>
    private static void RunConversion(
        int sector5Id, string? bodyBox, Action<string, ConversionReport> assert)
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

            var headerFields = bodyBox == null ? string.Empty : $", {bodyBox}";

            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                $$"""
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": {{sector5Id}}{{headerFields}} },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
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

            File.WriteAllText(
                Path.Combine(dataDirectory, "map_alundra.json"),
                """{ "SpriteInfo": { "SpriteRecords": [], "SpriteEffectRecords": [] } }""");

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);

            assert(outputDirectory, report);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
