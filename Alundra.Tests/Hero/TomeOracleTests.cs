#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.h4a T2 (docs/plan-e20-heros.md section 1.31.1, the oracle tests (o1) and (o3)): the eight traces of the swim oracle where the hero floats in deep water with a tome equipped (<c>W9-item2B</c> to <c>W9-item32</c>: the Circle at the
/// tick 10 is refused by the animation 0x1D, the sound 3 and nothing else), and the start of the cast of <c>trace_cast.py 0x2B</c> (<c>docs/plan-e20-annexe/e20-h4a/rerun/trace_cast_2B.txt</c>: the magic 4 to 3, the animation 0x32 and the
/// object in progress at the tick 1, the animation 0x33 and the counter 32 at the tick 33) with a driver of the test that answers 0. The start of the tomes is installed by the seam (D-H-4).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TomeOracleTests : IDisposable
{
    public TomeOracleTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
    }

    /// <summary>The float of the oracle is refused at the press with the sound 3 and the trace is the oracle's, field by field (the rows, the sounds); with the stub of another slice there would be no sound.</summary>
    [Theory]
    [InlineData("w9/W9-item2B")]
    [InlineData("w9/W9-item2C")]
    [InlineData("w9/W9-item2D")]
    [InlineData("w9/W9-item2E")]
    [InlineData("w9/W9-item2F")]
    [InlineData("w9/W9-item30")]
    [InlineData("w9/W9-item31")]
    [InlineData("w9/W9-item32")]
    public void O1_TheTomeInDeepWater_IsRefusedWithTheSound3_AsTheOracle(string name)
    {
        PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
        var trace = RunOracleTrace.Load(name);
        var montage = RunMontage.Start(trace);
        montage.Play();

        RunMontage.AssertEqualToOracle(montage.CompareRows(int.MaxValue, 1, null, null, 16, 0, 0, null, compareEndCounter: true));

        Assert.Contains(montage.Rows, r => r.Tick == 10 && r.Sounds.Contains(3));
        Assert.Equal(1, montage.Rows.Sum(r => r.Sounds.Count(s => s == 3)));
        Assert.All(montage.Rows.Skip(1), r => Assert.Equal(0x1Du, r.AnimTarget));
    }

    /// <summary>The start of the cast, tick by tick: the tick 1 is the press (the magic 4 to 3, 0x32, the object 0x2B in progress, the counter 0), the counter is the tick minus 1, the tick 33 holds 0x33 with the counter 32.</summary>
    [Fact]
    public void O3_TheBeginningOfTheCastOf0x2B_IsTheTraceOfTheOracle()
    {
        using var bench = new TomeBench("o3", 4, 4, 0x2B);
        PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
        PlayerManager.TomeTrackCallsForTests = new List<(uint, byte)>();
        PlayerManager.ItemDriverForTests = (_, _) => 0;
        PlayerManager.SetCurrentItemId(GameState.Instance, 0x2B);

        for (var tick = 1; tick <= 33; tick++)
        {
            bench.Tick(tick == 1 ? TomeBench.Circle : 0u);
            var hero = bench.Hero;
            Assert.Equal(0x2Bu, hero.ItemInUse);
            Assert.Equal(0, hero.ItemUsePhase);
            Assert.Equal(3, TomeBench.Stats.Mp);
            Assert.Equal(tick - 1, hero.ItemUseCounter); // tick 1: 0; tick 33: 32.
            Assert.True(hero.TargetAnimationId == (tick < 33 ? 0x32u : 0x33u), $"tick {tick}: the animation is {hero.TargetAnimationId:X}");
        }

        Assert.Empty(bench.Sounds.Requests);
    }
}
