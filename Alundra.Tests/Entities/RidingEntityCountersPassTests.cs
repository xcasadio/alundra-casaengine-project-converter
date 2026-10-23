#nullable enable
using System;
using System.Collections.Generic;
using Xunit;
using Alundra.Entities;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Entities;

/// <summary>
/// E19.d2c2 D5b T1 (docs/plan-e19-opcodes.md §1.2h.3.2b, M4, D-D5B-6): the counters pass of the binary (<c>0x80038998</c>, after the programs, before the lists) clears
/// <c>+0x12C</c> (<see cref="EntityScriptProxy.RidingEntity"/>) of every slot (<c>0x800389DC</c>); the carrier is set again at the head of the entity's own tick (<c>0x800364C8</c>). The DLL's
/// <c>WorldProxy.RunCountersPass</c> does not clear it: what the previous tick computed stays until the lists step recomputes it, so a reader between the two (a program of the next tick,
/// a hook of the pass) reads the previous tick's carrier. Written BEFORE the code, on the real map 22 (the lift, record 2, with the barrel, record 33, put on it as in the probe P10), through
/// the trace hook of the world: <c>"lists"</c> is called right after the counters pass and before <c>RefreshUpdateProxiesAndCollidables</c>.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class RidingEntityCountersPassTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public RidingEntityCountersPassTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The map 22 with the barrel on the lift and everything settled: the barrel rides the platform.</summary>
    private static (ArcRun Arc, EntityScriptProxy Platform, EntityScriptProxy Barrel) SettledBarrelOnTheLift()
    {
        var spec = new ArcSpec("D5b-RIDE-M4", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(996 << 16, 176 << 16, 2 << 20, GameState.ResetAnimationId, 0));
        var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var barrel = arc.EntityByRecord(33)!;
        barrel.PosX = 1020 << 16;
        barrel.PosY = 176 << 16;
        barrel.PosZ = (32 << 16) - barrel.ModZ;
        barrel.PushLogicalPositionToRoot();
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Assert.Same(platform.LogicContextEntity, barrel.RidingEntity);
        Assert.Same(platform.LogicContextEntity, arc.Hero.RidingEntity);
        return (arc, platform, barrel);
    }

    [Fact]
    public void RideM4_APassengerOnItsCarrier_HasNoCarrierRightAfterTheCountersPass_AndHasItAgainAfterTheLists()
    {
        var (arc, platform, barrel) = SettledBarrelOnTheLift();
        using (arc)
        {
            var atLists = new List<(bool Barrel, bool Hero)>();
            arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "lists")
                {
                    atLists.Add((barrel.RidingEntity != null, arc.Hero.RidingEntity != null));
                }
            };

            arc.OneFrame();
            arc.OneFrame();
            Assert.Equal(2, atLists.Count);
            for (var i = 0; i < atLists.Count; i++)
            {
                // The binary clears +0x12C of every slot, the hero's too (0x800389DC): nothing is read between the counters and the recomputation of the lists.
                Assert.True(!atLists[i].Barrel, $"tick {i}: the barrel still has its carrier of the previous tick");
                Assert.True(!atLists[i].Hero, $"tick {i}: the hero still has its carrier of the previous tick");
            }

            // The lists step recomputes it: the pile is intact at the end of the tick.
            Assert.Same(platform.LogicContextEntity, barrel.RidingEntity);
        }
    }

    [Fact]
    public void RideM4_TheCarrierRemovedBetweenTwoTicks_NoStaleReferenceIsReadAfterTheCountersPass_AndNoneAtTheEndOfTheTick()
    {
        var (arc, platform, barrel) = SettledBarrelOnTheLift();
        using (arc)
        {
            // The lift is taken away between two ticks (put 64 px higher, above the barrel): the barrel has nothing under its feet any more.
            platform.PosZ += 64 << 16;
            platform.PushLogicalPositionToRoot();
            var readAtLists = new List<object?>();
            arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "lists")
                {
                    readAtLists.Add(barrel.RidingEntity);
                }
            };

            arc.OneFrame();
            Assert.Single(readAtLists);
            Assert.Null(readAtLists[0]);
            Assert.Null(barrel.RidingEntity);
        }
    }
}
