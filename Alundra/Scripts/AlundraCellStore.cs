#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// The three cell-mutation primitives 0x54/0x55/0x85 need (docs/plan-e7-mutation-tuiles.md, slice
/// E7.a) - implemented by <see cref="AlundraCellStore"/>, consumed by
/// <see cref="AlundraEventProgramRunner.Dispatch"/> through <see cref="IEntityWorldContext.CellMutator"/>.
/// A separate interface (rather than exposing <see cref="AlundraCellStore"/> itself through the seam)
/// so a synthetic interpreter test can inject a fake without any real <see cref="AlundraCellsRecords"/>.
/// </summary>
public interface IAlundraCellMutator
{
    /// <summary>Opcode 0x85 (Script_133_085) - port of <c>GameEngine.ChangeAreaTileProperties</c>
    /// (GameEngine.cs:2239-2321). See <see cref="AlundraCellStore.CopyCellRectangle"/> for the exact
    /// field list and the documented no-clamp deviation.</summary>
    void CopyCellRectangle(int srcX, int srcY, int width, int height, int dstX, int dstY);

    /// <summary>Opcode 0x54 (Script_84_054) - OR's <paramref name="walkabilityMask"/>/
    /// <paramref name="groundPropertyMask"/> into one cell's Walkability/GroundProperty. See
    /// <see cref="AlundraCellStore.SetCellBits"/> for the hardcoded clamp.</summary>
    void SetCellBits(int x, int y, int walkabilityMask, int groundPropertyMask);

    /// <summary>Opcode 0x55 (Script_85_055) - AND's the COMPLEMENT of
    /// <paramref name="walkabilityMask"/>/<paramref name="groundPropertyMask"/> into one cell's
    /// Walkability/GroundProperty (bit clear). See <see cref="AlundraCellStore.ClearCellBits"/> for the
    /// hardcoded clamp.</summary>
    void ClearCellBits(int x, int y, int walkabilityMask, int groundPropertyMask);
}

/// <summary>
/// Mutable per-cell gameplay store for one map (docs/plan-e7-mutation-tuiles.md, slice E7.a) - the DLL
/// counterpart of the original's in-place <c>MapTile</c> mutation (GameEngine.cs:2260-2321,
/// EntityEventHandlers.cs:1589-1654). Built from the SAME <see cref="AlundraCellsRecords"/> instance
/// <see cref="AlundraCellsCollisionField"/> aliases its own arrays from (see
/// <see cref="AlundraCellsCollisionField.TryCreate(TileMapData, string, out AlundraCellsCollisionField?, out AlundraCellsRecords?)"/>):
/// this store mutates <c>Walkability</c>/<c>GroundProperty</c>/<c>Slope</c>/<c>Height</c>/<c>TileId</c>/
/// <c>WallTilesOffset</c> IN PLACE, element by element, on those exact same <c>int[]</c> instances -
/// never by replacing the array reference - so a mutation is instantly visible to
/// <see cref="AlundraCellsCollisionField.TrySampleGround(in Vector3, float, out GroundSample)"/>/
/// <see cref="AlundraCellsCollisionField.SampleGroundProperty"/>/
/// <see cref="AlundraCellsCollisionField.SampleRawCellHeight"/>, exactly like the original's entities
/// holding a live <c>MapTile</c> reference. Wall tile stacks are the one field NOT aliased from
/// <see cref="AlundraCellsRecords"/> (nothing else reads them in E7.a - E7.b's overlay applier is the
/// first other reader): this store re-indexes the sparse, JSON-shaped <see cref="AlundraCellsRecords.WallTiles"/>
/// dictionary into a dense, mutable per-cell array once, at construction, for O(1) access/replace/destroy.
///
/// D-E7-5 (map re-entry fidelity): every mutation here is transient by construction - a fresh
/// <see cref="AlundraCellsRecords"/> is parsed (and this store rebuilt from it) every time a world is
/// (re)initialized, and nothing here ever writes back into <c>TileMapData</c> itself.
/// </summary>
public sealed class AlundraCellStore : IAlundraCellMutator
{
    private readonly int _width;
    private readonly int _height;
    private readonly int[] _walkability;
    private readonly int[] _groundProperty;
    private readonly int[] _slope;
    private readonly int[] _cellHeight;
    private readonly int[] _tileId;
    private readonly int[] _wallTilesOffset;
    private readonly MutableWallTileStack?[] _wallTiles;

