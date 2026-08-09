using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class FontWriterTests
{
    // The writer copies the PNG verbatim and never decodes it; the 8-byte signature is enough.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertFont_KeysGlyphsOnUnicodeCodepointsNotRawGameCodes()
    {
        RunConversion((outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Assets.Font"]);

            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));

            Assert.Equal(256, font.ScaleW);
            Assert.Equal(256, font.ScaleH);
            Assert.Equal(16, font.LineHeight);
            Assert.Equal(16, font.Base);
            Assert.Equal(1, font.Pages);
            Assert.Equal("Textures/font3.png", font.PageFile);

            // The extracted strings are already Unicode, so a glyph must be reachable by code point.
            // 'é' U+00E9 lives at raw code 130 -> cell (130 % 16, 130 / 16) = (2, 8).
            AssertGlyph(font, 'é', 2 * 16, 8 * 16);
            // 'à' U+00E0 <- raw 133 -> cell (5, 8).
            AssertGlyph(font, 'à', 5 * 16, 8 * 16);
            // 'Ç' U+00C7 <- raw 128 -> cell (0, 8).
            AssertGlyph(font, 'Ç', 0, 8 * 16);
            // ASCII is identity: 'A' is raw 65 -> cell (1, 4).
            AssertGlyph(font, 'A', 1 * 16, 4 * 16);

            // Every glyph advances by the fixed cell: the real per-character width table is not in
            // data-extracted (see the writer's known limitation).
            Assert.All(font.Chars.Values, glyph => Assert.Equal(16, glyph.XAdvance));
            Assert.Contains(report.Messages, message => message.Contains("monospaced", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ConvertFont_DropsDuplicateCodepointsAndKeepsTheCountHonest()
    {
        RunConversion((outputDirectory, report) =>
        {
            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));

            // "chars count" must describe the lines actually written, and no id may repeat: a
            // duplicate "char id" line is invalid BMFont.
            Assert.Equal(font.DeclaredCount, font.Chars.Count);
            Assert.Equal(font.Chars.Count, font.CharLineCount);
            Assert.Equal(font.Chars.Count, report.Counters["Font.Glyphs"]);
            Assert.True(font.Chars.Count < 256, "the CP850 table must collide with the identity range");

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "UI", "font3-charset.json"), Encoding.UTF8));
            var rows = document.RootElement.EnumerateArray()
                .ToDictionary(row => row.GetProperty("raw_code").GetInt32(), row => row);

            // All 256 source records are listed, so the raw code stays recoverable.
            Assert.Equal(256, rows.Count);
            Assert.Equal(233, rows[130].GetProperty("codepoint").GetInt32());
            Assert.True(rows[130].GetProperty("in_font").GetBoolean());

            // Raw 233 has no CP850 entry, so it keeps its own value and collides with raw 130's
            // 'é'. The lower code wins; the loser stays in the charset with its owner recorded.
            Assert.Equal(233, rows[233].GetProperty("codepoint").GetInt32());
            Assert.False(rows[233].GetProperty("in_font").GetBoolean());
            Assert.Equal(130, rows[233].GetProperty("duplicate_of_raw_code").GetInt32());

            Assert.Contains(report.Warnings, warning => warning.Contains("code point", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ConvertFont_CataloguesTheFntAndItsPageTexture()
    {
        RunConversion((outputDirectory, _) =>
        {
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "font3.png")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "font3.texture")));

            using var catalog = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "AssetInfos.json")));
            var fileNames = catalog.RootElement.GetProperty("asset_infos").EnumerateArray()
                .Select(asset => asset.GetProperty("file_name").GetString())
                .ToList();

            Assert.Contains(Path.Combine("UI", "font3.fnt"), fileNames);
            Assert.Contains(Path.Combine("UI", "Textures", "font3.png"), fileNames);
            Assert.Contains(Path.Combine("UI", "Textures", "font3.texture"), fileNames);
        });
    }

    [Fact]
    public void ConvertFont_ProducesAFntFontStashSharpCanLoad()
    {
        RunConversion((outputDirectory, _) =>
        {
            var fntText = File.ReadAllText(Path.Combine(outputDirectory, "UI", "font3.fnt"), Encoding.UTF8);

            // FromBMFont(string, Func<string, TextureWithOffset>) is the overload that does not need
            // a GraphicsDevice: the page texture is only stored on each glyph, never sampled until
            // something draws. TextureWithOffset rejects a null texture though, so the page is an
            // uninitialised Texture2D - enough to load the font headless, not enough to render it.
            var pageTexture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
            var font = StaticSpriteFont.FromBMFont(fntText, _ => new TextureWithOffset(pageTexture));

            Assert.NotNull(font);
            Assert.Equal(16, font.LineHeight);

            var glyph = font.Glyphs['é'];
            Assert.NotNull(glyph);
            Assert.Equal(2 * 16, glyph!.TextureRectangle.X);
            Assert.Equal(8 * 16, glyph.TextureRectangle.Y);
            Assert.Equal(16, glyph.TextureRectangle.Width);
            Assert.Equal(16, glyph.XAdvance);

            Assert.NotNull(font.Glyphs['à']);
            Assert.NotNull(font.Glyphs['Ç']);
            Assert.NotNull(font.Glyphs['A']);
        });
    }

    private static void RunConversion(Action<string, ConversionReport> assert)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteFontFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            FontWriter.ConvertFont(inputDirectory, outputDirectory, report);

            assert(outputDirectory, report);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Uses the real data-extracted/ui/font3.json when the repository is present, and otherwise the
    /// same table generated from the layout that file was verified to follow: 256 codes, every glyph
    /// a 16x16 cell at (Code % 16, Code / 16) with palette 8.
    /// </summary>
    private static void WriteFontFixture(string inputDirectory)
    {
        var uiDirectory = Path.Combine(inputDirectory, "ui");
        Directory.CreateDirectory(uiDirectory);

        var realFontJson = FindRealFile("font3.json");
        if (realFontJson is not null)
        {
            File.Copy(realFontJson, Path.Combine(uiDirectory, "font3.json"));
        }
        else
        {
            var builder = new StringBuilder("[");
            for (var code = 0; code < 256; code++)
            {
                builder.Append(code == 0 ? string.Empty : ",");
                builder.Append(CultureInfo.InvariantCulture, $$"""

                    { "Code": {{code}}, "X": {{code % 16 * 16}}, "Y": {{code / 16 * 16}}, "Width": 16, "Height": 16, "Palette": 8 }
                    """);
            }

            builder.AppendLine().Append(']');
            File.WriteAllText(Path.Combine(uiDirectory, "font3.json"), builder.ToString(), Encoding.UTF8);
        }

        var realFontPng = FindRealFile("font3.png");
        if (realFontPng is not null)
        {
            File.Copy(realFontPng, Path.Combine(uiDirectory, "font3.png"));
        }
        else
        {
            File.WriteAllBytes(Path.Combine(uiDirectory, "font3.png"), FakePngBytes);
        }
    }

    private static string? FindRealFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "ui", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void AssertGlyph(BmFontDocument font, char character, int x, int y)
    {
        Assert.True(font.Chars.TryGetValue(character, out var glyph), $"no char id={(int)character} ('{character}')");
        Assert.Equal(x, glyph!.X);
        Assert.Equal(y, glyph.Y);
        Assert.Equal(16, glyph.Width);
        Assert.Equal(16, glyph.Height);
    }

    /// <summary>
    /// A minimal BMFont text reader, so the assertions check the file as a consumer would parse it
    /// rather than as a string.
    /// </summary>
    private static BmFontDocument ParseBmFont(string path)
    {
        var document = new BmFontDocument();

        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            var fields = ParseFields(line);
            switch (line.Split(' ')[0])
            {
                case "common":
                    document.LineHeight = fields["lineHeight"].AsInt();
                    document.Base = fields["base"].AsInt();
                    document.ScaleW = fields["scaleW"].AsInt();
                    document.ScaleH = fields["scaleH"].AsInt();
                    document.Pages = fields["pages"].AsInt();
                    break;

                case "page":
                    document.PageFile = fields["file"].Trim('"');
                    break;

                case "chars":
                    document.DeclaredCount = fields["count"].AsInt();
                    break;

                case "char":
                    document.CharLineCount++;
                    document.Chars[fields["id"].AsInt()] = new BmFontChar
                    {
                        X = fields["x"].AsInt(),
                        Y = fields["y"].AsInt(),
                        Width = fields["width"].AsInt(),
                        Height = fields["height"].AsInt(),
                        XAdvance = fields["xadvance"].AsInt(),
                    };
                    break;
            }
        }

        return document;
    }

    private static Dictionary<string, string> ParseFields(string line)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = token.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                fields[token[..separator]] = token[(separator + 1)..];
            }
        }

        return fields;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "alundra-font-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class BmFontDocument
    {
        public int LineHeight { get; set; }
        public int Base { get; set; }
        public int ScaleW { get; set; }
        public int ScaleH { get; set; }
        public int Pages { get; set; }
        public string PageFile { get; set; } = string.Empty;
        public int DeclaredCount { get; set; }
        public int CharLineCount { get; set; }
        public Dictionary<int, BmFontChar> Chars { get; } = new();
    }

    private sealed class BmFontChar
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int XAdvance { get; set; }
    }
}

internal static class BmFontFieldExtensions
{
    public static int AsInt(this string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
