#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;

namespace Alundra.UI.SubInventory;

/// <summary>An icon drawn with its top-left at the native position the original itself draws it at - no
/// centring (D-E13D-34: the sub-inventory is the one screen whose icons are not laid out in the standard
/// 24x32 cells, so the main inventory's D-E13D-10 centring does not apply).</summary>
public readonly record struct SubInventoryIcon(Guid AssetId, int NativeX, int NativeY);
