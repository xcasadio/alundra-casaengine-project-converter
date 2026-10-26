#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Events;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T1, (r1) (docs/plan-e20-heros.md section 1.24.5, D-G2C3-1): the keys <c>(A, 19)</c> and <c>(C, 71)</c> are registered for the sprite type 391 (the drop of water) and for it ALONE, in the production registry, next to the keys of the torch <c>(E, 20)</c> for 393 (a torch put out by a drop needs its E20 to go).
/// Red until the phase B registers the class; the world tests of T2 show the keys at work.
/// </summary>
public sealed class WaterDropAiRegistrationTests
{
    private static int[] TypesHolding(NativeSpriteRegistry registry, int slot, int code)
        => Enumerable.Range(0, 512).Where(type => registry.TryGet(slot, code, type, out _)).ToArray();

    [Fact]
    public void TheKeysAreA19AndC71_OnTheType391Alone()
    {
        Assert.Equal(new[] { (ScriptHelper.ProgramALoad, 19), (ScriptHelper.ProgramCTick, 71) }, WaterDropAi.Keys);
        Assert.Equal(391, WaterDropAi.SpriteType);
        Assert.Equal(TorchAi.WaterDropSpriteType, WaterDropAi.SpriteType);

        var registry = new NativeSpriteRegistry();
        WaterDropAi.Register(registry);

        Assert.Equal(WaterDropAi.Keys.Count, registry.Count);
        Assert.Equal(new[] { 391 }, TypesHolding(registry, ScriptHelper.ProgramALoad, 19));
        Assert.Equal(new[] { 391 }, TypesHolding(registry, ScriptHelper.ProgramCTick, 71));
        Assert.Empty(TypesHolding(registry, ScriptHelper.ProgramCTick, 19));
        Assert.Empty(TypesHolding(registry, ScriptHelper.ProgramALoad, 71));
        Assert.Empty(TypesHolding(registry, ScriptHelper.ProgramDTouch, 71));
    }

    [Fact]
    public void R1_TheProductionRegistryHoldsA19AndC71ForExactlyThe391_AndE20ForExactlyThe393()
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        Assert.Equal(new[] { 391 }, TypesHolding(production, ScriptHelper.ProgramALoad, 19));
        Assert.Equal(new[] { 391 }, TypesHolding(production, ScriptHelper.ProgramCTick, 71));
        Assert.Equal(new[] { 393 }, TypesHolding(production, ScriptHelper.ProgramEDeactivate, 20));
    }
}
