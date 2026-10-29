#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4d2 (docs/plan-e20-heros.md section 1.31.6, D-H4D2-1 to D-H4D2-7): the fire tome 1, the item <c>0x2F</c>: the native handler C63 (<c>0x8007AC60</c>, slot C) for the sprite type 23, the fire ball, and the cancellation <c>0x80034D2C</c>; E3 for the type 23 is the one of the rocks
/// (<see cref="EarthTome1Ai.SpriteTypes"/>). The pilot <c>0x80035A84</c> is the phase A2 of the slice, the entries <c>0x2F</c> of the three seams of the item in use and the key of C63 in <see cref="NativeSpriteRegistry.CreateForProduction"/> its phase B: in the phase A1 this class is inert in
/// production (the tests install the key themselves).
/// <list type="bullet">
/// <item><description>C63, each tick of a ball (<see cref="BallTick(EntityScriptProxy, IReadOnlyList{EntityScriptProxy}, int, bool, int)"/>): when the wait counter (<c>+0x274</c>, <see cref="TomeWords"/>) is not zero it loses one and nothing else is done (the ball born at the tick S holds 16, 15 at S+1, 0 at the end of S+16, the first action is at S+17). Otherwise the Z force
/// is -3 px per tick (<c>0xFFFD0000</c>) and the target is handled. Without a target (<see cref="EntityScriptProxy.DelayOrAngleOrEntityId"/> zero) the query of the spells (<see cref="TargetQuery.Find"/>, the mask <see cref="EntityFlags.ClassA"/>, the type the low nibble of the attribute byte of
/// the attack of the ball) lists the enemies: none, and the wait is one draw below 9; at least one, and the FIRST becomes the target (its slot in <see cref="EntityScriptProxy.DelayOrAngleOrEntityId"/>, the reference in <see cref="EntityScriptProxy.SpellTarget"/>) and the turn is made in the same tick. With a target, it must still be the same entity at the status
/// <see cref="EntityStatus.Normal"/>, otherwise it is forgotten (the query restarts at the next tick, with no wait). The turn (D-H4D2-1, D-E20-57, the original's, loops included): the direction <c>d</c> of the raw differences (<see cref="ScriptHelper.GetDirectionToTarget"/>) against the direction asked <c>cur</c>,
/// compared as NUMBERS (no wrap from 31 to 0): equal, nothing is written; below, <c>d</c> if <c>cur - d &lt; 4</c> otherwise <c>cur - 4</c>; above, <c>d</c> if <c>d - cur &lt; 4</c> otherwise <c>cur + 4</c>; the direction is written and the wait becomes 2 (a step every three ticks).</description></item>
/// <item><description>The cancellation <c>0x80034D2C</c>, called with the item still in use (only when it is <c>0x2F</c>: the dispatcher of the binary): each entity at the status <see cref="EntityStatus.Normal"/> is looked at in the order of the slots; the column (the type 20) gives the effect <c>(19, 2)</c> at its raw position (Z in the binary's frame) THEN
/// <c>DestroyEntity(e)</c> (the one-argument destruction: no effect of break); the ball (the type 23) goes to the status <see cref="EntityStatus.Deactivated"/> and loses its blocker; the others are left. The item is freed by <see cref="PlayerManager.CancelItemMode"/>.</description></item>
/// </list>
/// </summary>
internal static class FireTome1Ai
{
    /// <summary>The sprite type of the fire balls: 23 (<c>0x17</c>, of the global page).</summary>
    internal static readonly int[] SpriteTypes = { 23 };

    /// <summary>The program code of C63 in slot C.</summary>
    internal const int BallCode = 63;

    /// <summary>The keys this class registers (the pins of the registry follow them). E3 of the type 23 is the key of <see cref="EarthTome1Ai"/>.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, BallCode) };

    private const int ColumnType = 20;
    private const int BallType = 23;

    /// <summary>The Z force of a ball that falls: -3 px per tick (<c>0xFFFD0000</c>).</summary>
    private const int FallForceZ = unchecked((int)0xFFFD0000);

    /// <summary>The wait after a turn: a step every three ticks.</summary>
    private const int TurnWait = 2;

    /// <summary>The bound of the draw of the wait when the query finds no enemy: 9.</summary>
    private const int NoTargetWaitBound = 9;

    /// <summary>The turn step: 4 of the 32 directions.</summary>
    private const int TurnStep = 4;

