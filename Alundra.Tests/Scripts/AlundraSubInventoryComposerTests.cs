#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests.Scripts;

/// <summary>
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md): pins <see cref="AlundraSubInventoryComposer.Compose"/>'s own
/// position formulas by hand-computed values, against the plan's own §1.3/§1.5 tables - same shape as
/// <see cref="AlundraInventoryComposerTests"/>.
/// </summary>
public sealed class AlundraSubInventoryComposerTests
{
    // Rest positions (plan §1.3's own table), PUBLIC index order: 0 armory, 1 key items, 2 armor name,
    // 3 boots name, 4 icons, 5 description, 6 money/falcon/key.
    private static readonly (int X, int Y)[] RestBoxPositions =
    {
        (0x08, 0x10), // armory
        (0x08, 0x80), // key items
        (0xB0, 0x10), // armor name
        (0xB0, 0x40), // boots name
        (0x88, 0x10), // icons
        (0x10, 0xA8), // description
        (0xB0, 0x60), // money/falcon/key
    };

    private static readonly Guid?[] NoArmoryIcons = new Guid?[7];
    private static readonly Guid?[] NoKeyItemIcons = new Guid?[5];

    private static SubInventoryDisplayModel ComposeAtRest(
        Guid?[]? armoryIconAssetIds = null,
        Guid?[]? keyItemIconAssetIds = null,
        Guid? armorIconAssetId = null,
        Guid? bootsIconAssetId = null,
        string? armorName = null,
        string? bootsName = null,
        string line0 = "",
        string line1 = "",
        (int BoxIndex, int OffsetX, int OffsetY)? cursorReference = null,
        int money = 0,
        int keyCount = 0,
        int falconTotal = 0)
    {
        return AlundraSubInventoryComposer.Compose(
            isDrawn: true,
            boxPositions: RestBoxPositions,
            armoryIconAssetIds: armoryIconAssetIds ?? NoArmoryIcons,
            keyItemIconAssetIds: keyItemIconAssetIds ?? NoKeyItemIcons,
            armorIconAssetId: armorIconAssetId,
            bootsIconAssetId: bootsIconAssetId,
            armorName: armorName,
            bootsName: bootsName,
            descriptionLine0: line0,
            descriptionLine1: line1,
            cursorReference: cursorReference ?? (0, 0x42, -4),
            money: money,
            keyCount: keyCount,
            falconTotal: falconTotal);
    }

    [Fact]
    public void NotDrawn_ReturnsInvisibleEmptyModel()
    {
        var model = AlundraSubInventoryComposer.Compose(
            isDrawn: false, boxPositions: RestBoxPositions,
            armoryIconAssetIds: NoArmoryIcons, keyItemIconAssetIds: NoKeyItemIcons,
            armorIconAssetId: null, bootsIconAssetId: null,
            armorName: "x", bootsName: "y", descriptionLine0: "d0", descriptionLine1: "d1",
            cursorReference: (0, 0, 0), money: 10, keyCount: 1, falconTotal: 0);

        Assert.False(model.Visible);
        Assert.Empty(model.Boxes);
        Assert.Empty(model.ArmoryIcons);
        Assert.Empty(model.KeyItemIcons);
        Assert.Null(model.Armor);
        Assert.Null(model.Boots);
    }

    [Fact]
    public void NewGameAtRest_ArmorAndBootsNamesAndFrames_AtThePlanPlaces()
    {
        var armorIcon = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var bootsIcon = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var model = ComposeAtRest(
            armorIconAssetId: armorIcon, bootsIconAssetId: bootsIcon,
            armorName: "Armure en tissu", bootsName: "Bottes courtes");

        Assert.Equal("Armure en tissu", model.ArmorName);
        Assert.Equal(192, model.ArmorNameX); // 0xB0 + 0x10
        Assert.Equal(24, model.ArmorNameY);  // 0x10 + 8

        Assert.Equal("Bottes courtes", model.BootsName);
        Assert.Equal(192, model.BootsNameX); // 0xB0 + 0x10
        Assert.Equal(72, model.BootsNameY);  // 0x40 + 8

        Assert.NotNull(model.Armor);
        Assert.Equal(144, model.Armor!.Value.FrameNativeX); // icons.X (0x88) + 8
        Assert.Equal(40, model.Armor.Value.FrameNativeY);   // icons.Y (0x10) + 0*0x28 + 0x18
        Assert.Equal(armorIcon, model.Armor.Value.Icon.AssetId);

        Assert.NotNull(model.Boots);
        Assert.Equal(144, model.Boots!.Value.FrameNativeX);
        Assert.Equal(80, model.Boots.Value.FrameNativeY);   // icons.Y (0x10) + 1*0x28 + 0x18
        Assert.Equal(bootsIcon, model.Boots.Value.Icon.AssetId);
    }

