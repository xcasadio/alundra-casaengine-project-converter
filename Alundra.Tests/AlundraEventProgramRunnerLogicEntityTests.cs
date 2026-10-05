#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.a T1 to T4 (docs/plan-e19-opcodes.md §0.2.3, §1.2): the logic entity of the interpreter. Every handler is
/// called as <c>h(logic = *(owner+0x230), owner, ...)</c> with the word re-read before each instruction
/// (<c>0x80042284</c>); <c>0x42</c> and <c>0x43</c> are the only writers of the owner's word. The program state
/// stays the owner's. Documents are synthetic, built with the tables of the slot under test.
/// </summary>
public class AlundraEventProgramRunnerLogicEntityTests
{
    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private static AlundraEventProgramRunner NewRunner(EventProgramDocument document, IEntityWorldContext? world = null)
        => new(document, new AlundraGameState(), world);

    /// <summary>A world with a settable list of spawned entities and a player - just what the searches read.</summary>
    private sealed class FakeWorld : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;

        public AlundraEntityScriptProxy? PlayerEntity { get; set; }

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

        public NavigationGrid2D? NavigationGrid => null;
    }

    private static AlundraEntityScriptProxy Entity(int record, EntityStatus status = EntityStatus.Normal)
        => new() { EntityRefId = record, Status = status };

    private static EventProgramState StateFor(EventProgramDocument document) => new() { Codes = document.CodesAsBytes() };

    [Fact]
    public void WithoutALogicEntity_TheOwnerIsTheLogicEntity()
    {
        var document = NewDocument(0x1A, 7, 0xFF);
        var runner = NewRunner(document);
        var owner = Entity(1);

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Null(owner.LogicEntity);
        Assert.Equal(7u, owner.TargetAnimationId);
    }

    [Fact]
    public void SetAnim_0x1A_AndSetDirection_0x09_ActOnTheLogicEntity_NotTheOwner()
    {
        var document = NewDocument(0x1A, 7, 0x09, 5, 0xFF);
        var runner = NewRunner(document);
        var owner = Entity(1);
        var logic = Entity(2);
        owner.LogicEntity = logic;

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Equal(7u, logic.TargetAnimationId);
        Assert.Equal(5u, logic.TargetDirection);
        Assert.Equal(0u, owner.TargetAnimationId);
        Assert.Equal(0u, owner.TargetDirection);
    }

    [Fact]
    public void Walk_0x1E_MeasuresTheDistanceOfTheLogicEntity_ButKeepsItsMemoOnTheOwnersState()
    {
        // 0x1E [80, 0]: waits until the walker has moved 80 px, then falls through to 0x1A [9].
        var document = NewDocument(0x1E, 80, 0, 0x1A, 9, 0xFF);
        var runner = NewRunner(document);
        var owner = Entity(1);
        var logic = Entity(2);
        owner.LogicEntity = logic;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state); // first call: memorises the start position, suspends.
        Assert.Equal(0, state.CodeIndex);
        Assert.Equal(logic.PosX, state.Parameters[2]);

        owner.PosX += 200 << 16; // the OWNER moving does not end the walk.
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(0, state.CodeIndex);

        logic.PosX += 80 << 16; // the logic entity did.
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(9u, logic.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);
    }

    [Fact]
    public void IdSearch_TakesTheLogicEntityAsItsReference()
    {
        // 0x2C [3]: Result = 1 iff NO entity matches search 3 (an "Entities" record id). A search by record id only
        // tests the state of its REFERENCE (Loaded, Normal or Deactivated), never the candidate's.
        var document = NewDocument(0x2C, 3, 0xFF);
        var world = new FakeWorld();
        var candidate = Entity(3);
        world.Spawned.Add(candidate);
        var runner = NewRunner(document, world);
        var owner = Entity(1);
        var logic = Entity(2);
        owner.LogicEntity = logic;

        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(0, state.Result); // the reference (logic) is Normal: the candidate is found.

        logic.Status = EntityStatus.FlagToDestroy; // a destroyed logic entity finds nothing, whatever the owner.
        state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(1, state.Result);
        Assert.Equal(EntityStatus.Normal, owner.Status);
    }

    [Fact]
    public void TheProgramState_StaysTheOwners_WhateverTheLogicEntity()
    {
        // Slot B resumes off the OWNER's own state: 0x37 Wait(1) suspends, the second call resumes it.
        var document = new EventProgramDocument
        {
            EventCodesBTable = new[] { 0 },
            Codes = new[] { 0x37, 1, 0x1A, 9, 0xFF },
        };
        var runner = NewRunner(document);
        var owner = Entity(1);
        var logic = Entity(2);
        owner.LogicEntity = logic;
        owner.ProgramIndexes[ScriptHelper.ProgramBMap] = 0x80;

        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        Assert.NotNull(owner.EventProgramState.Codes);
        Assert.Null(logic.EventProgramState.Codes);
        Assert.Equal(0u, logic.TargetAnimationId);

        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        Assert.Equal(9u, logic.TargetAnimationId);
        Assert.Null(logic.EventProgramState.Codes);
    }

    [Fact]
    public void AMapEvent_KeepsItsOwnLogicEntity_FromOneFrameToTheNext()
    {
        var document = new EventProgramDocument
        {
            EventCodesBTable = new[] { 99, 0 },
            Codes = new[] { 0x1A, 7, 0x00, 0x1A, 8, 0xFF },
        };
        var runner = NewRunner(document);
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5 };
        var npc = Entity(2);
        var mapEvent = new AlundraMapEvent { Id = 0, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10, ProgramBMap = 129, OriginalProgramBMap = 129, Entity = npc };

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);

        // The first frame ran up to the Break on the event's own entity, not on the hero.
        Assert.Equal(7u, npc.TargetAnimationId);
        Assert.Equal(0u, hero.TargetAnimationId);
        Assert.Same(npc, mapEvent.Entity);
        Assert.Same(npc, hero.LogicEntity); // the hero's own word keeps the last event's value, as hero+0x230 does.

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);

        Assert.Equal(8u, npc.TargetAnimationId);
        Assert.Equal(0u, hero.TargetAnimationId);
        Assert.Same(npc, mapEvent.Entity);
    }

    // ---------------------------------------------------------------------------------------------
    // T2: 0x42 and 0x43
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SetLogicEntityByMatch_0x43_Found_TakesTheLastMatch_SetsResult1_AndAdvancesBy2()
    {
        // 0x43 [5]; 0x1A [7]; end. Two entities carry record id 5: the LAST one found becomes the context.
        var document = NewDocument(0x43, 5, 0x1A, 7, 0xFF);
        var world = new FakeWorld();
        var first = Entity(5);
        var last = Entity(5);
        world.Spawned.Add(first);
        world.Spawned.Add(last);
        var runner = NewRunner(document, world);
        var owner = Entity(1);
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Same(last, owner.LogicEntity);
        Assert.Equal(1, state.Result);
        Assert.Equal(7u, last.TargetAnimationId); // the next instruction (at +2) ran on the new logic entity.
        Assert.Equal(0u, first.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);
    }

    [Fact]
    public void SetLogicEntityByMatch_0x43_NotFound_SetsResult0_KeepsTheContext_AndAdvancesBy2()
    {
        var document = NewDocument(0x43, 9, 0x1A, 7, 0xFF);
        var world = new FakeWorld();
        world.Spawned.Add(Entity(5));
        var runner = NewRunner(document, world);
        var owner = Entity(1);
        var kept = Entity(2);
        owner.LogicEntity = kept;
        var state = StateFor(document);
        state.Result = 1;

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(0, state.Result);
        Assert.Same(kept, owner.LogicEntity);
        Assert.Equal(7u, kept.TargetAnimationId);
    }

    [Fact]
    public void SetLogicEntityByMatch_0x43_GetOwner_0x80_LeavesTheContextAlone_ButSetsResult1()
    {
        // The search 0x80 returns its reference: the logic entity itself.
        var document = NewDocument(0x43, 0x80, 0xFF);
        var runner = NewRunner(document, new FakeWorld());
        var owner = Entity(1);
        var kept = Entity(2);
        owner.LogicEntity = kept;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(1, state.Result);
        Assert.Same(kept, owner.LogicEntity);

        // With the context still on the owner, the owner remains its own logic entity.
        var self = Entity(3);
        state = StateFor(document);
        runner.RunOneScriptCall(self, state);
        Assert.Equal(1, state.Result);
        Assert.Same(self, self.LogicEntity ?? self);
    }

    [Fact]
    public void SetLogicEntityToPlayer_0x42_SetsTheHero_NotTheOwner_AndAdvancesBy1()
    {
        // The owner retargeted itself on an NPC, then 0x42 gives the context back to the hero.
        var document = NewDocument(0x42, 0x1A, 7, 0xFF);
        var world = new FakeWorld();
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, Status = EntityStatus.Normal };
        world.PlayerEntity = hero;
        var runner = NewRunner(document, world);
        var owner = Entity(1);
        owner.LogicEntity = Entity(2);
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Same(hero, owner.LogicEntity);
        Assert.Equal(7u, hero.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);
    }

    [Fact]
    public void NoObstacleSlide_0x46And0x45_AfterA0x42_WriteTheFlagsOfTheHero_NotTheOwners_ResultUntouched()
    {
        // E19.m6 M6-3: UO-1 runs 0x45/0x46 on an entity that is its own logic entity, so the handlers' operand (the logic entity) is never told apart from the owner.
        // Here the owner retargets the hero with 0x42: the bit 0x2000 lands on the hero's flags, the owner's are not written.
        var world = new FakeWorld();
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, Status = EntityStatus.Normal, Flags = 0x100 };
        world.PlayerEntity = hero;
        var owner = Entity(1);
        var ownerFlags = owner.Flags;

        var set = NewDocument(0x42, 0x46, 0xFF);
        var setState = StateFor(set);
        setState.Result = 7;
        NewRunner(set, world).RunOneScriptCall(owner, setState);

        Assert.Same(hero, owner.LogicEntity);
        Assert.Equal(0x2100u, hero.Flags);
        Assert.Equal(ownerFlags, owner.Flags);
        Assert.Equal(2, setState.CodeIndex);
        Assert.Equal(7, setState.Result);

        var clear = NewDocument(0x45, 0xFF);
        var clearState = StateFor(clear);
        clearState.Result = 7;
        NewRunner(clear, world).RunOneScriptCall(owner, clearState); // the owner's word still points at the hero.

        Assert.Equal(0x100u, hero.Flags);
        Assert.Equal(ownerFlags, owner.Flags);
        Assert.Equal(1, clearState.CodeIndex);
        Assert.Equal(7, clearState.Result);
    }

    [Fact]
    public void SetLogicEntityToPlayer_0x42_WithoutAHero_LeavesTheContext_IsDegraded_AndAdvancesBy1()
    {
        var document = NewDocument(0x42, 0x1A, 7, 0xFF);
        var runner = NewRunner(document, new FakeWorld());
        var kinds = new List<EventTraceKind>();
        runner.TraceSink = record => kinds.Add(record.Kind);
        var owner = Entity(1);
        var kept = Entity(2);
        owner.LogicEntity = kept;

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Same(kept, owner.LogicEntity);
        Assert.Equal(7u, kept.TargetAnimationId);
        Assert.Equal(EventTraceKind.Degraded, kinds[0]);
    }

    [Fact]
    public void TheContext_PersistsBetweenCalls_AfterABreak_AndFromOneSlotToTheNextOfTheSameEntity()
    {
        // Slot C: 0x43 [5]; Break; 0x1A [7]; end. Slot F: 0x1A [8]; end.
        var document = new EventProgramDocument
        {
            EventCodesCTable = new[] { 0 },
            EventCodesFTable = new[] { 6 },
            Codes = new[] { 0x43, 5, 0x00, 0x1A, 7, 0xFF, 0x1A, 8, 0xFF },
        };
        var world = new FakeWorld();
        var target = Entity(5);
        world.Spawned.Add(target);
        var runner = NewRunner(document, world);
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;
        owner.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x80;

        runner.RunScript(owner, ScriptHelper.ProgramCTick); // 0x43, then the Break.
        Assert.Same(target, owner.LogicEntity);
        Assert.Equal(0u, target.TargetAnimationId);

        runner.RunScript(owner, ScriptHelper.ProgramCTick); // resumed: 0x1A [7] acts on the context kept across the Break.
        Assert.Equal(7u, target.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);

        runner.RunScript(owner, ScriptHelper.ProgramFInteract); // another slot of the same entity: the same context.
        Assert.Equal(8u, target.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);
        Assert.Same(target, owner.LogicEntity);
    }

    [Fact]
    public void AMapEvent_ThatRetargetsItsContext_KeepsItForItsNextFrame()
    {
        // 0x43 [5]; Break; 0x1A [7]; end - run as a map event of the hero.
        var document = new EventProgramDocument
        {
            EventCodesBTable = new[] { 99, 0 },
            Codes = new[] { 0x43, 5, 0x00, 0x1A, 7, 0xFF },
        };
        var world = new FakeWorld();
        var npc = Entity(5);
        world.Spawned.Add(npc);
        var runner = NewRunner(document, world);
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5, Status = EntityStatus.Normal };
        world.PlayerEntity = hero;
        var mapEvent = new AlundraMapEvent { Id = 0, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10, ProgramBMap = 129, OriginalProgramBMap = 129, Entity = hero };

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);
        Assert.Same(npc, mapEvent.Entity);

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);
        Assert.Equal(7u, npc.TargetAnimationId);
        Assert.Equal(0u, hero.TargetAnimationId);
    }

    // ---------------------------------------------------------------------------------------------
    // T3: 0x59
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SetEntityAnim_0x59_OnTheHero_0x81_SetsItsTargetAnimation_AndAdvancesBy3()
    {
        // 0x59 [0x81, 3]; 0x1A [7]; end: the hero "at rest" idiom, then the next instruction at +3.
        var document = NewDocument(0x59, 0x81, 3, 0x1A, 7, 0xFF);
        var world = new FakeWorld();
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, Status = EntityStatus.Normal };
        world.PlayerEntity = hero;
        var runner = NewRunner(document, world);
        var owner = Entity(1);

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Equal(3u, hero.TargetAnimationId);
        Assert.Equal(7u, owner.TargetAnimationId);
    }

    [Fact]
    public void SetEntityAnim_0x59_ById_SetsEveryMatch_AndTakesTheLogicEntityAsReference()
    {
        var document = NewDocument(0x59, 5, 4, 0xFF);
        var world = new FakeWorld();
        var a = Entity(5);
        var b = Entity(5);
        var other = Entity(6);
        world.Spawned.Add(a);
        world.Spawned.Add(b);
        world.Spawned.Add(other);
        var runner = NewRunner(document, world);
        var owner = Entity(1);

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Equal(4u, a.TargetAnimationId);
        Assert.Equal(4u, b.TargetAnimationId);
        Assert.Equal(0u, other.TargetAnimationId);

        // A destroyed logic entity is the reference: the id search finds nothing.
        a.TargetAnimationId = 0;
        b.TargetAnimationId = 0;
        owner.LogicEntity = Entity(2, EntityStatus.FlagToDestroy);
        runner.RunOneScriptCall(owner, StateFor(document));
        Assert.Equal(0u, a.TargetAnimationId);
        Assert.Equal(0u, b.TargetAnimationId);
    }

    [Fact]
    public void SetEntityAnim_0x59_GetOwner_0x80_SetsTheLogicEntity()
    {
        var document = NewDocument(0x59, 0x80, 9, 0xFF);
        var runner = NewRunner(document, new FakeWorld());
        var owner = Entity(1);
        var logic = Entity(2);
        owner.LogicEntity = logic;

        runner.RunOneScriptCall(owner, StateFor(document));

        Assert.Equal(9u, logic.TargetAnimationId);
        Assert.Equal(0u, owner.TargetAnimationId);
    }

    // ---------------------------------------------------------------------------------------------
    // T4: the production loop guard (D-E19-3)
    // ---------------------------------------------------------------------------------------------

    /// <summary>Three instructions per turn (0x01, 0x01, then 0x02 back to the start): 1024 does not divide by 3, so
    /// the guard cuts in the middle of a turn, at code index 1.</summary>
    private static readonly int[] ThreeInstructionLoop = { 0x01, 0x01, 0x02, 0xFE, 0xFF };

    private const string LoopGuardText = "loop guard";

    private static List<string> LoopGuardWarnings(SaveGameDirectorTestSupport.LogCapture log)
        => log.Warnings.Where(w => w.Contains(LoopGuardText)).ToList();

    [Fact]
    public void LoopGuard_ASuspendFreeLoop_ReturnsAfterTheProductionBudget_AndResumesAtTheSameCodeIndex()
    {
        var document = NewDocument(ThreeInstructionLoop);
        var runner = NewRunner(document);
        var records = new List<EventTraceRecord>();
        runner.TraceSink = records.Add;
        var owner = Entity(1);
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state); // returns instead of looping forever.

        Assert.Equal(AlundraEventProgramRunner.ProductionLoopBudget, records.Count(r => r.Kind == EventTraceKind.Implemented));
        var cut = Assert.Single(records, r => r.Kind == EventTraceKind.LoopBudgetExceeded);
        Assert.Same(records[^1].State, cut.State);
        Assert.Equal(1, state.CodeIndex); // the 1024th instruction was the 0x01 at 0: the next one is at 1, not yet read.

        records.Clear();
        runner.RunOneScriptCall(owner, state); // the same state, next frame: resumes AT the opcode it was cut before.
        Assert.Equal(1, records[0].CodeIndex);
    }

    [Fact]
    public void LoopGuard_ReportsTheTraceKind_TheCodeIndexAndTheOpcodeAboutToBeRead()
    {
        var document = NewDocument(ThreeInstructionLoop);
        var runner = NewRunner(document);
        EventTraceRecord? cut = null;
        runner.TraceSink = r =>
        {
            if (r.Kind == EventTraceKind.LoopBudgetExceeded)
            {
                cut = r;
            }
        };
        var state = StateFor(document);

        runner.RunOneScriptCall(Entity(1), state, ScriptHelper.ProgramCTick);

        Assert.NotNull(cut);
        Assert.Equal(EventTraceKind.LoopBudgetExceeded, cut!.Value.Kind);
        Assert.Equal(ScriptHelper.ProgramCTick, cut.Value.ProgramSlot);
        Assert.Equal(1, cut.Value.CodeIndex);
        Assert.Equal(0x01, cut.Value.Opcode); // Codes[CodeIndex], not state.Sp (the opcode read just before).
    }

    [Fact]
    public void LoopGuard_LogsOnce_PerOwnerSlotAndProgramIndex_NotPerFrame_NorPerPc()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var document = new EventProgramDocument
        {
            EventCodesBTable = new[] { 0, 0 },
            Codes = ThreeInstructionLoop,
        };
        var runner = NewRunner(document);
        var owner = Entity(11);
        owner.ProgramIndexes[ScriptHelper.ProgramBMap] = 0x80;

        // Many frames of the same program: the loop resumes at a different pc each frame, one warning only.
        for (var frame = 0; frame < 5; frame++)
        {
            runner.RunScript(owner, ScriptHelper.ProgramBMap);
        }

        var warnings = LoopGuardWarnings(log);
        Assert.Single(warnings, w => w.Contains("entity[11]"));

        // Another program of the same owner and slot (the map events all have the hero as owner, slot B) logs again.
        owner.ProgramIndexes[ScriptHelper.ProgramBMap] = 0x81;
        owner.EventProgramState.Codes = null;
        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        Assert.Equal(2, LoopGuardWarnings(log).Count(w => w.Contains("entity[11]")));
    }

    [Fact]
    public void LoopGuard_SlotsBAndCResumeAtTheSameOpcode_ButSlotAStartsOver()
    {
        var document = new EventProgramDocument
        {
            EventCodesATable = new[] { 0 },
            EventCodesBTable = new[] { 0 },
            Codes = ThreeInstructionLoop,
        };
        var runner = NewRunner(document);
        var records = new List<EventTraceRecord>();
        runner.TraceSink = records.Add;
        var owner = Entity(1);
        owner.ProgramIndexes[ScriptHelper.ProgramBMap] = 0x80;
        owner.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x80;

        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        records.Clear();
        runner.RunScript(owner, ScriptHelper.ProgramBMap);
        Assert.Equal(1, records[0].CodeIndex); // B: resumes where it was cut.

        records.Clear();
        runner.RunScript(owner, ScriptHelper.ProgramALoad);
        records.Clear();
        runner.RunScript(owner, ScriptHelper.ProgramALoad);
        Assert.Equal(0, records[0].CodeIndex); // A: starts over, as after any yield.
    }

    [Fact]
    public void LoopGuard_ALoopWithAWait_NeverTriggersIt()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        // 0x01, then 0x37 Wait(1), then back to the start: every turn suspends. The Wait is not at index 0 because
        // the DLL compares Parameters[1] with the code INDEX where the original compares the pc POINTER (never
        // null): at index 0 a freshly cleared Parameters[1] already reads as a re-entry. A deviation of the DLL,
        // unreachable on real data (no program starts at index 0), not a fidelity point.
        var document = NewDocument(0x01, 0x37, 1, 0x02, 0xFD, 0xFF);
        var runner = NewRunner(document);
        var cuts = 0;
        runner.TraceSink = r => cuts += r.Kind == EventTraceKind.LoopBudgetExceeded ? 1 : 0;
        var owner = Entity(21);
        var state = StateFor(document);

        for (var frame = 0; frame < 3000; frame++)
        {
            runner.RunOneScriptCall(owner, state);
        }

        Assert.Equal(0, cuts);
        Assert.Empty(LoopGuardWarnings(log).Where(w => w.Contains("entity[21]")));
    }

    [Fact]
    public void LoopGuard_MaxIterationsPerCall_ReplacesTheProductionBudget()
    {
        var document = NewDocument(ThreeInstructionLoop);
        var runner = NewRunner(document);
        runner.MaxIterationsPerCall = 10;
        var implemented = 0;
        runner.TraceSink = r => implemented += r.Kind == EventTraceKind.Implemented ? 1 : 0;

        runner.RunOneScriptCall(Entity(1), StateFor(document));

        Assert.Equal(10, implemented);
    }

    [Fact]
    public void Clone_DoesNotCopyTheLogicEntity_ANewEntityStartsOnItself()
    {
        var owner = Entity(1);
        owner.LogicEntity = Entity(2);

        var clone = Assert.IsType<AlundraEntityScriptProxy>(owner.Clone());

        Assert.Null(clone.LogicEntity);
    }
}
