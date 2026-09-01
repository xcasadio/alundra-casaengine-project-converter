using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Bug fix (user-reported runtime pacing bug - see <see cref="AlundraLogicClock"/>'s own class doc): the
/// integration half of the fix's acceptance - a bare <see cref="AlundraEntityScriptProxy"/>, driven
/// entirely through its own public <see cref="AlundraEntityScriptProxy.Update"/> (never calling
/// <see cref="AlundraEntityScriptProxy.PickEventTrigger"/>/<see cref="AlundraEntityScriptProxy.RunPickedEvent"/>
/// directly, unlike <see cref="AlundraWorldProxyEventPassTests"/>), against a host sharing ONE
/// <see cref="AlundraLogicClock"/> - the same wiring <see cref="Alundra.Scripts.AlundraWorldProxy"/> uses in
/// production. Confirms the script pick/run pass now dispatches at 50/s regardless of the caller's own
/// frame rate, not at whatever rate <see cref="AlundraEntityScriptProxy.Update"/> itself is called.
/// </summary>
public class AlundraWorldProxyLogicClockTests
{
    private sealed class CountingRunner : IEventProgramRunner
    {
        public int Calls;
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => Calls++;
        public void RunSpriteEvent(AlundraEntityScriptProxy entity) => Calls++;
    }

    /// <summary>Minimal <see cref="IAlundraScriptHost"/> - one shared <see cref="AlundraLogicClock"/>,
    /// exactly the field every real host (<see cref="Alundra.Scripts.AlundraWorldProxy"/>, the intro trace
    /// harness's own HeadlessIntroSimulation) owns. World-level callers (RunMapEventsPass et al) are out
    /// of scope for this test - only the per-entity half is exercised here.</summary>
    private sealed class ClockHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; }
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = System.Array.Empty<AlundraEntityScriptProxy>();

        private readonly AlundraLogicClock _logicClock = new();

        public ClockHost(IEventProgramRunner runner) => Runner = runner;

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
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
    /// Real <see cref="CasaEngine.Framework.Scene.Entities.Entity"/>/<see cref="AlundraEntityScriptProxy"/>
    /// pair, same construction shape <see cref="AlundraEntitySpawnFactory.CreateEntityFromPrefab"/> uses
    /// (<c>entity.Initialize()</c>) - needed because <see cref="AlundraEntityScriptProxy.Update"/>'s own
    /// tail (<see cref="AlundraFrameSyncPasses.SyncAnimation"/>/<see cref="AlundraFrameSyncPasses.SyncTransform"/>)
    /// reads <c>Owner</c>, which only a real <c>Initialize(Entity)</c> call sets - a bare
    /// <c>new AlundraEntityScriptProxy()</c> (as <see cref="AlundraWorldProxyEventPassTests"/> uses) is not
    /// enough here since this test drives the full <see cref="AlundraEntityScriptProxy.Update"/>, not just
    /// its pick/run half directly.
    /// </summary>
    private static AlundraEntityScriptProxy BuildDispatchingEntity(IAlundraScriptHost host)
    {
        var entity = new CasaEngine.Framework.Scene.Entities.Entity
        {
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();

        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy!;
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
        const float dt = AlundraScriptedMotion.FixedTickSeconds;

        for (var i = 0; i < frames; i++)
        {
            entity.Update(dt);
        }

        Assert.Equal(frames, runner.Calls);
    }
}
