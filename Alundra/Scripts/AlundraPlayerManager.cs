#nullable enable
using System;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai;

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
/// <item><description>Idle(0x00)/Moving(0x01) case, PlayerManager.cs:363-383 (binary <c>0x80031E38</c>): <c>TargetDirection = dir</c>, then (E20.a A4) <c>TryUseItem</c>, <c>PlayerTryAction</c>
/// and <c>CheckEntityInteraction</c> in the binary's order, then the common tail: <c>PlayerTryAttack</c> (E20.b), the jump of the Cross, <c>buttonsHold != 0 -&gt; Moving</c> else <c>Idle</c>.
/// <c>TryUseItem</c> and <c>PlayerTryAction</c> are named stubs (E20.h and E20.g: the use of an item and lifting/carrying are not ported yet).</description></item>
/// <item><description>The attack groups, binary <c>0x80031E18</c> and <c>0x80031DF8</c> (E20.b): a swing in progress only keeps its side of the ground (see <see cref="MovePlayer"/>).</description></item>
/// <item><description>The run (E20.d1), binary <c>0x80031F54</c>, <c>0x80031FF4</c>, <c>0x80032118</c> and <c>0x80032558</c>: the preparation (0x29, entered from the common tail by a held Triangle), the run (0x03), the
/// slide that ends it (0x2A) and the bounce (0x3E, and 0x1C which shares its handler); the tackle (0x04) is of the ground attack group.</description></item>
/// <item><description>The swim (E20.i), binary <c>0x8003260C</c>: on deep water (slope 4) <see cref="SwimTick"/> replaces everything between the pad and the end of the tick: the 80-entry table <c>0x80022E7C</c> in six kinds of handler (<see cref="SwimTable"/>), the floating (0x1D), the swimming (0x0F), the burst on the edge of the Triangle, the Cross or the Square (0x28), the end of the hurt in water (0x3A, 0x3B, entered by E20.c2b); the exits to the land are cases of the state chain. NOT ported: the wall of the deep water without the swim boots (O-E19-22), the currents (O-E20-16), the splash, the wake and the ripple (E20.e).</description></item>
/// <item><description>LoadingMap(0x36) case, binary 0x800325E8 (PlayerManager.cs:914-922): on the ground the
/// case ends and the hero stays LoadingMap - its exit is then the animation Chain bridge
/// (<c>AlundraWorldProxy.OnAnimationFinished</c>, 2026-08-23 "Correctif fins d'animation" - anim 54's trailing
/// control frame chains to anim 0/Idle), matching the original, where the same trailing-control-frame-driven
/// <c>UpdateAnimation</c> recursion leaves LoadingMap; in the air (E19.v, O-E19-32) it gives 0x2D and leaves the
/// switch.</description></item>
/// </list>
///
/// NOT ported (out of E2's own scope, unconditionally, regardless of <c>TargetAnimationId</c>):
/// <list type="bullet">
/// <item><description><c>g_activeCollisionEntity = null</c>; <c>g_currentWeaponFlags</c> - PlayerManager.cs:23-27,61-79 (the weapon record is resolved at the head of <see cref="MovePlayer"/> since
/// E20.b, from <c>GetItemIdFromCurrentWeapon</c>, but the flags the later weapon systems read are not ported). <c>CheckAndExecuteWarp</c>'s
/// TRIGGER half (PlayerManager.cs:29,3424-3459) IS now ported (T3, docs/plan-transitions-carte.md §1.2.a) -
/// see this method's own T3 comment at its call site; its TRANSFER half (<c>HandleWarpTransition</c>,
/// PlayerManager.cs:3488-3541) is NOT ported (T4's own scope).</description></item>
/// <item><description>HP==0 death branch - PlayerManager.cs:82-170 (no HP system wired to the player
/// yet - <see cref="AlundraEntityScriptProxy.Hp"/> stays the C# default 0, which would otherwise
/// immediately hit this branch every frame; skipping it entirely is the deliberate V1 choice, not an
/// oversight).</description></item>
/// <item><description><c>TileAttributes &amp; 0x80</c> warp-tile branch - PlayerManager.cs:172-196 (no
/// tile-attribute sampling yet, <see cref="AlundraEntityScriptProxy.TileAttributes"/> stays 0).</description></item>
/// <item><description>Every slope-switch case other than 4 (the swim, E20.i) and 6 (including
/// default) - PlayerManager.cs:207-353 - still NOT ported. Case 6 (climbing wall) IS now ported (É4,
/// see <see cref="MovePlayer"/>'s own body) - the ONLY slope case that sets <c>TargetAnimationId</c>,
/// unconditionally to <see cref="ClimbingAnimationId"/> whenever its own 5-conjunct gate holds, regardless
/// of the current animation. <see cref="AlundraEntityScriptProxy.Slope_18c"/> itself has been alimented
/// since E1 (<see cref="AlundraEntityScriptProxy.UpdateGroundSlope"/>) from the real map 389 cell data
/// every frame, reading 6 on the four ladder cells - see that method's own doc.</description></item>
/// <item><description><c>UpdatePlayerCarriedEntity</c> - PlayerManager.cs:355-357 (named stub: carrying is E20.g; <c>UpdatePlayerWeaponEffect</c> is ported by E20.f1a and E20.f1b, the aura included);
/// <c>UpdateWeaponStepProgression</c> is ported without its projectile spawn (E20.b B4, the spawn is a named stub, E20.e0/E20.f).</description></item>
/// <item><description>Every <c>TargetAnimationId</c> case other than Idle/Moving/LoadingMap/Climbing/
/// ClimbStill, the jump states, the attack groups and the run - the charged attacks, pickup/carry/throw, sand, spell cast,
/// damage-taken, victory pose, etc. - PlayerManager.cs:385-943. An entity whose <c>TargetAnimationId</c>
/// is none of the five ported values simply keeps it unchanged (no case matches, same shape as the
/// original's own <c>default: AnimateWarpEffect(); break;</c>, itself not ported since it is a pure
/// visual-effect call). Within the Climbing/ClimbStill case itself, the original's own lateral-input exit
/// to Jump (PlayerManager.cs:681-683) is likewise not ported (no jump animation/physics in V1) - this
/// port's own documented deviation sends that exit to Idle instead (user decision, 2026-08-26).</description></item>
/// <item><description><c>END</c>: <c>UpdateItemEffectState</c>, <c>SetPlayerHpMax</c>/<c>SetPlayerHp</c> -
/// PlayerManager.cs:947-950, ported by E20.c2a on every exit (<see cref="EndOfMovePlayer"/>).</description></item>
/// </list>
/// </summary>
public static class AlundraPlayerManager
{
    /// <summary>PlayerAnimation.Idle = 0x00 (alundra-datas-analyser .../Gameplay/PlayerAnimation.cs:5).</summary>
    private const uint IdleAnimationId = 0x00;

    /// <summary>PlayerAnimation.Moving = 0x01 (PlayerAnimation.cs:6).</summary>
    private const uint MovingAnimationId = 0x01;

    /// <summary>PlayerAnimation.JumpWalking = 2 (E19.d2c2 S1): the take-off of the Cross edge with a direction held.</summary>
    private const uint JumpWalkingAnimationId = 0x02;

    /// <summary>PlayerAnimation 0x2B (E19.d2c2 S1): the take-off of the Cross edge without a direction.</summary>
    private const uint JumpStandingAnimationId = 0x2B;

    /// <summary>PlayerAnimation 0x2C (E19.d2c2 S1): in the air with a direction held.</summary>
    private const uint AirMovingAnimationId = 0x2C;

    /// <summary>PlayerAnimation 0x2D (E19.d2c2 S1): in the air without a direction.</summary>
    private const uint AirStillAnimationId = 0x2D;

    /// <summary>The bit of <see cref="AlundraEntityScriptProxy.CombinedVramFlagsOR"/> (a ground property of 0x40) on which the Cross does not jump.</summary>
    private const uint NoJumpVramFlag = 0x4000;

    /// <summary>PlayerAnimation 0x03 (E20.d1 T1): the run itself (the sprint), 258048 units of force per tick once the speed is up.</summary>
    internal const uint SprintAnimationId = 0x03;

    /// <summary>PlayerAnimation 0x04 (E20.d1 T1): the tackle, entered from the run by the Square, the Cross or the Circle; its tick is the ground attack group's (<see cref="IsGroundAttackGroup"/>).</summary>
    internal const uint TackleAnimationId = 0x04;

    /// <summary>PlayerAnimation 0x29 (E20.d1 T1): the preparation of the run (25 ticks), entered from the common tail by a held Triangle.</summary>
    internal const uint PrepareSprintAnimationId = 0x29;

    /// <summary>PlayerAnimation 0x2A (E20.d1 T1): the slide that ends the run (16 ticks), in which a direction resumes the run from the eleventh tick.</summary>
    internal const uint StopSprintAnimationId = 0x2A;

    /// <summary>PlayerAnimation 0x3E (E20.d1 T1): the bounce against an obstacle at run speed; it shares its tick with <see cref="HurtBounceAnimationId"/> (<c>0x80032558</c>).</summary>
    internal const uint SprintBounceAnimationId = 0x3E;

    /// <summary>PlayerAnimation 0x1C (E20.d1 T1): the recoil after a hit (reached from the hurt state 0x31, E20.c2b); the jump table <c>0x80022D3C</c> sends it to the handler of <see cref="SprintBounceAnimationId"/>.</summary>
    internal const uint HurtBounceAnimationId = 0x1C;

    /// <summary>The slope value of deep water (<c>Slope_18c</c> 4): <see cref="MovePlayer"/> takes the swim branch there (<c>0x8003260C</c>, E20.i).</summary>
    private const int DeepWaterSlope = 4;

    /// <summary>PlayerAnimation 0x0F (E20.i T1): swimming, on the surface of deep water (speed 144, 42 ticks, chained to itself, sound 22).</summary>
    internal const uint SwimAnimationId = 0x0F;

    /// <summary>PlayerAnimation 0x1D (E20.i T1): floating, the rest of the swim and the state the swim table gives to almost every other state (a 36-tick loop, no speed, deceleration shift 3).</summary>
    internal const uint FloatAnimationId = 0x1D;

    /// <summary>PlayerAnimation 0x28 (E20.i T1): the burst of the swim, a "dive" that stays on the surface (speed 176, 42 ticks, sound 21, then 0x1D); the Triangle, Cross or Square edge starts it.</summary>
    internal const uint DiveAnimationId = 0x28;

    /// <summary>PlayerAnimation 0x3A (E20.i T1): the hurt state in deep water (4 ticks, sound 23, then 0x3B); entered by the hurt block of E20.c2b, which writes 0x3A instead of 0x31 on slope 4.</summary>
    internal const uint SwimHurtAnimationId = 0x3A;

    /// <summary>PlayerAnimation 0x3B (E20.i T1): the end of the hurt in deep water (8 ticks, held until the animation sets <c>+0xAC</c>), left by the handler <c>0x800326FC</c>.</summary>
    internal const uint SwimHurtEndAnimationId = 0x3B;

    /// <summary>The buttons that start the burst of the swim, <c>pressed &amp; 0xD0</c> (<c>0x800326D0</c>): the Triangle (0x10), the Cross (0x40) and the Square (0x80); the Circle (0x20) is the item button.</summary>
    internal const uint SwimDiveButtons = AlundraPadState.Triangle | AlundraPadState.Cross | AlundraPadState.Square;

    /// <summary>The six kinds of handler the swim table <c>0x80022E7C</c> gives to the 80 states (E20.i T1); the address of each handler is in the summary of its member.</summary>
    internal enum SwimHandler : byte
    {
        /// <summary><c>0x80032674</c>: the state becomes 0x1D, then <c>TryUseItem</c>; the direction is not written. Every state not cited below, Idle, the jumps and the attacks included.</summary>
        Float = 0,

        /// <summary><c>0x80032658</c> (0x1C, 0x31, 0x3E): the direction + 0x10, then 0x1D, then <c>TryUseItem</c>.</summary>
        FlipThenFloat,

        /// <summary><c>0x80032680</c> (0x28, 0x3A): <c>TryUseItem</c> only.</summary>
        ItemOnly,

        /// <summary><c>0x80032690</c> (0x0F, 0x1D): the steering of the swim.</summary>
        Steer,

        /// <summary><c>0x800326FC</c> (0x3B): the end of the hurt.</summary>
        HurtEnd,

        /// <summary><c>0x80032758</c> (15 states and every state from 0x50): the item mode is cancelled (<c>0x800350C0</c>), the state stays.</summary>
        Skip,
    }

    /// <summary>E20.i T1: the swim table <c>0x80022E7C</c>, 80 entries indexed by <c>TargetAnimationId</c> (a state from 0x50 on is <see cref="SwimHandler.Skip"/>, <c>0x8003261C</c>).</summary>
    internal static readonly SwimHandler[] SwimTable = BuildSwimTable();

    private static SwimHandler[] BuildSwimTable()
    {
        var table = new SwimHandler[0x50]; // every entry starts as SwimHandler.Float (0)
        foreach (var id in new[] { 0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D })
        {
            table[id] = SwimHandler.Skip;
        }

        table[0x1C] = table[0x31] = table[0x3E] = SwimHandler.FlipThenFloat;
        table[0x28] = table[0x3A] = SwimHandler.ItemOnly;
        table[0x0F] = table[0x1D] = SwimHandler.Steer;
        table[0x3B] = SwimHandler.HurtEnd;
        return table;
    }

    /// <summary>E20.i T1: the handler of the swim table for <paramref name="animationId"/> (<see cref="SwimHandler.Skip"/> from 0x50 on).</summary>
    internal static SwimHandler SwimHandlerOf(uint animationId) => animationId < SwimTable.Length ? SwimTable[animationId] : SwimHandler.Skip;

