#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Alundra.Session;
using Alundra.Hero;

namespace Alundra.SaveGames;

/// <summary>
/// E19.d2a S1 (docs/plan-e19-opcodes.md section 1.2h.1, D-E19-33): the presets of the test saves - a new game
/// plus the few flags, map-table entries and item counters that a map of the story chain reads - and the
/// builder that turns one into an <see cref="SaveGame"/>.
///
/// <para>Pure and without any global state or disk access: the builder starts from a fresh
/// <see cref="GameState"/> (flags at 0, identity map table), runs the new game's own
/// <see cref="PlayerManager.InitializeNewGameStats"/> and
/// <see cref="PlayerManager.InitializeNewGameInventory"/> with the item tables of the rules, applies the
/// preset, captures with <see cref="SaveGame.Capture"/> and validates with the real
/// <see cref="SaveGameRules"/>. A refusal is a <c>false</c> with a message, never an exception.</para>
///
/// <para>A save never carries the temporary flags (id &gt;= 0x8000) and keeps only the first
/// <see cref="SaveGame.GameFlagWordCount"/> words of the persistent bank, so a flag id of 2048 and above
/// would be lost silently by <see cref="SaveGame.Capture"/>: both are refused up front.</para>
///
/// <para>The slot of each preset is fixed, <c>test-&lt;name&gt;</c>, and follows the slot-name rule of the
/// engine's ADR-0044 (1 to 32 characters among <c>a-z0-9_-</c>), which the engine keeps internal.</para>
/// </summary>
public static class TestSaves
{
    /// <summary>The first id of the temporary flag bank (<c>g_temporaryFlags</c>), never saved.</summary>
    private const uint TemporaryFlagBit = 0x8000;

    /// <summary>The first persistent flag id a save cannot hold: 64 words of 32 bits.</summary>
    private const uint FirstUnsavedFlag = SaveGame.GameFlagWordCount * 32;

