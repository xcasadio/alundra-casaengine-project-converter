#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Owns the pure camera mathematics formerly on <see cref="AlundraWorldProxy"/>: the scripted-follow
/// scroll/smoothing/clamp/zoom math (<see cref="StepCameraScroll"/>, <see cref="AdvanceCameraSmoothing"/>,
/// <see cref="ComputeSmoothedCameraTarget"/>, <see cref="ResolveCameraLookAt"/>,
/// <see cref="ComputeCameraLookAtRenderPosition"/>, <see cref="ClampCameraTargetToMap"/>,
/// <see cref="ComputeCameraZoom"/>) and the debug-pan math (<see cref="ComputeDebugCameraPanOffset"/>,
/// <see cref="ResolveDebugCameraBase"/>), plus the constants only these methods read. Pure `static`,
/// stateless, moved from <see cref="AlundraWorldProxy"/> by slice R2 of
/// docs/plan-decoupage-proxies.md - a behaviour-preserving relocation only, see that plan's §3 for
/// the exact delta rule (call qualification only; no widening was needed here since every constant
/// moved with its only remaining callers) this move used. Broken `<see cref>` references left by this
/// move are fixed in slice R5, not here (plan §4 R5) - this class's XML documentation is otherwise the
/// ORIGINAL text, unmodified. The instance wiring around this math (<c>UpdateCameraFollow</c>,
/// <c>UpdateDebugCameraPan</c>, <c>ResolveDebugCameraOnce</c>, <c>SetForcedCameraLookAt</c> and every
/// camera field) stays on <see cref="AlundraWorldProxy"/> - plan decision R-1, explicitly out of scope.
/// </summary>
internal static class AlundraCameraMath
{
    /// <summary>
    /// E5-1 (docs/plan-e5-camera.md, decision E5-1): the original's own visible-area size in logical
    /// pixels, derived (not guessed) from <c>GraphicManager.cs</c>'s own scroll/clamp constants - see
    /// <see cref="ClampCameraTargetToMap"/>'s own doc for the arithmetic that pins this to 320x240 rather
    /// than the unrelated 320x236 "native screen" constant (<c>AlundraDisplay.NativeHeight</c>/
    /// <c>StaticVariables.ScreenHeight</c> in the decompilation) - a DIFFERENT, framebuffer-crop constant
    /// this camera's own scroll math never reads.
    /// </summary>
    private const float CameraVisibleWidth = 320f;
    private const float CameraVisibleHeight = 240f;

