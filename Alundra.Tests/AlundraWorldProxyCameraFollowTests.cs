using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the pure math of E5.a's scripted camera follow (docs/plan-e5-camera.md) -
/// <see cref="AlundraCameraMath.ResolveCameraLookAt"/>, <see cref="AlundraCameraMath.ComputeCameraLookAtRenderPosition"/>,
/// <see cref="AlundraCameraMath.ClampCameraTargetToMap"/>, <see cref="AlundraCameraMath.ComputeCameraZoom"/>
/// and <see cref="AlundraCameraMath.ComputeSmoothedCameraTarget"/> (the snap-or-step-then-clamp state
/// transition) - plus E5.c's own integer scroll port, <see cref="AlundraCameraMath.StepCameraScroll"/>
/// and <see cref="AlundraCameraMath.AdvanceCameraSmoothing"/> (the per-frame seam
/// <see cref="AlundraWorldProxy.UpdateCameraFollow"/> calls with that frame's LOGIC TICK count).
///
/// Not covered here (headless-untestable, needs a live World/Camera2dComponent): the thin wiring around
/// that state transition (<see cref="AlundraWorldProxy.UpdateCameraFollow"/>'s own null-guard/field
/// writes, and <see cref="AlundraWorldProxy.ResolveDebugCameraOnce"/>) -
/// same shape as <see cref="AlundraWorldProxyDebugCameraPanTests"/>'s own doc on
/// <see cref="AlundraWorldProxy.UpdateDebugCameraPan"/>. <see cref="AlundraEventProgramRunnerTests"/> covers
/// opcodes 0x67/0x68/0x69 (the only way <see cref="AlundraWorldProxy.EntityFollowedByCamera"/> is written
/// at runtime, besides pawn adoption).
/// </summary>
public class AlundraWorldProxyCameraFollowTests
{
    // -----------------------------------------------------------------------------------------
    // ResolveCameraLookAt - port of GameEngine.cs:1747-1752's own gate
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ResolveCameraLookAt_ValidTarget_AdoptsCandidatePosition()
    {
        var result = AlundraCameraMath.ResolveCameraLookAt(
            hasValidTarget: true, candidateX: 100, candidateY: 200, candidateZ: 3,
            previousX: 1, previousY: 2, previousZ: 3000);

        Assert.Equal((100, 200, 3), result);
    }

    [Fact]
    public void ResolveCameraLookAt_NoValidTarget_FreezesOnPreviousPosition_NeverFallsBackToCandidate()
    {
        // Simulates a destroyed followed entity: hasValidTarget is false regardless of whatever the
        // (still non-null, but destroyed) entity's own current position is - the candidate here is
        // deliberately a totally different, implausible position (e.g. wherever the player happens to be)
        // to prove the result is NOT that candidate.
        var result = AlundraCameraMath.ResolveCameraLookAt(
            hasValidTarget: false, candidateX: 9999, candidateY: 9999, candidateZ: 9999,
            previousX: 804, previousY: 952, previousZ: 0);

        Assert.Equal((804, 952, 0), result);
        Assert.NotEqual((9999, 9999, 9999), result);
    }

    // -----------------------------------------------------------------------------------------
    // ComputeCameraLookAtRenderPosition - framing/centre-bias (decision E5-1)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Pinned test (plan acceptance): hand-computed at the hero's own New Game logical position, (804,
    /// 952, 0) - AlundraWorldProxy.AdoptPlayerPawn: PosX = (33*24+12)&lt;&lt;16 = 804&lt;&lt;16, PosY =
    /// (59*16+8)&lt;&lt;16 = 952&lt;&lt;16, PosZ = 0 (AlundraGameState.CameraTileX/Y = 33/59, TileWidth/
    /// Height = 24/16). By-hand arithmetic:
    /// <list type="bullet">
    /// <item><description>renderX = X = 804 (no X bias - 0xa0 is exactly half of 320).</description></item>
    /// <item><description>renderY (pre-bias) = -(Y - Z) = -(952 - 0) = -952 (SimulationSpacePolicy's own
    /// Y-flip).</description></item>
    /// <item><description>+16 centre-bias (0x88 = 136 is 16px more than half of 240) = -952 + 16 =
    /// -936.</description></item>
    /// </list>
    /// So Target = (804, -936, 0).
    /// </summary>
    [Fact]
    public void ComputeCameraLookAtRenderPosition_HeroNewGamePosition_MatchesHandComputedTarget()
    {
        var result = AlundraCameraMath.ComputeCameraLookAtRenderPosition(804, 952, 0);

        Assert.Equal(new Vector3(804f, -936f, 0f), result);
    }

