#nullable enable
using System;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;

namespace Alundra.Combat;

/// <summary>
/// E20.c2b T1 (docs/plan-e20-heros.md section 1.12.1, D-C2B-3): the entry of the damage of the hero (<c>0x80031A68</c>, 58 instructions, no parameter but the hero <c>0x80127D30</c>) as a pure function. Every input is a
/// parameter: the hero, the sheet of <c>0x80127008</c> (<see cref="EquipmentRecords.HeroSheet"/>), the three equipment records, the frozen animation of the hero (<see cref="HitPass.HeroAnimationLatch"/>) and the boots level
/// (<c>[0x80127000]</c>, <see cref="EquipmentRecords.BootsLevel"/>: the value the previous tick's <c>UpdateItemEffectState</c> left). It returns the direction the hurt hero faces (the caller writes it to <c>+0x8C</c>,
/// with the state and the invulnerability) or <see cref="NoInjury"/>.
/// <list type="bullet">
/// <item><description><b>Toucher</b> (<see cref="EntityScriptProxy.TouchingEntity"/>, <c>+0x224</c>, non-null): <see cref="Damage.ComputeNewHp"/> of the hero (its result is dropped: a life that falls to 0 is still a hurt; the response
/// <c>0x80</c> or <c>0xC0</c> of the sheet hurts without taking life, <c>0x40</c> takes it all), then the direction <see cref="ScriptHelper.GetDirectionToTarget"/> of hero minus toucher (a hero and a toucher on the same point give 24).
/// The branch tests neither <c>+0x1CC</c>, nor <c>+0x1D0</c>, nor the boots, nor the state: the protection comes from the hit pass, which skips a target whose <c>+0x1D0</c>, <c>+0x1CC</c> or animation byte bit <c>0x40</c> is set.</description></item>
/// <item><description><b>Hazard cell</b> (no toucher): <c>+0x1CC == 0</c>, boots level below 3 and <c>(+0x180 &amp; 0x180) &gt;&gt; 7</c> not zero; the life falls by <c>{0, 1, 2, 3}[that]</c> (the table of <c>0x80028B8C</c>; the
/// decompilation writes 0, 256, 512, 768, the binary decides), down to 0, the armour not counting; the direction is <c>(+0x8C + 0x10) &amp; 0x1F</c>.</description></item>
/// </list>
/// A hero with neither, or a protected one, gets <see cref="NoInjury"/> and nothing is written. With no sheet (a montage without tables) the toucher's damage leaves the life alone, as <see cref="Damage.ResolveBalanceTarget"/>
/// does (D-C2B-9: the binary would take 1, invisible in production where the sheet is set at the adoption).
/// </summary>
internal static class HeroInjury
{
    /// <summary>The return of the entry when nothing hurts the hero.</summary>
    public const int NoInjury = -1;

    /// <summary>The boots level that spares the hero from the hazard cells (<c>sltiu v0, 3</c> at <c>0x80031AC4</c>).</summary>
    public const int SparingBootsLevel = 3;

    /// <summary>The bits of <see cref="EntityScriptProxy.CombinedVramFlagsOR"/> (<c>+0x180</c>) that carry the cost of a hazard cell.</summary>
    public const uint CellCostBits = 0x180;

    public static int Enter(EntityScriptProxy hero, BalanceRecord? sheet, EquipmentRecords? equipment, uint animationLatch, int bootsLevel)
    {
        if (hero.TouchingEntity != null)
        {
            Damage.ComputeNewHp(hero, sheet, equipment, animationLatch);

            // The toucher of the binary is always an entity with a program; a bare Entity here is read as standing on the hero (24, the direction of (0, 0)).
            if (hero.TouchingEntity.GameplayProxy is EntityScriptProxy toucher)
            {
                return (int)ScriptHelper.GetDirectionToTarget(unchecked(hero.PosX - toucher.PosX), unchecked(hero.PosY - toucher.PosY));
            }

            return (int)ScriptHelper.GetDirectionToTarget(0, 0);
        }

        if (hero.DamagedTickCounter != 0 || bootsLevel >= SparingBootsLevel)
        {
            return NoInjury;
        }

        var cost = (int)((hero.CombinedVramFlagsOR & CellCostBits) >> 7); // {0, 1, 2, 3}[n] is n itself.
        if (cost == 0)
        {
            return NoInjury;
        }

        hero.Hp = Math.Max(0, hero.Hp - cost);
        return (int)((hero.TargetDirection + 0x10) & 0x1F);
    }
}
