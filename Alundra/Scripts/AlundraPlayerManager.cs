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
/// <item><description>LoadingMap(0x36) case, PlayerManager.cs:914-922, ported faithfully: <c>if
/// IsOnGround != 0, break</c> (stay in LoadingMap); the original's ground-contact check reads a stub
/// value (<see cref="AlundraEntityScriptProxy.IsOnGround"/> pinned to 1 for the player until E3's own
/// gravity/collision chantier - see <see cref="MovePlayer"/>'s own doc). The freshly spawned hero's
/// actual LoadingMap -&gt; Idle exit is the animation Chain bridge (<c>AlundraWorldProxy.OnAnimationFinished</c>,
/// 2026-08-23 "Correctif fins d'animation" - anim 54's trailing control frame chains to anim 0/Idle),
/// not this method - matching the original, where the same trailing-control-frame-driven
/// <c>UpdateAnimation</c> recursion is what actually leaves LoadingMap, not
/// <c>MovePlayer</c>.</description></item>
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

        // LoadingMap(0x36) case, PlayerManager.cs:914-922, ported faithfully: "if IsOnGround != 0, break"
        // (stay in LoadingMap) else switch to Jump (NOT ported - no jump animation/physics in V1, see this
        // class' own doc). AlundraWorldProxy.AdoptPlayerPawn pins IsOnGround = 1 for the player until E3
        // (no gravity/collision yet), so this always takes the "stay" branch - the hero's actual
        // LoadingMap -> Idle exit is the animation Chain bridge instead (anim 54's trailing control frame
        // chains to anim 0/Idle - see AlundraWorldProxy.OnAnimationFinished), matching the original's own
        // trailing-control-frame-driven animation switch rather than this ground check.
        if (player.TargetAnimationId == LoadingMapAnimationId && player.IsOnGround == 0)
        {
            // PlayerManager.cs:919-920 (goto LAB_80032604): the original branches to the Jump animation
            // here. Not ported (no jump animation/physics in V1) - documented no-op rather than a fake
            // jump, same convention as every other unported TargetAnimationId case in this class.
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
    /// exactly what a collision-free, gravity-free player needs (see this class' own doc, D4/E2).
    /// Delegates to <see cref="AlundraScriptedMotion.TickPlayer"/> (E4.b extraction,
    /// docs/plan-e4-deplacement-scripte.md - see that class' own doc): bit-for-bit the same accumulator/
    /// tick body this method used to run inline, now shared with the E4.b scripted-NPC mover.
    /// </summary>
    public static void Tick(AlundraEntityScriptProxy player, float elapsedTime)
        => AlundraScriptedMotion.TickPlayer(player, elapsedTime);
}
