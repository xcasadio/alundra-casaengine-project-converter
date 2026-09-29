#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// E16.c C1 (docs/plan-e16-etat-partie.md): what <see cref="AlundraSaveGame.TryValidate"/> consults to bound a
/// save's values (C3, C4) - all of it project data, none of it read from the save:
/// <list type="bullet">
/// <item><description>the map table, <c>Maps/world-index.json</c> of <see cref="ProjectPath"/>
/// (<see cref="WorldIndex"/>): which map ids exist, and where each world lives;</description></item>
/// <item><description>the project folder itself, where <see cref="AlundraMapSizeReader"/> finds each map's
/// <c>.tileMap</c> (D-E16-32);</description></item>
/// <item><description>whether a world is in the asset catalog (<see cref="IsWorldInCatalog"/>), an injected
/// predicate over a <c>world-index.json</c> value - production wires it to <c>AssetCatalog.GetByFileName</c> in
/// E16.d, tests pass their own;</description></item>
/// <item><description>the item table (<see cref="ItemTables"/>): the per-item ceiling of the counters,
/// <c>ItemsProperties[id * 5 + 3]</c>. Pass the SAME instance the New Game inventory uses, so a capture of
/// that inventory validates against the table that filled it.</description></item>
/// </list>
/// </summary>
public sealed class AlundraSaveGameRules
{
    /// <param name="projectPath">The project folder: <c>Maps/world-index.json</c> and the maps' <c>.tileMap</c>
    /// files are read under it.</param>
    /// <param name="isWorldInCatalog">True when the world at a <c>world-index.json</c> value is in the asset
    /// catalog. An exception it throws becomes a refusal (C4).</param>
    /// <param name="itemTables">The item table the counters are bounded by.</param>
    public AlundraSaveGameRules(string projectPath, Func<string, bool> isWorldInCatalog, AlundraItemTables itemTables)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(isWorldInCatalog);
        ArgumentNullException.ThrowIfNull(itemTables);

        ProjectPath = projectPath;
        WorldIndex = new AlundraWorldIndexTable(projectPath);
        IsWorldInCatalog = isWorldInCatalog;
        ItemTables = itemTables;
    }

    /// <summary>The project folder the map table and the <c>.tileMap</c> files are read from.</summary>
    public string ProjectPath { get; }

    /// <summary><c>Maps/world-index.json</c> of <see cref="ProjectPath"/> (degraded mode: no key resolves, so
    /// every save is refused on <c>initialMapId</c>).</summary>
    public AlundraWorldIndexTable WorldIndex { get; }

    /// <summary>The catalog predicate, over a <c>world-index.json</c> value.</summary>
    public Func<string, bool> IsWorldInCatalog { get; }

    /// <summary>The item table the counters are bounded by (F11: in degraded mode every ceiling is 0).</summary>
    public AlundraItemTables ItemTables { get; }
}
