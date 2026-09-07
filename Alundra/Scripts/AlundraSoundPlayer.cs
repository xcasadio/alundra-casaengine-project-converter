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

    /// <summary>
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-6): backs opcodes 0xAB/0xBF - remixes the volume/pan of
    /// every voice ALREADY PLAYING for <paramref name="sfxId"/> from a stereo mix
    /// (<paramref name="left"/>/<paramref name="right"/> channel gains); NEVER starts new playback (fact
    /// 4 - a non-audible/silent id is a total no-op). See <see cref="AlundraSoundPlayer"/>'s own doc for
    /// the exact (volume, pan) projection.
    /// </summary>
    void RemixVoice(int sfxId, int left, int right);

    /// <summary>
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-4): clears this player's per-frame anti-duplicate table
    /// (fact 5) - MUST be called exactly once per RENDERED frame, from the world proxy's own frame-close
    /// site (next to <c>AlundraLogicClock.CloseFrame</c>), never from inside a dispatch pass. A degraded
    /// recorder fake with no table of its own (e.g. the intro trace harness) may leave this a no-op.
    /// </summary>
    void FlushFrameSounds();
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
    private readonly object _owner;
    private readonly Dictionary<int, List<AudioVoiceHandle>> _liveVoicesBySfxId = new();

    /// <summary>
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-4, fact 5): port of the original's 64-slot
    /// <c>INT_ARRAY_80165028</c> anti-duplicate table - "which sfx ids have already been requested THIS
    /// RENDERED FRAME". Consulted by <see cref="PlaySfx"/> for every caller, exactly like the original's
    /// own <c>IsSoundEffectAlreadyPlaying</c>/<c>PlaySoundEffectCore</c>. Owned by THIS player (one per
    /// world - the closest equivalent this DLL has to a map-entry reset), flushed by
    /// <see cref="FlushFrameSounds"/> from the world proxy's own frame-close site (D-B-4's own deviation,
    /// proven and pinned by AlundraWorldProxy's own dispatch-cadence tests).
    /// </summary>
    private const int FrameAntiDuplicateSlotCount = 64;
    private readonly int[] _frameSoundIds = new int[FrameAntiDuplicateSlotCount];
    private int _frameSoundCount;

    /// <summary>
    /// <paramref name="owner"/> is passed straight through to every <see cref="AudioService.PlayClip"/>
    /// call this instance makes (docs/plan-e11c-musique.md, slice C1, D-C-5): production hands it the
    /// owning <c>World</c> (<see cref="AlundraWorldProxy.InstallAudioSystems"/>) so
    /// <c>World.Clear</c>'s own <c>StopVoicesOwnedBy(world)</c> actually stops these voices - the fix
    /// for fact 1.7's real defect (this class used to pass <c>owner: this</c>, and a fresh instance is
    /// built per world, so <see cref="CasaEngine.Framework.Audio.AudioService.StopVoicesOwnedBy"/> could
    /// never match it by <c>ReferenceEquals</c>). A sound effect has no reason to outlive its world, so
    /// unlike <see cref="AlundraMusicPlayer"/> (session-owned, D-C-5's other half) this one always
    /// receives the world.
    /// </summary>
    public AlundraSoundPlayer(AudioService audioService, AlundraSoundBank soundBank, object owner)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _soundBank = soundBank ?? throw new ArgumentNullException(nameof(soundBank));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void PlaySfx(int sfxId)
    {
        if (!TryRegisterForThisFrame(sfxId))
        {
            return;
        }

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
            var handle = _audioService.PlayClip(clip, AudioBusNames.Sfx, parameters, owner: _owner);
            if (handle.IsValid)
            {
                liveVoices.Add(handle);
            }
        }
    }

    /// <summary>
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-6, fact 4): backs opcodes 0xAB/0xBF. The original resolves
    /// the fiche for the current sound group and remixes each TONE's own already-playing voice
    /// individually (<c>FindVoiceBySfxIdAndToneIndex</c>); this seam is never handed a group (same
    /// deviation as <see cref="PlaySfx"/> - D-E11-6), so it simply remixes every voice this player is
    /// still tracking as live for the REQUESTED id - a no-op (zero backend calls) when none are.
    ///
    /// Deviation (fact 11, D-B-1 oracle simulé): the original recomputes independent left/right gains
    /// from MIPS magic constants (SoundManager.cs:5276-5298) this engine's mono volume+pan voice model
    /// cannot reproduce. Projected instead onto (volume, pan) via <see cref="ProjectMixToVolumeAndPan"/>,
    /// applied through <see cref="AudioService.SetVoiceVolume"/>/<see cref="AudioService.SetVoicePan"/> -
    /// both already bus-gain-safe (see <see cref="AudioService.SetVoicePan"/>'s own doc on the trap a raw
    /// <c>SetParameters</c> push would fall into), so neither call clobbers the other's gain handling.
    /// </summary>
    public void RemixVoice(int sfxId, int left, int right)
    {
        if (!_liveVoicesBySfxId.TryGetValue(sfxId, out var liveVoices) || liveVoices.Count == 0)
        {
            return;
        }

        PruneFinishedVoices(liveVoices);
        if (liveVoices.Count == 0)
        {
            return;
        }

        var (volume, pan) = ProjectMixToVolumeAndPan(left, right);

        foreach (var voice in liveVoices)
        {
            _audioService.SetVoiceVolume(voice, volume);
            _audioService.SetVoicePan(voice, pan);
        }
    }

    /// <inheritdoc cref="IAlundraSoundPlayer.FlushFrameSounds"/>
    public void FlushFrameSounds()
    {
        _frameSoundCount = 0;
    }

    /// <summary>Port of <c>IsSoundEffectAlreadyPlaying</c> (fact 5): an id already registered this frame
    /// is refused; the first 64 DISTINCT ids seen this frame are registered and allowed; a 65th distinct
    /// id, with the table already full, is allowed WITHOUT being registered - the original's own
    /// documented overflow behaviour (duplicates are permitted once the table saturates).</summary>
    private bool TryRegisterForThisFrame(int sfxId)
    {
        for (var i = 0; i < _frameSoundCount; i++)
        {
            if (_frameSoundIds[i] == sfxId)
            {
                return false;
            }
        }

        if (_frameSoundCount < _frameSoundIds.Length)
        {
            _frameSoundIds[_frameSoundCount++] = sfxId;
        }

        return true;
    }

    private static (float Volume, float Pan) ProjectMixToVolumeAndPan(int left, int right)
    {
        // 0x7f (127) is this DLL's own "full" reference for these byte-range operands (fact 1:
        // StopAllSound restores the master/sequencer volumes to 0x7f).
        const float fullScale = 127f;
        var clampedLeft = Math.Clamp(left, 0, 127);
        var clampedRight = Math.Clamp(right, 0, 127);

        var volume = Math.Clamp(Math.Max(clampedLeft, clampedRight) / fullScale, 0f, 1f);

        var sum = clampedLeft + clampedRight;
        var pan = sum > 0
            ? Math.Clamp((clampedRight - clampedLeft) / (float)sum, -1f, 1f)
            : 0f;

        return (volume, pan);
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
