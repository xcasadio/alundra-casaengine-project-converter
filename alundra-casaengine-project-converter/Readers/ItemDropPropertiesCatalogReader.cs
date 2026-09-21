namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>ItemDropProperties.csv</c> - the RAW port of the original's <c>g_itemDropProperties</c>
/// (<c>StaticVariables.cs:841</c>), 98 records, one per item the new-game unlock loop visits
/// (<c>GameInitializer.cs:408</c>, <c>while (iconIndex &lt; 0x62)</c>). The four byte fields come as the array
/// holds them: <c>Field1</c>, <c>SoundSfxIndex</c>, <c>Field3</c> and <c>Field4</c>.
/// docs/plan-e13c-icones-hud.md, slice S2.b, decision D-E13C-2.
///
/// <c>Field3</c> stays one byte on purpose. Its high bit is what the new game reads to unlock an item
/// (<c>GameInitializer.cs:402</c>), and its low seven bits are read on their own by <c>PlayerManager</c>;
/// splitting it here would decide for the consumer which of the two it wants. <c>Field0</c> is absent from
/// the CSV: a string the decompilation did not recover, empty on every record.
///
/// Same "linked, not copied" precedent as <see cref="ItemsPropertiesCatalogReader"/>: the analyser owns
/// this table, this converter only reads it and republishes it as <c>Data/item-drop-properties.json</c>
/// (<see cref="Writers.ItemsWriter"/>).
/// </summary>
public static class ItemDropPropertiesCatalogReader
{
    /// <summary>The four value columns that follow <c>item_id</c>, in the original record's order.</summary>
    public const int ColumnCount = 4;

    public sealed record ReadResult(IReadOnlyList<IReadOnlyList<int>> RowsByItemId, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var rowsByItemId = new SortedDictionary<int, IReadOnlyList<int>>();

        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < ColumnCount + 1 || !int.TryParse(columns[0], out var itemId))
            {
                warnings.Add($"ItemDropProperties.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            var values = new int[ColumnCount];
            var parsed = true;
            for (var column = 0; column < ColumnCount; column++)
            {
                if (!int.TryParse(columns[column + 1], out var value))
                {
                    parsed = false;
                    break;
                }

                values[column] = value;
            }

            if (!parsed)
            {
                warnings.Add($"ItemDropProperties.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            rowsByItemId[itemId] = values;
        }

        // The rows are republished as an array indexed by item_id, so a hole or a shifted start would
        // silently renumber every item after it.
        if (rowsByItemId.Count > 0
            && (rowsByItemId.Keys.First() != 0 || rowsByItemId.Keys.Last() != rowsByItemId.Count - 1))
        {
            warnings.Add(
                $"ItemDropProperties.csv: expected a contiguous 0..{rowsByItemId.Count - 1} item_id range, "
                + $"got {rowsByItemId.Keys.First()}..{rowsByItemId.Keys.Last()} ({rowsByItemId.Count} rows).");
        }

        return new ReadResult(rowsByItemId.Values.ToList(), warnings);
    }
}
