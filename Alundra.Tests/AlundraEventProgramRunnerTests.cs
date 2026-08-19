using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraEventProgramRunner"/>'s interpreter core on synthetic bytecode (not real
/// map data - see <see cref="MapEventProgramLoaderTests"/> for path resolution/loading, and the
/// self-skipping map-389 test below for a real decoded program).
/// </summary>
public class AlundraEventProgramRunnerTests
{
    private static EventProgramDocument NewDocument(params int[] codes)
    {
        return new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
    }

    private static AlundraEventProgramRunner NewRunner(EventProgramDocument document, AlundraGameState? gameState = null)
        => new(document, gameState ?? new AlundraGameState());

    private static AlundraEntityScriptProxy NewEntity() => new();

    [Fact]
    public void SequentialOps_ThenEnd_RunsToCompletion()
    {
        // 0x1A SetAnim(7); 0xFF End
        var document = NewDocument(0x1A, 7, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(7u, entity.TargetAnimationId);
        Assert.Equal(2, state.CodeIndex); // stopped at the 0xFF byte
    }

    [Fact]
    public void Goto_Forward_SkipsInterveningBytes()
    {
        // @0: Goto +5 -> @5; @3: SetAnim(9) [never reached]; @5: SetAnim(3); @7: End
        var document = NewDocument(0x02, 5, 0, 0x1A, 9, 0x1A, 3, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(3u, entity.TargetAnimationId);
    }

    [Fact]
    public void Goto_Backward_NegativeOffsetReachesEarlierCode()
    {
        // @2: SetAnim(42); @4: End (the backward jump's target)
        // @6: Goto v1=0xFC,v2=0xFF -> jump = sign16(0xFF<<8|0xFC) = -4 -> target = 6 + (-4) = 2
        var document = NewDocument(0, 0, 0x1A, 42, 0xFF, 0, 0x02, 0xFC, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        // Starts execution directly at the Goto (index 6) - this test is only about the negative-offset
        // math, not about how execution first got there.
        var state = new EventProgramState { Codes = document.CodesAsBytes(), CodeIndex = 6 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(42u, entity.TargetAnimationId);
    }

    // Shared layout for the three conditional-goto tests below: @0 is the 3-byte conditional-goto
    // instruction (size 3, so its own fallthrough lands at @3); @3: SetAnim(1); End (the "not taken"
    // path); @6: SetAnim(2); End (the "taken" path, jump target = 6).
    private static EventProgramDocument NewConditionalGotoDocument(int opcode)
        => NewDocument(opcode, 6, 0, 0x1A, 1, 0xFF, 0x1A, 2, 0xFF);

    [Fact]
    public void IfTrueGoto_TakesBranch_WhenResultNonZero()
    {
        var document = NewConditionalGotoDocument(0x03);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(2u, entity.TargetAnimationId);
    }

    [Fact]
    public void IfTrueGoto_FallsThrough_WhenResultZero()
    {
        var document = NewConditionalGotoDocument(0x03);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1u, entity.TargetAnimationId);
    }

    [Fact]
    public void IfFalseGoto_TakesBranch_WhenResultZero()
    {
        var document = NewConditionalGotoDocument(0x04);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(2u, entity.TargetAnimationId);
    }

    [Fact]
    public void Wait_SuspendsAcrossMultipleCalls_ThenAdvances()
    {
        // @0: DoNothing (pushes Wait off CodeIndex 0 - see below); @1: Wait(2); @3: SetAnim(9); @5: End
        //
        // Wait's re-entrancy check compares Parameters[1] (0 by default) against CodeIndex: placing Wait
        // at CodeIndex 0 would make an unarmed state indistinguishable from an already-armed one on the
        // very first call, since both default to 0 - a synthetic-test-only edge case (real bytecode
        // offsets are never 0), avoided here the same way real programs avoid it, by simply not being
        // the first byte of the blob.
        var document = NewDocument(0x01, 0x37, 2, 0x1A, 9, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        // First call: DoNothing advances to @1, then Wait arms and suspends immediately.
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0u, entity.TargetAnimationId);

        // Frame 2: counter 0 < 2, still suspended.
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(1, state.CodeIndex);

        // Frame 3: counter 1 < 2, still suspended.
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(1, state.CodeIndex);

        // Frame 4: counter 2 >= 2, advances past Wait and runs SetAnim(9).
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId);
    }

    [Fact]
    public void UnknownOpcode_KnownSize_SkipsBySize()
    {
        // 0x08 "Turn" (size 2) is not one of the implemented handlers - skipped by its table size.
        var document = NewDocument(0x08, 0, 0x1A, 4, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(4u, entity.TargetAnimationId);
    }

    [Fact]
    public void UnknownOpcode_NoKnownSize_TerminatesScriptCall()
    {
        // 0xC5 is past the last named opcode (0xC4) and not 0xFF - no size entry at all.
        var document = NewDocument(0x1A, 3, 0xC5, 0x1A, 9, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        // SetAnim(3) ran, but the unknown-size opcode terminated the call before SetAnim(9).
        Assert.Equal(3u, entity.TargetAnimationId);
        Assert.Equal(2, state.CodeIndex);
    }

    [Fact]
    public void Break_TerminatesImmediately_WithoutRunningFollowingOps()
    {
        var document = NewDocument(0x1A, 3, 0x00, 0x1A, 9, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(3u, entity.TargetAnimationId);
        Assert.Equal(3, state.CodeIndex); // CodeIndex++ past the Break byte, per the original
    }

    [Fact]
    public void FlagOnThenOff_RoundTrips()
    {
        // Flag word (v2<<8|v1) = 300 = 0x012C -> v1=0x2C(44), v2=0x01(1); bit = v1&0x1f = 44&31 = 12.
        var document = NewDocument(0x05, 44, 1, 0xFF);
        var gameState = new AlundraGameState();
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1u << 12, gameState.GetFlag(300) & (1u << 12));

        var offDocument = NewDocument(0x06, 44, 1, 0xFF);
        var offState = new EventProgramState { Codes = offDocument.CodesAsBytes() };
        runner.RunOneScriptCall(entity, offState);

        Assert.Equal(0u, gameState.GetFlag(300) & (1u << 12));
    }

    [Fact]
    public void IfFlagOff_TakesBranch_WhenFlagClear()
    {
        // flag = v1 + v2*0x100 = 92 + 3*256 = 860; jump = sign16(v4<<8|v3) = 6 -> target @6 (SetAnim(1)).
        var document = NewDocument(0x31, 92, 3, 6, 0, 0, 0x1A, 1, 0xFF);
        var runner = NewRunner(document); // fresh AlundraGameState: flag 860 starts clear
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1u, entity.TargetAnimationId);
        Assert.Equal(8, state.CodeIndex);
    }

    [Fact]
    public void IfFlagOff_FallsThrough_WhenFlagSet()
    {
        var document = NewDocument(0x31, 92, 3, 5, 0, 0, 0x1A, 1, 0xFF);
        var gameState = new AlundraGameState();
        gameState.AddFlag(860, 1u << (92 & 0x1f));
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        // Falls through (size 5) into the filler zero byte at @5 (0x00 Break) instead of jumping.
        Assert.Equal(0u, entity.TargetAnimationId);
        Assert.Equal(6, state.CodeIndex);
    }

    [Fact]
    public void InitializeEventData_MasksIndexWith0x7f_AndResolvesATableOffset()
    {
        // Mirrors the brief's own worked example: EventCodesA_LoadIndex=133 -> &0x7f=5 -> table[5]=132.
        var document = new EventProgramDocument
        {
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 132 },
            Codes = Enumerable.Repeat(0xFF, 133).ToArray(),
        };
        document.Codes[132] = 0x1A;
        // pad one more byte so 0x1A's param read does not run off the end
        document.Codes = document.Codes.Concat(new[] { 7 }).ToArray();

        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramALoad] = 133;

        var state = new EventProgramState();
        runner.InitializeEventData(entity, ScriptHelper.ProgramALoad, state);

        Assert.Equal(132, state.CodeIndex);
    }

    [Fact]
    public void RunScript_SlotA_RunsToCompletion_AndSetsAnimation()
    {
        var document = new EventProgramDocument
        {
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 132 },
            Codes = new int[132].Concat(new[] { 0x1A, 10, 0xFF }).ToArray(),
        };
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramALoad] = 133;

        runner.RunScript(entity, ScriptHelper.ProgramALoad);

        Assert.Equal(10u, entity.TargetAnimationId);
        Assert.Equal(1, runner.ScriptRunCount);
    }

    [Fact]
    public void RunScript_NonLoadSlot_IsCountedNoOp()
    {
        var document = NewDocument(0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();

        runner.RunScript(entity, ScriptHelper.ProgramCTick);

        Assert.Equal(1, runner.ScriptRunCount);
        Assert.Equal(0u, entity.TargetAnimationId);
    }

    [Fact]
    public void RunScript_NoDocument_IsCountedNoOp_DegradedMode()
    {
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState());
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramALoad] = 133;

        runner.RunScript(entity, ScriptHelper.ProgramALoad);

        Assert.Equal(1, runner.ScriptRunCount);
        Assert.Equal(0u, entity.TargetAnimationId);
    }

    [Fact]
    public void RunSpriteEvent_IsCountedNoOp()
    {
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState());
        var entity = NewEntity();

        runner.RunSpriteEvent(entity);
        runner.RunSpriteEvent(entity);

        Assert.Equal(2, runner.SpriteEventRunCount);
    }
}
