#nullable enable
using System;
using System.Diagnostics;
using Alundra.Maps;
using Alundra.Events;
using Alundra.Ai.Enemies;
using Alundra.Scripts;
using Alundra.Combat;
using Alundra.Rng;

namespace Alundra.Ai;

/// <summary>
/// E14.a0b T3 (docs/plan-e14-ia-native.md section 1.2): the helpers the native handlers of the enemies share (Beannoid C1, Muruta C10, and later E14.a1's), ported from <c>ALUN_CD.EXE</c> (France) and compared, state by state, with the real code run
/// in the emulator on 10,000 random states each (<c>docs/plan-e14-annexe/e14a/oracle</c>, tests <c>AlundraEnemyHelpersTests</c>). E14.a1b registers the Beannoid handlers that use them (<see cref="BeannoideAi"/>, <see cref="NativeSpriteRegistry"/>); the Muruta's come with E14.a1c. Every field a helper reads is tied to its DLL field in the table of the plan (section 1.2, "Entrées des aides"): the fields that carry a direction or an animation take the value of the TICK
/// (<see cref="AlundraEntityScriptProxy.BoxDirection"/>, never the end-of-frame <see cref="AlundraEntityScriptProxy.AnimationDirection"/>, D-E14-6); all heights are in the DLL's convention (the binary's minus 1: only differences are read).
/// </summary>
internal static class EnemyHelpers
{
    /// <summary>The quarter-turn table at <c>0x80028B34</c> (32 bytes, FACT: <c>18, 00 x8, 08 x8, 10 x8, 18 x7</c>), indexed by a packed direction (0..31): the cardinal direction a turn snaps to. The decompilation's table of the same name
    /// (<c>StaticVariables.cs:543-547</c>) differs at indices 14-16, 19-23 and 25-27 and is wrong; <see cref="ScriptHelper"/>'s <c>DirectionTable</c> is the octant table at <c>0x80022488</c>, not this one.</summary>
    internal static readonly byte[] QuarterTurnTable =
    {
        0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
        0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18,
    };

    /// <summary>The cardinal directions at <c>0x80028B54</c> (FACT: <c>00 10 08 18</c>), indexed by a draw below 4 (the Muruta's random turn, C10 step 3).</summary>
    internal static readonly byte[] CardinalDirections = { 0x00, 0x10, 0x08, 0x18 };

    /// <summary>The generator's draw below <paramref name="n"/>: <c>((ulong)(uint)Next() * n) &gt;&gt; 32</c> (the inline pattern <c>multu / mfhi</c> of the binary). <see cref="AlundraRandom.Next"/> keeps 64 bits of state, so the
    /// product is taken on its low 32 bits, as the binary's register holds them.</summary>
    internal static int Below(int n) => (int)(((ulong)(uint)AlundraRandom.Next() * (uint)n) >> 32);

    /// <summary>
    /// <c>0x8007FE20</c>, the relative position of <paramref name="self"/> to the hero: <paramref name="output"/> = [|dx|, |dy|, |dz|, dx, dy, dz] with <c>dx = self.TileX - hero.TileX</c> (<c>lw +0x120</c> at <c>0x8007FE28</c> and <c>0x8007FE2C</c>),
    /// <c>dy</c> likewise on <c>TileY</c> (<c>+0x124</c>, <c>0x8007FE30</c>/<c>0x8007FE34</c>) and <c>dz = self.FloorHeight - hero.FloorHeight</c> (<c>+0x134</c>, <c>0x8007FE38</c>/<c>0x8007FE3C</c>); the absolute values wrap as <c>negu</c> does.
    /// </summary>
    internal static void RelativePosition(AlundraEntityScriptProxy self, AlundraEntityScriptProxy hero, Span<int> output)
        => Fill(self.TileX - hero.TileX, self.TileY - hero.TileY, self.FloorHeight - hero.FloorHeight, output);

    /// <summary><c>0x8007FE8C</c>: the same, but the Z of <paramref name="self"/> is its <see cref="AlundraEntityScriptProxy.PosZ"/> (<c>+0x11C</c>, <c>0x8007FEA4</c>) and not its floor; the hero's is his floor (<c>+0x134</c>, <c>0x8007FEA8</c>).</summary>
    internal static void RelativePositionFromPosZ(AlundraEntityScriptProxy self, AlundraEntityScriptProxy hero, Span<int> output)
        => Fill(self.TileX - hero.TileX, self.TileY - hero.TileY, self.PosZ - hero.FloorHeight, output);

    private static void Fill(int dx, int dy, int dz, Span<int> output)
    {
        output[0] = Abs(dx);
        output[1] = Abs(dy);
        output[2] = Abs(dz);
        output[3] = dx;
        output[4] = dy;
        output[5] = dz;
    }

    private static int Abs(int v) => v >= 0 ? v : unchecked(-v);

