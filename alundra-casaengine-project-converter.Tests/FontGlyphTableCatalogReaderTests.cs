using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.f2b0 (docs/plan-e19-opcodes.md): drives <see cref="FontGlyphTableCatalogReader.Read"/> directly, off
/// small synthetic CSVs, plus the real linked <c>FontGlyphTable.csv</c> (256 rows, nothing skipped).
/// </summary>
public class FontGlyphTableCatalogReaderTests
{
    private const string Header = "code;width;height;srcX;srcY;yOffset";

    [Fact]
    public void Read_ParsesEveryRow_SkippingTheHeader()
    {
        var path = WriteCsv(Header, "0;16;16;0;0;0", "9;5;16;144;224;0", "156;9;16;195;144;2");

        try
        {
            var result = FontGlyphTableCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.EntryByRawCode.Count);
            Assert.Equal(new FontGlyphTableCatalogReader.GlyphEntry(16, 16, 0, 0, 0), result.EntryByRawCode[0]);
            Assert.Equal(new FontGlyphTableCatalogReader.GlyphEntry(5, 16, 144, 224, 0), result.EntryByRawCode[9]);
            Assert.Equal(new FontGlyphTableCatalogReader.GlyphEntry(9, 16, 195, 144, 2), result.EntryByRawCode[156]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MalformedRow_WarnsAndSkipsIt_ButKeepsTheOthers()
    {
        var path = WriteCsv(Header, "0;16;16;0;0;0", "x;1;1;1;1;1", "2;16;16;32", "3;16;16;48;0;0");

        try
        {
            var result = FontGlyphTableCatalogReader.Read(path);

            Assert.Equal(2, result.Warnings.Count);
            Assert.All(result.Warnings, warning => Assert.Contains("FontGlyphTable.csv", warning, StringComparison.Ordinal));
            Assert.Equal(2, result.EntryByRawCode.Count);
            Assert.False(result.EntryByRawCode.ContainsKey(2));
            Assert.Equal(48, result.EntryByRawCode[3].SrcX);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_TheLinkedCsv_Reads256RowsWithoutAWarning()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "FontGlyphTable.csv");
        Assert.True(File.Exists(path), $"FontGlyphTable.csv is not linked into '{AppContext.BaseDirectory}'.");

        var result = FontGlyphTableCatalogReader.Read(path);

        Assert.Empty(result.Warnings);
        Assert.Equal(256, result.EntryByRawCode.Count);
        Assert.Equal(Enumerable.Range(0, 256), result.EntryByRawCode.Keys.Order());
    }

    private static string WriteCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "FontGlyphTableCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, lines);
        return path;
    }
}
