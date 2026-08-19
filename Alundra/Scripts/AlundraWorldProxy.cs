#nullable enable
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;

namespace Alundra.Scripts;

/// <summary>
/// World script every converted .world declares as its "script_class_name" (see
/// <c>WorldWriter.WorldScriptClassName</c>). V1 scope: on world load, read the map's tilemap
/// "Entities" object layer (see <c>AlundraDataExtractor.TiledMapExporter</c>) and spawn one bare
/// game entity per record, carrying an <see cref="AlundraEntityScriptProxy"/> filled by
/// <see cref="EntityRecordMapper"/>. No sprite/collision components, no coordinate conversion, no
/// status-machine/event-program execution - that all lands in follow-up work.
/// </summary>
public class AlundraWorldProxy : GameplayProxy
{
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
            var entity = CreateEntityFromRecord(record);
            world.AddEntity(entity);
        }
    }

    /// <summary>
    /// Builds one bare game entity from an "Entities" object-layer record: a deterministically named
    /// entity carrying an <see cref="AlundraEntityScriptProxy"/> filled by <see cref="EntityRecordMapper"/>,
    /// with <see cref="EntityStatus.Loaded"/>. Factored out of <see cref="InitializeWithWorld"/> so it is
    /// reusable by a future dynamic-spawn path. Does not add the entity to any world; the caller does that.
    ///
    /// Split into <see cref="BuildEntityName"/> and <see cref="ApplyRecord"/>, both unit-testable in
    /// isolation, plus the entity/proxy wiring below (<c>entity.Initialize()</c>, which resolves
    /// <see cref="Entity.GameplayProxyClassName"/> through <c>ElementFactory</c>) which is not: that
    /// call walks every loaded assembly's types and needs the real game host's MonoGame assemblies to
    /// be present, which the headless unit test process does not have on its runtime probing path
    /// (Alundra.csproj marks its own MonoGame.Framework.DesktopGL reference PrivateAssets="All" for
    /// game-folder deployment, so it never flows into Alundra.Tests's deps.json). It is exercised by a
    /// live engine World only.
    /// </summary>
    internal static Entity CreateEntityFromRecord(TileMapObjectData record)
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