    /// <summary>
    /// FIX (fresh verifier of cc1fc60), investigated in <c>GraphicManager.cs</c>/<c>StaticVariables.cs</c>:
    /// the original itself uses TWO different heights and they are NOT the same value.
    /// <list type="bullet">
    /// <item><description><b>Display height (this constant) = 236.</b>
    /// <c>StaticVariables.cs:56</c>: <c>public const int ScreenHeight = 236; //224</c> - the trailing
    /// comment is an earlier (wrong) guess the analyst left in place, 236 is what actually ships. This is
    /// the height of the real framebuffer: <c>Renderer.cs:22</c> allocates the backbuffer bitmap as
    /// <c>ScreenWidth x ScreenHeight</c>, and every blit/copy routine in <c>GraphicManager.cs</c> that
    /// touches the actual displayed surface bounds itself by <c>StaticVariables.ScreenHeight</c> (loop/clip
    /// bounds at <c>GraphicManager.cs:243,284,2000,2030,2102,2191,2198,2223,2289</c>). It is also exactly
    /// the converter's own <c>AlundraDisplay.NativeHeight</c> (<c>alundra-casaengine-project-converter/
    /// AlundraDisplay.cs</c>), which is why the real 1280x944 window (944 = 236 x <c>PixelScale</c> 4) is
    /// the RIGHT window, not a converter bug - no STOP needed here.</description></item>
    /// <item><description><b>Clamp height (<see cref="CameraVisibleHeight"/>, 240) is a SEPARATE
    /// constant</b> the original's scroll code uses for its own arithmetic, never for the framebuffer:
    /// <c>GraphicManager.cs:117-121</c>'s clamp bound <c>0x2cf</c> (719) = mapHeightPx(960) - 240 - 1 (see
    /// <see cref="ClampCameraTargetToMap"/>'s own doc for that derivation), and the SAME 240 shows up again,
    /// completely independently, as <c>GraphicManager.cs:817</c>'s local <c>scrollScreenHeight = 240</c>
    /// used only by the background-tile-layer scroll/wrap math (<c>GraphicManager.cs:942,1083,1086,1088,
    /// 1170,1173</c>) and the overlay height at <c>GraphicManager.cs:1256</c> - none of which ever reads
    /// <c>StaticVariables.ScreenHeight</c>. So the original itself clamps/scrolls as though 240 logical
    /// rows were visible while only drawing 236 of them to the actual screen (a 4px margin baked into its
    /// own scroll math, not a rendering bug this port needs to fix) - two genuinely different constants
    /// for two different purposes, both faithfully kept separate here: <see cref="CameraVisibleHeight"/>
    /// stays 240 for the CLAMP (<see cref="ClampCameraTargetToMap"/>, still verified against the original's
    /// own 0x39f/0x2cf), while <see cref="CameraDisplayHeight"/> (236) now drives the ZOOM
    /// (<see cref="ComputeCameraZoom"/>) so the rendered window is an exact integer multiple of the
    /// original's own display area - the 320x240 CLAMP window and the 320x236 DISPLAY window overlap
    /// almost entirely (236 of the clamp's 240 visible rows are actually drawn) and are never meant to be
    /// the same measurement.</description></item>
    /// </list>
    /// </summary>
    private const float CameraDisplayHeight = 236f;

    /// <summary>
    /// E5-1's centre-bias: the original's look-at sits at screen position (0xa0, 0x88) = (160, 136) from
    /// the view's top-left (<c>GraphicManager.cs:75-92</c>), and 136 is 16px more than half of 240 (120) -
    /// i.e. the followed point is NOT at the view's geometric vertical centre, it sits 16px below it (more
    /// map is visible above the point than below). Ported as a bias added to the render-space Y of the
    /// (already Y-flipped) look-at position - see <see cref="ComputeCameraLookAtRenderPosition"/>.
    /// Horizontal bias is 0: 0xa0 = 160 is exactly half of 320, so no X bias exists.
    /// </summary>
    private const float CameraCenterBiasY = 16f;

    /// <summary>
    /// DEBUG ONLY - temporary right-stick camera pan so the map can be flown over at runtime to inspect
    /// spawned entities, until the real camera-follow (E4) replaces it. Speed in world pixels/second,
    /// picked so a full stick deflection crosses the widest converted map (52 tiles * 24px = 1248px) in
    /// roughly 2.5 seconds.
    /// </summary>
    private const float DebugCameraPanSpeedPixelsPerSecond = 500f;

    /// <summary>DEBUG ONLY - see <see cref="DebugCameraPanSpeedPixelsPerSecond"/>. Per-axis stick deadzone.</summary>
    private const float DebugCameraPanDeadZone = 0.2f;