    /// <summary>
    /// E20.d1 T1: the facing (<see cref="AlundraEntityScriptProxy.AnimationDirection"/>: 0 down, 1 up, 2 left, 3 right) the binary has when <c>MovePlayer</c> of this tick starts: <c>UpdateAnimation</c> (<c>0x80038AB4</c>) rewrites
    /// <c>+0x98</c> at every tick from the previous facing and the direction (<c>+0x8C</c>) <c>MovePlayer</c> left, through the table <c>0x800237F4</c> (<c>0x80038AD8</c>). The DLL writes the field only when a frame ends
    /// (<see cref="AlundraFrameSyncPasses.SyncAnimation"/>), so in a frame of two ticks the second one would read the facing of the frame before. The value is the one the binary would have posed, the table lookup of
    /// <see cref="AlundraFrameSyncPasses.TryResolveAnimationTarget"/>; it is exact with one and two ticks per frame, and for any number of ticks in <c>0x03</c>, whose direction is a cardinal one (the table is idempotent on
    /// a cardinal direction). Computed once at the head of <see cref="MovePlayer"/>, before the tick writes anything; this is the binary's own value, not a deviation.
    /// </summary>
    internal static int TickFacing(AlundraEntityScriptProxy player)
    {
        AlundraFrameSyncPasses.TryResolveAnimationTarget(player, out _, out var direction);
        return direction;
    }

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

    /// <summary>E20.a A8 test-only seam: the number of calls of <see cref="MovePlayer"/> (the structure tests of the tick count them: one per logic tick, none in a frame without tick). Per thread, so tests running in parallel do not count each other.</summary>
    [ThreadStatic]
    internal static int MovePlayerCallCountForTests;

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
        MovePlayerCallCountForTests++;

        // E20.b B1, binary 0x80031B70-0x80031B8C (the very head of MovePlayer): the weapon record of the equipped weapon, byte[0x80028C14 + GetItemIdFromCurrentWeapon()]. Without item tables (a host that has none)
        // the hero has no weapon, which the binary reads the same way: item -1 gives byte[0x80028C13] = 0, the record of the basic sword.
        player.CurrentWeaponRecord = AlundraWeaponTables.RecordIndexOfItem(
            host?.ItemTables is { } itemTables ? GetItemIdFromCurrentWeapon(state, itemTables) : NoItem);

        // E20.d1 T1: the facing the binary has at the head of this tick (read by the cone of the run), computed before the tick writes anything.
        var tickFacing = TickFacing(player);

        // E12.d: `host` is REQUIRED at every call site (no default) - null means "no world" and skips
        // CheckEntityInteraction below, a documented degraded mode the ~19 direct movement-only test
        // callers opt into EXPLICITLY with `host: null` (never silently by omission - the
        // green-and-inert family demands the skip be visible at the site). Production passes ScriptHost.

        // T3 (docs/plan-transitions-carte.md §1.2.a/§3): the ORIGINAL calls CheckAndExecuteWarp at
        // PlayerManager.cs:29 - BEFORE the BlockedByEntity return (:31) and BEFORE the InputBlockedMask
        // gate (:38) below. This is that exact call site: placing it any later would make the "hole"
        // branch unreachable the instant any InputBlockedMask bit is posed (a warp-departure/forced-
        // sequence lock in particular), which is NOT what the original does. DETECTION ONLY (T3's whole
        // scope) - AlundraPortalTrigger.TryGetTrigger is pure, and IAlundraScriptHost.OnPortalTriggerDetected's
        // default implementation is a no-op, so this call starts no fade and requests no world change yet;
        // it only reports a firing trigger through the seam T4's AlundraWarpDirector will fill in. A null
        // host (the same degraded mode CheckEntityInteraction below documents) skips this entirely - no
        // portal list to scan without a world.
        if (host != null)
        {
            var trigger = AlundraPortalTrigger.TryGetTrigger(player, in pad, state, host.Portals);
            if (trigger != null)
            {
                host.OnPortalTriggerDetected(trigger.Value.Portal, trigger.Value.ArrivalDirectionId);
            }
        }

        // PlayerManager.cs:31-36 (BlockedByEntity != null -> END). Nothing ported so far ever sets
        // BlockedByEntity (it stays the C# default null), so this is currently always false - ported
        // anyway for forward parity once something does set it.
        if (player.BlockedByEntity != null)
        {
            // E20.b B2, binary 0x80031BB0-0x80031BC0: this branch also clears the charge timer (E20.f1a, 0x80031BB4) and the pending-spawn pointer of the weapon (0x80031BC0) before END.
            player.WeaponChargeTimer = 0;
            player.WeaponRecordPointer = 0;
            EndOfMovePlayer(player, in pad, state, host); // E20.c2a T1: every exit of the binary reaches END (0x80032914), this one from 0x80031BB4.
            return;
        }

        // PlayerManager.cs:38-57 (locked branch): CreatePlayerAnimationEffects(1) (E20.f2: the skeleton, binary 0x80031BDC; its stubs do nothing yet), UpdatePlayerCarriedEntity(1),
        // AnimateWarpEffect, and the ForcedSequence facing resolution are ALL no-ops on a fresh New Game -
        // nothing ported yet drives carried entities, warp effects, or forced-sequence facing - so the rest of
        // this branch is a deliberate no-op: the player simply does not move or change animation while
        // g_playerControlFlags carries any of ControlLocked/MessageBox/ForcedSequence.
        if (!DebugIgnoreControlLock && (state.PlayerControlFlags & AlundraGameState.PlayerControlBits.InputBlockedMask) != 0)
        {
            AlundraPlayerAnimationEffects.Create(player, AlundraPlayerAnimationEffects.LockedMode);

            // E20.f1a, binary 0x80031BE8-0x80031BF8, right after CreatePlayerAnimationEffects(1) (0x80031BDC): the locked branch clears the charge timer (0x80031BEC) and the pending-spawn pointer (0x80031BF8), so a
            // charge or a shot pending when a lock begins fires nothing at the unlock. The aura's handle is left as it is: the binary does not touch it here (a stale handle is recreated or released by the aura code with its generation number, E20.f1b).
            player.WeaponChargeTimer = 0;
            player.WeaponRecordPointer = 0;
            EndOfMovePlayer(player, in pad, state, host); // E20.c2a T1: the locked branch also reaches END (0x80031C0C-0x80031C28 lead to 0x80032914): the life vessel of a locked script is mirrored at once.
            return;
        }

        // PlayerManager.cs:59-60 (free branch header), binary 0x80031C38. E20.f2: CreatePlayerAnimationEffects(0) comes right after the gravity flag (0x80031C3C) and before the entry of the damage
        // (0x80031C44, E20.c2) and the second call of the item driver (0x80031CC4): the flail bounce is real, the steps, the landing, the grass cut and the low-life alarm are named stubs.
        player.Flags |= EntityFlags.Gravity;
        AlundraPlayerAnimationEffects.Create(player, AlundraPlayerAnimationEffects.FreeMode);

        // E20.a A4 (D-E20-14): the second call of the item driver, once per normal tick before the pad is read (binary 0x80031CC4). Stub until E20.c2.
        UpdateItemUseDriver(player);

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

        // Slope switch (PlayerManager.cs:207-353). E20.i, case 4 (deep water), binary 0x8003260C: the swim. It comes right after the item pilot (0x80031CC4) and the pad (0x80031CD0), in the place of the
        // gate of the ladder and of the three calls before the switch of states (0x80031D68-0x80031D78), which it never reaches, and it ends the tick (0x80032914).
        if (player.Slope_18c == DeepWaterSlope)
        {
            SwimTick(player, in pad, state, host, buttonsHold, dir);
            EndOfMovePlayer(player, in pad, state, host);
            return;
        }

        // Case 6 (climbing wall) is ported per
        // docs/plan-echelles-chiffrage.md É4 (user decision, 2026-08-26). Every other case (including default)
        // is NOT ported - no other slope case has a documented consumer yet; every one of them falls through as a
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

        // PlayerManager.cs:355-357, binary 0x80031D68-0x80031D78: before the switch of states, every tick on a terrain. E20.a A4: the three calls are named stubs, in the order of the
        // binary (the weapon effects, the run and carrying come with E20.f, E20.d and E20.g; E20.b ported UpdateWeaponStepProgression without its spawn).
        UpdatePlayerWeaponEffect(player, in pad, host);
        UpdateWeaponStepProgression(player, host);
        UpdatePlayerCarriedEntity(player, 0);

        // LoadingMap(0x36) case, binary 0x800325E8 (PlayerManager.cs:914-922; E19.v, O-E19-32): on the ground the case ends and the
        // hero stays LoadingMap - its exit is then the animation Chain bridge (anim 54's trailing control frame chains to anim 0/Idle,
        // see AlundraWorldProxy.OnAnimationFinished); in the air it sets 0x2D and leaves the switch, so none of the cases below runs in
        // this call (V-R1: no stick direction, no 0x2C). "In the air" is the common tail's predicate (V-R2, IsHeroInTheAir).
        if (player.TargetAnimationId == LoadingMapAnimationId)
        {
            if (IsHeroInTheAir(player) && IsAboveItsTerrain(player))
            {
                player.TargetAnimationId = AirStillAnimationId;
            }
        }
        // PlayerManager.cs:361-383 (Idle/Moving case) - E12.d ports CheckEntityInteraction into it
        // (TryUseItem and PlayerTryAction are named stubs, PlayerTryAttack is the common tail's, E20.b): res==2 (button
        // interact) forces Idle and ends the case; res==1 (auto-touch interact) ends it WITHOUT
        // updating the animation this tick; res==0 falls through to the normal animation update -
        // exactly the original's `if (iVar2 != 0) { if (iVar2 != 2) break; ... Idle ... }` shape.
        else if (player.TargetAnimationId == IdleAnimationId || player.TargetAnimationId == MovingAnimationId)
        {
            player.TargetDirection = dir;

            // E20.a A4, binary 0x80031E38 (section 0.2.2 of the plan): TryUseItem (0 ends the tick), PlayerTryAction (non-zero ends it), CheckEntityInteraction (2 gives Idle and ends
            // the tick, another non-zero value ends it), then the common tail.
            if (TryUseItem(player, in pad) != 0 && PlayerTryAction(player, in pad) == 0)
            {
                var interact = CheckEntityInteraction(player, in pad, state, host);
                if (interact == 2)
                {
                    player.TargetAnimationId = IdleAnimationId;
                }
                else if (interact == 0)
                {
                    ApplyGroundOrAirTail(player, in pad, buttonsHold);
                }
            }
        }
        // E19.d2c2 S1 (binary 0x80031E84 and the common tail 0x80031EA8): the jump states keep the direction of the stick WITHOUT the interaction check, then take the same tail.
        // E20.a A4: MovePlayer now runs once per tick like the binary's, so the take-off states are rewritten by the tail every tick (the stamp that spared them on a frame without
        // tick, JumpStartTickStamp, is gone); like Idle and Moving they call TryUseItem then PlayerTryAction first (0x80031E84).
        else if (player.TargetAnimationId is JumpWalkingAnimationId or JumpStandingAnimationId or AirMovingAnimationId or AirStillAnimationId)
        {
            player.TargetDirection = dir;
            if (TryUseItem(player, in pad) != 0 && PlayerTryAction(player, in pad) == 0)
            {
                ApplyGroundOrAirTail(player, in pad, buttonsHold);
            }
        }
        // Climbing(0x0E)/ClimbStill(0x35) case, PlayerManager.cs:675-731 (docs/plan-echelles-chiffrage.md
        // É4). TryUseItem/PlayerTryAction (:677) NOT PORTED (no item/interaction system, same scope
        // restriction as every other unported call in this class) - proceeds straight to the body.
        else if (player.TargetAnimationId == ClimbingAnimationId || player.TargetAnimationId == ClimbStillAnimationId)
        {
            // PlayerManager.cs:679-684: any held direction OTHER than up/down (i.e. a genuinely LATERAL
            // input - left/right) while climbing. The original goes to Jump here - NOT ported (no jump
            // animation/physics in V1). The LoadingMap case above IS ported (E19.v: in the air it gives
            // 0x2D); only this lateral exit to Jump is not. USER DECISION
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

        // E20.i, the exits of the swim to the land, through the jump table 0x80022D3C: 0x0F, 0x1D and 0x28 go to 0x80031DD0 (Idle on the ground, 0x2D in the air, then TryUseItem), 0x3A and 0x3B to 0x80031DB4 (the same,
        // after the direction + 0x10). They are reached on every tick whose slope is not 4: the swimmer who steps out of the deep water, falls off a ledge or arrives from a warp in a swim pose.
        else if (player.TargetAnimationId is SwimAnimationId or FloatAnimationId or DiveAnimationId or SwimHurtAnimationId or SwimHurtEndAnimationId)
        {
            if (player.TargetAnimationId is SwimHurtAnimationId or SwimHurtEndAnimationId)
            {
                player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
            }

            player.TargetAnimationId = IsHeroInTheAir(player) ? AirStillAnimationId : IdleAnimationId;
            TryUseItem(player, in pad);
        }
        // E20.d1 T2, the run (binary 0x80031F54, 0x80031FF4, 0x80032118 and 0x80032558, reached through the jump table 0x80022D3C): the preparation (0x29), the run (0x03), the slide that ends it (0x2A) and the bounce
        // (0x3E, and 0x1C, which shares its handler). The tackle (0x04) is of the ground attack group below.
        else if (player.TargetAnimationId == PrepareSprintAnimationId)
        {
            PrepareSprintTick(player, in pad, buttonsHold, dir);
        }
        else if (player.TargetAnimationId == SprintAnimationId)
        {
            SprintTick(player, in pad, buttonsHold, dir, tickFacing);
        }
        else if (player.TargetAnimationId == StopSprintAnimationId)
        {
            StopSprintTick(player, in pad, buttonsHold);
        }
        else if (player.TargetAnimationId is SprintBounceAnimationId or HurtBounceAnimationId)
        {
            BounceTick(player, in pad);
        }

        // E20.b B3, binary 0x80031E18 (the ground attack group) and 0x80031DF8 (the air attack group), reached through the jump table 0x80022D3C: the states of an attack in progress do nothing but keep
        // the swing on the right side of the ground. Neither reads the pad, writes the direction or calls PlayerTryAttack (the swing ends by the animation Chain, E19.c2); both then call TryUseItem,
        // whose result is ignored, and end.
        else if (IsGroundAttackGroup(player.TargetAnimationId))
        {
            // A swing of the ground group whose hero has lost the ground (a ledge) gives the air state at the next tick.
            if (IsHeroInTheAir(player))
            {
                player.TargetAnimationId = AirStillAnimationId;
            }

            TryUseItem(player, in pad);
        }
        else if (IsAirAttackGroup(player.TargetAnimationId))
        {
            // A swing of the air group whose hero has landed is cut at once: Idle.
            if (!IsHeroInTheAir(player))
            {
                player.TargetAnimationId = IdleAnimationId;
            }

            TryUseItem(player, in pad);
        }

        // PlayerManager.cs:385-943 (every other TargetAnimationId case) - NOT PORTED; an entity whose
        // TargetAnimationId is none of the values ported above keeps it unchanged.

        // PlayerManager.cs:947-950, binary 0x80032914 (END: UpdateItemEffectState, then SetPlayerHpMax/SetPlayerHp), shared with the two early exits above.
        EndOfMovePlayer(player, in pad, state, host);
    }

