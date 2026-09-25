#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// Port of <c>AlundraEngine.Gameplay.PlayerStats</c>
/// (alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerStats.cs:4-12): the five fields
/// E13's C0 needs (<see cref="Hp"/>, <see cref="HpMax"/>, <see cref="Mp"/>, <see cref="MpMax"/>,
/// <see cref="Money"/>), then <see cref="WeaponId"/> and <see cref="ItemId"/>, which E13.c S3 added for
/// the HUD's weapon and accessory boxes (docs/plan-e13c-icones-hud.md). <c>FalconTemp</c> and
/// <c>Falcon</c> are still not ported - no falcon system exists yet in this DLL.
///
/// <para><b>One instance, not two.</b> The original allocates a single <c>PlayerStats</c> object and
/// ALIASES it onto two globals - <c>g_saveData.PlayerStats = new PlayerStats()</c> immediately followed
/// by <c>g_playerStats = g_saveData.PlayerStats</c> (GameInitializer.cs:444-445, inside
/// <c>InitializePlayerStatsAndItems</c>) - so "the save" and "the current session" read and write the
/// very same object; there was never a second one to keep in sync. This port reproduces that by giving
/// <see cref="AlundraGameState"/> exactly one <see cref="AlundraPlayerStats"/> field (see that class'
/// own <c>PlayerStats</c> member) - docs/plan-e13-hud.md §1.5 bis, corrected by adverse review
/// 2026-09-18 after a first draft wrongly claimed two separate objects.</para>
///
/// <para>Fields are plain data, unclamped by construction - exactly like the original struct, whose own
/// clamps live in <c>PlayerManager</c>'s setters, not in <c>PlayerStats</c> itself. The bound-enforcing
/// setters are ported onto <see cref="AlundraPlayerManager"/> for the same reason (docs/plan-e13-hud.md
/// C0's own note: "l'emplacement des setters est ton choix local" - kept beside <see cref="AlundraPlayerManager"/>
/// because that is where the original's own <c>SetPlayerHp</c>/<c>SetPlayerHpMax</c>/<c>SetPlayerMp</c>/
/// <c>SetPlayerMpMax</c>/<c>SetMoney</c> live, PlayerManager.cs:1671-1830, and because
/// <see cref="AlundraPlayerManager"/> already takes an explicit <see cref="AlundraGameState"/> parameter
/// on every other method in this port, the same shape these setters need).</para>
/// </summary>
public sealed class AlundraPlayerStats
{
    /// <summary>Current HP. New Game default 10 - port of the New Game branch's
    /// <c>SetPlayerHp(10)</c> call (GameInitializer.cs:372-376, <c>SlotData == 0</c>).</summary>
    public short Hp = 10;

    /// <summary>Max HP. New Game default 10 - port of <c>SetPlayerHpMax(10)</c> (GameInitializer.cs:372).</summary>
    public short HpMax = 10;

    /// <summary>Current MP. New Game default 0 - port of <c>SetPlayerMp(0)</c> (GameInitializer.cs:375).</summary>
    public short Mp;

    /// <summary>Max MP. New Game default 0 - port of <c>SetPlayerMpMax(0)</c> (GameInitializer.cs:374).</summary>
    public short MpMax;

    /// <summary>Money. New Game default 0 - port of <c>SetMoney(0)</c> (GameInitializer.cs:376).</summary>
    public short Money;

    /// <summary>The equipped weapon, as a 1-based weapon slot (1 is the sword's slot, up to 6); -1 means none,
    /// the value <see cref="AlundraPlayerManager.SetPlayerWeaponId"/> stores for no weapon, and it rejects 0
    /// (ALUN_CD.EXE 0x8004e484, plan E13.d SI9.b) - 0 is only the constructed default, which resolves to no
    /// weapon too. Port of <c>PlayerStats.WeaponId</c> (PlayerStats.cs:9, a <c>short</c>). Constructed at 0, the
    /// value <c>new PlayerStats()</c> gives it; the New Game then sets 1 through
    /// <see cref="AlundraPlayerManager.SetPlayerWeaponId"/> (GameInitializer.cs:410), called by
    /// <see cref="AlundraPlayerManager.InitializeNewGameInventory"/>. Not baked in as 1 like <see cref="Hp"/>
    /// is baked in as 10: the weapon is only meaningful once the item counters exist, which the same
    /// New Game step fills.</summary>
    public short WeaponId;

    /// <summary>The equipped accessory, as an item id. Port of <c>PlayerStats.ItemId</c> (PlayerStats.cs:10,
    /// a <c>short</c>). Constructed at 0 and never set by the New Game - so the HUD's accessory box stays
    /// empty, as in the original (<see cref="AlundraPlayerManager.SetItemIdFromCurrentItemId"/> finds no
    /// count for item 0).</summary>
    public short ItemId;

    /// <summary>E13.d D4 (docs/plan-e13d-inventaire.md, D-E13D-5): port of <c>PlayerStats.Falcon</c> -
    /// the falcon-key count <c>DisplayAmountOfMoneyFalconKeys</c> shows next to the regular key count
    /// (MainInventoryManager.cs:1555-1587, <c>GetNumberOfFalcon()</c>). No falcon system exists in this
    /// DLL yet (this class' own doc, above) - kept at 0, the value a New Game has, with no mechanic that
    /// ever changes it (D-E13D-5: "affichés ; aucune mécanique de faucon").</summary>
    public short Falcon;

    /// <summary>E13.d D4: port of <c>PlayerStats.FalconTemp</c> - the original adds this to
    /// <see cref="Falcon"/> for the inventory's displayed total (<c>GetNumberOfFalconTemp()</c>,
    /// MainInventoryManager.cs:1556). Same "0, no mechanic" status as <see cref="Falcon"/>.</summary>
    public short FalconTemp;

    /// <summary>Test-only: restores this object to its New-Game-equivalent construction state, the same
    /// seam every other session-singleton field on <see cref="AlundraGameState"/> gets from
    /// <see cref="AlundraGameState.ResetForTests"/>.</summary>
    internal void ResetForTests()
    {
        Hp = 10;
        HpMax = 10;
        Mp = 0;
        MpMax = 0;
        Money = 0;
        WeaponId = 0;
        ItemId = 0;
        Falcon = 0;
        FalconTemp = 0;
    }
}
