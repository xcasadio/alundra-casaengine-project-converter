#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>One of the seven baked box sprites (D-E13D-13/SI2) at its director-tweened native position.
/// <see cref="BoxIndex"/> is <see cref="AlundraSubInventoryDirector.BoxPosition"/>'s own public index order:
/// 0 armory, 1 key items, 2 armor name, 3 boots name, 4 icons, 5 description, 6 money/falcon/key.</summary>
public readonly record struct SubInventoryBoxSprite(int BoxIndex, int NativeX, int NativeY);

/// <summary>One armory crest icon (0..6, Rubis..Diamant) or key-item icon (0..4) - the slot it belongs to
/// plus the same centred-icon shape <see cref="AlundraHudComposer"/>'s own <see cref="AlundraHudIcon"/> uses
/// (D-E13D-23).</summary>
public readonly record struct SubInventorySlotIcon(int SlotIndex, AlundraHudIcon Icon);

/// <summary>The armor or boots equipment icon plus its selection frame (<c>wind_039</c>) at the SAME native
/// position (plan §1.5, <c>FUN_80052fb4</c>: the frame is visible only when the item resolves) - null when
/// nothing is equipped there, matching <see cref="AlundraSubInventoryDirector.ArmorItemId"/>/<c>BootsItemId</c>.</summary>
public readonly record struct SubInventoryEquipmentIcon(AlundraHudIcon Icon, int FrameNativeX, int FrameNativeY);

/// <summary>The cursor's base native position (plan §1.5: <c>PTR_ARRAY_800b44b8</c>/<c>INT_ARRAY_800b4368</c>/
/// <c>800b43a0</c>) - unlike the main inventory's own <see cref="InventoryCursorState"/>, there is no
/// per-phase offset to compute here: the <c>ui_inventory_cursor</c> animation carries it itself (D-E13D-27,
/// same "animation carries the offset" choice the main inventory's own screen already makes for its own
/// <c>Cursor.Show</c>).</summary>
public readonly record struct SubInventoryCursorState(int NativeX, int NativeY);

/// <summary>The whole sub-inventory screen's display state for one tick - <see cref="AlundraSubInventoryComposer.Compose"/>'s
/// own return value, written into <see cref="AlundraSubInventoryViewModel"/> by
/// <see cref="AlundraSubInventoryPresenter"/>. <see cref="Visible"/> false means every other member is a
/// default/empty placeholder - same "IsDrawn faux -&gt; liste vide" contract as
/// <see cref="InventoryDisplayModel"/>.</summary>
public sealed class SubInventoryDisplayModel
{
    public bool Visible { get; init; }
    public IReadOnlyList<SubInventoryBoxSprite> Boxes { get; init; } = Array.Empty<SubInventoryBoxSprite>();
    public IReadOnlyList<SubInventorySlotIcon> ArmoryIcons { get; init; } = Array.Empty<SubInventorySlotIcon>();
    public IReadOnlyList<SubInventorySlotIcon> KeyItemIcons { get; init; } = Array.Empty<SubInventorySlotIcon>();
    public SubInventoryEquipmentIcon? Armor { get; init; }
    public SubInventoryEquipmentIcon? Boots { get; init; }
    public string? ArmorName { get; init; }
    public int ArmorNameX { get; init; }
    public int ArmorNameY { get; init; }
    public string? BootsName { get; init; }
    public int BootsNameX { get; init; }
    public int BootsNameY { get; init; }
    public string DescriptionLine0 { get; init; } = string.Empty;
    public int DescriptionLine0X { get; init; }
    public int DescriptionLine0Y { get; init; }
    public string DescriptionLine1 { get; init; } = string.Empty;
    public int DescriptionLine1X { get; init; }
    public int DescriptionLine1Y { get; init; }
    public IReadOnlyList<InventoryDigit> MoneyDigits { get; init; } = Array.Empty<InventoryDigit>();
    public IReadOnlyList<InventoryDigit> FalconDigits { get; init; } = Array.Empty<InventoryDigit>();
    public IReadOnlyList<InventoryDigit> KeyDigits { get; init; } = Array.Empty<InventoryDigit>();
    public SubInventoryCursorState Cursor { get; init; }
}

