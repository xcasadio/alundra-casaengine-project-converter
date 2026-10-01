#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.c1 T4 (docs/plan-e19-opcodes.md §1.2e): opcodes 0x1C and 0x1D exactly as the binary (<c>0x8003D7FC</c>, <c>0x8003D890</c>),
/// the Chain end that feeds their counter, and the Hold flag cleared by every animation switch. Every program starts with a
/// <c>0x01</c> (size 1): the memorised key of a wait is its pc, and a pc of 0 reads as "already memorised" (no program of the corpus
/// has a 0x1C or 0x1D at pc 0).
/// </summary>
public class AlundraRepeatAnimationOpcodeTests
{
    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private sealed class Harness
    {
        public readonly AlundraEventProgramRunner Runner;
        public AlundraEntityScriptProxy Entity = new();
        public readonly EventProgramState State;
        public readonly List<EventTraceRecord> Trace = new();

        public Harness(params int[] codes)
        {
            var document = NewDocument(codes);
            Runner = new AlundraEventProgramRunner(document, new AlundraGameState());
            Runner.TraceSink = Trace.Add;
            State = new EventProgramState { Codes = document.CodesAsBytes() };
        }

        /// <summary>One script call; the handler result of the wait it ended on (0 suspended, 2 ended).</summary>
        public int Call(int opcode)
        {
            Trace.Clear();
            Runner.RunOneScriptCall(Entity, State);
            return Trace.Last(r => r.Opcode == opcode).Size;
        }
    }

    [Fact]
    public void RepeatAnimation_0x1C_FirstCall_MemorisesThePc_ZeroesTheCounter_KeepsTheHoldFlag_AndSuspends()
    {
        var h = new Harness(0x01, 0x1C, 1, 0xFF);
        h.Entity.AnimCompleteCounter = 5;
        h.Entity.ForceResetAnimationFlag = 1;

        Assert.Equal(0, h.Call(0x1C));

        Assert.Equal(0, h.Entity.AnimCompleteCounter);
        Assert.Equal(1, h.Entity.ForceResetAnimationFlag); // never cleared by the handler.
        Assert.Equal(1, h.State.Parameters[1]);
        Assert.Equal(0, h.State.Parameters[2]);
        Assert.Equal(1, h.State.CodeIndex);
    }

    [Fact]
    public void RepeatAnimation_0x1C_WithTheHoldFlag_RestartsTheAnimationByComplementingTheCurrentId_AndEnds()
    {
        var h = new Harness(0x01, 0x1C, 1, 0xFF);
        h.Entity.TargetAnimationId = 7;
        h.Entity.CurrentAnimationId = 7;
        h.Call(0x1C); // first call.
        h.Entity.ForceResetAnimationFlag = 1;

        Assert.Equal(2, h.Call(0x1C));

        Assert.Equal(~7u, h.Entity.CurrentAnimationId);
        Assert.Equal(1, h.Entity.ForceResetAnimationFlag);
        Assert.Equal(3, h.State.CodeIndex); // ended on 0xFF @3.
    }

    [Fact]
    public void RepeatAnimation_0x1C_ACounterOfThree_CountsOnce_AndIsZeroed()
    {
        var h = new Harness(0x01, 0x1C, 2, 0xFF);
        h.Call(0x1C);
        h.Entity.AnimCompleteCounter = 3;

        Assert.Equal(0, h.Call(0x1C)); // one count of two.

        Assert.Equal(1, h.State.Parameters[2]);
        Assert.Equal(0, h.Entity.AnimCompleteCounter);
    }

    [Fact]
    public void RepeatAnimation_0x1C_TheHoldFlagAndTheCounterTogether_CountOnce()
    {
        var h = new Harness(0x01, 0x1C, 2, 0xFF);
        h.Call(0x1C);
        h.Entity.AnimCompleteCounter = 3;
        h.Entity.ForceResetAnimationFlag = 1;

        Assert.Equal(0, h.Call(0x1C));

        Assert.Equal(1, h.State.Parameters[2]);
        Assert.Equal(0, h.Entity.AnimCompleteCounter); // zeroed in both cases.
    }

