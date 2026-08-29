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
/// Camera instance wiring extracted out of <see cref="AlundraWorldProxy"/>
/// (docs/plan-update-caracterisation.md, slice S2), with NO behaviour change - see that plan's §3
/// "S2" for the extraction rules this class follows (the "règle de preuve étendue") and
/// AlundraWorldProxyUpdateCharacterizationTests for the oracle this move must keep satisfying with
/// zero assertions changed.
///
/// Owns the one-time debug-camera lookup (<see cref="ResolveDebugCameraOnce"/>), the scripted camera
/// follow (<see cref="UpdateCameraFollow"/>) and the debug stick pan (<see cref="UpdateDebugCameraPan"/>),
/// plus every field those three exclusively read or write. Built in <see cref="AlundraWorldProxy"/>'s own
/// FIELD INITIALIZER (never lazily from a <c>World</c>, never static, never handed a back-reference to
/// the proxy) - the plan's trap 9: <c>AlundraWorldProxy.Clone</c> returns a bare
/// <c>new AlundraWorldProxy()</c> and copies nothing, which is only safe while every collaborator is
/// constructed exactly that way.
///
/// <c>AlundraWorldProxy.SetForcedCameraLookAt</c> stays on the proxy - it is an
/// <c>IEntityWorldContext</c> member and cannot move - and DELEGATES to <see cref="SetForcedLookAt"/>
/// here. That is not the banned "facade": the ban targets forwarders created to dodge updating call
/// sites, not an object delegating one of its own members to a collaborator it owns, which is the
/// ordinary shape of composition.
///
/// The world's tilemap bounds and the followed-camera target are read by the caller at USE TIME and
/// passed in per frame rather than captured here (trap 2 in the plan's §1): <c>_tileMapData</c> is only
/// assigned in <c>InitializeWithWorld</c> AFTER two early returns, so an <c>Initialize</c>-time capture
/// on this class would hold null forever and silently drop the map-bounds clamp.
/// </summary>
internal sealed class AlundraCameraDirector
{
    /// <summary>DEBUG ONLY. Cached <see cref="Camera2dComponent"/> of the world's camera entity, resolved
    /// once on the first <see cref="ResolveDebugCameraOnce"/> call; stays null (and logs once) when the
    /// world has no such entity/component.</summary>
    private Camera2dComponent? _debugCamera;

    /// <summary>Read-only exposure of the resolved <see cref="_debugCamera"/> - "the resolved
    /// <c>Camera2dComponent</c>" is one of the four named substitutions the extended proof rule
    /// (docs/plan-update-caracterisation.md §3) allows a caller to read at use time. Consumed by
    /// <c>AlundraWorldProxy.UpdateAndDrawBackdrop</c> (a step 6-only member S2 does not move) so it keeps
    /// reusing the same camera the follow/pan just resolved this frame.</summary>
    internal Camera2dComponent? ResolvedCamera => _debugCamera;

    /// <summary>DEBUG ONLY. Guards the one-time <see cref="_debugCamera"/> lookup/warning.</summary>
    private bool _debugCameraLookupDone;

    /// <summary>
    /// DEBUG ONLY (see <see cref="UpdateDebugCameraPan"/>). The stick-accumulated pan offset applied on
    /// top of <see cref="_debugCameraBase"/> - <c>Z</c> is always 0 (matching
    /// <see cref="AlundraCameraMath.ComputeDebugCameraPanOffset"/>'s own contract). Reset to
    /// <see cref="Vector3.Zero"/> by an R3 (right-stick) click; never touched at all while
    /// <c>AlundraWorldProxy.DebugCameraPanEnabled</c> is false.
    /// </summary>
    private Vector3 _debugCameraOffset;

    /// <summary>
    /// DEBUG ONLY (see <see cref="UpdateDebugCameraPan"/>). This debug tool's notion of the camera's
    /// "real" target - whatever the camera's own behavior (the scripted follow, or nothing yet) last put
    /// in <see cref="Camera2dComponent.Target"/>, with the stick's own <see cref="_debugCameraOffset"/>
    /// subtracted back out. Adopted fresh from <see cref="Camera2dComponent.Target"/> whenever that no
    /// longer matches <see cref="_debugCameraLastWrittenTarget"/> - see
    /// <see cref="AlundraCameraMath.ResolveDebugCameraBase"/>.
    /// </summary>
    private Vector3 _debugCameraBase;

