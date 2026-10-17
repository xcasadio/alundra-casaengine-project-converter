#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;

namespace Alundra.UI.Hud;

/// <summary>E13 C6 (docs/plan-e13-hud.md, "Les fonds des cases arme et accessoire"): one untextured
/// gouraud-shaded background quad, the port of a PSX <c>POLY_G4</c> (<c>Graphics/POLY_G4.cs:3-32</c>) -
/// a rectangle in the SAME native-pixel/tile convention as <see cref="HudTile"/> (the screen
/// multiplies every field but the four colours and <see cref="Alpha"/> by its own integer pixel-scale
/// factor), plus its four independent corner colours and its alpha. Deliberately its OWN record, not an
/// extension of <see cref="HudGlyph"/>/<see cref="HudTile"/> (C6's own mission item 1: "un type
/// d'enregistrement dédié... qui restent les tuiles texturées de la jauge") - a background quad has no
/// glyph/sprite at all, unlike every <see cref="HudTile"/>.</summary>
public readonly record struct HudBackgroundQuad(
    int NativeX, int NativeY, int NativeWidth, int NativeHeight,
    HudRgb TopLeftColor, HudRgb TopRightColor, HudRgb BottomLeftColor, HudRgb BottomRightColor,
    float Alpha);
