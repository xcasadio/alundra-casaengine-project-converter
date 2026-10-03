using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the E1 replacement for the old manager-level <c>AlundraWorldProxy.RunEntityEventsPass</c>
/// (removed - see docs/plan-conversion-totale.md §2 decision D2/D3): per-entity pick/run
/// (<see cref="AlundraEntityScriptProxy.PickEventTrigger"/>/<see cref="AlundraEntityScriptProxy.RunPickedEvent"/>,
/// exercised directly - this is what each entity's own <c>Update</c> now calls) plus the world's own
/// catch-up re-scan (<see cref="AlundraWorldProxy.RunPendingEventTriggers"/>, decision D3) and MapEvents
/// pass (<see cref="AlundraWorldProxy.RunMapEventsPass"/>). Uses a fake <see cref="IEventProgramRunner"/>
/// recording every RunScript/RunSpriteEvent call, and a fake <see cref="IAlundraScriptHost"/> instead of a
/// live <see cref="AlundraWorldProxy"/>/<see cref="CasaEngine.Framework.Scene.World.World"/>.
/// </summary>
public class AlundraWorldProxyEventPassTests
{
    private sealed record RunCall(AlundraEntityScriptProxy Entity, int? ProgramSlot);

    private sealed class RecordingRunner : IEventProgramRunner
    {
        public readonly List<RunCall> Calls = new();

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            Calls.Add(new RunCall(entity, programSlot));
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
            Calls.Add(new RunCall(entity, null));
        }
    }

    private sealed class FakeScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; }
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = new List<AlundraEntityScriptProxy>();
        public readonly List<(AlundraEntityScriptProxy Entity, int EffectId)> Destroyed = new();

        public FakeScriptHost(IEventProgramRunner runner)
        {
            Runner = runner;
        }

        // Bug fix (AlundraLogicClock's own class doc): this file exercises PickEventTrigger/RunPickedEvent
        // directly, never through AlundraEntityScriptProxy.Update, so nothing here ever calls this - kept
        // only to satisfy the interface. Auto-closes its own clock each call (own frame per call) in case
        // a future test does call Update, same pattern as every other FakeScriptHost in this test project.
        private readonly AlundraLogicClock _logicClock = new();
        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            return ticks;
        }

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
            Destroyed.Add((entity, effectId));
            entity.Status = EntityStatus.FlagToDestroy;
        }
    }

    private static AlundraEntityScriptProxy NewEntity(
        EntityStatus status, int[]? programIndexes = null, IAlundraScriptHost? host = null)
    {
        var entity = new AlundraEntityScriptProxy { Status = status, ScriptHost = host };
        if (programIndexes != null)
        {
            programIndexes.CopyTo(entity.ProgramIndexes, 0);
        }

        return entity;
    }

    /// <summary>Runs <see cref="AlundraEntityScriptProxy.PickEventTrigger"/> then
    /// <see cref="AlundraEntityScriptProxy.RunPickedEvent"/> for <paramref name="entity"/> - what each
    /// entity's own <c>Update</c> does for a non-player entity (see that method's own doc).</summary>
    private static void PickAndRun(AlundraEntityScriptProxy entity, IEventProgramRunner runner)
    {
        entity.PickEventTrigger();
        entity.RunPickedEvent(runner);
    }

    [Fact]
    public void Loaded_RunsProgramALoad_AndBecomesNormal_SameFrame()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Loaded, new[] { 1, 0, 0, 0, 0, 0 }, host);

        PickAndRun(entity, runner);

        Assert.Equal(EntityStatus.Normal, entity.Status);
        var call = Assert.Single(runner.Calls);
        Assert.Same(entity, call.Entity);
        Assert.Equal(ScriptHelper.ProgramALoad, call.ProgramSlot);
        Assert.Equal(-1, entity.EventTrigger);
    }

    [Fact]
    public void Normal_NoTouchingEntity_RunsProgramCTick()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entity.TouchingEntity = null;

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramCTick, call.ProgramSlot);
    }

    [Fact]
    public void Normal_WithTouchingEntity_RunsProgramDTouch()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 1, 0, 0 }, host);
        entity.TouchingEntity = new Entity();

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramDTouch, call.ProgramSlot);
    }

    [Fact]
    public void Normal_ActiveCollisionEntityAndInteractProgramSet_RunsProgramFInteract()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 0, 0, 1 }, host);
        entity.TouchingEntity = null;
        host.ActiveCollisionEntity = entity;

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramFInteract, call.ProgramSlot);
    }

    [Fact]
    public void Normal_ActiveCollisionEntityButNoInteractProgram_StaysProgramCTick()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entity.TouchingEntity = null;
        host.ActiveCollisionEntity = entity;

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramCTick, call.ProgramSlot);
    }

    [Fact]
    public void DeactivateOnHit_TriggersDeactivatedStatus_AndProgramEDeactivate_NextFrameStillSlotE()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 0, 1, 0 }, host);
        entity.Flags = EntityFlags.DeactivateOnHit;
        entity.HitCounter = 1;

        PickAndRun(entity, runner);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramEDeactivate, call.ProgramSlot);

        // Next frame: still Deactivated, still runs slot E.
        PickAndRun(entity, runner);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(ScriptHelper.ProgramEDeactivate, runner.Calls[1].ProgramSlot);
    }

    [Fact]
    public void BlockedByEntity_SetsEventTriggerToUnknown_AndRunsNothing()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entity.BlockedByEntity = new Entity();

        PickAndRun(entity, runner);

        Assert.Empty(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramUnknown, entity.EventTrigger);
    }

    [Theory]
    [InlineData(EntityStatus.Destroyed)]
    [InlineData(EntityStatus.FlagToDestroy)]
    public void DestroyedOrFlagToDestroy_RunsNothing(EntityStatus status)
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(status, new[] { 0, 0, 1, 0, 0, 0 }, host);

        PickAndRun(entity, runner);

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void ProgramIndexZero_DispatchesRunSpriteEvent()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Loaded, new[] { 0, 0, 0, 0, 0, 0 }, host);

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Null(call.ProgramSlot);
    }

    [Fact]
    public void ProgramIndexNonZeroMaskedTo0x7f_DispatchesRunScript()
    {
        // 0x80 masked with 0x7f becomes 0 -> sprite event branch, even though the stored index is non-zero.
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Loaded, new[] { 0x80, 0, 0, 0, 0, 0 }, host);

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Null(call.ProgramSlot);
    }

    [Fact]
    public void ProgramIndexNonZeroAfterMasking_DispatchesRunScript()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Loaded, new[] { 5, 0, 0, 0, 0, 0 }, host);

        PickAndRun(entity, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramALoad, call.ProgramSlot);
    }

    [Fact]
    public void DestroyOnVramFlags_DestroysEntity_AndRunsNothing()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entity.Flags = EntityFlags.DestroyOnVramFlags;
        entity.CombinedVramFlagsOR = 0x8004;

        PickAndRun(entity, runner);

        var (destroyed, effectId) = Assert.Single(host.Destroyed);
        Assert.Same(entity, destroyed);
        Assert.Equal(-1, effectId);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void DestroyOnSlidingSlope_DestroysEntity_WithEffect6()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entity.Flags = EntityFlags.DestroyOnSlidingSlope;
        entity.Slope_18c = 4;

        PickAndRun(entity, runner);

        var (_, effectId) = Assert.Single(host.Destroyed);
        Assert.Equal(6, effectId);
        Assert.Empty(runner.Calls);
    }

    // ------------------------------------------------------------------------------------------------
    // AlundraWorldProxy.RunPendingEventTriggers - decision D3's catch-up re-scan
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void RunPendingEventTriggers_CrossEntityTrigger_ReplayedSameFrame()
    {
        var runner = new RecordingRunner();
        var host = new FakeScriptHost(runner);
        // entityA: picked/run first (mirrors its own Update having already run this frame, setting up a
        // trigger for entityB). entityB starts with EventTrigger already Unknown (nothing picked for it
        // this frame) - exercising the re-scan loop when something OTHER than its own pick sets it.
        var entityA = NewEntity(EntityStatus.Loaded, new[] { 1, 0, 0, 0, 0, 0 }, host);
        var entityB = NewEntity(EntityStatus.Destroyed, new[] { 0, 0, 1, 0, 0, 0 }, host);
        entityB.EventTrigger = ScriptHelper.ProgramUnknown;

        var calls = new List<RunCall>();
        IEventProgramRunner triggeringRunner = new DelegatingRunner((entity, slot) =>
        {
            calls.Add(new RunCall(entity, slot));
            if (ReferenceEquals(entity, entityA))
            {
                entityB.EventTrigger = ScriptHelper.ProgramCTick;
            }
        }, entity => calls.Add(new RunCall(entity, null)));

        // entityA's own Update already ran (pick + run) before the world's catch-up pass - mirror that:
        entityA.PickEventTrigger();
        entityA.RunPickedEvent(triggeringRunner);

        AlundraWorldProxy.RunPendingEventTriggers(new[] { entityA, entityB }, triggeringRunner);

        Assert.Equal(2, calls.Count);
        Assert.Same(entityA, calls[0].Entity);
        Assert.Equal(ScriptHelper.ProgramALoad, calls[0].ProgramSlot);
        Assert.Same(entityB, calls[1].Entity);
        Assert.Equal(ScriptHelper.ProgramCTick, calls[1].ProgramSlot);
        Assert.Equal(-1, entityA.EventTrigger);
        Assert.Equal(-1, entityB.EventTrigger);
    }

    [Fact]
    public void RunPendingEventTriggers_PlayerEntity_NeverRun_EvenWithATriggerSet()
    {
        var runner = new RecordingRunner();
        var player = NewEntity(EntityStatus.Normal, new[] { 1, 0, 0, 0, 0, 0 });
        player.IsPlayer = true;
        player.EventTrigger = ScriptHelper.ProgramALoad; // e.g. left over from RunMapEventsPass

        AlundraWorldProxy.RunPendingEventTriggers(new[] { player }, runner);

        Assert.Empty(runner.Calls);
        // Untouched - RunPendingEventTriggers skips the player outright, exactly like the original's own
        // loop starting at slot index 1.
        Assert.Equal(ScriptHelper.ProgramALoad, player.EventTrigger);
    }

    private sealed class DelegatingRunner : IEventProgramRunner
    {
        private readonly Action<AlundraEntityScriptProxy, int> _onRunScript;
        private readonly Action<AlundraEntityScriptProxy> _onRunSpriteEvent;

        public DelegatingRunner(Action<AlundraEntityScriptProxy, int> onRunScript, Action<AlundraEntityScriptProxy> onRunSpriteEvent)
        {
            _onRunScript = onRunScript;
            _onRunSpriteEvent = onRunSpriteEvent;
        }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => _onRunScript(entity, programSlot);

        public void RunSpriteEvent(AlundraEntityScriptProxy entity) => _onRunSpriteEvent(entity);
    }

    // ------------------------------------------------------------------------------------------------
    // AlundraWorldProxy.RunMapEventsPass - port of RunMapEvents (GameEngine.cs:1667-1718)
    // ------------------------------------------------------------------------------------------------

    private static AlundraMapEvent NewMapEvent(
        int x1, int y1, int x2, int y2, int programBMap, AlundraEntityScriptProxy player, int id = 0)
        => new() { Id = id, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, ProgramBMap = programBMap, OriginalProgramBMap = programBMap, Entity = player };

    [Fact]
    public void RunMapEventsPass_PlayerInZone_RunsProgramB_AndCopiesStateBack()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5 };
        // Id != the list position (0) on purpose - EventTrigger must come from the record's own index
        // (mapEvent.Id), not the (possibly compacted) list position - see the next test for the case
        // where they actually diverge.
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, player, id: 3);

        AlundraWorldProxy.RunMapEventsPass(player, new[] { mapEvent }, runner, playerControlFlags: 0);

        var call = Assert.Single(runner.Calls);
        Assert.Same(player, call.Entity);
        Assert.Equal(ScriptHelper.ProgramBMap, call.ProgramSlot);
        Assert.Equal(129, player.ProgramIndexes[ScriptHelper.ProgramBMap]);
        Assert.Equal(129, player.MapEventProgramId);
        Assert.Equal(3, player.EventTrigger); // record index (mapEvent.Id) of the map event that ran
        Assert.Same(player, player.LogicEntity); // initial logic entity is the player itself
    }

    /// <summary>
    /// Regression for the A3 fix: the decompilation (GameEngine.cs:1702) indexes the FIXED <c>g_mapEvents[0x40]</c>
    /// array by RECORD position (every record, including ones whose EventCodesBIndex is 0, occupies a slot -
    /// InitializeMapEvents sets <c>Id = i</c> for all 0x40 of them); the binary compacts its slot table
    /// (0x8003C5F4-0x8003C608), with no effect on the data (no record has a zero program byte). This port's own
    /// <c>mapEvents</c> list is COMPACTED (BuildMapEvents skips records with EventCodesBIndex == 0 entirely), so a B==0 record
    /// preceding a real one shifts the real one's LIST position away from its own record index -
    /// EventTrigger must still come from <see cref="AlundraMapEvent.Id"/>, never the loop position.
    /// </summary>
    [Fact]
    public void RunMapEventsPass_SkippedZeroRecordPrecedingRealOne_EventTriggerUsesRecordIndex_NotListPosition()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5 };

        // BuildMapEvents would never actually add a ProgramBMap==0 entry (it skips it outright before
        // ever constructing an AlundraMapEvent) - this stands in for one anyway, purely to prove
        // RunMapEventsPass itself does not rely on list position even when it DOES see one (its own
        // "(ProgramBMap & 0x7F) == 0 -> continue" guard covers that case independently of A3).
        var skipped = NewMapEvent(0, 0, 10, 10, 0, player, id: 0);
        // Record index 5 (e.g. map-event 5, "134" in the real map 389 data), sitting at LIST position 1.
        var real = NewMapEvent(0, 0, 10, 10, 134, player, id: 5);

        AlundraWorldProxy.RunMapEventsPass(player, new[] { skipped, real }, runner, playerControlFlags: 0);

        var call = Assert.Single(runner.Calls);
        Assert.Same(player, call.Entity);
        Assert.Equal(5, player.EventTrigger); // real.Id, NOT its list position (1)
    }

    /// <summary>
    /// Out of zone, every tick (RunMapEvents 0x8003C7F0-0x8003C804, E19.j J-R2): the slot's program state goes back to
    /// "not started" (entry 0, pc 0, Result 0), its logic entity back to the hero and its program byte back to the
    /// record's own byte - and NO entity is written. The map event's own logic entity (here a distinct one) keeps
    /// every field it had.
    /// </summary>
    [Fact]
    public void RunMapEventsPass_PlayerOutOfZone_RearmsTheSlot_AndWritesNoEntity()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 50, TileY = 50, Index = 7 };
        var logic = new AlundraEntityScriptProxy { Index = 3, RelativeWarpOffsetX = 42 };
        var child = new Entity();
        logic.ChildEntity = child;
        logic.EventProgramState.Sp = 0xAB;
        var mapEvent = NewMapEvent(0, 0, 10, 10, 130, player);
        mapEvent.OriginalProgramBMap = 129;
        mapEvent.Entity = logic;
        mapEvent.EventData.Sp = 0xAB;
        mapEvent.EventData.Codes = new byte[] { 0x1A, 7, 0xFF };
        mapEvent.EventData.CodeIndex = 2;
        mapEvent.EventData.Result = 1;
        mapEvent.EventData.Parameters[3] = 9;

        AlundraWorldProxy.RunMapEventsPass(player, new[] { mapEvent }, runner, playerControlFlags: 0);

        Assert.Empty(runner.Calls);
        Assert.Equal(0, mapEvent.EventData.Sp);
        Assert.Null(mapEvent.EventData.Codes);
        Assert.Equal(0, mapEvent.EventData.CodeIndex);
        Assert.Equal(0, mapEvent.EventData.Result);
        Assert.Equal(9, mapEvent.EventData.Parameters[3]); // the binary keeps the other fields of the state
        Assert.Same(player, mapEvent.Entity);
        Assert.Equal(129, mapEvent.ProgramBMap);
        Assert.Same(child, logic.ChildEntity);
        Assert.Equal(0xAB, logic.EventProgramState.Sp);
        Assert.Equal(42, logic.RelativeWarpOffsetX);
        Assert.Equal(3, logic.Index);
        Assert.Equal(7, player.Index);
    }

    // ------------------------------------------------------------------------------------------------
    // E19.j J1: the out-of-zone re-arm, driven through the real interpreter (docs/plan-e19-opcodes.md §1.2l)
    // ------------------------------------------------------------------------------------------------

    private sealed class MapEventWorld : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    private const int InZone = 5;
    private const int OutOfZone = 50;

    private static EventProgramDocument BProgram(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        EventCodesBTable = new[] { 99, 0 },
        Codes = codes,
    };

    private static AlundraEventProgramRunner RealRunner(EventProgramDocument document, MapEventWorld world)
        => new(document, new AlundraGameState(), world);

    /// <summary>One tick: the hero stands on (x, x), one <see cref="AlundraWorldProxy.RunMapEventsPass"/>.</summary>
    private static void Tick(AlundraEntityScriptProxy hero, AlundraMapEvent mapEvent, IEventProgramRunner runner, int x)
    {
        hero.TileX = x;
        hero.TileY = x;
        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);
    }

    [Fact]
    public void RunMapEventsPass_J1a_ProgramLeftMidWay_RestartsFromTheStartOnReentry()
    {
        var hero = new AlundraEntityScriptProxy { IsPlayer = true };
        var world = new MapEventWorld { PlayerEntity = hero };
        var runner = RealRunner(BProgram(0x1A, 7, 0x00, 0x1A, 8, 0xFF), world);
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, hero);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, hero.TargetAnimationId);
        hero.TargetAnimationId = 0;

        Tick(hero, mapEvent, runner, OutOfZone);
        Assert.Null(mapEvent.EventData.Codes);
        Assert.Equal(0, mapEvent.EventData.Result);
        Assert.Same(hero, mapEvent.Entity);
        Assert.Equal(129, mapEvent.ProgramBMap);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, hero.TargetAnimationId); // from the start again, not the 8 that follows the Break
    }

    [Fact]
    public void RunMapEventsPass_J1b_FinishedProgram_RunsAgainAfterALeaveAndReentry()
    {
        var hero = new AlundraEntityScriptProxy { IsPlayer = true };
        var world = new MapEventWorld { PlayerEntity = hero };
        var runner = RealRunner(BProgram(0x1A, 7, 0xFF), world);
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, hero);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, hero.TargetAnimationId);
        hero.TargetAnimationId = 0;

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(0u, hero.TargetAnimationId); // finished: it does not run again while the hero stays in

        Tick(hero, mapEvent, runner, OutOfZone);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, hero.TargetAnimationId);
    }

    [Fact]
    public void RunMapEventsPass_J1c_OutOfZone_PutsTheLogicEntityBackOnTheHero_WithoutWritingAnyEntity()
    {
        var hero = new AlundraEntityScriptProxy
        {
            IsPlayer = true, Status = EntityStatus.Normal, RelativeWarpOffsetX = 17, Index = 9,
        };
        var npc = new AlundraEntityScriptProxy
        {
            EntityRefId = 5, Status = EntityStatus.Normal, RelativeWarpOffsetX = 42, Index = 3,
        };
        var child = new Entity();
        npc.ChildEntity = child;
        npc.EventProgramState.Sp = 0xAB;
        var world = new MapEventWorld { PlayerEntity = hero };
        world.Spawned.Add(npc);
        var runner = RealRunner(BProgram(0x43, 5, 0x00, 0x1A, 7, 0xFF), world);
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, hero);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Same(npc, mapEvent.Entity);

        Tick(hero, mapEvent, runner, OutOfZone);
        Assert.Same(hero, mapEvent.Entity);
        Assert.Same(child, npc.ChildEntity);
        Assert.Equal(0xAB, npc.EventProgramState.Sp);
        Assert.Equal(42, npc.RelativeWarpOffsetX);
        Assert.Equal(3, npc.Index);
        Assert.Equal(17, hero.RelativeWarpOffsetX);
        Assert.Equal(9, hero.Index);

        Tick(hero, mapEvent, runner, InZone); // restarts: 0x43 again, then the Break
        Assert.Equal(0u, npc.TargetAnimationId);
        Assert.Same(npc, mapEvent.Entity);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, npc.TargetAnimationId);
    }

    [Fact]
    public void RunMapEventsPass_J1d_OutOfZone_ClearsResult()
    {
        var hero = new AlundraEntityScriptProxy { IsPlayer = true };
        var world = new MapEventWorld { PlayerEntity = hero };
        // @0 IfTrueGoto 6 ; @3 anim 7 ; @5 end ; @6 anim 8 ; @8 end.
        var runner = RealRunner(BProgram(0x03, 6, 0, 0x1A, 7, 0xFF, 0x1A, 8, 0xFF), world);
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, hero);
        mapEvent.EventData.Result = 1;

        Tick(hero, mapEvent, runner, OutOfZone);
        Assert.Equal(0, mapEvent.EventData.Result);

        Tick(hero, mapEvent, runner, InZone);
        Assert.Equal(7u, hero.TargetAnimationId);
    }

    [Fact]
    public void RunMapEventsPass_J1e_ProgramByte_ZeroMaskedIsSkippedBeforeTheZoneTest_StartedByteGoesBackToTheOriginal()
    {
        var runner = new RecordingRunner();
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, TileX = OutOfZone, TileY = OutOfZone };

        var masked = NewMapEvent(0, 0, 10, 10, 129, hero);
        masked.ProgramBMap = 0x80; // masked to 0: skipped before the zone test, so not put back either
        var started = NewMapEvent(0, 0, 10, 10, 129, hero);
        started.ProgramBMap = 130;

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { masked, started }, runner, playerControlFlags: 0);

        Assert.Equal(0x80, masked.ProgramBMap);
        Assert.Equal(129, started.ProgramBMap);
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData(4, 6, true)]
    [InlineData(2, 3, true)]
    [InlineData(5, 6, false)]
    [InlineData(4, 7, false)]
    [InlineData(1, 3, false)]
    public void RunMapEventsPass_J1f_ZoneBoundsAreInclusive(int tileX, int tileY, bool runs)
    {
        var runner = new RecordingRunner();
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, TileX = tileX, TileY = tileY };
        var mapEvent = NewMapEvent(2, 3, 4, 6, 129, hero);

        AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);

        Assert.Equal(runs ? 1 : 0, runner.Calls.Count);
    }

    /// <summary>Real data, off the story chain: Torla (inner) 445, record 0 (zone (1,8)-(13,59), program byte 129).</summary>
    [Fact]
    public void RunMapEventsPass_J1g_RealMap445Record0_ReplaysItsSoundsOnReentry()
    {
        var projectRoot = FindProjectRoot();
        var document = MapEventProgramLoader.Load(projectRoot, "Torla (inner)-445");
        Assert.NotNull(document);

        var hero = new AlundraEntityScriptProxy { IsPlayer = true };
        var world = new MapEventWorld { PlayerEntity = hero };
        var runner = RealRunner(document!, world);
        var trace = new List<(int Opcode, int CodeIndex, EventTraceKind Kind)>();
        runner.TraceSink = r => trace.Add((r.Opcode, r.CodeIndex, r.Kind));
        var mapEvent = NewMapEvent(1, 8, 13, 59, 129, hero);

        List<(int Opcode, int CodeIndex, EventTraceKind Kind)> TickAt(int x, int y)
        {
            trace.Clear();
            hero.TileX = x;
            hero.TileY = y;
            AlundraWorldProxy.RunMapEventsPass(hero, new[] { mapEvent }, runner, playerControlFlags: 0);
            return new List<(int, int, EventTraceKind)>(trace);
        }

        var enter = new[]
        {
            (0xBD, 160, EventTraceKind.Degraded), (0xBF, 163, EventTraceKind.Degraded), (0xFF, 168, EventTraceKind.End),
        };

        Assert.Equal(enter, TickAt(5, 20));
        Assert.Equal(new[] { (0xFF, 168, EventTraceKind.End) }, TickAt(5, 20));
        Assert.Empty(TickAt(20, 20));
        Assert.Equal(enter, TickAt(5, 20));
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraWorldProxyEventPassTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - the J1-g test needs the real converter export of map 445.");
    }

    [Fact]
    public void RunMapEventsPass_ProgramBMapZeroMasked_Skipped()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5 };
        var mapEvent = NewMapEvent(0, 0, 10, 10, 0x80, player); // masked to 0

        AlundraWorldProxy.RunMapEventsPass(player, new[] { mapEvent }, runner, playerControlFlags: 0);

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void RunMapEventsPass_GameplayBlockedMask_SkipsEverything()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 5, TileY = 5 };
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, player);

        AlundraWorldProxy.RunMapEventsPass(
            player, new[] { mapEvent }, runner, AlundraGameState.PlayerControlBits.MenuOpen);

        Assert.Empty(runner.Calls);
    }
}
