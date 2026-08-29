#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
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
    }

    public void Dispose() => AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);

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
        var field = typeof(AlundraWorldProxy).GetField("_debugCameraOffset", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(proxy, offset);
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
}
