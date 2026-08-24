#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alundra.Tests;

/// <summary>
/// Headless trace harness for the "New Game -&gt; map 389 (Ship Klark (beginning))" intro, off the
/// converter's own output in alundra-project/ - self-skips when that directory is absent, exactly like
/// <see cref="Map389LoadProgramsTests"/>.
///
/// Purpose: replay the exact original frame structure (<see cref="HeadlessIntroSimulation"/>'s own doc
/// comment lists every original call this mirrors, with file:line) using the REAL bytecode interpreter
/// (<see cref="AlundraEventProgramRunner"/>, via its <see cref="AlundraEventProgramRunner.TraceSink"/>
/// seam) and the runtime's own already-ported passes (<see cref="AlundraEntityScriptProxy.PickEventTrigger"/>/
/// <see cref="AlundraEntityScriptProxy.RunPickedEvent"/>, <see cref="AlundraWorldProxy.RunMapEventsPass"/>,
/// <see cref="AlundraWorldProxy.RunPendingEventTriggers"/>, <see cref="EntityRecordMapper"/>,
/// <see cref="MapEventProgramLoader"/>), and produce an ordered trace of
/// every dispatched opcode plus every absent engine system, in the order the original would reach them.
/// Two artifacts are written under docs/ next to this checkout:
/// <list type="bullet">
/// <item><description><c>intro-trace-389.txt</c> - the full ordered trace.</description></item>
/// <item><description><c>intro-programs-389.txt</c> - a static linear disassembly of every program map
/// 389 references (slots A/B/C/F), independent of whether the trace actually walked every byte of
/// it.</description></item>
/// </list>
///
/// IMPORTANT CAVEAT (also stated in the generated docs): unimplemented opcodes are SKIPPED here, not
/// suspended - <see cref="AlundraEventProgramRunner"/>'s own documented V1 deviation (see its class doc).
/// So this trace's ordering is the linear "skip path" the interpreter actually takes, not real gameplay
/// timing: opcodes that would suspend for multiple frames in the original (dialog waits, animation waits,
/// 0x37 Wait's own multi-frame case) appear earlier/denser here than they would in real play. Flag-gated
/// branches use New Game's all-zero flags (see <see cref="AlundraGameState"/>'s class doc), except
/// whatever flags this same trace sets along the way via opcode 0x05.
/// </summary>
public class IntroTraceHarnessTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private readonly ITestOutputHelper _output;

    public IntroTraceHarnessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void IntroTrace_Map389_ProducesOrderedOpcodeAndSystemTrace()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!);
        sim.Run();

        // repo root = parent of alundra-project/
        var docsDir = Path.Combine(Directory.GetParent(projectRoot)!.FullName, "docs");
        Directory.CreateDirectory(docsDir);

        var tracePath = Path.Combine(docsDir, "intro-trace-389.txt");
        var programsPath = Path.Combine(docsDir, "intro-programs-389.txt");
        File.WriteAllText(tracePath, sim.BuildTraceText());
        File.WriteAllText(programsPath, sim.BuildProgramsDisassemblyText());

        _output.WriteLine(sim.BuildSummaryText());

        Assert.NotEmpty(sim.TraceLines);
        Assert.Contains(sim.TraceLines, line => line.StartsWith("1 | MapEvent"));
        Assert.True(File.Exists(tracePath));
        Assert.True(File.Exists(programsPath));
    }
}

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
/// <see cref="AlundraWorldProxy.RunMapEventsPass"/>'s own doc comment).</description></item>
/// <item><description><c>UpdateEntities</c> (EntityManager.cs:367-395):
/// <c>UpdateEntitiesEvents</c> (806, itself <c>MovePlayer()</c> then the pick/run passes - now each
/// entity's own <see cref="AlundraEntityScriptProxy.Update"/>, run directly by this harness's own frame
/// loop in engine order, then <see cref="AlundraWorldProxy.RunPendingEventTriggers"/> for decision D3's
/// catch-up re-scan), then <c>UpdateDestroyedEntities</c>/<c>Counters</c>/<c>Lists</c>/<c>Animation</c>,
/// <c>PhysicsEngine.UpdateEntitiesPhysics</c>, <c>UpdateActiveEffects</c>, <c>UpdateBalanceRecords</c>,
/// <c>UpdateVisibleEntitiesZSort</c>.</description></item>
/// </list>
///
/// Out of scope (documented non-goals, not simulated): rendering, real player movement/physics (no
/// controller/input/camera yet - E2/E5/E6), sound/effects/HUD. Dynamic entity spawn (opcodes 0x2D/0x8B,
/// via <see cref="SpawnEntityByRecordId"/>) IS simulated - see that method's own doc.
/// </summary>
internal sealed class HeadlessIntroSimulation : IEntityWorldContext, IAlundraScriptHost
{
    private const string WorldName_ = "Ship Klark (beginning)-389";

