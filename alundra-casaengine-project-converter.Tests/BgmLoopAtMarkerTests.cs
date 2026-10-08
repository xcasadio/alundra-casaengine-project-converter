using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Guards the BGM extraction rendered to the second loop jump (docs/plan-e19-opcodes.md section 1.2w.2, E19.L-b1,
/// D-E19-73, D-E19-76): a looping track is written as [0, J2) with its loop start J1 recorded in bgm.json
/// (Looping, LoopStartFrame, LoopStartSample = J1 x 735) and a one-frame linear crossfade at its end; a track
/// without a loop keeps its release tail. The expected values are the oracle of docs/plan-e19-loop-annexe/data/
/// (oracle_sha256.tsv, notes.md section 5), written before the re-extraction.
/// Runs on real data-extracted content; returns early (rather than failing) when data-extracted/ or the annex
/// is not present next to the built test binaries. Each executed case writes "executed" to the test output so
/// a run can report how many cases really ran.
/// </summary>
public class BgmLoopAtMarkerTests
{
    private const int SamplesPerVideoFrame = 735;
    private const int BytesPerStereoFrame = 4;
    private const int WavHeaderBytes = 44;
    private const int BytesPerVideoFrame = SamplesPerVideoFrame * BytesPerStereoFrame;

    private readonly ITestOutputHelper _output;

    public BgmLoopAtMarkerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // (soundIndex, J1 = first loop jump, J2 = second loop jump = the written Frames), oracle_sha256.tsv.
    public static TheoryData<int, int, int> LoopingTracks => new()
    {
        { 1, 7965, 15215 }, { 2, 3992, 7870 }, { 3, 2313, 4607 }, { 4, 5874, 11718 },
        { 5, 8019, 16002 }, { 6, 5725, 11415 }, { 7, 10638, 21192 }, { 8, 5290, 10551 },
        { 9, 6402, 12768 }, { 10, 5955, 11848 }, { 11, 5455, 10707 }, { 12, 3454, 6782 },
        { 13, 6567, 13007 }, { 14, 14552, 27204 }, { 15, 7644, 15202 }, { 16, 8178, 15914 },
        { 17, 7949, 15834 }, { 18, 5117, 10199 }, { 19, 3835, 7566 }, { 20, 5330, 10614 },
        { 21, 6754, 13474 }, { 22, 7071, 14107 }, { 23, 7404, 14764 }, { 24, 4599, 8598 },
        { 25, 7272, 13186 }, { 27, 7263, 14039 }, { 28, 5871, 11594 }, { 29, 5282, 10462 },
        { 30, 10215, 19947 }, { 31, 6000, 11559 }, { 32, 6220, 12273 }, { 33, 5900, 11750 },
        { 34, 3746, 7383 }, { 35, 13744, 20549 }, { 39, 2255, 4474 }, { 41, 2943, 5853 },
        { 45, 380, 718 }, { 46, 380, 718 },
    };

    // (soundIndex, Frames) of the 7 tracks without a loop: the end of the track plus its release tail.
    public static TheoryData<int, int> OneShotTracks => new()
    {
        { 26, 1220 }, { 36, 601 }, { 37, 620 }, { 38, 531 }, { 40, 543 }, { 42, 772 }, { 43, 900 },
    };

    public static TheoryData<int> SeamTracks
    {
        get
        {
            var data = new TheoryData<int>();
            foreach (var row in LoopingTracks)
            {
                data.Add((int)row[0]);
            }

            return data;
        }
    }

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

    // L-A: a looping track records Looping, its loop start J1 and J1 x 735, and is written up to J2.
    [Theory]
    [MemberData(nameof(LoopingTracks))]
    public void LoopingTrack_RecordsItsLoopStartAndEndsAtTheSecondJump(int soundIndex, int loopStartFrame, int frames)
    {
        var bgmJson = FindFile("sound", "bgm.json");
        if (bgmJson is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var entry = ReadEntry(bgmJson, soundIndex);
        Assert.True(entry.GetProperty("Looping").GetBoolean());
        Assert.Equal(loopStartFrame, entry.GetProperty("LoopStartFrame").GetInt32());
        Assert.Equal(loopStartFrame * SamplesPerVideoFrame, entry.GetProperty("LoopStartSample").GetInt32());
        Assert.Equal(frames, entry.GetProperty("Frames").GetInt32());
    }

    // L-B: a track without a loop is not looping, has no loop start and keeps its release tail in Frames.
    [Theory]
    [MemberData(nameof(OneShotTracks))]
    public void OneShotTrack_HasNoLoopStartAndKeepsItsReleaseTail(int soundIndex, int frames)
    {
        var bgmJson = FindFile("sound", "bgm.json");
        if (bgmJson is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var entry = ReadEntry(bgmJson, soundIndex);
        Assert.False(entry.GetProperty("Looping").GetBoolean());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("LoopStartFrame").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("LoopStartSample").ValueKind);
        Assert.Equal(frames, entry.GetProperty("Frames").GetInt32());
    }

    // L-C (guard, green before and after): the WAV length is 44 + Frames x 2940 for every written track.
    [Theory]
    [MemberData(nameof(WrittenTracks))]
    public void WavLength_IsTheHeaderPlusFramesTimesTheFrameSize(int soundIndex)
    {
        var bgmJson = FindFile("sound", "bgm.json");
        var bgmDirectory = FindBgmDirectory();
        if (bgmJson is null || bgmDirectory is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var frames = ReadEntry(bgmJson, soundIndex).GetProperty("Frames").GetInt32();
        var length = new FileInfo(Path.Combine(bgmDirectory, $"bgm_{soundIndex:D3}.wav")).Length;
        Assert.Equal(WavHeaderBytes + (long)frames * BytesPerVideoFrame, length);
    }

    // L-D: the loop seam. The crossfade makes the last stereo frame of the file equal the stereo frame just
    // before the loop start, so the wrap J2 -> J1 is the junction the render itself made at J1.
    [Theory]
    [MemberData(nameof(SeamTracks))]
    public void LoopingTrack_LastFrameEqualsTheFrameBeforeTheLoopStart(int soundIndex)
    {
        var bgmDirectory = FindBgmDirectory();
        var oracle = FindAnnexFile("oracle_sha256.tsv");
        if (bgmDirectory is null || oracle is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var loopStartSample = long.Parse(
            ReadTsv(oracle).Single(r => r["SoundIndex"] == soundIndex.ToString(CultureInfo.InvariantCulture))["LoopStartSample"],
            CultureInfo.InvariantCulture);
        using var stream = File.OpenRead(Path.Combine(bgmDirectory, $"bgm_{soundIndex:D3}.wav"));
        var last = ReadStereoFrame(stream, stream.Length - BytesPerStereoFrame);
        var beforeLoopStart = ReadStereoFrame(stream, WavHeaderBytes + (loopStartSample - 1) * BytesPerStereoFrame);
        Assert.Equal(beforeLoopStart, last);
    }

    // L-E (guard, green before and after): the first TodayFrames video frames are the X2 render byte for byte.
    [Theory]
    [MemberData(nameof(WrittenTracks))]
    public void Track_StartsWithTheX2RenderByteForByte(int soundIndex)
    {
        var bgmDirectory = FindBgmDirectory();
        var oracle = FindAnnexFile("oracle_sha256.tsv");
        if (bgmDirectory is null || oracle is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var row = ReadTsv(oracle).Single(r => r["SoundIndex"] == soundIndex.ToString(CultureInfo.InvariantCulture));
        var todayFrames = int.Parse(row["TodayFrames"], CultureInfo.InvariantCulture);
        Assert.Equal(row["TodayDataSHA256"], Sha256OfDataPrefix(Path.Combine(bgmDirectory, row["File"]), todayFrames));
    }

    // Golden: bgm.json is the oracle's, byte for byte (the 45 WAV are checked by BgmFreshSoundSystemTests).
    [Fact]
    public void BgmJson_IsTheOracleByteForByte()
    {
        var bgmJson = FindFile("sound", "bgm.json");
        if (bgmJson is null)
        {
            return;
        }

        _output.WriteLine("executed");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(bgmJson))).ToLowerInvariant();
        Assert.Equal("3d176f4a2df0dc7ad0d8f1a2b88fed1e919d4e4b4aab173c6028192ca0b6efaf", hash);
    }

    private static (short Left, short Right) ReadStereoFrame(FileStream stream, long offset)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var bytes = new byte[BytesPerStereoFrame];
        stream.ReadExactly(bytes);
        return (BitConverter.ToInt16(bytes, 0), BitConverter.ToInt16(bytes, 2));
    }

    private static string Sha256OfDataPrefix(string path, int frames)
    {
        using var stream = File.OpenRead(path);
        stream.Seek(WavHeaderBytes, SeekOrigin.Begin);
        var remaining = (long)frames * BytesPerVideoFrame;
        var buffer = new byte[1 << 20];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static JsonElement ReadEntry(string bgmJsonPath, int soundIndex)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(bgmJsonPath));
        return document.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("SoundIndex").GetInt32() == soundIndex)
            .Clone();
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
        return FindUpwards(Path.Combine("docs", "plan-e19-loop-annexe", "data", fileName));
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
