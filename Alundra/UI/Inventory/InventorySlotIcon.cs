#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;
using Alundra.UI.Hud;

namespace Alundra.UI.Inventory;

/// <summary>One grid slot's icon - the slot it belongs to (0..23) plus the same centred-icon shape
/// <see cref="HudComposer"/>'s own <see cref="HudIcon"/> uses (D-E13D-10).</summary>
public readonly record struct InventorySlotIcon(int SlotIndex, HudIcon Icon);
