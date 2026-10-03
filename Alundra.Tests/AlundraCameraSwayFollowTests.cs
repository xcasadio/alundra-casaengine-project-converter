#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.k1 K2 (docs/plan-e19-opcodes.md, section 1.2k.1): the camera follow adds the sway offset to the scroll at every
/// tick, between the follow step and the map bound, never on the snap (<see cref="AlundraCameraMath.AdvanceCameraSmoothing"/>).
/// Fixed target, fresh sway instance, one call per tick; values are read in the original's scroll space
/// (<see cref="AlundraCameraMath.ToOriginalScrollSpace"/>) and are relative to the rest position (the state sitting on
/// the target). Sequences reproduced by the audit's independent model (<c>0x8002CDA0</c>).
/// </summary>
public sealed class AlundraCameraSwayFollowTests
{
    private static readonly Vector3 RestTarget = new(500f, -400f, 0f);

    private static (int X, int Y) Scroll(Vector3 target) => AlundraCameraMath.ToOriginalScrollSpace(target);

    private static (int X, int Y) RestScroll => Scroll(RestTarget);

    private static AlundraCameraSway NewSway(int speedX, int speedY, int limitX, int limitY)
    {
        var sway = new AlundraCameraSway();
        sway.Start(speedX, speedY, limitX, limitY);
        return sway;
    }

    /// <summary>One <c>AdvanceCameraSmoothing</c> call per tick; the first call is the snap when asked.</summary>
    private static (List<int> X, List<int> Y) Run(
        AlundraCameraSway sway, int calls, bool snapFirst = false, int? mapWidthPx = null, int? mapHeightPx = null,
        Vector3? start = null)
    {
        var state = start ?? RestTarget;
        var xs = new List<int>();
        var ys = new List<int>();
        for (var i = 0; i < calls; i++)
        {
            state = AlundraCameraMath.AdvanceCameraSmoothing(
                state, snapFirst && i == 0, RestTarget, 1, mapWidthPx, mapHeightPx, sway);
            var (x, y) = Scroll(state);
            xs.Add(x - RestScroll.X);
            ys.Add(y - RestScroll.Y);
        }

        return (xs, ys);
    }

    [Fact]
    public void Sway_1_1_3_2_MovesTheScrollByTheIntegralOfTheOffset_AndComesBackToRest()
    {
        var (xs, ys) = Run(NewSway(1, 1, 3, 2), 12);

        Assert.Equal(new[] { -1, -3, -6, -8, -9, -9, -8, -6, -3, -1, 0, 0 }, xs);
        Assert.Equal(new[] { -1, -3, -4, -4, -3, -1, 0, 0 }, ys.GetRange(0, 8));
    }

    [Fact]
    public void Sway_WithTheSnapOnTheFirstCall_TheSnapTickAddsNoOffset_AndTheFollowPullsBackOnePixel()
    {
        var (xs, _) = Run(NewSway(1, 1, 3, 2), 12, snapFirst: true);

        Assert.Equal(new[] { 0, -2, -5, -7, -8, -8, -7, -5, -2, 0, 1, 0 }, xs);
    }

    [Fact]
    public void Sway_ASnapFrameOfThreeTicks_SnapsThenTwoFullTicks_ButThreeSwaySteps()
    {
        var sway = NewSway(1, 1, 3, 2);

        var state = AlundraCameraMath.AdvanceCameraSmoothing(RestTarget, true, RestTarget, 3, null, null, sway);

        var (x, y) = Scroll(state);
        Assert.Equal((-5, -3), (x - RestScroll.X, y - RestScroll.Y));
        Assert.Equal((-3, -1), (sway.OffsetX, sway.OffsetY));
        Assert.Equal((1, 1), (sway.ReachX, sway.ReachY));
    }

    [Fact]
    public void Sway_ASnapFrameOfZeroTicks_SnapsAlone_WithoutAStep()
    {
        var sway = NewSway(1, 1, 3, 2);

        var state = AlundraCameraMath.AdvanceCameraSmoothing(RestTarget, true, RestTarget, 0, null, null, sway);

        Assert.Equal(RestTarget, state);
        Assert.Equal((0, 0, 0, 0), (sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY));
    }

    [Fact]
    public void Sway_AtTheLeftBorder_TheBoundCutsTheNegativeSideAndTheFollowAbsorbsTheRest()
    {
        // Map 1248x960, rest at the top-left scroll (0, 0): render (160, -120). Y is irrelevant here.
        var rest = new Vector3(160f, -400f, 0f);
        var sway = NewSway(1, 1, 3, 2);
        var state = rest;
        var xs = new List<int>();
        for (var i = 0; i < 14; i++)
        {
            state = AlundraCameraMath.AdvanceCameraSmoothing(state, false, rest, 1, 1248, 960, sway);
            xs.Add(Scroll(state).X);
        }

        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 1, 2, 4, 5, 5, 4, 2, 0 }, xs);
    }

    [Fact]
    public void Sway_AfterTheStop_TheCameraStaysWhereItIs_InsideTheDeadZoneOfTheFollow()
    {
        var sway = NewSway(1, 1, 3, 2);
        var state = RestTarget;
        for (var i = 0; i < 5; i++)
        {
            state = AlundraCameraMath.AdvanceCameraSmoothing(state, false, RestTarget, 1, null, null, sway);
        }

        Assert.Equal(-9, Scroll(state).X - RestScroll.X);

        sway.Stop();
        for (var i = 0; i < 4; i++)
        {
            state = AlundraCameraMath.AdvanceCameraSmoothing(state, false, RestTarget, 1, null, null, sway);
            Assert.Equal(-9, Scroll(state).X - RestScroll.X);
        }
    }

    [Fact]
    public void WithoutASway_OrWithAStoppedOne_TheStepIsUnchanged()
    {
        var withNull = AlundraCameraMath.AdvanceCameraSmoothing(RestTarget + new Vector3(40f, -40f, 0f), false, RestTarget, 2, null, null);
        var withStopped = AlundraCameraMath.AdvanceCameraSmoothing(
            RestTarget + new Vector3(40f, -40f, 0f), false, RestTarget, 2, null, null, new AlundraCameraSway());

        Assert.Equal(withNull, withStopped);
        // Two plain follow steps from a gap of (40, -40): floor 40>>4 = 2 then ... pinned against the pure step itself.
        var expected = RestTarget + new Vector3(40f, -40f, 0f);
        for (var i = 0; i < 2; i++)
        {
            expected = AlundraCameraMath.StepCameraScroll(expected, RestTarget);
        }

        Assert.Equal(expected, withNull);
    }
}
