#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// World script every converted .world declares as its "script_class_name" (see
/// <c>WorldWriter.WorldScriptClassName</c>). On world load, read the map's tilemap "Entities"
/// object layer (see <c>AlundraDataExtractor.TiledMapExporter</c>) and spawn one game entity per
/// record. Each record carries a <c>PrefabAssetId</c> custom property (see
/// <c>AlundraDataExtractor.TiledMapExporter</c>/<c>EntityBankPrefabWriter</c>) linking it to the
/// per-bank prefab (<c>Entities/{Name}/{Name}.entity</c>); the normal path clones that prefab so
/// the spawned entity carries the bank's sprite/collision components. When the link is missing,
/// cannot be loaded, or the record has none, a bare entity is created instead (logged fallback).
/// Either way, the resulting entity carries an <see cref="AlundraEntityScriptProxy"/> filled by
/// <see cref="EntityRecordMapper"/>, whose logical position fields (<c>PosX</c>/<c>PosY</c>/<c>PosZ</c>)
/// this proxy then converts into the spawned entity's <c>RootComponent.LocalTransform.Position</c> - now
/// the entity's LOGICAL pose (E3.a, docs/plan-e3-collisions.md decision E3-1), not a render pose - via
/// <see cref="ResolveLogicalPosition"/> - see <see cref="CreateEntityFromPrefab"/>. A
/// <c>RenderProjectionComponent</c> child of that root (<c>SpriteWriter.WriteEntityPrefab</c>) derives
/// the render pose from it every update.
///
/// This proxy also retains every entity it spawned and drives their status machine each frame (see
/// <see cref="Update"/>), a faithful port of the two-phase pass of
/// <c>EntityManager.UpdateEntitiesEvents</c> @ 0x800386D0: the original manager-level pass, not a
/// per-entity one, which is why the driver lives here rather than on
/// <see cref="AlundraEntityScriptProxy"/> (whose own <c>Update</c> stays a no-op by design - see its
/// doc comment). Actual event-program execution goes through the <see cref="IEventProgramRunner"/> seam
/// (<see cref="EventProgramRunner"/>); the bytecode interpreter itself is a later chantier. Transform
/// re-derivation when the logical position changes at runtime is still a follow-up task.
/// </summary>
public class AlundraWorldProxy : GameplayProxy, IEntityWorldContext, IAlundraScriptHost
{
    // EntityRecordMapper's own tile constants (StaticVariables.MapTileWidth/Height) - duplicated here
    // (rather than made public there) since only the player spawn (this class) and that pure mapper need
    // them; see EntityRecordMapper's own class doc for the same constants' derivation.
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    /// <summary>Catalog name of the hero bank prefab (<c>Entities/Alundra/Alundra.entity</c>) - see
    /// <see cref="SpawnPlayerEntity"/>'s own doc.</summary>
    private const string HeroAssetName = "Alundra";

    /// <summary>Key of the custom property linking an "Entities" record to its bank prefab asset.</summary>
    private const string PrefabAssetIdPropertyKey = "PrefabAssetId";

    /// <summary>Name of the entity carrying the map's <see cref="TileMapComponent"/> (see WorldWriter).</summary>
    private const string TileMapEntityName = "tileMap";

    private const string EntitiesLayerName = "Entities";
    private const string PortalsLayerName = "Portals";
    private const string MapEventsLayerName = "MapEvents";


    /// <summary>
    /// DEBUG ONLY - temporary right-stick camera pan so the map can be flown over at runtime to inspect
    /// spawned entities, until the real camera-follow (E4) replaces it. Speed in world pixels/second,
    /// picked so a full stick deflection crosses the widest converted map (52 tiles * 24px = 1248px) in
    /// roughly 2.5 seconds.
    /// </summary>
    private const float DebugCameraPanSpeedPixelsPerSecond = 500f;

    /// <summary>DEBUG ONLY - see <see cref="DebugCameraPanSpeedPixelsPerSecond"/>. Per-axis stick deadzone.</summary>
    private const float DebugCameraPanDeadZone = 0.2f;

    /// <summary>
    /// Entities spawned by this proxy in <see cref="InitializeWithWorld"/> (both the prefab-clone and
    /// bare-fallback paths), in creation order - <see cref="Update"/> drives their status machine in
    /// this same order, mirroring the original manager's single flat entity-slot array.
    /// </summary>
    private readonly List<Entity> _spawnedEntities = new();

    /// <summary>
    /// Every "Entities" record of this world's own tilemap layer, keyed by its <c>Index</c> custom
    /// property - i.e. the same id <see cref="AlundraEntityScriptProxy.EntityRefId"/> is filled from
    /// (<see cref="EntityRecordMapper"/>) and the id the dynamic-spawn opcode 0x2D
    /// (<c>GameEngine.SpawnEntity</c>'s <c>entityId</c> parameter) looks records up by
    /// (<c>GameEngine.GetEntityRecord</c>, GameEngine.cs:2125-2144). Populated once in
    /// <see cref="InitializeWithWorld"/> from every record of the layer, including ones
    /// <see cref="ShouldSpawnRecord"/> would reject for the map-load pass - 0x2D applies its own,
    /// looser gate (<c>notCheckSpawnZone = 1</c>, see <see cref="SpawnEntityByRecordId"/>) so a record
    /// this world did not spawn at load time can still become spawnable later.
    /// </summary>
    private readonly Dictionary<int, TileMapObjectData> _entityRecordsByIndex = new();

    /// <summary>
    /// Per-frame working list for <see cref="Update"/>: cleared and refilled from
    /// <see cref="_spawnedEntities"/> every frame instead of allocating a temporary list in the hot
    /// path. Kept as a re-read of each entity's <c>GameplayProxy</c> (rather than a list frozen at
    /// spawn) so <see cref="_spawnedEntities"/> stays the single source of truth.
    /// </summary>
    private readonly List<AlundraEntityScriptProxy> _updateProxies = new();

    /// <summary>
    /// Seam over actual event-program execution (see <see cref="IEventProgramRunner"/>); defaults to a
    /// silent no-op since the bytecode interpreter does not exist yet. Internal, not injected through
    /// the constructor: <c>ElementFactory</c> constructs gameplay proxies parameterless, so tests swap
    /// this field directly instead.
    /// </summary>
    internal IEventProgramRunner EventProgramRunner = new NoOpEventProgramRunner();

    /// <summary>
    /// Backing store for <see cref="EventProgramRunner"/> when it is a real <see cref="AlundraEventProgramRunner"/>
    /// (see <see cref="InitializeWithWorld"/>) - kept as its own field (rather than only living inside the
    /// runner's constructor call) so <see cref="Update"/> can read <see cref="AlundraGameState.PlayerControlFlags"/>
    /// for <see cref="RunMapEventsPass"/>'s own gate, mirroring how the original reads the SAME global
    /// (<c>g_playerControlFlags</c>) from both <c>RunMapEvents</c> and the opcode handlers alike.
    /// </summary>
    internal readonly AlundraGameState GameState = new();

    /// <summary>
    /// The New Game hero entity, spawned once in <see cref="InitializeWithWorld"/> BEFORE every "Entities"
    /// record (its tile gates their own spawn-zone check, exactly like the original -
    /// <see cref="ShouldSpawnRecord(TileMapObjectData,bool,int,int,out string)"/>). Minimal V1 port of
    /// <c>ResetEntityState</c> (GameEngine.cs:648-670, called from <c>InitializeEntitySlots</c> BEFORE its
    /// own record-spawn loop, GameEngine.cs:626-643) - see <see cref="SpawnPlayerEntity"/>'s own doc for
    /// exactly what is and is not ported. Null when this world has no hero asset in the catalog, no prefab
    /// loader, or the loader fails (logged, degraded - same shape as every other spawn failure in this
    /// class); in that case no MapEvents run either (they always execute against the player,
    /// <see cref="RunMapEventsPass"/> requires a non-null player).
    /// </summary>
    internal AlundraEntityScriptProxy? PlayerEntity { get; private set; }

    /// <summary>
    /// This world's MapEvents (port of <c>InitializeMapEvents</c>, GameEngine.cs:476-583): one entry per
    /// "MapEvents" object-layer record whose <c>EventCodesBIndex</c> is non-zero, in record order, each
    /// always executing against <see cref="PlayerEntity"/> - see <see cref="RunMapEventsPass"/>'s own doc.
    /// Empty (not null) when this world has no "MapEvents" layer, or no <see cref="PlayerEntity"/> to run
    /// them against.
    /// </summary>
    private readonly List<AlundraMapEvent> _mapEvents = new();

    private bool _loggedNoHeroHeader;

    /// <summary>E2: this world's own <see cref="AlundraPlayerController"/>, resolved once in
    /// <see cref="InitializeWithWorld"/> (<c>World.PlayerControllers</c> is already populated by then -
    /// see <see cref="AdoptPlayerPawn"/>'s own doc). Null when no such controller exists (no
    /// <c>.gameMode</c>/PlayerStartupSettings wired for this world, or its <c>player_controller_class</c>
    /// resolved to something other than <see cref="AlundraPlayerController"/>) - logged once.</summary>
    private AlundraPlayerController? _playerController;
    private bool _loggedNoPlayerController;

    /// <summary>
    /// Seam over <c>Data/sprite-records.json</c> lookups (see <see cref="Alundra.Scripts.SpriteRecordCatalog"/>'s
    /// class doc), read once and reused for every record this proxy spawns. Internal, not injected
    /// through the constructor - same reasoning as <see cref="EventProgramRunner"/>: <c>ElementFactory</c>
    /// constructs gameplay proxies parameterless, so tests swap this field directly.
    /// </summary>
    internal ISpriteRecordCatalog SpriteRecordCatalog = new SpriteRecordCatalog();

    /// <summary>
    /// Port of the original global <c>g_activeCollisionEntity</c>: the entity currently involved in the
    /// active collision pair, used by the pick phase to decide whether a touch downgrades all the way
    /// to an interact (slot F). Null in V1 (no collision system driving it yet); settable internally for
    /// tests.
    /// </summary>
    internal AlundraEntityScriptProxy? ActiveCollisionEntity;

    /// <summary>
    /// DEBUG ONLY (see <see cref="DebugCameraPanSpeedPixelsPerSecond"/>). Cached so <see cref="Update"/>,
    /// which gets no <see cref="World"/> parameter, can still read the gamepad and reach the camera entity
    /// looked up in <see cref="InitializeWithWorld"/>.
    /// </summary>
    private World? _world;

    /// <summary>DEBUG ONLY. Cached <see cref="Camera2dComponent"/> of the world's camera
    /// entity, resolved once on first <see cref="Update"/> call; stays null (and logs once) when the
    /// world has no such entity/component.</summary>
    private Camera2dComponent? _debugCamera;

    /// <summary>DEBUG ONLY. Guards the one-time <see cref="_debugCamera"/> lookup/warning.</summary>
    private bool _debugCameraLookupDone;

    /// <summary>
    /// True once <see cref="InitializeWithWorld"/> successfully parsed and applied this world's
    /// <see cref="WallPlacementOverlay"/> (see <see cref="WallPlacementOverlay.CustomPropertyKey"/>).
    /// Gates the per-entity <see cref="WallPlacementOverlay.ApplyEntitySortKey"/> call in
    /// <see cref="RunAnimationSyncPass"/>'s caller (<see cref="Update"/>): with no wall placements loaded
    /// there is nothing to interleave against, so entities keep whatever
    /// <see cref="DepthSortable2DComponent"/> defaults their prefab already carries.
    /// </summary>
    private bool _wallPlacementOverlayApplied;

    /// <summary>
    /// This world's <see cref="AlundraCellsCollisionField"/>, built once in
    /// <see cref="InitializeWithWorld"/> from the loaded <see cref="TileMapData"/>'s "AlundraCells"
    /// custom property and installed as <c>World.CollisionField</c> (also exposed here for tests -
    /// null in degraded mode, see <see cref="AlundraCellsRecords.TryParse"/>).
    /// </summary>
    public AlundraCellsCollisionField? CollisionField { get; private set; }

    /// <summary>Renders this world's scrolling background layers (see <see cref="BackdropRenderer"/>'s
    /// class doc) - loaded once in <see cref="InitializeWithWorld"/>, ticked and drawn every frame
    /// from <see cref="Update"/>.</summary>
    private readonly BackdropRenderer _backdropRenderer = new();

