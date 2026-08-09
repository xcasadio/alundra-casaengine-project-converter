using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 4: preserves the extracted audio (45 BGM tracks, 996 SFX samples) in the CasaEngine
/// project.
///
/// Mapping decisions:
///  - CasaEngine has NO audio asset type: AssetLoaderRegistry registers no loader for .wav, and
///    CasaEngine.Framework.Audio.Sound is a thin runtime SoundEffect wrapper with no serialized
///    form. So there is nothing to convert the WAVs *into*. Per the converter's "never throw away
///    source data" rule they are instead copied verbatim (no re-encoding - the extractor already
///    produced standard PCM WAVs) and registered in the asset catalog, so a future gameplay DLL can
///    resolve a sound id to a file through the same catalog it uses for every other asset. If the
///    engine later grows a real audio asset type, this phase only has to gain a wrapper document;
///    the files and ids stay where they are.
///  - Layout follows the folders Phase 0 already creates: BGM under Musics/, SFX under Sounds/.
///    Paths are relative to the output root, which IS the content root (same convention as
///    Maps/... and Sprites/Textures/...), so no extra "Content/" segment is introduced.
///  - Asset ids are deterministic (Ids.For on the source-relative path, e.g.
///    "sound/sfx/sfx_0001.wav"). Unlike the Phase 1/3 assets, nothing here is an engine ObjectBase
///    with a private Id setter generating its own guid, so the ids can be - and are - stable across
///    runs. That matters because the manifests below, and any future gameplay data, reference them.
///  - The manifests (Musics/bgm-manifest.json, Sounds/sfx-manifest.json) keep every source field
///    under its original name (snake_cased), plus the catalog asset id of each WAV, so they are
///    self-sufficient: a consumer needs the manifest only, not the extractor's originals. This is
///    where the data the engine cannot represent survives - per-tone SampleRate/LoopStart/LoopEnd
///    (MonoGame's SoundEffect exposes no loop points), the VAB/SEQ addressing (VabId,
///    ProgramNumber, ToneNumber, Note, SeqNum, RefSfxId, MaxVoices), and the BGM loudness analysis.
///  - Records the extractor could not decode (SkipReason non-null, 91 of them) are kept in the
///    manifest with an empty tone list rather than dropped: scripts address sfx by id, so the id
///    space must stay complete, and the reason is itself a fact worth keeping.
/// </summary>
public static class AudioWriter
{
    private const string BgmRelativeDirectory = "Musics";
    private const string SfxRelativeDirectory = "Sounds";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static void ConvertAudio(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var soundDirectory = Path.Combine(inputDirectory, "sound");
        if (!Directory.Exists(soundDirectory))
        {
            report.Warnings.Add($"Audio: sound directory not found at '{soundDirectory}'; phase 4 skipped.");
            return;
        }

        var copiedWavCount = 0;
        copiedWavCount += ConvertBgm(soundDirectory, outputDirectory, report);
        copiedWavCount += ConvertSfx(soundDirectory, outputDirectory, report);

        EditorAssetCatalogService.Save();
        report.Increment("Audio.WavCopied", copiedWavCount);
    }

    private static int ConvertBgm(string soundDirectory, string outputDirectory, ConversionReport report)
    {
        var manifestSourcePath = Path.Combine(soundDirectory, "bgm.json");
        if (!File.Exists(manifestSourcePath))
        {
            report.Warnings.Add($"Audio: '{manifestSourcePath}' not found; BGM skipped.");
            return 0;
        }

        var entries = SoundManifestReader.ReadBgm(manifestSourcePath);
        var sourceDirectory = Path.Combine(soundDirectory, "bgm");
        var targetDirectory = Path.Combine(outputDirectory, BgmRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var referencedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var copiedCount = 0;

        foreach (var entry in entries)
        {
            referencedFileNames.Add(entry.File);
            entry.AssetId = Ids.For($"sound/bgm/{entry.File}");

            if (CopyAndRegister(sourceDirectory, targetDirectory, BgmRelativeDirectory, entry.File, entry.AssetId, report))
            {
                copiedCount++;
            }
        }

        WarnAboutUnreferencedWavs(sourceDirectory, referencedFileNames, "bgm", report);

        WriteManifest(Path.Combine(targetDirectory, "bgm-manifest.json"), entries);

        report.Increment("Audio.Bgm", entries.Count);
        return copiedCount;
    }

    private static int ConvertSfx(string soundDirectory, string outputDirectory, ConversionReport report)
    {
        var manifestSourcePath = Path.Combine(soundDirectory, "sfx.json");
        if (!File.Exists(manifestSourcePath))
        {
            report.Warnings.Add($"Audio: '{manifestSourcePath}' not found; SFX skipped.");
            return 0;
        }

        var records = SoundManifestReader.ReadSfx(manifestSourcePath);
        var sourceDirectory = Path.Combine(soundDirectory, "sfx");
        var targetDirectory = Path.Combine(outputDirectory, SfxRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var referencedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var copiedCount = 0;
        var toneCount = 0;
        var skippedCount = 0;

        foreach (var record in records)
        {
            if (record.SkipReason != null)
            {
                skippedCount++;
            }

            foreach (var tone in record.Tones)
            {
                toneCount++;
                referencedFileNames.Add(tone.File);
                tone.AssetId = Ids.For($"sound/sfx/{tone.File}");

                if (CopyAndRegister(sourceDirectory, targetDirectory, SfxRelativeDirectory, tone.File, tone.AssetId, report))
                {
                    copiedCount++;
                }
            }
        }

        WarnAboutUnreferencedWavs(sourceDirectory, referencedFileNames, "sfx", report);

        WriteManifest(Path.Combine(targetDirectory, "sfx-manifest.json"), records);

        report.Increment("Audio.Sfx", records.Count);
        report.Increment("Audio.SfxTones", toneCount);
        report.Increment("Audio.SfxSkipped", skippedCount);
        return copiedCount;
    }

    private static bool CopyAndRegister(
        string sourceDirectory,
        string targetDirectory,
        string relativeDirectory,
        string fileName,
        Guid assetId,
        ConversionReport report)
    {
        var sourcePath = Path.Combine(sourceDirectory, fileName);
        if (!File.Exists(sourcePath))
        {
            report.Warnings.Add($"Audio: manifest references '{fileName}' but '{sourcePath}' does not exist.");
            return false;
        }

        File.Copy(sourcePath, Path.Combine(targetDirectory, fileName), overwrite: true);

        EditorAssetCatalogService.Add(new AssetInfo(assetId)
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FileName = Path.Combine(relativeDirectory, fileName),
        });

        return true;
    }

    // The reverse check of the missing-file warning: a WAV nobody references would be silently lost
    // (it is never copied), which is exactly the kind of data loss this phase exists to prevent.
    private static void WarnAboutUnreferencedWavs(
        string sourceDirectory, HashSet<string> referencedFileNames, string label, ConversionReport report)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            report.Warnings.Add($"Audio: '{sourceDirectory}' not found; no {label} WAV was copied.");
            return;
        }

        foreach (var path in Directory.EnumerateFiles(sourceDirectory, "*.wav").OrderBy(p => p, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            if (!referencedFileNames.Contains(fileName))
            {
                report.Warnings.Add($"Audio: '{label}/{fileName}' exists on disk but no manifest entry references it.");
            }
        }
    }

    private static void WriteManifest<T>(string filePath, List<T> entries)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(entries, SerializerOptions));
    }
}
