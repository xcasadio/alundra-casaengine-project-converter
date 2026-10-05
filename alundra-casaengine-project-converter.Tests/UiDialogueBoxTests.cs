using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.f1 of docs/plan-e19-opcodes.md: the dialogue name box (<c>g_textTilesConfiguration</c>, 112 x 32) and
/// the choice box (<c>g_uiBoxesConfigurationBackgroundMessageChoice</c>, 128 x 32), baked by
/// <see cref="UiBoxWriter"/> from the real linked <c>UiBoxes.csv</c>/<c>UiBoxCells.csv</c> and the real
/// <c>data-extracted/ui/wind.png</c>, compared byte for byte, alpha included, with two reference PNGs.
/// The references (TestData/) come from a script that does not read the CSVs: it reads the arrays of the
/// decompilation's StaticVariables.cs, starts from a fully transparent canvas and copies the raw RGBA of each
/// 8 x 8 tile of the atlas to the cell's place, with the box position as origin (the name box at (64, 140), where
/// the binary draws it, not at the stale raw y0 144 to 168 of its array).
/// Skips when data-extracted/ is absent (it is not versioned), like the other tests on real data.
/// </summary>
public class UiDialogueBoxTests
{
    [Theory]
    [InlineData("g_textTilesConfiguration", "dialogue_name_box_reference.png", 112, 32)]
    [InlineData("g_uiBoxesConfigurationBackgroundMessageChoice", "dialogue_choice_box_reference.png", 128, 32)]
    public void ConvertUiBoxes_BakesTheDialogueBoxes_ByteForByteLikeTheIndependentReference(
        string box, string referenceFileName, int width, int height)
    {
        var realUiDirectory = FindRealUiDirectory();
        if (realUiDirectory is null)
        {
            return; // data-extracted/ not present in this environment; nothing to assert against.
        }

        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var inputUiDirectory = Path.Combine(inputDirectory, "ui");
            Directory.CreateDirectory(inputUiDirectory);
            File.Copy(Path.Combine(realUiDirectory, "wind.png"), Path.Combine(inputUiDirectory, "wind.png"));
            File.Copy(Path.Combine(realUiDirectory, "wind.json"), Path.Combine(inputUiDirectory, "wind.json"));

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            UiBoxWriter.ConvertUiBoxes(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(0, report.Counters["UiBoxes.CellsWithoutTile"]);

            var bakedPath = Path.Combine(outputDirectory, "UI", "Textures", box + ".png");
            Assert.True(File.Exists(bakedPath), bakedPath);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", box + ".sprite")));

            var (baked, bakedWidth, bakedHeight) = ReadArgbPixels(bakedPath);
            var (reference, referenceWidth, referenceHeight) =
                ReadArgbPixels(Path.Combine(AppContext.BaseDirectory, "TestData", referenceFileName));

            Assert.Equal((width, height), (referenceWidth, referenceHeight));
            Assert.Equal((width, height), (bakedWidth, bakedHeight));
            Assert.True(reference.AsSpan().SequenceEqual(baked), $"{box}: first differing byte at {FirstDifference(reference, baked, width)}");
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void FirstDifference_LocatesThePixelInTheBoxWidth()
    {
        var expected = new byte[112 * 32 * 4]; // the name box is 112 wide: byte 470 is pixel (5, 1), channel 2.
        var actual = (byte[])expected.Clone();
        actual[470] = 1;

        Assert.Equal("470 (pixel 5, 1, channel 2): expected 0, got 1", FirstDifference(expected, actual, 112));
    }

    private static string FirstDifference(byte[] expected, byte[] actual, int width)
    {
        for (var i = 0; i < Math.Min(expected.Length, actual.Length); i++)
        {
            if (expected[i] != actual[i])
            {
                return $"{i} (pixel {i / 4 % width}, {i / 4 / width}, channel {i % 4}): expected {expected[i]}, got {actual[i]}";
            }
        }

        return "none (lengths differ)";
    }

    /// <summary>The PNG's pixels as 32bpp ARGB (System.Drawing's byte order), rows packed without padding.</summary>
    private static (byte[] Pixels, int Width, int Height) ReadArgbPixels(string pngPath)
    {
        using var bitmap = new Bitmap(pngPath);
        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new byte[width * height * 4];
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 4, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return (pixels, width, height);
    }

    private static string? FindRealUiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "ui");
            if (File.Exists(Path.Combine(candidate, "wind.png")) && File.Exists(Path.Combine(candidate, "wind.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
