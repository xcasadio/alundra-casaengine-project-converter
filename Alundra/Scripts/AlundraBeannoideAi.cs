#nullable enable
using System;
using Alundra.Maps;
using Alundra.Events;

namespace Alundra.Scripts;

/// <summary>
/// E14.a1b T4 (docs/plan-e14-ia-native.md section 1.3b, D-E14-1): the native handlers of the Beannoid (sprite type 335) - A1 (<c>0x80061758</c>, slot A, code 1), C1 (<c>0x80065ED4</c>, slot C, code 1) and D1
/// (<c>0x8007D9A4</c>, slot D, code 1) of the master table <c>0x80098F4C</c> - registered in the registry of the native sprite handlers (<see cref="AlundraNativeSpriteRegistry"/>) for the type 335 only. E1 (its death) is
/// not here: it is <c>DestroyEntity(e, -1)</c> of the host (E0/E1 stay outside the registry). They are ported from the real code of <c>ALUN_CD.EXE</c> (France) run in the emulator of the E14.a1 oracle
/// (<c>docs/plan-e14-annexe/e14a/oracle-a1</c>: 1,185 single-call vectors of C1, 720 of D1, 3 of A1, and the calls of 21 scenarios) and compared call by call (<c>AlundraBeannoideOracleTests</c>). The fields they read are
/// tied to the DLL's in the table of the plan (section 1.3b): the position, floor and tile of the entity and of the hero, the animation flags of the TICK, the generator's word (<see cref="AlundraRandom"/>).
/// <para>
/// Without a hero (<see cref="IEntityWorldContext.PlayerEntity"/> null) C1 does nothing and D1 only applies the damage and the animation (as <see cref="AlundraEnemyHelpers.HitReaction"/>): the binary always has a hero and the
/// oracle has none of these cases (documented limit).
/// </para>
/// </summary>
internal static class AlundraBeannoideAi
{
    /// <summary>The sprite type of the Beannoid (<see cref="AlundraEntityScriptProxy.SpriteType"/>).</summary>
    internal const int SpriteType = 335;

    /// <summary>The program code of A1, C1 and D1 in their slot.</summary>
    internal const int Code = 1;

    /// <summary>Registers A1, C1 and D1 for the type 335.</summary>
    internal static void Register(AlundraNativeSpriteRegistry registry)
    {
        var types = new[] { SpriteType };
        registry.Register(ScriptHelper.ProgramALoad, Code, types, (entity, context, state) => Load(entity));
        registry.Register(ScriptHelper.ProgramCTick, Code, types, (entity, context, state) => Tick(entity, context));
        registry.Register(ScriptHelper.ProgramDTouch, Code, types, (entity, context, state) => Hit(entity, context));
    }

    /// <summary>A1 (<c>0x80061758</c>): the target animation 2 (dormant), written at <c>0x80061760</c>.</summary>
    internal static void Load(AlundraEntityScriptProxy entity) => entity.TargetAnimationId = 2;

    /// <summary>D1 (<c>0x8007D9A4</c>): <see cref="AlundraEnemyHelpers.HitReaction"/> with the animation 5.</summary>
    internal static void Hit(AlundraEntityScriptProxy entity, IEntityWorldContext context) => AlundraEnemyHelpers.HitReaction(entity, 5, context);

