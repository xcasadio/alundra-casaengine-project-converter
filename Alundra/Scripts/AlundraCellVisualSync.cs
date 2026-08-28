#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;

namespace Alundra.Scripts;

/// <summary>
/// E7.b (docs/plan-e7-mutation-tuiles.md): the visual + navigation half of "cell mutation at runtime" -
/// subscribed to <see cref="AlundraCellStore.CellsMutated"/> by <see cref="AlundraWorldProxy"/>'s own
/// installation method, this is what makes 0x54/0x55/0x85 actually redraw the map and re-route
/// navigation instead of only mutating data E7.a otherwise leaves inert (that class doc's own "assumed
/// gap"). One instance per world.
///
/// LIVE MODEL (Conception, plan): a per-cell dictionary of the overlay contributions this world's runtime
/// sorted overlay currently carries, seeded once at construction from exactly what
/// <see cref="WallPlacementOverlay.Apply"/>/<see cref="WallPlacementOverlay.ApplyFloor"/> ACTUALLY
/// resubmitted (their own return value, fact 13 - not the raw placement documents, which are a superset:
/// seeding from the documents would resubmit a mismatched entry whose flat tile was never stripped,
/// double-drawing it the first time a mutation forces a reconstruction). On a mutated cell, this class
/// re-derives that cell's floor/wall contributions from the SAME <see cref="AlundraCellStore"/> the
/// opcodes just wrote into and compares them field-by-field against the model; only a real difference
/// marks the overlay dirty. <see cref="FlushPendingOverlayReconstruction"/> - called once per rendered
/// frame by <see cref="AlundraWorldProxy.Update"/>, coalescing every mutation dispatched that frame (a
/// map's four-hatch entry alone fires four separate <c>CopyCellRectangle</c> calls) - is the only place
/// that ever calls <see cref="TileMapComponent.ClearSortedOverlayTiles"/>/
/// <see cref="TileMapComponent.AddSortedOverlayTile"/> again after construction: there is no unitary
/// removal in the engine (plan fact 8), so a dirty frame always clears and resubmits the WHOLE model.
///
/// Positions/keys (plan facts 2/3/4, verified on the 389's own 28 hatch entries): a floor draws at
/// <c>(x, y - height)</c>; wall stack index <c>k</c> draws at <c>(x, y - height - offset + k + 1)</c>
/// (<c>offset</c> signed, the "+1" is the original's own dy-pre-increment - GraphicManager.cs:279). The
/// SORT KEY, for both kinds, always uses the MUTATED cell's own <c>y</c> (the "source" row) - never the
/// derived draw row - exactly like <see cref="WallPlacementOverlay"/>'s own init-time formulas. Depth slot
/// is a pure function of the raw tile id (<see cref="ComputeDepthSlot"/>), and the raw-id -&gt; local-tile-id
/// map comes from <see cref="TileMapComponent.TileSetData"/>'s own per-tile "TileId" custom property (the
/// only visual tileset any Render_* layer ever indexes, index 0 - plan fact 6), built once here.
///
/// Wall stack holes (plan fact 14): <see cref="AlundraCellStore.GetWallTileStack"/> already trims its
/// returned <c>Tiles</c> to the visible <c>Count</c> (never the backing array's own longer
/// <c>Length</c>), but a <c>0xffff</c> entry INSIDE that visible range is a hole that still consumes its
/// own stack index - the loop below walks the RAW index <c>k</c> and skips a hole without compacting, so
/// every tile after it keeps its original <c>k</c> (and therefore its original draw row).
///
/// Degraded, never fatal (Conception): an unmapped raw tile id, a mutated floor living outside the
/// original placements (D-E7-3's own degraded case - proved unreachable on map 389, plan fact 10, but the
/// precheck below still exists for any future map/opcode), or a derived position landing off the map, are
/// all a single warning (or, for the off-map case, deliberately no warning at all - plan fact 15, the
/// export itself drops those silently) and a skipped entry - never an exception, since
/// <see cref="TileMapComponent.AddSortedOverlayTile"/> itself throws on a bad reference and this class
/// must never let that happen mid-frame.
/// </summary>
public sealed class AlundraCellVisualSync
{
    private const int NoFloorTileId = 0xffff;
    private const int NoWallTileId = 0xffff;
    private const int NavigationWalkabilityMask = 0x40;

