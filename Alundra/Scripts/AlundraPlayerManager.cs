#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// V1 port of a narrow, documented subset of <c>PlayerManager.MovePlayer</c>
/// (alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerManager.cs:17-951, address 0x80031b50)
/// plus the LOGICAL (no <c>CharacterControllerComponent</c>, no gravity, no collision - user decision,
/// 2026-08-23, <c>docs/plan-conversion-totale.md</c> §2 D4/E2) kinematic integration
/// <c>PhysicsEngine.UpdateEntitiesPhysics</c>/<c>UpdateEntityPhysics</c> normally drives for the player.
/// Called once per frame from <see cref="AlundraEntityScriptProxy.Update"/>'s own <c>IsPlayer</c> branch
/// (the original calls <c>MovePlayer()</c> at the head of <c>EntityManager.UpdateEntitiesEvents</c>,
/// EntityManager.cs:808/17 - now this proxy's own per-entity tick instead, decision D2).
///
/// Ported (see <see cref="MovePlayer"/>):
/// <list type="bullet">
/// <item><description><c>BlockedByEntity != null</c> -&gt; END (PlayerManager.cs:31-36).</description></item>
/// <item><description><c>(g_playerControlFlags &amp; InputBlockedMask) != 0</c> -&gt; locked branch,
/// PlayerManager.cs:38-57: a deliberate no-op here (see <see cref="MovePlayer"/>'s own doc for why every
/// side effect of that branch is dormant on a fresh New Game).</description></item>
/// <item><description>Free branch header, PlayerManager.cs:59-60: <c>Flags |= Gravity</c> (kept for
/// parity; V1 has no physics reading it yet).</description></item>
/// <item><description>Pad -&gt; direction, PlayerManager.cs:199-205: <c>buttonsHold = ButtonsHold &gt;&gt;
/// 0xc</c>, <see cref="AnimationTables.DirectionByButtons"/>, invalid combination falls back to the
/// entity's current <c>TargetDirection</c>.</description></item>
/// <item><description>Idle(0x00)/Moving(0x01) case, PlayerManager.cs:363-383, SIMPLIFIED per E2's own
/// scope: <c>TargetDirection = dir</c>; <c>buttonsHold != 0 -&gt; Moving</c> else <c>Idle</c>. The
/// original's own <c>TryUseItem</c>/<c>PlayerTryAction</c>/<c>CheckEntityInteraction</c>/<c>PlayerTryAttack</c>
/// calls (item use, carried-entity/NPC interaction, weapon attack) are NOT ported - no item/interaction/
/// combat system exists yet.</description></item>
/// <item><description>LoadingMap(0x36) case, PlayerManager.cs:914-922 - see <see cref="MovePlayer"/>'s
/// own doc for the exact deviation (bridged straight to Idle instead of the original's ground-contact
/// check).</description></item>
/// </list>
///
/// NOT ported (out of E2's own scope, unconditionally, regardless of <c>TargetAnimationId</c>):
/// <list type="bullet">
/// <item><description><c>g_activeCollisionEntity = null</c>; weapon flags (<c>GetItemIdFromCurrentWeapon</c>,
/// <c>g_currentWeaponFlags</c>); <c>CheckAndExecuteWarp</c> - PlayerManager.cs:23-29,61-79 (no weapon/warp
/// system).</description></item>
/// <item><description>HP==0 death branch - PlayerManager.cs:82-170 (no HP system wired to the player
/// yet - <see cref="AlundraEntityScriptProxy.Hp"/> stays the C# default 0, which would otherwise
/// immediately hit this branch every frame; skipping it entirely is the deliberate V1 choice, not an
/// oversight).</description></item>
/// <item><description><c>TileAttributes &amp; 0x80</c> warp-tile branch - PlayerManager.cs:172-196 (no
/// tile-attribute sampling yet, <see cref="AlundraEntityScriptProxy.TileAttributes"/> stays 0).</description></item>
/// <item><description>The slope switch's non-flat cases (4=water, 6=climbing wall, default) -
/// PlayerManager.cs:207-353 (no collision/slope detection yet, <see cref="AlundraEntityScriptProxy.Slope_18c"/>
/// stays 0 = flat ground, which is exactly <c>case 0</c>'s own <c>break</c> - i.e. falling straight
/// through to the animation switch below, same as the original for this scenario).</description></item>
/// <item><description><c>UpdatePlayerWeaponEffect</c>/<c>UpdateWeaponStepProgression</c>/
/// <c>UpdatePlayerCarriedEntity</c> - PlayerManager.cs:355-357 (no weapon/carry system).</description></item>
/// <item><description>Every <c>TargetAnimationId</c> case other than Idle/Moving/LoadingMap - jump, sprint,
/// attack (all weapon types), pickup/carry/throw, climbing, swimming, sand, spell cast, damage-taken,
/// victory pose, etc. - PlayerManager.cs:385-943. An entity whose <c>TargetAnimationId</c> is none of the
/// three ported values simply keeps it unchanged (no case matches, same shape as the original's own
/// <c>default: AnimateWarpEffect(); break;</c>, itself not ported since it is a pure visual-effect
/// call).</description></item>
/// <item><description><c>END</c>: <c>UpdateItemEffectState</c>, <c>SetPlayerHpMax</c>/<c>SetPlayerHp</c> -
/// PlayerManager.cs:947-950 (no item/HP system).</description></item>
/// </list>
/// </summary>
public static class AlundraPlayerManager
{
    /// <summary>PlayerAnimation.Idle = 0x00 (alundra-datas-analyser .../Gameplay/PlayerAnimation.cs:5).</summary>
    private const uint IdleAnimationId = 0x00;

