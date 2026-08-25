using System.Text.Json;
using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class UiWriterTests
{
    // The writer copies PNGs verbatim and never decodes them: the 8-byte signature is enough.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertUi_NamesWindSpritesByIndexSoIdenticalRectanglesDoNotCollide()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteUiFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            UiWriter.ConvertUi(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(3, report.Counters["Assets.UiSprite"]);

            // Entries 0 and 2 of the fixture describe the exact same rectangle and palette, as
            // several real wind.json entries do; naming by index keeps them two distinct assets.
            var firstPath = Path.Combine(outputDirectory, "UI", "wind_000.sprite");
            var duplicatePath = Path.Combine(outputDirectory, "UI", "wind_002.sprite");
            Assert.True(File.Exists(firstPath));
            Assert.True(File.Exists(duplicatePath));

            var second = new SpriteData();
            second.Load(JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "UI", "wind_001.sprite"))));
            Assert.Equal(8, second.PositionInTexture.X);
            Assert.Equal(56, second.PositionInTexture.Y);
            Assert.Equal(9, second.PositionInTexture.Width);
            Assert.Equal(16, second.PositionInTexture.Height);
            Assert.Equal(4, second.Origin.X); // crop centre, integer-truncated like Phase 3
            Assert.Equal(8, second.Origin.Y);

            // Every sprite points at the one wind.texture wrapper, not at the raw PNG entry.
            var wrapperPath = Path.Combine(outputDirectory, "UI", "Textures", "wind.texture");
            Assert.True(File.Exists(wrapperPath));
            var wrapperDocument = JObject.Parse(File.ReadAllText(wrapperPath));
            Assert.Equal(wrapperDocument["id"]!.ToString(), second.SpriteSheetAssetId.ToString());

            var texture = new CasaEngine.Framework.Assets.Textures.Texture();
            texture.Load(wrapperDocument);
            Assert.Equal(SamplerState.PointClamp.Filter, texture.PreferredSamplerState.Filter);

            // PaletteIndex has nowhere to go in SpriteData, so it must survive in the companion.
            using var companion = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "UI", "wind-sprites.json")));
            var rows = companion.RootElement.EnumerateArray().ToArray();
            Assert.Equal(3, rows.Length);
            Assert.Equal(5, rows[0].GetProperty("palette_index").GetInt32());
            Assert.Equal(7, rows[1].GetProperty("palette_index").GetInt32());
            Assert.Equal("wind_002", rows[2].GetProperty("name").GetString());
            Assert.Equal(second.Id, rows[1].GetProperty("asset_id").GetGuid());

            // wind.png + 1 memory card frame + 2 closing screens + the loading screen.
            Assert.Equal(5, report.Counters["Assets.UiTexture"]);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "closing_01.png")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "loading_screen.texture")));
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
    public void ConvertUi_BalanceKeepsUnknownFieldsAndDropsTheExtractorPath()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteUiFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            UiWriter.ConvertUi(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Data.BalanceRecords"]);

            using var balance = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Data", "balance.json")));
            var root = balance.RootElement;

            // FileName is the absolute path of the .BIN on the extractor's machine: provenance, not
            // game data, and keeping it would make the output machine-dependent.
            Assert.False(root.TryGetProperty("FileName", out _));

            // Everything else keeps its original (unknown-meaning) name and structure.
            Assert.True(root.TryGetProperty("Offsets", out var offsets));
            Assert.Equal(2, offsets.GetArrayLength());

            var firstRecord = root.GetProperty("BalanceRecords")[0];
            Assert.Equal(255, firstRecord.GetProperty("Level").GetInt32());
            Assert.Equal(169, firstRecord.GetProperty("OffsetToNextLevel").GetInt32());
            Assert.Equal(3, firstRecord.GetProperty("Values").GetArrayLength());
            Assert.Equal(2, firstRecord.GetProperty("NumAnimVals").GetInt32());
            Assert.Equal(133, firstRecord.GetProperty("AnimVals")[1].GetProperty("Val").GetInt32());
            Assert.Equal(2, firstRecord.GetProperty("AnimVals")[1].GetProperty("U2").GetInt32());

            Assert.Contains(report.Messages, message => message.Contains("FileName", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteUiFixture(string inputDirectory)
    {
        var uiDirectory = Path.Combine(inputDirectory, "ui");
        var dataDirectory = Path.Combine(inputDirectory, "data");
        var memoryCardDirectory = Path.Combine(inputDirectory, "memorycard");
        var closingDirectory = Path.Combine(inputDirectory, "closing");
        Directory.CreateDirectory(uiDirectory);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(memoryCardDirectory);
        Directory.CreateDirectory(closingDirectory);

        File.WriteAllBytes(Path.Combine(uiDirectory, "wind.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(dataDirectory, "loading_screen.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(memoryCardDirectory, "memorycardframe1.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(closingDirectory, "closing_00.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(closingDirectory, "closing_01.png"), FakePngBytes);

        // Entries 0 and 2 are byte-identical records: the real table does this too.
        File.WriteAllText(
            Path.Combine(uiDirectory, "wind.json"),
            """
            [
                { "U0": 0, "V0": 40, "Width": 8, "Height": 16, "PaletteIndex": 5 },
                { "U0": 8, "V0": 56, "Width": 9, "Height": 16, "PaletteIndex": 7 },
                { "U0": 0, "V0": 40, "Width": 8, "Height": 16, "PaletteIndex": 5 }
            ]
            """);

        File.WriteAllText(
            Path.Combine(dataDirectory, "BALANCE.BIN.json"),
            """
            {
                "FileName": "D:\\development\\repo\\Alundra Remake\\DATA\\BALANCE.BIN",
                "BalanceRecords": [
                    {
                        "Level": 255,
                        "OffsetToNextLevel": 169,
                        "Hp": 10,
                        "Values": [ 0, 0, 0 ],
                        "NumAnimVals": 2,
                        "AnimVals": [ { "Val": 0, "U2": 0 }, { "Val": 133, "U2": 2 } ],
                        "Offset": 0,
                        "Next": 169
                    },
                    {
                        "Level": 1,
                        "OffsetToNextLevel": 42,
                        "Hp": 20,
                        "Values": [ 1, 2, 3 ],
                        "NumAnimVals": 0,
                        "AnimVals": [],
                        "Offset": 169,
                        "Next": 211
                    }
                ],
                "Offsets": [ 0, 169 ]
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
