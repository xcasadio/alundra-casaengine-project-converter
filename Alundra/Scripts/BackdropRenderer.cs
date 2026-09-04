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
/// Every other layer keeps <see cref="SpriteBlendMode.Opaque"/> (the previous fixed behavior) - except
/// <c>Ground == true</c> layers with <c>BlendMode</c> 2/3/4, additive/subtractive/additive-attenuated
/// respectively (E10.b, docs/plan-e10-fondu.md §1.8) - see <see cref="ResolveGroundLayerBlend"/>'s own
/// doc for the exact mapping and the deliberately untouched <c>(Ground = false, BlendMode 1)</c> bucket.
/// </summary>
internal sealed class BackdropRenderer
{
    /// <summary>Test-only observation seam (docs/plan-e9-backdrops-residus.md §3, slice B1) - the last
    /// layer's wrapped canvas offset computed by the most recent <see cref="Draw"/> call, set right
    /// after <see cref="BackdropOffsetMath.ComputeLayerOffset"/> for each layer (so with one layer it
    /// pins that layer's exact offset). Never read by production code.</summary>
    internal (float OffsetX, float OffsetY)? LastLayerOffsetForTests { get; private set; }

    /// <summary>
    /// One loaded Tiles layer. Mutable (unlike the previous <c>readonly struct</c>) purely because
    /// <see cref="AnimFrameTimer"/>/<see cref="AnimFrameCounter"/> (D-E9-5,
    /// docs/plan-e9-backdrops-residus.md §2/§3 slice B3) must persist and advance frame over frame for
    /// the lifetime of the layer - a struct stored in <see cref="_layers"/> would need index-based
    /// mutation throughout <see cref="AdvanceAnimation"/> for no benefit.
    /// </summary>
    private sealed class LayerRuntime
    {
        public LayerRuntime(BackdropScrollarData scrollar, Texture2D[] frames, int animTimer, RenderSortKey2D sortKey, Color tint, SpriteBlendMode blendMode)
        {
            Scrollar = scrollar;
            Frames = frames;
            AnimTimer = animTimer;
            SortKey = sortKey;
            Tint = tint;
            BlendMode = blendMode;
        }

        public BackdropScrollarData Scrollar { get; }

        /// <summary>Every V-animation frame's texture, in order - <c>[0]</c> is the layer's original,
        /// always-present texture. Length is at least 1 (see <see cref="ResolveFrameAssetIds"/>), so
        /// <see cref="AnimFrameCounter"/> is always a valid index into this array.</summary>
        public Texture2D[] Frames { get; }

        /// <summary>The layer's own <see cref="BackdropLayerData.AnimTimer"/> (frames the counter holds
        /// before advancing - §1.2.a).</summary>
        public int AnimTimer { get; }

        public RenderSortKey2D SortKey { get; }
        public Color Tint { get; }
        public SpriteBlendMode BlendMode { get; }

        /// <summary>Ticks accumulated since the counter last advanced (§1.2.a's <c>AnimFrameTimer</c>).</summary>
        public int AnimFrameTimer { get; set; }

