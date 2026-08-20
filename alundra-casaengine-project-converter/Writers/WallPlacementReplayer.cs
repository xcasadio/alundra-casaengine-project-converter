using System.Globalization;
using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// One baked WALL tile's placement in the renderer-ordered flat tile planes ("Render_0",
/// "Render_1", ...) that <c>TiledMapExporter.CreateRendererOrderedTileLayers</c> (analyser sources,
/// AlundraTools/AlundraDataExtractor/TiledMapExporter.cs:287-358) packed it into, plus the PSX
/// depth slot the original renderer would have looked up for it
/// (AlundraTools/AlundraEngine/Graphics/GraphicManager.cs:287,313-321). Columnar (one array per
/// field) for the same reason <see cref="CellMetadataDocument"/> is: this is embedded as a single
/// TileMapData custom property string, so repeating field names per placement would multiply the
/// serialized size for nothing.
/// </summary>
public sealed class WallPlacementDocument
{
    public int MapIndex { get; set; }
    public int Count { get; set; }
    public int[] CellX { get; set; } = Array.Empty<int>();
    public int[] CellY { get; set; } = Array.Empty<int>();
    public int[] StackIndex { get; set; } = Array.Empty<int>();
    public int[] Plane { get; set; } = Array.Empty<int>();
    public int[] X { get; set; } = Array.Empty<int>();
    public int[] Y { get; set; } = Array.Empty<int>();
    public int[] Gid { get; set; } = Array.Empty<int>();
    public int[] DepthSlot { get; set; } = Array.Empty<int>();
}

public sealed class WallPlacementReplayResult
{
    public WallPlacementDocument Document { get; init; } = new();

    /// <summary>Number of wall tile stacks the replay walked (one per non-empty cell in the sparse
    /// wall-tile table) - must always equal Cells.WallTileStacks, since both count the exact same
    /// dictionary.</summary>
    public int StacksCovered { get; init; }

    /// <summary>Sum, over every stack, of tiles that are non-empty and land in-bounds - computed by
    /// an independent pass so it can catch a bug in the main placement loop rather than restate it.
    /// Must equal <see cref="WallPlacementDocument.Count"/>.</summary>
    public int ExpectedEmitted { get; init; }

    /// <summary>Elevated (Height > 0) floor tile placements - see <see cref="FloorPlacementDocument"/>'s
    /// class doc for why only elevated floors are recorded.</summary>
    public FloorPlacementDocument FloorDocument { get; init; } = new();

    /// <summary>Independent recount of how many Height&gt;0 floor tiles should have been emitted -
    /// non-empty and in-bounds after the Height offset alone (no stack/offset math, floors have
    /// neither) - mirrors <see cref="ExpectedEmitted"/>'s role for walls. <see cref="FloorPlacementDocument.Count"/>
    /// must equal this plus <see cref="FloorConflictEmitted"/> (the closure additions below).</summary>
    public int FloorExpectedEmitted { get; init; }

    /// <summary>
    /// Number of additional (Height 0) floor placements the CLOSURE pass promoted: for every bake
    /// position that already holds at least one placement (a wall tile or an elevated floor), every
    /// other baked tile still occupying that same (x, y) across the other planes must become a
    /// placement too, or the two would draw inconsistently - one true-depth-sorted, the other stuck
    /// in bake-plane order (see the class doc and docs/formats/cells-companion.md's closure section).
    /// These are always Height 0 floors: walls and Height&gt;0 floors are unconditionally placements
    /// already, so nothing else can be left over - <see cref="ResidualConflicts"/> is the hard-error
    /// safety net if that assumption is ever violated.
    /// </summary>
    public int FloorConflictEmitted { get; init; }

    /// <summary>
    /// Bake positions where, even after the closure pass above, a non-empty baked tile on some plane
    /// still coexists with a placement at the same (x, y) on another plane - each entry is
    /// (Plane, X, Y) of the leftover tile. Must always be empty; a non-empty result means the closure
    /// invariant is violated and the map must fail the export (see <see cref="CellMetadataWriter"/>).
    /// </summary>
    public IReadOnlyList<(int Plane, int X, int Y)> ResidualConflicts { get; init; } = Array.Empty<(int, int, int)>();
}