    /// <summary>
    /// E20.c2a T1: the END of <c>MovePlayer</c> (<c>0x80032914</c>), reached by every exit of the binary (the frozen branch <c>+0x20 != 0</c> = <see cref="AlundraEntityScriptProxy.BlockedByEntity"/>, the locked branch
    /// <c>&amp; 0x34</c>, the hurt path, the normal path): <c>UpdateItemEffectState</c>, then <c>SetPlayerHpMax(hero.HpMax)</c> and <c>SetPlayerHp(hero.Hp)</c> (<c>0x80032924</c>, <c>0x80032930</c>), so the stats are a mirror
    /// of the life the entity carries. The mirror runs only for a hero that pulled its life from the stats (<see cref="AlundraEntityScriptProxy.LifePulledFromStats"/>, set by
    /// <see cref="AlundraWorldProxy.AdoptPlayerPawn"/>): the bare heroes of the unit tests keep the default life of 0, which would otherwise be written over the stats the test set up. This guard is the one
    /// documented deviation of the epilogue.
    /// </summary>
    internal static void EndOfMovePlayer(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host)
    {
        UpdateItemEffectState(player, in pad, state, host);

        if (!player.LifePulledFromStats)
        {
            return;
        }

        SetPlayerHpMax(state, player.HpMax);
        SetPlayerHp(state, ClampToShort(player.Hp)); // the binary compares 32-bit values; only the clamps decide, so saturating the argument is exact.
    }

    private static short ClampToShort(int value) => value > short.MaxValue ? short.MaxValue : value < short.MinValue ? short.MinValue : (short)value;

    /// <summary>
    /// E20.i T2: the swim, the branch of the slope 4 (<c>0x8003260C</c>) in the binary's order. The charge counter (<c>0x80126FFC</c>) and the pending-shot pointer (<c>0x80127110</c>) are zeroed, carrying is told
    /// <c>UpdatePlayerCarriedEntity(2)</c> (a named stub, E20.g), then the state decides, through the table <c>0x80022E7C</c> (<see cref="SwimTable"/>), which of the six handlers runs. The branch never reaches the
    /// weapon effects, the weapon steps, <c>PlayerTryAttack</c> or <c>PlayerTryAction</c>; the caller ends the tick. It reads no boots level: the wall of the deep water (O-E19-22) and the currents (O-E20-16) are not ported,
    /// so a hero without the swim boots swims where the original stops him; the splash, the wake and the ripple of the dive (the shadow pass <c>0x80038E84</c> and <c>CreatePlayerAnimationEffects</c>) are E20.e's.
    /// </summary>
    private static void SwimTick(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host, uint nibble, uint dir)
    {
        player.WeaponChargeTimer = 0;
        player.WeaponRecordPointer = 0;
        UpdatePlayerCarriedEntity(player, 2);

        switch (SwimHandlerOf(player.TargetAnimationId))
        {
            case SwimHandler.Skip:
                CancelItemMode(player);
                break;

            case SwimHandler.Float: // 0x80032674: the state, then the item driver; the direction is not written.
                player.TargetAnimationId = FloatAnimationId;
                TryUseItem(player, in pad);
                break;

            case SwimHandler.FlipThenFloat: // 0x80032658
                player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
                player.TargetAnimationId = FloatAnimationId;
                TryUseItem(player, in pad);
                break;

            case SwimHandler.ItemOnly: // 0x80032680
                TryUseItem(player, in pad);
                break;

            case SwimHandler.Steer: // 0x80032690
                SwimSteerTick(player, in pad, state, host, nibble, dir);
                break;

            case SwimHandler.HurtEnd: // 0x800326FC
                SwimHurtEndTick(player, in pad);
                break;
        }
    }

    /// <summary>
    /// E20.i T2: the steering of the swim, states 0x0F and 0x1D (<c>0x80032690</c>). The direction of the pad is written before the item driver, even when no direction is held (the pad falls back to the current one);
    /// <c>TryUseItem</c> at 0 ends the tick; <c>CheckEntityInteraction</c> 2 gives 0x1D, another non-zero value ends the tick; then an edge of the Triangle, the Cross or the Square (<see cref="SwimDiveButtons"/>) starts the
    /// burst (0x28), else any bit of the direction nibble (even a contradictory pair) swims (0x0F), else the hero floats (0x1D).
    /// </summary>
    private static void SwimSteerTick(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host, uint nibble, uint dir)
    {
        player.TargetDirection = dir;
        if (TryUseItem(player, in pad) == 0)
        {
            return;
        }

        var interact = CheckEntityInteraction(player, in pad, state, host);
        if (interact == 2)
        {
            player.TargetAnimationId = FloatAnimationId;
            return;
        }

        if (interact != 0)
        {
            return;
        }

        player.TargetAnimationId = (pad.ButtonsJustPressed & SwimDiveButtons) != 0 ? DiveAnimationId : nibble != 0 ? SwimAnimationId : FloatAnimationId;
    }

    /// <summary>
    /// E20.i T2: the end of the hurt in deep water, state 0x3B (<c>0x800326FC</c>). <c>TryUseItem</c> at 0 turns the direction (+ 0x10) and ends the tick; otherwise the hero stays in 0x3B until the animation sets
    /// <c>+0xAC</c> (<see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/>, the hold of its last frame), then floats (0x1D) and turns back (+ 0x10). Nothing enters 0x3A or 0x3B yet: the hurt block of E20.c2b does.
    /// </summary>
    private static void SwimHurtEndTick(AlundraEntityScriptProxy player, in AlundraPadState pad)
    {
        if (TryUseItem(player, in pad) == 0)
        {
            player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
            return;
        }

        if (player.ForceResetAnimationFlag == 1)
        {
            player.TargetAnimationId = FloatAnimationId;
            player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
        }
    }

    /// <summary>E20.i T1 stub of <c>0x800350C0</c>, the cancellation of the item mode the swim table's skip group performs (E20.h0): no item mode exists yet.</summary>
    private static void CancelItemMode(AlundraEntityScriptProxy player)
    {
    }

    /// <summary>E20.d1 T2: <c>g_dashDirections[nibble]</c> (<c>0x80022CAC</c>), the direction a single cardinal bit gives, -1 for anything else.</summary>
    private static int DashDirection(uint nibble)
        => nibble < AnimationTables.DashDirectionByNibble.Length ? AnimationTables.DashDirectionByNibble[nibble] : -1;

    /// <summary>
    /// E20.d1 T2: the preparation of the run, state 0x29 (<c>0x80031F54</c>), in the binary's order. <c>TryUseItem</c> (0 ends the tick), <c>PlayerTryAction</c> (non-zero ends it), <c>PlayerTryAttack</c> (its second
    /// call site, <c>0x80031F74</c>; non-zero ends it: the Square swings the sword in the preparation); then the direction of the pad is written (<c>0x80031F94</c>, in a delay slot: whatever follows, even in the air);
    /// in the air, 0x2D; Triangle released, Idle; the counter of animation ends (<c>+0xB0</c>, zeroed by the Triangle entry and counted by the loop of the animation) still 0 (the first 25 ticks), the tick ends;
    /// else the dash table: -1 ends the tick, otherwise the direction takes its value and the run (0x03) starts.
    /// </summary>
    private static void PrepareSprintTick(AlundraEntityScriptProxy player, in AlundraPadState pad, uint nibble, uint dir)
    {
        if (TryUseItem(player, in pad) == 0 || PlayerTryAction(player, in pad) != 0 || PlayerTryAttack(player, in pad) != 0)
        {
            return;
        }

        player.TargetDirection = dir;

        if (IsHeroInTheAir(player))
        {
            player.TargetAnimationId = AirStillAnimationId;
            return;
        }

        if ((pad.ButtonsHold & AlundraPadState.Triangle) == 0)
        {
            player.TargetAnimationId = IdleAnimationId;
            return;
        }

        if (player.AnimCompleteCounter == 0)
        {
            return;
        }

        AlundraStickProbe.LogStartTableRead(PrepareSprintAnimationId, nibble); // E20.d2a S3
        var start = DashDirection(nibble);
        if (start < 0)
        {
            return;
        }

        player.TargetDirection = (uint)start;
        player.TargetAnimationId = SprintAnimationId;
    }

    /// <summary>
    /// E20.d1 T2: the run, state 0x03 (<c>0x80031FF4</c>), in the binary's order; <c>TryUseItem</c> is NOT called. <c>PlayerTryAction</c> (non-zero ends the tick); in the air, 0x2D; the mover curtailed the run
    /// (<see cref="AlundraEntityScriptProxy.ForceAdjusted"/>): the bounce, the effect 9 at the hero (<see cref="AlundraNativeEffects.CreateSprintBounce"/>), state 0x3E, direction + 0x10; no direction on the pad: the
    /// slide (0x2A) with the resume counter at 1; Triangle released, or the direction (the pad's, or the current one when the pad's combination is invalid) outside the cone of the facing of the tick
    /// (<see cref="AnimationTables.SprintConeRows"/>): the slide with the counter at 0; a Square, a Cross or a Circle held (<c>hold &amp; 0xE0</c>): the tackle (0x04). The direction of the hero is never written here
    /// except by the bounce.
    /// </summary>
    private static void SprintTick(AlundraEntityScriptProxy player, in AlundraPadState pad, uint nibble, uint dir, int tickFacing)
    {
        if (PlayerTryAction(player, in pad) != 0)
        {
            return;
        }

        if (IsHeroInTheAir(player))
        {
            player.TargetAnimationId = AirStillAnimationId;
            return;
        }

        if (player.ForceAdjusted != 0)
        {
            if (player.ScriptHost?.EffectStage is { } stage)
            {
                var x = player.PosX;
                var y = player.PosY;
                var z = player.PosZ + 1; // the binary's frame of Z (ADR-0026).
                stage.Emit(reservoir => AlundraNativeEffects.CreateSprintBounce(reservoir, x, y, z));
            }

            player.TargetAnimationId = SprintBounceAnimationId;
            player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
            return;
        }

        if (nibble == 0)
        {
            player.TargetAnimationId = StopSprintAnimationId;
            player.StopSprintCounter = 1;
            return;
        }

        if ((pad.ButtonsHold & AlundraPadState.Triangle) == 0 || !IsInSprintCone(dir, tickFacing))
        {
            player.TargetAnimationId = StopSprintAnimationId;
            player.StopSprintCounter = 0;
            return;
        }

        if ((pad.ButtonsHold & (AlundraPadState.Circle | AlundraPadState.Cross | AlundraPadState.Square)) != 0)
        {
            player.TargetAnimationId = TackleAnimationId;
        }
    }

    /// <summary>E20.d1 T2: the cone of the run (<c>0x800320A4</c>): <paramref name="dir"/> is one of the three words of the row of the facing of the tick.</summary>
    private static bool IsInSprintCone(uint dir, int tickFacing)
    {
        var row = Math.Clamp(tickFacing, 0, 3) * 3;
        var cone = AnimationTables.SprintConeRows;
        return dir == cone[row] || dir == cone[row + 1] || dir == cone[row + 2];
    }

    /// <summary>
    /// E20.d1 T2: the slide that ends the run, state 0x2A (<c>0x80032118</c>), in the binary's order. <c>TryUseItem</c> (0 ends the tick), <c>PlayerTryAction</c> (non-zero ends it); in the air, 0x2D; the resume counter
    /// (<see cref="AlundraEntityScriptProxy.StopSprintCounter"/>) at 0: the tick ends; the mover curtailed the slide, or Triangle released: the counter goes to 0; else it counts up. Below 11 a direction bit on the pad clears
    /// it (a press too early is lost); from 11 on the dash table decides: -1 ends the tick and keeps the counter, otherwise the direction takes its value and the run (0x03) resumes with the force of the slide.
    /// </summary>
    private static void StopSprintTick(AlundraEntityScriptProxy player, in AlundraPadState pad, uint nibble)
    {
        if (TryUseItem(player, in pad) == 0 || PlayerTryAction(player, in pad) != 0)
        {
            return;
        }

        if (IsHeroInTheAir(player))
        {
            player.TargetAnimationId = AirStillAnimationId;
            return;
        }

        if (player.StopSprintCounter == 0)
        {
            return;
        }

        if (player.ForceAdjusted != 0 || (pad.ButtonsHold & AlundraPadState.Triangle) == 0)
        {
            player.StopSprintCounter = 0;
            return;
        }

        player.StopSprintCounter++;
        if (player.StopSprintCounter < ResumeWindowStart)
        {
            if (nibble != 0)
            {
                player.StopSprintCounter = 0;
            }

            return;
        }

        AlundraStickProbe.LogStartTableRead(StopSprintAnimationId, nibble); // E20.d2a S3
        var resume = DashDirection(nibble);
        if (resume < 0)
        {
            return;
        }

        player.TargetDirection = (uint)resume;
        player.TargetAnimationId = SprintAnimationId;
    }

    /// <summary>E20.d1 T2: the value of the resume counter from which a direction resumes the run (<c>0x80032190</c>: below 11 a press is lost).</summary>
    private const int ResumeWindowStart = 11;

