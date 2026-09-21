namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>ItemsProperties.csv</c> - the RAW port of the original's <c>g_itemsProperties</c>
/// (<c>StaticVariables.cs:737-838</c>), a <c>ushort[]</c> of 100 rows of 5. The columns are
/// documented at <c>StaticVariables.cs:729-734</c>: inventory slot, replace flag, replace priority,
/// max count, and the icon - column 4, the one <c>GetItemTextureIdByItemId</c> reads as
/// <c>g_itemsProperties[itemId * 5 + 4]</c> (<c>GraphicManager.cs:1911-1914</c>).
/// docs/plan-e13c-icones-hud.md, slice S2, decision D-E13C-2.
///
/// Same "linked, not copied" precedent as <see cref="MapSoundGroupIndexCatalogReader"/>: the analyser
/// owns this table (generated straight off the decompiled array, see
/// <c>alundra-datas-analyser/AlundraTools/AlundraTools/ItemsProperties.csv</c>'s own header comment in
/// that project's <c>.csproj</c>), this converter only reads it and republishes it as
/// <c>Data/items-properties.json</c> (<see cref="Writers.ItemsWriter"/>).
///
/// Values are exported RAW on purpose, same reasoning as D-C-2 for the music index. Only column 4 is
/// understood today; the other four drive the original's inventory and replacement rules, which the
/// DLL has yet to port. Naming or interpreting them here would freeze semantics this side of the
/// project has not established.
/// </summary>
public static class ItemsPropertiesCatalogReader
{
    /// <summary>The five value columns that follow <c>item_id</c>, in the original array's order.</summary>
    public const int ColumnCount = 5;

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
                warnings.Add($"ItemsProperties.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
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
                warnings.Add($"ItemsProperties.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
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
                $"ItemsProperties.csv: expected a contiguous 0..{rowsByItemId.Count - 1} item_id range, "
                + $"got {rowsByItemId.Keys.First()}..{rowsByItemId.Keys.Last()} ({rowsByItemId.Count} rows).");
        }

        return new ReadResult(rowsByItemId.Values.ToList(), warnings);
    }
}