    [Fact]
    public void ArmorAndBoots_NullWhenNothingResolved()
    {
        var model = ComposeAtRest(armorName: null, bootsName: null);

        Assert.Null(model.ArmorName);
        Assert.Null(model.BootsName);
        Assert.Null(model.Armor);
        Assert.Null(model.Boots);
    }

    [Fact]
    public void Boxes_AtRest_MatchDirectorPositions_AllSeven()
    {
        var model = ComposeAtRest();

        Assert.Equal(7, model.Boxes.Count);
        var armory = model.Boxes.Single(b => b.BoxIndex == 0);
        Assert.Equal(0x08, armory.NativeX);
        Assert.Equal(0x10, armory.NativeY);

        var moneyBox = model.Boxes.Single(b => b.BoxIndex == 6);
        Assert.Equal(0xB0, moneyBox.NativeX);
        Assert.Equal(0x60, moneyBox.NativeY);
    }

    [Theory]
    [InlineData(0, 0x30, 0x04)]
    [InlineData(1, 0x08, 0x14)]
    [InlineData(2, 0x58, 0x14)]
    [InlineData(3, 0x08, 0x44)]
    [InlineData(4, 0x58, 0x44)]
    [InlineData(5, 0x30, 0x54)]
    [InlineData(6, 0x30, 0x2c)]
    public void ArmoryIcon_CellAtBoxPlusPlanOffset(int index, int offsetX, int offsetY)
    {
        var iconId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var armoryIcons = (Guid?[])NoArmoryIcons.Clone();
        armoryIcons[index] = iconId;

        var model = ComposeAtRest(armoryIconAssetIds: armoryIcons);

        var icon = Assert.Single(model.ArmoryIcons);
        Assert.Equal(index, icon.SlotIndex);
        Assert.Equal(iconId, icon.Icon.AssetId);
        Assert.Equal(0x08 + offsetX, icon.Icon.BoxNativeX);
        Assert.Equal(0x10 + offsetY, icon.Icon.BoxNativeY);
        Assert.Equal(24, icon.Icon.BoxNativeWidth);
        Assert.Equal(32, icon.Icon.BoxNativeHeight);
    }

    [Fact]
    public void ArmoryIcons_OnlyEmittedForOwnedCrests()
    {
        var model = ComposeAtRest(); // no crest owned
        Assert.Empty(model.ArmoryIcons);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void KeyItemIcon_CellAtBoxPlusIndexTimesThirtyTwoPlusEight(int index)
    {
        var iconId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var keyIcons = (Guid?[])NoKeyItemIcons.Clone();
        keyIcons[index] = iconId;

        var model = ComposeAtRest(keyItemIconAssetIds: keyIcons);

        var icon = Assert.Single(model.KeyItemIcons);
        Assert.Equal(index, icon.SlotIndex);
        Assert.Equal(0x08 + index * 0x20 + 8, icon.Icon.BoxNativeX); // key items box X (0x08)
        Assert.Equal(0x80 + 4, icon.Icon.BoxNativeY);                // key items box Y (0x80)
    }

    [Fact]
    public void KeyItemIcons_EmptyPositionsStayEmpty_NoDoubling()
    {
        var keyIcons = (Guid?[])NoKeyItemIcons.Clone();
        keyIcons[0] = Guid.Parse("55555555-5555-5555-5555-555555555555");
        keyIcons[1] = Guid.Parse("66666666-6666-6666-6666-666666666666");
        // positions 2..4 left null - the executable's own lost guard (D-E13D-24): once the search is
        // exhausted, the remaining positions stay empty, never duplicated.

        var model = ComposeAtRest(keyItemIconAssetIds: keyIcons);

        Assert.Equal(2, model.KeyItemIcons.Count);
        Assert.DoesNotContain(model.KeyItemIcons, i => i.SlotIndex >= 2);
    }

    [Fact]
    public void MoneyDigits_FourDigitsMostSignificantFirst_OnTheMoneyFalconKeyBox()
    {
        var model = ComposeAtRest(money: 1234);

        Assert.Equal(4, model.MoneyDigits.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, model.MoneyDigits.Select(d => d.Value));

        var boxX = 0xB0;
        var boxY = 0x60;
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(boxX + 0x18 + i * 8, model.MoneyDigits[i].NativeX);
            Assert.Equal(boxY + 4, model.MoneyDigits[i].NativeY);
        }
    }

