using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Bakes one mode-1 ("Tiles") backdrop layer's tile grid into a static 640x480 RGBA PNG,
/// replicating the pixel decode of AlundraEngine.Graphics.ScrollParameters.GetScrollBitmap and the
/// tile addressing of GraphicManager.RenderLayerToBuffer (@ 0x8005B848), for one fixed V-animation
/// frame selected by <c>vAnim</c> (GraphicManager.cs:943,979 - see the class doc on
/// <see cref="Readers.BackdropDocument"/> and docs/plan-e9-backdrops-residus.md D-E9-2). The default
/// <c>vAnim = 0</c> is frame 0 (AnimFrameCounter == 0), byte-identical to every call made before this
/// parameter existed.
///
/// Each of the grid's 40x30 entries is 2 bytes: a tile index (0 means "no tile here", left fully
/// transparent) whose low/high nibble select a 16px column/row in the 256x256 tile sheet, and a
/// palette index (0-7) into PaletteWords. A tile sheet pixel is itself transparent when its 15-bit
/// RGB is zero and its semi-transparency (STP) bit is clear - PSX convention, matching
/// GetScrollBitmap's isTransparentBlack check. Opaque and semi-transparent (STP) pixels are both
/// baked at full alpha into the one exported texture; which BlendMode the whole layer draws with is
/// carried separately in the companion JSON (see BackdropLayerDocument.BlendMode) rather than
/// re-derived per pixel, since a renderer applies one blend state per draw call already.
/// </summary>
public static class BackdropImageBuilder
{
    private const int TileSheetWidth = 256;
    private const int TileSheetStride = TileSheetWidth / 2;

    /// <summary>
    /// Returns null when every tile in the grid is empty (index 0) - an all-transparent PNG would
    /// only waste an asset entry for a layer that draws nothing.
    /// </summary>
    /// <param name="vAnim">
    /// The frame's V offset (GraphicManager.cs:943, <c>vAnim = (AnimFrameCounter &lt;&lt; 8) / AnimNum</c>),
    /// added to each tile's base V before wrapping: <c>V = ((tileVal &amp; 0xF0) + vAnim) &amp; 0xFF</c>
    /// (:979). Defaults to 0 (frame 0), which reproduces exactly what this method computed before the
    /// parameter existed.
    /// </param>
    public static Bitmap? Build(byte[] tileGrid, byte[] tileSheetImageData, ushort[][] paletteWords, int vAnim = 0)
    {
        if (tileGrid.Length == 0 || tileSheetImageData.Length == 0 || paletteWords.Length == 0)
        {
            return null;
        }

        var width = BackdropReader.CanvasWidth;
        var height = BackdropReader.CanvasHeight;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bounds = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            var stride = bitmapData.Stride;
            var pixels = new byte[stride * height];
            var wroteAnyTile = false;

            for (var tileY = 0; tileY < BackdropReader.GridHeightTiles; tileY++)
            {
                var rowOffset = tileY * BackdropReader.GridRowStride;

                for (var tileX = 0; tileX < BackdropReader.GridWidthTiles; tileX++)
                {
                    var entryOffset = rowOffset + (tileX << 1);
                    if (entryOffset + 1 >= tileGrid.Length)
                    {
                        continue;
                    }

                    var tileVal = tileGrid[entryOffset];
                    if (tileVal == 0)
                    {
                        continue;
                    }

                    var palDex = tileGrid[entryOffset + 1];
                    if ((uint)palDex >= (uint)paletteWords.Length)
                    {
                        palDex = 0;
                    }

                    var sheetU = (tileVal & 0x0F) << 4;
                    var sheetV = ((tileVal & 0xF0) + vAnim) & 0xFF;

                    wroteAnyTile |= DrawTile(
                        pixels, stride, tileX * BackdropReader.TileSize, tileY * BackdropReader.TileSize,
                        sheetU, sheetV, tileSheetImageData, paletteWords[palDex]);
                }
            }

            if (!wroteAnyTile)
            {
                return null;
            }

            Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    /// <summary>
    /// A fully transparent 640x480 RGBA bitmap, same size and format as <see cref="Build"/>'s
    /// output. D-E9-4 (docs/plan-e9-backdrops-residus.md): the original still draws animation frame
    /// <c>f &gt;= 1</c> even when every tile in it happens to be empty, so the converter must still
    /// emit a texture for that frame rather than leave a hole in the per-layer frame array.
    /// </summary>
    public static Bitmap CreateTransparentFrame()
    {
        var width = BackdropReader.CanvasWidth;
        var height = BackdropReader.CanvasHeight;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
        }

        return bitmap;
    }

