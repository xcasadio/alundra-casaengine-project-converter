#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.d1 T4 (docs/plan-e20-heros.md section 1.13): the run of the Triangle, tick by tick against the run oracle (<c>docs/plan-e20-annexe/e20-d/oracle/out/</c>: the real code of <c>ALUN_CD.EXE</c> (France) in the MIPS
/// emulator, groups G1 to G9). The montage is the hero rig of E20.b with the run states in its sprite clock (<see cref="RunMontage"/>), one tick per frame. Every trace is compared field by field: both animations, the facing,
/// the direction, the three forces, the three positions, the ground contact, the mover's curtailment flag, the slope, the resume and the charge counters, the sounds and the effect 9 of the bounce. Beside each comparison the
/// numbers of the plan are asserted on the DLL itself, written before the first run.
/// <para>
/// WHAT IS NOT COMPARED, and why (each is an allowance of the plan):
/// the step (sound 12, dust) and the landing (sound 11, effect 7) are stubs of E20.e; the hurt of the Beannoid (columns after the blow) is E20.c2b; the sound 3 of a Circle pressed in the preparation is the item driver
/// <c>TryUseItem</c> (E20.h); the east wall bounces one tick EARLY (O-E19-42 / D-E19-58: the DLL stops at the contact where the binary stops one unit short), the traces of that wall are compared with the DLL's pad shifted
/// by one tick from the bounce on; the ground column of a ledge is compared one tick late at the tick of the loss (O-E20-12, as AT8); the deep-water wall without boots (O-E19-22) and the clamp at the border
/// of the map (O-E19-17) are not ported (the swim is E20.i's: see <see cref="AlundraHeroSwimTests"/>).
/// </para>
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraHeroRunTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint AirMoving = 0x2C;
    private const uint AirStill = 0x2D;
    private const uint Prepare = 0x29;
    private const uint Sprint = 3;
    private const uint Stop = 0x2A;
    private const uint Tackle = 4;
    private const uint Bounce = 0x3E;

    public AlundraHeroRunTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraNativeEffects.CreatedForTests = null;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    private static RunMontage Played(string name, int lastTick = int.MaxValue, int shiftFrom = 0, int shift = 0)
    {
        var montage = RunMontage.Start(RunOracleTrace.Load(name));
        montage.Play(lastTick, shiftFrom, shift);
        return montage;
    }

    /// <summary>The DLL against the oracle, field by field, the allowances of the call.</summary>
    private static RunMontage Equal(string name, int lastTick = int.MaxValue, int[]? onGroundLag = null, Func<int, string, bool>? ignore = null, int positionTolerance = 0, int shiftFrom = 0, int shift = 0)
    {
        var montage = Played(name, lastTick, shiftFrom, shift);
        RunMontage.AssertEqualToOracle(montage.CompareRows(lastTick, 1, onGroundLag, ignore, positionTolerance, shiftFrom, shift));
        return montage;
    }

    /// <summary>The first tick (from <paramref name="from"/>) at which the DLL's target animation is <paramref name="animation"/>, or -1.</summary>
    private static int First(RunMontage m, uint animation, int from = 1)
    {
        for (var t = from; t < m.Rows.Count; t++)
        {
            if (m.Rows[t].AnimTarget == animation)
            {
                return t;
            }
        }

        return -1;
    }

    private static void AssertStates(RunMontage m, uint animation, int first, int last)
    {
        for (var t = first; t <= last; t++)
        {
            Assert.True(m.Rows[t].AnimTarget == animation, $"{m.Trace.Name} tick {t}: the target animation is {m.Rows[t].AnimTarget:X}, the plan says {animation:X} on ticks {first} to {last}");
        }
    }

    public static IEnumerable<object[]> Group(string group) => RunOracleTrace.NamesOf(group).Select(n => new object[] { n });

    public static IEnumerable<object[]> G1() => Group("g1");

    public static IEnumerable<object[]> G3() => Group("g3");

    public static IEnumerable<object[]> G4() => Group("g4");

    // -----------------------------------------------------------------------------------------
    // The proof that the comparison compares.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Control_ADifferentPad_IsNotTheOracle()
    {
        var montage = RunMontage.Start(RunOracleTrace.Load("g1/R1-T+R"));
        var other = RunOracleTrace.Load("g1/R1-T+L");
        montage.Play(drive: tick => (uint)other.Hold[tick]);
        var diffs = montage.CompareRows();

        Assert.NotEmpty(diffs);
        Assert.Contains(diffs, d => d.Contains("tick 26 force_x"));
    }

    // -----------------------------------------------------------------------------------------
    // G1 - the starts.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(G1))]
    public void G1_TheStartsOfTheRun_AreTheOracleTickByTick(string name) => Equal(name);

    [Theory]
    [InlineData("g1/R1-T+R", 0x18u, 64512, 129024, 193536, 258048)]
    [InlineData("g1/R1-T+L", 0x08u, -64512, -129024, -193536, -258048)]
    public void G1_ACardinalTriangle_Prepares25Ticks_ThenRuns_ForceXRamps_SoundAt26(string name, uint direction, int f26, int f27, int f28, int f29)
    {
        var m = Played(name);

        AssertStates(m, Prepare, 1, 25);
        AssertStates(m, Sprint, 26, 64);
        Assert.Equal(new[] { f26, f27, f28, f29 }, new[] { m.Rows[26].ForceX, m.Rows[27].ForceX, m.Rows[28].ForceX, m.Rows[29].ForceX });
        Assert.Equal(258048, Math.Abs(m.Rows[60].ForceX));
        Assert.All(Enumerable.Range(1, 25), t => Assert.Equal(0, m.Rows[t].ForceX));
        Assert.Equal(direction, m.Rows[26].DirTarget);
        Assert.Contains(13, m.Rows[26].Sounds);
        Assert.DoesNotContain(13, m.Rows[27].Sounds);
    }

    [Theory]
    [InlineData("g1/R1-T+U", 0x10u, -43008, -86016, -129024, -172032)]
    [InlineData("g1/R1-T+D", 0x00u, 43008, 86016, 129024, 172032)]
    public void G1_AVerticalTriangle_RampsForceY(string name, uint direction, int f26, int f27, int f28, int f29)
    {
        var m = Played(name);

        AssertStates(m, Prepare, 1, 25);
        AssertStates(m, Sprint, 26, 64);
        Assert.Equal(new[] { f26, f27, f28, f29 }, new[] { m.Rows[26].ForceY, m.Rows[27].ForceY, m.Rows[28].ForceY, m.Rows[29].ForceY });
        Assert.Equal(direction, m.Rows[26].DirTarget);
        Assert.Contains(13, m.Rows[26].Sounds);
    }

    [Theory]
    [InlineData("g1/R1-T", 0x18u, 3)]
    [InlineData("g1/R1-T+LR", 0x18u, 3)]
    [InlineData("g1/R1-T+UR", 0x14u, 3)]
    [InlineData("g1/R1-T+DR", 0x1Cu, 3)]
    [InlineData("g1/R1-T+DL", 0x04u, 2)]
    [InlineData("g1/R1-T+UL", 0x0Cu, 2)]
    public void G1_TriangleAloneBothSidesAndTheDiagonals_PrepareWithoutEnd_NoForce(string name, uint direction, int facing)
    {
        var m = Played(name);

        AssertStates(m, Prepare, 1, 64);
        Assert.All(Enumerable.Range(1, 64), t => Assert.Equal((0, 0), (m.Rows[t].ForceX, m.Rows[t].ForceY)));
        Assert.Equal(direction, m.Rows[30].DirTarget);
        Assert.Equal(facing, m.Rows[30].Facing);
    }

    [Theory]
    [InlineData("g1/R1-T-then-R-at-10", 26)]
    [InlineData("g1/R1-T-then-R-at-24", 26)]
    [InlineData("g1/R1-T-then-R-at-25", 26)]
    [InlineData("g1/R1-T-then-R-at-30", 30)]
    [InlineData("g1/R1-T-then-R-at-40", 40)]
    [InlineData("g1/R1-T-turn-in-windup", 35)] // a turn at 10 restarts the preparation: the facing changes, the animation starts again at the frame 0 (+0xB0 untouched).
    [InlineData("g1/R1-T-turn-in-windup-at-20", 45)]
    [InlineData("g1/R1-diag-then-card", 30)]
    [InlineData("g1/R1-diag-then-card-early", 26)]
    [InlineData("g1/R1-walk-then-T", 45)]
    [InlineData("g1/R1-cross-with-T", 47)]
    public void G1_TheRunStartsAtTheTickThePlanSays(string name, int start)
    {
        var m = Played(name);

        Assert.Equal(start, First(m, Sprint));
        Assert.Contains(13, m.Rows[start].Sounds);
    }

    [Fact]
    public void G1_AWalkThenTriangle_GoesToThePreparationAtTick20_WithNoForceFromThatTick()
    {
        var m = Played("g1/R1-walk-then-T");

        AssertStates(m, Moving, 1, 19);
        Assert.Equal(Prepare, m.Rows[20].AnimTarget);
        Assert.Equal(0, m.Rows[20].ForceX);
        Assert.Equal(159744, m.Rows[19].ForceX);
    }

    [Fact]
    public void G1_AJumpWithTriangle_LandsInThePreparationAtTick22_AndRunsAt47()
    {
        var m = Played("g1/R1-cross-with-T");

        Assert.Equal(AirMoving, m.Rows[2].AnimTarget);
        AssertStates(m, Prepare, 22, 46);
        Assert.Equal(Sprint, m.Rows[47].AnimTarget);
    }

    [Fact]
    public void G1_TriangleReleasedAtTick10_IsIdleAtTen_AndMovingFromEleven()
    {
        var m = Played("g1/R1-T-released-in-windup");

        AssertStates(m, Prepare, 1, 9);
        Assert.Equal(Idle, m.Rows[10].AnimTarget);
        AssertStates(m, Moving, 11, 64);

        var none = Played("g1/R1-T-released-in-windup-nodir");
        Assert.Equal(Idle, none.Rows[10].AnimTarget);
        AssertStates(none, Idle, 10, 64);
    }

    [Fact]
    public void G1_TheDirectionOfThePreparation_IsWrittenEveryTick_EvenIfTheStartIsLate()
    {
        // The hero faces west (8) and holds Triangle + Right from tick 1: the preparation writes the direction of the pad (0x80031F94), so it faces east at once.
        var m = Played("g1/R1-T-faceW-then-R");

        Assert.Equal(0x18u, m.Rows[1].DirTarget);
        Assert.Equal(26, First(m, Sprint));
    }

    // -----------------------------------------------------------------------------------------
    // G2 - to keep running.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G2_ALongRun_KeepsTheSpeed_AndTheStateForever()
    {
        var m = Equal("g2/R2-long");

        AssertStates(m, Sprint, 26, 120);
        Assert.Equal(258048, m.Rows[120].ForceX);
        Assert.Equal(258048, m.Rows[120].PosX - m.Rows[119].PosX);
    }

    // -----------------------------------------------------------------------------------------
    // G3 - the cone (52 cases).
    // -----------------------------------------------------------------------------------------

    private static readonly (string Run, string[] Continues)[] Cones =
    {
        ("U", new[] { "U", "UR", "UL" }),
        ("R", new[] { "R", "UR", "DR" }),
        ("D", new[] { "D", "DR", "DL" }),
        ("L", new[] { "L", "UL", "DL" }),
    };

    private static readonly string[] Switches = { "none_T", "T_U", "T_R", "T_D", "T_L", "T_UR", "T_DR", "T_DL", "T_UL", "T_LR", "T_UD", "dir_only", "nothing" };

    public static IEnumerable<object[]> ConeCases() => Cones.SelectMany(c => Switches.Select(s => new object[] { c.Run, s }));

    [Theory]
    [MemberData(nameof(ConeCases))]
    public void G3_TheCone_ContinuesForItsDirectionTheNeighbourDiagonalsAndTheInvalidPairs_ElseTheSlide(string run, string pad)
    {
        var name = $"g3/R3-{run}-{pad}";
        var m = Equal(name);

        var continues = pad is "T_LR" or "T_UD" || (pad.StartsWith("T_", StringComparison.Ordinal) && Cones.Single(c => c.Run == run).Continues.Contains(pad[2..]));
        AssertStates(m, Sprint, 26, 40);
        if (continues)
        {
            AssertStates(m, Sprint, 41, 54);
        }
        else
        {
            Assert.Equal(Stop, m.Rows[41].AnimTarget);
            // The counter is 1 when no direction is held (the test of the nibble comes first), 0 when the Triangle is released or the direction is out of the cone.
            Assert.Equal(pad is "none_T" or "nothing" ? 1 : 0, m.Rows[41].StopSprintCounter);
        }
    }

    [Theory]
    [MemberData(nameof(G3))]
    public void G3_EveryTrace_IsTheOracleTickByTick(string name) => Equal(name);

    // -----------------------------------------------------------------------------------------
    // G4 - the stop and the resume.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G4_TheStop_Slides16Ticks_ThenIdle_ForceXDecaysBy8064()
    {
        var m = Played("g4/R4-rel40-R", 60);

        AssertStates(m, Stop, 41, 56);
        Assert.Equal(249984, m.Rows[41].ForceX);
        for (var t = 42; t <= 56; t++)
        {
            Assert.Equal(m.Rows[t - 1].ForceX - 8064, m.Rows[t].ForceX);
        }

        Assert.Equal(129024, m.Rows[56].ForceX);
        Assert.Equal(Idle, m.Rows[57].AnimTarget);
        Assert.Equal(64512, m.Rows[57].ForceX);
        Assert.Equal(0, m.Rows[58].ForceX);
        Assert.Equal(Prepare, m.Rows[58].AnimTarget);
    }

    [Theory]
    [InlineData(10, 51)]
    [InlineData(11, 52)]
    [InlineData(12, 53)]
    [InlineData(13, 54)]
    [InlineData(14, 55)]
    [InlineData(15, 56)]
    [InlineData(16, 57)]
    public void G4_ARepressFrom10To16TicksAfterTheStop_ResumesTheRunAtOnce_WithTheForceOfTheSlide(int k, int press)
    {
        var m = Played($"g4/R4-rel{k:D2}-R", press + 4);

        Assert.Equal(Stop, m.Rows[press - 1].AnimTarget);
        Assert.Equal(Sprint, m.Rows[press].AnimTarget);
        Assert.Equal(0x18u, m.Rows[press].DirTarget);
        // The force of the slide is kept: it ramps from where it was towards 258048 by a quarter of the gap (the acceleration byte 2 of the run).
        var before = m.Rows[press - 1].ForceX;
        Assert.Equal(before + ((258048 - before) >> 2), m.Rows[press].ForceX);
        Assert.Contains(13, m.Rows[press].Sounds);
    }

    [Fact]
    public void G4_AResumeAtK12_GivesTheForcesOfThePlan()
    {
        var m = Played("g4/R4-rel12-R", 60);

        Assert.Equal(new[] { 161280, 185472, 209664, 233856, 258048 }, Enumerable.Range(52, 5).Select(t => m.Rows[t].ForceX).ToArray());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(20)]
    [InlineData(26)]
    public void G4_ARepressTooEarlyOrTooLate_IsLost_IdleAt57_PreparationFrom58_RunAt83(int k)
    {
        var m = Played($"g4/R4-rel{k:D2}-R", 90);

        Assert.Equal(Idle, m.Rows[57].AnimTarget);
        AssertStates(m, Prepare, 58, 82);
        Assert.Equal(Sprint, m.Rows[83].AnimTarget);
    }

    [Fact]
    public void G4_ARepressAtTheFirstTicksOfTheSlide_IsLost_TheTraceStopsBeforeTheRunAgain()
    {
        // k = 1 to 4: the trace ends at tick 82, 83 is not recorded; the preparation still runs from 58 to 82.
        foreach (var k in new[] { 1, 2, 3, 4 })
        {
            var m = Played($"g4/R4-rel{k:D2}-R", 90);

            Assert.Equal(Idle, m.Rows[57].AnimTarget);
            AssertStates(m, Prepare, 58, Math.Min(82, m.Rows.Count - 1));
        }
    }

    [Theory]
    [InlineData(10, "U", 0x10u)]
    [InlineData(11, "U", 0x10u)]
    [InlineData(12, "U", 0x10u)]
    [InlineData(10, "L", 0x08u)]
    [InlineData(11, "L", 0x08u)]
    [InlineData(12, "L", 0x08u)]
    public void G4_AResumeInAnotherCardinalDirection_TakesThatDirection(int k, string pad, uint direction)
    {
        var m = Played($"g4/R4-rel{k:D2}-{pad}", 41 + k + 6);

        Assert.Equal(Sprint, m.Rows[41 + k].AnimTarget);
        Assert.Equal(direction, m.Rows[41 + k].DirTarget);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void G4_ADiagonalDoesNotResume_TheCounterIsKept(int k)
    {
        var m = Played($"g4/R4-rel{k:D2}-UR", 41 + k + 4);

        for (var t = 41 + k; t <= Math.Min(41 + k + 4, 56); t++)
        {
            Assert.Equal(Stop, m.Rows[t].AnimTarget);
            Assert.Equal(m.Rows[t - 1].StopSprintCounter + 1, m.Rows[t].StopSprintCounter); // not cleared: it keeps counting.
        }
    }

    [Theory]
    [MemberData(nameof(G4))]
    public void G4_EveryTrace_IsTheOracleTickByTick(string name) => Equal(name);

    // -----------------------------------------------------------------------------------------
    // G5 - the bounce.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G5_TheWestWall_BouncesAt78_ExactlyLikeTheBinary()
    {
        var m = Equal("g5/R5-wall-W");

        Assert.NotEqual(m.Rows[75].PosX, m.Rows[76].PosX); // still moving at 76 (the last step), the contact.
        Assert.Equal(m.Rows[76].PosX, m.Rows[77].PosX);
        Assert.False(m.Rows[76].ForceAdjusted);
        Assert.True(m.Rows[77].ForceAdjusted);
        AssertStates(m, Sprint, 26, 77);
        AssertStates(m, Bounce, 78, 88);
        Assert.Equal(0x08u, m.Rows[77].DirTarget);
        Assert.All(Enumerable.Range(78, 11), t => Assert.Equal(0x18u, m.Rows[t].DirTarget));
        Assert.All(Enumerable.Range(78, 11), t => Assert.Equal(196608, m.Rows[t].ForceX));
        Assert.Equal(262144, m.Rows[78].ForceZ);
        Assert.Contains(28, m.Rows[78].Sounds);
        Assert.Equal(new[] { (m.Rows[77].PosX, m.Rows[77].PosY, 0x100001) }, m.Rows[78].Bounces); // effect 9 at the hero, 16 px up (PosZ + 1 + 0x100000).
        Assert.Equal(33 * 65536, m.Rows[88].PosX - m.Rows[77].PosX); // the recoil: 33 px.
        Assert.Equal(18 * 65536, Enumerable.Range(78, 11).Max(t => m.Rows[t].PosZ)); // the apex: 18 px.
        Assert.Equal(Moving, m.Rows[89].AnimTarget);
        Assert.Equal(18432, m.Rows[89].ForceX);
        AssertStates(m, AirMoving, 90, 94);
        Assert.Equal(Prepare, m.Rows[95].AnimTarget);
    }

    [Fact]
    public void G5_TheNorthWall_BouncesAt55To65_TheSouthWallAt95()
    {
        var north = Equal("g5/R5-wall-N");
        AssertStates(north, Bounce, 55, 65);

        var south = Equal("g5/R5-wall-S");
        Assert.Equal(95, First(south, Bounce));
    }

    [Fact]
    public void G5_TheEastWall_BouncesOneTickEarly_O_E19_42_ThenTheOracleShifted()
    {
        var probe = Played("g5/R5-wall-E", 90);
        var first = First(probe, Bounce);

        // The binary bounces at 87 (it stops one unit short of the wall at 84, moves that unit at 85); the DLL stops at the contact: 86 (O-E19-42, D-E19-58). Both are accepted by the plan.
        Assert.Contains(first, new[] { 86, 87 });
        Equal("g5/R5-wall-E", positionTolerance: 1, shiftFrom: first - 1, shift: 87 - first);
    }

    [Theory]
    [InlineData("g5/R5-wall-E-release")]
    [InlineData("g5/R5-wall-E-releaseT")]
    [InlineData("g5/R5-wall-E-square")]
    [InlineData("g5/R5-step-16px")]
    [InlineData("g5/R5-step-32px")]
    public void G5_TheEastVariants_FollowTheOracleShiftedByTheSameTick(string name)
    {
        var first = First(Played(name, 90), Bounce);

        Assert.Contains(first, new[] { 86, 87 });
        Equal(name, positionTolerance: 1, shiftFrom: first - 1, shift: 87 - first);
    }

    [Fact]
    public void G5_APadReleasedDuringTheBounce_GivesTheAirThenIdle_ASquareDuringTheBounce_DoesNothing()
    {
        // In the oracle's ticks (the DLL is one tick earlier from the bounce on): 0x3E 87-97, 0x01 at 98, then the air 99-103, Idle after.
        var released = Played("g5/R5-wall-E-release", 130, 85, 1);
        AssertStates(released, Bounce, 86, 96);
        Assert.Equal(Moving, released.Rows[97].AnimTarget);
        AssertStates(released, AirStill, 98, 102);
        Assert.Equal(Idle, released.Rows[103].AnimTarget);

        var square = Played("g5/R5-wall-E-square", 130, 85, 1);
        AssertStates(square, Bounce, 86, 96);
        Assert.DoesNotContain(square.Rows.Skip(86).Take(40), r => r.AnimTarget is 0x10 or 0x14 or Tackle);
    }

    [Fact]
    public void G5_ARunStartedGluedToTheWall_BouncesAt27()
    {
        var m = Equal("g5/R5-wall-E-adjacent");

        Assert.Equal(27, First(m, Bounce));
    }

    [Theory]
    [InlineData("g5/R5-walk-step-16px")]
    [InlineData("g5/R5-walk-step-32px")]
    [InlineData("g5/R5-wall-E-walk")]
    public void G5_AWalkAgainstTheSameObstacles_StaysMoving_NoBounce(string name)
    {
        var m = Equal(name);

        Assert.DoesNotContain(m.Rows, r => r.AnimTarget == Bounce);
        Assert.Equal(Moving, m.Rows[^1].AnimTarget);
    }

    // -----------------------------------------------------------------------------------------
    // G6 - the ledge.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G6_TheDropOf32Pixels_FallsFromTick90_LandsAt100_AndRunsAgainFrom101()
    {
        var m = Equal("g6/R6-drop-32px", onGroundLag: new[] { 89 });

        Assert.Equal(AirStill, m.Rows[90].AnimTarget);
        Assert.Equal(129024, m.Rows[90].ForceX);
        AssertStates(m, AirMoving, 91, 100);
        Assert.Equal(139776, m.Rows[91].ForceX);
        Assert.Equal(150528, m.Rows[92].ForceX);
        Assert.Equal(1, m.Rows[100].OnGround);
        Assert.Equal(Prepare, m.Rows[101].AnimTarget);
    }

    [Theory]
    [InlineData("g6/R6-drop-16px", 98)]
    [InlineData("g6/R6-drop-32px", 101)]
    [InlineData("g6/R6-drop-64px", 106)]
    public void G6_TheLandingPreparesTheRunAgain_AtTheTickThePlanSays(string name, int landingPreparation)
    {
        var m = Equal(name, onGroundLag: new[] { 89 });

        Assert.Equal(Prepare, m.Rows[landingPreparation].AnimTarget);
        Assert.Equal(AirMoving, m.Rows[landingPreparation - 1].AnimTarget);
    }

    [Fact]
    public void G6_TriangleReleasedInTheAir_IsMovingAt101_TheWholePadReleased_IsIdle_ASquareInTheAir_SwingsThePlanTicks()
    {
        var triangle = Equal("g6/R6-drop-32px-releaseT-air", onGroundLag: new[] { 89 });
        Assert.Equal(Moving, triangle.Rows[101].AnimTarget);

        var all = Equal("g6/R6-drop-32px-release-all-air", onGroundLag: new[] { 89 });
        Assert.Equal(Idle, all.Rows[101].AnimTarget);

        var square = Equal("g6/R6-drop-32px-square-air", onGroundLag: new[] { 89 });
        AssertStates(square, 0x14, 93, 100);
        Assert.Equal(Idle, square.Rows[101].AnimTarget);
        Assert.Equal(Prepare, square.Rows[102].AnimTarget);
    }

    [Fact]
    public void G6_AWalkOffTheLedge_IsTheSameAirState_ControlTrace()
    {
        var m = Equal("g6/R6-walk-drop-32px", onGroundLag: new[] { 102 });

        Assert.DoesNotContain(m.Rows, r => r.AnimTarget is Sprint or Prepare or Stop);
    }

    // -----------------------------------------------------------------------------------------
    // G7 - the tackle and the charge.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("g7/R7-square-sprint")]
    [InlineData("g7/R7-cross-sprint")]
    [InlineData("g7/R7-circle-sprint")]
    public void G7_ASquareACrossOrACircle_InTheRun_TacklesFor32Ticks(string name)
    {
        var m = Equal(name);

        AssertStates(m, Sprint, 26, 39);
        AssertStates(m, Tackle, 40, 71);
        Assert.Contains(83, m.Rows[40].Sounds);
        Assert.Equal(254016, m.Rows[40].ForceX);
        for (var t = 41; t <= 71; t++)
        {
            Assert.Equal(m.Rows[t - 1].ForceX - 4032, m.Rows[t].ForceX);
        }

        Assert.Equal(6128640, m.Rows[71].PosX - m.Rows[39].PosX); // 93.52 px.
        Assert.Equal(Idle, m.Rows[72].AnimTarget);
        AssertStates(m, Prepare, 73, 97);
        Assert.Equal(Sprint, m.Rows[98].AnimTarget);
    }

    [Theory]
    [InlineData("g7/R7-square-stopsprint")]
    [InlineData("g7/R7-cross-stopsprint")]
    public void G7_InTheSlide_ASquareOrACross_DoNothing(string name)
    {
        var m = Equal(name);

        Assert.DoesNotContain(m.Rows.Skip(41).Take(16), r => r.AnimTarget != Stop);
    }

    [Fact]
    public void G7_InThePreparation_TheSquareSwingsTheSword_TheCrossAndTheCircleDoNothing()
    {
        var square = Equal("g7/R7-square-windup");
        AssertStates(square, 0x10, 10, 30);
        Assert.Equal(Idle, square.Rows[31].AnimTarget);
        Assert.Equal(Sprint, square.Rows[57].AnimTarget);

        var cross = Equal("g7/R7-cross-windup");
        AssertStates(cross, Prepare, 1, 25);
        Assert.Equal(Sprint, cross.Rows[26].AnimTarget);

        // The Circle: the preparation carries on; the binary's TryUseItem plays the sound 3 on the press (E20.h, a stub here), the only difference.
        var circle = Equal("g7/R7-circle-windup", ignore: (tick, field) => tick == 10 && field == "sounds");
        AssertStates(circle, Prepare, 1, 25);
        Assert.Equal(Sprint, circle.Rows[26].AnimTarget);
    }

    [Fact]
    public void G7_TheSquareHeld_SwingsTheSwordAgain_InThePreparationAndInTheRun()
    {
        Equal("g7/R7-square-windup-held");
        Equal("g7/R7-square-sprint-held");
    }

    [Fact]
    public void G7_TheSacredSword_CountsTheChargeInEveryStateOfTheRun()
    {
        var m = Equal("g7/R7-holy-square-held");

        Assert.Equal(new[] { 1, 16, 31, 60 }, new[] { m.Rows[30].ChargeCounter, m.Rows[45].ChargeCounter, m.Rows[60].ChargeCounter, m.Rows[90].ChargeCounter });

        Equal("g7/R7-holy-square-held-windup");
    }

    // -----------------------------------------------------------------------------------------
    // G8 - the ladder.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G8_ARunUpIntoALadder_ClimbsAtTick54_TheBounceDoesNotHappen()
    {
        var m = Equal("g8/R8-ladder-sprint", onGroundLag: new[] { 54 });

        Assert.Equal(51, m.Rows.First(r => r.Tick >= 1 && r.Slope == 6).Tick);
        Assert.Equal(53, m.Rows.First(r => r.Tick >= 1 && r.ForceAdjusted).Tick);
        Assert.Equal(0x0Eu, m.Rows[54].AnimTarget);
        Assert.DoesNotContain(m.Rows, r => r.AnimTarget == Bounce);

        Equal("g8/R8-ladder-sprint-faceE", onGroundLag: new[] { 54 });
        Equal("g8/R8-ladder-sprint-UR");
    }

    [Fact]
    public void G8_WithoutTheLadder_ItBouncesAt54_AWalkIntoTheLadderClimbsToo()
    {
        var m = Equal("g8/R8-cliff-no-ladder");
        Assert.Equal(54, First(m, Bounce));

        Equal("g8/R8-ladder-walk", onGroundLag: new[] { 43 });
    }

    // -----------------------------------------------------------------------------------------
    // G9 - the water, the ice and the deep water (E20.i: the run goes to the swim there).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void G9_TheShallowWater_HalvesTheRunWithoutBoots_NotWithThem()
    {
        var plain = Equal("g9/R9-shallow-water");
        Assert.Equal(129024, plain.Rows[60].ForceX);

        var boots = Equal("g9/R9-shallow-water-boots1");
        Assert.Equal(258048, boots.Rows[60].ForceX);

        var flat = Equal("g9/R9-flat-reference");
        Assert.Equal(258048, flat.Rows[60].ForceX);
    }

    [Fact]
    public void G9_TheIce_SlowsTheRamp_AndTheSlideEnds16UnitsApart_Rounding()
    {
        // 20160, 60480, 141120 at ticks 30, 40, 60 (the ramp divided by 16); after the release the glide ends 16 units apart from the binary's: the mover works on floats (the slide is O-E19-42, out of scope).
        var m = Equal("g9/R9-ice", positionTolerance: 16);

        Assert.Equal(new[] { 20160, 60480, 141120 }, new[] { m.Rows[30].ForceX, m.Rows[40].ForceX, m.Rows[60].ForceX });
    }

    [Fact]
    public void G9_OnDeepWater_TheRunGoesToTheFloatAtTheTickAfterTheSlope4_ThenTheHeroSwims_TheWholeTrace()
    {
        // E20.i: the binary goes to 0x1D (force 258048 - 258048 >> 3 = 225792) at tick 90, then to 0x0F at tick 91; the stand-in guard of E20.d1 (Idle, then the walk) is gone. The whole trace is compared; the positions
        // within the float allowance of the mover (see AlundraHeroSwimTests).
        var trace = RunOracleTrace.Load("g9/R9-deepwater-boots2");
        var slope4 = trace.FirstTick("slope", 4);
        Assert.Equal(89, slope4);

        var m = Equal("g9/R9-deepwater-boots2", positionTolerance: 16);

        Assert.Equal(4, m.Rows[slope4].Slope);
        Assert.Equal(Sprint, m.Rows[slope4].AnimTarget);
        Assert.Equal(0x1Du, m.Rows[slope4 + 1].AnimTarget);
        Assert.Equal(225792, m.Rows[slope4 + 1].ForceX);
        Assert.Equal(0x0Fu, m.Rows[slope4 + 2].AnimTarget);
        Assert.DoesNotContain(m.Rows.Skip(slope4 + 1), r => r.AnimTarget is Prepare or Sprint or Stop or Tackle or Bounce);
    }

    [Fact]
    public void G9_ATriangleOnDeepWater_DoesNotPrepareTheRun_TheHeroSwims_TheWholeTrace()
    {
        var m = Equal("g9/R9-allwater-start-T-R", positionTolerance: 16);

        Assert.DoesNotContain(m.Rows, r => r.AnimTarget is Prepare or Sprint or Stop or Tackle or Bounce);
        Assert.Equal(0x0Fu, m.Rows[40].AnimTarget);
    }

    // -----------------------------------------------------------------------------------------
    // Two ticks in a frame.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TwoTicksPerFrame_AStartThatChangesTheFacing_DoesNotLeaveTheCone()
    {
        // Triangle + Up + Right ticks 1-28 (the preparation faces east), Triangle + Up from tick 29: the frame [29, 30] starts the run north on its first tick; on its second tick the facing of the FRAME is
        // still east (AnimationDirection is written when a frame ends), where the cone of the east lacks the north: the facing of the tick (the binary's own value) keeps it in the run.
        var m = RunMontage.Start(RunOracleTrace.Load("g1/R1-T+UR"));
        for (var frame = 1; frame <= 14; frame++)
        {
            m.Tick(AlundraPadState.Triangle | AlundraPadState.Up | AlundraPadState.Right, 2);
        }

        Assert.Equal(Prepare, m.Hero.TargetAnimationId);
        Assert.Equal(3, m.Hero.AnimationDirection);

        m.Tick(AlundraPadState.Triangle | AlundraPadState.Up, 2);

        Assert.Equal(Sprint, m.Hero.TargetAnimationId);
        Assert.Equal(0x10u, m.Hero.TargetDirection);
        Assert.Equal(0, m.Hero.StopSprintCounter);

        for (var frame = 0; frame < 10; frame++)
        {
            m.Tick(AlundraPadState.Triangle | AlundraPadState.Up, 2);
            Assert.Equal(Sprint, m.Hero.TargetAnimationId);
        }

        Assert.Equal(-172032, m.Hero.ForceY);
    }
}
