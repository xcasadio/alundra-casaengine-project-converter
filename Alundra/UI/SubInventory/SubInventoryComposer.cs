#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;
using Alundra.UI.Inventory;

namespace Alundra.UI.SubInventory;

/// <summary>
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md): pure port of the sub-inventory's own per-frame drawing -
/// <c>FUN_80052dd8</c> (armory, <c>SubInventoryManager.cs:1203-1236</c>), <c>FUN_80052c64</c> (key items,
/// <c>:1239-1281</c>), <c>FUN_80052fb4</c>/<c>FUN_80053144</c> (armor/boots icons and their frames,
/// <c>:1157-1200</c>/<c>:1119-1154</c>), <c>FUN_80052f24</c> (names, <c>:297-323</c>),
/// <c>DisplayAmountOfMoneyFalconKeys2</c> (<c>:742-854</c>, same places as the main inventory's own digits)
/// and <c>UpdateCursorSpritePosition</c>'s own reference table (plan §1.5). No MGUI type appears anywhere in
/// this file, same "pure function of plain data" shape as <see cref="InventoryComposer"/> - every
/// input is either a director-exposed value or a value the caller (SI4's presenter) already resolved through
/// <see cref="ItemTables"/>/<see cref="GameState"/>.
///
/// <b>D-E13D-34 (icons at their original top-left, no centring)</b>: every icon - armory, key items, armor,
/// boots - is drawn where the original draws it, whatever its sprite's size; the author's "au plus simple",
/// replacing D-E13D-23's centring in an invisible 24x32 cell.
/// </summary>
public static class SubInventoryComposer
{
    // StaticVariables.cs (INT_ARRAY_800b42f8/800b4314), plan §1.5's own table - the armory's seven crest
    // offsets (Rubis..Diamant), duplicated here so this composer stays a pure function of plain data (same
    // "duplicated citation, not a cross-file constant" shape AlundraInventoryComposer's own OffsetX/Y use).
    private static readonly int[] ArmoryOffsetX = { 0x30, 0x08, 0x58, 0x08, 0x58, 0x30, 0x30 };
    private static readonly int[] ArmoryOffsetY = { 0x04, 0x14, 0x14, 0x44, 0x44, 0x54, 0x2c };

    // FUN_80052fb4/FUN_80053144 (plan §1.5): armor is slot 0, boots slot 1 of the icons box - (box + 8,
    // + slot * 0x28 + 0x18).
    private const int EquipmentIconOffsetX = 0x08;
    private const int EquipmentIconOffsetY = 0x18;
    private const int EquipmentIconSlotStride = 0x28;

