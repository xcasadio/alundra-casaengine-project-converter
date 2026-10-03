#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 R4 (docs/plan-e19-opcodes.md §1.2h.3.1), test UJ-2: opcode <c>0x25</c> (<c>0x8003DB7C</c>, size 1) returns 1 when the LOGIC
/// entity's <c>CollidedWithEntityZ</c> (+0x140) or <c>IsOnGround</c> (+0x144) is nonzero, else 0. No operand, no memory, no side effect.
/// Same montage as <see cref="AlundraEventProgramRunnerWaitForceAdjustedTests"/>.
/// </summary>
public class AlundraEventProgramRunnerWaitCollidedZTests
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
    public void UJ2_TheWaitSuspendsWhileBothAreZero_AndEndsOnceEitherIsNonZero()
    {
        var document = NewDocument(0x01, 0x25, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, CollidedWithEntityZ = 0, IsOnGround = 0 };
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x25, 0) }, trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray());
        Assert.Equal(1, state.CodeIndex);

        // (CollidedWithEntityZ, IsOnGround) = (0, 1), then (1, 0), then (1, 1): each ends the wait.
        foreach (var (collided, onGround) in new[] { (0, 1), (1, 0), (1, 1) })
        {
            var s = StateFor(document);
            var o = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, CollidedWithEntityZ = 0, IsOnGround = 0 };
            var (r, t) = NewRunner(document);
            r.RunOneScriptCall(o, s);
            Assert.Equal(1, s.CodeIndex);

            t.Clear();
            o.CollidedWithEntityZ = collided;
            o.IsOnGround = onGround;
            r.RunOneScriptCall(o, s);

            Assert.Equal(new[] { (1, 0x25, 1), (2, 0xFF, 0) }, t.Select(x => (x.CodeIndex, x.Opcode, x.Size)).ToArray());
            Assert.Equal(EventTraceKind.End, t[^1].Kind);
            Assert.Equal(2, s.CodeIndex);
        }
    }

    [Fact]
    public void UJ2_TheWaitReadsTheLogicEntity_NotTheOwner()
    {
        var document = NewDocument(0x25, 0xFF);

        // The owner is at (1, 1) and the logic entity at (0, 0): the wait goes on.
        var (runner, _) = NewRunner(document);
        var owner = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, CollidedWithEntityZ = 1, IsOnGround = 1 };
        var logic = new AlundraEntityScriptProxy { EntityRefId = 2, Status = EntityStatus.Normal, CollidedWithEntityZ = 0, IsOnGround = 0 };
        owner.LogicEntity = logic;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(0, state.CodeIndex);

        // The owner at (0, 0) and the logic entity at (0, 1): the wait ends.
        owner.CollidedWithEntityZ = 0;
        owner.IsOnGround = 0;
        logic.IsOnGround = 1;
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void UJ2_TheWaitHasNoSideEffect_NoDetourNoDirectionNoAnimation_EvenWithANavigationGrid()
    {
        var document = NewDocument(0x01, 0x25, 0xFF); // 0x25 at pc 1, as the first case: the pc 0 would hide a stray write to Parameters[1].
        var (runner, _) = NewRunner(document, new GridWorld());
        var owner = new AlundraEntityScriptProxy
        {
            EntityRefId = 1,
            Status = EntityStatus.Normal,
            TargetDirection = 5,
            TargetAnimationId = 7,
        };
        var state = StateFor(document);
        state.Parameters[1] = 0;
        state.Parameters[2] = 0;
        state.Parameters[3] = 0;

        for (var call = 0; call < 10; call++)
        {
            runner.RunOneScriptCall(owner, state);
            Assert.Equal(1, state.CodeIndex);
        }

        owner.IsOnGround = 1;
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(2, state.CodeIndex);

        Assert.Equal(5u, owner.TargetDirection);
        Assert.Equal(7u, owner.TargetAnimationId);
        Assert.Null(owner.WalkDetourPath);
        Assert.False(owner.WalkDetourAttempted);
        Assert.Equal(new[] { 0, 0, 0 }, new[] { state.Parameters[1], state.Parameters[2], state.Parameters[3] });
    }
}
