#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b-2 (docs/plan-e20-heros.md section 1.7, "Acceptation", arcs A to E): a sword hit on a lever makes the lever react exactly as its programs
/// say. Each arc runs the real prefab and the real controller on the real map; the hero presses Square next to the lever, and the flags, the animations,
/// the programs that run (trace pcs) and the cells the programs rewrite are read tick by tick. Every expected value was written by hand before the code
/// from the plan and from docs/plan-e20-annexe/e20-c1b2/check.md (where the three discovery reports differ, check.md wins). A value marked INFERENCE in
/// the plan (the pass of <c>0x1C [1]</c> at s + L + 1, the walk of the doors) is an expectation too: a run that disagrees stops the task, the expectation
/// is never re-fitted. H is <c>arc.Frame</c> before the press; the trace frames are absolute and the checks compare ticks.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraLeverArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;

    private readonly ITestOutputHelper _output;

    public AlundraLeverArcTests(ITestOutputHelper output) => _output = output;

    private static void AssertCells(AlundraCellStore store, int x, int y, int walkability, int height, int groundProperty, int tileLeft, int tileRight)
    {
        var left = LeverArcs.CellAt(store, x, y);
        var right = LeverArcs.CellAt(store, x + 1, y);
        Assert.Equal((walkability, height, groundProperty, tileLeft), (left.Walkability, left.Height, left.GroundProperty, left.FloorTile));
        Assert.Equal((walkability, height, groundProperty, tileRight), (right.Walkability, right.Height, right.GroundProperty, right.FloorTile));
    }

    // -----------------------------------------------------------------------------------------
    // Arc A: 115, the north-west lever (record 2)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_Map115_TheNorthWestLever_TurnsAndOpensTheDoorAtTheTicksOfItsPrograms()
    {
        // Expected, written before the run (plan 1.7 arc A): the hero stands at pixel (84,120), floor Z 16, facing west; the lever is record 2 at (60,120), Z 24 (INFERENCE).
        using var arc = new ArcRun(LeverArcs.Spec("E20c1b2-A115", LeverArcs.Zone, LeverArcs.World115, Array.Empty<int>(), 84, 120, 1, 8, 1500));
        var sounds = LeverStampedSounds.Inject(arc);
        var sparks = new List<(int X, int Y, int Z)>();
        arc.Proxy.HitSparksForTests = (x, y, z) => sparks.Add((x, y, z));
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        ArcChecks.AssertNoUnexpectedError(arc);
        Assert.Null(arc.EntityByRecord(0));
        Assert.Null(arc.EntityByRecord(1));
        var lever = arc.EntityByRecord(2)!;
        Assert.NotNull(lever);
        Assert.Equal((60, 120, 24), (lever.PosX >> 16, lever.PosY >> 16, lever.PosZ >> 16)); // Z 24: INFERENCE (16 would give the contact too).
        Assert.True(LeverArcs.ClassA(lever));
        var store = LeverArcs.Store(arc);
        AssertCells(store, 4, 6, 1, 4, 16, 8514, 8515); // image A (the door closed by the map event at the first tick).
        arc.Hero.TargetDirection = 8;
        arc.OneFrame();
        LeverArcs.AssertHeroReady(arc, 84, 120, 16);

        // H: the press. The contact is at the press tick.
        var h = arc.Frame;
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, lever.TouchingEntity);
        Assert.Equal(25, lever.FrameCollisionTickCounter);
        Assert.Equal(1, arc.Hero.HitCounter);
        Assert.Single(sparks);
        Assert.True(sounds.Contains(h, 29)); // the swing.
        Assert.Empty(LeverArcs.Pcs(arc, D, h));

        // H+1: D of the lever at 1476, 1477, 1480; class A cleared; T2 set.
        LeverArcs.RunThrough(arc, h + 1);
        Assert.Equal(new[] { 1476, 1477, 1480 }, LeverArcs.Pcs(arc, D, h + 1));
        Assert.Empty(LeverArcs.Pcs(arc, C, h + 1).Where(pc => pc is >= 1200 and <= 1215));
        Assert.False(LeverArcs.ClassA(lever));
        Assert.True(LeverArcs.Temp(2));

        // H+2: C at 1200, 1203, 1206, 1208; T2 cleared; animation 2 asked (and its sound 218).
        LeverArcs.RunThrough(arc, h + 2);
        Assert.Equal(new[] { 1200, 1203, 1206, 1208 }, LeverArcs.Pcs(arc, C, h + 2).Where(pc => pc is >= 1200 and <= 1215).ToArray());
        Assert.False(LeverArcs.Temp(2));
        Assert.Equal(2u, lever.TargetAnimationId);
        Assert.True(sounds.Contains(h + 2, 218));

        // The hero keeps the pad: at no tick is control locked (checked at every following tick below). Second press after H+25: no second hit (usage unique).
        var lockedTicks = new List<int>();
        for (var tick = h + 3; tick <= h + 33; tick++)
        {
            if (tick == h + 25)
            {
                Assert.Equal(0u, arc.Hero.TargetAnimationId); // the first swing is over (Idle at H+21).
                arc.Press(LeverArcs.Square);
                Assert.Equal(0, arc.Hero.HitCounter);
                Assert.Null(lever.TouchingEntity);
                Assert.Equal(0x10u, arc.Hero.TargetAnimationId); // the second swing starts.
            }
            else
            {
                arc.OneFrame();
            }

            if (LeverArcs.Locked())
            {
                lockedTicks.Add(tick);
            }

            if (tick < h + 33)
            {
                Assert.Equal(2u, lever.TargetAnimationId);
                Assert.True(LeverArcs.Temp(1) == false, $"T1 set at tick {tick}-{h}");
            }
        }

        // H+33 (INFERENCE, s + L + 1 with s = H+2 and L = 30): C at 1210, 1212 (T1 set), animation 1.
        Assert.Contains(1210, LeverArcs.Pcs(arc, C, h + 33));
        Assert.Contains(1212, LeverArcs.Pcs(arc, C, h + 33));
        Assert.True(LeverArcs.Temp(1));
        Assert.Equal(1u, lever.TargetAnimationId);
        Assert.Equal(1, ArcChecks.FramesOf(arc, C, 1212, 0x05).Count);
        Assert.Equal(h + 33, ArcChecks.FramesOf(arc, C, 1212, 0x05)[0]);
        AssertCells(store, 4, 6, 1, 4, 16, 8514, 8515); // still image A at H+33: B[1] sees T1 one tick later.

        // H+34: B[1] passes the wait at 135 and rewrites the cells with image B (W1, H3). The trace frame of T1 + 1 is the frame of the pass.
        arc.OneFrame();
        Assert.Contains(135, LeverArcs.Pcs(arc, B, h + 34));
        Assert.Equal(h + 34, ArcChecks.FrameOf(arc, B, 138));
        Assert.Equal(ArcChecks.FramesOf(arc, C, 1212, 0x05)[0] + 1, ArcChecks.FrameOf(arc, B, 138));
        AssertCells(store, 4, 6, 1, 3, 16, 8514, 8515);
        Assert.True(sounds.Contains(h + 34, 222));

        // H+55: image C (W1, H2); H+76: image D (W0, H1, GP144, walkable); H+97: the Break at 168.
        LeverArcs.RunThrough(arc, h + 54);
        AssertCells(store, 4, 6, 1, 3, 16, 8514, 8515);
        LeverArcs.RunThrough(arc, h + 55);
        AssertCells(store, 4, 6, 1, 2, 16, 8514, 8515);
        LeverArcs.RunThrough(arc, h + 75);
        AssertCells(store, 4, 6, 1, 2, 16, 8514, 8515);
        LeverArcs.RunThrough(arc, h + 76);
        AssertCells(store, 4, 6, 0, 1, 144, 8514, 8515);
        LeverArcs.RunThrough(arc, h + 97);
        Assert.Contains(168, LeverArcs.Pcs(arc, B, h + 97));
        Assert.Equal(21, ArcChecks.FrameOf(arc, B, 150) - ArcChecks.FrameOf(arc, B, 141));
        Assert.Equal(21, ArcChecks.FrameOf(arc, B, 159) - ArcChecks.FrameOf(arc, B, 150));
        Assert.Equal(21, ArcChecks.FrameOf(arc, B, 168) - ArcChecks.FrameOf(arc, B, 159));

        // The lever is one-shot (class A stays cleared) and D ran once; the hand was never lost.
        Assert.False(LeverArcs.ClassA(lever));
        Assert.Equal(new[] { h + 1 }, arc.Trace.Where(t => t.Slot == D && t.Pc == 1476).Select(t => t.Frame).ToArray());
        Assert.Empty(lockedTicks);
        Assert.Single(sparks); // the second swing hit nothing.
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
