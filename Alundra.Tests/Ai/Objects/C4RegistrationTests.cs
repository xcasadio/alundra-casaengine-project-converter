#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Events;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c1 T2 (docs/plan-e20-heros.md section 1.24.5, D-G2C1-1, D-G2C1-9): the registration of C4 in production. The key <c>(C, 4)</c> is held for the 19 types (the 7 of the export, the ice block 304 and the 11 shop articles), for no other type and in no other slot;
/// the trunk is the only type that feeds its ground slope when the map placed it.
/// </summary>
public sealed class C4RegistrationTests
{
    private static readonly int[] ShopArticles = { 48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113 };

    [Fact]
    public void TheProductionRegistry_HoldsC4ForTheNineteenTypes_AndForNoOther()
    {
        var production = NativeSpriteRegistry.CreateForProduction();
        var expected = new[] { 290, 293, 363, 367, 368, 369, 373, 304 }.Concat(ShopArticles).OrderBy(t => t).ToArray();
        Assert.Equal(19, expected.Length);
        Assert.Equal(expected, LiftableAi.C4ProductionTypes.OrderBy(t => t).ToArray());

        for (var type = 0; type < 600; type++)
        {
            Assert.True(expected.Contains(type) == production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.C4Code, type, out _), $"type {type}");
        }

        // the key is in the slot C only; the torch (type 393, g2c2) and C23 are untouched.
        foreach (var slot in new[] { ScriptHelper.ProgramALoad, ScriptHelper.ProgramDTouch, ScriptHelper.ProgramEDeactivate })
        {
            Assert.All(expected, type => Assert.False(production.TryGet(slot, LiftableAi.C4Code, type, out _), $"slot {slot} type {type}"));
        }

        Assert.False(production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.C4Code, 393, out _));
        Assert.All(LiftableAi.SpriteTypes, type => Assert.True(production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, type, out _), $"C23, type {type}"));
    }

    [Fact]
    public void OnlyTheTrunk_FeedsItsGroundSlope()
    {
        Assert.Equal(new[] { 373 }, Enumerable.Range(0, 600).Where(LiftableAi.FeedsGroundSlope).ToArray());
    }
}
