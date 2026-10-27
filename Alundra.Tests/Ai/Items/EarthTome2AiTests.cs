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
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1, phase A (docs/plan-e20-heros.md section 1.31.4, tests (t1) to (t6), (e1) to (e7), (c1), (r1)): the table, the driver <c>0x800354D0</c>, E13 (<c>0x8007F30C</c>, slot E) and the cancellation <c>0x80034BDC</c> of the earth tome 2, on a hero and rocks made by hand
/// and on a registry of their own (the registry of production is not touched in the phase A). The expected values are the cases of the driver and the vectors of E13 of the real code of the binary, replayed field by field, and hand cases next to them: each case without effect is
/// set next to a case of the same front that acts. The cases whose terrain under the newborn is not representable by a field (<c>eq</c>, <c>lt</c>: the height at 256 px is a <c>float</c> of spacing 2 raw units) are excluded by their name and covered by (t4) and (w8).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome2AiTests : IDisposable
{
    private const uint Tome = 0x2C;
    private const uint Hold = PlayerManager.TomeHoldAnimationId;
    private const uint Cast = PlayerManager.TomeCastAnimationId;
    private const uint End = PlayerManager.TomeEndAnimationId;
    private const int Rock = 17;

    public EarthTome2AiTests() => Reset();

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

    [Fact]
    public void T1_TheTable_IsTheOneOfTheExecutable_15EntriesOfU16S16_AndTheSentinel()
    {
        var expected = Convert.FromHexString(File.ReadAllText(AnnexFiles.EarthTome2Annex("t1/table_80023504.hex")).Trim());

        Assert.Equal(64, expected.Length);
        Assert.Equal(expected, EarthTome2Ai.TableBytes);
        Assert.Equal(0xFFFF, expected[60] | (expected[61] << 8)); // the sentinel is the executable's.

        var decoded = new List<(int Tick, int P)>();
        for (var i = 0; i < 15; i++)
        {
            decoded.Add((expected[4 * i] | (expected[4 * i + 1] << 8), (short)(expected[4 * i + 2] | (expected[4 * i + 3] << 8))));
        }

        Assert.Equal(decoded, EarthTome2Ai.Table);
        Assert.Equal(new[] { 16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 224, 240 }, EarthTome2Ai.Table.Select(e => e.Tick)); // two entries at the tick 224.
        Assert.Equal(Enumerable.Range(1, 15).Select(i => 512 * i), EarthTome2Ai.Table.Select(e => e.P));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t2) the driver against the cases of the oracle.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (BombHandsBench Bench, List<EntityScriptProxy> Others, List<uint> Cancelled) World(EarthTome2Vectors.DriverCase v)
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var hero = bench.Hero;
        hero.PosX = v.HeroX;
        hero.PosY = v.HeroY;
        hero.PosZ = v.HeroZRaw - 1; // the DLL's Z is the binary's minus 1.
        hero.TargetAnimationId = v.HeroAnimation;
        hero.Slope_18c = v.Slope;
        hero.ItemInUse = Tome;
        hero.ItemUsePhase = v.Phase;
        hero.ItemUseCounter = v.Counter;
        bench.Host.RefuseCall = call => call < v.Spawns.Count && v.Spawns[call] == "nul";
        GameRandom.RandomSeed = v.Seed;
        var others = new List<EntityScriptProxy>();
        foreach (var other in v.Others)
        {
            var entity = bench.AddOther(other.Type);
            entity.Status = (EntityStatus)other.Status;
            entity.BlockedByEntity = hero.LogicContextEntity; // +0x20 non-zero: an entity blocks it.
            others.Add(entity);
        }

        var cancelled = new List<uint>();
        PlayerManager.ItemCancelForTests = (h, item) =>
        {
            cancelled.Add(item);
            EarthTome2Ai.Cancel(h);
        };
        return (bench, others, cancelled);
    }

    private static List<EntityScriptProxy> Rocks(BombHandsBench bench) => bench.Host.Born.Where(b => b.SpriteType == Rock).ToList();

    private static void AssertDriver(EarthTome2Vectors.DriverCase v)
    {
        var label = $"case {v.Index} (phase {v.Phase} counter {v.Counter} animation {v.HeroAnimation:X} slope {v.Slope} spawns [{string.Join(",", v.Spawns)}])";
        var (bench, others, cancelled) = World(v);
        var hero = bench.Hero;

        var answer = EarthTome2Ai.Driver(hero);

        Assert.True(v.Return == answer, $"{label}: answer {answer}, case {v.Return}");
        Assert.True(v.PhaseAfter == hero.ItemUsePhase, $"{label}: phase {hero.ItemUsePhase}, case {v.PhaseAfter}");
        Assert.True(v.HeroAnimationAfter == hero.TargetAnimationId, $"{label}: animation {hero.TargetAnimationId:X}, case {v.HeroAnimationAfter:X}");
        Assert.True(v.SeedAfter == unchecked((uint)GameRandom.RandomSeed), $"{label}: generator {unchecked((uint)GameRandom.RandomSeed)}, case {v.SeedAfter}"); // two draws a birth, refused or not.

        // The driver frees the item only through the guard (the cancellation of the item, then the answer 1); the wrapper frees it when the answer is non-zero and increments the counter (the tick's, tested by D1_TheCounterAndTheRelease...).
        if (v.Phase != 2)
        {
            Assert.True(v.ItemAfter == hero.ItemInUse, $"{label}: item {hero.ItemInUse:X}, case {v.ItemAfter:X}");
            Assert.Equal(v.ItemAfter == 0 ? new[] { Tome } : Array.Empty<uint>(), cancelled);
        }
        else
        {
            Assert.Equal(Tome, hero.ItemInUse);
            Assert.Empty(cancelled);
        }

        // The births asked, refused or not: SpawnWarpEntity(hero, global page, 17, x, y, z, direction); the Z of the case is the binary's.
        Assert.Equal(v.Births.Count, bench.Host.SpawnCalls.Count);
        for (var k = 0; k < v.Births.Count; k++)
        {
            var call = bench.Host.SpawnCalls[k];
            var birth = v.Births[k];
            Assert.True((hero, false, birth.Type, birth.X, birth.Y, birth.Z - 1, birth.Direction) == call, $"{label}: birth {k} is {call}, case {birth}");
        }

        // The entities the world gave (the pool, prepared by the stub for every accepted birth, the unused ones untouched): destroyed with the one-argument destroy (status 4) or given the force -8 px a tick, in the order of the births.
        var rocks = Rocks(bench).Where(r => !others.Contains(r)).ToList(); // the entities of the case (which may be of the type 17 too) are not births.
        var accepted = v.Spawns.Take(v.Births.Count).Count(s => s == "ok");
        Assert.Equal(accepted, rocks.Count);
        Assert.All(v.Pool.Skip(accepted), p => Assert.Equal((1, 0u), (p.Status, p.ForceZ)));
        for (var k = 0; k < rocks.Count; k++)
        {
            var destroyed = bench.Host.DestroyedAlone.Contains(rocks[k]);
            Assert.True((v.Pool[k].Status == 4) == destroyed, $"{label}: rock {k} destroyed {destroyed}, case status {v.Pool[k].Status}");
            Assert.True(v.Pool[k].ForceZ == unchecked((uint)rocks[k].ForceZ), $"{label}: rock {k} force {unchecked((uint)rocks[k].ForceZ):X}, case {v.Pool[k].ForceZ:X}");
        }

        // The other entities of the world after the guard's cancellation (statuses 2 of the type 17 go to 3, nothing else moves).
        Assert.Equal(v.OthersAfter.Count, others.Count);
        for (var k = 0; k < others.Count; k++)
        {
            var after = v.OthersAfter[k];
            Assert.True((after.Status, after.B20 == 0, (uint)after.Animation) == ((int)others[k].Status, others[k].BlockedByEntity is null, others[k].TargetAnimationId), $"{label}: entity {k} status {others[k].Status} blocked {others[k].BlockedByEntity is not null} animation {others[k].TargetAnimationId}, case {after}");
        }
    }

    [Fact]
    public void T2_Driver_TheCasesOfTheOracleWhoseTerrainIsRepresentable_AreReplayedField_AnswerPhaseAnimationGeneratorBirthsDestructionAndForceOfZ()
    {
        var cases = EarthTome2Vectors.DriverCases();

        Assert.Equal(609, cases.Count);
        var excluded = cases.Where(c => c.Terrain is "eq" or "lt").ToList(); // not representable by a field: covered by (t4) and (w8).
        Assert.Equal(5, excluded.Count);
        var replayed = cases.Where(c => c.Terrain == "lo").ToList();
        Assert.Equal(604, replayed.Count);
        foreach (var c in replayed)
        {
            AssertDriver(c);
            PlayerManager.ItemCancelForTests = null;
        }

        // Both sides of every front are in the sample: births and none, two births at the tick 224, a refused birth that still draws, the guard acting and not, the three phases.
        Assert.Contains(replayed, c => c.Births.Count == 1 && c.Pool.Count == 1 && c.Pool[0].ForceZ == 0xFFF80000u);
        Assert.Contains(replayed, c => c.Births.Count == 2 && c.Pool.Count == 2);
        Assert.Contains(replayed, c => c.Spawns.Contains("nul") && c.Births.Count >= 1); // a refused birth: the draws are made, nothing is written.
        Assert.Contains(replayed, c => c is { Phase: 0, PhaseAfter: 1 });
        Assert.Contains(replayed, c => c is { Phase: 1, PhaseAfter: 2 });
        Assert.Contains(replayed, c => c is { Phase: 2, Return: 1 });
        Assert.Contains(replayed, c => c is { Return: 1, Phase: < 2 } && c.OthersAfter.Any(o => o.B20 == 0));
        Assert.DoesNotContain(replayed, c => c.Pool.Any(p => p.Status == 4)); // no destruction at a terrain of 0.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t2) the counter and the release, by the path of the tick: the wrapper 0x8003634C increments the counter (saturated) after the driver and frees the item when the driver answers non-zero.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T2_TheCounterAndTheRelease_ByThePathOfTheTick_AreTheWrapperOfTheOracle_TheCounterSaturatesAtTheLargestPositiveWord()
    {
        var cases = EarthTome2Vectors.DriverCases()
            .Where(c => c.Terrain == "lo" && c.Slope == 0 && c.HeroAnimation is Cast or Hold or End or 0 && !c.Spawns.Contains("nul"))
            .GroupBy(c => (c.Phase, c.Counter, c.HeroAnimation))
            .Select(g => g.First())
            .ToList();

        Assert.True(cases.Count > 30, cases.Count.ToString());
        using var bench = new TomeBench("h4c2-d1-tick");
        PlayerManager.ItemStartForTests = (_, _) => 0;
        PlayerManager.ItemDriverForTests = (hero, _) => EarthTome2Ai.Driver(hero);
        PlayerManager.ItemCancelForTests = (hero, _) => EarthTome2Ai.Cancel(hero);
        var driven = bench.Hero;
        var freed = 0;
        var kept = 0;
        foreach (var c in cases)
        {
            driven.ItemInUse = Tome;
            driven.ItemUsePhase = c.Phase;
            driven.ItemUseCounter = c.Counter;
            driven.TargetAnimationId = c.HeroAnimation;

            bench.Tick();

            var label = $"phase {c.Phase} counter {c.Counter} animation {c.HeroAnimation:X}";
            Assert.True(c.CounterAfter == driven.ItemUseCounter, $"{label}: counter {driven.ItemUseCounter}, case {c.CounterAfter}");
            Assert.True(c.ItemAfter == driven.ItemInUse, $"{label}: item {driven.ItemInUse:X}, case {c.ItemAfter:X}");
            freed += c.ItemAfter == 0 ? 1 : 0;
            kept += c.ItemAfter == 0 ? 0 : 1;

            // The rocks born by the case are taken out of the world: the next case starts from a world with the hero alone.
            foreach (var rock in bench.Arc.Entities.Where(e => e.SpriteType == Rock).ToList())
            {
                rock.Status = EntityStatus.FlagToDestroy;
            }

            bench.Arc.Proxy.RecycleDestroyedEntities();
        }

        Assert.True(freed > 5 && kept > 5, $"freed {freed}, kept {kept}"); // both outcomes are in the sample.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t3) a refused birth draws all the same and destroys nothing; (t4) the comparison of the birth; (t5) the thresholds; (t6) the guard.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static EarthTome2Vectors.DriverCase Input(int phase, int counter, uint animation, int slope = 0, IReadOnlyList<string>? spawns = null, uint direction = 24)
        => new(0, phase, counter, animation, slope, 100 << 16, 100 << 16, 1, 0x12345678, spawns ?? Array.Empty<string>(), "lo", Array.Empty<EarthTome2Vectors.Other>(), 0, 0, 0, 0, 0, 0,
            Array.Empty<EarthTome2Vectors.Birth>(), Array.Empty<EarthTome2Vectors.Born>(), Array.Empty<EarthTome2Vectors.OtherAfter>());

    [Fact]
    public void T3_ARefusedBirth_DrawsTheTwoNumbersAllTheSame_AndDestroysNothingNorWritesAForce_WhileAnAnsweredOneIsGivenTheForceOf8PixelsATick()
    {
        foreach (var counter in new[] { 16, 112, 224, 240 })
        {
            var (refused, _, _) = World(Input(0, counter, Hold, spawns: new[] { "nul", "nul" }));
            var seed = unchecked((uint)GameRandom.RandomSeed);
            var heroBefore = (refused.Hero.TargetAnimationId, refused.Hero.ItemInUse, refused.Hero.PosX, refused.Hero.PosZ, refused.Hero.ForceZ);

            var answer = EarthTome2Ai.Driver(refused.Hero);

            var births = counter == 224 ? 2 : 1;
            Assert.Equal(0, answer);
            Assert.Equal(births, refused.Host.SpawnCalls.Count); // the births are asked ...
            Assert.Empty(Rocks(refused)); // ... and none is given: nothing is destroyed, nothing is written.
            Assert.Empty(refused.Host.DestroyedAlone);
            Assert.Equal(2 * births, EarthTome2Run.Steps(seed, unchecked((uint)GameRandom.RandomSeed))); // the two draws are made before the birth, whatever its result.
            Assert.Equal(heroBefore, (refused.Hero.TargetAnimationId, refused.Hero.ItemInUse, refused.Hero.PosX, refused.Hero.PosZ, refused.Hero.ForceZ));

            var (answered, _, _) = World(Input(0, counter, Hold));
            seed = unchecked((uint)GameRandom.RandomSeed);
            EarthTome2Ai.Driver(answered.Hero);
            Assert.Equal(2 * births, EarthTome2Run.Steps(seed, unchecked((uint)GameRandom.RandomSeed)));
            Assert.Equal(Enumerable.Repeat(unchecked((int)0xFFF80000), births), Rocks(answered).Select(b => b.ForceZ));
        }
    }

    [Fact]
    public void T4_TheDestructionAtTheBirth_IsTheSignedStrictComparisonOfTheTerrainWithTheZOfTheHeroPlus1PlusHalfAMeter_TheEqualityKeeps()
    {
        const int Above = 0x1000000; // 256 px: the height of the birth above the hero.

        // The comparison is heroPosZ + 1 + 0x1000000 < terrain (the binary's Z is the DLL's plus 1).
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(0, Above + 1)); // equality keeps ...
        Assert.True(EarthTome2Ai.ShouldDestroyAtBirth(0, Above + 2)); // ... one raw unit more destroys.
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(0, Above)); // one unit less keeps: the +1 is in the comparison.
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(0, 0));
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(0, -Above)); // a terrain far below.
        Assert.True(EarthTome2Ai.ShouldDestroyAtBirth(0, 17_825_792)); // the plateau of the byte 17 (272 px) above the hero at the ground.
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(0, 16_777_216)); // the plateau of the byte 16 (256 px) is the birth height: kept.
        Assert.False(EarthTome2Ai.ShouldDestroyAtBirth(1 << 20, 17_825_792)); // the hero 16 px higher: the 272 px plateau is under the birth.
        Assert.True(EarthTome2Ai.ShouldDestroyAtBirth((1 << 20) - 1, 17_825_792 + 1)); // ... until the plateau is 2 raw units above it (the DLL Z is one less).
        Assert.True(EarthTome2Ai.ShouldDestroyAtBirth(int.MaxValue, 0)); // signed 32-bit wrap: the sum is negative, below the terrain 0.
    }

    [Fact]
    public void T5_Thresholds_TwoRocksAtTheCounter224_ThePhase1AtTheCounter240InTheSameCall_0x117Against0x118_ThePhase2()
    {
        // Two entries at the tick 224: two births in the same call, in the order of the table (p = 6656 then 7168).
        var (two, _, _) = World(Input(0, 224, Hold));
        Assert.Equal(0, EarthTome2Ai.Driver(two.Hero));
        Assert.Equal(2, two.Host.SpawnCalls.Count);
        Assert.Equal(0, two.Hero.ItemUsePhase);

        // The last entry (240) gives its rock and the phase 1 in the same call; the counter 239 gives neither a rock nor the phase; a counter past the table gives the phase 1 alone.
        var (last, _, _) = World(Input(0, 240, Hold));
        Assert.Equal(0, EarthTome2Ai.Driver(last.Hero));
        Assert.Equal((1, 1), (last.Host.SpawnCalls.Count, last.Hero.ItemUsePhase));
        var (before, _, _) = World(Input(0, 239, Hold));
        EarthTome2Ai.Driver(before.Hero);
        Assert.Equal((0, 0), (before.Host.SpawnCalls.Count, before.Hero.ItemUsePhase));
        var (after, _, _) = World(Input(0, 241, Hold));
        EarthTome2Ai.Driver(after.Hero);
        Assert.Equal((0, 1), (after.Host.SpawnCalls.Count, after.Hero.ItemUsePhase));

        // Every entry fires at its own tick and at no other.
        foreach (var tick in Enumerable.Range(0, 250))
        {
            var (world, _, _) = World(Input(0, tick, Hold));
            EarthTome2Ai.Driver(world.Hero);
            Assert.Equal(EarthTome2Ai.Table.Count(e => e.Tick == tick), world.Host.SpawnCalls.Count);
        }

        // The phase 1 ends the cast at 0x118 and not before; the phase 2 is not guarded and answers 0 while the animation is 0x34.
        foreach (var (counter, phaseAfter, animation) in new[] { (0, 1, Hold), (0x117, 1, Hold), (0x118, 2, End), (0x119, 2, End), (400, 2, End) })
        {
            var (world, _, _) = World(Input(1, counter, Hold));
            Assert.Equal(0, EarthTome2Ai.Driver(world.Hero));
            Assert.True((phaseAfter, animation) == (world.Hero.ItemUsePhase, world.Hero.TargetAnimationId), $"phase 1, counter {counter}");
            Assert.Empty(world.Host.SpawnCalls);
        }

        foreach (var counter in new[] { 0, 0x118, 5000, int.MaxValue })
        {
            var (ending, _, cancelled) = World(Input(2, counter, End, slope: 4));
            Assert.Equal(0, EarthTome2Ai.Driver(ending.Hero));
            Assert.Equal((Tome, 2, End), (ending.Hero.ItemInUse, ending.Hero.ItemUsePhase, ending.Hero.TargetAnimationId));
            Assert.Empty(cancelled);
            Assert.Empty(ending.Host.SpawnCalls);

            foreach (var other in new[] { 0u, Hold, Cast, 0x31u })
            {
                var (over, _, cancelledOver) = World(Input(2, counter, other));
                Assert.Equal(1, EarthTome2Ai.Driver(over.Hero));
                Assert.Equal((Tome, 2, other), (over.Hero.ItemInUse, over.Hero.ItemUsePhase, over.Hero.TargetAnimationId)); // the wrapper frees it, not the driver.
                Assert.Empty(cancelledOver);
            }
        }
    }

    [Fact]
    public void T6_TheGuard_CancelsAndAnswers1_OnTheSlope4OrOutsideTheCastAnimations_InThePhases0And1_AndAnswers0InTheCast()
    {
        foreach (var phase in new[] { 0, 1 })
        {
            foreach (var (animation, slope, cancels) in new[] { (Hold, 0, false), (Cast, 0, false), (Hold, 4, true), (Cast, 4, true), (End, 0, true), (0x31u, 0, true), (0u, 0, true), (0x32u + 2, 0, true) })
            {
                var (world, _, cancelled) = World(Input(phase, 0, animation, slope));

                var answer = EarthTome2Ai.Driver(world.Hero);

                Assert.True(answer == (cancels ? 1 : 0), $"phase {phase} animation {animation:X} slope {slope}: answer {answer}");
                Assert.True(cancels == (world.Hero.ItemInUse == 0), $"phase {phase} animation {animation:X} slope {slope}: item {world.Hero.ItemInUse:X}");
                Assert.Equal(cancels ? new[] { Tome } : Array.Empty<uint>(), cancelled); // the cancellation of the item (0x80034BDC), then the item is freed.
                Assert.Empty(world.Host.SpawnCalls);
                Assert.Equal(phase, world.Hero.ItemUsePhase);
            }
        }

        // The end 0x34 is NOT accepted by the guard before the phase 2: a cast that reaches the counter 0x118 in the animation 0x34 is cancelled.
        var (ending, _, _) = World(Input(1, 0x118, End));
        Assert.Equal(1, EarthTome2Ai.Driver(ending.Hero));
        Assert.Equal(1, ending.Hero.ItemUsePhase); // no phase change on a cancelled spell.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e1) E13 against the vectors of the oracle; (e2) to (e7) the hand cases.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const long BaseOfTheEntity = 2148699964;
    private const long FlagsWord = BaseOfTheEntity + 0x6C;
    private const long AnimationWord = BaseOfTheEntity + 0x88;
    private const long EndWord = BaseOfTheEntity + 0xAC;
    private const long KickYWord = 0x800DD874;

    [Fact]
    public void E1_E13_TheFortyVectorsOfTheOracle_AreReplayed_TheFirstPassesOnTheGround_TheKickIsAssigned2_TheSecondPassesDestroyAtTheEndOfTheAnimation()
    {
        var vectors = EarthTome2Vectors.Vectors("vec_e13_8007F30C.json");

        Assert.Equal(40, vectors.Count);
        foreach (var v in vectors)
        {
            var bench = BombHandsBench.Create(effectStage: false);
            var rock = bench.AddOther(Rock);
            rock.Status = EntityStatus.Normal;
            rock.IsOnGround = 1; // revision 3: the first passes are replayed on the ground; none of the vectors reads the ground word.
            rock.TargetAnimationId = (uint)v.Read(AnimationWord);
            rock.Flags = (uint)v.Read(FlagsWord);
            rock.ForceResetAnimationFlag = v.ReadInt(EndWord);
            var flagsBefore = rock.Flags;
            var firstPass = v.Read(AnimationWord) != 1;

            EarthTome2Ai.ImpactDeactivate(rock);

            var label = $"vector {v.Id}";
            Assert.True(v.Wrote(AnimationWord) ? (uint)v.Writes[AnimationWord] == rock.TargetAnimationId : (uint)v.Read(AnimationWord) == rock.TargetAnimationId, $"{label}: animation {rock.TargetAnimationId}");
            Assert.True(v.Wrote(FlagsWord) ? (uint)v.Writes[FlagsWord] == rock.Flags : flagsBefore == rock.Flags, $"{label}: flags {rock.Flags:X}");
            Assert.Equal(v.JournalDestroy ? EntityStatus.FlagToDestroy : EntityStatus.Normal, rock.Status); // the world's destruction sets the status; E13 itself writes none.
            Assert.Equal(v.Wrote(KickYWord) ? new[] { (int)v.Writes[KickYWord] } : Array.Empty<int>(), bench.Host.CameraKicks); // [0x800DD874] = 2, assigned.
            Assert.Equal(v.JournalDestroy ? new[] { (rock, -1) } : Array.Empty<(EntityScriptProxy, int)>(), bench.Host.Destroyed); // DestroyEntity(self, -1).
            Assert.Empty(bench.Host.DestroyedAlone);
            Assert.True(firstPass == v.Wrote(KickYWord), $"{label}: the kick is the first pass's alone");
        }

        Assert.Equal(20, vectors.Count(v => v.Read(AnimationWord) != 1));
        Assert.Equal(20, vectors.Count(v => v.Wrote(KickYWord)));
        Assert.Contains(vectors, v => v.JournalDestroy);
        Assert.Contains(vectors, v => v.Read(AnimationWord) == 1 && !v.JournalDestroy);
    }

    private static (BombHandsBench Bench, EntityScriptProxy Rock) Landed(EntityStatus status = EntityStatus.Normal)
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var rock = bench.AddOther(Rock);
        rock.Status = status;
        rock.IsOnGround = 1;
        rock.TargetAnimationId = 0;
        rock.Flags = 0x132014;
        rock.ForceResetAnimationFlag = 0;
        return (bench, rock);
    }

    [Fact]
    public void E2_E13_TheFirstPassOnTheGround_AsksTheAnimation1_SetsTheFlag0x40_ClearsTheBits0x30_AndAssignsTheKick2()
    {
        var (bench, rock) = Landed();

        EarthTome2Ai.ImpactDeactivate(rock);

        Assert.Equal(1u, rock.TargetAnimationId);
        Assert.Equal(0x132044u, rock.Flags); // (0x132014 | 0x40) & ~0x30.
        Assert.Equal(new[] { 2 }, bench.Host.CameraKicks);
        Assert.Empty(bench.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, rock.Status);

        // A second rock at the same tick: its own kick, 2 again (the world assigns the byte: (k2) checks it is not added).
        var second = bench.AddOther(Rock);
        second.IsOnGround = 1;
        second.Flags = 0x132014;
        EarthTome2Ai.ImpactDeactivate(second);
        Assert.Equal(new[] { 2, 2 }, bench.Host.CameraKicks);
    }

    [Fact]
    public void E3_E13_TheSecondPass_DestroysAtTheEndOfTheAnimation1Alone_WithTheAnimationAndTheFlagsUntouched_OnTheGroundOrInTheAir()
    {
        foreach (var onGround in new[] { 1, 0 })
        {
            foreach (var (end, destroys) in new[] { (0, false), (1, true), (2, false) })
            {
                var (bench, rock) = Landed(EntityStatus.Deactivated);
                rock.IsOnGround = onGround;
                rock.TargetAnimationId = 1;
                rock.ForceResetAnimationFlag = end;
                rock.Flags = 0x132044;

                EarthTome2Ai.ImpactDeactivate(rock);

                Assert.True(destroys == bench.Host.Destroyed.Any(d => d.Entity == rock && d.EffectId == -1), $"ground {onGround} end {end}");
                Assert.Equal((1u, 0x132044u, destroys ? EntityStatus.FlagToDestroy : EntityStatus.Deactivated), (rock.TargetAnimationId, rock.Flags, rock.Status)); // E13 changes nothing; the world's destruction sets the status.
                Assert.Empty(bench.Host.CameraKicks); // the second pass has no guard of the ground and no kick.
            }
        }
    }

    [Fact]
    public void E4_E13_ARockAlreadyInTheAnimation1AtItsFirstView_ACancelledRock_GivesNeitherKickNorFlags()
    {
        var (bench, rock) = Landed(EntityStatus.Deactivated);
        rock.TargetAnimationId = 1; // asked by the cancellation 0x80034BDC.
        var flags = rock.Flags;

        EarthTome2Ai.ImpactDeactivate(rock);

        Assert.Empty(bench.Host.CameraKicks);
        Assert.Equal(flags, rock.Flags);
        Assert.Empty(bench.Host.Destroyed);
        Assert.Equal(EntityStatus.Deactivated, rock.Status);
    }

    [Fact]
    public void E5_E13_WithoutAWorldContext_DoesTheRockAndNothingElse_NoKickNoException()
    {
        var alone = new EntityScriptProxy { SpriteType = Rock, IsOnGround = 1, Flags = 0x132014 };

        EarthTome2Ai.ImpactDeactivate(alone); // no host: no kick to give.

        Assert.Equal((1u, 0x132044u), (alone.TargetAnimationId, alone.Flags));

        var second = new EntityScriptProxy { SpriteType = Rock, TargetAnimationId = 1, ForceResetAnimationFlag = 1 };
        EarthTome2Ai.ImpactDeactivate(second); // no host: nothing to destroy with.
        Assert.Equal(1u, second.TargetAnimationId);
    }

    [Fact]
    public void E6_E13_TheFirstPassInTheAir_PutsTheStatusBackTo2_AndWritesNothingElse_NoAnimationNoFlagsNoKick()
    {
        foreach (var adjusted in new[] { 0, 1 })
        {
            var (bench, rock) = Landed(EntityStatus.Deactivated); // the pass of the events put it at the status 3 (held in the air).
            rock.IsOnGround = 0;
            rock.ForceAdjusted = adjusted;
            var before = (rock.TargetAnimationId, rock.Flags, rock.ForceResetAnimationFlag, rock.PosX, rock.PosY, rock.PosZ, rock.ForceZ);

            EarthTome2Ai.ImpactDeactivate(rock);

            Assert.Equal(EntityStatus.Normal, rock.Status);
            Assert.Equal(before, (rock.TargetAnimationId, rock.Flags, rock.ForceResetAnimationFlag, rock.PosX, rock.PosY, rock.PosZ, rock.ForceZ));
            Assert.Empty(bench.Host.CameraKicks);
            Assert.Empty(bench.Host.Destroyed);

            // Next to it, the same rock on the ground acts.
            rock.IsOnGround = 1;
            EarthTome2Ai.ImpactDeactivate(rock);
            Assert.Equal(1u, rock.TargetAnimationId);
            Assert.Equal(new[] { 2 }, bench.Host.CameraKicks);
        }
    }

    [Fact]
    public void E7_E13_OnTheGroundWithTheFlagOfTheWallRaised_TheImpactHappens_TheGuardReadsTheGroundAndNotTheFlag()
    {
        var (bench, rock) = Landed(EntityStatus.Deactivated);
        rock.IsOnGround = 1;
        rock.ForceAdjusted = 1;

        EarthTome2Ai.ImpactDeactivate(rock);

        Assert.Equal((1u, 0x132044u), (rock.TargetAnimationId, rock.Flags));
        Assert.Equal(new[] { 2 }, bench.Host.CameraKicks);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) the cancellation 0x80034BDC.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C1_TheCancellation_PutsTheRocksAtTheStatus2AtTheStatus3_WithNothingBlockingThemAndTheAnimation1_AndTouchesNothingElse()
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var hero = bench.Hero;
        EntityScriptProxy Add(int type, EntityStatus status)
        {
            var e = bench.AddOther(type);
            e.Status = status;
            e.BlockedByEntity = hero.LogicContextEntity;
            e.Flags = 0x132014;
            return e;
        }

        var inTheAir = Add(Rock, EntityStatus.Normal);
        var loaded = Add(Rock, EntityStatus.Loaded);
        var deactivated = Add(Rock, EntityStatus.Deactivated);
        var dying = Add(Rock, EntityStatus.FlagToDestroy);
        var block = Add(25, EntityStatus.Normal); // a target of another type.
        var beannoid = Add(0x100 + 79, EntityStatus.Normal);
        var highest = Add(Rock, EntityStatus.Normal); // the last of the table: the highest slot reached is cancelled too.
        var untouched = new[] { loaded, deactivated, dying, block, beannoid }.Select(e => (e, e.Status, e.BlockedByEntity, e.TargetAnimationId, e.Flags)).ToList();
        var heroBefore = (hero.Status, hero.TargetAnimationId, hero.Flags, hero.ItemInUse, hero.ForceZ, hero.PosX);

        EarthTome2Ai.Cancel(hero);

        foreach (var rock in new[] { inTheAir, highest })
        {
            Assert.Equal((EntityStatus.Deactivated, true, 1u, 0x132014u), (rock.Status, rock.BlockedByEntity is null, rock.TargetAnimationId, rock.Flags)); // +0x10 = 3, +0x20 = 0, +0x88 = 1, nothing else.
        }

        Assert.Equal(untouched, new[] { loaded, deactivated, dying, block, beannoid }.Select(e => (e, e.Status, e.BlockedByEntity, e.TargetAnimationId, e.Flags)).ToList());
        Assert.Equal(heroBefore, (hero.Status, hero.TargetAnimationId, hero.Flags, hero.ItemInUse, hero.ForceZ, hero.PosX)); // the cancellation writes nothing on the hero.
        Assert.Empty(bench.Host.Destroyed);
        Assert.Empty(bench.Host.DestroyedAlone);
        Assert.Empty(bench.Host.SpawnCalls);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (r1) the registration.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void R1_E13_IsRegisteredForTheType17Alone_NotThe469_NorThe16_NorAnyOtherType_AndTheRegistrationIsTakenOffByItsDisposable()
    {
        var registry = new NativeSpriteRegistry();

        using (EarthTome2Ai.Register(registry))
        {
            Assert.Equal(EarthTome2Ai.Keys.Count, registry.Count);
            Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 13) }, EarthTome2Ai.Keys);
            Assert.Equal(new[] { 17 }, EarthTome2Ai.SpriteTypes);
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 13, 17, out var handler));
            foreach (var other in new[] { 0, 2, 15, 16, 18, 23, 150, 153, 0x100 + 16, 335, 469 }) // 469: the sprite of the record 1 of the map 321, which also runs E13 in the original: inert here.
            {
                Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 13, other, out _), $"type {other}");
            }

            foreach (var slot in new[] { ScriptHelper.ProgramCTick, ScriptHelper.ProgramDTouch })
            {
                Assert.False(registry.TryGet(slot, 13, 17, out _), $"slot {slot}");
            }

            // The registered handler is E13: a rock at the end of its animation 1 is destroyed.
            var bench = BombHandsBench.Create(effectStage: false);
            var rock = bench.AddOther(Rock);
            rock.TargetAnimationId = 1;
            rock.ForceResetAnimationFlag = 1;
            handler!(rock, bench.Host, GameState.Instance);
            Assert.Single(bench.Host.Destroyed);
        }

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 13, 17, out _));
    }
}
