#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Entities;
using Alundra.Scripts;

namespace Alundra.Audio;

/// <summary>
/// Loads <c>Sounds/sfx-manifest.json</c> once (from <see cref="EngineEnvironment.ProjectPath"/>, the
/// same project-root resolution <see cref="SpriteRecordCatalog"/> already uses - see that class's own
/// doc) and answers <see cref="TryResolve"/> - the port of <c>TryResolveSoundEffectRecord</c>
/// (docs/plan-e11-audio.md, slice E11.a, fact 1.4): <c>VabId == -2</c> is a disabled sound (reject);
/// <c>VabId == -1</c> is the global VAB (always playable, own tones); <c>VabId == soundGroup</c> is the
/// current map's own VAB (own tones); otherwise, when a <paramref name="soundGroup"/> WAS supplied, the
/// <c>RefSfxId</c> chain is followed looking for a sibling of the right group, giving up when
/// <c>RefSfxId == 0</c>. <paramref name="soundGroup"/> is OPTIONAL (D-E11-6, plan §1.4/§3): no
/// production caller in E11.a supplies one (the map -&gt; VAB-group table is not exported - see the
/// plan's own "trou" in fact 1.4), so a record with <c>VabId &gt;= 0</c> plays its OWN tones instead of
/// being redirected, deviation assumed n°3 there.
///
/// Degraded mode: missing/unreadable/unparsable manifest logs one warning at construction and then
/// every <see cref="TryResolve"/> call returns false - same shape as <see cref="SpriteRecordCatalog"/>'s
/// own degraded mode.
/// </summary>
public sealed class AlundraSoundBank
{
    private const string DataDirectoryName = "Sounds";
    private const string FileName = "sfx-manifest.json";

    private readonly Dictionary<int, ManifestRecord> _recordsById = new();

