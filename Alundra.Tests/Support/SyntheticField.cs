#nullable enable
using System;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.f4 (docs/plan-e20-heros.md section 1.20, "Revision au HEAD" point 10 and the bullet (c8)): puts the synthetic field of an oracle scenario into the REAL world of an arc (map 14, the real prefabs and controllers). The values of the oracles of E20.f4 to E20.f6
/// come from a flat synthetic field (<c>docs/plan-e20-annexe/e20-f3/oracle/tools/world.py</c>: a wall in the column 15, a step in the column 8, a strip of grass), so the tests put the same field by reflection, as <c>MapEdgeWorldTests</c> does; the field and the cell store
/// are those of <see cref="CutBench"/> (ONE set of records, the arrays aliased), so a cut by the world's <see cref="Alundra.Cells.ICellMutator"/> clears the bit the field reads on the same cell (the production pairing of <c>WorldProxy.InstallCellAndOverlaySystems</c>).
/// The visual synchronisation of the real map is not wired to this store (no overlay is rebuilt on this bench).
/// </summary>
internal static class SyntheticField
{
    /// <summary>
    /// Replaces <see cref="WorldProxy.CollisionField"/> (and the field of the engine's world) and <see cref="WorldProxy.CellMutator"/> of <paramref name="arc"/> by the 52 x 60 cells that <paramref name="cell"/> gives (default: all zero) and returns the bench that holds them.
    /// </summary>
    public static CutBench Install(ArcRun arc, Func<int, int, CutBench.Cell>? cell)
    {
        var bench = CutBench.Create(cell);
        typeof(WorldProxy).GetProperty(nameof(WorldProxy.CollisionField))!.SetValue(arc.Proxy, bench.Field);
        arc.RealWorld!.CollisionField = bench.Field;
        typeof(WorldProxy).GetProperty(nameof(WorldProxy.CellMutator))!.SetValue(arc.Proxy, bench.Store);
        return bench;
    }
}