    private readonly TileMapComponent _tileMapComponent;
    private readonly AlundraCellStore _cellStore;
    private readonly int _width;
    private readonly int _height;
    private readonly string _worldName;
    private readonly Func<NavigationGrid2D?> _navigationGridAccessor;
    private readonly Dictionary<int, int> _localIdByRawTileId;

    private readonly Dictionary<(int X, int Y), OverlayLiveEntry> _floorModel = new();
    private readonly Dictionary<(int X, int Y, int K), OverlayLiveEntry> _wallModel = new();
    private int _nextFloorStableId;
    private int _nextWallStableId;
    private bool _overlayDirty;

    /// <summary>Test-observable count of ACTUAL <see cref="TileMapComponent.ClearSortedOverlayTiles"/> +
    /// resubmit reconstructions performed by <see cref="FlushPendingOverlayReconstruction"/> - acceptance
    /// item 5 (docs/plan-e7-mutation-tuiles.md, slice E7.b): a bits-only mutation (0x54/0x55) must never
    /// bump this.</summary>
    public int ReconstructionCount { get; private set; }

    private AlundraCellVisualSync(
        TileMapComponent tileMapComponent, AlundraCellStore cellStore, int width, int height, string worldName,
        Func<NavigationGrid2D?> navigationGridAccessor, Dictionary<int, int> localIdByRawTileId)
    {
        _tileMapComponent = tileMapComponent;
        _cellStore = cellStore;
        _width = width;
        _height = height;
        _worldName = worldName;
        _navigationGridAccessor = navigationGridAccessor;
        _localIdByRawTileId = localIdByRawTileId;
    }

    /// <summary>
    /// Builds the sync and seeds its live model from exactly what
    /// <see cref="WallPlacementOverlay.Apply"/>/<see cref="WallPlacementOverlay.ApplyFloor"/> resubmitted
    /// (<paramref name="submittedWallIndices"/>/<paramref name="submittedFloorIndices"/> - their own return
    /// value), not from the (superset) <paramref name="wallRecords"/>/<paramref name="floorRecords"/>
    /// documents themselves - see this class's own doc. <paramref name="navigationGridAccessor"/> is a
    /// live getter (not a captured snapshot) so a test can inject
    /// <see cref="AlundraWorldProxy.NavigationGrid"/> AFTER this call and still have navigation sync
    /// pick it up (the real resolver needs a live <c>AssetContentManager</c> and degrades to null without
    /// one - acceptance item 7's own "inject a grid into the proxy" step).
    /// </summary>
    public static AlundraCellVisualSync Create(
        TileMapComponent tileMapComponent, AlundraCellStore cellStore, int width, int height, string worldName,
        TileSetData? visualTileSet,
        WallPlacementRecords? wallRecords, IReadOnlyList<int> submittedWallIndices,
        FloorPlacementRecords? floorRecords, IReadOnlyList<int> submittedFloorIndices,
        Func<NavigationGrid2D?> navigationGridAccessor)
    {
        var localIdByRawTileId = BuildRawToLocalTileIdMap(visualTileSet);
        var sync = new AlundraCellVisualSync(
            tileMapComponent, cellStore, width, height, worldName, navigationGridAccessor, localIdByRawTileId);

        if (wallRecords != null)
        {
            foreach (var i in submittedWallIndices)
            {
                var key = (wallRecords.CellX[i], wallRecords.CellY[i], wallRecords.StackIndex[i]);
                var localId = wallRecords.Gid[i] - WallPlacementOverlay.FirstGid;
                var sortKey = WallPlacementOverlay.ComputeWallSortKey(wallRecords.CellY[i], wallRecords.DepthSlot[i], i);
                sync._wallModel[key] = new OverlayLiveEntry(
                    i, new TileMapTileReference(0, localId), wallRecords.X[i], wallRecords.Y[i], in sortKey);
            }

            sync._nextWallStableId = wallRecords.Count;
        }

        if (floorRecords != null)
        {
            foreach (var i in submittedFloorIndices)
            {
                var key = (floorRecords.CellX[i], floorRecords.CellY[i]);
                var localId = floorRecords.Gid[i] - WallPlacementOverlay.FirstGid;
                var sortKey = WallPlacementOverlay.ComputeFloorSortKey(floorRecords.CellY[i], floorRecords.DepthSlot[i], i);
                sync._floorModel[key] = new OverlayLiveEntry(
                    i, new TileMapTileReference(0, localId), floorRecords.X[i], floorRecords.Y[i], in sortKey);
            }

            sync._nextFloorStableId = floorRecords.Count;
        }

        return sync;
    }

