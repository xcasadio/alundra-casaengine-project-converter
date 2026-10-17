#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Alundra.Session;

namespace Alundra.Tests;

/// <summary>
/// E20.i T4 (docs/plan-e20-heros.md section 1.14): the swim, tick by tick against the swim oracle (<c>docs/plan-e20-annexe/e20-d/oracle/out-swim/</c>, groups W1 to W10, and the swim traces R9 of the run oracle): the real code of
/// <c>ALUN_CD.EXE</c> (France) in the MIPS emulator. The montage is the run montage (<see cref="RunMontage"/>) with the five swim states in its sprite clock, one tick per frame, on a field of 52 x 60 cells painted from the layout
/// of the trace. Every trace is compared field by field (both animations, the facing, the direction, the forces, the positions, the ground contact, the mover's curtailment flag, the slope, the counters, the sounds); the numbers
/// of the plan are asserted beside each comparison, written before the first run.
/// <para>
/// WHAT IS NOT COMPARED, and why (each is an allowance of the plan): the effects 6 and 0x15 (the splash, the wake and the ripple of the dive: the shadow pass <c>0x80038E84</c> and <c>CreatePlayerAnimationEffects</c>, E20.e), the sounds 21
/// and 6 of a landing in the water (E20.e), the sound 3 of the Circle (the item driver, E20.h); the hurt of the water is entered by an access point of the test (the exit of the hurt block of the binary, which is E20.c2b's:
/// the traces W7 hurt the hero with a Beannoid the DLL does not have, so their effect 9 is not compared); the wall of the deep water without the swim boots (O-E19-22) and the currents (O-E20-16) are not ported, the traces of
/// boots level 0 and 1 are information; the east wall stops one unit short in the binary (O-E19-42); the clamp at the border of the map (O-E19-17).
/// </para>
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraHeroSwimTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint AirMoving = 0x2C;
    private const uint Swim = 0x0F;
    private const uint Float = 0x1D;
    private const uint Dive = 0x28;
    private const uint SwimHurt = 0x3A;
    private const uint SwimHurtEnd = 0x3B;
    private const uint Sprint = 3;

    public AlundraHeroSwimTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraNativeEffects.CreatedForTests = null;
        AlundraPlayerManager.TryUseItemResultForTests = null;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    /// <summary>The sounds the DLL does not ask in the water: the Circle (3, E20.h) and the landing in the water (21 and 6 the tick after an air animation, E20.e).</summary>
    private static Func<int, int, bool> StubbedSounds(RunOracleTrace trace)
        => (tick, id) => id == 3 || (id is 21 or 6 && tick >= 1 && trace.Col("anim_cur", tick - 1) is 0x2C or 0x2D or 0x02 or 0x2B);

    /// <summary>The allowance on the positions X and Y, in units of 1/65536 px: the mover of the DLL works on floats (24 bits of mantissa: 4 units at x 600 px, and a few of them add up on a diagonal), the binary on integers (the family of O-E19-42, as the 16 units of the ice of E20.d1).</summary>
    private const int FloatUnits = 16;

    private sealed record Allowances(int[]? OnGroundLag = null, Func<int, string, bool>? Ignore = null, int Tolerance = FloatUnits, int ShiftFrom = 0, int Shift = 0, int LastTick = int.MaxValue, bool Hurt = false);

    /// <summary>The access point of the hurt (the exit of the hurt block of the binary, <c>0x80031C74</c>-<c>0x80031C9C</c>, without its entry, which is E20.c2b's): at the tick the oracle's state turns to 0x3A, the state and the direction the oracle
    /// holds on that tick are posed before <c>MovePlayer</c>.</summary>
    private static Action<int> HurtAccess(RunMontage m, RunOracleTrace trace)
    {
        var tick = trace.FirstTick("anim_target", SwimHurt);
        Assert.True(tick > 0, $"{trace.Name}: the oracle never hurts the hero in the water");
        return t =>
        {
            if (t == tick)
            {
                m.Hero.TargetAnimationId = SwimHurt;
                m.Hero.TargetDirection = (uint)trace.Col("dir_target", tick);
            }
        };
    }

    private static RunMontage Played(string name, Allowances? allowances = null)
    {
        allowances ??= new Allowances();
        var trace = RunOracleTrace.Load(name);
        var montage = RunMontage.Start(trace);
        montage.Play(allowances.LastTick, allowances.ShiftFrom, allowances.Shift, before: allowances.Hurt ? HurtAccess(montage, trace) : null);
        return montage;
    }

    /// <summary>The DLL against the oracle, field by field, the allowances of the call.</summary>
    private static RunMontage Equal(string name, Allowances? allowances = null)
    {
        allowances ??= new Allowances();
        var montage = Played(name, allowances);
        var ignore = allowances.Ignore;
        if (allowances.Hurt)
        {
            var inner = ignore;
            ignore = (tick, field) => field == "effects" || inner?.Invoke(tick, field) == true; // the hit effect of the Beannoid the DLL does not have (E20.c2b)
        }

        RunMontage.AssertEqualToOracle(montage.CompareRows(
            allowances.LastTick, 1, allowances.OnGroundLag, ignore, allowances.Tolerance, allowances.ShiftFrom, allowances.Shift, StubbedSounds(montage.Trace), compareEndCounter: IsOnlyInTheWater(montage.Trace)));
        return montage;
    }

    private static void AssertStates(RunMontage m, uint animation, int first, int last)
    {
        for (var t = first; t <= last; t++)
        {
            Assert.True(m.Rows[t].AnimTarget == animation, $"{m.Trace.Name} tick {t}: the target animation is {m.Rows[t].AnimTarget:X}, the plan says {animation:X} on ticks {first} to {last}");
        }
    }

    /// <summary>The counter of animation ends (+0xB0) is compared on the traces whose hero swims all along: the loops of the land animations of the montage (Idle, Moving: placeholders of 50 ticks) do not turn it as the binary's do.</summary>
    private static bool IsOnlyInTheWater(RunOracleTrace trace)
        => Enumerable.Range(1, trace.Ticks).All(t => trace.Col("slope", t) == 4 && trace.Col("anim_target", t) is 0x0F or 0x1D or 0x28 or 0x3A or 0x3B);

    private static bool Sounded(RunMontage m, int tick, int id) => m.Rows[tick].Sounds.Contains(id);

    public static IEnumerable<object[]> Names(params string[] names) => names.Select(n => new object[] { n });

    // -----------------------------------------------------------------------------------------
    // The proof that the comparison compares.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Control_ADifferentPad_IsNotTheOracle()
    {
        var montage = RunMontage.Start(RunOracleTrace.Load("w2/W2-dir-R"));
        var other = RunOracleTrace.Load("w2/W2-dir-L");
        montage.Play(drive: tick => (uint)other.Hold[tick]);
        var diffs = montage.CompareRows();

        Assert.NotEmpty(diffs);
        Assert.Contains(diffs, d => d.Contains("tick 6 force_x"));
    }

    // -----------------------------------------------------------------------------------------
    // R9 - the run into the water, and the swim from the start.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void R9_TheRunIntoDeepWater_Floats_ThenSwims_WholeTraceEqualToTheOracle()
    {
        var m = Equal("g9/R9-deepwater-boots2");

        Assert.Equal(4, m.Rows[89].Slope);
        Assert.Equal(Sprint, m.Rows[89].AnimTarget);
        Assert.Equal(258048, m.Rows[89].ForceX);

        Assert.Equal(Float, m.Rows[90].AnimTarget);
        Assert.Equal(225792, m.Rows[90].ForceX);
        Assert.Empty(m.Rows[90].Sounds);

        Assert.Equal(Swim, m.Rows[91].AnimTarget);
        Assert.Equal(218592, m.Rows[91].ForceX);
        Assert.True(Sounded(m, 91, 22));
        for (var t = 91; t <= 106; t++)
        {
            Assert.Equal(218592 - 7200 * (t - 91), m.Rows[t].ForceX);
        }

        Assert.Equal(110592, m.Rows[106].ForceX);
    }

    [Theory]
    [InlineData("g9/R9-allwater-start-R")]
    [InlineData("g9/R9-allwater-start-T-R")]
    public void R9_AHeroInTheWater_FloatsAtTick1_SwimsAt2_RampsBy6912_Sound22AtEachChain(string name)
    {
        var m = Equal(name);

        Assert.Equal(Float, m.Rows[1].AnimTarget);
        Assert.Equal(Swim, m.Rows[2].AnimTarget);
        Assert.Equal(6912, m.Rows[2].ForceX);
        Assert.True(Sounded(m, 2, 22));
        for (var t = 2; t <= 17; t++)
        {
            Assert.Equal(6912 * (t - 1), m.Rows[t].ForceX);
        }

        Assert.Equal(110592, m.Rows[17].ForceX);
        Assert.True(Sounded(m, 44, 22));
        Assert.Equal(1, m.Rows[44].AnimCompleteCounter);
        Assert.DoesNotContain(m.Rows, r => r.AnimTarget is 0x29 or Sprint or 0x2A or 4 or 0x3E); // no preparation of the run in the water
    }

    [Fact]
    public void R9_ATriangleHeldInTheWater_Floats_NeverDives_TheCounterTurnsAt37()
    {
        var m = Equal("g9/R9-allwater-start-T");

        AssertStates(m, Float, 1, 40);
        Assert.All(m.Rows, r => Assert.Equal(0, r.ForceX));
        Assert.Equal(1, m.Rows[37].AnimCompleteCounter);
        Assert.Equal(0, m.Rows[36].AnimCompleteCounter);
    }

    // -----------------------------------------------------------------------------------------
    // W1 - entering the water.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("w1/W1-walk-L2")]
    [InlineData("w1/W1-walk-L3")]
    public void W1_AWalkIn_Slope4At20_Floats21_Swims22(string name)
    {
        var m = Equal(name);

        Assert.Equal(4, m.Rows[20].Slope);
        Assert.Equal(0, m.Rows[19].Slope);
        Assert.Equal(Float, m.Rows[21].AnimTarget);
        Assert.Equal(139776, m.Rows[21].ForceX);
        Assert.Equal(Swim, m.Rows[22].AnimTarget);
        Assert.Equal(137952, m.Rows[22].ForceX);
        Assert.True(Sounded(m, 22, 22));
    }

    [Theory]
    [InlineData("w1/W1-run-L2")]
    [InlineData("w1/W1-run-L3")]
    public void W1_ARunIn_IsTheOracle_NoRunInTheWater(string name)
    {
        var m = Equal(name);

        var slope4 = m.Rows.First(r => r.Tick >= 1 && r.Slope == 4).Tick;
        Assert.Equal(Float, m.Rows[slope4 + 1].AnimTarget);
        Assert.Equal(Swim, m.Rows[slope4 + 2].AnimTarget);
        Assert.DoesNotContain(m.Rows.Skip(slope4 + 1), r => r.AnimTarget is 0x29 or Sprint or 0x2A or 4 or 0x3E);
    }

    [Fact]
    public void W1_AJumpOverTheShore_LandsInTheWater_Air23_Floats24_Swims25()
    {
        var m = Equal("w1/W1-jump-s03");

        AssertStates(m, AirMoving, 4, 23);
        Assert.Equal(Float, m.Rows[24].AnimTarget);
        Assert.Equal(131712, m.Rows[24].ForceX);
        Assert.Equal(Swim, m.Rows[25].AnimTarget);
        Assert.Equal(130392, m.Rows[25].ForceX);
    }

    [Theory]
    [InlineData("w1/W1-jump-s06")]
    [InlineData("w1/W1-jump-s09")]
    [InlineData("w1/W1-jump-s12")]
    [InlineData("w1/W1-jump-s15")]
    public void W1_TheOtherJumps_AreTheOracle(string name) => Equal(name);

    [Fact]
    public void W1_AFall32px_OffALedge_Air21To31_Floats32_Swims33()
    {
        var m = Equal("w1/W1-fall-walk", new Allowances(OnGroundLag: new[] { 20 })); // O-E20-12: the ground contact is judged at the tick of the loss

        AssertStates(m, AirMoving, 21, 31);
        Assert.Equal(Float, m.Rows[32].AnimTarget);
        Assert.Equal(Swim, m.Rows[33].AnimTarget);
    }

    [Theory]
    [InlineData("w1/W1-fall-run", 89)]
    [InlineData("w1/W1-fall16-walk", 20)]
    public void W1_TheOtherFalls_AreTheOracle(string name, int tickOfTheLoss) => Equal(name, new Allowances(OnGroundLag: new[] { tickOfTheLoss })); // O-E20-12

    // -----------------------------------------------------------------------------------------
    // W2 - swimming.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("w2/W2-dir-R", 110592, 0, 0x18u, 6912, 0)]
    [InlineData("w2/W2-dir-L", -110592, 0, 0x08u, -6912, 0)]
    [InlineData("w2/W2-dir-D", 0, 73728, 0x00u, 0, 4608)]
    [InlineData("w2/W2-dir-U", 0, -73728, 0x10u, 0, -4608)]
    [InlineData("w2/W2-dir-DR", 78192, 52128, 0x1Cu, 4887, 3258)]
    [InlineData("w2/W2-dir-UR", 78192, -52128, 0x14u, 4887, -3258)]
    [InlineData("w2/W2-dir-DL", -78192, 52128, 0x04u, -4887, 3258)]
    [InlineData("w2/W2-dir-UL", -78192, -52128, 0x0Cu, -4887, -3258)]
    public void W2_TheEightDirections_ReachTheirForce_ByTheirRamp(string name, int forceX, int forceY, uint direction, int rampX, int rampY)
    {
        var m = Equal(name);

        Assert.Equal(forceX, m.Rows[70].ForceX);
        Assert.Equal(forceY, m.Rows[70].ForceY);
        Assert.Equal(direction, m.Rows[70].DirTarget);
        Assert.Equal(rampX, m.Rows[5].ForceX);
        Assert.Equal(rampY, m.Rows[5].ForceY);
    }

    [Theory]
    [InlineData("w2/W2-idle")]
    [InlineData("w2/W2-turn")]
    [InlineData("w2/W2-L3-R")]
    public void W2_TheOtherSwims_AreTheOracle(string name) => Equal(name);

    [Fact]
    public void W2_ATurnAround_IsAt41_AndTheSwimSoundsEvery42Ticks()
    {
        var turn = Equal("w2/W2-turn");
        Assert.Equal(96768, turn.Rows[41].ForceX);

        var m = Equal("w2/W2-long-R");
        foreach (var tick in new[] { 5, 47, 89, 131, 173 })
        {
            Assert.True(Sounded(m, tick, 22), $"tick {tick}");
        }

        Assert.Equal(5, Enumerable.Range(1, m.Rows.Count - 1).Count(t => Sounded(m, t, 22)));
    }

    // -----------------------------------------------------------------------------------------
    // W3 - the burst.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W3_TheTriangle_StartsTheBurst_42Ticks_ThenFloats_ThenSwims()
    {
        var m = Equal("w3/W3-T-swim");

        AssertStates(m, Swim, 5, 19);
        AssertStates(m, Dive, 20, 61);
        for (var t = 20; t <= 61; t++)
        {
            Assert.Equal(135168, m.Rows[t].ForceX);
        }

        Assert.True(Sounded(m, 20, 21));
        Assert.Equal(Float, m.Rows[62].AnimTarget);
        Assert.Equal(118272, m.Rows[62].ForceX);
        Assert.Equal(1, m.Rows[62].AnimCompleteCounter);
        Assert.Equal(Swim, m.Rows[63].AnimTarget);
        Assert.Equal(117792, m.Rows[63].ForceX);
    }

    [Theory]
    [InlineData("w3/W3-X-swim")]
    [InlineData("w3/W3-S-swim")]
    public void W3_TheCrossAndTheSquare_BurstLikeTheTriangle(string name)
    {
        var m = Equal(name);
        AssertStates(m, Dive, 20, 61);

        var triangle = RunOracleTrace.Load("w3/W3-T-swim");
        for (var t = 1; t <= triangle.Ticks; t++)
        {
            Assert.Equal(triangle.Col("anim_target", t), (long)m.Rows[t].AnimTarget);
            Assert.Equal(triangle.Col("force_x", t), (long)m.Rows[t].ForceX);
        }
    }

    [Fact]
    public void W3_TheCircle_DoesNotBurst_ItIsTheItemButton()
    {
        var m = Equal("w3/W3-O-swim");

        Assert.DoesNotContain(m.Rows, r => r.AnimTarget == Dive);
        var idle = Equal("w3/W3-O-idle");
        Assert.DoesNotContain(idle.Rows, r => r.AnimTarget == Dive);
    }

    [Fact]
    public void W3_AHeldTriangle_DoesNotBurstAgain_WhenTheBurstEnds()
    {
        var m = Equal("w3/W3-T-held");

        Assert.Equal(Float, m.Rows[52].AnimTarget);
        Assert.Equal(1, m.Rows.Skip(1).Count(r => r.AnimTarget == Dive && Sounded(m, r.Tick, 21)));
        Assert.DoesNotContain(m.Rows.Skip(53), r => r.AnimTarget == Dive);
    }

    [Fact]
    public void W3_TapsEvery5Ticks_DiveFrom10To51_AndFrom55To96()
    {
        var m = Equal("w3/W3-T-taps5");

        AssertStates(m, Dive, 10, 51);
        AssertStates(m, Dive, 55, 96);
        Assert.Equal(Float, m.Rows[52].AnimTarget); // the press of the surfacing tick (tick 52 is the surfacing, 55 the next press that works) is lost
    }

    [Theory]
    [InlineData("w3/W3-T-idle")]
    [InlineData("w3/W3-X-idle")]
    [InlineData("w3/W3-S-idle")]
    [InlineData("w3/W3-TX-same-tick")]
    [InlineData("w3/W3-X-during-dive")]
    [InlineData("w3/W3-T-near-end-1")]
    [InlineData("w3/W3-T-near-end+0")]
    [InlineData("w3/W3-T-near-end+1")]
    [InlineData("w3/W3-T-near-end+2")]
    [InlineData("w3/W3-dive-steer")]
    [InlineData("w3/W3-dive-end-held")]
    [InlineData("w3/W3-dive-end-release")]
    public void W3_TheOtherBursts_AreTheOracle(string name) => Equal(name);

    [Theory]
    [InlineData("w3/W3-dive-dir-U")]
    [InlineData("w3/W3-dive-dir-D")]
    [InlineData("w3/W3-dive-dir-L")]
    [InlineData("w3/W3-dive-dir-R")]
    [InlineData("w3/W3-dive-dir-UR")]
    [InlineData("w3/W3-dive-dir-DR")]
    [InlineData("w3/W3-dive-dir-DL")]
    [InlineData("w3/W3-dive-dir-UL")]
    public void W3_TheBurstTakesTheDirectionOfThePressTick(string name) => Equal(name);

    [Fact]
    public void W3_ADiagonalBurst_And_NoDirection_KeepsTheDirection()
    {
        var diagonal = Equal("w3/W3-dive-dir-DR");
        Assert.Equal(95568, diagonal.Rows.First(r => r.AnimTarget == Dive).ForceX);
        Assert.Equal(63712, diagonal.Rows.First(r => r.AnimTarget == Dive).ForceY);

        var none = Equal("w3/W3-dive-dir-none");
        Assert.Equal(0x18u, none.Rows.First(r => r.AnimTarget == Dive).DirTarget);

        var contradictory = Equal("w10/W10-dive-LR");
        Assert.Equal(0x18u, contradictory.Rows.First(r => r.AnimTarget == Dive).DirTarget);
    }

    [Fact]
    public void W3_TheEntry_FloatsOneTick_APressOnThatTickIsLost()
    {
        var zero = Equal("w3/W3-enter-T+0");
        Assert.Equal(0x29u, zero.Rows[12].AnimTarget);
        Assert.Equal(Idle, zero.Rows[13].AnimTarget);
        Assert.Equal(Moving, zero.Rows[14].AnimTarget);
        Assert.Equal(Float, zero.Rows[15].AnimTarget);
        Assert.Equal(Swim, zero.Rows[16].AnimTarget);

        var lost = Equal("w3/W3-enter-T+1");
        Assert.Equal(Float, lost.Rows[13].AnimTarget);
        Assert.DoesNotContain(lost.Rows.Take(30), r => r.AnimTarget == Dive);

        var burst = Equal("w3/W3-enter-T+2");
        Assert.Equal(Float, burst.Rows[13].AnimTarget);
        AssertStates(burst, Dive, 14, 55);
    }

    [Theory]
    [InlineData("w3/W3-enter-T+3")]
    [InlineData("w3/W3-enter-T+4")]
    [InlineData("w3/W3-enter-T-1")]
    [InlineData("w3/W3-enter-ref")]
    public void W3_TheOtherEntries_AreTheOracle(string name) => Equal(name);

    // -----------------------------------------------------------------------------------------
    // W4 - leaving the water.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W4_ALeaveToTheLeft_Slope0At103_IdleHalvesTheForce_ThenWalks()
    {
        var m = Equal("w4/W4-leave-L");

        Assert.Equal(0, m.Rows[103].Slope);
        Assert.Equal(Idle, m.Rows[104].AnimTarget);
        Assert.Equal(-55296, m.Rows[104].ForceX);
        Assert.Equal(Moving, m.Rows[105].AnimTarget);
        Assert.Equal(-107520, m.Rows[105].ForceX);
        Assert.Equal(-159744, m.Rows[106].ForceX);
    }

    [Fact]
    public void W4_LeavingWithATriangleHeld_RunsAtOnce_AfterTheIdle()
    {
        var m = Equal("w4/W4-leave-L-T");

        Assert.Equal(Idle, m.Rows[122].AnimTarget);
        AssertStates(m, 0x29, 123, 147);
        Assert.Equal(Sprint, m.Rows[148].AnimTarget);
    }

    [Fact]
    public void W4_ADiveInProgress_IsCutByTheLand()
    {
        var m = Equal("w4/W4-leave-dive-s05");

        AssertStates(m, Dive, 5, 31);
        Assert.Equal(Idle, m.Rows[32].AnimTarget);
        Assert.Equal(-67584, m.Rows[32].ForceX);
    }

    [Theory]
    [InlineData("w4/W4-leave-L-release")]
    [InlineData("w4/W4-leave-L-T-late")]
    [InlineData("w4/W4-leave-UL")]
    [InlineData("w4/W4-leave-dive-s15")]
    [InlineData("w4/W4-leave-dive-s25")]
    public void W4_TheOtherLeaves_AreTheOracle(string name) => Equal(name);

    [Theory]
    [InlineData("w4/W4-bank16")]
    [InlineData("w4/W4-bank32")]
    public void W4_ABankHigherThanTheStep_IsAWallForTheSwimmer(string name)
    {
        var m = Equal(name);

        Assert.Equal(346.00, (m.Rows[^1].PosX + m.StartX) / 65536.0, 2);
        Assert.Equal(Swim, m.Rows[^1].AnimTarget);
        Assert.True(m.Rows.Skip(1).Any(r => r.ForceAdjusted));
        Assert.DoesNotContain(m.Rows, r => r.Slope == 0 && r.Tick > 1);
    }

    // -----------------------------------------------------------------------------------------
    // W5 - walls.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W5_AWall_StopsTheSwimmer_ForceAdjusted_ThenKeepsSwimming()
    {
        // The east wall: the binary stops one unit short, the DLL at the contact (O-E19-42): one unit and one tick of allowance.
        var m = Equal("w5/W5-wall-R");

        Assert.Equal(Swim, m.Rows[^1].AnimTarget);
        Assert.True(m.Rows.Skip(77).Any(r => r.ForceAdjusted));
    }

    [Fact]
    public void W5_ADiagonalIntoAWall_Slides_TheContactTickKeepsAnEighthOfAPixelOnY()
    {
        // The oracle scales the last step of Y by the share of X the wall allowed (x 709.17 to 709.00 at tick 103); the DLL's mover clamps X only and keeps the whole step: Y is 7424 units (0.113 px) ahead from the contact on, constant
        // (the wall-contact family of O-E19-42). Measured, then pinned: the tolerance is that offset, no more.
        var m = Equal("w5/W5-wall-UR", new Allowances(Tolerance: 7424));

        var offsets = Enumerable.Range(103, m.Rows.Count - 103).Select(t => m.Rows[t].PosY - (m.Trace.Col("pos_y", t) - m.Trace.Col("pos_y", 0))).Distinct().ToArray();
        Assert.Equal(new long[] { -7424 }, offsets);
    }

    [Fact]
    public void W5_ADiveIntoAWall_ContinuesToItsEnd_IsTheOracle() => Equal("w5/W5-wall-dive");

    [Theory]
    [InlineData("w5/W5-edge-E", 63, 1237.00)]
    [InlineData("w5/W5-edge-N", 58, 7.00)]
    public void W5_TheEdgesOfTheMap_AreTheOracleUntilTheClamp_ThenTheDllCrossesThem_O_E19_17(string name, int lastTickBeforeTheClamp, double edge)
    {
        // The binary clamps the hero at the border of the map (x 1237.00, y 7.00); the DLL has no clamp (O-E19-17, the engine's own bounds): an allowance of the plan, pinned here as a deviation.
        var m = Equal(name, new Allowances(LastTick: lastTickBeforeTheClamp));
        m.Play();

        var last = m.Rows[^1];
        var pos = name.EndsWith('E') ? (last.PosX + m.StartX) / 65536.0 : (last.PosY + m.StartY) / 65536.0;
        Assert.True(name.EndsWith('E') ? pos > edge : pos < edge, $"{name}: the oracle stops at {edge}, the DLL is at {pos}");
    }

    // -----------------------------------------------------------------------------------------
    // W6 - the weapon in the water.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W6_TheSquareHeld_ChargesOnTheLand_ZeroInTheWater_AndOnTheLandAgain()
    {
        var m = Equal("w6/W6-walk-square-held");

        Assert.Equal(60, m.Rows[83].ChargeCounter);
        for (var t = 84; t < m.Rows.Count; t++)
        {
            Assert.Equal(0, m.Rows[t].ChargeCounter);
        }

        var land = Equal("w6/W6-leave-water-square-held");
        Assert.Equal(3, land.Rows[90].ChargeCounter);
        Assert.Equal(60, land.Rows[150].ChargeCounter);
    }

    [Theory]
    [InlineData("w6/W6-allwater-square-held")]
    [InlineData("w6/W6-charge-then-water")]
    public void W6_TheOtherWeaponTraces_AreTheOracle(string name)
    {
        var m = Equal(name);
        var firstWater = m.Rows.First(r => r.Tick >= 1 && r.Slope == 4).Tick; // the slope is probed at the end of a tick: the charge is zeroed by the next one
        Assert.All(m.Rows.Skip(firstWater + 1), r => Assert.Equal(0, r.ChargeCounter));
    }

    // -----------------------------------------------------------------------------------------
    // W7 - the hurt in the water (the access point poses the state of the hurt block's exit).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W7_AHurtAtRest_3AFor4Ticks_3BFor9_FloatsAt15_TheDirectionTurnsAndComesBack()
    {
        var m = Equal("w7/W7-hurt-idle-L2", new Allowances(Hurt: true));

        AssertStates(m, SwimHurt, 2, 5);
        Assert.Equal(-196608, m.Rows[2].ForceX);
        Assert.True(Sounded(m, 2, 23));
        AssertStates(m, SwimHurtEnd, 6, 14);
        Assert.Equal(-184320, m.Rows[6].ForceX);
        Assert.Equal(-172032, m.Rows[7].ForceX);
        Assert.Equal(Float, m.Rows[15].AnimTarget);
        Assert.Equal(-75264, m.Rows[15].ForceX);
        Assert.Equal(0x08u, m.Rows[14].DirTarget);
        Assert.Equal(0x18u, m.Rows[15].DirTarget);
        Assert.Equal(229.00, (m.Rows[2].PosX + m.StartX) / 65536.0, 2);
        Assert.Equal(217.19, (m.Rows[6].PosX + m.StartX) / 65536.0, 2);
        Assert.Equal(200.29, (m.Rows[15].PosX + m.StartX) / 65536.0, 2);
    }

    [Fact]
    public void W7_AKnockbackThatLeavesTheWater_CutsTheHurt_Idle_TheDirectionComesBack()
    {
        var m = Equal("w7/W7-hurt-leaving-x352", new Allowances(Hurt: true));

        Assert.Equal(Idle, m.Rows[5].AnimTarget);
        Assert.Equal(-98304, m.Rows[5].ForceX);
        Assert.Equal(0x18u, m.Rows[5].DirTarget);
    }

    [Fact]
    public void W7_AHurtDuringTheBurst_InterruptsIt_At81()
    {
        var m = Equal("w7/W7-hurt-dive-s45", new Allowances(Hurt: true));

        Assert.Equal(Dive, m.Rows[80].AnimTarget);
        Assert.Equal(SwimHurt, m.Rows[81].AnimTarget);
    }

    [Theory]
    [InlineData("w7/W7-hurt-idle-L3")]
    [InlineData("w7/W7-hurt-swim")]
    [InlineData("w7/W7-hurt-leaving-x346")]
    [InlineData("w7/W7-hurt-press-T")]
    [InlineData("w7/W7-hurt-press-X")]
    [InlineData("w7/W7-hurt-dive-s35")]
    [InlineData("w7/W7-hurt-dive-s55")]
    [InlineData("w7/W7-hurt-dive-s60")]
    [InlineData("w7/W7-hurt-dive-s65")]
    [InlineData("w7/W7-hurt-dive-s70")]
    [InlineData("w7/W7-hurt-dive-s75")]
    public void W7_TheOtherHurts_AreTheOracle_AtTheFirstContact(string name) => Equal(name, new Allowances(Hurt: true, LastTick: FirstHurtEnd(name)));

    /// <summary>The traces with a second contact (the Beannoid keeps touching) are compared until the end of the first hurt: the second entry is E20.c2b's.</summary>
    private static int FirstHurtEnd(string name)
    {
        var trace = RunOracleTrace.Load(name);
        var hurt = trace.FirstTick("anim_target", SwimHurt);
        return Math.Min(trace.Ticks, hurt + 40);
    }

    // -----------------------------------------------------------------------------------------
    // W9 and W10 - the land side, the contradictory nibbles, the channels.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("w9/W9-land-s0F", Idle, 0u)]
    [InlineData("w9/W9-land-s1D", Idle, 0u)]
    [InlineData("w9/W9-land-s28", Idle, 0u)]
    [InlineData("w9/W9-land-s3A", Idle, 1u)]
    [InlineData("w9/W9-land-s3B", Idle, 1u)]
    public void W9_TheSwimStatesOnTheLand_GoToIdleAtTick1_TheHurtOnesTurnTheDirection(string name, uint expected, uint turned)
    {
        var m = Equal(name);
        var trace = m.Trace;

        Assert.Equal(expected, m.Rows[1].AnimTarget);
        Assert.Equal((uint)trace.Col("dir_target", 1), m.Rows[1].DirTarget);
        Assert.Equal(turned == 1 ? (uint)trace.Col("dir_target", 0) ^ 0x10u : (uint)trace.Col("dir_target", 0), m.Rows[1].DirTarget);
    }

    [Theory]
    [InlineData("w9/W9-circle-idle")]
    [InlineData("w9/W9-circle-swim")]
    [InlineData("w10/W10-LR-UD")]
    [InlineData("w10/W10-channel-down")]
    [InlineData("w10/W10-channel-edge")]
    [InlineData("w10/W10-shallow-to-deep")]
    public void W9W10_TheOtherCases_AreTheOracle(string name) => Equal(name);

    // -----------------------------------------------------------------------------------------
    // Two ticks in a frame.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TwoTicksPerFrame_ABurstEdge_DivesOnce()
    {
        var m = RunMontage.Start(RunOracleTrace.Load("w3/W3-T-swim"));
        for (var frame = 0; frame < 3; frame++)
        {
            m.Tick(AlundraPadState.Right, 2);
        }

        Assert.Equal(Swim, m.Hero.TargetAnimationId);

        var dives = 0;
        for (var frame = 0; frame < 24; frame++)
        {
            m.Tick(AlundraPadState.Right | (frame == 0 ? AlundraPadState.Triangle : 0u), 2);
            if (m.Sounds.Requests.Contains(21))
            {
                dives++;
            }
        }

        Assert.Equal(1, dives); // the edge of the first frame bursts once; the 42 ticks of the burst are 21 frames
        Assert.NotEqual(Dive, m.Hero.TargetAnimationId);
    }
}
