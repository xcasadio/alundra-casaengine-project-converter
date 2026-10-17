#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;

namespace Alundra.UI.Hud;

/// <summary>E13.c S3 (docs/plan-e13c-icones-hud.md): one equipment icon - the <c>.sprite</c> asset that is an
/// item's portrait - and the box it is drawn in, as a NATIVE pixel rectangle, same convention as
/// <see cref="HudTile"/>. Its own record rather than a new <see cref="HudGlyph"/>: the 24 glyphs are a
/// fixed set loaded once, an icon is whichever item is equipped, known only as an asset id. Its size is not
/// carried here - it is the sprite's own, as the original takes <c>Swidth</c>/<c>Sheight</c> straight from
/// the image (GraphicManager.cs:1933-1934) - so the screen, which loads the sprite, centres it in the box
/// through <see cref="ScreenLeft"/> and <see cref="ScreenTop"/>. The original draws the icon's top-left at
/// the box's top-left instead; the author's D-E13D-10 (2026-09-21, docs/plan-e13d-inventaire.md) centres
/// every icon in its parent box rather than matching the original to the pixel.</summary>
public readonly record struct HudIcon(
    Guid AssetId, int BoxNativeX, int BoxNativeY, int BoxNativeWidth, int BoxNativeHeight)
{
    /// <summary>The icon's left edge in screen pixels, centred in its box: the box's own left edge scaled
    /// like every tile, plus half the scaled free width. Halved in screen pixels, not native ones, so the one
    /// free native pixel of a 23-wide icon in the 24-wide box is still split evenly at any even scale; at an
    /// odd scale the extra screen pixel goes to the right.</summary>
    public int ScreenLeft(int iconNativeWidth, int pixelScale) =>
        BoxNativeX * pixelScale + (BoxNativeWidth - iconNativeWidth) * pixelScale / 2;

    /// <summary>The icon's top edge in screen pixels, centred in its box the same way as
    /// <see cref="ScreenLeft"/>.</summary>
    public int ScreenTop(int iconNativeHeight, int pixelScale) =>
        BoxNativeY * pixelScale + (BoxNativeHeight - iconNativeHeight) * pixelScale / 2;
}
