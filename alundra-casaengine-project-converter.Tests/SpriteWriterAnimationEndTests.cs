using System.Linq;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers the "Alundra's animation is too fast" fix: every animation used to be exported as
/// AnimationType.Loop regardless of its trailing control frame (SpriteFrame.TerminatorCode). See
/// AnimationEndClassifier and SpriteWriter's class doc IdsvAnimDirs bullet for End/ChainTo.
/// </summary>
public class SpriteWriterAnimationEndTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_LoopTerminator_KeepsAnimationTypeLoopAndOldTerminalKeyframe()
    {
        // Delay 1 on the control frame -> Loop, unchanged from the writer's pre-existing behaviour:
        // the terminal keyframe still hides every part (it is unreachable through wrapping anyway).
        RunConversion(
            sector5Id: 60,
            terminatorFrameJson: """{ "Delay": 1, "TransformIndexLow": 0 }""",
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["Sprites.AnimationsLoop"]);
                Assert.Equal(0, report.Counters.GetValueOrDefault("Sprites.AnimationsHold"));
                Assert.Equal(0, report.Counters.GetValueOrDefault("Sprites.AnimationsChain"));

                var animationPath = FindEntityAsset(outputDirectory, "bank60_anim0_down.anim2d");
                var animationData = new Animation2dData();
                animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));
                Assert.Equal(AnimationType.Loop, animationData.AnimationType);

                var visibleTrack = animationData.Tracks.Single(
                    t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Visible);
                Assert.Equal(3, visibleTrack.VisibleKeyframes.Count);
                Assert.True(visibleTrack.VisibleKeyframes[0].Value);
                Assert.True(visibleTrack.VisibleKeyframes[1].Value);
                Assert.False(visibleTrack.VisibleKeyframes[2].Value); // terminal keyframe: hidden

                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetIdsByBankKey["60"].ToString());
                var entry = record.GetProperty("IdsvAnimDirs")[0];
                Assert.Equal("Loop", entry.GetProperty("End").GetString());
                Assert.False(entry.TryGetProperty("ChainTo", out _));
            });
    }

    [Fact]
    public void ConvertSprites_HoldTerminator_IsOnceAndRepeatsLastFrameOnTerminalKeyframe()
    {
        // Delay 0, TransformIndexLow 0x80 set -> Hold: freeze on the last displayed frame.
        RunConversion(
            sector5Id: 61,
            terminatorFrameJson: """{ "Delay": 0, "TransformIndexLow": 128 }""",
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["Sprites.AnimationsHold"]);
                Assert.Equal(0, report.Counters.GetValueOrDefault("Sprites.AnimationsLoop"));

                var animationPath = FindEntityAsset(outputDirectory, "bank61_anim0_down.anim2d");
                var animationData = new Animation2dData();
                animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));
                Assert.Equal(AnimationType.Once, animationData.AnimationType);

                var visibleTrack = animationData.Tracks.Single(
                    t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Visible);
                Assert.Equal(3, visibleTrack.VisibleKeyframes.Count);
                // Terminal keyframe now stays visible, repeating the last displayed frame.
                Assert.True(visibleTrack.VisibleKeyframes[2].Value);

                var spriteTrack = animationData.Tracks.Single(
                    t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Sprite);
                // Only 2 sprite keyframes were ever added for the 2 displayed frames, but the 3rd
                // (terminal) one must repeat the 2nd (last displayed) frame's sprite/position values.
                Assert.Equal(3, spriteTrack.SpriteKeyframes.Count);
                Assert.Equal(spriteTrack.SpriteKeyframes[1].Value, spriteTrack.SpriteKeyframes[2].Value);

                var positionTrack = animationData.Tracks.Single(
                    t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Position);
                Assert.Equal(positionTrack.PositionKeyframes[1].Value, positionTrack.PositionKeyframes[2].Value);

                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetIdsByBankKey["61"].ToString());
                var entry = record.GetProperty("IdsvAnimDirs")[0];
                Assert.Equal("Hold", entry.GetProperty("End").GetString());
                Assert.False(entry.TryGetProperty("ChainTo", out _));
            });
    }

    [Fact]
    public void ConvertSprites_ChainTerminator_IsOnceAndRecordsChainTo()
    {
        // Delay 0, TransformIndexLow 3 (bit 7 clear) -> Chain to AnimSet index 3.
        RunConversion(
            sector5Id: 62,
            terminatorFrameJson: """{ "Delay": 0, "TransformIndexLow": 3 }""",
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["Sprites.AnimationsChain"]);
                Assert.Equal(0, report.Counters.GetValueOrDefault("Sprites.AnimationsChainSelf"));

                var animationPath = FindEntityAsset(outputDirectory, "bank62_anim0_down.anim2d");
                var animationData = new Animation2dData();
                animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));
                Assert.Equal(AnimationType.Once, animationData.AnimationType);

                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetIdsByBankKey["62"].ToString());
                var entry = record.GetProperty("IdsvAnimDirs")[0];
                Assert.Equal("Chain", entry.GetProperty("End").GetString());
                Assert.Equal(3, entry.GetProperty("ChainTo").GetInt32());
            });
    }

    [Fact]
    public void ConvertSprites_SelfChainingTerminator_IncrementsChainSelfCounter()
    {
        // AnimSet index 0 chaining to TransformIndexLow 0 -> chains to itself.
        RunConversion(
            sector5Id: 63,
            terminatorFrameJson: """{ "Delay": 0, "TransformIndexLow": 0 }""",
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["Sprites.AnimationsChain"]);
                Assert.Equal(1, report.Counters["Sprites.AnimationsChainSelf"]);
            });
    }

    /// <summary>
    /// Converts a one-bank, one-animation, two-displayed-frame fixture whose trailing control frame
    /// is <paramref name="terminatorFrameJson"/>, then hands the output directory, the report and
    /// the bank-key -&gt; prefab id map to <paramref name="assert"/>. Same shape as
    /// SpriteWriterSpriteRecordsTests.RunConversion.
    /// </summary>
    private static void RunConversion(
        int sector5Id, string terminatorFrameJson,
        Action<string, ConversionReport, IReadOnlyDictionary<string, Guid>> assert)
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
                                        { "Delay": 136, "Images": { "DepthSortValue": 1, "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
                                        ] } },
                                        { "Delay": 138, "Images": { "DepthSortValue": 2, "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -6, "Y1": -20, "X2": 10, "Y2": -20, "X3": -6, "Y3": -4, "X4": 10, "Y4": -4, "Signature": {{sector5Id}}1 }
                                        ] } },
                                        {{terminatorFrameJson}}
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
            var prefabAssetIdsByBankKey = SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);

            assert(outputDirectory, report, prefabAssetIdsByBankKey);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string FindEntityAsset(string outputDirectory, string fileName)
    {
        var entitiesDirectory = Path.Combine(outputDirectory, "Entities");
        Assert.True(Directory.Exists(entitiesDirectory), $"'{entitiesDirectory}' was not created.");

        var matches = Directory.GetFiles(entitiesDirectory, fileName, SearchOption.AllDirectories);
        Assert.True(matches.Length == 1, $"Expected exactly one '{fileName}' under Entities/, found {matches.Length}.");
        return matches[0];
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
