using System.Drawing;
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

    // D-E15-16 (docs/plan-e15-yarn.md, E15.e): the 17 proven non-ASCII characters, each keyed by its
    // own CP1252 raw code (= codepoint, except 'œ') so the cell can be checked against the grid
    // formula x = (code % 16) * 16, y = (code / 16) * 16.
    private static readonly (char Character, int RawCode)[] ProvenHighCharacters =
    {
        ('é', 0xE9), ('à', 0xE0), ('è', 0xE8), ('ê', 0xEA), ('ç', 0xE7), ('î', 0xEE), ('ô', 0xF4),
        ('â', 0xE2), ('œ', 0x9C), ('û', 0xFB), ('ù', 0xF9), ('Ç', 0xC7), ('°', 0xB0), ('ï', 0xEF),
        ('«', 0xAB), ('»', 0xBB), ('É', 0xC9),
    };

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

            // Each of the 17 proven characters (D-E15-16) points at its own CP1252 cell.
            // E19.f2b0: the rectangle is the binary's own glyph table entry for that raw code (the
            // annex), not the 16x16 grid cell.
            var annex = ReadAnnex();
            foreach (var (character, rawCode) in ProvenHighCharacters)
            {
                var entry = annex[rawCode];
                AssertGlyph(font, character, entry.SrcX, entry.SrcY, entry.Width, entry.Height);
            }

            // ASCII is identity: 'A' is raw 65 -> source (16, 64), 7 wide in the binary's table.
            AssertGlyph(font, 'A', 16, 64, 7, 16);
        });
    }

    [Fact]
    public void ConvertFont_UsesProportionalXAdvanceFromFontCharWidthsCsv()
    {
        RunConversion((outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);

            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));

            // docs/plan-e12-dialogues.md, D-E12-2: g_fontCharWidthTable is not a fixed 16px cell -
            // at least two glyphs must come out with genuinely different advances, proving the .fnt
            // is proportional rather than monospace.
            var distinctAdvances = font.Chars.Values.Select(glyph => glyph.XAdvance).Distinct().Count();
            Assert.True(
                distinctAdvances > 1,
                "expected at least two distinct xadvance values; the font is still monospaced");

            // ASCII space, raw code 32: FontCharWidths.csv row "32;4". ASCII '!' raw 33: row "33;3".
            // Both keep raw code == codepoint (ASCII is identity in ConvertCp850ToLatin1), so looking
            // them up by char is unambiguous.
            AssertXAdvance(font, ' ', 4);
            AssertXAdvance(font, '!', 3);
        });
    }

    [Fact]
    public void ConvertFont_OnlyThe17ProvenHighCellsGetACharacter_AndTheCountIsHonest()
    {
        RunConversion((outputDirectory, report) =>
        {
            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));

            // "chars count" must describe the lines actually written, and no id may repeat: a
            // duplicate "char id" line is invalid BMFont.
            Assert.Equal(font.DeclaredCount, font.Chars.Count);
            Assert.Equal(font.Chars.Count, font.CharLineCount);
            Assert.Equal(font.Chars.Count, report.Counters["Font.Glyphs"]);

            // 128 ASCII/control codes (identity, D-E15-16) + the 17 proven high cells.
            Assert.Equal(128 + ProvenHighCharacters.Length, font.Chars.Count);

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "UI", "font3-charset.json"), Encoding.UTF8));
            var rows = document.RootElement.EnumerateArray()
                .ToDictionary(row => row.GetProperty("raw_code").GetInt32(), row => row);

            // All 256 source records are listed, so the raw code stays recoverable.
            Assert.Equal(256, rows.Count);

            // Raw 233 ('é' by CP1252) is proven and in_font.
            Assert.Equal(233, rows[233].GetProperty("codepoint").GetInt32());
            Assert.True(rows[233].GetProperty("in_font").GetBoolean());
            Assert.Equal(JsonValueKind.Null, rows[233].GetProperty("duplicate_of_raw_code").ValueKind);

            // Cell 130, which the old CP850 table wrongly sent 'é' to, has no proven character any
            // more: codepoint null, in_font false, a readable reason, and no line in the .fnt points
            // at its cell (x=32, y=128).
            Assert.Equal(JsonValueKind.Null, rows[130].GetProperty("codepoint").ValueKind);
            Assert.False(rows[130].GetProperty("in_font").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(rows[130].GetProperty("reason").GetString()));
            Assert.DoesNotContain(font.Chars.Values, glyph => glyph.X == 32 && glyph.Y == 128);

            // No collision warning any more.
            Assert.DoesNotContain(report.Warnings, warning => warning.Contains("code point", StringComparison.Ordinal));
            Assert.DoesNotContain(report.Warnings, warning => warning.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void ConvertFont_MapsOeToItsCp1252Codepoint_NotItsRawByteValue()
    {
        RunConversion((outputDirectory, _) =>
        {
            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));

            // 'œ' U+0153 <- raw 0x9C (156). CP1252 byte 0x9C is 'œ', but as a bare Latin-1/Unicode
            // code point U+009C is a control character - the one case that is not raw-code-equals-
            // codepoint (D-E15-16, FontWriter class doc comment).
            AssertGlyph(font, 'œ', 195, 144, 9, 16); // the binary's table entry of raw 0x9C (E19.f2b0)
            Assert.False(font.Chars.ContainsKey(0x9C), "raw code 0x9C must not appear as a codepoint on its own");
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

            // 'é' U+00E9 <- raw 233 (D-E15-16) -> cell (233 % 16, 233 / 16) = (9, 14).
            var glyph = font.Glyphs['é'];
            Assert.NotNull(glyph);
            Assert.Equal(9 * 16, glyph!.TextureRectangle.X);
            Assert.Equal(14 * 16, glyph.TextureRectangle.Y);
            Assert.Equal(5, glyph.TextureRectangle.Width); // E19.f2b0: the binary's table entry (was the 16px cell)
            Assert.Equal(5, glyph.XAdvance); // FontCharWidths.csv row "233;5"

            Assert.NotNull(font.Glyphs['à']);
            Assert.NotNull(font.Glyphs['Ç']);
            Assert.NotNull(font.Glyphs['A']);
        });
    }

    /// <summary>
    /// E19.f2b0, T1: every char line carries the binary's glyph table entry of its raw code
    /// (<c>RenderTextBitmap</c> copies <c>w x h</c> texels from <c>(srcX, srcY)</c> to <c>(pen, yoff)</c> and
    /// advances the pen by <c>w</c>) - not the 16x16 grid cell of the source record.
    /// </summary>
    [Fact]
    public void ConvertFont_EveryCharLineCarriesTheBinarysGlyphTableEntryOfItsRawCode()
    {
        var annex = ReadAnnex();

        RunConversion((outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);

            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));
            Assert.Equal(145, font.Chars.Count);

            var wrong = new List<int>();
            foreach (var (codepoint, glyph) in font.Chars)
            {
                var entry = annex[RawCodeOf(codepoint)];
                var expected = (entry.SrcX, entry.SrcY, entry.Width, entry.Height, 0, entry.YOffset, entry.Width);
                var actual = (glyph.X, glyph.Y, glyph.Width, glyph.Height, glyph.XOffset, glyph.YOffset, glyph.XAdvance);
                if (expected != actual)
                {
                    wrong.Add(codepoint);
                }
            }

            Assert.True(
                wrong.Count == 0,
                $"{wrong.Count} char lines differ from the binary's table (codepoints: {string.Join(", ", wrong.Order())}).");
        });
    }

    /// <summary>
    /// E19.f2b0, T2: what a renderer draws from a char line equals what the binary draws for its raw code.
    /// Both are stamped, unclipped, on a transparent band (the line's rectangle at
    /// <c>(xoffset, yoffset)</c>, the table's at <c>(0, yoff)</c>) and only texels of non-zero alpha count,
    /// compared as sets of (position, RGBA) - an empty texel and a missing texel are the same on screen.
    /// </summary>
    [Fact]
    public void ConvertFont_EveryCharLineDrawsTheSameOpaqueTexelsAsTheBinary()
    {
        if (FindRealFile("font3.png") is null)
        {
            return; // real data-extracted/ui/font3.png needed, like the neighbouring real-data tests
        }

        var annex = ReadAnnex();

        RunConversion((outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);

            var font = ParseBmFont(Path.Combine(outputDirectory, "UI", "font3.fnt"));
            using var page = new Bitmap(Path.Combine(outputDirectory, "UI", "Textures", "font3.png"));

            HashSet<(int X, int Y, int Argb)> Stamp(int x, int y, int width, int height, int dx, int dy)
            {
                var texels = new HashSet<(int, int, int)>();
                for (var row = 0; row < height; row++)
                {
                    for (var column = 0; column < width; column++)
                    {
                        var sx = x + column;
                        var sy = y + row;
                        if (sx < 0 || sy < 0 || sx >= page.Width || sy >= page.Height)
                        {
                            continue; // outside the page: transparent
                        }

                        var color = page.GetPixel(sx, sy);
                        if (color.A != 0)
                        {
                            texels.Add((dx + column, dy + row, color.ToArgb()));
                        }
                    }
                }

                return texels;
            }

            var different = new List<int>();
            foreach (var (codepoint, glyph) in font.Chars)
            {
                var entry = annex[RawCodeOf(codepoint)];
                var drawn = Stamp(glyph.X, glyph.Y, glyph.Width, glyph.Height, glyph.XOffset, glyph.YOffset);
                var expected = Stamp(entry.SrcX, entry.SrcY, entry.Width, entry.Height, 0, entry.YOffset);
                if (!drawn.SetEquals(expected))
                {
                    different.Add(RawCodeOf(codepoint));
                }
            }

            Assert.True(
                different.Count == 0,
                $"{different.Count} glyphs draw other texels than the binary (raw codes: {string.Join(", ", different.Order())}).");
        });
    }

    /// <summary>E19.f2b0, T3: the 256 rows of font3-charset.json carry the binary's rectangle too.</summary>
    [Fact]
    public void ConvertFont_EveryCharsetRowCarriesTheBinarysRectangle()
    {
        var annex = ReadAnnex();

        RunConversion((outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "UI", "font3-charset.json"), Encoding.UTF8));
            var rows = document.RootElement.EnumerateArray().ToList();
            Assert.Equal(256, rows.Count);

            var wrong = new List<int>();
            foreach (var row in rows)
            {
                var rawCode = row.GetProperty("raw_code").GetInt32();
                var entry = annex[rawCode];
                if (row.GetProperty("x").GetInt32() != entry.SrcX
                    || row.GetProperty("y").GetInt32() != entry.SrcY
                    || row.GetProperty("width").GetInt32() != entry.Width
                    || row.GetProperty("height").GetInt32() != entry.Height)
                {
                    wrong.Add(rawCode);
                }
            }

            Assert.True(wrong.Count == 0, $"{wrong.Count} charset rows differ from the binary's table.");
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

    private static void AssertGlyph(BmFontDocument font, char character, int x, int y, int width, int height)
    {
        Assert.True(font.Chars.TryGetValue(character, out var glyph), $"no char id={(int)character} ('{character}')");
        Assert.Equal(x, glyph!.X);
        Assert.Equal(y, glyph.Y);
        Assert.Equal(width, glyph.Width);
        Assert.Equal(height, glyph.Height);
    }

    /// <summary>One row of the annex: the binary's glyph table entry of a raw code.</summary>
    private sealed record AnnexEntry(int Width, int Height, int SrcX, int SrcY, int YOffset);

    /// <summary>
    /// docs/plan-e19-f2b0-annexe/glyph_table.txt: the table at 0x800993C4 of ALUN_CD.EXE, dumped off the
    /// binary and independent of both the decompilation and the analyser's FontGlyphTable.csv, so it is
    /// the reference these tests compare the export against (never the converter's own output).
    /// Columns: idx (hex raw code), w, h, srcX, srcY, yoff.
    /// </summary>
    private static Dictionary<int, AnnexEntry> ReadAnnex()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f2b0-annexe", "glyph_table.txt");
            if (File.Exists(candidate))
            {
                var result = new Dictionary<int, AnnexEntry>();
                foreach (var line in File.ReadAllLines(candidate))
                {
                    var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 6 || fields[0] == "idx")
                    {
                        continue;
                    }

                    result[int.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = new AnnexEntry(
                        int.Parse(fields[1], CultureInfo.InvariantCulture),
                        int.Parse(fields[2], CultureInfo.InvariantCulture),
                        int.Parse(fields[3], CultureInfo.InvariantCulture),
                        int.Parse(fields[4], CultureInfo.InvariantCulture),
                        int.Parse(fields[5], CultureInfo.InvariantCulture));
                }

                Assert.Equal(256, result.Count);
                return result;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"no 'docs/plan-e19-f2b0-annexe/glyph_table.txt' found above '{AppContext.BaseDirectory}'.");
    }

    // The raw game code a font3.fnt char id came from: identity below 128, and 'œ' (U+0153) is raw 0x9C;
    // every other proven high character is its own CP1252 byte (FontWriter's ProvenHighCodepoints).
    private static int RawCodeOf(int codepoint) => codepoint == 0x0153 ? 0x9C : codepoint;

    private static void AssertXAdvance(BmFontDocument font, char character, int expectedAdvance)
    {
        Assert.True(font.Chars.TryGetValue(character, out var glyph), $"no char id={(int)character} ('{character}')");
        Assert.Equal(expectedAdvance, glyph!.XAdvance);
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
                        XOffset = fields["xoffset"].AsInt(),
                        YOffset = fields["yoffset"].AsInt(),
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
        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public int XAdvance { get; set; }
    }
}

internal static class BmFontFieldExtensions
{
    public static int AsInt(this string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
