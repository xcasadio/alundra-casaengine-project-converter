#nullable enable
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Enemies;
using Alundra.Ai.Objects;
using Alundra.Ai.Projectiles;
using Alundra.Events;
using Alundra.Session;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g1a T4 (docs/plan-e20-heros.md section 1.24.1, phase C, (r1)): the production registry holds the key <c>(C, 23)</c> for the seven liftable types of <see cref="LiftableAi.SpriteTypes"/> and for them ALONE. Written before the line of
/// <see cref="NativeSpriteRegistry.CreateForProduction"/> that registers it. The world test of the maps of crates at rest (r2) is <see cref="LiftableAiMap392Tests"/>.
/// </summary>
public sealed class LiftableAiRegistrationTests
{
    [Fact]
    public void R1_TheProductionRegistryHoldsC23_ForEachOfTheSevenTypes()
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        foreach (var type in LiftableAi.SpriteTypes)
        {
            Assert.True(production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, type, out var handler), $"type {type}");
            Assert.NotNull(handler);
        }
    }

    [Theory]
    [InlineData(233)]
    [InlineData(235)]
    [InlineData(290)]   // the neighbours of the seven types.
    [InlineData(293)]
    [InlineData(298)]   // the pushable statue.
    [InlineData(303)]
    [InlineData(305)]
    [InlineData(437)]
    [InlineData(439)]
    [InlineData(430)]   // the pillar, whose key is (C, 76).
    [InlineData(7)]     // the charged arrow.
    [InlineData(335)]   // the Beannoid.
    [InlineData(0)]
    public void R1_TheKeyIsNotRegisteredForAnyOtherSpriteType(int spriteType)
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        Assert.False(production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, spriteType, out _));
    }

    [Fact]
    public void R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses()
    {
        var production = NativeSpriteRegistry.CreateForProduction();
        foreach (var type in LiftableAi.SpriteTypes)
        {
            Assert.False(production.TryGet(ScriptHelper.ProgramALoad, LiftableAi.Code, type, out _), $"A23, type {type}");
            Assert.False(production.TryGet(ScriptHelper.ProgramDTouch, LiftableAi.Code, type, out _), $"D23, type {type}");
            Assert.False(production.TryGet(ScriptHelper.ProgramEDeactivate, LiftableAi.Code, type, out _), $"E23, type {type}"); // E2 is g2b's, and E has no key 23.
            Assert.False(production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code + 1, type, out _), $"C24, type {type}");
        }

        var sum = new NativeSpriteRegistry();
        BeannoideAi.Register(sum);
        MurutaAi.Register(sum);
        WaveSwordAi.Register(sum);
        PickupAi.Register(sum);
        ChargedArrowAi.Register(sum);
        ArrowAi.Register(sum);
        PillarAi.Register(sum);
        LiftableAi.Register(sum);
        LiftableAi.RegisterBreak(sum);

        Assert.Equal(sum.Count, production.Count);
        Assert.Equal(3 + 3 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count + PillarAi.Keys.Count + LiftableAi.Keys.Count + LiftableAi.BreakKeys.Count, production.Count);
    }

    [Fact]
    public void R1_TheRunnerOfProduction_HoldsTheKeyToo()
    {
        var runner = new EventProgramRunner(null, GameState.Instance);

        Assert.All(LiftableAi.SpriteTypes, type => Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, type, out _), $"type {type}"));
    }
}
