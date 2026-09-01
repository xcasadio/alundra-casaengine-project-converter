#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E3 (docs/plan-echelles-chiffrage.md E3): <see cref="AlundraEntityScriptProxy.GetTileHeightAtOffset"/>'s
/// port of <c>GetTileHeightAtOffset</c> (<c>EntityGameplayManager.cs:277-345</c>) against the REAL map 389
/// ("Ship Klark (beginning)") cell data - same fixture/self-skip pattern as
/// <see cref="AlundraGroundSlopeTests"/>/<see cref="AlundraFloorHeightTests"/> (E1/E2's own sibling
/// slices): a real headless <see cref="World"/> with the real map 389 <see cref="AlundraCellsCollisionField"/>
/// installed as <c>World.CollisionField</c>, and a hand-built hero pawn from the shared
/// <see cref="HeroWorldFixture"/> montage.
///
/// E3 is a PURE function with NO production call site yet (see the method's own class doc,
/// "RESTRICTION" paragraph - E4's job). So, unlike E1/E2, there is no production-call-site test here.
///
/// ORACLE INDEPENDENCE (fixed after review - see the completion report for this slice): the FIRST
/// version of this file derived every expectation from
/// <see cref="AlundraCellsCollisionField.TrySampleGround(in Vector3, float, out GroundSample)"/> - the
/// SAME per-slope-interpolating helper the method under test's FIRST (wrong) implementation also
/// called. That made every assertion structurally blind to "does this method interpolate by slope
/// when the original never does" - the actual bug this slice had. <see cref="ExpectedRawCornerHeight16"/>
/// below instead re-parses the map's raw <c>AlundraCells</c> JSON directly (via
/// <see cref="AlundraCellsRecords.TryParse"/>, completely independent of
/// <see cref="AlundraCellsCollisionField"/>'s own internals) and takes the four-corner max of the RAW
/// <c>Height</c> array entries - exactly what the original's <c>tile.Height</c> read - so an
/// implementation that interpolates by slope produces a DIFFERENT number than this oracle for any
/// corner landing on a sloped cell (see
/// <see cref="ZeroOffset_FootprintOnSlopeCell_ReturnsRawHeight_NotSlopeInterpolatedHeight"/> below,
/// which exercises exactly that).
/// </summary>
public class AlundraTileHeightAtOffsetTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // 24x16 px cells (StaticVariables.MapTileWidth/MapTileHeight) - same constants
    // AlundraGroundSlopeTests/AlundraFloorHeightTests/AlundraCellsCollisionFieldTests use.
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

    // Same production hero footprint as AlundraGroundSlopeTests/AlundraFloorHeightTests (F4 fix - real
    // converter-exported bank header, alundra-project/Data/sprite-records.json, hero asset
    // 4158f0d7-c5f0-4f6a-a48f-e73d0dd2250b).
    private const int HeroOffsetX = -10;
    private const int HeroOffsetY = -7;
    private const int HeroSizeX = 21;
    private const int HeroSizeY = 15;

    // PlayerManager.cs:718's own real offset for the ladder-climb-up guard: 1px north, 16.16.
    private const int OneNorth = -0x10000;

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

    private static string TileMapPath(string projectRoot) => Path.Combine(
        projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
        "Ship Klark (beginning)-389.tileMap");

    private static AlundraCellsCollisionField? LoadMap389Field(string projectRoot)
    {
        var tileMapPath = TileMapPath(projectRoot);
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

    /// <summary>Independent second parse of the SAME file <see cref="LoadMap389Field"/> loads - deliberately
    /// NOT going through <see cref="AlundraCellsCollisionField"/> at all, so the oracle below cannot share
    /// any bug with the code under test (see class doc, "ORACLE INDEPENDENCE").</summary>
    private static (AlundraCellsRecords Records, int Width, int Height)? LoadRawRecords(string projectRoot)
    {
        var tileMapPath = TileMapPath(projectRoot);
        if (!File.Exists(tileMapPath))
        {
            return null;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));

        var parsed = AlundraCellsRecords.TryParse(tileMapData.CustomProperties, WorldName, out var records);
        Assert.True(parsed, "map 389's AlundraCells custom property should parse (raw oracle).");
        return (records, tileMapData.MapSize.Width, tileMapData.MapSize.Height);
    }

    /// <summary>The CORRECT oracle: four-corner max of the RAW per-cell <c>Height</c> (cell units, 1 unit
    /// = 16 px), converted to 16.16 px the same way the original does (<c>unit &lt;&lt; 20</c>) - see
    /// <see cref="AlundraEntityScriptProxy.GetTileHeightAtOffset"/>'s own doc for the shift derivation.
    /// Never touches <c>Slope</c>, so it cannot be satisfied by an implementation that interpolates.</summary>
    private static int ExpectedRawCornerHeight16(
        AlundraCellsRecords records, int width, int height, int x1, int y1, int x2, int y2)
    {
        var best = 0;
        foreach (var (px, py) in new[] { (x1, y1), (x2, y1), (x1, y2), (x2, y2) })
        {
            var cellX = Math.Clamp(px / CellWidthPx, 0, width - 1);
            var cellY = Math.Clamp(py / CellHeightPx, 0, height - 1);
            var cellIndex = cellY * width + cellX;
            var units = records.Height[cellIndex];
            if (units > best)
            {
                best = units;
            }
        }

        return best << 20;
    }

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class NoOpScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public System.Collections.Generic.IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => 0;
    }

    /// <summary>Same shape as <c>AlundraGroundSlopeTests.BuildProbe</c> - installs a real hero pawn with
    /// the production footprint, positioned so <c>(PosX + ModX) &gt;&gt; 16 == x1</c> and
    /// <c>(PosY + ModY) &gt;&gt; 16 == y1</c> hold, so callers can reason about the footprint's own
    /// top-left pixel directly. <see cref="AlundraEntityScriptProxy.GetTileHeightAtOffset"/> reads no
    /// other field (not <see cref="AlundraEntityScriptProxy.PosZ"/>, not <see cref="AlundraEntityScriptProxy.Flags"/>),
    /// so this probe leaves those at their defaults, unlike <c>AlundraGroundSlopeTests.BuildProbe</c>.</summary>
    private static AlundraEntityScriptProxy BuildProbe(World world, int x1, int y1)
    {
        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(0f, 0f, 0f), new NoOpScriptHost());

        // Register with CharacterMotionSystem (Owner.World populated) before the scripted PosX/PosY
        // writes below - same ordering AlundraGroundSlopeTests.BuildProbe/AlundraCharacterControllerAdoptionTests
        // use, and for the same reason (World.AddEntity only queues; nothing here calls world.Update
        // again afterward, so the later root-pull never overwrites these fields).
        world.Update(1f / 50f);

        proxy.PosX = (x1 - HeroOffsetX) << 16;
        proxy.PosY = (y1 - HeroOffsetY) << 16;
        proxy.ModX = HeroOffsetX << 16;
        proxy.ModY = HeroOffsetY << 16;
        proxy.Width = (HeroSizeX << 16) - 1;
        proxy.Height = (HeroSizeY << 16) - 1;
        return proxy;
    }

    // -----------------------------------------------------------------------------------------
    // (1) Zero offset, footprint fully inside one of the four real ladder cells: GetTileHeightAtOffset
    // must reproduce that cell's own real RAW ground height (measured off the raw AlundraCells JSON, not
    // hand-picked) - the same values established for E1 (AlundraGroundSlopeTests.ScaleCell_HeroFootprintFullyInsideCell_Slope18cIsSix):
    // (18,36)=11 units=176px, (19,38)=8 units=128px, (15,55)=5 units=80px, (21,55)=5 units=80px. All four
    // are flat (slope&3==0), so raw and slope-interpolated heights coincide here - this test alone cannot
    // distinguish the two (see test (4) below, which specifically picks a SLOPED cell).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(18, 36)]
    [InlineData(19, 38)]
    [InlineData(15, 55)]
    [InlineData(21, 55)]
    public void ZeroOffset_HeroFootprintFullyInsideScaleCell_ReturnsThatCellsOwnHeight(int cellX, int cellY)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // Same flush-against-top-left placement as AlundraGroundSlopeTests's own scale-cell test: a
        // 21x15 footprint fits entirely inside one 24x16 cell.
        var x1 = cellX * CellWidthPx + 1;
        var y1 = cellY * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1);

        var x2 = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;
        var expected = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x2, y2);

        var actual = proxy.GetTileHeightAtOffset(0, 0);

        Assert.Equal(expected, actual);
    }

    // -----------------------------------------------------------------------------------------
    // (2) The real ladder-climb-up guard's own offset (PlayerManager.cs:718, -0x10000 = 1px north): each
    // of the four real ladder cells' own north neighbor is MEASURED to be a DIFFERENT, HIGHER real ground
    // height than the ladder cell itself (measured off the raw AlundraCells JSON, not invented):
    //   (18,36)=176px -> north (18,35)=576px (a wall/ceiling cell)
    //   (19,38)=128px -> north (19,37)=176px
    //   (15,55)=80px  -> north (15,54)=128px
    //   (21,55)=80px  -> north (21,54)=128px
    // Positioned flush at the scale cell's own top edge (y1 == cellY*16), a 1px-north offset pushes the
    // footprint's own y1 corners into the north cell while its y2 corners stay in the scale cell (a 15px
    // footprint cannot fully vacate a 16px cell on a 1px nudge) - and because the north cell is measured
    // to be taller in every one of these four cases, the four-corner MAXIMUM still equals the north
    // cell's own height, exactly as PlayerManager.cs:718's climb-up guard needs ("is there a real
    // obstruction above"). An implementation that ignores offsetY entirely would instead return the
    // (smaller) scale-cell height in all four cases - the two never coincide here, so this kills that
    // mutation.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(18, 36)]
    [InlineData(19, 38)]
    [InlineData(15, 55)]
    [InlineData(21, 55)]
    public void OneNorthOffset_HeroFlushAtScaleCellTop_ReturnsNorthNeighborsHeight(int cellX, int cellY)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        var x1 = cellX * CellWidthPx + 1;
        var y1 = cellY * CellHeightPx; // flush at the cell's own top edge - see class-level comment above.
        var proxy = BuildProbe(world, x1, y1);

        var x2Base = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y1Shifted = ((proxy.PosY + proxy.ModY + OneNorth)) >> 16;
        var y2Shifted = ((proxy.PosY + proxy.ModY + OneNorth + proxy.Height)) >> 16;

        // Sanity check on the geometry itself: prove the shift really does move the top corners into the
        // cell above, and the bottom corners really do stay put - otherwise this test would not be
        // exercising the offset at all.
        Assert.Equal(cellY - 1, y1Shifted / CellHeightPx);
        Assert.Equal(cellY, y2Shifted / CellHeightPx);

        var expected = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1Shifted, x2Base, y2Shifted);

        // Independent confirmation that "expected" really is the NORTH cell's own height here (not some
        // accidental tie).
        var northHeight16 = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, cellY * CellHeightPx - 1, x1, cellY * CellHeightPx - 1);
        Assert.Equal(northHeight16, expected);

        var ownHeight16 = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x2Base, (proxy.PosY + proxy.ModY + proxy.Height) >> 16);
        Assert.NotEqual(ownHeight16, expected); // the whole point: offset must change the answer.

        var actual = proxy.GetTileHeightAtOffset(0, OneNorth);

        Assert.Equal(expected, actual);
    }

    // -----------------------------------------------------------------------------------------
    // (3) Mutation-killer for BOTH "maximum -> minimum" and "sample one corner instead of four", on the Y
    // axis: the same footprint straddling the (18,36)/(18,37) row boundary AlundraGroundSlopeTests's own
    // DivergentCase_HeroFootprintStraddlesWallBoundary_... test uses (x1=434, y1=578, zero offset). Real,
    // measured RAW heights: (18,36)=176px=11534336 (16.16), (18,37)=208px=13631488 (16.16) - two DIFFERENT
    // real heights across the same footprint's own y1/y2 corners (both cells flat, slope&3==0, so raw and
    // interpolated coincide here too). The correct four-corner MAXIMUM is 208px: a "minimum" mutant would
    // return 176px instead, and a "sample only corner (x1,y1) instead of all four" mutant would also
    // return 176px instead (that corner sits in row 36) - both wrong, both different from the correct
    // 208px, so this one test kills both mutations on the Y axis (see test (5) below for the X axis,
    // which this test does NOT cover - both x-corners fall in the same column here).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZeroOffset_FootprintStraddlesRowBoundary_ReturnsTheHigherRowsHeight_NotTheLowerOrACorner()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        const int scaleCellX = 18;
        const int scaleCellY = 36;
        var x1 = scaleCellX * CellWidthPx + 2; // 434 - same geometry as AlundraGroundSlopeTests's own
        var y1 = scaleCellY * CellHeightPx + 2; // 578 - DivergentCase test (F3 fix).
        var proxy = BuildProbe(world, x1, y1);

        var x2 = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;

        // Sanity check: the footprint really does straddle the row boundary (y1 in row 36, y2 in row 37),
        // both x-corners stay in column 18 - same geometry AlundraGroundSlopeTests's own DivergentCase
        // test already verified for Slope_18c.
        Assert.Equal(scaleCellY, y1 / CellHeightPx);
        Assert.Equal(scaleCellY + 1, y2 / CellHeightPx);
        Assert.Equal(scaleCellX, x1 / CellWidthPx);
        Assert.Equal(scaleCellX, x2 / CellWidthPx);

        var row36Height16 = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x1, y1);
        var row37Height16 = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y2, x1, y2);
        Assert.Equal(11534336, row36Height16); // 176px - measured, matches E1's own established fact.
        Assert.Equal(13631488, row37Height16); // 208px - measured, matches E1's own established fact.
        Assert.True(row37Height16 > row36Height16); // the mutation-killing precondition.

        var actual = proxy.GetTileHeightAtOffset(0, 0);

        Assert.Equal(row37Height16, actual);
    }

    // -----------------------------------------------------------------------------------------
    // (4) THE anti-vacuous-green test for this slice's own bug (D1/D2 of the adversarial re-review):
    // cell (13,27) is a REAL sloped cell on map 389 (slope=5, slope&3==1 "stairs", raw height=10 units=
    // 160px). Positioned so the footprint's own y1 corner sits at yMod=1 within that cell and its y2
    // corner at yMod=15 (both corners still inside the SAME cell, column 13 throughout - a single-cell
    // probe, so this isolates the raw-vs-interpolated question from the four-corner-max question tests
    // (1)-(3) and (5) already cover):
    //   RAW (what the original's tile.Height actually is): 10 units = 160px = 10485760 (16.16).
    //   SLOPE-INTERPOLATED (what AlundraCellsCollisionField.TrySampleGround/ComputeGroundHeight would
    //   give at these same two y positions - the WRONG grandeur this slice's first implementation
    //   imported from ComputeTerrainHeight/ComputeEntityGroundHeight, a DIFFERENT original function):
    //   (10-1)*16+16-1=159px at yMod=1, (10-1)*16+16-15=145px at yMod=15, max=159px=10420224.
    // 160 != 159: a "reuse SampleTerrainHeightCorner/TrySampleGround" mutant returns 10420224 here, the
    // correct implementation returns 10485760 - this is exactly the divergence measured on 4560 real
    // poses across the whole map (worst case 15px, this specific cell diverges by 1px).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZeroOffset_FootprintOnSlopeCell_ReturnsRawHeight_NotSlopeInterpolatedHeight()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        const int slopeCellX = 13;
        const int slopeCellY = 27;
        var x1 = slopeCellX * CellWidthPx + 1; // 313 - column 13 throughout (x2 = 333, also column 13).
        var y1 = slopeCellY * CellHeightPx + 1; // 433 - yMod 1 (top corners).
        var proxy = BuildProbe(world, x1, y1);

        var x2 = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16; // 447 - yMod 15 (bottom corners).

        // Sanity check on the geometry: single cell throughout, both x-corners in column 13, both
        // y-corners in row 27 - so any divergence measured below is caused ONLY by raw-vs-interpolated,
        // not by which of the four corners gets sampled.
        Assert.Equal(slopeCellX, x1 / CellWidthPx);
        Assert.Equal(slopeCellX, x2 / CellWidthPx);
        Assert.Equal(slopeCellY, y1 / CellHeightPx);
        Assert.Equal(slopeCellY, y2 / CellHeightPx);

        var rawExpected = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x2, y2);
        Assert.Equal(10485760, rawExpected); // 10 units * 16 px/unit = 160px, 16.16.

        // The WRONG oracle (kept ONLY to prove the divergence is real, never used as this test's
        // expectation) - the same per-slope-interpolating helper this slice's first implementation
        // reused by mistake.
        field.TrySampleGround(new Vector3(x1, y1, 0f), float.MaxValue, out var topSample);
        field.TrySampleGround(new Vector3(x1, y2, 0f), float.MaxValue, out var bottomSample);
        var interpolatedMax = Math.Max(
            (int)Math.Round((double)topSample.GroundHeight * 65536.0),
            (int)Math.Round((double)bottomSample.GroundHeight * 65536.0));
        Assert.Equal(10420224, interpolatedMax); // 159px, 16.16 - WRONG for this method, proven different.
        Assert.NotEqual(rawExpected, interpolatedMax); // the mutation-killing precondition.

        var actual = proxy.GetTileHeightAtOffset(0, 0);

        Assert.Equal(rawExpected, actual);
        Assert.NotEqual(interpolatedMax, actual); // fails if slope interpolation is ever reintroduced.
    }

    // -----------------------------------------------------------------------------------------
    // (5) X-axis counterpart of test (3): a mutant that samples only the x1 corner pair (dropping the x2
    // corners entirely) passes every test above, because tests (1)-(4) all keep both x-corners in the
    // SAME column. Cells (19,55) and (20,55) are real, ADJACENT, both flat (slope&3==0), with DIFFERENT
    // raw heights (10 and 11 units = 160px/176px) - positioned so x1 lands in column 19 and x2 in column
    // 20 while y1/y2 both stay in row 55 (no Y straddling), this isolates the X-axis four-corner rule.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZeroOffset_FootprintStraddlesColumnBoundary_ReturnsTheHigherColumnsHeight_NotOnlyTheLeftColumn()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        const int leftCellX = 19;
        const int rowY = 55;
        var x1 = leftCellX * CellWidthPx + 10; // 466 - near the right edge of column 19.
        var y1 = rowY * CellHeightPx; // flush at the row's own top edge, so y2 stays in the same row.
        var proxy = BuildProbe(world, x1, y1);

        var x2 = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;

        // Sanity check: x1/x2 really do straddle the column boundary, y1/y2 stay in the same row.
        Assert.Equal(leftCellX, x1 / CellWidthPx);
        Assert.Equal(leftCellX + 1, x2 / CellWidthPx);
        Assert.Equal(rowY, y1 / CellHeightPx);
        Assert.Equal(rowY, y2 / CellHeightPx);

        var leftColumnOnly = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x1, y2);
        var expected = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1, y1, x2, y2);
        Assert.Equal(10485760, leftColumnOnly); // 10 units = 160px - column 19 alone.
        Assert.Equal(11534336, expected); // 11 units = 176px - the correct max including column 20.
        Assert.NotEqual(leftColumnOnly, expected); // the mutation-killing precondition.

        var actual = proxy.GetTileHeightAtOffset(0, 0);

        Assert.Equal(expected, actual);
        Assert.NotEqual(leftColumnOnly, actual); // fails if the x2 corners are ever dropped.
    }

    // -----------------------------------------------------------------------------------------
    // (6) Out-of-map behaviour - DOCUMENTED DEVIATION, not a faithful port: the original indexes
    // g_tileToWorldXTable (1248 entries) directly off the pixel offset with no clamp, so an out-of-range
    // corner is UNDEFINED in the original (EntityGameplayManager.cs:289-292's table has no bounds check
    // visible in the decompiled body). This port clamps to the nearest edge cell instead (the same
    // pre-existing E1 convention AlundraCellsCollisionField.TrySampleGround/SampleRawCellHeight already
    // use) - a deliberate, DIFFERENT behaviour, not "the same thing the original does". This test proves
    // the clamp lands on the CORRECT edge cell (not just "some edge cell of height 0", which the map's
    // four grid corners all happen to share): column x=0 carries DIFFERENT non-zero raw heights at
    // different rows (row 20 = 13 units = 208px, row 29 = 14 units = 224px, both flat cells) - clamping
    // the base position far off the west edge at each of these two rows must reproduce THAT row's own
    // column-0 height, not 0 and not the other row's height.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(20, 13)]
    [InlineData(29, 14)]
    public void OffsetPushesFootprintOffMapWest_ClampsToColumnZeroOfTheSameRow_DocumentedDeviationFromOriginal(
        int rowCellY, int expectedRawHeightUnits)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        var raw = LoadRawRecords(projectRoot);
        if (field == null || raw == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // Base footprint well inside the map, at column 10 (arbitrary, away from the west edge); only
        // the offset below pushes it off the grid.
        const int baseCellX = 10;
        var x1 = baseCellX * CellWidthPx + 1;
        var y1 = rowCellY * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1);

        var offsetX = -3000 << 16; // far past the west edge (map is 52 cells = 1248px wide).

        var x1Shifted = (proxy.PosX + proxy.ModX + offsetX) >> 16;
        var x2Shifted = (proxy.PosX + proxy.ModX + offsetX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;

        // Sanity check: both x-corners really are off the west edge, not just one.
        Assert.True(x1Shifted < 0);
        Assert.True(x2Shifted < 0);

        var expected = ExpectedRawCornerHeight16(raw.Value.Records, raw.Value.Width, raw.Value.Height, x1Shifted, y1, x2Shifted, y2);
        var expectedFromLiteral = expectedRawHeightUnits << 20;
        Assert.Equal(expectedFromLiteral, expected); // ties the InlineData literal to the raw oracle.
        Assert.NotEqual(0, expected); // distinguishes "correct edge cell" from "any zero-height edge cell".

        var actual = proxy.GetTileHeightAtOffset(offsetX, 0);

        Assert.Equal(expected, actual);
    }

    // -----------------------------------------------------------------------------------------
    // (7) No collision field installed: same degraded-mode fallback as ComputeTerrainHeight/UpdateGroundSlope.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void NoCollisionFieldInstalled_ReturnsZero()
    {
        var world = HeroWorldFixture.BuildWorld(null!);
        var proxy = BuildProbe(world, 100, 100);

        var actual = proxy.GetTileHeightAtOffset(0, OneNorth);

        Assert.Equal(0, actual);
    }
}
