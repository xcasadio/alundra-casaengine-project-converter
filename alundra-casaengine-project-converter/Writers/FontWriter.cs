using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 5, second half: Alundra's bitmap font (ui/font3.png + ui/font3.json) as a BMFont a real
/// font library can load.
///
/// Mapping decisions:
///  - ui/font3.json is 256 records {Code, X, Y, Width, Height, Palette}. In the extracted data every
///    glyph is 16x16 with Palette 8 and sits exactly at X = (Code % 16) * 16, Y = (Code / 16) * 16
///    in the 256x256 atlas. The rectangles are still taken from the file rather than recomputed from
///    the grid - the file is the source of truth - but a deviation from the grid is reported, since
///    it would mean the atlas layout changed under us.
///  - font3.png goes through TextureAssetWriter into UI/Textures/, the same folder and catalog shape
///    Phase 7 gives every other UI texture, so the font's page is a catalogued asset and not a
///    stray file. The .fnt itself is catalogued too, as a plain file entry: it is not a CasaEngine
///    asset type, but a runtime needs a way to address it by id.
///  - char id is the UNICODE CODEPOINT, not the raw game code. Alundra indexes its atlas by a
///    CP850-ish byte, but the extracted strings are already decoded to Unicode, so a .fnt keyed on
///    raw game codes could not render a single one of them - looking up 'é' (U+00E9) would miss the
///    glyph stored at code 130. The conversion is a port of
///    AlundraEngine.Text.TextDecoder.ConvertCp850ToLatin1: identity below 128, and the CP850 to
///    Latin-1 table above it. Only that branch is ported; the same function has a "cp850 - 0x10"
///    branch for the USA release, and the data being converted here is the French/PAL release.
///  - The raw code stays recoverable through UI/font3-charset.json, which lists all 256 source
///    records with the codepoint each produced and whether it made it into the .fnt.
///  - Codes above 127 that the CP850 table does not mention keep their own value as a codepoint
///    (that is what the game's function does). Several of those collide with a code the table does
///    map - raw 130 and raw 233 both mean U+00E9 'é' - and a duplicate "char id" line is invalid
///    BMFont and would make the count line lie, so one code has to win. A code the table names wins
///    over one that only fell through to identity, because the table is evidence and the fallback is
///    a guess; between two codes of equal standing the lower one wins. The losers are dropped with a
///    warning and "chars count" is recomputed from the lines actually written.
///
/// KNOWN LIMITATION - text renders monospaced. Alundra draws text proportionally, advancing by
/// g_fontCharWidthTable[code * 5], a table that lives in the game executable and is NOT part of
/// data-extracted. Every xadvance below is therefore the fixed 16px cell width. This is a real
/// fidelity gap, not a rounding detail: dialogue laid out with these advances will be far wider and
/// more loosely spaced than the original. Extracting that table is the fix.
/// </summary>
public static class FontWriter
{
    private const string UiRelativeDirectory = "UI";
    private const string FontFileName = "font3.fnt";
    private const string CharsetFileName = "font3-charset.json";
    private const int CellSize = 16;

    private static readonly string UiTexturesRelativeDirectory = Path.Combine("UI", "Textures");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    // Port of AlundraEngine.Text.TextDecoder.Cp850ToLatin1 (French/PAL branch). Latin-1 code points
    // are Unicode code points, so these values need no further conversion.
    private static readonly Dictionary<int, int> Cp850ToLatin1 = new()
    {
        { 128, 199 }, // Ç
        { 129, 252 }, // ü
        { 130, 233 }, // é
        { 131, 226 }, // â
        { 132, 228 }, // ä
        { 133, 224 }, // à
        { 134, 229 }, // å
        { 135, 231 }, // ç
        { 136, 234 }, // ê
        { 137, 235 }, // ë
        { 138, 232 }, // è
        { 139, 239 }, // ï
        { 140, 238 }, // î
        { 141, 236 }, // ì
        { 142, 196 }, // Ä
        { 143, 197 }, // Å
        { 144, 201 }, // É
        { 145, 230 }, // æ
        { 146, 198 }, // Æ
        { 147, 244 }, // ô
        { 148, 246 }, // ö
        { 149, 242 }, // ò
        { 150, 251 }, // û
        { 151, 249 }, // ù
        { 152, 255 }, // ÿ
        { 153, 214 }, // Ö
        { 154, 220 }, // Ü
        { 155, 162 }, // ¢
        { 156, 163 }, // £
        { 157, 165 }, // ¥
        { 160, 225 }, // á
        { 161, 237 }, // í
        { 162, 243 }, // ó
        { 163, 250 }, // ú
        { 164, 241 }, // ñ
        { 165, 209 }, // Ñ
        { 166, 170 }, // ª
        { 167, 186 }, // º
        { 168, 191 }, // ¿
        { 170, 172 }, // ¬
        { 171, 189 }, // ½
        { 172, 188 }, // ¼
        { 173, 161 }, // ¡
        { 174, 171 }, // «
        { 175, 187 }, // »
        { 181, 193 }, // Á
        { 182, 194 }, // Â
        { 183, 192 }, // À
        { 184, 169 }, // ©
    };

