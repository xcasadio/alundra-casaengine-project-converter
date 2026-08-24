#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

public class AlundraEntityScriptProxy : GameplayProxy
{
    public bool IsLoadedNormalOrDeactivated =>
                Status is EntityStatus.Loaded or EntityStatus.Normal or EntityStatus.Deactivated;

    /// <summary>
    /// True only for the world's hero entity (<see cref="AlundraWorldProxy.PlayerEntity"/>, port of
    /// <c>ResetEntityState</c>'s slot-0 spawn - GameEngine.cs:648-670). The original's own pick/run pass
    /// (<c>EntityManager.UpdateEntitiesEvents</c>, EntityManager.cs:810) loops <c>i = 1..n</c>, i.e. it
    /// never picks or runs slot 0's own event program - the hero is driven by <c>PlayerManager.MovePlayer</c>
    /// instead (E2's own scope) and, during map-events, by <see cref="AlundraWorldProxy.RunMapEventsPass"/>.
    /// <see cref="Update"/> honors the same exclusion for its own pick/run half; the hero still gets its
    /// per-frame animation/transform sync every frame like every other entity.
    /// </summary>
    public bool IsPlayer;

    /// <summary>
    /// Port of the ORIGINAL's own <c>Entity.LogicContextEntity</c> field - in the decompiled engine this
    /// is the same struct type as this whole proxy (there is no separate "scene entity" type there), used
    /// by <c>RunScript</c> as the "logic entity" context passed to every opcode handler
    /// (EntityEventHandlers.cs:335: <c>func(entity.LogicContextEntity, entity, ...)</c>) and reassigned by
    /// <see cref="AlundraWorldProxy.RunMapEventsPass"/> (port of <c>RunMapEvents</c>, GameEngine.cs:1698:
    /// <c>playerEntity.LogicContextEntity = mapEventEntity</c>) to the map-event's own logic entity
    /// (initially the player itself, retargetable by opcode 0x66 - not ported). Deliberately a SEPARATE
    /// field from this proxy's own <see cref="LogicContextEntity"/> below, which is an engine-only,
    /// unrelated-by-coincidence-of-name back-pointer to this proxy's OWN CasaEngine <see cref="Entity"/>
    /// (set once at spawn, see <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>) - conflating the
    /// two would silently overwrite that self-pointer with a different proxy's engine <c>Entity</c>
    /// reference, which is not what the original does.
    /// </summary>
    public AlundraEntityScriptProxy? LogicEntity;

    /// <summary>
    /// Seam over the world-level services this entity's own pick/run pass needs (see
    /// <see cref="IAlundraScriptHost"/>'s own doc) - set once per spawn by whichever
    /// <see cref="AlundraWorldProxy"/> created this entity (<c>InitializeWithWorld</c> /
    /// <c>SpawnEntityByRecordId</c>), never by <see cref="Clone"/> (a cloned prefab has not been spawned
    /// into any world yet, so there is no host to carry over - the spawner always sets its own reference
    /// right after cloning). Null until then; <see cref="Update"/> is a no-op while it is null (mirrors
    /// every other override on this class staying a safe no-op before the entity is actually spawned - see
    /// the class-level note above <see cref="InitializeWithWorld"/>).
    /// </summary>
    internal IAlundraScriptHost? ScriptHost;

    public int Index;
    public int Index2;
    public Entity? ChildEntity;
    public Entity? ParentEntity;
    public EntityStatus Status;//10
    public int Hp;
    public int HpMax;
    public int FrameCounter;//1c
    public Entity? BlockedByEntity;//20
    public int Flags2;//24
    public Entity? PlatformEntity; //28
    public Entity? CarriedEntity;
    public int RelativeWarpOffsetX;
    public int RelativeWarpOffsetY;
    public int RelativeWarpOffsetZ;
    public uint ContentsItemId; //3c
    public int ContentsGameFlag;
    //public SiEntityRecord? EntityRecord;
    public int EntityRefId;
    public readonly int[] ProgramIndexes = new int[6]; //4c
    //public SpriteRecord? SpriteRecord;
    public uint SpriteTableIndex;
    public uint Flags;//6c
    public readonly int[] SpriteProgramIndexes = new int[6]; //70
    public uint TargetAnimationId; //88
    public uint TargetDirection;
    public uint CurrentAnimationId;
    public uint CurrentDirection;
    public int AnimationDirection;
    //public AnimationSet? AnimationSet;
    //public SiFrame? FirstFrame;
    //public SiFrame? Frame;
    public int NextFrameDelay;
    public int ForceResetAnimationFlag;
    public int AnimCompleteCounter;
    public int AnimFlags;
    public int ForceZ;//rise/fall speed
    public int TargetForceX, TargetForceY;
    public int ForceX, ForceY;
    public int PreviousAdjustedForceX;//?cc
    public int PreviousAdjustedForceY;//?d0
    public int ForceStepX, ForceStepY;//d4,d8
    public int AdjustedForceX, AdjustedForceY;//dc,e0
    public int FinalForceX, FinalForceY, FinalForceZ;//e4,e8,ec
    public int Acceleration;//f0
    public int Speed;//f4
    public int IsZForceApplied;//this is probably named wrong, has to do with animation  f8
    public int ScreenClipX, ScreenClipY, ScreenClipZ;
    public int NegModX, NegModY, NegModZ;
    public int PosX; //114
    public int PosY;
    public int PosZ;
    public int TileX;
    public int TileY;
    public int TileZ;
    public Entity? RidingEntity; //12c
    public Entity? XCollisionEntity;
    public int FloorHeight;
    public int TerrainHeight;//
    /// <summary>
    /// Port of the original's <c>Entity.ForceAdjusted</c> (E4.d, docs/plan-e4-deplacement-scripte.md):
    /// cleared once per frame at the top of the scripted-motion pass (before the 50 Hz sub-step loop -
    /// <see cref="AlundraScriptedMotion.TickPlayer"/>/<see cref="AlundraScriptedMotion.TickScriptedNpc"/>,
    /// porting <c>PhysicsEngine.UpdateEntitiesPhysics</c>'s own top-of-frame reset, PhysicsEngine.cs:17),
    /// set (nonzero) by <see cref="MoveControllerAndPullPosition"/> whenever the controller's own
    /// <c>Move</c> returns an actual displacement that falls short of the requested one beyond a small
    /// epsilon on either horizontal axis (<c>CharacterControllerComponent.Move</c> returns the actual
    /// displacement - CharacterControllerComponent.cs:345-369) - the DLL's own equivalent of the
    /// original's "movement was curtailed by a wall/screen clamp/collision" signal, consumed by opcode
    /// 0x1F (<see cref="AlundraEventProgramRunner"/>'s own Walk-with-collision bridge) and by 0x1E's own
    /// navigation detour. Stays 0 the whole session for an entity with no controller (bare-fallback
    /// spawn) - <see cref="MoveControllerAndPullPosition"/> is itself a no-op in that case.
    /// </summary>
    public int ForceAdjusted;//0x13c
    public int CollidedWithEntityZ;//0x140
    public int IsOnGround;
    //public readonly MapTile[] MapTiles = new MapTile[4];
    public readonly int[] MapHeights = new int[4]; // 158
    public int PlatformUpdateFlag; //
    public int _16c;
    public int HitBoxOriginX;
    public int HitBoxOriginY;
    public int HitBoxOriginZ;
    public int _17c;
    public uint CombinedVramFlagsOR;
    public uint CombinedVramFlagsAND;
    public int TileAttributes; //188
    public int Slope_18c; //18c
    public int Slope_190; //190
    //public SpriteRef SpriteRef = new SpriteRef();//194
    //public int SpriteSheetOffset, PaletteOffset;
    //public SpriteEffect? ActiveEffect;
    public int ZUpperBound;//1bc
    public int RenderSortKey;//1c0
    //public BalanceRecord? BalanceRecord;//1c4
    //public BalanceAttack? CurrentAttack;//1c8
    public int DamagedTickCounter;//1cc
    public int FrameCollisionTickCounter;//1d0
    //public FrameCollisionData? FrameCollision;//1d4
    public int ModdedPosX, ModdedPosY, ModdedPosZ;
    public int ModX, ModY, ModZ;
    public int Width, Height, Depth;
    public int HitBoxX;//1fc
    public int HitBoxY;//200
    public int HitBoxZ;//204
    public int CollisionOffsetX;//208
    public int CollisionOffsetY;//20c
    public int CollisionOffsetZ;//210
    public int CollisionWidth;//214
    public int CollisionDepth;//218
    public int CollisionHeight;//21c
    public int HitCounter;//220
    public Entity? TouchingEntity;//224
    public int EventTrigger;//228
    public int MapEventProgramId;//22c
    public Entity LogicContextEntity = null!; //self

    /// <summary>
    /// Engine-only, not part of the original struct (same as <see cref="LogicContextEntity"/>): the
    /// entity root's own <see cref="RenderProjectionComponent"/> child (E3.a,
    /// docs/plan-e3-collisions.md), resolved once at spawn/adoption
    /// (<see cref="AlundraWorldProxy.CreateEntityFromPrefab"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>)
    /// so the per-frame transform sync (<see cref="AlundraWorldProxy.SyncTransform"/>) can re-project the
    /// sprite the same frame the logical root pose changes without a per-frame
    /// <c>Entity.GetComponent&lt;RenderProjectionComponent&gt;()</c> search. Null for a bare-fallback
    /// spawn (<see cref="AlundraWorldProxy.CreateBareEntityFromRecord"/>, which has no
    /// <c>RootComponent</c> at all) or for a prefab whose root does not carry one (defensive only - every
    /// converted prefab does, see <c>SpriteWriter.WriteEntityPrefab</c>).
    /// </summary>
    public RenderProjectionComponent? RenderProjection;

