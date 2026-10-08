using System.Drawing;
using Xunit;
using Xunit.Abstractions;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Guards ui/font3.png decoded with the WIND.CL palette entry 8 (docs/plan-e19-opcodes.md section 1.2w.2,
/// E19.L-b1, LB1-R2, O-E19-66): the binary draws the text bands and the cursor of font3 with entry 8 of the CLUT
/// table it fills from WIND.CL, in which colour index 4 is (74, 82, 57) where FONT3.TIM's own palette 0 has
/// (82, 90, 57). Exactly 44 texels of the page take that colour. Runs on the real data-extracted/ content and
/// returns early when it is not present next to the built test binaries.
/// </summary>
public class Font3WindClutTests
{
    private const int WindClutIndex4Texels = 44;

    private readonly ITestOutputHelper _output;

    public Font3WindClutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // F-1: 44 texels at (74, 82, 57, 255) and none at (82, 90, 57, 255).
    [Fact]
    public void Font3Page_UsesTheWindClutColourForIndex4()
    {
        var path = FindFont3();
        if (path is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var windClut = 0;
        var timPalette = 0;
        using var bitmap = new Bitmap(path);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A != 255)
                {
                    continue;
                }

                if (pixel.R == 74 && pixel.G == 82 && pixel.B == 57)
                {
                    windClut++;
                }
                else if (pixel.R == 82 && pixel.G == 90 && pixel.B == 57)
                {
                    timPalette++;
                }
            }
        }

        Assert.Equal(0, timPalette);
        Assert.Equal(WindClutIndex4Texels, windClut);
    }

    private static string? FindFont3()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "ui", "font3.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
