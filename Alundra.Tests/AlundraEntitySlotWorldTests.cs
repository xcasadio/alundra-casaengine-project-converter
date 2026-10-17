#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a2 (docs/plan-e20-heros.md section 1.16.2, ADR-0064), the table of slots on the REAL world of map 14 (<c>Overworld 3,1-14</c>, built by the production path of <see cref="ArcRun"/> with the export's prefabs): the hero
/// holds slot 0, the other entities the lowest free slot of 1 to 63, <c>_spawnedEntities</c> follows the order of the slots, a destroyed entity keeps its slot until <see cref="WorldProxy.RecycleDestroyedEntities"/>, the 64th
/// apparition is refused on the three paths that create entities, and an exception after the addition leaves nothing behind. Also <c>ChildEntity</c> (the root of the chain of parents) on the record path.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraEntitySlotWorldTests
{
    private const int HeroX = 150;
    private const int HeroY = 100;
    private const uint Blocked = GameState.PlayerControlBits.GameplayBlockedMask;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0a2-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    private static EntityScriptProxy? TrySpawn(ArcRun arc, EntityScriptProxy? parent = null, int index = 3)
        => Context(arc).SpawnEntityBySpriteIndex(parent ?? arc.Hero, false, index, HeroX << 16, HeroY << 16, -1, 0x10);

    private static EntityScriptProxy Spawn(ArcRun arc, EntityScriptProxy? parent = null, int index = 3) => TrySpawn(arc, parent, index)!;

    private static List<Entity> SpawnedList(ArcRun arc)
        => (List<Entity>)typeof(WorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!;

    private static int WarningsWith(ArcRun arc, string text) => arc.Log!.Warnings.Count(w => w.Contains(text, StringComparison.Ordinal));

    private static int FreeSlots(ArcRun arc) => EntitySlotTable.SlotCount - arc.Proxy.SlotTable.HeldCount;

    private static void Recycle(ArcRun arc, params EntityScriptProxy[] entities)
    {
        foreach (var entity in entities)
        {
            entity.Status = EntityStatus.FlagToDestroy;
        }

        arc.Proxy.RecycleDestroyedEntities();
    }

    private static EntityScriptProxy SlotlessProxy()
    {
        var entity = new Entity { Name = "SlotlessEntity", RootComponent = new TransformComponent(), GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        return Assert.IsType<EntityScriptProxy>(entity.GameplayProxy);
    }

    private static bool IsAscending(IEnumerable<int> slots)
    {
        var previous = int.MinValue;
        foreach (var slot in slots)
        {
            if (slot < previous)
            {
                return false;
            }

            previous = slot;
        }

        return true;
    }

    [Fact]
    public void TheHeroHoldsSlot0_TheLoadedEntitiesTakeTheSlotsFrom1InTheOrderOfTheRecords_AndTheTableHoldsThemAll()
    {
        using var arc = Arc("load");
        Assert.Equal(0, arc.Hero.Slot);
        Assert.Same(arc.Hero, arc.Entities[0]); // the hero is first in the list: its slot is the lowest.
        var others = arc.Entities.Skip(1).ToList();
        Assert.True(others.Count >= 3, "map 14 must load several entities for these tests to bite");
        Assert.Equal(Enumerable.Range(1, others.Count), others.Select(e => e.Slot));
        Assert.Equal(arc.Entities.Count, arc.Proxy.SlotTable.HeldCount);
    }

    [Fact]
    public void AWorldWithoutAHero_HoldsSlot0_FromItsConstruction()
    {
        var proxy = new WorldProxy();
        Assert.True(proxy.SlotTable.IsHeld(0));
        Assert.Equal(1, proxy.SlotTable.Allocate());
    }

    [Fact]
    public void AnApparitionTakesTheLowestFreeSlot_AndTheListFollowsTheOrderOfTheSlots()
    {
        using var arc = Arc("order");
        var a = Spawn(arc);
        var b = Spawn(arc);
        var c = Spawn(arc);
        Assert.Equal(new[] { a.Slot + 1, a.Slot + 2 }, new[] { b.Slot, c.Slot });
        Recycle(arc, b);
        Assert.Equal(-1, b.Slot);
        Assert.False(arc.Proxy.SlotTable.IsHeld(a.Slot + 1));

        var d = Spawn(arc);
        Assert.Equal(a.Slot + 1, d.Slot); // the freed slot is the lowest free one.
        var order = arc.Entities.ToList();
        Assert.Equal(order.IndexOf(a) + 1, order.IndexOf(d)); // right after a, before c: the order of the slots, not of the creation.
        Assert.Equal(order.IndexOf(d) + 1, order.IndexOf(c));
        Assert.True(IsAscending(order.Select(e => e.Slot)), "the list follows the slots");
    }

    [Fact]
    public void TheEntriesWithoutASlot_StayWhereTheyAre_AndTheInsertionSkipsThem()
    {
        using var arc = Arc("slotless");
        var a = Spawn(arc);
        var b = Spawn(arc);
        var c = Spawn(arc);
        var slotless = SlotlessProxy();
        SpawnedList(arc).Add(slotless.OwnerEntity!); // as the six test classes do by reflection: after c, no slot.

        Recycle(arc, b);
        var d = Spawn(arc); // takes b's slot: goes before c, the slotless entry stays after c.
        var order = arc.Entities.ToList();
        Assert.Equal(order.IndexOf(a) + 1, order.IndexOf(d));
        Assert.Equal(order.IndexOf(c) + 1, order.IndexOf(slotless));

        var e = Spawn(arc); // the new highest slot: no higher entry, at the end (after the slotless entry).
        order = arc.Entities.ToList();
        Assert.Equal(order.Count - 1, order.IndexOf(e));
        Assert.Equal(order.IndexOf(c) + 1, order.IndexOf(slotless));
    }

    [Fact]
    public void ADestroyedEntityKeepsItsSlot_UntilTheRecyclingPassOpens_ThenTheSlotIsFree()
    {
        using var arc = Arc("release");
        var a = Spawn(arc);
        var slot = a.Slot;
        a.Status = EntityStatus.FlagToDestroy;
        Assert.True(arc.Proxy.SlotTable.IsHeld(slot)); // the state 4 of the binary is busy until the slot pass.

        var flags = ArcRun.State.PlayerControlFlags;
        ArcRun.State.PlayerControlFlags = flags | Blocked;
        try
        {
            arc.Proxy.RecycleDestroyedEntities(); // the gate is closed (0x48): nothing is recycled, nothing is freed.
            Assert.True(arc.Proxy.SlotTable.IsHeld(slot));
            Assert.Equal(slot, a.Slot);
            Assert.Contains(a, arc.Entities);
            Assert.Equal(slot + 1, Spawn(arc).Slot); // the next apparition goes above.
        }
        finally
        {
            ArcRun.State.PlayerControlFlags = flags;
        }

        arc.Proxy.RecycleDestroyedEntities();
        Assert.False(arc.Proxy.SlotTable.IsHeld(slot));
        Assert.Equal(-1, a.Slot);
        Assert.Equal(slot, Spawn(arc).Slot); // lowest free first.
    }

    [Fact]
    public void TheSixtyFourthApparition_ThroughTheInterface_IsRefused_WithOneLogLine_AndNothingAdded()
    {
        using var arc = Arc("cap-interface");
        var free = FreeSlots(arc);
        for (var i = 0; i < free; i++)
        {
            Assert.NotNull(TrySpawn(arc));
        }

        Assert.Equal(EntitySlotTable.SlotCount, arc.Proxy.SlotTable.HeldCount);
        Assert.Equal(63, arc.Entities.Max(e => e.Slot));
        var count = arc.Entities.Count;
        var lines = WarningsWith(arc, "SpawnEntityBySpriteIndex");
        Assert.Null(TrySpawn(arc));
        Assert.Null(TrySpawn(arc)); // the 65th too.
        Assert.Equal(count, arc.Entities.Count);
        Assert.Equal(lines + 2, WarningsWith(arc, "SpawnEntityBySpriteIndex"));
        Assert.Contains("slots is full", arc.Log!.Warnings.Last(), StringComparison.Ordinal);
        Assert.Equal(EntitySlotTable.SlotCount, arc.Proxy.SlotTable.HeldCount); // a refusal takes nothing.
    }

    [Fact]
    public void AnInvalidIndexWithAFullTable_IsRefusedForTheIndex_NotForTheTable_AsTheBinaryChecksThePageFirst()
    {
        using var arc = Arc("cap-invalid");
        for (var i = FreeSlots(arc); i > 0; i--)
        {
            Assert.NotNull(TrySpawn(arc));
        }

        var count = arc.Entities.Count;
        var lines = WarningsWith(arc, "SpawnEntityBySpriteIndex");
        Assert.Null(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, true, 213, HeroX << 16, HeroY << 16, 0, 0x10)); // a number of the map page that is not valid on map 14.
        Assert.Equal(lines + 1, WarningsWith(arc, "SpawnEntityBySpriteIndex"));
        Assert.DoesNotContain("slots is full", arc.Log!.Warnings.Last(), StringComparison.Ordinal);
        Assert.Contains("does not hold this number", arc.Log.Warnings.Last(), StringComparison.Ordinal);
        Assert.Equal(count, arc.Entities.Count);
    }

    [Fact]
    public void TheSixtyFourthApparition_ByRecordId_IsRefused_WithALogLine_AndNothingAdded()
    {
        using var arc = Arc("cap-record");
        var record = arc.Entities.First(e => e.EntityRefId >= 0).EntityRefId;
        var first = arc.Proxy.SpawnEntityByRecordId(arc.Hero, record);
        Assert.NotNull(first);
        Assert.Equal(arc.Entities.Count - 1, arc.Entities.ToList().IndexOf(first!)); // takes the next slot, at the end.
        Assert.Equal(arc.Entities.Count - 1, first!.Slot);
        for (var i = FreeSlots(arc); i > 0; i--)
        {
            Assert.NotNull(TrySpawn(arc));
        }

        var count = arc.Entities.Count;
        Assert.Null(arc.Proxy.SpawnEntityByRecordId(arc.Hero, record));
        Assert.Equal(count, arc.Entities.Count);
        Assert.Contains("slots is full", arc.Log!.Warnings.Last(), StringComparison.Ordinal);
        Assert.Contains("SpawnEntityByRecordId", arc.Log.Warnings.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMapLoadWithAFullTable_SkipsTheRecordsThatFindNoSlot_AndLogsEach()
    {
        int normal;
        using (var baseline = Arc("load-baseline"))
        {
            normal = baseline.Entities.Count;
        }

        Assert.True(normal > 4, "map 14 must load more entities than the two free slots left below");

        WorldProxy.SlotTableFactoryForTests = () =>
        {
            var table = new EntitySlotTable();
            for (var i = 0; i < 61; i++)
            {
                table.Allocate(); // slots 1 to 61 held: two free, 62 and 63.
            }

            return table;
        };
        try
        {
            using var arc = Arc("load-full");
            Assert.Equal(3, arc.Entities.Count); // the hero and two records.
            Assert.Equal(new[] { 0, 62, 63 }, arc.Entities.Select(e => e.Slot));
            Assert.Equal(normal - 3, WarningsWith(arc, "slots is full"));
        }
        finally
        {
            WorldProxy.SlotTableFactoryForTests = null;
        }
    }

    [Theory]
    [InlineData("sprite-index")]
    [InlineData("record")]
    public void AnExceptionAfterTheAddition_LeavesNothingBehind_NullIsReturned_AndTheSlotIsFree(string path)
    {
        using var arc = Arc("fault-" + path);
        var held = arc.Proxy.SlotTable.HeldCount;
        var count = arc.Entities.Count;
        var expectedSlot = count; // hero 0 plus count - 1 entities: the lowest free one is count.
        var record = arc.Entities.First(e => e.EntityRefId >= 0).EntityRefId;
        Entity? thrown = null;
        WorldProxy.CreationFaultHookForTests = (p, entity) =>
        {
            if (p == path)
            {
                thrown = entity;
                throw new InvalidOperationException("injected fault after the addition");
            }
        };
        EntityScriptProxy? result;
        try
        {
            result = path == "sprite-index" ? TrySpawn(arc) : arc.Proxy.SpawnEntityByRecordId(arc.Hero, record);
        }
        finally
        {
            WorldProxy.CreationFaultHookForTests = null;
        }

        Assert.Null(result);
        Assert.NotNull(thrown);
        Assert.True(thrown!.ToBeRemoved, "the entity was handed back to the engine");
        Assert.DoesNotContain(thrown, SpawnedList(arc));
        Assert.Equal(count, arc.Entities.Count);
        Assert.Equal(held, arc.Proxy.SlotTable.HeldCount);
        Assert.Equal(-1, ((EntityScriptProxy)thrown.GameplayProxy!).Slot);
        Assert.Contains("injected fault", arc.Log!.Warnings.Last(), StringComparison.Ordinal);

        Assert.Equal(expectedSlot, Spawn(arc).Slot); // the slot was given back: the next apparition takes it.
    }

    [Fact]
    public void AnExceptionAfterTheAddition_AtTheMapLoad_SkipsThatRecordOnly_AndGivesItsSlotBack()
    {
        int normal;
        using (var baseline = Arc("fault-load-baseline"))
        {
            normal = baseline.Entities.Count;
        }

        var calls = 0;
        Entity? thrown = null;
        WorldProxy.CreationFaultHookForTests = (p, entity) =>
        {
            if (p == "load" && calls++ == 1) // the second record of the layer.
            {
                thrown = entity;
                throw new InvalidOperationException("injected fault after the addition");
            }
        };
        try
        {
            using var arc = Arc("fault-load");
            Assert.NotNull(thrown);
            Assert.True(thrown!.ToBeRemoved);
            Assert.DoesNotContain(thrown, SpawnedList(arc));
            Assert.Equal(normal - 1, arc.Entities.Count);
            Assert.Equal(Enumerable.Range(0, normal - 1), arc.Entities.Select(e => e.Slot)); // the failed record's slot went to the next one: no hole.
            Assert.Equal(normal - 1, arc.Proxy.SlotTable.HeldCount);
            Assert.Equal(1, WarningsWith(arc, "failed to spawn entity for record"));
        }
        finally
        {
            WorldProxy.CreationFaultHookForTests = null;
        }
    }

    [Fact]
    public void ChildEntity_OnTheRecordPath_IsTheRootOfTheChainOfParents_AsTheBinaryWritesIt()
    {
        using var arc = Arc("root-record");
        var record = arc.Entities.First(e => e.EntityRefId >= 0).EntityRefId;

        var child = arc.Proxy.SpawnEntityByRecordId(arc.Hero, record)!;
        var grandChild = arc.Proxy.SpawnEntityByRecordId(child, record)!;
        var greatGrandChild = arc.Proxy.SpawnEntityByRecordId(grandChild, record)!;

        Assert.Same(arc.HeroEntity, child.ParentEntity);
        Assert.Same(arc.HeroEntity, child.ChildEntity); // the hero has no root of its own: the parent itself.
        Assert.Same(child.OwnerEntity, grandChild.ParentEntity);
        Assert.Same(arc.HeroEntity, grandChild.ChildEntity); // the root of the parent.
        Assert.Same(arc.HeroEntity, greatGrandChild.ChildEntity);
    }

    [Fact]
    public void ChildEntity_ThroughBothPaths_RecordThenInterfaceThenRecord_KeepsTheSameRoot()
    {
        using var arc = Arc("root-mixed");
        var record = arc.Entities.First(e => e.EntityRefId >= 0).EntityRefId;

        var viaRecord = arc.Proxy.SpawnEntityByRecordId(arc.Hero, record)!;
        var viaInterface = Spawn(arc, viaRecord);
        var viaRecordAgain = arc.Proxy.SpawnEntityByRecordId(viaInterface, record)!;

        Assert.Same(arc.HeroEntity, viaInterface.ChildEntity);
        Assert.Same(arc.HeroEntity, viaRecordAgain.ChildEntity);
        Assert.Same(viaInterface.OwnerEntity, viaRecordAgain.ParentEntity);
    }

    [Fact]
    public void ChildEntity_OnTheRecordPath_IsNullForAMapLoadEntity_ThereIsNoParent()
    {
        using var arc = Arc("root-load");
        foreach (var entity in arc.Entities.Skip(1))
        {
            Assert.Null(entity.ParentEntity);
            Assert.Null(entity.ChildEntity);
        }
    }
}
