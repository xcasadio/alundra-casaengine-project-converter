#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5c T1 (docs/plan-e20-heros.md section 1.24.6, D-G5C-1, D-G5C-5), phase A: the end of C76 (<c>0x8007C024-0x8007C0D4</c>) asks the sound player for the stop of the sound 25 through <see cref="Alundra.Audio.ISoundPlayer.StopSfx"/> - the call the
/// binary makes (<c>StopSoundEffect</c> <c>0x80049634</c>) where the decompilation has a second play. The tests read the STOP the player receives (<see cref="RecordingSoundPlayer.StopRequests"/>).
/// They were red while the pillar called the named stub of g5a (since removed). Each "no stop" assertion sits in a test that also holds the stop that does happen.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarAiStopSoundTests
{
    private const string Zone = "Ancient Shrine";
    private const string World = "Ancient Shrine-26";
    private const int PillarRecord = 29;

    private static (EntityScriptProxy Pillar, EntityScriptProxy Hero, PickupHost Host, RecordingSoundPlayer Sound) Bench(bool contact)
    {
        var pillar = new EntityScriptProxy { PosX = 100 << 16, PosY = 100 << 16, ModdedPosX = 100 << 16, Width = 24 << 16 };
        var hero = new EntityScriptProxy { PosX = 80 << 16, PosY = 100 << 16, ModdedPosX = 80 << 16, Width = 24 << 16 };
        hero.XCollisionEntity = contact ? pillar : null;
        var sound = new RecordingSoundPlayer();
        return (pillar, hero, new PickupHost { Hero = hero, Sound = sound }, sound);
    }

    [Fact]
    public void Tick_TheStopOfThe25IsAskedOnceWhenTheContactEndsAfterThePush_NeverWhilePushing_NeverWhenNothingWasPushed()
    {
        var (pillar, hero, host, sound) = Bench(contact: true);

        for (var tick = 0; tick < PillarAi.PushTicks; tick++)
        {
            PillarAi.Tick(pillar, host); // the counter goes up to 31: silent
        }

        PillarAi.Tick(pillar, host); // the 32nd tick of contact: the animation 1 and the first play
        PillarAi.Tick(pillar, host); // pushing: the play again
        Assert.Equal(PillarAi.PushAnimationId, pillar.TargetAnimationId);
        Assert.Equal(new[] { 25, 25 }, sound.Requests);
        Assert.Empty(sound.StopRequests); // while the push goes on there is no stop

        hero.XCollisionEntity = null;
        PillarAi.Tick(pillar, host); // the contact ends: the animation was 1, so the stop of the sound is asked

        Assert.Equal(new[] { 25 }, sound.StopRequests); // the positive: the stop, with the id 25, once
        Assert.Equal(new[] { 25, 25 }, sound.Requests); // and it is not a third play (the defect of the decompilation)
        Assert.Equal(0u, pillar.TargetAnimationId);

        PillarAi.Tick(pillar, host); // the next tick without contact: the animation is already 0, no second stop
        Assert.Equal(new[] { 25 }, sound.StopRequests);
    }

    [Fact]
    public void Tick_AHeroThatNeverTouchedThePillar_AsksNoStop_WhereOneThatPushedItDoes()
    {
        var (idlePillar, _, idleHost, idleSound) = Bench(contact: false);
        for (var tick = 0; tick < 5; tick++)
        {
            PillarAi.Tick(idlePillar, idleHost);
        }

        Assert.Empty(idleSound.StopRequests);
        Assert.Empty(idleSound.Requests);

        var (pillar, hero, host, sound) = Bench(contact: true);
        for (var tick = 0; tick <= PillarAi.PushTicks; tick++)
        {
            PillarAi.Tick(pillar, host);
        }

        hero.XCollisionEntity = null;
        PillarAi.Tick(pillar, host);
        Assert.Equal(new[] { 25 }, sound.StopRequests);
    }

    [Fact]
    public void World26_ThePlayerReceivesTheStopOf25OnceAtTheFrame103_TheTickTheAnimationGoesFrom1To0_AndNotBefore()
    {
        using var run = new PillarRun("g5c-stop", Zone, World, 300, 424, 16);
        run.Watched = run.Record(PillarRecord);

        run.Hold(PadState.Right, 100);

        Assert.Equal(67, run.Samples.Sum(sample => sample.PushSounds)); // the frames 34 to 100 played the sound at every tick of the push
        Assert.Empty(run.Sounds.StopRequests);                                              // a continuous push, even against the wall of the column 15: no stop

        run.Release(2);
        Assert.Empty(run.Sounds.StopRequests);                                              // the frames 101 and 102: the animation is still 1

        run.Release(1);
        Assert.Equal(new[] { 25 }, run.Sounds.StopRequests);                                // the frame 103: the stop, once

        run.Release(20);
        Assert.Equal(new[] { 25 }, run.Sounds.StopRequests);                                // and no second one
        Assert.Equal(69, run.Samples.Sum(sample => sample.PushSounds)); // the plays of the frames 34 to 102 (the frames 101 and 102 still push, one tick after the release) and none after the stop: the stop is not a play
    }

    [Fact]
    public void World26_AHeroThatWalksPastThePillar_StopsNothing_WhereTheOneThatPushedItStopsTheScrape()
    {
        using (var past = new PillarRun("g5c-past", Zone, World, 300, 424 + 16, 16))
        {
            past.Watched = past.Record(PillarRecord);
            past.Hold(PadState.Right, 90);
            past.Release(10);
            Assert.Empty(past.Sounds.StopRequests); // the pillar was never pushed: no sound, no stop
        }

        using var pushed = new PillarRun("g5c-pushed", Zone, World, 300, 424, 16);
        pushed.Watched = pushed.Record(PillarRecord);
        pushed.Hold(PadState.Right, 100);
        pushed.Release(10);
        Assert.Equal(new[] { 25 }, pushed.Sounds.StopRequests);
    }
}
