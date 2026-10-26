#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;
using Alundra.UI.Inventory;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3b T2 (docs/plan-e20-heros.md section 1.30.3, the tests of the world (w1) to (w11)): the bomb in the hands of the REAL hero, on the bench <see cref="BombHandsRun"/> (the real world of map 14 emptied of its entities, the
/// field of each scenario, the bomb given and equipped on the Circle, the Circle pressed by <see cref="ArcRun"/>). They run on the REGISTRY OF PRODUCTION and the TABLES OF PRODUCTION and register NOTHING: they are red as long as the
/// entry <c>0x23</c> of the tables of h0a is the silent stub (Circle starts nothing, the stub answers 1 with no sound) and as long as (C, 62) is not in <see cref="NativeSpriteRegistry.CreateForProduction"/> (T3, the phase B).
/// Every expected value is the oracle's, typed from its key or read from the file of the key under <c>docs/plan-e20-annexe/</c> (the wording of each test says which): the keys were made by the real code of <c>ALUN_CD.EXE</c> (France)
/// in the MIPS machine, replayed identically in the T0 of this slice (<c>docs/plan-e20-annexe/e20-h3b/README.md</c>). The tick of a key is the tick of the test (the Circle of the key's first tick is the first tick of the run), with
/// NO tolerance of one tick on (w1), (w8), (w10) and (w11). The Z of the entities is the DLL's (the binary's minus 1, ADR-0026) where the key prints the binary's: the keys print the bomb's Z at rest as 0.0 and in the hands as 32.0,
/// which are the same numbers here (the key's own ground is at 0). The columns of a key that the DLL has no counterpart for (the invulnerability and blink counters of the wound, the slot numbers) are left out.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombHandsWorldTests : IDisposable
{
    private const uint Circle = BombHandsRun.Circle;
    private const uint Square = BombHandsRun.Square;
    private const uint Cross = BombHandsRun.Cross;
    private const uint Right = BombHandsRun.Right;
    private const uint Left = BombHandsRun.Left;
    private const uint Up = BombHandsRun.Up;
    private const uint Down = BombHandsRun.Down;
    private const uint Start = BombHandsRun.Start;

    private const int Born = 1;
    private const int Normal = 2;
    private const int Deactivated = 3;
    private const int Dying = 4;

    private const uint BombCircle = 0x23;
    private const string KeyW8 = "e20-h3b/explode_in_hands.txt";
    private const string KeyW10 = "e20-h3b/k_throw_explode.txt";
    private const string KeyW11 = "e20-h3/bin/results/scenarios/bomb_hurt_while_carrying_bomb.txt";

    public BombHandsWorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        InventoryDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // The helpers.
    // -----------------------------------------------------------------------------------------

    /// <summary>The differences found against a key, listed whole so that a failure shows every column that moved.</summary>
    private sealed class Diffs
    {
        private readonly List<string> _list = new();

        internal void Eq<T>(string what, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                _list.Add($"{what}: key {expected}, test {actual}");
            }
        }

        internal void Seq<T>(string what, IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var e = expected.ToList();
            var a = actual.ToList();
            if (!e.SequenceEqual(a))
            {
                _list.Add($"{what}: key [{string.Join(", ", e)}], test [{string.Join(", ", a)}]");
            }
        }

        internal void None() => Assert.True(_list.Count == 0, $"{_list.Count} differences with the key; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, _list.Take(40))}");
    }

    /// <summary>A position as the scripts of the oracle print it: <c>'%.1f'</c> (Python rounds an exact tie to the even digit).</summary>
    private static string P1(double value) => Math.Round(value, 1, MidpointRounding.ToEven).ToString("F1", CultureInfo.InvariantCulture);

    private static string P2(double value) => Math.Round(value, 2, MidpointRounding.ToEven).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>The pad of the scripts of the reading (<c>sc_misc.basic_holds</c>): the hero turns right for the ticks 1 to 3, the Circle at 4, the Square at <paramref name="squareAt"/>.</summary>
    private static Func<int, uint> Reading(int squareAt = 0, Func<int, uint>? extra = null)
        => t => (t <= 3 ? Right : 0u) | (t == 4 ? Circle : 0u) | (squareAt != 0 && t == squareAt ? Square : 0u) | (extra?.Invoke(t) ?? 0u);

    /// <summary>The cells of the reading's field (<c>cells(col, row) -> (height, slope, walkability, ground property)</c>) as the bench's.</summary>
    private static Func<int, int, CutBench.Cell> WallColumn(int column) => (c, r) => c == column && r is >= 9 and <= 15 ? new CutBench.Cell(0x01, 0, null, 0x10) : default;

    private static Func<int, int, CutBench.Cell> DeepWaterFrom(int column) => (c, _) => c >= column ? new CutBench.Cell(0, 0, null, 0x08) : default;

    /// <summary>The words of the spark of the effect 9 the code made (the effect 9 of the bomb, the hit sparks of the wound excluded by their tick).</summary>
    private static string Words(BombHandsRun.EffectTick e) => $"id {e.Id} at ({e.X}, {e.Y}, {e.Z})";

    // -----------------------------------------------------------------------------------------
    // (w1) the chronology of a bomb started by the Circle (critic: bomb_timeline.txt, hero_hit.txt, the sparks of k_h3b_sparks.py with the per-frame step)
    // -----------------------------------------------------------------------------------------

    /// <summary>The forces of the seven sparks (<c>docs/plan-e20-annexe/e20-h3b/sparks/spark_values_pf.txt</c>, slot words <c>+0x54</c> and <c>+0x58</c>): force X, force Y.</summary>
    private static readonly (int Tick, int ForceX, int ForceY)[] SparkForces =
    {
        (187, unchecked((int)0xffffdff3), 0xe28e),
        (195, 0x9584, unchecked((int)0xffffd900)),
        (203, unchecked((int)0xffff5891), unchecked((int)0xffff1fc8)),
        (211, 0x7733, 0x56b2),
        (219, 0x1606b, unchecked((int)0xffff865f)),
        (227, unchecked((int)0xfffefed8), 0x6e59),
        (235, unchecked((int)0xffff7878), unchecked((int)0xffff21ca)),
    };

    [Fact]
    public void W1_TheCircle_BornsTheBombInTheHands_ItIsLifted_SevenSparksFall_StatusIs3At243_ItExplodesHurtingTheHero_AndLeavesItsSlotAt297_TickByTickTheKey()
    {
        using var run = new BombHandsRun("w1");
        run.Go(300, t => t == 1 ? Circle : 0, seeded: true);
        var d = new Diffs();

        // The start (tick 1): the hero in the state 5, the bomb in his hands, the item in use, the fuse at 240, the sound 44.
        var h1 = run.Hero[1];
        d.Eq("t1 hero state", 5u, h1.State);
        d.Eq("t1 hero carries", true, h1.Carrying);
        d.Eq("t1 item in use", BombCircle, h1.ItemInUse);
        d.Eq("t1 bombs in the world", 1, run.PresentAt(1).Count);
        d.Eq("t1 bomb status", Born, run.BombAt(0, 1).Status);
        d.Eq("t1 bomb fuse", 240, run.BombAt(0, 1).Fuse);
        d.Eq("t2 bomb status", Normal, run.BombAt(0, 2).Status);
        d.Eq("t2 bomb fuse", 240, run.BombAt(0, 2).Fuse);
        d.Seq("sounds of the run", new[] { (1, 44), (244, 46), (261, 23) }, run.AllSounds());

        // The fuse runs from the tick 3 (one a tick) while the bomb is carried (the keys print it at 37, 187 to 235: 205, 55, 47, ...).
        for (var t = 3; t <= 242; t++)
        {
            d.Eq($"t{t} fuse", 242 - t, run.BombAt(0, t).Fuse);
        }

        // The hero: the lift (state 5) up to 36, the carry (state 12) from 37 to 243, free (state 0) from 244; the link up to 242; the item up to 243.
        for (var t = 1; t <= 276; t++)
        {
            var hero = run.Hero[t];
            var state = t <= 36 ? 5u : t <= 243 ? 12u : t <= 260 ? 0u : t <= 264 ? 49u : t <= 275 ? 28u : 0u;
            d.Eq($"t{t} hero state", state, hero.State);
            d.Eq($"t{t} hero carries", t <= 242, hero.Carrying);
            d.Eq($"t{t} item in use", t <= 243 ? BombCircle : 0u, hero.ItemInUse);
            d.Eq($"t{t} hero hp", t <= 260 ? 10 : 5, hero.Hp);
        }

        // The Z of the bomb at the rows of the key: 32.0 in the hands, 31.5 and 30.5 once let go, 0.0 at the explosion.
        foreach (var (t, z) in new[] { (37, 32.0), (187, 32.0), (195, 32.0), (203, 32.0), (211, 32.0), (219, 32.0), (227, 32.0), (235, 32.0), (243, 31.5), (244, 30.5), (260, 0.0) })
        {
            d.Eq($"t{t} bomb z", z, run.BombAt(0, t).Z);
        }

        // The seven sparks (effect 9 at (100, 100, 40 px + 1 unit)) at 187, 195, ..., 235, with the forces the draws gave (the generator posed at the seed of the key, the per-frame step of the engine).
        var carried = run.Effects.Where(e => e.Id == 9 && e.Tick < 250).ToList();
        d.Seq("ticks of the sparks", SparkForces.Select(s => s.Tick), carried.Select(e => e.Tick));
        foreach (var spark in carried)
        {
            d.Eq($"t{spark.Tick} spark position", (6553600, 6553600, 2621441), (spark.X, spark.Y, spark.Z));
        }

        foreach (var (t, fx, fy) in SparkForces)
        {
            var spark = carried.FirstOrDefault(e => e.Tick == t);
            d.Eq($"t{t} spark force X", fx, spark.ForceX);
            d.Eq($"t{t} spark force Y", fy, spark.ForceY);
        }

        // The status 3 at 243 (the fuse at -1, the hands free), the animation 1 at 244 (E12), the end flag at 295, the status 4 at 296, the slot free at 297.
        d.Eq("t242 bomb status", Normal, run.BombAt(0, 242).Status);
        d.Eq("t243 bomb status", Deactivated, run.BombAt(0, 243).Status);
        d.Eq("t243 bomb fuse", -1, run.BombAt(0, 243).Fuse);
        d.Eq("t243 bomb linked", false, run.BombAt(0, 243).Linked);
        d.Eq("t243 bomb animation", 0u, run.BombAt(0, 243).TargetAnimation);
        d.Eq("t244 bomb animation", 1u, run.BombAt(0, 244).TargetAnimation);
        d.Eq("t294 bomb end", 0, run.BombAt(0, 294).End);
        d.Eq("t295 bomb end", 1, run.BombAt(0, 295).End);
        d.Eq("t295 bomb status", Deactivated, run.BombAt(0, 295).Status);
        d.Eq("t296 bomb status", Dying, run.BombAt(0, 296).Status);
        d.Eq("t297 bomb present", false, run.BombAt(0, 297).Present);
        d.Eq("t300 bomb present", false, run.BombAt(0, 300).Present);

        // The wound of the hero (hero_hit.txt, A4): the box at 260 gives the three sparks of the hit at (6553599, 6553599, 360448), the state 49 at 261 (hp 5, x 103), 28 at 265 (114.81), 0 at 276 (133.09); y stays 100.
        // (The seam hands over the centre in the DLL's Z, the binary's minus one unit, ADR-0026: the key's 360448 is 360447 here.)
        d.Seq("t260 centre of the hit (the three sparks are made there)", new[] { (6553599, 6553599, 360447) }, run.HitSparks.TryGetValue(260, out var hits) ? hits : new List<(int X, int Y, int Z)>());
        d.Eq("t260 sparks of the effect 9", 3, run.Effects.Count(e => e.Id == 9 && e.Tick == 260));
        d.Eq("sparks of the effect 9 in all", 7 + 3, run.Effects.Count(e => e.Id == 9));
        d.Seq("hero x up to the explosion", Enumerable.Repeat("100.00", 260), Enumerable.Range(1, 260).Select(t => P2(run.Hero[t].X)));
        d.Eq("t261 hero x", "103.00", P2(run.Hero[261].X));
        d.Eq("t265 hero x", "114.81", P2(run.Hero[265].X));
        d.Eq("t276 hero x", "133.09", P2(run.Hero[276].X));
        d.Eq("t276 hero y", "100.00", P2(run.Hero[276].Y));
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w2) three bombs at most (critic: bomb_limit3.txt)
    // -----------------------------------------------------------------------------------------

    /// <summary>The rows of the key: tick, the sounds 3, 44 and 46 of the tick, the bombs (status, fuse) in the order of their births, the hero state, the item in use.</summary>
    private static readonly (int Tick, int[] Sounds, (int Status, int Fuse)[] Bombs, uint Hero, uint Item)[] LimitRows =
    {
        (1, new[] { 44 }, new[] { (1, 240) }, 5, 0x23),
        (43, Array.Empty<int>(), new[] { (2, 199) }, 9, 0x23),
        (71, new[] { 44 }, new[] { (2, 171), (1, 240) }, 5, 0x23),
        (113, Array.Empty<int>(), new[] { (2, 129), (2, 199) }, 9, 0x23),
        (141, new[] { 44 }, new[] { (2, 101), (2, 171), (1, 240) }, 5, 0x23),
        (183, Array.Empty<int>(), new[] { (2, 59), (2, 129), (2, 199) }, 9, 0x23),
        (211, new[] { 3 }, new[] { (2, 31), (2, 101), (2, 171) }, 0, 0),
        (244, new[] { 46 }, new[] { (3, -1), (2, 68), (2, 138) }, 0, 0),
        (253, Array.Empty<int>(), new[] { (3, -1), (2, 59), (2, 129) }, 16, 0),
        (262, new[] { 46, 46 }, new[] { (3, -1), (3, 52), (3, 122) }, 16, 0),
        (281, new[] { 3 }, new[] { (3, -1), (3, 52), (3, 122) }, 0, 0),
        (323, Array.Empty<int>(), Array.Empty<(int, int)>(), 16, 0),
    };

    [Fact]
    public void W2_TheFourthStart_IsRefusedWithTheSound3_AsLongAsTheSlotOfTheFirstBombIsNotFree_AndAcceptedOnceAllAreFree_TheKey()
    {
        var circles = new[] { 1, 71, 141, 211, 281, 351 };
        var squares = new[] { 43, 113, 183, 253, 323 };
        using var run = new BombHandsRun("w2");
        run.Go(360, t => circles.Contains(t) ? Circle : squares.Contains(t) ? Square : 0);
        var d = new Diffs();

        foreach (var (tick, sounds, bombs, hero, item) in LimitRows)
        {
            d.Seq($"t{tick} sounds", sounds, run.SoundsAt(tick).Where(s => s is 3 or 44 or 46));
            d.Seq($"t{tick} bombs (status, fuse)", bombs, run.PresentAt(tick).Select(b => (b.Status, b.Fuse)));
            d.Eq($"t{tick} hero state", hero, run.Hero[tick].State);
            d.Eq($"t{tick} item in use", (uint)item, run.Hero[tick].ItemInUse);
        }

        // The table is empty by 323 (the key's last bomb leaves at 318); the start of 351 is accepted with all the slots free: one bomb, the fuse at 240, the sound 44, the hero in the state 5 with the item in use.
        // (The key prints that bomb in the status 2 at once, a trait of the oracle's recycled slots; the test compares what the plan lists: the bomb, the fuse, the sound and the hero.)
        d.Eq("t351 bombs", 1, run.PresentAt(351).Count);
        d.Eq("t351 fuse", 240, run.PresentAt(351).Select(b => b.Fuse).FirstOrDefault());
        d.Seq("t351 sounds", new[] { 44 }, run.SoundsAt(351).Where(s => s is 3 or 44 or 46));
        d.Eq("t351 hero state", 5u, run.Hero[351].State);
        d.Eq("t351 item in use", BombCircle, run.Hero[351].ItemInUse);

        // The sound 3 happens exactly twice in the run: the fourth start (211) and the one of 281 (the three slots are still in the table).
        d.Seq("ticks of the sound 3", new[] { 211, 281 }, run.AllSounds().Where(s => s.Sfx == 3).Select(s => s.Tick));
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w3) the throw in the four directions (critic: throw_dirs.txt, k_throw_explode.txt; the reading: bomb_throw_4dirs.txt, bomb_air_throw.txt)
    // -----------------------------------------------------------------------------------------

    public static TheoryData<string, uint, double, double, string, string, string, string> Throws => new()
    {
        // name, the direction bit, the bomb at its first Z = 0 (tick 72), the offset from the hero at 72, then at 73 (the rest)
        { "RIGHT", Right, 136.328125, 100.0, "34.50", "0.00", "35.25", "0.00" },
        { "LEFT", Left, 63.671875, 100.0, "-34.50", "0.00", "-35.25", "0.00" },
        { "UP", Up, 100.0, 75.78125, "0.00", "-23.00", "0.00", "-23.50" },
        { "DOWN", Down, 100.0, 124.21875, "0.00", "23.00", "0.00", "23.50" },
    };

    [Theory]
    [MemberData(nameof(Throws))]
    public void W3_TheSquare_ThrowsTheBomb_34Point5PixelsEastAndWest_23NorthAndSouth_ItLands22TicksLaterWithTheSound24_TheKey(string name, uint bit, double x72, double y72, string dx72, string dy72, string dx73, string dy73)
    {
        using var run = new BombHandsRun("w3-" + name);
        run.Go(80, t => t == 1 ? bit : t == 4 ? Circle : t == 50 ? Square : 0);
        var d = new Diffs();

        // The hero is where he threw (the key prints his position at the tick 50), the bomb leaves his hands at 50 with the sound 26 and the hero in the state 9.
        var hero = run.Hero[50];
        d.Eq("t50 hero state", 9u, hero.State);
        d.Eq("t50 bomb status", Normal, run.BombAt(0, 50).Status);
        d.Eq("t50 bomb animation", 2u, run.BombAt(0, 50).TargetAnimation);
        d.Eq("t50 bomb linked", false, run.BombAt(0, 50).Linked);
        d.Seq("sounds", new[] { (4, 44), (50, 26), (73, 24) }, run.AllSounds());

        // The first Z at or under 0.01 after the throw is the tick 72 for all of them, at the key's position.
        var first = Enumerable.Range(52, 28).First(t => run.BombAt(0, t).Z <= 0.01);
        d.Eq("tick of the landing", 72, first);
        var at72 = run.BombAt(0, 72);
        d.Eq("t72 bomb x", x72, at72.X);
        d.Eq("t72 bomb y", y72, at72.Y);
        d.Eq("t72 offset x", dx72, P2(at72.X - hero.X));
        d.Eq("t72 offset y", dy72, P2(at72.Y - hero.Y));
        d.Eq("t72 bomb animation", 2u, at72.TargetAnimation);

        // The next tick the bomb takes the animation 0 and is at rest 35.25 (east, west) or 23.50 (north, south) from the hero (k_throw_explode.txt).
        var at73 = run.BombAt(0, 73);
        d.Eq("t73 bomb animation", 0u, at73.TargetAnimation);
        d.Eq("t73 offset x", dx73, P2(at73.X - hero.X));
        d.Eq("t73 offset y", dy73, P2(at73.Y - hero.Y));
        d.None();
    }

    [Fact]
    public void W3_TheBombThrownInTheAir_LeavesAt63Point5_LandsAt106_AndPlaysTheSound24AtThe107_TheKey()
    {
        using var run = new BombHandsRun("w3-air", 200, 200);
        run.Go(115, Reading(extra: t => t == 70 ? Cross : t == 80 ? Square : 0));
        var d = new Diffs();

        d.Eq("t70 hero state", 0x2Eu, run.Hero[70].State);
        d.Eq("t71 hero state", 0x30u, run.Hero[71].State);
        d.Eq("t80 hero state", 0x0Au, run.Hero[80].State);
        d.Eq("t80 hero z", 27.5, run.Hero[80].Z);
        d.Eq("t81 hero z", 27.0, run.Hero[81].Z);
        d.Eq("t80 item in use", BombCircle, run.Hero[80].ItemInUse);
        d.Eq("t81 item in use", 0u, run.Hero[81].ItemInUse);
        d.Eq("t80 bomb animation", 2u, run.BombAt(0, 80).TargetAnimation);
        d.Eq("t80 bomb", ("207.6", "200.0", "63.5"), (P1(run.BombAt(0, 80).X), P1(run.BombAt(0, 80).Y), P1(run.BombAt(0, 80).Z)));
        d.Eq("t91 hero state", 0u, run.Hero[91].State);
        d.Eq("t106 bomb", ("246.6", "200.0", "0.0"), (P1(run.BombAt(0, 106).X), P1(run.BombAt(0, 106).Y), P1(run.BombAt(0, 106).Z)));
        d.Eq("t106 bomb on the ground", 1, run.BombAt(0, 106).OnGround);
        d.Eq("t106 bomb animation", 2u, run.BombAt(0, 106).TargetAnimation);
        d.Eq("t107 bomb x", "247.3", P1(run.BombAt(0, 107).X));
        d.Eq("t107 bomb animation", 0u, run.BombAt(0, 107).TargetAnimation);
        // The key also plays the sound 11 at 91 (the hero's landing from his jump, not the bomb's): left out, the bomb's family (44, 10 and 26 of the hero's jump and throw, 24) is compared.
        d.Seq("sounds", new[] { (4, 44), (70, 10), (80, 26), (107, 24) }, run.AllSounds().Where(s => s.Sfx != 11));
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w4) the single bounce on a wall in the air, the landing at the foot of a wall (the reading: bomb_wall.txt, bomb_wall2.txt)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W4_AWallInTheFlight_BouncesTheBombOnce_AtTheTick51WithASpark_AndTheHalfTurn_ItLandsAt74AndPlaysTheSound24AtThe75_TheKey()
    {
        using var run = new BombHandsRun("w4-wall", 200, 200, WallColumn(9));
        run.Go(120, Reading(squareAt: 50));
        var d = new Diffs();

        d.Eq("t50 hero x", "205.0", P1(run.Hero[50].X));
        d.Eq("t50 bomb", ("205.0", "200.0", "36.0"), (P1(run.BombAt(0, 50).X), P1(run.BombAt(0, 50).Y), P1(run.BombAt(0, 50).Z)));
        d.Eq("t50 bomb animation", 2u, run.BombAt(0, 50).TargetAnimation);
        d.Eq("t50 bomb direction", 24u, run.BombAt(0, 50).Direction);

        // The bounce (the key's lock +0x278 = 1): the direction takes the half turn, the bomb is on its way back, one spark of the effect 9 at the position it had before the move.
        d.Eq("t51 bomb direction", 8u, run.BombAt(0, 51).Direction);
        d.Eq("t51 bomb", ("203.5", "200.0", "40.0"), (P1(run.BombAt(0, 51).X), P1(run.BombAt(0, 51).Y), P1(run.BombAt(0, 51).Z)));
        d.Eq("t51 bomb animation", 2u, run.BombAt(0, 51).TargetAnimation);
        d.Seq("the sparks of the flight", new[] { (51, 13434880, 13107200, 2883585) }, run.Effects.Where(e => e.Id == 9 && e.Tick is >= 50 and <= 80).Select(e => (e.Tick, e.X, e.Y, e.Z)));
        for (var t = 52; t <= 75; t++)
        {
            d.Eq($"t{t} bomb direction (one bounce only)", 8u, run.BombAt(0, t).Direction);
        }

        d.Eq("t74 bomb", ("169.0", "200.0", "0.0"), (P1(run.BombAt(0, 74).X), P1(run.BombAt(0, 74).Y), P1(run.BombAt(0, 74).Z)));
        d.Eq("t74 bomb on the ground", 1, run.BombAt(0, 74).OnGround);
        d.Eq("t74 bomb animation", 2u, run.BombAt(0, 74).TargetAnimation);
        d.Eq("t75 bomb x", "168.2", P1(run.BombAt(0, 75).X));
        d.Eq("t75 bomb animation", 0u, run.BombAt(0, 75).TargetAnimation);
        d.Seq("sound 24", new[] { 75 }, run.AllSounds().Where(s => s.Sfx == 24).Select(s => s.Tick));
        d.None();
    }

    public static TheoryData<int, string, string, string, string, string, bool> FootOfAWall => new()
    {
        // the wall column, the bomb at 72 (x, z), at 73 (x), the Z at 67 (a wall in the air stops it at 229.0 / 27.5; none: not compared), the sound 24 at 73
        { 10, "229.0", "0.0", "229.0", "27.5", "229.0", false },
        { 11, "240.6", "0.0", "241.3", "", "", true },
    };

    [Theory]
    [MemberData(nameof(FootOfAWall))]
    public void W4_AWallColumnAtTheLandingPoint_StopsTheBombAtItsFoot_WithNoSound24_OrLetsItLandWithTheSound24_TheKey(int column, string x72, string z72, string x73, string z67, string x67, bool sound24)
    {
        using var run = new BombHandsRun("w4-foot-" + column, 200, 200, WallColumn(column));
        run.Go(110, Reading(squareAt: 50));
        var d = new Diffs();

        d.Eq("t50 hero x", "206.1", P1(run.Hero[50].X));
        d.Eq("t50 bomb", ("207.6", "200.0", "36.0"), (P1(run.BombAt(0, 50).X), P1(run.BombAt(0, 50).Y), P1(run.BombAt(0, 50).Z)));
        d.Eq("t50 bomb direction", 24u, run.BombAt(0, 50).Direction);
        if (z67 != "")
        {
            d.Eq("t67 bomb", (x67, "200.0", z67), (P1(run.BombAt(0, 67).X), P1(run.BombAt(0, 67).Y), P1(run.BombAt(0, 67).Z)));
        }

        d.Eq("t72 bomb x", x72, P1(run.BombAt(0, 72).X));
        d.Eq("t72 bomb z", z72, P1(run.BombAt(0, 72).Z));
        d.Eq("t72 bomb on the ground", 1, run.BombAt(0, 72).OnGround);
        d.Eq("t72 bomb animation", 2u, run.BombAt(0, 72).TargetAnimation);
        d.Eq("t73 bomb x", x73, P1(run.BombAt(0, 73).X));
        d.Eq("t73 bomb animation", 0u, run.BombAt(0, 73).TargetAnimation);
        for (var t = 50; t <= 73; t++)
        {
            d.Eq($"t{t} bomb direction (no bounce)", 24u, run.BombAt(0, t).Direction);
        }

        d.Seq("sound 24", sound24 ? new[] { 73 } : Array.Empty<int>(), run.AllSounds().Where(s => s.Sfx == 24).Select(s => s.Tick));
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w5) the lock of the control while the bomb is carried (the reading: bomb_locked.txt)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W5_TheLockOfTheControl_WhileTheBombIsCarried_PutsItOutInSmokeAtTheNextTick_NoExplosion_TheKey()
    {
        using var run = new BombHandsRun("w5", 200, 200);
        run.Go(300, Reading(), beforeTick: t =>
        {
            if (t == 61)
            {
                GameState.Instance.PlayerControlFlags = GameState.PlayerControlBits.ForcedSequence;
            }
        });
        var d = new Diffs();

        d.Eq("t60 hero state", 0x0Cu, run.Hero[60].State);
        d.Eq("t60 hero carries", true, run.Hero[60].Carrying);
        d.Eq("t60 bomb status", Normal, run.BombAt(0, 60).Status);

        // Tick 61: the hero keeps the carry state, lets go, the item is freed; the bomb is flagged to destroy at the hero's side (Z 32.0) with the smoke (effect 12) at its position; gone at 62.
        d.Eq("t61 hero state", 0x0Cu, run.Hero[61].State);
        d.Eq("t61 hero carries", false, run.Hero[61].Carrying);
        d.Eq("t61 item in use", 0u, run.Hero[61].ItemInUse);
        d.Eq("t61 bomb status", Dying, run.BombAt(0, 61).Status);
        d.Eq("t61 bomb", ("206.1", "200.0", "32.0"), (P1(run.BombAt(0, 61).X), P1(run.BombAt(0, 61).Y), P1(run.BombAt(0, 61).Z)));
        d.Seq("the smoke", new[] { (61, 13506560, 13107200, 2097153) }, run.Effects.Where(e => e.Id == 12).Select(e => (e.Tick, e.X, e.Y, e.Z)));
        d.Eq("t62 bomb present", false, run.BombAt(0, 62).Present);
        d.Eq("bombs ever born", 1, run.Births);

        // No explosion: no bomb in the world to the end, no sound 46, no spark of the effect 9, the hero keeps his ten points.
        d.Eq("bombs in the world at 300", 0, run.PresentAt(300).Count);
        d.Eq("sound 46", 0, run.AllSounds().Count(s => s.Sfx == 46));
        d.Eq("effects 9", 0, run.Effects.Count(e => e.Id == 9));
        d.Eq("t300 hero hp", 10, run.Hero[300].Hp);
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w6a) carried into the deep water (the reading: bomb_water.txt); (w6b) thrown into it (critic: water.txt; the reading: bomb_thrown_in_water.txt)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W6a_TheBombCarriedIntoTheDeepWater_IsPutOutInSmokeAt80_TheItemIsFreed_NoExplosion_TheKey()
    {
        using var run = new BombHandsRun("w6a", 200, 200, DeepWaterFrom(10), extraItems: new[] { 0x1B });
        run.Go(120, Reading(extra: t => t > 60 ? Right : 0u));
        var d = new Diffs();

        d.Eq("t61 hero state", 0x07u, run.Hero[61].State);
        d.Eq("t61 hero x", "208.5", P1(run.Hero[61].X));
        d.Eq("t61 hero carries", true, run.Hero[61].Carrying);
        d.Eq("t79 bomb status", Normal, run.BombAt(0, 79).Status);

        // Tick 80: the hero swims (0x1D), has let go, the item is freed, the bomb is flagged to destroy with the smoke at (252.4, 200, 32 + 1); gone at 81 (the hero state 0x0F, the sound 22).
        d.Eq("t80 hero state", 0x1Du, run.Hero[80].State);
        d.Eq("t80 hero x", "254.5", P1(run.Hero[80].X));
        d.Eq("t80 hero carries", false, run.Hero[80].Carrying);
        d.Eq("t80 item in use", 0u, run.Hero[80].ItemInUse);
        d.Eq("t80 bomb status", Dying, run.BombAt(0, 80).Status);
        d.Eq("t80 bomb", ("252.4", "200.0", "32.0"), (P1(run.BombAt(0, 80).X), P1(run.BombAt(0, 80).Y), P1(run.BombAt(0, 80).Z)));
        d.Seq("the smoke", new[] { (80, 16541696, 13107200, 2097153) }, run.Effects.Where(e => e.Id == 12).Select(e => (e.Tick, e.X, e.Y, e.Z)));
        d.Eq("t81 bomb present", false, run.BombAt(0, 81).Present);
        d.Eq("t81 hero state", 0x0Fu, run.Hero[81].State);
        d.Eq("t81 hero x", "256.6", P1(run.Hero[81].X));
        d.Seq("sounds", new[] { (4, 44), (81, 22) }, run.AllSounds());
        d.Eq("sound 46", 0, run.AllSounds().Count(s => s.Sfx == 46));
        d.None();
    }

    [Fact]
    public void W6b_TheBombThrownIntoTheDeepWater_MakesTheEffect6AtTheLanding_IsStatus4_AndLeavesTheNextTick_TheCriticKey()
    {
        using var run = new BombHandsRun("w6b-critic", 100, 100, DeepWaterFrom(5));
        run.Go(90, t => t == 1 ? Circle : t == 42 ? Square : 0);
        var d = new Diffs();

        // water.txt: t60 (status 2, animation 2, x 128.5, Z 22.5), t61 (130.0, 17.0), t65 status 4 at (134.5, 0.0) with the effect 6, t66 gone.
        d.Eq("t60 bomb", (Normal, 2u, "128.5", "22.5"), (run.BombAt(0, 60).Status, run.BombAt(0, 60).TargetAnimation, P1(run.BombAt(0, 60).X), P1(run.BombAt(0, 60).Z)));
        d.Eq("t61 bomb", (Normal, 2u, "130.0", "17.0"), (run.BombAt(0, 61).Status, run.BombAt(0, 61).TargetAnimation, P1(run.BombAt(0, 61).X), P1(run.BombAt(0, 61).Z)));
        d.Eq("t65 bomb", (Dying, 2u, "134.5", "0.0"), (run.BombAt(0, 65).Status, run.BombAt(0, 65).TargetAnimation, P1(run.BombAt(0, 65).X), P1(run.BombAt(0, 65).Z)));
        d.Seq("the effect 6", new[] { 65 }, run.Effects.Where(e => e.Id == 6).Select(e => e.Tick));
        d.Eq("t66 bomb present", false, run.BombAt(0, 66).Present);
        d.Eq("sound 46", 0, run.AllSounds().Count(s => s.Sfx == 46));
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w7) the Circle during a stroke of the sword (critic R1: states_sample.txt)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(4, new[] { 0, 5, 12, 16 })]
    [InlineData(14, new[] { 0, 5, 16 })]
    public void W7_TheCircleDuringAStrokeOfTheSword_StartsTheBomb_AndTheStrokeIsInterrupted_TheKey(int circleAt, int[] states)
    {
        using var run = new BombHandsRun("w7-" + circleAt);
        run.Go(40, t => t == 2 ? Square : t == circleAt ? Circle : 0);
        var d = new Diffs();

        d.Seq("sounds of the start", new[] { circleAt }, run.AllSounds().Where(s => s.Sfx == 44).Select(s => s.Tick));
        d.Seq("sounds of the refusal", Array.Empty<int>(), run.AllSounds().Where(s => s.Sfx == 3).Select(s => s.Tick));
        d.Seq("hero states of the run", states, run.Hero.Values.Select(h => (int)h.State).Distinct().OrderBy(s => s));
        d.Eq("the stroke was struck (state 16 before the Circle)", 16u, run.Hero[circleAt - 1].State);
        d.Eq("the bomb was born by the Circle (state 5 at the Circle)", 5u, run.Hero[circleAt].State);
        d.Eq("the bomb is in the hands", true, run.Hero[circleAt].Carrying);
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w8) the explosion in the hands (critic A4: explode_in_hands.txt)
    // -----------------------------------------------------------------------------------------

    /// <summary>One printed row of a key of the critic (hurt_circle): the hero, the bombs by slot number (status, target animation, end flag), the sounds.</summary>
    private sealed record ExplodeRow(int Tick, int Hp, uint State, string X, bool Linked, uint Item, Dictionary<int, (int Status, int Animation, int End)> Bombs, int[] Sounds);

    private static readonly Regex ExplodeLine = new(@"^t(\d+) hero hp +(-?\d+) state 0x([0-9a-f]+) x +(-?[\d.]+) link 0x([0-9a-f]+) item 0x([0-9a-f]+) \| \{(.*)\} \| sounds \[(.*)\]$", RegexOptions.Compiled);
    private static readonly Regex ExplodeBomb = new(@"(\d+): \((\d+), (\d+), (\d+)\)", RegexOptions.Compiled);

    private static List<ExplodeRow> ReadExplodeKey()
    {
        var rows = new List<ExplodeRow>();
        foreach (var text in File.ReadAllLines(AnnexFiles.BombAnnex(KeyW8)))
        {
            var m = ExplodeLine.Match(text);
            if (!m.Success)
            {
                continue;
            }

            var bombs = ExplodeBomb.Matches(m.Groups[7].Value).ToDictionary(
                b => int.Parse(b.Groups[1].Value, CultureInfo.InvariantCulture),
                b => (int.Parse(b.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(b.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(b.Groups[4].Value, CultureInfo.InvariantCulture)));
            var sounds = m.Groups[8].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            rows.Add(new ExplodeRow(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), Convert.ToUInt32(m.Groups[3].Value, 16), m.Groups[4].Value,
                Convert.ToUInt32(m.Groups[5].Value, 16) != 0, Convert.ToUInt32(m.Groups[6].Value, 16), bombs, sounds));
        }

        return rows;
    }

    [Fact]
    public void W8_ASecondBombBornInTheBoxOfTheFirst_AfterTheWound_ExplodesInTheHands_TheHeroIsNotHurtTwice_TheLinkAndTheItemStayUntil318_TickByTickTheKey()
    {
        var key = ReadExplodeKey();
        Assert.Equal(240, key[0].Tick);
        Assert.Equal(318, key[^1].Tick);

        using var run = new BombHandsRun("w8");
        run.Go(320, t => t is 1 or 262 ? Circle : 0, seeded: true);
        var d = new Diffs();

        // A tick without a row holds the state of the last row before it (the key prints only the changes) and plays no sound.
        for (var t = 240; t <= 320; t++)
        {
            var row = key.Last(r => r.Tick <= t);
            var printed = row.Tick == t;
            var hero = run.Hero[t];
            d.Eq($"t{t} hero hp", row.Hp, hero.Hp);
            d.Eq($"t{t} hero state", row.State, hero.State);
            d.Eq($"t{t} hero x", row.X, P2(hero.X));
            d.Eq($"t{t} hero carries (the link)", row.Linked, hero.Carrying);
            d.Eq($"t{t} item in use", row.Item, hero.ItemInUse);
            d.Seq($"t{t} sounds", printed ? row.Sounds.Where(s => s != 0) : Array.Empty<int>(), run.SoundsAt(t));
            for (var slot = 1; slot <= 2; slot++)
            {
                var bomb = run.BombAt(slot - 1, t);
                if (row.Bombs.TryGetValue(slot, out var expected))
                {
                    d.Eq($"t{t} bomb {slot}", (expected.Status, (uint)expected.Animation, expected.End), (bomb.Present ? bomb.Status : -1, bomb.TargetAnimation, bomb.End));
                }
                else
                {
                    d.Eq($"t{t} bomb {slot} present", false, bomb.Present);
                }
            }
        }

        // The wound once, at 261 (hp 10 -> 5), none at the second explosion (the counter of the wound is not zero when the box of the second bomb touches him).
        d.Eq("t260 hp", 10, run.Hero[260].Hp);
        d.Eq("t261 hp", 5, run.Hero[261].Hp);
        d.Eq("t261 hero state", 0x31u, run.Hero[261].State);
        d.Eq("hp from 261 on", 0, Enumerable.Range(261, 60).Count(t => run.Hero[t].Hp != 5));
        d.Eq("the second explosion finds the wound counter set", true, run.Hero[265].DamagedTickCounter != 0);
        d.Seq("the sounds of the run", new[] { (1, 44), (244, 46), (261, 23), (262, 44), (265, 46) }, run.AllSounds());
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w9) the Start menu is refused from the start to T+243 and opens after (the items in use of bomb_timeline.txt: 0x23 up to the tick 243, 0 from 244)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, false)]
    [InlineData(100, false)]
    [InlineData(243, false)]
    [InlineData(244, true)]
    [InlineData(245, true)]
    public void W9_TheStartMenu_IsRefusedWhileTheBombIsInUse_FromTheStartToTheTick243_AndOpensAfter(int startAt, bool opens)
    {
        using var run = new BombHandsRun("w9-" + startAt);
        run.Go(startAt, t => t == 1 ? Circle : t == startAt ? Start : 0);
        var d = new Diffs();

        d.Eq("the Circle of the tick 1 puts the bomb in use", BombCircle, run.Hero[1].ItemInUse);
        d.Eq($"the item in use at the tick {startAt}", startAt <= 243 ? BombCircle : 0u, run.Hero[startAt].ItemInUse);
        d.Eq($"the menu open after the Start of the tick {startAt}", opens, InventoryDirector.Instance.IsActive);
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w10) the throw downwards hurts the hero who stays where he threw (the a362 reading: k_throw_explode.txt)
    // -----------------------------------------------------------------------------------------

    public static TheoryData<string, uint, string, string, string, string, int, int> Zones => new()
    {
        // the direction, the bit, the hero at the tick 60 (x, y), the bomb at rest minus the hero (x, y), the first hurt tick (0: none), the final hp
        { "RIGHT", Right, "101.83", "100.00", "35.25", "0.00", 0, 10 },
        { "LEFT", Left, "98.17", "100.00", "-35.25", "0.00", 0, 10 },
        { "UP", Up, "100.00", "98.78", "0.00", "-23.50", 0, 10 },
        { "DOWN", Down, "100.00", "101.22", "0.00", "23.50", 273, 5 },
    };

    [Theory]
    [MemberData(nameof(Zones))]
    public void W10_TheBombThrownDownwards_LandsHalfAPixelInsideItsZone_TheHeroWhoStaysIsHurtAtTheTick273_TheOtherDirectionsPassBy_TheKey(string name, uint bit, string hx, string hy, string dx, string dy, int hurtAt, int finalHp)
    {
        // The key's file is the source of the values typed here (one line a direction): it must say what the data says.
        var line = File.ReadAllLines(AnnexFiles.BombAnnex(KeyW10)).Single(l => l.StartsWith(name, StringComparison.Ordinal));
        Assert.Contains($"hero at ({hx},{hy})", line);
        Assert.Contains($"offset ({dx},{dy})", line);
        Assert.Contains(hurtAt == 0 ? "first hurt: None" : $"first hurt: ({hurtAt}, {finalHp}, '0x31')", line);

        using var run = new BombHandsRun("w10-" + name);
        run.Go(330, t => t == 1 ? bit : t == 4 ? Circle : t == 50 ? Square : 0);
        var d = new Diffs();

        var hero = run.Hero[60];
        var rest = run.BombAt(0, 200);
        d.Eq("hero at the tick 60", (hx, hy), (P2(hero.X), P2(hero.Y)));
        d.Eq("bomb at rest at the tick 200 (offset from the hero)", (dx, dy), (P2(rest.X - hero.X), P2(rest.Y - hero.Y)));
        var firstHurt = Enumerable.Range(101, 230).Cast<int?>().FirstOrDefault(t => run.Hero[t!.Value].Hp < 10);
        d.Eq("first tick the hero is hurt", hurtAt == 0 ? null : hurtAt, firstHurt);
        if (hurtAt != 0)
        {
            d.Eq("hp at the first hurt tick", finalHp, run.Hero[hurtAt].Hp);
            d.Eq("state at the first hurt tick", 0x31u, run.Hero[hurtAt].State);
        }

        d.Eq("final hp", finalHp, run.Hero[330].Hp);
        d.None();
    }

    // -----------------------------------------------------------------------------------------
    // (w11) the hero hurt while he carries a bomb (the reading: bomb_hurt_while_carrying_bomb.txt)
    // -----------------------------------------------------------------------------------------

    private sealed record HurtRow(int Tick, int Hp, uint State, bool Linked, uint Item, string X, Dictionary<int, HurtBomb> Bombs);

    private sealed record HurtBomb(int Status, int Animation, bool Linked, string X, string Y, string Z);

    private static readonly Regex HurtLine = new(@"^t(\d+) hero hp=(-?\d+) ta=0x([0-9a-f]+) c2c=0x([0-9a-f]+) inuse=0x([0-9a-f]+) inv1D0=-?\d+ blink1CC=-?\d+ x=(-?[\d.]+)(.*)$", RegexOptions.Compiled);
    private static readonly Regex HurtBombPart = new(@"B(\d+) st=(\d+) ta=(\d+) c28=0x([0-9a-f]+) t24=-?\d+ pos=\((-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\) fz=-?\d+", RegexOptions.Compiled);

    private static List<HurtRow> ReadHurtKey()
    {
        var rows = new List<HurtRow>();
        foreach (var text in File.ReadAllLines(AnnexFiles.BombAnnex(KeyW11)))
        {
            var m = HurtLine.Match(text);
            if (!m.Success)
            {
                continue;
            }

            var bombs = HurtBombPart.Matches(m.Groups[7].Value).ToDictionary(
                b => int.Parse(b.Groups[1].Value, CultureInfo.InvariantCulture),
                b => new HurtBomb(
                    int.Parse(b.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(b.Groups[3].Value, CultureInfo.InvariantCulture), Convert.ToUInt32(b.Groups[4].Value, 16) != 0,
                    b.Groups[5].Value, b.Groups[6].Value, b.Groups[7].Value));
            rows.Add(new HurtRow(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), Convert.ToUInt32(m.Groups[3].Value, 16),
                Convert.ToUInt32(m.Groups[4].Value, 16) != 0, Convert.ToUInt32(m.Groups[5].Value, 16), m.Groups[6].Value, bombs));
        }

        return rows;
    }

    [Fact]
    public void W11_TheHeroHurtWhileHeCarriesABomb_DropsItInTheSameTick264_TheItemIsFreedAt265_ItRestsAt287Unbroken_AndExplodesAtItsOwnTime_TickByTickTheKey()
    {
        var key = ReadHurtKey();
        Assert.Equal(1, key[0].Tick);
        Assert.Equal(386, key[^1].Tick);

        using var run = new BombHandsRun("w11", 200, 200);
        run.Go(390, Reading(squareAt: 50, extra: t => t == 90 ? Circle : t >= 140 && (run.HeroProxy.PosX >> 16) < 222 ? Right : 0u));
        var d = new Diffs();

        // A row of the key prints the whole line (the hero and every bomb of the type 2); a tick without a row holds the columns of the key (the hero's points, state, link and item, the bombs' status, animation and link) of the row before it.
        for (var t = 1; t <= 386; t++)
        {
            var row = key.Last(r => r.Tick <= t);
            var printed = row.Tick == t;
            var hero = run.Hero[t];
            d.Eq($"t{t} hero hp", row.Hp, hero.Hp);
            d.Eq($"t{t} hero state", row.State, hero.State);
            d.Eq($"t{t} hero carries (the link)", row.Linked, hero.Carrying);
            d.Eq($"t{t} item in use", row.Item, hero.ItemInUse);
            if (printed)
            {
                d.Eq($"t{t} hero x", row.X, P1(hero.X));
            }

            for (var slot = 1; slot <= 2; slot++)
            {
                var bomb = run.BombAt(slot - 1, t);
                if (row.Bombs.TryGetValue(slot, out var expected))
                {
                    d.Eq($"t{t} bomb {slot} (status, animation, link)", (expected.Status, (uint)expected.Animation, expected.Linked), (bomb.Present ? bomb.Status : -1, bomb.TargetAnimation, bomb.Linked));
                    if (printed)
                    {
                        d.Eq($"t{t} bomb {slot} position", (expected.X, expected.Y, expected.Z), (P1(bomb.X), P1(bomb.Y), P1(bomb.Z)));
                    }
                }
                else
                {
                    d.Eq($"t{t} bomb {slot} present", false, bomb.Present);
                }
            }
        }

        // The facts of the plan, read from the key: the wound and the drop in the same tick 264, the item freed at 265, the landing at 286, the rest at 287 (animation 0), the fuse going on: status 3 at 332.
        d.Eq("t263 hero hp", 10, run.Hero[263].Hp);
        d.Eq("t264 hero hp", 5, run.Hero[264].Hp);
        d.Eq("t264 hero state", 0x31u, run.Hero[264].State);
        d.Eq("t264 the bomb is let go", false, run.BombAt(1, 264).Linked);
        d.Eq("t264 the bomb flies", 2u, run.BombAt(1, 264).TargetAnimation);
        d.Eq("t264 item in use", BombCircle, run.Hero[264].ItemInUse);
        d.Eq("t265 item in use", 0u, run.Hero[265].ItemInUse);
        d.Eq("t286 the bomb lands (Z 0)", 0.0, run.BombAt(1, 286).Z);
        d.Eq("t287 the bomb rests", 0u, run.BombAt(1, 287).TargetAnimation);
        d.Eq("t287 the bomb is not broken", Normal, run.BombAt(1, 287).Status);
        d.Eq("t331 the bomb is in the status 2", Normal, run.BombAt(1, 331).Status);
        d.Eq("t332 the fuse is over: status 3", Deactivated, run.BombAt(1, 332).Status);
        d.Seq("the sounds 23 and 24", new[] { (264, 23), (287, 24) }, run.AllSounds().Where(s => s.Sfx is 23 or 24));
        d.None();
    }
}
