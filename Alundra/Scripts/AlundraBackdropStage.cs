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
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Rendering instance wiring extracted out of <see cref="AlundraWorldProxy"/>
/// (docs/plan-update-caracterisation.md, slice S3), with NO behaviour change - see that plan's §3
/// "S3" for the extraction rules this class follows (the "règle de preuve étendue" defined ahead of
/// S2 and reused here) and AlundraWorldProxyUpdateCharacterizationTests for the oracle this move must
/// keep satisfying with zero assertions changed.
///
/// Owns the one-time background-clear-color lookup (<see cref="ApplyOriginalBackgroundClearColorOnce"/>)
/// and the per-frame backdrop tick/draw (<see cref="UpdateAndDrawBackdrop"/>), plus every field those two
/// exclusively read or write. Built in <see cref="AlundraWorldProxy"/>'s own FIELD INITIALIZER (never
/// lazily from a <c>World</c>, never static, never handed a back-reference to the proxy) - the plan's
/// trap 9: <c>AlundraWorldProxy.Clone</c> returns a bare <c>new AlundraWorldProxy()</c> and copies
/// nothing, which is only safe while every collaborator is constructed exactly that way.
///
/// <c>AlundraWorldProxy.InitializeWithWorld</c> still calls <see cref="BackdropRenderer.Load"/> itself,
/// requalified to <see cref="_backdropRenderer"/>'s new owner (extended proof rule delta (b), same shape
/// as S2's <c>_cameraNeedsSnap</c>) - that <c>Load</c> call needs a live <c>GraphicsDevice</c>, unlike the
/// two members this class owns outright, so it is not itself one of them.
///
/// The world and the resolved debug camera (owned by <see cref="AlundraCameraDirector"/>, S2) are read by
/// the caller at USE TIME and passed in per frame rather than captured here (extended proof rule
/// delta (a)): a stage that captured <c>World</c> at construction would predate
/// <c>AlundraWorldProxy.InitializeWithWorld</c> assigning it, and the resolved camera is this stage's own
/// collaborator's state, not its own - passing it in keeps this class from reaching into
/// <c>AlundraCameraDirector</c> itself.
/// </summary>
internal sealed class AlundraBackdropStage
{
    /// <summary>Renders this world's scrolling background layers (see <see cref="BackdropRenderer"/>'s
    /// class doc) - loaded once by <see cref="AlundraWorldProxy.InitializeWithWorld"/> (requalified field
    /// access, this class' own extended proof rule delta (b)), ticked and drawn every frame by
    /// <see cref="UpdateAndDrawBackdrop"/>. Internal (rather than private) purely so
    /// <c>AlundraWorldProxy.InitializeWithWorld</c> can call <see cref="BackdropRenderer.Load"/> on it
    /// directly, the same shape S2 used for <c>_cameraNeedsSnap</c>.</summary>
    private readonly BackdropRenderer _backdropRenderer = new();

    /// <summary>Cached once <see cref="ApplyOriginalBackgroundClearColorOnce"/> has set the world's
    /// runtime view <see cref="CasaEngine.Framework.Rendering.RenderView.ClearColor"/> - the view does not
    /// exist yet when <see cref="AlundraWorldProxy.InitializeWithWorld"/> runs (<c>GameManager.EndLoadContent</c>
    /// calls <c>World.LoadContent</c>, which drives the proxy, strictly before
    /// <c>IRuntimeViewBootstrapper.BootstrapViews</c>), so the lookup is retried lazily from
    /// <see cref="AlundraWorldProxy.Update"/>, mirroring <c>AlundraCameraDirector</c>'s own
    /// one-time-retry shape for the debug camera lookup.</summary>
    private bool _clearColorApplied;

    /// <summary>
    /// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, D-E9b-2) - the engine-side mechanism this stage
    /// pushes frame state to via <see cref="PushFrame"/>. Attached (never constructed here) by
    /// <c>AlundraWorldProxy.InitializeWithWorld</c>, same acquisition shape as
    /// <c>AlundraScreenFadeDirector.AttachToWorld</c>. S1 (this slice): nothing in production calls
    /// <see cref="AttachService"/> yet, so this is always <see langword="null"/> at the one call site
    /// that exists today - S2 wires it in and adds the null-with-live-Game warning (D-E9b-2's own
    /// "arrêt" clause), deliberately not implemented here.
    /// </summary>
    private ScrollingLayerService? _service;

    /// <summary>Attaches (or detaches, with <see langword="null"/>) this stage's engine-side scrolling-
    /// layers mechanism - see <see cref="_service"/>'s own doc. Internal so <c>Alundra.Tests</c> (via
    /// <c>InternalsVisibleTo</c>) can inject a real <see cref="ScrollingLayerService"/> on a headless
    /// montage, exactly the shape D-E9b-2 specifies for production.</summary>
    internal void AttachService(ScrollingLayerService? service)
    {
        _service = service;
    }

