#nullable enable
using System;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Hero;

/// <summary>
/// E20.g1a (docs/plan-e20-heros.md section 1.24.1, phase A, D-G-4 and D-G-5): the rules of the object in the hands of the hero, read in the binary (<c>ALUN_CD.EXE</c> (France), <c>docs/plan-e20-annexe/e20-g1a/</c>) and written as functions of what
/// the binary reads, so that each can be tested without a world. Nothing here is called by a production path yet: <see cref="PlayerManager"/> wires them in phase B (<c>UpdatePlayerCarriedEntity</c> and the states of the carrying group).
/// <list type="bullet">
/// <item><description><see cref="StepTowards"/> (<c>0x8002F854</c>): the step of every axis.</description></item>
/// <item><description><see cref="UpdateCarried"/> (<c>UpdatePlayerCarriedEntity</c>, <c>0x8002F884</c>): mode 0 (<c>0x80031D78</c>, every free tick) draws the object up to the head of the hero by one pixel a tick on each axis, <see cref="FreeOffset"/>;
/// mode 1 (the locked branch, <c>0x80031BF4</c>) waits five calls, then destroys an object that is within <see cref="DestroyLimit"/> of the hero and otherwise draws it in by two pixels, <see cref="LockedPull"/>; mode 2 (a wound, deep water, a death)
/// lets the object go with the code of throw -1, at the head of the hero. A sheet of balance without an attack (the seeds, the herb) turns mode 1 into mode 0 (the object is kept by the lock).</description></item>
/// <item><description><see cref="CarryGroup"/> and <see cref="LiftGestureNext"/>: the next state of the hero in the carrying group (<c>0x8003232C</c>) and in the two lift gestures (<c>0x800322F0</c>, <c>0x800322B4</c>).</description></item>
/// </list>
/// The offset words (<c>+0x30/+0x34/+0x38</c>) and the counter (<c>[0x80098F28]</c>) belong to the hero (D-G-4: <see cref="EntityScriptProxy.RelativeWarpOffsetX"/> and <see cref="EntityScriptProxy.CarryLockCounter"/>).
/// </summary>
internal static class CarryRules
{
    /// <summary>The height of the head of the hero above his feet: 32 px, the target of Z in mode 0 and the height of the object let go in mode 2 (<c>0x200000</c>).</summary>
    internal const int HeadHeight = 0x200000;

    /// <summary>The step of every axis in mode 0: one pixel a tick.</summary>
    internal const int FreeStep = 0x10000;

    /// <summary>The step of every axis in mode 1: two pixels a call.</summary>
    internal const int LockedStep = 0x20000;

    /// <summary>The three gaps at most this far from the hero (<c>0x1FFFF</c>, less than <see cref="LockedStep"/>) let mode 1 destroy the object.</summary>
    internal const int DestroyLimit = 0x1FFFF;

    /// <summary>Mode 1 counts its calls up to this value before it acts (<c>slti 5</c>, signed).</summary>
    internal const int LockedTicksBeforePull = 5;

    /// <summary>The state 0x00: standing, on the ground.</summary>
    internal const uint IdleState = 0x00;

    /// <summary>The state 0x05 (animation 5, sound 44): the lift gesture on the ground; chained by its sheet to <see cref="CarryStillState"/>.</summary>
    internal const uint LiftGroundState = 0x05;

    /// <summary>The state 0x06 (animation 6): the jump in walk with an object.</summary>
    internal const uint JumpWalkState = 0x06;

    /// <summary>The state 0x07 (animation 7): the walk with an object.</summary>
    internal const uint CarryWalkState = 0x07;

    /// <summary>The state 0x0B (animation 11): the lift gesture in the air; chained by its sheet to <see cref="AirStillState"/>.</summary>
    internal const uint LiftAirState = 0x0B;

    /// <summary>The state 0x0C (animation 12): standing with an object on the ground.</summary>
    internal const uint CarryStillState = 0x0C;

    /// <summary>The state 0x2D: in the air without a direction (the fall state the binary gives a hero who has lost his object in the air).</summary>
    internal const uint FallState = 0x2D;

    /// <summary>The state 0x2E (animation 46): the jump on the spot with an object.</summary>
    internal const uint JumpStillState = 0x2E;

    /// <summary>The state 0x2F (animation 47): in the air in walk with an object.</summary>
    internal const uint AirWalkState = 0x2F;

