namespace Alundra.Scripts;

/// <summary>
/// Seam between the entity event status machine (<see cref="AlundraWorldProxy"/>, ported from
/// <c>EntityManager.UpdateEntitiesEvents</c> @ 0x800386D0) and the actual event-program bytecode
/// interpreter, which is a later chantier. The original "run" phase calls one of two callees depending
/// on whether the picked program slot resolves to a real bytecode program index (<c>RunScript</c>) or
/// falls back to the sprite-level AI table (<c>RunSpriteEvent</c>, <c>g_entityEventFunctionsByType</c>)
/// - this interface keeps that distinction so a future interpreter can implement both.
/// </summary>
public interface IEventProgramRunner
{
    /// <summary>
    /// Runs the bytecode program at <paramref name="programSlot"/> (one of the <c>ScriptHelper.Program*</c>
    /// slots) for <paramref name="entity"/>. Mirrors the original's <c>_gameEngine.RunScript(entity, entity.EventTrigger)</c>.
    /// </summary>
    void RunScript(AlundraEntityScriptProxy entity, int programSlot);

    /// <summary>
    /// Runs the sprite-level AI event for <paramref name="entity"/> (picked program index masked to 0).
    /// Mirrors the original's <c>_gameEngine.RunSpriteEvent(entity)</c>.
    /// </summary>
    void RunSpriteEvent(AlundraEntityScriptProxy entity);
}

/// <summary>
/// V1 implementation of <see cref="IEventProgramRunner"/>: the bytecode interpreter does not exist yet,
/// so this is a silent no-op - it only counts invocations (useful for logs/tests). Deliberately never
/// logs per-frame, to avoid log spam once real entities start ticking every frame.
/// </summary>
public sealed class NoOpEventProgramRunner : IEventProgramRunner
{
    public int ScriptRunCount { get; private set; }
    public int SpriteEventRunCount { get; private set; }

    public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
    {
        ScriptRunCount++;
    }

    public void RunSpriteEvent(AlundraEntityScriptProxy entity)
    {
        SpriteEventRunCount++;
    }
}