    /// <summary>
    /// Fires once per primitive call (<see cref="CopyCellRectangle"/>/<see cref="SetCellBits"/>/
    /// <see cref="ClearCellBits"/>) with every cell index that call touched (row-major destination
    /// indices for a rectangle copy, a single-element list for the bit primitives). E7.a installs no
    /// subscriber - E7.b's visual overlay applier is the first (docs/plan-e7-mutation-tuiles.md).
    /// </summary>
    public event Action<IReadOnlyList<int>>? CellsMutated;

    private AlundraCellStore(int width, int height, AlundraCellsRecords records, string worldName)
    {
        _width = width;
        _height = height;
        _walkability = records.Walkability;
        _groundProperty = records.GroundProperty;
        _slope = records.Slope;
        _cellHeight = records.Height;
        _tileId = records.TileId;
        _wallTilesOffset = records.WallTilesOffset;

        _wallTiles = new MutableWallTileStack?[width * height];
        foreach (var (key, stack) in records.WallTiles)
        {
            if (int.TryParse(key, out var index) && index >= 0 && index < _wallTiles.Length)
            {
                _wallTiles[index] = new MutableWallTileStack
                {
                    Offset = stack.Offset,
                    // Count == Tiles.Length holds for every stack AS PARSED (the original allocates
                    // Tiles = new ushort[Count] at load, WallTiles.cs:19-26, so the export has no reason
                    // to carry Count). The two only diverge after a copy - see CopyWallTileStack.
                    Count = stack.Tiles.Length,
                    Tiles = (int[])stack.Tiles.Clone(),
                };
            }
            else
            {
                // E7.b (docs/plan-e7-mutation-tuiles.md, deferred item picked up from E7.a): a malformed
                // key - not an integer, or out of [0, width*height) - used to be silently dropped. Never
                // observed on a converter-produced export (WallTiles keys are always CellMetadataWriter's
                // own row-major indices), but a hand-edited/corrupted document should not lose a wall
                // stack without a trace.
                Logs.WriteWarning(
                    $"AlundraCellStore: world '{worldName}' - AlundraCells 'wall_tiles' key '{key}' is not a "
                    + $"valid cell index in [0,{_wallTiles.Length}); that wall tile stack is dropped.");
            }
        }
    }

    /// <summary>
    /// Builds the store from an ALREADY-PARSED <paramref name="records"/> (the same instance a sibling
    /// <see cref="AlundraCellsCollisionField"/> was built from - see this class's own doc). Fails, with a
    /// single warning, when <see cref="AlundraCellsRecords.TileId"/>/<see cref="AlundraCellsRecords.WallTilesOffset"/>
    /// do not have exactly <paramref name="width"/>*<paramref name="height"/> elements - a column-length
    /// mismatch these two arrays specifically can carry that <see cref="AlundraCellsCollisionField"/>'s
    /// own well-formed gate does not check (it never reads them - see that gate's own doc).
    /// </summary>
    public static bool TryCreate(AlundraCellsRecords records, int width, int height, string worldName, out AlundraCellStore? store)
    {
        store = null;
        var expectedCellCount = width * height;

        if (records.TileId.Length != expectedCellCount || records.WallTilesOffset.Length != expectedCellCount)
        {
            Logs.WriteWarning(
                $"AlundraCellStore: world '{worldName}' - tile_id/wall_tiles_offset column length does not "
                + $"match TileMapData.MapSize ({width}x{height}={expectedCellCount}); cell store not "
                + "installed (degraded mode).");
            return false;
        }

        store = new AlundraCellStore(width, height, records, worldName);
        return true;
    }

    /// <summary>One cell's raw floor tile id (E7.b, docs/plan-e7-mutation-tuiles.md) - <c>0xffff</c> means
    /// "no floor" (matches the export's own sentinel, see <see cref="AlundraCellsCollisionField"/>'s class
    /// doc on <c>tile_id</c> not being read there). No clamping, same contract as
    /// <see cref="GetWallTileStack"/>.</summary>
    public int GetFloorTileId(int x, int y) => _tileId[y * _width + x];

