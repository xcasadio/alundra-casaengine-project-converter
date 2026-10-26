#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3a T2: a key of the oracle of the bomb read from its file under <c>docs/plan-e20-annexe/</c> (the lines the scripts <c>k_w1_posed.py</c>, <c>k_w3_chain.py</c> and <c>sc_grass_placed_pf.py</c> print, copied there by T0 and by the arc A362). The scripts print a line only when the state or the journal
/// changed from the previous tick, so a tick without a line holds the state of the last line before it and an empty journal (<see cref="At"/> carries the state forward). Ticks are relative to K, the tick at the end of which the harness put the bomb in status 3 (K = 5 in the files; k+0 is that tick).
/// Nothing is typed in the tests: every expected value is read here.
/// </summary>
internal sealed class BombKey
{
    private static readonly Regex Head = new(@"^k\s*([+-]\d+)\s", RegexOptions.Compiled);
    private static readonly Regex OneBomb = new(@"bomb\(st,tanim,canim,end,z,box\)=\((-?\d+), (-?\d+), (-?\d+), (-?\d+), (-?[\d.]+), (-?\d+)\)", RegexOptions.Compiled);
    private static readonly Regex Hero = new(@"hero\(hp,state,touching,1cc,1d0\)=\((-?\d+), '(0x[0-9a-f]+)', '(0x[0-9a-f]+)', (-?\d+), (-?\d+)\) x=(-?[\d.]+) y=(-?[\d.]+)", RegexOptions.Compiled);
    private static readonly Regex TwoBombs = new(@"bomb1\(st,anim,end,hitc,box\)=\((\d+), (\d+), (\d+), (\d+), (\d+)\) bomb2=\((\d+), (\d+), (\d+), (\d+), (\d+)\) hero\(hp,state,touching\)=\((\d+), (\d+), (\d+)\)", RegexOptions.Compiled);
    private static readonly Regex Sound = new(@"\('sound', (\d+)\)", RegexOptions.Compiled);
    private static readonly Regex Sounds = new(@"sounds \[([\d, ]*)\]", RegexOptions.Compiled);
    private static readonly Regex Spark = new(@"\('effect_3BDD8', \(0, 9\)\)", RegexOptions.Compiled);
    private static readonly Regex Grass = new(@"^k\s*([+-]\d+) rng0=([0-9A-F]{8}) rng1=([0-9A-F]{8}) draws=(\d+) bomb\(st,tanim,box\)=\((\d+), (\d+), (\d+)\) hero\(hp\)=(\d+)(?: cut=\[(.*?)\])?(?: fx=\[(.*?)\])?(?: snd=\[(.*?)\])?$", RegexOptions.Compiled);

    private BombKey(Dictionary<int, Row> lines) => Lines = lines;

    /// <summary>One printed line.</summary>
    internal sealed record Row(
        int Tick, int Status, int TargetAnimation, int CurrentAnimation, int End, bool Box,
        int Status2, int TargetAnimation2, int End2, int Hit1, int Hit2, bool Box2,
        int HeroHp, int HeroState, bool HeroTouching, int HeroDamaged, int HeroCooldown, double HeroX, double HeroY,
        IReadOnlyList<int> Sounds, int SparkSets);

    internal Dictionary<int, Row> Lines { get; }

    /// <summary>The last tick of the file.</summary>
    internal int Last => Lines.Keys.Max();

    private static string Path(string relative) => AnnexFiles.BombAnnex(relative);

    /// <summary>The key of one bomb and the hero (<c>k_w1_posed.py</c>: (w1) and (w6)).</summary>
    internal static BombKey LoadOneBomb(string relative)
    {
        var lines = new Dictionary<int, Row>();
        foreach (var text in File.ReadAllLines(Path(relative)))
        {
            var head = Head.Match(text);
            var bomb = OneBomb.Match(text);
            var hero = Hero.Match(text);
            if (!head.Success || !bomb.Success || !hero.Success)
            {
                continue;
            }

            var tick = int.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
            var sounds = Sound.Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            lines[tick] = new Row(
                tick, I(bomb, 1), I(bomb, 2), I(bomb, 3), I(bomb, 4), I(bomb, 6) != 0,
                0, 0, 0, 0, 0, false,
                I(hero, 1), Convert.ToInt32(hero.Groups[2].Value, 16), Convert.ToInt32(hero.Groups[3].Value, 16) != 0, I(hero, 4), I(hero, 5), D(hero, 6), D(hero, 7),
                sounds, Spark.Matches(text).Count / 3);
        }

        return new BombKey(lines);
    }

