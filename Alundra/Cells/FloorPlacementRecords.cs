#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using Alundra.Maps.Loop;

namespace Alundra.Cells;

/// <summary>
/// One baked FLOOR tile's placement - the "AlundraFloorPlacements" custom property counterpart to
/// <see cref="WallPlacementRecords"/> (see
/// <c>AlundraCasaEngineProjectConverter.Writers.WallPlacementReplayer.FloorPlacementDocument</c> and
/// <c>docs/formats/cells-companion.md</c>). Most entries are elevated (Height &gt; 0) floors, recorded
/// unconditionally; a Height-0 (ground-level) floor also appears here whenever it shares its bake
/// position with another placement - the CLOSURE rule (see the converter's <c>WallPlacementReplayer</c>
/// class doc) - so parsing and the strip/resubmit below make no assumption about which case a given
/// entry is: the key formula (<c>CellY*16 + clamp(DepthSlot,0,5)</c>, see
/// <see cref="ComputeFloorSortKey"/>) is Height-independent already. No <c>StackIndex</c> column: each
/// cell has exactly one floor tile, so (CellX, CellY) alone identifies it.
/// </summary>
public sealed class FloorPlacementRecords
{
    public int MapIndex { get; init; }
    public int Count { get; init; }
    public int[] CellX { get; init; } = Array.Empty<int>();
    public int[] CellY { get; init; } = Array.Empty<int>();
    public int[] Plane { get; init; } = Array.Empty<int>();
    public int[] X { get; init; } = Array.Empty<int>();
    public int[] Y { get; init; } = Array.Empty<int>();
    public int[] Gid { get; init; } = Array.Empty<int>();
    public int[] DepthSlot { get; init; } = Array.Empty<int>();
}