    /// <summary>
    /// <c>0x8008048C</c>, TryAttackPlayer2 (<c>a0</c> unused): 0 when <paramref name="d3"/> is below |dz| (<c>rel[2]</c>); then by <paramref name="direction"/> (the low byte): 0 needs <paramref name="a3"/> &gt;= |dx|, dy &lt;= 0 and <paramref name="d2"/> &gt;= |dy|;
    /// 1 needs <paramref name="a3"/> &gt;= |dx|, dy &gt;= 0 and <paramref name="d2"/> &gt;= |dy|; 2 needs <paramref name="a3"/> &gt;= |dy|, dx &gt;= 0 and <paramref name="d2"/> &gt;= |dx|; 3 needs <paramref name="a3"/> &gt;= |dy|, dx &lt;= 0 and
    /// <paramref name="d2"/> &gt;= |dx|; any other direction fails. <paramref name="rel"/> is the six words of <see cref="RelativePosition"/> (at <c>0x80080498</c>-<c>0x80080580</c>, all comparisons signed).
    /// </summary>
    internal static bool TryAttackPlayer2(ReadOnlySpan<int> rel, int direction, int a3, int d2, int d3) => TryAttack(rel, direction & 0xFF, a3, d2, d3);

    /// <summary>
    /// <c>0x800805C8</c>, TryAttackPlayerFront: the same window with the direction of the entity, the WORD <c>+0x98</c> (<c>lw 0x98</c> at <c>0x800805E0</c>), which is the value of the TICK: <see cref="AlundraEntityScriptProxy.BoxDirection"/>
    /// (D-E14-6; never <see cref="AlundraEntityScriptProxy.AnimationDirection"/>, which is the value of the end of the frame), with <paramref name="a2"/> and <paramref name="a3"/> in the places of <see cref="TryAttackPlayer2"/>'s <c>a3</c> and <c>d2</c>.
    /// </summary>
    internal static bool TryAttackPlayerFront(AlundraEntityScriptProxy self, ReadOnlySpan<int> rel, int a2, int a3, int d3) => TryAttack(rel, self.BoxDirection, a2, a3, d3);

    private static bool TryAttack(ReadOnlySpan<int> rel, int direction, int a, int b, int d3)
    {
        if (d3 < rel[2])
        {
            return false;
        }

        switch (direction)
        {
            case 0:
                return a >= rel[0] && rel[4] <= 0 && b >= rel[1];
            case 1:
                return a >= rel[0] && rel[4] >= 0 && b >= rel[1];
            case 2:
                return a >= rel[1] && rel[3] >= 0 && b >= rel[0];
            case 3:
                return a >= rel[1] && rel[3] <= 0 && b >= rel[0];
            default:
                return false;
        }
    }

    /// <summary>
    /// <c>0x80080144</c>, HandleAnimationDirection: with <c>thr = dist + 1</c> and <c>t[k] = FloorHeight - MapHeights[k]</c> (<c>+0x134</c> at <c>0x8008014C</c>, <c>+0x158</c>-<c>+0x164</c> at <c>0x80080150</c>-<c>0x80080164</c>), the four pairs of
    /// neighbouring corners that are all higher than the threshold turn the entity (the decision tree of <c>0x80080170-0x80080258</c>, read against <see cref="AlundraEntityScriptProxy.TargetDirection"/>, <c>+0x8C</c>, <c>0x80080184</c>, <c>0x800801B0</c>,
    /// <c>0x800801DC</c>, <c>0x80080208</c>): it then takes the animation <paramref name="animation"/> (<c>+0x88</c>, <c>0x80080230</c>), zeroes the forces <c>+0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8</c> (<c>TargetForceX/Y</c>, <c>ForceX/Y</c>, <c>ForceStepX/Y</c>),
    /// and takes the quarter-turn direction <see cref="QuarterTurnTable"/> of its target direction (<c>+0x8C</c>, <c>0x8008024C</c>). Returns whether it turned. The target direction is a packed direction (0..31).
    /// </summary>
    internal static bool HandleAnimationDirection(AlundraEntityScriptProxy self, uint animation, int dist)
    {
        var threshold = unchecked(dist + 1);
        var floor = self.FloorHeight;
        var t0 = unchecked(floor - self.MapHeights[0]);
        var t1 = unchecked(floor - self.MapHeights[1]);
        var t2 = unchecked(floor - self.MapHeights[2]);
        var t3 = unchecked(floor - self.MapHeights[3]);
        var a = threshold < t0;
        var b = threshold < t1;
        var d = threshold < t2;
        var c = threshold < t3;
        var direction = (int)self.TargetDirection;

        // 0x80080170-0x80080258, label by label ('19c' = 0x8008019C, '1c8', '1cc', '1f4', '1f8')
        var state = 0x19C;
        if (a)
        {
            if (b)
            {
                if (InRange(direction, 9, 15))
                {
                    return Turn(self, animation, direction);
                }
            }
            else
            {
                state = 0x1CC;
            }
        }

        if (state == 0x19C)
        {
            if (!b)
            {
                state = 0x1CC;
            }
            else if (!c)
            {
                state = 0x1F8;
            }
            else
            {
                if (InRange(direction, 0x11, 15))
                {
                    return Turn(self, animation, direction);
                }

                state = 0x1CC;
            }
        }

        if (state == 0x1CC)
        {
            if (c)
            {
                if (!d)
                {
                    return false;
                }

                if (!InRange(direction, 8, 0x11))
                {
                    return Turn(self, animation, direction);
                }
            }
        }

        // 0x800801F8
        if (!d || !a)
        {
            return false;
        }

        return direction < 0x10 && Turn(self, animation, direction);
    }