    /// <param name="isDrawn">Director's own <see cref="SubInventoryDirector.IsDrawn"/> - false makes
    /// every other argument irrelevant (nothing is read).</param>
    /// <param name="boxPositions">The seven boxes' current native (X, Y), <see cref="SubInventoryDirector.BoxPosition"/>
    /// order.</param>
    /// <param name="armoryIconAssetIds">7 entries (Rubis..Diamant) - the icon asset id for each OWNED crest
    /// (<see cref="SubInventoryDirector.ArmoryOwned"/> true and <see cref="ItemTables.TryGetIconAssetId"/>
    /// resolved), null otherwise (not owned, or no icon).</param>
    /// <param name="keyItemIconAssetIds">5 entries, the icon asset id for each entry of
    /// <see cref="SubInventoryDirector.KeyItemIds"/> that is filled and resolves, null otherwise.</param>
    /// <param name="armorIconAssetId">The equipped armor's icon (<see cref="SubInventoryDirector.ArmorItemId"/>
    /// resolved through <see cref="ItemTables.TryGetIconAssetId"/>), or null when nothing is equipped
    /// there (or it has no icon) - hides both the icon and its frame.</param>
    /// <param name="bootsIconAssetId">Same as <paramref name="armorIconAssetId"/>, boots.</param>
    /// <param name="cursorReference"><see cref="SubInventoryDirector.CursorReference"/>'s own result
    /// for <see cref="SubInventoryDirector.SelectedPosition"/> - which box the cursor sits on plus its
    /// offset.</param>
    public static SubInventoryDisplayModel Compose(
        bool isDrawn,
        IReadOnlyList<(int X, int Y)> boxPositions,
        IReadOnlyList<Guid?> armoryIconAssetIds,
        IReadOnlyList<Guid?> keyItemIconAssetIds,
        Guid? armorIconAssetId,
        Guid? bootsIconAssetId,
        string? armorName,
        string? bootsName,
        string descriptionLine0,
        string descriptionLine1,
        (int BoxIndex, int OffsetX, int OffsetY) cursorReference,
        int money,
        int keyCount,
        int falconTotal)
    {
        if (!isDrawn)
        {
            return new SubInventoryDisplayModel { Visible = false };
        }

        var boxes = new List<SubInventoryBoxSprite>(boxPositions.Count);
        for (var i = 0; i < boxPositions.Count; i++)
        {
            var (x, y) = boxPositions[i];
            boxes.Add(new SubInventoryBoxSprite(i, x, y));
        }

        var armoryBox = boxPositions[0];
        var armoryIcons = new List<SubInventorySlotIcon>(7);
        for (var i = 0; i < 7 && i < armoryIconAssetIds.Count; i++)
        {
            var assetId = armoryIconAssetIds[i];
            if (assetId == null)
            {
                continue;
            }

            var cellX = armoryBox.X + ArmoryOffsetX[i];
            var cellY = armoryBox.Y + ArmoryOffsetY[i];
            armoryIcons.Add(new SubInventorySlotIcon(i, new SubInventoryIcon(assetId.Value, cellX, cellY)));
        }

        var keyItemsBox = boxPositions[1];
        var keyItemIcons = new List<SubInventorySlotIcon>(5);
        for (var i = 0; i < 5 && i < keyItemIconAssetIds.Count; i++)
        {
            var assetId = keyItemIconAssetIds[i];
            if (assetId == null)
            {
                continue;
            }

            var cellX = keyItemsBox.X + i * 0x20 + 8;
            var cellY = keyItemsBox.Y + 4;
            keyItemIcons.Add(new SubInventorySlotIcon(i, new SubInventoryIcon(assetId.Value, cellX, cellY)));
        }

        var iconsBox = boxPositions[4];
        var armor = ComposeEquipmentIcon(iconsBox, 0, armorIconAssetId);
        var boots = ComposeEquipmentIcon(iconsBox, 1, bootsIconAssetId);

        var armorNameBox = boxPositions[2];
        var bootsNameBox = boxPositions[3];
        var descriptionBox = boxPositions[5];
        var moneyFalconKeyBox = boxPositions[6];

        var moneyDigits = InventoryComposer.ComposeDigits(money, 4, moneyFalconKeyBox.X + 0x18, moneyFalconKeyBox.Y + 4);
        var falconDigits = InventoryComposer.ComposeDigits(falconTotal, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x1c);
        var keyDigits = InventoryComposer.ComposeDigits(keyCount, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x34);

        var cursorBox = boxPositions[cursorReference.BoxIndex];
        var cursor = new SubInventoryCursorState(cursorBox.X + cursorReference.OffsetX, cursorBox.Y + cursorReference.OffsetY);

        return new SubInventoryDisplayModel
        {
            Visible = true,
            Boxes = boxes,
            ArmoryIcons = armoryIcons,
            KeyItemIcons = keyItemIcons,
            Armor = armor,
            Boots = boots,
            ArmorName = armorName,
            ArmorNameX = armorNameBox.X + 0x10,
            ArmorNameY = armorNameBox.Y + 8,
            BootsName = bootsName,
            BootsNameX = bootsNameBox.X + 0x10,
            BootsNameY = bootsNameBox.Y + 8,
            DescriptionLine0 = descriptionLine0,
            DescriptionLine0X = descriptionBox.X + 0x10,
            DescriptionLine0Y = descriptionBox.Y + 0xc,
            DescriptionLine1 = descriptionLine1,
            DescriptionLine1X = descriptionBox.X + 0x10,
            DescriptionLine1Y = descriptionBox.Y + 0x1c,
            MoneyDigits = moneyDigits,
            FalconDigits = falconDigits,
            KeyDigits = keyDigits,
            Cursor = cursor,
        };
    }

    private static SubInventoryEquipmentIcon? ComposeEquipmentIcon(
        (int X, int Y) iconsBox, int slot, Guid? assetId)
    {
        if (assetId == null)
        {
            return null;
        }

        var x = iconsBox.X + EquipmentIconOffsetX;
        var y = iconsBox.Y + slot * EquipmentIconSlotStride + EquipmentIconOffsetY;
        return new SubInventoryEquipmentIcon(assetId.Value, x, y);
    }
}
