#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g2c3 phase A (docs/plan-e20-heros.md section 1.24.5, D-G2C3-1 to D-G2C3-3): the native handlers A19 (<c>0x800619D0</c>, slot A, 3 words) and C71 (<c>0x8007B834</c>, slot C, 89 words) of the drop of water, the map-page sprite 135 (the type 391), the only sprite type that holds them.
/// <list type="bullet">
/// <item><description>A19, at the load: the low byte of the program C that the record gave (<c>+0x54</c>, <see cref="EntityScriptProxy.ProgramIndexes"/> 2) is kept in <c>+0x275</c> (<c>Bytes[1]</c>): the restoration of the state 4 hands it back, which re-arms the script of the drop (a drop born with no program keeps 0).</description></item>
/// <item><description>C71, each tick, on the state <c>+0x274</c> (<c>Bytes[0]</c>): 0, arm (state 1, the counter <c>+0x282</c> = <see cref="WaitTicks"/>); 1, count down and at 0 start the fall (animation 1, state 2, Gravity); 2, at the landing (<c>CollidedWithEntityZ</c> or <c>IsOnGround</c>) the splash (animation 2, state 3);
/// 3, state 4 and <c>Collidable</c> off FIRST, then the test of the torch (below); 4, once the animation is at its end (<c>+0xAC</c> not zero): state 0, animation 0, <c>ForceZ</c> 0, Gravity off and <c>Collidable</c> on, the program C given back, and the drop goes <see cref="RiseRaw"/> (160 px) up; from the state 5 on, nothing.</description></item>
/// </list>
/// The torch: the support of the drop (<c>+0x12C</c>, <see cref="EntityScriptProxy.RidingEntity"/>); when it is the hero, the object he carries (<c>+0x2C</c>); when that entity is a torch (type 393) it is put out: status 3 (Deactivated, whatever its status was) and the mark <c>Bytes[0] = 1</c> that the E20 of the torch reads
/// (no flame is left). The drop reads neither the hit counter nor the touching entity, and writes no damage. The store <c>[0x8013DA20] = 1</c> of the state 4 (<c>g_clearProgramState</c>) is dead in the binary (C71 runs outside <c>RunScript</c>, which clears that word at its entry before any read) and is not ported (D-G2C3-2).
/// The enrolment in production is the phase B (<see cref="NativeSpriteRegistry.CreateForProduction"/>).
/// </summary>
internal static class WaterDropAi
{
    /// <summary>The sprite type of the drop of water: 391 (<c>0x100 + 135</c>), the type the torch tests too (<see cref="TorchAi.WaterDropSpriteType"/>).</summary>
    internal const int SpriteType = TorchAi.WaterDropSpriteType;

    /// <summary>The program codes of A19 (slot A) and C71 (slot C).</summary>
    internal const int LoadCode = 19;

    internal const int TickCode = 71;

    /// <summary>The wait of the state 1, in ticks (<c>+0x282 = 40</c>).</summary>
    internal const int WaitTicks = 40;

    /// <summary>The rise of the restoration: 160 px in 16.16 (<c>0xA00000</c>).</summary>
    internal const int RiseRaw = 160 << 16;

    /// <summary>The flag Gravity (<c>0x100</c>) the fall raises and the restoration drops, the flag Collidable (<c>0x80</c>) the splash drops and the restoration raises.</summary>
    private const uint Gravity = 0x100;

    private const uint Collidable = 0x80;

    private const uint FallAnimation = 1;

    private const uint SplashAnimation = 2;

    private const uint RestAnimation = 0;

    /// <summary>The mark the E20 of a torch reads: it goes without leaving a flame.</summary>
    private const byte NoFlameMark = 1;

    /// <summary>The keys the class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramALoad, LoadCode), (ScriptHelper.ProgramCTick, TickCode) };

    private static readonly int[] SpriteTypes = { SpriteType };

