#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Owns the per-frame entity synchronisation passes formerly on <see cref="AlundraWorldProxy"/>:
/// the animation-target sync pass, the transform re-derivation pass, and the wall/sprite depth
/// interleave sort-key pass, plus the two helper methods those passes call. Pure `static`,
/// stateless, moved from <see cref="AlundraWorldProxy"/> by slice R1 of
/// docs/plan-decoupage-proxies.md - a behaviour-preserving relocation only, see that plan's §3 for
/// the exact delta rule (call qualification and the one documented private-to-internal widening)
/// this move used. Broken `<see cref>` references left by this move are fixed in slice R5, not here
/// (plan §4 R5) - this class's XML documentation is otherwise the ORIGINAL text, unmodified.
/// </summary>
internal static class AlundraFrameSyncPasses
{
    /// <summary>
    /// Loops <see cref="SyncAnimation"/> over <paramref name="entities"/> - kept as its own method (rather
    /// than inlined at its one remaining call site) since it is independently unit-tested
    /// (AlundraWorldProxyAnimationSyncTests). The world's own <see cref="Update"/> no longer calls this: as
    /// of decision D2, each entity syncs itself from its own <see cref="AlundraEntityScriptProxy.Update"/>
    /// (via <see cref="SyncAnimation"/> directly) - see that method's own doc.
    /// </summary>
    internal static void RunAnimationSyncPass(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            SyncAnimation(entity);
        }
    }

    /// <summary>
    /// Per-entity target-resolution part of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4
    /// (EntityManager.cs:209-224 only - see <see cref="TryResolveAnimationTarget"/>), then bridges a
    /// resolved change onto <paramref name="entity"/>'s own <see cref="AnimatedSpriteComponent"/> (see
    /// <see cref="TrySelectAnimationByNameSuffix"/>). Called once per frame for every spawned entity, from
    /// <see cref="AlundraEntityScriptProxy.Update"/> (moved there from this world's own per-frame pass -
    /// decision D2, docs/plan-conversion-totale.md §2) - a no-op for an entity with no
    /// <see cref="AlundraEntityScriptProxy"/> (defensive only; every caller already knows it has one).
    ///
    /// By the time any entity's own first <c>Update</c> runs, the engine has already integrated it
    /// (<c>World.InternalAddEntities</c>, called before any entity's <c>GameplayProxy.Update</c> ever
    /// runs), so its <see cref="AnimatedSpriteComponent.Animations"/> list is already populated - and every
    /// freshly spawned entity has <c>CurrentAnimationId = ~TargetAnimationId</c> (spawn-time bit-complement,
    /// see <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>/<see cref="SpawnPlayerEntity"/>, guaranteed different from
    /// <c>TargetAnimationId</c>), so the very first sync always fires and sets the entity's initial visual.
    ///
    /// Frame timing for the DRAWING stays with CasaEngine's own <c>Animation2dCompositionSampler</c> (driven in real time by
    /// <see cref="AnimatedSpriteComponent.Update"/>, D-E19-17); the ENDS the scripts see (Hold, Chain, Loop turn) come from the
    /// sprite's logical tick clock, stepped once per logic tick by <see cref="StepAnimationClock"/> (E19.c2, ADR-0019). This method
    /// runs once per frame, at the end of the frame (the kept one-frame animation lag, D-E19-13), and keeps the clock aligned with
    /// the switch it makes:
    /// <list type="bullet">
    /// <item><description>it reads and zeroes <see cref="AlundraEntityScriptProxy.AnimationSwitchTicksReserved"/>, the ticks of this
    /// frame the clock spent on the switch (whatever it decides, so a frame never leaks its count into the next);</description></item>
    /// <item><description>on a switch it restarts the animation (the logical tick goes back to 0: the binary shows frame 0 on the tick
    /// of the switch without counting it). With no tick reserved (the switch was asked on a frame without a logic tick, by a map
    /// event or by the hero's input) the next tick is owed to the switch and consumed without advancing; with several reserved (a
    /// catch-up frame) the new animation advances by the reserved ticks minus one, the ticks the binary would have counted after the
    /// switch tick;</description></item>
    /// <item><description>when the animation asked for is not one of the entity's (its prefab does not have it, as the Flames of the lair
    /// of Nirude do for <c>1A 09</c>), the animation that plays starts again from its frame 0: the binary reads past its table and
    /// shows the images of animation 0.</description></item>
    /// </list>
    /// </summary>
    internal static void SyncAnimation(Entity entity)
    {
        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            return;
        }

        // The ticks the clock reserved for a switch this frame: read and zeroed first, before any early return.
        var reservedTicks = proxy.AnimationSwitchTicksReserved;
        proxy.AnimationSwitchTicksReserved = 0;

        // Destroyed-entity visibility (structural piece for the search-driven destroy opcodes, 0x2E
        // in particular): once an entity is flagged for destruction it stops being drawn and stops
        // being synced here, until the recycling pass of the same tick or the next one takes it out of the world
        // (AlundraWorldProxy.RecycleDestroyedEntities, E19.r R3). Checked against FlagToDestroy specifically, not
        // EntityStatus.Destroyed (numeric value 0, the default AlundraEntityScriptProxy.Status a
        // freshly-constructed-but-never-spawned proxy carries, and the value the recycling gives back).
        if (proxy.Status == EntityStatus.FlagToDestroy)
        {
            entity.IsVisible = false;
            return;
        }

        // A pending chain restart is consumed here whatever TryResolveAnimationTarget decides: a chain
        // onto a DIFFERENT animation is already covered by the id-changed path below, but a chain onto
        // the SAME animation (the original's own spelling of a looping walk) changes neither the id nor
        // the direction, so it would otherwise never reach SetCurrentAnimation. Cleared unconditionally so
        // one finished animation can only ever cause one restart.
        var chainRestartRequested = proxy.PendingChainRestartFlag != 0;
        proxy.PendingChainRestartFlag = 0;

        if (!TryResolveAnimationTarget(proxy, out var newCurrentAnimationId, out var newAnimationDirection)
            && !chainRestartRequested)
        {
            // E19.d2c2 D4: nothing is pending at the end of the frame (a switch seen at a tick, then cancelled by the entity's script before this validation): the
            // lock of the impulse taken at that tick is lowered, or the next switch would get no impulse.
            proxy.ZImpulseTaken = false;
            return;
        }

        // E19.d2c1 R1/R2: the validation of the switch. The tick that saw it pending already took its Z impulse (the lock is raised): the lock is
        // lowered. A validation on a frame without a tick (no lock) owes the impulse to the first tick that follows, except for the animation of an
        // appearance or an arrival, which never gives one (R2). The appearance flag falls at this first validation, whatever it decides.
        var spawnSwitch = proxy.SpawnAnimationActive && proxy.TargetAnimationId == proxy.SpawnAnimationId;
        proxy.SpawnAnimationActive = false;
        if (proxy.ZImpulseTaken)
        {
            proxy.ZImpulseTaken = false;
        }
        else if (!spawnSwitch)
        {
            proxy.ZImpulseDue = true;
        }

        proxy.CurrentAnimationId = newCurrentAnimationId;
        proxy.AnimationDirection = newAnimationDirection;

        // E19.d2b (D-E19-29): AnimFlags is the byte 0xD of the animation set the binary reloads in the switch block of UpdateAnimation
        // (0x80038B68), before anything that needs a sprite; the export carries it under the name Acceleration, whole (the kinematic
        // tick keeps the 4 low bits of it). 0 when the animation is not in the set. Its bit 0x80 takes the entity out of the collidable
        // list for the duration of the animation.
        proxy.AnimFlags = proxy.AnimSetsByAnim != null
            && proxy.AnimSetsByAnim.TryGetValue((int)proxy.CurrentAnimationId, out var animSet)
                ? animSet.Acceleration
                : 0;

        // Any switch (target, direction or chain restart) clears the Hold flag: UpdateAnimation writes +0xAC = 0 in
        // its switch block (0x80038B6C), before anything that needs a sprite, so a sprite-less entity clears it too
        // (E19.c1 T4). The animation-end counter is never zeroed here: the binary does not.
        proxy.ForceResetAnimationFlag = 0;
        proxy.HoldCountedAwaitingSwitch = false;

        var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
        if (animatedSprite == null)
        {
            return;
        }

        if (TrySelectAnimationByNameSuffix(animatedSprite, proxy.CurrentAnimationId, proxy.AnimationDirection, out var selected))
        {
            animatedSprite.SetCurrentAnimation(selected, forceReset: true);
        }
        else if (animatedSprite.CurrentAnimation != null)
        {
            // The animation asked for is not one of this entity's: the one that plays starts again (see this method's doc).
            animatedSprite.SetCurrentAnimation(animatedSprite.CurrentAnimation, forceReset: true);
        }

        if (animatedSprite.LogicalTickRate == 0)
        {
            return;
        }

        if (reservedTicks == 0)
        {
            proxy.AnimationSwitchTickOwed = true;
        }
        else if (reservedTicks > 1)
        {
            animatedSprite.AdvanceLogicalTicks(reservedTicks - 1);
        }
    }

    /// <summary>
    /// E19.c2 (docs/plan-e19-opcodes.md §1.2f, ADR-0019): one step of the logical clock of the animation ends, called once per logic
    /// tick by <see cref="AlundraEntityScriptProxy.Update"/>, inside the gameplay-blockable block (so never while the game is frozen
    /// or a transition departs), after the entity's script and before its motion. It ports the tick of the binary's
    /// <c>UpdateAnimation</c> (<c>0x80038AB4</c>):
    /// <list type="bullet">
    /// <item><description>a switch is pending (<see cref="AlundraEntityScriptProxy.PendingChainRestartFlag"/>, or
    /// <see cref="TryResolveAnimationTarget"/> would change the animation, a new target or a new direction): the tick belongs to the
    /// switch, the old animation neither advances nor ends on it. The tick is reserved for <see cref="SyncAnimation"/> and the tick
    /// owed is cleared (this switch spends it);</description></item>
    /// <item><description>otherwise, a tick owed to an earlier switch is consumed without advancing (the new animation shows its frame 0
    /// on the tick of its switch, uncounted);</description></item>
    /// <item><description>otherwise the sprite's logical clock advances by one tick and raises the ends (Hold flag, Chain, Loop turn)
    /// through the bridge; a Chain end it raised switches animation on this very tick in the binary, so it reserves the tick too.</description></item>
    /// </list>
    /// Nothing of the clock for an entity flagged for destruction or without a sprite whose clock the bridge turned on. No allocation.
    /// <para>
    /// E19.d2c1 R1 (docs/plan-e19-opcodes.md §1.2h.3.1): this is also the per-tick home of the Z impulse of the animation
    /// (<see cref="AlundraEntityScriptProxy.IsZForceApplied"/>, <c>+0xF8</c>). The binary's <c>UpdateAnimation</c> clears it at every call
    /// (<c>0x80038AE4</c>) and writes the impulse of the animation in its switch block (<c>0x80038B5C</c>): a new target, a new direction row or the end
    /// of a chain. So, before any early return: cleared; an impulse owed by a switch the validation made on a frame without tick is taken (from
    /// <see cref="AlundraEntityScriptProxy.CurrentAnimationId"/>); a switch pending whose impulse is not yet taken gives the impulse of its target
    /// (not for the animation of an appearance or an arrival, R2) and raises <see cref="AlundraEntityScriptProxy.ZImpulseTaken"/>, which makes a
    /// catch-up frame give one impulse per switch; the end of a chain this tick's clock raises gives the impulse of the chained animation at this tick;
    /// a turn of a loop gives none. This holds for an entity without a sprite or a clock too.
    /// </para>
    /// </summary>
    internal static void StepAnimationClock(AlundraEntityScriptProxy proxy)
    {
        proxy.IsZForceApplied = 0;
        if (proxy.Status == EntityStatus.FlagToDestroy)
        {
            return;
        }

        if (proxy.ZImpulseDue)
        {
            proxy.ZImpulseDue = false;
            GiveImpulse(proxy, proxy.CurrentAnimationId);
        }

        var switchPending = proxy.PendingChainRestartFlag != 0 || TryResolveAnimationTarget(proxy, out _, out _);
        if (switchPending && !proxy.ZImpulseTaken)
        {
            TakeZImpulse(proxy);
        }

        var sprite = proxy.LogicalClockSprite;
        if (sprite == null)
        {
            return;
        }

        if (switchPending)
        {
            proxy.AnimationSwitchTicksReserved++;
            proxy.AnimationSwitchTickOwed = false;
            return;
        }

        if (proxy.AnimationSwitchTickOwed)
        {
            proxy.AnimationSwitchTickOwed = false;
            return;
        }

        sprite.AdvanceLogicalTicks(1);
        if (proxy.PendingChainRestartFlag != 0)
        {
            proxy.AnimationSwitchTicksReserved++;
            TakeZImpulse(proxy); // the end of a chain switches animation on this very tick in the binary (0x80038D54-0x80038D68).
        }
    }

    /// <summary>E19.d2c1 R1/R2: gives the impulse of the target animation for the switch pending, and raises the lock; nothing for the animation of an
    /// appearance or an arrival (the lock then stays down, and the validation does not owe an impulse either).</summary>
    private static void TakeZImpulse(AlundraEntityScriptProxy proxy)
    {
        if (proxy.SpawnAnimationActive && proxy.TargetAnimationId == proxy.SpawnAnimationId)
        {
            return;
        }

        GiveImpulse(proxy, proxy.TargetAnimationId);
        proxy.ZImpulseTaken = true;
    }

    /// <summary>
    /// E19.d2c1 R1, E19.t T-R1 (D-E19-57): the one place a change of animation is acted on at its tick - posts the impulse of <paramref name="animationId"/> and
    /// asks the world's sound player for its sound (<see cref="AlundraEntityScriptProxy.AnimationSoundOf"/>, for every entity, nothing when it is not positive).
    /// The appearance's first switch never reaches it (R2, <see cref="TakeZImpulse"/>), a loop turn, a hold and the same animation asked again are no switch.
    /// </summary>
    private static void GiveImpulse(AlundraEntityScriptProxy proxy, uint animationId)
    {
        proxy.IsZForceApplied = proxy.ZImpulseOf(animationId);
        var sound = proxy.AnimationSoundOf(animationId);
        if (sound > 0)
        {
            proxy.ScriptHost?.SoundPlayer?.PlaySfx(sound);
        }
    }

    /// <summary>
    /// E19.c2 (docs/plan-e19-opcodes.md §1.2f): between two map-event passes of the same (catch-up) frame, clears the Hold flag
    /// of every entity whose animation switch is pending, as the <c>UpdateAnimation</c> of the tick in between would have
    /// (<c>0x80038B6C</c> writes the flag 0 in its switch block). Without it a map-event <c>0x1C</c> would count the same Hold
    /// end on every pass of the frame. Indexed loop, no allocation.
    /// </summary>
    internal static void ClearHoldFlagsOfPendingSwitches(IReadOnlyList<Entity> entities)
    {
        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i].GameplayProxy is not AlundraEntityScriptProxy proxy
                || proxy.Status == EntityStatus.FlagToDestroy
                || proxy.ForceResetAnimationFlag == 0)
            {
                continue;
            }

            if (proxy.PendingChainRestartFlag != 0 || TryResolveAnimationTarget(proxy, out _, out _))
            {
                proxy.ForceResetAnimationFlag = 0;
                proxy.HoldCountedAwaitingSwitch = false;
            }
        }
    }

    /// <summary>
    /// Transform re-derivation: re-applies <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/> to every spawned entity's
    /// <c>RootComponent.LocalTransform.Position</c> from its CURRENT logical
    /// <see cref="AlundraEntityScriptProxy.PosX"/>/<see cref="AlundraEntityScriptProxy.PosY"/>/
    /// <see cref="AlundraEntityScriptProxy.PosZ"/>, every frame, for every spawned entity - the original
    /// recomputes screen position from the logical position every frame (there is no cached "world
    /// transform" struct in the PSX engine, the renderer projects PosX/PosY/PosZ straight from the entity
    /// struct each frame), it never trusts a stale, spawn-time-only placement. This supersedes
    /// <see cref="AlundraEntitySpawnFactory.CreateEntityFromPrefab"/>'s own spawn-time-only <c>ResolveLogicalPosition</c> call (still
    /// needed there so a freshly spawned, not-yet-<see cref="Update"/>-ed entity has a sane initial
    /// transform for its very first draw) - see that method's own doc, and
    /// <c>WallPlacementOverlay.ApplyEntitySortKey</c>'s deviation note, now resolved by this pass.
    /// Required for the search-driven position opcodes (0x64/0x65) to have any visible effect: without
    /// this, PosX/PosY/PosZ change but nothing ever reads them again. Field write only, no allocation - a
    /// bare-fallback spawn (<see cref="AlundraEntitySpawnFactory.CreateBareEntityFromRecord"/>) has no <c>RootComponent</c> and is
    /// skipped, same as a destroyed entity (see <see cref="RunAnimationSyncPass"/>'s own doc on the
    /// FlagToDestroy check).
    /// </summary>
    internal static void RunTransformSyncPass(IReadOnlyList<Entity> entities)
    {
        foreach (var entity in entities)
        {
            SyncTransform(entity);
        }
    }

    /// <summary>Per-entity half of <see cref="RunTransformSyncPass"/> - see that method's own doc, and
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s doc for why this is now called per-entity, once per
    /// frame, rather than looped from this world's own <see cref="Update"/> (decision D2).
    /// E3.a (docs/plan-e3-collisions.md): after writing the LOGICAL pose onto the root, also re-runs
    /// <see cref="RenderProjectionComponent.UpdateProjection"/> on the entity's cached
    /// <see cref="AlundraEntityScriptProxy.RenderProjection"/> (resolved once at spawn/adoption, not
    /// looked up here) so the <c>AnimatedSpriteComponent</c> renders the projected pose of THIS frame,
    /// not the previous one: component <c>Update</c> (hence a natural, non-forced projection) runs
    /// BEFORE <c>GameplayProxy.Update</c> in <c>Entity.Update</c> (Entity.cs:473-504), and this method is
    /// itself called from <see cref="AlundraEntityScriptProxy.Update"/>, i.e. from inside that same
    /// GameplayProxy.Update - without the explicit call here the sprite would lag the logical pose by
    /// exactly one frame.
    /// <para>
    /// E3.d ("DLL - propriete de la racine par frame" item 3, docs/plan-e3-collisions.md): for a
    /// controller-driven entity (<see cref="AlundraEntityScriptProxy.Controller"/> non-null) the ROOT
    /// is this frame's source of truth - <see cref="AlundraEntityScriptProxy.Update"/> already pulled
    /// Pos*/IsOnGround FROM it - so this method must not write it back from Pos* (that would undo
    /// whatever the mover resolved this frame); it only re-projects the sprite. Every other entity (no
    /// controller, E4) keeps the E3.a behaviour above unchanged.
    /// </para>
    /// </summary>
    internal static void SyncTransform(Entity entity)
    {
        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy || proxy.Status == EntityStatus.FlagToDestroy)
        {
            return;
        }

        if (entity.RootComponent != null)
        {
            if (proxy.Controller == null)
            {
                entity.RootComponent.LocalTransform.Position = AlundraEntitySpawnFactory.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
            }

            proxy.RenderProjection?.UpdateProjection();
        }
    }

    /// <summary>
    /// Per-frame half of the wall/sprite depth interleave (see <see cref="WallPlacementOverlay"/>'s class
    /// doc): aligns every spawned entity's <see cref="DepthSortable2DComponent.Elevation"/> with its
    /// current logical <see cref="AlundraEntityScriptProxy.PosY"/> plus its current (anim, direction)'s
    /// IDSV bias, looked up from <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/> (already
    /// resolved at spawn - no per-frame catalog dictionary lookup here) - field writes/one small-dictionary
    /// lookup only, the overlay tiles themselves are built once in <see cref="InitializeWithWorld"/> and
    /// never touched again. An entity without a <see cref="DepthSortable2DComponent"/> (the bare-fallback
    /// spawn path, <see cref="AlundraEntitySpawnFactory.CreateBareEntityFromRecord"/>) is skipped - it carries no sprite to sort in
    /// the first place. A <see cref="EntityStatus.FlagToDestroy"/> entity is skipped too, same as
    /// <see cref="RunAnimationSyncPass"/> and <see cref="RunTransformSyncPass"/>.
    /// </summary>
    internal static void RunWallInterleaveSortKeyPass(IReadOnlyList<Entity> entities)
    {
        // Indexed for, not foreach - see RunPendingEventTriggers's own doc on why an IReadOnlyList<T>
        // foreach's boxed enumerator is no longer free to ignore on a per-frame pass.
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy || proxy.Status == EntityStatus.FlagToDestroy)
            {
                continue;
            }

            var depthSortable = entity.GetComponent<DepthSortable2DComponent>();
            if (depthSortable == null)
            {
                continue;
            }

            // E19.c2 (P4 of E19.c1): a 0x1C relaunch from a map event leaves CurrentAnimationId = ~TargetAnimationId until the
            // entity's next sync, while this pass runs right after the map events. The binary switches back to the target
            // before drawing, so the key is the target's.
            var animationId = proxy.CurrentAnimationId == ~proxy.TargetAnimationId ? proxy.TargetAnimationId : proxy.CurrentAnimationId;
            var idsv = 0;
            var idsvKey = (int)animationId * AlundraEntitySpawnFactory.IdsvDirectionStride + proxy.AnimationDirection;
            proxy.IdsvByAnimDirection?.TryGetValue(idsvKey, out idsv);

            WallPlacementOverlay.ApplyEntitySortKey(depthSortable, proxy.PosY, idsv);
        }
    }

    /// <summary>
    /// Port of the target-resolution part of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4
    /// (EntityManager.cs:209-224 only): resolves <see cref="AlundraEntityScriptProxy.AnimationDirection"/>
    /// from the entity's current facing and its <see cref="AlundraEntityScriptProxy.TargetDirection"/> via
    /// <see cref="AnimationTables.AnimationDirectionTable"/>, and returns true (with the new
    /// <c>CurrentAnimationId</c>/<c>AnimationDirection</c> pair) exactly when the original would have
    /// entered its "animation or direction changed" branch. Pure and static so it can be unit tested
    /// without a <see cref="World"/> or a component.
    /// </summary>
    internal static bool TryResolveAnimationTarget(
        AlundraEntityScriptProxy proxy, out uint newCurrentAnimationId, out int newAnimationDirection)
    {
        var row = proxy.AnimationDirection;
        var col = (int)(((proxy.TargetDirection + 2) & 0x1c) >> 2);
        var animationDirectionFromTargetDirection = AnimationTables.AnimationDirectionTable[row * 8 + col];

        if (proxy.CurrentAnimationId != proxy.TargetAnimationId || proxy.AnimationDirection != animationDirectionFromTargetDirection)
        {
            newCurrentAnimationId = proxy.TargetAnimationId;
            newAnimationDirection = animationDirectionFromTargetDirection;
            return true;
        }

        newCurrentAnimationId = proxy.CurrentAnimationId;
        newAnimationDirection = proxy.AnimationDirection;
        return false;
    }

    /// <summary>
    /// Finds, among <paramref name="animatedSprite"/>'s own loaded animations, the one whose name ends
    /// with "_anim{animationId}_{directionName}" - the converter's own naming scheme
    /// (<c>AlundraCasaEngineProjectConverter.Writers.SpriteWriter</c>: <c>$"bank{bank.BankKey}_anim{animSetIndex}_{DirectionNames[directionIndex]}"</c>).
    /// Matches by suffix rather than the component's own exact-name <c>SetCurrentAnimation(string,bool)</c>
    /// because this proxy does not carry the bank key prefix - only the (animationId, direction) pair the
    /// original engine itself tracked.
    /// </summary>
    internal static bool TrySelectAnimationByNameSuffix(
        AnimatedSpriteComponent animatedSprite, uint animationId, int animationDirection, out Animation2d? selected)
    {
        if (animationDirection < 0 || animationDirection >= AnimationTables.DirectionNames.Length)
        {
            selected = null;
            return false;
        }

        var suffix = "_anim" + animationId.ToString(CultureInfo.InvariantCulture) + "_" + AnimationTables.DirectionNames[animationDirection];

        foreach (var animation in animatedSprite.Animations)
        {
            if (animation.Animation2dData.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                selected = animation;
                return true;
            }
        }

        selected = null;
        return false;
    }
}
