namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>FontCharWidths.csv</c> - the RAW port of the original's <c>g_fontCharWidthTable</c>
/// (<c>StaticVariables.cs:9484</c>), one row per raw glyph code (0..255), <c>advance</c> holding the
/// exact value the original table carries at <c>[code * 5]</c> - the stride verified against all
/// four <c>TextDecoder.cs</c> advance sites (:214, :237, :448, :607), every one of which reads that
/// same offset (docs/plan-e12-dialogues.md, D-E12-2, §1.5).
///
/// Same "linked, not copied" precedent as <see cref="EntityNameCatalogReader"/>/<c>EntityNames.csv</c>
/// and <see cref="MusicIndexCatalogReader"/>/<c>MapMusicIndex.csv</c>: the analyser owns this table
/// (generated straight off the decompiled array - see
/// <c>alundra-datas-analyser/AlundraTools/AlundraTools/FontCharWidths.csv</c>), this converter only
/// reads it and republishes it as per-glyph <c>xadvance</c> in <c>UI/font3.fnt</c>
/// (<see cref="Writers.FontWriter"/>).
///
/// Values are exported RAW: this reader hands back <c>code -&gt; advance</c> by the game's own byte
/// code, not by Unicode code point - the caller is responsible for applying its own raw-code -&gt;
/// codepoint mapping (and any duplicate-code resolution) before using this table, exactly the way it
/// already resolves glyph identity from ui/font3.json.
/// </summary>
public static class FontCharWidthCatalogReader
{
    public sealed record ReadResult(IReadOnlyDictionary<int, int> AdvanceByRawCode, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var advanceByRawCode = new Dictionary<int, int>();

        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < 2
                || !int.TryParse(columns[0], out var code)
                || !int.TryParse(columns[1], out var advance))
            {
                warnings.Add($"FontCharWidths.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            advanceByRawCode[code] = advance;
        }

        return new ReadResult(advanceByRawCode, warnings);
    }
}
