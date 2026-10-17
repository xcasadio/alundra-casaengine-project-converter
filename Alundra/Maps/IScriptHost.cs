#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Alundra.Maps.Loop;
using Alundra.Scripts;
using Alundra.Session;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Audio;
using Alundra.Transitions;

namespace Alundra.Maps;

/// <summary>
/// Seam an <see cref="AlundraEntityScriptProxy"/> uses to reach the handful of world-level services its
/// own <see cref="AlundraEntityScriptProxy.Update"/> needs now that the entity, not the world, drives its
/// own pick/run pass (decision D2, docs/plan-conversion-totale.md §2): the shared bytecode interpreter,
/// the original's <c>g_activeCollisionEntity</c> (read by the pick phase to decide slot F), and the
/// two-argument <c>DestroyEntity</c> the pick phase's own destroy branches and E1 of the native sprites call. Implemented by
/// <see cref="AlundraWorldProxy"/> (which owns all three); every proxy this world spawns gets
/// <c>proxy.ScriptHost = this</c> in the shared spawn path (<see cref="AlundraWorldProxy.InitializeWithWorld"/>
/// / <see cref="AlundraWorldProxy.SpawnEntityByRecordId"/>). A proxy with a null <see cref="AlundraEntityScriptProxy.ScriptHost"/>
/// (never spawned through this world, e.g. a bare prefab instantiated in a unit test) skips its own
/// <see cref="AlundraEntityScriptProxy.Update"/> body entirely - see that method's own doc.
/// </summary>
public interface IScriptHost
{
    /// <summary>The shared event-program bytecode interpreter every spawned entity's pick/run pass and
    /// the world's own MapEvents pass run against - mirrors the original's single <c>_gameEngine</c>
    /// (there is exactly one interpreter per world, not one per entity).</summary>
    IEventProgramRunner Runner { get; }