    private static Dictionary<int, int> BuildRawToLocalTileIdMap(TileSetData? visualTileSet)
    {
        var map = new Dictionary<int, int>();
        if (visualTileSet == null)
        {
            return map;
        }

        foreach (var tile in visualTileSet.Tiles)
        {
            if (tile.CustomProperties.TryGetValue("TileId", out var rawIdText)
                && int.TryParse(rawIdText, out var rawId))
            {
                map[rawId] = tile.Id;
            }
        }

        return map;
    }

    /// <summary>Subscribed directly to <see cref="AlundraCellStore.CellsMutated"/> by
    /// <see cref="AlundraWorldProxy"/>'s installation method - acceptance item 1's own wiring check
    /// (docs/plan-e7-mutation-tuiles.md). Processes every mutated cell's overlay contributions AND its
    /// navigation cell; only the overlay half is deferred (<see cref="_overlayDirty"/>) to
    /// <see cref="FlushPendingOverlayReconstruction"/> - navigation writes are a single array element each,
    /// cheap enough to apply immediately, unlike the overlay's clear-and-resubmit-everything reconstruction.</summary>
    public void OnCellsMutated(IReadOnlyList<int> mutatedCellIndices)
    {
        foreach (var index in mutatedCellIndices)
        {
            var x = index % _width;
            var y = index / _width;

            ProcessCellFloor(x, y);
            ProcessCellWalls(x, y);
            ProcessCellNavigation(x, y);
        }
    }

    /// <summary>Applies every pending overlay change accumulated since the last flush - a no-op when
    /// nothing changed (bits-only mutations, acceptance item 5). Called once per rendered frame by
    /// <see cref="AlundraWorldProxy.Update"/>, coalescing however many mutation opcodes dispatched that
    /// frame into at most one <see cref="TileMapComponent.ClearSortedOverlayTiles"/> +
    /// resubmit-the-whole-model pass (plan Conception, "coalescer par frame").</summary>
    public void FlushPendingOverlayReconstruction()
    {
        if (!_overlayDirty)
        {
            return;
        }

        _tileMapComponent.ClearSortedOverlayTiles();

        foreach (var entry in _floorModel.Values)
        {
            var sortKey = entry.SortKey;
            _tileMapComponent.AddSortedOverlayTile(entry.TileReference, entry.GridX, entry.GridY, in sortKey);
        }

        foreach (var entry in _wallModel.Values)
        {
            var sortKey = entry.SortKey;
            _tileMapComponent.AddSortedOverlayTile(entry.TileReference, entry.GridX, entry.GridY, in sortKey);
        }

        _overlayDirty = false;
        ReconstructionCount++;
    }

