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
/// T3 (docs/plan-transitions-carte.md §3) - DETECTION ONLY: the tile-flag probe
/// (<see cref="AlundraEntityScriptProxy.UpdateVramFlags"/>, D-T-10), the portal record parse
/// (<see cref="AlundraWorldProxy.BuildPortals"/>, §1.1), the slot scan
/// (<see cref="AlundraPortalScanner.FindPortalAtTile"/>, §1.2.b), and the trigger predicate
/// (<see cref="AlundraPortalTrigger.TryGetTrigger"/>, §1.2.a), including its exact
/// <see cref="AlundraPlayerManager.MovePlayer"/> call site. No transition is ever started here - see
/// <see cref="AlundraPortalTrigger"/>'s own class doc.
/// </summary>
public class AlundraPortalDetectionTests : IDisposable
{
    // D-T-14: this class constructs AlundraWorldProxy, so it meets the operational criterion and resets
    // the session carriers in the constructor (the isolation-carrying half - xunit builds a fresh
    // instance per test) and in Dispose (hygiene). Its own tests build private AlundraGameState
    // instances rather than touching the session one, so nothing leaks today; the block is here so the
    // criterion stays mechanical rather than something each new class has to re-reason about.
    public AlundraPortalDetectionTests() => ResetSessionCarriers();

    public void Dispose() => ResetSessionCarriers();

    private static void ResetSessionCarriers()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // ---------------------------------------------------------------------------------------
    // Section A - AlundraPortalScanner.FindPortalAtTile (§1.2.b), pure, no map data needed.
    // ---------------------------------------------------------------------------------------

    private static AlundraPortalRecord Portal(int index, int x1, int y1, int x2, int y2, int destMapId, int arrivalDirectionIndex = 0, int requiredFacing = 0)
        => new()
        {
            Index = index,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            DestMapId = destMapId,
            DestTileX = 0,
            DestTileY = 0,
            ZLevel = 0,
            Flags = (requiredFacing << 14) | (arrivalDirectionIndex << 12),
        };

    [Fact]
    public void FindPortalAtTile_FirstOverlappingSlotWins_NotTheSmallestOrClosest()
    {
        // Two overlapping slots both contain (5,5) - slot 0 covers a huge area, slot 1 is a tight
        // mono-cell match. §1.2.b: FIRST match wins, regardless of which rectangle is smaller/closer.
        var portals = new List<AlundraPortalRecord>
        {
            Portal(0, 0, 0, 10, 10, destMapId: 100),
            Portal(1, 5, 5, 5, 5, destMapId: 200),
        };

        var found = AlundraPortalScanner.FindPortalAtTile(portals, 5, 5);

        Assert.NotNull(found);
        Assert.Equal(100, found!.DestMapId);
    }

    [Fact]
    public void FindPortalAtTile_DestMapIdZero_BlocksScan_EvenWhenALaterSlotAlsoMatches()
    {
        // Required mutation (T3 ticket): "continuer le balayage après une destination nulle -> le test
        // de blocage tombe". Slot 0 matches with DestMapId == 0 (must return null and STOP); slot 1 also
        // matches the same tile with a real destination - it must never be reached.
        var portals = new List<AlundraPortalRecord>
        {
            Portal(0, 5, 5, 5, 5, destMapId: 0),
            Portal(1, 0, 0, 10, 10, destMapId: 200),
        };

        var found = AlundraPortalScanner.FindPortalAtTile(portals, 5, 5);

        Assert.Null(found);
    }

    [Fact]
    public void FindPortalAtTile_NoSlotContainsTile_ReturnsNull()
    {
        var portals = new List<AlundraPortalRecord> { Portal(0, 0, 0, 3, 3, destMapId: 100) };

        var found = AlundraPortalScanner.FindPortalAtTile(portals, 50, 50);

        Assert.Null(found);
    }

    // ---------------------------------------------------------------------------------------
    // Section B - AlundraPortalTrigger.TryGetTrigger (§1.2.a), pure, synthetic proxy fields (no real
    // map data needed - CombinedVramFlagsAND/TileX/TileY/AnimationDirection are set directly).
    // ---------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy Player(int tileX, int tileY, uint combinedVramFlagsAnd, int animationDirection, uint targetDirection = 0xFF)
        => new()
        {
            IsPlayer = true,
            TileX = tileX,
            TileY = tileY,
            CombinedVramFlagsAND = combinedVramFlagsAnd,
            AnimationDirection = animationDirection,
            TargetDirection = targetDirection,
        };

