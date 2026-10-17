#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;

namespace Alundra.UI.Hud;

/// <summary>One drawn tile: which glyph, at which NATIVE (320x240, unscaled) pixel position. The screen
/// (E13 C2) multiplies both coordinates by the integer pixel-scale factor before handing them to MGUI -
/// this struct itself carries no notion of scale.</summary>
public readonly record struct HudTile(HudGlyph Glyph, int NativeX, int NativeY);
