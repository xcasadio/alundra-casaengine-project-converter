#nullable enable
using System;
using Alundra.Maps;
using Alundra.Events;
using Alundra.Rng;
using Alundra.Entities;

namespace Alundra.Ai.Enemies;

/// <summary>
/// E14.a1c T1 (docs/plan-e14-ia-native.md section 1.3c, D-E14-1): the native handlers of the Muruta (sprite type 347) - A0 (<c>0x8006174C</c>, slot A, code 0), C10 (<c>0x80068154</c>, slot C, code 10) and D9
/// (<c>0x8007DE04</c>, slot D, code 9) of the master table <c>0x80098F4C</c> - registered in the registry of the native sprite handlers (<see cref="NativeSpriteRegistry"/>) for the type 347 only. E1 (its death) is
/// <c>DestroyEntity(e, -1)</c> of the host (E14.a1b). They are ported from the real code of <c>ALUN_CD.EXE</c> (France) run in the emulator of the E14.a1 oracle (<c>docs/plan-e14-annexe/e14a/oracle-a1</c>: 4,043 single-call vectors
/// of C10, 720 of D9, 6 of A0, and the calls of the scenarios MU-*) and compared call by call (<c>AlundraMurutaOracleTests</c>). The fields they read are tied to the DLL's in the table of the plan (section 1.3c): the tile, floor
/// and corner heights of the entity and of the hero, the direction of the TICK (<see cref="EntityScriptProxy.BoxDirection"/>, D-E14-6), the hero's frame box (the FIELD <see cref="EntityScriptProxy.HasFrameBox"/>, never
/// re-evaluated) and current attack, the generator's word (<see cref="GameRandom"/>) and the sound player.
/// <para>
/// Not ported: the branch of C10 for the map <c>0x99</c> (the BSS word <c>[0x800DC5A0]</c> read at <c>0x80068164</c>, then the parent entity at <c>+0x0C</c>, <c>0x80068178</c>-<c>0x8006819C</c>): no record of the map 153 places a 347, the branch is
/// dead for this sprite. Without a hero (<see cref="IEntityWorldContext.PlayerEntity"/> null) C10 does nothing and D9 only applies the damage and the animation (as <see cref="EnemyHelpers.HitReaction"/>): the binary always has a hero
/// and the oracle has none of these cases (documented limit). C10 never writes a position.
/// </para>
/// </summary>
internal static class MurutaAi
{
    /// <summary>The sprite type of the Muruta (<see cref="EntityScriptProxy.SpriteType"/>).</summary>
    internal const int SpriteType = 347;

    /// <summary>The program code of A0 in slot A.</summary>
    internal const int LoadCode = 0;

    /// <summary>The program code of C10 in slot C.</summary>
    internal const int TickCode = 10;

    /// <summary>The program code of D9 in slot D.</summary>
    internal const int TouchCode = 9;

    /// <summary>The sound the Muruta plays one time in four when it starts to hop or to claw (<c>0xCD</c>).</summary>
    internal const int ClawSound = 0xCD;

    /// <summary>The window of <c>TryAttackPlayerFront</c> and its back counterpart: depth in tiles, height difference (16.16 units).</summary>
    private const int ZMax = 0x100000;

    /// <summary>Registers A0, C10 and D9 for the type 347.</summary>
    internal static void Register(NativeSpriteRegistry registry)
    {
        var types = new[] { SpriteType };
        registry.Register(ScriptHelper.ProgramALoad, LoadCode, types, (entity, context, state) => Load(entity));
        registry.Register(ScriptHelper.ProgramCTick, TickCode, types, (entity, context, state) => Tick(entity, context));
        registry.Register(ScriptHelper.ProgramDTouch, TouchCode, types, (entity, context, state) => Hit(entity, context));
    }

