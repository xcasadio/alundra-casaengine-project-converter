#nullable enable
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Scripts;

namespace Alundra.Combat;

/// <summary>
/// The questions the opcodes <c>0x87</c> (<c>0x80040048</c>) and <c>0x95</c> (<c>0x800407C0</c>) ask the hit state (E20.c1b K4, docs/plan-e20-heros.md section 1.6), as pure functions over entity proxies:
/// is a target touched by an attack of a given type, and is some attacker's stored box inside a rectangle. The interpreter (<see cref="EventProgramRunner"/>) calls them; the oracle
/// <c>docs/plan-e20-annexe/e20-c1b/oracle/out/K3.txt</c> pins them.
/// </summary>
internal static class HitOpcodes
{
    /// <summary>
    /// The 12 bytes the binary reads at <c>0x80098FA4 + typeIdx</c>: the attack types <c>1, 2, 3, 4, 5, 5, 6, 7</c> (so the index 1 is the hammer, 2 the arrow, 3 the bomb..., the sword is the index 0), then the first four
    /// bytes of the handler table that follows it (<c>58 D1 03 80</c>). Only the index 10 can match an attack nibble (3, like the index 2); 8, 9 and 11 never do (above 15).
    /// </summary>
    internal static readonly byte[] TypeNibbles = { 1, 2, 3, 4, 5, 5, 6, 7, 0x58, 0xD1, 3, 0x80 };

    /// <summary>The attack nibble an index asks for; -1 for an index the table of 12 does not cover (the binary reads past it; no site of the corpus does).</summary>
    public static int TypeNibble(int typeIndex) => (uint)typeIndex < (uint)TypeNibbles.Length ? TypeNibbles[typeIndex] : -1;

    /// <summary>The attack of <paramref name="attacker"/> when it counts for the opcodes: a frame box (<c>+0x1D4</c>) and an attack (<c>+0x1C8</c>) with a non-zero attribute byte whose low nibble is <paramref name="nibble"/>.</summary>
    private static bool AttacksWithNibble(AlundraEntityScriptProxy attacker, int nibble)
        => attacker.HasFrameBox && attacker.CurrentAttack is { Attribute: not 0 } attack && (attack.Attribute & 0xF) == nibble;

    /// <summary><c>0x87</c>: true when <paramref name="target"/> is touched (<c>+0x224</c>) by an entity that has a frame box and an attack of the type <paramref name="typeIndex"/>.</summary>
    public static bool IsTouchedByAttackType(AlundraEntityScriptProxy target, int typeIndex)
        => target.TouchingEntity?.GameplayProxy is AlundraEntityScriptProxy attacker && AttacksWithNibble(attacker, TypeNibble(typeIndex));

    /// <summary>
    /// <c>0x95</c>: true when some attacker of the world has its STORED hit box (<c>+0x1FC</c>, <see cref="AlundraEntityScriptProxy.StoreHitBox"/>) inside the rectangle (<paramref name="originX"/>, <paramref name="originY"/>,
    /// <paramref name="originZ"/>, sizes <paramref name="sizeX"/> (also the Z size), <paramref name="sizeY"/>; raw 16.16, the Z in the binary's convention) and passes the filters: status Normal or Deactivated, not blocked,
    /// a frame box, an attack of the attribute nibble <paramref name="typeIndex"/> asks for, and <c>Flags &amp; mask != 0</c>. A zero mask answers false at once. The hero is slot 0, scanned first, then the others in order.
    /// <para>
    /// The overlap is the hit pass's (<see cref="HitPass.AxisOverlaps"/>) with the rectangle as the body, but NOT widened on the positive side: with <c>d = hitBox - origin</c> a hit when <c>d &gt;= 0</c> and <c>d &lt; size</c>,
    /// or <c>d &lt; 0</c> and <c>-d &lt; attackerSize + 1</c> (K3 boundaries). The DLL's Z is the binary's minus 1 (ADR-0026): the stored box gets one raw unit on Z, on this comparison only.
    /// </para>
    /// </summary>
    public static bool AnyAttackerInRectangle(
        IReadOnlyList<AlundraEntityScriptProxy> entities, AlundraEntityScriptProxy? hero, int mask, int typeIndex, int originX, int originY, int originZ, int sizeX, int sizeY)
    {
        if (mask == 0)
        {
            return false;
        }

        var nibble = TypeNibble(typeIndex);
        if (hero != null && Matches(hero, mask, nibble, originX, originY, originZ, sizeX, sizeY))
        {
            return true;
        }

        for (var i = 0; i < entities.Count; i++)
        {
            var candidate = entities[i];
            if (!ReferenceEquals(candidate, hero) && Matches(candidate, mask, nibble, originX, originY, originZ, sizeX, sizeY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(AlundraEntityScriptProxy attacker, int mask, int nibble, int originX, int originY, int originZ, int sizeX, int sizeY)
    {
        if (!attacker.Status.IsActive() || attacker.BlockedByEntity != null || (attacker.Flags & (uint)mask) == 0 || !AttacksWithNibble(attacker, nibble))
        {
            return false;
        }

        unchecked
        {
            return HitPass.AxisOverlaps(attacker.HitBoxX, attacker.CollisionWidth, originX, sizeX - 1)
                && HitPass.AxisOverlaps(attacker.HitBoxY, attacker.CollisionDepth, originY, sizeY - 1)
                && HitPass.AxisOverlaps(attacker.HitBoxZ + 1, attacker.CollisionHeight, originZ, sizeX - 1);
        }
    }
}
