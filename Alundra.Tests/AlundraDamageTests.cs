#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Session;
using Alundra.Hero;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b K1, K2 and K5 (docs/plan-e20-heros.md section 1.6): the damage functions of the binary (<see cref="AlundraDamage"/>), the three equipment records (<see cref="AlundraEquipmentRecords"/>, filled by
/// <see cref="AlundraPlayerManager.FillEquipmentRecords"/> as steps 2 to 5 of <c>UpdateItemEffectState</c>) and their selection without the sprite remapping, against the oracle
/// <c>docs/plan-e20-annexe/e20-c1b/oracle/out/</c> (<c>K1.json</c>, <c>K2.json</c>: the real code of <c>ALUN_CD.EXE</c> (France), twelve inventories built by the real new-game code) and the HP cases of the D2 oracle
/// (<c>docs/plan-e20-annexe/d2-oracle-coups/out/cases.txt</c>, cases 3 and 4 and the extra x8): the expected values below are the oracle's, never computed by the code under test.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraDamageTests
{
    // -----------------------------------------------------------------------------------------
    // The oracle files.
    // -----------------------------------------------------------------------------------------

    private static string OracleFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c1b", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c1b/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the E20.c1b oracle");
    }

    private static JsonElement Load(string name)
    {
        var path = Path.Combine(OracleFolder(), name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the E20.c1b oracle file '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static readonly string[] Inventories =
    {
        "kit", "steel", "holy", "ring", "leather", "noweapon", "all", "noarmour", "bracelet", "charm", "tonic", "rings2",
    };

    public static IEnumerable<object[]> InventoryNames() => Inventories.Select(name => new object[] { name });

    private static AlundraItemTables ItemTables() => AlundraItemTables.GetOrCreate(ProjectRootFinder.Find());

    private static AlundraBalanceTables BalanceTables() => AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find());

    // -----------------------------------------------------------------------------------------
    // K1: the equipment records of each inventory, steps 2 to 5 of UpdateItemEffectState.
    // -----------------------------------------------------------------------------------------

    private static AlundraGameState StateOf(JsonElement pre)
    {
        var state = new AlundraGameState();
        CopyInventory(state, pre);
        return state;
    }

    private static void CopyInventory(AlundraGameState state, JsonElement pre)
    {
        Array.Clear(state.NumberOfItems);
        foreach (var owned in pre.GetProperty("owned").EnumerateObject())
        {
            state.NumberOfItems[int.Parse(owned.Name) * 2 + 1] = (short)owned.Value.GetInt32();
        }

        state.PlayerStats.ItemId = (short)pre.GetProperty("stats").GetProperty("item_id").GetInt32();
        state.PlayerStats.WeaponId = (short)pre.GetProperty("stats").GetProperty("weapon_id").GetInt32();
    }

    private static void AssertRecordsEqual(JsonElement expectedRecords, AlundraEquipmentRecords records, string where)
    {
        var slot = 0;
        foreach (var expected in expectedRecords.EnumerateArray())
        {
            var itemWord = expected.GetProperty("item_word").GetInt32();
            var record = records[slot];
            if (itemWord == 0)
            {
                Assert.True(record == null, $"{where}: slot {slot} must be empty");
                Assert.Equal(0, records.ItemIdOf(slot));
            }
            else
            {
                Assert.True(record != null, $"{where}: slot {slot} must hold the item {itemWord - 0x1E}");
                Assert.Equal(itemWord - 0x1E, records.ItemIdOf(slot));
                Assert.Equal(expected.GetProperty("level").GetInt32(), record!.Level);
                Assert.Equal(expected.GetProperty("size").GetInt32(), record.RecordSize);
                Assert.Equal(expected.GetProperty("max_hp").GetInt32(), record.MaxHp);
                Assert.Equal(expected.GetProperty("responses").EnumerateArray().Select(v => v.GetInt32()), record.DamageResponses.Select(b => (int)b));
                Assert.Equal(
                    expected.GetProperty("attacks").EnumerateArray().Select(a => (a[0].GetInt32(), a[1].GetInt32())),
                    record.Attacks.Select(a => (a.Attribute, a.Power)));
            }

            slot++;
        }

        Assert.Equal(AlundraEquipmentRecords.Count, slot);
    }

    [Theory]
    [MemberData(nameof(InventoryNames))]
    public void K1_TheThreeRecordsOfEachInventory_EqualTheBinary_AtTheFirstTickAndTheSecond(string name)
    {
        var inventory = Load("K1.json").GetProperty(name);
        var state = StateOf(inventory.GetProperty("pre"));
        var records = new AlundraEquipmentRecords();

        AlundraPlayerManager.FillEquipmentRecords(state, ItemTables(), BalanceTables(), 0, records);
        AssertRecordsEqual(inventory.GetProperty("records"), records, name + " (first tick)");
        Assert.Equal(inventory.GetProperty("post").GetProperty("stats").GetProperty("item_id").GetInt32(), state.PlayerStats.ItemId); // the accessory step rewrites ItemId, as 0x8004E0F8 does.

        AlundraPlayerManager.FillEquipmentRecords(state, ItemTables(), BalanceTables(), 0, records);
        var second = inventory.GetProperty("second");
        Assert.Equal(second.GetProperty("item_id").GetInt32(), state.PlayerStats.ItemId);
        var secondWords = second.GetProperty("record_words").EnumerateArray().Select(w => w[1].GetInt32()).ToArray();
        for (var slot = 0; slot < AlundraEquipmentRecords.Count; slot++)
        {
            Assert.Equal(secondWords[slot] == 0 ? 0 : secondWords[slot] - 0x1E, records.ItemIdOf(slot));
        }
    }

    [Fact]
    public void K1_TwoRingsOwned_TheFirstTickUsesTheEquippedOne_AndTheNextTheBestOfItsSlot()
    {
        var inventory = Load("K1.json").GetProperty("rings2");
        var state = StateOf(inventory.GetProperty("pre"));
        var records = new AlundraEquipmentRecords();

        AlundraPlayerManager.FillEquipmentRecords(state, ItemTables(), BalanceTables(), 0, records);
        Assert.Equal(51, records.ItemIdOf(AlundraEquipmentRecords.Accessory)); // the OLD id (0x33), as 0x8004E0F8 returns it.
        Assert.Equal(52, state.PlayerStats.ItemId); // rewritten with the best of the slot (0x34).

        AlundraPlayerManager.FillEquipmentRecords(state, ItemTables(), BalanceTables(), 0, records);
        Assert.Equal(52, records.ItemIdOf(AlundraEquipmentRecords.Accessory));
    }

    [Fact]
    public void K2_SelectEquipment_HasNoRemapping_ElseEveryItemWouldBecomeTheEntry0x1E()
    {
        var tables = BalanceTables();

        // The sprite lookup remaps 0x1F-0x7F to 0x1E; the equipment lookup reads the entry item + 0x1E itself.
        Assert.Same(tables.Select(0x1E, 0), tables.Select(0x1F + 5, 0));
        Assert.NotSame(tables.SelectEquipment(1, 0), tables.SelectEquipment(2, 0));
        Assert.Same(tables.Entry(1 + 0x1E), tables.SelectEquipment(1, 0));
        Assert.Same(tables.Entry(0x61 + 0x1E), tables.SelectEquipment(0x61, 0));
        Assert.Equal(2, tables.SelectEquipment(2, 0)!.Attacks[0].Power); // item 2, the Steel Edge: power 2.
        Assert.Equal(8, tables.SelectEquipment(4, 0)!.Attacks[0].Power); // item 4, the Holy Sword: power 8.

        // The guard n < 0x62: nothing past 0x61 (the binary prints and ends), nothing below 0.
        Assert.Null(tables.SelectEquipment(0x62, 0));
        Assert.Null(tables.SelectEquipment(0x100, 0));
        Assert.Null(tables.SelectEquipment(-1, 0));
    }

    [Fact]
    public void K2_SelectEquipment_WalksTheChainUpToTheThreshold_LikeTheSpriteLookup()
    {
        var last = new AlundraBalanceRecord(255, 15, 0, new byte[11], Array.Empty<AlundraBalanceAttack>(), null);
        var first = new AlundraBalanceRecord(3, 15, 0, new byte[11], Array.Empty<AlundraBalanceAttack>(), last);
        var tables = new AlundraBalanceTables(new Dictionary<int, AlundraBalanceRecord> { [0x1E + 7] = first });

        Assert.Same(first, tables.SelectEquipment(7, 3)); // the level 3 does not fall below the threshold 3.
        Assert.Same(last, tables.SelectEquipment(7, 4));
    }

    [Fact]
    public void K2_TheRecordsAreFilledByTheHeroTick_FromTheHost_AndLeftAloneWithoutTables()
    {
        var inventory = Load("K1.json").GetProperty("all");
        var rig = JumpHeroRig.Build(freePad: true);
        var host = rig.Host;
        host.ItemTables = ItemTables();
        host.BalanceTables = BalanceTables();
        host.EquipmentRecords = new AlundraEquipmentRecords();
        CopyInventory(host.GameState, inventory.GetProperty("pre"));

        rig.Step(); // one tick of the hero's MovePlayer, which ends with UpdateItemEffectState.
        AssertRecordsEqual(inventory.GetProperty("records"), host.EquipmentRecords, "all through the hero's tick");

        // A host without balance tables leaves the records alone (they were emptied by hand to see it). E20.c2a T2: the hand-made Clear() is the map load's (0x80044520), which empties the three POINTERS
        // only: the item words stay (here the armour's, 0x12 = 18 for the inventory 'all'); only the six-word clear of UpdateItemEffectState steps 2 to 5 (ClearAll) zeroes them, and it did not run.
        var armourWord = host.EquipmentRecords.ItemIdOf(AlundraEquipmentRecords.Armour);
        Assert.NotEqual(0, armourWord);
        host.EquipmentRecords.Clear();
        host.BalanceTables = null;
        rig.Step();
        Assert.Null(host.EquipmentRecords[AlundraEquipmentRecords.Weapon]);
        Assert.Null(host.EquipmentRecords[AlundraEquipmentRecords.Armour]);
        Assert.Equal(armourWord, host.EquipmentRecords.ItemIdOf(AlundraEquipmentRecords.Armour));
    }

    // -----------------------------------------------------------------------------------------
    // K2: the life after an attack, against the oracle line by line.
    // -----------------------------------------------------------------------------------------

    /// <summary>An entity whose current attack is the given one: what a target's <c>TouchingEntity</c> points at.</summary>
    private static Entity Attacker(int attribute, int power)
    {
        var entity = new Entity { Name = "attacker", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.CurrentAttack = new AlundraBalanceAttack(attribute, power);
        return entity;
    }

    /// <summary>The Beannoid's record (balance entry 335, level D 0) with the response byte of the attack nibble replaced.</summary>
    private static AlundraBalanceRecord BeannoidWithResponse(int attribute, int response)
    {
        var natural = BalanceTables().Select(335, 0)!;
        var responses = natural.DamageResponses.ToArray();
        var nibble = attribute & 0xF;
        Assert.InRange(nibble, 1, 11);
        responses[nibble - 1] = (byte)response;
        return new AlundraBalanceRecord(natural.Level, natural.RecordSize, natural.MaxHp, responses, natural.Attacks.ToArray(), null);
    }

    private static AlundraEquipmentRecords EquipmentOf(JsonElement attacks)
    {
        var records = new AlundraEquipmentRecords();
        var slot = 0;
        foreach (var entry in attacks.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Array)
            {
                var list = entry.EnumerateArray().Select(a => new AlundraBalanceAttack(a[0].GetInt32(), a[1].GetInt32())).ToArray();
                records.Set(slot, new AlundraBalanceRecord(255, 15 + 2 * list.Length, 0, new byte[11], list, null), 1);
            }

            slot++;
        }

        return records;
    }

    private static void AssertLine(string inventory, string section, JsonElement line, string label)
    {
        var attribute = line.GetProperty("attr").GetInt32();
        var power = line.GetProperty("power").GetInt32();
        var response = line.GetProperty("response").GetInt32();
        var latch = unchecked((uint)line.GetProperty("latch").GetInt32());
        var target = new AlundraEntityScriptProxy { Hp = line.GetProperty("hp_before").GetInt32(), TouchingEntity = Attacker(attribute, power) };

        var dead = AlundraDamage.ComputeNewHp(target, BeannoidWithResponse(attribute, response), EquipmentOf(line.GetProperty("equip_attacks")), latch);

        var where = $"{inventory}/{section}/{label} (attr 0x{attribute:X2} power {power} response 0x{response:X2} latch {(int)latch})";
        Assert.True(line.GetProperty("hp_after").GetInt32() == target.Hp, $"{where}: expected HP {line.GetProperty("hp_after").GetInt32()}, got {target.Hp}");
        Assert.True((line.GetProperty("ret").GetInt32() != 0) == dead, $"{where}: ComputeNewHp must return {line.GetProperty("ret").GetInt32()}");
    }

    [Theory]
    [MemberData(nameof(InventoryNames))]
    public void K2_TheLifeAfterEveryLineOfTheOracle_IsTheBinarys(string name)
    {
        var inventory = Load("K2.json").GetProperty(name);
        var lines = 0;
        foreach (var section in new[] { "HERO", "A10", "NH", "HT" })
        {
            if (!inventory.TryGetProperty(section, out var list) || list.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in list.EnumerateArray())
            {
                AssertLine(name, section, entry[1], entry[0].GetString() ?? string.Empty);
                lines++;
            }
        }

        Assert.True(lines >= 14 + 3 + 5, $"{name}: the oracle must hold at least the 22 lines of HERO, NH and HT (got {lines})");
    }

    /// <summary>The key values of the plan: the natural hit of each inventory on a Beannoid of 100 and of 9 life, with the damage the oracle gives.</summary>
    [Theory]
    [InlineData("kit", 98, 7)]
    [InlineData("steel", 96, 5)]
    [InlineData("holy", 84, 0)]
    [InlineData("ring", 95, 4)]
    [InlineData("leather", 98, 7)]
    [InlineData("noweapon", 99, 8)]
    [InlineData("all", 81, 0)]
    public void K2_TheNaturalHit_OnAGroundedBeannoid_WithTheSwingStateOfEachWeapon(string name, int hpAfter100, int hpAfter9)
    {
        var meta = Load("K2.json").GetProperty(name).GetProperty("meta");
        var inventory = Load("K1.json").GetProperty(name);
        var state = StateOf(inventory.GetProperty("pre"));
        var records = new AlundraEquipmentRecords();
        AlundraPlayerManager.FillEquipmentRecords(state, ItemTables(), BalanceTables(), 0, records);

        // The swing the weapon gives (E20.b), then the hero's own attack record for that animation: the state the oracle's hit pass left (steel and holy swing with 0x3F and 0x49, so their power counts twice).
        var weaponRecord = AlundraWeaponTables.RecordIndexOfItem(AlundraPlayerManager.GetItemIdFromCurrentWeapon(state, ItemTables()));
        var animation = AlundraWeaponTables.RecordByte(AlundraWeaponTables.AnimationAddress(weaponRecord, false));
        Assert.Equal(meta.GetProperty("hero_anim").GetInt32(), (int)animation);
        var attack = BalanceTables().Select(0, 0)!.AttackForAnimation(animation)!;
        Assert.Equal(meta.GetProperty("atk_attr").GetInt32(), attack.Attribute);
        Assert.Equal(meta.GetProperty("atk_power").GetInt32(), attack.Power);
        Assert.Equal(meta.GetProperty("latch").GetInt32(), (int)animation);

        var beannoid = BalanceTables().Select(335, 0)!;
        foreach (var (hpBefore, hpAfter) in new[] { (100, hpAfter100), (9, hpAfter9) })
        {
            Assert.Equal(hpAfter, AlundraDamage.ResolveBalanceTarget(attack, beannoid, hpBefore, records, animation));
        }
    }

    [Fact]
    public void K2_TheEquipmentTerm_NeedsTheBit0x80_AndCountsForAnyAttacker()
    {
        var records = new AlundraEquipmentRecords();
        records.Set(AlundraEquipmentRecords.Weapon, new AlundraBalanceRecord(255, 17, 0, new byte[11], new[] { new AlundraBalanceAttack(1, 8) }, null), 4);
        var sheet = BeannoidWithResponse(0x81, 16);

        Assert.Equal(100 - 9, AlundraDamage.ResolveBalanceTarget(new AlundraBalanceAttack(0x81, 1), sheet, 100, records, 0)); // (1 + 8) * 16 >> 4
        Assert.Equal(100 - 1, AlundraDamage.ResolveBalanceTarget(new AlundraBalanceAttack(0x01, 1), sheet, 100, records, 0)); // no bit: the power alone
        Assert.Equal(100 - 1, AlundraDamage.ResolveBalanceTarget(new AlundraBalanceAttack(0x81, 1), sheet, 100, null, 0)); // no records: nothing to add
    }

    [Fact]
    public void K2_TheLatchIndexRule_OnARecordOfThreeAttacks_IsTheOracles()
    {
        // K2.txt "synthetic 3-attack record in [2]": attacks [(1,2),(1,3),(1,5)], attacker attr 0x81 power 1 on the Beannoid, equipment [0] armour [] and [1] the basic sword (1,1).
        var kit = Load("K2.json").GetProperty("kit").GetProperty("NH");
        var seen = 0;
        foreach (var entry in kit.EnumerateArray().Where(e => (e[0].GetString() ?? string.Empty).StartsWith("synthetic 3-attack", StringComparison.Ordinal)))
        {
            AssertLine("kit", "NH", entry[1], entry[0].GetString() ?? string.Empty);
            seen++;
        }

        Assert.Equal(5, seen); // latch 16, 0, 1, 2 and -1: damage 4, 5, 7, 4, 4.
    }

    // -----------------------------------------------------------------------------------------
    // K5: the HP cases of the D2 oracle.
    // -----------------------------------------------------------------------------------------

    private static AlundraBalanceRecord SheetWithResponse(int nibble1Response)
    {
        var responses = Enumerable.Repeat((byte)16, 11).ToArray();
        responses[0] = (byte)nibble1Response;
        return new AlundraBalanceRecord(255, 15, 9, responses, Array.Empty<AlundraBalanceAttack>(), null);
    }

    [Fact]
    public void D2_Case3_AResponseOf0xC0_LeavesTheLife_AndIsNotDead()
    {
        var target = new AlundraEntityScriptProxy { Hp = 10, TouchingEntity = Attacker(1, 1) };

        var dead = AlundraDamage.ComputeNewHp(target, SheetWithResponse(0xC0), null, 0);

        Assert.False(dead);
        Assert.Equal(10, target.Hp); // "_dead": {"2": 0}, hp 10
    }

    [Theory]
    [InlineData(1, 8, 10, 9, false)] // power 1, response 8: 1*8>>4 = 0 -> 1 damage; HP 10 -> 9
    [InlineData(3, 8, 10, 9, false)] // control: power 3, response 8: 3*8>>4 = 1; HP 10 -> 9
    [InlineData(1, 8, 1, 0, true)] // minimum at HP 1: HP 1 -> 0, dead
    public void D2_Case4_TheMinimumDamageIsOne(int power, int response, int hpBefore, int hpAfter, bool dead)
    {
        var target = new AlundraEntityScriptProxy { Hp = hpBefore, TouchingEntity = Attacker(1, power) };

        Assert.Equal(dead, AlundraDamage.ComputeNewHp(target, SheetWithResponse(response), null, 0));
        Assert.Equal(hpAfter, target.Hp);
    }

    [Fact]
    public void D2_ExtraX8_AResponseOf0x40_SendsTheLifeToZero_WhateverTheDamage()
    {
        var target = new AlundraEntityScriptProxy { Hp = 10, TouchingEntity = Attacker(1, 1) };

        Assert.True(AlundraDamage.ComputeNewHp(target, SheetWithResponse(0x40), null, 0));
        Assert.Equal(0, target.Hp);
    }

    [Fact]
    public void ComputeNewHp_WithoutAnAttackerOrAnAttack_LeavesTheLife()
    {
        var withoutAttacker = new AlundraEntityScriptProxy { Hp = 7 };
        Assert.False(AlundraDamage.ComputeNewHp(withoutAttacker, SheetWithResponse(16), null, 0));
        Assert.Equal(7, withoutAttacker.Hp);

        var noSheet = new AlundraEntityScriptProxy { Hp = 7, TouchingEntity = Attacker(1, 1) };
        Assert.False(AlundraDamage.ComputeNewHp(noSheet, null, null, 0));
        Assert.Equal(7, noSheet.Hp);

        var attributeZero = new AlundraEntityScriptProxy { Hp = 7, TouchingEntity = Attacker(0, 50) };
        Assert.False(AlundraDamage.ComputeNewHp(attributeZero, SheetWithResponse(16), null, 0));
        Assert.Equal(7, attributeZero.Hp);
    }
}
