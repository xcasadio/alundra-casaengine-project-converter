#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;
using Alundra.UI.Hud;
using Alundra.UI.SubInventory;

namespace Alundra.UI.Inventory;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md): pure port of the main inventory's own per-frame drawing -
/// <c>DisplayWeaponAndItemIcons</c> (<c>MainInventoryManager.cs:1267-1457</c>), the herb special case
/// therein, <c>DisplayAmountOfMoneyFalconKeys</c> (<c>:1478-1588</c>), <c>DisplayInventoryCursor</c>/
/// <c>UpdateCursorSpritePosition</c> (<c>:1591-1615</c>, <c>:907-910</c>), <c>FUN_800562dc</c>
/// (<c>:1157-1225</c>, weapon/item names) and the description box positions used by
/// <c>DisplayInventoryTexts</c>/<c>DisplayInventoryDescription</c> (<c>:929-1154</c>, <c>:1097-1134</c>).
/// No MGUI type appears anywhere in this file (same "pure function of plain data" shape as
/// <see cref="AlundraHudComposer"/>) - every input is either a director-exposed value or a value the
/// caller (D5's presenter) already resolved through <see cref="PlayerManager"/>/
/// <see cref="AlundraItemTables"/>, themselves plain C#, no MGUI.
///
/// <b>D-E13D-10 (icons centred, not pixel-exact)</b>: every grid icon reuses
/// <see cref="AlundraHudIcon"/>'s own centring formula (<see cref="AlundraHudIcon.ScreenLeft"/>/
/// <see cref="AlundraHudIcon.ScreenTop"/>), the SAME 24x32 (0x18x0x20 - the selection frame's own size)
/// box the original's <c>g_uiBoxesInventoryAnimationOffsetX/Y</c> step by, instead of the original's own
/// top-left placement.
/// </summary>
public static class AlundraInventoryComposer
{
    public const int SlotCount = 24;
    private const int CellWidth = 0x18;  // 24 - the selection frame's own size, D-E13D-10's "cell".
    private const int CellHeight = 0x20; // 32
    private const int HerbSlotIndex = 6; // row 1, col 0 - MainInventoryManager.cs:1305 ("special case herbs").

    // StaticVariables.cs:12363-12377 (g_uiBoxesInventoryAnimationOffsetX/Y) - duplicated here so this
    // composer stays a pure function of plain data (same "duplicated citation, not a cross-file
    // constant" shape AlundraHudPresenter's own BakedBoxY already uses for UIBoxHud.Y).
    private static readonly int[] OffsetX =
    {
        0x08, 0x20, 0x38, 0x50, 0x68, 0x80,
        0x08, 0x20, 0x38, 0x50, 0x68, 0x80,
        0x08, 0x20, 0x38, 0x50, 0x68, 0x80,
        0x08, 0x20, 0x38, 0x50, 0x68, 0x80,
    };

    private static readonly int[] OffsetY =
    {
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x38, 0x38, 0x38, 0x38, 0x38, 0x38,
        0x54, 0x54, 0x54, 0x54, 0x54, 0x54,
        0x70, 0x70, 0x70, 0x70, 0x70, 0x70,
    };

    // UpdateCursorSpritePosition (0x80050908, ALUN_CD.EXE France) reads the phase offsets as WORDS, index
    // FrameDelay/10 shifted left by 2, from 0x800a82c8 (X) and 0x800a82d8 (Y): (0,0) (-1,+1) (-2,+2) (-2,+2).
    // The decompilation lost the stride (StaticVariables.cs:71-80 keeps the same bytes as short[] halves and
    // MainInventoryManager.cs:137-138 indexes them with a 2-byte stride, which reads (0,0) (0,0) (-1,+1) (-1,0));
    // the executable is the reference (plan E13.d SI9).
    private static readonly int[] CursorPhaseX = { 0, -1, -2, -2 };
    private static readonly int[] CursorPhaseY = { 0, 1, 2, 2 };

