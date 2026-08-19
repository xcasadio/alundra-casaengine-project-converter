using System.Linq;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers Data/sprite-records.json (see SpriteWriter's class summary): one raw entry per emitted
/// prefab, keyed by the prefab's own asset id, carrying SpriteRecord.Header's runtime fields for
/// the future gameplay DLL.
/// </summary>
public class SpriteWriterSpriteRecordsTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertSprites_WithAFullHeader_WritesTheRawHeaderFieldsKeyedByPrefabId()
    {
        RunConversion(
            sector5Id: 50,
            headerFields: """
                "MoreFlags": 140, "CanPickup": 17, "FlagsPortraitShadowType": 3,
                "ProgramLoad": 5, "ProgramTick": 6, "ProgramTouch": 7, "ProgramDeactivate": 8,
                "ProgramInteract": 9,
                "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "SizeX": 21, "SizeY": 15, "SizeZ": 32,
                "Contents": 4
                """,
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["SpriteRecords.Exported"]);
                Assert.Empty(report.Errors);

                var prefabAssetId = prefabAssetIdsByBankKey["50"];
                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                Assert.True(File.Exists(recordsPath));

                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetId.ToString());

                Assert.Equal(140, record.GetProperty("MoreFlags").GetInt32());
                Assert.Equal(17, record.GetProperty("CanPickup").GetInt32());
                Assert.Equal(3, record.GetProperty("FlagsPortraitShadowType").GetInt32());
                Assert.Equal(5, record.GetProperty("ProgramLoad").GetInt32());
                Assert.Equal(6, record.GetProperty("ProgramTick").GetInt32());
                Assert.Equal(7, record.GetProperty("ProgramTouch").GetInt32());
                Assert.Equal(8, record.GetProperty("ProgramDeactivate").GetInt32());
                Assert.Equal(9, record.GetProperty("ProgramInteract").GetInt32());
                Assert.Equal(-10, record.GetProperty("OffsetX").GetInt32());
                Assert.Equal(-7, record.GetProperty("OffsetY").GetInt32());
                Assert.Equal(0, record.GetProperty("OffsetZ").GetInt32());
                Assert.Equal(21, record.GetProperty("SizeX").GetInt32());
                Assert.Equal(15, record.GetProperty("SizeY").GetInt32());
                Assert.Equal(32, record.GetProperty("SizeZ").GetInt32());
                Assert.Equal(4, record.GetProperty("Contents").GetInt32());

                // The asset is a plain companion, not registered like the .sprite/.entity assets are.
                Assert.DoesNotContain(
                    EditorAssetCatalogService.AssetInfos, info => info.FileName.Contains("sprite-records"));
            });
    }

    [Fact]
    public void ConvertSprites_WithAZeroBodyBox_StillGetsASpriteRecordEntry()
    {
        // Sprite-only prefabs (degenerate or absent body box) still declare a header.
        RunConversion(
            sector5Id: 51,
            headerFields: """
                "MoreFlags": 12, "CanPickup": 0, "FlagsPortraitShadowType": 1,
                "ProgramLoad": 0, "ProgramTick": 0, "ProgramTouch": 0, "ProgramDeactivate": 0,
                "ProgramInteract": 0,
                "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "SizeX": 20, "SizeY": 14, "SizeZ": 0,
                "Contents": 0
                """,
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["SpriteRecords.Exported"]);
                Assert.Equal(report.Counters["Entities.Prefabs"], report.Counters["SpriteRecords.Exported"]);
                Assert.Empty(report.Errors);

                var prefabAssetId = prefabAssetIdsByBankKey["51"];
                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetId.ToString());

                Assert.Equal(12, record.GetProperty("MoreFlags").GetInt32());
                Assert.Equal(20, record.GetProperty("SizeX").GetInt32());
                Assert.Equal(14, record.GetProperty("SizeY").GetInt32());
                Assert.Equal(0, record.GetProperty("SizeZ").GetInt32());
            });
    }

    [Fact]
    public void ConvertSprites_WithVaryingPerFrameIdsv_RoundTripsTheFullFrameList()
    {
        // The fixture's one converted (anim, direction) pair carries two displayed frames with
        // DepthSortValue 6 then 3 (SiImageSet.DepthSortValue, EntityManager.cs:338/1049) followed by a
        // terminator frame (Images: null, no Idsv of its own) - see SpriteBankReader.ReadAnimation.
        RunConversion(
            sector5Id: 52,
            headerFields: """
                "MoreFlags": 0, "CanPickup": 0, "FlagsPortraitShadowType": 0,
                "ProgramLoad": 0, "ProgramTick": 0, "ProgramTouch": 0, "ProgramDeactivate": 0,
                "ProgramInteract": 0,
                "OffsetX": 0, "OffsetY": 0, "OffsetZ": 0, "SizeX": 0, "SizeY": 0, "SizeZ": 0,
                "Contents": 0
                """,
            (outputDirectory, report, prefabAssetIdsByBankKey) =>
            {
                Assert.Equal(1, report.Counters["SpriteRecords.IdsvAnimDirs"]);

                var prefabAssetId = prefabAssetIdsByBankKey["52"];
                var recordsPath = Path.Combine(outputDirectory, "Data", "sprite-records.json");
                using var document = JsonDocument.Parse(File.ReadAllText(recordsPath));
                var record = document.RootElement.GetProperty(prefabAssetId.ToString());

                var idsvAnimDirs = record.GetProperty("IdsvAnimDirs");
                Assert.Equal(1, idsvAnimDirs.GetArrayLength());

                var entry = idsvAnimDirs[0];
                Assert.Equal(0, entry.GetProperty("Anim").GetInt32());
                Assert.Equal(0, entry.GetProperty("Direction").GetInt32());

                var frames = entry.GetProperty("Frames").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                Assert.Equal(new[] { 6, 3 }, frames);
            });
    }

    /// <summary>
    /// Converts a one-bank fixture whose header carries <paramref name="headerFields"/> (the raw
    /// JSON fields, comma-separated, following "Sector5Id"), then hands the output directory, the
    /// report and the bank-key -&gt; prefab id map to <paramref name="assert"/>.
    /// </summary>
    private static void RunConversion(
        int sector5Id, string headerFields,
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
                                "Header": { "Sector5Id": {{sector5Id}}, {{headerFields}} },
                                "AnimSets": [ { "PreloadedAnims": [
                                    { "Frames": [
                                        { "Delay": 136, "Images": { "DepthSortValue": 6, "Images": [
                                            { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{sector5Id}}0 }
                                        ] } },
                                        { "Delay": 136, "Images": { "DepthSortValue": 3, "Images": [
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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