    /// <summary>
    /// E20.d1 T2: the bounce, states 0x3E and 0x1C (<c>0x80032558</c>). <c>TryUseItem</c> at 0 flips the direction (+ 0x10) and ends the tick (<c>0x8003270C</c>); otherwise nothing happens until the animation holds on its
    /// last frame (<see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/>, the binary's <c>+0xAC</c>, raised by the Hold end); then the direction flips (+ 0x10, the hero faces the way he came from) and the state
    /// is Idle on the ground, Moving (1) in the air.
    /// </summary>
    private static void BounceTick(AlundraEntityScriptProxy player, in AlundraPadState pad)
    {
        if (TryUseItem(player, in pad) == 0)
        {
            player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
            return;
        }

        if (player.ForceResetAnimationFlag != 1)
        {
            return;
        }

        player.TargetDirection = (player.TargetDirection + 0x10) & 0x1F;
        player.TargetAnimationId = IsHeroInTheAir(player) ? MovingAnimationId : IdleAnimationId;
    }

    /// <summary>E20.a A4 stub of <c>TryUseItem</c> (<c>0x8002ED64</c>): non-zero means "carry on this tick". The binary returns 1 whenever the Circle button has no rising edge
    /// (<c>0x8002EDA8</c>); a stub returning 0 would end the tick and kill every attack, jump and run (critique C10). The use of an item (the Circle) is E20.h.</summary>
    private static int TryUseItem(AlundraEntityScriptProxy player, in AlundraPadState pad) => TryUseItemResultForTests ?? 1;

    /// <summary>E20.d1 T4 test seam, per thread: the answer of the stub of <see cref="TryUseItem"/> (null: 1, "carry on"). The branches of the run that the binary takes when the item driver answers 0 (<c>0x80031F5C</c>,
    /// <c>0x80032120</c>, <c>0x80032560</c>) are unreachable while the stub answers 1; the tests of those branches pose 0 here.</summary>
    [ThreadStatic]
    internal static int? TryUseItemResultForTests;

    /// <summary>E20.a A4 stub of <c>PlayerTryAction</c> (<c>0x8002EEAC</c>): 0 means "no action, carry on" (<c>0x8002EFC8</c>: nothing carried and nothing to lift). Lifting, carrying and throwing are E20.g.</summary>
    private static int PlayerTryAction(AlundraEntityScriptProxy player, in AlundraPadState pad) => 0;

    /// <summary>The bit of <see cref="AlundraEntityScriptProxy.CombinedVramFlagsOR"/> (a ground property of 0x20 under a corner of the footprint) on which the Square does not strike (<c>0x8002EB24</c>).</summary>
    private const uint NoAttackVramFlag = 0x2000;

    /// <summary>The "no animation" answer of a weapon record: a press only plays the sound 3 (<c>0x8002ED30</c>).</summary>
    private const int NoAttackSoundId = 3;

    /// <summary>
    /// E20.b B2: port of <c>PlayerTryAttack</c> (<c>0x8002EAF4</c>), first call of the common tail (<c>0x80031EA8</c>); the second call, in the run preparation (0x29, <c>0x80031F74</c>), is E20.d.
    /// Rends 1 when a swing starts (the caller ends its tick), else 0.
    /// <list type="number">
    /// <item><description>A footprint corner on a ground property of 0x20 (<c>CombinedVramFlagsOR &amp; 0x2000</c>): nothing, not even a sound.</description></item>
    /// <item><description>A release of the Square comes first (<c>0x8002EB38</c>): the charged attack needs the charge timer at 60 and the ground (<see cref="ReleaseChargedAttack"/>, E20.f1a); otherwise this path
    /// returns 0 and a release never reaches the press test (D-E20-9: a press followed by a release in the same tick does not strike, which the tick pad cannot even produce).</description></item>
    /// <item><description>The press of the Square (<c>ButtonsJustPressed</c>): the animation byte of the weapon record, the ground one or, in the air, the one 4 bytes after
    /// (<c>0x80028C79 + 13 w</c>, <see cref="AlundraWeaponTables.AnimationAddress"/>). Animation 0 (the staff, no weapon record animation): the sound 3, 0. Otherwise the pending-spawn pointer
    /// (<c>0x80127110</c>) takes the address, its counter (<c>0x80127114</c>) 0, and the target animation (<c>+0x88</c>) the animation; the sound of the swing comes from the change of animation
    /// itself (E19.t).</description></item>
    /// </list>
    /// </summary>
    private static int PlayerTryAttack(AlundraEntityScriptProxy player, in AlundraPadState pad)
    {
        if ((player.CombinedVramFlagsOR & NoAttackVramFlag) != 0)
        {
            return 0;
        }

        if ((pad.ButtonsReleased & AlundraPadState.Square) != 0)
        {
            return ReleaseChargedAttack(player);
        }

        if ((pad.ButtonsJustPressed & AlundraPadState.Square) == 0)
        {
            return 0;
        }

        var address = AlundraWeaponTables.AnimationAddress(player.CurrentWeaponRecord, IsHeroInTheAir(player));
        var animation = AlundraWeaponTables.RecordByte(address);
        if (animation == 0)
        {
            player.ScriptHost?.SoundPlayer?.PlaySfx(NoAttackSoundId);
            return 0;
        }

        player.WeaponRecordPointer = address;
        player.WeaponStepCounter = 0;
        player.TargetAnimationId = animation;
        return 1;
    }

    /// <summary>The sound of the release of a full charge (<c>0x8002EC54</c>).</summary>
    internal const int ReleaseSfx = 0x2B;

    /// <summary>
    /// E20.f1a: the release branch of <see cref="PlayerTryAttack"/> (<c>0x8002EB38-0x8002EC88</c>), reached when the Square is released this tick (it comes before the press test). A charge counter below 60, or a hero in the
    /// air (<c>IsOnGround</c> zero, <c>0x8002EB60</c>), ends it with nothing (the release is lost; D-E20-9). Otherwise: the 16 effects of the burst (<see cref="AlundraNativeEffects.CreateChargeBurst"/>, queued on
    /// the stage, at the hero's position with the binary's Z), the sound 0x2B, the pending-spawn pointer on the charged animation byte of the weapon record (<c>0x80028C79 + 13 w + 8</c>), its counter at 0, and the
    /// target animation on that byte; it returns 1 (the caller ends its tick). The charge counter itself is not touched: it falls to 0 at the next tick, because the charged animation does not count the charge.
    /// The counter is read AFTER <see cref="UpdatePlayerWeaponEffect"/> of the same tick, so a hold spark (counter 60, <c>+0x1C &amp; 3</c> zero) is created first, then the burst (oracle C10, C14, C16 at tick 101: 17 creations).
    /// </summary>
    private static int ReleaseChargedAttack(AlundraEntityScriptProxy player)
    {
        if (player.WeaponChargeTimer < FullChargeTimer || IsHeroInTheAir(player))
        {
            return 0;
        }

        if (player.ScriptHost?.EffectStage is { } stage)
        {
            var x = player.PosX;
            var y = player.PosY;
            var z = player.PosZ + 1; // the binary's frame of Z (ADR-0026).
            stage.Emit(reservoir => AlundraNativeEffects.CreateChargeBurst(reservoir, x, y, z));
        }

        player.ScriptHost?.SoundPlayer?.PlaySfx(ReleaseSfx);
        var address = AlundraWeaponTables.ChargeAnimationAddress(player.CurrentWeaponRecord);
        player.WeaponRecordPointer = address;
        player.WeaponStepCounter = 0;
        player.TargetAnimationId = AlundraWeaponTables.RecordByte(address);
        return 1;
    }

    /// <summary>E20.b B3: the ids the jump table <c>0x80022D3C</c> sends to the ground attack group <c>0x80031E18</c> (the swings and charges of the ground, the bounces of the flail 08 and 25, 04 and 09).</summary>
    private static bool IsGroundAttackGroup(uint animationId)
        => animationId is 0x04 or 0x08 or 0x09 or 0x10 or 0x11 or 0x12 or 0x13 or 0x18 or 0x19 or 0x25 or 0x3F or 0x40 or 0x44 or 0x49;

    /// <summary>E20.b B3: the ids the jump table <c>0x80022D3C</c> sends to the air attack group <c>0x80031DF8</c>.</summary>
    private static bool IsAirAttackGroup(uint animationId)
        => animationId is 0x0A or 0x14 or 0x15 or 0x16 or 0x17 or 0x1F or 0x26 or 0x41 or 0x42 or 0x46 or 0x4B;

    /// <summary>The Square charges the weapon for 60 ticks (<c>0x8002F530</c>).</summary>
    internal const int FullChargeTimer = 60;

    /// <summary>The charge counter from which the aura appears and the hold sounds play (<c>0x8002F560</c>: below 11, the aura is released).</summary>
    internal const int AuraChargeTimer = 11;

    /// <summary>The sound of the charge, every 4 ticks from the counter 11 to 59 (<c>0x8002F724</c>).</summary>
    internal const int ChargeSfx = 0x2A;

    /// <summary>
    /// E20.f1a: port of <c>UpdatePlayerWeaponEffect</c> (<c>0x8002F49C</c>, called at <c>0x80031D68</c>, every tick on a terrain, before <c>UpdateWeaponStepProgression</c> and the switch of states, so BEFORE
    /// <see cref="PlayerTryAttack"/> of the same tick).
    /// <list type="number">
    /// <item><description>The counter (<see cref="AlundraEntityScriptProxy.WeaponChargeTimer"/>, <c>0x80126FFC</c>): +1 up to 60 when the Square is held or released this tick, the weapon record of the equipped weapon is chargeable
    /// (<c>byte[0x80028C78 + 13 w]</c> non-zero) and the animation the hero is in counts the charge (<see cref="AlundraWeaponTables.AnimationCountsCharge"/>); else 0.</description></item>
    /// <item><description>Below 11: the aura is released (<see cref="ReleaseAura"/>), the function ends. From 11: the aura is created or renewed (<see cref="CreateAuraIfNeeded"/>, E20.f1b), every tick.</description></item>
    /// <item><description>Only when <c>+0x1C &amp; 3</c> is 0 (the counter of frames BEFORE the counters pass of this tick): below 60 the sound 0x2A; at 60 the hold spark, queued on the stage
    /// (<see cref="AlundraNativeEffects.CreateChargeSpark"/>, whose sound and two numbers happen only if the effect is created, when the stage applies it).</description></item>
    /// </list>
    /// The stage is the host's (<see cref="IAlundraScriptHost.EffectStage"/>); a host without one creates no effect, and the sounds that do not depend on a creation still play.
    /// </summary>
    internal static void UpdatePlayerWeaponEffect(AlundraEntityScriptProxy player, in AlundraPadState pad, IAlundraScriptHost? host)
    {
        if (((pad.ButtonsHold | pad.ButtonsReleased) & AlundraPadState.Square) != 0
            && AlundraWeaponTables.RecordByte(AlundraWeaponTables.RecordsAddress + (uint)(AlundraWeaponTables.RecordSize * player.CurrentWeaponRecord)) != 0
            && AlundraWeaponTables.AnimationCountsCharge(player.TargetAnimationId))
        {
            if (player.WeaponChargeTimer < FullChargeTimer)
            {
                player.WeaponChargeTimer++;
            }
        }
        else
        {
            player.WeaponChargeTimer = 0;
        }

        if (player.WeaponChargeTimer < AuraChargeTimer)
        {
            ReleaseAura(player, host);
            return;
        }

        CreateAuraIfNeeded(player, host);
        if ((player.FrameCounter & 3) != 0)
        {
            return;
        }

        if (player.WeaponChargeTimer < FullChargeTimer)
        {
            host?.SoundPlayer?.PlaySfx(ChargeSfx);
            return;
        }

        if (host?.EffectStage is not { } stage)
        {
            return;
        }

        // The binary writes the hero's X, Y, Z (+0x114, +0x118, +0x11C) and forces (+0xC4, +0xC8) when it creates the effect, at this tick: captured now, applied by the stage at the tick of the emission.
        var x = player.PosX;
        var y = player.PosY;
        var z = player.PosZ + 1; // the binary's frame of Z (ADR-0026: the DLL's PosZ is the binary's minus 1).
        var forceX = player.ForceX;
        var forceY = player.ForceY;
        var frameCounter = player.FrameCounter;
        var soundPlayer = host.SoundPlayer;
        stage.Emit(reservoir => AlundraNativeEffects.CreateChargeSpark(reservoir, x, y, z, forceX, forceY, frameCounter, soundPlayer));
    }

    /// <summary>The effect of the charge aura: global effect 3 (<c>0x8002F5B0</c>, <c>0x8003BE74(0, 3, 0, hero, 1, 0, 0, 0x100000)</c>, its animation 0 lasts 96 ticks and ends with <c>Destroy</c>).</summary>
    internal const int AuraEffect = 3;

    /// <summary>The depth offset of the aura (the creator's fifth argument, <c>+0x60</c>): its key is the hero's plus one, so it is drawn just after the hero.</summary>
    internal const int AuraDepthOffset = 1;

    /// <summary>The height of the aura above the hero (the creator's last argument): 16 pixels.</summary>
    internal const int AuraOffsetZ = 0x100000;

