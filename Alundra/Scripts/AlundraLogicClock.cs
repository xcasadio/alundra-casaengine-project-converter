#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// Bug fix (user-reported runtime pacing bug, gull entity 6, docs/... see commit message for the log
/// evidence): the alundra-project runs with "IsFixedTimeStep": false, so
/// <see cref="AlundraEntityScriptProxy.Update"/>/<see cref="AlundraWorldProxy.Update"/> are called once
/// per RENDERED frame, not once per the original PSX build's own fixed 50 Hz game tick. Everything the
/// original counted in FRAMES (the whole event-program 0x37 Wait chronology, MapEvents, the do/while
/// catch-up rescan) ran at that same fixed rate; running the SAME per-frame pick/run pass once per
/// rendered frame instead makes it run at the display's own (unrelated, usually much higher) rate. This
/// clock decouples the two: it hands out an integer TICK COUNT per rendered frame, reusing
/// <see cref="AlundraScriptedMotion.FixedTickSeconds"/>/<see cref="AlundraScriptedMotion.MaxTicksPerFrame"/>
/// (the SAME constants the hero/NPC kinematic mover already fixed-steps at - one clock, one rate, not a
/// second invented one) - callers loop that many times over whatever they gate on ticks.
///
/// ONE clock is shared by the whole world (an <see cref="IAlundraScriptHost"/> implementer owns exactly
/// one instance - <see cref="AlundraWorldProxy"/> in production, the intro trace harness's own
/// HeadlessIntroSimulation in tests), NOT one accumulator per entity: a per-entity accumulator would
/// phase-drift entities spawned at different real times against each other and against the world's own
/// MapEvents/catch-up passes, which must all agree on "how many ticks happened this frame" to stay in
/// lock-step (exactly the shape <see cref="AlundraScriptedMotion"/>'s OWN per-entity
/// <c>PhysicsTickAccumulator</c> deliberately does NOT need to agree on, since motion is smooth/visual,
/// not logic-stepped - see that class' own doc).
///
/// Per-frame memo: the engine updates every entity BEFORE the world's own <c>GameplayProxy.Update</c>
/// (<c>World.cs:443-491</c>), so the world proxy cannot itself be first to know this frame's tick count
/// most frames - whichever caller reaches <see cref="TicksThisFrame"/> FIRST this frame advances the
/// accumulator and fixes the count (<see cref="_frameComputed"/>); every later caller the SAME frame
/// reads back that same cached value regardless of the <c>elapsedTime</c> it passes (which is expected to
/// be identical - all callers this frame observed the same engine tick). <see cref="CloseFrame"/> is
/// called exactly once, by whichever caller runs LAST this frame (the world proxy's own <c>Update</c>,
/// after every entity's <c>Update</c> already ran) so the NEXT frame's first caller recomputes fresh. A
/// world with zero spawned entities still needs its clock to advance every frame - the world proxy itself
/// becomes the first (and only, and closing) caller in that case; see
/// <see cref="AlundraWorldProxy.Update"/>'s own call site.
/// </summary>
internal sealed class AlundraLogicClock
{
    private float _accumulator;
    private int _ticksThisFrame;
    private bool _frameComputed;

    /// <summary>
    /// Returns this frame's tick count, computing and caching it on the first call this frame (see this
    /// class' own doc) - every later call this frame is a pure cache read, <paramref name="elapsedTime"/>
    /// ignored. Same catch-up-4/leftover-carries shape as <see cref="AlundraScriptedMotion.TickPlayer"/>'s
    /// own accumulator loop, just counting steps instead of running them.
    /// </summary>
    internal int TicksThisFrame(float elapsedTime)
    {
        if (_frameComputed)
        {
            return _ticksThisFrame;
        }

        _accumulator += elapsedTime;

        var ticks = 0;
        while (_accumulator >= AlundraScriptedMotion.FixedTickSeconds && ticks < AlundraScriptedMotion.MaxTicksPerFrame)
        {
            _accumulator -= AlundraScriptedMotion.FixedTickSeconds;
            ticks++;
        }

        if (ticks >= AlundraScriptedMotion.MaxTicksPerFrame)
        {
            _accumulator = 0f;
        }

        _ticksThisFrame = ticks;
        _frameComputed = true;
        return ticks;
    }

    /// <summary>Closes the current frame's memo (see this class' own doc) - called exactly once per
    /// frame, by whichever caller runs last (the world proxy's own <c>Update</c>).</summary>
    internal void CloseFrame()
    {
        _frameComputed = false;
    }
}
