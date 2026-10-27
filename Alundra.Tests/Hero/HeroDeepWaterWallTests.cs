#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Session;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.i2a T1 (docs/plan-e20-heros.md section 1.14.2): the wall of the deep water for a hero without the swim boots of level 2 (O-E19-22), the corner test of the binary <c>0x80037488</c>. The hero stops at the edge of the deep
/// water instead of swimming; already in the water without the boots it cannot move (Q-I2-2, D-I2A-4, the default A). The 15 traces of the oracle (<c>out/g9</c> and <c>out-swim</c>, boots of level 0 and 1) are replayed on the
/// montage, the mask being posed by the function of production (called by <see cref="PlayerManager.Tick"/>), with the only tolerances T0 (c) wrote for each of them (D-I2A-6, <c>docs/plan-e20-annexe/e20-i2a/README.md</c>):
/// (t1) the east wall of O-E19-42 (the rebound and the <c>ForceAdjusted</c> one tick early: the pad is shifted from the DLL tick 85 by one, the position within 1 unit), (t2) the step the halving search of the binary loses at
/// the wall on the diagonal (<c>W5-L0-wall-diag</c>: tick 45, X within 1 and Y within 73 872; from tick 46, X exact and Y within 149 167), (t4) the sounds of the landing in the water (E20.e).
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class HeroDeepWaterWallTests : IDisposable
{
    /// <summary>The pseudo-bit of the walkability mask (T0 (d)).</summary>
    private const uint Bit = 0x10000;

    private const uint Float = 0x1D;
    private const uint Swim = 0x0F;
    private const uint Rebound = 0x3E;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public HeroDeepWaterWallTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>(t4) the sounds 21 and 6 of a landing in the water (E20.e), dropped on both sides, as <c>HeroSwimTests</c> does.</summary>
    private static Func<int, int, bool> StubbedSounds(RunOracleTrace trace)
        => (tick, id) => (id is 21 or 6 && tick >= 1 && trace.Col("anim_cur", tick - 1) is 0x2C or 0x2D or 0x02 or 0x2B);

    private static uint HeroMask(RunMontage m) => m.Hero.Controller!.Settings.WalkabilityMask;

    /// <summary>The montage played over the whole trace, then compared with the oracle: the positions within <paramref name="tolerance"/>, the pad shifted from the DLL tick <paramref name="shiftFrom"/> by <paramref name="shift"/> (t1).</summary>
    private static RunMontage Equal(string name, int tolerance = 0, int shiftFrom = 0, int shift = 0)
    {
        var trace = RunOracleTrace.Load(name);
        var montage = RunMontage.Start(trace);
        montage.Play(int.MaxValue, shiftFrom, shift);
        RunMontage.AssertEqualToOracle(montage.CompareRows(int.MaxValue, 1, null, null, tolerance, shiftFrom, shift, StubbedSounds(trace)));
        return montage;
    }

    public static IEnumerable<object[]> Exact() => new[]
    {
        "w1/W1-walk-L0", "w1/W1-walk-L1", "w1/W1-jump-L0", "w1/W1-jump-L1", "w1/W1-fall-walk-L0", "w1/W1-fall-walk-L1", "w2/W2-L0-inside-R", "w2/W2-L1-inside-R",
        "w5/W5-L0-wall-walk", "w5/W5-L1-wall-walk", "w10/W10-shallow-to-deep-L0",
    }.Select(n => new object[] { n });

    // -----------------------------------------------------------------------------------------
    // (r1) The 15 traces.
    // -----------------------------------------------------------------------------------------

    /// <summary>The eleven traces with no gap at all (T0 (c): 0 at tolerance 0, only (t4) employed).</summary>
    [Theory]
    [MemberData(nameof(Exact))]
    public void R1_TheTracesWithoutTheBoots_AreTheOracle_Exactly(string name)
    {
        var m = Equal(name);

        Assert.True(m.Rows.Count > 1);
        Assert.NotEqual(0u, HeroMask(m) & Bit);
    }

    /// <summary>R9 and the two runs of W1: the east wall of O-E19-42 (t1), the DLL one tick early from the DLL tick 85 (the rebound 0x3E at 86 against 87, the mover curtailed at 85 against 86), the position within 1 unit.</summary>
    [Theory]
    [InlineData("g9/R9-deepwater-noboots")]
    [InlineData("w1/W1-run-L0")]
    [InlineData("w1/W1-run-L1")]
    public void R1_TheRunIntoTheDeepWater_StopsAtTheEdge_TheEastWallOfTheBinary(string name)
    {
        var m = Equal(name, tolerance: 1, shiftFrom: 85, shift: 1);

        // The hero reaches x = 325.00 at tick 84 (the oracle's), the mover is curtailed at 86 (the DLL's at 85), the rebound is the oracle's 0x3E at 87 (the DLL's at 86): no swim, never the deep water.
        Assert.InRange(Math.Abs((long)m.StartX + m.Rows[84].PosX - 325 * 65536L), 0, 1);
        Assert.NotEqual(0L, m.Trace.Col("force_adj", 86));
        Assert.Equal((long)Rebound, m.Trace.Col("anim_target", 87));
        Assert.True(m.Rows[85].ForceAdjusted);
        Assert.Equal(Rebound, m.Rows[86].AnimTarget);
        Assert.DoesNotContain(m.Rows, r => r.Slope == 4);
        Assert.DoesNotContain(m.Rows, r => r.AnimTarget is Float or Swim);
    }

    /// <summary>W2: the hero starts in the deep water without the boots and cannot move (Q-I2-2): its position is frozen, the mover is curtailed from tick 5 and the force climbs by 6 912 a tick.</summary>
    [Theory]
    [InlineData("w2/W2-L0-inside-R")]
    [InlineData("w2/W2-L1-inside-R")]
    public void R1_AHeroInTheWaterWithoutTheBoots_CannotMove(string name)
    {
        var m = Equal(name);

        Assert.All(m.Rows, r => Assert.Equal(0, r.PosX));
        Assert.All(m.Rows, r => Assert.Equal(0, r.PosY));
        Assert.Equal(624L * 65536, m.StartX);
        Assert.False(m.Rows[4].ForceAdjusted);
        Assert.True(m.Rows[5].ForceAdjusted);
        Assert.Equal(6912, m.Rows[6].ForceX - m.Rows[5].ForceX);
        Assert.Equal(6912, m.Rows[7].ForceX - m.Rows[6].ForceX);
    }

    /// <summary>W5: the hero walks along the edge of the water and slides along it, never in: the corner test reads the LIVE mask of the controller (D-I2A-3).</summary>
    [Theory]
    [InlineData("w5/W5-L0-wall-walk")]
    [InlineData("w5/W5-L1-wall-walk")]
    public void R1_TheWalkAlongTheEdge_NeverEntersTheWater(string name)
    {
        var m = Equal(name);

        Assert.DoesNotContain(m.Rows, r => r.Slope == 4);
    }

    /// <summary>W10: from the shallow water into the deep one, the hero stops at the first deep cell.</summary>
    [Fact]
    public void R1_FromTheShallowWaterToTheDeepOne_TheHeroStopsAtTheEdge()
    {
        var m = Equal("w10/W10-shallow-to-deep-L0");

        Assert.DoesNotContain(m.Rows, r => r.Slope == 4);
    }

    /// <summary>
    /// W5-L0-wall-diag (t2): the binary's halving search divides both axes together at the wall, so Y advances by 1 424 then by 1 unit at ticks 45 and 46 where the DLL advances by 75 296, a constant offset afterwards
    /// (D-E20-80, O-E19-42). Every other column is exact; the positions are held to the bound of each tick: tick 45, X within 1 and Y within 73 872; from tick 46, X exact and Y within 149 167.
    /// </summary>
    [Fact]
    public void R1_TheDiagonalAgainstTheEdge_SlidesAlongIt_WithinTheBoundsOfEachTick()
    {
        var trace = RunOracleTrace.Load("w5/W5-L0-wall-diag");
        var m = RunMontage.Start(trace);
        m.Play();

        RunMontage.AssertEqualToOracle(m.CompareRows(int.MaxValue, 1, null, (tick, field) => field is "pos_x" or "pos_y", 0, 0, 0, StubbedSounds(trace)));
        for (var tick = 1; tick < m.Rows.Count; tick++)
        {
            var dx = Math.Abs(m.Rows[tick].PosX - (trace.Col("pos_x", tick) - trace.Col("pos_x", 0)));
            var dy = Math.Abs(m.Rows[tick].PosY - (trace.Col("pos_y", tick) - trace.Col("pos_y", 0)));
            var (boundX, boundY) = tick < 45 ? (0L, 0L) : tick == 45 ? (1L, 73872L) : (0L, 149167L);
            Assert.True(dx <= boundX, $"tick {tick}: X differs by {dx}, the bound is {boundX}");
            Assert.True(dy <= boundY, $"tick {tick}: Y differs by {dy}, the bound is {boundY}");
        }

        Assert.DoesNotContain(m.Rows, r => r.Slope == 4);
    }

    // -----------------------------------------------------------------------------------------
    // (r2) The boots of level 2 and 3: the swim of E20.i unchanged (witness green before and after).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("g9/R9-deepwater-boots2")]
    [InlineData("g9/R9-allwater-start-T-R")]
    [InlineData("w1/W1-walk-L2")]
    [InlineData("w1/W1-walk-L3")]
    [InlineData("w1/W1-run-L2")]
    [InlineData("w1/W1-run-L3")]
    [InlineData("w2/W2-L3-R")]
    public void R2_WithTheBootsOfLevel2Or3_TheSwimOfE20i_IsTheOracle_AndTheMaskHasNoBit(string name)
    {
        var m = Equal(name, tolerance: 16);

        Assert.Equal(0u, HeroMask(m) & Bit);
        Assert.Contains(m.Rows, r => r.Slope == 4);
    }

    // -----------------------------------------------------------------------------------------
    // (r3) The mask kept by the opcodes of flags. (r6) The function of the tick leaves the gravity alone.
    // -----------------------------------------------------------------------------------------

    private static RunMontage Montage()
    {
        var m = RunMontage.Start(RunOracleTrace.Load("g9/R9-deepwater-noboots"));
        m.Tick(0);
        return m;
    }

    [Fact]
    public void R3_AnOpcodeOfFlagsAimedAtTheHero_KeepsTheBit()
    {
        var m = Montage();
        Assert.NotEqual(0u, HeroMask(m) & Bit);

        m.Hero.ResyncControllerFromFlags(); // the one resync point of the opcodes 0x16, 0x17, 0x62 and 0x63

        Assert.Equal(CellsCollisionField.WalkabilityMaskFor(m.Hero.Flags) | Bit, HeroMask(m));
    }

    [Fact]
    public void R6_TheFunctionOfTheTick_LeavesTheGravityAndTheMaxFallSpeedOfTheHeroAlone_WhenTheBitChanges()
    {
        var m = Montage();
        var settings = m.Hero.Controller!.Settings;
        Assert.NotEqual(0u, settings.WalkabilityMask & Bit); // the bit is on: no boots
        settings.Gravity = 123f;
        settings.MaxFallSpeed = 456f;

        // The boots of level 2 come into the inventory (as the menu does, the world stopped): at the next tick the bit goes.
        m.Rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 1;
        m.Tick(0);

        Assert.Equal(0u, settings.WalkabilityMask & Bit);
        Assert.Equal(123f, settings.Gravity);
        Assert.Equal(456f, settings.MaxFallSpeed);

        // And the other way: the boots leave, the bit comes back, the settings are still the ones set.
        m.Rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 0;
        m.Tick(0);

        Assert.NotEqual(0u, settings.WalkabilityMask & Bit);
        Assert.Equal(123f, settings.Gravity);
        Assert.Equal(456f, settings.MaxFallSpeed);
    }
}
