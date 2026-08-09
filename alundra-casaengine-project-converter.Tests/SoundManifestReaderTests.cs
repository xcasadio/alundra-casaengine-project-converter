using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class SoundManifestReaderTests
{
    [Fact]
    public void ReadSfx_KeepsSkippedRecordsAndAllTonesOfAMultiToneRecord()
    {
        // Three shapes present in the real sfx.json: a plain one-tone record, a record the
        // extractor could not decode (SkipReason non-null, empty Tones) and a multi-tone record.
        // The skipped one must survive - scripts address sfx by id, so dropping it would punch a
        // hole in the id space.
        var path = WriteFixture(
            """
            [
                {
                    "Id": 2, "VabId": -1, "ProgramNumber": 0, "ToneNumber": 1, "Note": 61,
                    "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 2, "NumTones": 1, "SkipReason": null,
                    "Tones": [
                        { "ToneIndex": 0, "File": "sfx_0002.wav", "SampleRate": 24214, "LoopStart": 28, "LoopEnd": 5655, "Repeat": false }
                    ]
                },
                {
                    "Id": 1, "VabId": -2, "ProgramNumber": 0, "ToneNumber": 0, "Note": 60,
                    "SeqNum": -1, "RefSfxId": 0, "MaxVoices": 0, "NumTones": 0,
                    "SkipReason": "invalid (VabId=-2)",
                    "Tones": []
                },
                {
                    "Id": 3, "VabId": 4, "ProgramNumber": 7, "ToneNumber": 2, "Note": 62,
                    "SeqNum": 9, "RefSfxId": 11, "MaxVoices": 3, "NumTones": 2, "SkipReason": null,
                    "Tones": [
                        { "ToneIndex": 1, "File": "sfx_0004.wav", "SampleRate": 11025, "LoopStart": 0, "LoopEnd": 100, "Repeat": true },
                        { "ToneIndex": 0, "File": "sfx_0003.wav", "SampleRate": 4274, "LoopStart": 28, "LoopEnd": 923, "Repeat": false }
                    ]
                }
            ]
            """);

        try
        {
            var records = SoundManifestReader.ReadSfx(path);

            // Sorted by Id, so the manifests the writer emits are stable between runs.
            Assert.Equal(new[] { 1, 2, 3 }, records.Select(record => record.Id));

            var skipped = records[0];
            Assert.Equal("invalid (VabId=-2)", skipped.SkipReason);
            Assert.Empty(skipped.Tones);
            Assert.Equal(-2, skipped.VabId);

            var single = records[1];
            Assert.Null(single.SkipReason);
            var tone = Assert.Single(single.Tones);
            Assert.Equal("sfx_0002.wav", tone.File);
            Assert.Equal(24214, tone.SampleRate);
            Assert.Equal(28, tone.LoopStart);
            Assert.Equal(5655, tone.LoopEnd);
            Assert.False(tone.Repeat);

            var multi = records[2];
            Assert.Equal(2, multi.Tones.Count);
            Assert.Equal(new[] { 0, 1 }, multi.Tones.Select(t => t.ToneIndex)); // tones sorted too
            Assert.Equal("sfx_0003.wav", multi.Tones[0].File);
            Assert.True(multi.Tones[1].Repeat);
            Assert.Equal(4, multi.VabId);
            Assert.Equal(7, multi.ProgramNumber);
            Assert.Equal(9, multi.SeqNum);
            Assert.Equal(11, multi.RefSfxId);
            Assert.Equal(3, multi.MaxVoices);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadBgm_PreservesEveryAnalysisField()
    {
        var path = WriteFixture(
            """
            [
                {
                    "SoundIndex": 2, "File": "bgm_002.wav", "Frames": 3993, "DurationSeconds": 66.55,
                    "LoopDetected": false, "PeakLeft": 14200, "PeakRight": 22013,
                    "RmsLeft": 1955.88, "RmsRight": 3390.49, "FirstAudibleFrame": 0
                },
                {
                    "SoundIndex": 1, "File": "bgm_001.wav", "Frames": 7966, "DurationSeconds": 132.75,
                    "LoopDetected": true, "PeakLeft": 32767, "PeakRight": 28689,
                    "RmsLeft": 3756.87, "RmsRight": 3811.29, "FirstAudibleFrame": 34
                }
            ]
            """);

        try
        {
            var entries = SoundManifestReader.ReadBgm(path);

            Assert.Equal(new[] { 1, 2 }, entries.Select(entry => entry.SoundIndex));

            var first = entries[0];
            Assert.Equal("bgm_001.wav", first.File);
            Assert.Equal(7966, first.Frames);
            Assert.Equal(132.75, first.DurationSeconds, 3);
            Assert.True(first.LoopDetected);
            Assert.Equal(32767, first.PeakLeft);
            Assert.Equal(28689, first.PeakRight);
            Assert.Equal(3756.87, first.RmsLeft, 3);
            Assert.Equal(3811.29, first.RmsRight, 3);
            Assert.Equal(34, first.FirstAudibleFrame);

            Assert.False(entries[1].LoopDetected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteFixture(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sound-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
