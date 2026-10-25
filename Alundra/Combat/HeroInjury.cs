#nullable enable
using Alundra.Entities;
using Alundra.Items;

namespace Alundra.Combat;

/// <summary>
/// E20.c2b T1 (docs/plan-e20-heros.md section 1.12.1): the entry of the damage of the hero (<c>0x80031A68</c>) as a pure function. STUB of the tests commit: it hurts nobody and writes nothing.
/// </summary>
internal static class HeroInjury
{
    /// <summary>The return of the entry when nothing hurts the hero.</summary>
    public const int NoInjury = -1;

    public static int Enter(EntityScriptProxy hero, BalanceRecord? sheet, EquipmentRecords? equipment, uint animationLatch, int bootsLevel) => NoInjury;
}
