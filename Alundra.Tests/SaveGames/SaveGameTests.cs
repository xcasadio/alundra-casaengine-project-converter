#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Session;
using Alundra.SaveGames;
using Alundra.Entities;

namespace Alundra.Tests.SaveGames;

/// <summary>
/// E16.c T3 (docs/plan-e16-etat-partie.md, C2, C5, C6, C7): the save-game object, its capture from the live
/// state and from the live world, its application to a state, and its metadata. Every test builds private
/// <see cref="GameState"/> instances; nothing here reads or writes a save file (D-E16-31).
/// </summary>
public sealed class SaveGameTests
{
    /// <summary>The real world name of map 389 (F4): the "-{id}" suffix is the current map.</summary>
    private const string Map389WorldName = "Ship Klark (beginning)-389";

    /// <summary>A state with a distinct value in every saved field, and in the unsaved ones too.</summary>
    private static GameState BuildPopulatedState()
    {
        var state = new GameState();

        for (var i = 0; i < 64; i++)
        {
            state.GameFlags[i] = 0x9E3779B9u * (uint)(i + 1);
        }

        for (var i = 0; i < state.MapIdToInternalMapIndexTable.Length; i++)
        {
            state.MapIdToInternalMapIndexTable[i] = (ushort)((i * 7 + 3) % 483);
        }

        for (var i = 0; i < state.NumberOfItems.Length; i++)
        {
            state.NumberOfItems[i] = (short)(i % 2 == 1 ? i % 10 : 0);
        }

        var stats = state.PlayerStats;
        stats.Hp = 17;
        stats.HpMax = 23;
        stats.Mp = 2;
        stats.MpMax = 3;
        stats.Money = 4321;
        stats.WeaponId = 4;
        stats.ItemId = 31;
        stats.FalconTemp = 12;
        stats.Falcon = 34;

        state.GameTime = 1234567;
        state.DeathRetryCount = 9;

        state.TemporaryFlags[5] = 0xDEADBEEF;
        state.TextCategoryIndex = 3;
        state.GameVariables[2] = 77;
        state.PlayerControlFlags = GameState.PlayerControlBits.ControlLocked;
        return state;
    }

    private static void AssertSavedFieldsEqual(GameState expected, GameState actual)
    {
        Assert.Equal(expected.GameFlags.Take(64), actual.GameFlags.Take(64));
        Assert.Equal(expected.MapIdToInternalMapIndexTable, actual.MapIdToInternalMapIndexTable);
        Assert.Equal(expected.NumberOfItems, actual.NumberOfItems);

        Assert.Equal(expected.PlayerStats.Hp, actual.PlayerStats.Hp);
        Assert.Equal(expected.PlayerStats.HpMax, actual.PlayerStats.HpMax);
        Assert.Equal(expected.PlayerStats.Mp, actual.PlayerStats.Mp);
        Assert.Equal(expected.PlayerStats.MpMax, actual.PlayerStats.MpMax);
        Assert.Equal(expected.PlayerStats.Money, actual.PlayerStats.Money);
        Assert.Equal(expected.PlayerStats.WeaponId, actual.PlayerStats.WeaponId);
        Assert.Equal(expected.PlayerStats.ItemId, actual.PlayerStats.ItemId);
        Assert.Equal(expected.PlayerStats.FalconTemp, actual.PlayerStats.FalconTemp);
        Assert.Equal(expected.PlayerStats.Falcon, actual.PlayerStats.Falcon);

        Assert.Equal(expected.GameTime, actual.GameTime);
        Assert.Equal(expected.DeathRetryCount, actual.DeathRetryCount);
    }

    // ---- Capture then ApplyTo (C5, C6) ------------------------------------------------------------

    [Fact]
    public void Capture_ThenApplyTo_OnAFreshState_GivesAnIdenticalStateFieldByField()
    {
        var source = BuildPopulatedState();

        var save = SaveGame.Capture(source, 389, 33, 59, 0);
        var target = new GameState();
        save.ApplyTo(target);

        AssertSavedFieldsEqual(source, target);
    }