    /// <summary>
    /// A0 (<c>0x8006174C</c>): <c>AIValues[0]</c> (<c>+0x280</c>, <c>0x80061754</c>) takes the high half-word of the entity's Z (<c>lh +0x11E</c>, signed). The DLL's <see cref="EntityScriptProxy.PosZ"/> is the binary's minus
    /// one raw unit (ADR-0026), so the binary's Z is <c>PosZ + 1</c>.
    /// </summary>
    internal static void Load(EntityScriptProxy entity) => entity.AIValues[0] = unchecked((short)(unchecked(entity.PosZ + 1) >> 16));

    /// <summary>D9 (<c>0x8007DE04</c>): <see cref="EnemyHelpers.HitReaction"/> with the animation 2.</summary>
    internal static void Hit(EntityScriptProxy entity, IEntityWorldContext context) => EnemyHelpers.HitReaction(entity, 2, context);

    /// <summary>
    /// C10 (<c>0x80068154</c>), one tick of the Muruta, by its target animation (<c>+0x88</c>; only 0, 5, 9, 10 and 12 have code, the others only run the tail). <c>rel</c> is the relative position to the hero
    /// (<see cref="EnemyHelpers.RelativePosition"/>), <c>ai1</c> the 16-bit counter <c>+0x282</c> (<see cref="EntityScriptProxy.AIValues"/>[1]), <c>b0</c> (<c>+0x274</c>) 0 calm / 1 alerted, <c>b1</c> (<c>+0x275</c>) the calm step.
    /// Every path ends in the tail: <c>TargetDirection = (TargetDirection + 2) &amp; 0x1C</c> (<c>0x80068904</c>).
    /// <list type="bullet">
    /// <item><description>0 calm: the hero in the front window (2 tiles wide, 6 deep) alerts it; else the counter counts down; else, with <c>b1</c> 0 only one time in four (a draw below 4 equal to 1), it plays the step <c>b1</c>: 0 to 2 hop
    /// toward the hero (animation 8), 3 a random cardinal direction with a stop of 0x14, 4 a quarter turn with a stop of 0x14, 5 walk (animation 0xC) 0x78, then reset.</description></item>
    /// <item><description>0 alerted: out of the front window and of the back one (<c>BoxDirection ^ 1</c>, 2 by 3) it calms down; the counter counts down; it faces the hero; a hero who strikes (frame box, attack with a non-zero attribute) within 5 tiles
    /// is answered by a lunge (animation 3); else by the distance (3, 5, 7 tiles) a draw picks walk, hop or claw.</description></item>
    /// <item><description>5 (hurt): at the end of the animation, dying (<c>b3</c> set) it takes the animation 6 and the flag <c>0x40</c>, else it is alerted again for 0x1E ticks. 9 and 10: on the ground, animation 0 for 0x14 ticks.
    /// 12 (walk): the front window alerts it; the counter or a wall stops it; a ledge (<see cref="EnemyHelpers.HandleAnimationDirection"/>) turns it.</description></item>
    /// </list>
    /// </summary>
    internal static void Tick(EntityScriptProxy entity, IEntityWorldContext context)
    {
        if (context.PlayerEntity is not { } hero)
        {
            return;
        }

        Span<int> rel = stackalloc int[6];
        EnemyHelpers.RelativePosition(entity, hero, rel);

        switch (entity.TargetAnimationId)
        {
            case 0:
                Idle(entity, context, hero, rel);
                return;

            case 5:
                if (entity.ForceResetAnimationFlag != 0)
                {
                    if (entity.Bytes[3] == 0)
                    {
                        entity.Bytes[0] = 1;
                        entity.TargetAnimationId = 0;
                        SetCounter(entity, 0x1E);
                        entity.Bytes[1] = 0;
                    }
                    else
                    {
                        entity.TargetAnimationId = 6;
                        entity.Flags |= 0x40;
                    }
                }

                break;

            case 9:
            case 10:
                if (entity.IsOnGround != 0)
                {
                    entity.TargetAnimationId = 0;
                    SetCounter(entity, 0x14);
                }

                break;

            case 12:
                Walk(entity, rel);
                return;
        }

        Tail(entity);
    }

