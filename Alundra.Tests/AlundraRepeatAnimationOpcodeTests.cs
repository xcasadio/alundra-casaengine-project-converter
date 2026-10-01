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
        public readonly AlundraEntityScriptProxy Entity = new();
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

    [Fact]
    public void RepeatAnimation_0x1C_AHoldFlagHeldWithoutASyncBetweenCalls_CountsOnEveryCall_AndKeepsTheFlag()
    {
        var h = new Harness(0x01, 0x1C, 2, 0xFF);
        h.Entity.ForceResetAnimationFlag = 1;

        Assert.Equal(0, h.Call(0x1C)); // first call: memorise.
        Assert.Equal(0, h.Call(0x1C)); // count 1.
        Assert.Equal(2, h.Call(0x1C)); // count 2.
        Assert.Equal(1, h.Entity.ForceResetAnimationFlag);
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
}