    /// <summary>
    /// Engine-only, not part of the original struct: this entity's <see cref="CharacterControllerComponent"/>,
    /// resolved once at adoption (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/> - only the hero prefab
    /// carries one, E3.d, docs/plan-e3-collisions.md) and cached the same way <see cref="RenderProjection"/>
    /// is, so no per-frame <c>Entity.GetComponent&lt;CharacterControllerComponent&gt;()</c> tree search is
    /// needed. Null for every entity without a controller (every non-hero prefab today, E4 decides for
    /// NPCs) - every site that reads this field treats null as "keep E2's controller-free movement",
    /// never as an error.
    /// </summary>
    public CharacterControllerComponent? Controller;

    /// <summary>
    /// Engine-only, not part of the original struct: this entity's own map's Gravity/ZViscosity, already
    /// converted to the units <see cref="CharacterControllerSettings.Gravity"/>/<see cref="CharacterControllerSettings.MaxFallSpeed"/>
    /// expect (E3.d's own formula: <c>mapGravity*256/65536*2500</c> / <c>mapZViscosity*256/65536*50</c> -
    /// 1250/800 on map 389). Resolved ONCE at spawn (<see cref="AlundraWorldProxy.ApplySpawnInitialization"/>/
    /// <see cref="AlundraWorldProxy.AdoptPlayerPawn"/>, both via <c>AlundraWorldProxy.ResolveMapGravitySettings</c>)
    /// and stashed here so <see cref="ApplyGravitySettingsToController"/> - called again by the 0x16/0x17
    /// opcode bridges every time the script toggles <see cref="Flags"/>' Gravity bit (E4.b,
    /// docs/plan-e4-deplacement-scripte.md) - never has to re-read the map's <c>TileMapData.CustomProperties</c>
    /// per call. Stay 0 for an entity spawned with no controller and no map data (bare-fallback spawn) -
    /// harmless, since <see cref="ApplyGravitySettingsToController"/> is itself a no-op without a
    /// <see cref="Controller"/>.
    /// </summary>
    public float MapGravity;

    /// <summary>See <see cref="MapGravity"/>'s own doc - the matching <c>MaxFallSpeed</c> half.</summary>
    public float MapMaxFallSpeed;

    /// <summary>
    /// E4.f (verifier A1 follow-up): the map's RAW <c>Gravity</c>/<c>ZViscosity</c> ints (before E3.d's
    /// own controller-unit conversion - see <see cref="MapGravity"/>'s own doc), resolved at the SAME spawn
    /// site and gated the SAME way (<c>Controller != null</c>). Needed because a controller-driven entity's
    /// own vertical fall is entirely owned by the ENGINE (<c>Settings.Gravity</c>/<c>MaxFallSpeed</c>), so
    /// <see cref="AlundraScriptedMotion.RunOneKinematicTick"/> (shared, horizontal-only) never decays
    /// <see cref="ForceZ"/> for such an entity - <see cref="EvaluateEntitySupport"/> now does that decay
    /// itself, in the SAME <c>ForceZ -= Gravity&lt;&lt;8</c>/terminal-velocity-clamp shape the intro trace
    /// harness's own <c>RunVerticalPhysicsPass</c> already used (port of <c>UpdateEntityPhysics</c>'s
    /// <c>IsZForceApplied == 0</c> branch, PhysicsEngine.cs:1460-1476), so a resting-but-still-Gravity-
    /// flagged entity's <c>ForceZ</c> genuinely cycles (0 -&gt; decayed -&gt; re-clamped to 0 by the next
    /// support hit) exactly like the original's own per-tick struct, instead of staying frozen at 0 forever
    /// (which would make <see cref="EntitySupport.TryFindSupport"/>'s own reach gate, verifier A1,
    /// permanently unsatisfiable for an entity that is already resting).
    /// </summary>
    public int MapGravityRaw;

    /// <summary>See <see cref="MapGravityRaw"/>'s own doc - the matching raw <c>ZViscosity</c> half.</summary>
    public int MapZViscosityRaw;

    /// <summary>
    /// Engine-only, not part of the original struct: set by <see cref="EvaluateEntitySupport"/> to whether
    /// THIS tick's own evaluation actually found and pinned a support (true right after the "if found"
    /// branch runs; false in both the "ineligible subject" early return and the "no candidate found" else
    /// branch - i.e. always kept in sync with this same call, no stale carry-over). Consumed by BOTH of
    /// this class' own root-Z pull sites - <see cref="Update"/>'s own head pull (root -&gt; <c>Pos*</c>)
    /// AND <see cref="MoveControllerAndPullPosition"/>'s own pull (reached EARLIER in the same frame, via
    /// <see cref="AlundraScriptedMotion.TickScriptedNpc"/>'s own kinematic tick, for every controller-
    /// driven NPC) - while true, both pulls PRESERVE the current logical <see cref="PosZ"/> instead of
    /// re-deriving it from the engine's own float32 root transform (X/Y are never guarded - support never
    /// constrains them). See either call site's own doc for why (the authored support margin on map 389
    /// can be as tight as ONE 16.16 unit, e.g. sailor 11 on record 2's own real top - far below float32's
    /// own representable precision at this magnitude, so re-quantizing PosZ from a float root every frame
    /// would silently erode that margin and break <see cref="EntitySupport.TryFindSupport"/>'s own STRICT
    /// comparator). Both sites need the guard: gating only <see cref="Update"/>'s own pull was found
    /// empirically insufficient on its own - <see cref="MoveControllerAndPullPosition"/> runs first in the
    /// same frame (before this same tick's own <see cref="EvaluateEntitySupport"/> call even re-evaluates
    /// support) and would otherwise silently re-collapse PosZ moments before that re-evaluation ran. Defaults
    /// false (a freshly constructed/never-evaluated proxy is never treated as supported) - harmless for
    /// every entity this mechanism does not apply to (the player, never entity-supported on map 389; any
    /// NPC before its own first <see cref="EvaluateEntitySupport"/> call).
    /// </summary>
    internal bool WasEntitySupportedLastTick;

    /// <summary>
    /// Bug fix (user-reported runtime timing bug, gull entity 6 of map 389 - measured ~158 px/s climb
    /// instead of the faithful 150 px/s, westward drift only 179.25 px instead of 209.25 px): a
    /// controller-driven NPC's vertical is now ENTIRELY owned by the DLL's own per-tick decay
    /// (<see cref="EvaluateEntitySupport"/>, the single site - see that method's own doc), never by the
    /// engine's own continuous <c>Settings.Gravity</c>/<c>MaxFallSpeed</c> integrator
    /// (<c>CharacterControllerComponent.ApplyVerticalVelocity</c>, which runs every RENDERED frame, not
    /// every 50 Hz logic tick - integrating a per-tick quantity at render rate is exactly what produced
    /// the measured drift). This method therefore always zeroes both settings for a controller-driven
    /// NPC, REGARDLESS of <see cref="Flags"/>' Gravity bit - the bit still exists and still matters (it
    /// now gates <see cref="EvaluateEntitySupport"/>'s own decay instead, the original's own
    /// <c>PhysicsEngine.cs:1458-1476</c> gate, ported one level down). <see cref="MapGravity"/>/
    /// <see cref="MapMaxFallSpeed"/> are consequently unused by this method (kept as fields - see their own
    /// doc, still fed by <see cref="AlundraWorldProxy.ResolveMapGravitySettings"/> for parity with the
    /// hero's own unrelated E3.d path, <see cref="AlundraWorldProxy.AdoptPlayerPawn"/>, which sets its
    /// controller's real Settings.Gravity/MaxFallSpeed directly and is OUT OF SCOPE for this fix - the hero
    /// keeps its engine-driven vertical unchanged). Called once at spawn
    /// (<see cref="AlundraWorldProxy.ApplySpawnInitialization"/>), by the 0x16/0x17 and 0x62/0x63 opcode
    /// handlers (via <see cref="ResyncControllerFromFlags"/>) every time they flip a Flags bit, and every
    /// tick from <see cref="EvaluateEntitySupport"/>'s own "not found" tail (defensive re-assertion, cheap
    /// - see that method's own doc). A no-op without a <see cref="Controller"/> (bare-fallback spawn, or a
    /// prefab that predates E4.a's converter change).
    /// </summary>
    internal void ApplyGravitySettingsToController()
    {
        if (Controller == null)
        {
            return;
        }

        Controller.Settings.Gravity = 0f;
        Controller.Settings.MaxFallSpeed = 0f;
    }

