namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads the layout of the main inventory's background boxes - docs/plan-e13d-inventaire.md, slice D3.b,
/// decisions D-E13D-13 and D-E13D-14. Two raw CSVs the analyser generates from the decompilation (slice
/// D3.a), linked into this project like <see cref="ItemsPropertiesCatalogReader"/>'s own table:
///
///  - <c>UiBoxes.csv</c>, <c>box;x;y;width;height</c>: the seven <c>UIBoxConfiguration</c> literals
///    (<c>StaticVariables.cs:11196-11267</c>) in the drawing order of the inventory's seven
///    <c>DisplayUiBoxes</c> calls (<c>MainInventoryManager.cs:914-920</c>). X and Y are screen pixels,
///    width and height count 8 pixel cells; the empty 0 by 0 spacer is included.
///  - <c>UiBoxCells.csv</c>, <c>box;cell;x0;y0;u0;v0;w;h;clut</c>: every <c>SPRT</c> of each box's
///    <c>SpritesA</c> copy, in array order. <c>x0</c>/<c>y0</c> are the cell's screen position with the
///    box at rest, <c>u0</c>/<c>v0</c>/<c>w</c>/<c>h</c> its rectangle in the UI atlas, <c>clut</c> its palette.
///
/// Rows are returned as read, in file order; a malformed row is a warning and is skipped. Deciding what
/// a box's cells mean (where they land, whether their tile exists) is <see cref="Writers.UiBoxBaker"/>'s job.
/// </summary>
public static class UiBoxLayoutReader
{
    public sealed record UiBox(string Name, int X, int Y, int Width, int Height);

    public sealed record UiBoxCell(string Box, int Cell, int X0, int Y0, int U0, int V0, int W, int H, int Clut);

    public sealed record ReadResult(IReadOnlyList<UiBox> Boxes, IReadOnlyList<UiBoxCell> Cells, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string boxesCsvPath, string cellsCsvPath)
    {
        var warnings = new List<string>();
        var boxes = new List<UiBox>();
        var cells = new List<UiBoxCell>();

        foreach (var (lineNumber, columns) in DataRows(boxesCsvPath))
        {
            if (columns.Length < 5 || !TryParseInts(columns, 1, 4, out var values))
            {
                warnings.Add($"UiBoxes.csv: malformed row {lineNumber} ('{string.Join(';', columns)}'), skipped.");
                continue;
            }

            boxes.Add(new UiBox(columns[0], values[0], values[1], values[2], values[3]));
        }

        var boxNames = boxes.Select(box => box.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var (lineNumber, columns) in DataRows(cellsCsvPath))
        {
            if (columns.Length < 9 || !TryParseInts(columns, 1, 8, out var values))
            {
                warnings.Add($"UiBoxCells.csv: malformed row {lineNumber} ('{string.Join(';', columns)}'), skipped.");
                continue;
            }

            if (!boxNames.Contains(columns[0]))
            {
                warnings.Add($"UiBoxCells.csv: row {lineNumber} names box '{columns[0]}', which UiBoxes.csv does not list; skipped.");
                continue;
            }

            cells.Add(new UiBoxCell(columns[0], values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]));
        }

        return new ReadResult(boxes, cells, warnings);
    }

    /// <summary>Every non-blank line after the header, with its 1-based line number, split on ';'.</summary>
    private static IEnumerable<(int LineNumber, string[] Columns)> DataRows(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            if (!string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                yield return (lineIndex + 1, lines[lineIndex].Split(';'));
            }
        }
    }

    private static bool TryParseInts(string[] columns, int first, int count, out int[] values)
    {
        values = new int[count];
        for (var i = 0; i < count; i++)
        {
            if (!int.TryParse(columns[first + i], out values[i]))
            {
                return false;
            }
        }

        return true;
    }
}