/// <summary>
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md): pure port of the sub-inventory's own per-frame drawing -
/// <c>FUN_80052dd8</c> (armory, <c>SubInventoryManager.cs:1203-1236</c>), <c>FUN_80052c64</c> (key items,
/// <c>:1239-1281</c>), <c>FUN_80052fb4</c>/<c>FUN_80053144</c> (armor/boots icons and their frames,
/// <c>:1157-1200</c>/<c>:1119-1154</c>), <c>FUN_80052f24</c> (names, <c>:297-323</c>),
/// <c>DisplayAmountOfMoneyFalconKeys2</c> (<c>:742-854</c>, same places as the main inventory's own digits)
/// and <c>UpdateCursorSpritePosition</c>'s own reference table (plan §1.5). No MGUI type appears anywhere in
/// this file, same "pure function of plain data" shape as <see cref="AlundraInventoryComposer"/> - every
/// input is either a director-exposed value or a value the caller (SI4's presenter) already resolved through
/// <see cref="AlundraItemTables"/>/<see cref="AlundraGameState"/>.
///
/// <b>D-E13D-23 (icons centred, at their ORIGINAL top-left)</b>: armory and key-item icons are centred in a
/// 24x32 cell whose top-left is the position the original itself draws them at (extension of D-E13D-10:
/// there is no dedicated frame for these two, unlike armor/boots which DO have <c>wind_039</c>) - the SAME
/// <see cref="AlundraHudIcon"/> centring formula, just without a frame image alongside it.
/// </summary>
public static class AlundraSubInventoryComposer
{
    private const int CellWidth = 0x18;  // 24 - same "cell" as the main inventory's own D-E13D-10.
    private const int CellHeight = 0x20; // 32

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

    /// <param name="isDrawn">Director's own <see cref="AlundraSubInventoryDirector.IsDrawn"/> - false makes
    /// every other argument irrelevant (nothing is read).</param>
    /// <param name="boxPositions">The seven boxes' current native (X, Y), <see cref="AlundraSubInventoryDirector.BoxPosition"/>
    /// order.</param>
    /// <param name="armoryIconAssetIds">7 entries (Rubis..Diamant) - the icon asset id for each OWNED crest
    /// (<see cref="AlundraSubInventoryDirector.ArmoryOwned"/> true and <see cref="AlundraItemTables.TryGetIconAssetId"/>
    /// resolved), null otherwise (not owned, or no icon).</param>
    /// <param name="keyItemIconAssetIds">5 entries, the icon asset id for each entry of
    /// <see cref="AlundraSubInventoryDirector.KeyItemIds"/> that is filled and resolves, null otherwise.</param>
    /// <param name="armorIconAssetId">The equipped armor's icon (<see cref="AlundraSubInventoryDirector.ArmorItemId"/>
    /// resolved through <see cref="AlundraItemTables.TryGetIconAssetId"/>), or null when nothing is equipped
    /// there (or it has no icon) - hides both the icon and its frame.</param>
    /// <param name="bootsIconAssetId">Same as <paramref name="armorIconAssetId"/>, boots.</param>
    /// <param name="cursorReference"><see cref="AlundraSubInventoryDirector.CursorReference"/>'s own result
    /// for <see cref="AlundraSubInventoryDirector.SelectedPosition"/> - which box the cursor sits on plus its
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
            armoryIcons.Add(new SubInventorySlotIcon(i, new AlundraHudIcon(assetId.Value, cellX, cellY, CellWidth, CellHeight)));
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
            keyItemIcons.Add(new SubInventorySlotIcon(i, new AlundraHudIcon(assetId.Value, cellX, cellY, CellWidth, CellHeight)));
        }

        var iconsBox = boxPositions[4];
        var armor = ComposeEquipmentIcon(iconsBox, 0, armorIconAssetId);
        var boots = ComposeEquipmentIcon(iconsBox, 1, bootsIconAssetId);

        var armorNameBox = boxPositions[2];
        var bootsNameBox = boxPositions[3];
        var descriptionBox = boxPositions[5];
        var moneyFalconKeyBox = boxPositions[6];

        var moneyDigits = AlundraInventoryComposer.ComposeDigits(money, 4, moneyFalconKeyBox.X + 0x18, moneyFalconKeyBox.Y + 4);
        var falconDigits = AlundraInventoryComposer.ComposeDigits(falconTotal, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x1c);
        var keyDigits = AlundraInventoryComposer.ComposeDigits(keyCount, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x34);

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

        var cellX = iconsBox.X + EquipmentIconOffsetX;
        var cellY = iconsBox.Y + slot * EquipmentIconSlotStride + EquipmentIconOffsetY;
        return new SubInventoryEquipmentIcon(new AlundraHudIcon(assetId.Value, cellX, cellY, CellWidth, CellHeight), cellX, cellY);
    }
}
