#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E4 (docs/plan-echelles-chiffrage.md É4): the case-6 ladder entry gate and the Climbing(0x0E)/
/// ClimbStill(0x35) state machine in <see cref="AlundraPlayerManager.MovePlayer"/>, plus the per-tick
/// vertical step <see cref="AlundraScriptedMotion.TickPlayer"/> now applies while climbing - against the
/// REAL map 389 ("Ship Klark (beginning)") ladder cell (18, 36). Same fixture/self-skip pattern as
/// <see cref="AlundraGroundSlopeTests"/>/<see cref="AlundraFloorHeightTests"/>/<see cref="AlundraTileHeightAtOffsetTests"/>
/// (E1/E2/É3's own sibling slices): a real headless <see cref="World"/> with the real map 389
/// <see cref="AlundraCellsCollisionField"/> installed as <c>World.CollisionField</c>, and a hand-built hero
/// pawn from the shared <see cref="HeroWorldFixture"/> montage, driven through the SAME production call
/// site (<see cref="AlundraEntityScriptProxy.Update"/>'s own <c>IsPlayer</c> branch) every other slice in
/// this plan uses (see <see cref="AlundraGroundSlopeTests.ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix"/>'s
/// own doc for why that matters - LESSON 2 of this ticket, "le vert inerte").
/// </summary>
public class AlundraLadderClimbTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // 24x16 px cells (StaticVariables.MapTileWidth/MapTileHeight) - same constants every sibling slice uses.
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

    // Same production hero footprint as AlundraGroundSlopeTests/AlundraFloorHeightTests/AlundraTileHeightAtOffsetTests
    // (F4 fix - real converter-exported bank header, alundra-project/Data/sprite-records.json).
    private const int HeroOffsetX = -10;
    private const int HeroOffsetY = -7;
    private const int HeroSizeX = 21;
    private const int HeroSizeY = 15;

    // Ladder cell (18, 36) - one of the four real map 389 scale cells (GroundProperty 12, height 11 units
    // -> 176px), same cell AlundraGroundSlopeTests.ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix
    // and AlundraTileHeightAtOffsetTests use.
    private const int LadderCellX = 18;
    private const int LadderCellY = 36;
    private const int LadderGroundHeightPx = 176;

    // E3's own measured value for this cell's north neighbor (18, 35) - a wall/ceiling cell, real ground
    // height 576px (see AlundraTileHeightAtOffsetTests' own class comment) - far above any Z this test
    // reaches, so the climb-up guard never blocks these tests.
    private const int NorthNeighborHeightPx = 576;

    // Map 389's own real Gravity/ZViscosity converted per AlundraWorldProxy.ResolveMapGravitySettings'
    // own formula (mapGravity*256/65536*2500 / mapZViscosity*256/65536*50) - 1250/800, the SAME numbers
    // that formula's own doc cites. Used here to stand in for what AdoptPlayerPawn would have resolved and
    // stashed on MapGravity/MapMaxFallSpeed (E4's own fix to that method) before the hero ever entered a
    // real map.
    private const float MapGravity = 1250f;
    private const float MapMaxFallSpeed = 800f;

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

    /// <summary>Loads the REAL exported hero <see cref="CharacterControllerComponent"/> settings from
    /// <c>alundra-project/Entities/Alundra/Alundra.entity</c> (same pattern as
    /// <c>HeroTraceHarnessTests.LoadHeroControllerSettings</c>) - CORRECTED (verifier F1): a hand-built
    /// <c>new CharacterControllerSettings { Gravity, MaxFallSpeed }</c> leaves every OTHER field at its C#
    /// default, in particular <c>GroundSnapDistance</c> (C# default 0.15) where the real exported prefab
    /// carries 4.0 - and <c>GroundSnapDistance</c> alone decides whether
    /// <c>CharacterControllerComponent.UpdateGround</c>'s per-frame ground-field re-snap cancels a climb's
    /// 1px/tick rise (measured: dz/frame is 1,1,1,1,1,1 at snap=4 but 1,2,3,4,5,6 at snap=0.15 - i.e. the
    /// snap=0.15 fixture was measuring a fiction where the hero climbs freely because the engine's own
    /// re-snap never reaches that far). Loading the real settings here means these tests exercise the same
    /// <c>GroundSnapDistance</c>/<c>StepHeight</c> production actually ships.</summary>
    private static CharacterControllerSettings LoadHeroControllerSettings(string projectRoot)
    {
        var heroEntityPath = Path.Combine(projectRoot, "Entities", "Alundra", "Alundra.entity");
        Assert.True(File.Exists(heroEntityPath), $"hero prefab not found at '{heroEntityPath}'.");

        var document = JObject.Parse(File.ReadAllText(heroEntityPath));
        var componentsNode = (JArray?)document["components"];
        JObject? controllerNode = null;
        if (componentsNode != null)
        {
            foreach (var node in componentsNode)
            {
                if ((string?)node["type"] == nameof(CharacterControllerComponent))
                {
                    controllerNode = (JObject)node;
                    break;
                }
            }
        }

        Assert.NotNull(controllerNode);
        var settingsNode = (JObject?)controllerNode!["settings"];
        Assert.NotNull(settingsNode);

        var settings = new CharacterControllerSettings();
        settings.Load(settingsNode!);
        return settings;
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

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Same minimal real-<see cref="AlundraPlayerController"/> script host as
    /// <see cref="AlundraGroundSlopeTests"/>'s own sibling class (see that file's own doc) - one logic tick
    /// per rendered frame, exactly what these tests need to observe a single +-1px per-tick step.</summary>
    private sealed class PlayerScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity => null;
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    /// <summary>Builds a hero pawn seeded on the real ladder cell (18, 36), at its own real ground height,
    /// with a real <see cref="CharacterControllerComponent"/> whose <see cref="CharacterControllerSettings.Gravity"/>/
    /// <see cref="CharacterControllerSettings.MaxFallSpeed"/> AND <see cref="AlundraEntityScriptProxy.MapGravity"/>/
    /// <see cref="AlundraEntityScriptProxy.MapMaxFallSpeed"/> are pre-set to the SAME map-389 values
    /// (standing in for what AlundraWorldProxy.AdoptPlayerPawn's own E4 fix now does at real adoption
    /// time) - the RESERVE <see cref="AlundraPlayerManager.RestoreGravityAfterClimb"/> reads from.</summary>
    private static (World World, Entity Entity, AlundraEntityScriptProxy Proxy, AlundraPlayerController Controller) BuildLadderHero(
        Func<AlundraPadState> padProvider)
    {
        var projectRoot = FindProjectRoot();
        Assert.NotNull(projectRoot);
        var field = LoadMap389Field(projectRoot!);
        Assert.NotNull(field);

        var world = HeroWorldFixture.BuildWorld(field!);
        var controller = new AlundraPlayerController { PadStateProviderForTests = padProvider };
        var host = new PlayerScriptHost { PlayerController = controller };

        var x1 = LadderCellX * CellWidthPx + 1;
        var y1 = LadderCellY * CellHeightPx;
        var rootX = x1 - HeroOffsetX;
        var rootY = y1 - HeroOffsetY;

        // F1 fix: load the REAL exported hero settings (GroundSnapDistance=4.0/StepHeight=3.0, not the C#
        // defaults) - see LoadHeroControllerSettings' own doc. AlundraWorldProxy.AdoptPlayerPawn overrides
        // ONLY Gravity/MaxFallSpeed/WalkabilityMask post-load (the three map/flags-dependent settings the
        // converter cannot bake in) - mirror that same override here so this fixture matches real
        // adoption exactly instead of just matching it on two fields.
        var settings = LoadHeroControllerSettings(projectRoot!);
        settings.Gravity = MapGravity;
        settings.MaxFallSpeed = MapMaxFallSpeed;
        var (entity, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(rootX, rootY, LadderGroundHeightPx), host);
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);

        // E4 fix (this slice's own AdoptPlayerPawn change) - stands in for what real adoption now does:
        // stash the SAME Gravity/MaxFallSpeed as the reserve MovePlayer's climbing state machine restores
        // to on lateral exit.
        proxy.MapGravity = MapGravity;
        proxy.MapMaxFallSpeed = MapMaxFallSpeed;

        // SETTLE FRAME (neutral pad, no ladder input yet): lets the REAL production pipeline compute
        // Slope_18c (E1's own UpdateGroundSlope) and FloorHeight (E2's own UpdateFloorHeight) for this
        // exact real position, exactly the way AlundraGroundSlopeTests.ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix
        // proves reads 6 here - NOT hand-set, so a broken Slope_18c/FloorHeight computation would fail
        // these tests too, not just AlundraGroundSlopeTests/AlundraFloorHeightTests' own. ComputeTerrainHeight
        // (FloorHeight's own terrain half) depends only on PosX/PosY, never PosZ (see that method's own
        // body) - so this settle frame's own FloorHeight stays correct for every later frame in this test,
        // even once PosZ is bumped up/down by climbing, as long as PosX/PosY never change (true here - no
        // AnimSetsByAnim entry exists for Climbing/ClimbStill on this minimal test proxy, so the hero never
        // drifts horizontally while climbing).
        controller.PadStateProviderForTests = () => default;
        world.Update(1f / 50f);
        Assert.Equal(6, proxy.Slope_18c); // sanity: this really is a real ladder cell, computed for real.

        // Seeded "already walked up to the ladder, facing it, holding into it" state - same seeding
        // convention AlundraGroundSlopeTests.ProductionCallSite_HeroSeededOnScaleCell_Slope18cIsSix uses
        // for its own no-step-up caveat: a real hero cannot walk any FURTHER onto this cell than flat
        // ground allows, so the actual wall-bump that sets ForceAdjusted (this port's own equivalent of
        // the original's "movement was curtailed" signal) is pinned directly rather than staged through a
        // full horizontal-collision simulation - the ladder-entry PRE-conditions below are exactly the
        // ones a real frame of walking into the wall while holding Up would have produced the tick before.
        controller.PadStateProviderForTests = padProvider;
        proxy.TargetDirection = 0x10;
        proxy.ForceAdjusted = 1;
        proxy.CarriedEntity = null;
        proxy.TargetAnimationId = 0u; // Idle - the case-6 gate below fires regardless of the CURRENT anim.

        return (world, entity, proxy, controller);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 1: climbing UP moves the hero +1px (0x10000, one 16.16 unit) exactly per logic tick,
    // through the production call site.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_HoldingUpIntoLadder_EntersClimbing_MovesUpOnePixelPerTick()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, AlundraPadState.Up))
        {
            return;
        }

        var startPosZ = proxy.PosZ;

        // Verifier F1's own adversarial measurement (6 frames, real GroundSnapDistance=4.0/StepHeight=3.0)
        // found the hero recolled to the SAME height every frame instead of rising - re-run here as a
        // permanent regression test, not just the original 2-frame check, so this exact scenario stays
        // covered: dz must be exactly +1px EVERY frame, never 0 (recolled) and never >1 (uncapped).
        for (var frame = 1; frame <= 6; frame++)
        {
            world.Update(1f / 50f);

            Assert.Equal(AlundraPlayerManager.ClimbingAnimationId, proxy.TargetAnimationId);
            Assert.Equal(startPosZ + frame * 0x10000, proxy.PosZ); // +1px, cumulative, every single frame.
        }

    }

    /// <summary>Writes an elevated <see cref="AlundraEntityScriptProxy.PosZ"/> THROUGH to the real
    /// CasaEngine root transform too - a bare <c>proxy.PosZ = ...</c> assignment alone would be silently
    /// overwritten by the very next <see cref="World.Update"/>'s own root-pull (<see cref="AlundraEntityScriptProxy.Update"/>'s
    /// head, "PosZ = root.Z..." - see that method's own E3.d/E4.f doc) since nothing else moved the root
    /// to match. Deliberately bypasses <see cref="AlundraEntityScriptProxy.PushLogicalPositionToRoot"/>
    /// (which re-clamps onto the terrain via <see cref="AlundraEntityScriptProxy.ClampToGround"/> - not
    /// what these tests want, they need the hero suspended ABOVE ground mid-climb) - same
    /// <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/> formula, applied directly to the root.</summary>
    private static void SetElevatedPosZ(Entity entity, AlundraEntityScriptProxy proxy, int posZ)
    {
        proxy.PosZ = posZ;
        entity.RootComponent!.LocalTransform.Position =
            AlundraEntitySpawnFactory.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 2: climbing DOWN moves the hero -1px exactly per logic tick, through the production call
    // site. Seeded a few pixels above FloorHeight (a real ladder cell's own ground height) so the descent
    // condition (FloorHeight + 1 < PosZ) starts true - see MovePlayer's own LESSON 1 re-derivation comment
    // for why it is false at rest but true here.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_AlreadyClimbing_HoldingDown_MovesDownOnePixelPerTick()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, AlundraPadState.Down))
        {
            return;
        }

        // Start already elevated 5px above the ladder's own ground height (well clear of FloorHeight+1),
        // already in the Climbing animation (as if a previous frame had climbed up this far).
        SetElevatedPosZ(entity, proxy, LadderGroundHeightPx * 65536 + 5 * 0x10000);
        proxy.TargetAnimationId = AlundraPlayerManager.ClimbingAnimationId;

        var startPosZ = proxy.PosZ;

        world.Update(1f / 50f);

        Assert.Equal(AlundraPlayerManager.ClimbingAnimationId, proxy.TargetAnimationId);
        Assert.Equal(startPosZ - 0x10000, proxy.PosZ); // -1px this one tick.
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 3: pad released while climbing -> ClimbStill (anim 0x35), position FROZEN (no vertical
    // step at all, not even a residual one).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_PadReleasedWhileClimbing_BecomesClimbStill_PositionFrozen()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, () => default))
        {
            return;
        }

        SetElevatedPosZ(entity, proxy, LadderGroundHeightPx * 65536 + 3 * 0x10000);
        proxy.TargetAnimationId = AlundraPlayerManager.ClimbingAnimationId;

        // Simulates a REAL preceding climbing frame's own SuspendGravityForClimb/TickPlayer declarations
        // (verifier F1/F2 fix), consistent with this test's own "as if a previous frame had climbed up
        // this far" comment above: CasaEngine's own per-frame order is component Update (which runs
        // CharacterControllerComponent.UpdateGround) THEN GameplayProxy.Update (Entity.cs:475-502) - i.e.
        // THIS frame's UpdateGround reads whatever the PREVIOUS frame's script left latched, never
        // something this same frame's script is about to set. A real hero already several ticks into a
        // climb has ALWAYS had Controller.IsVerticalOwnedExternally true (and a positive latch) since the
        // very first climbing frame that walked into the wall - a bare proxy.TargetAnimationId assignment
        // alone (this test's own shortcut for "already mid-climb") skips that real history, so without
        // seeding it here this test measures an unreachable bootstrap state (elevated position, but the
        // controller never actually told to treat it as external/airborne) rather than a genuine
        // already-climbing frame.
        proxy.Controller!.IsVerticalOwnedExternally = true;
        proxy.Controller!.SetExternalVerticalDisplacement(1f);

        var startPosZ = proxy.PosZ;

        world.Update(1f / 50f);

        Assert.Equal(AlundraPlayerManager.ClimbStillAnimationId, proxy.TargetAnimationId);
        Assert.Equal(startPosZ, proxy.PosZ); // frozen - no vertical step while ClimbStill.
        Assert.Equal(0, proxy.ForceZ);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 4: a genuinely LATERAL input (left/right, neither up nor down) while climbing exits to
    // Idle - the documented deviation from the original's own Jump exit (jump is not ported in V1).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_LateralInputWhileClimbing_ExitsToIdle()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, AlundraPadState.Right))
        {
            return;
        }

        SetElevatedPosZ(entity, proxy, LadderGroundHeightPx * 65536 + 3 * 0x10000);
        proxy.TargetAnimationId = AlundraPlayerManager.ClimbingAnimationId;

        world.Update(1f / 50f);

        Assert.Equal(0u, proxy.TargetAnimationId); // Idle(0x00).
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 5 (chiffree): gravity is the map's real value BEFORE climbing, exactly 0 WHILE climbing,
    // and restored to the SAME real value AFTER a lateral exit.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_GravitySuspendedWhileClimbing_RestoredOnLateralExit()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out var controller, AlundraPadState.Up))
        {
            return;
        }

        // BEFORE: the map's own real value (as AdoptPlayerPawn's E4 fix would have set it).
        Assert.Equal(MapGravity, proxy.Controller!.Settings.Gravity);
        Assert.Equal(MapMaxFallSpeed, proxy.Controller!.Settings.MaxFallSpeed);

        // DURING: entering climbing (holding Up into the wall) suspends both to exactly 0.
        world.Update(1f / 50f);
        Assert.Equal(AlundraPlayerManager.ClimbingAnimationId, proxy.TargetAnimationId);
        Assert.Equal(0f, proxy.Controller!.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller!.Settings.MaxFallSpeed);

        // AFTER: a lateral input exits to Idle and restores both to the map's own real value.
        controller.PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = AlundraPadState.Right };
        world.Update(1f / 50f);

        Assert.Equal(0u, proxy.TargetAnimationId);
        Assert.Equal(MapGravity, proxy.Controller!.Settings.Gravity);
        Assert.Equal(MapMaxFallSpeed, proxy.Controller!.Settings.MaxFallSpeed);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 7 (verifier F3): reaching the BOTTOM of the climbable descent (FloorHeight + 1 < PosZ no
    // longer holds) exits to Idle with gravity restored - PlayerManager.cs:697-701/:729 both guard
    // failures fall to the SAME Idle exit, not a stuck Climbing/ClimbStill with gravity pinned at 0.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_DescendingReachesBottom_ExitsToIdle_GravityRestored()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, AlundraPadState.Down))
        {
            return;
        }

        // Already AT the ladder's own resting ground height - FloorHeight + 1 < PosZ is false here (the
        // "at rest" case MovePlayer's own LESSON 1 re-derivation comment establishes), exactly the
        // bottom-of-descent guard failure.
        SetElevatedPosZ(entity, proxy, LadderGroundHeightPx * 65536);
        proxy.TargetAnimationId = AlundraPlayerManager.ClimbingAnimationId;
        // Simulate a real preceding climbing/descending frame's own declarations - see the ClimbStill
        // freeze test's own doc above for why a bare TargetAnimationId assignment alone is not enough
        // (CasaEngine's own per-frame order runs CharacterControllerComponent.UpdateGround BEFORE this
        // frame's own script logic).
        proxy.Controller!.IsVerticalOwnedExternally = true;
        proxy.Controller!.SetExternalVerticalDisplacement(1f);
        proxy.Controller!.Settings.Gravity = 0f;
        proxy.Controller!.Settings.MaxFallSpeed = 0f;

        var startPosZ = proxy.PosZ;

        world.Update(1f / 50f);

        Assert.Equal(0u, proxy.TargetAnimationId); // Idle(0x00), not stuck in Climbing/ClimbStill.
        Assert.Equal(0, proxy.ForceZ);
        Assert.Equal(startPosZ, proxy.PosZ); // did not fall through the floor on the very exit tick.
        Assert.Equal(MapGravity, proxy.Controller!.Settings.Gravity); // gravity resumes - can fall again.
        Assert.Equal(MapMaxFallSpeed, proxy.Controller!.Settings.MaxFallSpeed);
        Assert.False(proxy.Controller!.IsVerticalOwnedExternally); // handed back to the engine.
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 8 (verifier F3): reaching the TOP of the climbable ascent (PosZ no longer <=
    // tileHeightAbove) exits to Idle with gravity restored - the MONT guard's own mirror of Acceptance 7.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ProductionCallSite_AscendingReachesTop_ExitsToIdle_GravityRestored()
    {
        if (!TrySetUp(out var world, out var entity, out var proxy, out _, AlundraPadState.Up))
        {
            return;
        }

        // Above the ladder cell's own north-neighbor ceiling height (576px, NorthNeighborHeightPx) -
        // PosZ <= tileHeightAbove is false here, exactly the top-of-ascent guard failure.
        SetElevatedPosZ(entity, proxy, (NorthNeighborHeightPx + 1) * 65536);
        proxy.TargetAnimationId = AlundraPlayerManager.ClimbingAnimationId;
        proxy.Controller!.IsVerticalOwnedExternally = true;
        proxy.Controller!.SetExternalVerticalDisplacement(1f);
        proxy.Controller!.Settings.Gravity = 0f;
        proxy.Controller!.Settings.MaxFallSpeed = 0f;

        var startPosZ = proxy.PosZ;

        world.Update(1f / 50f);

        Assert.Equal(0u, proxy.TargetAnimationId); // Idle(0x00), not stuck in Climbing/ClimbStill.
        Assert.Equal(0, proxy.ForceZ);
        Assert.Equal(startPosZ, proxy.PosZ);
        Assert.Equal(MapGravity, proxy.Controller!.Settings.Gravity);
        Assert.Equal(MapMaxFallSpeed, proxy.Controller!.Settings.MaxFallSpeed);
        Assert.False(proxy.Controller!.IsVerticalOwnedExternally);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance 6: one case per removed entry condition (PlayerManager.cs:341-350's own 5-conjunct gate)
    // - each shows the SAME otherwise-qualifying setup fails to enter Climbing when exactly one condition
    // is individually broken (LESSON 2 - "le vert inerte": each must show a DIFFERENT value than the
    // targeted path, proven by neutralizing that one path only).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CaseSixGate_Slope18cNotSix_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        player.Slope_18c = 0; // broken: not on a ladder cell.
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up };

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        Assert.NotEqual(AlundraPlayerManager.ClimbingAnimationId, player.TargetAnimationId);
    }

    [Fact]
    public void CaseSixGate_NoPadHeld_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        var pad = default(AlundraPadState); // broken: buttonsHold == 0.

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        // CORRECTED (verifier F4): `Assert.NotEqual(ClimbingAnimationId, ...)` is inert against a mutant
        // that removes this conjunct from the gate - a broken gate here still leaves TargetAnimationId at
        // some non-Climbing(0x0E) value (0x35/ClimbStill, measured), which is enough to pass a NotEqual
        // check without actually detecting the mutation. Assert the EXACT value the real (unbroken) gate
        // produces instead: the gate correctly does NOT fire (buttonsHold == 0), so TargetAnimationId falls
        // through to the Idle/Moving case (PlayerManager.cs:361-383) with buttonsHold == 0 -> Idle(0x00).
        Assert.Equal(0x00u, player.TargetAnimationId);
    }

    [Fact]
    public void CaseSixGate_DirectionNotUp_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Right }; // broken: dir == 0x18, not 0x10.

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        // CORRECTED (verifier F4): same inert-NotEqual problem as CaseSixGate_NoPadHeld above. The gate
        // correctly does NOT fire (dir == 0x18, not 0x10), so TargetAnimationId falls through to the
        // Idle/Moving case with buttonsHold != 0 (Right held) -> Moving(0x01).
        Assert.Equal(0x01u, player.TargetAnimationId);
    }

    [Fact]
    public void CaseSixGate_NotFacingUp_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        player.TargetDirection = 0x18; // broken: not already facing up.
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up };

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        Assert.NotEqual(AlundraPlayerManager.ClimbingAnimationId, player.TargetAnimationId);
    }

    [Fact]
    public void CaseSixGate_ForceAdjustedZero_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        player.ForceAdjusted = 0; // broken: no collision this tick (never walked into the wall).
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up };

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        Assert.NotEqual(AlundraPlayerManager.ClimbingAnimationId, player.TargetAnimationId);
    }

    [Fact]
    public void CaseSixGate_CarryingEntity_DoesNotEnterClimbing()
    {
        var player = MakeQualifyingIdlePlayer();
        player.CarriedEntity = new CasaEngine.Framework.Scene.Entities.Entity(); // broken: carrying something.
        var pad = new AlundraPadState { ButtonsHold = AlundraPadState.Up };

        AlundraPlayerManager.MovePlayer(player, in pad, new AlundraGameState());

        Assert.NotEqual(AlundraPlayerManager.ClimbingAnimationId, player.TargetAnimationId);
    }

    /// <summary>Otherwise-qualifying setup for the case-6 gate (all five conditions hold) - a controller-
    /// less bare proxy is enough here since these tests only care about <c>TargetAnimationId</c>, not the
    /// per-tick vertical step (that is <see cref="AlundraLadderClimbTests"/>'s own production-call-site
    /// tests' job). Each "CaseSixGate_*" test above takes a fresh copy and breaks exactly ONE of the five
    /// conjuncts (LESSON 2 - proving the targeted path alone gates entry).</summary>
    private static AlundraEntityScriptProxy MakeQualifyingIdlePlayer() => new()
    {
        TargetAnimationId = 0, // Idle
        TargetDirection = 0x10, // already facing up
        Slope_18c = 6, // on a ladder cell (last frame's UpdateGroundSlope)
        ForceAdjusted = 1, // this tick's own movement was curtailed (walked into the wall)
        CarriedEntity = null,
    };

    /// <summary>Shared setup for the production-call-site tests above - self-skips (returns false) when
    /// <c>alundra-project/</c> is not present in this checkout, same convention as every sibling slice.</summary>
    private static bool TrySetUp(
        out World world, out Entity entity, out AlundraEntityScriptProxy proxy, out AlundraPlayerController controller, uint padHeld)
        => TrySetUp(out world, out entity, out proxy, out controller, () => new AlundraPadState { ButtonsHold = padHeld });

    private static bool TrySetUp(
        out World world, out Entity entity, out AlundraEntityScriptProxy proxy, out AlundraPlayerController controller,
        Func<AlundraPadState> padProvider)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            world = null!;
            entity = null!;
            proxy = null!;
            controller = null!;
            return false;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            world = null!;
            entity = null!;
            proxy = null!;
            controller = null!;
            return false;
        }

        (world, entity, proxy, controller) = BuildLadderHero(padProvider);
        return true;
    }
}
