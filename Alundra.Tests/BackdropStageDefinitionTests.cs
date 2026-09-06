#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S1") - covers
/// <see cref="AlundraBackdropStage.BuildDefinitions"/>, the PURE translation of a loaded
/// <see cref="BackdropDocument"/> into the engine mechanism's own
/// <see cref="ScrollingLayerDefinition"/>/<see cref="ScrollingTintDefinition"/>/
/// <see cref="ScrollingLayerConfiguration"/> data, applying exactly the rules
/// <see cref="BackdropRenderer.Load"/> applies today (D-E9b-8). Nothing here calls
/// <see cref="AlundraBackdropStage.AttachService"/>/<see cref="AlundraBackdropStage.PushFrame"/> or
/// touches production - see <see cref="BackdropEquivalenceTests"/> for the tick-by-tick comparison
/// against the retired renderer.
///
/// The three real-companion tests read <c>alundra-project/</c> (the converter's own export) and FAIL,
/// rather than skip, when it is absent - the local convention documented at
/// <see cref="SpriteRecordCatalogTests"/>'s own <c>FindProjectRoot</c>, reused here via
/// <see cref="AlundraWorldProxyGlobalFreezeTests.FindProjectRoot"/> (same "no 'alundra-project/Maps'
/// directory found" message).
/// </summary>
public class BackdropStageDefinitionTests
{
    // -----------------------------------------------------------------------------------------
    // Real companions - exact translation of 389, 159, 321.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BuildDefinitions_RealMap389_TranslatesTheSingleGroundAverageLayer_DisabledLayerAbsent()
    {
        var document = LoadRealCompanion("Ship Klark (beginning)-389", "The Klark");

        var (layers, tint, configuration) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Single(layers); // the companion's second layer is Mode "Disabled" - must be absent.
        var layer = layers[0];

        Assert.Equal(new[] { Guid.Parse("b97a0c58-e83b-57fa-a653-058dcc6d5ecc") }, layer.FrameTextureAssetIds);
        Assert.Equal(1, layer.FactorXNum);
        Assert.Equal(1, layer.FactorXDenom);
        Assert.Equal(1, layer.FactorYNum);
        Assert.Equal(1, layer.FactorYDenom);
        Assert.Equal(0, layer.ScrollXSpeed);
        Assert.Equal(10, layer.ScrollXPeriod);
        Assert.Equal(0, layer.ScrollYSpeed);
        Assert.Equal(5, layer.ScrollYPeriod);
        Assert.Equal(1, layer.AnimTimer);
        Assert.Equal(RenderPass2D.Effects, layer.Pass); // Ground = true.
        Assert.Equal(0, layer.SortingLayer);
        Assert.Equal(1, layer.OrderInLayer); // DepthOrder.
        Assert.Equal(0, layer.StableId); // LayerId.
        Assert.Equal(SpriteBlendMode.AlphaBlend, layer.Blend); // Ground=true, BlendMode 1 (Average).
        Assert.Equal(new Color(255, 255, 255, 128), layer.Tint);

        Assert.Null(tint); // OverlayEnabled is false.
        AssertFixedConfiguration(configuration);
    }

    [Fact]
    public void BuildDefinitions_RealMap159_TranslatesTheAnimatedGroundAdditiveLayer_DisabledLayerAbsent()
    {
        var document = LoadRealCompanion("Fairy cave (underwater)-159", "Fairy cave");

        var (layers, tint, configuration) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Single(layers);
        var layer = layers[0];

        Assert.Equal(
            new[]
            {
                Guid.Parse("324a813b-c5eb-5731-badd-b58ca305ba91"),
                Guid.Parse("ec92743f-bf00-53aa-9520-49c695538e0c"),
                Guid.Parse("f06dc309-b419-5045-accb-376585dc1d42"),
                Guid.Parse("4474253a-d70e-5320-b6cb-95c85e84ee7a"),
            },
            layer.FrameTextureAssetIds);
        Assert.Equal(0, layer.FactorXNum);
        Assert.Equal(1, layer.FactorXDenom);
        Assert.Equal(0, layer.FactorYNum);
        Assert.Equal(1, layer.FactorYDenom);
        Assert.Equal(0, layer.ScrollXSpeed);
        Assert.Equal(0, layer.ScrollXPeriod);
        Assert.Equal(0, layer.ScrollYSpeed);
        Assert.Equal(0, layer.ScrollYPeriod);
        Assert.Equal(6, layer.AnimTimer);
        Assert.Equal(RenderPass2D.Effects, layer.Pass); // Ground = true.
        Assert.Equal(0, layer.SortingLayer);
        Assert.Equal(1, layer.OrderInLayer);
        Assert.Equal(0, layer.StableId);
        Assert.Equal(SpriteBlendMode.Additive, layer.Blend); // Ground=true, BlendMode 2.
        Assert.Equal(Color.White, layer.Tint);

        Assert.Null(tint);
        AssertFixedConfiguration(configuration);
    }

