#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Events;
using Alundra.Hero;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T1, phase B test (r1) (docs/plan-e20-heros.md section 1.24.5, D-G2C2-1, D-G2C2-3, D-G2C2-5): the production registry holds <c>(C, 70)</c> and <c>(E, 20)</c> for exactly the torch (393) and <c>(D, 34)</c> for exactly the brambles (238 and 239), in no other slot; and every type of the wired set of the lift has
/// the key of its release in the slot C, so that no type is wired without its key. Written before the inscriptions of T2: the first two are red until then (nothing is registered in production by the phase A).
/// </summary>
public sealed class TorchRegistrationTests
{
    [Fact]
    public void R1_TheProductionRegistryHoldsC70AndE20_ForTheTorchAlone_AndD34ForTheTwoBramblesAlone()
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        for (var type = 0; type < 600; type++)
        {
            Assert.True((type == 393) == production.TryGet(ScriptHelper.ProgramCTick, 70, type, out _), $"(C, 70) type {type}");
            Assert.True((type == 393) == production.TryGet(ScriptHelper.ProgramEDeactivate, 20, type, out _), $"(E, 20) type {type}");
            Assert.True((type is 238 or 239) == production.TryGet(ScriptHelper.ProgramDTouch, 34, type, out _), $"(D, 34) type {type}");
        }

        // the three keys sit in their slot only.
        foreach (var type in new[] { 393, 238, 239 })
        {
            Assert.False(production.TryGet(ScriptHelper.ProgramALoad, 70, type, out _));
            Assert.False(production.TryGet(ScriptHelper.ProgramDTouch, 70, type, out _));
            Assert.False(production.TryGet(ScriptHelper.ProgramEDeactivate, 70, type, out _));
            Assert.False(production.TryGet(ScriptHelper.ProgramCTick, 20, type, out _));
            Assert.False(production.TryGet(ScriptHelper.ProgramCTick, 34, type, out _));
            Assert.False(production.TryGet(ScriptHelper.ProgramEDeactivate, 34, type, out _));
        }
    }

    [Fact]
    public void R1_EveryTypeOfTheWiredLiftSet_HoldsTheKeyOfItsRelease_C23OrC4OrC70()
    {
        var production = NativeSpriteRegistry.CreateForProduction();

        Assert.NotEmpty(PlayerManager.ProductionWiredLiftableTypes);
        foreach (var type in PlayerManager.ProductionWiredLiftableTypes.OrderBy(t => t))
        {
            var released = production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, type, out _)
                || production.TryGet(ScriptHelper.ProgramCTick, LiftableAi.C4Code, type, out _)
                || production.TryGet(ScriptHelper.ProgramCTick, TorchAi.TickCode, type, out _);
            Assert.True(released, $"the type {type} is wired to be lifted but nothing lets it go");
        }
    }
}