    /// <summary>One cell's current elevation (E7.b) - the SAME <c>_cellHeight</c> array
    /// <see cref="AlundraCellsCollisionField"/> aliases, read here by (x, y) instead of by world
    /// position.</summary>
    public int GetHeight(int x, int y) => _cellHeight[y * _width + x];

    /// <summary>One cell's raw Walkability byte (E7.b) - see <see cref="GetHeight"/>'s own doc on why this
    /// duplicates an <see cref="AlundraCellsCollisionField.SampleRawWalkability"/>-shaped read by (x, y)
    /// instead of by world position: the overlay/navigation applier only ever has cell coordinates on
    /// hand, not a <c>Vector3</c>.</summary>
    public int GetWalkability(int x, int y) => _walkability[y * _width + x];

    /// <summary>One cell's raw GroundProperty byte (E7.b) - see <see cref="GetWalkability"/>'s own
    /// doc.</summary>
    public int GetGroundProperty(int x, int y) => _groundProperty[y * _width + x];

    /// <summary>
    /// One cell's wall tile stack AS THE ORIGINAL RENDERER SEES IT. Returns null when the cell carries no
    /// stack. No clamping: <paramref name="x"/>/<paramref name="y"/> must already be in range (unlike the
    /// mutation primitives below, which port the original's own clamp/no-clamp policy verbatim).
    ///
    /// The returned <c>Tiles</c> is the stack's first <c>Count</c> entries, NOT its whole backing array:
    /// the original's wall-tile loop runs <c>for (i = 0; i &lt; wallTiles.Count; i++)</c>
    /// (GraphicManager.cs:277), so anything the backing array holds past <c>Count</c> is invisible in the
    /// original and must be invisible here too. The two lengths coincide for every stack as parsed and
    /// diverge only after a copy onto a longer destination - see <see cref="CopyWallTileStack"/>.
    /// </summary>
    public (int Offset, IReadOnlyList<int> Tiles)? GetWallTileStack(int x, int y)
    {
        var stack = _wallTiles[y * _width + x];

        if (stack == null)
        {
            return null;
        }

        // E7.b (docs/plan-e7-mutation-tuiles.md, deferred item picked up from E7.a): always a fresh copy,
        // never the live backing array (nor an ArraySegment over it, which would still alias it) - a
        // caller (e.g. AlundraCellVisualSync) must not be able to observe/corrupt this store's own mutable
        // state through what looks like a read-only accessor.
        var visibleTiles = new int[stack.Count];
        Array.Copy(stack.Tiles, visibleTiles, stack.Count);

        return (stack.Offset, visibleTiles);
    }

