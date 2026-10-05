using Alundra.Scripts;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using System.IO.Compression;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.g G2c (rules G2c-R1 and G2c-R3, D-E19-68, ADR-0035): the backdrop layers of the real companions get the PSX mode of their
/// <c>BlendMode</c> (an opaque white layer: the mode alone decides), and the PNG sheets of the converter's export carry the STP texels
/// at alpha 128. The tests on <c>alundra-project/</c> FAIL, rather than skip, when it is absent (the local convention of
/// <see cref="BackdropStageDefinitionTests"/>); the three pixel reads are the guard that fails loudly on a stale export (the PNGs of
/// before G2c have no alpha 128: they would draw the sea at a quarter and the additive layers opaque).
/// </summary>
public class BackdropStagePsxSemiTransparencyTests
{
    // -----------------------------------------------------------------------------------------
    // Real companions.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RealMap391_CellularRain_IsMode0_OpaqueWhite()
    {
        var layers = AlundraBackdropStage.BuildCellularDefinitions(LoadRealCompanion("Ship Klark (night, break, Event)-391"));

        var layer = Assert.Single(layers);
        Assert.Equal(SpritePsxSemiTransparency.Mode0, layer.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, layer.Blend);
        Assert.Equal(Color.White, layer.Tint);
    }

    [Fact]
    public void RealMap44_Layer1Tiles_IsMode0_AndLayer0Cellular_IsMode1()
    {
        var document = LoadRealCompanion("Wendels Nightmare-44");

        var (scrolling, _, _) = AlundraBackdropStage.BuildDefinitions(document);
        var cellular = AlundraBackdropStage.BuildCellularDefinitions(document);

        var tiles = Assert.Single(scrolling);
        Assert.Equal(1, tiles.StableId); // layer 1, whose Ground is false: the pass stays Background
        Assert.Equal(RenderPass2D.Background, tiles.Pass);
        Assert.Equal(SpritePsxSemiTransparency.Mode0, tiles.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, tiles.Blend);
        Assert.Equal(Color.White, tiles.Tint);

        var cells = Assert.Single(cellular);
        Assert.Equal(0, cells.LayerId);
        Assert.Equal(SpritePsxSemiTransparency.Mode1, cells.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, cells.Blend);
    }

    [Fact]
    public void RealMap389_TheSeaLayer_IsMode0()
    {
        var (layers, _, _) = AlundraBackdropStage.BuildDefinitions(LoadRealCompanion("Ship Klark (beginning)-389"));

        var layer = Assert.Single(layers);
        Assert.Equal(SpritePsxSemiTransparency.Mode0, layer.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, layer.Blend);
        Assert.Equal(Color.White, layer.Tint);
    }

    [Fact]
    public void RealMap321_TheLayerWithoutBlend_IsNone()
    {
        var (layers, _, _) = AlundraBackdropStage.BuildDefinitions(LoadRealCompanion("Arena Zorgia (Boss)-321"));

        var layer = Assert.Single(layers);
        Assert.Equal(SpritePsxSemiTransparency.None, layer.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, layer.Blend);
    }

    // -----------------------------------------------------------------------------------------
    // Guard on the real export: the PNG sheets were exported by the converter of G2c.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ExportedSheets_CarryTheStpTexelsAtAlpha128_AndTheOtherTexelsAtAlpha255()
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();

        // The sea of map 389, layer 0: the texel (46, 23) has the colour word 0x94C5, whose bit 15 is set.
        Assert.Equal(128, AlphaAt(projectRoot, "Ship Klark (beginning)-389-layer0.png", 46, 23));

        // The first cell sheet of map 476 (the wave bands), texel (0, 0): the word 0xB18C.
        Assert.Equal(128, AlphaAt(projectRoot, "Lars & Melzas Room (beginning Event)-476-cellsheet0.png", 0, 0));

        // The rain of map 391, texel (2, 0): the word 0x2D8C has no STP bit, the rain is opaque in the original.
        Assert.Equal(255, AlphaAt(projectRoot, "Ship Klark (night, break, Event)-391-cellsheet0.png", 2, 0));
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    private static BackdropDocument LoadRealCompanion(string worldName)
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
        var document = BackdropLoader.Load(projectRoot, worldName);
        Assert.NotNull(document);
        return document!;
    }

    private static int AlphaAt(string projectRoot, string fileName, int x, int y)
    {
        var matches = Directory.GetFiles(Path.Combine(projectRoot, "Maps"), fileName, SearchOption.AllDirectories);
        Assert.True(matches.Length == 1, $"Expected exactly one '{fileName}' under alundra-project/Maps, found {matches.Length}.");

        return ReadPngAlpha(File.ReadAllBytes(matches[0]), x, y);
    }

    /// <summary>
    /// Alpha of one pixel of an 8-bit RGBA, non-interlaced PNG (the format the converter writes), decoded by hand: the test
    /// project has no image library and must not load the file through a graphics device.
    /// </summary>
    private static int ReadPngAlpha(byte[] png, int x, int y)
    {
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);

        var offset = 8;
        var width = 0;
        var height = 0;
        using var compressed = new MemoryStream();
        while (offset < png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = offset + 8;
            if (type == "IHDR")
            {
                width = (png[data] << 24) | (png[data + 1] << 16) | (png[data + 2] << 8) | png[data + 3];
                height = (png[data + 4] << 24) | (png[data + 5] << 16) | (png[data + 6] << 8) | png[data + 7];
                Assert.Equal(8, png[data + 8]); // bit depth
                Assert.Equal(6, png[data + 9]); // colour type: RGBA
                Assert.Equal(0, png[data + 12]); // not interlaced
            }
            else if (type == "IDAT")
            {
                compressed.Write(png, data, length);
            }

            offset = data + length + 4;
        }

        compressed.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        var stride = width * 4;
        var previous = new byte[stride];
        var current = new byte[stride];
        for (var row = 0; row <= y; row++)
        {
            var filter = raw[row * (stride + 1)];
            Array.Copy(raw, row * (stride + 1) + 1, current, 0, stride);
            for (var i = 0; i < stride; i++)
            {
                var left = i >= 4 ? current[i - 4] : 0;
                var up = previous[i];
                var upLeft = i >= 4 ? previous[i - 4] : 0;
                current[i] = (byte)(current[i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"PNG filter {filter}"),
                });
            }

            if (row < y)
            {
                (previous, current) = (current, previous);
            }
        }

        Assert.True(x < width && y < height);
        return current[x * 4 + 3];
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
