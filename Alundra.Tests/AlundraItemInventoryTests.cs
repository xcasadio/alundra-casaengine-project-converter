#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Writes a temporary project directory holding the three <c>Data/</c> item tables, the way
/// <see cref="AlundraSoundGroupIndexTableTests"/> writes its own fixture. The default rows are the real ones
/// (alundra-datas-analyser ItemsProperties.csv and ItemDropProperties.csv) for every item the New Game
/// touches: item 0, the four slot-1 weapons 1..4, and the two other items it unlocks, 17 and 25. Every other
/// row is zero, which the loader and the ported code read as "no such item".
/// </summary>
internal static class ItemTablesFixture
{
    /// <summary>Item 1's real portrait asset id, from the exported <c>Data/item-icon-index.json</c>.</summary>
    public static readonly Guid SwordIconAssetId = Guid.Parse("aeebd7a0-faa7-57b0-a844-34470272a4eb");

    /// <summary>Arbitrary, distinct from the sword's - an icon for item 17, which is not a weapon.</summary>
    public static readonly Guid Item17IconAssetId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static int[][] RealProperties()
    {
        var rows = NewRows(AlundraItemTables.ItemRowCount, AlundraItemTables.ItemColumnCount);
        rows[0] = new[] { 0, 0, 0, 0, 65535 };
        rows[1] = new[] { 1, 1, 0, 1, 31 };
        rows[2] = new[] { 1, 1, 1, 1, 32 };
        rows[3] = new[] { 1, 1, 2, 1, 33 };
        rows[4] = new[] { 1, 1, 3, 1, 34 };
        rows[17] = new[] { 7, 1, 0, 1, 47 };
        rows[25] = new[] { 9, 1, 0, 1, 55 };
        return rows;
    }

    public static int[][] RealDrops()
    {
        var rows = NewRows(AlundraItemTables.DropRecordCount, 4);
        rows[1] = new[] { 0, 0, 129, 1 };
        rows[17] = new[] { 0, 0, 130, 1 };
        rows[25] = new[] { 0, 0, 128, 1 };
        return rows;
    }

    public static Dictionary<int, Guid> RealIcons() => new()
    {
        [1] = SwordIconAssetId,
        [17] = Item17IconAssetId,
    };

    /// <summary>Writes the three files (a null table is simply not written) and returns the project path.
    /// The caller deletes it.</summary>
    public static string Write(int[][]? properties, int[][]? drops, Dictionary<int, Guid>? icons)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraItemTablesFixture_" + Guid.NewGuid());
        var dataPath = Path.Combine(projectPath, "Data");
        Directory.CreateDirectory(dataPath);

        if (properties != null)
        {
            File.WriteAllText(Path.Combine(dataPath, "items-properties.json"), ToJson(properties));
        }

        if (drops != null)
        {
            File.WriteAllText(Path.Combine(dataPath, "item-drop-properties.json"), ToJson(drops));
        }

        if (icons != null)
        {
            var entries = new List<string>();
            foreach (var (itemId, assetId) in icons)
            {
                entries.Add($"\"{itemId}\": \"{assetId}\"");
            }

            File.WriteAllText(Path.Combine(dataPath, "item-icon-index.json"), "{" + string.Join(",", entries) + "}");
        }

