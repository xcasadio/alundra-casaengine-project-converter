#nullable enable
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

    /// <summary>Port of the original global <c>g_activeCollisionEntity</c> - see
    /// <see cref="AlundraWorldProxy.ActiveCollisionEntity"/>'s own doc for what sets it (nothing yet, V1).</summary>
    AlundraEntityScriptProxy? ActiveCollisionEntity { get; }

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
}
