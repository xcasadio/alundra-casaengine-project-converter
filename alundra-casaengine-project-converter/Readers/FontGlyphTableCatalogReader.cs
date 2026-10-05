namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>FontGlyphTable.csv</c> - the whole glyph table of the original's <c>g_fontCharWidthTable</c>
/// (<c>StaticVariables.cs:9483</c>, the table at <c>0x800993C4</c> of the executable): one row per raw
/// glyph code (0..255) holding the five ints of its 20-byte entry, <c>width;height;srcX;srcY;yOffset</c>.
/// <c>RenderTextBitmap</c> (<c>0x800478C4</c>) copies <c>width x height</c> texels of FONT3.TIM from
/// <c>(srcX, srcY)</c> to <c>(pen, yOffset)</c> and advances the pen by <c>width</c>
/// (docs/plan-e19-opcodes.md, E19.f2b0). <c>FontCharWidths.csv</c> (<see cref="FontCharWidthCatalogReader"/>)
/// publishes only the first of those five ints.
///
/// Same "linked, not copied" precedent as <see cref="FontCharWidthCatalogReader"/>: the analyser owns
/// the table (see <c>alundra-datas-analyser/AlundraTools/AlundraTools/FontGlyphTable.csv</c>), this
/// converter only reads it and republishes each rectangle in <c>UI/font3.fnt</c> and
/// <c>UI/font3-charset.json</c> (<see cref="Writers.FontWriter"/>).
///
/// Values are RAW: keyed by the game's own byte code, not by Unicode code point.
/// </summary>
public static class FontGlyphTableCatalogReader
{
    public sealed record GlyphEntry(int Width, int Height, int SrcX, int SrcY, int YOffset);

    public sealed record ReadResult(IReadOnlyDictionary<int, GlyphEntry> EntryByRawCode, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var entryByRawCode = new Dictionary<int, GlyphEntry>();

        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < 6
                || !int.TryParse(columns[0], out var code)
                || !int.TryParse(columns[1], out var width)
                || !int.TryParse(columns[2], out var height)
                || !int.TryParse(columns[3], out var srcX)
                || !int.TryParse(columns[4], out var srcY)
                || !int.TryParse(columns[5], out var yOffset))
            {
                warnings.Add($"FontGlyphTable.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            entryByRawCode[code] = new GlyphEntry(width, height, srcX, srcY, yOffset);
        }

        return new ReadResult(entryByRawCode, warnings);
    }
}
