using System.Globalization;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Republishes the three item tables the analyser owns, raw, under <c>Data/</c>:
/// docs/plan-e13c-icones-hud.md, slices S2 and S2.b, decisions D-E13C-2 and D-E13C-4.
///
///  - <c>Data/items-properties.json</c> is <c>g_itemsProperties</c> as it stands: an array of 100
///    arrays of 5, outer index the <c>item_id</c>, inner order the original array's column order
///    (slot, replace flag, priority, max count, icon). No field names, no interpretation.
///  - <c>Data/item-icon-index.json</c> maps each item that has a portrait to the asset id of the
///    <c>.sprite</c> this converter has ALREADY emitted for that portrait's image. 88 items; item 42
///    is absent because the game has no portrait record for it.
///  - <c>Data/item-drop-properties.json</c> is <c>g_itemDropProperties</c> as it stands: an array of
///    98 arrays of 4, outer index the <c>item_id</c>, inner order the original record's byte fields
///    (field 1, sound effect index, field 3, field 4). Field 3 stays one byte: its high bit unlocks
///    the item at the start of a new game and its low seven bits mean something else to the game.
///
/// Nothing here extracts or writes an image. The portrait of an item turns out to be an image the
/// animation pass already reached, so the whole correspondence is a lookup: the signature in
/// <c>ItemPortrait.csv</c> is the very key the sprite deduplication uses, and every bank read out of
/// <c>map_alundra.json</c> shares one spritesheet
/// (<see cref="Readers.SpriteBankReader.AlundraSpritesheetFileName"/>), so
/// <see cref="SpriteWriter.SpriteAssetId"/> names the asset outright.
///
/// That recomputation is never trusted on its own. An id is published only when the asset catalog
/// actually holds it AND the entry's name is <c>sprite_{signature}</c>, which is what
/// <see cref="SpriteWriter"/> names a sprite after (so the check ties each item to ITS OWN
/// signature, not merely to some existing sprite). Anything else is an error in the report and the
/// item is dropped rather than published with a dangling id.
///
/// None of these files is registered in the asset catalog, like every other raw companion this converter
/// writes (<c>sound-group-index.json</c>, <c>music-index.json</c>, <c>etc-index.json</c>,
/// <c>sprite-records.json</c>, <c>balance.json</c>): <c>json</c> is not a CasaEngine asset type, so
/// nothing loads them back and the verification pass does not look for them.
/// </summary>
public static class ItemsWriter
{
    private const string DataRelativeDirectory = "Data";
    private const string ItemsPropertiesFileName = "items-properties.json";
    private const string ItemIconIndexFileName = "item-icon-index.json";
    private const string ItemDropPropertiesFileName = "item-drop-properties.json";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static void ConvertItems(string outputDirectory, ConversionReport report)
    {
        var propertiesCsvPath = Path.Combine(AppContext.BaseDirectory, "ItemsProperties.csv");
        if (!File.Exists(propertiesCsvPath))
        {
            report.Errors.Add(
                $"ItemsProperties.csv not found at '{propertiesCsvPath}'; "
                + $"{DataRelativeDirectory}/{ItemsPropertiesFileName} not written.");
            return;
        }

        var portraitCsvPath = Path.Combine(AppContext.BaseDirectory, "ItemPortrait.csv");
        if (!File.Exists(portraitCsvPath))
        {
            report.Errors.Add(
                $"ItemPortrait.csv not found at '{portraitCsvPath}'; "
                + $"{DataRelativeDirectory}/{ItemIconIndexFileName} not written.");
            return;
        }

        var properties = ItemsPropertiesCatalogReader.Read(propertiesCsvPath);
        foreach (var warning in properties.Warnings)
        {
            report.Warnings.Add(warning);
        }

        var portraits = ItemPortraitCatalogReader.Read(portraitCsvPath);
        foreach (var warning in portraits.Warnings)
        {
            report.Warnings.Add(warning);
        }

        var targetDirectory = Path.Combine(outputDirectory, DataRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        WriteRowTable(targetDirectory, ItemsPropertiesFileName, properties.RowsByItemId);
        report.Increment("Items.PropertiesRows", properties.RowsByItemId.Count);
        WriteItemIconIndex(targetDirectory, portraits.PortraitByItemId, report);
        WriteItemDropProperties(targetDirectory, report);
    }

    /// <summary>
    /// The unlock table stands on its own: its CSV is resolved and checked here rather than with the two
    /// above, so that a missing third table is reported as its own error and never takes down files
    /// that do not depend on it.
    /// </summary>
    private static void WriteItemDropProperties(string targetDirectory, ConversionReport report)
    {
        var dropCsvPath = Path.Combine(AppContext.BaseDirectory, "ItemDropProperties.csv");
        if (!File.Exists(dropCsvPath))
        {
            report.Errors.Add(
                $"ItemDropProperties.csv not found at '{dropCsvPath}'; "
                + $"{DataRelativeDirectory}/{ItemDropPropertiesFileName} not written.");
            return;
        }

        var drops = ItemDropPropertiesCatalogReader.Read(dropCsvPath);
        foreach (var warning in drops.Warnings)
        {
            report.Warnings.Add(warning);
        }

        WriteRowTable(targetDirectory, ItemDropPropertiesFileName, drops.RowsByItemId);
        report.Increment("Items.DropPropertiesRows", drops.RowsByItemId.Count);
    }

    /// <summary>A raw table: one JSON array per item, indexed by <c>item_id</c>, values as read.</summary>
    private static void WriteRowTable(
        string targetDirectory, string fileName, IReadOnlyList<IReadOnlyList<int>> rowsByItemId)
    {
        using (var stream = File.Create(Path.Combine(targetDirectory, fileName)))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartArray();
            foreach (var row in rowsByItemId)
            {
                writer.WriteStartArray();
                foreach (var value in row)
                {
                    writer.WriteNumberValue(value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }
    }

    private static void WriteItemIconIndex(
        string targetDirectory,
        IReadOnlyDictionary<int, ItemPortraitCatalogReader.ItemPortraitEntry> portraitByItemId,
        ConversionReport report)
    {
        var catalogedById = new Dictionary<Guid, AssetInfo>();
        foreach (var assetInfo in EditorAssetCatalogService.AssetInfos)
        {
            catalogedById[assetInfo.Id] = assetInfo;
        }

        var iconAssetIdByItemId = new SortedDictionary<int, Guid>();
        var unresolved = 0;

        foreach (var (itemId, portrait) in portraitByItemId)
        {
            var assetId = SpriteWriter.SpriteAssetId(
                SpriteBankReader.AlundraSpritesheetFileName, portrait.Signature);
            var expectedName = $"sprite_{portrait.Signature}";

            if (!catalogedById.TryGetValue(assetId, out var assetInfo)
                || !string.Equals(assetInfo.Name, expectedName, StringComparison.Ordinal))
            {
                unresolved++;
                report.Errors.Add(
                    $"ItemPortrait.csv: item {itemId} (icon {portrait.Icon}, signature {portrait.Signature}) "
                    + $"resolves to asset id {assetId}, which no emitted sprite named '{expectedName}' carries; "
                    + $"item omitted from {DataRelativeDirectory}/{ItemIconIndexFileName}.");
                continue;
            }

            iconAssetIdByItemId[itemId] = assetId;
        }

        using (var stream = File.Create(Path.Combine(targetDirectory, ItemIconIndexFileName)))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var (itemId, assetId) in iconAssetIdByItemId)
            {
                writer.WriteString(itemId.ToString(CultureInfo.InvariantCulture), assetId.ToString());
            }

            writer.WriteEndObject();
        }

        report.Increment("Items.IconsIndexed", iconAssetIdByItemId.Count);
        report.Increment("Items.IconsUnresolved", unresolved);
    }
}
