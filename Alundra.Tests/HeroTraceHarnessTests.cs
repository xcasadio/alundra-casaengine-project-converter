#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Golden-trace oracle for the Alundra hero on the real map 389 (Ship Klark (beginning)), docs/plan-oracle-heros.md.
/// FAILS (does not silently skip) when <c>alundra-project/</c> is not present in this checkout (§2.8).
///
/// TWO scenarios (§2.6 bis amendment, 2026-08-26 - the terrain makes a single walk from the real New Game
/// spawn topologically unable to reach any slope cell: 3120 cells, 23 slope cells (<c>Slope &amp; 3 != 0</c>),
/// NONE bordering a height-0 cell even diagonally, closest neighbor height 5 (80px), while the hero's own
/// exported <c>StepHeight</c> is 3px and every non-slope cell differs from its neighbor by whole 16px units):
/// <list type="bullet">
/// <item><description><b>Scenario A "spawn"</b>: starts at the real New Game position (§2.2's own exact
/// recipe). Covers predicates 1 (plat) and 4 (mur) from that real position; predicate 5 (chute) is produced
/// by a documented mid-trace scripted reposition (see the trace header) onto a genuinely elevated,
/// non-slope tile directly bordering a real 32px cliff, then walking off it - a real gravity-driven fall,
/// with NO scripted vertical-only bump (post-mortem fix, see class doc below on the settings-clone bug this
/// replaced).</description></item>
/// <item><description><b>Scenario B "highground"</b>: starts DELIBERATELY seeded on an elevated, non-slope
/// landing tile directly adjacent to a slope cell, then walks down the ramp - same technique the
/// pre-existing <c>AlundraCharacterControllerAdoptionTests.Stairs_SteppingDownTheSlope</c> uses, and the
/// only vertical movement the terrain allows (descent). Covers predicates 2 (pente), 3 (marche).</description></item>
/// </list>
///
/// POST-MORTEM (independent verifier, 2026-08-26): the first version of this harness set
/// <c>Settings.Gravity</c>/<c>MaxFallSpeed</c>/<c>WalkabilityMask</c> on the LOCAL <c>CharacterControllerSettings</c>
/// object passed into <see cref="HeroWorldFixture.BuildHeroPawn"/>, AFTER that call already ran -
/// <c>CharacterControllerComponent.Settings</c>'s own setter CLONES the value it is given
/// (<c>_settings = value.Clone();</c>), so mutating the original post-construction silently wrote to an
/// orphaned copy nothing ever read; the LIVE controller kept the export's own zeroed Gravity forever. A
/// scripted vertical-only bump therefore never came back down (no engine gravity was ever really applied),
/// producing 57 "chute" frames of pure aerial stasis - confirmed by an isolated repro (since deleted) that
/// fell and landed correctly ONLY when Gravity was set BEFORE construction, matching every pre-existing
/// <c>AlundraCharacterControllerAdoptionTests</c> test's own ordering. Fixed here by mutating
/// <c>proxy.Controller!.Settings</c> (the LIVE, already-cloned instance - its getter returns
/// <c>_settings</c> directly, no further clone) AFTER construction, exactly like
/// <c>AlundraWorldProxy.AdoptPlayerPawn</c>'s own real production code does (<c>var settings =
/// proxy.Controller.Settings; settings.Gravity = ...;</c>).
/// </summary>
public class HeroTraceHarnessTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private readonly ITestOutputHelper _output;

    public HeroTraceHarnessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // -----------------------------------------------------------------------------------------
    // §1.3 headers - copied verbatim per the plan's own instruction.
    // -----------------------------------------------------------------------------------------

    private const string SixItemHeader =
        "This oracle does NOT cover (verbatim per docs/plan-oracle-heros.md §1.3):\n" +
        "1. AlundraWorldProxy.LogicTicksThisFrame (a future M3.a's own clock switch);\n" +
        "2. the E5.c camera catch-up (UpdateCameraFollow / StepCameraScroll);\n" +
        "3. RunMapEventsPass;\n" +
        "4. the D3 catch-up (RunPendingEventTriggers);\n" +
        "5. AlundraWorldProxy.AdoptPlayerPawn: this harness RE-COPIES its New Game initial state field by\n" +
        "   field, with no live link to production - if real adoption drifts, this trace stays green while\n" +
        "   the real New Game state has changed;\n" +
        "6. the real entry path of AlundraPlayerController.BuildPadState (Input, InputMappingManager,\n" +
        "   ComputePadState, the \"AlundraButtons\" mapping): short-circuited by this harness's own pad\n" +
        "   provider seam, consulted at the head of the method.\n";

    private const string SeventhItemHighGround =
        "7. (highground scenario only) this scenario's own initial state: NOT a reachable New Game state -\n" +
        "   deliberately seeded on an elevated, non-slope landing tile adjacent to a slope cell (see the\n" +
        "   \"start position\" note below), because no slope cell on this map borders height-0 ground.\n";

    private const string SeventhItemSpawn =
        "7. (spawn scenario only) the mid-trace scripted reposition that produces predicate 5 (chute): NOT\n" +
        "   a position the real New Game walk itself ever reaches - the same topological reason no slope\n" +
        "   cell is reachable means no cliff-top tile is either. See the \"chute\" note below.\n";

    // -----------------------------------------------------------------------------------------
    // Fixture plumbing (mirrors AlundraCharacterControllerAdoptionTests's own FindProjectRoot).
    // -----------------------------------------------------------------------------------------

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

    private static TileMapData LoadMap389TileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        if (!File.Exists(tileMapPath))
        {
            throw new InvalidOperationException($"HeroTraceHarnessTests: tileMap not found at '{tileMapPath}'.");
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    private static AlundraCellsCollisionField LoadMap389Field(TileMapData tileMapData)
    {
        var created = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "HeroTraceHarnessTests: map 389 AlundraCells custom property should parse and match MapSize.");
        return field!;
    }

    private static CharacterControllerSettings LoadHeroControllerSettings(string projectRoot)
    {
        var heroEntityPath = Path.Combine(projectRoot, "Entities", "Alundra", "Alundra.entity");
        if (!File.Exists(heroEntityPath))
        {
            throw new InvalidOperationException($"HeroTraceHarnessTests: hero prefab not found at '{heroEntityPath}'.");
        }

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

    /// <summary>§2.2: the hero's own sprite-record header (Flags/AnimSetsByAnim source) - keyed by the hero
    /// prefab's own asset id (<c>Entities/Alundra/Alundra.entity</c>'s "id" field), same catalog the real
    /// <c>AlundraWorldProxy.AdoptPlayerPawn</c> reads via <c>AssetCatalog.Get("Alundra")</c> - this harness
    /// has no live <c>AssetCatalog</c>, so it reads the prefab's own id directly off the JSON instead. FAILS
    /// (does not silently skip) when the header is missing - §2.2's own explicit requirement, since a
    /// missing header means AnimSetsByAnim stays null and the hero would never move (a green, motionless
    /// trace).</summary>
    private static SpriteRecordHeader LoadHeroHeader(string projectRoot)
    {
        var heroEntityPath = Path.Combine(projectRoot, "Entities", "Alundra", "Alundra.entity");
        var document = JObject.Parse(File.ReadAllText(heroEntityPath));
        var heroId = Guid.Parse((string)document["id"]!);

        var catalog = new SpriteRecordCatalog(projectRoot);
        if (!catalog.TryGet(heroId, out var header))
        {
            throw new InvalidOperationException(
                $"HeroTraceHarnessTests: no sprite-records.json header found for the hero prefab (id "
                + $"{heroId}) - AnimSetsByAnim would stay null and the hero would never move (§2.2).");
        }

        return header;
    }

    // -----------------------------------------------------------------------------------------
    // No-op event program runner / dedicated script host (§2.4) - unlike AlundraCharacterControllerAdoptionTests's
    // own FakeScriptHost, PlayerController here is a REAL AlundraPlayerController instance, seeded via the
    // §2.4 production field so MovePlayer is actually exercised.
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

    private sealed class HeroScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity => null;

        // §2.4: PlayerControlFlags = 0 explicitly (not the ALUNDRA_DEBUG_IGNORE_CONTROL_LOCK escape hatch),
        // so MovePlayer's real InputBlockedMask gate is exercised (and found open) rather than masked.
        public AlundraGameState GameState { get; } = new() { PlayerControlFlags = 0 };
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        private readonly AlundraLogicClock _logicClock = new();

        /// <summary>Last value this frame's own call computed - the harness's own frame driver reads this
        /// right after <c>World.Update</c> returns to populate the trace's own <c>dllTicks</c> column,
        /// since this is the ONLY caller of <see cref="LogicTicksThisFrame"/> in this harness (no
        /// world-level MapEvents/D3 passes exist here - §1.3).</summary>
        public int LastTicksThisFrame { get; private set; }

        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            LastTicksThisFrame = ticks;
            return ticks;
        }
    }

    // -----------------------------------------------------------------------------------------
    // §2.2 New Game state recipe (scenario A) - hand port of AlundraWorldProxy.AdoptPlayerPawn:1442-1518,
    // field by field, per §1.3 point 5's own documented "no live link to production" caveat.
    // -----------------------------------------------------------------------------------------

    private const int TileWidth = 24;
    private const int TileHeight = 16;
    private const int CameraTileX = AlundraGameState.CameraTileX;
    private const int CameraTileY = AlundraGameState.CameraTileY;

    /// <summary>Common header/dimensions/gravity-override setup shared by both scenarios' initial state -
    /// the ONLY difference between them is the position write (§2.2's exact recipe for scenario A vs a
    /// deliberately seeded position for scenario B, both callers' own responsibility). Overrides are
    /// applied to <c>proxy.Controller!.Settings</c> - the LIVE, already-cloned instance (see class doc's
    /// own post-mortem) - never to a detached pre-construction object.</summary>
    private static void ApplyCommonHeaderAndOverrides(AlundraEntityScriptProxy proxy, SpriteRecordHeader header, TileMapData tileMapData)
    {
        proxy.TargetAnimationId = 0; // §2.3 documented gap (Idle, not LoadingMap).
        proxy.TargetDirection = AlundraGameState.ResetDirectionId;
        proxy.CurrentAnimationId = ~0u;
        proxy.CurrentDirection = ~AlundraGameState.ResetDirectionId;
        proxy.IsOnGround = 1;

        proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));
        proxy.AnimSetsByAnim = header.AnimSets;
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);

        tileMapData.CustomProperties.TryGetValue("Gravity", out var gravityRaw);
        int.TryParse(gravityRaw, out var mapGravityRaw);
        tileMapData.CustomProperties.TryGetValue("ZViscosity", out var zViscosityRaw);
        int.TryParse(zViscosityRaw, out var mapZViscosityRaw);
        proxy.MapGravityRaw = mapGravityRaw;
        proxy.MapZViscosityRaw = mapZViscosityRaw;

        // AdoptPlayerPawn:1500-1518's own formula, applied to the LIVE controller Settings (see class doc).
        var liveSettings = proxy.Controller!.Settings;
        liveSettings.Gravity = mapGravityRaw * 256f / 65536f * 2500f;
        liveSettings.MaxFallSpeed = mapZViscosityRaw * 256f / 65536f * 50f;
        liveSettings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(proxy.Flags);
    }

    private static void ApplyNewGameState(AlundraEntityScriptProxy proxy, SpriteRecordHeader header, TileMapData tileMapData)
    {
        // PosX/PosY/PosZ then ClampToGround(), TileX/Y/Z - AdoptPlayerPawn:1442-1453.
        proxy.PosX = (CameraTileX * TileWidth + TileWidth / 2) << 16;
        proxy.PosY = (CameraTileY * TileHeight + TileHeight / 2) << 16;
        proxy.PosZ = 0;
        proxy.ClampToGround();
        proxy.TileX = (proxy.PosX >> 16) / TileWidth;
        proxy.TileY = (proxy.PosY >> 16) / TileHeight;
        proxy.TileZ = proxy.PosZ >> 20;

        ApplyCommonHeaderAndOverrides(proxy, header, tileMapData);
    }

    /// <summary>Scenario B (§2.6 bis): still the real hero header/overrides (movement math must be real),
    /// but the POSITION is deliberately seeded on an elevated landing tile - documented in the trace
    /// header, never claimed as a reachable New Game state.</summary>
    private static void ApplyHighGroundState(AlundraEntityScriptProxy proxy, SpriteRecordHeader header, TileMapData tileMapData, Vector3 startPosition)
    {
        proxy.PosX = (int)Math.Round((double)startPosition.X * 65536.0);
        proxy.PosY = (int)Math.Round((double)startPosition.Y * 65536.0);
        proxy.PosZ = (int)Math.Round((double)startPosition.Z * 65536.0);
        proxy.ClampToGround(); // no-op here (already exactly on the real ground), kept for parity with §2.2.
        proxy.TileX = (proxy.PosX >> 16) / TileWidth;
        proxy.TileY = (proxy.PosY >> 16) / TileHeight;
        proxy.TileZ = proxy.PosZ >> 20;

        ApplyCommonHeaderAndOverrides(proxy, header, tileMapData);
    }

    /// <summary>Full (x,y,z) scripted reposition via the SAME production entry point real scripted
    /// repositioning uses (0x64 SetEntitiesPosition et al - <see cref="AlundraEntityScriptProxy.PushLogicalPositionToRoot"/>).
    /// Used by scenario A only, mid-trace, to reach the one place on this map a real gravity-driven fall is
    /// reachable from - see the trace header's own note.</summary>
    private static void Reposition(AlundraEntityScriptProxy proxy, float x, float y, float z)
    {
        proxy.PosX = (int)Math.Round((double)x * 65536.0);
        proxy.PosY = (int)Math.Round((double)y * 65536.0);
        proxy.PosZ = (int)Math.Round((double)z * 65536.0);
        proxy.PushLogicalPositionToRoot();
    }

    // -----------------------------------------------------------------------------------------
    // §2.5 cadence burst - shared by every scenario/campaign: proves dllTicks=0, dllTicks>=2, and
    // dllTicks=AlundraScriptedMotion.MaxTicksPerFrame (4) all appear, and (fixedstep only)
    // engineStepsDelta>=2 and =CharacterMotionSystem.MaxStepsPerFrame (also 4, same dt drives both clocks)
    // and, on freestep, engineStepsDelta stays identically 0 throughout (CharacterMotionSystem.Update's own
    // early-out never increments ExecutedFixedStepCount when FixedTimeStep <= 0).
    //
    // A3 fix (verifier, 2026-08-26): the "0 ticks" cases below use 1/240s, NOT 0f - a zero-duration frame
    // never happens in real play. 1/240s (~4.17ms) is still short enough to round down to 0 ticks of
    // AlundraScriptedMotion.FixedTickSeconds (1/50s = 20ms) and 0 fixed steps of CharacterMotionSystem's own
    // FixedTimeStep (also 1/50s in the fixedstep campaign) - the same "0" case, reached plausibly instead of
    // via a duration that cannot occur.
    // -----------------------------------------------------------------------------------------

    private const float ShortNonZeroDt = 1f / 240f;

    private static readonly float[] CadenceBurstDt =
    {
        // Frame 1 is a normal 1/50s step first (establishes real grounding before any short-dt frame is
        // sampled - a short-dt FIRST frame would read Controller.IsGrounded before CharacterMotionSystem
        // ever ran a single full step, a harness initialization artifact rather than a genuine state).
        1f / 50f, ShortNonZeroDt, 2f / 50f, 1f / 50f, 5f / 50f, 1f / 50f, ShortNonZeroDt,
    };

    // -----------------------------------------------------------------------------------------
    // Predicate accounting (§2.7) - counts, not first-frame indices: the acceptance only requires each
    // relevant count > 0. Exact frame numbers (A1) are pinned separately, after inspecting one concrete
    // run - see the test method's own comment on where those numbers came from.
    // -----------------------------------------------------------------------------------------

    private sealed class PredicateCounts
    {
        public int Flat;
        public int Slope;
        public int Walk;
        public int Wall;
        public int Fall;
    }

    // -----------------------------------------------------------------------------------------
    // One scenario/campaign run.
    // -----------------------------------------------------------------------------------------

    private enum Scenario { Spawn, HighGround }

    private sealed record TraceLine(
        int Frame, long DtMicros, int DllTicks, long EngineStepsDelta, int PosX, int PosY, int PosZ,
        int TileZ, int IsOnGround, int ForceAdjusted, uint TargetAnim, uint TargetDir, int CellSlope, int CellHeight)
    {
        public string ToDataLine() => string.Join(" | ", new object[]
        {
            Frame, DtMicros, DllTicks, EngineStepsDelta, PosX, PosY, PosZ, TileZ, IsOnGround, ForceAdjusted,
            TargetAnim, TargetDir, CellSlope, CellHeight,
        }.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
    }

    private sealed record RunResult(IReadOnlyList<TraceLine> Lines, PredicateCounts Counts, long MaxEngineStepsDelta)
    {
        public string DataText => string.Join("\n", Lines.Select(l => l.ToDataLine())) + "\n";
    }

    /// <summary>Scenario A's own mid-trace reposition target (§2.6 bis amendment / F1 fix): tile (24,56),
    /// pixel (588,904,32) - a non-slope, height-2 (32px) tile whose EAST neighbor, tile (25,56), is
    /// height-0 (flat room floor, this map's own global height minimum) - a real 32px cliff (well past
    /// GroundSnapDistance=4px), confirmed by direct AlundraCellsRecords inspection. Walking east off it is
    /// a genuine, uninterrupted gravity-driven fall - no scripted vertical bump.</summary>
    private const float RepositionX = 588f;
    private const float RepositionY = 904f;
    private const float RepositionZ = 32f;

    private static RunResult RunOnce(
        Scenario scenario, float fixedTimeStep, string projectRoot, TileMapData tileMapData,
        AlundraCellsCollisionField field, SpriteRecordHeader header)
    {
        var settings = LoadHeroControllerSettings(projectRoot);
        var world = HeroWorldFixture.BuildWorld(field);
        world.RuntimeSystems.CharacterMotion.FixedTimeStep = fixedTimeStep;
        world.RuntimeSystems.CharacterMotion.MaxStepsPerFrame = 4;

        var controller = new AlundraPlayerController();
        var host = new HeroScriptHost { PlayerController = controller };

        Vector3 startPosition = scenario == Scenario.Spawn
            ? new Vector3(CameraTileX * TileWidth + TileWidth / 2f, CameraTileY * TileHeight + TileHeight / 2f, 0f)
            // Tile (21,49): non-slope landing, h=8 (128px), directly west-adjacent to slope cell (20,49)
            // (Slope&3=3, "ladder exiting") - see the trace header's own note.
            : new Vector3(516f, 792f, 128f);

        var (entity, proxy) = HeroWorldFixture.BuildHeroPawn(world, settings, startPosition, host);

        if (scenario == Scenario.Spawn)
        {
            ApplyNewGameState(proxy, header, tileMapData);
        }
        else
        {
            ApplyHighGroundState(proxy, header, tileMapData, startPosition);
        }

        var width = tileMapData.MapSize.Width;
        var height = tileMapData.MapSize.Height;
        var records = ReadRecords(tileMapData);

        (int Slope, int Height) SampleCell(int posX, int posY)
        {
            var x = posX >> 16;
            var y = posY >> 16;
            var cellX = Math.Clamp(x / 24, 0, width - 1);
            var cellY = Math.Clamp(y / 16, 0, height - 1);
            var idx = cellY * width + cellX;
            return (records.Slope[idx] & 0x3, records.Height[idx]);
        }

        uint pad = 0;
        AlundraPadState PadProvider() => new() { ButtonsHold = pad, ButtonsJustPressed = 0 };
        controller.PadStateProviderForTests = PadProvider;

        var lines = new List<TraceLine>();
        var counts = new PredicateCounts();
        var maxEngineStepsDelta = 0L;

        var previousPosZ = proxy.PosZ;
        var previousExecutedSteps = 0L;
        var consecutiveAirborne = 0;
        var previousTileZ = proxy.TileZ;
        var previousIsOnGround = proxy.IsOnGround;

        // §2.5 schedule: cadence burst first, then a steady 1/50s walk long enough to reach every
        // predicate this scenario is responsible for (empirically sized - see plan §2.6 bis / this
        // method's own scenario split).
        var dtSchedule = new List<float>(CadenceBurstDt);
        var steadyFrameCount = scenario == Scenario.Spawn ? 260 : 110;
        for (var i = 0; i < steadyFrameCount; i++)
        {
            dtSchedule.Add(1f / 50f);
        }

        // Scenario A only (§2.6 bis / F1 fix): mid-walk full reposition (Reposition, not a Z-only bump) to
        // the one real cliff-top tile this map's own topology makes reachable from "no climbing needed" -
        // documented in the trace header (§1.3 item 7 for this scenario). Frame 210 is well past the wall
        // interaction (empirically: forceAdjusted=1 starts around frame 98, see the test's own pinned
        // assertion). Direction switches from Left (west, into the wall) to Right (east, off the cliff).
        const int repositionFrameIndex = 210;

        for (var frame = 1; frame <= dtSchedule.Count; frame++)
        {
            var dt = dtSchedule[frame - 1];

            if (scenario == Scenario.Spawn && frame == repositionFrameIndex)
            {
                Reposition(proxy, RepositionX, RepositionY, RepositionZ);
            }

            pad = scenario == Scenario.Spawn && frame >= repositionFrameIndex
                ? AlundraPadState.Right
                : AlundraPadState.Left;

            world.Update(dt);

            var executedSteps = world.RuntimeSystems.CharacterMotion.ExecutedFixedStepCount;
            var engineStepsDelta = executedSteps - previousExecutedSteps;
            previousExecutedSteps = executedSteps;
            if (engineStepsDelta > maxEngineStepsDelta)
            {
                maxEngineStepsDelta = engineStepsDelta;
            }

            var (cellSlope, cellHeight) = SampleCell(proxy.PosX, proxy.PosY);
            var dtMicros = (long)Math.Round(dt * 1_000_000.0);

            // Predicate accounting (§2.7), at frame granularity, using this frame's own previous-frame
            // values (captured BEFORE this frame overwrites them below).
            if (proxy.IsOnGround == 1 && cellSlope == 0 && proxy.PosZ == previousPosZ)
            {
                counts.Flat++;
            }

            if (proxy.IsOnGround == 1 && cellSlope != 0)
            {
                counts.Slope++;
            }

            if (proxy.IsOnGround == 1 && previousIsOnGround == 1 && proxy.TileZ != previousTileZ)
            {
                counts.Walk++;
            }

            if (proxy.ForceAdjusted == 1)
            {
                counts.Wall++;
            }

            if (proxy.IsOnGround == 0)
            {
                consecutiveAirborne++;
                if (consecutiveAirborne >= 2)
                {
                    counts.Fall++;
                }
            }
            else
            {
                consecutiveAirborne = 0;
            }

            lines.Add(new TraceLine(
                frame, dtMicros, host.LastTicksThisFrame, engineStepsDelta, proxy.PosX, proxy.PosY, proxy.PosZ,
                proxy.TileZ, proxy.IsOnGround, proxy.ForceAdjusted, proxy.TargetAnimationId, proxy.TargetDirection,
                cellSlope, cellHeight));

            previousPosZ = proxy.PosZ;
            previousTileZ = proxy.TileZ;
            previousIsOnGround = proxy.IsOnGround;
        }

        return new RunResult(lines, counts, maxEngineStepsDelta);
    }

    private static AlundraCellsRecords ReadRecords(TileMapData tileMapData)
    {
        Assert.True(AlundraCellsRecords.TryParse(tileMapData.CustomProperties, WorldName, out var records));
        return records;
    }

    private static readonly string[] ColumnNames =
    {
        "frame", "dtMicros", "dllTicks", "engineStepsDelta", "posX", "posY", "posZ", "tileZ", "isOnGround",
        "forceAdjusted", "targetAnim", "targetDir", "cellSlope", "cellHeight",
    };

    /// <summary>A2 fix (verifier, 2026-08-26): which data columns actually differ between the freestep and
    /// fixedstep campaigns of the SAME scenario, computed for real off the two runs' own data lines (not
    /// asserted a priori) - written into BOTH campaigns' own header text, since this is exactly the
    /// measurement §1.2 exists to produce (the deferred M3.a decision this oracle informs).</summary>
    private static string DescribeCampaignDifference(RunResult freestep, RunResult fixedstep)
    {
        var differingColumns = new SortedSet<int>();
        var lineCount = Math.Min(freestep.Lines.Count, fixedstep.Lines.Count);
        for (var i = 0; i < lineCount; i++)
        {
            var a = freestep.Lines[i].ToDataLine().Split(" | ");
            var b = fixedstep.Lines[i].ToDataLine().Split(" | ");
            for (var col = 0; col < a.Length; col++)
            {
                if (a[col] != b[col])
                {
                    differingColumns.Add(col);
                }
            }
        }

        if (freestep.Lines.Count != fixedstep.Lines.Count)
        {
            return $"freestep has {freestep.Lines.Count} data lines, fixedstep has {fixedstep.Lines.Count} - different lengths.";
        }

        var names = string.Join(", ", differingColumns.Select(c => ColumnNames[c]));
        return differingColumns.Count == 0
            ? "no column differs (unexpected - see acceptance's own NotEqual check, which would then fail)."
            : $"only column(s) [{names}] differ between the two campaigns' own data lines - every other "
              + "column (including every behavioral one: position, ground state, predicates) is identical, "
              + "i.e. FixedTimeStep has NO observable effect on the hero beyond its own bookkeeping column, "
              + "on this exact scripted walk.";
    }

    private static string BuildHeader(Scenario scenario, float fixedTimeStep, string? campaignDifferenceNote)
    {
        var sb = new StringBuilder();
        sb.Append("# Hero trace oracle - map 389 (Ship Klark (beginning)) - docs/plan-oracle-heros.md\n");
        sb.Append($"# Scenario: {(scenario == Scenario.Spawn ? "spawn (real New Game position)" : "highground (deliberately seeded, see below)")}\n");
        sb.Append($"# Campaign: {(fixedTimeStep <= 0f ? "freestep (FixedTimeStep=0, today's production default)" : "fixedstep (FixedTimeStep=1/50, a future M3.a's own target)")}\n");
        sb.Append("#\n");
        sb.Append("# " + SixItemHeader.Replace("\n", "\n# ").TrimEnd() + "\n");
        sb.Append("# " + (scenario == Scenario.HighGround ? SeventhItemHighGround : SeventhItemSpawn).Replace("\n", "\n# ").TrimEnd() + "\n");
        sb.Append("#\n");

        if (scenario == Scenario.HighGround)
        {
            sb.Append("# Start position: tile (21,49), pixel (516,792,128) - a non-slope landing (height 8,\n");
            sb.Append("# 128px) directly west-adjacent to slope cell (20,49) (Slope&3=3, \"ladder exiting\").\n");
            sb.Append("# Justification: no slope cell on map 389 borders height-0 ground (closest neighbor\n");
            sb.Append("# height is 5, 80px) and the hero's own exported StepHeight (3px) blocks any climb\n");
            sb.Append("# above a 16px cell-height quantum, so a walk starting at the real New Game position\n");
            sb.Append("# can never reach a slope cell. This scenario instead reproduces the one motion the\n");
            sb.Append("# terrain DOES allow from up here: walking the ramp down (predicates 2/pente, 3/marche).\n");
        }
        else
        {
            sb.Append("# §2.3 documented gap: starts at TargetAnimationId=0 (Idle)/CurrentAnimationId=~0, not\n");
            sb.Append("# AdoptPlayerPawn's real LoadingMap(0x36) - the LoadingMap->Idle bridge needs an\n");
            sb.Append("# animation asset absent from this headless montage.\n");
            sb.Append("#\n");
            sb.Append($"# Predicate 5 (chute) note: at frame {RepositionRuntimeFrameForHeader}, the hero is\n");
            sb.Append($"# repositioned (PushLogicalPositionToRoot - the SAME production entry point real\n");
            sb.Append($"# scripted repositioning uses, e.g. opcode 0x64) to tile (24,56), pixel (588,904,32) -\n");
            sb.Append("# a non-slope, height-2 (32px) tile whose east neighbor (tile 25,56) is height-0 (this\n");
            sb.Append("# map's own global floor minimum) - a real 32px cliff. The pad then switches to Right\n");
            sb.Append("# (east); the hero walks off the cliff and FALLS for real (engine gravity, no scripted\n");
            sb.Append("# Z-only bump) before landing back on the real height-0 floor. Neither this tile nor\n");
            sb.Append("# any climb is reachable from the real New Game walk itself (§2.6 bis) - the reposition\n");
            sb.Append("# is a documented, one-shot relocation, not a claim that this position is reachable.\n");
        }

        if (campaignDifferenceNote != null)
        {
            sb.Append("#\n");
            sb.Append($"# Campaign comparison (§1.2 measurement): {campaignDifferenceNote}\n");
        }

        sb.Append("#\n");
        sb.Append("# Columns: frame | dtMicros | dllTicks | engineStepsDelta | posX | posY | posZ | tileZ |\n");
        sb.Append("# isOnGround | forceAdjusted | targetAnim | targetDir | cellSlope | cellHeight\n");
        return sb.ToString();
    }

    private const int RepositionRuntimeFrameForHeader = 210;

    // -----------------------------------------------------------------------------------------
    // The test.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void HeroTrace_Map389_SpawnAndHighGround_BothCampaigns_ProduceGoldenTraces()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            throw new InvalidOperationException(
                "HeroTraceHarnessTests: 'alundra-project/' was not found in this checkout - this oracle "
                + "cannot run without the real converter export (map 389 AlundraCells + the hero's own "
                + "sprite-records.json header). Anti-faux-vert (§2.8): this test FAILS rather than skips.");
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        var field = LoadMap389Field(tileMapData);
        var header = LoadHeroHeader(projectRoot);

        var docsDir = Path.Combine(Directory.GetParent(projectRoot)!.FullName, "docs");
        Directory.CreateDirectory(docsDir);

        // Run every scenario/campaign combo twice (determinism, §3) before writing anything - see below.
        var spawnFreestep1 = RunOnce(Scenario.Spawn, 0f, projectRoot, tileMapData, field, header);
        var spawnFreestep2 = RunOnce(Scenario.Spawn, 0f, projectRoot, tileMapData, field, header);
        Assert.Equal(spawnFreestep1.DataText, spawnFreestep2.DataText);

        var spawnFixedstep1 = RunOnce(Scenario.Spawn, 1f / 50f, projectRoot, tileMapData, field, header);
        var spawnFixedstep2 = RunOnce(Scenario.Spawn, 1f / 50f, projectRoot, tileMapData, field, header);
        Assert.Equal(spawnFixedstep1.DataText, spawnFixedstep2.DataText);

        var highgroundFreestep1 = RunOnce(Scenario.HighGround, 0f, projectRoot, tileMapData, field, header);
        var highgroundFreestep2 = RunOnce(Scenario.HighGround, 0f, projectRoot, tileMapData, field, header);
        Assert.Equal(highgroundFreestep1.DataText, highgroundFreestep2.DataText);

        var highgroundFixedstep1 = RunOnce(Scenario.HighGround, 1f / 50f, projectRoot, tileMapData, field, header);
        var highgroundFixedstep2 = RunOnce(Scenario.HighGround, 1f / 50f, projectRoot, tileMapData, field, header);
        Assert.Equal(highgroundFixedstep1.DataText, highgroundFixedstep2.DataText);

        // A2 fix: compare DATA LINES ONLY between campaigns of the SAME scenario (headers differ by
        // construction - comparing full text made this assertion trivially true) - and measure/record which
        // column(s) actually differ, per scenario.
        Assert.NotEqual(spawnFreestep1.DataText, spawnFixedstep1.DataText);
        var spawnDiffNote = DescribeCampaignDifference(spawnFreestep1, spawnFixedstep1);

        Assert.NotEqual(highgroundFreestep1.DataText, highgroundFixedstep1.DataText);
        var highgroundDiffNote = DescribeCampaignDifference(highgroundFreestep1, highgroundFixedstep1);

        void WriteAndCheck(RunResult result, Scenario scenario, float fixedTimeStep, string fileSuffix, string diffNote, bool requireSlopeWalk)
        {
            var headerText = BuildHeader(scenario, fixedTimeStep, diffNote);
            var fullText = headerText + result.DataText;
            var path = Path.Combine(docsDir, $"hero-trace-389-{fileSuffix}.txt");
            File.WriteAllText(path, fullText);

            _output.WriteLine(
                $"{fileSuffix}: flat={result.Counts.Flat} slope={result.Counts.Slope} walk={result.Counts.Walk} "
                + $"wall={result.Counts.Wall} fall={result.Counts.Fall} maxEngineStepsDelta={result.MaxEngineStepsDelta}");

            if (requireSlopeWalk)
            {
                Assert.True(result.Counts.Slope > 0, $"{fileSuffix}: predicate 2 (pente) must be > 0.");
                Assert.True(result.Counts.Walk > 0, $"{fileSuffix}: predicate 3 (marche) must be > 0.");
            }
            else
            {
                Assert.True(result.Counts.Flat > 0, $"{fileSuffix}: predicate 1 (plat) must be > 0.");
                Assert.True(result.Counts.Wall > 0, $"{fileSuffix}: predicate 4 (mur) must be > 0.");
                Assert.True(result.Counts.Fall > 0, $"{fileSuffix}: predicate 5 (chute) must be > 0.");
            }

            // §3 cadence assertions, PER scenario (amendment): dllTicks 0/>=2/=MaxTicksPerFrame present in
            // BOTH campaigns.
            var dllTicksValues = result.Lines.Select(l => l.DllTicks).ToArray();
            Assert.Contains(0, dllTicksValues);
            Assert.Contains(dllTicksValues, v => v >= 2);
            Assert.Contains(AlundraScriptedMotion.MaxTicksPerFrame, dllTicksValues);

            var engineStepsDeltaValues = result.Lines.Select(l => l.EngineStepsDelta).ToArray();
            if (fixedTimeStep > 0f)
            {
                Assert.Contains(engineStepsDeltaValues, v => v >= 2);
                Assert.Contains(4L, engineStepsDeltaValues);
            }
            else
            {
                Assert.True(engineStepsDeltaValues.All(v => v == 0), $"{fileSuffix}: engineStepsDelta must be identically 0 on freestep.");
            }
        }

        WriteAndCheck(spawnFreestep1, Scenario.Spawn, 0f, "spawn-freestep", spawnDiffNote, requireSlopeWalk: false);
        WriteAndCheck(spawnFixedstep1, Scenario.Spawn, 1f / 50f, "spawn-fixedstep", spawnDiffNote, requireSlopeWalk: false);
        WriteAndCheck(highgroundFreestep1, Scenario.HighGround, 0f, "highground-freestep", highgroundDiffNote, requireSlopeWalk: true);
        WriteAndCheck(highgroundFixedstep1, Scenario.HighGround, 1f / 50f, "highground-fixedstep", highgroundDiffNote, requireSlopeWalk: true);

        // A1 fix (verifier, 2026-08-26): exact pinned values, not just counts - every number below was
        // independently read off spawnFreestep1's own data (docs/hero-trace-389-spawn-freestep.txt),
        // exactly as this same technique is used in IntroTraceHarnessTests. Re-derived and cross-checked
        // against spawnFixedstep1 where noted (the two campaigns share the same dllTicks/position values -
        // see the campaign-comparison note above - so the SAME pinned numbers apply to both, except
        // engineStepsDelta itself).
        var spawnLines = spawnFreestep1.Lines;
        var firstWallFrame = spawnLines.First(l => l.ForceAdjusted == 1).Frame;
        Assert.Equal(98, firstWallFrame);

        var wallPosX = spawnLines.First(l => l.ForceAdjusted == 1).PosX;
        Assert.Equal(36956160, wallPosX); // frozen against the wall - see AlundraCellsCollisionField's own tx22/23 boundary.

        // First real directional input takes effect: MovePlayer sets TargetAnimationId=1 (Moving) the very
        // first frame ButtonsHold != 0 - frame 1 itself (pad is already Left from the very first Update).
        Assert.Equal(1u, spawnLines.First().TargetAnim);

        // Reposition frame (§2.6 bis mid-trace relocation): posZ jumps to exactly 32px (2097152 in 16.16)
        // the SAME frame, then genuinely decreases (real gravity) before landing back at 0.
        var repositionLine = spawnLines.Single(l => l.Frame == RepositionRuntimeFrameForHeader);
        Assert.Equal(2097152, repositionLine.PosZ); // 32px << 16.
        Assert.Equal(1, repositionLine.IsOnGround); // still grounded THIS frame (walking on the new tile).

        var postRepositionLines = spawnLines.Where(l => l.Frame > RepositionRuntimeFrameForHeader).ToList();
        var firstAirborneAfterReposition = postRepositionLines.First(l => l.IsOnGround == 0);
        Assert.Equal(221, firstAirborneAfterReposition.Frame); // first frame the wider cliff-edge probe misses ground.

        // The FIRST airborne frame's own posZ has not moved yet (gravity only starts building velocity
        // that frame - the very next controller Update, same shape as PhysicsEngine's own one-tick lag);
        // the NEXT frame is where the real, engine-driven descent becomes visible.
        Assert.Equal(2097152, firstAirborneAfterReposition.PosZ);
        var firstDescendingFrame = postRepositionLines.First(l => l.Frame == firstAirborneAfterReposition.Frame + 1);
        Assert.True(firstDescendingFrame.PosZ < 2097152, "posZ must genuinely decrease the frame after going airborne (real gravity, not a frozen bump).");

        var landingLine = postRepositionLines.First(l => l.Frame > firstAirborneAfterReposition.Frame && l.IsOnGround == 1);
        Assert.Equal(232, landingLine.Frame);
        Assert.Equal(0, landingLine.PosZ); // lands exactly on the real height-0 floor.
        Assert.True(landingLine.Frame > firstAirborneAfterReposition.Frame + 1, "the fall must last more than one frame.");
    }
}
