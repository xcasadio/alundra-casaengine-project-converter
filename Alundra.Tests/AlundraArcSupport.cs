#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>What an arc loads and runs (E19.a T5, docs/plan-e19-opcodes.md §1.3): one real exported map, the
/// flags already set, the hero placed, and the frame budget after which the arc FAILS naming where each program
/// stopped.</summary>
/// <param name="Name">The arc's name in the plan (A0, A0b, A1...), quoted by its failure message.</param>
/// <param name="Zone">The folder of the map under <c>Maps/</c> (<c>The Klark</c>).</param>
/// <param name="WorldName">The world's own name (<c>Ship Klark (inner)-390</c>), also its folder name.</param>
/// <param name="Flags">The persistent game flags set before the first frame (<c>G866</c> is 866).</param>
/// <param name="HeroTileX">The hero's tile, set with its position before the first frame.</param>
/// <param name="HeroTileY">See <paramref name="HeroTileX"/>.</param>
/// <param name="HeroTileZ">See <paramref name="HeroTileX"/>; <c>TileZ = PosZ &gt;&gt; 20</c> exactly.</param>
/// <param name="FrameLimit">The frame budget of the arc.</param>
/// <param name="RealController">E19.a2: the hero is a real <see cref="CharacterControllerComponent"/> set like the
/// export's, and every frame is the world's own <c>Update</c> (which moves the entities), so walks meet the map's
/// cells and rails. Off (the default), the entities are bare and <see cref="ArcRun.OneFrame"/> updates them one by
/// one.</param>
/// <param name="Prefabs">E19.c1 (D-E19-14): the NPCs are the export's real prefabs, loaded by the production spawn path
/// through an <see cref="AssetContentManager"/> built by the test (<see cref="ArcPrefabAssets"/>). Requires
/// <see cref="RealController"/>. Off (the default), every NPC is a bare entity.</param>
/// <param name="Arrival">E19.d2a S3 (U3): when given, the arc starts from a real portal arrival: the record is posed on the warp
/// director before the map entry, <c>AdoptPlayerPawn</c> (and its <c>ClampToGround</c>) places the hero as in production, and the
/// arc writes neither the hero's position nor its <c>Tile*</c> (<see cref="HeroTileX"/> to <see cref="HeroTileZ"/> are then unused).</param>
internal sealed record ArcSpec(
    string Name, string Zone, string WorldName, int[] Flags, int HeroTileX, int HeroTileY, int HeroTileZ, int FrameLimit,
    bool RealController = false, bool Prefabs = false, ArcArrival? Arrival = null);

/// <summary>E19.d2a S3 (U3): a real portal arrival record - what <c>AlundraWarpDirector.BeginDepartureCore</c> writes and
/// <c>AdoptPlayerPawn</c> consumes. Positions are in 16.16 (<c>&lt;&lt; 16</c>) for X and Y and <c>&lt;&lt; 20</c> for Z
/// (<c>ZLevel &lt;&lt; 20</c>, not yet the ground height: the adoption raises it), the animation is <c>0x36</c> for a portal.</summary>
internal sealed record ArcArrival(int PosX, int PosY, int PosZ, uint AnimationId, uint DirectionId);

/// <summary>One instruction of the arc's trace: the frame it ran in, the program it belongs to (slot and start
/// code index, the identity a trace record can give without its owner), and its outcome.</summary>
internal readonly record struct ArcInstruction(int Frame, int Slot, int ProgramStart, int Pc, int Opcode, EventTraceKind Kind);

