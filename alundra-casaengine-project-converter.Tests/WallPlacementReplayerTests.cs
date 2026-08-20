using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Pure-logic tests for <see cref="WallPlacementReplayer"/> against a small synthetic 1-wide,
/// 4-tall map, independent of any file I/O. Column x=0 throughout, so every target is (0, y).
///
/// Wall stacks (all Offset relative to sourceY - cellHeight, targetY = sourceY - height - offset +
/// stackIndex + 1):
///  - cell (0,0): Offset 0,  raw 10  -> targetY = 1  (lands on plane 0)
///  - cell (0,1): Offset -1, raw 200 -> targetY = 3  (lands on plane 0)
///  - cell (0,2): Offset 0,  raw 350 -> targetY = 3  (collides with the tile above - plane 0's
///    target cell is already taken, so this one is pushed to plane 1)
///  - cell (0,3): Offset 5,  raw 40  -> targetY = -1 (out of bounds - dropped, never recorded)
/// </summary>
public class WallPlacementReplayerTests
{
    private const int MapIndex = 7;
    private const int Width = 1;
    private const int Height = 4;

    [Fact]
    public void Replay_PacksFloorFreeWallStacks_WithCollisionAndOutOfBoundsDrop()
    {
        var floorTileId = new[] { 0xffff, 0xffff, 0xffff, 0xffff };
        var cellHeight = new[] { 0, 0, 0, 0 };
        var wallTiles = new Dictionary<string, WallTileStack>
        {
            ["0"] = new WallTileStack { Offset = 0, Tiles = new[] { 10 } },
            ["1"] = new WallTileStack { Offset = -1, Tiles = new[] { 200 } },
            ["2"] = new WallTileStack { Offset = 0, Tiles = new[] { 350 } },
            ["3"] = new WallTileStack { Offset = 5, Tiles = new[] { 40 } },
        };
        var gidByRawTileId = new Dictionary<int, int> { [10] = 101, [200] = 102, [350] = 103, [40] = 104 };

        var result = WallPlacementReplayer.Replay(MapIndex, Width, Height, floorTileId, cellHeight, wallTiles, gidByRawTileId);

        Assert.Equal(4, result.StacksCovered);
        Assert.Equal(3, result.ExpectedEmitted);

        var document = result.Document;
        Assert.Equal(MapIndex, document.MapIndex);
        Assert.Equal(3, document.Count);

        Assert.Equal(new[] { 0, 0, 0 }, document.CellX);
        Assert.Equal(new[] { 0, 1, 2 }, document.CellY);
        Assert.Equal(new[] { 0, 0, 0 }, document.StackIndex);
        Assert.Equal(new[] { 0, 0, 1 }, document.Plane); // the collision forces the 3rd tile to plane 1.
        Assert.Equal(new[] { 0, 0, 0 }, document.X);
        Assert.Equal(new[] { 1, 3, 3 }, document.Y);
        Assert.Equal(new[] { 101, 102, 103 }, document.Gid);
        Assert.Equal(new[] { 0, 1, 2 }, document.DepthSlot); // (rawId & 0x3ff) / 160: 10/160=0, 200/160=1, 350/160=2.

        // At least one placement on plane 0 and one on plane >= 1: the first-free-plane replay is
        // exercised, not trivially always-plane-0.
        Assert.Contains(0, document.Plane);
        Assert.Contains(document.Plane, plane => plane >= 1);
    }

