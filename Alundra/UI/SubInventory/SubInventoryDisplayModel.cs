#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;
using Alundra.UI.Inventory;

namespace Alundra.UI.SubInventory;

/// <summary>The whole sub-inventory screen's display state for one tick - <see cref="SubInventoryComposer.Compose"/>'s
/// own return value, written into <see cref="SubInventoryViewModel"/> by
/// <see cref="SubInventoryPresenter"/>. <see cref="Visible"/> false means every other member is a
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
