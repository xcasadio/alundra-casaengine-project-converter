#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Alundra.Scripts;

/// <summary>
/// E19.d2a S1 (docs/plan-e19-opcodes.md section 1.2h.1, D-E19-33): the presets of the test saves - a new game
/// plus the few flags, map-table entries and item counters that a map of the story chain reads - and the
/// builder that turns one into an <see cref="AlundraSaveGame"/>.
///
/// <para>Pure and without any global state or disk access: the builder starts from a fresh
/// <see cref="AlundraGameState"/> (flags at 0, identity map table), runs the new game's own
/// <see cref="AlundraPlayerManager.InitializeNewGameStats"/> and
/// <see cref="AlundraPlayerManager.InitializeNewGameInventory"/> with the item tables of the rules, applies the
/// preset, captures with <see cref="AlundraSaveGame.Capture"/> and validates with the real
/// <see cref="AlundraSaveGameRules"/>. A refusal is a <c>false</c> with a message, never an exception.</para>
///
/// <para>A save never carries the temporary flags (id &gt;= 0x8000) and keeps only the first
/// <see cref="AlundraSaveGame.GameFlagWordCount"/> words of the persistent bank, so a flag id of 2048 and above
/// would be lost silently by <see cref="AlundraSaveGame.Capture"/>: both are refused up front.</para>
///
/// <para>The slot of each preset is fixed, <c>test-&lt;name&gt;</c>, and follows the slot-name rule of the
/// engine's ADR-0044 (1 to 32 characters among <c>a-z0-9_-</c>), which the engine keeps internal.</para>
/// </summary>
public static class AlundraTestSaves
{
    /// <summary>The first id of the temporary flag bank (<c>g_temporaryFlags</c>), never saved.</summary>
    private const uint TemporaryFlagBit = 0x8000;

    /// <summary>The first persistent flag id a save cannot hold: 64 words of 32 bits.</summary>
    private const uint FirstUnsavedFlag = AlundraSaveGame.GameFlagWordCount * 32;

    /// <summary>One test save: where the hero arrives and what differs from a new game.</summary>
    public sealed record Preset(
        string Name,
        string Slot,
        int MapId,
        int TileX,
        int TileY,
        int TileZ,
        IReadOnlyList<uint> FlagsToSet,
        IReadOnlyList<uint> FlagsToClear,
        IReadOnlyList<(int Index, ushort Value)> MapTableEntries,
        IReadOnlyList<(int Index, short Count)> ItemCounts);

    /// <summary>
    /// The presets, values written in advance (docs/plan-e19-opcodes.md, S1). Flags: G203 = word 6 bit 11, G204 =
    /// word 6 bit 12, G1651 to G1655 = word 51 bits 19 to 23, G1660 = word 51 bit 28. Tables, from the original's
    /// setters: opcode 0x38 at 117 <c>@1068</c> (<c>[162] = 176</c>) and 178 <c>C[6]</c> (<c>[176] = 183</c>,
    /// <c>[162] = 183</c>).
    /// </summary>
    public static IReadOnlyList<Preset> Presets { get; } =
    [
        new Preset(
            "day3-after-dream", "test-day3-after-dream", 179, 17, 7, 1,
            [203u, 1651u, 1660u],
            [],
            [(162, (ushort)176)],
            []),
        new Preset(
            "day4-meeting", "test-day4-meeting", 185, 5, 18, 1,
            [204u, 1651u, 1652u, 1653u, 1654u, 1655u],
            [203u],
            [(162, (ushort)183), (176, (ushort)183)],
            []),
    ];

    /// <summary>Finds a preset by its exact name.</summary>
    public static bool TryFind(string name, out Preset? preset)
    {
        foreach (var candidate in Presets)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                preset = candidate;
                return true;
            }
        }

        preset = null;
        return false;
    }

    /// <summary>Builds the save of a preset by name; <c>false</c> with a message on an unknown name or any refusal.</summary>
    public static bool TryBuild(string name, AlundraSaveGameRules rules, out AlundraSaveGame? save, out string error)
    {
        if (!TryFind(name, out var preset))
        {
            save = null;
            error = Format($"unknown preset '{name}'");
            return false;
        }

        return TryBuild(preset!, rules, out save, out error);
    }

    /// <summary>Builds the save of a preset: the new game, the preset, the capture, the real validation.</summary>
    public static bool TryBuild(Preset preset, AlundraSaveGameRules rules, out AlundraSaveGame? save, out string error)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(rules);

        save = null;
        error = CheckPreset(preset);
        if (error.Length != 0)
        {
            return false;
        }

        var state = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameStats(state);
        AlundraPlayerManager.InitializeNewGameInventory(state, rules.ItemTables);

        foreach (var flag in preset.FlagsToSet)
        {
            state.AddFlag(flag, 1u << (int)(flag & 31));
        }

        foreach (var flag in preset.FlagsToClear)
        {
            state.SetFlag(flag, ~(1u << (int)(flag & 31)));
        }

        foreach (var (index, value) in preset.MapTableEntries)
        {
            state.MapIdToInternalMapIndexTable[index] = value;
        }

        foreach (var (index, count) in preset.ItemCounts)
        {
            state.NumberOfItems[index] = count;
        }

        var captured = AlundraSaveGame.Capture(state, preset.MapId, preset.TileX, preset.TileY, preset.TileZ);
        if (!captured.TryValidate(rules, out var validationError))
        {
            error = Format($"preset '{preset.Name}' refused by the save-game rules: {validationError}");
            return false;
        }

        save = captured;
        return true;
    }

    /// <summary>The refusals that come before the build: a flag a save cannot carry, an index out of its table.</summary>
    private static string CheckPreset(Preset preset)
    {
        foreach (var flag in preset.FlagsToSet)
        {
            var refusal = CheckFlag(preset, flag);
            if (refusal.Length != 0)
            {
                return refusal;
            }
        }

        foreach (var flag in preset.FlagsToClear)
        {
            var refusal = CheckFlag(preset, flag);
            if (refusal.Length != 0)
            {
                return refusal;
            }
        }

        foreach (var (index, _) in preset.MapTableEntries)
        {
            if (index < 0 || index >= AlundraSaveGame.MapIndexTableLength)
            {
                return Format($"preset '{preset.Name}': map table index {index} is outside 0..{AlundraSaveGame.MapIndexTableLength - 1}");
            }
        }

        foreach (var (index, _) in preset.ItemCounts)
        {
            if (index < 0 || index >= AlundraSaveGame.NumberOfItemsLength)
            {
                return Format($"preset '{preset.Name}': item counter index {index} is outside 0..{AlundraSaveGame.NumberOfItemsLength - 1}");
            }
        }

        return string.Empty;
    }

    private static string CheckFlag(Preset preset, uint flag)
    {
        if (flag >= TemporaryFlagBit)
        {
            return Format($"preset '{preset.Name}': flag {flag} is a temporary flag (id >= {TemporaryFlagBit}), a save never carries it");
        }

        if (flag >= FirstUnsavedFlag)
        {
            return Format($"preset '{preset.Name}': flag {flag} is outside the saved words (id >= {FirstUnsavedFlag}, 2048), the save would lose it");
        }

        return string.Empty;
    }

    private static string Format(FormattableString message)
    {
        return message.ToString(CultureInfo.InvariantCulture);
    }
}
