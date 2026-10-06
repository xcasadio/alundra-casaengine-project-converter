using System.Text;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 9: the effects of each map (docs/formats/effects.md, plan E19.g G1). The binary draws map and
/// object effects (doors of Inoa, the aura of the 476, rays, sparks...) as free quads cut from an effect
/// sheet; none of it was exported. Output:
///  - for each map that has effect records or an effect table:
///    <c>Maps/{Zone}/{Name}-{id}/effects/{Name}-{id}.effects.json</c>, a raw companion (not a CasaEngine
///    asset, nothing in the editor loads it - same convention as events.json and the backdrop companion), and,
///    when the map has its own effect table, its sheet <c>map_{id}_effectsheet.png</c> (+ .texture wrapper)
///    through <see cref="TextureAssetWriter"/>;
///  - the global table (the 29 effects shared by every map, <c>map_alundra.json</c>):
///    <c>Data/effects/effects-global.json</c> and <c>Data/effects/map_alundra_effectsheet.png</c> (+ .texture).
/// Effects are not exported as <c>.sprite</c> assets: the PS1 mode (ABR) belongs to the quad, not to the cell,
/// and an effect quad has four free corners where a sprite has a rectangle and an origin.
/// </summary>
public static class EffectWriter
{
    // The plan's G1-R7 invariants (docs/plan-e19-opcodes.md, "E19.g G1"), enforced only on a full run - same
    // reasoning as BackdropWriter and WorldWriter.CheckInvariants: with --maps the totals are a subset by construction.
    private const int ExpectedMapCorpusSize = 483;

    private static readonly (string Counter, int Expected)[] ExpectedTotals =
    {
        ("Effects.Records", 544),
        ("Effects.RecordsSpawnAtLoad", 251),
        ("Effects.RecordsMapTable", 350),
        ("Effects.RecordsGlobalTable", 194),
        ("Effects.Tables", 165),
        ("Effects.Animations", 363),
        ("Effects.AnimationSlotsDropped", 83),
        ("Effects.Frames", 5148),
        ("Effects.ImageSets", 2832),
        ("Effects.Images", 12307),
        ("Effects.ImagesDegenerateDropped", 23),
        ("Effects.Companions", 157),
        ("Effects.Sheets", 87),
        ("Effects.UnresolvedRecords", 0),
    };

    private const int SpawnAtLoadFlag = 0x40;
    private const int MapTableFlag = 0x80;

    private static readonly string GlobalDirectory = Path.Combine("Data", "effects");
    private static readonly string GlobalCompanionRelativePath = Path.Combine(GlobalDirectory, "effects-global.json");

    public static void ConvertEffects(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var discoveredMapIndices = MapDiscovery.DiscoverMapIndices(inputDirectory);
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : discoveredMapIndices;
        var isFullRun = discoveredMapIndices.Count == ExpectedMapCorpusSize
                        && discoveredMapIndices.All(mapIndices.Contains);

        // Listed first so that a clean run shows every counter, zeros included.
        foreach (var (counter, _) in ExpectedTotals)
        {
            report.Increment(counter, 0);
        }

        var textureCache = new Dictionary<string, Guid>();
        var dataDirectory = Path.Combine(inputDirectory, "data");

        var globalBank = ReadGlobalBank(dataDirectory, report);
        var globalTables = globalBank?.Tables ?? new List<EffectTable>();
        if (globalBank is not null)
        {
            CountTables(globalBank, report);
        }

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(dataDirectory, outputDirectory, mapIndex, mapLocations, globalTables, textureCache, report);
        }

        if (globalBank is not null)
        {
            WriteGlobal(dataDirectory, outputDirectory, globalBank, textureCache, report);
        }

        // The sheets were registered through EditorAssetCatalogService.Add (TextureAssetWriter.EnsureTexture), but only
        // Save() persists the catalog to AssetInfos.json - and this phase runs after every other writer's own Save(),
        // so skipping it would silently drop every entry (the trap of BackdropWriter).
        EditorAssetCatalogService.Save();

