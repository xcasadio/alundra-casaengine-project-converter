using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the "Alundra's animation is too fast" fix's DLL-side bridge: reading End/ChainTo off
/// <see cref="SpriteRecordHeader.IdsvAnimDirs"/> (<see cref="AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection"/>)
/// and reacting to <see cref="AnimatedSpriteComponent.AnimationFinished"/>
/// (<see cref="AlundraEntitySpawnFactory.OnAnimationFinished"/>) - bridging the engine's Once-finished event
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

        var table = AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection(idsvAnimDirs);

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

        var table = AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection(idsvAnimDirs);

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
        Assert.Null(AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection(null));
        Assert.Null(AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection(new List<AnimDirIdsv>()));
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

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

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

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0u, proxy.TargetAnimationId);
        Assert.Equal(0, proxy.ForceResetAnimationFlag); // Chain only touches TargetAnimationId, not Hold.
        Assert.Equal(1, proxy.PendingChainRestartFlag); // every chain asks for a restart - see below.
    }

    /// <summary>
    /// User-reported bug (2026-08-26, "the walk animation does not loop when it should"): the original
    /// spells a looping animation TWO ways, and only one survives conversion as an engine loop. A
    /// terminator with <c>TerminatorCode == 1</c> becomes <c>AnimationType.Loop</c>; a terminator that
    /// CHAINS BACK TO ITSELF becomes <c>Once</c> plus a chain edge onto its own id. The hero's real
    /// exported data is exactly that: anim 0 (idle) is <c>Loop</c>, while anim 1 (walk) is
    /// <c>Chain, ChainTo = 1</c> in all four directions - which is why the idle looped on screen and the
    /// walk froze on its last frame.
    ///
    /// Without <see cref="AlundraEntityScriptProxy.PendingChainRestartFlag"/> this self-chain is
    /// invisible to the sync pass: <see cref="AlundraFrameSyncPasses.TryResolveAnimationTarget"/> only reports
    /// work when the animation id or the direction CHANGES, and a self-chain changes neither, so
    /// <c>SetCurrentAnimation(..., forceReset: true)</c> was never reached. This test pins the exact
    /// self-chain shape (both ids equal to 1), so it fails if the restart flag is dropped.
    /// </summary>
    [Fact]
    public void OnAnimationFinished_SelfChainEntry_RequestsARestartEvenThoughTheIdIsUnchanged()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 1; // the hero's walk
        proxy.AnimationDirection = 0;
        proxy.TargetAnimationId = 1;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [1 * 4 + 0] = new() { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = 1 },
        };

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        // The chain target IS the animation that just ended, so nothing about the target changed...
        Assert.Equal(1u, proxy.TargetAnimationId);
        Assert.Equal(proxy.CurrentAnimationId, proxy.TargetAnimationId);
        // ...and the restart flag is therefore the ONLY thing that can make the walk play again.
        Assert.Equal(1, proxy.PendingChainRestartFlag);
    }

    [Fact]
    public void OnAnimationFinished_HoldEntry_DoesNotRequestAChainRestart()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 54;
        proxy.AnimationDirection = 0;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [54 * 4 + 0] = new() { Kind = AnimationEndKind.Hold },
        };

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(1, proxy.ForceResetAnimationFlag);
        Assert.Equal(0, proxy.PendingChainRestartFlag); // a Hold must stay frozen, never restart.
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

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.Equal(0u, proxy.TargetAnimationId);
    }

    [Fact]
    public void OnAnimationFinished_NoAnimationEndTable_IsANoOp()
    {
        // Degraded catalog, or nothing wired up for this entity - same as a lookup miss.
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.AnimationEndByAnimDirection = null;

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
    }

    [Fact]
    public void OnAnimationFinished_SenderIsNotAnAnimatedSpriteComponent_DoesNotThrow()
    {
        AlundraEntitySpawnFactory.OnAnimationFinished(new object(), new Animation2d(new Animation2dData()));
        AlundraEntitySpawnFactory.OnAnimationFinished(null, new Animation2d(new Animation2dData()));
    }

    // -----------------------------------------------------------------------------------------
    // SubscribeAnimationEndBridge
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SubscribeAnimationEndBridge_EntityWithNoAnimatedSpriteComponent_DoesNotThrow()
    {
        var entity = new Entity { Name = "bare", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();

        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);
    }

    // -----------------------------------------------------------------------------------------
    // E19.c2: the logical clock and the Loop signal (TB1, TB2)
    // -----------------------------------------------------------------------------------------

    /// <summary>A spawned entity whose sprite plays one Loop of <paramref name="durationSeconds"/> (animation 0, facing down).</summary>
    private static (Entity Entity, AnimatedSpriteComponent Component, AlundraEntityScriptProxy Proxy) BuildEntityOnALoop(float durationSeconds)
    {
        var component = new AnimatedSpriteComponent();
        var data = new Animation2dData { Name = "bank_anim0_down", AnimationType = AnimationType.Loop };
        data.Parts.Add(new Animation2dPartData { Id = "body" });
        data.CollisionKeyframes.Add(new Animation2dCollisionKeyframeData { TimeSeconds = durationSeconds });
        component.AddAnimation(new Animation2d(data));

        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = component,
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);
        component.SetCurrentAnimation(0, forceReset: true);
        return (entity, component, proxy);
    }

    /// <summary>TB1: the bridge turns the logical clock of the sprite on at 50 ticks per second, keeps the sprite on the proxy, and
    /// feeds the Loop turn into <see cref="AlundraEntityScriptProxy.AnimCompleteCounter"/>.</summary>
    [Fact]
    public void SubscribeAnimationEndBridge_TurnsTheLogicalClockOn_KeepsTheSprite_AndCountsALoopTurn()
    {
        var (_, component, proxy) = BuildEntityOnALoop(0.06f);

        Assert.Equal(50, component.LogicalTickRate);
        Assert.Same(component, proxy.LogicalClockSprite);
        Assert.Equal(3, component.LogicalDurationTicks);
        Assert.Equal(0, proxy.AnimCompleteCounter);

        component.AdvanceLogicalTicks(3); // one turn of the Loop.

        Assert.Equal(1, proxy.AnimCompleteCounter);
    }

    [Fact]
    public void SubscribeAnimationEndBridge_WithoutASprite_LeavesTheClockSpriteNull()
    {
        var entity = new Entity { Name = "bare", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);

        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);

        Assert.Null(proxy.LogicalClockSprite);
    }

    /// <summary>TB2: each raising of the Loop signal adds one (an advance that makes two turns raises twice), a record with no end table
    /// counts too, and a sender that is not a sprite of an entity with a proxy is ignored.</summary>
    [Fact]
    public void OnAnimationLooped_EachTurnAddsOne_WithOrWithoutAnEndTable_AndAForeignSenderIsIgnored()
    {
        var (_, component, proxy) = BuildEntityOnALoop(0.06f);
        Assert.Null(proxy.AnimationEndByAnimDirection); // an all-Loop record has no end table.

        component.AdvanceLogicalTicks(3);
        Assert.Equal(1, proxy.AnimCompleteCounter);

        component.AdvanceLogicalTicks(6); // two turns in one advance: two raisings.
        Assert.Equal(3, proxy.AnimCompleteCounter);

        // A record with an end table (Hold and Chain entries only) counts the turns of its Loops all the same.
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [10 * 4] = new() { Kind = AnimationEndKind.Hold },
        };
        component.AdvanceLogicalTicks(3);
        Assert.Equal(4, proxy.AnimCompleteCounter);

        var animation = new Animation2d(new Animation2dData());
        AlundraEntitySpawnFactory.OnAnimationLooped(null, animation);
        AlundraEntitySpawnFactory.OnAnimationLooped(new object(), animation);
        AlundraEntitySpawnFactory.OnAnimationLooped(new AnimatedSpriteComponent(), animation); // no owner.
        Assert.Equal(4, proxy.AnimCompleteCounter);
    }
}
