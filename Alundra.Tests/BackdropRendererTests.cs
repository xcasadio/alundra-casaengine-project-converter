using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="BackdropDocument"/>'s overlay-tint parsing and <see cref="BackdropRenderer"/>'s
/// tint submission (see <c>docs/formats/backdrops.md</c> and <see cref="BackdropRenderer"/>'s class
/// doc). Actual texture loading through <c>AssetContentManager</c> needs a live GraphicsDevice and is
/// exercised manually (see the task's validation notes) - here the renderer's private tint fields are
/// set directly via reflection to exercise <see cref="BackdropRenderer.Draw"/> headlessly, exactly
/// like <c>BackdropOffsetMathTests</c>'s own note on why full rendering is not unit-testable.
/// </summary>
public class BackdropRendererTests
{
    [Fact]
    public void BackdropDocument_DeserializesOverlayTintFields()
    {
        const string json = """
            {
              "MapIndex": 18,
              "Enabled": true,
              "OverlayEnabled": true,
              "OverlayColorR": 84,
              "OverlayColorG": 75,
              "OverlayColorB": 52,
              "Layers": []
            }
            """;

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.True(document!.OverlayEnabled);
        Assert.Equal(84, document.OverlayColorR);
        Assert.Equal(75, document.OverlayColorG);
        Assert.Equal(52, document.OverlayColorB);
    }

    [Fact]
    public void BackdropDocument_WithAbsentOverlayFields_DefaultsToTintDisabled()
    {
        // An old companion written before this feature existed - no Overlay* properties at all.
        const string json = """{ "MapIndex": 4, "Enabled": true, "Layers": [] }""";

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.False(document!.OverlayEnabled);
        Assert.Equal(0, document.OverlayColorR);
        Assert.Equal(0, document.OverlayColorG);
        Assert.Equal(0, document.OverlayColorB);
    }

