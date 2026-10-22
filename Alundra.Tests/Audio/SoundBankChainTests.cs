using System;
using System.IO;
using Xunit;
using Alundra.Audio;
using Alundra.Tests.Cells;

namespace Alundra.Tests.Audio;

/// <summary>
/// A dedicated MULTI-HOP synthetic fixture for the RefSfxId chain (T3, mutation 5 of
/// docs/plan-e11-audio.md's own table): the real manifest's own 303-&gt;835 chain (see
/// <see cref="SoundBankTests"/> above) is only ONE hop long, so a mutant that stops at the
/// FIRST sibling regardless of its group - ignoring the group entirely - would still pass it (the first
/// hop already happens to be the right answer there). This fixture's chain is two hops long, with the
/// FIRST sibling belonging to the WRONG group, so that exact mutation picks the wrong record.
/// </summary>
public class SoundBankChainTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "SoundBankChainTests_" + Guid.NewGuid());

    public SoundBankChainTests()
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
        var bank = new SfxBank(_projectPath);

        var resolved = bank.TryResolve(500, soundGroup: 56, out var resolution);

        Assert.True(resolved);
        Assert.Equal(502, resolution.ResolvedId); // NOT 501 - the wrong-group first hop.
        Assert.Equal("sfx_0502.wav", resolution.Tones[0].File);
    }
}