    public static void ConvertFont(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var fontJsonPath = Path.Combine(inputDirectory, "ui", "font3.json");
        var fontPngPath = Path.Combine(inputDirectory, "ui", "font3.png");

        if (!File.Exists(fontJsonPath))
        {
            report.Warnings.Add($"Font: '{fontJsonPath}' not found; font skipped.");
            return;
        }

        List<FontGlyphRecord> records;
        try
        {
            records = ReadGlyphRecords(fontJsonPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Font: failed to read '{fontJsonPath}' - {exception.Message}");
            return;
        }

        var textureCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        try
        {
            TextureAssetWriter.EnsureTexture(fontPngPath, UiTexturesRelativeDirectory, outputDirectory, textureCache);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Font: failed to import font3.png - {exception.Message}");
            return;
        }

        var charsetRows = BuildCharset(records, report);
        WriteFontFile(outputDirectory, charsetRows, report);
        WriteCharsetFile(outputDirectory, charsetRows);

        EditorAssetCatalogService.Save();

        report.Increment("Assets.Font");
        report.Increment("Font.Glyphs", charsetRows.Count(row => row.InFont));
        report.Messages.Add(
            "Font: UI/font3.fnt uses a fixed xadvance of 16px for every glyph. Alundra renders text "
            + "proportionally through g_fontCharWidthTable, which lives in the game executable and is "
            + "not part of data-extracted, so converted text will render monospaced until that table "
            + "is extracted.");
    }

    private static List<FontGlyphRecord> ReadGlyphRecords(string fontJsonPath)
    {
        using var stream = File.OpenRead(fontJsonPath);
        using var document = JsonDocument.Parse(stream);

        var records = new List<FontGlyphRecord>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            records.Add(new FontGlyphRecord
            {
                Code = element.GetProperty("Code").GetInt32(),
                X = element.GetProperty("X").GetInt32(),
                Y = element.GetProperty("Y").GetInt32(),
                Width = element.GetProperty("Width").GetInt32(),
                Height = element.GetProperty("Height").GetInt32(),
                Palette = element.GetProperty("Palette").GetInt32(),
            });
        }

        return records;
    }

    private static List<CharsetRow> BuildCharset(List<FontGlyphRecord> records, ConversionReport report)
    {
        var rows = new List<CharsetRow>(records.Count);
        var ownerByCodepoint = new Dictionary<int, int>();
        var duplicateCount = 0;

        // Two passes, so a code the CP850 table actually names beats one that only fell through to
        // the identity branch. Raw 184 IS '©' per the table while raw 169 merely keeps its own byte
        // value; taking them in plain ascending order would hand U+00A9 to the guess and drop the
        // known glyph. Within a pass the lower code still wins - at that point the two candidates
        // carry the same weight of evidence.
        var byConfidence = records
            .OrderBy(record => Cp850ToLatin1.ContainsKey(record.Code) ? 0 : 1)
            .ThenBy(record => record.Code);

        foreach (var record in byConfidence)
        {
            var expectedX = record.Code % 16 * CellSize;
            var expectedY = record.Code / 16 * CellSize;
            if (record.X != expectedX || record.Y != expectedY
                || record.Width != CellSize || record.Height != CellSize)
            {
                report.Warnings.Add(
                    $"Font: glyph {record.Code} is at ({record.X},{record.Y}) {record.Width}x{record.Height}, "
                    + $"not the expected 16x16 cell at ({expectedX},{expectedY}); its own rectangle was used.");
            }

            var codepoint = ConvertCp850ToLatin1(record.Code);
            var row = new CharsetRow
            {
                RawCode = record.Code,
                Codepoint = codepoint,
                X = record.X,
                Y = record.Y,
                Width = record.Width,
                Height = record.Height,
                Palette = record.Palette,
                InFont = true,
            };

            if (ownerByCodepoint.TryGetValue(codepoint, out var owner))
            {
                row.InFont = false;
                row.DuplicateOfRawCode = owner;
                duplicateCount++;
            }
            else
            {
                ownerByCodepoint[codepoint] = record.Code;
            }

            rows.Add(row);
        }

        // Ordered by raw code, so the charset file reads as the source table it mirrors rather than
        // in the resolution order above.
        rows.Sort((left, right) => left.RawCode.CompareTo(right.RawCode));

        if (duplicateCount > 0)
        {
            report.Warnings.Add(
                $"Font: {duplicateCount} of {records.Count} glyph codes map to a code point another code "
                + "already claimed (CP850 130 and the unmapped byte 233 both mean U+00E9). A code the "
                + "CP850 table names wins over one that only kept its own byte value, and the lower code "
                + "wins between equals; the losers are listed in UI/font3-charset.json with "
                + "duplicate_of_raw_code set and are not reachable through the .fnt.");
        }

        return rows;
    }