    [Fact]
    public void Capture_SetsTheMapAndTileFromItsArguments()
    {
        var save = SaveGame.Capture(new GameState(), 390, 12, 34, 5);

        Assert.Equal(390, save.InitialMapId);
        Assert.Equal(12, save.CameraTileX);
        Assert.Equal(34, save.CameraTileY);
        Assert.Equal(5, save.CameraTileZ);
    }

    [Fact]
    public void Capture_CopiesTheSavedFieldsIntoTheObject()
    {
        var state = BuildPopulatedState();

        var save = SaveGame.Capture(state, 389, 33, 59, 0);

        Assert.Equal(state.GameFlags.Take(64), save.GameFlags);
        Assert.Equal(state.MapIdToInternalMapIndexTable, save.MapIdToInternalMapIndexTable);
        Assert.Equal(state.NumberOfItems, save.NumberOfItems);
        Assert.Equal(state.PlayerStats.Hp, save.Hp);
        Assert.Equal(state.PlayerStats.HpMax, save.HpMax);
        Assert.Equal(state.PlayerStats.Mp, save.Mp);
        Assert.Equal(state.PlayerStats.MpMax, save.MpMax);
        Assert.Equal(state.PlayerStats.Money, save.Money);
        Assert.Equal(state.PlayerStats.WeaponId, save.WeaponId);
        Assert.Equal(state.PlayerStats.ItemId, save.ItemId);
        Assert.Equal(state.PlayerStats.FalconTemp, save.FalconTemp);
        Assert.Equal(state.PlayerStats.Falcon, save.Falcon);
        Assert.Equal(state.GameTime, save.GameTime);
        Assert.Equal(state.DeathRetryCount, save.DeathRetryCount);
        Assert.Equal(1, save.LoadedDataVersion);
    }

    [Fact]
    public void ApplyTo_ClearsTheGameFlagWordsAt64AndAbove()
    {
        var save = SaveGame.Capture(BuildPopulatedState(), 389, 33, 59, 0);
        var target = new GameState();
        target.GameFlags[64] = 0x1;
        target.GameFlags[500] = 0xFFFF0000;
        target.GameFlags[1023] = 0x80000000;

        save.ApplyTo(target);

        Assert.All(target.GameFlags.Skip(64), word => Assert.Equal(0u, word));
    }

    [Fact]
    public void ApplyTo_ReplacesTheTargetsSavedWords_EvenWhereTheSaveIsZero()
    {
        var save = SaveGame.Capture(new GameState(), 389, 33, 59, 0); // all 64 words zero.
        var target = new GameState();
        target.GameFlags[0] = 0xFFFFFFFF;
        target.GameFlags[63] = 0x1234;

        save.ApplyTo(target);

        Assert.All(target.GameFlags, word => Assert.Equal(0u, word));
    }

    [Fact]
    public void ApplyTo_LeavesTemporaryFlagsTextCategoryGameVariablesAndControlFlagsAlone()
    {
        var save = SaveGame.Capture(new GameState(), 389, 33, 59, 0);
        var target = BuildPopulatedState();
        var temporaryBefore = target.TemporaryFlags.ToArray();
        var gameVariablesBefore = target.GameVariables.ToArray();

        save.ApplyTo(target);

        Assert.Equal(temporaryBefore, target.TemporaryFlags);
        Assert.Equal(3, target.TextCategoryIndex);
        Assert.Equal(gameVariablesBefore, target.GameVariables);
        Assert.Equal(GameState.PlayerControlBits.ControlLocked, target.PlayerControlFlags);
    }

    [Fact]
    public void ApplyTo_DropsTheUnsavedFractionOfTheGameTime()
    {
        var save = SaveGame.Capture(new GameState { GameTime = 100 }, 389, 33, 59, 0);
        var target = new GameState();
        target.AdvanceGameTime(0.75f / 60f); // three quarters of a unit pending in the target.

        save.ApplyTo(target);
        target.AdvanceGameTime(0.5f / 60f); // with the old fraction, 0.75 + 0.5 would pass a whole unit.

        Assert.Equal(100u, target.GameTime);
    }

