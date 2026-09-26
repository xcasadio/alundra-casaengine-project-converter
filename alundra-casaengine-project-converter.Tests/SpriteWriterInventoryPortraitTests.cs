using System.Text.Json;
using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Sprites;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// The inventory's opening portrait (docs/plan-portrait-inventaire.md, PI5): sprite record 0's portrait
/// image reaches the converter only through <c>map_alundra.json</c>'s top-level
/// <see cref="SpriteBankReader.InventoryPortraitPropertyName"/>, since no animation uses it. The values below
/// are the real portrait's (page 2, palette 16, atlas (200, 568), 48x56, signature 61779762221058).
/// </summary>
public class SpriteWriterInventoryPortraitTests
{
    private const long PortraitSignature = 61779762221058;

    // Minimal PNG signature: the writer copies the sheet without decoding it.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private const string AlundraMapWithPortrait =
        """
        {
            "SpriteInfo": { "SpriteRecords": [] },
            "InventoryPortrait": { "Spritesheet": 2, "Palette": 16, "AtlasX": 200, "AtlasY": 568, "Swidth": 48, "Sheight": 56, "X1": -24, "Y1": -56, "X2": 24, "Y2": -56, "X3": -24, "Y3": 0, "X4": 24, "Y4": 0, "Signature": 61779762221058 }
        }
        """;

    private const string AlundraMapWithoutPortrait =
        """
        {
            "SpriteInfo": { "SpriteRecords": [] }
        }
        """;

    [Fact]
    public void ConvertSprites_WithInventoryPortrait_EmitsItsSpriteAndIndex()
    {
        RunConversion(AlundraMapWithPortrait, (outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Sprites.InventoryPortrait"]);

            var expectedId = SpriteWriter.SpriteAssetId(SpriteBankReader.AlundraSpritesheetFileName, PortraitSignature);

            var spritePath = Path.Combine(outputDirectory, "UI", "Portraits", $"sprite_{PortraitSignature}.sprite");
            Assert.True(File.Exists(spritePath), $"'{spritePath}' was not written.");
            var sprite = new SpriteData();
            sprite.Load(JObject.Parse(File.ReadAllText(spritePath)));
            Assert.Equal(expectedId, sprite.Id);
            Assert.Equal($"sprite_{PortraitSignature}", sprite.Name);
            Assert.Equal(200, sprite.PositionInTexture.X);
            Assert.Equal(568, sprite.PositionInTexture.Y);
            Assert.Equal(48, sprite.PositionInTexture.Width);
            Assert.Equal(56, sprite.PositionInTexture.Height);

            var cataloged = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == expectedId);
            Assert.Equal($"sprite_{PortraitSignature}", cataloged.Name);
            var sheetTexture = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == sprite.SpriteSheetAssetId);
            Assert.Contains("map_alundra_spritesheet", sheetTexture.Name);

            var indexPath = Path.Combine(outputDirectory, "Data", "inventory-portrait.json");
            Assert.True(File.Exists(indexPath), $"'{indexPath}' was not written.");
            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            Assert.Equal(expectedId.ToString(), index.RootElement.GetProperty("SpriteAssetId").GetString());
        });
    }

    [Fact]
    public void ConvertSprites_WithoutInventoryPortrait_WritesNoIndexAndOnlyWarns()
    {
        RunConversion(AlundraMapWithoutPortrait, (outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.False(report.Counters.ContainsKey("Sprites.InventoryPortrait"));
            Assert.Contains(report.Warnings, warning => warning.Contains(SpriteBankReader.InventoryPortraitPropertyName));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Data", "inventory-portrait.json")));
            Assert.False(Directory.Exists(Path.Combine(outputDirectory, "UI", "Portraits")));
        });
    }

    [Fact]
    public void ConvertSprites_InventoryPortrait_IsByteIdenticalAcrossRuns()
    {
        byte[]? firstIndex = null, firstSprite = null;
        for (var run = 0; run < 2; run++)
        {
            RunConversion(AlundraMapWithPortrait, (outputDirectory, report) =>
            {
                var index = File.ReadAllBytes(Path.Combine(outputDirectory, "Data", "inventory-portrait.json"));
                var sprite = File.ReadAllBytes(Path.Combine(outputDirectory, "UI", "Portraits", $"sprite_{PortraitSignature}.sprite"));
                if (firstIndex == null)
                {
                    firstIndex = index;
                    firstSprite = sprite;
                }
                else
                {
                    Assert.Equal(firstIndex, index);
                    Assert.Equal(firstSprite, sprite);
                }
            });
        }

        Assert.NotNull(firstIndex);
    }

    private static void RunConversion(string alundraMapJson, Action<string, ConversionReport> assert)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);
            File.WriteAllText(Path.Combine(dataDirectory, "map_alundra.json"), alundraMapJson);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

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
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