    /// <summary>
    /// Bakes the WHOLE 256x256 per-map tile sheet for one palette, rather than an atlas of only the
    /// rectangles a Cellular (mode 2) layer's cells reference. A cell samples its source window at
    /// <c>(V0 + phase) &amp; 0xFF</c> at runtime (GraphicManager.RenderLayerToBuffer, @
    /// 0x8005B848), so the window scrolls and WRAPS modulo 256 as the phase advances - baking the
    /// full sheet once absorbs that wrap with no UV remapping at all, whereas an atlas of only the
    /// cells' own rectangles would have to re-wrap and re-tile every frame. The sheet and its 8
    /// palettes are shared by both layers of a map (<see cref="Readers.BackdropReadResult.TileSheetImageData"/>/
    /// <see cref="Readers.BackdropReadResult.PaletteWords"/>), so one sheet per used palette is baked
    /// at document level and shared by every Cellular cell that references that palette, regardless
    /// of which layer it belongs to.
    /// </summary>
    /// <returns>Null when the palette decodes to no visible pixel at all - same rule as <see cref="Build"/>,
    /// so a fully transparent palette costs no asset.</returns>
    public static Bitmap? BuildTileSheet(byte[] tileSheetImageData, ushort[] palette)
    {
        if (tileSheetImageData.Length == 0 || palette.Length == 0)
        {
            return null;
        }

        var bitmap = new Bitmap(TileSheetWidth, TileSheetWidth, PixelFormat.Format32bppArgb);
        var bounds = new Rectangle(0, 0, TileSheetWidth, TileSheetWidth);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            var stride = bitmapData.Stride;
            var pixels = new byte[stride * TileSheetWidth];

            var wroteAnyPixel = DrawTile(
                pixels, stride, destX: 0, destY: 0, sheetU: 0, sheetV: 0,
                tileSheetImageData, palette, width: TileSheetWidth, height: TileSheetWidth);

            if (!wroteAnyPixel)
            {
                return null;
            }

            Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    private static bool DrawTile(
        byte[] pixels, int stride, int destX, int destY, int sheetU, int sheetV,
        byte[] tileSheetImageData, ushort[] palette,
        int width = BackdropReader.TileSize, int height = BackdropReader.TileSize)
    {
        var wroteAnyPixel = false;

        for (var y = 0; y < height; y++)
        {
            var sourceY = (sheetV + y) & 0xFF;

            for (var x = 0; x < width; x++)
            {
                var sourceX = (sheetU + x) & 0xFF;
                var sourceIndex = sourceY * TileSheetStride + (sourceX >> 1);
                if ((uint)sourceIndex >= (uint)tileSheetImageData.Length)
                {
                    continue;
                }

                var packed = tileSheetImageData[sourceIndex];
                var paletteDex = (sourceX & 1) == 0 ? packed & 0x0F : (packed >> 4) & 0x0F;
                var paletteWord = palette[paletteDex];
                var stp = (paletteWord & 0x8000) != 0;
                var isTransparentBlack = (paletteWord & 0x7FFF) == 0 && !stp;

                if (isTransparentBlack)
                {
                    continue;
                }

                var color = FromPsxColor(paletteWord);
                var pixelIndex = (destY + y) * stride + (destX + x) * 4;

                pixels[pixelIndex + 0] = color.B;
                pixels[pixelIndex + 1] = color.G;
                pixels[pixelIndex + 2] = color.R;
                pixels[pixelIndex + 3] = 255;
                wroteAnyPixel = true;
            }
        }

        return wroteAnyPixel;
    }

    // Mirrors AlundraEngine.Graphics.ImageHelper.FromPsxColor(int): 5 bits per channel, in BGR555
    // order (bit 15 is the STP flag, not alpha - alpha/transparency is handled separately above);
    // red occupies the LOW bits and blue the HIGH bits of the 16-bit palette word.
    private static Color FromPsxColor(int paletteWord)
    {
        var r = (paletteWord & 0x1F) << 3;
        var g = ((paletteWord >> 5) & 0x1F) << 3;
        var b = ((paletteWord >> 10) & 0x1F) << 3;
        return Color.FromArgb(255, r, g, b);
    }
}