    /// <summary>Faithful port (E2, docs/plan-e2-rendu.md) of the original engine's own background clear
    /// (<c>AlundraGame.Draw</c>'s <c>GraphicsDevice.Clear(Color.Black)</c>, both for the game's off-screen
    /// render target and the final backbuffer blit -
    /// alundra-datas-analyser/AlundraTools/AlundraGame/AlundraGame.cs:199,236) instead of the engine's
    /// default <c>Color.CornflowerBlue</c> (<see cref="CasaEngine.Framework.Application.DefaultRuntimeViewBootstrapper"/>):
    /// without this, every pixel no cell tile (or, now, no <see cref="BackdropRenderer"/> layer) covers
    /// shows turquoise instead of the black the original always drew there. Retried lazily once per world
    /// from <see cref="AlundraWorldProxy.Update"/> (see <see cref="_clearColorApplied"/>'s own doc for why
    /// <see cref="AlundraWorldProxy.InitializeWithWorld"/> is too early to find the view).
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (extended proof rule delta (a)) -
    /// the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this class never
    /// stores.
    /// </summary>
    internal void ApplyOriginalBackgroundClearColorOnce(World? world)
    {
        if (_clearColorApplied || world?.Game == null)
        {
            return;
        }

        foreach (var view in world.Game.GameManager.ViewManager.Views)
        {
            if (view.World == world)
            {
                view.ClearColor = Color.Black;
                _clearColorApplied = true;
                break;
            }
        }
    }

    /// <summary>Ticks and draws this world's scrolling background layers - see
    /// <see cref="BackdropRenderer"/>'s class doc for the render pass/camera-space reasoning. A no-op
    /// when the world has no backdrop companion at all, or one with neither a Tiles-mode layer nor the
    /// overlay tint (the common case), or before the engine's <see cref="SpriteRendererComponent"/> is
    /// resolvable.
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (extended proof rule delta (a)) -
    /// the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this class never
    /// stores. <paramref name="resolvedCamera"/> is passed in rather than re-resolved (extended proof
    /// rule delta (a), the one named for S3) - it reuses the same <c>Camera2dComponent</c> the debug pan
    /// drives (see <c>AlundraCameraDirector.UpdateDebugCameraPan</c>, which already ran earlier this frame
    /// and resolved it) - both are "the world's camera", and the runtime has no other camera reference yet
    /// (E4 follow-up).
    ///
    /// D-E9-1 (docs/plan-e9-backdrops-residus.md §2, §3 slice B1): before drawing, the resolved
    /// camera's render-space <c>Target</c> is converted through
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> - the single place in the codebase that
    /// performs this conversion - into the original's own <c>g_cameraScrollingX/Y</c> scroll space, fed
    /// to <see cref="BackdropRenderer.Draw"/> alongside the unconverted render-space camera (still
    /// needed there to place the quads in world space).
    ///
    /// D-E9-5 (docs/plan-e9-backdrops-residus.md §2, §3 slice B3): <see cref="BackdropRenderer.AdvanceAnimation"/>
    /// is the FIRST instruction of this method, BEFORE both early-return guards below - it depends on
    /// neither <paramref name="world"/>'s <c>Game</c> nor the engine's <see cref="SpriteRendererComponent"/>,
    /// which is exactly what makes the V-animation cadence observable at this production call site
    /// through a montage where both guards would otherwise short-circuit it (see
    /// <c>AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World</c>'s own note on why - §1.4).
    /// <paramref name="ticksThisFrame"/> is <c>AlundraWorldProxy.LogicTicksThisFrame</c>'s result, passed
    /// in unchanged, same reasoning as every other per-tick advance on that call site (camera follow,
    /// fade, warp, dialogue).</summary>
    internal void UpdateAndDrawBackdrop(float elapsedTime, int ticksThisFrame, World? world, Camera2dComponent? resolvedCamera)
    {
        _backdropRenderer.AdvanceAnimation(ticksThisFrame);

        if (!_backdropRenderer.HasContent || world?.Game == null)
        {
            return;
        }

        _backdropRenderer.Tick(elapsedTime);

        var spriteRenderer = world.Game.GetGameComponent<SpriteRendererComponent>();
        if (spriteRenderer == null)
        {
            return;
        }

        var cameraPosition = resolvedCamera?.Target ?? Vector3.Zero;
        var scroll = AlundraCameraMath.ToOriginalScrollSpace(cameraPosition);

        // E9.a B5 (docs/plan-e9-backdrops-residus.md §5, map 159): the view size handed to Draw is the
        // ORIGINAL's 320x240 framebuffer in world units, not the window's pixel size. Draw uses it as
        // world half-extents to anchor the canvas' top-left on the screen's top-left (E5: the camera
        // Target is the framebuffer centre, so top-left = Target + (-160, +120)) and to tile the covering
        // quads. With the window's 1280x944 pixels (zoom 4 maps them onto this same 320x236 view) the
        // half-height became 472 world units: four canvas copies, and the one shifted by 480 put its
        // row 0 at camera.Y - 8, i.e. the band of map 159 started at screen row 126 instead of 0 - an
        // 8/640-pixel shift that the periodic clouds of map 389 could never show.
        _backdropRenderer.Draw(
            spriteRenderer, scroll.X, scroll.Y, cameraPosition,
            (int)AlundraCameraMath.CameraVisibleWidth, (int)AlundraCameraMath.CameraVisibleHeight);
    }

