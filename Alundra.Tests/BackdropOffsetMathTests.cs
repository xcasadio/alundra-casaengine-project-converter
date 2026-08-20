using System.Collections.Generic;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>Covers <see cref="BackdropOffsetMath"/>'s pure formulas (see its class doc and
/// <c>docs/formats/backdrops.md</c>) - the only part of <see cref="BackdropRenderer"/> that is
/// headless-testable; actual rendering and texture loading need a live GraphicsDevice/AssetContentManager
/// and are exercised manually (see the task's validation notes).</summary>
public class BackdropOffsetMathTests
{
    [Theory]
    [InlineData(100f, 1, 1, 100f)]
    [InlineData(100f, 1, 2, 50f)]
    [InlineData(-40f, 1, 2, -20f)]
    [InlineData(100f, 1, 0, 0f)] // zero denominator disables parallax instead of dividing by zero.
    public void ComputeParallaxOffset_AppliesFactorOrDisables(float cameraPosition, int factorNum, int factorDenom, float expected)
    {
        Assert.Equal(expected, BackdropOffsetMath.ComputeParallaxOffset(cameraPosition, factorNum, factorDenom));
    }

    [Theory]
    [InlineData(0, 10, 5, 0f)] // no speed, no period -> stays put.
    [InlineData(1, 0, 10, 10f)] // pure per-tick speed, no period contribution.
    [InlineData(1, 10, 10, 11f)] // speed*10 (=10) plus one extra pixel every 10 ticks (=1).
    [InlineData(1, -10, 10, 9f)] // period negative -> extra pixel direction flips (sign XOR).
    [InlineData(-1, 10, 10, -11f)] // speed negative, period positive -> extra pixel also negative.
    [InlineData(-1, -10, 10, -9f)] // both negative -> XOR is false -> extra pixel positive.
    public void ComputeAutoScrollOffset_AdvancesBySpeedPlusPeriodStep(int speed, int period, long tickCount, float expected)
    {
        Assert.Equal(expected, BackdropOffsetMath.ComputeAutoScrollOffset(speed, period, tickCount));
    }

    [Theory]
    [InlineData(0f, 640, 0f)]
    [InlineData(640f, 640, 0f)]
    [InlineData(700f, 640, 60f)]
    [InlineData(-10f, 640, 630f)]
    [InlineData(-650f, 640, 630f)]
    public void WrapOffset_WrapsIntoCanvasRange(float value, int canvasSize, float expected)
    {
        Assert.Equal(expected, BackdropOffsetMath.WrapOffset(value, canvasSize));
    }

    [Fact]
    public void ComputeLayerOffset_CombinesParallaxAndAutoScroll_ThenWraps()
    {
        // cameraX=1280 * 1/1 = 1280 (parallax) + autoscroll(speed=0,period=10,ticks=605)=60 -> 1340, wrapped mod 640 = 60.
        var offset = BackdropOffsetMath.ComputeLayerOffset(
            cameraPosition: 1280f, factorNum: 1, factorDenom: 1, speed: 0, period: 10, tickCount: 605, canvasSize: 640);

        Assert.Equal(60f, offset);
    }

    [Theory]
    [InlineData(320, 0f)]
    [InlineData(320, 100f)]
    [InlineData(320, 639f)]
    [InlineData(640, 0f)]
    [InlineData(1000, 0f)]
    [InlineData(1000, 500f)]
    public void ComputeCoveringQuadOrigins_CoversViewportWithNoGaps(int viewportWidth, float offsetX)
    {
        const int viewportHeight = 240;
        const float offsetY = 0f;

        var origins = BackdropOffsetMath.ComputeCoveringQuadOrigins(viewportWidth, viewportHeight, offsetX, offsetY);

        // Every horizontal position in [0, viewportWidth) must be covered by exactly one quad's
        // [origin.X, origin.X + tileWidth) interval - i.e. adjacent tiling with no gap and no overlap
        // across tile boundaries.
        var xs = new SortedSet<int>();
        foreach (var origin in origins)
        {
            xs.Add(origin.X);
        }

        Assert.True(xs.Count > 0);

        var covered = new bool[viewportWidth];
        foreach (var x in xs)
        {
            for (var px = x; px < x + BackdropOffsetMath.CanvasWidth; px++)
            {
                if (px >= 0 && px < viewportWidth)
                {
                    covered[px] = true;
                }
            }
        }

        for (var px = 0; px < viewportWidth; px++)
        {
            Assert.True(covered[px], $"pixel {px} not covered by any quad");
        }
    }

    [Fact]
    public void ComputeCoveringQuadOrigins_CrossesXAndY()
    {
        var origins = BackdropOffsetMath.ComputeCoveringQuadOrigins(1000, 700, 100f, 50f);

        // 1000-wide viewport needs 2 tiles horizontally, 700-tall needs 2 tiles vertically -> 4 quads.
        Assert.Equal(4, origins.Count);
    }

    [Fact]
    public void ComputeCoveringOrigins1D_ZeroOffset_StartsAtZero()
    {
        var origins = BackdropOffsetMath.ComputeCoveringOrigins1D(640, 0f, 640);

        Assert.Single(origins);
        Assert.Equal(0, origins[0]);
    }
}
