using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the "Alundra's animation is too fast" fix's DLL-side bridge: reading End/ChainTo off
/// <see cref="SpriteRecordHeader.IdsvAnimDirs"/> (<see cref="AlundraWorldProxy.BuildAnimationEndByAnimDirection"/>)
/// and reacting to <see cref="AnimatedSpriteComponent.AnimationFinished"/>
/// (<see cref="AlundraWorldProxy.OnAnimationFinished"/>) - bridging the engine's Once-finished event
/// back to the original's Hold ("freeze") / Chain ("play this other animation next") semantics,
/// EntityManager.cs:257-281.
/// </summary>
public class AlundraWorldProxyAnimationEndBridgeTests
{
    // -----------------------------------------------------------------------------------------
    // BuildAnimationEndByAnimDirection
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BuildAnimationEndByAnimDirection_LoopEntry_IsNotAddedToTheTable()
    {
        var idsvAnimDirs = new List<AnimDirIdsv>
        {
            new() { Anim = 0, Direction = 0, End = AnimationEndKind.Loop },
        };

        var table = AlundraWorldProxy.BuildAnimationEndByAnimDirection(idsvAnimDirs);

        Assert.Null(table); // every entry was Loop -> nothing worth keeping.
    }

    [Fact]
    public void BuildAnimationEndByAnimDirection_HoldAndChainEntries_AreKeyedByAnimAndDirection()
    {
        var idsvAnimDirs = new List<AnimDirIdsv>
        {
            new() { Anim = 0, Direction = 0, End = AnimationEndKind.Loop },
            new() { Anim = 10, Direction = 2, End = AnimationEndKind.Hold },
            new() { Anim = 54, Direction = 0, End = AnimationEndKind.Chain, ChainTo = 0 },
        };

        var table = AlundraWorldProxy.BuildAnimationEndByAnimDirection(idsvAnimDirs);

        Assert.NotNull(table);
        Assert.Equal(2, table!.Count); // the Loop entry is excluded.

        Assert.True(table.TryGetValue(10 * 4 + 2, out var hold));
        Assert.Equal(AnimationEndKind.Hold, hold.Kind);

        Assert.True(table.TryGetValue(54 * 4 + 0, out var chain));
        Assert.Equal(AnimationEndKind.Chain, chain.Kind);
        Assert.Equal(0, chain.ChainTargetAnimationId);
    }

    [Fact]
    public void BuildAnimationEndByAnimDirection_EmptyOrNullList_ReturnsNull()
    {
        Assert.Null(AlundraWorldProxy.BuildAnimationEndByAnimDirection(null));
        Assert.Null(AlundraWorldProxy.BuildAnimationEndByAnimDirection(new List<AnimDirIdsv>()));
    }

    // -----------------------------------------------------------------------------------------
    // OnAnimationFinished
    // -----------------------------------------------------------------------------------------

    private static (Entity Entity, AnimatedSpriteComponent Component, AlundraEntityScriptProxy Proxy) BuildSpawnedEntity()
    {
        var component = new AnimatedSpriteComponent();
        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = component,
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        return (entity, component, proxy);
    }

    [Fact]
    public void OnAnimationFinished_HoldEntry_SetsForceResetAnimationFlag()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 10;
        proxy.AnimationDirection = 2;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [10 * 4 + 2] = new() { Kind = AnimationEndKind.Hold },
        };

        AlundraWorldProxy.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(1, proxy.ForceResetAnimationFlag);
        Assert.Equal(0u, proxy.TargetAnimationId); // Hold never touches TargetAnimationId, unlike Chain.
    }

    [Fact]
    public void OnAnimationFinished_ChainEntry_SetsTargetAnimationIdToChainTarget()
    {
        // Reproduces the reported bug's own fix: hero anim 54 (LoadingMap) chains to anim 0 (Idle).
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 54;
        proxy.AnimationDirection = 0;
        proxy.TargetAnimationId = 54;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [54 * 4 + 0] = new() { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = 0 },
        };

        AlundraWorldProxy.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0u, proxy.TargetAnimationId);
        Assert.Equal(0, proxy.ForceResetAnimationFlag); // Chain only touches TargetAnimationId, not Hold.
    }

    [Fact]
    public void OnAnimationFinished_NoTableEntryForCurrentAnimDirection_IsANoOp()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 0;
        proxy.AnimationDirection = 0;
        proxy.TargetAnimationId = 0;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [10 * 4 + 2] = new() { Kind = AnimationEndKind.Hold }, // a different (anim, direction)
        };

        AlundraWorldProxy.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.Equal(0u, proxy.TargetAnimationId);
    }

    [Fact]
    public void OnAnimationFinished_NoAnimationEndTable_IsANoOp()
    {
        // Degraded catalog, or nothing wired up for this entity - same as a lookup miss.
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.AnimationEndByAnimDirection = null;

        AlundraWorldProxy.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
    }

    [Fact]
    public void OnAnimationFinished_SenderIsNotAnAnimatedSpriteComponent_DoesNotThrow()
    {
        AlundraWorldProxy.OnAnimationFinished(new object(), new Animation2d(new Animation2dData()));
        AlundraWorldProxy.OnAnimationFinished(null, new Animation2d(new Animation2dData()));
    }

    // -----------------------------------------------------------------------------------------
    // SubscribeAnimationEndBridge
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SubscribeAnimationEndBridge_EntityWithNoAnimatedSpriteComponent_DoesNotThrow()
    {
        var entity = new Entity { Name = "bare", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();

        AlundraWorldProxy.SubscribeAnimationEndBridge(entity);
    }
}
