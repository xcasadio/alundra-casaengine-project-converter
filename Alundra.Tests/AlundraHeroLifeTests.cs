#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Audio;

namespace Alundra.Tests;

/// <summary>
/// E20.c2a (docs/plan-e20-heros.md section 1.12): the hero's life carried by the entity, the end of <c>MovePlayer</c> on every exit, <c>UpdateItemEffectState</c> steps 1, 6 and 7, and the alarm of the low life,
/// replayed against the oracle of E20.c2-0 (<c>docs/plan-e20-annexe/e20-c2/oracle/out/</c>: the real code of <c>ALUN_CD.EXE</c> (France) in an emulator; groups E, F, G and D1) and, for the period of the
/// alarm, <c>oracle/out-c2a/alarm.txt</c> (<c>oracle/alarm.py</c>, a new file of E20.c2a). Every expected value below is the oracle's or is written from the binary's rule; none is computed by the code under test.
/// The pure functions of the DLL are fed with the oracle's inputs (inventory, stats, pad word, control flags, events) and compared column by column with what the binary produced.
/// </summary>
public sealed class AlundraHeroLifeTests
{
    // -----------------------------------------------------------------------------------------
    // The oracle files.
    // -----------------------------------------------------------------------------------------

    internal static string OracleFolder(string sub)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c2", "oracle", sub);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c2/oracle/{sub}' directory found above '{AppContext.BaseDirectory}': this test needs the E20.c2-0 oracle");
    }

    internal static JsonElement Load(string name)
    {
        var path = Path.Combine(OracleFolder("out"), name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the E20.c2-0 oracle file '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static JsonElement RunNamed(string file, string name) =>
        Load(file).GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("spec").GetProperty("name").GetString() == name);

    internal static IEnumerable<object[]> RunNames(string file) =>
        Load(file).GetProperty("runs").EnumerateArray().Select(r => new object[] { r.GetProperty("spec").GetProperty("name").GetString()! });

    private sealed class RecordingSound : ISoundPlayer
    {
        public List<int> Played { get; } = new();

        public void PlaySfx(int sfxId) => Played.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    private static ItemTables ItemTables() => Items.ItemTables.GetOrCreate(ProjectRootFinder.Find());

    private static BalanceTables BalanceTables() => Combat.BalanceTables.GetOrCreate(ProjectRootFinder.Find());

    // -----------------------------------------------------------------------------------------
    // The montage: a host with the real tables and a hero that carries its life, an inventory taken from the oracle.
    // -----------------------------------------------------------------------------------------

    private sealed class Montage
    {
        public required ContactHost Host { get; init; }

        public required AlundraEntityScriptProxy Hero { get; init; }

        public GameState State => Host.GameState;

        public EquipmentRecords Records => Host.EquipmentRecords!;
    }

    private static readonly JsonElement KitInventory = JsonDocument.Parse("{\"owned\":[1,17,25],\"item\":null}").RootElement.Clone();

    /// <summary>The inventory of an oracle run (<c>meta.inventory</c>: the owned items, the equipped accessory), the weapon slot 1 of the new game, the stats as given through the real setters, and a hero created from them.</summary>
    private static Montage BuildMontage(JsonElement inventory, int hp, int hpMax)
    {
        var host = new ContactHost { ItemTables = ItemTables(), BalanceTables = BalanceTables(), EquipmentRecords = new EquipmentRecords() };
        var state = host.GameState;
        Array.Clear(state.NumberOfItems);
        foreach (var id in inventory.GetProperty("owned").EnumerateArray())
        {
            state.NumberOfItems[id.GetInt32() * 2 + 1]++;
        }

        state.PlayerStats.WeaponId = 1;
        var item = inventory.GetProperty("item");
        state.PlayerStats.ItemId = (short)(item.ValueKind == JsonValueKind.Number ? item.GetInt32() : 0);
        PlayerManager.SetPlayerHpMax(state, hpMax);
        PlayerManager.SetPlayerHp(state, (short)hp);

        // What AdoptPlayerPawn does (PullHeroLifeFromStats), on a bare proxy that carries the hero's own balance record (+0x1C4).
        var hero = new AlundraEntityScriptProxy
        {
            ScriptHost = host,
            BalanceRecord = BalanceTables().Select(0, 0),
            HpMax = state.PlayerStats.HpMax,
            Hp = state.PlayerStats.Hp,
            LifePulledFromStats = true,
        };
        return new Montage { Host = host, Hero = hero };
    }

    private static int Column(JsonElement run, string group, string name, int tick) => run.GetProperty(group).GetProperty(name)[tick].GetInt32();

    // -----------------------------------------------------------------------------------------
    // Group G: the boots level and the hero's sheet, for each inventory (steps 1 and 6)
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> GRuns() => RunNames("G.json");

    [Theory]
    [MemberData(nameof(GRuns))]
    public void G_TheBootsLevelAndTheSheetOfEachInventory_EqualTheBinary_AtEveryTick(string run)
    {
        var oracle = RunNamed("G.json", run);
        var meta = oracle.GetProperty("meta");
        var m = BuildMontage(meta.GetProperty("inventory"), 10, 10);
        var pad = default(PadState);

        for (var tick = 1; tick <= 3; tick++)
        {
            PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);

            Assert.Equal(Column(oracle, "g", "boots", tick), m.Records.BootsLevel);
            var sheet = m.Records.HeroSheet;
            Assert.NotNull(sheet);
            for (var k = 1; k <= 11; k++)
            {
                Assert.Equal(Column(oracle, "g", "sheet_r" + k, tick), (int)sheet!.DamageResponses[k - 1]);
            }
        }

        // The first 15 bytes of the final 255-byte sheet: level, size, byte 2 (the maximum life), the eleven responses, the attack count.
        var hex = meta.GetProperty("end_sheet_full").GetString()!;
        var final = m.Records.HeroSheet!;
        for (var b = 0; b < 15; b++)
        {
            Assert.Equal(Convert.ToInt32(hex.Substring(2 * b, 2), 16), final.ByteAt(b));
        }
    }

    // -----------------------------------------------------------------------------------------
    // Group F: the regeneration (step 7), column by column
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> FRuns() => new[]
    {
        new object[] { "F1.json", "F1" },
        new object[] { "F2.json", "F2" },
        new object[] { "F4.json", "F4-full" },
        new object[] { "F4.json", "F4-zero" },
        new object[] { "F5.json", "F5-ring37" },
        new object[] { "F5.json", "F5-armour-weapon" },
    };

    [Theory]
    [MemberData(nameof(FRuns))]
    public void F_TheRegeneration_EqualsTheBinary_TheLifeTheStatsAndTheThreeCounters_AtEveryTick(string file, string run)
    {
        var oracle = RunNamed(file, run);
        var spec = oracle.GetProperty("spec");
        var hp = spec.TryGetProperty("hp", out var hpValue) ? hpValue.GetInt32() : 10;
        var hpMax = spec.TryGetProperty("hpmax", out var hpMaxValue) ? hpMaxValue.GetInt32() : 10;
        var holds = spec.GetProperty("holds").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetUInt32());
        uint? flagsEveryTick = spec.TryGetProperty("flags_every_tick", out var flagsValue) ? flagsValue.GetUInt32() : null;
        var m = BuildMontage(oracle.GetProperty("meta").GetProperty("inventory"), hp, hpMax);

        var rises = new List<int>();
        for (var tick = 1; tick <= spec.GetProperty("n").GetInt32(); tick++)
        {
            if (flagsEveryTick is { } flags)
            {
                m.State.PlayerControlFlags = flags;
            }

            var before = m.Hero.Hp;
            var pad = new PadState { ButtonsHold = holds.GetValueOrDefault(tick) };
            PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);

            Assert.Equal(Column(oracle, "hero", "hp", tick), m.Hero.Hp);
            Assert.Equal(Column(oracle, "hero", "hpmax_18", tick), m.Hero.HpMax);
            Assert.Equal(Column(oracle, "g", "stats_hp", tick), (int)m.State.PlayerStats.Hp);
            Assert.Equal(Column(oracle, "g", "stats_hpmax", tick), (int)m.State.PlayerStats.HpMax);
            for (var slot = 0; slot < 3; slot++)
            {
                Assert.Equal(Column(oracle, "g", "regen" + slot, tick), m.State.RegenerationCounters[slot]);
            }

            if (m.Hero.Hp > before)
            {
                rises.Add(tick);
            }
        }

        // The key values of the oracle summary: +1 at 201 and 402 (F1, F5-ring37), delayed to 301 by a press at tick 100 (F2), never otherwise.
        var expected = run switch
        {
            "F1" or "F5-ring37" => new[] { 201, 402 },
            "F2" => new[] { 301 },
            _ => Array.Empty<int>(),
        };
        Assert.Equal(expected, rises);
    }

    // -----------------------------------------------------------------------------------------
    // Group E: the entity wins over the stats (E2), the stats are clamped by the setters (E2-vessel50)
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> E2Runs() => RunNames("E2.json");

    [Theory]
    [MemberData(nameof(E2Runs))]
    public void E2_TheEntityWinsOverTheStats_TheMirrorClampsThem_AtEveryTick(string run)
    {
        var oracle = RunNamed("E2.json", run);
        var spec = oracle.GetProperty("spec");
        var hp = spec.TryGetProperty("hp", out var hpValue) ? hpValue.GetInt32() : 10;
        var hpMax = spec.TryGetProperty("hpmax", out var hpMaxValue) ? hpMaxValue.GetInt32() : 10;
        var events = spec.GetProperty("events").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.Clone());
        var m = BuildMontage(KitInventory, hp, hpMax);
        var pad = default(PadState);

        for (var tick = 1; tick <= spec.GetProperty("n").GetInt32(); tick++)
        {
            if (events.TryGetValue(tick, out var list))
            {
                foreach (var ev in list.EnumerateArray())
                {
                    switch (ev[0].GetString())
                    {
                        case "stats_hp":
                            PlayerManager.SetPlayerHp(m.State, (short)ev[1].GetInt32());
                            break;
                        case "stats_hpmax":
                            PlayerManager.SetPlayerHpMax(m.State, ev[1].GetInt32());
                            break;
                        case "vessel":
                            PlayerManager.IncreaseHpMaxAndFill(m.State, m.Hero);
                            break;
                        default:
                            throw new InvalidOperationException("event " + ev[0].GetString());
                    }
                }
            }

            PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);

            Assert.Equal(Column(oracle, "hero", "hp", tick), m.Hero.Hp);
            Assert.Equal(Column(oracle, "hero", "hpmax_18", tick), m.Hero.HpMax);
            Assert.Equal(Column(oracle, "g", "stats_hp", tick), (int)m.State.PlayerStats.Hp);
            Assert.Equal(Column(oracle, "g", "stats_hpmax", tick), (int)m.State.PlayerStats.HpMax);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Group D1: the END runs under every locking flag, through the real exits of MovePlayer
    // -----------------------------------------------------------------------------------------

    /// <summary>The D1 values whose tick reaches MovePlayer (the others, 0x08 0x28 0x40 0x48, stop the whole tick but the lists pass in the tick dispatcher, E20.a: MovePlayer never runs).</summary>
    public static IEnumerable<object[]> D1Runs() => new[] { "D1-00", "D1-20", "D1-24", "D1-30", "D1-10", "D1-04", "D1-80", "D1-A0" }.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(D1Runs))]
    public void D1_TheEndMirrorsTheEntityOverThePokedStats_UnderEveryLockingFlag(string run)
    {
        var oracle = RunNamed("D1.json", run);
        var flags = oracle.GetProperty("spec").GetProperty("flags_value").GetUInt32();
        var m = BuildMontage(KitInventory, 10, 10);
        m.State.PlayerControlFlags = flags;
        PlayerManager.SetPlayerHp(m.State, 6); // the poke of the D1 scenario, before tick 1.
        var pad = default(PadState);

        PlayerManager.MovePlayer(m.Hero, in pad, m.State, host: null);

        Assert.Equal(10, Column(oracle, "g", "stats_hp", 1)); // the binary: the poked 6 is back to the entity's 10 after tick 1, locked or not.
        Assert.Equal(Column(oracle, "g", "stats_hp", 1), (int)m.State.PlayerStats.Hp);
        Assert.Equal(10, m.Hero.Hp);
    }

    // -----------------------------------------------------------------------------------------
    // T1: every exit of MovePlayer reaches the END; the mirror is guarded for heroes that did not pull
    // -----------------------------------------------------------------------------------------

    public enum Exit
    {
        Normal,
        Locked,
        Frozen,
    }

    private static void RunExit(AlundraEntityScriptProxy hero, GameState state, Exit exit)
    {
        switch (exit)
        {
            case Exit.Locked:
                state.PlayerControlFlags |= GameState.PlayerControlBits.ControlLocked;
                break;
            case Exit.Frozen:
                hero.BlockedByEntity = new Entity { Name = "blocker" }; // +0x20 != 0, the DLL's BlockedByEntity branch.
                break;
        }

        var pad = default(PadState);
        PlayerManager.MovePlayer(hero, in pad, state, host: null);
    }

    [Theory]
    [InlineData(Exit.Normal)]
    [InlineData(Exit.Locked)]
    [InlineData(Exit.Frozen)]
    public void TheEnd_OfEveryExitOfMovePlayer_MirrorsTheEntityLifeIntoTheStats_HpMaxFirst(Exit exit)
    {
        var state = new GameState();
        state.PlayerStats.HpMax = 4;
        state.PlayerStats.Hp = 3;
        var hero = new AlundraEntityScriptProxy { Hp = 20, HpMax = 30, LifePulledFromStats = true };

        RunExit(hero, state, exit);

        // SetPlayerHpMax(30) first, so that SetPlayerHp(20) is clamped by 30 and not by the old maximum 4 (0x80032924 then 0x80032930).
        Assert.Equal((20, 30), ((int)state.PlayerStats.Hp, (int)state.PlayerStats.HpMax));
    }

    [Theory]
    [InlineData(Exit.Normal)]
    [InlineData(Exit.Locked)]
    [InlineData(Exit.Frozen)]
    public void TheEnd_AHeroThatNeverPulledItsLife_IsNotMirrored_TheBareMontagesKeepTheirStats(Exit exit)
    {
        var state = new GameState();
        state.PlayerStats.HpMax = 12;
        state.PlayerStats.Hp = 4;
        var hero = new AlundraEntityScriptProxy(); // Hp 0, HpMax 0 (the C# defaults), LifePulledFromStats false.

        RunExit(hero, state, exit);

        Assert.Equal((4, 12), ((int)state.PlayerStats.Hp, (int)state.PlayerStats.HpMax));
    }

    [Theory]
    [InlineData(60, 70, 50, 50)] // oracle E3-60-70 clamps: HpMax >= 0x33 is 0x32, Hp above the maximum is the maximum.
    [InlineData(9, 5, 5, 5)] // E3-5-9
    [InlineData(-3, -1, 0, 0)] // E3-neg
    [InlineData(4, 12, 4, 12)] // E3-12-4
    public void TheEnd_ClampsLikeTheSetters(int hp, int hpMax, int statsHp, int statsHpMax)
    {
        var state = new GameState();
        var hero = new AlundraEntityScriptProxy { Hp = hp, HpMax = hpMax, LifePulledFromStats = true };

        RunExit(hero, state, Exit.Normal);

        Assert.Equal((statsHp, statsHpMax), ((int)state.PlayerStats.Hp, (int)state.PlayerStats.HpMax));
    }

    // -----------------------------------------------------------------------------------------
    // T2: step 6, the combination of one response (0x80030B8C-0x80030C0C), written from the disassembly
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x10, 0x40, 0x10, 0x10)] // the new-game kit: cloth, dagger (flagged), no accessory.
    [InlineData(0x0C, 0x40, 0x10, 0x0C)] // leather.
    [InlineData(0x10, 0x40, 0x0F, 0x0F)] // bracelet: 0x10 + (0x0F - 0x10).
    [InlineData(0x80, 0x10, 0x10, 0x80)] // any 0x80 flag gives 0x80.
    [InlineData(0x10, 0x80, 0x10, 0x80)]
    [InlineData(0x10, 0x10, 0x8F, 0x80)] // the charm.
    [InlineData(0x40, 0x40, 0x40, 0x40)] // the three flagged: 0x40 as it is.
    [InlineData(0x40, 0x40, 0x10, 0x10)] // flagged armour restarts from 0x10, only the unflagged accessory adds (0).
    [InlineData(0x40, 0x14, 0x40, 0x14)] // flagged armour, unflagged weapon 0x14 adds 4.
    [InlineData(0xC0, 0x10, 0x10, 0x10)] // 0xC0 is a flag, not 0x80.
    [InlineData(0x10, 0x1F, 0x1F, 0x1F)] // 0x10 + 15 + 15 = 0x2E: capped at 0x1F.
    [InlineData(0x30, 0x10, 0x10, 0x1F)] // the bare armour byte already above 0x1F.
    [InlineData(0x01, 0x00, 0x10, 0x00)] // 1 - 16 below 0: floored.
    [InlineData(0x08, 0x40, 0x10, 0x08)] // ancient.
    public void CombineResponse_FollowsTheDisassembly(int armour, int weapon, int accessory, int expected)
    {
        Assert.Equal(expected, PlayerManager.CombineResponse(armour, weapon, accessory));
    }

    [Theory]
    [InlineData(0x1C, 3)]
    [InlineData(0x1B, 2)]
    [InlineData(0x1A, 1)]
    [InlineData(0x19, 0)] // the object 0x19 does not count.
    public void BootsLevelOf_IsTheFirstOwnedOf_1C_1B_1A(int item, int expected)
    {
        var state = new GameState();
        Array.Clear(state.NumberOfItems);
        state.NumberOfItems[item * 2 + 1] = 1;

        Assert.Equal(expected, PlayerManager.BootsLevelOf(state));

        state.NumberOfItems[0x1A * 2 + 1] = 1; // 0x1A with any other keeps the higher one.
        Assert.Equal(Math.Max(expected, 1), PlayerManager.BootsLevelOf(state));
    }

    [Fact]
    public void TheHeroSheet_IsOneReusableRecord_RewrittenInPlace_AndRebuiltOnlyWhenTheCopiedRecordChanges()
    {
        var m = BuildMontage(KitInventory, 10, 10);
        var pad = default(PadState);
        PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);
        var first = m.Records.HeroSheet;
        Assert.NotNull(first);
        Assert.All(first!.DamageResponses, b => Assert.Equal(0x10, b));

        PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);
        Assert.Same(first, m.Records.HeroSheet); // the same object, tick after tick.

        // A charm (0x36: response 6 is 0x80, the rest 0x0F) over the same armour: the same record, its bytes rewritten.
        m.State.NumberOfItems[0x36 * 2 + 1] = 1;
        m.State.PlayerStats.ItemId = 0x36;
        PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);
        Assert.Same(first, m.Records.HeroSheet);
        Assert.Equal(new byte[] { 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x80, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F }, first.DamageResponses.ToArray()); // oracle G-charm.

        // Another armour (leather 0x12): another source record, so another sheet object, with the leather's 0x0C (and the charm's combination: 0x0C + (0x0F - 0x10)... flagged 0x80 wins on response 6).
        m.State.NumberOfItems[0x12 * 2 + 1] = 1;
        PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);
        Assert.NotSame(first, m.Records.HeroSheet);
        Assert.Equal(0x80, m.Records.HeroSheet!.DamageResponses[5]);
        Assert.Equal(0x0C - 1, m.Records.HeroSheet.DamageResponses[0]); // 0x0C + (0x0F - 0x10).
    }

    [Fact]
    public void TheHeroSheet_WithoutAnArmour_IsTheHeroOwnRecord_AndEmptyWithoutAnyRecord()
    {
        var noArmour = JsonDocument.Parse("{\"owned\":[1,25],\"item\":null}").RootElement.Clone();
        var m = BuildMontage(noArmour, 10, 10);
        var pad = default(PadState);

        PlayerManager.EndOfMovePlayer(m.Hero, in pad, m.State, m.Host);

        Assert.All(m.Records.HeroSheet!.DamageResponses, b => Assert.Equal(0, b)); // oracle G-noarmour: record 0 of the balance table, all responses 0.

        var bare = new EquipmentRecords();
        PlayerManager.ComputeHeroSheet(new AlundraEntityScriptProxy(), bare); // no armour and no record of its own: nothing to copy.
        Assert.Null(bare.HeroSheet);
    }

    [Fact]
    public void TheMapLoad_EmptiesThePointersOnly_AndTheSixWordClearEmptiesTheItemWordsToo()
    {
        var records = new EquipmentRecords();
        var record = new BalanceRecord(255, 15, 0, new byte[11], Array.Empty<BalanceAttack>(), null);
        records.Set(EquipmentRecords.Armour, record, 0x12);
        records.Set(EquipmentRecords.Accessory, record, 0x36);

        records.Clear(); // 0x80044520.

        Assert.Null(records[EquipmentRecords.Armour]);
        Assert.Null(records[EquipmentRecords.Accessory]);
        Assert.Equal(0x12, records.ItemIdOf(EquipmentRecords.Armour));
        Assert.Equal(0x36, records.ItemIdOf(EquipmentRecords.Accessory));

        records.ClearAll(); // 0x800308A4-0x800308BC.

        Assert.Equal(0, records.ItemIdOf(EquipmentRecords.Armour));
        Assert.Equal(0, records.ItemIdOf(EquipmentRecords.Accessory));
    }

    // -----------------------------------------------------------------------------------------
    // T2: step 7, the rules of the regeneration written from the disassembly (0x80030C20-0x80030D3C)
    // -----------------------------------------------------------------------------------------

    private static EquipmentRecords RecordsWithPeriods(params int[] periods)
    {
        var records = new EquipmentRecords();
        for (var slot = 0; slot < periods.Length; slot++)
        {
            records.Set(slot, new BalanceRecord(255, 15, periods[slot], new byte[11], Array.Empty<BalanceAttack>(), null), slot + 1);
        }

        return records;
    }

    private static void Regenerate(AlundraEntityScriptProxy hero, GameState state, EquipmentRecords? records, uint hold = 0)
    {
        var pad = new PadState { ButtonsHold = hold };
        PlayerManager.RegenerateLife(hero, in pad, state, records);
    }

    [Fact]
    public void Regeneration_OneRecordOfByte2_3_GainsOneEveryFourTicks()
    {
        var state = new GameState();
        var hero = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 };
        var records = RecordsWithPeriods(3);

        var lives = new List<int>();
        for (var tick = 1; tick <= 8; tick++)
        {
            Regenerate(hero, state, records);
            lives.Add(hero.Hp);
        }

        Assert.Equal(new[] { 5, 5, 5, 6, 6, 6, 6, 7 }, lives); // the counter passes the byte 2 on the fourth tick: a period of byte 2 + 1.
    }

    [Theory]
    [InlineData(0x04u)]
    [InlineData(0x08u)]
    [InlineData(0x10u)]
    [InlineData(0x20u)]
    [InlineData(0x40u)]
    public void Regeneration_EachBitOf_0x7C_ClosesTheGate_AndZeroesTheCounters(uint flag)
    {
        var state = new GameState();
        var hero = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 };
        var records = RecordsWithPeriods(100, 100, 100);
        Regenerate(hero, state, records);
        Regenerate(hero, state, records);
        Assert.Equal(new[] { 2, 2, 2 }, state.RegenerationCounters.ToArray());

        state.PlayerControlFlags = flag;
        Regenerate(hero, state, records);

        Assert.Equal(new[] { 0, 0, 0 }, state.RegenerationCounters.ToArray());
        Assert.Equal(5, hero.Hp);
    }

    [Theory]
    [InlineData(0x01u)]
    [InlineData(0x02u)]
    [InlineData(0x80u)] // the scripted weapon lock is not in the mask 0x7C.
    public void Regeneration_TheBitsOutsideTheMask_DoNotCloseTheGate(uint flag)
    {
        var state = new GameState { PlayerControlFlags = flag };
        var hero = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 };

        Regenerate(hero, state, RecordsWithPeriods(100));

        Assert.Equal(1, state.RegenerationCounters[0]);
    }

    [Fact]
    public void Regeneration_AFrozenHero_AHeldButton_ADeadHero_AndAFullLife_CloseTheGate()
    {
        var records = RecordsWithPeriods(100);

        var state = new GameState();
        var frozen = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10, BlockedByEntity = new Entity { Name = "blocker" } };
        Regenerate(frozen, state, records);
        Assert.Equal(0, state.RegenerationCounters[0]);

        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records, hold: PadState.L2); // any button of the halfword, the D-pad included.
        Assert.Equal(0, state.RegenerationCounters[0]);
        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records, hold: PadState.Up);
        Assert.Equal(0, state.RegenerationCounters[0]);

        Regenerate(new AlundraEntityScriptProxy { Hp = 0, HpMax = 10 }, state, records); // Hp == 0: the regeneration stops (D-E20-20).
        Assert.Equal(0, state.RegenerationCounters[0]);
        Regenerate(new AlundraEntityScriptProxy { Hp = 10, HpMax = 10 }, state, records);
        Assert.Equal(0, state.RegenerationCounters[0]);
        Regenerate(new AlundraEntityScriptProxy { Hp = 11, HpMax = 10 }, state, records);
        Assert.Equal(0, state.RegenerationCounters[0]);

        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records, hold: 0x10000); // bit 16: out of the halfword (lhu), the gate stays open.
        Assert.Equal(1, state.RegenerationCounters[0]);
    }

    [Fact]
    public void Regeneration_TwoRecordsThatPassTogether_GainTwo_CappedAtTheMaximum_AndARecordWithoutByte2ZeroesItsCounter()
    {
        var state = new GameState();
        var hero = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 };
        var records = RecordsWithPeriods(1, 1, 0);

        Regenerate(hero, state, records);
        Assert.Equal(5, hero.Hp);
        Assert.Equal(new[] { 1, 1, 0 }, state.RegenerationCounters.ToArray());
        Regenerate(hero, state, records); // both counters pass the byte 2 = 1 on the second tick: +2 at once.
        Assert.Equal(7, hero.Hp);
        Assert.Equal(new[] { 0, 0, 0 }, state.RegenerationCounters.ToArray());

        hero.Hp = 9;
        Regenerate(hero, state, records);
        Regenerate(hero, state, records);
        Assert.Equal(10, hero.Hp); // 9 + 2 capped at HpMax.
    }

    [Fact]
    public void Regeneration_TheCountersAreSessionState_TheyContinueWithTheHeroOfTheNextMap()
    {
        var state = new GameState();
        var records = RecordsWithPeriods(100);
        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records);
        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records);

        Regenerate(new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 }, state, records);

        Assert.Equal(3, state.RegenerationCounters[0]); // nothing resets them at a map load: the binary's globals 0x80126FE8-0x80126FF0.
    }

    [Fact]
    public void Regeneration_WithoutRecords_OnlyKeepsTheCountersAtZero()
    {
        var state = new GameState();
        var hero = new AlundraEntityScriptProxy { Hp = 5, HpMax = 10 };

        Regenerate(hero, state, records: null);

        Assert.Equal(5, hero.Hp);
        Assert.Equal(new[] { 0, 0, 0 }, state.RegenerationCounters.ToArray());
    }

    // -----------------------------------------------------------------------------------------
    // T4: the low-life alarm (0x800303DC-0x80030430), against oracle/out-c2a/alarm.txt
    // -----------------------------------------------------------------------------------------

    /// <summary>name, Hp, HpMax, ticks, then the ticks at which the entity's Hp is set (tick:value): the setups of oracle/alarm.py.</summary>
    public static IEnumerable<object[]> AlarmCases()
    {
        yield return new object[] { "AL1", 2, 12, 200, "" };
        yield return new object[] { "AL2a", 2, 10, 60, "" };
        yield return new object[] { "AL2b", 3, 10, 60, "" };
        yield return new object[] { "AL2c", 10, 50, 60, "" };
        yield return new object[] { "AL2d", 11, 50, 60, "" };
        yield return new object[] { "AL2e", 1, 50, 60, "" };
        yield return new object[] { "AL2f", 0, 10, 30, "" };
        yield return new object[] { "AL3", 2, 12, 100, "21:10,41:2" };
    }

    private static int[] OracleAlarmTicks(string name)
    {
        var line = File.ReadAllLines(Path.Combine(OracleFolder("out-c2a"), "alarm.txt"))
            .Single(l => Regex.IsMatch(l, @"^PASS\s+" + name + @"\s"));
        var match = Regex.Match(line, @"sound\(400\) at \[(?<ticks>[^\]]*)\] \(expected");
        Assert.True(match.Success, line);
        var text = match.Groups["ticks"].Value;
        return text.Length == 0 ? Array.Empty<int>() : text.Split(',').Select(t => int.Parse(t.Trim())).ToArray();
    }

    private static List<int> AlarmTicksOf(int hp, int hpMax, int ticks, string hpSets, bool locked = false)
    {
        var sound = new RecordingSound();
        var host = new ContactHost { SoundPlayer = sound };
        var hero = new AlundraEntityScriptProxy { ScriptHost = host, Hp = hp, HpMax = hpMax, LifePulledFromStats = true };
        var sets = hpSets.Length == 0
            ? new Dictionary<int, int>()
            : hpSets.Split(',').Select(p => p.Split(':')).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]));

        var at = new List<int>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            if (sets.TryGetValue(tick, out var value))
            {
                hero.Hp = value;
            }

            var before = sound.Played.Count;
            PlayerAnimationEffects.Create(hero, locked ? PlayerAnimationEffects.LockedMode : PlayerAnimationEffects.FreeMode);
            at.AddRange(sound.Played.Skip(before).Where(id => id == 0x190).Select(_ => tick));
        }

        return at;
    }

    [Theory]
    [MemberData(nameof(AlarmCases))]
    public void TheLowLifeAlarm_SoundsAtTheTicksOfTheBinary(string name, int hp, int hpMax, int ticks, string hpSets)
    {
        Assert.Equal(OracleAlarmTicks(name), AlarmTicksOf(hp, hpMax, ticks, hpSets));
    }

    [Fact]
    public void TheLowLifeAlarm_RuleOfTheBinary_PeriodIs46_FirstSoundAtOnce_AndTheLockedModeNeverSounds()
    {
        // Written from the rule re-read in the binary (0x800303DC-0x80030430), independently of the oracle file: counter 0 -> -1 at the first tick, reload 0x2D, again 46 ticks later.
        Assert.Equal(new[] { 1, 47, 93 }, AlarmTicksOf(2, 12, 100, ""));
        Assert.Empty(AlarmTicksOf(2, 12, 100, "", locked: true)); // the end of the function is mode 0 only (bnez $s5, 0x800303D8).
    }

    [Fact]
    public void TheLowLifeAlarm_AHeroWithoutAScriptHost_HasNoSessionCounter_AndIsSilent()
    {
        var hero = new AlundraEntityScriptProxy { Hp = 1, HpMax = 10 };

        PlayerAnimationEffects.Create(hero, PlayerAnimationEffects.FreeMode); // the bare proxy of a montage: no session state, nothing to count, nothing thrown.

        Assert.Equal(1, hero.Hp);
    }

    [Fact]
    public void TheLowLifeAlarm_TheCounterIsSessionState_AndSurvivesTheHeroBeingRebuilt()
    {
        var sound = new RecordingSound();
        var host = new ContactHost { SoundPlayer = sound };
        var first = new AlundraEntityScriptProxy { ScriptHost = host, Hp = 2, HpMax = 12, LifePulledFromStats = true };
        PlayerAnimationEffects.Create(first, PlayerAnimationEffects.FreeMode); // tick 1: the sound, the counter 0x2D.
        Assert.Equal(PlayerAnimationEffects.LowHpAlarmReload, host.GameState.LowHpAlarmCounter);

        var second = new AlundraEntityScriptProxy { ScriptHost = host, Hp = 2, HpMax = 12, LifePulledFromStats = true }; // the hero of the next map: the global is not reset by a map load.
        PlayerAnimationEffects.Create(second, PlayerAnimationEffects.FreeMode);

        Assert.Equal(PlayerAnimationEffects.LowHpAlarmReload - 1, host.GameState.LowHpAlarmCounter);
        Assert.Single(sound.Played, id => id == 0x190);
    }
}

