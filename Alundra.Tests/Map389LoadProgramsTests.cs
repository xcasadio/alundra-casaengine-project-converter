using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Runs the real map 389 ("Ship Klark (beginning)") Load programs end to end, off the converter's own
/// output in alundra-project/ - self-skips when that directory is absent (it is regenerable output, not
/// guaranteed to exist on a fresh clone; the synthetic tests in
/// <see cref="AlundraEventProgramRunnerTests"/> carry the interpreter's correctness guarantees on their
/// own). Exercises the exact scenario the interpreter was built for: sailors ending up on their real
/// Load-program animation instead of TargetAnimationId staying 0.
/// </summary>
public class Map389LoadProgramsTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    private static string? FindProjectRoot()
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

        return null;
    }

    /// <summary>Runs one entity's slot-A Load program to completion and returns its final
    /// TargetAnimationId, mirroring what AlundraWorldProxy's pick phase does on the frame it spawns.</summary>
    private static uint RunLoad(AlundraEventProgramRunner runner, int loadIndex)
    {
        var entity = new AlundraEntityScriptProxy();
        entity.ProgramIndexes[ScriptHelper.ProgramALoad] = loadIndex;
        runner.RunScript(entity, ScriptHelper.ProgramALoad);
        return entity.TargetAnimationId;
    }

    [Fact]
    public void Map389_SailorLoadPrograms_EndOnRealAnimations_NotZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());

        // Entities 6-9 (EventCodesA_LoadIndex 134-137) all fall (under New Game's all-zero flags)
        // through their "if flag off" branch straight to a nonzero SetAnim, per the decoded programs at
        // events offsets 136/176/212/220 (see the brief's report for the full listing).
        Assert.Equal(10u, RunLoad(runner, 134));
        Assert.Equal(10u, RunLoad(runner, 135));
        Assert.Equal(10u, RunLoad(runner, 136));
        Assert.Equal(10u, RunLoad(runner, 137));

        // Entities 13/14 (loadIndex 141/142) and 16/17 (144/145) set their animation unconditionally.
        Assert.Equal(1u, RunLoad(runner, 141));
        Assert.Equal(1u, RunLoad(runner, 142));
        Assert.Equal(9u, RunLoad(runner, 144));
        Assert.Equal(9u, RunLoad(runner, 145));
    }
}
