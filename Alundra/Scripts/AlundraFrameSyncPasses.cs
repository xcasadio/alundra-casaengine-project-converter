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
    /// see <see cref="ApplySpawnInitialization"/>/<see cref="SpawnPlayerEntity"/>, guaranteed different from
    /// <c>TargetAnimationId</c>), so the very first sync always fires and sets the entity's initial visual.
    ///
    /// Frame-level animation state (<c>Frame</c>/<c>NextFrameDelay</c>/<c>AnimCompleteCounter</c>, the rest
    /// of <c>UpdateAnimation</c>) stays out of scope: CasaEngine's own <c>Animation2dCompositionSampler</c>
    /// (driven by <see cref="AnimatedSpriteComponent.Update"/>) already owns frame timing once the right
    /// animation is selected.
    /// </summary>
    internal static void SyncAnimation(Entity entity)
    {
        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            return;
        }

        // Destroyed-entity visibility (structural piece for the search-driven destroy opcodes, 0x2E
        // in particular): once an entity is flagged for destruction it stops being drawn and stops
        // being synced here - see DestroyEntity's own V1 scope note on why this is invisibility
        // rather than full removal/slot recycling. Checked against FlagToDestroy specifically, not
        // EntityStatus.Destroyed (numeric value 0, the default AlundraEntityScriptProxy.Status a
        // freshly-constructed-but-never-spawned proxy carries) - no ported code path ever transitions
        // an entity all the way to Destroyed in V1 (see EntityStatus's own doc on slot recycling).
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
            return;
        }

        proxy.CurrentAnimationId = newCurrentAnimationId;
        proxy.AnimationDirection = newAnimationDirection;

        var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
        if (animatedSprite == null)
        {
            return;
        }

        if (TrySelectAnimationByNameSuffix(animatedSprite, proxy.CurrentAnimationId, proxy.AnimationDirection, out var selected))
        {
            animatedSprite.SetCurrentAnimation(selected, forceReset: true);
        }
    }

    /// <summary>
    /// Transform re-derivation: re-applies <see cref="ResolveLogicalPosition"/> to every spawned entity's
    /// <c>RootComponent.LocalTransform.Position</c> from its CURRENT logical
    /// <see cref="AlundraEntityScriptProxy.PosX"/>/<see cref="AlundraEntityScriptProxy.PosY"/>/
    /// <see cref="AlundraEntityScriptProxy.PosZ"/>, every frame, for every spawned entity - the original
    /// recomputes screen position from the logical position every frame (there is no cached "world
    /// transform" struct in the PSX engine, the renderer projects PosX/PosY/PosZ straight from the entity
    /// struct each frame), it never trusts a stale, spawn-time-only placement. This supersedes
    /// <see cref="CreateEntityFromPrefab"/>'s own spawn-time-only <c>ResolveLogicalPosition</c> call (still
    /// needed there so a freshly spawned, not-yet-<see cref="Update"/>-ed entity has a sane initial
    /// transform for its very first draw) - see that method's own doc, and
    /// <c>WallPlacementOverlay.ApplyEntitySortKey</c>'s deviation note, now resolved by this pass.
    /// Required for the search-driven position opcodes (0x64/0x65) to have any visible effect: without
    /// this, PosX/PosY/PosZ change but nothing ever reads them again. Field write only, no allocation - a
    /// bare-fallback spawn (<see cref="CreateBareEntityFromRecord"/>) has no <c>RootComponent</c> and is
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
                entity.RootComponent.LocalTransform.Position = AlundraWorldProxy.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
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
    /// spawn path, <see cref="CreateBareEntityFromRecord"/>) is skipped - it carries no sprite to sort in
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

            var idsv = 0;
            var idsvKey = (int)proxy.CurrentAnimationId * AlundraWorldProxy.IdsvDirectionStride + proxy.AnimationDirection;
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
