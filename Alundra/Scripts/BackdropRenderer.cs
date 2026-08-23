#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace Alundra.Scripts;

/// <summary>
/// Renders one world's scrolling background layers (<see cref="BackdropDocument"/> - see
/// <c>docs/formats/backdrops.md</c> for the source format this consumes and
/// <see cref="BackdropOffsetMath"/> for the per-frame offset formulas). Owned by
/// <see cref="AlundraWorldProxy"/>: <see cref="Load"/> runs once from
/// <see cref="AlundraWorldProxy.InitializeWithWorld"/>, <see cref="Tick"/> and <see cref="Draw"/> run
/// every frame from <see cref="AlundraWorldProxy.Update"/> (the world proxy's own <c>Draw()</c> is
/// never called by the engine - <see cref="Scene.World.World"/> only forwards
/// <c>GameplayProxy.Update</c>, not <c>Draw</c>, for the world-level proxy - so submitting draw calls
/// from <c>Update</c>, like the tile map's own sorted overlay, is the only place that actually runs).
///
/// Only <c>Mode == "Tiles"</c> layers with a texture are rendered; <c>Mode == "Cellular"</c> and
/// <c>"Disabled"</c> layers are skipped (see <see cref="BackdropDocument"/>'s class doc - Cellular
/// rendering is a later chantier). The full-screen overlay tint (<see
/// cref="BackdropDocument.OverlayEnabled"/>) is loaded and drawn independently of any Tiles layer -
/// see <see cref="HasContent"/>. A missing/absent companion file degrades to "nothing to draw",
/// logged once by <see cref="BackdropLoader"/>.
///
/// Render pass mapping (GraphicManager.RenderAllTileLayers/RenderLayerToBuffer @ 0x8005B670/
/// 0x8005B848 - see <see cref="BackdropLayerData"/>'s and <see cref="BackdropDocument"/>'s class
/// docs): <c>Ground == false</c> layers use <see cref="RenderPass2D.Background"/> (far below every
/// floor/wall/entity, matching the original's <c>-0x10000000 + order</c> depth); <c>Ground == true</c>
/// layers use <see cref="RenderPass2D.Effects"/> (above the whole Y-sorted world/foreground, below
/// UI, matching the original's near-<c>int.MaxValue</c> depth). Within a bucket, <see
/// cref="BackdropLayerData.DepthOrder"/> (1 for layer 0, 0 for layer 1) breaks ties so layer 0 always
/// paints after layer 1 when both are active, exactly like the original. The overlay tint uses
/// <see cref="RenderPass2D.Effects"/> too, but with <c>SortingLayer = -1</c> - strictly below every
/// Ground=1 layer's key (<c>SortingLayer = 0</c>) regardless of their <c>DepthOrder</c>/<c>LayerId</c>,
/// mirroring the original's <c>BackgroundUI - 2000</c> vs. <c>BackgroundUI - 1000</c> ordering - while
/// still comparing above <see cref="RenderPass2D.YSortedWorld"/> (500 &gt; 300), i.e. above every
/// floor/wall/entity/Ground=0 backdrop.
///
/// Blend handling: <see cref="SpriteRendererComponent"/>'s keyed <c>DrawSprite</c> overloads that take
/// a <see cref="SpriteBlendMode"/> apply a per-draw-run blend state. <see
/// cref="SpriteBlendMode.AlphaBlend"/> is <c>BlendState.NonPremultiplied</c>, which matches this
/// shader's non-premultiplied texel * color output: a vertex alpha of 128/255 (~0.5) yields exactly
/// <c>0.5 * src + 0.5 * dest</c>, the PSX "Average" blend mode (<see
/// cref="BackdropLayerData.BlendMode"/> == 1) - both for Ground=1 Tiles layers using it (true
/// semi-transparency, replacing the previous opaque-draw limitation) and for the overlay tint itself.
/// Every other layer keeps <see cref="SpriteBlendMode.Opaque"/> (the previous fixed behavior).
/// </summary>
internal sealed class BackdropRenderer
{

    private readonly struct LayerRuntime
    {
        public LayerRuntime(BackdropScrollarData scrollar, Texture2D texture, RenderSortKey2D sortKey, Color tint, SpriteBlendMode blendMode)
        {
            Scrollar = scrollar;
            Texture = texture;
            SortKey = sortKey;
            Tint = tint;
            BlendMode = blendMode;
        }

        public BackdropScrollarData Scrollar { get; }
        public Texture2D Texture { get; }
        public RenderSortKey2D SortKey { get; }
        public Color Tint { get; }
        public SpriteBlendMode BlendMode { get; }
    }