    /// <summary>
    /// Loads this map's backdrop document and textures. Called from
    /// <c>AlundraWorldProxy.InitializeWithWorld</c>, never from the frame path: it needs a live
    /// GraphicsDevice, unlike <see cref="UpdateAndDrawBackdrop"/> which only queues sprites.
    /// Exposed as an operation rather than by handing out <c>_backdropRenderer</c>: a responsibility
    /// callers reach through is not an extracted one (docs/plan-update-caracterisation.md, slice S3).
    /// </summary>
    internal void Load(World world, string projectPath) => _backdropRenderer.Load(world, projectPath);

    /// <summary>
    /// Plan E9.b (D-E9b-8) - PURE translation of a loaded <see cref="BackdropDocument"/> into the engine
    /// mechanism's own data (<c>CasaEngine.Framework.Rendering.ScrollingLayers</c>), applying EXACTLY the
    /// rules <see cref="BackdropRenderer.Load"/> applies today: a layer is translated only if
    /// <c>Mode == "Tiles" &amp;&amp; Scrollar != null &amp;&amp; TextureAssetId</c> is non-empty (the same
    /// two guards as <see cref="BackdropRenderer.Load"/>'s own early-`continue` - a <c>Disabled</c>/
    /// <c>Cellular</c> layer, a <c>Tiles</c> layer with a null <see cref="BackdropScrollarData"/>, or one
    /// with an empty <see cref="BackdropLayerData.TextureAssetId"/> is silently absent from the result,
    /// never an exception); <c>Ground</c>/<c>BlendMode</c> resolve to (<c>Pass</c>, <c>Blend</c>,
    /// <c>Tint</c>) through <see cref="ResolveGroundLayerBlend"/> (the definition this method now owns -
    /// see that method's own doc); <c>SortingLayer</c> is always 0, <c>OrderInLayer</c> is
    /// <see cref="BackdropLayerData.DepthOrder"/>, <c>StableId</c> is <see cref="BackdropLayerData.LayerId"/>;
    /// frame ids reuse <see cref="BackdropRenderer.ResolveFrameAssetIds"/> (same
    /// <c>FrameTextureAssetIds ?? [TextureAssetId]</c> fallback already tested there) then convert each
    /// string to a <see cref="Guid"/> of the SAME LENGTH - a null, empty or unparsable id becomes
    /// <see cref="Guid.Empty"/>, NEVER an exception and NEVER a shortened array (D-E9-9's own fallback
    /// runs afterward, engine-side - see <see cref="ScrollingLayerComponent.ResolveTextures"/>). The
    /// overlay tint is independent of any layer (mirrors <see cref="BackdropRenderer.HasContent"/>'s own
    /// "layers and/or tint" contract) - <c>(R, G, B, 128)</c> at <see cref="RenderPass2D.Effects"/>
    /// sorting −1, strictly below every <c>Ground=1</c> layer's <c>SortingLayer</c> 0 key. The
    /// configuration is always the fixed 640x480 canvas / 320x240 view (D-E9b-5).
    /// </summary>
    internal static (ScrollingLayerDefinition[] Layers, ScrollingTintDefinition? Tint, ScrollingLayerConfiguration Configuration) BuildDefinitions(
        BackdropDocument document)
    {
        var layers = new List<ScrollingLayerDefinition>();

        foreach (var layer in document.Layers)
        {
            if (layer.Mode != "Tiles" || layer.Scrollar == null || string.IsNullOrEmpty(layer.TextureAssetId))
            {
                continue;
            }

            var scrollar = layer.Scrollar;
            var (blendMode, tint) = ResolveGroundLayerBlend(layer.Ground, layer.BlendMode);
            var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;

            var frameAssetIds = BackdropRenderer.ResolveFrameAssetIds(layer);
            var frameTextureAssetIds = new Guid[frameAssetIds.Length];
            for (var frameIndex = 0; frameIndex < frameAssetIds.Length; frameIndex++)
            {
                frameTextureAssetIds[frameIndex] = ParseFrameAssetIdOrEmpty(frameAssetIds[frameIndex]);
            }

            layers.Add(new ScrollingLayerDefinition
            {
                FrameTextureAssetIds = frameTextureAssetIds,
                FactorXNum = scrollar.FactorXNum,
                FactorXDenom = scrollar.FactorXDenom,
                FactorYNum = scrollar.FactorYNum,
                FactorYDenom = scrollar.FactorYDenom,
                ScrollXSpeed = scrollar.ScrollXSpeed,
                ScrollXPeriod = scrollar.ScrollXPeriod,
                ScrollYSpeed = scrollar.ScrollYSpeed,
                ScrollYPeriod = scrollar.ScrollYPeriod,
                AnimTimer = layer.AnimTimer,
                Pass = renderPass,
                SortingLayer = 0,
                OrderInLayer = layer.DepthOrder,
                StableId = layer.LayerId,
                Blend = blendMode,
                Tint = tint,
            });
        }

        ScrollingTintDefinition? tintDefinition = null;
        if (document.OverlayEnabled)
        {
            var tintColor = new Color(document.OverlayColorR, document.OverlayColorG, document.OverlayColorB, (byte)128);
            var tintSortKey = new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);
            tintDefinition = new ScrollingTintDefinition(tintColor, tintSortKey);
        }

