using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Guards the BGM batch extraction rendered from one fresh sound system per track
/// (docs/plan-e19-opcodes.md section 1.2v, X2, D-E19-69). Before X2 every track started with what the
/// previous one left sounding (the "ding" at the start of track 25, the map 389 music), and track 44 was
/// the tail of track 43. The expected values are the value annex of the plan (docs/plan-e19-x2-annexe/),
/// written before the re-extraction.
/// Runs on real data-extracted content; returns early (rather than failing) when data-extracted/ or the
/// annex is not present next to the built test binaries - same convention as SpriteBankReaderAnimSetsTests.
/// </summary>
public class BgmFreshSoundSystemTests
{
    private const int SamplesPerVideoFrame = 735;
    private const int BytesPerStereoFrame = 4;
    private const int WavHeaderBytes = 44;

    // First frame at which the sequence driver plays a note, per track (plan section 1.2v, X2-1 T-B).
    public static TheoryData<int, int> DriverFirstNoteFrames => new()
    {
        { 1, 34 }, { 2, 57 }, { 3, 19 }, { 4, 30 }, { 5, 36 }, { 6, 36 }, { 7, 28 }, { 8, 30 }, { 9, 36 }, { 10, 36 },
        { 11, 53 }, { 12, 63 }, { 13, 63 }, { 14, 43 }, { 15, 36 }, { 16, 31 }, { 17, 42 }, { 18, 36 }, { 19, 20 },
        { 20, 32 }, { 21, 36 }, { 22, 36 }, { 23, 45 }, { 24, 33 }, { 25, 36 }, { 26, 39 }, { 27, 36 }, { 28, 36 },
        { 29, 34 }, { 30, 36 }, { 31, 36 }, { 32, 84 }, { 33, 32 }, { 34, 36 }, { 35, 36 }, { 36, 26 }, { 37, 18 },
        { 38, 20 }, { 39, 36 }, { 40, 18 }, { 41, 36 }, { 42, 31 }, { 43, 44 },
    };

    public static TheoryData<int> WrittenTracks
    {
        get
        {
            var data = new TheoryData<int>();
            for (var index = 1; index <= 46; index++)
            {
                if (index != 44)
                {
                    data.Add(index);
                }
            }

            return data;
        }
    }

    // T-A: the "ding" of track 25 is gone, the first 36 video frames are silent.
    [Fact]
    public void Track25_IsSilentBeforeItsFirstNote()
    {
        var bgmDirectory = FindBgmDirectory();
        if (bgmDirectory is null)
        {
            return;
        }

        var samples = ReadLeadingSamples(Path.Combine(bgmDirectory, "bgm_025.wav"), 36 * SamplesPerVideoFrame);
        Assert.Equal(36 * SamplesPerVideoFrame * 2, samples.Length);
        Assert.Equal(0, samples.Max(sample => Math.Abs((int)sample)));
    }

    // T-B (a): FirstAudibleFrame in bgm.json is not before the driver's first note.
    [Theory]
    [MemberData(nameof(DriverFirstNoteFrames))]
    public void FirstAudibleFrame_IsNotBeforeTheDriversFirstNote(int soundIndex, int driverFirstNoteFrame)
    {
        var bgmJson = FindFile("sound", "bgm.json");
        if (bgmJson is null)
        {
            return;
        }

        var entry = ReadEntries(bgmJson).Single(e => e.GetProperty("SoundIndex").GetInt32() == soundIndex);
        Assert.True(entry.GetProperty("FirstAudibleFrame").GetInt32() >= driverFirstNoteFrame);
    }

    // T-B (b): every sample before the driver's first note is 0.
    [Theory]
    [MemberData(nameof(DriverFirstNoteFrames))]
    public void Samples_BeforeTheDriversFirstNote_AreAllZero(int soundIndex, int driverFirstNoteFrame)
    {
        var bgmDirectory = FindBgmDirectory();
        if (bgmDirectory is null)
        {
            return;
        }

        var wanted = driverFirstNoteFrame * SamplesPerVideoFrame;
        var samples = ReadLeadingSamples(Path.Combine(bgmDirectory, $"bgm_{soundIndex:D3}.wav"), wanted);
        Assert.Equal(wanted * 2, samples.Length);
        Assert.Equal(0, samples.Max(sample => Math.Abs((int)sample)));
    }

