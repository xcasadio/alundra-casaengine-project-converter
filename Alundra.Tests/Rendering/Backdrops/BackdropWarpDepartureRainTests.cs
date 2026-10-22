#nullable enable
using System;
using System.Reflection;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Rng;
using Alundra.Rendering.Backdrops;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Maps;
using Alundra.Tests.Rng;

namespace Alundra.Tests.Rendering.Backdrops;

/// <summary>
/// E19.m4 T-55c (docs/plan-e19-opcodes.md, section 1.2s.5) - the rain of map 391 and the C library generator: a departure
/// draws no <c>rand()</c> value. Own class because the generator is a process-wide static (<see cref="LibcRandom"/>):
/// <see cref="RandomStaticStateCollection"/>. Self-skips when the export is absent.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class BackdropWarpDepartureRainTests : IDisposable
{
    private readonly uint _savedLibcState = LibcRandom.State;

    public BackdropWarpDepartureRainTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        LibcRandom.State = _savedLibcState;
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    [Fact]
    public void RealMap391Rain_DuringAWarpDeparture_DrawsNoRandValueAndKeepsItsCells()
    {
        string projectRoot;
        try
        {
            projectRoot = WorldProxyGlobalFreezeTests.FindProjectRoot();
        }
        catch
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        const string worldName = "Ship Klark (night, break, Event)-391";
        if (BackdropLoader.Load(projectRoot, worldName) == null)
        {
            return; // self-skip: export absent
        }

        var world = new World { Name = worldName };
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrolling = new ScrollingLayerService();
        var cellular = new CellularLayerService();
        stage.AttachService(scrolling);
        stage.AttachCellularService(cellular);
        stage.Load(world, projectRoot);
        Assert.True(cellular.LayerCount > 0);

        LibcRandom.State = 0;
        var draws = 0;
        Func<uint> source = () =>
        {
            draws++;
            return (uint)LibcRandom.Next();
        };

        proxy.Update(0.02f); // priming frame: one tick.
        cellular.Advance(source);

        Assert.Equal(17, draws);
        Assert.Equal(0x7E7099A9u, LibcRandom.State);
        Assert.True(cellular.TryGetCellState(0, 0, out var primed));
        Assert.Equal((251, 202), (primed.DrawX, primed.DrawY));

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);

        for (var frame = 0; frame < 16; frame++)
        {
            proxy.Update(0.02f);
            cellular.Advance(source);
        }

        Assert.Equal(17, draws);
        Assert.Equal(0x7E7099A9u, LibcRandom.State);
        Assert.True(cellular.TryGetCellState(0, 0, out var last));
        Assert.Equal((251, 202), (last.DrawX, last.DrawY));
        GC.KeepAlive(player);
    }
}
