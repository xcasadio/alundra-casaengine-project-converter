#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;

namespace Alundra.UI.SubInventory;

/// <summary>One armory crest icon (0..6, Rubis..Diamant) or key-item icon (0..4) - the slot it belongs to
/// plus the icon at its original position.</summary>
public readonly record struct SubInventorySlotIcon(int SlotIndex, SubInventoryIcon Icon);
