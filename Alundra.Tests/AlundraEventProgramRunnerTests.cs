using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
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
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public readonly List<(int X, int Y, int Z)> ForcedCameraLookAtCalls = new();
        public void SetForcedCameraLookAt(int x, int y, int z)
        {
            EntityFollowedByCamera = null;
            ForcedCameraLookAtCalls.Add((x, y, z));
        }

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

        public NavigationGrid2D? NavigationGrid { get; set; }

        // E7.a (docs/plan-e7-mutation-tuiles.md): declaring a plain public property with the interface's
        // own member signature overrides IEntityWorldContext.CellMutator's default-interface-member "=>
        // null" for THIS class - see IEntityWorldContext's own doc on why the default member exists.
        public IAlundraCellMutator? CellMutator { get; set; }

        // E11.a (docs/plan-e11-audio.md): same override shape as CellMutator above - overrides
        // IEntityWorldContext.SoundPlayer's default-interface-member "=> null" for THIS class.
        public IAlundraSoundPlayer? SoundPlayer { get; set; }

        // E10.b (docs/plan-e10-fondu.md): same override shape as SoundPlayer above - overrides
        // IEntityWorldContext.ScreenFadeDirector's default-interface-member "=> null" for THIS class.
        public IAlundraScreenFadeDirector? ScreenFadeDirector { get; set; }
    }

    /// <summary>Records every <see cref="BeginFadeEffect"/>/<see cref="SetWarpFadeDuration"/> call, in
    /// order - the dispatch-level oracle for 0xAF/0xB0's own operand extraction (E10.b,
    /// docs/plan-e10-fondu.md). <see cref="IsSettled"/> is settable so 0xB1's own dispatch (Result written
    /// both ways) is directly testable without a real <see cref="AlundraScreenFadeDirector"/>.</summary>
    private sealed class FakeScreenFadeDirector : IAlundraScreenFadeDirector
    {
        public readonly List<(int R, int G, int B, int Tpage, int Duration, int Persist)> BeginFadeCalls = new();
        public readonly List<(int R, int G, int B, int Duration)> SetWarpFadeDurationCalls = new();
        public bool IsSettled { get; set; }

        public void BeginFadeEffect(int r, int g, int b, int tpage, int duration, int persistLock)
            => BeginFadeCalls.Add((r, g, b, tpage, duration, persistLock));

        public void SetWarpFadeDuration(int r, int g, int b, int duration)
            => SetWarpFadeDurationCalls.Add((r, g, b, duration));
    }

    /// <summary>Records every <see cref="PlaySfx"/> call, in order - T6's own oracle for the exact id
    /// each opcode derived (docs/plan-e11-audio.md, slice E11.a).</summary>
    private sealed class FakeSoundPlayer : IAlundraSoundPlayer
    {
        public readonly List<int> Requests = new();

        public void PlaySfx(int sfxId) => Requests.Add(sfxId);
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

    /// <summary>Bug fix (user-reported runtime timing bug, gull entity 6): 0x62/0x63 must resync a
    /// matched entity's <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent"/>
    /// (gravity AND walkability mask) whenever they touch <see cref="AlundraEntityScriptProxy.Flags"/> on
    /// an entity that carries one - see <see cref="AlundraEntityScriptProxy.ResyncControllerFromFlags"/>'s
    /// own doc. Sets Gravity (0x100) via v2/v3 = (0,1) -&gt; flag = (1&lt;&lt;8)|0 = 0x100, the exact byte
    /// pair the real gull program uses (just the OR direction, not the clear direction below).</summary>
    [Fact]
    public void SetEntitiesFlagsLow16_0x62_ResyncsControllerGravityAndWalkabilityMask()
    {
        var document = NewDocument(0x62, 0x82, 0, 1, 0xFF); // flag = (1<<8)|0 = 0x100 = Gravity.
        var target = NewEntity();
        target.Status = EntityStatus.Normal;
        target.Flags = EntityFlags.ClassA; // mask should already reflect ClassA once resynced.
        target.MapGravity = 1250f;
        target.MapMaxFallSpeed = 800f;
        target.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        // Bug fix follow-up (gull entity 6, map 389, measured ~158 px/s vs the faithful 150 px/s): the
        // engine's own Settings.Gravity/MaxFallSpeed now stay permanently pinned at 0 for every
        // controller-driven NPC, at every Flags.Gravity state - see
        // AlundraEntityScriptProxy.ApplyGravitySettingsToController's own doc. The Gravity bit this opcode
        // sets still matters: it now gates AlundraEntityScriptProxy.EvaluateEntitySupport's own per-tick
        // ForceZ decay instead of the controller's settings.
        Assert.Equal(EntityFlags.Gravity, target.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, target.Controller.Settings.Gravity);
        Assert.Equal(0f, target.Controller.Settings.MaxFallSpeed);
        Assert.Equal(AlundraCellsCollisionField.WalkabilityMaskFor(target.Flags), target.Controller.Settings.WalkabilityMask);
        Assert.Equal(0x1040u, target.Controller.Settings.WalkabilityMask); // base 0x40 | ClassA's own 0x1000.
    }

    /// <summary>The gull-6 bug's own exact repro shape: Tick program 134's real 0x63 [128,0,1] (clear
    /// mask (1&lt;&lt;8)|0 = 0x100 = Gravity) at the climb apex used to leave the controller's own cached
    /// <c>Settings.Gravity</c> stuck at its spawn-time value - this asserts the resync now zeroes it.
    /// </summary>
    [Fact]
    public void ClearEntitiesFlagsLow16_0x63_ResyncsControllerGravityAndWalkabilityMask()
    {
        var document = NewDocument(0x63, 0x82, 0, 1, 0xFF); // clear mask = (1<<8)|0 = 0x100 = Gravity.
        var target = NewEntity();
        target.Status = EntityStatus.Normal;
        target.Flags = EntityFlags.Gravity | EntityFlags.ClassB;
        target.MapGravity = 1250f;
        target.MapMaxFallSpeed = 800f;
        target.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();
        target.Controller.Settings.Gravity = 1250f;
        target.Controller.Settings.MaxFallSpeed = 800f;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0u, target.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, target.Controller.Settings.Gravity);
        Assert.Equal(0f, target.Controller.Settings.MaxFallSpeed);
        Assert.Equal(AlundraCellsCollisionField.WalkabilityMaskFor(target.Flags), target.Controller.Settings.WalkabilityMask);
        Assert.Equal(0x41u, target.Controller.Settings.WalkabilityMask); // base 0x40 | ClassB's own 0x01, Gravity cleared, ClassA never set.
    }

    [Fact]
    public void SetEntitiesFlagsLow16_0x62_NoController_DoesNotThrow()
    {
        var document = NewDocument(0x62, 0x82, 0, 1, 0xFF);
        var target = NewEntity();
        target.Status = EntityStatus.Normal;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(target);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state); // must not throw without a controller

        Assert.Equal(EntityFlags.Gravity, target.Flags & EntityFlags.Gravity);
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

    // -----------------------------------------------------------------------------------------
    // Camera follow opcodes (0x67, 0x68, 0x69) - E5.a, docs/plan-e5-camera.md. Real map-389 operands
    // (docs/intro-programs-389.txt) for 0x67 - params=[11]/[12] (raw entity-record-id search, sailors 11
    // and 12) - the map's own six 0x67 occurrences never carry a non-matching or 0x80-flavored operand,
    // so the "no match" case below uses a synthetic id instead. Map 389 has no 0x68/0x69 occurrence at
    // all (grep of docs/intro-programs-389.txt), so both use synthetic operands.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CameraFollowEntity_0x67_RealMap389Param_DesignatesTheMatchingEntity()
    {
        // params=[11] - raw entity-record-id search (bit 0x80 clear), matches EntityRefId == 11 (sailor
        // 11, real operand of map-389 program offset 1436 - docs/intro-programs-389.txt).
        var document = NewDocument(0x67, 11, 0xFF);
        var sailor11 = NewEntity();
        sailor11.Status = EntityStatus.Normal;
        sailor11.EntityRefId = 11;
        var other = NewEntity();
        other.Status = EntityStatus.Normal;
        other.EntityRefId = 12;
        var context = new FakeEntityWorldContext();
        context.SpawnedEntitiesList.Add(other);
        context.SpawnedEntitiesList.Add(sailor11);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        owner.Status = EntityStatus.Normal; // raw entity-id search gates on the OWNER's own status too.
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Same(sailor11, context.EntityFollowedByCamera);
    }

    [Fact]
    public void CameraFollowEntity_0x67_NoMatch_SetsNull_DoesNotKeepPreviousTarget()
    {
        var document = NewDocument(0x67, 99, 0xFF); // no entity carries EntityRefId 99.
        var previousTarget = NewEntity();
        previousTarget.Status = EntityStatus.Normal;
        previousTarget.EntityRefId = 5;
        var context = new FakeEntityWorldContext { EntityFollowedByCamera = previousTarget };
        context.SpawnedEntitiesList.Add(previousTarget);
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        owner.Status = EntityStatus.Normal;
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Null(context.EntityFollowedByCamera);
    }

    [Fact]
    public void CameraStopFollowEntity_0x68_SetsNull()
    {
        var document = NewDocument(0x68, 0xFF);
        var previousTarget = NewEntity();
        var context = new FakeEntityWorldContext { EntityFollowedByCamera = previousTarget };
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Null(context.EntityFollowedByCamera);
    }

    [Fact]
    public void CameraForceLookAt_0x69_NullsFollowedEntity_AndForwardsPackedCoordinates()
    {
        // v1..v6 = 1,2,3,4,5,6 -> X = 1|(2<<8) = 513, Y = 3|(4<<8) = 1027, Z = 5|(6<<8) = 1541 (synthetic
        // - map 389 has no real 0x69 occurrence, see this section's own doc).
        var document = NewDocument(0x69, 1, 2, 3, 4, 5, 6, 0xFF);
        var previousTarget = NewEntity();
        var context = new FakeEntityWorldContext { EntityFollowedByCamera = previousTarget };
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(owner, state);

        Assert.Null(context.EntityFollowedByCamera);
        Assert.Equal(new[] { (513, 1027, 1541) }, context.ForcedCameraLookAtCalls);
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
    public void HighGravity_0x16_SetsFlagAndKeepsControllerGravityAtZero()
    {
        // Bug fix (gull entity 6, map 389): a controller-driven NPC's vertical is now entirely owned by
        // the DLL's own per-tick decay (AlundraEntityScriptProxy.EvaluateEntitySupport), never by the
        // engine's own continuous Settings.Gravity/MaxFallSpeed (which used to integrate at render rate,
        // not the original's 50 Hz tick rate - see ApplyGravitySettingsToController's own doc for the
        // measured numbers). 0x16 still sets the Flags Gravity bit - it now gates EvaluateEntitySupport's
        // own decay instead of the controller's settings, which stay pinned at 0.
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
        Assert.Equal(0f, entity.Controller.Settings.Gravity);
        Assert.Equal(0f, entity.Controller.Settings.MaxFallSpeed);
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
    public void Fly_0x1B_BlockEighteenRealImpulse_SetsForceZOnlyNeverPokesControllerVelocity()
    {
        // Real block-18 (masked index 18, entity record 18/bank 25 "Bloc transparent (1x1x2)") program 146
        // impulse - docs/intro-programs-389.txt offset 1620: "0x1B Fly params=[0,255]".
        // ForceZ = SignExtend16((255<<8)|0) * 0x10000 >> 8 = SignExtend16(0xFF00) * 0x10000 >> 8
        //        = -256 * 0x10000 >> 8 = -65536 (16.16 -> -1.0 px/tick, i.e. -50 px/s at the 50 Hz tick rate).
        //
        // Root-cause vertical-fidelity fix (see AlundraEntityScriptProxy.EvaluateEntitySupport's own doc):
        // 0x1B no longer pushes this impulse onto Controller.SetVerticalVelocity directly - only the DLL-
        // side ForceZ struct field is set here; EvaluateEntitySupport reads it (after its own per-tick
        // decay) and drives the entity through Controller.Move() every logic tick instead, so the
        // controller's own Velocity is left untouched by this opcode call alone.
        var document = NewDocument(0x1B, 0, 255, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.Controller = new CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(-65536, entity.ForceZ);
        Assert.Equal(0f, entity.Controller.Velocity.X);
        Assert.Equal(0f, entity.Controller.Velocity.Y);
        Assert.Equal(0f, entity.Controller.Velocity.Z);
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

    // ---------------------------------------------------------------------------------------------
    // E4.d: 0x1E "Walk" / 0x1F "Walk with collision" - core suspend/complete logic (opcode-level,
    // synthetic PosX/PosY - see AlundraNpcCharacterControllerMoverTests for the real-mover integration
    // scenarios: real program-139 walk, real wall + navigation detour, degraded no-grid mode over the
    // real map).
    // ---------------------------------------------------------------------------------------------

    private static NavigationGrid2D NewSyntheticGrid(int width, int height, params (int X, int Y)[] blockedCells)
    {
        var grid = new NavigationGrid2D(width, height, 1f);
        var blocked = new HashSet<(int, int)>(blockedCells);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                grid.SetCell(x, y, blocked.Contains((x, y))
                    ? NavigationGridCell.Blocked
                    : new NavigationGridCell(true, 1f, NavigationLayerMask.All));
            }
        }

        return grid;
    }

    [Fact]
    public void Walk_0x1E_FirstPass_MemorizesPositionAndSuspends()
    {
        // threshold = (v2<<8)|v1 = 24 px; params=[24,0].
        var document = NewDocument(0x1E, 24, 0, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.PosX = 1000 << 16;
        entity.PosY = 2000 << 16;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.CodeIndex); // suspended - still sitting at the 0x1E instruction.
        Assert.Equal(1000 << 16, state.Parameters[2]);
        Assert.Equal(2000 << 16, state.Parameters[3]);
    }

    [Fact]
    public void Walk_0x1E_BelowThreshold_KeepsSuspending()
    {
        var document = NewDocument(0x1E, 24, 0, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.PosX = 1000 << 16;
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // first pass

        entity.PosX += 20 << 16; // dx_px = 20 < 24
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.CodeIndex);
    }

    [Fact]
    public void Walk_0x1E_ExactThreshold_CompletesInclusive_AndResetsParameters()
    {
        // Script_30_01E's own comparison is "threshold <= dx" (inclusive), not "threshold < dx" - dx_px
        // exactly equal to the threshold must already complete the walk.
        var document = NewDocument(0x1E, 24, 0, 0x1A, 99, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.PosX = 1000 << 16;
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // first pass

        entity.PosX += 24 << 16; // dx_px = 24 == threshold.
        runner.RunOneScriptCall(entity, state);

        // Completion (result=3, nonzero) falls straight through to the marker 0x1A within the SAME call
        // (RunOneScriptCall only stops on a result of 0).
        Assert.Equal(99u, entity.TargetAnimationId);
        Assert.Equal(0, state.Parameters[1]); // reset by the generic post-dispatch bookkeeping.
    }

    [Fact]
    public void WalkWithCollision_0x1F_ForceAdjustedNonzero_EndsEarlyDespiteDistanceNotReached()
    {
        var document = NewDocument(0x1F, 24, 0, 0x1A, 77, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.PosX = 1000 << 16;
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // first pass

        entity.PosX += 5 << 16; // dx_px = 5, well under the 24 threshold.
        entity.ForceAdjusted = 1;
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(77u, entity.TargetAnimationId);
    }

    [Fact]
    public void WalkWithCollision_0x1F_NeitherDistanceNorForceAdjusted_KeepsSuspending()
    {
        var document = NewDocument(0x1F, 24, 0, 0x1A, 77, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        entity.PosX = 1000 << 16;
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        entity.PosX += 5 << 16;
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0u, entity.TargetAnimationId); // 0x1A never reached.
        Assert.Equal(0, state.CodeIndex);
    }

    [Fact]
    public void Walk_0x1E_ForceAdjustedWithNavigationGrid_EngagesDetourAndReDerivesDirection_WithoutEndingTheWalk()
    {
        // 10x10 synthetic grid, cell (5,5) blocked - directly between the entity's own cell (4,5) and the
        // projected destination cell (6,5) (threshold 24 + one-cell margin 24 = 48px east of the
        // memorized start, per TryEngageDetour's own doc), so TryFindPath must route around it.
        var grid = NewSyntheticGrid(10, 10, (5, 5));
        var context = new FakeEntityWorldContext { NavigationGrid = grid };
        var document = NewDocument(0x1E, 24, 0, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        entity.PosX = 108 << 16; // cell (4,5) center exactly (4*24+12).
        entity.PosY = 88 << 16; // cell (4,5) center exactly (5*16+8).
        entity.TargetDirection = 24; // east (OffsetXList[24]=0x300>0, OffsetYList[24]=0).

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // first pass - memorizes, no detour yet.

        entity.ForceAdjusted = 1; // simulates a curtailed sub-step between the two calls.
        runner.RunOneScriptCall(entity, state); // second call - PosX/PosY unchanged -> still suspended.

        Assert.Equal(0, state.CodeIndex); // 0x1E itself only ends via the ORIGINAL distance test.
        Assert.NotNull(entity.WalkDetourPath);
        Assert.True(entity.WalkDetourPath!.Points.Count > 2, "a genuine detour around the blocked cell should need more than a direct 2-point line.");
        Assert.NotEqual(24u, entity.TargetDirection); // re-derived toward the first real waypoint.
    }

    [Fact]
    public void Walk_0x1E_ForceAdjustedWithoutNavigationGrid_DoesNotDetour_KeepsPushingOriginalDirection()
    {
        var document = NewDocument(0x1E, 24, 0, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext -> NavigationGrid null.
        var entity = NewEntity();
        entity.PosX = 108 << 16;
        entity.PosY = 88 << 16;
        entity.TargetDirection = 24;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state); // first pass

        entity.ForceAdjusted = 1;
        runner.RunOneScriptCall(entity, state);

        Assert.Null(entity.WalkDetourPath);
        Assert.Equal(24u, entity.TargetDirection); // unchanged - degraded mode, original "keep pushing" behavior.
    }

    // -----------------------------------------------------------------------------------------
    // Cell mutation opcodes (0x54/0x55/0x85) - E7.a, docs/plan-e7-mutation-tuiles.md. A tiny 2x2
    // synthetic AlundraCellStore (built through the real TryCreate/TryCreate factories, not a hand-rolled
    // fake) exercises the real production mutation code, exactly like AlundraCellsCollisionFieldTests'
    // own synthetic-grid tests do for the field. Cell (0,0) starts with walkability=1/groundProperty=2/
    // slope=3/height=4/tileId=1000/wallTilesOffset=10, wall stack {offset:5, tiles:[11,22]}; cell (1,0)
    // starts with walkability=9/groundProperty=8/slope=7/height=6/tileId=2000/wallTilesOffset=20, wall
    // stack {offset:50, tiles:[99]} (deliberately SHORTER than cell (0,0)'s, to exercise the resize
    // branch); cell (0,1) starts all-zero with no wall stack (to exercise stack destruction).
    // -----------------------------------------------------------------------------------------

    private static AlundraCellStore NewSyntheticCellStore(out AlundraCellsCollisionField field)
    {
        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(2, 2);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":4,"
            + "\"walkability\":[1,9,0,0],"
            + "\"ground_property\":[2,8,0,0],"
            + "\"slope\":[3,7,0,0],"
            + "\"height\":[4,6,0,0],"
            + "\"tile_id\":[1000,2000,0,0],"
            + "\"wall_tiles_offset\":[10,20,-1,-1],"
            + "\"wall_tiles\":{\"0\":{\"offset\":5,\"tiles\":[11,22]},\"1\":{\"offset\":50,\"tiles\":[99]}}}";

        var fieldCreated = AlundraCellsCollisionField.TryCreate(tileMapData, "synthetic", out var createdField, out var records);
        Assert.True(fieldCreated);
        field = createdField!;

        var storeCreated = AlundraCellStore.TryCreate(records!, 2, 2, "synthetic", out var store);
        Assert.True(storeCreated);
        return store!;
    }

    private static EventTraceKind? CaptureKindForOpcode(AlundraEventProgramRunner runner, int opcode, System.Action run)
    {
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == opcode)
            {
                kind = record.Kind;
            }
        };

        run();
        runner.TraceSink = null;
        return kind;
    }

    [Fact]
    public void SetWalkable_0x54_Implemented_OrsBitsIntoClampedCell_ResultUntouched()
    {
        var store = NewSyntheticCellStore(out var field);
        var context = new FakeEntityWorldContext { CellMutator = store };
        var document = NewDocument(0x54, 0, 0, 0x10, 0x20, 0xFF); // x=0,y=0, walkMask=0x10, gpMask=0x20
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 42 };

        var kind = CaptureKindForOpcode(runner, 0x54, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(5, state.CodeIndex); // advanced by the instruction's own size (5), not suspended (0).
        Assert.Equal(42, state.Result); // untouched.
        Assert.Equal(0x11, field.SampleRawWalkability(new Vector3(0, 0, 0))); // 1 | 0x10.
        Assert.Equal(0x22, field.SampleGroundProperty(new Vector3(0, 0, 0))); // 2 | 0x20.
    }

    [Fact]
    public void SetUnwalkable_0x55_Implemented_AndsComplementIntoClampedCell_ResultUntouched()
    {
        var store = NewSyntheticCellStore(out var field);
        var context = new FakeEntityWorldContext { CellMutator = store };
        var document = NewDocument(0x55, 0, 0, 0x01, 0x02, 0xFF); // clears bit 0 of walkability (1) and gp (2)
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };

        var kind = CaptureKindForOpcode(runner, 0x55, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(5, state.CodeIndex);
        Assert.Equal(7, state.Result);
        Assert.Equal(0, field.SampleRawWalkability(new Vector3(0, 0, 0))); // 1 & ~1 = 0.
        Assert.Equal(0, field.SampleGroundProperty(new Vector3(0, 0, 0))); // 2 & ~2 = 0.
    }

    [Fact]
    public void SetCellBits_0x54_ClampsCoordinatesToHardcodedBounds()
    {
        // x=200 clamps to 0x33, y=200 clamps to 0x3b - but this 2x2 synthetic grid only has cells
        // (0,0)/(1,0)/(0,1)/(1,1), so the clamped index (0x33 + 0x3b*2 = 51 + 118 = 169) falls OUTSIDE
        // this tiny grid's own 4-cell array: exactly the "no clamping to map size" deviation the real
        // map-389 acceptance test below exercises safely (that grid IS 0x34 x 0x3c). Proven here instead
        // via a grid exactly the clamp's own size, so the clamped write lands in bounds and is directly
        // observable.
        var tileMapData = new TileMapData();
        var width = 0x34;
        var height = 0x3c;
        var cellCount = width * height;
        var walkability = string.Join(",", System.Linq.Enumerable.Repeat("0", cellCount));
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(width, height);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":" + cellCount + ","
            + "\"walkability\":[" + walkability + "],"
            + "\"ground_property\":[" + walkability + "],"
            + "\"slope\":[" + walkability + "],"
            + "\"height\":[" + walkability + "],"
            + "\"tile_id\":[" + walkability + "],"
            + "\"wall_tiles_offset\":[" + walkability + "],"
            + "\"wall_tiles\":{}}";

        AlundraCellsCollisionField.TryCreate(tileMapData, "clamp", out var field, out var records);
        AlundraCellStore.TryCreate(records!, width, height, "clamp", out var store);
        var context = new FakeEntityWorldContext { CellMutator = store };
        var document = NewDocument(0x54, 60, 70, 0x40, 0, 0xFF); // (60,70) -> clamps to (0x33,0x3b)
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0x40, field!.SampleRawWalkability(new Vector3(0x33 * 24, 0x3b * 16, 0f)));
    }

    [Fact]
    public void ChangeAreaTileProperties_0x85_Implemented_CopiesSixFieldsAndWallStack_ResultUntouched()
    {
        var store = NewSyntheticCellStore(out var field);
        var context = new FakeEntityWorldContext { CellMutator = store };
        // srcX=0,srcY=0,width=1,height=1,dstX=1,dstY=0 -> copies cell (0,0) onto cell (1,0).
        var document = NewDocument(0x85, 0, 0, 1, 1, 1, 0, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 99 };

        var kind = CaptureKindForOpcode(runner, 0x85, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(7, state.CodeIndex);
        Assert.Equal(99, state.Result);

        var destPos = new Vector3(1 * 24, 0, 0f);
        Assert.Equal(1, field.SampleRawWalkability(destPos));
        Assert.Equal(2, field.SampleGroundProperty(destPos));
        var destStack = store.GetWallTileStack(1, 0);
        Assert.NotNull(destStack);
        Assert.Equal(5, destStack!.Value.Offset);
        // dest (1,0) started with a SHORTER stack (1 tile) than source (0,0)'s (2 tiles) - exercises the
        // resize-to-source-length branch (GameEngine.cs:2296-2299), not just an in-place overwrite.
        Assert.Equal(new[] { 11, 22 }, destStack.Value.Tiles);
    }

    [Fact]
    public void ChangeAreaTileProperties_0x85_ShorterSourceStack_HidesTheDestinationsStaleTail()
    {
        var store = NewSyntheticCellStore(out _);
        var context = new FakeEntityWorldContext { CellMutator = store };
        // The mirror of the resize case above: source (1,0) has ONE tile, destination (0,0) has TWO, so
        // the original neither shrinks nor reallocates the destination array - it copies the 1-tile prefix
        // and sets Count = 1 (GameEngine.cs:2293-2297). Its renderer then stops at Count
        // (GraphicManager.cs:277), so the stale second entry (22) is never drawn. Reading Tiles.Length
        // instead of Count would surface it and make E7.b's overlay draw a wall tile the original does not.
        var document = NewDocument(0x85, 1, 0, 1, 1, 0, 0, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        var destStack = store.GetWallTileStack(0, 0);
        Assert.NotNull(destStack);
        Assert.Equal(50, destStack!.Value.Offset);
        Assert.Equal(new[] { 99 }, destStack.Value.Tiles);
    }

    [Fact]
    public void ChangeAreaTileProperties_0x85_SourceWithNoStack_DestroysDestinationStack()
    {
        var store = NewSyntheticCellStore(out _);
        var context = new FakeEntityWorldContext { CellMutator = store };
        // srcX=0,srcY=1 (cell (0,1), no wall stack), width=1,height=1, dstX=0,dstY=0 (cell (0,0), HAS one).
        var document = NewDocument(0x85, 0, 1, 1, 1, 0, 0, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Null(store.GetWallTileStack(0, 0));
    }

    [Fact]
    public void Opcode54_NullCellMutator_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0x54, 1, 2, 3, 4, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext -> CellMutator null.
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 5 };

        var kind = CaptureKindForOpcode(runner, 0x54, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(5, state.CodeIndex); // still skipped by its own real size, not 0/suspended.
        Assert.Equal(5, state.Result); // untouched.
    }

    [Fact]
    public void Opcode55_NullCellMutator_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0x55, 1, 2, 3, 4, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x55, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(5, state.CodeIndex);
    }

    [Fact]
    public void Opcode85_NullCellMutator_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0x85, 0, 0, 1, 1, 1, 1, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x85, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(7, state.CodeIndex);
    }

    // -----------------------------------------------------------------------------------------
    // 0x3B (Check player in area) / 0x2F (Check pad buttons) - E7.c, docs/plan-e7-mutation-tuiles.md.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CheckPlayerInArea_0x3B_PlayerInsideBox_ResultOne_AdvancesBySeven()
    {
        // Box xmin,xmax,ymin,ymax,zmin,zmax = [18,18,38,38,8,8] - the real B130 hatch box.
        var document = NewDocument(0x3B, 18, 18, 38, 38, 8, 8, 0xFF);
        var player = NewEntity();
        player.TileX = 18;
        player.TileY = 38;
        player.TileZ = 8;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);
        var owner = NewEntity(); // executing entity - deliberately NOT the player, see the next test.

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1, state.Result);
        Assert.Equal(7, state.CodeIndex); // stopped at 0xFF.
    }

    [Fact]
    public void CheckPlayerInArea_0x3B_ReadsThePlayerEntity_NotTheExecutingEntity()
    {
        var document = NewDocument(0x3B, 18, 18, 38, 38, 8, 8, 0xFF);
        var player = NewEntity();
        player.TileX = 18;
        player.TileY = 38;
        player.TileZ = 8;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);
        // The EXECUTING entity sits outside the box - would fail if 0x3B read it instead of the player.
        var owner = NewEntity();
        owner.TileX = 0;
        owner.TileY = 0;
        owner.TileZ = 0;

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1, state.Result);
    }

    [Theory]
    [InlineData(17, 38, 8, 0)] // X below xmin.
    [InlineData(19, 38, 8, 0)] // X above xmax.
    [InlineData(18, 37, 8, 0)] // Y below ymin.
    [InlineData(18, 39, 8, 0)] // Y above ymax.
    [InlineData(18, 38, 7, 0)] // Z below zmin.
    [InlineData(18, 38, 9, 0)] // Z above zmax.
    [InlineData(18, 38, 8, 1)] // exactly on every bound (all mins == maxes here) - inside.
    public void CheckPlayerInArea_0x3B_BoundsAreInclusiveOnEveryFace(int tileX, int tileY, int tileZ, int expectedResult)
    {
        var document = NewDocument(0x3B, 18, 18, 38, 38, 8, 8, 0xFF);
        var player = NewEntity();
        player.TileX = tileX;
        player.TileY = tileY;
        player.TileZ = tileZ;
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = NewRunner(document, worldContext: context);

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(NewEntity(), state);

        Assert.Equal(expectedResult, state.Result);
    }

    [Fact]
    public void CheckPlayerInArea_0x3B_NoPlayerSpawned_ResultZero_DegradedKind_StillAdvances()
    {
        var document = NewDocument(0x3B, 18, 18, 38, 38, 8, 8, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext.PlayerEntity == null
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 9 };

        var kind = CaptureKindForOpcode(runner, 0x3B, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(0, state.Result);
        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(7, state.CodeIndex); // advanced by its own real size (7), not suspended.
    }

    [Fact]
    public void CheckPadButtons_0x2F_ButtonHeld_ResultOne()
    {
        // Real map-389 params [0,16,0]: flag = (v2<<8)|v1 = (16<<8)|0 = 0x1000 = Up, snapshot 0 (Hold).
        var document = NewDocument(0x2F, 0, 16, 0, 0xFF);
        var gameState = new AlundraGameState { LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Up } };
        var runner = NewRunner(document, gameState: gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1, state.Result);
        Assert.Equal(4, state.CodeIndex); // stopped at 0xFF.
    }

    [Fact]
    public void CheckPadButtons_0x2F_ButtonNotHeld_ResultZero()
    {
        var document = NewDocument(0x2F, 0, 16, 0, 0xFF);
        var gameState = new AlundraGameState { LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Down } };
        var runner = NewRunner(document, gameState: gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void CheckPadButtons_0x2F_Mode1_ReadsButtonsJustPressed_NotButtonsHold()
    {
        var document = NewDocument(0x2F, 0, 16, 1, 0xFF); // v3=1 -> ButtonsJustPressed
        var gameState = new AlundraGameState
        {
            LastPadState = new AlundraPadState { ButtonsHold = 0, ButtonsJustPressed = AlundraPadState.Up },
        };
        var runner = NewRunner(document, gameState: gameState);
        var entity = NewEntity();

        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1, state.Result);
    }

    [Theory]
    [InlineData(2)] // ButtonsReleased - unported.
    [InlineData(3)] // ButtonsJustPressedByInterval (the original's own default arm) - unported.
    public void CheckPadButtons_0x2F_UnportedSnapshotModes_ResultZero_DegradedKind(int mode)
    {
        var document = NewDocument(0x2F, 0, 16, mode, 0xFF);
        // Both real fields set so a bug reading the wrong one would still (wrongly) pass - only reading
        // NEITHER (the degraded path) proves this.
        var gameState = new AlundraGameState
        {
            LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Up, ButtonsJustPressed = AlundraPadState.Up },
        };
        var runner = NewRunner(document, gameState: gameState);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x2F, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(0, state.Result);
        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(4, state.CodeIndex);
    }

    [Fact]
    public void CheckPadButtons_0x2F_MaskComposedFromTwoOperandBytes()
    {
        // A pad state with ONLY bit 0 held (0x0001). v1 carries the LOW byte, v2 the HIGH byte
        // (flag = v[2]<<8|v[1]) - two disjoint single-bit masks prove each operand lands in its own byte
        // position, not that they are simply OR'd/ignored.
        var gameState = new AlundraGameState { LastPadState = new AlundraPadState { ButtonsHold = 0x0001 } };

        // v1=0x01 (low byte) -> flag = 0x0001 - matches the held bit.
        var document = NewDocument(0x2F, 0x01, 0x00, 0, 0xFF);
        var runner = NewRunner(document, gameState: gameState);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(NewEntity(), state);
        Assert.Equal(1, state.Result);

        // v2=0x01 (HIGH byte) instead -> flag = 0x0100, a DIFFERENT bit - must miss the 0x0001 pad state,
        // proving v[1] is not simply dropped in favor of v[2] (or the two bytes swapped).
        var document2 = NewDocument(0x2F, 0x00, 0x01, 0, 0xFF);
        var runner2 = NewRunner(document2, gameState: gameState);
        var state2 = new EventProgramState { Codes = document2.CodesAsBytes() };
        runner2.RunOneScriptCall(NewEntity(), state2);
        Assert.Equal(0, state2.Result);
    }

    // -----------------------------------------------------------------------------------------
    // D-E7-8 pad seam, production site: AlundraEntityScriptProxy.Update's player branch publishes
    // AlundraGameState.LastPadState just before MovePlayer - see that write site's own doc.
    // -----------------------------------------------------------------------------------------

    private sealed class PadSeamScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunnerForPadSeamTest();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = System.Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    private sealed class NoOpRunnerForPadSeamTest : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    [Fact]
    public void PadSeam_ProductionSite_PlayerUpdate_PublishesLastPadStateBeforeMovePlayer_And0x2FReadsIt()
    {
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = AlundraPadState.Up } };
        var host = new PadSeamScriptHost { PlayerController = controller };

        var player = new AlundraEntityScriptProxy { IsPlayer = true, ScriptHost = host };
        var owner = new CasaEngine.Framework.Scene.Entities.Entity();
        player.Initialize(owner); // gives Owner a value so Update's SyncAnimation/SyncTransform are no-ops.

        player.Update(1f / 50f); // real production frame - the player branch runs MovePlayer for real.

        // A real 0x2F dispatch now reads what THIS Update call published, not a hand-set gameState.
        var document = NewDocument(0x2F, 0, 16, 0, 0xFF); // mask 0x1000 = Up, snapshot 0 (Hold).
        var runner = NewRunner(document, gameState: host.GameState);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(NewEntity(), state);

        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void PadSeam_Neutralization_NoPlayerUpdateRan_LastPadStateStaysZero_0x2FReadsZero()
    {
        // Same gameState, but never touched by a player Update - the untouched default.
        var gameState = new AlundraGameState();

        var document = NewDocument(0x2F, 0, 16, 0, 0xFF);
        var runner = NewRunner(document, gameState: gameState);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(NewEntity(), state);

        Assert.Equal(0, state.Result);
    }

    // -----------------------------------------------------------------------------------------
    // 0x3B, production call site (item 2 bis - the item that decides the tranche, docs/plan-e7-mutation-
    // tuiles.md): drives the REAL AlundraWorldProxy.RunMapEventsPass over the REAL map-389 door program
    // (masked index 2, program id 130, offset 400 in docs/intro-programs-389.txt), with the harness
    // player placed exactly inside program B130's own real box [18,18,38,38,8,8]. A stubbed-0x3B ("always
    // false") or a null-PlayerEntity implementation would both loop the program back onto its own Break
    // (codeIndex 412) every call, forever - so codeIndex alone at the end of one call cannot distinguish
    // "0x3B took the in-zone branch" from "0x3B is still false" (0x70's own false branch loops back to
    // that SAME pc). What DOES distinguish them: 0x70 (pc 423, right after 0x3B's own "if false goto") is
    // only ever dispatched at all when 0x3B's branch was NOT taken - captured via TraceSink.
    // -----------------------------------------------------------------------------------------

    private static string FindProjectRootForRealMap389()
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "alundra-project");
            if (System.IO.Directory.Exists(System.IO.Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new System.InvalidOperationException(
            $"AlundraEventProgramRunnerTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - the 0x3B production-site test needs the real converter "
            + "export of map 389 and cannot self-skip without one (docs/plan-e7-mutation-tuiles.md, "
            + "slice E7.c, acceptance item 2 bis).");
    }

    private const string Map389WorldName = "Ship Klark (beginning)-389";
    private const int HatchDoorProgramBMap = 130; // masked index 2 (130 & 0x7f), offset 400.

    /// <summary>Snapshot of one dispatched opcode, taken AT DISPATCH TIME - unlike the raw
    /// <see cref="EventTraceRecord"/> (whose own <c>State</c> is a LIVE reference, see that record's own
    /// doc), <see cref="ResultAtDispatch"/> here is a frozen copy, so a later opcode overwriting
    /// <c>Result</c> (0x70 does, right after 0x3B in this program) cannot retroactively corrupt what THIS
    /// entry observed.</summary>
    private readonly record struct DispatchSnapshot(int Opcode, int CodeIndex, EventTraceKind Kind, int ResultAtDispatch);

    private static List<DispatchSnapshot> RunHatchDoorProgramTwice(AlundraEventProgramRunner runner, AlundraEntityScriptProxy player)
    {
        var records = new List<DispatchSnapshot>();
        runner.TraceSink = r => records.Add(new DispatchSnapshot(r.Opcode, r.CodeIndex, r.Kind, r.State.Result));

        var mapEvent = new AlundraMapEvent { Id = 2, X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, ProgramBMap = HatchDoorProgramBMap, Entity = player };
        var mapEvents = new[] { mapEvent };

        // Frame 1: fresh state -> InitializeEventData seeds CodeIndex at table[2]=400. Dispatches
        // 0x55/0x85 (map-entry hatch-close template) then hits the program's own Break at 412, suspends.
        AlundraWorldProxy.RunMapEventsPass(player, mapEvents, runner, playerControlFlags: 0);
        records.Clear(); // only the SECOND call (0x3B onward) matters for this test.

        // Frame 2: resumes at pc 413 (0x3B).
        AlundraWorldProxy.RunMapEventsPass(player, mapEvents, runner, playerControlFlags: 0);

        runner.TraceSink = null;
        return records;
    }

    [Fact]
    public void CheckPlayerInArea_0x3B_RealHatchDoorProgram_PlayerInZone_TakesInZoneBranch_Not0x3BAlone()
    {
        var projectRoot = FindProjectRootForRealMap389();
        var document = MapEventProgramLoader.Load(projectRoot, Map389WorldName);
        Assert.NotNull(document);

        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 18, TileY = 38, TileZ = 8 };
        var context = new FakeEntityWorldContext { PlayerEntity = player };
        var runner = new AlundraEventProgramRunner(document!, new AlundraGameState(), context);

        var records = RunHatchDoorProgramTwice(runner, player);

        // 0x3B itself dispatched, Implemented, and Result true AT THE MOMENT IT DISPATCHED (0x70 right
        // after it overwrites the SAME live Result to 0 - see DispatchSnapshot's own doc - so this must
        // read the frozen snapshot, not the record's own live State.Result post-hoc).
        var check = Assert.Single(records, r => r.Opcode == 0x3B);
        Assert.Equal(EventTraceKind.Implemented, check.Kind);
        Assert.Equal(1, check.ResultAtDispatch);

        // The decisive signal: 0x70 (pc 423) is dispatched in this SAME call, meaning the "if false
        // goto" right after 0x3B did NOT jump back to the Break loop - it can only reach here if 0x3B's
        // own branch was true.
        Assert.Contains(records, r => r.Opcode == 0x70);
    }

    [Fact]
    public void CheckPlayerInArea_0x3B_RealHatchDoorProgram_Neutralization_NullPlayerEntity_StaysInBreakLoop()
    {
        var projectRoot = FindProjectRootForRealMap389();
        var document = MapEventProgramLoader.Load(projectRoot, Map389WorldName);
        Assert.NotNull(document);

        // Same player TILE, same box - but the world context's own PlayerEntity is null, so the
        // production runner cannot see it (proves IEntityWorldContext.PlayerEntity and RunMapEventsPass's
        // own "player" parameter are two distinct seams - see item 2 bis's own doc, docs/plan-e7-
        // mutation-tuiles.md).
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 18, TileY = 38, TileZ = 8 };
        var context = new FakeEntityWorldContext { PlayerEntity = null };
        var runner = new AlundraEventProgramRunner(document!, new AlundraGameState(), context);

        var records = RunHatchDoorProgramTwice(runner, player);

        var check = Assert.Single(records, r => r.Opcode == 0x3B);
        Assert.Equal(EventTraceKind.Degraded, check.Kind);
        Assert.Equal(0, check.ResultAtDispatch);

        // Never reaches 0x70 - the false branch looped straight back onto the Break.
        Assert.DoesNotContain(records, r => r.Opcode == 0x70);
    }

    // -----------------------------------------------------------------------------------------
    // Sound opcodes (0xBD/0xBE/0x12/0x75/0xA8/0xBA) - E11.a, docs/plan-e11-audio.md.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PlaySound2_0xBD_Implemented_DerivesIdFromTwoBytes_ResultUntouched()
    {
        var soundPlayer = new FakeSoundPlayer();
        var context = new FakeEntityWorldContext { SoundPlayer = soundPlayer };
        // sfxId = (v[2] << 8) | v[1] = (0x01 << 8) | 0x2C = 0x12C = 300.
        var document = NewDocument(0xBD, 0x2C, 0x01, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 42 };

        var kind = CaptureKindForOpcode(runner, 0xBD, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(3, state.CodeIndex);
        Assert.Equal(42, state.Result); // untouched - a side-effect-only opcode.
        Assert.Equal(new[] { 300 }, soundPlayer.Requests);
    }

    [Fact]
    public void PlaySound2_0xBD_NullSoundPlayer_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0xBD, 0x2C, 0x01, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext -> SoundPlayer null.
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 5 };

        var kind = CaptureKindForOpcode(runner, 0xBD, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, state.CodeIndex); // still skipped by its own real size.
        Assert.Equal(5, state.Result);
    }

    [Fact]
    public void PlaySound2Bis_0xBE_Implemented_SameTwoByteDerivationAs0xBD()
    {
        var soundPlayer = new FakeSoundPlayer();
        var context = new FakeEntityWorldContext { SoundPlayer = soundPlayer };
        // sfxId = (v[2] << 8) | v[1] = (0x01 << 8) | 0x2D = 0x12D = 301.
        var document = NewDocument(0xBE, 0x2D, 0x01, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0xBE, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(3, state.CodeIndex);
        Assert.Equal(new[] { 301 }, soundPlayer.Requests);
    }

    // T6 (docs/plan-e11-audio.md, blocker P2 of the second relecture): 0x12/0x75 derive sfxId from
    // v[1] ALONE, on a 2-byte instruction - NOT 0xBD/0xBE's two-byte (v[2] << 8) | v[1]. The byte
    // FOLLOWING the operand (v[2] in the underlying byte stream, which this 2-byte instruction never
    // reads as part of ITS OWN operand, but FillDataFromCommand still peeks at) is pinned NON-ZERO and
    // DIFFERENT from the operand - operand 0x2A (42), following byte 0xFF (255, which conveniently also
    // doubles as the terminator once the correct 2-byte advance lands on it) - so a mutant that wrongly
    // applies 0xBD's derivation produces (0xFF << 8) | 0x2A = 0xFF2A = 65322, a clearly different id than
    // the correct 0x2A = 42, and the id assertion (not just the advance) catches it.

    [Fact]
    public void PlaySound1_0x12_Implemented_DerivesIdFromSingleByte_NotTwo()
    {
        var soundPlayer = new FakeSoundPlayer();
        var context = new FakeEntityWorldContext { SoundPlayer = soundPlayer };
        var document = NewDocument(0x12, 0x2A, 0xFF); // operand=0x2A(42); v[2]=0xFF - non-zero, != operand.
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };

        var kind = CaptureKindForOpcode(runner, 0x12, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(2, state.CodeIndex); // 2-byte instruction - stops right after its own operand.
        Assert.Equal(7, state.Result);
        Assert.Equal(new[] { 42 }, soundPlayer.Requests); // NOT 0xFF2A (65322) - the wrong two-byte derivation.
    }

    [Fact]
    public void PlaySound1_0x12_NullSoundPlayer_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0x12, 0x2A, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x12, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(2, state.CodeIndex);
    }

    [Fact]
    public void PlaySoundEffect_0x75_Implemented_DerivesIdFromSingleByte_NotTwo()
    {
        var soundPlayer = new FakeSoundPlayer();
        var context = new FakeEntityWorldContext { SoundPlayer = soundPlayer };
        var document = NewDocument(0x75, 0x2A, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x75, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(2, state.CodeIndex);
        Assert.Equal(new[] { 42 }, soundPlayer.Requests);
    }

    [Fact]
    public void IsSoundLoading_0xA8_AlwaysWritesResultZero_OverwritingAStalePredicateValue()
    {
        // The value must first be shown stale (D-E11-5): pose Result=1 by a PREVIOUS predicate, then
        // dispatch 0xA8 - unlike the old UnknownOpcode fallback (which never touched Result at all), this
        // opcode is a real implemented predicate and must overwrite it.
        var document = NewDocument(0xA8, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };

        var kind = CaptureKindForOpcode(runner, 0xA8, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0, state.Result); // overwrites the stale 1 - never streaming from a CD.
    }

    [Fact]
    public void CheckLoadingFromCd_0xBA_AlwaysWritesResultZero_OverwritingAStalePredicateValue()
    {
        var document = NewDocument(0xBA, 0xFF);
        var runner = NewRunner(document);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };

        var kind = CaptureKindForOpcode(runner, 0xBA, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0, state.Result);
    }

    // -----------------------------------------------------------------------------------------
    // Screen fade opcodes (0xAF/0xB0/0xB1) - E10.b, docs/plan-e10-fondu.md.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BeginFadeTransition_0xAF_Implemented_ExtractsAllSixOperandsInDisplayOrder()
    {
        var fadeDirector = new FakeScreenFadeDirector();
        var context = new FakeEntityWorldContext { ScreenFadeDirector = fadeDirector };
        // [op, r, g, b, tpage, duration, persist] - r=10, g=20, b=30, tpage=1, duration=8, persist=9.
        var document = NewDocument(0xAF, 10, 20, 30, 1, 8, 9, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        var kind = CaptureKindForOpcode(runner, 0xAF, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(7, state.CodeIndex);
        Assert.Single(fadeDirector.BeginFadeCalls);
        // The mutation this pins (T3, "follow the decomp's own inverted names"): r=10/b=30 must NOT swap.
        Assert.Equal((10, 20, 30, 1, 8, 9), fadeDirector.BeginFadeCalls[0]);
    }

    [Fact]
    public void BeginFadeTransition_0xAF_NullScreenFadeDirector_DegradedNoOp_SkipsBySize()
    {
        var document = NewDocument(0xAF, 10, 20, 30, 1, 8, 9, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext -> null director.
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 5 };

        var kind = CaptureKindForOpcode(runner, 0xAF, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(7, state.CodeIndex);
        Assert.Equal(5, state.Result); // untouched.
    }

    [Fact]
    public void SetWarpFadeDuration_0xB0_Implemented_ExtractsAllFourOperands()
    {
        var fadeDirector = new FakeScreenFadeDirector();
        var context = new FakeEntityWorldContext { ScreenFadeDirector = fadeDirector };
        // [op, r, g, b, duration] - r=1, g=2, b=3, duration=6.
        var document = NewDocument(0xB0, 1, 2, 3, 6, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        var kind = CaptureKindForOpcode(runner, 0xB0, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(5, state.CodeIndex);
        Assert.Single(fadeDirector.SetWarpFadeDurationCalls);
        Assert.Equal((1, 2, 3, 6), fadeDirector.SetWarpFadeDurationCalls[0]);
    }

    [Fact]
    public void CheckFadeAndWarpFlags_0xB1_WritesResultBothWays_OverwritingAStaleValue()
    {
        var fadeDirector = new FakeScreenFadeDirector { IsSettled = false };
        var context = new FakeEntityWorldContext { ScreenFadeDirector = fadeDirector };
        var document = NewDocument(0xB1, 0xFF);
        var runner = NewRunner(document, worldContext: context);
        var entity = NewEntity();

        // Stale Result = 1, mid-fade (IsSettled = false) - must be overwritten to 0 (T6's own "stale"
        // half - unlike UnknownOpcode's own no-touch fallback).
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };
        var kind = CaptureKindForOpcode(runner, 0xB1, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0, state.Result);

        // After arrival (IsSettled = true) - Result flips to 1.
        fadeDirector.IsSettled = true;
        state.CodeIndex = 0;
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void CheckFadeAndWarpFlags_0xB1_NullScreenFadeDirector_WritesResultZero()
    {
        var document = NewDocument(0xB1, 0xFF);
        var runner = NewRunner(document); // no worldContext -> NoOpEntityWorldContext -> null director.
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(0, state.Result);
    }
}
