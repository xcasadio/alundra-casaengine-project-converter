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
///  - entity reference "camera": a reference to the single shared Entities/AlundraCamera.entity
///    asset. See the camera note below.
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
/// Camera choice - one shared Camera2dComponent asset, referenced first by every world.
///
///  Why Camera2dComponent. CasaEngineMonogame/docs/engine/rendering-2d-3d-spaces.md calls
///  "Mode 2 - World-space 2D pixel-perfect" the nominal mode of a 2D game, and says of
///  Camera3dIn2dAxisComponent (which this writer used before) that it "reste supportée pour
///  compatibilité des scènes existantes, mais ne doit plus être choisie pour un nouveau rendu 2D".
///  Facts behind that, all from the same document: Camera3dIn2dAxisComponent stays a *perspective*
///  camera placed at the distance that renders the target plane 1:1, so only that one plane is at
///  scale - every layer at another Z is scaled and translated, which is exactly the situation of an
///  Alundra map (its render layers sit at z 0 / 0.1 / 0.2 / 0.3); its camera distance is recomputed
///  from the field of view at every resize and from the global screen height rather than the view's;
///  and it has neither zoom nor texel snap, so no pixel-perfect contract is expressible. A
///  Camera2dComponent is orthographic: world units are pixels, every layer projects at the same
///  scale whatever its Z, and Zoom / PixelSnap make the doc's pixel-perfect checklist satisfiable.
///  Only the depth window changes, to [Target.Z - 500, Target.Z + 499], which the layer offsets fit
///  into easily.
///
///  Framing. The engine shows viewport / Zoom world pixels, and the viewport is the live window, so
///  the zoom written here only means something together with the window size Phase 0 writes into
///  AlundraGame.json. Both come from AlundraDisplay, which exists so they cannot drift: at Zoom = 1
///  against the engine's default 1024x768 window the view showed 1024x768 world pixels where
///  Alundra shows 320x236 - about ten times too much map on screen, which is what a run of the
///  converted game actually looked like before this was fixed.
///
///  Why the camera is shared rather than inlined per world. Its Target *is* serialized -
///  EditorEntityJsonSerializer.SaveCamera2dComponent writes target / zoom / pixel_snap and
///  Camera2dComponent.Load reads them back - so a camera written here really does keep its framing,
///  which was not true of the component this replaced. And a single Target frames every map:
///  all 483 Alundra maps measure exactly 52 x 60 tiles of 24 x 16 px (verified on the 483 .tmj),
///  so the map centre is the same point everywhere. Hence one Entities/AlundraCamera.entity asset,
///  referenced by asset_id from the 483 worlds. That is a reference, not sharing at runtime:
///  EntityReference.Load(AssetContentManager) does Load&lt;Entity&gt;(AssetId).Clone(), so each world
///  still gets its own instance and a gameplay proxy may move it freely.
///
///  Why it is written first in entity_references. DefaultRuntimeViewBootstrapper picks a world's
///  camera with world.Entities.Select(GetComponent&lt;CameraComponent&gt;).FirstOrDefault(c =&gt; c != null),
///  i.e. the first entity carrying a CameraComponent wins. Today it is also the only one, so the
///  order is not load-bearing yet - it is written that way so the intent survives the day another
///  camera-carrying entity is added. The entity keeps the name "camera" so an Alundra world proxy
///  can find it by name, the way RPGDemo's ScriptWorld re-aims its camera in OnBeginPlay.
///
/// Assumption, not fact: the Target is the geometric centre of a map. Alundra's real camera tracks
/// an entity - g_entityFollowedByCamera, which UpdateEntities reads every frame as
/// g_cameraLookAtX = followed.PosX &gt;&gt; 16. That entity is usually the hero, but it is a variable, not
/// a constant: scripts reassign it (cutscenes, bosses), so runtime code must follow whichever entity
/// is currently designated rather than hard-coding the player. The centred Target here is therefore
/// only the framing a world has *before* any gameplay code runs - deliberately a valid, centred view
/// of any map rather than a guess at the runtime behaviour.
///
/// That tracking belongs in the gameplay DLL, not in this asset: Camera2dComponent.Target is a
/// Vector3, so the proxy assigns it each frame from the followed entity's position - which is what
/// the original does too. CameraTargeted2dComponent would model the follow natively (its Target IS
/// an Entity, with dead zone and limits), but it derives from Camera3dComponent, i.e. it is a
/// perspective camera, and so falls under the very objection the engine's rendering-2d-3d-spaces.md
/// raises against the component replaced here. Entity-following and an orthographic pixel-perfect
/// projection cannot both come from a stock component today; the projection is the one that cannot
/// be added later from gameplay code, so it wins.
///
/// player_startup_settings_asset_id points at Entities/Alundra/Alundra.gameMode (see
/// PlayerSetupWriter), the same id in all 483 worlds - World.LoadContent spawns the hero at
/// whichever PlayerStartComponent that world declares, so every world can share the one
/// PlayerStartupSettings asset. Phase 6 receives the id already resolved (it needs the hero
/// .entity's own asset id, which only Phase 3 knows), so ConvertWorlds takes it as a parameter
/// rather than deriving it itself.
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

    // Verified on the 483 exported .tmj: every Alundra map is exactly this size, which is what makes
    // a single shared camera correct and not merely convenient (see the class summary).
    private const int AlundraMapWidthInTiles = 52;
    private const int AlundraMapHeightInTiles = 60;

    // The one camera asset every world references. Written once per run, before the worlds that
    // reference it.
    private const string SharedCameraName = "AlundraCamera";
    private const string SharedCameraEntityName = "camera";
    private const string SharedCameraEntitiesFolder = "Entities";

    // The world-level GameplayProxy class, implemented in the Alundra gameplay DLL
    // (Alundra/Scripts/AlundraWorldProxy.cs) and resolved by simple name at runtime.
    private const string WorldScriptClassName = "AlundraWorldProxy";

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
        Guid playerStartupSettingsAssetId,
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

        var sharedCameraAssetId = WriteSharedCamera(outputDirectory, report);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(
                inputDirectory, outputDirectory, mapIndex, mapLocations, sharedCameraAssetId,
                playerStartupSettingsAssetId, worldPathsByMapId, report);
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

    /// <summary>
    /// Writes the single Entities/AlundraCamera.entity every world references, and returns its asset
    /// id. See the class summary for why there is only one and why it is a Camera2dComponent.
    /// The Target is the centre of a map, derived from the map size rather than written as a literal:
    /// (52*24/2, -(60*16/2), 0) = (624, -480, 0). The negative Y is the Y = -pixelY convention of
    /// docs/guidelines-runtime-alundra-casaengine.md section 2.3, used everywhere in this converter.
    /// </summary>
    private static Guid WriteSharedCamera(string outputDirectory, ConversionReport report)
    {
        var assetId = Ids.For("entity:camera");
        var relativePath = Path.Combine(SharedCameraEntitiesFolder, $"{SharedCameraName}.entity");

        Directory.CreateDirectory(Path.Combine(outputDirectory, SharedCameraEntitiesFolder));

        var entityNode = new JObject
        {
            ["id"] = assetId.ToString(),
            ["name"] = SharedCameraName,
            ["root_component"] = BuildCamera2dComponent(Ids.For("entity:camera:component")),
            ["components"] = new JArray(),
            ["script_class_name"] = null,
            // Entity policy keys omitted on purpose, as in BuildEntityReference below: Entity.Load
            // treats them as optional and EntityPolicyResolver derives better values from the
            // components themselves.
        };

        EditorAssetWriterService.SaveDocument(relativePath, entityNode);
        EditorAssetCatalogService.Add(new AssetInfo(assetId)
        {
            Name = SharedCameraName,
            FileName = relativePath,
        });

        report.Increment("Worlds.SharedCameras");
        return assetId;
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        Guid sharedCameraAssetId,
        Guid playerStartupSettingsAssetId,
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
        var worldNode = BuildWorld(
            mapIndex, worldId, location.FileBaseName, tileMapData.Id, sharedCameraAssetId,
            playerStartupSettingsAssetId, spawn);

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

    private static JObject BuildWorld(
        int mapIndex,
        Guid worldId,
        string worldName,
        Guid tileMapDataAssetId,
        Guid sharedCameraAssetId,
        Guid playerStartupSettingsAssetId,
        TileCentreSpawn spawn)
    {
        var entityReferences = new JArray
        {
            // FIRST on purpose: DefaultRuntimeViewBootstrapper takes the first entity of the world
            // carrying a CameraComponent as the default view camera.
            BuildSharedEntityReference(sharedCameraAssetId, SharedCameraEntityName),

            BuildEntityReference(
                Ids.For($"world:{mapIndex}:entity:tileMap"),
                "tileMap",
                BuildTileMapComponent(Ids.For($"world:{mapIndex}:component:tileMap"), tileMapDataAssetId)),

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
            // The world's own GameplayProxy class, implemented by AlundraWorldProxy in the Alundra
            // gameplay DLL (Alundra/Scripts/), and resolved at runtime by simple name via
            // ElementFactory.Create<GameplayProxy>. The forward reference is intentional: the class
            // itself is implemented in a follow-up commit, but World.Load reads this key
            // unconditionally, so it must be present now.
            ["script_class_name"] = WorldScriptClassName,
            // Alundra's simulation space: X/Y is the ground plane and Z is elevation, which is what
            // SimulationSpacePolicyNames.TopDownElevation constrains bodies to. Written as the
            // literal the engine matches on (World.Load -> SpacePolicyName -> CreateByName).
            ["space_policy"] = "TopDownElevation",
            ["player_startup_settings_asset_id"] = playerStartupSettingsAssetId.ToString(),
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

    /// <summary>
    /// The other branch of EntityReference.Load: a non-empty asset_id means "load this .entity asset
    /// and clone it", and the reference then carries only a name and the transform to copy onto the
    /// clone's root component. The keys are exactly the three that branch reads - "entity" would be
    /// ignored here, so it is not written.
    /// </summary>
    private static JObject BuildSharedEntityReference(Guid assetId, string entityName)
    {
        return new JObject
        {
            ["asset_id"] = assetId.ToString(),
            ["name"] = entityName,
            ["initial_local_transform"] = new JObject
            {
                ["position"] = new JObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f },
                ["scale"] = new JObject { ["x"] = 1f, ["y"] = 1f, ["z"] = 1f },
                ["rotation"] = new JObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f, ["w"] = 1f },
            },
        };
    }

    private static JObject BuildTileMapComponent(Guid componentId, Guid tileMapDataAssetId)
    {
        var node = BuildSceneComponent(componentId, "TileMapComponent", 0f, 0f, 0f);
        node["tile_map_data_asset_id"] = tileMapDataAssetId.ToString();
        return node;
    }

    /// <summary>
    /// The shared camera's root component. Key order and names mirror exactly what
    /// EditorEntityJsonSerializer.SaveCamera2dComponent emits: the SceneComponent base, then
    /// SaveCameraComponent's view_distance / viewport, then target / zoom / pixel_snap.
    /// Values follow the pixel-perfect checklist of docs/engine/rendering-2d-3d-spaces.md:
    /// integer Zoom and PixelSnap on.
    /// </summary>
    private static JObject BuildCamera2dComponent(Guid componentId)
    {
        var node = BuildSceneComponent(componentId, "Camera2dComponent", 0f, 0f, 0f);

        // CameraComponent.InitializeWithWorld overwrites the viewport from the live screen size, so
        // these are only placeholders keeping the document loadable (both keys are read
        // unconditionally by CameraComponent.Load). They still carry the window size the project
        // asks for, so a reader of this file is not misled about the intended framing.
        node["view_distance"] = 999f;
        node["viewport"] = new JObject
        {
            ["x"] = 0,
            ["y"] = 0,
            ["w"] = AlundraDisplay.WindowWidth,
            ["h"] = AlundraDisplay.WindowHeight,
            ["min_depth"] = 1f,
            ["max_depth"] = 1000f,
        };

        node["target"] = new JObject
        {
            ["x"] = AlundraMapWidthInTiles * AlundraTileWidth / 2f,
            ["y"] = -(AlundraMapHeightInTiles * AlundraTileHeight / 2f),
            ["z"] = 0f,
        };
        // The other half of the framing (see AlundraDisplay). Zoom = 1 with the engine's default
        // window showed 1024x768 world pixels where Alundra shows 320x236 - ten times too much map.
        node["zoom"] = AlundraDisplay.CameraZoom;
        node["pixel_snap"] = true;
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
