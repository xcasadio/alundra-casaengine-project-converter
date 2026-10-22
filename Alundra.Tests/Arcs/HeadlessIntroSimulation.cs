#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Cells;
using Alundra.Audio;
using Alundra.Dialogues;

namespace Alundra.Tests.Arcs;

/// <summary>
/// Drives one headless replay of map 389's intro frame structure. Mirrors, with original names and
/// file:line references, the exact original call chain:
/// <list type="bullet">
/// <item><description>Map-entry block, <c>GameEngine.cs:168-219</c>: <c>LoadMap</c>; player spawn;
/// <c>ClearTemporaryFlags</c> (429); <c>ResetCameraAndLoadVRAMAssets</c>; <c>InitializeItems</c>;
/// <c>LoadMapAndInitializeEntities</c> (466: <c>InitializeEntitySlots</c> -&gt;
/// <c>InitializeMapEvents</c> -&gt; <c>EffectManager.InitializeEffectSlots</c>); <c>WarpPlayer</c> (878:
/// fade setup, <c>g_warpDelayFrames=10</c>); <c>InitializeScrollingMode</c>;
/// <c>HudManager.InitializeHudPositionBeforeHide</c>; <c>LoadMapSounds</c>; <c>Update(1)</c>;
/// <c>GraphicManager.ResetDebugRenderingState</c>.</description></item>
/// <item><description>Then each frame: <c>RenderScene</c> (352, not simulated - no rendering here) then
/// <c>Update(0)</c> (1500).</description></item>
/// <item><description><c>Update</c> (1500-1592): pad polling, <c>UpdateWorld()</c>, warp delay countdown,
/// inventory-open check, sound streaming, RNG tick.</description></item>
/// <item><description><c>UpdateWorld</c> (1638-1664): <c>RunMapEvents()</c>; <c>UpdateEntities()</c>;
/// <c>EffectManager.UpdateEffects()</c>.</description></item>
/// <item><description><c>RunMapEvents</c> (1667-1718, quoted in full in
/// <see cref="WorldProxy.RunMapEventsPass"/>'s own doc comment).</description></item>
/// <item><description><c>UpdateEntities</c> (EntityManager.cs:367-395):
/// <c>UpdateEntitiesEvents</c> (806, itself <c>MovePlayer()</c> then the pick/run passes - now each
/// entity's own <see cref="EntityScriptProxy.Update"/>, run directly by this harness's own frame
/// loop in engine order, then <see cref="WorldProxy.RunPendingEventTriggers"/> for decision D3's
/// catch-up re-scan), then <c>UpdateDestroyedEntities</c>/<c>Counters</c>/<c>Lists</c>/<c>Animation</c>,
/// <c>PhysicsEngine.UpdateEntitiesPhysics</c> (E4.e: horizontal integration now runs per-entity inside
/// <see cref="EntityScriptProxy.Update"/> via the production <c>ScriptedMotion.TickScriptedNpc</c>,
/// vertical gravity/ground-clamp now runs here via <see cref="RunVerticalPhysicsPass"/>, both flat-ground
/// and no-entity-collision only - see that method's own doc for the exact scope), <c>UpdateActiveEffects</c>,
/// <c>UpdateBalanceRecords</c>, <c>UpdateVisibleEntitiesZSort</c>.</description></item>
/// </list>
///
/// Out of scope (documented non-goals, not simulated): rendering, the PLAYER's own movement/physics (no
/// controller/input/camera for the hero yet - E2/E5/E6 - the player proxy has no Controller and its own
/// Flags stay 0, so it is excluded from both the horizontal and vertical passes above by construction),
/// sound/effects/HUD, entity-vs-entity HORIZONTAL collision, walls/navigation (E4-1: the intro's own
/// paths are unobstructed on map 389). Entity-vs-entity Z SUPPORT (E4.f, decision E4-4 -
/// <see cref="EntitySupport"/>) IS simulated - static platforms only, no moving-platform passenger
/// follow (E14). Dynamic entity spawn (opcodes 0x2D/0x8A/0x8B, via <see cref="SpawnEntityByRecordId"/>) IS
/// simulated - see that method's own doc.
/// </summary>
internal sealed class HeadlessIntroSimulation : IEntityWorldContext, IScriptHost, ISoundPlayer
{
    private const string WorldName_ = "Ship Klark (beginning)-389";

    /// <summary>Program id of map-event 0 (B 129), the intro's own cinematic driver - see
    /// <see cref="TraceAwareEntityRunner.RunScript"/>'s own doc for why this replaces the old
    /// index-based "is this map-event 0" check now that <see cref="WorldProxy.RunMapEventsPass"/>
    /// no longer hands this harness a per-map-event callback to hook context off of.</summary>
    private const int IntroCinematicProgramId = 129;

    // D-E7-11 (docs/plan-e7-mutation-tuiles.md): same tile-size constants every TileX/TileY derivation in
    // this DLL already carries its own private copy of (EntityRecordMapper.cs:107-108,
    // ScriptedMotion.cs:60-61, WorldProxy.cs:55-56).
    private const int TileWidthPx = 24;
    private const int TileHeightPx = 16;