    [Fact]
    public void ApplyTo_SharesNoArrayWithTheObject()
    {
        var save = SaveGame.Capture(BuildPopulatedState(), 389, 33, 59, 0);
        var target = new GameState();
        save.ApplyTo(target);
        var snapshot = new GameState();
        save.ApplyTo(snapshot);

        save.GameFlags[0] ^= 0xFFFFFFFF;
        save.MapIdToInternalMapIndexTable[0] ^= 0xFFFF;
        save.NumberOfItems[1] ^= 0x7FFF;

        AssertSavedFieldsEqual(snapshot, target);
    }

    [Fact]
    public void Capture_SharesNoArrayWithTheState()
    {
        var state = BuildPopulatedState();
        var save = SaveGame.Capture(state, 389, 33, 59, 0);
        var flagsBefore = save.GameFlags.ToArray();
        var tableBefore = save.MapIdToInternalMapIndexTable.ToArray();
        var itemsBefore = save.NumberOfItems.ToArray();

        Assert.NotSame(state.GameFlags, save.GameFlags);
        Assert.NotSame(state.MapIdToInternalMapIndexTable, save.MapIdToInternalMapIndexTable);
        Assert.NotSame(state.NumberOfItems, save.NumberOfItems);

        for (var i = 0; i < 64; i++)
        {
            state.GameFlags[i] = ~state.GameFlags[i];
        }

        for (var i = 0; i < state.MapIdToInternalMapIndexTable.Length; i++)
        {
            state.MapIdToInternalMapIndexTable[i] = 0;
        }

        Array.Fill(state.NumberOfItems, (short)99);
        state.PlayerStats.Money = 1;
        state.GameTime = 1;

        Assert.Equal(flagsBefore, save.GameFlags);
        Assert.Equal(tableBefore, save.MapIdToInternalMapIndexTable);
        Assert.Equal(itemsBefore, save.NumberOfItems);
        Assert.Equal((short)4321, save.Money);
        Assert.Equal(1234567u, save.GameTime);
    }

    // ---- Shape of the object (C2, SC3, SC4) ----------------------------------------------------------

