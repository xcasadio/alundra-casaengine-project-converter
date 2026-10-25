#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.Tests.Items;

/// <summary>
/// E13.c S3 (docs/plan-e13c-icones-hud.md): the item counters, the equipped weapon and the HUD's two
/// equipment boxes, ported line by line into <see cref="PlayerManager"/> - the plan's own acceptance:
/// the setter rule, the chain weapon to item to icon at the New Game (1 to 1 to 31), the no-weapon sentinel,
/// the empty accessory box.
/// </summary>
public class ItemInventoryTests
{
    private static GameState NewGameState(ItemTables tables)
    {
        var state = new GameState();
        PlayerManager.InitializeNewGameInventory(state, tables);
        return state;
    }

    // -------------------------------------------------------------------------------------------------
    // The New Game (GameInitializer.cs:395-411).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void NewGame_UnlocksExactlyItems1_17_And25_OnceEach()
    {
        var state = NewGameState(ItemTablesFixture.LoadReal());

        for (var itemId = 0; itemId < PlayerManager.ItemsCount; itemId++)
        {
            var expected = itemId is 1 or 17 or 25 ? 1 : 0;
            Assert.Equal(expected, PlayerManager.GetNumberOfItem(state, itemId));
        }
    }

    [Fact]
    public void NewGame_EquipsWeaponSlot1_WhichResolvesToItem1_WhoseIconIs31_TheSwordSprite()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);

        // The plan's own chain, link by link.
        Assert.Equal(1, state.PlayerStats.WeaponId);
        var itemId = PlayerManager.GetItemIdFromCurrentWeapon(state, tables);
        Assert.Equal(1u, itemId);
        Assert.Equal(31, PlayerManager.GetItemTextureIdByItemId(tables, (int)itemId));
        Assert.True(tables.TryGetIconAssetId((int)itemId, out var assetId));
        Assert.Equal(ItemTablesFixture.SwordIconAssetId, assetId);

        // And what the HUD is handed for it.
        var (weapon, _) = PlayerManager.ResolveHudEquipmentIcons(state, tables);
        Assert.Equal(ItemTablesFixture.SwordIconAssetId, weapon);
    }

    [Fact]
    public void NewGame_LeavesTheAccessoryBoxEmpty_AndTheAccessoryIdUntouched()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);

        // ItemId is 0 and item 0 is not owned: the original's own sentinel, no write (D-E13C-7).
        Assert.Equal(PlayerManager.NoItem, PlayerManager.SetItemIdFromCurrentItemId(state, tables));
        Assert.Equal(0, state.PlayerStats.ItemId);

        var (_, accessory) = PlayerManager.ResolveHudEquipmentIcons(state, tables);
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
            var tables = new ItemTables(projectPath);
            var state = NewGameState(tables);

            Assert.Equal(1, state.PlayerStats.WeaponId);
            Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(state, tables));
            var (weapon, accessory) = PlayerManager.ResolveHudEquipmentIcons(state, tables);
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
        var state = new GameState();

        PlayerManager.SetPlayerWeaponId(state, tables, (ushort)weaponId);

        Assert.Equal((short)weaponId, state.PlayerStats.WeaponId);
    }

    [Fact]
    public void SetPlayerWeaponId_RejectsZero_AsTheExecutableDoes_OpenPoint7()
    {
        // docs/plan-e13c-icones-hud.md, open point 7, settled by ALUN_CD.EXE 0x8004e49c (plan E13.d SI9.b):
        // (0 - 1) <u 6 fails, so 0 is rejected and the current slot is kept.
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();
        state.PlayerStats.WeaponId = 3;

        PlayerManager.SetPlayerWeaponId(state, tables, 0);

        Assert.Equal(3, state.PlayerStats.WeaponId);
    }

    [Fact]
    public void SetPlayerWeaponId_StoresMinusOne_TheNoWeaponValue()
    {
        // ALUN_CD.EXE 0x8004e490: -1 is stored before the range test (the decompilation's ushort never saw it).
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();
        state.PlayerStats.WeaponId = 3;

        PlayerManager.SetPlayerWeaponId(state, tables, -1);

        Assert.Equal(-1, state.PlayerStats.WeaponId);
        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(state, tables));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(100)]
    [InlineData(ushort.MaxValue)]
    public void SetPlayerWeaponId_RejectsAnythingAbove6_AndKeepsTheCurrentSlot(int weaponId)
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();
        state.PlayerStats.WeaponId = 3;

        PlayerManager.SetPlayerWeaponId(state, tables, (ushort)weaponId);

        Assert.Equal(3, state.PlayerStats.WeaponId);
    }

    // -------------------------------------------------------------------------------------------------
    // GetItemIdFromSlotId (PlayerManager.cs:4369-4419) and the sentinel.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void NoWeaponOwned_ResolvesToTheSentinel_AndTheWeaponBoxStaysEmpty()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState(); // nothing owned, no New Game run
        state.PlayerStats.WeaponId = 1;

        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(state, tables));
        Assert.Null(PlayerManager.ResolveHudEquipmentIcons(state, tables).Weapon);
    }

    [Fact]
    public void WeaponIdZero_IsNoSlot_AndResolvesToTheSentinel()
    {
        // WeaponId - 1 = -1 falls outside GetWeaponIdBySlotId's six cases.
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        state.PlayerStats.WeaponId = 0;

        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(state, tables));
    }

    [Fact]
    public void GetItemIdFromSlotId_AmongOwnedItemsWithTheReplacementFlag_KeepsTheStrictlyHigherPriority()
    {
        // Items 1..4 share slot 1 with priorities 0..3 and the replacement flag set: owning 1, 3 and 4 must
        // give 4, the highest priority, and owning 1 and 3 must give 3.
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[3 * 2 + 1] = 1;

        Assert.Equal(3u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));

        state.NumberOfItems[4 * 2 + 1] = 1;
        Assert.Equal(4u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));
    }

    [Fact]
    public void GetItemIdFromSlotId_FirstOwnedMatchWithoutTheReplacementFlag_WinsAtOnce()
    {
        var properties = ItemTablesFixture.RealProperties();
        properties[2] = new[] { 1, 0, 0, 1, 32 }; // item 2 of slot 1 without the flag, and the lowest priority
        var projectPath = ItemTablesFixture.Write(properties, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        try
        {
            var tables = new ItemTables(projectPath);
            var state = new GameState();
            state.NumberOfItems[2 * 2 + 1] = 1;
            state.NumberOfItems[4 * 2 + 1] = 1; // higher priority, never looked at

            Assert.Equal(2u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));
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
            var tables = new ItemTables(projectPath);
            var state = new GameState();
            state.NumberOfItems[1 * 2 + 1] = 1;
            state.NumberOfItems[3 * 2 + 1] = 1;

            Assert.Equal(1u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));
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

        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromSlotId(new GameState(), tables, 0x20));
    }

    // -------------------------------------------------------------------------------------------------
    // The counters (PlayerManager.cs:4330-4346, :4666-4688) and the accessory (:4350-4366).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AddOneItemIfUnlocked_StopsAtTheMaxCount_AndThenReturnsTheCount()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();

        Assert.Equal(1, PlayerManager.AddOneItemIfUnlocked(state, tables, 17)); // max count 1
        // The binary (0x8004E58C/0x8004E5B0) returns the count at the maximum; the decompilation returned the item id (17).
        Assert.Equal(1, PlayerManager.AddOneItemIfUnlocked(state, tables, 17));
        Assert.Equal(1, PlayerManager.GetNumberOfItem(state, 17));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void GetNumberOfItem_OutOfRange_IsZero(int itemId)
    {
        Assert.Equal(0, PlayerManager.GetNumberOfItem(new GameState(), itemId));
    }

    [Fact]
    public void SetItemIdFromCurrentItemId_WhenTheAccessoryIsOwned_ReturnsTheOldId_AndReEquipsTheBestOfItsSlot()
    {
        // Item 1 is owned, and item 4 - same slot, higher priority - too: the call returns the id it started
        // from (1) and, as a side effect, equips 4, exactly as the original does.
        var tables = ItemTablesFixture.LoadReal();
        var state = new GameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[4 * 2 + 1] = 1;
        state.PlayerStats.ItemId = 1;

        Assert.Equal(1u, PlayerManager.SetItemIdFromCurrentItemId(state, tables));
        Assert.Equal(4, state.PlayerStats.ItemId);
    }

    [Fact]
    public void ResolveHudEquipmentIcons_AnOwnedAccessoryWithAPortrait_FillsTheSecondBox()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        state.PlayerStats.ItemId = 17; // owned by the New Game, and given an icon by the fixture

        var (weapon, accessory) = PlayerManager.ResolveHudEquipmentIcons(state, tables);

        Assert.Equal(ItemTablesFixture.SwordIconAssetId, weapon);
        Assert.Equal(ItemTablesFixture.Item17IconAssetId, accessory);
    }

    // -------------------------------------------------------------------------------------------------
    // E20.b2 T1 (docs/plan-e20-heros.md section 1.4.1): the Legendary Sword (item 8, rank 4 of the slot 1 whose items 1, 2, 3, 4 and 8 have the ranks 0 to 4).
    // -------------------------------------------------------------------------------------------------

    /// <summary>The item tables of the exported project (all the items, the item 8 included): the fixture above holds only the items of the new game and the sword chain 1 to 4.</summary>
    private static ItemTables ExportedTables() => ItemTables.GetOrCreate(Alundra.Tests.Support.ProjectRootFinder.Find());

    [Fact]
    public void GetItemIdFromSlotId_OwningTheItems1_4_And8_GivesTheLegendarySword()
    {
        var tables = ExportedTables();
        var state = new GameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[4 * 2 + 1] = 1;
        state.NumberOfItems[8 * 2 + 1] = 1;

        Assert.Equal(8u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));
    }

    [Fact]
    public void GetItemIdFromSlotId_OwningTheItems1And8_GivesTheLegendarySword_AndTheItem8AloneToo()
    {
        var tables = ExportedTables();
        var state = new GameState();
        state.NumberOfItems[1 * 2 + 1] = 1;
        state.NumberOfItems[8 * 2 + 1] = 1;
        Assert.Equal(8u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));

        var alone = new GameState();
        alone.NumberOfItems[8 * 2 + 1] = 1;
        Assert.Equal(8u, PlayerManager.GetItemIdFromSlotId(alone, tables, 1));
    }

    [Fact]
    public void GetItemIdFromSlotId_TheLegendarySword_BeatsEverySwordOfTheSlot()
    {
        var tables = ExportedTables();
        var state = new GameState();
        foreach (var item in new[] { 1, 2, 3, 4, 8 })
        {
            state.NumberOfItems[item * 2 + 1] = 1;
        }

        Assert.Equal(8u, PlayerManager.GetItemIdFromSlotId(state, tables, 1));
    }

    [Fact]
    public void TheEquippedSlot1_OfAHeroWhoOwnsTheItem8_ResolvesToTheItem8()
    {
        var tables = ExportedTables();
        var state = NewGameState(tables);
        state.NumberOfItems[8 * 2 + 1] = 1;

        PlayerManager.SetPlayerWeaponId(state, tables, 1);

        Assert.Equal(8u, PlayerManager.GetItemIdFromCurrentWeapon(state, tables));
    }
}