    /// <summary>
    /// One test save: where the hero arrives and what differs from a new game. The seven last fields are optional (E20.f0 T3, docs/plan-e20-heros.md section 1.8): a
    /// <c>null</c> keeps the new game's value, otherwise the value goes through the DLL's own setter after the new-game kit and the item counters
    /// (<see cref="PlayerManager.SetPlayerHpMax"/>, <see cref="PlayerManager.SetPlayerHp"/>, <see cref="PlayerManager.SetPlayerMpMax"/>,
    /// <see cref="PlayerManager.SetPlayerMp"/>, <see cref="PlayerManager.SetMoney"/>, <see cref="PlayerManager.SetPlayerWeaponId"/>,
    /// <see cref="PlayerManager.SetCurrentItemId"/>), and <see cref="CheckPreset"/> refuses a value outside the save's domains instead of letting a setter clamp it.
    /// <c>Weapon</c> is the equipped weapon SLOT (-1 or 1 to 6), <c>Item</c> the Circle item.
    /// </summary>
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
        IReadOnlyList<(int Index, short Count)> ItemCounts,
        int? Weapon = null,
        int? Item = null,
        int? Hp = null,
        int? HpMax = null,
        int? Mp = null,
        int? MpMax = null,
        int? Money = null);

    /// <summary>The raw <c>NumberOfItems</c> index of an item's counter: <c>2 * item + 1</c>.</summary>
    private static int CounterIndex(int item) => 2 * item + 1;

    /// <summary>
    /// What every weapon test save carries besides its weapons (docs/plan-e20-annexe/e20-armes/test-means.md section 7): the bomb (35, one), the herbs (36, nine), the
    /// strength and magic elixirs (37, 38), the fortifier (41) and the four scrolls (43, 45, 47, 49). The kit's basic sword (item 1) stays: the highest rank of a slot
    /// wins whatever the lower ones (f-check.md row 43, correction C10).
    /// </summary>
    private static readonly (int Item, short Count)[] TestKitItems =
    [
        (35, 1), (36, 9), (37, 1), (38, 1), (41, 1), (43, 1), (45, 1), (47, 1), (49, 1),
    ];

    private static IReadOnlyList<(int Index, short Count)> Loadout(params int[] extraItems)
    {
        var counts = new List<(int Index, short Count)>();
        foreach (var (item, count) in TestKitItems)
        {
            counts.Add((CounterIndex(item), count));
        }

        foreach (var item in extraItems)
        {
            counts.Add((CounterIndex(item), 1));
        }

        return counts;
    }

    /// <summary>A weapon test save: the loadout above on a map, the weapon slot equipped, life 4 of 10 and magic 4 of 4 (herbs and the staff need both to be testable).</summary>
    private static Preset WeaponPreset(string name, int mapId, int tileX, int tileY, int tileZ, int weaponSlot, params int[] extraItems)
        => new(
            name, "test-" + name, mapId, tileX, tileY, tileZ,
            [], [], [], Loadout(extraItems),
            Weapon: weaponSlot, Hp: 4, HpMax: 10, Mp: 4, MpMax: 4);

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

        // E20.f0 T3: the weapon test saves (docs/plan-e20-annexe/e20-armes/test-means.md section 7). The weapon items by slot: 1 sword (1, 2, 3, 4, 8), 2 bows (5, 6), 3 flails (9, 10),
        // 4 ice rod (14), 5 fire rod (16), 6 magic staff (7); the books 44, 46, 48, 50 win over the scrolls of their slot. Map 14 is the Beannoides arena (tile (8, 6, 0)); the
        // fire rod save arrives on map 303 (38, 11, 1) and the bow save on map 16 (34, 31, 3), where the arenas A3 and A4 stand.
        WeaponPreset("arsenal-a-m14", 14, 8, 6, 0, 1, 5, 9, 14, 16, 7),
        WeaponPreset("arsenal-b-m14", 14, 8, 6, 0, 1, 4, 6, 10, 14, 16, 7, 44, 46, 48, 50),
        WeaponPreset("steel-m14", 14, 8, 6, 0, 1, 2),
        WeaponPreset("demon-m14", 14, 8, 6, 0, 1, 3),
        WeaponPreset("fire-m303", 303, 38, 11, 1, 5, 5, 9, 14, 16, 7),
        WeaponPreset("arrow-m16", 16, 34, 31, 3, 2, 5, 9, 14, 16, 7),

        // E20.i T5 (docs/plan-e20-heros.md section 1.14): the swim. Map 14, tile (40, 48, 1), on the bank of the pond (81 deep cells at the tiles x 40 to 46, y 43 to 59, height 0; the bank is height 1, walkable, no
        // portal floor), with the swim boots (item 0x1B, level 2) on top of the new game's kit. Walk south off the bank into the water. Without the boots the original stops the hero at the edge of the deep water;
        // this port does not port that wall yet (O-E19-22), the hero swims either way.
        new Preset("swim-m14", "test-swim-m14", 14, 40, 48, 1, [], [], [], [(CounterIndex(0x1B), 1)]),

        // E20.e0c T4 (docs/plan-e20-heros.md section 1.26, T0 (k)): the pickup recipes, on the new game's kit (the basic sword) with HP 4/10 and MP 2/4 so that the hearts, the herbs and the crystals show. `pickup-m11`: the Debug zone (map 11),
        // tile (27, 18, 10), the hero rests on the ground; to the north on the same column the herb (27, 16), the Small Potion (27, 15) and the Large Potion (27, 14); the Magic Extract (29, 15), the Brew (29, 14) and a key (24, 20); the 22 key objects
        // are in the rows 22 to 30. `loot-m14`: the Beannoides arena (map 14), tile (19, 34, 3), a Beannoid 72 pixels to the east at the same height (record 1, tile (22, 34, 3)); kill several, what they let fall depends on the generator.
        new Preset("pickup-m11", "test-pickup-m11", 11, 27, 18, 10, [], [], [], [], Weapon: 1, Hp: 4, HpMax: 10, Mp: 2, MpMax: 4),
        new Preset("loot-m14", "test-loot-m14", 14, 19, 34, 3, [], [], [], [], Weapon: 1, Hp: 4, HpMax: 10, Mp: 2, MpMax: 4),

        // E20.e T3 (docs/plan-e20-heros.md section 1.27): the cut of the grass. Map 14, tile (31, 43, 1), on the flat ground (height 1) east of the 16 bushes of the cells 26 to 29, rows 40 to 44 (the rows 43 and 44 are walkable up to the
        // bushes; row 42 is a wall to the east). The kit with the Steel Edge in the slot 1 (a tap of the Square cuts with the swing 0x3F, a held Square cuts with the wave when it is released). A save cannot carry the direction
        // (always 0, south): walk west first, then swing.
        WeaponPreset("herbe-m14", 14, 31, 43, 1, 1, 2),

        // E19.d2c2 D5b T7 (docs/plan-e19-opcodes.md section 1.2h.3.2b, D-D5B-10, D-E19-124): the lift of the map 22. Tile (46, 8), level 2, south of the right-hand lever, on the new game's kit. A start already on the platform is
        // impossible (the state of the lever, the temporary flags T1 and T3, is not saved): strike the lever, go down into the hollow, jump onto the platform and wait; Alundra rises with it.
        new Preset("elevator-m22", "test-elevator-m22", 22, 46, 8, 2, [], [], [], []),

        // E20.g4a T1 (docs/plan-e20-heros.md section 1.29.1): the barricade of the ship Klark (map 392). Tile (29, 13), level 4, with the flag G1641 posed (the same start as the arc A5: the ArcSpec of
        // ShipCorridorArcTests). The map program B1 walks the hero 48 pixels east to (775, 216), then leaves him the hand: hold Down, then Left along the north wall of the door (the hero is aligned
        // on y = 295 + a multiple of 0.5), jump (Cross + Left) when he is flush against the crates (x = 657), land on the low crate, walk off its west edge and go round the crate rec9 by the south.
        // Do not hold a direction when the ship polls the pad (every 16 ticks): the hero rolls in a random direction.
        new Preset("barricade-m392", "test-barricade-m392", 392, 29, 13, 4, [1641u], [], [], []),
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
    public static bool TryBuild(string name, SaveGameRules rules, out SaveGame? save, out string error)
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
    public static bool TryBuild(Preset preset, SaveGameRules rules, out SaveGame? save, out string error)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(rules);

        save = null;
        error = CheckPreset(preset);
        if (error.Length != 0)
        {
            return false;
        }

        var state = new GameState();
        PlayerManager.InitializeNewGameStats(state);
        PlayerManager.InitializeNewGameInventory(state, rules.ItemTables);

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

        // E20.f0 T3: the optional fields, through the setters of the DLL (maxima before the values they bound, the weapon after the counters it resolves).
        if (preset.HpMax is { } hpMax)
        {
            PlayerManager.SetPlayerHpMax(state, hpMax);
        }

        if (preset.Hp is { } hp)
        {
            PlayerManager.SetPlayerHp(state, (short)hp);
        }

        if (preset.MpMax is { } mpMax)
        {
            PlayerManager.SetPlayerMpMax(state, (short)mpMax);
        }

        if (preset.Mp is { } mp)
        {
            PlayerManager.SetPlayerMp(state, (short)mp);
        }

        if (preset.Money is { } money)
        {
            PlayerManager.SetMoney(state, (short)money);
        }

        if (preset.Weapon is { } weapon)
        {
            PlayerManager.SetPlayerWeaponId(state, rules.ItemTables, weapon);
        }

        if (preset.Item is { } item)
        {
            PlayerManager.SetCurrentItemId(state, (uint)item);
        }

        var captured = SaveGame.Capture(state, preset.MapId, preset.TileX, preset.TileY, preset.TileZ);
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
            if (index < 0 || index >= SaveGame.MapIndexTableLength)
            {
                return Format($"preset '{preset.Name}': map table index {index} is outside 0..{SaveGame.MapIndexTableLength - 1}");
            }
        }

        foreach (var (index, _) in preset.ItemCounts)
        {
            if (index < 0 || index >= SaveGame.NumberOfItemsLength)
            {
                return Format($"preset '{preset.Name}': item counter index {index} is outside 0..{SaveGame.NumberOfItemsLength - 1}");
            }
        }

        return CheckStats(preset);
    }

    /// <summary>The optional stat fields against the domains of the save (<see cref="SaveGame"/>): a setter would clamp a value outside them silently.</summary>
    private static string CheckStats(Preset preset)
    {
        var newGame = new GameState();
        PlayerManager.InitializeNewGameStats(newGame);
        var hpMax = preset.HpMax ?? newGame.PlayerStats.HpMax;
        var mpMax = preset.MpMax ?? newGame.PlayerStats.MpMax;

        if (preset.HpMax is { } declaredHpMax && (declaredHpMax < 0 || declaredHpMax > SaveGame.MaxHpMax))
        {
            return Format($"preset '{preset.Name}': hpMax {declaredHpMax} is outside 0..{SaveGame.MaxHpMax}");
        }

        if (preset.Hp is { } hp && (hp < 0 || hp > hpMax))
        {
            return Format($"preset '{preset.Name}': hp {hp} is outside 0..{hpMax} (the maximum)");
        }

        if (preset.MpMax is { } declaredMpMax && (declaredMpMax < 0 || declaredMpMax > SaveGame.MaxMpMax))
        {
            return Format($"preset '{preset.Name}': mpMax {declaredMpMax} is outside 0..{SaveGame.MaxMpMax}");
        }

        if (preset.Mp is { } mp && (mp < 0 || mp > mpMax))
        {
            return Format($"preset '{preset.Name}': mp {mp} is outside 0..{mpMax} (the maximum)");
        }

        if (preset.Money is { } money && (money < 0 || money > SaveGame.MaxMoney))
        {
            return Format($"preset '{preset.Name}': money {money} is outside 0..{SaveGame.MaxMoney}");
        }

        if (preset.Weapon is { } weapon && weapon != -1 && (weapon < 1 || weapon > SaveGame.MaxWeaponId))
        {
            return Format($"preset '{preset.Name}': weapon {weapon} is outside -1 or 1..{SaveGame.MaxWeaponId}");
        }

        if (preset.Item is { } item && (item < 0 || item >= PlayerManager.ItemsCount))
        {
            return Format($"preset '{preset.Name}': item {item} is outside 0..{PlayerManager.ItemsCount - 1}");
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
