#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>One creation of an effect by the oracle's real <c>0x8003BDD8</c> (the hold spark or an effect of the burst): the effect, whether it was created, and the fields it holds at the end of the logic of the tick.</summary>
/// <param name="SlotIndex">E20.f1b: the index of the slot the binary's pool gave it (<c>(slot - 0x80138608) / 0x80</c>), -1 when not created.</param>
/// <param name="Mode">E20.f1b: the slot's <c>+0x34</c> (1 for the aura attached to the hero), 0 otherwise.</param>
/// <param name="DepthOffset">E20.f1b: the slot's <c>+0x60</c>.</param>
/// <param name="OffsetX">E20.f1b: the slot's <c>+0x48</c>.</param>
/// <param name="OffsetY">E20.f1b: the slot's <c>+0x4C</c>.</param>
/// <param name="OffsetZ">E20.f1b: the slot's <c>+0x50</c>.</param>
internal sealed record ChargeCall(int Effect, bool Created, uint RngEntry, int X, int Y, int Z, int ForceX, int ForceY, int SlotIndex = -1, int Mode = 0, int DepthOffset = 0, int OffsetX = 0, int OffsetY = 0, int OffsetZ = 0);

/// <summary>One call of the shot (<c>0x80039F58</c>) the oracle journalled: type, position and direction, in the binary's frame of Z.</summary>
internal sealed record ChargeSpawn(int Type, int X, int Y, int Z, int Direction);
