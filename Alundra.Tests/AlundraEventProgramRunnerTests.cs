using System.Collections.Generic;
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

    private static AlundraEventProgramRunner NewRunner(EventProgramDocument document, AlundraGameState? gameState = null, IEntityWorldContext? worldContext = null)
        => new(document, gameState ?? new AlundraGameState(), worldContext);

    private static AlundraEntityScriptProxy NewEntity() => new();

    /// <summary>Records spawn/destroy calls and hands back a fixed <see cref="SpawnedEntities"/> list -
    /// the fake <see cref="IEntityWorldContext"/> every 0x2D/0x2E/0x62/0x63/0x64/0x65/0xAC test below
    /// uses instead of a live <see cref="AlundraWorldProxy"/>.</summary>
    private sealed class FakeEntityWorldContext : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> SpawnedEntitiesList { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => SpawnedEntitiesList;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }

        public readonly List<(AlundraEntityScriptProxy LogicEntity, int EntityRecordId)> SpawnCalls = new();
        public AlundraEntityScriptProxy? EntityToSpawn;

        public readonly List<AlundraEntityScriptProxy> DestroyedEntities = new();

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
        {
            SpawnCalls.Add((logicEntity, entityRecordId));
            return EntityToSpawn;
        }

        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
            DestroyedEntities.Add(entity);
        }
    }

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
    public void WaitUntilFlagOn_FlagClear_SuspendsAtSameCodeIndex()
    {
        // Script_54_036 (0x36) - despite EventOpcodeSizeTable naming it "Wait flag off", it suspends
        // (returns 0) while the flag bit is CLEAR and only advances once it is SET - see the case 0x36
        // comment on AlundraEventProgramRunner.Dispatch. flag = v2<<8|v1 = 1<<8|44 = 300; bit = 44&0x1f = 12.
        var document = NewDocument(0x36, 44, 1, 0x1A, 9, 0xFF);
        var runner = NewRunner(document); // fresh AlundraGameState: flag 300 bit 12 starts clear
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.CodeIndex); // suspended - never advanced past the 0x36 instruction
        Assert.Equal(0u, entity.TargetAnimationId); // SetAnim(9) never reached
    }

    [Fact]
    public void WaitUntilFlagOn_FlagSet_AdvancesByThree()
    {
        var document = NewDocument(0x36, 44, 1, 0x1A, 9, 0xFF);
        var gameState = new AlundraGameState();
        gameState.AddFlag(300, 1u << (44 & 0x1f));
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(9u, entity.TargetAnimationId); // advanced past 0x36 (size 3) and ran SetAnim(9)
        Assert.Equal(5, state.CodeIndex);
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

    // -----------------------------------------------------------------------------------------
    // Entity search/manipulation opcodes (0x2D, 0x2E, 0x62, 0x63, 0x64, 0x65, 0xAC)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ActivateEntity_0x2D_DelegatesToContext_WithSelfAsLogicEntity()
    {
        var document = NewDocument(0x2D, 5, 0xFF);
        var context = new FakeEntityWorldContext { EntityToSpawn = NewEntity() };
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        var call = Assert.Single(context.SpawnCalls);
        Assert.Same(entity, call.LogicEntity);
        Assert.Equal(5, call.EntityRecordId);
        Assert.Equal(2, state.CodeIndex);
    }

    [Fact]
    public void SpawnEntityNextToEntity_0x8B_MatchFound_AppliesOffsetToMatchPosition()
    {
        // v1=0x80 (owner match, returns [entity] regardless of SpawnedEntities); v2=5 (record id);
        // offset (v3..v8) = (2,0, 3,0, 4,0) -> +2/+3/+4 in 16.16 units on X/Y/Z.
        var document = NewDocument(0x8B, 0x80, 5, 2, 0, 3, 0, 4, 0, 0xFF);
        var spawned = NewEntity();
        var context = new FakeEntityWorldContext { EntityToSpawn = spawned };
        var runner = NewRunner(document, worldContext: context);
        var entity = new AlundraEntityScriptProxy { PosX = 5 << 16, PosY = 7 << 16, PosZ = 9 << 16 };
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        var call = Assert.Single(context.SpawnCalls);
        Assert.Same(entity, call.LogicEntity);
        Assert.Equal(5, call.EntityRecordId);
        Assert.Equal(7 << 16, spawned.PosX);
        Assert.Equal(10 << 16, spawned.PosY);
        Assert.Equal(13 << 16, spawned.PosZ);
    }

    [Fact]
    public void SpawnEntityNextToEntity_0x8B_NoMatch_LeavesSpawnedAtItsOwnRecordPosition()
    {
        // v1=0x81 ("get player" - never matches, no player system yet): the position write is skipped
        // entirely, leaving the spawned entity wherever SpawnEntityByRecordId already placed it (its own
        // record's spawn position, mirrored by the fake context here as a preset PosX/PosY/PosZ).
        var document = NewDocument(0x8B, 0x81, 6, 2, 0, 3, 0, 4, 0, 0xFF);
        var spawned = new AlundraEntityScriptProxy { PosX = 111, PosY = 222, PosZ = 333 };
        var context = new FakeEntityWorldContext { EntityToSpawn = spawned };
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(111, spawned.PosX);
        Assert.Equal(222, spawned.PosY);
        Assert.Equal(333, spawned.PosZ);
    }

    [Fact]
    public void CheckFlagsOn_0x33_AllFourSet_SetsResult1()
    {
        // Four (flag,bit) pairs, each encoded as flag = v[2i+1] + v[2i+2]*0x100: flags 12/13/14/15
        // directly (all < 32, so mask = 1<<flag - keeps the test arithmetic simple).
        var document = NewDocument(0x33, 12, 0, 13, 0, 14, 0, 15, 0, 0xFF);
        var gameState = new AlundraGameState();
        gameState.AddFlag(12, 1u << 12);
        gameState.AddFlag(13, 1u << 13);
        gameState.AddFlag(14, 1u << 14);
        gameState.AddFlag(15, 1u << 15);
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1, state.Result);
        Assert.Equal(9, state.CodeIndex);
    }

    [Fact]
    public void CheckFlagsOn_0x33_OnePairClear_SetsResult0_ShortCircuits()
    {
        // Same four pairs as above, but flag 14's bit is never set.
        var document = NewDocument(0x33, 12, 0, 13, 0, 14, 0, 15, 0, 0xFF);
        var gameState = new AlundraGameState();
        gameState.AddFlag(12, 1u << 12);
        gameState.AddFlag(13, 1u << 13);
        gameState.AddFlag(15, 1u << 15);
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.Result);
        Assert.Equal(9, state.CodeIndex);
    }

    [Fact]
    public void DestroyEntity_0x2E_DestroysEveryMatch_AndSetsResult1()
    {
        // v1=0x80 -> functionId 0 ("get owner"): the owner itself is the only match.
        var document = NewDocument(0x2E, 0x80, 0xFF);
        var context = new FakeEntityWorldContext();
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(new[] { entity }, context.DestroyedEntities);
        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void DestroyEntity_0x2E_NoMatches_SetsResult0()
    {
        // v1=0x81 -> functionId 1 ("get player"): never matches (no player system yet).
        var document = NewDocument(0x2E, 0x81, 0xFF);
        var context = new FakeEntityWorldContext();
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };

        runner.RunOneScriptCall(entity, state);

        Assert.Empty(context.DestroyedEntities);
        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void SetEntitiesFlagsLow16_0x62_OrsFlagIntoEveryMatch()
    {
        // v1=0x82 (all entities), v2/v3 = flag bytes 0x34/0x12 -> flag = 0x1234.
        var document = NewDocument(0x62, 0x82, 0x34, 0x12, 0xFF);
        var target = NewEntity();
        target.Status = EntityStatus.Normal;
        target.Flags = 0x0001;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0x1235u, target.Flags);
    }

    [Fact]
    public void ClearEntitiesFlagsLow16_0x63_ClearsOnlyLow16Bits()
    {
        var document = NewDocument(0x63, 0x82, 0xFF, 0xFF, 0xFF); // clear mask = 0xFFFF
        var target = NewEntity();
        target.Status = EntityStatus.Normal;
        target.Flags = 0xABCD1234;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        // High 16 bits (0xABCD) survive untouched; low 16 bits (0x1234) are cleared.
        Assert.Equal(0xABCD0000u, target.Flags);
    }

    [Fact]
    public void SetEntitiesPosition_0x64_SetsPosXYZ_FromRealMap389Operands()
    {
        // The exact operand bytes decoded from map 389's real Load program 139 (events offset 239):
        // v1=0x80 (owner), x=(2<<8|0x34)<<16, y=(1<<8|0x78)<<16, z=((0<<8|0xa0)<<16)+1.
        var document = NewDocument(0x64, 0x80, 0x34, 0x02, 0x78, 0x01, 0xa0, 0x00, 0xFF);
        var owner = NewEntity();
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0x234 << 16, owner.PosX);
        Assert.Equal(0x178 << 16, owner.PosY);
        Assert.Equal((0xa0 << 16) + 1, owner.PosZ);
        Assert.Equal(8, state.CodeIndex); // stopped at the 0xFF byte
    }

    [Fact]
    public void AddEntitiesPositionOffset_0x65_AddsOffset_ToEveryMatch()
    {
        var document = NewDocument(0x65, 0x80, 0x10, 0x00, 0x20, 0x00, 0x30, 0x00, 0xFF);
        var owner = NewEntity();
        owner.PosX = 1 << 16;
        owner.PosY = 2 << 16;
        owner.PosZ = 3 << 16;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Equal((1 + 0x10) << 16, owner.PosX);
        Assert.Equal((2 + 0x20) << 16, owner.PosY);
        Assert.Equal((3 + 0x30) << 16, owner.PosZ);
    }

    [Fact]
    public void SetEntityShadowSize_0xAC_RewritesFirstMatchOnly()
    {
        var document = NewDocument(0xAC, 0x82, 5, 0xFF);
        var first = NewEntity();
        first.Status = EntityStatus.Normal;
        var second = NewEntity();
        second.Status = EntityStatus.Normal;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(first);
        context.SpawnedEntitiesList.Add(second);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(5u, EntityFlags.GetShadowSize(first.Flags));
        Assert.Equal(0u, EntityFlags.GetShadowSize(second.Flags));
    }

    [Fact]
    public void DestroyEntity_0x2E_NoWorldContext_DoesNotThrow_StillMatchesOwner()
    {
        var document = NewDocument(0x2E, 0x80, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        // "get owner" (functionId 0) always matches the owner itself, independent of
        // NoOpEntityWorldContext's empty SpawnedEntities list (see EntitySearchService's own doc) - so
        // Result is still 1, and NoOpEntityWorldContext.DestroyEntity swallows the call without throwing.
        Assert.Equal(1, state.Result);
    }

    // -----------------------------------------------------------------------------------------
    // Result carry across sequential slot-A calls (shared scratch EventProgramState)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RunScript_SlotA_ResultCarriesAcrossSequentialCalls()
    {
        // Block 1 (@0, program index masked to 0): 0x2E DestroyEntity(v1=0x80 "get owner") - matches the
        // owner itself unconditionally (EntitySearchService's functionId 0), so this always sets
        // Result=1, regardless of what SpawnedEntities the context (none here) knows about.
        //
        // Block 2 (@3, program index masked to 1): 0x03 IfTrueGoto +6 -> @9 SetAnim(2) if Result != 0,
        // else falls through (size 3) to @6 SetAnim(1). Block 2 never runs any opcode of its own that
        // would set Result - whichever value it sees came only from block 1's earlier, unrelated call.
        var document = new EventProgramDocument
        {
            EventCodesATable = new[] { 0, 3, 0, 0, 0, 0 },
            Codes = new[]
            {
                /* @0 */ 0x2E, 0x80, 0xFF,
                /* @3 */ 0x03, 6, 0,
                /* @6 */ 0x1A, 1, 0xFF,
                /* @9 */ 0x1A, 2, 0xFF,
            },
        };
        var runner = NewRunner(document);

        var entityA = NewEntity();
        entityA.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x80; // masked 0x7f -> table[0] -> @0
        runner.RunScript(entityA, ScriptHelper.ProgramALoad);

        var entityB = NewEntity();
        entityB.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x81; // masked 0x7f -> table[1] -> @3
        runner.RunScript(entityB, ScriptHelper.ProgramALoad);

        // If Result had NOT carried over (the old "new EventProgramState() per call" behaviour), the
        // shared state's Result would start this second call at its C# default (0) and entityB would end
        // up with TargetAnimationId=1 (the not-taken fallthrough) instead.
        Assert.Equal(2u, entityB.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // RunScript slot policy (EntityEventHandlers.cs:232-296)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RunScript_SlotB_ResumesAcrossCalls_InsteadOfRestartingTheProgram()
    {
        // @0: 0x37 Wait(1) (2 bytes); @2: 0x1A SetAnim(9) (2 bytes); @4: 0xFF End.
        var document = new EventProgramDocument
        {
            EventCodesBTable = new[] { 0 },
            Codes = new byte[] { 0x37, 1, 0x1A, 9, 0xFF }.Select(b => (int)b).ToArray(),
        };
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramBMap] = 0x80; // masked 0x7f -> table[0] -> @0

        runner.RunScript(entity, ScriptHelper.ProgramBMap); // suspends inside Wait(1), counter 0 -> 0
        Assert.Equal(0u, entity.TargetAnimationId); // not reached yet

        // If this call re-initialized instead of resuming entity.EventProgramState, CodeIndex/Parameters
        // would reset to the program's own start and Wait's counter would never reach 1 - the program
        // would suspend forever instead of ever reaching SetAnim(9).
        runner.RunScript(entity, ScriptHelper.ProgramBMap);
        Assert.Equal(9u, entity.TargetAnimationId);
    }

    [Fact]
    public void RunScript_SlotC_MapEventProgramIdNotTick_RestoresLastTargetAnimationAndDirection()
    {
        // A single 0x00 Break (1 byte) so the call suspends immediately without touching Target*
        // itself - only RunScript's own slot-C prelude (EntityEventHandlers.cs:255-260) should move
        // LastTargetAnimationId/LastTargetDirection back onto TargetAnimationId/TargetDirection.
        var document = new EventProgramDocument
        {
            EventCodesCTable = new[] { 0 },
            Codes = new[] { 0x00 },
        };
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80; // masked 0x7f -> table[0] -> @0
        entity.LastTargetAnimationId = 7;
        entity.LastTargetDirection = 3;
        entity.TargetAnimationId = 99;
        entity.TargetDirection = 99;

        // First call: entity.EventProgramState.Codes is still null (never resumed before), so RunScript
        // falls into InitializeEventData instead of the "resume" branch - the Last* restore only happens
        // on the RESUME path (Codes != null AND MapEventProgramId != ProgramCTick), so seed
        // MapEventProgramId to something else and run it a first time to populate Codes, then a second
        // time to actually exercise the restore.
        entity.MapEventProgramId = ScriptHelper.ProgramALoad;
        runner.RunScript(entity, ScriptHelper.ProgramCTick); // suspends on Break, Codes now non-null
        entity.TargetAnimationId = 99; // clobber again so the second call's restore is observable
        entity.TargetDirection = 99;
        entity.MapEventProgramId = ScriptHelper.ProgramALoad; // != ProgramCTick -> restore should fire

        runner.RunScript(entity, ScriptHelper.ProgramCTick);

        Assert.Equal(7u, entity.TargetAnimationId);
        Assert.Equal(3u, entity.TargetDirection);
    }

    [Fact]
    public void RunScript_DefaultSlot_AlwaysReInitializes_EvenAfterASuspend()
    {
        // Slot A (Load), like D/E, never resumes - see the shared-scratch-state doc. @0: 0x01 no-op
        // (1 byte, just so the Wait below does not coincidentally start at CodeIndex 0 - Wait keys its
        // own re-entrancy off "Parameters[1] != CodeIndex", and a freshly-cleared Parameters[1] is
        // already 0); @1: 0x37 Wait(5) (2 bytes, would suspend for 5 frames if ever allowed to keep
        // counting) - if RunScript resumed instead of re-initializing, enough calls would eventually
        // reach past it; since it always re-initializes, every call restarts Wait's own counter from
        // scratch, so SetAnim(3) is never reached no matter how many calls are made.
        var document = new EventProgramDocument
        {
            EventCodesATable = new[] { 0 },
            Codes = new[] { 0x01, 0x37, 5, 0x1A, 3, 0xFF },
        };
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x80;

        for (var i = 0; i < 8; i++)
        {
            runner.RunScript(entity, ScriptHelper.ProgramALoad);
        }

        Assert.Equal(0u, entity.TargetAnimationId);
    }

    [Fact]
    public void RunScript_SlotF_ZeroesThePlayersOwnForces_NotTheRunningEntitys()
    {
        var document = new EventProgramDocument
        {
            EventCodesFTable = new[] { 0 },
            Codes = new[] { 0x00 }, // Break - suspend immediately, nothing else to interpret
        };
        var player = NewEntity();
        player.ForceX = 11;
        player.ForceY = 22;
        player.ForceStepX = 33;
        player.ForceStepY = 44;

        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);

        var npc = NewEntity();
        npc.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x80;
        npc.ForceX = 111;
        npc.ForceY = 222;
        npc.ForceStepX = 333;
        npc.ForceStepY = 444;

        runner.RunScript(npc, ScriptHelper.ProgramFInteract);

        Assert.Equal(0, player.ForceX);
        Assert.Equal(0, player.ForceY);
        Assert.Equal(0, player.ForceStepX);
        Assert.Equal(0, player.ForceStepY);

        // The entity actually running slot F keeps its own forces untouched - only the player's are
        // zeroed (EntityEventHandlers.cs:266-273).
        Assert.Equal(111, npc.ForceX);
        Assert.Equal(222, npc.ForceY);
        Assert.Equal(333, npc.ForceStepX);
        Assert.Equal(444, npc.ForceStepY);
    }

    [Fact]
    public void RunScript_SlotF_NoPlayerEntity_DoesNotThrow()
    {
        var document = new EventProgramDocument
        {
            EventCodesFTable = new[] { 0 },
            Codes = new[] { 0x00 },
        };
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext, PlayerEntity == null
        var entity = NewEntity();
        entity.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x80;

        runner.RunScript(entity, ScriptHelper.ProgramFInteract); // must not throw
    }

    // ---------------------------------------------------------------------------------------------
    // E4.b: 0x16/0x17 (gravity flag + controller bridge), 0x1B (Fly - vertical impulse), 0x70 (IsOnGround).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void HighGravity_0x16_SetsFlagAndAppliesMapGravityToController()
    {
        // @0: 0x16 High gravity; @1: End.
        var document = NewDocument(0x16, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();
        entity.MapGravity = 1250f;
        entity.MapMaxFallSpeed = 800f;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(EntityFlags.Gravity, entity.Flags & EntityFlags.Gravity);
        Assert.Equal(1250f, entity.Controller.Settings.Gravity);
        Assert.Equal(800f, entity.Controller.Settings.MaxFallSpeed);
    }

    [Fact]
    public void LowGravity_0x17_ClearsFlagAndZeroesControllerGravity()
    {
        // @0: 0x17 Low gravity; @1: End.
        var document = NewDocument(0x17, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Flags = EntityFlags.Gravity;
        entity.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();
        entity.MapGravity = 1250f;
        entity.MapMaxFallSpeed = 800f;
        entity.Controller.Settings.Gravity = 1250f;
        entity.Controller.Settings.MaxFallSpeed = 800f;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0u, entity.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, entity.Controller.Settings.Gravity);
        Assert.Equal(0f, entity.Controller.Settings.MaxFallSpeed);
    }

    [Fact]
    public void LowGravity_0x17_NoController_DoesNotThrow()
    {
        var document = NewDocument(0x17, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Flags = EntityFlags.Gravity;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // must not throw

        Assert.Equal(0u, entity.Flags & EntityFlags.Gravity);
    }

    [Fact]
    public void Fly_0x1B_BlockEighteenRealImpulse_SetsForceZAndControllerVerticalVelocity()
    {
        // Real block-18 (masked index 18, entity record 18/bank 25 "Bloc transparent (1x1x2)") program 146
        // impulse - docs/intro-programs-389.txt offset 1620: "0x1B Fly params=[0,255]".
        // ForceZ = SignExtend16((255<<8)|0) * 0x10000 >> 8 = SignExtend16(0xFF00) * 0x10000 >> 8
        //        = -256 * 0x10000 >> 8 = -65536 (16.16 -> -1.0 px/tick, i.e. -50 px/s at the 50 Hz tick rate).
        var document = NewDocument(0x1B, 0, 255, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(-65536, entity.ForceZ);
        Assert.Equal(-50f, entity.Controller.Velocity.Y); // Y-up default axis (no World -> ResolveUp falls back to Vector3.Up).
    }

    [Fact]
    public void Fly_0x1B_NoController_OnlyStoresForceZ()
    {
        var document = NewDocument(0x1B, 0, 255, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // must not throw without a controller

        Assert.Equal(-65536, entity.ForceZ);
        Assert.Null(entity.Controller);
    }

    [Fact]
    public void Fly_0x1B_PositiveImpulse_SignExtendsCorrectly()
    {
        // v1=0,v2=1 -> (1<<8|0)=0x100 -> SignExtend16(0x100)=256 (positive, MSB clear) -> *0x10000>>8 = 65536.
        var document = NewDocument(0x1B, 0, 1, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(65536, entity.ForceZ);
    }

    [Fact]
    public void IsAboveGround_0x70_ReadsProxyIsOnGround()
    {
        // @0: 0x70 Is above ground; @1: End.
        var document = NewDocument(0x70, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.IsOnGround = 1;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void IsAboveGround_0x70_FallingEntity_ResultZero()
    {
        var document = NewDocument(0x70, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.IsOnGround = 0;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.Result);
    }

    // ---------------------------------------------------------------------------------------------
    // E4.c: 0x19 (Deactivate), 0x0A (ReverseDirection), 0x49/0x4B (Restart), 0x10/0x11 (control lock),
    // 0x38 (save map index table), 0x27 (Face player), 0x5A/0x5B (Turn entity [+ anim]),
    // 0x07 (Check entity in area). See docs/plan-e4-deplacement-scripte.md "E4.c".
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Deactivate_0x19_SetsStatusToDeactivated()
    {
        var document = NewDocument(0x19, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Status = EntityStatus.Normal;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Equal(1, state.CodeIndex); // 1-byte instruction (EventOpcodeSizeTable: 0x19 size 1).
    }

    [Fact]
    public void Deactivate_0x19_FromLoaded_AlsoBecomesDeactivated()
    {
        var document = NewDocument(0x19, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Status = EntityStatus.Loaded;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
    }

    [Fact]
    public void ReverseDirection_0x0A_AddsSixteen()
    {
        var document = NewDocument(0x0A, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.TargetDirection = 4;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        // (4 + 0x10) & 0x1f = 0x14 = 20.
        Assert.Equal(20u, entity.TargetDirection);
    }

    [Fact]
    public void ReverseDirection_0x0A_WrapsAroundThirtyTwo()
    {
        var document = NewDocument(0x0A, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.TargetDirection = 0x1F; // 31

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        // (31 + 16) & 0x1f = 47 & 31 = 15.
        Assert.Equal(15u, entity.TargetDirection);
    }

    [Fact]
    public void Restart_0x49_JumpsBackToProgramStartAndResumesExecution()
    {
        // @0: SetAnim(9), size 2; @2: Break (0x00); @5: Restart (0x49), size 1; @6: End.
        var document = NewDocument(0x1A, 9, 0, 0, 0, 0x49, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        // Parameters[0] pinned to 0 - reproduces InitializeEventData's own "program start" bookkeeping
        // (AlundraEventProgramRunner.InitializeEventData: state.Parameters[0] = state.CodeIndex) by hand,
        // since this test drives RunOneScriptCall directly rather than through RunScript/InitializeEventData.
        var state = new EventProgramState { Codes = document.CodesAsBytes(), CodeIndex = 5, Parameters = { [0] = 0 } };

        runner.RunOneScriptCall(entity, state);

        // The jump actually landed on @0 and re-executed SetAnim(9) (not just moved CodeIndex): @0
        // SetAnim(9) -> @2 Break -> suspend, CodeIndex left at 3 (Break's own CodeIndex++, see
        // RunOneScriptCall's own doc on command==0x00).
        Assert.Equal(9u, entity.TargetAnimationId);
        Assert.Equal(3, state.CodeIndex);
    }

    [Fact]
    public void IfFalseRestart_0x4B_ResultZero_JumpsBackToProgramStart()
    {
        var document = NewDocument(0x1A, 9, 0, 0, 0, 0x4B, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), CodeIndex = 5, Result = 0, Parameters = { [0] = 0 } };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(9u, entity.TargetAnimationId);
    }

    [Fact]
    public void IfFalseRestart_0x4B_ResultNonZero_AdvancesPastInstructionInstead()
    {
        var document = NewDocument(0x1A, 9, 0, 0, 0, 0x4B, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), CodeIndex = 5, Result = 1, Parameters = { [0] = 0 } };

        runner.RunOneScriptCall(entity, state);

        // No jump - advances by 1 (its own instruction size) to @6 (0xFF, End); the anim at @0 never runs.
        Assert.Equal(0u, entity.TargetAnimationId);
        Assert.Equal(6, state.CodeIndex);
    }

    [Fact]
    public void PlayerLoseControl_0x10_SetsControlLockedBit()
    {
        var document = NewDocument(0x10, 0xFF);
        var gameState = new AlundraGameState();
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(
            AlundraGameState.PlayerControlBits.ControlLocked,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked);
    }

    [Fact]
    public void PlayerGainControl_0x11_ClearsControlLockedBit_PreservesOtherBits()
    {
        var document = NewDocument(0x11, 0xFF);
        var gameState = new AlundraGameState
        {
            PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked | AlundraGameState.PlayerControlBits.MenuOpen,
        };
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0u, gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked);
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);
    }

    [Fact]
    public void SetSaveMapIdToInternalMapIndex_0x38_RealMap389Operands_WritesTable()
    {
        // Real Load-program operand bytes (docs/intro-programs-389.txt offset 305):
        // params=[17,0,183,1] -> index = (0<<8)|17 = 17; value = (1<<8)|183 = 439.
        var document = NewDocument(0x38, 17, 0, 183, 1, 0xFF);
        var gameState = new AlundraGameState();
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal((ushort)439, gameState.MapIdToInternalMapIndexTable[17]);
    }

    [Fact]
    public void SetSaveMapIdToInternalMapIndex_0x38_OutOfRangeIndex_SkipsWithoutThrowing()
    {
        // v1=255,v2=255 -> index = 0xFFFF = 65535, far beyond the table's 500 entries.
        var document = NewDocument(0x38, 255, 255, 1, 0, 0xFF);
        var gameState = new AlundraGameState();
        var runner = NewRunner(document, gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // must not throw

        // Untouched elsewhere - the identity seed at a real index survives.
        Assert.Equal((ushort)17, gameState.MapIdToInternalMapIndexTable[17]);
    }

    [Fact]
    public void FacePlayer_0x27_HandComputedCardinalCase_PlayerDueEast()
    {
        var document = NewDocument(0x27, 0xFF);
        var entity = NewEntity();
        entity.PosX = 0;
        entity.PosY = 0;
        var player = NewEntity();
        player.PosX = 100 << 16; // due east, 16.16 fixed-point (same shape 0x27 actually feeds GetDirectionToTarget)
        player.PosY = 0;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        // ScriptHelper.GetDirectionToTarget(100<<16, 0): y<1 -> flipper=2; x>=0 unaffected; greatest=x;
        // div picks the shift bringing x into 0..15 (DivTable[19]=0x7fffff >= 6553600 > DivTable[18]) ->
        // x>>19=12, y>>19=0 -> DirectionTable[0*16+12] (row 0, all zero) = 0 -> ret = 0x18-0 = 0x18 = 24,
        // matching AnimationTables.CardinalDirectionTable[3] (east/right).
        Assert.Equal(24u, entity.TargetDirection);
    }

    [Fact]
    public void FacePlayer_0x27_HandComputedDiagonalCase_PlayerUpAndRight()
    {
        var document = NewDocument(0x27, 0xFF);
        var entity = NewEntity();
        entity.PosX = 0;
        entity.PosY = 0;
        var player = NewEntity();
        player.PosX = 100 << 16;
        player.PosY = -100 << 16;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        // ScriptHelper.GetDirectionToTarget(100<<16, -100<<16): y<1 -> flipper=2; x>=0 unaffected; |y|=|x|
        // -> the div-loop shift lands both on the SAME quotient the un-scaled x=100,y=-100 case would
        // (12,12 - the octant ratio, not the raw magnitude, drives the table lookup) ->
        // DirectionTable[12*16+12] = row12[index12] = 0x4 -> ret = 0x18-4 = 0x14 = 20 (halfway between
        // up=0x10 and right=0x18, as expected for an exact 45-degree target).
        Assert.Equal(20u, entity.TargetDirection);
    }

    [Fact]
    public void FacePlayer_0x27_NoPlayerSpawned_LeavesDirectionUnchanged()
    {
        var document = NewDocument(0x27, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext.PlayerEntity == null
        var entity = NewEntity();
        entity.TargetDirection = 7;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // must not throw

        Assert.Equal(7u, entity.TargetDirection);
    }

    [Fact]
    public void TurnEntity_0x5A_RealMap389Params_Mode2Cardinal()
    {
        // Real Load-program operand bytes (docs/intro-programs-389.txt offset 1297): params=[15,64].
        // v1=15 (raw EntityRefId search, 0x80 clear), v2=64=0b01000000 -> mode = 64>>5 = 2 (cardinal),
        // encodedDir&3 = 0 -> AnimationTables.CardinalDirectionTable[0] = 0 (down).
        var document = NewDocument(0x5A, 15, 64, 0xFF);
        var target = NewEntity();
        target.EntityRefId = 15;
        var owner = NewEntity();
        // Raw EntityRefId search (0x80 clear) is gated on the OWNER's own status (EntitySearchService,
        // GameEngine.cs:1940-1953) - Status defaults to Destroyed (0), which would gate this off.
        owner.Status = EntityStatus.Normal;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0u, target.TargetDirection);
    }

    [Fact]
    public void TurnEntityWithAnim_0x5B_RealMap389Params_SetsAnimAndDirection()
    {
        // Real Load-program operand bytes (docs/intro-programs-389.txt offset 1126): params=[128,1,66].
        // v1=128 -> function id 0 (owner); v2=1 (TargetAnimationId); v3=66=0b01000010 -> mode 2, &3=2 ->
        // CardinalDirectionTable[2] = 0x08 (left).
        var document = NewDocument(0x5B, 128, 1, 66, 0xFF);
        var owner = NewEntity();
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1u, owner.TargetAnimationId);
        Assert.Equal(0x08u, owner.TargetDirection);
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode0Direct_ReturnsLowFiveBitsVerbatim()
    {
        var runner = NewRunner(NewDocument(0xFF));
        var entity = NewEntity();

        // encodedDir=9 -> mode = 9>>5 = 0 -> result = 9&0x1f = 9.
        Assert.Equal(9u, runner.ResolveDirectionFromParam(entity, 9));
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode1RelativeToOwnTargetDirection()
    {
        var runner = NewRunner(NewDocument(0xFF));
        var entity = NewEntity();
        entity.TargetDirection = 6;

        // encodedDir = 0x23 = 0b00100011 -> mode=1, result=3 -> (6+3)&0x1f = 9.
        Assert.Equal(9u, runner.ResolveDirectionFromParam(entity, 0x23));
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode3TowardPlayerPlusOffset()
    {
        var player = NewEntity();
        player.PosX = 100 << 16;
        player.PosY = 0;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(NewDocument(0xFF), worldContext: context);
        var entity = NewEntity();
        entity.PosX = 0;
        entity.PosY = 0;

        // encodedDir = 0x61 = 0b01100001 -> mode=3. GetDirectionToTarget(100<<16,0) = 24 (east, see
        // FacePlayer_0x27_HandComputedCardinalCase_PlayerDueEast's own derivation). result added is the
        // FULL encodedDir byte (0x61 = 97), not just its low 5 bits: (24 + 97) & 0x1f = 121 & 31 = 25.
        Assert.Equal(25u, runner.ResolveDirectionFromParam(entity, 0x61));
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode3NoPlayer_TreatsToPlayerDirectionAsZero()
    {
        var runner = NewRunner(NewDocument(0xFF)); // no worldContext -> PlayerEntity null
        var entity = NewEntity();

        // mode=3, encodedDir=0x60=96 -> (0 + 96)&0x1f = 96&31 = 0.
        Assert.Equal(0u, runner.ResolveDirectionFromParam(entity, 0x60));
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode6PlayersOwnTargetDirectionPlusOffset()
    {
        var player = NewEntity();
        player.TargetDirection = 8;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(NewDocument(0xFF), worldContext: context);
        var entity = NewEntity();

        // encodedDir = 0xC3 = 0b11000011 -> mode=6, result=3 -> (8+3)&0x1f = 11.
        Assert.Equal(11u, runner.ResolveDirectionFromParam(entity, 0xC3));
    }

    [Fact]
    public void ResolveDirectionFromParam_Mode7NoActiveWarpEntity_FallsBackToLowFiveBits()
    {
        // Mode 7 (warp facing) - this runtime never has a "g_activeCollisionEntity", so
        // GetWarpFacingDirection always returns -1 (see that method's own doc), matching the original's
        // own fallback for every entity that is not mid-warp.
        var runner = NewRunner(NewDocument(0xFF));
        var entity = NewEntity();

        // encodedDir = 0xE5 = 0b11100101 -> mode=7, result=5 -> facingDirection==-1 -> returns result=5.
        Assert.Equal(5u, runner.ResolveDirectionFromParam(entity, 0xE5));
    }

    [Theory]
    [InlineData(0x80u)] // mode 4 (random cardinal), result=0.
    [InlineData(0xA0u)] // mode 5 (random 0..31), result=0.
    public void ResolveDirectionFromParam_RandomModes_ThrowNotSupported(uint encodedDir)
    {
        var runner = NewRunner(NewDocument(0xFF));
        var entity = NewEntity();

        // Pre-read census (docs/plan-e4-deplacement-scripte.md "MANDATORY PRE-READ"): no 0x5A/0x5B
        // occurrence in map 389's own programs decodes to mode 4/5 - this path is provably unreached in
        // practice, so it must fail loudly instead of silently guessing a direction (no faithful PSX RNG
        // port exists).
        Assert.Throws<System.NotSupportedException>(() => runner.ResolveDirectionFromParam(entity, encodedDir));
    }

    [Fact]
    public void CheckEntityInArea_0x07_MatchInsideBox_ResultOne()
    {
        var document = NewDocument(0x07, 0x80, 10, 20, 30, 40, 0, 5, 0xFF); // v1=0x80 (owner)
        var owner = NewEntity();
        owner.TileX = 15;
        owner.TileY = 35;
        owner.TileZ = 2;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1, state.Result);
        Assert.Equal(8, state.CodeIndex); // stopped at 0xFF
    }

    [Fact]
    public void CheckEntityInArea_0x07_MatchOutsideBox_ResultZero()
    {
        var document = NewDocument(0x07, 0x80, 10, 20, 30, 40, 0, 5, 0xFF);
        var owner = NewEntity();
        owner.TileX = 25; // outside [10,20]
        owner.TileY = 35;
        owner.TileZ = 2;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void CheckEntityInArea_0x07_RealMap389Params_TileZDerivedFromPosZ()
    {
        // Real Tick-program operand bytes (docs/intro-programs-389.txt offset 1131):
        // params=[128,18,18,0,59,20,60] -> v1=0x80 (owner), box x:[18,18] y:[0,59] z:[20,60].
        var document = NewDocument(0x07, 128, 18, 18, 0, 59, 20, 60, 0xFF);
        var owner = NewEntity();
        owner.TileX = 18;
        owner.TileY = 40;
        owner.PosZ = 30 << 20; // TileZ = PosZ >> 20 = 30, inside [20,60].
        owner.TileZ = owner.PosZ >> 20;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(owner);
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1, state.Result);
    }
}
