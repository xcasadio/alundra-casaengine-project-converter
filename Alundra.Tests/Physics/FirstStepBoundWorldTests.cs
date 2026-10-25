#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Tests.Ai.Projectiles;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Physics;

/// <summary>
/// E20.e0k5 T2 (docs/plan-e20-heros.md section 1.18.5, phase B, D-E0K5-2): the first steps of an entity born in the game, bounded by the halving search of the binary while the entity is not yet integrated into the world, in the real world of map 14
/// (the real controller, the wall in the column 5 of the synthetic field, the EAST side only: O-E0K5-1) at 1, 2 and 3 ticks per frame, against the keys of the oracle (<c>docs/plan-e20-annexe/e20-f4f6</c>, read with the step by image). Each hero position is the one of
/// the scenario of its key: the arrow at 109 against <c>arrow3_flush</c> (0 px travelled, the flag at S), at 108 against <c>arrow3_point_blank</c> (the free step 111 bounded to 109), at 107 against <c>arrow3_x107</c> (the step 110 bounded to 109 minus one raw unit, 7143423, by the halving),
/// the charged arrow at 105 against <c>charged7_near105</c> and at 109 against <c>charged7_flush</c> (destroyed at S+1, the effect 8); the witness: the arrow at 105 against <c>arrow3_near105</c> (the free step to 108, nothing to bound). The lines are compared from S to S+19.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FirstStepBoundWorldTests
{
    private const int Lines = 20;

    private static Func<int, int, CutBench.Cell> Column(int column) => (c, r) => c == column ? new CutBench.Cell(0x40, 0) : default;

    private static void AssertArrowLines(int heroX, string name, int ticksPerFrame, int count = Lines)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun($"e0k5-{name}-{ticksPerFrame}", 5, Column(5), heroX: heroX);
        run.Go(ticksPerFrame, 40);
        var expected = Enumerable.Range(0, count).Select(k => $"{k,3}: {key.Line(key.FirstTick + k)}");
        var actual = Enumerable.Range(0, count).Select(k => $"{k,3}: {run.Line(run.BirthTick + k)}");
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
    }

    private static void AssertChargedLines(int heroX, string name, int ticksPerFrame)
    {
        var key = ChargedArrowOracle.Load(name, stepByImage: true);
        using var run = new ChargedArrowRun($"e0k5-charged-{name}-{ticksPerFrame}", Column(5), heroX);
        run.Go(ticksPerFrame, 150);
        var expected = Enumerable.Range(0, Lines).Select(k => $"{k,3}: {key.Line(ChargedArrowOracle.Birth + k)}");
        var actual = Enumerable.Range(0, Lines).Select(k => $"{k,3}: {(run.Lines.TryGetValue(run.BirthTick + k, out var line) ? line : "absent")}");
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Arrow_FlushAgainstTheWall_AtHeroX109_TravelsNothing_TheFlagAtS_TheBounceAtSPlus1(int ticksPerFrame) => AssertArrowLines(109, "arrow3_flush", ticksPerFrame);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Arrow_PointBlank_AtHeroX108_TheFreeStep111IsBoundedTo109(int ticksPerFrame) => AssertArrowLines(108, "arrow3_point_blank", ticksPerFrame);

    /// <summary>Three ticks per frame: S, S+1 and S+2 are all unintegrated, so the whole life S to S+19 equals the key.</summary>
    [Fact]
    public void Arrow_AtHeroX107_ThreeTicksPerFrame_TheStep110IsBoundedBelow109ByOneRawUnit_TheHalvingNotTheLargestFreePosition() => AssertArrowLines(107, "arrow3_x107", 3);

    /// <summary>
    /// At one and two ticks per frame S+1 is already integrated and the mover of the engine decides: it raises the wall flag at S+1 where the binary's halving raises it at S+2 (the one-tick flag gap of D-E20-38, "one start in three", kept by D-E0K5-2:
    /// after the integration nothing changes; measured by the T0 probe at one tick, by T2 at two). The position of the birth tick S, the unit 7143423, is the halving's: the key is compared at S only.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Arrow_AtHeroX107_OneAndTwoTicksPerFrame_TheBirthTickIsBoundedBelow109ByOneRawUnit(int ticksPerFrame) => AssertArrowLines(107, "arrow3_x107", ticksPerFrame, count: 1);

    /// <summary>The witness, green before and after the wiring: at one and two ticks per frame the second step (S+1) is already integrated (or reaches nothing the wall refuses), so nothing is bounded.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Arrow_Witness_AtHeroX105_TheFreeStepTo108IsNotBounded(int ticksPerFrame) => AssertArrowLines(105, "arrow3_near105", ticksPerFrame);

    /// <summary>At three ticks per frame S+1 is still unintegrated and its step of 3 px from 108 would end at 111 inside the wall: bounded to 109 (red before the wiring, green after).</summary>
    [Fact]
    public void Arrow_AtHeroX105_ThreeTicksPerFrame_TheSecondStepStillUnintegratedIsBoundedTo109() => AssertArrowLines(105, "arrow3_near105", 3);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ChargedArrow_AtHeroX105_TheFreeStepOf6PxTo111IsBoundedTo109(int ticksPerFrame) => AssertChargedLines(105, "near105", ticksPerFrame);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ChargedArrow_FlushAtHeroX109_IsDestroyedAtSPlus1_WithTheEffect8(int ticksPerFrame) => AssertChargedLines(109, "flush", ticksPerFrame);
}
