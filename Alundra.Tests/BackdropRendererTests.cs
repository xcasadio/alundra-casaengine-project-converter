using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
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

        renderer.Draw(spriteRenderer, cameraPosition: Vector3.Zero, viewportWidth: 320, viewportHeight: 240);

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
    /// the tiles. The defect: Draw fed the RENDER-space camera Y (up-positive) into
    /// ComputeLayerOffset, where the original consumes a WORLD-space scroll (down-positive,
    /// g_cameraScrollingY) - so the vertical parallax term carried the wrong sign and the layer
    /// drifted at TWICE the camera's vertical movement. X was fine (no flip on that axis), which is
    /// why the symptom was vertical-only.
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

        renderer.Draw(spriteRenderer, cameraA, viewportWidth: 320, viewportHeight: 240);
        var quadsA = ReadLayerQuadTranslations(spriteRenderer);
        GetSpriteDatas(spriteRenderer).Clear();

        renderer.Draw(spriteRenderer, cameraB, viewportWidth: 320, viewportHeight: 240);
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
        var texture = CreateTexture();
        var sortKey = new RenderSortKey2D((int)RenderPass2D.Effects, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, texture, sortKey, Color.White, SpriteBlendMode.AlphaBlend);

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
