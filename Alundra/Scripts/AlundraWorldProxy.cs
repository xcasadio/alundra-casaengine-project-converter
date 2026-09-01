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
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// World script every converted .world declares as its "script_class_name" (see
/// <c>WorldWriter.WorldScriptClassName</c>). On world load, read the map's tilemap "Entities"
/// object layer (see <c>AlundraDataExtractor.TiledMapExporter</c>) and spawn one game entity per
/// record. Each record carries a <c>PrefabAssetId</c> custom property (see
/// <c>AlundraDataExtractor.TiledMapExporter</c>/<c>EntityBankPrefabWriter</c>) linking it to the
/// per-bank prefab (<c>Entities/{Name}/{Name}.entity</c>); the normal path clones that prefab so
/// the spawned entity carries the bank's sprite/collision components. When the link is missing,
/// cannot be loaded, or the record has none, a bare entity is created instead (logged fallback).
/// Either way, the resulting entity carries an <see cref="AlundraEntityScriptProxy"/> filled by
/// <see cref="EntityRecordMapper"/>, whose logical position fields (<c>PosX</c>/<c>PosY</c>/<c>PosZ</c>)
/// this proxy then converts into the spawned entity's <c>RootComponent.LocalTransform.Position</c> - now
/// the entity's LOGICAL pose (E3.a, docs/plan-e3-collisions.md decision E3-1), not a render pose - via
/// <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/> - see <see cref="AlundraEntitySpawnFactory.CreateEntityFromPrefab"/>. A
/// <c>RenderProjectionComponent</c> child of that root (<c>SpriteWriter.WriteEntityPrefab</c>) derives
/// the render pose from it every update.
///
/// This proxy also retains every entity it spawned and drives their status machine each frame (see
/// <see cref="Update"/>), a faithful port of the two-phase pass of
/// <c>EntityManager.UpdateEntitiesEvents</c> @ 0x800386D0: the original manager-level pass, not a
/// per-entity one, which is why the driver lives here rather than on
/// <see cref="AlundraEntityScriptProxy"/> (whose own <c>Update</c> stays a no-op by design - see its
/// doc comment). Actual event-program execution goes through the <see cref="IEventProgramRunner"/> seam
/// (<see cref="EventProgramRunner"/>); the bytecode interpreter itself is a later chantier. Transform
/// re-derivation when the logical position changes at runtime is still a follow-up task.
/// </summary>
public class AlundraWorldProxy : GameplayProxy, IEntityWorldContext, IAlundraScriptHost
{
    // EntityRecordMapper's own tile constants (StaticVariables.MapTileWidth/Height) - duplicated here
    // (rather than made public there) since only the player spawn (this class) and that pure mapper need
    // them; see EntityRecordMapper's own class doc for the same constants' derivation.
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    /// <summary>Catalog name of the hero bank prefab (<c>Entities/Alundra/Alundra.entity</c>) - see
    /// <see cref="SpawnPlayerEntity"/>'s own doc.</summary>
    private const string HeroAssetName = "Alundra";

    /// <summary>Key of the custom property linking an "Entities" record to its bank prefab asset.</summary>
    private const string PrefabAssetIdPropertyKey = "PrefabAssetId";

    /// <summary>Name of the entity carrying the map's <see cref="TileMapComponent"/> (see WorldWriter).</summary>
    private const string TileMapEntityName = "tileMap";

    private const string EntitiesLayerName = "Entities";
    private const string PortalsLayerName = "Portals";
    private const string MapEventsLayerName = "MapEvents";

    /// <summary>
    /// Name of the debug-only environment variable that gates whether the right stick may drive
    /// <see cref="AlundraCameraDirector.UpdateDebugCameraPan"/>'s DEBUG offset (and its R3/right-stick-click reset) at all.
    /// ENABLED BY DEFAULT - user decision, 2026-08-24 - deliberately the opposite convention of
    /// <see cref="AlundraPlayerManager.DebugIgnoreControlLockEnvVar"/> ("never active by default"):
    /// that flag bypasses a real gameplay gate, so it must be opted into explicitly, whereas this one
    /// only gates an always-optional debug convenience layered on top of whatever the camera is already
    /// doing (see <see cref="_debugCameraBase"/>) - leaving it on by default costs nothing and saves
    /// developers from re-enabling it every session. Set to exactly "0" or "false" (case-insensitive) to
    /// disable; any other value, or leaving it unset, keeps it enabled.
    /// </summary>
    internal const string DebugCameraPanEnabledEnvVar = "ALUNDRA_DEBUG_CAMERA_ENABLED";

    /// <summary>Real-world value of <see cref="DebugCameraPanEnabledEnvVar"/> - the environment variable
    /// read exactly once (static readonly, evaluated on this type's first use) and logged exactly once
    /// when it evaluates disabled - see that field's own doc.</summary>
    private static readonly bool DebugCameraPanEnabledFromEnvironment = ReadDebugCameraPanEnabledFromEnvironment();

    /// <summary>Test-only seam over <see cref="DebugCameraPanEnabledFromEnvironment"/> - same rationale as
    /// <see cref="AlundraPlayerManager"/>'s own <c>_debugIgnoreControlLockOverrideForTests</c> seam: a
    /// shared xunit host cannot guarantee this type's static field has not already been forced to
    /// initialize before a test sets the real environment variable. Never read or written by production
    /// code paths.</summary>
    private static bool? _debugCameraPanEnabledOverrideForTests;

    /// <summary>Internal (widened from private, S2's base extraction rule) so
    /// <see cref="AlundraCameraDirector.UpdateDebugCameraPan"/> can read it after the camera wiring moved
    /// out of this class.</summary>
    internal static bool DebugCameraPanEnabled
        => _debugCameraPanEnabledOverrideForTests ?? DebugCameraPanEnabledFromEnvironment;

    /// <summary>Test-only read of <see cref="DebugCameraPanEnabled"/> - that property itself is private
    /// (production code only reads it from inside <see cref="AlundraCameraDirector.UpdateDebugCameraPan"/>), so tests exercise
    /// the flag's resolution through this seam instead.</summary>
    internal static bool DebugCameraPanEnabledForTests => DebugCameraPanEnabled;

    private static bool ReadDebugCameraPanEnabledFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(DebugCameraPanEnabledEnvVar);
        var disabled = raw == "0" || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase);

        if (disabled)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: {DebugCameraPanEnabledEnvVar}={raw} - debug camera pan stick input "
                + "disabled (default is enabled).");
        }

        return !disabled;
    }

    /// <summary>Test-only seam - see <see cref="_debugCameraPanEnabledOverrideForTests"/>'s own doc. Pass
    /// null to restore the real environment-variable-derived value.</summary>
    internal static void SetDebugCameraPanEnabledOverrideForTests(bool? value)
        => _debugCameraPanEnabledOverrideForTests = value;

    /// <summary>
    /// Entities spawned by this proxy in <see cref="InitializeWithWorld"/> (both the prefab-clone and
    /// bare-fallback paths), in creation order - <see cref="Update"/> drives their status machine in
    /// this same order, mirroring the original manager's single flat entity-slot array.
    /// </summary>
    private readonly List<Entity> _spawnedEntities = new();

    /// <summary>
    /// Every "Entities" record of this world's own tilemap layer, keyed by its <c>Index</c> custom
    /// property - i.e. the same id <see cref="AlundraEntityScriptProxy.EntityRefId"/> is filled from
    /// (<see cref="EntityRecordMapper"/>) and the id the dynamic-spawn opcode 0x2D
    /// (<c>GameEngine.SpawnEntity</c>'s <c>entityId</c> parameter) looks records up by
    /// (<c>GameEngine.GetEntityRecord</c>, GameEngine.cs:2125-2144). Populated once in
    /// <see cref="InitializeWithWorld"/> from every record of the layer, including ones
    /// <see cref="AlundraEntitySpawnFactory.ShouldSpawnRecord"/> would reject for the map-load pass - 0x2D applies its own,
    /// looser gate (<c>notCheckSpawnZone = 1</c>, see <see cref="SpawnEntityByRecordId"/>) so a record
    /// this world did not spawn at load time can still become spawnable later.
    /// </summary>
    private readonly Dictionary<int, TileMapObjectData> _entityRecordsByIndex = new();

    /// <summary>
    /// Per-frame working list for <see cref="Update"/>: cleared and refilled from
    /// <see cref="_spawnedEntities"/> every frame instead of allocating a temporary list in the hot
    /// path. Kept as a re-read of each entity's <c>GameplayProxy</c> (rather than a list frozen at
    /// spawn) so <see cref="_spawnedEntities"/> stays the single source of truth.
    /// </summary>
    private readonly List<AlundraEntityScriptProxy> _updateProxies = new();

    /// <summary>
    /// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): this frame's collidable-entity snapshot
    /// (<see cref="EntitySupport.BuildCollidables"/>), rebuilt from <see cref="_updateProxies"/> every time
    /// that list is refreshed (<see cref="RefreshUpdateProxiesAndCollidables"/>) - same no-per-frame-
    /// allocation shape as <see cref="_updateProxies"/> itself. Exposed as <see cref="IAlundraScriptHost.Collidables"/>.
    /// </summary>
    private readonly List<AlundraEntityScriptProxy> _collidables = new();

    /// <summary>
    /// Seam over actual event-program execution (see <see cref="IEventProgramRunner"/>); defaults to a
    /// silent no-op since the bytecode interpreter does not exist yet. Internal, not injected through
    /// the constructor: <c>ElementFactory</c> constructs gameplay proxies parameterless, so tests swap
    /// this field directly instead.
    /// </summary>
    internal IEventProgramRunner EventProgramRunner = new NoOpEventProgramRunner();

    /// <summary>
    /// Backing store for <see cref="EventProgramRunner"/> when it is a real <see cref="AlundraEventProgramRunner"/>
    /// (see <see cref="InitializeWithWorld"/>) - kept as its own field (rather than only living inside the
    /// runner's constructor call) so <see cref="Update"/> can read <see cref="AlundraGameState.PlayerControlFlags"/>
    /// for <see cref="RunMapEventsPass"/>'s own gate, mirroring how the original reads the SAME global
    /// (<c>g_playerControlFlags</c>) from both <c>RunMapEvents</c> and the opcode handlers alike.
    /// </summary>
    internal readonly AlundraGameState GameState = new();

    /// <summary>
    /// The New Game hero entity, spawned once in <see cref="InitializeWithWorld"/> BEFORE every "Entities"
    /// record (its tile gates their own spawn-zone check, exactly like the original -
    /// <see cref="ShouldSpawnRecord(TileMapObjectData,bool,int,int,out string)"/>). Minimal V1 port of
    /// <c>ResetEntityState</c> (GameEngine.cs:648-670, called from <c>InitializeEntitySlots</c> BEFORE its
    /// own record-spawn loop, GameEngine.cs:626-643) - see <see cref="SpawnPlayerEntity"/>'s own doc for
    /// exactly what is and is not ported. Null when this world has no hero asset in the catalog, no prefab
    /// loader, or the loader fails (logged, degraded - same shape as every other spawn failure in this
    /// class); in that case no MapEvents run either (they always execute against the player,
    /// <see cref="RunMapEventsPass"/> requires a non-null player).
    /// </summary>
    /// Setter widened to <c>internal</c> (C1, docs/plan-camera-ordre-frame.md §4): the map-event
    /// characterization test needs to seed this directly - <see cref="AdoptPlayerPawn"/> is not
    /// headless-reachable (it needs a live <see cref="AlundraPlayerController"/>), same precedent as
    /// <see cref="InstallCellAndOverlaySystems"/> being carved out of <see cref="InitializeWithWorld"/>.
    internal AlundraEntityScriptProxy? PlayerEntity { get; set; }

    /// <summary>
    /// This world's MapEvents (port of <c>InitializeMapEvents</c>, GameEngine.cs:476-583): one entry per
    /// "MapEvents" object-layer record whose <c>EventCodesBIndex</c> is non-zero, in record order, each
    /// always executing against <see cref="PlayerEntity"/> - see <see cref="RunMapEventsPass"/>'s own doc.
    /// Empty (not null) when this world has no "MapEvents" layer, or no <see cref="PlayerEntity"/> to run
    /// them against.
    /// </summary>
    private readonly List<AlundraMapEvent> _mapEvents = new();

    private bool _loggedNoHeroHeader;

    /// <summary>E3.d: logged once when the hero's engine-spawned pawn carries no
    /// <see cref="CharacterControllerComponent"/> (older/regenerated non-hero prefab reused as the
    /// default pawn, or an export that predates E3.d's converter change) - the player then keeps E2's
    /// controller-free movement, exactly as before this chantier.</summary>
    private bool _loggedNoHeroController;

    /// <summary>E2: this world's own <see cref="AlundraPlayerController"/>, resolved once in
    /// <see cref="InitializeWithWorld"/> (<c>World.PlayerControllers</c> is already populated by then -
    /// see <see cref="AdoptPlayerPawn"/>'s own doc). Null when no such controller exists (no
    /// <c>.gameMode</c>/PlayerStartupSettings wired for this world, or its <c>player_controller_class</c>
    /// resolved to something other than <see cref="AlundraPlayerController"/>) - logged once.</summary>
    private AlundraPlayerController? _playerController;
    private bool _loggedNoPlayerController;

    /// <summary>
    /// Seam over <c>Data/sprite-records.json</c> lookups (see <see cref="Alundra.Scripts.SpriteRecordCatalog"/>'s
    /// class doc), read once and reused for every record this proxy spawns. Internal, not injected
    /// through the constructor - same reasoning as <see cref="EventProgramRunner"/>: <c>ElementFactory</c>
    /// constructs gameplay proxies parameterless, so tests swap this field directly.
    /// </summary>
    internal ISpriteRecordCatalog SpriteRecordCatalog = new SpriteRecordCatalog();

    /// <summary>
    /// Port of the original global <c>g_activeCollisionEntity</c>: the entity currently involved in the
    /// active collision pair, used by the pick phase to decide whether a touch downgrades all the way
    /// to an interact (slot F). Null in V1 (no collision system driving it yet); settable internally for
    /// tests.
    /// </summary>
    internal AlundraEntityScriptProxy? ActiveCollisionEntity;

    /// <summary>
    /// DEBUG ONLY (see <see cref="AlundraCameraMath.DebugCameraPanSpeedPixelsPerSecond"/>). Cached so <see cref="Update"/>,
    /// which gets no <see cref="World"/> parameter, can still read the gamepad and reach the camera entity
    /// looked up in <see cref="InitializeWithWorld"/>.
    /// </summary>
    private World? _world;

    /// <summary>
    /// This world's own <see cref="TileMapData"/> (resolved once in <see cref="InitializeWithWorld"/>,
    /// same instance <see cref="AlundraCellsCollisionField"/>/<see cref="AdoptPlayerPawn"/> already read) -
    /// cached so every NPC spawn (<see cref="AlundraEntitySpawnFactory.CreateEntityFromRecord"/> -&gt; <see cref="AlundraEntitySpawnFactory.CreateEntityFromPrefab"/>/
    /// <see cref="AlundraEntitySpawnFactory.CreateBareEntityFromRecord"/> -&gt; <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>, both the
    /// map-load loop below and the dynamic-spawn opcode 0x2D's own <see cref="SpawnEntityByRecordId"/>)
    /// can resolve THIS map's own Gravity/ZViscosity (E4.b, docs/plan-e4-deplacement-scripte.md "Spawn")
    /// without re-threading a <see cref="TileMapData"/> parameter through every caller of this world's own
    /// per-frame passes. Null before <see cref="InitializeWithWorld"/> runs, or when this world has no
    /// loaded tilemap (see that method's own early-return) - every reader already treats a missing map
    /// gracefully (see <see cref="AlundraEntityScriptProxy.ApplyGravitySettingsToController"/>'s own
    /// no-controller no-op).
    /// </summary>
    private TileMapData? _tileMapData;

    /// <summary>
    /// Camera instance wiring (S2, docs/plan-update-caracterisation.md) - built in this FIELD
    /// INITIALIZER, never lazily and never handed a back-reference to this proxy (trap 9: <see cref="Clone"/>
    /// returns a bare <c>new AlundraWorldProxy()</c> and copies nothing, which stays safe only while every
    /// collaborator is constructed exactly this way). Internal (rather than private) purely so
    /// AlundraWorldProxyUpdateCharacterizationTests' <c>SeedDebugCameraOffset</c> helper can reach this
    /// instance directly - the private-&gt;internal widening the plan's base extraction rule already
    /// permits - instead of adding a second reflection hop.
    /// </summary>
    internal readonly AlundraCameraDirector _cameraDirector = new();

    /// <summary>
    /// E5.a: port of <c>g_entityFollowedByCamera</c> (GameEngine.cs) - see
    /// <see cref="IEntityWorldContext.EntityFollowedByCamera"/>'s own doc. Initialized to the hero at
    /// pawn adoption (port of <c>GameEngine.cs:644</c>, <see cref="AdoptPlayerPawn"/>), then only ever
    /// written by opcodes 0x67/0x68/0x69 (<see cref="AlundraEventProgramRunner"/>).
    /// </summary>
    public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

    /// <summary>E5.a: <see cref="IEntityWorldContext.SetForcedCameraLookAt"/> - port of opcode 0x69
    /// (Script_105_069). Public (rather than explicit-interface) since nothing about it needs hiding from
    /// this proxy's own surface, unlike <see cref="PlayerEntity"/>'s internal setter above. Clears
    /// <see cref="EntityFollowedByCamera"/> (which stays on this proxy) and DELEGATES the look-at pixel
    /// coordinates themselves to <see cref="AlundraCameraDirector.SetForcedLookAt"/> (S2,
    /// docs/plan-update-caracterisation.md) - see that collaborator's own doc on why this delegation is
    /// not the banned "facade".</summary>
    public void SetForcedCameraLookAt(int x, int y, int z)
    {
        EntityFollowedByCamera = null;
        _cameraDirector.SetForcedLookAt(x, y, z);
    }

    /// <summary>
    /// True once <see cref="InitializeWithWorld"/> successfully parsed and applied this world's
    /// <see cref="WallPlacementOverlay"/> (see <see cref="WallPlacementOverlay.CustomPropertyKey"/>).
    /// Gates the per-entity <see cref="WallPlacementOverlay.ApplyEntitySortKey"/> call in
    /// <see cref="AlundraFrameSyncPasses.RunAnimationSyncPass"/>'s caller (<see cref="Update"/>): with no wall placements loaded
    /// there is nothing to interleave against, so entities keep whatever
    /// <see cref="DepthSortable2DComponent"/> defaults their prefab already carries.
    /// </summary>
    private bool _wallPlacementOverlayApplied;

    /// <summary>
    /// This world's <see cref="AlundraCellsCollisionField"/>, built once in
    /// <see cref="InitializeWithWorld"/> from the loaded <see cref="TileMapData"/>'s "AlundraCells"
    /// custom property and installed as <c>World.CollisionField</c> (also exposed here for tests -
    /// null in degraded mode, see <see cref="AlundraCellsRecords.TryParse"/>).
    /// </summary>
    public AlundraCellsCollisionField? CollisionField { get; private set; }

    /// <summary>
    /// This world's navigation grid (E4.d, docs/plan-e4-deplacement-scripte.md decision E4-2), built once
    /// in <see cref="InitializeWithWorld"/> right after <see cref="CollisionField"/> from the SAME
    /// <see cref="TileMapData"/> - see <see cref="TryBuildNavigationGrid"/>'s own doc. Null in degraded
    /// mode (missing navigation layer or tileset-resolution failure) - satisfies
    /// <see cref="IEntityWorldContext.NavigationGrid"/> implicitly (this class already implements that
    /// interface).
    /// </summary>
    public NavigationGrid2D? NavigationGrid { get; internal set; }

    /// <summary>
    /// This world's cell-mutation seam (E7.a, docs/plan-e7-mutation-tuiles.md) - a real
    /// <see cref="AlundraCellStore"/> built from the SAME parsed <see cref="AlundraCellsRecords"/>
    /// <see cref="CollisionField"/> aliases its own arrays from (<see cref="InstallCellAndOverlaySystems"/>),
    /// satisfying <see cref="IEntityWorldContext.CellMutator"/>. Null in degraded mode (missing/malformed
    /// "AlundraCells" property, or a tile_id/wall_tiles_offset column length mismatch) - the interpreter's
    /// own null-mutator fallback then applies (skip by size, <c>Degraded</c> trace kind).
    /// </summary>
    public IAlundraCellMutator? CellMutator { get; private set; }

    /// <summary>
    /// This world's sound-effect playback seam (E11.a, docs/plan-e11-audio.md) - a real
    /// <see cref="AlundraSoundPlayer"/> over <c>world.Game.AudioSystemComponent.Service</c>, installed by
    /// <see cref="InstallAudioSystems"/>. Null without a <c>Game</c> (e.g. a world built without one in a
    /// test) - the interpreter's own null-player fallback then applies (skip by size, <c>Degraded</c>
    /// trace kind), same shape as <see cref="CellMutator"/> above.
    /// </summary>
    public IAlundraSoundPlayer? SoundPlayer { get; private set; }

    /// <summary>
    /// This session's background-music playback seam (docs/plan-e11c-musique.md, slice C1) - the
    /// session-scoped <see cref="AlundraMusicPlayer.Instance"/> (D-C-6, see that class's own doc), NOT
    /// a per-world instance like <see cref="SoundPlayer"/> above. Null without a <c>Game</c> (same
    /// degraded shape as <see cref="SoundPlayer"/>), installed by <see cref="InstallAudioSystems"/>.
    /// </summary>
    public IAlundraMusicPlayer? MusicPlayer { get; private set; }

    /// <summary>
    /// This session's screen fade/tint seam (E10.b, docs/plan-e10-fondu.md, D-E10-6) - the SESSION-scoped
    /// <see cref="AlundraScreenFadeDirector.Instance"/> (see that class's own doc), NOT a per-world
    /// instance. Always non-null (unlike <see cref="SoundPlayer"/>/<see cref="MusicPlayer"/>): attaching
    /// to a <c>null</c> engine service is itself a tolerated, tested state (T2) - the singleton is
    /// installed by <see cref="InstallScreenFadeSystems"/> regardless of whether this world has a
    /// <c>Game</c>.
    /// </summary>
    public IAlundraScreenFadeDirector ScreenFadeDirector => AlundraScreenFadeDirector.Instance;

    /// <summary>
    /// Seam over <c>Sounds/sfx-manifest.json</c> lookups (see <see cref="Alundra.Scripts.AlundraSoundBank"/>'s
    /// class doc), read once and reused by <see cref="SoundPlayer"/> for every sfx it resolves. Internal,
    /// not injected through the constructor - same reasoning as <see cref="SpriteRecordCatalog"/> above.
    /// </summary>
    internal AlundraSoundBank SoundBank = new AlundraSoundBank();

    /// <summary>
    /// E7.b (docs/plan-e7-mutation-tuiles.md): the visual + navigation applier subscribed to
    /// <see cref="CellMutator"/>'s (as an <see cref="AlundraCellStore"/>) own
    /// <see cref="AlundraCellStore.CellsMutated"/> event - see <see cref="AlundraCellVisualSync"/>'s own
    /// class doc. Null when <see cref="CellMutator"/> is (no cell store installed - nothing to make
    /// visible), or when the wall/floor placement overlay itself was never applied.
    /// </summary>
    private AlundraCellVisualSync? _cellVisualSync;

    /// <summary>Test-only accessor (E7.b, docs/plan-e7-mutation-tuiles.md, acceptance items 1-8): lets a
    /// test drive <see cref="AlundraCellVisualSync.FlushPendingOverlayReconstruction"/> and read
    /// <see cref="AlundraCellVisualSync.ReconstructionCount"/> after mutating through <see cref="CellMutator"/>,
    /// without exposing either on the public surface.</summary>
    internal AlundraCellVisualSync? CellVisualSync => _cellVisualSync;

    /// <summary>
    /// Rendering instance wiring (S3, docs/plan-update-caracterisation.md) - built in this FIELD
    /// INITIALIZER, never lazily and never handed a back-reference to this proxy (trap 9: <see cref="Clone"/>
    /// returns a bare <c>new AlundraWorldProxy()</c> and copies nothing, which stays safe only while every
    /// collaborator is constructed exactly this way).
    /// </summary>
    internal readonly AlundraBackdropStage _backdropStage = new();

    /// <summary>Bug fix (see <see cref="AlundraLogicClock"/>'s own class doc for the full diagnosis): this
    /// world's ONE shared 50 Hz logic clock - every spawned entity's own <see cref="AlundraEntityScriptProxy.Update"/>
    /// and this proxy's own <see cref="Update"/> read <see cref="LogicTicksThisFrame"/> off this SAME
    /// instance, so they always agree on how many logic ticks happened this rendered frame.</summary>
    private readonly AlundraLogicClock _logicClock = new();

    /// <summary>
    /// Bug fix (docs/plan-camera-premiere-frame.md §3, point 1): on a free time-step engine faster than
    /// 50 Hz, the very first rendered frame can carry ZERO raw logic ticks (the accumulator has not yet
    /// reached <see cref="AlundraScriptedMotion.FixedTickSeconds"/>) - so neither the map-events loop nor
    /// the camera's armed first-frame snap (<see cref="AlundraCameraDirector.ArmFirstFrameSnap"/>) ever
    /// runs before that frame's own camera resolve, and the one snap is spent on the player's raw spawn
    /// pose instead of wherever the intro's own opening script retargets the camera one instruction
    /// later.
    ///
    /// Guarantees at least ONE tick on this world's very first frame - applied INSIDE
    /// <see cref="LogicTicksThisFrame"/> itself (every caller, entity or proxy, already funnels through
    /// it), not as a flag <see cref="Update"/> alone consumes: <see cref="AlundraLogicClock"/>'s own memo
    /// caches the RAW count on the FIRST call of the frame and every later call this same frame is a pure
    /// cache read (<see cref="AlundraLogicClock.TicksThisFrame"/>), and the engine updates every entity
    /// BEFORE this world's own <see cref="Update"/> - so a floor consumed only by the first caller would
    /// hand a tick to whichever entity happens to call first and leave every later caller (every other
    /// entity, and this proxy's own read) at the RAW, un-floored count, permanently splitting the world's
    /// tick count from the entities' - exactly the desync <see cref="AlundraLogicClock"/>'s own class doc
    /// gives as the reason to share ONE clock in the first place. So this flag is instead STICKY across
    /// the whole open frame - applied on EVERY call while <see langword="true"/> - and cleared exactly
    /// once, at the frame-close site right next to <see cref="_logicClock"/>'s own <c>CloseFrame()</c>
    /// call in <see cref="Update"/>. <see cref="AlundraLogicClock"/> itself is untouched (plan §2: both
    /// golden trace harnesses build their own instance of it and pin exact tick counts against it).
    /// </summary>
    private bool _firstFrameStillOpen = true;

    /// <summary>See <see cref="IAlundraScriptHost.LogicTicksThisFrame"/>'s own doc; the sticky first-frame
    /// floor is documented on <see cref="_firstFrameStillOpen"/> itself.</summary>
    public int LogicTicksThisFrame(float elapsedTime)
    {
        var ticks = _logicClock.TicksThisFrame(elapsedTime);
        return _firstFrameStillOpen ? Math.Max(ticks, 1) : ticks;
    }

    public override void InitializeWithWorld(World world)
    {
        _world = world;

        // E5.a (decision E5-2): port of GraphicManager.cs's own g_isCameraScrolling = 1 at map load - the
        // next UpdateCameraFollow call snaps straight to that frame's look-at instead of scrolling in from
        // the engine's default camera Target. Requalified to the camera director (S2's extended proof
        // rule delta (b)) since that flag moved there.
        _cameraDirector.ArmFirstFrameSnap();

        // The engine enables its physics debug wireframes by default (PhysicsDebugViewRendererComponent
        // .DisplayPhysics = true), which draws every kinetic body box - one white rectangle per spawned
        // entity with a body - over the game. Off for normal play; the Back button toggles it back on
        // (see UpdateDebugCameraPan) to inspect collision boxes while flying the debug camera.
        if (world.Game?.PhysicsDebugViewRendererComponent != null)
        {
            world.Game.PhysicsDebugViewRendererComponent.DisplayPhysics = false;
        }

        // Loads this world's own event-code document (see MapEventProgramLoader's class doc on path
        // resolution) and wires the real slot-A interpreter over it; null document means "not found /
        // failed to parse" and AlundraEventProgramRunner degrades to a counted no-op for slot A too, the
        // same shape as SpriteRecordCatalog's own degraded mode.
        var eventProgramDocument = MapEventProgramLoader.Load(EngineEnvironment.ProjectPath, world.Name);
        EventProgramRunner = new AlundraEventProgramRunner(eventProgramDocument, GameState, this);

        // Scrolling background layers (see BackdropRenderer's class doc) - same degraded-mode shape as
        // the event-program document above: a world with no companion file (most of them - Scroll
        // Parameters.Infos.Enabled was false) simply renders nothing extra.
        // S3 (docs/plan-update-caracterisation.md): _backdropRenderer now lives on _backdropStage
        // (requalified field access, extended proof rule delta (b), same shape as S2's
        // _cameraDirector.ArmFirstFrameSnap()) - this Load call needs a live GraphicsDevice, so it stays
        // here rather than moving into the stage's own members.
        _backdropStage.Load(world, EngineEnvironment.ProjectPath);

        var tileMapEntity = world.Entities.FirstOrDefault(entity => entity.Name == TileMapEntityName);
        if (tileMapEntity == null)
        {
            Logs.WriteWarning($"AlundraWorldProxy: no '{TileMapEntityName}' entity found in world '{world.Name}'; no entity spawned.");
            return;
        }

        var tileMapComponent = tileMapEntity.GetComponent<TileMapComponent>();
        var tileMapData = tileMapComponent?.TileMapData;
        if (tileMapData == null)
        {
            Logs.WriteWarning($"AlundraWorldProxy: entity '{TileMapEntityName}' has no loaded TileMapData in world '{world.Name}'; no entity spawned.");
            return;
        }

        _tileMapData = tileMapData;

        InstallCellAndOverlaySystems(world, tileMapComponent!, tileMapData);
        InstallAudioSystems(world);
        InstallScreenFadeSystems(world);

        var entitiesLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == EntitiesLayerName);
        var portalsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == PortalsLayerName);
        var mapEventsLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == MapEventsLayerName);

        Logs.WriteInfo(
            $"AlundraWorldProxy: world '{world.Name}' object layers - "
            + $"{EntitiesLayerName}={entitiesLayer?.Objects.Count ?? 0}, "
            + $"{PortalsLayerName}={portalsLayer?.Objects.Count ?? 0}, "
            + $"{MapEventsLayerName}={mapEventsLayer?.Objects.Count ?? 0}.");

        // E2: register the "AlundraButtons" input mappings once per game (idempotent across world
        // reloads - see AlundraPlayerController.EnsureInputMappingsRegistered's own doc), before any
        // entity's first Update ever reads them.
        AlundraPlayerController.EnsureInputMappingsRegistered(world.Game);

        // E2: the engine itself already spawned and possessed the hero pawn (World.LoadContent ->
        // InitializePlayerControllers, strictly before this GameplayProxy's own InitializeWithWorld runs -
        // see AdoptPlayerPawn's own doc) - adopt it and apply the New Game logical state (port of
        // ResetEntityState/InitializeEntitySlots' own spawn order, GameEngine.cs:626-643: the player exists
        // BEFORE any record is spawned - the spawn-zone gate below reads its tile) instead of spawning a
        // second, separate hero entity ourselves.
        AdoptPlayerPawn(world, tileMapData);

        // MapEvents (port of InitializeMapEvents, GameEngine.cs:476-583) - always against PlayerEntity;
        // empty when there is none (see PlayerEntity's own doc).
        BuildMapEvents(mapEventsLayer);

        if (entitiesLayer == null)
        {
            return;
        }

        var skippedCount = 0;

        foreach (var record in entitiesLayer.Objects)
        {
            // Indexed for the whole world's lifetime regardless of whether this record spawns right now
            // (see _entityRecordsByIndex's own doc) - 0x2D's looser gate can still spawn it later.
            if (AlundraEntitySpawnFactory.TryGetRecordInt(record, "Index", out var recordIndex))
            {
                _entityRecordsByIndex[recordIndex] = record;
            }

            bool shouldSpawn;
            string skipReason;
            if (PlayerEntity != null)
            {
                shouldSpawn = AlundraEntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone: false, PlayerEntity.TileX, PlayerEntity.TileY, out skipReason);
            }
            else
            {
                shouldSpawn = AlundraEntitySpawnFactory.ShouldSpawnRecord(record, out skipReason);
            }

            if (!shouldSpawn)
            {
                skippedCount++;
                Logs.WriteDebug($"AlundraWorldProxy: record '{record.Name}' not spawned ({skipReason}).");
                continue;
            }

            try
            {
                var entity = AlundraEntitySpawnFactory.CreateEntityFromRecord(
                    record, guid => world.Game.AssetContentManager.Load<Entity>(guid), SpriteRecordCatalog,
                    tileMapData: _tileMapData);
                var spawnedProxy = entity.GameplayProxy as AlundraEntityScriptProxy;
                if (spawnedProxy != null)
                {
                    spawnedProxy.ScriptHost = this;
                }

                world.AddEntity(entity);
                _spawnedEntities.Add(entity);

                // E4.b ("Spawn" item, docs/plan-e4-deplacement-scripte.md): ground-clamp + root push for a
                // controller-driven NPC now that the entity is actually IN the world - ClampToGround needs
                // World.CollisionField, only reachable once Entity.World is set (World.AddEntity, just
                // above), strictly AFTER CreateEntityFromPrefab's own spawn-time root write (which had to
                // run off the un-clamped PosZ). A no-op without a Controller
                // (PushLogicalPositionToRoot's own gate), same as every other entity today.
                spawnedProxy?.PushLogicalPositionToRoot();

                // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): ONE support evaluation right
                // at spawn - see BuildCollidablesSnapshot's own doc on why platform records (0-5) are
                // already present here for a rider record (11+) to land on.
                spawnedProxy?.EvaluateEntitySupport(BuildCollidablesSnapshot(), immediateAtSpawn: true);
            }
            catch (Exception ex)
            {
                Logs.WriteWarning(
                    $"AlundraWorldProxy: failed to spawn entity for record '{record.Name}' in world '{world.Name}'; "
                    + $"skipping. {ex.Message}");
            }
        }

        // E4.f: seed _updateProxies/_collidables from the fully-spawned load-time entity set BEFORE the
        // engine's own first Update cycle ever runs - see RefreshUpdateProxiesAndCollidables's own doc for
        // why this second call (beyond the per-frame one in Update) matters.
        RefreshUpdateProxiesAndCollidables();

        if (skippedCount > 0)
        {
            Logs.WriteInfo(
                $"AlundraWorldProxy: world '{world.Name}' - {skippedCount} of {entitiesLayer.Objects.Count} "
                + "Entities records not spawned (see ShouldSpawnRecord).");
        }
    }

    /// <summary>
    /// Converts an entity's logical spawn position (<see cref="AlundraEntityScriptProxy.PosX"/> /
    /// <see cref="AlundraEntityScriptProxy.PosY"/> / <see cref="AlundraEntityScriptProxy.PosZ"/>, 16.16
    /// fixed-point Alundra pixels - see <see cref="EntityRecordMapper"/>) into the LOGICAL pose written
    /// onto the entity root's <c>LocalTransform.Position</c> (E3.a, docs/plan-e3-collisions.md decision
    /// E3-1) - consistently with <c>WorldWriter.ResolveTileCentreSpawn</c> (the converter's own
    /// tile-to-logical-pose conversion, used for the PlayerStart):
    /// <list type="bullet">
    /// <item><description><c>X = pixelX</c> (no conversion - CasaEngine's X already matches Alundra's).</description></item>
    /// <item><description><c>Y = pixelY</c>: Alundra's own down-positive depth, NOT flipped here - the
    /// flip from depth to a Y-up render position is now the render policy's job
    /// (<c>SimulationSpacePolicy.DeriveRenderPosition</c> under the world's TopDownElevation policy),
    /// applied every frame by the <c>RenderProjectionComponent</c> child a prefab's root now carries
    /// (<c>SpriteWriter.WriteEntityPrefab</c>), not baked into this snapshot.</description></item>
    /// <item><description><c>Z = elevationPixels</c> (<c>PosZ &gt;&gt; 16</c>): Alundra's elevation, kept
    /// on the logical Z axis rather than folded into Y - again the render policy's job to translate into
    /// a screen offset.</description></item>
    /// </list>
    /// This is a spawn-time snapshot of a logical position that can change at runtime (movement, event
    /// programs); every caller that writes it onto the root MUST also re-run the entity's
    /// <c>RenderProjectionComponent.UpdateProjection()</c> in the same frame so the sprite renders the
    /// new pose immediately rather than one frame late (component update order:
    /// <c>RootComponent.Update</c>, hence the projection, runs before <c>GameplayProxy.Update</c> -
    /// Entity.cs:473-504) - see <see cref="AlundraFrameSyncPasses.SyncTransform"/>.
    /// </summary>
    /// <summary>
    /// E7.b (docs/plan-e7-mutation-tuiles.md, "Testabilité du câblage", plan fact 17): the block that used
    /// to live inline in <see cref="InitializeWithWorld"/> - collision field, cell store/
    /// <see cref="CellMutator"/>, navigation grid, and the wall/floor placement overlay - extracted into
    /// its own INTERNAL method so a test can drive it directly against a hand-built <c>World</c> +
    /// <see cref="TileMapComponent"/> (no live <c>CasaEngineGame</c>/asset catalog required - the only
    /// piece that needs one, <see cref="TryBuildNavigationGrid"/>, already degrades to null without one).
    /// <see cref="InitializeWithWorld"/> is now a thin caller; acceptance tests (docs/plan-e7-mutation-tuiles.md,
    /// slice E7.b) call this method directly - the SAME production path, not a hand-rolled store.
    ///
    /// Builds <see cref="AlundraCellVisualSync"/> and subscribes it to the freshly-built
    /// <see cref="AlundraCellStore"/>'s own <see cref="AlundraCellStore.CellsMutated"/> event - THIS
    /// subscription line is what makes 0x54/0x55/0x85 visible/routable at runtime (acceptance item 1's own
    /// mutation: deleting it must fail that test). Skipped entirely when no cell store could be built (no
    /// "AlundraCells" property, or a tile_id/wall_tiles_offset length mismatch) - degraded mode, same as
    /// E7.a's own no-mutator fallback.
    /// </summary>
    internal void InstallCellAndOverlaySystems(World world, TileMapComponent tileMapComponent, TileMapData tileMapData)
    {
        // E3.b: build this world's ground/walkability field from the same TileMapData and install it on
        // World.CollisionField - World.Clear() resets the slot to null (World.cs), so every load
        // re-installs it here. Tolerant by design, like the wall/floor placement overlays right below:
        // a missing/malformed "AlundraCells" property (or a cell_count that does not match MapSize)
        // just leaves World.CollisionField null (degraded mode, single warning already logged by
        // AlundraCellsCollisionField.TryCreate) - E3.c's mover then has no field to sample.
        //
        // E7.a/E7.b: the 4-out overload also hands back the parsed AlundraCellsRecords, so the cell store
        // built right below aliases the SAME int[] instances this field reads - a mutation is instantly
        // visible to both.
        AlundraCellsRecords? cellRecords = null;
        if (AlundraCellsCollisionField.TryCreate(tileMapData, world.Name, out var collisionField, out cellRecords))
        {
            CollisionField = collisionField;
            world.CollisionField = collisionField;
        }
        else
        {
            CollisionField = null;
        }

        // E4.d (docs/plan-e4-deplacement-scripte.md, decision E4-2): navigation grid, built from the same
        // TileMapData right after the collision field above - see TryBuildNavigationGrid's own doc.
        NavigationGrid = TryBuildNavigationGrid(world, tileMapData);

        // E7.a: the cell-mutation store, built from the SAME parsed records the collision field above
        // aliases its arrays from - see AlundraCellStore's own class doc. Degraded (null CellMutator) on
        // a tile_id/wall_tiles_offset length mismatch, or when the collision field itself never parsed.
        CellMutator = null;
        AlundraCellStore? cellStore = null;
        if (cellRecords != null
            && AlundraCellStore.TryCreate(cellRecords, tileMapData.MapSize.Width, tileMapData.MapSize.Height, world.Name, out cellStore))
        {
            CellMutator = cellStore;
        }

        // Wall/sprite depth interleave (Slice B): strip every baked wall tile the converter recorded
        // out of the flat "Render_*" layers and resubmit it through the tile map's runtime sorted
        // overlay, so it draws ordered against Y-sorted entity sprites instead of always flat. Tolerant
        // by design - see WallPlacementOverlay.TryParse's doc comment - so a world with no (or a
        // malformed) "AlundraWallPlacements" property still spawns its entities normally, just without
        // the interleave.
        WallPlacementRecords? wallPlacements = null;
        IReadOnlyList<int> submittedWallIndices = Array.Empty<int>();
        if (WallPlacementOverlay.TryParse(tileMapData.CustomProperties, world.Name, out wallPlacements))
        {
            submittedWallIndices = WallPlacementOverlay.Apply(tileMapComponent, wallPlacements, world.Name);
            _wallPlacementOverlayApplied = true;
        }

        // Same interleave for elevated (Height > 0) floor tiles, through the same runtime sorted
        // overlay - see WallPlacementOverlay.ComputeFloorSortKey's doc for why a floor's own row bias
        // (slot 0..5, no +7) already orders it correctly against both walls and Y-sorted entities.
        // Independently tolerant, like the wall property above: a world with no (or malformed)
        // "AlundraFloorPlacements" property still spawns normally, just without this interleave.
        FloorPlacementRecords? floorPlacements = null;
        IReadOnlyList<int> submittedFloorIndices = Array.Empty<int>();
        if (WallPlacementOverlay.TryParseFloor(tileMapData.CustomProperties, world.Name, out floorPlacements))
        {
            submittedFloorIndices = WallPlacementOverlay.ApplyFloor(tileMapComponent, floorPlacements, world.Name);
        }

        // E7.b: the visual + navigation applier - see AlundraCellVisualSync's own class doc. Seeded from
        // exactly what was resubmitted above (submittedWallIndices/submittedFloorIndices), never from the
        // documents themselves (plan fact 13). The navigation grid accessor reads THIS property live
        // (`() => NavigationGrid`), not a captured snapshot, so a test injecting a synthetic grid after
        // this call still gets it picked up by navigation sync.
        _cellVisualSync = null;
        if (cellStore != null)
        {
            _cellVisualSync = AlundraCellVisualSync.Create(
                tileMapComponent, cellStore, tileMapData.MapSize.Width, tileMapData.MapSize.Height, world.Name,
                tileMapComponent.TileSetData,
                wallPlacements, submittedWallIndices,
                floorPlacements, submittedFloorIndices,
                () => NavigationGrid);

            cellStore.CellsMutated += _cellVisualSync.OnCellsMutated;
        }
    }

    /// <summary>
    /// E11.a (docs/plan-e11-audio.md, D-E11-1/D-E11-2): installs <see cref="SoundPlayer"/> - a real
    /// <see cref="AlundraSoundPlayer"/> over <c>world.Game.AudioSystemComponent.Service</c> (the same
    /// engine-owned <see cref="AudioService"/> every other production audio call site resolves off of,
    /// e.g. <c>CutsceneActionCoroutineFactory.GetAudioService</c>/<c>SoundEmitterComponent</c>'s own
    /// <c>world?.Game?.AudioSystemComponent?.Service</c>). Extracted as its own INTERNAL method
    /// (same precedent as <see cref="InstallCellAndOverlaySystems"/>) so a test can drive it directly.
    /// Left null without a <c>Game</c> (a world built without one) - the interpreter's own null-player
    /// fallback then applies, same degraded shape every other missing-system seam in this DLL already
    /// has.
    /// </summary>
    internal void InstallAudioSystems(World world)
    {
        SoundPlayer = null;
        MusicPlayer = null;

        var audioService = world.Game?.AudioSystemComponent?.Service;
        if (audioService != null)
        {
            // D-C-5: owner: world, so World.Clear's own StopVoicesOwnedBy(world) actually stops these
            // voices (fact 1.7's fix - see AlundraSoundPlayer's own constructor doc).
            SoundPlayer = new AlundraSoundPlayer(audioService, SoundBank, world);

            // D-C-6: AlundraMusicPlayer.Instance is SESSION-scoped, never rebuilt here - only
            // re-pointed at this world's own AudioService/project path. See that class's own doc for
            // why (the guard state would be vacuous by construction in a per-world instance).
            AlundraMusicPlayer.Instance.AttachToWorld(audioService, EngineEnvironment.ProjectPath);
            MusicPlayer = AlundraMusicPlayer.Instance;
        }

        // The map-entry music start lives HERE rather than at a second call site in
        // InitializeWithWorld, and that placement is the point: an outcome-verifier of slice C1 showed
        // that deleting a separate `TriggerMapEntryMusic(world);` line left all 637 tests green - the
        // whole slice would have gone inert in the real game with a fully green suite. This method is
        // already driven end-to-end by AlundraWorldProxyAudioInstallationTests on the real install
        // path, so folding the trigger into it puts the last wiring link under test instead of leaving
        // it to the in-game check alone. Ordering is unaffected: the original starts the BGM at the end
        // of its own map-entry block, but the start depends on nothing this method runs after.
        TriggerMapEntryMusic(world);
    }

    /// <summary>
    /// E10.b (docs/plan-e10-fondu.md, D-E10-7): installs the screen fade/tint seam - re-points the
    /// SESSION-scoped <see cref="AlundraScreenFadeDirector.Instance"/> at this world's own
    /// <see cref="CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectService"/>
    /// (<c>world.Game?.ScreenEffectComponent?.Service</c> - null without a <c>Game</c>, tolerated, T2),
    /// THEN arms effect 0 for this map entry (<see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>).
    /// Called from <see cref="InitializeWithWorld"/> - the ONLY call site (D-E10-7's own M16 lesson: no
    /// separate, independently deletable call site) - and extracted as its own INTERNAL method, same
    /// precedent as <see cref="InstallAudioSystems"/>, so a test can drive it directly against a world
    /// with no <c>Game</c> (T2) or against two successive worlds sharing the same session (T7).
    ///
    /// <b>Pushes nothing to the service</b> (see <see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>'s
    /// own doc) - the first push happens from <see cref="Update"/>, after that same frame's
    /// <see cref="AlundraScreenFadeDirector.Advance"/> call.
    /// </summary>
    internal void InstallScreenFadeSystems(World world)
    {
        AlundraScreenFadeDirector.Instance.AttachToWorld(world.Game?.ScreenEffectComponent?.Service);
        AlundraScreenFadeDirector.Instance.InstallForMapEntry();
    }

    /// <summary>
    /// docs/plan-e11c-musique.md, slice C1, item 4: the equivalent of the original's own
    /// <c>LoadMapSounds</c> map-entry call (fact 1.4: the second-to-last instruction of the map-entry
    /// block, right before the first <c>Update</c>) - this world's own map id, read the same way
    /// <see cref="BackdropLoader"/>/<see cref="MapEventProgramLoader"/> already do (trailing "-{mapId}"
    /// of <see cref="World.Name"/>), a no-op when the name carries none (not a converted Alundra map
    /// world) or when <see cref="MusicPlayer"/> was never installed (no <c>Game</c> - degraded, same
    /// shape as every other missing-system seam in this DLL). Internal so a test can drive it directly
    /// (same precedent as <see cref="InstallAudioSystems"/>/<see cref="InstallCellAndOverlaySystems"/>).
    /// </summary>
    private void TriggerMapEntryMusic(World world)
    {
        if (MusicPlayer == null)
        {
            return;
        }

        if (!BackdropLoader.TryParseMapIndex(world.Name, out var mapId))
        {
            return;
        }

        MusicPlayer.PlayMapMusic(mapId);
    }

    /// <summary>
    /// This map's own Gravity/ZViscosity (<c>TileMapData.CustomProperties</c>, written by
    /// <c>CellMetadataWriter.ConvertMap</c>), converted to the units
    /// <see cref="CharacterControllerSettings.Gravity"/>/<see cref="CharacterControllerSettings.MaxFallSpeed"/>
    /// expect - E3.d's own formula (<c>AdoptPlayerPawn</c>'s original override block): <c>mapGravity*256/
    /// 65536*2500</c> / <c>mapZViscosity*256/65536*50</c> (1250/800 on map 389). Shared by
    /// <see cref="AdoptPlayerPawn"/> (hero, unconditional) and <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/> (NPC,
    /// gated on the Gravity flag bit via <see cref="AlundraEntityScriptProxy.ApplyGravitySettingsToController"/>)
    /// so the formula itself lives in exactly one place. Float arithmetic throughout: an integer
    /// <c>128*256/65536</c> truncates to 0 before the final <c>*2500</c> ever runs.
    /// </summary>
    /// <summary>
    /// E4.d (docs/plan-e4-deplacement-scripte.md, decision E4-2): builds this world's navigation grid
    /// from the SAME <paramref name="tileMapData"/> <see cref="InitializeWithWorld"/>'s collision field
    /// just read, right after it. Tilesets are resolved in <c>tile_set_asset_ids</c> order - the exact
    /// same order/lookup <c>TileMapComponent.LoadTileSets</c> uses at runtime
    /// (CasaEngineMonogame/CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs:819-850:
    /// index <see cref="TileMapData.TileSetDataAssetIds"/> by position, <c>Load&lt;TileSetData&gt;</c>
    /// each through the world's own <c>AssetContentManager</c>) - required so the navigation layer's own
    /// per-tile <c>tileSourceIndex</c> lines up with <paramref name="tileSets"/>' own indices exactly the
    /// way <see cref="NavigationGrid2D.TryCreateFromTileMap"/> expects
    /// (<c>tileSets[tileSourceIndex]</c>). <c>cellSize</c> is always 1f: the DLL consumes the grid purely
    /// in "cell space" and does its own px&lt;-&gt;cell conversion (24x16 - see
    /// <see cref="AlundraEventProgramRunner"/>'s own 0x1E walk-detour helpers), since the engine's
    /// <see cref="CasaEngine.Framework.AI.Navigation.CharacterControllerNavigationDriverComponent"/> is
    /// deliberately NOT used in E4 (its <c>(X, -Z)</c> intent axis is hard-coded, E4-2). Degraded mode -
    /// null, one warning - on a missing navigation layer (an older/regenerated export that predates E4.a)
    /// or any tileset-resolution failure (missing asset id, catalog miss), the same tolerant shape as
    /// <see cref="AlundraCellsCollisionField.TryCreate"/> right above this method's own call site.
    /// </summary>
    private static NavigationGrid2D? TryBuildNavigationGrid(World world, TileMapData tileMapData)
    {
        List<TileSetData> tileSets;
        try
        {
            tileSets = new List<TileSetData>(tileMapData.TileSetDataAssetIds.Count);
            foreach (var assetId in tileMapData.TileSetDataAssetIds)
            {
                tileSets.Add(world.Game.AssetContentManager.Load<TileSetData>(assetId));
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: world '{world.Name}' - failed to resolve its tilesets for "
                + $"navigation grid construction ({ex.Message}); navigation disabled (degraded mode).");
            return null;
        }

        if (NavigationGrid2D.TryCreateFromTileMap(tileMapData, tileSets, cellSize: 1f, out var grid))
        {
            return grid;
        }

        Logs.WriteWarning(
            $"AlundraWorldProxy: world '{world.Name}' has no navigation layer ('{NavigationGrid2D.NavigationRoleProperty}' "
            + $"= '{NavigationGrid2D.NavigationRoleGrid}'); navigation disabled (degraded mode).");
        return null;
    }

    /// <summary>
    /// E2 replacement for the old <c>SpawnPlayerEntity</c> (which used to clone a SECOND hero prefab
    /// itself): the ENGINE now spawns the hero pawn and possesses it with an <see cref="AlundraPlayerController"/>
    /// (<c>World.LoadContent</c> -&gt; <c>InitializePlayerControllers</c>, CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:221-252/282-297,
    /// strictly before <c>CreateGameplayProxy</c>/<c>InitializeWithWorld</c> run - so by the time this
    /// method runs, the pawn is already in <c>world.Entities</c>, already <c>Initialize</c>/
    /// <c>InitializeWithWorld</c>'d, and already positioned at the map's <c>PlayerStart</c> component by
    /// <c>CreateLocalPlayerController</c>, World.cs:350-367). This method only ADOPTS that pawn (finds its
    /// controller via <c>world.PlayerControllers</c>, World.cs:76) and applies the New Game LOGICAL state -
    /// same fields the old <c>SpawnPlayerEntity</c> set, a V1 port of <c>ResetEntityState</c>
    /// (GameEngine.cs:648-670), called BEFORE any "Entities" record is spawned (their own spawn-zone gate
    /// reads the player's tile): spawn position (New Game tile (33,59), tile-centre 16.16 fixed-point -
    /// GameInitializer.cs's New Game branch via <see cref="AlundraGameState"/>'s own New Game constants),
    /// <c>TargetAnimationId</c>/<c>TargetDirection</c> = <see cref="AlundraGameState.ResetAnimationId"/>/
    /// <see cref="AlundraGameState.ResetDirectionId"/> (54/"LoadingMap", 0/down), <c>Status = Normal</c>
    /// (NOT <c>Loaded</c> - GameEngine.cs:661: the hero has no Load program, unlike every record-spawned
    /// entity), <c>Flags</c>/<c>SpriteProgramIndexes</c>/<c>AnimSetsByAnim</c> from the hero's own
    /// sprite-records.json header when the catalog has one for it.
    ///
    /// Deviation note: the engine already positioned the pawn's transform at the <c>PlayerStart</c>
    /// component (logical pose (804, 952, 0) on map 389, equal to <c>ResolveLogicalPosition</c> of this same
    /// New Game tile) - this method's own <see cref="AlundraEntitySpawnFactory.ResolveLogicalPosition"/> call below OVERWRITES that
    /// with the logical position instead, which is harmless (same result) but makes explicit that
    /// <c>AlundraEntityScriptProxy</c>'s logical PosX/PosY/PosZ, not the engine's PlayerStart transform, is
    /// the field this proxy's own <see cref="AlundraFrameSyncPasses.SyncTransform"/> re-derives from every frame going forward
    /// (decision D2's "logical state wins" rule).
    ///
    /// Deliberately NOT ported (still out of E2's own scope - a real <c>InitializeGameState</c>/full
    /// <c>PlayerManager</c>): <c>Hp</c>/<c>HpMax</c> (<c>PlayerManager.GetPlayerHp/HpMax</c>),
    /// <c>g_activeCollisionEntity = null</c> (this world's own <see cref="ActiveCollisionEntity"/> already
    /// starts null), <c>g_currentWeaponFlags</c>/weapon item id, the warp timer/effect resets
    /// (<c>g_playerWarpTimer</c>, <c>g_isWarpDisabled</c>, <c>g_playerWarpEffect</c>,
    /// <c>g_playerEffectTransitionCooldown</c>, <c>ResetWarpLockTimer</c>) - none of these have any
    /// observable effect on E2's own ported <see cref="AlundraPlayerManager.MovePlayer"/> subset. No
    /// camera-follow yet (E5).
    /// </summary>
    private void AdoptPlayerPawn(World world, TileMapData tileMapData)
    {
        var playerController = world.PlayerControllers.OfType<AlundraPlayerController>().FirstOrDefault();
        _playerController = playerController;

        if (playerController?.Pawn == null)
        {
            if (!_loggedNoPlayerController)
            {
                _loggedNoPlayerController = true;
                Logs.WriteWarning(
                    $"AlundraWorldProxy: no {nameof(AlundraPlayerController)} possessing a pawn in world "
                    + $"'{world.Name}' (missing/misconfigured player_startup_settings_asset_id, "
                    + "player_controller_class, or default_pawn_asset_id); no player entity adopted, no "
                    + "fallback spawn.");
            }

            return;
        }

        var entity = playerController.Pawn;

        if (entity.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: the engine-spawned pawn in world '{world.Name}' did not produce an "
                + $"{nameof(AlundraEntityScriptProxy)} (GameplayProxyClassName on the pawn prefab); no "
                + "player entity adopted.");
            return;
        }

        proxy.IsPlayer = true;
        proxy.ScriptHost = this;
        proxy.LogicContextEntity = entity;
        proxy.ParentEntity = null;
        proxy.Status = EntityStatus.Normal;
        proxy.EntityRefId = -1; // not an "Entities" layer record - no slot to index by.
        proxy.EventTrigger = ScriptHelper.ProgramUnknown; // hygiene only - IsPlayer already excludes it from RunPendingEventTriggers regardless of value.

        // E3.d ("DLL - adoption", docs/plan-e3-collisions.md): resolved once here, before the New Game
        // pose write below, so AlundraEntityScriptProxy.ClampToGround (called right after that write)
        // and every later per-frame root/controller routing (AlundraEntityScriptProxy.Update,
        // AlundraPlayerManager.Tick, AlundraEntityScriptProxy.PushLogicalPositionToRoot) all see the
        // same cached reference. Null on a hero prefab that predates/skips E3.d's converter change -
        // every controller-aware site already falls back to E2's controller-free behaviour on null.
        proxy.Controller = entity.GetComponent<CharacterControllerComponent>();

        proxy.PosX = (AlundraGameState.CameraTileX * TileWidth + TileWidth / 2) << 16;
        proxy.PosY = (AlundraGameState.CameraTileY * TileHeight + TileHeight / 2) << 16;
        proxy.PosZ = 0;
        // E3.d: raises PosZ onto the actual cell height under the New Game spawn tile before anything
        // else reads it - port of EntityManager.cs:127-136's own spawn-time ground clamp (see
        // AlundraEntityScriptProxy.ClampToGround's own doc). A no-op without a controller/collision
        // field/Box fixture, so PosZ simply stays 0 exactly like before E3.d.
        proxy.ClampToGround();
        // PhysicsEngine.cs:1698-1700, same formula EntityRecordMapper seeds every record's own tile from.
        proxy.TileX = (proxy.PosX >> 16) / TileWidth;
        proxy.TileY = (proxy.PosY >> 16) / TileHeight;
        proxy.TileZ = proxy.PosZ >> 20;

        proxy.TargetAnimationId = AlundraGameState.ResetAnimationId;
        proxy.TargetDirection = AlundraGameState.ResetDirectionId;
        // EntityManager.cs:85-88 - bit-complemented so the very first per-frame animation sync always fires.
        proxy.CurrentAnimationId = ~AlundraGameState.ResetAnimationId;
        proxy.CurrentDirection = ~AlundraGameState.ResetDirectionId;

        // Documented stub for AlundraPlayerManager's faithful LoadingMap(0x36) port
        // (PlayerManager.cs:914-916: "if IsOnGround != 0, break" - i.e. stay in LoadingMap): only ever
        // read before this frame's own AlundraEntityScriptProxy.Update runs (E3.d has that method pull
        // a real Controller.IsGrounded reading here on, every frame, once the controller exists - see
        // its own doc). Pinning it to 1 here reproduces the ONE case that matters for a fresh New Game
        // spawn - a grounded hero - so MovePlayer's LoadingMap case takes the "stay" branch instead
        // of falling to the NOT-ported Jump case; the actual LoadingMap -> Idle exit is the animation
        // Chain bridge instead (anim 54 -> 0, see AlundraWorldProxy.OnAnimationFinished), matching the
        // original's own trailing-control-frame-driven animation switch rather than a ground check.
        proxy.IsOnGround = 1;

        var assetInfo = AssetCatalog.Get(HeroAssetName);
        if (assetInfo != null && SpriteRecordCatalog != null && SpriteRecordCatalog.TryGet(assetInfo.Id, out var header))
        {
            proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = header.ProgramLoad;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramBMap] = 0;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = header.ProgramTick;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramDTouch] = header.ProgramTouch;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = header.ProgramDeactivate;
            proxy.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = header.ProgramInteract;
            proxy.IdsvByAnimDirection = AlundraEntitySpawnFactory.BuildIdsvByAnimDirection(header.IdsvAnimDirs);
            proxy.AnimationEndByAnimDirection = AlundraEntitySpawnFactory.BuildAnimationEndByAnimDirection(header.IdsvAnimDirs);
            proxy.AnimSetsByAnim = header.AnimSets;
            // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): the hero's own logical
            // Mod*/Width/Height/Depth, same port (SetEntityDimensions, EntityManager.cs:160-199) every
            // record-spawned NPC already gets from ApplySpawnInitialization - needed so the hero counts as
            // a valid EntitySupport candidate/target (e.g. a future entity standing on the hero, or the
            // hero itself queried by EntitySearchService) with real dimensions instead of all-zero ones.
            AlundraEntitySpawnFactory.SetEntityDimensions(proxy, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);
        }
        else if (!_loggedNoHeroHeader)
        {
            _loggedNoHeroHeader = true;
            Logs.WriteDebug(
                $"AlundraWorldProxy: no sprite-records.json header found for the hero prefab in world "
                + $"'{world.Name}'; Flags/SpriteProgramIndexes/AnimSetsByAnim left at their defaults.");
        }

        // E3.d ("DLL - adoption", docs/plan-e3-collisions.md): overrides the converter-exported
        // Gravity/MaxFallSpeed/WalkabilityMask - the only three CharacterControllerSettings the
        // converter cannot bake in, since they depend on this MAP's own properties and this ENTITY's
        // own Flags, not on the hero prefab alone. Deliberately placed AFTER the Flags assignment
        // above: WalkabilityMaskFor(proxy.Flags) needs the real header flags, not the pre-adoption
        // default 0 (which would derive a masks-nothing-blocks mask regardless of the hero's real
        // ClassA/ClassB bits). Float arithmetic throughout (mapGravity/mapZViscosity are ints - an
        // integer 128*256/65536 truncates to 0 before the final *2500 ever runs).
        if (proxy.Controller != null)
        {
            var (mapGravity, mapMaxFallSpeed, mapGravityRaw, mapZViscosityRaw) = AlundraEntitySpawnFactory.ResolveMapGravitySettings(tileMapData);
            // E4 (docs/plan-echelles-chiffrage.md É4): stash the resolved values on the proxy itself, not
            // just on the live Controller.Settings below - AlundraPlayerManager's own climbing state
            // machine needs a RESERVE to restore the hero's engine-driven gravity to once a climb ends
            // (Controller.Settings.Gravity/MaxFallSpeed are zeroed WHILE climbing, see MovePlayer's own
            // Climbing/ClimbStill case). Before this fix, proxy.MapGravity/MapMaxFallSpeed were left at
            // their C# defaults (0f) for the hero - only ApplySpawnInitialization's own NPC path populated
            // them (see that method's own doc) - so "restore" would have restored to zero gravity forever,
            // exactly the gap docs/plan-echelles-chiffrage.md §7 risk 3 flags.
            proxy.MapGravity = mapGravity;
            proxy.MapMaxFallSpeed = mapMaxFallSpeed;
            proxy.MapGravityRaw = mapGravityRaw;
            proxy.MapZViscosityRaw = mapZViscosityRaw;

            var settings = proxy.Controller.Settings;
            settings.Gravity = mapGravity;
            settings.MaxFallSpeed = mapMaxFallSpeed;
            settings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(proxy.Flags);
        }
        else if (!_loggedNoHeroController)
        {
            _loggedNoHeroController = true;
            Logs.WriteWarning(
                $"AlundraWorldProxy: hero prefab in world '{world.Name}' has no "
                + $"{nameof(CharacterControllerComponent)}; falling back to E2's controller-free player "
                + "movement (no gravity, no field collision).");
        }

        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);

        // Overwrites the engine's own PlayerStart-derived transform - see this method's own doc
        // ("Deviation note") for why that is intentional, not redundant.
        if (entity.RootComponent != null)
        {
            entity.RootComponent.LocalTransform.Position = AlundraEntitySpawnFactory.ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);

            // The pawn is already in world.Entities by the time this method runs (see this method's
            // own doc), so - unlike CreateEntityFromPrefab's spawn-time call - this re-projection is
            // not a no-op: without it the sprite would keep showing the engine's PlayerStart-derived
            // render pose for one extra frame instead of the New Game logical pose just written above.
            proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();

            // E5.b (docs/plan-e5-camera.md): the hero is controller-driven exactly like a scripted NPC
            // (ApplySpawnInitialization's own E5.b block) and keeps the same float remainder every
            // frame, so it needs the same integer render snap. Set before the re-projection below so
            // the very first draw already snaps.
            if (proxy.RenderProjection != null)
            {
                proxy.RenderProjection.SnapToPixel = true;
            }

            proxy.RenderProjection?.UpdateProjection();
        }

        // The pawn is already in world.Entities (the engine added it) but not yet in this proxy's own
        // _spawnedEntities - add it so the per-entity animation/transform sync passes (SyncAnimation/
        // SyncTransform) see it every frame, exactly like the old SpawnPlayerEntity used to.
        _spawnedEntities.Add(entity);
        PlayerEntity = proxy;

        // E5.a: port of GameEngine.cs:644 (InitializeEntitySlots' trailing
        // "g_entityFollowedByCamera = StaticVariables.PlayerEntity") - the camera follows the hero by
        // default until an event program retargets it (opcode 0x67) or forces a look-at (0x69).
        EntityFollowedByCamera = proxy;
    }

    /// <summary>
    /// Port of <c>InitializeMapEvents</c> (GameEngine.cs:476-583), restricted to the record-driven half
    /// (the fixed 0x40-slot pre-clear loop is a PSX-specific fixed-array reset with no equivalent need
    /// here - <see cref="_mapEvents"/> is just built fresh every world load). One <see cref="AlundraMapEvent"/>
    /// per "MapEvents" object-layer record whose <c>EventCodesBIndex</c> custom property is non-zero, in
    /// record order, each with <see cref="AlundraMapEvent.Entity"/> = <see cref="PlayerEntity"/> and a
    /// fresh <see cref="EventProgramState"/> - exactly like the original's <c>Entity = PlayerEntity</c>,
    /// <c>EventData = new EventProgramState()</c>. Left empty when there is no "MapEvents" layer, or no
    /// <see cref="PlayerEntity"/> to run them against (<see cref="RunMapEventsPass"/> always executes
    /// against the player; a null player has nothing to drive them with).
    /// </summary>
    /// Widened to <c>internal</c> (C1, docs/plan-camera-ordre-frame.md §4), same reason/precedent as
    /// <see cref="PlayerEntity"/>'s own setter above: lets the map-event characterization test build
    /// <see cref="_mapEvents"/> directly off a synthetic layer, since <see cref="InitializeWithWorld"/>'s
    /// own call site is unreachable without a live world/tilemap.
    internal void BuildMapEvents(TileMapObjectLayerData? mapEventsLayer)
    {
        if (mapEventsLayer == null || PlayerEntity == null)
        {
            return;
        }

        foreach (var record in mapEventsLayer.Objects)
        {
            AlundraEntitySpawnFactory.TryGetRecordInt(record, "EventCodesBIndex", out var programBMap);
            if (programBMap == 0)
            {
                continue;
            }

            AlundraEntitySpawnFactory.TryGetRecordInt(record, "Index", out var id);
            AlundraEntitySpawnFactory.TryGetRecordInt(record, "X1", out var x1);
            AlundraEntitySpawnFactory.TryGetRecordInt(record, "Y1", out var y1);
            AlundraEntitySpawnFactory.TryGetRecordInt(record, "X2", out var x2);
            AlundraEntitySpawnFactory.TryGetRecordInt(record, "Y2", out var y2);

            _mapEvents.Add(new AlundraMapEvent
            {
                Id = id,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                ProgramBMap = programBMap,
                Entity = PlayerEntity,
            });
        }
    }

    /// <summary>
    /// World-level half of the frame (decision D2/D3, docs/plan-conversion-totale.md §2): every spawned
    /// entity now picks/runs/syncs itself in its OWN <see cref="AlundraEntityScriptProxy.Update"/>, driven
    /// by the ENGINE's own entity update loop - which always runs BEFORE this world's own
    /// <see cref="Update"/> (<c>World.Update</c>, CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:443-491).
    /// So by the time this method runs, every entity has already had its own pick/run/sync this frame; this
    /// only covers what the ORIGINAL still runs at the manager/world level: MapEvents
    /// (<see cref="RunMapEventsPass"/>, port of <c>RunMapEvents</c>) and the do/while catch-up re-scan
    /// (<see cref="RunPendingEventTriggers"/>, port of <c>UpdateEntitiesEvents</c>'s phase-2 loop, decision
    /// D3) for any entity another entity's script triggered DURING this same frame's entity pass.
    /// <c>RunEntityEventsPass</c> et al are removed - see <see cref="AlundraEntityScriptProxy.Update"/>'s
    /// own doc for the accepted ordering deviation this implies.
    /// </summary>
    public override void Update(float elapsedTime)
    {
        // Bug fix (AlundraLogicClock's own class doc): this world's ONE shared logic clock. Reads the SAME
        // cached value every spawned entity's own Update already advanced/read this frame (this proxy's
        // own Update always runs LAST - World.cs:443-491) - or, for a world with no entities at all (the
        // `if` further down never ran a single AlundraEntityScriptProxy.Update this frame), becomes the
        // clock's own first caller this frame, so the clock still advances every frame regardless of
        // entity count.
        //
        // E5.c (docs/plan-e5-camera.md §2 ter): read here, at the very head of the frame, rather than
        // further down where it used to live - UpdateCameraFollow below now needs it. Moving the read
        // earlier is side-effect-free: it is a memo read (AlundraLogicClock._frameComputed), nothing
        // between this line and the old site touches the clock, and both CloseFrame() calls still run
        // after it.
        var ticksThisFrame = LogicTicksThisFrame(elapsedTime);

        // C1 (docs/plan-camera-ordre-frame.md §3): map-events run FIRST, before the camera block - a
        // faithful port of the original's own frame order (GameEngine.cs:1638-1664/1743-1753:
        // RunMapEvents() -> UpdateEntities(), whose own look-at update is the LAST thing it does), not
        // camera-then-map-events (the previous, unmotivated order - see the plan's §1.2/1.3 for the
        // symptom this produced: the camera saw a scripted teleport/retarget one frame late, most
        // visibly at map load, where it showed up as a startup camera snap-then-correct).
        if (PlayerEntity != null)
        {
            // Frame-counted map-event chronology (GameEngine.RunMapEvents originally ran once per the
            // fixed 50 Hz frame) - gated the same way as every entity's own pick/run pass.
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                RunMapEventsPass(PlayerEntity, _mapEvents, EventProgramRunner, GameState.PlayerControlFlags);
            }
        }

        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame"): applies at most one overlay
        // reconstruction for however many 0x54/0x55/0x85 opcodes the loop above just dispatched (a map's
        // four-hatch entry alone fires four separate CopyCellRectangle calls) - a no-op when nothing
        // actually changed the overlay's contents (AlundraCellVisualSync.ReconstructionCount stays put).
        // Invariant (plan §3, point 1): stays immediately after the map-events pass, unmoved by C1.
        _cellVisualSync?.FlushPendingOverlayReconstruction();

        if (_spawnedEntities.Count != 0)
        {
            RefreshUpdateProxiesAndCollidables();

            // Decision D3's own catch-up rescan - same frame-counted shape as RunMapEventsPass above (the
            // original's own do/while re-scan ran once per fixed frame too). C1: this still runs BEFORE
            // the camera block below - the original's own re-scan (EntityManager.cs's UpdateEntitiesEvents
            // phase-2 loop) is itself part of UpdateEntities, which the look-at update only follows at the
            // very end (see this method's own C1 comment above) - so anything a re-scanned entity's 0x67/
            // 0x64/0x69 does must be visible to THIS frame's camera too, not just the map-events pass'.
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                RunPendingEventTriggers(_updateProxies, EventProgramRunner);
            }

            // Wall/sprite depth interleave (Slice B) - see WallPlacementOverlay's class doc. Gated on the
            // overlay actually having been populated: with no wall placements loaded (missing/malformed
            // property) there is nothing to interleave against, so entities are left at whatever
            // DepthSortable2DComponent defaults their prefab already carries instead of paying a per-frame
            // field write for nothing.
            if (_wallPlacementOverlayApplied)
            {
                AlundraFrameSyncPasses.RunWallInterleaveSortKeyPass(_spawnedEntities);
            }
        }

        // E5.a: resolve the camera once, then run the scripted follow BEFORE the debug pan - see
        // UpdateCameraFollow's own doc on why this order lets the pan's base-adoption mechanism pick up
        // the follow's write the SAME frame instead of one frame late. Both run unconditionally (like the
        // debug pan already did) so the camera still follows/can be panned even for a world with no
        // entities. Invariant (plan §3, point 3): this internal order - resolve -> follow -> pan - is
        // untouched by C1.
        //
        // E5.c: the follow is driven by the LOGIC TICK COUNT, not by elapsed time - see
        // UpdateCameraFollow's own doc on the cadence beat that per-frame smoothing caused. The debug pan
        // stays per rendered frame (it samples the stick).
        // S2 (docs/plan-update-caracterisation.md): the camera wiring itself now lives on
        // _cameraDirector; the map bounds and _world are read here at USE TIME and passed in per frame
        // (extended proof rule delta (a)) - _tileMapData is only assigned in InitializeWithWorld AFTER
        // two early returns, so capturing it any earlier would hold null forever and silently drop the
        // map-bounds clamp (trap 2).
        //
        // C1: the whole camera block moves here, AFTER the map-events pass and the pending-event/wall
        // passes above (plan §3: not merely after map-events - the original's own look-at update is the
        // LAST thing UpdateEntities does, after its own catch-up re-scan too) - so EntityFollowedByCamera/
        // the look-at position both reflect whatever this SAME frame's scripts (0x64/0x65/0x67/0x69, from
        // either pass) just wrote, exactly like the original.
        _cameraDirector.ResolveDebugCameraOnce(_world);
        _cameraDirector.UpdateCameraFollow(
            ticksThisFrame,
            EntityFollowedByCamera,
            _tileMapData != null ? _tileMapData.MapSize.Width * TileWidth : null,
            _tileMapData != null ? _tileMapData.MapSize.Height * TileHeight : null);
        _cameraDirector.UpdateDebugCameraPan(elapsedTime, _world);

        // Rendering-only passes - per rendered frame, same reasoning. C1: moved down with the camera
        // block (plan §3) - UpdateAndDrawBackdrop reads the just-resolved camera, so it must stay right
        // after it.
        // S3 (docs/plan-update-caracterisation.md): the rendering wiring itself now lives on
        // _backdropStage; _world is read here at USE TIME and passed in per frame (extended proof rule
        // delta (a)), and the resolved camera is passed in rather than re-looked-up (delta (a), the one
        // named for S3) since it is _cameraDirector's own state.
        _backdropStage.ApplyOriginalBackgroundClearColorOnce(_world);
        _backdropStage.UpdateAndDrawBackdrop(elapsedTime, _world, _cameraDirector.ResolvedCamera);

        // E10.b (docs/plan-e10-fondu.md, §1.6/D-E10-8): the fade pass - positioned here purely for
        // frame-order consistency with the camera/backdrop block above, NOT because it depends on
        // either: it advances its own two 16.16 machines by LOGIC TICKS (ticksThisFrame, same cadence as
        // UpdateCameraFollow - never by rendered frame, §1.6) and then pushes colour/blend/active to the
        // engine's ScreenEffectService - no camera read, no backdrop read. "Advance, then push" - never
        // the reverse (§1.5: pushing before Advance would submit a stale, or even the just-armed 255,
        // value one frame early).
        AlundraScreenFadeDirector.Instance.Advance(ticksThisFrame);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        // Closes this frame's logic-clock memo (see AlundraLogicClock's own class doc) - this proxy's own
        // Update always runs last (World.cs:443-491), so the next frame's first caller (an entity's own
        // Update, or this proxy again for a zero-entity world) recomputes fresh. C1 (plan §3): the old
        // two-call-site shape (an early return for zero entities, this same call for the rest) collapses
        // to this ONE call on every path now that the camera/render block runs unconditionally instead of
        // early-returning before it - invariant (plan §3, point 2): CloseFrame runs exactly once.
        _logicClock.CloseFrame();

        // docs/plan-camera-premiere-frame.md §3, point 1: clears the sticky first-frame tick floor
        // exactly once, right next to CloseFrame - see _firstFrameStillOpen's own doc. Idempotent past
        // the very first frame (already false), so this unconditional write is safe on every later call.
        _firstFrameStillOpen = false;
    }

    /// <summary>
    /// Port of <c>RunMapEvents</c> (GameEngine.cs:1667-1718, 0x8003c67c). Always executes against
    /// <paramref name="player"/> - every MapEvent's own logic entity starts as the player
    /// (<see cref="BuildMapEvents"/>) and can only ever be retargeted by opcode 0x66 (not ported, never
    /// reached by map 389's own programs - docs/intro-roadmap.md §1.5).
    /// </summary>
    internal static void RunMapEventsPass(
        AlundraEntityScriptProxy player, IReadOnlyList<AlundraMapEvent> mapEvents, IEventProgramRunner runner,
        uint playerControlFlags)
    {
        if ((playerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0)
        {
            return;
        }

        for (var i = 0; i < mapEvents.Count; i++)
        {
            var mapEvent = mapEvents[i];

            if ((mapEvent.ProgramBMap & 0x7F) == 0)
            {
                continue;
            }

            var mapEventEntity = mapEvent.Entity ?? player;

            if (player.TileX < mapEvent.X1 || player.TileX > mapEvent.X2
                || player.TileY < mapEvent.Y1 || player.TileY > mapEvent.Y2)
            {
                // Out-of-zone reset, GameEngine.cs:1690-1697 - ported exactly, including the somewhat
                // surprising choice of resetting the MAP EVENT'S OWN logic entity's EventProgramState
                // (not the player's own persistent one) each time the player leaves the zone.
                mapEventEntity.ChildEntity = null;
                mapEventEntity.EventProgramState.Sp = 0;
                mapEventEntity.RelativeWarpOffsetX = 0;
                mapEventEntity.Index = player.Index;
                continue;
            }

            player.ProgramIndexes[ScriptHelper.ProgramBMap] = mapEvent.ProgramBMap;
            player.MapEventProgramId = mapEvent.ProgramBMap;
            // GameEngine.cs:1702: the original indexes the FIXED g_mapEvents[0x40] array by record
            // position (InitializeMapEvents sets g_mapEvents[i].Id = i for every slot, occupied or not),
            // so "i" there is the record's own slot index. mapEvents here is compacted (only records with
            // EventCodesBIndex != 0 are kept - see BuildMapEvents), so the loop's own "i" is the compacted
            // list position, NOT the record index; mapEvent.Id carries the real record index instead.
            player.EventTrigger = mapEvent.Id;
            player.LogicEntity = mapEventEntity;
            player.EventProgramState.CopyFrom(mapEvent.EventData);

            runner.RunScript(player, ScriptHelper.ProgramBMap);

            mapEvent.EventData.CopyFrom(player.EventProgramState);
            mapEvent.Entity = player.LogicEntity;
            mapEvent.ProgramBMap = player.ProgramIndexes[ScriptHelper.ProgramBMap];
        }
    }

    /// <summary>
    /// Decision D3 (docs/plan-conversion-totale.md §2): port of the do/while re-scan half of
    /// <c>EntityManager.UpdateEntitiesEvents</c> (EntityManager.cs:874-921), applied here to whatever
    /// <see cref="AlundraEntityScriptProxy.EventTrigger"/> another entity's OWN <c>Update</c> set on it
    /// earlier THIS SAME FRAME (<see cref="AlundraEntityScriptProxy.PickEventTrigger"/> having already run
    /// for every entity, since the engine updates entities before this world - see this class's own
    /// <see cref="Update"/> doc). The player is excluded, same as the original's own loop starting at
    /// index 1 - its own trigger (set by <see cref="RunMapEventsPass"/>) is consumed directly by that
    /// method's own <c>RunScript</c> call, never by this re-scan.
    /// </summary>
    internal static void RunPendingEventTriggers(IReadOnlyList<AlundraEntityScriptProxy> entities, IEventProgramRunner runner)
    {
        bool keepGoing;

        do
        {
            keepGoing = false;

            // Indexed for, not foreach: an IReadOnlyList<T>-typed foreach goes through the interface's
            // own IEnumerator<T> (a boxed enumerator on every call, unlike List<T>'s own struct
            // enumerator) - this pass now runs every frame, so that allocation is no longer free to skip.
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                if (entity.IsPlayer || entity.EventTrigger == ScriptHelper.ProgramUnknown)
                {
                    continue;
                }

                entity.RunPickedEvent(runner);
                keepGoing = true;
            }
        } while (keepGoing);
    }

    /// <summary>
    /// V1 minimal port of <c>GameEngine.DestroyEntity(Entity, int)</c> @ 0x8003A59C: marks the entity for
    /// destruction (naturally skipped by the pick phase from now on) and logs once at debug level with
    /// the original's numeric effect-id argument (-1 = "use the sprite record's break effect", 6 = the
    /// sliding-slope break effect, see the pick-phase callers above). Does not remove the entity from the
    /// CasaEngine world yet (slot recycling, contents spawning and the original's other side effects -
    /// ActiveEffect/PlatformEntity cleanup, SpawnEntityContents - are later work).
    /// </summary>
    internal void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (effectId={effectId}).");
    }

    /// <summary>
    /// V1 port of the single-argument <c>GameEngine.DestroyEntity(Entity)</c> @ 0x8003A774 - the overload
    /// every search-driven destroy opcode (0x2E Script_46_02E) calls once per match, distinct from the
    /// two-argument overload above (which the pick-phase status machine uses, and which also spawns
    /// break-effect contents). Same V1 scope note as the two-argument overload: does not remove the
    /// entity from the CasaEngine world (slot recycling is later work - see that overload's own doc) and
    /// does not port <c>ActiveEffect</c>/<c>PlatformEntity.CarriedEntity</c> cleanup. Clears
    /// <see cref="AlundraEntityScriptProxy.EventTrigger"/> like the original, so a same-frame re-scan
    /// (<see cref="RunPendingEventTriggers"/>) does not also try to run whatever program slot this entity
    /// had queued before being destroyed.
    /// </summary>
    internal void DestroyEntity(AlundraEntityScriptProxy entity)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        entity.EventTrigger = ScriptHelper.ProgramUnknown;
        Logs.WriteDebug($"AlundraWorldProxy: entity[{entity.EntityRefId}] -> FlagToDestroy (search-destroyed).");
    }

    /// <summary>
    /// Backs opcode 0x2D (Script_45_02D) via <see cref="IEntityWorldContext"/>. Faithful port of
    /// <c>GameEngine.SpawnEntity(parent, entityId, notCheckSpawnZone)</c> (GameEngine.cs:679-758)
    /// restricted to <c>notCheckSpawnZone = 1</c>, the only value the opcode ever passes - so only the
    /// <c>IsEnabled</c> gate applies (see <see cref="ShouldSpawnRecord(TileMapObjectData,bool,out string)"/>'s
    /// own doc); the record lookup (<c>GameEngine.GetEntityRecord</c>) is <see cref="_entityRecordsByIndex"/>.
    /// Shares the exact same build path as the map-load spawn loop in <see cref="InitializeWithWorld"/>
    /// (<see cref="AlundraEntitySpawnFactory.CreateEntityFromRecord"/> -&gt; <see cref="AlundraEntitySpawnFactory.ApplyRecord"/> -&gt;
    /// <see cref="AlundraEntitySpawnFactory.ApplySpawnInitialization"/>), with <paramref name="logicEntity"/>'s own backing entity
    /// passed down as the new entity's <c>ParentEntity</c> - exactly like <c>EntityManager.InitializeEntity</c>
    /// does for its <c>parentEntity</c> argument. The spawned entity joins <see cref="_spawnedEntities"/>
    /// immediately, so it is visible to any further search this same script call issues, and is picked up
    /// by <see cref="Update"/>'s per-frame passes starting next frame (it enters as
    /// <see cref="EntityStatus.Loaded"/>, so its own Load program runs then, same as every map-load spawn).
    /// Unlike the original (a single global, size-limited entity-slot table with recycling), this always
    /// allocates a brand new CasaEngine <see cref="Entity"/> - no slot reuse in V1.
    /// </summary>
    internal AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
    {
        if (_world == null)
        {
            Logs.WriteWarning("AlundraWorldProxy: SpawnEntityByRecordId called before InitializeWithWorld; ignored.");
            return null;
        }

        if (!_entityRecordsByIndex.TryGetValue(entityRecordId, out var record))
        {
            Logs.WriteDebug(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) - no such entity record "
                + "(GameEngine.GetEntityRecord would return null); spawn skipped.");
            return null;
        }

        if (!AlundraEntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone: true, out var skipReason))
        {
            Logs.WriteDebug(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) - record '{record.Name}' "
                + $"not spawned ({skipReason}).");
            return null;
        }

        try
        {
            var entity = AlundraEntitySpawnFactory.CreateEntityFromRecord(
                record, guid => _world.Game.AssetContentManager.Load<Entity>(guid), SpriteRecordCatalog,
                parentEntity: logicEntity.LogicContextEntity, tileMapData: _tileMapData);
            var spawnedProxy = entity.GameplayProxy as AlundraEntityScriptProxy;
            if (spawnedProxy != null)
            {
                spawnedProxy.ScriptHost = this;
            }

            _world.AddEntity(entity);
            _spawnedEntities.Add(entity);
            // E4.b: same ground-clamp + root push as the map-load spawn loop above - see that call site's
            // own doc for why this must happen AFTER AddEntity.
            spawnedProxy?.PushLogicalPositionToRoot();
            // E4.f: same one-shot spawn-time support evaluation as the map-load spawn loop - see
            // BuildCollidablesSnapshot's own doc. Block 18 (record 18) spawns this way (opcode 0x2D) and
            // does not land on another entity (its own fall is terrain-only), so this is a no-op for it,
            // but a future dynamically-spawned rider needs it too.
            spawnedProxy?.EvaluateEntitySupport(BuildCollidablesSnapshot(), immediateAtSpawn: true);
            return spawnedProxy;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: SpawnEntityByRecordId({entityRecordId}) failed to spawn; skipping. "
                + $"{ex.Message}");
            return null;
        }
    }

    // IEntityWorldContext - see that interface's own doc. Explicit implementation keeps these reachable
    // through the interpreter seam without adding public surface to this proxy.
    IReadOnlyList<AlundraEntityScriptProxy> IEntityWorldContext.SpawnedEntities => GetSpawnedEntityProxies();

    AlundraEntityScriptProxy? IEntityWorldContext.PlayerEntity => PlayerEntity;

    AlundraEntityScriptProxy? IEntityWorldContext.SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
        => SpawnEntityByRecordId(logicEntity, entityRecordId);

    void IEntityWorldContext.DestroyEntity(AlundraEntityScriptProxy entity) => DestroyEntity(entity);

    // IAlundraScriptHost - see that interface's own doc. Explicit implementation, same reasoning as
    // IEntityWorldContext above.
    IEventProgramRunner IAlundraScriptHost.Runner => EventProgramRunner;

    AlundraEntityScriptProxy? IAlundraScriptHost.ActiveCollisionEntity => ActiveCollisionEntity;

    void IAlundraScriptHost.DestroyEntity(AlundraEntityScriptProxy entity, int effectId) => DestroyEntity(entity, effectId);

    AlundraGameState IAlundraScriptHost.GameState => GameState;

    AlundraPlayerController? IAlundraScriptHost.PlayerController => _playerController;

    IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => _collidables;

    /// <summary>
    /// Snapshot of <see cref="_spawnedEntities"/>'s own <see cref="AlundraEntityScriptProxy"/> proxies, in
    /// the same creation order - built fresh on every call (not cached) so an entity dynamically spawned
    /// by 0x2D earlier in the same script call is visible to a search issued later in that call, exactly
    /// like the original's live <c>g_entitySlots</c> array (see <see cref="EntitySearchService"/>'s class
    /// doc). Not a per-frame hot path (only entity-manipulation opcodes call it), so the allocation here
    /// is fine - contrast with <see cref="_updateProxies"/>, the actual per-frame working list.
    /// </summary>
    private List<AlundraEntityScriptProxy> GetSpawnedEntityProxies()
    {
        var proxies = new List<AlundraEntityScriptProxy>(_spawnedEntities.Count);

        foreach (var entity in _spawnedEntities)
        {
            if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
            {
                proxies.Add(proxy);
            }
        }

        return proxies;
    }

    /// <summary>
    /// Rebuilds <see cref="_updateProxies"/> from <see cref="_spawnedEntities"/> and, from that,
    /// <see cref="_collidables"/> (E4.f) - the per-frame hot-path pair, both reused list instances (no
    /// allocation). Called once per frame from <see cref="Update"/>, and once more right after the
    /// map-load spawn loop in <see cref="InitializeWithWorld"/> completes, so <see cref="_collidables"/>
    /// already reflects every load-time-spawned entity BEFORE the engine's very first
    /// <c>CharacterMotionSystem.UpdateControllers</c>/entity-<c>Update</c> pass ever runs - without that
    /// second call, frame 1's <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/> would see an
    /// empty <see cref="_collidables"/> and wrongly restore gravity on every supported entity for one
    /// frame (the exact "creux de première frame" decision E4-4 forbids) before <see cref="Update"/> ever
    /// gets to populate it. <see cref="_updateProxies"/>/<see cref="_collidables"/> are otherwise always
    /// exactly one frame stale relative to entities spawned or moved THIS frame (the same latency
    /// <see cref="_updateProxies"/> already had before E4.f) - harmless for map 389's intro, whose
    /// platform entities never move.
    /// </summary>
    private void RefreshUpdateProxiesAndCollidables()
    {
        _updateProxies.Clear();
        foreach (var entity in _spawnedEntities)
        {
            if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
            {
                _updateProxies.Add(proxy);
            }
        }

        EntitySupport.BuildCollidables(_updateProxies, _collidables);
        EntitySupport.UpdateRidingEntities(_collidables);
    }

    /// <summary>
    /// E4.f spawn-time support snapshot: a freshly-built (allocating - spawn is not a per-frame hot path,
    /// same policy as <see cref="GetSpawnedEntityProxies"/>) collidables list off whatever is in
    /// <see cref="_spawnedEntities"/> AT THIS EXACT MOMENT, for
    /// <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>'s own one-shot call right after a
    /// record spawns (see the map-load spawn loop in <see cref="InitializeWithWorld"/> and
    /// <see cref="SpawnEntityByRecordId"/>). Map 389's platform records (0-5) spawn before their riders in
    /// record order (verified against the real export - see docs/plan-e4-deplacement-scripte.md E4.f's own
    /// "Pourquoi" note), so by the time a rider's own spawn reaches this call every platform it could land
    /// on is already present.
    /// </summary>
    private List<AlundraEntityScriptProxy> BuildCollidablesSnapshot()
    {
        var collidables = new List<AlundraEntityScriptProxy>();
        EntitySupport.BuildCollidables(GetSpawnedEntityProxies(), collidables);
        return collidables;
    }

    public override void Draw()
    {
        //Nothing to do at world level yet.
    }

    public override void OnHit(Collision collision)
    {
        //The world proxy does not participate in collisions.
    }

    public override void OnHitEnded(Collision collision)
    {
        //The world proxy does not participate in collisions.
    }

    public override void OnBeginPlay(World world)
    {
        //Entity creation happens in InitializeWithWorld so the engine integrates the entities
        //(InternalAddEntities) before BeginPlay is dispatched to them.
    }

    public override void OnEndPlay(World world)
    {
        //Nothing to tear down at world level yet.
    }

    public override IGameplayProxy Clone()
    {
        //Still returns a fresh instance: the spawned-entity list is runtime state rebuilt by
        //InitializeWithWorld (each world instance spawns and owns its own entities), not something a
        //clone should share or carry over.
        return new AlundraWorldProxy();
    }
}

/// <summary>
/// One <c>MapEvent</c> slot (GameEngine.cs's <c>MapEvent</c> struct) - see
/// <see cref="AlundraWorldProxy.BuildMapEvents"/>/<see cref="AlundraWorldProxy.RunMapEventsPass"/> for how
/// this is built and driven. A plain mutable class (not a struct): <see cref="RunMapEventsPass"/> mutates
/// <see cref="Entity"/>/<see cref="ProgramBMap"/> in place across frames, exactly like the original's own
/// persistent array slot.
/// </summary>
internal sealed class AlundraMapEvent
{
    public int Id;
    public int X1, Y1, X2, Y2;
    public int ProgramBMap;

    /// <summary>The map-event's own current "logic entity" (initially the player - see
    /// <see cref="AlundraWorldProxy.BuildMapEvents"/>; only opcode 0x66, not ported, can ever retarget
    /// it).</summary>
    public AlundraEntityScriptProxy? Entity;

    public readonly EventProgramState EventData = new();
}