    [Fact]
    public void RepeatAnimation_0x1C_AZeroCount_EndsOnTheSecondCall()
    {
        var h = new Harness(0x01, 0x1C, 0, 0xFF);

        Assert.Equal(0, h.Call(0x1C));
        Assert.Equal(2, h.Call(0x1C));
    }

    /// <summary>E19.c2 (C3, the one existing test that moves, announced by the plan): E19.c1 pinned 0, 0, 2 here, the double count of a Hold end
    /// under catch-up; with the guard the end is counted once (0, 0, 0), the flag stays, and the count goes on after a switch.</summary>
    [Fact]
    public void RepeatAnimation_0x1C_AHoldFlagHeldWithoutASyncBetweenCalls_CountsTheEndOnce_AndKeepsTheFlag()
    {
        var (entity, proxy) = BuildEntity(withSprite: false);
        proxy.AnimCompleteCounter = 0;
        var h = new Harness(0x01, 0x1C, 2, 0xFF) { Entity = proxy };

        Assert.Equal(0, h.Call(0x1C)); // first call: memorise.
        Assert.Equal(0, h.Call(0x1C)); // count 1: the Hold end is counted, the animation is asked to restart.
        Assert.True(proxy.HoldCountedAwaitingSwitch);
        Assert.Equal(0, h.Call(0x1C)); // the same end is not counted again.
        Assert.Equal(1, h.State.Parameters[2]);
        Assert.Equal(1, proxy.ForceResetAnimationFlag);

        AlundraFrameSyncPasses.SyncAnimation(entity); // the switch the count asked for.

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.False(proxy.HoldCountedAwaitingSwitch);

        proxy.ForceResetAnimationFlag = 1; // the restarted animation ends again.
        Assert.Equal(2, h.Call(0x1C)); // count 2.
    }

    /// <summary>TG2: while a tick of switch is reserved the Hold flag is invisible; the counter is not.</summary>
    [Fact]
    public void RepeatAnimation_0x1C_WhileASwitchTickIsReserved_TheHoldFlagIsInvisible_TheCounterIsNot()
    {
        var h = new Harness(0x01, 0x1C, 1, 0xFF);
        h.Entity.ForceResetAnimationFlag = 1;
        h.Entity.AnimationSwitchTicksReserved = 1;
        h.Entity.TargetAnimationId = 7;
        h.Entity.CurrentAnimationId = 7;

        Assert.Equal(0, h.Call(0x1C)); // first call: memorise.
        Assert.Equal(0, h.Call(0x1C));
        Assert.Equal(0, h.State.Parameters[2]);
        Assert.Equal(7u, h.Entity.CurrentAnimationId);
        Assert.False(h.Entity.HoldCountedAwaitingSwitch);

        h.Entity.ForceResetAnimationFlag = 0;
        h.Entity.AnimCompleteCounter = 1;

        Assert.Equal(2, h.Call(0x1C));
    }

    [Fact]
    public void RepeatAnimationWithCollision_0x1D_ForceAdjustedAtTheFirstCall_EndsAtOnce()
    {
        var h = new Harness(0x01, 0x1D, 3, 0xFF);
        h.Entity.ForceAdjusted = 1;

        Assert.Equal(2, h.Call(0x1D));
        Assert.Equal(3, h.State.CodeIndex);
    }

    [Fact]
    public void RepeatAnimationWithCollision_0x1D_WithoutForceAdjusted_BehavesLike0x1C()
    {
        var h = new Harness(0x01, 0x1D, 1, 0xFF);

        Assert.Equal(0, h.Call(0x1D));
        h.Entity.AnimCompleteCounter = 1;
        Assert.Equal(2, h.Call(0x1D));
    }