        if (isFullRun)
        {
            CheckInvariants(report);
        }
    }

    private static EffectBank? ReadGlobalBank(string dataDirectory, ConversionReport report)
    {
        var globalMapPath = Path.Combine(dataDirectory, "map_alundra.json");
        if (!File.Exists(globalMapPath))
        {
            return null;
        }

        try
        {
            var bank = EffectBankReader.Read(globalMapPath);
            foreach (var error in bank.Errors)
            {
                report.Errors.Add($"map_alundra: effects - {error}");
            }

            return bank;
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_alundra: failed to read effects - {exception.Message}");
            return null;
        }
    }

    private static void ConvertMap(
        string dataDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        IReadOnlyList<EffectTable> globalTables,
        Dictionary<string, Guid> textureCache,
        ConversionReport report)
    {
        var nativeMapPath = Path.Combine(dataDirectory, $"map_{mapIndex}.json");
        if (!File.Exists(nativeMapPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{nativeMapPath}'.");
            return;
        }

        EffectBank bank;
        try
        {
            bank = EffectBankReader.Read(nativeMapPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read effects - {exception.Message}");
            return;
        }

        if (bank.Records.Count == 0 && bank.Tables.Count == 0)
        {
            return;
        }

        foreach (var error in bank.Errors)
        {
            report.Errors.Add($"map_{mapIndex}: effects - {error}");
        }

        CountTables(bank, report);
        CountAndResolveRecords(bank, mapIndex, globalTables, report);

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);

        string? sheetTextureAssetId = null;
        if (bank.Tables.Any(table => table.Animations.Count > 0))
        {
            var sheetPath = Path.Combine(dataDirectory, $"map_{mapIndex}_effectsheet.png");
            if (File.Exists(sheetPath))
            {
                sheetTextureAssetId = TextureAssetWriter.EnsureTexture(
                    sheetPath, location.EffectsDirectory, outputDirectory, textureCache).ToString();
                report.Increment("Effects.Sheets");
            }
            else
            {
                report.Errors.Add($"map_{mapIndex}: effect sheet not found at '{sheetPath}'.");
            }
        }

        var companionPath = Path.Combine(outputDirectory, location.EffectsRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(companionPath)!);
        File.WriteAllText(companionPath, BuildCompanion(mapIndex, sheetTextureAssetId, bank.Records, bank.Tables));

        report.Increment("Effects.Companions");
    }

    private static void WriteGlobal(
        string dataDirectory,
        string outputDirectory,
        EffectBank globalBank,
        Dictionary<string, Guid> textureCache,
        ConversionReport report)
    {
        if (globalBank.Tables.Count == 0)
        {
            return;
        }

        string? sheetTextureAssetId = null;
        var sheetPath = Path.Combine(dataDirectory, "map_alundra_effectsheet.png");
        if (File.Exists(sheetPath))
        {
            sheetTextureAssetId = TextureAssetWriter.EnsureTexture(
                sheetPath, GlobalDirectory, outputDirectory, textureCache).ToString();
            report.Increment("Effects.Sheets");
        }
        else if (globalBank.Tables.Any(table => table.Animations.Count > 0))
        {
            report.Errors.Add($"map_alundra: effect sheet not found at '{sheetPath}'.");
        }

        var companionPath = Path.Combine(outputDirectory, GlobalCompanionRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(companionPath)!);
        File.WriteAllText(companionPath, BuildCompanion(null, sheetTextureAssetId, null, globalBank.Tables));
    }

    private static void CountTables(EffectBank bank, ConversionReport report)
    {
        report.Increment("Effects.Tables", bank.Tables.Count);
        report.Increment("Effects.Animations", bank.Tables.Sum(table => table.Animations.Count));
        report.Increment("Effects.AnimationSlotsDropped", bank.AnimationSlotsDropped);
        report.Increment("Effects.Frames", bank.Frames);
        report.Increment("Effects.ImageSets", bank.Tables.Sum(table => table.ImageSets.Count));
        report.Increment("Effects.Images", bank.Images);
        report.Increment("Effects.ImagesDegenerateDropped", bank.ImagesDegenerateDropped);
    }

    private static void CountAndResolveRecords(
        EffectBank bank, int mapIndex, IReadOnlyList<EffectTable> globalTables, ConversionReport report)
    {
        for (var index = 0; index < bank.Records.Count; index++)
        {
            var record = bank.Records[index];
            report.Increment("Effects.Records");

            if ((record.Flags & SpawnAtLoadFlag) != 0)
            {
                report.Increment("Effects.RecordsSpawnAtLoad");
            }

            var usesMapTable = (record.Flags & MapTableFlag) != 0;
            report.Increment(usesMapTable ? "Effects.RecordsMapTable" : "Effects.RecordsGlobalTable");

            var tables = usesMapTable ? bank.Tables : globalTables;
            var resolved = record.Effect >= 0 && record.Effect < tables.Count
                           && record.Anim >= 0 && record.Anim < tables[record.Effect].Animations.Count;
            if (!resolved)
            {
                report.Increment("Effects.UnresolvedRecords");
                report.Errors.Add(
                    $"map_{mapIndex}: effect record {index} (flags 0x{record.Flags:X2}) names effect {record.Effect} " +
                    $"animation {record.Anim} of the {(usesMapTable ? "map" : "global")} table, which does not exist.");
            }
        }
    }

    /// <summary>
    /// The companion is written by hand: the top-level members and each record, animation and image set sit on
    /// their own line (compact inside), so that a 12 000-image export stays readable and diffable. The order of the
    /// keys and the number formats are fixed (integers and booleans only), for a stable double export.
    /// </summary>
    private static string BuildCompanion(
        int? mapIndex, string? sheetTextureAssetId, IReadOnlyList<EffectRecord>? records, IReadOnlyList<EffectTable> tables)
    {
        var builder = new StringBuilder();
        builder.Append("{\n");

        if (mapIndex is not null)
        {
            builder.Append("  \"MapIndex\": ").Append(mapIndex.Value).Append(",\n");
        }

        builder.Append("  \"SheetTextureAssetId\": ")
            .Append(sheetTextureAssetId is null ? "null" : JsonSerializer.Serialize(sheetTextureAssetId))
            .Append(",\n");

        if (records is not null)
        {
            builder.Append("  \"Records\": ");
            AppendLines(builder, records, "    ", record => JsonSerializer.Serialize(record));
            builder.Append(",\n");
        }

        builder.Append("  \"Effects\": ");
        if (tables.Count == 0)
        {
            builder.Append("[]");
        }
        else
        {
            builder.Append("[\n");
            for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                var table = tables[tableIndex];
                builder.Append("    {\n      \"Animations\": ");
                AppendLines(builder, table.Animations, "        ", animation => JsonSerializer.Serialize(animation));
                builder.Append(",\n      \"ImageSets\": ");
                AppendLines(builder, table.ImageSets, "        ", imageSet => JsonSerializer.Serialize(imageSet));
                builder.Append("\n    }").Append(tableIndex < tables.Count - 1 ? ",\n" : "\n");
            }

            builder.Append("  ]");
        }

        builder.Append("\n}\n");
        return builder.ToString();
    }

    private static void AppendLines<T>(StringBuilder builder, IReadOnlyList<T> items, string indent, Func<T, string> serialize)
    {
        if (items.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append("[\n");
        for (var index = 0; index < items.Count; index++)
        {
            builder.Append(indent).Append(serialize(items[index])).Append(index < items.Count - 1 ? ",\n" : "\n");
        }

        // The closing bracket sits two columns left of the items.
        builder.Append(indent[..^2]).Append(']');
    }

    private static void CheckInvariants(ConversionReport report)
    {
        foreach (var (counter, expected) in ExpectedTotals)
        {
            var actual = report.Counters.GetValueOrDefault(counter);
            if (actual != expected)
            {
                report.Errors.Add($"Effects: invariant '{counter}' is {actual}, expected {expected}.");
            }
        }
    }
}