    /// <summary><c>(direction - lo) &lt;u count</c>: the <c>addiu / sltiu</c> pair.</summary>
    private static bool InRange(int direction, int lo, int count) => unchecked((uint)(direction - lo)) < (uint)count;

    private static bool Turn(AlundraEntityScriptProxy self, uint animation, int direction)
    {
        Debug.Assert((uint)direction < (uint)QuarterTurnTable.Length, "a packed direction is 0..31");
        self.TargetAnimationId = animation & 0xFF;
        ZeroForces(self);
        self.TargetDirection = QuarterTurnTable[direction & 0x1F];
        return true;
    }

    /// <summary>The six words every "stop" of the binary's enemies clears: <c>+0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8</c> (<c>TargetForceX/Y</c>, <c>ForceX/Y</c>, <c>ForceStepX/Y</c>), as <see cref="HandleAnimationDirection"/> and C1's walk stop write them.</summary>
    internal static void ZeroForces(AlundraEntityScriptProxy self)
    {
        self.TargetForceX = 0;
        self.TargetForceY = 0;
        self.ForceX = 0;
        self.ForceY = 0;
        self.ForceStepX = 0;
        self.ForceStepY = 0;
    }

    /// <summary>
    /// <c>0x8007FEF8</c>, StartWalk (<paramref name="self"/>, animation <paramref name="animation"/>, <paramref name="range"/>, <paramref name="chance"/>): one draw below 0x65 (the random word <c>0x80098708</c>, <see cref="AlundraRandom"/>); when it is below
    /// <c>chance &amp; 0xFF</c> the entity aims at the hero (<see cref="ScriptHelper.GetDirectionToTarget"/> of hero minus entity, <c>0x8007FF6C-0x8007FF84</c> on <c>+0x114</c> and <c>+0x118</c>, i.e. <see cref="AlundraEntityScriptProxy.PosX"/> and
    /// <see cref="AlundraEntityScriptProxy.PosY"/>), else a second draw below 0x20 is its direction; <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> (<c>+0x88</c>) takes <c>animation &amp; 0xFF</c>; when the low 16 bits of
    /// <paramref name="range"/> are not zero, a draw below 16 plus <paramref name="range"/> is stored as 16 bits at <c>+0x282</c> (<see cref="AlundraEntityScriptProxy.AIValues"/>[1], <c>0x80080010</c>).
    /// </summary>
    internal static void StartWalk(AlundraEntityScriptProxy self, AlundraEntityScriptProxy hero, uint animation, int range, int chance)
    {
        if (Below(0x65) < (chance & 0xFF))
        {
            self.TargetDirection = ScriptHelper.GetDirectionToTarget(unchecked(hero.PosX - self.PosX), unchecked(hero.PosY - self.PosY));
        }
        else
        {
            self.TargetDirection = (uint)Below(0x20);
        }

        self.TargetAnimationId = animation & 0xFF;
        if ((short)range != 0)
        {
            self.AIValues[1] = unchecked((short)(Below(16) + range));
        }
    }

    /// <summary>
    /// The skeleton of the hit handlers D1 (<c>0x8007D9A4</c>, Beannoid, animation 5) and D9 (<c>0x8007DE04</c>, Muruta, animation 2): <see cref="AlundraDamage.ComputeNewHp"/> of the entity (its touching entity's attack, its balance record, the equipment and
    /// the hero's animation latch of <paramref name="context"/>), <see cref="AlundraEntityScriptProxy.Bytes"/>[3] = 1 when it returned true (<c>sb 0x277</c>, <c>0x8007D9C0</c>), the animation <paramref name="animation"/> (<c>+0x88</c>, <c>0x8007D9C8</c>), and the
    /// direction away from the hero (<see cref="ScriptHelper.GetDirectionToTarget"/> of entity minus hero, <c>+0x114</c>/<c>+0x118</c> of both, <c>0x8007D9D4-0x8007D9E4</c>; written at <c>0x8007D9F0</c>). Without a hero in <paramref name="context"/> the direction is left alone.
    /// </summary>
    internal static void HitReaction(AlundraEntityScriptProxy self, uint animation, IEntityWorldContext context)
    {
        if (AlundraDamage.ComputeNewHp(self, self.BalanceRecord, context.EquipmentRecords, context.HeroAnimationLatch))
        {
            self.Bytes[3] = 1;
        }

        self.TargetAnimationId = animation;
        if (context.PlayerEntity is { } hero)
        {
            self.TargetDirection = ScriptHelper.GetDirectionToTarget(unchecked(self.PosX - hero.PosX), unchecked(self.PosY - hero.PosY));
        }
    }
}
