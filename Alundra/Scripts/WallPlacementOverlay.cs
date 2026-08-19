#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;

namespace Alundra.Scripts;

/// <summary>
/// One map's "AlundraWallPlacements" custom property (see
/// <c>AlundraCasaEngineProjectConverter.Writers.CellMetadataWriter.WriteWallPlacements</c> and
/// <c>docs/formats/cells-companion.md</c>): every baked wall tile's exact flat-layer location
/// (<see cref="Plane"/>/<see cref="X"/>/<see cref="Y"/>, matching <c>TileMapComponent.RemoveTile</c>'s
/// own (layer, x, y) triple) plus its PSX depth slot (0..5, see <see cref="DepthSlot"/>). Columnar for
/// the same reason the converter's own <c>WallPlacementDocument</c> is: this project does not reference
/// the converter, so the shape is duplicated here rather than shared - field names/order are read back
/// with the same <c>JsonNamingPolicy.SnakeCaseLower</c> the converter serialized them with, so no
/// per-field attribute is needed on either side.
/// </summary>
public sealed class WallPlacementRecords
{
    public int MapIndex { get; init; }
    public int Count { get; init; }
    public int[] CellX { get; init; } = Array.Empty<int>();
    public int[] CellY { get; init; } = Array.Empty<int>();
    public int[] StackIndex { get; init; } = Array.Empty<int>();
    public int[] Plane { get; init; } = Array.Empty<int>();
    public int[] X { get; init; } = Array.Empty<int>();
    public int[] Y { get; init; } = Array.Empty<int>();
    public int[] Gid { get; init; } = Array.Empty<int>();
    public int[] DepthSlot { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Parses <see cref="WallPlacementRecords"/> from a <see cref="TileMapData"/>'s custom properties, and
/// drives the wall/sprite depth interleave (strip the baked wall tiles from the flat layers, resubmit
/// them through <see cref="TileMapComponent.AddSortedOverlayTile"/> so they order against Y-sorted
/// entity sprites by <see cref="RenderSortKey2D"/> instead of always drawing flat) - see
/// <see cref="AlundraWorldProxy.InitializeWithWorld"/> for the call site and
/// <c>docs/formats/cells-companion.md</c> for the original PSX depth formula this ports
/// (<c>GraphicManager.DepthWallBlock</c>/<c>DepthEntity</c>, AlundraTools/AlundraEngine/Graphics/GraphicManager.cs:313-346).
/// </summary>
public static class WallPlacementOverlay
{
    /// <summary>Key of the map-level custom property carrying the columnar wall placement JSON.</summary>
    public const string CustomPropertyKey = "AlundraWallPlacements";

    /// <summary>Every gid in the placement document is <c>firstgid + localTileId</c> with a single
    /// tileset per map and <c>firstgid</c> fixed at 1 (see <c>TileSetGidMapReader</c>/
    /// <c>docs/formats/cells-companion.md</c>) - so the local tile id <see cref="TileMapComponent"/>'s own
    /// layers store is always <c>gid - 1</c>.</summary>
    private const int FirstGid = 1;

    /// <summary>
    /// PSX depth stride: original code packs <c>otIndex = value &gt;&gt; 20</c>ish and multiplies the
    /// tile row by 16 slots (<c>GraphicManager.cs</c>: <c>STRIDE = 16</c>). Floors occupy slots 0..6, walls
    /// slots 7..12 (<c>WALL_BIAS = 7</c>), and Y-sorted entities land at slot 6 within their own row
    /// (<c>ENTITY_SLOT = 6</c>) - one below the wall row of the very same tile row, and one at/above the
    /// floor of that row, matching the original's z-buffer ordering.
    /// </summary>
    private const int RowStride = 16;

    private const int WallDepthBias = 7;
    private const int EntityDepthSlot = 6;

    /// <summary>
    /// Shared <see cref="RenderSortKey2D"/> bucket every wall-overlay tile and every spawned entity's
    /// <see cref="DepthSortable2DComponent"/> lands in by default (<c>SpriteWriter</c> attaches a bare,
    /// all-default <see cref="DepthSortable2DComponent"/> to every bank prefab): <see cref="RenderPass2D.YSortedWorld"/>
    /// for both <c>RenderPass</c> and <c>SortingLayer</c>, order-in-layer 0. Only <c>Elevation</c> (and,
    /// for entities, the component's own computed <c>SortCoordinate</c>) then decides draw order between
    /// the two families of keyed sprites.
    /// </summary>
    private const int SharedSortingLayer = (int)RenderPass2D.YSortedWorld;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Best-effort parse of <see cref="CustomPropertyKey"/> off <paramref name="customProperties"/>.
    /// Tolerant by design (see <see cref="AlundraWorldProxy.InitializeWithWorld"/>'s class doc): a
    /// missing property, malformed JSON, or a document whose array lengths do not match its own
    /// <c>Count</c> all return false with a single warning logged - the feature is simply disabled for
    /// that world, spawned entities and the map's flat tile layers stay exactly as loaded.
    /// </summary>
    public static bool TryParse(
        IReadOnlyDictionary<string, string> customProperties, string worldName, out WallPlacementRecords records)
    {
        records = null!;

        if (!customProperties.TryGetValue(CustomPropertyKey, out var json) || string.IsNullOrEmpty(json))
        {
            Logs.WriteWarning(
                $"WallPlacementOverlay: world '{worldName}' has no '{CustomPropertyKey}' custom property; "
                + "wall/sprite depth interleave disabled.");
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<WallPlacementRecords>(json, SerializerOptions);
            if (parsed == null || !IsWellFormed(parsed))
            {
                Logs.WriteWarning(
                    $"WallPlacementOverlay: world '{worldName}' - '{CustomPropertyKey}' parsed to a malformed "
                    + "document (column length mismatch); wall/sprite depth interleave disabled.");
                return false;
            }

            records = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            Logs.WriteWarning(
                $"WallPlacementOverlay: world '{worldName}' - failed to parse '{CustomPropertyKey}' "
                + $"({ex.Message}); wall/sprite depth interleave disabled.");
            return false;
        }
    }

    private static bool IsWellFormed(WallPlacementRecords records)
    {
        var count = records.Count;
        return records.CellX.Length == count
            && records.CellY.Length == count
            && records.StackIndex.Length == count
            && records.Plane.Length == count
            && records.X.Length == count
            && records.Y.Length == count
            && records.Gid.Length == count
            && records.DepthSlot.Length == count;
    }

    /// <summary>
    /// STRIP + RESUBMIT: for every placement, removes the baked tile from its flat layer
    /// (<see cref="TileMapComponent.RemoveTile"/>) and immediately re-adds the identical tile reference to
    /// the runtime sorted overlay (<see cref="TileMapComponent.AddSortedOverlayTile"/>) keyed by
    /// <see cref="ComputeWallSortKey"/>. A placement whose (plane, x, y) no longer holds the exact gid the
    /// document predicted (already stripped, hand-edited map, out-of-range plane/coordinates, ...) is
    /// counted as a mismatch and skipped entirely (neither stripped nor resubmitted) rather than thrown -
    /// this is expected to be rare/zero on a converter-produced map, but must never crash world load.
    /// </summary>
    public static void Apply(TileMapComponent tileMapComponent, WallPlacementRecords records, string worldName)
    {
        var removedCount = 0;
        var mismatchCount = 0;

        for (var i = 0; i < records.Count; i++)
        {
            var plane = records.Plane[i];
            var x = records.X[i];
            var y = records.Y[i];
            var expectedLocalTileId = records.Gid[i] - FirstGid;

            TileMapTileReference tileReference;
            try
            {
                tileReference = tileMapComponent.GetTileReference(plane, x, y);
            }
            catch (Exception)
            {
                mismatchCount++;
                continue;
            }

            if (tileReference.IsEmpty || tileReference.TileId != expectedLocalTileId)
            {
                mismatchCount++;
                continue;
            }

            tileMapComponent.RemoveTile(plane, x, y);
            removedCount++;

            var sortKey = ComputeWallSortKey(records.CellY[i], records.DepthSlot[i], i);
            tileMapComponent.AddSortedOverlayTile(tileReference, x, y, in sortKey);
        }

        if (mismatchCount > 0)
        {
            Logs.WriteError(
                $"WallPlacementOverlay: world '{worldName}' - {mismatchCount} of {records.Count} wall "
                + $"placements did not match the live tile map (removed {removedCount}); those tiles were "
                + "left exactly as loaded, un-interleaved.");
        }
    }

    /// <summary>
    /// Port of <c>GraphicManager.DepthWallBlock(tileY, wallSlot)</c>: <c>tileY * 16 + 7 + clamp(wallSlot, 0, 6)</c>.
    /// <paramref name="stableId"/> is this placement's index within its document - overlay tiles at the
    /// exact same (row, depth slot) only ever occupy different, non-overlapping grid cells, so this tiebreak
    /// never affects visible draw order; it only keeps <see cref="RenderSortKey2D.CompareTo"/> a strict,
    /// deterministic ordering instead of a documentless tie.
    /// </summary>
    internal static RenderSortKey2D ComputeWallSortKey(int cellY, int depthSlot, int stableId)
    {
        var elevation = cellY * RowStride + WallDepthBias + Math.Clamp(depthSlot, 0, 6);
        return new RenderSortKey2D(
            (int)RenderPass2D.YSortedWorld, SharedSortingLayer, 0, elevation, 0, 0, stableId);
    }

    /// <summary>
    /// Port of <c>GraphicManager.DepthEntity</c>'s row bucket only (<c>(depthSortValue &gt;&gt; 20) * 16 + 6</c>),
    /// where the original's own <c>depthSortValue</c> is
    /// <c>EntityManager.cs:1049</c>'s <c>PosY + (ImageDepthSortValue &lt;&lt; 16)</c> - <paramref name="idsv"/>
    /// is that per-frame image-depth-sort bias (<c>Data/sprite-records.json</c>'s "IdsvAnimDirs", see
    /// <c>SpriteWriter</c>'s class doc), 0 when the entity's current (anim, direction) has none. The
    /// original's finer <c>depthSortValue &amp; 0xffff</c> tiebreak is a screen-projected z, which this 2D
    /// engine has no equivalent input for at the proxy level - see <see cref="AlundraWorldProxy"/>'s class
    /// doc and the deviation note on <c>ApplyEntitySortKey</c> below for how the fine order is handled
    /// instead. <paramref name="posY"/> is the entity's logical Y (16.16 fixed, <c>AlundraEntityScriptProxy.PosY</c>).
    /// </summary>
    internal static int ComputeEntityElevation(int posY, int idsv)
    {
        return ((posY + (idsv << 16)) >> 20) * RowStride + EntityDepthSlot;
    }

    /// <summary>
    /// Applies <see cref="ComputeEntityElevation"/> to a spawned entity's <see cref="DepthSortable2DComponent"/>.
    /// Only <c>Elevation</c> is written: <c>RenderPass</c>/<c>SortingLayer</c>/<c>OrderInLayer</c> already carry
    /// their bank-prefab defaults (see <see cref="SharedSortingLayer"/>'s doc), and <c>SortCoordinate</c> stays
    /// entirely component-computed (<see cref="DepthSortMode2D.TopDownYUp"/>, from the entity's actual world
    /// position) - not settable from gameplay code without either a new engine hook (out of scope, engine is
    /// read-only for this slice) or re-deriving the world transform every frame (out of scope, a separate
    /// follow-up per <see cref="AlundraWorldProxy"/>'s class doc). DEVIATION from the original: the fine order
    /// within a row therefore comes from <c>-worldY</c> (which folds in <c>PosZ</c>'s elevation offset via
    /// <c>AlundraWorldProxy.ResolveWorldPosition</c>) instead of the original's raw <c>PosY</c> low bits -
    /// both still order correctly by row (the coarse <c>Elevation</c> bucket this method sets), only the
    /// tiebreak among entities sharing one row can diverge slightly from the original.
    ///
    /// SECOND DEVIATION: <paramref name="idsv"/> is only ever the CURRENT (CurrentAnimationId,
    /// AnimationDirection) pair's frame-0 IDSV value (see <see cref="AlundraWorldProxy.RunWallInterleaveSortKeyPass"/>),
    /// not the currently-displayed frame's own value. The original's animation player owns exact frame
    /// timing; this proxy does not track "which frame is currently showing" at all (see
    /// <c>AlundraWorldProxy.RunAnimationSyncPass</c>'s class doc - frame-level state is CasaEngine's own
    /// <c>Animation2dCompositionSampler</c>'s job). Measured across the full converted corpus (395 unique
    /// banks), 2851 of 9620 (anim, direction) pairs with displayed frames (about 30%) have a non-constant
    /// IDSV between frames, so this approximation can pick the wrong depth bucket mid-animation on almost a
    /// third of animations; per-frame fidelity (reading the sampler's actual current frame index) is a
    /// later refinement if this turns out to matter visually.
    /// </summary>
    public static void ApplyEntitySortKey(DepthSortable2DComponent depthSortable, int posY, int idsv)
    {
        depthSortable.Elevation = ComputeEntityElevation(posY, idsv);
    }
}
