#nullable enable
using Alundra.Scripts;
using Xunit;
using Alundra.Session;
using Alundra.Events;

namespace Alundra.Tests;

/// <summary>
/// E19.m9 (docs/plan-e19-opcodes.md section 1.2s.10, O-E19-34): the binary's <c>InitializeEventData</c> (0x80041EE4) writes only the
/// program's start (+0, <c>Parameters[0]</c>) and the pc (+4); the marker and the data of a pending wait (<c>Parameters[1..9]</c>, +8 to
/// +0x28) survive a re-initialization. It matters only for a program re-initialized while its FIRST instruction waits: any instruction
/// that advances puts the marker back to 0. Values from <c>docs/plan-e19-m9-annexe/dynamic.md</c>.
/// </summary>
public sealed class AlundraInitializeEventDataTests
{
    /// <summary>@0 0x01, @1 0xFF, then the program at @2 (never at pc 0, where a zero marker would already match):
    /// <c>0x37 n</c> (Wait), <c>0x1A 9</c> (SetAnim 9), <c>0xFF</c>.</summary>
    private static EventProgramDocument WaitThenSetAnim9(int n) => new()
    {
        EventCodesCTable = new[] { 2 },
        EventCodesDTable = new[] { 2 },
        Codes = new[] { 0x01, 0xFF, 0x37, n, 0x1A, 9, 0xFF },
    };

    [Fact]
    public void M9a_ASlotDProgramWhoseFirstInstructionWaits_CountsAcrossItsReInitializations_AndEndsAtTheCallNPlus2()
    {
        // D, like A, E and F, is re-initialized at every call (one shared state): the binary keeps the marker of the wait, so the count goes
        // on across calls; the port used to clear it and the wait never ended.
        var runner = new EventProgramRunner(WaitThenSetAnim9(3), new GameState());
        var entity = new AlundraEntityScriptProxy();
        entity.ProgramIndexes[ScriptHelper.ProgramDTouch] = 0x80;

        for (var call = 1; call <= 4; call++)
        {
            runner.RunScript(entity, ScriptHelper.ProgramDTouch);
            Assert.Equal(0u, entity.TargetAnimationId);
        }

        runner.RunScript(entity, ScriptHelper.ProgramDTouch);
        Assert.Equal(9u, entity.TargetAnimationId);
    }

    [Fact]
    public void M9b_RealMap392B3_AHeroWhoLeavesTheZoneOnceDuringTheWait_ContinuesTheCount_62TicksInTheZone()
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var (document, _) = AlundraStoryChainOpcodeAudit.LoadMap(root, 392);
        const int entry = 156;
        Assert.Equal(entry, document.EventCodesBTable[3]);
        Assert.Equal(0x37, document.Codes[entry]);
        Assert.Equal(60, document.Codes[entry + 1]);

        // One opcode per call (the measurement's montage): the call that passes the wait stops right after it, so nothing past the wait
        // needs a world. The zone is made up; the hero leaves it for one tick after 31 ticks in it, then comes back.
        var passed = false;
        var runner = new EventProgramRunner(document, new GameState()) { MaxIterationsPerCall = 1 };
        runner.TraceSink = r =>
        {
            if (r.CodeIndex == entry && r.Kind == EventTraceKind.Implemented && r.Size != 0)
            {
                passed = true;
            }
        };
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, TileY = 5 };
        var mapEvent = new MapEvent { Id = 0, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10, ProgramBMap = 3 | 0x80, OriginalProgramBMap = 3 | 0x80 };

        var ticksInTheZone = 0;
        for (var tick = 1; tick <= 200 && !passed; tick++)
        {
            var inside = tick != 32;
            hero.TileX = inside ? 5 : 50;
            AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);
            if (inside)
            {
                ticksInTheZone++;
            }
        }

        Assert.True(passed, "the wait of 392 B[3] never ended");
        Assert.Equal(62, ticksInTheZone);
    }

    [Fact]
    public void M9c_ASlotCProgramClearedDuringItsFirstWait_ContinuesTheCount_AndEndsAtTheCallNPlus2()
    {
        // C has the entity's own state: cleared by hand after 2 calls as EndCall clears a running state (Sp = 0, Codes = null), the next call
        // re-initializes it, and the kept marker lets the count go on (the port used to restart it: 2 calls more).
        var runner = new EventProgramRunner(WaitThenSetAnim9(3), new GameState());
        var entity = new AlundraEntityScriptProxy();
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;

        for (var call = 1; call <= 4; call++)
        {
            runner.RunScript(entity, ScriptHelper.ProgramCTick);
            Assert.Equal(0u, entity.TargetAnimationId);
            if (call == 2)
            {
                entity.EventProgramState.Sp = 0;
                entity.EventProgramState.Codes = null;
            }
        }

        runner.RunScript(entity, ScriptHelper.ProgramCTick);
        Assert.Equal(9u, entity.TargetAnimationId);
    }

    [Fact]
    public void M9d_AReInitializationWritesTheStartAndThePc()
    {
        var runner = new EventProgramRunner(WaitThenSetAnim9(3), new GameState());
        var entity = new AlundraEntityScriptProxy();
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;

        runner.RunScript(entity, ScriptHelper.ProgramCTick);

        Assert.Equal(2, entity.EventProgramState.Parameters[0]);
        Assert.Equal(2, entity.EventProgramState.CodeIndex);
    }
}
