#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Audio;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>Deterministic <see cref="IAudioClip"/> for T5: no device, no decoding - see
/// <see cref="FakeAudioBackend"/>'s own doc for why this is re-implemented in this project.
///
/// T4.2 (docs/plan-audio-mix-exact-muet.md): also implements <see cref="IAudioClipSamples"/>, exactly
/// like the engine's own <c>CasaEngine.Tests.Audio.FakeAudioClip</c>, so
/// <see cref="AudioService.PlayClipStereo"/> can be exercised without a real wav file - pass
/// <paramref name="monoSamples"/> to expose them, or leave it null/empty for a clip that must fall
/// back away from the stereo key-on path (T4.2's "clip without samples" cause).</summary>
public sealed class FakeAudioClip : IAudioClip, IAudioClipSamples
{
    public FakeAudioClip(string name = "clip", int sampleRate = 44100, short[] monoSamples = null)
    {
        Name = name;
        SampleRate = sampleRate;
        MonoSamples = monoSamples ?? Array.Empty<short>();
    }

    public string Name { get; }

    public int SampleRate { get; }

    public int ChannelCount => 1;

    public TimeSpan Duration => TimeSpan.FromSeconds(1);

    public bool IsDisposed { get; private set; }

    public ReadOnlyMemory<short> MonoSamples { get; }

    public void Dispose() => IsDisposed = true;

    public override string ToString() => Name;
}
