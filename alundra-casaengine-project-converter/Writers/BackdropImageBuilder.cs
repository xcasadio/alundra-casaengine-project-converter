using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Bakes one mode-1 ("Tiles") backdrop layer's tile grid into a static 640x480 RGBA PNG,
/// replicating the pixel decode of AlundraEngine.Graphics.ScrollParameters.GetScrollBitmap and the
/// tile addressing of GraphicManager.RenderLayerToBuffer (@ 0x8005B848), for a single fixed
/// animation frame (AnimFrameCounter == 0, i.e. no per-tile vertical animation offset - see the
/// deferred-items note on <see cref="Readers.BackdropDocument"/>).
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
    public static Bitmap? Build(byte[] tileGrid, byte[] tileSheetImageData, ushort[][] paletteWords)
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
                    var sheetV = tileVal & 0xF0;

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

    private static bool DrawTile(
        byte[] pixels, int stride, int destX, int destY, int sheetU, int sheetV,
        byte[] tileSheetImageData, ushort[] palette)
    {
        var wroteAnyPixel = false;

        for (var y = 0; y < BackdropReader.TileSize; y++)
        {
            var sourceY = (sheetV + y) & 0xFF;

            for (var x = 0; x < BackdropReader.TileSize; x++)
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
    // order (bit 15 is the STP flag, not alpha - alpha/transparency is handled separately above).
    private static Color FromPsxColor(int paletteWord)
    {
        var r = (paletteWord & (0x1F << 10)) >> 7;
        var g = (paletteWord & (0x1F << 5)) >> 2;
        var b = (paletteWord & 0x1F) << 3;
        return Color.FromArgb(255, r, g, b);
    }
}
