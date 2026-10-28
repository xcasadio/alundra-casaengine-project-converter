#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T2, phase B (docs/plan-e20-heros.md section 1.31.5, tests (w8) to (w10)): the fire tome 2 through the PRODUCTION path, with no seam of the tests: the entries <c>0x30</c> of the three seams of the item in use in <see cref="PlayerManager"/> (the start of the tomes of h4a,
/// <see cref="FireTome2Ai.Driver"/>, <see cref="FireTome2Ai.Cancel"/>) and the (E, 28) of <see cref="NativeSpriteRegistry.CreateForProduction"/>. They are RED before the phase B (the entry of the tome is the stub of h0a: the Circle starts nothing; E28 is not registered) and green after it.
/// (w8) Rond with the item <c>0x30</c>: the key of (w1); (w9) the table of slots full: the column is not born, the magic is lost, the item is freed at the tick 34, the hero is Idle and never in <c>0x34</c>; (w10) Rond with the item <c>0x30</c> interrupted at the tick 60 (R-EL-6): compared as (w2).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome2ProductionTests : IDisposable
{
    private const uint Tome = 0x30;
    private const int FillerType = 5;

    public FireTome2ProductionTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void W8_RondWithTheItem0x30_TheKeyOfW1HoldsThroughTheEntriesOfTheObject_TheColumnIsBornAt34_ItsEightEffectsAt154_TheItemIsFreedAt182()
    {
        var key = FireTome2Key.Load("fire2_cast_pf");
        using var run = new FireTome2Run("w8", production: true);
        Assert.Null(PlayerManager.ItemStartForTests); // no seam: the entries of the object alone.
        Assert.Null(PlayerManager.ItemDriverForTests);
        Assert.Null(PlayerManager.ItemCancelForTests);

        run.Go(200);

        var wrong = FireTome2Compare.Everything(key, run, 200, 200, t => t < 34 || t >= 155);
        Assert.True(wrong.Count == 0, FireTome2Compare.Show(wrong));
        Assert.Equal(34, run.Columns.Keys.Min());
        Assert.Equal(154, run.Columns.Keys.Max());
        Assert.Equal(Enumerable.Repeat(154, 8), run.ColumnBlasts().Select(e => e.Tick));
        Assert.Equal(Tome, run.Hero[181].ItemInUse);
        Assert.Equal(182, Enumerable.Range(1, 200).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 200), t => Assert.Equal(3, run.Hero[t].Mp));
    }

    [Fact]
    public void W9_WithNoFreeSlot_NoColumnIsBorn_TheMagicIsLost_TheItemIsFreedAtTheTick34_TheHeroIsIdleAndNeverInTheState0x34()
    {
        using var run = new FireTome2Run("w9", production: true);

        run.Go(80, 1, null, tick =>
        {
            if (tick < 2 || tick > 60)
            {
                return;
            }

            var world = (IEntityWorldContext)run.Arena.Arc.Proxy;
            while (run.Arena.Arc.Proxy.SlotTable.HeldCount < EntitySlotTable.SlotCount)
            {
                Assert.NotNull(world.SpawnEntityBySpriteIndex(run.Arena.Hero, false, FillerType, 100 << 16, 100 << 16, 0, 0x10));
            }
        });

        Assert.Empty(run.Columns);
        Assert.Empty(run.Effects);
        Assert.Equal(Tome, run.Hero[33].ItemInUse);
        Assert.Equal(0u, run.Hero[34].ItemInUse); // the driver ran and answered 1 at the birth: freed.
        Assert.Equal(34, Enumerable.Range(1, 80).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 80), t => Assert.Equal(3, run.Hero[t].Mp)); // 4 -> 3 at the start, never given back.
        Assert.All(Enumerable.Range(1, 80), t => Assert.NotEqual(PlayerManager.TomeEndAnimationId, run.Hero[t].Animation));
        Assert.Equal(0u, run.Hero[80].Animation); // Idle.
        Assert.All(Enumerable.Range(1, 80), t => Assert.Equal(new int[9], run.Buffers[t])); // nothing was captured.
        Assert.All(Enumerable.Range(1, 80), t => Assert.Equal(new int[9], run.Blocks[t]));
    }

    [Fact]
    public void W10_RondWithTheItem0x30_InterruptedAtTheTick60_TheColumnIsAtTheStatus3_ItsEightEffectsAndTheDestructionAtTheTickItself_ThroughTheEntriesOfTheObject()
    {
        var key = FireTome2Key.Load("fire2_hurt60");
        using var run = new FireTome2Run("w10", production: true);

        run.Go(120, 1, 60);

        var wrong = FireTome2Compare.Everything(key, run, 120, 59);
        Assert.True(wrong.Count == 0, FireTome2Compare.Show(wrong));
        Assert.Equal(34, run.Columns.Keys.Min());
        Assert.Equal(60, run.Columns.Keys.Max());
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.Columns[60].Status);
        Assert.Equal(8, run.ColumnBlasts().Count);
        Assert.Equal(Tome, run.Hero[59].ItemInUse);
        Assert.Equal(0u, run.Hero[60].ItemInUse);
        Assert.Equal(1, run.Hero[60].Phase);
        Assert.Equal(new int[9], run.Blocks[60]); // the cancellation restored the buffer.
    }
}
