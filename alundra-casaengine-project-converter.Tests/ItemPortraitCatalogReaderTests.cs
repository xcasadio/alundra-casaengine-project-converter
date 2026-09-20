using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// S2 of docs/plan-e13c-icones-hud.md, D-E13C-4: drives <see cref="ItemPortraitCatalogReader.Read"/>
/// directly, off a small synthetic CSV rather than the real linked <c>ItemPortrait.csv</c>, whose 88
/// rows the export's own acceptance resolves against the asset catalog.
/// </summary>
public class ItemPortraitCatalogReaderTests
{
    private static string NewCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "ItemPortraitCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Read_ParsesEveryRow_SkippingTheHeaderAndBlankLines()
    {
        var path = NewCsv(
            "item_id;icon;signature",
            "1;31;34187962752519",
            string.Empty,
            "2;32;34187958034183");

        try
        {
            var result = ItemPortraitCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.Equal(2, result.PortraitByItemId.Count);
            Assert.Equal(31, result.PortraitByItemId[1].Icon);
            Assert.Equal(34187962752519L, result.PortraitByItemId[1].Signature);
            Assert.Equal(32, result.PortraitByItemId[2].Icon);
            Assert.Equal(34187958034183L, result.PortraitByItemId[2].Signature);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_SignatureBeyondIntRange_RoundTripsThroughLong()
    {
        // Every real signature packs six bytes, so all 88 of them overflow int: parsing the column as
        // an int would reject the entire table.
        var path = NewCsv(
            "item_id;icon;signature",
            "89;119;35290681974279");

        try
        {
            var result = ItemPortraitCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.True(35290681974279L > int.MaxValue);
            Assert.Equal(35290681974279L, result.PortraitByItemId[89].Signature);
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
            "item_id;icon;signature",
            "1;31;34187962752519",
            "2;32;not-a-number",
            "3;33;35287989755655");

        try
        {
            var result = ItemPortraitCatalogReader.Read(path);

            Assert.Single(result.Warnings);
            Assert.Equal(2, result.PortraitByItemId.Count);
            Assert.False(result.PortraitByItemId.ContainsKey(2));
            Assert.Equal(35287989755655L, result.PortraitByItemId[3].Signature);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_KeepsAGapInItemIds()
    {
        // Item 42 is absent from the real table on purpose: the game has no portrait record for it.
        var path = NewCsv(
            "item_id;icon;signature",
            "41;71;26494587380231",
            "43;73;26494591048711");

        try
        {
            var result = ItemPortraitCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.False(result.PortraitByItemId.ContainsKey(42));
            Assert.Equal(2, result.PortraitByItemId.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
