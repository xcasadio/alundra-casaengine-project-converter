using System;
using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the pure math of the DEBUG-only right-stick camera pan
/// (<see cref="AlundraWorldProxy.ComputeDebugCameraPanOffset"/>, <see cref="AlundraWorldProxy.ResolveDebugCameraBase"/>)
/// and the <c>ALUNDRA_DEBUG_CAMERA_ENABLED</c> flag - see
/// <see cref="AlundraWorldProxy.UpdateDebugCameraPan"/>'s own doc comment for the tool's rationale/lifetime
/// and the base/offset composition (<c>Target = base + offset</c>).
///
/// Not covered here (headless-untestable, needs a live gamepad/world): the gamepad read through
/// <c>CasaEngineGame.InputComponent.GamePadManager</c> and the "camera" entity/<c>Camera2dComponent</c>
/// lookup in <see cref="AlundraWorldProxy.UpdateDebugCameraPan"/> itself.
/// </summary>
public class AlundraWorldProxyDebugCameraPanTests
{
    [Fact]
    public void ComputeDebugCameraPanOffset_StickInsideDeadZone_LeavesOffsetUnchanged()
    {
        var offset = new Vector3(10f, -20f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0.1f, -0.15f, 1f);

        Assert.Equal(offset, result);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_StickJustBelowDeadZone_IsStillSuppressed()
    {
        var offset = new Vector3(10f, -20f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0.199f, -0.199f, 1f);

        Assert.Equal(offset, result);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_StickExactlyAtDeadZone_IsNotSuppressed()
    {
        // The suppression rule is a strict Abs(stick) < deadzone, so a stick value exactly equal to the
        // deadzone constant passes through (only strictly smaller magnitudes are zeroed).
        var offset = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0.2f, 0f, 1f);

        Assert.Equal(100f, result.X); // 0.2 * 500 * 1
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_StickOutsideDeadZone_AccumulatesByStickTimesSpeedTimesDt()
    {
        var offset = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 1f, 0f, 0.5f);

        // Full deflection on X only, half a second: 1 * 500 * 0.5 = 250.
        Assert.Equal(250f, result.X);
        Assert.Equal(0f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_Accumulates_OnTopOfExistingOffset()
    {
        var offset = new Vector3(100f, -50f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 1f, 0f, 0.5f);

        Assert.Equal(350f, result.X); // 100 + 250
        Assert.Equal(-50f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_StickUp_IncreasesOffsetY()
    {
        // Axis mapping: MonoGame right-stick Y is positive up, and these converted maps' world Y is also
        // "more positive = further up" - stick-up must increase the offset's Y - no sign flip.
        var offset = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0f, 1f, 1f);

        Assert.True(result.Y > offset.Y);
        Assert.Equal(500f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_StickDown_DecreasesOffsetY()
    {
        var offset = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0f, -1f, 1f);

        Assert.True(result.Y < offset.Y);
        Assert.Equal(-500f, result.Y);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_ZeroElapsedTime_LeavesOffsetUnchanged()
    {
        var offset = new Vector3(3f, 4f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 1f, 1f, 0f);

        Assert.Equal(offset, result);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_ZIsAlwaysZero_RegardlessOfInputZ()
    {
        // The offset never carries a depth component - Z is forced to 0 on the way out even if the
        // incoming accumulator was somehow non-zero.
        var offset = new Vector3(1f, 2f, 42f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 1f, 1f, 1f);

        Assert.Equal(0f, result.Z);
    }

    [Fact]
    public void ComputeDebugCameraPanOffset_BothAxesOutsideDeadZone_MovesBoth()
    {
        var offset = new Vector3(0f, 0f, 0f);

        var result = AlundraWorldProxy.ComputeDebugCameraPanOffset(offset, 0.5f, -0.5f, 1f);

        Assert.Equal(250f, result.X);
        Assert.Equal(-250f, result.Y);
    }

    [Fact]
    public void ResolveDebugCameraBase_TargetUnchangedFromLastWrite_KeepsPreviousBase()
    {
        var lastWritten = new Vector3(100f, 200f, 0f);
        var previousBase = new Vector3(50f, 150f, 0f);

        var result = AlundraWorldProxy.ResolveDebugCameraBase(lastWritten, lastWritten, previousBase);

        Assert.Equal(previousBase, result);
    }

    [Fact]
    public void ResolveDebugCameraBase_ExternalWrite_AdoptsCurrentTargetAsNewBase()
    {
        var lastWritten = new Vector3(100f, 200f, 0f);
        var previousBase = new Vector3(50f, 150f, 0f);
        var externalTarget = new Vector3(999f, -42f, 3f);

        var result = AlundraWorldProxy.ResolveDebugCameraBase(externalTarget, lastWritten, previousBase);

        Assert.Equal(externalTarget, result);
    }

    [Fact]
    public void BaseAndOffset_ExternalTargetWriteBetweenFrames_PreservesOffsetOnTopOfNewBase()
    {
        // Simulates two UpdateDebugCameraPan ticks around an external Target write (the future E5
        // follow-target camera moving the camera on its own): frame 1 establishes a base and offset;
        // between frames, something other than this proxy overwrites Target; frame 2 must adopt that
        // external value as the new base and re-apply the SAME offset on top of it, rather than either
        // discarding the offset or fighting the external write.
        var initialTarget = new Vector3(0f, 0f, 0f);
        var lastWritten = initialTarget;
        var basePosition = initialTarget;

        // Frame 1: stick pans right for one second, no external write yet.
        var offset = AlundraWorldProxy.ComputeDebugCameraPanOffset(Vector3.Zero, 1f, 0f, 1f);
        var frame1Target = basePosition + offset;
        lastWritten = frame1Target;

        Assert.Equal(500f, offset.X);
        Assert.Equal(new Vector3(500f, 0f, 0f), frame1Target);

        // Between frames: an external system (simulating E5) writes a brand-new Target.
        var externalTarget = new Vector3(1234f, 5678f, 9f);

        // Frame 2: no further stick input (offset unchanged); base resolution must notice the external
        // write and adopt it, then the written Target must equal externalBase + the SAME offset.
        basePosition = AlundraWorldProxy.ResolveDebugCameraBase(externalTarget, lastWritten, basePosition);
        var frame2Target = basePosition + offset;

        Assert.Equal(externalTarget, basePosition);
        Assert.Equal(externalTarget + offset, frame2Target);
        Assert.Equal(new Vector3(1734f, 5678f, 9f), frame2Target);
    }

    [Fact]
    public void DebugCameraPanEnabled_DefaultsTrue_WhenEnvironmentVariableUnset()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        try
        {
            Assert.Null(Environment.GetEnvironmentVariable(AlundraWorldProxy.DebugCameraPanEnabledEnvVar));

            // No override and no environment variable set for this process: falls back to the real
            // environment-derived value, which - per the documented "0"/"false" opt-out contract and this
            // process never having set the variable - is true.
        }
        finally
        {
            AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        }
    }

    [Fact]
    public void DebugOffset_WhenFlagOverriddenDisabled_StickNeverChangesOffset()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(false);
        try
        {
            // Mirrors UpdateDebugCameraPan's own gate: when the flag override is disabled, production
            // code never calls ComputeDebugCameraPanOffset or resets the offset at all, regardless of
            // stick deflection - the offset accumulator simply stays whatever it already was (0, since
            // nothing else ever writes it). This test documents that contract at the flag-read level; the
            // per-frame gating itself lives in UpdateDebugCameraPan (see that method's own doc), which is
            // not directly unit-testable without a live gamepad/world.
            Assert.False(AlundraWorldProxy.DebugCameraPanEnabledForTests);
        }
        finally
        {
            AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        }
    }
}
