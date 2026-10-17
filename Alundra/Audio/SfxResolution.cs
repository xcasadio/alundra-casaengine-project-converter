#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Entities;

namespace Alundra.Audio;

/// <summary>
/// One playable sound effect, resolved by <see cref="AlundraSoundBank.TryResolve"/> - the record
/// actually chosen (which, when the <c>RefSfxId</c> chain fired, is a DIFFERENT id than the one asked
/// for - see that method's own doc), carrying every tone <see cref="AlundraSoundPlayer"/> needs to start
/// one voice per tone (fact 1.3: "several tones at once, not one").
/// </summary>
public readonly struct SfxResolution
{
    /// <summary>The id of the record actually resolved to - <see cref="AlundraSoundBank.TryResolve"/>'s
    /// own requested id when no <c>RefSfxId</c> redirection happened, or the chain sibling's id when it
    /// did.</summary>
    public int ResolvedId { get; init; }
    public int VabId { get; init; }

    /// <summary>
    /// B3 (docs/plan-e11b-opcodes-audio.md, D-B-7, fact 7 corrected): the VabId of the RECORD ACTUALLY
    /// REQUESTED (the <c>sfxId</c> parameter of <see cref="TryResolve"/>) - BEFORE any
    /// <c>RefSfxId</c> redirection, so it equals <see cref="VabId"/> exactly when no redirection fired
    /// and differs from it otherwise. <see cref="AlundraSoundPlayer"/>'s own polyphony ceiling filters
    /// by THIS value, not <see cref="VabId"/> (the resolved record's own) - the original's
    /// <c>CountActiveVoicesForSfx</c> indexes <c>g_soundEffectData</c> with the requested id and filters
    /// by that record's VabId (<c>SoundManager.cs:4025-4034/:4049</c>), while voices are registered with
    /// the RESOLVED record's VabId (<c>:3990-3995</c>) - so under redirection the filter never matches
    /// and the ceiling is inoperative, faithfully.
    /// </summary>
    public int RequestedVabId { get; init; }
    public int MaxVoices { get; init; }
    public IReadOnlyList<SfxToneRecord> Tones { get; init; }

    /// <summary>
    /// <c>VabHdr.Mvol</c> and <c>ProgAtr.Mvol</c>/<c>Mpan</c> of the RESOLVED record - the one whose
    /// <see cref="Tones"/> are played, i.e. the program the original hands to <c>TriggerVoice</c> and reads back
    /// in its remix (docs/plan-audio-mix-exact-muet.md, T4.1, ADR-0003). Null when the manifest does not carry them.
    /// </summary>
    public int? VabMasterVolume { get; init; }
    public int? ProgramVolume { get; init; }
    public int? ProgramPan { get; init; }
}