    [Fact]
    public void Replay_EmitsFloorPlacements_OnlyForElevatedCells()
    {
        // 1-wide, 4-tall map. Cell (0,0): Height 0, floor raw 10 -> targetY = 0 (flat; not recorded by
        // itself, but promoted by the CLOSURE pass below since it shares position (0,0) with the
        // elevated placement). Cell (0,2): Height 2, floor raw 300 -> targetY = 0 (elevated, recorded,
        // depth slot 300/160=1). Cell (0,3): Height 0, no floor tile at all (EmptyTileId).
        var floorTileId = new[] { 10, 0xffff, 300, 0xffff };
        var cellHeight = new[] { 0, 0, 2, 0 };
        var wallTiles = new Dictionary<string, WallTileStack>();
        var gidByRawTileId = new Dictionary<int, int> { [10] = 101, [300] = 103 };

        var result = WallPlacementReplayer.Replay(MapIndex, Width, Height, floorTileId, cellHeight, wallTiles, gidByRawTileId);

        Assert.Equal(1, result.FloorExpectedEmitted);

        // CLOSURE: (0,0) already holds a placement (the elevated floor, on plane 1), so cell (0,0)'s
        // own flat Height-0 floor (plane 0, same position) must be promoted too, or the position would
        // draw with one plane depth-sorted and the other stuck flat.
        Assert.Equal(1, result.FloorConflictEmitted);
        Assert.Empty(result.ResidualConflicts);

        var floorDocument = result.FloorDocument;
        Assert.Equal(MapIndex, floorDocument.MapIndex);
        Assert.Equal(2, floorDocument.Count);
        Assert.Equal(new[] { 0, 0 }, floorDocument.CellX);
        Assert.Equal(new[] { 2, 0 }, floorDocument.CellY);
        // Collides with cell (0,0)'s flat floor (raw 10, target (0,0)) on plane 0, so it is pushed to
        // plane 1 - not trivially always plane 0.
        Assert.Equal(new[] { 1, 0 }, floorDocument.Plane);
        Assert.Equal(new[] { 0, 0 }, floorDocument.X);
        Assert.Equal(new[] { 0, 0 }, floorDocument.Y); // sourceY(2) - height(2) = 0; cell (0,0) itself.
        Assert.Equal(new[] { 103, 101 }, floorDocument.Gid);
        Assert.Equal(new[] { 1, 0 }, floorDocument.DepthSlot); // 300/160 = 1; 10/160 = 0.
    }

    [Fact]
    public void Replay_ClosurePromotesCoLocatedHeightZeroFloor_WhenAWallSharesItsBakePosition()
    {
        // 1-wide, 2-tall map. Cell (0,0): a wall stack (Offset 0) whose single tile (raw 200) targets
        // Y = 0 - 0 - 0 + 0 + 1 = 1, landing on plane 0 first (row 0 is replayed before row 1). Cell
        // (0,1): Height 0, floor raw 10 -> targetY = 1 too (flat by itself), colliding with the wall's
        // plane 0 slot and getting pushed to plane 1. Because (0,1) already holds the wall placement,
        // the co-located Height-0 floor on plane 1 must close too.
        var floorTileId = new[] { 0xffff, 10 };
        var cellHeight = new[] { 0, 0 };
        var wallTiles = new Dictionary<string, WallTileStack>
        {
            ["0"] = new WallTileStack { Offset = 0, Tiles = new[] { 200 } },
        };
        var gidByRawTileId = new Dictionary<int, int> { [10] = 101, [200] = 102 };

        var result = WallPlacementReplayer.Replay(MapIndex, Width, 2, floorTileId, cellHeight, wallTiles, gidByRawTileId);

        Assert.Equal(1, result.Document.Count);
        Assert.Equal(0, result.Document.CellX[0]);
        Assert.Equal(0, result.Document.CellY[0]);
        Assert.Equal(0, result.Document.Plane[0]); // the wall is replayed first, so it claims plane 0.
        Assert.Equal(1, result.Document.Y[0]);

        Assert.Equal(1, result.FloorConflictEmitted);
        Assert.Empty(result.ResidualConflicts);

        var floorDocument = result.FloorDocument;
        Assert.Equal(1, floorDocument.Count);
        Assert.Equal(new[] { 0 }, floorDocument.CellX); // owner cell of the closed-over floor tile.
        Assert.Equal(new[] { 1 }, floorDocument.CellY);
        Assert.Equal(new[] { 1 }, floorDocument.Plane); // pushed off plane 0 by the wall already there.
        Assert.Equal(new[] { 0 }, floorDocument.X);
        Assert.Equal(new[] { 1 }, floorDocument.Y);
        Assert.Equal(new[] { 101 }, floorDocument.Gid);
    }