    [Fact]
    public void FalconAndKeyDigits_TwoDigitsEach_SamePlacesAsTheMainInventory()
    {
        var model = ComposeAtRest(falconTotal: 7, keyCount: 42);

        Assert.Equal(new[] { 0, 7 }, model.FalconDigits.Select(d => d.Value));
        Assert.Equal(new[] { 4, 2 }, model.KeyDigits.Select(d => d.Value));

        var boxX = 0xB0;
        var boxY = 0x60;
        Assert.Equal(boxX + 0x28, model.FalconDigits[0].NativeX);
        Assert.Equal(boxY + 0x1c, model.FalconDigits[0].NativeY);
        Assert.Equal(boxX + 0x28, model.KeyDigits[0].NativeX);
        Assert.Equal(boxY + 0x34, model.KeyDigits[0].NativeY);
    }

    [Fact]
    public void DescriptionLines_ShowExactlyWhatTheDirectorDrew_LineOneAlwaysEmpty()
    {
        var model = ComposeAtRest(line0: "Confortable protection en tissu.", line1: "");
        Assert.Equal("Confortable protection en tissu.", model.DescriptionLine0);
        Assert.Equal(string.Empty, model.DescriptionLine1);

        Assert.Equal(0x10 + 0x10, model.DescriptionLine0X);
        Assert.Equal(0xA8 + 0xc, model.DescriptionLine0Y);
        Assert.Equal(0x10 + 0x10, model.DescriptionLine1X);
        Assert.Equal(0xA8 + 0x1c, model.DescriptionLine1Y);
    }

    // Cursor reference table (plan §1.5): box index (this class' own RestBoxPositions order) and offset per
    // position 0..13 - CursorBoxByPosition/CursorOffsetX/CursorOffsetY of AlundraSubInventoryDirector.
    private static readonly int[] CursorBoxByPosition = { 0, 0, 0, 0, 0, 0, 0, 4, 4, 1, 1, 1, 1, 1 };
    private static readonly int[] CursorOffsetX = { 0x42, 0x1A, 0x6A, 0x1A, 0x6A, 0x42, 0x42, 0x1C, 0x1C, 0x1C, 0x3C, 0x5C, 0x72, 0x92 };
    private static readonly int[] CursorOffsetY = { -4, 0x0C, 0x0C, 0x3C, 0x3C, 0x4C, 0x24, 0x10, 0x34, 0x00, 0x00, 0x00, 0x00, 0x00 };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void Cursor_BasePosition_PerPosition_AllFourteen(int position)
    {
        var boxIndex = CursorBoxByPosition[position];
        var offsetX = CursorOffsetX[position];
        var offsetY = CursorOffsetY[position];

        var model = ComposeAtRest(cursorReference: (boxIndex, offsetX, offsetY));

        var (boxX, boxY) = RestBoxPositions[boxIndex];
        Assert.Equal(boxX + offsetX, model.Cursor.NativeX);
        Assert.Equal(boxY + offsetY, model.Cursor.NativeY);
    }

    [Fact]
    public void Icon_CentresInItsCell_AtVariousIconSizes()
    {
        var iconId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var armoryIcons = (Guid?[])NoArmoryIcons.Clone();
        armoryIcons[0] = iconId;

        var model = ComposeAtRest(armoryIconAssetIds: armoryIcons);
        var icon = Assert.Single(model.ArmoryIcons).Icon;

        // Same centring formula AlundraHudIcon already carries (D-E13D-10/D-E13D-23): an icon smaller than
        // 24x32 lands centred in its cell (16x15 -> (4, 8) offset at scale 1).
        Assert.Equal(icon.BoxNativeX + 4, icon.ScreenLeft(16, pixelScale: 1));
        Assert.Equal(icon.BoxNativeY + 8, icon.ScreenTop(15, pixelScale: 1));
    }
}
