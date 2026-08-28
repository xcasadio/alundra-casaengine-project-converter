using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraCellsCollisionField"/>: tolerant parsing of the "AlundraCells" custom
/// property (synthetic, same shape as <see cref="WallPlacementOverlayTests"/>'s own parsing coverage),
/// <see cref="AlundraCellsCollisionField.WalkabilityMaskFor"/>'s ClassA/ClassB folding, and the ground
/// height / walkability rules against the REAL map 389 ("Ship Klark (beginning)") cell data - self-skips
/// when alundra-project/ is absent, same pattern as <see cref="Map389LoadProgramsTests"/>.
/// </summary>
public class AlundraCellsCollisionFieldTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // -------------------- Parsing (synthetic) --------------------

    [Fact]
    public void TryParse_MissingProperty_ReturnsFalse()
    {
        var customProperties = new Dictionary<string, string>();

        var result = AlundraCellsRecords.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsFalse()
    {
        var customProperties = new Dictionary<string, string> { ["AlundraCells"] = "{not valid json" };

        var result = AlundraCellsRecords.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryParse_ColumnLengthMismatch_ReturnsFalse()
    {
        var customProperties = new Dictionary<string, string>
        {
            ["AlundraCells"] = "{\"map_index\":1,\"cell_count\":4,\"walkability\":[0,0],\"ground_property\":[0,0,0,0],"
                + "\"slope\":[0,0,0,0],\"height\":[0,0,0,0]}",
        };

        var result = AlundraCellsRecords.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryCreate_WellFormedSyntheticGrid_ReturnsField()
    {
        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(2, 2);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":4,\"walkability\":[0,0,0,0],\"ground_property\":[0,0,0,0],"
            + "\"slope\":[0,0,0,0],\"height\":[1,2,3,4]}";

        var created = AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out var field);

        Assert.True(created);
        Assert.NotNull(field);
        var found = field!.TrySampleGround(new Vector3(0f, 0f, 0f), 0f, out var sample);
        Assert.True(found);
        Assert.Equal(16f, sample.GroundHeight);
    }

    [Fact]
    public void TryCreate_CellCountMismatchesMapSize_ReturnsFalse()
    {
        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(2, 2);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":9,\"walkability\":[0,0,0,0,0,0,0,0,0],"
            + "\"ground_property\":[0,0,0,0,0,0,0,0,0],\"slope\":[0,0,0,0,0,0,0,0,0],"
            + "\"height\":[0,0,0,0,0,0,0,0,0]}";

        var created = AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out var field);

        Assert.False(created);
        Assert.Null(field);
    }

    /// <summary>
    /// E7.b (docs/plan-e7-mutation-tuiles.md, acceptance item 9, picking up an E7.a deferral): the
    /// pre-fill of all 256 possible <c>_surfaceTagCache</c> entries (not just the GroundProperty values
    /// present at load) had no test. A value ABSENT from the synthetic document at load (only 5 is
    /// present) is introduced purely by a mutation - <see cref="AlundraCellStore.SetCellBits"/> OR-ing in
    /// 200, giving 205 - and must still read back as the surface tag "205", not the pre-fix "" fallback.
    /// </summary>
    [Fact]
    public void TrySampleGround_SurfaceTag_PrefilledForValueOnlyIntroducedByMutation()
    {
        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(1, 1);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":1,\"walkability\":[0],\"ground_property\":[5],"
            + "\"slope\":[0],\"height\":[0],\"tile_id\":[65535],\"wall_tiles_offset\":[0]}";

        var created = AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out var field, out var records);
        Assert.True(created);

        var storeCreated = AlundraCellStore.TryCreate(records!, 1, 1, "map_1", out var store);
        Assert.True(storeCreated);

        var position = new Vector3(0f, 0f, 0f);
        var foundBefore = field!.TrySampleGround(position, 0f, out var sampleBefore);
        Assert.True(foundBefore);
        Assert.Equal("5", sampleBefore.SurfaceTag);

        // 5 | 200 = 205 - never present in the document, so pre-E7.a's "only what was seen at load" cache
        // would have missed it and fallen back to "".
        store!.SetCellBits(0, 0, 0, 200);

        var foundAfter = field.TrySampleGround(position, 0f, out var sampleAfter);
        Assert.True(foundAfter);
        Assert.Equal("205", sampleAfter.SurfaceTag);
    }

    // -------------------- WalkabilityMaskFor --------------------

    [Fact]
    public void WalkabilityMaskFor_NoClassBits_ReturnsBaseMask()
    {
        Assert.Equal(0x40u, AlundraCellsCollisionField.WalkabilityMaskFor(0));
    }

    [Fact]
    public void WalkabilityMaskFor_ClassB_AddsBit0()
    {
        Assert.Equal(0x41u, AlundraCellsCollisionField.WalkabilityMaskFor(EntityFlags.ClassB));
    }

    [Fact]
    public void WalkabilityMaskFor_ClassA_AddsBit12()
    {
        Assert.Equal(0x1040u, AlundraCellsCollisionField.WalkabilityMaskFor(EntityFlags.ClassA));
    }

    [Fact]
    public void WalkabilityMaskFor_ClassAAndClassB_CombinesBoth()
    {
        Assert.Equal(0x1041u, AlundraCellsCollisionField.WalkabilityMaskFor(EntityFlags.ClassA | EntityFlags.ClassB));
    }

    // -------------------- Real map 389 data --------------------

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static AlundraCellsCollisionField? LoadMap389Field()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return null;
        }

        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");

        if (!File.Exists(tileMapPath))
        {
            return null;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));

        var created = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "map 389's AlundraCells custom property should parse and match MapSize.");
        return field;
    }

    [Fact]
    public void Map389_FlatCell_ReturnsHeightTimes16()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        // Cell (18, 57): walkability 0, ground_property 0, slope 4 (slope & 3 == 0, flat), height 5.
        var found = field.TrySampleGround(new Vector3(18 * 24, 57 * 16, 0f), 0f, out var sample);

        Assert.True(found);
        Assert.Equal(80f, sample.GroundHeight);
    }

    [Fact]
    public void Map389_Stairs_HeightVariesWithinCellByY()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (13, 27): slope 5 (slope & 3 == 1, stairs), height 10.
        var atRowTop = field.TrySampleGround(new Vector3(13 * 24, 27 * 16 + 0, 0f), 0f, out var sampleTop);
        var atRowPlus8 = field.TrySampleGround(new Vector3(13 * 24, 27 * 16 + 8, 0f), 0f, out var samplePlus8);

        Assert.True(atRowTop);
        Assert.Equal(160f, sampleTop.GroundHeight);
        Assert.True(atRowPlus8);
        Assert.Equal(152f, samplePlus8.GroundHeight);
    }

    [Fact]
    public void Map389_LadderEntering_HeightVariesWithinCellByX()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (17, 40): slope 6 (slope & 3 == 2, ladder entering), height 8.
        var atColumn0 = field.TrySampleGround(new Vector3(17 * 24 + 0, 40 * 16, 0f), 0f, out var sample0);
        var atColumn23 = field.TrySampleGround(new Vector3(17 * 24 + 23, 40 * 16, 0f), 0f, out var sample23);

        Assert.True(atColumn0);
        Assert.Equal(128f, sample0.GroundHeight);
        Assert.True(atColumn23);
        Assert.Equal(113f, sample23.GroundHeight);
    }

    [Fact]
    public void Map389_LadderExiting_HeightVariesWithinCellByX()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (18, 48): slope 7 (slope & 3 == 3, ladder exiting), height 6.
        var atColumn0 = field.TrySampleGround(new Vector3(18 * 24 + 0, 48 * 16, 0f), 0f, out var sample0);
        var atColumn23 = field.TrySampleGround(new Vector3(18 * 24 + 23, 48 * 16, 0f), 0f, out var sample23);

        Assert.True(atColumn0);
        Assert.Equal(81f, sample0.GroundHeight);
        Assert.True(atColumn23);
        Assert.Equal(96f, sample23.GroundHeight);
    }

    [Fact]
    public void Map389_Walkability_BaseMaskWalkable_ClassBMaskNotWalkable()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (18, 15): walkability 1, ground_property 0.
        var position = new Vector3(18 * 24, 15 * 16, 0f);

        var foundBase = field.TrySampleGround(position, 0f, 0x40u, out var sampleBase);
        var foundClassB = field.TrySampleGround(position, 0f, 0x41u, out var sampleClassB);

        Assert.True(foundBase);
        Assert.True(sampleBase.IsWalkable);
        Assert.True(foundClassB);
        Assert.False(sampleClassB.IsWalkable);
    }

    [Fact]
    public void Map389_GroundPropertyBit_MaskedByClassA_NotWalkable()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (18, 38): walkability 0, ground_property 128.
        var position = new Vector3(18 * 24, 38 * 16, 0f);

        var found = field.TrySampleGround(position, 0f, 0x8040u, out var sample);

        Assert.True(found);
        Assert.False(sample.IsWalkable);
    }

    [Fact]
    public void Map389_OutOfGrid_ClampsToNearestCell_HasGroundTrue()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        var found = field.TrySampleGround(new Vector3(-10f, 5000f, 0f), 0f, out var sample);

        Assert.True(found);
        Assert.True(sample.HasGround);
    }

    [Fact]
    public void Map389_PositionBelowSurface_ReturnsGroundHeightAboveSample()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        // Cell (18, 57), flat height 5 -> ground at 80px; sampled from Z = 10 (below the surface).
        var found = field.TrySampleGround(new Vector3(18 * 24, 57 * 16, 10f), 0f, out var sample);

        Assert.True(found);
        Assert.Equal(80f, sample.GroundHeight);
    }

    [Fact]
    public void Map389_MaxDropZero_SameResultAsUnbounded()
    {
        var field = LoadMap389Field();
        if (field == null)
        {
            return;
        }

        var position = new Vector3(18 * 24, 57 * 16, 200f);

        var foundZero = field.TrySampleGround(position, 0f, out var sampleZero);
        var foundLarge = field.TrySampleGround(position, 10000f, out var sampleLarge);

        Assert.True(foundZero);
        Assert.True(foundLarge);
        Assert.Equal(sampleLarge.GroundHeight, sampleZero.GroundHeight);
    }
}
