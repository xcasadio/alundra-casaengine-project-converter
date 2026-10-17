#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Alundra.Events;
using Alundra.Cells;

namespace Alundra.Audio;

/// <summary>
/// This world's sound-effect playback seam (docs/plan-e11-audio.md, slice E11.a, D-E11-1) - backs
/// opcodes 0xBD/0xBE/0x12/0x75 in <see cref="EventProgramRunner.Dispatch"/>. Same precedent as
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
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-6), T4.3 (docs/plan-audio-mix-exact-muet.md): backs
    /// opcodes 0xAB/0xBF - remixes the stereo gains of the OLDEST voice ALREADY PLAYING for each tone of
    /// <paramref name="sfxId"/>'s resolved record, from the original's own SPU volume computation
    /// (<see cref="AlundraSpuVoiceVolume.ComputeRemix"/>) over <paramref name="left"/>/<paramref name="right"/>
    /// (the opcode's own mix operands); NEVER starts new playback (fact 4 - a non-audible/silent id is a
    /// total no-op). See <see cref="AlundraSoundPlayer"/>'s own doc for the exact rules.
    /// </summary>
    void RemixVoice(int sfxId, int left, int right);

    /// <summary>
    /// B1 (docs/plan-e11b-opcodes-audio.md, D-B-4): clears this player's per-frame anti-duplicate table
    /// (fact 5) - MUST be called exactly once per RENDERED frame, from the world proxy's own frame-close
    /// site (next to <c>AlundraLogicClock.CloseFrame</c>), never from inside a dispatch pass. A degraded
    /// recorder fake with no table of its own (e.g. the intro trace harness) may leave this a no-op.
    /// </summary>
    void FlushFrameSounds();

    /// <summary>
    /// B2 (docs/plan-e11b-opcodes-audio.md, D-B-6): the SFX half of opcode 0xA5
    /// (<see cref="AlundraBgmFadeDirector.StopAllSound"/>) and of the master fade machine's own
    /// swap-tick key-off (fact 2's own "key-off all 24 voices") - stops every voice this player still
    /// tracks as live, across every sfx id, and forgets them, so a fresh <see cref="PlaySfx"/> for the
    /// SAME id is free to start again immediately. Never touches the per-frame anti-duplicate table
    /// (<see cref="FlushFrameSounds"/>'s own concern, fact 5 - a distinct mechanism from this one).
    /// </summary>
    void StopAllSfx();
}
