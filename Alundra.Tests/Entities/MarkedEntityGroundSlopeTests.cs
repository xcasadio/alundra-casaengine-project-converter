#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Tests.Ai.Projectiles;
using Alundra.Tests.Support;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.f6a T1 (docs/plan-e20-heros.md section 1.22.1 and the "Revision au HEAD" bullet "f6a"): an entity born by the sprite-index interface (the marker <see cref="EntityScriptProxy.HasLogicalZ"/>) that has the gravity gets its tile attributes (<see cref="EntityScriptProxy.Slope_18c"/>, the four-corner rule of
/// the binary's <c>0x80038064</c>) with the tick, in the active statuses, as the binary's physics list does (D-E19-29): the fire and ice balls (sprite types 11 and 14, born by the test hook) are the entities that show it. The values are those of the oracle's keys (<c>docs/plan-e20-annexe/e20-f4f6/bin/out/json/</c> and
/// <c>f6a/oracle/out/json/</c>), read by <see cref="BallOracle"/>; the ball carries no handler (C60 and E9 are E20.f6b), so the comparisons stop at the landing tick (S+10 on a flat field, S+7 on the step) except on deep water, where the choice of the program of the tick S+11 destroys the ball with the break effect 6.
/// Each "no effect" case is run next to a case that reads a positive value in the same test.
/// </summary>
public class MarkedEntityGroundSlopeTests
{
    private const int Fire = 16;
    private const int Ice = 14;

    private static Func<int, int, CutBench.Cell> Dry(params (int Column, int Row)[] dry)
        => BallRun.Field((column, row) => column >= 5 && column <= 9 && !dry.Contains((column, row)) ? (0, 0x08) : (0, 0));

    private static Func<int, int, CutBench.Cell> Step16()
        => BallRun.Field((column, row) => column >= 5 && column <= 9 ? (column >= 6 ? 1 : 0, 0x08) : (0, 0));

    /// <summary>The scenarios of the oracle: the key, the rod, the cells.</summary>
    private static readonly Dictionary<string, (int Item, Func<int, int, CutBench.Cell> Cells)> Scenarios = new()
    {
        ["fire11_gp8_deepwater"] = (Fire, BallRun.Columns(5, 9, 0x08)),
        ["ice14_gp8_deepwater"] = (Ice, BallRun.Columns(5, 9, 0x08)),
        ["fire11_gp2_slope1"] = (Fire, BallRun.Columns(5, 9, 0x02)),
        ["fire11_gp4_slope2"] = (Fire, BallRun.Columns(5, 9, 0x04)),
        ["fire11_gp6_slope3"] = (Fire, BallRun.Columns(5, 9, 0x06)),
        ["fire11_mixed_footprint_water"] = (Fire, BallRun.Columns(7, 9, 0x08)),
        ["fire11_dry_corner_c6r5"] = (Fire, Dry((6, 5))),
        ["fire11_dry_corner_c7r5"] = (Fire, Dry((7, 5))),
        ["fire11_dry_corner_c6r6"] = (Fire, Dry((6, 6))),
        ["fire11_dry_corner_c7r6"] = (Fire, Dry((7, 6))),
        ["fire11_step16_water"] = (Fire, Step16()),
        ["ice14_step16_water"] = (Ice, Step16()),
    };

    public static IEnumerable<object[]> PositiveKeys() => new[]
    {
        new object[] { "fire11_gp8_deepwater", 4 },
        new object[] { "ice14_gp8_deepwater", 4 },
        new object[] { "fire11_gp2_slope1", 1 },
        new object[] { "fire11_gp4_slope2", 2 },
        new object[] { "fire11_gp6_slope3", 3 },
    };

    public static IEnumerable<object[]> ZeroKeys() => new[]
    {
        "fire11_mixed_footprint_water", "fire11_dry_corner_c6r5", "fire11_dry_corner_c7r5", "fire11_dry_corner_c6r6", "fire11_dry_corner_c7r6", "fire11_step16_water", "ice14_step16_water",
    }.Select(k => new object[] { k });

    /// <summary>Runs the scenario to its landing tick at one tick per frame and checks the whole flight against the key: the line (status, X, Z, slope, grounded) of every tick from S to the landing. Returns the run (the caller disposes it) and the landing tick.</summary>
    private static (BallRun Run, BallOracle Key, int Landing) RunToLanding(string key)
    {
        var (item, cells) = Scenarios[key];
        var oracle = BallOracle.Load(key);
        var landing = oracle.LandingTick();
        var run = new BallRun(key, item, cells);
        try
        {
            run.Go(1, landing);
            Assert.Equal(BallOracle.Birth, run.BirthTick);
            return (run, oracle, landing);
        }
        catch
        {
            run.Dispose();
            throw;
        }
    }

    private static void AssertFlightEqualsTheKey(BallRun run, BallOracle key, int landing)
    {
        for (var tick = BallOracle.Birth; tick <= landing; tick++)
        {
            Assert.Equal(key.Line(tick), run.Line(tick));
        }
    }

    /// <summary>The control every zero case is run next to: the ball on the deep water of the oracle reads 4 at its landing tick, and 0 in the air before it.</summary>
    private static void AssertTheDeepWaterBallReadsFour()
    {
        using var control = new BallRun("control", Fire, BallRun.Columns(5, 9, 0x08));
        var landing = BallOracle.Load("fire11_gp8_deepwater").LandingTick();
        control.Go(1, landing);
        Assert.Equal(4, control.SlopeAt(landing));
    }

    [Theory]
    [MemberData(nameof(PositiveKeys))]
    public void S1a_AMarkedBallAtRestOnTheGroundProperty_ReadsTheSlopeOfTheKey_AndZeroInTheAirBeforeIt(string key, int expected)
    {
        var (run, oracle, landing) = RunToLanding(key);
        using (run)
        {
            Assert.Equal(expected, oracle.Value(landing, "slope_18C"));
            AssertFlightEqualsTheKey(run, oracle, landing);
            Assert.Equal(expected, run.SlopeAt(landing));
            for (var tick = BallOracle.Birth; tick < landing; tick++)
            {
                Assert.Equal(0, run.SlopeAt(tick)); // in the air: no corner is posed
            }
        }
    }

    [Theory]
    [MemberData(nameof(ZeroKeys))]
    public void S1b_AMarkedBallWhoseFootprintIsNotWhollyOnTheWater_ReadsZero_NextToTheDeepWaterBallThatReadsFour(string key)
    {
        AssertTheDeepWaterBallReadsFour();
        var (run, oracle, landing) = RunToLanding(key);
        using (run)
        {
            Assert.Equal(0, oracle.Value(landing, "slope_18C"));
            AssertFlightEqualsTheKey(run, oracle, landing);
            Assert.Equal(0, run.SlopeAt(landing));
        }
    }

    [Fact]
    public void S1c_AMarkedBallWithoutTheGravity_ReadsZero_NextToTheSameBallThatReadsFour()
    {
        var (run, oracle, landing) = RunToLanding("fire11_gp8_deepwater");
        using (run)
        {
            var ball = run.Born!;
            Assert.Equal(4, run.SlopeAt(landing)); // the positive: the ball at rest on the deep water
            ball.Flags &= ~EntityFlags.Gravity;
            ball.Slope_18c = 7; // a sentinel the pass must overwrite with 0 (the gate of the gravity)
            ball.TickPhysics();
            Assert.Equal(0, ball.Slope_18c);
        }
    }

    [Fact]
    public void S2_AnEntityBornByTheRecordsOfTheMapWithTheGravity_KeepsZeroOnTheSameWater_WhereAMarkedBallReadsFour()
    {
        using var run = new BallRun("s2", Fire, BallRun.Columns(5, 9, 0x08), new List<int[]> { new[] { 180, 108 } });
        var landing = BallOracle.Load("fire11_gp8_deepwater").LandingTick();
        run.Go(1, landing);
        var record = Assert.Single(run.Arena.Targets);
        Assert.NotEqual(0u, record.Flags & EntityFlags.Gravity);
        Assert.False(record.HasLogicalZ);
        Assert.NotEqual(0, record.IsOnGround); // it stands: the rule has something to read
        Assert.Equal(BallOracle.Birth, run.BirthTick);
        Assert.True(run.Ticks[landing].Present);
        for (var tick = 1; tick <= landing; tick++)
        {
            Assert.Equal(0, run.TargetSlopes[tick][0]);
        }

        // The positive of this negative: the very rule, called by hand on the record entity standing on the same water, reads 4 (so a call made for it would be seen).
        var before = record.Slope_18c;
        record.UpdateGroundSlope();
        Assert.Equal(4, record.Slope_18c);
        record.Slope_18c = before;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void S3a_TheFireBallBornOnTheDeepWater_IsDestroyedAtS11WithTheEffect6_FreeAtS12_NoDraw_NextToTheSameBallOnTheDryGroundThatLives(int ticksPerFrame)
    {
        var key = BallOracle.Load("fire11_gp8_deepwater");
        var destroyedTick = BallOracle.Birth + 11;
        var freeTick = BallOracle.Birth + 12;
        Assert.Equal(destroyedTick, key.Attached().Single(a => a.Id == BallRun.SlopeBreakEffect).Tick);
        Assert.Equal(freeTick - 1, key.LastTickPresent());

        using (var water = new BallRun("s3-water", Fire, BallRun.Columns(5, 9, 0x08)))
        {
            water.Go(ticksPerFrame, 30);
            AssertDeepWaterLife(water, key, ticksPerFrame, destroyedTick, freeTick);
        }

        // The same ball on the dry ground (the flat field of the oracle): it lands, takes the status 3 of its impact and lives on, with no break effect and slope 0.
        var flat = BallOracle.Load("fire11_flat");
        using var dry = new BallRun("s3-dry", Fire, null);
        dry.Go(ticksPerFrame, 30);
        Assert.Equal(BallOracle.Birth, dry.BirthTick);
        for (var tick = BallOracle.Birth; tick <= flat.LandingTick(); tick++)
        {
            Assert.Equal(flat.Line(tick), dry.Line(tick));
        }

        for (var tick = destroyedTick; tick <= 30; tick++)
        {
            Assert.True(dry.Ticks[tick].Present);
            Assert.Equal(EntityStatus.Deactivated, dry.Ticks[tick].Status);
            Assert.Equal(0, dry.Ticks[tick].Slope);
        }

        Assert.Empty(dry.SlopeBreakSeenAtFrameEnd);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void S3b_TheIceBallBornOnTheDeepWater_IsDestroyedAtS11WithTheEffect6_FreeAtS12_NoDraw(int ticksPerFrame)
    {
        var key = BallOracle.Load("ice14_gp8_deepwater");
        Assert.Equal(BallOracle.Birth + 11, key.Attached().Single(a => a.Id == BallRun.SlopeBreakEffect).Tick);
        using var water = new BallRun("s3-ice", Ice, BallRun.Columns(5, 9, 0x08));
        water.Go(ticksPerFrame, 30);
        AssertDeepWaterLife(water, key, ticksPerFrame, BallOracle.Birth + 11, BallOracle.Birth + 12);
    }

    private static void AssertDeepWaterLife(BallRun run, BallOracle key, int ticksPerFrame, int destroyedTick, int freeTick)
    {
        Assert.Equal(BallOracle.Birth, run.BirthTick);
        for (var tick = BallOracle.Birth; tick < destroyedTick; tick++)
        {
            Assert.Equal(key.Line(tick), run.Line(tick)); // the flight and the slope 4 at the landing S+10
        }

        Assert.Equal(key.Line(destroyedTick), run.Line(destroyedTick)); // the status 4 (FlagToDestroy), the ball still in the table
        Assert.Equal(key.Line(freeTick), run.Line(freeTick)); // absent
        Assert.Equal("absent", run.Line(freeTick));

        // The break effect 6 comes with the destruction, never before: seen once, on the first frame whose last tick is S+11 or later.
        var seen = Assert.Single(run.SlopeBreakSeenAtFrameEnd);
        Assert.Equal(BallRun.FrameEnd(destroyedTick, ticksPerFrame), seen);

        // No draw: one step of the generator per tick, which is the key's draws (0) plus the step of the end of the frame, over every frame up to the free tick.
        for (var end = ticksPerFrame; end <= BallRun.FrameEnd(freeTick, ticksPerFrame); end += ticksPerFrame)
        {
            Assert.Equal(ticksPerFrame, run.StepsDuring[end]);
        }

        for (var tick = BallOracle.Birth; tick <= destroyedTick; tick++)
        {
            Assert.Equal(0, key.Draws(tick));
        }
    }

    [Fact]
    public void S4_TheCallIsMadeInTheStatus3AsInTheStatus2_AndNotInTheStatus4()
    {
        var (run, _, landing) = RunToLanding("fire11_gp8_deepwater");
        using (run)
        {
            var ball = run.Born!;
            Assert.Equal(4, run.SlopeAt(landing)); // the ball at rest on the deep water, status 2

            ball.Slope_18c = 0;
            ball.Status = EntityStatus.Deactivated;
            ball.TickPhysics();
            Assert.Equal(4, ball.Slope_18c); // status 3: read, and f6a destroys nothing here (the pick phase only takes the program E in the status 3)
            Assert.Equal(EntityStatus.Deactivated, ball.Status);

            ball.Slope_18c = 0;
            ball.Status = EntityStatus.FlagToDestroy;
            ball.TickPhysics();
            Assert.Equal(0, ball.Slope_18c); // status 4: keeps its value of before
        }
    }
}
