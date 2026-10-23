#nullable enable
using System.Collections.Generic;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Projectiles;

/// <summary>
/// E20.f5 (docs/plan-e20-heros.md section 1.21.1, D-F5-1 to D-F5-9): the native handler E17 (<c>0x8007F420</c>, slot E, code 17 of the master table <c>0x80098F4C</c>) of the normal arrows, the sprite types 3 (the hunter's arrow) and 4 (William's normal arrow) of the global page; the types 5 and 6 carry
/// the same codes (and no attack), so the key is registered for the four, the set the header names (D-F5-6). The program E runs at each tick of the status Deactivated, which the arrow enters when it meets a wall or the edge of the map (<see cref="EntityFlags.DeactivateOnImpact"/>), when it hits something
/// (<see cref="EntityFlags.DeactivateOnHit"/>) or when its animation ends (<see cref="EntityFlags.DeactivateOnAnimationEnd"/>). E17 tells these cases apart by the animation asked and by the hit counter:
/// <list type="bullet">
/// <item><description>animation 1 (<c>+0x88 == 1</c>, the phase of the bounce): at the end of the animation (<c>+0xAC == 1</c>, <see cref="EntityScriptProxy.ForceResetAnimationFlag"/>) the arrow is destroyed, otherwise nothing;</description></item>
/// <item><description>otherwise, without a hit (<c>+0x220 == 0</c>, <see cref="EntityScriptProxy.HitCounter"/>): the bounce (<see cref="Bounce"/>), a hop of 16 ticks back where the arrow came from;</description></item>
/// <item><description>otherwise, the search of what the arrow touched (<see cref="FindTarget"/>): found, the arrow is destroyed; not found (the target was made invulnerable after the touch), it goes back to the status Normal and flies on.</description></item>
/// </list>
/// The binary falls from the destruction into the writes of the bounce on the dead entity (no effect, D-F5-3): here the destruction returns. The program A0 of the two types only writes <c>AIValues[0]</c>, which no code of the arrow reads: it is not registered (D-F5-6).
/// </summary>
internal static class ArrowAi
{
    /// <summary>The sprite types that carry E17: the hunter's arrow (3), William's normal arrow (4) and the two types of the header without an attack (5, 6).</summary>
    internal static readonly int[] SpriteTypes = { 3, 4, 5, 6 };

    /// <summary>The program code of E17 in slot E.</summary>
    internal const int DeactivateCode = 17;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>The sprite type that E17 never finds: <c>0x1AD</c> = <c>0x100 + 173</c>, the balloon switch of the map 16 (the arrow flies through it).</summary>
    internal const int ExcludedSpriteType = 0x1AD;

    /// <summary>The animation of the bounce (<c>+0x88 = 1</c>): the 16 ticks of the hop, then the end of the animation destroys the arrow.</summary>
    internal const uint BounceAnimationId = 1;

    /// <summary>The bits the bounce sets in the flags (<c>0x140</c>: the gravity bit, <see cref="EntityFlags.Gravity"/>, and <c>0x40</c>, already set by the header) and clears (<c>0x30</c>, no longer read by anything).</summary>
    private const uint BounceSetFlags = 0x140;

    private const uint BounceClearFlags = 0x30;

    /// <summary>Registers E17 for the types 3 to 6.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Deactivate(entity, context));

    /// <summary>E17 (<c>0x8007F420</c>), one tick of the Deactivated arrow.</summary>
    internal static void Deactivate(EntityScriptProxy entity, IEntityWorldContext context)
    {
        if (entity.TargetAnimationId == BounceAnimationId)
        {
            if (entity.ForceResetAnimationFlag == 1)
            {
                entity.ScriptHost?.DestroyEntity(entity, -1);
            }

            return;
        }

        if (entity.HitCounter == 0)
        {
            Bounce(entity);
            return;
        }

        if (FindTarget(entity, context))
        {
            // The binary destroys the arrow and then runs the writes of the bounce on the dead entity, which change nothing that lives (the break effect of the header is 0 for these types, so nothing is made either).
            entity.ScriptHost?.DestroyEntity(entity, -1);
            return;
        }

        entity.Status = EntityStatus.Normal;
    }

    /// <summary>
    /// The bounce (<c>0x8007F61C-0x8007F644</c>): the animation 1, the direction turned half a turn (<c>(+0x8C + 0x10) &amp; 0x1F</c>), the flags <c>(+0x6C | 0x140) &amp; ~0x30</c>; only the gravity bit does something afterwards, so the arrow falls and comes back at -1.875 pixels per tick.
    /// The controller is told of the new flags (<see cref="EntityScriptProxy.ResyncControllerFromFlags"/>, an assurance: it changes neither the walkability mask, which reads the classes only, nor the gravity of the controller, which is always null here, D-F5-4).
    /// </summary>
    internal static void Bounce(EntityScriptProxy entity)
    {
        entity.TargetAnimationId = BounceAnimationId;
        entity.TargetDirection = (entity.TargetDirection + 0x10) & 0x1F;
        entity.Flags = (entity.Flags | BounceSetFlags) & ~BounceClearFlags;
        entity.ResyncControllerFromFlags();
    }

    /// <summary>
    /// The search of E17 (<c>0x8007F4E4-0x8007F5F0</c>): every entity of the world, in the order of the slots and WITHOUT any filter of status (an entity destroyed in the same tour, status 4, is still found), except the arrow itself, an entity untouchable by its animation (<c>+0xB4 &amp; 0x40</c>,
    /// <see cref="EntityScriptProxy.HitAnimByte"/>), an entity that is invulnerable since a hit (<c>+0x1CC != 0</c>, <see cref="EntityScriptProxy.DamagedTickCounter"/>, written by the opcode <c>0x86</c> and, later, by the handlers), an entity whose classes do not meet the mask of the arrow (the same
    /// mask as the hit pass, <see cref="HitPass.ClassMaskOf"/>) and the balloon switch (<see cref="ExcludedSpriteType"/>). The overlap is the one of the hit pass (<see cref="HitPass.AxisOverlaps"/>, inclusive) between the box the arrow STORED after its last tick (<see cref="EntityScriptProxy.HitBoxX"/> to
    /// <see cref="EntityScriptProxy.HitBoxZ"/> and its size) and the body of the target; the hero is one of the entities (it is in the world's list), with the classes of the hero (<c>0x3118C</c>: none of the mask of an arrow).
    /// </summary>
    internal static bool FindTarget(EntityScriptProxy arrow, IEntityWorldContext context)
    {
        var mask = HitPass.ClassMaskOf(arrow.Flags);
        foreach (var target in context.SpawnedEntities)
        {
            if (ReferenceEquals(target, arrow)
                || (target.HitAnimByte & HitPass.InvulnerableAnimationBit) != 0
                || target.DamagedTickCounter != 0
                || (target.Flags & mask) == 0
                || target.SpriteType == ExcludedSpriteType)
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

            if (HitPass.AxisOverlaps(arrow.HitBoxX, arrow.CollisionWidth, bodyX, target.Width)
                && HitPass.AxisOverlaps(arrow.HitBoxY, arrow.CollisionDepth, bodyY, target.Height)
                && HitPass.AxisOverlaps(arrow.HitBoxZ, arrow.CollisionHeight, bodyZ, target.Depth))
            {
                return true;
            }
        }

        return false;
    }
}
