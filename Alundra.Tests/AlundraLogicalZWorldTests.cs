#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Physics;

namespace Alundra.Tests;

/// <summary>
/// E20.e0k2 (docs/plan-e20-heros.md section 1.18.2, T2): the world tests (z1) to (z10) of the logical Z of an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>, in the real world of map 14
/// (<see cref="ArcRun"/>, the real prefabs and controllers, a synthetic field of cells), against the oracle of <c>docs/plan-e20-annexe/e20-e0k/oracle/out</c> read by key (never copied), in the DLL's frame (the oracle
/// minus ONE raw unit, ADR-0026: the criterion of the main session after the probe P1 of E20.e0k1). Written before the phase B wiring and red until it: each one asserts an effect that the phase A code does not produce
/// (the raw unit lost by the root, the engine that anchors or steps, the StepHeight that is not 0 in the air, the latch lost after a teleport). Each case without effect is set next to a positive case in the same assertion.
/// (z1) the flight at the height of the hero keeps its raw unit; (z2) an arrow over a step of 16 px is not on the ground, against 32 px it is stopped; (z3) the ball falls on the series of the oracle; (z4) an impulse of 1 024
/// lands at the tick of the oracle, flat, on 16 px and on 32 px; (z5) a freeze and a thaw in the air; (z6) the root follows the logical Z and the engine does not move it between two ticks; (z7) the StepHeight rule;
/// (z8) the same series at 1, 2 and 3 ticks per frame; (z9) the magnet of 3 px, on the descending side where the oracle tells it from the engine (the keys magnet-down-3px and magnet-down-4px of the annex: on the ascending side the engine already steps 3 px, the key magnet-3px is green before the wiring and
/// the key magnet-4px, a step shortened by 4 raw units, is out of reach of any mover that works in whole pixels); (z10) a teleport in flight.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraLogicalZWorldTests
{
    private const int Arrow = 3;
    private const int Bomb = 2;
    private const int FireBall = 11;
    private const int Beannoid = 79; // the walker of the oracle (map 14, sprite 79).

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0k2-Z-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static TileMapData Map(int gravity, int viscosity)
    {
        var map = new TileMapData();
        map.CustomProperties["Gravity"] = gravity.ToString();
        map.CustomProperties["ZViscosity"] = viscosity.ToString();
        return map;
    }

    private static void Install(ArcRun arc, CellsCollisionField field)
    {
        typeof(WorldProxy).GetProperty(nameof(WorldProxy.CollisionField))!.SetValue(arc.Proxy, field);
        arc.RealWorld!.CollisionField = field;
        typeof(WorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, Map(128, 4096));
    }

    private static CellsCollisionField Flat() => FlatCells.Create();

    /// <summary>The height byte <paramref name="heightByte"/> (16 px) on the columns <paramref name="from"/> to <paramref name="to"/> - 1, flat elsewhere.</summary>
    private static CellsCollisionField Steps(int from, int to, int heightByte) => FlatCells.Create(cell: (x, y) => (0, x >= from && x < to ? heightByte : 0));

    /// <summary>The descending ramp of the oracle (k0_walker.py E2): columns 0 to 2 at height byte 4, columns 3 to 6 at 4..1 with the slope 2, flat 0 after.</summary>
    private static CellsCollisionField RampDown()
        => FlatCells.Create(cell: (x, y) => (0, x < 3 ? 4 : x <= 6 ? 4 - (x - 3) : 0), slope: (x, y) => x >= 3 && x <= 6 ? 2 : 0);

    private static EntityScriptProxy Born(ArcRun arc, bool mapPage, int sprite, int xPixels, int zRaw)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, mapPage, sprite, xPixels << 16, 100 << 16, zRaw, 0x18)!;

    /// <summary>One frame of <paramref name="ticks"/> logic ticks (the clock hands out a tick per 20 ms; the small surplus keeps the float accumulator from losing one).</summary>
    private static void Frame(ArcRun arc, int ticks) => arc.OneFrame(ticks * 0.02f + 0.0002f);

    private static void Frames(ArcRun arc, int ticksPerFrame, int ticks, Action<int>? afterFrame = null)
    {
        for (var tick = ticksPerFrame; tick <= ticks; tick += ticksPerFrame)
        {
            Frame(arc, ticksPerFrame);
            afterFrame?.Invoke(tick);
        }
    }

    private static string Series(IEnumerable<int> values) => string.Join(",", values);

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

    /// <summary>The first tick (1-based) whose value of <paramref name="field"/> is not 0; 0 when there is none.</summary>
    private static int FirstTick(IReadOnlyList<JsonElement> oracle, string field)
        => oracle.Select((s, i) => (s, i)).Where(p => p.s.GetProperty(field).GetInt32() != 0).Select(p => p.i + 1).DefaultIfEmpty(0).First();

    private static float RootZ(EntityScriptProxy p) => ArcRun.EntityOf(p)!.RootComponent!.LocalTransform.Position.Z;

    /// <summary>A record entity at rest on the ground after ten settling frames: the witness that the entities from a record are not touched.</summary>
    private static EntityScriptProxy Companion(ArcRun arc)
    {
        for (var i = 0; i < 10; i++)
        {
            Frame(arc, 1);
        }

        return arc.Entities.First(e => !ReferenceEquals(e, arc.Hero) && !e.HasLogicalZ && e.Controller != null && e.IsOnGround == 1);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z1) an entity born without gravity at the height of the hero keeps its raw unit and is not on the ground, for 30 ticks.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Z1_AnArrowBornAtTheHeightOfTheHero_KeepsHeroPlusOneRawUnitAndIsNotOnTheGroundFor30Ticks_WhereTheRecordEntityIsUnchanged(int ticksPerFrame)
    {
        using var arc = Arc("z1-" + ticksPerFrame);
        Install(arc, Flat());
        var companion = Companion(arc);
        var companionAtRest = (companion.PosZ, companion.IsOnGround);
        var arrow = Born(arc, false, Arrow, 100, arc.Hero.PosZ);

        var z = new List<int>();
        var ground = new List<int>();
        var companionSeries = new List<(int, int)>();
        Frames(arc, ticksPerFrame, 30, _ =>
        {
            z.Add(arrow.PosZ);
            ground.Add(arrow.IsOnGround);
            companionSeries.Add((companion.PosZ, companion.IsOnGround));
        });

        var samples = 30 / ticksPerFrame;
        Assert.Equal(
            (Series(Enumerable.Repeat(arc.Hero.PosZ + 1, samples)), Series(Enumerable.Repeat(0, samples)), true),
            (Series(z), Series(ground), companionSeries.All(c => c == companionAtRest)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z2) an arrow 16 px up over a step of 16 px is not on the ground; against a step of 32 px it is stopped at the tick of the oracle.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z2_AnArrowOver16PxIsNotOnTheGroundAtAnyTick_AndAgainst32PxItIsStoppedAtTheTickOfTheOracle()
    {
        const int ticks = 60;
        var h1Root = OracleRows("arrows-arrow-h1.json");
        var h1 = OracleSlot(h1Root);
        var h2 = OracleSlot(OracleRows("arrows-arrow-h2.json"));

        List<int> Fly(int heightByte, Func<EntityScriptProxy, int> read, out int firstAdjusted)
        {
            using var arc = Arc("z2-" + heightByte);
            Install(arc, Steps(15, 30, heightByte));
            var arrow = Born(arc, false, Arrow, 100, 16 << 16);
            var values = new List<int>();
            var adjusted = 0;
            Frames(arc, 1, ticks, tick =>
            {
                values.Add(read(arrow));
                if (adjusted == 0 && arrow.ForceAdjusted != 0)
                {
                    adjusted = tick;
                }
            });
            firstAdjusted = adjusted;
            return values;
        }

        var ground16 = Fly(1, a => a.IsOnGround, out _);
        var z16 = Fly(1, a => a.PosZ, out _);
        Fly(2, a => a.PosZ, out var adjusted32);

        Assert.Equal(
            (Series(Column(h1, "ground_144", ticks)), Series(Column(h1, "pz", ticks).Select(v => v - 1)), FirstTick(h2, "adj_13C")),
            (Series(ground16), Series(z16), adjusted32));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z3) the ball: the series of Z of the oracle from tick 1, the landing at tick 11, the flag at tick 7 against a step of 32 px, the pose at tick 8 on a step of 16 px.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Z3_TheBall_FollowsTheSeriesOfTheOracleFromTheFirstTick_AndLandsAtTheTickOfTheOracle(int ticksPerFrame)
    {
        const int ticks = 12;
        var oracle = OracleSlot(OracleRows("arrows-ball-fire.json"));
        using var arc = Arc("z3-" + ticksPerFrame);
        Install(arc, Flat());
        var ball = Born(arc, false, FireBall, 100, 32 << 16);

        var z = new List<int>();
        var ground = new List<int>();
        Frames(arc, ticksPerFrame, ticks, _ =>
        {
            z.Add(ball.PosZ);
            ground.Add(ball.IsOnGround);
        });

        var sampled = Enumerable.Range(1, ticks / ticksPerFrame).Select(i => i * ticksPerFrame - 1).ToList();
        Assert.Equal(
            (Series(sampled.Select(i => oracle[i].GetProperty("pz").GetInt32() - 1)), Series(sampled.Select(i => oracle[i].GetProperty("ground_144").GetInt32()))),
            (Series(z), Series(ground)));
    }

    [Fact]
    public void Z3_TheBallAgainstAStepOf32Px_IsStoppedAtTheTickOfTheOracleAndFallsToTheGround_AndOnAStepOf16Px_ItIsPosedOnItAtTheTickOfTheOracle()
    {
        const int ticks = 14;
        var step32 = OracleSlot(OracleRows("arrows-ball-step32.json"));
        var step16 = OracleSlot(OracleRows("arrows-ball-step16.json"));

        (int Adjusted, int Ground, string Z) Fly(int heightByte)
        {
            using var arc = Arc("z3s-" + heightByte);
            Install(arc, Steps(6, 12, heightByte));
            var ball = Born(arc, false, FireBall, 100, 32 << 16);
            var z = new List<int>();
            var adjusted = 0;
            var ground = 0;
            Frames(arc, 1, ticks, tick =>
            {
                z.Add(ball.PosZ);
                if (adjusted == 0 && ball.ForceAdjusted != 0)
                {
                    adjusted = tick;
                }

                if (ground == 0 && ball.IsOnGround != 0)
                {
                    ground = tick;
                }
            });
            return (adjusted, ground, Series(z));
        }

        var at32 = Fly(2);
        var at16 = Fly(1);
        Assert.Equal(
            (FirstTick(step32, "adj_13C"), FirstTick(step32, "ground_144"), Series(Column(step32, "pz", ticks).Select(v => v - 1)), FirstTick(step16, "ground_144"), Series(Column(step16, "pz", ticks).Select(v => v - 1))),
            (at32.Adjusted, at32.Ground, at32.Z, at16.Ground, at16.Z));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z4) an entity with gravity thrown by an impulse of 1 024 from 32 px lands at the tick of the oracle: flat 23, 21 over 16 px, 18 over 32 px.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z4_AnImpulseOf1024_LandsAtTheTickOfTheOracleFlatOver16PxAndOver32Px_OnTheSeriesOfTheCrateMinusOneRawUnit()
    {
        // The three throws in one assertion. Measured on the phase A code (E20.e0k2 T2): the flat throw (23) and the one over 16 px (21) already match the oracle, because the birth of the crate is a whole number of pixels
        // in the DLL's frame (the raw unit that the root loses is not there); the throw over 32 px lands one tick EARLY (the `<=` landing of the controller path against the strict test of the binary): that row is the red one.
        var flat = Throw("crate-flat-state1.json", 0);
        var over16 = Throw("crate-step16.json", 1);
        var over32 = Throw("crate-step32.json", 2);
        Assert.Equal(
            (flat.Expected, over16.Expected, over32.Expected),
            (flat.Actual, over16.Actual, over32.Actual));
    }

    private static ((int Landing, string Z) Expected, (int Landing, string Z) Actual) Throw(string file, int heightByte)
    {
        var root = OracleRows(file);
        var oracle = OracleSlot(root);
        var landing = FirstTick(oracle, "collz_140");
        using var arc = Arc("z4-" + heightByte);
        Install(arc, heightByte == 0 ? Flat() : Steps(5, 39, heightByte));
        // The record of the crate has 32 px + 1 raw unit; the DLL's frame is that minus one, and the interface adds its own unit to the argument. The thrown entity is the bomb (gravity) put on its animation 2, whose
        // impulse is 1 024 and whose speed is 128 (1.5 px per tick, the crate's state 1): the impulse is taken by the animation, at the first tick, as the game takes it. Over a step the series of Z does not depend on
        // the horizontal speed as long as the entity is above the step when it reaches it and stays over it (the step columns run to the edge of the field).
        var bomb = Born(arc, false, Bomb, 100, OracleInit(root, "pz") - 2);
        bomb.TargetAnimationId = 2;
        var z = new List<int>();
        var ground = 0;
        for (var tick = 1; tick <= landing + 1; tick++)
        {
            Frame(arc, 1);
            z.Add(bomb.PosZ);
            if (ground == 0 && bomb.CollidedWithEntityZ != 0)
            {
                ground = tick; // the contact flag (+0x140), the oracle's collz_140: the landing proper, not the grounded state that an exact graze already gives.
            }
        }

        return ((landing, Series(Column(oracle, "pz", landing + 1).Select(v => v - 1))), (ground, Series(z)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z5) a freeze and a thaw in the air: the Z goes on from where it stopped, no anchoring.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z5_AFreezeAndAThawInTheAir_LetTheZGoOnOnTheSeriesOfTheOracle_AndNeverAnchorTheEntity_WhereTheRecordEntityIsUnchanged()
    {
        const int ticks = 12;
        var oracle = OracleSlot(OracleRows("arrows-ball-fire.json"));
        using var arc = Arc("z5");
        Install(arc, Flat());
        var companion = Companion(arc);
        var companionAtRest = (companion.PosZ, companion.IsOnGround);
        var ball = Born(arc, false, FireBall, 100, 32 << 16);
        var flush = Born(arc, false, Arrow, 100, 0); // 1 raw unit above the floor: the engine would anchor it down at the thaw.
        var terrain = flush.ComputeTerrainHeight();
        Assert.Equal(terrain + 1, flush.PosZ);

        var ballZ = new List<int>();
        var flushZ = new List<int>();
        var companionSeries = new List<(int, int)>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            if (tick == 5)
            {
                arc.Proxy.GameState.PlayerControlFlags |= GameState.PlayerControlBits.MenuOpen; // the freeze: three frames without any tick.
                for (var i = 0; i < 3; i++)
                {
                    Frame(arc, 1);
                }

                arc.Proxy.GameState.PlayerControlFlags &= ~GameState.PlayerControlBits.MenuOpen; // the thaw.
            }

            Frame(arc, 1);
            ballZ.Add(ball.PosZ);
            flushZ.Add(flush.PosZ);
            companionSeries.Add((companion.PosZ, companion.IsOnGround));
        }

        // The freeze lasts no tick: the ball's series is the oracle's, tick for tick, as if the three frozen frames had not happened.
        Assert.Equal(
            (Series(Enumerable.Range(0, ticks).Select(i => oracle[i].GetProperty("pz").GetInt32() - 1)), Series(Enumerable.Repeat(terrain + 1, ticks)), true),
            (Series(ballZ), Series(flushZ), companionSeries.All(c => c == companionAtRest)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z6) the root follows the logical Z, and the engine does not move it between two ticks.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Z6_TheRootFollowsTheLogicalZToTheRawUnit_AndAnEngineUpdateWithoutATickDoesNotMoveIt(int ticksPerFrame)
    {
        const int ticks = 9;
        var oracle = OracleSlot(OracleRows("arrows-ball-fire.json"));
        using var arc = Arc("z6-" + ticksPerFrame);
        Install(arc, Flat());
        var ball = Born(arc, false, FireBall, 100, 32 << 16);

        var z = new List<int>();
        var gaps = new List<float>();
        var moved = new List<float>();
        Frames(arc, ticksPerFrame, ticks, _ =>
        {
            z.Add(ball.PosZ);
            gaps.Add(Math.Abs(RootZ(ball) * 65536f - ball.PosZ));
            var before = RootZ(ball);
            arc.OneFrame(0.001f); // the engine's own update, no logic tick (the clock hands out none for 1 ms).
            moved.Add(Math.Abs(RootZ(ball) - before));
        });

        var sampled = Enumerable.Range(1, ticks / ticksPerFrame).Select(i => i * ticksPerFrame - 1).ToList();
        Assert.Equal(
            (Series(sampled.Select(i => oracle[i].GetProperty("pz").GetInt32() - 1)), true, true),
            (Series(z), gaps.All(g => g < 0.5f), moved.All(m => m == 0f)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z7) the StepHeight rule: 0 in the air, the value of the prefab at rest with gravity, 0 without gravity.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z7_TheStepHeight_IsZeroInTheAir_TheValueOfThePrefabAtRestWithGravity_AndZeroWithoutGravity()
    {
        using var arc = Arc("z7");
        Install(arc, Flat());
        var bomb = Born(arc, false, Bomb, 100, 32 << 16);
        var arrow = Born(arc, false, Arrow, 100, 0);
        var prefabValue = bomb.Controller!.Settings.StepHeight;
        Assert.True(prefabValue > 0f);

        var bombSteps = new List<float>();
        var bombGround = new List<int>();
        var arrowSteps = new List<float>();
        Frames(arc, 1, 14, _ =>
        {
            bombSteps.Add(bomb.Controller!.Settings.StepHeight);
            bombGround.Add(bomb.IsOnGround);
            arrowSteps.Add(arrow.Controller!.Settings.StepHeight);
        });

        // In the air (IsOnGround 0, ForceZ negative) the bomb's step height is 0; once it lands (ForceZ 0 at rest with gravity) it is the prefab's; the arrow has no gravity: 0 all along.
        var airborne = bombGround.Select((g, i) => (g, i)).Where(p => p.g == 0).Select(p => bombSteps[p.i]).ToList();
        var resting = bombGround.Select((g, i) => (g, i)).Where(p => p.g != 0).Select(p => bombSteps[p.i]).ToList();
        Assert.Equal(
            (true, true, 0f, prefabValue),
            (airborne.Count > 0 && resting.Count > 0, arrowSteps.All(s => s == 0f), airborne.Max(), resting.Min()));
        Assert.Equal(prefabValue, resting.Max());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z8) the same series at 1, 2 and 3 ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z8_TheBallAndTheFlushArrow_HaveTheSameStateAtTicks6And12_Whatever1_2Or3TicksPerFrame_AndItIsTheOracles()
    {
        var oracle = OracleSlot(OracleRows("arrows-ball-fire.json"));

        string Run(int ticksPerFrame)
        {
            using var arc = Arc("z8-" + ticksPerFrame);
            Install(arc, Flat());
            var ball = Born(arc, false, FireBall, 100, 32 << 16);
            var flush = Born(arc, false, Arrow, 100, 0); // 1 raw unit above the floor.
            var samples = new List<string>();
            Frames(arc, ticksPerFrame, 12, tick =>
            {
                if (tick % 6 == 0)
                {
                    samples.Add($"{tick}:{ball.PosZ}/{ball.IsOnGround}/{ball.ForceZ}/{flush.PosZ}/{flush.IsOnGround}");
                }
            });
            return string.Join(" ", samples);
        }

        // The same state at the ticks 6 and 12 whatever the grouping, and it is the oracle's: the ball on its series minus one raw unit (in the air at tick 6, landed at tick 12), the arrow flush at one raw unit and never on the ground.
        string Expected(int tick)
        {
            var row = oracle[tick - 1];
            return $"{tick}:{row.GetProperty("pz").GetInt32() - 1}/{row.GetProperty("ground_144").GetInt32()}/{(row.GetProperty("ground_144").GetInt32() != 0 ? 0 : row.GetProperty("fz_B8").GetInt32())}/1/0";
        }

        var expected = Expected(6) + " " + Expected(12);
        Assert.Equal(
            (expected, expected, expected),
            (Run(1), Run(2), Run(3)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z9) the magnet of 3 px.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static EntityScriptProxy Walker(ArcRun arc, int speed, int xPixels, int zRaw)
    {
        var walker = Born(arc, true, Beannoid, xPixels, zRaw);
        // The AI of the Beannoid picks its animation 2 at the first tick (speed 0): the speed of the harness (the oracle overrides the walk the same way) is given to that animation, east.
        var original = walker.AnimSetsByAnim!;
        var entry = original[2];
        walker.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>(original)
        {
            [2] = new AnimSetEntry { Anim = 2, Speed = speed, Acceleration = entry.Acceleration, IsZForceApplied = entry.IsZForceApplied, Sfx = entry.Sfx, Flags = entry.Flags, Unknown = entry.Unknown },
        };
        walker.TargetDirection = 24;
        return walker;
    }

    [Theory]
    [InlineData("magnet-down-3px.json", 320)]
    [InlineData("magnet-down-4px.json", 480)]
    public void Z9_TheMagnetOfTheBinary_PosesAWalkerOnTheLowerGroundWithin3Px_AndLeavesItInTheAirBeyond_OnTheSeriesOfTheOracle(string file, int speed)
    {
        const int ticks = 40;
        var root = OracleRows(file);
        var oracle = OracleSlot(root);
        using var arc = Arc("z9-" + speed);
        Install(arc, RampDown());
        arc.Hero.PosX = 800 << 16; // out of the way of the walker (entities block one another).
        arc.Hero.PosY = 800 << 16;
        arc.Hero.PushLogicalPositionToRoot();
        var walker = Walker(arc, speed, 40, OracleInit(root, "pz") - 2);
        Assert.Equal(OracleInit(root, "pz") - 1, walker.PosZ);

        var z = new List<int>();
        var ground = new List<int>();
        Frames(arc, 1, ticks, _ =>
        {
            z.Add(walker.PosZ);
            ground.Add(walker.IsOnGround);
        });

        Assert.Equal(
            (Series(Column(oracle, "ground_144", ticks)), Series(Column(oracle, "pz", ticks).Select(v => v - 1))),
            (Series(ground), Series(z)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z10) a teleport in flight: the entity is not set back on the ground by the engine.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z10_ATeleportInFlight_KeepsTheMarkedEntityInTheAirAgainstAnEngineUpdateWithoutATick_WhereTheRecordEntityIsSetBackOnTheGround()
    {
        using var arc = Arc("z10");
        Install(arc, Flat());
        var companion = Companion(arc);
        var ball = Born(arc, false, FireBall, 100, 40 << 16);
        for (var i = 0; i < 3; i++)
        {
            Frame(arc, 1);
        }

        // The scripted position write (the opcodes 0x64, 0x65 and 0x8B end on PushLogicalPositionToRoot): both entities are put 3 px up, inside the ground snap of the engine (4 px).
        foreach (var entity in new[] { ball, companion })
        {
            entity.PosZ = 3 << 16;
            entity.ForceZ = 0;
            entity.PushLogicalPositionToRoot();
        }

        arc.OneFrame(0.001f); // an engine update with no logic tick.
        arc.OneFrame(0.001f);

        Assert.Equal(
            (3f, 0f),
            (MathF.Round(RootZ(ball), 3), MathF.Round(RootZ(companion), 3)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (z11) what the engine's support block would otherwise write for a marked entity: the terrain height field follows the entity, the force of the tick survives the landing, the ground flag is not read back from the controller.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z11_TheMarkedEntity_KeepsTheTerrainHeightFieldAtItsPosition_TheForceOfTheLandingTick_AndTheLogicalGroundFlagAcrossAnEngineUpdate()
    {
        // (1) an arrow flying 16 px up crosses onto a step of 16 px (the cells are 24 px wide: the step starts at x = 360, tick 44): the terrain height field (written for every entity at the end of the step) is the terrain under the box at each tick, and it does rise to the step.
        using var flight = Arc("z11-terrain");
        Install(flight, Steps(15, 30, 1));
        var arrow = Born(flight, false, Arrow, 100, 16 << 16);
        var stale = new List<int>();
        var fields = new List<int>();
        Frames(flight, 1, 60, _ =>
        {
            fields.Add(arrow.TerrainHeight);
            stale.Add(arrow.TerrainHeight - arrow.ComputeTerrainHeight());
        });

        // (2) the ball: on the tick of its landing the force of the tick is still the fall's (the support block of the engine would overwrite it with the force after the landing, 0), the force is 0.
        using var fall = Arc("z11-force");
        Install(fall, Flat());
        var ball = Born(fall, false, FireBall, 100, 32 << 16);
        var landingTick = FirstTick(OracleSlot(OracleRows("arrows-ball-fire.json")), "ground_144");
        var forces = new List<(int Force, int TickForce)>();
        Frames(fall, 1, landingTick, _ => forces.Add((ball.ForceZ, ball.TickForceZ)));

        // (3) the ball that has landed, then engine updates without a logic tick: the logical ground flag stays 1 (the controller, whose vertical is held outside, does not report it).
        using var rest = Arc("z11-ground");
        Install(rest, Flat());
        var landed = Born(rest, false, FireBall, 100, 32 << 16);
        Frames(rest, 1, landingTick + 2);
        rest.OneFrame(0.001f);
        rest.OneFrame(0.001f);

        // (4) a thaw followed by engine updates without a logic tick: the thaw hands the controller the sentinel, not the force of the entity (0 for the arrow one raw unit above the floor, which the engine would anchor down), so the root does not move.
        using var thaw = Arc("z11-thaw");
        Install(thaw, Flat());
        var falling = Born(thaw, false, Arrow, 100, 0);
        Frames(thaw, 1, 5);
        var rootBefore = RootZ(falling);
        thaw.Proxy.GameState.PlayerControlFlags |= GameState.PlayerControlBits.MenuOpen;
        Frame(thaw, 1);
        thaw.Proxy.GameState.PlayerControlFlags &= ~GameState.PlayerControlBits.MenuOpen;
        thaw.OneFrame(0.001f);
        thaw.OneFrame(0.001f);
        thaw.OneFrame(0.001f);

        Assert.Equal(
            (true, 16 << 16, 0, true, true, 1, true, 0f),
            (stale.All(s => s == 0), fields.Max(), forces[^1].Force, forces[^1].TickForce < 0, forces[0].TickForce == forces[0].Force, landed.IsOnGround, falling.PosZ == falling.ComputeTerrainHeight() + 1, RootZ(falling) - rootBefore));
    }
}
