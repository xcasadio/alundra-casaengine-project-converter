#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f5 phase A (docs/plan-e20-heros.md section 1.21.1, T1; (e6) and (e7)): the life of the normal arrows (sprite types 3 and 4) born by the TEST HOOK in the real world of map 14 on the synthetic field of the scenario (<see cref="ArrowRun"/>), against the keys of the oracle
/// <c>docs/plan-e20-annexe/e20-f4f6</c> read by key (<see cref="ArrowOracle"/>; the press is at tick P = 1 and the arrow is born at S = P + 9 = 10 in the bench as in the oracle). Nothing is born in production in this phase (the set of wired types holds neither 3 nor 4).
/// (e7) the whole life against a wall and the edges of the map, at 1, 2 and 3 ticks per frame (which also proves the rule of the speed key, (e6): the bounce and the series of X equal the oracle's whatever the grouping), the 12 starts of the wall, the touches (a high target, the reach of 16
/// and 17, the real sprite 136, two targets, a target in a lower slot than the arrow, a target destroyed in the same tour, an invulnerable target, the balloon switch, the filters applied after the touch), the shots in the air. Each case without effect is set next to a positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ArrowLifeTests : IDisposable
{
    private const int Birth = ArrowOracle.Birth;
    private const uint Cross = PadState.Cross;
    private const uint Square = PadState.Square;
    private const int Px = 1 << 16;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public ArrowLifeTests() => Reset();

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
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static Func<int, int, CutBench.Cell> Column(int column) => (c, r) => c == column ? new CutBench.Cell(0x40, 0) : default;

    private static Func<int, int, CutBench.Cell> Row(int row) => (c, r) => r == row ? new CutBench.Cell(0x40, 0) : default;

    private static readonly int[] TargetAt300x104 = { 300, 104 };

    /// <summary>The oracle patches the body height of its Beannoid (SizeZ of its map-14 record) to this many pixels; the DLL's body is the header's one, so the test patches the body of the entity it uses the same way (the depth is the size minus one raw unit).</summary>
    private static void PatchBodyHeight(EntityScriptProxy target, int pixels) => target.Depth = (pixels * Px) - 1;

    /// <summary>The lines of the arrow from S+<paramref name="from"/> to S+<paramref name="to"/> against the key: the state, the position, the forces, the wall flag, the direction and the animation, the flags, the end-of-animation flag.</summary>
    private static void AssertLife(ArrowOracle key, ArrowRun run, int from, int to)
    {
        var expected = Enumerable.Range(from, to - from + 1).Select(k => $"{k,3}: {key.Line(key.FirstTick + k)}").ToList();
        var actual = Enumerable.Range(from, to - from + 1).Select(k => $"{k,3}: {run.Line(run.BirthTick + k)}").ToList();
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
    }

    /// <summary>The number of ticks of the key counted from the birth of the arrow (the last tick of the key is S + this).</summary>
    private static int KeyLength(ArrowOracle key) => key.Ticks - key.FirstTick;

    /// <summary>After every frame (one tick per frame) the state of the generator is the key's <c>rng0</c> of the next tick and the steps of the frame are the key's draws plus the step of the end of the frame.</summary>
    private static void AssertGenerator(ArrowOracle key, ArrowRun run)
    {
        var wrong = new List<string>();
        for (var tick = 1; tick < key.Ticks; tick++)
        {
            if (run.StateAfter[tick] != key.Rng0(tick + 1) || run.StepsDuring[tick] != key.Draws(tick) + 1)
            {
                wrong.Add($"t{tick}: state 0x{run.StateAfter[tick]:X8} (key 0x{key.Rng0(tick + 1):X8}), steps {run.StepsDuring[tick]} (key draws {key.Draws(tick)} + 1)");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e7) the whole life against the wall of the column 15, at 1, 2 and 3 ticks per frame, for the hunter's arrow (3) and for William's (4).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, "arrow3_east_wall", 1)]
    [InlineData(5, "arrow3_east_wall", 2)]
    [InlineData(5, "arrow3_east_wall", 3)]
    [InlineData(6, "arrow4_east_wall", 1)]
    [InlineData(6, "arrow4_east_wall", 2)]
    [InlineData(6, "arrow4_east_wall", 3)]
    public void E7_TheWholeLifeAgainstTheWall_FromSToSPlus61_EqualsTheKey_TheBounceAndTheSeriesOfXToo_WhateverTheGrouping(int item, string name, int ticksPerFrame)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun("e7-wall-" + name + ticksPerFrame, item, Column(15));

        run.Go(ticksPerFrame, 80);

        var s = run.BirthTick;
        Assert.Equal(Birth, s);
        Assert.Equal(1, run.LowestFreeSlotBeforeBirth);
        Assert.Equal(1, run.BirthSlot);
        Assert.Equal(item == 5 ? 3 : 4, run.Born!.SpriteType);

        // The line of the arrow at every tick of S to S+62: the status 2 from S to S+42 (the wall flag at S+42), 3 from S+43 (the bounce) to S+59, 4 at S+60, absent from S+61.
        AssertLife(key, run, 0, 62);
        Assert.Equal("absent", run.Line(s + 61));
        Assert.Equal(
            (EntityStatus.Normal, EntityStatus.Deactivated, EntityStatus.Deactivated, EntityStatus.FlagToDestroy),
            (run.Ticks[s + 42].Status, run.Ticks[s + 43].Status, run.Ticks[s + 59].Status, run.Ticks[s + 60].Status));
        Assert.Equal(1, run.Ticks[s + 42].Adjusted);

        // The table of the plan, by value (the key is read as it is; these are its numbers): X 103 at S, 109 at S+1, 349 at S+41 and S+42, 347.125 at S+43 and -1.875 per tick down to 317.125 at S+59.
        Assert.Equal(
            new[] { 103 * Px, 109 * Px, 349 * Px, 349 * Px, (347 * Px) + (Px / 8), (317 * Px) + (Px / 8) },
            new[] { run.Ticks[s].X, run.Ticks[s + 1].X, run.Ticks[s + 41].X, run.Ticks[s + 42].X, run.Ticks[s + 43].X, run.Ticks[s + 59].X });
        // Z in the DLL's frame (the binary's minus one raw unit, ADR-0026: 16 pixels plus a unit at rest): 16 px to S+42, then the hop of 16 ticks (S+43 to S+59).
        var hop = new[] { 20.0, 23.5, 26.5, 29, 31, 32.5, 33.5, 34, 34, 33.5, 32.5, 31, 29, 26.5, 23.5, 20, 16 };
        Assert.All(Enumerable.Range(0, 43), k => Assert.Equal((16 * Px) + 1, run.Ticks[s + k].Z));
        Assert.Equal(hop.Select(h => ((int)(h * Px)) + 1).ToList(), Enumerable.Range(43, 17).Select(k => run.Ticks[s + k].Z).ToList());
        Assert.Equal(new[] { 196608, 393216, -122880 }, new[] { run.Ticks[s].ForceX, run.Ticks[s + 42].ForceX, run.Ticks[s + 43].ForceX });
        Assert.Equal((24u, 0u, 0x12074u), (run.Ticks[s + 42].Direction, run.Ticks[s + 42].Animation, run.Ticks[s + 42].Flags));
        Assert.Equal((8u, 1u, 0x12144u), (run.Ticks[s + 43].Direction, run.Ticks[s + 43].Animation, run.Ticks[s + 43].Flags));
        Assert.Equal(1, run.Ticks[s + 59].ResetFlag);

        // The statics of the arrow at its birth: the flags 0x12074, the attack (131, power 1 or 2), the box 24 x 16 x 11 at the Z 16.
        Assert.Equal($"flags=0x12074 atk=(131,{(item == 5 ? 1 : 2)}) box=(1572863,1048575,720895) hbz=16", run.BirthStatics);

        // The sound 34 of the animation 1 at the bounce (the tick S+43), and no other from S; no break effect (the break effect of the header is 0).
        Assert.Equal(new[] { (43, 34) }, key.Sounds(Birth, Birth + 62).Select(x => (x.Tick - Birth, x.Sfx)).ToArray());
        Assert.Equal(new[] { (43, 34) }, run.Heard.Played.Where(x => x.Tick >= s && x.Tick <= s + 62).Select(x => (x.Tick - s, x.Sfx)).ToArray()); // heard at the tick that sees the switch, whatever the grouping.
        Assert.Equal(0, run.Born.BreakEffect);
        Assert.Equal(0, run.AnchoredEffectsSeen);
        Assert.Empty(key.Attached());

        // The bounce changes neither the walkability mask of the controller nor its gravity (the mask reads the classes only, the gravity of the controller is always null here).
        Assert.NotEmpty(run.ControllerAfter);
        Assert.Single(run.ControllerAfter.Values.Distinct());
    }

    [Fact]
    public void E7_TheGeneratorFollowsTheKeyWithTheStepByImage_NothingDrawsDuringTheLifeAgainstTheWall_AtOneTickPerFrame()
    {
        foreach (var (item, name) in new[] { (5, "arrow3_east_wall"), (6, "arrow4_east_wall") })
        {
            var key = ArrowOracle.Load(name, stepByImage: true);
            using var run = new ArrowRun("e7-rng-" + name, item, Column(15));

            run.Go(1, key.Ticks + 1);

            AssertGenerator(key, run);
            Assert.All(Enumerable.Range(Birth, 62), tick => Assert.Equal(0, key.Draws(tick)));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e7) the other walls, the edges of the map, the free flights: each key, at 1 and 3 ticks per frame. (The key arrow3_point_blank, a wall 12 pixels from the hero, is not here: the DLL does not clip the step of the birth tick against a wall closer than a step, the
    // arrow is born inside it at x = 111 where the oracle holds it at 109; outside the handler E17, reported in docs/plan-e20-annexe/e20-f4f6/f5/t1/t1-results.md.)
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Func<int, int, CutBench.Cell>? WallOf(string wall) => wall switch
    {
        "col2" => Column(2),
        "row2" => Row(2),
        "row12" => Row(12),
        _ => null,
    };

    public static IEnumerable<object[]> Paths()
    {
        yield return new object[] { "arrow3_wall_west", 5, "col2", 100, 100, 8u };
        yield return new object[] { "arrow3_wall_north", 5, "row2", 100, 100, 16u };
        yield return new object[] { "arrow3_wall_south", 5, "row12", 100, 100, 0u };
        yield return new object[] { "arrow3_map_edge", 5, "", 1150, 100, 24u };
        yield return new object[] { "arrow3_map_edge_south", 5, "", 100, 900, 0u };
        yield return new object[] { "arrow3_free_north", 5, "", 100, 100, 16u };
        yield return new object[] { "arrow3_free_west", 5, "", 100, 100, 8u };
        yield return new object[] { "arrow3_free_east", 5, "", 100, 100, 24u };
        yield return new object[] { "arrow3_free_south", 5, "", 100, 100, 0u };
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public void E7_TheOtherWallsAndTheEdgesOfTheMap_EqualTheKey_AtOneTickPerFrame(string name, int item, string wall, int heroX, int heroY, uint direction)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun("e7-path-" + name, item, WallOf(wall), heroX, heroY, direction);

        run.Go(1, key.Ticks + 1);

        Assert.Equal(Birth, run.BirthTick);
        AssertLife(key, run, 0, KeyLength(key));
        AssertGenerator(key, run);
        Assert.Equal(0, run.Hits.Values.Count(h => h.HitCounter != 0 || h.Sparks != 0 || h.Touching)); // the bench is empty: nothing is touched on the way.
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public void E7_TheOtherWallsAndTheEdgesOfTheMap_EqualTheKey_AtThreeTicksPerFrame(string name, int item, string wall, int heroX, int heroY, uint direction)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun("e7-path3-" + name, item, WallOf(wall), heroX, heroY, direction);

        run.Go(3, key.Ticks + 3);

        Assert.Equal(Birth, run.BirthTick);
        AssertLife(key, run, 0, KeyLength(key));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e7) the 12 starts of the wall: the flag comes one tick after the arrival at the wall; the key equals it for 8 starts and is one tick later for the 4 others (D-F4-9, D-F5-7).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E7_TheFlagOfTheWall_ComesOneTickAfterTheArrival_OnTheTwelveStarts_TheKeyEqualsItFor8StartsAndIsOneTickLaterFor4_TheArrowBouncesTheTickAfterTheFlag()
    {
        var keys = ArrowOracle.WallStarts();
        Assert.Equal(12, keys.Count);
        var late = new[] { 98, 101, 104, 107 };
        var report = new List<string>();
        foreach (var x0 in Enumerable.Range(96, 12))
        {
            using var run = new ArrowRun("e7-start-" + x0, 5, Column(15), heroX: x0);
            run.Go(1, 80);

            var s = run.BirthTick;
            var arrival = (349 - x0 - 3 + 5) / 6; // the first tick k at which x0 + 3 + 6 k reaches 349 (the arrow stops at 349).
            var flag = Enumerable.Range(0, 60).First(k => run.Ticks[s + k].Adjusted != 0);
            var bounce = Enumerable.Range(0, 60).First(k => run.Ticks[s + k].Animation == 1);
            var expectedKeyFlag = flag + (late.Contains(x0) ? 1 : 0);
            report.Add($"x0={x0}: S={s} flag S+{flag} (arrival S+{arrival}) bounce S+{bounce} key S+{keys[x0]}");
            Assert.True(
                s == Birth && flag == arrival + 1 && bounce == flag + 1 && keys[x0] == expectedKeyFlag && run.Ticks[s + flag].X == 349 * Px,
                string.Join(Environment.NewLine, report));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e7) the touches.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static ArrowRun WithTarget(string name, int item, ArrowOracle key, int bodyHeight, Func<int, int, CutBench.Cell>? cell = null, int[]? at = null)
    {
        var run = new ArrowRun(name, item, cell, targets: new List<int[]> { at ?? TargetAt300x104 }, generatorAtTick1: key.Rng0(1));
        PatchBodyHeight(run.Arena.Targets[0], bodyHeight);
        return run;
    }

    [Theory]
    [InlineData(5, "arrow3_target")]
    [InlineData(6, "arrow4_target")]
    public void E7_AHighTarget_IsTouchedOnceAtSPlus30_ThreeSparksNineDraws_TheArrowDiesAtSPlus31_TheSlotIsFreeAtSPlus32_TheSameArrowWithoutTargetTouchesNothing(int item, string name)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using (var run = WithTarget("e7-" + name, item, key, bodyHeight: 32))
        {
            run.Go(1, key.Ticks + 1);

            var s = run.BirthTick;
            Assert.Equal(Birth, s);
            AssertLife(key, run, 0, KeyLength(key));
            AssertGenerator(key, run);

            // The key: the hit counter of the arrow 1 at S+30 and after, the cooldown 25 of the target then 24, 23... , three sparks at S+30 and nine draws; the arrow is touched once (a set of three sparks at S+30 only).
            var expected = Enumerable.Range(25, 10).Select(k =>
            {
                var arrow = key.Arrow(Birth + k);
                var target = key.Target(Birth + k)!.Value;
                var sparkSets = key.EffectTicks(9).Where(t => t.Tick == Birth + k).Sum(t => t.Count) / 3;
                return $"{k,3}: hit={(arrow is null ? 0 : arrow.Value.GetProperty("hitc").GetInt32())} cooldown={target.GetProperty("c1d0").GetInt32()} sparkSets={sparkSets}";
            }).ToList();
            var actual = Enumerable.Range(25, 10).Select(k =>
            {
                var h = run.Hits[s + k];
                return $"{k,3}: hit={h.HitCounter} cooldown={h.TargetCooldown} sparkSets={h.Sparks}";
            }).ToList();
            Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
            Assert.Equal(new[] { 30 }, run.Hits.Where(h => h.Value.Sparks > 0).Select(h => h.Key - s).ToArray());
            Assert.Equal(new[] { 30 }, run.TargetTouchedTicks.Select(t => t - s).ToArray());
            Assert.Equal(10, run.StepsDuring[s + 30]); // nine draws of the three sparks plus the step of the end of the frame.
            Assert.Equal(9, key.Draws(Birth + 30));

            // The death: the status 4 at S+31 (E17 found the target), the slot free at S+32; the sparks and the touch leave the break effect empty.
            Assert.Equal((EntityStatus.Normal, EntityStatus.FlagToDestroy, "absent"), (run.Ticks[s + 30].Status, run.Ticks[s + 31].Status, run.Line(s + 32)));
            Assert.Equal(0, run.AnchoredEffectsSeen);
        }

        // The same arrow with no target in the world: nothing is touched, no spark, no draw, and it flies on to the end of the key (the free flight of the key arrow3_free_east).
        var freeKey = ArrowOracle.Load("arrow3_free_east", stepByImage: true);
        using var alone = new ArrowRun("e7-alone-" + name, item, null);
        alone.Go(1, freeKey.Ticks + 1);
        Assert.All(Enumerable.Range(0, 31), k => Assert.Equal((0, 0, 0, false), alone.Hits[alone.BirthTick + k]));
        Assert.Equal(1, alone.StepsDuring[alone.BirthTick + 30]);
        AssertLife(freeKey, alone, 0, KeyLength(freeKey));
    }

    [Fact]
    public void E7_TheReachInZ_ABodyOf16IsMissed_ABodyOf17IsTouched_TheRealBodyOfTheBeannoidIsMissed_TheRealSprite136IsTouched()
    {
        foreach (var (name, height, touched) in new[] { ("arrow3_zreach_16", 16, false), ("arrow3_zreach_17", 17, true) })
        {
            var key = ArrowOracle.Load(name, stepByImage: true);
            using var run = WithTarget("e7-" + name, 5, key, height);
            run.Go(1, key.Ticks + 1);

            AssertLife(key, run, 0, KeyLength(key));
            AssertGenerator(key, run);
            Assert.Equal(touched ? new[] { 30 } : Array.Empty<int>(), run.TargetTouchedTicks.Select(t => t - run.BirthTick).ToArray());
            Assert.Equal(touched, run.Line(run.BirthTick + 32) == "absent");
        }

        // The real Beannoid (a body of 8): the arrow flies 16 pixels above it.
        var lowKey = ArrowOracle.Load("arrow3_target_low", stepByImage: true);
        using (var low = new ArrowRun("e7-low", 5, null, targets: new List<int[]> { TargetAt300x104 }, generatorAtTick1: lowKey.Rng0(1)))
        {
            low.Go(1, lowKey.Ticks + 1);
            AssertLife(lowKey, low, 0, KeyLength(lowKey));
            Assert.Empty(low.TargetTouchedTicks);
        }

        // The real sprite 136 of the map (a header body of 14 x 16 x 17, its hurt box 9 x 9 x 5 in the animation 3): touched at S+30, the arrow dies at S+31.
        var key136 = ArrowOracle.Load("arrow3_target_sprite136", stepByImage: true);
        using var run136 = new ArrowRun("e7-136", 5, null, alsoKeep: e => e.SpriteType == 0x100 + 136, generatorAtTick1: key136.Rng0(1));
        var sprite = run136.Kept!;
        HitArena.Place(sprite, 300, 104);
        sprite.TargetAnimationId = 3;
        run136.Go(1, key136.Ticks + 1);
        AssertLife(key136, run136, 0, KeyLength(key136));
        Assert.Equal((17 * Px) - 1, sprite.Depth);
        Assert.Equal(new[] { 30 }, run136.Hits.Where(h => h.Value.HitCounter != 0).Select(h => h.Key - run136.BirthTick).Take(1).ToArray());
        Assert.Equal("absent", run136.Line(run136.BirthTick + 32));
    }

    [Fact]
    public void E7_ATargetMadeInvulnerableAfterTheTouch_IsNotFound_TheArrowGoesBackToTheStatus2AndFliesOn_TheSameArrowWithoutTheChangeDies()
    {
        foreach (var (name, change) in new (string, Action<EntityScriptProxy>)[]
        {
            ("arrow3_target_invulnerable_after_hit", t => t.DamagedTickCounter = 0x78),           // the opcode 0x86 or a handler of the target (E14)
            ("arrow3_target_filter_b4", t => t.HitAnimByte |= 0x40),                              // the animation byte untouchable
            ("arrow3_target_filter_class", t => t.Flags &= ~1u),                                  // the class bit the arrow's mask tests
        })
        {
            var key = ArrowOracle.Load(name, stepByImage: true);
            using var run = WithTarget("e7-" + name, 5, key, bodyHeight: 32);
            var done = false;
            run.AfterEachTick = _ =>
            {
                if (!done && run.Born is { HitCounter: not 0 })
                {
                    change(run.Arena.Targets[0]);
                    done = true;
                }
            };
            run.Go(1, key.Ticks + 1);

            Assert.True(done);
            AssertLife(key, run, 0, KeyLength(key));
            Assert.All(Enumerable.Range(30, KeyLength(key) - 30 + 1), k => Assert.Equal(EntityStatus.Normal, run.Ticks[run.BirthTick + k].Status)); // flies on at the status 2.
        }

        // Next to them: the unchanged target kills the arrow at S+31.
        var plain = ArrowOracle.Load("arrow3_target", stepByImage: true);
        using var dying = WithTarget("e7-inv-plain", 5, plain, bodyHeight: 32);
        dying.Go(1, plain.Ticks + 1);
        Assert.Equal("absent", dying.Line(dying.BirthTick + 32));
    }

    [Fact]
    public void E7_TheBalloonSwitch_Type0x1AD_IsTouchedOnce_ButNeverFound_TheArrowFliesThrough_TheSameTargetOfAnotherTypeKillsIt()
    {
        var key = ArrowOracle.Load("arrow3_target_1AD", stepByImage: true);
        using (var run = WithTarget("e7-1ad", 5, key, bodyHeight: 32))
        {
            run.Arena.Targets[0].SpriteType = 0x1AD;
            run.Go(1, key.Ticks + 1);

            AssertLife(key, run, 0, KeyLength(key));
            Assert.Equal(new[] { 30 }, run.TargetTouchedTicks.Select(t => t - run.BirthTick).ToArray());
            Assert.All(Enumerable.Range(30, KeyLength(key) - 30 + 1), k => Assert.Equal(EntityStatus.Normal, run.Ticks[run.BirthTick + k].Status));
        }

        var other = ArrowOracle.Load("arrow3_target", stepByImage: true);
        using var killed = WithTarget("e7-1ad-other", 5, other, bodyHeight: 32);
        killed.Arena.Targets[0].SpriteType = 0x1AC;
        killed.Go(1, other.Ticks + 1);
        Assert.Equal("absent", killed.Line(killed.BirthTick + 32));
    }

    [Fact]
    public void E7_TwoTargets_AreTouchedAtTheSameTick_EighteenDraws_OnlyOneDeath()
    {
        var key = ArrowOracle.Load("arrow3_two_targets", stepByImage: true);
        using var run = new ArrowRun("e7-two", 5, null, targets: new List<int[]> { TargetAt300x104, new[] { 300, 96 } }, generatorAtTick1: key.Rng0(1));
        run.Arena.Targets.ForEach(t => PatchBodyHeight(t, 32));
        var cooldowns = new Dictionary<int, int>();
        run.AfterEachTick = tick => cooldowns[tick] = run.Arena.Targets[1].FrameCollisionTickCounter;

        run.Go(1, key.Ticks + 1);

        var s = run.BirthTick;
        AssertLife(key, run, 0, KeyLength(key));
        AssertGenerator(key, run);
        Assert.Equal((25, 25), (run.Hits[s + 30].TargetCooldown, cooldowns[s + 30]));
        Assert.Equal(19, run.StepsDuring[s + 30]); // eighteen draws plus the step of the end of the frame.
        Assert.Equal(18, key.Draws(Birth + 30));
        Assert.Equal("absent", run.Line(s + 32));
    }

    [Fact]
    public void E7_ATargetInALowerSlotThanItsArrowIsFoundToo_AndTheDOfTheTargetStillReadsTheArrow_ATargetDestroyedInTheSameTourIsStillFound()
    {
        // The arrow in the slot 1 (the first target freed before the shot), the target in the slot 2: the last slot of the scan.
        var key = ArrowOracle.Load("arrow3_target_higher_slot", stepByImage: true);
        using (var run = new ArrowRun("e7-higher", 5, null, targets: new List<int[]> { new[] { 300, 56 }, TargetAt300x104 }, generatorAtTick1: key.Rng0(1)))
        {
            var target = run.Arena.Targets[1];
            run.Recycle(run.Arena.Targets[0]);
            PatchBodyHeight(target, 32);
            var attacks = new Dictionary<int, (int Attribute, int Power)?>();
            var afterDeath = new Dictionary<int, (EntityStatus Status, int Attribute, int Power)>();
            run.AfterEachTick = tick =>
            {
                attacks[tick] = run.Born is { } born && target.TouchingEntity is { } touching && ReferenceEquals(touching, born.SelfEntity) && born.CurrentAttack is { } a ? (a.Attribute, a.Power) : null;
                if (run.Born is { CurrentAttack: { } attack } dying)
                {
                    afterDeath[tick] = (dying.Status, attack.Attribute, attack.Power);
                }
            };

            run.Go(1, key.Ticks + 1);

            var s = run.BirthTick;
            Assert.Equal(1, run.BirthSlot);
            Assert.True(target.Slot > run.BirthSlot, "the target lives in a higher slot than the arrow");
            AssertLife(key, run, 0, KeyLength(key));
            Assert.Equal("absent", run.Line(s + 32));
            // The D of the target runs at the pick of S+31, AFTER the E17 of the arrow (the arrow is in the lower slot): it finds the target touched by the arrow at S+30 (attack 131, power 1) and the arrow, destroyed in that same pass, still holds
            // its attack (the entity is not recycled before the end of the tour): the pair the D reads is intact.
            Assert.Equal((131, 1), attacks[s + 30]);
            Assert.Equal((EntityStatus.FlagToDestroy, 131, 1), afterDeath[s + 31]);
            Assert.Equal((EntityStatus.Normal, 131, 1), afterDeath[s + 30]);
        }

        // The target destroyed in the same tour (status 4 before E17 runs): still found, the arrow dies at S+31 as without it.
        var deadKey = ArrowOracle.Load("arrow3_target_dead_same_dispatch", stepByImage: true);
        using var dead = WithTarget("e7-dead", 5, deadKey, bodyHeight: 32);
        var deadTarget = dead.Arena.Targets[0];
        Assert.True(deadTarget.Slot < 2, "the target is in a lower slot than the arrow: its D runs first in the pass");
        var runner = new DestroyingRunner(dead.Arena.Arc.Proxy.EventProgramRunner, deadTarget);
        dead.Arena.Arc.Proxy.EventProgramRunner = runner;
        dead.Go(1, deadKey.Ticks + 1);
        Assert.Equal(1, runner.Calls); // the D of the target ran once, at the pick that follows the touch, and destroyed it before the E17 of the arrow.
        AssertLife(deadKey, dead, 0, KeyLength(deadKey));
        Assert.Equal("absent", dead.Line(dead.BirthTick + 32));
    }

    /// <summary>The D program of the target, made by hand: <c>DestroyEntity(e, -1)</c> (the destruction in the same tour that the oracle's key stands for), at the pick that follows the touch; every other call goes to the runner of the arena.</summary>
    private sealed class DestroyingRunner : IEventProgramRunner
    {
        private readonly IEventProgramRunner _inner;
        private readonly EntityScriptProxy _target;

        public DestroyingRunner(IEventProgramRunner inner, EntityScriptProxy target)
        {
            _inner = inner;
            _target = target;
        }

        public int Calls { get; private set; }

        public void RunScript(EntityScriptProxy entity, int programSlot) => _inner.RunScript(entity, programSlot);

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
            if (ReferenceEquals(entity, _target) && entity.EventTrigger == ScriptHelper.ProgramDTouch)
            {
                Calls++;
                entity.ScriptHost!.DestroyEntity(entity, -1);
                return;
            }

            _inner.RunSpriteEvent(entity);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e7) the shot in the air: the hunter's arrow is born at P+1, William's normal arrow at P+9.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, "arrow3_air_east", 9)]
    [InlineData(6, "arrow4_air_east", 17)]
    public void E7_TheShotInTheAir_IsBornAtThePlusOneOrPlusNine_AndLivesTheKeysLife(int item, string name, int birth)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun("e7-" + name, item, Column(15), pad: t => t == 1 ? Cross : t == 8 ? Square : 0u);

        run.Go(1, key.Ticks + 1);

        Assert.Equal((birth, birth), (run.BirthTick, key.FirstTick));
        AssertLife(key, run, 0, KeyLength(key));
        Assert.Equal("absent", run.Line(run.BirthTick + 61));
        // The generator is not compared here: the key has 6 draws at the tick 22 that are the hero's (his jump), not the arrow's (it is born at S = 9 or 17 and draws nothing in its life); the DLL's hero makes one step only at that tick in this bench (measured: steps 1 against 6 + 1), a gap of the hero's jump outside this slice.
    }
}
