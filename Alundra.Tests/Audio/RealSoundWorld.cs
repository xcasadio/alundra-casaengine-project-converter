#nullable enable
using System;
using System.IO;
using System.Reflection;
using CasaEngine.Framework.Audio;
using Alundra.Audio;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests.Audio;

/// <summary>
/// E20.g5c T2: the pieces of the world tests that run the REAL <see cref="SoundPlayer"/> over the REAL export manifest (record 25: looping tone, <c>MaxVoices</c> 1) and a <see cref="FakeAudioBackend"/>, installed in the proxy of a running arc. The decorator
/// <see cref="IgnoreStop"/> is the player of the HEAD before g5c (a stop that reaches nobody), kept so that each positive test sits next to the case that proves the stop is what ends the sound.
/// </summary>
internal static class RealSoundWorld
{
    /// <summary>A player that forwards everything but the stop of one sound.</summary>
    internal sealed class IgnoreStop : ISoundPlayer
    {
        private readonly ISoundPlayer _inner;

        public IgnoreStop(ISoundPlayer inner) => _inner = inner;

        public void PlaySfx(int sfxId) => _inner.PlaySfx(sfxId);

        public void RemixVoice(int sfxId, int left, int right) => _inner.RemixVoice(sfxId, left, right);

        public void FlushFrameSounds() => _inner.FlushFrameSounds();

        public void StopAllSfx() => _inner.StopAllSfx();

        public void StopSfx(int sfxId)
        {
            // the HEAD before g5c: nothing reaches the player
        }
    }

    internal static string ProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "alundra-project", "Sounds")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "alundra-project");
    }

    /// <summary>The real player over the real manifest; the clip of the record 25 (tone 0, as exported) is registered on the fake provider.</summary>
    internal static (SoundPlayer Player, FakeAudioBackend Backend, AudioService Service) RealPlayer(object owner)
    {
        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.Register(Guid.Parse("3f12a63d-5824-5103-9141-57002bbffb7b"), new FakeAudioClip("sfx_0025", 10401));
        var service = new AudioService(backend) { ClipProvider = provider };
        return (new SoundPlayer(service, new SfxBank(ProjectPath()), owner), backend, service);
    }

    internal static void Install(WorldProxy proxy, ISoundPlayer player)
        => typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(proxy, player);
}
