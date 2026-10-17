#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;

namespace Alundra.Combat;

/// <summary>
/// The hit pass of the binary, <c>UpdateBalanceRecords</c> (<c>0x80039300</c>, the last pass of the entity block, E20.c1a C3, docs/plan-e20-heros.md section 1.5). Pure logic over a list of entity proxies, headless-testable:
/// <list type="number">
/// <item><description>The animation of the hero is latched first, even when the list is empty (<c>0x80039344/48</c>): <see cref="HeroAnimationLatch"/> is <c>+0x88</c> of the hero (<see cref="AlundraEntityScriptProxy.TargetAnimationId"/>, not
/// the current animation) as it is at this point of the tick, after the animation clock. The damage calculation of E20.c1b reads it one tick later (<c>0x80146DF8</c>).</description></item>
/// <item><description>For each attacker of the list that has a frame box, a current attack with a non-zero attribute byte and a non-zero class mask (<c>0x8003936C-0x800393C0</c>), each other entity of the list (the target loop,
/// <c>0x800393FC-0x80039758</c>) is tested in the order of the list. A target is skipped when its <c>+0x1D0</c> or <c>+0x1CC</c> counter is non-zero, when the animation byte <c>+0xB4</c> has the bit <c>0x40</c>, or when its
/// <c>Flags</c> have none of the bits of the mask. The mask is REORDERED from the attacker's flags: <c>((F &gt;&gt; 2) &amp; 1) | ((F &lt;&lt; 2) &amp; 8) | (F &amp; 0x1000 ? 0x800 : 0)</c> (HitsClassA hits ClassA, HitsClassB hits ClassB, HitsClassC
/// hits ClassC).</description></item>
/// <item><description>The overlap is an integer test on the three axes, in raw 16.16, WIDENED by one unit (<c>0x80039464-0x8003951C</c>): with <c>d = box - body</c>, a hit when <c>d &gt;= 0</c> and <c>d &lt; bodySize + 1</c>, or <c>d &lt; 0</c> and
/// <c>-d &lt; boxSize + 1</c>. The box is <c>HitBox = Pos + CollisionOffset</c>, the body <c>ModdedPos = Pos + Mod</c> of the target, both read now (the stored copies are stale in the DLL).</description></item>
/// <item><description>A hit: when the response of the target for the attack nibble, <c>record[(attribute &amp; 0xF) + 2]</c> of its balance record, is NOT <c>(r &amp; 0xC0) == 0x80</c> (immune), the target's <c>TouchingEntity</c> becomes the attacker
/// (and, just before, the effect 4 or 5 of the nibbles 6, 10 and 7, 9 is attached to it through <c>attachEffect</c>, E20.f3b); then always the target's <c>+0x1D0</c> becomes <c>0x19</c>, the attacker's <c>HitCounter</c> grows by one, and three sparks
/// (effect 9) appear at the centre of the overlap. The attacker goes on with the next targets (it can hit several); a target whose <c>+0x1D0</c> a first attacker has just set is skipped by the next ones (the first attacker of the list wins).</description></item>
/// </list>
/// No <c>Hp</c> changes here: the damage is E20.c1b (the program D of the target reads <c>TouchingEntity</c> at the next tick).
/// </summary>
internal sealed class HitPass
{
    /// <summary>The value the pass writes in <c>+0x1D0</c> of a target it hit: 25 ticks before the same target can be hit again.</summary>
    public const int HitCooldownTicks = 0x19;

    /// <summary>The animation byte bit that makes an entity untouchable while its animation plays (the hero's roll, a hurt Beannoid...).</summary>
    public const int InvulnerableAnimationBit = 0x40;

    /// <summary>The number of sparks of a hit (<c>0x800391A8</c>).</summary>
    public const int SparkCount = 3;

    /// <summary>The binary's <c>0x80146DF8</c>: <c>+0x88</c> of the hero, latched at the head of every pass.</summary>
    public uint HeroAnimationLatch;

    /// <summary>The number of hits the pass wrote (a counter for the tests).</summary>
    public int HitsWritten;

    /// <summary>The class mask a target's flags are tested against, reordered from the attacker's flags (<c>0x8003939C-0x800393BC</c>).</summary>
    public static uint ClassMaskOf(uint attackerFlags)
    {
        var mask = ((attackerFlags >> 2) & 1) | ((attackerFlags << 2) & 8);
        if ((attackerFlags & 0x1000) != 0)
        {
            mask |= 0x800;
        }

        return mask;
    }

    /// <summary>The effect a hit of the attack nibble attaches to a target that is not immune (<c>0x800395E8-0x80039668</c>): 4 for the nibbles 6 and 10, 5 for 7 and 9, 0 (none) for the others.</summary>
    internal static int EffectOfNibble(int nibble) => nibble switch
    {
        6 or 10 => 4,
        7 or 9 => 5,
        _ => 0,
    };