    private static readonly HashSet<int> ImplementedOpcodes = new()
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x09, 0x0A, 0x10, 0x11, 0x16, 0x17, 0x19, 0x1A, 0x1B,
        0x1E, 0x1F, 0x27, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38,
        0x3B, 0x3E, 0x49, 0x4B, 0x54, 0x55, 0x5A, 0x5B, 0x62, 0x63, 0x64, 0x65, 0x67, 0x68, 0x69, 0x6E,
        0x70, 0x78, 0x79, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x80, 0x81, 0x85, 0x8B, 0x8D, 0xAC, 0xAD, 0xB8,
        0x0D, 0x12, 0x39, 0x44, 0x50, 0x51, 0x53, 0x5C, 0x75, 0x9B, 0x9C, 0xA5, 0xA6, 0xA7, 0xA8, 0xAB,
        0xAF, 0xB0, 0xB1, 0xBA, 0xBD, 0xBE, 0xBF,
        0x42, 0x43, 0x59,
        0x28, 0x29, 0x2A, 0x2B, 0x3F, 0x4A, 0x52, 0x57, 0x58, 0x5D, 0x8C, // E19.l1 (classes, riding, restart, directional branches, portal, random).
        0x82, 0x83, 0x84, 0x98, 0x99, 0x9A, 0x9F, // E19.l2 (items, money and the chest flag).
        0xC4, // E19.b T1 (dialogue with a speaker search, without the name box).
        0x8A, // E19.b T2 (spawn at an absolute position).
        0x08, 0x0B, 0x0C, 0x3A, 0x5E, 0x73, 0x74, 0x89, // E19.c1 T3 (motion opcodes).
        0x1C, 0x1D, // E19.c1 T4 (wait for animation ends).
        0x24, // E19.d D4 (wait until ForceAdjusted of the logic entity is nonzero).
        0x25, // E19.d2c1 R4 (wait until CollidedWithEntityZ or IsOnGround of the logic entity is nonzero; trace label only).
        0x21, 0x26, 0x47, 0x48, 0x6F, // E19.h1 H1-1 (Z waits and the Z contact reader).
        0x20, 0x22, 0x23, // E19.h1b2 H1B2-R1 (the Z distance wait and the two height waits; trace label only).
        0x45, 0x46, // E19.h4 H4-R1 (clear and set NoObstacleSlide).
        0x8E, 0x8F, // E19.k1 K1 (camera sway start and stop).
        0xA4, // E19.k2 K2-2 (background layer mask; the palette cycle of a bank above 0 stays degraded, O-E19-43).
        0x40, 0x41, // E19.d D6 (set program index and clear the state, set sprite program index).
        // E16.a T6 (docs/plan-e16-etat-partie.md, contract item 4): the 23 opcodes of the two rows that start at 0x0D were
        // ported by earlier slices (dialogue, map change, HUD, audio, ...) and already have a Dispatch
        // case, but were never added here - the same staleness the notes below describe. Checked by
        // comparing every Dispatch case label with this set: they are now equal.
        // E16.a (docs/plan-e16-etat-partie.md): 0x32/0x34/0x35 (T1), 0x2C/0x3E/0x6E/0xAD/0xB8 (T2), 0x8D
        // (T3) and 0x78/0x79/0x7B/0x7C/0x7D/0x7E/0x7F/0x80/0x81 (T4, D-E16-21 - the "stored choice param"
        // family) newly implemented (EventProgramRunner.Dispatch) - added here together since none
        // of T1-T3 had touched this hand-maintained mirror yet.
        // E5.a: 0x67/0x68/0x69 (camera follow/stop/forced look-at) newly implemented - the six real
        // map-389 0x67 occurrences (docs/intro-programs-389.txt) now trace as [implemented].
        // E4.e correction: 0x1E/0x1F (Walk / Walk with collision) were already ported by E4.d
        // (EventProgramRunner.Dispatch cases 0x1E/0x1F) but were missing here, so the static
        // disassembly annex kept tagging them [NOT IMPLEMENTED] a whole tranche after they stopped being
        // so - a stale label with no effect on the trace itself (the real EventTraceKind the runner
        // reports was always correct), fixed in passing.
        // E7.a (docs/plan-e7-mutation-tuiles.md): 0x54/0x55/0x85 newly implemented (CellStore).
        // 0x33 correction: this HashSet is the SAME "provenance" the plan asked E7.a to establish for the
        // dump's [NOT IMPLEMENTED]/[implemented] tag (BuildProgramsDisassemblyText below, NOT
        // EventProgramRunner.Dispatch, which has had a 0x33 case since before E7 - see
        // Script_51_033/CheckFlagsOn) - it is a hand-maintained mirror of Dispatch's real opcode coverage,
        // independent of it by construction, exactly the same class of staleness the 0x1E/0x1F note above
        // already documents. 0x33 was simply never added here when its Dispatch case landed - fixed in
        // passing, in this SAME set, since it is a one-line addition next to the three opcodes this slice
        // already touches (not a re-litigation of any E7 decision - 0x33 itself is untouched, only its
        // label in this disassembly annex).
        // E7.c (docs/plan-e7-mutation-tuiles.md): 0x3B (Check player in area) and 0x2F (Check pad
        // buttons, D-E7-7 relabel) newly implemented (EventProgramRunner.Dispatch cases 0x3B/0x2F)
        // - see PessimisticPredicateOpcodes/OptimisticPredicateOpcodes' own updated docs above for why
        // this changes only labels, never a Result, on map 389.
        // E19.a (docs/plan-e19-opcodes.md T2): 0x42/0x43 (logic entity) newly implemented; map 389 has no
        // site of either, so no line of the annexes changes. T3: 0x59 (Set entity anim) newly implemented;
        // map 389 has one site (offset 1369), whose label changes in the annexes.
    };

    private readonly string _projectRoot;
    private readonly string _worldName;
    private readonly EventProgramDocument _document;
    private readonly byte[] _codesBytes;
    private readonly GameState _gameState = new();
    private readonly EventProgramRunner _runner;
    private readonly TraceAwareEntityRunner _wrapperRunner;

    // E4.e simulated kinematics (docs/plan-e4-deplacement-scripte.md, decision E4-1): _catalog feeds
    // EntitySpawnFactory.ApplySpawnInitialization (real AnimSetsByAnim/Flags/Width/Height/Depth/ModX/Y/Z off
    // the real Data/sprite-records.json - see BuildInitialState/SpawnEntityByRecordId, same seam
    // NpcCharacterControllerMoverTests' own end-to-end spawn test already uses headless);
    // _tileMapData/_groundField/_mapGravityRaw/_mapZViscosityRaw feed RunVerticalPhysicsPass's own ground
    // probe and gravity integration (real CellsCollisionField/Gravity/ZViscosity of map 389, not
    // synthetic data). _groundField null means "AlundraCells missing or malformed" (degraded mode, already
    // warned by CellsCollisionField.TryCreate itself) - RunVerticalPhysicsPass is then a no-op.
    private SpriteRecordCatalog? _catalog;
    private TileMapData? _tileMapData;
    private CellsCollisionField? _groundField;
    private int _mapGravityRaw;
    private int _mapZViscosityRaw;

    // E7.a (docs/plan-e7-mutation-tuiles.md): built from the SAME CellsRecords instance
    // _groundField above aliases its own arrays from (CellsCollisionField.TryCreate's 4-out
    // overload) - see CellMutator's own doc for why this matters. _installCellMutator gates
    // CellMutator's returned value only (never whether the store itself gets built), so the
    // neutralization twin (acceptance 3) still builds a real, working store - it just never hands it to
    // the interpreter, proving the "no mutator" path leaves export values untouched via the SAME
    // production RunMapEventsPass call, not a different code path.
    private readonly bool _installCellMutator;
    private CellStore? _cellStore;

    // E11.a (docs/plan-e11-audio.md): T1's own "production call site" fake - this harness IMPLEMENTS
    // ISoundPlayer itself (rather than a separate class) since it already tracks its own Frame
    // property, so PlaySfx just records (Frame, sfxId) directly - see SoundRequests' own doc.
    // _installSoundPlayer gates only IEntityWorldContext.SoundPlayer's returned value, same "install a
    // real thing but only sometimes hand it to the interpreter" shape as _installCellMutator above (T2's
    // neutralization twin).
    private readonly bool _installSoundPlayer;
    private readonly List<(int Frame, int SfxId)> _soundRequests = new();

    // E12.a (docs/plan-e12-dialogues.md): unlike every OTHER _install* flag above, this one defaults to
    // FALSE - DialogueDirector.Instance is a SESSION-scoped singleton (not a per-harness fake like
    // ISoundPlayer above), so wiring it by default would leak across every OTHER pre-existing test
    // built on this harness that never anticipated a dialogue system being present. Only
    // DialogueOpcodesProductionTests (T1) passes true, and resets/attaches the singleton itself
    // before driving frames - see OptimisticPredicateOpcodes' own updated doc (item ⑦) for why removing
    // 0x39/0x44/0x51 from that set is still behaviourally inert for every OTHER test.
    private readonly bool _installDialogueDirector;

    /// <summary>E19.f2a (F2-R1): the harness's mirror of the world proxy's pad pass for the dialogue box - the square button as it was at the previous frame, with its
    /// rising edge, derived from <c>LastPadState.ButtonsHold</c> the way <see cref="TickPad"/> derives it in production.</summary>
    private readonly TickPad _dialoguePadMirror = new();

    // Includes the player (index 0) - mirrors WorldProxy's own _spawnedEntities, which also holds
    // the player (SpawnPlayerEntity adds it before any record) - see IEntityWorldContext.SpawnedEntities's
    // own doc for why that matters (a search opcode must be able to find the player too).
    private readonly List<EntityScriptProxy> _spawnedEntities = new();
    private readonly Dictionary<EntityScriptProxy, string> _entityNames = new();
    private readonly Dictionary<int, TileMapObjectData> _entityRecordsByIndex = new(); // for SpawnEntityByRecordId (0x2D/0x8B)
    private readonly List<MapEvent> _mapEvents = new();

    private readonly SortedSet<int> _referencedALoad = new();
    private readonly SortedSet<int> _referencedBMap = new();
    private readonly SortedSet<int> _referencedCTick = new();
    private readonly SortedSet<int> _referencedFInteract = new();

    private EntityScriptProxy _player = null!;
    private string _context = "";
    private bool _contextIsPlayerMapEvent;
    private bool _sawOpcode11;

    // Ordered chronological items: either a pre-formatted system-line string, or a mutable OpcodeRun
    // (mutated in place while its consecutive-frame run keeps extending - see OnOpcodeTraced/step 3 of
    // the trace-compaction brief). Formatted into text only at output time (BuildTraceText/TraceLines).
    private readonly List<object> _flatItems = new();
    private readonly Dictionary<string, OpcodeRun> _openRuns = new(); // context -> its currently-open run
    private int _rawDispatchCount; // uncompacted dispatch count, reported alongside the compacted line count
    private readonly HashSet<string> _seenSystems = new();
    private readonly List<(int FirstFrame, string Name, string FileLine, string Role)> _systemLedger = new();

    private readonly Dictionary<int, (int FirstFrame, string FirstContext)> _unimplementedFirstSeen = new();
    private readonly Dictionary<int, int> _unimplementedCount = new();
    private readonly Dictionary<int, (int FirstFrame, string FirstContext)> _implementedFirstSeen = new();
    private readonly Dictionary<int, int> _implementedCount = new();

    // E7.b (docs/plan-e7-mutation-tuiles.md, acceptance item 9, picking up an E7.a deferral): per-opcode
    // count of EventTraceKind.Degraded dispatches - Implemented and Degraded shared one accounting bucket
    // above (_implementedCount) with nothing distinguishing them; a neutralization run (installCellMutator:
    // false) needs to assert it actually took the degraded path, not merely that export values held still.
    private readonly Dictionary<int, int> _degradedCount = new();
    public IReadOnlyDictionary<int, int> DegradedOpcodeCounts => _degradedCount;

    private readonly SortedSet<int> _blindSpots = new(); // UnknownNoSizeTerminated opcodes
    private readonly List<string> _loopBudgetHits = new(); // LoopBudgetExceeded occurrences (diagnostic)

    private int _distinctTotal;
    private readonly HashSet<(string Context, int Pc)> _seenContextPc = new(); // stop condition (b), see OnOpcodeTraced

    public int Frame { get; private set; }
    public int FrameCount { get; private set; }
    public string? StopReason { get; private set; }
    public IReadOnlyList<string> TraceLines => FormatFlatItems();

    // E7.a test-only accessors (acceptance 3/4, docs/plan-e7-mutation-tuiles.md): let a test inspect
    // post-mutation cell state after driving real frames via RunFramesForTest, without this class itself
    // needing any assertion of its own.
    public CellsCollisionField? GroundField => _groundField;
    public CellStore? CellStore => _cellStore;

    /// <summary>E11.a test-only accessor (T1/T2, docs/plan-e11-audio.md): every <c>(Frame, sfxId)</c>
    /// pair requested through the REAL production dispatch path (0xBD/0xBE/0x12/0x75) while
    /// <see cref="_installSoundPlayer"/> was true.</summary>
    public IReadOnlyList<(int Frame, int SfxId)> SoundRequests => _soundRequests;

    public HeadlessIntroSimulation(
        string projectRoot, string worldName, EventProgramDocument document,
        bool installCellMutator = true, bool installSoundPlayer = true, bool installDialogueDirector = false)
    {
        _projectRoot = projectRoot;
        _worldName = worldName;
        _document = document;
        _installCellMutator = installCellMutator;
        _installSoundPlayer = installSoundPlayer;
        _installDialogueDirector = installDialogueDirector;
        _codesBytes = document.CodesAsBytes();
        _runner = new EventProgramRunner(document, _gameState, this)
        {
            TraceSink = OnOpcodeTraced,
            // Safety valve, diagnostic only (see EventProgramRunner.MaxIterationsPerCall's own
            // doc): a B/C program that loops on an unimplemented SUSPENDING opcode (e.g. 0x35/0x36
            // wait-flag - skipped by this V1 interpreter instead of suspending, see its class doc) would
            // otherwise spin forever inside one RunOneScriptCall, since this harness's own frame-level
            // stop conditions only ever get checked BETWEEN frames. 20000 is generous for any real map
            // 389 program (its longest observed program is well under 200 bytes).
            MaxIterationsPerCall = 20000,
            // E4.e: no more HarnessForceImmediateWalkCompletion / kind-Implemented forcing for 0x1E/0x1F/
            // 0x70/0x07 - this harness now drives real per-entity kinematics (RunVerticalPhysicsPass below
            // + the production ScriptedMotion.TickScriptedNpc horizontal integration every entity's
            // own Update already runs), so these opcodes suspend/resolve for real, exactly like production.
        };
        _wrapperRunner = new TraceAwareEntityRunner(this);
    }

    public void Run()
    {
        BuildInitialState();
        Frame = 0;
        RecordMapEntrySystemsOnce();

        const int hardCap = 3600;
        const int idleCap = 300;
        var framesSinceNewDistinct = 0;

        Frame = 1;
        while (Frame <= hardCap)
        {
            var before = _distinctTotal;

            try
            {
                RunFrame();
            }
            catch (RunawayTraceException)
            {
                StopReason = $"runaway guard: {MaxTotalDispatches} total dispatched opcodes reached (probable script loop through an unimplemented suspending opcode - see LoopBudgetExceeded hits), stopped mid-frame {Frame}.";
                FrameCount = Frame;
                return;
            }

            if (_sawOpcode11)
            {
                StopReason = $"opcode 0x11 (player gain control) dispatched on frame {Frame} - stop condition (a).";
                FrameCount = Frame;
                return;
            }

            framesSinceNewDistinct = _distinctTotal > before ? 0 : framesSinceNewDistinct + 1;

            if (framesSinceNewDistinct >= idleCap)
            {
                StopReason = $"no new distinct opcode/system first-occurrence for {idleCap} consecutive frames - stop condition (b), stopped at frame {Frame}.";
                FrameCount = Frame;
                return;
            }

            Frame++;
        }

        StopReason = $"hard cap of {hardCap} frames reached - stop condition (c).";
        FrameCount = hardCap;
    }

    /// <summary>
    /// E7.a test-only entry point (acceptance 3, docs/plan-e7-mutation-tuiles.md): builds the SAME
    /// initial state <see cref="Run"/> does, then drives exactly <paramref name="frameCount"/> frames via
    /// the SAME per-frame <see cref="RunFrame"/> (hence the SAME production
    /// <see cref="WorldProxy.RunMapEventsPass"/> call, hence the same real
    /// <see cref="EventProgramRunner.Dispatch"/> path 0x54/0x55/0x85 opcodes go through) - without
    /// <see cref="Run"/>'s own stop-condition/runaway-guard bookkeeping, since a full multi-thousand-frame
    /// intro trace is unnecessary just to observe the map-entry mutations dispatched on frame 1. Never
    /// called by <see cref="Run"/> itself - a separate entry point, not a change to it.
    /// </summary>
    public void RunFramesForTest(int frameCount, Action<HeadlessIntroSimulation>? afterEachFrame = null)
    {
        BuildInitialState();
        Frame = 0;
        RecordMapEntrySystemsOnce();

        for (Frame = 1; Frame <= frameCount; Frame++)
        {
            RunFrame();
            // Mirrors WorldProxy.Update's own per-tick dialogue pass (the F1 fix: the box's
            // advance/close belongs to the frame loop, not to opcode 0x39) - the harness IS the frame
            // owner here, exactly as it mirrors the other world-level passes. One tick per simulated
            // frame (the harness runs one logic tick per frame, per its own class doc).
            // E19.f2a (F2-R1): the order of a frame here is RunFrame (the scripts), the pass of the box, the callback of the test. The pass reads the pad
            // of the PREVIOUS frame: the mirror is updated from LastPadState.ButtonsHold, which the callback of the previous frame wrote - so a press is a
            // hold of one frame, followed by a release.
            if (_installDialogueDirector)
            {
                _dialoguePadMirror.Update(_gameState.LastPadState.ButtonsHold);
                Alundra.Dialogues.DialogueDirector.Instance.Pass(
                    (_dialoguePadMirror.ButtonsHold & PadState.Square) != 0,
                    (_dialoguePadMirror.ButtonsJustPressed & PadState.Square) != 0);
            }

            afterEachFrame?.Invoke(this);
        }

        FrameCount = frameCount;
    }

    // ------------------------------------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------------------------------------

    private void BuildInitialState()
    {
        // New Game hero spawn (GameEngine.cs ResetEntityState / map-entry block, port of
        // WorldProxy.SpawnPlayerEntity's own logical-field half - this harness has no engine
        // AssetCatalog/prefab to clone, so it builds the proxy directly): tile (33,59,0), anim 0x36
        // ("idle"), direction 0 - GameState's own New Game constants. Position is the tile-centre
        // 16.16 fixed-point form (TileWidth=24, TileHeight=16 - see EntityRecordMapper's own class doc for
        // the same constants).
        _player = new EntityScriptProxy
        {
            IsPlayer = true,
            EntityRefId = -1,
            Status = EntityStatus.Normal,
            EventTrigger = ScriptHelper.ProgramUnknown,
            TileX = GameState.CameraTileX,
            TileY = GameState.CameraTileY,
            TileZ = 0,
            PosX = (GameState.CameraTileX * 24 + 12) << 16,
            PosY = (GameState.CameraTileY * 16 + 8) << 16,
            PosZ = 0,
            TargetAnimationId = GameState.ResetAnimationId,
            CurrentAnimationId = GameState.ResetAnimationId,
            TargetDirection = GameState.ResetDirectionId,
            CurrentDirection = GameState.ResetDirectionId,
        };
        var playerOwnerEntity = new Entity();
        _player.LogicContextEntity = playerOwnerEntity;
        _player.ScriptHost = this;
        _player.Initialize(playerOwnerEntity); // gives Owner a value so Update's SyncAnimation/SyncTransform are no-ops instead of throwing
        _entityNames[_player] = "PlayerEntity";
        _spawnedEntities.Add(_player); // mirrors WorldProxy's own _spawnedEntities, which also holds the player

        var tileMapFile = Directory.GetFiles(
            Path.Combine(_projectRoot, "Maps"), $"{_worldName}.tileMap", SearchOption.AllDirectories).FirstOrDefault();

        if (tileMapFile == null)
        {
            throw new InvalidOperationException($"HeadlessIntroSimulation: no '{_worldName}.tileMap' found under '{_projectRoot}/Maps'.");
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFile)));
        _tileMapData = tileMapData;

        // E4.e (docs/plan-e4-deplacement-scripte.md): the real catalog/ground field/gravity constants of
        // map 389, same seam EntitySpawnFactory.ApplySpawnInitialization/ResolveMapGravitySettings already
        // use in production and NpcCharacterControllerMoverTests' own end-to-end spawn test already
        // exercises headless (new SpriteRecordCatalog(projectRoot), reading the real Data/sprite-records.json).
        // CellsCollisionField.TryCreate logs its own warning and leaves _groundField null on any
        // failure (missing/malformed AlundraCells, size mismatch) - RunVerticalPhysicsPass then no-ops,
        // same degraded-mode shape as every other missing-system fallback in this harness.
        _catalog = new SpriteRecordCatalog(_projectRoot);

        // E7.a: the 4-out overload hands back the SAME parsed CellsRecords _groundField itself
        // aliases its arrays from, so CellStore's mutations (0x54/0x55/0x85, via CellMutator below)
        // are instantly visible to _groundField's own TrySampleGround/SampleGroundProperty/
        // SampleRawCellHeight - no separate parse, no copy (see CellStore's own class doc).
        CellsCollisionField.TryCreate(tileMapData, _worldName, out _groundField, out var cellRecords);
        if (cellRecords != null)
        {
            CellStore.TryCreate(
                cellRecords, tileMapData.MapSize.Width, tileMapData.MapSize.Height, _worldName, out _cellStore);
        }

        tileMapData.CustomProperties.TryGetValue("Gravity", out var gravityRaw);
        int.TryParse(gravityRaw, out _mapGravityRaw);
        tileMapData.CustomProperties.TryGetValue("ZViscosity", out var zViscosityRaw);
        int.TryParse(zViscosityRaw, out _mapZViscosityRaw);

        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var mapEventsLayer = tileMapData.ObjectLayers.First(l => l.Name == "MapEvents");

        // InitializeMapEvents (GameEngine.cs:476-583), reusing WorldProxy.BuildMapEvents' own
        // record shape by hand (that method is private and takes a live World-resolved layer type - this
        // harness has no World): one MapEvent per record with EventCodesBIndex != 0, in record
        // order, Entity=PlayerEntity, fresh EventData.
        foreach (var record in mapEventsLayer.Objects)
        {
            var cp = record.CustomProperties;
            var programBMap = int.Parse(cp.GetValueOrDefault("EventCodesBIndex", "0"));
            if (programBMap == 0)
            {
                continue;
            }

            _mapEvents.Add(new MapEvent
            {
                Id = int.Parse(cp.GetValueOrDefault("Index", "0")),
                X1 = int.Parse(cp.GetValueOrDefault("X1", "0")),
                Y1 = int.Parse(cp.GetValueOrDefault("Y1", "0")),
                X2 = int.Parse(cp.GetValueOrDefault("X2", "0")),
                Y2 = int.Parse(cp.GetValueOrDefault("Y2", "0")),
                ProgramBMap = programBMap,
                OriginalProgramBMap = programBMap,
                Entity = _player,
            });
            _referencedBMap.Add(programBMap & 0x7F);
        }

        // Entity spawn: EntitySpawnFactory.ApplyRecord (EntityRecordMapper.Map + Status=Loaded +
        // EventTrigger=ProgramUnknown, reused verbatim), gated the same way GameEngine.SpawnEntity(null, i, 0)
        // gates InitializeEntitySlots' own map-load spawn pass (GameEngine.cs:684-717, notCheckSpawnZone=0):
        // the player-tile spawn-zone box (XMin/XMax/YMin/YMax vs the player's OWN tile) AND
        // EntitySpawnFactory.ShouldSpawnRecord's own two gates (IsEnabled==0, (SpriteDirection & 0x40) == 0) -
        // now the SAME production overload WorldProxy.InitializeWithWorld itself calls once a player
        // exists (EntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone, playerTileX, playerTileY,
        // out reason)), reused here instead of this harness's own former hand-rolled box check. On map 389
        // this drops the load-time spawn count from 19 to 14 (records 7/8/9 have SpriteDirection 128,
        // records 10/18 have SpriteDirection 0 - all four values have bit 0x40 clear).
        //
        // Every record (spawned or not) is also indexed by its own "Index" property into
        // _entityRecordsByIndex, so SpawnEntityByRecordId (opcodes 0x2D/0x8B) can dynamically spawn any
        // of them later, including the five gated out here.
        foreach (var record in entitiesLayer.Objects)
        {
            var cp = record.CustomProperties;
            var recordIndex = int.Parse(cp.GetValueOrDefault("Index", "0"));
            _entityRecordsByIndex[recordIndex] = record;

            if (!EntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone: false, _player.TileX, _player.TileY, out _))
            {
                continue;
            }

            // E4.e: EntitySpawnFactory.CreateBareEntityFromRecord instead of hand-wiring a proxy - this is
            // the SAME production seam CreateEntityFromRecord's own bare-fallback path uses (Entity with
            // GameplayProxyClassName -> entity.Initialize(), which resolves the proxy via ElementFactory
            // AND wires entity.GameplayProxy back to it - Entity.GameplayProxy has no public setter, so a
            // hand-built proxy/Entity pair the harness used to wire itself left entity.GameplayProxy null,
            // which made WorldProxy.SyncAnimation(Owner) - called every frame from this proxy's own
            // Update - a silent no-op: CurrentAnimationId never advanced off its spawn-time bit-complement,
            // so ScriptedMotion's own hasAnimSet lookup (keyed off CurrentAnimationId) always missed.
            // A latent bug invisible before E4.e (nothing depended on CurrentAnimationId actually changing
            // while every entity stood still), found via a real 0x1F walk that suspended forever. Real
            // Flags/AnimSetsByAnim/header body box (Width/Height/Depth/ModX/Y/Z) come along for free from
            // ApplySpawnInitialization, same as before - see this method's own doc, above, on _catalog.
            // Controller/RenderProjection stay null (the bare entity carries no components), which is
            // exactly the "bare-fallback" case ScriptedMotion.RunOneKinematicTick's own
            // Controller-null branch and RunVerticalPhysicsPass below are built to drive directly.
            var entity = EntitySpawnFactory.CreateBareEntityFromRecord(record, _catalog, tileMapData: tileMapData);
            var proxy = (EntityScriptProxy)entity.GameplayProxy!;
            proxy.ScriptHost = this; // ApplySpawnInitialization already set proxy.LogicContextEntity = entity

            var entityName = cp.GetValueOrDefault("EntityName", record.Name ?? "Entity");
            _entityNames[proxy] = $"{record.Name} ({entityName})";
            _spawnedEntities.Add(proxy);
            RegisterReferencedPrograms(proxy);

            // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): ONE support evaluation right at
            // spawn, same as production's own WorldProxy spawn loop - platform records (0-5) are
            // already in _spawnedEntities by the time a rider record (11+) is processed, in this SAME
            // sequential loop, over the SAME record order verified against the real export.
            EntitySupport.BuildCollidables(_spawnedEntities, _collidables);
            proxy.EvaluateEntitySupport(_collidables, immediateAtSpawn: true);
        }

        // E15.c T5/T7 (docs/plan-e15-yarn.md): opcode 0x0D/0x5C's own text resolution (both the real
        // dispatch AND this harness's own TryResolveDialogText trace annotation, T7) goes through the
        // REAL EventProgramRunner.MapDialogueAsset/SharedDialogueAsset seam - production wiring is
        // WorldProxy.InitializeWithWorld's own InstallDialogueAssets (AssetCatalog + the game's
        // asset manager); this harness has neither, so it loads the SAME compiled `.dialogue` files the
        // real export already wrote next to the map's own directory, directly off disk, with the
        // engine's own loader (no AssetCatalog/AssetContentManager needed for a single known file path).
        // No more best-effort `{world}.strings.json`/`_dialogueStrings` read here (T7 removed it): the
        // degraded path (contract item 8, no director) already plays this SAME asset headless through
        // EventProgramRunner.OpenDialog/PlayNodeHeadlessToEnd, so the trace annotation now reads
        // the identical Yarn text instead of a second, now-nonexistent source.
        _runner.MapDialogueAsset = LoadDialogueAssetBestEffort(
            Directory.GetFiles(Path.Combine(_projectRoot, "Maps"), $"{_worldName}.dialogue", SearchOption.AllDirectories).FirstOrDefault());
        _runner.SharedDialogueAsset = LoadDialogueAssetBestEffort(
            Path.Combine(_projectRoot, "Dialogues", "Shared.dialogue"));
    }

    /// <summary>Loads a single, already-compiled <c>.dialogue</c> file straight off disk (the engine's own
    /// <see cref="CasaEngine.Framework.Assets.Loaders.DialogueAssetLoader"/>, which needs no
    /// <c>AssetContentManager</c> for this) - null when the path is null/missing or fails to load,
    /// best-effort only.</summary>
    private static CasaEngine.Framework.Dialogue.Assets.DialogueAsset? LoadDialogueAssetBestEffort(string? fullPath)
    {
        if (fullPath == null || !File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return new CasaEngine.Framework.Assets.Loaders.DialogueAssetLoader().LoadAsset(fullPath, null!)
                as CasaEngine.Framework.Dialogue.Assets.DialogueAsset;
        }
        catch
        {
            return null; // best-effort only.
        }
    }

    private void RecordMapEntrySystemsOnce()
    {
        RecordSystemOnce("GameEngine.ClearTemporaryFlags", "GameEngine.cs:429", "clears g_temporaryFlags for the new map - AlundraGameState (this harness's flag store) already starts zeroed, no ported code re-clears it");
        RecordSystemOnce("GameEngine.ResetCameraAndLoadVRAMAssets", "GameEngine.cs:168-219 (map-entry block)", "camera reset + VRAM asset streaming - not ported");
        RecordSystemOnce("GameEngine.InitializeItems", "GameEngine.cs:168-219 (map-entry block)", "per-map item table init - not ported");
        RecordSystemOnce("EntityManager.InitializeEntitySlots -> ResetEntityState (player slot 0)", "GameEngine.cs:621-661", "player slot/status init - approximated manually by this harness's BuildInitialState, not literally ported");
        RecordSystemOnce("GameEngine.InitializeMapEvents", "GameEngine.cs:476-583", "ported by this harness's BuildInitialState (map-event slot construction), not by any production runtime code yet");
        RecordSystemOnce("EffectManager.InitializeEffectSlots", "GameEngine.cs:466-472 (LoadMapAndInitializeEntities)", "particle/sprite-effect slot pool init - not ported");
        RecordSystemOnce("GameEngine.WarpPlayer (fade setup, g_warpDelayFrames=10)", "GameEngine.cs:878", "screen fade-in + warp delay countdown init - not ported");
        RecordSystemOnce("GameEngine.InitializeScrollingMode", "GameEngine.cs:168-219 (map-entry block)", "camera scroll-mode setup - not ported");
        RecordSystemOnce("HudManager.InitializeHudPositionBeforeHide", "GameEngine.cs:168-219 (map-entry block)", "HUD position/visibility init - not ported");
        RecordSystemOnce("GameEngine.LoadMapSounds", "GameEngine.cs:168-219 (map-entry block)", "map BGM index resolution/playback - PORTED (docs/plan-e11c-musique.md, slice C1: WorldProxy.TriggerMapEntryMusic -> AlundraMusicPlayer.PlayMapMusic, map 389 -> music index 25); the sound-GROUP/ambient half (LoadMapSoundGroup) is NOT ported - the map -> VAB-group table is not exported (plan's own fact 2.1), same trou as E11.a's group parameter");
        RecordSystemOnce("GraphicManager.ResetDebugRenderingState", "GameEngine.cs:168-219 (map-entry block)", "debug rendering flags reset - not ported");
    }

    // ------------------------------------------------------------------------------------------------
    // Per-frame simulation
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// One frame: the map events, then the entities, then the pending triggers - the order of the production <see cref="WorldProxy"/> since E20.a (the binary's
    /// <c>UpdateWorld</c>: map events, then entities).
    /// </summary>
    private void RunFrame()
    {
        // E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): rebuild the shared collidables
        // snapshot BEFORE any entity's own Update runs this frame - fresher than production's own
        // WorldProxy (which can only refresh at the END of its own per-frame pass, one frame stale
        // for the entity Update loop that runs before it) since this harness fully owns its frame
        // ordering. EntitySupport.UpdateRidingEntities (search 5/6 fidelity) runs off the SAME snapshot.
        EntitySupport.BuildCollidables(_spawnedEntities, _collidables);
        EntitySupport.UpdateRidingEntities(_collidables);

        RecordSystemOnce("PadManager.UpdatePads()", "GameEngine.cs:1517 (Update)", "gamepad polling - CasaEngine's own InputComponent/GamePadManager exists but is not wired to this proxy's Update yet");

        // Snapshot BEFORE the pass, not the live _spawnedEntities list itself: 0x2D/0x8B
        // (SpawnEntityByRecordId) can append to _spawnedEntities WHILE a script is running inside this
        // same pass, and iterating the live list would otherwise throw "collection was modified" - a pure
        // harness-side artifact of reusing the mutable master list directly, not an engine bug (the
        // original never runs a same-frame pick/run pass over an entity spawned mid-pass either -
        // InitializeEntitySlots only picks up a new Loaded entity on ITS OWN next frame's pick phase, see
        // EntitySpawnFactory.ApplyRecord's own EventTrigger=ProgramUnknown seeding). A frozen snapshot
        // reproduces that: newly spawned entities join the simulation starting next frame, exactly like
        // the original.
        var entitiesThisFrame = _spawnedEntities.ToList();

        RecordSystemOnce("PlayerManager.MovePlayer()", "EntityManager.cs:808 / PlayerManager.cs:17", "player movement/input/physics - no player controller yet (E2's own scope); EntityScriptProxy.Update excludes the player from its own pick/run half, same as the original's own loop starting at slot index 1");

        // Bug fix (LogicClock's own class doc): this harness's own shared logic clock - advanced
        // here (this call becomes the frame's first caller; every entity's own Update below reads back the
        // SAME cached value) so ticksThisFrame is available for the world-level passes further down too,
        // without needing entitiesThisFrame to be non-empty. At this harness's fixed dt
        // (ScriptedMotion.FixedTickSeconds exactly, see below) this always yields exactly 1 - see
        // this class' own _logicClock doc.
        var ticksThisFrame = LogicTicksThisFrame(ScriptedMotion.FixedTickSeconds);

        // E4.e: ScriptedMotion.FixedTickSeconds (1/50 s), not 0f - the harness is frame-locked at
        // 50 Hz (1 frame = 1 tick, matching the original's own fixed physics rate), so this must feed
        // TickScriptedNpc's own accumulator exactly one whole tick's worth of elapsed time every frame;
        // 0f never crosses the accumulator's >= FixedTickSeconds threshold, so RunOneKinematicTick would
        // never run at all (silently, before E4.e this was unobservable - no NPC's own kinematics ran
        // before this tranche either way, gated on Controller != null, which was never true for a bare
        // harness proxy).
        // E20.a (D-E20-11): the map events run BEFORE the entities of the tick, the order of UpdateWorld (RunMapEvents 0x8003C67C, then UpdateEntities 0x8003B388), as the production
        // world now does; they used to come after the entity updates.
        if (PlayerEntity != null)
        {
            for (var tick = 0; tick < ticksThisFrame; tick++)
            {
                WorldProxy.RunMapEventsPass(PlayerEntity, _mapEvents, _wrapperRunner, _gameState.PlayerControlFlags);
            }
        }

        foreach (var entity in entitiesThisFrame)
        {
            entity.Update(ScriptedMotion.FixedTickSeconds);
        }

        for (var tick = 0; tick < ticksThisFrame; tick++)
        {
            WorldProxy.RunPendingEventTriggers(entitiesThisFrame, _wrapperRunner);
        }

        // Closes this frame's logic-clock memo (see LogicClock's own class doc) so the NEXT
        // RunFrame call's own LogicTicksThisFrame call recomputes fresh instead of reading this frame's
        // now-stale cached count.
        _logicClock.CloseFrame();

        RecordSystemOnce("EntityManager.UpdateDestroyedEntities", "EntityManager.cs:367-395 (UpdateEntities pass list)", "slot recycling for destroyed entities - not ported");
        RecordSystemOnce("EntityManager.UpdateEntitiesCounters", "EntityManager.cs:367-395 (UpdateEntities pass list)", "per-entity frame counters - not ported");
        RecordSystemOnce("EntityManager.UpdateEntityLists", "EntityManager.cs:367-395 (UpdateEntities pass list)", "active/renderable list rebuild - not ported");
        RecordSystemOnce("EntityManager.UpdateAnimation", "EntityManager.cs:209-224", "PARTIAL: AlundraFrameSyncPasses.SyncAnimation (called per-entity from EntityScriptProxy.Update) ports the target-resolution half (CurrentAnimationId/AnimationDirection) and StepAnimationClock the ends the scripts see (Hold flag, Chain, Loop counter) through the sprite's logical tick clock (E19.c2); NextFrameDelay and the frames themselves are not ported (the drawing is CasaEngine's own Animation2dCompositionSampler, in real time)");
        RecordSystemOnce("PhysicsEngine.UpdateEntitiesPhysics", "PhysicsEngine.cs:10", "PARTIAL (E4.e/E4.f): horizontal integration is ported per-entity, inside EntityScriptProxy.Update itself (AlundraScriptedMotion.TickScriptedNpc, run earlier this same frame, above); vertical gravity/ground-clamp + entity-vs-entity Z support (CheckEntityCollisionDown, EntitySupport.TryFindSupport) is ported here (RunVerticalPhysicsPass); CheckRidingEntities (search 5/6 fidelity) runs once per frame from RunFrame. Riding-platform FORCE feed for a MOVING platform (UpdateRidingEntity's own AdjustedForceX/Y propagation, MoveEntity's own PlatformEntity branch) is still not ported - map 389's intro platforms are all static (documented deviation, E14 for moving platforms).");
        // E4.e (docs/plan-e4-deplacement-scripte.md, decision E4-1): real per-entity vertical kinematics -
        // see RunVerticalPhysicsPass's own doc. Runs over the LIVE _spawnedEntities (not the frame-start
        // entitiesThisFrame snapshot): PhysicsEngine.UpdateEntitiesPhysics iterates g_activeEntityCount,
        // which the original's own UpdateEntityLists rebuilds (to include this SAME frame's spawns) right
        // before physics runs (EntityManager.cs:367-395) - so an entity 0x2D/0x8B spawns this frame (block
        // 18, the mouettes) gets gravity applied to it the very same frame it spawns, exactly like the
        // original, even though its own scripted Update (pick/run + horizontal integration) only starts
        // NEXT frame (see entitiesThisFrame's own doc, above, for why that half stays snapshot-based).
        RunVerticalPhysicsPass(_spawnedEntities);
        RecordSystemOnce("EntityManager.UpdateActiveEffects", "EntityManager.cs:367-395 (UpdateEntities pass list)", "not ported");
        RecordSystemOnce("EntityManager.UpdateBalanceRecords", "EntityManager.cs:367-395 (UpdateEntities pass list)", "combat/damage balance records - not ported");
        RecordSystemOnce("EntityManager.UpdateVisibleEntitiesZSort", "EntityManager.cs:367-395 (UpdateEntities pass list)", "not ported (WorldProxy.RunWallInterleaveSortKeyPass covers a narrower wall/sprite depth-interleave concern only, and is not even called by this harness - no rendering here)");

        RecordSystemOnce("EffectManager.UpdateEffects", "GameEngine.cs:1638-1664 (UpdateWorld)", "particle/sprite-effect tick - not ported");
        RecordSystemOnce("GameEngine.Update: g_warpDelayFrames--, inventory-open check", "GameEngine.cs:1500-1592", "warp fade countdown / inventory-open input gate - not ported");
        RecordSystemOnce("SoundManager.HandleMapSoundStreaming", "GameEngine.cs:1500-1592 (Update)", "deliberately NOT ported (docs/plan-e11c-musique.md, slice C1, fact 1.4/decision D-C-4): the frame-1 g_resetSoundFlag consumption is a re-arm, not a real restart - StartSequencePlayback never resets SeqPosition - and produces no audible difference on map 389, so porting it would add code without changing what is heard");
        RecordSystemOnce("Random.Next()", "GameEngine.cs:1500-1592 (Update)", "PSX-faithful RNG stream tick - not ported (nothing ported yet consumes randomness)");
    }

    /// <summary>
    /// E4.e simulated vertical kinematics (docs/plan-e4-deplacement-scripte.md, decision E4-1) - faithful
    /// port of <c>PhysicsEngine.ComputeZPosition</c>'s own flat-ground branch (PhysicsEngine.cs:109-166,
    /// <c>finalZVelocity &lt; 1</c> half only - the rising/<c>CheckEntityCollisionUp</c> half never lands,
    /// see below) plus the gravity/terminal-velocity integration normally done earlier, inside
    /// <c>UpdateEntityPhysics</c> (PhysicsEngine.cs:1460-1476, the <c>IsZForceApplied == 0</c> branch only -
    /// E4.b's own pre-read finding already established every AnimSet on map 389's intro carries
    /// <c>IsZForceApplied == 0</c>, so the sibling branches at :1478-1486 stay unported here too, same as
    /// production). Runs over every entity in <paramref name="entities"/> that is not
    /// <see cref="EntityStatus.Destroyed"/>/<see cref="EntityStatus.FlagToDestroy"/> - a no-op for the
    /// player (whose <see cref="EntityScriptProxy.Flags"/> stays 0, so the gravity branch below
    /// never applies to it, and whose <see cref="EntityScriptProxy.ForceZ"/> never becomes non-zero
    /// either) and for <see cref="_groundField"/> == null (degraded mode - CellsCollisionField
    /// missing/malformed for this world, already warned by <see cref="CellsCollisionField.TryCreate"/>
    /// itself).
    ///
    /// Ground probe: <see cref="ComputeTerrainHeight"/> below is the SAME 4-corner-max, far-edge-exclusive
    /// convention as <see cref="EntityScriptProxy.ClampToGround"/> and the original's own
    /// <c>ComputeEntityGroundHeight</c> (see <see cref="CellsCollisionField"/>'s own class doc) -
    /// reused here directly against the entity's real header body box
    /// (<see cref="EntityScriptProxy.Width"/>/<c>Height</c>/<c>ModX</c>/<c>ModY</c>, populated by
    /// <c>EntitySpawnFactory.ApplySpawnInitialization</c> at spawn - see <see cref="BuildInitialState"/>'s
    /// own doc on <see cref="_catalog"/>) rather than an engine <c>CollisionComponent</c> Box fixture (this
    /// harness's proxies carry no engine components at all).
    ///
    /// Landing: ported bit-for-bit including the original's own "+1" detail (PhysicsEngine.cs:128:
    /// <c>entity.PosZ = platformHeight - entity.ModZ</c> where <c>platformHeight = TerrainHeight + 1</c>
    /// when the collision is against terrain, not another entity) - <c>ModdedPosZ + FinalForceZ &lt;=
    /// TerrainHeight</c> (this tick's fall would put the entity's collision-box origin AT OR BELOW the
    /// ground) clamps <see cref="EntityScriptProxy.PosZ"/> to <c>TerrainHeight + 1 - ModZ</c> instead
    /// of applying <c>FinalForceZ</c> for real this tick, and zeroes <see cref="EntityScriptProxy.ForceZ"/>
    /// only while the Gravity flag is set (PhysicsEngine.cs:129-135, both branches return without falling
    /// through to <c>PosZ += finalZVelocity</c> below). <see cref="EntityScriptProxy.IsOnGround"/>
    /// is <c>UpdateTileAttributes</c>'s own formula (PhysicsEngine.cs:1704), simplified per the plan to
    /// read off this SAME 4-corner terrain probe rather than a separate single-point re-probe
    /// (<c>GetCollisionOnZ</c>, not ported) - documented deviation, harmless here since TerrainHeight does
    /// not depend on <c>PosZ</c> (only PosX/PosY, which the vertical pass never changes).
    ///
    /// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4; verifiers A1/A2): merged with
    /// <see cref="EntitySupport.TryFindSupport"/> - the SAME shared entity-vs-entity support detection
    /// <see cref="EntityScriptProxy.EvaluateEntitySupport"/> consumes for a controller-driven
    /// entity, reused here directly against <c>Pos*</c> (this harness's proxies carry no controller),
    /// including the FULL PhysicsEngine.cs:180-187/:205 seed (<c>Math.Max(naturalStep, TerrainHeight+1)</c>,
    /// passed to <c>TryFindSupport</c> so it only accepts a candidate this tick's own downward reach
    /// legitimately gets to) and the PhysicsEngine.cs:189 subject-eligibility gate
    /// (<see cref="EntitySupport.IsEligibleSubject"/>, checked before ever searching candidates - an
    /// ineligible entity falls back to terrain-only landing). The two candidates (terrain, best qualifying
    /// entity) are merged by taking whichever gives the HIGHER landing surface - the original's own
    /// <c>CheckEntityCollisionDown</c> does the same union, seeded from the terrain-based test before ever
    /// considering an entity candidate. <see cref="EntitySupport.UpdateRidingEntities"/> (search 5/6
    /// fidelity) runs separately, once per frame, from <see cref="RunFrame"/> - see that method's own doc;
    /// this pass never touches <see cref="EntityScriptProxy.RidingEntity"/>.
    /// </summary>
    private void RunVerticalPhysicsPass(IReadOnlyList<EntityScriptProxy> entities)
    {
        if (_groundField == null)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (entity.Status is EntityStatus.Destroyed or EntityStatus.FlagToDestroy)
            {
                continue;
            }

            // PhysicsEngine.cs:1460-1476 (UpdateEntityPhysics, IsZForceApplied == 0 branch): decay ForceZ
            // toward more-negative by Gravity<<8 every tick, but never past -(ZViscosity<<8) while
            // actually falling (the original only clamps the FALLING excess, never a rising ForceZ, e.g.
            // block 18's own 0x1B impulse - it simply decays under gravity like anything else).
            if ((entity.Flags & EntityFlags.Gravity) != 0)
            {
                var force = entity.ForceZ - (_mapGravityRaw << 8);
                var forceAbs = force < 0 ? -force : force;
                var terminal = _mapZViscosityRaw << 8;
                if (terminal < forceAbs && force < 1)
                {
                    force = -terminal;
                }

                entity.ForceZ = force;
            }

            // ApplyEntityForces' own FinalForceZ = ForceZ (PhysicsEngine.cs:1452).
            entity.FinalForceZ = entity.ForceZ;

            var terrainHeight = ComputeTerrainHeight(entity);
            entity.TerrainHeight = terrainHeight;

            var moddedPosZ = entity.PosZ + entity.ModZ;

            // Verifier A1 (PhysicsEngine.cs:180-187): the FULL original seed - this tick's own natural
            // step, clamped UP to TerrainHeight + 1 when it would go at-or-below terrain (algebraically
            // Math.Max, see EntitySupport.TryFindSupport's own doc) - passed to the entity-support search
            // so a falling entity only snaps onto a candidate its OWN downward reach this tick actually
            // gets to, not any overlapping candidate however far below.
            var platformTopZSeed = Math.Max(moddedPosZ + entity.FinalForceZ, terrainHeight + 1);
            var landingTop = terrainHeight + 1;

            // Verifier A2 (PhysicsEngine.cs:189): only an eligible subject is even tested against
            // candidates - an ineligible entity (not Collidable, or NoEntityCollision this frame) falls
            // back to terrain-only landing, same as CheckEntityCollisionDown never being called for it.
            if (EntitySupport.IsEligibleSubject(entity)
                && EntitySupport.TryFindSupport(entity, _collidables, platformTopZSeed, out _, out var supportTopZ)
                && supportTopZ > landingTop)
            {
                landingTop = supportTopZ;
            }

            if (entity.FinalForceZ < 1 && moddedPosZ + entity.FinalForceZ <= landingTop - 1)
            {
                entity.PosZ = landingTop - entity.ModZ;
                entity.CollidedWithEntityZ = landingTop > terrainHeight + 1 ? 1 : 0;
                if ((entity.Flags & EntityFlags.Gravity) != 0)
                {
                    entity.ForceZ = 0;
                }
            }
            else
            {
                entity.PosZ += entity.FinalForceZ;
            }

            // D-E7-11 (docs/plan-e7-mutation-tuiles.md, fact 5): TileX/TileY were never refreshed
            // ANYWHERE in this harness before this fix - only 0x64 (SetEntitiesPosition) ever writes
            // Pos*, and this harness's player never ticks a movement system of its own (E2's own
            // non-goal here), so a teleported player's own TileX/TileY stayed frozen at the New Game
            // spawn seed (33,59) forever, even after the intro's own two 0x64 calls (offsets 318/327)
            // moved it to its real (18,57) - a mismatch opcode 0x3B's real box tests would have silently
            // computed "just for the wrong reason". Mirrors production's own derivation
            // (EntityRecordMapper.cs:181-190 / ScriptedMotion.cs:248-249), alongside TileZ below,
            // which this pass already refreshed correctly - see this method's own class doc.
            entity.TileX = (entity.PosX >> 16) / TileWidthPx;
            entity.TileY = (entity.PosY >> 16) / TileHeightPx;
            entity.TileZ = entity.PosZ >> 20;
            // landingTop already carries the original's own "+1" (TerrainHeight+1 / candidateTop+1 -
            // see ComputeTerrainHeight/EntitySupport.TryFindSupport's own doc) - a landed entity's own
            // PosZ = landingTop - ModZ, so the comparator must be landingTop >= PosZ (NOT landingTop-1),
            // otherwise a just-landed entity (PosZ == landingTop when ModZ == 0) would read IsOnGround = 0
            // for one raw 16.16 unit's worth of "not quite there yet" - harmless in practice for X/Y, but
            // fatal for opcode 0x70 (Is above ground, Result = IsOnGround), which would never see 1.
            entity.IsOnGround = landingTop >= entity.PosZ ? 1 : 0;
        }
    }

    /// <summary>4-corner-max ground probe - see <see cref="RunVerticalPhysicsPass"/>'s own doc for the
    /// exact convention/rationale. Returns a 16.16 fixed-point height, same unit as
    /// <see cref="EntityScriptProxy.TerrainHeight"/>/<c>PosZ</c>.</summary>
    private int ComputeTerrainHeight(EntityScriptProxy entity)
    {
        var x1 = (entity.PosX + entity.ModX) >> 16;
        var x2 = (entity.PosX + entity.ModX + entity.Width) >> 16;
        var y1 = (entity.PosY + entity.ModY) >> 16;
        var y2 = (entity.PosY + entity.ModY + entity.Height) >> 16;

        var highest = int.MinValue;
        SampleCorner(x1, y1, ref highest);
        SampleCorner(x2, y1, ref highest);
        SampleCorner(x1, y2, ref highest);
        SampleCorner(x2, y2, ref highest);
        return highest == int.MinValue ? 0 : highest;

        void SampleCorner(int px, int py, ref int best)
        {
            if (_groundField!.TrySampleGround(new Vector3(px, py, 0f), float.MaxValue, out var sample) && sample.HasGround)
            {
                var height = (int)Math.Round((double)sample.GroundHeight * 65536.0);
                if (height > best)
                {
                    best = height;
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------
    // IEntityWorldContext - entity search/manipulation opcodes (0x2D/0x2E/0x62-0x65/0xAC/0x8B)
    // ------------------------------------------------------------------------------------------------

    public IReadOnlyList<EntityScriptProxy> SpawnedEntities => _spawnedEntities;

    public EntityScriptProxy? PlayerEntity => _player;

    /// <summary>E5.a: minimal support for opcodes 0x67/0x68/0x69 - this harness only needs the
    /// interpreter to dispatch them as [implemented] and advance by their real size (see
    /// <see cref="ImplementedOpcodes"/>); nothing here reads camera state, so the stored value is never
    /// consumed.</summary>
    public EntityScriptProxy? EntityFollowedByCamera { get; set; }

    public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;

    /// <summary>No navigation grid in this harness (E4.d/E4.e decision E4-1: "sans murs ni navigation" -
    /// the intro's own paths are unobstructed on map 389, see docs/plan-e4-deplacement-scripte.md) -
    /// degraded mode, same shape as every other missing-system fallback here. 0x1E's own navigation
    /// detour (<see cref="EventProgramRunner"/>'s Walk-with-collision bridge) simply never engages
    /// without a grid (its own <c>grid != null</c> gate), which is exactly the documented deviation: every
    /// 0x1E/0x1F occurrence on map 389 now suspends for its REAL distance/time and ends by the ORIGINAL
    /// distance test alone, never by a synthetic wall.</summary>
    public NavigationGrid2D? NavigationGrid => null;

    /// <summary>
    /// E7.a (docs/plan-e7-mutation-tuiles.md) - the "production call site" acceptance: this harness drives
    /// opcodes 0x54/0x55/0x85 through the REAL <see cref="EventProgramRunner.Dispatch"/> via
    /// <see cref="WorldProxy.RunMapEventsPass"/>/<see cref="TraceAwareEntityRunner.RunScript"/>
    /// exactly like production, backed by a real <see cref="Cells.CellStore"/> (see
    /// <see cref="_cellStore"/>'s own doc on why it shares its records with <see cref="_groundField"/>).
    /// <see cref="_installCellMutator"/> gates only THIS property's returned value - the neutralization
    /// twin (constructed with <c>installCellMutator: false</c>) still builds the same real store, it just
    /// never hands it to the interpreter, so its run takes the SAME degraded "CellMutator null" path
    /// production takes on a world with no cell store, proving the mutation actually flows through this
    /// exact seam rather than some other, accidental code path.
    /// </summary>
    public ICellMutator? CellMutator => _installCellMutator ? _cellStore : null;

    /// <summary>
    /// E11.a (docs/plan-e11-audio.md) - the "production call site" acceptance for T1: this harness
    /// drives opcodes 0xBD/0xBE/0x12/0x75 through the REAL <see cref="EventProgramRunner.Dispatch"/>
    /// via <see cref="WorldProxy.RunMapEventsPass"/>/<see cref="TraceAwareEntityRunner.RunScript"/>
    /// exactly like production, backed by itself as a fake <see cref="ISoundPlayer"/> (see
    /// <see cref="PlaySfx"/>). <see cref="_installSoundPlayer"/> gates only THIS property's returned
    /// value (T2's neutralization twin), same shape as <see cref="CellMutator"/> above.
    /// </summary>
    public ISoundPlayer? SoundPlayer => _installSoundPlayer ? this : null;

    /// <summary>
    /// E12.a (docs/plan-e12-dialogues.md) - the "production call site" for T1: this harness drives
    /// opcodes 0x0D/0x39/0x44/0x50/0x51/0x5C through the REAL <see cref="EventProgramRunner.Dispatch"/>
    /// via each entity's own <see cref="EntityScriptProxy.Update"/> -&gt; pick -&gt; run (the real
    /// slot-C path, NOT <see cref="WorldProxy.RunPendingEventTriggers"/>'s own D3 catch-up),
    /// exactly like production, backed by the SAME session-scoped <see cref="DialogueDirector.Instance"/>
    /// <see cref="WorldProxy.InstallDialogueSystems"/> installs. <see cref="_installDialogueDirector"/>
    /// gates only THIS property's returned value (default false - see that field's own doc on why, unlike
    /// every other <c>_install*</c> flag here).
    /// </summary>
    public IDialogueDirector? DialogueDirector => _installDialogueDirector ? Alundra.Dialogues.DialogueDirector.Instance : null;

    /// <summary>Fake <see cref="ISoundPlayer"/> implementation (T1, docs/plan-e11-audio.md):
    /// records the request instead of actually playing anything - no live AudioService in this
    /// headless harness.</summary>
    public void PlaySfx(int sfxId) => _soundRequests.Add((Frame, sfxId));

    /// <summary>B1 (docs/plan-e11b-opcodes-audio.md): same plain-recorder shape as <see cref="PlaySfx"/>
    /// above - no live AudioService here, so there is nothing to remix.</summary>
    public void RemixVoice(int sfxId, int left, int right)
    {
    }

    /// <summary>B1 (docs/plan-e11b-opcodes-audio.md): deliberately a no-op - this harness's own fake is a
    /// plain recorder with NO 64-slot anti-duplicate table of its own (the production table lives in the
    /// real <see cref="Audio.SoundPlayer"/>, exercised by <c>WorldProxyAudioInstallationTests</c>
    /// instead - see this DLL's own plan for why duplicating it here would be pointless).</summary>
    public void FlushFrameSounds()
    {
    }

    /// <summary>B2 (docs/plan-e11b-opcodes-audio.md): deliberately a no-op, same rationale as
    /// <see cref="FlushFrameSounds"/> above - a plain recorder with no live voices to stop.</summary>
    public void StopAllSfx()
    {
    }

    /// <summary>
    /// Dynamic spawn-by-record-id (opcodes 0x2D ActivateEntity, 0x8A SpawnEntityAtPosition, 0x8B SpawnEntityNextToEntity) - mirrors
    /// GameEngine.SpawnEntity (GameEngine.cs:684-760) called with notCheckSpawnZone=1, i.e. only
    /// EntitySpawnFactory.ShouldSpawnRecord's IsEnabled gate still applies (the 0x40 SpriteDirection gate
    /// and the player-tile spawn-zone box are both skipped, exactly like the original). Builds a fresh
    /// proxy from the same record BuildInitialState already indexed, via EntitySpawnFactory.ApplyRecord
    /// (EntityRecordMapper.Map + Status=Loaded + EventTrigger=ProgramUnknown) the load-time spawn path
    /// also uses, then seeds TargetAnimationId/TargetDirection/CurrentAnimationId/CurrentDirection exactly
    /// like WorldProxy's own ApplySpawnInitialization (WorldProxy.cs:645-654: animationId
    /// always 0, direction off AnimationTables.CardinalDirectionTable[SpriteDirection &amp; 0x3], Current*
    /// bit-complemented so the next animation sync always fires). Status=Loaded so its own next Update
    /// runs its Load slot (record 18's Load index is 0, so it goes through RunSpriteEvent's native-AI path
    /// instead - see EntityScriptProxy.RunPickedEvent's own programIndex==0 branch).
    ///
    /// Deviation: <see cref="EntityScriptProxy.ParentEntity"/> is left null - it is a live
    /// CasaEngine <c>Entity</c> reference and this harness's proxies are never wrapped in one (no engine
    /// World here), so there is nothing valid to assign; nothing this trace reads depends on it.
    /// </summary>
    public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId)
    {
        if (!_entityRecordsByIndex.TryGetValue(entityRecordId, out var record))
        {
            return null;
        }

        if (!EntitySpawnFactory.ShouldSpawnRecord(record, notCheckSpawnZone: true, out _))
        {
            return null;
        }

        // E4.e: EntitySpawnFactory.CreateBareEntityFromRecord instead of hand-wiring a proxy - see the
        // load-time spawn loop's own doc, above (BuildInitialState), on why. Block 18 (record 18) is
        // spawned this way (opcode 0x2D) and needs its real header box for RunVerticalPhysicsPass's own
        // ground probe, plus a proxy whose CurrentAnimationId actually advances via SyncAnimation.
        var entity = EntitySpawnFactory.CreateBareEntityFromRecord(record, _catalog, tileMapData: _tileMapData);
        var proxy = (EntityScriptProxy)entity.GameplayProxy!;
        proxy.ScriptHost = this; // ApplySpawnInitialization already set proxy.LogicContextEntity = entity

        var cp = record.CustomProperties;
        var spriteDirectionRaw = int.Parse(cp.GetValueOrDefault("SpriteDirection", "0"));
        var direction = AnimationTables.CardinalDirectionTable[spriteDirectionRaw & 0x3];
        proxy.TargetAnimationId = 0;
        proxy.TargetDirection = direction;
        proxy.CurrentAnimationId = ~0u;
        proxy.CurrentDirection = ~direction;

        var entityName = cp.GetValueOrDefault("EntityName", record.Name ?? "Entity");
        _entityNames[proxy] = $"{record.Name} ({entityName}) [dynamically spawned]";
        _spawnedEntities.Add(proxy);
        RegisterReferencedPrograms(proxy);

        // E4.f: same one-shot spawn-time support evaluation as the load-time spawn loop, above.
        EntitySupport.BuildCollidables(_spawnedEntities, _collidables);
        proxy.EvaluateEntitySupport(_collidables, immediateAtSpawn: true);

        _flatItems.Add($"{Frame} | SPAWN | record {entityRecordId} \"{entityName}\" by {_context}");

        return proxy;
    }

    public void DestroyEntity(EntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

    // ------------------------------------------------------------------------------------------------
    // IScriptHost - what each entity's own Update reaches for pick/run (see that interface's own
    // doc). ActiveCollisionEntity stays null (no collision system in V1, same as WorldProxy's own
    // field); DestroyEntity(entity, effectId) is the pick phase's own two-argument overload, distinct
    // from IEntityWorldContext.DestroyEntity(entity) above (the search-driven one-argument overload).
    // ------------------------------------------------------------------------------------------------

    public IEventProgramRunner Runner => _wrapperRunner;

    public EntityScriptProxy? ActiveCollisionEntity { get; set; }

    public void DestroyEntity(EntityScriptProxy entity, int effectId) => entity.Status = EntityStatus.FlagToDestroy;

    // E2: this harness builds its own player proxy directly (see BuildInitialState) with no
    // HeroController/World behind it - GameState is still the same shared flag store every other
    // pass here already reads (_gameState), but PlayerController stays null so
    // EntityScriptProxy.Update's own player branch (PlayerManager.MovePlayer/Tick) is a
    // documented no-op, exactly like "no player controller yet" used to read before E2 (see RunFrame's own
    // comment) - keeps this trace's frame count/opcode sequence unchanged.
    public GameState GameState => _gameState;

    public HeroController? PlayerController => null;

    /// <summary>
    /// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): this harness's own reused, per-frame
    /// collidables buffer - rebuilt at the top of every <see cref="RunFrame"/> (see that method's own
    /// doc), fresher than production's own one-frame-stale <c>WorldProxy._collidables</c> since the
    /// harness fully owns its own frame ordering (no external engine loop to contend with). Also rebuilt
    /// once in <see cref="BuildInitialState"/> right after the load-time spawn loop, and consulted
    /// directly (not through this property) by <see cref="ApplyImmediateSupportAtSpawn"/> for the
    /// spawn-time evaluation - see that method's own doc.
    /// </summary>
    public IReadOnlyList<EntityScriptProxy> Collidables => _collidables;

    private readonly List<EntityScriptProxy> _collidables = new();

    /// <summary>
    /// Bug fix (LogicClock's own class doc): this harness's own shared logic clock - same one
    /// instance every spawned entity's own <see cref="EntityScriptProxy.Update"/> AND
    /// <see cref="RunFrame"/> itself read/advance, mirroring production's <see cref="WorldProxy"/>.
    /// This harness runs at a FIXED dt (<see cref="ScriptedMotion.FixedTickSeconds"/> exactly) one
    /// frame at a time - see <see cref="RunFrame"/>'s own doc on why that makes
    /// <see cref="LogicClock.TicksThisFrame"/> yield exactly 1 every single frame, forever, so this
    /// fix is intentionally a no-op for the intro trace (see docs/intro-trace-389.txt's own acceptance
    /// note).
    /// </summary>
    private readonly LogicClock _logicClock = new();

    public int LogicTicksThisFrame(float elapsedTime) => _logicClock.TicksThisFrame(elapsedTime);

    /// <summary>Shared with the initial load-time spawn loop in <see cref="BuildInitialState"/> - registers
    /// a freshly spawned entity's non-zero A/C/F program indexes so the static disassembly annex covers
    /// dynamically spawned entities too, not just the ones present at map load.</summary>
    private void RegisterReferencedPrograms(EntityScriptProxy proxy)
    {
        if (proxy.ProgramIndexes[ScriptHelper.ProgramALoad] != 0)
        {
            _referencedALoad.Add(proxy.ProgramIndexes[ScriptHelper.ProgramALoad] & 0x7F);
        }

        if (proxy.ProgramIndexes[ScriptHelper.ProgramCTick] != 0)
        {
            _referencedCTick.Add(proxy.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7F);
        }

        if (proxy.ProgramIndexes[ScriptHelper.ProgramFInteract] != 0)
        {
            _referencedFInteract.Add(proxy.ProgramIndexes[ScriptHelper.ProgramFInteract] & 0x7F);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Trace-aware IEventProgramRunner wrapper - what each entity's own EntityScriptProxy.Update
    // (pick/run) and WorldProxy.RunMapEventsPass/RunPendingEventTriggers drive
    // ------------------------------------------------------------------------------------------------

    internal sealed class TraceAwareEntityRunner : IEventProgramRunner
    {
        private readonly HeadlessIntroSimulation _sim;

        public TraceAwareEntityRunner(HeadlessIntroSimulation sim)
        {
            _sim = sim;
        }

        /// <summary>
        /// Delegates straight to the REAL production interpreter (<see cref="EventProgramRunner.RunScript"/>,
        /// which now interprets every slot A-F - see that class's own doc) after setting the trace
        /// context. <see cref="WorldProxy.RunMapEventsPass"/> always calls this with
        /// <paramref name="entity"/> = the player and <paramref name="programSlot"/> = <see cref="ScriptHelper.ProgramBMap"/>
        /// - that specific combination gets the "MapEvent" context label instead of the generic
        /// per-entity one, and <see cref="HeadlessIntroSimulation.IntroCinematicProgramId"/> (129, map-event
        /// 0's own program) replaces the old index-based "is this map-event 0" check stop-condition (a)
        /// needs, since <see cref="WorldProxy.RunMapEventsPass"/> no longer hands this harness a
        /// per-map-event callback to hook an index off of.
        /// </summary>
        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
            if (programSlot == ScriptHelper.ProgramBMap && entity.IsPlayer)
            {
                var programId = entity.ProgramIndexes[ScriptHelper.ProgramBMap];
                _sim.SetContext(
                    $"MapEvent (prog {programId})",
                    isPlayerMapEvent: programId == IntroCinematicProgramId);
            }
            else
            {
                _sim.SetContext(_sim.DescribeEntityContext(entity, programSlot));
            }

            _sim._runner.RunScript(entity, programSlot);
        }

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
            _sim.SetContext(_sim.DescribeEntityContext(entity, -1));
            _sim.RecordSystemOnce(
                $"native sprite AI (entity {entity.EntityRefId} \"{_sim.NameOf(entity)}\" sprite {entity.SpriteTableIndex})",
                "SpriteEventHandlers.cs (g_entityEventFunctionsByType dispatch table, ~120 handlers)",
                "sprite-level AI - none ported (AlundraEventProgramRunner.RunSpriteEvent is a counted no-op)");
        }
    }

    private string DescribeEntityContext(EntityScriptProxy entity, int slot)
    {
        var slotName = slot switch
        {
            ScriptHelper.ProgramALoad => "A(Load)",
            ScriptHelper.ProgramBMap => "B(Map)",
            ScriptHelper.ProgramCTick => "C(Tick)",
            ScriptHelper.ProgramDTouch => "D(Touch)",
            ScriptHelper.ProgramEDeactivate => "E(Deactivate)",
            ScriptHelper.ProgramFInteract => "F(Interact)",
            _ => "AI",
        };
        var prog = slot is >= 0 and < 6 ? entity.ProgramIndexes[slot] : (int)entity.SpriteTableIndex;
        return $"Entity {entity.EntityRefId} \"{NameOf(entity)}\" slot {slotName} prog {prog}";
    }

    private string NameOf(EntityScriptProxy entity) => _entityNames.GetValueOrDefault(entity, "?");

    private void SetContext(string context, bool isPlayerMapEvent = false)
    {
        _context = context;
        _contextIsPlayerMapEvent = isPlayerMapEvent;
    }

    // ------------------------------------------------------------------------------------------------
    // Trace sink / ledger bookkeeping
    // ------------------------------------------------------------------------------------------------

    private sealed class RunawayTraceException : Exception
    {
    }

    private const int MaxTotalDispatches = 500_000;
    private int _totalDispatches;

    /// <summary>
    /// Trace-mode-only deviation (documented, see IntroTraceHarnessTests's own class doc, section 0):
    /// opcodes whose original handler is a pure PREDICATE that writes <c>EventProgramState.Result</c> off
    /// state this V1 interpreter does not have (absent dialog system for 0x39/0x44/0x51) are SKIPPED
    /// (UnknownSkipped), not suspended - see EventProgramRunner's own class doc. A skipped
    /// predicate leaves <c>Result</c> untouched (defaults to 0/"false"), which is exactly backwards for
    /// the extremely common original idiom "predicate; If false goto back": since the predicate can never
    /// become true without the missing system driving it, that idiom would spin in place FOREVER in this
    /// trace. Assuming these predicates TRUE ("optimistic") instead lets the trace-only script keep making
    /// forward progress past them, exactly as if the missing system had already satisfied the condition.
    /// NOT ported into the DLL - this mutates only the live <see cref="EventTraceRecord.State"/>
    /// reference, harness-side.
    ///
    /// E4.e retraits (docs/plan-e4-deplacement-scripte.md): 0x07 (Check entity in area) and 0x70 (Is above
    /// ground) are REMOVED from this set - both are now genuinely <c>Implemented</c> opcodes
    /// (EventProgramRunner.Dispatch cases 0x07/0x70) driven by this harness's own real per-entity
    /// kinematics (<see cref="RunVerticalPhysicsPass"/> for 0x70's <c>IsOnGround</c>; the entities' own
    /// real <see cref="EntityScriptProxy.TileX"/>/<c>TileY</c>/<c>TileZ</c>, refreshed every tick,
    /// for 0x07's tile-box search) - their <c>Result</c> is computed for real now, not assumed.
    ///
    /// E7.c retrait (2026-08-28, docs/plan-e7-mutation-tuiles.md, acceptance item 6): 0x2F ("Check moving
    /// in dir", D-E7-7 relabels it "Check pad buttons") is REMOVED too - now a genuinely
    /// <c>Implemented</c> opcode (EventProgramRunner.Dispatch case 0x2F) reading this harness's own
    /// real <see cref="GameState.LastPadState"/> (D-E7-8). Inert on map 389: fact 3 established
    /// 0x2F is dispatched ZERO times in this trace's own window (it always sits behind 0x3B, which stays
    /// false throughout - see <see cref="PessimisticPredicateOpcodes"/>'s own updated doc below), so this
    /// removal is dead-code cleanup, not an observed behaviour change.
    /// </summary>
    /// <summary>
    /// E12.a retrait (docs/plan-e12-dialogues.md, item ⑦): 0x39/0x44/0x51 are REMOVED - all three are now
    /// genuinely <c>Implemented</c>/<c>Degraded</c> opcodes (EventProgramRunner.Dispatch cases
    /// 0x39/0x44/0x51, backed by <see cref="Dialogues.DialogueDirector"/>) rather than <c>UnknownSkipped</c>,
    /// so this forcing set - which only ever applies to <c>UnknownSkipped</c> dispatches - self-neutralized
    /// for them the moment their real case landed (§1.6: "retirer les entrées du set est un nettoyage, pas
    /// un ré-étalonnage"). By DEFAULT this harness's own <see cref="HeadlessIntroSimulation.DialogueDirector"/>
    /// still returns null (no dialogue system wired) - a full intro trace run therefore takes the SAME
    /// "degraded, once-logged, size-only-advance" fallback these three opcodes' real dispatch cases
    /// already document for a null <see cref="IEntityWorldContext.DialogueDirector"/>, which is
    /// BEHAVIOURALLY IDENTICAL to the old forced-optimistic skip (Result=1 for 0x44, size-only advance
    /// for 0x39/0x51/0x0D/0x50/0x5C either way) - see each opcode's own dispatch doc. Only
    /// <c>DialogueOpcodesProductionTests</c> (E12.a's own T1) opts into a REAL, session-scoped
    /// <see cref="Dialogues.DialogueDirector"/> (<c>installDialogueDirector: true</c>), since that singleton
    /// must not leak into every OTHER pre-existing test built on this same harness.
    /// </summary>
    private static readonly HashSet<int> OptimisticPredicateOpcodes = new()
    {
    };

    /// <summary>
    /// Trace-mode-only deviation, the PESSIMISTIC counterpart to <see cref="OptimisticPredicateOpcodes"/>.
    /// E7.c retrait (2026-08-28, docs/plan-e7-mutation-tuiles.md, acceptance item 6): 0x3B "Check player in
    /// area" (Script_59_03B, EntityEventHandlers.cs:1223-1238) is REMOVED from this set - now a genuinely
    /// <c>Implemented</c> opcode (EventProgramRunner.Dispatch case 0x3B) testing the real
    /// <see cref="PlayerEntity"/>'s own <c>TileX</c>/<c>TileY</c>/<c>TileZ</c>, D-E7-11's own harness tile
    /// fix included (see <see cref="RunVerticalPhysicsPass"/>). Fact 4 (docs/plan-e7-mutation-tuiles.md):
    /// a real 0x3B still returns 0 (false) across this trace's own window - none of the five boxes map
    /// 389's own map-event/tick programs test - (18,18,38,38,8,8), (15,15,28,28,7,7), (21,21,28,28,7,7),
    /// (16,16,42,42,5,5), (15..21,32..40,25..30) - contains the player's real tile at any frame, so this
    /// removal changes only the trace's own <c>UnknownSkipped</c> -&gt; <c>Implemented</c> label, not any
    /// <c>Result</c> value: the old pessimistic forcing below was exactly right for the wrong reason (a
    /// frozen, PRE-D-E7-11 tile), now it is exactly right for the real one. Left as its own (now empty) set
    /// rather than deleted outright, matching this file's own precedent for a fully-retired forcing policy
    /// (see the class doc's own "E4.e" paragraph on 0x07/0x70 above).
    /// </summary>
    private static readonly HashSet<int> PessimisticPredicateOpcodes = new()
    {
    };

    private void OnOpcodeTraced(EventTraceRecord record)
    {
        EventOpcodeSizeTable.Entries.TryGetValue((byte)record.Opcode, out var entry);
        var name = entry?.Name ?? "?";
        var paramsText = record.Parameters is { Length: > 0 } ? string.Join(",", record.Parameters) : "";
        var dialog = TryResolveDialogText(record.Opcode, record.Parameters);
        _rawDispatchCount++;

        if (record.Kind == EventTraceKind.UnknownSkipped)
        {
            if (OptimisticPredicateOpcodes.Contains(record.Opcode))
            {
                record.State.Result = 1;
            }
            else if (PessimisticPredicateOpcodes.Contains(record.Opcode))
            {
                record.State.Result = 0;
            }
        }
        // E4.e: no more kind-Implemented Result forcing for 0x70/0x07 - RunVerticalPhysicsPass now
        // maintains a real EntityScriptProxy.IsOnGround (0x70) and TileX/TileY/TileZ (0x07) every
        // tick, so both opcodes compute their real Result off real state (same as production).

        // Stop condition (b) is progress-based: a NEW (context, pc) pair - reaching an instruction for
        // this context for the very first time - is progress. A suspended opcode re-entering the SAME pc
        // every frame (0x37 Wait, 0x36 WaitUntilFlagOn) is explicitly NOT progress by this definition.
        if (_seenContextPc.Add((_context, record.CodeIndex)))
        {
            _distinctTotal++;
        }
        // Trace compaction: extend the context's currently-open run in place when this dispatch is the
        // exact same (pc, opcode, kind, params, dialog) as last time AND lands on the very next frame -
        // this is what a suspended-and-retried opcode (0x37 Wait, 0x36 WaitUntilFlagOn) looks like across
        // frames. Anything else (first dispatch for this context, a different opcode/pc, or a frame gap)
        // starts a brand new run instead.
        if (_openRuns.TryGetValue(_context, out var openRun)
            && openRun.Pc == record.CodeIndex && openRun.Opcode == record.Opcode && openRun.Kind == record.Kind
            && openRun.ParamsText == paramsText && openRun.Dialog == dialog && openRun.LastFrame == Frame - 1)
        {
            openRun.LastFrame = Frame;
            openRun.Count++;
        }
        else
        {
            var run = new OpcodeRun
            {
                Context = _context,
                Pc = record.CodeIndex,
                Opcode = record.Opcode,
                Name = name,
                Kind = record.Kind,
                ParamsText = paramsText,
                Dialog = dialog,
                StartFrame = Frame,
                LastFrame = Frame,
                Count = 1,
            };
            _flatItems.Add(run);
            _openRuns[_context] = run;
        }

        switch (record.Kind)
        {
            case EventTraceKind.UnknownSkipped:
            case EventTraceKind.UnknownNoSizeTerminated:
                if (!_unimplementedFirstSeen.ContainsKey(record.Opcode))
                {
                    _unimplementedFirstSeen[record.Opcode] = (Frame, _context);
                }

                _unimplementedCount[record.Opcode] = _unimplementedCount.GetValueOrDefault(record.Opcode) + 1;

                if (record.Kind == EventTraceKind.UnknownNoSizeTerminated)
                {
                    _blindSpots.Add(record.Opcode);
                }

                break;

            case EventTraceKind.Implemented:
            case EventTraceKind.Degraded:
                if (!_implementedFirstSeen.ContainsKey(record.Opcode))
                {
                    _implementedFirstSeen[record.Opcode] = (Frame, _context);
                }

                _implementedCount[record.Opcode] = _implementedCount.GetValueOrDefault(record.Opcode) + 1;

                if (record.Kind == EventTraceKind.Degraded)
                {
                    _degradedCount[record.Opcode] = _degradedCount.GetValueOrDefault(record.Opcode) + 1;
                }

                break;

            case EventTraceKind.LoopBudgetExceeded:
                _loopBudgetHits.Add($"frame {Frame}: {_context} pc={record.CodeIndex}");
                break;
        }

        // Stop condition (a) only fires when 0x11 is dispatched on the PLAYER entity in a MapEvent
        // (slot B) context - not on an arbitrary NPC's own Tick/Interact program. 0x11 toggles the same
        // GLOBAL g_playerControlFlags regardless of who dispatches it, but only a B-slot dispatch on the
        // player genuinely means "the map-event script that locked the player is done" - an NPC's own
        // Tick program reaching 0x11 (see the sailor dialogue loop, program 140) is a script-local
        // artifact of this V1 interpreter's skip-path (see this file's own class doc), not a real
        // "player regains control" milestone.
        if (record.Opcode == 0x11 && _contextIsPlayerMapEvent)
        {
            _sawOpcode11 = true;
        }

        if (++_totalDispatches > MaxTotalDispatches)
        {
            throw new RunawayTraceException();
        }
    }

    private string? TryResolveDialogText(int opcode, byte[]? parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        // Script_OpenDialog_13_00D: TryOpenDialog((uint)variables[1], variables[2]) - dialog id is the
        // FIRST parameter byte. Script_OpenDialogWithChoice_05C: variables[1] is a search type, the
        // dialog id is the SECOND parameter byte (best-effort - see this file's own class doc caveat).
        // E12.a fix (docs/plan-e12-dialogues.md, item ⑥): parameters[0]/[1] is the RAW textId operand,
        // whose bit 0x80 selects local vs shared text (see EventProgramRunner's own dispatch case
        // 0x0D) - kept RAW here (not masked) since T7 (docs/plan-e15-yarn.md, contract item 1) needs that
        // same bit to resolve the SAME asset/node EventProgramRunner.ResolveDialogNode resolves for
        // the real dispatch.
        var textId = opcode switch
        {
            0x0D when parameters.Length >= 1 => (int)parameters[0],
            0x5C when parameters.Length >= 2 => (int)parameters[1],
            0xC4 when parameters.Length >= 4 => (int)parameters[3], // E19.b: v[4] is the text id.
            _ => -1,
        };

        if (textId < 0)
        {
            return null;
        }

        // T7: mirrors EventProgramRunner.ResolveDialogNode's own bit test and node-name formulas
        // (contract item 1), off the SAME MapDialogueAsset/SharedDialogueAsset BuildInitialState above
        // loaded - the same two assets the degraded path (contract item 8) plays headless - so this trace
        // annotation always names the node the real dispatch actually resolves and plays.
        CasaEngine.Framework.Dialogue.Assets.DialogueAsset? asset;
        string node;
        if ((textId & 0x80) != 0)
        {
            asset = _runner.MapDialogueAsset;
            node = $"M{_document.MapIndex}_S{textId & 0x7f:000}";
        }
        else
        {
            asset = _runner.SharedDialogueAsset;
            node = $"Shared_S{textId:000}";
        }

        if (asset == null || !asset.TryGetLineText($"line:{node}_p0", out var rawText))
        {
            return null;
        }

        var expanded = CasaEngine.Framework.Dialogue.Yarn.YarnLineTextParser.ExpandSubstitutions(rawText, Array.Empty<string>());
        var text = CasaEngine.Framework.Dialogue.Yarn.YarnLineTextParser.Parse(expanded).Text.Replace('\n', ' ');
        if (text.Length > 70)
        {
            text = text[..70] + "...";
        }

        return $"#{textId} \"{text}\" ({node})";
    }

    private void RecordSystemOnce(string name, string fileLine, string role)
    {
        if (_seenSystems.Add(name))
        {
            _systemLedger.Add((Frame, name, fileLine, role));
            _distinctTotal++;
            _flatItems.Add($"{Frame} | SYSTEM | {name} | {fileLine}");
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Output
    // ------------------------------------------------------------------------------------------------

    /// <summary>Formats <see cref="_flatItems"/> into final text lines - a plain string (system line) is
    /// passed through, an <see cref="OpcodeRun"/> is formatted via its own <see cref="OpcodeRun.ToLine"/>
    /// (showing a compacted <c>frames A-B (xN)</c> range once its run grew past a single dispatch).</summary>
    private List<string> FormatFlatItems()
    {
        var lines = new List<string>(_flatItems.Count);
        foreach (var item in _flatItems)
        {
            lines.Add(item switch
            {
                string s => s,
                OpcodeRun run => run.ToLine(),
                _ => item?.ToString() ?? "",
            });
        }

        return lines;
    }

    public string BuildTraceText()
    {
        var lines = FormatFlatItems();

        var sb = new StringBuilder();
        sb.AppendLine($"Intro trace - New Game -> map 389 (\"{_worldName}\")");
        sb.AppendLine($"Stop reason: {StopReason}");
        sb.AppendLine($"Frames simulated: {FrameCount}");
        sb.AppendLine($"Trace lines: {lines.Count} (compacted) / {_rawDispatchCount} (raw dispatches, before compaction) + {_systemLedger.Count} system lines");
        sb.AppendLine("Format: frame | context | pc=codeIndex | opcode 0xNN name | kind | params=[...]");
        sb.AppendLine("        frames A-B (xN) | context | ... (compacted: the SAME event was re-dispatched on N consecutive frames A..B for that context, nothing else happened for it in between - typical of a suspended 0x37 Wait / 0x36 WaitUntilFlagOn re-entering every frame while it waits)");
        sb.AppendLine("        frame | SYSTEM | system name | original file:line");
        sb.AppendLine("CAVEAT: unimplemented opcodes are SKIPPED here, not suspended - see this file's own class doc on AlundraEventProgramRunner / IntroTraceHarnessTests for why this is not real gameplay timing. NOTE: opcode 0x36 (Script_54_036, 'wait until flag on') IS now implemented (see AlundraEventProgramRunner.Dispatch case 0x36) and genuinely suspends across frames, same as 0x37 Wait - only opcodes still marked UnknownSkipped/UnknownNoSizeTerminated in this trace are affected by the skip-path caveat.");
        sb.AppendLine();

        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    public string BuildProgramsDisassemblyText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Static disassembly of every program map 389 (\"{_worldName}\") references");
        sb.AppendLine($"{_codesBytes.Length} code bytes total. [implemented]/[degraded]/[NOT IMPLEMENTED] tags reflect AlundraEventProgramRunner's V1 dispatch table.");
        sb.AppendLine();

        DumpSlot(sb, "A (Load)", _document.EventCodesATable, _referencedALoad);
        DumpSlot(sb, "B (Map)", _document.EventCodesBTable, _referencedBMap);
        DumpSlot(sb, "C (Tick)", _document.EventCodesCTable, _referencedCTick);
        DumpSlot(sb, "F (Interact)", _document.EventCodesFTable, _referencedFInteract);

        return sb.ToString();
    }

    private void DumpSlot(StringBuilder sb, string slotName, int[] table, SortedSet<int> maskedIndexes)
    {
        sb.AppendLine($"=== Slot {slotName} ===");

        foreach (var masked in maskedIndexes)
        {
            var offset = masked >= 0 && masked < table.Length ? table[masked] : -1;
            sb.AppendLine($"-- masked index {masked} - offset {(offset >= 0 ? offset.ToString() : "OUT OF RANGE")} --");

            if (offset >= 0)
            {
                foreach (var line in Disassemble(_codesBytes, offset))
                {
                    sb.AppendLine("  " + line);
                }
            }

            sb.AppendLine();
        }
    }

    private static List<string> Disassemble(byte[] codes, int startOffset)
    {
        var lines = new List<string>();
        var pos = startOffset;

        if (pos < 0 || pos >= codes.Length)
        {
            lines.Add($"{pos}: OUT OF RANGE");
            return lines;
        }

        while (pos < codes.Length)
        {
            var opcode = codes[pos];

            if (opcode == 0xFF)
            {
                lines.Add($"{pos}: 0xFF End");
                break;
            }

            if (!EventOpcodeSizeTable.Entries.TryGetValue(opcode, out var entry) || entry.Size <= 0)
            {
                lines.Add($"{pos}: 0x{opcode:X2} <unknown or size<=0 - cannot safely continue disassembly>");
                break;
            }

            var parameters = new List<byte>();
            for (var i = 1; i < entry.Size && pos + i < codes.Length; i++)
            {
                parameters.Add(codes[pos + i]);
            }

            // 0x00 (Break) is handled by RunOneScriptCall's own main loop, not the Dispatch switch (see
            // that method's own doc) - "implemented" in every sense that matters, just not through the
            // opcode table ImplementedOpcodes mirrors.
            var tag = opcode == 0x00 ? "[implemented - Break, handled by the main loop]"
                : opcode == 0xBD ? "[degraded]"
                : ImplementedOpcodes.Contains(opcode) ? "[implemented]" : "[NOT IMPLEMENTED]";
            lines.Add($"{pos}: 0x{opcode:X2} {entry.Name} {tag} params=[{string.Join(",", parameters)}]");
            pos += entry.Size;
        }

        return lines;
    }

    public string BuildSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Stop reason: {StopReason}");
        sb.AppendLine($"Frames simulated: {FrameCount}");
        sb.AppendLine($"Spawned entities: {_spawnedEntities.Count}, map events: {_mapEvents.Count}");
        sb.AppendLine($"Trace lines: {_flatItems.Count} (compacted) / {_rawDispatchCount} (raw dispatches)");
        sb.AppendLine($"Distinct unimplemented opcodes encountered: {_unimplementedFirstSeen.Count}");
        sb.AppendLine($"Distinct implemented/degraded opcodes encountered: {_implementedFirstSeen.Count}");
        sb.AppendLine($"Distinct absent/partial systems logged: {_systemLedger.Count}");

        if (_blindSpots.Count > 0)
        {
            sb.AppendLine($"BLIND SPOTS (unknown opcode with no known size - terminated its script call): {string.Join(", ", _blindSpots.Select(o => $"0x{o:X2}"))}");
        }

        if (_loopBudgetHits.Count > 0)
        {
            sb.AppendLine($"LOOP BUDGET HITS ({_loopBudgetHits.Count}, diagnostic MaxIterationsPerCall guard - a script looped through an unimplemented suspending opcode):");
            foreach (var hit in _loopBudgetHits.Take(20))
            {
                sb.AppendLine($"  {hit}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Unimplemented opcodes, in first-occurrence order:");
        foreach (var (opcode, (firstFrame, firstContext)) in _unimplementedFirstSeen.OrderBy(kv => kv.Value.FirstFrame))
        {
            EventOpcodeSizeTable.Entries.TryGetValue((byte)opcode, out var entry);
            sb.AppendLine($"  0x{opcode:X2} {entry?.Name ?? "?"} - first frame {firstFrame} ({firstContext}), count {_unimplementedCount[opcode]}");
        }

        sb.AppendLine();
        sb.AppendLine("Implemented/degraded opcodes encountered, in first-occurrence order (true dispatch counts, before compaction):");
        foreach (var (opcode, (firstFrame, firstContext)) in _implementedFirstSeen.OrderBy(kv => kv.Value.FirstFrame))
        {
            EventOpcodeSizeTable.Entries.TryGetValue((byte)opcode, out var entry);
            sb.AppendLine($"  0x{opcode:X2} {entry?.Name ?? "?"} - first frame {firstFrame} ({firstContext}), count {_implementedCount[opcode]}");
        }

        sb.AppendLine();
        sb.AppendLine("Absent/partial systems, in first-occurrence order:");
        foreach (var (firstFrame, name, fileLine, role) in _systemLedger)
        {
            sb.AppendLine($"  frame {firstFrame}: {name} ({fileLine}) - {role}");
        }

        return sb.ToString();
    }
}
