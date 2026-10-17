#nullable enable
using Alundra.Scripts;
using Alundra.Items;

namespace Alundra.Combat;

/// <summary>
/// The damage functions of the binary as pure functions (E20.c1b K1, docs/plan-e20-heros.md section 1.6): <c>ResolveBalanceTarget</c> (<c>0x8004464C</c>) and <c>ComputeNewHp</c> (<c>0x8003A374</c>). Every input is a
/// parameter: the target's sheet (its balance record, or for the hero the combined sheet of <c>0x80127008</c>, E20.c2), the attack, the three equipment records and the frozen hero animation
/// (<c>0x80146DF8</c>, <see cref="HitPass.HeroAnimationLatch"/>). No production code calls them yet: the programs D of the entities that decide on a hit are native handlers (E14.a), and the hero's entry
/// is E20.c2.
/// </summary>
internal static class Damage
{
    /// <summary>The bit of the attack attribute that makes the equipment's powers count (<c>0x80</c>).</summary>
    public const int EquipmentBit = 0x80;

    /// <summary>
    /// <c>ResolveBalanceTarget</c>: the life after the attack. A missing sheet, a missing attack or an attribute byte of 0 leave <paramref name="hp"/> alone. Otherwise <c>r</c> is the byte <c>2 + (attribute &amp; 0xF)</c> of the
    /// sheet (<see cref="BalanceRecord.ByteAt"/>: the attribute nibble 0 reads the maximum life): <c>r &amp; 0xC0 == 0x40</c> kills (0), <c>0x80</c> and <c>0xC0</c> leave the life alone, and else the damage is
    /// <c>((power + equipment) * r) &gt;&gt; 4</c>, at least 1, taken off down to 0. The equipment term (attribute bit <c>0x80</c>, for ANY attacker) is, for each non-empty equipment record, the power of
    /// <c>Attacks[g + 1 &lt; count ? g + 1 : 0]</c>, <c>g</c> being <paramref name="animationLatch"/> read as a signed number (a record with no attack counts nothing).
    /// </summary>
    public static int ResolveBalanceTarget(BalanceAttack? attack, BalanceRecord? sheet, int hp, AlundraEquipmentRecords? equipment, uint animationLatch)
    {
        if (sheet == null || attack == null || attack.Attribute == 0)
        {
            return hp;
        }

        var response = sheet.ByteAt(2 + (attack.Attribute & 0xF));
        var kind = response & 0xC0;
        if (kind == 0x40)
        {
            return 0;
        }

        if (kind != 0)
        {
            return hp;
        }

        var power = attack.Power;
        if ((attack.Attribute & EquipmentBit) != 0 && equipment != null)
        {
            for (var slot = 0; slot < AlundraEquipmentRecords.Count; slot++)
            {
                power += EquipmentPower(equipment[slot], animationLatch);
            }
        }

        var damage = (power * response) >> 4;
        if (damage == 0)
        {
            damage = 1;
        }

        return damage < hp ? hp - damage : 0;
    }

    private static int EquipmentPower(BalanceRecord? record, uint animationLatch)
    {
        if (record == null || record.Attacks.Count == 0)
        {
            return 0;
        }

        // g is the signed latch; -1 reads Attacks[0] like an index that is not below the count. No lower value occurs (the latch is an animation id); it is clamped to 0 rather than read before the array.
        var next = (long)(int)animationLatch + 1;
        var index = next < record.Attacks.Count ? next : 0;
        return record.Attacks[(int)(index < 0 ? 0 : index)].Power;
    }

    /// <summary>
    /// <c>ComputeNewHp</c>: sets the life of <paramref name="target"/> from the attack of the entity that touches it (<c>TouchingEntity</c>, whose <see cref="AlundraEntityScriptProxy.CurrentAttack"/> is read; the binary
    /// does not test it for null, a caller only asks after a hit; here an absent attacker leaves the life alone) against <paramref name="sheet"/>, the sheet of the target (<c>+0x1C4</c>, or <c>0x80127008</c> for the hero),
    /// and returns whether the life is now 0.
    /// </summary>
    public static bool ComputeNewHp(AlundraEntityScriptProxy target, BalanceRecord? sheet, AlundraEquipmentRecords? equipment, uint animationLatch)
    {
        var attack = (target.TouchingEntity?.GameplayProxy as AlundraEntityScriptProxy)?.CurrentAttack;
        target.Hp = ResolveBalanceTarget(attack, sheet, target.Hp, equipment, animationLatch);
        return target.Hp == 0;
    }
}