    /// <summary>
    /// Bug fix (user-reported runtime timing bug, gull entity 6): the original has NO bridge between its
    /// per-entity <see cref="Flags"/> word and its physics - <c>PhysicsEngine.cs</c> reads <c>Flags</c>
    /// directly every single tick (the Gravity gate at :1458-1476 <see cref="ApplyGravitySettingsToController"/>
    /// already ports, and the ClassA/ClassB-driven collision mask <c>GetCollisionFlagsWithPlayer</c>/
    /// <c>GetCollisionFlags</c> build fresh every call, PhysicsEngine.cs:1085-1149). This engine's own
    /// <see cref="CharacterControllerComponent"/> instead CACHES both as <c>Settings.Gravity</c>/
    /// <c>MaxFallSpeed</c> and <c>Settings.WalkabilityMask</c> - so unlike the original, EVERY site that
    /// mutates <see cref="Flags"/> on a controller-driven entity must explicitly resync both, or the
    /// controller keeps acting on stale flags. This is the ONE shared resync point: re-applies gravity
    /// (delegating to <see cref="ApplyGravitySettingsToController"/>, unchanged) AND re-derives
    /// <c>Settings.WalkabilityMask</c> from the CURRENT <see cref="Flags"/> via
    /// <see cref="AlundraCellsCollisionField.WalkabilityMaskFor"/> - same pairing
    /// <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>
    /// already apply once at spawn. Called by every opcode handler that can flip a controller-relevant bit
    /// in <see cref="Flags"/> (0x16/0x17 High/Low gravity - Gravity bit only; 0x62/0x63 Set/Clear entities
    /// flags low 16 - any bit, including Gravity/ClassA/ClassB) instead of each calling
    /// <see cref="ApplyGravitySettingsToController"/> directly, so there is exactly one place that knows
    /// both settings need refreshing together. A no-op without a <see cref="Controller"/>, same shape as
    /// every other controller-gated site on this class.
    /// </summary>
    internal void ResyncControllerFromFlags()
    {
        if (Controller == null)
        {
            return;
        }

        ApplyGravitySettingsToController();
        Controller.Settings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(Flags);
    }

    /// <summary>
    /// E4.f entity-support clamp (docs/plan-e4-deplacement-scripte.md, decision E4-4) - consumes
    /// <see cref="EntitySupport.TryFindSupport"/> (port of <c>CheckEntityCollisionDown</c>'s consumption
    /// half, <c>PhysicsEngine.cs:123-139</c>) every call, no latch: when a support is found, pins
    /// <see cref="PosZ"/> to its top (<see cref="ModZ"/>-adjusted), sets <see cref="CollidedWithEntityZ"/>,
    /// zeroes <see cref="ForceZ"/> ONLY while <see cref="EntityFlags.Gravity"/> is set (preserved
    /// otherwise, <c>PhysicsEngine.cs:129-134</c>) - and, for a controller-driven entity, pins the
    /// controller's own vertical velocity to zero (<c>SetVerticalVelocity(0)</c> - <c>Settings.Gravity</c>
    /// is ALREADY 0 at all times for an NPC, see <see cref="ApplyGravitySettingsToController"/>'s own doc,
    /// so there is nothing left to suppress) and pushes the pinned Z through the existing pose path
    /// (<see cref="PushLogicalPositionToRoot"/>) so THIS SAME frame's rendered/logical position is already
    /// correct - no "first frame dip" (see this class' own E4.f doc, "anti-creux" ordering:
    /// <see cref="AlundraWorldProxy.ApplySpawnInitialization"/> already ran ONE such evaluation
    /// immediately at spawn, before this entity's own first <see cref="Update"/> or the engine's first
    /// <c>CharacterMotionSystem.UpdateControllers</c> pass ever runs). When no support is found, drives
    /// this tick's own vertical displacement through the controller instead (bug fix - see this method's
    /// own body for the exact <c>SetVerticalVelocity</c> call and rationale) - a no-op for a controller-less
    /// entity (harness bare proxy, or a genuinely controller-less sprite-only prefab), which callers instead
    /// read the updated <see cref="PosZ"/>/<see cref="ForceZ"/> fields directly (see
    /// <c>IntroTraceHarnessTests</c>' own <c>RunVerticalPhysicsPass</c>, which reuses
    /// <see cref="EntitySupport.TryFindSupport"/> the same way but merges it with its own terrain probe
    /// instead of a <see cref="CharacterControllerComponent"/>). Deliberately does NOT touch
    /// <see cref="RidingEntity"/> - the original keeps that relation entirely separate, populated only by
    /// <c>CheckRidingEntities</c> (<see cref="EntitySupport.UpdateRidingEntities"/>, its own EXACT-match
    /// test, unrelated to this clamp's own strict-below/highest-wins one - see that method's own doc).
    ///
    /// Verifier A2 (PhysicsEngine.cs:189): this entity itself must pass
    /// <see cref="EntitySupport.IsEligibleSubject"/> before <see cref="EntitySupport.TryFindSupport"/> ever
    /// runs - <c>CheckEntityCollisionDown</c> is only ever called for a
    /// Collidable/not-NoEntityCollision/no-PlatformEntity entity in the first place. Bug fix follow-up: that
    /// eligibility gate covers ONLY the <c>TryFindSupport</c> call, not the decay/displacement around it -
    /// the original's own <c>MoveEntity</c>/<c>ComputeZPosition</c> (PhysicsEngine.cs:74-165) has NO such
    /// gate at all, it runs for every active entity unconditionally, and <c>CheckEntityCollisionDown</c>'s
    /// own eligibility check (PhysicsEngine.cs:189) only skips ITS internal entity-candidate search, still
    /// falling through to the same "apply <c>finalZVelocity</c>" tail (PhysicsEngine.cs:165) an eligible,
    /// unsupported entity reaches too. So an ineligible entity here takes the exact SAME "no support" tail
    /// a genuinely-unsupported eligible one does (never a separate no-op) - same decay, same
    /// <c>SetVerticalVelocity</c> displacement, just never entity-vs-entity clamped.
    ///
    /// Verifier A1 (PhysicsEngine.cs:180-187, :205): <paramref name="immediateAtSpawn"/> false (the normal,
    /// per-frame call from <see cref="Update"/>) computes the FULL original conjunct's terrain-clamped
    /// seed - for a controller-driven entity there is no DLL-tracked <c>TerrainHeight</c> (the engine owns
    /// terrain separately), so this passes the UNCLAMPED <c>ModdedPosZ + FinalForceZ</c> (this tick's own
    /// natural vertical step, already computed by <see cref="AlundraScriptedMotion.RunOneKinematicTick"/>
    /// earlier this SAME call chain) straight through to <see cref="EntitySupport.TryFindSupport"/> - see
    /// that method's own doc for why a controller-driven caller cannot supply the terrain half and does
    /// not need to (the engine's own ground-snap already prevents falling through real terrain
    /// independently of this entity-only clamp). <paramref name="immediateAtSpawn"/> true (the ONE-SHOT
    /// call <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>'s own callers make right at spawn,
    /// before <c>FinalForceZ</c> has ever been computed by a real tick - see that call site's own doc on
    /// the "anti-creux" ordering) instead passes <see cref="int.MinValue"/> as the seed, disabling the
    /// reach gate for this one evaluation only: this is NOT literally <c>CheckEntityCollisionDown</c>'s
    /// own per-tick reach test (a freshly spawned entity's <c>FinalForceZ</c> is 0, which would make ANY
    /// perch fail the reach gate even though the level's own authored spawn height sits, by design,
    /// exactly one 16.16 unit above the platform's own strict edge - e.g. sailor 11's real record 2 perch,
    /// (468,584,368) - see docs/plan-e4-deplacement-scripte.md's own E4.f "Pourquoi" note) - it is this
    /// runtime's own proactive fixup for the engine's controller-update-before-DLL-tick frame ordering,
    /// matching the ALREADY documented "ClampToGround also covers EntityManager.cs's own separate spawn
    /// clamp" precedent on this same class: the strict below-the-feet test alone is exactly what an
    /// authored, already-resting spawn position needs.
    /// </summary>
    internal void EvaluateEntitySupport(IReadOnlyList<AlundraEntityScriptProxy> collidables, bool immediateAtSpawn = false)
    {
        // Bug fix (gull entity 6, map 389 - see ApplyGravitySettingsToController's own doc for the full
        // measured numbers): EntitySupport.IsEligibleSubject only gates the ENTITY-VS-ENTITY search below
        // (CheckEntityCollisionDown's own eligibility gate, PhysicsEngine.cs:189/994) - the original's own
        // ComputeZPosition (PhysicsEngine.cs:375-421) decays/applies ForceZ to PosZ UNCONDITIONALLY, for
        // every active entity regardless of Collidable/eligibility (MoveEntity has no such gate at all).
        // So eligibility must NOT skip the decay/velocity work below it only skips the TryFindSupport call
        // a few lines down - both an eligible and an ineligible entity fall through to the exact same
        // "found"/"not found" tail (see that variable's own use).
        var eligible = EntitySupport.IsEligibleSubject(this);

        // Single decay site (verifier A1/A6 follow-up, reconciled with the timing bug fix above): a
        // controller-driven entity's own ForceZ is never decayed by AlundraScriptedMotion.RunOneKinematicTick
        // (horizontal-only, shared with the player) - this is the ONE place it decays, port of
        // UpdateEntityPhysics's own IsZForceApplied == 0 branch (PhysicsEngine.cs:1460-1476), same shape as
        // the harness's own RunVerticalPhysicsPass. Gated on ticksThisFrame by this method's only caller
        // (Update, see its own doc) so this runs exactly once per 50 Hz logic tick, never once per rendered
        // frame - the actual root cause of the measured bug was NOT this decay (already tick-quantized
        // before this fix) but the fact that nothing routed the decayed value through the controller,
        // leaving the engine's own continuous Settings.Gravity (render-rate) to integrate the vertical
        // instead - see the SetVerticalVelocity call in the "not found" tail below, and
        // ApplyGravitySettingsToController's own doc for why that engine path is now permanently disabled
        // for every controller-driven NPC. Skipped at the immediate spawn-time evaluation (FinalForceZ has
        // no real per-tick meaning yet there either).
        if (Controller != null && !immediateAtSpawn && (Flags & EntityFlags.Gravity) != 0)
        {
            var force = ForceZ - (MapGravityRaw << 8);
            var forceAbs = force < 0 ? -force : force;
            var terminal = MapZViscosityRaw << 8;
            if (terminal < forceAbs && force < 1)
            {
                force = -terminal;
            }

            ForceZ = force;
            FinalForceZ = force;
        }

        var platformTopZSeed = immediateAtSpawn ? int.MinValue : (PosZ + ModZ) + FinalForceZ;

        var supportTopZ = 0;
        var found = eligible
            && EntitySupport.TryFindSupport(this, collidables, platformTopZSeed, out _, out supportTopZ);

        if (found)
        {
            WasEntitySupportedLastTick = true;
            CollidedWithEntityZ = 1;
            PosZ = supportTopZ - ModZ;
            if ((Flags & EntityFlags.Gravity) != 0)
            {
                ForceZ = 0;
            }

            TileZ = PosZ >> 20;

            if (Controller != null)
            {
                Controller.SetVerticalVelocity(0f);
                PushLogicalPositionToRoot();
            }
        }
        else
        {
            WasEntitySupportedLastTick = false;

            if (Controller != null)
            {
                // Always re-assert Gravity/MaxFallSpeed at 0 - defensive, already the steady state (see
                // ApplyGravitySettingsToController's own doc).
                ApplyGravitySettingsToController();

                // Bug fix (sailor 12, map 389 intro - walks down toward the last staircase and never
                // climbs it): the original's own ComputeZPosition (PhysicsEngine.cs:123-135) resets ForceZ
                // to 0 on TWO kinds of landing, not one - CheckEntityCollisionDown's own platformEntity
                // result can be either a genuine entity (platformEntity != null, the case
                // EntitySupport.TryFindSupport/the "found" branch above already ports) OR the bare TERRAIN
                // probe itself (platformEntity == null, ComputeEntityGroundHeight's own result folded into
                // CheckEntityCollisionDown's platformTopZ seed at PhysicsEngine.cs:180-187) - PhysicsEngine.cs:
                // 125-135's own `if (platformEntity == null || ...)` branch runs the SAME
                // `entity.PosZ = platformHeight - entity.ModZ; if (Gravity) entity.ForceZ = 0;` pair for
                // both. Only the entity half was ported (see the "found" branch above); this "not found"
                // tail (no ENTITY support this tick) is exactly where the missing terrain half belongs -
                // without it, a controller-driven NPC resting on terrain keeps decaying ForceZ every tick
                // (the decay above) down to the terminal -(ZViscosity&lt;&lt;8) and re-applies that terminal
                // velocity to the controller every tick via SetVerticalVelocity below; on flat ground the
                // engine's own ground-snap silently absorbs it, but at a staircase the resulting deep
                // downward velocity makes the mover's own step-up test see a wall instead of a step, so the
                // NPC stalls instead of climbing.
                //
                // Implementation choice (shape (a) of the two the fix's own brief allows): reuses the
                // ENGINE's own ground resolution - Controller.IsGrounded - instead of a second DLL-side
                // terrain probe like EntitySupport.TryFindSupport/the harness's own RunVerticalPhysicsPass
                // run for the entity-support/intro-trace cases. The controller has owned ground detection
                // since E3 (CharacterMotionSystem's own UpdateGround, which always runs at the head of THIS
                // SAME frame's World.Update, strictly before GameplayProxy.Update ever reaches here - see
                // this class' own Update doc on that ordering) - re-probing the terrain here would compute
                // the exact same answer a second time, at real per-tick cost, purely to duplicate work the
                // engine already did. The one-frame lag this reuse carries (IsGrounded reflects the LAST
                // controller ground update, evaluated once at the head of Update - see that method's own
                // IsOnGround assignment - not a fresh probe for every tick of a multi-tick catch-up frame)
                // is the same documented lag <see cref="AlundraEntityScriptProxy.Update"/>'s own root-pull
                // already accepts elsewhere on this class, and is harmless here: it can only delay the reset
                // by at most one further frame's worth of ticks, never wipe a genuine landing.
                //
                // Faithfulness gate (must not fire while rising): the original only ever reaches this reset
                // through ComputeZPosition's own `if (finalZVelocity < 1)` branch (PhysicsEngine.cs:118) -
                // i.e. only while this tick's own vertical step is descending or stationary, never while
                // rising under a scripted upward 0x1B impulse. Gated here on `ForceZ < 0` (strictly
                // descending) rather than the original's `< 1` (descending-or-zero): ForceZ is already 0 in
                // the steady "resting on flat ground" case (the very case this fix targets), so `< 1` would
                // be a no-op re-assignment there anyway, while `< 0` still cannot wipe a same-tick 0x1B lift
                // (ForceZ > 0 the instant that impulse lands - see
                // AlundraNpcCharacterControllerMoverTests.VerticalImpulse_UpwardZeroOneBWhileRestingOnRealGround...)
                // - the narrower test is strictly safer with no behavioural cost.
                if (Controller.IsGrounded && (Flags & EntityFlags.Gravity) != 0 && ForceZ < 0)
                {
                    ForceZ = 0;
                    FinalForceZ = 0;
                }

                // THE fix: port of PhysicsEngine.cs:165's own unconditional `entity.PosZ = entity.PosZ +
                // finalZVelocity` - drives this tick's own vertical displacement through the controller so
                // the engine integrates EXACTLY FinalForceZ px over the tick (ForceZ is a 16.16 px/tick
                // quantity at the original's own 50 Hz rate; * 50f converts ticks/s, / 65536f converts
                // 16.16 to whole pixels). Skipped at the immediate spawn-time evaluation, same reasoning as
                // the decay above (FinalForceZ is still 0 there, so this would be a harmless no-op anyway,
                // but skipping keeps the two guards symmetric and documents the same reasoning). Reads
                // FinalForceZ AFTER the terrain-landing reset above so a real landing this tick genuinely
                // holds still (matching the original never falling through to its own PosZ +=
                // finalZVelocity, PhysicsEngine.cs:165, once it has already returned from the reset above).
                if (!immediateAtSpawn)
                {
                    Controller.SetVerticalVelocity(FinalForceZ * 50f / 65536f);
                }
            }
        }
    }

