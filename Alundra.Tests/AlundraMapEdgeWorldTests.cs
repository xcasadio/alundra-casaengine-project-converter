#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Physics;

namespace Alundra.Tests;

/// <summary>
/// E20.e0k3 (docs/plan-e20-heros.md section 1.18.3, T2): the world tests (b1) to (b4) of the map edge for an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> (marked <see cref="AlundraEntityScriptProxy.HasLogicalZ"/>),
/// in the real world of map 14 (<see cref="ArcRun"/>, the real prefabs and controllers, a synthetic field of 52 x 60 cells whose cells go on past the bound: the edge is crossable, which (b3) proves in the same assertion), against the
/// oracle of <c>docs/plan-e20-annexe/e20-e0k/oracle/out</c> read by key (never copied). Written before the phase B wiring and red until it: the phase A function <see cref="AlundraScriptedMotion.ClampStepToMapEdge"/> exists but nothing calls it,
/// so a marked entity goes past its bound and never raises the flag. Each case without effect is set next to a positive case in the same assertion.
/// (b1) the arrow of <c>arrows2-map-edge</c> (ticks 1 to 7 ONLY: at tick 8 the oracle shows the bounce of the E17 handler, which is not ported, E20.f5) next to an arrow that is far from the edge; (b2) the continuous push of the walker,
/// east (<c>edge-push-east</c>) and north-east (<c>edge-push-diag</c>: X bounded while Y goes on, the flag kept through the slide along the wall); (b3) a walker from a record at the same place is not bounded (O-E19-17, D-E19-25);
/// (b4) the hero, pushed east past its own bound, goes past it at the tick that its speed gives.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraMapEdgeWorldTests
{
    private const int Arrow = 3;
    private const int Beannoid = 79; // the walker of the oracle (map 14, sprite 79).

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0k3-B-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static TileMapData Map(int gravity, int viscosity)
    {
        var map = new TileMapData();
        map.CustomProperties["Gravity"] = gravity.ToString();
        map.CustomProperties["ZViscosity"] = viscosity.ToString();
        return map;
    }

    /// <summary>A flat field of the whole grid of the map (52 x 60 cells of 24 x 16 px): the cells go on to x = 1 248, past the bound of every box tested here (1 237 and 1 238).</summary>
    private static void InstallFlatField(ArcRun arc)
    {
        var field = FlatCells.Create(width: 52, height: 60);
        typeof(AlundraWorldProxy).GetProperty(nameof(AlundraWorldProxy.CollisionField))!.SetValue(arc.Proxy, field);
        arc.RealWorld!.CollisionField = field;
        typeof(AlundraWorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, Map(128, 4096));
    }

    private static AlundraEntityScriptProxy Born(ArcRun arc, int sprite, bool mapPage, int xPixels, int zRaw)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, mapPage, sprite, xPixels << 16, 100 << 16, zRaw, 0x18)!;

    /// <summary>One frame of one logic tick (the clock hands out a tick per 20 ms; the small surplus keeps the float accumulator from losing one).</summary>
    private static void Tick(ArcRun arc) => arc.OneFrame(0.02f + 0.0002f);

    private static string Series(IEnumerable<int> values) => string.Join(",", values);

    /// <summary>One assertion over named parts: all of them must hold, and the message gives the whole expected and actual series of each part that does not.</summary>
    private static void AssertParts(params (string Name, string Expected, string Actual)[] parts)
    {
        var bad = parts.Where(p => p.Expected != p.Actual).Select(p => $"{p.Name}: expected [{p.Expected}] but was [{p.Actual}]").ToList();
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    private static JsonElement OracleRows(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-e0k", "oracle", "out", file);
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.Clone();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-e0k/oracle/out/{file}' found above '{AppContext.BaseDirectory}'");
    }

    private static IReadOnlyList<JsonElement> OracleSlot(JsonElement root)
    {
        var slot = root.GetProperty("meta").GetProperty("slot").GetInt32().ToString();
        return root.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("slots").GetProperty(slot)).ToList();
    }

    private static int OracleInit(JsonElement root, string field) => root.GetProperty("meta").GetProperty("init").GetProperty(field).GetInt32();

    /// <summary>The oracle's values of <paramref name="field"/> for the ticks 1 to <paramref name="ticks"/>.</summary>
    private static List<int> Column(IReadOnlyList<JsonElement> oracle, string field, int ticks) => oracle.Take(ticks).Select(s => s.GetProperty(field).GetInt32()).ToList();

    /// <summary>The walker of the oracle: the Beannoid of map 14 (the AI picks its animation 2 at the first tick, speed 0), the speed of the harness given to that animation, <paramref name="direction"/> as its target direction.</summary>
    private static void GiveWalk(AlundraEntityScriptProxy walker, int speed, uint direction)
    {
        var original = walker.AnimSetsByAnim!;
        var entry = original[2];
        walker.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>(original)
        {
            [2] = new AnimSetEntry { Anim = 2, Speed = speed, Acceleration = entry.Acceleration, IsZForceApplied = entry.IsZForceApplied, Sfx = entry.Sfx, Flags = entry.Flags, Unknown = entry.Unknown },
        };
        walker.TargetDirection = direction;
    }

    private static void HeroOutOfTheWay(ArcRun arc)
    {
        arc.Hero.PosX = 800 << 16; // entities block one another.
        arc.Hero.PosY = 800 << 16;
        arc.Hero.PushLogicalPositionToRoot();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b1) the arrow born at x = 1 200, east, on an open field: X = 1 233 at tick 6, X = 1 237 at tick 7 with the flag raised the same tick (arrows2-map-edge), next to an arrow born at x = 1 100.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B1_AnArrowBornAtX1200_IsBoundedAtTheTickOfTheOracleWithTheFlagTheSameTick_AndAnArrowBornAtX1100IsNotBoundedFor20Ticks()
    {
        // Only the ticks 1 to 7 are pinned: the oracle's tick 8 is the bounce of the E17 handler (direction 8), which E20.e0k3 does not port (E20.f5); in the port the arrow keeps its direction and is bounded again.
        var oracle = OracleSlot(OracleRows("arrows2-map-edge.json"));
        var px = Column(oracle, "px", 7);
        var step = px[5] - px[4]; // the steady step of the arrow (6 px), read from the oracle.
        using var arc = Arc("b1");
        InstallFlatField(arc);
        var near = Born(arc, Arrow, false, 1200, 16 << 16);
        var far = Born(arc, Arrow, false, 1100, 16 << 16);
        var nearX = new List<int>();
        var nearAdjusted = new List<int>();
        var farX = new List<int>();
        var farAdjusted = new List<int>();
        for (var tick = 1; tick <= 20; tick++)
        {
            Tick(arc);
            if (tick <= 7)
            {
                nearX.Add(near.PosX);
                nearAdjusted.Add(near.ForceAdjusted);
            }

            farX.Add(far.PosX);
            farAdjusted.Add(far.ForceAdjusted);
        }

        // The far arrow flies the same path 100 px behind (the oracle's first six ticks, which the bound has not touched, then the steady step), never near its bound.
        var farExpected = Enumerable.Range(1, 20).Select(t => 1100 * 65536 + (t <= 6 ? px[t - 1] - 1200 * 65536 : px[5] - 1200 * 65536 + (t - 6) * step)).ToList();
        AssertParts(
            ("near arrow X, ticks 1 to 7", Series(px), Series(nearX)),
            ("near arrow ForceAdjusted, ticks 1 to 7", Series(Column(oracle, "adj_13C", 7)), Series(nearAdjusted)),
            ("far arrow X, 20 ticks", Series(farExpected), Series(farX)),
            ("far arrow ForceAdjusted, 20 ticks", Series(Enumerable.Repeat(0, 20)), Series(farAdjusted)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b2) the continuous push of a walker against the edge: east (edge-push-east) and north-east (edge-push-diag).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (List<int> X, List<int> Y, List<int> Adjusted) PushWalker(string file, uint direction, int ticks)
    {
        var root = OracleRows(file);
        using var arc = Arc("b2-" + file);
        InstallFlatField(arc);
        HeroOutOfTheWay(arc);
        var walker = Born(arc, Beannoid, true, 1225, OracleInit(root, "pz") - 2);
        GiveWalk(walker, 160, direction);
        var x = new List<int>();
        var y = new List<int>();
        var adjusted = new List<int>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            Tick(arc);
            x.Add(walker.PosX);
            y.Add(walker.PosY);
            adjusted.Add(walker.ForceAdjusted);
        }

        return (x, y, adjusted);
    }

    [Fact]
    public void B2_AWalkerPushedEast_IsBoundedAtTheTickOfTheOracleAndStaysAtTheBound_WithTheFlagUpAtTheTickAfterToo()
    {
        const int ticks = 8; // the tick of the clamp (7) and the tick after.
        var oracle = OracleSlot(OracleRows("edge-push-east.json"));
        var (x, y, adjusted) = PushWalker("edge-push-east.json", 24, ticks);

        AssertParts(
            ("walker X", Series(Column(oracle, "px", ticks)), Series(x)),
            ("walker Y", Series(Column(oracle, "py", ticks)), Series(y)),
            ("walker ForceAdjusted", Series(Column(oracle, "adj_13C", ticks)), Series(adjusted)));
    }

    [Fact]
    public void B2_AWalkerPushedNorthEast_IsBoundedOnXWhileYGoesOn_AndTheFlagIsKeptThroughTheSlideAlongTheWall()
    {
        const int ticks = 11; // the tick of the clamp (10) and the tick after.
        var oracle = OracleSlot(OracleRows("edge-push-diag.json"));
        var (x, y, adjusted) = PushWalker("edge-push-diag.json", 20, ticks);

        AssertParts(
            ("walker X", Series(Column(oracle, "px", ticks)), Series(x)),
            ("walker Y", Series(Column(oracle, "py", ticks)), Series(y)),
            ("walker ForceAdjusted", Series(Column(oracle, "adj_13C", ticks)), Series(adjusted)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b3) a walker from a record, at the same place and with the same push, is not bounded (O-E19-17, D-E19-25): next to the marked arrow, bounded.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_ARecordWalkerAtTheEdgeIsNotBounded_WhereTheMarkedArrowIsBounded()
    {
        var oracle = OracleSlot(OracleRows("arrows2-map-edge.json"));
        using var arc = Arc("b3");
        InstallFlatField(arc);
        HeroOutOfTheWay(arc);
        for (var i = 0; i < 4; i++)
        {
            Tick(arc);
        }

        // A Beannoid of the records of the map (the entity of the record 3), put at the place of the walker of (b2) and given the same push.
        var record = arc.EntityByRecord(3)!;
        Assert.False(record.HasLogicalZ);
        foreach (var other in arc.Entities.Where(e => e != arc.Hero && e != record).ToList())
        {
            other.PosX = 400 << 16;
            other.PosY = 300 << 16;
            other.PushLogicalPositionToRoot();
        }

        GiveWalk(record, 160, 24);
        record.PosX = 1225 << 16;
        record.PosY = 100 << 16;
        record.PosZ = 1 << 16;
        record.ForceX = 0;
        record.ForceY = 0;
        record.PushLogicalPositionToRoot();
        var arrow = Born(arc, Arrow, false, 1200, 16 << 16);

        var recordX = new List<int>();
        var recordAdjusted = new List<int>();
        var arrowX = new List<int>();
        var arrowAdjusted = new List<int>();
        for (var tick = 1; tick <= 10; tick++)
        {
            Tick(arc);
            recordX.Add(record.PosX);
            recordAdjusted.Add(record.ForceAdjusted);
            if (tick <= 7)
            {
                arrowX.Add(arrow.PosX);
                arrowAdjusted.Add(arrow.ForceAdjusted);
            }
        }

        // The record walker walks on past its bound (its step every tick, exactly: the edge is crossable on this field), never flagged; the marked arrow is bounded at the tick of the oracle.
        var bound = record.ScreenClipX;
        var walkStep = 160 * 768;
        AssertParts(
            ("record walker X, 10 ticks", Series(Enumerable.Range(1, 10).Select(t => (1225 << 16) + t * walkStep)), Series(recordX)),
            ("record walker ForceAdjusted, 10 ticks", Series(Enumerable.Repeat(0, 10)), Series(recordAdjusted)),
            ("record walker past the bound at tick 10", "True", (recordX[9] > bound).ToString()),
            ("marked arrow X, ticks 1 to 7", Series(Column(oracle, "px", 7)), Series(arrowX)),
            ("marked arrow ForceAdjusted, ticks 1 to 7", Series(Column(oracle, "adj_13C", 7)), Series(arrowAdjusted)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b4) the hero pushed east goes past its own bound (D-E19-25, O-E19-17), at the tick its speed gives: next to the marked arrow, bounded.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheHeroPushedEast_GoesPastItsOwnBoundAtTheTickItsSpeedGives_WhereTheMarkedArrowIsBounded()
    {
        const int start = 1200;
        var oracle = OracleSlot(OracleRows("arrows2-map-edge.json"));
        using var arc = Arc("b4");
        InstallFlatField(arc);
        arc.Hero.TargetAnimationId = 0; // the map-entry animation ends through its chain, which the headless montage never plays.
        for (var i = 0; i < 6; i++)
        {
            Tick(arc);
        }

        arc.PlaceHero(start, 300, 0);
        var arrow = Born(arc, Arrow, false, 1200, 16 << 16);
        var bound = arc.Hero.ScreenClipX;
        arc.HoldDirections(AlundraPadState.Right);
        var heroX = new List<int>();
        var heroAdjusted = new List<int>();
        var arrowX = new List<int>();
        var arrowAdjusted = new List<int>();
        for (var tick = 1; tick <= 22; tick++)
        {
            Tick(arc);
            heroX.Add(arc.Hero.PosX);
            heroAdjusted.Add(arc.Hero.ForceAdjusted);
            if (tick <= 7)
            {
                arrowX.Add(arrow.PosX);
                arrowAdjusted.Add(arrow.ForceAdjusted);
            }
        }

        // The tick named by its speed: the first step is the start of the acceleration, then the steady step; the first tick whose X is past the bound is 1 + ceil((bound - X after the first step) / steady step).
        var first = heroX[0];
        var steady = heroX[2] - heroX[1];
        var named = 1 + (int)Math.Ceiling((bound - first) / (double)steady);
        var firstPast = heroX.FindIndex(x => x > bound) + 1;
        AssertParts(
            ("hero first tick past its bound", named.ToString(), firstPast.ToString()),
            ("hero past the bound at tick 22", "True", (heroX[21] > bound).ToString()),
            ("hero ForceAdjusted, 22 ticks", Series(Enumerable.Repeat(0, 22)), Series(heroAdjusted)),
            ("marked arrow X, ticks 1 to 7", Series(Column(oracle, "px", 7)), Series(arrowX)),
            ("marked arrow ForceAdjusted, ticks 1 to 7", Series(Column(oracle, "adj_13C", 7)), Series(arrowAdjusted)));
    }
}
