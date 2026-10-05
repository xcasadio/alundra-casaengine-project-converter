using System.Drawing;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.g G2c (rule G2c-R1, D-E19-68): the backdrop sheets are the content of the PSX video memory, so a texel whose colour word
/// has the semi-transparency bit (STP, bit 15) is baked at alpha 128, any other drawn texel at alpha 255 and the word 0x0000 at
/// alpha 0 (not drawn). The RGB is unchanged. Which blend a layer draws with stays in its companion document: the mode of the
/// layer only applies to the STP texels (the engine draws the two groups in two passes).
/// </summary>
public class BackdropStpAlphaTests
{
    private const int TileIndex = 0x11;
    private const int SheetU = 16;
    private const int SheetV = 16;
    private const int PaletteEntry = 2;

    // ---- synthetic: one tile of one palette entry ----

    [Theory]
    [InlineData((ushort)0x83E0, 128, 0, 248, 0)] // STP green: alpha 128 (was 255)
    [InlineData((ushort)0x03E0, 255, 0, 248, 0)] // not STP: alpha 255
    [InlineData((ushort)0x8000, 128, 0, 0, 0)] // STP black is drawn, at alpha 128 (was 255)
    public void Build_BakesTheStpBitAsAlpha128_AndAnyOtherDrawnTexelAsAlpha255(ushort word, int alpha, int red, int green, int blue)
    {
        using var bitmap = BackdropImageBuilder.Build(BuildTileGrid(), BuildTileSheet(), BuildPaletteWords(word));

        Assert.NotNull(bitmap);
        Assert.Equal(Color.FromArgb(alpha, red, green, blue), bitmap!.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(alpha, red, green, blue), bitmap.GetPixel(15, 15));
    }

    [Theory]
    [InlineData((ushort)0x83E0, 128, 0, 248, 0)]
    [InlineData((ushort)0x03E0, 255, 0, 248, 0)]
    [InlineData((ushort)0x8000, 128, 0, 0, 0)]
    public void BuildTileSheet_BakesTheStpBitAsAlpha128_AndAnyOtherDrawnTexelAsAlpha255(ushort word, int alpha, int red, int green, int blue)
    {
        using var sheet = BackdropImageBuilder.BuildTileSheet(BuildTileSheet(), BuildPaletteWords(word)[0]);

        Assert.NotNull(sheet);
        Assert.Equal(Color.FromArgb(alpha, red, green, blue), sheet!.GetPixel(SheetU, SheetV));
        Assert.Equal(Color.FromArgb(alpha, red, green, blue), sheet.GetPixel(SheetU + 15, SheetV + 15));
    }

    [Fact]
    public void Build_TheWordZero_IsNotDrawn_AndStaysAlpha0()
    {
        // The tile also carries a second texel (nibble 3) that resolves to the word 0x0000: it must stay transparent.
        var sheetBytes = BuildTileSheet();
        sheetBytes[SheetV * 128 + (SheetU >> 1)] = (byte)(3 | (PaletteEntry << 4));
        var palettes = BuildPaletteWords(0x83E0);

        using var bitmap = BackdropImageBuilder.Build(BuildTileGrid(), sheetBytes, palettes);

        Assert.NotNull(bitmap);
        Assert.Equal(0, bitmap!.GetPixel(0, 0).A);
        Assert.Equal(Color.FromArgb(128, 0, 248, 0), bitmap.GetPixel(1, 0));
    }

    // ---- real data (skipped, like the neighbouring tests, when data-extracted/ is not next to the binaries) ----

    [Fact]
    public void RealMap44_Layer1_StpWord0x8C83_IsAlpha128_AndTheNeighbourWord0x0020_IsAlpha255()
    {
        var path = FindRealDataFile("map_44.json");
        if (path is null)
        {
            return;
        }

        using var bitmap = BuildTilesLayer(path, 44, layerId: 1);

        Assert.Equal(Color.FromArgb(128, 24, 32, 24), bitmap.GetPixel(79, 38));
        Assert.Equal(Color.FromArgb(255, 0, 8, 0), bitmap.GetPixel(78, 34));
    }

    [Fact]
    public void RealMap389_Layer0_StpWord0x94C5_IsAlpha128()
    {
        var path = FindRealDataFile("map_389.json");
        if (path is null)
        {
            return;
        }

        using var bitmap = BuildTilesLayer(path, 389, layerId: 0);

        Assert.Equal(Color.FromArgb(128, 40, 48, 40), bitmap.GetPixel(46, 23));
    }

    [Fact]
    public void RealMap476_Cellsheet0_StpWord0xB18C_IsAlpha128()
    {
        var path = FindRealDataFile("map_476.json");
        if (path is null)
        {
            return;
        }

        using var sheet = BuildCellSheet(path, 476, palette: 0);

        Assert.Equal(Color.FromArgb(128, 96, 96, 96), sheet.GetPixel(0, 0));
    }

    [Theory]
    [InlineData("map_391.json", 391)]
    [InlineData("map_31.json", 31)]
    public void RealRainMaps_Cellsheet0_Word0x2D8C_StaysAlpha255(string fileName, int mapIndex)
    {
        var path = FindRealDataFile(fileName);
        if (path is null)
        {
            return;
        }

        using var sheet = BuildCellSheet(path, mapIndex, palette: 0);

        Assert.Equal(Color.FromArgb(255, 96, 96, 88), sheet.GetPixel(2, 0));
    }

    // ---- helpers ----

    private static Bitmap BuildTilesLayer(string mapPath, int mapIndex, int layerId)
    {
        var result = BackdropReader.Read(mapPath, mapIndex);
        Assert.True(result.LayerIsTiles[layerId], $"map {mapIndex} layer {layerId} is not a Tiles layer.");
        var grid = BackdropReader.ExtractTileGrid(result, layerId);
        Assert.NotNull(grid);
        var bitmap = BackdropImageBuilder.Build(grid!, result.TileSheetImageData, result.PaletteWords);
        Assert.NotNull(bitmap);
        return bitmap!;
    }

    private static Bitmap BuildCellSheet(string mapPath, int mapIndex, int palette)
    {
        var result = BackdropReader.Read(mapPath, mapIndex);
        var sheet = BackdropImageBuilder.BuildTileSheet(result.TileSheetImageData, result.PaletteWords[palette]);
        Assert.NotNull(sheet);
        return sheet!;
    }

    private static byte[] BuildTileGrid()
    {
        var grid = new byte[BackdropReader.GridRowStride * BackdropReader.GridHeightTiles];
        grid[0] = TileIndex;
        grid[1] = 0;
        return grid;
    }

    /// <summary>256x256 4bpp tile sheet, all nibble 0 except the 16x16 block at (SheetU, SheetV), which is nibble PaletteEntry.</summary>
    private static byte[] BuildTileSheet()
    {
        const int stride = 128;
        var sheet = new byte[stride * 256];

        for (var y = SheetV; y < SheetV + 16; y++)
        {
            for (var x = SheetU; x < SheetU + 16; x += 2)
            {
                sheet[y * stride + (x >> 1)] = (byte)(PaletteEntry | (PaletteEntry << 4));
            }
        }

        return sheet;
    }

    private static ushort[][] BuildPaletteWords(ushort word)
    {
        var palettes = new ushort[8][];
        for (var i = 0; i < 8; i++)
        {
            palettes[i] = new ushort[16];
        }

        palettes[0][PaletteEntry] = word;
        return palettes;
    }

    private static string? FindRealDataFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "data", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