    [Fact]
    public void BuildDefinitions_RealMap321_TranslatesTheAnimatedNonGroundParallaxLayer_DisabledLayerAbsent()
    {
        var document = LoadRealCompanion("Arena Zorgia (Boss)-321", "Arena Zorgia");

        var (layers, tint, configuration) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Single(layers);
        var layer = layers[0];

        Assert.Equal(
            new[]
            {
                Guid.Parse("586c6a0e-866b-5af8-bd8e-52c72d14434f"),
                Guid.Parse("35c8f4b3-5eaa-5d5b-a0bf-60e603b9a824"),
                Guid.Parse("18231114-cf8c-5617-9991-4911b0e55301"),
                Guid.Parse("011612b9-571c-5e3e-8640-170e27994669"),
            },
            layer.FrameTextureAssetIds);
        Assert.Equal(1, layer.FactorXNum);
        Assert.Equal(8, layer.FactorXDenom);
        Assert.Equal(1, layer.FactorYNum);
        Assert.Equal(8, layer.FactorYDenom);
        Assert.Equal(2, layer.ScrollXSpeed);
        Assert.Equal(0, layer.ScrollXPeriod);
        Assert.Equal(2, layer.ScrollYSpeed);
        Assert.Equal(0, layer.ScrollYPeriod);
        Assert.Equal(4, layer.AnimTimer);
        Assert.Equal(RenderPass2D.Background, layer.Pass); // Ground = false.
        Assert.Equal(0, layer.SortingLayer);
        Assert.Equal(1, layer.OrderInLayer);
        Assert.Equal(0, layer.StableId);
        // Ground=false, BlendMode 0: the deliberately untouched bucket - stays Opaque/white.
        Assert.Equal(SpriteBlendMode.Opaque, layer.Blend);
        Assert.Equal(Color.White, layer.Tint);

        Assert.Null(tint);
        AssertFixedConfiguration(configuration);
    }

    // -----------------------------------------------------------------------------------------
    // Synthetic documents - filtering and guards.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BuildDefinitions_DisabledLayer_IsFiltered()
    {
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Disabled",
                    Ground = true,
                    Scrollar = new BackdropScrollarData { FactorXDenom = 1, FactorYDenom = 1 },
                    TextureAssetId = Guid.NewGuid().ToString(),
                },
            },
        };

        var (layers, _, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Empty(layers);
    }

    [Fact]
    public void BuildDefinitions_TilesLayerWithNullScrollarOrEmptyTextureAssetId_BothAbsent_NoException()
    {
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Tiles",
                    Ground = true,
                    Scrollar = null, // guard 1: null Scrollar.
                    TextureAssetId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    LayerId = 1,
                    Mode = "Tiles",
                    Ground = true,
                    Scrollar = new BackdropScrollarData { FactorXDenom = 1, FactorYDenom = 1 },
                    TextureAssetId = "", // guard 2: empty TextureAssetId.
                },
            },
        };

        var exception = Record.Exception(() => AlundraBackdropStage.BuildDefinitions(document));

        Assert.Null(exception);
        var (layers, _, _) = AlundraBackdropStage.BuildDefinitions(document);
        Assert.Empty(layers);
    }

    [Fact]
    public void BuildDefinitions_UnparsableFrameAssetIdAtIndexOneOrMore_BecomesGuidEmpty_ResolvesToFrameZeroOnly()
    {
        var frame0Id = Guid.NewGuid();
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Tiles",
                    Ground = true,
                    Scrollar = new BackdropScrollarData { FactorXDenom = 1, FactorYDenom = 1 },
                    TextureAssetId = frame0Id.ToString(),
                    FrameTextureAssetIds = new[] { frame0Id.ToString(), "not-a-guid" },
                },
            },
        };

        var (layers, _, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Single(layers);
        // Same LENGTH as BackdropRenderer.LoadLayerFrames' input (D-E9-9 fallback runs engine-side,
        // not here) - the unparsable id becomes Guid.Empty, never shortening the array.
        Assert.Equal(new[] { frame0Id, Guid.Empty }, layers[0].FrameTextureAssetIds);

        var frame0Texture = CreateTexture();
        var component = CreateHeadlessScrollingLayerComponent();
        component.Service.SetConfiguration(new ScrollingLayerConfiguration(640, 480, 320, 240));
        component.Service.SetLayers(layers);
        component.ResolveTextures(id => id == frame0Id ? frame0Texture : null!);

        var resolvedFrames = GetResolvedLayerFrames(component, layerIndex: 0);

        // Same count as BackdropRenderer.LoadLayerFrames gives today for the same entry: frame 1
        // fails to load (Guid.Empty never reaches the loader, resolves to null) -> falls back to
        // exactly [frame0].
        Assert.Equal(new[] { frame0Texture }, resolvedFrames);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    private static void AssertFixedConfiguration(ScrollingLayerConfiguration configuration)
    {
        Assert.Equal(640, configuration.CanvasWidth);
        Assert.Equal(480, configuration.CanvasHeight);
        Assert.Equal(320, configuration.ViewWidth);
        Assert.Equal(240, configuration.ViewHeight);
    }

    private static BackdropDocument LoadRealCompanion(string worldName, string zoneFolderIgnored)
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
        var document = BackdropLoader.Load(projectRoot, worldName);
        Assert.NotNull(document);
        return document!;
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    /// <summary>Same headless montage shape as <c>BackdropRendererTests.CreateSpriteRendererComponent</c>
    /// - an uninitialized <see cref="CasaEngineGame"/> with a real <see cref="GameComponentCollection"/>,
    /// just enough for <see cref="ScrollingLayerComponent"/>'s constructor (which adds itself to
    /// <c>game.Components</c>) to run without touching a live <c>GraphicsDevice</c>.</summary>
    private static ScrollingLayerComponent CreateHeadlessScrollingLayerComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());

        return new ScrollingLayerComponent(game);
    }

    private static Texture2D[] GetResolvedLayerFrames(ScrollingLayerComponent component, int layerIndex)
    {
        var field = typeof(ScrollingLayerComponent).GetField("_layerFrames", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var layerFrames = (Texture2D[][])field!.GetValue(component)!;
        return layerFrames[layerIndex];
    }
}
