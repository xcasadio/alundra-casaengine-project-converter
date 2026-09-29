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
        var mapEvent = new AlundraMapEvent { Id = 0, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10, ProgramBMap = 129, Entity = npc };

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

    [Fact]
    public void Clone_DoesNotCopyTheLogicEntity_ANewEntityStartsOnItself()
    {
        var owner = Entity(1);
        owner.LogicEntity = Entity(2);

        var clone = Assert.IsType<AlundraEntityScriptProxy>(owner.Clone());

        Assert.Null(clone.LogicEntity);
    }
}
