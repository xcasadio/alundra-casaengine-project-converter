using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the structural pieces the entity search/manipulation opcodes need on
/// <see cref="AlundraWorldProxy"/>: the <c>notCheckSpawnZone</c> overload of
/// <see cref="AlundraWorldProxy.ShouldSpawnRecord(TileMapObjectData,bool,out string)"/>, the single-argument
/// <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy)"/> overload, the
/// <c>ParentEntity</c> threading through the shared spawn path, transform re-derivation
/// (<see cref="AlundraWorldProxy.RunTransformSyncPass"/>) and destroyed-entity visibility/skip in the
/// per-frame passes. <see cref="AlundraWorldProxy.SpawnEntityByRecordId"/>'s live-<c>World</c> path is
/// intentionally not exercised here for the same reason <see cref="AlundraWorldProxyTests"/> does not
/// cover <see cref="AlundraWorldProxy.InitializeWithWorld"/>'s live prefab loader - it needs a running
/// <c>CasaEngineGame</c>; its build path is the exact same <see cref="AlundraWorldProxy.CreateEntityFromRecord"/>
/// covered directly below and by <see cref="AlundraWorldProxyTests"/>.
/// </summary>
public class AlundraWorldProxyEntityManipulationTests
{
    // -----------------------------------------------------------------------------------------
    // ShouldSpawnRecord(record, notCheckSpawnZone, out reason)
    // -----------------------------------------------------------------------------------------

    private static TileMapObjectData NewRecord(int? isEnabled = null, int? spriteDirection = null)
    {
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        if (isEnabled.HasValue)
        {
            record.CustomProperties["IsEnabled"] = isEnabled.Value.ToString();
        }

        if (spriteDirection.HasValue)
        {
            record.CustomProperties["SpriteDirection"] = spriteDirection.Value.ToString();
        }

        return record;
    }

    [Fact]
    public void ShouldSpawnRecord_NotCheckSpawnZoneTrue_IgnoresSpriteDirectionGate()
    {
        // SpriteDirection=0 has bit 0x40 clear - would be rejected with notCheckSpawnZone=false.
        var record = NewRecord(isEnabled: 1, spriteDirection: 0);

        Assert.False(AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone: false, out _));
        Assert.True(AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone: true, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void ShouldSpawnRecord_NotCheckSpawnZoneTrue_StillRejectsDisabledRecord()
    {
        var record = NewRecord(isEnabled: 0, spriteDirection: 0);

        Assert.False(AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone: true, out var reason));
        Assert.Equal("IsEnabled=0", reason);
    }

    [Fact]
    public void ShouldSpawnRecord_TwoArgOverload_MatchesNotCheckSpawnZoneFalse()
    {
        var record = NewRecord(isEnabled: 1, spriteDirection: 0);

        Assert.False(AlundraWorldProxy.ShouldSpawnRecord(record, out var reason));
        Assert.Equal("SpriteDirection=0 has bit 0x40 clear", reason);
    }

