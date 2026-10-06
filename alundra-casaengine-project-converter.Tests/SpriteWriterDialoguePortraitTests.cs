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
/// E19.f4a (docs/plan-e19-opcodes.md, F4A-R1..R3; parent ADR-0005 extended): the dialogue portrait of a sprite bank
/// (<c>SpriteRecords[i].DialoguePortrait</c> of the bank's canonical record, present exactly where the header bit 0x80 is set)
/// is exported as a <c>.sprite</c> under <c>UI/Portraits/</c> (the precedent of the inventory portrait,
/// <see cref="SpriteWriterInventoryPortraitTests"/>) and linked from <c>Data/sprite-records.json</c> by an optional
/// <c>DialoguePortrait {SpriteAssetId, Width, Height}</c> field of the speaker's prefab entry, omitted when there is none.
/// </summary>
public class SpriteWriterDialoguePortraitTests
{
    private const string Sheet = "map_0_spritesheet.png";

    // Minimal PNG signature: the writer copies the sheet without decoding it.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_BankWithPortrait_EmitsItsSpriteAndTheLinkOfItsPrefabOnly()
    {
        var records = new[]
        {
            Record(sector5Id: 50, flags: 131, animQuad: Quad(5000, 0, 0, 16, 16), portrait: Quad(5001, 100, 0, 48, 56)),
            Record(sector5Id: 52, flags: 3, animQuad: Quad(5200, 0, 0, 16, 16), portrait: null),
        };

        RunConversion(records, (outputDirectory, report, prefabs) =>
        {
            Assert.Empty(report.Errors);
            Assert.Empty(OwnWarnings(report));
            Assert.Equal(1, report.Counters["Sprites.DialoguePortrait"]);

            var expectedId = SpriteWriter.SpriteAssetId(Sheet, 5001);
            var spritePath = Path.Combine(outputDirectory, "UI", "Portraits", "sprite_5001.sprite");
            Assert.True(File.Exists(spritePath), $"'{spritePath}' was not written.");
            var document = JObject.Parse(File.ReadAllText(spritePath));
            var sprite = new SpriteData();
            sprite.Load(document);
            Assert.Equal(expectedId, sprite.Id);
            Assert.Equal("sprite_5001", sprite.Name);
            Assert.Equal(100, sprite.PositionInTexture.X);
            Assert.Equal(0, sprite.PositionInTexture.Y);
            Assert.Equal(48, sprite.PositionInTexture.Width);
            Assert.Equal(56, sprite.PositionInTexture.Height);
            Assert.Equal(SpritePsxSemiTransparency.None, sprite.PsxSemiTransparency);
            Assert.False(document.ContainsKey("psx_semi_transparency"));

            var cataloged = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == expectedId);
            Assert.Equal("sprite_5001", cataloged.Name);
            var sheetTexture = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == sprite.SpriteSheetAssetId);
            Assert.Contains("map_0_spritesheet", sheetTexture.Name);

            using var recordsDocument = ReadSpriteRecords(outputDirectory);
            var withPortrait = recordsDocument.RootElement.GetProperty(prefabs["50"].ToString());
            var link = withPortrait.GetProperty("DialoguePortrait");
            Assert.Equal(expectedId.ToString(), link.GetProperty("SpriteAssetId").GetString());
            Assert.Equal(48, link.GetProperty("Width").GetInt32());
            Assert.Equal(56, link.GetProperty("Height").GetInt32());

            // The other prefab keeps the entry it always had: no field at all, not a null.
            var without = recordsDocument.RootElement.GetProperty(prefabs["52"].ToString());
            Assert.False(without.TryGetProperty("DialoguePortrait", out _));
        });
    }

    [Fact]
    public void ConvertSprites_FlagSetAndFieldAbsent_WarnsExactlyOnceAndWritesNothing()
    {
        var records = new[] { Record(sector5Id: 51, flags: 131, animQuad: Quad(5100, 0, 0, 16, 16), portrait: null) };

        RunConversion(records, (outputDirectory, report, prefabs) =>
        {
            Assert.Empty(report.Errors);
            var warning = Assert.Single(OwnWarnings(report));
            Assert.Contains("bank_51", warning);
            Assert.Contains("DialoguePortrait", warning);
            Assert.False(report.Counters.ContainsKey("Sprites.DialoguePortrait"));
            Assert.False(Directory.Exists(Path.Combine(outputDirectory, "UI", "Portraits")));

            using var recordsDocument = ReadSpriteRecords(outputDirectory);
            Assert.False(recordsDocument.RootElement.GetProperty(prefabs["51"].ToString()).TryGetProperty("DialoguePortrait", out _));
        });
    }

    [Fact]
    public void ConvertSprites_FlagClearAndFieldAbsent_WritesNothingAndSaysNothing()
    {
        var records = new[] { Record(sector5Id: 53, flags: 3, animQuad: Quad(5300, 0, 0, 16, 16), portrait: null) };

        RunConversion(records, (outputDirectory, report, prefabs) =>
        {
            Assert.Empty(report.Errors);
            Assert.Empty(OwnWarnings(report));
            Assert.False(report.Counters.ContainsKey("Sprites.DialoguePortrait"));
            Assert.False(Directory.Exists(Path.Combine(outputDirectory, "UI", "Portraits")));
            Assert.DoesNotContain(EditorAssetCatalogService.AssetInfos, info => info.FileName.Contains("Portraits"));

            var recordsText = File.ReadAllText(Path.Combine(outputDirectory, "Data", "sprite-records.json"));
            Assert.DoesNotContain("DialoguePortrait", recordsText);
        });
    }

    [Fact]
    public void ConvertSprites_PortraitAlreadyAnAnimationSprite_ReusesItsIdAndWritesNoSecondFile()
    {
        // Bank 15 of the real data (Bonaire): the portrait cell is also a quad of the bank's own animation.
        var records = new[] { Record(sector5Id: 15, flags: 131, animQuad: Quad(1500, 245, 0, 48, 56), portrait: Quad(1500, 245, 0, 48, 56)) };

        RunConversion(records, (outputDirectory, report, prefabs) =>
        {
            Assert.Empty(report.Errors);
            Assert.Empty(OwnWarnings(report));
            Assert.Equal(1, report.Counters["Sprites.DialoguePortrait"]);
            Assert.False(Directory.Exists(Path.Combine(outputDirectory, "UI", "Portraits")));

            var existingId = SpriteWriter.SpriteAssetId(Sheet, 1500);
            Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == existingId);

            using var recordsDocument = ReadSpriteRecords(outputDirectory);
            var link = recordsDocument.RootElement.GetProperty(prefabs["15"].ToString()).GetProperty("DialoguePortrait");
            Assert.Equal(existingId.ToString(), link.GetProperty("SpriteAssetId").GetString());
        });
    }

    [Fact]
    public void ConvertSprites_Portrait48By72_KeepsItsTrueSize()
    {
        var records = new[] { Record(sector5Id: 122, flags: 131, animQuad: Quad(12200, 0, 0, 16, 16), portrait: Quad(12201, 147, 81, 48, 72)) };

        RunConversion(records, (outputDirectory, report, prefabs) =>
        {
            Assert.Empty(report.Errors);
            var sprite = new SpriteData();
            sprite.Load(JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "UI", "Portraits", "sprite_12201.sprite"))));
            Assert.Equal(72, sprite.PositionInTexture.Height);
            Assert.Equal(48, sprite.PositionInTexture.Width);

            using var recordsDocument = ReadSpriteRecords(outputDirectory);
            var link = recordsDocument.RootElement.GetProperty(prefabs["122"].ToString()).GetProperty("DialoguePortrait");
            Assert.Equal(48, link.GetProperty("Width").GetInt32());
            Assert.Equal(72, link.GetProperty("Height").GetInt32());
        });
    }

    [Fact]
    public void ReadAllBanks_OnTheRealExtraction_GivesThe25PortraitIdsOfTheTable()
    {
        var dataExtracted = FindUp(Path.Combine("data-extracted", "data", "map_alundra.json"));
        var tablePath = FindUp(Path.Combine("docs", "plan-e19-f4-annexe", "portraits_table.tsv"));
        if (dataExtracted is null || tablePath is null)
        {
            return; // data-extracted/ or the annex not present in this environment; nothing to assert against.
        }

        var inputDirectory = Path.GetDirectoryName(Path.GetDirectoryName(dataExtracted))!;
        var table = File.ReadAllLines(tablePath).Skip(1)
            .Select(line => line.Split('\t'))
            .ToDictionary(columns => int.Parse(columns[0]), columns => (Id: Guid.Parse(columns[3]), Width: int.Parse(columns[4]), Height: int.Parse(columns[5])));
        Assert.Equal(25, table.Count);

        var banks = SpriteBankReader.ReadAllBanks(inputDirectory, new ConversionReport());
        var found = banks.Where(bank => bank.DialoguePortrait != null).ToList();

        Assert.Equal(table.Keys.OrderBy(key => key), found.Select(bank => bank.Sector5Id).OrderBy(key => key));
        foreach (var bank in found)
        {
            var portrait = bank.DialoguePortrait!;
            var expected = table[bank.Sector5Id];
            Assert.Equal(expected.Id, SpriteWriter.SpriteAssetId(bank.SourceSpritesheetFileName, portrait.Signature));
            Assert.Equal(expected.Width, portrait.Width);
            Assert.Equal(expected.Height, portrait.Height);
        }
    }

    // The fixtures carry no map_alundra.json InventoryPortrait: that unrelated warning is not under test here.
    private static List<string> OwnWarnings(ConversionReport report)
    {
        return report.Warnings.Where(warning => !warning.Contains("InventoryPortrait")).ToList();
    }

    private static JsonDocument ReadSpriteRecords(string outputDirectory)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "Data", "sprite-records.json")));
    }

    private static string Quad(long signature, int atlasX, int atlasY, int width, int height)
    {
        return $$"""{ "Spritesheet": 0, "Palette": 0, "AtlasX": {{atlasX}}, "AtlasY": {{atlasY}}, "Swidth": {{width}}, "Sheight": {{height}}, "X1": -24, "Y1": -56, "X2": 24, "Y2": -56, "X3": -24, "Y3": 0, "X4": 24, "Y4": 0, "Signature": {{signature}} }""";
    }

    private static string Record(int sector5Id, int flags, string animQuad, string? portrait)
    {
        var portraitField = portrait == null ? string.Empty : $", \"DialoguePortrait\": {portrait}";
        return $$"""
            {
                "Header": { "Sector5Id": {{sector5Id}}, "MoreFlags": 0, "CanPickup": 0, "FlagsPortraitShadowType": {{flags}},
                            "ProgramLoad": 0, "ProgramTick": 0, "ProgramTouch": 0, "ProgramDeactivate": 0, "ProgramInteract": 0,
                            "OffsetX": 0, "OffsetY": 0, "OffsetZ": 0, "SizeX": 0, "SizeY": 0, "SizeZ": 0, "Contents": 0 },
                "AnimSets": [ { "PreloadedAnims": [
                    { "Frames": [
                        { "Delay": 136, "Images": { "DepthSortValue": 6, "Images": [ {{animQuad}} ] } },
                        { "Delay": 1, "Images": null }
                    ] },
                    null, null, null
                ] } ]{{portraitField}}
            }
            """;
    }

    private static void RunConversion(string[] records, Action<string, ConversionReport, IReadOnlyDictionary<string, Guid>> assert)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, Sheet), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_0.json"),
                "{ \"SpriteInfo\": { \"SpriteRecords\": [ " + string.Join(", ", records) + " ] } }");
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_alundra.json"),
                """{ "SpriteInfo": { "SpriteRecords": [], "SpriteEffectRecords": [] } }""");

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            var prefabs = SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            assert(outputDirectory, report, prefabs);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string? FindUp(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
