#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d D6 (docs/plan-e19-opcodes.md §1.2g, D-E19-22), tests U5 to U13 (U9 abandoned, no site in the corpus): opcodes <c>0x40</c> (Set program
/// index, <c>0x8003E7B8</c>) and <c>0x41</c> (Set sprite program index, <c>0x8003E7E4</c>), size 3, written on the LOGIC entity. <c>0x40</c>
/// also raises <c>g_clearProgramState</c>: <c>RunScript</c> resets it at the start of the call and tests it after every opcode (binary
/// <c>0x80042310</c>) - when the logic entity read before the opcode is the owner, the state that runs is cleared at the end of the call (on a
/// Break or a suspension too, whatever v1); otherwise the logic entity's own state is cleared at once. A program slot v1 of 6 or more does
/// nothing (the original would write past its array), with one warning per opcode.
/// </summary>
public class AlundraEventProgramRunnerProgramIndexTests
{
    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        EventCodesCTable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private sealed class FakeWorld : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;

        public AlundraEntityScriptProxy? PlayerEntity => null;

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

        public NavigationGrid2D? NavigationGrid => null;
    }

    private static (AlundraEventProgramRunner Runner, List<EventTraceRecord> Trace) NewRunner(EventProgramDocument document, IEntityWorldContext? world = null)
    {
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), world);
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        return (runner, trace);
    }

    private static AlundraEntityScriptProxy Entity(int record)
        => new() { EntityRefId = record, Status = EntityStatus.Normal };

    private static EventProgramState StateFor(EventProgramDocument document) => new() { Codes = document.CodesAsBytes() };

    private static (int CodeIndex, int Opcode, int Size)[] Steps(IEnumerable<EventTraceRecord> trace)
        => trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray();

    private static void GiveARunningState(AlundraEntityScriptProxy entity, EventProgramDocument document, int codeIndex)
    {
        entity.EventProgramState.Codes = document.CodesAsBytes();
        entity.EventProgramState.CodeIndex = codeIndex;
        entity.EventProgramState.Sp = 1;
    }

    [Fact]
    public void U5_SetProgramIndex_OnTheOwnerInASlotCProgram_ZeroesTheIndex_ClearsTheRunningState_AndTheNextTickGoesToTheNativeHandler()
    {
        var document = NewDocument(0x40, 0x02, 0x00, 0x1A, 0x05, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x84;

        runner.RunScript(owner, ScriptHelper.ProgramCTick);

        Assert.Equal(0, owner.ProgramIndexes[ScriptHelper.ProgramCTick]);
        Assert.Equal(5u, owner.TargetAnimationId);
        Assert.Null(owner.EventProgramState.Codes);
        Assert.Equal(0, owner.EventProgramState.Sp);
        Assert.Equal(new[] { (0, 0x40, 3), (3, 0x1A, 2), (5, 0xFF, 0) }, Steps(trace));
        Assert.Equal(EventTraceKind.End, trace[^1].Kind);
        Assert.False(runner.ClearProgramStateRequested);

        // The next C tick: the program index is 0 now, so the entity goes to its native handler, not to the script.
        var scriptRuns = runner.ScriptRunCount;
        var spriteRuns = runner.SpriteEventRunCount;
        owner.EventTrigger = ScriptHelper.ProgramCTick;
        owner.RunPickedEvent(runner);

        Assert.Equal(spriteRuns + 1, runner.SpriteEventRunCount);
        Assert.Equal(scriptRuns, runner.ScriptRunCount);
    }

    [Fact]
    public void U6_SetProgramIndex_OnAnotherLogicEntity_ClearsThatEntitysStateAtOnce_AndKeepsTheOwnersOwn()
    {
        var document = NewDocument(0x40, 0x03, 0x00, 0x37, 0x05, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        var other = Entity(2);
        owner.LogicEntity = other;
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;
        owner.ProgramIndexes[3] = 0x55;
        other.ProgramIndexes[3] = 0x83;
        GiveARunningState(other, document, 7);

        runner.RunScript(owner, ScriptHelper.ProgramCTick); // 0x40, then the Wait: the call is suspended.

        Assert.Equal(new[] { (0, 0x40, 3), (3, 0x37, 0) }, Steps(trace));
        Assert.Equal(0, other.ProgramIndexes[3]);
        Assert.Null(other.EventProgramState.Codes);
        Assert.Equal(0, other.EventProgramState.Sp);
        Assert.NotNull(owner.EventProgramState.Codes);
        Assert.Equal(3, owner.EventProgramState.CodeIndex);
        Assert.Equal(0x55, owner.ProgramIndexes[3]);
        Assert.False(runner.ClearProgramStateRequested);
    }

    [Fact]
    public void U7_TheFlagIsResetByTheTestAfterTheOpcode_SoARetargetLaterInTheCallNeverClearsTheNewLogicEntity()
    {
        // 0x40 while the owner is its own logic entity, then 0x43 retargets to X, then 0x1A acts on X.
        var document = NewDocument(0x40, 0x02, 0x00, 0x43, 0x09, 0x1A, 0x05, 0xFF);
        var world = new FakeWorld();
        var other = Entity(9);
        world.Spawned.Add(other);
        GiveARunningState(other, document, 5);
        var (runner, trace) = NewRunner(document, world);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;

        runner.RunScript(owner, ScriptHelper.ProgramCTick);

        Assert.Equal(new[] { (0, 0x40, 3), (3, 0x43, 2), (5, 0x1A, 2), (7, 0xFF, 0) }, Steps(trace));
        Assert.Equal(5u, other.TargetAnimationId);
        Assert.NotNull(other.EventProgramState.Codes);
        Assert.Equal(5, other.EventProgramState.CodeIndex);
        Assert.Equal(0, owner.ProgramIndexes[ScriptHelper.ProgramCTick]);
        Assert.Null(owner.EventProgramState.Codes);
        Assert.Equal(0, owner.EventProgramState.Sp);
        Assert.False(runner.ClearProgramStateRequested);
    }

    [Fact]
    public void U8_SetProgramIndex_InASlotAProgram_WritesTheIndex_LeavesTheEntitysCStateAlone_AndTheNextCTickUsesTheNativeHandler()
    {
        var document = NewDocument(0x40, 0x02, 0x00, 0xFF);
        var (runner, _) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x80;
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x85;
        GiveARunningState(owner, document, 4);

        runner.RunScript(owner, ScriptHelper.ProgramALoad);

        Assert.Equal(0, owner.ProgramIndexes[ScriptHelper.ProgramCTick]);
        Assert.NotNull(owner.EventProgramState.Codes); // the state of slot C is not the one that ran
        Assert.Equal(4, owner.EventProgramState.CodeIndex);

        var spriteRuns = runner.SpriteEventRunCount;
        owner.EventTrigger = ScriptHelper.ProgramCTick;
        owner.RunPickedEvent(runner);

        Assert.Equal(spriteRuns + 1, runner.SpriteEventRunCount);
    }

    [Fact]
    public void U10_AProgramSlotOfSixOrMore_DoesNothing_ClearsNothing_WarnsOncePerOpcode_AndReturnsSize3()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var document = NewDocument(0x40, 0x06, 0x00, 0x41, 0x06, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x77;
        owner.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = 0x66;
        var programsBefore = owner.ProgramIndexes.ToArray();
        var spritesBefore = owner.SpriteProgramIndexes.ToArray();

        for (var call = 0; call < 2; call++)
        {
            trace.Clear();
            var state = StateFor(document);

            runner.RunOneScriptCall(owner, state);

            Assert.Equal(new[] { (0, 0x40, 3), (3, 0x41, 3), (6, 0xFF, 0) }, Steps(trace));
            Assert.NotNull(state.Codes); // nothing was cleared: the out-of-range opcode has no effect at all
            Assert.False(runner.ClearProgramStateRequested);
        }

        Assert.Equal(programsBefore, owner.ProgramIndexes);
        Assert.Equal(spritesBefore, owner.SpriteProgramIndexes);
        var warnings = log.Warnings.Where(w => w.Contains("program slot 6")).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Single(warnings, w => w.Contains("0x40"));
        Assert.Single(warnings, w => w.Contains("0x41"));
    }

    [Fact]
    public void U11_SetSpriteProgramIndex_WritesTheLogicEntitysSpriteIndex_ClearsNothing_AndSizeIs3()
    {
        var document = NewDocument(0x41, 0x02, 0x04, 0xFF);

        // Without a retarget: the owner is the logic entity.
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[2] = 0x81;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(4, owner.SpriteProgramIndexes[2]);
        Assert.Equal(0x81, owner.ProgramIndexes[2]);
        Assert.NotNull(state.Codes);
        Assert.Equal(new[] { (0, 0x41, 3), (3, 0xFF, 0) }, Steps(trace));
        Assert.Equal(3, state.CodeIndex);

        // With a logic entity X: only X changes.
        var (runner2, _) = NewRunner(document);
        var owner2 = Entity(1);
        var other = Entity(2);
        owner2.LogicEntity = other;

        runner2.RunOneScriptCall(owner2, StateFor(document));

        Assert.Equal(4, other.SpriteProgramIndexes[2]);
        Assert.Equal(0, owner2.SpriteProgramIndexes[2]);
    }

    [Fact]
    public void U12_SetProgramIndex_ThenABreak_ClearsTheStateAtTheEndOfTheCall_AndTheNextCallStartsOver()
    {
        var document = NewDocument(0x40, 0x03, 0x00, 0x1A, 0x05, 0x00, 0x1A, 0x06, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x81;
        owner.ProgramIndexes[3] = 0x82;

        runner.RunScript(owner, ScriptHelper.ProgramCTick);

        Assert.Equal(new[] { (0, 0x40, 3), (3, 0x1A, 2), (5, 0x00, 1) }, Steps(trace));
        Assert.Equal(EventTraceKind.Break, trace[^1].Kind);
        Assert.Equal(0, owner.ProgramIndexes[3]);
        Assert.Null(owner.EventProgramState.Codes);

        // Without the clear, the program would resume after its Break and set the animation to 6.
        trace.Clear();
        runner.RunScript(owner, ScriptHelper.ProgramCTick);

        Assert.Equal(new[] { (0, 0x40, 3), (3, 0x1A, 2), (5, 0x00, 1) }, Steps(trace));
        Assert.NotEqual(6u, owner.TargetAnimationId);
        Assert.Equal(5u, owner.TargetAnimationId);
    }

    [Fact]
    public void U13_SetProgramIndex_ThenAWait_ClearsTheStateAtTheEndOfTheCall_SoTheProgramNeverGetsPastTheWait()
    {
        var document = NewDocument(0x40, 0x03, 0x00, 0x37, 0x05, 0x1A, 0x06, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;

        for (var call = 0; call < 12; call++)
        {
            runner.RunScript(owner, ScriptHelper.ProgramCTick);

            Assert.Null(owner.EventProgramState.Codes);
            Assert.NotEqual(6u, owner.TargetAnimationId);
        }

        // Every call starts over: the Wait is the only thing it reaches, pc 5 never runs.
        Assert.DoesNotContain(trace, r => r.CodeIndex == 5);
        Assert.Equal(12, trace.Count(r => r.CodeIndex == 0 && r.Opcode == 0x40));
        Assert.Equal(12, trace.Count(r => r.CodeIndex == 3 && r.Opcode == 0x37));
    }
}