    /// <summary>Registers A19 and C71 for the type <see cref="SpriteType"/> only.</summary>
    internal static void Register(NativeSpriteRegistry registry)
    {
        registry.Register(ScriptHelper.ProgramALoad, LoadCode, SpriteTypes, (entity, context, state) => Load(entity));
        registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity, context));
    }

    /// <summary>What one tick of C71 decides, without its reads and writes.</summary>
    /// <param name="State">The state after the tick.</param>
    /// <param name="Counter">The counter after the tick (16 bits).</param>
    /// <param name="Animation">The animation asked, or null when the tick asks none.</param>
    /// <param name="Flags">The flags after the tick.</param>
    /// <param name="Restore">The restoration of the state 4: <c>ForceZ</c> 0, the program C given back, the rise.</param>
    /// <param name="Extinguish">The target is put out (the state 3 with a torch).</param>
    internal readonly record struct DropStep(byte State, ushort Counter, uint? Animation, uint Flags, bool Restore, bool Extinguish);

    /// <summary>
    /// The step of C71 (<c>0x8007B834-0x8007B998</c>) as a function of the state, the counter, the landing (<c>CollidedWithEntityZ</c> or <c>IsOnGround</c>), the end of the animation (<c>+0xAC</c> not zero), the flags and the question
    /// "the target is a torch". The states from 5 on do nothing.
    /// </summary>
    internal static DropStep Step(byte state, ushort counter, bool landed, bool animationDone, uint flags, bool targetIsTorch)
    {
        switch (state)
        {
            case 0:
                return new DropStep(1, WaitTicks, null, flags, false, false);
            case 1:
                var left = unchecked((ushort)(counter - 1));
                return left != 0
                    ? new DropStep(1, left, null, flags, false, false)
                    : new DropStep(2, left, FallAnimation, flags | Gravity, false, false);
            case 2:
                return landed
                    ? new DropStep(3, counter, SplashAnimation, flags, false, false)
                    : new DropStep(2, counter, null, flags, false, false);
            case 3:
                // the advance and Collidable come off before, and whatever, the test of the torch (D-G2C3-3).
                return new DropStep(4, counter, null, flags & ~Collidable, false, targetIsTorch);
            case 4:
                return animationDone
                    ? new DropStep(0, counter, RestAnimation, (flags & ~Gravity) | Collidable, true, false)
                    : new DropStep(4, counter, null, flags, false, false);
            default:
                return new DropStep(state, counter, null, flags, false, false);
        }
    }

    /// <summary>A19 (<c>0x800619D0</c>): <c>Bytes[1]</c> takes the low byte of the program C (<c>ProgramIndexes[2]</c>).</summary>
    internal static void Load(EntityScriptProxy drop) => drop.Bytes[1] = (byte)drop.ProgramIndexes[2];

    /// <summary>The entity the drop puts out: the object the HERO carries when the support is the hero, otherwise the support itself; null when there is no support or no object.</summary>
    private static EntityScriptProxy? TargetOf(EntityScriptProxy drop, IEntityWorldContext context)
    {
        var support = drop.RidingEntity;
        if (support == null)
        {
            return null;
        }

        var hero = context.PlayerEntity;
        var target = hero != null && ReferenceEquals(support, hero.LogicContextEntity) ? hero.CarriedEntity : support;
        return target?.GameplayProxy as EntityScriptProxy;
    }

    /// <summary>One tick of C71 for <paramref name="drop"/>: see the class. A change of the flags is handed to the controller (<see cref="EntityScriptProxy.ResyncControllerFromFlags"/>), the rise of the restoration to the root (<see cref="EntityScriptProxy.PushLogicalPositionToRoot"/>).</summary>
    internal static void Tick(EntityScriptProxy drop, IEntityWorldContext context)
    {
        var state = drop.Bytes[0];
        var target = state == 3 ? TargetOf(drop, context) : null;
        var step = Step(
            state,
            unchecked((ushort)drop.AIValues[1]),
            drop.CollidedWithEntityZ != 0 || drop.IsOnGround != 0,
            drop.ForceResetAnimationFlag != 0,
            drop.Flags,
            target != null && target.SpriteType == TorchAi.SpriteType);

        drop.Bytes[0] = step.State;
        drop.AIValues[1] = unchecked((short)step.Counter);
        if (step.Animation is { } animation)
        {
            drop.TargetAnimationId = animation;
        }

        if (step.Flags != drop.Flags || step.Restore)
        {
            // the restoration hands the flags over even when they already hold the values (the controller must not keep the gravity of the fall).
            drop.Flags = step.Flags;
            drop.ResyncControllerFromFlags();
        }

        if (step.Extinguish)
        {
            target!.Status = EntityStatus.Deactivated;
            target.Bytes[0] = NoFlameMark;
        }

        if (step.Restore)
        {
            // the store [0x8013DA20] = 1 of the binary is dead here (D-G2C3-2): not ported.
            drop.ForceZ = 0;
            drop.ProgramIndexes[2] = drop.Bytes[1];
            drop.PosZ = unchecked(drop.PosZ + RiseRaw);
            drop.PushLogicalPositionToRoot();
        }
    }
}
