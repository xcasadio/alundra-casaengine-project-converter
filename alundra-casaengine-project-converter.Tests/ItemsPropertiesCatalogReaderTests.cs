using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// S2 of docs/plan-e13c-icones-hud.md, D-E13C-2: drives <see cref="ItemsPropertiesCatalogReader.Read"/>
/// directly, off a small synthetic CSV rather than the real linked <c>ItemsProperties.csv</c>, whose
/// 100 rows the export's own acceptance compares column by column.
/// </summary>
public class ItemsPropertiesCatalogReaderTests
{
    private static string NewCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "ItemsPropertiesCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Read_ParsesEveryRow_SkippingTheHeaderAndBlankLines()
    {
        var path = NewCsv(
            "item_id;slot;replace_flag;priority;max_count;icon",
            "0;0;0;0;0;65535",
            string.Empty,
            "1;1;1;0;1;31",
            "2;1;1;1;1;32");

        try
        {
            var result = ItemsPropertiesCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.RowsByItemId.Count);
            Assert.Equal(new[] { 0, 0, 0, 0, 65535 }, result.RowsByItemId[0]);
            Assert.Equal(new[] { 1, 1, 0, 1, 31 }, result.RowsByItemId[1]);
            Assert.Equal(new[] { 1, 1, 1, 1, 32 }, result.RowsByItemId[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MalformedRow_WarnsAndSkipsIt_ButKeepsTheOthers()
    {
        var path = NewCsv(
            "item_id;slot;replace_flag;priority;max_count;icon",
            "0;0;0;0;0;65535",
            "1;1;1;0;not-a-number;31",
            "1;1;1;0;1;31");

        try
        {
            var result = ItemsPropertiesCatalogReader.Read(path);

            Assert.Single(result.Warnings);
            Assert.Equal(2, result.RowsByItemId.Count);
            Assert.Equal(new[] { 1, 1, 0, 1, 31 }, result.RowsByItemId[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ShortRow_WarnsAndSkipsIt()
    {
        var path = NewCsv(
            "item_id;slot;replace_flag;priority;max_count;icon",
            "0;0;0;0;0;65535",
            "1;1;1;0;1",
            "1;1;1;0;1;31");

        try
        {
            var result = ItemsPropertiesCatalogReader.Read(path);

            // Only the short row warns: the well formed row that follows it fills item 1, so the
            // table stays contiguous and the contiguity check stays silent.
            Assert.Single(result.Warnings);
            Assert.Equal(2, result.RowsByItemId.Count);
            Assert.Equal(new[] { 1, 1, 0, 1, 31 }, result.RowsByItemId[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NonContiguousItemIds_Warns()
    {
        // The rows are republished as an array indexed by item_id, so a hole renumbers everything
        // after it: the reader has to say so rather than hand back a silently shifted table.
        var path = NewCsv(
            "item_id;slot;replace_flag;priority;max_count;icon",
            "0;0;0;0;0;65535",
            "1;1;1;0;1;31",
            "3;1;1;2;1;33");

        try
        {
            var result = ItemsPropertiesCatalogReader.Read(path);

            var warning = Assert.Single(result.Warnings);
            Assert.Contains("contiguous", warning);
            Assert.Equal(3, result.RowsByItemId.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
