#nullable enable
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
}