        /// <summary>Index into <see cref="Frames"/> of the frame currently drawn (§1.2.a's
        /// <c>AnimFrameCounter</c>).</summary>
        public int AnimFrameCounter { get; set; }
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
    /// A layer's <see cref="BackdropLayerData.FrameTextureAssetIds"/>, resolved to the array of ids this
    /// layer's frames actually load from (D-E9-9, docs/plan-e9-backdrops-residus.md §2/§3 slice B3) -
    /// pure and static so it is testable without a <see cref="GraphicsDevice"/>. A layer with no (or an
    /// empty) <see cref="BackdropLayerData.FrameTextureAssetIds"/> - every non-animated layer, and every
    /// companion written before this feature existed - resolves to exactly one id,
    /// <see cref="BackdropLayerData.TextureAssetId"/>; otherwise the array is returned as given (its
    /// <c>[0]</c> already equals <see cref="BackdropLayerData.TextureAssetId"/> by construction on the
    /// converter side).
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
    /// Loads one layer's frames in order through <paramref name="loadFrameTexture"/>, applying D-E9-9's
    /// partial-failure rule: frame 0 failing to load skips the layer entirely (returns
    /// <see langword="null"/>, same as today - "a layer = a failure = <c>continue</c>"); a later frame
    /// (<c>f &gt;= 1</c>) failing leaves the layer with only frame 0 loaded, never a partial/sparse
    /// array. Extracted as its own internal method, independent of <see cref="Texture2D"/>/
    /// <see cref="World"/> loading machinery, specifically so this rule is testable with a synthetic
    /// <paramref name="loadFrameTexture"/> delegate and no live <see cref="GraphicsDevice"/> - <see cref="Load"/>
    /// itself cannot run headless (see its own doc).
    /// </summary>
    internal static Texture2D[]? LoadLayerFrames(
        string?[] frameAssetIds, Func<string, Texture2D?> loadFrameTexture, string worldName, int layerId)
    {
        var frames = new List<Texture2D>(frameAssetIds.Length);

        for (var frameIndex = 0; frameIndex < frameAssetIds.Length; frameIndex++)
        {
            var frameAssetId = frameAssetIds[frameIndex];
            var texture2d = string.IsNullOrEmpty(frameAssetId) ? null : loadFrameTexture(frameAssetId);

            if (texture2d == null)
            {
                if (frameIndex == 0)
                {
                    Logs.WriteWarning(
                        $"BackdropRenderer: world '{worldName}' layer {layerId} frame 0 texture "
                        + $"'{frameAssetId}' failed to load; layer skipped.");
                    return null;
                }

                // D-E9-9: never a partial array - the frames already loaded after frame 0 are dropped
                // too, so the layer freezes on frame 0 exactly as the warning says (FIX of the fresh
                // verifier's F1: a bare `break` here kept frames 1..f-1 when f >= 2 failed).
                Logs.WriteWarning(
                    $"BackdropRenderer: world '{worldName}' layer {layerId} frame {frameIndex} texture "
                    + $"'{frameAssetId}' failed to load; layer falls back to frame 0 only.");
                return new[] { frames[0] };
            }

            frames.Add(texture2d);
        }

        return frames.ToArray();
    }

    /// <summary>
    /// Loads this world's backdrop companion (see <see cref="BackdropLoader"/>) and resolves each
    /// Tiles-mode layer's frame textures (D-E9-5/D-E9-9: <see cref="ResolveFrameAssetIds"/> then
    /// <see cref="LoadLayerFrames"/>) through the same asset path every other converter texture uses
    /// (<see cref="Texture"/> wrapper -&gt; <c>AssetContentManager.Load&lt;Texture&gt;</c>, mirroring
    /// <c>Sprite.Load</c>/<c>TileMapComponent</c>'s own tile-sheet lookup). A layer whose texture id is
    /// missing, unparsable, or whose frame 0 fails to load is skipped with one warning; a later frame
    /// failing degrades the layer to frame 0 only (D-E9-9); nothing else about the world load is
    /// affected.
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

            Texture2D? LoadFrameTexture(string assetIdString)
            {
                if (!Guid.TryParse(assetIdString, out var textureAssetId))
                {
                    Logs.WriteWarning(
                        $"BackdropRenderer: world '{world.Name}' layer {layer.LayerId} has an unparsable "
                        + $"TextureAssetId '{assetIdString}'.");
                    return null;
                }

                try
                {
                    var wrapperTexture = world.Game.AssetContentManager.Load<CasaEngineTexture>(textureAssetId);

                    // Two-step asset: Load<CasaEngineTexture> alone only reads the wrapper document -
                    // its inner Texture2D is materialized by the SECOND call below, the exact call
                    // TileMapComponent.cs:901-902 makes for tilesets. Skipping it left .Resource null
                    // for EVERY backdrop layer on EVERY map since this renderer was written: the
                    // degraded branch below fired each time, its warning went unread, and no headless
                    // test can reach this line (it needs a live GraphicsDevice - the known-uncovered
                    // link this file's tests document). Found when E10's witness-map check came back
                    // "no additive effect" on Fairy cave (underwater)-159.
                    wrapperTexture?.Load(world.Game.AssetContentManager);
                    return wrapperTexture?.Resource;
                }
                catch (Exception ex)
                {
                    Logs.WriteWarning(
                        $"BackdropRenderer: world '{world.Name}' layer {layer.LayerId} texture "
                        + $"'{textureAssetId}' failed to load ({ex.Message}).");
                    return null;
                }
            }

