using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class SpriteWriterTests
{
    // Minimal 8-byte PNG signature: enough for the writer's file-copy pipeline, which does not
    // decode image content. Not related to any Alundra asset.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    // A bank's assets live in Entities/<entity name>/, and the name comes from the shipped
    // EntityNames.csv - so these tests look their fixture assets up by file name rather than
    // hardcoding a folder and pinning themselves to that table's contents. The naming rule itself
    // is covered by EntityNameCatalogReaderTests.
    private static string FindEntityAsset(string outputDirectory, string fileName)
    {
        var entitiesDirectory = Path.Combine(outputDirectory, "Entities");
        Assert.True(Directory.Exists(entitiesDirectory), $"'{entitiesDirectory}' was not created.");

        var matches = Directory.GetFiles(entitiesDirectory, fileName, SearchOption.AllDirectories);
        Assert.True(matches.Length == 1, $"Expected exactly one '{fileName}' under Entities/, found {matches.Length}.");
        return matches[0];
    }

    [Fact]
    public void ConvertSprites_WithPaletteCyclingFrames_CreatesDistinctSpritesPerFrame()
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

            // Same (Spritesheet, Sx, Sy) VRAM region reused across frame 0 (palette 0) and frame 1
            // (palette 1), like the real color-cycling sparkle effect this fixture is modeled on.
            // AtlasX/AtlasY differ per frame because the extractor packs one atlas cell per
            // (region, palette) pair - that is exactly what must not collapse into one sprite.
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 5 },
                                "AnimSets": [
                                    {
                                        "PreloadedAnims": [
                                            {
                                                "Frames": [
                                                    {
                                                        "Delay": 10,
                                                        "Images": { "Images": [
                                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 111 }
                                                        ] }
                                                    },
                                                    {
                                                        "Delay": 10,
                                                        "Images": { "Images": [
                                                            { "Spritesheet": 0, "Palette": 1, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 222 }
                                                        ] }
                                                    }
                                                ]
                                            },
                                            null,
                                            null,
                                            null
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                }
                """);

            File.WriteAllText(
                Path.Combine(dataDirectory, "map_alundra.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [],
                        "SpriteEffectRecords": [ { "EffectId": 1 }, { "EffectId": 2 } ]
                    }
                }
                """);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Sprites.QuadsRead"]);
            Assert.Equal(2, report.Counters["Sprites.QuadsConverted"]);
            Assert.Equal(2, report.Counters["Assets.Sprite"]); // one per frame: distinct atlas cells

            var animationPath = FindEntityAsset(outputDirectory, "bank5_anim0_down.anim2d");
            Assert.True(File.Exists(animationPath));

            var animationDocument = JObject.Parse(File.ReadAllText(animationPath));
            var animationData = new Animation2dData();
            animationData.Load(animationDocument);

            Assert.Single(animationData.Parts);
            var spriteTrack = Assert.Single(animationData.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Equal(2, spriteTrack.SpriteKeyframes.Count);
            Assert.NotEqual(spriteTrack.SpriteKeyframes[0].Value, spriteTrack.SpriteKeyframes[1].Value);

            // The quad sits above the anchor (Alundra Y-down corners -16..0), so in CasaEngine's
            // Y-up part space its center must be at +8, not -8. A wrong sign here mirrors the part
            // vertically across the anchor.
            var positionTrack = Assert.Single(animationData.Tracks, t => t.Property == Animation2dTrackProperty.Position);
            Assert.Equal(0f, positionTrack.PositionKeyframes[0].Value.X);
            Assert.Equal(8f, positionTrack.PositionKeyframes[0].Value.Y);

            var firstSpritePath = FindEntityAsset(outputDirectory, "sprite_111.sprite");
            var secondSpritePath = FindEntityAsset(outputDirectory, "sprite_222.sprite");
            Assert.True(File.Exists(firstSpritePath));
            Assert.True(File.Exists(secondSpritePath));

            var firstSprite = new SpriteData();
            firstSprite.Load(JObject.Parse(File.ReadAllText(firstSpritePath)));
            Assert.Equal(0, firstSprite.PositionInTexture.X);

            var secondSprite = new SpriteData();
            secondSprite.Load(JObject.Parse(File.ReadAllText(secondSpritePath)));
            Assert.Equal(20, secondSprite.PositionInTexture.X);

            var heroEffectsPath = Path.Combine(outputDirectory, "Sprites", "hero", "hero_effects.json");
            Assert.True(File.Exists(heroEffectsPath));
            Assert.Equal(2, report.Counters["Sprites.HeroEffectsPreserved"]);

            // Regression guard: AssetLoader<Texture>.LoadAsset swallows its exception and returns
            // null on any missing sampler_state field, surfacing only as a generic "IAssetLoader
            // can't load" error in the editor with no indication of the real cause. Load()'ing the
            // wrapper for real is the only way to catch a schema mismatch here.
            var textureWrapperPath = Path.Combine(outputDirectory, "Sprites", "Textures", "map_0_spritesheet.texture");
            Assert.True(File.Exists(textureWrapperPath));
            var texture = new CasaEngine.Framework.Assets.Textures.Texture();
            texture.Load(JObject.Parse(File.ReadAllText(textureWrapperPath)));
            // PointClamp (nearest-neighbour, clamp addressing): pixel art, drawn at fractional
            // positions, must never blend between texels or bleed across atlas cell edges.
            Assert.Equal(SamplerState.PointClamp.Filter, texture.PreferredSamplerState.Filter);
            Assert.Equal(SamplerState.PointClamp.AddressU, texture.PreferredSamplerState.AddressU);
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
    public void ConvertSprites_MultiPartFrame_PreservesVerticalArrangementBetweenParts()
    {
        // Reproduces the reported "multi-part animations are mispositioned" bug: a frame with two
        // quads, one clearly above the anchor and one clearly below (Alundra Y-down corners). Their
        // relative vertical order must survive the Y-down -> Y-up conversion; getting the sign
        // wrong mirrors each part across the anchor and swaps which one is on top.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

            // part0 quad: Y-down corners -40..-24 (above the anchor)  -> Y-up center +32
            // part1 quad: Y-down corners  8..24  (below the anchor)   -> Y-up center -16
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 7 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -40, "X2": 8, "Y2": -40, "X3": -8, "Y3": -24, "X4": 8, "Y4": -24, "Signature": 700 },
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": 8, "X2": 8, "Y2": 8, "X3": -8, "Y3": 24, "X4": 8, "Y4": 24, "Signature": 701 }
                                        ] } }
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

            var animationPath = FindEntityAsset(outputDirectory, "bank7_anim0_down.anim2d");
            var animationData = new Animation2dData();
            animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));

            Assert.Equal(2, animationData.Parts.Count);

            var part0Position = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Position);
            var part1Position = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part1" && t.Property == Animation2dTrackProperty.Position);

            Assert.Equal(32f, part0Position.PositionKeyframes[0].Value.Y);
            Assert.Equal(-16f, part1Position.PositionKeyframes[0].Value.Y);
            // The above-anchor part must end up higher (greater Y-up) than the below-anchor part.
            Assert.True(part0Position.PositionKeyframes[0].Value.Y > part1Position.PositionKeyframes[0].Value.Y);

            // Depth order: Alundra draws a frame's images in reverse, so part0 (image[0]) is
            // frontmost. CasaEngine draws higher DrawOrder in front, so part0 must carry the higher
            // DefaultDrawOrder. A no-DrawOrder track is expected (it's constant on the part).
            var part0 = animationData.Parts.Single(p => p.Id == "part0");
            var part1 = animationData.Parts.Single(p => p.Id == "part1");
            Assert.Equal(1, part0.DefaultDrawOrder);
            Assert.Equal(0, part1.DefaultDrawOrder);
            Assert.True(part0.DefaultDrawOrder > part1.DefaultDrawOrder);
            Assert.DoesNotContain(animationData.Tracks, t => t.Property == Animation2dTrackProperty.DrawOrder);
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
    public void ConvertSprites_TakesFrameDurationFromTheLowSevenBitsOfDelay()
    {
        // Reproduces the reported "animations are far too slow" bug. Only the low 7 bits of
        // SiFrame.Delay are the duration - bit 7 marks a frame that plays and then advances, and
        // the game reads it as Delay & 0x7f. Every displayable frame in the shipped data sets that
        // bit, so using the raw byte stretched a 0x88 walk frame from 8 game frames to 136.
        // The trailing frame carries no images and no duration either: its Delay is a control code
        // (1 loops, anything else chains), so it must land on the end of the last displayed frame.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

            // Two 0x88 frames (8 game frames each) closed by a Delay=1 loop terminator, the exact
            // shape of the real bank 0 walk cycles.
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 11 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 1100 }
                                        ] } },
                                        { "Delay": 136, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 1101 }
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

            var animationPath = FindEntityAsset(outputDirectory, "bank11_anim0_down.anim2d");
            var animationData = new Animation2dData();
            animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));

            var sprites = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Sprite);
            var visible = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Visible);

            // 0x88 -> 8 game frames -> 8/50 s, not 136/50 s.
            Assert.Equal(2, sprites.SpriteKeyframes.Count);
            Assert.Equal(0f, sprites.SpriteKeyframes[0].TimeSeconds);
            Assert.Equal(0.16f, sprites.SpriteKeyframes[1].TimeSeconds, 5);

            // The terminator hides the parts at the end of the second frame, and adds no time of
            // its own - so the animation lasts exactly its two frames.
            Assert.Equal(3, visible.VisibleKeyframes.Count);
            Assert.False(visible.VisibleKeyframes[2].Value);
            Assert.Equal(0.32f, visible.VisibleKeyframes[2].TimeSeconds, 5);
            Assert.Equal(0.32f, animationData.GetDurationSeconds(), 5);
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
    public void ConvertSprites_WithOddSizedQuads_CompensatesForTheIntegerSpriteOrigin()
    {
        // Reproduces the reported "bank0_anim1_down eats the top of the legs" bug. SpriteData.Origin
        // is a Point, so an odd-sized crop cannot store its own centre, and the renderer draws the
        // sprite centred on Position + (Width/2f - Origin.X, Origin.Y - Height/2f). That leftover
        // half pixel only appears on odd dimensions, so a 23x31 body paired with a 15x24 legs crop
        // slides half a pixel down onto the legs unless Position absorbs it.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

            // The real bank 0 / anim 1 / down frame 0 quads: a 23x31 body (odd on both axes) over a
            // 15x24 legs crop (odd width, even height).
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 9 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 23, "Sheight": 31, "X1": -11, "Y1": -36, "X2": 12, "Y2": -36, "X3": -11, "Y3": -5, "X4": 12, "Y4": -5, "Signature": 900 },
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 40, "AtlasY": 0, "Swidth": 15, "Sheight": 24, "X1": -7, "Y1": -15, "X2": 8, "Y2": -15, "X3": -7, "Y3": 9, "X4": 8, "Y4": 9, "Signature": 901 }
                                        ] } }
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

            var animationPath = FindEntityAsset(outputDirectory, "bank9_anim0_down.anim2d");
            var animationData = new Animation2dData();
            animationData.Load(JObject.Parse(File.ReadAllText(animationPath)));

            var part0Position = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part0" && t.Property == Animation2dTrackProperty.Position).PositionKeyframes[0].Value;
            var part1Position = Assert.Single(
                animationData.Tracks, t => t.TargetPartId == "part1" && t.Property == Animation2dTrackProperty.Position).PositionKeyframes[0].Value;

            // Odd width -> Origin.X is half a pixel short; odd height -> Origin.Y is too.
            Assert.Equal(0f, part0Position.X);
            Assert.Equal(21f, part0Position.Y);
            Assert.Equal(0f, part1Position.X);
            Assert.Equal(3f, part1Position.Y);

            // What actually matters is where the renderer ends up drawing each crop's centre:
            // Position + (Width/2f - Origin.X, Origin.Y - Height/2f) must land on the Alundra
            // destination rectangle's own centre (Y negated), for every part of the frame.
            AssertDrawnCentre(outputDirectory, animationData, "part0", expectedX: 0.5f, expectedY: 20.5f);
            AssertDrawnCentre(outputDirectory, animationData, "part1", expectedX: 0.5f, expectedY: 3f);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // Mirrors SpriteRendererComponent.DrawSprite / Animation2dBoundsCalculator, which both place a
    // part's crop centred on Position + (Width/2f - Origin.X, Origin.Y - Height/2f).
    private static void AssertDrawnCentre(
        string outputDirectory, Animation2dData animationData, string partId, float expectedX, float expectedY)
    {
        var position = Assert.Single(
            animationData.Tracks, t => t.TargetPartId == partId && t.Property == Animation2dTrackProperty.Position).PositionKeyframes[0].Value;
        var spriteId = Assert.Single(
            animationData.Tracks, t => t.TargetPartId == partId && t.Property == Animation2dTrackProperty.Sprite).SpriteKeyframes[0].Value;

        var spriteInfo = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == spriteId);
        var spriteData = new SpriteData();
        spriteData.Load(JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, spriteInfo.FileName))));

        Assert.Equal(expectedX, position.X + spriteData.PositionInTexture.Width / 2f - spriteData.Origin.X);
        Assert.Equal(expectedY, position.Y + spriteData.Origin.Y - spriteData.PositionInTexture.Height / 2f);
    }

    [Fact]
    public void ConvertSprites_WithAlundraAndMapSharingSameSector5Id_KeepsBothBanksSeparate()
    {
        // Regression test: map_alundra.json's SpriteRecords use their own id space, independent of
        // regular maps'. The map_alundra.json bank and a map bank can carry the same numeric
        // Sector5Id while being unrelated content; they must not collide/shadow each other.
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
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 5 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 111 }
                                        ] } },
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 111 }
                                        ] } },
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 111 }
                                        ] } }
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
                """
                {
                    "SpriteInfo": {
                        "SpriteRecords": [
                            {
                                "Header": { "Sector5Id": 5 },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 10, "Images": { "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 999 }
                                        ] } }
                                    ] },
                                    null, null, null
                                ] } ]
                            }
                        ],
                        "SpriteEffectRecords": []
                    }
                }
                """);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Sprites.Banks"]);

            var regularAnimationPath = FindEntityAsset(outputDirectory, "bank5_anim0_down.anim2d");
            var alundraAnimationPath = FindEntityAsset(outputDirectory, "bankalundra_5_anim0_down.anim2d");
            Assert.True(File.Exists(regularAnimationPath));
            Assert.True(File.Exists(alundraAnimationPath));

            var regularAnimation = new Animation2dData();
            regularAnimation.Load(JObject.Parse(File.ReadAllText(regularAnimationPath)));
            var regularSpriteTrack = Assert.Single(regularAnimation.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Equal(3, regularSpriteTrack.SpriteKeyframes.Count);

            var alundraAnimation = new Animation2dData();
            alundraAnimation.Load(JObject.Parse(File.ReadAllText(alundraAnimationPath)));
            var alundraSpriteTrack = Assert.Single(alundraAnimation.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Single(alundraSpriteTrack.SpriteKeyframes);
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
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
