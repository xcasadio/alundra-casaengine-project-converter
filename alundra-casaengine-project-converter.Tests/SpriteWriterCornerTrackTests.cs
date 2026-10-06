using System.Linq;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.g G2b-3 (ADR-0069 of the engine, the parent's ADR of the corner track): a part with at least one deformed image
/// (anything but a rectangle of the source size, mirrors included) gets a <c>Corners</c> track appended after every
/// existing track. Keys sit on the times of the part's Sprite keys, only at changes of state; the corners are the raw PS1
/// corners (y flipped) minus the Position of the image; <c>source_offset</c> is (-mx, -my). The values of the six cases
/// of docs/plan-e19-g2b-annexe/engine-notes.md 3.2 were written before the code.
/// </summary>
public class SpriteWriterCornerTrackTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private readonly record struct Q(int W, int H, int X1, int Y1, int X2, int Y2, int X3, int Y3, int X4, int Y4);

    // engine-notes.md 3.2
    private static readonly Q Scaled = new(16, 16, -12, -20, 12, -20, -12, 4, 12, 4);          // x1.5, Position (0, 8)
    private static readonly Q Shear = new(16, 16, -8, -16, 8, -16, -4, 0, 12, 0);              // parallelogram, Position (2, 8)
    private static readonly Q Trapezoid = new(16, 16, -4, -16, 4, -16, -8, 0, 8, 0);           // general, Position (0, 8)
    private static readonly Q MirroredScaled = new(16, 16, 12, -20, -12, -20, 12, 4, -12, 4);  // flipX kept, Position (0, 8)
    private static readonly Q OddHalfPixel = new(23, 31, -11, -40, 13, -38, -9, -6, 13, -5);   // Position (0.5, 23)
    private static readonly Q Plain = new(16, 16, -8, -16, 8, -16, -8, 0, 8, 0);
    private static readonly Q PlainMirrored = new(16, 16, 8, -16, -8, -16, 8, 0, -8, 0);

    private static int _signature = 100;

    private static string QuadJson(Q q)
    {
        _signature++;
        return $$"""{ "Spritesheet": 0, "Palette": 0, "AtlasX": {{_signature * 30}}, "AtlasY": 0, "Swidth": {{q.W}}, "Sheight": {{q.H}}, "X1": {{q.X1}}, "Y1": {{q.Y1}}, "X2": {{q.X2}}, "Y2": {{q.Y2}}, "X3": {{q.X3}}, "Y3": {{q.Y3}}, "X4": {{q.X4}}, "Y4": {{q.Y4}}, "Signature": {{_signature}} }""";
    }

    // Delay 138 = 0x80 | 10 game frames
    private static string FrameJson(params Q[] quads)
    {
        return $$"""{ "Delay": 138, "Images": { "Images": [ {{string.Join(",", quads.Select(QuadJson))}} ] } }""";
    }

    private const string LoopTerminator = """{ "Delay": 1, "TransformIndexLow": 0 }""";
    private const string HoldTerminator = """{ "Delay": 0, "TransformIndexLow": 128 }""";

    private static string AnimJson(string terminator, params string[] frames)
    {
        return $$"""{ "Frames": [ {{string.Join(",", frames)}}, {{terminator}} ] }""";
    }

    // anim set index -> animation (direction down)
    private const int CasesSet = 0;
    private const int NoDeformedSet = 1;
    private const int MixedSet = 2;
    private const int SeriesSet = 3;
    private const int HiddenSet = 4;
    private const int HoldSet = 5;

    private static string BuildMapJson()
    {
        var animations = new[]
        {
            AnimJson(LoopTerminator,
                FrameJson(Scaled), FrameJson(Shear), FrameJson(Trapezoid), FrameJson(MirroredScaled), FrameJson(OddHalfPixel), FrameJson(Plain)),
            AnimJson(LoopTerminator, FrameJson(Plain), FrameJson(PlainMirrored)),
            AnimJson(LoopTerminator, FrameJson(Plain), FrameJson(Scaled), FrameJson(Plain)),
            AnimJson(LoopTerminator, FrameJson(Scaled), FrameJson(Scaled), FrameJson(Scaled), FrameJson(Shear)),
            AnimJson(LoopTerminator, FrameJson(Plain, Scaled), FrameJson(Plain), FrameJson(Plain, Scaled)),
            AnimJson(HoldTerminator, FrameJson(Plain), FrameJson(Scaled)),
        };

        var animSets = string.Join(",", animations.Select(a => $$"""{ "PreloadedAnims": [ {{a}}, null, null, null ] }"""));
        return $$"""{ "SpriteInfo": { "SpriteRecords": [ { "Header": { "Sector5Id": 5 }, "AnimSets": [ {{animSets}} ] } ] } }""";
    }

    private static float[] FrameTimes(int count)
    {
        var times = new float[count];
        var cumulative = 0f;
        for (var index = 0; index < count; index++)
        {
            times[index] = cumulative;
            cumulative += 10 * (1f / 50f);
        }

        return times;
    }

    private static void RunConversion(System.Action<string, ConversionReport> assert)
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
            File.WriteAllText(Path.Combine(dataDirectory, "map_0.json"), BuildMapJson());
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

    private static Animation2dData LoadAnimation(string outputDirectory, int animSet)
    {
        var path = Directory.GetFiles(
            Path.Combine(outputDirectory, "Entities"), $"bank5_anim{animSet}_down.anim2d", SearchOption.AllDirectories).Single();
        var data = new Animation2dData();
        data.Load(JObject.Parse(File.ReadAllText(path)));
        return data;
    }

    private static Animation2dTrackData[] CornerTracks(Animation2dData data)
    {
        return data.Tracks.Where(t => t.Property == Animation2dTrackProperty.Corners).ToArray();
    }

    private static void AssertActive(
        Animation2dCornersKeyframeData key, float time, Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, Point sourceOffset)
    {
        Assert.Equal(time, key.TimeSeconds);
        Assert.True(key.Enabled, "the key is active");
        Assert.Equal(topLeft, key.TopLeft);
        Assert.Equal(topRight, key.TopRight);
        Assert.Equal(bottomLeft, key.BottomLeft);
        Assert.Equal(bottomRight, key.BottomRight);
        Assert.Equal(sourceOffset, key.SourceOffset);
    }

    [Fact]
    public void ConvertSprites_SixCasesOfTheNotes_WriteTheirCornersAndSourceOffsetAndFinallyADisabledKey()
    {
        RunConversion((outputDirectory, report) =>
        {
            var data = LoadAnimation(outputDirectory, CasesSet);
            var corners = Assert.Single(CornerTracks(data));
            Assert.Equal("part0", corners.TargetPartId);
            var times = FrameTimes(6);
            var keys = corners.CornerKeyframes;
            Assert.Equal(6, keys.Count);

            AssertActive(keys[0], times[0], new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-12, -12), new Vector2(12, -12), new Point(0, 0));
            AssertActive(keys[1], times[1], new Vector2(-10, 8), new Vector2(6, 8), new Vector2(-6, -8), new Vector2(10, -8), new Point(0, 0));
            AssertActive(keys[2], times[2], new Vector2(-4, 8), new Vector2(4, 8), new Vector2(-8, -8), new Vector2(8, -8), new Point(0, 0));
            AssertActive(keys[3], times[3], new Vector2(12, 12), new Vector2(-12, 12), new Vector2(12, -12), new Vector2(-12, -12), new Point(-1, 0));
            AssertActive(keys[4], times[4], new Vector2(-11.5f, 17), new Vector2(12.5f, 15), new Vector2(-9.5f, -17), new Vector2(12.5f, -18), new Point(0, 0));
            Assert.Equal(times[5], keys[5].TimeSeconds);
            Assert.False(keys[5].Enabled, "a plain image after a deformed one disables the corners");

            // the Position of each image (the corners are relative to it) and every key time is a Sprite key time
            var position = data.Tracks.Single(t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Position);
            Assert.Equal(new Vector2(0, 8), position.PositionKeyframes[0].Value);
            Assert.Equal(new Vector2(2, 8), position.PositionKeyframes[1].Value);
            Assert.Equal(new Vector2(0, 8), position.PositionKeyframes[2].Value);
            Assert.Equal(new Vector2(0, 8), position.PositionKeyframes[3].Value);
            Assert.Equal(new Vector2(0.5f, 23), position.PositionKeyframes[4].Value);
            var spriteTimes = data.Tracks.Single(t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Sprite)
                .SpriteKeyframes.Select(k => k.TimeSeconds).ToHashSet();
            Assert.All(keys, k => Assert.Contains(k.TimeSeconds, spriteTimes));

            // appended after every existing track: the existing tracks and their names do not move
            Assert.Equal(
                new[] { Animation2dTrackProperty.Sprite, Animation2dTrackProperty.Position, Animation2dTrackProperty.Visible, Animation2dTrackProperty.FlipX, Animation2dTrackProperty.Corners },
                data.Tracks.Select(t => t.Property).ToArray());
            Assert.Equal(new[] { "Track 1", "Track 2", "Track 3", "Track 4", "Track 5" }, data.Tracks.Select(t => t.Name).ToArray());
        });
    }

    [Fact]
    public void ConvertSprites_AnimationWithoutDeformedImage_HasNoCornersTrack()
    {
        RunConversion((outputDirectory, _) =>
        {
            var data = LoadAnimation(outputDirectory, NoDeformedSet);
            Assert.Empty(CornerTracks(data));
            Assert.Equal(
                new[] { Animation2dTrackProperty.Sprite, Animation2dTrackProperty.Position, Animation2dTrackProperty.Visible, Animation2dTrackProperty.FlipX },
                data.Tracks.Select(t => t.Property).ToArray());
        });
    }

    [Fact]
    public void ConvertSprites_MixedPart_WritesAnActiveKeyThenADisabledKey()
    {
        RunConversion((outputDirectory, _) =>
        {
            var corners = Assert.Single(CornerTracks(LoadAnimation(outputDirectory, MixedSet)));
            var times = FrameTimes(3);
            Assert.Equal(2, corners.CornerKeyframes.Count);
            AssertActive(corners.CornerKeyframes[0], times[1], new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-12, -12), new Vector2(12, -12), new Point(0, 0));
            Assert.Equal(times[2], corners.CornerKeyframes[1].TimeSeconds);
            Assert.False(corners.CornerKeyframes[1].Enabled);
        });
    }

    [Fact]
    public void ConvertSprites_IdenticalDeformedImagesInARow_WriteASingleKey()
    {
        RunConversion((outputDirectory, _) =>
        {
            var corners = Assert.Single(CornerTracks(LoadAnimation(outputDirectory, SeriesSet)));
            var times = FrameTimes(4);
            Assert.Equal(2, corners.CornerKeyframes.Count);
            AssertActive(corners.CornerKeyframes[0], times[0], new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-12, -12), new Vector2(12, -12), new Point(0, 0));
            AssertActive(corners.CornerKeyframes[1], times[3], new Vector2(-10, 8), new Vector2(6, 8), new Vector2(-6, -8), new Vector2(10, -8), new Point(0, 0));
        });
    }

    [Fact]
    public void ConvertSprites_HiddenImage_WritesNoKey_AndOnlyTheDeformedPartGetsATrack()
    {
        RunConversion((outputDirectory, _) =>
        {
            var corners = Assert.Single(CornerTracks(LoadAnimation(outputDirectory, HiddenSet)));
            Assert.Equal("part1", corners.TargetPartId);
            var key = Assert.Single(corners.CornerKeyframes);
            AssertActive(key, 0f, new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-12, -12), new Vector2(12, -12), new Point(0, 0));
        });
    }

    [Fact]
    public void ConvertSprites_HoldTerminalRepeat_WritesNoKeyForTheRepeatedLastImage()
    {
        RunConversion((outputDirectory, _) =>
        {
            var data = LoadAnimation(outputDirectory, HoldSet);
            var corners = Assert.Single(CornerTracks(data));
            var times = FrameTimes(3);
            var key = Assert.Single(corners.CornerKeyframes);
            AssertActive(key, times[1], new Vector2(-12, 12), new Vector2(12, 12), new Vector2(-12, -12), new Vector2(12, -12), new Point(0, 0));

            // the Sprite track still repeats the last displayed image on the terminal keyframe (unchanged behaviour)
            var sprite = data.Tracks.Single(t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Sprite);
            Assert.Equal(3, sprite.SpriteKeyframes.Count);
        });
    }

    [Fact]
    public void ConvertSprites_CountsDeformedQuadsCornerTracksAndKeys()
    {
        RunConversion((_, report) =>
        {
            // deformed quads read (the Hold terminal repeat is not counted again): 5 + 0 + 1 + 4 + 2 + 1
            Assert.Equal(13, report.Counters.GetValueOrDefault("Sprites.QuadsDeformed"));
            // tracks: one per set 0, 2, 3, 4, 5
            Assert.Equal(5, report.Counters.GetValueOrDefault("Sprites.CornerTracks"));
            // keys, active and disabled together: 6 + 2 + 2 + 1 + 1
            Assert.Equal(12, report.Counters.GetValueOrDefault("Sprites.CornerKeyframes"));
            Assert.Equal(report.Counters["Sprites.QuadsRead"], report.Counters["Sprites.QuadsConverted"]);
        });
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