    /// <summary>Loads from <c>Sounds/sfx-manifest.json</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraSoundBank() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads from <c>Sounds/sfx-manifest.json</c> under <paramref name="projectPath"/> - the
    /// overload tests use to point at a temporary fixture directory instead of the real project.</summary>
    public AlundraSoundBank(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"AlundraSoundBank: '{filePath}' not found; every sound effect resolves to nothing "
                    + "(degraded mode).");
                return;
            }

            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<List<ManifestRecord>>(json, SerializerOptions);

            if (parsed == null)
            {
                Logs.WriteWarning(
                    $"AlundraSoundBank: '{filePath}' parsed to nothing; every sound effect resolves to "
                    + "nothing (degraded mode).");
                return;
            }

            foreach (var record in parsed)
            {
                _recordsById[record.Id] = record;
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraSoundBank: failed to load '{filePath}' ({ex.Message}); every sound effect "
                + "resolves to nothing (degraded mode).");
            _recordsById.Clear();
        }
    }

    /// <summary>
    /// Port of <c>TryResolveSoundEffectRecord</c> (see this class's own doc for the exact rules).
    /// Fails softly (returns false, no exception, no log) for an unknown id, a disabled record
    /// (<c>VabId == -2</c>), a record with no tones (the manifest's 91 <c>skip_reason</c> entries - fact
    /// 1.2), or a <c>RefSfxId</c> chain that runs out (<c>RefSfxId == 0</c>) before finding a sibling of
    /// the requested group - every one of these is a normal "this id is not playable right now" outcome
    /// on the real corpus, not a data error.
    /// </summary>
    public bool TryResolve(int sfxId, int? soundGroup, out SfxResolution resolution)
    {
        resolution = default;

        if (!_recordsById.TryGetValue(sfxId, out var record))
        {
            return false;
        }

        if (record.VabId == -2)
        {
            return false;
        }

        // B3 (D-B-7, fact 7 corrected): captured BEFORE the RefSfxId chain below may redirect to a
        // different record - see SfxResolution.RequestedVabId's own doc.
        var requestedVabId = record.VabId;

        if (soundGroup is { } group && record.VabId != -1 && record.VabId != group)
        {
            // Follow the RefSfxId chain looking for a sibling of the requested group - abandon the
            // moment the chain runs out (RefSfxId == 0), exactly like the original (fact 1.4).
            var current = record;
            while (true)
            {
                if (current.RefSfxId == 0)
                {
                    return false;
                }

                if (!_recordsById.TryGetValue(current.RefSfxId, out var next))
                {
                    return false;
                }

                current = next;

                if (current.VabId == -1 || current.VabId == group)
                {
                    break;
                }
            }

            record = current;
        }

        if (record.Tones == null || record.Tones.Count == 0)
        {
            return false;
        }

        var tones = new SfxToneRecord[record.Tones.Count];
        for (var i = 0; i < record.Tones.Count; i++)
        {
            var tone = record.Tones[i];
            if (!Guid.TryParse(tone.AssetId, out var assetGuid))
            {
                return false;
            }

            tones[i] = new SfxToneRecord
            {
                ToneIndex = tone.ToneIndex,
                File = tone.File ?? "",
                SampleRate = tone.SampleRate,
                LoopStart = tone.LoopStart,
                LoopEnd = tone.LoopEnd,
                Repeat = tone.Repeat,
                AssetId = assetGuid,
                Volume = tone.Volume,
                Pan = tone.Pan,
            };
        }

        resolution = new SfxResolution
        {
            ResolvedId = record.Id,
            VabId = record.VabId,
            RequestedVabId = requestedVabId,
            MaxVoices = record.MaxVoices,
            Tones = tones,
            VabMasterVolume = record.VabMasterVolume,
            ProgramVolume = record.ProgramVolume,
            ProgramPan = record.ProgramPan,
        };
        return true;
    }

    /// <summary>
    /// Session-scoped cache keyed by project path (D-T-14, docs/plan-transitions-carte.md, slice T1) -
    /// same shape and rationale as <see cref="SpriteRecordCatalog"/>'s own cache: <see cref="AlundraWorldProxy"/>'s
    /// field initializer resolves through here so two consecutive worlds over the SAME project read
    /// <c>Sounds/sfx-manifest.json</c> only once, while two DIFFERENT projects never share a cached bank.
    /// </summary>
    private static readonly Dictionary<string, AlundraSoundBank> SessionCacheByProjectPath = new();

    /// <summary>Returns the cached bank for <paramref name="projectPath"/>, loading it once on the first
    /// request and reusing it for every later one - see <see cref="SessionCacheByProjectPath"/>'s own doc.</summary>
    public static AlundraSoundBank GetOrCreate(string projectPath)
    {
        if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var bank))
        {
            bank = new AlundraSoundBank(projectPath);
            SessionCacheByProjectPath[projectPath] = bank;
        }

        return bank;
    }

    /// <summary>Test-only: clears the session cache so tests do not leak a bank loaded by one test into
    /// another through <see cref="GetOrCreate"/> - same seam as <see cref="SpriteRecordCatalog.ResetForTests"/>.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();

    /// <summary>
    /// B17 (docs/plan-bgm-demarrage-binaire.md, D8/P7): the raw <c>SeqNum</c>/<c>MaxVoices</c> off the
    /// record itself - NOT run through <see cref="TryResolve"/>'s own <c>RefSfxId</c> chain or tone
    /// requirements (a manifest record can have zero tones and still be a valid "is this warp sound
    /// silent" test subject - id 69, warp behaviour 1's own sfx, is exactly such a record). Read
    /// directly off <c>Sounds/sfx-manifest.json</c>'s own <c>seq_num</c> field (table <c>0x800a82e8</c>
    /// in the original, the warp-departure sound record - NOT the <c>0x16</c> table the analyser's own
    /// pre-T1.1 bug read instead). False for an id absent from the manifest.
    /// </summary>
    public bool TryGetSeqNumAndMaxVoices(int sfxId, out int seqNum, out int maxVoices)
    {
        if (_recordsById.TryGetValue(sfxId, out var record))
        {
            seqNum = record.SeqNum;
            maxVoices = record.MaxVoices;
            return true;
        }

        seqNum = 0;
        maxVoices = 0;
        return false;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new();

    // Field names match AudioWriter's own JSON contract exactly (snake_case - see that writer's doc).
    private sealed class ManifestRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("vab_id")] public int VabId { get; set; }
        [JsonPropertyName("ref_sfx_id")] public int RefSfxId { get; set; }
        [JsonPropertyName("max_voices")] public int MaxVoices { get; set; }

        /// <summary>B17 (docs/plan-bgm-demarrage-binaire.md): additive read of the manifest's own
        /// <c>seq_num</c> field - see <see cref="TryGetSeqNumAndMaxVoices"/>.</summary>
        [JsonPropertyName("seq_num")] public int SeqNum { get; set; }
        [JsonPropertyName("tones")] public List<ManifestTone>? Tones { get; set; }
        [JsonPropertyName("vab_master_volume")] public int? VabMasterVolume { get; set; }
        [JsonPropertyName("program_volume")] public int? ProgramVolume { get; set; }
        [JsonPropertyName("program_pan")] public int? ProgramPan { get; set; }
    }

    private sealed class ManifestTone
    {
        [JsonPropertyName("tone_index")] public int ToneIndex { get; set; }
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("sample_rate")] public int SampleRate { get; set; }
        [JsonPropertyName("loop_start")] public int LoopStart { get; set; }
        [JsonPropertyName("loop_end")] public int LoopEnd { get; set; }
        [JsonPropertyName("repeat")] public bool Repeat { get; set; }
        [JsonPropertyName("asset_id")] public string? AssetId { get; set; }
        [JsonPropertyName("volume")] public int? Volume { get; set; }
        [JsonPropertyName("pan")] public int? Pan { get; set; }
    }
}