    /// <summary>The key of the chain of two bombs (<c>k_w3_chain.py</c>: (w3)).</summary>
    internal static BombKey LoadTwoBombs(string relative)
    {
        var lines = new Dictionary<int, Row>();
        foreach (var text in File.ReadAllLines(Path(relative)))
        {
            var head = Head.Match(text);
            var two = TwoBombs.Match(text);
            if (!head.Success || !two.Success)
            {
                continue;
            }

            var tick = int.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
            var sounds = Sounds.Match(text);
            var ids = sounds.Success ? sounds.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList() : new List<int>();
            lines[tick] = new Row(
                tick, I(two, 1), I(two, 2), I(two, 2), I(two, 3), I(two, 5) != 0,
                I(two, 6), I(two, 7), I(two, 8), I(two, 4), I(two, 9), I(two, 10) != 0,
                I(two, 11), I(two, 12), I(two, 13) != 0, 0, 0, 100, 100,
                ids, 0);
        }

        return new BombKey(lines);
    }

    /// <summary>The row in force at <paramref name="tick"/>: the last printed line at or before it; the sounds and the sparks only when the tick has a line of its own.</summary>
    internal Row At(int tick)
    {
        var printed = Lines.TryGetValue(tick, out var own);
        var source = printed ? own! : Lines[Lines.Keys.Where(k => k <= tick).Max()];
        return printed ? source : source with { Sounds = Array.Empty<int>(), SparkSets = 0 };
    }

    private static int I(Match m, int group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    private static double D(Match m, int group) => double.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    /// <summary>One line of the key of the grass.</summary>
    internal sealed record GrassRow(int Tick, uint Rng0, uint Rng1, int Draws, int Status, int TargetAnimation, bool Box, int HeroHp, IReadOnlyList<(int Column, int Row)> Cut, IReadOnlyList<(int Id, double X, double Y, double Z)> Effects, IReadOnlyList<int> Sounds);

    /// <summary>The key of the grass cut by a bomb born posed (<c>sc_grass_placed_pf.py</c>: (w4)): one line per tick from k+1 to the free slot.</summary>
    internal static IReadOnlyDictionary<int, GrassRow> LoadGrass(string relative)
    {
        var rows = new Dictionary<int, GrassRow>();
        foreach (var text in File.ReadAllLines(Path(relative)))
        {
            var m = Grass.Match(text);
            if (!m.Success)
            {
                continue;
            }

            var cut = m.Groups[9].Success
                ? Regex.Matches(m.Groups[9].Value, @"\((\d+), (\d+)\)").Select(c => (int.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(c.Groups[2].Value, CultureInfo.InvariantCulture))).ToList()
                : new List<(int, int)>();
            var effects = m.Groups[10].Success
                ? Regex.Matches(m.Groups[10].Value, @"\((\d+), (-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)").Select(c => (int.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(c.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(c.Groups[3].Value, CultureInfo.InvariantCulture), double.Parse(c.Groups[4].Value, CultureInfo.InvariantCulture))).ToList()
                : new List<(int, double, double, double)>();
            var sounds = m.Groups[11].Success
                ? m.Groups[11].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList()
                : new List<int>();
            var tick = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            rows[tick] = new GrassRow(
                tick, Convert.ToUInt32(m.Groups[2].Value, 16), Convert.ToUInt32(m.Groups[3].Value, 16), I(m, 4), I(m, 5), I(m, 6), I(m, 7) != 0, I(m, 8), cut, effects, sounds);
        }

        return rows;
    }
}