    private readonly List<LayerRuntime> _layers = new();
    private double _elapsedTicks;
    private bool _hasTint;
    private Color _tintColor;
    private RenderSortKey2D _tintSortKey;
    private Texture2D? _whiteTexture;

    /// <summary>
    /// True once there is anything at all for <see cref="Draw"/> to submit: at least one Tiles-mode
    /// layer with a usable texture, and/or the full-screen overlay tint (loaded independently of any
    /// layer - see the class doc; the 9 Cellular-only tinted maps in the corpus have zero Tiles
    /// layers but still need this true).
    /// </summary>
    public bool HasContent => _layers.Count > 0 || _hasTint;

    /// <summary>
    /// Loads this world's backdrop companion (see <see cref="BackdropLoader"/>) and resolves each
    /// Tiles-mode layer's texture through the same asset path every other converter texture uses
    /// (<see cref="Texture"/> wrapper -&gt; <c>AssetContentManager.Load&lt;Texture&gt;</c>, mirroring
    /// <c>Sprite.Load</c>/<c>TileMapComponent</c>'s own tile-sheet lookup). A layer whose texture id is
    /// missing, unparsable, or fails to load is skipped with one warning; nothing else about the world
    /// load is affected.
    /// </summary>
    public void Load(World world, string projectPath)
    {
        var document = BackdropLoader.Load(projectPath, world.Name);
        if (document == null)
        {
            return;
        }

        _hasTint = document.OverlayEnabled;
        if (_hasTint)
        {
            _tintColor = new Color(document.OverlayColorR, document.OverlayColorG, document.OverlayColorB, (byte)128);
            // SortingLayer = -1 sorts strictly below every Ground=1 layer's key (SortingLayer = 0
            // below) - see the class doc's Render pass mapping paragraph.
            _tintSortKey = new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

            // A 1x1 white texture stretched to viewport size draws the tint quad - the engine has no
            // reusable runtime white texture (only an editor-only one in TileMapEditorPanel), so one
            // is created once here and kept for this renderer's lifetime; at 4 bytes of GPU memory
            // per world it is not worth an explicit disposal path (this mirrors every Tiles layer's
            // own texture, which BackdropRenderer likewise never disposes - both live and die with
            // the world/game).
            _whiteTexture = new Texture2D(world.Game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            _whiteTexture.SetData(new[] { Color.White });
        }

        foreach (var layer in document.Layers)
        {
            if (layer.Mode != "Tiles" || layer.Scrollar == null || string.IsNullOrEmpty(layer.TextureAssetId))
            {
                continue;
            }

            if (!Guid.TryParse(layer.TextureAssetId, out var textureAssetId))
            {
                Logs.WriteWarning(
                    $"BackdropRenderer: world '{world.Name}' layer {layer.LayerId} has an unparsable "
                    + $"TextureAssetId '{layer.TextureAssetId}'; layer skipped.");
                continue;
            }

            Texture2D? texture2d;
            try
            {
                texture2d = world.Game.AssetContentManager.Load<CasaEngineTexture>(textureAssetId)?.Resource;
            }
            catch (Exception ex)
            {
                Logs.WriteWarning(
                    $"BackdropRenderer: world '{world.Name}' layer {layer.LayerId} texture "
                    + $"'{textureAssetId}' failed to load ({ex.Message}); layer skipped.");
                continue;
            }

            if (texture2d == null)
            {
                Logs.WriteWarning(
                    $"BackdropRenderer: world '{world.Name}' layer {layer.LayerId} texture "
                    + $"'{textureAssetId}' resolved to null; layer skipped.");
                continue;
            }

            var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;
            var sortKey = new RenderSortKey2D((int)renderPass, 0, layer.DepthOrder, 0, 0, 0, layer.LayerId);

            // BlendMode == 1 is the PSX "Average" blend - see the class doc's Blend paragraph.
            var isAverage = layer.Ground && layer.BlendMode == 1;
            var tint = isAverage ? new Color(255, 255, 255, 128) : Color.White;
            var blendMode = isAverage ? SpriteBlendMode.AlphaBlend : SpriteBlendMode.Opaque;

            _layers.Add(new LayerRuntime(layer.Scrollar, texture2d, sortKey, tint, blendMode));
        }
    }

    /// <summary>Advances the shared tick clock every layer's auto-scroll reads from - see
    /// <see cref="BackdropOffsetMath.TicksPerSecond"/>. Recomputed from total accumulated time rather
    /// than integrated per-frame deltas, so there is no drift to track across frames.</summary>
    public void Tick(float elapsedTime)
    {
        _elapsedTicks += elapsedTime * BackdropOffsetMath.TicksPerSecond;
    }

    /// <summary>
    /// Submits one tiled, allocation-free set of quads per layer through
    /// <paramref name="spriteRenderer"/>'s keyed <c>DrawSprite</c> path (the same one the tile map's
    /// runtime sorted overlay uses), covering the whole <paramref name="viewportWidth"/>x
    /// <paramref name="viewportHeight"/> viewport with no gaps (see
    /// <see cref="BackdropOffsetMath.ComputeCoveringQuadOrigins"/>).
    ///
    /// Each quad is positioned in world space as <paramref name="cameraPosition"/> plus a
    /// viewport-centered local offset, which cancels the camera's own view transform (screen =
    /// worldPos - cameraPosition for this engine's 2D camera) so the quad lands at the intended
    /// screen-space position regardless of where the camera currently is - the camera's own
    /// contribution to that position was already folded into the offset via
    /// <see cref="BackdropOffsetMath.ComputeParallaxOffset"/>. World Y is up-positive while screen Y
    /// is down-positive (see <see cref="AlundraWorldProxy.ResolveLogicalPosition"/>'s own note on this),
    /// hence the Y flip below; X needs no flip.
    /// </summary>
    public void Draw(SpriteRendererComponent spriteRenderer, Vector3 cameraPosition, int viewportWidth, int viewportHeight)
    {
        if ((_layers.Count == 0 && !_hasTint) || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var tickCount = (long)_elapsedTicks;
        var halfWidth = viewportWidth / 2f;
        var halfHeight = viewportHeight / 2f;

        // Explicit full-viewport scissor rectangle rather than the device's current one (see the
        // other keyed overload used below): the backdrop always covers the entire viewport and must
        // not depend on whatever clip some other component happened to leave active - also lets this
        // be exercised without a live GraphicsDevice (see BackdropRendererTests).
        var fullViewport = new Rectangle(0, 0, viewportWidth, viewportHeight);

        if (_hasTint && _whiteTexture != null)
        {
            // One quad covering the whole viewport, no tiling/parallax - mirrors the original's
            // fixed 320x240 rectangle (see the class doc). Scaling the 1x1 white texture by the
            // viewport size stretches it to the full quad; origin (0,0) with the same world-space Y
            // flip as the tiled layers below places its top-left corner at the viewport's top-left.
            var tintWorldPosition = new Vector2(cameraPosition.X - halfWidth, cameraPosition.Y + halfHeight);

            spriteRenderer.DrawSprite(
                _whiteTexture,
                _whiteTexture.Bounds,
                Point.Zero,
                tintWorldPosition,
                0f,
                new Vector2(viewportWidth, viewportHeight),
                _tintColor,
                0f,
                _tintSortKey,
                SpriteEffects.None,
                fullViewport,
                SpriteBlendMode.AlphaBlend);
        }

        foreach (var layer in _layers)
        {
            var scrollar = layer.Scrollar;

            var offsetX = BackdropOffsetMath.ComputeLayerOffset(
                cameraPosition.X, scrollar.FactorXNum, scrollar.FactorXDenom,
                scrollar.ScrollXSpeed, scrollar.ScrollXPeriod, tickCount, BackdropOffsetMath.CanvasWidth);
            var offsetY = BackdropOffsetMath.ComputeLayerOffset(
                cameraPosition.Y, scrollar.FactorYNum, scrollar.FactorYDenom,
                scrollar.ScrollYSpeed, scrollar.ScrollYPeriod, tickCount, BackdropOffsetMath.CanvasHeight);

            var origins = BackdropOffsetMath.ComputeCoveringQuadOrigins(viewportWidth, viewportHeight, offsetX, offsetY);

            foreach (var origin in origins)
            {
                var worldPosition = new Vector2(
                    cameraPosition.X + (origin.X - halfWidth),
                    cameraPosition.Y + (halfHeight - origin.Y));

                spriteRenderer.DrawSprite(
                    layer.Texture,
                    layer.Texture.Bounds,
                    Point.Zero,
                    worldPosition,
                    0f,
                    Vector2.One,
                    layer.Tint,
                    0f,
                    layer.SortKey,
                    SpriteEffects.None,
                    fullViewport,
                    layer.BlendMode);
            }
        }
    }
}
