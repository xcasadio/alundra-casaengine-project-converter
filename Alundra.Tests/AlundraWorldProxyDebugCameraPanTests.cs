using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the pure math of the DEBUG-only right-stick camera pan
/// (<see cref="AlundraWorldProxy.ComputeDebugCameraPanTarget"/>) - see that method's own doc comment and
/// <see cref="AlundraWorldProxy.UpdateDebugCameraPan"/> for the tool's rationale/lifetime.
///
/// Not covered here (headless-untestable, needs a live gamepad/world): the gamepad read through
/// <c>CasaEngineGame.InputComponent.GamePadManager</c> and the "camera" entity/<c>Camera2dComponent</c>
/// lookup in <see cref="AlundraWorldProxy.UpdateDebugCameraPan"/> itself.
/// </summary>
public class AlundraWorldProxyDebugCameraPanTests
{
    [Fact]
    public void ComputeDebugCameraPanTarget_StickInsideDeadZone_LeavesTargetUnchanged()
    {
        var target = new Vector3(10f, -20f, 5f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0.1f, -0.15f, 1f);

        Assert.Equal(target, result);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_StickJustBelowDeadZone_IsStillSuppressed()
    {
        var target = new Vector3(10f, -20f, 5f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0.199f, -0.199f, 1f);

        Assert.Equal(target, result);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_StickExactlyAtDeadZone_IsNotSuppressed()
    {
        // The suppression rule is a strict Abs(stick) < deadzone, so a stick value exactly equal to the
        // deadzone constant passes through (only strictly smaller magnitudes are zeroed).
        var target = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0.2f, 0f, 1f);

        Assert.Equal(100f, result.X); // 0.2 * 500 * 1
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_StickOutsideDeadZone_MovesTargetByStickTimesSpeedTimesDt()
    {
        var target = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 1f, 0f, 0.5f);

        // Full deflection on X only, half a second: 1 * 500 * 0.5 = 250.
        Assert.Equal(250f, result.X);
        Assert.Equal(0f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_StickUp_IncreasesTargetY()
    {
        // Axis mapping: MonoGame right-stick Y is positive up, and these converted maps' world Y is also
        // "more positive = further up" - stick-up must increase Target.Y, no sign flip.
        var target = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0f, 1f, 1f);

        Assert.True(result.Y > target.Y);
        Assert.Equal(500f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_StickDown_DecreasesTargetY()
    {
        var target = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0f, -1f, 1f);

        Assert.True(result.Y < target.Y);
        Assert.Equal(-500f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_ZeroElapsedTime_LeavesTargetUnchanged()
    {
        var target = new Vector3(3f, 4f, 5f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 1f, 1f, 0f);

        Assert.Equal(target, result);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_NeverTouchesZ()
    {
        var target = new Vector3(1f, 2f, 42f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 1f, 1f, 1f);

        Assert.Equal(42f, result.Z);
    }

    [Fact]
    public void ComputeDebugCameraPanTarget_BothAxesOutsideDeadZone_MovesBoth()
    {
        var target = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanTarget(target, 0.5f, -0.5f, 1f);

        Assert.Equal(250f, result.X);
        Assert.Equal(-250f, result.Y);
    }
}