    [Fact]
    public void HoleBranch_TriggersUnconditionally_IgnoringFacingHeldKeyAndPlayerControlFlags()
    {
        // §1.2.a hole branch (PlayerManager.cs:3455-3468): no orientation test, no held key, no
        // PlayerControlFlags test - only that a portal is found at the player's tile.
        var portals = new List<AlundraPortalRecord> { Portal(0, 1, 1, 1, 1, destMapId: 390, arrivalDirectionIndex: 2) };
        var player = Player(tileX: 1, tileY: 1, combinedVramFlagsAnd: 0x4, animationDirection: 3);
        var state = new AlundraGameState { PlayerControlFlags = 0xFFFFFFFF };
        var pad = new AlundraPadState { ButtonsHold = 0 };

        var trigger = AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals);

        Assert.NotNull(trigger);
        Assert.Equal(0, trigger!.Value.Portal.Index);
        Assert.Equal(AnimationTables.CardinalDirectionTable[2], trigger.Value.ArrivalDirectionId);
    }

    [Fact]
    public void HoleBranch_NoPortalAtTile_ReturnsNull()
    {
        var portals = new List<AlundraPortalRecord> { Portal(0, 9, 9, 9, 9, destMapId: 390) };
        var player = Player(tileX: 1, tileY: 1, combinedVramFlagsAnd: 0x4, animationDirection: 0);
        var state = new AlundraGameState();
        var pad = new AlundraPadState();

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals));
    }

    [Fact]
    public void PortalFloorBranch_AllConjunctsHold_ReturnsPortalAndResolvedArrivalDirection()
    {
        // RequiredFacingDirection 1 -> RequiredInputByFacing[1] == 0x1000 (Up). ArrivalDirectionIndex 1
        // -> AnimationTables.CardinalDirectionTable[1] == 0x10, never the raw index 1 (§1.2.c).
        var portals = new List<AlundraPortalRecord> { Portal(0, 18, 38, 18, 38, destMapId: 390, arrivalDirectionIndex: 1, requiredFacing: 1) };
        var player = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 1);
        var state = new AlundraGameState { PlayerControlFlags = 0 };
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };

        var trigger = AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals);

        Assert.NotNull(trigger);
        Assert.Equal(0x10u, trigger!.Value.ArrivalDirectionId);
    }

    [Fact]
    public void PortalFloorBranch_MissingFloorBit_ReturnsNull()
    {
        var portals = new List<AlundraPortalRecord> { Portal(0, 18, 38, 18, 38, destMapId: 390, requiredFacing: 1) };
        var player = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0, animationDirection: 1);
        var state = new AlundraGameState();
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals));
    }

    [Fact]
    public void PortalFloorBranch_PlayerControlFlagsNonZero_ReturnsNull()
    {
        // PlayerManager.cs:3429 - a test PROPER TO THIS BRANCH, distinct from the InputBlockedMask gate
        // MovePlayer's own caller applies elsewhere - any nonzero value blocks it, not just a masked bit.
        var portals = new List<AlundraPortalRecord> { Portal(0, 18, 38, 18, 38, destMapId: 390, requiredFacing: 1) };
        var player = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 1);
        var state = new AlundraGameState { PlayerControlFlags = 1 };
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals));
    }

    [Fact]
    public void PortalFloorBranch_RequiredKeyNotHeld_ReturnsNull()
    {
        var portals = new List<AlundraPortalRecord> { Portal(0, 18, 38, 18, 38, destMapId: 390, requiredFacing: 1) };
        var player = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 1);
        var state = new AlundraGameState();
        var pad = new AlundraPadState { ButtonsHold = 0 };

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals));
    }

    [Fact]
    public void PortalFloorBranch_OrientationTestReadsAnimationDirection_NeverTargetDirection()
    {
        // §1.2.a/point 4 of the ticket: the comparison MUST read AnimationDirection (domain 0..3), never
        // TargetDirection (domain {0x00,0x08,0x10,0x18}). RequiredFacingDirection is 1: give the player an
        // AnimationDirection that MISMATCHES (2) but a TargetDirection that, if misread as the compared
        // field, would spuriously "match" (1) - the predicate must still refuse to trigger.
        var portals = new List<AlundraPortalRecord> { Portal(0, 18, 38, 18, 38, destMapId: 390, requiredFacing: 1) };
        var player = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 2, targetDirection: 1);
        var state = new AlundraGameState();
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(player, in pad, state, portals));

        // Conversely: AnimationDirection MATCHES (1) while TargetDirection carries an unrelated
        // domain-0x00/0x08/0x10/0x18 value (0x10) - the predicate must still fire, proving it never
        // reads TargetDirection either way.
        var player2 = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 1, targetDirection: 0x10);
        Assert.NotNull(AlundraPortalTrigger.TryGetTrigger(player2, in pad, state, portals));
    }

    [Fact]
    public void IsWarpDisabled_SuppressesBothBranches()
    {
        // Point 5 of the ticket: the predicate folds in AlundraGameState.IsWarpDisabled itself (the
        // original only tests g_isWarpDisabled downstream, in HandleWarpTransition - T4's own scope).
        var portals = new List<AlundraPortalRecord>
        {
            Portal(0, 1, 1, 1, 1, destMapId: 390), // hole-eligible tile.
            Portal(1, 18, 38, 18, 38, destMapId: 390, requiredFacing: 1), // portal-floor-eligible tile.
        };
        var state = new AlundraGameState { IsWarpDisabled = true };
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };

        var holePlayer = Player(tileX: 1, tileY: 1, combinedVramFlagsAnd: 0x4, animationDirection: 0);
        Assert.Null(AlundraPortalTrigger.TryGetTrigger(holePlayer, in pad, state, portals));

        var floorPlayer = Player(tileX: 18, tileY: 38, combinedVramFlagsAnd: 0x8000, animationDirection: 1);
        Assert.Null(AlundraPortalTrigger.TryGetTrigger(floorPlayer, in pad, state, portals));
    }

    // ---------------------------------------------------------------------------------------
    // Section C - real map 389 data: the four-corner probe (D-T-10) feeding the trigger predicate, on
    // the ACTUAL portal 0 acceptance pair (§1.1.d: mono-cell, tile (18,38) -> map 390 (10,40)).
    // Same fixture/self-skip pattern as AlundraGroundSlopeTests.
    // ---------------------------------------------------------------------------------------

    private const string WorldName = "Ship Klark (beginning)-389";
    private const int CellWidthPx = 24;
    private const int CellHeightPx = 16;

    // Same real converter-exported hero footprint AlundraGroundSlopeTests uses.
    private const int HeroOffsetX = -10;
    private const int HeroOffsetY = -7;
    private const int HeroSizeX = 21;
    private const int HeroSizeY = 15;

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

    private static string Map389TileMapPath(string projectRoot) => Path.Combine(
        projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
        "Ship Klark (beginning)-389.tileMap");

    private static TileMapData LoadMap389TileMapData(string projectRoot)
    {
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(Map389TileMapPath(projectRoot))));
        return tileMapData;
    }

    private static AlundraCellsCollisionField? LoadMap389Field(TileMapData tileMapData)
    {
        var created = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "map 389's AlundraCells custom property should parse and match MapSize.");
        return field;
    }

    private static int QualifyingPosZ(int cellHeightUnits) => cellHeightUnits * CellHeightPx << 16;

    /// <summary>Same shape as AlundraGroundSlopeTests' own BuildProbe - see that method's own doc for the
    /// F4 footprint-derivation rationale. Duplicated rather than shared: each test file owns its own
    /// minimal fixture, matching this project's existing convention (AlundraFloorHeightTests/
    /// AlundraLadderClimbTests each keep their own copy too).</summary>
    private static AlundraEntityScriptProxy BuildProbe(World world, int x1, int y1, int posZ, int tileX, int tileY)
    {
        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(0f, 0f, 0f), new NoOpScriptHost());
        world.Update(1f / 50f);

        proxy.PosX = (x1 - HeroOffsetX) << 16;
        proxy.PosY = (y1 - HeroOffsetY) << 16;
        proxy.PosZ = posZ;
        proxy.ModX = HeroOffsetX << 16;
        proxy.ModY = HeroOffsetY << 16;
        proxy.ModZ = 0;
        proxy.Width = (HeroSizeX << 16) - 1;
        proxy.Height = (HeroSizeY << 16) - 1;
        proxy.Flags = EntityFlags.Gravity;
        proxy.TileX = tileX;
        proxy.TileY = tileY;
        return proxy;
    }

    private sealed class NoOpScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner => throw new NotSupportedException();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => 0;
    }

    private static List<AlundraPortalRecord> BuildMap389PortalZero() => new()
    {
        // §1.1.c/§1.1.d, real exported data: portal 0 of map 389, mono-cell, Flags 20481 (0x5001) ->
        // RequiredFacing 1, ArrivalDirection 1, TransitionEffect 0, WarpBehavior 1.
        new AlundraPortalRecord { Index = 0, X1 = 18, Y1 = 38, X2 = 18, Y2 = 38, DestMapId = 390, DestTileX = 10, DestTileY = 40, ZLevel = 0, Flags = 20481 },
    };

    [Fact]
    public void PortalZeroAcceptance_AllFourCornersQualifyAndCarryTheBit_TriggersWithHeldKeyAndFacing()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout.
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // §1.1.f: tile (18,38) height = 8 cell units, ground_property = 128 (0x80 -> bit 0x8000 once
        // shifted <<8). Footprint fully inside the cell (same "flush against top-left corner" recipe as
        // AlundraGroundSlopeTests' own ScaleCell test) - all four corners land on (18,38).
        var x1 = 18 * CellWidthPx + 1;
        var y1 = 38 * CellHeightPx;
        var proxy = BuildProbe(world, x1, y1, QualifyingPosZ(8), tileX: 18, tileY: 38);

        proxy.UpdateVramFlags();

        Assert.Equal(0x8000u, proxy.CombinedVramFlagsAND & 0x8000u);

        var portals = BuildMap389PortalZero();
        var state = new AlundraGameState();
        var pad = new AlundraPadState { ButtonsHold = 0x1000 }; // RequiredFacingDirection 1 -> Up.
        proxy.AnimationDirection = 1; // matches portal 0's RequiredFacingDirection.

        var trigger = AlundraPortalTrigger.TryGetTrigger(proxy, in pad, state, portals);

        Assert.NotNull(trigger);
        Assert.Equal(390, trigger!.Value.Portal.DestMapId);
        Assert.Equal(0x10u, trigger.Value.ArrivalDirectionId); // CardinalDirectionTable[1].
    }

    [Fact]
    public void PortalZeroAcceptance_FootprintStraddlesNeighborCellWithoutTheBit_DoesNotTrigger()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);

        // Mandatory negative case (T3 ticket): the player's own TILE is (18,38) - the portal's own
        // mono-cell rectangle - but the FOOTPRINT spills 1px into row 39 below (§1.6.e: neighbouring
        // tiles do NOT carry ground_property 128/the 0x8000 bit). A single disqualified/differently-
        // flagged corner zeroes the AND outright (§1.6.b) - no trigger, even though TileX/TileY match.
        var x1 = 18 * CellWidthPx + 1;
        var y1 = 38 * CellHeightPx + 2;
        var proxy = BuildProbe(world, x1, y1, QualifyingPosZ(8), tileX: 18, tileY: 38);

        // Sanity check on the geometry itself - prove the footprint really does straddle two rows,
        // otherwise this test would not exercise the documented negative case at all.
        var y2 = (proxy.PosY + proxy.ModY + proxy.Height) >> 16;
        Assert.Equal(38, y1 / CellHeightPx);
        Assert.Equal(39, y2 / CellHeightPx);

        proxy.UpdateVramFlags();

        Assert.Equal(0u, proxy.CombinedVramFlagsAND & 0x8000u);

        var portals = BuildMap389PortalZero();
        var state = new AlundraGameState();
        var pad = new AlundraPadState { ButtonsHold = 0x1000 };
        proxy.AnimationDirection = 1;

        Assert.Null(AlundraPortalTrigger.TryGetTrigger(proxy, in pad, state, portals));
    }

    // ---------------------------------------------------------------------------------------
    // Section D - D-T-12 non-regression: DestroyOnVramFlags must stay untouched for NPCs (the probe is
    // PLAYER ONLY, wired from a single call site in AlundraEntityScriptProxy.Update's IsPlayer branch).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void NpcOnPortalTile_UpdateNeverPopulatesCombinedVramFlags_RegardlessOfQualifyingPosition()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var field = LoadMap389Field(tileMapData);
        if (field == null)
        {
            return;
        }

        var world = HeroWorldFixture.BuildWorld(field);
        var host = new NoOpScriptHost();

        // Same fully-qualifying pose as the positive acceptance test above - if UpdateVramFlags were
        // (incorrectly) reached for a non-player entity, CombinedVramFlagsOR/AND would come back non-zero
        // here exactly like the player's own case did. Spawned DIRECTLY at the qualifying world position
        // (not post-hoc PosX/PosY/PosZ writes) - Update's own pose-repatriation block re-pulls PosX/Y/Z
        // from Owner.RootComponent every call whenever a Controller is attached (E3.d), so the ROOT
        // itself must already sit at the qualifying pose before Update runs, exactly like
        // ProductionCallSite_HeroSeededOnScaleCell (AlundraGroundSlopeTests) does for its own player case.
        var x1 = 18 * CellWidthPx + 1;
        var y1 = 38 * CellHeightPx;
        var rootX = x1 - HeroOffsetX;
        var rootY = y1 - HeroOffsetY;
        var groundHeightPx = 8 * CellHeightPx;

        var settings = new CharacterControllerSettings();
        var (_, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(rootX, rootY, groundHeightPx), host);
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);

        proxy.IsPlayer = false; // D-T-12: NPC, not the player.
        proxy.Flags = EntityFlags.Gravity | EntityFlags.DestroyOnVramFlags;

        // One real production frame: CharacterMotionSystem runs first (root stays put - already exactly
        // on real ground, no pad input), then AlundraEntityScriptProxy.Update's own NPC branch runs -
        // the exact live call chain, never IsPlayer's own branch since IsPlayer is false here.
        world.Update(1f / 50f);

        Assert.Equal(0u, proxy.CombinedVramFlagsOR);
        Assert.Equal(0u, proxy.CombinedVramFlagsAND);
    }

    // ---------------------------------------------------------------------------------------
    // Section E - AlundraWorldProxy.BuildPortals (§1.1/§3): real-data parse of the "Portals" layer.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void BuildPortals_Map389_ParsesNineFieldsPlusIndex_MatchingPortalZero()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var portalsLayer = tileMapData.ObjectLayers.Find(layer => layer.Name == "Portals");
        Assert.NotNull(portalsLayer);
        Assert.Equal(4, portalsLayer!.Objects.Count); // §1.1.c: map 389 has 4 portals.

        var worldProxy = new AlundraWorldProxy();
        worldProxy.BuildPortals(portalsLayer);

        var portals = ((IAlundraScriptHost)worldProxy).Portals;
        Assert.Equal(4, portals.Count);

        var portalZero = portals[0];
        Assert.Equal(0, portalZero.Index);
        Assert.Equal(18, portalZero.X1);
        Assert.Equal(38, portalZero.Y1);
        Assert.Equal(18, portalZero.X2);
        Assert.Equal(38, portalZero.Y2);
        Assert.Equal(390, portalZero.DestMapId);
        Assert.Equal(10, portalZero.DestTileX);
        Assert.Equal(40, portalZero.DestTileY);
        Assert.Equal(0, portalZero.ZLevel);
        Assert.Equal(20481, portalZero.Flags);
        Assert.Equal(1u, portalZero.RequiredFacingDirection);
        Assert.Equal(1, portalZero.ArrivalDirectionIndex);
        Assert.Equal(0, portalZero.TransitionEffectId);
        Assert.Equal(1, portalZero.WarpBehaviorId);
    }

    [Fact]
    public void BuildPortals_CalledTwice_RebuildsInsteadOfAppending()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var portalsLayer = tileMapData.ObjectLayers.Find(layer => layer.Name == "Portals");
        Assert.NotNull(portalsLayer);

        var worldProxy = new AlundraWorldProxy();
        worldProxy.BuildPortals(portalsLayer);
        worldProxy.BuildPortals(portalsLayer);

        // T4/T5 bring world reloads; appending would duplicate every portal, and first-match-wins
        // (§1.2.b) makes the scan order-sensitive, so duplicates are not merely wasteful.
        Assert.Equal(4, ((IAlundraScriptHost)worldProxy).Portals.Count);
    }

    [Fact]
    public void RequiredFacingDirection_StaysAValidTableIndex_WhateverTheExportPutsInFlags()
    {
        // Flags is parsed from a string custom property into an int here, while the original reads a
        // ushort (Portal.cs:32), where the shift is 0..3 by construction. The mask keeps this an
        // in-range index into the four-entry required-input and cardinal-direction tables.
        var portal = Portal(index: 0, x1: 0, y1: 0, x2: 0, y2: 0, destMapId: 1);
        var wide = new AlundraPortalRecord
        {
            Index = portal.Index,
            X1 = portal.X1,
            Y1 = portal.Y1,
            X2 = portal.X2,
            Y2 = portal.Y2,
            DestMapId = portal.DestMapId,
            Flags = unchecked((int)0xDEAD5001),
        };

        Assert.InRange(wide.RequiredFacingDirection, 0u, 3u);
    }

    // ---------------------------------------------------------------------------------------
    // Section F - AlundraPlayerManager.MovePlayer's own call site (§1.2.a/point 4 of the ticket): the
    // hole branch must fire even with an InputBlockedMask bit posed - proof the trigger predicate is
    // wired BEFORE that gate, not after (a required mutation: "placer la sonde après la porte
    // InputBlockedMask -> le test de la branche trou tombe").
    // ---------------------------------------------------------------------------------------

    private sealed class RecordingRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) { }
        public void RunSpriteEvent(AlundraEntityScriptProxy entity) { }
    }

    private sealed class RecordingPortalScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new RecordingRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public IReadOnlyList<AlundraPortalRecord> Portals { get; init; } = Array.Empty<AlundraPortalRecord>();
        public readonly List<(AlundraPortalRecord Portal, uint ArrivalDirectionId)> Triggered = new();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => 0;

        public void OnPortalTriggerDetected(AlundraPortalRecord portal, uint arrivalDirectionId)
            => Triggered.Add((portal, arrivalDirectionId));
    }

    [Fact]
    public void MovePlayer_HoleBranch_FiresThroughTheHostSeam_EvenWithInputBlockedMaskPosed()
    {
        var portals = BuildMap389PortalZero(); // any real portal covering the player's own tile works here.
        var host = new RecordingPortalScriptHost { Portals = portals };
        var state = host.GameState;
        // A ControlLocked bit (part of InputBlockedMask, PlayerManager.cs:38) posed - the hole branch
        // must still fire, since the predicate is called BEFORE that gate (PlayerManager.cs:29).
        state.PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked;

        var player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            TileX = 18,
            TileY = 38,
            CombinedVramFlagsAND = 0x4, // hole bit.
            AnimationDirection = 0,
        };
        var pad = new AlundraPadState();

        AlundraPlayerManager.MovePlayer(player, in pad, state, host);

        var fired = Assert.Single(host.Triggered);
        Assert.Equal(390, fired.Portal.DestMapId);
    }

    [Fact]
    public void MovePlayer_PortalFloorBranch_DoesNotFire_WhenNoPortalCoversTheTile()
    {
        // Negative control for the MovePlayer-level wiring itself: with no portal at the player's tile,
        // the host's own seam must never be called, hole bit or not.
        var host = new RecordingPortalScriptHost { Portals = Array.Empty<AlundraPortalRecord>() };
        var state = host.GameState;

        var player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            TileX = 1,
            TileY = 1,
            CombinedVramFlagsAND = 0x8000,
            AnimationDirection = 0,
        };
        var pad = new AlundraPadState { ButtonsHold = 0x4000 };

        AlundraPlayerManager.MovePlayer(player, in pad, state, host);

        Assert.Empty(host.Triggered);
    }

    [Fact]
    public void MovePlayer_NullHost_SkipsTriggerDetectionEntirely_NoException()
    {
        // Degraded mode (same convention as CheckEntityInteraction's own host==null skip) - the ~19
        // direct movement-only MovePlayer callers elsewhere in this test project pass host: null and
        // must keep working unchanged.
        var player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            TileX = 18,
            TileY = 38,
            CombinedVramFlagsAND = 0x4,
        };
        var state = new AlundraGameState();
        var pad = new AlundraPadState();

        AlundraPlayerManager.MovePlayer(player, in pad, state, host: null);
    }
}
