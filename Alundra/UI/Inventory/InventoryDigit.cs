#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.UI.Inventory;

/// <summary>One digit glyph (0-9) at a native pixel position - money, falcon, key or the herb count.</summary>
public readonly record struct InventoryDigit(int Value, int NativeX, int NativeY);
