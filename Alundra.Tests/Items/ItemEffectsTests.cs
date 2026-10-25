#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Environment;
using Xunit;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Rendering.Effects;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Items;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, (l2) and (l5)): what a pickup hands to the hero, <c>GiveItem</c> (<c>0x80033F00</c>) and the text it builds, against the vectors that T0 made from the real code of the
/// binary (<c>docs/plan-e20-annexe/e20-e0c/exec/vectors/give-item.json</c>: 228 vectors of 19 objects, the effects in the order of their creation with their forces, the generator after). The effects go through the queue of the effect
/// stage (D-E0C-8, ADR-0077): before the stage applies it neither the reservoir nor the generator has moved; after, with the state of the generator imposed by the vector, the slots equal the keys of the oracle.
/// The tests draw from <see cref="GameRandom"/>: they live in the collection that serialises its static state.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class ItemEffectsTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());
    private static readonly EffectBank Bank = EffectGolden.BankOf(Root, 476);

    public ItemEffectsTests()
    {
        GameRandom.Reset();
        EtcStringTable.ResetForTests();
    }

    public void Dispose()
    {
        GameRandom.Reset();
        EtcStringTable.ResetForTests();
    }

    private static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    private static int I(JsonElement e) => unchecked((int)e.GetInt64());

    /// <summary>One vector mounted: the hero, the stats, the inventory, the stage with its reservoir prefilled like the oracle's pool, the sound recorder, the generator.</summary>
    private sealed class Rig
    {
        public JsonElement Vector { get; }
        public PickupHost Host { get; } = new();
        public RecordingSoundPlayer Sound { get; } = new();
        public GameState State { get; } = new();
        public EntityScriptProxy Hero { get; }
        public EffectStage Stage { get; } = new();
        public HashSet<int> Before { get; }
        public int Item { get; }
        public uint Seed { get; }

        public Rig(JsonElement vector)
        {
            Vector = vector;
            var s = vector.GetProperty("state");
            Item = s.GetProperty("item").GetInt32();
            Seed = U(s.GetProperty("seed"));
            var position = s.GetProperty("hero").EnumerateArray().Select(I).ToArray();
            Hero = new EntityScriptProxy
            {
                Hp = s.GetProperty("hp").GetInt32(),
                HpMax = s.GetProperty("hpmax").GetInt32(),
                PosX = position[0],
                PosY = position[1],
                PosZ = unchecked(position[2] - 1), // the binary's Z (+0x11C) is the DLL's PosZ plus one raw unit.
                LifePulledFromStats = true,
            };
            State.PlayerStats.Mp = (short)s.GetProperty("mp").GetInt32();
            State.PlayerStats.MpMax = (short)s.GetProperty("mpmax").GetInt32();
            State.PlayerStats.Money = (short)s.GetProperty("money").GetInt32();
            State.PlayerStats.FalconTemp = (short)s.GetProperty("pending").GetInt32();
            State.NumberOfItems[Item * 2 + 1] = (short)s.GetProperty("count").GetInt32();

            Stage.Load(Bank);
            if (s.GetProperty("free").ValueKind != JsonValueKind.Null)
            {
                for (var i = 0; i < EffectReservoir.SlotCount - s.GetProperty("free").GetInt32(); i++)
                {
                    Assert.NotNull(Stage.Reservoir.Create(false, 14, 0, 0, 0, 0));
                }
            }

            Before = Stage.Reservoir.ActiveSlots.Select(x => x.Index).ToHashSet();
            Host.Stage = Stage;
            Host.Sound = Sound;
            Host.Tables = Tables;
            Host.Hero = Hero;
            GameRandom.RandomSeed = Seed;
        }

        public int Give() => ItemEffects.GiveItem(Host, State, Hero, Item);

        public void Apply() => Stage.RunFrame(0, Array.Empty<bool>());

        public List<EffectSlot> Created() => Stage.Reservoir.ActiveSlots.Where(x => !Before.Contains(x.Index)).OrderBy(x => x.Index).ToList();
    }

    private static IEnumerable<JsonElement> Vectors(JsonDocument document) => document.RootElement.GetProperty("vectors").EnumerateArray();

    [Fact]
    public void TheOracle_Holds228Vectors_OfNineteenObjects_AndThe40BodiesOfHealThatT0CheckedAgainstH1()
    {
        using var document = AnnexFiles.ReadVector("give-item.json");
        Assert.Equal(228, Vectors(document).Count());
        Assert.Equal(19, Vectors(document).Select(v => v.GetProperty("state").GetProperty("item").GetInt32()).Distinct().Count());
    }

    [Fact]
    public void TheRealInventoryMaxima_AreTheOnesOfTheBinary_ForTheNineteenObjects()
    {
        using var document = AnnexFiles.ReadVector("give-item.json");
        foreach (var maximum in document.RootElement.GetProperty("inventoryMaxima").EnumerateObject())
        {
            var item = Convert.ToInt32(maximum.Name, 16);
            Assert.True(Tables.ItemsProperties[item * 5 + 3] == maximum.Value.GetInt32(), $"item {maximum.Name}: the table holds {Tables.ItemsProperties[item * 5 + 3]}, the binary {maximum.Value.GetInt32()}");
        }
    }

    [Fact]
    public void GiveItem_OnEachVectorOfTheOracle_Answers_AndLeavesTheHeroTheStatsTheInventoryTheFlagAndTheSoundsAsTheRealCodeDoes()
    {
        using var document = AnnexFiles.ReadVector("give-item.json");
        var n = 0;
        foreach (var vector in Vectors(document))
        {
            var rig = new Rig(vector);
            var o = vector.GetProperty("out");
            var where = $"item 0x{rig.Item:X2} vector {n}";

            var answer = rig.Give();

            Assert.True(answer == I(o.GetProperty("ret")), $"{where}: answer {answer}, the oracle {I(o.GetProperty("ret"))}");
            Assert.True(rig.Hero.Hp == I(o.GetProperty("hp")), $"{where}: Hp {rig.Hero.Hp}, the oracle {I(o.GetProperty("hp"))}");
            Assert.True(rig.Hero.HpMax == I(o.GetProperty("hpmax")), $"{where}: HpMax {rig.Hero.HpMax}, the oracle {I(o.GetProperty("hpmax"))}");
            Assert.True(rig.State.PlayerStats.Mp == I(o.GetProperty("mp")), $"{where}: Mp {rig.State.PlayerStats.Mp}, the oracle {I(o.GetProperty("mp"))}");
            Assert.True(rig.State.PlayerStats.Money == I(o.GetProperty("money")), $"{where}: Money {rig.State.PlayerStats.Money}, the oracle {I(o.GetProperty("money"))}");
            Assert.True(rig.State.PlayerStats.FalconTemp == I(o.GetProperty("pending")), $"{where}: FalconTemp {rig.State.PlayerStats.FalconTemp}, the oracle {I(o.GetProperty("pending"))}");
            Assert.True(rig.State.NumberOfItems[rig.Item * 2 + 1] == I(o.GetProperty("count")), $"{where}: count {rig.State.NumberOfItems[rig.Item * 2 + 1]}, the oracle {I(o.GetProperty("count"))}");
            Assert.True(((rig.State.GetFlag(1450) >> (1450 & 31)) & 1u) == (o.GetProperty("flag1450").GetBoolean() ? 1u : 0u), $"{where}: the flag 1450");
            Assert.True(o.GetProperty("sounds").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(rig.Sound.Requests), $"{where}: sounds [{string.Join(",", rig.Sound.Requests)}]");
            n++;
        }

        Assert.Equal(228, n);
    }

    [Fact]
    public void TheEffects_AreEmittedOnTheQueue_AndNeitherTheReservoirNorTheGeneratorMovesBeforeTheStageAppliesIt_ThenBoth_Move()
    {
        using var document = AnnexFiles.ReadVector("give-item.json");
        var withEffects = 0;
        var created = 0;
        foreach (var vector in Vectors(document).Where(v => v.GetProperty("out").GetProperty("effects").GetArrayLength() > 0))
        {
            var rig = new Rig(vector);
            rig.Give();

            Assert.Equal(rig.Before.Count, rig.Stage.Reservoir.ActiveCount); // nothing created at the tick of the pickup ...
            Assert.Equal(rig.Seed, (uint)GameRandom.RandomSeed);            // ... and no number drawn.

            rig.Apply();

            var oracle = vector.GetProperty("out").GetProperty("effects").EnumerateArray().Where(e => e.GetProperty("created").GetBoolean()).Count();
            Assert.Equal(oracle, rig.Created().Count);
            created += oracle;
            withEffects++;
        }

        Assert.Equal(82, withEffects);
        Assert.Equal(325, created);
    }

    [Fact]
    public void TheEffects_AfterTheApplicationOfTheQueue_AreTheKeysOfTheOracle_InTheOrderOfTheQueue_AndTheGeneratorEndsWhereTheOracleEnds()
    {
        using var document = AnnexFiles.ReadVector("give-item.json");
        var drawn = 0;
        foreach (var vector in Vectors(document))
        {
            var rig = new Rig(vector);
            var o = vector.GetProperty("out");
            var where = $"item 0x{rig.Item:X2} state {vector.GetProperty("state")}";
            rig.Give();
            rig.Apply();

            var expected = o.GetProperty("effects").EnumerateArray().Where(e => e.GetProperty("created").GetBoolean()).ToList();
            var actual = rig.Created();
            Assert.True(expected.Count == actual.Count, $"{where}: {actual.Count} effects created, the oracle {expected.Count}");
            for (var i = 0; i < expected.Count; i++)
            {
                var key = expected[i];
                var slot = actual[i];
                Assert.True(slot.TargetEffect == 14 && !slot.TargetMapTable && slot.TargetAnimation == I(key.GetProperty("anim")) && slot.RecordIndex == -1 && slot.Mode == 0, $"{where}: effect {i} identity");
                Assert.True((slot.X, slot.Y, slot.Z) == (I(key.GetProperty("x3c")), I(key.GetProperty("y40")), I(key.GetProperty("z44"))), $"{where}: effect {i} position ({slot.X}, {slot.Y}, {slot.Z}), the oracle ({I(key.GetProperty("x3c"))}, {I(key.GetProperty("y40"))}, {I(key.GetProperty("z44"))})");
                Assert.True((slot.ForceX, slot.ForceY, slot.ForceZ) == (I(key.GetProperty("fx")), I(key.GetProperty("fy")), I(key.GetProperty("fz"))), $"{where}: effect {i} forces ({slot.ForceX}, {slot.ForceY}, {slot.ForceZ}), the oracle ({I(key.GetProperty("fx"))}, {I(key.GetProperty("fy"))}, {I(key.GetProperty("fz"))})");
            }

            Assert.True(U(o.GetProperty("seed")) == (uint)GameRandom.RandomSeed, $"{where}: the generator ends at {(uint)GameRandom.RandomSeed:X8}, the oracle's {U(o.GetProperty("seed")):X8}");
            if (U(o.GetProperty("seed")) != rig.Seed)
            {
                drawn++;
            }
        }

        Assert.Equal(38, drawn);
    }

    /// <summary>A rig from explicit values (the same shape as a vector of the oracle, with no pool limit and no expected output).</summary>
    private static Rig Make(int item, int hp = 10, int hpMax = 10, int mp = 0, int mpMax = 0, int money = 0, int count = 0)
    {
        var json = $"{{\"state\":{{\"item\":{item},\"count\":{count},\"hp\":{hp},\"hpmax\":{hpMax},\"mp\":{mp},\"mpmax\":{mpMax},\"money\":{money},\"pending\":0,\"seed\":305419896,\"hero\":[6553600,6553600,1048576],\"free\":null}},\"out\":{{}}}}";
        using var document = JsonDocument.Parse(json);
        return new Rig(document.RootElement.Clone());
    }

    [Fact]
    public void AFullInventory_DrinksTheHerbAtOnce_AnInventoryWithRoomKeepsIt()
    {
        var full = Make(0x24, hp: 3, hpMax: 20, count: 9);
        full.Give();
        full.Apply();
        Assert.Equal(8, full.Hero.Hp); // + max(5, 20 / 4)
        Assert.Equal(9, full.State.NumberOfItems[0x24 * 2 + 1]);
        Assert.Single(full.Created()); // the glow of the herb.

        var room = Make(0x24, hp: 3, hpMax: 20, count: 8);
        room.Give();
        room.Apply();
        Assert.Equal(3, room.Hero.Hp);
        Assert.Equal(9, room.State.NumberOfItems[0x24 * 2 + 1]);
        Assert.Empty(room.Created());
    }

    [Theory]
    [InlineData(0x25)]
    [InlineData(0x27)]
    [InlineData(0x29)]
    public void ThePotionsAndTheBrew_AreUsedOnTheSpotWhenTheInventoryHoldsTheMaximum_AndKeptOtherwise(int item)
    {
        var full = Make(item, hp: 2, hpMax: 30, mp: 0, mpMax: 4, count: 1);
        full.Give();
        full.Apply();
        Assert.True(full.Hero.Hp > 2);
        Assert.Equal(1, full.State.NumberOfItems[item * 2 + 1]);
        Assert.NotEmpty(full.Created());

        var kept = Make(item, hp: 2, hpMax: 30, mp: 0, mpMax: 4, count: 0);
        kept.Give();
        kept.Apply();
        Assert.Equal(2, kept.Hero.Hp);
        Assert.Equal(0, kept.State.PlayerStats.Mp);
        Assert.Equal(1, kept.State.NumberOfItems[item * 2 + 1]);
        Assert.Empty(kept.Created());
    }

    [Fact]
    public void TheMoneyObjects_AddTheirAmount_AndStopAtNineThousandNineHundredNinetyNine()
    {
        foreach (var (item, amount) in new[] { (0x45, 1), (0x46, 5), (0x47, 10), (0x48, 30) })
        {
            var low = Make(item, money: 100);
            low.Give();
            Assert.Equal(100 + amount, low.State.PlayerStats.Money);

            var high = Make(item, money: 9999);
            high.Give();
            Assert.Equal(9999, high.State.PlayerStats.Money);

            var edge = Make(item, money: 10000 - amount);
            edge.Give();
            Assert.Equal(9999, edge.State.PlayerStats.Money);
        }
    }

    [Fact]
    public void TheThreeHearts_HealTheEntityOfTheHero_ByTwoAQuarterAndAHalf_AndNeverTheStatsOfTheGameState()
    {
        foreach (var (item, healed) in new[] { (0x54, 2), (0x55, 10), (0x56, 20) })
        {
            var rig = Make(item, hp: 1, hpMax: 40);
            var statsHp = rig.State.PlayerStats.Hp;
            rig.Give();
            Assert.Equal(1 + healed, rig.Hero.Hp);
            Assert.Equal(statsHp, rig.State.PlayerStats.Hp);
        }
    }

    [Fact]
    public void TheMagicDrop_AddsOnePointOfMagic_UpToTheMaximum_AndTheBigCrystalFillsIt()
    {
        foreach (var item in new[] { 0x26, 0x51 })
        {
            var room = Make(item, mp: 1, mpMax: 4, count: 1);
            room.Give();
            Assert.Equal(2, room.State.PlayerStats.Mp);

            var full = Make(item, mp: 4, mpMax: 4, count: 1);
            full.Give();
            Assert.Equal(4, full.State.PlayerStats.Mp);
        }

        var crystal = Make(0x52, mp: 0, mpMax: 3);
        crystal.Give();
        Assert.Equal(3, crystal.State.PlayerStats.Mp);

        var noMagic = Make(0x52, mp: 0, mpMax: 0);
        noMagic.Give();
        Assert.Equal(0, noMagic.State.PlayerStats.Mp);
    }

    [Fact]
    public void TheLifeVessel_RaisesTheMaximumByOneWithoutACap_FillsTheLife_AndPlaysTheSound49()
    {
        var rig = Make(0x53, hp: 12, hpMax: 50);
        rig.Give();
        Assert.Equal((51, 51), (rig.Hero.HpMax, rig.Hero.Hp));
        Assert.Equal(new[] { 0x31 }, rig.Sound.Requests.ToArray());
        rig.Apply();
        Assert.Equal(12, rig.Created().Count); // eight rings and four sparks of the vessel.
    }

    [Fact]
    public void AnObjectWithoutAnySpecialBody_GoesToTheInventory_AndNothingElse()
    {
        using var document = AnnexFiles.ReadVector("give-jump-table.json");
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().Where(r => r.GetProperty("group").GetString()!.StartsWith("simple add")).ToList();
        Assert.Equal(42, rows.Count);
        foreach (var row in rows)
        {
            var item = row.GetProperty("item").GetInt32();
            var host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Stage = new EffectStage() };
            host.Stage.Load(Bank);
            var state = new GameState();
            var hero = new EntityScriptProxy { Hp = 3, HpMax = 10, LifePulledFromStats = true };
            var answer = ItemEffects.GiveItem(host, state, hero, item);
            host.Stage.RunFrame(0, Array.Empty<bool>());

            Assert.True(state.NumberOfItems[item * 2 + 1] == (Tables.ItemsProperties[item * 5 + 3] == 0 ? 0 : 1), $"item 0x{item:X2}: the inventory");
            Assert.True((hero.Hp, state.PlayerStats.Money, state.PlayerStats.Mp, host.Stage.Reservoir.ActiveCount) == (3, 0, 0, 0), $"item 0x{item:X2}: nothing else moved");
            Assert.Empty(((RecordingSoundPlayer)host.Sound!).Requests);
            Assert.Equal(row.GetProperty("octet4").GetInt32() == 0 ? 1 : 0, answer);
        }
    }

    [Fact]
    public void TheAnswer_IsOne_ExactlyWhenTheColumnZeroOfTheObjectIsZero()
    {
        var host = new PickupHost { Tables = Tables };
        var state = new GameState();
        var hero = new EntityScriptProxy { Hp = 3, HpMax = 10 };
        Assert.Equal(1, ItemEffects.GiveItem(host, state, hero, 0x04)); // the sword: no 600 ticks.
        Assert.Equal(0, ItemEffects.GiveItem(host, state, hero, 0x45)); // the coin: it vanishes.
        Assert.Equal(0, ItemEffects.GiveItem(host, state, hero, 0x00)); // the item 0 gives nothing and answers 0.
    }

    [Fact]
    public void TheObject0x50_IsTheMagicSeed_ItRaisesTheMagicAndGivesNoItem_WhileAnotherObjectOfTheSameRangeIsGivenToTheInventory()
    {
        // E20.e0d phase C: 0x8003382C is the magic seed (the table and the effects are MagicSeedTests'): the maximum of magic + 1, the magic filled, the answer 1 (the column 0 of the table), no item in the inventory, the life untouched.
        var host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer() };
        var state = new GameState();
        var hero = new EntityScriptProxy { Hp = 3, HpMax = 10 };
        Assert.Equal(1, ItemEffects.GiveItem(host, state, hero, 0x50));
        Assert.True((hero.Hp, state.PlayerStats.Money, state.PlayerStats.Mp, state.PlayerStats.MpMax, state.NumberOfItems[0x50 * 2 + 1]) == (3, 0, 1, 1, 0));

        Assert.Equal(1, ItemEffects.GiveItem(host, state, hero, 0x4E));
        Assert.Equal(1, state.NumberOfItems[0x4E * 2 + 1]);
    }

    // ---- (l5) the composed message -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void LoadEtc(params (int Index, string Text)[] entries)
        => EtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc(entries));

    [Fact]
    public void APlacedObject_AlwaysHasItsMessage_TheNameThenASpaceThenTheGained()
    {
        LoadEtc((0x200 + 0x24, "Herbes"), (0x45, "gagné!"));

        Assert.Equal("Herbes gagné!", ItemEffects.ComposePickupMessage(0x24, 0, giveReturn: 0)); // a placed herb: GiveItem answers 0 (it vanishes) and the message is built all the same,
        Assert.Equal("Herbes gagné!", ItemEffects.ComposePickupMessage(0x24, 0, giveReturn: 1));
    }

    [Fact]
    public void ADroppedObject_HasNoMessage_WhenGiveItemAnswersZero_AndOneWhenItAnswersOne()
    {
        LoadEtc((0x200 + 0x24, "Herbes"), (0x45, "gagné!"));

        Assert.Equal(string.Empty, ItemEffects.ComposePickupMessage(0x24, 1, giveReturn: 0)); // no dropped object has a non-zero answer: none has a message.
        Assert.Equal("Herbes gagné!", ItemEffects.ComposePickupMessage(0x24, 1, giveReturn: 1)); // the rule is the binary's, kept for the answer one.
    }

    [Fact]
    public void TheSpaceThatTheCompiledTextLost_IsPutBack_SoTheBufferIsTheOneOfTheBinary_ForTheNinetySixNamesWithoutATrailingSpace()
    {
        using var document = AnnexFiles.ReadVector("pickup-message.json");
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToList();
        var etc45Raw = document.RootElement.GetProperty("etc45Raw").GetString()!;
        var etc45Dll = document.RootElement.GetProperty("etc45Dll").GetString()!;
        Assert.Equal(" gagné!", etc45Raw.Replace('é', 'é'));
        Assert.Equal("gagné!", etc45Dll);

        var entries = items.Where(i => i.GetProperty("dll").ValueKind == JsonValueKind.String).Select(i => (0x200 + i.GetProperty("item").GetInt32(), i.GetProperty("dll").GetString()!)).ToList();
        entries.Add((0x45, etc45Dll));
        LoadEtc(entries.ToArray());

        var exact = 0;
        foreach (var item in items.Where(i => i.GetProperty("raw").ValueKind == JsonValueKind.String))
        {
            var number = item.GetProperty("item").GetInt32();
            var message = ItemEffects.ComposePickupMessage(number, 0, 0);
            var binary = item.GetProperty("raw").GetString() + etc45Raw;
            if (item.GetProperty("trailingSpaceInRaw").GetBoolean())
            {
                Assert.Equal(binary.Replace("  ", " "), message); // the two names with a trailing space: the compiled text lost it, the buffer of the binary has one more space.
            }
            else
            {
                Assert.True(binary == message, $"item {number}: '{message}', the binary's '{binary}'");
                exact++;
            }
        }

        Assert.Equal(74, exact); // the 98 rows hold 76 names; two of them have a trailing space.
    }
}
