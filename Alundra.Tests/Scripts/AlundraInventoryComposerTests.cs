using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests.Scripts;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md): pins <see cref="AlundraInventoryComposer.Compose"/>'s own
/// position formulas by hand-computed values, against the original citations reproduced in the
/// composer's own doc (<c>MainInventoryManager.cs:1267-1615</c>).
/// </summary>
public sealed class AlundraInventoryComposerTests
{
    private static readonly (int X, int Y)[] RestBoxPositions =
    {
        (0x08, 0x10), // 0 weapon
        (0x08, 0x40), // 1 item
        (0xB0, 0x10), // 2 weapon name
        (0xB0, 0x40), // 3 item name
        (0xF0, 0x70), // 4 spacer
        (0xB0, 0x60), // 5 money/falcon/key
        (0x10, 0xA8), // 6 description
    };

    private static readonly int?[] EmptySlots = new int?[AlundraInventoryComposer.SlotCount];
    private static readonly Guid?[] NoIcons = new Guid?[AlundraInventoryComposer.SlotCount];

    private static InventoryDisplayModel ComposeAtRest(
        int selectedSlotId = 0,
        int cursorFrameDelay = 0,
        string weaponName = "",
        string itemName = "",
        string line0 = "",
        string line1 = "",
        int?[]? slotItemIds = null,
        Guid?[]? slotIconAssetIds = null,
        int? currentWeaponItemId = null,
        int? currentItemId = null,
        int herbOwnedCount = 0,
        int money = 0,
        int keyCount = 0,
        int falconTotal = 0)
    {
        return AlundraInventoryComposer.Compose(
            isDrawn: true,
            boxPositions: RestBoxPositions,
            selectedSlotId: selectedSlotId,
            cursorFrameDelay: cursorFrameDelay,
            equippedWeaponName: weaponName,
            equippedItemName: itemName,
            descriptionLine0: line0,
            descriptionLine1: line1,
            slotItemIds: slotItemIds ?? EmptySlots,
            slotIconAssetIds: slotIconAssetIds ?? NoIcons,
            currentWeaponItemId: currentWeaponItemId,
            currentItemId: currentItemId,
            herbOwnedCount: herbOwnedCount,
            money: money,
            keyCount: keyCount,
            falconTotal: falconTotal);
    }

    [Fact]
    public void NotDrawn_ReturnsInvisibleEmptyModel()
    {
        var model = AlundraInventoryComposer.Compose(
            isDrawn: false, boxPositions: RestBoxPositions, selectedSlotId: 0, cursorFrameDelay: 0,
            equippedWeaponName: "x", equippedItemName: "y",
            descriptionLine0: "d0", descriptionLine1: "d1",
            slotItemIds: EmptySlots, slotIconAssetIds: NoIcons,
            currentWeaponItemId: null, currentItemId: null, herbOwnedCount: 3, money: 10, keyCount: 1, falconTotal: 0);

        Assert.False(model.Visible);
        Assert.Empty(model.Boxes);
        Assert.Empty(model.SlotIcons);
    }

    [Fact]
    public void Boxes_AtRest_MatchDirectorPositions_SkippingSpacer()
    {
        var model = ComposeAtRest();

        Assert.Equal(6, model.Boxes.Count);
        Assert.DoesNotContain(model.Boxes, b => b.BoxIndex == 4);

        var weapon = model.Boxes.Single(b => b.BoxIndex == 0);
        Assert.Equal(0x08, weapon.NativeX);
        Assert.Equal(0x10, weapon.NativeY);

        var description = model.Boxes.Single(b => b.BoxIndex == 6);
        Assert.Equal(0x10, description.NativeX);
        Assert.Equal(0xA8, description.NativeY);
    }