    /// <summary>The state 0x30 (animation 48): in the air on the spot with an object.</summary>
    internal const uint AirStillState = 0x30;

    /// <summary>The state 0x09 (animation 9): the throw on the ground; chained by its sheet to the idle state 0.</summary>
    internal const uint ThrowGroundState = 0x09;

    /// <summary>The state 0x0A (animation 10): the throw in the air; chained by its sheet to the fall (animation 45).</summary>
    internal const uint ThrowAirState = 0x0A;

    /// <summary>Three raw (16.16) quantities: the gaps of a position, the offset of an object from his carrier.</summary>
    internal readonly record struct Triple(int X, int Y, int Z);

    /// <summary>
    /// What the carrying group reads (<c>0x8003232C</c>): an object in the hands, its kind (<see cref="EntityPickupKind.LiftableHeavy"/>), the ground, a direction held on the pad, the rising edges of the Square and the Cross, and the two properties of
    /// the ground under the feet (<c>CombinedVramFlagsOR</c> 0x2000 forbids the throw, 0x4000 the jump).
    /// </summary>
    internal readonly record struct CarryInput(bool HasObject, bool Heavy, bool Ground, bool DirectionHeld, bool SquareEdge, bool CrossEdge, bool NoThrow, bool NoJump);

    /// <summary>The result of the group: the next state (null: the state stays) and whether the Square throws (the branch of the carrying group that calls <see cref="ThrowCarried"/>, E20.g2a).</summary>
    internal readonly record struct CarryStep(uint? NextState, bool Throw);

    /// <summary>
    /// <c>StepTowards</c> (<c>0x8002F854</c>): <paramref name="target"/> when the gap to it is under <paramref name="step"/> (the gap is the unsigned magnitude of the 32-bit difference), otherwise <paramref name="current"/> moved by
    /// <paramref name="step"/> towards it. The 32-bit wrap is the machine's.
    /// </summary>
    internal static int StepTowards(int current, int target, int step)
    {
        var difference = unchecked(target - current);
        var gap = difference < 0 ? unchecked((uint)-difference) : (uint)difference;
        if (gap < (uint)step)
        {
            return target;
        }

        return difference > 0 ? unchecked(current + step) : unchecked(current - step);
    }

    /// <summary>
    /// Mode 0 (<c>0x8002F8F4</c> and following): the new offset from the gaps (object minus hero). <c>m</c> is the larger of the two horizontal gaps and <c>e</c> the error of Z against the head; when <c>m &lt; e</c> only Z advances (the horizontal
    /// gaps are written as they are), otherwise, equality included (the disassembly branches on "less than", oracle <c>c1_equality</c>), the three axes advance by <see cref="FreeStep"/>, X and Y towards 0 and Z towards <see cref="HeadHeight"/>.
    /// </summary>
    internal static Triple FreeOffset(Triple difference)
    {
        var horizontal = Math.Max(Math.Abs((long)difference.X), Math.Abs((long)difference.Y));
        var error = Math.Abs((long)difference.Z - HeadHeight);
        var z = StepTowards(difference.Z, HeadHeight, FreeStep);
        return horizontal < error
            ? new Triple(difference.X, difference.Y, z)
            : new Triple(StepTowards(difference.X, 0, FreeStep), StepTowards(difference.Y, 0, FreeStep), z);
    }

    /// <summary>Mode 1 past its five calls, when the object is not within <see cref="DestroyLimit"/>: each axis advances by <see cref="LockedStep"/> towards 0.</summary>
    internal static Triple LockedPull(Triple difference)
        => new(StepTowards(difference.X, 0, LockedStep), StepTowards(difference.Y, 0, LockedStep), StepTowards(difference.Z, 0, LockedStep));

    /// <summary>Mode 1: the three gaps are at most <see cref="DestroyLimit"/> in magnitude (the object has been drawn in), so it is destroyed (<c>0x8003A774</c>: no loot, no effect).</summary>
    internal static bool CanDestroyLocked(Triple difference)
        => Math.Abs((long)difference.X) <= DestroyLimit && Math.Abs((long)difference.Y) <= DestroyLimit && Math.Abs((long)difference.Z) <= DestroyLimit;

