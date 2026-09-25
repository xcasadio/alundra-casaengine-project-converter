using System.Text.Json;
using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class AudioWriterTests
{
    // The writer copies WAVs verbatim and never decodes them, so any bytes will do.
    private static readonly byte[] FakeWavBytes = { 82, 73, 70, 70, 0, 0, 0, 0, 87, 65, 86, 69 };

    [Fact]
    public void ConvertAudio_CopiesEveryReferencedWavAndPreservesTheSourceFieldsInTheManifests()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteSoundFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            AudioWriter.ConvertAudio(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Audio.Bgm"]);
            Assert.Equal(3, report.Counters["Audio.Sfx"]);
            Assert.Equal(3, report.Counters["Audio.SfxTones"]);
            Assert.Equal(1, report.Counters["Audio.SfxSkipped"]);
            Assert.Equal(5, report.Counters["Audio.WavCopied"]);

            Assert.True(File.Exists(Path.Combine(outputDirectory, "Musics", "bgm_001.wav")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Sounds", "sfx_0003.wav")));

            // Every source field survives, plus the catalog asset id that makes the manifest
            // self-sufficient (no need to keep the extractor's originals around).
            using var sfxManifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Sounds", "sfx-manifest.json")));
            var records = sfxManifest.RootElement.EnumerateArray().ToArray();
            Assert.Equal(3, records.Length);

            var skipped = records[0];
            Assert.Equal(1, skipped.GetProperty("id").GetInt32());
            Assert.Equal("no tones (NumTones=0)", skipped.GetProperty("skip_reason").GetString());
            Assert.Equal(0, skipped.GetProperty("tones").GetArrayLength());

            var multiTone = records[2];
            Assert.Equal(2, multiTone.GetProperty("tones").GetArrayLength());
            var firstTone = multiTone.GetProperty("tones")[0];
            Assert.Equal(4274, firstTone.GetProperty("sample_rate").GetInt32());
            Assert.Equal(28, firstTone.GetProperty("loop_start").GetInt32());
            Assert.Equal(923, firstTone.GetProperty("loop_end").GetInt32());

            // Ids are deterministic (Ids.For on the source-relative path), so the manifest and the
            // catalog agree and both stay stable between runs.
            var expectedId = Ids.For("sound/sfx/sfx_0003.wav");
            Assert.Equal(expectedId, firstTone.GetProperty("asset_id").GetGuid());
            var assetInfo = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Id == expectedId);
            Assert.Equal(Path.Combine("Sounds", "sfx_0003.wav"), assetInfo.FileName);

            using var bgmManifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Musics", "bgm-manifest.json")));
            var firstBgm = bgmManifest.RootElement[0];
            Assert.Equal(1, firstBgm.GetProperty("sound_index").GetInt32());
            Assert.Equal(34, firstBgm.GetProperty("first_audible_frame").GetInt32());
            Assert.True(firstBgm.GetProperty("loop_detected").GetBoolean());
            Assert.Equal(Ids.For("sound/bgm/bgm_001.wav"), firstBgm.GetProperty("asset_id").GetGuid());
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertAudio_WarnsBothWaysWhenTheManifestAndTheWavFolderDisagree()
    {
        // Both directions matter: a referenced-but-missing WAV means a sound id resolves to
        // nothing, and a WAV nobody references would be silently left behind - the exact data loss
        // this phase exists to prevent.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteSoundFixture(inputDirectory);
            File.Delete(Path.Combine(inputDirectory, "sound", "sfx", "sfx_0002.wav"));
            File.WriteAllBytes(Path.Combine(inputDirectory, "sound", "sfx", "sfx_9999.wav"), FakeWavBytes);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            AudioWriter.ConvertAudio(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(4, report.Counters["Audio.WavCopied"]);
            Assert.Contains(report.Warnings, warning => warning.Contains("sfx_0002.wav", StringComparison.Ordinal));
            Assert.Contains(
                report.Warnings,
                warning => warning.Contains("sfx_9999.wav", StringComparison.Ordinal)
                           && warning.Contains("no manifest entry", StringComparison.Ordinal));

            // The missing WAV must not leave a dangling catalog entry pointing at a file that was
            // never copied.
            Assert.DoesNotContain(
                EditorAssetCatalogService.AssetInfos,
                info => info.Name == "sfx_0002");
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // P9 of docs/plan-audio-mix-exact-muet.md: the VAB volume/pan attributes are carried as nullable ints.
    // A missing field must stay null in the manifest, never become 0, so the DLL can tell "unknown" from a
    // real 0 (tone 1 of sound 162 has a volume of 0 in the original).
    [Fact]
    public void ConvertAudio_CarriesTheVabAttributesAndKeepsMissingOnesNull()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteSoundFixture(inputDirectory);
            File.WriteAllText(
                Path.Combine(inputDirectory, "sound", "sfx.json"),
                """
                [
                    {
                        "Id": 1, "VabId": -2, "ProgramNumber": 0, "ToneNumber": 0, "Note": 60,
                        "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 0, "NumTones": 0,
                        "SkipReason": "invalid (VabId=-2)", "Tones": [],
                        "VabMasterVolume": null, "ProgramVolume": null, "ProgramPan": null
                    },
                    {
                        "Id": 2, "VabId": -1, "ProgramNumber": 0, "ToneNumber": 1, "Note": 61,
                        "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 2, "NumTones": 1, "SkipReason": null,
                        "Tones": [
                            { "ToneIndex": 0, "File": "sfx_0002.wav", "SampleRate": 24214, "LoopStart": 28, "LoopEnd": 5655, "Repeat": false }
                        ]
                    },
                    {
                        "Id": 3, "VabId": -1, "ProgramNumber": 0, "ToneNumber": 2, "Note": 62,
                        "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 1, "NumTones": 2, "SkipReason": null,
                        "Tones": [
                            { "ToneIndex": 0, "File": "sfx_0003.wav", "SampleRate": 4274, "LoopStart": 28, "LoopEnd": 923, "Repeat": false, "Volume": 127, "Pan": 64 },
                            { "ToneIndex": 1, "File": "sfx_0004.wav", "SampleRate": 11025, "LoopStart": 0, "LoopEnd": 100, "Repeat": true, "Volume": 0, "Pan": 0 }
                        ],
                        "VabMasterVolume": 127, "ProgramVolume": 120, "ProgramPan": 0
                    }
                ]
                """);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            AudioWriter.ConvertAudio(inputDirectory, outputDirectory, report);

            using var sfxManifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Sounds", "sfx-manifest.json")));
            var records = sfxManifest.RootElement.EnumerateArray().ToArray();

            // Unresolved record: null in the source, null in the manifest.
            AssertNull(records[0], "vab_master_volume", "program_volume", "program_pan");

            // Source written before the attributes existed: the keys are absent, the manifest says null.
            AssertNull(records[1], "vab_master_volume", "program_volume", "program_pan");
            AssertNull(records[1].GetProperty("tones")[0], "volume", "pan");

            // Resolved record: exact values, a real 0 included.
            var resolved = records[2];
            Assert.Equal(127, resolved.GetProperty("vab_master_volume").GetInt32());
            Assert.Equal(120, resolved.GetProperty("program_volume").GetInt32());
            Assert.Equal(0, resolved.GetProperty("program_pan").GetInt32());
            Assert.Equal(127, resolved.GetProperty("tones")[0].GetProperty("volume").GetInt32());
            Assert.Equal(64, resolved.GetProperty("tones")[0].GetProperty("pan").GetInt32());
            Assert.Equal(0, resolved.GetProperty("tones")[1].GetProperty("volume").GetInt32());
            Assert.Equal(0, resolved.GetProperty("tones")[1].GetProperty("pan").GetInt32());
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void AssertNull(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.True(element.TryGetProperty(propertyName, out var value), $"'{propertyName}' is missing from the manifest.");
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
    }

    private static void WriteSoundFixture(string inputDirectory)
    {
        var soundDirectory = Path.Combine(inputDirectory, "sound");
        var bgmDirectory = Path.Combine(soundDirectory, "bgm");
        var sfxDirectory = Path.Combine(soundDirectory, "sfx");
        Directory.CreateDirectory(bgmDirectory);
        Directory.CreateDirectory(sfxDirectory);

        foreach (var fileName in new[] { "bgm_001.wav", "bgm_002.wav" })
        {
            File.WriteAllBytes(Path.Combine(bgmDirectory, fileName), FakeWavBytes);
        }

        foreach (var fileName in new[] { "sfx_0002.wav", "sfx_0003.wav", "sfx_0004.wav" })
        {
            File.WriteAllBytes(Path.Combine(sfxDirectory, fileName), FakeWavBytes);
        }

        File.WriteAllText(
            Path.Combine(soundDirectory, "bgm.json"),
            """
            [
                {
                    "SoundIndex": 1, "File": "bgm_001.wav", "Frames": 7966, "DurationSeconds": 132.75,
                    "LoopDetected": true, "PeakLeft": 32767, "PeakRight": 28689,
                    "RmsLeft": 3756.87, "RmsRight": 3811.29, "FirstAudibleFrame": 34
                },
                {
                    "SoundIndex": 2, "File": "bgm_002.wav", "Frames": 3993, "DurationSeconds": 66.55,
                    "LoopDetected": false, "PeakLeft": 14200, "PeakRight": 22013,
                    "RmsLeft": 1955.88, "RmsRight": 3390.49, "FirstAudibleFrame": 0
                }
            ]
            """);

        File.WriteAllText(
            Path.Combine(soundDirectory, "sfx.json"),
            """
            [
                {
                    "Id": 1, "VabId": -2, "ProgramNumber": 0, "ToneNumber": 0, "Note": 60,
                    "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 0, "NumTones": 0,
                    "SkipReason": "no tones (NumTones=0)", "Tones": []
                },
                {
                    "Id": 2, "VabId": -1, "ProgramNumber": 0, "ToneNumber": 1, "Note": 61,
                    "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 2, "NumTones": 1, "SkipReason": null,
                    "Tones": [
                        { "ToneIndex": 0, "File": "sfx_0002.wav", "SampleRate": 24214, "LoopStart": 28, "LoopEnd": 5655, "Repeat": false }
                    ]
                },
                {
                    "Id": 3, "VabId": -1, "ProgramNumber": 0, "ToneNumber": 2, "Note": 62,
                    "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 1, "NumTones": 2, "SkipReason": null,
                    "Tones": [
                        { "ToneIndex": 0, "File": "sfx_0003.wav", "SampleRate": 4274, "LoopStart": 28, "LoopEnd": 923, "Repeat": false },
                        { "ToneIndex": 1, "File": "sfx_0004.wav", "SampleRate": 11025, "LoopStart": 0, "LoopEnd": 100, "Repeat": true }
                    ]
                }
            ]
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
