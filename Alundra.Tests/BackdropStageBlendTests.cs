using Alundra.Scripts;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.g G2c (rule G2c-R3, D-E19-68, ADR-0035) - the backdrop layers render the per-texel PSX semi-transparency like the original
/// binary: the mode of a layer is <c>BlendMode - 1</c> (the primitive of mode set at the head of its slot, <c>0x8005B958</c>-<c>0x8005B9E4</c>),
/// <c>Ground</c> only chooses the pass. Replaces the E10.b mapping of <c>ResolveGroundLayerBlend</c> (one blend per layer, gated on
/// <c>Ground</c>), whose three tests became these three.
/// </summary>
public class BackdropStageBlendTests
{
    [Fact]
    public void ResolveLayerPsxSemiTransparency_MapsBlendModes1To4_ToTheFourPsxModes()
    {
        Assert.Equal(SpritePsxSemiTransparency.Mode0, AlundraBackdropStage.ResolveLayerPsxSemiTransparency(1)); // average
        Assert.Equal(SpritePsxSemiTransparency.Mode1, AlundraBackdropStage.ResolveLayerPsxSemiTransparency(2)); // additive
        Assert.Equal(SpritePsxSemiTransparency.Mode2, AlundraBackdropStage.ResolveLayerPsxSemiTransparency(3)); // subtractive
        Assert.Equal(SpritePsxSemiTransparency.Mode3, AlundraBackdropStage.ResolveLayerPsxSemiTransparency(4)); // quarter
    }

    [Theory]
    [InlineData(0)] // the layer is not semi-transparent
    [InlineData(99)] // the binary would read a table past its end: no case in the corpus
    [InlineData(-1)]
    public void ResolveLayerPsxSemiTransparency_ZeroAndUnknownBlendModes_AreNone(int blendMode)
    {
        Assert.Equal(SpritePsxSemiTransparency.None, AlundraBackdropStage.ResolveLayerPsxSemiTransparency(blendMode));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildDefinitions_TheModeFollowsTheBlendModeAlone_AndTheLayerIsOpaqueWhiteWhateverGround(bool ground)
    {
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Tiles",
                    Ground = ground,
                    BlendMode = 1,
                    Scrollar = new BackdropScrollarData { FactorXDenom = 1, FactorYDenom = 1 },
                    TextureAssetId = Guid.NewGuid().ToString(),
                },
                new()
                {
                    LayerId = 1,
                    Mode = "Cellular",
                    Ground = ground,
                    BlendMode = 4,
                    Cellular = new BackdropCellularData(),
                },
            },
        };

        var (scrolling, _, _) = AlundraBackdropStage.BuildDefinitions(document);
        var cellular = AlundraBackdropStage.BuildCellularDefinitions(document);

        var tiles = Assert.Single(scrolling);
        Assert.Equal(SpritePsxSemiTransparency.Mode0, tiles.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, tiles.Blend);
        Assert.Equal(Color.White, tiles.Tint);
        Assert.Equal(ground ? RenderPass2D.Effects : RenderPass2D.Background, tiles.Pass);

        var cells = Assert.Single(cellular);
        Assert.Equal(SpritePsxSemiTransparency.Mode3, cells.PsxSemiTransparency);
        Assert.Equal(SpriteBlendMode.Opaque, cells.Blend);
        Assert.Equal(Color.White, cells.Tint);
    }
}
