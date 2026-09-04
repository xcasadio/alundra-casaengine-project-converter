using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> (docs/plan-e9-backdrops-residus.md
/// §2/§3, D-E9-1) - the sole conversion from the render-space camera <c>Target</c> back to the
/// original's own <c>g_cameraScrollingX/Y</c> scroll space. Pure, headless-testable.
/// </summary>
public class AlundraCameraMathTests
{
    [Fact]
    public void ToOriginalScrollSpace_MapOrigin_IsZeroZero()
    {
        var (x, y) = AlundraCameraMath.ToOriginalScrollSpace(new Vector3(160f, -120f, 0f));

        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    /// <summary>The 389's own frozen far-corner bound (E5): render <c>(1087, -839)</c> -&gt; scroll
    /// <c>(0x39f, 0x2cf)</c>. An inverted Y sign in <c>ToOriginalScrollSpace</c> would fail this
    /// (docs/plan-e9-backdrops-residus.md §3, B1 mutation table).</summary>
    [Fact]
    public void ToOriginalScrollSpace_Map389FarCorner_MatchesFrozenScrollBounds()
    {
        var (x, y) = AlundraCameraMath.ToOriginalScrollSpace(new Vector3(1087f, -839f, 0f));

        Assert.Equal(0x39f, x);
        Assert.Equal(0x2cf, y);
    }
}
