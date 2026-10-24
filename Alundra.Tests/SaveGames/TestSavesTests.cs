#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.Support.SaveGameDirectorTestSupport;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Transitions;
using Alundra.SaveGames;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.SaveGames;

/// <summary>
/// E19.d2a S1 (docs/plan-e19-opcodes.md section 1.2h.1, D-E19-33): the test-save presets of
/// <see cref="TestSaves"/>. Expected values are written by hand from the plan's table, never read from
/// the preset: new game plus the preset, nothing else.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TestSavesTests : IDisposable
{
    // Copy of the slot-name rule of the engine's ADR-0044 (SaveGameNames.SlotNameRegex,
    // CasaEngine/Framework/SaveGames/SaveGameNames.cs, internal to the engine): 1 to 32 characters among
    // a-z, 0-9, '_' and '-'.
    private static readonly Regex SlotNameRule = new(@"\A[a-z0-9_-]{1,32}\z", RegexOptions.CultureInvariant);

    private static readonly DateTime T0 = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    public TestSavesTests()
    {
        ResetSingletons();
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private sealed record Expected(
        string Name,
        int MapId,
        int TileX,
        int TileY,
        int TileZ,
        (int Word, uint Value)[] Flags,
        (int Index, ushort Value)[] Table);

    // The plan's table: G203 = word 6 bit 11, G204 = word 6 bit 12, G1651 = word 51 bit 19, G1660 = word 51 bit 28.
    private static readonly Expected Day3 = new(
        "day3-after-dream", 179, 17, 7, 1,
        [(6, 0x800u), (51, 0x10080000u)],
        [(162, (ushort)176)]);

    private static readonly Expected Day4 = new(
        "day4-meeting", 185, 5, 18, 1,
        [(6, 0x1000u), (51, 0x00F80000u)],
        [(162, (ushort)183), (176, (ushort)183)]);

    public static TheoryData<string> PresetNames => new() { Day3.Name, Day4.Name };

    // E20.f0 T3: the six weapon presets, then the full list in the order of the table. The weapon presets are written by hand from the plan's table (section 1.8, test-means.md
    // section 7), never read from the code under test.
    private static readonly string[] WeaponPresetNames =
        ["arsenal-a-m14", "arsenal-b-m14", "steel-m14", "demon-m14", "fire-m303", "arrow-m16"];

    // E20.i T5: the swim preset, then the full list in the order of the table.
    private const string SwimPresetName = "swim-m14";

    // E20.e0c T4: the two pickup recipes (docs/plan-e20-heros.md section 1.26, T0 (k)), then the full list in the order of the table.
    private static readonly string[] PickupPresetNames = ["pickup-m11", "loot-m14"];

    // E20.e T3: the cut-of-the-grass preset (docs/plan-e20-heros.md section 1.27), the last of the table: the weapon loadout (the Steel Edge in the slot 1) at the bushes of map 14.
    private const string GrassPresetName = "herbe-m14";

    // E19.d2c2 D5b T7: the lift of the map 22 (docs/plan-e19-opcodes.md section 1.2h.3.2b, D-D5B-10), the last of the table.
    private const string ElevatorPresetName = "elevator-m22";

    // E20.g4a T1: the barricade of the ship Klark (docs/plan-e20-heros.md section 1.29.1), the last of the table.
    private const string BarricadePresetName = "barricade-m392";

    // E20.g1b T2: the recipe of the lifting on the map 72 (docs/plan-e20-heros.md section 1.24.4, D-E20-71), the last of the table.
    private const string LiftPresetName = "porter-m72";

    // E20.g4b D-E20-79: the platform step of the same recipe, from the peninsula of the map 72 (docs/plan-e20-heros.md section 1.0), the last of the table.
    private const string LiftPlatformPresetName = "porter-plateforme-m72";

    private static readonly string[] AllPresetNames = [Day3.Name, Day4.Name, .. WeaponPresetNames, SwimPresetName, .. PickupPresetNames, GrassPresetName, ElevatorPresetName, BarricadePresetName, LiftPresetName, LiftPlatformPresetName];

    public static TheoryData<string> WeaponNames => new([.. WeaponPresetNames, GrassPresetName]);

    public static TheoryData<string> EveryPresetName => new(AllPresetNames);

    private static Expected For(string name) => name == Day3.Name ? Day3 : Day4;

    private static SaveGame Build(string name)
    {
        Assert.True(TestSaves.TryBuild(name, RealRules(), out var save, out var error), error);
        Assert.NotNull(save);
        return save!;
    }

    [Fact]
    public void TheFifteenPresets_AreListed_InTheOrderOfTheTable()
    {
        Assert.Equal(AllPresetNames, TestSaves.Presets.Select(p => p.Name));
        Assert.Equal(AllPresetNames.Length, TestSaves.Presets.Count);
    }

    [Theory]
    [InlineData("pickup-m11", 11, 27, 18, 10)]
    [InlineData("loot-m14", 14, 19, 34, 3)]
    public void ThePickupPresets_ArriveWhereTheRecipeSaysAndHaveTheLifeAndMagicToShowTheHeals(string name, int map, int tileX, int tileY, int tileZ)
    {
        var save = Build(name);

        Assert.Equal((map, tileX, tileY, tileZ), (save.InitialMapId, save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.Equal((4, 10, 2, 4), (save.Hp, save.HpMax, save.Mp, save.MpMax)); // hurt and short of magic, so that a heart, a herb and a crystal can be seen.
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void ABuiltPreset_IsTheNewGamePlusThePreset_InEveryField(string name)
    {
        var expected = For(name);
        var rules = RealRules();

        // The new game, built the way the game builds it.
        var newGame = new GameState();
        PlayerManager.InitializeNewGameStats(newGame);
        PlayerManager.InitializeNewGameInventory(newGame, rules.ItemTables);
        var reference = SaveGame.Capture(newGame, expected.MapId, expected.TileX, expected.TileY, expected.TileZ);
        foreach (var (word, value) in expected.Flags)
        {
            reference.GameFlags[word] |= value;
        }

        foreach (var (index, value) in expected.Table)
        {
            reference.MapIdToInternalMapIndexTable[index] = value;
        }

        var save = Build(name);

        Assert.Equal(expected.MapId, save.InitialMapId);
        Assert.Equal((expected.TileX, expected.TileY, expected.TileZ), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));

        // 64 flag words: zero except the posed words.
        Assert.Equal(64, save.GameFlags.Length);
        for (var word = 0; word < 64; word++)
        {
            var posed = expected.Flags.Where(f => f.Word == word).Select(f => f.Value).DefaultIfEmpty(0u).Single();
            Assert.Equal(posed, save.GameFlags[word]);
        }

        // 500 table entries: identity except the posed entries.
        Assert.Equal(500, save.MapIdToInternalMapIndexTable.Length);
        for (var i = 0; i < 500; i++)
        {
            var posed = expected.Table.Where(t => t.Index == i).Select(t => t.Value).DefaultIfEmpty((ushort)i).Single();
            Assert.Equal(posed, save.MapIdToInternalMapIndexTable[i]);
        }

        // 256 item counters: items 1, 17 and 25 once (NumberOfItems[3] = [35] = [51] = 1), nothing else - object 88 included.
        Assert.Equal(256, save.NumberOfItems.Length);
        for (var i = 0; i < 256; i++)
        {
            Assert.Equal(i is 3 or 35 or 51 ? 1 : 0, save.NumberOfItems[i]);
        }

        Assert.Equal(0, save.NumberOfItems[177]);

        // The nine stats: new game (HP 10/10, MP 0/0, gold 0, weapon 1, item 0, falcon 0/0), game time and retries 0.
        Assert.Equal(
            ((short)10, (short)10, (short)0, (short)0, (short)0, (short)1, (short)0, (short)0, (short)0),
            (save.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId, save.FalconTemp, save.Falcon));
        Assert.Equal(0u, save.GameTime);
        Assert.Equal((byte)0, save.DeathRetryCount);

        // And field by field against the reference built above.
        Assert.Equal(reference.GameFlags, save.GameFlags);
        Assert.Equal(reference.MapIdToInternalMapIndexTable, save.MapIdToInternalMapIndexTable);
        Assert.Equal(reference.NumberOfItems, save.NumberOfItems);
    }

    [Fact]
    public void Day4_ClearsG203_AndOnlyDay3SetsIt()
    {
        Assert.Equal(0x800u, Build(Day3.Name).GameFlags[6]);
        Assert.Equal(0x1000u, Build(Day4.Name).GameFlags[6]);
    }

    [Theory]
    [MemberData(nameof(EveryPresetName))]
    public void ABuiltPreset_IsAcceptedByTheRealRules(string name)
    {
        var save = Build(name);

        Assert.True(save.TryValidate(RealRules(), out var error), error);
    }

    [Theory]
    [MemberData(nameof(EveryPresetName))]
    public void TwoConstructions_AreEqual_ByteForByte(string name)
    {
        var a = Build(name);
        var b = Build(name);

        Assert.NotSame(a, b);
        Assert.Equal(Serialize(a), Serialize(b));
    }

    private static byte[] Serialize(SaveGame save)
    {
        // The slot's own metadata plus every field, through the public arrays and fields.
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);
        writer.Write(save.GameTime);
        writer.Write(save.InitialMapId);
        writer.Write(save.CameraTileX);
        writer.Write(save.CameraTileY);
        writer.Write(save.CameraTileZ);
        foreach (var word in save.GameFlags)
        {
            writer.Write(word);
        }

        foreach (var value in save.MapIdToInternalMapIndexTable)
        {
            writer.Write(value);
        }

        writer.Write(save.Hp);
        writer.Write(save.HpMax);
        writer.Write(save.Mp);
        writer.Write(save.MpMax);
        writer.Write(save.Money);
        writer.Write(save.WeaponId);
        writer.Write(save.ItemId);
        writer.Write(save.FalconTemp);
        writer.Write(save.Falcon);
        foreach (var count in save.NumberOfItems)
        {
            writer.Write(count);
        }

        writer.Write(save.DeathRetryCount);
        writer.Flush();
        return stream.ToArray();
    }

    [Theory]
    [MemberData(nameof(EveryPresetName))]
    public void TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule(string name)
    {
        Assert.True(TestSaves.TryFind(name, out var preset));

        Assert.Equal("test-" + name, preset!.Slot);
        Assert.Matches(SlotNameRule, preset.Slot);
    }

    [Fact]
    public void AnUnknownName_IsRefused_WithAMessage_AndNoException()
    {
        var ok = TestSaves.TryBuild("day9-nowhere", RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("day9-nowhere", error);
        Assert.False(TestSaves.TryFind("day9-nowhere", out _));
    }

    private static TestSaves.Preset Custom(uint[]? set = null, uint[]? clear = null, int mapId = 179, int tileX = 17, int tileY = 7, int tileZ = 1)
        => new(
            "custom", "test-custom", mapId, tileX, tileY, tileZ,
            set ?? [], clear ?? [], [], []);

    [Theory]
    [InlineData(0x8000u)]
    [InlineData(0x8000u + 5u)]
    [InlineData(0xFFFFu)]
    public void ATemporaryFlag_IsRefused_BecauseASaveNeverCarriesIt(uint flag)
    {
        var ok = TestSaves.TryBuild(Custom(set: [flag]), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("temporary", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2048u)]
    [InlineData(5000u)]
    [InlineData(0x7FFFu)]
    public void APersistentFlagBeyondTheSavedWords_IsRefused_InsteadOfBeingLostSilently(uint flag)
    {
        var ok = TestSaves.TryBuild(Custom(set: [flag]), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("2048", error);
    }

    [Fact]
    public void AClearedFlagOutOfRange_IsRefusedToo()
    {
        Assert.False(TestSaves.TryBuild(Custom(clear: [2048u]), RealRules(), out _, out var error));
        Assert.Contains("2048", error);
    }

    [Fact]
    public void ASaveTheRulesRefuse_IsRefused_WithTheRulesMessage()
    {
        // Map 99999 is no key of world-index.json.
        var ok = TestSaves.TryBuild(Custom(mapId: 99999), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("initialMapId", error);
    }

    [Fact]
    public void AFlagAtTheEdgeOfTheSavedWords_IsAccepted()
    {
        // Flag 2047 is the last bit of word 63, the last saved word.
        var ok = TestSaves.TryBuild(Custom(set: [2047u]), RealRules(), out var save, out var error);

        Assert.True(ok, error);
        Assert.Equal(0x80000000u, save!.GameFlags[63]);
    }

    // ---- E19.d2b B7: the hygiene of S1 (the mutations that survived the first 22 tests) ----------------------

    [Fact]
    public void APresetThatSetsThenClearsAFlag_EndsWithItCleared_AndTheOthersPosed()
    {
        // G203 is 0 in a new game, so clearing it alone proves nothing: set it and another, clear the first.
        var preset = new TestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [203u, 5u], [203u], [], []);

        Assert.True(TestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

        Assert.Equal(0u, save!.GameFlags[6]); // G203 (word 6 bit 11): set then cleared.
        Assert.Equal(1u << 5, save.GameFlags[0]); // G5: only set.
    }

    [Fact]
    public void AnItemCounter_IsWrittenToItsIndex_AndNothingElseMoves()
    {
        var preset = new TestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [], [(5, (short)1)]); // the rules cap each object (0..1 for the object 5).

        Assert.True(TestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

        for (var i = 0; i < 256; i++)
        {
            Assert.Equal(i switch { 3 or 35 or 51 => 1, 5 => 1, _ => 0 }, save!.NumberOfItems[i]);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(500)]
    public void AMapTableIndexOutsideItsTable_IsRefused(int index)
    {
        var preset = new TestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [(index, (ushort)1)], []);

        Assert.False(TestSaves.TryBuild(preset, RealRules(), out var save, out var error));

        Assert.Null(save);
        Assert.Contains("map table index " + index, error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void AnItemCounterIndexOutsideItsTable_IsRefused(int index)
    {
        var preset = new TestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [], [(index, (short)1)]);

        Assert.False(TestSaves.TryBuild(preset, RealRules(), out var save, out var error));

        Assert.Null(save);
        Assert.Contains("item counter index " + index, error);
    }

    // ---- E20.f0 T3: the weapon presets and the optional fields (docs/plan-e20-heros.md section 1.8) ----------

    /// <summary>The loadout every weapon preset carries, as RAW counter indices (2 * item + 1), written by hand: the bomb 35 (71), the herbs 36 (73, nine), the elixirs 37 and 38
    /// (75, 77), the fortifier 41 (83) and the scrolls 43, 45, 47, 49 (87, 91, 95, 99); plus the kit's items 1, 17, 25 (3, 35, 51).</summary>
    private static readonly (int Index, int Count)[] CommonCounters =
        [(3, 1), (35, 1), (51, 1), (71, 1), (73, 9), (75, 1), (77, 1), (83, 1), (87, 1), (91, 1), (95, 1), (99, 1)];

    /// <summary>Hand-written rows: preset, map, tile, equipped weapon slot, the extra raw counters (weapons, books), the item the equipped slot resolves to.</summary>
    private sealed record WeaponExpected(string Name, int Map, int X, int Y, int Z, int Slot, (int Index, int Count)[] Extra, uint CurrentItem);

    // Weapon items: 2 -> 5, 3 -> 7, 4 -> 9, 5 -> 11, 6 -> 13, 7 -> 15, 9 -> 19, 10 -> 21, 14 -> 29, 16 -> 33; books 44 -> 89, 46 -> 93, 48 -> 97, 50 -> 101.
    private static readonly (int Index, int Count)[] ArsenalAWeapons = [(11, 1), (19, 1), (29, 1), (33, 1), (15, 1)]; // items 5, 9, 14, 16, 7.

    private static readonly WeaponExpected[] WeaponRows =
    [
        new("arsenal-a-m14", 14, 8, 6, 0, 1, ArsenalAWeapons, 1),
        new("arsenal-b-m14", 14, 8, 6, 0, 1, [(9, 1), (13, 1), (21, 1), (29, 1), (33, 1), (15, 1), (89, 1), (93, 1), (97, 1), (101, 1)], 4), // items 4, 6, 10, 14, 16, 7 and the four books.
        new("steel-m14", 14, 8, 6, 0, 1, [(5, 1)], 2), // item 2; the kit's item 1 stays, the higher rank wins (correction C10).
        new("demon-m14", 14, 8, 6, 0, 1, [(7, 1)], 3), // item 3.
        new("fire-m303", 303, 38, 11, 1, 5, ArsenalAWeapons, 16),
        new("arrow-m16", 16, 34, 31, 3, 2, ArsenalAWeapons, 5),
        new(GrassPresetName, 14, 31, 43, 1, 1, [(5, 1)], 2), // E20.e: the Steel Edge (item 2) at the bushes of the map 14, tile (31, 43, 1).
    ];

    private static WeaponExpected Row(string name) => WeaponRows.Single(r => r.Name == name);

    [Fact]
    public void TheSwimPreset_IsTheNewGamePlusTheSwimBoots_OnTheBankOfThePondOfMap14()
    {
        var rules = RealRules();
        var save = Build(SwimPresetName);

        Assert.Equal(14, save.InitialMapId);
        Assert.Equal((40, 48, 1), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.All(save.GameFlags, word => Assert.Equal(0u, word));

        // The new game's counters (items 1, 17 and 25: raw indices 3, 35, 51) and the boots 0x1B (raw index 55), nothing else.
        var expectedCounts = new int[256];
        expectedCounts[3] = expectedCounts[35] = expectedCounts[51] = expectedCounts[55] = 1;
        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));
        Assert.Equal(((short)10, (short)10, (short)0, (short)0, (short)0, (short)1, (short)0), (save.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId));

        // The state a load puts in place owns the boots of level 2.
        var reloaded = new GameState();
        save.ApplyTo(reloaded);
        Assert.Equal(1, reloaded.NumberOfItems[2 * 0x1B + 1]);
        Assert.True(save.TryValidate(rules, out var error), error);
    }

    [Fact]
    public void TheElevatorPreset_IsTheNewGameAtTheRightHandLeverOfMap22()
    {
        var rules = RealRules();
        var save = Build(ElevatorPresetName);

        Assert.Equal(22, save.InitialMapId);
        Assert.Equal((46, 8, 2), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.All(save.GameFlags, word => Assert.Equal(0u, word)); // the state of the lever (T1, T3) is temporary and not saved: nothing is posed.

        // The new game's counters (items 1, 17 and 25: raw indices 3, 35, 51), nothing else.
        var expectedCounts = new int[256];
        expectedCounts[3] = expectedCounts[35] = expectedCounts[51] = 1;
        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));
        Assert.True(save.TryValidate(rules, out var error), error);
    }

    [Fact]
    public void TheBarricadePreset_IsTheNewGameAtTheShipKlarksStart_WithOnlyTheFlagG1641()
    {
        var rules = RealRules();
        var save = Build(BarricadePresetName);

        // The ArcSpec of the arc A5 (ShipCorridorArcTests): map 392, tile (29, 13), level 4, flag 1641 = word 51 (1641 >> 5), bit 9 (1641 & 31) = 0x200.
        Assert.Equal(392, save.InitialMapId);
        Assert.Equal((29, 13, 4), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        for (var word = 0; word < save.GameFlags.Length; word++)
        {
            Assert.Equal(word == 51 ? 0x200u : 0u, save.GameFlags[word]);
        }

        // The new game's counters (items 1, 17 and 25: raw indices 3, 35, 51), nothing else.
        var expectedCounts = new int[256];
        expectedCounts[3] = expectedCounts[35] = expectedCounts[51] = 1;
        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));
        Assert.True(save.TryValidate(rules, out var error), error);
    }

    [Fact]
    public void TheLiftPreset_IsTheNewGameOnTheWestBankOfTheCoastalCave_WithNoFlag()
    {
        var rules = RealRules();
        var save = Build(LiftPresetName);

        // Map 72 (Coastal Cave), tile (10, 12), level 3 (the bank, 48 px): the jar of the record 37 is 48 px to the west, the barrel of the record 1 behind it (docs/plan-e20-annexe/e20-g1b/README.md, T0 (a)).
        Assert.Equal(72, save.InitialMapId);
        Assert.Equal((10, 12, 3), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.All(save.GameFlags, word => Assert.Equal(0u, word));

        // The new game's counters (items 1, 17 and 25: raw indices 3, 35, 51), nothing else.
        var expectedCounts = new int[256];
        expectedCounts[3] = expectedCounts[35] = expectedCounts[51] = 1;
        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));
        Assert.True(save.TryValidate(rules, out var error), error);
    }

    [Fact]
    public void TheLiftPlatformPreset_IsTheNewGameOnThePeninsulaOfTheCoastalCave_WithNoFlag()
    {
        var rules = RealRules();
        var save = Build(LiftPlatformPresetName);

        // Map 72, tile (22, 15), level 5 (the peninsula, 80 px high: pixel (540, 248)), next to the west end of the platform of the record 2 (D-E20-79).
        Assert.Equal(72, save.InitialMapId);
        Assert.Equal((22, 15, 5), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.All(save.GameFlags, word => Assert.Equal(0u, word));

        // The new game's counters (items 1, 17 and 25: raw indices 3, 35, 51), nothing else.
        var expectedCounts = new int[256];
        expectedCounts[3] = expectedCounts[35] = expectedCounts[51] = 1;
        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));
        Assert.True(save.TryValidate(rules, out var error), error);
    }

    [Theory]
    [MemberData(nameof(WeaponNames))]
    public void AWeaponPreset_IsTheNewGamePlusItsLoadout_InEveryField(string name)
    {
        var row = Row(name);

        var save = Build(name);

        Assert.Equal(row.Map, save.InitialMapId);
        Assert.Equal((row.X, row.Y, row.Z), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.All(save.GameFlags, word => Assert.Equal(0u, word));
        for (var i = 0; i < 500; i++)
        {
            Assert.Equal((ushort)i, save.MapIdToInternalMapIndexTable[i]);
        }

        var expectedCounts = new int[256];
        foreach (var (index, count) in CommonCounters.Concat(row.Extra))
        {
            expectedCounts[index] = count;
        }

        Assert.Equal(expectedCounts, save.NumberOfItems.Select(c => (int)c));

        // Life 4 of 10, magic 4 of 4, no gold, the preset's weapon slot, no Circle item, no falcon.
        Assert.Equal(
            ((short)4, (short)10, (short)4, (short)4, (short)0, (short)row.Slot, (short)0, (short)0, (short)0),
            (save.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId, save.FalconTemp, save.Falcon));
        Assert.Equal(0u, save.GameTime);
        Assert.Equal((byte)0, save.DeathRetryCount);
    }

    [Theory]
    [MemberData(nameof(WeaponNames))]
    public void AWeaponPreset_ReloadedByTheLoadPath_GivesTheChosenWeapon_TheLifeTheMagicAndTheObjects(string name)
    {
        var row = Row(name);
        var rules = RealRules();
        var reloaded = new GameState();

        Build(name).ApplyTo(reloaded); // the state a load puts in place: the aller-retour of the save.

        Assert.Equal(row.CurrentItem, PlayerManager.GetItemIdFromCurrentWeapon(reloaded, rules.ItemTables));
        Assert.Equal(((short)4, (short)10, (short)4, (short)4), (reloaded.PlayerStats.Hp, reloaded.PlayerStats.HpMax, reloaded.PlayerStats.Mp, reloaded.PlayerStats.MpMax));
        Assert.Equal((short)row.Slot, reloaded.PlayerStats.WeaponId);
        Assert.Equal(9, reloaded.NumberOfItems[73]); // the herbs.
        Assert.Equal(1, reloaded.NumberOfItems[71]); // the bomb.
        foreach (var (index, count) in CommonCounters.Concat(row.Extra))
        {
            Assert.Equal(count, reloaded.NumberOfItems[index]);
        }
    }

    [Fact]
    public void ArsenalA_And_ArsenalB_GiveOneWeaponPerSlot_TheHighestRankOfTheSlotEquipped()
    {
        var rules = RealRules();
        var expected = new Dictionary<string, uint[]>
        {
            // Slots 1 to 6: the sword (arsenal-b: the Holy Sword 4 over the kit's dagger 1), the bow, the flail, the ice rod, the fire rod, the staff.
            ["arsenal-a-m14"] = [1, 5, 9, 14, 16, 7],
            ["arsenal-b-m14"] = [4, 6, 10, 14, 16, 7],
        };

        foreach (var (name, items) in expected)
        {
            var state = new GameState();
            Build(name).ApplyTo(state);
            for (var slot = 1; slot <= 6; slot++)
            {
                PlayerManager.SetPlayerWeaponId(state, rules.ItemTables, slot);
                Assert.Equal(items[slot - 1], PlayerManager.GetItemIdFromCurrentWeapon(state, rules.ItemTables));
            }
        }
    }

    [Fact]
    public void TheOptionalFields_AreAppliedThroughTheSetters_AfterTheKit_AndTheEdgesAreAccepted()
    {
        var preset = new TestSaves.Preset(
            "custom", "test-custom", 14, 8, 6, 0, [], [], [], [(5, (short)1)],
            Weapon: -1, Item: 36, Hp: 50, HpMax: 50, Mp: 4, MpMax: 4, Money: 9999);

        Assert.True(TestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

        Assert.Equal(
            ((short)50, (short)50, (short)4, (short)4, (short)9999, (short)-1, (short)36),
            (save!.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId));
        Assert.Equal(1, save.NumberOfItems[5]);
    }

    [Fact]
    public void WithoutTheOptionalFields_ThePresetKeepsTheNewGamesValues()
    {
        var preset = new TestSaves.Preset("custom", "test-custom", 14, 8, 6, 0, [], [], [], []);

        Assert.True(TestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

        Assert.Equal(
            ((short)10, (short)10, (short)0, (short)0, (short)0, (short)1, (short)0),
            (save!.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId));
    }

    private static TestSaves.Preset WithFields(
        int? weapon = null, int? item = null, int? hp = null, int? hpMax = null, int? mp = null, int? mpMax = null, int? money = null)
        => new("custom", "test-custom", 14, 8, 6, 0, [], [], [], [], weapon, item, hp, hpMax, mp, mpMax, money);

    public static TheoryData<string, int?, int?, int?, int?, int?, int?, int?> OutOfDomain => new()
    {
        // field named in the message, then weapon, item, hp, hpMax, mp, mpMax, money.
        { "hpMax ", null, null, null, 51, null, null, null },
        { "hpMax ", null, null, null, -1, null, null, null },
        { "hp ", null, null, 11, 10, null, null, null }, // 11 over the maximum declared with it.
        { "hp ", null, null, 11, null, null, null, null }, // 11 over the new game's maximum 10.
        { "hp ", null, null, -1, null, null, null, null },
        { "mpMax ", null, null, null, null, null, 5, null },
        { "mpMax ", null, null, null, null, null, -1, null },
        { "mp ", null, null, null, null, 5, 4, null },
        { "mp ", null, null, null, null, 1, null, null }, // 1 over the new game's magic maximum 0.
        { "money ", null, null, null, null, null, null, 10000 },
        { "money ", null, null, null, null, null, null, -1 },
        { "weapon ", 0, null, null, null, null, null, null },
        { "weapon ", 7, null, null, null, null, null, null },
        { "weapon ", -2, null, null, null, null, null, null },
        { "item ", null, 99, null, null, null, null, null },
        { "item ", null, -1, null, null, null, null, null },
    };

    [Theory]
    [MemberData(nameof(OutOfDomain))]
    public void AnOptionalFieldOutsideItsDomain_IsRefusedByCheckPreset_NotClamped(
        string field, int? weapon, int? item, int? hp, int? hpMax, int? mp, int? mpMax, int? money)
    {
        var ok = TestSaves.TryBuild(WithFields(weapon, item, hp, hpMax, mp, mpMax, money), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("preset 'custom'", error);
        Assert.Contains(field, error);
    }

    // ---- The existing application path (the pattern of SaveGameApplyTests) ----------------------------

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void APreset_AppliedByTheExistingLoadPath_PutsItsFlagsAndTableInTheArrivalState(string name)
    {
        var expected = For(name);
        var keys = new HeldKeys();
        var slots = new FakeSaveSlots();
        var director = SaveGameDirector.Instance;
        SaveGameDirector.RecipeKeysEnabledOverrideForTests = true;
        director.KeyHeldProviderForTests = keys.IsHeld;
        director.SaveSlots = slots;
        director.RulesFactoryForTests = RealRules;
        slots.Slots.Add(new SlotEntry("test-" + name, true, T0));
        WarpDirector.Instance.AttachToWorld(BuildGameManager(), null, FindProjectRoot());

        var state = GameState.Instance;
        slots.LoadedSave = Build(name);
        director.UpdateRecipeKeys(null, state, Map389WorldName, HeroAt());
        keys.Held.Add(Keys.F9);
        director.UpdateRecipeKeys(null, state, Map389WorldName, HeroAt());
        keys.Held.Clear();
        Assert.True(director.HasPendingLoad);

        state.InstallForMapEntry();
        director.ApplyPendingLoad(state, expected.MapId);

        foreach (var (word, value) in expected.Flags)
        {
            Assert.Equal(value, state.GameFlags[word]);
        }

        foreach (var (index, value) in expected.Table)
        {
            Assert.Equal(value, state.MapIdToInternalMapIndexTable[index]);
        }

        Assert.Equal(1, state.NumberOfItems[3]);
        Assert.Equal(10, state.PlayerStats.HpMax);
    }
}