    private static void Idle(EntityScriptProxy entity, IEntityWorldContext context, EntityScriptProxy hero, ReadOnlySpan<int> rel)
    {
        if (entity.Bytes[0] == 0)
        {
            Calm(entity, context, hero, rel);
        }
        else
        {
            Alerted(entity, context, hero, rel);
        }

        Tail(entity);
    }

    private static void Calm(EntityScriptProxy entity, IEntityWorldContext context, EntityScriptProxy hero, ReadOnlySpan<int> rel)
    {
        if (EnemyHelpers.TryAttackPlayerFront(entity, rel, 2, 6, ZMax))
        {
            entity.Bytes[0] = 1;
            SetCounter(entity, 0);
            return;
        }

        var counter = unchecked((ushort)entity.AIValues[1]);
        if (counter != 0)
        {
            SetCounter(entity, unchecked((ushort)(counter - 1)));
            return;
        }

        if (entity.Bytes[1] == 0 && EnemyHelpers.Below(4) != 1)
        {
            return;
        }

        var step = entity.Bytes[1];
        entity.Bytes[1] = unchecked((byte)(step + 1));
        switch (step)
        {
            case 0:
            case 1:
            case 2:
                if (EnemyHelpers.Below(4) == 0)
                {
                    context.SoundPlayer?.PlaySfx(ClawSound);
                }

                entity.TargetAnimationId = 8;
                entity.TargetDirection = DirectionToHero(entity, hero);
                break;

            case 3:
            {
                var direction = EnemyHelpers.CardinalDirections[EnemyHelpers.Below(4)];
                SetCounter(entity, 0x14);
                EnemyHelpers.ZeroForces(entity);
                entity.TargetDirection = direction;
                break;
            }

            case 4:
                SetCounter(entity, 0x14);
                EnemyHelpers.ZeroForces(entity);
                entity.TargetDirection = EnemyHelpers.QuarterTurnTable[(int)(entity.TargetDirection & 0x1F)];
                break;

            case 5:
                entity.TargetAnimationId = 0xC;
                SetCounter(entity, 0x78);
                break;

            default:
                entity.Bytes[1] = 0;
                SetCounter(entity, 0);
                break;
        }
    }

