#nullable enable
using System;
using System.Collections.Generic;
using Xunit;
using Alundra.Ai.Projectiles;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.f4 T1 (docs/plan-e20-heros.md section 1.20, D-F4-7, case (c9)): the arena switch by sprite type, <see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/> (null in production). With the switch of the native handlers OFF (as the arenas of the hit passes keep it, so that the Beannoids
/// stay inert) and a set of types posed, the handler of a type of the set runs and the handler of a type outside it does not; without the set, none runs; with the switch ON the set changes nothing. Case (c9) in the arena of map 14: a projectile of the type 7 runs C94 while a Beannoid of the same arena stays inert,
/// and the same arrow without the set runs nothing. Each case without effect is set next to the positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class NativeSpriteTypeFilterTests : IDisposable
{
    private const int TypeA = 7001;
    private const int TypeB = 7002;
    private const int Code = 25;

    public NativeSpriteTypeFilterTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        ChargedArrowAi.StubCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static EntityScriptProxy Entity(int spriteType)
    {
        var entity = new EntityScriptProxy { SpriteType = spriteType, Status = EntityStatus.Normal, EventTrigger = ScriptHelper.ProgramCTick };
        entity.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = Code;
        return entity;
    }

    [Fact]
    public void WithTheSwitchOff_TheHandlerOfATypeOfTheSetRuns_TheOtherTypeAndTheEmptyFilterRunNothing_WithTheSwitchOnTheSetChangesNothing()
    {
        var runner = new EventProgramRunner(null, GameState.Instance);
        var calls = new Dictionary<int, int> { [TypeA] = 0, [TypeB] = 0 };
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, Code, new[] { TypeA, TypeB }, (entity, _, _) => calls[entity.SpriteType]++);
        runner.NativeHandlersEnabled = false;

        // The set {A}: A runs, B does not.
        EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { TypeA };
        runner.RunSpriteEvent(Entity(TypeA));
        runner.RunSpriteEvent(Entity(TypeB));
        Assert.Equal((1, 0), (calls[TypeA], calls[TypeB]));
        Assert.Equal(1, runner.NativeHandlerRunCount);

        // No set (production): neither runs, as before the switch existed.
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        runner.RunSpriteEvent(Entity(TypeA));
        runner.RunSpriteEvent(Entity(TypeB));
        Assert.Equal((1, 0), (calls[TypeA], calls[TypeB]));

        // The empty set: neither runs.
        EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int>();
        runner.RunSpriteEvent(Entity(TypeA));
        Assert.Equal(1, calls[TypeA]);

        // The switch ON: every registered type runs whatever the set.
        runner.NativeHandlersEnabled = true;
        EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { TypeA };
        runner.RunSpriteEvent(Entity(TypeA));
        runner.RunSpriteEvent(Entity(TypeB));
        Assert.Equal((2, 1), (calls[TypeA], calls[TypeB]));
    }

    [Fact]
    public void C9_InTheArenaOfMap14_AProjectileOfTheType7RunsC94_ABeannoidStaysInert_AndWithoutTheSetTheProjectileRunsNothing()
    {
        using var arena = new HitArena("f4-c9", 100, 100, new List<int[]> { new[] { 300, 104 } }, 24, 0);
        SyntheticField.Install(arena.Arc, null);
        var runner = (EventProgramRunner)arena.Arc.Runner;
        Assert.False(runner.NativeHandlersEnabled); // the arena keeps the Beannoids inert.
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var arrow = ((IEntityWorldContext)arena.Arc.Proxy).SpawnEntityBySpriteIndex(arena.Hero, false, 7, 100 << 16, 100 << 16, 8 << 16, 0x18)!;
        Assert.Equal(7, arrow.SpriteType);

        // With the set {7}: the arrow's C94 runs at each tick, the Beannoid (type 335) runs none of its A1, C1 and D1.
        EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { 7 };
        arena.Begin();
        for (var i = 0; i < 8; i++)
        {
            arena.Frame(0, 1);
        }

        var withSet = runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, 94));
        Assert.True(withSet > 0, "C94 runs for the type 7 with the set");
        Assert.Equal(0, runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramALoad, 1)) + runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, 1)) + runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramDTouch, 1)));
        Assert.Equal(withSet, journal.Count); // the stub of the cut is called by each run.

        // Without the set the same arrow, still alive, runs nothing more.
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        Assert.Contains(arrow, arena.Arc.Entities);
        for (var i = 0; i < 4; i++)
        {
            arena.Frame(0, 1);
        }

        Assert.Equal(withSet, runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, 94)));
        Assert.Equal(withSet, journal.Count);
    }
}
