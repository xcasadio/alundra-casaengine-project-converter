#nullable enable
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// D-E9d, E19.m2 (D-E19-66, ADR-0032) - <c>AlundraWorldProxy.InitializeWithWorld</c> wires
/// <see cref="CellularLayerComponent.RandomSource"/> to <see cref="AlundraLibcRandom.Next"/> (the C library
/// <c>rand()</c> of the original, not the game's shared <see cref="AlundraRandom"/> stream). Before the wiring
/// <see cref="CellularLayerComponent.RandomSource"/> defaults to a delegate that warns once and returns 0 - this
/// pins that, after <c>InitializeWithWorld</c>, calling it returns the next C library value (state
/// <c>0x12345678</c> gives 2929 and leaves the state at <c>0x0B719151</c>) and leaves the game's stream untouched
/// (proving it is that generator, not the unwired default, without relying on the process-wide "already warned
/// once" static that would make a missing-warning assertion unreliable across the whole test run). Shares
/// <see cref="AlundraRandomStaticStateCollection"/> with <see cref="AlundraRandomTests"/> and
/// <see cref="AlundraLibcRandomTests"/> - they touch the process-wide statics <see cref="AlundraRandom.RandomSeed"/>
/// and <see cref="AlundraLibcRandom.State"/>.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraWorldProxyCellularRandomWiringTests : System.IDisposable
{
    public AlundraWorldProxyCellularRandomWiringTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md): this class constructs an AlundraWorldProxy, so it resets the session carriers in its constructor (the
        // isolation-carrying element) and in Dispose (hygiene).
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void InitializeWithWorld_WiresRandomSource_ToTheLibcRandAndLeavesTheGameStreamUntouched()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var cellularComponent = new CellularLayerComponent(game);
        AlundraWorldProxyGlobalFreezeTests.SetProperty(game, nameof(CasaEngineGame.CellularLayerComponent), cellularComponent);

        var world = new World { Name = "TestWorld" };
        AlundraWorldProxyGlobalFreezeTests.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no "tileMap" entity -> early return AFTER the wiring above.

        var savedLibcState = AlundraLibcRandom.State;
        try
        {
            AlundraRandom.Reset();
            AlundraLibcRandom.State = 0x12345678; // not 0: the first draw from state 0 is 0, like the unwired default.

            var actual = cellularComponent.RandomSource();

            Assert.Equal(2929u, actual);
            Assert.Equal(0x0B719151u, AlundraLibcRandom.State);
            Assert.Equal(0xB017C93DUL, AlundraRandom.RandomSeed); // the game's own stream did not move.
        }
        finally
        {
            AlundraLibcRandom.State = savedLibcState;
            AlundraRandom.Reset();
        }
    }
}