    /// <summary>Program id of map-event 0 (B 129), the intro's own cinematic driver - see
    /// <see cref="TraceAwareEntityRunner.RunScript"/>'s own doc for why this replaces the old
    /// index-based "is this map-event 0" check now that <see cref="AlundraWorldProxy.RunMapEventsPass"/>
    /// no longer hands this harness a per-map-event callback to hook context off of.</summary>
    private const int IntroCinematicProgramId = 129;

    private static readonly HashSet<int> ImplementedOpcodes = new()
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x09, 0x0A, 0x10, 0x11, 0x16, 0x17, 0x19, 0x1A, 0x1B,
        0x27, 0x2D, 0x2E, 0x30, 0x31, 0x36, 0x37, 0x38, 0x49, 0x4B, 0x5A, 0x5B, 0x62, 0x63, 0x64, 0x65,
        0x70, 0x8B, 0xAC,
    };

    private readonly string _projectRoot;
    private readonly string _worldName;
    private readonly EventProgramDocument _document;
    private readonly byte[] _codesBytes;
    private readonly AlundraGameState _gameState = new();
    private readonly AlundraEventProgramRunner _runner;
    private readonly TraceAwareEntityRunner _wrapperRunner;

    // Includes the player (index 0) - mirrors AlundraWorldProxy's own _spawnedEntities, which also holds
    // the player (SpawnPlayerEntity adds it before any record) - see IEntityWorldContext.SpawnedEntities's
    // own doc for why that matters (a search opcode must be able to find the player too).
    private readonly List<AlundraEntityScriptProxy> _spawnedEntities = new();
    private readonly Dictionary<AlundraEntityScriptProxy, string> _entityNames = new();
    private readonly Dictionary<int, TileMapObjectData> _entityRecordsByIndex = new(); // for SpawnEntityByRecordId (0x2D/0x8B)
    private readonly List<AlundraMapEvent> _mapEvents = new();

    private readonly SortedSet<int> _referencedALoad = new();
    private readonly SortedSet<int> _referencedBMap = new();
    private readonly SortedSet<int> _referencedCTick = new();
    private readonly SortedSet<int> _referencedFInteract = new();

    private List<string>? _dialogueStrings;
    private AlundraEntityScriptProxy _player = null!;
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
    private readonly SortedSet<int> _blindSpots = new(); // UnknownNoSizeTerminated opcodes
    private readonly List<string> _loopBudgetHits = new(); // LoopBudgetExceeded occurrences (diagnostic)

    private int _distinctTotal;
    private readonly HashSet<(string Context, int Pc)> _seenContextPc = new(); // stop condition (b), see OnOpcodeTraced

    public int Frame { get; private set; }
    public int FrameCount { get; private set; }
    public string? StopReason { get; private set; }
    public IReadOnlyList<string> TraceLines => FormatFlatItems();

    public HeadlessIntroSimulation(string projectRoot, string worldName, EventProgramDocument document)
    {
        _projectRoot = projectRoot;
        _worldName = worldName;
        _document = document;
        _codesBytes = document.CodesAsBytes();
        _runner = new AlundraEventProgramRunner(document, _gameState, this)
        {
            TraceSink = OnOpcodeTraced,
            // Safety valve, diagnostic only (see AlundraEventProgramRunner.MaxIterationsPerCall's own
            // doc): a B/C program that loops on an unimplemented SUSPENDING opcode (e.g. 0x35/0x36
            // wait-flag - skipped by this V1 interpreter instead of suspending, see its class doc) would
            // otherwise spin forever inside one RunOneScriptCall, since this harness's own frame-level
            // stop conditions only ever get checked BETWEEN frames. 20000 is generous for any real map
            // 389 program (its longest observed program is well under 200 bytes).
            MaxIterationsPerCall = 20000,
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

    // ------------------------------------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------------------------------------

    private void BuildInitialState()
    {
        // New Game hero spawn (GameEngine.cs ResetEntityState / map-entry block, port of
        // AlundraWorldProxy.SpawnPlayerEntity's own logical-field half - this harness has no engine
        // AssetCatalog/prefab to clone, so it builds the proxy directly): tile (33,59,0), anim 0x36
        // ("idle"), direction 0 - AlundraGameState's own New Game constants. Position is the tile-centre
        // 16.16 fixed-point form (TileWidth=24, TileHeight=16 - see EntityRecordMapper's own class doc for
        // the same constants).
        _player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            EntityRefId = -1,
            Status = EntityStatus.Normal,
            EventTrigger = ScriptHelper.ProgramUnknown,
            TileX = AlundraGameState.CameraTileX,
            TileY = AlundraGameState.CameraTileY,
            TileZ = 0,
            PosX = (AlundraGameState.CameraTileX * 24 + 12) << 16,
            PosY = (AlundraGameState.CameraTileY * 16 + 8) << 16,
            PosZ = 0,
            TargetAnimationId = AlundraGameState.ResetAnimationId,
            CurrentAnimationId = AlundraGameState.ResetAnimationId,
            TargetDirection = AlundraGameState.ResetDirectionId,
            CurrentDirection = AlundraGameState.ResetDirectionId,
        };
        var playerOwnerEntity = new Entity();
        _player.LogicContextEntity = playerOwnerEntity;
        _player.ScriptHost = this;
        _player.Initialize(playerOwnerEntity); // gives Owner a value so Update's SyncAnimation/SyncTransform are no-ops instead of throwing
        _entityNames[_player] = "PlayerEntity";
        _spawnedEntities.Add(_player); // mirrors AlundraWorldProxy's own _spawnedEntities, which also holds the player

        var tileMapFile = Directory.GetFiles(
            Path.Combine(_projectRoot, "Maps"), $"{_worldName}.tileMap", SearchOption.AllDirectories).FirstOrDefault();

        if (tileMapFile == null)
        {
            throw new InvalidOperationException($"HeadlessIntroSimulation: no '{_worldName}.tileMap' found under '{_projectRoot}/Maps'.");
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFile)));

        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var mapEventsLayer = tileMapData.ObjectLayers.First(l => l.Name == "MapEvents");

        // InitializeMapEvents (GameEngine.cs:476-583), reusing AlundraWorldProxy.BuildMapEvents' own
        // record shape by hand (that method is private and takes a live World-resolved layer type - this
        // harness has no World): one AlundraMapEvent per record with EventCodesBIndex != 0, in record
        // order, Entity=PlayerEntity, fresh EventData.
        foreach (var record in mapEventsLayer.Objects)
        {
            var cp = record.CustomProperties;
            var programBMap = int.Parse(cp.GetValueOrDefault("EventCodesBIndex", "0"));
            if (programBMap == 0)
            {
                continue;
            }

            _mapEvents.Add(new AlundraMapEvent
            {
                Id = int.Parse(cp.GetValueOrDefault("Index", "0")),
                X1 = int.Parse(cp.GetValueOrDefault("X1", "0")),
                Y1 = int.Parse(cp.GetValueOrDefault("Y1", "0")),
                X2 = int.Parse(cp.GetValueOrDefault("X2", "0")),
                Y2 = int.Parse(cp.GetValueOrDefault("Y2", "0")),
                ProgramBMap = programBMap,
                Entity = _player,
            });
            _referencedBMap.Add(programBMap & 0x7F);
        }

        // Entity spawn: AlundraWorldProxy.ApplyRecord (EntityRecordMapper.Map + Status=Loaded +
        // EventTrigger=ProgramUnknown, reused verbatim), gated the same way GameEngine.SpawnEntity(null, i, 0)
        // gates InitializeEntitySlots' own map-load spawn pass (GameEngine.cs:684-717, notCheckSpawnZone=0):
        // the player-tile spawn-zone box (XMin/XMax/YMin/YMax vs the player's OWN tile) AND
        // AlundraWorldProxy.ShouldSpawnRecord's own two gates (IsEnabled==0, (SpriteDirection & 0x40) == 0) -
        // now the SAME production overload AlundraWorldProxy.InitializeWithWorld itself calls once a player
        // exists (AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone, playerTileX, playerTileY,
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

            if (!AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone: false, _player.TileX, _player.TileY, out _))
            {
                continue;
            }

            var proxy = new AlundraEntityScriptProxy();
            AlundraWorldProxy.ApplyRecord(record, proxy);
            proxy.LogicContextEntity = new Entity();
            proxy.ScriptHost = this;
            proxy.Initialize(proxy.LogicContextEntity);

            var entityName = cp.GetValueOrDefault("EntityName", record.Name ?? "Entity");
            _entityNames[proxy] = $"{record.Name} ({entityName})";
            _spawnedEntities.Add(proxy);
            RegisterReferencedPrograms(proxy);
        }

        // Best-effort dialog text resolution (opcodes 0x0D/0x5C) off the map's own exported strings.
        var dialoguePath = Directory.GetFiles(
            Path.Combine(_projectRoot, "Maps"), $"{_worldName}.strings.json", SearchOption.AllDirectories).FirstOrDefault();
        if (dialoguePath != null)
        {
            try
            {
                _dialogueStrings = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(dialoguePath));
            }
            catch
            {
                _dialogueStrings = null; // best-effort only
            }
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
        RecordSystemOnce("GameEngine.LoadMapSounds", "GameEngine.cs:168-219 (map-entry block)", "map BGM/ambient sound table load - not ported");
        RecordSystemOnce("GraphicManager.ResetDebugRenderingState", "GameEngine.cs:168-219 (map-entry block)", "debug rendering flags reset - not ported");
    }

    // ------------------------------------------------------------------------------------------------
    // Per-frame simulation
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// One frame: entities first, then the world - same order as the production
    /// <see cref="AlundraWorldProxy"/>, since the engine's own <c>World.Update</c> always updates every
    /// entity (<c>GameplayProxy.Update</c>, here <see cref="AlundraEntityScriptProxy.Update"/>) before the
    /// world's own <c>GameplayProxy.Update</c> (CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:443-491).
    /// </summary>
    private void RunFrame()
    {
        RecordSystemOnce("PadManager.UpdatePads()", "GameEngine.cs:1517 (Update)", "gamepad polling - CasaEngine's own InputComponent/GamePadManager exists but is not wired to this proxy's Update yet");

        // Snapshot BEFORE the pass, not the live _spawnedEntities list itself: 0x2D/0x8B
        // (SpawnEntityByRecordId) can append to _spawnedEntities WHILE a script is running inside this
        // same pass, and iterating the live list would otherwise throw "collection was modified" - a pure
        // harness-side artifact of reusing the mutable master list directly, not an engine bug (the
        // original never runs a same-frame pick/run pass over an entity spawned mid-pass either -
        // InitializeEntitySlots only picks up a new Loaded entity on ITS OWN next frame's pick phase, see
        // AlundraWorldProxy.ApplyRecord's own EventTrigger=ProgramUnknown seeding). A frozen snapshot
        // reproduces that: newly spawned entities join the simulation starting next frame, exactly like
        // the original.
        var entitiesThisFrame = _spawnedEntities.ToList();

        RecordSystemOnce("PlayerManager.MovePlayer()", "EntityManager.cs:808 / PlayerManager.cs:17", "player movement/input/physics - no player controller yet (E2's own scope); AlundraEntityScriptProxy.Update excludes the player from its own pick/run half, same as the original's own loop starting at slot index 1");

        foreach (var entity in entitiesThisFrame)
        {
            entity.Update(0f);
        }

        if (PlayerEntity != null)
        {
            AlundraWorldProxy.RunMapEventsPass(PlayerEntity, _mapEvents, _wrapperRunner, _gameState.PlayerControlFlags);
        }

        AlundraWorldProxy.RunPendingEventTriggers(entitiesThisFrame, _wrapperRunner);

        RecordSystemOnce("EntityManager.UpdateDestroyedEntities", "EntityManager.cs:367-395 (UpdateEntities pass list)", "slot recycling for destroyed entities - not ported");
        RecordSystemOnce("EntityManager.UpdateEntitiesCounters", "EntityManager.cs:367-395 (UpdateEntities pass list)", "per-entity frame counters - not ported");
        RecordSystemOnce("EntityManager.UpdateEntityLists", "EntityManager.cs:367-395 (UpdateEntities pass list)", "active/renderable list rebuild - not ported");
        RecordSystemOnce("EntityManager.UpdateAnimation", "EntityManager.cs:209-224", "PARTIAL: AlundraWorldProxy.SyncAnimation (called per-entity from AlundraEntityScriptProxy.Update) ports the target-resolution half only (CurrentAnimationId/AnimationDirection); frame timing/AnimCompleteCounter/NextFrameDelay are not ported (owned by CasaEngine's own Animation2dCompositionSampler instead)");
        RecordSystemOnce("PhysicsEngine.UpdateEntitiesPhysics", "PhysicsEngine.cs:10", "gravity/collision/ground-height physics tick - not ported");
        RecordSystemOnce("EntityManager.UpdateActiveEffects", "EntityManager.cs:367-395 (UpdateEntities pass list)", "not ported");
        RecordSystemOnce("EntityManager.UpdateBalanceRecords", "EntityManager.cs:367-395 (UpdateEntities pass list)", "combat/damage balance records - not ported");
        RecordSystemOnce("EntityManager.UpdateVisibleEntitiesZSort", "EntityManager.cs:367-395 (UpdateEntities pass list)", "not ported (AlundraWorldProxy.RunWallInterleaveSortKeyPass covers a narrower wall/sprite depth-interleave concern only, and is not even called by this harness - no rendering here)");

        RecordSystemOnce("EffectManager.UpdateEffects", "GameEngine.cs:1638-1664 (UpdateWorld)", "particle/sprite-effect tick - not ported");
        RecordSystemOnce("GameEngine.Update: g_warpDelayFrames--, inventory-open check", "GameEngine.cs:1500-1592", "warp fade countdown / inventory-open input gate - not ported");
        RecordSystemOnce("SoundManager.HandleMapSoundStreaming", "GameEngine.cs:1500-1592 (Update)", "not ported");
        RecordSystemOnce("Random.Next()", "GameEngine.cs:1500-1592 (Update)", "PSX-faithful RNG stream tick - not ported (nothing ported yet consumes randomness)");
    }

    // ------------------------------------------------------------------------------------------------
    // IEntityWorldContext - entity search/manipulation opcodes (0x2D/0x2E/0x62-0x65/0xAC/0x8B)
    // ------------------------------------------------------------------------------------------------

    public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => _spawnedEntities;

    public AlundraEntityScriptProxy? PlayerEntity => _player;

    /// <summary>
    /// Dynamic spawn-by-record-id (opcodes 0x2D ActivateEntity, 0x8B SpawnEntityNextToEntity) - mirrors
    /// GameEngine.SpawnEntity (GameEngine.cs:684-760) called with notCheckSpawnZone=1, i.e. only
    /// AlundraWorldProxy.ShouldSpawnRecord's IsEnabled gate still applies (the 0x40 SpriteDirection gate
    /// and the player-tile spawn-zone box are both skipped, exactly like the original). Builds a fresh
    /// proxy from the same record BuildInitialState already indexed, via AlundraWorldProxy.ApplyRecord
    /// (EntityRecordMapper.Map + Status=Loaded + EventTrigger=ProgramUnknown) the load-time spawn path
    /// also uses, then seeds TargetAnimationId/TargetDirection/CurrentAnimationId/CurrentDirection exactly
    /// like AlundraWorldProxy's own ApplySpawnInitialization (AlundraWorldProxy.cs:645-654: animationId
    /// always 0, direction off AnimationTables.CardinalDirectionTable[SpriteDirection &amp; 0x3], Current*
    /// bit-complemented so the next animation sync always fires). Status=Loaded so its own next Update
    /// runs its Load slot (record 18's Load index is 0, so it goes through RunSpriteEvent's native-AI path
    /// instead - see AlundraEntityScriptProxy.RunPickedEvent's own programIndex==0 branch).
    ///
    /// Deviation: <see cref="AlundraEntityScriptProxy.ParentEntity"/> is left null - it is a live
    /// CasaEngine <c>Entity</c> reference and this harness's proxies are never wrapped in one (no engine
    /// World here), so there is nothing valid to assign; nothing this trace reads depends on it.
    /// </summary>
    public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId)
    {
        if (!_entityRecordsByIndex.TryGetValue(entityRecordId, out var record))
        {
            return null;
        }

        if (!AlundraWorldProxy.ShouldSpawnRecord(record, notCheckSpawnZone: true, out _))
        {
            return null;
        }

        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        proxy.LogicContextEntity = new Entity();
        proxy.ScriptHost = this;
        proxy.Initialize(proxy.LogicContextEntity);

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

        _flatItems.Add($"{Frame} | SPAWN | record {entityRecordId} \"{entityName}\" by {_context}");

        return proxy;
    }

    public void DestroyEntity(AlundraEntityScriptProxy entity) => entity.Status = EntityStatus.FlagToDestroy;

    // ------------------------------------------------------------------------------------------------
    // IAlundraScriptHost - what each entity's own Update reaches for pick/run (see that interface's own
    // doc). ActiveCollisionEntity stays null (no collision system in V1, same as AlundraWorldProxy's own
    // field); DestroyEntity(entity, effectId) is the pick phase's own two-argument overload, distinct
    // from IEntityWorldContext.DestroyEntity(entity) above (the search-driven one-argument overload).
    // ------------------------------------------------------------------------------------------------

    public IEventProgramRunner Runner => _wrapperRunner;

    public AlundraEntityScriptProxy? ActiveCollisionEntity => null;

    public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) => entity.Status = EntityStatus.FlagToDestroy;

    // E2: this harness builds its own player proxy directly (see BuildInitialState) with no
    // AlundraPlayerController/World behind it - GameState is still the same shared flag store every other
    // pass here already reads (_gameState), but PlayerController stays null so
    // AlundraEntityScriptProxy.Update's own player branch (AlundraPlayerManager.MovePlayer/Tick) is a
    // documented no-op, exactly like "no player controller yet" used to read before E2 (see RunFrame's own
    // comment) - keeps this trace's frame count/opcode sequence unchanged.
    public AlundraGameState GameState => _gameState;

    public AlundraPlayerController? PlayerController => null;

    /// <summary>Shared with the initial load-time spawn loop in <see cref="BuildInitialState"/> - registers
    /// a freshly spawned entity's non-zero A/C/F program indexes so the static disassembly annex covers
    /// dynamically spawned entities too, not just the ones present at map load.</summary>
    private void RegisterReferencedPrograms(AlundraEntityScriptProxy proxy)
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
    // Trace-aware IEventProgramRunner wrapper - what each entity's own AlundraEntityScriptProxy.Update
    // (pick/run) and AlundraWorldProxy.RunMapEventsPass/RunPendingEventTriggers drive
    // ------------------------------------------------------------------------------------------------

    internal sealed class TraceAwareEntityRunner : IEventProgramRunner
    {
        private readonly HeadlessIntroSimulation _sim;

        public TraceAwareEntityRunner(HeadlessIntroSimulation sim)
        {
            _sim = sim;
        }

        /// <summary>
        /// Delegates straight to the REAL production interpreter (<see cref="AlundraEventProgramRunner.RunScript"/>,
        /// which now interprets every slot A-F - see that class's own doc) after setting the trace
        /// context. <see cref="AlundraWorldProxy.RunMapEventsPass"/> always calls this with
        /// <paramref name="entity"/> = the player and <paramref name="programSlot"/> = <see cref="ScriptHelper.ProgramBMap"/>
        /// - that specific combination gets the "MapEvent" context label instead of the generic
        /// per-entity one, and <see cref="HeadlessIntroSimulation.IntroCinematicProgramId"/> (129, map-event
        /// 0's own program) replaces the old index-based "is this map-event 0" check stop-condition (a)
        /// needs, since <see cref="AlundraWorldProxy.RunMapEventsPass"/> no longer hands this harness a
        /// per-map-event callback to hook an index off of.
        /// </summary>
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
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

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
            _sim.SetContext(_sim.DescribeEntityContext(entity, -1));
            _sim.RecordSystemOnce(
                $"native sprite AI (entity {entity.EntityRefId} \"{_sim.NameOf(entity)}\" sprite {entity.SpriteTableIndex})",
                "SpriteEventHandlers.cs (g_entityEventFunctionsByType dispatch table, ~120 handlers)",
                "sprite-level AI - none ported (AlundraEventProgramRunner.RunSpriteEvent is a counted no-op)");
        }
    }

    private string DescribeEntityContext(AlundraEntityScriptProxy entity, int slot)
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

    private string NameOf(AlundraEntityScriptProxy entity) => _entityNames.GetValueOrDefault(entity, "?");

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
    /// state this V1 interpreter does not have (absent physics for 0x07/0x2F/0x70, absent dialog system
    /// for 0x39/0x44/0x51) are SKIPPED (UnknownSkipped), not suspended - see
    /// AlundraEventProgramRunner's own class doc. A skipped predicate leaves <c>Result</c> untouched
    /// (defaults to 0/"false"), which is exactly backwards for the extremely common original idiom
    /// "predicate; If false goto back": since the predicate can never become true without the missing
    /// system driving it (an entity can never reach an area or the ground without movement), that idiom
    /// would spin in place FOREVER in this trace - precisely what stalled Entity 18's Tick program on
    /// 0x70 two rounds ago. Assuming these predicates TRUE ("optimistic") instead lets the trace-only
    /// script keep making forward progress past them, exactly as if the missing system had already
    /// satisfied the condition. NOT ported into the DLL - this mutates only the live
    /// <see cref="EventTraceRecord.State"/> reference, harness-side.
    /// </summary>
    private static readonly HashSet<int> OptimisticPredicateOpcodes = new()
    {
        0x07, // Check entity in area
        0x2F, // Check moving in dir
        0x70, // Is above ground (Result = logicEntity.IsOnGround, EntityEventHandlers.cs:2161-2165)
        0x39, // Wait for dialog
        0x44, // Wait dialog choice
        0x51, // Get dialog choice
    };

    /// <summary>
    /// Trace-mode-only deviation, the PESSIMISTIC counterpart to <see cref="OptimisticPredicateOpcodes"/>:
    /// 0x3B "Check player in area" (Script_59_03B, EntityEventHandlers.cs:1223-1238) tests
    /// PlayerEntity.TileX/TileY/TileZ against a static box [v1..v2]x[v3..v4]x[v5..v6]. This runner has no
    /// player-movement system yet (lot 2's own non-goal), so the player stays exactly where B1 froze it
    /// for the whole intro - tile (33,59) per New Game spawn, or wherever a scripted warp/teleport last
    /// left it. Every 0x3B box map 389's own map-event/tick programs actually test - (18,18,38,38,8,8),
    /// (15,15,28,28,7,7), (21,21,28,28,7,7), (16,16,42,42,5,5), (15..21,32..40,25..30) - excludes the
    /// player's frozen tile, so Result=0 (pessimistic/false) is not a guess here: it is EXACTLY what the
    /// original computes for this scenario, unlike the genuinely-missing-system predicates above (where
    /// optimism is the only way to avoid an artificial forever-loop). Kept as its own set (rather than
    /// folded into <see cref="OptimisticPredicateOpcodes"/> with an inverted meaning) so the two policies
    /// stay visually and semantically distinct at the call site below.
    /// </summary>
    private static readonly HashSet<int> PessimisticPredicateOpcodes = new()
    {
        0x3B, // Check player in area
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
        else if (record.Kind == EventTraceKind.Implemented && (record.Opcode == 0x70 || record.Opcode == 0x07))
        {
            // E4.b/E4.c harness-only deviation (docs/plan-e4-deplacement-scripte.md E4.b/E4.c acceptance
            // notes): 0x70 and 0x07 are now genuinely Implemented (AlundraEventProgramRunner.Dispatch
            // cases 0x70/0x07), but this harness drives entities with no controller/kinematics of its own
            // before E4.e - AlundraEntityScriptProxy.IsOnGround stays its C# default 0 (falling), and no
            // entity here ever actually MOVES its TileX/TileY/TileZ into a real box, so a genuine
            // Result=IsOnGround / Result=(tile in box) would still spin the "0x70/0x07 -> 0x04" idioms
            // forever, same reasoning as OptimisticPredicateOpcodes above, just now applying to
            // Implemented dispatches instead of UnknownSkipped ones. Removed once E4.e gives this harness
            // its own simulated kinematics/ground field (see this file's own class doc, section 0) -
            // 0x2F stays optimistic/unimplemented regardless (dialogue/doors, out of E4's own scope).
            record.State.Result = 1;
        }

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
        if (_dialogueStrings == null || parameters == null)
        {
            return null;
        }

        // Script_OpenDialog_13_00D: TryOpenDialog((uint)variables[1], variables[2]) - dialog id is the
        // FIRST parameter byte. Script_OpenDialogWithChoice_05C: variables[1] is a search type, the
        // dialog id is the SECOND parameter byte (best-effort - see this file's own class doc caveat).
        var id = opcode switch
        {
            0x0D when parameters.Length >= 1 => (int)parameters[0],
            0x5C when parameters.Length >= 2 => (int)parameters[1],
            _ => -1,
        };

        if (id < 0 || id >= _dialogueStrings.Count)
        {
            return null;
        }

        var text = _dialogueStrings[id].Replace("\\N", " ").Replace("\\C", "").Replace('\n', ' ');
        if (text.Length > 70)
        {
            text = text[..70] + "...";
        }

        return $"#{id} \"{text}\"";
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

/// <summary>
/// One line of the compacted trace output (trace-compaction step, see the coordinator's follow-up):
/// a single dispatched-opcode event, OR a run of the SAME (context, pc, opcode, kind, params, dialog)
/// re-dispatched on consecutive frames with nothing else happening for that context in between (the
/// normal shape of a suspended 0x37 Wait / 0x36 WaitUntilFlagOn re-entering every frame while it waits).
/// Mutated in place by <see cref="HeadlessIntroSimulation.OnOpcodeTraced"/> while its run keeps
/// extending; formatted to text only at output time.
/// </summary>
internal sealed class OpcodeRun
{
    public string Context = "";
    public int Pc;
    public int Opcode;
    public string Name = "";
    public EventTraceKind Kind;
    public string ParamsText = "";
    public string? Dialog;
    public int StartFrame;
    public int LastFrame;
    public int Count;

    public string ToLine()
    {
        var frameText = Count > 1 ? $"{StartFrame}-{LastFrame} (x{Count})" : StartFrame.ToString();
        var dialogText = Dialog != null ? $" | dialog: {Dialog}" : "";
        return $"{frameText} | {Context} | pc={Pc} | opcode 0x{Opcode:X2} {Name} | {Kind} | params=[{ParamsText}]{dialogText}";
    }
}
