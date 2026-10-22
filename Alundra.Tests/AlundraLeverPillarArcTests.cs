#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b-2 L4 (docs/plan-e20-heros.md section 1.7, arc D): map 117, the lever 22 (record 22) and the pillar (record 2). The lever is one-shot: its program D
/// clears the class A and raises T230, its program C turns it (animation 2, then 1) and raises T231, which starts the program of the pillar. Every expected
/// value was written by hand before the code from the plan, check.md and maps-117-44.md section 2.2.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraLeverPillarArcTests
{
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;

    [Fact]
    public void D_Map117_TheLever22_StartsThePillar_AtTheTicksOfItsPrograms()
    {
        // Expected, written before the run (plan 1.7 arc D). Hero (72,640,32) facing east; the lever is record 22 at (96,640), the pillar record 2.
        using var arc = new ArcRun(LeverArcs.Spec("E20c1b2-D117", LeverArcs.Zone, LeverArcs.World117, Array.Empty<int>(), 72, 640, 2, 24, 1500));
        var sounds = LeverStampedSounds.Inject(arc);
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var lever = arc.EntityByRecord(22)!;
        var pillar = arc.EntityByRecord(2)!;
        Assert.NotNull(lever);
        Assert.NotNull(pillar);
        Assert.Null(arc.EntityByRecord(0));
        Assert.True(arc.Entities.ToList().IndexOf(pillar) < arc.Entities.ToList().IndexOf(lever), "the pillar must precede the lever 22 in the order of the slots");
        Assert.True(LeverArcs.ClassA(lever));
        LeverArcs.AssertHeroReady(arc, 72, 640, 32);

        var n = arc.Frame;
        arc.Press(LeverArcs.Square);
        Assert.Same(arc.HeroEntity, lever.TouchingEntity);

        // N+1: D[23] (1240, 1241, 1244): class A cleared, T230 set. N+2: C[23]: T230 cleared, animation 2.
        LeverArcs.RunThrough(arc, n + 1);
        Assert.Equal(new[] { 1240, 1241, 1244 }, LeverArcs.Pcs(arc, D, n + 1).Where(pc => pc is >= 1240 and <= 1244).ToArray());
        Assert.False(LeverArcs.ClassA(lever));
        Assert.True(LeverArcs.Temp(230));
        LeverArcs.RunThrough(arc, n + 2);
        Assert.False(LeverArcs.Temp(230));
        Assert.Equal(2u, lever.TargetAnimationId);
        Assert.True(sounds.Contains(n + 2, 218));
        LeverArcs.RunThrough(arc, n + 32);
        Assert.False(LeverArcs.Temp(231));
        Assert.Equal(2u, lever.TargetAnimationId);

        // N+33 (INFERENCE: s + L + 1 = N+2 + 30 + 1): T231 set, animation 1.
        LeverArcs.RunThrough(arc, n + 33);
        Assert.True(LeverArcs.Temp(231));
        Assert.Equal(1u, lever.TargetAnimationId);

        // The pillar: the wait on T231 passes at N+34 (pc 711 first), animation 3 at N+55 (pc 713), the end of the program at N+96 (pc 717, INFERENCE: the
        // animation 3 chains to the animation 0, AnimationEndClassifier.cs:51-63). Its Z is not asserted.
        LeverArcs.RunThrough(arc, n + 34);
        Assert.Equal(n + 34, ArcChecks.FrameOf(arc, C, 711));
        LeverArcs.RunThrough(arc, n + 54);
        Assert.NotEqual(3u, pillar.TargetAnimationId);
        LeverArcs.RunThrough(arc, n + 55);
        Assert.Equal(n + 55, ArcChecks.FrameOf(arc, C, 713));
        Assert.Equal(3u, pillar.TargetAnimationId);
        LeverArcs.RunThrough(arc, n + 95);
        Assert.False(arc.Trace.Any(t => t.Slot == C && t.Pc == 717 && t.Opcode == 0xFF && t.Frame < n + 96));
        LeverArcs.RunThrough(arc, n + 96);
        Assert.Equal(n + 96, ArcChecks.FrameOf(arc, C, 717));

        // One-shot: the class A of the lever never comes back and D ran once.
        LeverArcs.RunThrough(arc, n + 100);
        Assert.False(LeverArcs.ClassA(lever));
        Assert.Equal(new[] { n + 1 }, arc.Trace.Where(t => t.Slot == D && t.Pc == 1240).Select(t => t.Frame).ToArray());
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