    private void ProcessCellFloor(int x, int y)
    {
        var key = (x, y);
        var hadExisting = _floorModel.TryGetValue(key, out var existingEntry);
        var tileId = _cellStore.GetFloorTileId(x, y);

        if (tileId == NoFloorTileId)
        {
            if (hadExisting)
            {
                _floorModel.Remove(key);
                _overlayDirty = true;
            }

            return;
        }

        var height = _cellStore.GetHeight(x, y);

        if (!hadExisting && height == 0)
        {
            // Ordinary ground-level floor, never stripped from its flat layer - nothing to adopt, and
            // NOT the D-E7-3 degraded case (that one requires an elevated floor - see below).
            return;
        }

        if (!hadExisting && height != 0)
        {
            // D-E7-3's degraded case: an elevated floor mutated into existence outside the original
            // AlundraFloorPlacements - its flat tile was never stripped, so adopting it into the overlay
            // would double-draw it. Proved unreachable on map 389 (plan fact 10); still handled here for
            // any future map/opcode. Left exactly as loaded (flat), one warning.
            Logs.WriteWarning(
                $"AlundraCellVisualSync: world '{_worldName}' - cell ({x},{y}) mutated to an elevated floor "
                + $"(height={height}) that was never in the original AlundraFloorPlacements; left flat and "
                + "un-adopted (degraded).");
            return;
        }

        var stableId = hadExisting ? existingEntry.StableId : _nextFloorStableId++;

        if (!_localIdByRawTileId.TryGetValue(tileId, out var localId))
        {
            Logs.WriteWarning(
                $"AlundraCellVisualSync: world '{_worldName}' - cell ({x},{y})'s mutated floor raw tile id "
                + $"{tileId} has no matching local tile id in the visual tileset; entry skipped (degraded).");
            if (hadExisting)
            {
                _floorModel.Remove(key);
                _overlayDirty = true;
            }

            return;
        }

        var drawX = x;
        var drawY = y - height;

        if (!IsInsideMap(drawX, drawY))
        {
            // Fact 15: an off-map target is dropped silently, matching the export's own PlaceTile(-1)
            // behavior - no warning.
            if (hadExisting)
            {
                _floorModel.Remove(key);
                _overlayDirty = true;
            }

            return;
        }

        var depthSlot = ComputeDepthSlot(tileId);
        var sortKey = WallPlacementOverlay.ComputeFloorSortKey(y, depthSlot, stableId);
        var candidate = new OverlayLiveEntry(stableId, new TileMapTileReference(0, localId), drawX, drawY, in sortKey);

        if (hadExisting && EntriesEqual(existingEntry, candidate))
        {
            return;
        }

        _floorModel[key] = candidate;
        _overlayDirty = true;
    }

    private void ProcessCellWalls(int x, int y)
    {
        var height = _cellStore.GetHeight(x, y);
        var stack = _cellStore.GetWallTileStack(x, y);
        var visibleCount = stack?.Tiles.Count ?? 0;

        // Any (x, y, k) this model still holds beyond the cell's current visible count is stale - either
        // the whole stack was destroyed (source-less copy, AlundraCellStore.CopyWallTileStack's own doc)
        // or it shrank. Removed here rather than skipped in the loop below, since the loop only ever walks
        // 0..visibleCount-1.
        List<(int X, int Y, int K)>? staleKeys = null;
        foreach (var existingKey in _wallModel.Keys)
        {
            if (existingKey.X == x && existingKey.Y == y && existingKey.K >= visibleCount)
            {
                (staleKeys ??= new List<(int, int, int)>()).Add(existingKey);
            }
        }

        if (staleKeys != null)
        {
            foreach (var staleKey in staleKeys)
            {
                _wallModel.Remove(staleKey);
            }

            _overlayDirty = true;
        }

        if (stack == null)
        {
            return;
        }

        var (offset, tiles) = stack.Value;

        for (var k = 0; k < tiles.Count; k++)
        {
            var raw = tiles[k];
            var key = (x, y, k);
            var hadExisting = _wallModel.TryGetValue(key, out var existingEntry);

            if (raw == NoWallTileId)
            {
                // A hole - plan fact 14: it still consumed its own index k above (the loop is not
                // compacted), it just carries no visible tile.
                if (hadExisting)
                {
                    _wallModel.Remove(key);
                    _overlayDirty = true;
                }

                continue;
            }

            if (!_localIdByRawTileId.TryGetValue(raw, out var localId))
            {
                Logs.WriteWarning(
                    $"AlundraCellVisualSync: world '{_worldName}' - cell ({x},{y}) wall stack index {k}'s "
                    + $"raw tile id {raw} has no matching local tile id in the visual tileset; entry skipped "
                    + "(degraded).");
                if (hadExisting)
                {
                    _wallModel.Remove(key);
                    _overlayDirty = true;
                }

                continue;
            }

            var drawX = x;
            var drawY = y - height - offset + k + 1;

            if (!IsInsideMap(drawX, drawY))
            {
                // Fact 15: dropped silently, no warning - the export abandons an out-of-map wall target
                // the exact same way (PlaceTile returns -1, never recorded, no trace).
                if (hadExisting)
                {
                    _wallModel.Remove(key);
                    _overlayDirty = true;
                }

                continue;
            }

            var stableId = hadExisting ? existingEntry.StableId : _nextWallStableId++;
            var depthSlot = ComputeDepthSlot(raw);
            var sortKey = WallPlacementOverlay.ComputeWallSortKey(y, depthSlot, stableId);
            var candidate = new OverlayLiveEntry(stableId, new TileMapTileReference(0, localId), drawX, drawY, in sortKey);

            if (hadExisting && EntriesEqual(existingEntry, candidate))
            {
                continue;
            }

            _wallModel[key] = candidate;
            _overlayDirty = true;
        }
    }