    [Fact]
    public void PublicFields_AreExactlyTheSavedOnes()
    {
        var names = typeof(SaveGame)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var expected = new[]
        {
            "GameTime", "InitialMapId", "CameraTileX", "CameraTileY", "CameraTileZ", "GameFlags",
            "MapIdToInternalMapIndexTable", "Hp", "HpMax", "Mp", "MpMax", "Money", "WeaponId", "ItemId",
            "FalconTemp", "Falcon", "NumberOfItems", "DeathRetryCount",
        }.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, names);
    }

    [Theory]
    [InlineData("TemporaryFlags")]
    [InlineData("TextCategoryIndex")]
    [InlineData("GameVariables")]
    [InlineData("PlayerControlFlags")]
    public void HasNoMemberForTheUnsavedState(string name)
    {
        var members = typeof(SaveGame).GetMembers(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.DoesNotContain(members, m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadedDataVersion_IsAPropertyNotAField()
    {
        var property = typeof(SaveGame).GetProperty(nameof(SaveGame.LoadedDataVersion));

        Assert.NotNull(property);
        Assert.Null(typeof(SaveGame).GetField(
            nameof(SaveGame.LoadedDataVersion), BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void NewObject_HasItsThreeReadonlyArraysAtTheirLengths_AndVersion1()
    {
        var save = new SaveGame();

        Assert.NotNull(save.GameFlags);
        Assert.NotNull(save.MapIdToInternalMapIndexTable);
        Assert.NotNull(save.NumberOfItems);
        Assert.Equal(64, save.GameFlags.Length);
        Assert.Equal(500, save.MapIdToInternalMapIndexTable.Length);
        Assert.Equal(256, save.NumberOfItems.Length);

        foreach (var name in new[] { "GameFlags", "MapIdToInternalMapIndexTable", "NumberOfItems" })
        {
            var field = typeof(SaveGame).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(field);
            Assert.True(field!.IsInitOnly, name + " must be readonly");
        }

        Assert.Equal(1, save.LatestDataVersion);
        Assert.Equal(1, save.LoadedDataVersion);
    }

    // ---- Capture from the live world (C5, F4, F5) ------------------------------------------------------

    [Fact]
    public void TryCaptureFromWorld_RealHeroProxyOnMap389_CapturesTheMapAndTheHerosTile()
    {
        var state = BuildPopulatedState();
        var hero = new EntityScriptProxy { TileX = 33, TileY = 59, TileZ = 2 };

        var captured = SaveGame.TryCaptureFromWorld(state, Map389WorldName, hero, out var save);

        Assert.True(captured);
        Assert.NotNull(save);
        Assert.Equal(389, save!.InitialMapId);
        Assert.Equal(33, save.CameraTileX);
        Assert.Equal(59, save.CameraTileY);
        Assert.Equal(2, save.CameraTileZ);
        Assert.Equal(state.GameTime, save.GameTime);
        Assert.Equal(1, save.LoadedDataVersion);
    }

    [Theory]
    [InlineData("Ship Klark (beginning)")]
    [InlineData("TestWorld")]
    [InlineData("")]
    [InlineData("Map-")]
    [InlineData("Map-99999999999")]
    public void TryCaptureFromWorld_NameWithoutAnId_ReturnsFalseWithoutThrowing(string worldName)
    {
        var hero = new EntityScriptProxy { TileX = 33, TileY = 59, TileZ = 0 };

        var captured = SaveGame.TryCaptureFromWorld(new GameState(), worldName, hero, out var save);

        Assert.False(captured);
        Assert.Null(save);
    }

    [Fact]
    public void TryCaptureFromWorld_NullName_ReturnsFalseWithoutThrowing()
    {
        var hero = new EntityScriptProxy();

        var captured = SaveGame.TryCaptureFromWorld(new GameState(), null!, hero, out var save);

        Assert.False(captured);
        Assert.Null(save);
    }

    [Fact]
    public void TryCaptureFromWorld_NoHero_ReturnsFalseWithoutThrowing()
    {
        var captured = SaveGame.TryCaptureFromWorld(new GameState(), Map389WorldName, null, out var save);

        Assert.False(captured);
        Assert.Null(save);
    }

    // ---- Metadata (C7) ---------------------------------------------------------------------------------

    [Fact]
    public void BuildMetadata_ChapterAndSummary_MatchT2sComputation()
    {
        var state = new GameState();
        foreach (var id in ChapterFlags.FlagIds.Take(3))
        {
            state.AddFlag(id, 1u << (id & 0x1F));
        }

        state.PlayerStats.HpMax = 20;
        state.PlayerStats.Hp = 5; // the summary shows the MAX HP (F7), not the current one.
        state.GameTime = 3600;

        var save = SaveGame.Capture(state, 389, 33, 59, 0);
        var metadata = save.BuildMetadata();

        Assert.Equal(2, metadata.Count);
        Assert.Equal(
            ChapterFlags.FormatChapter(ChapterFlags.GetFirstEnabledFlagIndex(save.GameFlags)),
            metadata["chapter"]);
        Assert.Equal(SaveGame.BuildSummary(save.HpMax, save.GameTime), metadata["summary"]);
        Assert.Equal("0003", metadata["chapter"]);
        Assert.Equal("  HP 20       TIME 00:01:00   ", metadata["summary"]);
    }

    [Fact]
    public void BuildMetadata_IsComputedFromTheObject_NotFromALiveState()
    {
        var save = new SaveGame { HpMax = 50, GameTime = GameState.GameTimeMax };
        foreach (var id in ChapterFlags.FlagIds)
        {
            save.GameFlags[id >> 5] |= 1u << (id & 0x1F);
        }

        var metadata = save.BuildMetadata();

        Assert.Equal("0041", metadata["chapter"]);
        Assert.Equal("  HP 50       TIME 99:59:59   ", metadata["summary"]);
    }
}
