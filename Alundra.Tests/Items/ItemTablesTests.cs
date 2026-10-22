#nullable enable
using System;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Items;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Items;

/// <summary>
/// E13.c S3 (docs/plan-e13c-icones-hud.md): <see cref="ItemTables"/> reads the three <c>Data/</c>
/// item tables the converter republishes, and degrades to zeros - never an exception - when one is missing,
/// unreadable or short. Driven off fixture directories, the way <see cref="SoundGroupIndexTableTests"/>
/// drives its own table.
/// </summary>
public class ItemTablesTests : IDisposable
{
    public ItemTablesTests() => ItemTables.ResetForTests();

    public void Dispose() => ItemTables.ResetForTests();

    [Fact]
    public void LoadsAllThreeTables_IndexedLikeTheOriginal()
    {
        var tables = ItemTablesFixture.LoadReal();

        // g_itemsProperties[itemId * 5 + column]: item 1 is slot 1, flag 1, priority 0, max 1, icon 31.
        Assert.Equal(new ushort[] { 1, 1, 0, 1, 31 }, new[]
        {
            tables.ItemsProperties[5], tables.ItemsProperties[6], tables.ItemsProperties[7],
            tables.ItemsProperties[8], tables.ItemsProperties[9],
        });
        Assert.Equal(65535, tables.ItemsProperties[0 * 5 + 4]); // item 0 has no icon
        Assert.Equal(ItemTables.ItemRowCount * ItemTables.ItemColumnCount, tables.ItemsProperties.Count);

        // g_itemDropProperties[itemId].Field3 - the whole byte, unlock bit and low bits together.
        Assert.Equal(129, tables.DropField3[1]);
        Assert.Equal(130, tables.DropField3[17]);
        Assert.Equal(128, tables.DropField3[25]);
        Assert.Equal(ItemTables.DropRecordCount, tables.DropField3.Count);

        Assert.True(tables.TryGetIconAssetId(1, out var swordIcon));
        Assert.Equal(ItemTablesFixture.SwordIconAssetId, swordIcon);
        Assert.False(tables.TryGetIconAssetId(42, out _)); // no portrait in the game data
    }

