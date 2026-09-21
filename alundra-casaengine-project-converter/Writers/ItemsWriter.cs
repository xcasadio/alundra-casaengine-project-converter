using System.Globalization;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Republishes the two item tables the analyser owns, raw, under <c>Data/</c>:
/// docs/plan-e13c-icones-hud.md, slice S2, decisions D-E13C-2 and D-E13C-4.
///
///  - <c>Data/items-properties.json</c> is <c>g_itemsProperties</c> as it stands: an array of 100
///    arrays of 5, outer index the <c>item_id</c>, inner order the original array's column order
///    (slot, replace flag, priority, max count, icon). No field names, no interpretation.
///  - <c>Data/item-icon-index.json</c> maps each item that has a portrait to the asset id of the
///    <c>.sprite</c> this converter has ALREADY emitted for that portrait's image. 88 items; item 42
///    is absent because the game has no portrait record for it.
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
/// Neither file is registered in the asset catalog, like every other raw companion this converter
/// writes (<c>sound-group-index.json</c>, <c>music-index.json</c>, <c>etc-index.json</c>,
/// <c>sprite-records.json</c>, <c>balance.json</c>): <c>json</c> is not a CasaEngine asset type, so
/// nothing loads them back and the verification pass does not look for them.
/// </summary>
public static class ItemsWriter
{
    private const string DataRelativeDirectory = "Data";
    private const string ItemsPropertiesFileName = "items-properties.json";
    private const string ItemIconIndexFileName = "item-icon-index.json";

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

        WriteItemsProperties(targetDirectory, properties.RowsByItemId, report);
        WriteItemIconIndex(targetDirectory, portraits.PortraitByItemId, report);
    }

    private static void WriteItemsProperties(
        string targetDirectory, IReadOnlyList<IReadOnlyList<int>> rowsByItemId, ConversionReport report)
    {
        using (var stream = File.Create(Path.Combine(targetDirectory, ItemsPropertiesFileName)))
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

        report.Increment("Items.PropertiesRows", rowsByItemId.Count);
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
