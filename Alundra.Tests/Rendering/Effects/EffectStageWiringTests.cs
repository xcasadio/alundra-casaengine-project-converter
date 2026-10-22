#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Support;
using Alundra.Tests.Maps;

namespace Alundra.Tests;

/// <summary>
/// E19.g G3a (docs/plan-e19-opcodes.md, section 1.2o.6, G3A-R5 to R7): the effect stage - load-time spawn once the hero is on its arrival tile and before the first tick, the
/// queue of operations, the per-tick loop and its gate - alone, then wired in <see cref="WorldProxy.Update"/>. The values come from <c>g3a-golden.tsv</c> (the binary) and were
/// measured red on empty types first. Shares <see cref="AlundraMusicPlayerSingletonCollection"/>: the warp departure arms the real BGM fade director.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraEffectStageWiringTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    public AlundraEffectStageWiringTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    /// <summary>A map table with a loop of two one-tick frames (image sets 0 then 1, forever), and the given records.</summary>
    private static EffectBank Alternating(params EffectRecord[] records) =>
        EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 1, 0 }, new[] { 1, 1 }) }, 2, records);

    private static EffectStage StageOf(EffectBank bank, int heroTileX = 0, int heroTileY = 0)
    {
        var stage = new EffectStage();
        stage.Load(bank);
        stage.CaptureHeroTile(heroTileX, heroTileY);
        return stage;
    }

    private static (int Index, bool Active, int Set, int X, int Y, int Z)[] State(EffectStage stage) =>
        stage.Reservoir.Slots.Select(s => (s.Index, s.Active, s.CurrentImageSetIndex, s.X, s.Y, s.Z)).ToArray();

    // ---------------------------------------------------------------------------------------------------------------------
    // The stage alone
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRaysOfMap163_AreDrawnAtTicks0To10_AndFreedAtTick11_NotAt10Nor12()
    {
        var stage = new EffectStage();
        stage.Load(EffectGolden.BankOf(Root, 163));
        stage.CaptureHeroTile(0, 0);
        stage.SpawnLoadEffects();
        Assert.Equal(4, stage.Reservoir.ActiveCount);

        var rows = EffectGolden.Scenario("S163").Select(r => (Tick: int.Parse(r[2]), Active: int.Parse(r[3]))).OrderBy(r => r.Tick).ToList();
        Assert.Equal(15, rows.Count);
        foreach (var row in rows)
        {
            stage.RunFrame(1, new[] { false });
            Assert.Equal(row.Active, stage.Reservoir.ActiveCount);
        }

        Assert.Equal(new[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0 }, rows.Take(12).Select(r => r.Active).ToArray());
    }

    [Fact]
    public void TheLoadSpawn_HappensOnce_AndNeverClearsWhatIsThere()
    {
        var stage = StageOf(Alternating(EffectGolden.Record(0xC0, 1, 1, 0, 0)));
        stage.SpawnLoadEffects();
        stage.SpawnLoadEffects();

        Assert.Equal(1, stage.Reservoir.ActiveCount);
    }

    [Fact]
    public void ACreationQueuedBeforeTheFirstFrame_TakesTheNextSlot_TheLoadSlotsStayAtTheOracle()
    {
        var stage = new EffectStage();
        stage.Load(EffectGolden.BankOf(Root, 391));
        stage.CaptureHeroTile(26, 30);

        stage.Queue(0, 0, r => r.Create(true, 0, 0, 7 << 16, 8 << 16, 9 << 16)); // the generic creator of G3d, queued before the first Update of the proxy
        stage.SpawnLoadEffects();
        stage.RunFrame(1, new[] { false });

        var load = EffectGolden.Scenario("S391").Select(r => (Slot: int.Parse(r[2]), Record: int.Parse(r[3]), X: int.Parse(r[4]), Y: int.Parse(r[5]), Z: int.Parse(r[6]), Anim: int.Parse(r[7]))).ToList();
        Assert.Equal(5, load.Count);
        var slots = stage.Reservoir.ActiveSlots.ToList();
        Assert.Equal(6, slots.Count);
        foreach (var row in load)
        {
            Assert.Equal((row.Slot, row.Record, row.X, row.Y, row.Z, row.Anim), (slots[row.Slot].Index, slots[row.Slot].RecordIndex, slots[row.Slot].X, slots[row.Slot].Y, slots[row.Slot].Z, slots[row.Slot].TargetAnimation));
        }

        Assert.Equal((5, -1, 7 << 16, 8 << 16, 9 << 16), (slots[5].Index, slots[5].RecordIndex, slots[5].X, slots[5].Y, slots[5].Z));
    }

    [Fact]
    public void AFrameOfTwoTicks_GivesTheSameStateAsTwoFramesOfOneTick_OnTheRaysOf163()
    {
        var two = new EffectStage();
        var one = new EffectStage();
        foreach (var stage in new[] { two, one })
        {
            stage.Load(EffectGolden.BankOf(Root, 163));
            stage.CaptureHeroTile(0, 0);
            stage.SpawnLoadEffects();
        }

        for (var frame = 0; frame < 8; frame++) // 16 ticks: past the freeing tick 11
        {
            two.RunFrame(2, new[] { false, false });
            one.RunFrame(1, new[] { false });
            one.RunFrame(1, new[] { false });
            Assert.Equal(State(one), State(two));
        }

        Assert.Equal(0, two.Reservoir.ActiveCount);
    }

    [Fact]
    public void AFrozenTickOfAFrame_UpdatesNothing_TheOthersDo()
    {
        var frozen = StageOf(Alternating(EffectGolden.Record(0xC0, 1, 1, 0, 0)));
        var plain = StageOf(Alternating(EffectGolden.Record(0xC0, 1, 1, 0, 0)));
        frozen.SpawnLoadEffects();
        plain.SpawnLoadEffects();

        frozen.RunFrame(3, new[] { false, true, false });
        plain.RunFrame(2, new[] { false, false });

        Assert.Equal(State(plain), State(frozen));
        Assert.Equal(1, frozen.Reservoir.Slots[0].CurrentImageSetIndex);
    }

    [Fact]
    public void TheQueue_AppliesAnOperationAtItsTick_BeforeThatTicksUpdate_ByTickThenRankThenEmission_AndIsEmptyAtTheEndOfTheFrame()
    {
        var stage = StageOf(Alternating());
        void Make(int tick, int rank, int x) => stage.Queue(tick, rank, r => r.Create(true, 0, 0, x << 16, 0, 0));

        Make(1, 0, 10); // marked tick 1: created after the update of tick 0, updated at tick 1
        Make(0, 1, 20); // rank 1 before rank 0 at the same tick: the rank decides the slot
        Make(0, 0, 30);
        Make(9, 0, 40); // beyond the ticks of the frame: applied at the end, without an update
        stage.RunFrame(2, new[] { false, false });

        var slots = stage.Reservoir.ActiveSlots.ToList();
        Assert.Equal(new[] { 30 << 16, 20 << 16, 10 << 16, 40 << 16 }, slots.Select(s => s.X).ToArray());
        Assert.Equal(new[] { 1, 1, 0, -1 }, slots.Select(s => s.CurrentImageSetIndex).ToArray()); // 2 updates, 2 updates, 1 update, none

        stage.RunFrame(1, new[] { false }); // nothing is left to apply: the same four slots, one more update each
        Assert.Equal(4, stage.Reservoir.ActiveCount);
        Assert.Equal(new[] { 0, 0, 1, 0 }, stage.Reservoir.ActiveSlots.Select(s => s.CurrentImageSetIndex).ToArray());
    }

    [Fact]
    public void AFrameWithoutTicks_StillAppliesTheQueue_WithoutAnyUpdate()
    {
        var stage = StageOf(Alternating());
        stage.Queue(0, 0, r => r.Create(true, 0, 0, 1, 2, 3));

        stage.RunFrame(0, Array.Empty<bool>());

        var slot = Assert.Single(stage.Reservoir.ActiveSlots);
        Assert.Equal(-1, slot.CurrentImageSetIndex);
        stage.RunFrame(1, new[] { false });
        Assert.Equal(0, slot.CurrentImageSetIndex);
    }

    [Fact]
    public void ACreationOfAFrozenTick_ExistsAndIsReadable_ButNotAnimated()
    {
        var stage = StageOf(Alternating());
        stage.Queue(0, 0, r => r.Create(true, 0, 0, 1, 2, 3));

        stage.RunFrame(1, new[] { true });

        var slot = Assert.Single(stage.Reservoir.ActiveSlots);
        Assert.Equal((1, 2, 3, -1), (slot.X, slot.Y, slot.Z, slot.CurrentImageSetIndex));
    }

    [Fact]
    public void WithoutAHero_TheLoadSpawnReadsTheTile00_AndWarnsOnce()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var stage = new EffectStage();
        stage.Load(Alternating(
            EffectGolden.Record(0xC0, 1, 1, 0, 0, x1: 0, y1: 0, x2: 5, y2: 5),
            EffectGolden.Record(0xC0, 2, 2, 0, 0, x1: 10, y1: 10, x2: 20, y2: 20)));
        Assert.False(stage.HeroTileCaptured);

        stage.SpawnLoadEffects();
        stage.SpawnLoadEffects();

        Assert.Equal(new[] { 0 }, stage.Reservoir.ActiveSlots.Select(s => s.RecordIndex).ToArray());
        Assert.Equal(1, log.Warnings.Count(w => w.Contains("EffectStage")));
    }

    [Fact]
    public void AMapWithNoRecordToSpawn_NeedsNoHero_AndWarnsOfNothing()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var stage = new EffectStage();
        stage.Load(Alternating());

        stage.SpawnLoadEffects();

        Assert.Equal(0, stage.Reservoir.ActiveCount);
        Assert.Empty(log.Warnings.Where(w => w.Contains("EffectStage")));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Wired in the proxy
    // ---------------------------------------------------------------------------------------------------------------------

    private static ArcSpec Ship391 => new("G3a-391", "The Klark", "Ship Klark (night, break, Event)-391", new[] { 1641 }, 15, 28, 7, 200, RealController: true, Prefabs: true);

    // B1 of map 391 also runs four 0xA2 (pc 228, 236, 244, 252) in the first frame: they take the slots 5 to 8 (rows S391a 5 to 8 of g3b-golden.tsv, raw 16.16).
    private static readonly (int Record, int X, int Y, int Z)[] FourA2OfB1 =
    {
        (3, 22020096, 45613056, 5242881),
        (4, 36175872, 45613056, 5242881),
        (0, 22020096, 50855936, 5242881),
        (1, 31981568, 50855936, 5242881),
    };

    [Fact]
    public void Map391_AfterTheFirstFrame_HoldsTheFiveLoadSlotsOfTheBinary_AtTheirPositions_AndTheFourA2OfB1()
    {
        using var arc = new ArcRun(Ship391);

        arc.OneFrame();

        var load = EffectGolden.Scenario("S391").Select(r => (Slot: int.Parse(r[2]), Record: int.Parse(r[3]), X: int.Parse(r[4]), Y: int.Parse(r[5]), Z: int.Parse(r[6]), Anim: int.Parse(r[7]))).ToList();
        var slots = arc.Proxy._effectStage.Reservoir.ActiveSlots.ToList();
        Assert.Equal(9, slots.Count);
        foreach (var row in load)
        {
            var slot = slots[row.Slot];
            Assert.Equal((row.Slot, row.Record, row.X, row.Y, row.Z, row.Anim), (slot.Index, slot.RecordIndex, slot.X, slot.Y, slot.Z, slot.TargetAnimation));
            Assert.NotNull(slot.CurrentImageSet); // updated at the tick 0 of the first frame
        }
        for (var i = 0; i < FourA2OfB1.Length; i++)
        {
            var slot = slots[5 + i];
            Assert.Equal((5 + i, FourA2OfB1[i].Record, FourA2OfB1[i].X, FourA2OfB1[i].Y, FourA2OfB1[i].Z), (slot.Index, slot.RecordIndex, slot.X, slot.Y, slot.Z));
        }
    }

    [Fact]
    public void Map391_ACreationQueuedBeforeTheFirstUpdateOfTheProxy_TakesSlot9_AfterTheMapEventCreations_NothingIsErased()
    {
        using var arc = new ArcRun(Ship391);
        // Source rank 1 (entities): applied after the map-event creations (rank 0) of the same tick.
        arc.Proxy._effectStage.Queue(0, 1, r => r.Create(true, 0, 0, 5 << 16, 6 << 16, 7 << 16));

        arc.OneFrame();

        var slots = arc.Proxy._effectStage.Reservoir.ActiveSlots.ToList();
        Assert.Equal(10, slots.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, slots.Select(s => s.Index).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 3, 4, 0, 1, -1 }, slots.Select(s => s.RecordIndex).ToArray());
        Assert.Equal(new[] { 65273856, 65273856, 65273856, 65273856, 66846720, 22020096, 36175872, 22020096, 31981568, 5 << 16 }, slots.Select(s => s.X).ToArray());
        Assert.Equal(new[] { 33030144, 39321600, 45613056, 51904512, 51904512, 45613056, 45613056, 50855936, 50855936, 6 << 16 }, slots.Select(s => s.Y).ToArray());
    }

    private static ArcSpec Dream109(ArcArrival? arrival) =>
        new("G3a-109", "Nestus' Dream", "Nestus' Dream-109", Array.Empty<int>(), 30, 10, 0, 20, Arrival: arrival);

    private static int[] RestrictedRecordsAfterTheFirstFrame(ArcRun arc)
    {
        arc.OneFrame();
        return arc.Proxy._effectStage.Reservoir.ActiveSlots.Select(s => s.RecordIndex).Where(i => i is 4 or 5 or 6).ToArray();
    }

    [Fact]
    public void TheLoadSpawn_ReadsTheArrivalTile_NotTheTileTheHeroHasWhenTheFirstUpdateRuns_Inside()
    {
        // The arrival tile (30, 10) is inside the zone (27, 0)-(51, 27) of the records 4, 5 and 6 of map 109 (G3a-0, table Z); the hero is then moved off it
        // (the scripts and the physics of the first frame do) before the proxy's first Update.
        using var arc = new ArcRun(Dream109(new ArcArrival((30 * 24 + 12) << 16, (10 * 16 + 8) << 16, 0, GameState.ResetAnimationId, 0)));
        Assert.Equal((30, 10), (arc.Hero.TileX, arc.Hero.TileY));
        arc.Hero.TileX = 0;
        arc.Hero.TileY = 59;

        Assert.Equal(new[] { 4, 5, 6 }, RestrictedRecordsAfterTheFirstFrame(arc));
    }

    [Fact]
    public void TheLoadSpawn_ReadsTheArrivalTile_NotTheTileTheHeroHasWhenTheFirstUpdateRuns_Outside()
    {
        // No arrival: AdoptPlayerPawn puts the hero on the New Game tile, outside the zone; the montage then moves it inside before the first Update.
        using var arc = new ArcRun(Dream109(null));
        Assert.Equal((30, 10), (arc.Hero.TileX, arc.Hero.TileY)); // the montage's own placement, after the adoption

        Assert.Empty(RestrictedRecordsAfterTheFirstFrame(arc));
    }

    private static (WorldProxy Proxy, World World) HeadlessProxy()
    {
        var world = new World { Name = "TestWorld" };
        world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        return (proxy, world);
    }

    [Fact]
    public void Update_WithoutHero_StillTicksTheEffects_AndTheGateIs0x48Only()
    {
        var (proxy, _) = HeadlessProxy();
        var stage = proxy._effectStage;
        stage.Load(Alternating());
        var slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;
        Assert.Null(proxy.PlayerEntity);

        proxy.Update(0.02f); // tick 0
        Assert.Equal(0, slot.CurrentImageSetIndex);
        proxy.Update(0.02f);
        Assert.Equal(1, slot.CurrentImageSetIndex);

        foreach (var (flags, moves) in new[]
        {
            (GameState.PlayerControlBits.MenuOpen, false), (GameState.PlayerControlBits.Unused40, false),
            (GameState.PlayerControlBits.MenuOpen | GameState.PlayerControlBits.Unused40, false),
            (0x01u, true), (0x07u, true), (0x37u, true), (GameState.PlayerControlBits.ForcedWeapon, true),
        })
        {
            proxy.GameState.PlayerControlFlags = flags;
            var before = slot.CurrentImageSetIndex;
            proxy.Update(0.02f);
            Assert.Equal(moves ? 1 - before : before, slot.CurrentImageSetIndex);
            Assert.True(slot.Active);
        }

        proxy.GameState.PlayerControlFlags = 0;
    }

    [Fact]
    public void Update_WithAFrameOfTwoTicks_GivesTwoUpdates_AndAZeroTickFrameNone()
    {
        var (proxy, _) = HeadlessProxy();
        var stage = proxy._effectStage;
        stage.Load(Alternating());
        var slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;
        proxy.Update(0.02f); // tick 0 of the first frame: set 0

        proxy.Update(0.04f); // two ticks: sets 1 then 0
        Assert.Equal(0, slot.CurrentImageSetIndex);
        proxy.Update(0.0f);
        Assert.Equal(0, slot.CurrentImageSetIndex);
        proxy.Update(0.02f);
        Assert.Equal(1, slot.CurrentImageSetIndex);
    }

    [Fact]
    public void Update_DuringAWarpDeparture_GivesTheEffectsNoTick_AndTheQueueIsStillApplied()
    {
        var (world, _) = WorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        BackdropWarpDepartureSupport.SetPrivateField(proxy._backdropStage, "_clearColorApplied", true); // the headless montage has no view for the clear colour.
        var stage = proxy._effectStage;
        stage.Load(Alternating());
        var slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;

        proxy.Update(0.02f); // priming frame: tick 0
        Assert.Equal(0, slot.CurrentImageSetIndex);

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);
        stage.Queue(0, 0, r => r.Create(true, 0, 0, 1, 2, 3));
        for (var frame = 0; frame < 16; frame++)
        {
            proxy.Update(0.02f);
            Assert.True(WarpDirector.Instance.IsTransitionInProgress, $"F{frame}");
            Assert.Equal(0, slot.CurrentImageSetIndex);
            Assert.Equal(2, stage.Reservoir.ActiveCount);
        }

        GC.KeepAlive(player);
        var created = stage.Reservoir.Slots[1];
        Assert.Equal(-1, created.CurrentImageSetIndex); // created during the departure, never updated

        WarpDirector.Instance.InstallForMapEntry();
        proxy.Update(0.02f);
        Assert.Equal(1, slot.CurrentImageSetIndex);
        Assert.Equal(0, created.CurrentImageSetIndex);
    }
}
