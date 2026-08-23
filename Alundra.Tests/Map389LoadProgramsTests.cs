using System;
using System.Collections.Generic;
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

    /// <summary>Records SpawnEntityByRecordId/DestroyEntity calls and hands back a fixed spawned-entity
    /// list - drives loads 139/140/143's real 0x64/0x2E path exactly the way AlundraWorldProxy would,
    /// without a live World.</summary>
    private sealed class RecordingWorldContext : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> SpawnedEntitiesList { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => SpawnedEntitiesList;
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public readonly List<AlundraEntityScriptProxy> DestroyedEntities = new();

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity) => DestroyedEntities.Add(entity);
    }

    /// <summary>
    /// Under New Game's all-zero flags, flag 860 is OFF - the "IfFlagOff(860)" branch of loads 139/140/143
    /// is taken straight to end (0xFF), before ever reaching their 0x64/0x2E instruction (see the
    /// decoded-effects test below, which forces flag 860 ON instead). This mirrors
    /// Map389_SailorLoadPrograms_EndOnRealAnimations_NotZero's own "flag off" default and confirms the
    /// entity search/manipulation opcodes stay dormant (no spawn/destroy calls, no position writes) on a
    /// fresh save, exactly like every other opcode gated behind that same flag on this map.
    /// </summary>
    [Fact]
    public void Map389_Load139_140_143_FlagOff_NeverReachTheirManipulationOpcode()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        var context = new RecordingWorldContext();
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);

        var e11 = new AlundraEntityScriptProxy { PosX = 111, PosY = 222, PosZ = 333 };
        e11.ProgramIndexes[ScriptHelper.ProgramALoad] = 139;
        runner.RunScript(e11, ScriptHelper.ProgramALoad);

        Assert.Equal(0u, e11.TargetAnimationId); // SetAnim(0) ran, but the flag-off branch ends right after
        Assert.Equal(111, e11.PosX);
        Assert.Equal(222, e11.PosY);
        Assert.Equal(333, e11.PosZ);
        Assert.Empty(context.DestroyedEntities);
    }

    /// <summary>
    /// Decoded effects of loads 139 (E11), 140 (E12) and 143 (E15) once flag 860 is ON: all three fall
    /// through their "IfFlagOff(860)" check (see the brief's own worked decode of events offset
    /// 232/252/280) straight into their real entity-manipulation opcode, each searching "get owner"
    /// (0x80, functionId 0 - matches the entity running the script itself, per
    /// <see cref="EntitySearchService"/>).
    /// </summary>
    [Fact]
    public void Map389_Load139_140_143_FlagOn_RunTheirRealManipulationOpcode()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        var gameState = new AlundraGameState();
        gameState.AddFlag(860, 1u << (0x5c & 0x1f)); // the exact flag/bit these three programs test
        var context = new RecordingWorldContext();
        var runner = new AlundraEventProgramRunner(document, gameState, context);

        // Load 139 (E11): SetAnim(0); flag on -> 0x64 SetEntitiesPosition(owner) then SetAnim(1).
        var e11 = new AlundraEntityScriptProxy();
        e11.ProgramIndexes[ScriptHelper.ProgramALoad] = 139;
        runner.RunScript(e11, ScriptHelper.ProgramALoad);

        Assert.Equal(1u, e11.TargetAnimationId);
        Assert.Equal(0x234 << 16, e11.PosX);
        Assert.Equal(0x178 << 16, e11.PosY);
        Assert.Equal((0xa0 << 16) + 1, e11.PosZ);

        // Load 140 (E12): SetAnim(0); flag on -> 0x64 SetEntitiesPosition(owner) then SetDirection(0).
        var e12 = new AlundraEntityScriptProxy { TargetDirection = 3 };
        e12.ProgramIndexes[ScriptHelper.ProgramALoad] = 140;
        runner.RunScript(e12, ScriptHelper.ProgramALoad);

        Assert.Equal(0u, e12.TargetAnimationId);
        Assert.Equal(0x1d4 << 16, e12.PosX);
        Assert.Equal(0x340 << 16, e12.PosY);
        Assert.Equal((0x80 << 16) + 1, e12.PosZ);
        Assert.Equal(0u, e12.TargetDirection);

        // Load 143 (E15): SetAnim(5); flag on -> 0x2E DestroyEntity(owner).
        var e15 = new AlundraEntityScriptProxy();
        e15.ProgramIndexes[ScriptHelper.ProgramALoad] = 143;
        runner.RunScript(e15, ScriptHelper.ProgramALoad);

        Assert.Equal(5u, e15.TargetAnimationId);
        Assert.Equal(new[] { e15 }, context.DestroyedEntities);
    }
}
