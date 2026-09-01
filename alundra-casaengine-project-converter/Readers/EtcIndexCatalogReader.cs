namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>EtcIndexTable.csv</c> - the RAW port of the first 1024 int16 of the game's own
/// <c>ETC_RES.R</c> file (mirrors <c>EtcResR.cs:7-13</c>'s exact read), one row per index (0..1023),
/// <c>value</c> holding the offset that index resolves to in the ETC string region
/// (docs/plan-e12-dialogues.md, D-E12-6).
///
/// This table is NOT derivable from decompiled code - it only exists inside the game's data file, so
/// unlike <see cref="FontCharWidthCatalogReader"/> it cannot be read off a literal array. Same
/// "linked, not copied" precedent regardless: the analyser owns it (a one-shot dump straight off
/// <c>ETC_RES.R</c> at the game path - see
/// <c>alundra-datas-analyser/AlundraTools/AlundraTools/EtcIndexTable.csv</c>), this converter only
/// reads it and republishes it as <c>Dialogues/etc-index.json</c> (<see cref="Writers.TextWriter"/>).
///
/// <c>GetEtcString(id) = StringByIndex[IndexTable[id]]</c>: the DLL resolves a global string (e.g.
/// the OUI/NON labels at index 0x43/0x44) by looking up this table for the offset, then looking that
/// offset up in <c>Dialogues/global-strings.json</c>. Exported RAW on purpose - resolving offsets to
/// strings here would duplicate global-strings.json's own content and drift the moment either side
/// changes.
/// </summary>
public static class EtcIndexCatalogReader
{
    public sealed record ReadResult(IReadOnlyList<int> ValueByIndex, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var values = new SortedDictionary<int, int>();

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
                || !int.TryParse(columns[0], out var index)
                || !int.TryParse(columns[1], out var value))
            {
                warnings.Add($"EtcIndexTable.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            values[index] = value;
        }

        if (values.Count > 0 && (values.Keys.First() != 0 || values.Keys.Last() != values.Count - 1))
        {
            warnings.Add(
                $"EtcIndexTable.csv: expected a contiguous 0..{values.Count - 1} index range, "
                + $"got {values.Keys.First()}..{values.Keys.Last()} ({values.Count} rows).");
        }

        return new ReadResult(values.Values.ToList(), warnings);
    }
}