    /// <summary>
    /// Opcode 0x85 - exact port of <c>GameEngine.ChangeAreaTileProperties(int,int,int,int,int,int)</c>
    /// (GameEngine.cs:2239-2321): row-major (y outer, x inner) copy of Walkability/GroundProperty/Slope/
    /// Height/TileId/WallTilesOffset from the source rectangle to the destination rectangle, plus a
    /// deep copy of each cell's wall tile stack (source with no stack -&gt; destination stack destroyed -
    /// see <see cref="CopyWallTileStack"/>).
    ///
    /// NO CLAMPING (documented deviation, plan §3 E7.a): the original checks for negative operands and
    /// for the rectangle exceeding the map's own Width/Height, and on either violation calls
    /// <c>Debugger.Breakpoint()</c> - a DEBUG-ONLY trap, not a guard: even when it fires, the original
    /// still runs the copy loop below with the same raw, unclamped operands (GameEngine.cs:2244-2257).
    /// This port has no debugger to break into, so it logs one warning instead and then behaves
    /// IDENTICALLY - it still performs the copy with the raw operands, including indexing the backing
    /// arrays out of range if the caller truly passed out-of-map coordinates (matching the original's own
    /// "the trap doesn't stop anything" behavior, not adding new protection the original never had).
    /// </summary>
    public void CopyCellRectangle(int srcX, int srcY, int width, int height, int dstX, int dstY)
    {
        if (srcX < 0 || srcY < 0 || width < 0 || height < 0 || dstX < 0 || dstY < 0)
        {
            Logs.WriteWarning(
                $"AlundraCellStore.CopyCellRectangle: negative operand (srcX={srcX}, srcY={srcY}, "
                + $"width={width}, height={height}, dstX={dstX}, dstY={dstY}) - the original debug-breaks "
                + "here then proceeds with the same unclamped operands anyway; this port logs and "
                + "proceeds identically (no debugger to break into).");
        }
        else if (_width < srcX + width || _height < srcY + height || _width < dstX + width || _height < dstY + height)
        {
            Logs.WriteWarning(
                $"AlundraCellStore.CopyCellRectangle: rectangle exceeds map bounds ({_width}x{_height}) - "
                + $"srcX={srcX}, srcY={srcY}, width={width}, height={height}, dstX={dstX}, dstY={dstY} - "
                + "the original debug-breaks here then proceeds with the same unclamped operands anyway; "
                + "this port logs and proceeds identically (no debugger to break into).");
        }

        // E7.b (docs/plan-e7-mutation-tuiles.md, deferred item picked up from E7.a): only allocate the
        // mutated-indices list when something is actually listening - most synthetic/degraded-mode tests
        // (and any world with no cell visual sync installed) have no subscriber at all.
        var hasSubscriber = CellsMutated != null;
        List<int>? mutated = hasSubscriber ? new List<int>() : null;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var srcIndex = srcX + x + (srcY + y) * _width;
                var dstIndex = dstX + x + (dstY + y) * _width;

                _walkability[dstIndex] = _walkability[srcIndex];
                _groundProperty[dstIndex] = _groundProperty[srcIndex];
                _slope[dstIndex] = _slope[srcIndex];
                _cellHeight[dstIndex] = _cellHeight[srcIndex];
                _tileId[dstIndex] = _tileId[srcIndex];
                _wallTilesOffset[dstIndex] = _wallTilesOffset[srcIndex];

                CopyWallTileStack(srcIndex, dstIndex);

                mutated?.Add(dstIndex);
            }
        }

        if (mutated != null)
        {
            CellsMutated?.Invoke(mutated);
        }
    }

    /// <summary>
    /// GameEngine.cs:2285-2309's own wall-tile-stack half of <see cref="CopyCellRectangle"/>: when the
    /// source cell carries no stack, the destination's is destroyed entirely (PSX: a raw
    /// <c>WallTilesOffset</c> of 0xFFFF alone stopped the renderer from drawing wall tiles; this C# port
    /// checks <c>WallTiles != null</c> instead, per the original's own comment at :2299-2302, so the
    /// object itself must go). Otherwise: allocate the destination's <c>Tiles</c> array only if it does
    /// not have one yet, grow it (replacing the array, losing any longer-than-needed old tail) only if it
    /// is SHORTER than the source's, then <c>Array.Copy</c> the shared prefix - exactly
    /// GameEngine.cs:2291-2309.
    ///
    /// <c>Count</c> is copied from the source alongside <c>Offset</c> (GameEngine.cs:2297) and is what
    /// makes the "destination longer than the source" case behave: the backing array does keep its stale
    /// tail past the copied prefix, but the original's renderer stops at <c>Count</c>
    /// (GraphicManager.cs:277), so that tail is invisible - and <see cref="GetWallTileStack"/> hides it
    /// here for the same reason. Dropping <c>Count</c> and relying on <c>Tiles.Length</c> would reproduce
    /// the array state while diverging on the only thing that is observable. Map 389 never shrinks a stack
    /// today (its one shape change, (21,27), GROWS 6 -&gt; 7), so this is latent there - but E7.b's overlay
    /// applier reads this accessor, and a wrong stack length would draw wall tiles the original does not.
    /// </summary>
    private void CopyWallTileStack(int srcIndex, int dstIndex)
    {
        var source = _wallTiles[srcIndex];

        if (source == null)
        {
            _wallTiles[dstIndex] = null;
            return;
        }

        var destination = _wallTiles[dstIndex];

        if (destination == null)
        {
            destination = new MutableWallTileStack { Tiles = new int[source.Tiles.Length] };
            _wallTiles[dstIndex] = destination;
        }

        var copyLength = Math.Min(source.Tiles.Length, destination.Tiles.Length);
        destination.Offset = source.Offset;
        destination.Count = source.Count;

        if (destination.Tiles.Length < source.Tiles.Length)
        {
            destination.Tiles = new int[source.Tiles.Length];
            copyLength = source.Tiles.Length;
        }

        Array.Copy(source.Tiles, destination.Tiles, copyLength);
    }

    /// <summary>Opcode 0x54 - exact port of <c>Script_84_054</c> (EntityEventHandlers.cs:1589-1620):
    /// OR's <paramref name="walkabilityMask"/>/<paramref name="groundPropertyMask"/> into the clamped
    /// cell's Walkability/GroundProperty. See <see cref="ApplyCellBits"/> for the shared clamp/index
    /// logic with <see cref="ClearCellBits"/>.</summary>
    public void SetCellBits(int x, int y, int walkabilityMask, int groundPropertyMask)
        => ApplyCellBits(x, y, walkabilityMask, groundPropertyMask, clear: false);

    /// <summary>Opcode 0x55 - exact port of <c>Script_85_055</c> (EntityEventHandlers.cs:1623-1654):
    /// AND's the complement of <paramref name="walkabilityMask"/>/<paramref name="groundPropertyMask"/>
    /// into the clamped cell's Walkability/GroundProperty (bit clear). See
    /// <see cref="ApplyCellBits"/>.</summary>
    public void ClearCellBits(int x, int y, int walkabilityMask, int groundPropertyMask)
        => ApplyCellBits(x, y, walkabilityMask, groundPropertyMask, clear: true);

    /// <summary>
    /// Shared body of <see cref="SetCellBits"/>/<see cref="ClearCellBits"/> - both original handlers clamp
    /// identically before diverging only on |= vs &amp;=~ (EntityEventHandlers.cs:1592-1610 /
    /// :1626-1644). The clamp bounds - <c>x</c> to [0,0x33], <c>y</c> to [0,0x3b] - are the ORIGINAL's own
    /// hardcoded literal constants (0x33=51, 0x3b=59), NOT re-derived from this map's actual
    /// <see cref="_width"/>/<see cref="_height"/>: map 389 happens to be exactly 52x60 (so
    /// 0x33/0x3b coincide with "map width/height - 1" here), but the original clamps to these two bytes
    /// on every map regardless of its real size - a faithful port keeps the literals, not the
    /// coincidence. The final index still uses the REAL <see cref="_width"/> (EntityEventHandlers.cs:1611
    /// / :1645 - <c>tilex + tiley * mapWidth</c>, <c>mapWidth</c> read from the live map, not hardcoded).
    /// </summary>
    private void ApplyCellBits(int x, int y, int walkabilityMask, int groundPropertyMask, bool clear)
    {
        var clampedX = Math.Clamp(x, 0, 0x33);
        var clampedY = Math.Clamp(y, 0, 0x3b);
        var index = clampedX + clampedY * _width;

        if (clear)
        {
            _walkability[index] &= (byte)(~walkabilityMask);
            _groundProperty[index] &= (byte)(~groundPropertyMask);
        }
        else
        {
            _walkability[index] |= (byte)walkabilityMask;
            _groundProperty[index] |= (byte)groundPropertyMask;
        }

        // E7.b (docs/plan-e7-mutation-tuiles.md, deferred item): `?.Invoke(new[] { index })` would still
        // allocate the array even with no subscriber - the argument is evaluated before the null-check on
        // the delegate. Guarded explicitly instead.
        if (CellsMutated != null)
        {
            CellsMutated(new[] { index });
        }
    }

    /// <summary>Mutable counterpart of the immutable, JSON-parsed <see cref="WallTileStack"/> - this
    /// store's own private working copy, never shared with <see cref="AlundraCellsRecords"/> itself (see
    /// class doc on why wall stacks are not aliased). Unlike the parsed record it carries the original's
    /// <c>Count</c> (<c>WallTiles.cs:5-7</c>), because a copy can leave it below
    /// <see cref="Tiles"/>.Length - see <see cref="CopyWallTileStack"/>.</summary>
    private sealed class MutableWallTileStack
    {
        public int Offset { get; set; }
        public int Count { get; set; }
        public int[] Tiles { get; set; } = Array.Empty<int>();
    }
}
