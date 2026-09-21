using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Bakes one of the main inventory's background boxes into a single image - docs/plan-e13d-inventaire.md,
/// slice D3.b, decisions D-E13D-13 (one baked image per box, the author's choice) and D-E13D-14 (from the
/// box's <c>SpritesA</c> copy alone). Pure: bytes in, bytes out, no file and no image library, so every rule
/// below is testable on a synthetic atlas.
///
/// The original lays each box out cell by cell: <c>DisplayUiBoxes</c> (<c>0x80055d78</c>,
/// <c>MainInventoryManager.cs:1227-1265</c>) adds one 8x8 <c>SPRT</c> per cell to the ordering table, each
/// set up with <c>SetSemiTrans(sprite, 0)</c> and <c>SetShadeTex(sprite, 1)</c>
/// (<c>GraphicManager.cs:1858-1860</c>): no semi-transparency, no colour modulation. The cells of one copy
/// never overlap (measured, plan §1.4), so there is nothing to blend: a cell's texels are copied as they
/// are, alpha included, and the atlas's alpha 0 stays the transparency that lets the scene show through.
///
/// A box is refused - no image, one error per offending cell - when a cell's tile is not one the atlas
/// holds (its <c>(u0, v0, w, h, clut)</c> has no equal <c>(U0, V0, Width, Height, PaletteIndex)</c> entry
/// in <c>wind.json</c>), when it falls outside the box or outside the atlas, or when it overlaps another
/// cell. A 0 by 0 box, the spacer, has nothing to bake and is not an error.
///
/// Pixels are 4 bytes each, row after row with no padding, in whatever channel order the caller's atlas
/// uses: they are copied, never interpreted. The image starts all zero, which is fully transparent in any
/// channel order.
/// </summary>
public static class UiBoxBaker
{
    public const int CellSize = 8;
    private const int BytesPerPixel = 4;

    /// <param name="Pixels">The baked image, or null when the box is empty or refused.</param>
    /// <param name="CellsWithoutTile">Cells refused because their tile is not in the atlas.</param>
    public sealed record BakeResult(
        byte[]? Pixels, int Width, int Height, int CellsBaked, int CellsWithoutTile, IReadOnlyList<string> Errors);

    public static BakeResult Bake(
        UiBoxLayoutReader.UiBox box,
        IReadOnlyList<UiBoxLayoutReader.UiBoxCell> cells,
        byte[] atlasPixels,
        int atlasWidth,
        int atlasHeight,
        ISet<(int U0, int V0, int Width, int Height, int PaletteIndex)> atlasTiles)
    {
        var width = box.Width * CellSize;
        var height = box.Height * CellSize;
        if (width == 0 || height == 0)
        {
            return new BakeResult(null, width, height, 0, 0, Array.Empty<string>());
        }

        var errors = new List<string>();
        var pixels = new byte[width * height * BytesPerPixel];
        var covered = new bool[width * height];
        var baked = 0;
        var withoutTile = 0;

        foreach (var cell in cells)
        {
            var where = $"UI box '{box.Name}' cell {cell.Cell}";

            if (!atlasTiles.Contains((cell.U0, cell.V0, cell.W, cell.H, cell.Clut)))
            {
                errors.Add($"{where}: tile ({cell.U0}, {cell.V0}, {cell.W}x{cell.H}, palette {cell.Clut}) is not in wind.json.");
                withoutTile++;
                continue;
            }

            if (cell.W <= 0 || cell.H <= 0
                || cell.U0 < 0 || cell.V0 < 0 || cell.U0 + cell.W > atlasWidth || cell.V0 + cell.H > atlasHeight)
            {
                errors.Add($"{where}: tile ({cell.U0}, {cell.V0}, {cell.W}x{cell.H}) lies outside the {atlasWidth}x{atlasHeight} atlas.");
                continue;
            }

            var left = cell.X0 - box.X;
            var top = cell.Y0 - box.Y;
            if (left < 0 || top < 0 || left + cell.W > width || top + cell.H > height)
            {
                errors.Add($"{where}: lands at ({left}, {top}), outside its {width}x{height} box.");
                continue;
            }

            var overlaps = false;
            for (var y = 0; y < cell.H && !overlaps; y++)
            {
                for (var x = 0; x < cell.W; x++)
                {
                    if (covered[(top + y) * width + left + x])
                    {
                        overlaps = true;
                        break;
                    }
                }
            }

            if (overlaps)
            {
                errors.Add($"{where}: overlaps another cell of the box.");
                continue;
            }

            for (var y = 0; y < cell.H; y++)
            {
                Buffer.BlockCopy(
                    atlasPixels, ((cell.V0 + y) * atlasWidth + cell.U0) * BytesPerPixel,
                    pixels, ((top + y) * width + left) * BytesPerPixel,
                    cell.W * BytesPerPixel);
                for (var x = 0; x < cell.W; x++)
                {
                    covered[(top + y) * width + left + x] = true;
                }
            }

            baked++;
        }

        return errors.Count > 0
            ? new BakeResult(null, width, height, baked, withoutTile, errors)
            : new BakeResult(pixels, width, height, baked, withoutTile, errors);
    }
}
