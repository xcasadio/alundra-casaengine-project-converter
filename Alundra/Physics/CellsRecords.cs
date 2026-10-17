#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Cells;

namespace Alundra.Physics;

/// <summary>
/// One map's "AlundraCells" custom property (see
/// <c>AlundraCasaEngineProjectConverter.Writers.CellMetadataWriter</c>/
/// <c>AlundraCasaEngineProjectConverter.Readers.CellMetadataReader</c>): per-cell gameplay data in
/// row-major CellOrder (index = y * width + x - <c>CellMetadataReader</c>'s own doc, matching
/// <c>PhysicsEngine.ComputeEntityGroundHeight</c>'s <c>tileY * mapWidth + tileX</c> lookup,
/// PhysicsEngine.cs:989). This project does not reference the converter, so the shape is duplicated
/// here rather than shared - same tolerant-parse pattern as <see cref="WallPlacementRecords"/>/
/// <see cref="WallPlacementOverlay.TryParse"/>, read back with the same
/// <c>JsonNamingPolicy.SnakeCaseLower</c> the converter serialized it with.
///
/// E7.a (docs/plan-e7-mutation-tuiles.md): <see cref="TileId"/>, <see cref="WallTilesOffset"/> and the
/// sparse <see cref="WallTiles"/> dictionary are now parsed too - <see cref="CellStore"/> needs
/// them to faithfully port <c>GameEngine.ChangeAreaTileProperties</c> (opcode 0x85), which copies both
/// alongside Walkability/GroundProperty/Slope/Height. The JSON also carries <c>palette</c>/<c>tile</c>/
/// <c>flags</c> columns, still silently ignored by <see cref="JsonSerializer"/>: <c>flags</c> in
/// particular is never read here on purpose - the original <c>MapTile.Flags</c> is a DERIVED property
/// (computed from the other fields, e.g. by <c>PhysicsEngine.UpdateTileAttributes</c>), never itself
/// copied by <c>ChangeAreaTileProperties</c> or the 0x54/0x55 handlers - so there is nothing for this
/// port to read it into.
/// </summary>
public sealed class CellsRecords
{
    public int MapIndex { get; init; }
    public int CellCount { get; init; }
    public int[] Walkability { get; init; } = Array.Empty<int>();
    public int[] GroundProperty { get; init; } = Array.Empty<int>();
    public int[] Slope { get; init; } = Array.Empty<int>();
    public int[] Height { get; init; } = Array.Empty<int>();
    public int[] TileId { get; init; } = Array.Empty<int>();
    public int[] WallTilesOffset { get; init; } = Array.Empty<int>();

    /// <summary>Sparse: only cells with an actual wall tile stack, keyed by cell index (as a string -
    /// JSON object keys must be strings, mirroring <c>CellMetadataDocument.WallTiles</c> on the
    /// converter side). <see cref="CellStore"/> re-indexes this into a dense per-cell array once,
    /// at construction, rather than every reader re-parsing string keys.</summary>
    public Dictionary<string, WallTileStack> WallTiles { get; init; } = new();

    private const string CustomPropertyKey = "AlundraCells";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Best-effort parse of <see cref="CustomPropertyKey"/> off <paramref name="customProperties"/>,
    /// same tolerant shape as <see cref="WallPlacementOverlay.TryParse"/>: a missing property,
    /// malformed JSON, or a document whose array lengths do not match its own <c>CellCount</c> all
    /// return false with a single warning logged - the caller installs no collision field for that
    /// world instead of throwing.
    /// </summary>
    public static bool TryParse(
        IReadOnlyDictionary<string, string> customProperties, string worldName, out CellsRecords records)
    {
        records = null!;

        if (!customProperties.TryGetValue(CustomPropertyKey, out var json) || string.IsNullOrEmpty(json))
        {
            Logs.WriteWarning(
                $"CellsCollisionField: world '{worldName}' has no '{CustomPropertyKey}' custom property; "
                + "collision field not installed (degraded mode).");
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CellsRecords>(json, SerializerOptions);
            if (parsed == null || !IsWellFormed(parsed))
            {
                Logs.WriteWarning(
                    $"CellsCollisionField: world '{worldName}' - '{CustomPropertyKey}' parsed to a "
                    + "malformed document (column length mismatch); collision field not installed (degraded mode).");
                return false;
            }

            records = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            Logs.WriteWarning(
                $"CellsCollisionField: world '{worldName}' - failed to parse '{CustomPropertyKey}' "
                + $"({ex.Message}); collision field not installed (degraded mode).");
            return false;
        }
    }

    private static bool IsWellFormed(CellsRecords records)
    {
        var count = records.CellCount;
        return records.Walkability.Length == count
            && records.GroundProperty.Length == count
            && records.Slope.Length == count
            && records.Height.Length == count;

        // TileId/WallTilesOffset are NOT checked here on purpose: AlundraCellsCollisionField (the only
        // reader of this well-formed gate) never touches them, and every synthetic document in this
        // codebase's own tests predates E7.a (no tile_id/wall_tiles_offset columns at all) - adding them
        // here would make those degrade to false. AlundraCellStore.TryCreate below validates their
        // length against cell_count itself, independently, since it is the one that actually needs them.
    }
}