            var frameAssetIds = ResolveFrameAssetIds(layer);
            var frames = LoadLayerFrames(frameAssetIds, LoadFrameTexture, world.Name, layer.LayerId);
            if (frames == null)
            {
                continue;
            }

            var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;
            var sortKey = new RenderSortKey2D((int)renderPass, 0, layer.DepthOrder, 0, 0, 0, layer.LayerId);

            var (blendMode, tint) = ResolveGroundLayerBlend(layer.Ground, layer.BlendMode);

            _layers.Add(new LayerRuntime(layer.Scrollar, frames, layer.AnimTimer, sortKey, tint, blendMode));
        }
    }

    /// <summary>
    /// E10.b (docs/plan-e10-fondu.md §1.8): the ORIGINAL's own backdrop blend mapping
    /// (GraphicManager.cs:846-853) - 1 = average (unchanged from before this slice), 2 = additive white,
    /// 3 = subtractive white, 4 = additive tint (63,63,63) (the shader multiplies the tint into the
    /// source, so B + 0.247F against the original's own targeted 0.25F - a documented quantization gap,
    /// 63/255, with no fourth blend state involved). Only <paramref name="ground"/> == <see langword="true"/>
    /// layers are re-mapped by any of this - the <c>(Ground = false, BlendMode 1)</c> bucket (34 layers,
    /// re-verified on the export) is explicitly OUT OF SCOPE (the original per-PIXEL STP-bit gate that
    /// bucket needs, GraphicManager.cs:1233-1246, is unanalyzed) and must stay Opaque, untouched - so
    /// every other combination (including that bucket) falls through to the pre-existing Opaque/white
    /// default.
    ///
    /// Extracted as its own static, pure method (no <see cref="Texture2D"/>/<see cref="World"/> touched)
    /// specifically so it is testable with a synthetic document and no live <see cref="GraphicsDevice"/>
    /// (T8, docs/plan-e10-fondu.md).
    /// </summary>
    internal static (SpriteBlendMode BlendMode, Color Tint) ResolveGroundLayerBlend(bool ground, int blendMode)
    {
        if (ground)
        {
            switch (blendMode)
            {
                case 1: // Average - unchanged (§1.8): true semi-transparency via AlphaBlend.
                    return (SpriteBlendMode.AlphaBlend, new Color(255, 255, 255, 128));
                case 2: // Additive white.
                    return (SpriteBlendMode.Additive, Color.White);
                case 3: // Subtractive white.
                    return (SpriteBlendMode.Subtractive, Color.White);
                case 4: // Additive, tint (63,63,63) - see this method's own doc on the 0.247 vs 0.25
                        // quantization gap.
                    return (SpriteBlendMode.Additive, new Color(63, 63, 63));
            }
        }

        // Every other combination - including the deliberately untouched (Ground=false, BlendMode 1)
        // bucket - keeps the pre-existing fixed behavior.
        return (SpriteBlendMode.Opaque, Color.White);
    }

    /// <summary>Advances the shared tick clock every layer's auto-scroll reads from - see
    /// <see cref="BackdropOffsetMath.TicksPerSecond"/>. Recomputed from total accumulated time rather
    /// than integrated per-frame deltas, so there is no drift to track across frames.</summary>
    public void Tick(float elapsedTime)
    {
        _elapsedTicks += elapsedTime * BackdropOffsetMath.TicksPerSecond;
    }

    /// <summary>
    /// Replays the original's V-animation cadence (D-E9-5, docs/plan-e9-backdrops-residus.md §1.2.a/§2):
    /// for each of <paramref name="ticks"/> logic ticks, for every layer, in timer-then-counter order -
    /// <c>if (++AnimFrameTimer &gt; AnimTimer) { if (++AnimFrameCounter &gt;= animNum) AnimFrameCounter = 0; AnimFrameTimer = 0; }</c>
    /// - exactly as the original applies it once per logic tick, BEFORE that tick's draw (see
    /// <see cref="AlundraBackdropStage.UpdateAndDrawBackdrop"/>, which calls this first). The loop's
    /// <c>animNum</c> is <see cref="LayerRuntime.Frames"/>' length, NEVER
    /// <see cref="BackdropDocument.AnimNum"/> - it is therefore always at least 1 and
    /// <see cref="LayerRuntime.AnimFrameCounter"/> is always a valid index, even for a companion whose
    /// document-level <c>AnimNum</c> disagrees with how many frames actually got resolved/loaded. A
    /// one-frame layer runs the same loop; its counter always resets back to 0.
    /// </summary>
    public void AdvanceAnimation(int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            foreach (var layer in _layers)
            {
                layer.AnimFrameTimer++;
                if (layer.AnimFrameTimer > layer.AnimTimer)
                {
                    layer.AnimFrameCounter++;
                    if (layer.AnimFrameCounter >= layer.Frames.Length)
                    {
                        layer.AnimFrameCounter = 0;
                    }

                    layer.AnimFrameTimer = 0;
                }
            }
        }
    }

    /// <summary>
    /// Submits one tiled, allocation-free set of quads per layer through
    /// <paramref name="spriteRenderer"/>'s keyed <c>DrawSprite</c> path (the same one the tile map's
    /// runtime sorted overlay uses), covering the whole <paramref name="viewportWidth"/>x
    /// <paramref name="viewportHeight"/> viewport with no gaps (see
    /// <see cref="BackdropOffsetMath.ComputeCoveringQuadOrigins"/>).
    ///
    /// Each quad is positioned in world space as <paramref name="renderCamera"/> plus a
    /// viewport-centered local offset, which cancels the camera's own view transform (screen =
    /// worldPos - cameraPosition for this engine's 2D camera) so the quad lands at the intended
    /// screen-space position regardless of where the camera currently is - the camera's own
    /// contribution to that position was already folded into the offset via
    /// <see cref="BackdropOffsetMath.ComputeParallaxOffset"/>. World Y is up-positive while screen Y
    /// is down-positive (see <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/>'s own note on this),
    /// hence the Y flip below; X needs no flip.
    ///
    /// <paramref name="scrollX"/>/<paramref name="scrollY"/> are the original's own
    /// <c>g_cameraScrollingX/Y</c> (docs/plan-e9-backdrops-residus.md D-E9-1) - produced by
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/>, the ONE place that converts render space
    /// back to scroll space - and are fed to <see cref="BackdropOffsetMath.ComputeLayerOffset"/> AS-IS
    /// on both axes: no negation happens here any more. <paramref name="renderCamera"/> is unrelated to
    /// that parallax term; it is only the render-space camera position used to place the quads (and the
    /// tint quad above) in world space.
    /// </summary>
    public void Draw(
        SpriteRendererComponent spriteRenderer, int scrollX, int scrollY, Vector3 renderCamera, int viewportWidth, int viewportHeight)
    {
        if ((_layers.Count == 0 && !_hasTint) || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var cameraPosition = renderCamera;

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
                scrollX, scrollar.FactorXNum, scrollar.FactorXDenom,
                scrollar.ScrollXSpeed, scrollar.ScrollXPeriod, tickCount, BackdropOffsetMath.CanvasWidth);
            var offsetY = BackdropOffsetMath.ComputeLayerOffset(
                scrollY, scrollar.FactorYNum, scrollar.FactorYDenom,
                scrollar.ScrollYSpeed, scrollar.ScrollYPeriod, tickCount, BackdropOffsetMath.CanvasHeight);

            // Observation seam (docs/plan-e9-backdrops-residus.md §3, slice B1 acceptance): the quad
            // world positions submitted below do not expose the wrapped canvas offset they were
            // computed from without inverting the covering-quad tiling math, so the last layer's
            // computed offset is recorded here for BackdropRendererTests to assert on directly at the
            // production call site (AlundraBackdropStage.UpdateAndDrawBackdrop -> Draw). Test-only;
            // never read by production code.
            LastLayerOffsetForTests = (offsetX, offsetY);

            var origins = BackdropOffsetMath.ComputeCoveringQuadOrigins(viewportWidth, viewportHeight, offsetX, offsetY);

            // D-E9-5: draws the frame AdvanceAnimation's counter currently points at - advance-then-draw
            // within the same tick, exactly like the original (§1.2.a precedes §1.2.b/GraphicManager.cs:943).
            var frame = layer.Frames[layer.AnimFrameCounter];

            foreach (var origin in origins)
            {
                var worldPosition = new Vector2(
                    cameraPosition.X + (origin.X - halfWidth),
                    cameraPosition.Y + (halfHeight - origin.Y));

                spriteRenderer.DrawSprite(
                    frame,
                    frame.Bounds,
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