    /// <summary>
    /// E20.f1b: the creation of the aura at <c>0x8002F570-0x8002F5BC</c>, at every tick where the charge counter is 11 or more. The binary creates when the handle (<c>0x80126FF8</c>) is null or when the <c>+0x7D</c> of
    /// its slot (<see cref="AlundraEffectSlot.Destroying"/>) is set: the animation of the aura lasts 96 ticks, so a held charge renews the aura every 97 ticks (the old one is freed by the effect pass of the same tick).
    /// The handle receives the result of the creation even when it is null (a full reservoir: the creation is retried at every tick). The effect is created AT ONCE and its slot read at the tick (D-E20-28, ADR-0057:
    /// the hero's exception to the queued model of ADR-0046; <see cref="AlundraEffectStage.CreateAttachedNow"/>).
    /// <para>
    /// Written deviation from the binary (ADR-0059, proposed): the aura is also created again when its slot is no longer active or no longer carries the aura (<see cref="AlundraEntityScriptProxy.AuraGeneration"/>).
    /// At one tick per frame the binary never gets there; at three ticks per frame, at the renewal, the frame frees the old aura and then applies the spark of its last tick on the freed slot, which the handle would
    /// follow as if it were the aura. Other deviations of D-E20-28 for a frame of several ticks: a frame that holds both c + 96 and c + 97 shows one frame without aura; the spark's draws stay at the application
    /// of the stage (E20.f1a); the hero's slot index is taken before the queued operations of the frame.
    /// </para>
    /// </summary>
    private static void CreateAuraIfNeeded(AlundraEntityScriptProxy player, IAlundraScriptHost? host)
    {
        if (host?.EffectStage is not { } stage)
        {
            return;
        }

        var slot = player.AuraSlot;
        if (slot != null && !slot.Destroying && slot.Active && slot.Generation == player.AuraGeneration)
        {
            return;
        }

        // The anchor is the hero itself (explicit IAlundraEffectAnchor of the proxy): the effect starts at its position, follows it and takes its depth key plus AuraDepthOffset.
        var created = stage.CreateAttachedNow(false, AuraEffect, 0, player, AuraDepthOffset, 0, 0, AuraOffsetZ);
        player.AuraSlot = created;
        player.AuraGeneration = created?.Generation ?? 0;
    }

    /// <summary>
    /// E20.f1b: the release of the aura below a charge of 11 (<c>0x8002F730-0x8002F744</c>): with a non-null handle, the binary writes 0 in the <c>+0x68</c> of its slot (status free) and clears the handle, without any
    /// other test. Here the handle is cleared at once and the slot is released by an operation of the stage at the position of this tick (<see cref="AlundraEffectStage.FreeAtTick"/>, ADR-0059), and ONLY IF it still
    /// holds the aura (D-E20-30, the defect of the original corrected: when the aura expired during a lock and its slot was taken by another effect, the binary frees that other effect).
    /// </summary>
    private static void ReleaseAura(AlundraEntityScriptProxy player, IAlundraScriptHost? host)
    {
        var slot = player.AuraSlot;
        if (slot == null)
        {
            return;
        }

        var generation = player.AuraGeneration;
        player.AuraSlot = null;
        player.AuraGeneration = 0;
        host?.EffectStage?.FreeAtTick(slot, generation);
    }

    /// <summary>E20.b test seam: the number of calls of <see cref="SpawnWeaponProjectile"/> (the tick at which a bow or a wand would spawn its projectile; the oracle journals the same call). Per thread.</summary>
    [ThreadStatic]
    internal static int WeaponSpawnCallCountForTests;

    /// <summary>
    /// E20.b B4: port of <c>UpdateWeaponStepProgression</c> (<c>0x8002F768</c>, called at <c>0x80031D70</c>, every tick before the state switch), without the spawn. With a pending pointer
    /// (<see cref="AlundraEntityScriptProxy.WeaponRecordPointer"/>): a record whose type (<c>ptr[1]</c>) is 0, or whose current animation does not carry the "spawns after the delay" flag of
    /// <see cref="AlundraWeaponTables.AnimationFlagsAddress"/>, clears the pointer (the melee swings: at the tick after the press); otherwise the counter counts up to the delay of the record
    /// (<c>ptr[2]</c>), then the projectile is spawned (<see cref="SpawnWeaponProjectile"/>) and the pointer cleared.
    /// </summary>
    private static void UpdateWeaponStepProgression(AlundraEntityScriptProxy player, IAlundraScriptHost? host)
    {
        var pointer = player.WeaponRecordPointer;
        if (pointer == 0)
        {
            return;
        }

        if (AlundraWeaponTables.RecordByte(pointer + 1) == 0 || !AlundraWeaponTables.AnimationSpawnsAfterDelay(player.TargetAnimationId))
        {
            player.WeaponRecordPointer = 0;
            return;
        }

        if (player.WeaponStepCounter < AlundraWeaponTables.RecordByte(pointer + 2))
        {
            player.WeaponStepCounter++;
            return;
        }

        SpawnWeaponProjectile(player, pointer, host);
        player.WeaponRecordPointer = 0;
    }

    /// <summary>E20.b B4 seam of the spawn call <c>0x80039F58</c> of <c>UpdateWeaponStepProgression</c> (<c>0x8002F818</c>): the projectile of a bow or a wand, at the hero's X and Y, at his Z plus the
    /// height <c>ptr[3]</c> of the record. Counted and journaled for the tests; since E20.f3a it calls the apparition of an entity by sprite index for the types of <see cref="WiredWeaponTypes"/> (empty until E20.f3c). The direction is the one of the tick (<see cref="AlundraEntityScriptProxy.BoxDirection"/>), read before the tick writes anything, as the binary reads <c>+0x98</c>.</summary>
    internal static void SpawnWeaponProjectile(AlundraEntityScriptProxy player, uint recordPointer, IAlundraScriptHost? host)
    {
        WeaponSpawnCallCountForTests++;
        var call = new WeaponSpawnCall(
            AlundraWeaponTables.RecordByte(recordPointer + 1), player.PosX, player.PosY, player.PosZ + (AlundraWeaponTables.RecordByte(recordPointer + 3) << 16),
            SpawnDirections[Math.Clamp(player.BoxDirection, 0, SpawnDirections.Length - 1)]);
        WeaponSpawnCallsForTests?.Add(call);

        // E20.e0a3b: the place of the call of the binary (0x8002F818, inside MovePlayer) where the real shot calls the interface of the sprite index. E20.f3a (D-F3-3): the test hook, when posed, REPLACES the production call (never
        // two apparitions); otherwise a type of the wired set is born through the interface when the host is a world. The set is empty in this slice, so nothing is born in production. The return is ignored, as the
        // binary does (0x8002F818 drops it): a full table of slots loses the shot silently and the caller clears the pointer all the same.
        if (WeaponSpawnBirthHookForTests is { } hook)
        {
            hook(player, call);
            return;
        }

        if (WiredWeaponTypes.Contains(call.Type) && host is IEntityWorldContext world)
        {
            world.SpawnEntityBySpriteIndex(player, false, call.Type, call.X, call.Y, call.Z, call.Direction);
        }
    }

    /// <summary>
    /// E20.f3a (docs/plan-e20-heros.md section 1.19.1, D-F3-3): the sprite types of the global page whose shot is wired to <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>, one type after the other and never before its
    /// handlers exist (the invariant of <see cref="UnhandledWeaponSpriteCodes"/>, D-F3-4). {27, 28, 29} since E20.f3c (the wave of the sword, C61 and E10); E20.f4 to E20.f6 wire the others.
    /// </summary>
    internal static readonly System.Collections.Generic.IReadOnlySet<int> ProductionWiredWeaponTypes = new System.Collections.Generic.HashSet<int> { 27, 28, 29 };

    /// <summary>E20.f3a test seam, per thread: when set, replaces <see cref="ProductionWiredWeaponTypes"/> (the production set stays empty and a test wires a type to prove the seam).</summary>
    [ThreadStatic]
    internal static System.Collections.Generic.IReadOnlySet<int>? WiredWeaponTypesForTests;

    /// <summary>The set the shot reads: the one of the test when a test posed it, otherwise <see cref="ProductionWiredWeaponTypes"/>.</summary>
    internal static System.Collections.Generic.IReadOnlySet<int> WiredWeaponTypes => WiredWeaponTypesForTests ?? ProductionWiredWeaponTypes;

    /// <summary>
    /// E20.f3a (D-F3-4): the invariant "a type is wired only when its handlers exist". For every type of the global page in <paramref name="types"/>, every non-zero code of its program C (tick) and of its program E
    /// (deactivation) must be handled: a key of <paramref name="registry"/> for this type, or, for the program E, a code the runner treats in line (<see cref="AlundraEventProgramRunner.IsInlineDeactivateCode"/>). Returns the
    /// (slot, code) pairs that are not, in the order of the types and then C before E; a type the catalog does not hold gives (-1, type). Empty means the types may be wired.
    /// </summary>
    internal static System.Collections.Generic.IReadOnlyList<(int Slot, int Code)> UnhandledWeaponSpriteCodes(
        System.Collections.Generic.IEnumerable<int> types, ISpriteRecordCatalog catalog, AlundraNativeSpriteRegistry registry)
    {
        var unhandled = new System.Collections.Generic.List<(int Slot, int Code)>();
        foreach (var type in types)
        {
            if (!catalog.TryGetPrefab(false, type, out var prefabAssetId) || !catalog.TryGet(prefabAssetId, out var header))
            {
                unhandled.Add((-1, type));
                continue;
            }

            if (header.ProgramTick != 0 && !registry.TryGet(ScriptHelper.ProgramCTick, header.ProgramTick, type, out _))
            {
                unhandled.Add((ScriptHelper.ProgramCTick, header.ProgramTick));
            }

            if (header.ProgramDeactivate != 0
                && !AlundraEventProgramRunner.IsInlineDeactivateCode(header.ProgramDeactivate)
                && !registry.TryGet(ScriptHelper.ProgramEDeactivate, header.ProgramDeactivate, type, out _))
            {
                unhandled.Add((ScriptHelper.ProgramEDeactivate, header.ProgramDeactivate));
            }
        }

        return unhandled;
    }

    /// <summary>E20.e0a3b test seam, per thread (the motif of <see cref="WeaponSpawnCallsForTests"/>): called at the exact place of <c>MovePlayer</c> where the shot of the hero calls <c>0x80039F58</c>, with the hero and the arguments of the call.
    /// Null in production. E20.f3a: when posed, it REPLACES the call of the production seam (<see cref="WiredWeaponTypes"/>), so a test that spawns from here gets exactly one entity.</summary>
    [ThreadStatic]
    internal static Action<AlundraEntityScriptProxy, WeaponSpawnCall>? WeaponSpawnBirthHookForTests;

    /// <summary>E20.f1a: the arguments of one call of the spawn stub, as the binary passes them to <c>0x80039F58</c> (<c>0x8002F7D4-0x8002F818</c>): the type is <c>ptr[1]</c> of the record, the position is the hero's X and Y and his Z
    /// plus <c>ptr[3]</c> pixels (the DLL's frame of Z: the oracle's is one raw unit more, ADR-0026), the direction is <c>CardinalDirectionTable[+0x98]</c> (<c>0x80023644</c>: down 0, up 0x10, left 8, right 0x18), indexed by <c>BoxDirection</c> (E20.f3a).</summary>
    internal readonly record struct WeaponSpawnCall(int Type, int X, int Y, int Z, int Direction);

    /// <summary>E20.f1a test seam, per thread: when set, every call of <see cref="SpawnWeaponProjectile"/> is appended to it with its arguments.</summary>
    [ThreadStatic]
    internal static System.Collections.Generic.List<WeaponSpawnCall>? WeaponSpawnCallsForTests;

    /// <summary>The table <c>0x80023644</c>, indexed by the hero's direction of the sprite at the tick of the shot (<see cref="AlundraEntityScriptProxy.BoxDirection"/>, never the end-of-frame <see cref="AlundraEntityScriptProxy.AnimationDirection"/>, D-F3-2; the order of <see cref="AnimationTables.DirectionNames"/>).</summary>
    private static readonly int[] SpawnDirections = { 0, 0x10, 8, 0x18 };

    /// <summary>E20.a A4 stub of <c>UpdatePlayerCarriedEntity</c> (<c>0x8002F884</c>, called with 0 at <c>0x80031D78</c> and, E20.i, with 2 by the swim branch): nothing is carried yet (E20.g).</summary>
    private static void UpdatePlayerCarriedEntity(AlundraEntityScriptProxy player, int mode)
    {
    }

    /// <summary>
    /// <c>UpdateItemEffectState</c> (<c>0x800307E8</c>), the first thing of the END of <see cref="MovePlayer"/> (<c>0x80032914</c>) on every exit. E20.c1b K2 ported its steps 2 to 5, E20.c2a T2 the others:
    /// <list type="number">
    /// <item><description>step 1, the boots level (<see cref="BootsLevelOf"/>), stored in <see cref="AlundraEquipmentRecords.BootsLevel"/>;</description></item>
    /// <item><description>steps 2 to 5, the three equipment records (<see cref="FillEquipmentRecords"/>);</description></item>
    /// <item><description>step 6, the hero's defence sheet (<see cref="ComputeHeroSheet"/>);</description></item>
    /// <item><description>step 7, the regeneration (<see cref="RegenerateLife"/>), which reads the hold word of the pad.</description></item>
    /// </list>
    /// A host without the seam, tables or records does steps 1, 6 and 2-5 nothing (a bare test montage); the regeneration then finds no record and only keeps its counters at 0.
    /// </summary>
    internal static void UpdateItemEffectState(AlundraEntityScriptProxy player, in AlundraPadState pad, AlundraGameState state, IAlundraScriptHost? host)
    {
        RefreshEquipmentAndSheet(player, state, host);
        RegenerateLife(player, in pad, state, host?.EquipmentRecords);
    }

    /// <summary>
    /// Steps 1 to 6 of <c>UpdateItemEffectState</c>, the part that does not touch the hero's life: the boots level, the three records, the sheet. Also run once when the hero is adopted by a world
    /// (<see cref="AlundraWorldProxy"/>, E20.c2a T2): the binary's sheet is a global that outlives the map load (the load only empties the record pointers), so a hit on the first tick of a map reads the sheet of the
    /// end of the previous map; this port, which rebuilds the world's records at every map, computes it again at the adoption from the same inventory instead of reading an empty sheet.
    /// </summary>
    internal static void RefreshEquipmentAndSheet(AlundraEntityScriptProxy player, AlundraGameState state, IAlundraScriptHost? host)
    {
        if (host?.EquipmentRecords is not { } records)
        {
            return;
        }

        records.BootsLevel = BootsLevelOf(state); // step 1, 0x800307EC-0x80030890.

        if (host.ItemTables is { } itemTables && host.BalanceTables is { } balanceTables)
        {
            FillEquipmentRecords(state, itemTables, balanceTables, host.BalanceThreshold, records);
            ComputeHeroSheet(player, records);
        }
    }