    /// <summary>
    /// DEBUG ONLY. True once <see cref="_debugCameraBase"/> has been seeded from the camera's actual
    /// <see cref="Camera2dComponent.Target"/> on the first tick the camera was found - before that, there
    /// is no prior write to compare <see cref="Camera2dComponent.Target"/> against, so
    /// <see cref="AlundraCameraMath.ResolveDebugCameraBase"/> would otherwise wrongly treat the camera's
    /// initial target as "unchanged from a stale zero base".
    /// </summary>
    private bool _debugCameraBaseInitialized;

    /// <summary>
    /// DEBUG ONLY. The exact <see cref="Camera2dComponent.Target"/> value this class itself wrote last
    /// frame (<c>base + offset</c>) - compared against the camera's current <c>Target</c> each frame to
    /// detect an external write (see <see cref="_debugCameraBase"/>'s own doc).
    /// </summary>
    private Vector3 _debugCameraLastWrittenTarget;

    /// <summary>E5.a: port of <c>g_cameraLookAtX/Y/Z</c> - plain pixel ints (not 16.16 fixed-point),
    /// updated every frame from the followed camera target (see <see cref="UpdateCameraFollow"/>) while
    /// it is non-null and loaded/normal/deactivated; left untouched otherwise - a destroyed target
    /// FREEZES the camera on its last look-at, never falls back to the player (faithful: the original
    /// never auto-clears <c>g_entityFollowedByCamera</c> either). Also written directly by
    /// <see cref="SetForcedLookAt"/> (opcode 0x69, <c>IEntityWorldContext.SetForcedCameraLookAt</c>).</summary>
    private int _cameraLookAtX;
    private int _cameraLookAtY;
    private int _cameraLookAtZ;

    /// <summary>E5.a (decision E5-2): port of <c>g_isCameraScrolling = 1</c> at map load
    /// (<c>GraphicManager.cs</c>) - true makes the NEXT <see cref="UpdateCameraFollow"/> call snap the
    /// smoothed target straight to that frame's look-at instead of catching up to it, so the camera never
    /// scrolls in from wherever the engine's own default <c>Target</c> happened to be when a new map
    /// loads. Set once by <c>AlundraWorldProxy.InitializeWithWorld</c> (requalified field access, S2's
    /// own extended proof rule delta (b)), cleared by the first <see cref="UpdateCameraFollow"/> call
    /// that finds the camera. Internal (rather than private) purely so
    /// <c>AlundraWorldProxy.InitializeWithWorld</c> can requalify that one write to its new owner.</summary>
    private bool _cameraNeedsSnap;

    /// <summary>E5.a (decision E5-2): the smoothed render-space camera target - this class' own float
    /// catch-up state, written every frame by <see cref="UpdateCameraFollow"/> (snap-or-lerp), then
    /// clamped and pushed onto <see cref="_debugCamera"/>'s <c>Target</c> as the BASE
    /// <see cref="UpdateDebugCameraPan"/> then adds its own stick offset on top of (see that method's own
    /// doc on adopting an external write as the new base).</summary>
    private Vector3 _cameraSmoothedTarget;

    /// <summary>E5.a: <c>IEntityWorldContext.SetForcedCameraLookAt</c> (opcode 0x69, Script_105_069) -
    /// the half of that call that is this collaborator's own state. <see cref="AlundraWorldProxy"/> still
    /// owns the interface member itself (it also clears <c>EntityFollowedByCamera</c>, which stays on the
    /// proxy) and delegates here - see this class' own doc on why that delegation is not a facade.</summary>
    internal void SetForcedLookAt(int x, int y, int z)
    {
        _cameraLookAtX = x;
        _cameraLookAtY = y;
        _cameraLookAtZ = z;
    }

    /// <summary>
    /// One-time <see cref="_debugCamera"/> lookup (by component, not by reference name - see this
    /// method's own doc below for why), shared by <see cref="UpdateCameraFollow"/> and
    /// <see cref="UpdateDebugCameraPan"/> so whichever runs first this frame resolves it. E5-1
    /// (docs/plan-e5-camera.md): also poses the ORIGINAL's own framing on the camera right here, the
    /// moment it is found - runtime-only (no asset touched, no full export needed):
    /// <c>Zoom = real viewport height / 236</c> (see <see cref="AlundraCameraMath.ComputeCameraZoom"/> -
    /// computed from the LIVE viewport, never hardcoded, since <c>CameraComponent.InitializeWithWorld</c>
    /// already overwrites whatever Zoom/viewport an asset serialized) and <c>PixelSnap = true</c>.
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (S2's extended proof rule
    /// delta (a)) - both call sites below pass <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which
    /// this class never stores.
    /// </summary>
    internal void ResolveDebugCameraOnce(World? world)
    {
        if (_debugCameraLookupDone)
        {
            return;
        }

        _debugCameraLookupDone = true;

        // Looked up by COMPONENT, not by the reference name "camera": EntityReference.Load's
        // shared-asset branch clones the asset without applying the reference's name, so the live
        // entity is named after the asset ("AlundraCamera"). Taking the first Camera2dComponent in
        // the world mirrors DefaultRuntimeViewBootstrapper's own camera pick, so the pan/follow always
        // drives the camera the runtime view actually uses.
        if (world != null)
        {
            foreach (var entity in world.Entities)
            {
                _debugCamera = entity.GetComponent<Camera2dComponent>();
                if (_debugCamera != null)
                {
                    break;
                }
            }
        }

        if (_debugCamera == null)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: no Camera2dComponent found in world "
                + $"'{world?.Name}'; debug camera pan/follow disabled.");
            return;
        }

