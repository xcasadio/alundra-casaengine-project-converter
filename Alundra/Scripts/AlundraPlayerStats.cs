#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// Port of <c>AlundraEngine.Gameplay.PlayerStats</c>
/// (alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerStats.cs:4-12) - just the five
/// fields E13's C0 needs (<see cref="Hp"/>, <see cref="HpMax"/>, <see cref="Mp"/>, <see cref="MpMax"/>,
/// <see cref="Money"/>). <c>WeaponId</c>, <c>ItemId</c>, <c>FalconTemp</c> and <c>Falcon</c> are not
/// ported here - no weapon/item/falcon system exists yet in this DLL.
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
    }
}