    /// <summary>PlayerAnimation.Moving = 0x01 (PlayerAnimation.cs:6).</summary>
    private const uint MovingAnimationId = 0x01;

    /// <summary>PlayerAnimation.LoadingMap = 0x36 (PlayerAnimation.cs:62) - the animation
    /// <c>ResetEntityState</c>/<c>AlundraWorldProxy</c>'s own pawn-adoption spawns the hero with
    /// (<see cref="AlundraGameState.ResetAnimationId"/>).</summary>
    private const uint LoadingMapAnimationId = 0x36;

    // EntityRecordMapper's own tile constants (StaticVariables.MapTileWidth/Height), duplicated here for
    // the same reason AlundraWorldProxy duplicates them on its own copy (see that class' own doc): only
    // this file's TileX/TileY re-derivation after a kinematic tick needs them.
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    /// <summary>Original engine tick rate the PSX build ran physics at (50 Hz) - see <see cref="Tick"/>'s
    /// own doc for the fixed-step accumulation this drives.</summary>
    private const float FixedTickSeconds = 1f / 50f;

    /// <summary>Caps the catch-up run inside <see cref="Tick"/> to at most this many 50 Hz steps per
    /// engine frame, so a long stall (debugger pause, GC hitch) cannot spiral into an unbounded loop of
    /// kinematic ticks - documented fixed-step deviation, <c>docs/plan-conversion-totale.md</c> §4 E2. Any
    /// accumulated remainder beyond that is dropped, not carried into the next frame.</summary>
    private const int MaxTicksPerFrame = 4;

