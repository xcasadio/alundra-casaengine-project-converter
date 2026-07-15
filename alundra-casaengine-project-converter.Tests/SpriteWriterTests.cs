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

            var animationPath = Path.Combine(outputDirectory, "Sprites", "bank_5", "bank5_anim0_down.anim2d");
            Assert.True(File.Exists(animationPath));

            var animationDocument = JObject.Parse(File.ReadAllText(animationPath));
            var animationData = new Animation2dData();
            animationData.Load(animationDocument);

            Assert.Single(animationData.Parts);
            var spriteTrack = Assert.Single(animationData.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Equal(2, spriteTrack.SpriteKeyframes.Count);
            Assert.NotEqual(spriteTrack.SpriteKeyframes[0].Value, spriteTrack.SpriteKeyframes[1].Value);

            var firstSpritePath = Path.Combine(outputDirectory, "Sprites", "bank_5", "sprite_111.sprite");
            var secondSpritePath = Path.Combine(outputDirectory, "Sprites", "bank_5", "sprite_222.sprite");
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
            Assert.Equal(SamplerState.AnisotropicWrap.Filter, texture.PreferredSamplerState.Filter);
            Assert.Equal(SamplerState.AnisotropicWrap.AddressU, texture.PreferredSamplerState.AddressU);
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
    public void ConvertSprites_WithHeroAndMapSharingSameSector5Id_KeepsBothBanksSeparate()
    {
        // Regression test: the hero's SpriteRecords (map_alundra.json) use their own id space,
        // independent of regular maps'. A hero bank and a map bank can carry the same numeric
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

            var regularAnimationPath = Path.Combine(outputDirectory, "Sprites", "bank_5", "bank5_anim0_down.anim2d");
            var heroAnimationPath = Path.Combine(outputDirectory, "Sprites", "bank_hero_5", "bankhero_5_anim0_down.anim2d");
            Assert.True(File.Exists(regularAnimationPath));
            Assert.True(File.Exists(heroAnimationPath));

            var regularAnimation = new Animation2dData();
            regularAnimation.Load(JObject.Parse(File.ReadAllText(regularAnimationPath)));
            var regularSpriteTrack = Assert.Single(regularAnimation.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Equal(3, regularSpriteTrack.SpriteKeyframes.Count);

            var heroAnimation = new Animation2dData();
            heroAnimation.Load(JObject.Parse(File.ReadAllText(heroAnimationPath)));
            var heroSpriteTrack = Assert.Single(heroAnimation.Tracks, t => t.Property == Animation2dTrackProperty.Sprite);
            Assert.Single(heroSpriteTrack.SpriteKeyframes);
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
