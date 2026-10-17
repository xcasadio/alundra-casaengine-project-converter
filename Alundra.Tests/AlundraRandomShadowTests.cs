#nullable enable
using System;
using Xunit;
using Alundra.Rng;
using Alundra.Probes;

namespace Alundra.Tests;

/// <summary>
/// E20.e0b1 T1 (docs/plan-e20-heros.md section 1.17.1, annex docs/plan-e20-annexe/e20-e0b/README.md): the measuring sink of the per-tick advance (<see cref="AlundraRandomShadow"/>) is inert without its
/// environment variable. It says NOTHING about <see cref="GameRandom.RandomSeed"/> (the real state, which phase B makes advance): it only looks at the sink, so it is the same test in phase A and in phase B.
/// It is a no-op when the sink is active (the suite launched with <c>ALUNDRA_RNG_SHADOW_LOG</c> to measure), where the sink is supposed to record.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraRandomShadowTests
{
    public AlundraRandomShadowTests()
    {
        GameRandom.Reset();
    }

    [Fact]
    public void WithoutItsVariable_TheSinkWritesNoFile_AllocatesNothing_AndMovesNeitherTheVirtualWordNorTheCounters()
    {
        if (AlundraRandomShadow.Active)
        {
            return; // the measuring run: the sink is on on purpose.
        }

        // Warm-up: the first calls compile the paths measured below.
        GameRandom.AdvanceFrame();
        GameRandom.Next();
        _ = GameRandom.RandomSeed;

        var word = AlundraRandomShadow.VirtualWord;
        var events = AlundraRandomShadow.EventCount;
        var lines = AlundraRandomShadow.LinesWritten;
        var allocated = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            GameRandom.AdvanceFrame();
            GameRandom.Next();
        }

        Assert.Equal(allocated, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(word, AlundraRandomShadow.VirtualWord);
        Assert.Equal(events, AlundraRandomShadow.EventCount);
        Assert.Equal(lines, AlundraRandomShadow.LinesWritten);
        Assert.Equal(0, lines);
        Assert.Equal(0, events);
    }
}
