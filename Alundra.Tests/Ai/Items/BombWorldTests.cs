#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3a T2 (docs/plan-e20-heros.md section 1.30.2, the tests of the world (w1), (w2), (w3), (w4) and (w6); (w5) is in <c>OlenBombLidArcTests</c>, on the bench of the arc A362). They run on the REGISTRY OF PRODUCTION and register NOTHING: they are RED while
/// <see cref="NativeSpriteRegistry.CreateForProduction"/> does not hold (D, 30), (E, 12) and (E, 19), and green once it does. The bench is <see cref="BombRun"/> (a bomb born by the interface, the first one put in status 3 by the harness at the end of the tick 5, the tick "k+0" of the keys). Every expected value is read
/// from the key (a file of the annex, <see cref="BombKey"/>) at the tick it names: the oracle's tick k+N is the tick StatusTick + N of the test, at one, two and three ticks per frame for what does not depend on the draws ((w1), (w2), (w3), (w6)), at one tick per frame for (w4), whose loot draws.
/// The keys were made by the real code of <c>ALUN_CD.EXE</c> (France) in the MIPS machine: k_w1_away, k_w3_chain_away and k_w1_17east by the reading of 2026-10-25 (<c>../e20-a362-disc/discovery/out/</c>), zone_335 and grass_placed_pf by T0 of this slice (<c>../e20-h3a/</c>).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombWorldTests : IDisposable
{
    private const string KeyW1 = "e20-a362-disc/discovery/out/k_w1_away.txt";
    private const string KeyW3 = "e20-a362-disc/discovery/out/k_w3_chain_away.txt";
    private const string KeyW6 = "e20-a362-disc/discovery/out/k_w1_17east.txt";
    private const string KeyW4 = "e20-h3a/grass_placed_pf.txt";
    private const string ZoneGrid = "e20-h3a/zone_335_grid.txt";
    private const string ZoneScan = "e20-h3a/zone_335.txt";
    private const int K = BombRun.StatusTick;

    public BombWorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> Cadences => new() { 1, 2, 3 };

    /// <summary>The ticks run: the key holds 54 ticks after k+0 (the last frame is whole, so it can end two ticks later).</summary>
    private const int LastTick = K + 64;

    private static string Describe(BombRun.BombTick t) => t.Present
        ? $"st={t.Status} tanim={t.TargetAnimation} end={t.End} box={t.Box} hit={t.HitCounter}"
        : "free";

    private static void AssertNoDifference(List<string> diffs)
        => Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the key; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(15))}");

    /// <summary>The bomb <paramref name="index"/> against the key's columns (status, target animation, end of the animation, box, and for two bombs the hit counter) at every tick from k-4 to k+<paramref name="last"/>.</summary>
    private static void CompareBomb(BombRun run, int index, BombKey key, int last, List<string> diffs, bool second = false)
    {
        for (var r = -4; r <= last; r++)
        {
            var row = key.At(r);
            var (status, tanim, end, box, hit) = second
                ? (row.Status2, row.TargetAnimation2, row.End2, row.Box2, row.Hit2)
                : (row.Status, row.TargetAnimation, row.End, row.Box, row.Hit1);
            var expected = status == 0 ? default : new BombRun.BombTick(true, status, tanim, end, box, hit);
            var actual = run.At(index, r);
            if (status != 0 && key.Lines.Values.All(l => l.Hit1 == 0 && l.Hit2 == 0))
            {
                // The keys of one bomb carry no hit counter column.
                actual = actual with { HitCounter = 0 };
            }

            if (expected != actual)
            {
                diffs.Add($"bomb {index} k{r:+0;-0;+0}: key {Describe(expected)}, test {Describe(actual)}");
            }
        }
    }

    private static List<(int Tick, int Sfx)> SoundsOf(BombRun run) => run.Heard.Played.Where(x => x.Sfx != 0).Select(x => (x.Tick - K, x.Sfx)).ToList();

    private static List<(int Tick, int Sfx)> SoundsOfTheKey(BombKey key) => key.Lines.Values.OrderBy(l => l.Tick).SelectMany(l => l.Sounds.Select(s => (l.Tick, s))).ToList();

    // -----------------------------------------------------------------------------------------
    // (w1) the chronology of a bomb born posed, the hero far
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W1_ABombPosedInStatus3_HeroFar_TakesTheAnimation1AtK1_BoxesFromK17ToK31_EndsAtK52_IsStatus4AtK53_AndLeavesItsSlotAtK54_TickByTickTheKey(int ticksPerFrame)
    {
        var key = BombKey.LoadOneBomb(KeyW1);
        using var run = new BombRun("w1-" + ticksPerFrame, new[] { (300, 300) });
        run.Go(ticksPerFrame, LastTick);

        var diffs = new List<string>();
        CompareBomb(run, 0, key, 56, diffs);
        AssertNoDifference(diffs);

        // The facts of the plan, read from the key: status 3 at k+0, animation 1 at k+1 with the sound 46, the box from k+17 to k+31, the end of the animation at k+52, status 4 at k+53, the slot free at k+54.
        Assert.Equal(EntityStatus.Deactivated, (EntityStatus)run.At(0, 0).Status);
        Assert.Equal((0, 1), (run.At(0, 0).TargetAnimation, run.At(0, 1).TargetAnimation));
        Assert.All(Enumerable.Range(17, 15), r => Assert.True(run.At(0, r).Box, $"no box at k+{r}"));
        Assert.False(run.At(0, 16).Box);
        Assert.False(run.At(0, 32).Box);
        Assert.Equal((0, 1), (run.At(0, 51).End, run.At(0, 52).End));
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.At(0, 53).Status);
        Assert.False(run.At(0, 54).Present);
        Assert.False(run.At(0, 56).Present);

        // The sound 46 once, at k+1, and nothing else; the hero is never touched and keeps his ten points.
        Assert.Equal(SoundsOfTheKey(key), SoundsOf(run));
        Assert.Equal(new[] { (1, 46) }, SoundsOf(run));
        Assert.All(run.Samples.Values, s => Assert.Equal(10, s.Hero.Hp));
        Assert.All(run.HeroTouchesABomb.Values, touched => Assert.False(touched));
        Assert.Empty(run.SparkTicks);
    }

    // -----------------------------------------------------------------------------------------
    // (w3) the chain: a second bomb at 11 pixels
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W3_ABombPosed11PixelsFromAnother_IsTouchedAtK17_IsStatus3AtK18_ExplodesFromK19_AndLeavesItsSlotAtK72_TickByTickTheKey(int ticksPerFrame)
    {
        var key = BombKey.LoadTwoBombs(KeyW3);
        using var run = new BombRun("w3-" + ticksPerFrame, new[] { (300, 300), (311, 300) });
        run.Go(ticksPerFrame, LastTick + 20);

        var diffs = new List<string>();
        CompareBomb(run, 0, key, 74, diffs);
        CompareBomb(run, 1, key, 74, diffs, second: true);
        AssertNoDifference(diffs);

        // The facts of the plan: the first bomb touches the second at k+17 (its hit counter 1), the second is in status 3 at k+18, in animation 1 at k+19 (the sound 46 again), and both slots are free at k+54 and k+72.
        Assert.Equal(1, run.At(0, 17).HitCounter);
        Assert.Equal((2, 3), (run.At(1, 17).Status, run.At(1, 18).Status));
        Assert.Equal((0, 1), (run.At(1, 18).TargetAnimation, run.At(1, 19).TargetAnimation));
        Assert.False(run.At(0, 54).Present);
        Assert.True(run.At(1, 71).Present);
        Assert.False(run.At(1, 72).Present);
        Assert.Equal(SoundsOfTheKey(key), SoundsOf(run));
        Assert.Equal(new[] { (1, 46), (19, 46) }, SoundsOf(run));
        Assert.All(run.Samples.Values, s => Assert.Equal(10, s.Hero.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (w6) the hero in the zone: the hurt of E20.c2b by the bomb
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W6_TheHero17PixelsFromTheBomb_IsTouchedAtK17_HurtAtK18_From10To5PointsWithTheSound23_ThrownBack33Pixels_AndAtRestAtK33_TickByTickTheKey(int ticksPerFrame)
    {
        var key = BombKey.LoadOneBomb(KeyW6);
        using var run = new BombRun("w6-" + ticksPerFrame, new[] { (117, 100) });
        run.Go(ticksPerFrame, LastTick);

        var diffs = new List<string>();
        CompareBomb(run, 0, key, 54, diffs);
        for (var r = -4; r <= 54; r++)
        {
            var row = key.At(r);
            var hero = run.SampleAt(r).Hero;
            var actual = $"hp={hero.Hp} state=0x{hero.AnimTarget:x} touching={run.HeroTouchesABomb[K + r]} damaged={hero.DamagedTicks} cooldown={hero.CooldownTicks} x={BombRun.Px(hero.PosX)} y={BombRun.Px(hero.PosY)}";
            var expected = $"hp={row.HeroHp} state=0x{row.HeroState:x} touching={row.HeroTouching} damaged={row.HeroDamaged} cooldown={row.HeroCooldown} x={row.HeroX.ToString("F2", CultureInfo.InvariantCulture)} y={row.HeroY.ToString("F2", CultureInfo.InvariantCulture)}";
            if (!HeroLineEqual(expected, actual))
            {
                diffs.Add($"hero k{r:+0;-0;+0}: key {expected}, test {actual}");
            }
        }

        AssertNoDifference(diffs);

        // The facts of the plan, read from the key: contact at k+17, at k+18 ten points down to five with the state 0x31 and the sound 23, the counter 119, 0x31 to k+21, 0x1C from k+22 to k+32, rest from k+33, recoil of -33.09375 pixels.
        Assert.True(run.HeroTouchesABomb[K + 17]);
        Assert.Equal((10, 5), (run.SampleAt(17).Hero.Hp, run.SampleAt(18).Hero.Hp));
        Assert.Equal(0x31u, run.SampleAt(18).Hero.AnimTarget);
        Assert.Equal(119, run.SampleAt(18).Hero.DamagedTicks);
        Assert.Equal(0x31u, run.SampleAt(21).Hero.AnimTarget);
        Assert.Equal(0x1Cu, run.SampleAt(22).Hero.AnimTarget);
        Assert.Equal(0x1Cu, run.SampleAt(32).Hero.AnimTarget);
        Assert.Equal(0u, run.SampleAt(33).Hero.AnimTarget);
        Assert.Equal(-33.09375, (run.SampleAt(33).Hero.PosX - run.SampleAt(16).Hero.PosX) / 65536.0, 5);
        Assert.Equal(new[] { 17 }, run.SparkTicks.Select(t => t - K).Distinct().ToArray());
        Assert.Equal(SoundsOfTheKey(key), SoundsOf(run));
        Assert.Equal(new[] { (1, 46), (18, 23) }, SoundsOf(run));
    }

    /// <summary>The columns of two hero lines are equal; the positions to within half a hundredth (the key prints two decimals).</summary>
    private static bool HeroLineEqual(string expected, string actual)
    {
        static (string Columns, double X, double Y) Split(string line)
        {
            var m = Regex.Match(line, @"^(.*) x=(-?[\d.]+) y=(-?[\d.]+)$");
            return (m.Groups[1].Value, double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        var (e, ex, ey) = Split(expected);
        var (a, ax, ay) = Split(actual);
        return e == a && Math.Abs(ex - ax) <= 0.0051 && Math.Abs(ey - ay) <= 0.0051;
    }

    // -----------------------------------------------------------------------------------------
    // (w2) the zone, against the target of the test: the Beannoid of the map 14, the record 335
    // -----------------------------------------------------------------------------------------

    private readonly record struct ZoneCase(int Dx, int Dy, int? First, int Life);

    private static List<ZoneCase> ReadZone(string relative, bool grid)
    {
        var cases = new List<ZoneCase>();
        foreach (var text in File.ReadAllLines(AnnexFiles.BombAnnex(relative)))
        {
            var g = Regex.Match(text, @"^\s*\(\s*(-?\d+),\s*(-?\d+)\) -> first (None|\d+), life 9 -> (\d+), changes (\d)");
            if (grid && g.Success)
            {
                cases.Add(new ZoneCase(int.Parse(g.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(g.Groups[2].Value, CultureInfo.InvariantCulture), g.Groups[3].Value == "None" ? null : int.Parse(g.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(g.Groups[4].Value, CultureInfo.InvariantCulture)));
            }

            var s = Regex.Match(text, @"^\s*d([xy])\s+(-?\d+) -> \{'first': (None|\d+), 'hp0': 9, 'hp_end': (\d+), 'status_end': \d+, 'changes': (\d), 'bomb3': 4\}");
            if (!grid && s.Success)
            {
                var v = int.Parse(s.Groups[2].Value, CultureInfo.InvariantCulture);
                cases.Add(new ZoneCase(s.Groups[1].Value == "x" ? v : 0, s.Groups[1].Value == "y" ? v : 0, s.Groups[3].Value == "None" ? null : int.Parse(s.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(s.Groups[4].Value, CultureInfo.InvariantCulture)));
            }
        }

        return cases;
    }

    public static IEnumerable<object[]> GridCases() => ReadZone(ZoneGrid, grid: true).Select(c => new object[] { c.Dx, c.Dy, c.First ?? -1, c.Life });

    public static IEnumerable<object[]> ScanCases() => ReadZone(ZoneScan, grid: false).Select(c => new object[] { c.Dx, c.Dy, c.First ?? -1, c.Life });

    /// <summary>The oracle's tick 4 is the tick of its status 3 (the fuse shortened to 2): its "first" is relative to it.</summary>
    private const int OracleStatusTick = 4;

    private static void AssertZone(int ticksPerFrame, int dx, int dy, int first, int lifeAtTheEnd)
    {
        using var run = new BombRun($"w2-{dx}-{dy}-{ticksPerFrame}", new[] { (300, 300) }, targets: new[] { (300 + dx, 300 + dy) });
        run.Go(ticksPerFrame, LastTick);

        var changes = 0;
        int? touchedAt = null;
        var previous = 9;
        for (var r = -4; r <= 60; r++)
        {
            var target = run.SampleAt(r).Targets[0];
            if (target.Hp != previous)
            {
                changes++;
                previous = target.Hp;
            }

            if (touchedAt is null && (target.AnimTarget == 5 || target.Hp != 9))
            {
                touchedAt = r;
            }
        }

        // The key: the first tick at which the target is touched (relative to the status 3), its life at the end and the number of ticks at which its life changed.
        Assert.Equal(first < 0 ? null : first - OracleStatusTick, touchedAt);
        Assert.Equal(lifeAtTheEnd, previous);
        Assert.Equal(first < 0 ? 0 : 1, changes);
        if (first < 0)
        {
            Assert.Equal(EntityStatus.Normal, run.Arena.Targets[0].Status);
        }
        else
        {
            Assert.NotEqual(EntityStatus.Normal, run.Arena.Targets[0].Status);
        }
    }

    [Theory]
    [MemberData(nameof(GridCases))]
    public void W2_TheTargetNearTheBomb_DiesOnceAtTheTickOfTheKey_TheTargetJustOutsideTheBoundsIsNotTouched_TheGrid(int dx, int dy, int first, int life)
        => AssertZone(1, dx, dy, first, life);

    [Theory]
    [MemberData(nameof(ScanCases))]
    public void W2_TheTargetNearTheBomb_DiesOnceAtTheTickOfTheKey_TheTargetJustOutsideTheBoundsIsNotTouched_TheScans(int dx, int dy, int first, int life)
        => AssertZone(1, dx, dy, first, life);

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(2, 33, 22)]
    [InlineData(3, -33, 22)]
    [InlineData(2, -34, 0)]
    [InlineData(3, 34, 0)]
    [InlineData(2, 0, 23)]
    [InlineData(3, 0, -23)]
    public void W2_TheZoneIsTheSameAtTwoAndThreeTicksPerFrame_TheCentreTheCornersJustInsideAndJustOutside(int ticksPerFrame, int dx, int dy)
    {
        var grid = ReadZone(ZoneGrid, grid: true);
        var c = grid.Single(x => x.Dx == dx && x.Dy == dy);
        AssertZone(ticksPerFrame, dx, dy, c.First ?? -1, c.Life);
    }

    // -----------------------------------------------------------------------------------------
    // (w4) the grass cut by a bomb born posed
    // -----------------------------------------------------------------------------------------

    private static CutBench.Cell Bush(int column, int row) => column is >= 8 and <= 12 && row is >= 10 and <= 14 ? new CutBench.Cell(0x02, 0) : default;

    [Fact]
    public void W4_ABombPosedInTheMiddleOfTheBushes_CutsTheFiveCellsOfItsBoxAtK18AndK21_TheEffects2TheSounds31AndTheGeneratorAreTheKeys_AtOneTickPerFrame()
    {
        var key = BombKey.LoadGrass(KeyW4);
        using var run = new BombRun("w4", new[] { (252, 200) }, Bush, seedAtStatusTick: true);
        run.Go(1, K + 60);

        // The cells: five cut (the bit 0x02 cleared), the twenty others untouched.
        var cut = key.Values.SelectMany(r => r.Cut).OrderBy(c => c).ToList();
        Assert.Equal(new[] { (9, 11), (9, 13), (10, 12), (11, 11), (11, 13) }, cut);
        Assert.All(cut, c => Assert.Equal(0, run.Bench.Read(c.Column, c.Row).Walkability & CellStore.CuttableWalkabilityBit));
        var stillBushes = Enumerable.Range(0, CutBench.Width * CutBench.Height).Count(i => (run.Bench.Read(i % CutBench.Width, i / CutBench.Width).Walkability & CellStore.CuttableWalkabilityBit) != 0);
        Assert.Equal(20, stillBushes);

        // The effects 2 at the centre of each cut cell, in the order of the key, at the tick of the key; none anywhere else.
        var effectsOfTheKey = key.Values.OrderBy(r => r.Tick).SelectMany(r => r.Effects.Select(e => $"k+{r.Tick} id{e.Id} ({e.X:F1}, {e.Y:F1}, {e.Z:F1})")).ToList();
        var effectsHere = run.Effects.Where(e => e.Id == 2).Select(e => $"k+{e.Tick - K} id{e.Id} ({e.X:F1}, {e.Y:F1}, {e.Z:F1})").ToList();
        Assert.Equal(5, effectsOfTheKey.Count);
        Assert.Equal(effectsOfTheKey, effectsHere);

        // The sounds: the 46 of the explosion at k+1 and one 31 per cut at the tick of the key; the 84 of an object a cut lets fall is not in the key (the machine of the oracle holds only the records of the bombs), the draws tell it.
        var soundsOfTheKey = key.Values.OrderBy(r => r.Tick).SelectMany(r => r.Sounds.Select(s => (r.Tick, s))).ToList();
        Assert.Equal(new[] { (1, 46), (18, 31), (21, 31), (21, 31), (21, 31), (21, 31) }, soundsOfTheKey);
        Assert.Equal(soundsOfTheKey, run.Heard.Played.Where(x => x.Sfx != 84).Select(x => (x.Tick - K, x.Sfx)).ToList());
        Assert.All(run.Samples.Values, s => Assert.Equal(10, s.Hero.Hp));

        // The generator after each frame is the key's next word, and it made the steps the key draws plus the one of the frame (the per-frame step of the engine), from k+1 to the last line of the key.
        var wrong = new List<string>();
        foreach (var (tick, row) in key.OrderBy(p => p.Key))
        {
            if (!key.TryGetValue(tick + 1, out var next))
            {
                continue;
            }

            var end = K + tick;
            if (run.StateAfter[end] != next.Rng0 || run.StepsDuring[end] != row.Draws + 1)
            {
                wrong.Add($"k+{tick}: state 0x{run.StateAfter[end]:X8} (key 0x{next.Rng0:X8}), steps {run.StepsDuring[end]} (key draws {row.Draws} + 1)");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));

        // The bomb itself: the same chronology as (w1), its slot free at k+54.
        Assert.False(run.At(0, 54).Present);
        Assert.Equal(key.Values.Count(r => r.Status == 3), Enumerable.Range(1, 52).Count(r => run.At(0, r).Present && run.At(0, r).Status == 3));
    }

    [Fact]
    public void W4_TheSameBombOnAFieldWithoutGrass_CutsNothing_NoEffect2_NoSound31()
    {
        using var run = new BombRun("w4-bare", new[] { (252, 200) }, null, seedAtStatusTick: true);
        run.Go(1, K + 60);

        Assert.Empty(run.Effects.Where(e => e.Id == 2));
        Assert.DoesNotContain(run.Heard.Played, x => x.Sfx == 31);
        Assert.False(run.At(0, 54).Present);
    }
}