    /// <summary>
    /// E5.c - ONE catch-up step of the original's own scroll smoothing
    /// (<c>GraphicManager.cs:75-92</c>: <c>scroll += (target - scroll) &gt;&gt; 4</c>), ported verbatim as
    /// INTEGER arithmetic and expressed in RENDER space. Pure, so the whole rule is unit-testable.
    ///
    /// <para><b>Axis convention - the subtle part.</b> <c>&gt;&gt; 4</c> is an arithmetic shift, i.e. a
    /// FLOOR, so it is not symmetric: <c>(-d) &gt;&gt; 4 != -(d &gt;&gt; 4)</c> (e.g. <c>1 &gt;&gt; 4 == 0</c>
    /// but <c>-1 &gt;&gt; 4 == -1</c>). Our render space flips Y relative to the original's scroll space,
    /// so the shift must be converted, not copied. From the constants E5.a already froze:
    /// <list type="bullet">
    /// <item><description>X: <c>scrollX = lookAtX - 0xa0</c> and <c>renderX = lookAtX</c>, so
    /// <c>renderX = scrollX + 160</c> - SAME direction;</description></item>
    /// <item><description>Y: <c>scrollY = (Y - Z) - 0x88</c> and
    /// <c>renderY = -(Y - Z) + </c><see cref="CameraCenterBiasY"/> (16), so
    /// <c>renderY = -scrollY - 120</c> - OPPOSITE direction.</description></item>
    /// </list>
    /// Both relations are confirmed by <see cref="ClampCameraTargetToMap"/>'s own frozen map-389 bounds
    /// (scroll <c>[0, 0x39f]</c> -&gt; render <c>[160, 1087]</c>; scroll <c>[0, 0x2cf]</c> -&gt; render
    /// <c>[-120, -839]</c>). A render-space delta <c>d</c> is therefore <c>-d</c> in scroll space; the
    /// original's increment is <c>(-d) &gt;&gt; 4</c>; converting back flips the sign again, giving
    /// <c>-((-d) &gt;&gt; 4) == ceil(d / 16)</c>. Hence <b>floor on X, ceiling on Y</b> - the very same
    /// convention E5.b established for sprites (<c>SimulationSpacePolicy.SnapRenderPosition</c>).
    /// Verified against the original on every sign: d = -40/-16/-15/-7/-1/0/1/7/15/16/40 gives
    /// X = -3/-1/-1/-1/-1/0/0/0/0/1/2 and Y = -2/-1/0/0/0/0/1/1/1/1/3.</para>
    ///
    /// <para><b>Why integer and not a float lerp.</b> A continuous float state converges to a NON-integer
    /// lag, so quantizing only the rendered value makes it flip whenever the target - which advances in
    /// irregular 1-1-2 steps, being <c>PosY &gt;&gt; 16</c> - pushes it across a boundary: measured 480
    /// direction reversals per 1500 ticks at 1.22 px/tick, 1197 at 2.4 px/tick, i.e. the same 1-logical-px
    /// shimmer this fix exists to remove, merely slowed from 123Hz to 50Hz. The integer shift has a DEAD
    /// ZONE (increment 0 while |d| &lt; 16) that locks the state onto an integer: the ceiling absorbs the
    /// target's irregular step exactly (d = -31 -&gt; -1, d = -32 -&gt; -2), so the gap is a FIXED POINT,
    /// not a cycle - measured 0 reversals at every speed from 1.22 to 8 px/tick. The trade, accepted by
    /// the user on 2026-08-26 in place of decision E5-2's own deviation: the camera stops once the gap
    /// falls under 16px, so the followed entity can sit up to 15px off exact centre - which is precisely
    /// what the original does.</para>
    ///
    /// <para>Z carries no scroll in the original and is passed through untouched (it is always 0, see
    /// <see cref="ComputeCameraLookAtRenderPosition"/>).</para>
    /// </summary>
    internal static Vector3 StepCameraScroll(Vector3 current, Vector3 target)
    {
        // Both operands are whole numbers (see AdvanceCameraSmoothing's own integer invariant), so these
        // casts are exact.
        var deltaX = (int)(target.X - current.X);
        var deltaY = (int)(target.Y - current.Y);

        return new Vector3(
            current.X + (deltaX >> 4),      // floor - same direction as the original's scroll axis
            current.Y + -((-deltaY) >> 4),  // ceiling - render Y is the original's scroll Y, flipped
            current.Z);
    }

