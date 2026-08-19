using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers the per-bank Entities/{name}/{name}.entity prefab: root AnimatedSpriteComponent, the
/// header body box (SpriteRecord.Header) as an optional CollisionComponent child, the entity-level
/// DepthSortable2DComponent and script_class_name. The assertions go through the engine's own
/// Entity.Load, because that is the loader whose unconditional read of "physics_definition" the
/// emitted document has to satisfy.
/// </summary>
public class SpriteWriterBodyPrefabTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_WithAPositiveHeaderBox_WritesAnEntityThatLoadsInTheEngine()
    {
        // The most common real header volume (map_1, bank 22): Offset (-10,-7,0), Size 20x14x32.
        RunConversion(
            sector5Id: 40,
            bodyBox: """ "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "SizeX": 20, "SizeY": 14, "SizeZ": 32 """,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.BodyPrefabs"]);
                Assert.False(report.Counters.ContainsKey("Entities.SpriteOnlyPrefabs"));

                var entityPath = Assert.Single(
                    Directory.GetFiles(Path.Combine(outputDirectory, "Entities"), "*.entity", SearchOption.AllDirectories));

                // The prefab is named after its own folder, like the plan asks.
                var folderName = Path.GetFileName(Path.GetDirectoryName(entityPath))!;
                Assert.Equal($"{folderName}.entity", Path.GetFileName(entityPath));

                var entityDocument = JObject.Parse(File.ReadAllText(entityPath));
                var relativePath = Path.Combine("Entities", folderName, $"{folderName}.entity");
                var assetInfo = Assert.Single(
                    EditorAssetCatalogService.AssetInfos, info => info.FileName == relativePath);
                Assert.Equal(folderName, assetInfo.Name);
                Assert.Equal(assetInfo.Id.ToString(), (string?)entityDocument["id"]);

                var entity = new Entity();
                entity.Load(entityDocument);

                Assert.Equal("AlundraEntityScriptProxy", entity.GameplayProxyClassName);

                var spriteComponent = Assert.IsType<AnimatedSpriteComponent>(entity.RootComponent);
                Assert.NotEmpty(spriteComponent.AnimationAssetIds);

                var depthSortable = Assert.IsType<DepthSortable2DComponent>(Assert.Single(entity.Components));
                Assert.Equal(RenderPass2D.YSortedWorld, depthSortable.RenderPass);

                var collisionComponent = Assert.IsType<CollisionComponent>(Assert.Single(spriteComponent.Children));
                Assert.Equal(PhysicsType.Kinetic, collisionComponent.PhysicsType);

                var fixture = Assert.Single(collisionComponent.Fixtures);
                var box = Assert.IsType<Box>(fixture.Shape);

                // SizeX -> X, SizeY -> Y, SizeZ -> Z, in raw Alundra pixels.
                Assert.Equal(new Vector3(20f, 14f, 32f), box.Size);

                // Alundra stores the min corner; the engine poses the fixture by its centre.
                Assert.Equal(new Vector3(0f, 0f, 16f), fixture.LocalPosition);
                Assert.Equal(Quaternion.Identity, fixture.LocalRotation);

                // Empty profiles on both levels: Kinetic resolves to Pawn by the PhysicsType rule.
                Assert.True(string.IsNullOrEmpty(fixture.ProfileName));
                Assert.True(string.IsNullOrEmpty(fixture.Tag));
                Assert.True(string.IsNullOrEmpty(collisionComponent.PhysicsDefinition.ProfileName));
            });
    }

    [Fact]
    public void ConvertSprites_WithADegenerateHeaderBox_WritesASpriteOnlyEntity()
    {
        // 4 of the 147 hero records ship SizeZ = 0: a flat volume is no volume.
        RunConversion(
            sector5Id: 41,
            bodyBox: """ "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "SizeX": 20, "SizeY": 14, "SizeZ": 0 """,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.SpriteOnlyPrefabs"]);
                Assert.False(report.Counters.ContainsKey("Entities.BodyPrefabs"));
                AssertSpriteOnlyPrefab(outputDirectory);
            });
    }

    [Fact]
    public void ConvertSprites_WithoutAHeaderBox_WritesASpriteOnlyEntity()
    {
        RunConversion(
            sector5Id: 42,
            bodyBox: null,
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Entities.Prefabs"]);
                Assert.Equal(1, report.Counters["Entities.SpriteOnlyPrefabs"]);
                Assert.False(report.Counters.ContainsKey("Entities.BodyPrefabs"));
                AssertSpriteOnlyPrefab(outputDirectory);
            });
    }

    private static void AssertSpriteOnlyPrefab(string outputDirectory)
    {
        var entityPath = Assert.Single(
            Directory.GetFiles(Path.Combine(outputDirectory, "Entities"), "*.entity", SearchOption.AllDirectories));
        var entityDocument = JObject.Parse(File.ReadAllText(entityPath));

        var entity = new Entity();
        entity.Load(entityDocument);

        Assert.Equal("AlundraEntityScriptProxy", entity.GameplayProxyClassName);
        var spriteComponent = Assert.IsType<AnimatedSpriteComponent>(entity.RootComponent);
        Assert.Empty(spriteComponent.Children);
        Assert.IsType<DepthSortable2DComponent>(Assert.Single(entity.Components));
    }

    /// <summary>
    /// Converts a one-bank fixture whose header carries <paramref name="bodyBox"/> (the raw JSON
    /// fields, or null for a header without them), then hands the output directory and the report
    /// to <paramref name="assert"/>.
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