/// <summary>
/// E19.a T5 (docs/plan-e19-opcodes.md §0.2.6, §1.2 T5): the shared support of the chain arcs, built on the montage
/// of <see cref="AlundraSaveBookEndToEndTests"/> - the real <see cref="AlundraWorldProxy.InitializeWithWorld"/>
/// over a real exported map, a hero possessed by a real <see cref="AlundraPlayerController"/> with a pad seam, a
/// real dialogue presenter over a recording UI view, one logic tick per <c>Update(0.02f)</c>. On top of it:
/// <list type="bullet">
/// <item><description>the world is built from the map's folder path (zone and name) with all its layers;</description></item>
/// <item><description>the map's dialogue asset, the shared one and the ETC one are injected (the headless game has no
/// asset manager, so no Yarn node would play otherwise);</description></item>
/// <item><description>flags set, then the hero's <c>Pos*</c> and <c>Tile*</c> set together before the first frame;</description></item>
/// <item><description>a trace collector on every instruction, for the end signal, the last pc of every program and
/// the skipped or exceeded lists;</description></item>
/// <item><description>a frame loop with a numbered budget: past it the arc fails, naming for every program that ran the last
/// (slot, pc, opcode) executed - a stuck arc never hangs the run.</description></item>
/// </list>
/// A missing export FAILS the test naming it, never skips it. The session singletons are reset at the start and at
/// the end, as the E16.e montage does; a class using this support carries
/// <c>[Collection(AlundraMusicPlayerSingletonCollection.Name)]</c>.
/// </summary>
internal sealed class ArcRun : IDisposable
{
    private readonly List<ArcInstruction> _trace = new();
    private readonly Dictionary<(int Slot, int Pc, int Opcode), int> _firstIndex = new();
    private readonly Dictionary<(int Slot, int ProgramStart), ArcInstruction> _lastByProgram = new();
    private readonly string _previousProjectPath;
    private uint _hold;
    private uint _held;
    private uint _heldPreviousFrame;
    private readonly World? _realWorld;

    public ArcSpec Spec { get; }

    public AlundraWorldProxy Proxy { get; }

    public AlundraEntityScriptProxy Hero { get; }

    /// <summary>The entity that carries <see cref="Hero"/>.</summary>
    public Entity HeroEntity { get; }