    /// <summary>One axis of the widened overlap test (see the class doc); all in raw 16.16.</summary>
    internal static bool AxisOverlaps(int boxPosition, int boxSize, int bodyPosition, int bodySize)
    {
        unchecked
        {
            var d = boxPosition - bodyPosition;
            return d >= 0 ? d < bodySize + 1 : -d < boxSize + 1;
        }
    }

    /// <summary>One axis of the centre of the overlap: <c>(max of the two minima + min of the two maxima) / 2</c>, truncated toward zero (<c>0x80039684-0x80039744</c>).</summary>
    internal static int AxisCentre(int boxPosition, int boxSize, int bodyPosition, int bodySize)
    {
        unchecked
        {
            var low = Math.Max(boxPosition, bodyPosition);
            var high = Math.Min(boxPosition + boxSize, bodyPosition + bodySize);
            return (low + high) / 2;
        }
    }

    /// <summary>
    /// Runs the pass over <paramref name="list"/> (the list of <c>0x800384F4</c> as the lists step built it: status 2 or 3, <c>BlockedByEntity</c> null, the hero first). <paramref name="hero"/> is slot 0 (its animation is latched even
    /// when <paramref name="list"/> is empty). <paramref name="emitSparks"/> is told the centre of the overlap of each hit (the world queues the three sparks on its effect stage). <paramref name="attachEffect"/> (E20.f3b, D-F3-9) is told
    /// the target and the effect (4 or 5) of a hit that is not immune, before the store of <c>TouchingEntity</c> and before the sparks.
    /// </summary>
    public void Run(IReadOnlyList<AlundraEntityScriptProxy> list, AlundraEntityScriptProxy? hero, Action<int, int, int>? emitSparks, Action<AlundraEntityScriptProxy, int>? attachEffect = null)
    {
        if (hero != null)
        {
            HeroAnimationLatch = hero.TargetAnimationId;
        }

        var count = list.Count;
        for (var a = 0; a < count; a++)
        {
            var attacker = list[a];
            var attack = attacker.CurrentAttack;
            if (attack == null || attack.Attribute == 0)
            {
                continue;
            }

            var mask = ClassMaskOf(attacker.Flags);
            if (mask == 0 || !attacker.EvaluateFrameBox())
            {
                continue;
            }

            unchecked
            {
                attacker.HitBoxX = attacker.PosX + attacker.CollisionOffsetX;
                attacker.HitBoxY = attacker.PosY + attacker.CollisionOffsetY;
                attacker.HitBoxZ = attacker.PosZ + attacker.CollisionOffsetZ;
            }

            for (var t = 0; t < count; t++)
            {
                if (t == a)
                {
                    continue;
                }

                var target = list[t];
                if (target.FrameCollisionTickCounter != 0
                    || target.DamagedTickCounter != 0
                    || (target.HitAnimByte & InvulnerableAnimationBit) != 0
                    || (target.Flags & mask) == 0)
                {
                    continue;
                }

                int bodyX, bodyY, bodyZ;
                unchecked
                {
                    bodyX = target.PosX + target.ModX;
                    bodyY = target.PosY + target.ModY;
                    bodyZ = target.PosZ + target.ModZ;
                }

                if (!AxisOverlaps(attacker.HitBoxX, attacker.CollisionWidth, bodyX, target.Width)
                    || !AxisOverlaps(attacker.HitBoxY, attacker.CollisionDepth, bodyY, target.Height)
                    || !AxisOverlaps(attacker.HitBoxZ, attacker.CollisionHeight, bodyZ, target.Depth))
                {
                    continue;
                }

                var response = target.BalanceRecord?.ByteAt((attack.Attribute & 0xF) + 2) ?? 0;
                if ((response & 0xC0) != 0x80)
                {
                    // Not immune. The binary attaches the effect 4 (attack nibbles 6 and 10) or 5 (7 and 9) to the target here (0x800395E8-0x80039668), BEFORE the store of TouchingEntity and before the sparks;
                    // no random word is drawn. The world gives the callback that puts the effect on its stage (E20.f3b phase B); without one the pass behaves as before.
                    if (attachEffect != null)
                    {
                        var effect = EffectOfNibble(attack.Attribute & 0xF);
                        if (effect != 0)
                        {
                            attachEffect(target, effect);
                        }
                    }

                    target.TouchingEntity = attacker.SelfEntity;
                }

                target.FrameCollisionTickCounter = HitCooldownTicks;
                attacker.HitCounter++;
                HitsWritten++;

                if (emitSparks != null)
                {
                    emitSparks(
                        AxisCentre(attacker.HitBoxX, attacker.CollisionWidth, bodyX, target.Width),
                        AxisCentre(attacker.HitBoxY, attacker.CollisionDepth, bodyY, target.Height),
                        AxisCentre(attacker.HitBoxZ, attacker.CollisionHeight, bodyZ, target.Depth));
                }
            }
        }
    }
}
