#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;

namespace Alundra.Scripts;

/// <summary>
/// E16.d K1 (docs/plan-e16-etat-partie.md): the SESSION-scoped director of the save-game recipe keys - same
/// shape as the other session singletons (<see cref="Instance"/>, <see cref="ResetForTests"/>). It carries the
/// debug switch (K2), the keys and their rising edges (K3), the save-game service behind the DLL's own interface
/// (K4), and the choice of the slot to load (K5).
///
/// <para><b>The keys (K3, D-E16-11).</b> F5 saves in binary to <see cref="BinarySlotName"/>, F6 in JSON to
/// <see cref="JsonSlotName"/>, F9 loads the most recent readable slot. They are read once per RENDERED frame, at
/// the head of <see cref="AlundraWorldProxy.Update"/> (right after the game time, before the gameplay gate is
/// computed, SD3), through <see cref="KeyboardManager.IsKeyPressed"/> with this director's own rising edge: the
/// engine's keyboard advances once per rendered frame, so read inside the logic-tick loop a key would count once
/// per tick (G9). One key is handled per frame, in the order F5, F6, F9. The edges live in this session
/// singleton, so a key held through a world change does not act a second time (SD13).</para>
///
/// <para><b>The switch (K2, D-E16-33).</b> The keys exist only in a DLL compiled in Debug
/// (<see cref="RecipeKeysEnabledByBuild"/>); switched off, this director does not even read the keyboard. When on,
/// it is logged once, at this director's first pass.</para>
///
/// <para><b>Messages (K9).</b> The DLL has no on-screen message (G12): every refusal and every service result
/// is one log line (<see cref="Logs.WriteWarning"/> for a refusal, <see cref="Logs.WriteInfo"/> for a success).
/// These keys only serve the recipe; the player's own save screen is E16.e.</para>
/// </summary>
public sealed class AlundraSaveGameDirector
{
    public static readonly AlundraSaveGameDirector Instance = new();

    private AlundraSaveGameDirector()
    {
    }

    /// <summary>K3: the slot F5 writes, in binary.</summary>
    internal const string BinarySlotName = "debug-binary";

    /// <summary>K3: the slot F6 writes, in JSON.</summary>
    internal const string JsonSlotName = "debug-json";

    internal const Keys SaveBinaryKey = Keys.F5;
    internal const Keys SaveJsonKey = Keys.F6;
    internal const Keys LoadKey = Keys.F9;

    private const string LogPrefix = "AlundraSaveGameDirector: ";

    // ---- K2: the debug switch (D-E16-33) --------------------------------------------------------------------

    /// <summary>K2/D-E16-33: whether the build turns the recipe keys on - true in a DLL compiled in Debug, false in
    /// Release. The launcher is started from the IDE, ignores its arguments, and an environment variable never
    /// reached the game (D-E13-12), so the build configuration is the switch. The DLL the game loads is the one
    /// the last build of <c>Alundra.csproj</c> copied into the project (SD7).</summary>
    internal static bool RecipeKeysEnabledByBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Test-only seam over <see cref="RecipeKeysEnabledByBuild"/>, on the model of
    /// <c>AlundraPlayerManager.SetDebugIgnoreControlLockOverrideForTests</c> (G10): null (the default) defers to
    /// the build, a value forces it. <c>dotnet test</c> builds the DLL in Debug, so the "off" branch is only
    /// reachable through this seam. Reachable only by code already in the process (SD14).</summary>
    internal static bool? RecipeKeysEnabledOverrideForTests;

    /// <summary>K2: the switch in force.</summary>
    internal static bool RecipeKeysEnabled => RecipeKeysEnabledOverrideForTests ?? RecipeKeysEnabledByBuild;

    private bool _switchLogged;

    // ---- K3: the keys ---------------------------------------------------------------------------------------

    /// <summary>Test-only seam over this frame's raw "is this key held" reading, same rationale as the former
    /// F1 recipe key's <c>DebugHudToggleKeyHeldProviderForTests</c> (G9): a headless test has no live keyboard.
    /// It returns the HELD state; the edge detection is this director's own, on both paths.</summary>
    internal Func<Keys, bool>? KeyHeldProviderForTests;

    private bool _saveBinaryKeyWasHeld;
    private bool _saveJsonKeyWasHeld;
    private bool _loadKeyWasHeld;

    // ---- K1/K4: rules and service ---------------------------------------------------------------------------

    /// <summary>
    /// K1 (the closing plan review of 2026-09-28): test-only seam over the validation rules - null (the default)
    /// builds the production rules, <c>new AlundraSaveGameRules(EngineEnvironment.ProjectPath, path =&gt;
    /// AssetCatalog.GetByFileName(path) != null, AlundraItemTables.GetOrCreate(EngineEnvironment.ProjectPath))</c>,
    /// the session item tables behind <see cref="AlundraWorldProxy.ItemTables"/>. In the test process the asset
    /// catalog is empty, so every validation would refuse <c>initialMapId</c> without this seam. It replaces the
    /// rules only, never the validation itself; reachable only by code already in the process (SD14, SD15).
    /// </summary>
    internal Func<AlundraSaveGameRules>? RulesFactoryForTests;

