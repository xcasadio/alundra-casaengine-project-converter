using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 7, after <see cref="UiWriter"/>: the main inventory's background boxes, one baked image each -
/// docs/plan-e13d-inventaire.md, slice D3.b, decisions D-E13D-13 and D-E13D-14.
///
/// Reads the box layout the analyser exports (<see cref="UiBoxLayoutReader"/>, <c>UiBoxes.csv</c> and
/// <c>UiBoxCells.csv</c>, linked into this project) and the same inputs <see cref="UiWriter"/> reads,
/// <c>ui/wind.png</c> and <c>ui/wind.json</c>; bakes each non-empty box with <see cref="UiBoxBaker"/>; then,
/// for each box, writes <c>UI/Textures/&lt;box&gt;.png</c> and its <c>.texture</c> wrapper through
/// <see cref="TextureAssetWriter.EnsureTexture"/> and a <c>UI/&lt;box&gt;.sprite</c> covering the whole
/// image, id <c>Ids.For("sprite-ui:" + box)</c>, named after the box's variable in the decompilation.
/// The inventory screen places each image at the box's own screen position and slides it on its own.
///
/// A refused box is reported as errors and emits nothing; a missing input is one error and emits nothing.
/// <see cref="UiWriter.ConvertUi"/> has already saved the catalog when this runs, so this saves it again,
/// like <see cref="BackdropWriter"/>. The PNG is encoded with System.Drawing, like the backdrops, whose
/// double export proved the encoding deterministic.
/// </summary>
public static class UiBoxWriter
{
    private const string UiRelativeDirectory = "UI";
    private static readonly string UiTexturesRelativeDirectory = Path.Combine("UI", "Textures");

    public static void ConvertUiBoxes(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var boxesCsvPath = Path.Combine(AppContext.BaseDirectory, "UiBoxes.csv");
        var cellsCsvPath = Path.Combine(AppContext.BaseDirectory, "UiBoxCells.csv");
        var windJsonPath = Path.Combine(inputDirectory, "ui", "wind.json");
        var windPngPath = Path.Combine(inputDirectory, "ui", "wind.png");

        foreach (var path in new[] { boxesCsvPath, cellsCsvPath, windJsonPath, windPngPath })
        {
            if (!File.Exists(path))
            {
                report.Errors.Add($"UI boxes: '{path}' not found; no inventory box baked.");
                return;
            }
        }

        var layout = UiBoxLayoutReader.Read(boxesCsvPath, cellsCsvPath);
        foreach (var warning in layout.Warnings)
        {
            report.Warnings.Add(warning);
        }

        var atlasTiles = UiSpriteReader.Read(windJsonPath)
            .Select(entry => (entry.U0, entry.V0, entry.Width, entry.Height, entry.PaletteIndex))
            .ToHashSet();
        var (atlasPixels, atlasWidth, atlasHeight) = ReadArgbPixels(windPngPath);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineUiBoxBake", Guid.NewGuid().ToString("N"));
        var textureCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var box in layout.Boxes)
            {
                var cells = layout.Cells.Where(cell => cell.Box == box.Name).ToList();
                var result = UiBoxBaker.Bake(box, cells, atlasPixels, atlasWidth, atlasHeight, atlasTiles);
                report.Increment("UiBoxes.CellsWithoutTile", result.CellsWithoutTile);

                foreach (var error in result.Errors)
                {
                    report.Errors.Add(error);
                }

                if (result.Pixels is null)
                {
                    continue; // the empty spacer, or a refused box
                }

                Directory.CreateDirectory(tempDirectory);
                var tempPngPath = Path.Combine(tempDirectory, box.Name + ".png");
                WriteArgbPng(tempPngPath, result.Pixels, result.Width, result.Height);

                var textureAssetId = TextureAssetWriter.EnsureTexture(
                    tempPngPath, UiTexturesRelativeDirectory, outputDirectory, textureCache);

                var spriteData = new SpriteData(Ids.For("sprite-ui:" + box.Name))
                {
                    SpriteSheetAssetId = textureAssetId,
                    PositionInTexture = new Microsoft.Xna.Framework.Rectangle(0, 0, result.Width, result.Height),
                    Origin = new Microsoft.Xna.Framework.Point(result.Width / 2, result.Height / 2),
                    Name = box.Name,
                };
                spriteData.FileName = Path.Combine(UiRelativeDirectory, $"{spriteData.Name}.sprite");

                EditorAssetWriterService.SaveAsset(spriteData.FileName, spriteData);
                EditorAssetCatalogService.Add(new AssetInfo(spriteData.Id)
                {
                    Name = spriteData.Name,
                    FileName = spriteData.FileName,
                });

                report.Increment("UiBoxes.Boxes");
                report.Increment("UiBoxes.Cells", result.CellsBaked);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        EditorAssetCatalogService.Save();
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

    private static void WriteArgbPng(string pngPath, byte[] pixels, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        bitmap.Save(pngPath, ImageFormat.Png);
    }
}
