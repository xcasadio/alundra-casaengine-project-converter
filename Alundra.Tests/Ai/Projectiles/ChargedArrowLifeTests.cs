#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Projectiles;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f4 phase A (docs/plan-e20-heros.md section 1.20.1, T1; (c4) to (c8)): the life of the charged arrow of William (sprite type 7) born by the TEST HOOK in the real world of map 14 on the synthetic field of the scenario (<see cref="ChargedArrowRun"/>), against the keys of the
/// oracle <c>docs/plan-e20-annexe/e20-f4f6</c> read by key (<see cref="ChargedArrowOracle"/>; the release is at tick 96 and the arrow is born at S = 105 in the bench as in the oracle). Nothing is born in production in this phase (the set of wired types does not hold 7).
/// (c4) the whole life against a wall at 1, 2 and 3 ticks per frame: the line of the arrow at every tick, the trail, the break effect, the sounds, the generator; (c5) the arrow goes through a target; (c6) a step of 16 and of 32 pixels and the edge of the map stop it;
/// (c7) the window of the wall over the 12 starts (D-F4-9); (c8) C94 calls the cut of the grass at every tick and the cut is that of the oracle. Each case without effect is set next to a positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChargedArrowLifeTests : IDisposable
{
    private const int Birth = ChargedArrowOracle.Birth;
    private const int TrailEffect = 25;
    private const int BreakEffect = 8;
    private const int CutEffect = 2;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public ChargedArrowLifeTests() => Reset();

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
    }

    private static void Reset()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        ChargedArrowAi.StubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The column 15 is a wall (the walk flag <c>0x40</c>) on every row: the arrow dies against it.</summary>
    private static CutBench.Cell WallColumn15(int column, int row) => column == 15 ? new CutBench.Cell(0x40, 0) : default;

    /// <summary>A strip of cuttable cells (the walkability bit <c>0x02</c>) in the columns 5 to 12 of the rows 5 and 6.</summary>
    private static CutBench.Cell GrassStrip(int column, int row) => column is >= 5 and <= 12 && row is 5 or 6 ? new CutBench.Cell(0x02, 0) : default;

    private static Func<int, int, CutBench.Cell> StepColumn8(int height) => (column, _) => column == 8 ? new CutBench.Cell(0, height) : default;

    private static string FxLine(ChargedArrowOracle.Effect e, int tick) => e.Line(tick);

    private static string FxLine((int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz) e) => $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})";

    /// <summary>The lines of the arrow of the test from S+<paramref name="from"/> to S+<paramref name="to"/> against the key from S+<paramref name="from"/> to S+<paramref name="to"/>: the position, the status, the force, the wall flag.</summary>
    private static void AssertLife(ChargedArrowOracle key, ChargedArrowRun run, int from, int to)
    {
        var expected = Enumerable.Range(from, to - from + 1).Select(k => $"{k,3}: {key.Line(Birth + k)}").ToList();
        var actual = Enumerable.Range(from, to - from + 1).Select(k => $"{k,3}: {run.Lines[run.BirthTick + k]}").ToList();
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c4) the whole life against the wall of the column 15, at 1, 2 and 3 ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void C4_TheWholeLifeAgainstTheWall_FromSToSPlus44_EqualsTheKey_TheTrailTheBreakEffectAndTheSoundsToo_WhateverTheGrouping(int ticksPerFrame)
    {
        var key = ChargedArrowOracle.Load("east_wall", stepByImage: true);
        using var run = new ChargedArrowRun("c4-" + ticksPerFrame, WallColumn15);

        run.Go(ticksPerFrame, 170);

        var s = run.BirthTick;
        Assert.InRange(s - Birth, 0, ticksPerFrame - 1); // 105 at one tick per frame; the frame of the release shifts the shot by at most the grouping.
        Assert.Equal(1, run.LowestFreeSlotBeforeBirth);
        Assert.Equal(1, run.BirthSlot);

        // The line of the arrow at every tick of S to S+44: born at S, the status 2 to S+42, the wall flag at S+42, the status 4 at S+43, absent at S+44.
        AssertLife(key, run, 0, 44);

        // The trail: id 25, the direction 3, y 100, Z 16, speed (-3, 0, 0), at S+4, S+8, ..., S+40, x = 124 + 24 (k - 1); created at the END of the frame that holds the tick.
        var trails = key.Effects(TrailEffect);
        Assert.Equal(10, trails.Count);
        Assert.Equal(
            trails.Select(e => FxLine(e, ChargedArrowRun.FrameEnd(s + e.Tick - Birth, ticksPerFrame))).ToList(),
            run.Effects.Where(e => e.Id == TrailEffect).Select(e => FxLine(e)).ToList());

        // The break effect: the effect 8 attached (mode 1) to the arrow at S+43; its anchor is the arrow where it died.
        var attached = Assert.Single(key.Attached(BreakEffect));
        Assert.Equal(1, attached.Mode);
        Assert.Equal(ChargedArrowRun.FrameEnd(s + attached.Tick - Birth, ticksPerFrame), run.BreakSeenTick);
        Assert.Equal((349, 100), run.BreakAnchor);
        Assert.Empty(run.Effects.Where(e => e.Id == BreakEffect)); // it is an attached effect, not a free one.

        // The sounds from S to S+44: the 33 of the birth at S and none else (the trail and the break effect are silent).
        Assert.Equal(
            key.Sounds(Birth, Birth + 44).Select(x => $"+{x.Tick - Birth} snd{x.Sfx}").ToList(),
            run.Heard.Played.Where(x => x.Tick >= s && x.Tick <= s + 44).Select(x => $"+{x.Tick - s} snd{x.Sfx}").ToList());
        Assert.Equal(new[] { "+0 snd33" }, key.Sounds(Birth, Birth + 44).Select(x => $"+{x.Tick - Birth} snd{x.Sfx}").ToArray());

        // The static values of the arrow at S: the flags 0x32054, the attack (131, 5), the box 24 x 16 x 15 at the Z 0.
        var first = key.Arrow(Birth)!.Value;
        Assert.Equal(
            $"flags=0x{first.GetProperty("flags6C").GetInt32():X} atk=({first.GetProperty("atk_attr").GetInt32()},{first.GetProperty("atk_pow").GetInt32()}) box=({first.GetProperty("bw").GetInt32()},{first.GetProperty("bd").GetInt32()},{first.GetProperty("bh").GetInt32()}) hbz={first.GetProperty("hbz").GetInt32() >> 16}",
            run.BirthStatics);
        Assert.Equal("flags=0x32054 atk=(131,5) box=(1572863,1048575,983039) hbz=0", run.BirthStatics);
    }

    [Fact]
    public void C4_TheGeneratorFollowsTheKeyWithTheStepByImage_NothingDrawsDuringTheLifeOfTheArrow_AtOneTickPerFrame()
    {
        var key = ChargedArrowOracle.Load("east_wall", stepByImage: true);
        using var run = new ChargedArrowRun("c4-rng", WallColumn15);

        run.Go(1, 170);

        // After every frame the state is the key's rng0 of the next tick (the step of the end of the frame is applied by the DLL) and the number of steps of the frame is the draws of the key plus that one.
        var wrong = new List<string>();
        for (var tick = 1; tick <= 169; tick++)
        {
            if (run.StateAfter[tick] != key.Rng0(tick + 1) || run.StepsDuring[tick] != key.Draws(tick) + 1)
            {
                wrong.Add($"t{tick}: state 0x{run.StateAfter[tick]:X8} (key 0x{key.Rng0(tick + 1):X8}), steps {run.StepsDuring[tick]} (key draws {key.Draws(tick)} + 1)");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));
        // The trail and the break effect draw nothing: from S to S+44 the key has no draw (the sparks of the charge are before S).
        Assert.All(Enumerable.Range(Birth, 45), tick => Assert.Equal(0, key.Draws(tick)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c5) the arrow goes through the target and dies at the wall.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C5_TheArrowTouchesTheTargetAtSPlus29Once_ThreeSparksNineDraws_GoesOnAndDiesAtTheWallAtSPlus43_TheSameArrowWithoutTargetTouchesNothing()
    {
        var key = ChargedArrowOracle.Load("vs_target", stepByImage: true);
        var targets = new List<int[]> { new[] { 300, 104 } };
        using (var run = new ChargedArrowRun("c5-target", WallColumn15, targets: targets))
        {
            run.Go(1, 170);

            var s = run.BirthTick;
            Assert.Equal(Birth, s);
            AssertLife(key, run, 0, 44);

            // The key: the hit counter of the arrow 1 at S+29 only, the cooldown 25 of the target then 24, 23... (the same target is not hit again within 25 ticks), three sparks at S+29 and nine draws.
            var expectedHits = Enumerable.Range(25, 10).Select(k =>
            {
                var arrow = key.Arrow(Birth + k)!.Value;
                var target = key.Slot(Birth + k, key.ArrowSlot == "1" ? "2" : "1")!.Value;
                var sparkSets = key.Effects(9).Count(e => e.Tick == Birth + k) / 3; // the pass asks for ONE set of three sparks per hit.
                return $"{k,3}: hit={arrow.GetProperty("hitc").GetInt32()} cooldown={target.GetProperty("c1d0").GetInt32()} sparkSets={sparkSets}";
            }).ToList();
            var actualHits = Enumerable.Range(25, 10).Select(k =>
            {
                var h = run.Hits[s + k];
                return $"{k,3}: hit={h.HitCounter} cooldown={h.TargetCooldown} sparkSets={h.Sparks}";
            }).ToList();
            Assert.Equal(string.Join("\n", expectedHits), string.Join("\n", actualHits));
            Assert.Equal(new[] { $"{29,3}: hit=1 cooldown=25 sparkSets=1" }, actualHits.Where(l => l.Contains("hit=1", StringComparison.Ordinal)).ToArray());

            Assert.Equal(10, run.StepsDuring[s + 29]); // 9 draws of the three sparks, plus the step of the end of the frame.
            Assert.Equal(9, key.Draws(Birth + 29));
        }

        // The same arrow with no target in the world: nothing is touched, no spark, no draw, the same death.
        using (var alone = new ChargedArrowRun("c5-alone", WallColumn15))
        {
            alone.Go(1, 170);

            Assert.All(Enumerable.Range(0, 45), k => Assert.Equal((0, 0, 0, false), alone.Hits[alone.BirthTick + k]));
            Assert.Equal(1, alone.StepsDuring[alone.BirthTick + 29]);
            AssertLife(ChargedArrowOracle.Load("east_wall", stepByImage: true), alone, 0, 44);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c6) a step of 16 and of 32 pixels and the edge of the map stop the arrow; the free corridor and a start far from the edge do not.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("step16", 1)]
    [InlineData("step32", 2)]
    public void C6_AStepOfTheColumn8_StopsTheArrow_TheFlagAtSPlus14AtX181_ItIsDestroyedAtSPlus15_TheFreeCorridorLetsItGoOn(string name, int height)
    {
        var key = ChargedArrowOracle.Load(name);
        using (var run = new ChargedArrowRun("c6-" + name, StepColumn8(height)))
        {
            run.Go(1, 130);

            Assert.Equal(Birth, run.BirthTick);
            AssertLife(key, run, 0, 16);
            Assert.Contains("adj=1", run.Lines[run.BirthTick + 14], StringComparison.Ordinal);
            Assert.Contains("x=" + (181 << 16), run.Lines[run.BirthTick + 14], StringComparison.Ordinal);
            Assert.Equal("absent", run.Lines[run.BirthTick + 16]);
        }

        // The free corridor: the same arrow with no step is at S+14 at 106 + 6 x 14 = 190, not blocked, and still alive at S+16.
        using (var free = new ChargedArrowRun("c6-free-" + name, null))
        {
            free.Go(1, 130);

            Assert.Equal($"st=2 x={190 << 16} y={100 << 16} zpx=8 fx=393216 adj=0", free.Lines[free.BirthTick + 14]);
            Assert.StartsWith("st=2 ", free.Lines[free.BirthTick + 16], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void C6_TheEdgeOfTheMap_RaisesTheFlagAtSPlus14AtX1237_ADepartureFarFromTheEdgeIsNotBoundedThen()
    {
        var key = ChargedArrowOracle.Load("map_edge");
        using (var edge = new ChargedArrowRun("c6-edge", null, heroX: 1150))
        {
            edge.Go(1, 130);

            Assert.Equal(Birth, edge.BirthTick);
            AssertLife(key, edge, 0, 17);
            Assert.Equal($"st=2 x={1237 << 16} y={100 << 16} zpx=8 fx=393216 adj=1", edge.Lines[edge.BirthTick + 14]);
            Assert.Equal("absent", edge.Lines[edge.BirthTick + 16]);
        }

        using (var far = new ChargedArrowRun("c6-far", null, heroX: 1100))
        {
            far.Go(1, 130);

            Assert.Equal($"st=2 x={1190 << 16} y={100 << 16} zpx=8 fx=393216 adj=0", far.Lines[far.BirthTick + 14]);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c7) the window of the wall over the 12 starts of the hero (D-F4-9, Q-K2 default A): the flag comes one tick after the arrival at the wall; the key's equals it for 8 starts and is one tick later for the 4 others.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C7_TheFlagOfTheWall_ComesOneTickAfterTheArrival_OnTheTwelveStarts_TheKeyEqualsItFor8StartsAndIsOneTickLaterFor4()
    {
        var keys = ChargedArrowOracle.WallStarts();
        Assert.Equal(12, keys.Count);
        var late = new[] { 98, 101, 104, 107 };
        var report = new List<string>();
        foreach (var x0 in Enumerable.Range(96, 12))
        {
            using var run = new ChargedArrowRun("c7-" + x0, WallColumn15, heroX: x0);
            run.Go(1, 160);

            var s = run.BirthTick;
            var arrival = (349 - x0 - 6 + 5) / 6; // the first tick k at which x0 + 6 + 6 k reaches 349 (the arrow stops at 349).
            var flag = Enumerable.Range(0, 60).First(k => run.Lines[s + k].EndsWith("adj=1", StringComparison.Ordinal));
            var status4 = Enumerable.Range(0, 60).First(k => run.Lines[s + k].StartsWith("st=4", StringComparison.Ordinal));
            var xAtFlag = run.Lines[s + flag];
            var expectedKeyFlag = flag + (late.Contains(x0) ? 1 : 0);
            report.Add($"x0={x0}: S={s} flag S+{flag} (arrival S+{arrival}) status4 S+{status4} key S+{keys[x0].Flag}/S+{keys[x0].Status4}");
            Assert.True(
                s == Birth && flag == arrival + 1 && status4 == flag + 1 && keys[x0].Flag == expectedKeyFlag && keys[x0].Status4 == expectedKeyFlag + 1 && xAtFlag.Contains($"x={349 << 16} ", StringComparison.Ordinal),
                string.Join(Environment.NewLine, report));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c8) the cut of the grass: C94 calls the stub (and the port) at every tick, the cut is the key's, the generator follows it.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void C8_TheArrowCutsTheGrassOnItsWay_TheCellsTheEffectsTheSoundsAndTheCallsAreTheKeys_NothingIsCutOnAFieldWithoutGrass(int ticksPerFrame)
    {
        var key = ChargedArrowOracle.Load("grass", stepByImage: true);
        const int lastTick = 140;
        using (var run = new ChargedArrowRun("c8-" + ticksPerFrame, GrassStrip))
        {
            run.Go(ticksPerFrame, lastTick);

            var s = run.BirthTick;
            var cuts = key.Effects(CutEffect);
            Assert.Equal(16, cuts.Count);
            Assert.Equal(
                cuts.Select(e => FxLine(e, ChargedArrowRun.FrameEnd(s + e.Tick - Birth, ticksPerFrame))).ToList(),
                run.Effects.Where(e => e.Id == CutEffect).Select(e => FxLine(e)).ToList());

            // The sounds from S to S+30: the 33, then the 31 of each cut, at the ticks of the key. The object a cut can drop is born in the DLL (its sprite is in the world) and plays the 84; the machine of the oracle holds only the records of the projectiles,
            // so its spawn fails without a sound: the 84 is not in the key, but the DRAWS tell it (a cut draws one word, or two when an object appears): the 84s of a tick are the draws of the key beyond one per cut.
            Assert.Equal(
                key.Sounds(Birth, Birth + 30).Select(x => $"+{x.Tick - Birth} snd{x.Sfx}").ToList(),
                run.Heard.Played.Where(x => x.Tick >= s && x.Tick <= s + 30 && x.Sfx != 84).Select(x => $"+{x.Tick - s} snd{x.Sfx}").ToList());
            Assert.Equal(16, run.Heard.Played.Count(x => x.Sfx == 31));
            if (ticksPerFrame == 1)
            {
                // The values drawn are those of the key at one tick per frame only (at 2 and 3 the immediate draws of the next ticks do not see the intermediate steps of the generator, the documented deviation of ADR-0066).
                var objectsOfTheKey = cuts.GroupBy(c => c.Tick).Select(g => $"+{g.Key - Birth}: {key.Draws(g.Key) - g.Count()}").ToList();
                var objectsHere = cuts.GroupBy(c => c.Tick).Select(g => $"+{g.Key - Birth}: {run.Heard.Played.Count(x => x.Sfx == 84 && x.Tick == s + g.Key - Birth)}").ToList();
                Assert.Equal(objectsOfTheKey, objectsHere);
                Assert.Contains(objectsOfTheKey, line => !line.EndsWith(": 0", StringComparison.Ordinal)); // the key does drop an object (so the positive case of the 84 exists).
            }

            // The cells: the 16 cells the key cuts have lost the bit 0x02, nothing else changed (the strip had 16).
            var cutCells = cuts.Select(e => (Column: (int)(double.Parse(e.X, System.Globalization.CultureInfo.InvariantCulture) / 24), Row: (int)(double.Parse(e.Y, System.Globalization.CultureInfo.InvariantCulture) / 16))).Distinct().ToList();
            Assert.Equal(16, cutCells.Count);
            Assert.All(cutCells, c => Assert.Equal(0, run.Bench.Read(c.Column, c.Row).Walkability & CellStore.CuttableWalkabilityBit));
            Assert.Equal(0, Enumerable.Range(0, CutBench.Width * CutBench.Height).Count(i => (run.Bench.Read(i % CutBench.Width, i / CutBench.Width).Walkability & CellStore.CuttableWalkabilityBit) != 0));

            // The stub (and the port after it) is called once at every tick from S+1: the journal holds the arrow at each of them, never at S.
            Assert.Equal(Enumerable.Range(s + 1, run.LastTick - s).ToList(), run.StubCallTicks);
        }

        // At one tick per frame the generator follows the key with the step by image (the loot of each cut draws one or two words by the value of the first).
        using (var one = new ChargedArrowRun("c8-rng", GrassStrip))
        {
            one.Go(1, 134);

            var wrong = Enumerable.Range(1, 133).Where(t => one.StateAfter[t] != key.Rng0(t + 1) || one.StepsDuring[t] != key.Draws(t) + 1)
                .Select(t => $"t{t}: state 0x{one.StateAfter[t]:X8} (key 0x{key.Rng0(t + 1):X8}), steps {one.StepsDuring[t]} (key draws {key.Draws(t)} + 1)").ToList();
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));
        }

        // The same arrow on a field without grass: the stub is called all the same, nothing is cut, no effect 2, no sound 31.
        using (var bare = new ChargedArrowRun("c8-bare-" + ticksPerFrame, null))
        {
            bare.Go(ticksPerFrame, lastTick);

            Assert.Equal(Enumerable.Range(bare.BirthTick + 1, bare.LastTick - bare.BirthTick).ToList(), bare.StubCallTicks);
            Assert.Empty(bare.Effects.Where(e => e.Id == CutEffect));
            Assert.DoesNotContain(bare.Heard.Played, x => x.Sfx == 31);
        }
    }
}
