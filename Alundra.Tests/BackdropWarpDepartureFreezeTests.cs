#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.m4 (docs/plan-e19-opcodes.md, section 1.2s.5, D-E19-67, ADR-0034, O-E19-55) - the backdrops get no tick while a warp
/// departure is in progress, like the binary: its transition loop (0x8002C490-0x8002C4C0) never calls <c>RenderScene</c>,
/// the only road to the backdrop driver. The frame is still pushed (camera target and scroll), with 0 ticks, from the arming
/// frame F0 to the frame that ends the fade, F15. The values below were written before the code (audit
/// <c>e19o55-disc</c>, scripts <c>values.py</c> and <c>census.py</c>) and measured red on the code before the change.
/// Shares <see cref="AlundraMusicPlayerSingletonCollection"/>: <c>BeginDeparture</c> arms the real BGM fade director.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class BackdropWarpDepartureFreezeTests : IDisposable
{
    public BackdropWarpDepartureFreezeTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    /// <summary>
    /// T-55a - the push contract. Map 389 montage, both services attached, nothing calls <c>Advance</c>: after each of the 16
    /// departure frames F0..F15 both services hold 0 pending ticks (before the change: 1, from F0), yet every frame is still
    /// pushed (<c>FramesPushed</c> +1). Once the map entry lifts the gate, the next frame pushes 1 tick again.
    /// </summary>
    [Fact]
    public void Update_DuringAWarpDeparture_PushesEveryFrameWithZeroTicks_ThenOneTickAfterTheMapEntry()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrolling = new ScrollingLayerService();
        var cellular = new CellularLayerService();
        stage.AttachService(scrolling);
        stage.AttachCellularService(cellular);
        BackdropWarpDepartureSupport.SetPrivateField(stage, "_clearColorApplied", true);

        proxy.Update(0.02f); // priming frame: the sticky first-frame floor.
        Assert.Equal(1, scrolling.PendingTicks);
        Assert.Equal(1, cellular.PendingTicks);

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);

        for (var frame = 0; frame < 16; frame++)
        {
            var scrollingFrames = scrolling.FramesPushed;
            var cellularFrames = cellular.FramesPushed;

            proxy.Update(0.02f);

            Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress, $"F{frame}");
            Assert.Equal(0, scrolling.PendingTicks);
            Assert.Equal(0, cellular.PendingTicks);
            Assert.Equal(scrollingFrames + 1, scrolling.FramesPushed);
            Assert.Equal(cellularFrames + 1, cellular.FramesPushed);
        }

        GC.KeepAlive(player);

        AlundraWarpDirector.Instance.InstallForMapEntry();
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);

        proxy.Update(0.02f);
        Assert.Equal(1, scrolling.PendingTicks);
        Assert.Equal(1, cellular.PendingTicks);
    }

    /// <summary>
    /// T-55b - the scrolling state. The real sea of map 389 (layer 0: automatic scroll periods 10 and 5), loaded by the stage,
    /// <c>Advance</c> after every frame, departure armed at t = 10 (the priming frame plus 9): auto-scroll (1, 2) and timers
    /// (0, 0). After all of F0..F15 the state is unchanged (before the change: (2, 5) and (6, 1) after F15, timers (1, 1)
    /// after F0 alone), and so is the wrapped offset (481, 362) (before: (482, 365)).
    /// </summary>
    [Fact]
    public void RealMap389Sea_DuringAWarpDeparture_AutoScrollAndTimersDoNotAdvance()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrolling = new ScrollingLayerService();
        stage.AttachService(scrolling);
        BackdropWarpDepartureSupport.SetPrivateField(stage, "_clearColorApplied", true);
        stage.Load(world, AlundraWorldProxyGlobalFreezeTests.FindProjectRoot());
        Assert.True(scrolling.LayerCount > 0);

        for (var frame = 0; frame < 10; frame++)
        {
            proxy.Update(0.02f);
            scrolling.Advance();
        }

        Assert.True(scrolling.TryGetLayerState(0, out var armed));
        Assert.Equal((1, 2), (armed.AutoScrollOffsetX, armed.AutoScrollOffsetY));
        Assert.Equal((0, 0), (armed.TimerX, armed.TimerY));

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);

        for (var frame = 0; frame < 16; frame++)
        {
            proxy.Update(0.02f);
            scrolling.Advance();

            Assert.True(scrolling.TryGetLayerState(0, out var state), $"F{frame}");
            Assert.Equal((1, 2), (state.AutoScrollOffsetX, state.AutoScrollOffsetY));
            Assert.Equal((0, 0), (state.TimerX, state.TimerY));
        }

        Assert.True(scrolling.TryGetLayerState(0, out var last));
        Assert.Equal((481, 362), (last.LayerOffsetX, last.LayerOffsetY));
        GC.KeepAlive(player);
    }
}

