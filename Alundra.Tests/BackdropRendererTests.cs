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
