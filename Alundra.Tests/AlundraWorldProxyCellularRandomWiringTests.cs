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
/// D-E9d - <c>AlundraWorldProxy.InitializeWithWorld</c> wires <see cref="CellularLayerComponent.RandomSource"/>
/// to <see cref="AlundraRandom.Next"/> (the shared stream, D7). Before this slice
/// <see cref="CellularLayerComponent.RandomSource"/> defaults to a delegate that warns once and returns
/// 0 - this pins that, after <c>InitializeWithWorld</c>, calling it instead returns the SAME value
/// <see cref="AlundraRandom.Next"/> itself would return next (proving it is the shared stream, not the
/// unwired default, without relying on the process-wide "already warned once" static that would make a
/// missing-warning assertion unreliable across the whole test run). Shares
/// <see cref="AlundraRandomStaticStateCollection"/> with <see cref="AlundraRandomTests"/> - both touch
/// the process-wide static <see cref="AlundraRandom.RandomSeed"/>.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraWorldProxyCellularRandomWiringTests
{
    [Fact]
    public void InitializeWithWorld_WiresRandomSource_ToAlundraRandomNext()
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

        AlundraRandom.Reset();
        var expected = (uint)AlundraRandom.Next();
        AlundraRandom.Reset();

        var actual = cellularComponent.RandomSource();

        Assert.Equal(expected, actual);
        Assert.NotEqual(0u, actual); // the unwired default always returns 0 - a false pass is impossible here.
    }
}