/// <summary>
/// E19.m4 T-55c (docs/plan-e19-opcodes.md, section 1.2s.5) - the rain of map 391 and the C library generator: a departure
/// draws no <c>rand()</c> value. Own class because the generator is a process-wide static (<see cref="AlundraLibcRandom"/>):
/// <see cref="AlundraRandomStaticStateCollection"/>. Self-skips when the export is absent.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class BackdropWarpDepartureRainTests : IDisposable
{
    private readonly uint _savedLibcState = AlundraLibcRandom.State;

    public BackdropWarpDepartureRainTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        AlundraLibcRandom.State = _savedLibcState;
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    [Fact]
    public void RealMap391Rain_DuringAWarpDeparture_DrawsNoRandValueAndKeepsItsCells()
    {
        string projectRoot;
        try
        {
            projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
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
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrolling = new ScrollingLayerService();
        var cellular = new CellularLayerService();
        stage.AttachService(scrolling);
        stage.AttachCellularService(cellular);
        stage.Load(world, projectRoot);
        Assert.True(cellular.LayerCount > 0);

        AlundraLibcRandom.State = 0;
        var draws = 0;
        Func<uint> source = () =>
        {
            draws++;
            return (uint)AlundraLibcRandom.Next();
        };

        proxy.Update(0.02f); // priming frame: one tick.
        cellular.Advance(source);

        Assert.Equal(17, draws);
        Assert.Equal(0x7E7099A9u, AlundraLibcRandom.State);
        Assert.True(cellular.TryGetCellState(0, 0, out var primed));
        Assert.Equal((251, 202), (primed.DrawX, primed.DrawY));

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);

        for (var frame = 0; frame < 16; frame++)
        {
            proxy.Update(0.02f);
            cellular.Advance(source);
        }

        Assert.Equal(17, draws);
        Assert.Equal(0x7E7099A9u, AlundraLibcRandom.State);
        Assert.True(cellular.TryGetCellState(0, 0, out var last));
        Assert.Equal((251, 202), (last.DrawX, last.DrawY));
        GC.KeepAlive(player);
    }
}

internal static class BackdropWarpDepartureSupport
{
    internal static void ResetSingletons()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
    }

    /// <summary>Arms a departure through portal 0 of map 389 (the pattern of <c>AlundraWarpDepartureTests</c>), on a
    /// non-degraded montage: the director is attached to a real <see cref="GameManager"/> so that the gate stays posted
    /// through the 16 frames.</summary>
    internal static AlundraEntityScriptProxy BeginDeparture(AlundraWorldProxy proxy)
    {
        var entity = new Entity { Name = "player", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var player = (AlundraEntityScriptProxy)entity.GameplayProxy;
        player.IsPlayer = true;
        player.Status = EntityStatus.Normal;
        player.PosX = (18 * 24 + 12) << 16;
        player.PosY = (38 * 16 + 8) << 16;
        player.PosZ = 0;
        player.ScriptHost = proxy;
        proxy.PlayerEntity = player;

        AlundraWarpDirector.Instance.AttachToWorld(
            new GameManager(null), soundPlayer: null, AlundraWorldProxyGlobalFreezeTests.FindProjectRoot());

        var portal = new AlundraPortalRecord
        {
            Index = 0,
            X1 = 18,
            Y1 = 38,
            X2 = 18,
            Y2 = 38,
            DestMapId = 390,
            DestTileX = 10,
            DestTileY = 40,
            ZLevel = 0,
            Flags = 0x5001,
        };

        AlundraWarpDirector.Instance.BeginDeparture(portal, 0x10, player, proxy.GameState);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        return player;
    }

    internal static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
