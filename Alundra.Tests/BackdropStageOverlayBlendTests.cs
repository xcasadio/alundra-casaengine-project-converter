using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.g G2d (rule G2d-R3, O-E19-58, ADR-0037): the overlay tint of a map is drawn with the PSX mode of its <c>BGColorA</c> byte
/// (<c>OverlayBlendMode</c> in the companion): 1 average, 2 additive, 3 subtractive, 4 quarter, and the colour is handed to the
/// engine opaque (the mode decides, the engine bakes the alpha of the average). The tests on <c>alundra-project/</c> FAIL, rather
/// than skip, when it is absent (the local convention of <see cref="BackdropStageDefinitionTests"/>); the read of
/// <c>OverlayBlendMode</c> 2 on the burning Inoa is the guard that fails loudly on an export that predates G2d.
/// </summary>
public class BackdropStageOverlayBlendTests
{
    private static readonly RenderSortKey2D TintSortKey = new((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

    // -----------------------------------------------------------------------------------------
    // Real companions.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RealMap293_TheBurningInoa_HasAnAdditiveOverlay_OfColor50_0_0()
    {
        var document = LoadRealCompanion("Inoa (burning)-293");

        // The guard: an export that predates G2d has no such field.
        Assert.Equal(2, document.OverlayBlendMode);

        var (_, tint, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.NotNull(tint);
        Assert.Equal(SpritePsxSemiTransparency.Mode1, tint!.Value.PsxSemiTransparency);
        Assert.Equal(new Color(50, 0, 0, 255), tint.Value.Color);
        Assert.Equal(TintSortKey, tint.Value.SortKey);
    }

    [Fact]
    public void RealMap96_AnOverlayOfBGColorA1_IsTheAverage_OfColor40_40_40()
    {
        var document = LoadRealCompanion("Unused Nightmare-96");

        Assert.Equal(1, document.OverlayBlendMode);

        var (_, tint, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.NotNull(tint);
        Assert.Equal(SpritePsxSemiTransparency.Mode0, tint!.Value.PsxSemiTransparency);
        Assert.Equal(new Color(40, 40, 40, 255), tint.Value.Color);
    }

    // -----------------------------------------------------------------------------------------
    // Synthetic documents.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, SpritePsxSemiTransparency.Mode0)]
    [InlineData(2, SpritePsxSemiTransparency.Mode1)]
    [InlineData(3, SpritePsxSemiTransparency.Mode2)]
    [InlineData(4, SpritePsxSemiTransparency.Mode3)]
    [InlineData(0, SpritePsxSemiTransparency.Mode0)] // an export that predates the field: today's drawing
    [InlineData(5, SpritePsxSemiTransparency.Mode0)] // no map has it: the binary would read past its table
    public void OverlayBlendMode_PicksTheModeOfTheTint(int overlayBlendMode, SpritePsxSemiTransparency expected)
    {
        var document = new BackdropDocument
        {
            Enabled = true,
            OverlayEnabled = true,
            OverlayColorR = 50,
            OverlayColorG = 0,
            OverlayColorB = 0,
            OverlayBlendMode = overlayBlendMode,
        };

        var (_, tint, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.NotNull(tint);
        Assert.Equal(expected, tint!.Value.PsxSemiTransparency);
        Assert.Equal(new Color(50, 0, 0, 255), tint.Value.Color);
    }

    [Fact]
    public void WithoutTheOverlayGate_ThereIsNoTint_WhateverTheBlendMode()
    {
        var document = new BackdropDocument { Enabled = true, OverlayEnabled = false, OverlayBlendMode = 2 };

        var (_, tint, _) = AlundraBackdropStage.BuildDefinitions(document);

        Assert.Null(tint);
    }

    [Fact]
    public void BackdropDocument_DeserializesTheOverlayBlendMode_AndDefaultsToZeroWhenAbsent()
    {
        var withField = JsonSerializer.Deserialize<BackdropDocument>(
            """{ "MapIndex": 293, "Enabled": true, "OverlayEnabled": true, "OverlayBlendMode": 2, "Layers": [] }""");
        var withoutField = JsonSerializer.Deserialize<BackdropDocument>(
            """{ "MapIndex": 4, "Enabled": true, "OverlayEnabled": true, "Layers": [] }""");

        Assert.NotNull(withField);
        Assert.NotNull(withoutField);
        Assert.Equal(2, withField!.OverlayBlendMode);
        Assert.Equal(0, withoutField!.OverlayBlendMode);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    private static BackdropDocument LoadRealCompanion(string worldName)
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
        var document = BackdropLoader.Load(projectRoot, worldName);
        Assert.NotNull(document);
        return document!;
    }
}