    /// <summary>
    /// Port of the documented subset of <c>PlayerManager.MovePlayer</c> - see this class' own doc for the
    /// exact ported/not-ported line ranges. Only sets <see cref="AlundraEntityScriptProxy.TargetDirection"/>/
    /// <see cref="AlundraEntityScriptProxy.TargetAnimationId"/>/<see cref="AlundraEntityScriptProxy.Flags"/>;
    /// the kinematic integration that turns a changed <c>TargetDirection</c>/animation into an actual
    /// position change is <see cref="Tick"/>, called separately (mirrors the original's own split between
    /// <c>PlayerManager.MovePlayer</c> and <c>PhysicsEngine.UpdateEntitiesPhysics</c>).
    /// </summary>
    public static void MovePlayer(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state)
    {
        // PlayerManager.cs:31-36 (BlockedByEntity != null -> END). Nothing ported so far ever sets
        // BlockedByEntity (it stays the C# default null), so this is currently always false - ported
        // anyway for forward parity once something does set it.
        if (player.BlockedByEntity != null)
        {
            return;
        }

        // PlayerManager.cs:38-57 (locked branch): CreatePlayerAnimationEffects(1), UpdatePlayerCarriedEntity(1),
        // AnimateWarpEffect, and the ForcedSequence facing resolution are ALL no-ops on a fresh New Game -
        // nothing ported yet drives carried entities, warp effects, or forced-sequence facing - so this
        // whole branch is a deliberate no-op: the player simply does not move or change animation while
        // g_playerControlFlags carries any of ControlLocked/MessageBox/ForcedSequence.
        if ((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.InputBlockedMask) != 0)
        {
            return;
        }

        // PlayerManager.cs:59-60 (free branch header). CreatePlayerAnimationEffects(0) - not ported (no
        // effect system yet).
        player.Flags |= EntityFlags.Gravity;

        // PlayerManager.cs:61-353 (CheckAndExecuteWarp's warp-facing branch, HP==0 death branch,
        // TileAttributes&0x80 warp-tile branch, the slope switch's non-flat cases) - NOT PORTED, see this
        // class' own doc for why each is safe to skip for a fresh New Game / flat-ground V1 scenario.

        // PlayerManager.cs:198-205 (pad -> direction).
        var buttonsHold = pad.ButtonsHold >> 0xc;
        var dir = buttonsHold < AnimationTables.DirectionByButtons.Length
            ? AnimationTables.DirectionByButtons[buttonsHold]
            : uint.MaxValue;

        if (dir == uint.MaxValue)
        {
            dir = player.TargetDirection;
        }

        // PlayerManager.cs:355-357 (UpdatePlayerWeaponEffect/UpdateWeaponStepProgression/
        // UpdatePlayerCarriedEntity) - NOT PORTED (no weapon/carry system).

        // Documented V1 bridge for the LoadingMap(0x36) case (PlayerManager.cs:914-922): the original only
        // ever leaves LoadingMap when IsOnGround == 0 (branching to the Jump animation instead - itself not
        // ported, see this class' own doc); IsOnGround is never observable in V1 (no gravity/collision), so
        // a literal port would strand the freshly spawned hero in the LoadingMap pose forever. Instead, the
        // very first unlocked MovePlayer tick after spawn promotes LoadingMap straight to Idle so the
        // pad-driven Idle/Moving switch below can take over on the SAME tick - documented deviation,
        // docs/plan-conversion-totale.md §4 E2.
        if (player.TargetAnimationId == LoadingMapAnimationId)
        {
            player.TargetAnimationId = IdleAnimationId;
        }

        // PlayerManager.cs:361-383 (Idle/Moving case), simplified per E2's own scope - see this class' own
        // doc for exactly what is skipped (TryUseItem/PlayerTryAction/CheckEntityInteraction/PlayerTryAttack).
        if (player.TargetAnimationId == IdleAnimationId || player.TargetAnimationId == MovingAnimationId)
        {
            player.TargetDirection = dir;
            player.TargetAnimationId = buttonsHold != 0 ? MovingAnimationId : IdleAnimationId;
        }

        // PlayerManager.cs:385-943 (every other TargetAnimationId case) - NOT PORTED; an entity whose
        // TargetAnimationId is neither of the two ported values above keeps it unchanged.

        // PlayerManager.cs:947-950 (END: UpdateItemEffectState, SetPlayerHpMax/SetPlayerHp) - NOT PORTED.
    }

    /// <summary>
    /// Fixed-step (50 Hz, matching the original's own tick rate) kinematic integration - port of
    /// <c>PhysicsEngine.UpdateEntityPhysics</c> (PhysicsEngine.cs:1579-1597), the <c>IncrementForce</c>
    /// calls (PhysicsEngine.cs:1445-1446/1490-1491), and the flat-ground half of <c>ApplyEntityForces</c>
    /// (PhysicsEngine.cs:1514-1547) plus the position update (PhysicsEngine.cs:421-422) - restricted to
    /// exactly what a collision-free, gravity-free player needs (see this class' own doc, D4/E2). Accumulates
    /// <paramref name="elapsedTime"/> on <see cref="AlundraEntityScriptProxy.PhysicsTickAccumulator"/> and
    /// runs as many whole 50 Hz steps as have elapsed, capped at <see cref="MaxTicksPerFrame"/> per engine
    /// frame (documented fixed-step deviation - the original always ran exactly one physics tick per game
    /// frame, itself locked to ~50/60 Hz by the PSX's own vsync; this engine's frame rate is not fixed, so a
    /// slow frame must still advance the player by the same number of 50 Hz ticks a real frame budget would
    /// have covered, not by a single oversized step).
    /// </summary>
    public static void Tick(AlundraEntityScriptProxy player, float elapsedTime)
    {
        player.PhysicsTickAccumulator += elapsedTime;

        var ticks = 0;
        while (player.PhysicsTickAccumulator >= FixedTickSeconds && ticks < MaxTicksPerFrame)
        {
            player.PhysicsTickAccumulator -= FixedTickSeconds;
            RunOneTick(player);
            ticks++;
        }

        if (ticks >= MaxTicksPerFrame)
        {
            // Catch-up cap reached (see MaxTicksPerFrame's own doc) - drop the remainder rather than let
            // it carry over and force an even longer catch-up run next frame.
            player.PhysicsTickAccumulator = 0f;
        }
    }