    /// <param name="isDrawn">Director's own <see cref="AlundraInventoryDirector.IsDrawn"/> - false makes
    /// every other argument irrelevant (nothing is read), matching the setup tick's own "nothing drawn yet".</param>
    /// <param name="boxPositions">The seven boxes' current native (X, Y), <see cref="AlundraInventoryDirector.BoxPosition"/>
    /// order (index 4, the spacer, is read but never emitted).</param>
    /// <param name="slotItemIds">24 entries, <see cref="AlundraInventoryDirector.ResolveSlotItemId"/>'s
    /// own return per slot - null for an empty/unowned slot.</param>
    /// <param name="slotIconAssetIds">24 entries, the icon asset id for each slot with a resolved item
    /// (<see cref="AlundraItemTables.TryGetIconAssetId"/>'s own result) - null where the item has no
    /// portrait, or the slot itself has none.</param>
    /// <param name="currentWeaponItemId">The equipped weapon's item id (<see cref="PlayerManager.GetItemIdFromCurrentWeapon"/>),
    /// or null when nothing is equipped/resolvable - used only to find which of slots 0-5 shows the
    /// weapon selection frame.</param>
    /// <param name="currentItemId">The equipped accessory's item id (<see cref="PlayerManager.SetItemIdFromCurrentItemId"/>),
    /// or null - used only to find which of slots 6-23 shows the item selection frame.</param>
    /// <param name="herbOwnedCount">The herb slot's owned count (<see cref="PlayerManager.GetNumberOfItem"/>
    /// for item <c>0x24</c>) - 0 hides the count digit, matching the original's own <c>numOfItem != 0</c> guard.</param>
    public static InventoryDisplayModel Compose(
        bool isDrawn,
        IReadOnlyList<(int X, int Y)> boxPositions,
        int selectedSlotId,
        int cursorFrameDelay,
        string equippedWeaponName,
        string equippedItemName,
        string descriptionLine0,
        string descriptionLine1,
        IReadOnlyList<int?> slotItemIds,
        IReadOnlyList<Guid?> slotIconAssetIds,
        int? currentWeaponItemId,
        int? currentItemId,
        int herbOwnedCount,
        int money,
        int keyCount,
        int falconTotal)
    {
        if (!isDrawn)
        {
            return new InventoryDisplayModel { Visible = false };
        }

        var boxes = new List<InventoryBoxSprite>(6);
        for (var i = 0; i < boxPositions.Count; i++)
        {
            if (i == 4)
            {
                continue; // the 0x0 spacer - nothing to draw (plan §1.4).
            }

            var (x, y) = boxPositions[i];
            boxes.Add(new InventoryBoxSprite(i, x, y));
        }

        var weaponBackground = boxPositions[0];

        var icons = new List<InventorySlotIcon>(SlotCount);
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var assetId = slot < slotIconAssetIds.Count ? slotIconAssetIds[slot] : null;
            if (assetId == null)
            {
                continue;
            }

            var cellX = weaponBackground.X + OffsetX[slot];
            var cellY = weaponBackground.Y + OffsetY[slot];
            icons.Add(new InventorySlotIcon(slot, new AlundraHudIcon(assetId.Value, cellX, cellY, CellWidth, CellHeight)));
        }

        // MainInventoryManager.cs:1405-1408/1418-1421 - row 0 (slots 0-5) compares against the equipped
        // weapon; rows 1-3 (slots 6-23, herbs' own special case included: same target cell either way,
        // see this class' own doc) compare against the equipped accessory.
        var weaponFrame = default(InventorySelectionFrame);
        for (var slot = 0; slot < 6; slot++)
        {
            if (currentWeaponItemId != null && slot < slotItemIds.Count && slotItemIds[slot] == currentWeaponItemId)
            {
                weaponFrame = new InventorySelectionFrame(true, weaponBackground.X + OffsetX[slot], weaponBackground.Y + OffsetY[slot]);
                break;
            }
        }

        var itemFrame = default(InventorySelectionFrame);
        for (var slot = 6; slot < SlotCount; slot++)
        {
            if (currentItemId != null && slot < slotItemIds.Count && slotItemIds[slot] == currentItemId)
            {
                itemFrame = new InventorySelectionFrame(true, weaponBackground.X + OffsetX[slot], weaponBackground.Y + OffsetY[slot]);
                break;
            }
        }

        // MainInventoryManager.cs:1338-1339 - the herb count digit, cell + (0x10, 0x10).
        var herbDigit = new InventoryDigit(
            herbOwnedCount % 10,
            weaponBackground.X + OffsetX[HerbSlotIndex] + 0x10,
            weaponBackground.Y + OffsetY[HerbSlotIndex] + 0x10);

