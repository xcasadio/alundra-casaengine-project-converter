using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers the per-frame SiFrame.CollisionData -> Animation2dData.CollisionKeyframes conversion.
/// Every fixture here ends with a Delay=1 terminator frame, like the shipped data does.
/// </summary>
public class SpriteWriterCollisionTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    // Two 0x88 frames run 8 game frames each at 50 Hz, so the second frame starts at 0.16 s and the
    // animation lasts 0.32 s.
    private const float SecondFrameTime = 0.16f;
    private const float DurationSeconds = 0.32f;

    private const string CollisionA =
        """{ "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "Width": 20, "Depth": 14, "Height": 32 }""";

    private const string CollisionB =
        """{ "OffsetX": 0, "OffsetY": 0, "OffsetZ": 4, "Width": 8, "Depth": 8, "Height": 8 }""";

    [Fact]
    public void ConvertSprites_WithConstantHitbox_EmitsASingleKeyframeAtTimeZero()
    {
        RunConversion(
            sector5Id: 20,
            frame0Collision: CollisionA,
            frame1Collision: CollisionA,
            (animationData, _, report) =>
            {
                var keyframe = Assert.Single(animationData.CollisionKeyframes);
                Assert.Equal(0f, keyframe.TimeSeconds);

                var fixture = Assert.Single(keyframe.Fixtures);
                var box = Assert.IsType<Box>(fixture.Shape);

                // Width -> X, Depth -> Y, Height -> Z, in raw Alundra pixels.
                Assert.Equal(20f, box.Size.X);
                Assert.Equal(14f, box.Size.Y);
                Assert.Equal(32f, box.Size.Z);

                // Alundra stores the min corner; the engine poses the box by its centre.
                Assert.Equal(0f, fixture.LocalPosition.X);
                Assert.Equal(0f, fixture.LocalPosition.Y);
                Assert.Equal(16f, fixture.LocalPosition.Z);

                // CollisionData carries no attack/defence semantics, so no profile is invented.
                Assert.True(string.IsNullOrEmpty(fixture.ProfileName));
                Assert.True(string.IsNullOrEmpty(fixture.Tag));

                Assert.Equal(1, report.Counters["Sprites.CollisionKeyframes"]);
                Assert.Equal(1, report.Counters["Sprites.AnimationsWithCollision"]);

                AssertNoKeyframeAtDuration(animationData);
            });
    }

    [Fact]
    public void ConvertSprites_WithHitboxChangingMidAnimation_EmitsAKeyframePerDistinctVolume()
    {
        RunConversion(
            sector5Id: 21,
            frame0Collision: CollisionA,
            frame1Collision: CollisionB,
            (animationData, _, report) =>
            {
                Assert.Equal(2, animationData.CollisionKeyframes.Count);
                Assert.Equal(0f, animationData.CollisionKeyframes[0].TimeSeconds);
                Assert.Equal(SecondFrameTime, animationData.CollisionKeyframes[1].TimeSeconds, 5);

                var secondFixture = Assert.Single(animationData.CollisionKeyframes[1].Fixtures);
                var box = Assert.IsType<Box>(secondFixture.Shape);
                Assert.Equal(8f, box.Size.X);
                Assert.Equal(new Microsoft.Xna.Framework.Vector3(4f, 4f, 8f), secondFixture.LocalPosition);

                Assert.Equal(2, report.Counters["Sprites.CollisionKeyframes"]);
                Assert.Equal(1, report.Counters["Sprites.AnimationsWithCollision"]);

                AssertNoKeyframeAtDuration(animationData);
            });
    }

    [Fact]
    public void ConvertSprites_WithHitboxLostMidAnimation_EmitsAnEmptyFixtureSet()
    {
        RunConversion(
            sector5Id: 22,
            frame0Collision: CollisionA,
            frame1Collision: null,
            (animationData, _, _) =>
            {
                Assert.Equal(2, animationData.CollisionKeyframes.Count);
                Assert.Single(animationData.CollisionKeyframes[0].Fixtures);

                // Deactivation keyframe: the engine builds no body from an empty fixture set.
                Assert.Equal(SecondFrameTime, animationData.CollisionKeyframes[1].TimeSeconds, 5);
                Assert.Empty(animationData.CollisionKeyframes[1].Fixtures);

                AssertNoKeyframeAtDuration(animationData);
            });
    }

    [Fact]
    public void ConvertSprites_WithoutCollisionData_WritesNoCollisionKeyframesKey()
    {
        RunConversion(
            sector5Id: 23,
            frame0Collision: null,
            frame1Collision: null,
            (animationData, animationJson, report) =>
            {
                Assert.Empty(animationData.CollisionKeyframes);
                Assert.Null(animationJson["collision_keyframes"]);
                Assert.False(report.Counters.ContainsKey("Sprites.CollisionKeyframes"));
                Assert.False(report.Counters.ContainsKey("Sprites.AnimationsWithCollision"));
            });
    }

    [Fact]
    public void ConvertSprites_PinsTheMinCornerToCentreConversion()
    {
        // The most common real volume (map_1): Offset (-12,-12,0), Size 23x23x15.
        RunConversion(
            sector5Id: 24,
            frame0Collision: """{ "OffsetX": -12, "OffsetY": -12, "OffsetZ": 0, "Width": 23, "Depth": 23, "Height": 15 }""",
            frame1Collision: null,
            (animationData, _, _) =>
            {
                var fixture = Assert.Single(animationData.CollisionKeyframes[0].Fixtures);
                var box = Assert.IsType<Box>(fixture.Shape);

                Assert.Equal(new Microsoft.Xna.Framework.Vector3(23f, 23f, 15f), box.Size);
                Assert.Equal(new Microsoft.Xna.Framework.Vector3(-0.5f, -0.5f, 7.5f), fixture.LocalPosition);
            });
    }

    [Fact]
    public void ConvertSprites_EmptyFixtureSet_IsToleratedByTheEngineSampler()
    {
        // The deactivation keyframe is the one shape the engine had no authored precedent for, so
        // this walks it through the runtime path the converted asset will actually take: the
        // composition adapter and the step sampler.
        RunConversion(
            sector5Id: 25,
            frame0Collision: CollisionA,
            frame1Collision: null,
            (animationData, _, _) =>
            {
                var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animationData));

                sampler.Seek(0f);
                Assert.Equal(0, sampler.CurrentCollisionKeyframeIndex);
                Assert.Single(sampler.CollisionKeyframes[sampler.CurrentCollisionKeyframeIndex].Fixtures);

                sampler.Seek(SecondFrameTime + 0.01f);
                Assert.Equal(1, sampler.CurrentCollisionKeyframeIndex);
                Assert.Empty(sampler.CollisionKeyframes[sampler.CurrentCollisionKeyframeIndex].Fixtures);
            });
    }

    private static void AssertNoKeyframeAtDuration(Animation2dData animationData)
    {
        // A keyframe sitting on the duration is unreachable: the looping sampler wraps CurrentTime
        // at the duration, so it would never be selected.
        Assert.All(
            animationData.CollisionKeyframes,
            keyframe => Assert.True(
                keyframe.TimeSeconds < DurationSeconds,
                $"Keyframe at {keyframe.TimeSeconds} s sits on the animation duration."));
    }

    /// <summary>
    /// Converts a one-bank fixture whose animation is two displayed frames plus a loop terminator,
    /// then hands the reloaded animation, its raw JSON and the report to <paramref name="assert"/>.
    /// </summary>
    private static void RunConversion(
        int sector5Id,
        string? frame0Collision,
        string? frame1Collision,
        Action<Animation2dData, JObject, ConversionReport> assert)
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

            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                $$"""
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": {{sector5Id}} },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "CollisionData": {{frame0Collision ?? "null"}}, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
                                        ] } },
                                        { "Delay": 136, "CollisionData": {{frame1Collision ?? "null"}}, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}1 }
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

            var animationPath = Directory
                .GetFiles(Path.Combine(outputDirectory, "Entities"), $"bank{sector5Id}_anim0_down.anim2d",
                    SearchOption.AllDirectories)
                .Single();

            var animationJson = JObject.Parse(File.ReadAllText(animationPath));
            var animationData = new Animation2dData();
            animationData.Load(animationJson);

            Assert.Equal(DurationSeconds, animationData.GetDurationSeconds(), 5);

            assert(animationData, animationJson, report);
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
