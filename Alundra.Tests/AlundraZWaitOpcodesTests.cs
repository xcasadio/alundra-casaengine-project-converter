#nullable enable
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.h1 H1-1 (docs/plan-e19-opcodes.md §1.2n.1): the Z wait opcodes <c>0x21</c> (<c>0x8003DA28</c>, size 3), <c>0x26</c>
/// (<c>0x8003DBA8</c>, 1), <c>0x47</c> (<c>0x8003E984</c>, 1), <c>0x48</c> (<c>0x8003E9B0</c>, 1) and the reader <c>0x6F</c>
/// (<c>0x8003F9E8</c>, 1). Same montage as <see cref="AlundraEventProgramRunnerWaitCollidedZTests"/>: the opcode sits at pc 1
/// behind a <c>0x01</c> (a memo on <c>CodeIndex</c> 0 would be confused with the value cleared in advance).
/// </summary>
public class AlundraZWaitOpcodesTests
{
    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private static (AlundraEventProgramRunner Runner, System.Collections.Generic.List<EventTraceRecord> Trace) NewRunner(EventProgramDocument document)
    {
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), null);
        var trace = new System.Collections.Generic.List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        return (runner, trace);
    }

    private static EventProgramState StateFor(EventProgramDocument document) => new() { Codes = document.CodesAsBytes() };

    private static AlundraEntityScriptProxy NewEntity() => new() { EntityRefId = 1, Status = EntityStatus.Normal };

    private static (int CodeIndex, int Opcode, int Size)[] Shape(System.Collections.Generic.List<EventTraceRecord> trace) =>
        trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray();

    // ---- 0x21 ----

    [Fact]
    public void TZ21_WaitsForTheZDistanceFromTheFirstCall_AndMemorisesPcAndPosZ()
    {
        var document = NewDocument(0x01, 0x21, 0x10, 0x00, 0xFF); // 16 px
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x21, 0) }, Shape(trace));
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(1, state.Parameters[1]);
        Assert.Equal(3145728, state.Parameters[2]);

        // 15.99 px up: still waiting. 16 px up: ends (size 3), then 0xFF at 4.
        owner.PosZ = 4194303;
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 0) }, Shape(trace));

        owner.PosZ = 4194304;
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.End, trace[^1].Kind);
    }

    [Fact]
    public void TZ21_TheHighByteOfTheDistanceCounts_256Px()
    {
        var document = NewDocument(0x01, 0x21, 0x00, 0x01, 0xFF); // 0x0100 = 256 px
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x21, 0) }, Shape(trace));

        owner.PosZ = 19922943; // 255.99 px up: still waiting.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 0) }, Shape(trace));

        owner.PosZ = 19922944; // 256 px up: ends.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ21_TheDistanceIsAbsolute_DownwardEndsToo()
    {
        var document = NewDocument(0x01, 0x21, 0x10, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);

        owner.PosZ = 2097152; // 16 px down
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ21_ReadsTheLogicEntity_NotTheOwner()
    {
        var document = NewDocument(0x01, 0x21, 0x10, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        var logic = new AlundraEntityScriptProxy { EntityRefId = 2, Status = EntityStatus.Normal, PosZ = 3145728 };
        owner.LogicEntity = logic;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(3145728, state.Parameters[2]);

        owner.PosZ = 99999999; // the owner moved, not the logic entity: still waiting
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 0) }, Shape(trace));

        logic.PosZ = 4194304;
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ21_ACollisionAtTheFirstCallEndsAtOnce()
    {
        var document = NewDocument(0x01, 0x21, 0x10, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        owner.CollidedWithEntityZ = 1;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.End, trace[^1].Kind);
        Assert.Equal(3145728, state.Parameters[2]); // the distance test runs first (0x8003DA34) and memorises PosZ before the collision test (0x8003DA48).
    }

    [Fact]
    public void TZ21_ACollisionAtALaterCallEnds()
    {
        var document = NewDocument(0x01, 0x21, 0x10, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(1, state.CodeIndex);

        owner.CollidedWithEntityZ = 1; // PosZ unchanged
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x21, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    // ---- 0x26 ----

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, true)]
    public void TZ26_WaitsUntilForceAdjustedOrCollidedZ(int forceAdjusted, int collidedZ, bool ends)
    {
        var document = NewDocument(0x01, 0x26, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.ForceAdjusted = forceAdjusted;
        owner.CollidedWithEntityZ = collidedZ;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        if (ends)
        {
            Assert.Equal(new[] { (0, 0x01, 1), (1, 0x26, 1), (2, 0xFF, 0) }, Shape(trace));
            Assert.Equal(EventTraceKind.End, trace[^1].Kind);
        }
        else
        {
            Assert.Equal(new[] { (0, 0x01, 1), (1, 0x26, 0) }, Shape(trace));
            Assert.Equal(1, state.CodeIndex);
        }
    }

    [Fact]
    public void TZ26_TenCallsHaveNoSideEffect()
    {
        var document = NewDocument(0x01, 0x26, 0xFF);
        var (runner, _) = NewRunner(document);
        var owner = NewEntity();
        owner.TargetDirection = 5;
        owner.TargetAnimationId = 7;
        var state = StateFor(document);
        state.Result = 7;

        for (var call = 0; call < 10; call++)
        {
            runner.RunOneScriptCall(owner, state);
            Assert.Equal(1, state.CodeIndex);
        }

        Assert.Equal(7, state.Result);
        Assert.Equal(new[] { 0, 0, 0 }, new[] { state.Parameters[1], state.Parameters[2], state.Parameters[3] });
        Assert.Equal(5u, owner.TargetDirection);
        Assert.Equal(7u, owner.TargetAnimationId);
        Assert.Null(owner.WalkDetourPath);
        Assert.False(owner.WalkDetourAttempted);
    }

    // ---- 0x47 ----

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    public void TZ47_WaitsUntilHitCounterOrForceAdjusted(int hitCounter, int forceAdjusted, bool ends)
    {
        var document = NewDocument(0x01, 0x47, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.HitCounter = hitCounter;
        owner.ForceAdjusted = forceAdjusted;
        var state = StateFor(document);
        state.Result = 7;

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(7, state.Result);
        Assert.Equal(ends ? new[] { (0, 0x01, 1), (1, 0x47, 1), (2, 0xFF, 0) } : new[] { (0, 0x01, 1), (1, 0x47, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ47_ACollidedZAloneDoesNotEndTheWait()
    {
        var document = NewDocument(0x01, 0x47, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.CollidedWithEntityZ = 1;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x47, 0) }, Shape(trace));
    }

    // ---- 0x48 ----

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    public void TZ48_WaitsUntilHitCounterOrCollidedZ(int hitCounter, int collidedZ, bool ends)
    {
        var document = NewDocument(0x01, 0x48, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.HitCounter = hitCounter;
        owner.CollidedWithEntityZ = collidedZ;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(ends ? new[] { (0, 0x01, 1), (1, 0x48, 1), (2, 0xFF, 0) } : new[] { (0, 0x01, 1), (1, 0x48, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ48_AForceAdjustedAloneDoesNotEndTheWait()
    {
        var document = NewDocument(0x01, 0x48, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.ForceAdjusted = 1;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x48, 0) }, Shape(trace));
    }

    // ---- 0x6F ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TZ6F_ResultIsCollidedWithEntityZ(int collidedZ)
    {
        var document = NewDocument(0x01, 0x6F, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.CollidedWithEntityZ = collidedZ;
        var state = StateFor(document);
        state.Result = 9;

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x6F, 1), (2, 0xFF, 0) }, Shape(trace));
        Assert.Equal(collidedZ, state.Result);
    }
}
