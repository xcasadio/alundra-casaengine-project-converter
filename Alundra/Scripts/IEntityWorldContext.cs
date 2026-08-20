#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// Seam over the world-level entity services the entity search/manipulation opcodes need
/// (0x2D ActivateEntity, 0x2E DestroyEntity, 0x62/0x63/0x64/0x65/0xAC, all of which fan out through
/// <see cref="EntitySearchService"/>). Implemented by <see cref="AlundraWorldProxy"/> (which owns
/// <c>_spawnedEntities</c> and the live <c>World</c>); <see cref="AlundraEventProgramRunner"/> depends on
/// this interface rather than the concrete world proxy, so the interpreter stays unit-testable with a
/// fake context instead of a live <c>World</c>.
/// </summary>
public interface IEntityWorldContext
{
    /// <summary>
    /// Every entity this world has spawned so far this session, in creation order - mirrors the
    /// original's flat <c>g_entitySlots</c> array (see <see cref="EntitySearchService"/>'s class doc).
    /// A snapshot taken fresh on each read: an entity dynamically spawned by 0x2D earlier in the same
    /// script call is visible to a following search within that same call, exactly like the original's
    /// live array.
    /// </summary>
    IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; }

    /// <summary>
    /// Dynamic spawn by entity-record id - backs opcode 0x2D (Script_45_02D), which always calls the
    /// original's <c>GameEngine.SpawnEntity(logicEntity, entityRecordId, notCheckSpawnZone: 1)</c>.
    /// Returns null when the record is disabled/missing or the spawn otherwise fails (prefab loader
    /// unavailable, etc.) - the original breakpoints (debug-only trap) in that case instead.
    /// </summary>
    AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId);

    /// <summary>
    /// Marks <paramref name="entity"/> for destruction - backs the single-argument
    /// <c>GameEngine.DestroyEntity(Entity)</c> @ 0x8003A774 every search-driven destroy opcode (0x2E)
    /// calls per match, distinct from <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy,int)"/>
    /// (the two-argument overload the pick-phase status machine uses, which also spawns break-effect
    /// contents).
    /// </summary>
    void DestroyEntity(AlundraEntityScriptProxy entity);
}

/// <summary>
/// V1 default <see cref="IEntityWorldContext"/>: no entities, every spawn/destroy call is a logged no-op.
/// Used when an <see cref="AlundraEventProgramRunner"/> is constructed without a real context (e.g. most
/// synthetic interpreter tests, which do not exercise the search/manipulation opcodes).
/// </summary>
public sealed class NoOpEntityWorldContext : IEntityWorldContext
{
    public static readonly NoOpEntityWorldContext Instance = new();

    public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = System.Array.Empty<AlundraEntityScriptProxy>();

    public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

    public void DestroyEntity(AlundraEntityScriptProxy entity)
    {
        // No-op: no world to mutate.
    }
}
