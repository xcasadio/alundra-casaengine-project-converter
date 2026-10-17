#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;
using Alundra.UI.Hud;

namespace Alundra.UI.Inventory;

/// <summary>The whole inventory screen's display state for one tick - <see cref="InventoryComposer.Compose"/>'s
/// own return value, written into <see cref="InventoryViewModel"/> by <see cref="InventoryPresenter"/>.
/// <see cref="Visible"/> false means every other member is a default/empty placeholder - the screen must
/// not read them (same "IsDrawn faux -&gt; liste vide" contract as <see cref="HudComposer"/>).</summary>
public sealed class InventoryDisplayModel
{
    public bool Visible { get; init; }
    public IReadOnlyList<InventoryBoxSprite> Boxes { get; init; } = Array.Empty<InventoryBoxSprite>();
    public IReadOnlyList<InventorySlotIcon> SlotIcons { get; init; } = Array.Empty<InventorySlotIcon>();
    public bool HerbDigitVisible { get; init; }
    public InventoryDigit HerbDigit { get; init; }
    public IReadOnlyList<InventoryDigit> MoneyDigits { get; init; } = Array.Empty<InventoryDigit>();
    public IReadOnlyList<InventoryDigit> FalconDigits { get; init; } = Array.Empty<InventoryDigit>();
    public IReadOnlyList<InventoryDigit> KeyDigits { get; init; } = Array.Empty<InventoryDigit>();
    public string WeaponName { get; init; } = string.Empty;
    public int WeaponNameX { get; init; }
    public int WeaponNameY { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public int ItemNameX { get; init; }
    public int ItemNameY { get; init; }
    public string DescriptionLine0 { get; init; } = string.Empty;
    public int DescriptionLine0X { get; init; }
    public int DescriptionLine0Y { get; init; }
    public string DescriptionLine1 { get; init; } = string.Empty;
    public int DescriptionLine1X { get; init; }
    public int DescriptionLine1Y { get; init; }
    public InventorySelectionFrame WeaponSelectionFrame { get; init; }
    public InventorySelectionFrame ItemSelectionFrame { get; init; }
    public InventoryCursorState Cursor { get; init; }
}
