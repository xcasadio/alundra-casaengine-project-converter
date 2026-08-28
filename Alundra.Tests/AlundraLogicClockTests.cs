using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Bug fix (user-reported runtime pacing bug - see <see cref="AlundraLogicClock"/>'s own class doc for the
/// full diagnosis/log evidence): unit coverage for the shared 50 Hz logic clock in isolation, before any
/// integration with <see cref="Alundra.Scripts.AlundraEntityScriptProxy"/>/<see cref="Alundra.Scripts.AlundraWorldProxy"/>
/// (see <see cref="AlundraWorldProxyLogicClockTests"/> for that half).
/// </summary>
public class AlundraLogicClockTests
{
    private const float FixedTickSeconds = AlundraScriptedMotion.FixedTickSeconds; // 1/50

    [Fact]
    public void TicksThisFrame_DtExactlyOneTick_YieldsExactlyOneTickPerFrame_Indefinitely_NoDrift()
    {
        var clock = new AlundraLogicClock();

        for (var frame = 0; frame < 1000; frame++)
        {
            var ticks = clock.TicksThisFrame(FixedTickSeconds);
            Assert.Equal(1, ticks);
            clock.CloseFrame();
        }
    }

    /// <summary>
    /// Investigated 2026-08-26 after the main session raised a false alarm ("the game runs ~0.4% slow at
    /// 123 fps"). It does not. The clock is one tick BEHIND a perfect 50 Hz reference at this frame rate,
    /// and that deficit is a bounded one-off PHASE OFFSET of 20 ms - it never grows, at any duration.
    /// Measured on the production float32 arithmetic: -1 tick at 1 s, at 10 s, at 60 s, at 300 s and at
    /// 900 s, the rate converging on 49.9989 ticks/s over 900 s.
    ///
    /// <para>The cause is not this class' accumulator but the frame time it is handed: float32(1/123) is a
    /// hair BELOW the true 1/123, so 123 of them sum to 0.999999962747097 while the 50th tick's threshold
    /// sits at 0.9999999776482582. No accumulator of any precision - float, double, or exact rational -
    /// can emit 50 ticks in 123 such frames. Widening the parameter is impossible anyway: elapsedTime is
    /// already quantised by <c>GameplayProxy.Update(float)</c> in the engine submodule, before any Alundra
    /// code runs. Decision of 2026-08-26: leave the arithmetic alone (a double accumulator would fix 72,
    /// 144, 280 and 400 Hz by one tick each, but not 123 Hz, and it would make this clock diverge from the
    /// engine's own float <c>CharacterMotionSystem._fixedStepAccumulator</c>, which cannot be touched).</para>
    ///
    /// <para>This test replaces one that asserted <c>InRange(total, 49, 51)</c> - a tolerance written into
    /// its own name, which is precisely what let the behaviour go undescribed while the suite stayed
    /// green. Exact counts now, so the day any of this changes, a test says so.</para>
    /// </summary>
    [Theory]
    [InlineData(123, 49)]    // 1 s
    [InlineData(1230, 499)]  // 10 s
    [InlineData(7380, 2999)] // 60 s
    public void TicksThisFrame_Dt1Over123_IsExactlyOneTickBehind_WhateverTheDuration(int frames, int expectedTotal)
    {
        var clock = new AlundraLogicClock();
        const float dt = 1f / 123f;

        var total = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            var ticks = clock.TicksThisFrame(dt);
            Assert.True(ticks is 0 or 1, $"frame {frame}: expected 0 or 1 tick at ~123 Hz, got {ticks}.");
            total += ticks;
            clock.CloseFrame();
        }

        Assert.Equal(expectedTotal, total);

        // The point of the three rows: the deficit against a perfect 50 Hz clock is ONE tick at every
        // duration. A drift would show up here as a deficit growing with `frames`.
        var perfectClockTotal = (int)(frames / 123.0 * 50.0);
        Assert.Equal(1, perfectClockTotal - total);
    }

    [Fact]
    public void TicksThisFrame_LongStallFrame_CapsAtMaxTicksPerFrame()
    {
        var clock = new AlundraLogicClock();

        var ticks = clock.TicksThisFrame(1f); // a 1-second stall
        clock.CloseFrame();

        Assert.Equal(AlundraScriptedMotion.MaxTicksPerFrame, ticks);
    }

    [Fact]
    public void TicksThisFrame_StallFrame_DropsLeftoverPastTheCap_NextFrameStartsFresh()
    {
        var clock = new AlundraLogicClock();

        clock.TicksThisFrame(1f); // stall: capped at MaxTicksPerFrame, leftover dropped (accumulator reset)
        clock.CloseFrame();

        // A stall's own leftover time beyond the cap is dropped (never carried), so the very next frame at
        // dt = FixedTickSeconds sees exactly its own one tick, not an extra catch-up tick from the stall.
        var nextTicks = clock.TicksThisFrame(FixedTickSeconds);
        clock.CloseFrame();

        Assert.Equal(1, nextTicks);
    }

    [Fact]
    public void TicksThisFrame_LeftoverCarries_TwoSubThresholdFrames_ZeroThenOne()
    {
        var clock = new AlundraLogicClock();
        const float dt = 1f / 75f; // < FixedTickSeconds (1/50) on its own, two of them exceed it

        var first = clock.TicksThisFrame(dt);
        clock.CloseFrame();
        var second = clock.TicksThisFrame(dt);
        clock.CloseFrame();

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void TicksThisFrame_AllCallersInOneFrame_ObserveTheSameCount()
    {
        var clock = new AlundraLogicClock();

        var first = clock.TicksThisFrame(1f / 123f);
        var second = clock.TicksThisFrame(1f / 123f); // same frame - memo, elapsedTime ignored
        var third = clock.TicksThisFrame(9999f); // even a wildly different value - still memoized

        Assert.Equal(first, second);
        Assert.Equal(first, third);

        clock.CloseFrame();

        // After closing, the very next call is free to compute a fresh value again.
        var afterClose = clock.TicksThisFrame(FixedTickSeconds);
        Assert.Equal(1, afterClose);
    }

    [Fact]
    public void TicksThisFrame_WithoutClosingFrame_KeepsReturningTheSameMemoizedValue()
    {
        var clock = new AlundraLogicClock();

        var first = clock.TicksThisFrame(FixedTickSeconds);
        var second = clock.TicksThisFrame(FixedTickSeconds); // no CloseFrame in between

        Assert.Equal(first, second);
    }
}
