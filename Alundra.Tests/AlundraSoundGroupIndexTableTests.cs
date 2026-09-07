using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// B3 of docs/plan-e11b-opcodes-audio.md, D-B-7: drives <see cref="AlundraSoundGroupIndexTable"/>
/// directly off small synthetic fixtures - the real linked <c>Maps/sound-group-index.json</c> is not
/// re-exported by this slice (full export is out of scope here), so the real-project positive-lookup
/// coverage lives on the converter side (<c>WorldWriterTests.ConvertWorlds_WritesSoundGroupIndex_AllEntries_Map389Is56</c>)
/// and on the redirection tests in <see cref="AlundraSoundPlayerTests"/>, which construct their own
/// player with an explicit <c>soundGroup</c> instead of loading this table from disk.
/// </summary>
public class AlundraSoundGroupIndexTableTests
{
    [Fact]
    public void TryGetGroup_LoadsEveryEntry()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundGroupIndexTableTests_" + Guid.NewGuid());
        var mapsDir = Path.Combine(projectPath, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "sound-group-index.json"), """{"0":0,"1":46,"389":56}""");

        try
        {
            var table = new AlundraSoundGroupIndexTable(projectPath);

            Assert.True(table.TryGetGroup(389, out var group));
            Assert.Equal(56, group);
            Assert.True(table.TryGetGroup(1, out var group1));
            Assert.Equal(46, group1);
            Assert.False(table.TryGetGroup(999, out _)); // outside the table.
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void MissingSoundGroupIndexFile_EveryLookupMisses_NoException()
    {
        var emptyProjectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundGroupIndexTableTests_Missing_" + Guid.NewGuid());
        Directory.CreateDirectory(emptyProjectPath); // no Maps/sound-group-index.json inside

        try
        {
            AlundraSoundGroupIndexTable table = null!;
            var ex = Record.Exception(() => table = new AlundraSoundGroupIndexTable(emptyProjectPath));
            Assert.Null(ex);

            Assert.False(table.TryGetGroup(389, out _));
        }
        finally
        {
            Directory.Delete(emptyProjectPath, recursive: true);
        }
    }

    [Fact]
    public void MalformedSoundGroupIndexFile_EveryLookupMisses_NoException()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraSoundGroupIndexTableTests_Malformed_" + Guid.NewGuid());
        var mapsDir = Path.Combine(projectPath, "Maps");
        Directory.CreateDirectory(mapsDir);
        File.WriteAllText(Path.Combine(mapsDir, "sound-group-index.json"), "{ not valid json ][");

        try
        {
            AlundraSoundGroupIndexTable table = null!;
            var ex = Record.Exception(() => table = new AlundraSoundGroupIndexTable(projectPath));
            Assert.Null(ex);

            Assert.False(table.TryGetGroup(389, out _));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }
}