    /// <summary>The entity that carries <paramref name="proxy"/> (its protected <c>Owner</c>), whether bare or a prefab.</summary>
    public static Entity? EntityOf(AlundraEntityScriptProxy proxy) =>
        (Entity?)typeof(CasaEngine.Framework.Scripting.GameplayProxy)
            .GetProperty("Owner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(proxy);

    public AlundraEventProgramRunner Runner { get; }

    /// <summary>Frames run so far.</summary>
    public int Frame { get; private set; }

    public IReadOnlyList<ArcInstruction> Trace => _trace;

    /// <summary>Called right after every instruction is recorded (so its effects are already applied): the seam
    /// to sample positions at an exact pc.</summary>
    public Action<ArcInstruction>? OnInstruction { get; set; }

    /// <summary>E19.d: called at the end of every frame (<see cref="Frame"/> already incremented), the seam of the per-frame
    /// invariants (a position that must not change between two pcs).</summary>
    public Action? OnFrame { get; set; }

    public static AlundraGameState State => AlundraGameState.Instance;

    public ArcRun(ArcSpec spec)
    {
        Spec = spec;

        // Saved BEFORE anything that can fail: a failing constructor never reaches Dispose by itself (the catch
        // below calls it), and Dispose restores this value - saved after FindProjectRoot, it would put a null back.
        _previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            if (spec.Prefabs && !spec.RealController)
            {
                throw new ArgumentException($"arc {spec.Name}: Prefabs requires RealController (the prefabs' controllers need the world's own Update).");
            }

            ResetAll();
            if (spec.Prefabs)
            {
                Log = LogCapture.Install();
            }

            AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

            var root = FindProjectRoot();
            EngineEnvironment.ProjectPath = root; // the sprite record catalog is resolved when the proxy is built.

            foreach (var flag in spec.Flags)
            {
                State.AddFlag((uint)flag, 1u << (flag & 0x1f));
            }

            var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
            var world = BuildWorld(root, spec, uiView);
            Entity heroEntity;
            if (spec.Prefabs)
            {
                ArcPrefabAssets.Install(world, root);
            }

            if (spec.RealController)
            {
                // The game's execution policy (without it World.Update does not run the entities' scripts) and a
                // physics world (the controller needs one), both before InitializeWithWorld.
                world.Game!.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
                HeroWorldFixture.SetProperty(world, nameof(World.PhysicsWorld), new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy()));
                // E19.d (TH1): in Prefabs mode the hero is the export's prefab, spawned by the production call, with its animated
                // sprite on the logical clock; otherwise a box and a controller built by hand.
                heroEntity = spec.Prefabs
                    ? AddPrefabHeroPawn(world, root)
                    : AddControllerHeroPawn(world, HeroWorldFixture.LoadHeroControllerSettings(root));
                _realWorld = world;
            }
            else
            {
                heroEntity = AddHeroPawn(world);
            }

            var controller = world.PlayerControllers.OfType<AlundraPlayerController>().Single();
            controller.PadStateProviderForTests = () => new AlundraPadState
            {
                ButtonsHold = _hold | _held,
                ButtonsJustPressed = _hold | (_held & ~_heldPreviousFrame),
            };

            if (spec.Arrival is { } arrival)
            {
                // E19.d2a S3: production integrates the pawn (World.InternalAddEntities, which sets Entity.World) in
                // World.InitializePlayerControllers, BEFORE the world's gameplay proxy runs InitializeWithWorld and
                // AdoptPlayerPawn - whose ClampToGround reads Owner.World.CollisionField. The montage left the pawn queued, so
                // the adoption saw no world and raised nothing; an arrival is only measurable with the pawn integrated first.
                typeof(World).GetMethod("InternalAddEntities", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, null);

                Assert.True(BackdropLoader.TryParseMapIndex(spec.WorldName, out var arrivalMap), $"arc {spec.Name}: no map id in '{spec.WorldName}'");
                AlundraWarpDirector.Instance.SetPendingArrivalForTests(
                    (uint)arrivalMap, arrival.PosX, arrival.PosY, arrival.PosZ, arrival.AnimationId, arrival.DirectionId);
            }

            Proxy = new AlundraWorldProxy();
            Proxy.InitializeWithWorld(world);
            typeof(AlundraBackdropStage).GetField("_clearColorApplied", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Proxy._backdropStage, true); // the headless montage has no view for the clear colour.

            Runner = Assert.IsType<AlundraEventProgramRunner>(Proxy.EventProgramRunner);
            Runner.TraceSink = Record;

            // The headless game has no asset manager: the map's dialogues, the shared ones and the ETC texts come from
            // the export on disk.
            var mapFolder = Path.Combine(root, "Maps", spec.Zone, spec.WorldName);
            Runner.MapDialogueAsset = DialogueTestAssets.LoadFromDisk(Path.Combine(mapFolder, "dialogues", spec.WorldName + ".dialogue"));
            Runner.SharedDialogueAsset = DialogueTestAssets.LoadFromDisk(Path.Combine(root, "Dialogues", "Shared.dialogue"));
            AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.LoadFromDisk(Path.Combine(root, "Dialogues", "Etc.dialogue")));
            Assert.True(AlundraDialogueDirector.Instance.HasPresenter);

