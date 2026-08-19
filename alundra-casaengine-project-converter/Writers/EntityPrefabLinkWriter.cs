using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 3.5: links every "Entities" object-layer record of every map's .tileMap (written by Phase 1)
/// to the prefab Phase 3 wrote for its sprite bank, by adding a "PrefabAssetId" custom property
/// carrying that prefab's Guid. Runs after Phase 3 because it needs the bank -&gt; prefab id map
/// <see cref="SpriteWriter.ConvertSprites"/> returns, and it read-modify-writes the .tileMap the same
/// way <see cref="CellMetadataWriter"/> already does for Phase 2.
///
/// Resolution chain (see docs/demarrage-nouvelle-partie.md section 1.2 for the cross-check this was
/// verified against - map 389's Entity_0, SpriteTableIndex 25, EntityName "Bloc transparent (1x1x2)"):
///  - AlundraEngine.GameEngine.GetSpriteFromSpriteTable (analyser sources,
///    AlundraTools/AlundraEngine/GameEngine.cs) indexes one of two flat arrays by the record's raw,
///    unmodified SpriteTableIndex: SpriteDirection bit 0x80 clear (isMapSprite == false, the common
///    case) reads DatasBin.AlundraGameMap.SpriteInfo.SpriteRecords - i.e. map_alundra.json, which
///    SpriteBankReader treats as the "hero" source (SpriteBank.IsHeroBank) even though the vast
///    majority of its records are ordinary map entities/items, not the hero himself; bit 0x80 set
///    reads CurrentMap.SpriteInfo.SpriteRecords - the record's own map's local table.
///  - Every SpriteRecords[i].Header.Sector5Id equals its own ordinal index i (verified against all
///    257-entry SpriteRecords arrays in data-extracted/data/*.json, hero and regular maps alike; the
///    only mismatches are the unused/placeholder slots, which carry no Header at all). SpriteBank.BankKey
///    is exactly Sector5Id (prefixed "hero_" for the hero source), so the record's own
///    SpriteTableIndex already IS the bank key's numeric part - no JSON re-reads are needed here, only
///    the SpriteDirection bit and the id-space prefix.
///  - The resulting BankKey looks up the prefab id SpriteWriter.ConvertSprites returned. A miss (index
///    outside the known bank set) or a record with IsEnabled == "0" leaves the key absent rather than
///    writing a dangling reference - see WriteLink's summary for why each case is or isn't warned
///    about.
/// </summary>
public static class EntityPrefabLinkWriter
{
    private const string EntitiesLayerName = "Entities";
    private const string PrefabAssetIdPropertyKey = "PrefabAssetId";
    private const int MapSpriteDirectionBit = 0x80;

    // The plan's acceptance criterion for this pass, enforced the same way WorldWriter enforces its
    // own entity-count invariants: every one of the 9 741 "Entities" records across the 483 shipped
    // maps (Worlds.Entities - both enabled and disabled ones; disabled records still name a real bank,
    // they are just never spawned) resolves to a known bank. Only meaningful on a full run: with
    // --maps the totals are a subset by construction, so the check is skipped rather than made to fail.
    private const int ExpectedLinksResolved = 9741;

    public static void LinkMaps(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        IReadOnlyDictionary<string, Guid> prefabAssetIdsByBankKey,
        ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            LinkMap(outputDirectory, mapIndex, mapLocations, prefabAssetIdsByBankKey, report);
        }

        if (mapFilter is not { Count: > 0 })
        {
            CheckInvariant(report);
        }
    }

    private static void CheckInvariant(ConversionReport report)
    {
        var resolved = report.Counters.GetValueOrDefault("PrefabLinks.Resolved");
        if (resolved != ExpectedLinksResolved)
        {
            report.Errors.Add(
                $"PrefabLinks: invariant 'PrefabLinks.Resolved' is {resolved}, expected {ExpectedLinksResolved}.");
        }

        var unresolved = report.Counters.GetValueOrDefault("PrefabLinks.Unresolved");
        if (unresolved != 0)
        {
            report.Errors.Add($"PrefabLinks: invariant 'PrefabLinks.Unresolved' is {unresolved}, expected 0.");
        }
    }

    private static void LinkMap(
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        IReadOnlyDictionary<string, Guid> prefabAssetIdsByBankKey,
        ConversionReport report)
    {
        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var tileMapRelativePath = location.TileMapRelativePath;
        var tileMapFullPath = Path.Combine(outputDirectory, tileMapRelativePath);

        if (!File.Exists(tileMapFullPath))
        {
            report.Warnings.Add($"map_{mapIndex}: tileMap asset not found at '{tileMapFullPath}' (run Phase 1 first).");
            return;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFullPath)));

        var entitiesLayer = tileMapData.ObjectLayers.FirstOrDefault(
            layer => string.Equals(layer.Name, EntitiesLayerName, StringComparison.Ordinal));
        if (entitiesLayer == null)
        {
            return;
        }

        foreach (var record in entitiesLayer.Objects)
        {
            WriteLink(record, mapIndex, prefabAssetIdsByBankKey, report);
        }

        EditorAssetWriterService.SaveAsset(tileMapRelativePath, tileMapData);
        report.Increment("Maps.PrefabLinks");
    }

    /// <summary>
    /// Resolves one "Entities" record's SpriteTableIndex to a bank key (see the class summary) and,
    /// on a hit, adds the prefab's Guid as the record's "PrefabAssetId" custom property.
    ///
    /// A miss is expected, not warned about, for a record whose IsEnabled is "0": GameEngine.GetEntityRecord
    /// never spawns it, so its SpriteTableIndex was never meant to resolve to anything live (see
    /// EntityRecordMapper's own "Deliberately NOT mapped" notes on IsEnabled for the same reasoning).
    /// A miss on an ENABLED record - an out-of-range index, or an index with no known bank - is
    /// unexpected: the game would have failed to spawn it too, so it is worth surfacing as a warning
    /// rather than silently dropped.
    /// </summary>
    private static void WriteLink(
        TileMapObjectData record,
        int mapIndex,
        IReadOnlyDictionary<string, Guid> prefabAssetIdsByBankKey,
        ConversionReport report)
    {
        if (!record.CustomProperties.TryGetValue("SpriteTableIndex", out var spriteTableIndexRaw)
            || !int.TryParse(spriteTableIndexRaw, out var spriteTableIndex)
            || !record.CustomProperties.TryGetValue("SpriteDirection", out var spriteDirectionRaw)
            || !int.TryParse(spriteDirectionRaw, out var spriteDirection))
        {
            report.Increment("PrefabLinks.Unresolved");
            return;
        }

        var isMapSprite = (spriteDirection & MapSpriteDirectionBit) != 0;
        var bankKey = isMapSprite ? $"{spriteTableIndex}" : $"hero_{spriteTableIndex}";

        if (!prefabAssetIdsByBankKey.TryGetValue(bankKey, out var prefabAssetId))
        {
            report.Increment("PrefabLinks.Unresolved");

            var isEnabled = record.CustomProperties.TryGetValue("IsEnabled", out var isEnabledRaw)
                && isEnabledRaw != "0";
            if (isEnabled)
            {
                report.Warnings.Add(
                    $"map_{mapIndex}: {record.Name}: SpriteTableIndex {spriteTableIndex} "
                    + $"(bank '{bankKey}') has no known prefab; PrefabAssetId left unset.");
            }

            return;
        }

        record.CustomProperties[PrefabAssetIdPropertyKey] = prefabAssetId.ToString();
        report.Increment("PrefabLinks.Resolved");
    }
}