    /// <summary>One 50 Hz kinematic step - see <see cref="Tick"/>'s own doc for the ported line ranges.</summary>
    private static void RunOneTick(AlundraEntityScriptProxy player)
    {
        AnimSetEntry animSet = default;
        var hasAnimSet = player.AnimSetsByAnim != null
            && player.AnimSetsByAnim.TryGetValue((int)player.TargetAnimationId, out animSet);
        var speed = hasAnimSet ? animSet.Speed : 0;
        var acceleration = (hasAnimSet ? animSet.Acceleration : 0) & 0xf;

        // PhysicsEngine.UpdateEntityPhysics (PhysicsEngine.cs:1579-1597): only recompute the target
        // force/step when speed, direction or acceleration actually changed since the last tick - exactly
        // the original's own early-out.
        if (player.Speed != speed || player.TargetDirection != player.CurrentDirection || player.Acceleration != acceleration)
        {
            player.CurrentDirection = player.TargetDirection;
            player.Speed = speed;
            player.Acceleration = acceleration;

            var dirIndex = (int)player.TargetDirection;
            var offsetX = dirIndex >= 0 && dirIndex < AnimationTables.OffsetXList.Length ? AnimationTables.OffsetXList[dirIndex] : (short)0;
            var offsetY = dirIndex >= 0 && dirIndex < AnimationTables.OffsetYList.Length ? AnimationTables.OffsetYList[dirIndex] : (short)0;

            player.TargetForceX = offsetX * speed;
            player.TargetForceY = offsetY * speed;
            player.ForceStepX = Math.Abs(player.TargetForceX - player.ForceX) >> player.Acceleration;
            player.ForceStepY = Math.Abs(player.TargetForceY - player.ForceY) >> player.Acceleration;
        }

        // PhysicsEngine.cs:1445-1446/1490-1491.
        player.ForceX = IncrementForce(player.ForceX, player.TargetForceX, player.ForceStepX);
        player.ForceY = IncrementForce(player.ForceY, player.TargetForceY, player.ForceStepY);

        // PhysicsEngine.ApplyEntityForces (PhysicsEngine.cs:1514-1547), flat-ground-only: TileAttributes
        // stays 0 in V1 (no collision), so XForceTable[0]/YForceTable[0] (both 0, ScriptHelper.cs:183-194)
        // contribute nothing; PreviousAdjustedForceX/Y stay 0 (nothing ported ever sets them - the
        // original only feeds them from a riding-platform entity, PhysicsEngine.cs:61, not ported); the
        // NegModX/NegModY/ScreenClipX/ScreenClipY clamp is NOT ported (no collision/screen-clip system).
        player.AdjustedForceX = player.ForceX;
        player.AdjustedForceY = player.ForceY;
        player.FinalForceX = player.AdjustedForceX;
        player.FinalForceY = player.AdjustedForceY;
        player.FinalForceZ = player.ForceZ;

        // PhysicsEngine.cs:421-422 (position update) - the surrounding collision-retry loop
        // (PhysicsEngine.cs:390-800ish) is NOT ported (no collision system), so this always fully commits
        // FinalForceX/Y in one step, exactly like the original's own first (and, absent collision, only)
        // TRY_ADVANCE iteration would for an entity that never gets blocked.
        player.PosX += player.FinalForceX;
        player.PosY += player.FinalForceY;

        // PhysicsEngine.cs:1698-1700 (same formula EntityRecordMapper/AlundraWorldProxy already use to
        // seed TileX/TileY from PosX/PosY elsewhere) - kept in sync every tick so the record spawn-zone
        // gate (AlundraWorldProxy.ShouldSpawnRecord) and any future opcode reading TileX/TileY see the
        // player's current tile, not a stale spawn-time one.
        player.TileX = (player.PosX >> 16) / TileWidth;
        player.TileY = (player.PosY >> 16) / TileHeight;
    }

    /// <summary>Bit-for-bit port of <c>PhysicsEngine.IncrementForce</c> (PhysicsEngine.cs:1551-1576,
    /// address 0x800367e4).</summary>
    internal static int IncrementForce(int force, int targetForce, int step)
    {
        if (targetForce != force)
        {
            if (force < targetForce)
            {
                force += step;

                if (targetForce < force)
                {
                    return targetForce;
                }
            }
            else
            {
                force -= step;

                if (force < targetForce)
                {
                    return targetForce;
                }
            }
        }

        return force;
    }
}
