#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
/// Characterization test for <see cref="AlundraWorldProxy.Update"/> (docs/plan-update-caracterisation.md
/// S1) - the method had ZERO coverage before this file, both direct (no test calls it) and indirect
/// (<c>World.Update</c> only reaches the proxy through a live <c>Game.ExecutionPolicy</c>, which no test
/// wires - see the plan's §1). This file exists PURELY to pin the CURRENT behaviour of steps 2-4 of
/// <c>Update</c> (<c>ResolveDebugCameraOnce</c>/<c>UpdateCameraFollow</c>/<c>UpdateDebugCameraPan</c>) so
/// slices S2/S3 of that same plan can extract that wiring into its own collaborator with a safety net.
/// It touches zero production code (S1 is test-only) and covers zero new behaviour - the numbers pinned
/// here are frozen exactly as reconnaissance found them, defect included (see item 4 below).
///
/// <para><b>Montage - every point below traces back to a defect the plan's own relecture found; see
/// docs/plan-update-caracterisation.md §1/§3 for the full reasoning, not restated here:</b></para>
/// <list type="bullet">
/// <item><description>A headless <see cref="World"/>, built directly (no <see cref="CasaEngineGame"/> at
/// all - unlike <see cref="HeroWorldFixture.BuildWorld"/>, which wires a live-ish
/// <see cref="CasaEngineGame"/>/<c>GameManager</c>): <c>ApplyOriginalBackgroundClearColorOnce</c> NREs on
/// a <c>Game</c> without reflected <c>GameManager</c>/<c>ViewManager</c>, and S1 does not need one since
/// every wiring step it pins is a documented no-op without a live game.</description></item>
/// <item><description>The world's <see cref="World.Name"/> carries NO trailing <c>-&lt;digits&gt;</c> -
/// <see cref="AlundraWorldProxy.InitializeWithWorld"/> calls <c>_backdropRenderer.Load</c> BEFORE its own
/// early return, and that <c>Load</c> dereferences <c>world.Game</c> unless <c>BackdropLoader.Load</c>
/// degrades to null first, which only happens for a name with no map index.</description></item>
/// <item><description>A camera entity added DIRECTLY to the mutable <c>World.Entities</c> list -
/// <c>World.AddEntity</c> only queues (<c>InternalAddEntities</c> never runs without a <c>Game</c>, and
/// swallows the <c>CameraComponent.InitializeWithWorld</c> exception BEFORE the entity ever lands in
/// <c>Entities</c>), so it is the only way to get a resolvable camera into this headless world at all.
/// Presence is asserted before the first frame (below).</description></item>
/// <item><description>Because that means <c>CameraComponent.InitializeWithWorld</c> never runs, the
/// camera's <c>Viewport</c> stays <c>default</c> (Height 0) - <c>ResolveDebugCameraOnce</c> therefore
/// clamps <c>Zoom</c> down to <see cref="Camera2dComponent.MinimumZoom"/>, not some viewport-derived
/// value. Pinned as such, not guessed.</description></item>
/// <item><description>An explicit followed target (<see cref="AlundraWorldProxy.EntityFollowedByCamera"/>)
/// that <see cref="AlundraEntityScriptProxy.IsLoadedNormalOrDeactivated"/>, whose logical position is
/// moved between frames by stated pixel amounts - without one the look-at never changes and items 4/5/6
/// below would have nothing to observe.</description></item>
/// <item><description><c>_debugCameraOffset</c> seeded to a NON-ZERO value by reflection, isolated in the
/// one private helper <see cref="SeedDebugCameraOffset"/> - at offset zero
/// <c>UpdateDebugCameraPan</c>'s own pan step is the IDENTITY on <c>Target</c> (base adopts
/// <c>Target</c> itself when it changed), so the pan's own contribution - and swapping steps 3/4 - would
/// be completely unobservable.</description></item>
/// <item><description><see cref="AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests"/> set
/// explicitly (constructor/<see cref="IDisposable.Dispose"/> below) - the real static/env-var seam is
/// documented as unreliable under a shared xunit host.</description></item>
/// </list>
///
/// <para><b>Proof scope, stated honestly (plan §2):</b> this file's oracle covers <c>Update</c>'s steps
/// 2-4 only (camera resolve/follow/pan). Steps 5-6 (background clear color, backdrop) are no-ops without
/// a <c>Game</c> and carry no oracle here; steps 7-13 need a populated <c>_spawnedEntities</c>, out of
/// scope for this slice (see the plan's own non-goals). Every world built below therefore has NO "tileMap"
/// entity - <c>InitializeWithWorld</c> takes its own early return right after the (harmless, degraded)
/// backdrop load, before ever touching <c>AdoptPlayerPawn</c>/the entity-spawn loop.</para>
/// </summary>
public sealed class AlundraWorldProxyUpdateCharacterizationTests : IDisposable
{
    public AlundraWorldProxyUpdateCharacterizationTests()
    {
        // Trap 7 (plan §1): the env-var-backed static is unreliable under a shared xunit host - go
        // through the test seam explicitly instead. The value itself is inert for every scenario below
        // (world.Game is always null here, so UpdateDebugCameraPan's gamepad branch never runs at all),
        // but the plan requires calling it anyway rather than relying on the static's own initial state.
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // Shared montage helpers
    // -----------------------------------------------------------------------------------------

    /// <summary>Headless world, no <see cref="World.Game"/>, name with no trailing "-&lt;digits&gt;" - see
    /// this class' own doc for why both properties are load-bearing.</summary>
    private static World BuildHeadlessWorld() => new() { Name = "TestWorld" };

    /// <summary>Camera entity built so <see cref="Entity.GetComponent{T}"/> finds the
    /// <see cref="Camera2dComponent"/> by making it the entity's own root - added to
    /// <paramref name="world"/>'s <see cref="World.Entities"/> DIRECTLY (see this class' own doc); presence
    /// is asserted here, before any frame runs.</summary>
    private static Camera2dComponent AddCameraEntity(World world)
    {
        var camera = new Camera2dComponent();
        var cameraEntity = new Entity { Name = "camera", RootComponent = camera };

        world.Entities.Add(cameraEntity);

        Assert.Contains(cameraEntity, world.Entities);
        return camera;
    }

    /// <summary>A followed-target proxy that <see cref="AlundraEntityScriptProxy.IsLoadedNormalOrDeactivated"/>
    /// (<see cref="EntityStatus.Normal"/>) at the given LOGICAL pixel position (converted to this proxy's
    /// own 16.16 fixed-point <c>PosX</c>/<c>PosY</c>/<c>PosZ</c> units).</summary>
    private static AlundraEntityScriptProxy BuildFollowedTarget(int posXPixels, int posYPixels, int posZPixels = 0)
        => new()
        {
            Status = EntityStatus.Normal,
            PosX = posXPixels << 16,
            PosY = posYPixels << 16,
            PosZ = posZPixels << 16,
        };

    /// <summary>The ONE reflection seam this montage needs (plan §3 S1: isolated so S2 has a single line
    /// to re-point once <c>_debugCameraOffset</c> moves to the new camera-wiring collaborator).</summary>
    private static void SeedDebugCameraOffset(AlundraWorldProxy proxy, Vector3 offset)
    {
        var field = typeof(AlundraCameraDirector).GetField("_debugCameraOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(proxy._cameraDirector, offset);
    }

    /// <summary>The non-zero offset every scenario below seeds - see this class' own doc on why zero
    /// would make the pan's own contribution to <c>Target</c> unobservable.</summary>
    private static readonly Vector3 SeededOffset = new(5f, 7f, 0f);

    // -----------------------------------------------------------------------------------------
    // Item 1 - first frame: PixelSnap/Zoom resolved, Target = smoothed target + offset (the SUM).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void FirstFrame_ResolvesPixelSnapAndZoom_AndTargetIsSmoothedTargetPlusOffset()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);

        // lookAt = (100, 200, 0) -> ComputeCameraLookAtRenderPosition(100, 200, 0)
        //        = (100, -(200 - 0) + 16, 0) = (100, -184, 0) - the first-frame SNAP target (needsSnap is
        // set by InitializeWithWorld and consumed by this very first UpdateCameraFollow call).
        proxy.EntityFollowedByCamera = BuildFollowedTarget(posXPixels: 100, posYPixels: 200);

        proxy.Update(0.02f); // exactly one 50Hz logic tick.

        Assert.True(camera.PixelSnap);
        Assert.Equal(Camera2dComponent.MinimumZoom, camera.Zoom);
        // Smoothed target (100, -184, 0), snapped, plus the non-zero seeded offset (5, 7, 0) - the SUM,
        // not either value alone, so the pan's own contribution is provably present.
        Assert.Equal(new Vector3(105f, -177f, 0f), camera.Target);
    }

    // -----------------------------------------------------------------------------------------
    // Item 2 - the relay (trap 3): the follow's Target write is UNCONDITIONAL, including on a zero-tick
    // frame, so an external write is overwritten and the pan adopts the follow's own value as its base.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZeroTickFrame_FollowRewritesTargetUnconditionally_AndPanAdoptsThatAsItsBase()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);

        var followed = BuildFollowedTarget(posXPixels: 100, posYPixels: 200);
        proxy.EntityFollowedByCamera = followed;

        proxy.Update(0.02f); // frame 1: 1 tick - establishes Target = (105, -177, 0), see item 1.
        Assert.Equal(new Vector3(105f, -177f, 0f), camera.Target);

        // Perturb Target externally with a value the smoothed target/base would NOT naturally produce,
        // then run a ZERO-tick frame (accumulator sits at 0 after frame 1, so 0.0f elapsed carries no
        // tick at all).
        var externalWrite = new Vector3(999f, 888f, 0f);
        camera.Target = externalWrite;

        proxy.Update(0f); // frame 2: 0 ticks.

        // UNMUTATED: UpdateCameraFollow still writes _cameraSmoothedTarget (unchanged at (100, -184, 0),
        // since 0 ticks ran and the followed entity did not move) straight over the external write - so
        // Target ends up back at exactly the frame-1 value, NOT at externalWrite + offset (which is what
        // a conditional write - mutation (a) - would produce instead: (1004, 895, 0)).
        Assert.Equal(new Vector3(105f, -177f, 0f), camera.Target);
        Assert.NotEqual(externalWrite + SeededOffset, camera.Target);
    }