    /// <summary>
    /// E5.c - the whole per-frame camera catch-up, factored out of <see cref="AlundraCameraDirector.UpdateCameraFollow"/>
    /// (which needs a live World/Camera2dComponent and so cannot be driven headless - see this class's own
    /// scope note in <c>AlundraWorldProxyCameraFollowTests</c>) so that every rule it applies is unit-testable.
    ///
    /// Runs <paramref name="ticksThisFrame"/> steps of <see cref="StepCameraScroll"/>, clamping after EACH
    /// one (the original clamps every frame and the clamped value IS the scroll state - see
    /// <see cref="ComputeSmoothedCameraTarget"/>'s own doc). <paramref name="ticksThisFrame"/> = 0 leaves
    /// the state untouched: that is the entire point of this slice, since a followed entity's own position
    /// only changes on a logic tick, so between ticks the camera must not move either - otherwise the
    /// sprite slides across the screen on its own (see <see cref="AlundraCameraDirector.UpdateCameraFollow"/>'s own doc).
    ///
    /// <paramref name="needsSnap"/> (map entry, port of <c>g_isCameraScrolling = 1</c>) snaps straight to
    /// the clamped target BEFORE the loop. No tick is consumed for it: after the snap the state is a FIXED
    /// POINT of any further step this frame - the target is either already reached (delta 0, increment 0)
    /// or outside the bounds, in which case the step moves outward and the clamp pins it right back onto
    /// the same bound - so decrementing would be unobservable.
    ///
    /// <para><b>Integer invariant.</b> The state is always whole-numbered: the target is built from ints
    /// (<see cref="ComputeCameraLookAtRenderPosition"/>), <see cref="StepCameraScroll"/> adds an integer,
    /// and <see cref="ClampCameraTargetToMap"/>'s bounds are integers too (half of the whole 320/240
    /// visible size, offset by a whole map size in pixels). That is why the rendered value needs no
    /// separate pixel snap any more - the state IS the rendered value, exactly like the original's own
    /// <c>g_cameraScrollingX/Y</c>.</para>
    /// </summary>
    internal static Vector3 AdvanceCameraSmoothing(
        Vector3 previousSmoothedTarget, bool needsSnap, Vector3 lookAtRenderTarget,
        int ticksThisFrame, int? mapWidthPx, int? mapHeightPx)
    {
        var smoothed = previousSmoothedTarget;

        if (needsSnap)
        {
            smoothed = ComputeSmoothedCameraTarget(smoothed, true, lookAtRenderTarget, mapWidthPx, mapHeightPx);
        }

        for (var tick = 0; tick < ticksThisFrame; tick++)
        {
            smoothed = ComputeSmoothedCameraTarget(smoothed, false, lookAtRenderTarget, mapWidthPx, mapHeightPx);
        }

        return smoothed;
    }

    /// <summary>
    /// FIX (fresh verifier of cc1fc60) - pure state-transition factored out of <see cref="AlundraCameraDirector.UpdateCameraFollow"/>
    /// for unit testing (that method itself needs a live World/Camera2dComponent - see this class's own
    /// scope note in <c>AlundraWorldProxyCameraFollowTests</c>). Snaps to, or takes ONE
    /// <see cref="StepCameraScroll"/> step toward, <paramref name="lookAtRenderTarget"/>, THEN clamps
    /// - and returns the CLAMPED result as the new smoothing state (not just the render output), so the
    /// next call's <paramref name="previousSmoothedTarget"/> already reflects the clamp, exactly like the
    /// original writes its clamped scroll value back into <c>g_cameraScrollingX/Y</c>
    /// (<c>GraphicManager.cs:97-122</c>) rather than leaving it a read-only view over an unclamped state.
    /// <paramref name="mapWidthPx"/>/<paramref name="mapHeightPx"/> null (no tile map yet) skips the clamp,
    /// matching <see cref="AlundraCameraDirector.UpdateCameraFollow"/>'s own <c>_tileMapData == null</c> case.
    /// E5.c: one call is one 50Hz LOGIC TICK, no longer one rendered frame - see
    /// <see cref="AdvanceCameraSmoothing"/>, which is what production calls.
    /// </summary>
    internal static Vector3 ComputeSmoothedCameraTarget(
        Vector3 previousSmoothedTarget, bool needsSnap, Vector3 lookAtRenderTarget,
        int? mapWidthPx, int? mapHeightPx)
    {
        var smoothed = needsSnap
            ? lookAtRenderTarget
            : StepCameraScroll(previousSmoothedTarget, lookAtRenderTarget);

        return mapWidthPx.HasValue && mapHeightPx.HasValue
            ? ClampCameraTargetToMap(smoothed, mapWidthPx.Value, mapHeightPx.Value)
            : smoothed;
    }

