using Xunit;
using Alundra.Rng;
using Alundra.Tests.Maps;

namespace Alundra.Tests.Rng;

/// <summary>
/// D-E9d - <see cref="GameRandom"/>, the original engine's ONE shared random stream, ported
/// verbatim from <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Random.cs</c>. Pins the exact
/// sequence, computed by hand from the LCG (<c>seed = seed * 0x7d2b89dd + 0xe06a02e7 mod 2^64</c>,
/// <c>Next()</c> returns the low 32 bits), so the shared stream is pinned rather than trusted:
///
/// seed0        = 0xB017C93D
/// seed1        = (seed0 * 0x7d2b89dd + 0xe06a02e7) mod 2^64 -> low32 = 0x35E36190 (904094096)
/// seed2        = (seed1 * 0x7d2b89dd + 0xe06a02e7) mod 2^64 -> low32 = 0xC81B4C37 (3357232183)
/// seed3        = (seed2 * 0x7d2b89dd + 0xe06a02e7) mod 2^64 -> low32 = 0xE4013D62 (3825286498)
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class RandomTests
{
    public RandomTests()
    {
        GameRandom.Reset();
    }

    [Fact]
    public void Reset_SetsTheSeedBackToTheOriginalConstant()
    {
        GameRandom.Next();
        Assert.NotEqual(0xB017C93DUL, GameRandom.RandomSeed);

        GameRandom.Reset();

        Assert.Equal(0xB017C93DUL, GameRandom.RandomSeed);
    }

    [Fact]
    public void Next_ReproducesTheOriginalsFirstThreeValues_HandComputedFromTheLcg()
    {
        Assert.Equal(0x35E36190UL, GameRandom.Next());
        Assert.Equal(0xC81B4C37UL, GameRandom.Next());
        Assert.Equal(0xE4013D62UL, GameRandom.Next());
    }

    /// <summary>The multiplication wraps in 64 bits and only the low 32 bits are returned - the raw
    /// 64-bit seed after one step is NOT equal to the returned value (it carries the high bits too).</summary>
    [Fact]
    public void Next_ReturnsOnlyTheLow32BitsOfTheWrappedSeed()
    {
        var returned = GameRandom.Next();

        Assert.True(returned <= uint.MaxValue);
        Assert.Equal((ulong)(uint)GameRandom.RandomSeed, returned);
    }
}
