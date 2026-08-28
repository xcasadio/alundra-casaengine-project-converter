#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// Shared fixed-step (50 Hz, matching the original PSX build's own physics tick rate) kinematic mover -
/// extracted from <see cref="AlundraPlayerManager"/>'s own E2 hero tick (<c>RunOneTick</c>/<c>IncrementForce</c>,
/// PhysicsEngine.cs:1445-1446/1490-1491/1551-1598) so E4.b's scripted-NPC mover
/// (<see cref="AlundraEntityScriptProxy.Update"/>'s own <c>!IsPlayer</c> branch) can reuse the exact same
/// generic pieces - <see cref="IncrementForce"/> and <see cref="AnimationTables"/>' own offset tables -
/// without duplicating them, and WITHOUT changing the hero's own behaviour: <see cref="AlundraPlayerManager.Tick"/>
/// now simply calls <see cref="TickPlayer"/>, running the same body <c>RunOneTick</c> used to run inline,
/// once per logic tick.
///
/// ONE-CLOCK fix (user-reported stall, sailor entity 12 of map 389 stuck on opcode 0x1F at pc 1470 -
/// see the commit message for the full diagnosis): this class used to own its OWN per-entity 50 Hz
/// accumulator (<c>PhysicsTickAccumulator</c>, fed a raw <c>elapsedTime</c> every RENDERED frame),
/// completely independent of <see cref="AlundraLogicClock"/>, the SAME clock that already gates the
/// script (pick/run) pass. The two accumulators phase-drifted against each other (they only agree on the
/// long-run RATE, not on which rendered frame carries a tick) - <see cref="AlundraEntityScriptProxy.ForceAdjusted"/>
/// was cleared on a render frame that carried no motion sub-step and set only on a frame that did, so a
/// 0x1F (Walk with collision)'s own script-side read of <c>ForceAdjusted</c> usually saw a stale 0 and
/// never took its "movement was curtailed" exit - the entity walked into geometry and stalled forever.
/// <see cref="TickPlayer"/>/<see cref="TickScriptedNpc"/> no longer accumulate time themselves: the
/// caller (<see cref="AlundraEntityScriptProxy.Update"/>) passes the SAME <c>ticksThisFrame</c> count
/// <see cref="IAlundraScriptHost.LogicTicksThisFrame"/> already handed the script pass this frame, so one
/// logic tick is always exactly one script pass followed by one motion sub-step (and, for an NPC, one
/// <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/> vertical step) - never more, never fewer,
/// never on a different frame.
///
/// The ONE deliberate difference between the two callers is which field feeds the per-tick
/// <c>AnimSetsByAnim</c> lookup - <see cref="TickPlayer"/> keeps E2's own <c>TargetAnimationId</c> (out of
/// this chantier's scope to change, and still the closest available equivalent for a hero with no
/// <c>AnimationSet</c>-reassignment-site port of its own); <see cref="TickScriptedNpc"/> uses
/// <c>CurrentAnimationId</c> instead, matching the original's own <c>entity.AnimationSet</c> read (see
/// <see cref="AlundraEntityScriptProxy.Update"/>'s own E4.b doc for the full pre-read finding and the
/// documented one-frame-latency deviation this implies). <see cref="RunOneKinematicTick"/> below takes the
/// animation id as a parameter precisely so this one distinction stays a one-line difference at each call
/// site rather than two near-duplicate tick bodies.
///
/// No delegate/closure of any kind is used anywhere here (both callers inline their own tiny loop/call
/// instead of sharing one through an <c>Action</c>) - both run every frame for potentially many entities,
/// and a captured lambda would allocate per call, violating this codebase's no-per-frame-allocation rule
/// (see e.g. <see cref="AlundraEventProgramRunner"/>'s own <c>_fetchScratch</c> doc for the same
/// constraint applied elsewhere).
/// </summary>
internal static class AlundraScriptedMotion
{
    /// <summary>Original engine tick rate the PSX build ran physics at (50 Hz).</summary>
    internal const float FixedTickSeconds = 1f / 50f;

    /// <summary>Caps the catch-up run at this many 50 Hz steps per engine frame - see
    /// <see cref="AlundraPlayerManager"/>'s own (former) doc on this same constant for the full rationale
    /// (documented fixed-step deviation, docs/plan-conversion-totale.md §4 E2).</summary>
    internal const int MaxTicksPerFrame = 4;

