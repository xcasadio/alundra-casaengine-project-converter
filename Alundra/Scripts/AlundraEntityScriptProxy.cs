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
    /// (initially the player itself). Retargeted by opcodes 0x42 and 0x43, the only two that write it
    /// (E19.a, ALUN_CD.EXE 0x8003E808/0x8003E81C; 0x66 writes the owner's state, not this word, contrary to
    /// what this comment used to say). Null means "the entity itself" (<c>InitializeEntity</c>,
    /// 0x80042028): <see cref="AlundraEventProgramRunner"/> resolves <c>LogicEntity ?? owner</c> before every
    /// instruction, and a clone starts on itself. Deliberately a SEPARATE
    /// field from this proxy's own <see cref="LogicContextEntity"/> below, which is an engine-only,
    /// unrelated-by-coincidence-of-name back-pointer to this proxy's OWN CasaEngine <see cref="Entity"/>
    /// (set once at spawn, see <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>) - conflating the
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

    /// <summary>
    /// E19.h1b2 (H1B2-R2, docs/plan-e19-opcodes.md §1.2n.1c): the <c>Height</c> byte of the entity's record (record byte 9, which the binary reads at
    /// <c>+0x44</c> + 9), raw: the target of <c>0x22</c>/<c>0x23</c> is this value shifted left by 19, literally (ADR-0026). Set by
    /// <see cref="EntityRecordMapper"/> only when the record carries a <c>Height</c>; null for the hero and the bare proxies of the tests ("no record").
    /// </summary>
    public int? RecordHeight;

    public int EntityRefId;
    public readonly int[] ProgramIndexes = new int[6]; //4c
    //public SpriteRecord? SpriteRecord;
    public uint SpriteTableIndex;

    /// <summary>
    /// E16.e L1/SE6 (docs/plan-e16-etat-partie.md): the original's own sprite type, what
    /// <c>EntityManager.InitializeEntity</c> (0x80039D04) stores in <c>entity.SpriteTableIndex</c> - the record's
    /// <see cref="SpriteTableIndex"/> with <c>0x100</c> added for a map sprite (<c>SpriteDirection &amp; 0x80</c>,
    /// <c>GameEngine.SpawnEntity</c>, GameEngine.cs:732-737), 0 for the hero (<c>ResetEntityState</c>,
    /// 0x80031974). <see cref="SpriteTableIndex"/> keeps the raw record value, as the record mapper always did.
    /// Written by the two spawn paths (<see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>,
    /// <see cref="AlundraWorldProxy"/>'s hero adoption) next to <see cref="SpriteProgramIndexes"/>; -1 when no
    /// header resolved (degraded spawn, no native handler can apply). Read by
    /// <see cref="AlundraEventProgramRunner.RunSpriteEvent"/> to recognise the save book (type
    /// <see cref="AlundraSaveBook.SpriteType"/>).
    /// </summary>
    public int SpriteType = -1;

    /// <summary>
    /// E19.f4b (docs/plan-e19-opcodes.md, F4B-R3): the speaker's dialogue portrait (the sprite of its first image and its true size), written at the spawn by
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> from the header of its record (<see cref="SpriteRecordHeader.DialoguePortrait"/>), present exactly where
    /// <see cref="Flags"/> has <see cref="EntityFlags.HasPortrait"/>. Null for every other entity and for the hero (its adoption does not write it). Copied by <see cref="Clone"/>.
    /// </summary>
    public DialoguePortraitRef? DialoguePortrait;

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

    /// <summary>
    /// Engine-only, not part of the original struct. Raised by
    /// <see cref="AlundraEntitySpawnFactory.OnAnimationFinished"/> when a <c>Chain</c> terminator fires, and
    /// consumed by <see cref="AlundraFrameSyncPasses.SyncAnimation"/> to restart the animation even when the
    /// chain target turns out to be the animation that just ended.
    ///
    /// <para>Needed because the original expresses a LOOPING animation two different ways, and only one
    /// of them survives the conversion as <c>AnimationType.Loop</c>: a terminator with
    /// <c>TerminatorCode == 1</c> becomes a real engine loop, but a terminator that CHAINS BACK TO ITSELF
    /// (<c>ChainTo</c> = the same animation id) is exported as <c>Once</c> + a chain edge. The hero's own
    /// walk (anim 1) is the second kind in all four directions, while his idle (anim 0) is the first -
    /// which is exactly why the idle looped and the walk froze on its last frame (user report,
    /// 2026-08-26).</para>
    ///
    /// <para>Without this flag <see cref="AlundraFrameSyncPasses.TryResolveAnimationTarget"/> reports "nothing
    /// to do" for a self-chain - it only restarts when <see cref="CurrentAnimationId"/> or
    /// <see cref="AnimationDirection"/> actually CHANGE, and a self-chain changes neither - so
    /// <c>SetCurrentAnimation(..., forceReset: true)</c> was never reached and the sampler stayed parked
    /// at <c>IsFinished</c> on the terminal pose. Set on EVERY chain, not just self-chains: a chain onto a
    /// different animation already restarts through the normal id-changed path, so raising it there is
    /// redundant but harmless, and it keeps the rule "a chain always restarts the target" in one piece.</para>
    /// </summary>
    public int PendingChainRestartFlag;
    public int AnimCompleteCounter;

    /// <summary>
    /// Engine-only (E19.c2, docs/plan-e19-opcodes.md §1.2f, ADR-0019): the sprite whose logical clock
    /// (<see cref="AnimatedSpriteComponent.SetLogicalTickRate"/>) drives the ends of this entity's animations, cached when
    /// <see cref="AlundraEntitySpawnFactory.SubscribeAnimationEndBridge"/> turns the clock on. Null for an entity without
    /// a sprite. Not copied by <see cref="Clone"/>: a clone has no subscription.
    /// </summary>
    internal AnimatedSpriteComponent? LogicalClockSprite;

    /// <summary>
    /// Engine-only (E19.c2): the logic ticks of the current frame at which <see cref="AlundraFrameSyncPasses.StepAnimationClock"/>
    /// found an animation switch pending (the binary's <c>UpdateAnimation</c> of that tick switches instead of advancing the
    /// old animation). Read and zeroed by <see cref="AlundraFrameSyncPasses.SyncAnimation"/>, once per frame, whatever it
    /// decides. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int AnimationSwitchTicksReserved;

    /// <summary>
    /// Engine-only (E19.c2): set by <see cref="AlundraFrameSyncPasses.SyncAnimation"/> when it switches an animation on a frame
    /// that had no logic tick to spend on the switch; the next tick is then consumed without advancing the new animation (the
    /// binary shows the new animation's frame 0 on the tick of the switch without counting it). Cleared by a switch that reserves
    /// a tick. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal bool AnimationSwitchTickOwed;

    /// <summary>
    /// Engine-only (E19.c2): a Hold end already counted by <c>0x1C</c>/<c>0x1D</c> that waits for the switch it asked for
    /// (<c>CurrentAnimationId</c> = ~<c>TargetAnimationId</c>). While set, the Hold flag is invisible to those opcodes, so a
    /// catch-up frame cannot count the same end twice before the switch clears the flag. Cleared by any switch
    /// (<see cref="AlundraFrameSyncPasses.SyncAnimation"/>, <see cref="AlundraFrameSyncPasses.ClearHoldFlagsOfPendingSwitches"/>).
    /// Not copied by <see cref="Clone"/>.
    /// </summary>
    internal bool HoldCountedAwaitingSwitch;

    /// <summary>
    /// Engine-only (E19.d2c1 R1, docs/plan-e19-opcodes.md §1.2h.3.1): the "impulse taken" lock of the pending animation switch. The binary gives
    /// the Z impulse of an animation (<see cref="IsZForceApplied"/>, <c>+0xF8</c>) in the switch block of its <c>UpdateAnimation</c>, at the tick
    /// of the switch; here the switch is validated at the end of the frame by <see cref="AlundraFrameSyncPasses.SyncAnimation"/>, after any
    /// number of ticks (catch-up), so the first <see cref="AlundraFrameSyncPasses.StepAnimationClock"/> that sees the switch pending takes the
    /// impulse and raises this lock, the next ticks of the frame do not take it again, and the validation lowers it. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal bool ZImpulseTaken;

    /// <summary>
    /// Engine-only (E19.d2c1 R1): an impulse owed to a switch that <see cref="AlundraFrameSyncPasses.SyncAnimation"/> validated on a frame without
    /// a logic tick (a target written by a map event, for example): the first <see cref="AlundraFrameSyncPasses.StepAnimationClock"/> that follows
    /// takes it, from <see cref="CurrentAnimationId"/>, before anything else. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal bool ZImpulseDue;

    /// <summary>
    /// Engine-only (E19.d2c1 R2): raised with the animation of an appearance or an arrival (<see cref="SpawnAnimationId"/>) by
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> (load, <c>0x2D</c>, <c>0x8A</c>, <c>0x8B</c>) and
    /// <c>AlundraWorldProxy.AdoptPlayerPawn</c> (every arrival of the hero): the binary makes that first switch itself in
    /// <c>InitializeEntity</c> and its next <c>UpdateAnimation</c> clears the impulse before the physics, so an entity never takes off at its
    /// appearance. A pending switch whose target is that animation gives no impulse; the flag falls at the first validation of
    /// <see cref="AlundraFrameSyncPasses.SyncAnimation"/>. Another animation written by a script before that validation gives the impulse (an ordinary
    /// switch in the binary); the <c>0x1C</c> relaunch is never excluded (it clears the flag). Copied by <see cref="Clone"/>.
    /// </summary>
    internal bool SpawnAnimationActive;

    /// <summary>The animation of the appearance or arrival, see <see cref="SpawnAnimationActive"/>. Copied by <see cref="Clone"/>.</summary>
    internal uint SpawnAnimationId;

    /// <summary>
    /// Engine-only (E19.d2c1 R6, docs/plan-e19-opcodes.md §1.2h.3.1): the hero is in the air state the logic tick holds (a scripted jump, an impulse of
    /// the animation, or <c>0x1B</c> on the hero). While it is up, <see cref="AlundraScriptedMotion"/> owns the vertical - the force of the tick, the strict
    /// landing test, the exact <see cref="PosZ"/> - and the engine only follows the root: its gravity is 0, its vertical external with a positive latch. No
    /// pull of <see cref="PosZ"/> or <see cref="IsOnGround"/> from the root while it is up. Cleared at the landing and at the adoption of a new pawn. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal bool HeroAirborne;

    /// <summary>Engine-only (E19.d2c1 R6): <c>0x1B</c> was run on the hero (the logic entity): its tick enters the air state, the written <see cref="ForceZ"/> decaying from that tick.</summary>
    internal bool HeroFlyMarked;

    /// <summary>Engine-only (E19.d2c1 R6): the air state ran this tick, so <see cref="PosZ"/> is the tick's and no pull from the root may rewrite it until the next tick.</summary>
    internal bool ZHeldByTick;

    /// <summary>
    /// Engine-only (E19.d2c2 S4): the horizontal displacement REALIZED by the last kinematic tick of this entity (the position after the step minus the position
    /// before it, 16.16): what a rider takes along (the hero it carries adds it to its own XY step, the binary adds the carrier's <c>AdjustedForce</c>). The hero
    /// is updated before the entities that carry it, so what it reads is the displacement of the carrier's previous tick (one tick of phase). Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int LastTickDeltaX;

    /// <summary>See <see cref="LastTickDeltaX"/>.</summary>
    internal int LastTickDeltaY;

    /// <summary>
    /// Engine-only (E19.d2c2 F2b): the vertical force of the last tick of this entity as the binary's passes read it, BEFORE its own landing resets it (the binary computes
    /// the forces of all the entities first, <c>0x80036828</c>, and the passenger copies the carrier's force, <c>0x80037364</c>; only <c>ForceZ</c> is zeroed at the
    /// landing, <c>0x80037700</c>). A carrier with gravity at rest has -(Gravity &lt;&lt; 8) here at every tick while its <see cref="FinalForceZ"/> is 0 once it has landed.
    /// The hero carried by this entity takes it as its own tick force. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int TickForceZ;

    /// <summary>Engine-only (E19.d2c1 R6): the live engine values captured at the entry of the air state (its own gravity, <c>MaxFallSpeed</c> and vertical ownership), given back at the landing.</summary>
    internal float AirborneSavedGravity;

    /// <summary>See <see cref="AirborneSavedGravity"/>.</summary>
    internal float AirborneSavedMaxFallSpeed;

    /// <summary>See <see cref="AirborneSavedGravity"/>.</summary>
    internal bool AirborneSavedVerticalOwned;

    /// <summary>See <see cref="AirborneSavedGravity"/>: the <c>Settings.StepHeight</c> of the controller (E19.d2c2 S6, the air state sets it to 0 while <see cref="ForceZ"/> is not 0).</summary>
    internal float AirborneSavedStepHeight;

    /// <summary>
    /// Engine-only (E19.d2c2 S2): the head-of-frame pull of the root (<c>RunGameplayBlockableUpdate</c>) found the vertical NOT held elsewhere (no climb, no
    /// departure of transition, no freeze: <c>IsVerticalOwnedExternally</c> false) and the hero not in the air state, so the <see cref="IsOnGround"/> it gave is
    /// reliable. The first tick of the frame consumes it: a hero with a controller, outside the air state, enters it without impulse when this pull said not
    /// on the ground. Cleared by that tick (the decision is taken once per frame).
    /// </summary>
    internal bool HeadPullGroundTrusted;

    /// <summary>
    /// Engine-only (E19.d2c2 F3): the head-of-frame pull found the engine's ground missing (the engine does not see the boxes of entities) while the top of an entity
    /// under the box is exactly at the foot (<see cref="IsEntityTopFlushWithFoot"/>): <see cref="IsOnGround"/> was then given 1 and the first tick of the frame enters the
    /// air state at rest on that entity (no fall, <see cref="ForceZ"/> 0). Cleared by that tick. Only for the hero, with <see cref="HeadPullGroundTrusted"/>.
    /// </summary>
    internal bool HeadPullFlushOnEntity;

    /// <summary>
    /// Engine-only (E19.d2c2 S1, docs/plan-e19-opcodes.md §1.2h.3.2): the <see cref="MotionTickCount"/> at which <c>MovePlayer</c> last wrote the take-off
    /// animation (2 or <c>0x2B</c>), -1 for never. <c>MovePlayer</c> runs once per rendered frame while the binary's runs once per tick: while no tick has run
    /// since (a frame without tick) it does not rewrite the take-off target, so the jump is not lost between the press and the first tick that takes the
    /// impulse. Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int JumpStartTickStamp = -1;

    /// <summary>The impulse (<see cref="AnimSetEntry.IsZForceApplied"/>) of <paramref name="animationId"/> in this entity's animation sets, 0 when it has none.</summary>
    internal int ZImpulseOf(uint animationId)
        => AnimSetsByAnim != null && AnimSetsByAnim.TryGetValue((int)animationId, out var entry) ? entry.IsZForceApplied : 0;

    /// <summary>
    /// E19.t (D-E19-57, ADR-0028): the sound identifier <c>UpdateAnimation</c> asks for when the entity switches to <paramref name="animationId"/>
    /// (<c>0x80038BB0</c>): the byte 0xC of the animation set (<see cref="AnimSetEntry.Sfx"/>), plus 0x100 when the bit 0x20 of the byte 0xD is set
    /// (<see cref="AnimSetEntry.Acceleration"/> in the export; the decompilation reads the byte 0xB, wrong for 91 of the 2405 sets). 0 when the animation
    /// is not in the entity's sets; a result that is not positive asks nothing.
    /// </summary>
    internal int AnimationSoundOf(uint animationId)
        => AnimSetsByAnim != null && AnimSetsByAnim.TryGetValue((int)animationId, out var entry)
            ? entry.Sfx + ((entry.Acceleration & 0x20) != 0 ? 0x100 : 0)
            : 0;
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
    // E12.d (D-E12D-8): retyped from the engine Entity to this proxy type - the original compares
    // UNIFIED entities (the object carrying Flags/ProgramIndexes, i.e. our proxy), and the proxy-typed
    // comparison also kills a latent ReferenceEquals(null, null) match in EntitySearchService's
    // functions 7/8 against bare test proxies whose LogicContextEntity is null. Written once per logic
    // step for the entity by MoveControllerAndPullPosition (E19.d2b B4, D-E19-29): the obstacle of the controller's blocking report (the end-of-frame
    // contact pass of E12.d is gone).
    public AlundraEntityScriptProxy? XCollisionEntity;
    public int FloorHeight;
    public int TerrainHeight;//
    /// <summary>
    /// Port of the original's <c>Entity.ForceAdjusted</c> (E4.d, docs/plan-e4-deplacement-scripte.md):
    /// cleared once per LOGIC TICK, immediately before that tick's own kinematic step
    /// (<see cref="AlundraScriptedMotion.TickPlayer"/>/<see cref="AlundraScriptedMotion.TickScriptedNpc"/>,
    /// porting <c>PhysicsEngine.UpdateEntitiesPhysics</c>'s own top-of-frame reset, PhysicsEngine.cs:17 -
    /// "per frame" in the original IS "per tick" here, since the original's engine runs exactly one script
    /// pass and one physics pass per fixed 50 Hz frame; see the ONE-CLOCK fix doc on
    /// <see cref="AlundraScriptedMotion"/> for why this field used to go stale before that fix), set
    /// (nonzero) by <see cref="MoveControllerAndPullPosition"/> whenever, on either horizontal axis, a
    /// displacement beyond a small epsilon was requested and the controller's own <c>Move</c> returns none
    /// of it (an actual displacement within that epsilon; <c>CharacterControllerComponent.Move</c> returns
    /// the actual displacement - CharacterControllerComponent.cs:345-369) - the DLL's own equivalent of the
    /// original's "no sub-step was accepted" signal, consumed by opcode 0x1F
    /// (<see cref="AlundraEventProgramRunner"/>'s own Walk-with-collision bridge) and by 0x1E's own
    /// navigation detour. A value this field holds after its owning entity's motion tick survives
    /// unchanged across any additional RENDERED frames until the entity's next LOGIC tick (there may be
    /// zero, one, or several ticks per rendered frame - see <see cref="IAlundraScriptHost.LogicTicksThisFrame"/>),
    /// so the next tick's own script pass always reads exactly the last completed tick's own outcome, never
    /// a value a tick-less render frame silently wiped. Stays 0 the whole session for an entity with no
    /// controller (bare-fallback spawn) - <see cref="MoveControllerAndPullPosition"/> is itself a no-op in
    /// that case.
    /// <para>
    /// E19.a3 (D-E19-12, ADR-0017, superseding D-E19-10): like the binary, the flag rises only on a tick
    /// with no progress on a requested axis (<c>0x80037d54</c>, skipped by the guard of <c>0x800379a4</c>
    /// when a halved sub-step is accepted). A step the engine's cell field shortens to the contact
    /// (ADR-0045 of the engine) but that still advances leaves it at 0, so a walk chained on the tick that
    /// reaches the contact is not ended by a stale flag. The original's slide along a wall when only one
    /// corner touches stays with E19.h (D-E19-9).
    /// </para>
    /// </summary>
    public int ForceAdjusted;//0x13c

    /// <summary>
    /// Engine-only, not part of the original struct: incremented once every time this entity's own motion
    /// sub-step runs (<see cref="AlundraScriptedMotion"/>'s shared per-tick helper, both the
    /// <see cref="AlundraPlayerManager.Tick"/> and <see cref="AlundraScriptedMotion.TickScriptedNpc"/>
    /// callers) - never read by any gameplay code. Exists solely so the ONE-CLOCK invariant this class'
    /// own fix establishes (this entity's own script pass count, motion sub-step count, and
    /// <see cref="EvaluateEntitySupport"/> step count can never diverge again - see
    /// <see cref="AlundraScriptedMotion"/>'s own class doc) is independently verifiable at runtime, not
    /// merely implied by the call-site structure. Wraps around 32 bits like every other frame counter on
    /// this class (e.g. <see cref="FrameCounter"/>) - a real run never gets remotely close.
    /// </summary>
    public int MotionTickCount;

    /// <summary>See <see cref="MotionTickCount"/>'s own doc - the matching counter for
    /// <see cref="EvaluateEntitySupport"/>'s own per-tick step.</summary>
    public int SupportTickCount;

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
    /// (<see cref="AlundraEntitySpawnFactory.CreateEntityFromPrefab"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>)
    /// so the per-frame transform sync (<see cref="AlundraFrameSyncPasses.SyncTransform"/>) can re-project the
    /// sprite the same frame the logical root pose changes without a per-frame
    /// <c>Entity.GetComponent&lt;RenderProjectionComponent&gt;()</c> search. Null for a bare-fallback
    /// spawn (<see cref="AlundraEntitySpawnFactory.CreateBareEntityFromRecord"/>, which has no
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

    /// <summary>E13.d D2 (docs/plan-e13d-inventaire.md): what <see cref="AlundraGameplayFreeze"/> took away
    /// from this entity's controller and sprite while a MenuOpen freezes the world, to give it back.</summary>
    internal readonly AlundraGameplayFreeze.State FreezeState = new();

    /// <summary>E13.d D2: this entity's animated sprite, if it has one - for
    /// <see cref="AlundraGameplayFreeze"/>, which cannot reach the protected <c>Owner</c>. Looked up on
    /// each call: only a freeze or a thaw asks, never a per-frame path.</summary>
    internal AnimatedSpriteComponent? AnimatedSprite => Owner?.GetComponent<AnimatedSpriteComponent>();

    /// <summary>
    /// E19.d2b B4: how many controller steps (<see cref="MoveControllerAndPullPosition"/>) an entity shortened or cancelled, since the proxy exists.
    /// Not a game state: the arcs read it (T-REG-0 pins the arcs that must never meet an entity at 0). Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int EntityBlockCount;

    /// <summary>
    /// E19.h4 H4-R2: how many second steps (the slide along a wall of the binary's <c>ComputeXYPosition</c>, <c>0x80037A94</c>-<c>0x80037CD0</c>) the kinematic tick of this entity
    /// made, since the proxy exists. Not a game state: the arcs read it (<c>ArcsWithoutSlide</c> expects 0 outside A10J). Not copied by <see cref="Clone"/>.
    /// </summary>
    internal int SlideCount;

    /// <summary>E19.d2b: the engine entity this proxy drives (the movement obstacle probe returns obstacles as engine entities).</summary>
    internal Entity? OwnerEntity => Owner;

    /// <summary>
    /// Engine-only, not part of the original struct: this entity's own map's Gravity/ZViscosity, already
    /// converted to the units <see cref="CharacterControllerSettings.Gravity"/>/<see cref="CharacterControllerSettings.MaxFallSpeed"/>
    /// expect (E3.d's own formula: <c>mapGravity*256/65536*2500</c> / <c>mapZViscosity*256/65536*50</c> -
    /// 1250/800 on map 389). Resolved ONCE at spawn (<see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>/
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
    /// doc, still fed by <see cref="AlundraEntitySpawnFactory.ResolveMapGravitySettings"/> for parity with the
    /// hero's own unrelated E3.d path, <see cref="AlundraWorldProxy.AdoptPlayerPawn"/>, which sets its
    /// controller's real Settings.Gravity/MaxFallSpeed directly and is OUT OF SCOPE for this fix - the hero
    /// keeps its engine-driven vertical unchanged). Called once at spawn
    /// (<see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>), by the 0x16/0x17 and 0x62/0x63 opcode
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
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>
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
    /// otherwise, <c>PhysicsEngine.cs:129-134</c>) - and pushes the pinned Z through the existing pose path
    /// (<see cref="PushLogicalPositionToRoot"/>) so THIS SAME frame's rendered/logical position is already
    /// correct - no "first frame dip" (see this class' own E4.f doc, "anti-creux" ordering:
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> already ran ONE such evaluation
    /// immediately at spawn, before this entity's own first <see cref="Update"/> or the engine's first
    /// <c>CharacterMotionSystem.UpdateControllers</c> pass ever runs). When no support is found, drives
    /// this tick's own vertical displacement through the controller instead (bug fix - see
    /// <see cref="MoveVerticalAndPullPosition"/>'s own doc for the exact call and rationale) - a no-op for a controller-less
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
    /// <see cref="MoveVerticalAndPullPosition"/> displacement, just never entity-vs-entity clamped.
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
    /// call the intro harness makes right at spawn; since E19.h1b1 (D-E19-59) the production spawn sites make none any more,
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
        // See SupportTickCount's own doc (ONE-CLOCK invariant instrumentation) - counts every call, not
        // just the "found" branch, matching the original's own CheckEntityCollisionDown being evaluated
        // once per fixed frame regardless of outcome.
        SupportTickCount++;

        // Captured BEFORE this call can overwrite WasEntitySupportedLastTick below - see
        // MoveVerticalAndPullPosition's own call site (the "not found" tail's falling/rising branch) for
        // why: that call's own root-Z pull must stay gated on whether THIS ENTITY was supported ENTERING
        // this tick (the same "one-frame-late" contract MoveControllerAndPullPosition's own guard already
        // honours, reached earlier in the same tick, before this method ever runs), not on
        // WasEntitySupportedLastTick's own post-this-call value (already flipped false by the time that
        // branch runs, for every tick support just ended on - which would otherwise erode PosZ the SAME
        // tick support ends, one tick earlier than WasEntitySupportedLastTick's own documented contract).
        var wasSupportedEnteringThisTick = WasEntitySupportedLastTick;

        // Bug fix (gull entity 6, map 389 - see ApplyGravitySettingsToController's own doc for the full
        // measured numbers): EntitySupport.IsEligibleSubject only gates the ENTITY-VS-ENTITY search below
        // (CheckEntityCollisionDown's own eligibility gate, PhysicsEngine.cs:189/994) - the original's own
        // ComputeZPosition (PhysicsEngine.cs:375-421) decays/applies ForceZ to PosZ UNCONDITIONALLY, for
        // every active entity regardless of Collidable/eligibility (MoveEntity has no such gate at all).
        // So eligibility must NOT skip the decay/velocity work below it only skips the TryFindSupport call
        // a few lines down - both an eligible and an ineligible entity fall through to the exact same
        // "found"/"not found" tail (see that variable's own use).
        var eligible = EntitySupport.IsEligibleSubject(this);

        // E19.h1b2 (H1B2-R4, docs/plan-e19-opcodes.md §1.2n.1c): an entity without a controller, ticked in a world that has a collision field, steps its own Z like
        // every entity of the binary (which has no notion of a controller): decay, then the step below instead of the support block of the controller. The criterion is
        // the data dependency of the step itself (a field to read the terrain from) and it is what keeps out the entities of the intro harness, which are bare, never in
        // a world, and run their own vertical pass: it guards EVERY write below (decay, contact, IsOnGround, TileZ, PosZ). A destroyed entity is skipped, as in the
        // motion tick (D-E19-29).
        var stepsWithoutController = Controller == null && !immediateAtSpawn && Status.IsActive() && Owner?.World?.CollisionField != null;

        // Single decay site (verifier A1/A6 follow-up, reconciled with the timing bug fix above): a
        // controller-driven entity's own ForceZ is never decayed by AlundraScriptedMotion.RunOneKinematicTick
        // (horizontal-only, shared with the player) - this is the ONE place it decays, port of
        // UpdateEntityPhysics's own IsZForceApplied == 0 branch (PhysicsEngine.cs:1460-1476), same shape as
        // the harness's own RunVerticalPhysicsPass. Gated on ticksThisFrame by this method's only caller
        // (Update, see its own doc) so this runs exactly once per 50 Hz logic tick, never once per rendered
        // frame - the actual root cause of the measured bug was NOT this decay (already tick-quantized
        // before this fix) but the fact that nothing routed the decayed value through the controller,
        // leaving the engine's own continuous Settings.Gravity (render-rate) to integrate the vertical
        // instead - see the MoveVerticalAndPullPosition call in the "not found" tail below, and
        // ApplyGravitySettingsToController's own doc for why that engine path is now permanently disabled
        // for every controller-driven NPC. Skipped at the immediate spawn-time evaluation (FinalForceZ has
        // no real per-tick meaning yet there either).
        //
        // E19.d2c1 R3 (the binary, 0x80036AB8-0x80036B60): an impulse taken this tick (IsZForceApplied, R1) sets the force with no decay: IZF << 8, or 0 for the
        // stop marker of the iron grids (the 16 low bits of IZF equal 0x8000) on an entity without gravity (148 records of 58 maps). Without an impulse, an
        // entity with gravity decays its force and the result is bounded on BOTH sides to +-(ZViscosity << 8), as the binary does (the DLL bounded one side).
        //
        // E19.h1b2 (H1B2-R4): the same impulse and decay for an entity without a controller that steps its own Z (stepsWithoutController). The impulse is shared as in
        // the binary (inert in production: none of the 6 prefabs without a controller has an animation with one).
        if ((Controller != null || stepsWithoutController) && !immediateAtSpawn)
        {
            var gravity = (Flags & EntityFlags.Gravity) != 0;
            if (IsZForceApplied != 0)
            {
                var impulse = (IsZForceApplied & 0xFFFF) == 0x8000 && !gravity ? 0 : IsZForceApplied << 8;
                ForceZ = impulse;
                FinalForceZ = impulse;
            }
            else if (gravity)
            {
                var force = ForceZ - (MapGravityRaw << 8);
                var terminal = MapZViscosityRaw << 8;
                if (force > terminal)
                {
                    force = terminal;
                }
                else if (force < -terminal)
                {
                    force = -terminal;
                }

                ForceZ = force;
                FinalForceZ = force;
            }
        }

        // E19.d2c2 F2b: the force of this tick as the binary's passes read it, before the landing below resets it (see TickForceZ). The hero carried by this entity copies it.
        if (!immediateAtSpawn)
        {
            TickForceZ = FinalForceZ;
        }

        // Root-cause redo (measured on the real gull, entity 6, map 389, dt~1/123): driving this tick's own
        // vertical through Controller.SetVerticalVelocity (as this method's "not found" tail used to, before
        // E4.g replaced it with SetExternalVerticalDisplacement at this method's own tail - see that call's
        // own doc) let the ENGINE integrate it over REAL elapsed time every rendered frame - during a real asset-streaming hitch
        // (dt up to ~0.3s) the shared logic clock legitimately capped the HORIZONTAL step at
        // AlundraScriptedMotion.MaxTicksPerFrame (4 ticks) while the engine kept integrating the vertical
        // velocity for the entire 0.3s (~45px, ~15 ticks' worth) - the two axes counted in different units.
        // Landed 606.75 instead of the faithful 594.75 (12px = 8 ticks short of the 209.25px drift); the
        // very first trace sample already showed the vertical 9 ticks ahead of the horizontal (55.94px =
        // 18.6 ticks' worth of climb after only 13 elapsed logic ticks), and 9 ticks * 1.5px/tick == the
        // 12px error. THE fix (see <see cref="MoveVerticalAndPullPosition"/>'s own doc for the
        // CharacterControllerComponent.Move/UpdateGround investigation this required): drive this tick's
        // own vertical through the controller's Move() as a pure DISPLACEMENT - Move() has no elapsedTime
        // parameter, so it cannot integrate over real time by construction. Investigated (read-only):
        // Move()'s own displacement does NOT resolve this game's height-field terrain (only
        // MoveVerticalAndPullPosition's own general 3D sweep against rigid colliders, of which the terrain
        // height-field is not one) - CharacterMotionSystem's own field-based ground snap (UpdateGround)
        // only widens its probe by the SAME FRAME's own velocity-driven displacement, which no longer
        // exists once Velocity is kept at 0 for a controller-driven NPC (see <see cref="ApplyGravitySettingsToController"/>'s
        // sibling doc). So a controller-driven NPC now needs its OWN terrain probe again for the terrain
        // half of landing - <see cref="ComputeTerrainHeight"/>, the exact same 4-corner-max convention
        // <c>IntroTraceHarnessTests.RunVerticalPhysicsPass</c>'s own <c>ComputeTerrainHeight</c> already
        // ports faithfully for every controller-less entity - reused here (shape (b) of the two
        // a750256's own doc originally weighed, declined there only because the engine's own velocity-
        // driven ground snap covered it at the time; that coupling is exactly what this fix removes).
        // E16.a (D-E16-29/D-E16-30, docs/plan-e16-etat-partie.md, T3.1 "Résultats"): the original writes
        // TerrainHeight (+0x138) for EVERY entity, every tick, at that tick's own final position
        // (MoveEntity -> ComputeZPosition/ComputeXYPosition -> ComputeEntityGroundHeight,
        // PhysicsEngine.cs:180-187/0x80037E34), and again at spawn (InitializeEntity,
        // 0x80039D04/EntityManager.cs:127-128) BEFORE any script runs - regardless of Controller or
        // immediateAtSpawn. Written UNCONDITIONALLY here - this call's own single write site for every
        // entity that reaches it (a controller-driven NPC's per-tick call, a controller-less sprite's
        // per-tick call, and the one-shot immediateAtSpawn evaluation at spawn) - deliberately lifting the
        // FORMER `Controller != null && !immediateAtSpawn` gate for THIS WRITE ONLY. `terrainHeight` below
        // (the LOCAL feeding the controller-driven landing clamp a few lines down) keeps that exact same
        // gate: it drives different, unrelated physics (the entity-support seed/landing test), out of this
        // task's scope (contract item 3: "rien d'autre ne change dans la physique").
        TerrainHeight = ComputeTerrainHeight();

        if (stepsWithoutController)
        {
            StepVerticalWithoutController(collidables, eligible);
            return;
        }

        var terrainHeight = 0;
        if (Controller != null && !immediateAtSpawn)
        {
            terrainHeight = TerrainHeight;
        }

        // Verifier A1 (PhysicsEngine.cs:180-187): the FULL original conjunct - this tick's own natural
        // step, clamped UP to terrainHeight + 1 when it would go at-or-below terrain (Math.Max, same shape
        // <c>IntroTraceHarnessTests.RunVerticalPhysicsPass</c> already uses) - so a falling entity only
        // snaps onto an entity-support candidate its OWN downward reach this tick actually gets to, not any
        // overlapping candidate however far below. Only applied for a controller-driven, non-spawn
        // evaluation (terrainHeight is otherwise meaningless 0, and immediateAtSpawn keeps its own
        // int.MinValue reach-gate override - see this method's own doc on that one-shot spawn fixup).
        var platformTopZSeed = immediateAtSpawn
            ? int.MinValue
            : Controller != null
                ? Math.Max((PosZ + ModZ) + FinalForceZ, terrainHeight + 1)
                : (PosZ + ModZ) + FinalForceZ;

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
                PushLogicalPositionToRoot();
                IsOnGround = 1;
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

                if (!immediateAtSpawn)
                {
                    // Belt-and-suspenders reset (a750256's own original terrain-landing fix, kept alongside
                    // the strict terrain probe below): the engine's own Controller.IsGrounded already
                    // carries a StepHeight/GroundSnapDistance-wide tolerance a STRICT terrain-height
                    // comparator does not (see ComputeTerrainHeight's own doc) - on a real staircase, an NPC
                    // climbing one step at a time can sit genuinely grounded (IsGrounded true, mid-step,
                    // engine-tolerant) for a tick or two where its own PosZ has not yet crossed the strict
                    // "moddedPosZ + FinalForceZ &lt;= landingTop - 1" test below (sailor 12, map 389's own
                    // last staircase - the exact regression a750256 fixed). Gated on `ForceZ &lt; 0` (never
                    // wipes a same-tick rising 0x1B impulse), matching that commit's own faithfulness note.
                    //
                    // E19.d2c1 R5 a: the binary's strict landing test reads the force OF THE TICK, so it is taken here, before this reset
                    // wipes it (a resting NPC with gravity has -32768 here every tick, which is what makes CollidedWithEntityZ 1 at rest).
                    var tickForceZ = FinalForceZ;
                    if (Controller.IsGrounded && (Flags & EntityFlags.Gravity) != 0 && ForceZ < 0)
                    {
                        ForceZ = 0;
                        FinalForceZ = 0;
                    }

                    // Port of PhysicsEngine.cs:123-135's own `if (platformEntity == null || ...)` branch -
                    // entity support already came up empty above, so this tick's own landing test is
                    // TERRAIN-only (landingTop == terrainHeight + 1, never higher: an entity candidate
                    // taller than terrain would already have satisfied the "found" branch above via the
                    // SAME terrain-clamped platformTopZSeed). `FinalForceZ < 1` mirrors the original's own
                    // `finalZVelocity < 1` (descending-or-stationary, PhysicsEngine.cs:118) - never fires
                    // while rising under a scripted upward 0x1B impulse (FinalForceZ > 0 the instant that
                    // impulse lands).
                    var moddedPosZ = PosZ + ModZ;
                    var landingTop = terrainHeight + 1;

                    if (FinalForceZ < 1 && moddedPosZ + FinalForceZ <= landingTop - 1)
                    {
                        // Terrain landing this tick - same reset PhysicsEngine.cs:129-134 applies for
                        // either landing kind (see EntitySupport's own "found" branch above for the entity
                        // half). A GENUINE transition (previous tick was still airborne, or ForceZ still
                        // carried real motion) routes through the SAME landed-pose path
                        // (PushLogicalPositionToRoot: root write + ClampToGround + Teleport) the entity-
                        // support "found" branch already uses; an ALREADY-resting tick (this entity landed
                        // on a PRIOR tick and nothing has moved it since) skips that same call.
                        //
                        // Bug fix (measured on the real bank-146 spawn's own horizontal-kinematics test):
                        // AlundraWorldProxy.ResolveLogicalPosition (PushLogicalPositionToRoot's own root
                        // write) TRUNCATES PosX/PosY/PosZ to whole pixels (`pos &gt;&gt; 16`) - the entity-
                        // support "found" branch already accepts that cost every tick because a
                        // SUPPORT-pinned entity's PosZ genuinely needs re-deriving from a live candidate top
                        // every tick (see WasEntitySupportedLastTick's own doc). A terrain-resting entity's
                        // PosZ does NOT change tick to tick once landed - calling the same truncating write
                        // unconditionally here (this branch is reached EVERY tick a resting NPC is
                        // evaluated, not just once on impact) silently truncated this same NPC's own
                        // fractional horizontal walk progress every tick, measured to erode ~29px off a
                        // 500px, 70-tick real walk. Uses <c>terrainHeight</c> directly for the LANDED value
                        // (not the "+1"-shifted <c>landingTop</c> the harness's own controller-less port
                        // uses for its own PosZ assignment too) - that extra 16.16 unit (1/65536 px,
                        // imperceptible - the same order of magnitude already accepted by this class' own
                        // root-pull rounding, see Update's own "double-rounding, not a float cast" doc) is
                        // silently swallowed the moment root.Z round-trips through
                        // ResolveLogicalPosition's own integer truncation (`posZ &gt;&gt; 16`, no
                        // remainder) - re-adding it every tick without ever being able to keep it made
                        // `wasAlreadyLanded` permanently false (PosZ could never equal `landingTop - ModZ`
                        // after its own first round-trip), silently re-triggering the SAME truncating X/Y
                        // write above every single tick regardless of the skip.
                        var targetPosZ = terrainHeight - ModZ;
                        //
                        // E19.d D2 (docs/plan-e19-opcodes.md §1.2g): "already landed" depends on the position ALONE. An entity WITHOUT
                        // gravity that rests here keeps its negative ForceZ (the binary's ComputeZPosition, 0x800375E0, zeroes it only
                        // with gravity), and the former `&& ForceZ == 0` made it re-push its position every tick, the truncating write
                        // above: a walk of less than one pixel per tick never advanced (the camera block of map 391, 0.5 px per tick).
                        var wasAlreadyLanded = PosZ == targetPosZ;

                        PosZ = targetPosZ;
                        // E19.d2c1 R5 a (the binary, 0x80036C20-0x80036C34 and 0x800376E0): CollidedWithEntityZ is raised only when the STRICT
                        // test holds on the force of the tick, moddedPosZ + F < terrainHeight; this branch keeps the DLL's own `<=` landing
                        // (D-E19-40), so an exact touch (sum == 0) lands here without raising it. It is cleared at the head of every motion tick.
                        if (moddedPosZ + tickForceZ < terrainHeight)
                        {
                            CollidedWithEntityZ = 1;
                        }

                        if ((Flags & EntityFlags.Gravity) != 0)
                        {
                            ForceZ = 0;
                            FinalForceZ = 0;
                        }

                        TileZ = PosZ >> 20;
                        if (!wasAlreadyLanded)
                        {
                            PushLogicalPositionToRoot();
                        }

                        IsOnGround = 1;
                    }
                    else
                    {
                        // Still falling/rising this tick - drive it through the controller's own Move() as
                        // a pure per-tick displacement (see this method's own doc above and
                        // MoveVerticalAndPullPosition's own doc for the full investigation/measured
                        // numbers) - the engine contributes ZERO REAL vertical motion of its own between
                        // ticks; only this per-tick Move() ever moves this entity's Z. The end-of-method
                        // SetExternalVerticalDisplacement call below is what now keeps
                        // CharacterControllerComponent.UpdateGround from re-pinning a climbing NPC to
                        // ground every render frame - see this method's own tail doc (E4.g superseded the
                        // former RisingVelocitySignal workaround this comment used to describe).
                        MoveVerticalAndPullPosition(FinalForceZ / 65536f, wasSupportedEnteringThisTick);
                        IsOnGround = 0;
                    }
                }
            }
        }

        // E4.g (docs/plan-e4-deplacement-scripte.md): exactly ONE per-tick declaration of this tick's
        // RESOLVED vertical displacement, unconditional on which branch above ran (found/not-found,
        // landed/still-moving, even immediateAtSpawn) - the single site this invariant needs, since
        // nothing above ever returns out of this method early. Replaces the former
        // Controller.SetVerticalVelocity(0f | RisingVelocitySignal) calls the three branches above used
        // to make individually: CharacterControllerComponent.UpdateGround now reads this LATCHED
        // declaration directly (a positive value means airborne), so a genuinely zero FinalForceZ (a
        // landing/support reset, or an already-resting tick) correctly re-arms ground detection, while a
        // positive one (a still-rising tick) correctly keeps it suppressed - without the former signal's
        // own float-precision workaround. This call also runs AFTER every PushLogicalPositionToRoot
        // above (whose own Controller.Teleport -&gt; Stop call resets the latch to 0), so the latch is
        // always re-established fresh by this same tick's own resolved value, never left stale from a
        // Stop() a few lines earlier in this same call. A scripted position write made OUTSIDE a tick
        // (0x64/0x65/0x8B) leaves the latch at 0 until this entity's next tick - correct, since a
        // teleport-to-ground is never a rising displacement.
        if (Controller != null)
        {
            Controller.SetExternalVerticalDisplacement(FinalForceZ / 65536f);
        }
    }

    /// <summary>
    /// E19.h1b2 (H1B2-R4, docs/plan-e19-opcodes.md §1.2n.1c): the vertical step of an entity without a controller, the rules of the binary (<c>ComputeZPosition
    /// 0x800375E0</c>, the end pass <c>0x80038064</c>) in the DLL's convention (the binary's <c>PosZ - 1</c>: <c>docs/plan-e19-h1b2-annexe/binary-notes.md</c> §2.3 to §2.7),
    /// run after the decay of <see cref="EvaluateEntitySupport"/> and after this tick's script and XY step, from <see cref="TerrainHeight"/> (the probe of the box at
    /// the position of the tick). <see cref="FinalForceZ"/> is the force of the tick. A rise (<c>F &gt; 0</c>) stops under the absolute ceiling of 1920 px (contact, and
    /// <see cref="ForceZ"/> to 0 with the gravity), else adds <c>F</c>. A fall or rest (<c>F &lt;= 0</c>, zero included) lands on the highest entity top at or above the
    /// reach of the tick (<see cref="EntitySupport.TryFindSupport"/>, a subject of the search only when eligible), else on the terrain when
    /// <c>PosZ + ModZ + F &lt; T</c> (STRICT: reaching <c>T</c> exactly is not a landing, the contact comes one tick later), else adds <c>F</c>; a landing raises
    /// <see cref="CollidedWithEntityZ"/> and zeroes <see cref="ForceZ"/> with the gravity only (<see cref="FinalForceZ"/> keeps the force of the tick: at rest with the
    /// gravity it is -G &lt;&lt; 8 and the contact is raised at every tick). <see cref="IsOnGround"/> is positional, <c>PosZ &lt;= floor</c>, the floor being <c>T</c> or the top
    /// of the highest entity below plus one. Not ported (no case in the corpus): the ceilings that other entities form during a rise, and the binary's order Z before XY.
    /// </summary>
    private void StepVerticalWithoutController(IReadOnlyList<AlundraEntityScriptProxy> collidables, bool eligible)
    {
        var gravity = (Flags & EntityFlags.Gravity) != 0;
        var terrain = TerrainHeight;
        var force = ForceZ;
        FinalForceZ = force;

        var supportedByEntity = false;
        if (force > 0)
        {
            if (PosZ + ModZ + Depth + 1 + force > EntitySupport.AbsoluteCeiling)
            {
                PosZ = EntitySupport.AbsoluteCeiling - 2 - ModZ - Depth;
                CollidedWithEntityZ = 1;
                if (gravity)
                {
                    ForceZ = 0;
                }
            }
            else
            {
                PosZ += force;
            }
        }
        else
        {
            var moddedPosZ = PosZ + ModZ;
            var supportTopZ = 0;
            var landsOnEntity = eligible
                && EntitySupport.TryFindSupport(this, collidables, Math.Max(moddedPosZ + force, terrain), out _, out supportTopZ);
            if (landsOnEntity || moddedPosZ + force < terrain)
            {
                PosZ = (landsOnEntity ? supportTopZ : terrain) - ModZ;
                CollidedWithEntityZ = 1;
                supportedByEntity = landsOnEntity;
                if (gravity)
                {
                    ForceZ = 0;
                }
            }
            else
            {
                PosZ += force;
            }
        }

        WasEntitySupportedLastTick = supportedByEntity;
        TileZ = PosZ >> 20;

        // The end pass of the binary: the floor of the box after the step, the terrain raised to the top + 1 of the highest entity below (a subject only).
        var floor = terrain;
        if (eligible && EntitySupport.TryFindSupport(this, collidables, terrain, out _, out var floorTopZ) && floorTopZ > floor)
        {
            floor = floorTopZ;
        }

        IsOnGround = PosZ <= floor ? 1 : 0;
    }

    /// <summary>
    /// Engine-only, not part of the original struct (same as <see cref="LogicContextEntity"/>):
    /// this entity's IDSV table, resolved once at spawn from its <c>SpriteRecordCatalog</c> entry
    /// (see <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>) and keyed by
    /// <c>(int)CurrentAnimationId * 4 + AnimationDirection</c> so the per-frame wall-interleave sort
    /// pass (<see cref="AlundraFrameSyncPasses.RunWallInterleaveSortKeyPass"/>) never re-looks-up the
    /// catalog's own (larger, Guid-keyed) dictionary. Null when the entity's header carried no
    /// IdsvAnimDirs entries (degraded catalog, or a bank whose header simply has none); callers treat
    /// that the same as "0 bias for every (anim, direction)".
    /// </summary>
    public Dictionary<int, int>? IdsvByAnimDirection;

    /// <summary>
    /// Engine-only, not part of the original struct (same shape/purpose as
    /// <see cref="IdsvByAnimDirection"/>, same key packing): this entity's Hold/Chain end-of-animation
    /// table, resolved once at spawn from its <c>SpriteRecordCatalog</c> entry (see
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>/<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>)
    /// and read by <see cref="AlundraEntitySpawnFactory.OnAnimationFinished"/> - the bridge from the engine's
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
    /// per-frame animation/transform sync every frame (<see cref="AlundraFrameSyncPasses.SyncAnimation"/> /
    /// <see cref="AlundraFrameSyncPasses.SyncTransform"/>, moved here from the world's own per-frame passes -
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

        // T2 (docs/plan-transitions-carte.md §1.5/§3, D-T-6): port of EntityManager.cs:377's
        // "g_playerControlFlags & GameplayBlockedMask" gate over UpdateEntities. The exhaustive table in
        // §1.5 places every pass THIS method drives on one side or the other of that same gate: the pose
        // repatriation from the root + IsOnGround below (physics, EntityManager.cs:385), the whole NPC
        // pick/run/motion/support branch and the whole player MovePlayer/Tick/slope/floor-height branch
        // (events then physics, EntityManager.cs:380/385), and SyncAnimation (the target-resolution half
        // of UpdateAnimation, dispatched INSIDE the same `if`, EntityManager.cs:384) are all "dedans".
        // SyncTransform stays OUTSIDE (a pure render-position publish, the original's own
        // UpdateVisibleEntitiesZSort/sprite-publish half that runs after the `else`,
        // EntityManager.cs:394-408) - so it is the one call left unconditional below, after this block.
        // T4 (docs/plan-transitions-carte.md §3, D-T-6): OR'd with AlundraWarpDirector's own gel gate -
        // same "third mechanism" note as AlundraWorldProxy.Update's own T4 comment on its sibling
        // gameplayBlocked local.
        var gameplayBlocked = (ScriptHost.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0
            || AlundraWarpDirector.Instance.IsTransitionInProgress;

        // T2 REGRESSION FIX (softlock reported in play): the pad snapshot is "dehors", NOT "dedans".
        // It is the port of the original's g_padState1, which PadManager.UpdatePads refreshes from the
        // MAIN LOOP (GameEngine.cs:1518) and even from inside the warp transition loop
        // (GameEngine.cs:280) - never from UpdateEntities, so the original never freezes it, not even
        // while its own gameplay gate is posed. Publishing it from inside the gated block below froze
        // the very input the dialogue box's own advance/close pass consumes
        // (AlundraDialogueDirector.Tick reads LastPadState.ButtonsJustPressed, :239): with the snapshot
        // stuck on a frame that carried the interact bit the box flushed every page at once, and with
        // one that did not it could never be advanced at all - leaving MenuOpen posted forever, so
        // NPCs stayed frozen and the player stayed uncontrollable. §1.5's own table row is corrected
        // accordingly.
        if (IsPlayer)
        {
            var padSource = ScriptHost.PlayerController;
            if (padSource != null)
            {
                ScriptHost.GameState.LastPadState = padSource.BuildPadState();
            }
        }

        if (!gameplayBlocked)
        {
            RunGameplayBlockableUpdate(elapsedTime);
        }

        AlundraFrameSyncPasses.SyncTransform(Owner);
    }

    /// <summary>Everything <see cref="Update"/> skips while <see cref="AlundraGameState.PlayerControlBits.GameplayBlockedMask"/>
    /// is posed - see that method's own T2 doc for the exhaustive per-pass placement this reproduces.</summary>
    private void RunGameplayBlockableUpdate(float elapsedTime)
    {
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
        // as Gravity 0 + a zero SetExternalVerticalDisplacement declaration and pushed the authoritative logical PosZ through
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

            // E19.d2c1 R6: in the air state the logic tick owns PosZ and IsOnGround (the engine only follows the root).
            if (!WasEntitySupportedLastTick && !HeroAirborne)
            {
                PosZ = (int)Math.Round((double)root.Z * 65536.0);
            }

            if (!HeroAirborne)
            {
                IsOnGround = Controller.IsGrounded ? 1 : 0;
            }

            // E19.d2c2 S2: this pull is a reliable ground reading only when the vertical is not held elsewhere (a ladder, a departure, a freeze all declare it external).
            HeadPullGroundTrusted = !HeroAirborne && !Controller.IsVerticalOwnedExternally;

            // E19.d2c2 F3: the engine does not see the boxes of entities, so a hero passing from the ground onto the top of an object of the same height lying in a hollow
            // reads "not on the ground" here although the binary's FloorHeight (0x80037F28) holds it. The top of an entity exactly at the foot is a ground: IsOnGround 1, and
            // the first tick enters the state at rest on it.
            HeadPullFlushOnEntity = false;
            if (IsPlayer && HeadPullGroundTrusted && IsOnGround == 0 && (Flags & EntityFlags.Gravity) != 0 && IsEntityTopFlushWithFoot())
            {
                IsOnGround = 1;
                HeadPullFlushOnEntity = true;
            }
        }

        if (!IsPlayer)
        {
            // ONE-CLOCK fix (user-reported stall, sailor entity 12 of map 389 stuck on opcode 0x1F at pc
            // 1470 - see AlundraScriptedMotion's own class doc for the full diagnosis, and
            // AlundraLogicClock's own class doc for the original pacing bug this clock first fixed): the
            // whole per-entity logic tick - script pick/run, THEN kinematic motion, THEN Z support - now
            // runs as ONE fused loop over ticksThisFrame, exactly the original's own per-fixed-frame order
            // (EntityManager.UpdateEntitiesEvents THEN UpdateEntitiesPhysics, EntityManager.cs:367-395).
            // ticksThisFrame is usually 1 (a display frame roughly matches 50 Hz) but can be 0 (a fast
            // render frame, nothing to tick) or up to AlundraScriptedMotion.MaxTicksPerFrame under catch-up
            // (a stalled/slow frame) - see AlundraLogicClock's own class doc.
            //
            // Motion and Z support used to run OUTSIDE this loop, gated by their own separate mechanisms
            // (motion by its own per-entity PhysicsTickAccumulator fed raw elapsedTime every RENDERED
            // frame; support already correctly gated on ticksThisFrame, but as its OWN separate loop) - two
            // accumulators stepping at the same nominal 50 Hz rate but never agreeing on WHICH rendered
            // frame carries a tick. AlundraEntityScriptProxy.ForceAdjusted (cleared then possibly re-set
            // inside the motion tick) was therefore usually cleared on a frame that carried no motion
            // sub-step and set only on a frame that did - a 0x1F (Walk with collision)'s own script-side
            // read of ForceAdjusted this same fused loop almost always saw a stale 0 instead of the
            // previous tick's real outcome, so its "movement was curtailed" exit never fired and the walk
            // never ended. Fusing all three into one loop over the SAME ticksThisFrame count removes the
            // second clock entirely: one logic tick is always exactly one script pass, one motion sub-step,
            // and one support step, in that order, every time.
            var ticksThisFrame = ScriptHost.LogicTicksThisFrame(elapsedTime);
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                PickEventTrigger();
                RunPickedEvent(ScriptHost.Runner);

                // E19.c2 (docs/plan-e19-opcodes.md §1.2f, ADR-0019): the logical clock of the animation ends steps once per
                // tick, right after this entity's script and before its motion - the binary's order (events, then
                // UpdateAnimation, then physics, 0x8003B388). A switch the script just asked for reserves the tick (the old
                // animation neither advances nor ends on it); otherwise the animation advances one tick and may end, which
                // 0x1C/0x1D then see at the next tick, as in the binary. The sprites themselves keep drawing in real time.
                AlundraFrameSyncPasses.StepAnimationClock(this);

                // E4.b (docs/plan-e4-deplacement-scripte.md): scripted mover for every controller-driven
                // NPC - port of PhysicsEngine.UpdateEntityPhysics (:1579-1598) restricted to the
                // flat-ground half already ported for the hero (AlundraPlayerManager/AlundraScriptedMotion's
                // own class docs). Runs AFTER this tick's own pick/run above so a Load/Tick program that
                // just set TargetDirection/TargetAnimationId THIS tick (0x09/0x1A, or 0x5B/0x5A once E4.c
                // lands) is already visible to this tick's own motion, matching the original's own
                // MovePlayer-then-physics order.
                //
                // Pre-read finding (E4.b item 1a, docs/plan-e4-deplacement-scripte.md): the original's
                // UpdateEntityPhysics reads entity.AnimationSet, not TargetAnimationId directly - that
                // field is only reassigned by EntityManager.UpdateAnimation (EntityManager.cs:203-248) at
                // the exact moment the animation actually SWITCHES (CurrentAnimationId != TargetAnimationId),
                // i.e. it tracks the CURRENTLY PLAYING animation, not the just-written target. This proxy's
                // own equivalent of that reassignment site is AlundraWorldProxy.SyncAnimation - it likewise
                // only updates CurrentAnimationId when TryResolveAnimationTarget reports a change - so
                // AlundraScriptedMotion.TickScriptedNpc below is keyed off CurrentAnimationId, not
                // TargetAnimationId (unlike the hero's own AlundraPlayerManager.Tick, out of E4.b's scope to
                // change). Documented accepted deviation: SyncAnimation runs at the END of this same Update
                // call (below), so this tick sees CurrentAnimationId as of the END of the PREVIOUS frame -
                // one frame of latency behind a same-frame TargetAnimationId write, exactly the same shape
                // as this class' own documented one-frame World/entity latency (see this method's own doc,
                // "Accepted deviation" paragraph) - "à défaut d'équivalent exact, utiliser l'anim courante
                // synchronisée et documenter l'écart" per the plan. E19.c2: this lag (kept, D-E19-13) concerns the
                // MOTION only; the ends of the animations that 0x1C/0x1D see come from the logical clock stepped
                // just above, exact at the tick (a switch owes its tick, see AlundraFrameSyncPasses.SyncAnimation).
                //
                // E4.e (docs/plan-e4-deplacement-scripte.md): unconditional, not gated on Controller != null
                // - the original applies UpdateEntityPhysics to every entity in g_activeEntities regardless
                // of whether it carries a body/controller (PhysicsEngine.cs:12-14's own loop has no such
                // gate); RunOneKinematicTick's own Controller-null branch (AlundraScriptedMotion, "PosX +=
                // FinalForceX" else-arm) already existed for exactly this case - it is what the pre-E3 hero
                // used - and is production-safe: every body-carrying entity keeps a real
                // CharacterControllerComponent (E4.a), so this fallback only ever executes for the
                // harness's own bare proxies (no Owner/World at all) or a genuinely controller-less
                // sprite-only prefab (11 on map 389, whose Speed is 0 for every AnimSet they carry - this
                // is a no-op integration for them in practice).
                //
                // E19.d2b (D-E19-29): only an entity that is Normal or Deactivated moves (the binary's physics list takes the
                // statuses 2 and 3): one flagged for destruction stays where it was while it waits to be recycled.
                if (Status.IsActive())
                {
                    AlundraScriptedMotion.TickScriptedNpc(this);
                }

                // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): entity-vs-entity Z support
                // clamp - AFTER this tick's own motion above, so a walk that just moved this entity out of
                // a platform's XY footprint loses support the SAME tick (matching the original's own "hors
                // de l'empreinte: plus de support" behaviour), not one tick late. See
                // EvaluateEntitySupport's own doc. EvaluateEntitySupport's own ForceZ gravity-decay branch
                // is a per-TICK quantity (ForceZ -= Gravity&lt;&lt;8, PhysicsEngine.cs:1460-1476, ported
                // verbatim inside that method) - the original evaluates CheckEntityCollisionDown once per
                // its own fixed frame too (i.e. once per logic tick here).
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
            // controller at all, by design). MovePlayer itself deliberately stays per RENDERED frame (input
            // sampling, unchanged by the ONE-CLOCK fix - see AlundraLogicClock's own class doc on what
            // stays per-frame vs per-tick); only the kinematic integration below is now driven by the SAME
            // ticksThisFrame the NPC branch above uses, instead of its own separately-accumulated elapsed
            // time (ONE-CLOCK fix, AlundraScriptedMotion's own class doc) - the hero's own observable
            // per-tick behaviour is unchanged, only the source of the tick count.
            var playerController = ScriptHost.PlayerController;
            var heroTicksThisFrame = ScriptHost.LogicTicksThisFrame(elapsedTime);
            if (playerController != null)
            {
                // D-E7-8 (docs/plan-e7-mutation-tuiles.md, slice E7.c): this frame's pad snapshot is
                // already published, unconditionally, at the head of Update (see the T2 regression-fix
                // comment there for why it may not live inside this gated block) - so MovePlayer here
                // consumes exactly the value event opcode 0x2F and the dialogue director already saw
                // this frame, which is the single-global behaviour the original has.
                var pad = ScriptHost.GameState.LastPadState;
                AlundraPlayerManager.MovePlayer(this, in pad, ScriptHost.GameState, ScriptHost);
            }

            // E19.c2 (docs/plan-e19-opcodes.md §1.2f): the hero's animation clock steps at every tick, whether or not a
            // controller possesses the pawn (the binary animates the player whatever its input; production always has a
            // controller), interleaved with the kinematic tick: a Chain that MovePlayer asked for reserves the tick before the
            // motion reads TargetAnimationId. Tick(this, 1) once per tick is Tick(this, n).
            for (var tick = 0; tick < heroTicksThisFrame; tick++)
            {
                AlundraFrameSyncPasses.StepAnimationClock(this);
                if (playerController != null)
                {
                    AlundraPlayerManager.Tick(this, 1);

                    // E19.d2c1 R7: the water, ice and x160 rules read VramOR at the next tick, so it is refreshed after every tick of the hero (the binary's
                    // UpdateTileAttributes ends each physics pass). The end-of-frame call below stays: a frame without tick, a portal read.
                    UpdateVramFlags();
                }
            }

            if (playerController != null)
            {

                // E1 (docs/plan-echelles-chiffrage.md É1): alimente Slope_18c AFTER this frame's own
                // MovePlayer+Tick, exactly like the original's UpdateTileAttributes runs at the end of
                // the physics pass (PhysicsEngine.cs:1706-1826) - so next frame's MovePlayer reads THIS
                // frame's freshly computed value, the same one-frame latency the original already has
                // (see UpdateGroundSlope's own doc, and docs/plan-echelles-chiffrage.md §2's "fait qui
                // simplifie la conception").
                UpdateGroundSlope();

                // T3 (D-T-10, docs/plan-transitions-carte.md §1.6/§3): alimente CombinedVramFlagsOR/AND,
                // same restriction, same gates and the exact same one-frame latency as UpdateGroundSlope
                // just above (both read this same frame's freshly-integrated position, and both are what
                // the ORIGINAL's own UpdateTileAttributes computes from the SAME corner loop at the end of
                // its physics pass, PhysicsEngine.cs:1740-1768) - so next frame's MovePlayer (via
                // AlundraPortalTrigger.TryGetTrigger) reads THIS frame's freshly computed value, exactly
                // like the original's own CheckAndExecuteWarp reads the previous frame's
                // CombinedVramFlagsAND (PlayerManager.cs:29 runs before UpdateTileAttributes has run again
                // this frame).
                UpdateVramFlags();

                // E2 (docs/plan-echelles-chiffrage.md E2): alimente FloorHeight, same restriction and same
                // one-frame latency as UpdateGroundSlope just above (see UpdateFloorHeight's own doc) - both
                // are pure post-tick probes over this same frame's freshly-integrated position, independent
                // of each other (the original's own UpdateTileAttributes computes them independently too,
                // hitz at PhysicsEngine.cs:1702-1703 before ever touching Slope_18c's own corners).
                UpdateFloorHeight();
            }
        }

        // T2 (docs/plan-transitions-carte.md §1.5): "dedans" - port of UpdateAnimation's target-
        // resolution half, dispatched INSIDE the original's own GameplayBlockedMask `if`
        // (EntityManager.cs:384). Runs at the end of THIS gameplay-blockable block, not from the
        // caller (Update) below - so it is skipped, along with everything else above, whenever
        // GameplayBlockedMask is posed (Update never calls this method at all in that case).
        AlundraFrameSyncPasses.SyncAnimation(Owner);
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

                                // E12.d (D-E12D-4): CONSUME-ON-PICK. The original clears the signal at
                                // the head of the NEXT tick's MovePlayer (PlayerManager.cs:23) - with
                                // frame==tick there, that IS "consumed by the one pick of the tick".
                                // Here MovePlayer runs once per RENDERED frame while this pick runs per
                                // logic tick (0..4 per frame): a head-clear would drop presses on
                                // zero-tick frames and let one assignment feed N picks on catch-up
                                // frames. Consuming at the exact pick that selects F is the derived
                                // equivalent: one assignment -> exactly one F pick, never zero.
                                host.ActiveCollisionEntity = null;
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
        AlundraTerrainProbe.SampleGroundCorner(field, minX, minY, ref hasGround, ref groundMax);
        AlundraTerrainProbe.SampleGroundCorner(field, minX, maxY, ref hasGround, ref groundMax);
        AlundraTerrainProbe.SampleGroundCorner(field, maxX, minY, ref hasGround, ref groundMax);
        AlundraTerrainProbe.SampleGroundCorner(field, maxX, maxY, ref hasGround, ref groundMax);

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

    /// <summary>
    /// Direct DLL-side terrain ground probe for a controller-driven NPC's own per-tick vertical landing
    /// test (<see cref="EvaluateEntitySupport"/>'s own "not found" tail) - port of
    /// <c>PhysicsEngine.cs:180-187</c>'s own <c>ComputeEntityGroundHeight</c>, the SAME 4-corner-max
    /// convention <c>IntroTraceHarnessTests.ComputeTerrainHeight</c> already ports faithfully for every
    /// controller-less entity's own vertical pass. Needed again for a controller-driven NPC because
    /// <see cref="MoveVerticalAndPullPosition"/>'s own <c>Controller.Move</c> call resolves general 3D
    /// collision geometry (walls/ceilings against <c>Owner.World.PhysicsWorld</c>) but NOT this game's own
    /// height-field terrain - see that method's own doc for the full investigation. Samples this entity's
    /// own struct footprint (<see cref="PosX"/>/<see cref="PosY"/>/<see cref="ModX"/>/<see cref="ModY"/>/
    /// <see cref="Width"/>/<see cref="Height"/> - the original's own box, populated at spawn by
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>), NOT <see cref="ClampToGround"/>'s own
    /// <see cref="CollisionComponent"/> fixture - matching <c>IntroTraceHarnessTests.ComputeTerrainHeight</c>'s
    /// own corners bit-for-bit. Returns a 16.16 fixed-point height, 0 when no corner finds ground (same
    /// "sea level" fallback <c>ComputeEntityGroundHeight</c>/the harness's own port already use) - a no-op
    /// return without an installed <c>World.CollisionField</c>.
    /// </summary>
    internal int ComputeTerrainHeight() => ComputeTerrainHeight(Owner?.World?.CollisionField);

    /// <summary>The same height against an explicit <paramref name="field"/>: E19.h1b1 uses it at spawn, when the entity has no world yet
    /// (<see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> runs before <c>World.AddEntity</c>). 0 without a field.</summary>
    internal int ComputeTerrainHeight(ICollisionField? field)
    {
        if (field == null)
        {
            return 0;
        }

        var x1 = (PosX + ModX) >> 16;
        var x2 = (PosX + ModX + Width) >> 16;
        var y1 = (PosY + ModY) >> 16;
        var y2 = (PosY + ModY + Height) >> 16;
        var highest = int.MinValue;
        AlundraTerrainProbe.SampleTerrainHeightCorner(field, x1, y1, ref highest);
        AlundraTerrainProbe.SampleTerrainHeightCorner(field, x2, y1, ref highest);
        AlundraTerrainProbe.SampleTerrainHeightCorner(field, x1, y2, ref highest);
        AlundraTerrainProbe.SampleTerrainHeightCorner(field, x2, y2, ref highest);
        return highest == int.MinValue ? 0 : highest;
    }

    /// <summary>
    /// E1 (docs/plan-echelles-chiffrage.md É1): port of the <c>Slope_18c</c> half of
    /// <c>PhysicsEngine.UpdateTileAttributes</c>'s gravity branch (PhysicsEngine.cs:1740-1820) - the
    /// four-corner qualification rule that feeds the ladder/water slope switch in
    /// <c>PlayerManager.MovePlayer</c> (that switch is NOT ported yet - see
    /// <see cref="AlundraPlayerManager"/>'s own class doc; this method only ALIMENTS the field, nothing
    /// reads it yet besides the pre-existing <c>DestroyOnSlidingSlope</c>/<c>Slope_18c == 4</c> check -
    /// see the restriction paragraph below). Samples the SAME four corners as
    /// <see cref="ComputeTerrainHeight"/> (identical <see cref="PosX"/>/<see cref="PosY"/>/
    /// <see cref="ModX"/>/<see cref="ModY"/>/<see cref="Width"/>/<see cref="Height"/> footprint,
    /// PhysicsEngine.cs:960-971/1187-1190) instead of a single center point - the divergence between the
    /// two is the whole point of this port (docs/plan-echelles-chiffrage.md §7.1: a hero pressed flush
    /// against a wall straddles a cell boundary where the corners disagree; a center-only sample would
    /// silently read the wrong cell and misreport the slope).
    ///
    /// Per corner: qualifies only when that corner's ground height (16.16, same rounding as
    /// <see cref="AlundraTerrainProbe.SampleTerrainHeightCorner"/>) is exactly equal to <c>PosZ + ModZ</c> - i.e. the entity
    /// is standing exactly on that corner's ground.
    ///
    /// DEVIATION FROM THE LITERAL ORIGINAL (deliberate, not a bug): the original compares
    /// <c>MapHeights[i] + 1 == ModdedPosZ</c> (PhysicsEngine.cs:1748) because ITS OWN resting invariant is
    /// <c>ModdedPosZ == TerrainHeight + 1</c> (<c>PhysicsEngine.cs:186</c>'s <c>platformTopZ = entity.TerrainHeight + 1</c>,
    /// landed via <c>PhysicsEngine.cs:128</c>'s <c>entity.PosZ = platformHeight - entity.ModZ</c>). This
    /// port never adopted that <c>+1</c>: its own resting invariant is <c>ModdedPosZ == TerrainHeight</c>,
    /// chosen and documented where the player lands
    /// (<see cref="Update"/>'s landed-pose branch, <c>targetPosZ = terrainHeight - ModZ</c>, see the long
    /// comment a few lines above that assignment for why the extra 16.16 unit the original keeps is
    /// silently swallowed the moment <c>root.Z</c> round-trips through
    /// <c>AlundraWorldProxy.ResolveLogicalPosition</c>'s own integer truncation, <c>posZ &gt;&gt; 16</c>,
    /// no remainder) and at spawn (<c>ClampToGround</c>: <c>PosZ = groundPosZ;</c>, no <c>+1</c> either).
    /// Porting the original's <c>+1</c> literally here would therefore make this qualification
    /// permanently unsatisfiable in THIS engine - a player standing on ground always has
    /// <c>ModdedPosZ == TerrainHeight</c> exactly, never <c>TerrainHeight + 1</c>, confirmed on all four
    /// golden hero traces on map 389 (<c>posZ == cellHeight * 16 &lt;&lt; 16</c> exactly, e.g. frame 1 of
    /// <c>docs/hero-trace-389-highground-fixedstep.txt</c>: <c>posZ = 8388608</c>, <c>cellHeight = 8</c>,
    /// <c>8 * 16 &lt;&lt; 16 = 8388608</c>; same for frames 40/210/258 across both golden traces). So this
    /// method ports the RULE's MEANING - "the entity is resting on this corner" - against THIS port's own
    /// resting invariant, not the original's literal comparison. <c>bestFlagMask</c> starts at the sentinel <c>0xe00</c> (the
    /// maximum <c>(GroundProperty &amp; 0x0e) &lt;&lt; 8</c> can ever reach) and is only lowered to a
    /// qualifying corner's own masked value when that value is smaller (PhysicsEngine.cs:1751-1761); ANY
    /// disqualified corner resets it to 0 (PhysicsEngine.cs:1763) and it can never recover afterwards (0
    /// can never be beaten by an unsigned "&lt;" comparison) - so <see cref="Slope_18c"/> ends up non-zero
    /// only when ALL FOUR corners qualify, and is then the MINIMUM <c>(GroundProperty &gt;&gt; 1) &amp; 7</c>
    /// across them (PhysicsEngine.cs:1819-1820).
    ///
    /// RESTRICTION (E1 scope, documented deviation): called for the PLAYER ONLY - see the single call
    /// site in <see cref="Update"/>'s <c>IsPlayer</c> branch. Every NPC's <see cref="Slope_18c"/> stays
    /// the C# default 0. Reason: <see cref="Slope_18c"/> already has one live consumer,
    /// <see cref="PickEventTrigger"/>'s <c>DestroyOnSlidingSlope</c> check (<c>Slope_18c == 4</c>, water) -
    /// alimenting it for NPCs too could change their observable behaviour (and the intro trace). Map 389
    /// carries no water cell, so nothing could actually trigger that branch today even without this
    /// restriction, but the restriction is kept for this slice regardless, per the ticket.
    ///
    /// No-op (<see cref="Slope_18c"/> forced to 0) without <see cref="EntityFlags.Gravity"/> set
    /// (PhysicsEngine.cs:1706's own gravity gate) or without an installed
    /// <see cref="AlundraCellsCollisionField"/> (no <see cref="GroundSample"/> string tag was needed
    /// here - the four-corner rule needs the NUMERIC accessor <see cref="AlundraCellsCollisionField.SampleGroundProperty"/>,
    /// which only that concrete field type exposes, hence the type check instead of the plain
    /// <see cref="ICollisionField"/> interface <see cref="ComputeTerrainHeight"/> uses).
    /// </summary>
    internal void UpdateGroundSlope()
    {
        if ((Flags & EntityFlags.Gravity) == 0)
        {
            Slope_18c = 0;
            return;
        }

        if (Owner?.World?.CollisionField is not AlundraCellsCollisionField cellsField)
        {
            Slope_18c = 0;
            return;
        }

        var x1 = (PosX + ModX) >> 16;
        var x2 = (PosX + ModX + Width) >> 16;
        var y1 = (PosY + ModY) >> 16;
        var y2 = (PosY + ModY + Height) >> 16;
        var moddedPosZ = PosZ + ModZ;

        var bestFlagMask = 0xe00u;
        AlundraTerrainProbe.ProbeSlopeCorner(cellsField, x1, y1, moddedPosZ, ref bestFlagMask);
        AlundraTerrainProbe.ProbeSlopeCorner(cellsField, x2, y1, moddedPosZ, ref bestFlagMask);
        AlundraTerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);
        AlundraTerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);

        Slope_18c = (int)(bestFlagMask >> 9);
    }

    /// <summary>
    /// T3 (D-T-10, docs/plan-transitions-carte.md §1.6/§3): port of the <c>CombinedVramFlagsOR</c>/
    /// <c>CombinedVramFlagsAND</c> half of <c>PhysicsEngine.UpdateTileAttributes</c>'s gravity branch
    /// (<c>PhysicsEngine.cs:1740-1768</c>, §1.6.b) - the SAME four-corner qualification
    /// <see cref="UpdateGroundSlope"/> already ports, gathering the FULL per-corner <c>MapTile.Flags</c>
    /// (§1.6.d, <c>walkability | (groundProperty &lt;&lt; 8)</c>) instead of that method's own masked
    /// slope value. Kept as a SEPARATE method (not merged into <see cref="UpdateGroundSlope"/>) because
    /// the two fields (<see cref="Slope_18c"/>, <see cref="CombinedVramFlagsOR"/>/<see cref="CombinedVramFlagsAND"/>)
    /// have different consumers introduced at different chantiers - keeping them apart matches this
    /// port's own convention of one probe per original consumer group (<see cref="UpdateFloorHeight"/>
    /// alongside it is the same shape).
    ///
    /// Per corner: qualifies only when that corner's ground height (16.16, same rounding as
    /// <see cref="AlundraTerrainProbe.ProbeVramFlagsCorner"/>) is exactly equal to <c>PosZ + ModZ</c>.
    ///
    /// DEVIATION FROM THE LITERAL ORIGINAL (deliberate, D-T-10, same rule as
    /// <see cref="UpdateGroundSlope"/>'s own "DEVIATION" paragraph - read that one first): the original
    /// compares <c>MapHeights[i] + 1 == ModdedPosZ</c> (<c>PhysicsEngine.cs:1748</c>); THIS port's own
    /// resting invariant has no <c>+1</c> (<c>ModdedPosZ == TerrainHeight</c> exactly - see
    /// <see cref="UpdateGroundSlope"/>'s own doc for the full derivation and the four golden-trace
    /// measurements that confirm it). Porting the original's <c>+1</c> literally here would make this
    /// qualification permanently unsatisfiable, exactly like it would for <see cref="Slope_18c"/>.
    ///
    /// <see cref="CombinedVramFlagsOR"/> is the OR of the four per-corner contributions,
    /// <see cref="CombinedVramFlagsAND"/> their AND - a single disqualified corner contributes 0 to
    /// both, which zeroes the AND outright (a qualifying corner can never beat a 0 with AND) but need
    /// not zero the OR (the other three corners may still carry bits) - exactly the original's own
    /// <c>tempFlags[0]|tempFlags[1]|tempFlags[2]|tempFlags[3]</c> /
    /// <c>tempFlags[0]&amp;tempFlags[1]&amp;tempFlags[2]&amp;tempFlags[3]</c>
    /// (<c>PhysicsEngine.cs:1764-1765</c>).
    ///
    /// RESTRICTION (D-T-12, PLAYER ONLY - same rationale, same single call site convention, and same
    /// documentation shape as <see cref="UpdateGroundSlope"/>'s own restriction paragraph, which this one
    /// intentionally mirrors almost verbatim): <see cref="CombinedVramFlagsOR"/> already has a LIVE
    /// consumer for every entity, not just the player - the <c>DestroyOnVramFlags</c> transition tested
    /// with mask <c>0x8004</c> a few lines below in <see cref="Update"/> (exactly the hole/portal-floor
    /// bits this method writes). Alimenting this field for NPCs too would start destroying NPCs standing
    /// on portal tiles, a behaviour change T3's own acceptance proves does NOT happen (D-T-12). Every
    /// NPC's <see cref="CombinedVramFlagsOR"/>/<see cref="CombinedVramFlagsAND"/> therefore stay the C#
    /// default 0 - see the single call site in <see cref="Update"/>'s <c>IsPlayer</c> branch.
    ///
    /// Same no-op gates as <see cref="UpdateGroundSlope"/> (no <see cref="EntityFlags.Gravity"/>, no
    /// installed <see cref="AlundraCellsCollisionField"/>) - see that method's own doc for why.
    /// </summary>
    internal void UpdateVramFlags()
    {
        if ((Flags & EntityFlags.Gravity) == 0)
        {
            CombinedVramFlagsOR = 0;
            CombinedVramFlagsAND = 0;
            return;
        }

        if (Owner?.World?.CollisionField is not AlundraCellsCollisionField cellsField)
        {
            CombinedVramFlagsOR = 0;
            CombinedVramFlagsAND = 0;
            return;
        }

        var x1 = (PosX + ModX) >> 16;
        var x2 = (PosX + ModX + Width) >> 16;
        var y1 = (PosY + ModY) >> 16;
        var y2 = (PosY + ModY + Height) >> 16;
        var moddedPosZ = PosZ + ModZ;

        AlundraTerrainProbe.ProbeVramFlagsCorner(cellsField, x1, y1, moddedPosZ, out var corner0);
        AlundraTerrainProbe.ProbeVramFlagsCorner(cellsField, x2, y1, moddedPosZ, out var corner1);
        AlundraTerrainProbe.ProbeVramFlagsCorner(cellsField, x1, y2, moddedPosZ, out var corner2);
        AlundraTerrainProbe.ProbeVramFlagsCorner(cellsField, x2, y2, moddedPosZ, out var corner3);

        CombinedVramFlagsOR = corner0 | corner1 | corner2 | corner3;
        CombinedVramFlagsAND = corner0 & corner1 & corner2 & corner3;
    }

    /// <summary>
    /// E2 (docs/plan-echelles-chiffrage.md E2): port of <c>GetCollisionOnZ</c> (PhysicsEngine.cs:1602-1675),
    /// the method that feeds <c>entity.FloorHeight</c> (<c>PhysicsEngine.cs:1703</c>,
    /// <c>UpdateTileAttributes</c>: <c>entity.FloorHeight = hitz;</c>) - COMPOSED from this port's own
    /// existing bricks rather than re-ported line-for-line, per the plan: <see cref="ComputeTerrainHeight"/>
    /// for the terrain half (same 4-corner-max port <c>GetCollisionOnZ</c>'s own <c>entity.TerrainHeight</c>
    /// input feeds from, see <see cref="EvaluateEntitySupport"/>'s own <c>terrainHeight</c> local) and
    /// <see cref="EntitySupport.TryFindSupport"/> for the entity half (the SAME strict-below/highest-wins
    /// search <c>GetCollisionOnZ</c>'s own loop performs, field for field - see that method's own doc; both
    /// gate the entity search behind <see cref="EntitySupport.IsEligibleSubject"/>, the identical
    /// Collidable/NoEntityCollision/PlatformEntity conjunct <c>GetCollisionOnZ</c> checks at
    /// <c>:1606-1619</c> before ever touching <c>g_collideableEntities</c>).
    ///
    /// EQUIVALENCE ARGUMENT (verified against the decompiled source before composing, per the ticket):
    /// <c>GetCollisionOnZ</c> seeds <c>collision = entity.TerrainHeight + 1</c>, then for every eligible
    /// candidate below the entity's own feet (<c>otherEntityZTop &lt; entity.ModdedPosZ</c>) whose top is
    /// AT LEAST the running <c>collision</c> (<c>otherEntityZTop &gt;= collision</c>, i.e. NOT
    /// <c>otherEntityZTop &lt; collision</c>) and XY-overlaps (identical asymmetric <c>Width+1</c>/
    /// <c>Height+1</c> test), raises <c>collision</c> to <c>otherEntityZTop + 1</c> - "highest qualifying
    /// candidate wins, seeded at terrain height". <see cref="EntitySupport.TryFindSupport"/> is the exact
    /// same shape: seeded with <paramref name="seed"/>, a candidate qualifies only when
    /// <c>candidateTop &lt; moddedPosZ &amp;&amp; platformTopZ &lt;= candidateTop</c> (algebraically
    /// <c>NOT (candidateTop &gt;= moddedPosZ || platformTopZ &gt; candidateTop)</c> - the exact same two
    /// conjuncts, seed-variable renamed), and on match raises its own running seed to
    /// <c>candidateTop + 1</c> (<c>EntitySupport.cs:173</c>, "PhysicsEngine.cs:219/226/240/247" - the SAME
    /// otherEntityZTop + 1 update <c>GetCollisionOnZ:1661/:1668</c> performs). So calling
    /// <see cref="EntitySupport.TryFindSupport"/> with <c>seed = ComputeTerrainHeight() + 1</c> and taking
    /// its result (the winning candidate's <c>supportTopZ</c> when found, else the untouched seed)
    /// reproduces <c>GetCollisionOnZ</c>'s own return value bit for bit, in the ORIGINAL's <c>+1</c>
    /// convention.
    ///
    /// THE <c>-1</c> BELONGS TO THE SEED, NOT TO A FOUND RESULT (converting to THIS port's own
    /// convention): the original's resting invariant is <c>ModdedPosZ == TerrainHeight + 1</c>
    /// (<c>PhysicsEngine.cs:186</c>/<c>:128</c>), so on flat ground <c>GetCollisionOnZ</c>'s return -
    /// exactly "the terrain height, plus one" under that invariant - is one 16.16 unit ABOVE the terrain
    /// surface. THIS port's own TERRAIN resting invariant is <c>ModdedPosZ == TerrainHeight</c>, no
    /// <c>+1</c> (see <see cref="UpdateGroundSlope"/>'s own long "DEVIATION FROM THE LITERAL ORIGINAL"
    /// note for why - the extra unit is silently swallowed by
    /// <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/>'s own <c>posZ &gt;&gt; 16</c> truncation, so
    /// porting the original's <c>+1</c> literally here would make it permanently unobservable). That is
    /// why <paramref name="seed"/>'s own <c>-1</c> (folded into <c>terrainHeight</c> below, since
    /// <c>seed - 1 == terrainHeight</c> by construction) is correct when no candidate is found.
    ///
    /// The ENTITY branch does NOT carry this same offset, and must NOT have <c>-1</c> applied to it: THIS
    /// port kept the original's <c>+1</c> convention for entity-support resting, unlike terrain -
    /// <see cref="EntitySupport.TryFindSupport"/>'s own <c>platformTopZ = candidateTop + 1</c>
    /// (<c>EntitySupport.cs:173</c>) is exactly what <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>
    /// (<c>proxy.PosZ = proxy.PosZ - proxy.ModZ + 1</c>, mirroring the original's own spawn-adjust) relies
    /// on: an entity resting on a platform sits at <c>ModdedPosZ == candidateTop + 1</c>, not
    /// <c>candidateTop</c> (pinned by
    /// <c>AlundraNpcCharacterControllerMoverTests.Support_SailorElevenOnRealRecordTwoPlatform...</c>). So
    /// <see cref="EntitySupport.TryFindSupport"/>'s own <c>supportTopZ</c> (<c>candidateTop + 1</c>) IS
    /// already this port's own entity-resting value - subtracting 1 from it (as a previous version of
    /// this method did) would put <see cref="FloorHeight"/> one unit BELOW an entity actually standing on
    /// the platform, breaking the same "descent test" invariant the terrain branch is built to preserve
    /// (verified against real record data by <c>AlundraFloorHeightTests</c>' own platform case).
    /// Concretely: on flat ground with no entity underneath, <see cref="FloorHeight"/> ==
    /// <see cref="ComputeTerrainHeight"/>'s own return (the terrain height itself, no offset) - matching
    /// this port's own resting <c>ModdedPosZ</c> exactly, as verified by <c>AlundraFloorHeightTests</c>'
    /// flat-ground case; standing on an entity, <see cref="FloorHeight"/> == that entity's own
    /// <c>ModdedPosZ + Depth + 1</c> (its top surface, PLUS the port's own entity-resting <c>+1</c>) -
    /// verified by that same suite's platform case.
    ///
    /// RESTRICTION (player only, same rationale as <see cref="UpdateGroundSlope"/>'s own restriction -
    /// see the single call site in <see cref="Update"/>'s <c>IsPlayer</c> branch): <see cref="FloorHeight"/>
    /// has no live consumer yet in THIS slice (E2 is a pure brick - the descent condition above is E4's own
    /// job to wire), so widening this to NPCs cannot be justified by any currently-observable behaviour;
    /// kept restricted per the ticket, exactly like E1.
    /// </summary>
    internal void UpdateFloorHeight()
    {
        var terrainHeight = ComputeTerrainHeight();

        // E16.a (D-E16-29, docs/plan-e16-etat-partie.md): the player's own per-tick TerrainHeight (+0x138)
        // write - the original computes it from the SAME ComputeEntityGroundHeight probe this method
        // already calls for FloorHeight (T3.1's own "même valeur" finding), it only KEEPS the two in
        // separate fields (TerrainHeight AND FloorHeight, the latter FloorHeight's own +1-shifted seed
        // half). This is the hero's one per-tick call site into this probe (see this method's own call
        // site in Update's IsPlayer branch) - FloorHeight is unchanged.
        TerrainHeight = terrainHeight;

        FloorHeight = ComputeFloorHeight(terrainHeight);
    }

    /// <summary>
    /// The floor under the entity: the top (<c>candidateTop + 1</c>) of the highest eligible entity under its feet that <see cref="EntitySupport.TryFindSupport"/>
    /// finds seeded at <c>terrainHeight + 1</c>, else <paramref name="terrainHeight"/> itself. The -1 belongs to the seed (seed - 1 == terrainHeight): a found
    /// entity candidate's own supportTopZ already carries this port's entity-resting +1 convention and passes through unmodified (see
    /// <see cref="UpdateFloorHeight"/>'s own doc, "THE -1 BELONGS TO THE SEED" paragraph). Shared with the hero's tick (E19.d2c2 S3, <c>IsOnGround</c> in the state
    /// the tick holds).
    /// </summary>
    internal int ComputeFloorHeight(int terrainHeight)
    {
        var supportTopZ = 0;
        var found = ScriptHost != null
            && EntitySupport.IsEligibleSubject(this)
            && EntitySupport.TryFindSupport(this, ScriptHost.Collidables, terrainHeight + 1, out _, out supportTopZ);

        return found ? supportTopZ : terrainHeight;
    }

    /// <summary>
    /// E19.d2c2 F3: the top of an entity under the box (the very call of <see cref="ComputeFloorHeight"/>) is exactly at the foot (<c>PosZ + ModZ</c>): the entity is a floor the
    /// hero stands on, whatever the terrain under it.
    /// </summary>
    private bool IsEntityTopFlushWithFoot()
    {
        return ScriptHost != null
            && EntitySupport.IsEligibleSubject(this)
            && EntitySupport.TryFindSupport(this, ScriptHost.Collidables, ComputeTerrainHeight() + 1, out _, out var supportTopZ)
            && supportTopZ == PosZ + ModZ;
    }

    /// <summary>
    /// E3 (docs/plan-echelles-chiffrage.md É3): port of <c>GetTileHeightAtOffset</c>
    /// (<c>EntityGameplayManager.cs:277-345</c>) - the four-corner-max RAW terrain sample used, in the
    /// original, at an arbitrary XY offset from the entity's own footprint. The SAME
    /// <see cref="PosX"/>/<see cref="PosY"/>/<see cref="ModX"/>/<see cref="ModY"/>/<see cref="Width"/>/
    /// <see cref="Height"/> footprint as <see cref="ComputeTerrainHeight"/>, just displaced by
    /// <paramref name="offsetX"/>/<paramref name="offsetY"/> (both 16.16) before the four corners are
    /// derived.
    ///
    /// CORRECTED AFTER REVIEW (this slice's own re-derivation, not the original commit): the FIRST
    /// version of this port reused <see cref="AlundraTerrainProbe.SampleTerrainHeightCorner"/> -&gt;
    /// <see cref="AlundraCellsCollisionField.TrySampleGround(in Vector3, float, out GroundSample)"/>,
    /// i.e. <see cref="ComputeTerrainHeight"/>'s own helper. That is WRONG: the original
    /// <c>GetTileHeightAtOffset</c> (<c>EntityGameplayManager.cs:338-340</c>,
    /// <c>if (maxHeight &lt; tile.Height) maxHeight = tile.Height</c>) reads <c>tile.Height</c> - the
    /// cell's RAW, un-interpolated height - and NEVER inspects <c>tile.Slope</c>; there is no
    /// <c>switch (tile.Slope &amp; 0x3)</c> anywhere in its 68-line body. That switch belongs to a
    /// DIFFERENT original function, <c>PhysicsEngine.ComputeEntityGroundHeight</c>
    /// (PhysicsEngine.cs:1007-1061) - the function <see cref="AlundraCellsCollisionField"/>'s
    /// <c>ComputeGroundHeight</c> actually ports. Reusing it here silently imported slope
    /// interpolation the original does not perform: measured on map 389's real production hero
    /// footprint, 4560 of 1,159,515 integer poses diverge from the original by 1 to 15 px (worst case
    /// x1=312,y1=447: this port would have returned 145 px where the original returns 160 px). This
    /// version instead samples <see cref="AlundraCellsCollisionField.SampleRawCellHeight"/> - the raw
    /// per-cell value, added alongside <see cref="AlundraCellsCollisionField.SampleGroundProperty"/> in
    /// the exact same additive style É1 already established - so no interpolation is ever applied here.
    ///
    /// UNITS: <c>tile.Height</c> is in CELL units (1 unit = 16 px, <c>StaticVariables.MapTileHeight</c>),
    /// and the original converts straight to 16.16 pixels with <c>maxHeight &lt;&lt; 0x14</c>
    /// (EntityGameplayManager.cs:344). Derivation: <c>unit &lt;&lt; 20 == unit * 1,048,576 ==
    /// (unit * 16 px) * 65,536</c> - i.e. <c>&lt;&lt; 20</c> is exactly "convert unit to px (`&lt;&lt; 4`)
    /// then to 16.16 (`&lt;&lt; 16`)" fused into one shift; no separate multiply is needed. This port
    /// keeps the single fused shift for the same reason.
    ///
    /// ATTENUATION (not an excuse - documented per the ticket): map 389's four scale cells and their
    /// north neighbors are all flat (<c>slope &amp; 3 == 0</c>), so at É4's own call site
    /// (<c>PlayerManager.cs:718-719</c>) on THIS map the interpolation bug above would never actually
    /// have fired. That does not make the earlier port correct - it was wrong on 4560 real poses, and
    /// this method is also the brick <c>EntityGameplayManager.GetEntityTileHeight</c>
    /// (EntityGameplayManager.cs:262-274) would reuse for arbitrary directional offsets - so it is
    /// fixed regardless of today's reachability.
    ///
    /// TWO PIECES OF THE ORIGINAL DELIBERATELY NOT PORTED (documented, not silently dropped):
    /// <list type="bullet">
    /// <item><description><c>g_tileToWorldXTable</c> (<c>EntityGameplayManager.cs:289-292</c>): a
    /// precomputed lookup that answers <c>(pixelsX &gt;&gt; 16) / 24</c> - a fixed division by 24 (the
    /// cell width), nothing more (established in the read-only reconnaissance for this slice). This
    /// port already performs that same division directly, inside
    /// <see cref="AlundraCellsCollisionField.SampleRawCellHeight"/> (<c>x / CellWidthPx</c>,
    /// <c>CellWidthPx = 24</c>) - reusing it gets this division for free without needing the table.
    /// The original's table has 1248 entries and is never clamped before indexing
    /// (out-of-range is undefined there); this port's own clamp-to-nearest-cell (inherited from
    /// <see cref="AlundraCellsCollisionField"/>'s established convention, see its class doc) is a
    /// DEVIATION for out-of-map offsets, not a faithful port of that table - see
    /// <see cref="AlundraCellsCollisionField.TrySampleGround(in Vector3, float, out GroundSample)"/>'s
    /// own doc for the same pre-existing (E1) deviation.</description></item>
    /// <item><description>The <c>ClassA</c>/<c>ClassB</c> walkability filter
    /// (<c>EntityGameplayManager.cs:300-304</c>/<c>333-336</c>, returning the sentinel <c>0x7800000</c>
    /// on an unwalkable corner): NOT ported. Earlier text here claimed the hero never carries either
    /// flag, citing <c>PhysicsEngine.cs:1087-1098</c> as proof - that citation is
    /// <c>GetCollisionFlagsWithPlayer</c>, which reads <c>player.Flags &amp; EntityFlags.ClassB</c>/
    /// <c>ClassA</c> to build the PLAYER's own mask, i.e. evidence the player CAN carry these flags
    /// (e.g. <c>FunctionTypeC.cs:17343</c>, <c>player.Flags |= EntityFlags.ClassB | Gravity</c>; script
    /// opcodes 0x28/0x2A also set them on <c>logicEntity</c>, EntityEventHandlers.cs:983,997). So this
    /// is correctly "not ported, reachability not established" - NOT "dead code": if the flag were ever
    /// set, the original would return the sentinel (always &gt;= any real terrain height), while this
    /// port would return a real terrain sample instead, changing the SENSE of whatever guard consumes
    /// it, not just its value. Left unported because widening scope to a flag state this slice has no
    /// evidence is reachable for the intended (hero) caller would be inventing behaviour, not
    /// preserving any - to be revisited before any future caller passes a ClassA/ClassB-carrying
    /// entity through this method.</description></item>
    /// </list>
    ///
    /// NO <c>+1</c>/<c>-1</c> ANYWHERE IN THIS METHOD (lesson from earlier slices in this same plan):
    /// the original's own body (<c>EntityGameplayManager.cs:277-345</c>) never adds or subtracts 1
    /// against <c>ModdedPosZ</c> or any resting invariant - unlike <see cref="UpdateGroundSlope"/> or
    /// <see cref="UpdateFloorHeight"/>, this is a plain terrain-height sample, not a "resting on"
    /// comparison, so there is no <c>+1</c> to re-derive or drop here. The sole offset in this method is
    /// the caller-supplied spatial displacement (<paramref name="offsetX"/>/<paramref name="offsetY"/>,
    /// e.g. the ladder guard's own <c>-0x10000</c> - one pixel north, PlayerManager.cs:718), which is
    /// not a resting-invariant compensation at all.
    ///
    /// FALLBACK: the original seeds <c>maxHeight</c> as <c>uint 0</c> (EntityGameplayManager.cs:285,
    /// 306), so the result is floored at 0 by construction, never by a separate "no ground" branch -
    /// <c>tile.Height</c> is always non-negative (a raw map byte). This port seeds <c>best</c> at 0 the
    /// same way (no <c>int.MinValue</c> sentinel, no dead "no ground" ternary): raw cell heights are
    /// likewise always non-negative, so there is no negative case to floor.
    ///
    /// RESTRICTION (E3 scope): this method has NO CALL SITE yet - it is wired to nothing, per the ticket
    /// (docs/plan-echelles-chiffrage.md É3, "Débloque en propre : rien. Brique de É4."). The ladder
    /// climb guard that will consume it (<c>PlayerManager.cs:718-719</c>,
    /// <c>PosZ &lt;= GetTileHeightAtOffset(entity, 0, -0x10000)</c>) is É4's job. Kept as a pure,
    /// side-effect-free instance method (reads <see cref="PosX"/>/<see cref="PosY"/>/<see cref="ModX"/>/
    /// <see cref="ModY"/>/<see cref="Width"/>/<see cref="Height"/>/<see cref="Owner"/> only, writes
    /// nothing) rather than gated behind an <c>IsPlayer</c> restriction like <see cref="UpdateGroundSlope"/>/
    /// <see cref="UpdateFloorHeight"/> - those two write shared entity state (<see cref="Slope_18c"/>/
    /// <see cref="FloorHeight"/>) that a stray NPC call could observably corrupt; this one only computes
    /// and returns a value, so there is nothing to protect by restricting the caller.
    /// </summary>
    /// <param name="offsetX">Horizontal displacement (16.16) applied to the footprint before sampling.</param>
    /// <param name="offsetY">Vertical displacement (16.16) applied to the footprint before sampling.</param>
    /// <returns>The maximum RAW terrain height (16.16, cell-unit precision - never interpolated by
    /// slope) across the four displaced corners, or 0 if no <see cref="AlundraCellsCollisionField"/> is
    /// installed.</returns>
    internal int GetTileHeightAtOffset(int offsetX, int offsetY)
    {
        if (Owner?.World?.CollisionField is not AlundraCellsCollisionField cellsField)
        {
            return 0;
        }

        var x1 = (PosX + ModX + offsetX) >> 16;
        var x2 = (PosX + ModX + offsetX + Width) >> 16;
        var y1 = (PosY + ModY + offsetY) >> 16;
        var y2 = (PosY + ModY + offsetY + Height) >> 16;

        var best = 0;
        AlundraTerrainProbe.SampleRawTileHeightCorner(cellsField, x1, y1, ref best);
        AlundraTerrainProbe.SampleRawTileHeightCorner(cellsField, x2, y1, ref best);
        AlundraTerrainProbe.SampleRawTileHeightCorner(cellsField, x1, y2, ref best);
        AlundraTerrainProbe.SampleRawTileHeightCorner(cellsField, x2, y2, ref best);

        return best << 20;
    }

    /// <summary>
    /// Routes a scripted (post-spawn) write to <see cref="PosX"/>/<see cref="PosY"/>/<see cref="PosZ"/>
    /// onto the CasaEngine root transform - docs/plan-e3-collisions.md E3.d "DLL - propriete de la
    /// racine par frame" item 4 (grep sites: <c>AlundraEventProgramRunner</c>'s 0x64
    /// SetEntitiesPosition/0x65 AddEntitiesPositionOffset/0x8B SpawnEntityNextToEntity). A no-op for
    /// every entity WITHOUT a <see cref="Controller"/> - those keep the deferred per-frame
    /// re-derivation <see cref="AlundraFrameSyncPasses.SyncTransform"/> already does every frame (see that
    /// method's own doc), unchanged since E3.a. For a controller-driven entity the root write can no
    /// longer wait for <see cref="AlundraFrameSyncPasses.SyncTransform"/> - that method now skips the root
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

        var root = AlundraEntitySpawnFactory.ResolveLogicalPosition(PosX, PosY, PosZ);
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
    /// deviation, documented on the same plan section); a blocked axis advances to the contact on the cell
    /// field (E19.a2, ADR-0045 of the engine: the hero stops against the wall, no longer one step short of
    /// it), so the returned displacement is the part up to the contact. It DOES set <see cref="ForceAdjusted"/> (E4.d)
    /// when, on either horizontal axis, more than <see cref="ForceAdjustedEpsilonPixels"/> was requested
    /// here and the controller's own returned displacement is at most that (E19.a3, D-E19-12: no progress on
    /// the axis, as the binary's tick with no accepted sub-step; a step shortened to the contact that still
    /// advances leaves the flag alone - see <see cref="ForceAdjusted"/>'s own doc). A
    /// no-op without a controller (the caller falls back to its own direct
    /// <see cref="PosX"/>/<see cref="PosY"/> += in that case) - <see cref="ForceAdjusted"/> is left
    /// untouched, same as every other controller-gated site on this class.
    /// </summary>
    internal Vector3 MoveControllerAndPullPosition(float deltaXPixels, float deltaYPixels)
    {
        if (Controller == null || Owner?.RootComponent == null)
        {
            return Vector3.Zero;
        }

        var requested = new Vector3(deltaXPixels, deltaYPixels, 0f);

        // E19.d2b B4 (D-E19-29): the contact of the dialogue and of the grab is the entity that shortened or cancelled THIS step - the binary's +0x130
        // (0x80037F08): the obstacle of the tick, the one nearest the contact when two follow each other (the LAST axis processed, h2 when both), 0 when
        // nothing blocked or no displacement was asked. It is rewritten at every step, and kept while the world is frozen (no step is taken then).
        XCollisionEntity = null;
        var actual = Controller.Move(requested);
        var contact = Controller.LastContact;
        if ((contact.H2Obstacle ?? contact.H1Obstacle)?.GameplayProxy is AlundraEntityScriptProxy blockingEntity)
        {
            XCollisionEntity = blockingEntity;
            EntityBlockCount++;
        }

        if (AxisMadeNoProgress(requested.X, actual.X) || AxisMadeNoProgress(requested.Y, actual.Y))
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
        //
        // E19.d2c1 R6: nor does this pull rewrite the PosZ the air state of the hero has just posted this tick (ZHeldByTick).
        if (!WasEntitySupportedLastTick && !ZHeldByTick)
        {
            PosZ = (int)Math.Round((double)root.Z * 65536.0);
        }

        return actual;
    }

    /// <summary>
    /// E19.d2c1 R6: makes the root follow the logical <see cref="PosZ"/> (the truth of the hero in the air state) by a vertical displacement of the controller - never
    /// <see cref="PushLogicalPositionToRoot"/> or a teleport, which would truncate X, Y and Z to the pixel and reset the vertical latch. A no-op without a controller.
    /// </summary>
    internal void FollowPosZOnRoot()
    {
        if (Controller == null || Owner?.RootComponent == null)
        {
            return;
        }

        var delta = PosZ / 65536f - Owner.RootComponent.LocalTransform.Position.Z;
        if (delta != 0f)
        {
            Controller.Move(new Vector3(0f, 0f, delta));
        }
    }

    /// <summary>
    /// Routes THIS tick's own vertical displacement through the controller's own
    /// <see cref="CharacterControllerComponent.Move"/> - the seam <see cref="EvaluateEntitySupport"/>'s own
    /// "not found" tail uses instead of a controller-owned velocity the engine would integrate over REAL
    /// elapsed time (see that call site's own doc for the full measured gull-entity-6 numbers). Move()
    /// takes a pure displacement, never a rate, so the exact <c>FinalForceZ</c> pixel amount lands
    /// regardless of how long the current rendered frame took - this is what makes the per-tick vertical
    /// step frame-rate invariant.
    /// <para>
    /// Investigated (read-only, <c>CharacterControllerComponent.cs</c>): <c>Move</c>'s displacement is
    /// resolved by <c>MoveWithCollisions</c> against the FULL 3D physics world (<c>Sweep</c>/
    /// <c>TryStepMove</c>/<c>Slide</c>) - a nonzero Z component here is genuinely swept against solid
    /// colliders (walls, ceilings), not silently dropped or treated as horizontal-only; only
    /// <c>ResolveHorizontalDisplacementAgainstField</c>'s own per-corner walkability/step-height probe is
    /// horizontal-axis-specific (its own h1Amount/h2Amount are both 0 for a pure-Z displacement, so that
    /// probe is a no-op here and the Z delta passes straight to the general sweep). What Move() does NOT
    /// do: ground-snap or refresh <see cref="CharacterControllerComponent.IsGrounded"/>/MovementState -
    /// that is <c>UpdateGround</c>'s own job, run only from the controller's per-RENDER-FRAME
    /// <c>Update</c> (via <c>CharacterMotionSystem</c>, BEFORE this same entity's own <see cref="Update"/>
    /// each frame - see that method's own doc), never from <c>Move()</c> itself. So
    /// <see cref="Controller"/>.IsGrounded here still reflects the LAST render frame's own ground probe -
    /// the same one-frame lag <see cref="EvaluateEntitySupport"/>'s own terrain-reset branch already
    /// documents and accepts. <c>UpdateGround</c>'s own field-based ground snap still runs every render
    /// frame regardless of this call (unaffected by this fix): once this entity's Z genuinely reaches real
    /// ground, that snap (not this method) is what pins it there and zeroes the downward component of
    /// <see cref="CharacterControllerComponent.Velocity"/> - see
    /// <c>GravityFlagged_NoScriptImpulse_FallsTickQuantizedAndLandsOnRealGround_ClearingFlagFreezesAltitude</c>.
    /// </para>
    /// <para>
    /// Deliberately a SEPARATE call from <see cref="MoveControllerAndPullPosition"/> (not one Move() call
    /// carrying X/Y/Z together): this tick's own ForceZ gravity decay is computed by
    /// <see cref="EvaluateEntitySupport"/>, which runs AFTER <see cref="AlundraScriptedMotion.RunOneKinematicTick"/>'s
    /// own horizontal Move() call this same tick (see <see cref="Update"/>'s own doc on the fused per-tick
    /// loop order: script, THEN motion, THEN support) - by the time <see cref="FinalForceZ"/> is known for
    /// this tick, the horizontal step has already been taken. A second, vertical-only Move() call here is
    /// still exactly ONE per-tick Z displacement, sweep-resolved the same way the horizontal one is -
    /// reordering the fused loop so a single call could carry both axes was rejected as materially riskier
    /// (it would move the ForceZ decay ahead of the script pass, changing an order several other documented
    /// fixes on this class rely on) for no behavioural difference: two same-tick Move() calls against an
    /// unmoving collision world resolve identically to one call carrying both deltas at once.
    /// </para>
    /// Does not touch <see cref="ForceAdjusted"/> - that flag is the original's own HORIZONTAL "movement
    /// was curtailed" signal (0x1F Walk-with-collision's own exit test, see that field's own doc); the
    /// original has no vertical equivalent. A no-op without a controller (bare-fallback spawn/harness
    /// proxy), same shape as every other controller-gated site on this class.
    /// </summary>
    internal void MoveVerticalAndPullPosition(float deltaZPixels, bool wasSupportedEnteringThisTick)
    {
        if (Controller == null || Owner?.RootComponent == null)
        {
            return;
        }

        Controller.Move(new Vector3(0f, 0f, deltaZPixels));

        // Same guard MoveControllerAndPullPosition's own horizontal pull already uses (see that method's
        // own doc) - just evaluated against the caller-captured PRE-this-tick value
        // (EvaluateEntitySupport's own wasSupportedEnteringThisTick, not its current
        // WasEntitySupportedLastTick field, already flipped false by the time this "not found" tail runs).
        // A support-pinned entity's authoritative PosZ comes from the "found" branch's own supportTopZ
        // clamp, not a live float32 root read (the same 1-unit-margin precision argument
        // WasEntitySupportedLastTick's own doc makes) - without this guard, an entity whose support just
        // ended THIS tick had its preserved PosZ silently re-quantized (eroded by exactly the same 1 16.16
        // unit WasEntitySupportedLastTick's own fix already solved for MoveControllerAndPullPosition) one
        // tick EARLIER than that fix's own documented "one-frame-late" contract, purely because this
        // method's own call site (the "still falling/rising" branch) happens to run on the very tick
        // support ends.
        if (!wasSupportedEnteringThisTick)
        {
            var root = Owner.RootComponent.LocalTransform.Position;
            PosZ = (int)Math.Round((double)root.Z * 65536.0);
        }
    }

    /// <summary>Small horizontal-axis tolerance <see cref="MoveControllerAndPullPosition"/> uses on each axis,
    /// both to decide that a displacement was requested at all (more than this) and that the controller
    /// obtained none of it (at most this) - well under a single pixel, so ordinary floating-point noise from
    /// the <c>Move</c> round trip never sets <see cref="ForceAdjusted"/> spuriously, while any REAL wall/
    /// step-height block reliably does on the tick the entity keeps pushing without advancing (E19.a3,
    /// D-E19-12: the tick that merely reaches the contact is a shortened step that still advances, and does
    /// not).
    /// </summary>
    private const float ForceAdjustedEpsilonPixels = 0.01f;

    /// <summary>True when, on one horizontal axis, a displacement beyond
    /// <see cref="ForceAdjustedEpsilonPixels"/> was requested and the controller obtained at most that
    /// much of it, i.e. the axis made no progress at all (E19.a3, D-E19-12).</summary>
    internal static bool AxisMadeNoProgress(float requested, float actual)
    {
        return MathF.Abs(requested) > ForceAdjustedEpsilonPixels
            && MathF.Abs(actual) <= ForceAdjustedEpsilonPixels;
    }

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
            SpawnAnimationActive = SpawnAnimationActive,
            SpawnAnimationId = SpawnAnimationId,
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
            RecordHeight = RecordHeight,
            SpriteTableIndex = SpriteTableIndex,
            SpriteType = SpriteType,
            Flags = Flags,
            DialoguePortrait = DialoguePortrait,
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
