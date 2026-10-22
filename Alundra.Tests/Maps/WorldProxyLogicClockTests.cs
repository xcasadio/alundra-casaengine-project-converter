using Xunit;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Physics;
using Alundra.Hero;

namespace Alundra.Tests;

/// <summary>
/// Bug fix (user-reported runtime pacing bug - see <see cref="LogicClock"/>'s own class doc): the
/// integration half of the fix's acceptance - a bare <see cref="EntityScriptProxy"/>, driven
/// entirely through its own public <see cref="EntityScriptProxy.Update"/> (never calling
/// <see cref="EntityScriptProxy.PickEventTrigger"/>/<see cref="EntityScriptProxy.RunPickedEvent"/>
/// directly, unlike <see cref="AlundraWorldProxyEventPassTests"/>), against a host sharing ONE
/// <see cref="LogicClock"/> - the same wiring <see cref="Alundra.Maps.WorldProxy"/> uses in
/// production. Confirms the script pick/run pass now dispatches at 50/s regardless of the caller's own
/// frame rate, not at whatever rate <see cref="EntityScriptProxy.Update"/> itself is called.
/// </summary>
public class AlundraWorldProxyLogicClockTests
{
    private sealed class CountingRunner : IEventProgramRunner
    {
        public int Calls;
        public void RunScript(EntityScriptProxy entity, int programSlot) => Calls++;
        public void RunSpriteEvent(EntityScriptProxy entity) => Calls++;
    }

    /// <summary>Minimal <see cref="IScriptHost"/> - one shared <see cref="LogicClock"/>,
    /// exactly the field every real host (<see cref="Alundra.Maps.WorldProxy"/>, the intro trace
    /// harness's own HeadlessIntroSimulation) owns. World-level callers (RunMapEventsPass et al) are out
    /// of scope for this test - only the per-entity half is exercised here.</summary>
    private sealed class ClockHost : IScriptHost
    {
        public IEventProgramRunner Runner { get; }
        public EntityScriptProxy? ActiveCollisionEntity { get; set; }
        public GameState GameState { get; } = new();
        public HeroController? PlayerController => null;
        public IReadOnlyList<EntityScriptProxy> Collidables { get; } = System.Array.Empty<EntityScriptProxy>();

        private readonly LogicClock _logicClock = new();

        public ClockHost(IEventProgramRunner runner) => Runner = runner;

        public void DestroyEntity(EntityScriptProxy entity, int effectId)
        {
        }

        // Production shape (AlundraWorldProxy.Update): the clock is advanced/read once per real engine
        // frame and explicitly closed by whichever caller runs last. This test drives ONE bare entity with
        // no separate "world" caller, so it plays both roles itself - CloseFrame right after reading,
        // exactly like AlundraWorldProxy.Update does at the tail of its own per-frame pass.
        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            return ticks;
        }
    }

    /// <summary>
    /// Real <see cref="CasaEngine.Framework.Scene.Entities.Entity"/>/<see cref="EntityScriptProxy"/>
    /// pair, same construction shape <see cref="EntitySpawnFactory.CreateEntityFromPrefab"/> uses
    /// (<c>entity.Initialize()</c>) - needed because <see cref="EntityScriptProxy.Update"/>'s own
    /// tail (<see cref="FrameSyncPasses.SyncAnimation"/>/<see cref="FrameSyncPasses.SyncTransform"/>)
    /// reads <c>Owner</c>, which only a real <c>Initialize(Entity)</c> call sets - a bare
    /// <c>new AlundraEntityScriptProxy()</c> (as <see cref="AlundraWorldProxyEventPassTests"/> uses) is not
    /// enough here since this test drives the full <see cref="EntityScriptProxy.Update"/>, not just
    /// its pick/run half directly.
    /// </summary>
    private static EntityScriptProxy BuildDispatchingEntity(IScriptHost host)
    {
        var entity = new CasaEngine.Framework.Scene.Entities.Entity
        {
            GameplayProxyClassName = typeof(EntityScriptProxy).FullName!,
        };
        entity.Initialize();

        var proxy = (EntityScriptProxy)entity.GameplayProxy!;
        proxy.IsPlayer = false;
        proxy.Status = EntityStatus.Normal; // PickEventTrigger's Normal branch always sets a non-Unknown
                                             // eventProgramType here (TouchingEntity/BlockedByEntity both
                                             // null, ActiveCollisionEntity null) - ProgramCTick every pick.
        proxy.ScriptHost = host;
        return proxy;
    }

    [Fact]
    public void Update_DrivenAt123Hz_DispatchesScriptStepsAt50PerSecond_NotAt123PerSecond()
    {
        var runner = new CountingRunner();
        var host = new ClockHost(runner);
        var entity = BuildDispatchingEntity(host);

        const int frames = 123 * 5; // 5 simulated seconds at ~123 fps
        const float dt = 1f / 123f;

        for (var i = 0; i < frames; i++)
        {
            entity.Update(dt);
        }

        // 5 seconds at 50 Hz = 250 dispatches, not 615 (123*5) - the pre-fix bug this test guards against.
        Assert.InRange(runner.Calls, 245, 255);
    }

    [Fact]
    public void Update_DrivenAt50Hz_DispatchesScriptStepsOneToOneWithFrames()
    {
        var runner = new CountingRunner();
        var host = new ClockHost(runner);
        var entity = BuildDispatchingEntity(host);

        const int frames = 200;
        const float dt = ScriptedMotion.FixedTickSeconds;

        for (var i = 0; i < frames; i++)
        {
            entity.Update(dt);
        }

        Assert.Equal(frames, runner.Calls);
    }
}