    /// <summary>
    /// C1 (<c>0x80065ED4</c>), one tick of the Beannoid, by its target animation (<c>+0x88</c>, an unsigned byte-like value: 20 and above do nothing). With <c>rel = |dTileX|, |dTileY|, |dFloor|</c> to the hero
    /// (<see cref="AlundraEnemyHelpers.RelativePosition"/>) and <c>ai1</c> the 16-bit counter <c>+0x282</c> (<see cref="AlundraEntityScriptProxy.AIValues"/>[1]):
    /// <list type="bullet">
    /// <item><description>0 (idle): the counter counts down to 0; then, within 3 tiles and 32 height units of the hero and with <c>+0x275</c> clear, it turns to the hero, takes the animation 3 (lunge) and sets <c>+0x275</c>;
    /// else it takes the animation 1 (walk) for <c>0x3C + Below(0x3D)</c> ticks, turning to the hero when within 4 tiles, and clears <c>+0x275</c>.</description></item>
    /// <item><description>1 (walk): the counter counts down; at 0, off the ground-adjust flag it goes back to the animation 0 with a random direction (<c>Below(0x2000)</c>: counter <c>r &amp; 0x7F</c>, direction <c>r &gt;&gt; 8</c>), else it snaps to a
    /// quarter turn, stops its forces and walks <c>0x1E + Below(0x1F)</c> ticks.</description></item>
    /// <item><description>2 (dormant): within 4 tiles of the hero it wakes (animation 4). 3 (lunge): at the end of the animation, <see cref="AlundraEnemyHelpers.StartWalk"/>(1, 0x28, 0x14) and <c>+0x275</c> clear. 4: at the end,
    /// animation 0 for 0x1E ticks. 7 (hurt): at the end, dying (<c>+0x277</c> set) it takes the animation 6 and the flag <c>0x40</c>, else animation 0 for 0x28 ticks. 8 to 14: on the ground, animation 2. 19: the counter counts down,
    /// at 0 animation 0 for 0x1E ticks.</description></item>
    /// </list>
    /// </summary>
    internal static void Tick(AlundraEntityScriptProxy entity, IEntityWorldContext context)
    {
        if (context.PlayerEntity is not { } hero)
        {
            return;
        }

        var animation = entity.TargetAnimationId;
        if (animation >= 20)
        {
            return;
        }

        Span<int> rel = stackalloc int[6];
        AlundraEnemyHelpers.RelativePosition(entity, hero, rel);
        var near3 = rel[0] < 3 && rel[1] < 3;
        var near4 = rel[0] < 4 && rel[1] < 4;
        var counter = unchecked((ushort)entity.AIValues[1]);

        switch (animation)
        {
            case 0:
                if (counter != 0)
                {
                    counter = unchecked((ushort)(counter - 1));
                    SetCounter(entity, counter);
                    if (counter != 0)
                    {
                        return;
                    }
                }

                if (near3 && rel[2] <= 0x200000 && entity.Bytes[1] == 0)
                {
                    entity.TargetDirection = DirectionToHero(entity, hero);
                    entity.TargetAnimationId = 3;
                    entity.Bytes[1] = 1;
                    return;
                }

                entity.TargetAnimationId = 1;
                SetCounter(entity, 0x3C + AlundraEnemyHelpers.Below(0x3D));
                if (near4)
                {
                    entity.TargetDirection = DirectionToHero(entity, hero);
                }

                entity.Bytes[1] = 0;
                break;

            case 1:
                counter = unchecked((ushort)(counter - 1));
                SetCounter(entity, counter);
                if (counter != 0)
                {
                    return;
                }

                if (entity.ForceAdjusted == 0)
                {
                    var r = AlundraEnemyHelpers.Below(0x2000);
                    entity.TargetAnimationId = 0;
                    SetCounter(entity, r & 0x7F);
                    entity.TargetDirection = (uint)(r >> 8);
                }
                else
                {
                    var r = AlundraEnemyHelpers.Below(0x1F);
                    entity.TargetDirection = AlundraEnemyHelpers.QuarterTurnTable[(int)(entity.TargetDirection & 0x1F)];
                    AlundraEnemyHelpers.ZeroForces(entity);
                    SetCounter(entity, 0x1E + r);
                }

                break;

            case 2:
                if (near4)
                {
                    entity.TargetAnimationId = 4;
                }

                break;

            case 3:
                if (entity.ForceResetAnimationFlag != 0)
                {
                    AlundraEnemyHelpers.StartWalk(entity, hero, 1, 0x28, 0x14);
                    entity.Bytes[1] = 0;
                }

                break;

            case 4:
                if (entity.ForceResetAnimationFlag != 0)
                {
                    entity.TargetAnimationId = 0;
                    SetCounter(entity, 0x1E);
                }

                break;

            case 7:
                if (entity.ForceResetAnimationFlag != 0)
                {
                    if (entity.Bytes[3] != 0)
                    {
                        entity.TargetAnimationId = 6;
                        entity.Flags |= 0x40;
                    }
                    else
                    {
                        entity.TargetAnimationId = 0;
                        SetCounter(entity, 0x28);
                    }
                }

                break;

            case >= 8 and <= 14:
                if (entity.IsOnGround != 0)
                {
                    entity.TargetAnimationId = 2;
                }

                break;

            case 19:
                counter = unchecked((ushort)(counter - 1));
                SetCounter(entity, counter);
                if (counter == 0)
                {
                    entity.TargetAnimationId = 0;
                    SetCounter(entity, 0x1E);
                }

                break;
        }
    }

    private static void SetCounter(AlundraEntityScriptProxy entity, int value) => entity.AIValues[1] = unchecked((short)value);

    private static uint DirectionToHero(AlundraEntityScriptProxy entity, AlundraEntityScriptProxy hero)
        => ScriptHelper.GetDirectionToTarget(unchecked(hero.PosX - entity.PosX), unchecked(hero.PosY - entity.PosY));
}
