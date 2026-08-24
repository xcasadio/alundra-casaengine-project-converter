using Alundra.Scripts;
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
        => new() { Id = id, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, ProgramBMap = programBMap, Entity = player };

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
    /// Regression for the A3 fix: GameEngine.cs:1702 indexes the FIXED <c>g_mapEvents[0x40]</c> array by
    /// RECORD position (every record, including ones whose EventCodesBIndex is 0, occupies a slot -
    /// InitializeMapEvents sets <c>Id = i</c> for all 0x40 of them). This port's own <c>mapEvents</c> list
    /// is COMPACTED (BuildMapEvents skips records with EventCodesBIndex == 0 entirely), so a B==0 record
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

    [Fact]
    public void RunMapEventsPass_PlayerOutOfZone_ResetsLogicEntityState_AndDoesNotRun()
    {
        var runner = new RecordingRunner();
        var player = new AlundraEntityScriptProxy { IsPlayer = true, TileX = 50, TileY = 50, Index = 7 };
        var mapEvent = NewMapEvent(0, 0, 10, 10, 129, player);
        mapEvent.EventData.Sp = 0xAB;
        mapEvent.Entity!.ChildEntity = new Entity();
        mapEvent.Entity.RelativeWarpOffsetX = 42;

        AlundraWorldProxy.RunMapEventsPass(player, new[] { mapEvent }, runner, playerControlFlags: 0);

        Assert.Empty(runner.Calls);
        Assert.Null(mapEvent.Entity.ChildEntity);
        Assert.Equal(0, mapEvent.Entity.RelativeWarpOffsetX);
        Assert.Equal(7, mapEvent.Entity.Index);
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