    /// <summary>K4: the save-game service behind the DLL's interface - <see cref="AlundraEngineSaveSlots"/> over
    /// the engine's service in production; tests assign a fake (D-E16-31) and never reach the real folder.</summary>
    internal IAlundraSaveSlots SaveSlots { get; set; } = new AlundraEngineSaveSlots();

    /// <summary>K7 step 6: the validated save a departure is carrying to its arrival map, or null.</summary>
    private AlundraSaveGame? _pendingLoad;

    /// <summary>True while a load is pending (K7 step 6 to K8).</summary>
    public bool HasPendingLoad => _pendingLoad != null;

    /// <summary>
    /// K3: the recipe keys, once per rendered frame, from the head of <see cref="AlundraWorldProxy.Update"/>.
    /// Does nothing, and reads no key, while <see cref="RecipeKeysEnabled"/> is false.
    /// </summary>
    /// <param name="keyboard">The engine's keyboard (<c>world.Game.InputComponent.KeyboardManager</c>); null
    /// reads as "nothing held".</param>
    /// <param name="state">The session's game state.</param>
    /// <param name="worldName">The current world's name, whose "-{id}" suffix is the current map (F4).</param>
    /// <param name="player">The hero (<see cref="AlundraWorldProxy.PlayerEntity"/>), or null.</param>
    internal void UpdateRecipeKeys(KeyboardManager? keyboard, AlundraGameState state, string? worldName, AlundraEntityScriptProxy? player)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!RecipeKeysEnabled)
        {
            return;
        }

        if (!_switchLogged)
        {
            _switchLogged = true;
            Logs.WriteInfo(
                LogPrefix + "save-game recipe keys ON (Alundra.dll compiled in Debug, D-E16-33): "
                + $"{SaveBinaryKey} saves to '{BinarySlotName}' (binary), {SaveJsonKey} to '{JsonSlotName}' (JSON), "
                + $"{LoadKey} loads the most recent readable slot.");
        }

        // Every edge is updated every frame, so a key pressed on the same frame as a higher-priority one does
        // not act on the next frame instead.
        var saveBinary = ReadRisingEdge(keyboard, SaveBinaryKey, ref _saveBinaryKeyWasHeld);
        var saveJson = ReadRisingEdge(keyboard, SaveJsonKey, ref _saveJsonKeyWasHeld);
        var load = ReadRisingEdge(keyboard, LoadKey, ref _loadKeyWasHeld);

        if (saveBinary)
        {
            SaveToSlot(BinarySlotName, SaveGameFormat.Binary, state, worldName, player);
        }
        else if (saveJson)
        {
            SaveToSlot(JsonSlotName, SaveGameFormat.Json, state, worldName, player);
        }
        else if (load)
        {
            LoadMostRecent(state, player);
        }
    }

    private bool ReadRisingEdge(KeyboardManager? keyboard, Keys key, ref bool wasHeld)
    {
        var held = KeyHeldProviderForTests != null
            ? KeyHeldProviderForTests(key)
            : keyboard != null && keyboard.IsKeyPressed(key);

        var justPressed = held && !wasHeld;
        wasHeld = held;
        return justPressed;
    }

    // ---- K6: save ---------------------------------------------------------------------------------------------

    /// <summary>K6 (F5/F6): capture, validate, then write - nothing is written at the first refusal.</summary>
    private void SaveToSlot(string slot, SaveGameFormat format, AlundraGameState state, string? worldName, AlundraEntityScriptProxy? player)
    {
        var key = format == SaveGameFormat.Binary ? SaveBinaryKey : SaveJsonKey;

        // K6 step 2: the capture contract of E16.c C5 - the current world's name and the hero.
        if (!AlundraSaveGame.TryCaptureFromWorld(state, worldName ?? string.Empty, player, out var save) || save == null)
        {
            Refuse(key, $"nothing captured: no hero, or world '{worldName}' carries no map id.");
            return;
        }

        // K6 step 3: a capture that would not load is never written (contract of C5).
        if (!TryCreateRules(out var rules, out var rulesError))
        {
            Refuse(key, $"no validation rules: {rulesError}");
            return;
        }

        if (!save.TryValidate(rules!, out var validationError))
        {
            Refuse(key, $"the capture does not validate, nothing written: {validationError}");
            return;
        }

        // K6 step 4.
        var outcome = SaveSlots.Save(slot, save, format, save.BuildMetadata());
        if (outcome.Status == SaveGameSaveStatus.Saved)
        {
            Logs.WriteInfo(
                LogPrefix + $"{key}: saved map {save.InitialMapId} tile ({save.CameraTileX}, {save.CameraTileY}, "
                + $"{save.CameraTileZ}) to slot '{slot}' ({format}).");
        }
        else
        {
            Refuse(key, $"slot '{slot}' not written: {outcome.Status} {outcome.Message}");
        }
    }

    // ---- K7: load ---------------------------------------------------------------------------------------------

    /// <summary>K7 (F9): the most recent readable slot, loaded and validated.</summary>
    private void LoadMostRecent(AlundraGameState state, AlundraEntityScriptProxy? player)
    {
        // K7 step 2 (K5).
        var chosen = SelectMostRecent(SaveSlots.ListSlots());
        if (chosen == null)
        {
            Refuse(LoadKey, "no readable save slot with a known write time.");
            return;
        }

        var slot = chosen.Value.Name;

        // K7 step 3: every status but Loaded is a refusal, and so is Loaded without an object (K4).
        var outcome = SaveSlots.TryLoad(slot, out var save);
        if (outcome.Status != SaveGameLoadStatus.Loaded)
        {
            Refuse(LoadKey, $"slot '{slot}' not loaded: {outcome.Status} {outcome.Message}");
            return;
        }

        if (save == null)
        {
            Refuse(LoadKey, $"slot '{slot}' reported loaded without an object.");
            return;
        }

        // K7 step 4: the loaded object is untrusted (E16.c C4).
        if (!TryCreateRules(out var rules, out var rulesError))
        {
            Refuse(LoadKey, $"no validation rules: {rulesError}");
            return;
        }

        if (!save.TryValidate(rules!, out var validationError))
        {
            Refuse(LoadKey, $"slot '{slot}' refused by the validation: {validationError}");
            return;
        }

        Logs.WriteInfo(LogPrefix + $"{LoadKey}: slot '{slot}' loaded and validated (map {save.InitialMapId}).");
    }

    /// <summary>
    /// K5: the slot F9 loads - among the readable slots whose write time is known, the one with the greatest
    /// <see cref="AlundraSlotEntry.LastWriteTimeUtc"/>; on a tie, the greatest name in ordinal order. Null when
    /// there is none. Limits accepted for a recipe key (SD11, ADR-0013): a time in the future always wins;
    /// "readable" only means the header opened, so a most recent but damaged slot blocks F9 without falling back
    /// to the previous one; the engine's listing reads each slot whole (1 MiB at most). The player's own choice
    /// of a slot is E16.e.
    /// </summary>
    internal static AlundraSlotEntry? SelectMostRecent(IReadOnlyList<AlundraSlotEntry>? slots)
    {
        if (slots == null)
        {
            return null;
        }

        AlundraSlotEntry? best = null;
        foreach (var slot in slots)
        {
            if (!slot.IsReadable || slot.LastWriteTimeUtc == null || slot.Name == null)
            {
                continue;
            }

            if (best == null)
            {
                best = slot;
                continue;
            }

            var comparison = slot.LastWriteTimeUtc.Value.CompareTo(best.Value.LastWriteTimeUtc!.Value);
            if (comparison > 0 || (comparison == 0 && string.CompareOrdinal(slot.Name, best.Value.Name) > 0))
            {
                best = slot;
            }
        }

        return best;
    }

    // ---- Shared -----------------------------------------------------------------------------------------------

    /// <summary>K1: the rules of the validation - <see cref="RulesFactoryForTests"/> when set, the production
    /// rules otherwise. A failure to build them (no project path, an unreadable table) is a refusal, never an
    /// exception out of a key.</summary>
    private bool TryCreateRules(out AlundraSaveGameRules? rules, out string error)
    {
        rules = null;
        error = string.Empty;
        try
        {
            if (RulesFactoryForTests != null)
            {
                rules = RulesFactoryForTests();
            }
            else
            {
                var projectPath = EngineEnvironment.ProjectPath;
                rules = new AlundraSaveGameRules(
                    projectPath,
                    path => AssetCatalog.GetByFileName(path) != null,
                    AlundraItemTables.GetOrCreate(projectPath));
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            rules = null;
        }

        if (rules == null && error.Length == 0)
        {
            error = "the rules factory returned null";
        }

        return rules != null;
    }

    private static void Refuse(Keys key, string reason)
    {
        Logs.WriteWarning(LogPrefix + $"{key} refused - {reason}");
    }

    /// <summary>Test-only: clears every piece of session state and every seam, so tests do not leak into each
    /// other through this singleton - same seam as the other session directors.</summary>
    internal void ResetForTests()
    {
        _switchLogged = false;
        KeyHeldProviderForTests = null;
        _saveBinaryKeyWasHeld = false;
        _saveJsonKeyWasHeld = false;
        _loadKeyWasHeld = false;
        _pendingLoad = null;
        RulesFactoryForTests = null;
        SaveSlots = new AlundraEngineSaveSlots();
        RecipeKeysEnabledOverrideForTests = null;
    }
}
