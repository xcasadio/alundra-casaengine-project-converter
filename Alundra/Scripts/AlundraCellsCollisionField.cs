#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// One map's "AlundraCells" custom property (see
/// <c>AlundraCasaEngineProjectConverter.Writers.CellMetadataWriter</c>/
/// <c>AlundraCasaEngineProjectConverter.Readers.CellMetadataReader</c>): per-cell gameplay data in
/// row-major CellOrder (index = y * width + x - <c>CellMetadataReader</c>'s own doc, matching
/// <c>PhysicsEngine.ComputeEntityGroundHeight</c>'s <c>tileY * mapWidth + tileX</c> lookup,
/// PhysicsEngine.cs:989). This project does not reference the converter, so the shape is duplicated
/// here rather than shared - same tolerant-parse pattern as <see cref="WallPlacementRecords"/>/
/// <see cref="WallPlacementOverlay.TryParse"/>, read back with the same
/// <c>JsonNamingPolicy.SnakeCaseLower</c> the converter serialized it with. Only the columns
/// <see cref="AlundraCellsCollisionField"/> actually reads are declared here; the JSON also carries
/// tile_id/palette/tile/wall_tiles_offset/wall_tiles, silently ignored by
/// <see cref="JsonSerializer"/>.
/// </summary>
public sealed class AlundraCellsRecords
{
    public int MapIndex { get; init; }
    public int CellCount { get; init; }
    public int[] Walkability { get; init; } = Array.Empty<int>();
    public int[] GroundProperty { get; init; } = Array.Empty<int>();
    public int[] Slope { get; init; } = Array.Empty<int>();
    public int[] Height { get; init; } = Array.Empty<int>();

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
        IReadOnlyDictionary<string, string> customProperties, string worldName, out AlundraCellsRecords records)
    {
        records = null!;

        if (!customProperties.TryGetValue(CustomPropertyKey, out var json) || string.IsNullOrEmpty(json))
        {
            Logs.WriteWarning(
                $"AlundraCellsCollisionField: world '{worldName}' has no '{CustomPropertyKey}' custom property; "
                + "collision field not installed (degraded mode).");
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AlundraCellsRecords>(json, SerializerOptions);
            if (parsed == null || !IsWellFormed(parsed))
            {
                Logs.WriteWarning(
                    $"AlundraCellsCollisionField: world '{worldName}' - '{CustomPropertyKey}' parsed to a "
                    + "malformed document (column length mismatch); collision field not installed (degraded mode).");
                return false;
            }

            records = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            Logs.WriteWarning(
                $"AlundraCellsCollisionField: world '{worldName}' - failed to parse '{CustomPropertyKey}' "
                + $"({ex.Message}); collision field not installed (degraded mode).");
            return false;
        }
    }

    private static bool IsWellFormed(AlundraCellsRecords records)
    {
        var count = records.CellCount;
        return records.Walkability.Length == count
            && records.GroundProperty.Length == count
            && records.Slope.Length == count
            && records.Height.Length == count;
    }
}

/// <summary>
/// <see cref="ICollisionField"/> over one map's <see cref="AlundraCellsRecords"/>, in the LOGICAL
/// space E3.a established (X east, Y depth, Z elevation - <c>up = Vector3.UnitZ</c>). Built once per
/// world by <see cref="AlundraWorldProxy.InitializeWithWorld"/> from the loaded
/// <see cref="TileMapData"/> and installed as <c>World.CollisionField</c>.
///
/// Every rule below is a port of the FIRST-CORNER branch of
/// <c>PhysicsEngine.ComputeEntityGroundHeight</c> @ 0x800370c4 (PhysicsEngine.cs:956-1073) - the
/// original samples all 4 corners of an entity's footprint and takes the highest, accumulating
/// <c>slopesHit</c> so a later corner of a multi-cell sloped footprint gets +16 instead of
/// re-deriving its own slope formula (PhysicsEngine.cs:1021-1024, :1037-1040, :1053-1056); this field
/// samples a single point and never applies that +16 bump - E3.c's mover takes the max of 4 corner
/// samples instead, which reproduces the "highest corner wins" outcome but not the exact per-corner
/// value on a sloped multi-cell footprint. Documented deviation, not reproduced here.
///
/// Further documented deviations from the generic <see cref="ICollisionField"/> contract (all carried
/// over from the original, which never treats "no ground" as a real state - see
/// <c>PhysicsEngine.GetCollisionFlags</c>/<c>GetCollisionFlagsWithPlayer</c>, which decide walkability
/// from <see cref="GroundSample.IsWalkable"/> and a separate height compare, never from "ground
/// missing"):
/// <list type="bullet">
/// <item><description><see cref="GroundSample.HasGround"/> is always true, even for a position outside
/// the grid (clamped to the nearest edge cell, PhysicsEngine.cs:979-989) - a point off the map still
/// has ground.</description></item>
/// <item><description><see cref="GroundSample.GroundHeight"/> is returned even when it is above the
/// sampled position; the original's mover decides on top of it
/// (<c>IsOnGround = FloorHeight &gt;= PosZ</c>, landing by clamp, walking blocked past 3px) - deferred
/// to E3.c.</description></item>
/// <item><description><c>maxDropDistance</c> is ignored - the original has no fall-distance
/// limit.</description></item>
/// </list>
/// </summary>
public sealed class AlundraCellsCollisionField : ICollisionField
{
    /// <summary>
    /// Base walkability mask used by the mask-less <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/>
    /// overload - <c>PhysicsEngine.GetCollisionFlags</c>' own default before ClassA/ClassB are folded in
    /// (PhysicsEngine.cs:1139).
    /// </summary>
    public const uint BaseWalkabilityMask = 0x40;

