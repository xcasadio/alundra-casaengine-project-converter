using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using Xunit;
using UiBox = AlundraCasaEngineProjectConverter.Readers.UiBoxLayoutReader.UiBox;
using UiBoxCell = AlundraCasaEngineProjectConverter.Readers.UiBoxLayoutReader.UiBoxCell;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// D3.b of docs/plan-e13d-inventaire.md (D-E13D-13, D-E13D-14): <see cref="UiBoxBaker.Bake"/> on a synthetic
/// 16x16 atlas whose every pixel encodes its own position, so a copied texel tells where it came from.
/// </summary>
public class UiBoxBakerTests
{
    private const int AtlasSize = 16;

    /// <summary>Pixel (u, v) = bytes (u, v, 7, alpha); alpha 0 on the atlas's left column, 255 elsewhere.</summary>
    private static byte[] NewAtlas()
    {
        var pixels = new byte[AtlasSize * AtlasSize * 4];
        for (var v = 0; v < AtlasSize; v++)
        {
            for (var u = 0; u < AtlasSize; u++)
            {
                var i = (v * AtlasSize + u) * 4;
                pixels[i] = (byte)u;
                pixels[i + 1] = (byte)v;
                pixels[i + 2] = 7;
                pixels[i + 3] = u == 0 ? (byte)0 : (byte)255;
            }
        }

        return pixels;
    }

    private static readonly HashSet<(int, int, int, int, int)> Tiles = new()
    {
        (0, 0, 8, 8, 3),
        (8, 0, 8, 8, 3),
        (0, 8, 8, 8, 0),
        (8, 8, 8, 8, 0),
    };

    private static (byte, byte, byte, byte) PixelAt(UiBoxBaker.BakeResult result, int x, int y)
    {
        var i = (y * result.Width + x) * 4;
        return (result.Pixels![i], result.Pixels[i + 1], result.Pixels[i + 2], result.Pixels[i + 3]);
    }

    [Fact]
    public void Bake_CopiesEachCellFromItsTile_ToItsPlaceInTheBox_AlphaIncluded()
    {
        // A 2x1 box at screen (100, 50): cell 0 at x0 108 takes the tile (8, 0), cell 1 at x0 100 the tile (0, 8).
        var box = new UiBox("box", 100, 50, 2, 1);
        var cells = new[]
        {
            new UiBoxCell("box", 0, 108, 50, 8, 0, 8, 8, 3),
            new UiBoxCell("box", 1, 100, 50, 0, 8, 8, 8, 0),
        };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Empty(result.Errors);
        Assert.Equal((16, 8), (result.Width, result.Height));
        Assert.Equal(2, result.CellsBaked);
        // Box pixel (8 + 3, 5) comes from atlas (8 + 3, 0 + 5), opaque.
        Assert.Equal(((byte)11, (byte)5, (byte)7, (byte)255), PixelAt(result, 11, 5));
        // Box pixel (0, 2) comes from atlas (0, 8 + 2): the atlas's left column, alpha 0, copied as is.
        Assert.Equal(((byte)0, (byte)10, (byte)7, (byte)0), PixelAt(result, 0, 2));
        Assert.Equal(((byte)5, (byte)12, (byte)7, (byte)255), PixelAt(result, 5, 4));
    }

    [Fact]
    public void Bake_UncoveredPixels_StayFullyTransparent()
    {
        var box = new UiBox("box", 0, 0, 2, 1);
        var cells = new[] { new UiBoxCell("box", 0, 0, 0, 8, 0, 8, 8, 3) };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Empty(result.Errors);
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), PixelAt(result, 12, 3));
    }

    [Fact]
    public void Bake_EmptyBox_BakesNothing_AndIsNoError()
    {
        var result = UiBoxBaker.Bake(new UiBox("spacer", 240, 112, 0, 0), Array.Empty<UiBoxCell>(), NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Null(result.Pixels);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.CellsBaked);
    }

    [Fact]
    public void Bake_CellWhoseTileIsNotInWindJson_RefusesTheBox()
    {
        var box = new UiBox("box", 0, 0, 1, 1);
        // Same rectangle as a known tile, but another palette: not the same tile.
        var cells = new[] { new UiBoxCell("box", 0, 0, 0, 8, 0, 8, 8, 5) };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Null(result.Pixels);
        Assert.Equal(1, result.CellsWithoutTile);
        Assert.Contains("not in wind.json", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Bake_CellOutsideItsBox_RefusesTheBox()
    {
        var box = new UiBox("box", 100, 50, 1, 1);
        var cells = new[] { new UiBoxCell("box", 0, 104, 50, 0, 0, 8, 8, 3) };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Null(result.Pixels);
        Assert.Contains("outside its 8x8 box", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Bake_OverlappingCells_RefuseTheBox()
    {
        var box = new UiBox("box", 0, 0, 2, 1);
        var cells = new[]
        {
            new UiBoxCell("box", 0, 0, 0, 0, 0, 8, 8, 3),
            new UiBoxCell("box", 1, 4, 0, 8, 0, 8, 8, 3),
        };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, Tiles);

        Assert.Null(result.Pixels);
        Assert.Contains("overlaps", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Bake_TileOutsideTheAtlas_RefusesTheBox()
    {
        var box = new UiBox("box", 0, 0, 1, 1);
        var tiles = new HashSet<(int, int, int, int, int)>(Tiles) { (12, 12, 8, 8, 3) };
        var cells = new[] { new UiBoxCell("box", 0, 0, 0, 12, 12, 8, 8, 3) };

        var result = UiBoxBaker.Bake(box, cells, NewAtlas(), AtlasSize, AtlasSize, tiles);

        Assert.Null(result.Pixels);
        Assert.Contains("outside the 16x16 atlas", Assert.Single(result.Errors), StringComparison.Ordinal);
    }
}
