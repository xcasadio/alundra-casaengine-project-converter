using System.Text.Json;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class ConversionReportTests
{
    [Theory]
    [InlineData("map_4: companion file not found.", "map_#")]
    [InlineData("map_311: companion file not found.", "map_#")]
    [InlineData("Font: glyph 12 is empty.", "Font")]
    [InlineData("EntityNames.csv: row 3 has no name.", "EntityNames.csv")]
    [InlineData("no prefix at all", "(uncategorized)")]
    public void CategorizeWarning_NormalizesDigitRunsInThePrefix(string warning, string expectedCategory)
    {
        Assert.Equal(expectedCategory, ConversionReport.CategorizeWarning(warning));
    }

    [Fact]
    public void GroupWarnings_PutsMapWarningsOfDifferentIndicesInTheSameCategory()
    {
        var report = new ConversionReport();
        report.Warnings.Add("map_4: companion file not found.");
        report.Warnings.Add("map_311: companion file not found.");
        report.Warnings.Add("Font: glyph 12 is empty.");

        var categories = report.GroupWarnings();

        Assert.Equal(2, categories.Count);

        // Ordered by descending count: the map bucket first.
        Assert.Equal("map_#", categories[0].Category);
        Assert.Equal(2, categories[0].Count);
        Assert.Equal(new[] { "map_4: companion file not found.", "map_311: companion file not found." }, categories[0].Examples);

        Assert.Equal("Font", categories[1].Category);
        Assert.Equal(1, categories[1].Count);
    }

    [Fact]
    public void Save_KeepsTheRawWarningListNextToTheGroupedSummaryAndWritesTheRunMetrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(Path.Combine(directory, "asset.bin"), new byte[16]);

            var report = new ConversionReport();
            report.Increment("Assets", 2);
            report.Warnings.Add("map_4: something odd.");
            report.RunPhase("Phase1.TileMaps", () => { });
            report.TotalDurationSeconds = 12.5;
            report.MeasureOutput(directory);

            var reportPath = Path.Combine(directory, "report.json");
            report.Save(reportPath);

            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = document.RootElement;

            // The grouping is a summary; the exact warning text must survive.
            Assert.Equal("map_4: something odd.", root.GetProperty("Warnings")[0].GetString());
            Assert.Equal("map_#", root.GetProperty("WarningsByCategory")[0].GetProperty("Category").GetString());
            Assert.Equal(1, root.GetProperty("WarningsByCategory")[0].GetProperty("Count").GetInt32());

            Assert.Equal(2, root.GetProperty("Counters").GetProperty("Assets").GetInt32());

            var metrics = root.GetProperty("Metrics");
            Assert.Equal(12.5, metrics.GetProperty("TotalDurationSeconds").GetDouble());
            Assert.True(metrics.GetProperty("PhaseDurationsSeconds").TryGetProperty("Phase1.TileMaps", out _));
            Assert.Equal(16, metrics.GetProperty("OutputSizeBytes").GetInt64());
            Assert.Equal(1, metrics.GetProperty("OutputFileCount").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
