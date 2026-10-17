#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.SaveGames;

namespace Alundra.SaveGames;

/// <summary>
/// E16.d K4 (docs/plan-e16-etat-partie.md, D-E16-31): the engine's save-game service as the DLL sees it - the
/// three calls the recipe keys need (<see cref="Save"/>, <see cref="TryLoad"/>, <see cref="ListSlots"/>), with
/// results in DLL types over the engine's public status enumerations (G11: the engine's own result types only
/// have constructors internal to the engine). Production goes through <see cref="EngineSaveSlots"/>;
/// the tests fake every state with their own implementation and never touch the real save folder.
/// </summary>
internal interface ISaveSlots
{
    /// <summary>Writes <paramref name="data"/> to <paramref name="slot"/> in <paramref name="format"/>, with its
    /// plain-text <paramref name="metadata"/>. Never throws.</summary>
    SaveOutcome Save(string slot, SaveGame data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata);

    /// <summary>Loads <paramref name="slot"/>; <paramref name="data"/> is null on any status but
    /// <see cref="SaveGameLoadStatus.Loaded"/>. Never throws. The loaded object is untrusted: the caller
    /// validates it (<see cref="SaveGame.TryValidate"/>) before using it.</summary>
    LoadOutcome TryLoad(string slot, out SaveGame? data);

    /// <summary>The slots of the save folder, each with its readability and last-write time. Never throws; an
    /// environment problem gives an empty list.</summary>
    IReadOnlyList<SlotEntry> ListSlots();
}

/// <summary>E16.d K4: the outcome of <see cref="ISaveSlots.Save"/> - the engine's status, and its context
/// (slot file path and cause) on a failure.</summary>
internal readonly record struct SaveOutcome(SaveGameSaveStatus Status, string Message);

/// <summary>E16.d K4: the outcome of <see cref="ISaveSlots.TryLoad"/>.</summary>
internal readonly record struct LoadOutcome(SaveGameLoadStatus Status, string Message);

/// <summary>E16.d K4: one slot of <see cref="ISaveSlots.ListSlots"/>. <see cref="IsReadable"/> is the
/// engine's <c>SaveGameSlotInfo.IsReadable</c> (its header opened, <c>Status == Loaded</c>); a readable slot may
/// still fail to load or to validate. <see cref="LastWriteTimeUtc"/> is null when the file system could not
/// give it.</summary>
internal readonly record struct SlotEntry(string Name, bool IsReadable, DateTime? LastWriteTimeUtc);