    /// <summary>
    /// Step 1 of <c>UpdateItemEffectState</c> (<c>0x800307EC-0x80030890</c>): the boots level <c>[0x80127000]</c>, 3 when the object <c>0x1C</c> is owned, else 2 for <c>0x1B</c>, else 1 for <c>0x1A</c>, else 0
    /// (<see cref="GetNumberOfItem"/>, <c>0x8004E428</c>); the object <c>0x19</c> does not count. Shared with the physics of the DLL (<see cref="AlundraScriptedMotion"/>).
    /// </summary>
    internal static int BootsLevelOf(AlundraGameState state)
    {
        if (GetNumberOfItem(state, 0x1C) != 0)
        {
            return 3;
        }

        if (GetNumberOfItem(state, 0x1B) != 0)
        {
            return 2;
        }

        return GetNumberOfItem(state, 0x1A) != 0 ? 1 : 0;
    }

    /// <summary>
    /// Step 6 of <c>UpdateItemEffectState</c> (<c>0x800309A8-0x80030C18</c>): the hero's sheet is a copy of the record of the armour, or of the hero's own record (<c>+0x1C4</c>, <see cref="AlundraEntityScriptProxy.BalanceRecord"/>)
    /// when no armour is owned, whose responses 1 to 11 (bytes 3 to 13) are then combined with those of the weapon record and of the accessory record by <see cref="CombineResponse"/>. A missing weapon or accessory record
    /// counts as <c>0x10</c>. Written into <see cref="AlundraEquipmentRecords.HeroSheet"/>, one reusable record.
    /// </summary>
    internal static void ComputeHeroSheet(AlundraEntityScriptProxy hero, AlundraEquipmentRecords records)
    {
        var source = records[AlundraEquipmentRecords.Armour] ?? hero.BalanceRecord;
        if (source == null)
        {
            return;
        }

        var weapon = records[AlundraEquipmentRecords.Weapon];
        var accessory = records[AlundraEquipmentRecords.Accessory];
        Span<byte> responses = stackalloc byte[11];
        for (var i = 1; i <= 11; i++)
        {
            var offset = 2 + i;
            responses[i - 1] = (byte)CombineResponse(source.ByteAt(offset), weapon?.ByteAt(offset) ?? 0x10, accessory?.ByteAt(offset) ?? 0x10);
        }

        records.StoreHeroSheet(source, responses);
    }

    /// <summary>
    /// The combination of one response of the sheet (<c>0x80030B8C-0x80030C0C</c>): <paramref name="armour"/> is the byte of the copied sheet, <paramref name="weapon"/> and <paramref name="accessory"/> those of the two
    /// other records (<c>0x10</c> when absent). The flags are the two top bits (<c>&amp; 0xC0</c>). Any flag <c>0x80</c> gives <c>0x80</c>. Otherwise the value starts at the armour byte, or at <c>0x10</c> when the
    /// armour byte carries a flag (and then <c>0x40</c> as it is when the weapon and the accessory carry one too); each of the weapon and the accessory without flag adds its byte minus <c>0x10</c>; the result is
    /// capped at <c>0x1F</c> and floored at 0. Fuzzed against the real code in the discovery of E20.c2 (44,000 cases, no difference).
    /// </summary>
    internal static int CombineResponse(int armour, int weapon, int accessory)
    {
        var flagArmour = armour & 0xC0;
        var flagWeapon = weapon & 0xC0;
        var flagAccessory = accessory & 0xC0;
        if (flagArmour == 0x80 || flagWeapon == 0x80 || flagAccessory == 0x80)
        {
            return 0x80;
        }

        var value = armour;
        if (flagArmour != 0)
        {
            if (flagWeapon != 0 && flagAccessory != 0)
            {
                return 0x40;
            }

            value = 0x10;
        }

        if (flagWeapon == 0)
        {
            value += weapon - 0x10;
        }

        if (flagAccessory == 0)
        {
            value += accessory - 0x10;
        }

        return value >= 0x20 ? 0x1F : value < 0 ? 0 : value;
    }

    /// <summary>The bits of the control flags (<c>[0x800DC4B8] &amp; 0x7C</c>) that close the regeneration (<c>0x80030C2C</c>).</summary>
    private const uint RegenerationClosedFlags = 0x7C;

    /// <summary>
    /// Step 7 of <c>UpdateItemEffectState</c>, the regeneration (<c>0x80030C20-0x80030D3C</c>). Its gates, all of which must hold: no bit of <c>0x7C</c> in the control flags, the hero not frozen (<c>+0x20</c>,
    /// <see cref="AlundraEntityScriptProxy.BlockedByEntity"/>), the hold word of the pad (<c>0x80126F28</c>, a halfword: every button and the D-pad) zero, the life not 0 and below the maximum. When a gate is closed
    /// the three counters (<see cref="AlundraGameState.RegenerationCounters"/>) go back to 0 and the life is left alone. Otherwise, for each of the three records with a byte 2 (the period, <see cref="AlundraBalanceRecord.MaxHp"/>)
    /// other than 0, the counter grows by one and, when it passes the byte 2, goes back to 0 and the life gains 1 (so a period of byte 2 + 1 ticks; a record without a byte 2 zeroes its counter); the life is then
    /// capped at the maximum.
    /// </summary>
    internal static void RegenerateLife(AlundraEntityScriptProxy hero, in AlundraPadState pad, AlundraGameState state, AlundraEquipmentRecords? records)
    {
        var counters = state.RegenerationCounters;
        if ((state.PlayerControlFlags & RegenerationClosedFlags) != 0
            || hero.BlockedByEntity != null
            || (pad.ButtonsHold & 0xFFFF) != 0
            || hero.Hp == 0
            || hero.Hp >= hero.HpMax)
        {
            counters[0] = 0;
            counters[1] = 0;
            counters[2] = 0;
            return;
        }

        var life = hero.Hp;
        for (var slot = 0; slot < AlundraEquipmentRecords.Count; slot++)
        {
            var record = records?[slot];
            if (record != null && record.MaxHp != 0)
            {
                counters[slot]++;
                if (record.MaxHp < counters[slot])
                {
                    counters[slot] = 0;
                    life++;
                }
            }
            else
            {
                counters[slot] = 0;
            }
        }

        hero.Hp = hero.HpMax < life ? hero.HpMax : life;
    }

    /// <summary>The "armour" category of the equipment records: <c>byte[0x800C5F7C + 8 n + 6] &amp; 0x7F</c> (<see cref="AlundraItemTables.DropField3"/>, equal to the binary on the 98 rows).</summary>
    internal const int ArmourCategory = 2;

    /// <summary>The "weapon" category of the equipment records.</summary>
    internal const int WeaponCategory = 1;

    /// <summary>The "accessory" category of the equipment records.</summary>
    internal const int AccessoryCategory = 3;

    /// <summary>The highest item id an equipment record can describe (<c>0x61</c>; <c>0x800445C0</c> guards <c>n &lt; 0x62</c>).</summary>
    internal const int LastEquipmentItem = 0x61;

    /// <summary>
    /// Steps 2 to 5 of <c>UpdateItemEffectState</c> (<c>0x800307E8</c>), E20.c1b K2, docs/plan-e20-heros.md section 1.6: the six words <c>0x80146E00-0x80146E14</c> are cleared, then
    /// <list type="number">
    /// <item><description>record 0 is the armour: the owned item of the highest number whose category (<see cref="AlundraItemTables.DropField3"/> <c>&amp; 0x7F</c>) is 2, scanning <c>0x61</c> down to 0;</description></item>
    /// <item><description>record 1 is the weapon: <see cref="GetItemIdFromCurrentWeapon"/> when it answers 1 to <c>0x61</c> with the category 1;</description></item>
    /// <item><description>record 2 is the accessory: <see cref="SetItemIdFromCurrentItemId"/> (which also rewrites <c>ItemId</c>, as the binary's <c>0x8004E0F8</c> does: the tick is a second caller beside the HUD) when it answers 1 to
    /// <c>0x61</c> with the category 3.</description></item>
    /// </list>
    /// The record of item <c>n</c> is <see cref="AlundraBalanceTables.SelectEquipment"/>, with NO remapping of the index (the sprite lookup remaps <c>0x1F-0x7F</c> to <c>0x1E</c>, the equipment lookup does not).
    /// </summary>
    internal static void FillEquipmentRecords(AlundraGameState state, AlundraItemTables itemTables, AlundraBalanceTables balanceTables, int threshold, AlundraEquipmentRecords records)
    {
        records.ClearAll(); // 0x800308A4-0x800308BC: all six words, the item words too (the map load, 0x80044520, empties the pointers only: records.Clear()).

        for (var n = LastEquipmentItem; n >= 0; n--)
        {
            if (GetNumberOfItem(state, n) > 0 && (itemTables.DropField3[n] & 0x7F) == ArmourCategory)
            {
                records.Set(AlundraEquipmentRecords.Armour, balanceTables.SelectEquipment(n, threshold), n);
                break;
            }
        }

        var weapon = GetItemIdFromCurrentWeapon(state, itemTables);
        if (weapon is >= 1 and <= LastEquipmentItem && (itemTables.DropField3[(int)weapon] & 0x7F) == WeaponCategory)
        {
            records.Set(AlundraEquipmentRecords.Weapon, balanceTables.SelectEquipment((int)weapon, threshold), (int)weapon);
        }

        var accessory = SetItemIdFromCurrentItemId(state, itemTables);
        if (accessory is >= 1 and <= LastEquipmentItem && (itemTables.DropField3[(int)accessory] & 0x7F) == AccessoryCategory)
        {
            records.Set(AlundraEquipmentRecords.Accessory, balanceTables.SelectEquipment((int)accessory, threshold), (int)accessory);
        }
    }

    /// <summary>E20.a A4 stub (D-E20-14) of the item-in-use driver <c>0x8003634C</c> at its second site (<c>0x80031CC4</c>, every normal tick): the binary returns 1 without doing anything
    /// when no item is in use. Ported in E20.c2.</summary>
    private static void UpdateItemUseDriver(AlundraEntityScriptProxy player)
    {
    }

    /// <summary>
    /// E19.d2c2 S1: the common tail of the Idle/Moving and jump states (binary <c>0x80031EA8</c>), in this order. In the air (the controller's ground contact
    /// when the hero has a controller, <see cref="AlundraEntityScriptProxy.HeroAirborne"/> for a bare proxy of a test): 0x2C with a direction held, else 0x2D.
    /// On the ground: the edge of the Cross (<c>ButtonsJustPressed</c>) takes off - the walking jump (2) with a direction held, else the standing one (0x2B) -
    /// unless the cells forbid it (<c>VramOR &amp; 0x4000</c>: the case ends, nothing changes, not even the Moving); otherwise
    /// a held Triangle (E20.d1) starts the preparation of the run; otherwise Moving or Idle. <c>TryUseItem</c> and <c>PlayerTryAction</c> are the stubs of E20.a (the use of an item and lifting are not ported yet). The
    /// <c>LoadingMap</c> case reads the same predicate (E19.v, <see cref="IsHeroInTheAir"/>).
    /// </summary>
    private static void ApplyGroundOrAirTail(AlundraEntityScriptProxy player, in AlundraPadState pad, uint buttonsHold)
    {
        // E20.a A4, binary 0x80031EA8: PlayerTryAttack first (non-zero ends the tick, so the Square wins over the Cross); <see cref="PlayerTryAttack"/> starts the swing of the equipped weapon (E20.b).
        if (PlayerTryAttack(player, in pad) != 0)
        {
            return;
        }

        var airborne = IsHeroInTheAir(player);
        if (airborne)
        {
            player.TargetAnimationId = buttonsHold != 0 ? AirMovingAnimationId : AirStillAnimationId;
            return;
        }

        if ((pad.ButtonsJustPressed & AlundraPadState.Cross) != 0)
        {
            if ((player.CombinedVramFlagsOR & NoJumpVramFlag) != 0)
            {
                return;
            }

            player.TargetAnimationId = buttonsHold != 0 ? JumpWalkingAnimationId : JumpStandingAnimationId;
            return;
        }

        // E20.d1 T2, binary 0x80031F14-0x80031F30: a HELD Triangle (not an edge) starts the preparation of the run, with the counter of animation ends at 0 (the preparation lasts until the first loop of its animation
        // turns that counter). Not on deep water, which never reaches this tail (the swim branch of MovePlayer ends the tick before).
        if ((pad.ButtonsHold & AlundraPadState.Triangle) != 0)
        {
            player.TargetAnimationId = PrepareSprintAnimationId;
            player.AnimCompleteCounter = 0;
            return;
        }

        player.TargetAnimationId = buttonsHold != 0 ? MovingAnimationId : IdleAnimationId;
    }

    /// <summary>E19.v V-R2 (docs/plan-e19-opcodes.md section 1.2z): "in the air" for the two sites of the binary that read the hero's
    /// <c>IsOnGround</c> (+0x144), the <c>LoadingMap</c> case (0x800325E8) and the common tail (0x80031EBC): the controller's ground contact
    /// when the hero has a controller, <see cref="AlundraEntityScriptProxy.HeroAirborne"/> for a bare proxy of a test.</summary>
    private static bool IsHeroInTheAir(AlundraEntityScriptProxy player)
        => player.Controller != null ? player.IsOnGround == 0 : player.HeroAirborne;

    /// <summary>E19.v2 (docs/plan-e19-opcodes.md section 1.2z, V-R4): the binary's <c>IsOnGround</c> is the floor rule (the hero is
    /// on the ground when its Z is not above the floor, 0x800380F8). At map entry the DLL's <see cref="AlundraEntityScriptProxy.IsOnGround"/>
    /// comes from the engine controller, which has not resolved the ground yet on the first frame of a freshly placed pawn: on a
    /// New Game the first <see cref="MovePlayer"/> runs before the intro's lock (the map events pass after the entities in the DLL)
    /// with <c>IsGrounded</c> false and the logical Z still under the 80 px deck, and the LoadingMap case wrongly took the jump pose,
    /// which the lock then froze. A hero at or under its terrain is not in the air. A bare proxy of a test (no controller) keeps
    /// <see cref="IsHeroInTheAir"/> alone.</summary>
    private static bool IsAboveItsTerrain(AlundraEntityScriptProxy player)
        => player.Controller == null || player.PosZ > player.ComputeTerrainHeight();

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

