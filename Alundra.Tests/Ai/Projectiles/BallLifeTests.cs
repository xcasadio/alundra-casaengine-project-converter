#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
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
/// E20.f6b T1, phase A (docs/plan-e20-heros.md section 1.22.2, tests (b5) and (b6)): the life of the fire and ice balls (the sprite types 11, 12, 14 and 15) born by the TEST HOOK in the real world of map 14 on the synthetic field of the scenario (<see cref="BallRun"/>, the handlers C60 and E9 of <see cref="Alundra.Ai.Projectiles.BallAi"/>
/// registered by the run itself), against the keys of the oracle <c>docs/plan-e20-annexe/e20-f4f6</c> read by key (<see cref="BallOracle"/>): the machine that runs the real code of <c>ALUN_CD.EXE</c>. A tick of the oracle is a tick of the test: the normal ball is born at S = P + 11 = 12, the charged ball at S = R + 11 = 107
/// (the pad of the oracle: Square held to the tick 95, released at 96; 108 at three ticks per frame, the release being read at the frame boundary). Nothing is born in production in this phase (the set of wired types does not hold the balls). Each case without effect is set next to a positive case in the same test.
/// (b5) the whole life: flat ground, wall, steps of 16 and 32 pixels, water (the slopes 1 to 4), the box, the flags, the effects, the sounds and the generator, at 1, 2 and 3 ticks per frame; the window of the wall; the branch A of E9 on the water; (b6) the touches.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BallLifeTests : IDisposable
{
    private const int Fire = 16;
    private const int Ice = 14;
    private const int Rate1 = 1;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public BallLifeTests() => Reset();

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
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The scenarios of the keys: the field, the rod, the place of the hero, the charge, the target.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed record Scenario(int Item, Func<int, int, CutBench.Cell>? Cells, bool Charged = false, int HeroX = 100, int[]? Target = null);

    private static Func<int, int, CutBench.Cell> Wall(int column) => (c, _) => c == column ? new CutBench.Cell(0x40, 0) : default;

    private static Func<int, int, CutBench.Cell> Step(int firstColumn, int height) => (c, _) => c >= firstColumn && c <= 9 ? new CutBench.Cell(0, height) : default;

    private static Func<int, int, CutBench.Cell> WallThenWater() => (c, _) => c == 5 ? new CutBench.Cell(0, 0, null, 0x08) : c == 6 ? new CutBench.Cell(0x40, 0) : default;

    private static readonly IReadOnlyDictionary<string, Scenario> Scenarios = new Dictionary<string, Scenario>
    {
        ["fire11_flat"] = new(Fire, null),
        ["ice14_flat"] = new(Ice, null),
        ["fire12_flat"] = new(Fire, null, Charged: true),
        ["ice15_flat"] = new(Ice, null, Charged: true),
        ["fire11_wall_col6"] = new(Fire, Wall(6)),
        ["fire11_wall_col8"] = new(Fire, Wall(8)),
        ["ice14_wall_col6"] = new(Ice, Wall(6)),
        ["fire12_wall_col6"] = new(Fire, Wall(6), Charged: true),
        ["fire11_step16_col6"] = new(Fire, Step(6, 1)),
        ["fire11_step16_col7"] = new(Fire, Step(7, 1)),
        ["fire11_step32_col6"] = new(Fire, Step(6, 2)),
        ["ice14_step16_col6"] = new(Ice, Step(6, 1)),
        ["ice14_step32_col6"] = new(Ice, Step(6, 2)),
        ["fire11_gp8_deepwater"] = new(Fire, BallRun.Columns(5, 9, 0x08)),
        ["ice14_gp8_deepwater"] = new(Ice, BallRun.Columns(5, 9, 0x08)),
        ["fire11_gp2_slope1"] = new(Fire, BallRun.Columns(5, 9, 0x02)),
        ["fire11_gp4_slope2"] = new(Fire, BallRun.Columns(5, 9, 0x04)),
        ["fire11_gp6_slope3"] = new(Fire, BallRun.Columns(5, 9, 0x06)),
        ["fire11_mixed_footprint_water"] = new(Fire, BallRun.Columns(7, 9, 0x08)),
        ["fire11_water_both_cols"] = new(Fire, BallRun.Columns(6, 9, 0x08)),
        ["fire11_wall_then_water"] = new(Fire, WallThenWater()),
        ["ice14_wall_then_water"] = new(Ice, WallThenWater()),
        ["fire11_point_blank"] = new(Fire, Wall(5), HeroX: 108),
        ["fire11_flush"] = new(Fire, Wall(5), HeroX: 109),
        ["ice14_flush"] = new(Ice, Wall(5), HeroX: 109),
        ["fire12_flush"] = new(Fire, Wall(5), Charged: true, HeroX: 109),
        ["ice15_flush"] = new(Ice, Wall(5), Charged: true, HeroX: 109),
        ["fire11_near105"] = new(Fire, Wall(5), HeroX: 105),
        ["fire11_vs_target"] = new(Fire, null, Target: new[] { 156, 104 }),
        ["ice14_vs_target"] = new(Ice, null, Target: new[] { 156, 104 }),
        ["fire11_explosion_hits_target"] = new(Fire, null, Target: new[] { 180, 104 }),
        ["ice14_explosion_hits_target"] = new(Ice, null, Target: new[] { 180, 104 }),
    };

    /// <summary>The keys of the lifes that equal the oracle at every tick, at the rates that keep the claim (a charged ball is read at one and three ticks per frame).</summary>
    public static IEnumerable<object[]> LifeCases()
    {
        foreach (var (name, scenario) in Scenarios)
        {
            foreach (var rate in scenario.Charged ? new[] { 1, 3 } : new[] { 1, 2, 3 })
            {
                yield return new object[] { name, rate };
            }
        }
    }

    private static BallRun Run(string name, bool injectionsFree = true, Func<int, uint>? pad = null)
    {
        var s = Scenarios[name];
        var charged = s.Charged ? (Func<int, uint>)(t => t <= 95 ? PadState.Square : 0u) : null;
        return new BallRun(name, s.Item, s.Cells, s.Target is null ? null : new List<int[]> { s.Target }, pad ?? charged, s.HeroX, s.Charged, handlers: true, seed: Key(name).Rng0(1));
    }

    private static BallOracle Key(string name, bool stepByImage = true)
        => Scenarios.TryGetValue(name, out var s) && s.Charged ? BallOracle.LoadCharged(name, stepByImage) : stepByImage ? BallOracle.LoadStepByImage(name) : BallOracle.Load(name);

    private static int TrailId(int type) => type is 11 or 12 ? 17 : 18;

    /// <summary>The last tick of the key the run compares: the key's, cut to a whole number of frames.</summary>
    private static int LastTick(BallOracle key, int rate) => key.Ticks / rate * rate;

    /// <summary>The offset of the run's ticks over the key's (the birth of the ball in the run minus the first tick of the key).</summary>
    private static int Shift(BallRun run, BallOracle key) => run.BirthTick - key.FirstTick;

    private static string Fmt(IEnumerable<string> lines) => string.Join("\n", lines);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b5) the whole life, line by line: status, X, Z, slope, ground; the birth; at 1, 2 and 3 ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(LifeCases))]
    public void B5_TheLife_FromTheBirthToTheEnd_EqualsTheKeyAtEveryTick_WhateverTheGrouping(string name, int ticksPerFrame)
    {
        var key = Key(name, stepByImage: false);
        using var run = Run(name);

        run.Go(ticksPerFrame, LastTick(key, ticksPerFrame));

        var shift = Shift(run, key);
        var charged = Scenarios[name].Charged;
        Assert.InRange(shift, 0, ticksPerFrame - 1); // the birth: S in the oracle, the frame of the press shifts it by at most the grouping.
        Assert.Equal(charged ? 107 : 12, key.FirstTick);
        var last = Math.Min(key.Ticks - key.FirstTick, run.LastTick - run.BirthTick);
        Assert.True(last >= 11, $"the life compared is {last} ticks long"); // at least up to the explosion or the death at S+11.
        var expected = Enumerable.Range(0, last + 1).Select(k => $"S+{k,2}: {key.Line(key.FirstTick + k)}");
        var actual = Enumerable.Range(0, last + 1).Select(k => $"S+{k,2}: {run.Line(run.BirthTick + k)}");
        Assert.Equal(Fmt(expected), Fmt(actual));

        // The effects, the box, the flags and the sounds of the same life (the key with the step by image carries the same effects; it is read for the speeds the draws give).
        AssertEffectsBoxFlagsAndSounds(run, Key(name), ticksPerFrame);
    }

    [Fact]
    public void B5_OnFlatGround_TheValuesOfThePlan_TheStatusesTheXTheZTheForcesTheGroundTheBoxTheFlags_AtOneTickPerFrame()
    {
        using var run = Run("fire11_flat");
        run.Go(1, 70);
        var s = run.BirthTick;
        Assert.Equal(12, s);

        // The statuses: 2 from S to S+10, 3 from S+11 to S+47, 4 at S+48, absent at S+49.
        Assert.Equal(Enumerable.Repeat(EntityStatus.Normal, 11), Enumerable.Range(0, 11).Select(k => run.Ticks[s + k].Status));
        Assert.Equal(Enumerable.Repeat(EntityStatus.Deactivated, 37), Enumerable.Range(11, 37).Select(k => run.Ticks[s + k].Status));
        Assert.Equal(EntityStatus.FlagToDestroy, run.Ticks[s + 48].Status);
        Assert.Equal("absent", run.Line(s + 49));

        // X: 106 at S, +6 per tick, 166 at S+10 and after.
        Assert.Equal(new[] { 106, 112, 118, 124, 130, 136, 142, 148, 154, 160, 166, 166, 166 }, Enumerable.Range(0, 13).Select(k => run.Ticks[s + k].X >> 16));

        // Z of the DLL (the binary's minus one raw unit): 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5, 0 from S to S+10.
        Assert.Equal(new[] { 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5, 0 }, Enumerable.Range(0, 11).Select(k => Math.Round(run.Ticks[s + k].Z / 65536.0, 3)));

        // The forces: fx 393216 up to S+10, then 0 from S+11; fz = -32768 (k + 1) for k = 0 to 9, then 0 at S+10.
        Assert.Equal(Enumerable.Repeat(393216, 11).Concat(new[] { 0, 0 }), Enumerable.Range(0, 13).Select(k => run.Ticks[s + k].ForceX));
        Assert.Equal(Enumerable.Range(0, 10).Select(k => -32768 * (k + 1)).Concat(new[] { 0, 0 }), Enumerable.Range(0, 12).Select(k => run.Ticks[s + k].ForceZ));

        // The ground: it stands on it from S+10 (not before).
        Assert.Equal(Enumerable.Repeat(0, 10).Concat(new[] { 1, 1 }), Enumerable.Range(0, 12).Select(k => run.Ticks[s + k].IsOnGround));

        // The box: 30 x 20 x 15 in flight, 30 x 20 x 31 at the height 0 from S+11; the flags 0x132154 then 0x132144 from S+11.
        Assert.Equal("30x20x15", run.Ticks[s].Box[..run.Ticks[s].Box.IndexOf(' ', StringComparison.Ordinal)]);
        Assert.Equal("30x20x15", run.Ticks[s + 10].Box[..run.Ticks[s + 10].Box.IndexOf(' ', StringComparison.Ordinal)]);
        Assert.Equal("30x20x31 hbz=0", run.Ticks[s + 11].Box);
        Assert.Equal(0x132154u, run.Ticks[s + 10].Flags);
        Assert.Equal(0x132144u, run.Ticks[s + 11].Flags);
        Assert.Equal(0x132144u, run.Ticks[s + 47].Flags);

        // The end of the animation: +0xAC is 0 up to S+46 and 1 at S+47; the death is at S+48.
        Assert.Equal(Enumerable.Repeat(0, 47).Concat(new[] { 1 }), Enumerable.Range(0, 48).Select(k => run.Ticks[s + k].ResetAnimation));
    }

    [Fact]
    public void B5_TheEffectsOfTheLife_OnFlatGround_TheTrailsTheExplosionAndThePuffs_AtTheTicksAndThePositionsOfThePlan_TheIceAndTheChargedBallsToo()
    {
        // The fire: two trails before the explosion (S+4 and S+8), the explosion at S+11 (five effects at (166, 100, 0)), the puffs at S+16, +24, +32, +40; the last tick S+47 makes none.
        using (var run = Run("fire11_flat"))
        {
            run.Go(1, 70);
            var s = run.BirthTick;
            string At(int k) => string.Join(" | ", run.Effects.Where(e => e.Tick == s + k).Select(e => $"id={e.Id} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})"));

            Assert.Equal("id=17 x=124.000 y=100.000 z=35.000 v=(-6.0000,0.0000,2.0000)", At(4));
            Assert.Equal("id=17 x=148.000 y=100.000 z=22.000 v=(-6.0000,0.0000,4.0000)", At(8));
            Assert.Equal(5, run.Effects.Count(e => e.Tick == s + 11));
            Assert.All(run.Effects.Where(e => e.Tick == s + 11), e => Assert.Equal((17, "166.000", "100.000", "0.000"), (e.Id, e.X, e.Y, e.Z)));
            var puffs = run.Effects.Where(e => e.Tick > s + 11).ToList();
            Assert.Equal(new[] { 16, 24, 32, 40 }, puffs.Select(e => e.Tick - s).ToArray());
            Assert.All(puffs, e => Assert.Equal((17, "166.000", "100.000", "8.000", "2.0000"), (e.Id, e.X, e.Y, e.Z, e.Vz)));
            Assert.Equal(Array.Empty<int>(), run.Effects.Where(e => e.Tick >= s + 41).Select(e => e.Tick - s).ToArray()); // no puff at S+48, nor between S+41 and S+47.
            Assert.Equal(new[] { 39 }, run.Heard.Played.Where(x => x.Tick >= s && x.Sfx != 84).Select(x => x.Sfx).ToArray()); // the sound of the explosion.
            Assert.Equal(s + 11, run.Heard.Played.Single(x => x.Sfx == 39).Tick);
        }

        // The ice: x = 163 at the explosion, the effect 18, no Z offset on the trail (z = 27 at S+4, 14 at S+8) and on the puff, the sound 38; the end at S+43, the death at S+44, the case free at S+45.
        using (var ice = Run("ice14_flat"))
        {
            ice.Go(1, 70);
            var s = ice.BirthTick;
            Assert.Equal(new[] { "121.000,100.000,27.000", "145.000,100.000,14.000" }, ice.Effects.Where(e => e.Tick - s is 4 or 8).Select(e => $"{e.X},{e.Y},{e.Z}").ToArray());
            Assert.All(ice.Effects.Where(e => e.Tick >= s), e => Assert.Equal(18, e.Id));
            Assert.Equal(new[] { "163.000" }, ice.Effects.Where(e => e.Tick == s + 11).Select(e => e.X).Distinct().ToArray());
            Assert.All(ice.Effects.Where(e => e.Tick > s + 11), e => Assert.Equal("0.000", e.Z));
            Assert.Equal(new[] { 38 }, ice.Heard.Played.Where(x => x.Tick >= s && x.Sfx != 84).Select(x => x.Sfx).ToArray());
            Assert.Equal(EntityStatus.Deactivated, ice.Ticks[s + 43].Status);
            Assert.Equal(1, ice.Ticks[s + 43].ResetAnimation);
            Assert.Equal(EntityStatus.FlagToDestroy, ice.Ticks[s + 44].Status);
            Assert.Equal("absent", ice.Line(s + 45));
        }

        // The charged balls: S = R + 11 = 107, power 12 (the key), the explosion box 36 x 24 x 31; the sound 41 (fire) and 40 (ice); the end at S+47 / S+67, the death at S+48 / S+68.
        foreach (var (name, sound, end) in new[] { ("fire12_flat", 41, 47), ("ice15_flat", 40, 67) })
        {
            using var charged = Run(name);
            charged.Go(1, 190);
            var s = charged.BirthTick;
            Assert.Equal(107, s);
            Assert.Equal("36x24x31 hbz=0", charged.Ticks[s + 11].Box);
            Assert.Equal(new[] { sound }, charged.Heard.Played.Where(x => x.Tick >= s && x.Sfx != 84).Select(x => x.Sfx).ToArray());
            Assert.Equal(1, charged.Ticks[s + end].ResetAnimation);
            Assert.Equal(EntityStatus.FlagToDestroy, charged.Ticks[s + end + 1].Status);
            Assert.Equal("absent", charged.Line(s + end + 2));
        }
    }

    /// <summary>The effects, the box, the flags, the end of the animation and the sounds of the life of the run, against the key (the same run as the lines: one world a case).</summary>
    private static void AssertEffectsBoxFlagsAndSounds(BallRun run, BallOracle key, int ticksPerFrame)
    {
        var s = run.BirthTick;
        var shift = Shift(run, key);
        var id = TrailId(run.BallType);
        var last = Math.Min(key.Ticks - key.FirstTick, run.LastTick - s);

        // The effects of the ball (the trail, the explosion, the puffs), created when the stage applies its queue: at the END of the frame that holds the tick. At one tick per frame with the speeds the draws give; at more ticks per frame the speeds that depend on the
        // draws are not read (the immediate draws of the next ticks of the frame do not see the intermediate steps of the generator: the documented deviation of ADR-0066), the positions and the order are.
        var expected = key.Effects(id).Where(e => e.Tick >= key.FirstTick && e.Tick <= key.FirstTick + last)
            .Select(e => ticksPerFrame == 1 ? e.Line(BallRun.FrameEnd(e.Tick + shift, ticksPerFrame)) : e.PositionLine(BallRun.FrameEnd(e.Tick + shift, ticksPerFrame))).ToList();
        var actual = run.Effects.Where(e => e.Id == id && e.Tick >= s).Select(e => ticksPerFrame == 1
            ? $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})"
            : $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z}").ToList();
        Assert.Equal(Fmt(expected), Fmt(actual));
        Assert.NotEmpty(expected);

        // The box, the flags and the end of the animation at every tick of the life (the key's flags6C and reset_AC, the box WxDxH hbz).
        var wrong = new List<string>();
        for (var k = 0; k <= last; k++)
        {
            var t = run.Ticks[s + k];
            if (key.Ball(key.FirstTick + k) is null)
            {
                Assert.False(t.Present, $"S+{k}");
                continue;
            }

            var wanted = $"flags=0x{(uint)key.Value(key.FirstTick + k, "flags6C"):X} ac={key.Value(key.FirstTick + k, "reset_AC")} box={key.BoxLine(key.FirstTick + k)}";
            var got = $"flags=0x{t.Flags:X} ac={t.ResetAnimation} box={t.Box}";
            if (wanted != got)
            {
                wrong.Add($"S+{k}: key [{wanted}] dll [{got}]");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(6)));

        // The sounds of the life (the explosion): the key's, at the same tick. The object a cut could drop (84) is not the ball's.
        var wantedSounds = key.Sounds(key.FirstTick, key.FirstTick + last).Select(x => $"+{x.Tick - key.FirstTick} snd{x.Sfx}").ToList();
        var heard = run.Heard.Played.Where(x => x.Tick >= s && x.Tick <= s + last && x.Sfx != 84).Select(x => $"+{x.Tick - s} snd{x.Sfx}").ToList();
        Assert.Equal(wantedSounds, heard); // the key of a ball that dies on the water has none: the dedicated test of the water holds the positive case.
    }

    [Theory]
    [InlineData("fire11_flat")]
    [InlineData("ice14_flat")]
    [InlineData("fire12_flat")]
    [InlineData("ice15_flat")]
    [InlineData("fire11_wall_col6")]
    [InlineData("fire12_wall_col6")]
    [InlineData("fire11_step16_col6")]
    [InlineData("fire11_step32_col6")]
    [InlineData("fire11_gp8_deepwater")]
    [InlineData("ice14_gp8_deepwater")]
    [InlineData("fire11_gp2_slope1")]
    [InlineData("fire11_gp4_slope2")]
    [InlineData("fire11_gp6_slope3")]
    [InlineData("fire11_mixed_footprint_water")]
    [InlineData("fire11_wall_then_water")]
    [InlineData("ice14_wall_then_water")]
    [InlineData("fire11_flush")]
    [InlineData("ice15_flush")]
    [InlineData("fire11_vs_target")]
    [InlineData("ice14_vs_target")]
    public void B5_TheGenerator_FollowsTheKeyWithTheStepByImage_TheExplosionDrawsTenNumbersAndEachPuffTwo_AtOneTickPerFrame(string name)
    {
        var key = Key(name);
        using var run = Run(name);

        run.Go(1, key.Ticks);

        // After every frame the state is the key's rng0 of the next tick (the step of the end of the frame is applied by the DLL) and the number of steps of the frame is the draws of the key plus that one. The charged keys hold the two numbers of the spark of the full
        // charge at the tick 93 in the key and at the tick 95 in the DLL (the hero's, out of the perimeter of the balls: the state is equal from the tick 95 on).
        var from = Scenarios[name].Charged ? 96 : 1;
        var wrong = new List<string>();
        for (var tick = from; tick < key.Ticks; tick++)
        {
            if (run.StateAfter[tick] != key.Rng0(tick + 1) || run.StepsDuring[tick] != key.Draws(tick) + 1)
            {
                wrong.Add($"t{tick}: state 0x{run.StateAfter[tick]:X8} (key 0x{key.Rng0(tick + 1):X8}), steps {run.StepsDuring[tick]} (key draws {key.Draws(tick)} + 1)");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));
        Assert.Contains(Enumerable.Range(key.FirstTick, key.Ticks - key.FirstTick), t => key.Draws(t) == 10 || name.Contains("water", StringComparison.Ordinal) || name.Contains("gp", StringComparison.Ordinal)); // the key does draw (the positive case).
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b5) the water: the slopes 4 and 1 end the ball at S+11 with the break effect 6 and no draw; the slopes 2 and 3 and a foot half on the water explode.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("fire11_gp8_deepwater", 4)]
    [InlineData("ice14_gp8_deepwater", 4)]
    [InlineData("fire11_gp2_slope1", 1)]
    public void B5_OnTheSlopes4And1_TheBallDiesAtSPlus11WithTheBreakEffect6_FreeAtSPlus12_NoDrawNoExplosionNoSound(string name, int slope)
    {
        var key = Key(name);
        foreach (var ticksPerFrame in new[] { 1, 2, 3 })
        {
            using var run = Run(name);
            run.Go(ticksPerFrame, LastTick(key, ticksPerFrame));
            var s = run.BirthTick;

            Assert.Equal(slope, run.Ticks[s + 10].Slope); // the ball has landed on the water (the f6a feed).
            Assert.Equal(EntityStatus.FlagToDestroy, run.Ticks[s + 11].Status);
            Assert.Equal("absent", run.Line(s + 12));
            Assert.Equal(new[] { BallRun.FrameEnd(s + 11, ticksPerFrame) }, run.AttachedSeenAtFrameEnd[6].ToArray()); // the effect of the water, once, when the ball dies.
            Assert.Empty(run.Effects.Where(e => e.Tick > BallRun.FrameEnd(s + 8, ticksPerFrame))); // no explosion, no puff: nothing after the trail of S+8.
            Assert.Equal(2, run.Effects.Count(e => e.Id == TrailId(run.BallType))); // the two trails of S+4 and S+8.
            Assert.DoesNotContain(run.Heard.Played, x => x.Sfx is 38 or 39);
            Assert.Equal(0, key.Draws(key.FirstTick + 11)); // the key draws none either.
            if (ticksPerFrame == 1)
            {
                Assert.All(Enumerable.Range(s, 14), t => Assert.Equal(1, run.StepsDuring[t])); // one step a tick: no draw at all.
            }
        }

        // Next to it: the slopes 2 and 3 and the foot half on the water explode normally (ten draws, the sound 39, the status 3 at S+11).
        foreach (var other in new[] { "fire11_gp4_slope2", "fire11_gp6_slope3", "fire11_mixed_footprint_water" })
        {
            var otherKey = Key(other);
            using var run = Run(other);
            run.Go(1, LastTick(otherKey, 1));
            var s = run.BirthTick;

            Assert.Equal(EntityStatus.Deactivated, run.Ticks[s + 11].Status);
            Assert.Equal(11, run.StepsDuring[s + 11]);
            Assert.Contains(run.Heard.Played, x => x.Tick == s + 11 && x.Sfx == 39);
            Assert.False(run.AttachedSeenAtFrameEnd.ContainsKey(6));
        }
    }

    [Theory]
    [InlineData("fire11_wall_then_water")]
    [InlineData("ice14_wall_then_water")]
    public void B5_ABallThatExplodedAgainstAWallAndFallsOnTheWater_IsDestroyedByTheBranchAAtSPlus11_TheExplosionOfSPlus7StaysDone(string name)
    {
        var key = Key(name);
        using var run = Run(name);

        run.Go(1, key.Ticks);

        var s = run.BirthTick;
        Assert.Equal(11, run.StepsDuring[s + 7]); // the explosion at the wall: ten draws and the step of the frame.
        Assert.Equal(EntityStatus.Deactivated, run.Ticks[s + 7].Status);
        Assert.Equal(EntityStatus.FlagToDestroy, run.Ticks[s + 11].Status);
        Assert.Equal(new[] { s + 11 }, run.AttachedSeenAtFrameEnd[6].ToArray());
        Assert.Equal(1, run.StepsDuring[s + 11]); // the destruction draws nothing.
        Assert.Equal(key.Attached().Single(a => a.Id == 6).Tick, key.FirstTick + 11);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b5) the branch A of E9 on the water (the slope of the ball injected at the end of a tick, as the keys do): destruction then puff in the same tick at a multiple of 8, the destruction alone elsewhere, nothing for the slope 2.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static BallRun Injected(string name, int item, int atTick, int slope)
    {
        var run = new BallRun(name, item, null, null, null, 100, false, handlers: true);
        run.InjectEachTick = tick =>
        {
            if (tick == atTick)
            {
                run.Born!.Slope_18c = slope;
            }
        };
        return run;
    }

    [Theory]
    [InlineData("fire11_branchA_water_on_puff_tick", Fire, 4)]
    [InlineData("ice14_branchA_water_on_puff_tick", Ice, 4)]
    [InlineData("fire11_branchA_slope1_on_puff_tick", Fire, 1)]
    public void B5_BranchA_OnAPuffTick_TheBallIsDestroyedWithTheEffect6ThenThePuffOfTheSameTickIsMade_InThisOrder(string name, int item, int slope)
    {
        var key = BallOracle.LoadStepByImage(name);
        using var run = Injected(name, item, 12 + 15, slope);

        run.Go(1, key.Ticks);

        var s = run.BirthTick;
        var id = TrailId(run.BallType);
        var tick = s + 16;
        // The key: the tick S+16 holds, in this order, the attached effect 6 then the puff.
        var texts = key.EffectTexts(key.FirstTick + 16);
        Assert.StartsWith("attach(id=6", texts[0], StringComparison.Ordinal);
        Assert.StartsWith($"fx(id={id}", texts[1], StringComparison.Ordinal);
        // The run: the lines equal the key's from S to the end, the break effect 6 is seen at S+16, the puff is the key's (the speeds too), two draws and the step of the frame.
        var last = key.Ticks - key.FirstTick;
        Assert.Equal(
            Fmt(Enumerable.Range(0, last + 1).Select(k => key.Line(key.FirstTick + k))),
            Fmt(Enumerable.Range(0, last + 1).Select(k => run.Line(s + k))));
        Assert.Equal(new[] { tick }, run.AttachedSeenAtFrameEnd[6].ToArray());
        var puff = Assert.Single(run.Effects.Where(e => e.Tick == tick));
        Assert.Equal(key.Effects(id).Single(e => e.Tick == key.FirstTick + 16).Line(tick), $"t{puff.Tick} id={puff.Id} dir={puff.Direction} x={puff.X} y={puff.Y} z={puff.Z} v=({puff.Vx},{puff.Vy},{puff.Vz})");
        Assert.Equal(3, run.StepsDuring[tick]);
        Assert.True(run.AttachedSlots[6].Single() < puff.Slot, "the destruction comes first: the break effect takes a lower slot than the puff"); // the order of creation is the order of the lowest free slots.
        Assert.Equal(EntityStatus.FlagToDestroy, run.Ticks[tick].Status);
        Assert.Equal("absent", run.Line(tick + 1));
    }

    [Fact]
    public void B5_BranchA_NotOnAPuffTick_TheDestructionIsAlone_TheSlope2DestroysNothingAndThePuffGoesOn_TwoDestructionsAtTheEndOfTheAnimation()
    {
        // The slope 4 at the end of S+16: the tick S+17 is not a multiple of 8: the effect 6 alone, no puff, no draw.
        var off = BallOracle.LoadStepByImage("fire11_branchA_water_off_puff_tick");
        using (var run = Injected("branchA-off", Fire, 12 + 16, 4))
        {
            run.Go(1, off.Ticks);
            var s = run.BirthTick;
            Assert.Equal(new[] { s + 17 }, run.AttachedSeenAtFrameEnd[6].ToArray());
            Assert.Empty(run.Effects.Where(e => e.Tick == s + 17));
            Assert.Equal(1, run.StepsDuring[s + 17]);
            Assert.Equal(Fmt(Enumerable.Range(0, off.Ticks - off.FirstTick + 1).Select(k => off.Line(off.FirstTick + k))), Fmt(Enumerable.Range(0, off.Ticks - off.FirstTick + 1).Select(k => run.Line(s + k))));
        }

        // The slope 2 on the puff tick (the negative control of the slopes 1 and 4): no destruction, the puff goes on, the ball lives to S+47.
        var two = BallOracle.LoadStepByImage("fire11_branchA_slope2_on_puff_tick");
        using (var run = Injected("branchA-2", Fire, 12 + 15, 2))
        {
            run.Go(1, two.Ticks);
            var s = run.BirthTick;
            Assert.False(run.AttachedSeenAtFrameEnd.ContainsKey(6));
            Assert.Single(run.Effects.Where(e => e.Tick == s + 16));
            Assert.Equal(EntityStatus.Deactivated, run.Ticks[s + 17].Status);
            Assert.Equal(Fmt(Enumerable.Range(0, two.Ticks - two.FirstTick + 1).Select(k => two.Line(two.FirstTick + k))), Fmt(Enumerable.Range(0, two.Ticks - two.FirstTick + 1).Select(k => run.Line(s + k))));
        }

        // The slope 4 at the end of S+47, where the animation has ended (+0xAC is 1): at S+48 the destruction by the slope (the effect 6) then the one of the end of the animation (-1, no effect): the ball is dead at S+48 and the case is free at S+49.
        var end = BallOracle.LoadStepByImage("fire11_branchA_water_on_end_tick");
        using (var run = Injected("branchA-end", Fire, 12 + 47, 4))
        {
            run.Go(1, end.Ticks);
            var s = run.BirthTick;
            Assert.Equal(new[] { s + 48 }, run.AttachedSeenAtFrameEnd[6].ToArray()); // one break effect only: the second destruction has none.
            Assert.Equal(EntityStatus.FlagToDestroy, run.Ticks[s + 48].Status);
            Assert.Empty(run.Effects.Where(e => e.Tick == s + 48)); // no puff.
            Assert.Equal(Fmt(Enumerable.Range(0, end.Ticks - end.FirstTick + 1).Select(k => end.Line(end.FirstTick + k))), Fmt(Enumerable.Range(0, end.Ticks - end.FirstTick + 1).Select(k => run.Line(s + k))));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b5) the window of the wall (D-F6-10 = D-F4-9): the flag of the wall comes in the window S+5 to S+8 over the twelve starts of the hero, the explosion one tick after it; the landing and the death are those of the key for the start of the oracle.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Fire)]
    [InlineData(Ice)]
    public void B5_TheFlagOfTheWall_ComesInTheWindowSPlus5ToSPlus8_OnTheTwelveStartsOfTheHero_TheExplosionFollowsIt_TheStartOfTheOracleIsAtSPlus6(int item)
    {
        var report = new List<string>();
        foreach (var x0 in Enumerable.Range(96, 12))
        {
            using var run = new BallRun("c7-" + item + "-" + x0, item, Wall(6), null, null, x0, false, handlers: true);
            run.Go(1, 70);
            var s = run.BirthTick;
            var flag = Enumerable.Range(0, 20).First(k => run.Ticks[s + k].Adjusted == 1);
            var explosion = Enumerable.Range(0, 20).First(k => run.Ticks[s + k].Status == EntityStatus.Deactivated);
            var x = run.Ticks[s + flag].X >> 16;
            report.Add($"x0={x0}: flag S+{flag} explosion S+{explosion} x={x}");
            Assert.True(s == 12 && flag is >= 5 and <= 8 && explosion == flag + 1 && x == 133, string.Join(Environment.NewLine, report));
            if (x0 == 100)
            {
                Assert.Equal(6, flag);
            }
        }
    }

    [Fact]
    public void B5_ABallShotWithTheHeroAt107_HasTheStartOfTheKey_ButItsFlagComesOneTickEarly_TheDeviationOfE20e0k5IsPinned()
    {
        // The key fire11_x107 (the hero at 107, wall in the column 5): the position of S is the oracle's (7143423), the flag of the wall comes at S+1 here and at S+2 in the oracle (D-E20-38, D-E0K5-2: after the integration the entity goes through the engine again), so
        // the explosion is made one tick early (S+2 for S+3). It is pinned: it changes with the fix of the deviation and with nothing else.
        var key = BallOracle.LoadStepByImage("fire11_x107");
        using var run = new BallRun("x107", Fire, Wall(5), null, null, 107, false, handlers: true);

        run.Go(1, key.Ticks);

        var s = run.BirthTick;
        Assert.Equal(key.Line(key.FirstTick), run.Line(s));
        Assert.Equal(7143423, run.Ticks[s].X);
        Assert.Equal(EntityStatus.Deactivated, run.Ticks[s + 2].Status); // the oracle: the status 3 at S+3.
        Assert.Equal(3, key.Value(key.FirstTick + 3, "status"));
        Assert.Equal(2, key.Value(key.FirstTick + 2, "status"));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b6) the touches: the ball touches the Beannoid in flight at S+8 (fire) or S+9 (ice): the effect 4 or 5 attached to the target BEFORE the three sparks, nine draws, the ball goes on; the explosion box touches it again 25 ticks later.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("fire11_vs_target", 4, 8, 33)]
    [InlineData("ice14_vs_target", 5, 9, 34)]
    [InlineData("fire11_explosion_hits_target", 4, 9, 34)]
    [InlineData("ice14_explosion_hits_target", 5, 9, 34)]
    public void B6_TheBallTouchesTheTargetInFlight_TheAttachedEffectComesBeforeTheThreeSparks_NineDraws_TheBallGoesOn_TheSecondTouchIs25TicksLater(string name, int attached, int first, int second)
    {
        var key = Key(name);
        var keyTouches = key.Attached().Where(a => a.Id == attached).Select(a => a.Tick - key.FirstTick).ToList();
        Assert.Equal(new[] { first, second }, keyTouches.ToArray()); // the key's own touches: the figures of this test are the key's.

        using var run = Run(name);
        run.Go(1, key.Ticks);

        var s = run.BirthTick;
        // The effect attached to the target and the three sparks of the set, at the ticks of the key.
        Assert.Equal(keyTouches.Select(t => s + t).ToList(), run.AttachedSeenAtFrameEnd[attached]);
        foreach (var k in keyTouches)
        {
            var texts = key.EffectTexts(key.FirstTick + k);
            var attach = texts.ToList().FindIndex(t => t.StartsWith($"attach(id={attached},", StringComparison.Ordinal));
            var spark = texts.ToList().FindIndex(t => t.StartsWith("fx(id=9,", StringComparison.Ordinal));
            Assert.True(attach >= 0 && spark > attach, $"the key at S+{k}: {string.Join(" | ", texts)}"); // the attached effect before the sparks.
            Assert.Equal(1, run.Hits[s + k].Sparks); // one set of three sparks.
            Assert.Equal(10, run.StepsDuring[s + k]); // nine draws of the sparks and the step of the frame.
            Assert.Equal(9, key.Draws(key.FirstTick + k));
        }

        // The ball is not stopped by the touch: its line is the key's at every tick (it goes on flying, or standing for the touch of its explosion box), and the target is touched at no other tick of the life.
        var last = key.Ticks - key.FirstTick;
        Assert.Equal(
            Fmt(Enumerable.Range(0, last + 1).Select(k => key.Line(key.FirstTick + k))),
            Fmt(Enumerable.Range(0, last + 1).Select(k => run.Line(s + k))));
        Assert.Equal(keyTouches.Select(t => s + t).ToList(), Enumerable.Range(s, last + 1).Where(t => run.Hits[t].Sparks > 0).ToList());
        Assert.Equal(25, keyTouches[1] - keyTouches[0]);
        Assert.Equal(EntityStatus.Normal, run.Ticks[s + first].Status); // the first touch is in flight (the status 2): the ball is not stopped by it.
        Assert.Equal(EntityStatus.Deactivated, run.Ticks[s + second].Status); // the second is the one of the explosion box.

        // The same shot with no target in the world: no touch, no spark, no draw.
        using var alone = new BallRun(name + "-alone", Scenarios[name].Item, null, null, null, 100, false, handlers: true);
        alone.Go(1, key.Ticks);
        Assert.All(Enumerable.Range(alone.BirthTick, last + 1), t => Assert.Equal(0, alone.Hits[t].Sparks));
        Assert.False(alone.AttachedSeenAtFrameEnd.ContainsKey(attached));
        Assert.Equal(1, alone.StepsDuring[alone.BirthTick + keyTouches[0]]);
    }
}
