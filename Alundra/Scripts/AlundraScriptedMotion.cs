#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// Shared fixed-step (50 Hz, matching the original PSX build's own physics tick rate) kinematic mover -
/// extracted from <see cref="AlundraPlayerManager"/>'s own E2 hero tick (<c>RunOneTick</c>/<c>IncrementForce</c>,
/// PhysicsEngine.cs:1445-1446/1490-1491/1551-1598) so E4.b's scripted-NPC mover
/// (<see cref="AlundraEntityScriptProxy.Update"/>'s own <c>!IsPlayer</c> branch) can reuse the exact same
/// generic pieces - <see cref="IncrementForce"/>, the 50 Hz accumulator/catch-up-4 pattern, and
/// <see cref="AnimationTables"/>' own offset tables - without duplicating them, and WITHOUT changing the
/// hero's own behaviour: <see cref="AlundraPlayerManager.Tick"/> now simply calls <see cref="TickPlayer"/>,
/// bit-for-bit the same body <c>RunOneTick</c> used to run inline.
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
/// No delegate/closure of any kind is used anywhere here (<see cref="TickPlayer"/>/<see cref="TickScriptedNpc"/>
/// each inline their own copy of the tiny accumulator while-loop instead of sharing it through an
/// <c>Action</c>) - both run every frame for potentially many entities, and a captured lambda would
/// allocate per call, violating this codebase's no-per-frame-allocation rule (see e.g.
/// <see cref="AlundraEventProgramRunner"/>'s own <c>_fetchScratch</c> doc for the same constraint applied
/// elsewhere).
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

    /// <summary>Fixed-step tick for the hero pawn - see this class' own doc for why this is a thin,
    /// non-shared accumulator loop rather than a delegate-based one. Called from
    /// <see cref="AlundraPlayerManager.Tick"/>, bit-for-bit the same body/order as before this
    /// extraction.</summary>
    internal static void TickPlayer(AlundraEntityScriptProxy player, float elapsedTime)
    {
        player.PhysicsTickAccumulator += elapsedTime;

        var ticks = 0;
        while (player.PhysicsTickAccumulator >= FixedTickSeconds && ticks < MaxTicksPerFrame)
        {
            player.PhysicsTickAccumulator -= FixedTickSeconds;
            RunOneKinematicTick(player, player.TargetAnimationId);
            ticks++;
        }

        if (ticks >= MaxTicksPerFrame)
        {
            player.PhysicsTickAccumulator = 0f;
        }
    }

    /// <summary>Fixed-step tick for a controller-driven, non-player entity (E4.b,
    /// docs/plan-e4-deplacement-scripte.md "Mover scripte par frame") - same shape as
    /// <see cref="TickPlayer"/>, keyed off <see cref="AlundraEntityScriptProxy.CurrentAnimationId"/> instead
    /// of <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> - see this class' own doc for why.
    /// Called from <see cref="AlundraEntityScriptProxy.Update"/>'s own <c>!IsPlayer</c> branch, only for an
    /// entity that actually has a <see cref="AlundraEntityScriptProxy.Controller"/>.</summary>
    internal static void TickScriptedNpc(AlundraEntityScriptProxy entity, float elapsedTime)
    {
        entity.PhysicsTickAccumulator += elapsedTime;

        var ticks = 0;
        while (entity.PhysicsTickAccumulator >= FixedTickSeconds && ticks < MaxTicksPerFrame)
        {
            entity.PhysicsTickAccumulator -= FixedTickSeconds;
            RunOneKinematicTick(entity, entity.CurrentAnimationId);
            ticks++;
        }

        if (ticks >= MaxTicksPerFrame)
        {
            entity.PhysicsTickAccumulator = 0f;
        }
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
        // TileX/TileY from PosX/PosY elsewhere - kept in sync every tick.
        entity.TileX = (entity.PosX >> 16) / TileWidth;
        entity.TileY = (entity.PosY >> 16) / TileHeight;
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
