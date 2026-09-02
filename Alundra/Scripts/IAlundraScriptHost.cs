#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// Seam an <see cref="AlundraEntityScriptProxy"/> uses to reach the handful of world-level services its
/// own <see cref="AlundraEntityScriptProxy.Update"/> needs now that the entity, not the world, drives its
/// own pick/run pass (decision D2, docs/plan-conversion-totale.md §2): the shared bytecode interpreter,
/// the original's <c>g_activeCollisionEntity</c> (read by the pick phase to decide slot F), and the
/// two-argument <c>DestroyEntity</c> the pick phase's own destroy branches call. Implemented by
/// <see cref="AlundraWorldProxy"/> (which owns all three); every proxy this world spawns gets
/// <c>proxy.ScriptHost = this</c> in the shared spawn path (<see cref="AlundraWorldProxy.InitializeWithWorld"/>
/// / <see cref="AlundraWorldProxy.SpawnEntityByRecordId"/>). A proxy with a null <see cref="AlundraEntityScriptProxy.ScriptHost"/>
/// (never spawned through this world, e.g. a bare prefab instantiated in a unit test) skips its own
/// <see cref="AlundraEntityScriptProxy.Update"/> body entirely - see that method's own doc.
/// </summary>
public interface IAlundraScriptHost
{
    /// <summary>The shared event-program bytecode interpreter every spawned entity's pick/run pass and
    /// the world's own MapEvents pass run against - mirrors the original's single <c>_gameEngine</c>
    /// (there is exactly one interpreter per world, not one per entity).</summary>
    IEventProgramRunner Runner { get; }

    /// <summary>Port of the original global <c>g_activeCollisionEntity</c> - set by
    /// <c>AlundraPlayerManager.CheckEntityInteraction</c> on the interact frame (E12.d), consumed (and
    /// cleared) by the slot-F pick in <see cref="AlundraEntityScriptProxy.Update"/> - see D-E12D-4's
    /// consume-on-pick doc there. Settable since E12.d: the writer reaches it through this host.</summary>
    AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }

    /// <summary>Two-argument <c>GameEngine.DestroyEntity(Entity, int)</c> port the pick phase's own
    /// destroy branches (DestroyOnSlidingSlope/DestroyOnVramFlags) call - see
    /// <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy,int)"/>'s own doc.</summary>
    void DestroyEntity(AlundraEntityScriptProxy entity, int effectId);

    /// <summary>The shared game-flag/control-flag store (E2: also read by
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s own player branch for
    /// <see cref="AlundraGameState.PlayerControlBits.InputBlockedMask"/>, mirroring the original's single
    /// global <c>g_playerControlFlags</c>) - see <see cref="AlundraWorldProxy.GameState"/>'s own doc.</summary>
    AlundraGameState GameState { get; }

    /// <summary>
    /// The world's own <see cref="AlundraPlayerController"/> (E2), or null when no such controller
    /// possesses a pawn in this world (headless test harness, or a world with no player-startup settings) -
    /// see <see cref="AlundraWorldProxy"/>'s own implementation. <see cref="AlundraEntityScriptProxy.Update"/>'s
    /// player branch treats a null controller as "no input available this frame" (no-op), never a
    /// fallback spawn or a thrown exception.
    /// </summary>
    AlundraPlayerController? PlayerController { get; }

    /// <summary>
    /// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): this frame's collidable-entity snapshot
    /// (<see cref="EntitySupport.BuildCollidables"/>'s own criteria, <c>EntityManager.cs:994</c>) - shared,
    /// allocation-free, rebuilt once per frame by whichever host owns the spawned-entity list
    /// (<c>AlundraWorldProxy</c> in production, <c>HeadlessIntroSimulation</c> in the intro trace harness).
    /// Consumed by <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>, called once per entity per
    /// frame from <see cref="AlundraEntityScriptProxy.Update"/>. Never mutated by a reader - only ever
    /// rebuilt in place by its owner.
    /// </summary>
    IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; }

    /// <summary>
    /// Bug fix (user-reported runtime pacing bug - script logic was running at rendered-frame rate
    /// instead of the original's fixed 50 Hz, see <see cref="AlundraLogicClock"/>'s own class doc for the
    /// full diagnosis): this frame's logic-tick count, computed once per frame and shared by every caller
    /// (per-frame memo - see <see cref="AlundraLogicClock.TicksThisFrame"/>). <see cref="AlundraEntityScriptProxy.Update"/>
    /// calls this to gate its own pick/run pass and <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>;
    /// <see cref="AlundraWorldProxy.Update"/> calls it again (reading the SAME cached value, since every
    /// entity's own <c>Update</c> already ran this frame) to gate <see cref="AlundraWorldProxy.RunMapEventsPass"/>/
    /// <see cref="AlundraWorldProxy.RunPendingEventTriggers"/>, then closes the frame. Implemented by
    /// <see cref="AlundraWorldProxy"/> and the intro trace harness's own <c>HeadlessIntroSimulation</c>,
    /// each owning exactly one <see cref="AlundraLogicClock"/> instance.
    /// </summary>
    int LogicTicksThisFrame(float elapsedTime);

    /// <summary>
    /// T3 (docs/plan-transitions-carte.md §1.1/§3): this world's own parsed "Portals" object-layer
    /// records (see <see cref="AlundraWorldProxy.BuildPortals"/>), in slot order - the exact list
    /// <see cref="AlundraPortalScanner.FindPortalAtTile"/> scans. Default-implemented as an empty list
    /// so every host built for an unrelated slice/test (none of them portal-aware) needs no change to
    /// keep implementing this interface - only <see cref="AlundraWorldProxy"/> overrides it with real
    /// data.
    /// </summary>
    IReadOnlyList<AlundraPortalRecord> Portals => Array.Empty<AlundraPortalRecord>();

    /// <summary>
    /// T3's own seam for T4 (docs/plan-transitions-carte.md §3: "le prédicat ... exposé par une couture
    /// que T4 remplira"). Called by <see cref="AlundraPlayerManager.MovePlayer"/>, at most once per
    /// call, exactly when <see cref="AlundraPortalTrigger.TryGetTrigger"/> returns non-null - the moment
    /// the original's <c>CheckAndExecuteWarp</c> would have called <c>HandleWarpTransition</c>. Default
    /// no-op here: T3 DETECTS, it does not ACT (no fade, no <c>SetWorldToLoad</c> request) - see
    /// <see cref="AlundraPortalTrigger"/>'s own class doc. T4's <c>AlundraWarpDirector</c> overrides this
    /// on <see cref="AlundraWorldProxy"/> to actually start the departure sequence.
    /// </summary>
    void OnPortalTriggerDetected(AlundraPortalRecord portal, uint arrivalDirectionId)
    {
    }
}
