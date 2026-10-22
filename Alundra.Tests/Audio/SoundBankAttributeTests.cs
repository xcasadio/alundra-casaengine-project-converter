using System;
using System.IO;
using Xunit;
using Alundra.Audio;
using Alundra.Tests.Cells;

namespace Alundra.Tests.Audio;

/// <summary>
/// docs/plan-audio-mix-exact-muet.md, T4.1 (ADR-0003): the VAB volume/pan attributes of a resolution are those of
/// the record whose tones are played, and a manifest without them (or with null) gives absent values, never 0. The
/// real manifest's program-level values are uniform (127/127/64 everywhere), so a synthetic fixture with distinct
/// values is the only way to tell a sibling's program attributes from the requested record's.
/// </summary>
public class SoundBankAttributeTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "SoundBankAttributeTests_" + Guid.NewGuid());

    public SoundBankAttributeTests()
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
        var bank = new SfxBank(_projectPath);

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
        var bank = new SfxBank(_projectPath);

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
        var bank = new SfxBank(_projectPath);

        Assert.True(bank.TryResolve(sfxId, soundGroup: null, out var resolution));

        Assert.Null(resolution.VabMasterVolume);
        Assert.Null(resolution.ProgramVolume);
        Assert.Null(resolution.ProgramPan);
        Assert.Null(resolution.Tones[0].Volume);
        Assert.Null(resolution.Tones[0].Pan);
    }
}
