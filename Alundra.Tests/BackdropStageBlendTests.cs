using Alundra.Scripts;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S2") - T8 (docs/plan-e10-fondu.md, slice E10.b,
/// §1.8), the original's own backdrop blend mapping, moved here from the now-retired
/// <c>BackdropRendererTests</c> unedited (<see cref="AlundraBackdropStage.ResolveGroundLayerBlend"/>
/// kept its signature UNCHANGED when D-E9b-8 moved the definition onto the stage).
/// </summary>
public class BackdropStageBlendTests
{
    [Fact]
    public void ResolveGroundLayerBlend_MapsAllFourGroundBlendModes_ExactBlendAndTintPairs()
    {
        // 1 = Average, unchanged from before this slice.
        var (blend1, tint1) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: true, blendMode: 1);
        Assert.Equal(SpriteBlendMode.AlphaBlend, blend1);
        Assert.Equal(new Color(255, 255, 255, 128), tint1);

        // 2 = Additive, white.
        var (blend2, tint2) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: true, blendMode: 2);
        Assert.Equal(SpriteBlendMode.Additive, blend2);
        Assert.Equal(Color.White, tint2);

        // 3 = Subtractive, white.
        var (blend3, tint3) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: true, blendMode: 3);
        Assert.Equal(SpriteBlendMode.Subtractive, blend3);
        Assert.Equal(Color.White, tint3);

        // 4 = Additive, tint (63,63,63) - the 0.247 vs 0.25 quantization gap documented on the method.
        var (blend4, tint4) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: true, blendMode: 4);
        Assert.Equal(SpriteBlendMode.Additive, blend4);
        Assert.Equal(new Color(63, 63, 63), tint4);
    }

    [Fact]
    public void ResolveGroundLayerBlend_GroundFalseBlendMode1_StaysOpaque_OutOfScopeBucketUntouched()
    {
        // The deliberately untouched bucket (§1.8): (Ground=false, BlendMode 1) x34 on the export - the
        // original gates this one per-pixel on the STP bit (unanalyzed) - must stay Opaque.
        var (blend, tint) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: false, blendMode: 1);
        Assert.Equal(SpriteBlendMode.Opaque, blend);
        Assert.Equal(Color.White, tint);
    }

    [Fact]
    public void ResolveGroundLayerBlend_UnknownGroundBlendMode_FallsBackToOpaqueWhite()
    {
        var (blend, tint) = AlundraBackdropStage.ResolveGroundLayerBlend(ground: true, blendMode: 99);
        Assert.Equal(SpriteBlendMode.Opaque, blend);
        Assert.Equal(Color.White, tint);
    }
}