    /// <summary>Cached once <see cref="ApplyOriginalBackgroundClearColor"/> has set the world's runtime
    /// view <see cref="CasaEngine.Framework.Rendering.RenderView.ClearColor"/> - the view does not
    /// exist yet when <see cref="InitializeWithWorld"/> runs (<c>GameManager.EndLoadContent</c> calls
    /// <c>World.LoadContent</c>, which drives this proxy, strictly before
    /// <c>IRuntimeViewBootstrapper.BootstrapViews</c>), so the lookup is retried lazily from
    /// <see cref="Update"/>, mirroring <see cref="_debugCameraLookupDone"/>'s own one-time-retry shape.</summary>
    private bool _clearColorApplied;

    public override void InitializeWithWorld(World world)
    {
        _world = world;

        // The engine enables its physics debug wireframes by default (PhysicsDebugViewRendererComponent
        // .DisplayPhysics = true), which draws every kinetic body box - one white rectangle per spawned
        // entity with a body - over the game. Off for normal play; the Back button toggles it back on
        // (see UpdateDebugCameraPan) to inspect collision boxes while flying the debug camera.
        if (world.Game?.PhysicsDebugViewRendererComponent != null)
        {
            world.Game.PhysicsDebugViewRendererComponent.DisplayPhysics = false;
        }

        // Loads this world's own event-code document (see MapEventProgramLoader's class doc on path
        // resolution) and wires the real slot-A interpreter over it; null document means "not found /
        // failed to parse" and AlundraEventProgramRunner degrades to a counted no-op for slot A too, the
        // same shape as SpriteRecordCatalog's own degraded mode.
        var eventProgramDocument = MapEventProgramLoader.Load(EngineEnvironment.ProjectPath, world.Name);
        EventProgramRunner = new AlundraEventProgramRunner(eventProgramDocument, GameState, this);

        // Scrolling background layers (see BackdropRenderer's class doc) - same degraded-mode shape as
        // the event-program document above: a world with no companion file (most of them - Scroll
        // Parameters.Infos.Enabled was false) simply renders nothing extra.
        _backdropRenderer.Load(world, EngineEnvironment.ProjectPath);

        var tileMapEntity = world.Entities.FirstOrDefault(entity => entity.Name == TileMapEntityName);
        if (tileMapEntity == null)
        {
            Logs.WriteWarning($"AlundraWorldProxy: no '{TileMapEntityName}' entity found in world '{world.Name}'; no entity spawned.");
            return;
        }

        var tileMapComponent = tileMapEntity.GetComponent<TileMapComponent>();
        var tileMapData = tileMapComponent?.TileMapData;
        if (tileMapData == null)
        {
            Logs.WriteWarning($"AlundraWorldProxy: entity '{TileMapEntityName}' has no loaded TileMapData in world '{world.Name}'; no entity spawned.");
            return;
        }

        // E3.b: build this world's ground/walkability field from the same TileMapData and install it on
        // World.CollisionField - World.Clear() resets the slot to null (World.cs), so every load
        // re-installs it here. Tolerant by design, like the wall/floor placement overlays right below:
        // a missing/malformed "AlundraCells" property (or a cell_count that does not match MapSize)
        // just leaves World.CollisionField null (degraded mode, single warning already logged by
        // AlundraCellsCollisionField.TryCreate) - E3.c's mover then has no field to sample.
        if (AlundraCellsCollisionField.TryCreate(tileMapData, world.Name, out var collisionField))
        {
            CollisionField = collisionField;
            world.CollisionField = collisionField;
        }
        else
        {
            CollisionField = null;
        }

        // Wall/sprite depth interleave (Slice B): strip every baked wall tile the converter recorded
        // out of the flat "Render_*" layers and resubmit it through the tile map's runtime sorted
        // overlay, so it draws ordered against Y-sorted entity sprites instead of always flat. Tolerant
        // by design - see WallPlacementOverlay.TryParse's doc comment - so a world with no (or a
        // malformed) "AlundraWallPlacements" property still spawns its entities normally, just without
        // the interleave.
        if (WallPlacementOverlay.TryParse(tileMapData.CustomProperties, world.Name, out var wallPlacements))
        {
            WallPlacementOverlay.Apply(tileMapComponent!, wallPlacements, world.Name);
            _wallPlacementOverlayApplied = true;
        }

        // Same interleave for elevated (Height > 0) floor tiles, through the same runtime sorted
        // overlay - see WallPlacementOverlay.ComputeFloorSortKey's doc for why a floor's own row bias
        // (slot 0..5, no +7) already orders it correctly against both walls and Y-sorted entities.
        // Independently tolerant, like the wall property above: a world with no (or malformed)
        // "AlundraFloorPlacements" property still spawns normally, just without this interleave.
        if (WallPlacementOverlay.TryParseFloor(tileMapData.CustomProperties, world.Name, out var floorPlacements))
        {
            WallPlacementOverlay.ApplyFloor(tileMapComponent!, floorPlacements, world.Name);
        }

        var entitiesLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == EntitiesLayerName);
        var portalsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == PortalsLayerName);
        var mapEventsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == MapEventsLayerName);

        Logs.WriteInfo(
            $"AlundraWorldProxy: world '{world.Name}' object layers - "
            + $"{EntitiesLayerName}={entitiesLayer?.Objects.Count ?? 0}, "
            + $"{PortalsLayerName}={portalsLayer?.Objects.Count ?? 0}, "
            + $"{MapEventsLayerName}={mapEventsLayer?.Objects.Count ?? 0}.");

        // E2: register the "AlundraButtons" input mappings once per game (idempotent across world
        // reloads - see AlundraPlayerController.EnsureInputMappingsRegistered's own doc), before any
        // entity's first Update ever reads them.
        AlundraPlayerController.EnsureInputMappingsRegistered(world.Game);

        // E2: the engine itself already spawned and possessed the hero pawn (World.LoadContent ->
        // InitializePlayerControllers, strictly before this GameplayProxy's own InitializeWithWorld runs -
        // see AdoptPlayerPawn's own doc) - adopt it and apply the New Game logical state (port of
        // ResetEntityState/InitializeEntitySlots' own spawn order, GameEngine.cs:626-643: the player exists
        // BEFORE any record is spawned - the spawn-zone gate below reads its tile) instead of spawning a
        // second, separate hero entity ourselves.
        AdoptPlayerPawn(world);

        // MapEvents (port of InitializeMapEvents, GameEngine.cs:476-583) - always against PlayerEntity;
        // empty when there is none (see PlayerEntity's own doc).
        BuildMapEvents(mapEventsLayer);

        if (entitiesLayer == null)
        {
            return;
        }

        var skippedCount = 0;

        foreach (var record in entitiesLayer.Objects)
        {
            // Indexed for the whole world's lifetime regardless of whether this record spawns right now
            // (see _entityRecordsByIndex's own doc) - 0x2D's looser gate can still spawn it later.
            if (TryGetRecordInt(record, "Index", out var recordIndex))
            {
                _entityRecordsByIndex[recordIndex] = record;
            }

            bool shouldSpawn;
            string skipReason;
            if (PlayerEntity != null)
            {
                shouldSpawn = ShouldSpawnRecord(record, notCheckSpawnZone: false, PlayerEntity.TileX, PlayerEntity.TileY, out skipReason);
            }
            else
            {
                shouldSpawn = ShouldSpawnRecord(record, out skipReason);
            }

            if (!shouldSpawn)
            {
                skippedCount++;
                Logs.WriteDebug($"AlundraWorldProxy: record '{record.Name}' not spawned ({skipReason}).");
                continue;
            }

            try
            {
                var entity = CreateEntityFromRecord(
                    record, guid => world.Game.AssetContentManager.Load<Entity>(guid), SpriteRecordCatalog);
                if (entity.GameplayProxy is AlundraEntityScriptProxy spawnedProxy)
                {
                    spawnedProxy.ScriptHost = this;
                }

                world.AddEntity(entity);
                _spawnedEntities.Add(entity);
            }
            catch (Exception ex)
            {
                Logs.WriteWarning(
                    $"AlundraWorldProxy: failed to spawn entity for record '{record.Name}' in world '{world.Name}'; "
                    + $"skipping. {ex.Message}");
            }
        }

        if (skippedCount > 0)
        {
            Logs.WriteInfo(
                $"AlundraWorldProxy: world '{world.Name}' - {skippedCount} of {entitiesLayer.Objects.Count} "
                + "Entities records not spawned (see ShouldSpawnRecord).");
        }
    }

    /// <summary>
    /// Spawn-time gate over one "Entities" record, ported from the two checks
    /// <c>GameEngine.SpawnEntity</c> (GameEngine.cs:681-758) applies before ever building an entity, for
    /// the specific call the map-load path makes: <c>GameEngine.InitializeEntitySlots</c>
    /// (GameEngine.cs:629-645) spawns every record of the map with <c>SpawnEntity(null, i, 0)</c> - i.e.
    /// <c>notCheckSpawnZone == 0</c>, so both of these checks apply, in this order:
    /// <list type="number">
    /// <item><description><c>IsEnabled == 0</c>: <c>GameEngine.GetEntityRecord</c> (GameEngine.cs:2126-2144)
    /// returns null for such a record, so <c>SpawnEntity</c> never proceeds past its very first line.
    /// Every map-389 record happens to have <c>IsEnabled == 1</c>, so this never fires there, but other
    /// maps do carry disabled records (9741 total, 9631 with <c>IsEnabled != 0</c> - see
    /// <c>WorldWriter</c>'s own count).</description></item>
    /// <item><description><c>(SpriteDirection &amp; 0x40) == 0</c> (GameEngine.cs:715-718): with
    /// <c>notCheckSpawnZone == 0</c> this alone is enough to skip the record. On map 389 this drops the
    /// spawn count from 19 to 14 (5 of its records carry <c>SpriteDirection</c> values 0 or 128, both with
    /// bit 0x40 clear).</description></item>
    /// </list>
    /// Deliberately NOT ported: the player-tile spawn-zone box (<c>XMin</c>/<c>XMax</c>/<c>YMin</c>/
    /// <c>YMax</c> vs <c>StaticVariables.PlayerEntity.TileX</c>/<c>TileY</c>, GameEngine.cs:690-711). The
    /// original resolves it against a player entity <c>GameEngine.ResetEntityState</c> (GameEngine.cs:648-672)
    /// already spawned before this loop runs; this world proxy has no player system yet (see the class
    /// doc), so the check would only ever compare against a zeroed sentinel, which is worse than not
    /// checking it at all - a follow-up task once a player entity exists.
    /// </summary>
    internal static bool ShouldSpawnRecord(TileMapObjectData record, out string skipReason)
        => ShouldSpawnRecord(record, notCheckSpawnZone: false, out skipReason);

    /// <summary>
    /// <paramref name="notCheckSpawnZone"/> overload: mirrors <c>GameEngine.SpawnEntity</c>'s own
    /// <c>notCheckSpawnZone</c> parameter (GameEngine.cs:684-708) - the map-load pass always calls this
    /// with it false (<see cref="ShouldSpawnRecord(TileMapObjectData,out string)"/> above); the
    /// dynamic-spawn opcode 0x2D always calls it true (see <see cref="SpawnEntityByRecordId"/>), which
    /// skips the <c>SpriteDirection</c> 0x40 gate below - <c>IsEnabled</c> always applies either way,
    /// exactly like the original (<c>GameEngine.GetEntityRecord</c> returns null before
    /// <c>notCheckSpawnZone</c> is ever consulted).
    /// </summary>
    internal static bool ShouldSpawnRecord(TileMapObjectData record, bool notCheckSpawnZone, out string skipReason)
    {
        if (TryGetRecordInt(record, "IsEnabled", out var isEnabled) && isEnabled == 0)
        {
            skipReason = "IsEnabled=0";
            return false;
        }

        if (!notCheckSpawnZone
            && TryGetRecordInt(record, "SpriteDirection", out var spriteDirection) && (spriteDirection & 0x40) == 0)
        {
            skipReason = $"SpriteDirection={spriteDirection} has bit 0x40 clear";
            return false;
        }

        skipReason = string.Empty;
        return true;
    }

    /// <summary>
    /// <paramref name="playerTileX"/>/<paramref name="playerTileY"/> overload: adds the player-tile
    /// spawn-zone box (<c>XMin</c>/<c>XMax</c>/<c>YMin</c>/<c>YMax</c> vs the PLAYER's own tile,
    /// GameEngine.cs:690-711) on top of <see cref="ShouldSpawnRecord(TileMapObjectData,bool,out string)"/>'s
    /// two existing checks - the third gate <c>SpawnEntity</c> applies with <c>notCheckSpawnZone == 0</c>,
    /// deliberately NOT ported before E1 for lack of a player entity (see that method's own doc, now
    /// resolved by <see cref="PlayerEntity"/>). Missing <c>XMin</c>/<c>XMax</c>/<c>YMin</c>/<c>YMax</c>
    /// keys leave that side of the box unchecked (same best-effort-tolerant shape as every other
    /// <see cref="TryGetRecordInt"/> read in this class) rather than failing the spawn outright - every
    /// converted record is expected to carry all four, this only guards a malformed/older export.
    /// <paramref name="notCheckSpawnZone"/> skips this box too, exactly like the original (0x2D/0x8B never
    /// check it, see <see cref="SpawnEntityByRecordId"/>).
    /// </summary>
    internal static bool ShouldSpawnRecord(
        TileMapObjectData record, bool notCheckSpawnZone, int playerTileX, int playerTileY, out string skipReason)
    {
        if (!ShouldSpawnRecord(record, notCheckSpawnZone, out skipReason))
        {
            return false;
        }

        if (notCheckSpawnZone)
        {
            return true;
        }

        if (TryGetRecordInt(record, "XMin", out var xMin) && playerTileX < xMin)
        {
            skipReason = $"player tileX={playerTileX} < XMin={xMin}";
            return false;
        }

        if (TryGetRecordInt(record, "XMax", out var xMax) && playerTileX > xMax)
        {
            skipReason = $"player tileX={playerTileX} > XMax={xMax}";
            return false;
        }

        if (TryGetRecordInt(record, "YMin", out var yMin) && playerTileY < yMin)
        {
            skipReason = $"player tileY={playerTileY} < YMin={yMin}";
            return false;
        }

        if (TryGetRecordInt(record, "YMax", out var yMax) && playerTileY > yMax)
        {
            skipReason = $"player tileY={playerTileY} > YMax={yMax}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Stride used to pack (anim, direction) into <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/>'s
    /// single-int key: directions are always 0-3 (<c>AnimationTables.DirectionNames.Length</c>), so 4 is
    /// enough to keep every (anim, direction) pair distinct without a tuple key/comparer.
    /// </summary>
    private const int IdsvDirectionStride = 4;

    /// <summary>
    /// Builds the per-entity IDSV lookup <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/> stashes
    /// at spawn (see <see cref="ApplySpawnInitialization"/>): one frame-0 value per (anim, direction) pair
    /// the catalog entry carries. Returns null when <paramref name="idsvAnimDirs"/> is empty (nothing to
    /// look up - callers treat a null table the same as "0 bias for every (anim, direction)").
    /// </summary>
    internal static Dictionary<int, int>? BuildIdsvByAnimDirection(IReadOnlyList<AnimDirIdsv>? idsvAnimDirs)
    {
        if (idsvAnimDirs == null || idsvAnimDirs.Count == 0)
        {
            return null;
        }

        var table = new Dictionary<int, int>(idsvAnimDirs.Count);
        foreach (var entry in idsvAnimDirs)
        {
            var frame0 = entry.Frames is { Count: > 0 } frames ? frames[0] : 0;
            table[entry.Anim * IdsvDirectionStride + entry.Direction] = frame0;
        }

        return table;
    }

    /// <summary>
    /// Builds the per-entity Hold/Chain lookup <see cref="AlundraEntityScriptProxy.AnimationEndByAnimDirection"/>
    /// stashes at spawn - same key packing as <see cref="BuildIdsvByAnimDirection"/>, so both tables share
    /// the (anim, direction) -&gt; int key without a tuple key/comparer. Only entries whose End is Hold or
    /// Chain are worth keeping (a Loop entry has nothing to bridge - <see cref="OnAnimationFinished"/>
    /// treats a lookup miss as "keep looping" already, so a Loop entry would be a table slot that is never
    /// read for anything different); this also keeps the table small - Loop entries were the majority
    /// (5207 of 9620 across the real export) and would triple its size for no observable effect.
    /// </summary>
    internal static Dictionary<int, AnimationEndInfo>? BuildAnimationEndByAnimDirection(
        IReadOnlyList<AnimDirIdsv>? idsvAnimDirs)
    {
        if (idsvAnimDirs == null || idsvAnimDirs.Count == 0)
        {
            return null;
        }

        Dictionary<int, AnimationEndInfo>? table = null;
        foreach (var entry in idsvAnimDirs)
        {
            if (entry.End == AnimationEndKind.Loop)
            {
                continue;
            }

            table ??= new Dictionary<int, AnimationEndInfo>(idsvAnimDirs.Count);
            table[entry.Anim * IdsvDirectionStride + entry.Direction] =
                new AnimationEndInfo { Kind = entry.End, ChainTargetAnimationId = entry.ChainTo };
        }

        return table;
    }

    /// <summary>
    /// Subscribes <paramref name="entity"/>'s <see cref="AnimatedSpriteComponent"/> (if it has one) to
    /// <see cref="OnAnimationFinished"/> exactly once, at spawn/adoption (see
    /// <see cref="ApplySpawnInitialization"/>/<see cref="AdoptPlayerPawn"/>) - bridging the engine's
    /// Once-finished event back to the original's Hold/Chain semantics (EntityManager.cs:257-281, see
    /// <see cref="OnAnimationFinished"/>'s own doc). The cached static delegate
    /// (<see cref="AnimationFinishedHandler"/>) means subscribing allocates nothing beyond the one-time
    /// delegate instance shared by every entity; the handler itself resolves the proxy from
    /// <c>sender</c>/<c>Owner.GameplayProxy</c> rather than capturing anything per-entity.
    /// </summary>
    /// <summary>
    /// No unsubscribe on destroy: <see cref="DestroyEntity(AlundraEntityScriptProxy)"/> only ever sets
    /// <see cref="EntityStatus.FlagToDestroy"/> (V1 scope is invisibility, not removal/slot recycling -
    /// see that method's own doc), never disposes the entity or its components, so the subscription
    /// this method makes lives exactly as long as the entity object itself and needs no explicit
    /// teardown. A FlagToDestroy entity's <see cref="OnAnimationFinished"/> calls (should its sampler
    /// still run while invisible) are themselves harmless: every per-frame pass that reads
    /// <see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/>/<c>TargetAnimationId</c>
    /// (<see cref="SyncAnimation"/>, <see cref="RunPendingEventTriggers"/>) already skips FlagToDestroy
    /// entities.
    /// </summary>
    internal static void SubscribeAnimationEndBridge(Entity entity)
    {
        var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
        if (animatedSprite == null)
        {
            return;
        }

        animatedSprite.AnimationFinished += AnimationFinishedHandler;
    }

    private static readonly EventHandler<Animation2d> AnimationFinishedHandler = OnAnimationFinished;

    /// <summary>
    /// Bridge from <see cref="AnimatedSpriteComponent.AnimationFinished"/> (fired once, from inside
    /// <c>Entity.Update</c>'s component pass, strictly BEFORE that same entity's own
    /// <see cref="AlundraEntityScriptProxy.Update"/> runs - see <c>Animation2dCompositionSampler.Update</c>'s
    /// clamp-at-DurationSeconds/IsFinished and <c>AnimatedSpriteComponent.Update</c>) back to the
    /// original's Hold/Chain semantics (EntityManager.cs:257-281):
    /// <list type="bullet">
    /// <item><description>Hold: sets <see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/> = 1
    /// (EntityManager.cs:273-275) - already read by <see cref="AlundraEntityScriptProxy.Update"/>'s pick
    /// phase for <c>DeactivateOnAnimationEnd</c>. The engine's own Once clamp already holds the last
    /// displayed frame's pose (the writer's terminal keyframe repeats it, see <c>SpriteWriter</c>'s
    /// class doc) - nothing else to do.</description></item>
    /// <item><description>Chain: sets <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> to the
    /// chain target (EntityManager.cs:277-279). <see cref="AlundraEntityScriptProxy.Update"/> calls
    /// <see cref="SyncAnimation"/> every frame regardless, so the very next call - later this SAME
    /// frame, since the component pass already ran - notices <c>TargetAnimationId</c> changed and
    /// switches animation: the same-tick effect the original gets from its own recursive
    /// <c>UpdateAnimation</c> call (EntityManager.cs:280), without this bridge needing to call
    /// <see cref="SyncAnimation"/> itself.</description></item>
    /// </list>
    /// A lookup miss (no entry for the just-finished (anim, direction), including every Loop entry -
    /// see <see cref="BuildAnimationEndByAnimDirection"/>) is a no-op: the engine already looped or
    /// nothing was ever wired up for this entity (degraded catalog). The original's own
    /// <c>AnimCompleteCounter++</c> per Loop cycle (EntityManager.cs:263) is NOT bridged - nothing in
    /// the ported V1 gameplay reads it yet, and <see cref="AnimatedSpriteComponent.AnimationFinished"/>
    /// does not even fire for a Loop animation (<c>Animation2dCompositionSampler</c> wraps instead of
    /// finishing) so there would be no signal to bridge it from.
    /// </summary>
    internal static void OnAnimationFinished(object? sender, Animation2d finishedAnimation)
    {
        if (sender is not AnimatedSpriteComponent component
            || component.Owner?.GameplayProxy is not AlundraEntityScriptProxy proxy
            || proxy.AnimationEndByAnimDirection == null)
        {
            return;
        }

        var key = (int)proxy.CurrentAnimationId * IdsvDirectionStride + proxy.AnimationDirection;
        if (!proxy.AnimationEndByAnimDirection.TryGetValue(key, out var end))
        {
            return;
        }

        if (end.Kind == AnimationEndKind.Hold)
        {
            proxy.ForceResetAnimationFlag = 1;
        }
        else if (end.Kind == AnimationEndKind.Chain)
        {
            proxy.TargetAnimationId = (uint)end.ChainTargetAnimationId;
        }
    }

    /// <summary>Best-effort integer read of one custom property; missing or malformed leaves 0/false -
    /// mirroring how the converter always emits these two keys, so a missing key is not expected but
    /// should not itself block a spawn the way a malformed <see cref="EntityRecordMapper"/> key does.</summary>
    private static bool TryGetRecordInt(TileMapObjectData record, string key, out int value)
    {
        if (record.CustomProperties.TryGetValue(key, out var raw) && int.TryParse(raw, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Builds one game entity from an "Entities" object-layer record.
    ///
    /// When <paramref name="record"/> carries a valid <c>PrefabAssetId</c> custom property and
    /// <paramref name="prefabLoader"/> successfully resolves it, the returned entity is a clone of that
    /// bank prefab (see <see cref="CreateEntityFromPrefab"/>) - so it carries the bank's
    /// sprite/collision components. Otherwise (missing/malformed link, no loader, loader throws or
    /// returns null) a single warning is logged and a bare entity is built instead (see
    /// <see cref="CreateBareEntityFromRecord"/>). Either way the result carries an
    /// <see cref="AlundraEntityScriptProxy"/> filled by <see cref="EntityRecordMapper"/>, with
    /// <see cref="EntityStatus.Loaded"/>. Does not add the entity to any world; the caller does that.
    ///
    /// <paramref name="prefabLoader"/> is a seam for unit tests: the live path
    /// (<see cref="InitializeWithWorld"/>) wires it over <c>World.Game.AssetContentManager.Load&lt;Entity&gt;</c>,
    /// which the headless unit test process cannot exercise (Alundra.csproj marks its own
    /// MonoGame.Framework.DesktopGL reference PrivateAssets="All" for game-folder deployment, so it never
    /// flows into Alundra.Tests's deps.json); tests inject a fake in-memory prefab instead.
    /// </summary>
    internal static Entity CreateEntityFromRecord(
        TileMapObjectData record, Func<Guid, Entity?>? prefabLoader, ISpriteRecordCatalog? spriteRecordCatalog = null,
        Entity? parentEntity = null)
    {
        if (TryGetPrefabAssetId(record, out var prefabAssetId))
        {
            Entity? prefab = null;
            string? failureReason = null;

            if (prefabLoader == null)
            {
                failureReason = "no prefab loader available";
            }
            else
            {
                try
                {
                    prefab = prefabLoader(prefabAssetId);
                    if (prefab == null)
                    {
                        failureReason = "prefab loader returned null";
                    }
                }
                catch (Exception ex)
                {
                    failureReason = ex.Message;
                }
            }

            if (prefab != null)
            {
                return CreateEntityFromPrefab(record, prefab, spriteRecordCatalog, parentEntity);
            }

            Logs.WriteWarning(
                $"AlundraWorldProxy: record '{record.Name}': could not clone prefab '{prefabAssetId}' "
                + $"({failureReason}); falling back to a bare entity.");
        }
        else
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: record '{record.Name}' has no valid '{PrefabAssetIdPropertyKey}' link; "
                + "falling back to a bare entity.");
        }

        return CreateBareEntityFromRecord(record, spriteRecordCatalog, parentEntity);
    }

    /// <summary>
    /// Parses the record's <c>PrefabAssetId</c> custom property (see <c>AlundraDataExtractor</c>'s
    /// tilemap exporter) into the bank prefab's asset id. Returns false when the key is missing or its
    /// value is not a valid <see cref="Guid"/>.
    /// </summary>
    internal static bool TryGetPrefabAssetId(TileMapObjectData record, out Guid prefabAssetId)
    {
        if (record.CustomProperties.TryGetValue(PrefabAssetIdPropertyKey, out var rawValue)
            && Guid.TryParse(rawValue, out prefabAssetId))
        {
            return true;
        }

        prefabAssetId = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Clones <paramref name="prefab"/> (a bank prefab loaded from <c>Entities/{Name}/{Name}.entity</c>,
    /// per <c>EntityBankPrefabWriter</c>) into a fresh, independent entity carrying that bank's
    /// sprite/collision components, renamed for this record and with its
    /// <see cref="AlundraEntityScriptProxy"/> filled from <paramref name="record"/>.
    /// </summary>
    internal static Entity CreateEntityFromPrefab(
        TileMapObjectData record, Entity prefab, ISpriteRecordCatalog? spriteRecordCatalog = null,
        Entity? parentEntity = null)
    {
        var entity = prefab.Clone();
        entity.Name = BuildEntityName(record);

        if (string.IsNullOrEmpty(entity.GameplayProxyClassName))
        {
            //The prefab is expected to carry AlundraEntityScriptProxy as its script class; fall back
            //explicitly rather than spawning an entity with no gameplay proxy at all.
            entity.GameplayProxyClassName = nameof(AlundraEntityScriptProxy);
        }

        //Creates/keeps the GameplayProxy (via ElementFactory, from GameplayProxyClassName) and calls its
        //Initialize(entity); InitializeWithWorld() runs later, when the engine integrates the entity.
        entity.Initialize();

        if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
        {
            ApplyRecord(record, proxy);
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog, parentEntity);

            // The prefab's root is the inert TransformComponent (SpriteWriter.WriteEntityPrefab, E3.a);
            // place it in the CasaEngine LOGICAL frame from the logical position
            // EntityRecordMapper/ApplySpawnInitialization just filled (PosZ already carries the -ModZ+1
            // header adjustment when a header was found).
            // Defensive null-check only: a bank prefab is expected to always carry a root component.
            if (entity.RootComponent != null)
            {
                entity.RootComponent.LocalTransform.Position = ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);

                // Resolve and cache the root's RenderProjectionComponent once, then re-project
                // immediately so the very first draw already shows the projected render pose rather
                // than whatever default position the prefab's projection carried before this spawn
                // wrote the root (see AlundraEntityScriptProxy.RenderProjection's own doc).
                proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();
                proxy.RenderProjection?.UpdateProjection();
            }
        }

        return entity;
    }

    /// <summary>
    /// Converts an entity's logical spawn position (<see cref="AlundraEntityScriptProxy.PosX"/> /
    /// <see cref="AlundraEntityScriptProxy.PosY"/> / <see cref="AlundraEntityScriptProxy.PosZ"/>, 16.16
    /// fixed-point Alundra pixels - see <see cref="EntityRecordMapper"/>) into the LOGICAL pose written
    /// onto the entity root's <c>LocalTransform.Position</c> (E3.a, docs/plan-e3-collisions.md decision
    /// E3-1) - consistently with <c>WorldWriter.ResolveTileCentreSpawn</c> (the converter's own
    /// tile-to-logical-pose conversion, used for the PlayerStart):
    /// <list type="bullet">
    /// <item><description><c>X = pixelX</c> (no conversion - CasaEngine's X already matches Alundra's).</description></item>
    /// <item><description><c>Y = pixelY</c>: Alundra's own down-positive depth, NOT flipped here - the
    /// flip from depth to a Y-up render position is now the render policy's job
    /// (<c>SimulationSpacePolicy.DeriveRenderPosition</c> under the world's TopDownElevation policy),
    /// applied every frame by the <c>RenderProjectionComponent</c> child a prefab's root now carries
    /// (<c>SpriteWriter.WriteEntityPrefab</c>), not baked into this snapshot.</description></item>
    /// <item><description><c>Z = elevationPixels</c> (<c>PosZ &gt;&gt; 16</c>): Alundra's elevation, kept
    /// on the logical Z axis rather than folded into Y - again the render policy's job to translate into
    /// a screen offset.</description></item>
    /// </list>
    /// This is a spawn-time snapshot of a logical position that can change at runtime (movement, event
    /// programs); every caller that writes it onto the root MUST also re-run the entity's
    /// <c>RenderProjectionComponent.UpdateProjection()</c> in the same frame so the sprite renders the
    /// new pose immediately rather than one frame late (component update order:
    /// <c>RootComponent.Update</c>, hence the projection, runs before <c>GameplayProxy.Update</c> -
    /// Entity.cs:473-504) - see <see cref="SyncTransform"/>.
    /// </summary>
    internal static Vector3 ResolveLogicalPosition(int posX, int posY, int posZ)
    {
        var pixelX = posX >> 16;
        var pixelY = posY >> 16;
        var elevationPixels = posZ >> 16;

        return new Vector3(pixelX, pixelY, elevationPixels);
    }

    /// <summary>
    /// Builds one bare game entity from an "Entities" object-layer record: a deterministically named
    /// entity carrying an <see cref="AlundraEntityScriptProxy"/> filled by <see cref="EntityRecordMapper"/>,
    /// with <see cref="EntityStatus.Loaded"/>. Used as the fallback when the record has no usable prefab
    /// link (see <see cref="CreateEntityFromRecord"/>). Unlike <see cref="CreateEntityFromPrefab"/> this
    /// never sets a world transform: a bare entity has no <c>RootComponent</c> to place (it carries no
    /// components at all), only the proxy's logical position fields - the existing "falling back to a
    /// bare entity" warning already covers this case, so it needs no separate warning of its own here.
    /// Does not add the entity to any world; the caller does that.
    /// </summary>
    internal static Entity CreateBareEntityFromRecord(
        TileMapObjectData record, ISpriteRecordCatalog? spriteRecordCatalog = null, Entity? parentEntity = null)
    {
        var entity = new Entity
        {
            Name = BuildEntityName(record),
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };

        //Creates the GameplayProxy (via ElementFactory, from GameplayProxyClassName) and calls its
        //Initialize(entity); InitializeWithWorld() runs later, when the engine integrates the entity.
        entity.Initialize();

        if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
        {
            ApplyRecord(record, proxy);
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog, parentEntity);
        }

        return entity;
    }

    /// <summary>Maps <paramref name="record"/> onto <paramref name="proxy"/> and marks it loaded.</summary>
    internal static void ApplyRecord(TileMapObjectData record, AlundraEntityScriptProxy proxy)
    {
        EntityRecordMapper.Map(record, proxy);
        proxy.Status = EntityStatus.Loaded;

        // Required plumbing for decision D2/D3, not part of the original struct's own zero-init (the
        // original never explicitly sets this either - see EntityManager.InitializeEntity): a freshly
        // spawned proxy's EventTrigger otherwise defaults to the C# int default 0, which IS
        // ScriptHelper.ProgramALoad, not ScriptHelper.ProgramUnknown(-1) - so a proxy created THIS FRAME
        // by a running script (0x2D/0x8B, via SpawnEntityByRecordId) would look, to
        // AlundraWorldProxy.RunPendingEventTriggers (called later this SAME frame, after any such
        // spawn), exactly like an entity whose pick phase already ran and chose slot A - triggering its
        // Load program immediately, without ever going through PickEventTrigger's own Loaded -> Normal
        // transition. Explicitly seeding ProgramUnknown here preserves the documented "next frame" spawn
        // visibility (docs/intro-roadmap.md §0 deviation B) under the new per-entity architecture.
        proxy.EventTrigger = ScriptHelper.ProgramUnknown;
    }

    /// <summary>
    /// Faithful port of the rest of <c>EntityManager.InitializeEntity</c> @ 0x80039D04 that
    /// <see cref="EntityRecordMapper"/> could not do on its own (no header, no owning entity) - run after
    /// <see cref="ApplyRecord"/> for both the prefab and the bare creation path (see
    /// <see cref="CreateEntityFromPrefab"/>/<see cref="CreateBareEntityFromRecord"/>).
    /// <list type="bullet">
    /// <item><description><c>entity.LogicContextEntity = entity</c> (EntityManager.cs:147,
    /// <c>InitializeCodePrograms</c> @ 0x8004201C) is unconditional in the original - it needs nothing but
    /// the entity that was just created, so it always runs here too, header or not.</description></item>
    /// <item><description>Everything else below it (<c>Flags</c>, <c>SpriteProgramIndexes</c>,
    /// <c>SetEntityDimensions</c>, the <c>PosZ</c> header adjustment, <c>ModdedPosX/Y/Z</c>, and the
    /// spawn-time animation/direction fields) needs the bank's <c>SpriteRecord.Header</c>
    /// (<see cref="SpriteRecordHeader"/>), which the original always has by construction - <c>SpawnEntity</c>
    /// (GameEngine.cs:721-726) returns null before ever calling <c>InitializeEntity</c> when the sprite
    /// record fails to resolve. <paramref name="spriteRecordCatalog"/> can fail to resolve one here (file
    /// missing, or this record's prefab link missing/invalid) in ways the original never could; when that
    /// happens this entire block is skipped and the entity keeps the plain <see cref="EntityRecordMapper"/>
    /// output - documented degraded mode, see <see cref="SpriteRecordCatalog"/>'s class doc.</description></item>
    /// </list>
    /// </summary>
    internal static void ApplySpawnInitialization(
        TileMapObjectData record, Entity entity, AlundraEntityScriptProxy proxy, ISpriteRecordCatalog? spriteRecordCatalog,
        Entity? parentEntity = null)
    {
        proxy.LogicContextEntity = entity;

        // EntityManager.cs:52: entity.ParentEntity = parentEntity is unconditional, independent of
        // whether the sprite record header resolves below - null for every map-load spawn (the original
        // always passes null there too, GameEngine.cs:629-645 InitializeEntitySlots), non-null only for
        // the dynamic-spawn opcode 0x2D (AlundraWorldProxy.SpawnEntityByRecordId).
        proxy.ParentEntity = parentEntity;

        if (spriteRecordCatalog == null
            || !TryGetPrefabAssetId(record, out var prefabAssetId)
            || !spriteRecordCatalog.TryGet(prefabAssetId, out var header))
        {
            return;
        }

        // EntityManager.cs:92-93 (Entity.Flags packing documented by EntityFlags).
        proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));

        // EntityManager.cs:95-100.
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = header.ProgramLoad;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramBMap] = 0;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = header.ProgramTick;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramDTouch] = header.ProgramTouch;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = header.ProgramDeactivate;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = header.ProgramInteract;

        SetEntityDimensions(proxy, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);

        // Resolve this entity's IDSV table once, from the catalog entry already fetched above, and
        // stash it on the proxy - see AlundraEntityScriptProxy.IdsvByAnimDirection's doc comment and
        // WallPlacementOverlay.ApplyEntitySortKey's frame-0-only deviation note. Only frame 0 of each
        // (anim, direction) pair is kept; the per-frame lists Data/sprite-records.json carries are not
        // needed on this hot-path table.
        proxy.IdsvByAnimDirection = BuildIdsvByAnimDirection(header.IdsvAnimDirs);
        proxy.AnimationEndByAnimDirection = BuildAnimationEndByAnimDirection(header.IdsvAnimDirs);
        SubscribeAnimationEndBridge(entity);

        // EntityManager.cs:119: the mapper seeded PosZ with the raw pre-clamp elevation
        // (EntityRecordMapper's documented caveat); this is the -ModZ+1 offset InitializeEntity applies
        // once the header (hence ModZ) is known. The ground-height clamp (EntityManager.cs:130-136) stays
        // out - it needs the map's collision cells, a later chantier.
        proxy.PosZ = proxy.PosZ - proxy.ModZ + 1;

        // EntityManager.cs:123-125.
        proxy.ModdedPosX = proxy.PosX + proxy.ModX;
        proxy.ModdedPosY = proxy.PosY + proxy.ModY;
        proxy.ModdedPosZ = proxy.PosZ + proxy.ModZ;

        // GameEngine.cs:752-753: SpawnEntity always passes animationId=0 and reads the facing off the
        // record's own SpriteDirection (not the header) - a missing/malformed key defaults to 0, same as
        // a record whose SpriteDirection happens to be 0.
        TryGetRecordInt(record, "SpriteDirection", out var spriteDirection);
        const uint animationId = 0;
        var direction = AnimationTables.CardinalDirectionTable[spriteDirection & 0x3];

        // EntityManager.cs:85-88.
        proxy.CurrentAnimationId = ~animationId;
        proxy.CurrentDirection = ~direction;
        proxy.TargetAnimationId = animationId;
        proxy.TargetDirection = direction;
    }

    /// <summary>
    /// E2 replacement for the old <c>SpawnPlayerEntity</c> (which used to clone a SECOND hero prefab
    /// itself): the ENGINE now spawns the hero pawn and possesses it with an <see cref="AlundraPlayerController"/>
    /// (<c>World.LoadContent</c> -&gt; <c>InitializePlayerControllers</c>, CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:221-252/282-297,
    /// strictly before <c>CreateGameplayProxy</c>/<c>InitializeWithWorld</c> run - so by the time this
    /// method runs, the pawn is already in <c>world.Entities</c>, already <c>Initialize</c>/
    /// <c>InitializeWithWorld</c>'d, and already positioned at the map's <c>PlayerStart</c> component by
    /// <c>CreateLocalPlayerController</c>, World.cs:350-367). This method only ADOPTS that pawn (finds its
    /// controller via <c>world.PlayerControllers</c>, World.cs:76) and applies the New Game LOGICAL state -
    /// same fields the old <c>SpawnPlayerEntity</c> set, a V1 port of <c>ResetEntityState</c>
    /// (GameEngine.cs:648-670), called BEFORE any "Entities" record is spawned (their own spawn-zone gate
    /// reads the player's tile): spawn position (New Game tile (33,59), tile-centre 16.16 fixed-point -
    /// GameInitializer.cs's New Game branch via <see cref="AlundraGameState"/>'s own New Game constants),
    /// <c>TargetAnimationId</c>/<c>TargetDirection</c> = <see cref="AlundraGameState.ResetAnimationId"/>/
    /// <see cref="AlundraGameState.ResetDirectionId"/> (54/"LoadingMap", 0/down), <c>Status = Normal</c>
    /// (NOT <c>Loaded</c> - GameEngine.cs:661: the hero has no Load program, unlike every record-spawned
    /// entity), <c>Flags</c>/<c>SpriteProgramIndexes</c>/<c>AnimSetsByAnim</c> from the hero's own
    /// sprite-records.json header when the catalog has one for it.
    ///
    /// Deviation note: the engine already positioned the pawn's transform at the <c>PlayerStart</c>
    /// component (logical pose (804, 952, 0) on map 389, equal to <c>ResolveLogicalPosition</c> of this same
    /// New Game tile) - this method's own <see cref="ResolveLogicalPosition"/> call below OVERWRITES that
    /// with the logical position instead, which is harmless (same result) but makes explicit that
    /// <c>AlundraEntityScriptProxy</c>'s logical PosX/PosY/PosZ, not the engine's PlayerStart transform, is
    /// the field this proxy's own <see cref="SyncTransform"/> re-derives from every frame going forward
    /// (decision D2's "logical state wins" rule).
    ///
    /// Deliberately NOT ported (still out of E2's own scope - a real <c>InitializeGameState</c>/full
    /// <c>PlayerManager</c>): <c>Hp</c>/<c>HpMax</c> (<c>PlayerManager.GetPlayerHp/HpMax</c>),
    /// <c>g_activeCollisionEntity = null</c> (this world's own <see cref="ActiveCollisionEntity"/> already
    /// starts null), <c>g_currentWeaponFlags</c>/weapon item id, the warp timer/effect resets
    /// (<c>g_playerWarpTimer</c>, <c>g_isWarpDisabled</c>, <c>g_playerWarpEffect</c>,
    /// <c>g_playerEffectTransitionCooldown</c>, <c>ResetWarpLockTimer</c>) - none of these have any
    /// observable effect on E2's own ported <see cref="AlundraPlayerManager.MovePlayer"/> subset. No
    /// camera-follow yet (E5).
    /// </summary>
    private void AdoptPlayerPawn(World world)
    {
        var playerController = world.PlayerControllers.OfType<AlundraPlayerController>().FirstOrDefault();
        _playerController = playerController;

        if (playerController?.Pawn == null)
        {
            if (!_loggedNoPlayerController)
            {
                _loggedNoPlayerController = true;
                Logs.WriteWarning(
                    $"AlundraWorldProxy: no {nameof(AlundraPlayerController)} possessing a pawn in world "
                    + $"'{world.Name}' (missing/misconfigured player_startup_settings_asset_id, "
                    + "player_controller_class, or default_pawn_asset_id); no player entity adopted, no "
                    + "fallback spawn.");
            }

            return;
        }

        var entity = playerController.Pawn;

        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: the engine-spawned pawn in world '{world.Name}' did not produce an "
                + $"{nameof(AlundraEntityScriptProxy)} (GameplayProxyClassName on the pawn prefab); no "
                + "player entity adopted.");
            return;
        }

        proxy.IsPlayer = true;
        proxy.ScriptHost = this;
        proxy.LogicContextEntity = entity;
        proxy.ParentEntity = null;
        proxy.Status = EntityStatus.Normal;
        proxy.EntityRefId = -1; // not an "Entities" layer record - no slot to index by.
        proxy.EventTrigger = ScriptHelper.ProgramUnknown; // hygiene only - IsPlayer already excludes it from RunPendingEventTriggers regardless of value.

        proxy.PosX = (AlundraGameState.CameraTileX * TileWidth + TileWidth / 2) << 16;
        proxy.PosY = (AlundraGameState.CameraTileY * TileHeight + TileHeight / 2) << 16;
        proxy.PosZ = 0;
        // PhysicsEngine.cs:1698-1700, same formula EntityRecordMapper seeds every record's own tile from.
        proxy.TileX = (proxy.PosX >> 16) / TileWidth;
        proxy.TileY = (proxy.PosY >> 16) / TileHeight;
        proxy.TileZ = proxy.PosZ >> 20;

        proxy.TargetAnimationId = AlundraGameState.ResetAnimationId;
        proxy.TargetDirection = AlundraGameState.ResetDirectionId;
        // EntityManager.cs:85-88 - bit-complemented so the very first per-frame animation sync always fires.
        proxy.CurrentAnimationId = ~AlundraGameState.ResetAnimationId;
        proxy.CurrentDirection = ~AlundraGameState.ResetDirectionId;

        // Documented stub for AlundraPlayerManager's faithful LoadingMap(0x36) port
        // (PlayerManager.cs:914-916: "if IsOnGround != 0, break" - i.e. stay in LoadingMap): V1 has no
        // gravity/collision (D4/E2), so IsOnGround can never become a real ground-contact reading before
        // that chantier (E3). Pinning it to 1 here reproduces the ONE case that matters for a fresh New
        // Game spawn - a grounded hero - so MovePlayer's LoadingMap case takes the "stay" branch instead
        // of falling to the NOT-ported Jump case; the actual LoadingMap -> Idle exit is the animation
        // Chain bridge instead (anim 54 -> 0, see AlundraWorldProxy.OnAnimationFinished), matching the
        // original's own trailing-control-frame-driven animation switch rather than a ground check.
        proxy.IsOnGround = 1;

        var assetInfo = AssetCatalog.Get(HeroAssetName);
        if (assetInfo != null && SpriteRecordCatalog != null && SpriteRecordCatalog.TryGet(assetInfo.Id, out var header))
        {
            proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = header.ProgramLoad;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramBMap] = 0;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = header.ProgramTick;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramDTouch] = header.ProgramTouch;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = header.ProgramDeactivate;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = header.ProgramInteract;
            proxy.IdsvByAnimDirection = BuildIdsvByAnimDirection(header.IdsvAnimDirs);
            proxy.AnimationEndByAnimDirection = BuildAnimationEndByAnimDirection(header.IdsvAnimDirs);
            proxy.AnimSetsByAnim = header.AnimSets;
        }
        else if (!_loggedNoHeroHeader)
        {
            _loggedNoHeroHeader = true;
            Logs.WriteDebug(
                $"AlundraWorldProxy: no sprite-records.json header found for the hero prefab in world "
                + $"'{world.Name}'; Flags/SpriteProgramIndexes/AnimSetsByAnim left at their defaults.");
        }

        SubscribeAnimationEndBridge(entity);

        // Overwrites the engine's own PlayerStart-derived transform - see this method's own doc
        // ("Deviation note") for why that is intentional, not redundant.
        if (entity.RootComponent != null)
        {
            entity.RootComponent.LocalTransform.Position = ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);

            // The pawn is already in world.Entities by the time this method runs (see this method's
            // own doc), so - unlike CreateEntityFromPrefab's spawn-time call - this re-projection is
            // not a no-op: without it the sprite would keep showing the engine's PlayerStart-derived
            // render pose for one extra frame instead of the New Game logical pose just written above.
            proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();
            proxy.RenderProjection?.UpdateProjection();
        }

        // The pawn is already in world.Entities (the engine added it) but not yet in this proxy's own
        // _spawnedEntities - add it so the per-entity animation/transform sync passes (SyncAnimation/
        // SyncTransform) see it every frame, exactly like the old SpawnPlayerEntity used to.
        _spawnedEntities.Add(entity);
        PlayerEntity = proxy;
    }

    /// <summary>
    /// Port of <c>InitializeMapEvents</c> (GameEngine.cs:476-583), restricted to the record-driven half
    /// (the fixed 0x40-slot pre-clear loop is a PSX-specific fixed-array reset with no equivalent need
    /// here - <see cref="_mapEvents"/> is just built fresh every world load). One <see cref="AlundraMapEvent"/>
    /// per "MapEvents" object-layer record whose <c>EventCodesBIndex</c> custom property is non-zero, in
    /// record order, each with <see cref="AlundraMapEvent.Entity"/> = <see cref="PlayerEntity"/> and a
    /// fresh <see cref="EventProgramState"/> - exactly like the original's <c>Entity = PlayerEntity</c>,
    /// <c>EventData = new EventProgramState()</c>. Left empty when there is no "MapEvents" layer, or no
    /// <see cref="PlayerEntity"/> to run them against (<see cref="RunMapEventsPass"/> always executes
    /// against the player; a null player has nothing to drive them with).
    /// </summary>
    private void BuildMapEvents(TileMapObjectLayerData? mapEventsLayer)
    {
        if (mapEventsLayer == null || PlayerEntity == null)
        {
            return;
        }

        foreach (var record in mapEventsLayer.Objects)
        {
            TryGetRecordInt(record, "EventCodesBIndex", out var programBMap);
            if (programBMap == 0)
            {
                continue;
            }

            TryGetRecordInt(record, "Index", out var id);
            TryGetRecordInt(record, "X1", out var x1);
            TryGetRecordInt(record, "Y1", out var y1);
            TryGetRecordInt(record, "X2", out var x2);
            TryGetRecordInt(record, "Y2", out var y2);

            _mapEvents.Add(new AlundraMapEvent
            {
                Id = id,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                ProgramBMap = programBMap,
                Entity = PlayerEntity,
            });
        }
    }

    /// <summary>
    /// Port of <c>EntityManager.SetEntityDimensions</c> @ 0x80039C40: derives the entity's collision/mod
    /// box from its bank header's raw offset/size fields (already 16.16-fixed-point-free integers; the
    /// original shifts them into 16.16 itself with <c>&lt;&lt; 16</c>). Constants
    /// <c>0x4e00000</c>/<c>0x3c00000</c>/<c>0x7800000</c> are ported verbatim, unexplained in the original
    /// beyond their use as screen-clip bounds.
    /// </summary>
    internal static void SetEntityDimensions(
        AlundraEntityScriptProxy proxy, int offsetX, int offsetY, int offsetZ, int sizeX, int sizeY, int sizeZ)
    {
        proxy.NegModX = -(offsetX << 16);
        proxy.NegModY = -(offsetY << 16);
        proxy.ModX = offsetX << 16;
        proxy.ModY = offsetY << 16;
        proxy.ModZ = offsetZ << 16;
        proxy.ScreenClipX = 0x4e00000 - ((offsetX + sizeX) << 16);
        proxy.ScreenClipY = 0x3c00000 - ((offsetY + sizeY) << 16);
        proxy.ScreenClipZ = 0x7800000 - ((offsetZ + sizeZ) << 16);

        proxy.Width = sizeX == 0 ? 0 : (sizeX << 16) - 1;
        proxy.Height = sizeY == 0 ? 0 : (sizeY << 16) - 1;
        proxy.Depth = sizeZ == 0 ? 0 : (sizeZ << 16) - 1;
    }

    internal static string BuildEntityName(TileMapObjectData record)
    {
        var baseName = string.IsNullOrEmpty(record.Name) ? "Entity" : record.Name;
        return record.CustomProperties.TryGetValue("EntityName", out var entityName) && !string.IsNullOrEmpty(entityName)
            ? $"{baseName} ({entityName})"
            : baseName;
    }

    /// <summary>
    /// World-level half of the frame (decision D2/D3, docs/plan-conversion-totale.md §2): every spawned
    /// entity now picks/runs/syncs itself in its OWN <see cref="AlundraEntityScriptProxy.Update"/>, driven
    /// by the ENGINE's own entity update loop - which always runs BEFORE this world's own
    /// <see cref="Update"/> (<c>World.Update</c>, CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:443-491).
    /// So by the time this method runs, every entity has already had its own pick/run/sync this frame; this
    /// only covers what the ORIGINAL still runs at the manager/world level: MapEvents
    /// (<see cref="RunMapEventsPass"/>, port of <c>RunMapEvents</c>) and the do/while catch-up re-scan
    /// (<see cref="RunPendingEventTriggers"/>, port of <c>UpdateEntitiesEvents</c>'s phase-2 loop, decision
    /// D3) for any entity another entity's script triggered DURING this same frame's entity pass.
    /// <c>RunEntityEventsPass</c> et al are removed - see <see cref="AlundraEntityScriptProxy.Update"/>'s
    /// own doc for the accepted ordering deviation this implies.
    /// </summary>
    public override void Update(float elapsedTime)
    {
        // DEBUG ONLY - runs unconditionally (unlike the entity passes below, which are skipped when
        // nothing was spawned) so the map can still be flown over even for a world with no entities.
        UpdateDebugCameraPan(elapsedTime);

        ApplyOriginalBackgroundClearColorOnce();
        UpdateAndDrawBackdrop(elapsedTime);

        if (PlayerEntity != null)
        {
            RunMapEventsPass(PlayerEntity, _mapEvents, EventProgramRunner, GameState.PlayerControlFlags);
        }

        if (_spawnedEntities.Count == 0)
        {
            return;
        }

        _updateProxies.Clear();
        foreach (var entity in _spawnedEntities)
        {
            if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
            {
                _updateProxies.Add(proxy);
            }
        }

        RunPendingEventTriggers(_updateProxies, EventProgramRunner);

        // Wall/sprite depth interleave (Slice B) - see WallPlacementOverlay's class doc. Gated on the
        // overlay actually having been populated: with no wall placements loaded (missing/malformed
        // property) there is nothing to interleave against, so entities are left at whatever
        // DepthSortable2DComponent defaults their prefab already carries instead of paying a per-frame
        // field write for nothing.
        if (_wallPlacementOverlayApplied)
        {
            RunWallInterleaveSortKeyPass(_spawnedEntities);
        }
    }

    /// <summary>
    /// Port of <c>RunMapEvents</c> (GameEngine.cs:1667-1718, 0x8003c67c). Always executes against
    /// <paramref name="player"/> - every MapEvent's own logic entity starts as the player
    /// (<see cref="BuildMapEvents"/>) and can only ever be retargeted by opcode 0x66 (not ported, never
    /// reached by map 389's own programs - docs/intro-roadmap.md §1.5).
    /// </summary>
    internal static void RunMapEventsPass(
        AlundraEntityScriptProxy player, IReadOnlyList<AlundraMapEvent> mapEvents, IEventProgramRunner runner,
        uint playerControlFlags)
    {
        if ((playerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0)
        {
            return;
        }

        for (var i = 0; i < mapEvents.Count; i++)
        {
            var mapEvent = mapEvents[i];

            if ((mapEvent.ProgramBMap & 0x7F) == 0)
            {
                continue;
            }

            var mapEventEntity = mapEvent.Entity ?? player;

            if (player.TileX < mapEvent.X1 || player.TileX > mapEvent.X2
                || player.TileY < mapEvent.Y1 || player.TileY > mapEvent.Y2)
            {
                // Out-of-zone reset, GameEngine.cs:1690-1697 - ported exactly, including the somewhat
                // surprising choice of resetting the MAP EVENT'S OWN logic entity's EventProgramState
                // (not the player's own persistent one) each time the player leaves the zone.
                mapEventEntity.ChildEntity = null;
                mapEventEntity.EventProgramState.Sp = 0;
                mapEventEntity.RelativeWarpOffsetX = 0;
                mapEventEntity.Index = player.Index;
                continue;
            }

            player.ProgramIndexes[ScriptHelper.ProgramBMap] = mapEvent.ProgramBMap;
            player.MapEventProgramId = mapEvent.ProgramBMap;
            // GameEngine.cs:1702: the original indexes the FIXED g_mapEvents[0x40] array by record
            // position (InitializeMapEvents sets g_mapEvents[i].Id = i for every slot, occupied or not),
            // so "i" there is the record's own slot index. mapEvents here is compacted (only records with
            // EventCodesBIndex != 0 are kept - see BuildMapEvents), so the loop's own "i" is the compacted
            // list position, NOT the record index; mapEvent.Id carries the real record index instead.
            player.EventTrigger = mapEvent.Id;
            player.LogicEntity = mapEventEntity;
            player.EventProgramState.CopyFrom(mapEvent.EventData);

            runner.RunScript(player, ScriptHelper.ProgramBMap);

            mapEvent.EventData.CopyFrom(player.EventProgramState);
            mapEvent.Entity = player.LogicEntity;
            mapEvent.ProgramBMap = player.ProgramIndexes[ScriptHelper.ProgramBMap];
        }
    }

    /// <summary>
    /// Decision D3 (docs/plan-conversion-totale.md §2): port of the do/while re-scan half of
    /// <c>EntityManager.UpdateEntitiesEvents</c> (EntityManager.cs:874-921), applied here to whatever
    /// <see cref="AlundraEntityScriptProxy.EventTrigger"/> another entity's OWN <c>Update</c> set on it
    /// earlier THIS SAME FRAME (<see cref="AlundraEntityScriptProxy.PickEventTrigger"/> having already run
    /// for every entity, since the engine updates entities before this world - see this class's own
    /// <see cref="Update"/> doc). The player is excluded, same as the original's own loop starting at
    /// index 1 - its own trigger (set by <see cref="RunMapEventsPass"/>) is consumed directly by that
    /// method's own <c>RunScript</c> call, never by this re-scan.
    /// </summary>
    internal static void RunPendingEventTriggers(IReadOnlyList<AlundraEntityScriptProxy> entities, IEventProgramRunner runner)
    {
        bool keepGoing;

        do
        {
            keepGoing = false;

            // Indexed for, not foreach: an IReadOnlyList<T>-typed foreach goes through the interface's
            // own IEnumerator<T> (a boxed enumerator on every call, unlike List<T>'s own struct
            // enumerator) - this pass now runs every frame, so that allocation is no longer free to skip.
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                if (entity.IsPlayer || entity.EventTrigger == ScriptHelper.ProgramUnknown)
                {
                    continue;
                }

                entity.RunPickedEvent(runner);
                keepGoing = true;
            }
        } while (keepGoing);
    }

    /// <summary>
    /// DEBUG ONLY - temporary tool, to be gated/replaced once the real camera-follow (E4) lands. Pans the
    /// world's camera (first entity carrying a <see cref="Camera2dComponent"/>) with the gamepad's right
    /// thumbstick so the whole map can be flown over at runtime to inspect spawned entities.
    ///
    /// Reads the right stick through the engine's own <c>CasaEngineGame.InputComponent.GamePadManager</c>
    /// (see <c>CasaEngine.Framework.Input.InputComponent</c>/<c>CasaEngine.Engine.Input.GamePad</c>)
    /// rather than MonoGame's <c>GamePad.GetState</c> directly, since that manager is already reachable
    /// off <see cref="World.Game"/> and is what every other in-engine input read goes through
    /// (<c>InputMapping.Update</c>). A no-op whenever no gamepad is connected on player one, or no
    /// camera component can be found (warns once in the latter case).
    ///
    /// Axis mapping: MonoGame's right-stick Y is positive up; the camera lives in RENDER space (its
    /// <c>Target</c> is a world/render position, not a logical entity pose), where "more positive = further
    /// up/north" (the same Y-up convention <c>SimulationSpacePolicy.DeriveRenderPosition</c> produces for a
    /// projected entity - see <see cref="ResolveLogicalPosition"/>'s own doc for why entities themselves no
    /// longer negate Alundra's down-positive Y here), so stick-up must increase <c>Target.Y</c> - no sign
    /// flip needed, unlike Alundra's own Y. Stick X maps directly onto world X the same way. <c>Target.Z</c>
    /// is left untouched.
    /// </summary>
    private void UpdateDebugCameraPan(float elapsedTime)
    {
        if (!_debugCameraLookupDone)
        {
            _debugCameraLookupDone = true;

            // Looked up by COMPONENT, not by the reference name "camera": EntityReference.Load's
            // shared-asset branch clones the asset without applying the reference's name, so the live
            // entity is named after the asset ("AlundraCamera"). Taking the first Camera2dComponent in
            // the world mirrors DefaultRuntimeViewBootstrapper's own camera pick, so the pan always
            // drives the camera the runtime view actually uses.
            if (_world != null)
            {
                foreach (var entity in _world.Entities)
                {
                    _debugCamera = entity.GetComponent<Camera2dComponent>();
                    if (_debugCamera != null)
                    {
                        break;
                    }
                }
            }

            if (_debugCamera == null)
            {
                Logs.WriteWarning(
                    $"AlundraWorldProxy: no Camera2dComponent found in world "
                    + $"'{_world?.Name}'; debug camera pan disabled.");
            }
        }

        var gamePadManager = _world?.Game?.InputComponent?.GamePadManager;
        if (_debugCamera == null || gamePadManager == null)
        {
            return;
        }

        var gamePad = gamePadManager.GetGamePad(PlayerIndex.One);
        if (!gamePad.IsConnected)
        {
            return;
        }

        // DEBUG ONLY - Back (Select) toggles the engine's physics wireframes, off by default at world
        // load (see InitializeWithWorld), so collision boxes can be inspected while flying the camera.
        if (gamePad.BackJustPressed)
        {
            var physicsDebug = _world?.Game?.PhysicsDebugViewRendererComponent;
            if (physicsDebug != null)
            {
                physicsDebug.DisplayPhysics = !physicsDebug.DisplayPhysics;
            }
        }

        _debugCamera.Target = ComputeDebugCameraPanTarget(
            _debugCamera.Target, gamePad.RightStickX, gamePad.RightStickY, elapsedTime);
    }

    /// <summary>
    /// DEBUG ONLY (see <see cref="UpdateDebugCameraPan"/>) - the pure math factored out for unit testing:
    /// applies a per-axis deadzone to the raw stick values, then moves <paramref name="currentTarget"/> by
    /// stick * <see cref="DebugCameraPanSpeedPixelsPerSecond"/> * <paramref name="elapsedTime"/> on X/Y,
    /// leaving Z untouched.
    /// </summary>
    internal static Vector3 ComputeDebugCameraPanTarget(
        Vector3 currentTarget, float stickX, float stickY, float elapsedTime)
    {
        var x = MathF.Abs(stickX) < DebugCameraPanDeadZone ? 0f : stickX;
        var y = MathF.Abs(stickY) < DebugCameraPanDeadZone ? 0f : stickY;

        return new Vector3(
            currentTarget.X + x * DebugCameraPanSpeedPixelsPerSecond * elapsedTime,
            currentTarget.Y + y * DebugCameraPanSpeedPixelsPerSecond * elapsedTime,
            currentTarget.Z);
    }

    /// <summary>
    /// Sets the world's runtime view <see cref="CasaEngine.Framework.Rendering.RenderView.ClearColor"/>
    /// to the original engine's own background clear (<c>AlundraGame.Draw</c>'s
    /// <c>GraphicsDevice.Clear(Color.Black)</c>, both for the game's off-screen render target and the
    /// final backbuffer blit - alundra-datas-analyser/AlundraTools/AlundraGame/AlundraGame.cs:199,236)
    /// instead of the engine's default <c>Color.CornflowerBlue</c>
    /// (<see cref="CasaEngine.Framework.Application.DefaultRuntimeViewBootstrapper"/>): without this,
    /// every pixel no cell tile (or, now, no <see cref="BackdropRenderer"/> layer) covers shows
    /// turquoise instead of the black the original always drew there. Retried lazily once per world
    /// from <see cref="Update"/> (see <see cref="_clearColorApplied"/>'s own doc for why
    /// <see cref="InitializeWithWorld"/> is too early to find the view).
    /// </summary>
    private void ApplyOriginalBackgroundClearColorOnce()
    {
        if (_clearColorApplied || _world?.Game == null)
        {
            return;
        }

        foreach (var view in _world.Game.GameManager.ViewManager.Views)
        {
            if (view.World == _world)
            {
                view.ClearColor = Color.Black;
                _clearColorApplied = true;
                break;
            }
        }
    }

    /// <summary>Ticks and draws this world's scrolling background layers - see
    /// <see cref="BackdropRenderer"/>'s class doc for the render pass/camera-space reasoning. A no-op
    /// when the world has no backdrop companion at all, or one with neither a Tiles-mode layer nor the
    /// overlay tint (the common case), or before the engine's <see cref="SpriteRendererComponent"/>/
    /// debug camera are resolvable.</summary>
    private void UpdateAndDrawBackdrop(float elapsedTime)
    {
        if (!_backdropRenderer.HasContent || _world?.Game == null)
        {
            return;
        }

        _backdropRenderer.Tick(elapsedTime);

        var spriteRenderer = _world.Game.GetGameComponent<SpriteRendererComponent>();
        if (spriteRenderer == null)
        {
            return;
        }

        // Reuses the same camera the debug pan drives (see UpdateDebugCameraPan, which already ran
        // earlier this frame and resolved _debugCamera) - both are "the world's camera", and the
        // runtime has no other camera reference yet (E4 follow-up).
        var cameraPosition = _debugCamera?.Target ?? Vector3.Zero;
        _backdropRenderer.Draw(spriteRenderer, cameraPosition, _world.Game.ScreenSizeWidth, _world.Game.ScreenSizeHeight);
    }

    /// <summary>
    /// Loops <see cref="SyncAnimation"/> over <paramref name="entities"/> - kept as its own method (rather
    /// than inlined at its one remaining call site) since it is independently unit-tested
    /// (AlundraWorldProxyAnimationSyncTests). The world's own <see cref="Update"/> no longer calls this: as
    /// of decision D2, each entity syncs itself from its own <see cref="AlundraEntityScriptProxy.Update"/>
    /// (via <see cref="SyncAnimation"/> directly) - see that method's own doc.
    /// </summary>
    internal static void RunAnimationSyncPass(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            SyncAnimation(entity);
        }
    }

    /// <summary>
    /// Per-entity target-resolution part of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4
    /// (EntityManager.cs:209-224 only - see <see cref="TryResolveAnimationTarget"/>), then bridges a
    /// resolved change onto <paramref name="entity"/>'s own <see cref="AnimatedSpriteComponent"/> (see
    /// <see cref="TrySelectAnimationByNameSuffix"/>). Called once per frame for every spawned entity, from
    /// <see cref="AlundraEntityScriptProxy.Update"/> (moved there from this world's own per-frame pass -
    /// decision D2, docs/plan-conversion-totale.md §2) - a no-op for an entity with no
    /// <see cref="AlundraEntityScriptProxy"/> (defensive only; every caller already knows it has one).
    ///
    /// By the time any entity's own first <c>Update</c> runs, the engine has already integrated it
    /// (<c>World.InternalAddEntities</c>, called before any entity's <c>GameplayProxy.Update</c> ever
    /// runs), so its <see cref="AnimatedSpriteComponent.Animations"/> list is already populated - and every
    /// freshly spawned entity has <c>CurrentAnimationId = ~TargetAnimationId</c> (spawn-time bit-complement,
    /// see <see cref="ApplySpawnInitialization"/>/<see cref="SpawnPlayerEntity"/>, guaranteed different from
    /// <c>TargetAnimationId</c>), so the very first sync always fires and sets the entity's initial visual.
    ///
    /// Frame-level animation state (<c>Frame</c>/<c>NextFrameDelay</c>/<c>AnimCompleteCounter</c>, the rest
    /// of <c>UpdateAnimation</c>) stays out of scope: CasaEngine's own <c>Animation2dCompositionSampler</c>
    /// (driven by <see cref="AnimatedSpriteComponent.Update"/>) already owns frame timing once the right
    /// animation is selected.
    /// </summary>
    internal static void SyncAnimation(Entity entity)
    {
        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            return;
        }

        // Destroyed-entity visibility (structural piece for the search-driven destroy opcodes, 0x2E
        // in particular): once an entity is flagged for destruction it stops being drawn and stops
        // being synced here - see DestroyEntity's own V1 scope note on why this is invisibility
        // rather than full removal/slot recycling. Checked against FlagToDestroy specifically, not
        // EntityStatus.Destroyed (numeric value 0, the default AlundraEntityScriptProxy.Status a
        // freshly-constructed-but-never-spawned proxy carries) - no ported code path ever transitions
        // an entity all the way to Destroyed in V1 (see EntityStatus's own doc on slot recycling).
        if (proxy.Status == EntityStatus.FlagToDestroy)
        {
            entity.IsVisible = false;
            return;
        }

        if (!TryResolveAnimationTarget(proxy, out var newCurrentAnimationId, out var newAnimationDirection))
        {
            return;
        }

        proxy.CurrentAnimationId = newCurrentAnimationId;
        proxy.AnimationDirection = newAnimationDirection;

        var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
        if (animatedSprite == null)
        {
            return;
        }

        if (TrySelectAnimationByNameSuffix(animatedSprite, proxy.CurrentAnimationId, proxy.AnimationDirection, out var selected))
        {
            animatedSprite.SetCurrentAnimation(selected, forceReset: true);
        }
    }

    /// <summary>
    /// Transform re-derivation: re-applies <see cref="ResolveLogicalPosition"/> to every spawned entity's
    /// <c>RootComponent.LocalTransform.Position</c> from its CURRENT logical
    /// <see cref="AlundraEntityScriptProxy.PosX"/>/<see cref="AlundraEntityScriptProxy.PosY"/>/
    /// <see cref="AlundraEntityScriptProxy.PosZ"/>, every frame, for every spawned entity - the original
    /// recomputes screen position from the logical position every frame (there is no cached "world
    /// transform" struct in the PSX engine, the renderer projects PosX/PosY/PosZ straight from the entity
    /// struct each frame), it never trusts a stale, spawn-time-only placement. This supersedes
    /// <see cref="CreateEntityFromPrefab"/>'s own spawn-time-only <c>ResolveLogicalPosition</c> call (still
    /// needed there so a freshly spawned, not-yet-<see cref="Update"/>-ed entity has a sane initial
    /// transform for its very first draw) - see that method's own doc, and
    /// <c>WallPlacementOverlay.ApplyEntitySortKey</c>'s deviation note, now resolved by this pass.
    /// Required for the search-driven position opcodes (0x64/0x65) to have any visible effect: without
    /// this, PosX/PosY/PosZ change but nothing ever reads them again. Field write only, no allocation - a
    /// bare-fallback spawn (<see cref="CreateBareEntityFromRecord"/>) has no <c>RootComponent</c> and is
    /// skipped, same as a destroyed entity (see <see cref="RunAnimationSyncPass"/>'s own doc on the
    /// FlagToDestroy check).
    /// </summary>
    internal static void RunTransformSyncPass(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            SyncTransform(entity);
        }
    }

    /// <summary>Per-entity half of <see cref="RunTransformSyncPass"/> - see that method's own doc, and
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s doc for why this is now called per-entity, once per
    /// frame, rather than looped from this world's own <see cref="Update"/> (decision D2).
    /// E3.a (docs/plan-e3-collisions.md): after writing the LOGICAL pose onto the root, also re-runs
    /// <see cref="RenderProjectionComponent.UpdateProjection"/> on the entity's cached
    /// <see cref="AlundraEntityScriptProxy.RenderProjection"/> (resolved once at spawn/adoption, not
    /// looked up here) so the <c>AnimatedSpriteComponent</c> renders the projected pose of THIS frame,
    /// not the previous one: component <c>Update</c> (hence a natural, non-forced projection) runs
    /// BEFORE <c>GameplayProxy.Update</c> in <c>Entity.Update</c> (Entity.cs:473-504), and this method is
    /// itself called from <see cref="AlundraEntityScriptProxy.Update"/>, i.e. from inside that same
    /// GameplayProxy.Update - without the explicit call here the sprite would lag the logical pose by
    /// exactly one frame.</summary>
    internal static void SyncTransform(Entity entity)
    {
        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy || proxy.Status == EntityStatus.FlagToDestroy)
        {
            return;
        }

        if (entity.RootComponent != null)
        {
            entity.RootComponent.LocalTransform.Position = ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
            proxy.RenderProjection?.UpdateProjection();
        }
    }

    /// <summary>
    /// Per-frame half of the wall/sprite depth interleave (see <see cref="WallPlacementOverlay"/>'s class
    /// doc): aligns every spawned entity's <see cref="DepthSortable2DComponent.Elevation"/> with its
    /// current logical <see cref="AlundraEntityScriptProxy.PosY"/> plus its current (anim, direction)'s
    /// IDSV bias, looked up from <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/> (already
    /// resolved at spawn - no per-frame catalog dictionary lookup here) - field writes/one small-dictionary
    /// lookup only, the overlay tiles themselves are built once in <see cref="InitializeWithWorld"/> and
    /// never touched again. An entity without a <see cref="DepthSortable2DComponent"/> (the bare-fallback
    /// spawn path, <see cref="CreateBareEntityFromRecord"/>) is skipped - it carries no sprite to sort in
    /// the first place. A <see cref="EntityStatus.FlagToDestroy"/> entity is skipped too, same as
    /// <see cref="RunAnimationSyncPass"/> and <see cref="RunTransformSyncPass"/>.
    /// </summary>
    internal static void RunWallInterleaveSortKeyPass(IReadOnlyList<Entity> entities)
    {
        // Indexed for, not foreach - see RunPendingEventTriggers's own doc on why an IReadOnlyList<T>
        // foreach's boxed enumerator is no longer free to ignore on a per-frame pass.
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy || proxy.Status == EntityStatus.FlagToDestroy)
            {
                continue;
            }

            var depthSortable = entity.GetComponent<DepthSortable2DComponent>();
            if (depthSortable == null)
            {
                continue;
            }

            var idsv = 0;
            var idsvKey = (int)proxy.CurrentAnimationId * IdsvDirectionStride + proxy.AnimationDirection;
            proxy.IdsvByAnimDirection?.TryGetValue(idsvKey, out idsv);

            WallPlacementOverlay.ApplyEntitySortKey(depthSortable, proxy.PosY, idsv);
        }
    }

    /// <summary>
    /// Port of the target-resolution part of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4
    /// (EntityManager.cs:209-224 only): resolves <see cref="AlundraEntityScriptProxy.AnimationDirection"/>
    /// from the entity's current facing and its <see cref="AlundraEntityScriptProxy.TargetDirection"/> via
    /// <see cref="AnimationTables.AnimationDirectionTable"/>, and returns true (with the new
    /// <c>CurrentAnimationId</c>/<c>AnimationDirection</c> pair) exactly when the original would have
    /// entered its "animation or direction changed" branch. Pure and static so it can be unit tested
    /// without a <see cref="World"/> or a component.
    /// </summary>
    internal static bool TryResolveAnimationTarget(
        AlundraEntityScriptProxy proxy, out uint newCurrentAnimationId, out int newAnimationDirection)
    {
        var row = proxy.AnimationDirection;
        var col = (int)(((proxy.TargetDirection + 2) & 0x1c) >> 2);
        var animationDirectionFromTargetDirection = AnimationTables.AnimationDirectionTable[row * 8 + col];

        if (proxy.CurrentAnimationId != proxy.TargetAnimationId || proxy.AnimationDirection != animationDirectionFromTargetDirection)
        {
            newCurrentAnimationId = proxy.TargetAnimationId;
            newAnimationDirection = animationDirectionFromTargetDirection;
            return true;
        }

        newCurrentAnimationId = proxy.CurrentAnimationId;
        newAnimationDirection = proxy.AnimationDirection;
        return false;
    }

    /// <summary>
    /// Finds, among <paramref name="animatedSprite"/>'s own loaded animations, the one whose name ends
    /// with "_anim{animationId}_{directionName}" - the converter's own naming scheme
    /// (<c>AlundraCasaEngineProjectConverter.Writers.SpriteWriter</c>: <c>$"bank{bank.BankKey}_anim{animSetIndex}_{DirectionNames[directionIndex]}"</c>).
    /// Matches by suffix rather than the component's own exact-name <c>SetCurrentAnimation(string,bool)</c>
    /// because this proxy does not carry the bank key prefix - only the (animationId, direction) pair the
    /// original engine itself tracked.
    /// </summary>
    internal static bool TrySelectAnimationByNameSuffix(
        AnimatedSpriteComponent animatedSprite, uint animationId, int animationDirection, out Animation2d? selected)
    {
        if (animationDirection < 0 || animationDirection >= AnimationTables.DirectionNames.Length)
        {
            selected = null;
            return false;
        }

        var suffix = "_anim" + animationId.ToString(CultureInfo.InvariantCulture) + "_" + AnimationTables.DirectionNames[animationDirection];

        foreach (var animation in animatedSprite.Animations)
        {
            if (animation.Animation2dData.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                selected = animation;
                return true;
            }
        }

        selected = null;
        return false;
    }

    /// <summary>
    /// V1 minimal port of <c>GameEngine.DestroyEntity(Entity, int)</c> @ 0x8003A59C: marks the entity for
    /// destruction (naturally skipped by the pick phase from now on) and logs once at debug level with
    /// the original's numeric effect-id argument (-1 = "use the sprite record's break effect", 6 = the
    /// sliding-slope break effect, see the pick-phase callers above). Does not remove the entity from the
    /// CasaEngine world yet (slot recycling, contents spawning and the original's other side effects -
    /// ActiveEffect/PlatformEntity cleanup, SpawnEntityContents - are later work).
    /// </summary>
    internal void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (effectId={effectId}).");
    }

    /// <summary>
    /// V1 port of the single-argument <c>GameEngine.DestroyEntity(Entity)</c> @ 0x8003A774 - the overload
    /// every search-driven destroy opcode (0x2E Script_46_02E) calls once per match, distinct from the
    /// two-argument overload above (which the pick-phase status machine uses, and which also spawns
    /// break-effect contents). Same V1 scope note as the two-argument overload: does not remove the
    /// entity from the CasaEngine world (slot recycling is later work - see that overload's own doc) and
    /// does not port <c>ActiveEffect</c>/<c>PlatformEntity.CarriedEntity</c> cleanup. Clears
    /// <see cref="AlundraEntityScriptProxy.EventTrigger"/> like the original, so a same-frame re-scan
    /// (<see cref="RunPendingEventTriggers"/>) does not also try to run whatever program slot this entity
    /// had queued before being destroyed.
    /// </summary>
    internal void DestroyEntity(AlundraEntityScriptProxy entity)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        entity.EventTrigger = ScriptHelper.ProgramUnknown;
        Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (search-destroyed).");
    }

    /// <summary>
    /// Backs opcode 0x2D (Script_45_02D) via <see cref="IEntityWorldContext"/>. Faithful port of
    /// <c>GameEngine.SpawnEntity(parent, entityId, notCheckSpawnZone)</c> (GameEngine.cs:679-758)
    /// restricted to <c>notCheckSpawnZone = 1</c>, the only value the opcode ever passes - so only the
    /// <c>IsEnabled</c> gate applies (see <see cref="ShouldSpawnRecord(TileMapObjectData,bool,out string)"/>'s
    /// own doc); the record lookup (<c>GameEngine.GetEntityRecord</c>) is <see cref="_entityRecordsByIndex"/>.
    /// Shares the exact same build path as the map-load spawn loop in <see cref="InitializeWithWorld"/>
    /// (<see cref="CreateEntityFromRecord"/> -&gt; <see cref="ApplyRecord"/> -&gt;
    /// <see cref="ApplySpawnInitialization"/>), with <paramref name="logicEntity"/>'s own backing entity
    /// passed down as the new entity's <c>ParentEntity</c> - exactly like <c>EntityManager.InitializeEntity</c>
    /// does for its <c>parentEntity</c> argument. The spawned entity joins <see cref="_spawnedEntities"/>
    /// immediately, so it is visible to any further search this same script call issues, and is picked up
    /// by <see cref="Update"/>'s per-frame passes starting next frame (it enters as
    /// <see cref="EntityStatus.Loaded"/>, so its own Load program runs then, same as every map-load spawn).
    /// Unlike the original (a single global, size-limited entity-slot table with recycling), this always
    /// allocates a brand new CasaEngine <see cref="Entity"/> - no slot reuse in V1.
    /// </summary>
    internal AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
    {
        if (_world == null)
        {
            Logs.WriteWarning("AlundraWorldProxy: SpawnEntityByRecordId called before InitializeWithWorld; ignored.");
            return null;
        }

        if (!_entityRecordsByIndex.TryGetValue(entityRecordId, out var record))
        {
            Logs.WriteDebug(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) - no such entity record "
                + "(GameEngine.GetEntityRecord would return null); spawn skipped.");
            return null;
        }

        if (!ShouldSpawnRecord(record, notCheckSpawnZone: true, out var skipReason))
        {
            Logs.WriteDebug(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) - record '{record.Name}' "
                + $"not spawned ({skipReason}).");
            return null;
        }

        try
        {
            var entity = CreateEntityFromRecord(
                record, guid => _world.Game.AssetContentManager.Load<Entity>(guid), SpriteRecordCatalog,
                parentEntity: logicEntity.LogicContextEntity);
            if (entity.GameplayProxy is AlundraEntityScriptProxy spawnedProxy)
            {
                spawnedProxy.ScriptHost = this;
            }

            _world.AddEntity(entity);
            _spawnedEntities.Add(entity);
            return entity.GameplayProxy as AlundraEntityScriptProxy;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) failed to spawn; skipping. "
                + $"{ex.Message}");
            return null;
        }
    }

    // IEntityWorldContext - see that interface's own doc. Explicit implementation keeps these reachable
    // through the interpreter seam without adding public surface to this proxy.
    IReadOnlyList<AlundraEntityScriptProxy> IEntityWorldContext.SpawnedEntities => GetSpawnedEntityProxies();

    AlundraEntityScriptProxy? IEntityWorldContext.PlayerEntity => PlayerEntity;

    AlundraEntityScriptProxy? IEntityWorldContext.SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
        => SpawnEntityByRecordId(logicEntity, entityRecordId);

    void IEntityWorldContext.DestroyEntity(AlundraEntityScriptProxy entity) => DestroyEntity(entity);

    // IAlundraScriptHost - see that interface's own doc. Explicit implementation, same reasoning as
    // IEntityWorldContext above.
    IEventProgramRunner IAlundraScriptHost.Runner => EventProgramRunner;

    AlundraEntityScriptProxy? IAlundraScriptHost.ActiveCollisionEntity => ActiveCollisionEntity;

    void IAlundraScriptHost.DestroyEntity(AlundraEntityScriptProxy entity, int effectId) => DestroyEntity(entity, effectId);

    AlundraGameState IAlundraScriptHost.GameState => GameState;

    AlundraPlayerController? IAlundraScriptHost.PlayerController => _playerController;

    /// <summary>
    /// Snapshot of <see cref="_spawnedEntities"/>'s own <see cref="AlundraEntityScriptProxy"/> proxies, in
    /// the same creation order - built fresh on every call (not cached) so an entity dynamically spawned
    /// by 0x2D earlier in the same script call is visible to a search issued later in that call, exactly
    /// like the original's live <c>g_entitySlots</c> array (see <see cref="EntitySearchService"/>'s class
    /// doc). Not a per-frame hot path (only entity-manipulation opcodes call it), so the allocation here
    /// is fine - contrast with <see cref="_updateProxies"/>, the actual per-frame working list.
    /// </summary>
    private List<AlundraEntityScriptProxy> GetSpawnedEntityProxies()
    {
        var proxies = new List<AlundraEntityScriptProxy>(_spawnedEntities.Count);

        foreach (var entity in _spawnedEntities)
        {
            if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
            {
                proxies.Add(proxy);
            }
        }

        return proxies;
    }

    public override void Draw()
    {
        //Nothing to do at world level yet.
    }

    public override void OnHit(Collision collision)
    {
        //The world proxy does not participate in collisions.
    }

    public override void OnHitEnded(Collision collision)
    {
        //The world proxy does not participate in collisions.
    }

    public override void OnBeginPlay(World world)
    {
        //Entity creation happens in InitializeWithWorld so the engine integrates the entities
        //(InternalAddEntities) before BeginPlay is dispatched to them.
    }

    public override void OnEndPlay(World world)
    {
        //Nothing to tear down at world level yet.
    }

    public override IGameplayProxy Clone()
    {
        //Still returns a fresh instance: the spawned-entity list is runtime state rebuilt by
        //InitializeWithWorld (each world instance spawns and owns its own entities), not something a
        //clone should share or carry over.
        return new AlundraWorldProxy();
    }
}

/// <summary>
/// One <c>MapEvent</c> slot (GameEngine.cs's <c>MapEvent</c> struct) - see
/// <see cref="AlundraWorldProxy.BuildMapEvents"/>/<see cref="AlundraWorldProxy.RunMapEventsPass"/> for how
/// this is built and driven. A plain mutable class (not a struct): <see cref="RunMapEventsPass"/> mutates
/// <see cref="Entity"/>/<see cref="ProgramBMap"/> in place across frames, exactly like the original's own
/// persistent array slot.
/// </summary>
internal sealed class AlundraMapEvent
{
    public int Id;
    public int X1, Y1, X2, Y2;
    public int ProgramBMap;

    /// <summary>The map-event's own current "logic entity" (initially the player - see
    /// <see cref="AlundraWorldProxy.BuildMapEvents"/>; only opcode 0x66, not ported, can ever retarget
    /// it).</summary>
    public AlundraEntityScriptProxy? Entity;

    public readonly EventProgramState EventData = new();
}
