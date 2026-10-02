#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests;

/// <summary>
/// E19.d2a S1 (docs/plan-e19-opcodes.md section 1.2h.1, D-E19-33): the test-save presets of
/// <see cref="AlundraTestSaves"/>. Expected values are written by hand from the plan's table, never read from
/// the preset: new game plus the preset, nothing else.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraTestSavesTests : IDisposable
{
    // Copy of the slot-name rule of the engine's ADR-0044 (SaveGameNames.SlotNameRegex,
    // CasaEngine/Framework/SaveGames/SaveGameNames.cs, internal to the engine): 1 to 32 characters among
    // a-z, 0-9, '_' and '-'.
    private static readonly Regex SlotNameRule = new(@"\A[a-z0-9_-]{1,32}\z", RegexOptions.CultureInvariant);

    private static readonly DateTime T0 = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    public AlundraTestSavesTests()
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

    private static Expected For(string name) => name == Day3.Name ? Day3 : Day4;

    private static AlundraSaveGame Build(string name)
    {
        Assert.True(AlundraTestSaves.TryBuild(name, RealRules(), out var save, out var error), error);
        Assert.NotNull(save);
        return save!;
    }

    [Fact]
    public void TheTwoPresets_AreListed_InTheOrderOfTheTable()
    {
        Assert.Equal([Day3.Name, Day4.Name], AlundraTestSaves.Presets.Select(p => p.Name));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void ABuiltPreset_IsTheNewGamePlusThePreset_InEveryField(string name)
    {
        var expected = For(name);
        var rules = RealRules();

        // The new game, built the way the game builds it.
        var newGame = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameStats(newGame);
        AlundraPlayerManager.InitializeNewGameInventory(newGame, rules.ItemTables);
        var reference = AlundraSaveGame.Capture(newGame, expected.MapId, expected.TileX, expected.TileY, expected.TileZ);
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
    [MemberData(nameof(PresetNames))]
    public void ABuiltPreset_IsAcceptedByTheRealRules(string name)
    {
        var save = Build(name);

        Assert.True(save.TryValidate(RealRules(), out var error), error);
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void TwoConstructions_AreEqual_ByteForByte(string name)
    {
        var a = Build(name);
        var b = Build(name);

        Assert.NotSame(a, b);
        Assert.Equal(Serialize(a), Serialize(b));
    }

    private static byte[] Serialize(AlundraSaveGame save)
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
    [MemberData(nameof(PresetNames))]
    public void TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule(string name)
    {
        Assert.True(AlundraTestSaves.TryFind(name, out var preset));

        Assert.Equal("test-" + name, preset!.Slot);
        Assert.Matches(SlotNameRule, preset.Slot);
    }

    [Fact]
    public void AnUnknownName_IsRefused_WithAMessage_AndNoException()
    {
        var ok = AlundraTestSaves.TryBuild("day9-nowhere", RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("day9-nowhere", error);
        Assert.False(AlundraTestSaves.TryFind("day9-nowhere", out _));
    }

    private static AlundraTestSaves.Preset Custom(uint[]? set = null, uint[]? clear = null, int mapId = 179, int tileX = 17, int tileY = 7, int tileZ = 1)
        => new(
            "custom", "test-custom", mapId, tileX, tileY, tileZ,
            set ?? [], clear ?? [], [], []);

    [Theory]
    [InlineData(0x8000u)]
    [InlineData(0x8000u + 5u)]
    [InlineData(0xFFFFu)]
    public void ATemporaryFlag_IsRefused_BecauseASaveNeverCarriesIt(uint flag)
    {
        var ok = AlundraTestSaves.TryBuild(Custom(set: [flag]), RealRules(), out var save, out var error);

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
        var ok = AlundraTestSaves.TryBuild(Custom(set: [flag]), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("2048", error);
    }

    [Fact]
    public void AClearedFlagOutOfRange_IsRefusedToo()
    {
        Assert.False(AlundraTestSaves.TryBuild(Custom(clear: [2048u]), RealRules(), out _, out var error));
        Assert.Contains("2048", error);
    }

    [Fact]
    public void ASaveTheRulesRefuse_IsRefused_WithTheRulesMessage()
    {
        // Map 99999 is no key of world-index.json.
        var ok = AlundraTestSaves.TryBuild(Custom(mapId: 99999), RealRules(), out var save, out var error);

        Assert.False(ok);
        Assert.Null(save);
        Assert.Contains("initialMapId", error);
    }

    [Fact]
    public void AFlagAtTheEdgeOfTheSavedWords_IsAccepted()
    {
        // Flag 2047 is the last bit of word 63, the last saved word.
        var ok = AlundraTestSaves.TryBuild(Custom(set: [2047u]), RealRules(), out var save, out var error);

        Assert.True(ok, error);
        Assert.Equal(0x80000000u, save!.GameFlags[63]);
    }

    // ---- E19.d2b B7: the hygiene of S1 (the mutations that survived the first 22 tests) ----------------------

    [Fact]
    public void APresetThatSetsThenClearsAFlag_EndsWithItCleared_AndTheOthersPosed()
    {
        // G203 is 0 in a new game, so clearing it alone proves nothing: set it and another, clear the first.
        var preset = new AlundraTestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [203u, 5u], [203u], [], []);

        Assert.True(AlundraTestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

        Assert.Equal(0u, save!.GameFlags[6]); // G203 (word 6 bit 11): set then cleared.
        Assert.Equal(1u << 5, save.GameFlags[0]); // G5: only set.
    }

    [Fact]
    public void AnItemCounter_IsWrittenToItsIndex_AndNothingElseMoves()
    {
        var preset = new AlundraTestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [], [(5, (short)1)]); // the rules cap each object (0..1 for the object 5).

        Assert.True(AlundraTestSaves.TryBuild(preset, RealRules(), out var save, out var error), error);

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
        var preset = new AlundraTestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [(index, (ushort)1)], []);

        Assert.False(AlundraTestSaves.TryBuild(preset, RealRules(), out var save, out var error));

        Assert.Null(save);
        Assert.Contains("map table index " + index, error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void AnItemCounterIndexOutsideItsTable_IsRefused(int index)
    {
        var preset = new AlundraTestSaves.Preset("custom", "test-custom", 179, 17, 7, 1, [], [], [], [(index, (short)1)]);

        Assert.False(AlundraTestSaves.TryBuild(preset, RealRules(), out var save, out var error));

        Assert.Null(save);
        Assert.Contains("item counter index " + index, error);
    }

    // ---- The existing application path (the pattern of AlundraSaveGameApplyTests) ----------------------------

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void APreset_AppliedByTheExistingLoadPath_PutsItsFlagsAndTableInTheArrivalState(string name)
    {
        var expected = For(name);
        var keys = new HeldKeys();
        var slots = new FakeSaveSlots();
        var director = AlundraSaveGameDirector.Instance;
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = true;
        director.KeyHeldProviderForTests = keys.IsHeld;
        director.SaveSlots = slots;
        director.RulesFactoryForTests = RealRules;
        slots.Slots.Add(new AlundraSlotEntry("test-" + name, true, T0));
        AlundraWarpDirector.Instance.AttachToWorld(BuildGameManager(), null, FindProjectRoot());

        var state = AlundraGameState.Instance;
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
