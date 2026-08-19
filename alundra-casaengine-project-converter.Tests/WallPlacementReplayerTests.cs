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
}
