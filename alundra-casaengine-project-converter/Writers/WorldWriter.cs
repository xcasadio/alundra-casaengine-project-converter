using System.Globalization;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// One converted map's spawn point, in every unit the two engines disagree about.
/// See <see cref="WorldWriter.ResolveTileCentreSpawn"/> for the conversion rule.
/// </summary>
public readonly record struct TileCentreSpawn(int PixelX, int PixelY, float WorldX, float WorldY, float WorldZ);

/// <summary>
/// Phase 6, first half: one .world per map - at the root of that map's own folder,
/// Maps/{Zone}/{Name}-{id}/{Name}-{id}.world, see <see cref="MapLocation"/> - plus
/// Maps/world-index.json and the project's FirstWorldLoaded.
///
/// What a world contains, and why it contains so little:
///  - entity "tileMap": a TileMapComponent pointing at the map's .tileMap asset, placed at the
///    origin. The tilemap renderer draws tile (x, y) at (position.X + x*tileWidth,
///    position.Y - y*tileHeight), so a tilemap at the origin occupies X in [0, width*24] and Y in
///    [0, -height*16] - the same frame the entity/spawn conversion below targets.
///  - entity "camera": a Camera3dIn2dAxisComponent. See the camera note below.
///  - entity "PlayerStart": a PlayerStartComponent, which is what World.InitializePlayerControllers
///    looks for when it spawns the default pawn.
///  - nothing else. The map's entities, portals and map events are NOT duplicated here as
///    entity_references, even though the plan's Phase 6 text asks for it. Two reasons, both
///    verified: (a) CasaEngine's Entity has no custom-property bag, so the native fields the plan
///    wants preserved would have nowhere to live; (b) Phase 1's Tiled import already preserved
///    every one of them, in the .tileMap's own object layers named Portals / MapEvents / Entities,
///    each object carrying its native fields in custom_properties. The gameplay DLL will
///    instantiate them from TileMapData.ObjectLayers. Duplicating 9 631 entities into 483 worlds
///    would freeze component choices before that DLL exists, for no gain.
///
/// Camera choice - Camera3dIn2dAxisComponent, not CameraTargeted2dComponent:
///  neither camera's target is serialized (the editor's world writer saves a target only for
///  ArcBallCameraComponent), so whichever is written here starts with its default target and the
///  gameplay DLL has to aim it at the hero anyway. The difference is what happens *before* that
///  happens. Camera3dIn2dAxisComponent's target is a Vector3 defaulting to (0,0,0) and it places
///  the eye at target + (0, 0, +d) looking back along -Z, which is the orientation the 2D
///  rendering path is built for; the map renders the right way round with no gameplay code at all.
///  CameraTargeted2dComponent's target is an Entity reference (necessarily null in a freshly
///  converted project) and its ComputeViewMatrix places the eye at (0, 0, -d), i.e. behind the
///  scene plane, which mirrors the image until something assigns a target. RPGDemo's DefaultWorld
///  makes the same choice, and its ScriptWorld re-aims the camera in OnBeginPlay - the entity is
///  named "camera" here for exactly that reason, so an Alundra world proxy can find it the same
///  way.
///
/// player_startup_settings_asset_id is deliberately left at Guid.Empty. The .gameMode asset it
/// would point at needs the hero .entity, which is step E2 of docs/demarrage-nouvelle-partie.md
/// and outside Phase 6; writing one now would only create a dangling reference in 483 files.
///
/// Serialization: the .world JSON is built directly as a JObject rather than through
/// EditorWorldEditingService.AddEntity + EditorWorldWriter.SaveWorld. That path cannot run here:
/// World.AddEntityReferenceImmediate calls Entity.InitializeWithWorld, and
/// TileMapComponent.InitializeWithWorld dereferences Owner.World.Game (null outside a running
/// CasaEngineGame) to fetch the SpriteRendererComponent and to load the tilemap asset. The
/// converter is headless, so it emits the document the engine's loader reads
/// (World.Load / Entity.Load / SceneComponent.Load) and the tests assert that round-trip.
/// Ids are minted with Ids.For(...) so two runs produce byte-identical worlds; the referenced
/// .tileMap ids still come from Phase 1, which does not mint deterministic ids yet.
/// </summary>
public static class WorldWriter
{
    private const string WorldIndexFileName = "world-index.json";

