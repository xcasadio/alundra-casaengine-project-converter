#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.UI.Inventory;

/// <summary>The cursor (<c>wind_159/182/210/237</c>) - <see cref="Phase"/> is 0..3. <see cref="NativeX"/>/<see cref="NativeY"/>
/// include the original's per-phase pixel offset; <see cref="BaseNativeX"/>/<see cref="BaseNativeY"/> are the same
/// position without it, for a screen that plays the cursor as a UI animation carrying that offset itself
/// (parent ADR-0002, the converter's <c>ui_inventory_cursor</c>).</summary>
public readonly record struct InventoryCursorState(int NativeX, int NativeY, int Phase, int BaseNativeX, int BaseNativeY);
