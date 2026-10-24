#nullable enable
using System;
using System.IO;
using CasaEngine.Framework.Audio;
using Xunit;
using Alundra.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Audio;

/// <summary>
/// E20.g5c T1 (docs/plan-e20-heros.md section 1.24.6, D-G5C-2, D-G5C-5), phase A: <see cref="SoundPlayer.StopSfx"/> against the model of <c>StopSoundEffect</c> (<c>0x80049634</c>) that the discovery rejoined on the real code
/// (<c>docs/plan-e20-annexe/e20-g5/g5c-disc/stop_oracle.py</c>: 3 000 sequences without a gap, the 7 goldens of <c>stop_goldens.json</c>, the named scenarios of <c>stop_oracle_out.txt</c> that the cases below follow). A real
/// <see cref="SoundPlayer"/> over a real <see cref="AudioService"/> and the fake backend, on a synthetic manifest that has the three shapes of the real records: the id 25 (the stone scraping: one looping tone, <c>MaxVoices</c> 1),
/// the id 85 (two looping tones, <c>MaxVoices</c> 1) and the id 123 (a one-shot). Every "nothing happens" case sits next to a positive one that shows the stop does act on the same player.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class SoundPlayerStopTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "SoundPlayerStopTests_" + Guid.NewGuid());
    private static readonly Guid Id25 = Guid.Parse("00000000-0000-0000-0000-000000002500");
    private static readonly Guid Id85a = Guid.Parse("00000000-0000-0000-0000-000000008500");
    private static readonly Guid Id85b = Guid.Parse("00000000-0000-0000-0000-000000008501");
    private static readonly Guid Id123 = Guid.Parse("00000000-0000-0000-0000-000000012300");
    private static readonly Guid Id500 = Guid.Parse("00000000-0000-0000-0000-000000000500");
    private static readonly Guid Id501 = Guid.Parse("00000000-0000-0000-0000-000000000501");

    public SoundPlayerStopTests()
    {
        BgmFadeDirector.Instance.ResetForTests();
        var dir = Path.Combine(_projectPath, "Sounds");
        Directory.CreateDirectory(dir);

        // The records 25, 85 and 123 as exported (alundra-project/Sounds/sfx-manifest.json): 25 and 85 loop (repeat true, MaxVoices 1), 123 is a one-shot. 500 redirects to 501 (RefSfxId), as in SoundPlayerTests.
        var json = $$"""
        [
          { "id": 25, "vab_id": -1, "program_number": 1, "tone_number": 15, "note": 75, "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0025.wav", "sample_rate": 10401, "loop_start": 2772, "loop_end": 11815, "repeat": true, "asset_id": "{{Id25}}", "volume": 120, "pan": 64 } ],
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64 },
          { "id": 85, "vab_id": -1, "program_number": 5, "tone_number": 10, "note": 67, "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 2, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0085_0.wav", "sample_rate": 10110, "loop_start": 56, "loop_end": 10807, "repeat": true, "asset_id": "{{Id85a}}", "volume": 127, "pan": 0 },
                       { "tone_index": 1, "file": "sfx_0085_1.wav", "sample_rate": 9539, "loop_start": 56, "loop_end": 10807, "repeat": true, "asset_id": "{{Id85b}}", "volume": 127, "pan": 127 } ],
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64 },
          { "id": 123, "vab_id": -1, "program_number": 0, "tone_number": 1, "note": 61, "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0123.wav", "sample_rate": 9604, "loop_start": 28, "loop_end": 14615, "repeat": false, "asset_id": "{{Id123}}", "volume": 100, "pan": 64 } ],
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64 },
          { "id": 500, "vab_id": 99, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1, "ref_sfx_id": 501, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0500.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id500}}" } ] },
          { "id": 501, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0501.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id501}}" } ] }
        ]
        """;
        File.WriteAllText(Path.Combine(dir, "sfx-manifest.json"), json);
    }

    public void Dispose()
    {
        BgmFadeDirector.Instance.ResetForTests();
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private SoundPlayer NewPlayer(out FakeAudioBackend backend, out AudioService service, int? soundGroup = null)
    {
        backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(Id25, new FakeAudioClip("sfx_0025", 10401));
        provider.Register(Id85a, new FakeAudioClip("sfx_0085_0", 10110));
        provider.Register(Id85b, new FakeAudioClip("sfx_0085_1", 9539));
        provider.Register(Id123, new FakeAudioClip("sfx_0123", 9604));
        provider.Register(Id500, new FakeAudioClip("sfx_0500", 11025));
        provider.Register(Id501, new FakeAudioClip("sfx_0501", 11025));
        service = new AudioService(backend) { ClipProvider = provider };
        return new SoundPlayer(service, new SfxBank(_projectPath), new object(), soundGroup);
    }

    // ---- the stop acts on the voices of the id, and only of the id --------------------------------------------------------------------

    [Fact]
    public void StopSfx_Id25_StopsItsVoice_AndLeavesTheVoicesOfTheOtherIdsAlone()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);
        player.PlaySfx(85); // two tones
        Assert.Equal(3, backend.ActiveVoiceCount);

        player.StopSfx(25);
        service.Update(0.016f);

        Assert.Equal(2, backend.ActiveVoiceCount); // the voice of 25 is gone (the positive) ...
        player.StopSfx(85);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount); // ... and the two voices that remained were the ones of 85 (stopping them empties the backend).
    }

    [Fact]
    public void StopSfx_Id85_StopsBothTonesOfTheSound_AndNotTheScrapeNextToIt()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);
        player.PlaySfx(85);
        Assert.Equal(3, backend.ActiveVoiceCount);

        player.StopSfx(85);
        service.Update(0.016f);

        Assert.Equal(1, backend.ActiveVoiceCount); // both tones of 85 are stopped (3 - 2), not only the first ...
        player.StopSfx(25);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount); // ... and the one left is the voice of 25.
    }

    [Fact]
    public void StopSfx_FreesTheCap_TheSoundCanStartAgainAtOnce_WhereWithoutTheStopTheRequestIsRefused()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);
        player.FlushFrameSounds();
        player.PlaySfx(25); // MaxVoices 1, one voice alive: refused (the defect the stop repairs: the scrape never ended, every later request was refused).
        Assert.Single(backend.PlayCalls);

        player.StopSfx(25);
        service.Update(0.016f);
        player.FlushFrameSounds();
        player.PlaySfx(25);

        Assert.Equal(2, backend.PlayCalls.Count); // the cap is free: the sound starts again.
        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopSfx_DoesNotTouchTheAntiDuplicateTable_ThePlayAfterAStopInTheSameFrameIsRefused_ThenAllowedNextFrame()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);
        player.StopSfx(25);
        player.PlaySfx(25); // same frame: the table of the frame still holds 25 (Stop does not write it nor clear it) - the binary refuses it.
        Assert.Single(backend.PlayCalls);

        player.FlushFrameSounds();
        player.PlaySfx(25);
        Assert.Equal(2, backend.PlayCalls.Count); // the next frame: allowed again.
        service.Update(0.016f);
        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopSfx_DoesNotRegisterTheIdInTheAntiDuplicateTable_TheSoundCanPlayAgainInTheFrameOfItsStop_IfItWasNotAskedToPlayInThatFrame()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);          // the frame 1: 25 is in the table of that frame
        player.FlushFrameSounds();   // the frame 2: a fresh table

        player.StopSfx(25);          // the stop acts ...
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount);

        player.PlaySfx(25);          // ... and does not put 25 in the table of the frame 2: the play of that same frame is allowed
        Assert.Equal(2, backend.PlayCalls.Count);
        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    // ---- the cases where the stop does nothing, each next to the case where it acts ----------------------------------------------------

    [Fact]
    public void StopSfx_Id0_IsANoOp_WhereTheStopOfARealIdOnTheSamePlayerActs()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);

        player.StopSfx(0);
        service.Update(0.016f);
        Assert.Equal(1, backend.ActiveVoiceCount); // the binary returns at once for the id 0

        player.StopSfx(25);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount); // the positive
    }

    [Fact]
    public void StopSfx_AnUnknownId_IsANoOp_WhereTheStopOfARealIdOnTheSamePlayerActs()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);

        player.StopSfx(999);
        service.Update(0.016f);
        Assert.Equal(1, backend.ActiveVoiceCount);

        player.StopSfx(25);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopSfx_ASoundThatIsNotPlaying_IsANoOp_ItDoesNotStopTheVoiceOfAnotherId()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);

        player.StopSfx(85); // a known id with no live voice
        service.Update(0.016f);
        Assert.Equal(1, backend.ActiveVoiceCount);

        player.StopSfx(25);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopSfx_AOneShotThatEndedByItself_FindsNothingAndDoesNotThrow_ThenTheSoundPlaysAgain()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(123);
        Assert.Equal(1, backend.ActiveVoiceCount);
        backend.CompleteAllVoices();
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount);

        player.StopSfx(123); // nothing to stop
        player.FlushFrameSounds();
        player.PlaySfx(123);

        Assert.Equal(2, backend.PlayCalls.Count); // the positive: the cap is free, the sound plays again
        Assert.Equal(1, backend.ActiveVoiceCount);

        player.StopSfx(123);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount); // and the stop acts on the live one
    }

    // ---- the master fade machine does not gate the stop (the play it does) ----------------------------------------------------------------

    [Fact]
    public void StopSfx_IsNotGatedByTheArmedStateOfTheFadeMachine_WhereThePlayIs()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25);
        Assert.Equal(1, backend.ActiveVoiceCount);

        BgmFadeDirector.Instance.LoadBgm(5); // arms the machine (g_soundEffectState counting down)
        Assert.True(BgmFadeDirector.Instance.IsArmed);

        player.FlushFrameSounds();
        player.PlaySfx(85);
        Assert.Single(backend.PlayCalls); // the play IS refused while armed (the state gate of PlaySoundEffectCore) ...

        player.StopSfx(25);
        service.Update(0.016f);
        Assert.Equal(0, backend.ActiveVoiceCount); // ... the stop is not: the binary reads neither the countdown nor the state.
    }

    // ---- the redirection by RefSfxId ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void StopSfx_ARedirectedId_StopsTheVoiceItWasAskedFor_ByTheRequestedId()
    {
        var player = NewPlayer(out var backend, out var service, soundGroup: 56);
        player.PlaySfx(500); // id 500 has a foreign vab (99) and redirects to the tone of 501
        Assert.Single(backend.PlayCalls);
        Assert.Equal("sfx_0501", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
        Assert.Equal(1, backend.ActiveVoiceCount);

        player.StopSfx(500);
        service.Update(0.016f);

        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    // ---- two objects, one sound (D-G5C-5) ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void StopSfx_ASingleStop_CutsTheSoundOfTheOtherObjectToo_BecauseTheSecondRequestWasRefusedByTheCap()
    {
        var player = NewPlayer(out var backend, out var service);
        player.PlaySfx(25); // object A starts its scrape
        player.FlushFrameSounds();
        player.PlaySfx(25); // object B asks the same id: refused by MaxVoices 1, nothing new plays - it moves in silence
        Assert.Single(backend.PlayCalls);
        Assert.Equal(1, backend.ActiveVoiceCount);

        player.StopSfx(25); // A finishes
        service.Update(0.016f);

        Assert.Equal(0, backend.ActiveVoiceCount); // the only voice, the one B shares, is cut with it
    }
}