    /// <summary>
    /// Engine-only, not part of the original struct (same as <see cref="LogicContextEntity"/>):
    /// this entity's IDSV table, resolved once at spawn from its <c>SpriteRecordCatalog</c> entry
    /// (see <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>) and keyed by
    /// <c>(int)CurrentAnimationId * 4 + AnimationDirection</c> so the per-frame wall-interleave sort
    /// pass (<see cref="AlundraWorldProxy.RunWallInterleaveSortKeyPass"/>) never re-looks-up the
    /// catalog's own (larger, Guid-keyed) dictionary. Null when the entity's header carried no
    /// IdsvAnimDirs entries (degraded catalog, or a bank whose header simply has none); callers treat
    /// that the same as "0 bias for every (anim, direction)".
    /// </summary>
    public Dictionary<int, int>? IdsvByAnimDirection;

    /// <summary>
    /// Engine-only, not part of the original struct (same shape/purpose as
    /// <see cref="IdsvByAnimDirection"/>, same key packing): this entity's Hold/Chain end-of-animation
    /// table, resolved once at spawn from its <c>SpriteRecordCatalog</c> entry (see
    /// <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>)
    /// and read by <see cref="AlundraWorldProxy.OnAnimationFinished"/> - the bridge from the engine's
    /// Once-finished event back to the original's Hold ("freeze the last frame") / Chain ("play this
    /// other animation next") semantics (EntityManager.cs:257-281). Null when the entity's header
    /// carried no IdsvAnimDirs entries, same degraded case as <see cref="IdsvByAnimDirection"/>; a miss
    /// on a specific (anim, direction) key is also possible (an older export's IdsvAnimDirs entry with
    /// no End field defaults to Loop when parsed, so it is never chained/held) - both cases are callers
    /// treating a miss as "nothing to bridge, the engine already looped".
    /// </summary>
    public Dictionary<int, AnimationEndInfo>? AnimationEndByAnimDirection;

    /// <summary>
    /// Engine-only, not part of the original struct (same as <see cref="IdsvByAnimDirection"/>): this
    /// entity's per-anim walk-speed/acceleration lookup (E2), resolved once at spawn from its
    /// <c>SpriteRecordCatalog</c> entry (see <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> - only the
    /// player pawn uses this today, <see cref="AlundraPlayerManager"/>'s own kinematic tick). Null when the
    /// entity's header carried no <c>AnimSets</c> entries (degraded catalog, older export, or a non-player
    /// entity nothing has wired this up for yet) - callers treat that as "0 speed/acceleration for every
    /// anim", same shape as <see cref="IdsvByAnimDirection"/>'s own null case.
    /// </summary>
    public IReadOnlyDictionary<int, AnimSetEntry>? AnimSetsByAnim;

