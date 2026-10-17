#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Items;

namespace Alundra.UI.SubInventory;

/// <summary>The armor or boots equipment icon, with its selection frame (<c>wind_039</c>) at the SAME native
/// position (plan §1.5, <c>FUN_80052fb4</c>: the frame is visible only when the item resolves) - null when
/// nothing is equipped there, matching <see cref="AlundraSubInventoryDirector.ArmorItemId"/>/<c>BootsItemId</c>.</summary>
public readonly record struct SubInventoryEquipmentIcon(Guid AssetId, int NativeX, int NativeY);