    private static void Alerted(EntityScriptProxy entity, IEntityWorldContext context, EntityScriptProxy hero, ReadOnlySpan<int> rel)
    {
        if (!EnemyHelpers.TryAttackPlayerFront(entity, rel, 2, 6, ZMax) && !EnemyHelpers.TryAttackPlayer2(rel, entity.BoxDirection ^ 1, 2, 3, ZMax))
        {
            entity.Bytes[1] = 0;
            entity.Bytes[0] = 0;
            SetCounter(entity, 0x1E);
            return;
        }

        var counter = unchecked((ushort)entity.AIValues[1]);
        if (counter != 0)
        {
            SetCounter(entity, unchecked((ushort)(counter - 1)));
            return;
        }

        entity.TargetDirection = DirectionToHero(entity, hero);

        // 0x80068474 (frame box), 0x80068484 (current attack), lbu 0x80068494 (its attribute); the box is the FIELD of the tick.
        if (hero.HasFrameBox && hero.CurrentAttack is { Attribute: not 0 } && rel[0] < 5 && rel[1] < 5)
        {
            if (EnemyHelpers.Below(4) == 0)
            {
                context.SoundPlayer?.PlaySfx(ClawSound);
            }

            entity.TargetAnimationId = 3;
            entity.TargetDirection = DirectionToHero(entity, hero);
            SetCounter(entity, 0x1E);
            entity.Bytes[1] = 0;
            return;
        }

        var away = (entity.TargetDirection - 0x10) & 0x1F;
        if (rel[0] < 3 && rel[1] < 3)
        {
            switch (EnemyHelpers.Below(8))
            {
                case 0:
                    entity.TargetAnimationId = 0xC;
                    SetCounter(entity, 0x14);
                    entity.TargetDirection = DirectionToHero(entity, hero);
                    break;

                case 1:
                    entity.TargetAnimationId = 0xC;
                    SetCounter(entity, 0x3C);
                    entity.TargetDirection = away;
                    break;

                case 2:
                case 3:
                    entity.TargetAnimationId = 0xC;
                    SetCounter(entity, 100);
                    entity.TargetDirection = away;
                    break;

                case 4:
                case 5:
                    if (EnemyHelpers.Below(4) == 0)
                    {
                        context.SoundPlayer?.PlaySfx(ClawSound);
                    }

                    entity.TargetAnimationId = 7;
                    break;

                default:
                    entity.TargetAnimationId = 4;
                    entity.TargetDirection = away;
                    break;
            }
        }
        else if (rel[0] < 5 && rel[1] < 5)
        {
            if (EnemyHelpers.Below(5) <= 1)
            {
                entity.TargetAnimationId = 8;
            }
            else
            {
                entity.TargetAnimationId = 0xC;
                SetCounter(entity, 0x1E);
            }
        }
        else if (rel[0] < 7 && rel[1] < 7)
        {
            switch (EnemyHelpers.Below(8))
            {
                case 0:
                case 1:
                case 2:
                    entity.TargetAnimationId = 0xC;
                    SetCounter(entity, 0x3C);
                    break;

                case 3:
                    entity.TargetAnimationId = 0xC;
                    SetCounter(entity, 0x1E);
                    break;

                case 4:
                case 5:
                    entity.TargetAnimationId = 4;
                    break;

                default:
                    entity.TargetAnimationId = 8;
                    break;
            }
        }
    }

    /// <summary>State 12 (<c>0x80068800</c>), the walk.</summary>
    private static void Walk(EntityScriptProxy entity, ReadOnlySpan<int> rel)
    {
        if (entity.Bytes[0] == 0 && EnemyHelpers.TryAttackPlayerFront(entity, rel, 2, 6, ZMax))
        {
            entity.Bytes[0] = 1; // 0x800688D4
            entity.TargetAnimationId = 0;
            CooldownExit(entity);
            return;
        }

        var counter = unchecked((ushort)(entity.AIValues[1] - 1));
        SetCounter(entity, counter);
        if (counter == 0 || entity.ForceAdjusted != 0)
        {
            entity.TargetAnimationId = 0;
            EnemyHelpers.ZeroForces(entity);
            CooldownExit(entity);
            return;
        }

        if (EnemyHelpers.HandleAnimationDirection(entity, 0xC, 0x400000))
        {
            entity.TargetAnimationId = 0;
            CooldownExit(entity);
            return;
        }

        Tail(entity);
    }

    /// <summary><c>0x800688E0</c>: the cooldown of 0x1E ticks, the calm step cleared, then the tail.</summary>
    private static void CooldownExit(EntityScriptProxy entity)
    {
        SetCounter(entity, 0x1E);
        entity.Bytes[1] = 0;
        Tail(entity);
    }

    /// <summary><c>0x80068904</c>: every path ends by snapping the target direction to a multiple of four after a half step.</summary>
    private static void Tail(EntityScriptProxy entity) => entity.TargetDirection = (entity.TargetDirection + 2) & 0x1C;

    private static void SetCounter(EntityScriptProxy entity, int value) => entity.AIValues[1] = unchecked((short)value);

    private static uint DirectionToHero(EntityScriptProxy entity, EntityScriptProxy hero)
        => ScriptHelper.GetDirectionToTarget(unchecked(hero.PosX - entity.PosX), unchecked(hero.PosY - entity.PosY));
}