    [Fact]
    public void ComputeCameraLookAtRenderPosition_ZElevation_RaisesRenderY()
    {
        // Y - Z shrinks as Z (elevation) rises, so render Y (= -(Y-Z) + 16) increases (moves up-screen) -
        // an elevated entity's look-at sits higher on screen than a grounded one at the same X/Y.
        var grounded = AlundraCameraMath.ComputeCameraLookAtRenderPosition(100, 200, 0);
        var elevated = AlundraCameraMath.ComputeCameraLookAtRenderPosition(100, 200, 32);

        Assert.True(elevated.Y > grounded.Y);
        Assert.Equal(grounded.Y + 32f, elevated.Y);
    }

    // -----------------------------------------------------------------------------------------
    // StepCameraScroll - E5.c's integer port of GraphicManager.cs:75-92's own `scroll += diff >> 4`
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// E5.c: the ONE rule the whole slice rests on, and the easiest to get wrong. <c>&gt;&gt; 4</c> is an
    /// arithmetic shift (a FLOOR), so it is not symmetric; our render space flips Y relative to the
    /// original's scroll space (<c>renderY = -scrollY - 120</c>, <c>renderX = scrollX + 160</c>, both
    /// confirmed by <see cref="AlundraCameraMath.ClampCameraTargetToMap"/>'s own frozen map-389 bounds).
    /// The increment is therefore FLOOR on X and CEILING on Y - the same convention E5.b established for
    /// sprites. Every row below was checked against the original by computing <c>(-delta) &gt;&gt; 4</c> in
    /// scroll space and converting back.
    ///
    /// Discriminating by construction: a naive floor on BOTH axes fails on delta = 1, 7 and 15; a
    /// round-to-nearest fails on delta = 7 and -7; a truncation toward zero fails on delta = -1, -7, -15.
    /// Rows 15/16 pin the dead zone (nothing moves on X until the gap reaches 16), row 1600 pins the rate.
    /// </summary>
    [Theory]
    [InlineData(-40, -3, -2)]
    [InlineData(-16, -1, -1)]
    [InlineData(-15, -1, 0)]
    [InlineData(-7, -1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(7, 0, 1)]
    [InlineData(15, 0, 1)]
    [InlineData(16, 1, 1)]
    [InlineData(1600, 100, 100)]
    public void StepCameraScroll_MatchesTheOriginalShiftOnEverySign(int delta, int expectedX, int expectedY)
    {
        var current = new Vector3(0f, 0f, 0f);
        var target = new Vector3(delta, delta, 0f);

        var stepped = AlundraCameraMath.StepCameraScroll(current, target);

        Assert.Equal(expectedX, stepped.X);
        Assert.Equal(expectedY, stepped.Y);
        Assert.Equal(0f, stepped.Z);
    }

    // -----------------------------------------------------------------------------------------
    // ClampCameraTargetToMap - bounds derived from TileMapData.MapSize, verified against the
    // original's own hardcoded 0x39f/0x2cf scroll-clamp constants (map 389, 1248x960px)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Map 389 is 52x60 tiles = 1248x960px (52*24, 60*16). Derived bounds:
    /// TargetX in [160, 1248-160-1] = [160, 1087]; TargetY in [-(960-120-1), -120] = [-839, -120].
    /// Converted back to the original's own top-left scroll coordinates (X: Target-160, Y: -Target-120)
    /// these are EXACTLY scrollX in [0, 927] and scrollY in [0, 719] - i.e. 0x39f and 0x2cf, the
    /// original's own hardcoded constants (GraphicManager.cs:97-122) - confirming the derivation.
    /// </summary>
    [Fact]
    public void ClampCameraTargetToMap_Map389Bounds_MatchOriginalHardcodedConstants()
    {
        const int mapWidthPx = 1248;
        const int mapHeightPx = 960;

        Assert.Equal(1087f, AlundraCameraMath.ClampCameraTargetToMap(new Vector3(99999f, -500f, 0f), mapWidthPx, mapHeightPx).X);
        Assert.Equal(160f, AlundraCameraMath.ClampCameraTargetToMap(new Vector3(-99999f, -500f, 0f), mapWidthPx, mapHeightPx).X);
        Assert.Equal(-120f, AlundraCameraMath.ClampCameraTargetToMap(new Vector3(500f, 99999f, 0f), mapWidthPx, mapHeightPx).Y);
        Assert.Equal(-839f, AlundraCameraMath.ClampCameraTargetToMap(new Vector3(500f, -99999f, 0f), mapWidthPx, mapHeightPx).Y);

        // 0x39f/0x2cf cross-check, converted back to the original's own top-left scroll coordinates
        // (scrollX = TargetX - 160, scrollY = -TargetY - 120). Target.X's max and Target.Y's min are the
        // SAME corner of the map (bottom-right in the original's down-positive screen space), so the
        // probe pushes X positive and Y negative together.
        var maxTarget = AlundraCameraMath.ClampCameraTargetToMap(new Vector3(99999f, -99999f, 0f), mapWidthPx, mapHeightPx);
        Assert.Equal(0x39f, (int)(maxTarget.X - 160f));
        Assert.Equal(0x2cf, (int)(-maxTarget.Y - 120f));
    }

    [Fact]
    public void ClampCameraTargetToMap_InsideBounds_LeavesTargetUnchanged()
    {
        var target = new Vector3(600f, -400f, 0f);

        var result = AlundraCameraMath.ClampCameraTargetToMap(target, 1248, 960);

        Assert.Equal(target, result);
    }

    [Fact]
    public void ClampCameraTargetToMap_NearEdge_KeepsViewInsideTheMap()
    {
        // A look-at near the map's bottom-right corner (e.g. block 18's own area) must not push the
        // camera's visible 320x240 area outside the map.
        var target = new Vector3(1240f, -955f, 0f);

        var result = AlundraCameraMath.ClampCameraTargetToMap(target, 1248, 960);

        Assert.InRange(result.X, 160f, 1248f - 160f);
        Assert.InRange(result.Y, -(960f - 120f), -120f);
    }

    // -----------------------------------------------------------------------------------------
    // ComputeCameraZoom - runtime framing (decision E5-1)
    //
    // FIX (fresh verifier of cc1fc60): the divisor is the original's own DISPLAY height (236,
    // StaticVariables.ScreenHeight/AlundraDisplay.NativeHeight - the actual rendered framebuffer), not
    // its separate CLAMP height (240, GraphicManager.cs's own scroll-clamp arithmetic - still used
    // unchanged by ClampCameraTargetToMap). See AlundraWorldProxy's own CameraDisplayHeight doc for the
    // full display-vs-clamp investigation (file:line citations). At the real 1280x944 window (944 = 236
    // x PixelScale 4) this now yields an exact integer zoom of 4 - pixel-perfect - instead of the old
    // 944/240 = 3.9333.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputeCameraZoom_RealWindowHeight_IsExactlyFour()
    {
        // 944 = AlundraDisplay.WindowHeight (236 native x 4 PixelScale) - the actual window this DLL runs
        // in. Must be an exact integer zoom: pixel-perfect rendering requires it, not merely "close to 4".
        Assert.Equal(4f, AlundraCameraMath.ComputeCameraZoom(944));
    }

    [Fact]
    public void ComputeCameraZoom_ExactMultipleOf236_IsIntegerZoom()
    {
        Assert.Equal(2f, AlundraCameraMath.ComputeCameraZoom(472));
    }

    [Fact]
    public void ComputeCameraZoom_IsComputedFromViewport_NotHardcoded()
    {
        // Different viewport heights must yield different zooms - proves the value is derived, not a
        // constant 4.
        Assert.Equal(2f, AlundraCameraMath.ComputeCameraZoom(472));
        Assert.Equal(1f, AlundraCameraMath.ComputeCameraZoom(236));
    }

    // -----------------------------------------------------------------------------------------
    // FIX (fresh verifier of cc1fc60) - the clamp must feed back into the smoothing state, exactly like
    // the original's own g_cameraScrollingX/Y assignment (GraphicManager.cs:97-122 clamps IN PLACE the
    // same fields the catch-up formula at :75-92 reads back next frame). Covered here at the pure-math
    // level: a smoothed target sitting outside the clamp bounds (as if a previous frame's lerp had
    // overshot past an edge) must clamp on THIS call - proven by feeding the clamped result back in as
    // "current" for a second call and checking the position no longer moves at all once already pinned,
    // and moves immediately (not gradually) once the look-at target moves back inside bounds. This is a
    // pure-math analogue of AlundraWorldProxyCameraFollowTests's own scope note: UpdateCameraFollow
    // itself needs a live World/Camera2dComponent and is out of reach here, but the exact sequence it
    // performs - smooth, then clamp, then feed the clamp back into next frame's smoothing input - is
    // fully exercised by chaining StepCameraScroll -> ClampCameraTargetToMap -> (next call) by hand,
    // matching what AdvanceCameraSmoothing does per logic tick before UpdateCameraFollow stores the
    // result back into _cameraSmoothedTarget.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Exercises <see cref="AlundraCameraMath.ComputeSmoothedCameraTarget"/> - the exact state-transition
    /// <see cref="AlundraWorldProxy.UpdateCameraFollow"/> now calls every frame - across a short sequence
    /// of frames: pin the camera at the map's bottom edge (an unclamped look-at of -936, the verifier's
    /// own measured hidden overshoot, 97px past the -839 lower bound) for several ticks, THEN move the
    /// look-at inward to -800 (safely inside <c>[-839, -120]</c>) and take one more tick.
    ///
    /// Asserts the FIXED behaviour: because <c>ComputeSmoothedCameraTarget</c> returns (and this test
    /// feeds back in as next frame's <c>previousSmoothedTarget</c>) the CLAMPED value, the smoothing state
    /// is already sitting exactly at -839 once the look-at moves inward - so the very next tick steps from
    /// -839 toward -800 and moves immediately, not still -839.
    ///
    /// E5.c updated the expected value: under the integer port the step is <c>ceil(delta / 16)</c> with
    /// delta = -800 - (-839) = +39, i.e. <c>ceil(2.4375) = 3</c>, so the state lands on exactly -836. (The
    /// former -836.5625 was the float lerp's own 1/16 of 39; that smoothing no longer exists - see
    /// <see cref="AlundraCameraMath.StepCameraScroll"/>.) The PROPERTY under test is unchanged.
    ///
    /// A pre-fix implementation of this method (clamp applied only to the RETURNED render value, feeding
    /// the UNCLAMPED step result back as next frame's state) would still read -839 here: one step from the
    /// hidden -936 toward -800 only reaches -928 internally, which the clamp still pins to -839 on the way
    /// out - motion would stay invisible for many more ticks until the hidden internal state alone climbed
    /// back above the bound. This assertion therefore fails against that pre-fix shape and passes against
    /// the current one.
    /// </summary>
    [Fact]
    public void ComputeSmoothedCameraTarget_PinnedAtBoundThenTargetMovesInward_NextTickMovesImmediately()
    {
        const int mapWidthPx = 1248;
        const int mapHeightPx = 960;

        // Several frames pinned at the bottom edge with an unclamped look-at of -936 (the verifier's own
        // measured overshoot) - each call feeds the PREVIOUS call's returned (clamped) value back in,
        // exactly as UpdateCameraFollow's _cameraSmoothedTarget field does frame to frame.
        var smoothed = new Vector3(804f, 0f, 0f);
        var pinnedLookAt = new Vector3(804f, -936f, 0f);
        for (var i = 0; i < 5; i++)
        {
            smoothed = AlundraCameraMath.ComputeSmoothedCameraTarget(
                smoothed, needsSnap: i == 0, pinnedLookAt, mapWidthPx, mapHeightPx);
        }

        Assert.Equal(-839f, smoothed.Y); // clamped every frame, matches the plan's own documented bound.

        // Look-at now moves inward (e.g. the followed entity steps away from the map edge) - one more
        // tick.
        var newLookAt = new Vector3(804f, -800f, 0f);
        var nextFrame = AlundraCameraMath.ComputeSmoothedCameraTarget(
            smoothed, needsSnap: false, newLookAt, mapWidthPx, mapHeightPx);

        Assert.NotEqual(-839f, nextFrame.Y);
        Assert.Equal(-836f, nextFrame.Y); // ceil(39 / 16) = 3, see this test's own doc.
    }
    // -----------------------------------------------------------------------------------------
    // E5.c - AdvanceCameraSmoothing: the camera catches up once per LOGIC TICK, never per rendered
    // frame. Root cause of the reported vibration was that mismatch: a followed sprite only moves on a
    // 50Hz tick, so a camera that kept creeping between ticks crossed whole-pixel boundaries on its own
    // and slid the sprite around the screen. See AlundraWorldProxy.UpdateCameraFollow's own doc.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The heart of the fix: a rendered frame that carried NO logic tick must leave the camera exactly
    /// where it was. Fails against any per-frame smoothing, which by definition always advances.
    /// </summary>
    [Fact]
    public void AdvanceCameraSmoothing_ZeroTicks_LeavesTheStateStrictlyUnchanged()
    {
        var previous = new Vector3(804f, -936f, 0f);
        var lookAt = new Vector3(1000f, -100f, 0f);

        var result = AlundraCameraMath.AdvanceCameraSmoothing(
            previous, needsSnap: false, lookAt, ticksThisFrame: 0, mapWidthPx: null, mapHeightPx: null);

        Assert.Equal(previous, result);
    }

    /// <summary>
    /// Map entry (port of <c>g_isCameraScrolling = 1</c>): the snap jumps straight to the clamped look-at,
    /// and consumes no tick - after it, further steps THIS frame are a fixed point (the target is either
    /// already reached, or outside the bounds so the step moves outward and the clamp pins it back onto
    /// the same bound). Asserted both ways so the "no decrement" decision is pinned rather than assumed.
    /// </summary>
    [Fact]
    public void AdvanceCameraSmoothing_MapEntrySnap_ReachesTheClampedLookAtAndIsAFixedPoint()
    {
        const int mapWidthPx = 1248;
        const int mapHeightPx = 960;

        // Look-at 97px past the -839 lower bound, E5.a's own measured New Game overshoot.
        var lookAt = new Vector3(804f, -936f, 0f);
        var expected = AlundraCameraMath.ClampCameraTargetToMap(lookAt, mapWidthPx, mapHeightPx);

        var snappedNoTick = AlundraCameraMath.AdvanceCameraSmoothing(
            Vector3.Zero, needsSnap: true, lookAt, ticksThisFrame: 0, mapWidthPx, mapHeightPx);
        var snappedFourTicks = AlundraCameraMath.AdvanceCameraSmoothing(
            Vector3.Zero, needsSnap: true, lookAt, ticksThisFrame: 4, mapWidthPx, mapHeightPx);

        Assert.Equal(expected, snappedNoTick);
        Assert.Equal(expected, snappedFourTicks);
    }

    /// <summary>
    /// The integer invariant that lets the smoothing state BE the rendered value (E5.c removed the
    /// separate write-time pixel snap): whatever the tick count, and even with the clamp firing on every
    /// step, every component stays a whole number.
    /// </summary>
    [Fact]
    public void AdvanceCameraSmoothing_WithClampActive_KeepsTheStateWholeNumbered()
    {
        const int mapWidthPx = 1248;
        const int mapHeightPx = 960;

        var state = new Vector3(804f, -400f, 0f);
        var lookAt = new Vector3(5000f, -9000f, 0f); // far outside the map, clamp fires every step.

        for (var tick = 0; tick < 200; tick++)
        {
            state = AlundraCameraMath.AdvanceCameraSmoothing(
                state, needsSnap: false, lookAt, ticksThisFrame: 1, mapWidthPx, mapHeightPx);

            Assert.Equal(MathF.Truncate(state.X), state.X);
            Assert.Equal(MathF.Truncate(state.Y), state.Y);
            Assert.Equal(MathF.Truncate(state.Z), state.Z);
        }
    }

    /// <summary>
    /// The acceptance test of the whole slice, and the one the previous attempt (445594e) got wrong by
    /// running only 14 frames from a hand-picked tick phase. Drives 3000 logic ticks of a followed entity
    /// moving at a steady rate, exactly as production does: its logical position is truncated to an int
    /// look-at (<c>PosY &gt;&gt; 16</c>), the look-at is turned into a render target by
    /// <see cref="AlundraCameraMath.ComputeCameraLookAtRenderPosition"/>, and the camera takes ONE
    /// <see cref="AlundraCameraMath.AdvanceCameraSmoothing"/> step per tick. The sprite's own rendered Y
    /// is the negated look-at (<c>ceil(-x) = -floor(x)</c>, E5.b's own identity), so the on-screen gap is
    /// <c>(-look) - state.Y</c> in logical pixels.
    ///
    /// Asserts ZERO direction reversals of that gap over ticks 1500-3000: under the integer port the gap
    /// is a genuine FIXED POINT, because the ceiling absorbs the target's irregular 1-1-2 stepping exactly
    /// (delta -31 -&gt; -1, delta -32 -&gt; -2). Measured: gap constant at -30 for 1.22 px/tick, -45 for
    /// 2.4 px/tick.
    ///
    /// The counter-proof in the same test replays the SAME tick sequence through the rejected float 1/16
    /// smoothing (a local reference implementation, not production) and requires it to reverse at least
    /// <paramref name="minimumFloatReversals"/> times - measured 480 at 1.22 px/tick and 1197 at 2.4, i.e.
    /// the very shimmer this slice removes. At 3.7 px/tick the float variant happens to be stable too, so
    /// that row carries no counter-proof (0) and only pins the production behaviour.
    /// </summary>
    [Theory]
    [InlineData(1.22f, 100)]
    [InlineData(2.4f, 100)]
    [InlineData(3.7f, 0)]
    public void AdvanceCameraSmoothing_SteadyPursuit_GapNeverReverses_UnlikeFloatSmoothing(
        float pixelsPerTick, int minimumFloatReversals)
    {
        const int ticks = 3000;
        const int settleTicks = 1500;

        var rawY = 0f;
        var integerState = Vector3.Zero;
        var floatStateY = 0f;
        var started = false;
        var integerGaps = new List<float>();
        var floatGaps = new List<float>();

        for (var tick = 0; tick < ticks; tick++)
        {
            rawY += pixelsPerTick;
            var lookAtY = (int)MathF.Floor(rawY); // followed.PosY >> 16
            var target = AlundraCameraMath.ComputeCameraLookAtRenderPosition(804, lookAtY, 0);

            if (!started)
            {
                started = true;
                integerState = AlundraCameraMath.AdvanceCameraSmoothing(
                    integerState, needsSnap: true, target, ticksThisFrame: 0, null, null);
                floatStateY = target.Y;
            }
            else
            {
                integerState = AlundraCameraMath.AdvanceCameraSmoothing(
                    integerState, needsSnap: false, target, ticksThisFrame: 1, null, null);

                // Rejected variant, kept here purely as the counter-proof - see this test's own doc.
                floatStateY += (target.Y - floatStateY) * (1f / 16f);
            }

            if (tick < settleTicks)
            {
                continue;
            }

            integerGaps.Add(-lookAtY - integerState.Y);
            floatGaps.Add(-lookAtY - MathF.Ceiling(floatStateY));
        }

        Assert.Equal(0, CountDirectionReversals(integerGaps));
        Assert.True(
            CountDirectionReversals(floatGaps) >= minimumFloatReversals,
            $"the rejected float smoothing should reverse at least {minimumFloatReversals} times at "
            + $"{pixelsPerTick} px/tick, making this test discriminating; "
            + $"got {CountDirectionReversals(floatGaps)}");
    }

    /// <summary>
    /// Frame-rate independence, driven by the REAL <see cref="AlundraLogicClock"/>. The camera must be a
    /// pure function of the accumulated TICK count, never of the display rate.
    ///
    /// The target keeps advancing throughout (2.4 px per tick) rather than standing still: under the
    /// integer port a fixed target is reached in finitely many steps, so a converged run would compare
    /// equal whatever the cadence and the test would prove nothing.
    ///
    /// Measured: 50 ticks of pursuit leave the camera at -61 whatever dt (50, 124 and 240 rendered frames
    /// respectively). The counter-proof advances ONE step per rendered FRAME instead, which lands on -87
    /// at dt = 1/123 and 1/240 - the regression this slice fixes. At dt = 1/50 one frame IS one tick, so
    /// the two agree there by construction; that row is deliberately not asserted as different.
    /// </summary>
    [Fact]
    public void AdvanceCameraSmoothing_DrivenByTheRealLogicClock_IsIdenticalAtEveryFrameRate()
    {
        Assert.Equal(-61f, RunPursuit(1f / 50f, stepPerFrame: false));
        Assert.Equal(-61f, RunPursuit(1f / 123f, stepPerFrame: false));
        Assert.Equal(-61f, RunPursuit(1f / 240f, stepPerFrame: false));

        // Counter-proof: stepping per rendered frame makes the result depend on the display rate.
        Assert.Equal(-87f, RunPursuit(1f / 123f, stepPerFrame: true));
        Assert.Equal(-87f, RunPursuit(1f / 240f, stepPerFrame: true));
    }

    /// <summary>Runs a followed entity moving at 2.4 px per logic tick until the real
    /// <see cref="AlundraLogicClock"/> has delivered exactly 50 ticks at the given frame time, and returns
    /// the camera's render-space Y. <paramref name="stepPerFrame"/> selects the regression shape (one
    /// catch-up step per rendered frame) instead of the production one (one per logic tick).</summary>
    private static float RunPursuit(float dt, bool stepPerFrame)
    {
        const float pixelsPerTick = 2.4f;
        const int totalTicks = 50;

        var clock = new AlundraLogicClock();
        var rawY = 0f;
        var state = Vector3.Zero;
        var started = false;
        var delivered = 0;

        while (delivered < totalTicks)
        {
            var ticksThisFrame = Math.Min(clock.TicksThisFrame(dt), totalTicks - delivered);
            clock.CloseFrame();

            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                rawY += pixelsPerTick;
                delivered++;

                var target = AlundraCameraMath.ComputeCameraLookAtRenderPosition(804, (int)MathF.Floor(rawY), 0);

                if (!started)
                {
                    started = true;
                    state = AlundraCameraMath.AdvanceCameraSmoothing(state, true, target, 0, null, null);
                }
                else if (!stepPerFrame)
                {
                    state = AlundraCameraMath.AdvanceCameraSmoothing(state, false, target, 1, null, null);
                }
            }

            if (stepPerFrame && started)
            {
                var target = AlundraCameraMath.ComputeCameraLookAtRenderPosition(804, (int)MathF.Floor(rawY), 0);
                state = AlundraCameraMath.AdvanceCameraSmoothing(state, false, target, 1, null, null);
            }
        }

        return state.Y;
    }

    /// <summary>Number of times <paramref name="values"/> changes direction (a non-zero step whose sign
    /// differs from the previous non-zero step) - the shimmer signature. A flat run, or one that only ever
    /// moves one way, returns 0.</summary>
    private static int CountDirectionReversals(IReadOnlyList<float> values)
    {
        var previousSign = 0;
        var reversals = 0;

        for (var i = 1; i < values.Count; i++)
        {
            var sign = MathF.Sign(values[i] - values[i - 1]);

            if (sign == 0)
            {
                continue;
            }

            if (previousSign != 0 && sign != previousSign)
            {
                reversals++;
            }

            previousSign = sign;
        }

        return reversals;
    }
}
