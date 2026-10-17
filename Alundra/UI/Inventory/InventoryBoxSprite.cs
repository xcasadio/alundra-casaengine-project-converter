#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.UI.Inventory;

/// <summary>One of the six baked box sprites (D-E13D-13/D3.b) at its director-tweened native position.
/// <see cref="BoxIndex"/> is <see cref="AlundraInventoryDirector.BoxPosition"/>'s own index (0 weapon,
/// 1 item, 2 weapon name, 3 item name, 5 money/falcon/key, 6 description - index 4, the 0x0 spacer, never
/// appears here, nothing to draw).</summary>
public readonly record struct InventoryBoxSprite(int BoxIndex, int NativeX, int NativeY);
