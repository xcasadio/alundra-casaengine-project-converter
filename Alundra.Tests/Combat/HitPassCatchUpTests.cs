#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.c1a C5 (docs/plan-e20-heros.md section 1.5), the rendered frames that carry several logic ticks. The DLL validates an animation switch once, at the end of the frame (D-E19-13), while the binary switches, loads the box and
/// the attack and hits at the tick that sees it: in the rest of a catch-up frame the sprite still holds the old animation. The hit pass reads a cursor of its own (the animation, the direction and the ticks since the switch,
/// <see cref="EntityScriptProxy.NoteSwitchPendingAtTick"/>), so a hit lands at the tick of the oracle whatever the grouping of the ticks into frames. Each test replays an oracle trace with another grouping and compares every
/// tick, every column, as <see cref="HitPassTests"/> does. The pad of a frame is the oracle's pad of its first tick.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HitPassCatchUpTests
{
    private const uint Square = 0x80;

    private static IReadOnlyList<HitTickSample> Replay(string name, int[] frames, out HitOracleTrace trace, Action<HitArena>? configure = null)
    {
        trace = HitOracleTrace.Load(name);
        using var arena = HitPassTests.Arena(trace, name + "-catchup");
        configure?.Invoke(arena);
        var samples = arena.Run(trace, frames).ToList();
        HitPassTests.AssertEqualToOracle(HitPassTests.Compare(trace, samples));
        return samples;
    }

    // Calage 1: the press is the first tick of a frame, every frame has two ticks. Calage 2: a frame of one tick (the press), then frames of two: the contact tick (5 of H1) and the end of the swing fall on
    // the second tick of a frame in one of them and on the first in the other.

    [Fact]
    public void H1_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick()
    {
        var samples = Replay("H1", new[] { 2 }, out _);
        Assert.Equal(25, samples[4].Targets[0].CooldownTicks);
        Assert.Equal(0, samples[4].Targets[0].Touching);
    }

    [Fact]
    public void H1_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick()
    {
        var samples = Replay("H1", new[] { 1, 2 }, out _);
        Assert.Equal(25, samples[4].Targets[0].CooldownTicks);
    }

    [Fact]
    public void H1near_WithTwoTicksPerFrame_ThePressOnTheFirstTickOfAFrame_IsTheOracleTickByTick()
    {
        var samples = Replay("H1-near", new[] { 2 }, out _);
        Assert.Equal(25, samples[0].Targets[0].CooldownTicks); // the hit of the switch tick, the first tick of the first frame.
        Assert.Equal(0x10u, samples[0].Latch);
    }

    [Fact]
    public void H1near_WithTwoTicksPerFrame_AfterAFrameOfOneTick_IsTheOracleTickByTick()
    {
        var samples = Replay("H1-near", new[] { 1, 2 }, out _);
        Assert.Equal(25, samples[0].Targets[0].CooldownTicks);
    }

    [Fact]
    public void H1_WithThreeAndFourTicksPerFrame_IsTheOracleTickByTick()
    {
        Replay("H1", new[] { 3 }, out _);
        Replay("H1", new[] { 4 }, out _);
        Replay("H1-near", new[] { 3, 1, 4, 2 }, out _);
    }

    [Fact]
    public void TheHitWrittenAtTickN_IsReadByTheProgramsOfTickNPlus1_AndClearedByTheCounters_InTheSameFrameOfTwoTicks()
    {
        // Frames of two ticks from the press: {1,2} {3,4} {5,6}: the hit of tick 5 and the program D of tick 6 are in ONE frame.
        var samples = Replay("H1", new[] { 2 }, out _);
        var hit = samples[4];
        var next = samples[5];
        Assert.Equal(0, hit.Targets[0].Touching); // written at tick 5 (the toucher is the hero)...
        Assert.Equal(1, hit.Hero.HitCounter);
        Assert.DoesNotContain(hit.Handlers, h => h.Letter == 3);
        Assert.Contains((1, 3), next.Handlers); // ...chosen as program D at tick 6 (the events pass of that tick reads it)...
        Assert.Equal(-1, next.Targets[0].Touching); // ...and cleared by the counters pass of that same tick,
        Assert.Equal(0, next.Hero.HitCounter);
        Assert.Equal(24, next.Targets[0].CooldownTicks); // which also counts the cooldown down.
    }

    [Fact]
    public void TheBoxCursor_FollowsTheSpritesLogicalClock_AfterEveryFrame_WhateverTheNumberOfTicks()
    {
        var trace = HitOracleTrace.Load("H1");
        using var arena = HitPassTests.Arena(trace, "cursor");
        var frames = new[] { 3, 1, 2, 4, 2, 3, 1, 1, 4, 2, 3, 2, 4, 1 };
        var checkedFrames = 0;
        arena.Arc.OnFrame = () =>
        {
            foreach (var entity in new[] { arena.Hero }.Concat(arena.Targets))
            {
                Assert.False(entity.BoxSwitchLoaded, "the end of the frame validated the switch the clock loaded");
                Assert.Equal(entity.CurrentAnimationId, entity.BoxAnimationId);
                var sprite = entity.LogicalClockSprite!;
                var cursor = entity.CursorLogicalTickForTests();
                Assert.NotNull(cursor);
                Assert.Equal(sprite.LogicalTick, cursor!.Value);
            }

            checkedFrames++;
        };

        arena.Begin();
        arena.Frame(Square, frames[0]);
        foreach (var size in frames.Skip(1))
        {
            arena.Frame(0, size);
        }

        Assert.Equal(frames.Length, checkedFrames);
        Assert.Equal(frames.Sum(), arena.Samples.Count);
    }

    // -----------------------------------------------------------------------------------------
    // The byte 0x40 of the animation, at the very tick of the first overlap.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ATargetThatEntersAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsNotHit()
    {
        // H1: the box of the swing first overlaps the body of the Beannoid at tick 5. The Beannoid is asked for the animation 6 (byte 0x40) at the head of tick 5: the clock of that tick loads the byte (0x80038B68), the hit
        // pass (0x80039444) reads it.
        var trace = HitOracleTrace.Load("H1");
        using var arena = HitPassTests.Arena(trace, "byte40-enter");
        arena.BeforeTick = tick =>
        {
            if (tick == 5)
            {
                arena.Targets[0].TargetAnimationId = 6;
            }
        };

        var samples = arena.Run(trace, 1);
        Assert.Equal(0, samples[3].Targets[0].AnimByte);
        Assert.Equal(0x40, samples[4].Targets[0].AnimByte);
        Assert.All(samples, s => Assert.Equal(0, s.Targets[0].CooldownTicks));
        Assert.All(samples, s => Assert.Equal(0, s.Hero.HitCounter));

        // The control, the same run without the change: the hit of tick 5.
        using var control = HitPassTests.Arena(trace, "byte40-control");
        Assert.Equal(25, control.Run(trace, 1)[4].Targets[0].CooldownTicks);
    }

    [Fact]
    public void ATargetThatLeavesAnAnimationWithTheByte0x40_AtTheTickOfTheFirstOverlap_IsHit()
    {
        var trace = HitOracleTrace.Load("H1");
        using var arena = HitPassTests.Arena(trace, "byte40-leave");
        arena.Targets[0].TargetAnimationId = 6; // in the animation 6 from tick 1 (as H4b)...
        arena.BeforeTick = tick =>
        {
            if (tick == 5)
            {
                arena.Targets[0].TargetAnimationId = 0; // ...and out of it at the head of tick 5.
            }
        };

        var samples = arena.Run(trace, 1);
        Assert.Equal(0x40, samples[3].Targets[0].AnimByte);
        Assert.Equal(0, samples[4].Targets[0].AnimByte);
        Assert.Equal(0, samples[3].Targets[0].CooldownTicks);
        Assert.Equal(25, samples[4].Targets[0].CooldownTicks);
        Assert.Equal(0, samples[4].Targets[0].Touching);
        Assert.Equal(1, samples[4].Hero.HitCounter);
    }

    [Fact]
    public void ASwitchAskedAfterTheClockOfATick_IsTheSwitchTickOfTheNextOne_TheByteIsLoadedThereBeforeTheHitPass()
    {
        // The switch is written at the end of tick 4 (after its clock): the end of the frame validates it with no tick spent on it (the tick is owed), and the clock of tick 5 - the first tick of the first overlap - is its
        // switch tick, as the binary's UpdateAnimation of tick 5 would see it: the byte 0x40 is there for the hit pass of tick 5.
        var trace = HitOracleTrace.Load("H1");
        using var arena = HitPassTests.Arena(trace, "byte40-owed-enter");
        arena.AfterTick = tick =>
        {
            if (tick == 4)
            {
                arena.Targets[0].TargetAnimationId = 6;
            }
        };

        var samples = arena.Run(trace, 1);
        Assert.Equal(0, samples[3].Targets[0].AnimByte);
        Assert.Equal(0x40, samples[4].Targets[0].AnimByte);
        Assert.All(samples, s => Assert.Equal(0, s.Targets[0].CooldownTicks));

        // The way out: in the animation 6 from tick 1, back to 0 after the clock of tick 4: hit at tick 5.
        using var leave = HitPassTests.Arena(trace, "byte40-owed-leave");
        leave.Targets[0].TargetAnimationId = 6;
        leave.AfterTick = tick =>
        {
            if (tick == 4)
            {
                leave.Targets[0].TargetAnimationId = 0;
            }
        };

        var left = leave.Run(trace, 1);
        Assert.Equal(0x40, left[3].Targets[0].AnimByte);
        Assert.Equal(0, left[4].Targets[0].AnimByte);
        Assert.Equal(25, left[4].Targets[0].CooldownTicks);
    }

    [Fact]
    public void TheSameByte0x40Test_InAFrameOfTwoTicks_StillSeesTheSwitchOfTheTickItself()
    {
        // Frames {1,2} {3,4} {5,6}: the switch is asked at the head of tick 5, the FIRST tick of its frame; and in a variant at the head of tick 6, the second.
        foreach (var switchTick in new[] { 5, 6 })
        {
            var trace = HitOracleTrace.Load("H1");
            using var arena = HitPassTests.Arena(trace, "byte40-two-" + switchTick);
            arena.BeforeTick = tick =>
            {
                if (tick == switchTick)
                {
                    arena.Targets[0].TargetAnimationId = 6;
                }
            };

            var samples = arena.Run(trace, 2);
            if (switchTick == 5)
            {
                Assert.All(samples, s => Assert.Equal(0, s.Targets[0].CooldownTicks)); // the byte is loaded at tick 5, before its hit pass.
            }
            else
            {
                Assert.Equal(25, samples[4].Targets[0].CooldownTicks); // hit at tick 5, the target leaves the reach of the cooldown only later: the switch of tick 6 comes after the hit.
                Assert.Equal(0x40, samples[5].Targets[0].AnimByte);
            }
        }
    }
}