            HeroEntity = heroEntity;
            Hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);

            // The hero's sprite header (flags, programs, body box): AdoptPlayerPawn resolves it through the asset
            // catalog, empty in the test process - the same block, from the export's own catalog file.
            Assert.True(Proxy.SpriteRecordCatalog.TryGet(HeroPrefabId(root), out var heroHeader));
            AlundraWorldProxy.ApplyHeroSpriteHeader(Hero, heroHeader);
            if (spec.RealController)
            {
                // In this process AdoptPlayerPawn derived the walkability mask BEFORE the header (empty catalog):
                // the controller is re-synced from the flags now. ClassB gives 0x41.
                Hero.ResyncControllerFromFlags();
                Assert.Equal(0x41u, Hero.Controller!.Settings.WalkabilityMask);
            }

            // The map-entry animation (0x36) ends through the animation chain (anim 54 -> 0), which the headless
            // montage never plays: the hero is put at its end, Idle.
            Assert.Equal(AlundraGameState.ResetAnimationId, Hero.TargetAnimationId);
            Hero.TargetAnimationId = 0;

            // Position and tile together, before the first frame: AdoptPlayerPawn puts the hero on the New Game tile
            // and computes its Tile* once, and the zone tests (0x3B) read TileX/Y/Z.
            // E19.d2a S3 (U3): an arc with an arrival writes neither: AdoptPlayerPawn placed the hero from the arrival record.
            if (spec.Arrival is null)
            {
                Hero.PosX = (spec.HeroTileX * 24 + 12) << 16;
                Hero.PosY = (spec.HeroTileY * 16 + 8) << 16;
                Hero.PosZ = spec.HeroTileZ << 20;
                Hero.TileX = spec.HeroTileX;
                Hero.TileY = spec.HeroTileY;
                Hero.TileZ = Hero.PosZ >> 20;
                if (spec.RealController)
                {
                    Hero.PushLogicalPositionToRoot(); // the controller moves the root: it starts where the hero was placed.
                }
            }
        }
        catch
        {
            // Dispose is never called on a constructor that throws: the global state is restored here, or a failing
            // arc would dirty the tests that follow.
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// E19.d2b B6 (T-REG-0): the arcs whose pins were measured before entities blocked movement. Their positions, frames and flags have no reason to move
    /// under the entity contacts, and the proof is that no entity shortened or cancelled a single controller step of theirs: <see cref="TotalEntityBlockCount"/>
    /// is 0 when they end. Checked when the arc is disposed (the end of its <c>using</c>), after the global state is restored.
    /// </summary>
    private static readonly HashSet<string> ArcsWithoutEntityContact = new() { "A1c", "A3", "A4p", "A5", "A5r", "A6", "A7", "A8", "A9", "A10", "A10J", "A20" };

    /// <summary>The controller steps that an entity shortened or cancelled, summed over every entity of the world (the hero's included).</summary>
    public int TotalEntityBlockCount => Proxy == null ? 0 : Entities.Concat(new[] { Hero }).Distinct().Sum(e => e.EntityBlockCount);

    public void Dispose()
    {
        var blocked = Frame > 0 && ArcsWithoutEntityContact.Contains(Spec.Name) ? TotalEntityBlockCount : 0;
        Log?.Dispose();
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        ResetAll();
        if (blocked != 0)
        {
            throw new XunitException($"arc {Spec.Name}: {blocked} controller step(s) were shortened or cancelled by an entity (T-REG-0 expects none: its pins have no reason to move)");
        }
    }

    /// <summary>The log lines of the run, in <see cref="ArcSpec.Prefabs"/> mode only (null otherwise).</summary>
    public LogCapture? Log { get; private set; }

    /// <summary>The world of the arc in <see cref="ArcSpec.RealController"/> mode (null in bare mode).</summary>
    public World? RealWorld => _realWorld;

    private static void ResetAll()
    {
        // E19.d (TH2): the shared random stream of the arcs starts from its seed (in game its position depends on the draws made before).
        AlundraRandom.Reset();
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
    }

    public IReadOnlyList<AlundraEntityScriptProxy> Entities => ((IEntityWorldContext)Proxy).SpawnedEntities;

    /// <summary>The entity of an "Entities" record of the map (its <c>Index</c>), or null when it never spawned.</summary>
    public AlundraEntityScriptProxy? EntityByRecord(int record) => Entities.FirstOrDefault(e => e.EntityRefId == record);

    /// <summary>The index in the trace of the first execution of (slot, pc, opcode), or -1.</summary>
    public int FirstIndexOf(int slot, int pc, int opcode) => _firstIndex.TryGetValue((slot, pc, opcode), out var index) ? index : -1;

    public bool Has(int slot, int pc, int opcode) => FirstIndexOf(slot, pc, opcode) >= 0;

    /// <summary>Whether (slot, pc, opcode) ran after the trace index <paramref name="afterIndex"/>.</summary>
    public bool HasAfter(int afterIndex, int slot, int pc, int opcode)
    {
        for (var i = afterIndex + 1; i < _trace.Count; i++)
        {
            var t = _trace[i];
            if (t.Slot == slot && t.Pc == pc && t.Opcode == opcode)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every instruction the interpreter skipped by its size, terminated for want of a size, or cut off by
    /// the loop guard.</summary>
    public IEnumerable<ArcInstruction> SkippedOrExceeded => _trace.Where(t =>
        t.Kind is EventTraceKind.UnknownSkipped or EventTraceKind.UnknownNoSizeTerminated or EventTraceKind.LoopBudgetExceeded);

    private void Record(EventTraceRecord record)
    {
        var instruction = new ArcInstruction(
            Frame, record.ProgramSlot, record.State.Parameters[0], record.CodeIndex, record.Opcode, record.Kind);
        _firstIndex.TryAdd((instruction.Slot, instruction.Pc, instruction.Opcode), _trace.Count);
        _trace.Add(instruction);
        _lastByProgram[(instruction.Slot, instruction.ProgramStart)] = instruction;
        OnInstruction?.Invoke(instruction);
    }

    /// <summary>One frame the engine's way: every spawned entity's own <c>Update</c> - the hero's included, whose
    /// controller samples the pad - then the world's. In <see cref="ArcSpec.RealController"/> mode the world's own
    /// <c>Update</c> runs instead of the entity loop.</summary>
    public void OneFrame()
    {
        if (_realWorld is not null)
        {
            // The world already updates every entity added with AddEntity, the spawned ones included: the loop below
            // would update them twice.
            _realWorld.Update(0.02f);
        }
        else
        {
            foreach (var entity in Entities.ToList())
            {
                entity.Update(0.02f);
            }
        }

        Proxy.Update(0.02f);
        Frame++;
        _heldPreviousFrame = _held;
        OnFrame?.Invoke();
    }

    /// <summary>One frame with a pad button held (and just pressed) during it.</summary>
    public void Press(uint button)
    {
        _hold = button;
        OneFrame();
        _hold = 0;
    }

    /// <summary>E19.d: holds <paramref name="directions"/> (pad bits) on every following frame until <see cref="ReleaseDirections"/>;
    /// only the first frame reports them as just pressed. <see cref="Press"/> keeps its meaning (one frame, pressed and held).</summary>
    public void HoldDirections(uint directions) => _held = directions;

    /// <summary>E19.d: lets go of the directions held by <see cref="HoldDirections"/>.</summary>
    public void ReleaseDirections() => _held = 0;

    /// <summary>E19.d: places the hero at a pixel position of the map during an arc: <c>Pos*</c> and <c>Tile*</c> together
    /// (<c>TileZ = PosZ &gt;&gt; 20</c>), then the root, like the placement of the constructor.</summary>
    public void PlaceHero(int pixelX, int pixelY, int pixelZ)
    {
        Hero.PosX = pixelX << 16;
        Hero.PosY = pixelY << 16;
        Hero.PosZ = pixelZ << 16;
        Hero.TileX = pixelX / 24;
        Hero.TileY = pixelY / 16;
        Hero.TileZ = Hero.PosZ >> 20;
        Hero.PushLogicalPositionToRoot();
    }

    /// <summary>Frames until <paramref name="done"/> holds, or fails at the arc's frame limit naming the last
    /// instruction of every program that ran and the signal that never came.</summary>
    public void RunUntil(Func<bool> done, string signal)
    {
        while (!done())
        {
            if (Frame >= Spec.FrameLimit)
            {
                throw new XunitException(StuckMessage(signal));
            }

            OneFrame();
        }
    }

    /// <summary>The text of a stuck arc: for every program that ran, the last (slot, pc, opcode) executed.</summary>
    public string StuckMessage(string signal)
    {
        var programs = _lastByProgram
            .OrderBy(p => p.Value.Frame)
            .Select(p => $"slot {p.Key.Slot} program @{p.Key.ProgramStart}: last 0x{p.Value.Opcode:X2} @{p.Value.Pc} ({p.Value.Kind}, frame {p.Value.Frame})");
        var skipped = SkippedOrExceeded.Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})").Distinct().Take(20);
        return $"arc {Spec.Name} did not reach its end signal ({signal}) within {Spec.FrameLimit} frames. "
            + $"Last instruction of each program: [{string.Join("; ", programs)}]. "
            + $"Skipped or exceeded: [{string.Join("; ", skipped)}].";
    }

    /// <summary>Presses Square, the dialogue button, while a dialogue is open, until it closes, like the player.
    /// Fails at the arc's frame limit.</summary>
    public void CloseDialogueWithTheButton(string signal)
    {
        RunUntil(() =>
        {
            if (!AlundraDialogueDirector.Instance.IsOpen)
            {
                return true;
            }

            Press(AlundraPadState.Square);
            return !AlundraDialogueDirector.Instance.IsOpen;
        }, signal);
    }

    /// <summary>E19.d: frames until <paramref name="done"/> holds, pressing Square on EVERY frame that starts with a dialogue open
    /// (from the frame that follows the opening to the one that closes it: as many presses as pages), like a player who never lets
    /// go of the button. <see cref="CloseDialogueWithTheButton"/> alternates a press and a frame without the button and stays as it
    /// is for the arcs that pinned it. Fails at the arc's frame limit.</summary>
    public void RunUntilPressingTheButtonOnEveryDialogueFrame(Func<bool> done, string signal)
    {
        while (!done())
        {
            if (Frame >= Spec.FrameLimit)
            {
                throw new XunitException(StuckMessage(signal));
            }

            if (AlundraDialogueDirector.Instance.IsOpen)
            {
                Press(AlundraPadState.Square);
            }
            else
            {
                OneFrame();
            }
        }
    }

    /// <summary>The hero pawn of <see cref="ArcSpec.RealController"/> mode: the box of the export's hero (21x15x32, local
    /// (0.5, 0.5, 16)) and a <see cref="CharacterControllerComponent"/> with the export's settings, added to the world
    /// (which registers it with the motion system), then possessed by a real player controller.</summary>
    private static Entity AddControllerHeroPawn(World world, CharacterControllerSettings settings)
    {
        var root = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Microsoft.Xna.Framework.Vector3(21f, 15f, 32f) },
            LocalPosition = new Microsoft.Xna.Framework.Vector3(0.5f, 0.5f, 16f),
            LocalRotation = Microsoft.Xna.Framework.Quaternion.Identity,
        });
        root.AddChildComponent(collisionComponent);

        var entity = new Entity
        {
            Name = "AlundraHeroTestPawn",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.AddComponent(new CharacterControllerComponent { Settings = settings });
        entity.Initialize();
        world.AddEntity(entity);

        PossessWithAPlayerController(world, entity);
        return entity;
    }

    /// <summary>E19.d (TH1): the hero of the <see cref="ArcSpec.Prefabs"/> mode - the export's prefab <c>Alundra.entity</c>, created by the call
    /// production makes for the default pawn (<c>World.SpawnEntity</c>, <c>World.InitializePlayerControllers</c>), initialised (its script
    /// proxy exists, as <c>AdoptPlayerPawn</c> needs) and possessed by a real player controller.</summary>
    private static Entity AddPrefabHeroPawn(World world, string root)
    {
        var pawn = world.SpawnEntity<Entity>(HeroPrefabId(root));
        pawn.Initialize();
        PossessWithAPlayerController(world, pawn);
        return pawn;
    }

    private static void PossessWithAPlayerController(World world, Entity pawn)
    {
        var controller = new AlundraPlayerController();
        controller.Possess(pawn);
        var field = typeof(World).GetField("_playerControllers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((List<PlayerController>)field.GetValue(world)!).Add(controller);
    }

    private static Guid HeroPrefabId(string root)
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(root, "AssetInfos.json")));
        var entry = ((JArray)assetInfos["asset_infos"]!).Single(e => (string?)e["name"] == "Alundra" && (string?)e["asset_type"] == "entity");
        return Guid.Parse((string)entry["id"]!);
    }

    /// <summary>The headless real-map montage of <see cref="AlundraSaveBookEndToEndTests"/>, generalised to a map's folder
    /// path: all the layers of its tile map, a game whose active render view carries <paramref name="uiView"/>.</summary>
    private static World BuildWorld(string root, ArcSpec spec, AlundraSaveBookTests.RecordingUIViewRuntime uiView)
    {
        var tileMapPath = Path.Combine(root, "Maps", spec.Zone, spec.WorldName, "tilemap", spec.WorldName + ".tileMap");
        Assert.True(File.Exists(tileMapPath), $"the real export of map '{spec.WorldName}' is missing: '{tileMapPath}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        var tileSetData = AlundraWorldProxyGlobalFreezeTests.LoadRealVisualTileSet(root, tileMapData);

        var world = SaveScreenTestWorlds.WorldWithUIView(spec.WorldName, uiView);

        var tileMapEntity = new Entity { Name = "tileMap" };
        AlundraWorldProxyGlobalFreezeTests.SetProperty(tileMapEntity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;
        component.TileMapData = tileMapData;
        component.TileSetData = tileSetData;

        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var runtimeLayers = AlundraWorldProxyGlobalFreezeTests.GetLayers(component);
        for (var layerIndex = 0; layerIndex < tileMapData.Layers.Count; layerIndex++)
        {
            var layer = new TileMapLayer(tileMapData.Layers[layerIndex]);
            var cellCount = tileMapData.MapSize.Width * tileMapData.MapSize.Height;
            for (var i = 0; i < cellCount; i++)
            {
                layer.Tiles.Add(new AlundraWorldProxyGlobalFreezeTests.StubTile());
                layer.CollisionObjects.Add(null);
            }

            runtimeLayers.Add(layer);
            AlundraWorldProxyGlobalFreezeTests.InvokeBuildChunks(component, layer, layerIndex);
        }

        world.Entities.Add(tileMapEntity);
        return world;
    }
}

/// <summary>
/// E19.c1 T1 (D-E19-14): the asset manager of the <see cref="ArcSpec.Prefabs"/> mode. The headless game is an
/// uninitialised <c>CasaEngineGame</c>, whose <c>AssetContentManager</c> initializer never ran: the production prefab
/// loader throws and every NPC falls back to a bare entity. A manager built here and set by reflection, as the montage
/// already sets the <c>GameManager</c>, gives the production spawn path its real prefab.
/// <list type="bullet">
/// <item><description>Loaders: <c>Entity</c>, <c>Animation2dData</c> and <c>UIScreenAsset</c> only (the HUD wiring of a
/// non-null manager builds a screen that acquires its asset). NEVER <c>SpriteData</c>, <c>Texture</c> or
/// <c>TileSetData</c>: the first two need a graphics device and a failure past them drops the entity, the third would
/// build a navigation grid (no detour: the arcs' walks meet no wall).</description></item>
/// <item><description>The resolver reads the export's <c>AssetInfos.json</c> and answers <c>null</c> for an unknown id
/// (a throwing resolver makes <c>World.InternalAddEntities</c> drop the entity without a trace).</description></item>
/// </list>
/// </summary>
internal static class ArcPrefabAssets
{
    public static AssetContentManager Install(World world, string projectRoot)
    {
        var infos = new Dictionary<Guid, AssetInfo>();
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, "AssetInfos.json")));
        foreach (var node in (JArray)assetInfos["asset_infos"]!)
        {
            var info = new AssetInfo();
            info.Load((JObject)node);
            infos[info.Id] = info;
        }

        var context = new EngineRuntimeContext(new ProjectSettings(), projectRoot, id => infos.TryGetValue(id, out var info) ? info : null!);
        var manager = new AssetContentManager { RuntimeContext = context };
        manager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        manager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        manager.RegisterAssetLoader(typeof(UIScreenAsset), new AssetLoader<UIScreenAsset>());

        typeof(CasaEngineGame)
            .GetField("<AssetContentManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Game, manager);
        return manager;
    }
}