    // Port of AlundraEngine.Text.TextDecoder.ConvertCp850ToLatin1, French/PAL branch only: identity
    // below 128, table lookup above, and unmapped high bytes keep their own value.
    private static int ConvertCp850ToLatin1(int code)
        => code >= 128 && Cp850ToLatin1.TryGetValue(code, out var mapped) ? mapped : code;

    private static void WriteFontFile(string outputDirectory, List<CharsetRow> rows, ConversionReport report)
    {
        var emitted = rows.Where(row => row.InFont).OrderBy(row => row.Codepoint).ToList();

        var builder = new StringBuilder();
        builder.AppendLine(
            "info face=\"font3\" size=16 bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=0 "
            + "aa=1 padding=0,0,0,0 spacing=0,0 outline=0");
        builder.AppendLine(
            "common lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 packed=0 alphaChnl=0 "
            + "redChnl=0 greenChnl=0 blueChnl=0");

        // The page path is relative to the .fnt, which sits in UI/ while its texture is catalogued
        // in UI/Textures/ like every other UI PNG. Forward slash: it is read by loaders, not by the
        // file system alone.
        builder.AppendLine("page id=0 file=\"Textures/font3.png\"");

        // Recomputed from the lines actually written, so the count never claims dropped duplicates.
        builder.AppendLine(
            string.Create(CultureInfo.InvariantCulture, $"chars count={emitted.Count}"));

        foreach (var row in emitted)
        {
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"char id={row.Codepoint} x={row.X} y={row.Y} width={row.Width} height={row.Height} "
                + $"xoffset=0 yoffset=0 xadvance={CellSize} page=0 chnl=15"));
        }

        var targetDirectory = Path.Combine(outputDirectory, UiRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var relativePath = Path.Combine(UiRelativeDirectory, FontFileName);
        File.WriteAllText(
            Path.Combine(outputDirectory, relativePath),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        EditorAssetCatalogService.Add(new AssetInfo(Ids.For($"font/{relativePath.Replace('\\', '/')}"))
        {
            Name = Path.GetFileNameWithoutExtension(FontFileName),
            FileName = relativePath,
        });

        if (emitted.Count == 0)
        {
            report.Errors.Add("Font: no glyph could be written to UI/font3.fnt.");
        }
    }

    private static void WriteCharsetFile(string outputDirectory, List<CharsetRow> rows)
    {
        File.WriteAllText(
            Path.Combine(outputDirectory, UiRelativeDirectory, CharsetFileName),
            JsonSerializer.Serialize(rows, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>One record of ui/font3.json, field names as the extractor wrote them.</summary>
    private sealed class FontGlyphRecord
    {
        public int Code { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Palette { get; set; }
    }

    /// <summary>
    /// One row of UI/font3-charset.json: the raw game code, the code point it was mapped to, its
    /// cell in the atlas, the source palette (which has nowhere to live in a .fnt) and whether the
    /// row produced a "char id" line - so the raw code and everything the .fnt cannot hold stays
    /// recoverable.
    /// </summary>
    private sealed class CharsetRow
    {
        public int RawCode { get; set; }
        public int Codepoint { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Palette { get; set; }
        public bool InFont { get; set; }
        public int? DuplicateOfRawCode { get; set; }
    }
}
