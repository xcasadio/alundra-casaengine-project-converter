#nullable enable
using System;
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
/// <see cref="ResolveWorldPosition"/> - see <see cref="CreateEntityFromPrefab"/>. No status-machine or
/// event-program execution yet - that lands in follow-up work, which will need to re-derive this
/// transform every time the logical position changes.
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

    public override void Update(float elapsedTime)
    {
        //Nothing to do at world level yet.
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
        //No state on this proxy yet (V1 only spawns entities on load); revisit once it gains any.
        return new AlundraWorldProxy();
    }
}
