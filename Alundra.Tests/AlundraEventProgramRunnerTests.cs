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
}
