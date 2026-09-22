using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// D3.b of docs/plan-e13d-inventaire.md: drives <see cref="UiBoxLayoutReader.Read"/> off small synthetic
/// CSVs. The real linked <c>UiBoxes.csv</c>/<c>UiBoxCells.csv</c> are checked row for row against the
/// decompilation by D3.a's own acceptance, and pixel for pixel through the baked export by D3.b's.
/// </summary>
public class UiBoxLayoutReaderTests
{
    private static string NewCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "UiBoxLayoutReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void WithCsvs(string[] boxLines, string[] cellLines, Action<UiBoxLayoutReader.ReadResult> assert)
    {
        var boxesPath = NewCsv(boxLines);
        var cellsPath = NewCsv(cellLines);
        try
        {
            assert(UiBoxLayoutReader.Read(boxesPath, cellsPath));
        }
        finally
        {
            File.Delete(boxesPath);
            File.Delete(cellsPath);
        }
    }

    [Fact]
    public void Read_ParsesBoxesAndCells_InFileOrder_SkippingHeadersAndBlankLines()
    {
        WithCsvs(
            new[]
            {
                "box;x;y;width;height",
                "weapons;8;16;21;6",
                string.Empty,
                "spacer;240;112;0;0",
            },
            new[]
            {
                "box;cell;x0;y0;u0;v0;w;h;clut",
                "weapons;0;8;16;176;72;8;8;3",
                "weapons;1;16;16;184;72;8;8;3",
            },
            result =>
            {
                Assert.Empty(result.Warnings);
                Assert.Equal(
                    new[]
                    {
                        new UiBoxLayoutReader.UiBox("weapons", 8, 16, 21, 6),
                        new UiBoxLayoutReader.UiBox("spacer", 240, 112, 0, 0),
                    },
                    result.Boxes);
                Assert.Equal(
                    new[]
                    {
                        new UiBoxLayoutReader.UiBoxCell("weapons", 0, 8, 16, 176, 72, 8, 8, 3),
                        new UiBoxLayoutReader.UiBoxCell("weapons", 1, 16, 16, 184, 72, 8, 8, 3),
                    },
                    result.Cells);
            });
    }

    [Fact]
    public void Read_MalformedRows_AreWarnedAndSkipped()
    {
        WithCsvs(
            new[] { "box;x;y;width;height", "weapons;8;16;21;6", "broken;8;x;21;6", "short;1;2" },
            new[] { "box;cell;x0;y0;u0;v0;w;h;clut", "weapons;0;8;16;176;72;8;8;3", "weapons;1;16;16;184;72;8" },
            result =>
            {
                Assert.Single(result.Boxes);
                Assert.Single(result.Cells);
                Assert.Equal(3, result.Warnings.Count);
                Assert.Contains(result.Warnings, w => w.StartsWith("UiBoxes.csv: malformed row 3", StringComparison.Ordinal));
                Assert.Contains(result.Warnings, w => w.StartsWith("UiBoxes.csv: malformed row 4", StringComparison.Ordinal));
                Assert.Contains(result.Warnings, w => w.StartsWith("UiBoxCells.csv: malformed row 3", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void Read_CellOfAnUnlistedBox_IsWarnedAndSkipped()
    {
        WithCsvs(
            new[] { "box;x;y;width;height", "weapons;8;16;21;6" },
            new[] { "box;cell;x0;y0;u0;v0;w;h;clut", "items;0;8;64;176;72;8;8;3" },
            result =>
            {
                Assert.Empty(result.Cells);
                var warning = Assert.Single(result.Warnings);
                Assert.Contains("'items'", warning, StringComparison.Ordinal);
            });
    }
}
