using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T3 of docs/plan-e11-audio.md, slice E11.a: <see cref="AlundraSoundBank.TryResolve"/> as pure data,
/// against the real <c>Sounds/sfx-manifest.json</c> export (no synthetic fixture - every fact asserted
/// here is read straight off the manifest itself, same self-skip-forbidden shape as
/// <see cref="AlundraCellStoreProductionTests"/>).
/// </summary>
public class AlundraSoundBankTests
{
    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Sounds")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraSoundBankTests: no 'alundra-project/Sounds' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export and cannot "
            + "self-skip without one (docs/plan-e11-audio.md, slice E11.a).");
    }

    [Fact]
    public void TryResolve_Id302_TwoLoopedTonesAtTheirOwnDetunedRates()
    {
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(302, soundGroup: null, out var resolution);

        Assert.True(resolved);
        Assert.Equal(302, resolution.ResolvedId);
        Assert.Equal(56, resolution.VabId);
        Assert.Equal(1, resolution.MaxVoices);
        Assert.Equal(2, resolution.Tones.Count);

        Assert.Equal("sfx_0302_0.wav", resolution.Tones[0].File);
        Assert.Equal(11025, resolution.Tones[0].SampleRate);
        Assert.True(resolution.Tones[0].Repeat);

        Assert.Equal("sfx_0302_1.wav", resolution.Tones[1].File);
        Assert.Equal(10401, resolution.Tones[1].SampleRate);
        Assert.True(resolution.Tones[1].Repeat);
    }

    [Fact]
    public void TryResolve_Id61_GlobalVab()
    {
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(61, soundGroup: null, out var resolution);

        Assert.True(resolved);
        Assert.Equal(-1, resolution.VabId); // global - playable from any map (§1.4).
        Assert.Single(resolution.Tones);
        Assert.Equal("sfx_0061.wav", resolution.Tones[0].File);
    }

    [Fact]
    public void TryResolve_DisabledOrNoToneId_FailsSoftly()
    {
        // id 9: vab_id -2 (disabled), no tones - one of the manifest's own 91 "skip_reason" entries (§1.2).
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(9, soundGroup: null, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_UnknownId_FailsSoftly()
    {
        var bank = new AlundraSoundBank(FindProjectRoot());

        Assert.False(bank.TryResolve(999999, soundGroup: null, out _));
    }

    [Fact]
    public void TryResolve_WithoutGroup_PlaysItsOwnTones_NotTheRefSfxIdChain()
    {
        // id 303: vab_id 17, ref_sfx_id 835 (835's own vab_id is 56). D-E11-6: no group supplied ->
        // record with VabId >= 0 plays ITS OWN tones, never redirected by the chain.
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(303, soundGroup: null, out var resolution);

        Assert.True(resolved);
        Assert.Equal(303, resolution.ResolvedId);
        Assert.Equal(17, resolution.VabId);
        Assert.Equal("sfx_0303_0.wav", resolution.Tones[0].File);
    }

    [Fact]
    public void TryResolve_WithGroup_DifferentFromOwnVab_FollowsRefSfxIdChainToTheSibling()
    {
        // Same id 303 as above, but this time under group 56 (its OWN vab_id, 17, differs from it) - the
        // ref_sfx_id chain (303 -> 835) is followed, and 835's own vab_id (56) matches the requested
        // group, so 835's tones are used instead - the one case the group changes anything (§1.4).
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(303, soundGroup: 56, out var resolution);

        Assert.True(resolved);
        Assert.Equal(835, resolution.ResolvedId); // resolved to the SIBLING, not 303 itself.
        Assert.Equal(56, resolution.VabId);
        Assert.Equal("sfx_0835_0.wav", resolution.Tones[0].File);
        Assert.Equal("sfx_0835_1.wav", resolution.Tones[1].File);
    }

    [Fact]
    public void TryResolve_WithGroup_MatchingOwnVab_PlaysItsOwnTones()
    {
        // id 300: vab_id 56 - requesting under group 56 (the map's own) matches directly, no chain walk.
        var bank = new AlundraSoundBank(FindProjectRoot());

        var resolved = bank.TryResolve(300, soundGroup: 56, out var resolution);

        Assert.True(resolved);
        Assert.Equal(300, resolution.ResolvedId);
        Assert.Equal("sfx_0300.wav", resolution.Tones[0].File);
    }
    [Fact]
    public void TryResolve_Id302_CarriesItsVabVolumeAndPanAttributes()
    {
        // docs/plan-audio-mix-exact-muet.md, T4.1 / ADR-0003: the two looped tones of 302 sit left and right
        // (tone pans 34 and 94), at VAB and program volume 127 and program pan 64.
        var bank = new AlundraSoundBank(FindProjectRoot());

        Assert.True(bank.TryResolve(302, soundGroup: 56, out var resolution));

        Assert.Equal(127, resolution.VabMasterVolume);
        Assert.Equal(127, resolution.ProgramVolume);
        Assert.Equal(64, resolution.ProgramPan);
        Assert.Equal(100, resolution.Tones[0].Volume);
        Assert.Equal(34, resolution.Tones[0].Pan);
        Assert.Equal(100, resolution.Tones[1].Volume);
        Assert.Equal(94, resolution.Tones[1].Pan);
    }

    [Fact]
    public void TryResolve_WithGroup_FollowingTheChain_TakesTheSiblingsAttributes()
    {
        // 303 (vab 17) carries tone pans 0 and 127; its sibling 835 (vab 56) carries 34 and 94. Under group 56 the
        // tones played are 835's, so their attributes must be 835's too, never 303's.
        var bank = new AlundraSoundBank(FindProjectRoot());

        Assert.True(bank.TryResolve(303, soundGroup: 56, out var resolution));

        Assert.Equal(835, resolution.ResolvedId);
        Assert.Equal(34, resolution.Tones[0].Pan);
        Assert.Equal(94, resolution.Tones[1].Pan);
    }

    [Fact]
    public void TryResolve_Id162_KeepsTheOriginalsSilentTone()
    {
        // Tone 1 of 162 has a volume of 0 in the original: a real 0, not a missing value.
        var bank = new AlundraSoundBank(FindProjectRoot());

        Assert.True(bank.TryResolve(162, soundGroup: 12, out var resolution));

        Assert.Equal(0, resolution.Tones[1].Volume);
        Assert.Equal(0, resolution.Tones[1].Pan);
    }
}

/// <summary>
/// docs/plan-audio-mix-exact-muet.md, T4.1 (ADR-0003): the VAB volume/pan attributes of a resolution are those of
/// the record whose tones are played, and a manifest without them (or with null) gives absent values, never 0. The
/// real manifest's program-level values are uniform (127/127/64 everywhere), so a synthetic fixture with distinct
/// values is the only way to tell a sibling's program attributes from the requested record's.
/// </summary>
public class AlundraSoundBankAttributeTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundBankAttributeTests_" + Guid.NewGuid());

    public AlundraSoundBankAttributeTests()
    {
        var soundsDirectory = Path.Combine(_projectPath, "Sounds");
        Directory.CreateDirectory(soundsDirectory);

        // 600 (vab 10) -> ref_sfx_id 601 (vab 56); 602 has no attribute at all (older manifest); 603 carries null.
        var json = """
        [
          { "id": 600, "vab_id": 10, "ref_sfx_id": 601, "max_voices": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0600.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000600", "volume": 50, "pan": 20 } ],
            "vab_master_volume": 100, "program_volume": 90, "program_pan": 10 },
          { "id": 601, "vab_id": 56, "ref_sfx_id": 0, "max_voices": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0601.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000601", "volume": 70, "pan": 80 } ],
            "vab_master_volume": 120, "program_volume": 110, "program_pan": 90 },
          { "id": 602, "vab_id": -1, "ref_sfx_id": 0, "max_voices": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0602.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000602" } ] },
          { "id": 603, "vab_id": -1, "ref_sfx_id": 0, "max_voices": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0603.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000603", "volume": null, "pan": null } ],
            "vab_master_volume": null, "program_volume": null, "program_pan": null }
        ]
        """;

        File.WriteAllText(Path.Combine(soundsDirectory, "sfx-manifest.json"), json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_UnderRedirection_TakesEveryAttributeFromTheSibling()
    {
        var bank = new AlundraSoundBank(_projectPath);

        Assert.True(bank.TryResolve(600, soundGroup: 56, out var resolution));

        Assert.Equal(601, resolution.ResolvedId);
        Assert.Equal(120, resolution.VabMasterVolume);
        Assert.Equal(110, resolution.ProgramVolume);
        Assert.Equal(90, resolution.ProgramPan);
        Assert.Equal(70, resolution.Tones[0].Volume);
        Assert.Equal(80, resolution.Tones[0].Pan);
    }

    [Fact]
    public void TryResolve_WithoutRedirection_TakesItsOwnAttributes()
    {
        var bank = new AlundraSoundBank(_projectPath);

        Assert.True(bank.TryResolve(600, soundGroup: 10, out var resolution));

        Assert.Equal(600, resolution.ResolvedId);
        Assert.Equal(100, resolution.VabMasterVolume);
        Assert.Equal(90, resolution.ProgramVolume);
        Assert.Equal(10, resolution.ProgramPan);
        Assert.Equal(50, resolution.Tones[0].Volume);
        Assert.Equal(20, resolution.Tones[0].Pan);
    }

    [Theory]
    [InlineData(602)]
    [InlineData(603)]
    public void TryResolve_WithMissingOrNullAttributes_LeavesThemAbsent(int sfxId)
    {
        var bank = new AlundraSoundBank(_projectPath);

        Assert.True(bank.TryResolve(sfxId, soundGroup: null, out var resolution));

        Assert.Null(resolution.VabMasterVolume);
        Assert.Null(resolution.ProgramVolume);
        Assert.Null(resolution.ProgramPan);
        Assert.Null(resolution.Tones[0].Volume);
        Assert.Null(resolution.Tones[0].Pan);
    }
}

/// <summary>
/// A dedicated MULTI-HOP synthetic fixture for the RefSfxId chain (T3, mutation 5 of
/// docs/plan-e11-audio.md's own table): the real manifest's own 303-&gt;835 chain (see
/// <see cref="AlundraSoundBankTests"/> above) is only ONE hop long, so a mutant that stops at the
/// FIRST sibling regardless of its group - ignoring the group entirely - would still pass it (the first
/// hop already happens to be the right answer there). This fixture's chain is two hops long, with the
/// FIRST sibling belonging to the WRONG group, so that exact mutation picks the wrong record.
/// </summary>
public class AlundraSoundBankChainTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundBankChainTests_" + Guid.NewGuid());

    public AlundraSoundBankChainTests()
    {
        var soundsDirectory = Path.Combine(_projectPath, "Sounds");
        Directory.CreateDirectory(soundsDirectory);

        // id 500 (vab 10) -> ref_sfx_id 501 (vab 20, WRONG group) -> ref_sfx_id 502 (vab 56, the
        // requested group). Resolving id 500 under group 56 must walk PAST 501 to reach 502.
        var json = """
        [
          { "id": 500, "vab_id": 10, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1, "ref_sfx_id": 501, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0500.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000500" } ] },
          { "id": 501, "vab_id": 20, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1, "ref_sfx_id": 502, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0501.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000501" } ] },
          { "id": 502, "vab_id": 56, "program_number": 0, "tone_number": 0, "note": 60, "seq_num": -1, "ref_sfx_id": 0, "max_voices": 1, "num_tones": 1, "skip_reason": null,
            "tones": [ { "tone_index": 0, "file": "sfx_0502.wav", "sample_rate": 11025, "loop_start": 0, "loop_end": 100, "repeat": false, "asset_id": "00000000-0000-0000-0000-000000000502" } ] }
        ]
        """;

        File.WriteAllText(Path.Combine(soundsDirectory, "sfx-manifest.json"), json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_WithGroup_TwoHopChain_SkipsTheWrongFirstSiblingToReachTheRightOne()
    {
        var bank = new AlundraSoundBank(_projectPath);

        var resolved = bank.TryResolve(500, soundGroup: 56, out var resolution);

        Assert.True(resolved);
        Assert.Equal(502, resolution.ResolvedId); // NOT 501 - the wrong-group first hop.
        Assert.Equal("sfx_0502.wav", resolution.Tones[0].File);
    }
}
