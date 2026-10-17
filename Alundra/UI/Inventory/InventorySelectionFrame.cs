#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.UI.Inventory;

/// <summary>One of the two selection frames (<c>wind_039</c>) - invisible when nothing in that row is
/// currently equipped (e.g. no accessory equipped at all, <see cref="AlundraPlayerManager.NoItem"/>).</summary>
public readonly record struct InventorySelectionFrame(bool Visible, int NativeX, int NativeY);
