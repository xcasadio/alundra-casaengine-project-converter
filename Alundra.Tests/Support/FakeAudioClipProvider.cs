#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Audio;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>In-memory <see cref="IAudioClipProvider"/> for T5, keyed by the SAME asset guids the
/// synthetic manifest fixture carries (so <see cref="SoundPlayer"/>'s real
/// <see cref="SfxBank.TryResolve"/> lookup and this provider's clips line up) - see
/// <see cref="FakeAudioBackend"/>'s own doc.</summary>
public sealed class FakeAudioClipProvider : IAudioClipProvider
{
    private readonly Dictionary<Guid, IAudioClip> _clips = new();

    private readonly Dictionary<Guid, (string Name, byte[] Wav)> _streams = new();

    public void Register(Guid assetId, IAudioClip clip) => _clips[assetId] = clip;

    /// <summary>Registers a music file served by <see cref="OpenStream"/> (E19.L-b3), a small 16 bit stereo wav by default.</summary>
    public void RegisterStream(Guid assetId, string name, byte[] wav = null) => _streams[assetId] = (name, wav ?? BuildWav());

    /// <summary>The names of the streams opened so far, in order: which music files were actually started.</summary>
    public List<string> OpenedStreams { get; } = new();

    public IAudioClip GetClip(Guid audioFileAssetId) => _clips.GetValueOrDefault(audioFileAssetId);

    public Stream OpenStream(Guid audioFileAssetId)
    {
        if (!_streams.TryGetValue(audioFileAssetId, out var stream))
        {
            return null;
        }

        OpenedStreams.Add(stream.Name);
        return new MemoryStream(stream.Wav, writable: false);
    }

    /// <summary>A canonical 16 bit PCM stereo wav whose frame i holds the value i in both channels.</summary>
    public static byte[] BuildWav(int frameCount = 64, int sampleRate = 44100)
    {
        const int channels = 2;
        const int blockAlign = channels * 2;
        var dataLength = frameCount * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((short)blockAlign);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        for (var frame = 0; frame < frameCount; frame++)
        {
            writer.Write((short)frame);
            writer.Write((short)frame);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
