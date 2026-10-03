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
    internal const float ClimbingExternalDisplacementSentinel = 1f;

    /// <summary>Fixed positive value the hero's air state (E19.d2c1 R6) latches through
    /// <see cref="CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent.SetExternalVerticalDisplacement"/> at every tick, for the same reason as
    /// <see cref="ClimbingExternalDisplacementSentinel"/>: the engine only reads the SIGN of the latch (a positive one means airborne, no ground snap).</summary>
    internal const float AirborneExternalDisplacementSentinel = 1f;

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
        // E19.d2c1 R5 (the binary clears +0x140 and +0x13C together at the head of the physics pass, 0x800383B4/0x800383B8).
        entity.CollidedWithEntityZ = 0;
        entity.ZHeldByTick = false;

        // E19.d2c2 S4 (the binary resets the field at 0x80038998 and sets it again at 0x800364C8, at the head of the tick): what carries the hero is recalculated
        // at the head of EVERY tick of the hero from the position it has before this tick's movement, never kept.
        if (entity.IsPlayer)
        {
            EntitySupport.UpdateRidingEntityOfHero(entity);
        }

        // E19.d2c1 R6: the hero's vertical belongs to the tick while it is in the air state (a scripted jump, 0x1B): after the clears above and BEFORE the XY
        // step, the binary's order (Z before XY, 0x80037E34).
        var airborne = entity.IsPlayer && RunHeroVerticalTick(entity);

        // R5 d: the hero outside the airborne state lands on every tick of its rest with gravity (+0x140 stays 1 on the ground). The state
        // itself raises it at its own landing.
        if (entity.IsPlayer && !airborne && (entity.Flags & EntityFlags.Gravity) != 0 && entity.IsOnGround == 1)
        {
            entity.CollidedWithEntityZ = 1;
        }

        RunOneKinematicTick(entity, animSetAnimationId);

        // R6: after each tick of the state, IsOnGround is derived from the position after the XY step, not pulled from the engine: PosZ <= the floor (the binary's
        // !(FloorHeight < PosZ), 0x800380F8, without its +1). E19.d2c2 S3: the floor is the higher of the terrain and the top of an entity under the box.
        if (entity.ZHeldByTick)
        {
            entity.IsOnGround = entity.PosZ + entity.ModZ <= entity.ComputeFloorHeight(entity.ComputeTerrainHeight()) ? 1 : 0;
        }

        entity.MotionTickCount++; // see that field's own doc (ONE-CLOCK invariant instrumentation).
    }

    /// <summary>
    /// E19.d2c1 R6 (docs/plan-e19-opcodes.md §1.2h.3.1): the vertical step of the hero in the air state the logic tick holds, ported from the binary (the force
    /// of the hero <c>0x80036884</c>-<c>0x80036948</c>, the strict landing test <c>0x80036C20</c>-<c>0x80036C34</c> and <c>0x800376E0</c>/<c>0x80037700</c>). Returns
    /// true when the state ran this tick.
    /// <list type="bullet">
    /// <item><description>Entry: the impulse of an animation taken at this tick (<see cref="AlundraEntityScriptProxy.IsZForceApplied"/>, R1) or the mark of
    /// <c>0x1B</c> on the hero. The live values of the engine (its gravity, <c>MaxFallSpeed</c>, vertical ownership) are captured, then gravity and fall speed
    /// go to 0 and the vertical becomes external; they are given back AS CAPTURED at the landing (a gravity already at 0 by a <c>0x17</c> stays 0).</description></item>
    /// <item><description>Force: impulse tick, <c>ForceZ = IZF &lt;&lt; 8</c> with no decay; otherwise, with <see cref="EntityFlags.Gravity"/>, the decay
    /// <c>ForceZ -= Gravity &lt;&lt; 8</c> bounded on both sides by <c>ZViscosity &lt;&lt; 8</c>; without Gravity, unchanged.</description></item>
    /// <item><description>Step: <c>F &gt; 0</c> rises (no ceiling, E19.h); otherwise the STRICT test <c>PosZ + F &lt; T</c> (<c>T</c>, the terrain under the box at the
    /// position before the XY step): landed means <c>PosZ = T</c>, <c>ForceZ = 0</c> with gravity, <see cref="AlundraEntityScriptProxy.CollidedWithEntityZ"/> 1 and
    /// the state ends; not landed, <c>PosZ += F</c>. The logical <see cref="AlundraEntityScriptProxy.PosZ"/> is the truth; the root follows by a vertical
    /// displacement of the controller (never a teleport: X and Y keep their fraction).</description></item>
    /// </list>
    /// </summary>
    private static bool RunHeroVerticalTick(AlundraEntityScriptProxy hero)
    {
        var impulse = hero.IsZForceApplied != 0;

        // R8: the sound of the take-off, once per impulse (the impulse is cleared by every StepAnimationClock, so a catch-up frame gives it at one tick only).
        if (impulse && hero.ZImpulseSfx > 0)
        {
            hero.ScriptHost?.SoundPlayer?.PlaySfx(hero.ZImpulseSfx);
        }

        var controller = hero.Controller;
        if (controller == null || hero.OwnerEntity?.RootComponent == null)
        {
            hero.HeroFlyMarked = false;
            return false;
        }

        // E19.d2c2 S2 (a fall): outside the air state and without impulse, the first tick of a frame enters it when the head-of-frame pull of that frame found the
        // hero not on the ground and was reliable (the vertical not held elsewhere). Only with the gravity bit (the binary applies no gravity without it). The
        // decision is taken once per frame: the pull's flag is consumed here.
        var fall = !hero.HeroAirborne && !impulse && !hero.HeroFlyMarked && hero.HeadPullGroundTrusted && hero.IsOnGround == 0
            && (hero.Flags & EntityFlags.Gravity) != 0;
        hero.HeadPullGroundTrusted = false;

        if (!hero.HeroAirborne)
        {
            if (!impulse && !hero.HeroFlyMarked && !fall)
            {
                return false;
            }

            hero.AirborneSavedGravity = controller.Settings.Gravity;
            hero.AirborneSavedMaxFallSpeed = controller.Settings.MaxFallSpeed;
            hero.AirborneSavedVerticalOwned = controller.IsVerticalOwnedExternally;
            hero.AirborneSavedStepHeight = controller.Settings.StepHeight;
            controller.Settings.Gravity = 0f;
            controller.Settings.MaxFallSpeed = 0f;
            controller.IsVerticalOwnedExternally = true;
            hero.HeroAirborne = true;
            if (fall)
            {
                // The binary at rest lands at every tick (ForceZ 0); the climb leaves +-0x10000. The map's gravity then plays from this very tick.
                hero.ForceZ = 0;
            }
        }

        hero.HeroFlyMarked = false;
        var gravity = (hero.Flags & EntityFlags.Gravity) != 0;
        if (impulse)
        {
            // The lower jump (the binary, 0x80036884-0x80036948): with gravity, on the cells of the x160 bit (VramOR & 0x10) and without boots, the impulse is IZF * 160
            // instead of IZF << 8 (the water of the 0x18 cells). VramOR is the one of the previous tick (it is 0 in the air).
            hero.ForceZ = gravity && (hero.CombinedVramFlagsOR & 0x10) != 0 && BootsLevel(hero) <= 0
                ? hero.IsZForceApplied * 160
                : hero.IsZForceApplied << 8;
        }
        else if (gravity)
        {
            var force = hero.ForceZ - (hero.MapGravityRaw << 8);
            var terminal = hero.MapZViscosityRaw << 8;
            hero.ForceZ = Math.Clamp(force, -terminal, terminal);
        }

        // E19.d2c2 S4 (the binary, 0x80037364): carried and without impulse this tick, the force of the hero is the platform's (0 for an object at rest), taken BEFORE the
        // landing test. The platform is the entity whose logic entity the hero's RidingEntity holds (recalculated at the head of this tick).
        if (!impulse && hero.RidingEntity?.GameplayProxy is AlundraEntityScriptProxy carrier)
        {
            hero.ForceZ = carrier.ForceZ;
            hero.FinalForceZ = carrier.FinalForceZ;
        }
        else
        {
            hero.FinalForceZ = hero.ForceZ;
        }

        var tickForce = hero.FinalForceZ;
        var terrain = hero.ComputeTerrainHeight();

        // E19.d2c2 S3: the height to land on is the higher of the terrain and the top of an entity under the box, found by the very call of the binary's support
        // (EvaluateEntitySupport of an NPC: the seed is the reach of this tick's step, clamped up to the terrain + 1).
        var landingHeight = terrain;
        var onEntity = false;
        if (tickForce <= 0 && hero.ScriptHost != null && EntitySupport.IsEligibleSubject(hero))
        {
            var seed = Math.Max(hero.PosZ + hero.ModZ + tickForce, terrain + 1);
            if (EntitySupport.TryFindSupport(hero, hero.ScriptHost.Collidables, seed, out _, out var supportTopZ))
            {
                landingHeight = supportTopZ;
                onEntity = true;
            }
        }

        var landed = false;
        if (tickForce <= 0 && hero.PosZ + hero.ModZ + tickForce < landingHeight)
        {
            hero.PosZ = landingHeight - hero.ModZ;
            if (gravity)
            {
                hero.ForceZ = 0;
                hero.FinalForceZ = 0;
            }

            hero.CollidedWithEntityZ = 1;

            // On an entity the hero stays in the state the tick holds (the engine does not see the boxes of entities), with IsOnGround 1; it leaves it only when its
            // support is the terrain again.
            landed = !onEntity;
        }
        else
        {
            hero.PosZ += tickForce;
        }

        hero.TileZ = hero.PosZ >> 20;
        hero.ZHeldByTick = true;
        hero.FollowPosZOnRoot();

        if (landed)
        {
            hero.HeroAirborne = false;
            RestoreAirborneEngineValues(hero, controller);
            if (hero.AirborneSavedVerticalOwned)
            {
                controller.SetExternalVerticalDisplacement(AlundraGameplayFreeze.OwnerExternalVerticalDisplacement(hero));
            }
        }
        else
        {
            controller.SetExternalVerticalDisplacement(AirborneExternalDisplacementSentinel);

            // E19.d2c2 S6 (binary 0x80037524-0x80037538): no step tolerance in the air - a corner of a cell blocks when its height is above the foot; the 3 px of
            // tolerance only play at ForceZ == 0 (0x80037848). The engine's rule (ground height above foot + StepHeight) then gives the binary's exactly.
            controller.Settings.StepHeight = hero.ForceZ != 0 ? 0f : hero.AirborneSavedStepHeight;
        }

        return true;
    }

    /// <summary>
    /// Gives the engine the values captured at the entry of the air state (its gravity, <c>MaxFallSpeed</c>, vertical ownership and, E19.d2c2 S6,
    /// <c>StepHeight</c>): the landing, and the adoption of a new pawn while the state is up (<c>AlundraWorldProxy.AdoptPlayerPawn</c>).
    /// </summary>
    internal static void RestoreAirborneEngineValues(AlundraEntityScriptProxy hero, CasaEngine.Framework.Scene.Entities.Components.CharacterControllerComponent controller)
    {
        controller.Settings.Gravity = hero.AirborneSavedGravity;
        controller.Settings.MaxFallSpeed = hero.AirborneSavedMaxFallSpeed;
        controller.Settings.StepHeight = hero.AirborneSavedStepHeight;
        controller.IsVerticalOwnedExternally = hero.AirborneSavedVerticalOwned;
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

        // E19.d2c1 R7 (the binary, 0x80036954-0x80036A50), the hero alone: on LOCAL copies of the target and of the step (the caches above stay those of the
        // animation), the ice (VramOR & 0x20) divides the step by 16 and the water (VramOR & 0x08, without boots) halves the target, both with the signed
        // arithmetic shift of the binary (rounded down). The boots level is read only when the water asks for it.
        var targetX = entity.TargetForceX;
        var targetY = entity.TargetForceY;
        var stepX = entity.ForceStepX;
        var stepY = entity.ForceStepY;
        if (entity.IsPlayer)
        {
            var vramFlags = entity.CombinedVramFlagsOR;
            if ((vramFlags & 0x20) != 0)
            {
                stepX = (int)(((long)stepX * 0x1000) >> 16);
                stepY = (int)(((long)stepY * 0x1000) >> 16);
            }

            if ((vramFlags & 0x08) != 0 && BootsLevel(entity) <= 0)
            {
                targetX = (int)(((long)targetX * 0x8000) >> 16);
                targetY = (int)(((long)targetY * 0x8000) >> 16);
            }
        }

        // PhysicsEngine.cs:1445-1446/1490-1491.
        entity.ForceX = IncrementForce(entity.ForceX, targetX, stepX);
        entity.ForceY = IncrementForce(entity.ForceY, targetY, stepY);

        // PhysicsEngine.ApplyEntityForces (PhysicsEngine.cs:1514-1547), flat-ground-only - see this
        // method's own doc for what stays out.
        entity.AdjustedForceX = entity.ForceX;
        entity.AdjustedForceY = entity.ForceY;
        entity.FinalForceX = entity.AdjustedForceX;
        entity.FinalForceY = entity.AdjustedForceY;
        entity.FinalForceZ = entity.ForceZ;

        // E19.d2c2 S4 (the binary, 0x80037364): the hero carried by an entity adds the displacement that entity realized at its last tick to its own XY step.
        if (entity.IsPlayer && entity.RidingEntity?.GameplayProxy is AlundraEntityScriptProxy carrier)
        {
            entity.FinalForceX += carrier.LastTickDeltaX;
            entity.FinalForceY += carrier.LastTickDeltaY;
        }

        var startX = entity.PosX;
        var startY = entity.PosY;

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

        entity.LastTickDeltaX = entity.PosX - startX;
        entity.LastTickDeltaY = entity.PosY - startY;

        // PhysicsEngine.cs:1698-1700, same formula EntityRecordMapper/AlundraWorldProxy already use to seed
        // TileX/TileY from PosX/PosY elsewhere - kept in sync every tick. TileZ (E4.c deferral, fixed in
        // E4.d - docs/plan-e4-deplacement-scripte.md) was previously refreshed only at spawn; it is now
        // refreshed here every tick too, alongside TileX/TileY, for BOTH callers (TickPlayer/
        // TickScriptedNpc both funnel through this one shared method).
        entity.TileX = (entity.PosX >> 16) / TileWidth;
        entity.TileY = (entity.PosY >> 16) / TileHeight;
        entity.TileZ = entity.PosZ >> 20;
    }

    /// <summary>
    /// E19.d2c1 R7: the boots level of the hero, 3, 2 or 1 when the object <c>0x1C</c>, <c>0x1B</c> or <c>0x1A</c> is owned (<see cref="AlundraPlayerManager.GetNumberOfItem"/>,
    /// <c>NumberOfItems[id * 2 + 1]</c>), else 0; 0 too without a script host (a bare proxy), without exception.
    /// </summary>
    private static int BootsLevel(AlundraEntityScriptProxy hero)
    {
        var state = hero.ScriptHost?.GameState;
        if (state == null)
        {
            return 0;
        }

        if (AlundraPlayerManager.GetNumberOfItem(state, 0x1C) != 0)
        {
            return 3;
        }

        if (AlundraPlayerManager.GetNumberOfItem(state, 0x1B) != 0)
        {
            return 2;
        }

        return AlundraPlayerManager.GetNumberOfItem(state, 0x1A) != 0 ? 1 : 0;
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