    [Fact]
    public void Boxes_MidSlide_TrackTheDirectorsCurrentTweenedPosition()
    {
        var slidBoxes = (IReadOnlyList<(int X, int Y)>)new[]
        {
            (0x40, 0x10), (0x08, 0x40), (0xB0, 0x10), (0xB0, 0x40), (0xF0, 0x70), (0xB0, 0x60), (0x10, 0xA8),
        };

        var model = AlundraInventoryComposer.Compose(
            isDrawn: true, boxPositions: slidBoxes, selectedSlotId: 0, cursorFrameDelay: 0,
            equippedWeaponName: "", equippedItemName: "",
            descriptionLine0: "", descriptionLine1: "",
            slotItemIds: EmptySlots, slotIconAssetIds: NoIcons,
            currentWeaponItemId: null, currentItemId: null, herbOwnedCount: 0, money: 0, keyCount: 0, falconTotal: 0);

        var weapon = model.Boxes.Single(b => b.BoxIndex == 0);
        Assert.Equal(0x40, weapon.NativeX);

        // Everything anchored off the weapon box (icons/cursor/money) must follow ITS current position,
        // not the origin - here the herb digit (weaponBackground + offset[6] + (0x10,0x10)).
        var expectedHerbX = 0x40 + 0x08 + 0x10;
        Assert.Equal(expectedHerbX, model.HerbDigit.NativeX);
    }

    [Fact]
    public void SlotIcons_OnlyEmittedForSlotsWithAnAssetId_CentredInTheirCell()
    {
        var iconId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var slotIcons = (Guid?[])NoIcons.Clone();
        slotIcons[0] = iconId; // weapon row, slot 0 -> offset (0x08, 0x08)

        var model = ComposeAtRest(slotIconAssetIds: slotIcons);

        var icon = Assert.Single(model.SlotIcons);
        Assert.Equal(0, icon.SlotIndex);
        Assert.Equal(iconId, icon.Icon.AssetId);

        var cellX = 0x08 + 0x08; // weapon background X (0x08) + OffsetX[0] (0x08)
        var cellY = 0x10 + 0x08; // weapon background Y (0x10) + OffsetY[0] (0x08)
        Assert.Equal(cellX, icon.Icon.BoxNativeX);
        Assert.Equal(cellY, icon.Icon.BoxNativeY);
        Assert.Equal(24, icon.Icon.BoxNativeWidth);
        Assert.Equal(32, icon.Icon.BoxNativeHeight);
    }

    [Theory]
    [InlineData(24, 32, 0, 0)]  // exact cell size: no centring offset
    [InlineData(24, 31, 0, 0)]  // odd remainder on Y: floors to 0 at scale 1
    [InlineData(16, 15, 4, 8)]  // (24-16)/2=4, (32-15)/2=8 (integer division, scale 1)
    public void Icon_CentresInItsCell_AtVariousIconSizes(int iconWidth, int iconHeight, int expectedDx, int expectedDy)
    {
        var iconId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var slotIcons = (Guid?[])NoIcons.Clone();
        slotIcons[0] = iconId;

        var model = ComposeAtRest(slotIconAssetIds: slotIcons);
        var icon = Assert.Single(model.SlotIcons).Icon;

        var expectedLeft = icon.BoxNativeX + expectedDx;
        var expectedTop = icon.BoxNativeY + expectedDy;
        Assert.Equal(expectedLeft, icon.ScreenLeft(iconWidth, pixelScale: 1));
        Assert.Equal(expectedTop, icon.ScreenTop(iconHeight, pixelScale: 1));
    }

    [Fact]
    public void HerbDigit_VisibleOnlyWhenOwned_AtSlotSixCellPlusSixteenSixteen()
    {
        var hidden = ComposeAtRest(herbOwnedCount: 0);
        Assert.False(hidden.HerbDigitVisible);

        var shown = ComposeAtRest(herbOwnedCount: 23);
        Assert.True(shown.HerbDigitVisible);
        Assert.Equal(3, shown.HerbDigit.Value); // 23 % 10

        var expectedX = 0x08 + 0x08 + 0x10; // weapon.X + OffsetX[6] (0x08) + 0x10
        var expectedY = 0x10 + 0x38 + 0x10; // weapon.Y + OffsetY[6] (0x38) + 0x10
        Assert.Equal(expectedX, shown.HerbDigit.NativeX);
        Assert.Equal(expectedY, shown.HerbDigit.NativeY);
    }