    // -----------------------------------------------------------------------------------------
    // ParentEntity threading (CreateEntityFromRecord -> ApplySpawnInitialization) - the same path
    // SpawnEntityByRecordId (0x2D) uses, with the calling script's own backing entity as parent.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CreateEntityFromRecord_ParentEntity_IsThreadedOntoTheProxy()
    {
        var record = NewRecord(isEnabled: 1, spriteDirection: 0x40);
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "0";
        record.CustomProperties["YPos"] = "0";
        record.CustomProperties["Height"] = "0";
        var parent = new Entity { Name = "caller" };

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, prefabLoader: null, parentEntity: parent);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Same(parent, proxy.ParentEntity);
    }

    [Fact]
    public void CreateEntityFromRecord_NoParentEntity_LeavesParentEntityNull()
    {
        var record = NewRecord(isEnabled: 1, spriteDirection: 0x40);
        record.CustomProperties["Index"] = "0";

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, prefabLoader: null);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Null(proxy.ParentEntity);
    }

    // -----------------------------------------------------------------------------------------
    // SpawnEntityByRecordId - the _world==null guard is the only path reachable without a live World.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SpawnEntityByRecordId_BeforeInitializeWithWorld_ReturnsNull_DoesNotThrow()
    {
        var worldProxy = new AlundraWorldProxy();
        var logicEntity = new AlundraEntityScriptProxy();

        var result = worldProxy.SpawnEntityByRecordId(logicEntity, 5);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------------------------
    // DestroyEntity(AlundraEntityScriptProxy) - single-arg overload (0x2E's search-driven destroy)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void DestroyEntity_SingleArg_FlagsToDestroy_AndClearsEventTrigger()
    {
        var worldProxy = new AlundraWorldProxy();
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, EventTrigger = ScriptHelper.ProgramCTick };

        worldProxy.DestroyEntity(entity);

        Assert.Equal(EntityStatus.FlagToDestroy, entity.Status);
        Assert.Equal(ScriptHelper.ProgramUnknown, entity.EventTrigger);
    }

    // -----------------------------------------------------------------------------------------
    // IEntityWorldContext explicit interface members
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void IEntityWorldContext_DestroyEntity_DelegatesToTheSameSingleArgMethod()
    {
        IEntityWorldContext context = new AlundraWorldProxy();
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal };

        context.DestroyEntity(entity);

        Assert.Equal(EntityStatus.FlagToDestroy, entity.Status);
    }

    [Fact]
    public void IEntityWorldContext_SpawnedEntities_EmptyBeforeAnySpawn()
    {
        IEntityWorldContext context = new AlundraWorldProxy();

        Assert.Empty(context.SpawnedEntities);
    }

    // -----------------------------------------------------------------------------------------
    // RunTransformSyncPass
    // -----------------------------------------------------------------------------------------

    private static Entity NewEntityWithProxy(EntityStatus status, int posX, int posY, int posZ)
    {
        var entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy), RootComponent = new AnimatedSpriteComponent() };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Status = status;
        proxy.PosX = posX;
        proxy.PosY = posY;
        proxy.PosZ = posZ;
        return entity;
    }

    [Fact]
    public void RunTransformSyncPass_ActiveEntity_MovesTransformToCurrentLogicalPosition()
    {
        var entity = NewEntityWithProxy(EntityStatus.Normal, 420 * 0x10000, 584 * 0x10000, 46 << 19);

        AlundraFrameSyncPasses.RunTransformSyncPass(new List<Entity> { entity });

        // E3.a: the root now carries the LOGICAL pose (RenderProjection is null in this fixture -
        // AnimatedSpriteComponent is the bare root, so the re-projection call is a no-op here).
        var expected = AlundraWorldProxy.ResolveLogicalPosition(420 * 0x10000, 584 * 0x10000, 46 << 19);
        Assert.Equal(expected, entity.RootComponent.LocalTransform.Position);
    }

    [Fact]
    public void RunTransformSyncPass_PositionChangedSinceSpawn_FollowsTheNewPosition()
    {
        var entity = NewEntityWithProxy(EntityStatus.Normal, 0, 0, 0);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);

        // Simulates a 0x64 SetEntitiesPosition write happening between two frames.
        proxy.PosX = 100 * 0x10000;
        proxy.PosY = 50 * 0x10000;
        proxy.PosZ = 0;

        AlundraFrameSyncPasses.RunTransformSyncPass(new List<Entity> { entity });

        // E3.a: logical pose, Y un-flipped.
        Assert.Equal(new Microsoft.Xna.Framework.Vector3(100, 50, 0f), entity.RootComponent.LocalTransform.Position);
    }

    [Fact]
    public void RunTransformSyncPass_FlagToDestroy_SkipsTransformUpdate()
    {
        var entity = NewEntityWithProxy(EntityStatus.FlagToDestroy, 1000 * 0x10000, 1000 * 0x10000, 0);
        var originalPosition = entity.RootComponent.LocalTransform.Position;

        AlundraFrameSyncPasses.RunTransformSyncPass(new List<Entity> { entity });

        Assert.Equal(originalPosition, entity.RootComponent.LocalTransform.Position);
    }

    [Fact]
    public void RunTransformSyncPass_BareEntityWithNoRootComponent_DoesNotThrow()
    {
        var entity = new Entity { Name = "bare", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();

        AlundraFrameSyncPasses.RunTransformSyncPass(new List<Entity> { entity });
    }

    // -----------------------------------------------------------------------------------------
    // Destroyed-entity visibility/skip in the other per-frame passes
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RunAnimationSyncPass_FlagToDestroy_HidesEntity_AndSkipsSync()
    {
        var entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy), RootComponent = new AnimatedSpriteComponent() };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Status = EntityStatus.FlagToDestroy;
        proxy.CurrentAnimationId = ~0u; // would normally fire a sync if not skipped
        Assert.True(entity.IsVisible);

        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.False(entity.IsVisible);
        Assert.Equal(~0u, proxy.CurrentAnimationId); // untouched - the pass returned before resolving
    }

    [Fact]
    public void RunWallInterleaveSortKeyPass_FlagToDestroy_SkipsElevationWrite()
    {
        var entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        var depthSortable = new DepthSortable2DComponent { Elevation = 123 };
        entity.AddComponent(depthSortable);
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Status = EntityStatus.FlagToDestroy;
        proxy.PosY = 999 * 0x10000;

        AlundraFrameSyncPasses.RunWallInterleaveSortKeyPass(new List<Entity> { entity });

        Assert.Equal(123, depthSortable.Elevation);
    }
}