    [Fact]
    public void FindResidualConflicts_DetectsLeftoverNonEmptyTile_NotItselfAPlacement()
    {
        // A crafted 2-plane, 1x1 grid: plane 0 holds the "placement" tile, plane 1 holds a leftover
        // non-empty tile that was never closed over - the artificial invariant violation.
        var layerDataByPlane = new List<int[]> { new[] { 101 }, new[] { 202 } };
        var placementPositions = new HashSet<(int X, int Y)> { (0, 0) };
        var placementPlanes = new HashSet<(int Plane, int X, int Y)> { (0, 0, 0) };

        var residuals = WallPlacementReplayer.FindResidualConflicts(
            layerDataByPlane, width: 1, placementPositions, placementPlanes);

        var residual = Assert.Single(residuals);
        Assert.Equal((1, 0, 0), residual);
    }

    [Fact]
    public void FindResidualConflicts_ReturnsEmpty_WhenEveryNonEmptyPlaneIsAPlacement()
    {
        var layerDataByPlane = new List<int[]> { new[] { 101 }, new[] { 202 } };
        var placementPositions = new HashSet<(int X, int Y)> { (0, 0) };
        var placementPlanes = new HashSet<(int Plane, int X, int Y)> { (0, 0, 0), (1, 0, 0) };

        var residuals = WallPlacementReplayer.FindResidualConflicts(
            layerDataByPlane, width: 1, placementPositions, placementPlanes);

        Assert.Empty(residuals);
    }

    [Fact]
    public void Replay_ElevatedFloorOutOfBounds_IsDroppedFromFloorExpectedEmitted()
    {
        // Cell (0,0): Height 5 pushes targetY negative -> out of bounds, dropped, never counted.
        var floorTileId = new[] { 10, 0xffff, 0xffff, 0xffff };
        var cellHeight = new[] { 5, 0, 0, 0 };
        var wallTiles = new Dictionary<string, WallTileStack>();
        var gidByRawTileId = new Dictionary<int, int> { [10] = 101 };

        var result = WallPlacementReplayer.Replay(MapIndex, Width, Height, floorTileId, cellHeight, wallTiles, gidByRawTileId);

        Assert.Equal(0, result.FloorExpectedEmitted);
        Assert.Equal(0, result.FloorDocument.Count);
    }

    [Theory]
    [InlineData(0, 0)]      // tileIndex 0 -> bucket 0
    [InlineData(159, 0)]    // last entry of bucket 0
    [InlineData(160, 1)]    // first entry of bucket 1
    [InlineData(959, 5)]    // last entry of the 960-entry table -> bucket 5
    [InlineData(960, 0)]    // past the table -> GetTileDepthSlot's own out-of-range fallback (0)
    [InlineData(0x3ff, 0)]  // 1023 & 0x3ff = 1023, past the table -> 0
    public void ComputeDepthSlot_MatchesGetTileDepthSlot(int tileIndexPart, int expectedSlot)
    {
        // High bits (palette, above the 0x3ff mask GetTileDepthSlot applies) must not affect the
        // result.
        var rawTileId = tileIndexPart | 0xf000;

        Assert.Equal(expectedSlot, WallPlacementReplayer.ComputeDepthSlot(rawTileId));
    }

    [Fact]
    public void Replay_MissingGidForReferencedRawTileId_Throws()
    {
        var floorTileId = new[] { 0xffff };
        var cellHeight = new[] { 0 };
        var wallTiles = new Dictionary<string, WallTileStack>
        {
            ["0"] = new WallTileStack { Offset = 0, Tiles = new[] { 999 } },
        };
        var gidByRawTileId = new Dictionary<int, int>(); // 999 is missing on purpose.

        Assert.Throws<InvalidOperationException>(() =>
            WallPlacementReplayer.Replay(MapIndex, Width, 1, floorTileId, cellHeight, wallTiles, gidByRawTileId));
    }