    /// <summary>
    /// E5.a - pure decision factored out for unit testing: port of <c>GameEngine.cs:1747-1752</c>'s own
    /// look-at update gate. Returns <paramref name="candidateX"/>/<paramref name="candidateY"/>/
    /// <paramref name="candidateZ"/> (the followed entity's current position) when
    /// <paramref name="hasValidTarget"/> is true; otherwise returns
    /// <paramref name="previousX"/>/<paramref name="previousY"/>/<paramref name="previousZ"/> UNCHANGED -
    /// a destroyed (or null) followed entity freezes the look-at on its last value, faithful to the
    /// original which never auto-clears <c>g_entityFollowedByCamera</c> nor falls back to the player.
    /// </summary>
    internal static (int X, int Y, int Z) ResolveCameraLookAt(
        bool hasValidTarget, int candidateX, int candidateY, int candidateZ,
        int previousX, int previousY, int previousZ)
        => hasValidTarget ? (candidateX, candidateY, candidateZ) : (previousX, previousY, previousZ);

    /// <summary>
    /// E5.a - pure math factored out for unit testing: the ORIGINAL's own look-at-to-view-centre
    /// transform, in render space. <paramref name="lookAtX"/>/<paramref name="lookAtY"/>/
    /// <paramref name="lookAtZ"/> are plain pixel ints (<c>g_cameraLookAtX/Y/Z</c>'s own units - already
    /// shifted by 16, see <see cref="AlundraCameraDirector.UpdateCameraFollow"/>). Same Y-flip as
    /// <c>SimulationSpacePolicy.DeriveRenderPosition</c> (render Y = -(logical Y - Z)), PLUS the
    /// <see cref="CameraCenterBiasY"/> centre-bias (GraphicManager.cs's own scroll formula centres the
    /// view at look-at depth - 16, i.e. 16px ABOVE the look-at point in render space - see that
    /// constant's own doc for the full derivation). No X bias (0xa0 is exactly half of 320).
    /// </summary>
    internal static Vector3 ComputeCameraLookAtRenderPosition(int lookAtX, int lookAtY, int lookAtZ)
        => new(lookAtX, -(lookAtY - lookAtZ) + CameraCenterBiasY, 0f);

    /// <summary>
    /// E5.a (docs/plan-e5-camera.md §2, point 7) - pure math factored out for unit testing: clamps a
    /// render-space camera Target so the visible <see cref="CameraVisibleWidth"/>x<see cref="CameraVisibleHeight"/>
    /// area never leaves the map, bounds derived from <paramref name="mapWidthPx"/>/
    /// <paramref name="mapHeightPx"/> (<c>TileMapData.MapSize</c> x 24/16) exactly like the plan directs -
    /// NOT hardcoded from the original's own <c>0x39f</c>/<c>0x2cf</c> scroll-clamp constants
    /// (<c>GraphicManager.cs:97-122</c>), though those are what this formula was verified against: on
    /// map 389 (1248x960px), <c>ClampCameraTargetToMap</c> derives <c>TargetX in [160, 1087]</c> and
    /// <c>TargetY in [-839, -120]</c>, which is EXACTLY the original's own <c>[0, 0x39f]</c>/<c>[0,
    /// 0x2cf]</c> top-left scroll bounds once converted to render-space view-centre coordinates (X:
    /// scroll + 160 -&gt; [160, 927+160] = [160, 1087]; Y: -(scroll + 120) -&gt; [-(719+120), -120] =
    /// [-839, -120]) - confirming the original's own clamp is <c>mapSize - visibleSize - 1</c> (an
    /// inclusive-bound-by-one quirk faithfully reproduced here, not <c>mapSize - visibleSize</c>).</summary>
    internal static Vector3 ClampCameraTargetToMap(Vector3 target, int mapWidthPx, int mapHeightPx)
    {
        var halfWidth = CameraVisibleWidth / 2f;
        var halfHeight = CameraVisibleHeight / 2f;

        var minX = halfWidth;
        var maxX = mapWidthPx - halfWidth - 1f;
        var minY = -(mapHeightPx - halfHeight - 1f);
        var maxY = -halfHeight;

        // A map narrower/shorter than the visible area (never true for a real Alundra map, all 52x60)
        // would invert the bounds above - fall back to centring on the map instead of throwing.
        var x = minX <= maxX ? Math.Clamp(target.X, minX, maxX) : mapWidthPx / 2f;
        var y = minY <= maxY ? Math.Clamp(target.Y, minY, maxY) : -(mapHeightPx / 2f);

        return new Vector3(x, y, target.Z);
    }