    // T-C: the silent track 44 (the tail of track 43 before X2) is neither written nor listed.
    [Fact]
    public void SilentTrack44_IsNeitherWrittenNorListed()
    {
        var bgmJson = FindFile("sound", "bgm.json");
        if (bgmJson is null)
        {
            return;
        }

        var entries = ReadEntries(bgmJson);
        Assert.Equal(45, entries.Count);
        Assert.DoesNotContain(entries, e => e.GetProperty("SoundIndex").GetInt32() == 44);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(bgmJson)!, "bgm", "bgm_044.wav")));
    }

    // T-D (guard): the number of frames per track does not move (annex tracks.tsv).
    [Theory]
    [MemberData(nameof(WrittenTracks))]
    public void Frames_PerTrack_MatchTheAnnex(int soundIndex)
    {
        var bgmJson = FindFile("sound", "bgm.json");
        var tracks = FindAnnexFile("tracks.tsv");
        if (bgmJson is null || tracks is null)
        {
            return;
        }

        var row = ReadTsv(tracks).Single(r => r["SoundIndex"] == soundIndex.ToString(CultureInfo.InvariantCulture));
        var entry = ReadEntries(bgmJson).Single(e => e.GetProperty("SoundIndex").GetInt32() == soundIndex);
        Assert.Equal(int.Parse(row["NewFrames"], CultureInfo.InvariantCulture), entry.GetProperty("Frames").GetInt32());
    }

    // T-E (guard): track 1 is the same before and after X2, to the byte.
    [Fact]
    public void Track1_IsByteIdenticalToTheAnnex()
    {
        var bgmDirectory = FindBgmDirectory();
        var oracle = FindAnnexFile("oracle_sha256.tsv");
        if (bgmDirectory is null || oracle is null)
        {
            return;
        }

        var row = ReadTsv(oracle).Single(r => r["SoundIndex"] == "1");
        Assert.Equal(row["SHA256"], Sha256Of(Path.Combine(bgmDirectory, "bgm_001.wav")));
    }

    // Golden theory: each of the 45 written tracks equals the fixed batch's own output (annex).
    [Theory]
    [MemberData(nameof(WrittenTracks))]
    public void Track_IsByteIdenticalToTheFreshSoundSystemRender(int soundIndex)
    {
        var bgmDirectory = FindBgmDirectory();
        var oracle = FindAnnexFile("oracle_sha256.tsv");
        if (bgmDirectory is null || oracle is null)
        {
            return;
        }

        var row = ReadTsv(oracle).Single(r => r["SoundIndex"] == soundIndex.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(row["SHA256"], Sha256Of(Path.Combine(bgmDirectory, row["File"])));
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    // Reads up to 'stereoFrames' stereo frames after the 44-byte header, as interleaved 16-bit samples.
    private static short[] ReadLeadingSamples(string path, int stereoFrames)
    {
        using var stream = File.OpenRead(path);
        stream.Seek(WavHeaderBytes, SeekOrigin.Begin);
        var buffer = new byte[stereoFrames * BytesPerStereoFrame];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        var samples = new short[read / 2];
        Buffer.BlockCopy(buffer, 0, samples, 0, samples.Length * 2);
        return samples;
    }

    private static List<JsonElement> ReadEntries(string bgmJsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(bgmJsonPath));
        return document.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static List<Dictionary<string, string>> ReadTsv(string path)
    {
        var lines = File.ReadAllLines(path).Where(line => line.Length > 0).ToList();
        var header = lines[0].Split('\t');
        return lines.Skip(1)
            .Select(line =>
            {
                var cells = line.Split('\t');
                return header.Select((name, i) => (name, value: i < cells.Length ? cells[i] : string.Empty))
                    .ToDictionary(pair => pair.name, pair => pair.value);
            })
            .ToList();
    }

    private static string? FindBgmDirectory()
    {
        var json = FindFile("sound", "bgm.json");
        return json is null ? null : Path.Combine(Path.GetDirectoryName(json)!, "bgm");
    }

    private static string? FindFile(params string[] relativeUnderDataExtracted)
    {
        return FindUpwards(Path.Combine(new[] { "data-extracted" }.Concat(relativeUnderDataExtracted).ToArray()));
    }

    private static string? FindAnnexFile(string fileName)
    {
        return FindUpwards(Path.Combine("docs", "plan-e19-x2-annexe", fileName));
    }

    private static string? FindUpwards(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
