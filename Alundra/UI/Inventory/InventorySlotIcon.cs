#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;
using Alundra.UI.Hud;

namespace Alundra.UI.Inventory;

/// <summary>One grid slot's icon - the slot it belongs to (0..23) plus the same centred-icon shape
/// <see cref="AlundraHudComposer"/>'s own <see cref="AlundraHudIcon"/> uses (D-E13D-10).</summary>
public readonly record struct InventorySlotIcon(int SlotIndex, AlundraHudIcon Icon);
