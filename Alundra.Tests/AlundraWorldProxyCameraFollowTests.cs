using System;
using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the pure math of E5.a's scripted camera follow (docs/plan-e5-camera.md) -
/// <see cref="AlundraWorldProxy.ResolveCameraLookAt"/>, <see cref="AlundraWorldProxy.ComputeCameraLookAtRenderPosition"/>,
/// <see cref="AlundraWorldProxy.ComputeCameraSmoothingFactor"/>, <see cref="AlundraWorldProxy.ApplyCameraSmoothing"/>,
/// <see cref="AlundraWorldProxy.ClampCameraTargetToMap"/> and <see cref="AlundraWorldProxy.ComputeCameraZoom"/>.
///
/// Not covered here (headless-untestable, needs a live World/Camera2dComponent): the wiring itself
/// (<see cref="AlundraWorldProxy.UpdateCameraFollow"/>/<see cref="AlundraWorldProxy.ResolveDebugCameraOnce"/>) -
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
        var result = AlundraWorldProxy.ResolveCameraLookAt(
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
        var result = AlundraWorldProxy.ResolveCameraLookAt(
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
        var result = AlundraWorldProxy.ComputeCameraLookAtRenderPosition(804, 952, 0);

        Assert.Equal(new Vector3(804f, -936f, 0f), result);
    }

    [Fact]
    public void ComputeCameraLookAtRenderPosition_ZElevation_RaisesRenderY()
    {
        // Y - Z shrinks as Z (elevation) rises, so render Y (= -(Y-Z) + 16) increases (moves up-screen) -
        // an elevated entity's look-at sits higher on screen than a grounded one at the same X/Y.
        var grounded = AlundraWorldProxy.ComputeCameraLookAtRenderPosition(100, 200, 0);
        var elevated = AlundraWorldProxy.ComputeCameraLookAtRenderPosition(100, 200, 32);

        Assert.True(elevated.Y > grounded.Y);
        Assert.Equal(grounded.Y + 32f, elevated.Y);
    }

    // -----------------------------------------------------------------------------------------
    // ComputeCameraSmoothingFactor / ApplyCameraSmoothing - time-independent catch-up (decision E5-2)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputeCameraSmoothingFactor_OneSecond_MatchesFiftyTicksAtOneSixteenthCatchUp()
    {
        // 50 ticks of the original's own (target-cur)>>4 (i.e. *1/16) catch-up, compounded, leaves
        // (15/16)^50 of the original gap remaining - so the continuous formula over exactly 1 real second
        // must close 1 - (15/16)^50 of the gap, bit-for-bit the same value.
        var expected = 1f - MathF.Pow(15f / 16f, 50f);

        var factor = AlundraWorldProxy.ComputeCameraSmoothingFactor(1f);

        Assert.Equal(expected, factor, 5);
    }

    /// <summary>
    /// Plan acceptance: "identical at dt = 1/50, 1/123 and 1/240" - starting from a known 160px gap and
    /// applying the smoothing formula for exactly one real second (compounding across however many
    /// substeps that dt implies), the resulting position must be the SAME regardless of dt - proving the
    /// catch-up rate is truly time-independent, not a hidden per-frame-count dependency (the exact bug
    /// class E4 already fixed for movement).
    /// </summary>
    [Theory]
    [InlineData(1f / 50f)]
    [InlineData(1f / 123f)]
    [InlineData(1f / 240f)]
    public void ApplyCameraSmoothing_OneSecondOfSubsteps_IdenticalRegardlessOfFrameRate(float dt)
    {
        var current = new Vector3(0f, 0f, 0f);
        var target = new Vector3(160f, 0f, 0f);
        var steps = (int)MathF.Round(1f / dt);

        for (var i = 0; i < steps; i++)
        {
            var factor = AlundraWorldProxy.ComputeCameraSmoothingFactor(dt);
            current = AlundraWorldProxy.ApplyCameraSmoothing(current, target, factor);
        }

        // (15/16)^50 of the 160px gap should remain, whatever dt/step count got there.
        var expectedRemainingGap = 160f * MathF.Pow(15f / 16f, 50f);
        var expectedX = 160f - expectedRemainingGap;

        Assert.Equal(expectedX, current.X, 2);
    }

    [Fact]
    public void ApplyCameraSmoothing_ZeroFactor_LeavesCurrentUnchanged()
    {
        var current = new Vector3(5f, 6f, 7f);
        var target = new Vector3(100f, 100f, 100f);

        var result = AlundraWorldProxy.ApplyCameraSmoothing(current, target, 0f);

        Assert.Equal(current, result);
    }

    [Fact]
    public void ApplyCameraSmoothing_FactorOne_SnapsStraightToTarget()
    {
        var current = new Vector3(5f, 6f, 7f);
        var target = new Vector3(100f, 100f, 100f);

        var result = AlundraWorldProxy.ApplyCameraSmoothing(current, target, 1f);

        Assert.Equal(target, result);
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

        Assert.Equal(1087f, AlundraWorldProxy.ClampCameraTargetToMap(new Vector3(99999f, -500f, 0f), mapWidthPx, mapHeightPx).X);
        Assert.Equal(160f, AlundraWorldProxy.ClampCameraTargetToMap(new Vector3(-99999f, -500f, 0f), mapWidthPx, mapHeightPx).X);
        Assert.Equal(-120f, AlundraWorldProxy.ClampCameraTargetToMap(new Vector3(500f, 99999f, 0f), mapWidthPx, mapHeightPx).Y);
        Assert.Equal(-839f, AlundraWorldProxy.ClampCameraTargetToMap(new Vector3(500f, -99999f, 0f), mapWidthPx, mapHeightPx).Y);

        // 0x39f/0x2cf cross-check, converted back to the original's own top-left scroll coordinates
        // (scrollX = TargetX - 160, scrollY = -TargetY - 120). Target.X's max and Target.Y's min are the
        // SAME corner of the map (bottom-right in the original's down-positive screen space), so the
        // probe pushes X positive and Y negative together.
        var maxTarget = AlundraWorldProxy.ClampCameraTargetToMap(new Vector3(99999f, -99999f, 0f), mapWidthPx, mapHeightPx);
        Assert.Equal(0x39f, (int)(maxTarget.X - 160f));
        Assert.Equal(0x2cf, (int)(-maxTarget.Y - 120f));
    }

    [Fact]
    public void ClampCameraTargetToMap_InsideBounds_LeavesTargetUnchanged()
    {
        var target = new Vector3(600f, -400f, 0f);

        var result = AlundraWorldProxy.ClampCameraTargetToMap(target, 1248, 960);

        Assert.Equal(target, result);
    }

    [Fact]
    public void ClampCameraTargetToMap_NearEdge_KeepsViewInsideTheMap()
    {
        // A look-at near the map's bottom-right corner (e.g. block 18's own area) must not push the
        // camera's visible 320x240 area outside the map.
        var target = new Vector3(1240f, -955f, 0f);

        var result = AlundraWorldProxy.ClampCameraTargetToMap(target, 1248, 960);

        Assert.InRange(result.X, 160f, 1248f - 160f);
        Assert.InRange(result.Y, -(960f - 120f), -120f);
    }

    // -----------------------------------------------------------------------------------------
    // ComputeCameraZoom - runtime framing (decision E5-1)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputeCameraZoom_ExactMultipleOf240_IsIntegerZoom()
    {
        Assert.Equal(4f, AlundraWorldProxy.ComputeCameraZoom(960));
    }

    [Fact]
    public void ComputeCameraZoom_IsComputedFromViewport_NotHardcoded()
    {
        // Different viewport heights must yield different zooms - proves the value is derived, not a
        // constant 4.
        Assert.Equal(2f, AlundraWorldProxy.ComputeCameraZoom(480));
        Assert.Equal(1f, AlundraWorldProxy.ComputeCameraZoom(240));
    }
}
