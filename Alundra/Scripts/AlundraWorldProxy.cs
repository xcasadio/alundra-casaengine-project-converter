#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
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
/// this proxy then converts into the spawned entity's <c>RootComponent.LocalTransform.Position</c> via
/// <see cref="ResolveWorldPosition"/> - see <see cref="CreateEntityFromPrefab"/>.
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
public class AlundraWorldProxy : GameplayProxy
{
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

        var entitiesLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == EntitiesLayerName);
        var portalsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == PortalsLayerName);
        var mapEventsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == MapEventsLayerName);

        Logs.WriteInfo(
            $"AlundraWorldProxy: world '{world.Name}' object layers - "
            + $"{EntitiesLayerName}={entitiesLayer?.Objects.Count ?? 0}, "
            + $"{PortalsLayerName}={portalsLayer?.Objects.Count ?? 0}, "
            + $"{MapEventsLayerName}={mapEventsLayer?.Objects.Count ?? 0}.");

        if (entitiesLayer == null)
        {
            return;
        }

        var skippedCount = 0;

        foreach (var record in entitiesLayer.Objects)
        {
            if (!ShouldSpawnRecord(record, out var skipReason))
            {
                skippedCount++;
                Logs.WriteDebug($"AlundraWorldProxy: record '{record.Name}' not spawned ({skipReason}).");
                continue;
            }

            try
            {
                var entity = CreateEntityFromRecord(
                    record, guid => world.Game.AssetContentManager.Load<Entity>(guid), SpriteRecordCatalog);
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
    {
        if (TryGetRecordInt(record, "IsEnabled", out var isEnabled) && isEnabled == 0)
        {
            skipReason = "IsEnabled=0";
            return false;
        }

        if (TryGetRecordInt(record, "SpriteDirection", out var spriteDirection) && (spriteDirection & 0x40) == 0)
        {
            skipReason = $"SpriteDirection={spriteDirection} has bit 0x40 clear";
            return false;
        }

        skipReason = string.Empty;
        return true;
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
        TileMapObjectData record, Func<Guid, Entity?>? prefabLoader, ISpriteRecordCatalog? spriteRecordCatalog = null)
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
                return CreateEntityFromPrefab(record, prefab, spriteRecordCatalog);
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

        return CreateBareEntityFromRecord(record, spriteRecordCatalog);
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
        TileMapObjectData record, Entity prefab, ISpriteRecordCatalog? spriteRecordCatalog = null)
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
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog);

            // The prefab's root is the bank's AnimatedSpriteComponent (EntityBankPrefabWriter); place it
            // in the CasaEngine world frame from the logical position EntityRecordMapper/ApplySpawnInitialization
            // just filled (PosZ already carries the -ModZ+1 header adjustment when a header was found).
            // Defensive null-check only: a bank prefab is expected to always carry a root component.
            if (entity.RootComponent != null)
            {
                entity.RootComponent.LocalTransform.Position = ResolveWorldPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
            }
        }

        return entity;
    }

    /// <summary>
    /// Converts an entity's logical spawn position (<see cref="AlundraEntityScriptProxy.PosX"/> /
    /// <see cref="AlundraEntityScriptProxy.PosY"/> / <see cref="AlundraEntityScriptProxy.PosZ"/>, 16.16
    /// fixed-point Alundra pixels - see <see cref="EntityRecordMapper"/>) into a CasaEngine world
    /// position, consistently with <c>WorldWriter.ResolveTileCentreSpawn</c> (the converter's own
    /// tile-to-world conversion, used for the PlayerStart) and
    /// docs/guidelines-runtime-alundra-casaengine.md section 2.3:
    /// <list type="bullet">
    /// <item><description><c>X = pixelX</c> (no conversion - CasaEngine's X already matches Alundra's).</description></item>
    /// <item><description><c>Y = -pixelY + elevationPixels</c>: Alundra's Y points down, CasaEngine's Y
    /// points up, hence the negation; and Alundra's Z is not a camera depth, it is an elevation that
    /// shifts the sprite up the screen (<c>elevationPixels = pixelZ</c>, i.e. <c>PosZ &gt;&gt; 16</c>) -
    /// it is folded into this projected Y rather than left in Z, because
    /// <see cref="CasaEngine.Framework.Scene.Entities.Components.DepthSortable2DComponent"/>'s default
    /// <c>TopDownYUp</c> sort mode (and <see cref="CasaEngine.Framework.Scene.Entities.Components.AnimatedSpriteComponent.DrawComposedAnimation"/>,
    /// which draws at <c>Position.X</c>/<c>Position.Y</c> verbatim) only read world X/Y - there is no
    /// orthographic-camera projection step in this 2D pipeline that would turn a raw Z into a screen
    /// offset the way the original PSX renderer did.</description></item>
    /// <item><description><c>Z = 0</c>: left unused here, exactly like <c>WorldWriter</c> - in this
    /// engine Z only orders render layers (<c>DepthSortable2DComponent.SortingLayer</c>/<c>Elevation</c>),
    /// it does not carry Alundra's elevation.</description></item>
    /// </list>
    /// This is a spawn-time snapshot of a logical position that can change at runtime (movement, event
    /// programs); a future status-machine task must call this again whenever <c>PosX</c>/<c>PosY</c>/
    /// <c>PosZ</c> changes to keep the transform in sync - the logical fields are authoritative, this
    /// transform is derived.
    /// </summary>
    internal static Vector3 ResolveWorldPosition(int posX, int posY, int posZ)
    {
        var pixelX = posX >> 16;
        var pixelY = posY >> 16;
        var elevationPixels = posZ >> 16;

        return new Vector3(pixelX, -pixelY + elevationPixels, 0f);
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
    internal static Entity CreateBareEntityFromRecord(TileMapObjectData record, ISpriteRecordCatalog? spriteRecordCatalog = null)
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
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog);
        }

        return entity;
    }

    /// <summary>Maps <paramref name="record"/> onto <paramref name="proxy"/> and marks it loaded.</summary>
    internal static void ApplyRecord(TileMapObjectData record, AlundraEntityScriptProxy proxy)
    {
        EntityRecordMapper.Map(record, proxy);
        proxy.Status = EntityStatus.Loaded;
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
        TileMapObjectData record, Entity entity, AlundraEntityScriptProxy proxy, ISpriteRecordCatalog? spriteRecordCatalog)
    {
        proxy.LogicContextEntity = entity;

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
    /// Drives the status machine of every entity this proxy spawned, in creation order. Port of
    /// <c>EntityManager.UpdateEntitiesEvents</c> @ 0x800386D0 - see <see cref="RunEntityEventsPass"/> for
    /// the actual (headless-testable) two-phase pass.
    /// </summary>
    public override void Update(float elapsedTime)
    {
        // DEBUG ONLY - runs unconditionally (unlike the entity passes below, which are skipped when
        // nothing was spawned) so the map can still be flown over even for a world with no entities.
        UpdateDebugCameraPan(elapsedTime);

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

        RunEntityEventsPass(_updateProxies, EventProgramRunner, ActiveCollisionEntity, DestroyEntity);

        // Mirrors the original ordering: EntityManager.UpdateEntitiesEvents runs before
        // EntityManager.UpdateEntitiesAnimation in UpdateEntities' own pass list - see RunAnimationSyncPass.
        RunAnimationSyncPass(_spawnedEntities);
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
    /// Axis mapping: MonoGame's right-stick Y is positive up; these converted maps' world Y is also "more
    /// positive = further up/north" (<see cref="ResolveWorldPosition"/> negates Alundra's down-positive Y),
    /// so stick-up must increase <c>Target.Y</c> - no sign flip needed, unlike Alundra's own Y. Stick X
    /// maps directly onto world X the same way. <c>Target.Z</c> is left untouched.
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
    /// Faithful port of the two-phase entity event pass of <c>EntityManager.UpdateEntitiesEvents</c> @
    /// 0x800386D0, factored out of <see cref="Update"/> so it can run headless over a plain list of
    /// proxies in tests, without a live <see cref="World"/>.
    ///
    /// Out of V1 scope (documented, not ported): <c>MovePlayer()</c> at the head of the original pass
    /// (no player system yet), the <c>g_playerControlFlags</c> gate, and the sibling passes
    /// (UpdateDestroyedEntities/Counters/Lists/Animation/Physics) - this only ports the event pass
    /// itself. The original loop also starts at slot index 1 (slot 0 is an unused sentinel); every
    /// element of <paramref name="entities"/> here is already a real spawned entity, so this port
    /// iterates all of them.
    /// </summary>
    internal static void RunEntityEventsPass(
        IReadOnlyList<AlundraEntityScriptProxy> entities,
        IEventProgramRunner runner,
        AlundraEntityScriptProxy? activeCollisionEntity,
        Action<AlundraEntityScriptProxy, int> destroyEntity)
    {
        // Phase 1 (pick): decide which event program slot each entity should run this frame, and apply
        // the status transitions the original interleaves into that same pick.
        foreach (var entity in entities)
        {
            var eventProgramType = ScriptHelper.ProgramUnknown;

            if (entity.BlockedByEntity == null)
            {
                switch (entity.Status)
                {
                    case EntityStatus.Destroyed:
                    case EntityStatus.FlagToDestroy:
                        eventProgramType = ScriptHelper.ProgramUnknown;
                        break;

                    case EntityStatus.Loaded:
                        eventProgramType = ScriptHelper.ProgramALoad;
                        entity.Status = EntityStatus.Normal;
                        Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] Loaded -> Normal (slot A).");
                        break;

                    case EntityStatus.Normal:
                    {
                        var flags = entity.Flags;

                        if ((flags & EntityFlags.DestroyOnSlidingSlope) != 0 && entity.Slope_18c == 4)
                        {
                            destroyEntity(entity, 6);
                            eventProgramType = ScriptHelper.ProgramUnknown;
                        }
                        else if ((flags & EntityFlags.DestroyOnVramFlags) != 0 && (entity.CombinedVramFlagsOR & 0x8004U) != 0)
                        {
                            destroyEntity(entity, -1);
                            eventProgramType = ScriptHelper.ProgramUnknown;
                        }
                        else if (((flags & EntityFlags.DeactivateOnImpact) != 0 && (entity.ForceAdjusted != 0 || entity.IsOnGround != 0))
                                 || ((flags & EntityFlags.DeactivateOnHit) != 0 && entity.HitCounter != 0)
                                 || ((flags & EntityFlags.DeactivateOnAnimationEnd) != 0 && entity.ForceResetAnimationFlag != 0))
                        {
                            entity.Status = EntityStatus.Deactivated;
                            eventProgramType = ScriptHelper.ProgramEDeactivate;
                            Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] Normal -> Deactivated (slot E).");
                        }
                        else
                        {
                            eventProgramType = ScriptHelper.ProgramDTouch;

                            if (entity.TouchingEntity == null)
                            {
                                eventProgramType = ScriptHelper.ProgramCTick;

                                if (ReferenceEquals(activeCollisionEntity, entity)
                                    && (entity.ProgramIndexes[5] != 0 || entity.SpriteProgramIndexes[5] != 0))
                                {
                                    eventProgramType = ScriptHelper.ProgramFInteract;
                                }
                            }
                        }

                        break;
                    }

                    case EntityStatus.Deactivated:
                        eventProgramType = ScriptHelper.ProgramEDeactivate;
                        break;
                }
            }

            entity.EventTrigger = eventProgramType;
        }

        // Phase 2 (run): the do/while re-scan loop of the original - a runner that sets another
        // entity's EventTrigger while running gets that entity run within the same call, until a clean
        // pass finds nothing left to do.
        bool keepGoing;

        do
        {
            keepGoing = false;

            foreach (var entity in entities)
            {
                if (entity.EventTrigger == ScriptHelper.ProgramUnknown)
                {
                    continue;
                }

                var programIndex = entity.ProgramIndexes[entity.EventTrigger] & 0x7f;

                if (programIndex == 0)
                {
                    // g_entityEventFunctionsByType => AI
                    runner.RunSpriteEvent(entity);
                }
                else
                {
                    runner.RunScript(entity, entity.EventTrigger);
                }

                entity.EventTrigger = -1;
                keepGoing = true;
            }
        } while (keepGoing);
    }

    /// <summary>
    /// Drives, once per frame, the target-resolution part of <c>EntityManager.UpdateAnimation</c> @
    /// 0x80038AB4 (EntityManager.cs:209-224 only - see <see cref="TryResolveAnimationTarget"/>) for every
    /// entity this proxy spawned, then bridges a resolved change onto the spawned entity's own
    /// <see cref="AnimatedSpriteComponent"/> (see <see cref="TrySelectAnimationByNameSuffix"/>).
    ///
    /// Runs from <see cref="Update"/> deliberately, not from spawn: at spawn time
    /// (<see cref="ApplySpawnInitialization"/>, mirroring <c>EntityManager.InitializeEntity</c> setting
    /// <c>CurrentAnimationId = ~animationId</c>) the entity has just been queued with <c>World.AddEntity</c>
    /// and not yet integrated - its <see cref="AnimatedSpriteComponent.Animations"/> list is only
    /// populated later, when <c>World.InternalAddEntities</c> calls the component's own
    /// <c>InitializeWithWorld</c> (see <see cref="World.LoadContent"/>: that runs strictly after
    /// <c>GameplayProxy.InitializeWithWorld</c>, i.e. after this whole proxy's spawn loop returns). By the
    /// time <see cref="Update"/> first runs the list is populated, and every freshly spawned entity has
    /// <c>CurrentAnimationId = ~TargetAnimationId</c> (guaranteed different, since <c>TargetAnimationId</c>
    /// is never 0xFFFFFFFF-complemented of itself), so the very first sync always fires and sets the
    /// entity's initial visual - the visibility payoff this port exists for.
    ///
    /// Frame-level animation state (<c>Frame</c>/<c>NextFrameDelay</c>/<c>AnimCompleteCounter</c>, the rest
    /// of <c>UpdateAnimation</c>) stays out of scope: CasaEngine's own <c>Animation2dCompositionSampler</c>
    /// (driven by <see cref="AnimatedSpriteComponent.Update"/>) already owns frame timing once the right
    /// animation is selected.
    /// </summary>
    internal static void RunAnimationSyncPass(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
            {
                continue;
            }

            if (!TryResolveAnimationTarget(proxy, out var newCurrentAnimationId, out var newAnimationDirection))
            {
                continue;
            }

            proxy.CurrentAnimationId = newCurrentAnimationId;
            proxy.AnimationDirection = newAnimationDirection;

            var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
            if (animatedSprite == null)
            {
                continue;
            }

            if (TrySelectAnimationByNameSuffix(animatedSprite, proxy.CurrentAnimationId, proxy.AnimationDirection, out var selected))
            {
                animatedSprite.SetCurrentAnimation(selected, forceReset: true);
            }
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