        var configuration = new ScrollingLayerConfiguration(
            BackdropOffsetMath.CanvasWidth, BackdropOffsetMath.CanvasHeight,
            (int)AlundraCameraMath.CameraVisibleWidth, (int)AlundraCameraMath.CameraVisibleHeight);

        return (layers.ToArray(), tintDefinition, configuration);
    }

    private static Guid ParseFrameAssetIdOrEmpty(string? assetIdString)
    {
        return Guid.TryParse(assetIdString, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// E10.b (docs/plan-e10-fondu.md §1.8) - the ORIGINAL's own backdrop blend mapping
    /// (GraphicManager.cs:846-853): 1 = average, 2 = additive white, 3 = subtractive white, 4 = additive
    /// tint (63,63,63). Only <paramref name="ground"/> == <see langword="true"/> layers are re-mapped -
    /// the <c>(Ground = false, BlendMode 1)</c> bucket stays Opaque, untouched (out of scope - see
    /// <see cref="BackdropRenderer.ResolveGroundLayerBlend"/>'s own doc, kept as the forwarding pin for
    /// this method, for the full rationale). Moved here from <see cref="BackdropRenderer"/> (plan E9.b,
    /// D-E9b-8: "la définition passe sur le stage") - <see cref="BackdropRenderer.ResolveGroundLayerBlend"/>
    /// now forwards to this method with its own signature UNCHANGED, so the T8 pins stay green
    /// unedited.
    /// </summary>
    internal static (SpriteBlendMode BlendMode, Color Tint) ResolveGroundLayerBlend(bool ground, int blendMode)
    {
        if (ground)
        {
            switch (blendMode)
            {
                case 1: // Average - true semi-transparency via AlphaBlend.
                    return (SpriteBlendMode.AlphaBlend, new Color(255, 255, 255, 128));
                case 2: // Additive white.
                    return (SpriteBlendMode.Additive, Color.White);
                case 3: // Subtractive white.
                    return (SpriteBlendMode.Subtractive, Color.White);
                case 4: // Additive, tint (63,63,63).
                    return (SpriteBlendMode.Additive, new Color(63, 63, 63));
            }
        }

        // Every other combination - including the deliberately untouched (Ground=false, BlendMode 1)
        // bucket - keeps the pre-existing fixed behavior.
        return (SpriteBlendMode.Opaque, Color.White);
    }

    /// <summary>
    /// Plan E9.b (D-E9b-2) - pushes this frame's original-scroll-space state to <see cref="_service"/>
    /// (a no-op while <see cref="_service"/> is <see langword="null"/> - S1 never attaches one in
    /// production, see <see cref="_service"/>'s own doc). Mirrors
    /// <see cref="UpdateAndDrawBackdrop"/>'s own scroll conversion exactly:
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> on <paramref name="resolvedCamera"/>'s
    /// <c>Target</c> (or <see cref="Vector3.Zero"/> with no resolved camera, same fallback as
    /// <see cref="UpdateAndDrawBackdrop"/>). NOT called by any production site yet (S1) - <see cref="Load"/>
    /// and <see cref="UpdateAndDrawBackdrop"/> stay the only two members <c>AlundraWorldProxy</c> calls.
    /// </summary>
    internal void PushFrame(int ticksThisFrame, Camera2dComponent? resolvedCamera)
    {
        if (_service == null)
        {
            return;
        }

        var cameraTarget = resolvedCamera?.Target ?? Vector3.Zero;
        var scroll = AlundraCameraMath.ToOriginalScrollSpace(cameraTarget);
        _service.SetFrame(scroll.X, scroll.Y, ticksThisFrame, cameraTarget);
    }
}