    [Fact]
    public void WallPlacementDocument_RoundTripsThroughSnakeCaseJson()
    {
        var floorTileId = new[] { 0xffff, 0xffff };
        var cellHeight = new[] { 0, 0 };
        var wallTiles = new Dictionary<string, WallTileStack>
        {
            ["0"] = new WallTileStack { Offset = 0, Tiles = new[] { 10 } },
        };
        var gidByRawTileId = new Dictionary<int, int> { [10] = 101 };
        var document = WallPlacementReplayer.Replay(MapIndex, Width, 2, floorTileId, cellHeight, wallTiles, gidByRawTileId).Document;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var json = JsonSerializer.Serialize(document, options);

        // The columnar-array schema used by AlundraWallPlacements: one field per array, snake_case
        // keys, matching AlundraCells' own convention.
        Assert.Contains("\"map_index\"", json);
        Assert.Contains("\"cell_x\"", json);
        Assert.Contains("\"cell_y\"", json);
        Assert.Contains("\"stack_index\"", json);
        Assert.Contains("\"plane\"", json);
        Assert.Contains("\"depth_slot\"", json);

        var roundTripped = JsonSerializer.Deserialize<WallPlacementDocument>(json, options)!;
        Assert.Equal(document.MapIndex, roundTripped.MapIndex);
        Assert.Equal(document.Count, roundTripped.Count);
        Assert.Equal(document.CellX, roundTripped.CellX);
        Assert.Equal(document.CellY, roundTripped.CellY);
        Assert.Equal(document.StackIndex, roundTripped.StackIndex);
        Assert.Equal(document.Plane, roundTripped.Plane);
        Assert.Equal(document.X, roundTripped.X);
        Assert.Equal(document.Y, roundTripped.Y);
        Assert.Equal(document.Gid, roundTripped.Gid);
        Assert.Equal(document.DepthSlot, roundTripped.DepthSlot);
    }

    [Fact]
    public void FloorPlacementDocument_RoundTripsThroughSnakeCaseJson()
    {
        var floorTileId = new[] { 0xffff, 300 };
        var cellHeight = new[] { 0, 1 };
        var wallTiles = new Dictionary<string, WallTileStack>();
        var gidByRawTileId = new Dictionary<int, int> { [300] = 103 };
        var document = WallPlacementReplayer.Replay(MapIndex, Width, 2, floorTileId, cellHeight, wallTiles, gidByRawTileId).FloorDocument;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var json = JsonSerializer.Serialize(document, options);

        // Columnar schema, same convention as AlundraWallPlacements - no "stack_index" column, floor
        // tiles have no stack.
        Assert.Contains("\"map_index\"", json);
        Assert.Contains("\"cell_x\"", json);
        Assert.Contains("\"cell_y\"", json);
        Assert.Contains("\"plane\"", json);
        Assert.Contains("\"depth_slot\"", json);
        Assert.DoesNotContain("\"stack_index\"", json);

        var roundTripped = JsonSerializer.Deserialize<FloorPlacementDocument>(json, options)!;
        Assert.Equal(document.MapIndex, roundTripped.MapIndex);
        Assert.Equal(document.Count, roundTripped.Count);
        Assert.Equal(document.CellX, roundTripped.CellX);
        Assert.Equal(document.CellY, roundTripped.CellY);
        Assert.Equal(document.Plane, roundTripped.Plane);
        Assert.Equal(document.X, roundTripped.X);
        Assert.Equal(document.Y, roundTripped.Y);
        Assert.Equal(document.Gid, roundTripped.Gid);
        Assert.Equal(document.DepthSlot, roundTripped.DepthSlot);
    }
}
