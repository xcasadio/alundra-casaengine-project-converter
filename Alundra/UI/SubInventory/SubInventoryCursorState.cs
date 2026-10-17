#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;
using Alundra.UI.Inventory;

namespace Alundra.UI.SubInventory;

/// <summary>The cursor's base native position (plan §1.5: <c>PTR_ARRAY_800b44b8</c>/<c>INT_ARRAY_800b4368</c>/
/// <c>800b43a0</c>) - unlike the main inventory's own <see cref="InventoryCursorState"/>, there is no
/// per-phase offset to compute here: the <c>ui_inventory_cursor</c> animation carries it itself (D-E13D-27,
/// same "animation carries the offset" choice the main inventory's own screen already makes for its own
/// <c>Cursor.Show</c>).</summary>
public readonly record struct SubInventoryCursorState(int NativeX, int NativeY);
