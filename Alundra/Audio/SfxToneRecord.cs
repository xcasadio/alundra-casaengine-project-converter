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
/// One tone of a <see cref="SfxResolution"/> - a single voice's worth of playback data, straight off
/// one <c>Sounds/sfx-manifest.json</c> record's own <c>"tones"</c> array entry (docs/plan-e11-audio.md,
/// slice E11.a, fact 1.2: the manifest already expands per-tone entries, one WAV per tone, at the
/// TRANSPOSED sample rate the original's own <c>CalculateVoicePitch</c>/extractor's
/// <c>CalculateToneSampleRate</c> agree on - so playing each tone flat, at its own header rate, is
/// correct; see the plan's fact 1.3 for why a second transposition would be wrong).
/// </summary>
public readonly struct SfxToneRecord
{
    public int ToneIndex { get; init; }
    public string File { get; init; }
    public int SampleRate { get; init; }
    public int LoopStart { get; init; }
    public int LoopEnd { get; init; }
    public bool Repeat { get; init; }
    public Guid AssetId { get; init; }

    /// <summary>
    /// <c>VagAtr.Vol</c>/<c>Pan</c> of this tone (docs/plan-audio-mix-exact-muet.md, ADR-0003), read by the
    /// original's key-on (<c>FUN_80090C58</c>) and remix (<c>0x80049794</c>) volume paths. Null when the
    /// manifest does not carry them - never 0, since a silent tone (volume 0) exists.
    /// </summary>
    public int? Volume { get; init; }
    public int? Pan { get; init; }
}