    [Fact]
    public void MoneyDigits_FourDigitsMostSignificantFirst_EightPixelsApart()
    {
        var model = ComposeAtRest(money: 1234);

        Assert.Equal(4, model.MoneyDigits.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, model.MoneyDigits.Select(d => d.Value));

        var moneyBoxX = 0xB0;
        var moneyBoxY = 0x60;
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(moneyBoxX + 0x18 + i * 8, model.MoneyDigits[i].NativeX);
            Assert.Equal(moneyBoxY + 4, model.MoneyDigits[i].NativeY);
        }
    }

    [Fact]
    public void FalconAndKeyDigits_TwoDigitsEach_AtTheirOwnRows()
    {
        var model = ComposeAtRest(falconTotal: 7, keyCount: 42);

        Assert.Equal(new[] { 0, 7 }, model.FalconDigits.Select(d => d.Value));
        Assert.Equal(new[] { 4, 2 }, model.KeyDigits.Select(d => d.Value));

        var boxX = 0xB0;
        var boxY = 0x60;
        Assert.Equal(boxX + 0x28, model.FalconDigits[0].NativeX);
        Assert.Equal(boxY + 0x1c, model.FalconDigits[0].NativeY);
        Assert.Equal(boxX + 0x28 + 8, model.FalconDigits[1].NativeX);

        Assert.Equal(boxX + 0x28, model.KeyDigits[0].NativeX);
        Assert.Equal(boxY + 0x34, model.KeyDigits[0].NativeY);
    }

    /// <summary>Verifier's A2: the original sets the cursor's position (UpdateCursorSpritePosition,
    /// MainInventoryManager.cs:907-910) BEFORE DisplayInventoryCursor increments the counter and picks the
    /// sprite (:1593-1601). On the tick the counter enters a new phase, the sprite is already the new phase's
    /// and the position still the previous phase's.</summary>
    [Theory]
    [InlineData(20, 2, 0, 0)]   // enters phase 2: sprite 2, position phase 1 (0,0)
    [InlineData(30, 3, -1, 1)]  // enters phase 3: sprite 3, position phase 2 (-1,+1)
    [InlineData(0, 0, -1, 0)]   // wrapped to 0: sprite 0, position phase 3 (-1,0)
    [InlineData(10, 1, 0, 0)]   // enters phase 1: sprite 1, position phase 0 (0,0)
    public void Cursor_OnThePhaseChangeTick_SpriteIsTheNewPhase_PositionThePrevious(
        int frameDelay, int expectedSpritePhase, int expectedOffsetX, int expectedOffsetY)
    {
        var model = ComposeAtRest(selectedSlotId: 0, cursorFrameDelay: frameDelay);

        Assert.Equal(expectedSpritePhase, model.Cursor.Phase);
        Assert.Equal(0x08 + 0x08 + 0x12 + expectedOffsetX, model.Cursor.NativeX);
        Assert.Equal(0x10 + 0x08 - 8 + expectedOffsetY, model.Cursor.NativeY);

        // The base position is the same without the phase offset, whatever the phase (the UI animation
        // ui_inventory_cursor carries the offset itself).
        Assert.Equal(0x08 + 0x08 + 0x12, model.Cursor.BaseNativeX);
        Assert.Equal(0x10 + 0x08 - 8, model.Cursor.BaseNativeY);
    }

    [Fact]
    public void Cursor_PositionedByPhase_AtTheSelectedSlotsCell()
    {
        // Phase 0 (FrameDelay 1-9 after this tick's increment; the position uses the value before it,
        // 0-8, also phase 0): offset (0,0).
        var phase0 = ComposeAtRest(selectedSlotId: 0, cursorFrameDelay: 5);
        Assert.Equal(0, phase0.Cursor.Phase);
        Assert.Equal(0x08 + 0x08 + 0x12 + 0, phase0.Cursor.NativeX);
        Assert.Equal(0x10 + 0x08 - 8 + 0, phase0.Cursor.NativeY);

        // Phase 2 (FrameDelay 21-29, position from 20-28): offset (-1, +1).
        var phase2 = ComposeAtRest(selectedSlotId: 0, cursorFrameDelay: 25);
        Assert.Equal(2, phase2.Cursor.Phase);
        Assert.Equal(0x08 + 0x08 + 0x12 - 1, phase2.Cursor.NativeX);
        Assert.Equal(0x10 + 0x08 - 8 + 1, phase2.Cursor.NativeY);

        // A different selected slot (slot 7: row 1, col 1) moves the cursor with it.
        var otherSlot = ComposeAtRest(selectedSlotId: 7, cursorFrameDelay: 5);
        Assert.Equal(0x08 + 0x20 + 0x12, otherSlot.Cursor.NativeX);
        Assert.Equal(0x10 + 0x38 - 8, otherSlot.Cursor.NativeY);
    }

    [Fact]
    public void WeaponSelectionFrame_VisibleOnlyWhenAMatchingWeaponRowSlotIsFound()
    {
        var noWeapon = ComposeAtRest(currentWeaponItemId: null);
        Assert.False(noWeapon.WeaponSelectionFrame.Visible);

        var slotItemIds = (int?[])EmptySlots.Clone();
        slotItemIds[2] = 5; // weapon slot 2 -> offset (0x38, 0x08)

        var matched = ComposeAtRest(slotItemIds: slotItemIds, currentWeaponItemId: 5);
        Assert.True(matched.WeaponSelectionFrame.Visible);
        Assert.Equal(0x08 + 0x38, matched.WeaponSelectionFrame.NativeX);
        Assert.Equal(0x10 + 0x08, matched.WeaponSelectionFrame.NativeY);
    }

    [Fact]
    public void ItemSelectionFrame_OnlyScansRowsOneThroughThree_NeverTheWeaponRow()
    {
        var slotItemIds = (int?[])EmptySlots.Clone();
        slotItemIds[2] = 9; // weapon row - must never match the ITEM frame even with the same id

        var model = ComposeAtRest(slotItemIds: slotItemIds, currentItemId: 9);
        Assert.False(model.ItemSelectionFrame.Visible);
    }

    /// <summary>Which text the original drew on each line this tick is the director's to say
    /// (AlundraInventoryDirector.DrawnDescriptionLine0/1, pinned by AlundraInventoryDirectorTests); the
    /// composer shows exactly that, nothing more.</summary>
    [Fact]
    public void DescriptionLines_ShowExactlyWhatTheDirectorDrew()
    {
        var both = ComposeAtRest(line0: "Petit poignard.", line1: "Second line");
        Assert.Equal("Petit poignard.", both.DescriptionLine0);
        Assert.Equal("Second line", both.DescriptionLine1);

        var none = ComposeAtRest(line0: "", line1: "");
        Assert.Equal(string.Empty, none.DescriptionLine0);
        Assert.Equal(string.Empty, none.DescriptionLine1);
    }

    [Fact]
    public void Names_PositionedAtTheirOwnBoxesPlusSixteenEight()
    {
        var model = ComposeAtRest(weaponName: "Poignard", itemName: "Herbe");

        Assert.Equal("Poignard", model.WeaponName);
        Assert.Equal(0xB0 + 0x10, model.WeaponNameX);
        Assert.Equal(0x10 + 8, model.WeaponNameY);

        Assert.Equal("Herbe", model.ItemName);
        Assert.Equal(0xB0 + 0x10, model.ItemNameX);
        Assert.Equal(0x40 + 8, model.ItemNameY);
    }

    [Fact]
    public void DescriptionLines_PositionedAtTheDescriptionBoxPlusTheirOwnOffsets()
    {
        var model = ComposeAtRest();

        Assert.Equal(0x10 + 0x10, model.DescriptionLine0X);
        Assert.Equal(0xA8 + 0xc, model.DescriptionLine0Y);

        Assert.Equal(0x10 + 0x10, model.DescriptionLine1X);
        Assert.Equal(0xA8 + 0x1c, model.DescriptionLine1Y);
    }
}
