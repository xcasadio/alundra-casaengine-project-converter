using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T5 of docs/plan-e11-audio.md, slice E11.a - the readback blocker of the plan's own §2.2b: without
/// this test, every mechanic <see cref="AlundraSoundPlayer"/> claims to port (one voice per tone,
/// MaxVoices ceiling, no per-tone anti-duplicate) would ship unproven, because a request-only oracle
/// (T1/T2) cannot see past <see cref="IAlundraSoundPlayer.PlaySfx"/>'s own boundary. Built against a
/// REAL <see cref="AlundraSoundPlayer"/> over a REAL <see cref="AudioService"/>, backed by
/// <see cref="FakeAudioBackend"/>/<see cref="FakeAudioClip"/>/<see cref="FakeAudioClipProvider"/> (this
/// project's own re-implementation of the engine's test fakes - see <see cref="FakeAudioBackend"/>'s own
/// doc), the exact shape CasaEngineMonogame\CasaEngine.Tests\Audio\AudioServicePlaySoundTests.cs uses.
///
/// A synthetic <c>Sounds/sfx-manifest.json</c> fixture (not the real export - T3/T1/T2 already cover the
/// real one) keeps every id/guid/MaxVoices value under this test's own control.
/// </summary>
public class AlundraSoundPlayerTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundPlayerTests_" + Guid.NewGuid());

    private static readonly Guid Id300Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000300");
    private static readonly Guid Id302Tone0 = Guid.Parse("00000000-0000-0000-0000-000000030200");
    private static readonly Guid Id302Tone1 = Guid.Parse("00000000-0000-0000-0000-000000030201");
    private static readonly Guid Id61Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000061");

    public AlundraSoundPlayerTests()
    {
        var soundsDirectory = Path.Combine(_projectPath, "Sounds");
        Directory.CreateDirectory(soundsDirectory);

        var json = $$"""
        [
          {
            "id": 300, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 2, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0300.wav", "sample_rate": 11025, "loop_start": 28, "loop_end": 8847, "repeat": false, "asset_id": "{{Id300Tone0}}" }
            ]
          },
          {
            "id": 302, "vab_id": 56, "program_number": 0, "tone_number": 2, "note": 62,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 2, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0302_0.wav", "sample_rate": 11025, "loop_start": 1820, "loop_end": 28055, "repeat": true, "asset_id": "{{Id302Tone0}}" },
              { "tone_index": 1, "file": "sfx_0302_1.wav", "sample_rate": 10401, "loop_start": 1820, "loop_end": 28055, "repeat": true, "asset_id": "{{Id302Tone1}}" }
            ]
          },
          {
            "id": 61, "vab_id": -1, "program_number": 5, "tone_number": 6, "note": 64,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0061.wav", "sample_rate": 18142, "loop_start": 28, "loop_end": 7867, "repeat": false, "asset_id": "{{Id61Tone0}}" }
            ]
          }
        ]
        """;

        File.WriteAllText(Path.Combine(soundsDirectory, "sfx-manifest.json"), json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private AlundraSoundPlayer NewPlayer(out FakeAudioBackend backend, out AudioService service, object? owner = null)
    {
        backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(Id300Tone0, new FakeAudioClip("sfx_0300", 11025));
        provider.Register(Id302Tone0, new FakeAudioClip("sfx_0302_0", 11025));
        provider.Register(Id302Tone1, new FakeAudioClip("sfx_0302_1", 10401));
        provider.Register(Id61Tone0, new FakeAudioClip("sfx_0061", 18142));

        service = new AudioService(backend) { ClipProvider = provider };
        return new AlundraSoundPlayer(service, new AlundraSoundBank(_projectPath), owner ?? new object());
    }

    [Fact]
    public void PlaySfx_Id302_StartsTwoLoopedClips_OneTonePerVoice()
    {
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(302);

        Assert.Equal(2, backend.PlayCalls.Count);
        Assert.True(backend.PlayCalls[0].Parameters.IsLooped);
        Assert.True(backend.PlayCalls[1].Parameters.IsLooped);
        Assert.Equal("sfx_0302_0", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
        Assert.Equal("sfx_0302_1", ((FakeAudioClip)backend.PlayCalls[1].Clip).Name);
    }

    [Fact]
    public void PlaySfx_Id300_StartsOneClip_NotLooped()
    {
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(300);

        Assert.Single(backend.PlayCalls);
        Assert.False(backend.PlayCalls[0].Parameters.IsLooped);
        Assert.Equal("sfx_0300", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
    }

    [Fact]
    public void PlaySfx_Id300_TwoSuccessiveRequestsAcrossFrames_ProduceTwoClips()
    {
        // MaxVoices lets both requests succeed (2 for id 300) - but B1's own per-frame anti-duplicate
        // table (docs/plan-e11b-opcodes-audio.md, D-B-4, fact 5) now refuses the SAME id requested twice
        // in the SAME frame, so this asserts the MaxVoices ceiling across two DIFFERENT frames (a
        // FlushFrameSounds between the two requests, standing in for AlundraWorldProxy's own frame-close
        // call) rather than the same-frame case, which the anti-duplicate test below covers instead.
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(300);
        player.FlushFrameSounds();
        player.PlaySfx(300);

        Assert.Equal(2, backend.PlayCalls.Count);
    }

    [Fact]
    public void PlaySfx_Id300_TwoRequestsSameFrame_TheAntiDuplicateTableRefusesTheSecond_ThenAllowsItNextFrame()
    {
        // The anti-duplicate table itself (D-B-4, fact 5): a duplicate id within the SAME rendered frame
        // is refused outright - even though MaxVoices (2, well above 1) would otherwise allow it. The
        // mutation "no per-frame filter" (this seam's own pre-B1 shape) fails this test by producing 2
        // clips instead of 1.
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(300);
        player.PlaySfx(300); // same frame, same id - refused by the table, not by MaxVoices.
        Assert.Single(backend.PlayCalls);

        player.FlushFrameSounds(); // next rendered frame - AlundraWorldProxy's own frame-close call.
        player.PlaySfx(300);
        Assert.Equal(2, backend.PlayCalls.Count); // the id plays again once the table is flushed.
    }

    [Fact]
    public void PlaySfx_Id302_MaxVoicesOne_RefusesSecondRequestWhileVoicesLive_ThenAllowsItAgain()
    {
        var player = NewPlayer(out var backend, out var service);

        player.PlaySfx(302); // 2 voices live (one per tone) - MaxVoices=1 is already saturated.
        Assert.Equal(2, backend.PlayCalls.Count);

        // A DIFFERENT frame (FlushFrameSounds), so this second request is refused by MaxVoices, not by
        // the anti-duplicate table (D-B-4) - the case under test here.
        player.FlushFrameSounds();
        player.PlaySfx(302); // refused entirely - no new clips.
        Assert.Equal(2, backend.PlayCalls.Count);

        // Voices finish: the backend reaches Stopped, then AudioService.Update recycles the entries -
        // only THEN does AudioService.IsAlive go false and the cap release.
        backend.CompleteAllVoices();
        service.Update(0.016f);
        player.FlushFrameSounds(); // yet another frame.

        player.PlaySfx(302);
        Assert.Equal(4, backend.PlayCalls.Count); // allowed again - two more clips.
    }

    [Fact]
    public void PlaySfx_Id61_PlaysFromTheManifestGuid()
    {
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(61);

        Assert.Single(backend.PlayCalls);
        Assert.Equal("sfx_0061", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
    }

    // -----------------------------------------------------------------------------------------------
    // B1 (docs/plan-e11b-opcodes-audio.md) - the anti-duplicate table's own overflow rule (fact 5) and
    // RemixVoice (0xAB/0xBF, D-B-6).
    // -----------------------------------------------------------------------------------------------

    /// <summary>Builds a synthetic manifest with <paramref name="idCount"/> distinct one-tone,
    /// uncapped-enough (<c>max_voices</c> = <paramref name="idCount"/>) records, ids 1..idCount - its own
    /// isolated project directory, never shared with <see cref="NewPlayer"/>'s fixture above.</summary>
    private static AlundraSoundPlayer NewPlayerWithManyIds(int idCount, out FakeAudioBackend backend, out string projectPath)
    {
        projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundPlayerTests_Overflow_" + Guid.NewGuid());
        var soundsDirectory = Path.Combine(projectPath, "Sounds");
        Directory.CreateDirectory(soundsDirectory);

        var provider = new FakeAudioClipProvider();
        var records = new System.Text.StringBuilder("[");
        for (var id = 1; id <= idCount; id++)
        {
            var assetId = new Guid(id, 0, 0, new byte[8]);
            if (id > 1)
            {
                records.Append(',');
            }

            records.Append($$"""
            {
              "id": {{id}}, "vab_id": -1, "program_number": 0, "tone_number": 0, "note": 60,
              "seq_num": -1, "ref_sfx_id": 0, "max_voices": {{idCount}}, "num_tones": 1, "skip_reason": null,
              "tones": [
                { "tone_index": 0, "file": "sfx_{{id}}.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{assetId}}" }
              ]
            }
            """);

            provider.Register(assetId, new FakeAudioClip($"sfx_{id}", 11025));
        }

        records.Append(']');
        File.WriteAllText(Path.Combine(soundsDirectory, "sfx-manifest.json"), records.ToString());

        backend = new FakeAudioBackend(voiceCapacity: idCount + 8);
        var service = new AudioService(backend) { ClipProvider = provider };
        return new AlundraSoundPlayer(service, new AlundraSoundBank(projectPath), new object());
    }

    [Fact]
    public void PlaySfx_A65thDistinctIdInTheSameFrame_StillPlays_TheTableOverflowsRatherThanBlocking()
    {
        // Port of the original's own documented overflow behaviour (fact 5): the 64-slot table full of
        // 64 DISTINCT ids no longer refuses a 65th distinct id - it plays without being registered. The
        // mutation "table capacity ignored / always refuses once full" fails this test.
        var player = NewPlayerWithManyIds(65, out var backend, out var projectPath);
        try
        {
            for (var id = 1; id <= 65; id++)
            {
                player.PlaySfx(id);
            }

            Assert.Equal(65, backend.PlayCalls.Count);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void RemixVoice_OnANonAudibleSfx_TriggersNothing_ZeroBackendVoicesCreated()
    {
        // Fact 4: PlaySoundEffectWithToneVolumeMix NEVER starts new playback - it only remixes tones
        // ALREADY playing. Id 300 was never requested here, so there is nothing to remix.
        var player = NewPlayer(out var backend, out _);

        player.RemixVoice(300, 127, 127);

        Assert.Empty(backend.PlayCalls);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void RemixVoice_OnALiveVoice_ChangesPan_AndLeavesTheEffectiveVolumeIntactUnderBusGain()
    {
        var player = NewPlayer(out var backend, out var service);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;

        player.PlaySfx(300); // one voice, full pre-gain volume (1.0) -> backend sees 1.0 * 0.5 = 0.5.
        Assert.Single(backend.PlayCalls);

        // Full-left mix: left=127 (max), right=0 - volume stays "full" (max(127,0)/127 = 1.0), pan goes
        // hard left (-1.0).
        player.RemixVoice(300, left: 127, right: 0);

        // AlundraSoundPlayer never hands the raw AudioVoiceHandle back to the caller, so the applied
        // parameters are read straight off the backend's only live slot instead.
        Assert.Equal(1, backend.ActiveVoiceCount);
        var applied = backend.GetParameters(new AudioVoiceHandle(0, 0));
        Assert.Equal(-1f, applied.Pan, 4);
        // The trap this test kills: pushing RemixVoice's own (volume=1.0) raw, bypassing bus gain, would
        // leave the backend at 1.0 instead of the gain-applied 0.5.
        Assert.Equal(0.5f, applied.Volume, 4);
    }
}
