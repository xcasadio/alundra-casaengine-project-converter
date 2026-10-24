#nullable enable
using Xunit;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5c T2 (docs/plan-e20-heros.md section 1.24.6, D-G5C-2, D-G5C-4): the SCRIPT path of the stop. The right button of Torla (map 426, record 17): a push of 130 frames, the slide of 80 px on the frames 54 to 213, the <c>0xBE 25</c> at the frame 215
/// (<see cref="ButtonAndBlockSitesTests"/>), run with the REAL <see cref="Alundra.Audio.SoundPlayer"/> over the real manifest and a fake audio engine. The scrape is alive at the frame 214 and gone at 216. Measured on the discovery probe.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ButtonStopSoundWorldTests
{
    private static void RunTorla(bool stopWired, out int aliveAt214, out int aliveAt216, out int aliveAtTheEnd, out int playCalls)
    {
        var flags = new[] { 1253, 1254, 1267, 1269 };
        using var run = new ScriptedPushRun("g5c-b426", "Nirude", "Nirude (inner)-426", flags, 252, 790, 32, 17);
        var (player, backend, service) = RealSoundWorld.RealPlayer(new object());
        RealSoundWorld.Install(run.Arc.Proxy, stopWired ? player : new RealSoundWorld.IgnoreStop(player));
        aliveAt214 = -1;
        aliveAt216 = -1;
        for (var f = 1; f <= 310; f++)
        {
            if (f <= 130)
            {
                run.Hold(ScriptedPushRun.Pad('D'), 1);
            }
            else
            {
                run.Release(1);
            }

            if (f == 214)
            {
                aliveAt214 = backend.ActiveVoiceCount;
            }

            if (f == 216)
            {
                aliveAt216 = backend.ActiveVoiceCount;
            }
        }

        service.Update(0.016f);
        aliveAtTheEnd = backend.ActiveVoiceCount;
        playCalls = backend.PlayCalls.Count;
    }

    [Fact]
    public void Torla_TheBE25AtTheFrame215_EndsTheScrapeOfTheButton()
    {
        RunTorla(true, out var alive214, out var alive216, out var end, out var plays);

        Assert.True(plays >= 1);
        Assert.Equal(1, alive214);                        // the scrape lives through the slide
        Assert.Equal(0, alive216);                        // gone right after the 0xBE of the frame 215
        Assert.Equal(0, end);
    }

    [Fact]
    public void Torla_WithoutTheStop_TheSameScrapeNeverEnds()
    {
        RunTorla(false, out var alive214, out var alive216, out var end, out var plays);

        Assert.True(plays >= 1);
        Assert.Equal(1, alive214);
        Assert.Equal(1, alive216);                        // the HEAD before g5c: the 0xBE changes nothing
        Assert.Equal(1, end);
    }
}