    /// <summary>
    /// Engine-only, not part of the original struct: fixed-step accumulator for
    /// <see cref="AlundraPlayerManager.Tick"/>'s own 50 Hz kinematic integration (E2) - the original PSX
    /// build ran <c>PhysicsEngine.UpdateEntitiesPhysics</c> exactly once per game frame at a fixed rate;
    /// this engine's frame rate is not fixed, so this accumulates real elapsed time and lets
    /// <see cref="AlundraPlayerManager.Tick"/> run as many whole 50 Hz steps as have actually elapsed. Only
    /// ever written by that method; every other entity leaves it at its C# default (0).
    /// </summary>
    public float PhysicsTickAccumulator;

    /// <summary>
    /// Engine-only, not part of the original struct: the active 0x1E navigation detour's own path state
    /// (E4.d decision D5, docs/plan-e4-deplacement-scripte.md) - reused across ticks (no per-frame
    /// allocation: <see cref="AlundraEventProgramRunner"/>'s own walk-detour helpers only call
    /// <c>NavigationGrid2D.TryFindPath</c> ONCE per detour, right here, when engaging it), so a suspended
    /// 0x1E's own re-entry loop can keep re-deriving <see cref="TargetDirection"/> toward the CURRENT
    /// waypoint (<see cref="NavigationPath.CurrentPointIndex"/>) every tick without rebuilding the path.
    /// Null when no detour is active for this entity's current 0x1E occurrence (free walk, no navigation
    /// grid, or <c>TryFindPath</c> failed) - reset to null both on a fresh 0x1E occurrence (first-pass
    /// signature change) and once that occurrence completes (distance test satisfied), so a LATER 0x1E
    /// occurrence on this same entity always starts clean. 0x1F never sets this (D5: no detour for
    /// 0x1F).
    /// </summary>
    internal NavigationPath? WalkDetourPath;

    /// <summary>
    /// Engine-only, not part of the original struct: guards <see cref="WalkDetourPath"/>'s own one-shot
    /// <c>TryFindPath</c> attempt for the CURRENT 0x1E occurrence - once a detour attempt has been made
    /// (successful or not), later ticks do not retry <c>TryFindPath</c> every frame while
    /// <see cref="ForceAdjusted"/> stays nonzero (that would allocate a fresh
    /// <see cref="NavigationPath"/>/search buffers every tick of a stuck walk, violating this codebase's
    /// no-per-frame-allocation rule - see <see cref="AlundraEventProgramRunner"/>'s own class doc for the
    /// same constraint applied elsewhere). Reset alongside <see cref="WalkDetourPath"/> at the same two
    /// points (fresh 0x1E occurrence / occurrence completion).
    /// </summary>
    internal bool WalkDetourAttempted;

    /// <summary>
    /// Persisted interpreter cursor for slots B (Map) and C (Tick) - the only two slots the original
    /// resumes across frames instead of always re-initializing (see
    /// <see cref="AlundraEventProgramRunner"/>'s class doc). Slot A (Load) never reads this: it always
    /// runs off a fresh, throwaway <c>EventProgramState</c> instead (same reasoning). Not ported yet by
    /// any runner (B/C bytecode interpretation is a later chantier - V1 only interprets slot A), but the
    /// field lives here now so that work does not also need a proxy/Clone change.
    /// </summary>
    public readonly EventProgramState EventProgramState = new();
    public uint LastTargetAnimationId;//26c
    public uint LastTargetDirection;//270
    public byte[] Bytes = new byte[4];
    public int DelayOrAngleOrEntityId; //278
    public int ItemState;//27C
    public short[] AIValues = new short[10];//280

    //public int AnimationFrameIndex;

    //public bool IsMapSprite { get; set; }

    //for debugging
    //public string? Name { get; set; }



    //The engine calls Initialize/InitializeWithWorld/Update on every integrated entity
    //(World.InternalAddEntities catches and drops the entity on a throw, and World.Update would
    //throw every frame), so every override below is a safe no-op for now. The real status-machine
    //logic (event program interpreter driving Index/ProgramIndexes/etc.) lands in a follow-up.

    public override void InitializeWithWorld(World world)
    {
    }

