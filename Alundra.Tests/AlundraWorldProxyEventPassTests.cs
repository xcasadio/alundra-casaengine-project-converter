using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraWorldProxy.RunEntityEventsPass"/>, the headless-testable two-phase entity
/// event pass ported from <c>EntityManager.UpdateEntitiesEvents</c> @ 0x800386D0. Runs directly over a
/// plain list of <see cref="AlundraEntityScriptProxy"/> instances, with a fake <see cref="IEventProgramRunner"/>
/// recording every RunScript/RunSpriteEvent call instead of a live <see cref="CasaEngine.Framework.Scene.World.World"/>.
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

    private static AlundraEntityScriptProxy NewEntity(EntityStatus status, int[]? programIndexes = null)
    {
        var entity = new AlundraEntityScriptProxy { Status = status };
        if (programIndexes != null)
        {
            programIndexes.CopyTo(entity.ProgramIndexes, 0);
        }

        return entity;
    }

    private static readonly Action<AlundraEntityScriptProxy, int> NoOpDestroy = (_, _) => { };

    [Fact]
    public void Loaded_RunsProgramALoad_AndBecomesNormal_SameFrame()
    {
        var entity = NewEntity(EntityStatus.Loaded, new[] { 1, 0, 0, 0, 0, 0 });
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        Assert.Equal(EntityStatus.Normal, entity.Status);
        var call = Assert.Single(runner.Calls);
        Assert.Same(entity, call.Entity);
        Assert.Equal(ScriptHelper.ProgramALoad, call.ProgramSlot);
        Assert.Equal(-1, entity.EventTrigger);
    }

    [Fact]
    public void Normal_NoTouchingEntity_RunsProgramCTick()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 });
        entity.TouchingEntity = null;
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramCTick, call.ProgramSlot);
    }

    [Fact]
    public void Normal_WithTouchingEntity_RunsProgramDTouch()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 1, 0, 0 });
        entity.TouchingEntity = new Entity();
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramDTouch, call.ProgramSlot);
    }

    [Fact]
    public void Normal_ActiveCollisionEntityAndInteractProgramSet_RunsProgramFInteract()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 0, 0, 1 });
        entity.TouchingEntity = null;
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, entity, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramFInteract, call.ProgramSlot);
    }

    [Fact]
    public void Normal_ActiveCollisionEntityButNoInteractProgram_StaysProgramCTick()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 });
        entity.TouchingEntity = null;
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, entity, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramCTick, call.ProgramSlot);
    }

    [Fact]
    public void DeactivateOnHit_TriggersDeactivatedStatus_AndProgramEDeactivate_NextFrameStillSlotE()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 0, 0, 1, 0 });
        entity.Flags = EntityFlags.DeactivateOnHit;
        entity.HitCounter = 1;
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramEDeactivate, call.ProgramSlot);

        // Next frame: still Deactivated, still runs slot E.
        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(ScriptHelper.ProgramEDeactivate, runner.Calls[1].ProgramSlot);
    }

    [Fact]
    public void BlockedByEntity_SetsEventTriggerToUnknown_AndRunsNothing()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 });
        entity.BlockedByEntity = new Entity();
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        Assert.Empty(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramUnknown, entity.EventTrigger);
    }

    [Theory]
    [InlineData(EntityStatus.Destroyed)]
    [InlineData(EntityStatus.FlagToDestroy)]
    public void DestroyedOrFlagToDestroy_RunsNothing(EntityStatus status)
    {
        var entity = NewEntity(status, new[] { 0, 0, 1, 0, 0, 0 });
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void ProgramIndexZero_DispatchesRunSpriteEvent()
    {
        var entity = NewEntity(EntityStatus.Loaded, new[] { 0, 0, 0, 0, 0, 0 });
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Null(call.ProgramSlot);
    }

    [Fact]
    public void ProgramIndexNonZeroMaskedTo0x7f_DispatchesRunScript()
    {
        // 0x80 masked with 0x7f becomes 0 -> sprite event branch, even though the stored index is non-zero.
        var entity = NewEntity(EntityStatus.Loaded, new[] { 0x80, 0, 0, 0, 0, 0 });
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Null(call.ProgramSlot);
    }

    [Fact]
    public void ProgramIndexNonZeroAfterMasking_DispatchesRunScript()
    {
        var entity = NewEntity(EntityStatus.Loaded, new[] { 5, 0, 0, 0, 0, 0 });
        var runner = new RecordingRunner();

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, NoOpDestroy);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(ScriptHelper.ProgramALoad, call.ProgramSlot);
    }

    [Fact]
    public void RescanSemantics_RunnerSettingAnotherEntitysEventTrigger_RunsItSameFrame()
    {
        var entityA = NewEntity(EntityStatus.Loaded, new[] { 1, 0, 0, 0, 0, 0 });
        // entityB starts Destroyed (nothing picked in phase 1) so it only runs if entityA's script
        // pokes its EventTrigger during phase 2 - exercising the re-scan loop.
        var entityB = NewEntity(EntityStatus.Destroyed, new[] { 0, 0, 1, 0, 0, 0 });

        var calls = new List<RunCall>();
        IEventProgramRunner runner = new DelegatingRunner((entity, slot) =>
        {
            calls.Add(new RunCall(entity, slot));
            if (ReferenceEquals(entity, entityA))
            {
                entityB.EventTrigger = ScriptHelper.ProgramCTick;
            }
        }, entity => calls.Add(new RunCall(entity, null)));

        AlundraWorldProxy.RunEntityEventsPass(new[] { entityA, entityB }, runner, null, NoOpDestroy);

        Assert.Equal(2, calls.Count);
        Assert.Same(entityA, calls[0].Entity);
        Assert.Equal(ScriptHelper.ProgramALoad, calls[0].ProgramSlot);
        Assert.Same(entityB, calls[1].Entity);
        Assert.Equal(ScriptHelper.ProgramCTick, calls[1].ProgramSlot);
        Assert.Equal(-1, entityA.EventTrigger);
        Assert.Equal(-1, entityB.EventTrigger);
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

    [Fact]
    public void DestroyOnVramFlags_DestroysEntity_AndRunsNothing()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 });
        entity.Flags = EntityFlags.DestroyOnVramFlags;
        entity.CombinedVramFlagsOR = 0x8004;
        var runner = new RecordingRunner();
        AlundraEntityScriptProxy? destroyed = null;
        var effectId = int.MinValue;

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, (e, id) =>
        {
            destroyed = e;
            effectId = id;
            e.Status = EntityStatus.FlagToDestroy;
        });

        Assert.Same(entity, destroyed);
        Assert.Equal(-1, effectId);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void DestroyOnSlidingSlope_DestroysEntity_WithEffect6()
    {
        var entity = NewEntity(EntityStatus.Normal, new[] { 0, 0, 1, 0, 0, 0 });
        entity.Flags = EntityFlags.DestroyOnSlidingSlope;
        entity.Slope_18c = 4;
        var runner = new RecordingRunner();
        var effectId = int.MinValue;

        AlundraWorldProxy.RunEntityEventsPass(new[] { entity }, runner, null, (_, id) => effectId = id);

        Assert.Equal(6, effectId);
        Assert.Empty(runner.Calls);
    }
}
