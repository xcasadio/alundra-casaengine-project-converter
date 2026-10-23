#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, phase B, tests (w1) to (w4) and (w8)): the pillar of the map 26 (<c>Ancient Shrine-26</c>, record 29, the sprite type 430) pushed by the hero in the REAL world - the real prefabs, the real controller,
/// the production registry. The frames are numbered from 1, the first frame the direction is held. The values are the ones the DLL measured; the machine (the real code of the binary, <c>docs/plan-e20-annexe/e20-g5/disc/out/</c>) gives the same contact, the same delay of 32
/// ticks, the same speed and the same stops, and moves one tick earlier at the first step (D-E19-13, D-G5-4).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarAiWorld26Tests
{
    private const string Zone = "Ancient Shrine";
    private const string World = "Ancient Shrine-26";
    private const int PillarRecord = 29;

    private static PillarRun East(string name, int heroY = 424) => new(name, Zone, World, 300, heroY, 16);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) east: the contact, the 32 ticks of the counter, the animation 1 at the 32nd tick of contact, the first step, the stop at 350 against the wall of the column 15, the hero at 329.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_PushedEast_TheContactIsAtTheFrame2_TheCounterCountsTo31_TheAnimation1IsAtTheFrame34_TheFirstStepAt35_TheStopAt350AgainstTheWall_TheHeroAt329()
    {
        using var run = East("w1");
        var pillar = run.Record(PillarRecord);
        Assert.Equal((324.0, 424.0, 16.0), PillarRun.PixelOf(pillar));
        run.Watched = pillar;

        run.Hold(PadState.Right, 160);

        // before the contact: nothing (the hero is walking up to the pillar).
        Assert.Null(run.At(1).BlockedBy);
        Assert.Equal(0, run.At(1).PillarCounter);
        // the contact is the frame 2 and it stays to the end; the counter goes up one per frame from the frame 3 to 31 at the frame 33 (the pillar reads the contact one tick after the hero's step posed it).
        Assert.Equal(PillarRecord, run.At(2).BlockedBy);
        Assert.All(run.Samples.Skip(1), s => Assert.Equal(PillarRecord, s.BlockedBy));
        Assert.Equal(0, run.At(2).PillarCounter);
        for (var frame = 3; frame <= 33; frame++)
        {
            Assert.Equal(frame - 2, run.At(frame).PillarCounter);
            Assert.Equal(0u, run.At(frame).PillarAnimation);
            Assert.Equal(0, run.At(frame).PushSounds);
            Assert.Equal(324.0, run.At(frame).PillarX);
        }

        // the 32nd tick of contact: the animation 1, the direction 24 (the side of the hero plus half a turn), the sound; the pillar has not moved yet.
        Assert.Equal(31, run.At(34).PillarCounter);
        Assert.Equal((1u, 24u, 1), (run.At(34).PillarAnimation, run.At(34).PillarDirection, run.At(34).PushSounds));
        Assert.Equal(324.0, run.At(34).PillarX);
        // the first step is one tick later (the machine moves at the tick of the animation: D-E19-13).
        Assert.Equal(324.75, run.At(35).PillarX);

        // the stop at the wall of the column 15: x 350 from the frame 69, the hero at 329 from the frame 70, to the end; the animation stays 1 and the sound is asked at every tick.
        Assert.True(run.At(68).PillarX < 350.0);
        Assert.All(run.Samples.Skip(68), s => Assert.Equal((350.0, 424.0, 16.0), (s.PillarX, s.PillarY, s.PillarZ)));
        Assert.All(run.Samples.Skip(69), s => Assert.Equal(329.0, s.HeroX));
        Assert.All(run.Samples.Skip(33), s => Assert.Equal(1u, s.PillarAnimation));
        // the native handler is the one that ran (the positive companion of the inert pillars of the map 423).
        Assert.True(run.Arc.Runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, PillarAi.Code)) >= 160);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2) north: the stop at 406.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W2_PushedNorth_TheDirectionIs16_TheAnimation1IsThe32ndTickOfContact_ThePillarStopsAt406_TheHeroAt421()
    {
        using var run = new PillarRun("w2", Zone, World, 324, 452, 16);
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Up, 160);

        // the hero drops from the ledge it was put on, then reaches the pillar: the contact is the frame 9.
        Assert.Null(run.At(8).BlockedBy);
        Assert.Equal(PillarRecord, run.At(9).BlockedBy);
        Assert.Equal(0u, run.At(40).PillarAnimation);
        Assert.Equal(424.0, run.At(41).PillarY);
        Assert.Equal((1u, 16u, 1), (run.At(41).PillarAnimation, run.At(41).PillarDirection, run.At(41).PushSounds)); // 9 + 32
        Assert.Equal(423.5, run.At(42).PillarY);
        Assert.True(run.At(76).PillarY > 406.0);
        Assert.All(run.Samples.Skip(76), s => Assert.Equal((324.0, 406.0, 16.0), (s.PillarX, s.PillarY, s.PillarZ)));
        Assert.Equal((324.0, 421.0, 16.0), (run.At(160).HeroX, run.At(160).HeroY, run.At(160).HeroZ));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w3) the speed, the stop after the release, the new push that starts the 32 ticks again.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W3_TheSpeedIs075PixelPerTickInXAnd05InY_TheAnimationEndsTwoTicksAfterTheRelease_ANewPushStartsThe32TicksAgain()
    {
        using (var north = new PillarRun("w3n", Zone, World, 324, 452, 16))
        {
            north.Watched = north.Record(PillarRecord);
            north.Hold(PadState.Up, 80);
            for (var frame = 41; frame <= 75; frame++)
            {
                Assert.Equal(424.0 - 0.5 * (frame - 41), north.At(frame).PillarY); // 0.5 pixel per tick in Y.
            }
        }

        using var run = East("w3");
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Right, 60);
        for (var frame = 34; frame <= 60; frame++)
        {
            Assert.Equal(324.0 + 0.75 * (frame - 34), run.At(frame).PillarX); // 0.75 pixel per tick in X.
        }

        Assert.Equal(1u, run.At(60).PillarAnimation);

        // the direction is released at the frame 61: the hero stops at the frame 62 (no contact), the pillar goes on one tick (the animation is still 1 and the sound is asked), then the animation 0 at the frame 63.
        run.Release(10);
        Assert.Equal(PillarRecord, run.At(61).BlockedBy);
        Assert.Null(run.At(62).BlockedBy);
        Assert.Equal((1u, 1), (run.At(62).PillarAnimation, run.At(62).PushSounds));
        Assert.Equal(345.0, run.At(62).PillarX);
        Assert.Equal((0u, 0), (run.At(63).PillarAnimation, run.At(63).PushSounds));
        Assert.Equal(345.75, run.At(63).PillarX);
        Assert.All(run.Samples.Skip(62), s => Assert.Equal(345.75, s.PillarX)); // it never moves again by itself.
        Assert.Equal(0, run.At(63).PillarCounter);

        // a new push (the frame 71): the contact at the frame 72, the 32 ticks again - the animation 1 at the frame 104, the first step at 105.
        run.Hold(PadState.Right, 60);
        Assert.Equal(PillarRecord, run.At(72).BlockedBy);
        Assert.Equal(0u, run.At(103).PillarAnimation);
        Assert.Equal(345.75, run.At(103).PillarX);
        Assert.Equal((1u, 1), (run.At(104).PillarAnimation, run.At(104).PushSounds));
        Assert.Equal(345.75, run.At(104).PillarX);
        Assert.Equal(346.5, run.At(105).PillarX);
        Assert.Equal(350.0, run.At(130).PillarX);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w4) the vertical offset: contact and push at |dy| = 12, nothing at -14 (and the machine's own asymmetry: +14 still pushes, +16 does not).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-12)]
    [InlineData(12)]
    [InlineData(14)]
    public void W4_AHeroOffsetByUpTo12PixelsInY_PushesThePillar_ThePushIsTheSameAsOnTheAxis(int dy)
    {
        using var run = East("w4+" + dy, 424 + dy);
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Right, 90);

        Assert.Equal(PillarRecord, run.At(3).BlockedBy);
        Assert.Equal(1u, run.At(90).PillarAnimation);
        Assert.Equal((350.0, 424.0, 16.0), PillarRun.PixelOf(pillar));
        Assert.Equal(329.0, run.At(90).HeroX);
        Assert.Contains(run.Samples, s => s.PushSounds == 1);
    }

    [Theory]
    [InlineData(-14)]
    [InlineData(16)]
    public void W4_AHeroOffsetByMoreInY_NeverTouchesThePillar_ItWalksPast_NothingIsPushedAndNoSoundIsAsked(int dy)
    {
        using var run = East("w4+" + dy, 424 + dy);
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Right, 90);

        Assert.All(run.Samples, s => Assert.Null(s.BlockedBy));
        Assert.All(run.Samples, s => Assert.Equal((324.0, 424.0, 16.0, 0u, 0, 0), (s.PillarX, s.PillarY, s.PillarZ, s.PillarAnimation, s.PillarCounter, s.PushSounds)));
        Assert.True(run.At(90).HeroX > 340.0); // the hero went past the pillar's column.
        Assert.Empty(run.Stops); // the animation was never 1: the stop of the sound is not asked.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w8) the sound: PlaySfx(25) at every tick of push, the stop of the sound once, at the release (the named stub of g5c).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W8_TheSound25IsAskedAtEveryTickOfPush_AndTheStopOfTheSoundOnceAtTheRelease_NeverBefore()
    {
        using var run = East("w8");
        run.Watched = run.Record(PillarRecord);

        run.Hold(PadState.Right, 100);

        Assert.Empty(run.Stops); // a continuous push, even against the wall: the stop is not asked.
        Assert.All(run.Samples.Take(33), s => Assert.Equal(0, s.PushSounds));
        Assert.All(run.Samples.Skip(33), s => Assert.Equal(1, s.PushSounds)); // the frames 34 to 100: one per tick.
        Assert.Equal(25, PillarRun.PushSfx);

        run.Release(10);

        Assert.Equal(new[] { 25 }, run.Stops); // the stop is asked once, with the sound 25 ...
        Assert.Equal(0, run.At(101).StopCalls);
        Assert.Equal(0, run.At(102).StopCalls); // ... at the tick the animation goes from 1 to 0 (the frame 103), never before.
        Assert.Equal(1, run.At(103).StopCalls);
        Assert.Equal((0u, 0), (run.At(103).PillarAnimation, run.At(103).PushSounds));
        Assert.All(run.Samples.Skip(102), s => Assert.Equal(1, s.StopCalls));
    }
}
