using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Audio;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T4.2 (docs/plan-audio-mix-exact-muet.md): <see cref="AlundraSoundPlayer.PlaySfx"/> starting a real
/// stereo voice through <see cref="AudioService.PlayClipStereo"/>, with the exact key-on left/right SPU
/// volumes (<see cref="AlundraSpuVoiceVolume.ComputeKeyOn"/>) - and its two fallback causes (a record
/// without the ADR-0003 attributes, a clip without samples), which must still start a mono voice exactly
/// as before T4.2 (never silence). Same shape as <see cref="AlundraSoundPlayerTests"/>, split into its
/// own file because it needs its own synthetic manifest fixture (the VAB/program/tone attributes T4.2
/// reads) and its own <see cref="FakeAudioClip"/> flavours (with/without <see cref="IAudioClipSamples"/>
/// samples).
/// </summary>
public sealed class AlundraSoundPlayerStereoVolumeTests : IDisposable
{
    private readonly string _projectPath =
        Path.Combine(Path.GetTempPath(), "AlundraSoundPlayerStereoVolumeTests_" + Guid.NewGuid());

    // Id 302: real values (docs/plan-audio-mix-exact-muet.md, T4.2 table) - VAB 127, program 127,
    // program pan 64, tone 0 (100, 34) -> (10157, 2957), tone 1 (100, 94) -> (2786, 10157).
    private static readonly Guid Id302Tone0 = Guid.Parse("00000000-0000-0000-0000-000000030200");
    private static readonly Guid Id302Tone1 = Guid.Parse("00000000-0000-0000-0000-000000030201");

    // Id 162: real values - tone 1 (0, 0) is silent (gains 0/0) but still a live voice (never a no-op).
    private static readonly Guid Id162Tone1 = Guid.Parse("00000000-0000-0000-0000-000001620001");

    // Id 400: attributes present, but the provider hands back a clip with no samples (fallback cause 2).
    private static readonly Guid Id400Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000400");

    // Id 401: no attributes at all in the manifest (fallback cause 1).
    private static readonly Guid Id401Tone0 = Guid.Parse("00000000-0000-0000-0000-000000000401");

