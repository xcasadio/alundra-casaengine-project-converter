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
/// and, since plan E9.b (S2), the engine-side scrolling-layers adaptation: <see cref="Load"/> (translates
/// this world's backdrop companion and pushes it to the attached <see cref="ScrollingLayerService"/>) and
/// <see cref="PushFrame"/> (pushes this frame's scroll/ticks to it) - the rendering mechanism itself now
/// lives in the engine (<c>CasaEngine.Framework.Rendering.ScrollingLayers</c>,
/// <c>docs/engine/scrolling-layers.md</c>); this class keeps only the Alundra-specific POLICY (D-E9b-8).
///
/// <c>AlundraWorldProxy.InitializeWithWorld</c> calls <see cref="AttachService"/> then <see cref="Load"/>
/// itself, in that order (D-E9b-2) - <see cref="Load"/> only parses this world's own backdrop companion
/// and pushes pure data to the engine service; texture resolution happens later, engine-side, in
/// <see cref="ScrollingLayerComponent.Update"/> (D-E9b-7), so this stage never touches a
/// <c>GraphicsDevice</c> at all any more.
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
    /// pushes frame state to via <see cref="PushFrame"/> and loads layer/tint definitions into via
    /// <see cref="Load"/>. Attached (never constructed here) by
    /// <c>AlundraWorldProxy.InitializeWithWorld</c>, same acquisition shape as
    /// <c>AlundraScreenFadeDirector.AttachToWorld</c>, BEFORE <see cref="Load"/> runs (D-E9b-2). A
    /// <see langword="null"/> service (headless montage without <see cref="AttachService"/>, or a
    /// production world whose <c>Game</c> has none) makes both <see cref="Load"/> and
    /// <see cref="PushFrame"/> no-ops - <see cref="Load"/> additionally warns once when the world's
    /// <c>Game</c> is NOT null (the production "arrêt" case: a live game with no scrolling-layers
    /// component is a bug, not a legitimate degraded mode).
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
    /// without this, every pixel no cell tile (or, now, no scrolling background layer) covers
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

    /// <summary>
    /// Loads this map's backdrop companion (if any) and pushes its translation to <see cref="_service"/>
    /// (D-E9b-9: <c>Clear()</c> then <c>SetConfiguration</c>/<c>SetLayers</c>/<c>SetTint</c> from
    /// <see cref="BuildDefinitions"/> - always in that order, once per <c>AlundraWorldProxy.InitializeWithWorld</c>
    /// call, so a world with no companion at all still clears out whatever the PREVIOUS world left
    /// behind). Called from <c>AlundraWorldProxy.InitializeWithWorld</c> AFTER <see cref="AttachService"/>,
    /// never from the frame path.
    ///
    /// A <see langword="null"/> <see cref="_service"/> is a no-op - EXCEPT that a production world (one
    /// whose <paramref name="world"/>.<c>Game</c> is not <see langword="null"/>) with no service attached
    /// is a bug (D-E9b-2's own "arrêt" clause: <c>world.Game.ScrollingLayerComponent</c> did not exist,
    /// or its <c>Service</c> was null) - logged ONCE per call as a warning ("lire le journal d'abord"),
    /// never thrown, so a degraded/no-op frame is still visibly diagnosable.
    /// </summary>
    internal void Load(World world, string projectPath)
    {
        if (_service == null)
        {
            if (world.Game != null)
            {
                Logs.WriteWarning(
                    $"AlundraBackdropStage: world '{world.Name}' has a live Game but no ScrollingLayerService "
                    + "was attached (AttachService); scrolling background layers will not render.");
            }

            return;
        }

        _service.Clear();

        var document = BackdropLoader.Load(projectPath, world.Name);
        if (document == null)
        {
            return;
        }

        var (layers, tint, configuration) = BuildDefinitions(document);
        _service.SetConfiguration(configuration);
        _service.SetLayers(layers);
        _service.SetTint(tint);
    }

    /// <summary>
    /// Plan E9.b (D-E9b-8) - PURE translation of a loaded <see cref="BackdropDocument"/> into the engine
    /// mechanism's own data (<c>CasaEngine.Framework.Rendering.ScrollingLayers</c>), applying EXACTLY the
    /// rules the now-retired <c>BackdropRenderer.Load</c> applied: a layer is translated only if
    /// <c>Mode == "Tiles" &amp;&amp; Scrollar != null &amp;&amp; TextureAssetId</c> is non-empty (the same
    /// two guards as that method's own early-`continue` - a <c>Disabled</c>/<c>Cellular</c> layer, a
    /// <c>Tiles</c> layer with a null <see cref="BackdropScrollarData"/>, or one with an empty
    /// <see cref="BackdropLayerData.TextureAssetId"/> is silently absent from the result, never an
    /// exception); <c>Ground</c>/<c>BlendMode</c> resolve to (<c>Pass</c>, <c>Blend</c>, <c>Tint</c>)
    /// through <see cref="ResolveGroundLayerBlend"/> (the definition this method now owns - see that
    /// method's own doc); <c>SortingLayer</c> is always 0, <c>OrderInLayer</c> is
    /// <see cref="BackdropLayerData.DepthOrder"/>, <c>StableId</c> is <see cref="BackdropLayerData.LayerId"/>;
    /// frame ids reuse <see cref="ResolveFrameAssetIds"/> (same
    /// <c>FrameTextureAssetIds ?? [TextureAssetId]</c> fallback already tested there) then convert each
    /// string to a <see cref="Guid"/> of the SAME LENGTH - a null, empty or unparsable id becomes
    /// <see cref="Guid.Empty"/>, NEVER an exception and NEVER a shortened array (D-E9-9's own fallback
    /// runs afterward, engine-side - see <see cref="ScrollingLayerComponent.ResolveTextures"/>). The
    /// overlay tint is independent of any layer (mirrors the old renderer's own "layers and/or tint"
    /// <c>HasContent</c> contract) - <c>(R, G, B, 128)</c> at <see cref="RenderPass2D.Effects"/>
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

            var frameAssetIds = ResolveFrameAssetIds(layer);
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

        // 640x480: the original's own wrapping canvas size (D-E9b-5) - was BackdropOffsetMath.CanvasWidth/
        // Height before that file's retirement (plan E9.b, S2); the engine's own ScrollingLayerService
        // carries no such constant since the canvas size is per-configuration, not fixed mechanism-side.
        // BackgroundDepth 1: a Background-pass layer recedes to cameraTarget.Z - 1 (plan
        // plan-e9c-defauts-321.md, D-E9c-5), reproducing the original's own policy of placing
        // Ground=false layers behind every floor, wall and entity (GraphicManager.cs:825-826).
        var configuration = new ScrollingLayerConfiguration(
            640, 480, (int)AlundraCameraMath.CameraVisibleWidth, (int)AlundraCameraMath.CameraVisibleHeight,
            backgroundDepth: 1f);

        return (layers.ToArray(), tintDefinition, configuration);
    }

    private static Guid ParseFrameAssetIdOrEmpty(string? assetIdString)
    {
        return Guid.TryParse(assetIdString, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// D-E9-9 (docs/plan-e9-backdrops-residus.md §2/§3 slice B3), moved here from the now-retired
    /// <c>BackdropRenderer</c> (plan E9.b, S2) - the layer's frames actually load from, pure and
    /// static so it is testable without a <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/>.
    /// A layer with no (or an empty) <see cref="BackdropLayerData.FrameTextureAssetIds"/> - every
    /// non-animated layer, and every companion written before this feature existed - resolves to
    /// exactly one id, <see cref="BackdropLayerData.TextureAssetId"/>; otherwise the array is returned
    /// as given (its <c>[0]</c> already equals <see cref="BackdropLayerData.TextureAssetId"/> by
    /// construction on the converter side).
    /// </summary>
    internal static string?[] ResolveFrameAssetIds(BackdropLayerData layer)
    {
        if (layer.FrameTextureAssetIds == null || layer.FrameTextureAssetIds.Length == 0)
        {
            return new[] { layer.TextureAssetId };
        }

        return layer.FrameTextureAssetIds;
    }

    /// <summary>
    /// E10.b (docs/plan-e10-fondu.md §1.8) - the ORIGINAL's own backdrop blend mapping
    /// (GraphicManager.cs:846-853): 1 = average, 2 = additive white, 3 = subtractive white, 4 = additive
    /// tint (63,63,63). Only <paramref name="ground"/> == <see langword="true"/> layers are re-mapped -
    /// the <c>(Ground = false, BlendMode 1)</c> bucket stays Opaque, untouched (out of scope - see the
    /// T8 tests for the full rationale). Moved here from the now-retired <c>BackdropRenderer</c> (plan
    /// E9.b, D-E9b-8: "la définition passe sur le stage") with its signature UNCHANGED, so the T8 pins
    /// stay green unedited.
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
    /// (a no-op while <see cref="_service"/> is <see langword="null"/>, see <see cref="_service"/>'s own
    /// doc): <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> on <paramref name="resolvedCamera"/>'s
    /// <c>Target</c> (or <see cref="Vector3.Zero"/> with no resolved camera), fed to
    /// <see cref="ScrollingLayerService.SetFrame"/> alongside <paramref name="ticksThisFrame"/> and the
    /// UNCONVERTED <c>Target</c> (still needed engine-side to place the covering quads in world space).
    /// Called from <c>AlundraWorldProxy.Update</c> at the exact site the retired
    /// <c>UpdateAndDrawBackdrop</c> used to occupy (D-E9b-2, S2): unconditional, outside the gameplay
    /// freeze gate, independent of <c>world.Game</c> or of whether this world has any layers at all.
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
