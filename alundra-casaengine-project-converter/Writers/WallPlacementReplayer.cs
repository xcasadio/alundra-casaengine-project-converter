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
                    PlaceTile(layerDataByPlane, width, height, sourceX, sourceY - cellHeight[index], ResolveGid(floorRawId, gidByRawTileId));
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

        return new WallPlacementReplayResult
        {
            Document = document,
            StacksCovered = stacksCovered,
            ExpectedEmitted = expectedEmitted,
        };
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