        var moneyFalconKeyBox = boxPositions[5];
        var moneyDigits = ComposeDigits(money, 4, moneyFalconKeyBox.X + 0x18, moneyFalconKeyBox.Y + 4);
        var falconDigits = ComposeDigits(falconTotal, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x1c);
        var keyDigits = ComposeDigits(keyCount, 2, moneyFalconKeyBox.X + 0x28, moneyFalconKeyBox.Y + 0x34);

        var weaponNameBox = boxPositions[2];
        var itemNameBox = boxPositions[3];
        var descriptionBox = boxPositions[6];

        // MainInventoryManager.cs:929-1064: the two description lines exactly as the director says the
        // original drew them THIS tick (AlundraInventoryDirector.DrawnDescriptionLine0/1) - the name or the
        // first line on line 0, the second line on line 1, and nothing on the ticks and slots that draw
        // nothing.
        var line0 = descriptionLine0;
        var line1 = descriptionLine1;

        // :907-911 - the cursor's POSITION is set by UpdateCursorSpritePosition from the animation counter
        // BEFORE DisplayInventoryCursor increments it (:1593-1597), its SPRITE from the counter after. The
        // director has already incremented the counter this tick, so the position uses the previous value.
        var cursorPhase = Math.Clamp(cursorFrameDelay / 10, 0, 3);
        var cursorPositionPhase = Math.Clamp((cursorFrameDelay + 0x27) % 0x28 / 10, 0, 3);
        var cursorBaseX = weaponBackground.X + OffsetX[selectedSlotId] + 0x12;
        var cursorBaseY = weaponBackground.Y + OffsetY[selectedSlotId] - 8;
        var cursor = new InventoryCursorState(
            cursorBaseX + CursorPhaseX[cursorPositionPhase],
            cursorBaseY + CursorPhaseY[cursorPositionPhase],
            cursorPhase,
            cursorBaseX,
            cursorBaseY);

        return new InventoryDisplayModel
        {
            Visible = true,
            Boxes = boxes,
            SlotIcons = icons,
            HerbDigitVisible = herbOwnedCount != 0,
            HerbDigit = herbDigit,
            MoneyDigits = moneyDigits,
            FalconDigits = falconDigits,
            KeyDigits = keyDigits,
            WeaponName = equippedWeaponName,
            WeaponNameX = weaponNameBox.X + 0x10,
            WeaponNameY = weaponNameBox.Y + 8,
            ItemName = equippedItemName,
            ItemNameX = itemNameBox.X + 0x10,
            ItemNameY = itemNameBox.Y + 8,
            DescriptionLine0 = line0,
            DescriptionLine0X = descriptionBox.X + 0x10,
            DescriptionLine0Y = descriptionBox.Y + 0xc,
            DescriptionLine1 = line1,
            DescriptionLine1X = descriptionBox.X + 0x10,
            DescriptionLine1Y = descriptionBox.Y + 0x1c,
            WeaponSelectionFrame = weaponFrame,
            ItemSelectionFrame = itemFrame,
            Cursor = cursor,
        };
    }

    /// <summary>Port of the three identical digit loops in <c>DisplayAmountOfMoneyFalconKeys</c>
    /// (<c>MainInventoryManager.cs:1478-1588</c>) - most significant digit first, 8 native pixels apart.
    /// Internal (not private): <see cref="AlundraSubInventoryComposer"/> reuses it as-is, plan §1.5's own
    /// "the main's places" for the sub-inventory's money/falcon/key digits, on the SAME shared box - a
    /// one-line visibility change rather than a copy (E13.d SI4 brief).</summary>
    internal static List<InventoryDigit> ComposeDigits(int value, int count, int startX, int y)
    {
        var digits = new List<InventoryDigit>(count);
        var divisor = 1;
        for (var i = 1; i < count; i++)
        {
            divisor *= 10;
        }

        var x = startX;
        for (var i = 0; i < count; i++)
        {
            var digit = value / divisor % 10;
            digits.Add(new InventoryDigit(digit, x, y));
            divisor /= 10;
            x += 8;
        }

        return digits;
    }
}
