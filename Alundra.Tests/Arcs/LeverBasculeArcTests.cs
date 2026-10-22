#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Events;
using Alundra.Cells;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b-2 L3 (docs/plan-e20-heros.md section 1.7, arc C): map 117, the bascule (record 0). It is a four-state machine of the flags T1 to T4 whose program
/// C rewrites the three platforms S0, S1, S2 (2 x 4 cells at (21,4), (25,4), (29,4)) from the four frames F0 to F3 (2 x 4 cells at x 42, 44, 46, 48, y 4).
/// Every expected value was written by hand before the code from the plan, check.md and maps-117-44.md section 2.1.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class AlundraLeverBasculeArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;

    /// <summary>The platform <paramref name="i"/> (0, 1, 2): cells (21 + 4i .. 22 + 4i, 4 .. 7).</summary>
    private static List<LeverCell> Platform(CellStore store, int i) => LeverArcs.Rect(store, 21 + 4 * i, 4, 2, 4);

    /// <summary>The frame <paramref name="k"/> (0 to 3) of the template: cells (42 + 2k .. 43 + 2k, 4 .. 7).</summary>
    private static List<LeverCell> Frame(CellStore store, int k) => LeverArcs.Rect(store, 42 + 2 * k, 4, 2, 4);

    private static void AssertPlatforms(CellStore store, int[] frames, string when)
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.True(Frame(store, frames[i]).SequenceEqual(Platform(store, i)), $"{when}: platform S{i} is not F{frames[i]}: {string.Join(" ", Platform(store, i))} against {string.Join(" ", Frame(store, frames[i]))}");
            // The only logical difference between the frames: the ground property of the row 6 is 130 in F0 and 2 in the others.
            Assert.Equal(frames[i] == 0 ? 130 : 2, store.GetGroundProperty(21 + 4 * i, 6));
            Assert.Equal(frames[i] == 0 ? 130 : 2, store.GetGroundProperty(22 + 4 * i, 6));
        }
    }

    /// <summary>The wall stacks of the row 5 of the frame: their last four tiles, by frame (F0 ends 8678, 8688, 8698, 8708 and 8679, ...).</summary>
    private static int[] Tail(CellStore store, int x, int y) => LeverArcs.Walls(store, x, y).TakeLast(4).ToArray();

    private sealed record Hit(int[] TurnedOff, int[] TurnedOn, uint FacingInAnimation, uint FinalFacing, (int Platform, int Frame)[] At9, (int Platform, int Frame)[] At15, (int Platform, int Frame)[] At21, int[] After);

    [Fact]
    public void C_Map117_TheBascule_RewritesThePlatformsAtTheTicksOfItsProgram_InFourHits()
    {
        // Expected, written before the run (plan 1.7 arc C). The arrival is in the zone of the bascule room (tile (20,12)): the load program B[2] takes the hand for 24 ticks.
        using var arc = new ArcRun(LeverArcs.Spec("E20c1b2-C117", LeverArcs.Zone, LeverArcs.World117, Array.Empty<int>(), 500, 192, 1, 24, 1500));
        var sounds = LeverStampedSounds.Inject(arc);
        var lockedAfter = new Dictionary<int, bool>();
        arc.OnFrame = () => lockedAfter[arc.Frame - 1] = LeverArcs.Locked();
        arc.OneFrame();
        arc.OneFrame();
        var bascule = arc.EntityByRecord(0)!;
        Assert.NotNull(bascule);
        Assert.Null(arc.EntityByRecord(22));
        var store = LeverArcs.Store(arc);

        // The load: T0 = the first tick (pc 284, lock); F0 at T0, F1 at T0+6, F2 at T0+12, F3 at T0+18 on the three platforms; pc 380 at T0+24, then the hand is free.
        var t0 = ArcChecks.FrameOf(arc, B, 284);
        // The plan writes T0 = 1: the first TICK of the map, counted from 1. The trace frames count from 0, so the first tick is the trace frame 0 (the first frame run); every other value is relative to T0.
        Assert.Equal(0, t0);
        Assert.True(lockedAfter[t0]);
        AssertPlatforms(store, new[] { 0, 0, 0 }, "T0");
        for (var k = 1; k <= 3; k++)
        {
            LeverArcs.RunThrough(arc, t0 + 6 * k - 1);
            AssertPlatforms(store, new[] { k - 1, k - 1, k - 1 }, $"T0+{6 * k - 1}");
            LeverArcs.RunThrough(arc, t0 + 6 * k);
            AssertPlatforms(store, new[] { k, k, k }, $"T0+{6 * k}");
        }

        LeverArcs.RunThrough(arc, t0 + 23);
        Assert.True(lockedAfter[t0 + 23]);
        LeverArcs.RunThrough(arc, t0 + 24);
        Assert.Equal(t0 + 24, ArcChecks.FrameOf(arc, B, 380));
        Assert.False(lockedAfter[t0 + 24]);
        Assert.True(LeverArcs.Temp(1));
        for (var flag = 2u; flag <= 5; flag++)
        {
            Assert.False(LeverArcs.Temp(flag), $"T{flag} set after the load");
        }

        // The templates: F0 .. F3 end by the tiles the plan lists (row 5, even column, odd column).
        Assert.Equal(new[] { 8678, 8688, 8698, 8708 }, Tail(store, 42, 5));
        Assert.Equal(new[] { 8679, 8689, 8699, 8709 }, Tail(store, 43, 5));
        Assert.Equal(new[] { 8676, 8686, 8696, 8706 }, Tail(store, 44, 5));
        Assert.Equal(new[] { 8674, 8684, 8694, 8704 }, Tail(store, 46, 5));
        Assert.Equal(new[] { 8672, 8682, 8692, 8702 }, Tail(store, 48, 5));

        // The four hits (the table of maps-117-44.md 2.1): flags after D, the facing of the animation 1, the final facing, the rewrites at N+9, N+15, N+21.
        var hits = new[]
        {
            new Hit(new[] { 1 }, new[] { 4, 5 }, 16, 24, new[] { (1, 2) }, new[] { (1, 1) }, new[] { (1, 0) }, new[] { 3, 0, 3 }),
            new Hit(new[] { 4 }, new[] { 2, 5 }, 24, 0, new[] { (0, 2), (1, 1) }, new[] { (0, 1), (1, 2) }, new[] { (0, 0), (1, 3) }, new[] { 0, 3, 3 }),
            new Hit(new[] { 2 }, new[] { 3, 5 }, 0, 8, new[] { (2, 2), (0, 1) }, new[] { (2, 1), (0, 2) }, new[] { (2, 0), (0, 3) }, new[] { 3, 3, 0 }),
            new Hit(new[] { 3 }, new[] { 1, 5 }, 8, 16, new[] { (2, 1) }, new[] { (2, 2) }, new[] { (2, 3) }, new[] { 3, 3, 3 }),
        };
        var state = new[] { 3, 3, 3 };
        var previousN = -1000;
        var gap = 27;
        for (var n = 0; n < hits.Length; n++)
        {
            var hit = hits[n];
            // The first hit as soon as the hand is back (one settling frame); the following ones exactly 27 ticks after the previous (the class A is back at N+27).
            arc.Hero.TargetDirection = 24;
            if (n == 0)
            {
                arc.OneFrame();
                LeverArcs.AssertHeroReady(arc, 500, 192, 16);
            }
            else
            {
                LeverArcs.RunThrough(arc, previousN + gap - 1);
            }

            var h = arc.Frame;
            if (n > 0)
            {
                Assert.Equal(previousN + gap, h);
            }

            Assert.Equal(0u, arc.Hero.TargetAnimationId);
            if (n == 0)
            {
                Assert.True(LeverArcs.ClassA(bascule)); // later, the class A comes back during the tick of the press itself (N+27, before the hit pass).
            }

            arc.Press(LeverArcs.Square);
            Assert.Same(arc.HeroEntity, bascule.TouchingEntity);
            previousN = h;

            LeverArcs.RunThrough(arc, h + 1);
            var dPcs = LeverArcs.Pcs(arc, D, h + 1).Where(pc => pc is >= 1184 and <= 1239).ToArray();
            if (n == 0)
            {
                Assert.Equal(new[] { 1184, 1185, 1209, 1212, 1215, 1236, 1239 }, dPcs);
            }
            else
            {
                Assert.Equal(new[] { 1184, 1185 }, dPcs.Take(2).ToArray()); // then the branch of the state: 1190 (T2), 1195 (T3) or the else at 1200.
            }

            Assert.False(LeverArcs.ClassA(bascule));
            foreach (var flag in hit.TurnedOff)
            {
                Assert.False(LeverArcs.Temp((uint)flag), $"hit {n + 1}: T{flag} still set after D");
            }

            foreach (var flag in hit.TurnedOn)
            {
                Assert.True(LeverArcs.Temp((uint)flag), $"hit {n + 1}: T{flag} not set after D");
            }

            // N+2: the facing of the animation 1, animation 1; nothing rewritten yet.
            LeverArcs.RunThrough(arc, h + 2);
            Assert.Equal(hit.FacingInAnimation, bascule.TargetDirection);
            Assert.Equal(1u, bascule.TargetAnimationId);
            Assert.True(sounds.Contains(h + 2, 218));
            LeverArcs.RunThrough(arc, h + 8);
            AssertPlatforms(store, state, $"hit {n + 1}, N+8");
            Assert.Equal(1u, bascule.TargetAnimationId);

            // N+9 (INFERENCE: s + L + 1 = N+2 + 6 + 1): the final facing, animation 0, T5 cleared, sound 224, first rewrite.
            LeverArcs.RunThrough(arc, h + 9);
            Assert.Equal(hit.FinalFacing, bascule.TargetDirection);
            Assert.Equal(0u, bascule.TargetAnimationId);
            Assert.False(LeverArcs.Temp(5));
            Assert.True(sounds.Contains(h + 9, 224));
            foreach (var (platform, frame) in hit.At9)
            {
                state[platform] = frame;
            }

            AssertPlatforms(store, state, $"hit {n + 1}, N+9");
            LeverArcs.RunThrough(arc, h + 14);
            AssertPlatforms(store, state, $"hit {n + 1}, N+14");
            LeverArcs.RunThrough(arc, h + 15);
            foreach (var (platform, frame) in hit.At15)
            {
                state[platform] = frame;
            }

            AssertPlatforms(store, state, $"hit {n + 1}, N+15");
            LeverArcs.RunThrough(arc, h + 20);
            AssertPlatforms(store, state, $"hit {n + 1}, N+20");
            LeverArcs.RunThrough(arc, h + 21);
            foreach (var (platform, frame) in hit.At21)
            {
                state[platform] = frame;
            }

            AssertPlatforms(store, state, $"hit {n + 1}, N+21");
            Assert.Equal(hit.After, state);

            // N+26: still not hittable; N+27: the class A is back (set by the program during the tick). After the first hit the tick N+27 is run here to see it
            // and the next hit is pressed at N+28; the other hits are pressed at N+27 itself, which only works if the class A is back by then.
            LeverArcs.RunThrough(arc, h + 26);
            Assert.False(LeverArcs.ClassA(bascule));
            if (n == 0 || n == hits.Length - 1)
            {
                LeverArcs.RunThrough(arc, h + 27);
                Assert.True(LeverArcs.ClassA(bascule));
                gap = 28;
            }
            else
            {
                gap = 27;
            }
            if (n == 0)
            {
                // The first hit: S1 read through the wall stack of the row 5 and the ground property of the row 6 (F0 there since N+21).
                Assert.Equal(new[] { 8678, 8688, 8698, 8708 }, Tail(store, 25, 5));
                Assert.Equal(new[] { 8679, 8689, 8699, 8709 }, Tail(store, 26, 5));
                Assert.Equal((130, 130), (store.GetGroundProperty(25, 6), store.GetGroundProperty(26, 6)));
            }

            Assert.Equal(hit.After, state);
        }

        // The cycle has period 4: after the fourth hit the three platforms are F3 again, and the facing is the one of the record (16).
        AssertPlatforms(store, new[] { 3, 3, 3 }, "after the fourth hit");
        Assert.Equal(16u, bascule.TargetDirection);
        Assert.True(LeverArcs.Temp(1));
        Assert.False(LeverArcs.Locked());
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
