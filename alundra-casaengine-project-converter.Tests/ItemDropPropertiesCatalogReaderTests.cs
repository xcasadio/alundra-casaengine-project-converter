using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// S2.b of docs/plan-e13c-icones-hud.md, D-E13C-2: drives <see cref="ItemDropPropertiesCatalogReader.Read"/>
/// directly, off a small synthetic CSV rather than the real linked <c>ItemDropProperties.csv</c>, whose 98
/// rows the export's own acceptance compares cell by cell.
/// </summary>
public class ItemDropPropertiesCatalogReaderTests
{
    private static string NewCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "ItemDropPropertiesCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Read_ParsesEveryRow_SkippingTheHeaderAndBlankLines()
    {
        var path = NewCsv(
            "item_id;field1;sound_sfx_index;field3;field4",
            "0;0;0;0;0",
            string.Empty,
            "1;0;0;129;1",
            "2;0;0;1;1");

        try
        {
            var result = ItemDropPropertiesCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.RowsByItemId.Count);
            Assert.Equal(new[] { 0, 0, 0, 0 }, result.RowsByItemId[0]);
            Assert.Equal(new[] { 0, 0, 129, 1 }, result.RowsByItemId[1]);
            Assert.Equal(new[] { 0, 0, 1, 1 }, result.RowsByItemId[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_KeepsTheThirdFieldWhole()
    {
        // 129 is the unlock bit plus a low bit the game reads on its own. The reader must hand both over
        // untouched: deciding which one matters belongs to the consumer.
        var path = NewCsv(
            "item_id;field1;sound_sfx_index;field3;field4",
            "0;0;0;129;1");

        try
        {
            var result = ItemDropPropertiesCatalogReader.Read(path);

            Assert.Equal(129, result.RowsByItemId[0][2]);
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
            "item_id;field1;sound_sfx_index;field3;field4",
            "0;0;0;0;0",
            "1;0;0;not-a-number;1",
            "1;0;0;129;1");

        try
        {
            var result = ItemDropPropertiesCatalogReader.Read(path);

            Assert.Single(result.Warnings);
            Assert.Equal(2, result.RowsByItemId.Count);
            Assert.Equal(new[] { 0, 0, 129, 1 }, result.RowsByItemId[1]);
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
            "item_id;field1;sound_sfx_index;field3;field4",
            "0;0;0;0;0",
            "1;0;0;129",
            "1;0;0;129;1");

        try
        {
            var result = ItemDropPropertiesCatalogReader.Read(path);

            // Only the short row warns: the well formed row that follows it fills item 1, so the table
            // stays contiguous and the contiguity check stays silent.
            Assert.Single(result.Warnings);
            Assert.Equal(2, result.RowsByItemId.Count);
            Assert.Equal(new[] { 0, 0, 129, 1 }, result.RowsByItemId[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NonContiguousItemIds_Warns()
    {
        // The rows are republished as an array indexed by item_id, so a hole renumbers everything after
        // it: the reader has to say so rather than hand back a silently shifted table.
        var path = NewCsv(
            "item_id;field1;sound_sfx_index;field3;field4",
            "0;0;0;0;0",
            "1;0;0;129;1",
            "3;0;0;1;1");

        try
        {
            var result = ItemDropPropertiesCatalogReader.Read(path);

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