    /// <summary>
    /// <c>UpdatePlayerCarriedEntity</c> (<c>0x8002F884</c>) for <paramref name="hero"/>, in <paramref name="mode"/> 0, 1 or 2 (any other value is mode 2, the binary's last branch). The object is the entity of <see cref="EntityScriptProxy.CarriedEntity"/>;
    /// nothing carried resets the counter and returns. An engine entity without an <see cref="EntityScriptProxy"/> proxy (no object to move) is left alone. <paramref name="world"/> is the host of the single-argument destruction of mode 1
    /// (<c>player.ScriptHost</c> in production); without one, the destruction is not made. A sheet without an attack, or without a sheet, turns mode 1 into mode 0.
    /// </summary>
    internal static void UpdateCarried(EntityScriptProxy hero, int mode, IEntityWorldContext? world)
    {
        if (hero.CarriedEntity == null)
        {
            hero.CarryLockCounter = 0;
            return;
        }

        if (hero.CarriedEntity.GameplayProxy is not EntityScriptProxy item)
        {
            return;
        }

        if (mode == 1 && (item.BalanceRecord?.Attacks.Count ?? 0) == 0)
        {
            mode = 0;
        }

        switch (mode)
        {
            case 0:
                WriteOffset(hero, FreeOffset(Difference(hero, item)));
                hero.CarryLockCounter = 0;
                break;
            case 1:
                if (hero.CarryLockCounter < LockedTicksBeforePull)
                {
                    hero.CarryLockCounter = unchecked(hero.CarryLockCounter + 1);
                    break;
                }

                var difference = Difference(hero, item);
                if (CanDestroyLocked(difference))
                {
                    world?.DestroyEntity(item);
                    break;
                }

                WriteOffset(hero, LockedPull(difference));
                break;
            default:
                item.Flags2 = -1;
                item.TargetDirection = hero.TargetDirection;
                item.PosX = hero.PosX;
                item.PosY = hero.PosY;
                item.PosZ = unchecked(hero.PosZ + HeadHeight);
                hero.CarryLockCounter = 0;
                break;
        }
    }

    private static Triple Difference(EntityScriptProxy hero, EntityScriptProxy item)
        => new(unchecked(item.PosX - hero.PosX), unchecked(item.PosY - hero.PosY), unchecked(item.PosZ - hero.PosZ));

    private static void WriteOffset(EntityScriptProxy hero, Triple offset)
    {
        hero.RelativeWarpOffsetX = offset.X;
        hero.RelativeWarpOffsetY = offset.Y;
        hero.RelativeWarpOffsetZ = offset.Z;
    }

    /// <summary>True for the six states that share the body at <c>0x8003232C</c>: 0x06, 0x07, 0x0C, 0x2E, 0x2F and 0x30.</summary>
    internal static bool IsCarryGroupState(uint state)
        => state is JumpWalkState or CarryWalkState or CarryStillState or JumpStillState or AirWalkState or AirStillState;

    /// <summary>
    /// The decision of the carrying group after the direction of the pad and the item driver (<c>0x8003232C</c>-<c>0x800323D0</c>), in the order of the binary: no object, idle on the ground and <see cref="FallState"/> in the air; the Square (an edge)
    /// throws unless the ground forbids it (then the tick ends there, the state stays); a heavy object (kind 3) keeps the plain carrying state, on the ground or in the air; on the ground the Cross (an edge) jumps, unless the ground forbids it, in walk
    /// with a direction held and on the spot without; otherwise walk with a direction held and the plain state without; in the air the walk or still state of the air. There is no way to put the object down: the Square always throws.
    /// </summary>
    internal static CarryStep CarryGroup(in CarryInput input)
    {
        if (!input.HasObject)
        {
            return new CarryStep(input.Ground ? IdleState : FallState, false);
        }

        if (input.SquareEdge)
        {
            return input.NoThrow ? new CarryStep(null, false) : new CarryStep(null, true);
        }

        if (input.Heavy)
        {
            return new CarryStep(input.Ground ? CarryStillState : AirStillState, false);
        }

        if (!input.Ground)
        {
            return new CarryStep(input.DirectionHeld ? AirWalkState : AirStillState, false);
        }

        if (input.CrossEdge)
        {
            return input.NoJump ? new CarryStep(null, false) : new CarryStep(input.DirectionHeld ? JumpWalkState : JumpStillState, false);
        }

        return new CarryStep(input.DirectionHeld ? CarryWalkState : CarryStillState, false);
    }