    /// <summary>The pilot <c>0x80035A84</c> of the item <c>0x2F</c> (0: still in use; 1: finished). SKELETON of the tests-first commit of the phase A2: it does nothing (the code follows).</summary>
    internal static int Drive(EntityScriptProxy hero) => 0;

    /// <summary>C63 as the registry runs it: the count of the query is the highest slot of the entities born (<see cref="TargetQuery.StoredCount"/>), the settings of the query those of production.</summary>
    internal static void BallTick(EntityScriptProxy ball, IEntityWorldContext context)
        => BallTick(ball, context.SpawnedEntities, TargetQuery.StoredCount(context.SpawnedEntities));

    /// <summary>
    /// C63 with the count of the query given (the word <c>[0x80134360]</c> of the vectors) and the settings of the query: see the class. A ball without an attack asks the query for the type 0 (the binary reads the byte at the pointer <c>+0x1C8</c>; a null pointer has no oracle, the ball of the sprite 23 always holds an attack in the animation 0).
    /// </summary>
    internal static void BallTick(EntityScriptProxy ball, IReadOnlyList<EntityScriptProxy> entities, int count, bool inclusiveBound = TargetQuery.InclusiveBound, int sortThreshold = TargetQuery.SortThreshold)
    {
        var wait = TomeWords.Read274(ball);
        if (wait != 0)
        {
            TomeWords.Write274(ball, unchecked(wait - 1));
            return;
        }

        ball.ForceZ = FallForceZ;
        EntityScriptProxy target;
        if (ball.DelayOrAngleOrEntityId == 0)
        {
            var type = (ball.CurrentAttack?.Attribute ?? 0) & 0xF;
            var found = TargetQuery.Find(entities, count, ball, EntityFlags.ClassA, type, inclusiveBound, sortThreshold);
            if (found.Count == 0)
            {
                TomeWords.Write274(ball, EnemyHelpers.Below(NoTargetWaitBound));
                return;
            }

            target = found[0];
            ball.DelayOrAngleOrEntityId = target.Slot;
            ball.SpellTarget = target;
        }
        else
        {
            if (ball.SpellTarget is not { Status: EntityStatus.Normal } kept)
            {
                ball.DelayOrAngleOrEntityId = 0;
                ball.SpellTarget = null;
                return;
            }

            target = kept;
        }

        Turn(ball, target);
    }

    /// <summary>The turn of C63 (<c>0x8007AD58-0x8007ADBC</c>): see the class.</summary>
    private static void Turn(EntityScriptProxy ball, EntityScriptProxy target)
    {
        var wanted = (int)ScriptHelper.GetDirectionToTarget(unchecked(target.PosX - ball.PosX), unchecked(target.PosY - ball.PosY));
        var current = (int)ball.TargetDirection;
        if (wanted == current)
        {
            return;
        }

        int next;
        if (wanted < current)
        {
            next = current - wanted < TurnStep ? wanted : current - TurnStep;
        }
        else
        {
            next = wanted - current < TurnStep ? wanted : current + TurnStep;
        }

        ball.TargetDirection = (uint)next;
        TomeWords.Write274(ball, TurnWait);
    }

    /// <summary>The cancellation <c>0x80034D2C</c> of the item <c>0x2F</c>: see the class (the item is freed by <see cref="PlayerManager.CancelItemMode"/>).</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
        if (hero.ScriptHost is not IEntityWorldContext world)
        {
            return;
        }

        foreach (var entity in world.SpawnedEntities)
        {
            if (entity.Status != EntityStatus.Normal)
            {
                continue;
            }

            if (entity.SpriteType == ColumnType)
            {
                var x = entity.PosX;
                var y = entity.PosY;
                var z = entity.PosZ + 1; // the binary's frame of Z (ADR-0026).
                hero.ScriptHost.EffectStage?.Emit(reservoir => NativeEffects.CreateFireColumnBlast(reservoir, x, y, z));
                world.DestroyEntity(entity);
            }
            else if (entity.SpriteType == BallType)
            {
                entity.Status = EntityStatus.Deactivated;
                entity.BlockedByEntity = null;
            }
        }
    }

    /// <summary>Registers C63 for the type 23 alone (E3 is the registration of <see cref="EarthTome1Ai"/>); the disposable takes the key off (for the tests that register it themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, BallCode, SpriteTypes, (entity, context, state) => BallTick(entity, context));
}
