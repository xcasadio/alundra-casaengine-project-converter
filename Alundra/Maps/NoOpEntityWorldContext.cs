#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;
using Alundra.Events;
using Alundra.Entities;

namespace Alundra.Maps;

/// <summary>
/// V1 default <see cref="IEntityWorldContext"/>: no entities, every spawn/destroy call is a logged no-op.
/// Used when an <see cref="EventProgramRunner"/> is constructed without a real context (e.g. most
/// synthetic interpreter tests, which do not exercise the search/manipulation opcodes).
/// </summary>
public sealed class NoOpEntityWorldContext : IEntityWorldContext
{
    public static readonly NoOpEntityWorldContext Instance = new();

    public IReadOnlyList<EntityScriptProxy> SpawnedEntities { get; } = System.Array.Empty<EntityScriptProxy>();

    public EntityScriptProxy? PlayerEntity => null;

    public EntityScriptProxy? EntityFollowedByCamera { get; set; }

    public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;

    public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;

    public void DestroyEntity(EntityScriptProxy entity)
    {
        // No-op: no world to mutate.
    }

    public NavigationGrid2D? NavigationGrid => null;
}