/// <summary>
/// E20.c2a, the parts that need a real world: the life pulled at the adoption (group E3 of the oracle), kept across a first tick, a load and a second map, the sheet and boots computed at the adoption without the
/// side effects of the fill. The ArcRun montage resets and shares the session singletons, hence the collection.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroLifeArcTests
{
    private static ArcSpec Map14(string name) =>
        new("E20c2a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);

    /// <summary>A real arrival on the map 179 (the preset of AlundraAnimationImpulseSpawnTests UJ-7): the new-game kit does not run, the inventory is the one the test gives.</summary>
    private static ArcSpec Inoa179(string name) =>
        new("E20c2a-" + name, "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, GameState.ResetAnimationId, 0));

    public static IEnumerable<object[]> E3Runs() => AlundraHeroLifeTests.RunNames("E3.json");

    [Theory]
    [MemberData(nameof(E3Runs))]
    public void E3_TheHeroIsCreatedFromTheStats_AndTheStatsSurviveTheFirstTick(string run)
    {
        var oracle = AlundraHeroLifeTests.RunNamed("E3.json", run);
        var spec = oracle.GetProperty("spec");
        var spawn = oracle.GetProperty("meta").GetProperty("spawn_from_stats");

        using var arc = new ArcRun(Map14(run), _ =>
        {
            // The oracle's order: the real SetPlayerHpMax, then SetPlayerHp.
            PlayerManager.SetPlayerHpMax(ArcRun.State, spec.GetProperty("hpmax").GetInt32());
            PlayerManager.SetPlayerHp(ArcRun.State, (short)spec.GetProperty("hp").GetInt32());
        });

        Assert.Equal((spawn.GetProperty("entity_hp").GetInt32(), spawn.GetProperty("entity_hpmax").GetInt32()), (arc.Hero.Hp, arc.Hero.HpMax));
        Assert.True(arc.Hero.LifePulledFromStats);

        arc.OneFrame();

        Assert.Equal(oracle.GetProperty("hero").GetProperty("hp")[1].GetInt32(), arc.Hero.Hp);
        Assert.Equal(oracle.GetProperty("g").GetProperty("stats_hp")[1].GetInt32(), (int)ArcRun.State.PlayerStats.Hp);
        Assert.Equal(oracle.GetProperty("g").GetProperty("stats_hpmax")[1].GetInt32(), (int)ArcRun.State.PlayerStats.HpMax);
    }

    [Fact]
    public void ALoadAt4Over12_SurvivesTheFirstTick_AndTwoMapsInARow_KeepTheStats()
    {
        // Map 14: the stats as a load leaves them (ApplyPendingLoad runs before the adoption); the hero is created at 4/12 and the first ticks do not put the stats back to 10/10.
        short hp;
        short hpMax;
        using (var first = new ArcRun(Map14("two-maps-1"), _ =>
        {
            PlayerManager.SetPlayerHpMax(ArcRun.State, 12);
            PlayerManager.SetPlayerHp(ArcRun.State, 4);
        }))
        {
            Assert.Equal((4, 12), (first.Hero.Hp, first.Hero.HpMax));
            for (var frame = 0; frame < 30; frame++)
            {
                first.OneFrame();
            }

            (hp, hpMax) = (ArcRun.State.PlayerStats.Hp, ArcRun.State.PlayerStats.HpMax);
            Assert.Equal((4, 12), ((int)hp, (int)hpMax));
        }

        // The second map: the montage resets the session singletons at the start of every arc, so the stats the first map ended with are given back by hand (in the game they are the session's);
        // what is tested is the adoption of the second world and its ticks.
        using var second = new ArcRun(Inoa179("two-maps-2"), _ =>
        {
            PlayerManager.SetPlayerHpMax(ArcRun.State, hpMax);
            PlayerManager.SetPlayerHp(ArcRun.State, hp);
        });
        Assert.Equal((4, 12), (second.Hero.Hp, second.Hero.HpMax));
        for (var frame = 0; frame < 30; frame++)
        {
            second.OneFrame();
        }

        Assert.Equal((4, 12), ((int)ArcRun.State.PlayerStats.Hp, (int)ArcRun.State.PlayerStats.HpMax));
        Assert.Equal((4, 12), (second.Hero.Hp, second.Hero.HpMax));
    }

    [Fact]
    public void TheAdoption_ComputesTheSheetAndTheBootsAtOnce_WithTheNewGameKit()
    {
        using var arc = new ArcRun(Map14("adoption-sheet"));

        var records = arc.Proxy.EquipmentRecords;
        Assert.NotNull(records.HeroSheet);
        Assert.All(records.HeroSheet!.DamageResponses, b => Assert.Equal(0x10, b)); // cloth, dagger, nothing: the kit's sheet, before any tick.
        Assert.Equal(0, records.BootsLevel);
        Assert.Null(records[EquipmentRecords.Armour]); // the map load left the pointers empty, the first tick fills them.
    }

    [Fact]
    public void TheAdoption_DoesNotRewriteTheEquippedAccessory_TheFirstTickDoes_AndUsesTheOldId()
    {
        // K1 rings2: the rings 0x33 and 0x34 owned, 0x33 equipped. The binary's first tick builds its record from the OLD id and rewrites the equipped one with the best of the slot (0x34).
        using var arc = new ArcRun(Inoa179("adoption-rings"), _ =>
        {
            ArcRun.State.NumberOfItems[0x33 * 2 + 1] = 1;
            ArcRun.State.NumberOfItems[0x34 * 2 + 1] = 1;
            ArcRun.State.PlayerStats.ItemId = 0x33;
        });

        Assert.Equal(0x33, ArcRun.State.PlayerStats.ItemId); // the adoption's own fill restored it.
        Assert.Null(arc.Proxy.EquipmentRecords[EquipmentRecords.Accessory]);

        arc.OneFrame();

        Assert.Equal(0x34, ArcRun.State.PlayerStats.ItemId);
        Assert.Equal(0x33, arc.Proxy.EquipmentRecords.ItemIdOf(EquipmentRecords.Accessory));
    }
}
