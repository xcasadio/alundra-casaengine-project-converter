#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Core.Logging;
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
    /// Entities spawned by this proxy in <see cref="InitializeWithWorld"/> (both the prefab-clone and
    /// bare-fallback paths), in creation order - <see cref="Update"/> drives their status machine in
    /// this same order, mirroring the original manager's single flat entity-slot array.
    /// </summary>
    private readonly List<Entity> _spawnedEntities = new();

    /// <summary>
    /// Seam over actual event-program execution (see <see cref="IEventProgramRunner"/>); defaults to a
    /// silent no-op since the bytecode interpreter does not exist yet. Internal, not injected through
    /// the constructor: <c>ElementFactory</c> constructs gameplay proxies parameterless, so tests swap
    /// this field directly instead.
    /// </summary>
    internal IEventProgramRunner EventProgramRunner = new NoOpEventProgramRunner();

    /// <summary>
    /// Port of the original global <c>g_activeCollisionEntity</c>: the entity currently involved in the
    /// active collision pair, used by the pick phase to decide whether a touch downgrades all the way
    /// to an interact (slot F). Null in V1 (no collision system driving it yet); settable internally for
    /// tests.
    /// </summary>
    internal AlundraEntityScriptProxy? ActiveCollisionEntity;

    public override void InitializeWithWorld(World world)
    {
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

        foreach (var record in entitiesLayer.Objects)
        {
            try
            {
                var entity = CreateEntityFromRecord(record, guid => world.Game.AssetContentManager.Load<Entity>(guid));
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
    internal static Entity CreateEntityFromRecord(TileMapObjectData record, Func<Guid, Entity?>? prefabLoader)
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
                return CreateEntityFromPrefab(record, prefab);
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

        return CreateBareEntityFromRecord(record);
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
    internal static Entity CreateEntityFromPrefab(TileMapObjectData record, Entity prefab)
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

            // The prefab's root is the bank's AnimatedSpriteComponent (EntityBankPrefabWriter); place it
            // in the CasaEngine world frame from the logical position EntityRecordMapper just filled.
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
    internal static Entity CreateBareEntityFromRecord(TileMapObjectData record)
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
        }

        return entity;
    }

    /// <summary>Maps <paramref name="record"/> onto <paramref name="proxy"/> and marks it loaded.</summary>
    internal static void ApplyRecord(TileMapObjectData record, AlundraEntityScriptProxy proxy)
    {
        EntityRecordMapper.Map(record, proxy);
        proxy.Status = EntityStatus.Loaded;
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
        if (_spawnedEntities.Count == 0)
        {
            return;
        }

        var proxies = new List<AlundraEntityScriptProxy>(_spawnedEntities.Count);
        foreach (var entity in _spawnedEntities)
        {
            if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
            {
                proxies.Add(proxy);
            }
        }

        RunEntityEventsPass(proxies, EventProgramRunner, ActiveCollisionEntity, DestroyEntity);
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