        // E19.d2c2 F4: the climb takes the vertical from the state the logic tick holds (a jump whose foot touches the ladder's ground before the strict landing reaches this
        // portal while the state is up): the state ends first, the engine values it captured go back (its StepHeight above all), then the climb claims them below.
        if (player.HeroAirborne)
        {
            AlundraScriptedMotion.RestoreAirborneEngineValues(player, player.Controller);
            player.HeroAirborne = false;
            player.HeroFlyMarked = false;
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
    /// T4 ([R6] reserve #1, docs/plan-transitions-carte.md §3 T4/T2's own closing-verifier note): the
    /// original freezes its WHOLE entity pipeline - physics included - the instant a warp departure
    /// starts (§1.2.e: <c>AdvanceWarpTransitionFrame</c> replaces <c>UpdateEntities</c> outright, for
    /// every entity, not just the scripted ones this DLL's own gel gate already covers). This port's
    /// hero instead owns a continuous engine-driven <c>CharacterControllerComponent.Settings.Gravity</c>/
    /// <c>MaxFallSpeed</c> integrator that the gel gate (<see cref="AlundraGameState.PlayerControlBits.GameplayBlockedMask"/>/
    /// <see cref="AlundraWarpDirector.IsTransitionInProgress"/>) never touches - so a departure triggered
    /// mid-air would keep falling for the whole 16-tick outgoing fade, importing that fall into the
    /// arrival pose. CHOSEN FIX (not a deviation): reuse the exact same DLL-only mechanism
    /// <see cref="SuspendGravityForClimb"/> already proves safe (zero Gravity/MaxFallSpeed +
    /// <c>IsVerticalOwnedExternally</c>, no engine change) for the duration of the departure. No
    /// restore counterpart, unlike <see cref="RestoreGravityAfterClimb"/>: this entity and its whole
    /// world are torn down by the very map switch that ends the transition (§1.3.c), so nothing outlives
    /// it to hand the flag back to.
    /// </summary>
    internal static (float Gravity, float MaxFallSpeed, bool VerticalOwnedExternally)? SuspendGravityForWarpDeparture(
        AlundraEntityScriptProxy player)
    {
        if (player.Controller == null)
        {
            return null;
        }

        // Returned so an ABORTED departure can put them back (see RestoreGravityAfterAbortedWarpDeparture).
        // A departure that completes needs no restore: the map switch builds a fresh pawn, and
        // AdoptPlayerPawn rewrites both values unconditionally from the arrival map's own properties.
        var previous = (
            player.Controller.Settings.Gravity,
            player.Controller.Settings.MaxFallSpeed,
            player.Controller.IsVerticalOwnedExternally);

        player.Controller.Settings.Gravity = 0f;
        player.Controller.Settings.MaxFallSpeed = 0f;
        player.Controller.IsVerticalOwnedExternally = true;
        return previous;
    }

    /// <summary>Puts back what <see cref="SuspendGravityForWarpDeparture"/> took away, for the one path
    /// where no map switch will ever do it: a departure this session cannot finish (see
    /// <c>AlundraWarpDirector</c>'s own abort guard). Without it the hero would keep walking on the
    /// departure map with gravity permanently at zero.</summary>
    internal static void RestoreGravityAfterAbortedWarpDeparture(
        AlundraEntityScriptProxy player,
        (float Gravity, float MaxFallSpeed, bool VerticalOwnedExternally) previous)
    {
        if (player.Controller == null)
        {
            return;
        }

        player.Controller.Settings.Gravity = previous.Gravity;
        player.Controller.Settings.MaxFallSpeed = previous.MaxFallSpeed;
        player.Controller.IsVerticalOwnedExternally = previous.VerticalOwnedExternally;
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
    /// (<see cref="AlundraEntityScriptProxy.XCollisionEntity"/>, written by the blocking report of his controller in
    /// <see cref="AlundraEntityScriptProxy.MoveControllerAndPullPosition"/>, E19.d2b B4), maintains the interact latch on <paramref name="state"/> (see the latch fields'
    /// own doc there), and on success assigns <see cref="IAlundraScriptHost.ActiveCollisionEntity"/> -
    /// the one-shot signal the slot-F pick consumes (D-E12D-4). Returns the original's exact result
    /// codes: 0 = no interaction, 1 = auto-touch interact (no InteractRequiresButton flag),
    /// 2 = button interact (flag + Square just pressed).
    ///
    /// Two deliberate, documented gates the original carries UPSTREAM instead:
    /// <paramref name="host"/> null = "no world" - skipped (degraded; see MovePlayer's own doc);
    /// GameplayBlockedMask posed - skipped, the narrowest equivalent of the original's whole-pipeline
    /// gate at EntityManager.cs:377 (with a MenuOpen box up, neither MovePlayer nor the pick nor the
    /// physics ran at all there). CORRECTED (E13.d D4, docs/plan-e13d-inventaire.md §1.2): the port DOES
    /// have a global gate for this since T2 - <see cref="AlundraEntityScriptProxy.Update"/>'s own
    /// <c>gameplayBlocked</c> computation (AlundraEntityScriptProxy.cs:828-867) only calls
    /// <c>RunGameplayBlockableUpdate</c> - where <see cref="MovePlayer"/> itself lives - when it is
    /// false; this method's own repeated check just carries the same gate one level deeper, narrowly,
    /// the way D-E12D-5 chose - not because the port lacked one.
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
            // E19.m12 (M-33): the binary compares the slot serial first (0x8002E9B4-0x8002E9C0); a wiped slot
            // (state 0, serial 0, all 0x294 bytes rewritten by the template) never matches, and the mismatch
            // takes 0x8002E9C0 -> 0x8002EA78 (latch cleared, contact used). The DLL never assigns Index2 and
            // RecycleDestroyedEntities keeps Flags/ProgramIndexes/positions, so the Index2 comparison cannot
            // catch a recycled proxy: Status == Destroyed (the state the recycler leaves) refuses it instead,
            // through the same fall-through. FlagToDestroy is deliberately NOT refused (not proven faithful).
            if (state.InteractLatchEntity == null ||
                (state.InteractLatchEntity.Status != EntityStatus.Destroyed &&
                 state.InteractLatchEntity.Index2 == state.InteractLatchFacing &&
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

    // ---------------------------------------------------------------------------------------------
    // E13 C0 (docs/plan-e13-hud.md): the five player-stat setters, ported from PlayerManager.cs's own
    // SetPlayerHpMax/SetPlayerHp/SetPlayerMpMax/SetPlayerMp/SetMoney (address comments kept from that
    // file). The IsGodMode overrides inside SetPlayerHpMax/SetPlayerHp (PlayerManager.cs:1725-1728,
    // :1751-1754) are OUT OF SCOPE for C0 (docs/plan-e13-hud.md, C0's own "hors périmètre") - this DLL
    // carries no IsGodMode flag yet, so there is nothing to gate on.
    // ---------------------------------------------------------------------------------------------

    /// <summary>PlayerManager.SetPlayerHpMax (0x8004dc68, PlayerManager.cs:1705-1731, IsGodMode override
    /// at :1725-1729 excluded, see this method group's own header comment). Ceiling 50 (0x32/0x33 in the
    /// original: any value &gt;= 51 becomes 50), floor 0.</summary>
    public static int SetPlayerHpMax(AlundraGameState state, int hpMax)
    {
        var stats = state.PlayerStats;

        if (hpMax < 0x33)
        {
            stats.HpMax = hpMax < 0 ? (short)0 : (short)hpMax;
        }
        else
        {
            stats.HpMax = 0x32;
        }

        return stats.HpMax;
    }

    /// <summary>
    /// The life side of <c>IncreaseHpMaxAndCreateEffect</c> (0x800333AC, E19.g G3d, ADR-0048 amended by ADR-0056). For a hero that carries its life (<see cref="AlundraEntityScriptProxy.LifePulledFromStats"/>, every hero of a
    /// world), as the binary does (<c>0x800333D0-0x800333E4</c>): <c>HpMax += 1</c> with NO cap and <c>Hp = HpMax</c>, on the entity; the stats follow at the end of the next <see cref="MovePlayer"/> through the
    /// clamps of <see cref="SetPlayerHpMax"/> and <see cref="SetPlayerHp"/> (a vessel at 50/50 gives an entity of 51/51 and stats of 50/50). Without such a hero (no hero, or the bare proxy of a test montage, whose
    /// life is never mirrored) the stats are raised at once through the same setters, the former seam: the maximum + 1 capped at 50, the life filled to the raised value.
    /// </summary>
    public static void IncreaseHpMaxAndFill(AlundraGameState state, AlundraEntityScriptProxy? hero = null)
    {
        if (hero is { LifePulledFromStats: true })
        {
            hero.HpMax += 1;
            hero.Hp = hero.HpMax;
            return;
        }

        var raised = state.PlayerStats.HpMax + 1;
        SetPlayerHpMax(state, raised);
        SetPlayerHp(state, (short)raised);
    }

    /// <summary>
    /// Opcode <c>0xBC</c> (<c>Script_188_0BC</c>, <c>0x80041C00</c>): the hero's maximum life goes up by the operand byte, capped at <c>0x32</c> (<c>slti 0x33</c>); the life is not touched. Written on the entity of a
    /// hero that carries its life; otherwise (no hero, a bare test proxy) on the stats through <see cref="SetPlayerHpMax"/>, which has the same cap.
    /// </summary>
    public static void IncreaseHpMax(AlundraGameState state, AlundraEntityScriptProxy? hero, int amount)
    {
        if (hero is { LifePulledFromStats: true })
        {
            var raised = hero.HpMax + amount;
            hero.HpMax = raised < 0x33 ? raised : 0x32;
            return;
        }

        SetPlayerHpMax(state, state.PlayerStats.HpMax + amount);
    }

    /// <summary>
    /// Opcode <c>0xC3</c> (<c>Script_195_0C3</c>, <c>0x80041D6C</c>): <c>Hp = HpMax</c> on the hero entity (<c>0x80041D7C-0x80041D84</c>), <c>SetPlayerMp(GetPlayerMpMax())</c>, then <c>InitializeHpAndMp</c>
    /// (<c>0x8004B730</c>): the four displayed counters of the HUD (<see cref="AlundraHudDirector"/>) jump to the maxima of the stats, so the hearts and pips fill at once instead of rolling up. Without a hero that
    /// carries its life the stats are filled instead (<see cref="SetPlayerHp"/> to the stats' own maximum).
    /// </summary>
    public static void RestoreLifeAndMagic(AlundraGameState state, AlundraEntityScriptProxy? hero)
    {
        if (hero is { LifePulledFromStats: true })
        {
            hero.Hp = hero.HpMax;
        }
        else
        {
            SetPlayerHp(state, state.PlayerStats.HpMax);
        }

        SetPlayerMp(state, state.PlayerStats.MpMax);
        AlundraHudDirector.Instance.SnapDisplayedLifeAndMagic(state.PlayerStats);
    }

    /// <summary>PlayerManager.SetPlayerHp (0x8004dd30, PlayerManager.cs:1734-1761, IsGodMode override at
    /// :1751-1755 excluded). Clamped to
    /// <c>[0, HpMax]</c> - not to a fixed constant, to whatever <see cref="AlundraPlayerStats.HpMax"/>
    /// currently holds.</summary>
    public static int SetPlayerHp(AlundraGameState state, short amount)
    {
        var stats = state.PlayerStats;

        if (stats.HpMax < amount)
        {
            stats.Hp = stats.HpMax;
        }
        else if (amount < 0)
        {
            stats.Hp = 0;
        }
        else
        {
            stats.Hp = amount;
        }

        return stats.Hp;
    }

    /// <summary>PlayerManager.SetPlayerMpMax (0x8004ddf4, PlayerManager.cs:1791-1811). Ceiling 4 (any
    /// value &gt;= 5 becomes 4), floor 0, clamped on both sides per docs/plan-e13-hud.md §1.5.</summary>
    public static int SetPlayerMpMax(AlundraGameState state, short mpMax)
    {
        var stats = state.PlayerStats;

        if (mpMax < 5)
        {
            stats.MpMax = mpMax < 0 ? (short)0 : mpMax;
        }
        else
        {
            stats.MpMax = 4;
        }

        return stats.MpMax;
    }

    /// <summary>PlayerManager.SetPlayerMp (0x8004debc, PlayerManager.cs:1813-1829). Clamped to <c>[0,
    /// MpMax]</c>, same shape as
    /// <see cref="SetPlayerHp"/>.</summary>
    public static int SetPlayerMp(AlundraGameState state, short amount)
    {
        var stats = state.PlayerStats;

        if (stats.MpMax < amount)
        {
            stats.Mp = stats.MpMax;
        }
        else if (amount < 0)
        {
            stats.Mp = 0;
        }
        else
        {
            stats.Mp = amount;
        }

        return stats.Mp;
    }

    /// <summary>PlayerManager.SetMoney (0x8004df80, PlayerManager.cs:1671-1690). Clamped to
    /// <c>[0, 9999]</c> - the original checks <c>amount &lt; 10000</c> then floors negative amounts at 0;
    /// since <paramref name="amount"/> never legitimately exceeds 9999 in play, the net effect is the
    /// symmetric clamp docs/plan-e13-hud.md §1.5 describes.</summary>
    public static int SetMoney(AlundraGameState state, short amount)
    {
        var stats = state.PlayerStats;

        if (amount < 10000)
        {
            stats.Money = amount < 0 ? (short)0 : amount;
        }
        else
        {
            stats.Money = 9999;
        }

        return stats.Money;
    }

    /// <summary>New Game stat reset - port of the <c>SlotData == 0</c> branch's own five setter calls
    /// (GameInitializer.cs:372-376): <c>SetPlayerHpMax(10); SetPlayerHp(10); SetPlayerMpMax(0);
    /// SetPlayerMp(0); SetMoney(0)</c>. <see cref="AlundraPlayerStats"/>'s own field initializers already
    /// match this exact outcome, so this method exists for callers that need to reset stats on an
    /// ALREADY-CONSTRUCTED <see cref="AlundraGameState"/> (e.g. a New-Game flow reusing the session
    /// singleton) rather than relying on construction-time defaults.</summary>
    public static void InitializeNewGameStats(AlundraGameState state)
    {
        SetPlayerHpMax(state, 10);
        SetPlayerHp(state, 10);
        SetPlayerMpMax(state, 0);
        SetPlayerMp(state, 0);
        SetMoney(state, 0);
    }

    // ---- E13.c S3 (docs/plan-e13c-icones-hud.md): the item counters, the equipped weapon, and what the
    // HUD's two equipment boxes show. Everything below is a line-by-line port of the cited original; the
    // only systematic change is Breakpoint.TriggerBreak(), which becomes a warning here, the port's idiom.

    /// <summary>Port of <c>g_itemsCount = 99</c>, set by <c>InitializePlayerStatsAndItems</c>
    /// (GameInitializer.cs:470) and never changed afterwards - the bound item ids are checked against by
    /// <see cref="GetNumberOfItem"/>, <see cref="SetCurrentItemId"/> and <see cref="AddOneItemIfUnlocked"/>.
    /// One below the table's 100 rows, exactly as in the original.</summary>
    public const int ItemsCount = 99;

    /// <summary>The original's own "none" answer to an item or weapon lookup, <c>0xffffffff</c>.</summary>
    public const uint NoItem = 0xffffffff;

    /// <summary>
    /// The New Game inventory: the counter reset of <c>InitializePlayerStatsAndItems</c>
    /// (GameInitializer.cs:473-479), then the tail of <c>InitializeGameState</c>'s New Game branch
    /// (GameInitializer.cs:395-411) - every item whose drop record carries the unlock bit is given once
    /// (items 1, 17 and 25 in the shipped data), then the weapon is set to slot 1, the sword's.
    /// <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> runs this once per session, at the New Game entry.
    /// </summary>
    public static void InitializeNewGameInventory(AlundraGameState state, AlundraItemTables tables)
    {
        // GameInitializer.cs:473-479: 0x80 pairs, both halves zeroed.
        Array.Clear(state.NumberOfItems);

        // GameInitializer.cs:400-408.
        var iconIndex = 0;
        do
        {
            var value = tables.DropField3[iconIndex];
            if ((value & 0x80) != 0)
            {
                AddOneItemIfUnlocked(state, tables, iconIndex);
            }

            iconIndex += 1;
        }
        while (iconIndex < 0x62);

        // GameInitializer.cs:410 - outside the real-game / debug if/else, so it always runs.
        SetPlayerWeaponId(state, tables, 1);
    }

    /// <summary>Port of <c>SetPlayerWeaponId</c> (PlayerManager.cs:1832-1845, 8004e484): stores -1 (no weapon) or
    /// a weapon slot 1..6, rejects everything else (0 included), and ends, as the original does, by resolving the
    /// weapon's item - a lookup whose result the original discards too.</summary>
    public static void SetPlayerWeaponId(AlundraGameState state, AlundraItemTables tables, int weaponId)
    {
        // ALUN_CD.EXE (France) 0x8004e484 settles docs/plan-e13c-icones-hud.md open point 7: the argument is a
        // 32-bit int; -1 is stored (beq at 0x8004e490), otherwise (weaponId - 1) <u 6 must hold (sltiu at
        // 0x8004e49c), so 0 is rejected and logged ("SetEquipWeaponKind", 0x80026620). The decompilation's
        // ushort parameter inverted both (never -1, 0 accepted) - plan E13.d SI9.b.
        if (weaponId == -1 || (uint)(weaponId - 1) < 6)
        {
            state.PlayerStats.WeaponId = (short)weaponId;
        }

        GetItemIdFromCurrentWeapon(state, tables);
    }

    /// <summary>Port of <c>GetItemIdFromCurrentWeapon</c> (PlayerManager.cs:4462-4466, 8004e030): the item
    /// the equipped weapon slot resolves to, or <see cref="NoItem"/>.</summary>
    public static uint GetItemIdFromCurrentWeapon(AlundraGameState state, AlundraItemTables tables)
    {
        return GetWeaponIdBySlotId(state, tables, state.PlayerStats.WeaponId - 1);
    }

    /// <summary>Port of <c>GetWeaponIdBySlotId</c> (PlayerManager.cs:4433-4460). Each case of the original
    /// calls its own <c>GetWeaponIdFromSlotN</c> (PlayerManager.cs:4468-4496), every one of which is nothing
    /// but <c>GetItemIdFromSlotId(N)</c>; the call is written inline here, the case-to-slot mapping kept.</summary>
    public static uint GetWeaponIdBySlotId(AlundraGameState state, AlundraItemTables tables, int slotId)
    {
        var itemId = NoItem;

        switch (slotId)
        {
            case 0:
                itemId = GetItemIdFromSlotId(state, tables, 1);
                break;
            case 1:
                itemId = GetItemIdFromSlotId(state, tables, 2);
                break;
            case 2:
                itemId = GetItemIdFromSlotId(state, tables, 3);
                break;
            case 3:
                itemId = GetItemIdFromSlotId(state, tables, 4);
                break;
            case 4:
                itemId = GetItemIdFromSlotId(state, tables, 5);
                break;
            case 5:
                itemId = GetItemIdFromSlotId(state, tables, 6);
                break;
        }

        return itemId;
    }

    /// <summary>Port of <c>GetItemIdFromSlotId</c> (PlayerManager.cs:4369-4419, 8004e18c): among the owned
    /// items of an inventory slot, the one to use. The first owned match wins at once when its replacement
    /// flag is clear; otherwise a later owned match replaces it only on a strictly higher replacement
    /// priority. <see cref="NoItem"/> when nothing in the slot is owned.</summary>
    public static uint GetItemIdFromSlotId(AlundraGameState state, AlundraItemTables tables, uint slotId)
    {
        if (slotId >= 0x20)
        {
            CasaEngine.Core.Logging.Logs.WriteWarning($"AlundraPlayerManager: invalid slotId {slotId}.");
            return NoItem;
        }

        var properties = tables.ItemsProperties;
        uint bestMatchIndex = NoItem;
        uint currentIndex = 0;

        while (currentIndex < 100)
        {
            var entrySectionId = properties[(int)(currentIndex * 5)];

            if (entrySectionId == slotId)
            {
                var usageCount = state.NumberOfItems[currentIndex * 2 + 1];

                if (usageCount > 0)
                {
                    if (bestMatchIndex == NoItem)
                    {
                        bestMatchIndex = currentIndex;

                        var flags = properties[(int)(currentIndex * 5 + 1)];
                        if ((flags & 0x1) == 0)
                        {
                            return currentIndex;
                        }
                    }
                    else
                    {
                        var currentPriority = properties[(int)(currentIndex * 5 + 2)];
                        var bestPriority = properties[(int)(bestMatchIndex * 5 + 2)];

                        if (bestPriority < currentPriority)
                        {
                            bestMatchIndex = currentIndex;
                        }
                    }
                }
            }

            currentIndex++;
        }

        return bestMatchIndex;
    }

    /// <summary>Port of <c>GetNumberOfItem</c> (PlayerManager.cs:4330-4346, 8004e428): how many of an item
    /// the player owns, 0 for an id out of range.</summary>
    public static int GetNumberOfItem(AlundraGameState state, int itemId)
    {
        int nbItem;

        if (itemId < 0 || ItemsCount <= itemId)
        {
            nbItem = 0;
        }
        else
        {
            nbItem = state.NumberOfItems[itemId * 2 + 1];
        }

        return nbItem;
    }

    /// <summary>Port of <c>SetCurrentItemId</c> (PlayerManager.cs:4421-4431, 8004e4d8): equips an accessory,
    /// refusing an id out of range.</summary>
    public static void SetCurrentItemId(AlundraGameState state, uint itemId)
    {
        if ((int)itemId < 0 || itemId >= ItemsCount)
        {
            CasaEngine.Core.Logging.Logs.WriteWarning($"AlundraPlayerManager: invalid itemId {itemId} for SetCurrentItemId.");
            return;
        }

        state.PlayerStats.ItemId = (short)itemId;
    }

    /// <summary>
    /// Port of <c>SetItemIdFromCurrentItemId</c> (PlayerManager.cs:4350-4366, 8004e0f8), which the HUD calls
    /// every frame for the accessory box. When the equipped accessory is owned, it re-resolves the best item
    /// of that accessory's slot and equips it - a side effect, kept - then returns the accessory id it
    /// STARTED from, not the re-resolved one, exactly as the original does. <see cref="NoItem"/> when the
    /// equipped accessory is not owned, which is the New Game state: item 0, count 0, an empty box.
    /// </summary>
    public static uint SetItemIdFromCurrentItemId(AlundraGameState state, AlundraItemTables tables)
    {
        var currentItemId = state.PlayerStats.ItemId;
        var numberOfItem = GetNumberOfItem(state, currentItemId);

        if (numberOfItem == 0)
        {
            return NoItem;
        }

        var slotId = tables.ItemsProperties[currentItemId * 5];
        var itemId = GetItemIdFromSlotId(state, tables, slotId);
        SetCurrentItemId(state, itemId);

        return (uint)currentItemId;
    }

    /// <summary>Port of <c>AddOneItemIfUnlocked</c> (PlayerManager.cs:4666-4688, 8004e530): gives one more of
    /// an item unless the player already holds its max count (column 3). Returns the new count, or the
    /// unchanged count when it was already at its max - the binary's own value (8004e58c/8004e5b0: the count is
    /// returned); the decompilation returned the item id there, which made opcode 0x82 report "given" for nothing.</summary>
    public static int AddOneItemIfUnlocked(AlundraGameState state, AlundraItemTables tables, int itemId)
    {
        if (itemId < 0 || itemId >= ItemsCount)
        {
            CasaEngine.Core.Logging.Logs.WriteWarning($"AlundraPlayerManager: invalid itemId {itemId} in AddOneItemIfUnlocked.");
            return 0;
        }

        var itemIdIndex = itemId * 2;
        var numberOfItem = state.NumberOfItems[itemIdIndex + 1];
        var itemPropertyId = itemId * 5;
        var unlockRequirement = tables.ItemsProperties[itemPropertyId + 3];

        if (numberOfItem != unlockRequirement)
        {
            state.NumberOfItems[itemIdIndex + 1] = (short)(numberOfItem + 1);
            return numberOfItem + 1;
        }

        return numberOfItem;
    }

    /// <summary>Port of <c>UseItem</c> (8004e5c4, PlayerManager.cs:1871-1897): consumes one of an item. An id out of
    /// range answers 0 and writes nothing (the original's own quirk: a caller testing for -1 reads it as "used"); a count
    /// of 0 answers -1 and writes nothing; otherwise the count drops by one and the new count is returned.</summary>
    public static int UseItem(AlundraGameState state, int itemId)
    {
        if (itemId < 0 || itemId >= ItemsCount)
        {
            CasaEngine.Core.Logging.Logs.WriteWarning($"AlundraPlayerManager: invalid itemId {itemId} in UseItem.");
            return 0;
        }

        var numberOfItem = state.NumberOfItems[itemId * 2 + 1];

        if (numberOfItem == 0)
        {
            return -1;
        }

        state.NumberOfItems[itemId * 2 + 1] = (short)(numberOfItem - 1);
        return numberOfItem - 1;
    }

    /// <summary>Port of <c>AddMoney</c> (8004dfd8): <c>SetMoney(Money + amount)</c>. The sum is computed on 32 bits and
    /// clamped to <c>[0, 9999]</c> as <see cref="SetMoney"/> does - never cast to a <see cref="short"/> first, which
    /// would wrap a large amount into a negative one.</summary>
    public static int AddMoney(AlundraGameState state, int amount)
    {
        var sum = state.PlayerStats.Money + amount;
        state.PlayerStats.Money = (short)(sum < 0 ? 0 : sum >= 10000 ? 9999 : sum);
        return state.PlayerStats.Money;
    }

    /// <summary>Port of <c>GetItemTextureIdByItemId</c> (GraphicManager.cs:1910-1914, 8004e168): an item's
    /// icon column, the sprite record whose portrait is its icon (31 for the sword). The HUD does not go
    /// through it to find the icon's asset - the converter already resolved icon to asset, see
    /// <see cref="AlundraItemTables.TryGetIconAssetId"/> - but it is the link the tests pin, item 1 to 31.</summary>
    public static int GetItemTextureIdByItemId(AlundraItemTables tables, int itemId)
    {
        return tables.ItemsProperties[itemId * 5 + 4];
    }

    /// <summary>
    /// What the HUD's two equipment boxes show this frame - the two lookups
    /// <c>HudManager.DisplayHudWeaponAndItem</c> (HudManager.cs:490-602) makes every frame, in its order:
    /// the weapon's item first, then the accessory through <see cref="SetItemIdFromCurrentItemId"/>, side
    /// effect included. Each item that resolves is mapped to its portrait's asset id; a sentinel, or an item
    /// without a portrait, leaves its box empty.
    /// </summary>
    public static (Guid? Weapon, Guid? Accessory) ResolveHudEquipmentIcons(AlundraGameState state, AlundraItemTables tables)
    {
        Guid? weapon = null;
        var weaponItemId = GetItemIdFromCurrentWeapon(state, tables);
        if (weaponItemId != NoItem && tables.TryGetIconAssetId((int)weaponItemId, out var weaponAssetId))
        {
            weapon = weaponAssetId;
        }

        Guid? accessory = null;
        var accessoryItemId = SetItemIdFromCurrentItemId(state, tables);
        if (accessoryItemId != NoItem && tables.TryGetIconAssetId((int)accessoryItemId, out var accessoryAssetId))
        {
            accessory = accessoryAssetId;
        }

        return (weapon, accessory);
    }
}