    /// <summary>Port of <c>NavigationWriter</c>'s own walkability formula (converter,
    /// <c>Writers/NavigationWriter.cs</c>): <c>((Walkability | (GroundProperty &lt;&lt; 8)) &amp; 0x40) == 0</c>.
    /// Applied immediately (not deferred like the overlay) - a single <see cref="NavigationGridCell"/>
    /// write is cheap, and there is no equivalent "reconstruct everything" cost to coalesce. The layer mask
    /// is carried forward from the existing cell (plan Conception): a walkable cell with
    /// <see cref="NavigationLayerMask.None"/> would be unenterable regardless
    /// (<see cref="NavigationGridCell.CanEnter"/> requires both).</summary>
    private void ProcessCellNavigation(int x, int y)
    {
        var grid = _navigationGridAccessor();
        if (grid == null || !grid.IsInside(x, y))
        {
            return;
        }

        var walkability = _cellStore.GetWalkability(x, y);
        var groundProperty = _cellStore.GetGroundProperty(x, y);
        var isWalkable = ((walkability | (groundProperty << 8)) & NavigationWalkabilityMask) == 0;

        var existingCell = grid.GetCell(x, y);
        grid.SetCell(x, y, new NavigationGridCell(isWalkable, existingCell.Cost, existingCell.Layers));
    }

    /// <summary>Port of the raw-id depth slot formula (plan fact 4, verified against the 389's own
    /// entries): <c>(raw &amp; 0x3ff) &lt; 960 ? (raw &amp; 0x3ff) / 160 : 0</c>, range 0..5. A pure function
    /// of the raw tile id alone - no tileset lookup needed.</summary>
    internal static int ComputeDepthSlot(int rawTileId)
    {
        var masked = rawTileId & 0x3ff;
        return masked < 960 ? masked / 160 : 0;
    }

    private bool IsInsideMap(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

    private static bool EntriesEqual(in OverlayLiveEntry a, in OverlayLiveEntry b)
        => a.TileReference.TileSetIndex == b.TileReference.TileSetIndex
           && a.TileReference.TileId == b.TileReference.TileId
           && a.GridX == b.GridX
           && a.GridY == b.GridY
           && a.SortKey.Equals(b.SortKey);

    private readonly struct OverlayLiveEntry
    {
        public OverlayLiveEntry(int stableId, TileMapTileReference tileReference, int gridX, int gridY, in RenderSortKey2D sortKey)
        {
            StableId = stableId;
            TileReference = tileReference;
            GridX = gridX;
            GridY = gridY;
            SortKey = sortKey;
        }

        public readonly int StableId;
        public readonly TileMapTileReference TileReference;
        public readonly int GridX;
        public readonly int GridY;
        public readonly RenderSortKey2D SortKey;
    }
}
