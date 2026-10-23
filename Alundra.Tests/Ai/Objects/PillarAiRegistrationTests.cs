#nullable enable
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, D-G5-1): the key <c>(C, 76)</c> is registered for the sprite type 430 and for it ALONE. The other sprite types that carry the code 76 in their record (the statue, type 298, among them) are not ported by this slice and stay
/// without a handler. The world tests (<see cref="PillarAiWorld26Tests"/>) show the key at work.
/// </summary>
public sealed class PillarAiRegistrationTests
{
    [Fact]
    public void TheKeyIsC76_AndTheProductionRegistryHoldsItForTheType430()
    {
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 76) }, PillarAi.Keys);

        var production = NativeSpriteRegistry.CreateForProduction();

        Assert.True(production.TryGet(ScriptHelper.ProgramCTick, PillarAi.Code, PillarAi.SpriteType, out var handler));
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData(298)]  // the pushable statue (an event object, all scripted): same code in some records, not this slice.
    [InlineData(229)]  // the block.
    [InlineData(345)]  // the push switch.
    [InlineData(335)]  // the Beannoid.
    [InlineData(7)]    // the charged arrow, whose key is (C, 94).
    [InlineData(0)]
    [InlineData(429)]  // the neighbours of 430.
    [InlineData(431)]
    public void TheKeyIsNotRegisteredForAnyOtherSpriteType(int spriteType)
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        Assert.False(production.TryGet(ScriptHelper.ProgramCTick, PillarAi.Code, spriteType, out _));
    }

    [Fact]
    public void TheRegistrationIsAKeyOfItsOwn_OneMoreThanWithoutIt_AndNoOtherSlotHoldsCode76()
    {
        var registry = new NativeSpriteRegistry();
        PillarAi.Register(registry);

        Assert.Equal(PillarAi.Keys.Count, registry.Count);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 76, 430, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramALoad, 76, 430, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 76, 430, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 75, 430, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 77, 430, out _));
    }
}