    [Fact]
    public void MissingFiles_EveryEntryReadsAsZero_NoException()
    {
        var projectPath = ItemTablesFixture.Write(null, null, null);
        try
        {
            ItemTables tables = null!;
            Assert.Null(Record.Exception(() => tables = new ItemTables(projectPath)));

            Assert.All(tables.ItemsProperties, value => Assert.Equal(0, value));
            Assert.All(tables.DropField3, value => Assert.Equal(0, value));
            Assert.False(tables.TryGetIconAssetId(1, out _));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void UnparsableFiles_EveryEntryReadsAsZero_NoException()
    {
        var projectPath = ItemTablesFixture.Write(null, null, null);
        var dataPath = Path.Combine(projectPath, "Data");
        File.WriteAllText(Path.Combine(dataPath, "items-properties.json"), "not json");
        File.WriteAllText(Path.Combine(dataPath, "item-drop-properties.json"), "[[0,0,129");
        File.WriteAllText(Path.Combine(dataPath, "item-icon-index.json"), "{");
        try
        {
            ItemTables tables = null!;
            Assert.Null(Record.Exception(() => tables = new ItemTables(projectPath)));

            Assert.All(tables.ItemsProperties, value => Assert.Equal(0, value));
            Assert.All(tables.DropField3, value => Assert.Equal(0, value));
            Assert.False(tables.TryGetIconAssetId(1, out _));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void ShortTable_KeepsTheRowsItHas_AndReadsTheRestAsZero()
    {
        var properties = ItemTablesFixture.RealProperties()[..3]; // items 0, 1, 2 only
        var projectPath = ItemTablesFixture.Write(properties, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        try
        {
            var tables = new ItemTables(projectPath);

            Assert.Equal(31, tables.ItemsProperties[1 * 5 + 4]);
            Assert.Equal(32, tables.ItemsProperties[2 * 5 + 4]);
            Assert.Equal(0, tables.ItemsProperties[3 * 5 + 4]); // item 3 was cut off
            Assert.Equal(ItemTables.ItemRowCount * ItemTables.ItemColumnCount, tables.ItemsProperties.Count);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void GetOrCreate_ReadsAProjectOnce_AndKeepsTwoProjectsApart()
    {
        var first = ItemTablesFixture.Write(ItemTablesFixture.RealProperties(), ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        var second = ItemTablesFixture.Write(null, null, null);
        try
        {
            var a = ItemTables.GetOrCreate(first);

            Assert.Same(a, ItemTables.GetOrCreate(first));
            Assert.NotSame(a, ItemTables.GetOrCreate(second));
            Assert.True(a.TryGetIconAssetId(1, out _));
            Assert.False(ItemTables.GetOrCreate(second).TryGetIconAssetId(1, out _));
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    // ---- E20.e0c T1 (l3): the columns 0 and 1 of g_itemDropProperties --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheColumns0And1_AreTheOctets4And5OfTheObjectTableOfTheBinary_ForTheNinetyEightRowsOfTheRealExport()
    {
        var tables = new ItemTables(ProjectRootFinder.Find());
        using var document = AnnexFiles.ReadVector("item-table.json");
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(c => c.GetInt32()).ToArray()).ToList();
        Assert.Equal(ItemTables.DropRecordCount, rows.Count);
        Assert.Equal(ItemTables.DropRecordCount, tables.DropField1.Count);
        Assert.Equal(ItemTables.DropRecordCount, tables.DropSoundSfxIndex.Count);
        for (var item = 0; item < rows.Count; item++)
        {
            Assert.True(tables.DropField1[item] == rows[item][0], $"item {item}: column 0 {tables.DropField1[item]}, the binary's octet 4 {rows[item][0]}");
            Assert.True(tables.DropSoundSfxIndex[item] == rows[item][1], $"item {item}: column 1 {tables.DropSoundSfxIndex[item]}, the binary's octet 5 {rows[item][1]}");
            Assert.True(tables.DropField3[item] == rows[item][2], $"item {item}: column 2 is still read");
        }
    }

    [Fact]
    public void TheColumn0_IsNonZeroForTheFourteenObjectsThatVanish_AndTheColumn1HoldsThePickupSoundsOrZeroForTheKeyObjects()
    {
        var tables = new ItemTables(ProjectRootFinder.Find());
        var vanishing = new[] { 0x24, 0x25, 0x26, 0x27, 0x29, 0x45, 0x46, 0x47, 0x48, 0x51, 0x52, 0x54, 0x55, 0x56 };
        Assert.Equal(vanishing, Enumerable.Range(0, ItemTables.DropRecordCount).Where(i => tables.DropField1[i] != 0).ToArray());
        Assert.Equal(new byte[] { 0, 52, 53, 54 }, tables.DropSoundSfxIndex.Distinct().OrderBy(x => x).ToArray());
        Assert.Equal(53, tables.DropSoundSfxIndex[0x46]); // the gildas: 53
        Assert.Equal(54, tables.DropSoundSfxIndex[0x52]); // the big crystal: 54
        Assert.Equal(52, tables.DropSoundSfxIndex[0x55]); // the hearts: 52
        Assert.Equal(0, tables.DropSoundSfxIndex[0x04]);  // the sword is a key object.
    }

    [Fact]
    public void TheColumns0And1_AreReadFromAFixture_AndReadZeroWhenTheFileIsMissing()
    {
        var drops = ItemTablesFixture.RealDrops();
        drops[5] = new[] { 1, 52, 9, 7 };
        var projectPath = ItemTablesFixture.Write(ItemTablesFixture.RealProperties(), drops, null);
        var emptyPath = ItemTablesFixture.Write(null, null, null);
        try
        {
            var tables = new ItemTables(projectPath);
            Assert.Equal((1, 52, 9), (tables.DropField1[5], tables.DropSoundSfxIndex[5], tables.DropField3[5]));
            Assert.Equal((0, 0), (tables.DropField1[6], tables.DropSoundSfxIndex[6])); // an untouched row stays at zero.

            var degraded = new ItemTables(emptyPath);
            Assert.True(degraded.DropField1.All(b => b == 0));
            Assert.True(degraded.DropSoundSfxIndex.All(b => b == 0));
            Assert.Equal(ItemTables.DropRecordCount, degraded.DropField1.Count);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
            Directory.Delete(emptyPath, recursive: true);
        }
    }
}
