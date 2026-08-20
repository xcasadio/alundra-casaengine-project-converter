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
/// rendering is a later chantier). A missing/absent companion file degrades to "nothing to draw",
/// logged once by <see cref="BackdropLoader"/>.
///
/// Render pass mapping (GraphicManager.RenderAllTileLayers/RenderLayerToBuffer @ 0x8005B670/
/// 0x8005B848 - see <see cref="BackdropLayerData"/>'s and <see cref="BackdropDocument"/>'s class
/// docs): <c>Ground == false</c> layers use <see cref="RenderPass2D.Background"/> (far below every
/// floor/wall/entity, matching the original's <c>-0x10000000 + order</c> depth); <c>Ground == true</c>
/// layers use <see cref="RenderPass2D.Effects"/> (above the whole Y-sorted world/foreground, below
/// UI, matching the original's near-<c>int.MaxValue</c> depth). Within a bucket, <see
/// cref="BackdropLayerData.DepthOrder"/> (1 for layer 0, 0 for layer 1) breaks ties so layer 0 always
/// paints after layer 1 when both are active, exactly like the original.
///
/// Blend handling: <see cref="SpriteRendererComponent"/> draws every sprite through one fixed,
/// non-alpha-blending <c>BlendState</c> (<c>ColorSourceBlend=One</c>, <c>ColorDestinationBlend=Zero</c>
/// - an opaque overwrite, not a soft blend), with no per-draw override. A true "Average" blend
/// (<see cref="BackdropLayerData.BlendMode"/> == 1, observed on map 389's cloud-shadow overlay) is
/// therefore not achievable without an engine change (out of scope for this slice, deferred and
/// documented here per the task brief). No draw-color approximation is attempted either: with that
/// fixed blend state and the shader's plain texel * color multiply, an alpha-only tint such as
/// (255,255,255,128) changes NOTHING on screen (RGB multipliers stay 1.0 and alpha is only used for
/// the discard test) - verified during review. Average-blend layers are drawn opaque until the
/// engine grows per-draw blend support.
/// </summary>
internal sealed class BackdropRenderer
{

    private readonly struct LayerRuntime
    {
        public LayerRuntime(BackdropScrollarData scrollar, Texture2D texture, RenderSortKey2D sortKey, Color tint)
        {
            Scrollar = scrollar;
            Texture = texture;
            SortKey = sortKey;
            Tint = tint;
        }

        public BackdropScrollarData Scrollar { get; }
        public Texture2D Texture { get; }
        public RenderSortKey2D SortKey { get; }
        public Color Tint { get; }
    }

    private readonly List<LayerRuntime> _layers = new();
    private double _elapsedTicks;

    /// <summary>True once at least one Tiles-mode layer with a usable texture was loaded.</summary>
    public bool HasLayers => _layers.Count > 0;

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

            // Average-blend layers draw opaque for now - see the class doc's Blend paragraph.
            _layers.Add(new LayerRuntime(layer.Scrollar, texture2d, sortKey, Color.White));
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
    /// is down-positive (see <see cref="AlundraWorldProxy.ResolveWorldPosition"/>'s own note on this),
    /// hence the Y flip below; X needs no flip.
    /// </summary>
    public void Draw(SpriteRendererComponent spriteRenderer, Vector3 cameraPosition, int viewportWidth, int viewportHeight)
    {
        if (_layers.Count == 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var tickCount = (long)_elapsedTicks;
        var halfWidth = viewportWidth / 2f;
        var halfHeight = viewportHeight / 2f;

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
                    SpriteEffects.None);
            }
        }
    }
}
