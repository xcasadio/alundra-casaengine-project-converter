#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Audio;

namespace Alundra.Tests;

/// <summary>
/// T5 of docs/plan-e11-audio.md, slice E11.a - the "livrable de test non optionnel" (§4 item 3/§2.2b):
/// <see cref="IAudioBackend"/>/<see cref="IAudioClip"/>/<see cref="IAudioClipProvider"/> are PUBLIC engine
/// interfaces, but the engine's own fakes for them live in <c>CasaEngine.Tests</c>
/// (CasaEngineMonogame\CasaEngine.Tests\Audio\FakeAudioBackend.cs), which this project does not
/// reference - so they are re-implemented here, in the same shape, for a real
/// <see cref="AudioService"/>(this backend) { ClipProvider = &lt;the provider below&gt; } to be built
/// against in <see cref="AlundraSoundPlayerTests"/>, exactly the form the engine's own
/// AudioServicePlaySoundTests.cs uses.
/// </summary>
public sealed class FakeAudioBackend : IAudioBackend
{
    private readonly List<Slot> _slots = new();

    public FakeAudioBackend(int voiceCapacity = 8)
    {
        VoiceCapacity = voiceCapacity;
        IsAvailable = true;
    }

    public bool IsAvailable { get; set; }

    public int VoiceCapacity { get; set; }

    public int ActiveVoiceCount
    {
        get
        {
            var count = 0;
            foreach (var slot in _slots)
            {
                if (slot.InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsDisposed { get; private set; }

    /// <summary>Every successful <see cref="Play"/> call, in order - T5's own oracle for "which clips
    /// were actually started, with what parameters" (a plain <see cref="AudioService.ActiveVoiceCount"/>
    /// cannot tell two different clips apart).</summary>
    public List<(IAudioClip Clip, AudioVoiceParameters Parameters)> PlayCalls { get; } = new();

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (IsDisposed || !IsAvailable || ActiveVoiceCount >= VoiceCapacity)
        {
            return AudioVoiceHandle.None;
        }

        var index = FindFreeSlot();
        if (index < 0)
        {
            _slots.Add(new Slot());
            index = _slots.Count - 1;
        }

        var slot = _slots[index];
        slot.InUse = true;
        slot.Clip = clip;
        slot.Parameters = parameters;
        slot.State = AudioVoiceState.Playing;

        PlayCalls.Add((clip, parameters));
        return new AudioVoiceHandle(index, slot.Generation);
    }

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.Parameters = parameters;
        }
    }

    public void SetVolume(AudioVoiceHandle voice, float volume)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.Parameters = slot.Parameters.WithVolume(volume);
        }
    }

    public AudioVoiceState GetState(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.State : AudioVoiceState.Stopped;
    }

    public void Pause(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Playing)
        {
            slot.State = AudioVoiceState.Paused;
        }
    }

    public void Resume(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Paused)
        {
            slot.State = AudioVoiceState.Playing;
        }
    }

    public void Stop(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.State = AudioVoiceState.Stopped;
        }
    }

    public void Release(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        slot.State = AudioVoiceState.Stopped;
        slot.InUse = false;
        slot.Clip = null;
        slot.Generation++;
    }

    public void StopAll()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.InUse)
            {
                continue;
            }

            slot.State = AudioVoiceState.Stopped;
            slot.InUse = false;
            slot.Clip = null;
            slot.Generation++;
        }
    }

    public bool SupportsStreaming { get; set; } = true;

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
    {
        if (IsDisposed || !IsAvailable || !SupportsStreaming || ActiveVoiceCount >= VoiceCapacity)
        {
            return AudioVoiceHandle.None;
        }

        var index = FindFreeSlot();
        if (index < 0)
        {
            _slots.Add(new Slot());
            index = _slots.Count - 1;
        }

        var slot = _slots[index];
        slot.InUse = true;
        slot.Clip = null;
        slot.Parameters = parameters;
        slot.State = AudioVoiceState.Stopped;

        return new AudioVoiceHandle(index, slot.Generation);
    }

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count)
    {
        // Not exercised by T5 (no streaming voice ever created through AlundraSoundPlayer) - present
        // only so this class satisfies IAudioBackend in full.
    }

    public int GetPendingBufferCount(AudioVoiceHandle voice) => 0;

    public void Start(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State != AudioVoiceState.Playing)
        {
            slot.State = AudioVoiceState.Playing;
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        StopAll();
    }

    // ---- test helpers -------------------------------------------------------

    /// <summary>Simulates a voice reaching the end of its clip, without releasing its slot - exactly
    /// what a real backend does when playback finishes; <see cref="AudioService.IsAlive"/> keeps
    /// returning true until <see cref="AudioService.Update"/> (or an explicit Stop/Release) recycles it,
    /// but <see cref="AudioService.IsPlaying"/> flips false immediately.</summary>
    public void CompleteVoice(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.State = AudioVoiceState.Stopped;
        }
    }

    /// <summary>Simulates every currently playing voice reaching the end of its clip at once - paired
    /// with a following <see cref="AudioService.Update"/> call, this is what actually frees
    /// <see cref="AudioService.IsAlive"/> for T5's MaxVoices-releases-again assertion (the backend
    /// reaching Stopped is necessary but not sufficient - only <see cref="AudioService.Update"/> recycles
    /// the entry).</summary>
    public void CompleteAllVoices()
    {
        foreach (var slot in _slots)
        {
            if (slot.InUse)
            {
                slot.State = AudioVoiceState.Stopped;
            }
        }
    }

    public AudioVoiceParameters GetParameters(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.Parameters : default;
    }

    public IAudioClip GetClip(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.Clip : null;
    }

    // ------------------------------------------------------------------------

    private int FindFreeSlot()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].InUse)
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryGetSlot(AudioVoiceHandle voice, out Slot slot)
    {
        slot = null;

        if (!voice.IsValid || voice.Index >= _slots.Count)
        {
            return false;
        }

        var candidate = _slots[voice.Index];
        if (!candidate.InUse || candidate.Generation != voice.Generation)
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    private sealed class Slot
    {
        public bool InUse;
        public int Generation;
        public IAudioClip Clip;
        public AudioVoiceParameters Parameters;
        public AudioVoiceState State;
    }
}

/// <summary>Deterministic <see cref="IAudioClip"/> for T5: no device, no decoding - see
/// <see cref="FakeAudioBackend"/>'s own doc for why this is re-implemented in this project.</summary>
public sealed class FakeAudioClip : IAudioClip
{
    public FakeAudioClip(string name = "clip", int sampleRate = 44100)
    {
        Name = name;
        SampleRate = sampleRate;
    }

    public string Name { get; }

    public int SampleRate { get; }

    public int ChannelCount => 1;

    public TimeSpan Duration => TimeSpan.FromSeconds(1);

    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;

    public override string ToString() => Name;
}

/// <summary>In-memory <see cref="IAudioClipProvider"/> for T5, keyed by the SAME asset guids the
/// synthetic manifest fixture carries (so <see cref="AlundraSoundPlayer"/>'s real
/// <see cref="AlundraSoundBank.TryResolve"/> lookup and this provider's clips line up) - see
/// <see cref="FakeAudioBackend"/>'s own doc.</summary>
public sealed class FakeAudioClipProvider : IAudioClipProvider
{
    private readonly Dictionary<Guid, IAudioClip> _clips = new();

    public void Register(Guid assetId, IAudioClip clip) => _clips[assetId] = clip;

    public IAudioClip GetClip(Guid audioFileAssetId) => _clips.GetValueOrDefault(audioFileAssetId);

    public Stream OpenStream(Guid audioFileAssetId) => null;
}
