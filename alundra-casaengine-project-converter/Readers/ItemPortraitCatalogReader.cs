namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>ItemPortrait.csv</c> - the item to portrait-sprite correspondence, 88 rows, one per
/// exploitable icon column of <c>g_itemsProperties</c>. docs/plan-e13c-icones-hud.md, slice S2,
/// decision D-E13C-4.
///
/// Unlike the other linked tables this one is not a raw port of a single decompiled array: it is the
/// result of the analyser's <c>probe-portraits</c> probe, which reads <c>GetPortraitImageset</c> for
/// the 89 icon values straight off the game binary and records the <c>SiImage.Signature</c> of the
/// single image each one yields. Item 42 is absent on purpose: its icon column names a sprite-table
/// entry <c>SpriteInfo.cs:93-101</c> never builds, so the game itself has no portrait for it.
///
/// The signature is the packed 64-bit key the extractor deduplicates images on (spritesheet, palette,
/// source x and y, width and height), so it needs <c>long</c>: every real value overflows
/// <c>int</c>. It is exactly the key this converter's own sprite deduplication uses
/// (<see cref="Writers.SpriteWriter.SpriteAssetId"/>), which is how an item resolves to a
/// <c>.sprite</c> this converter has already emitted, without any new image being extracted.
///
/// Same "linked, not copied" precedent as <see cref="MapSoundGroupIndexCatalogReader"/>: the analyser
/// owns the measurement, this converter only reads it and republishes it as
/// <c>Data/item-icon-index.json</c> (<see cref="Writers.ItemsWriter"/>).
/// </summary>
public static class ItemPortraitCatalogReader
{
    /// <summary>The icon column's value, and the portrait image's deduplication signature.</summary>
    public sealed record ItemPortraitEntry(int Icon, long Signature);

    public sealed record ReadResult(
        IReadOnlyDictionary<int, ItemPortraitEntry> PortraitByItemId,
        IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var portraitByItemId = new SortedDictionary<int, ItemPortraitEntry>();

        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < 3
                || !int.TryParse(columns[0], out var itemId)
                || !int.TryParse(columns[1], out var icon)
                || !long.TryParse(columns[2], out var signature))
            {
                warnings.Add($"ItemPortrait.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            portraitByItemId[itemId] = new ItemPortraitEntry(icon, signature);
        }

        return new ReadResult(portraitByItemId, warnings);
    }
}
