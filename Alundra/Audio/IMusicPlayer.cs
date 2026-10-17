#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Alundra.Scripts;

namespace Alundra.Audio;

/// <summary>
/// This session's background-music playback seam (docs/plan-bgm-demarrage-binaire.md, T2.1) - the
/// equivalent of the original's <c>LoadMapSounds</c>/<c>LoadMapSequence</c> pair for BGM, called once
/// per map entry (see <see cref="AlundraWorldProxy.InstallAudioSystems"/>, item 4 of the plan's own
/// contract). Same shape as <see cref="IAlundraSoundPlayer"/>: the vocabulary is a plain map id, not
/// an <see cref="AudioService"/> one, so a fake can stand in for a test.
///
/// <para>Models the executable's own sequence state (B4-B8): a sequence is CHARGÉE (an index) or none
/// (closed), and, independently, PLAYS or not (a live voice) - loading never plays (B6/B7). The two
/// session fields below are the direct ports of the executable's own globals:
/// <see cref="CurrentMapSoundIndex"/> (<c>g_currentMapSoundIndex</c>, RAW, never remapped, B7's own
/// <c>0x80049cf4</c>) and <see cref="ResetSoundFlag"/> (<c>g_resetSoundFlag</c>, B8), consumed at the
/// frame-close site (<see cref="AlundraWorldProxy.Update"/>, P3) by <see cref="AlundraBgmFadeDirector.StopAllSound"/>.</para>
/// </summary>
public interface IAlundraMusicPlayer
{
    /// <summary>Port of <c>LoadMapSounds</c>/<c>LoadMapSequenceCore</c> (B10/B7) - a no-op when the
    /// table has no entry, when the raw value is <c>0</c>, or when it is the SAME raw value as
    /// <see cref="CurrentMapSoundIndex"/> (fact 1.1's guard). Otherwise stops and closes whatever is
    /// loaded; when the raw value is not <c>45</c> (stop), loads the track (<c>-1</c> remaps to track 1,
    /// B7), sets <see cref="CurrentMapSoundIndex"/> to the RAW value, and arms <see cref="ResetSoundFlag"/>.
    /// NEVER starts a voice by itself (B6/B7): the frame-close site's own <see cref="AlundraBgmFadeDirector.StopAllSound"/>
    /// call does that, once, at the end of the entry's first frame (P3).</summary>
    void PlayMapMusic(int mapId);

    /// <summary>Stops whatever this session's music voice currently is, if any, and clears every piece
    /// of session state: <see cref="CurrentMapSoundIndex"/> back to <c>0</c>, the loaded track closed,
    /// <see cref="ResetSoundFlag"/> cleared (nothing left to consume at the next frame close), and any
    /// warp departure <see cref="HandleWarpDeparture"/> had recorded but not yet evaluated, dropped.
    /// Not called by production code (nothing in this DLL needs an unconditional full reset outside
    /// <see cref="PlayMapMusic"/>'s own paths) - exposed for symmetry with <see cref="IAlundraSoundPlayer"/>
    /// and so a test can force silence between assertions.</summary>
    void StopMusic();

    /// <summary>
    /// B13 (docs/plan-bgm-demarrage-binaire.md, D8): backs opcode 0xA7's own raw-index semantics -
    /// DELIBERATELY NOT <see cref="PlayMapMusic"/>'s own table-driven guard/remap
    /// (<see cref="AlundraMusicIndexTable.ResolvePlaybackDirective"/> is never called here, per the
    /// plan's explicit "do not reuse" - the per-map table's own -1/45 remap has no equivalent in this
    /// raw opcode). <paramref name="rawIndex"/> &lt; 0 is ignored outright (unreachable in practice -
    /// 0xA7's own operand is an unsigned byte); <c>0</c> stops and closes the old track, leaving
    /// <see cref="ResetSoundFlag"/> untouched; &gt; 0 stops and closes the old track, loads the new one,
    /// sets <see cref="CurrentMapSoundIndex"/> to <paramref name="rawIndex"/> and clears
    /// <see cref="ResetSoundFlag"/> (P8 - the streaming path's own reset-flag clear, B13's second
    /// branch). NEVER starts a voice (B13: "Ni PlaySeq, ni SetSeqVolume" - the chargement is instant in
    /// this port, but still silent, exactly like the original's own load-only semantics).
    /// </summary>
    void PlayFromRawIndex(int rawIndex);

    /// <summary>
    /// B5 (docs/plan-bgm-demarrage-binaire.md): port of the original's own <c>InitializeBgm</c> - an
    /// ARRÊT, never a relaunch. Stops the current voice, if any; the loaded track (if any) stays loaded,
    /// rewound. Called by <c>LoadBgm(0)</c> and the master fade machine's own swap tick
    /// (<see cref="AlundraBgmFadeDirector"/>).
    /// </summary>
    void StopSequence();

    /// <summary>
    /// B4 (docs/plan-bgm-demarrage-binaire.md): port of the original's own <c>PlaySeq</c>. A live voice
    /// is left alone (no restart); a loaded-but-silent track starts a fresh voice from the top; no track
    /// loaded is a no-op. The ONLY method in this seam that can ever start a voice - called by
    /// <see cref="AlundraBgmFadeDirector.StopAllSound"/>, the executable's own sole BGM-playing call site
    /// (B1/B2).
    /// </summary>
    void PlaySequence();

    /// <summary>Port of <c>g_currentMapSoundIndex</c> (B7's own <c>0x80049cf4</c>) - the RAW value
    /// <see cref="PlayMapMusic"/>/<see cref="PlayFromRawIndex"/> last wrote, NEVER remapped (a
    /// <c>-1</c> map stays <c>-1</c> here even though its loaded track is 1). <c>0</c> initially.</summary>
    int CurrentMapSoundIndex { get; }

    /// <summary>Port of <c>g_resetSoundFlag</c> (B8) - armed by <see cref="PlayMapMusic"/> (B7's own
    /// <c>0x80049d10</c>), consumed at the frame-close site (P3) right before
    /// <c>SoundPlayer.FlushFrameSounds</c>, which calls <see cref="AlundraBgmFadeDirector.StopAllSound"/>
    /// when set, then <see cref="ClearResetSoundFlag"/>.</summary>
    bool ResetSoundFlag { get; }

    /// <summary>Clears <see cref="ResetSoundFlag"/> - B8's own writers (<c>LoadBgm</c>, the frame-close
    /// consumer, and, additively, <see cref="PlayFromRawIndex"/>'s own positive branch, P8).</summary>
    void ClearResetSoundFlag();
}