    [Fact]
    public void TintSortKey_IsStrictlyBelowAGround1LayerKey_RegardlessOfDepthOrder()
    {
        // Mirrors BackdropRenderer.Load's own construction exactly (see its class doc's Render pass
        // mapping paragraph): SortingLayer = -1 for the tint vs. SortingLayer = 0 for every Ground=1
        // layer - RenderPass2D.Effects is equal on both sides, so SortingLayer alone must decide it.
        var tintKey = new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

        var ground1LayerKeyOrder0 = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 1);
        var ground1LayerKeyOrder1 = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 1, 0, 0, 0, 0);

        Assert.True(tintKey.CompareTo(ground1LayerKeyOrder0) < 0);
        Assert.True(tintKey.CompareTo(ground1LayerKeyOrder1) < 0);

        // Still above the Y-sorted world (every floor/wall/entity) and every Ground=0 backdrop.
        var ySortedWorldKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
        Assert.True(tintKey.CompareTo(ySortedWorldKey) > 0);
    }

    [Fact]
    public void HasContent_IsTrueFromTintAlone_WithZeroLayers()
    {
        var renderer = new BackdropRenderer();
        SetPrivateField(renderer, "_hasTint", true);

        Assert.True(renderer.HasContent);
    }

    [Fact]
    public void Draw_WithTintAndZeroLayers_SubmitsOneAlphaBlendQuadAtTheTintSortKey()
    {
        // The round-2 fix's own scenario: a Cellular-only map (zero Tiles layers) whose tint must
        // still be drawn - HasContent (and therefore AlundraWorldProxy's gate) must not depend on
        // _layers being non-empty.
        var renderer = new BackdropRenderer();
        var tintColor = new Color(40, 40, 40, 128);
        var tintSortKey = new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

        SetPrivateField(renderer, "_hasTint", true);
        SetPrivateField(renderer, "_tintColor", tintColor);
        SetPrivateField(renderer, "_tintSortKey", tintSortKey);
        SetPrivateField(renderer, "_whiteTexture", CreateTexture());

        var spriteRenderer = CreateSpriteRendererComponent();

        renderer.Draw(spriteRenderer, scrollX: 0, scrollY: 0, renderCamera: Vector3.Zero, viewportWidth: 320, viewportHeight: 240);

        var spriteDatas = GetSpriteDatas(spriteRenderer);
        Assert.Single(spriteDatas);

        var entry = spriteDatas[0]!;
        Assert.Equal(tintColor, (Color)GetField(entry, "Color"));
        Assert.Equal(tintSortKey, (RenderSortKey2D)GetField(entry, "SortKey"));

        // SpriteBlendMode.AlphaBlend is BlendState.NonPremultiplied; with this shader's
        // non-premultiplied texel * color output, a vertex alpha of 128/255 (~0.5) yields exactly
        // 0.5 * src + 0.5 * dest - the PSX "Average"/overlay-tint blend equation (see
        // BackdropRenderer's class doc Blend paragraph and SpriteBlendMode.AlphaBlend's own doc).
        Assert.Equal(SpriteBlendMode.AlphaBlend, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    // ---- T8 (docs/plan-e10-fondu.md, slice E10.b, §1.8): the 36-layer backdrop blend mapping ---------

    [Fact]
    public void ResolveGroundLayerBlend_MapsAllFourGroundBlendModes_ExactBlendAndTintPairs()
    {
        // 1 = Average, unchanged from before this slice.
        var (blend1, tint1) = BackdropRenderer.ResolveGroundLayerBlend(ground: true, blendMode: 1);
        Assert.Equal(SpriteBlendMode.AlphaBlend, blend1);
        Assert.Equal(new Color(255, 255, 255, 128), tint1);

        // 2 = Additive, white.
        var (blend2, tint2) = BackdropRenderer.ResolveGroundLayerBlend(ground: true, blendMode: 2);
        Assert.Equal(SpriteBlendMode.Additive, blend2);
        Assert.Equal(Color.White, tint2);

        // 3 = Subtractive, white.
        var (blend3, tint3) = BackdropRenderer.ResolveGroundLayerBlend(ground: true, blendMode: 3);
        Assert.Equal(SpriteBlendMode.Subtractive, blend3);
        Assert.Equal(Color.White, tint3);

        // 4 = Additive, tint (63,63,63) - the 0.247 vs 0.25 quantization gap documented on the method.
        var (blend4, tint4) = BackdropRenderer.ResolveGroundLayerBlend(ground: true, blendMode: 4);
        Assert.Equal(SpriteBlendMode.Additive, blend4);
        Assert.Equal(new Color(63, 63, 63), tint4);
    }

    [Fact]
    public void ResolveGroundLayerBlend_GroundFalseBlendMode1_StaysOpaque_OutOfScopeBucketUntouched()
    {
        // The deliberately untouched bucket (§1.8): (Ground=false, BlendMode 1) x34 on the export - the
        // original gates this one per-pixel on the STP bit (unanalyzed) - must stay Opaque.
        var (blend, tint) = BackdropRenderer.ResolveGroundLayerBlend(ground: false, blendMode: 1);
        Assert.Equal(SpriteBlendMode.Opaque, blend);
        Assert.Equal(Color.White, tint);
    }

    [Fact]
    public void ResolveGroundLayerBlend_UnknownGroundBlendMode_FallsBackToOpaqueWhite()
    {
        var (blend, tint) = BackdropRenderer.ResolveGroundLayerBlend(ground: true, blendMode: 99);
        Assert.Equal(SpriteBlendMode.Opaque, blend);
        Assert.Equal(Color.White, tint);
    }

    /// <summary>
    /// The user's own bug report, first visible the day the backdrop textures finally loaded: "des que
    /// la camera se deplace verticalement les nuages bougent plus vite". Map 389's cloud layer has
    /// parallax factor 1/1 on BOTH axes - it must be GLUED TO THE WORLD, moving on screen exactly like
    /// the tiles. The original defect (pre-D-E9-1) was Draw itself feeding the RENDER-space camera Y
    /// (up-positive) into ComputeLayerOffset, where the original consumes a WORLD-space scroll
    /// (down-positive, g_cameraScrollingY) - so the vertical parallax term carried the wrong sign and
    /// the layer drifted at TWICE the camera's vertical movement. X was fine (no flip on that axis),
    /// which is why the symptom was vertical-only. Since D-E9-1 that conversion lives solely in
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/>, so this test now drives Draw the same way
    /// the production call site does - through that conversion - rather than by feeding Draw a raw
    /// render-space Y directly.
    ///
    /// Discriminating invariant, at the production call site (Draw): with factor 1/1 and no
    /// auto-scroll, the submitted quads' world positions must be IDENTICAL for two camera positions
    /// that differ only in Y (world-glued, same wrap window). Under the sign bug they differ by twice
    /// the camera delta.
    /// </summary>
    [Fact]
    public void Draw_Factor1Layer_StaysWorldGlued_WhenCameraMovesVertically()
    {
        var renderer = CreateRendererWithOneFactor1Layer();
        var spriteRenderer = CreateSpriteRendererComponent();

        // Camera moves DOWN in the world by 10 px: render-space Y (up-positive) decreases by 10.
        // Deltas chosen well inside one 480-px canvas period so no wrap boundary is crossed.
        var cameraA = new Vector3(0f, -100f, 0f);
        var cameraB = new Vector3(0f, -110f, 0f);
        var scrollA = AlundraCameraMath.ToOriginalScrollSpace(cameraA);
        var scrollB = AlundraCameraMath.ToOriginalScrollSpace(cameraB);

        renderer.Draw(spriteRenderer, scrollA.X, scrollA.Y, cameraA, viewportWidth: 320, viewportHeight: 240);
        var quadsA = ReadLayerQuadTranslations(spriteRenderer);
        GetSpriteDatas(spriteRenderer).Clear();

        renderer.Draw(spriteRenderer, scrollB.X, scrollB.Y, cameraB, viewportWidth: 320, viewportHeight: 240);
        var quadsB = ReadLayerQuadTranslations(spriteRenderer);

        Assert.NotEmpty(quadsA);
        Assert.NotEmpty(quadsB);

        // World-glued: the canvas grid sits at the same world alignment for both camera positions.
        // (Compared modulo the 480-px canvas period: a wrap re-tiling may add/remove an edge quad,
        // never move the grid itself.)
        static float Mod(float v, float m) => ((v % m) + m) % m;
        var alignmentA = Mod(quadsA[0].Y, 480f);
        var alignmentB = Mod(quadsB[0].Y, 480f);
        Assert.Equal(alignmentA, alignmentB, precision: 3);

        // Horizontal guard: X was never affected and must stay world-glued too.
        Assert.Equal(Mod(quadsA[0].X, 640f), Mod(quadsB[0].X, 640f), precision: 3);
    }

    // ---- D-E9-1 (docs/plan-e9-backdrops-residus.md §3, slice B1): parallax on the clamped scroll ----

    /// <summary>
    /// Site-of-production pin: drives <see cref="AlundraBackdropStage.UpdateAndDrawBackdrop"/> itself
    /// (not <see cref="BackdropRenderer.Draw"/> directly) with the resolved camera's render-space
    /// <c>Target</c> set to the 389's own far corner (<c>(1087, -839, 0)</c>, the frozen E5 bound - see
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/>'s own doc), a single factor-1/1 tiles
    /// layer, and zero auto-scroll. Asserts the ABSOLUTE wrapped offset via
    /// <see cref="BackdropRenderer.LastLayerOffsetForTests"/> (the observation seam this slice adds -
    /// <see cref="BackdropRenderer.Draw"/>'s own quad world positions do not expose it without
    /// inverting the covering-quad tiling math): <c>offsetX = (1087 - 160) mod 640 = 927 mod 640 =
    /// 287</c>, <c>offsetY = (-(-839) - 120) mod 480 = 719 mod 480 = 239</c> - computed by D-E9-1's
    /// formula and no other (see the plan's own mutation table: feeding the raw <c>Target</c> instead
    /// of the converted scroll gives 447; a residual <c>-scrollY</c> in <c>Draw</c> gives 241).
    /// </summary>
    [Fact]
    public void UpdateAndDrawBackdrop_ProductionSite_PinsAbsoluteParallaxOffset_OnClampedScroll()
    {
        var stage = new AlundraBackdropStage();
        var renderer = GetStageBackdropRenderer(stage);
        AddOneFactor1Layer(renderer);

        var spriteRenderer = CreateSpriteRendererComponent();
        var world = BuildWorldWithGame((CasaEngineGame)spriteRenderer.Game, viewportWidth: 320, viewportHeight: 240);
        var camera = new Camera2dComponent { Target = new Vector3(1087f, -839f, 0f) };

        stage.UpdateAndDrawBackdrop(elapsedTime: 0f, ticksThisFrame: 0, world, camera);

        var offset = renderer.LastLayerOffsetForTests;
        Assert.NotNull(offset);
        Assert.Equal(287f, offset!.Value.OffsetX);
        Assert.Equal(239f, offset.Value.OffsetY);
    }

    /// <summary>
    /// E9.a B5 (docs/plan-e9-backdrops-residus.md §5, map 159): the production site anchors the canvas
    /// on the ORIGINAL's 320x240 framebuffer in world units, never on the window's pixel size. The
    /// montage therefore gives the game the REAL window size in pixels (1280x944, the launcher's
    /// DebugWidth/Height - the camera zoom of 4 maps it onto the 320x236 view): a screen-fixed layer
    /// (parallax factors 0/1, the shape of map 159's band) at Target = (1087, -839) must be submitted
    /// as exactly ONE covering quad whose top-left is the screen's top-left, Target + (-160, +120) =
    /// (927, -719) (E5: Target is the framebuffer centre). With the pixel size the stage used to pass,
    /// Draw took half-extents of (640, 472): four quads, the first at (447, -367) - the defect that put
    /// map 159's band at screen row 126 instead of 0 (B4). The renderer stores a quad's centre,
    /// top-left + (width / 2, -height / 2) in the Y-up world, hence the bounds terms.
    /// </summary>
    [Fact]
    public void UpdateAndDrawBackdrop_ProductionSite_AnchorsTheCanvasOnTheOriginalScreen_NotOnTheWindowPixels()
    {
        var stage = new AlundraBackdropStage();
        var renderer = GetStageBackdropRenderer(stage);
        AddOneScreenFixedLayer(renderer);

        var spriteRenderer = CreateSpriteRendererComponent();
        var world = BuildWorldWithGame((CasaEngineGame)spriteRenderer.Game, viewportWidth: 1280, viewportHeight: 944);
        var camera = new Camera2dComponent { Target = new Vector3(1087f, -839f, 0f) };

        stage.UpdateAndDrawBackdrop(elapsedTime: 0f, ticksThisFrame: 0, world, camera);

        var quad = Assert.Single(ReadLayerQuadTranslations(spriteRenderer));
        var bounds = ((Texture2D)GetField(GetSpriteDatas(spriteRenderer)[0]!, "Texture")).Bounds;
        Assert.Equal(1087f - 160f + bounds.Width / 2f, quad.X);
        Assert.Equal(-839f + 120f - bounds.Height / 2f, quad.Y);

        var offset = renderer.LastLayerOffsetForTests;
        Assert.NotNull(offset);
        Assert.Equal(0f, offset!.Value.OffsetX);
        Assert.Equal(0f, offset.Value.OffsetY);
    }

    // ---- D-E9-5/D-E9-9 (docs/plan-e9-backdrops-residus.md §3, slice B3): V-animation replay ----------

    /// <summary>
    /// Pure cadence pin (§1.2.a): <c>Frames.Length = 4</c>, <c>AnimTimer = 6</c> - the counter's own
    /// sequence, one <see cref="BackdropRenderer.AdvanceAnimation"/>(1) call per tick (advance precedes
    /// the read, exactly as it precedes <see cref="BackdropRenderer.Draw"/> in production), over 40
    /// ticks: <c>0x6, 1x7, 2x7, 3x7, 0x7, 1x6</c> - the first plateau is one tick short (the first tick
    /// already consumes <c>AnimFrameTimer = 1</c>), every following plateau is the full
    /// <c>AnimTimer + 1 = 7</c> ticks.
    /// </summary>
    [Fact]
    public void AdvanceAnimation_FourFramesTimer6_ProducesExactCadence_OverFortyTicks()
    {
        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: 4, animTimer: 6);

        var drawn = new List<int>();
        for (var tick = 0; tick < 40; tick++)
        {
            renderer.AdvanceAnimation(1);
            drawn.Add((int)GetLayerAnimFrameCounter(layer));
        }

        var expected = RepeatFrames((0, 6), (1, 7), (2, 7), (3, 7), (0, 7), (1, 6));
        Assert.Equal(expected, drawn);
    }

    /// <summary>Same cadence rule, <c>AnimTimer = 4</c>: the first plateau is 4 ticks, every later one 5
    /// (<c>AnimTimer + 1</c>).</summary>
    [Fact]
    public void AdvanceAnimation_FourFramesTimer4_FirstPlateauFour_ThenFivePerFrame()
    {
        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: 4, animTimer: 4);

        var drawn = new List<int>();
        for (var tick = 0; tick < 14; tick++)
        {
            renderer.AdvanceAnimation(1);
            drawn.Add((int)GetLayerAnimFrameCounter(layer));
        }

        var expected = RepeatFrames((0, 4), (1, 5), (2, 5));
        Assert.Equal(expected, drawn);
    }

    /// <summary>A one-frame layer never advances: the counter always resets straight back to 0.</summary>
    [Fact]
    public void AdvanceAnimation_OneFrameLayer_CounterAlwaysZero()
    {
        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: 1, animTimer: 6);

        for (var tick = 0; tick < 40; tick++)
        {
            renderer.AdvanceAnimation(1);
            Assert.Equal(0, (int)GetLayerAnimFrameCounter(layer));
        }
    }

    /// <summary>
    /// Headless montage (real <see cref="BackdropRenderer.Draw"/>, four distinct <see cref="Texture2D"/>
    /// injected by reflection): the SAME cadence as the pure test above, now observed through the actual
    /// texture <see cref="BackdropRenderer.Draw"/> submits - at tick 7 (the first tick that lands on
    /// frame 1) the submitted texture is <c>Frames[1]</c>; at tick 28 (the first tick of the second
    /// 0..3 cycle) it is back to <c>Frames[0]</c>.
    /// </summary>
    [Fact]
    public void Draw_AfterAdvancingTicks_SubmitsTheExpectedFrameTexture()
    {
        var renderer = new BackdropRenderer();
        var frames = new[] { CreateTexture(), CreateTexture(), CreateTexture(), CreateTexture() };
        AddSyntheticAnimatedLayer(renderer, frames, animTimer: 6);

        var spriteRenderer = CreateSpriteRendererComponent();

        renderer.AdvanceAnimation(7);
        renderer.Draw(spriteRenderer, scrollX: 0, scrollY: 0, renderCamera: Vector3.Zero, viewportWidth: 320, viewportHeight: 240);
        var textureAtTick7 = (Texture2D)GetField(GetSpriteDatas(spriteRenderer)[0]!, "Texture");
        Assert.Same(frames[1], textureAtTick7);

        GetSpriteDatas(spriteRenderer).Clear();
        renderer.AdvanceAnimation(21); // tick 7 + 21 = tick 28.
        renderer.Draw(spriteRenderer, scrollX: 0, scrollY: 0, renderCamera: Vector3.Zero, viewportWidth: 320, viewportHeight: 240);
        var textureAtTick28 = (Texture2D)GetField(GetSpriteDatas(spriteRenderer)[0]!, "Texture");
        Assert.Same(frames[0], textureAtTick28);
    }

    /// <summary>D-E9-9's fallback: a layer with no (or an empty) <c>FrameTextureAssetIds</c> resolves to
    /// exactly <c>[TextureAssetId]</c> - one element, equal to the layer's own id.</summary>
    [Fact]
    public void ResolveFrameAssetIds_LayerWithNoFrameIds_ResolvesToExactlyTheTextureAssetId()
    {
        var layer = new BackdropLayerData { TextureAssetId = "layer-texture-id", FrameTextureAssetIds = null };
        Assert.Equal(new[] { "layer-texture-id" }, BackdropRenderer.ResolveFrameAssetIds(layer));

        var layerWithEmptyArray = new BackdropLayerData { TextureAssetId = "layer-texture-id", FrameTextureAssetIds = Array.Empty<string>() };
        Assert.Equal(new[] { "layer-texture-id" }, BackdropRenderer.ResolveFrameAssetIds(layerWithEmptyArray));
    }

    /// <summary>A layer WITH resolved frame ids gets them back unchanged.</summary>
    [Fact]
    public void ResolveFrameAssetIds_LayerWithFrameIds_ReturnsThemAsGiven()
    {
        var frameIds = new[] { "id0", "id1", "id2", "id3" };
        var layer = new BackdropLayerData { TextureAssetId = "id0", FrameTextureAssetIds = frameIds };
        Assert.Same(frameIds, BackdropRenderer.ResolveFrameAssetIds(layer));
    }

    /// <summary>
    /// A document with <c>AnimNum = 4</c> but no <c>FrameTextureAssetIds</c> at all (an old companion,
    /// or a fixture that never got B2's per-frame export) resolves via <see cref="BackdropRenderer.ResolveFrameAssetIds"/>
    /// to a single frame - advancing 40 ticks must observe frame 0 only, with no exception (the loop's
    /// <c>animNum</c> is <c>Frames.Length</c>, never the document-level <c>AnimNum</c> - D-E9-5).
    /// </summary>
    [Fact]
    public void AdvanceAnimation_LayerResolvedFromNoFrameIds_NeverLeavesFrameZero_NoException()
    {
        var layerData = new BackdropLayerData { TextureAssetId = "solo-texture", FrameTextureAssetIds = null, AnimTimer = 6 };
        var resolvedIds = BackdropRenderer.ResolveFrameAssetIds(layerData);
        Assert.Single(resolvedIds);

        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: resolvedIds.Length, animTimer: layerData.AnimTimer);

        var exception = Record.Exception(() =>
        {
            for (var tick = 0; tick < 40; tick++)
            {
                renderer.AdvanceAnimation(1);
                Assert.Equal(0, (int)GetLayerAnimFrameCounter(layer));
            }
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// D-E9-9's partial-failure rule, exercised through the loader-delegate seam
    /// <see cref="BackdropRenderer.LoadLayerFrames"/> adds specifically so this is testable without a
    /// live <see cref="GraphicsDevice"/> (<see cref="BackdropRenderer.Load"/> itself needs one): frame 0
    /// loads, frame 1 fails - the layer falls back to a ONE-FRAME array (frame 0 only), never a partial
    /// array reaching frame 2's id.
    /// </summary>
    [Fact]
    public void LoadLayerFrames_FrameOneFails_FallsBackToFrameZeroOnly()
    {
        var frame0 = CreateTexture();
        var frameAssetIds = new[] { "frame0-id", "frame1-id", "frame2-id" };

        Texture2D? LoadFrame(string assetId) => assetId == "frame0-id" ? frame0 : null;

        var frames = BackdropRenderer.LoadLayerFrames(frameAssetIds, LoadFrame, worldName: "TestWorld", layerId: 0);

        Assert.NotNull(frames);
        Assert.Single(frames!);
        Assert.Same(frame0, frames![0]);
    }

    /// <summary>
    /// D-E9-9's "never a partial array" half, pinned where the f = 1 case cannot see it: when a LATER
    /// frame fails after frames 1..f-1 loaded fine, those already-loaded frames must be dropped too -
    /// the layer falls back to exactly [frame0], never [frame0, frame1, frame2]. (FIX of the fresh
    /// verifier's F1 on slice B3: a bare `break` returned the partial prefix.)
    /// </summary>
    [Fact]
    public void LoadLayerFrames_FrameThreeFails_FallsBackToFrameZeroOnly_NotToThePartialPrefix()
    {
        var frame0 = CreateTexture();
        var frame1 = CreateTexture();
        var frame2 = CreateTexture();
        var frameAssetIds = new[] { "frame0-id", "frame1-id", "frame2-id", "frame3-id" };

        Texture2D? LoadFrame(string assetId) => assetId switch
        {
            "frame0-id" => frame0,
            "frame1-id" => frame1,
            "frame2-id" => frame2,
            _ => null,
        };

        var frames = BackdropRenderer.LoadLayerFrames(frameAssetIds, LoadFrame, worldName: "TestWorld", layerId: 0);

        Assert.NotNull(frames);
        Assert.Single(frames!);
        Assert.Same(frame0, frames![0]);
    }

    /// <summary>Frame 0 itself failing is the pre-existing rule: the whole layer is skipped
    /// (<see langword="null"/>), same as "a layer = a failure = <c>continue</c>" today.</summary>
    [Fact]
    public void LoadLayerFrames_FrameZeroFails_SkipsTheWholeLayer()
    {
        var frameAssetIds = new[] { "frame0-id", "frame1-id" };

        Texture2D? LoadFrame(string assetId) => null;

        var frames = BackdropRenderer.LoadLayerFrames(frameAssetIds, LoadFrame, worldName: "TestWorld", layerId: 0);

        Assert.Null(frames);
    }

    /// <summary>Every frame loading successfully returns the full dense array, in order.</summary>
    [Fact]
    public void LoadLayerFrames_AllFramesSucceed_ReturnsTheFullDenseArray()
    {
        var frame0 = CreateTexture();
        var frame1 = CreateTexture();
        var frameAssetIds = new[] { "frame0-id", "frame1-id" };

        Texture2D? LoadFrame(string assetId) => assetId == "frame0-id" ? frame0 : frame1;

        var frames = BackdropRenderer.LoadLayerFrames(frameAssetIds, LoadFrame, worldName: "TestWorld", layerId: 0);

        Assert.NotNull(frames);
        Assert.Equal(new[] { frame0, frame1 }, frames);
    }

    private static List<int> RepeatFrames(params (int Frame, int Count)[] plateaus)
    {
        var result = new List<int>();
        foreach (var (frame, count) in plateaus)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(frame);
            }
        }

        return result;
    }

    private static object AddSyntheticAnimatedLayer(BackdropRenderer renderer, int frameCount, int animTimer)
    {
        var frames = new Texture2D[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = CreateTexture();
        }

        return AddSyntheticAnimatedLayer(renderer, frames, animTimer);
    }

    private static object AddSyntheticAnimatedLayer(BackdropRenderer renderer, Texture2D[] frames, int animTimer)
    {
        var scrollar = new BackdropScrollarData
        {
            FactorXNum = 1, FactorXDenom = 1,
            FactorYNum = 1, FactorYDenom = 1,
            ScrollXSpeed = 0, ScrollXPeriod = 0,
            ScrollYSpeed = 0, ScrollYPeriod = 0,
        };

        var sortKey = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, frames, animTimer, sortKey, Color.White, SpriteBlendMode.AlphaBlend)!;

        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;
        layers.Add(layer);

        return layer;
    }

    private static object GetLayerAnimFrameCounter(object layer)
    {
        var property = layer.GetType().GetProperty("AnimFrameCounter", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property!.GetValue(layer)!;
    }

    private static BackdropRenderer GetStageBackdropRenderer(AlundraBackdropStage stage)
    {
        var field = typeof(AlundraBackdropStage).GetField("_backdropRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (BackdropRenderer)field!.GetValue(stage)!;
    }

    private static void AddOneFactor1Layer(BackdropRenderer renderer)
    {
        var scrollar = new BackdropScrollarData
        {
            FactorXNum = 1, FactorXDenom = 1,
            FactorYNum = 1, FactorYDenom = 1,
            ScrollXSpeed = 0, ScrollXPeriod = 0,
            ScrollYSpeed = 0, ScrollYPeriod = 0,
        };

        var frames = new[] { CreateTexture() };
        var sortKey = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, frames, 0, sortKey, Color.White, SpriteBlendMode.AlphaBlend);

        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;
        layers.Add(layer);
    }

    /// <summary>
    /// A screen-fixed layer - parallax factors 0/1 on both axes, no auto-scroll, the shape of map
    /// 159's band - so the covering-quad origin is (0, 0) whatever the camera Target (E9.a B5).
    /// </summary>
    private static void AddOneScreenFixedLayer(BackdropRenderer renderer)
    {
        var scrollar = new BackdropScrollarData
        {
            FactorXNum = 0, FactorXDenom = 1,
            FactorYNum = 0, FactorYDenom = 1,
            ScrollXSpeed = 0, ScrollXPeriod = 0,
            ScrollYSpeed = 0, ScrollYPeriod = 0,
        };

        var frames = new[] { CreateTexture() };
        var sortKey = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, frames, 0, sortKey, Color.White, SpriteBlendMode.AlphaBlend);

        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;
        layers.Add(layer);
    }

    /// <summary>
    /// Builds a headless <see cref="World"/> whose <see cref="World.Game"/> is <paramref name="game"/>
    /// (set via reflection - the property's setter is private) so
    /// <see cref="AlundraBackdropStage.UpdateAndDrawBackdrop"/>'s two early-return guards
    /// (<c>world?.Game == null</c>, then <c>GetGameComponent&lt;SpriteRendererComponent&gt;()</c>) both
    /// clear without needing a live <c>GraphicsDevice</c>: <paramref name="game"/>'s
    /// <c>ExecutionPolicy</c> is set to <see cref="GameplayExecutionPolicies.EditorPreview"/>
    /// (<c>UseExternalViewManagement = true</c>) so <c>ScreenSizeWidth</c>/<c>Height</c> read the
    /// private size fields set here instead of touching the uninitialized base <c>Game.Window</c>.
    /// </summary>
    private static World BuildWorldWithGame(CasaEngineGame game, int viewportWidth, int viewportHeight)
    {
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        SetPrivateField(game, "_screenSizeWidth", viewportWidth);
        SetPrivateField(game, "_screenSizeHeight", viewportHeight);

        var world = new World { Name = "TestWorld" };
        var gameProperty = typeof(World).GetProperty("Game", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(gameProperty);
        gameProperty!.SetValue(world, game);

        return world;
    }

    private static BackdropRenderer CreateRendererWithOneFactor1Layer()
    {
        var renderer = new BackdropRenderer();

        var scrollar = new BackdropScrollarData
        {
            FactorXNum = 1, FactorXDenom = 1,
            FactorYNum = 1, FactorYDenom = 1,
            ScrollXSpeed = 0, ScrollXPeriod = 0,
            ScrollYSpeed = 0, ScrollYPeriod = 0,
        };

        // An uninitialized Texture2D has 0x0 bounds; the covering-quad tiling runs off the canvas
        // constants, not the texture, so the quads are still queued with their world positions.
        var frames = new[] { CreateTexture() };
        var sortKey = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, frames, 0, sortKey, Color.White, SpriteBlendMode.AlphaBlend);

        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;
        layers.Add(layer);

        return renderer;
    }

    private static List<Vector3> ReadLayerQuadTranslations(SpriteRendererComponent spriteRenderer)
    {
        var result = new List<Vector3>();
        foreach (var entry in GetSpriteDatas(spriteRenderer))
        {
            var matrix = (Matrix)GetField(entry!, "WorldMatrix");
            result.Add(matrix.Translation);
        }

        return result;
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static SpriteRendererComponent CreateSpriteRendererComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());

        // Queuing sprites in DrawSprite never touches the GraphicsDevice, only Flush()/Draw() does -
        // same approach as SpriteRendererComponentBlendModeTests in CasaEngine.Tests.
        return new SpriteRendererComponent(game);
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
