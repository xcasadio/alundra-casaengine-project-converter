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

    // E19.m7 (O-E19-9): a Loop animation whose only displayed frame lasts 0 ticks (Delay 0x80) keeps that
    // frame visible, like the binary (UpdateAnimation holds it for 2^32 - 1 ticks).
    private static string ZeroTickDisplayedFrameJson(int sector5Id)
    {
        return $$"""
            { "Delay": 128, "Images": { "DepthSortValue": 1, "Images": [
                { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 },
                { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -6, "Y1": -20, "X2": 10, "Y2": -20, "X3": -6, "Y3": -4, "X4": 10, "Y4": -4, "Signature": {{sector5Id}}1 }
            ] } }
            """;
    }

    [Fact]
    public void ConvertSprites_ZeroDurationLoop_KeepsEveryPartVisibleAtZero()
    {
        RunConversion(
            sector5Id: 64,
            terminatorFrameJson: """{ "Delay": 1 }""",
            (outputDirectory, _, _) =>
            {
                var animationData = LoadAnimation(outputDirectory, "bank64_anim0_down.anim2d");
                Assert.Equal(AnimationType.Loop, animationData.AnimationType);
                Assert.Equal(0f, animationData.GetDurationSeconds());

                var visibleTracks = animationData.Tracks
                    .Where(t => t.Property == Animation2dTrackProperty.Visible).ToList();
                Assert.Equal(2, visibleTracks.Count);
                foreach (var track in visibleTracks)
                {
                    var key = Assert.Single(track.VisibleKeyframes);
                    Assert.Equal(0f, key.TimeSeconds);
                    Assert.True(key.Value);
                }
            },
            ZeroTickDisplayedFrameJson(64));
    }

    [Fact]
    public void ConvertSprites_ZeroDurationLoop_StaysVisibleThroughTheEngineSampler()
    {
        RunConversion(
            sector5Id: 65,
            terminatorFrameJson: """{ "Delay": 1 }""",
            (outputDirectory, _, _) =>
            {
                var animationData = LoadAnimation(outputDirectory, "bank65_anim0_down.anim2d");
                var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animationData));

                sampler.Reset();
                for (var tick = 0; tick < 100; tick++)
                {
                    sampler.Update(0.02f);
                    Assert.Equal(0, sampler.LastUpdateLoopTurns);
                    Assert.Equal(2, sampler.RuntimeState.Parts.Count);
                    Assert.All(sampler.RuntimeState.Parts, part => Assert.True(part.Visible));
                }
            },
            ZeroTickDisplayedFrameJson(65));
    }

    [Fact]
    public void ConvertSprites_LoopWithTwoTickFrame_KeepsTheTerminalHideKey()
    {
        // Guard: a non-zero duration still gets the terminal all-parts-hidden key.
        RunConversion(
            sector5Id: 66,
            terminatorFrameJson: """{ "Delay": 1 }""",
            (outputDirectory, _, _) =>
            {
                var animationData = LoadAnimation(outputDirectory, "bank66_anim0_down.anim2d");
                var track = animationData.Tracks.Single(
                    t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Visible);
                Assert.Equal(2, track.VisibleKeyframes.Count);
                Assert.Equal(0f, track.VisibleKeyframes[0].TimeSeconds);
                Assert.True(track.VisibleKeyframes[0].Value);
                Assert.Equal(0.04f, track.VisibleKeyframes[1].TimeSeconds, 5);
                Assert.False(track.VisibleKeyframes[1].Value);
            },
            ZeroTickDisplayedFrameJson(66).Replace("\"Delay\": 128", "\"Delay\": 130"));
    }

    private static Animation2dData LoadAnimation(string outputDirectory, string fileName)
    {
        var animationData = new Animation2dData();
        animationData.Load(JObject.Parse(File.ReadAllText(FindEntityAsset(outputDirectory, fileName))));
        return animationData;
    }

    /// <summary>
    /// Converts a one-bank, one-animation, two-displayed-frame fixture whose trailing control frame
    /// is <paramref name="terminatorFrameJson"/>, then hands the output directory, the report and
    /// the bank-key -&gt; prefab id map to <paramref name="assert"/>. Same shape as
    /// SpriteWriterSpriteRecordsTests.RunConversion.
    /// </summary>
    private static void RunConversion(
        int sector5Id, string terminatorFrameJson,
        Action<string, ConversionReport, IReadOnlyDictionary<string, Guid>> assert,
        string? displayedFramesJson = null)
    {
        var displayedFrames = displayedFramesJson ?? DefaultDisplayedFramesJson(sector5Id);
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
                                        {{displayedFrames}},
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

    /// <summary>The two displayed frames (Delay 136 and 138, one quad each) every fixture used so far shares.</summary>
    private static string DefaultDisplayedFramesJson(int sector5Id)
    {
        return $$"""
            { "Delay": 136, "Images": { "DepthSortValue": 1, "Images": [
                { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
            ] } },
            { "Delay": 138, "Images": { "DepthSortValue": 2, "Images": [
                { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -6, "Y1": -20, "X2": 10, "Y2": -20, "X3": -6, "Y3": -4, "X4": 10, "Y4": -4, "Signature": {{sector5Id}}1 }
            ] } }
            """;
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
