#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.SaveGames;

namespace Alundra.Scripts;

/// <summary>
/// E16.d K4 (docs/plan-e16-etat-partie.md): the production <see cref="IAlundraSaveSlots"/>, delegating to the
/// engine's one public service, <see cref="GameSettings.SaveGames"/> (engine ADR-0044: slots under
/// <c>LocalApplicationData/&lt;ProjectName&gt;/SaveGames</c>), and copying its results into the DLL's types.
/// <para><b>Never throws (SD6).</b> The service reports file and file-system problems as results, but throws
/// for what it calls developer misuse, and some of that is really the environment: a project without a valid
/// <c>ProjectName</c> or no local folder (<see cref="InvalidOperationException"/>), an invalid slot name
/// (<see cref="ArgumentException"/>). Every exception of every call is caught here, logged with
/// <see cref="Logs.WriteError"/>, and turned into <see cref="SaveGameSaveStatus.IoError"/>,
/// <see cref="SaveGameLoadStatus.IoError"/> or an empty list: an environment problem never crashes the game from a
/// recipe key.</para>
/// </summary>
internal sealed class AlundraEngineSaveSlots : IAlundraSaveSlots
{
    private readonly Func<SaveGameService> _service;

    /// <summary>The production adapter, over <see cref="GameSettings.SaveGames"/>.</summary>
    public AlundraEngineSaveSlots()
        : this(() => GameSettings.SaveGames)
    {
    }

    /// <summary>An adapter over the service <paramref name="service"/> returns, read at each call - tests pass a
    /// function that throws, to prove every call absorbs an exception of the service (SD6), without ever
    /// reaching the real save folder (D-E16-31).</summary>
    internal AlundraEngineSaveSlots(Func<SaveGameService> service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    public AlundraSaveOutcome Save(string slot, AlundraSaveGame data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata)
    {
        try
        {
            var result = _service().Save(slot, data, format, metadata);
            return new AlundraSaveOutcome(result.Status, result.Message);
        }
        catch (Exception ex)
        {
            var message = Describe("save", slot, ex);
            Logs.WriteError(message);
            return new AlundraSaveOutcome(SaveGameSaveStatus.IoError, message);
        }
    }

    public AlundraLoadOutcome TryLoad(string slot, out AlundraSaveGame? data)
    {
        data = null;
        try
        {
            var result = _service().TryLoad<AlundraSaveGame>(slot, out var loaded);
            if (result.Status == SaveGameLoadStatus.Loaded)
            {
                data = loaded;
            }

            return new AlundraLoadOutcome(result.Status, result.Message);
        }
        catch (Exception ex)
        {
            var message = Describe("load", slot, ex);
            Logs.WriteError(message);
            data = null;
            return new AlundraLoadOutcome(SaveGameLoadStatus.IoError, message);
        }
    }

    public IReadOnlyList<AlundraSlotEntry> ListSlots()
    {
        try
        {
            var slots = _service().ListSlots();
            var entries = new List<AlundraSlotEntry>(slots.Count);
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    entries.Add(new AlundraSlotEntry(slot.Name, slot.IsReadable, slot.LastWriteTimeUtc));
                }
            }

            return entries;
        }
        catch (Exception ex)
        {
            Logs.WriteError(Describe("list", null, ex));
            return Array.Empty<AlundraSlotEntry>();
        }
    }

    private static string Describe(string operation, string? slot, Exception ex)
    {
        var target = slot == null ? "the save slots" : $"slot '{slot}'";
        return $"AlundraEngineSaveSlots: {operation} of {target} failed in the save-game service ({ex.GetType().Name}: {ex.Message}).";
    }
}
