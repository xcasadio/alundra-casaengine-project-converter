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

    // B3 (docs/plan-e11b-opcodes-audio.md, D-B-7): a RefSfxId redirection pair for the ceiling-under-
    // redirection tests below - id 500 (a FOREIGN vab_id, 99) redirects to its sibling id 501 (vab_id
    // 56, the group these tests request).
    private static readonly Guid Id500Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000500");
    private static readonly Guid Id501Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000501");

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
          },
          {
            "id": 500, "vab_id": 99, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 501, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0500.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id500Tone0}}" }
            ]
          },
          {
            "id": 501, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0501.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id501Tone0}}" }
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

    private AlundraSoundPlayer NewPlayer(
        out FakeAudioBackend backend, out AudioService service, object? owner = null, int? soundGroup = null)
    {
        backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(Id300Tone0, new FakeAudioClip("sfx_0300", 11025));
        provider.Register(Id302Tone0, new FakeAudioClip("sfx_0302_0", 11025));
        provider.Register(Id302Tone1, new FakeAudioClip("sfx_0302_1", 10401));
        provider.Register(Id61Tone0, new FakeAudioClip("sfx_0061", 18142));
        provider.Register(Id500Tone0, new FakeAudioClip("sfx_0500", 11025));
        provider.Register(Id501Tone0, new FakeAudioClip("sfx_0501", 11025));

        service = new AudioService(backend) { ClipProvider = provider };
        return new AlundraSoundPlayer(service, new AlundraSoundBank(_projectPath), owner ?? new object(), soundGroup);
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
    public void RemixVoice_OnAMonoFallbackVoice_NeverTouchesItsVolumeOrPan_UnderBusGain()
    {
        // T4.3 (docs/plan-audio-mix-exact-muet.md): this fixture's records carry no VAB volume/pan
        // attributes, so PlaySfx(300) starts through T4.2's own mono fallback (AudioService.PlayClip,
        // unit volume, centred pan). RemixVoice on such a voice has no stereo gains to remix
        // (GetVoiceStereoGains returns false) - it is skipped entirely, never falling back to the old
        // SetVoiceVolume/SetVoicePan projection (removed in T4.3). The trap this test kills: a leftover
        // call to either would move Volume/Pan away from their as-started values.
        var player = NewPlayer(out var backend, out var service);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;

        player.PlaySfx(300); // one mono voice, full pre-gain volume (1.0) -> backend sees 1.0 * 0.5 = 0.5.
        Assert.Single(backend.PlayCalls);
        var beforeApplied = backend.GetParameters(new AudioVoiceHandle(0, 0));
        Assert.Equal(0f, beforeApplied.Pan);
        Assert.Equal(0.5f, beforeApplied.Volume, 4);

        player.RemixVoice(300, left: 127, right: 0);

        Assert.Equal(1, backend.ActiveVoiceCount);
        var afterApplied = backend.GetParameters(new AudioVoiceHandle(0, 0));
        Assert.Equal(0f, afterApplied.Pan); // unchanged - never remixed.
        Assert.Equal(0.5f, afterApplied.Volume, 4); // unchanged - bus gain still the only factor.
    }

    // -----------------------------------------------------------------------------------------------
    // B3 (docs/plan-e11b-opcodes-audio.md, D-B-7): the sound group reaches AlundraSoundBank.TryResolve,
    // the RefSfxId chain redirects for a foreign VabId, and the polyphony ceiling filters by the
    // REQUESTED record's own VabId (fact 7 corrected) - inoperative under redirection, still biting
    // without it.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void PlaySfx_Id500_ForeignVabId_WithMatchingSoundGroup_RedirectsThroughRefSfxIdChain_PlaysId501Tone()
    {
        // Id 500's own vab_id (99) does not match the player's soundGroup (56) - TryResolve must follow
        // RefSfxId to id 501 (vab_id 56) and play THAT record's tone, not id 500's own.
        var player = NewPlayer(out var backend, out _, soundGroup: 56);

        player.PlaySfx(500);

        Assert.Single(backend.PlayCalls);
        Assert.Equal("sfx_0501", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
    }

    [Fact]
    public void PlaySfx_Id500_UnderRedirection_TheCeilingNeverBites_CountStaysZero()
    {
        // Fact 7 corrected: id 501's own MaxVoices is 1, so WITHOUT the fix a second request (a
        // different rendered frame, so the anti-duplicate table is not what refuses it) would be
        // blocked. Under redirection the original's own filter (by the REQUESTED record's VabId, 99)
        // never matches a voice registered under the RESOLVED VabId (56), so the ceiling stays
        // inoperative and every request keeps succeeding.
        var player = NewPlayer(out var backend, out _, soundGroup: 56);

        player.PlaySfx(500);
        player.FlushFrameSounds();
        player.PlaySfx(500);
        player.FlushFrameSounds();
        player.PlaySfx(500);

        // The mutation this test kills: filtering by the RESOLVED VabId (56) instead of the requested
        // one (99) would match every one of these voices and cap the count at MaxVoices=1 after the
        // first call - producing 1 PlayCall instead of 3.
        Assert.Equal(3, backend.PlayCalls.Count);
    }

    [Fact]
    public void PlaySfx_Id501_DirectRequest_NoRedirection_TheCeilingStillBites()
    {
        // Requesting id 501 directly (its OWN vab_id, 56, already equals the group) never enters the
        // RefSfxId branch - requested and resolved VabId are the same, so this is the "no redirection"
        // control case fact 7 says must keep biting exactly as before.
        var player = NewPlayer(out var backend, out var service, soundGroup: 56);

        player.PlaySfx(501); // 1 voice live - MaxVoices=1 already saturated.
        Assert.Single(backend.PlayCalls);

        player.FlushFrameSounds(); // a different frame, so the anti-duplicate table is not the refusal.
        player.PlaySfx(501); // refused by the ceiling - no new clip.
        Assert.Single(backend.PlayCalls);

        backend.CompleteAllVoices();
        service.Update(0.016f);
        player.FlushFrameSounds();

        player.PlaySfx(501);
        Assert.Equal(2, backend.PlayCalls.Count); // allowed again once the live voice finished.
    }
}