    /// <summary>Port of the original global <c>g_activeCollisionEntity</c> - set by
    /// <c>AlundraPlayerManager.CheckEntityInteraction</c> on the interact frame (E12.d), consumed (and
    /// cleared) by the slot-F pick in <see cref="AlundraEntityScriptProxy.Update"/> - see D-E12D-4's
    /// consume-on-pick doc there. Settable since E12.d: the writer reaches it through this host.</summary>
    AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }

    /// <summary>Two-argument <c>GameEngine.DestroyEntity(Entity, int)</c> port the pick phase's own
    /// destroy branches (DestroyOnSlidingSlope/DestroyOnVramFlags) and E1 of the native sprites (E14.a1b) call - see
    /// <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy,int)"/>'s own doc.</summary>
    void DestroyEntity(AlundraEntityScriptProxy entity, int effectId);

    /// <summary>The shared game-flag/control-flag store (E2: also read by
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s own player branch for
    /// <see cref="GameState.PlayerControlBits.InputBlockedMask"/>, mirroring the original's single
    /// global <c>g_playerControlFlags</c>) - see <see cref="AlundraWorldProxy.GameState"/>'s own doc.</summary>
    GameState GameState { get; }

    /// <summary>
    /// The world's own <see cref="AlundraPlayerController"/> (E2), or null when no such controller
    /// possesses a pawn in this world (headless test harness, or a world with no player-startup settings) -
    /// see <see cref="AlundraWorldProxy"/>'s own implementation. <see cref="AlundraEntityScriptProxy.Update"/>'s
    /// player branch treats a null controller as "no input available this frame" (no-op), never a
    /// fallback spawn or a thrown exception.
    /// </summary>
    AlundraPlayerController? PlayerController { get; }

    /// <summary>
    /// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): this frame's collidable-entity snapshot
    /// (<see cref="EntitySupport.BuildCollidables"/>'s own criteria, <c>EntityManager.cs:994</c>) - shared,
    /// allocation-free, rebuilt once per frame by whichever host owns the spawned-entity list
    /// (<c>AlundraWorldProxy</c> in production, <c>HeadlessIntroSimulation</c> in the intro trace harness).
    /// Consumed by <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>, called once per entity per
    /// frame from <see cref="AlundraEntityScriptProxy.Update"/>. Never mutated by a reader - only ever
    /// rebuilt in place by its owner.
    /// </summary>
    IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; }

    /// <summary>
    /// Bug fix (user-reported runtime pacing bug - script logic was running at rendered-frame rate
    /// instead of the original's fixed 50 Hz, see <see cref="LogicClock"/>'s own class doc for the
    /// full diagnosis): this frame's logic-tick count, computed once per frame and shared by every caller
    /// (per-frame memo - see <see cref="LogicClock.TicksThisFrame"/>). <see cref="AlundraEntityScriptProxy.Update"/>
    /// calls this to gate its own pick/run pass and <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>;
    /// <see cref="AlundraWorldProxy.Update"/> calls it again (reading the SAME cached value, since every
    /// entity's own <c>Update</c> already ran this frame) to gate <see cref="AlundraWorldProxy.RunMapEventsPass"/>/
    /// <see cref="AlundraWorldProxy.RunPendingEventTriggers"/>, then closes the frame. Implemented by
    /// <see cref="AlundraWorldProxy"/> and the intro trace harness's own <c>HeadlessIntroSimulation</c>,
    /// each owning exactly one <see cref="LogicClock"/> instance.
    /// </summary>
    int LogicTicksThisFrame(float elapsedTime);

    /// <summary>
    /// E20.a A2 (docs/plan-e20-heros.md section 1.3, D-E20-12): true when the host runs the logic ticks of its entities itself, in the binary's order
    /// (<see cref="AlundraWorldProxy.Update"/>: the entity block of each tick, <c>0x8003B388</c>), so that <see cref="AlundraEntityScriptProxy.Update"/> only
    /// samples the pad, pulls the root pose and records that it took part in the frame. False (the default) for a rig or a fake host that has no such
    /// driver: the entity then runs its own loop of ticks (<c>AlundraEntityScriptProxy.RunSelfDrivenTicks</c>), over the same tick passes.
    /// </summary>
    bool DrivesEntityTicks => false;

    /// <summary>
    /// E20.e0a3b (D-E0A-5, ADR-0065): the collision field of the host, which an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> reads (<see cref="AlundraEntityScriptProxy.ReadCollisionField"/>)
    /// in the tick of its birth, while the engine has not integrated it and <c>Owner.World</c> is still null. Null (the default) for every host that has none; <see cref="AlundraWorldProxy"/> implements it EXPLICITLY (its own
    /// <see cref="AlundraWorldProxy.CollisionField"/> has another type, so the default would answer null).
    /// </summary>
    ICollisionField? CollisionField => null;

    /// <summary>
    /// E20.e0a5 (D-E0A5-3, ADR-0071): the animation data of the prefab of an entity that the engine has not integrated yet (its sprite has no <c>Animations</c> before the integration, but its <paramref name="animationAssetIds"/> are known):
    /// the data of the animation <paramref name="animationId"/> in the row <paramref name="direction"/> (the names end with <c>_anim{n}_{down|up|left|right}</c>), the shared instance the sprite will hold after the integration. The entity reads
    /// the box of its frame from it in the tick of its birth. Null (the default) for a host with no asset manager, and when no animation of the ids matches. Does not change the sprite.
    /// </summary>
    Animation2dData? ResolveAnimationData(IReadOnlyList<Guid> animationAssetIds, uint animationId, int direction) => null;

    /// <summary>
    /// T3 (docs/plan-transitions-carte.md §1.1/§3): this world's own parsed "Portals" object-layer
    /// records (see <see cref="AlundraWorldProxy.BuildPortals"/>), in slot order - the exact list
    /// <see cref="AlundraPortalScanner.FindPortalAtTile"/> scans. Default-implemented as an empty list
    /// so every host built for an unrelated slice/test (none of them portal-aware) needs no change to
    /// keep implementing this interface - only <see cref="AlundraWorldProxy"/> overrides it with real
    /// data.
    /// </summary>
    IReadOnlyList<AlundraPortalRecord> Portals => Array.Empty<AlundraPortalRecord>();

    /// <summary>
    /// E19.t (docs/plan-e19-opcodes.md §1.2r, D-E19-57): this world's sound-effect player, which every change of animation reaches (the sound of the new
    /// animation, <see cref="AlundraEntityScriptProxy.AnimationSoundOf"/>; the hero's take-off is one of them). Default-implemented as null, like <see cref="Portals"/>, so no other host needs to change - only
    /// <see cref="AlundraWorldProxy"/> overrides it, with its own player (<see cref="AlundraWorldProxy.SoundPlayer"/>).
    /// </summary>
    IAlundraSoundPlayer? SoundPlayer => null;

    /// <summary>
    /// E20.b B1 (docs/plan-e20-heros.md section 1.4): this world's item tables, which <see cref="PlayerManager.MovePlayer"/> reads at its head to resolve the equipped weapon
    /// (<c>GetItemIdFromCurrentWeapon</c>, then the weapon record of <see cref="WeaponTables"/>). A default interface member, null like <see cref="SoundPlayer"/>, so no other host needs to
    /// change: without tables the hero has no weapon, which the binary reads as record 0 (the basic sword's). <see cref="AlundraWorldProxy"/> returns its own tables; a rig may set some.
    /// </summary>
    ItemTables? ItemTables => null;

    /// <summary>
    /// E20.c1a C1 (docs/plan-e20-heros.md section 1.5): this world's balance tables (<c>Data/balance.json</c>), from which an entity gets its record, life, attack and responses at its creation
    /// (<see cref="AlundraEntityScriptProxy.InitializeBalance"/>). A default interface member, null like <see cref="ItemTables"/>: a host without tables creates entities with no record.
    /// </summary>
    BalanceTables? BalanceTables => null;

    /// <summary>E20.c1a C1: the balance threshold of the loaded map (its property <c>D</c>), the level up to which the chain of a record is walked (<see cref="BalanceTables.Select"/>). 0 by default.</summary>
    int BalanceThreshold => 0;

    /// <summary>
    /// E20.c1b K2 (docs/plan-e20-heros.md section 1.6): this world's three equipment records (<c>0x80146E00-0x80146E14</c>), which the hero's <see cref="PlayerManager.MovePlayer"/> fills at every tick
    /// (<c>UpdateItemEffectState</c> steps 2 to 5). A default interface member, null like <see cref="BalanceTables"/>: a host without the seam keeps no equipment.
    /// </summary>
    EquipmentRecords? EquipmentRecords => null;

    /// <summary>
    /// E20.f1a (docs/plan-e20-heros.md section 1.9): this world's effect stage, on which the hero's <see cref="PlayerManager.MovePlayer"/> queues the hold spark and the burst of the charged attack
    /// (<see cref="EffectStage.Emit"/>, applied by <see cref="EffectStage.RunFrame"/> at the tick of the emission, ADR-0046). A default interface member, null like <see cref="SoundPlayer"/>:
    /// a host without a stage creates no effect. <see cref="AlundraWorldProxy"/> returns its own stage (the same property implements <see cref="IEntityWorldContext.EffectStage"/>).
    /// </summary>
    EffectStage? EffectStage => null;

    /// <summary>
    /// T3's own seam for T4 (docs/plan-transitions-carte.md §3: "le prédicat ... exposé par une couture
    /// que T4 remplira"). Called by <see cref="PlayerManager.MovePlayer"/>, at most once per
    /// call, exactly when <see cref="AlundraPortalTrigger.TryGetTrigger"/> returns non-null - the moment
    /// the original's <c>CheckAndExecuteWarp</c> would have called <c>HandleWarpTransition</c>. Default
    /// no-op here: T3 DETECTS, it does not ACT (no fade, no <c>SetWorldToLoad</c> request) - see
    /// <see cref="AlundraPortalTrigger"/>'s own class doc. T4's <c>AlundraWarpDirector</c> overrides this
    /// on <see cref="AlundraWorldProxy"/> to actually start the departure sequence.
    /// </summary>
    void OnPortalTriggerDetected(AlundraPortalRecord portal, uint arrivalDirectionId)
    {
    }
}
