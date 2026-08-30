using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T3 of docs/plan-e11c-musique.md, slice C1: drives <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective(int, out int)"/> -
/// the SAME production resolution API <see cref="AlundraMusicPlayer.PlayMapMusic(int)"/> itself calls -
/// off the REAL converter export's own <c>Maps/music-index.json</c>, at several raw values (fact 1.1):
/// <c>0</c> (map 0, total short-circuit), <c>45</c> (map 476, stop only), <c>-1</c> (map 183, remapped to
/// index 1), and a real index that is NOT 25 (map 45, raw 30) alongside map 389 itself (raw 25) - so a
/// mutation that hard-codes 25 at the resolution site is actually caught (see the plan's own mutation
/// table).
/// </summary>
public class AlundraMusicIndexTableTests
{
    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraMusicIndexTableTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' (docs/plan-e11c-musique.md, slice C1).");
    }

    [Fact]
    public void ResolvePlaybackDirective_Map389_RawIndex25_ResolvesToPlay25()
    {
        var table = new AlundraMusicIndexTable(FindProjectRoot());

        var directive = table.ResolvePlaybackDirective(389, out var raw);

        Assert.Equal(25, raw);
        Assert.Equal(MusicPlaybackDirectiveKind.Play, directive.Kind);
        Assert.Equal(25, directive.PlayIndex);
    }

    [Fact]
    public void ResolvePlaybackDirective_Map45_RawIndex30_ResolvesToPlay30_NotTheMap389Value()
    {
        // Fact 2.1's own recount: map 45's raw table entry is 30, not 25 - a resolution site that
        // hard-codes 25 would fail THIS assertion while still passing the map-389 one above.
        var table = new AlundraMusicIndexTable(FindProjectRoot());

        var directive = table.ResolvePlaybackDirective(45, out var raw);

        Assert.Equal(30, raw);
        Assert.Equal(MusicPlaybackDirectiveKind.Play, directive.Kind);
        Assert.Equal(30, directive.PlayIndex);
    }

    [Fact]
    public void ResolvePlaybackDirective_Map0_RawIndex0_ResolvesToNoOp()
    {
        var table = new AlundraMusicIndexTable(FindProjectRoot());

        var directive = table.ResolvePlaybackDirective(0, out var raw);

        Assert.Equal(0, raw);
        Assert.Equal(MusicPlaybackDirectiveKind.NoOp, directive.Kind);
    }

    [Fact]
    public void ResolvePlaybackDirective_Map476_RawIndex45_ResolvesToStop()
    {
        var table = new AlundraMusicIndexTable(FindProjectRoot());

        var directive = table.ResolvePlaybackDirective(476, out var raw);

        Assert.Equal(45, raw);
        Assert.Equal(MusicPlaybackDirectiveKind.Stop, directive.Kind);
    }

    [Fact]
    public void ResolvePlaybackDirective_Map183_RawIndexMinusOne_ResolvesToPlayIndex1()
    {
        var table = new AlundraMusicIndexTable(FindProjectRoot());

        var directive = table.ResolvePlaybackDirective(183, out var raw);

        Assert.Equal(-1, raw);
        Assert.Equal(MusicPlaybackDirectiveKind.Play, directive.Kind);
        Assert.Equal(1, directive.PlayIndex); // fact 1.1: -1 plays index 1, NOT "no music"
    }

    // ---- T3 bis, DLL half: the degraded branch, same model as BackdropLoader ----------------------

    [Fact]
    public void MissingMusicIndexFile_EveryLookupMisses_ResolvesToNoOp_NoException()
    {
        var emptyProjectPath = Path.Combine(Path.GetTempPath(), "AlundraMusicIndexTableTests_Missing_" + Guid.NewGuid());
        Directory.CreateDirectory(emptyProjectPath); // no Maps/music-index.json inside

        try
        {
            AlundraMusicIndexTable table = null!;
            var ex = Record.Exception(() => table = new AlundraMusicIndexTable(emptyProjectPath));
            Assert.Null(ex);

            Assert.False(table.TryGetRawIndex(389, out _));
            var directive = table.ResolvePlaybackDirective(389, out var raw);
            Assert.Equal(0, raw);
            Assert.Equal(MusicPlaybackDirectiveKind.NoOp, directive.Kind);
        }
        finally
        {
            Directory.Delete(emptyProjectPath, recursive: true);
        }
    }

    [Fact]
    public void MalformedMusicIndexFile_EveryLookupMisses_ResolvesToNoOp_NoException()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraMusicIndexTableTests_Malformed_" + Guid.NewGuid());
        var mapsDir = Path.Combine(projectPath, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "music-index.json"), "{ not valid json ][");

        try
        {
            AlundraMusicIndexTable table = null!;
            var ex = Record.Exception(() => table = new AlundraMusicIndexTable(projectPath));
            Assert.Null(ex);

            Assert.False(table.TryGetRawIndex(389, out _));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }
}