    // -----------------------------------------------------------------------------------------
    // Item 3 - the resolve latch: Zoom/PixelSnap are set on the FIRST frame only.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZoomAndPixelSnap_AreSetOnlyOnFirstFrame_NotRestoredOnLaterFrames()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);
        proxy.EntityFollowedByCamera = BuildFollowedTarget(posXPixels: 0, posYPixels: 0);

        proxy.Update(0.02f); // frame 1 resolves once.
        Assert.True(camera.PixelSnap);
        Assert.Equal(Camera2dComponent.MinimumZoom, camera.Zoom);

        // Mutate both fields externally between frames.
        camera.Zoom = 42f;
        camera.PixelSnap = false;

        proxy.Update(0.02f); // frame 2 - ResolveDebugCameraOnce is a no-op (_debugCameraLookupDone latch).

        Assert.Equal(42f, camera.Zoom);
        Assert.False(camera.PixelSnap);
    }

    // -----------------------------------------------------------------------------------------
    // Item 4 - the latch defect (trap 1), CHARACTERIZED AS-IS, not fixed: an Update called BEFORE
    // InitializeWithWorld permanently disables camera resolution. This test documents a bug, not a
    // desired behaviour - see docs/plan-update-caracterisation.md §2bis, follow-up unit U-1, which will
    // fix ResolveDebugCameraOnce and must then invert this very test.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CharacterizedDefect_UpdateBeforeInitializeWithWorld_PermanentlyDisablesCamera_SeeFollowUpU1()
    {
        var proxy = new AlundraWorldProxy();

        // The premature call: _world is still null here. ResolveDebugCameraOnce sets its one-shot latch
        // BEFORE checking _world != null (trap 1) - so the camera lookup never gets a second chance, ever.
        proxy.Update(0.02f);

        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);

        var followed = BuildFollowedTarget(posXPixels: 100, posYPixels: 200);
        proxy.EntityFollowedByCamera = followed;

        var targetBeforeFrames = camera.Target;
        Assert.Equal(Vector3.Zero, targetBeforeFrames); // Camera2dComponent's own untouched default.

        proxy.Update(0.02f); // camera still never resolves - _debugCamera stays null forever.

        // Move the followed target substantially - if the camera were resolved, Target would move too.
        followed.PosX = 500 << 16;
        followed.PosY = 700 << 16;
        proxy.Update(0.02f);

        // Characterized: Target NEVER moves, even though the followed target did.
        Assert.Equal(Vector3.Zero, camera.Target);
    }

    // -----------------------------------------------------------------------------------------
    // Item 5 - _cameraNeedsSnap: the first frame SNAPS straight to the look-at target; later frames only
    // take ONE StepCameraScroll increment toward it, however far the target actually is.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CameraNeedsSnap_FirstFrameSnaps_LaterFramesOnlyStepTowardTheTarget()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);

        var followed = BuildFollowedTarget(posXPixels: 0, posYPixels: 0);
        proxy.EntityFollowedByCamera = followed;

        proxy.Update(0.02f); // frame 1 (1 tick): needsSnap - smoothed jumps straight to (0, 16, 0).
        Assert.Equal(new Vector3(5f, 23f, 0f), camera.Target); // (0,16,0) + offset (5,7,0).

        // Move the look-at FAR away in one jump.
        followed.PosX = 1600 << 16; // lookAt render target becomes (1600, 16, 0).

        proxy.Update(0.02f); // frame 2 (1 tick): NOT a snap - StepCameraScroll floors deltaX >> 4.
        // deltaX = 1600 - 0 = 1600; 1600 >> 4 = 100 -> smoothed = (100, 16, 0), NOT (1600, 16, 0).
        Assert.Equal(new Vector3(105f, 23f, 0f), camera.Target);
    }

    // -----------------------------------------------------------------------------------------
    // Item 6 - cadence: 0/1/>=2-tick frames produce different distances travelled by Target, because
    // UpdateCameraFollow steps once per LOGIC TICK, not once per rendered frame.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Cadence_ZeroOneAndMultiTickFrames_MoveTargetByDifferentDistances()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        SeedDebugCameraOffset(proxy, SeededOffset);

        var followed = BuildFollowedTarget(posXPixels: 0, posYPixels: 0);
        proxy.EntityFollowedByCamera = followed;

        // Frame 0 (1 tick, accumulator 0.02 -> consumed exactly): consumes the initial snap and settles
        // the smoothed target at (0, 16, 0) - see item 5. Baseline Target = (5, 23, 0).
        Assert.Equal(1, proxy.LogicTicksThisFrame(0.02f));
        proxy.Update(0.02f);
        Assert.Equal(new Vector3(5f, 23f, 0f), camera.Target);

        // The look-at now sits far away and stays PUT for the next three frames, so each frame's own
        // tick count is the only thing that changes how far Target moves.
        followed.PosX = 8000 << 16; // lookAt render target: (8000, 16, 0).

        // Frame A: dt = 0.01 -> accumulator 0.01 -> 0 ticks.
        Assert.Equal(0, proxy.LogicTicksThisFrame(0.01f));
        proxy.Update(0.01f);
        var targetAfterA = camera.Target;
        Assert.Equal(new Vector3(5f, 23f, 0f), targetAfterA); // unmoved: 0 ticks ran.

        // Frame B: dt = 0.02 -> accumulator 0.01 + 0.02 = 0.03 -> 1 tick (accumulator left at 0.01).
        // StepCameraScroll: deltaX = 8000 - 0 = 8000; 8000 >> 4 = 500 -> smoothed X = 500.
        Assert.Equal(1, proxy.LogicTicksThisFrame(0.02f));
        proxy.Update(0.02f);
        var targetAfterB = camera.Target;
        Assert.Equal(new Vector3(505f, 23f, 0f), targetAfterB);

        // Frame C: dt = 0.05 -> accumulator 0.01 + 0.05 = 0.06 -> 3 ticks (accumulator left at 0).
        // tick1: deltaX = 8000 - 500 = 7500; 7500 >> 4 = 468 -> 968.
        // tick2: deltaX = 8000 - 968 = 7032; 7032 >> 4 = 439 -> 1407.
        // tick3: deltaX = 8000 - 1407 = 6593; 6593 >> 4 = 412 -> 1819.
        Assert.Equal(3, proxy.LogicTicksThisFrame(0.05f));
        proxy.Update(0.05f);
        var targetAfterC = camera.Target;
        Assert.Equal(new Vector3(1824f, 23f, 0f), targetAfterC);

        // The three frames' own travelled distances (in X) are all different - 0, 500, 1319 - which is
        // the whole point of the cadence fix (E5.c): tick count, not rendered-frame count, drives motion.
        var distanceA = MathF.Abs(targetAfterA.X - 5f);
        var distanceB = MathF.Abs(targetAfterB.X - targetAfterA.X);
        var distanceC = MathF.Abs(targetAfterC.X - targetAfterB.X);

        Assert.Equal(0f, distanceA);
        Assert.Equal(500f, distanceB);
        Assert.Equal(1319f, distanceC);
        Assert.True(distanceA != distanceB && distanceB != distanceC && distanceA != distanceC);
    }

    // -----------------------------------------------------------------------------------------
    // Item 7 (C1, docs/plan-camera-ordre-frame.md) - the map-events/camera ORDER defect: a scripted
    // teleport (0x64) that runs THIS frame must be visible to THIS SAME frame's camera Target, exactly
    // like the original (GameEngine.cs:1638-1664/1743-1753 - RunMapEvents runs BEFORE the look-at
    // update). Today Update runs the camera block (steps 2-4) BEFORE the map-events pass (step 7), so
    // the camera still sees the PRE-move position. This test drives the real proxy.Update(...) - the
    // only way to traverse RunMapEventsPass at all, see this class' own "Montage" note below.
    //
    // Montage - each point traces back to a defect the plan's own relecture found (see plan §4):
    //  - The shared montage above (no "tileMap" entity) cannot reach the map-event pass on its own:
    //    without one, InitializeWithWorld returns before AdoptPlayerPawn, PlayerEntity stays null, and
    //    Update's own "if (PlayerEntity != null)" gate around RunMapEventsPass is never entered.
    //    AdoptPlayerPawn itself is not headless-reachable (needs a live AlundraPlayerController) - so
    //    this test seeds PlayerEntity/_mapEvents directly through the two members C1 widened to
    //    internal for exactly this reason (PlayerEntity's setter, BuildMapEvents).
    //  - ONE AND THE SAME proxy is moved and followed: PlayerEntity and EntityFollowedByCamera are set
    //    to the SAME instance. A distinct followed entity could not work - the 0x64 below uses search
    //    type 0x80 ("the owner"), which RunMapEventsPass always resolves to the player it was called
    //    with; every OTHER search type walks SpawnedEntities, empty in this montage.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MapEventTeleport_IsVisibleToCameraTarget_SameFrame()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no "tileMap" entity -> early return, PlayerEntity stays null.

        // Seed PlayerEntity == EntityFollowedByCamera (same instance, see montage note above), Normal so
        // IsLoadedNormalOrDeactivated holds, at an arbitrary PRE-move position - anything other than the
        // 0x64's own target below, so a camera reading the stale position is observably wrong.
        var playerProxy = new AlundraEntityScriptProxy
        {
            Status = EntityStatus.Normal,
            PosX = 1 << 16,
            PosY = 2 << 16,
            PosZ = 0,
            TileX = 0,
            TileY = 0,
        };
        proxy.PlayerEntity = playerProxy;
        proxy.EntityFollowedByCamera = playerProxy;

        // One MapEvents record whose zone [X1,X2]x[Y1,Y2] contains the player's own tile (0,0) - see
        // RunMapEventsPass's own "out-of-zone reset" branch, which would otherwise skip the program
        // entirely instead of running it. EventCodesBIndex=0x81 (masked 0x7F=1 != 0, passes
        // RunMapEventsPass's own "not a dud slot" gate; the SAME masked value then selects
        // EventCodesBTable[1] - see AlundraEventProgramRunnerTests's own
        // "RunScript_SlotB_ResumesAcrossCalls" comment for this exact masking - 0x80 alone masks to 0
        // and would be skipped by the dud-slot gate before ever reaching the interpreter).
        var record = new TileMapObjectData();
        record.CustomProperties["EventCodesBIndex"] = "129";
        record.CustomProperties["Index"] = "1";
        record.CustomProperties["X1"] = "0";
        record.CustomProperties["Y1"] = "0";
        record.CustomProperties["X2"] = "100";
        record.CustomProperties["Y2"] = "100";
        var mapEventsLayer = new TileMapObjectLayerData();
        mapEventsLayer.Objects.Add(record);
        proxy.BuildMapEvents(mapEventsLayer);

        // The program: 0x64 SetEntitiesPosition(v1=0x80 "owner", x=0x234, y=0x178, z=0xa0), then 0xFF
        // End - the exact operand encoding AlundraEventProgramRunnerTests.
        // SetEntitiesPosition_0x64_SetsPosXYZ_FromRealMap389Operands uses (real map 389 bytes), so the
        // resulting PosX/PosY/PosZ are 0x234<<16 / 0x178<<16 / (0xa0<<16)+1. Table[1]=0 points slot B's
        // resolved index (masked 1, see above) at code offset 0, the program's own start.
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesBTable = new[] { 0, 0 },
            Codes = new[] { 0x64, 0x80, 0x34, 0x02, 0x78, 0x01, 0xa0, 0x00, 0xFF },
        };
        // Replaces InitializeWithWorld's own degraded runner (world "TestWorld" has no trailing map id,
        // so MapEventProgramLoader.Load returns null and the wired runner is a permanent no-op) with a
        // real, document-backed one - proxy itself is the IEntityWorldContext, exactly like production
        // wiring (InitializeWithWorld: "new AlundraEventProgramRunner(eventProgramDocument, GameState,
        // this)").
        proxy.EventProgramRunner = new AlundraEventProgramRunner(document, proxy.GameState, proxy);

        proxy.Update(0.02f); // exactly one 50Hz logic tick - runs RunMapEventsPass exactly once.

        // The move actually happened (sanity: proves the 0x64 ran at all, independent of ordering).
        Assert.Equal(0x234 << 16, playerProxy.PosX);
        Assert.Equal(0x178 << 16, playerProxy.PosY);
        Assert.Equal((0xa0 << 16) + 1, playerProxy.PosZ);

        // Target must reflect the position AFTER the move, in the SAME frame - exactly like the
        // original (look-at update is the LAST thing UpdateEntities does, after RunMapEvents already
        // ran this frame). lookAt=(0x234,0x178,160) [PosZ>>16 truncates (0xa0<<16)+1 back to 160] ->
        // ComputeCameraLookAtRenderPosition = (0x234, -(0x178-160)+16, 0) = (564, -200, 0). No map
        // bounds are wired in this headless montage (_tileMapData stays null), so nothing clamps it,
        // and needsSnap (armed by InitializeWithWorld) makes this frame jump straight there - see item
        // 1 above for the same snap reasoning.
        Assert.Equal(new Vector3(564f, -200f, 0f), camera.Target);
    }

    // -----------------------------------------------------------------------------------------
    // docs/plan-camera-premiere-frame.md - the real user-reported bug: on a free time-step engine, the
    // very first rendered frame carries ZERO logic ticks (plan §1), so neither the map-events loop nor
    // the camera snap ran before that frame's own camera resolve - the one armed snap is spent on the
    // player's raw spawn pose instead of wherever the intro's own opening script (map-event 0, program
    // 129) retargets the camera one instruction later. Written BEFORE the fix, and must fail first
    // (plan §4).
    // -----------------------------------------------------------------------------------------

    private const string Map389WorldName = "Ship Klark (beginning)-389";

    private static string FindProjectRootForCameraTests()
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
            "AlundraWorldProxyUpdateCharacterizationTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - the camera-premiere-frame reproduction needs the real "
            + "converter export of map 389 and cannot self-skip without one (docs/plan-camera-premiere-frame.md §4).");
    }

    /// <summary>Reflection seam for two <see cref="AlundraWorldProxy"/> fields this montage needs but
    /// that carry no test-only accessor: <c>_tileMapData</c> (plan §4 needs the REAL map-389 tilemap
    /// installed there, exactly as <c>InitializeWithWorld</c> itself does, so the camera's map-bounds
    /// clamp and the map-events/record-spawn loops below all see real data) and <c>_world</c> - without
    /// it, <c>AlundraCameraDirector.ResolveDebugCameraOnce(_world)</c> (called at the top of every
    /// <c>Update</c>) sees a null world and never resolves the camera entity this montage already added,
    /// which would make every later <c>UpdateCameraFollow</c> call an unconditional no-op
    /// (<c>if (_debugCamera == null) return;</c>) - not the defect this montage exists to reproduce.</summary>
    private static void SetTileMapData(AlundraWorldProxy proxy, TileMapData tileMapData)
    {
        var field = typeof(AlundraWorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(proxy, tileMapData);
    }

    private static void SetWorld(AlundraWorldProxy proxy, World world)
    {
        var field = typeof(AlundraWorldProxy).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(proxy, world);
    }

    /// <summary>Reflection seam for <c>_spawnedEntities</c> (private, no test-only accessor): the record
    /// spawn loop below needs to land its entities there directly, exactly like
    /// <c>InitializeWithWorld</c>'s own record-spawn loop and <c>AdoptPlayerPawn</c>'s own trailing
    /// <c>_spawnedEntities.Add(entity)</c> do.</summary>
    private static System.Collections.Generic.List<Entity> GetSpawnedEntities(AlundraWorldProxy proxy)
    {
        var field = typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.Generic.List<Entity>)field!.GetValue(proxy)!;
    }

    /// <summary>
    /// Builds the real map-389 montage the reproduction test needs (plan §4): headless <see cref="World"/>
    /// + camera entity, a real <see cref="AlundraWorldProxy"/> with the real map-389
    /// <see cref="TileMapData"/> installed as <c>_tileMapData</c>, the player seeded EXACTLY as
    /// <see cref="AlundraWorldProxy"/>'s own private <c>AdoptPlayerPawn</c> does (tile 33/59 -&gt; px
    /// 804/952 - <see cref="AlundraGameState.CameraTileX"/>/<see cref="AlundraGameState.CameraTileY"/>,
    /// <c>PlayerEntity</c> == <c>EntityFollowedByCamera</c> - <c>AdoptPlayerPawn</c> itself is not
    /// headless-reachable, it needs a live <see cref="AlundraPlayerController"/>, the same reason
    /// <c>IntroTraceHarnessTests</c>' own <c>HeadlessIntroSimulation</c> seeds its player by hand too), the
    /// 14 "Entities" records this map's own spawn-zone gate actually admits through the SAME production
    /// seam <c>InitializeWithWorld</c> itself uses (<see cref="AlundraEntitySpawnFactory.ShouldSpawnRecord"/>/
    /// <see cref="AlundraEntitySpawnFactory.CreateBareEntityFromRecord"/> - the bare-entity, not
    /// prefab-clone, path, since this headless montage has no <c>World.Game.AssetContentManager</c> to
    /// clone a prefab through), map events built through the real <see cref="AlundraWorldProxy.BuildMapEvents"/>,
    /// and the real bytecode interpreter (<see cref="AlundraEventProgramRunner"/>) wired over map 389's own
    /// real event-code document (the same file <see cref="MapEventProgramLoader"/> loads in production).
    ///
    /// The player entity is built through <c>Entity.Initialize()</c> off a bare
    /// <c>GameplayProxyClassName</c> (not a hand-built <see cref="AlundraEntityScriptProxy"/> wired to a
    /// separate <c>new Entity()</c>) so <c>Entity.GameplayProxy</c> - private-set, only ever assigned by
    /// <c>Entity.Initialize()</c>'s own <c>ElementFactory</c> resolution - actually round-trips back to
    /// the SAME proxy instance this method configures, exactly like the engine-spawned pawn
    /// <c>AdoptPlayerPawn</c> adopts in production. Without that, <c>Update</c>'s own
    /// <c>RefreshUpdateProxiesAndCollidables</c> (reads <c>entity.GameplayProxy is AlundraEntityScriptProxy</c>)
    /// would silently drop the player out of <c>_updateProxies</c>.
    /// </summary>
    private static (AlundraWorldProxy Proxy, Camera2dComponent Camera) BuildMap389Montage()
    {
        var projectRoot = FindProjectRootForCameraTests();

        var world = new World { Name = Map389WorldName };
        var camera = AddCameraEntity(world);

        var tileMapFile = Directory.GetFiles(
            Path.Combine(projectRoot, "Maps"), $"{Map389WorldName}.tileMap", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(tileMapFile);

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFile!)));

        var proxy = new AlundraWorldProxy();
        SetTileMapData(proxy, tileMapData);
        SetWorld(proxy, world);
        proxy.SpriteRecordCatalog = new SpriteRecordCatalog(projectRoot);

        var spawnedEntities = GetSpawnedEntities(proxy);

        // Player - see this method's own doc for why it goes through Entity.Initialize() rather than a
        // hand-built proxy/Entity pair, and AdoptPlayerPawn's own doc for the exact field set this mirrors
        // (New Game tile (33,59), tile-centre 16.16 fixed-point pose (804,952,0), Status=Normal).
        var playerEntity = new Entity { GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        playerEntity.Initialize();
        var playerProxy = (AlundraEntityScriptProxy)playerEntity.GameplayProxy!;
        playerProxy.IsPlayer = true;
        playerProxy.LogicContextEntity = playerEntity;
        playerProxy.ScriptHost = proxy;
        playerProxy.Status = EntityStatus.Normal;
        playerProxy.EntityRefId = -1;
        playerProxy.EventTrigger = ScriptHelper.ProgramUnknown;
        playerProxy.PosX = (AlundraGameState.CameraTileX * 24 + 12) << 16;
        playerProxy.PosY = (AlundraGameState.CameraTileY * 16 + 8) << 16;
        playerProxy.PosZ = 0;
        playerProxy.TileX = (playerProxy.PosX >> 16) / 24;
        playerProxy.TileY = (playerProxy.PosY >> 16) / 16;
        playerProxy.TileZ = 0;
        playerProxy.TargetAnimationId = AlundraGameState.ResetAnimationId;
        playerProxy.TargetDirection = AlundraGameState.ResetDirectionId;
        playerProxy.CurrentAnimationId = ~AlundraGameState.ResetAnimationId;
        playerProxy.CurrentDirection = ~AlundraGameState.ResetDirectionId;

        proxy.PlayerEntity = playerProxy;
        proxy.EntityFollowedByCamera = playerProxy;
        spawnedEntities.Add(playerEntity);

        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var mapEventsLayer = tileMapData.ObjectLayers.First(l => l.Name == "MapEvents");

        foreach (var record in entitiesLayer.Objects)
        {
            if (!AlundraEntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone: false, playerProxy.TileX, playerProxy.TileY, out _))
            {
                continue;
            }

            var entity = AlundraEntitySpawnFactory.CreateBareEntityFromRecord(record, proxy.SpriteRecordCatalog, tileMapData: tileMapData);
            if (entity.GameplayProxy is AlundraEntityScriptProxy spawnedProxy)
            {
                spawnedProxy.ScriptHost = proxy;
            }

            spawnedEntities.Add(entity);
        }

        // Sanity (plan §1/§4): map 389's own load-time spawn-zone gate admits 14 of its 19 "Entities"
        // records - the player occupies slot 0, so this montage's own spawn loop must have added exactly
        // 14 more.
        Assert.Equal(14, spawnedEntities.Count - 1);

        proxy.BuildMapEvents(mapEventsLayer);

        var document = MapEventProgramLoader.Load(projectRoot, Map389WorldName);
        Assert.NotNull(document);
        proxy.EventProgramRunner = new AlundraEventProgramRunner(document!, proxy.GameState, proxy);

        // Port of GameEngine.cs:1638-1664's own g_isCameraScrolling=1 at map load (InitializeWithWorld's
        // own InitializeWithWorld call site) - the next UpdateCameraFollow call snaps straight to that
        // frame's look-at instead of scrolling in.
        proxy._cameraDirector.ArmFirstFrameSnap();

        return (proxy, camera);
    }

    [Fact]
    public void FirstFrame_FreeTimeStep_CameraTargetIsFinalIntroLookAt_NotRawSpawnPose()
    {
        var (proxy, camera) = BuildMap389Montage();

        // One 60Hz-shaped rendered frame (1/60s), free time step - the exact repro shape plan §1 measured:
        // the accumulator only reaches 1/60s, below the 1/50s (0.02) fixed-tick threshold, so this frame
        // carries ZERO raw logic ticks. Pre-fix, that means neither the map-events loop nor the camera
        // snap run this frame, and camera.Target lands on the player's own raw spawn pose (804,-839), a
        // full 279px away from the correct (804,-560) the fix must produce (plan §1's own measured table).
        proxy.Update(1f / 60f);

        Assert.Equal(new Vector3(804f, -560f, 0f), camera.Target);
    }

    [Fact]
    public void ZeroTickFrame_ArmedSnap_DoesNotFireUntilATickBearingFrameArrives()
    {
        var world = BuildHeadlessWorld();
        var camera = AddCameraEntity(world);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // arms the first-frame snap (ArmFirstFrameSnap).
        SeedDebugCameraOffset(proxy, SeededOffset);

        var followed = BuildFollowedTarget(posXPixels: 100, posYPixels: 200);
        proxy.EntityFollowedByCamera = followed;

        // Frame 1 - exactly one 50Hz tick (0.02s): consumes the armed snap normally, exactly like item 1
        // above. Not the frame under test; just establishes a known baseline and closes frame 1 (clearing
        // any sticky tick floor for every frame after it).
        proxy.Update(0.02f);
        var baseline = camera.Target;
        Assert.Equal(new Vector3(105f, -177f, 0f), baseline);

        // Re-arm the snap (mirrors a later in-game trigger - a map load, an opcode 0x69 forced look-at -
        // arming it again well after frame 1), move the look-at target somewhere the baseline could never
        // reach by drifting, then drive a GENUINELY zero-tick frame (0f elapsed - frame 1 already closed,
        // so no sticky floor applies here).
        proxy._cameraDirector.ArmFirstFrameSnap();
        followed.PosX = 5000 << 16;

        proxy.Update(0f);

        // The bug this test guards against: an unconditional _cameraNeedsSnap consumption would snap
        // camera.Target straight to the new look-at even though zero ticks ran this frame. The fix gates
        // the snap on ticksThisFrame > 0 in AlundraCameraDirector.UpdateCameraFollow (plan §3.2) - so on a
        // zero-tick frame Target must stay exactly at baseline, and the snap must stay armed.
        Assert.Equal(baseline, camera.Target);

        // The very next TICK-BEARING frame must then actually snap (the armed flag was not silently
        // dropped, only deferred) - straight to the new look-at, not a single incremental step toward it.
        proxy.Update(0.02f);
        Assert.Equal(new Vector3(5005f, -177f, 0f), camera.Target);
    }

    [Fact]
    public void LogicTicksThisFrame_FirstFrameFloorIsStickyForEveryCallThatFrame_NotJustTheFirst()
    {
        var proxy = new AlundraWorldProxy();

        // Frame 1, driven at 60Hz (1/60s < 1/50s - carries ZERO raw ticks): the floor must apply to
        // EVERY call this frame, not just the first - in production the FIRST caller is an entity's own
        // Update (entities update before the world proxy), so a first-call-only floor would hand the
        // entity a tick and leave the world proxy's own later read (and the world's own map-events loop)
        // at zero, splitting the world/entity tick counts the shared clock exists to keep in lock-step
        // (plan §3.1).
        Assert.Equal(1, proxy.LogicTicksThisFrame(1f / 60f));
        Assert.Equal(1, proxy.LogicTicksThisFrame(1f / 60f));

        // Closes frame 1 (same call site as production - right next to _logicClock.CloseFrame()) and
        // clears the sticky floor exactly once. The elapsed time here does not matter to this assertion;
        // Update reads LogicTicksThisFrame itself (a memoized re-read of the same floored frame).
        proxy.Update(1f / 60f);

        // Frame 2 - 0f elapsed on top of frame 1's own leftover ~1/60s accumulator (still under 1/50s):
        // its RAW tick count is 0. If the floor were still active (never cleared, or cleared then
        // reapplied unconditionally), this would read back 1 instead - the exact mutation this assertion
        // is paired against (plan §4, mutation 3).
        Assert.Equal(0, proxy.LogicTicksThisFrame(0f));
    }
}