        _debugCamera.Zoom = AlundraCameraMath.ComputeCameraZoom(_debugCamera.Viewport.Height);
        _debugCamera.PixelSnap = true;
    }

    /// <summary>
    /// E5.a (docs/plan-e5-camera.md §2): scripted camera follow, faithful port of
    /// <c>GameEngine.UpdateEntities</c>'s own look-at update (<c>GameEngine.cs:1747-1752</c>) plus
    /// <c>GraphicManager</c>'s own scroll smoothing/clamp (<c>GraphicManager.cs:75-122</c>). Runs BEFORE
    /// <see cref="UpdateDebugCameraPan"/> (see <c>AlundraWorldProxy.Update</c>'s own ordering comment):
    /// this method writes the camera's <c>Target</c> as the new BASE, and
    /// <see cref="UpdateDebugCameraPan"/>'s own <see cref="AlundraCameraMath.ResolveDebugCameraBase"/>
    /// call adopts that write as the base a moment later in the SAME frame, then adds the stick offset
    /// back on top - so the scripted follow and the debug pan never fight.
    ///
    /// No-op before <see cref="_debugCamera"/> is resolved (see <see cref="ResolveDebugCameraOnce"/>,
    /// already called this frame by <c>AlundraWorldProxy.Update</c>).
    ///
    /// <paramref name="followedByCamera"/>, <paramref name="mapWidthPx"/> and <paramref name="mapHeightPx"/>
    /// are all read by the caller at USE TIME and passed in per frame (S2's extended proof rule delta
    /// (a)) rather than captured here - the map bounds in particular MUST stay per-frame: they derive
    /// from <c>AlundraWorldProxy</c>'s own <c>_tileMapData</c>, which is only assigned in
    /// <c>InitializeWithWorld</c> AFTER two early returns, so capturing it any earlier would hold null
    /// forever and silently drop the map-bounds clamp below.
    /// </summary>
    internal void UpdateCameraFollow(int ticksThisFrame, AlundraEntityScriptProxy? followedByCamera, int? mapWidthPx, int? mapHeightPx)
    {
        if (_debugCamera == null)
        {
            return;
        }

        // Port of GameEngine.cs:1747-1752 via ResolveCameraLookAt (see that method's own doc): only
        // overwrites the look-at while the followed entity is non-null AND Loaded/Normal/Deactivated -
        // otherwise it freezes on the last value (never falls back to the player - the original never
        // auto-clears EntityFollowedByCamera either).
        var followed = followedByCamera;
        var hasValidTarget = followed != null && followed.IsLoadedNormalOrDeactivated;
        (_cameraLookAtX, _cameraLookAtY, _cameraLookAtZ) = AlundraCameraMath.ResolveCameraLookAt(
            hasValidTarget,
            followed?.PosX >> 16 ?? 0, followed?.PosY >> 16 ?? 0, followed?.PosZ >> 16 ?? 0,
            _cameraLookAtX, _cameraLookAtY, _cameraLookAtZ);

        var target = AlundraCameraMath.ComputeCameraLookAtRenderPosition(_cameraLookAtX, _cameraLookAtY, _cameraLookAtZ);

        // E5.c: one catch-up step per LOGIC TICK, none at all on a frame that carried no tick - that is
        // what keeps the camera locked to the sprites. The clamped value is what gets stored back into
        // _cameraSmoothedTarget, not just written out to Target, exactly like the original's own
        // g_cameraScrollingX/Y assignment (fresh verifier of cc1fc60).
        _cameraSmoothedTarget = AlundraCameraMath.AdvanceCameraSmoothing(
            _cameraSmoothedTarget, _cameraNeedsSnap, target, ticksThisFrame,
            mapWidthPx, mapHeightPx);
        _cameraNeedsSnap = false;

        // The state is whole-numbered by construction (see AdvanceCameraSmoothing's own integer
        // invariant), so it IS the rendered value - written unconditionally, including on a zero-tick
        // frame, so UpdateDebugCameraPan's base-adoption still sees this class' own last write.
        _debugCamera.Target = _cameraSmoothedTarget;
    }

    /// <summary>
    /// DEBUG ONLY - temporary tool, to be gated/replaced once the real camera-follow (E4) lands. Pans the
    /// world's camera (first entity carrying a <see cref="Camera2dComponent"/>) with the gamepad's right
    /// thumbstick so the whole map can be flown over at runtime to inspect spawned entities.
    ///
    /// Reads the right stick through the engine's own <c>CasaEngineGame.InputComponent.GamePadManager</c>
    /// rather than MonoGame's <c>GamePad.GetState</c> directly, since that manager is already reachable
    /// off <paramref name="world"/>'s own <c>Game</c> and is what every other in-engine input read goes
    /// through (<c>InputMapping.Update</c>). A no-op whenever no gamepad is connected on player one, or no
    /// camera component can be found (warns once in the latter case).
    ///
    /// The stick no longer moves <c>Target</c> directly (user decision, 2026-08-24, ahead of E5's own
    /// script-driven follow camera): it only accumulates <see cref="_debugCameraOffset"/> (<c>Z</c> always
    /// 0), applied on top of <see cref="_debugCameraBase"/> - whatever the camera's own behavior (the
    /// scripted follow) last put in <c>Target</c>. Each frame this method first re-derives
    /// <see cref="_debugCameraBase"/> from <c>Target</c> (see <see cref="AlundraCameraMath.ResolveDebugCameraBase"/>)
    /// so an external write - the follow's own write - always wins as the base; the stick only ever adds
    /// a debug offset around it. An R3 (right-stick) click resets the offset to 0. While
    /// <c>AlundraWorldProxy.DebugCameraPanEnabled</c> is false, the stick never changes the offset and R3
    /// is inert - only the base write-through still runs, so the camera simply follows its base.
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (S2's extended proof rule
    /// delta (a)) - the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this
    /// class never stores.
    /// </summary>
    internal void UpdateDebugCameraPan(float elapsedTime, World? world)
    {
        ResolveDebugCameraOnce(world);

        if (_debugCamera == null)
        {
            return;
        }

        // Base resilience (E5-proof, see this method's own doc): whatever last wrote Target - including
        // nothing yet, on the very first tick the camera was found - becomes the base to pan around.
        _debugCameraBase = _debugCameraBaseInitialized
            ? AlundraCameraMath.ResolveDebugCameraBase(_debugCamera.Target, _debugCameraLastWrittenTarget, _debugCameraBase)
            : _debugCamera.Target;
        _debugCameraBaseInitialized = true;

        var gamePadManager = world?.Game?.InputComponent?.GamePadManager;
        if (gamePadManager != null)
        {
            var gamePad = gamePadManager.GetGamePad(PlayerIndex.One);
            if (gamePad.IsConnected)
            {
                // DEBUG ONLY - Back (Select) toggles the engine's physics wireframes, off by default at
                // world load (see AlundraWorldProxy.InitializeWithWorld), so collision boxes can be
                // inspected while flying the camera.
                if (gamePad.BackJustPressed)
                {
                    var physicsDebug = world?.Game?.PhysicsDebugViewRendererComponent;
                    if (physicsDebug != null)
                    {
                        physicsDebug.DisplayPhysics = !physicsDebug.DisplayPhysics;
                    }
                }

                if (AlundraWorldProxy.DebugCameraPanEnabled)
                {
                    _debugCameraOffset = gamePad.RightStickJustPressed
                        ? Vector3.Zero
                        : AlundraCameraMath.ComputeDebugCameraPanOffset(
                            _debugCameraOffset, gamePad.RightStickX, gamePad.RightStickY, elapsedTime);
                }
            }
        }

        _debugCamera.Target = _debugCameraBase + _debugCameraOffset;
        _debugCameraLastWrittenTarget = _debugCamera.Target;
    }

    /// <summary>
    /// Arms the first-frame snap, so the next <see cref="UpdateCameraFollow"/> jumps straight to the
    /// followed target instead of scrolling in. Called from <c>AlundraWorldProxy.InitializeWithWorld</c>.
    /// Exposed as an operation rather than as a writable field, for the reason given on
    /// <c>AlundraBackdropStage.Load</c>.
    /// </summary>
    internal void ArmFirstFrameSnap() => _cameraNeedsSnap = true;
}
