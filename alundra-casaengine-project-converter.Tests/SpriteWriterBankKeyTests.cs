using System.Linq;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers E20.e0x (ADR-0062): the "BankKey" first member of every Data/sprite-records.json entry, and
/// Data/map-sprite-indices.json (the sprite-table indices valid on each map, D-E20-33).
/// </summary>
public class SpriteWriterBankKeyTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_WritesBankKeyAsTheFirstMemberOfEveryEntry()
    {
        RunConversion(
            new Dictionary<string, string>
            {
                ["map_alundra.json"] = MapJson(new[] { Record(2) }, spriteTable: null),
                ["map_0.json"] = MapJson(new[] { Record(91) }, spriteTable: null),
            },
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Empty(report.Errors);
                using var document = JsonDocument.Parse(File.ReadAllText(RecordsPath(outputDirectory)));

                // A map bank is keyed by its Sector5Id alone, the global bank by "alundra_" + Sector5Id.
                var mapEntry = document.RootElement.GetProperty(prefabAssetIdsByBankKey["91"].ToString());
                var globalEntry = document.RootElement.GetProperty(prefabAssetIdsByBankKey["alundra_2"].ToString());
                Assert.Equal("91", mapEntry.GetProperty("BankKey").GetString());
                Assert.Equal("alundra_2", globalEntry.GetProperty("BankKey").GetString());

                foreach (var entry in document.RootElement.EnumerateObject())
                {
                    Assert.Equal("BankKey", entry.Value.EnumerateObject().First().Name);
                }

                // The key is the first line of the object in the text itself, right after the entry's opening brace.
                var lines = File.ReadAllText(RecordsPath(outputDirectory)).Split("\r\n");
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("  \"", StringComparison.Ordinal) && lines[i].EndsWith("\": {", StringComparison.Ordinal))
                    {
                        Assert.StartsWith("    \"BankKey\": \"", lines[i + 1], StringComparison.Ordinal);
                    }
                }
            });
    }

    [Fact]
    public void ResolveBankKey_WithAPrefabThatHasNoKey_IsAHardError()
    {
        var prefabAssetId = Guid.NewGuid();
        var keysByPrefabId = new Dictionary<Guid, string> { [Guid.NewGuid()] = "7" };
        var report = new ConversionReport();

        var key = SpriteWriter.ResolveBankKey(prefabAssetId, keysByPrefabId, report);

        Assert.Equal(string.Empty, key);
        var error = Assert.Single(report.Errors);
        Assert.Contains("BankKey", error, StringComparison.Ordinal);
        Assert.Contains(prefabAssetId.ToString(), error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveBankKey_WithAKnownPrefab_ReturnsItsKeyWithoutAnError()
    {
        var prefabAssetId = Guid.NewGuid();
        var report = new ConversionReport();

        var key = SpriteWriter.ResolveBankKey(
            prefabAssetId, new Dictionary<Guid, string> { [prefabAssetId] = "alundra_0" }, report);

        Assert.Equal("alundra_0", key);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void ConvertSprites_WritesTheValidIndicesOfEachMapInNumericOrderSortedWithEmptyArrays()
    {
        // Maps 10, 2 and 3 (a string sort would put "10" before "2"); map 3 has nulls between its records, and
        // is listed out of order on purpose; map 10 has no SpriteInfo.SpriteRecords at all.
        RunConversion(
            new Dictionary<string, string>
            {
                ["map_alundra.json"] = MapJson(Array.Empty<string>(), spriteTable: null),
                ["map_10.json"] = """{ "SpriteInfo": { "SpriteTable": [ -1, -1 ] } }""",
                ["map_2.json"] = MapJson(Array.Empty<string>(), spriteTable: null),
                ["map_3.json"] = MapJson(
                    new[] { "null", Record(5), "null", "null", Record(7), Record(5) }, spriteTable: new[] { -1, 0, -1, -1, 1, 0 }),
            },
            (outputDirectory, report, _) =>
            {
                Assert.Empty(report.Errors);
                var path = Path.Combine(outputDirectory, "Data", "map-sprite-indices.json");
                Assert.True(File.Exists(path));
                var text = File.ReadAllText(path);

                using var document = JsonDocument.Parse(text);
                Assert.Equal(new[] { "2", "3", "10" }, document.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
                Assert.Equal(0, document.RootElement.GetProperty("2").GetArrayLength());
                Assert.Equal(0, document.RootElement.GetProperty("10").GetArrayLength());
                Assert.Equal(
                    new[] { 1, 4, 5 },
                    document.RootElement.GetProperty("3").EnumerateArray().Select(e => e.GetInt32()).ToArray());

                // The exact text: CRLF, two spaces per level, one number per line, "[]" for an empty array, no BOM, no final line break.
                Assert.Equal(
                    "{\r\n  \"2\": [],\r\n  \"3\": [\r\n    1,\r\n    4,\r\n    5\r\n  ],\r\n  \"10\": []\r\n}", text);
                Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);

                // A plain companion, never registered like the .sprite/.entity assets are.
                Assert.DoesNotContain(
                    EditorAssetCatalogService.AssetInfos, info => info.FileName.Contains("map-sprite-indices"));
            });
    }

    [Fact]
    public void ConvertSprites_WhenARecordAndItsTableEntryDisagree_IsAHardError()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        try
        {
            var report = Convert(
                inputDirectory, outputDirectory,
                new Dictionary<string, string>
                {
                    ["map_alundra.json"] = MapJson(Array.Empty<string>(), spriteTable: null),
                    // Record 1 is not null, but the table says -1 there.
                    ["map_4.json"] = MapJson(new[] { "null", Record(5) }, spriteTable: new[] { -1, -1 }),
                });

            var error = Assert.Single(report.Errors);
            Assert.Contains("map_4", error, StringComparison.Ordinal);
            Assert.Contains("SpriteTable", error, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(inputDirectory, outputDirectory);
        }
    }

    private static string RecordsPath(string outputDirectory)
    {
        return Path.Combine(outputDirectory, "Data", "sprite-records.json");
    }

    private static string Record(int sector5Id)
    {
        return $$"""
            {
                "Header": { "Sector5Id": {{sector5Id}}, "MoreFlags": 0, "CanPickup": 0, "FlagsPortraitShadowType": 0,
                    "ProgramLoad": 0, "ProgramTick": 0, "ProgramTouch": 0, "ProgramDeactivate": 0, "ProgramInteract": 0,
                    "OffsetX": 0, "OffsetY": 0, "OffsetZ": 0, "SizeX": 0, "SizeY": 0, "SizeZ": 0, "Contents": 0 },
                "AnimSets": [ { "PreloadedAnims": [
                    { "Frames": [
                        { "Delay": 136, "Images": { "DepthSortValue": 6, "Images": [
                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
                        ] } },
                        { "Delay": 1, "Images": null }
                    ] },
                    null, null, null
                ] } ]
            }
            """;
    }

    private static string MapJson(IReadOnlyList<string> records, int[]? spriteTable)
    {
        var table = spriteTable == null ? string.Empty : $", \"SpriteTable\": [ {string.Join(", ", spriteTable)} ]";
        return $$"""{ "SpriteInfo": { "SpriteRecords": [ {{string.Join(",\n", records)}} ]{{table}}, "SpriteEffectRecords": [] } }""";
    }

    private static void RunConversion(
        Dictionary<string, string> mapFiles,
        Action<string, ConversionReport, IReadOnlyDictionary<string, Guid>> assert)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            var report = Convert(inputDirectory, outputDirectory, mapFiles, out var prefabAssetIdsByBankKey);
            assert(outputDirectory, report, prefabAssetIdsByBankKey);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Cleanup(inputDirectory, outputDirectory);
        }
    }

    private static ConversionReport Convert(
        string inputDirectory, string outputDirectory, Dictionary<string, string> mapFiles)
    {
        var previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            return Convert(inputDirectory, outputDirectory, mapFiles, out _);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    private static ConversionReport Convert(
        string inputDirectory, string outputDirectory, Dictionary<string, string> mapFiles,
        out IReadOnlyDictionary<string, Guid> prefabAssetIdsByBankKey)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        foreach (var (fileName, content) in mapFiles)
        {
            File.WriteAllText(Path.Combine(dataDirectory, fileName), content);
            File.WriteAllBytes(
                Path.Combine(dataDirectory, Path.GetFileNameWithoutExtension(fileName) + "_spritesheet.png"), FakePngBytes);
        }

        EngineEnvironment.ProjectPath = outputDirectory;
        EditorAssetCatalogService.Clear();
        var report = new ConversionReport();
        ProjectWriter.CreateEmptyProject(outputDirectory, report);
        prefabAssetIdsByBankKey = SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);
        return report;
    }

    private static void Cleanup(string inputDirectory, string outputDirectory)
    {
        EditorAssetCatalogService.Clear();
        Directory.Delete(inputDirectory, recursive: true);
        Directory.Delete(outputDirectory, recursive: true);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