    // -----------------------------------------------------------------------------------------
    // The bridge: a Chain end counts, a Hold end does not.
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
        return (entity, component, Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy));
    }

    [Fact]
    public void OnAnimationFinished_AChainEnd_IncrementsTheAnimCompleteCounter()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 54;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [54 * 4] = new() { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = 0 },
        };

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(0u, proxy.TargetAnimationId);
        Assert.Equal(1, proxy.AnimCompleteCounter);
    }

    [Fact]
    public void OnAnimationFinished_ASelfChainEnd_AlsoIncrementsTheCounter()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 1;
        proxy.TargetAnimationId = 1;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [1 * 4] = new() { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = 1 },
        };

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(1, proxy.AnimCompleteCounter);
    }

    [Fact]
    public void OnAnimationFinished_AHoldEnd_LeavesTheCounterAlone()
    {
        var (_, component, proxy) = BuildSpawnedEntity();
        proxy.CurrentAnimationId = 10;
        proxy.AnimCompleteCounter = 4;
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [10 * 4] = new() { Kind = AnimationEndKind.Hold },
        };

        AlundraEntitySpawnFactory.OnAnimationFinished(component, new Animation2d(new Animation2dData()));

        Assert.Equal(1, proxy.ForceResetAnimationFlag);
        Assert.Equal(4, proxy.AnimCompleteCounter);
    }

    // -----------------------------------------------------------------------------------------
    // SyncAnimation: every switch clears the Hold flag, none touches the counter.
    // -----------------------------------------------------------------------------------------

    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildEntity(bool withSprite)
    {
        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        if (withSprite)
        {
            entity.RootComponent = new AnimatedSpriteComponent();
        }

        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.CurrentAnimationId = 0;
        proxy.TargetAnimationId = 0;
        proxy.AnimationDirection = 0;
        proxy.TargetDirection = 0;
        proxy.ForceResetAnimationFlag = 1;
        proxy.AnimCompleteCounter = 2;
        return (entity, proxy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyncAnimation_ATargetChange_ClearsTheHoldFlag_AndKeepsTheCounter(bool withSprite)
    {
        var (entity, proxy) = BuildEntity(withSprite);
        proxy.TargetAnimationId = 5;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(5u, proxy.CurrentAnimationId);
        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.Equal(2, proxy.AnimCompleteCounter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyncAnimation_ADirectionChangeAlone_ClearsTheHoldFlag(bool withSprite)
    {
        var (entity, proxy) = BuildEntity(withSprite);
        proxy.TargetDirection = 6; // row 0, column 2: animation direction 2.

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(2, proxy.AnimationDirection);
        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.Equal(2, proxy.AnimCompleteCounter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyncAnimation_ARestartedChain_ClearsTheHoldFlag(bool withSprite)
    {
        var (entity, proxy) = BuildEntity(withSprite);
        proxy.PendingChainRestartFlag = 1;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(0, proxy.ForceResetAnimationFlag);
        Assert.Equal(2, proxy.AnimCompleteCounter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyncAnimation_WithoutAChange_LeavesTheHoldFlag(bool withSprite)
    {
        var (entity, proxy) = BuildEntity(withSprite);

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(1, proxy.ForceResetAnimationFlag);
        Assert.Equal(2, proxy.AnimCompleteCounter);
    }

    // -----------------------------------------------------------------------------------------
    // E19.c2: the switch and the logical clock (TG3, TG4)
    // -----------------------------------------------------------------------------------------

    private static Animation2d MakeAnimation(uint id, AnimationType type, float seconds)
    {
        var data = new Animation2dData { Name = $"bank_anim{id}_down", AnimationType = type };
        data.Parts.Add(new Animation2dPartData { Id = "body" });
        data.CollisionKeyframes.Add(new Animation2dCollisionKeyframeData { TimeSeconds = seconds });
        return new Animation2d(data);
    }

    /// <summary>An entity on its animation 0 (a Loop of 10 ticks) with the logical clock on; animation 5 is a Loop of 10 ticks, animation 6 a
    /// Once of 2 ticks that chains to 0. Everything the first sync owed is cleared.</summary>
    private static (Entity Entity, AnimatedSpriteComponent Sprite, AlundraEntityScriptProxy Proxy) BuildClockedEntity()
    {
        var sprite = new AnimatedSpriteComponent();
        sprite.AddAnimation(MakeAnimation(0, AnimationType.Loop, 0.2f));
        sprite.AddAnimation(MakeAnimation(5, AnimationType.Loop, 0.2f));
        sprite.AddAnimation(MakeAnimation(6, AnimationType.Once, 0.04f));
        var entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy), RootComponent = sprite };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.AnimationEndByAnimDirection = new Dictionary<int, AnimationEndInfo>
        {
            [6 * AlundraEntitySpawnFactory.IdsvDirectionStride] = new() { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = 0 },
        };
        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);

        proxy.TargetAnimationId = 0;
        proxy.CurrentAnimationId = ~0u;
        AlundraFrameSyncPasses.SyncAnimation(entity);
        proxy.AnimationSwitchTickOwed = false;
        return (entity, sprite, proxy);
    }

    /// <summary>TG3: after a switch, the tick of the switch is owed when no tick was reserved for it, spent when one was, and a catch-up frame
    /// advances the new animation by the other reserved ticks. The reserved count is always back to 0 after a sync.</summary>
    [Theory]
    [InlineData(0, true, 0)]
    [InlineData(1, false, 0)]
    [InlineData(3, false, 2)]
    public void SyncAnimation_ASwitchOnAClockedSprite_OwesOrSpendsTheTickOfTheSwitch(int reserved, bool expectedOwed, int expectedTick)
    {
        var (entity, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.TargetAnimationId = 5;
        proxy.AnimationSwitchTicksReserved = reserved;
        proxy.HoldCountedAwaitingSwitch = true;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.False(proxy.HoldCountedAwaitingSwitch);
        Assert.Equal(5u, proxy.CurrentAnimationId);
        Assert.Equal(expectedOwed, proxy.AnimationSwitchTickOwed);
        Assert.Equal(expectedTick, sprite.LogicalTick);
        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
    }

    [Fact]
    public void SyncAnimation_WithoutASwitch_OrOnAnEntityToDestroy_StillZeroesTheReservedTicks()
    {
        var (entity, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.AnimationSwitchTicksReserved = 2;

        AlundraFrameSyncPasses.SyncAnimation(entity); // no change pending.

        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
        Assert.Equal(4, sprite.LogicalTick);
        Assert.False(proxy.AnimationSwitchTickOwed);

        proxy.Status = EntityStatus.FlagToDestroy;
        proxy.AnimationSwitchTicksReserved = 2;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
    }

    [Fact]
    public void SyncAnimation_ASwitchToAnAnimationTheSpriteDoesNotHave_RestartsTheOneThatPlays()
    {
        var (entity, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.TargetAnimationId = 9; // not one of the sprite's animations.

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(9u, proxy.CurrentAnimationId);
        Assert.EndsWith("_anim0_down", sprite.CurrentAnimation!.Animation2dData.Name);
        Assert.Equal(0, sprite.LogicalTick);
        Assert.True(proxy.AnimationSwitchTickOwed);
    }

    /// <summary>A sprite with no animation at all, whose selection fails at the switch: nothing to restart, no exception.</summary>
    [Fact]
    public void SyncAnimation_ASwitchOnASpriteWithoutAnyAnimation_DoesNotThrow()
    {
        var sprite = new AnimatedSpriteComponent();
        var entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy), RootComponent = sprite };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);
        proxy.TargetAnimationId = 3;
        proxy.CurrentAnimationId = ~3u;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(3u, proxy.CurrentAnimationId);
        Assert.Null(sprite.CurrentAnimation);
    }

    /// <summary>TG4: one step of the clock: a pending switch keeps the tick, a tick owed is consumed, otherwise the animation advances.</summary>
    [Fact]
    public void StepAnimationClock_ASwitchPending_ReservesTheTick_AndSpendsTheTickOwed()
    {
        var (_, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.TargetAnimationId = 5; // Current is 0: a switch is pending.
        proxy.AnimationSwitchTickOwed = true;

        AlundraFrameSyncPasses.StepAnimationClock(proxy);

        Assert.Equal(1, proxy.AnimationSwitchTicksReserved);
        Assert.False(proxy.AnimationSwitchTickOwed);
        Assert.Equal(4, sprite.LogicalTick);
    }

    [Fact]
    public void StepAnimationClock_ATickOwedAndNoSwitch_IsConsumedWithoutAdvancing()
    {
        var (_, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.AnimationSwitchTickOwed = true;

        AlundraFrameSyncPasses.StepAnimationClock(proxy);

        Assert.False(proxy.AnimationSwitchTickOwed);
        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
        Assert.Equal(4, sprite.LogicalTick);
    }

    [Fact]
    public void StepAnimationClock_NothingPending_AdvancesOneTick()
    {
        var (_, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);

        AlundraFrameSyncPasses.StepAnimationClock(proxy);

        Assert.Equal(5, sprite.LogicalTick);
        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
    }

    [Fact]
    public void StepAnimationClock_ADirectionChangeAlone_ReservesTheTick_AndRaisesNoEnd()
    {
        var (_, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.AnimationSwitchTickOwed = true;
        proxy.TargetDirection = 6; // row 0, column 2: animation direction 2.
        var turns = proxy.AnimCompleteCounter;

        AlundraFrameSyncPasses.StepAnimationClock(proxy);

        Assert.Equal(1, proxy.AnimationSwitchTicksReserved);
        Assert.False(proxy.AnimationSwitchTickOwed);
        Assert.Equal(4, sprite.LogicalTick);
        Assert.Equal(turns, proxy.AnimCompleteCounter);
    }

    [Fact]
    public void StepAnimationClock_AnAdvanceThatRaisesAChainEnd_ReservesTheTick_AndAsksForTheRestart()
    {
        var (entity, sprite, proxy) = BuildClockedEntity();
        proxy.TargetAnimationId = 6;
        AlundraFrameSyncPasses.SyncAnimation(entity); // plays animation 6: a Once of 2 ticks, chaining to 0.
        proxy.AnimationSwitchTickOwed = false;
        AlundraFrameSyncPasses.StepAnimationClock(proxy); // tick 1.
        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);

        AlundraFrameSyncPasses.StepAnimationClock(proxy); // tick 2: the end.

        Assert.Equal(1, proxy.AnimationSwitchTicksReserved);
        Assert.Equal(1, proxy.PendingChainRestartFlag);
        Assert.Equal(0u, proxy.TargetAnimationId);
    }

    [Fact]
    public void StepAnimationClock_AnEntityToDestroy_OrWithoutAClockedSprite_DoesNothing()
    {
        var (_, sprite, proxy) = BuildClockedEntity();
        sprite.AdvanceLogicalTicks(4);
        proxy.Status = EntityStatus.FlagToDestroy;
        proxy.TargetAnimationId = 5;

        AlundraFrameSyncPasses.StepAnimationClock(proxy);

        Assert.Equal(0, proxy.AnimationSwitchTicksReserved);
        Assert.Equal(4, sprite.LogicalTick);

        var (_, bare) = BuildEntity(withSprite: false);
        bare.TargetAnimationId = 5;

        AlundraFrameSyncPasses.StepAnimationClock(bare);

        Assert.Equal(0, bare.AnimationSwitchTicksReserved);
    }

    /// <summary>The Hold flag of an entity whose switch is pending is cleared between two map-event passes of a frame; the others keep it.</summary>
    [Fact]
    public void ClearHoldFlagsOfPendingSwitches_ClearsTheFlagOfAPendingSwitchOnly()
    {
        var (pendingEntity, pending) = BuildEntity(withSprite: false);
        pending.TargetAnimationId = 5; // Current is 0: a switch is pending.
        pending.HoldCountedAwaitingSwitch = true;
        var (chainEntity, chain) = BuildEntity(withSprite: false);
        chain.PendingChainRestartFlag = 1;
        var (steadyEntity, steady) = BuildEntity(withSprite: false);

        AlundraFrameSyncPasses.ClearHoldFlagsOfPendingSwitches(new[] { pendingEntity, chainEntity, steadyEntity });

        Assert.Equal(0, pending.ForceResetAnimationFlag);
        Assert.False(pending.HoldCountedAwaitingSwitch);
        Assert.Equal(0, chain.ForceResetAnimationFlag);
        Assert.Equal(1, steady.ForceResetAnimationFlag);
    }
}
