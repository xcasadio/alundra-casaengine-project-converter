#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T1, phase A (docs/plan-e20-heros.md section 1.31.3, tests (t1), (d1) to (d4), (e1), (e2)): the table, the driver <c>0x80035320</c> and the native handler E3 (<c>0x8007EDA0</c>) of the earth tome 1, on a hero and rocks made by hand and on a registry of
/// their own (the registry of production is not touched in the phase A). The expected values are the vectors of the real code of the binary (the 689 of T0 and the 384 of the base directions 4, 12, 20 and 28), replayed field by field, and hand cases next to them:
/// each case without effect is set next to a case of the same front that acts.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome1AiTests : IDisposable
{
    private const uint Tome = 0x2B;
    private const uint Hold = PlayerManager.TomeHoldAnimationId;
    private const uint Cast = PlayerManager.TomeCastAnimationId;
    private const uint End = PlayerManager.TomeEndAnimationId;

    public EarthTome1AiTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t1) the table.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static byte[] ExecutableTable() => Convert.FromHexString(File.ReadAllText(AnnexFiles.EarthTome1Annex("t1/table_8002343C.hex")).Trim());

    [Fact]
    public void T1_TheTable_IsTheOneOfTheExecutable_32EntriesOfU16S16S16_AndTheSentinel()
    {
        var expected = ExecutableTable();

        Assert.Equal(194, expected.Length);
        Assert.Equal(expected, EarthTome1Ai.TableBytes);
        Assert.Equal(0xFFFF, expected[192] | (expected[193] << 8)); // the sentinel is the executable's.

        var decoded = new List<(int Tick, int Speed, int Delta)>();
        for (var i = 0; i < 32; i++)
        {
            decoded.Add((expected[6 * i] | (expected[6 * i + 1] << 8), (short)(expected[6 * i + 2] | (expected[6 * i + 3] << 8)), (short)(expected[6 * i + 4] | (expected[6 * i + 5] << 8))));
        }

        Assert.Equal(decoded, EarthTome1Ai.Table);
        Assert.Equal(Enumerable.Range(0, 32).Select(i => 20 + 5 * (i / 2)), EarthTome1Ai.Table.Select(e => e.Tick)); // two entries a tick, 20 to 95 by 5.
        Assert.All(EarthTome1Ai.Table, e => Assert.Equal(e.Tick <= 55 ? 1024 : 1280, e.Speed));
        var deltas = new[] { 3, -6, 0, 6, 3, 2, 8, 14, 3, 10, 16, 22, 3, 18, 24, 30 };
        Assert.Equal(deltas.Concat(deltas), EarthTome1Ai.Table.Select(e => e.Delta));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d1) the driver against the vectors.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (BombHandsBench Bench, List<uint> Cancelled) World(EarthTome1Vectors.Driver v)
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var hero = bench.Hero;
        hero.PosX = v.HeroX << 16;
        hero.PosY = v.HeroY << 16;
        hero.PosZ = v.HeroZ;
        hero.TargetDirection = v.HeroDirection;
        hero.TargetAnimationId = v.HeroAnim;
        hero.Slope_18c = v.Slope;
        hero.ItemInUse = Tome;
        hero.ItemUsePhase = v.Phase;
        hero.ItemUseCounter = v.Counter;
        bench.Host.TableIsFull = !v.SpawnReturnsEntity;
        var cancelled = new List<uint>();
        PlayerManager.ItemCancelForTests = (_, item) => cancelled.Add(item);
        return (bench, cancelled);
    }

    /// <summary>The rocks the world gave birth to (the hero is the first entity the world lists).</summary>
    private static List<EntityScriptProxy> Rocks(BombHandsBench bench) => bench.Host.Born.Where(b => b.SpriteType == 16).ToList();

    private static void AssertDriver(EarthTome1Vectors.Driver v, string label)
    {
        var (bench, cancelled) = World(v);
        var hero = bench.Hero;

        var answer = EarthTome1Ai.Driver(hero);

        Assert.True(v.Return == answer, $"{label}: answer {answer}, vector {v.Return}");
        Assert.True(v.PhaseAfter == hero.ItemUsePhase, $"{label}: phase {hero.ItemUsePhase}, vector {v.PhaseAfter}");
        Assert.True(v.HeroAnimAfter == hero.TargetAnimationId, $"{label}: animation {hero.TargetAnimationId:X}, vector {v.HeroAnimAfter:X}");

        // The driver frees the item only through the guard (the cancellation of the item, then the answer 1); the wrapper frees it when the answer is non-zero (that part is the tick's, tested by D1_TheCounterAndTheRelease...).
        if (v.Phase != 2)
        {
            Assert.True(v.ItemAfter == hero.ItemInUse, $"{label}: item {hero.ItemInUse:X}, vector {v.ItemAfter:X}");
            Assert.Equal(v.ItemAfter == 0 ? new[] { Tome } : Array.Empty<uint>(), cancelled);
        }
        else
        {
            Assert.Equal(Tome, hero.ItemInUse);
            Assert.Empty(cancelled);
        }

        Assert.Equal(v.Spawns.Count, bench.Host.SpawnCalls.Count);
        for (var k = 0; k < v.Spawns.Count; k++)
        {
            var call = bench.Host.SpawnCalls[k];
            var spawn = v.Spawns[k];
            Assert.True((hero, false, spawn.Type, spawn.X, spawn.Y, spawn.Z, spawn.Direction) == call, $"{label}: birth {k} is {call}, vector {spawn}");
        }

        Assert.Equal(v.ForceZWritten, Rocks(bench).Select(b => b.ForceZ).ToList()); // the speed is written on a birth that gave an entity, and on that one alone.
        if (!v.SpawnReturnsEntity)
        {
            Assert.Empty(Rocks(bench));
        }
    }

    [Fact]
    public void D1_Driver_TheSixHundredAndEightyNineVectorsOfTheOracle_AreReplayedField_AnswerPhaseAnimationBirthsAndSpeedOfZ()
    {
        var vectors = EarthTome1Vectors.Drivers("disc/h4copy/vec/vec_driver_2B.json");

        Assert.Equal(689, vectors.Count);
        foreach (var (v, index) in vectors.Select((v, i) => (v, i)))
        {
            AssertDriver(v, $"vector {index}");
            PlayerManager.ItemCancelForTests = null;
        }

        // Both sides of every front are in the sample: births and none, the guard acting and not, the three phases.
        Assert.Contains(vectors, v => v.Spawns.Count == 2 && v.ForceZWritten.Count == 2);
        Assert.Contains(vectors, v => v.Spawns.Count == 2 && v.ForceZWritten.Count == 0);
        Assert.Contains(vectors, v => v is { Phase: 0, PhaseAfter: 1 });
        Assert.Contains(vectors, v => v is { Phase: 1, PhaseAfter: 2 });
        Assert.Contains(vectors, v => v is { Phase: 2, Return: 1 });
    }

    [Fact]
    public void D1_Driver_TheThreeHundredAndEightyFourVectorsOfTheBaseDirections4_12_20_28_AreReplayed()
    {
        var vectors = EarthTome1Vectors.Drivers("t0/vec/vec_driver_2B_dirs.json");

        Assert.Equal(384, vectors.Count);
        Assert.Equal(new uint[] { 4, 12, 20, 28 }, vectors.Select(v => v.HeroDirection).Distinct().Order().ToArray());
        foreach (var (v, index) in vectors.Select((v, i) => (v, i)))
        {
            AssertDriver(v, $"vector {index}");
            PlayerManager.ItemCancelForTests = null;
        }

        Assert.Contains(vectors, v => v.Spawns.Count == 2);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d1) the counter and the release, by the path of the tick: the wrapper 0x8003634C increments the counter (saturated) after the driver and frees the item when the driver answers non-zero.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void D1_TheCounterAndTheRelease_ByThePathOfTheTick_AreTheWrapperOfTheOracle_TheCounterSaturatesAtTheLargestPositiveWord()
    {
        var vectors = EarthTome1Vectors.Drivers("disc/h4copy/vec/vec_driver_2B.json")
            .Where(v => v.Slope == 0 && v.HeroAnim is Cast or Hold or End or 0)
            .GroupBy(v => (v.Phase, v.Counter, v.HeroAnim))
            .Select(g => g.First())
            .ToList();

        Assert.True(vectors.Count > 100, vectors.Count.ToString());
        using var bench = new TomeBench("d1-tick");
        PlayerManager.ItemStartForTests = (_, _) => 0;
        PlayerManager.ItemDriverForTests = (hero, _) => EarthTome1Ai.Driver(hero);
        PlayerManager.ItemCancelForTests = (hero, _) => EarthTome1Ai.Cancel(hero);
        var driven = bench.Hero;
        var freed = 0;
        var kept = 0;
        var saturated = 0;
        foreach (var v in vectors)
        {
            driven.ItemInUse = Tome;
            driven.ItemUsePhase = v.Phase;
            driven.ItemUseCounter = v.Counter;
            driven.TargetAnimationId = v.HeroAnim;

            bench.Tick();

            var label = $"phase {v.Phase} counter {v.Counter} animation {v.HeroAnim:X}";
            Assert.True(v.CounterAfter == driven.ItemUseCounter, $"{label}: counter {driven.ItemUseCounter}, vector {v.CounterAfter}");
            Assert.True(v.ItemAfter == driven.ItemInUse, $"{label}: item {driven.ItemInUse:X}, vector {v.ItemAfter:X}");
            freed += v.ItemAfter == 0 ? 1 : 0;
            kept += v.ItemAfter == 0 ? 0 : 1;
            saturated += v.Counter >= int.MaxValue - 1 ? 1 : 0;

            // The rocks born by the vector are taken out of the world: the next vector starts from a world with the hero alone.
            foreach (var rock in bench.Arc.Entities.Where(e => e.SpriteType == 16).ToList())
            {
                rock.Status = EntityStatus.FlagToDestroy;
            }

            bench.Arc.Proxy.RecycleDestroyedEntities();
        }

        Assert.True(freed > 5 && kept > 5, $"freed {freed}, kept {kept}"); // both outcomes are in the sample.
        Assert.True(saturated >= 2, $"saturated {saturated}"); // the counters 0x7FFFFFFE and 0x7FFFFFFF are in the sample.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d2) a refused birth writes nothing; (d3) the guard; (d4) the phase 2.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static EarthTome1Vectors.Driver Input(int phase, int counter, uint anim, int slope = 0, bool spawn = true, uint direction = 24)
        => new(phase, counter, anim, direction, 100, 100, 1, slope, spawn, 0, 0, 0, 0, 0, Array.Empty<EarthTome1Vectors.Spawn>(), Array.Empty<int>());

    [Fact]
    public void D2_ARefusedBirth_WritesNothing_AndIsNotDereferenced_WhileAnAnsweredOneGetsItsSpeedOfZ()
    {
        foreach (var (counter, speed) in new[] { (20, 1024), (55, 1024), (60, 1280), (95, 1280) })
        {
            var (refused, _) = World(Input(0, counter, Hold, spawn: false));
            var heroBefore = (refused.Hero.TargetAnimationId, refused.Hero.ItemInUse, refused.Hero.PosX, refused.Hero.PosZ, refused.Hero.ForceZ);

            var answer = EarthTome1Ai.Driver(refused.Hero);

            Assert.Equal(0, answer);
            Assert.Equal(2, refused.Host.SpawnCalls.Count); // the births are asked (two entries a tick) ...
            Assert.Empty(Rocks(refused)); // ... and none is given: nothing is written.
            Assert.Equal(heroBefore, (refused.Hero.TargetAnimationId, refused.Hero.ItemInUse, refused.Hero.PosX, refused.Hero.PosZ, refused.Hero.ForceZ)); // the speed of Z is not written on the hero either.

            var (answered, _) = World(Input(0, counter, Hold));
            EarthTome1Ai.Driver(answered.Hero);
            Assert.Equal(new[] { speed << 8, speed << 8 }, Rocks(answered).Select(b => b.ForceZ));
        }
    }

    [Fact]
    public void D3_TheGuard_CancelsAndAnswers1_OnTheSlope4OrOutsideTheCastAnimations_InThePhases0And1_AndAnswers0InTheCast()
    {
        foreach (var phase in new[] { 0, 1 })
        {
            foreach (var (anim, slope, cancels) in new[] { (Hold, 0, false), (Cast, 0, false), (Hold, 4, true), (Cast, 4, true), (End, 0, true), (0x31u, 0, true), (0u, 0, true), (0x32u + 2, 0, true) })
            {
                var (world, cancelled) = World(Input(phase, 0, anim, slope));

                var answer = EarthTome1Ai.Driver(world.Hero);

                Assert.True(answer == (cancels ? 1 : 0), $"phase {phase} animation {anim:X} slope {slope}: answer {answer}");
                Assert.True(cancels == (world.Hero.ItemInUse == 0), $"phase {phase} animation {anim:X} slope {slope}: item {world.Hero.ItemInUse:X}");
                Assert.Equal(cancels ? new[] { Tome } : Array.Empty<uint>(), cancelled); // the cancellation is the item's own (neutral), then the item is freed.
                Assert.Empty(world.Host.SpawnCalls);
                Assert.Equal(phase, world.Hero.ItemUsePhase);
            }
        }

        // Cast then hold: 0x33 alone is "nothing" and the cast 0x32 too; the end 0x34 is NOT accepted by the guard before the phase 2.
        var (ending, _) = World(Input(1, 100, End));
        Assert.Equal(1, EarthTome1Ai.Driver(ending.Hero));
        Assert.Equal(1, ending.Hero.ItemUsePhase); // no phase change on a cancelled spell.
        var (holding, _) = World(Input(1, 100, Hold));
        Assert.Equal(0, EarthTome1Ai.Driver(holding.Hero));
        Assert.Equal((2, End), (holding.Hero.ItemUsePhase, holding.Hero.TargetAnimationId));
    }

    [Fact]
    public void D4_ThePhase2_Answers0WhileTheAnimationIs0x34_AndIs_NotGuarded_NorFinishedWhenAnotherAnimationComesItAnswers1()
    {
        foreach (var counter in new[] { 0, 100, 101, 5000, int.MaxValue })
        {
            var (ending, cancelled) = World(Input(2, counter, End, slope: 4)); // not guarded: the slope 4 changes nothing in the phase 2.
            Assert.Equal(0, EarthTome1Ai.Driver(ending.Hero));
            Assert.Equal((Tome, 2, End), (ending.Hero.ItemInUse, ending.Hero.ItemUsePhase, ending.Hero.TargetAnimationId));
            Assert.Empty(cancelled);
            Assert.Empty(ending.Host.SpawnCalls);

            foreach (var other in new[] { 0u, Hold, Cast, 0x31u })
            {
                var (over, cancelledOver) = World(Input(2, counter, other));
                Assert.Equal(1, EarthTome1Ai.Driver(over.Hero));
                Assert.Equal((Tome, 2, other), (over.Hero.ItemInUse, over.Hero.ItemUsePhase, over.Hero.TargetAnimationId)); // the wrapper frees it, not the driver.
                Assert.Empty(cancelledOver);
            }
        }
    }

    [Fact]
    public void D5_TheCancellation_0x800351A0_IsNeutral_ItWritesNothingOnTheHero()
    {
        foreach (var (phase, counter, anim) in new[] { (0, 0, Hold), (0, 40, Cast), (1, 99, Hold), (2, 100, End) })
        {
            var (world, _) = World(Input(phase, counter, anim));
            var hero = world.Hero;
            var before = (hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, hero.TargetAnimationId, hero.TargetDirection, hero.PosX, hero.PosY, hero.PosZ, hero.ForceZ, hero.Flags);

            EarthTome1Ai.Cancel(hero);

            Assert.Equal(before, (hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, hero.TargetAnimationId, hero.TargetDirection, hero.PosX, hero.PosY, hero.PosZ, hero.ForceZ, hero.Flags));
            Assert.Empty(world.Host.SpawnCalls);
        }
    }

    [Fact]
    public void D4_ThePhaseChanges_At95And100_NotBefore()
    {
        foreach (var (counter, phaseAfter) in new[] { (19, 0), (20, 0), (94, 0), (95, 1), (96, 1), (150, 1) })
        {
            var (world, _) = World(Input(0, counter, Hold));
            Assert.Equal(0, EarthTome1Ai.Driver(world.Hero));
            Assert.True(phaseAfter == world.Hero.ItemUsePhase, $"phase 0, counter {counter}");
        }

        foreach (var (counter, phaseAfter, anim) in new[] { (0, 1, Hold), (98, 1, Hold), (99, 1, Hold), (100, 2, End), (101, 2, End), (150, 2, End) })
        {
            var (world, _) = World(Input(1, counter, Hold));
            Assert.Equal(0, EarthTome1Ai.Driver(world.Hero));
            Assert.True(phaseAfter == world.Hero.ItemUsePhase && anim == world.Hero.TargetAnimationId, $"phase 1, counter {counter}");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e1) E3 against the vectors; (e2) the registration; (c1) the neutral cancellation is in the world tests.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E1_E3_TheTwoHundredAndTwentyFourVectorsOfTheOracle_AreReplayed_TheDestructionAtTheEndOfTheAnimation_TheOthersSetTheAnimation1AndTheFlags()
    {
        var vectors = EarthTome1Vectors.Impacts();

        Assert.Equal(224, vectors.Count);
        foreach (var (v, index) in vectors.Select((v, i) => (v, i)))
        {
            var bench = BombHandsBench.Create(effectStage: false);
            var rock = bench.AddOther(16);
            rock.TargetAnimationId = v.TargetAnim;
            rock.ForceResetAnimationFlag = v.AnimEnd;
            rock.Flags = v.Flags;

            EarthTome1Ai.ImpactDeactivate(rock);

            Assert.True(v.TargetAnimAfter == rock.TargetAnimationId, $"vector {index}: animation {rock.TargetAnimationId}, vector {v.TargetAnimAfter}");
            Assert.True(v.FlagsAfter == rock.Flags, $"vector {index}: flags {rock.Flags:X}, vector {v.FlagsAfter:X}");
            Assert.True(v.AnimEndAfter == rock.ForceResetAnimationFlag, $"vector {index}: end of animation {rock.ForceResetAnimationFlag}");
            Assert.True(v.Destroyed == (bench.Host.Destroyed.Count == 1), $"vector {index}: destroyed {bench.Host.Destroyed.Count}");
            if (v.Destroyed)
            {
                Assert.Equal((rock, -1), bench.Host.Destroyed.Single()); // DestroyEntity(self, -1): the rock has no bursting effect, the argument is the executable's.
            }

            Assert.Empty(bench.Host.DestroyedAlone);
        }

        Assert.Equal(14, vectors.Count(v => v.Destroyed));
        Assert.Equal(210, vectors.Count(v => !v.Destroyed));
    }

    [Fact]
    public void E1_E3_NeedsNoHost_AndATouchOfTheAnimationAlone_OrTheEndAlone_DoesNotDestroy()
    {
        var alone = new EntityScriptProxy { SpriteType = 16, TargetAnimationId = 1, ForceResetAnimationFlag = 1, Flags = 0x132114 };
        EarthTome1Ai.ImpactDeactivate(alone); // no host: nothing to destroy with, and no exception.

        var bench = BombHandsBench.Create(effectStage: false);
        foreach (var (animation, end, destroys) in new[] { (1u, 1, true), (1u, 0, false), (0u, 1, false), (2u, 1, false), (1u, 2, false) })
        {
            var rock = bench.AddOther(16);
            rock.TargetAnimationId = animation;
            rock.ForceResetAnimationFlag = end;
            rock.Flags = 0x132114;

            EarthTome1Ai.ImpactDeactivate(rock);

            Assert.True(destroys == bench.Host.Destroyed.Any(d => ReferenceEquals(d.Entity, rock)), $"animation {animation} end {end}");
            Assert.Equal(destroys ? animation : 1u, rock.TargetAnimationId);
            Assert.Equal(destroys ? 0x132114u : 0x132144u, rock.Flags); // (flags | 0x40) & ~0x30: the deactivation by impact and by hit are off, the animation end is the only cause left.
        }
    }

    [Fact]
    public void E2_E3_IsRegisteredForTheTypes16And23Alone_NotThe17_NorThe24_NorAnyOtherType_AndTheRegistrationIsTakenOffByItsDisposable()
    {
        var registry = new NativeSpriteRegistry();

        using (EarthTome1Ai.Register(registry))
        {
            Assert.Equal(EarthTome1Ai.Keys.Count, registry.Count);
            Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 3) }, EarthTome1Ai.Keys);
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 16, out var handler));
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 23, out _)); // E20.h4d2 (D-H4D2-2): the fire balls of the fire tome 1 end on the same impact; one key, two types.
            Assert.Equal(new[] { 16, 23 }, EarthTome1Ai.SpriteTypes);
            foreach (var other in new[] { 0, 2, 15, 17, 24, 150, 153, 0x100 + 16, 0x100 + 23, 335 })
            {
                Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, other, out _), $"type {other}");
            }

            foreach (var slot in new[] { ScriptHelper.ProgramCTick, ScriptHelper.ProgramDTouch })
            {
                Assert.False(registry.TryGet(slot, 3, 16, out _), $"slot {slot}");
            }

            // The registered handler is E3: a rock at the end of its animation 1 is destroyed.
            var bench = BombHandsBench.Create(effectStage: false);
            var rock = bench.AddOther(16);
            rock.TargetAnimationId = 1;
            rock.ForceResetAnimationFlag = 1;
            handler!(rock, bench.Host, GameState.Instance);
            Assert.Single(bench.Host.Destroyed);
        }

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 16, out _));
    }
}
