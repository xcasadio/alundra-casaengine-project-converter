#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>The DLL's two machines (<see cref="AlundraDialogueNameBox"/>, <see cref="AlundraInventoryPortrait"/> as the dialogue's instance) behind the interface of the raw sequences.</summary>
internal sealed class DllSpeakerMachines : ISpeakerMachines
{
    private readonly AlundraDialogueNameBox _name = new();
    private readonly AlundraInventoryPortrait _portrait = new();
    private readonly Func<int, string?> _resolve;

    public DllSpeakerMachines(IReadOnlyDictionary<int, (string Name, int Width)> names) => _resolve = id => names.TryGetValue(id, out var n) ? n.Name : null;

    public int NameFlags => _name.Flags;

    public bool NameSlot => _name.IsSlotOpen;

    public int PortraitState => _portrait.State;

    public bool NameOpen(int id) => _name.TryOpen(id, _resolve, SpeakerRig.Advance);

    public void NameClose() => _name.Close();

    public bool PortraitOpen(RawEntity entity, (int X, int Y) cam, int height)
    {
        var head = AlundraInventoryPortrait.ComputeHeadPoint(entity.X << 16, entity.Y << 16, entity.Z << 16, cam.X, cam.Y);
        return _portrait.Start(head.X, head.Y, 8, 172 - height, 48, height);
    }

    public void PortraitClose(RawEntity entity, (int X, int Y) cam)
    {
        var head = AlundraInventoryPortrait.ComputeHeadPoint(entity.X << 16, entity.Y << 16, entity.Z << 16, cam.X, cam.Y);
        _portrait.BeginReturn(head.X, head.Y);
    }

    public string? NamePass()
    {
        _name.Pass();
        return _name.Drawn is { } d ? $"{d.FrameX}|{d.TextX}" : null;
    }

    public string? PortraitPass()
    {
        _portrait.Step();
        if (!_portrait.DrawnThisStep)
        {
            return null;
        }

        var phase = _portrait.Phase switch
        {
            AlundraPortraitPhase.In => "in",
            AlundraPortraitPhase.Rest => "rest",
            AlundraPortraitPhase.Out => "out",
            AlundraPortraitPhase.Gone => "gone",
            _ => "?",
        };
        return $"{_portrait.X}|{_portrait.Y}|{_portrait.DrawnWidth}|{_portrait.DrawnHeight}|{_portrait.Rgb}|{phase}";
    }
}

/// <summary>
/// E19.f4b F4B-1, "oracle et suites" (docs/plan-e19-opcodes.md): (1) the C# oracle of <see cref="AlundraDialogueSpeakerOracle"/> (a port of <c>model/f4_model.py</c> and
/// <c>model/f4_model_h.py</c>) reproduces the digests and the coverage counters of <c>sequences-raw-digests.json</c> (40 seeds of 400 images from the SplitMix64 generator
/// of <c>model/gen_sequences.py</c>, whose first five outputs of seed 1 validate the port); (2) the DLL's machines equal the oracle image by image on the same sequences.
/// </summary>
public sealed class AlundraDialogueSpeakerMachineTests
{
    private static readonly IReadOnlyDictionary<int, (string Name, int Width)> Names = SpeakerAnnex.Names();

