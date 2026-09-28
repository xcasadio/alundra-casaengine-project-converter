#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.d (docs/plan-e16-etat-partie.md, T2 to T5): shared plumbing of the save-game director's tests - a fake
/// <see cref="IAlundraSaveSlots"/> that records every call and answers what the test poses (D-E16-31: no test
/// ever reaches the real save folder), the real export's validation rules (K1: a catalog predicate that answers
/// yes, the export's own item tables), a log capture, a key-held provider, and a snapshot of the live state for
/// the "state identical" assertions.
/// </summary>
internal static class SaveGameDirectorTestSupport
{
    internal const string Map389WorldName = "Ship Klark (beginning)-389";

    /// <summary>The real converter export, with the two files the rules read. Fails naming the export when it is
    /// missing - never self-skips (the convention of <see cref="AlundraCellStoreProductionTests"/>).</summary>
    internal static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "Maps", "world-index.json"))
                && File.Exists(Path.Combine(candidate, "Data", "items-properties.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"SaveGameDirectorTestSupport: no 'alundra-project/Maps/world-index.json' with "
            + $"'alundra-project/Data/items-properties.json' found above '{AppContext.BaseDirectory}' - the E16.d "
            + "director tests need the real converter export and cannot self-skip without one (docs/plan-e16-etat-partie.md).");
    }

    /// <summary>K1/T3: the rules the director's tests inject - the real export folder, a catalog predicate that
    /// answers yes (the test process' asset catalog is empty), and the export's own item tables.</summary>
    internal static AlundraSaveGameRules RealRules()
    {
        var root = FindProjectRoot();
        return new AlundraSaveGameRules(root, _ => true, new AlundraItemTables(root));
    }

    /// <summary>A hero on the ground at a tile of map 389 (the New Game tile by default).</summary>
    internal static AlundraEntityScriptProxy HeroAt(int tileX = AlundraGameState.CameraTileX, int tileY = AlundraGameState.CameraTileY, int tileZ = AlundraGameState.CameraTileZ)
        => new() { IsPlayer = true, TileX = tileX, TileY = tileY, TileZ = tileZ, IsOnGround = 1 };

    /// <summary>A save that validates against <see cref="RealRules"/>: the New Game identity table, map 389 at
    /// the given tile, flags and stats as posed by the caller.</summary>
    internal static AlundraSaveGame ValidSave(int tileX = 33, int tileY = 59, int tileZ = 0)
    {
        var save = new AlundraSaveGame { InitialMapId = 389, CameraTileX = tileX, CameraTileY = tileY, CameraTileZ = tileZ };
        for (var i = 0; i < save.MapIdToInternalMapIndexTable.Length; i++)
        {
            save.MapIdToInternalMapIndexTable[i] = (ushort)i;
        }

        save.HpMax = 10;
        save.Hp = 10;
        save.WeaponId = 1;
        return save;
    }

    /// <summary>Resets every session singleton the director's tests touch.</summary>
    internal static void ResetSingletons()
    {
        AlundraSaveGameDirector.Instance.ResetForTests();
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = null;
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraInventoryPortrait.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    /// <summary>A key-held provider over a mutable set of held keys, counting every read.</summary>
    internal sealed class HeldKeys
    {
        public readonly HashSet<Keys> Held = new();
        public int Reads;

        public bool IsHeld(Keys key)
        {
            Reads++;
            return Held.Contains(key);
        }
    }

    /// <summary>
    /// The fake service: records every call; <see cref="Save"/> answers <see cref="SaveStatus"/>,
    /// <see cref="TryLoad"/> answers <see cref="LoadStatus"/> with <see cref="LoadedSave"/> (only when the status
    /// is <see cref="SaveGameLoadStatus.Loaded"/>, unless <see cref="ReturnObjectOnFailure"/>), and
    /// <see cref="ListSlots"/> answers <see cref="Slots"/>.
    /// </summary>
    internal sealed class FakeSaveSlots : IAlundraSaveSlots
    {
        public readonly List<(string Slot, AlundraSaveGame Data, SaveGameFormat Format, IReadOnlyDictionary<string, string> Metadata)> SaveCalls = new();
        public readonly List<string> LoadCalls = new();
        public int ListCalls;

        public SaveGameSaveStatus SaveStatus = SaveGameSaveStatus.Saved;
        public SaveGameLoadStatus LoadStatus = SaveGameLoadStatus.Loaded;
        public AlundraSaveGame? LoadedSave;
        public bool ReturnObjectOnFailure;
        public List<AlundraSlotEntry> Slots = new();

        public AlundraSaveOutcome Save(string slot, AlundraSaveGame data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata)
        {
            SaveCalls.Add((slot, data, format, metadata));
            return new AlundraSaveOutcome(SaveStatus, SaveStatus == SaveGameSaveStatus.Saved ? string.Empty : $"fake {SaveStatus}");
        }

        public AlundraLoadOutcome TryLoad(string slot, out AlundraSaveGame? data)
        {
            LoadCalls.Add(slot);
            data = LoadStatus == SaveGameLoadStatus.Loaded || ReturnObjectOnFailure ? LoadedSave : null;
            return new AlundraLoadOutcome(LoadStatus, LoadStatus == SaveGameLoadStatus.Loaded ? string.Empty : $"fake {LoadStatus}");
        }

        public IReadOnlyList<AlundraSlotEntry> ListSlots()
        {
            ListCalls++;
            return Slots;
        }

        public bool NothingCalled => SaveCalls.Count == 0 && LoadCalls.Count == 0 && ListCalls == 0;
    }

    /// <summary>Captures every log line for one test, then unregisters itself (<see cref="Logs"/> has no public
    /// removal API - same reflection precedent as <c>AlundraWarpArrivalTests.CapturingLogger</c>).</summary>
    internal sealed class LogCapture : ILogger, IDisposable
    {
        public readonly List<string> Infos = new();
        public readonly List<string> Warnings = new();
        public readonly List<string> Errors = new();

        public static LogCapture Install()
        {
            var capture = new LogCapture();
            Logs.AddLogger(capture);
            return capture;
        }

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg) => Infos.Add(msg);

        public void WriteWarning(string msg) => Warnings.Add(msg);

        public void WriteError(string msg) => Errors.Add(msg);

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }

    /// <summary>Everything a refused key must leave untouched: the whole game state, and the warp, fade and music
    /// sessions a departure would arm.</summary>
    internal sealed record StateSnapshot(
        uint[] GameFlags,
        uint[] TemporaryFlags,
        ushort[] MapTable,
        short[] Items,
        string Stats,
        uint PlayerControlFlags,
        uint GameTime,
        byte DeathRetryCount,
        int TextCategoryIndex,
        int[] GameVariables,
        bool NewGameInventoryInitialized,
        bool IsWarpDisabled,
        bool TransitionInProgress,
        bool HasPendingArrival,
        bool FadeSettled,
        bool HasPendingLoad)
    {
        public static StateSnapshot Take(AlundraGameState state)
        {
            var s = state.PlayerStats;
            return new StateSnapshot(
                state.GameFlags.ToArray(),
                state.TemporaryFlags.ToArray(),
                state.MapIdToInternalMapIndexTable.ToArray(),
                state.NumberOfItems.ToArray(),
                $"{s.Hp}/{s.HpMax}/{s.Mp}/{s.MpMax}/{s.Money}/{s.WeaponId}/{s.ItemId}/{s.FalconTemp}/{s.Falcon}",
                state.PlayerControlFlags,
                state.GameTime,
                state.DeathRetryCount,
                state.TextCategoryIndex,
                state.GameVariables.ToArray(),
                state.NewGameInventoryInitialized,
                state.IsWarpDisabled,
                AlundraWarpDirector.Instance.IsTransitionInProgress,
                AlundraWarpDirector.Instance.HasPendingArrival,
                AlundraScreenFadeDirector.Instance.IsSettled,
                AlundraSaveGameDirector.Instance.HasPendingLoad);
        }

        public void AssertSameAs(StateSnapshot other)
        {
            Assert.Equal(other.GameFlags, GameFlags);
            Assert.Equal(other.TemporaryFlags, TemporaryFlags);
            Assert.Equal(other.MapTable, MapTable);
            Assert.Equal(other.Items, Items);
            Assert.Equal(other.Stats, Stats);
            Assert.Equal(other.PlayerControlFlags, PlayerControlFlags);
            Assert.Equal(other.GameTime, GameTime);
            Assert.Equal(other.DeathRetryCount, DeathRetryCount);
            Assert.Equal(other.TextCategoryIndex, TextCategoryIndex);
            Assert.Equal(other.GameVariables, GameVariables);
            Assert.Equal(other.NewGameInventoryInitialized, NewGameInventoryInitialized);
            Assert.Equal(other.IsWarpDisabled, IsWarpDisabled);
            Assert.Equal(other.TransitionInProgress, TransitionInProgress);
            Assert.Equal(other.HasPendingArrival, HasPendingArrival);
            Assert.Equal(other.FadeSettled, FadeSettled);
            Assert.Equal(other.HasPendingLoad, HasPendingLoad);
        }
    }
}
