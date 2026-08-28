using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraCellStore"/> against the REAL map 389 ("Ship Klark (beginning)") cell data -
/// same self-skip pattern as <see cref="AlundraCellsCollisionFieldTests"/> (skips when alundra-project/ is
/// absent from this checkout). docs/plan-e7-mutation-tuiles.md, slice E7.a, acceptance 2 and 4. Acceptance
/// 1 (synthetic, per-opcode dispatch) lives in <see cref="AlundraEventProgramRunnerTests"/>; acceptance 3
/// (the production call site, via the real headless intro harness) lives in
/// <see cref="AlundraCellStoreProductionTests"/>.
/// </summary>
public class AlundraCellStoreTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

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

    private static (AlundraCellsCollisionField Field, AlundraCellStore Store)? LoadMap389()
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

        var fieldCreated = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field, out var records);
        Assert.True(fieldCreated, "map 389's AlundraCells custom property should parse and match MapSize.");

        var storeCreated = AlundraCellStore.TryCreate(
            records!, tileMapData.MapSize.Width, tileMapData.MapSize.Height, WorldName, out var store);
        Assert.True(storeCreated, "map 389's tile_id/wall_tiles_offset columns should match cell_count.");

        return (field!, store!);
    }

    // -------------------- Acceptance 2: CopyCellRectangle (0x85) on real data --------------------

    [Fact]
    public void CopyCellRectangle_HatchTemplateOntoDoor_18_37_ReplacesStackAndClosesGroundProperty()
    {
        var loaded = LoadMap389();
        if (loaded == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var (field, store) = loaded.Value;

        // Real map-389 map-entry mutation (docs/intro-programs-389.txt): 0x85 [0,20,1,2,18,37] copies the
        // "closed hatch" template rows (0,20)/(0,21) onto the door cell (18,37)/(18,38). Export ships the
        // hatch OPEN - (18,37)'s stack tail is 53251/61/71 pre-mutation - the CLOSED template's is
        // 53249/59/69 (docs/plan-e7-mutation-tuiles.md §1 "Preuve chiffrée").
        var before1837 = store.GetWallTileStack(18, 37);
        Assert.NotNull(before1837);
        Assert.Equal(new[] { 12434, 12444, 53251, 53261, 53271 }, before1837!.Value.Tiles);

        var doorRow2Position = new Vector3(18 * 24, 38 * 16, 0f);
        var groundPropertyBefore = field.SampleGroundProperty(doorRow2Position);
        Assert.Equal(128, groundPropertyBefore); // export ships the hatch OPEN (gp bit 0x80 set).

        store.CopyCellRectangle(0, 20, 1, 2, 18, 37);

        var after1837 = store.GetWallTileStack(18, 37);
        Assert.NotNull(after1837);
        Assert.Equal(0, after1837!.Value.Offset);
        Assert.Equal(new[] { 12434, 12444, 53249, 53259, 53269 }, after1837.Value.Tiles);

        // (18,38) is the SECOND row of the same rectangle (dstY=37, height=2 -> rows 37 and 38) - its
        // ground_property closes from 128 (checked above, pre-mutation) to 0 (template row (0,21)'s own
        // gp), read through the SAME shared field instance (proves aliasing, not a copy - see
        // AlundraCellStore's own class doc).
        Assert.Equal(0, field.SampleGroundProperty(doorRow2Position));
    }

    [Fact]
    public void CopyCellRectangle_HatchTemplateOntoDoor_21_27_ChangesStackShape()
    {
        var loaded = LoadMap389();
        if (loaded == null)
        {
            return;
        }

        var (_, store) = loaded.Value;

        // Real map-389 map-entry mutation: 0x85 [0,39,1,2,21,27] - the exceptional pair (plan §1 "Exception
        // de forme"): (21,27)'s export stack has offset -1 and 6 tiles; its template (0,39)/(0,40) has
        // offset 0 and 7 tiles (one extra tile, 17166, at the top of the stack) - the destination's Tiles
        // array must be RESIZED (source longer than destination), not merely overwritten in place.
        var before2127 = store.GetWallTileStack(21, 27);
        Assert.NotNull(before2127);
        Assert.Equal(-1, before2127!.Value.Offset);
        Assert.Equal(6, before2127.Value.Tiles.Count);

        store.CopyCellRectangle(0, 39, 1, 2, 21, 27);

        var after2127 = store.GetWallTileStack(21, 27);
        Assert.NotNull(after2127);
        Assert.Equal(0, after2127!.Value.Offset);
        Assert.Equal(7, after2127.Value.Tiles.Count);
        Assert.Equal(17166, after2127.Value.Tiles[0]);
        Assert.Equal(new[] { 17166, 17176, 12388, 12398, 53249, 53259, 53269 }, after2127.Value.Tiles);
    }

    // -------------------- Acceptance 2: SetCellBits/ClearCellBits (0x54/0x55) round trip --------------

    [Fact]
    public void SetThenClearCellBits_RealCell_RoundTripsExactly()
    {
        var loaded = LoadMap389();
        if (loaded == null)
        {
            return;
        }

        var (field, store) = loaded.Value;

        // Cell (18,15): walkability 1, ground_property 0 (AlundraCellsCollisionFieldTests' own
        // Map389_Walkability_... fixture) - untouched by any other E7.a rectangle/bit test above.
        var position = new Vector3(18 * 24, 15 * 16, 0f);
        var walkabilityBefore = field.SampleRawWalkability(position);
        var groundPropertyBefore = field.SampleGroundProperty(position);
        Assert.Equal(1, walkabilityBefore);
        Assert.Equal(0, groundPropertyBefore);

        store.SetCellBits(18, 15, 0x10, 0x20);

        Assert.Equal(walkabilityBefore | 0x10, field.SampleRawWalkability(position));
        Assert.Equal(groundPropertyBefore | 0x20, field.SampleGroundProperty(position));

        store.ClearCellBits(18, 15, 0x10, 0x20);

        Assert.Equal(walkabilityBefore, field.SampleRawWalkability(position));
        Assert.Equal(groundPropertyBefore, field.SampleGroundProperty(position));
    }

    [Fact]
    public void SetCellBits_OutOfRangeCoordinates_ClampsToHardcodedBounds()
    {
        var loaded = LoadMap389();
        if (loaded == null)
        {
            return;
        }

        var (field, store) = loaded.Value;

        // (60,70) clamps to (0x33,0x3b) = (51,59) - map 389's own last row/column, NOT re-derived from its
        // actual 52x60 size (see AlundraCellStore.ApplyCellBits's own doc on why the two coincide here).
        var clampedPosition = new Vector3(0x33 * 24, 0x3b * 16, 0f);
        var before = field.SampleRawWalkability(clampedPosition);

        store.SetCellBits(60, 70, 0x08, 0);

        Assert.Equal(before | 0x08, field.SampleRawWalkability(clampedPosition));
    }

    // -------------------- Acceptance 4: shared-instance aliasing (height + walkability) --------------

    [Fact]
    public void CopyCellRectangle_MutatesHeightAndWalkability_VisibleThroughSameFieldInstance()
    {
        var loaded = LoadMap389();
        if (loaded == null)
        {
            return;
        }

        var (field, store) = loaded.Value;

        // (18,15): height 11, walkability 1. (17,16): height 12, walkability 0 - both flat (slope 4,
        // slope&3==0), untouched by every other E7.a test in this file/AlundraEventProgramRunnerTests.
        var position = new Vector3(17 * 24, 16 * 16, 0f);

        var foundBefore = field.TrySampleGround(position, 0f, out var sampleBefore);
        Assert.True(foundBefore);
        Assert.Equal(192f, sampleBefore.GroundHeight); // 12 * 16
        var foundBeforeMasked = field.TrySampleGround(position, 0f, 0x41u, out var sampleBeforeMasked);
        Assert.True(foundBeforeMasked);
        Assert.True(sampleBeforeMasked.IsWalkable); // walkability 0 -> (0 & 0x41) == 0.

        store.CopyCellRectangle(18, 15, 1, 1, 17, 16);

        var foundAfter = field.TrySampleGround(position, 0f, out var sampleAfter);
        Assert.True(foundAfter);
        Assert.Equal(176f, sampleAfter.GroundHeight); // 11 * 16 - changed, same field instance.
        var foundAfterMasked = field.TrySampleGround(position, 0f, 0x41u, out var sampleAfterMasked);
        Assert.True(foundAfterMasked);
        Assert.False(sampleAfterMasked.IsWalkable); // walkability now 1 -> (1 & 0x41) != 0 - changed too.
    }
}