        return projectPath;
    }

    /// <summary>The real tables, loaded through <see cref="AlundraItemTables"/> itself.</summary>
    public static AlundraItemTables LoadReal()
    {
        var projectPath = Write(RealProperties(), RealDrops(), RealIcons());
        try
        {
            return new AlundraItemTables(projectPath);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    private static int[][] NewRows(int rowCount, int columnCount)
    {
        var rows = new int[rowCount][];
        for (var i = 0; i < rowCount; i++)
        {
            rows[i] = new int[columnCount];
        }

        return rows;
    }

    private static string ToJson(int[][] rows)
    {
        var lines = new List<string>();
        foreach (var row in rows)
        {
            lines.Add("[" + string.Join(",", row) + "]");
        }

        return "[" + string.Join(",", lines) + "]";
    }
}

/// <summary>
/// E13.c S3 (docs/plan-e13c-icones-hud.md): the item counters, the equipped weapon and the HUD's two
/// equipment boxes, ported line by line into <see cref="AlundraPlayerManager"/> - the plan's own acceptance:
/// the setter rule, the chain weapon to item to icon at the New Game (1 to 1 to 31), the no-weapon sentinel,
/// the empty accessory box.
/// </summary>
public class AlundraItemInventoryTests
{
    private static AlundraGameState NewGameState(AlundraItemTables tables)
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameInventory(state, tables);
        return state;
    }

    // -------------------------------------------------------------------------------------------------
    // The New Game (GameInitializer.cs:395-411).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void NewGame_UnlocksExactlyItems1_17_And25_OnceEach()
    {
        var state = NewGameState(ItemTablesFixture.LoadReal());

        for (var itemId = 0; itemId < AlundraPlayerManager.ItemsCount; itemId++)
        {
            var expected = itemId is 1 or 17 or 25 ? 1 : 0;
            Assert.Equal(expected, AlundraPlayerManager.GetNumberOfItem(state, itemId));
        }
    }

    [Fact]
    public void NewGame_EquipsWeaponSlot1_WhichResolvesToItem1_WhoseIconIs31_TheSwordSprite()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);

        // The plan's own chain, link by link.
        Assert.Equal(1, state.PlayerStats.WeaponId);
        var itemId = AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, tables);
        Assert.Equal(1u, itemId);
        Assert.Equal(31, AlundraPlayerManager.GetItemTextureIdByItemId(tables, (int)itemId));
        Assert.True(tables.TryGetIconAssetId((int)itemId, out var assetId));
        Assert.Equal(ItemTablesFixture.SwordIconAssetId, assetId);

        // And what the HUD is handed for it.
        var (weapon, _) = AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables);
        Assert.Equal(ItemTablesFixture.SwordIconAssetId, weapon);
    }

    [Fact]
    public void NewGame_LeavesTheAccessoryBoxEmpty_AndTheAccessoryIdUntouched()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);

        // ItemId is 0 and item 0 is not owned: the original's own sentinel, no write (D-E13C-7).
        Assert.Equal(AlundraPlayerManager.NoItem, AlundraPlayerManager.SetItemIdFromCurrentItemId(state, tables));
        Assert.Equal(0, state.PlayerStats.ItemId);

        var (_, accessory) = AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables);
        Assert.Null(accessory);
        Assert.Equal(0, state.PlayerStats.ItemId);
    }

    [Fact]
    public void NewGame_OverDegradedTables_EquipsSlot1_ButShowsNoWeapon()
    {
        // A broken export: every table reads as zero. The New Game still sets the slot, the original's own
        // unconditional SetPlayerWeaponId(1), but nothing is owned, so the weapon resolves to the sentinel.
        var projectPath = ItemTablesFixture.Write(null, null, null);
        try
        {
            var tables = new AlundraItemTables(projectPath);
            var state = NewGameState(tables);

            Assert.Equal(1, state.PlayerStats.WeaponId);
            Assert.Equal(AlundraPlayerManager.NoItem, AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, tables));
            var (weapon, accessory) = AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables);
            Assert.Null(weapon);
            Assert.Null(accessory);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    // -------------------------------------------------------------------------------------------------
    // SetPlayerWeaponId's rule (PlayerManager.cs:1832-1845).
    // -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void SetPlayerWeaponId_KeepsSlots1To6(int weaponId)
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();

        AlundraPlayerManager.SetPlayerWeaponId(state, tables, (ushort)weaponId);

        Assert.Equal((short)weaponId, state.PlayerStats.WeaponId);
    }

    [Fact]
    public void SetPlayerWeaponId_KeepsZero_AsTheCitedCodeDoes_OpenPoint7()
    {
        // docs/plan-e13c-icones-hud.md, open point 7: evaluated in int, 0 - 1 < 6 holds. The PSX most likely
        // rejects 0; if Ghidra settles it that way, this test is the one to flip.
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();
        state.PlayerStats.WeaponId = 3;

        AlundraPlayerManager.SetPlayerWeaponId(state, tables, 0);

        Assert.Equal(0, state.PlayerStats.WeaponId);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(100)]
    [InlineData(ushort.MaxValue)]
    public void SetPlayerWeaponId_RejectsAnythingAbove6_AndKeepsTheCurrentSlot(int weaponId)
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();
        state.PlayerStats.WeaponId = 3;

        AlundraPlayerManager.SetPlayerWeaponId(state, tables, (ushort)weaponId);

        Assert.Equal(3, state.PlayerStats.WeaponId);
    }

    // -------------------------------------------------------------------------------------------------
    // GetItemIdFromSlotId (PlayerManager.cs:4369-4419) and the sentinel.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void NoWeaponOwned_ResolvesToTheSentinel_AndTheWeaponBoxStaysEmpty()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState(); // nothing owned, no New Game run
        state.PlayerStats.WeaponId = 1;

        Assert.Equal(AlundraPlayerManager.NoItem, AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, tables));
        Assert.Null(AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables).Weapon);
    }

    [Fact]
    public void WeaponIdZero_IsNoSlot_AndResolvesToTheSentinel()
    {
        // WeaponId - 1 = -1 falls outside GetWeaponIdBySlotId's six cases.
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        state.PlayerStats.WeaponId = 0;

        Assert.Equal(AlundraPlayerManager.NoItem, AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, tables));
    }

    [Fact]
    public void GetItemIdFromSlotId_AmongOwnedItemsWithTheReplacementFlag_KeepsTheStrictlyHigherPriority()
    {
        // Items 1..4 share slot 1 with priorities 0..3 and the replacement flag set: owning 1, 3 and 4 must
        // give 4, the highest priority, and owning 1 and 3 must give 3.
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[3 * 2 + 1] = 1;

        Assert.Equal(3u, AlundraPlayerManager.GetItemIdFromSlotId(state, tables, 1));

        state.NumberOfItems[4 * 2 + 1] = 1;
        Assert.Equal(4u, AlundraPlayerManager.GetItemIdFromSlotId(state, tables, 1));
    }

    [Fact]
    public void GetItemIdFromSlotId_FirstOwnedMatchWithoutTheReplacementFlag_WinsAtOnce()
    {
        var properties = ItemTablesFixture.RealProperties();
        properties[2] = new[] { 1, 0, 0, 1, 32 }; // item 2 of slot 1 without the flag, and the lowest priority
        var projectPath = ItemTablesFixture.Write(properties, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        try
        {
            var tables = new AlundraItemTables(projectPath);
            var state = new AlundraGameState();
            state.NumberOfItems[2 * 2 + 1] = 1;
            state.NumberOfItems[4 * 2 + 1] = 1; // higher priority, never looked at

            Assert.Equal(2u, AlundraPlayerManager.GetItemIdFromSlotId(state, tables, 1));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void GetItemIdFromSlotId_EqualPriority_KeepsTheFirst()
    {
        var properties = ItemTablesFixture.RealProperties();
        properties[3] = new[] { 1, 1, 0, 1, 33 }; // item 3 now ties item 1 at priority 0
        var projectPath = ItemTablesFixture.Write(properties, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        try
        {
            var tables = new AlundraItemTables(projectPath);
            var state = new AlundraGameState();
            state.NumberOfItems[1 * 2 + 1] = 1;
            state.NumberOfItems[3 * 2 + 1] = 1;

            Assert.Equal(1u, AlundraPlayerManager.GetItemIdFromSlotId(state, tables, 1));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void GetItemIdFromSlotId_SlotAt0x20OrAbove_IsRejected()
    {
        var tables = ItemTablesFixture.LoadReal();

        Assert.Equal(AlundraPlayerManager.NoItem, AlundraPlayerManager.GetItemIdFromSlotId(new AlundraGameState(), tables, 0x20));
    }

    // -------------------------------------------------------------------------------------------------
    // The counters (PlayerManager.cs:4330-4346, :4666-4688) and the accessory (:4350-4366).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AddOneItemIfUnlocked_StopsAtTheMaxCount_AndThenReturnsTheItemId()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();

        Assert.Equal(1, AlundraPlayerManager.AddOneItemIfUnlocked(state, tables, 17)); // max count 1
        Assert.Equal(17, AlundraPlayerManager.AddOneItemIfUnlocked(state, tables, 17)); // the original's own quirk
        Assert.Equal(1, AlundraPlayerManager.GetNumberOfItem(state, 17));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void GetNumberOfItem_OutOfRange_IsZero(int itemId)
    {
        Assert.Equal(0, AlundraPlayerManager.GetNumberOfItem(new AlundraGameState(), itemId));
    }

    [Fact]
    public void SetItemIdFromCurrentItemId_WhenTheAccessoryIsOwned_ReturnsTheOldId_AndReEquipsTheBestOfItsSlot()
    {
        // Item 1 is owned, and item 4 - same slot, higher priority - too: the call returns the id it started
        // from (1) and, as a side effect, equips 4, exactly as the original does.
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[4 * 2 + 1] = 1;
        state.PlayerStats.ItemId = 1;

        Assert.Equal(1u, AlundraPlayerManager.SetItemIdFromCurrentItemId(state, tables));
        Assert.Equal(4, state.PlayerStats.ItemId);
    }

    [Fact]
    public void ResolveHudEquipmentIcons_AnOwnedAccessoryWithAPortrait_FillsTheSecondBox()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        state.PlayerStats.ItemId = 17; // owned by the New Game, and given an icon by the fixture

        var (weapon, accessory) = AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables);

        Assert.Equal(ItemTablesFixture.SwordIconAssetId, weapon);
        Assert.Equal(ItemTablesFixture.Item17IconAssetId, accessory);
    }
}
