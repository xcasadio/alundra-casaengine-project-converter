#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;

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
    /// The New Game hero entity (port of <c>ResetEntityState</c>, GameEngine.cs:648-670 - see
    /// <see cref="AlundraWorldProxy.PlayerEntity"/>'s own doc), or null when this world spawned none (no
    /// hero asset in the catalog, no prefab loader, etc. - see that same doc). Needed by
    /// <see cref="AlundraEventProgramRunner.RunScript"/>'s slot F policy (EntityEventHandlers.cs:268-273:
    /// slot F always zeroes the PLAYER's own forces, not the entity being run).
    /// </summary>
    AlundraEntityScriptProxy? PlayerEntity { get; }

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

    /// <summary>
    /// This world's navigation grid (E4.d, docs/plan-e4-deplacement-scripte.md decision E4-2), built once
    /// by <see cref="AlundraWorldProxy.InitializeWithWorld"/> right after <c>World.CollisionField</c> from
    /// the SAME <c>TileMapData</c>, in "cell space" (<c>cellSize = 1</c>) - the DLL, not the engine's own
    /// <c>CharacterControllerNavigationDriverComponent</c> (not used in E4), does its own px&lt;-&gt;cell
    /// conversion (see <see cref="AlundraEventProgramRunner"/>'s own 0x1E walk-detour helpers). Null in
    /// degraded mode (missing navigation layer/tileset, or a world with no tilemap at all) - every reader
    /// treats that as "no detour available, keep pushing" (0x1E's own original behavior). Settable by
    /// fakes so a test can inject a synthetic grid without a live <see cref="AlundraWorldProxy"/> (map 389
    /// itself has 0 blocked navigation cells - E4.a's own finding - so an obstacle test needs one).
    /// </summary>
    NavigationGrid2D? NavigationGrid { get; }
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

    public AlundraEntityScriptProxy? PlayerEntity => null;

    public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

    public void DestroyEntity(AlundraEntityScriptProxy entity)
    {
        // No-op: no world to mutate.
    }

    public NavigationGrid2D? NavigationGrid => null;
}
