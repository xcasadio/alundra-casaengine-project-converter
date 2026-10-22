using System;
using System.IO;
using Xunit;
using Alundra.Audio;
using Alundra.Tests.Cells;

namespace Alundra.Tests.Audio;

/// <summary>
/// T3 of docs/plan-e11-audio.md, slice E11.a: <see cref="SfxBank.TryResolve"/> as pure data,
/// against the real <c>Sounds/sfx-manifest.json</c> export (no synthetic fixture - every fact asserted
/// here is read straight off the manifest itself, same self-skip-forbidden shape as
/// <see cref="CellStoreProductionTests"/>).
/// </summary>
public class SoundBankTests
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
            $"SoundBankTests: no 'alundra-project/Sounds' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export and cannot "
            + "self-skip without one (docs/plan-e11-audio.md, slice E11.a).");
    }

    [Fact]
    public void TryResolve_Id302_TwoLoopedTonesAtTheirOwnDetunedRates()
    {
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

        var resolved = bank.TryResolve(9, soundGroup: null, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_UnknownId_FailsSoftly()
    {
        var bank = new SfxBank(FindProjectRoot());

        Assert.False(bank.TryResolve(999999, soundGroup: null, out _));
    }

    [Fact]
    public void TryResolve_WithoutGroup_PlaysItsOwnTones_NotTheRefSfxIdChain()
    {
        // id 303: vab_id 17, ref_sfx_id 835 (835's own vab_id is 56). D-E11-6: no group supplied ->
        // record with VabId >= 0 plays ITS OWN tones, never redirected by the chain.
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

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
        var bank = new SfxBank(FindProjectRoot());

        Assert.True(bank.TryResolve(303, soundGroup: 56, out var resolution));

        Assert.Equal(835, resolution.ResolvedId);
        Assert.Equal(34, resolution.Tones[0].Pan);
        Assert.Equal(94, resolution.Tones[1].Pan);
    }

    [Fact]
    public void TryResolve_Id162_KeepsTheOriginalsSilentTone()
    {
        // Tone 1 of 162 has a volume of 0 in the original: a real 0, not a missing value.
        var bank = new SfxBank(FindProjectRoot());

        Assert.True(bank.TryResolve(162, soundGroup: 12, out var resolution));

        Assert.Equal(0, resolution.Tones[1].Volume);
        Assert.Equal(0, resolution.Tones[1].Pan);
    }
}
