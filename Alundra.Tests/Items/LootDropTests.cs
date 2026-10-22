#nullable enable
using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Items;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, (l1)): the drop of the loot, <c>SpawnRandomLoot</c> (<c>0x80032C7C</c>) and <c>SpawnContents</c> (<c>0x80032B90</c>) with the veto (<c>0x80032A00</c>), against the vectors that T0
/// made from the real code of the binary (<c>docs/plan-e20-annexe/e20-e0c/exec/vectors/loot-spawn.json</c>, 323 cases). Every "does nothing" case is paired with the positive case that differs from it by the one thing under test.
/// The tests draw from <see cref="GameRandom"/>: they live in the collection that serialises its static state.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class LootDropTests : IDisposable
{
    private const int CoinsOfTen = 0x47;
    private const int LifeTime = 600;
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());

    public LootDropTests() => GameRandom.Reset();

    public void Dispose() => GameRandom.Reset();

    private sealed class Rig
    {
        public PickupHost Host { get; } = new();
        public RecordingSoundPlayer Sound { get; } = new();
        public GameState State { get; } = new();

        public Rig(int mpMax, bool tableFull = false)
        {
            Host.Sound = Sound;
            Host.Tables = Tables;
            Host.TableFull = tableFull;
            State.PlayerStats.MpMax = (short)mpMax;
        }
    }

    private static uint U(JsonElement e) => unchecked((uint)e.GetUInt64());

    private static int I(JsonElement e) => unchecked((int)U(e));

    private static void AssertCase(JsonElement c, Rig rig, bool result, EntityScriptProxy? container)
    {
        var id = c.GetProperty("id").GetString();
        var o = c.GetProperty("out");
        Assert.True((o.GetProperty("ret").GetInt32() == 1) == result, $"{id}: answer");
        Assert.True(U(o.GetProperty("seedAfter")) == (uint)GameRandom.RandomSeed, $"{id}: the generator after the call is {(uint)GameRandom.RandomSeed:X8}, the oracle's {U(o.GetProperty("seedAfter")):X8}");
        var sounds = o.GetProperty("sounds").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        Assert.True(sounds.SequenceEqual(rig.Sound.Requests), $"{id}: sounds [{string.Join(",", rig.Sound.Requests)}], the oracle's [{string.Join(",", sounds)}]");

        var spawn = o.GetProperty("spawn");
        if (spawn.ValueKind == JsonValueKind.Null)
        {
            Assert.True(rig.Host.Spawns.Count == 0, $"{id}: the oracle spawns nothing, the DLL asked for {rig.Host.Spawns.Count}");
            return;
        }

        Assert.True(rig.Host.Spawns.Count == 1, $"{id}: {rig.Host.Spawns.Count} spawns");
        var call = rig.Host.Spawns[0];
        Assert.True(call.Parent == null && !call.IsMapPage && call.Direction == 0, $"{id}: parent {call.Parent}, map page {call.IsMapPage}, direction {call.Direction}");
        Assert.True(call.Index == I(spawn.GetProperty("type")), $"{id}: type {call.Index}, the oracle's {I(spawn.GetProperty("type"))}");
        Assert.True((call.X, call.Y, call.Z) == (I(spawn.GetProperty("x")), I(spawn.GetProperty("y")), I(spawn.GetProperty("interfaceZ"))), $"{id}: position ({call.X}, {call.Y}, {call.Z})");

        var fields = o.GetProperty("fields");
        if (fields.ValueKind == JsonValueKind.Null)
        {
            Assert.True(rig.Host.Spawned.Count == 0, $"{id}: the table is full, nothing was created");
            return;
        }

        var born = Assert.Single(rig.Host.Spawned);
        Assert.True(born.ForceZ == I(fields.GetProperty("forceZ")), $"{id}: ForceZ");
        Assert.True(PickupAi.ReadWord274(born) == U(fields.GetProperty("word274")), $"{id}: +0x274");
        if (c.TryGetProperty("item", out var contained) && U(contained) >= ItemTables.DropRecordCount)
        {
            // Outside the 98 rows of the object table (no roll gives such an item): the binary reads the bytes that follow the table in its image (600 here), the DLL has no row and the object lives forever.
            Assert.True(born.DelayOrAngleOrEntityId == -1, $"{id}: +0x278 {born.DelayOrAngleOrEntityId}");
        }
        else
        {
            Assert.True(unchecked((uint)born.DelayOrAngleOrEntityId) == U(fields.GetProperty("word278")), $"{id}: +0x278 {born.DelayOrAngleOrEntityId}");
        }

        Assert.True(unchecked((uint)born.ItemState) == U(fields.GetProperty("state27C")), $"{id}: +0x27C");
        Assert.True(PickupAi.ReadWord280(born) == U(fields.GetProperty("word280")), $"{id}: +0x280");
        Assert.True(PickupAi.ReadWord284(born) == U(fields.GetProperty("word284")), $"{id}: +0x284");
        Assert.True(((born.Flags & EntityFlags.Collidable) == 0 && born.Flags == (0xFFFFFFFFu & ~EntityFlags.Collidable)) == fields.GetProperty("collidableCleared").GetBoolean(), $"{id}: only Collidable is cleared");
        Assert.True(container == null || container.ContentsItemId == U(c.GetProperty("item")), $"{id}: the container keeps its contents");
    }

    [Fact]
    public void TheOracle_HoldsThe323CasesOfT0_210RandomAnd113OfTheContents()
    {
        using var document = AnnexFiles.ReadVector("loot-spawn.json");
        var cases = document.RootElement.GetProperty("cases");
        Assert.Equal(323, cases.GetArrayLength());
        Assert.Equal(210, cases.EnumerateArray().Count(c => c.GetProperty("kind").GetString() == "random"));
        Assert.Equal(113, cases.EnumerateArray().Count(c => c.GetProperty("kind").GetString() == "contents"));
    }

    [Fact]
    public void SpawnRandomLoot_OnTheRandomCasesOfTheOracle_AsksForTheBinarysSpawn_WritesTheSevenFields_PlaysTheSound84_AndLeavesTheGeneratorWhereTheOracleDoes()
    {
        using var document = AnnexFiles.ReadVector("loot-spawn.json");
        var ran = 0;
        foreach (var c in document.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("kind").GetString() == "random"))
        {
            var rig = new Rig(c.GetProperty("mpMax").GetInt32(), tableFull: !c.GetProperty("spawnOk").GetBoolean());
            GameRandom.RandomSeed = U(c.GetProperty("seed"));
            var result = LootDrop.SpawnRandomLoot(rig.Host, rig.State, I(c.GetProperty("code")), I(c.GetProperty("x")), I(c.GetProperty("y")), I(c.GetProperty("z")));
            AssertCase(c, rig, result, null);
            ran++;
        }

        Assert.Equal(210, ran);
    }

    [Fact]
    public void SpawnContents_OnTheContentsCasesOfTheOracle_TakesTheItemTheFlagAndThePositionOfTheContainer_AndNeverDrawsANumber()
    {
        using var document = AnnexFiles.ReadVector("loot-spawn.json");
        var ran = 0;
        foreach (var c in document.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("kind").GetString() == "contents"))
        {
            var rig = new Rig(c.GetProperty("mpMax").GetInt32(), tableFull: !c.GetProperty("spawnOk").GetBoolean());
            var container = new EntityScriptProxy
            {
                ContentsItemId = U(c.GetProperty("item")),
                ContentsGameFlag = I(c.GetProperty("flag")),
                PosX = I(c.GetProperty("x")),
                PosY = I(c.GetProperty("y")),
                PosZ = unchecked(I(c.GetProperty("z")) - 1), // the binary's Z (+0x11C) is the DLL's PosZ plus one raw unit.
            };
            var seed = 0x13572468U; // the seed of the oracle's contents cases
            GameRandom.RandomSeed = seed;
            var result = LootDrop.SpawnContents(rig.Host, rig.State, container);
            Assert.Equal(seed, (uint)GameRandom.RandomSeed); // SpawnContents draws nothing.
            AssertCase(c, rig, result, container);
            ran++;
        }

        Assert.Equal(113, ran);
    }

    [Fact]
    public void TheVeto_CancelsTheThreeObjectsOfTheMagic_WhenMpMaxIsZero_AndOnlyThen()
    {
        foreach (var item in new[] { 0x26, 0x51, 0x52 })
        {
            var cancelled = new Rig(mpMax: 0);
            Assert.True(LootDrop.Veto(item, cancelled.State));
            Assert.False(LootDrop.SpawnContents(cancelled.Host, cancelled.State, new EntityScriptProxy { ContentsItemId = (uint)item }));
            Assert.Empty(cancelled.Host.Spawns);
            Assert.Empty(cancelled.Sound.Requests);

            var kept = new Rig(mpMax: 1);
            Assert.False(LootDrop.Veto(item, kept.State));
            Assert.True(LootDrop.SpawnContents(kept.Host, kept.State, new EntityScriptProxy { ContentsItemId = (uint)item }));
            Assert.Equal(item + 0x1E, Assert.Single(kept.Host.Spawns).Index);
            Assert.Equal(new[] { 0x54 }, kept.Sound.Requests.ToArray());
        }

        var herbWithoutMagic = new Rig(mpMax: 0);
        Assert.False(LootDrop.Veto(0x24, herbWithoutMagic.State)); // the herb is not vetoed whatever the magic.
        Assert.True(LootDrop.SpawnContents(herbWithoutMagic.Host, herbWithoutMagic.State, new EntityScriptProxy { ContentsItemId = 0x24 }));
    }

    /// <summary>The first seed whose roll of the code <c>0xF8</c> gives <paramref name="item"/>.</summary>
    private static ulong SeedGiving(int code, uint item)
    {
        for (ulong seed = 1; seed < 100000; seed++)
        {
            var word = seed;
            var chosen = LootRoll.Choose(code, () => (uint)(word = (word * 0x7D2B89DD + 0xE06A02E7) & 0xFFFFFFFF), out _);
            if (chosen == item)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("no seed found");
    }

    [Fact]
    public void TheVeto_StillConsumesTheGenerator_BeforeCancellingARandomLoot()
    {
        var seed = SeedGiving(0xF8, 0x51);
        var vetoed = new Rig(mpMax: 0);
        GameRandom.RandomSeed = seed;
        Assert.False(LootDrop.SpawnRandomLoot(vetoed.Host, vetoed.State, 0xF8, 1 << 16, 2 << 16, 3 << 16));
        var afterVeto = (uint)GameRandom.RandomSeed;
        Assert.Empty(vetoed.Host.Spawns);
        Assert.Empty(vetoed.Sound.Requests);

        var allowed = new Rig(mpMax: 4);
        GameRandom.RandomSeed = seed;
        Assert.True(LootDrop.SpawnRandomLoot(allowed.Host, allowed.State, 0xF8, 1 << 16, 2 << 16, 3 << 16));
        Assert.Equal(afterVeto, (uint)GameRandom.RandomSeed); // the same one step, with or without the veto.
        Assert.NotEqual((uint)seed, afterVeto);
        Assert.Equal(0x51 + 0x1E, Assert.Single(allowed.Host.Spawns).Index);
    }

    [Fact]
    public void AFullTable_CreatesNothing_PlaysNoSound_AndTheGeneratorMovedAsWithAFreeTable()
    {
        var seed = SeedGiving(0xFF, 0x45);
        var full = new Rig(mpMax: 4, tableFull: true);
        GameRandom.RandomSeed = seed;
        Assert.False(LootDrop.SpawnRandomLoot(full.Host, full.State, 0xFF, 5 << 16, 6 << 16, 7 << 16));
        var afterFull = (uint)GameRandom.RandomSeed;
        Assert.Single(full.Host.Spawns); // the interface was asked ...
        Assert.Empty(full.Host.Spawned);  // ... and refused: no entity, no field written
        Assert.Empty(full.Sound.Requests);

        var free = new Rig(mpMax: 4);
        GameRandom.RandomSeed = seed;
        Assert.True(LootDrop.SpawnRandomLoot(free.Host, free.State, 0xFF, 5 << 16, 6 << 16, 7 << 16));
        Assert.Equal(afterFull, (uint)GameRandom.RandomSeed);
        Assert.Equal(new[] { 0x54 }, free.Sound.Requests.ToArray());
        Assert.Single(free.Host.Spawned);
    }

    [Fact]
    public void ANoItemCode_DropsNothing_AndDrawsNothing_ButAnItemCodeDrops()
    {
        foreach (var nothing in new[] { 0x00, 0x62, 0x7F, 0x100 })
        {
            var rig = new Rig(mpMax: 4);
            GameRandom.RandomSeed = 0xCAFEF00DUL;
            Assert.False(LootDrop.SpawnRandomLoot(rig.Host, rig.State, nothing, 0, 0, 0));
            Assert.Equal(0xCAFEF00DU, (uint)GameRandom.RandomSeed);
            Assert.Empty(rig.Host.Spawns);
            Assert.Empty(rig.Sound.Requests);
        }

        var item = new Rig(mpMax: 4);
        GameRandom.RandomSeed = 0xCAFEF00DUL;
        Assert.True(LootDrop.SpawnRandomLoot(item.Host, item.State, 0x24, 0, 0, 0));
        Assert.Equal(0xCAFEF00DU, (uint)GameRandom.RandomSeed); // a fixed item draws nothing either.
        Assert.Single(item.Host.Spawns);
    }

    [Fact]
    public void TheObjectsOfTheColumnZero_LiveSixHundredTicks_TheOthersLiveForever()
    {
        var timed = new Rig(mpMax: 4);
        Assert.True(LootDrop.SpawnContents(timed.Host, timed.State, new EntityScriptProxy { ContentsItemId = CoinsOfTen }));
        Assert.Equal(LifeTime, Assert.Single(timed.Host.Spawned).DelayOrAngleOrEntityId);

        var kept = new Rig(mpMax: 4);
        Assert.True(LootDrop.SpawnContents(kept.Host, kept.State, new EntityScriptProxy { ContentsItemId = 0x3D })); // the key: column 0 is zero.
        Assert.Equal(-1, Assert.Single(kept.Host.Spawned).DelayOrAngleOrEntityId);
    }

    [Fact]
    public void SpawnContents_WritesTheFlagOfTheContainerInThePlus280_AndSpawnRandomLootAZero()
    {
        var contents = new Rig(mpMax: 4);
        Assert.True(LootDrop.SpawnContents(contents.Host, contents.State, new EntityScriptProxy { ContentsItemId = 0x45, ContentsGameFlag = 0x8123 }));
        Assert.Equal(0x8123U, PickupAi.ReadWord280(Assert.Single(contents.Host.Spawned)));

        var random = new Rig(mpMax: 4);
        Assert.True(LootDrop.SpawnRandomLoot(random.Host, random.State, 0x45, 0, 0, 0));
        Assert.Equal(0U, PickupAi.ReadWord280(Assert.Single(random.Host.Spawned)));
    }
}