    /// <summary>
    /// The end of the two lift gestures: <see cref="LiftGroundState"/> (<c>0x800322F0</c>) becomes <see cref="FallState"/> without an object and <see cref="AirStillState"/> in the air, and stays on the ground (its sheet chains it to the plain state);
    /// <see cref="LiftAirState"/> (<c>0x800322B4</c>) becomes <see cref="IdleState"/> without an object and <see cref="CarryStillState"/> on the ground, and stays in the air. Null: the state stays (and null for every other state).
    /// </summary>
    internal static uint? LiftGestureNext(uint state, bool hasObject, bool ground)
    {
        if (state == LiftGroundState)
        {
            return !hasObject ? FallState : !ground ? AirStillState : null;
        }

        if (state == LiftAirState)
        {
            return !hasObject ? IdleState : ground ? CarryStillState : null;
        }

        return null;
    }

    /// <summary>
    /// The state <c>TryLift</c> (<c>0x8002EDBC</c>) gives the hero when the object is in his hands: the plain carrying state when the object stood on his head (the object's support was the hero), otherwise the lift gesture; on the ground
    /// or in the air.
    /// </summary>
    internal static uint LiftAnimation(bool wasOnHead, bool ground)
        => wasOnHead ? (ground ? CarryStillState : AirStillState) : (ground ? LiftGroundState : LiftAirState);

    /// <summary>
    /// E20.g2a (docs/plan-e20-heros.md section 1.24.2, phase A, D-G-10): the throw of the Square in the carrying group (<c>0x8003237C-0x80032404</c>), as a function of the hero and of what the group read: <paramref name="ground"/> (the hero's
    /// <c>IsOnGround</c>, <c>+0x144</c>) and <paramref name="directionHeld"/> (a direction held on the pad). Returns false and writes nothing when nothing is in the hands (an engine entity without a proxy counts as nothing) or when the ground
    /// forbids the throw (<c>CombinedVramFlagsOR &amp; 0x2000</c>, <see cref="PlayerManager.NoAttackVramFlag"/>: the binary ends the tick there). Otherwise it writes, in the binary's order, the direction word of the object (<c>+0x8C</c>) from the
    /// table <c>0x80023644</c> indexed by the hero's direction of the PREVIOUS tick (<see cref="EntityScriptProxy.BoxDirection"/>, <c>+0x98</c>: never the pad of this tick nor the end-of-frame <see cref="EntityScriptProxy.AnimationDirection"/>,
    /// D-F3-2, probe P-G-2), its position (the hero's X and Y, his Z plus <see cref="HeadHeight"/>), then on the ground the hero's state <see cref="ThrowGroundState"/> and the code <c>+0x24</c> of the object 1 (no direction held) or 2, in the air
    /// <see cref="ThrowAirState"/> and 2 or 3. The mark of the thrower (<see cref="EntityScriptProxy.ThrownBy"/>, E20.g1d) is set on the object. The links, the flags, the marker, the offset and the counter are not touched: the program C23 of the object turns the code into a projectile at its own tick (<see cref="Alundra.Ai.Objects.LiftableAi"/>),
    /// and there is no way to put the object down: the Square always throws.
    /// </summary>
    internal static bool ThrowCarried(EntityScriptProxy hero, bool ground, bool directionHeld)
    {
        if (hero.CarriedEntity?.GameplayProxy is not EntityScriptProxy item || (hero.CombinedVramFlagsOR & PlayerManager.NoAttackVramFlag) != 0)
        {
            return false;
        }

        item.ThrownBy = hero; // E20.g1d (D-G1D-3): the sort pass orders the flying object against its thrower.
        item.TargetDirection = (uint)PlayerManager.SpawnDirections[Math.Clamp(hero.BoxDirection, 0, PlayerManager.SpawnDirections.Length - 1)];
        item.PosX = hero.PosX;
        item.PosY = hero.PosY;
        item.PosZ = unchecked(hero.PosZ + HeadHeight);
        if (ground)
        {
            hero.TargetAnimationId = ThrowGroundState;
            item.Flags2 = directionHeld ? 2 : 1;
        }
        else
        {
            hero.TargetAnimationId = ThrowAirState;
            item.Flags2 = directionHeld ? 3 : 2;
        }

        return true;
    }
}
