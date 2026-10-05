using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlundraCasaEngineProjectConverter.Readers;
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
///    in the 256x256 atlas. That 16x16 grid cell is NOT what the original draws (docs/plan-e19-opcodes.md,
///    E19.f2b0, ADR-0036): <c>RenderTextBitmap</c> (0x800478C4) copies, for each raw code, the
///    w x h texels the glyph table at 0x800993C4 gives from (srcX, srcY) to (pen, yoff). Each glyph's
///    rectangle (<c>x</c>, <c>y</c>, <c>width</c>, <c>height</c>, in the char lines and in
///    font3-charset.json) and its <c>yoffset</c> therefore come from <c>FontGlyphTable.csv</c>
///    (<see cref="FontGlyphTableCatalogReader"/>), by the glyph's raw code. The source record's own
///    rectangle is only the fallback when the CSV or a row is missing (a warning), and the check that
///    the records still follow the grid stays, since a deviation would mean the atlas layout changed
///    under us.
///  - font3.png goes through TextureAssetWriter into UI/Textures/, the same folder and catalog shape
///    Phase 7 gives every other UI texture, so the font's page is a catalogued asset and not a
///    stray file. The .fnt itself is catalogued too, as a plain file entry: it is not a CasaEngine
///    asset type, but a runtime needs a way to address it by id.
///  - char id is the UNICODE CODEPOINT, not the raw game code. Below 128 the raw code is its own
///    codepoint, identity, glyphs 16-29 included. From 128 to 255, docs/plan-e15-yarn.md E15.e
///    (D-E15-14 to D-E15-16, ADR-0009) established that the CP850 table this class used to apply
///    (AlundraEngine.Text.TextDecoder.ConvertCp850ToLatin1) does not match the atlas: the original
///    game draws an escape pair '{'+c at glyph 0x50+c and '}'+c at glyph 0x90+c
///    (TextDecoder.cs's TextInterpreter, and its own comment on the Tokens table), which lines up
///    with CP1252/Latin-1 - not with CP850 - and the exported atlas confirms it by eye (cell 130 is a
///    comma, cell 233 is 'é'). Only the 17 non-ASCII characters actually proven to occur in the
///    corpus this way are given a cell (see <see cref="ProvenHighCodepoints"/>); every other cell from
///    128 to 255 is left out of font3.fnt entirely - nothing "falls back to its own byte value" any
///    more, since a byte this class cannot point at a real character would be a guess, not evidence.
///  - The raw code stays recoverable through UI/font3-charset.json, which lists all 256 source
///    records with the codepoint each produced (or none) and whether it made it into the .fnt, plus a
///    short reason when it did not.
///  - Because only 17 proven, pairwise-distinct code points are produced above 128 (identity below
///    128 cannot collide with any of them - they are all either above the ASCII range or, for 'œ'
///    U+0153, not a Latin-1 code point at all), no two raw codes can ever claim the same codepoint any
///    more: there is no duplicate resolution left to do, and no "chars count" mismatch to recompute
///    around. <see cref="CharsetRow.DuplicateOfRawCode"/> is kept in the JSON shape (always null) for
///    docs/formats/font.md's existing schema rather than removed outright.
///
///  - PROPORTIONAL WIDTHS (docs/plan-e12-dialogues.md, slice E12.b, D-E12-2): each glyph's
///    <c>xadvance</c> comes from <c>FontCharWidths.csv</c> (<see cref="FontCharWidthCatalogReader"/>),
///    the RAW port of the game's own <c>g_fontCharWidthTable</c>, looked up by the glyph's own raw
///    game code - not by its resolved Unicode codepoint. Every row, whether or not it has a proven
///    character, carries its own <c>RawCode</c> and its own advance from the table. A code the CSV
///    has no row for (should not happen - it lists all 256 raw codes) falls back to the 16px cell
///    width, reported as a warning. The table's <c>width</c> equals that advance on all 145 lines
///    written; <c>xadvance</c> and the choice of characters (ADR-0009) are unchanged by E19.f2b0.
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

    // D-E15-16 (docs/plan-e15-yarn.md, E15.e; ADR-0009): the only 17 raw codes from 128 to 255 that
    // get a character, each keyed by its own CP1252 byte value - which is also the atlas cell the
    // original game draws for it (TextDecoder.cs's '{'/'}' escape formula, corpus + atlas evidence).
    // For every entry but 'œ' the raw code, the CP1252 byte and the Unicode code point are the same
    // number; 'œ' is the one case where CP1252 0x9C is not a Latin-1/Unicode code point (U+009C is a
    // control character), so it alone needs an explicit, different codepoint (U+0153).
    private static readonly Dictionary<int, int> ProvenHighCodepoints = new()
    {
        { 0xE9, 0x00E9 }, // é
        { 0xE0, 0x00E0 }, // à
        { 0xE8, 0x00E8 }, // è
        { 0xEA, 0x00EA }, // ê
        { 0xE7, 0x00E7 }, // ç
        { 0xEE, 0x00EE }, // î
        { 0xF4, 0x00F4 }, // ô
        { 0xE2, 0x00E2 }, // â
        { 0x9C, 0x0153 }, // œ
        { 0xFB, 0x00FB }, // û
        { 0xF9, 0x00F9 }, // ù
        { 0xC7, 0x00C7 }, // Ç
        { 0xB0, 0x00B0 }, // °
        { 0xEF, 0x00EF }, // ï
        { 0xAB, 0x00AB }, // «
        { 0xBB, 0x00BB }, // »
        { 0xC9, 0x00C9 }, // É
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

        var advanceByRawCode = ReadCharWidths(report);
        var glyphTable = ReadGlyphTable(report);

        var charsetRows = BuildCharset(records, advanceByRawCode, glyphTable, report);
        WriteFontFile(outputDirectory, charsetRows, report);
        WriteCharsetFile(outputDirectory, charsetRows);

        EditorAssetCatalogService.Save();

        report.Increment("Assets.Font");
        report.Increment("Font.Glyphs", charsetRows.Count(row => row.InFont));
    }

    // docs/plan-e12-dialogues.md, slice E12.b: FontCharWidths.csv ships with the converter the same
    // way EntityNames.csv/MapMusicIndex.csv do (see alundra-casaengine-project-converter.csproj).
    private static IReadOnlyDictionary<int, int> ReadCharWidths(ConversionReport report)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "FontCharWidths.csv");
        if (!File.Exists(csvPath))
        {
            report.Errors.Add(
                $"Font: FontCharWidths.csv not found at '{csvPath}'; every glyph falls back to the "
                + "fixed 16px cell width.");
            return new Dictionary<int, int>();
        }

        var result = FontCharWidthCatalogReader.Read(csvPath);
        foreach (var warning in result.Warnings)
        {
            report.Warnings.Add(warning);
        }

        return result.AdvanceByRawCode;
    }

    // docs/plan-e19-opcodes.md, slice E19.f2b0: FontGlyphTable.csv ships with the converter the same
    // way FontCharWidths.csv does (see alundra-casaengine-project-converter.csproj). Missing: every
    // glyph falls back to the rectangle of its source record, reported as a warning.
    private static IReadOnlyDictionary<int, FontGlyphTableCatalogReader.GlyphEntry> ReadGlyphTable(ConversionReport report)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "FontGlyphTable.csv");
        if (!File.Exists(csvPath))
        {
            report.Warnings.Add(
                $"Font: FontGlyphTable.csv not found at '{csvPath}'; every glyph falls back to the "
                + "rectangle of its ui/font3.json record.");
            return new Dictionary<int, FontGlyphTableCatalogReader.GlyphEntry>();
        }

        var result = FontGlyphTableCatalogReader.Read(csvPath);
        foreach (var warning in result.Warnings)
        {
            report.Warnings.Add(warning);
        }

        return result.EntryByRawCode;
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

    private static List<CharsetRow> BuildCharset(
        List<FontGlyphRecord> records,
        IReadOnlyDictionary<int, int> advanceByRawCode,
        IReadOnlyDictionary<int, FontGlyphTableCatalogReader.GlyphEntry> glyphTable,
        ConversionReport report)
    {
        var rows = new List<CharsetRow>(records.Count);

        foreach (var record in records)
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

            // Looked up by this glyph's OWN raw code, regardless of whether that code has a proven
            // character - xadvance is unchanged by D-E15-16 (docs/plan-e15-yarn.md, E15.e, contract
            // item 1).
            if (!advanceByRawCode.TryGetValue(record.Code, out var advance))
            {
                advance = CellSize;
                report.Warnings.Add(
                    $"Font: FontCharWidths.csv has no row for raw code {record.Code}; "
                    + $"falling back to the fixed {CellSize}px cell width.");
            }

            // E19.f2b0: the rectangle the original copies for this raw code; the source record's own
            // rectangle only when the table has no row for it.
            int x = record.X, y = record.Y, width = record.Width, height = record.Height, yOffset = 0;
            if (glyphTable.TryGetValue(record.Code, out var entry))
            {
                (x, y, width, height, yOffset) = (entry.SrcX, entry.SrcY, entry.Width, entry.Height, entry.YOffset);
            }
            else if (glyphTable.Count > 0)
            {
                report.Warnings.Add(
                    $"Font: FontGlyphTable.csv has no row for raw code {record.Code}; "
                    + "falling back to the rectangle of its ui/font3.json record.");
            }

            var hasCharacter = TryGetCodepoint(record.Code, out var codepoint);

            rows.Add(new CharsetRow
            {
                RawCode = record.Code,
                Codepoint = hasCharacter ? codepoint : null,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                YOffset = yOffset,
                Palette = record.Palette,
                Advance = advance,
                InFont = hasCharacter,
                DuplicateOfRawCode = null,
                Reason = hasCharacter ? null : "no proven character",
            });
        }

        // Ordered by raw code, so the charset file reads as the source table it mirrors.
        rows.Sort((left, right) => left.RawCode.CompareTo(right.RawCode));

        return rows;
    }

    // D-E15-16: identity below 128 (glyphs 16-29 included), the 17 proven CP1252 cells from 128 to
    // 255, nothing else.
    private static bool TryGetCodepoint(int code, out int codepoint)
    {
        if (code < 128)
        {
            codepoint = code;
            return true;
        }

        return ProvenHighCodepoints.TryGetValue(code, out codepoint);
    }

    private static void WriteFontFile(string outputDirectory, List<CharsetRow> rows, ConversionReport report)
    {
        var emitted = rows.Where(row => row.InFont).OrderBy(row => row.Codepoint!.Value).ToList();

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
                $"char id={row.Codepoint!.Value} x={row.X} y={row.Y} width={row.Width} height={row.Height} "
                + $"xoffset=0 yoffset={row.YOffset} xadvance={row.Advance} page=0 chnl=15"));
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
    /// One row of UI/font3-charset.json: the raw game code, the code point it was mapped to (null when
    /// D-E15-16 has no proven character for this cell), its rectangle in the atlas (the binary's glyph
    /// table entry since E19.f2b0, no longer the 16x16 grid cell), the source palette
    /// (which has nowhere to live in a .fnt) and whether the row produced a "char id" line - so the raw
    /// code and everything the .fnt cannot hold stays recoverable. <see cref="DuplicateOfRawCode"/> is
    /// always null now that no two raw codes can claim the same codepoint (see the class doc comment);
    /// it is kept only so the JSON shape docs/formats/font.md documents does not change.
    /// </summary>
    private sealed class CharsetRow
    {
        public int RawCode { get; set; }
        public int? Codepoint { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>The table's <c>yOffset</c>, written as the char line's <c>yoffset</c>; not part of the
        /// charset JSON, whose shape does not change.</summary>
        [JsonIgnore]
        public int YOffset { get; set; }

        public int Palette { get; set; }
        public int Advance { get; set; }
        public bool InFont { get; set; }
        public int? DuplicateOfRawCode { get; set; }

        /// <summary>Short, readable reason a cell has no character, e.g. "no proven character". Null
        /// when <see cref="InFont"/> is true.</summary>
        public string? Reason { get; set; }
    }
}