    // StaticVariables.MapTileWidth/MapTileHeight (StaticVariables.cs:57-58).
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _walkability;
    private readonly int[] _groundProperty;
    private readonly int[] _slope;
    private readonly int[] _cellHeight;
    private readonly string?[] _surfaceTagCache = new string?[256];

    private AlundraCellsCollisionField(int width, int height, AlundraCellsRecords records)
    {
        _width = width;
        _height = height;
        _walkability = records.Walkability;
        _groundProperty = records.GroundProperty;
        _slope = records.Slope;
        _cellHeight = records.Height;

        // Precompute every distinct SurfaceTag string once, here, so TrySampleGround never allocates.
        foreach (var raw in _groundProperty)
        {
            var groundProperty = raw & 0xFF;
            _surfaceTagCache[groundProperty] ??= groundProperty.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Parses <paramref name="tileMapData"/>'s "AlundraCells" custom property and, when it is present,
    /// well formed, and its cell_count matches <paramref name="tileMapData"/>'s own
    /// <see cref="TileMapData.MapSize"/> (52x60 / 3120 cells on map 389), returns the field. Any
    /// failure - parse or size mismatch - logs a single warning and returns false: degraded mode, no
    /// field installed for that world.
    /// </summary>
    public static bool TryCreate(TileMapData tileMapData, string worldName, out AlundraCellsCollisionField? field)
    {
        field = null;

        if (!AlundraCellsRecords.TryParse(tileMapData.CustomProperties, worldName, out var records))
        {
            return false;
        }

        var width = tileMapData.MapSize.Width;
        var height = tileMapData.MapSize.Height;
        var expectedCellCount = width * height;

        if (records.CellCount != expectedCellCount || records.Walkability.Length != expectedCellCount)
        {
            Logs.WriteWarning(
                $"AlundraCellsCollisionField: world '{worldName}' - AlundraCells cell_count "
                + $"({records.CellCount}) does not match TileMapData.MapSize ({width}x{height}="
                + $"{expectedCellCount}); collision field not installed (degraded mode).");
            return false;
        }

        field = new AlundraCellsCollisionField(width, height, records);
        return true;
    }

    /// <summary>
    /// <c>0x40 | (ClassB ? 0x01 : 0) | (ClassA ? 0x1000 : 0)</c> - port of the per-entity mask built at
    /// the top of <c>PhysicsEngine.GetCollisionFlagsWithPlayer</c> (PhysicsEngine.cs:1085-1099) and
    /// <c>PhysicsEngine.GetCollisionFlags</c> (PhysicsEngine.cs:1139-1149): both start from 0x40, widen
    /// to 0x41 when the entity carries <see cref="EntityFlags.ClassB"/> (bit 3, 0x8), and OR in 0x1000
    /// when it carries <see cref="EntityFlags.ClassA"/> (bit 0, 0x1).
    /// </summary>
    public static uint WalkabilityMaskFor(uint entityFlags)
    {
        var mask = BaseWalkabilityMask;

        if ((entityFlags & EntityFlags.ClassB) != 0)
        {
            mask |= 0x01;
        }

        if ((entityFlags & EntityFlags.ClassA) != 0)
        {
            mask |= 0x1000;
        }

        return mask;
    }

    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
        => TrySampleGround(worldPosition, maxDropDistance, BaseWalkabilityMask, out sample);

    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, uint walkabilityMask, out GroundSample sample)
    {
        var x = (int)MathF.Floor(worldPosition.X);
        var y = (int)MathF.Floor(worldPosition.Y);

        // cell = (x / 24, y / 16), clamped to [0, width-1] x [0, height-1] - PhysicsEngine.cs:979-989.
        // A point outside the grid therefore has ground (documented deviation, see class doc).
        var cellX = Math.Clamp(x / CellWidthPx, 0, _width - 1);
        var cellY = Math.Clamp(y / CellHeightPx, 0, _height - 1);
        var cellIndex = cellY * _width + cellX;

        var cellHeight = _cellHeight[cellIndex];
        var groundHeight = ComputeGroundHeight(_slope[cellIndex] & 0x3, cellHeight, x, y);

        var walkability = (uint)_walkability[cellIndex];
        var groundProperty = _groundProperty[cellIndex];
        var tileFlags = walkability | ((uint)groundProperty << 8);
        var isWalkable = (tileFlags & walkabilityMask) == 0;
        var surfaceTag = _surfaceTagCache[groundProperty & 0xFF];

        sample = new GroundSample(groundHeight, Vector3.UnitZ, isWalkable, surfaceTag ?? string.Empty);
        return true;
    }

    /// <summary>
    /// Raw per-cell <c>GroundProperty</c> (E1, docs/plan-echelles-chiffrage.md É1) - additive numeric
    /// accessor next to <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/>, which already
    /// reads this exact same <c>_groundProperty[cellIndex]</c> internally but, until now, only exposed it
    /// through <see cref="GroundSample.SurfaceTag"/>'s string form (<c>_surfaceTagCache</c>). Needed
    /// because <c>PhysicsEngine.UpdateTileAttributes</c>'s <c>Slope_18c</c> four-corner rule
    /// (PhysicsEngine.cs:1706-1826, ported in <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>)
    /// reads <c>GroundProperty</c> bits directly, not a surface-tag string. Same clamp-to-nearest-cell
    /// convention as <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/> (never fails -
    /// see that method's own doc on the out-of-grid deviation); does not touch any existing member.
    /// </summary>
    public int SampleGroundProperty(in Vector3 worldPosition)
    {
        var x = (int)MathF.Floor(worldPosition.X);
        var y = (int)MathF.Floor(worldPosition.Y);

        var cellX = Math.Clamp(x / CellWidthPx, 0, _width - 1);
        var cellY = Math.Clamp(y / CellHeightPx, 0, _height - 1);
        var cellIndex = cellY * _width + cellX;

        return _groundProperty[cellIndex];
    }

    /// <summary>
    /// Raw per-cell <c>Height</c> (E3, docs/plan-echelles-chiffrage.md É3) - additive numeric accessor
    /// next to <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/>, exposing this exact
    /// same <c>_cellHeight[cellIndex]</c> BEFORE <see cref="ComputeGroundHeight"/>'s per-slope
    /// interpolation is applied (cell units - 1 unit = 16 px; NOT the interpolated pixel height
    /// <see cref="GroundSample.GroundHeight"/> returns). Needed because
    /// <c>EntityGameplayManager.GetTileHeightAtOffset</c> (EntityGameplayManager.cs:277-345) reads
    /// <c>tile.Height</c> directly and never interpolates by slope - unlike
    /// <c>PhysicsEngine.ComputeEntityGroundHeight</c> (PhysicsEngine.cs:1007-1061, the function
    /// <see cref="ComputeGroundHeight"/> actually ports), which is a DIFFERENT original function that
    /// happens to read the same per-cell table. Same clamp-to-nearest-cell convention as
    /// <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/> (never fails - see that
    /// method's own doc on the out-of-grid deviation); does not touch any existing member.
    /// </summary>
    public int SampleRawCellHeight(in Vector3 worldPosition)
    {
        var x = (int)MathF.Floor(worldPosition.X);
        var y = (int)MathF.Floor(worldPosition.Y);

        var cellX = Math.Clamp(x / CellWidthPx, 0, _width - 1);
        var cellY = Math.Clamp(y / CellHeightPx, 0, _height - 1);
        var cellIndex = cellY * _width + cellX;

        return _cellHeight[cellIndex];
    }

    /// <summary>
    /// Height (px) of one point, ported from the single-corner body of the
    /// <c>switch (tile.Slope &amp; 0x3)</c> in <c>ComputeEntityGroundHeight</c>
    /// (PhysicsEngine.cs:1009-1061), without the <c>slopesHit</c> multi-corner +16 bump (see class
    /// doc).
    /// </summary>
    private static float ComputeGroundHeight(int slopeKind, int cellHeight, int x, int y)
    {
        switch (slopeKind)
        {
            case 1: // Stairs up/down - PhysicsEngine.cs:1014-1019.
            {
                var yMod = Mod(y, CellHeightPx);
                return (cellHeight - 1) * CellHeightPx + CellHeightPx - yMod;
            }

            case 2: // Ladder entering / stair side down - PhysicsEngine.cs:1030-1035.
            {
                var xIndex = Mod(23 - Mod(x, CellWidthPx), CellWidthPx);
                return (cellHeight - 1) * CellHeightPx + AnimationTables.HeightsTable_800236d4[xIndex];
            }

            case 3: // Ladder exiting - PhysicsEngine.cs:1046-1051.
            {
                var xIndex = Mod(x, CellWidthPx);
                return (cellHeight - 1) * CellHeightPx + AnimationTables.HeightsTable_800236d4[xIndex];
            }

            default: // Normal tile - PhysicsEngine.cs:1011-1012 (slopeKind == 0).
                return cellHeight * CellHeightPx;
        }
    }

    /// <summary>Positive modulo (C#'s <c>%</c> keeps the dividend's sign; the table indices below never do).</summary>
    private static int Mod(int value, int modulus)
    {
        var m = value % modulus;
        return m < 0 ? m + modulus : m;
    }
}
