#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Entities;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Maps;

/// <summary>
/// E20.c2b T3 (docs/plan-e20-heros.md section 1.12.1, D-C2B-7, tests (k1) to (k3)): the blink of the invulnerable entities. The draw list of the binary (<c>0x800385D0-0x800385F4</c>) takes an entity of status 2 or 3 only if
/// <c>(+0x1CC &amp; 3) != 3</c>, a rule that holds for every entity with a <c>+0x1CC</c> (the hero hurt, an object dropped for its last 120 ticks, the targets of the opcode <c>0x86</c>); the DLL publishes to the engine the
/// list of the LAST tick of each frame, as the binary draws the list of its last tick.
/// (k1) the hero of A1 against the <c>visible</c> column of the hurt-hero oracle of E20.c2 (<c>docs/plan-e20-annexe/e20-c2/oracle/out/A1.json</c>: the real code of <c>ALUN_CD.EXE</c> (France) in the emulator), read at the last
/// tick of every frame at one, two and three ticks per frame; (k2) a dropped object blinks its last 120 ticks (Q-E0C-3), the rule being the mark of 120 written by the binary and the period of the counter; (k3) the witnesses:
/// what the blink did not hide it never shows (an object held Loaded by a chest, a flagged entity) and an entity with no counter stays visible.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WorldProxyBlinkTests : IDisposable
{
    private const int Gildas = 0x45;       // life 600 and the mark of the last 120 ticks (PickupAiDropWorldTests W4).

    public WorldProxyBlinkTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static IEnumerable<object[]> TicksPerFrame() => new[] { 1, 2, 3 }.Select(n => new object[] { n });

    // -----------------------------------------------------------------------------------------
    // (k1) A1: the hero's draw column of the oracle, at the last tick of each frame.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void K1_TheHeroOfA1_IsHiddenAsTheOraclesDrawListHidesHim_AtTheLastTickOfEachFrame(int ticksPerFrame)
    {
        var trace = HurtOracleTrace.Load("A1.json", "A1");
        var hiddenInTheOracle = Enumerable.Range(1, trace.Ticks).Count(t => trace.Col("visible", t) == 0);
        Assert.Equal(30, hiddenInTheOracle); // the premise: 30 ticks of 120 (the rule of the binary), from the tick of the hurt.
        Assert.Equal(1, trace.Col("visible", 17));
        Assert.Equal(18, Enumerable.Range(1, trace.Ticks).First(t => trace.Col("visible", t) == 0));

        using var arena = new HitArena("blink-A1-" + ticksPerFrame, trace.HeroPixelX, trace.HeroPixelY, trace.TargetPixels);
        var hero = arena.Hero.OwnerEntity ?? throw new InvalidOperationException("the hero has no entity");
        var diffs = new List<string>();
        var hiddenSeen = 0;
        arena.Begin();
        for (var tick = 1; tick <= trace.Ticks;)
        {
            var size = Math.Min(ticksPerFrame, trace.Ticks - tick + 1);
            arena.Frame((uint)trace.Hold[tick], size);
            tick += size;
            var last = tick - 1;
            var visible = hero.IsVisible;
            hiddenSeen += visible ? 0 : 1;
            if (visible != (trace.Col("visible", last) != 0))
            {
                diffs.Add($"A1 x{ticksPerFrame} tick {last}: dll visible {visible}, oracle {trace.Col("visible", last) != 0} (+0x1CC {arena.Hero.DamagedTickCounter})");
            }
        }

        Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the oracle; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(15))}");
        Assert.True(hiddenSeen > 0, "the hero was hidden at least once");
    }

    // -----------------------------------------------------------------------------------------
    // (k2) A dropped object blinks its last 120 ticks.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void K2_ADroppedObject_BlinksItsLast120Ticks_HiddenOneTickInFour_FromTheTickOfTheMark(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("blink-drop-" + ticksPerFrame);
        var drop = run.DropAt(Gildas, 284, 96);
        if (ticksPerFrame == 2)
        {
            run.Run(1, 1); // the mark falls an odd number of ticks after the drop: shift the frames by one tick so that a frame of two ticks ends on a hidden tick of the period.
        }

        var shown = drop.OwnerEntity ?? throw new InvalidOperationException("the drop has no entity");
        var frames = new List<(int Tick, bool Visible)>();
        bool Alive() => run.Context.SpawnedEntities.Contains(drop) && drop.Status < EntityStatus.FlagToDestroy;
        for (var frame = 0; frame < 800 && Alive(); frame++)
        {
            run.Run(ticksPerFrame, ticksPerFrame);
            if (Alive())
            {
                frames.Add((run.Tick, shown.IsVisible));
            }
        }

        // The tick of the mark: the tick that wrote 0x78 (PickupAiDropWorldTests W4 proves it is the one where the life reaches 120); the counters pass of that tick leaves 0x77, hidden (0x77 & 3 == 3).
        var mark = run.Samples.Single(s => s.Out.Marker == 0x78);
        Assert.Equal(0x77, mark.End.Marker);

        var before = frames.Where(f => f.Tick < mark.Tick).ToList();
        var during = frames.Where(f => f.Tick >= mark.Tick).ToList();
        Assert.NotEmpty(before);
        Assert.NotEmpty(during);
        Assert.All(before, f => Assert.True(f.Visible, $"tick {f.Tick}: before the mark the object is drawn"));
        Assert.All(during, f => Assert.Equal((f.Tick - mark.Tick) % 4 != 0, f.Visible));
        Assert.Contains(during, f => !f.Visible);
        if (ticksPerFrame == 1)
        {
            Assert.Equal(30, during.Count(f => !f.Visible)); // 30 hidden ticks of the 120.
        }
    }

    // -----------------------------------------------------------------------------------------
    // (k3) The witnesses: the blink shows only what it hid, and hides only the active entities with a counter.
    // -----------------------------------------------------------------------------------------

    private static (Entity Entity, EntityScriptProxy Proxy) Bare(EntityStatus status, bool visible)
    {
        var entity = new Entity();
        var proxy = new EntityScriptProxy { Status = status };
        entity.SetGameplayProxy(proxy);
        entity.IsVisible = visible;
        return (entity, proxy);
    }

    private static void ListsAndPublish(Entity entity, EntityScriptProxy proxy)
    {
        FrameSyncPasses.MarkBlink(proxy);
        FrameSyncPasses.PublishBlink(entity, proxy);
    }

    [Fact]
    public void K3_AnObjectHeldLoadedByAChest_StaysHidden_WhateverItsCounter()
    {
        var (entity, proxy) = Bare(EntityStatus.Loaded, visible: false); // ChestAi.cs: born Loaded, not drawn until A255 lets it go.
        for (var counter = 0x7B; counter >= 0x70; counter--)
        {
            proxy.DamagedTickCounter = counter;
            ListsAndPublish(entity, proxy);
            Assert.False(entity.IsVisible, $"counter {counter:X}: the blink never shows what it did not hide");
        }
    }

    [Fact]
    public void K3_AFlaggedEntity_StaysHidden_WhenTheBlinkLetsGo()
    {
        var (entity, proxy) = Bare(EntityStatus.FlagToDestroy, visible: false); // FrameSyncPasses.SyncAnimation hid it; the blink had hidden it before the flag.
        proxy.HiddenByBlink = true;
        proxy.DamagedTickCounter = 2;
        ListsAndPublish(entity, proxy);
        Assert.False(entity.IsVisible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(0x78)]
    [InlineData(0x74)]
    public void K3_AnEntityWithNoCounterToHideIt_StaysVisible(int counter)
    {
        var (entity, proxy) = Bare(EntityStatus.Normal, visible: true);
        proxy.DamagedTickCounter = counter;
        ListsAndPublish(entity, proxy);
        Assert.True(entity.IsVisible, $"counter {counter:X}: (counter & 3) != 3");
    }

    [Fact]
    public void K3_TheBlinkHidesAnActiveEntity_AndShowsItAgainWhenTheCounterLeavesTheCase()
    {
        var (entity, proxy) = Bare(EntityStatus.Deactivated, visible: true);
        proxy.DamagedTickCounter = 7;
        ListsAndPublish(entity, proxy);
        Assert.False(entity.IsVisible);
        proxy.DamagedTickCounter = 6;
        ListsAndPublish(entity, proxy);
        Assert.True(entity.IsVisible);
    }
}
