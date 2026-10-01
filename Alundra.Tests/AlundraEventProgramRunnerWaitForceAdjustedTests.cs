#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d D4 (docs/plan-e19-opcodes.md §1.2g, D-E19-21), tests U1 to U4: opcode <c>0x24</c> (Wait force adjusted, <c>0x8003DB70</c>, size 1)
/// returns 1 when the LOGIC entity's <c>ForceAdjusted</c> is nonzero, else 0. No operand, no memory, no detour, no timer.
/// </summary>
public class AlundraEventProgramRunnerWaitForceAdjustedTests
{
    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private sealed class GridWorld : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = new List<AlundraEntityScriptProxy>();

        public AlundraEntityScriptProxy? PlayerEntity => null;

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

        public NavigationGrid2D? NavigationGrid { get; } = new(20, 20, 1f);
    }

    private static (AlundraEventProgramRunner Runner, List<EventTraceRecord> Trace) NewRunner(EventProgramDocument document, IEntityWorldContext? world = null)
    {
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), world);
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        return (runner, trace);
    }

    private static EventProgramState StateFor(EventProgramDocument document) => new() { Codes = document.CodesAsBytes() };

    [Fact]
    public void U1_TheWaitSuspendsWhileForceAdjustedIsZero_AndEndsOnceItIsNonZero()
    {
        var document = NewDocument(0x01, 0x24, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, ForceAdjusted = 0 };
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x24, 0) }, trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray());
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0, state.Parameters[1]);

        trace.Clear();
        owner.ForceAdjusted = 1;
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (1, 0x24, 1), (2, 0xFF, 0) }, trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray());
        Assert.Equal(EventTraceKind.End, trace[^1].Kind);
        Assert.Equal(2, state.CodeIndex);
    }

    [Fact]
    public void U2_AnyNonZeroForceAdjustedEndsTheWait()
    {
        // ForceAdjusted is copied as is by the binary (0x6E too): 2 ends the wait like 1.
        var document = NewDocument(0x24, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, ForceAdjusted = 2 };
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal((0, 0x24, 1), (trace[0].CodeIndex, trace[0].Opcode, trace[0].Size));
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void U3_TheWaitReadsTheLogicEntity_NotTheOwner()
    {
        var document = NewDocument(0x24, 0xFF);

        // The owner is at 1 and the logic entity at 0: the wait goes on.
        var (runner, _) = NewRunner(document);
        var owner = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, ForceAdjusted = 1 };
        var logic = new AlundraEntityScriptProxy { EntityRefId = 2, Status = EntityStatus.Normal, ForceAdjusted = 0 };
        owner.LogicEntity = logic;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(0, state.CodeIndex);

        // The other way round: the wait ends.
        owner.ForceAdjusted = 0;
        logic.ForceAdjusted = 1;
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void U4_TheWaitHasNoSideEffect_NoDetourNoDirectionNoAnimation_EvenWithANavigationGrid()
    {
        var document = NewDocument(0x24, 0xFF);
        var (runner, _) = NewRunner(document, new GridWorld());
        var owner = new AlundraEntityScriptProxy
        {
            EntityRefId = 1,
            Status = EntityStatus.Normal,
            TargetDirection = 5,
            TargetAnimationId = 7,
            ForceAdjusted = 0,
        };
        var state = StateFor(document);
        state.Parameters[1] = 0;
        state.Parameters[2] = 0;
        state.Parameters[3] = 0;

        for (var call = 0; call < 10; call++)
        {
            runner.RunOneScriptCall(owner, state);
            Assert.Equal(0, state.CodeIndex);
        }

        owner.ForceAdjusted = 1;
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(1, state.CodeIndex);

        Assert.Equal(5u, owner.TargetDirection);
        Assert.Equal(7u, owner.TargetAnimationId);
        Assert.Null(owner.WalkDetourPath);
        Assert.False(owner.WalkDetourAttempted);
        Assert.Equal(new[] { 0, 0, 0 }, new[] { state.Parameters[1], state.Parameters[2], state.Parameters[3] });
    }
}