    // EntityRecordMapper's own tile constants (StaticVariables.MapTileWidth/Height) - see
    // AlundraPlayerManager's own (former) duplicate of these for the same reasoning.
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    /// <summary>Fixed positive value <see cref="TickPlayer"/> latches through
    /// <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent.SetExternalVerticalDisplacement"/>
    /// for EVERY tick the hero spends Climbing/ClimbStill, regardless of the real signed
    /// <see cref="AlundraEntityScriptProxy.ForceZ"/> for that tick (verifier F1/F2 fix) - see that call
    /// site's own doc for the full rationale (the engine's own `UpdateGround` only reads this latch's SIGN,
    /// never its magnitude). Not a physical distance; any positive value would do.</summary>
    private const float ClimbingExternalDisplacementSentinel = 1f;

    /// <summary>Runs <paramref name="ticks"/> whole 50 Hz kinematic ticks for the hero pawn - the tick
    /// COUNT is owned entirely by the caller now (this class' own doc, ONE-CLOCK fix): it is always the
    /// same <c>ticksThisFrame</c> the shared <see cref="AlundraLogicClock"/> already handed the script
    /// pass this same frame, never a separately-accumulated value. Called from
    /// <see cref="AlundraPlayerManager.Tick"/>.</summary>
    internal static void TickPlayer(AlundraEntityScriptProxy player, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            RunOneMotionTick(player, player.TargetAnimationId);

            // E4 (docs/plan-echelles-chiffrage.md É4): the hero's vertical is normally the ENGINE's own
            // continuous Settings.Gravity/MaxFallSpeed integrator (AlundraWorldProxy.AdoptPlayerPawn,
            // E3.d) - deliberately UNCHANGED by this method (see this class' own doc, "no jump/gravity
            // system"). Climbing is the ONE documented exception: while AlundraPlayerManager.MovePlayer
            // set TargetAnimationId to Climbing/ClimbStill THIS frame (it also suspended
            // Controller.Settings.Gravity/MaxFallSpeed to 0 for the same duration, see
            // AlundraPlayerManager's own SuspendGravityForClimb), the ladder's own vertical step is pushed
            // through the controller HERE, once per LOGIC TICK (not once per rendered frame like
            // MovePlayer itself runs) - the exact reason player.ForceZ is a per-tick +-0x10000 (+-1px)
            // quantity, not a per-frame one: a render frame carrying 2 logic ticks climbs 2px this frame,
            // matching how the horizontal RunOneMotionTick call just above already applies its own
            // per-tick step ticks times. Reuses AlundraEntityScriptProxy.MoveVerticalAndPullPosition - the
            // SAME primitive EvaluateEntitySupport's own "not found" tail already uses for every
            // controller-driven scripted NPC's per-tick vertical (proven pattern - see that method's own
            // doc); wasSupportedEnteringThisTick is always false here because EntitySupport/
            // EvaluateEntitySupport is never called for the player (WasEntitySupportedLastTick stays false
            // for the hero the whole session - see that field's own doc), so this always takes the normal
            // "pull PosZ from the post-Move root" branch. ForceZ is 0 while ClimbStill (frozen) or at
            // either climbable boundary (see MovePlayer's own DESC/MONT "else" branches) - a harmless
            // zero-displacement Move() call in that case, still needed to re-pull PosZ from the root every
            // tick the SAME way the "moving" case does. This entire block is unreachable for every OTHER
            // TargetAnimationId (Idle/Moving/LoadingMap/anything not ported) - the hero's own four golden
            // traces never set TargetAnimationId to either climbing value (no ladder cell lies on any of
            // them), so this addition leaves them byte-identical.
            //
            // CORRECTED (verifier F1/F2): MoveVerticalAndPullPosition's own Controller.Move call alone is
            // NOT enough - it only advances the root along a per-tick displacement, it does not stop
            // CharacterControllerComponent.UpdateGround from re-snapping that same root back to the
            // ground field on the very next RENDERED frame's Update. AlundraPlayerManager.
            // SuspendGravityForClimb now also claims Controller.IsVerticalOwnedExternally for the
            // duration of the climb (see that method's own doc) - the SAME latched declaration every
            // controller-driven scripted NPC already makes each tick
            // (AlundraEntityScriptProxy.Update's own trailing SetExternalVerticalDisplacement call), so
            // UpdateGround treats the hero as airborne instead of re-grounding it while on the ladder.
            //
            // Declares a FIXED positive sentinel, not player.ForceZ/65536f (measured, verifier F1
            // regression): CharacterControllerComponent.UpdateGround's own gate
            // (`IsVerticalOwnedExternally && _externalVerticalDisplacement > 0f`) only treats a POSITIVE
            // declaration as airborne - "Ground resolution ... on non-rising ticks are unaffected" is
            // that method's own documented contract, i.e. a zero or negative declaration deliberately lets
            // the engine's normal ground-field snap run. That is exactly wrong for THIS state machine:
            // ClimbStill (pad released, ForceZ == 0, holding position mid-ladder) and DESC (ForceZ < 0)
            // are just as much "clinging to the wall, not resting on real terrain" as an ascending tick is
            // - the hero is never actually standing on the ground while Climbing/ClimbStill, regardless of
            // which way ForceZ currently points or whether it is momentarily zero. Measured: with the real
            // exported GroundSnapDistance (4.0px, F1's own fixture fix), declaring the true signed
            // ForceZ/65536f let a frozen ClimbStill 3px above the ladder's own ground height get silently
            // pulled back down to that ground height the very next frame (GroundSnapDistance covers the
            // gap) - defeating the freeze entirely. A fixed positive sentinel here is NOT a fabricated
            // physical value: grep of CharacterControllerComponent.cs confirms the ONLY reader of the
            // latched field this call sets is that single `> 0f` sign check (UpdateGround), never its
            // magnitude - so any positive constant conveys the exact same "airborne, owner-controlled"
            // signal AlundraPlayerManager.MovePlayer's own explicit FloorHeight/tileHeightAbove guards
            // (not this engine field) already own as the SOLE ground-detection authority for the whole
            // climb, matching the original PS1 code's own slope-switch logic (no continuous ground-field
            // physics at all while climbing). The REAL per-tick displacement/direction is still exactly
            // player.ForceZ, applied unchanged by MoveVerticalAndPullPosition just above - only the
            // separate airborne-signal latch uses this fixed value.
            if (player.TargetAnimationId is AlundraPlayerManager.ClimbingAnimationId or AlundraPlayerManager.ClimbStillAnimationId)
            {
                player.MoveVerticalAndPullPosition(player.ForceZ / 65536f, wasSupportedEnteringThisTick: false);
                player.Controller?.SetExternalVerticalDisplacement(ClimbingExternalDisplacementSentinel);
            }
        }
    }

    /// <summary>Runs ONE 50 Hz kinematic tick for a controller-driven, non-player entity (E4.b,
    /// docs/plan-e4-deplacement-scripte.md "Mover scripte par frame") - same shape as
    /// <see cref="TickPlayer"/>, keyed off <see cref="AlundraEntityScriptProxy.CurrentAnimationId"/> instead
    /// of <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> - see this class' own doc for why.
    /// Called once per logic tick from <see cref="AlundraEntityScriptProxy.Update"/>'s own <c>!IsPlayer</c>
    /// branch, inside the SAME per-tick loop that runs this entity's script pass immediately before it and
    /// its <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/> immediately after (this class' own
    /// doc, ONE-CLOCK fix) - unconditionally (E4.e), not gated on <see cref="AlundraEntityScriptProxy.Controller"/>:
    /// <see cref="RunOneKinematicTick"/>'s own controller-null branch integrates <c>Pos*</c> directly, same
    /// as it always did for the pre-E3 hero, so this is safe to call for every non-player entity regardless
    /// of whether it carries a controller.</summary>
    internal static void TickScriptedNpc(AlundraEntityScriptProxy entity)
    {
        RunOneMotionTick(entity, entity.CurrentAnimationId);
    }

    /// <summary>One logic tick's worth of motion for either caller - PhysicsEngine.cs:17 (top of
    /// UpdateEntitiesPhysics): <see cref="AlundraEntityScriptProxy.ForceAdjusted"/> is cleared exactly once
    /// per TICK, immediately before that tick's own kinematic step - see that field's own doc. A curtailed
    /// step (<see cref="AlundraEntityScriptProxy.MoveControllerAndPullPosition"/>) sets it back within this
    /// SAME tick; it then stays set until the very next tick's own clear, which is exactly what makes it a
    /// reliable "last completed tick was curtailed" signal for the NEXT tick's script pass to read (see
    /// <see cref="AlundraEntityScriptProxy.ForceAdjusted"/>'s own doc and this class' own doc, ONE-CLOCK
    /// fix).</summary>
    private static void RunOneMotionTick(AlundraEntityScriptProxy entity, uint animSetAnimationId)
    {
        entity.ForceAdjusted = 0;
        RunOneKinematicTick(entity, animSetAnimationId);
        entity.MotionTickCount++; // see that field's own doc (ONE-CLOCK invariant instrumentation).
    }

    /// <summary>
    /// One 50 Hz kinematic step - port of <c>PhysicsEngine.UpdateEntityPhysics</c> (PhysicsEngine.cs:1579-1598),
    /// the <c>IncrementForce</c> calls (PhysicsEngine.cs:1445-1446/1490-1491), and the flat-ground half of
    /// <c>ApplyEntityForces</c> (PhysicsEngine.cs:1514-1547) plus the position update (PhysicsEngine.cs:421-422) -
    /// bit-for-bit the former <c>AlundraPlayerManager.RunOneTick</c> body, generalized only by which field
    /// supplies the <c>AnimSetsByAnim</c> lookup key (<paramref name="animSetAnimationId"/> - see this
    /// class' own doc for <see cref="TickPlayer"/> vs <see cref="TickScriptedNpc"/>'s own choice). Same V1
    /// scope as before this extraction: no collision/screen-clip system (<c>ApplyEntityForces</c>'
    /// NegModX/Y/ScreenClipX/Y clamp is NOT ported), no riding-platform force feed
    /// (<c>PreviousAdjustedForceX/Y</c> stay 0).
    /// </summary>
    private static void RunOneKinematicTick(AlundraEntityScriptProxy entity, uint animSetAnimationId)
    {
        AnimSetEntry animSet = default;
        var hasAnimSet = entity.AnimSetsByAnim != null
            && entity.AnimSetsByAnim.TryGetValue((int)animSetAnimationId, out animSet);
        var speed = hasAnimSet ? animSet.Speed : 0;
        var acceleration = (hasAnimSet ? animSet.Acceleration : 0) & 0xf;

        // PhysicsEngine.UpdateEntityPhysics (PhysicsEngine.cs:1579-1597): only recompute the target
        // force/step when speed, direction or acceleration actually changed since the last tick - exactly
        // the original's own early-out.
        if (entity.Speed != speed || entity.TargetDirection != entity.CurrentDirection || entity.Acceleration != acceleration)
        {
            entity.CurrentDirection = entity.TargetDirection;
            entity.Speed = speed;
            entity.Acceleration = acceleration;

            var dirIndex = (int)entity.TargetDirection;
            var offsetX = dirIndex >= 0 && dirIndex < AnimationTables.OffsetXList.Length ? AnimationTables.OffsetXList[dirIndex] : (short)0;
            var offsetY = dirIndex >= 0 && dirIndex < AnimationTables.OffsetYList.Length ? AnimationTables.OffsetYList[dirIndex] : (short)0;

            entity.TargetForceX = offsetX * speed;
            entity.TargetForceY = offsetY * speed;
            entity.ForceStepX = Math.Abs(entity.TargetForceX - entity.ForceX) >> entity.Acceleration;
            entity.ForceStepY = Math.Abs(entity.TargetForceY - entity.ForceY) >> entity.Acceleration;
        }

        // PhysicsEngine.cs:1445-1446/1490-1491.
        entity.ForceX = IncrementForce(entity.ForceX, entity.TargetForceX, entity.ForceStepX);
        entity.ForceY = IncrementForce(entity.ForceY, entity.TargetForceY, entity.ForceStepY);

        // PhysicsEngine.ApplyEntityForces (PhysicsEngine.cs:1514-1547), flat-ground-only - see this
        // method's own doc for what stays out.
        entity.AdjustedForceX = entity.ForceX;
        entity.AdjustedForceY = entity.ForceY;
        entity.FinalForceX = entity.AdjustedForceX;
        entity.FinalForceY = entity.AdjustedForceY;
        entity.FinalForceZ = entity.ForceZ;

        // PhysicsEngine.cs:421-422 (position update) - E3.d/E4.b: for a controller-driven entity,
        // FinalForceX/Y is routed through the mover's own Move instead of committed directly (see
        // AlundraEntityScriptProxy.MoveControllerAndPullPosition's own doc); every other entity (no
        // controller) keeps E2's original collision-free integration, unchanged.
        if (entity.Controller != null)
        {
            entity.MoveControllerAndPullPosition(entity.FinalForceX / 65536f, entity.FinalForceY / 65536f);
        }
        else
        {
            entity.PosX += entity.FinalForceX;
            entity.PosY += entity.FinalForceY;
        }

        // PhysicsEngine.cs:1698-1700, same formula EntityRecordMapper/AlundraWorldProxy already use to seed
        // TileX/TileY from PosX/PosY elsewhere - kept in sync every tick. TileZ (E4.c deferral, fixed in
        // E4.d - docs/plan-e4-deplacement-scripte.md) was previously refreshed only at spawn; it is now
        // refreshed here every tick too, alongside TileX/TileY, for BOTH callers (TickPlayer/
        // TickScriptedNpc both funnel through this one shared method).
        entity.TileX = (entity.PosX >> 16) / TileWidth;
        entity.TileY = (entity.PosY >> 16) / TileHeight;
        entity.TileZ = entity.PosZ >> 20;
    }

    /// <summary>Bit-for-bit port of <c>PhysicsEngine.IncrementForce</c> (PhysicsEngine.cs:1551-1576,
    /// address 0x800367e4) - moved here verbatim from <see cref="AlundraPlayerManager"/>.</summary>
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
