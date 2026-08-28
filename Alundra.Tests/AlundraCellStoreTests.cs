using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraCellStore"/> against the REAL map 389 ("Ship Klark (beginning)") cell data.
/// docs/plan-e7-mutation-tuiles.md, slice E7.a, acceptance 2 and 4. Acceptance 1 (synthetic, per-opcode
/// dispatch) lives in <see cref="AlundraEventProgramRunnerTests"/>; acceptance 3 (the production call
/// site, via the real headless intro harness) lives in <see cref="AlundraCellStoreProductionTests"/>.
///
/// E7.b (docs/plan-e7-mutation-tuiles.md, acceptance item 9, picking up an E7.a deferral): these used to
/// self-skip silently when alundra-project/ was absent from the checkout - the same pattern every other
/// real-data test in this project used, but one plan-oracle-heros.md §2.8 already flagged as wrong for
/// this kind of test (a self-skip can hide these 5 tests going silently unrun for a long time). They now
/// throw, naming the missing export, instead of self-skipping.
/// </summary>
public class AlundraCellStoreTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    private static string FindProjectRoot()
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

        throw new InvalidOperationException(
            $"AlundraCellStoreTests: no 'alundra-project/Maps' directory found above '{AppContext.BaseDirectory}' - "
            + "these tests need the real converter export of map 389 and cannot self-skip without one "
            + "(docs/plan-e7-mutation-tuiles.md, slice E7.b, acceptance item 9).");
    }

    private static (AlundraCellsCollisionField Field, AlundraCellStore Store) LoadMap389()
    {
        var projectRoot = FindProjectRoot();

        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");

        if (!File.Exists(tileMapPath))
        {
            throw new InvalidOperationException(
                $"AlundraCellStoreTests: '{tileMapPath}' not found - the real map 389 export is incomplete "
                + "in this checkout.");
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
        var (field, store) = LoadMap389();

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
        var (_, store) = LoadMap389();

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
        var (field, store) = LoadMap389();

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
        var (field, store) = LoadMap389();

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
        var (field, store) = LoadMap389();

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

    // -------------------- E7.b acceptance item 9: E7.a deferrals picked up (synthetic) --------------

    private static AlundraCellStore NewSyntheticStoreWithMalformedWallTilesKey()
    {
        var tileMapData = new TileMapData();
        tileMapData.MapSize = new CasaEngine.Core.Math.Size(1, 1);
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":1,\"walkability\":[0],\"ground_property\":[0],"
            + "\"slope\":[0],\"height\":[0],\"tile_id\":[0],\"wall_tiles_offset\":[0],"
            + "\"wall_tiles\":{\"not_a_number\":{\"offset\":0,\"tiles\":[1]}}}";

        var fieldCreated = AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out _, out var records);
        Assert.True(fieldCreated);

        var storeCreated = AlundraCellStore.TryCreate(records!, 1, 1, "map_1", out var store);
        Assert.True(storeCreated);
        return store!;
    }

    /// <summary>
    /// A "wall_tiles" key that is not a valid cell index used to be silently dropped (E7.a). It now also
    /// logs a warning (AlundraCellStore's own constructor) - this codebase has no test-capturable log
    /// sink to assert the message text against, so this test covers the BEHAVIOR the warning documents:
    /// the malformed entry is dropped (no stack for cell (0,0)) and construction never throws.
    /// </summary>
    [Fact]
    public void TryCreate_MalformedWallTilesKey_DropsThatStackWithoutThrowing()
    {
        var store = NewSyntheticStoreWithMalformedWallTilesKey();

        Assert.Null(store.GetWallTileStack(0, 0));
    }

    /// <summary>
    /// E7.b: <see cref="AlundraCellStore.GetWallTileStack"/> used to hand back its own live backing array
    /// (or an <see cref="ArraySegment{T}"/> over it) whenever <c>Count == Tiles.Length</c> - a caller could
    /// mutate the store's own state through what looks like a read-only accessor. It must now always
    /// return an independent copy.
    /// </summary>
    [Fact]
    public void GetWallTileStack_ReturnsIndependentCopy_NotTheLiveBackingArray()
    {
        var (_, store) = LoadMap389();

        var first = store.GetWallTileStack(18, 37);
        Assert.NotNull(first);
        var tiles = (int[])first!.Value.Tiles;
        tiles[0] = -12345; // mutate the returned array in place.

        var second = store.GetWallTileStack(18, 37);
        Assert.NotNull(second);
        Assert.NotEqual(-12345, second!.Value.Tiles[0]); // the store's own state must be unaffected.
        Assert.Equal(12434, second.Value.Tiles[0]); // still the real, unmutated value.
    }

    /// <summary>
    /// E7.b: <see cref="AlundraCellStore.CellsMutated"/>'s payload used to be allocated unconditionally,
    /// even with zero subscribers (both <c>CopyCellRectangle</c>'s <c>List&lt;int&gt;</c> and
    /// <c>ApplyCellBits</c>'s single-element array were built before the null-conditional invoke, which
    /// still evaluates its argument). Measured via <see cref="GC.GetAllocatedBytesForCurrentThread"/>: a
    /// tight loop of same-shape mutations with no subscriber must allocate near nothing per call.
    /// </summary>
    [Fact]
    public void CellMutations_NoSubscriber_AllocateNearNothingPerCall()
    {
        var (_, store) = LoadMap389();

        // Warm up (JIT, any one-time allocations) before measuring.
        store.CopyCellRectangle(0, 0, 1, 1, 0, 1);
        store.SetCellBits(0, 0, 0, 0);
        store.ClearCellBits(0, 0, 0, 0);

        const int iterations = 2000;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < iterations; i++)
        {
            store.CopyCellRectangle(0, 0, 1, 1, 0, 1); // same shape every time - no stack resize either.
            store.SetCellBits(0, 0, 0, 0);
            store.ClearCellBits(0, 0, 0, 0);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        var bytesPerIteration = (after - before) / (double)iterations;

        Assert.True(
            bytesPerIteration < 64,
            $"expected near-zero allocation per iteration with no CellsMutated subscriber, got {bytesPerIteration:F1} bytes.");
    }
}
