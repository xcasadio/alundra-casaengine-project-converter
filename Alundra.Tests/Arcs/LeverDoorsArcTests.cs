#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.c1b-2 L4 (docs/plan-e20-heros.md section 1.7, arc E): map 44 (Wendels Nightmare), the six levers (records 7 to 12) and the seven doors (records 0
/// to 6). With the flag G1633 the program D shared by the six levers takes the hand, clears the class A of the six, and the program C of the record 7
/// turns the six levers (animation 2, then 1), raises T81 and so makes the seven doors walk; when they all report (T10 to T70) it gives the class A and the
/// hand back. Without G1633 the hit only runs the first test and the end of D. Every expected value was written by hand before the code from the plan,
/// check.md (the corrected timeline, section b.1) and maps-117-44.md section 3.1; the values marked INFERENCE (the pass of <c>0x1C</c>, the 64 ticks of the
/// doors) are expectations too.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LeverDoorsArcTests
{
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;

    private static readonly int[] DoorStarts = { 1160, 1220, 1268, 1328, 1376, 1436, 1496 };
    private const int LeverProgramStart = 1556;

    /// <summary>Where each door starts and where it ends (the "P1 end" column of maps-117-44.md 3.1), in pixels.</summary>
    private static readonly ((int X, int Y) Start, (int X, int Y) End)[] Doors =
    {
        ((192, 528), (144, 560)),
        ((384, 592), (480, 592)),
        ((264, 768), (216, 800)),
        ((720, 512), (816, 512)),
        ((528, 896), (576, 928)),
        ((960, 864), (912, 896)),
        ((1152, 624), (1104, 592)),
    };

    private readonly ITestOutputHelper _output;

    public LeverDoorsArcTests(ITestOutputHelper output) => _output = output;

    private static ArcRun Build(string name, int[] flags) =>
        new(LeverArcs.Spec(name, LeverArcs.Zone44, LeverArcs.World44, flags, 120, 440, 8, 24, 3000));

    [Fact]
    public void E_Map44_TheLever7_TurnsTheSixLevers_WalksTheSevenDoors_AndGivesTheHandBack()
    {
        // Expected, written before the run (plan 1.7 arc E). Hero (120,440,128) facing east, 24 px west of the lever 7 at (144,440); G1633 set.
        using var arc = Build("E20c1b2-E44", new[] { 1633 });
        var sounds = LeverStampedSounds.Inject(arc);
        var lockedAfter = new Dictionary<int, bool>();
        arc.OnFrame = () => lockedAfter[arc.Frame - 1] = LeverArcs.Locked();
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var levers = Enumerable.Range(7, 6).Select(r => arc.EntityByRecord(r)!).ToArray();
        var doors = Enumerable.Range(0, 7).Select(r => arc.EntityByRecord(r)!).ToArray();
        Assert.All(levers, l => Assert.NotNull(l));
        Assert.All(doors, x => Assert.NotNull(x));
        Assert.All(levers, l => Assert.True(LeverArcs.ClassA(l)));
        for (var d = 0; d < 7; d++)
        {
            Assert.Equal(Doors[d].Start, (doors[d].PosX >> 16, doors[d].PosY >> 16));
        }

        arc.Hero.TargetDirection = 24;
        arc.OneFrame();
        LeverArcs.AssertHeroReady(arc, 120, 440, 128);
        Assert.False(LeverArcs.Temp(80));

        var n = arc.Frame;
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, levers[0].TouchingEntity);

        // N+1: D[8] from 2320 to 2353: T80 set, the hand lost, the class A of the six cleared (six opcodes 0x63).
        LeverArcs.RunThrough(arc, n + 1);
        var d8 = LeverArcs.Pcs(arc, D, n + 1).Where(pc => pc is >= 2320 and <= 2353).ToArray();
        Assert.Equal(new[] { 2320, 2325, 2328 }, d8.Take(3).ToArray());
        Assert.Equal(2353, d8[^1]);
        Assert.Equal(6, arc.Trace.Count(t => t.Slot == D && t.Frame == n + 1 && t.Opcode == 0x63));
        Assert.True(LeverArcs.Temp(80));
        Assert.True(lockedAfter[n + 1]);
        Assert.All(levers, l => Assert.Equal(0u, l.Flags & EntityFlags.ClassA));

        // N+2: C[8] at 1556, 1559, 1562 (Break); T80 cleared.
        LeverArcs.RunThrough(arc, n + 2);
        Assert.Equal(new[] { 1556, 1559, 1562 }, LeverArcs.Pcs(arc, C, n + 2).Where(pc => pc >= LeverProgramStart && pc <= 1583).ToArray());
        Assert.False(LeverArcs.Temp(80));

        // N+3: pc 1563 (the hero's animation 0), the six levers on the animation 2, the first call of pc 1584.
        LeverArcs.RunThrough(arc, n + 3);
        var c3 = LeverArcs.Pcs(arc, C, n + 3);
        Assert.Contains(1563, c3);
        Assert.Contains(1584, c3);
        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        Assert.All(levers, l => Assert.Equal(2u, l.TargetAnimationId));
        Assert.True(sounds.Contains(n + 3, 218));

        // N+34 (INFERENCE: s + L + 1 = N+3 + 30 + 1): pc 1584 passes, the six levers on the animation 1; T81 is not set yet (the Break at 1604).
        LeverArcs.RunThrough(arc, n + 33);
        Assert.All(levers, l => Assert.Equal(2u, l.TargetAnimationId));
        LeverArcs.RunThrough(arc, n + 34);
        Assert.Equal(n + 34, ArcChecks.FrameOf(arc, C, 1586));
        Assert.All(levers, l => Assert.Equal(1u, l.TargetAnimationId));
        Assert.False(LeverArcs.Temp(81));

        // N+35: T81 set. N+36: every door executes its 0x1A [2] and its first 0x1E.
        LeverArcs.RunThrough(arc, n + 35);
        Assert.Equal(n + 35, ArcChecks.FrameOf(arc, C, 1605));
        Assert.True(LeverArcs.Temp(81));
        for (var d = 0; d < 7; d++)
        {
            Assert.DoesNotContain(arc.Trace, t => t.Slot == C && t.Opcode == 0x1A && t.Pc == DoorStarts[d] + 3);
        }

        LeverArcs.RunThrough(arc, n + 36);
        for (var d = 0; d < 7; d++)
        {
            var end = d < 6 ? DoorStarts[d + 1] : LeverProgramStart;
            var inRange = arc.Trace.Where(t => t.Slot == C && t.Pc >= DoorStarts[d] && t.Pc < end).ToList();
            var first1A = inRange.First(t => t.Opcode == 0x1A);
            Assert.Equal((n + 36, DoorStarts[d] + 3), (first1A.Frame, first1A.Pc));
            Assert.Equal(n + 36, inRange.First(t => t.Opcode == 0x1E).Frame);
            Assert.Equal(2u, doors[d].TargetAnimationId);
        }

        // About N+101 (INFERENCE: 64 ticks of walk, plus the tick of delay of D-E19-13): the seven flags T10 to T70 are set and C[8] reaches pc 1630.
        arc.RunUntil(() => arc.Has(C, 1630, 0x00), "pc 1630 of the lever 7");
        var m = ArcChecks.FrameOf(arc, C, 1630);
        Assert.InRange(m, n + 100, n + 102);
        _output.WriteLine($"arc E: N = {n}, pc 1630 at N+{m - n}");
        Assert.True(arc.Frame == m + 1);
        for (uint flag = 10; flag <= 70; flag += 10)
        {
            Assert.True(LeverArcs.Temp(flag), $"T{flag} not set at the tick of pc 1630");
        }

        Assert.All(levers, l => Assert.Equal(0u, l.Flags & EntityFlags.ClassA));
        for (var tick = n + 1; tick <= m; tick++)
        {
            Assert.True(lockedAfter[tick], $"the hand is not lost at N+{tick - n}");
        }

        // The doors reached the end of the first path (within 2 px).
        for (var d = 0; d < 7; d++)
        {
            var (x, y) = (doors[d].PosX >> 16, doors[d].PosY >> 16);
            Assert.True(Math.Abs(x - Doors[d].End.X) <= 2 && Math.Abs(y - Doors[d].End.Y) <= 2, $"door {d} at ({x},{y}), ({Doors[d].End.X},{Doors[d].End.Y}) +/- 2 expected");
        }

        // Exactly one tick later: pcs 1631 to 1676; the class A of the six back, T10 to T70 cleared, the hand given back.
        arc.OneFrame();
        Assert.Equal(m + 1, ArcChecks.FrameOf(arc, C, 1631));
        Assert.Equal(m + 1, ArcChecks.FrameOf(arc, C, 1676));
        Assert.All(levers, l => Assert.NotEqual(0u, l.Flags & EntityFlags.ClassA));
        for (uint flag = 10; flag <= 70; flag += 10)
        {
            Assert.False(LeverArcs.Temp(flag), $"T{flag} still set after pc 1676");
        }

        Assert.False(lockedAfter[m + 1]);
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }

    [Fact]
    public void E_Map44_WithoutG1633_TheHitOnlyRunsTheTestAndTheEnd_NoLockNoT80()
    {
        // Expected, written before the run (plan 1.7, the variant): D[8] runs 2320 then 2353 (End), nothing else moves.
        using var arc = Build("E20c1b2-E44-no1633", Array.Empty<int>());
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var levers = Enumerable.Range(7, 6).Select(r => arc.EntityByRecord(r)!).ToArray();
        Assert.All(levers, l => Assert.NotNull(l));
        Assert.False(ArcChecks.IsSet(1633));
        arc.Hero.TargetDirection = 24;
        arc.OneFrame();
        LeverArcs.AssertHeroReady(arc, 120, 440, 128);

        var n = arc.Frame;
        var lockedTicks = new List<int>();
        arc.OnFrame = () =>
        {
            if (LeverArcs.Locked())
            {
                lockedTicks.Add(arc.Frame - 1);
            }
        };
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, levers[0].TouchingEntity);
        LeverArcs.RunThrough(arc, n + 1);
        Assert.Equal(new[] { 2320, 2353 }, LeverArcs.Pcs(arc, D, n + 1).Where(pc => pc is >= 2320 and <= 2353).ToArray());
        LeverArcs.RunThrough(arc, n + 40);
        Assert.Empty(lockedTicks);
        Assert.False(LeverArcs.Temp(80));
        Assert.DoesNotContain(arc.Trace, t => t.Pc == 2325);
        Assert.DoesNotContain(arc.Trace, t => t.Slot == C && t.Pc >= LeverProgramStart && t.Pc <= 1583 && t.Opcode != 0x36);
        Assert.All(levers, l => Assert.NotEqual(0u, l.Flags & EntityFlags.ClassA));
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
