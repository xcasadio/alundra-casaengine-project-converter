#nullable enable
using System;
using System.Collections.Generic;
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
/// Covers E1 (docs/plan-echelles-chiffrage.md É1): <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>'s
/// port of <c>PhysicsEngine.UpdateTileAttributes</c>'s <c>Slope_18c</c> four-corner rule, against the REAL
/// map 389 ("Ship Klark (beginning)") cell data - same fixture/self-skip pattern as
/// <see cref="AlundraCellsCollisionFieldTests"/>/<see cref="AlundraCharacterControllerAdoptionTests"/>: a
/// real headless <see cref="World"/> with the real map 389 <see cref="AlundraCellsCollisionField"/>
/// installed as <c>World.CollisionField</c>, and a hand-built hero pawn from the shared
/// <see cref="HeroWorldFixture"/> montage.
/// </summary>
public class AlundraGroundSlopeTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // 24x16 px cells (StaticVariables.MapTileWidth/MapTileHeight) - same constants
    // AlundraCellsCollisionFieldTests uses for its own real-map assertions.
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

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

    private static AlundraCellsCollisionField? LoadMap389Field(string projectRoot)
    {
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

    /// <summary>F4 fix (docs/plan-echelles-chiffrage.md, adversarial review): the hero's own real
    /// converter-exported bank header (<c>alundra-project/Data/sprite-records.json</c>, hero asset
    /// <c>4158f0d7-c5f0-4f6a-a48f-e73d0dd2250b</c>) - <c>AlundraEntitySpawnFactory.SetEntityDimensions</c>'s own
    /// formula (<c>Width = (sizeX &lt;&lt; 16) - 1</c>, <c>ModX = offsetX &lt;&lt; 16</c>, etc.) applied to
    /// these exact values is what every real player entity carries in production.</summary>
    private const int HeroOffsetX = -10;
    private const int HeroOffsetY = -7;
    private const int HeroSizeX = 21;
    private const int HeroSizeY = 15;

    /// <summary>
    /// Builds the shared hero-pawn montage (<see cref="HeroWorldFixture"/>) and pins the struct fields
    /// <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/> reads directly. <paramref name="x1"/>/
    /// <paramref name="y1"/> are the footprint's own top-left pixel corner - <see cref="AlundraEntityScriptProxy.PosX"/>/
    /// <see cref="AlundraEntityScriptProxy.PosY"/> are derived from them so that
    /// <c>(PosX + ModX) &gt;&gt; 16 == x1</c> holds under the PRODUCTION <see cref="AlundraEntityScriptProxy.ModX"/>/
    /// <see cref="AlundraEntityScriptProxy.ModY"/>/<see cref="AlundraEntityScriptProxy.Width"/>/
    /// <see cref="AlundraEntityScriptProxy.Height"/> set below (F4 fix - was <c>ModX=ModY=0</c>,
    /// <c>Width=21&lt;&lt;16</c>, <c>Height=15&lt;&lt;16</c>, a 1px-per-axis-larger footprint than any real
    /// player entity ever carries). <see cref="AlundraEntityScriptProxy.Flags"/> defaults to carrying
    /// <see cref="EntityFlags.Gravity"/> - the gate <see cref="AlundraPlayerManager.MovePlayer"/> itself
    /// unconditionally sets every frame in the free branch (PlayerManager.cs:59-60) before this method
    /// would ever run for a real player - individual tests clear it to cover the no-gravity case.
    /// </summary>
    private static AlundraEntityScriptProxy BuildProbe(
        World world, int x1, int y1, int posZ, bool gravity = true)
    {
        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(0f, 0f, 0f), new NoOpScriptHost());

        // AlundraCharacterControllerAdoptionTests' own pattern: World.AddEntity only QUEUES the entity
        // (World.cs's own _baseObjectsToAdd) - Owner.World stays null (so UpdateGroundSlope's own
        // Owner?.World?.CollisionField read would too) until one World.Update flushes the queue and
        // registers the pawn with CharacterMotionSystem. Done BEFORE the scripted PosX/PosY/PosZ/Flags
        // writes below, exactly like that file's own "register with CharacterMotionSystem before the
        // scripted write" comment - this call's own root-pull (Update's E3.d paragraph) would otherwise
        // overwrite them right back from the Vector3.Zero start position on any LATER Update, but nothing
        // here calls Update again afterward (only UpdateGroundSlope, which never touches PosX/PosY/PosZ).
        world.Update(1f / 50f);

        proxy.PosX = (x1 - HeroOffsetX) << 16;
        proxy.PosY = (y1 - HeroOffsetY) << 16;
        proxy.PosZ = posZ;
        proxy.ModX = HeroOffsetX << 16;
        proxy.ModY = HeroOffsetY << 16;
        proxy.ModZ = 0;
        proxy.Width = (HeroSizeX << 16) - 1;
        proxy.Height = (HeroSizeY << 16) - 1;
        proxy.Flags = gravity ? EntityFlags.Gravity : 0;
        return proxy;
    }

    /// <summary>16.16 ground height (px) matching <see cref="AlundraTerrainProbe.SampleTerrainHeightCorner"/>'s
    /// own rounding - the exact <c>ModdedPosZ</c> a resting player entity carries in THIS port (F1 fix,
    /// docs/plan-echelles-chiffrage.md: the qualification rule is <c>height == ModdedPosZ</c>, no <c>+1</c>
    /// - see <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>'s own doc for why this port never
    /// adopted the original's <c>+1</c> resting invariant).</summary>
    private static int QualifyingPosZ(int cellHeightUnits) => cellHeightUnits * CellHeightPx << 16;

    /// <summary>No-op <see cref="IAlundraScriptHost"/> - none of these tests ever call
    /// <see cref="AlundraEntityScriptProxy.Update"/>, only <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>
    /// directly, so nothing on this host is ever exercised.</summary>
    private sealed class NoOpScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner => throw new NotSupportedException();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public System.Collections.Generic.IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => 0;
    }

    // -----------------------------------------------------------------------------------------
    // The four real ladder cells: GroundProperty 12 -> Slope_18c = (12 >> 1) & 7 = 6.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(18, 36, 11)]
    [InlineData(19, 38, 8)]
    [InlineData(15, 55, 5)]
    [InlineData(21, 55, 5)]
    public void ScaleCell_HeroFootprintFullyInsideCell_Slope18cIsSix(int cellX, int cellY, int cellHeightUnits)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // A 21x15 footprint fits entirely inside one 24x16 cell - place it flush against the cell's own
        // top-left corner so all four corners land in the SAME cell (no straddling).
        var x1 = cellX * CellWidthPx + 1;
        var y1 = cellY * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1, QualifyingPosZ(cellHeightUnits));

        proxy.UpdateGroundSlope();

        Assert.Equal(6, proxy.Slope_18c);
    }

    [Fact]
    public void FlatNonScaleCell_HeroAtMatchingHeight_Slope18cIsZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // Cell (18, 57): ground_property 0, height 5 (AlundraCellsCollisionFieldTests' own flat-cell
        // fixture) - height matches exactly like the scale cells above, but ground_property is 0.
        var x1 = 18 * CellWidthPx + 1;
        var y1 = 57 * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1, QualifyingPosZ(5));

        proxy.UpdateGroundSlope();

        Assert.Equal(0, proxy.Slope_18c);
    }

    [Fact]
    public void ScaleCell_GravityBitAbsent_Slope18cForcedToZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        var x1 = 18 * CellWidthPx + 1;
        var y1 = 36 * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1, QualifyingPosZ(11), gravity: false);

        proxy.UpdateGroundSlope();

        Assert.Equal(0, proxy.Slope_18c);
    }

    [Fact]
    public void NoCollisionFieldInstalled_Slope18cStaysZero()
    {
        // No self-skip needed - this test does not touch the real map data at all, it only exercises the
        // "no field installed" branch (the same degraded-mode world a headless test harness with no
        // AlundraCellsCollisionField would produce - see AlundraCellsCollisionField's own class doc).
        // HeroWorldFixture.BuildWorld itself only stores whatever field it is given as World.CollisionField
        // (see its own doc) - passing null reproduces that degraded-mode world exactly, without
        // duplicating its Game/GameManager reflection plumbing here.
        var world = HeroWorldFixture.BuildWorld(null!);

        var proxy = BuildProbe(world, 100, 100, QualifyingPosZ(6));

        proxy.UpdateGroundSlope();

        Assert.Equal(0, proxy.Slope_18c);
    }

    // -----------------------------------------------------------------------------------------
    // F3 fix (adversarial review, docs/plan-echelles-chiffrage.md §7.1): this test was originally sold as
    // exercising the §7.1 risk - "a hero pressed flush against a wall is precisely the pose where the
    // four corners and a center-only sample disagree". Measured against the REAL map 389 data (brute
    // force over every integer footprint position on the 52x60 grid, requiring all four corners to share
    // one ground height): the implemented rule returns 6 for only 12 positions on the whole map (3 per
    // scale cell), and every one of those 12 positions is a footprint that fits ENTIRELY inside its scale
    // cell with room to spare - INCLUDING the position flush against the wall cell (18, 37) below (18,
    // 36): x1 in {432, 433, 434}, y1 = 576 puts y2 at 590 (production footprint, Height = (15&lt;&lt;16) - 1,
    // 15px tall) - still inside row 36, one px shy of row 37's own 591 boundary. At that flush-against-the-
    // wall pose the four corners and the center AGREE (both read 6) - the §7.1 risk does NOT materialize
    // anywhere on this map. The corner/center divergence this test exercises only happens at footprint
    // y1 = 578 (2px further down than any reachable flush-against-wall pose), which is NOT a pose the hero
    // can occupy - map 389 has no step-up (see docs/plan-echelles-chiffrage.md §1's own StepHeight=3px
    // note) and every neighboring cell differs by a whole 16px/32px unit, so no walk can land the
    // footprint's y1 between 577 and 591 while still qualifying at height 11.
    //
    // What this test actually proves, honestly: it kills the "center-sample" mutation (an implementation
    // that reads only the footprint's geometric center instead of all four corners) - useful mutation
    // coverage on its own - but the pose it uses to do that is topologically unreachable, so it does NOT
    // demonstrate the §7.1 risk in a scenario a real player can occupy. Kept as a mutation-killer with this
    // caveat spelled out, per the same "seeded position, documented as such" pattern
    // <see cref="ScaleCell_HeroFootprintFullyInsideCell_Slope18cIsSix"/>'s own sibling production-call-site
    // test (<see cref="ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix"/>) uses for its own
    // unreachable-but-legitimate seeded placement.
    //
    // Ground truth read straight off the real map data: cell (18, 36) is the scale cell, ground_property
    // 12, slope 4 (flat), height 11 -&gt; ground height 176px. Cell (18, 37) right below it: ground_property
    // 0, slope 4 (flat), height 13 -&gt; ground height 208px, a DIFFERENT height, so it cannot qualify for
    // the same ModdedPosZ as the scale cell above it. (18, 37) also carries walkability 1, but that is NOT
    // what disqualifies it here - measured, walkability 1 is not blocking under the hero's own base mask
    // 0x40 (AlundraCellsCollisionField.cs:135/:210-213, the 0x01 bit only blocks ClassB entities); the
    // HEIGHT mismatch (13 vs 11) is the sole disqualifying fact.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void DivergentCase_HeroFootprintStraddlesWallBoundary_CornersDisagreeWithCenter_Slope18cIsZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        const int scaleCellX = 18;
        const int scaleCellY = 36;
        const int scaleCellHeightUnits = 11;

        var world = HeroWorldFixture.BuildWorld(field);

        // Footprint: x in [434, 454] (entirely inside column 18 - both x-corners stay in the scale
        // cell's own column), y in [578, 592] (row 36 at the top, spilling 1px into row 37 at the
        // bottom, production footprint Height = (15 &lt;&lt; 16) - 1) - both x-corners land in the SAME
        // column, only the y-corners split across the row boundary. Geometric center (~444, ~585) still
        // floors into row 36 (the scale cell). NOT a reachable pose (see this test's own class-level
        // caveat above) - a 2px-deeper seed than the reachable flush-against-the-wall pose, deliberately
        // chosen to still trigger the corner/center divergence for mutation-killing purposes.
        var x1 = scaleCellX * CellWidthPx + 2; // 434
        var y1 = scaleCellY * CellHeightPx + 2; // 578

        var moddedPosZ = QualifyingPosZ(scaleCellHeightUnits); // matches row 36's own ground height, not row 37's.
        var proxy = BuildProbe(world, x1, y1, moddedPosZ);

        // Sanity check on the geometry itself, independent of UpdateGroundSlope: prove the center really
        // does land in the scale cell while a corner lands in the wall cell below it - otherwise this
        // test would not actually be exercising the documented divergence.
        var x2 = (proxy.PosX + proxy.ModX + proxy.Width) >> 16;
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;
        Assert.Equal(scaleCellX, x1 / CellWidthPx);
        Assert.Equal(scaleCellX, x2 / CellWidthPx);
        Assert.Equal(scaleCellY, y1 / CellHeightPx);
        Assert.Equal(scaleCellY + 1, y2 / CellHeightPx); // the disagreeing corner's own cell.
        var centerY = (y1 + y2) / 2;
        Assert.Equal(scaleCellY, centerY / CellHeightPx); // the center's own cell - still row 36.

        proxy.UpdateGroundSlope();

        // The real four-corner rule: two of the four corners (the y2 ones) fail the height match against
        // cell (18, 37)'s own ground height, so bestFlagMask is reset to 0 and stays there - Slope_18c
        // must be 0. An implementation that (incorrectly) samples only the footprint's CENTER point would
        // read cell (18, 36) alone (ground_property 12) and report Slope_18c == 6 instead - this
        // assertion fails against that implementation, which is the whole point of this test (verified by
        // mutation - see the E1 completion report).
        Assert.Equal(0, proxy.Slope_18c);
    }

    // -----------------------------------------------------------------------------------------
    // F1 corollary (adversarial review, THE critical gap): every test above calls
    // <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/> DIRECTLY - never through the real
    // production call site (<see cref="AlundraEntityScriptProxy.Update"/>'s own <c>IsPlayer</c> branch,
    // AFTER <c>AlundraPlayerManager.MovePlayer</c>/<c>Tick</c>, see that method's own E1 comment). That
    // gap is exactly what let F1 (the unsatisfiable <c>+1</c>) ship green: every direct-call test built
    // its own <c>ModdedPosZ</c> by hand and could silently bake in a Z the real player never occupies.
    // This test goes through the SAME real <see cref="World"/>/<see cref="CasaEngine.Framework.Application.Components.Physics.PhysicsWorld"/>/
    // <see cref="AlundraCellsCollisionField"/> montage (<see cref="HeroWorldFixture"/>), a REAL
    // <see cref="AlundraPlayerController"/> (not a stub), and one real <c>World.Update</c> frame - the
    // exact call chain a live player frame takes.
    //
    // Seeded placement, documented as such (same pattern as HeroTraceHarnessTests' own "highground"
    // scenario): the hero cannot WALK onto any of the 4 scale cells (no step-up, map 389's own neighbor
    // heights differ by whole 16px/32px units - docs/plan-echelles-chiffrage.md §1) - climbing itself is
    // out of E1's own scope (É4, not yet built). So this test seeds the hero directly on cell (18, 36)
    // at its own real ground height, pad held at all-zero (no input, nothing to walk it off the cell),
    // and asserts the production Update() call computes Slope_18c == 6 from that real position - proving
    // the four-corner rule is actually reachable in production, not just satisfiable by a hand-picked
    // test Z.
    // -----------------------------------------------------------------------------------------

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Minimal real-<see cref="AlundraPlayerController"/> script host for the production
    /// call-site test below - <see cref="PlayerController"/> is a REAL controller (its own
    /// <c>BuildPadState</c> is what <see cref="AlundraPlayerManager.MovePlayer"/> actually calls), unlike
    /// every other test in this file's own <see cref="NoOpScriptHost"/> (<c>PlayerController =&gt; null</c>,
    /// which makes the whole <c>IsPlayer</c> branch in <see cref="AlundraEntityScriptProxy.Update"/> a
    /// no-op and is exactly why those tests never reach <see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>
    /// through production code).</summary>
    private sealed class PlayerScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        // One logic tick per rendered frame - this test only runs a single frame, and
        // AlundraPlayerManager.Tick's own per-tick work (animation advance) is irrelevant to Slope_18c,
        // which UpdateGroundSlope computes from PosX/PosY/PosZ alone (already pulled from the root by the
        // time the IsPlayer branch runs - see Update's own E3.d paragraph).
        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    [Fact]
    public void ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        // Same scale cell as ScaleCell_HeroFootprintFullyInsideCell_Slope18cIsSix: (18, 36), height 11 ->
        // ground height 176px. x1/y1 chosen so the production footprint (Width=(21<<16)-1,
        // Height=(15<<16)-1, ModX=-10<<16, ModY=-7<<16) sits entirely inside the cell - see that test's
        // own geometry comment.
        const int cellX = 18;
        const int cellY = 36;
        const int cellHeightUnits = 11;
        var x1 = cellX * CellWidthPx + 1; // 433
        var y1 = cellY * CellHeightPx; // 576

        // Root position (pixels) such that (PosX + ModX) >> 16 == x1 and (PosY + ModY) >> 16 == y1 under
        // the production ModX/ModY below - see BuildProbe's own doc for the same derivation.
        var rootX = x1 - HeroOffsetX;
        var rootY = y1 - HeroOffsetY;
        var groundHeightPx = cellHeightUnits * CellHeightPx;

        var world = HeroWorldFixture.BuildWorld(field);
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = 0, ButtonsJustPressed = 0 } };
        var host = new PlayerScriptHost { PlayerController = controller };

        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(
            world, settings, new Vector3(rootX, rootY, groundHeightPx), host);

        // Production footprint (F4 fix) - AlundraEntitySpawnFactory.SetEntityDimensions is the SAME method the
        // real AdoptPlayerPawn calls with the hero's own real header (SizeX=21, SizeY=15, OffsetX=-10,
        // OffsetY=-7, OffsetZ=0); SizeZ=32 matches the CollisionComponent Box HeroWorldFixture builds.
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);

        // One real production frame: CharacterMotionSystem runs first (root stays put - already exactly
        // on real ground, no pad input), then AlundraEntityScriptProxy.Update's IsPlayer branch runs
        // MovePlayer (sets Flags |= Gravity, BlockedByEntity/InputBlockedMask both clear) then
        // UpdateGroundSlope - the exact live call chain.
        world.Update(1f / 50f);

        Assert.Equal(6, proxy.Slope_18c);
    }

    // -----------------------------------------------------------------------------------------
    // F8 (adversarial review): out-of-map footprint - AlundraCellsCollisionField.TrySampleGround/
    // SampleGroundProperty clamp any coordinate to the nearest edge cell rather than failing (documented
    // deviation on that class), so an out-of-map probe always resolves to a real cell's own data. Both
    // directions exercised: fully negative (clamps to cell (0,0)) and fully beyond the map's own 1248x960
    // px bounds (52x60 cells of 24x16px - clamps to the last cell, (51,59)). Expected height/property are
    // read directly off the SAME field the production accessor uses, not hand-computed, so this test
    // tracks whatever the real map data is rather than asserting a specific magic number.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-2000, -2000)] // clamps to cell (0, 0).
    [InlineData(3000, 3000)] // clamps to the last cell, (51, 59) - map 389 is 52x60 cells (1248x960 px).
    public void OutOfMapFootprint_ClampsToNearestEdgeCell_MatchesThatCellsOwnData(int x1, int y1)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // Read the clamped cell's own real ground height/property directly off the field, at the SAME
        // clamped position ProbeSlopeCorner will query (all four corners of a 21x15 footprint this far
        // out land in the identical clamped edge cell, since the footprint is tiny next to the distance
        // being clamped).
        var probePosition = new Vector3(x1, y1, 0f);
        var sampled = field.TrySampleGround(probePosition, float.MaxValue, out var groundSample);
        Assert.True(sampled, "TrySampleGround never fails, even out of map - see its own class doc.");
        var expectedGroundHeightPx = (int)Math.Round((double)groundSample.GroundHeight * 65536.0);
        var expectedGroundProperty = field.SampleGroundProperty(probePosition);
        var expectedSlope = (expectedGroundProperty >> 1) & 7;

        var proxy = BuildProbe(world, x1, y1, expectedGroundHeightPx);

        proxy.UpdateGroundSlope();

        Assert.Equal(expectedSlope, proxy.Slope_18c);
    }
}