    private static JsonElement Digests()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SpeakerAnnex.File_("sequences-raw-digests.json")));
        return document.RootElement.Clone();
    }

    [Fact]
    public void SplitMix64_FirstFiveOutputsOfSeed1_MatchTheDigestFile()
    {
        var expected = Digests().GetProperty("prng_first5_seed1").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var rnd = new SplitMix64(1);
        var actual = Enumerable.Range(0, 5).Select(_ => rnd.Next().ToString()).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Oracle_ReproducesTheDigestsAndTheCoverage_OfTheFortySeeds()
    {
        var digests = Digests();
        var widths = SpeakerRawRun.ModelWidths(Names);
        var total = SpeakerRawRun.CoverageKeys.ToDictionary(k => k, _ => 0);
        var seeds = digests.GetProperty("sequences");
        Assert.Equal(40, seeds.EnumerateObject().Count());
        foreach (var seed in seeds.EnumerateObject())
        {
            var (lines, cov) = SpeakerRawRun.Run(int.Parse(seed.Name), new OracleSpeakerMachines(widths), widths);
            Assert.Equal(seed.Value.GetProperty("sha256").GetString(), SpeakerRawRun.Digest(lines));
            foreach (var key in SpeakerRawRun.CoverageKeys)
            {
                Assert.True(seed.Value.GetProperty("coverage").GetProperty(key).GetInt32() == cov[key], $"seed {seed.Name}: coverage {key} {cov[key]}, file {seed.Value.GetProperty("coverage").GetProperty(key).GetInt32()}");
                total[key] += cov[key];
            }
        }

        foreach (var key in SpeakerRawRun.CoverageKeys)
        {
            Assert.Equal(digests.GetProperty("coverage_total").GetProperty(key).GetInt32(), total[key]);
        }
    }

    [Fact]
    public void TheMachinesOfTheDll_EqualTheOracle_ImageByImage_OnTheFortySeeds()
    {
        var widths = SpeakerRawRun.ModelWidths(Names);
        var diffs = new List<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var (oracle, _) = SpeakerRawRun.Run(seed, new OracleSpeakerMachines(widths), widths);
            var (dll, _) = SpeakerRawRun.Run(seed, new DllSpeakerMachines(Names), widths);
            for (var f = 0; f < oracle.Count; f++)
            {
                if (oracle[f] != dll[f])
                {
                    diffs.Add($"seed {seed} image {f}: DLL {dll[f]}, oracle {oracle[f]}");
                    break;
                }
            }
        }

        Assert.True(diffs.Count == 0, $"{diffs.Count} of 40 sequences differ; first ones:\n{string.Join("\n", diffs.Take(6))}");
    }

    [Fact]
    public void TheNameBoxOfTheDll_MeasuresTheSixtyNamesOfTheBinary()
    {
        foreach (var (id, (name, width)) in Names)
        {
            var box = new AlundraDialogueNameBox();
            Assert.True(box.TryOpen(id, i => i == id ? name : null, SpeakerRig.Advance), $"name 0x{id:x} {name}");
            Assert.True(width == box.TextWidth, $"name 0x{id:x} {name}: width {box.TextWidth}, binary {width}");
        }
    }

    // ---- the refusals of the name, in their order: busy, then the range, then the ETC text ------------------------------------

    private sealed class CountingResolver
    {
        public int Calls;

        public string? Resolve(int id)
        {
            Calls++;
            return id == 0x104 ? "Jess" : id == 0x10C ? string.Empty : null;
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    [InlineData(6)]
    public void ANameBoxAlreadyOpen_RefusesBeforeAskingTheEtc(int flags)
    {
        var resolver = new CountingResolver();
        var box = new AlundraDialogueNameBox();
        Assert.True(box.TryOpen(0x104, resolver.Resolve, SpeakerRig.Advance));
        if (flags == 4)
        {
            for (var i = 0; i < 18; i++)
            {
                box.Pass();
            }
        }
        else if (flags == 6)
        {
            box.Close();
        }

        Assert.Equal(flags, box.Flags);
        resolver.Calls = 0;

        Assert.False(box.TryOpen(0x104, resolver.Resolve, SpeakerRig.Advance));
        Assert.Equal(0, resolver.Calls);
        Assert.Equal(flags, box.Flags);
    }

    [Theory]
    [InlineData(0xFF)]
    [InlineData(0x200)]
    [InlineData(-1)]
    [InlineData(0)]
    public void AnIdOutOfTheRange_RefusesBeforeAskingTheEtc(int id)
    {
        var resolver = new CountingResolver();
        var box = new AlundraDialogueNameBox();

        Assert.False(box.TryOpen(id, resolver.Resolve, SpeakerRig.Advance));
        Assert.Equal(0, resolver.Calls);
        Assert.False(box.IsSlotOpen);
    }

    [Theory]
    [InlineData(0x10C)] // present but empty
    [InlineData(0x1AC)] // absent
    public void AnEmptyOrAbsentText_IsRefused_AfterOneEtcLookup(int id)
    {
        var resolver = new CountingResolver();
        var box = new AlundraDialogueNameBox();

        Assert.False(box.TryOpen(id, resolver.Resolve, SpeakerRig.Advance));
        Assert.Equal(1, resolver.Calls);
        Assert.False(box.IsSlotOpen);
        Assert.Equal(0, box.Flags);
    }

    [Fact]
    public void TheNameBox_ClosingDuringTheSlideIn_LeavesFromTheCurrentX_AndIsReleasedAfterSeventeenPasses()
    {
        var box = new AlundraDialogueNameBox();
        Assert.True(box.TryOpen(0x104, _ => "Jess", SpeakerRig.Advance));
        for (var pass = 1; pass <= 4; pass++)
        {
            box.Pass();
        }

        Assert.Equal(269, box.Drawn!.FrameX); // pass 4: 320 + trunc(-256 * 3 / 15)
        box.Close();
        Assert.Equal(AlundraDialogueNameBox.FlagClosing | AlundraDialogueNameBox.FlagOpen, box.Flags);

        box.Pass();
        Assert.Equal(269, box.Drawn!.FrameX); // closing pass 0: from where it is
        for (var pass = 2; pass <= 17; pass++)
        {
            box.Pass();
            Assert.NotNull(box.Drawn);
        }

        box.Pass(); // the release: nothing is drawn
        Assert.Null(box.Drawn);
        Assert.False(box.IsSlotOpen);
        Assert.Equal(0, box.Flags);
        Assert.Equal(64, box.X);
    }
}
