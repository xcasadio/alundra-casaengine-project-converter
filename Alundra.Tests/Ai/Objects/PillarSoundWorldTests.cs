#nullable enable
using Xunit;
using Alundra.Hero;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5c T2 (docs/plan-e20-heros.md section 1.24.6, D-G5C-2, D-G5C-4): the pillar of the map 26 pushed in the REAL world (<see cref="PillarRun"/>, the production registry) with the REAL <see cref="Alundra.Audio.SoundPlayer"/> over the real manifest
/// and a fake audio engine: the scrape (record 25, a looping tone, <c>MaxVoices</c> 1) lives from the start of the push to its release and no longer. Measured on the discovery probe (<c>G5cWorldProbeTests.cs.txt</c>).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarSoundWorldTests
{
    [Fact]
    public void TheScrapeLivesFromTheStartOfThePushToItsRelease_AndStopsAtTheFrame103()
    {
        using var run = new PillarRun("g5c-stop", "Ancient Shrine", "Ancient Shrine-26", 300, 424, 16);
        var (player, backend, service) = RealSoundWorld.RealPlayer(new object());
        RealSoundWorld.Install(run.Arc.Proxy, player);

        run.Hold(PadState.Right, 33);
        Assert.Empty(backend.PlayCalls);                  // the first 32 ticks of contact are silent
        run.Hold(PadState.Right, 1);                      // frame 34: the animation 1 and the first PlaySfx(25)
        Assert.Single(backend.PlayCalls);
        Assert.True(backend.PlayCalls[0].Parameters.IsLooped);
        Assert.Equal(1, backend.ActiveVoiceCount);
        run.Hold(PadState.Right, 66);
        Assert.Equal(1, backend.ActiveVoiceCount);        // alive through the whole push
        Assert.Single(backend.PlayCalls);                 // the cap of one voice refuses the replays

        run.Release(1);                                   // frame 101: still pushing one tick after the release
        run.Release(2);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount);        // stopped at the frame 103 (the tick the animation goes 1 -> 0)
    }

    [Fact]
    public void WithoutTheStop_TheSameScrapeNeverEnds_TenSecondsAfterTheRelease()
    {
        using var run = new PillarRun("g5c-head", "Ancient Shrine", "Ancient Shrine-26", 300, 424, 16);
        var (player, backend, service) = RealSoundWorld.RealPlayer(new object());
        RealSoundWorld.Install(run.Arc.Proxy, new RealSoundWorld.IgnoreStop(player));

        run.Hold(PadState.Right, 100);
        Assert.Single(backend.PlayCalls);
        Assert.Equal(1, backend.ActiveVoiceCount);

        run.Release(500);                                 // 10 s after the release
        service.Update(0.016f);
        Assert.Equal(1, backend.ActiveVoiceCount);        // the HEAD before g5c: the scrape is still playing
    }
}