    // docs/guidelines-runtime-alundra-casaengine.md section 1: uniform over all 483 maps.
    private const int AlundraTileWidth = 24;
    private const int AlundraTileHeight = 16;

    // docs/demarrage-nouvelle-partie.md section 2.1: the only documented spawn in the whole game.
    // g_saveData.InitialMapId = 389, CameraTileX/Y/Z = 33 / 59 / 0.
    private const int NewGameMapId = 389;
    private const int NewGameSpawnTileX = 33;
    private const int NewGameSpawnTileY = 59;
    private const int NewGameSpawnTileZ = 0;

    // Ground truth computed from data-extracted before this writer existed; see the class summary
    // of the invariant check below for what each number means.
    private const int ExpectedWorlds = 483;
    private const int ExpectedEntities = 9741;
    private const int ExpectedEntitiesEnabled = 9631;
    private const int ExpectedPortals = 3316;
    private const int ExpectedMapEvents = 1714;

    public static void ConvertWorlds(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        // The invariants below are the totals of the shipped Alundra corpus, so they may only be
        // asserted when that whole corpus was actually converted - which is about the set of maps
        // processed, not about how the caller spelled it (--maps listing all 483 ids is still a full
        // run). Requiring the discovered corpus to be the expected size as well keeps a small test
        // fixture, which trivially "covers everything discovered", from being judged against them.
        var discoveredMapIndices = MapDiscovery.DiscoverMapIndices(inputDirectory);
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : discoveredMapIndices;
        var isFullRun = discoveredMapIndices.Count == ExpectedWorlds
                        && discoveredMapIndices.All(mapIndices.Contains);

        var worldPathsByMapId = new SortedDictionary<int, string>();

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, mapLocations, worldPathsByMapId, report);
        }

        WriteWorldIndex(outputDirectory, worldPathsByMapId, report);
        EditorAssetCatalogService.Save();

        SetFirstWorldLoaded(outputDirectory, worldPathsByMapId, report);

        if (isFullRun)
        {
            CheckInvariants(report);
        }
    }

    /// <summary>
    /// Converts an Alundra tile coordinate to a CasaEngine world position, for a spawn placed at
    /// the centre of its tile. Follows docs/guidelines-runtime-alundra-casaengine.md:
    ///  - section 2.2, runtime tile coordinate to pixels:
    ///    pixelX = tileX*24 + 12, pixelY = tileY*16 + 8 (the +12/+8 centre the point in the tile).
    ///  - section 2.3, pixels to CasaEngine: X = pixelX, Y = -pixelY. Alundra's Y points down, the
    ///    tilemap renderer's Y points up, so the sign flip is mandatory.
    ///  - section 2.3 again, elevation: Alundra's Z is not a camera depth, it shifts the sprite up
    ///    the screen by tileZ*16. In a Y-up frame that is Y += tileZ*16. CasaEngine's Z is left at
    ///    0, because here it only orders the render layers (0 / 0.1 / 0.2 / 0.3).
    /// For the documented New Game spawn, tile (33, 59, 0), this yields pixels (804, 952) and world
    /// (804, -952, 0).
    /// </summary>
    public static TileCentreSpawn ResolveTileCentreSpawn(int tileX, int tileY, int tileZ)
    {
        var pixelX = tileX * AlundraTileWidth + AlundraTileWidth / 2;
        var pixelY = tileY * AlundraTileHeight + AlundraTileHeight / 2;

        return new TileCentreSpawn(
            pixelX,
            pixelY,
            pixelX,
            -pixelY + tileZ * AlundraTileHeight,
            0f);
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        IDictionary<int, string> worldPathsByMapId,
        ConversionReport report)
    {
        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);

        var tileMapRelativePath = location.TileMapRelativePath;
        var tileMapFullPath = Path.Combine(outputDirectory, tileMapRelativePath);
        if (!File.Exists(tileMapFullPath))
        {
            report.Warnings.Add($"map_{mapIndex}: tileMap asset not found at '{tileMapFullPath}' (run Phase 1 first); no world written.");
            return;
        }

        TileMapData tileMapData;
        try
        {
            tileMapData = new TileMapData();
            tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFullPath)));
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to reload '{tileMapRelativePath}' - {exception.Message}");
            return;
        }

        var spawn = ResolveSpawn(mapIndex, tileMapData, report);

        var worldRelativePath = location.WorldRelativePath;
        Directory.CreateDirectory(Path.Combine(outputDirectory, location.MapFolder));

        var worldId = Ids.For($"world:{mapIndex}");
        var worldNode = BuildWorld(mapIndex, worldId, location.FileBaseName, tileMapData.Id, spawn);

        EditorAssetWriterService.SaveDocument(worldRelativePath, worldNode);
        EditorAssetCatalogService.Add(new AssetInfo(worldId)
        {
            Name = location.FileBaseName,
            FileName = worldRelativePath,
        });

        worldPathsByMapId[mapIndex] = worldRelativePath;
        report.Increment("Worlds");

        CountSourceObjects(inputDirectory, mapIndex, report);
    }

    /// <summary>
    /// Map 389 is the only map with an intrinsic spawn (the New Game save state names it). Every
    /// other map is entered through a portal, which carries its own DestTileX/DestTileY, so their
    /// PlayerStart is a placeholder at the map centre and is counted as such in the report - 483
    /// PlayerStartComponents must not be mistaken for 483 real spawn points.
    /// </summary>
    private static TileCentreSpawn ResolveSpawn(int mapIndex, TileMapData tileMapData, ConversionReport report)
    {
        if (mapIndex == NewGameMapId)
        {
            report.Increment("Worlds.PlayerStartFromSaveData");
            return ResolveTileCentreSpawn(NewGameSpawnTileX, NewGameSpawnTileY, NewGameSpawnTileZ);
        }

        report.Increment("Worlds.PlayerStartPlaceholders");

        var centreX = tileMapData.MapSize.Width * AlundraTileWidth / 2f;
        var centreY = tileMapData.MapSize.Height * AlundraTileHeight / 2f;
        return new TileCentreSpawn((int)centreX, (int)centreY, centreX, -centreY, 0f);
    }

    private static JObject BuildWorld(int mapIndex, Guid worldId, string worldName, Guid tileMapDataAssetId, TileCentreSpawn spawn)
    {
        var entityReferences = new JArray
        {
            BuildEntityReference(
                Ids.For($"world:{mapIndex}:entity:tileMap"),
                "tileMap",
                BuildTileMapComponent(Ids.For($"world:{mapIndex}:component:tileMap"), tileMapDataAssetId)),

            BuildEntityReference(
                Ids.For($"world:{mapIndex}:entity:camera"),
                "camera",
                BuildCameraComponent(Ids.For($"world:{mapIndex}:component:camera"))),

            BuildEntityReference(
                Ids.For($"world:{mapIndex}:entity:playerStart"),
                "PlayerStart",
                BuildPlayerStartComponent(Ids.For($"world:{mapIndex}:component:playerStart"), spawn)),
        };

        return new JObject
        {
            ["id"] = worldId.ToString(),
            ["name"] = worldName,
            ["entity_references"] = entityReferences,
            // The world's own GameplayProxy class. Empty until the gameplay DLL exists (E3);
            // World.Load reads this key unconditionally, so it must be present.
            ["script_class_name"] = null,
            ["player_startup_settings_asset_id"] = Guid.Empty.ToString(),
            ["gameplay_mode_asset_id"] = Guid.Empty.ToString(),
        };
    }

    // asset_id = Guid.Empty means "the world stores the whole entity inline" rather than
    // referencing a separate .entity asset - see EntityReference.Load.
    private static JObject BuildEntityReference(Guid entityId, string entityName, JObject rootComponentNode)
    {
        return new JObject
        {
            ["asset_id"] = Guid.Empty.ToString(),
            ["entity"] = new JObject
            {
                ["id"] = entityId.ToString(),
                ["name"] = entityName,
                ["root_component"] = rootComponentNode,
                ["components"] = new JArray(),
                ["script_class_name"] = null,
                // The entity policy keys (policy_source, mobility, tick_policy, ...) are omitted
                // on purpose: Entity.Load treats them as optional and, when absent, the engine
                // derives the policies from the components themselves (EntityPolicyResolver has
                // explicit cases for TileMapComponent and the cameras). Writing guessed values
                // would only override a better answer the engine already has.
            },
        };
    }

    private static JObject BuildTileMapComponent(Guid componentId, Guid tileMapDataAssetId)
    {
        var node = BuildSceneComponent(componentId, "TileMapComponent", 0f, 0f, 0f);
        node["tile_map_data_asset_id"] = tileMapDataAssetId.ToString();
        return node;
    }

    private static JObject BuildCameraComponent(Guid componentId)
    {
        var node = BuildSceneComponent(componentId, "Camera3dIn2dAxisComponent", 0f, 0f, 0f);

        // CameraComponent.InitializeWithWorld overwrites the viewport from the live screen size and
        // Camera3dComponent.InitializeWithWorld recomputes the field of view from it, so these are
        // only placeholders that keep the document loadable (both keys are read unconditionally).
        node["view_distance"] = 999f;
        node["viewport"] = new JObject
        {
            ["x"] = 0,
            ["y"] = 0,
            ["w"] = 1024,
            ["h"] = 768,
            ["min_depth"] = 1f,
            ["max_depth"] = 1000f,
        };
        node["fieldOfView"] = MathF.PI / 4f;
        return node;
    }

    private static JObject BuildPlayerStartComponent(Guid componentId, TileCentreSpawn spawn)
    {
        return BuildSceneComponent(componentId, "PlayerStartComponent", spawn.WorldX, spawn.WorldY, spawn.WorldZ);
    }

    // Mirrors what the engine's own writer emits for a SceneComponent: id, name, type,
    // local_transform (position/scale/rotation) and children_component. "type" is the simple type
    // name ElementFactory resolves the component class by.
    private static JObject BuildSceneComponent(Guid componentId, string typeName, float x, float y, float z)
    {
        return new JObject
        {
            ["id"] = componentId.ToString(),
            ["name"] = typeName,
            ["type"] = typeName,
            ["local_transform"] = new JObject
            {
                ["position"] = new JObject { ["x"] = x, ["y"] = y, ["z"] = z },
                ["scale"] = new JObject { ["x"] = 1f, ["y"] = 1f, ["z"] = 1f },
                ["rotation"] = new JObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f, ["w"] = 1f },
            },
            ["children_component"] = new JArray(),
        };
    }

    /// <summary>
    /// Maps/world-index.json maps an Alundra MapId to the relative path of its .world. It sits at
    /// the root of Maps/ because that is where the worlds it indexes now live, one per map folder.
    /// docs/demarrage-nouvelle-partie.md E6 asks the converter to produce it so the runtime portal
    /// system can resolve a portal's DestMapId to a world without scanning the catalog. The values
    /// are the exact strings registered in AssetInfos.json, because
    /// AssetCatalog.GetByFileName - which GameManager.UpdateWorld uses to resolve a world name - is
    /// an ordinal dictionary lookup, not a path comparison.
    /// MapId equals the map file index for all 483 maps (verified against Info.MapId), which is
    /// also what the game's identity MapIdToInternalMapIndexTable implies.
    /// </summary>
    private static void WriteWorldIndex(
        string outputDirectory, SortedDictionary<int, string> worldPathsByMapId, ConversionReport report)
    {
        var indexNode = new JObject();
        foreach (var (mapId, worldRelativePath) in worldPathsByMapId)
        {
            indexNode[mapId.ToString(CultureInfo.InvariantCulture)] = worldRelativePath;
        }

        Directory.CreateDirectory(Path.Combine(outputDirectory, MapLocation.MapsRootFolder));
        File.WriteAllText(
            Path.Combine(outputDirectory, MapLocation.MapsRootFolder, WorldIndexFileName),
            indexNode.ToString());

        report.Increment("Worlds.Indexed", worldPathsByMapId.Count);
    }

    private static void SetFirstWorldLoaded(
        string outputDirectory, SortedDictionary<int, string> worldPathsByMapId, ConversionReport report)
    {
        if (!worldPathsByMapId.TryGetValue(NewGameMapId, out var worldRelativePath))
        {
            report.Warnings.Add(
                $"Worlds: map {NewGameMapId} was not converted, so FirstWorldLoaded is left empty. "
                + "The New Game map is the project's entry point (docs/demarrage-nouvelle-partie.md E2).");
            return;
        }

        ProjectWriter.SetFirstWorldLoaded(outputDirectory, worldRelativePath, report);
    }

    /// <summary>
    /// Counts, from the native map dump, the objects the plan's Phase 6 acceptance names. They are
    /// counted here rather than written into the worlds because they live in the .tileMap object
    /// layers (see the class summary); the counters exist so a run can be checked against the
    /// analysis report's statistics.
    ///  - Worlds.Entities: non-null SpriteInfo.Entities.Entities records (every map has 128 slots).
    ///  - Worlds.EntitiesEnabled: those with IsEnabled != 0. This is the "9 631 entities" figure;
    ///    the raw record count is a different, larger number and both are reported.
    ///  - Worlds.Portals: Info.Portals with DestMapId != 0. Every map has 64 portal slots
    ///    (30 912 in total), so an unfiltered count would be meaningless - a portal that leads
    ///    nowhere is an empty slot.
    ///  - Worlds.MapEvents: non-null SpriteInfo.MapEvents.Records (64 slots per map).
    /// </summary>
    private static void CountSourceObjects(string inputDirectory, int mapIndex, ConversionReport report)
    {
        var nativeMapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
        if (!File.Exists(nativeMapPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{nativeMapPath}'; world object counters skipped.");
            return;
        }

        MapObjectCounts counts;
        try
        {
            counts = MapObjectCounts.Read(nativeMapPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to count world objects - {exception.Message}");
            return;
        }

        report.Increment("Worlds.Entities", counts.Entities);
        report.Increment("Worlds.EntitiesEnabled", counts.EntitiesEnabled);
        report.Increment("Worlds.Portals", counts.Portals);
        report.Increment("Worlds.MapEvents", counts.MapEvents);
    }

    /// <summary>
    /// The plan's Phase 6 acceptance criterion, enforced. Only meaningful on a full run: with
    /// --maps the totals are a subset by construction, so the check is skipped rather than made to
    /// fail. A mismatch is an error, not a warning - it means the converter stopped seeing part of
    /// the source data.
    /// </summary>
    private static void CheckInvariants(ConversionReport report)
    {
        CheckInvariant(report, "Worlds", ExpectedWorlds);
        CheckInvariant(report, "Worlds.Entities", ExpectedEntities);
        CheckInvariant(report, "Worlds.EntitiesEnabled", ExpectedEntitiesEnabled);
        CheckInvariant(report, "Worlds.Portals", ExpectedPortals);
        CheckInvariant(report, "Worlds.MapEvents", ExpectedMapEvents);
    }

    private static void CheckInvariant(ConversionReport report, string counterName, int expected)
    {
        var actual = report.Counters.GetValueOrDefault(counterName);
        if (actual != expected)
        {
            report.Errors.Add($"Worlds: invariant '{counterName}' is {actual}, expected {expected}.");
        }
    }

    private readonly record struct MapObjectCounts(int Entities, int EntitiesEnabled, int Portals, int MapEvents)
    {
        public static MapObjectCounts Read(string nativeMapFilePath)
        {
            using var stream = File.OpenRead(nativeMapFilePath);
            using var jsonDocument = System.Text.Json.JsonDocument.Parse(stream);
            var root = jsonDocument.RootElement;

            var entities = 0;
            var entitiesEnabled = 0;
            if (root.TryGetProperty("SpriteInfo", out var spriteInfo)
                && spriteInfo.TryGetProperty("Entities", out var entitiesHolder)
                && entitiesHolder.TryGetProperty("Entities", out var entityArray)
                && entityArray.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var entityElement in entityArray.EnumerateArray())
                {
                    if (entityElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    {
                        continue;
                    }

                    entities++;
                    if (entityElement.TryGetProperty("IsEnabled", out var isEnabled) && isEnabled.GetInt32() != 0)
                    {
                        entitiesEnabled++;
                    }
                }
            }

            var mapEvents = 0;
            if (root.TryGetProperty("SpriteInfo", out var spriteInfoForEvents)
                && spriteInfoForEvents.TryGetProperty("MapEvents", out var mapEventsHolder)
                && mapEventsHolder.TryGetProperty("Records", out var recordArray)
                && recordArray.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var recordElement in recordArray.EnumerateArray())
                {
                    if (recordElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        mapEvents++;
                    }
                }
            }

            var portals = 0;
            if (root.TryGetProperty("Info", out var info)
                && info.TryGetProperty("Portals", out var portalArray)
                && portalArray.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var portalElement in portalArray.EnumerateArray())
                {
                    if (portalElement.ValueKind == System.Text.Json.JsonValueKind.Object
                        && portalElement.TryGetProperty("DestMapId", out var destMapId)
                        && destMapId.GetInt32() != 0)
                    {
                        portals++;
                    }
                }
            }

            return new MapObjectCounts(entities, entitiesEnabled, portals, mapEvents);
        }
    }
}
