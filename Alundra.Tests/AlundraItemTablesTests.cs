#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.c S3 (docs/plan-e13c-icones-hud.md): <see cref="AlundraItemTables"/> reads the three <c>Data/</c>
/// item tables the converter republishes, and degrades to zeros - never an exception - when one is missing,
/// unreadable or short. Driven off fixture directories, the way <see cref="AlundraSoundGroupIndexTableTests"/>
/// drives its own table.
/// </summary>
public class AlundraItemTablesTests : IDisposable
{
    public AlundraItemTablesTests() => AlundraItemTables.ResetForTests();

    public void Dispose() => AlundraItemTables.ResetForTests();

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
        Assert.Equal(AlundraItemTables.ItemRowCount * AlundraItemTables.ItemColumnCount, tables.ItemsProperties.Count);

        // g_itemDropProperties[itemId].Field3 - the whole byte, unlock bit and low bits together.
        Assert.Equal(129, tables.DropField3[1]);
        Assert.Equal(130, tables.DropField3[17]);
        Assert.Equal(128, tables.DropField3[25]);
        Assert.Equal(AlundraItemTables.DropRecordCount, tables.DropField3.Count);

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
            AlundraItemTables tables = null!;
            Assert.Null(Record.Exception(() => tables = new AlundraItemTables(projectPath)));

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
            AlundraItemTables tables = null!;
            Assert.Null(Record.Exception(() => tables = new AlundraItemTables(projectPath)));

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
            var tables = new AlundraItemTables(projectPath);

            Assert.Equal(31, tables.ItemsProperties[1 * 5 + 4]);
            Assert.Equal(32, tables.ItemsProperties[2 * 5 + 4]);
            Assert.Equal(0, tables.ItemsProperties[3 * 5 + 4]); // item 3 was cut off
            Assert.Equal(AlundraItemTables.ItemRowCount * AlundraItemTables.ItemColumnCount, tables.ItemsProperties.Count);
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
            var a = AlundraItemTables.GetOrCreate(first);

            Assert.Same(a, AlundraItemTables.GetOrCreate(first));
            Assert.NotSame(a, AlundraItemTables.GetOrCreate(second));
            Assert.True(a.TryGetIconAssetId(1, out _));
            Assert.False(AlundraItemTables.GetOrCreate(second).TryGetIconAssetId(1, out _));
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }
}
