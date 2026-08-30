using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Audio;
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
    public void PlaySfx_Id300_TwoSuccessiveRequests_ProduceTwoClips()
    {
        // The assertion that kills a mute-after-the-first-sound player (D-E11-4: no per-frame
        // anti-duplicate filter in this seam) - id 300's own MaxVoices is 2, so both requests succeed.
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(300);
        player.PlaySfx(300);

        Assert.Equal(2, backend.PlayCalls.Count);
    }

    [Fact]
    public void PlaySfx_Id302_MaxVoicesOne_RefusesSecondRequestWhileVoicesLive_ThenAllowsItAgain()
    {
        var player = NewPlayer(out var backend, out var service);

        player.PlaySfx(302); // 2 voices live (one per tone) - MaxVoices=1 is already saturated.
        Assert.Equal(2, backend.PlayCalls.Count);

        player.PlaySfx(302); // refused entirely - no new clips.
        Assert.Equal(2, backend.PlayCalls.Count);

        // Voices finish: the backend reaches Stopped, then AudioService.Update recycles the entries -
        // only THEN does AudioService.IsAlive go false and the cap release.
        backend.CompleteAllVoices();
        service.Update(0.016f);

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
}
