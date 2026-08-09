using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// One background music track, as described by the extractor's sound/bgm.json. Every source field
/// is carried over verbatim; the analysis fields (Peak*/Rms*/FirstAudibleFrame/LoopDetected) are
/// measurements the extractor made on the decoded WAV, not values read from the game data, but
/// they are cheap to keep and a gameplay DLL may want them (e.g. to skip leading silence).
/// AssetId is the only field the converter adds: the catalog id of the copied WAV.
/// </summary>
public sealed class BgmEntry
{
    public int SoundIndex { get; set; }
    public string File { get; set; } = string.Empty;
    public int Frames { get; set; }
    public double DurationSeconds { get; set; }
    public bool LoopDetected { get; set; }
    public int PeakLeft { get; set; }
    public int PeakRight { get; set; }
    public double RmsLeft { get; set; }
    public double RmsRight { get; set; }
    public int FirstAudibleFrame { get; set; }
    public Guid AssetId { get; set; }
}

/// <summary>
/// One decodable sample of a sound effect. A single sfx record can hold several tones (the game
/// picks one by note), so the WAV count is larger than the record count. LoopStart/LoopEnd are
/// sample offsets from the VAG loop points - MonoGame's SoundEffect does not expose loop points at
/// all, so they exist here only to be preserved.
/// </summary>
public sealed class SfxTone
{
    public int ToneIndex { get; set; }
    public string File { get; set; } = string.Empty;
    public int SampleRate { get; set; }
    public int LoopStart { get; set; }
    public int LoopEnd { get; set; }
    public bool Repeat { get; set; }
    public Guid AssetId { get; set; }
}

/// <summary>
/// One sound effect record from sound/sfx.json. Fields keep their extractor names because their
/// meaning comes from the game's own VAB/SEQ structures (VabId, ProgramNumber, ToneNumber, Note,
/// SeqNum, RefSfxId, MaxVoices) and renaming them would only obscure the mapping back to the
/// source data. SkipReason is non-null on records the extractor could not decode into a sample -
/// those keep an empty Tones list and are preserved as-is, since a gameplay DLL still needs to
/// know that sfx id exists (scripts reference sfx by id, including undecodable ones).
/// </summary>
public sealed class SfxRecord
{
    public int Id { get; set; }
    public int VabId { get; set; }
    public int ProgramNumber { get; set; }
    public int ToneNumber { get; set; }
    public int Note { get; set; }
    public int SeqNum { get; set; }
    public int RefSfxId { get; set; }
    public int MaxVoices { get; set; }
    public int NumTones { get; set; }
    public string? SkipReason { get; set; }
    public List<SfxTone> Tones { get; set; } = new();
}

/// <summary>
/// Reads the extractor's two sound index files (sound/bgm.json, sound/sfx.json). Both are plain
/// arrays; entries are returned sorted by SoundIndex / Id (and tones by ToneIndex) so the manifests
/// the writer produces are byte-stable between runs regardless of the source file's order.
/// </summary>
public static class SoundManifestReader
{
    public static List<BgmEntry> ReadBgm(string filePath)
    {
        using var stream = System.IO.File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        var entries = new List<BgmEntry>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            entries.Add(new BgmEntry
            {
                SoundIndex = GetInt32(element, "SoundIndex"),
                File = GetString(element, "File"),
                Frames = GetInt32(element, "Frames"),
                DurationSeconds = GetDouble(element, "DurationSeconds"),
                LoopDetected = GetBoolean(element, "LoopDetected"),
                PeakLeft = GetInt32(element, "PeakLeft"),
                PeakRight = GetInt32(element, "PeakRight"),
                RmsLeft = GetDouble(element, "RmsLeft"),
                RmsRight = GetDouble(element, "RmsRight"),
                FirstAudibleFrame = GetInt32(element, "FirstAudibleFrame"),
            });
        }

        entries.Sort((left, right) => left.SoundIndex.CompareTo(right.SoundIndex));
        return entries;
    }

    public static List<SfxRecord> ReadSfx(string filePath)
    {
        using var stream = System.IO.File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        var records = new List<SfxRecord>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var record = new SfxRecord
            {
                Id = GetInt32(element, "Id"),
                VabId = GetInt32(element, "VabId"),
                ProgramNumber = GetInt32(element, "ProgramNumber"),
                ToneNumber = GetInt32(element, "ToneNumber"),
                Note = GetInt32(element, "Note"),
                SeqNum = GetInt32(element, "SeqNum"),
                RefSfxId = GetInt32(element, "RefSfxId"),
                MaxVoices = GetInt32(element, "MaxVoices"),
                NumTones = GetInt32(element, "NumTones"),
                SkipReason = GetNullableString(element, "SkipReason"),
            };

            if (element.TryGetProperty("Tones", out var tonesElement)
                && tonesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var toneElement in tonesElement.EnumerateArray())
                {
                    record.Tones.Add(new SfxTone
                    {
                        ToneIndex = GetInt32(toneElement, "ToneIndex"),
                        File = GetString(toneElement, "File"),
                        SampleRate = GetInt32(toneElement, "SampleRate"),
                        LoopStart = GetInt32(toneElement, "LoopStart"),
                        LoopEnd = GetInt32(toneElement, "LoopEnd"),
                        Repeat = GetBoolean(toneElement, "Repeat"),
                    });
                }

                record.Tones.Sort((left, right) => left.ToneIndex.CompareTo(right.ToneIndex));
            }

            records.Add(record);
        }

        records.Sort((left, right) => left.Id.CompareTo(right.Id));
        return records;
    }

    private static int GetInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static double GetDouble(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0d;

    private static bool GetBoolean(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement element, string propertyName)
        => GetNullableString(element, propertyName) ?? string.Empty;

    private static string? GetNullableString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