/// <summary>
/// One baked FLOOR tile's placement in the renderer-ordered flat tile planes - recorded for every cell
/// with Height &gt; 0 (an elevated floor, e.g. an upper ship deck), PLUS every Height-0 (ground-level)
/// floor that the CLOSURE pass (see <see cref="WallPlacementReplayResult.FloorConflictEmitted"/>) had
/// to promote because it shares its bake position with another placement on a different plane.
///
/// Ground-level floors otherwise participate in the original's unified depth sort exactly like
/// elevated ones (<c>DepthFloor(cellY, slot) = cellY*16 + slot</c>,
/// AlundraTools/AlundraEngine/Graphics/GraphicManager.cs:275,324-347) but can never visibly diverge
/// from staying flat ON THEIR OWN: a floor only draws in front of an entity when the floor's own row
/// is south of (numerically greater than) the entity's row, and a Height-0 floor draws at its own
/// cell's screen row - so the only entities it could contend with are ones anchored on that same row
/// or further north, which the flat Ground pass already draws it behind or under correctly (see
/// <c>docs/formats/cells-companion.md</c> for the full argument, including the elevated case this
/// column exists to fix, and the closure section for why a Height-0 floor CAN still diverge once a
/// co-located tile of its own is pulled into the depth-sorted overlay). A bake position holding only
/// Height-0 floors (no placement at all - e.g. an open-sea stack) stays flat with all its tiles in
/// bake-plane order; this remains a documented, invisible approximation. No stack_index column: unlike
/// wall stacks, each cell has exactly one floor tile, so the (cell_x, cell_y) pair alone identifies it.
/// </summary>
public sealed class FloorPlacementDocument
{
    public int MapIndex { get; set; }
    public int Count { get; set; }
    public int[] CellX { get; set; } = Array.Empty<int>();
    public int[] CellY { get; set; } = Array.Empty<int>();
    public int[] Plane { get; set; } = Array.Empty<int>();
    public int[] X { get; set; } = Array.Empty<int>();
    public int[] Y { get; set; } = Array.Empty<int>();
    public int[] Gid { get; set; } = Array.Empty<int>();
    public int[] DepthSlot { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Replays the exact packing algorithm <c>TiledMapExporter.CreateRendererOrderedTileLayers</c> used
/// to bake floor and wall tiles into the map's flat "Render_*" tile planes, from the per-cell data
/// the converter already reads via <see cref="CellMetadataReader"/>. Iteration order, the
/// floor-then-walls-per-cell sequence, the first-free-plane rule and the empty/out-of-bounds drop
/// rules are all ported 1:1 from TiledMapExporter.cs:292-357 (AddRendererTile), so this produces the
/// same (plane, x, y, gid) as the exporter for every wall tile that made it onto a plane. Floor
/// tiles are replayed too - purely to keep plane occupancy in sync with the exporter, since floors
/// and walls compete for the same first-free-plane slots - but are not recorded in the output
/// (<see cref="WallPlacementDocument"/> only carries WALL tile placements).
/// </summary>
public static class WallPlacementReplayer
{
    // Matches TiledMapExporter's own EmptyTileId (map_N.json / the companion both use the PSX
    // "no tile" sentinel 0xffff for both floor TileId and wall stack Tiles entries).
    private const int EmptyTileId = 0xffff;

    // GraphicManager.GetTileDepthSlot: tileIndex = tileMapIndex & 0x3ff, then
    // g_tileAnimDescriptorTable[tileIndex].SpriteIndex (0 if tileIndex is out of the table's range).
    // GameInitializer.CreateTileAnimDescriptors builds that 960-entry table purely algorithmically,
    // independently of any per-map data or of its own (unused) parameter: it fills 6 buckets of 160
    // consecutive entries (spriteIndex 0..5), so SpriteIndex is simply tileIndex / 160, clamped to 0
    // once tileIndex reaches 960. There is no per-map table to look up - the "table" is one fixed
    // function of the raw tile id, reproduced here directly.
    private const int TileIndexMask = 0x3ff;
    private const int TileAnimDescriptorCount = 960;
    private const int TileAnimDescriptorBucketSize = 160;

    public static int ComputeDepthSlot(int rawTileId)
    {
        var tileIndex = rawTileId & TileIndexMask;
        return tileIndex < TileAnimDescriptorCount ? tileIndex / TileAnimDescriptorBucketSize : 0;
    }

    public static WallPlacementReplayResult Replay(
        int mapIndex,
        int width,
        int height,
        IReadOnlyList<int> floorRawTileId,
        IReadOnlyList<int> cellHeight,
        IReadOnlyDictionary<string, WallTileStack> wallTilesByCellIndex,
        IReadOnlyDictionary<int, int> gidByRawTileId)
    {
        var layerDataByPlane = new List<int[]>();

        var cellX = new List<int>();
        var cellY = new List<int>();
        var stackIndexList = new List<int>();
        var planeList = new List<int>();
        var xList = new List<int>();
        var yList = new List<int>();
        var gidList = new List<int>();
        var depthSlotList = new List<int>();

        var floorCellX = new List<int>();
        var floorCellY = new List<int>();
        var floorPlaneList = new List<int>();
        var floorXList = new List<int>();
        var floorYList = new List<int>();
        var floorGidList = new List<int>();
        var floorDepthSlotList = new List<int>();

        // Every non-dropped baked floor tile (elevated or not), tracked purely so the CLOSURE pass
        // below can find the Height-0 ones co-located with a placement - see FloorConflictEmitted's
        // doc. Elevated floors are already added to the placement lists above at insertion time; this
        // list additionally remembers their owner cell so closure can recognize "already a placement"
        // positions without re-deriving them from the four column lists.
        var floorOccupancy = new List<FloorOccupancy>();

        var stacksCovered = 0;

        for (var sourceY = 0; sourceY < height; sourceY++)
        {
            for (var sourceX = 0; sourceX < width; sourceX++)
            {
                var index = sourceY * width + sourceX;

                // Floor tile of this cell competes for planes first, exactly like the exporter's own
                // per-cell order (TiledMapExporter.cs:298-301 runs before :303-319).
                var floorRawId = floorRawTileId[index];
                if (floorRawId != EmptyTileId)
                {
                    var floorTargetY = sourceY - cellHeight[index];
                    var floorGid = ResolveGid(floorRawId, gidByRawTileId);
                    var floorPlane = PlaceTile(layerDataByPlane, width, height, sourceX, floorTargetY, floorGid);

                    if (floorPlane >= 0)
                    {
                        var isElevated = cellHeight[index] > 0;
                        var floorDepthSlot = ComputeDepthSlot(floorRawId);

                        // Only elevated (Height > 0) floors are recorded here unconditionally - see
                        // FloorPlacementDocument's class doc for why Height-0 floors can otherwise stay
                        // flat with no observable divergence. The CLOSURE pass below promotes the
                        // Height-0 ones that turn out to share a bake position with a placement.
                        if (isElevated)
                        {
                            floorCellX.Add(sourceX);
                            floorCellY.Add(sourceY);
                            floorPlaneList.Add(floorPlane);
                            floorXList.Add(sourceX);
                            floorYList.Add(floorTargetY);
                            floorGidList.Add(floorGid);
                            floorDepthSlotList.Add(floorDepthSlot);
                        }

                        floorOccupancy.Add(new FloorOccupancy(
                            sourceX, sourceY, floorPlane, sourceX, floorTargetY, floorGid, floorDepthSlot, isElevated));
                    }
                }

                if (!wallTilesByCellIndex.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out var stack))
                {
                    continue;
                }

                stacksCovered++;

                for (var stackIndex = 0; stackIndex < stack.Tiles.Length; stackIndex++)
                {
                    var wallRawId = stack.Tiles[stackIndex];
                    if (wallRawId == EmptyTileId)
                    {
                        continue;
                    }

                    var targetX = sourceX;
                    var targetY = sourceY - cellHeight[index] - stack.Offset + stackIndex + 1;

                    // Resolved unconditionally, in bounds or not - AddRendererTile's own call site
                    // resolves the gid as an argument before the bounds check runs inside it
                    // (TiledMapExporter.cs:318), so a raw id absent from the tileset's catalog is a
                    // real error regardless of where the tile lands.
                    var gid = ResolveGid(wallRawId, gidByRawTileId);
                    var plane = PlaceTile(layerDataByPlane, width, height, targetX, targetY, gid);

                    if (plane < 0)
                    {
                        continue; // dropped, matches AddRendererTile's bounds/gid==0 check - never recorded.
                    }

                    cellX.Add(sourceX);
                    cellY.Add(sourceY);
                    stackIndexList.Add(stackIndex);
                    planeList.Add(plane);
                    xList.Add(targetX);
                    yList.Add(targetY);
                    gidList.Add(gid);
                    depthSlotList.Add(ComputeDepthSlot(wallRawId));
                }
            }
        }

        var expectedEmitted = CountNonEmptyInBoundsWallTiles(width, height, cellHeight, wallTilesByCellIndex);
        var floorExpectedEmitted = CountElevatedInBoundsFloorTiles(width, height, floorRawTileId, cellHeight);

        // CLOSURE: every bake position holding at least one placement (a wall tile above, or an
        // elevated floor just added above) must have ALL of its co-located baked tiles - on every
        // plane - become placements too, or the position renders inconsistently: some planes
        // depth-sorted through the runtime overlay, others stuck drawing flat in bake-plane order
        // (the exporter's first-free-plane ENCOUNTER order, not depth). See the class doc and
        // docs/formats/cells-companion.md.
        var placementPositions = new HashSet<(int X, int Y)>(xList.Count + floorXList.Count);
        for (var i = 0; i < xList.Count; i++)
        {
            placementPositions.Add((xList[i], yList[i]));
        }
        for (var i = 0; i < floorXList.Count; i++)
        {
            placementPositions.Add((floorXList[i], floorYList[i]));
        }

        var floorConflictEmitted = 0;
        foreach (var occupancy in floorOccupancy)
        {
            if (occupancy.IsElevated || !placementPositions.Contains((occupancy.X, occupancy.Y)))
            {
                continue;
            }

            floorCellX.Add(occupancy.CellX);
            floorCellY.Add(occupancy.CellY);
            floorPlaneList.Add(occupancy.Plane);
            floorXList.Add(occupancy.X);
            floorYList.Add(occupancy.Y);
            floorGidList.Add(occupancy.Gid);
            floorDepthSlotList.Add(occupancy.DepthSlot);
            floorConflictEmitted++;
        }

        // HARD INVARIANT: after the closure pass above, no bake position that holds a placement may
        // still have a leftover non-empty tile, on any plane, that is not itself a placement. Walls
        // and elevated floors are always placements already, and the closure pass just promoted every
        // co-located Height-0 floor, so this can only fire on a logic regression - never expected to
        // find anything on a converter-produced map.
        var placementPlanes = new HashSet<(int Plane, int X, int Y)>(xList.Count + floorXList.Count);
        for (var i = 0; i < xList.Count; i++)
        {
            placementPlanes.Add((planeList[i], xList[i], yList[i]));
        }
        for (var i = 0; i < floorXList.Count; i++)
        {
            placementPlanes.Add((floorPlaneList[i], floorXList[i], floorYList[i]));
        }

        var residualConflicts = FindResidualConflicts(layerDataByPlane, width, placementPositions, placementPlanes);

        var document = new WallPlacementDocument
        {
            MapIndex = mapIndex,
            Count = cellX.Count,
            CellX = cellX.ToArray(),
            CellY = cellY.ToArray(),
            StackIndex = stackIndexList.ToArray(),
            Plane = planeList.ToArray(),
            X = xList.ToArray(),
            Y = yList.ToArray(),
            Gid = gidList.ToArray(),
            DepthSlot = depthSlotList.ToArray(),
        };

        var floorDocument = new FloorPlacementDocument
        {
            MapIndex = mapIndex,
            Count = floorCellX.Count,
            CellX = floorCellX.ToArray(),
            CellY = floorCellY.ToArray(),
            Plane = floorPlaneList.ToArray(),
            X = floorXList.ToArray(),
            Y = floorYList.ToArray(),
            Gid = floorGidList.ToArray(),
            DepthSlot = floorDepthSlotList.ToArray(),
        };

        return new WallPlacementReplayResult
        {
            Document = document,
            StacksCovered = stacksCovered,
            ExpectedEmitted = expectedEmitted,
            FloorDocument = floorDocument,
            FloorExpectedEmitted = floorExpectedEmitted,
            FloorConflictEmitted = floorConflictEmitted,
            ResidualConflicts = residualConflicts,
        };
    }

    /// <summary>One baked floor tile's occupancy, tracked regardless of Height so the CLOSURE pass in
    /// <see cref="Replay"/> can find the Height-0 ones sharing a bake position with a placement.</summary>
    private readonly record struct FloorOccupancy(
        int CellX, int CellY, int Plane, int X, int Y, int Gid, int DepthSlot, bool IsElevated);

    /// <summary>
    /// Scans every plane at every position that holds at least one placement for a leftover non-empty
    /// tile that is not itself a placement - the hard invariant backing <see
    /// cref="WallPlacementReplayResult.ResidualConflicts"/>. Exposed internally so it can be exercised
    /// directly with a crafted plane grid, independent of a full <see cref="Replay"/> call.
    /// </summary>
    public static List<(int Plane, int X, int Y)> FindResidualConflicts(
        IReadOnlyList<int[]> layerDataByPlane,
        int width,
        IReadOnlySet<(int X, int Y)> placementPositions,
        IReadOnlySet<(int Plane, int X, int Y)> placementPlanes)
    {
        var residuals = new List<(int Plane, int X, int Y)>();

        foreach (var (x, y) in placementPositions)
        {
            var targetIndex = y * width + x;
            for (var plane = 0; plane < layerDataByPlane.Count; plane++)
            {
                if (layerDataByPlane[plane][targetIndex] != 0 && !placementPlanes.Contains((plane, x, y)))
                {
                    residuals.Add((plane, x, y));
                }
            }
        }

        return residuals;
    }

    /// <summary>
    /// Independent recount of how many wall tiles should have been emitted - non-empty and in
    /// bounds - kept deliberately separate from the placement loop above (no shared accumulator) so
    /// it can catch a bug in that loop instead of merely restating it.
    /// </summary>
    private static int CountNonEmptyInBoundsWallTiles(
        int width,
        int height,
        IReadOnlyList<int> cellHeight,
        IReadOnlyDictionary<string, WallTileStack> wallTilesByCellIndex)
    {
        var count = 0;

        foreach (var (key, stack) in wallTilesByCellIndex)
        {
            var index = int.Parse(key, CultureInfo.InvariantCulture);
            var sourceX = index % width;
            var sourceY = index / width;

            for (var stackIndex = 0; stackIndex < stack.Tiles.Length; stackIndex++)
            {
                if (stack.Tiles[stackIndex] == EmptyTileId)
                {
                    continue;
                }

                var targetY = sourceY - cellHeight[index] - stack.Offset + stackIndex + 1;
                if (sourceX >= 0 && sourceX < width && targetY >= 0 && targetY < height)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Independent recount of how many elevated (Height &gt; 0) floor tiles should have been
    /// emitted - non-empty and in-bounds after applying the Height offset alone - kept deliberately
    /// separate from the placement loop above, mirroring <see cref="CountNonEmptyInBoundsWallTiles"/>'s
    /// role for walls.
    /// </summary>
    private static int CountElevatedInBoundsFloorTiles(
        int width, int height, IReadOnlyList<int> floorRawTileId, IReadOnlyList<int> cellHeight)
    {
        var count = 0;

        for (var sourceY = 0; sourceY < height; sourceY++)
        {
            for (var sourceX = 0; sourceX < width; sourceX++)
            {
                var index = sourceY * width + sourceX;
                if (floorRawTileId[index] == EmptyTileId || cellHeight[index] <= 0)
                {
                    continue;
                }

                var targetY = sourceY - cellHeight[index];
                if (targetY >= 0 && targetY < height)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int ResolveGid(int rawTileId, IReadOnlyDictionary<int, int> gidByRawTileId)
    {
        if (!gidByRawTileId.TryGetValue(rawTileId, out var gid))
        {
            throw new InvalidOperationException(
                $"raw tile id {rawTileId} has no known gid in the tileset (missing 'TileId' tile property).");
        }

        return gid;
    }

    /// <summary>
    /// Ports AddRendererTile (TiledMapExporter.cs:338-358): first plane whose target cell is still
    /// empty (0) gets the gid; if every existing plane's target cell is already taken, a new plane is
    /// appended and used. Out-of-bounds targets are the caller's responsibility (checked before this
    /// is called for wall tiles, folded into the bounds check below for the floor-tile call site).
    /// </summary>
    private static int PlaceTile(List<int[]> layerDataByPlane, int width, int height, int targetX, int targetY, int gid)
    {
        if (gid == 0 || targetX < 0 || targetX >= width || targetY < 0 || targetY >= height)
        {
            return -1;
        }

        var targetIndex = targetY * width + targetX;
        for (var plane = 0; plane < layerDataByPlane.Count; plane++)
        {
            if (layerDataByPlane[plane][targetIndex] == 0)
            {
                layerDataByPlane[plane][targetIndex] = gid;
                return plane;
            }
        }

        var newPlane = new int[width * height];
        newPlane[targetIndex] = gid;
        layerDataByPlane.Add(newPlane);
        return layerDataByPlane.Count - 1;
    }
}