    public AlundraSoundPlayerStereoVolumeTests()
    {
        var soundsDirectory = Path.Combine(_projectPath, "Sounds");
        Directory.CreateDirectory(soundsDirectory);

        var json = $$"""
        [
          {
            "id": 302, "vab_id": 56, "program_number": 0, "tone_number": 2, "note": 62,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 2, "num_tones": 2, "skip_reason": null,
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64,
            "tones": [
              { "tone_index": 0, "file": "sfx_0302_0.wav", "sample_rate": 11025, "loop_start": 1820, "loop_end": 28055, "repeat": true, "asset_id": "{{Id302Tone0}}", "volume": 100, "pan": 34 },
              { "tone_index": 1, "file": "sfx_0302_1.wav", "sample_rate": 10401, "loop_start": 1820, "loop_end": 28055, "repeat": true, "asset_id": "{{Id302Tone1}}", "volume": 100, "pan": 94 }
            ]
          },
          {
            "id": 162, "vab_id": 12, "program_number": 0, "tone_number": 1, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 4, "num_tones": 1, "skip_reason": null,
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64,
            "tones": [
              { "tone_index": 0, "file": "sfx_0162_1.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id162Tone1}}", "volume": 0, "pan": 0 }
            ]
          },
          {
            "id": 400, "vab_id": -1, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "vab_master_volume": 127, "program_volume": 127, "program_pan": 64,
            "tones": [
              { "tone_index": 0, "file": "sfx_0400.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id400Tone0}}", "volume": 127, "pan": 64 }
            ]
          },
          {
            "id": 401, "vab_id": -1, "program_number": 0, "tone_number": 0, "note": 60,
            "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [
              { "tone_index": 0, "file": "sfx_0401.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 0, "repeat": false, "asset_id": "{{Id401Tone0}}" }
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

    private AlundraSoundPlayer NewPlayer(out FakeAudioBackend backend, out AudioService service)
    {
        backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(Id302Tone0, new FakeAudioClip("sfx_0302_0", 11025, monoSamples: new short[] { 100, -100 }));
        provider.Register(Id302Tone1, new FakeAudioClip("sfx_0302_1", 10401, monoSamples: new short[] { 200, -200 }));
        provider.Register(Id162Tone1, new FakeAudioClip("sfx_0162_1", 11025, monoSamples: new short[] { 300, -300 }));
        // Id 400's clip deliberately has NO samples (monoSamples left null/empty): fallback cause 2.
        provider.Register(Id400Tone0, new FakeAudioClip("sfx_0400", 11025));
        provider.Register(Id401Tone0, new FakeAudioClip("sfx_0401", 11025, monoSamples: new short[] { 400, -400 }));

        service = new AudioService(backend) { ClipProvider = provider };
        return new AlundraSoundPlayer(service, new AlundraSoundBank(_projectPath), new object());
    }

    [Fact]
    public void PlaySfx_Id302_StartsOneStereoVoicePerTone_WithTheKeyOnGains()
    {
        var player = NewPlayer(out var backend, out var service);

        player.PlaySfx(302);

        Assert.Equal(2, backend.ActiveVoiceCount);
        Assert.Empty(backend.PlayCalls); // never the mono PlayClip fallback.

        var (left0, right0) = AlundraSpuVoiceVolume.ComputeKeyOn(127, 127, 64, 100, 34);
        var (left1, right1) = AlundraSpuVoiceVolume.ComputeKeyOn(127, 127, 64, 100, 94);
        Assert.Equal((10157, 2957), (left0, right0)); // T4.2 table, 302 t0.
        Assert.Equal((2786, 10157), (left1, right1)); // T4.2 table, 302 t1.

        Assert.True(service.GetVoiceStereoGains(new AudioVoiceHandle(0, 0), out var g0Left, out var g0Right));
        Assert.Equal(AlundraSpuVoiceVolume.ToGain(left0), g0Left, 5);
        Assert.Equal(AlundraSpuVoiceVolume.ToGain(right0), g0Right, 5);

        Assert.True(service.GetVoiceStereoGains(new AudioVoiceHandle(1, 0), out var g1Left, out var g1Right));
        Assert.Equal(AlundraSpuVoiceVolume.ToGain(left1), g1Left, 5);
        Assert.Equal(AlundraSpuVoiceVolume.ToGain(right1), g1Right, 5);
    }

    [Fact]
    public void PlaySfx_Id162_SilentTone_StillStartsALiveVoice_WithZeroGains()
    {
        var player = NewPlayer(out var backend, out var service);

        player.PlaySfx(162);

        Assert.Equal(1, backend.ActiveVoiceCount); // a live voice, not a no-op.
        Assert.Empty(backend.PlayCalls);

        Assert.True(service.GetVoiceStereoGains(new AudioVoiceHandle(0, 0), out var left, out var right));
        Assert.Equal(0f, left);
        Assert.Equal(0f, right);
    }

    [Fact]
    public void PlaySfx_MissingAttributes_FallsBackToTheMonoPathUnitVolumeCenteredPan()
    {
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(401);

        Assert.Single(backend.PlayCalls);
        var applied = backend.PlayCalls[0].Parameters;
        Assert.Equal(AudioVoiceParameters.MaxVolume, applied.Volume);
        Assert.Equal(0f, applied.Pan);
    }

    [Fact]
    public void PlaySfx_ClipWithoutSamples_FallsBackToTheMonoPathUnitVolumeCenteredPan()
    {
        var player = NewPlayer(out var backend, out _);

        player.PlaySfx(400);

        Assert.Single(backend.PlayCalls);
        var applied = backend.PlayCalls[0].Parameters;
        Assert.Equal(AudioVoiceParameters.MaxVolume, applied.Volume);
        Assert.Equal(0f, applied.Pan);
        Assert.Equal("sfx_0400", ((FakeAudioClip)backend.PlayCalls[0].Clip).Name);
    }
}