    /// <summary>
    /// FIX (fresh verifier of cc1fc60) - pure math factored out for unit testing: the camera's
    /// <see cref="Camera2dComponent.Zoom"/> that reproduces the original's own
    /// <see cref="CameraDisplayHeight"/>-tall (236, NOT <see cref="CameraVisibleHeight"/>'s 240 - see that
    /// constant's own doc for the display-vs-clamp investigation) DISPLAY area for a real
    /// <paramref name="viewportHeight"/> (<c>Camera2dComponent.ComputeProjectionMatrix</c>'s own visible
    /// area is <c>viewport / Zoom</c>). Computed at runtime from the LIVE viewport rather than hardcoded,
    /// per the plan's own instruction - the DLL's own window height (<c>AlundraDisplay.WindowHeight</c> =
    /// 944 = 236 x <c>PixelScale</c> 4) now divides evenly by 236, so this yields the exact integer zoom 4
    /// at the real 1280x944 window, restoring pixel-perfect rendering (used to be 944/240 = 3.9333, texels
    /// stretched over 3-4 device pixels).
    /// </summary>
    internal static float ComputeCameraZoom(int viewportHeight) => viewportHeight / CameraDisplayHeight;

    /// <summary>
    /// DEBUG ONLY (see <see cref="AlundraCameraDirector.UpdateDebugCameraPan"/>) - the pure math factored out for unit testing:
    /// applies a per-axis deadzone to the raw stick values, then moves <paramref name="currentOffset"/> by
    /// stick * <see cref="DebugCameraPanSpeedPixelsPerSecond"/> * <paramref name="elapsedTime"/> on X/Y.
    /// <c>Z</c> is always 0, both in and out - the offset never carries a depth component.
    /// </summary>
    internal static Vector3 ComputeDebugCameraPanOffset(
        Vector3 currentOffset, float stickX, float stickY, float elapsedTime)
    {
        var x = MathF.Abs(stickX) < DebugCameraPanDeadZone ? 0f : stickX;
        var y = MathF.Abs(stickY) < DebugCameraPanDeadZone ? 0f : stickY;

        return new Vector3(
            currentOffset.X + x * DebugCameraPanSpeedPixelsPerSecond * elapsedTime,
            currentOffset.Y + y * DebugCameraPanSpeedPixelsPerSecond * elapsedTime,
            0f);
    }

    /// <summary>
    /// DEBUG ONLY (see <see cref="AlundraCameraDirector.UpdateDebugCameraPan"/>) - the pure math factored out for unit testing:
    /// resolves this frame's debug-camera base. Returns <paramref name="currentTarget"/> itself whenever
    /// it no longer matches <paramref name="lastWrittenTarget"/> (some other system - a future E5
    /// follow-target - wrote <c>Target</c> since this proxy last did, so that new value IS the base, with
    /// nothing to subtract back out); otherwise returns <paramref name="previousBase"/> unchanged (nothing
    /// external moved the camera this frame).
    /// </summary>
    internal static Vector3 ResolveDebugCameraBase(
        Vector3 currentTarget, Vector3 lastWrittenTarget, Vector3 previousBase)
        => currentTarget != lastWrittenTarget ? currentTarget : previousBase;
}
