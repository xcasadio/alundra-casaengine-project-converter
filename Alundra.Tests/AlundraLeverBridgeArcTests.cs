#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b-2 L2 (docs/plan-e20-heros.md section 1.7, arc B): map 116, the levers 39 and 40 and the bridge. The lever 39 alone moves nothing; the
/// lever 40 then makes the bridge program take the hand for 45 ticks and write the five rows of the bridge from the source rectangles; a new hit on the
/// lever 40 retracts it. Every expected value was written by hand before the code from the plan and check.md.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraLeverBridgeArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;

    // -----------------------------------------------------------------------------------------
    // Arc B: 116, the levers 39 and 40 and the bridge
    // -----------------------------------------------------------------------------------------

    /// <summary>The cells (40..42, y) of the bridge.</summary>
    private static List<LeverCell> BridgeRow(AlundraCellStore store, int y) => LeverArcs.Rect(store, 40, y, 3, 1);

    /// <summary>The pcs of the bridge program B[1] (@152, below the one-time ambience B[2] @300, whose End repeats every tick) that ran during the tick.</summary>
    private static int[] Bridge116(ArcRun arc, int tick) => LeverArcs.Pcs(arc, B, tick).Where(pc => pc < 300).ToArray();

    [Fact]
    public void B_Map116_TheTwoLeversExtendTheBridge_AndTheSecondHitOnTheLever40RetractsIt()
    {
        // Expected, written before the run (plan 1.7 arc B). Arrival tile (3,20) (pixel (84,328)), then the hero stands at (866,424,128) facing west.
        using var arc = new ArcRun(LeverArcs.Spec("E20c1b2-B116", LeverArcs.Zone, LeverArcs.World116, Array.Empty<int>(), 84, 328, 0, 8, 1500));
        var sounds = LeverStampedSounds.Inject(arc);
        var lockedAfter = new Dictionary<int, bool>();
        arc.OnFrame = () => lockedAfter[arc.Frame - 1] = LeverArcs.Locked();
        arc.OneFrame();
        Assert.Null(arc.EntityByRecord(7));
        Assert.Null(arc.EntityByRecord(8));
        var lever39 = arc.EntityByRecord(39)!;
        var lever40 = arc.EntityByRecord(40)!;
        Assert.NotNull(lever39);
        Assert.NotNull(lever40);
        Assert.True(LeverArcs.ClassA(lever39) && LeverArcs.ClassA(lever40));
        var store = LeverArcs.Store(arc);

        // The references of the bridge, read after the first frame (the load program writes the retracted bridge: (42,24) has GP 2 then, not the exported 18).
        var baseline = LeverArcs.Rect(store, 40, 20, 3, 5);
        Assert.Equal(2, baseline[14].GroundProperty);

        arc.PlaceHero(866, 424, 128);
        arc.Hero.TargetDirection = 8;
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        LeverArcs.AssertHeroReady(arc, 866, 424, 128);

        // Lever 39, hit at H1.
        var h1 = arc.Frame;
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, lever39.TouchingEntity);
        LeverArcs.RunThrough(arc, h1 + 1);
        Assert.Equal(new[] { 1076, 1077, 1080, 1083 }, LeverArcs.Pcs(arc, D, h1 + 1));
        Assert.True(LeverArcs.Temp(1));
        Assert.True(LeverArcs.Temp(101));
        Assert.False(LeverArcs.ClassA(lever39));
        Assert.Equal(new[] { 164, 163 }, Bridge116(arc, h1 + 1)); // T2 off: the bridge program does not move.
        LeverArcs.RunThrough(arc, h1 + 2);
        var c39 = LeverArcs.Pcs(arc, C, h1 + 2).Where(pc => pc is >= 692 and <= 735).ToArray();
        Assert.Contains(702, c39);
        Assert.Contains(703, c39);
        Assert.Contains(720, c39);
        Assert.Equal(2u, lever39.TargetAnimationId);
        Assert.Equal(new[] { 164, 169, 163 }, Bridge116(arc, h1 + 2)); // T1 on, T2 off: no extension.
        LeverArcs.RunThrough(arc, h1 + 32);
        Assert.False(LeverArcs.ClassA(lever39));
        Assert.True(LeverArcs.Temp(101));
        LeverArcs.RunThrough(arc, h1 + 33); // INFERENCE: s + L + 1 = H1+2 + 30 + 1.
        Assert.True(LeverArcs.ClassA(lever39));
        Assert.False(LeverArcs.Temp(101));
        Assert.True(LeverArcs.Temp(1));
        Assert.Equal(1u, lever39.TargetAnimationId);
        Assert.Equal(baseline, LeverArcs.Rect(store, 40, 20, 3, 5)); // one lever alone moves nothing.

        // The hero is free (the swing was over at H1+21): lever 40.
        arc.PlaceHero(1106, 424, 128);
        arc.Hero.TargetDirection = 8;
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        LeverArcs.AssertHeroReady(arc, 1106, 424, 128);
        var h2 = arc.Frame;
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, lever40.TouchingEntity);
        LeverArcs.RunThrough(arc, h2 + 1);
        Assert.True(LeverArcs.Temp(2));
        Assert.True(LeverArcs.Temp(102));
        Assert.False(lockedAfter[h2 + 1]);

        // t0 = H2+2: the extension starts: control lost, the hero's animation 0, the sound 221, row 20 written.
        var t0 = h2 + 2;
        Assert.Equal(baseline, LeverArcs.Rect(store, 40, 20, 3, 5));
        LeverArcs.RunThrough(arc, t0);
        var bridge = Bridge116(arc, t0);
        Assert.Contains(174, bridge);
        Assert.True(lockedAfter[t0]);
        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        Assert.True(sounds.Contains(t0, 221));
        LeverArcs.AssertTuples(BridgeRow(store, 20), (0, 10, 0, 16574), (0, 10, 0, 16575), (0, 8, 2, 58146));
        Assert.Equal(new[] { 49979, 49989 }, LeverArcs.Walls(store, 40, 20));
        Assert.Equal(new[] { 49980, 49990 }, LeverArcs.Walls(store, 41, 20));
        Assert.Equal(LeverArcs.Rect(store, 29, 26, 3, 1), BridgeRow(store, 20)); // row 20 is a copy of the source (29,26) 3x1.
        for (var y = 21; y <= 24; y++)
        {
            Assert.Equal(baseline.Skip((y - 20) * 3).Take(3).ToList(), BridgeRow(store, y));
        }

        // t0+9, +18, +27, +36: rows 21 to 24 from the source (29,27), the third tile 58173.
        var source = LeverArcs.Rect(store, 29, 27, 3, 1);
        for (var step = 1; step <= 4; step++)
        {
            var y = 20 + step;
            LeverArcs.RunThrough(arc, t0 + 9 * step - 1);
            Assert.Equal(baseline.Skip((y - 20) * 3).Take(3).ToList(), BridgeRow(store, y));
            LeverArcs.RunThrough(arc, t0 + 9 * step);
            LeverArcs.AssertTuples(BridgeRow(store, y), (0, 10, 0, 16574), (0, 10, 0, 16575), (0, 8, 2, 58173));
            Assert.Equal(source, BridgeRow(store, y));
        }

        // t0+45: the hand is given back (pc 225), not before.
        LeverArcs.RunThrough(arc, t0 + 44);
        Assert.True(lockedAfter[t0 + 44]);
        LeverArcs.RunThrough(arc, t0 + 45);
        Assert.Contains(225, Bridge116(arc, t0 + 45));
        Assert.False(lockedAfter[t0 + 45]);
        for (var tick = t0; tick <= t0 + 44; tick++)
        {
            Assert.True(lockedAfter[tick], $"control not locked at t0+{tick - t0}");
        }

        // Retraction: a new hit on the lever 40 at H3 >= H2+48 (the class A was reset at H2+33, the hand returned at H2+47).
        LeverArcs.RunThrough(arc, h2 + 47);
        Assert.True(LeverArcs.ClassA(lever40));
        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        var h3 = arc.Frame;
        Assert.True(h3 >= h2 + 48);
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, lever40.TouchingEntity);
        LeverArcs.RunThrough(arc, h3 + 1);
        Assert.False(LeverArcs.Temp(2)); // the toggle.
        Assert.True(LeverArcs.Temp(102));
        var r = h3 + 2;
        LeverArcs.RunThrough(arc, r);
        var back = Bridge116(arc, r);
        Assert.Contains(227, back);
        Assert.Contains(232, back);
        Assert.Contains(241, back);
        Assert.True(lockedAfter[r]);
        Assert.True(sounds.Contains(r, 221));
        Assert.Equal(LeverArcs.Rect(store, 30, 23, 3, 1), BridgeRow(store, 24)); // row 24 first, from (30,23).
        for (var step = 1; step <= 4; step++)
        {
            LeverArcs.RunThrough(arc, r + 9 * step - 1);
            var rowWritten = 24 - step; // the row that the step writes.
            Assert.NotEqual(LeverArcs.Rect(store, 30, 23 - step, 3, 1), BridgeRow(store, rowWritten));
            LeverArcs.RunThrough(arc, r + 9 * step);
            Assert.Equal(LeverArcs.Rect(store, 30, 23 - step, 3, 1), BridgeRow(store, rowWritten)); // rows 23, 22, 21, 20 from (30,22), (30,21), (30,20), (30,19).
        }

        LeverArcs.RunThrough(arc, r + 44);
        Assert.True(lockedAfter[r + 44]);
        LeverArcs.RunThrough(arc, r + 45);
        Assert.False(lockedAfter[r + 45]);
        Assert.Equal(baseline, LeverArcs.Rect(store, 40, 20, 3, 5)); // the bridge is back to the references of the first frame.
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
