#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;

namespace Alundra.UI.SubInventory;

/// <summary>One of the seven baked box sprites (D-E13D-13/SI2) at its director-tweened native position.
/// <see cref="BoxIndex"/> is <see cref="SubInventoryDirector.BoxPosition"/>'s own public index order:
/// 0 armory, 1 key items, 2 armor name, 3 boots name, 4 icons, 5 description, 6 money/falcon/key.</summary>
public readonly record struct SubInventoryBoxSprite(int BoxIndex, int NativeX, int NativeY);
