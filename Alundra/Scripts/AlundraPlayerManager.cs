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
/// <item><description>The slope switch's case 4 (water/swimming) and every case other than 6 (including
/// default) - PlayerManager.cs:207-353 - still NOT ported (no swimming system; map 389 has no water cell,
/// docs/plan-echelles-chiffrage.md, decision "cas 4 differe"). Case 6 (climbing wall) IS now ported (É4,
/// see <see cref="MovePlayer"/>'s own body) - the ONLY slope case that sets <c>TargetAnimationId</c>,
/// unconditionally to <see cref="ClimbingAnimationId"/> whenever its own 5-conjunct gate holds, regardless
/// of the current animation. <see cref="AlundraEntityScriptProxy.Slope_18c"/> itself has been alimented
/// since E1 (<see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>) from the real map 389 cell data
/// every frame, reading 6 on the four ladder cells - see that method's own doc.</description></item>
/// <item><description><c>UpdatePlayerWeaponEffect</c>/<c>UpdateWeaponStepProgression</c>/
/// <c>UpdatePlayerCarriedEntity</c> - PlayerManager.cs:355-357 (no weapon/carry system).</description></item>
/// <item><description>Every <c>TargetAnimationId</c> case other than Idle/Moving/LoadingMap/Climbing/
/// ClimbStill - jump, sprint, attack (all weapon types), pickup/carry/throw, swimming, sand, spell cast,
/// damage-taken, victory pose, etc. - PlayerManager.cs:385-943. An entity whose <c>TargetAnimationId</c>
/// is none of the five ported values simply keeps it unchanged (no case matches, same shape as the
/// original's own <c>default: AnimateWarpEffect(); break;</c>, itself not ported since it is a pure
/// visual-effect call). Within the Climbing/ClimbStill case itself, the original's own lateral-input exit
/// to Jump (PlayerManager.cs:681-683) is likewise not ported (no jump animation/physics in V1) - this
/// port's own documented deviation sends that exit to Idle instead (user decision, 2026-08-26).</description></item>
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

    /// <summary>PlayerAnimation.Climbing = 0x0E (PlayerAnimation.cs, address comment - see
    /// docs/plan-echelles-chiffrage.md É4): the hero is actively moving up/down a ladder wall this
    /// frame. Internal (not private): <see cref="AlundraScriptedMotion.TickPlayer"/> needs it too, to gate
    /// its own per-TICK vertical step - see that method's own doc.</summary>
    internal const uint ClimbingAnimationId = 0x0E;

    /// <summary>PlayerAnimation.ClimbStill = 0x35 (PlayerAnimation.cs - see
    /// docs/plan-echelles-chiffrage.md É4): the hero is on the ladder wall, pad released, holding position
    /// (frozen, no vertical step this tick). Internal for the same reason as
    /// <see cref="ClimbingAnimationId"/>.</summary>
    internal const uint ClimbStillAnimationId = 0x35;

    /// <summary>
    /// Decision E4-3 (docs/plan-e4-deplacement-scripte.md): name of the debug-only environment variable
    /// that, set to exactly "1" in the process environment, disables <see cref="MovePlayer"/>'s
    /// <c>InputBlockedMask</c> gate below - lets a developer drive the hero with the pad at any point in
    /// the intro (including while script-locked by opcode 0x10) to validate walk/collision without
    /// waiting for 0x11. NEVER active by default - the variable must be explicitly set before this
    /// process starts (or, for tests only, forced via <see cref="SetDebugIgnoreControlLockOverrideForTests"/>).
    /// This is a debug knob only, never toggled by any opcode or game system.
    /// </summary>
    internal const string DebugIgnoreControlLockEnvVar = "ALUNDRA_DEBUG_IGNORE_CONTROL_LOCK";

    /// <summary>Real-world value of the flag: the environment variable read exactly once (static
    /// readonly, evaluated on this type's first use) and logged exactly once when it evaluates active -
    /// see <see cref="DebugIgnoreControlLockEnvVar"/>'s own doc.</summary>
    private static readonly bool DebugIgnoreControlLockFromEnvironment = ReadDebugIgnoreControlLockFromEnvironment();

    /// <summary>Test-only seam over <see cref="DebugIgnoreControlLockFromEnvironment"/>: null (the
    /// default) defers to the real environment-variable read above; a non-null value overrides it. Exists
    /// solely so unit tests can exercise both the "active" and "inactive" branches of
    /// <see cref="MovePlayer"/> deterministically, without depending on this process's environment
    /// variables being set before the CLR first touches this type (which a shared xunit test host cannot
    /// guarantee - some other test may have already forced the static field's one-time initialization).
    /// Never read or written by production code paths.</summary>
    private static bool? _debugIgnoreControlLockOverrideForTests;

    private static bool DebugIgnoreControlLock => _debugIgnoreControlLockOverrideForTests ?? DebugIgnoreControlLockFromEnvironment;

    private static bool ReadDebugIgnoreControlLockFromEnvironment()
    {
        var active = Environment.GetEnvironmentVariable(DebugIgnoreControlLockEnvVar) == "1";

        if (active)
        {
            CasaEngine.Core.Logging.Logs.WriteWarning(
                $"AlundraPlayerManager: {DebugIgnoreControlLockEnvVar}=1 - MovePlayer's InputBlockedMask "
                + "gate is disabled (debug-only, never active by default).");
        }

        return active;
    }

    /// <summary>Test-only seam - see <see cref="_debugIgnoreControlLockOverrideForTests"/>'s own doc.
    /// Pass null to restore the real environment-variable-derived value.</summary>
    internal static void SetDebugIgnoreControlLockOverrideForTests(bool? value)
        => _debugIgnoreControlLockOverrideForTests = value;

    /// <summary>
    /// Port of the documented subset of <c>PlayerManager.MovePlayer</c> - see this class' own doc for the
    /// exact ported/not-ported line ranges. Only sets <see cref="AlundraEntityScriptProxy.TargetDirection"/>/
    /// <see cref="AlundraEntityScriptProxy.TargetAnimationId"/>/<see cref="AlundraEntityScriptProxy.Flags"/>;
    /// the kinematic integration that turns a changed <c>TargetDirection</c>/animation into an actual
    /// position change is <see cref="Tick"/>, called separately (mirrors the original's own split between
    /// <c>PlayerManager.MovePlayer</c> and <c>PhysicsEngine.UpdateEntitiesPhysics</c>).
    /// </summary>
    public static void MovePlayer(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host)
    {
        // E12.d: `host` is REQUIRED at every call site (no default) - null means "no world" and skips
        // CheckEntityInteraction below, a documented degraded mode the ~19 direct movement-only test
        // callers opt into EXPLICITLY with `host: null` (never silently by omission - the
        // green-and-inert family demands the skip be visible at the site). Production passes ScriptHost.

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
        if (!DebugIgnoreControlLock && (state.PlayerControlFlags & AlundraGameState.PlayerControlBits.InputBlockedMask) != 0)
        {
            return;
        }

        // PlayerManager.cs:59-60 (free branch header). CreatePlayerAnimationEffects(0) - not ported (no
        // effect system yet).
        player.Flags |= EntityFlags.Gravity;

        // PlayerManager.cs:61-353 (CheckAndExecuteWarp's warp-facing branch, HP==0 death branch,
        // TileAttributes&0x80 warp-tile branch) - NOT PORTED, see this class' own doc for why each is
        // safe to skip for a fresh New Game / flat-ground V1 scenario.

        // PlayerManager.cs:198-205 (pad -> direction).
        var buttonsHold = pad.ButtonsHold >> 0xc;
        var dir = buttonsHold < AnimationTables.DirectionByButtons.Length
            ? AnimationTables.DirectionByButtons[buttonsHold]
            : uint.MaxValue;

        if (dir == uint.MaxValue)
        {
            dir = player.TargetDirection;
        }

        // Slope switch (PlayerManager.cs:207-353) - ONLY case 6 (climbing wall) ported, per
        // docs/plan-echelles-chiffrage.md É4 (user decision, 2026-08-26). Case 4 (water/swimming) and
        // every other case (including default) are deliberately NOT ported - no swimming system exists,
        // and no other slope case has a documented consumer yet; every one of them falls through as a
        // silent no-op, same convention as every other unported TargetAnimationId case in this class.
        // PlayerManager.cs:341-350's own 5-conjunct gate, verbatim: buttonsHold != 0 (some direction key
        // held - note the RAW pad bitmask before AnimationTables resolution, not "dir != invalid"),
        // dir == 0x10 (resolved direction is up/north THIS frame), TargetDirection == 0x10 (already facing
        // up - guards against a same-frame facing change), ForceAdjusted != 0 (this tick's own horizontal
        // step was curtailed - i.e. walked INTO the ladder wall, this port's equivalent of the original's
        // collision-adjusted-movement signal, see that field's own doc), CarriedEntity == null (not
        // carrying an object). Sets TargetAnimationId = Climbing UNCONDITIONALLY when all five hold,
        // regardless of the CURRENT TargetAnimationId (the original's own cas 6 body does the same - it
        // does not gate on the current animation at all) - the very next statement below (the
        // Climbing/ClimbStill case) then runs THIS SAME frame against the freshly-set animation, exactly
        // like the original's own slope-switch-then-animation-switch order within one MovePlayer call.
        if (player.Slope_18c == 6
            && buttonsHold != 0
            && dir == 0x10
            && player.TargetDirection == 0x10
            && player.ForceAdjusted != 0
            && player.CarriedEntity == null)
        {
            player.TargetAnimationId = ClimbingAnimationId;
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

        // PlayerManager.cs:361-383 (Idle/Moving case) - E12.d ports CheckEntityInteraction into it
        // (TryUseItem/PlayerTryAction/PlayerTryAttack stay unported no-ops, E2's scope): res==2 (button
        // interact) forces Idle and ends the case; res==1 (auto-touch interact) ends it WITHOUT
        // updating the animation this tick; res==0 falls through to the normal animation update -
        // exactly the original's `if (iVar2 != 0) { if (iVar2 != 2) break; ... Idle ... }` shape.
        if (player.TargetAnimationId == IdleAnimationId || player.TargetAnimationId == MovingAnimationId)
        {
            player.TargetDirection = dir;

            var interact = CheckEntityInteraction(player, in pad, state, host);
            if (interact == 2)
            {
                player.TargetAnimationId = IdleAnimationId;
            }
            else if (interact == 0)
            {
                player.TargetAnimationId = buttonsHold != 0 ? MovingAnimationId : IdleAnimationId;
            }
        }
        // Climbing(0x0E)/ClimbStill(0x35) case, PlayerManager.cs:675-731 (docs/plan-echelles-chiffrage.md
        // É4). TryUseItem/PlayerTryAction (:677) NOT PORTED (no item/interaction system, same scope
        // restriction as every other unported call in this class) - proceeds straight to the body.
        else if (player.TargetAnimationId == ClimbingAnimationId || player.TargetAnimationId == ClimbStillAnimationId)
        {
            // PlayerManager.cs:679-684: any held direction OTHER than up/down (i.e. a genuinely LATERAL
            // input - left/right) while climbing. The original goes to Jump here - NOT ported (no jump
            // animation/physics in V1, same restriction as the LoadingMap case above). USER DECISION
            // (2026-08-26, docs/plan-echelles-chiffrage.md): this port's own lateral exit goes to Idle
            // instead - documented deviation, not an oversight. ForceZ is cleared (engine-only addition:
            // the original has no separate "stop climbing" reset because a Jump entry starts its own fresh
            // vertical impulse; this port instead just needs to stop feeding the ladder's per-tick vertical
            // step to Tick, see AlundraScriptedMotion.TickPlayer's own doc).
            if (buttonsHold != 0 && dir != 0 && dir != 0x10)
            {
                player.TargetAnimationId = IdleAnimationId;
                player.ForceZ = 0;
                RestoreGravityAfterClimb(player);
            }
            else
            {
                // PlayerManager.cs:685 (TargetDirection = 0x10) - stays facing up the whole time on the ladder.
                player.TargetDirection = 0x10;

                if (buttonsHold == 0)
                {
                    // PlayerManager.cs:687-691 (pad released): freeze - ClimbStill, no vertical step, and
                    // (engine-only addition - see this method's own doc on SuspendGravityForClimb/
                    // RestoreGravityAfterClimb) gravity STAYS suspended (still on the wall, just not moving).
                    player.TargetAnimationId = ClimbStillAnimationId;
                    player.ForceZ = 0;
                    player.Flags &= ~EntityFlags.Gravity; // PlayerManager.cs:690 - see this class' own
                    // doc on the Gravity FLAG BIT (distinct from the engine's own
                    // Controller.Settings.Gravity, suspended below). CORRECTED (verifier's own minor
                    // observation): this bit IS read for the player -
                    // AlundraEntityScriptProxy.UpdateGroundSlope's own gravity gate
                    // (`if ((Flags & EntityFlags.Gravity) == 0) { Slope_18c = 0; return; }`), and that
                    // method only ever runs for IsPlayer (see AlundraEntityScriptProxy.Update's own
                    // IsPlayer branch) - so clearing this bit here forces Slope_18c to 0 for the whole
                    // climb, matching the original's own "clear the bit before UpdateTileAttributes" order,
                    // but also means MovePlayer's own Slope_18c == 6 re-entry gate needs one extra frame
                    // to see Slope_18c == 6 again after a lateral exit re-sets the bit.
                    SuspendGravityForClimb(player);
                }
                else if (dir == 0) // PlayerManager.cs:694-701 (DESC - held Down).
                {
                    // LESSON 1 re-derivation (docs/plan-echelles-chiffrage.md, established facts §2): the
                    // original's own condition is "FloorHeight + 1 < PosZ" against ITS OWN resting
                    // invariant (ModdedPosZ == TerrainHeight + 1, PhysicsEngine.cs:186/:128). Re-derived
                    // for THIS port's own two resting invariants (terrain: PosZ == TerrainHeight, no +1;
                    // entity-support: PosZ == candidateTop + 1, EntitySupport.cs:173) - both make the SAME
                    // literal condition FALSE at rest (terrain: TerrainHeight+1 < TerrainHeight is false;
                    // platform: (candidateTop+1)+1 < candidateTop+1 is false) - see UpdateFloorHeight's own
                    // doc for why FloorHeight already carries the correct convention for each case, so no
                    // further +1/-1 adjustment belongs HERE. Ported verbatim, unmodified.
                    if (player.FloorHeight + 1 < player.PosZ)
                    {
                        player.ForceZ = -0x10000;
                        player.TargetAnimationId = ClimbingAnimationId;
                        player.Flags &= ~EntityFlags.Gravity; // parity only - see the ClimbStill branch's own note.
                        SuspendGravityForClimb(player);
                    }
                    // else: PlayerManager.cs:701's own fallthrough to LAB_80031e7c/:729 (reached bottom of
                    // the climbable descent). CORRECTED (verifier F3): the original DOES reassign there -
                    // both guard failures (DESC and MONT) fall to the SAME ":729 TargetAnimationId = Idle",
                    // without clearing the Gravity flag bit, so the original hero resumes falling under its
                    // own engine's gravity. This port's equivalent of "resume falling" is
                    // RestoreGravityAfterClimb (undoes SuspendGravityForClimb's zeroing of
                    // Controller.Settings.Gravity/MaxFallSpeed AND its IsVerticalOwnedExternally latch, see
                    // that method's own doc) - without it the hero was left in Idle with gravity/max fall
                    // speed pinned at 0 forever (AnimSets[14]/[53].Speed == 0 for that animation pair too,
                    // so the hero could not even walk away). ForceZ = 0 is this port's own engine-only
                    // addition, same rationale as the Climbing/MONT entry above: stop feeding a stale
                    // per-tick vertical step to AlundraScriptedMotion.TickPlayer now that this is no longer
                    // a climbing animation.
                    else
                    {
                        player.TargetAnimationId = IdleAnimationId;
                        player.ForceZ = 0;
                        RestoreGravityAfterClimb(player);
                    }
                }
                else if (dir == 0x10) // PlayerManager.cs:704-712 (MONT - held Up).
                {
                    // PlayerManager.cs:718-719, verbatim - the ladder guard É3 built for exactly this call.
                    var tileHeightAbove = player.GetTileHeightAtOffset(0, -0x10000);
                    if (player.PosZ <= tileHeightAbove)
                    {
                        player.ForceZ = 0x10000;
                        player.TargetAnimationId = ClimbingAnimationId;
                        player.Flags &= ~EntityFlags.Gravity; // parity only - see the ClimbStill branch's own note.
                        SuspendGravityForClimb(player);
                    }
                    else
                    {
                        // Reached the top of the climbable tile - same ":729 exit to Idle + gravity
                        // restore" fix as the DESC branch's own "else" above (verifier F3).
                        player.TargetAnimationId = IdleAnimationId;
                        player.ForceZ = 0;
                        RestoreGravityAfterClimb(player);
                    }
                }
                // dir cannot be anything else here: the lateral-exit check above already filtered out
                // every value except 0 (down) and 0x10 (up), matching the original's own two-armed
                // if/else-if (no third branch in PlayerManager.cs either).
            }
        }

        // PlayerManager.cs:385-943 (every other TargetAnimationId case) - NOT PORTED; an entity whose
        // TargetAnimationId is neither of the three ported values above keeps it unchanged.

        // PlayerManager.cs:947-950 (END: UpdateItemEffectState, SetPlayerHpMax/SetPlayerHp) - NOT PORTED.
    }

    /// <summary>
    /// Engine-only addition (docs/plan-echelles-chiffrage.md É4, no equivalent original call): the
    /// original's own vertical-hold-while-climbing is the Gravity FLAG BIT
    /// (<c>entity.Flags &amp; EntityFlags.Gravity</c>, read directly by <c>PhysicsEngine.ComputeZPosition</c>
    /// every original tick). THIS port's hero has no such bridge - the player's own vertical is owned by
    /// the ENGINE's continuous <c>CharacterControllerComponent.Settings.Gravity</c>/<c>MaxFallSpeed</c>
    /// integrator instead (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>'s own override block, E3.d) -
    /// so suspending gravity here means zeroing THOSE, exactly the same
    /// zero-Gravity/zero-MaxFallSpeed shape <see cref="AlundraEntityScriptProxy.ApplyGravitySettingsToController"/>
    /// already uses for every controller-driven scripted NPC (proven pattern, not a new one - see that
    /// method's own doc). Idempotent (safe to call every frame the hero is actively on the ladder, same as
    /// the original's own per-frame "Flags &amp;= ~Gravity" re-application). A no-op without a
    /// <see cref="AlundraEntityScriptProxy.Controller"/> (E2's own controller-free fallback player, which
    /// has no engine-driven vertical to suspend in the first place).
    /// <para>
    /// CORRECTED (verifier F1/F2): zeroing Settings.Gravity/MaxFallSpeed alone is NOT enough - the
    /// engine's own <c>CharacterControllerComponent.UpdateGround</c> still re-snaps the root to the
    /// ground field every RENDERED frame regardless of those two settings (it runs before gravity
    /// integration, off the same per-frame ground field probe a stationary/walking entity needs), which
    /// silently cancelled every ladder-tick's 1px rise the instant the next frame's
    /// <c>CharacterControllerComponent.Update</c> ran - the hero oscillated at the bottom rung and never
    /// actually climbed. This is exactly the case
    /// <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent.IsVerticalOwnedExternally"/>
    /// exists for (its own doc: "lets a scripted-motion owner ... declare vertical displacement without
    /// the controller's per-render-frame gravity fighting a per-logic-tick value") - already the exact
    /// contract every scripted NPC uses (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>'s NPC sibling,
    /// <c>ApplySpawnInitialization</c>, sets it permanently; <see cref="AlundraEntityScriptProxy.Update"/>
    /// declares <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent.SetExternalVerticalDisplacement"/>
    /// every tick). The hero's own controller (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>) never sets
    /// this flag, so outside a climb the hero keeps its normal engine-driven gravity/ground-snap
    /// unchanged - this method only claims the flag for the DURATION of a climb, and
    /// <see cref="RestoreGravityAfterClimb"/> hands it back. No engine change: the mechanism already
    /// existed, unused by the hero.
    /// </para>
    /// </summary>
    private static void SuspendGravityForClimb(AlundraEntityScriptProxy player)
    {
        if (player.Controller == null)
        {
            return;
        }

        player.Controller.Settings.Gravity = 0f;
        player.Controller.Settings.MaxFallSpeed = 0f;
        player.Controller.IsVerticalOwnedExternally = true;
    }

    /// <summary>
    /// See <see cref="SuspendGravityForClimb"/>'s own doc - the matching restore, called from every path
    /// this port's own Climbing/ClimbStill case leaves those two animations from: the lateral-exit branch
    /// (PlayerManager.cs:679-684) and, since verifier F3, the DESC/MONT boundary-guard exits
    /// (PlayerManager.cs:697-730 - both guard failures fall to the SAME ":729 TargetAnimationId = Idle",
    /// see <see cref="MovePlayer"/>'s own updated doc on that call site). Restores
    /// <c>Controller.Settings.Gravity</c>/<c>Controller.Settings.MaxFallSpeed</c> from
    /// <see cref="AlundraEntityScriptProxy.MapGravity"/>/<see cref="AlundraEntityScriptProxy.MapMaxFallSpeed"/> -
    /// the RESERVE this same slice's own <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> fix now populates
    /// for the hero (previously left at the C# default 0f - see that method's own E4 comment; without that
    /// fix, "restoring" would have restored the hero's own gravity to zero forever, silently breaking
    /// every fall after the hero's first climb) - AND hands
    /// <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent.IsVerticalOwnedExternally"/>
    /// back to <c>false</c> (verifier F1/F2's own fix, see <see cref="SuspendGravityForClimb"/>'s own doc):
    /// without this, the hero would keep skipping the engine's own per-frame ground snap/gravity
    /// integration forever after its first climb, exactly like a climb that never actually ended. A no-op
    /// without a <see cref="AlundraEntityScriptProxy.Controller"/>, same guard as
    /// <see cref="SuspendGravityForClimb"/>.
    /// </summary>
    private static void RestoreGravityAfterClimb(AlundraEntityScriptProxy player)
    {
        if (player.Controller == null)
        {
            return;
        }

        player.Controller.Settings.Gravity = player.MapGravity;
        player.Controller.Settings.MaxFallSpeed = player.MapMaxFallSpeed;
        player.Controller.IsVerticalOwnedExternally = false;
    }

    /// <summary>
    /// Runs <paramref name="ticks"/> whole 50 Hz kinematic ticks - port of
    /// <c>PhysicsEngine.UpdateEntityPhysics</c> (PhysicsEngine.cs:1579-1597), the <c>IncrementForce</c>
    /// calls (PhysicsEngine.cs:1445-1446/1490-1491), and the flat-ground half of <c>ApplyEntityForces</c>
    /// (PhysicsEngine.cs:1514-1547) plus the position update (PhysicsEngine.cs:421-422) - restricted to
    /// exactly what a collision-free, gravity-free player needs (see this class' own doc, D4/E2).
    /// Delegates to <see cref="AlundraScriptedMotion.TickPlayer"/> (E4.b extraction,
    /// docs/plan-e4-deplacement-scripte.md - see that class' own doc), now driven by the SAME
    /// <c>ticksThisFrame</c> the shared <see cref="AlundraLogicClock"/> hands the script/pick-run pass this
    /// frame (ONE-CLOCK fix, see <see cref="AlundraScriptedMotion"/>'s own class doc) instead of its own
    /// separately-accumulated elapsed time - the hero's own observable per-tick behaviour is unchanged, only
    /// the source of the tick count.
    /// </summary>
    /// <summary>
    /// Port of <c>CheckEntityInteraction</c> @ 0x8002e910 (decompilation PlayerManager.cs:1597-1669),
    /// E12.d (docs/plan-e12d-interaction-joueur.md). Reads the player's per-tick entity contact
    /// (<see cref="AlundraEntityScriptProxy.XCollisionEntity"/>, written by AlundraWorldProxy.Update's
    /// contact pass), maintains the interact latch on <paramref name="state"/> (see the latch fields'
    /// own doc there), and on success assigns <see cref="IAlundraScriptHost.ActiveCollisionEntity"/> -
    /// the one-shot signal the slot-F pick consumes (D-E12D-4). Returns the original's exact result
    /// codes: 0 = no interaction, 1 = auto-touch interact (no InteractRequiresButton flag),
    /// 2 = button interact (flag + Square just pressed).
    ///
    /// Two deliberate, documented gates the original carries UPSTREAM instead:
    /// <paramref name="host"/> null = "no world" - skipped (degraded; see MovePlayer's own doc);
    /// GameplayBlockedMask posed - skipped, the narrowest equivalent of the original's whole-pipeline
    /// gate at EntityManager.cs:377 (with a MenuOpen box up, neither MovePlayer nor the pick nor the
    /// physics ran at all there; our pipeline has no such global gate - E4.c only ported the map-events
    /// one - so the interact computation carries it itself, D-E12D-5).
    /// </summary>
    internal static int CheckEntityInteraction(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host)
    {
        if (host == null)
        {
            return 0;
        }

        if ((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0)
        {
            return 0;
        }

        // PlayerManager.cs:1603-1643 - candidate resolution, latch included, ported branch for branch.
        var collidedEntity = player.XCollisionEntity;
        var reachedFinalCheck = false;

        if (player.XCollisionEntity == null)
        {
            if (state.InteractLatchEntity == null ||
                (state.InteractLatchEntity.Index2 == state.InteractLatchFacing &&
                 state.InteractLatchEntity.PosX == state.InteractLatchEntityX &&
                 state.InteractLatchEntity.PosY == state.InteractLatchEntityY &&
                 state.InteractLatchEntity.PosZ == state.InteractLatchEntityZ &&
                 player.PosX == state.InteractLatchPlayerX &&
                 player.PosY == state.InteractLatchPlayerY &&
                 player.PosZ == state.InteractLatchPlayerZ))
            {
                collidedEntity = state.InteractLatchEntity;

                if (player.TargetDirection == state.InteractLatchDirection)
                {
                    reachedFinalCheck = true; // goto FinalCheck (:1624-1626).
                }
            }
        }
        else if ((player.XCollisionEntity.Flags & EntityFlags.InteractRequiresButton) != 0)
        {
            state.InteractLatchFacing = player.XCollisionEntity.Index2;
            state.InteractLatchEntity = player.XCollisionEntity;
            state.InteractLatchEntityX = player.XCollisionEntity.PosX;
            state.InteractLatchEntityY = player.XCollisionEntity.PosY;
            state.InteractLatchEntityZ = player.XCollisionEntity.PosZ;
            state.InteractLatchPlayerX = player.PosX;
            state.InteractLatchPlayerY = player.PosY;
            state.InteractLatchPlayerZ = player.PosZ;
            state.InteractLatchDirection = player.TargetDirection;
            reachedFinalCheck = true; // goto FinalCheck (:1640).
        }

        if (!reachedFinalCheck)
        {
            // :1642-1643 - the latch invalidation fall-through.
            state.InteractLatchEntity = null;
            collidedEntity = player.XCollisionEntity;
        }

        // FinalCheck (:1645-1667).
        var res = 0;

        if (collidedEntity != null &&
            (collidedEntity.ProgramIndexes[ScriptHelper.ProgramFInteract] != 0
             || collidedEntity.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] != 0))
        {
            if ((collidedEntity.Flags & EntityFlags.InteractRequiresButton) == 0)
            {
                res = 1;
                host.ActiveCollisionEntity = collidedEntity;
            }
            else if ((pad.ButtonsJustPressed & AlundraPadState.Square) == 0)
            {
                res = 0;
            }
            else
            {
                res = 2;
                host.ActiveCollisionEntity = collidedEntity;
            }
        }

        return res;
    }

    public static void Tick(AlundraEntityScriptProxy player, int ticks)
        => AlundraScriptedMotion.TickPlayer(player, ticks);
}
