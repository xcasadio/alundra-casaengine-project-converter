#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;

namespace Alundra.Scripts;

/// <summary>
/// This world's sound-effect playback seam (docs/plan-e11-audio.md, slice E11.a, D-E11-1) - backs
/// opcodes 0xBD/0xBE/0x12/0x75 in <see cref="AlundraEventProgramRunner.Dispatch"/>. Same precedent as
/// <see cref="IAlundraCellMutator"/>: the vocabulary is Alundra's own (<c>int sfxId</c>), not
/// <see cref="AudioService"/>'s, because <see cref="AudioService"/> is <c>sealed</c> and unusable as a
/// fake in a synthetic interpreter test, and the opcodes themselves only ever carry a plain sfx id.
/// </summary>
public interface IAlundraSoundPlayer
{
    /// <summary>Requests playback of sound effect <paramref name="sfxId"/> (Alundra's own id space, the
    /// manifest's own <c>"id"</c> field) - a no-op when the id has no resolvable tones (see
    /// <see cref="AlundraSoundBank.TryResolve"/>) or its <c>MaxVoices</c> cap is currently full (see
    /// <see cref="AlundraSoundPlayer"/>'s own doc).</summary>
    void PlaySfx(int sfxId);
}

/// <summary>
/// Implements <see cref="IAlundraSoundPlayer"/> on top of a real <see cref="AudioService"/>
/// (docs/plan-e11-audio.md, slice E11.a, D-E11-2: no <c>.sound</c> asset, no convertor change - the
/// exported <c>.wav</c> files already load as <see cref="IAudioClip"/> through
/// <see cref="AudioService.ClipProvider"/>, so this seam resolves a clip straight from its manifest
/// guid and calls the PUBLIC <see cref="AudioService.PlayClip"/>, bypassing <see cref="SoundAsset"/>
/// entirely - that type fixes pan/parameters per asset, which would cost us the per-call control the
/// original's own per-tone playback needs).
/// </summary>
/// <remarks>
/// Fidelity (fact 1.3, D-E11-4): one voice PER TONE of the resolved record, played simultaneously, each
/// flat at its own tone's header sample rate (the export already applied the original's transposition -
/// transposing again here would be wrong). No group is ever passed to
/// <see cref="AlundraSoundBank.TryResolve"/> here (D-E11-6: no production caller has one to give).
///
/// The polyphony cap (<c>MaxVoices</c>) is tested BEFORE the per-tone loop, exactly like the original
/// (fact 1.3: "<c>MaxVoices</c> is not a selector: it's a polyphony ceiling tested before the loop") -
/// live voices are tracked per REQUESTED sfx id (this seam is never handed a group, so the requested id
/// and the resolved id are always the same one in E11.a) and pruned of anything the backend already
/// finished, so a full cap releases again once its voices end.
///
/// Deliberately NOT ported here (D-E11-4): the original's per-audio-frame anti-duplicate filter
/// (<c>IsSoundEffectAlreadyPlaying</c>) - it needs a frame owner this seam is never given, and it is
/// provably inert on map 389 (no id is dispatched twice in the same frame there). See the plan's own
/// "Déviation assumée n°1/n°2" for volume/pan (unit/centered, no per-tone data survives the export) and
/// the son 302 loop point (the whole buffer loops, not the true 1820..28055 window - the engine has no
/// loop-point support at the <c>SoundEffectInstance</c> level).
/// </remarks>
public sealed class AlundraSoundPlayer : IAlundraSoundPlayer
{
    private readonly AudioService _audioService;
    private readonly AlundraSoundBank _soundBank;
    private readonly Dictionary<int, List<AudioVoiceHandle>> _liveVoicesBySfxId = new();

    public AlundraSoundPlayer(AudioService audioService, AlundraSoundBank soundBank)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _soundBank = soundBank ?? throw new ArgumentNullException(nameof(soundBank));
    }

    public void PlaySfx(int sfxId)
    {
        if (!_soundBank.TryResolve(sfxId, soundGroup: null, out var resolution))
        {
            return;
        }

        var liveVoices = GetLiveVoices(sfxId);
        PruneFinishedVoices(liveVoices);

        if (liveVoices.Count >= resolution.MaxVoices)
        {
            return;
        }

        var clipProvider = _audioService.ClipProvider;
        if (clipProvider == null)
        {
            return;
        }

        foreach (var tone in resolution.Tones)
        {
            var clip = clipProvider.GetClip(tone.AssetId);
            if (clip == null)
            {
                continue;
            }

            var parameters = new AudioVoiceParameters(
                AudioVoiceParameters.MaxVolume, 0f, 0f, tone.Repeat);
            var handle = _audioService.PlayClip(clip, AudioBusNames.Sfx, parameters, owner: this);
            if (handle.IsValid)
            {
                liveVoices.Add(handle);
            }
        }
    }

    private List<AudioVoiceHandle> GetLiveVoices(int sfxId)
    {
        if (!_liveVoicesBySfxId.TryGetValue(sfxId, out var voices))
        {
            voices = new List<AudioVoiceHandle>();
            _liveVoicesBySfxId[sfxId] = voices;
        }

        return voices;
    }

    private void PruneFinishedVoices(List<AudioVoiceHandle> voices)
    {
        for (var i = voices.Count - 1; i >= 0; i--)
        {
            if (!_audioService.IsAlive(voices[i]))
            {
                voices.RemoveAt(i);
            }
        }
    }
}
