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

    [Fact]
    public void TicksThisFrame_Dt1Over123_Over123Frames_TotalIs50TicksPlusOrMinus1_NoFrameExceedsOneTick()
    {
        var clock = new AlundraLogicClock();
        const float dt = 1f / 123f;

        var total = 0;
        for (var frame = 0; frame < 123; frame++)
        {
            var ticks = clock.TicksThisFrame(dt);
            Assert.True(ticks is 0 or 1, $"frame {frame}: expected 0 or 1 tick at ~123 Hz, got {ticks}.");
            total += ticks;
            clock.CloseFrame();
        }

        Assert.InRange(total, 49, 51);
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