    /// <summary>
    /// Per-entity port of the original's status machine (decision D2, docs/plan-conversion-totale.md §2):
    /// unlike the manager-level pass this replaced (<c>EntityManager.UpdateEntitiesEvents</c> @
    /// 0x800386D0), each entity now picks and runs its OWN event-program slot, in the engine's own entity
    /// update order (<c>World.Update</c>, CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:443-491:
    /// entities update before the world's own <see cref="AlundraWorldProxy.Update"/>). A no-op before this
    /// entity has actually been spawned through a world (<see cref="ScriptHost"/> still null - e.g. a bare
    /// prefab instantiated directly in a unit test).
    ///
    /// The original's own do/while re-scan (EntityManager.cs:874-921: an entity whose <see cref="EventTrigger"/>
    /// another entity's script sets DURING THE SAME FRAME runs again immediately, within the same pass) no
    /// longer fits a per-entity <c>Update</c> - <see cref="AlundraWorldProxy.RunPendingEventTriggers"/>
    /// (decision D3) replays it afterward, once every spawned entity has had its own <c>Update</c> this
    /// frame.
    ///
    /// <see cref="IsPlayer"/> is excluded from the pick/run half only (see that field's own doc - the
    /// original's own pass never picks/runs slot 0 either); every entity, player included, still gets its
    /// per-frame animation/transform sync every frame (<see cref="AlundraWorldProxy.SyncAnimation"/> /
    /// <see cref="AlundraWorldProxy.SyncTransform"/>, moved here from the world's own per-frame passes -
    /// see those methods' own doc on why the world no longer loops over every spawned entity for this).
    ///
    /// Accepted deviation (documented per docs/plan-conversion-totale.md §5): the original picks EVERY
    /// entity, THEN runs every picked entity, THEN syncs every entity's animation/transform, all as three
    /// separate manager-level passes. Here each entity picks, runs and syncs itself in one call, in the
    /// engine's entity iteration order - so an entity later in that order sees an EARLIER entity's
    /// same-frame script effects (position/flags/status changes) one step sooner than the original would,
    /// and anything the WORLD half of the frame changes (MapEvents moving the player via 0x64, the D3
    /// catch-up loop) is only visible to entities starting the NEXT frame's sync (one frame of latency),
    /// since the world's own <see cref="AlundraWorldProxy.Update"/> always runs after every entity's
    /// <c>Update</c> this same frame, never before.
    /// </summary>
    public override void Update(float elapsedTime)
    {
        if (ScriptHost == null)
        {
            return;
        }

        // E3.d ("DLL - propriete de la racine par frame" item 1, docs/plan-e3-collisions.md): for a
        // controller-driven entity the root is this frame's source of truth - CharacterMotionSystem
        // registers/updates controllers at the head of the SAME frame's World.Update, strictly before
        // Entity.Update ever reaches this GameplayProxy.Update (CharacterMotionSystem.cs:96-98,
        // :272-278), so by the time this runs the controller has always had at least one update since
        // registration; there is no "before the first controller update" case to special-case here.
        // Pulled with double-rounding, not a float cast: at this magnitude (~950 px) a float's ULP is
        // already ~8 16.16 units, so `(int)(px * 65536f)` would silently drop low bits every frame
        // (quantization, not a bug - see the 100-frame bounded-drift acceptance test).
        //
        // E4.f follow-up (engine-integration fix, coordinator-dispositioned FIX after verifier A1/A2):
        // while <see cref="WasEntitySupportedLastTick"/> is set, PosZ is NOT re-derived from the float
        // root here - X/Y still are (support never constrains them). Rationale: map 389's own authored
        // entity-support margins can be as tight as ONE 16.16 unit (e.g. sailor 11 perched on record 2's
        // own real top, PhysicsEngine.cs:205's own strict edge) - float32's own representable precision at
        // ~400px magnitude is roughly 1/32 px (2^-23 relative ULP), i.e. ~2048 16.16 units, VASTLY coarser
        // than that 1-unit margin. While supported, EvaluateEntitySupport already configured the controller
        // as Gravity 0 + SetVerticalVelocity(0) and pushed the authoritative logical PosZ through
        // PushLogicalPositionToRoot THIS SAME tick (see that method's own doc) - the root's own Z cannot
        // legitimately change on its own between two such pushes (no vertical force is being applied), so
        // pulling it back through the lossy float transform would only re-quantize an unchanged value and
        // silently erode the authored margin, permanently defeating EntitySupport.TryFindSupport's own
        // strict comparator one tick after the entity settles (empirically confirmed: PosZ collapsed
        // 26214401 -> 26214400 on the very next real World.Update before this fix). Logical PosZ stays the
        // single source of truth for as long as the flag holds; the very next tick that finds NO support
        // resumes the normal float pull immediately (WasEntitySupportedLastTick clears in
        // EvaluateEntitySupport's own "not found" branch, evaluated after this same frame's pull already
        // ran using last tick's still-true flag - a documented one-frame-late transition, harmless since
        // the flag itself was already accurate for what THIS pull needed to decide).
        if (Controller != null && Owner.RootComponent != null)
        {
            var root = Owner.RootComponent.LocalTransform.Position;
            PosX = (int)Math.Round((double)root.X * 65536.0);
            PosY = (int)Math.Round((double)root.Y * 65536.0);
            if (!WasEntitySupportedLastTick)
            {
                PosZ = (int)Math.Round((double)root.Z * 65536.0);
            }

            IsOnGround = Controller.IsGrounded ? 1 : 0;
        }

        if (!IsPlayer)
        {
            // Bug fix (user-reported runtime pacing bug - see AlundraLogicClock's own class doc for the
            // full diagnosis/log evidence): the pick/run status-machine pass is counted in FRAMES by the
            // original (0x37 Wait, the whole EntityManager.UpdateEntitiesEvents chronology) - it must run
            // at the fixed 50 Hz logic rate, not once per rendered frame. ticksThisFrame is usually 1 (a
            // display frame roughly matches 50 Hz) but can be 0 (a fast render frame, nothing new to pick
            // this frame - EventTrigger simply stays whatever RunPickedEvent last cleared it to) or up to
            // AlundraScriptedMotion.MaxTicksPerFrame under catch-up (a stalled/slow frame).
            var ticksThisFrame = ScriptHost.LogicTicksThisFrame(elapsedTime);
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                PickEventTrigger();
                RunPickedEvent(ScriptHost.Runner);
            }

            // E4.b (docs/plan-e4-deplacement-scripte.md): scripted mover for every controller-driven NPC -
            // port of PhysicsEngine.UpdateEntityPhysics (:1579-1598) restricted to the flat-ground half
            // already ported for the hero (AlundraPlayerManager/AlundraScriptedMotion's own class docs).
            // Placed AFTER this frame's own pick/run above so a Load/Tick program that just set
            // TargetDirection/TargetAnimationId this same frame (0x09/0x1A, or 0x5B/0x5A once E4.c lands)
            // is already visible to this tick, matching the original's own MovePlayer-then-physics order.
            //
            // Pre-read finding (E4.b item 1a, docs/plan-e4-deplacement-scripte.md): the original's
            // UpdateEntityPhysics reads entity.AnimationSet, not TargetAnimationId directly - that field is
            // only reassigned by EntityManager.UpdateAnimation (EntityManager.cs:203-248) at the exact
            // moment the animation actually SWITCHES (CurrentAnimationId != TargetAnimationId), i.e. it
            // tracks the CURRENTLY PLAYING animation, not the just-written target. This proxy's own
            // equivalent of that reassignment site is AlundraWorldProxy.SyncAnimation - it likewise only
            // updates CurrentAnimationId when TryResolveAnimationTarget reports a change - so
            // AlundraScriptedMotion.TickScriptedNpc below is keyed off CurrentAnimationId, not
            // TargetAnimationId (unlike the hero's own AlundraPlayerManager.Tick, out of E4.b's scope to
            // change). Documented accepted deviation: SyncAnimation runs at the END of this same Update
            // call (below), so this tick sees CurrentAnimationId as of the END of the PREVIOUS frame - one
            // frame of latency behind a same-frame TargetAnimationId write, exactly the same shape as this
            // class' own documented one-frame World/entity latency (see this method's own doc, "Accepted
            // deviation" paragraph) - "à défaut d'équivalent exact, utiliser l'anim courante synchronisée et
            // documenter l'écart" per the plan.
            //
            // E4.e (docs/plan-e4-deplacement-scripte.md): unconditional, not gated on Controller != null -
            // the original applies UpdateEntityPhysics to every entity in g_activeEntities regardless of
            // whether it carries a body/controller (PhysicsEngine.cs:12-14's own loop has no such gate);
            // RunOneKinematicTick's own Controller-null branch (AlundraScriptedMotion, "PosX += FinalForceX"
            // else-arm) already existed for exactly this case - it is what the pre-E3 hero used - and is
            // production-safe: every body-carrying entity keeps a real CharacterControllerComponent
            // (E4.a), so this fallback only ever executes for the harness's own bare proxies (no
            // Owner/World at all) or a genuinely controller-less sprite-only prefab (11 on map 389, whose
            // Speed is 0 for every AnimSet they carry - this is a no-op integration for them in practice).
            //
            // Bug fix (AlundraLogicClock's own class doc): deliberately STAYS per RENDERED frame, not
            // gated on ticksThisFrame - it already carries its OWN internal 50 Hz accumulator
            // (PhysicsTickAccumulator), same constants as the logic clock, and time-based Move commands
            // routed through it need to stay smooth at display rate rather than snapping once per logic
            // tick (a controller-driven entity's engine-owned interpolation already expects a per-frame
            // call - see SyncTransform's own doc for the same "must follow every rendered frame" reasoning
            // applied to the pose it produces). The motion clock and the logic clock are two SEPARATE
            // accumulators, both stepping at the same 50 Hz - acceptable because they never need to agree
            // frame-by-frame with each other, only converge to the same long-run rate: motion is a smooth,
            // idempotent-in-the-limit visual quantity, while the logic clock's job is exact tick COUNTING
            // for frame-counted game logic (Wait, MapEvents) that must never run at the wrong rate.
            AlundraScriptedMotion.TickScriptedNpc(this, elapsedTime);

            // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): entity-vs-entity Z support clamp
            // - AFTER the horizontal tick above, so a walk that just moved this entity out of a platform's
            // XY footprint loses support the SAME frame (matching the original's own "hors de l'empreinte:
            // plus de support" behaviour), not one frame late. See EvaluateEntitySupport's own doc.
            //
            // Bug fix (AlundraLogicClock's own class doc): gated on ticksThisFrame, same as the pick/run
            // pass above - EvaluateEntitySupport's own ForceZ gravity-decay branch is a per-TICK quantity
            // (ForceZ -= Gravity<<8, PhysicsEngine.cs:1460-1476, ported verbatim inside that method); at
            // rendered-frame rate it would decay 2.5x too fast, the exact same class of bug this whole fix
            // addresses. The WHOLE method (detection + pin), not just its decay half, runs per tick: the
            // original evaluates CheckEntityCollisionDown once per its own fixed frame too (i.e. once per
            // logic tick here), and a supported entity provably cannot move between two ticks of the SAME
            // rendered frame (EvaluateEntitySupport's own "if found" branch already zeroes Gravity and
            // SetVerticalVelocity(0) on the controller the first time support is found this frame, so a
            // second call in the same loop iteration re-evaluates the SAME still-true support and is a
            // cheap, side-effect-free confirmation, not wasted divergent work).
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                EvaluateEntitySupport(ScriptHost.Collidables);
            }
        }
        else
        {
            // E2: port of PlayerManager.MovePlayer, called at the head of the original's own
            // UpdateEntitiesEvents (EntityManager.cs:808) - now this proxy's own per-frame tick instead
            // (decision D2) - plus the kinematic integration PhysicsEngine.UpdateEntitiesPhysics normally
            // drives for the player (see AlundraPlayerManager's own class doc). A no-op whenever this
            // world has no AlundraPlayerController possessing a pawn yet (see IAlundraScriptHost.PlayerController's
            // own doc - headless test harnesses in particular construct their own player proxy with no
            // controller at all, by design).
            var playerController = ScriptHost.PlayerController;
            if (playerController != null)
            {
                var pad = playerController.BuildPadState();
                AlundraPlayerManager.MovePlayer(this, in pad, ScriptHost.GameState);
                AlundraPlayerManager.Tick(this, elapsedTime);
            }
        }

        AlundraWorldProxy.SyncAnimation(Owner);
        AlundraWorldProxy.SyncTransform(Owner);
    }

    /// <summary>
    /// Phase 1 ("pick") of the original's status machine, ported for THIS entity only - faithful
    /// per-entity port of the per-iteration body of <c>EntityManager.UpdateEntitiesEvents</c>'s first loop
    /// (EntityManager.cs:810-870, exactly the body <see cref="AlundraWorldProxy"/>'s own (now removed)
    /// <c>RunEntityEventsPass</c> used to run over every entity). Requires <see cref="ScriptHost"/> to be
    /// non-null (only called from <see cref="Update"/>, which already checked).
    /// </summary>
    internal void PickEventTrigger()
    {
        var eventProgramType = ScriptHelper.ProgramUnknown;

        if (BlockedByEntity == null)
        {
            switch (Status)
            {
                case EntityStatus.Destroyed:
                case EntityStatus.FlagToDestroy:
                    eventProgramType = ScriptHelper.ProgramUnknown;
                    break;

                case EntityStatus.Loaded:
                    eventProgramType = ScriptHelper.ProgramALoad;
                    Status = EntityStatus.Normal;
                    // Transition-only (fires once per entity, not every frame), but guarded anyway: the
                    // interpolated string itself is built eagerly regardless of Logs.WriteDebug's own
                    // internal verbosity check, so this avoids that formatting cost too when the existing
                    // Logs.Verbosity gate is raised above Debug.
                    if (Logs.Verbosity <= LogVerbosity.Debug)
                    {
                        Logs.WriteDebug($"AlundraEntityScriptProxy: entity[{EntityRefId}] Loaded -> Normal (slot A).");
                    }

                    break;

                case EntityStatus.Normal:
                {
                    var flags = Flags;
                    var host = ScriptHost!;

                    if ((flags & EntityFlags.DestroyOnSlidingSlope) != 0 && Slope_18c == 4)
                    {
                        host.DestroyEntity(this, 6);
                        eventProgramType = ScriptHelper.ProgramUnknown;
                    }
                    else if ((flags & EntityFlags.DestroyOnVramFlags) != 0 && (CombinedVramFlagsOR & 0x8004U) != 0)
                    {
                        host.DestroyEntity(this, -1);
                        eventProgramType = ScriptHelper.ProgramUnknown;
                    }
                    else if (((flags & EntityFlags.DeactivateOnImpact) != 0 && (ForceAdjusted != 0 || IsOnGround != 0))
                             || ((flags & EntityFlags.DeactivateOnHit) != 0 && HitCounter != 0)
                             || ((flags & EntityFlags.DeactivateOnAnimationEnd) != 0 && ForceResetAnimationFlag != 0))
                    {
                        Status = EntityStatus.Deactivated;
                        eventProgramType = ScriptHelper.ProgramEDeactivate;
                        if (Logs.Verbosity <= LogVerbosity.Debug)
                        {
                            Logs.WriteDebug($"AlundraEntityScriptProxy: entity[{EntityRefId}] Normal -> Deactivated (slot E).");
                        }
                    }
                    else
                    {
                        eventProgramType = ScriptHelper.ProgramDTouch;

                        if (TouchingEntity == null)
                        {
                            eventProgramType = ScriptHelper.ProgramCTick;

                            if (ReferenceEquals(host.ActiveCollisionEntity, this)
                                && (ProgramIndexes[5] != 0 || SpriteProgramIndexes[5] != 0))
                            {
                                eventProgramType = ScriptHelper.ProgramFInteract;
                            }
                        }
                    }

                    break;
                }

                case EntityStatus.Deactivated:
                    eventProgramType = ScriptHelper.ProgramEDeactivate;
                    break;
            }
        }

        EventTrigger = eventProgramType;
    }

    /// <summary>
    /// Phase 2 ("run") of the original's status machine, ported for THIS entity only - one dispatch, not
    /// the original's own re-scanning do/while (that loop is now <see cref="AlundraWorldProxy.RunPendingEventTriggers"/>,
    /// run by the world once every entity has had its own <see cref="Update"/> this frame - decision D3).
    /// A no-op when <see cref="EventTrigger"/> is <see cref="ScriptHelper.ProgramUnknown"/> (nothing was
    /// picked, or it was already run and cleared).
    /// </summary>
    internal void RunPickedEvent(IEventProgramRunner runner)
    {
        if (EventTrigger == ScriptHelper.ProgramUnknown)
        {
            return;
        }

        var programIndex = ProgramIndexes[EventTrigger] & 0x7f;

        if (programIndex == 0)
        {
            // g_entityEventFunctionsByType => AI
            runner.RunSpriteEvent(this);
        }
        else
        {
            runner.RunScript(this, EventTrigger);
        }

        EventTrigger = -1;
    }

    /// <summary>
    /// Port of <c>EntityManager.cs:127-136</c> (spawn clamp) and the unconditional ground clamp in
    /// <c>PhysicsEngine.cs:123-135</c>, restricted to this entity's own header-box footprint - the 4
    /// corners of its <see cref="CollisionComponent"/>'s <see cref="Box"/> fixture, far edge exclusive,
    /// same corners <see cref="CharacterControllerComponent"/>'s own C4 ground probe uses
    /// (docs/plan-e3-collisions.md E3.c/E3.c-bis) - sampled against <c>World.CollisionField</c>. The far
    /// edge is pushed one <see cref="MathF.BitDecrement"/> below <c>centre + half-extent</c> (the FINAL
    /// summed coordinate, not the half-extent before the addition) - a fixed <c>1/65536f</c> subtraction
    /// is only exclusive near zero (float32's ULP grows with magnitude and swallows a fixed epsilon at
    /// Alundra's real map coordinates, e.g. 928 px - see E3.c-bis's own doc on
    /// <see cref="CharacterControllerComponent"/>'s identical fix for the full incident write-up); one ULP
    /// below the already-rounded sum stays exclusive at any magnitude. RAISES <see cref="PosZ"/> to the
    /// highest corner's ground height, never lowers it: a written position under its effective ground is
    /// meant to be caught here (a real fall is the mover's own per-frame gravity job, not this helper's -
    /// see the callers' own doc for why skipping this would let a scripted write onto a cell that sits
    /// below the sampled ground fall forever). A no-op without a <see cref="CollisionComponent"/> Box
    /// fixture or an installed <c>World.CollisionField</c> - PosZ is simply left as written, exactly like
    /// before E3.d.
    /// </summary>
    internal void ClampToGround()
    {
        var field = Owner?.World?.CollisionField;
        if (field == null)
        {
            return;
        }

        var collisionComponent = Owner!.GetComponent<CollisionComponent>();
        ColliderFixture? boxFixture = null;
        if (collisionComponent != null)
        {
            for (var i = 0; i < collisionComponent.Fixtures.Count; i++)
            {
                if (collisionComponent.Fixtures[i].Shape is Box)
                {
                    boxFixture = collisionComponent.Fixtures[i];
                    break;
                }
            }
        }

        if (boxFixture?.Shape is not Box box)
        {
            return;
        }

        var centerX = PosX / 65536f + boxFixture.LocalPosition.X;
        var centerY = PosY / 65536f + boxFixture.LocalPosition.Y;
        var halfX = box.Size.X / 2f;
        var halfY = box.Size.Y / 2f;

        var minX = centerX - halfX;
        var maxX = MathF.BitDecrement(centerX + halfX);
        var minY = centerY - halfY;
        var maxY = MathF.BitDecrement(centerY + halfY);

        var hasGround = false;
        var groundMax = 0f;
        SampleGroundCorner(field, minX, minY, ref hasGround, ref groundMax);
        SampleGroundCorner(field, minX, maxY, ref hasGround, ref groundMax);
        SampleGroundCorner(field, maxX, minY, ref hasGround, ref groundMax);
        SampleGroundCorner(field, maxX, maxY, ref hasGround, ref groundMax);

        if (!hasGround)
        {
            return;
        }

        var groundPosZ = (int)Math.Round((double)groundMax * 65536.0);
        if (groundPosZ > PosZ)
        {
            PosZ = groundPosZ;
        }
    }

    private static void SampleGroundCorner(ICollisionField field, float x, float y, ref bool hasGround, ref float groundMax)
    {
        if (!field.TrySampleGround(new Vector3(x, y, 0f), float.MaxValue, out var sample) || !sample.HasGround)
        {
            return;
        }

        if (!hasGround || sample.GroundHeight > groundMax)
        {
            groundMax = sample.GroundHeight;
            hasGround = true;
        }
    }

    /// <summary>
    /// Routes a scripted (post-spawn) write to <see cref="PosX"/>/<see cref="PosY"/>/<see cref="PosZ"/>
    /// onto the CasaEngine root transform - docs/plan-e3-collisions.md E3.d "DLL - propriete de la
    /// racine par frame" item 4 (grep sites: <c>AlundraEventProgramRunner</c>'s 0x64
    /// SetEntitiesPosition/0x65 AddEntitiesPositionOffset/0x8B SpawnEntityNextToEntity). A no-op for
    /// every entity WITHOUT a <see cref="Controller"/> - those keep the deferred per-frame
    /// re-derivation <see cref="AlundraWorldProxy.SyncTransform"/> already does every frame (see that
    /// method's own doc), unchanged since E3.a. For a controller-driven entity the root write can no
    /// longer wait for <see cref="AlundraWorldProxy.SyncTransform"/> - that method now skips the root
    /// write entirely for a controller-driven entity (item 3 of the same plan section) - so this pushes
    /// the (possibly ground-clamped, see <see cref="ClampToGround"/>) logical position onto the root
    /// immediately, re-projects the sprite the same frame, and calls
    /// <see cref="CharacterControllerComponent.Teleport"/> so its velocity/collision state resets onto
    /// the new pose, exactly like a scripted jump resets the original's own force/ground state
    /// (<c>EntityManager.cs:127-136</c>).
    /// </summary>
    internal void PushLogicalPositionToRoot()
    {
        if (Controller == null || Owner?.RootComponent == null)
        {
            return;
        }

        ClampToGround();

        var root = AlundraWorldProxy.ResolveLogicalPosition(PosX, PosY, PosZ);
        Owner.RootComponent.LocalTransform.Position = root;
        RenderProjection?.UpdateProjection();
        Controller.Teleport(root);
    }

    /// <summary>
    /// Routes one 50 Hz sub-step's horizontal displacement through the controller's own
    /// <see cref="CharacterControllerComponent.Move"/>, then re-pulls <see cref="PosX"/>/<see cref="PosY"/>/
    /// <see cref="PosZ"/> from the resulting root - docs/plan-e3-collisions.md E3.d "DLL - propriete de
    /// la racine par frame" item 2. <c>Move</c> only resolves the horizontal axes (wall/walkability/
    /// step-height, C5) - it never touches the vertical axis, so the re-pulled <see cref="PosZ"/> is
    /// unchanged by this call; ground/gravity resolution stays this frame's own
    /// CharacterMotionSystem pass, pulled separately at the head of
    /// <see cref="Update"/>. <paramref name="deltaXPixels"/>/<paramref name="deltaYPixels"/> are the
    /// sub-step's <c>ΔPosX</c>/<c>ΔPosY</c> (16.16) already converted to float pixels INCLUDING their
    /// fraction (<c>Δ / 65536f</c>, not the original's own truncated <c>Δ &gt;&gt; 16</c>) - see
    /// <see cref="AlundraPlayerManager.RunOneTick"/>'s own call site. An axis <c>Move</c> blocks leaves
    /// <see cref="ForceX"/>/<see cref="ForceY"/> untouched by design (no per-axis correction - accepted
    /// deviation, documented on the same plan section); it DOES set <see cref="ForceAdjusted"/> (E4.d)
    /// when the controller's own returned displacement falls short of what was requested here by more
    /// than <see cref="ForceAdjustedEpsilonPixels"/> on either horizontal axis - the DLL's own equivalent
    /// of the original's "movement was curtailed" signal (see <see cref="ForceAdjusted"/>'s own doc). A
    /// no-op without a controller (the caller falls back to its own direct
    /// <see cref="PosX"/>/<see cref="PosY"/> += in that case) - <see cref="ForceAdjusted"/> is left
    /// untouched, same as every other controller-gated site on this class.
    /// </summary>
    internal void MoveControllerAndPullPosition(float deltaXPixels, float deltaYPixels)
    {
        if (Controller == null || Owner?.RootComponent == null)
        {
            return;
        }

        var requested = new Vector3(deltaXPixels, deltaYPixels, 0f);
        var actual = Controller.Move(requested);

        if (MathF.Abs(actual.X - requested.X) > ForceAdjustedEpsilonPixels
            || MathF.Abs(actual.Y - requested.Y) > ForceAdjustedEpsilonPixels)
        {
            ForceAdjusted = 1;
        }

        var root = Owner.RootComponent.LocalTransform.Position;
        PosX = (int)Math.Round((double)root.X * 65536.0);
        PosY = (int)Math.Round((double)root.Y * 65536.0);

        // Engine-integration fix (same rationale as Update's own head-pull - see
        // WasEntitySupportedLastTick's own doc): this is a SECOND pull site, reached earlier in the same
        // frame (RunOneKinematicTick -> here, called from TickScriptedNpc, which runs BEFORE
        // EvaluateEntitySupport in Update). Without this same guard here, THIS site alone re-quantized
        // PosZ through the float32 root every tick regardless of support, which is what actually caused
        // the 26214401 -> 26214400 collapse (root-caused via a debug trace: Update's own head-pull was
        // already correctly gated and read back 26214401 intact, but this method's unconditional PosZ
        // pull silently overwrote it moments later, in the SAME frame, before EvaluateEntitySupport's own
        // re-evaluation ever ran).
        if (!WasEntitySupportedLastTick)
        {
            PosZ = (int)Math.Round((double)root.Z * 65536.0);
        }
    }

    /// <summary>Small horizontal-axis tolerance <see cref="MoveControllerAndPullPosition"/> uses to decide
    /// whether the controller's own returned displacement counts as "curtailed" (sets
    /// <see cref="ForceAdjusted"/>) - well under a single pixel, so ordinary floating-point noise from the
    /// <c>Move</c> round trip never sets it spuriously, while any REAL wall/step-height block (which stops
    /// the entity short by at least a fraction of a pixel every tick it keeps pushing) reliably does.
    /// </summary>
    private const float ForceAdjustedEpsilonPixels = 0.01f;

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {
    }

    public override void OnEndPlay(World world)
    {
    }

    public override IGameplayProxy Clone()
    {
        var clone = new AlundraEntityScriptProxy
        {
            IsPlayer = IsPlayer,
            LogicEntity = LogicEntity,
            Index = Index,
            Index2 = Index2,
            ChildEntity = ChildEntity,
            ParentEntity = ParentEntity,
            Status = Status,
            Hp = Hp,
            HpMax = HpMax,
            FrameCounter = FrameCounter,
            BlockedByEntity = BlockedByEntity,
            Flags2 = Flags2,
            PlatformEntity = PlatformEntity,
            CarriedEntity = CarriedEntity,
            RelativeWarpOffsetX = RelativeWarpOffsetX,
            RelativeWarpOffsetY = RelativeWarpOffsetY,
            RelativeWarpOffsetZ = RelativeWarpOffsetZ,
            ContentsItemId = ContentsItemId,
            ContentsGameFlag = ContentsGameFlag,
            EntityRefId = EntityRefId,
            SpriteTableIndex = SpriteTableIndex,
            Flags = Flags,
            TargetAnimationId = TargetAnimationId,
            TargetDirection = TargetDirection,
            CurrentAnimationId = CurrentAnimationId,
            CurrentDirection = CurrentDirection,
            AnimationDirection = AnimationDirection,
            NextFrameDelay = NextFrameDelay,
            ForceResetAnimationFlag = ForceResetAnimationFlag,
            AnimCompleteCounter = AnimCompleteCounter,
            AnimFlags = AnimFlags,
            ForceZ = ForceZ,
            TargetForceX = TargetForceX,
            TargetForceY = TargetForceY,
            ForceX = ForceX,
            ForceY = ForceY,
            PreviousAdjustedForceX = PreviousAdjustedForceX,
            PreviousAdjustedForceY = PreviousAdjustedForceY,
            ForceStepX = ForceStepX,
            ForceStepY = ForceStepY,
            AdjustedForceX = AdjustedForceX,
            AdjustedForceY = AdjustedForceY,
            FinalForceX = FinalForceX,
            FinalForceY = FinalForceY,
            FinalForceZ = FinalForceZ,
            Acceleration = Acceleration,
            Speed = Speed,
            IsZForceApplied = IsZForceApplied,
            ScreenClipX = ScreenClipX,
            ScreenClipY = ScreenClipY,
            ScreenClipZ = ScreenClipZ,
            NegModX = NegModX,
            NegModY = NegModY,
            NegModZ = NegModZ,
            PosX = PosX,
            PosY = PosY,
            PosZ = PosZ,
            TileX = TileX,
            TileY = TileY,
            TileZ = TileZ,
            RidingEntity = RidingEntity,
            XCollisionEntity = XCollisionEntity,
            FloorHeight = FloorHeight,
            TerrainHeight = TerrainHeight,
            ForceAdjusted = ForceAdjusted,
            CollidedWithEntityZ = CollidedWithEntityZ,
            IsOnGround = IsOnGround,
            PlatformUpdateFlag = PlatformUpdateFlag,
            _16c = _16c,
            HitBoxOriginX = HitBoxOriginX,
            HitBoxOriginY = HitBoxOriginY,
            HitBoxOriginZ = HitBoxOriginZ,
            _17c = _17c,
            CombinedVramFlagsOR = CombinedVramFlagsOR,
            CombinedVramFlagsAND = CombinedVramFlagsAND,
            TileAttributes = TileAttributes,
            Slope_18c = Slope_18c,
            Slope_190 = Slope_190,
            ZUpperBound = ZUpperBound,
            RenderSortKey = RenderSortKey,
            DamagedTickCounter = DamagedTickCounter,
            FrameCollisionTickCounter = FrameCollisionTickCounter,
            ModdedPosX = ModdedPosX,
            ModdedPosY = ModdedPosY,
            ModdedPosZ = ModdedPosZ,
            ModX = ModX,
            ModY = ModY,
            ModZ = ModZ,
            Width = Width,
            Height = Height,
            Depth = Depth,
            HitBoxX = HitBoxX,
            HitBoxY = HitBoxY,
            HitBoxZ = HitBoxZ,
            CollisionOffsetX = CollisionOffsetX,
            CollisionOffsetY = CollisionOffsetY,
            CollisionOffsetZ = CollisionOffsetZ,
            CollisionWidth = CollisionWidth,
            CollisionDepth = CollisionDepth,
            CollisionHeight = CollisionHeight,
            HitCounter = HitCounter,
            TouchingEntity = TouchingEntity,
            EventTrigger = EventTrigger,
            MapEventProgramId = MapEventProgramId,
            LogicContextEntity = LogicContextEntity,
            RenderProjection = RenderProjection,
            Controller = Controller,
            MapGravity = MapGravity,
            MapMaxFallSpeed = MapMaxFallSpeed,
            IdsvByAnimDirection = IdsvByAnimDirection,
            AnimationEndByAnimDirection = AnimationEndByAnimDirection,
            AnimSetsByAnim = AnimSetsByAnim,
            PhysicsTickAccumulator = PhysicsTickAccumulator,
            LastTargetAnimationId = LastTargetAnimationId,
            LastTargetDirection = LastTargetDirection,
            Bytes = (byte[])Bytes.Clone(),
            DelayOrAngleOrEntityId = DelayOrAngleOrEntityId,
            ItemState = ItemState,
            AIValues = (short[])AIValues.Clone(),
        };

        ProgramIndexes.CopyTo(clone.ProgramIndexes, 0);
        SpriteProgramIndexes.CopyTo(clone.SpriteProgramIndexes, 0);
        MapHeights.CopyTo(clone.MapHeights, 0);

        return clone;
    }
}
