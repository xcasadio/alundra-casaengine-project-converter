#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a3a T3, the three V5 lines of <c>docs/plan-e20-annexe/e20-e0a/oracle/out/v5.json</c> that no test of the slot table read (reported by the verification of E20.e0a2, ADR-0064 said they were copied):
/// <c>invalid_full</c> (an invalid index with a full table, replayed on the world of map 14), <c>dirty</c> (a free slot with leftovers: replayed on the allocator and on a recycled slot of the world) and
/// <c>probes</c> (the 13 invalid indices of the oracle, replayed on the world). The numbers are read from the file, never recomputed. The binary also reports that the series number, the slot count word and the
/// generator did not move on a refusal: the DLL writes none of the three (no series, D-E0A-7; no generator draw at the creation, D-E0A-3), so the replay checks what it has, the held slots and the entity list.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraEntitySlotV5RowsTests
{
    private const int HeroX = 150;
    private const int HeroY = 100;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0a3a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static JsonElement V5Root()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AlundraSpawnIndexOracle.OutputFolder(), "v5.json")));
        return document.RootElement.Clone();
    }

    private static int Warnings(ArcRun arc) => arc.Log!.Warnings.Count(w => w.Contains("SpawnEntityBySpriteIndex", StringComparison.Ordinal));

    private static void FillTheTable(ArcRun arc)
    {
        for (var i = EntitySlotTable.SlotCount - arc.Proxy.SlotTable.HeldCount; i > 0; i--)
        {
            Assert.NotNull(((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 3, HeroX << 16, HeroY << 16, -1, 0x10));
        }

        Assert.Equal(EntitySlotTable.SlotCount, arc.Proxy.SlotTable.HeldCount);
    }

    private static AlundraEntityScriptProxy? Call(ArcRun arc, bool isMapPage, int index)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, isMapPage, index, HeroX << 16, HeroY << 16, 0, 0x10);

    [Fact]
    public void V5_invalid_full_AnInvalidIndexWithAFullTable_ReturnsNothing_WithTheMessageOfThePage_AndTakesNothing()
    {
        var line = V5Root().GetProperty("table").GetProperty("invalid_full");
        var index = line.GetProperty("index").GetInt32();
        Assert.DoesNotContain(index, AlundraSpawnIndexOracle.Vectors("v1").Select(v => v.Index)); // a null entry of the global page: the DLL's catalog has no prefab there either.

        using var arc = Arc("invalid-full");
        FillTheTable(arc);
        var count = arc.Entities.Count;
        var held = arc.Proxy.SlotTable.HeldCount;
        var lines = Warnings(arc);

        Assert.Equal(line.GetProperty("ret").GetInt32() != 0, Call(arc, false, index) != null);
        Assert.Equal(lines + line.GetProperty("prints").GetArrayLength(), Warnings(arc)); // one message, as the binary prints one.
        Assert.DoesNotContain("slots is full", arc.Log!.Warnings.Last(), StringComparison.Ordinal); // the message of the page, never the one of the full table (0x800239B0).
        Assert.Equal(count + line.GetProperty("count_moved").GetInt32(), arc.Entities.Count);
        Assert.Equal(held + line.GetProperty("count_moved").GetInt32(), arc.Proxy.SlotTable.HeldCount);
    }

    [Fact]
    public void V5_dirty_AFreeSlotWithLeftovers_IsTakenLowestFirst_AndTheCreationOverwritesWhatItWrites()
    {
        var dirty = V5Root().GetProperty("dirty");
        var p = dirty.GetProperty("p");
        Assert.Equal(1, dirty.GetProperty("ret").GetInt32());

        // The allocator: the lowest free slot is the one the oracle's creation landed in, and a released slot is handed out again.
        var table = new EntitySlotTable();
        Assert.Equal(p.GetProperty("slot").GetInt32(), table.Allocate());
        table.Release(p.GetProperty("slot").GetInt32());
        Assert.Equal(p.GetProperty("slot").GetInt32(), table.Allocate());

        // The world: a recycled slot with a dirty entity (a record number, life points) gives a creation whose fields are the creation's, not the leftovers'.
        using var arc = Arc("dirty");
        var first = Call(arc, false, dirty.GetProperty("index").GetInt32())!;
        var slot = first.Slot;
        first.EntityRefId = 7;
        first.Hp = 9;
        first.Status = EntityStatus.FlagToDestroy;
        arc.Proxy.RecycleDestroyedEntities();
        Assert.False(arc.Proxy.SlotTable.IsHeld(slot));

        var second = Call(arc, false, dirty.GetProperty("index").GetInt32())!;
        Assert.Equal(slot, second.Slot);
        Assert.NotSame(first, second);
        Assert.Equal(p.GetProperty("ref").GetInt32(), second.EntityRefId); // +0x48 = -1: no record.
        Assert.Equal(p.GetProperty("hp").GetInt32(), second.Hp);
        Assert.Equal(p.GetProperty("hpmax").GetInt32(), second.HpMax);
    }

    [Fact]
    public void V5_probes_TheThirteenInvalidIndicesOfTheOracle_ReturnNothing_WithOneMessage_AndTakeNothing()
    {
        var probes = V5Root().GetProperty("probes").EnumerateArray().ToList();
        Assert.Equal(13, probes.Count);
        var globalValid = AlundraSpawnIndexOracle.Vectors("v1").Select(v => v.Index).ToHashSet();
        var mapValid = AlundraSpawnIndexOracle.ValidIndicesByMap()[14].ToHashSet();

        using var arc = Arc("probes");
        foreach (var probe in probes)
        {
            var isMapPage = probe.GetProperty("page").GetInt32() == 1;
            var index = unchecked((int)probe.GetProperty("index").GetInt64());
            var why = probe.GetProperty("why").GetString();

            // The oracle patched its table entry (null, 0, 0x1000) or read past it; the same number must be invalid for the DLL's catalog and map 14.
            Assert.False(isMapPage ? mapValid.Contains(index) : globalValid.Contains(index), $"{why}: index {index} must be invalid for the DLL as it is for the oracle");

            var count = arc.Entities.Count;
            var held = arc.Proxy.SlotTable.HeldCount;
            var lines = Warnings(arc);
            var spawned = Call(arc, isMapPage, index);
            Assert.True((spawned != null) == (probe.GetProperty("ret").GetInt32() != 0), $"{why}: return");
            Assert.True(lines + probe.GetProperty("prints").GetArrayLength() == Warnings(arc), $"{why}: one message");
            Assert.True(count + probe.GetProperty("count_moved").GetInt32() == arc.Entities.Count, $"{why}: entity list");
            Assert.True(held + probe.GetProperty("count_moved").GetInt32() == arc.Proxy.SlotTable.HeldCount, $"{why}: held slots");
        }
    }
}
