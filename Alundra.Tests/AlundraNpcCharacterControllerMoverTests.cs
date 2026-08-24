#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E4.b "DLL - les PNJ bougent sur le mover" acceptance (docs/plan-e4-deplacement-scripte.md): a
/// hand-built NPC pawn - root <c>TransformComponent</c> -&gt; <c>CollisionComponent</c> Box 18x12x32
/// local_position (0,0,16), sibling <c>CharacterControllerComponent</c> whose settings are loaded from the
/// real converter export's own <c>Marin-passager-mouette-146.entity</c> "settings" node (Radius 6, Height
/// 32 - the real bank-146 "sailor/passenger/seagull" prefab, one of the intro's own scripted walkers,
/// map 389 entity records 6-14/16 per <c>data-extracted/data/map_389.json</c>'s own <c>SpriteTableIndex</c>)
/// - under a real headless <see cref="World"/> (TopDownElevation policy) with the REAL map 389
/// <see cref="AlundraCellsCollisionField"/> installed as <c>World.CollisionField</c>. Same fixture pattern
/// as <see cref="AlundraCharacterControllerAdoptionTests"/> (the hero's own E3.d pattern this file
/// mirrors, per docs/plan-e4-deplacement-scripte.md E4.b's own acceptance note), IsPlayer=false so the
/// scripted mover (<see cref="AlundraScriptedMotion.TickScriptedNpc"/>, wired from
/// <see cref="AlundraEntityScriptProxy.Update"/>'s own <c>!IsPlayer</c> branch) drives it instead of
/// <see cref="AlundraPlayerManager"/>. Self-skips every [Fact] when <c>alundra-project/</c> is not present
/// in this checkout (same convention as <see cref="AlundraCharacterControllerAdoptionTests"/>).
/// </summary>
public class AlundraNpcCharacterControllerMoverTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private const string Bank146EntityRelativePath = "Entities/Marin-passager-mouette-146/Marin-passager-mouette-146.entity";

    // -----------------------------------------------------------------------------------------
    // Fixture plumbing - same shape as AlundraCharacterControllerAdoptionTests's own private helpers.
    // -----------------------------------------------------------------------------------------

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

    private static TileMapData? LoadMap389TileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        if (!File.Exists(tileMapPath))
        {
            return null;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    /// <summary>
    /// E4.d: resolves map 389's own real tilesets (its native map tileset PLUS E4.a's shared
    /// "Navigation" tileset, both listed in <c>tile_set_asset_ids</c>) straight off disk via
    /// <c>AssetInfos.json</c>'s id -&gt; file_name index - the same "real export, no live
    /// AssetContentManager" constraint <see cref="LoadMap389TileMapData"/> itself works under (no
    /// GraphicsDevice/content pipeline in this headless test process). Feeds
    /// <see cref="NavigationGrid2D.TryCreateFromTileMap"/> in EXACTLY the order
    /// <c>AlundraWorldProxy.TryBuildNavigationGrid</c> resolves them at runtime (position-indexed by
    /// <see cref="TileMapData.TileSetDataAssetIds"/>).
    /// </summary>
    private static List<TileSetData> LoadMap389TileSets(string projectRoot, TileMapData tileMapData)
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, "AssetInfos.json")));
        var pathById = new Dictionary<Guid, string>();
        foreach (var entry in (JArray)assetInfos["asset_infos"]!)
        {
            if (Guid.TryParse((string?)entry["id"], out var id) && (string?)entry["file_name"] is { } fileName)
            {
                pathById[id] = fileName;
            }
        }

        var tileSets = new List<TileSetData>();
        foreach (var assetId in tileMapData.TileSetDataAssetIds)
        {
            Assert.True(pathById.TryGetValue(assetId, out var relativePath), $"AssetInfos.json missing tileset {assetId}.");
            var fullPath = Path.Combine(projectRoot, relativePath!.Replace('\\', Path.DirectorySeparatorChar));
            var tileSetData = new TileSetData();
            tileSetData.Load(JObject.Parse(File.ReadAllText(fullPath)));
            tileSets.Add(tileSetData);
        }

        return tileSets;
    }

    private static AlundraCellsCollisionField? LoadMap389Field(string projectRoot)
    {
        var tileMapData = LoadMap389TileMapData(projectRoot);
        if (tileMapData == null)
        {
            return null;
        }

        var created = AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "map 389 AlundraCells custom property should parse and match MapSize.");
        return field;
    }

    /// <summary>
    /// Reads the "settings" node of the REAL bank-146 sailor prefab's own <see cref="CharacterControllerComponent"/>
    /// (E4.a, <c>SpriteWriter.WriteEntityPrefab</c>) - same raw-JSON pattern as
    /// <see cref="AlundraCharacterControllerAdoptionTests.LoadHeroControllerSettings"/>, a different entity
    /// file. Confirmed on the real export: Radius 6, Height 32, SkinWidth 0.5, StepHeight 3,
    /// GroundSnapDistance 4, Gravity/MaxFallSpeed/WalkabilityMask all 0 (E4.a leaves them 0 - overridden at
    /// spawn by <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>, exactly like the hero's own
    /// <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> override, E4.b's own "Spawn" item).
    /// </summary>
    private static CharacterControllerSettings? LoadBank146ControllerSettings(string projectRoot)
    {
        var entityPath = Path.Combine(projectRoot, Bank146EntityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(entityPath))
        {
            return null;
        }

        var document = JObject.Parse(File.ReadAllText(entityPath));
        var componentsNode = (JArray?)document["components"];
        JObject? controllerNode = null;
        if (componentsNode != null)
        {
            foreach (var node in componentsNode)
            {
                if ((string?)node["type"] == nameof(CharacterControllerComponent))
                {
                    controllerNode = (JObject)node;
                    break;
                }
            }
        }

        Assert.NotNull(controllerNode);
        var settingsNode = (JObject?)controllerNode!["settings"];
        Assert.NotNull(settingsNode);

        var settings = new CharacterControllerSettings();
        settings.Load(settingsNode!);
        return settings;
    }

    /// <summary>Same headless montage as <see cref="AlundraCharacterControllerAdoptionTests.BuildWorld"/>.</summary>
    private static World BuildWorld(AlundraCellsCollisionField field)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManagerField = typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy()));
        world.CollisionField = field;

        return world;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>Hand-built NPC pawn - box 18x12x32 local_position (0,0,16), the real bank-146 prefab's own
    /// fixture (see this class' own doc). <see cref="AlundraEntityScriptProxy.IsPlayer"/> false, so
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s scripted-NPC-mover branch drives it. Added to
    /// <paramref name="world"/> (queued - the caller's first <see cref="World.Update"/> registers it with
    /// CharacterMotionSystem before this same frame's <c>GameplayProxy.Update</c> ever runs, same ordering
    /// note as <see cref="AlundraCharacterControllerAdoptionTests.BuildHeroPawn"/>).</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildNpcPawn(
        World world, CharacterControllerSettings settings, Vector3 startPosition, IAlundraScriptHost scriptHost)
    {
        var root = new TransformComponent();
        root.LocalTransform.Position = startPosition;

        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collisionComponent);

        var entity = new Entity
        {
            Name = "NpcTestPawn",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };

        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        controllerComponent.SetControlMode(CharacterControlMode.Script);
        entity.AddComponent(controllerComponent);

        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
        Assert.NotNull(proxy.Controller);
        proxy.IsPlayer = false;
        proxy.ScriptHost = scriptHost;
        proxy.Status = EntityStatus.Normal; // pick phase would otherwise run the (unwired) Load program.
        proxy.PosX = (int)Math.Round((double)startPosition.X * 65536.0);
        proxy.PosY = (int)Math.Round((double)startPosition.Y * 65536.0);
        proxy.PosZ = (int)Math.Round((double)startPosition.Z * 65536.0);
        // Spawn-time bit-complement (EntityManager.cs:85-88, ApplySpawnInitialization) - guarantees the
        // very first SyncAnimation call notices a "change" even when TargetAnimationId is later left at 0.
        proxy.CurrentAnimationId = ~0u;

        world.AddEntity(entity);
        return (entity, proxy);
    }

    /// <summary>No-op <see cref="IEventProgramRunner"/> - PickEventTrigger/RunPickedEvent still run every
    /// frame (IsPlayer is false), but with EventTrigger always ProgramUnknown (no ProgramIndexes wired on
    /// this bare test pawn) RunPickedEvent is itself a no-op, so this runner is never actually
    /// invoked.</summary>
    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class FakeScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity => null;
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;

        /// <summary>E4.f (verifier A5 tests): mutable so a test can seed the collidables list a scripted
        /// mover's own <c>EvaluateEntitySupport</c> call reads every frame (see
        /// <see cref="IAlundraScriptHost.Collidables"/>) - empty by default, same as before this list
        /// existed.</summary>
        public List<AlundraEntityScriptProxy> Collidables { get; } = new();

        IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        // Bug fix (AlundraLogicClock's own class doc): this file drives world.Update(1/50) in a loop, one
        // call per test-frame - a single-frame auto-closed clock reproduces the pre-fix "always exactly 1
        // tick per world.Update call" behaviour these tests already assume, since every call passes the
        // SAME dt as the clock's own fixed-tick constant. Same pattern as every other FakeScriptHost in
        // this test project.
        private readonly AlundraLogicClock _logicClock = new();
        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            return ticks;
        }
    }

    /// <summary>E4.d: same shape as <see cref="FakeScriptHost"/>, but wraps a REAL
    /// <see cref="AlundraEventProgramRunner"/> instead of the no-op one - so a hand-built Tick program
    /// (0x1E/0x1F) actually dispatches through <see cref="AlundraEntityScriptProxy.Update"/>'s own
    /// pick/run pass every <c>World.Update</c>.</summary>
    private sealed class RealRunnerScriptHost : IAlundraScriptHost
    {
        public RealRunnerScriptHost(IEventProgramRunner runner) => Runner = runner;
        public IEventProgramRunner Runner { get; }
        public AlundraEntityScriptProxy? ActiveCollisionEntity => null;
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;

        /// <summary>See <see cref="FakeScriptHost.Collidables"/>'s own doc.</summary>
        public List<AlundraEntityScriptProxy> Collidables { get; } = new();

        IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        // Bug fix (AlundraLogicClock's own class doc): see FakeScriptHost's own doc on this same member,
        // above - identical rationale.
        private readonly AlundraLogicClock _logicClock = new();
        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            return ticks;
        }
    }

    /// <summary>E4.d: minimal <see cref="IEntityWorldContext"/> a hand-built <see cref="AlundraEventProgramRunner"/>
    /// needs so its 0x1E/0x1F walk-detour helpers can reach an injected <see cref="NavigationGrid2D"/> -
    /// map 389 itself has 0 blocked navigation cells (E4.a's own finding), so obstacle/detour tests need
    /// a synthetic one. Every other member is a trivial no-op - none of this class' own tests exercise the
    /// entity-search opcodes.</summary>
    private sealed class FakeNavigationWorldContext : IEntityWorldContext
    {
        public NavigationGrid2D? NavigationGrid { get; set; }
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Wires <paramref name="proxy"/> so its own Tick slot (C) dispatches <paramref name="codes"/>
    /// (a raw opcode byte stream, no header) through a REAL <see cref="AlundraEventProgramRunner"/> every
    /// <c>World.Update</c> - <see cref="ScriptHelper.ProgramCTick"/>'s masked index 1 -&gt; offset 0 (index
    /// 0 of the table is left unused/zero, never referenced).</summary>
    private static AlundraEventProgramRunner WireRealTickProgram(
        AlundraEntityScriptProxy proxy, int[] codes, IEntityWorldContext? worldContext = null)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 389,
            EventCodesCTable = new[] { 0, 0 },
            Codes = codes,
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), worldContext);
        proxy.ScriptHost = new RealRunnerScriptHost(runner);
        proxy.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x81; // bit 0x80 set, masked index 1.
        return runner;
    }

    // -----------------------------------------------------------------------------------------
    // (1) Walk kinematics: real bank-146 anim 10 (Speed 128, Acceleration 6 - a genuine non-zero
    // acceleration, unlike anim 1's Speed 160/Acceleration 0) + cardinal direction 24 (0x18, the third
    // CardinalDirectionTable entry - AnimationTables.OffsetXList[24]=0x300/768, OffsetYList[24]=0, pure
    // +X). Transient ramp AND steady state, like the E2 scenario 208/1 (AlundraPlayerManagerTests).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void WalkKinematics_Bank146AnimTenRealSpeedAndAcceleration_TransientThenSteadyStateMatchHandComputedIntegration()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Flat row 57, columns 23+ (height 0, sol 80px) - same open stretch AlundraCharacterControllerAdoptionTests
        // own RootOwnership tests use for the hero.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 920f, 80f), new FakeScriptHost());

        // Real bank-146 AnimSets[10] (alundra-project/Data/sprite-records.json, this prefab's own guid):
        // Speed=128, Acceleration=6.
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [10] = new AnimSetEntry { Anim = 10, Speed = 128, Acceleration = 6 },
        };
        proxy.TargetAnimationId = 10;
        proxy.TargetDirection = 24; // AnimationTables.CardinalDirectionTable[3] = 0x18.

        const int frames = 70;

        // Hand-computed reference (bit-for-bit AlundraScriptedMotion.RunOneKinematicTick/IncrementForce,
        // PhysicsEngine.cs:1579-1598/1551-1576): World.Update call #1 has proxy.CurrentAnimationId still at
        // its spawn-time bit-complement (~0, guaranteed != 10) - AnimSetsByAnim[~0] misses, so tick 1's
        // Speed/TargetForce/ForceStep all resolve to 0 (zero contribution; SyncAnimation only sets
        // CurrentAnimationId=10 at the END of that same frame - E4.b's own documented one-frame latency,
        // see AlundraEntityScriptProxy.Update's own E4.b doc). From World.Update call #2 on,
        // CurrentAnimationId=10 (a real AnimSet hit): TargetForceX = OffsetXList[24]*128 = 0x300*128 =
        // 98304; ForceStepX = |98304-0| >> 6 = 1536 (recomputed exactly once, since Speed/TargetDirection/
        // Acceleration then stay unchanged every following tick); IncrementForce ramps ForceX by exactly
        // 1536 every tick until it reaches 98304 (after 64 ticks, i.e. World.Update call #65) and holds
        // there - transient (calls #2-#65) THEN steady state (calls #66-#70).
        var expectedForce = 0;
        var expectedPositionDeltaPixels = 0.0;
        for (var frame = 2; frame <= frames; frame++)
        {
            expectedForce = Math.Min(expectedForce + 1536, 98304);
            expectedPositionDeltaPixels += expectedForce / 65536.0;
        }

        Assert.Equal(98304, expectedForce); // sanity: steady state was actually reached within this run.

        for (var frame = 1; frame <= frames; frame++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(564.0 + expectedPositionDeltaPixels, entity.RootComponent!.Position.X, 2);
        Assert.Equal(920f, entity.RootComponent!.Position.Y); // pure +X direction - Y untouched.
    }

    // -----------------------------------------------------------------------------------------
    // (2) 0x1B real block-18 impulse (program 146, docs/intro-programs-389.txt offset 1620:
    // "0x1B Fly params=[0,255]") from standstill (KNOWN WATCH-POINT, E4.0 verifier P4: the engine's
    // step-support branch in UpdateGround runs before the upward-velocity gate and can swallow an
    // impulse applied the same tick as a horizontal step-up - isolating to a standstill impulse avoids it).
    // ForceZ = SignExtend16((255<<8)|0) * 0x10000 >> 8 = -256 * 0x10000 >> 8 = -65536 (16.16), i.e. a
    // constant -50 px/s (Controller.SetVerticalVelocity(ForceZ * 50f / 65536f)) - the real block-18 program
    // clears gravity (0x17) BEFORE this impulse, so this is a linear (non-accelerated) fall, not a
    // jump/liftoff: "IsOnGround 0 during flight, 1 after" per the plan's own acceptance wording, without a
    // liftoff/peak phase (documented deviation - real data has no positive 0x1B impulse anywhere in map
    // 389's own programs, see this class' own IsZForceApplied pre-read finding in the E4.b commit body).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void VerticalImpulse_RealBlockEighteen0x1BValue_FallsAtConstantVelocityAndLandsOnRealGround()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        // Cell (18,57): flat, sol 80px (same cell AlundraCharacterControllerAdoptionTests's own Fall test
        // uses for the hero). Spawn 50px above it, matching the real impulse's own fall distance/time.
        var world = BuildWorld(field);
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 130f), new FakeScriptHost());
        world.Update(1f / 50f); // register with CharacterMotionSystem before the scripted impulse.

        // 0x17 semantics (Flags & Gravity clear -> Settings.Gravity/MaxFallSpeed = 0, real block-18 program
        // order: 0x17 THEN 0x1B) - ApplyGravitySettingsToController with Flags carrying no Gravity bit.
        proxy.MapGravity = 1250f;
        proxy.MapMaxFallSpeed = 800f;
        proxy.ApplyGravitySettingsToController();
        Assert.Equal(0f, proxy.Controller!.Settings.Gravity);

        // 0x1B: real block-18 params [0,255] - SignExtend16((v2<<8)|v1) * 0x10000 >> 8.
        proxy.ForceZ = unchecked((short)(0 | (255 << 8))) * 0x10000 >> 8;
        Assert.Equal(-65536, proxy.ForceZ);
        proxy.Controller.SetVerticalVelocity(proxy.ForceZ * 50f / 65536f);
        Assert.Equal(-50f, proxy.Controller.Velocity.Z);

        Assert.Equal(0, proxy.IsOnGround);

        var landingFrame = -1;
        for (var frame = 1; frame <= 100 && landingFrame < 0; frame++)
        {
            world.Update(1f / 50f);
            if (proxy.IsOnGround != 0)
            {
                landingFrame = frame;
            }
        }

        // Constant -50 px/s over a 50px fall = exactly 1.0s = 50 ticks at the 50 Hz tick rate; GroundSnapDistance
        // (4px = 2 ticks at this speed) can land it up to 4 ticks early (measured: 46) - never late.
        Assert.InRange(landingFrame, 44, 50);
        Assert.Equal(80f, entity.RootComponent!.Position.Z);
        Assert.Equal(1, proxy.IsOnGround);
    }

    // -----------------------------------------------------------------------------------------
    // (3) Gravity toggle: 0x17 (Gravity cleared) keeps an airborne NPC at a constant altitude; 0x16
    // (Gravity set) makes it fall and land on the real field ground, fall speed clamped at MaxFallSpeed
    // (800 px/s on map 389).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GravityToggle_LowGravityHoldsAltitude_HighGravityFallsAndClampsAtMaxFallSpeed()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Cell (18,57): flat, sol 80px. Spawn well above it (400px) so the terminal-velocity clamp is
        // exercised for several ticks before landing.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 480f), new FakeScriptHost());
        world.Update(1f / 50f);

        // Bug fix (gull entity 6, map 389): the DLL now owns 100% of this NPC's vertical via
        // EvaluateEntitySupport's own per-tick decay (MapGravityRaw/MapZViscosityRaw - the map's RAW
        // Gravity/ZViscosity ints, "128"/"4096" in Ship Klark (beginning)-389.tileMap's own
        // CustomProperties, the same raw values the gull's own measured facts use), never the engine's
        // Settings.Gravity/MaxFallSpeed float pair - see ApplyGravitySettingsToController's own doc for why
        // those now stay pinned at 0 for every controller-driven NPC, at every Flags.Gravity state.
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;

        // 0x17 semantics: Flags has no Gravity bit -> EvaluateEntitySupport's own decay never runs, ForceZ
        // stays 0, SetVerticalVelocity(0) every tick -> altitude holds exactly.
        Assert.Equal(0u, proxy.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, proxy.Controller!.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.MaxFallSpeed);

        for (var frame = 0; frame < 25; frame++)
        {
            world.Update(1f / 50f);
            Assert.Equal(480f, entity.RootComponent!.Position.Z);
            Assert.Equal(0, proxy.IsOnGround);
        }

        // 0x16 semantics: Flags |= Gravity -> EvaluateEntitySupport's own per-tick decay now runs every
        // logic tick; the engine's own Settings.Gravity/MaxFallSpeed stay 0 regardless (they no longer
        // branch on this flag at all).
        proxy.Flags |= EntityFlags.Gravity;
        Assert.Equal(0f, proxy.Controller.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.MaxFallSpeed);

        var maxDownwardSpeed = 0f;
        for (var i = 0; i < 200 && entity.RootComponent!.Position.Z > 80f; i++)
        {
            world.Update(1f / 50f);
            maxDownwardSpeed = Math.Max(maxDownwardSpeed, -proxy.Controller.Velocity.Z);
        }

        Assert.Equal(80f, entity.RootComponent!.Position.Z);
        Assert.Equal(1, proxy.IsOnGround);
        // Terminal velocity = (MapZViscosityRaw << 8) / 65536 px/tick * 50 ticks/s = 4096*256/65536*50 =
        // 800 px/s - algebraically the same value E3.d's own MaxFallSpeed formula produces for map 389,
        // now reached by the DLL's own tick-quantized decay instead of the engine's continuous integrator.
        Assert.True(maxDownwardSpeed <= 800f, $"fall speed {maxDownwardSpeed} exceeded the terminal ZViscosity-derived clamp of 800.");
        Assert.True(maxDownwardSpeed > 700f, $"fall speed {maxDownwardSpeed} never got close to the 800 clamp over a 400px fall.");
    }

    // -----------------------------------------------------------------------------------------
    // (3b) Regression - sailor 12, map 389 intro: the original resets ForceZ to 0 on a TERRAIN landing
    // too, not only an entity-vs-entity one (PhysicsEngine.cs:123-135's own `if (platformEntity == null ||
    // ...)` branch runs the SAME reset for both). Before the fix, a controller-driven NPC resting on real
    // terrain keeps decaying ForceZ every logic tick past 0 down to the terminal
    // -(ZViscosity&lt;&lt;8) - never re-clamped, because EvaluateEntitySupport's own "found" branch (the
    // only site that reset ForceZ) never fires without an ENTITY support below.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TerrainLanding_RestingOnRealFlatGround_ForceZStaysZeroInsteadOfDecayingToTheTerminalVelocity()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Cell (18,57): flat, sol 80px - spawned exactly on the ground, no script impulse, so this test
        // isolates the resting-on-terrain decay alone (no walk, no entity support).
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 80f), new FakeScriptHost());
        world.Update(1f / 50f); // register with CharacterMotionSystem first (same ordering note as the tests above).

        proxy.Flags = EntityFlags.Gravity;
        // Real map 389 raw Gravity/ZViscosity (same values GravityToggle/GullHoverCycle use above).
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;
        proxy.ApplyGravitySettingsToController();

        for (var frame = 0; frame < 100; frame++)
        {
            world.Update(1f / 50f);

            // Bug (pre-fix): ForceZ decayed every tick regardless of terrain support and was never reset,
            // so by frame ~32 it had already reached the terminal -(4096&lt;&lt;8) = -1048576 and stayed
            // there; the resulting constant terminal velocity was re-applied to the controller every tick
            // (SetVerticalVelocity below), silently absorbed here by the engine's own ground-snap on flat
            // ground (this is exactly why the bug was invisible on flat ground - see the staircase test
            // below for where it stops being invisible).
            Assert.Equal(0, proxy.ForceZ);
            Assert.Equal(80f, entity.RootComponent!.Position.Z);
            Assert.Equal(1, proxy.IsOnGround);
        }

        Assert.Equal(0f, proxy.Controller!.Velocity.Z);
    }

    // -----------------------------------------------------------------------------------------
    // (3c) THE user-visible regression - sailor 12 stuck at the intro's own last staircase (map 389,
    // after commit 1d460f0): real stair cell (13,27), slope 5 height 10, the SAME fixture
    // AlundraCharacterControllerAdoptionTests.Stairs_SteppingDownTheSlope_FollowsEachStepInTurn uses for
    // the hero's own direct-Move stair-following test ((324,439,160) -&gt; (324,443,156) -&gt;
    // (324,447,152), walking +Y/south = descending). This NPC spawns at the BOTTOM of that same slope
    // ((324,447,152)) and walks NORTH (TargetDirection 16: OffsetXList[16]=0/OffsetYList[16]=-512, pure
    // -Y, the reverse of the hero's own descending direction) - i.e. it must CLIMB the same slope the
    // hero descends, and onto the real flat deck at the slope's own documented top-corner height (160,
    // per the hero test's own comment) - a scripted mover, not a direct Controller.Move, so this
    // exercises the SAME AlundraScriptedMotion.TickScriptedNpc + EvaluateEntitySupport path the real
    // sailor 12 walks through.
    //
    // Fidelity note (verified empirically, both with and without the fix, real map/settings): this
    // engine's own <see cref="CharacterControllerComponent.Update"/> already re-derives ground height
    // fully every rendered frame (<c>UpdateGround</c>) and its own <c>ApplyVerticalVelocity</c> zeroes
    // any downward request outright while grounded - a robustness net the original PSX engine's
    // equivalent code did not have. Because of that net, THIS test's own X/Y/Z trajectory is bit-for-bit
    // identical with and without the fix (confirmed by direct comparison) - the missing terrain reset
    // never manages to visibly displace this particular NPC on this particular slope. The bug is real
    // and worth fixing regardless (see EvaluateEntitySupport's own doc for the full citation/rationale,
    // and TerrainLanding_.../VerticalImpulse_... above for the state-level regression coverage that DOES
    // discriminate cleanly) - this test additionally proves the SAME missing reset on the EXACT real
    // stair cells the user hit: <see cref="AlundraEntityScriptProxy.ForceZ"/> must recover to 0 on every
    // grounded tick while climbing, not merely decay unboundedly underneath a trajectory this engine's
    // own snap happens to still render correctly.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Stairs_ScriptedWalkNorthFromTheBottomStep_ClimbsEachStepWithForceZPinnedAtZeroInsteadOfDecayingUnderneathIt()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Bottom of the real stair cell (13,27) - the hero's own final position in
        // AlundraCharacterControllerAdoptionTests.Stairs_SteppingDownTheSlope_FollowsEachStepInTurn.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(324f, 447f, 152f), new FakeScriptHost());
        world.Update(1f / 50f); // register with CharacterMotionSystem first.

        proxy.Flags = EntityFlags.Gravity;
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;
        proxy.ApplyGravitySettingsToController();

        // Real bank-146 AnimSets[1] (Speed 160, Acceleration 0 - same as RealWalk_0x1F.../Walk0x1E... above),
        // TargetDirection 16 (pure north, -Y) - climbing, the reverse of the hero's own descending test.
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1;
        proxy.TargetDirection = 16;

        // 1.25 px/tick north once CurrentAnimationId catches up (one-frame lag, same as every other
        // scripted-walk test in this class) - crossing the slope's own 8px span (447 -&gt; 439) takes ~7
        // ticks; 60 ticks gives ample margin to also clear the top corner onto the flat deck beyond it.
        var crossedBoundaryFrame = -1;
        var everSawNonZeroForceZWhileGrounded = false;
        for (var frame = 1; frame <= 60 && crossedBoundaryFrame < 0; frame++)
        {
            world.Update(1f / 50f);

            if (proxy.Controller!.IsGrounded && proxy.ForceZ != 0)
            {
                everSawNonZeroForceZWhileGrounded = true;
            }

            if (entity.RootComponent!.Position.Y <= 435f)
            {
                crossedBoundaryFrame = frame;
            }
        }

        // Real-world progress across the stair boundary (both before and after the fix, in this engine -
        // see this test's own "Fidelity note" above for why position alone does not discriminate here).
        Assert.True(crossedBoundaryFrame > 0,
            $"NPC never crossed the stair boundary (Y <= 435) within 60 ticks - stuck at Y={entity.RootComponent!.Position.Y}, Z={entity.RootComponent!.Position.Z}.");
        Assert.True(entity.RootComponent!.Position.Z >= 158f,
            $"expected the NPC to have climbed to (near) the top-corner height 160 by Y={entity.RootComponent!.Position.Y}; got Z={entity.RootComponent!.Position.Z}.");

        // THE fix, exercised on the exact real stair cells: while grounded and gravity-flagged, ForceZ
        // must never be left non-zero (pre-fix, it decays every tick and is only ever reset by the
        // entity-support "found" branch, which this scenario never reaches - no entity below, only real
        // terrain). This is the direct, state-level proof of the missing PhysicsEngine.cs:123-135
        // terrain reset on the EXACT scenario the user reported (sailor 12, map 389's last staircase).
        Assert.False(everSawNonZeroForceZWhileGrounded,
            "ForceZ was left non-zero on a grounded tick while climbing the real staircase - the terrain-landing reset (PhysicsEngine.cs:123-135's missing half) did not fire.");
    }

    // -----------------------------------------------------------------------------------------
    // (3d) The terrain-landing reset must not wipe a same-tick scripted upward (0x1B) impulse - the
    // original's own reset is reached only through ComputeZPosition's own `finalZVelocity &lt; 1` gate
    // (PhysicsEngine.cs:118), i.e. never while rising; here it is gated on `ForceZ &lt; 0` instead (see
    // EvaluateEntitySupport's own doc for why that is the narrower, still-safe equivalent).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void VerticalImpulse_UpwardZeroOneBWhileRestingOnRealGround_StillLiftsAndIsNotWipedByTheTerrainReset()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Cell (18,57): flat, sol 80px - same resting spot as TerrainLanding_... above.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 80f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Flags = EntityFlags.Gravity;
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;
        proxy.ApplyGravitySettingsToController();

        // Rest first, exactly like TerrainLanding_... above, so the terrain-landing reset is genuinely
        // engaged (ForceZ pinned at 0 by it every tick) before the impulse arrives - proving the reset does
        // not somehow suppress the FOLLOWING tick's own positive impulse either.
        for (var frame = 0; frame < 30; frame++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(0, proxy.ForceZ);
        var zBeforeImpulse = entity.RootComponent!.Position.Z;

        // Real block-18 0x1B params [128,3] (same raw impulse GullHoverCycle... above uses): raw=(3&lt;&lt;8)|128=896,
        // ForceZ=896*0x10000&gt;&gt;8=229376 (+3.5 px/tick) BEFORE this tick's own decay; net after the
        // -MapGravityRaw&lt;&lt;8=-32768 decay is +196608 (+3 px/tick) - still positive, so this tick's own
        // terrain-reset test (ForceZ &lt; 0) must NOT fire.
        var runner = new AlundraEventProgramRunner(
            new EventProgramDocument { MapIndex = 389, Codes = Array.Empty<int>() }, new AlundraGameState(), worldContext: null);
        runner.RunOneScriptCall(proxy, new EventProgramState { Codes = new byte[] { 0x1B, 128, 3, 0xFF } });

        world.Update(1f / 50f);

        Assert.True(entity.RootComponent!.Position.Z > zBeforeImpulse,
            $"expected the upward 0x1B impulse to lift the entity off {zBeforeImpulse}; got {entity.RootComponent!.Position.Z}.");
        Assert.True(proxy.Controller!.Velocity.Z > 0f,
            $"expected a positive vertical velocity from the upward impulse; got {proxy.Controller.Velocity.Z}.");
    }

    /// <summary>
    /// Bug fix (user-reported runtime timing bug, gull entity 6): a gull-6-shaped cycle - hover-climb,
    /// clear the Gravity bit mid-air via the REAL 0x63 opcode bridge (Tick program 134's own real
    /// operands, offset 900, [128,0,1]: clear mask (1&lt;&lt;8)|0 = 0x100 = <see cref="EntityFlags.Gravity"/>),
    /// zero the vertical impulse (0x1B [0,0]), hold for 60 real ticks, then a real 0x1B [127,255] scripted
    /// descent, then the REAL 0x62 opcode bridge restores gravity ([128,0,1]: set mask 0x100).
    /// Pre-fix (original 0x62/0x63 fix), <see cref="AlundraEventProgramRunner.RunOneScriptCall"/>'s own
    /// 0x63 handler mutated <see cref="AlundraEntityScriptProxy.Flags"/> but never told the entity's own
    /// <see cref="CharacterControllerComponent"/> - <c>Settings.Gravity</c> stayed at its spawn-time 1250
    /// and kept pulling the entity down every RENDERED frame of the "hover" via the engine's own continuous
    /// integrator, exactly the reported bug's own root cause (measured ~158 px/s vs the faithful 150 px/s
    /// - see <see cref="AlundraEntityScriptProxy.ApplyGravitySettingsToController"/>'s own doc). Current
    /// fix: <c>Settings.Gravity</c>/<c>MaxFallSpeed</c> now stay pinned at 0 for this entity at every
    /// Flags.Gravity state - the DLL's own <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>
    /// decays <see cref="AlundraEntityScriptProxy.ForceZ"/> exactly once per 50 Hz logic tick and drives the
    /// controller's vertical velocity from that decayed value directly, so Z genuinely holds during the
    /// hover (0x63) and genuinely resumes at the faithful decayed rate once gravity is restored (0x62) -
    /// both bridged the same way, via <see cref="AlundraEntityScriptProxy.ResyncControllerFromFlags"/>.
    /// </summary>
    [Fact]
    public void GullHoverCycle_Real0x63ClearsGravityMidAir_HoldsAltitudeThenScriptedDescentThenReal0x62Falls()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Same flat cell (18,57) the GravityToggle test above uses (sol 80px) - spawned well above it
        // (300px) so neither the climb nor the later accelerated fall clips the ground within this test's
        // own frame budget.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 300f), new FakeScriptHost());
        world.Update(1f / 50f); // register with CharacterMotionSystem first (same ordering note as GravityToggle above).

        proxy.Flags = EntityFlags.Gravity;
        // Real map 389 raw Gravity/ZViscosity (see GravityToggle test above's own doc) - the decay
        // EvaluateEntitySupport now runs every logic tick, replacing the engine's own continuous
        // Settings.Gravity/MaxFallSpeed (bug fix - see ApplyGravitySettingsToController's own doc).
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;
        proxy.ApplyGravitySettingsToController(); // spawn-time resync (ApplySpawnInitialization's own real call).

        var runner = new AlundraEventProgramRunner(
            new EventProgramDocument { MapIndex = 389, Codes = Array.Empty<int>() }, new AlundraGameState(), worldContext: null);
        void RunOpcode(params int[] codes)
            => runner.RunOneScriptCall(proxy, new EventProgramState { Codes = Array.ConvertAll(codes, c => (byte)c) });

        // Hover-climb: real 0x1B [128,3] (v1=128,v2=3 -> raw=(3<<8)|128=896, ForceZ=896*0x10000>>8=229376,
        // i.e. +3.5 px/tick/+175 px/s) reissued every tick for 10 ticks, exactly the real gull program's own
        // Wait(N)-gated 0x1B loop before the 0x63/apex. 0x1B's own handler sets ForceZ/velocity to the raw
        // impulse FIRST; EvaluateEntitySupport's own per-tick decay then runs LATER the same World.Update
        // (ForceZ -= MapGravityRaw&lt;&lt;8 = 32768, i.e. -0.5 px/tick), so the velocity actually applied
        // this tick is the DECAYED 196608 (3.0 px/tick, 150 px/s) - the measured facts' own faithful number
        // for the gull's real climb, not the raw 175 px/s impulse.
        for (var frame = 0; frame < 10; frame++)
        {
            RunOpcode(0x1B, 128, 3, 0xFF);
            world.Update(1f / 50f);
        }

        // Exact: this loop calls RunOpcode BEFORE World.Update, not through the real pick/run pipeline
        // (RunPickedEvent, reached from GameplayProxy.Update - see AlundraEntityScriptProxy.Update's own
        // E4.b doc) - so each iteration's controller integration (CharacterMotionSystem, head of World.Update,
        // strictly before GameplayProxy.Update) always uses the RAW impulse velocity RunOpcode just set
        // (175 px/s), never the decayed one EvaluateEntitySupport computes moments later THIS SAME
        // World.Update call (which only ever matters for whatever integration happens next - here,
        // immediately re-overwritten by the next iteration's own RunOpcode before that happens). A real
        // 0x1B issued from inside Update (the real script pipeline) would instead only take effect on the
        // FOLLOWING frame's integration, seeing the DECAYED 150 px/s the measured facts describe - covered
        // precisely by GullVerticalFidelityTests' own real-pipeline acceptance test. 10 ticks * 175 px/s *
        // (1/50 s/tick) = 35px.
        Assert.Equal(335f, entity.RootComponent!.Position.Z, 2);

        // Climb apex: real Tick program 134's own 0x63 [128,0,1] (clear Gravity) immediately followed by
        // 0x1B [0,0] (zero the vertical impulse/velocity - same shape the real program's own hover uses to
        // stop climbing exactly at the apex instead of merely disabling further acceleration).
        RunOpcode(0x63, 128, 0, 1, 0xFF);
        RunOpcode(0x1B, 0, 0, 0xFF);

        Assert.Equal(0u, proxy.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, proxy.Controller!.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.MaxFallSpeed);

        var hoverZ = entity.RootComponent!.Position.Z;

        // THE bug's own exact repro: pre-fix, the engine's own Settings.Gravity/MaxFallSpeed silently
        // stayed at the map's real 1250/800 here (the OLD ApplyGravitySettingsToController branched on the
        // Flags bit) and kept pulling the entity down every RENDERED frame regardless of the 50 Hz logic
        // tick rate - the gull fell through every one of these 60 ticks instead of holding. Post-fix, those
        // settings stay permanently pinned at 0 for every NPC (see that method's own doc) and this class'
        // own decay only runs while Flags.Gravity is set (cleared above by 0x63), so Z is bit-exact constant
        // (not just "close"): nothing ever writes to the root's own Z component again until the 0x1B
        // descent call below - no float round-trip is even in play (contrast the entity-support pin, which
        // DOES re-derive Pos every tick and needed a quantization-tolerant comparator).
        for (var frame = 0; frame < 60; frame++)
        {
            world.Update(1f / 50f);
            Assert.Equal(hoverZ, entity.RootComponent!.Position.Z);
            Assert.Equal(0f, proxy.Controller.Velocity.Z);
        }

        // Scripted descent: real 0x1B [127,255] (v1=127,v2=255 -> raw=(255<<8)|127=0xFF7F=65407,
        // SignExtend16=65407-65536=-129, ForceZ=-129*0x10000>>8=-33024, i.e. -0.50390625 px/tick/
        // -25.1953125 px/s). Gravity is STILL 0 here (only 0x1B was called, not 0x62 yet), so this is pure
        // constant-velocity motion, no acceleration - the hand-computed window below is exact float
        // arithmetic, not an approximation band.
        RunOpcode(0x1B, 127, 255, 0xFF);

        const int descentTicks = 20;
        for (var frame = 0; frame < descentTicks; frame++)
        {
            world.Update(1f / 50f);
        }

        var expectedDescentZ = hoverZ + descentTicks * (-33024 / 65536f);
        Assert.Equal(expectedDescentZ, entity.RootComponent!.Position.Z, 2);
        Assert.True(entity.RootComponent!.Position.Z < hoverZ, "expected the scripted descent to have lowered Z below the hover altitude.");

        var zBeforeGravityRestored = entity.RootComponent!.Position.Z;

        // Real 0x62 [128,0,1] (set mask 0x100 = Gravity) restores this entity's own per-tick decay through
        // the SAME resync helper as 0x63 above - confirms the fix is symmetric (0x62 also resyncs, not just
        // 0x63). The engine's own Settings.Gravity/MaxFallSpeed stay 0 regardless (they no longer branch on
        // this flag - see ApplyGravitySettingsToController's own doc); it is EvaluateEntitySupport's own
        // decay, gated on this same Flags bit, that resumes accelerating the fall from here on.
        RunOpcode(0x62, 128, 0, 1, 0xFF);

        Assert.Equal(EntityFlags.Gravity, proxy.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.MaxFallSpeed);

        for (var frame = 0; frame < 15; frame++)
        {
            world.Update(1f / 50f);
        }

        // The DLL's own decay now accelerates the fall well past the prior constant -25.2 px/s scripted
        // descent rate - the entity has genuinely resumed falling under EvaluateEntitySupport's own
        // MapGravityRaw decay, not merely continuing at the old scripted velocity.
        Assert.True(-proxy.Controller.Velocity.Z > 60f,
            $"expected restored gravity to accelerate the fall past 60 px/s within 15 ticks; got {-proxy.Controller.Velocity.Z}.");
        Assert.True(entity.RootComponent!.Position.Z < zBeforeGravityRestored,
            "expected Z to keep dropping once real gravity was restored.");
    }

    // -----------------------------------------------------------------------------------------
    // (4) Root-ownership replay of the E3.d pattern (AlundraCharacterControllerAdoptionTests
    // .RootOwnership_OneHundredFramesFlatWalk...) on an NPC: 100 frames of flat walking via a direct
    // Controller.Move (bypassing the scripted mover, isolating root-pull correctness), |Pos16.16 - pure
    // integration| bounded, no monotonic growth.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RootOwnership_OneHundredFramesFlatWalk_PosDriftFromPureIntegrationStaysBoundedAndDoesNotGrowMonotonically()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 920f, 80f), new FakeScriptHost());
        world.Update(1f / 50f);

        const float deltaXPixels = 2f;
        var deltaX1616 = (int)Math.Round((double)deltaXPixels * 65536.0);
        var expectedPosX1616 = proxy.PosX;

        var diffs = new int[100];
        for (var frame = 0; frame < 100; frame++)
        {
            proxy.Controller!.Move(new Vector3(deltaXPixels, 0f, 0f));
            world.Update(1f / 50f);

            expectedPosX1616 += deltaX1616;
            diffs[frame] = Math.Abs(proxy.PosX - expectedPosX1616);

            Assert.True(diffs[frame] <= 16, $"frame {frame}: |diff| {diffs[frame]} > 16 (16.16 units)");
        }

        var strictlyIncreasingRun = true;
        for (var i = 1; i < diffs.Length && strictlyIncreasingRun; i++)
        {
            strictlyIncreasingRun = diffs[i] > diffs[i - 1];
        }

        Assert.False(strictlyIncreasingRun, "the Pos*-vs-pure-integration drift grew monotonically over 100 frames.");
        _ = entity; // kept for symmetry with the hero's own pattern (root position not asserted directly here).
    }

    // -----------------------------------------------------------------------------------------
    // (5) End-to-end spawn regression (verifier finding F1, P2): ApplySpawnInitialization must itself
    // populate AnimSetsByAnim, not just a hand-built test fixture. Drives a REAL "Entities" record (bank
    // 146, pulled straight out of the real map 389 tileMapData) through the REAL spawn path -
    // AlundraWorldProxy.CreateEntityFromRecord -&gt; CreateEntityFromPrefab -&gt; ApplySpawnInitialization -
    // with a REAL SpriteRecordCatalog reading the real Data/sprite-records.json (so the AnimSets asserted
    // below come straight off the converter export, not a value this test transcribed by hand). Only the
    // "prefab" argument itself is a hand-built in-memory Entity: the headless test process has no live
    // AssetContentManager to Load&lt;Entity&gt; a real .entity file from disk (same constraint
    // CreateEntityFromRecord's own doc comment states, and the same pattern
    // AlundraWorldProxySpawnInitializationTests's own "Creation-flow integration" test already uses) - this
    // is the closest reachable seam to a genuinely full spawn (world's own InitializeWithWorld spawn loop)
    // short of a live GraphicsDevice/content pipeline. Without ApplySpawnInitialization's own
    // "proxy.AnimSetsByAnim = header.AnimSets" line, the first assertion below (AnimSetsByAnim non-null)
    // fails outright, and the walk-kinematics assertion after it would fail too (Speed resolves to 0 -
    // AlundraScriptedMotion.RunOneKinematicTick's own hasAnimSet miss).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EndToEndSpawn_RealBank146RecordThroughApplySpawnInitialization_PopulatesAnimSetsByAnimAndDrivesRealWalkKinematics()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot!);
        Assert.NotNull(tileMapData);

        var entitiesLayer = tileMapData!.ObjectLayers.FirstOrDefault(layer => layer.Name == "Entities");
        Assert.NotNull(entitiesLayer);
        var record = entitiesLayer!.Objects.FirstOrDefault(
            o => o.CustomProperties.TryGetValue("SpriteTableIndex", out var spriteTableIndex) && spriteTableIndex == "146");
        Assert.NotNull(record);

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var prefabRoot = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        prefabRoot.AddChildComponent(collisionComponent);

        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        controllerComponent.SetControlMode(CharacterControlMode.Script);

        var prefab = new Entity
        {
            Name = "Bank146Prefab",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = prefabRoot,
        };
        prefab.AddComponent(controllerComponent);

        // Real Data/sprite-records.json, resolved against the record's own real PrefabAssetId below - not
        // a FakeSpriteRecordCatalog with hand-copied values.
        var catalog = new SpriteRecordCatalog(projectRoot!);

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record!, _ => prefab, catalog, tileMapData: tileMapData);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);

        // The regression itself (F1): this is null without ApplySpawnInitialization's own assignment.
        Assert.NotNull(proxy.AnimSetsByAnim);
        Assert.NotEmpty(proxy.AnimSetsByAnim!);
        Assert.True(proxy.AnimSetsByAnim!.ContainsKey(10), "real bank-146 AnimSets should carry index 10 (Speed 128/Acceleration 6).");
        var animSetTen = proxy.AnimSetsByAnim[10];
        Assert.Equal(128, animSetTen.Speed);
        Assert.Equal(6, animSetTen.Acceleration);

        // Same caching this file's own BuildNpcPawn asserts explicitly - ApplySpawnInitialization's E4.b
        // "Spawn" block resolves it too (this class' own WalkKinematics test builds it by hand instead).
        Assert.NotNull(proxy.Controller);

        var world = BuildWorld(field);
        proxy.IsPlayer = false;
        proxy.ScriptHost = new FakeScriptHost();
        proxy.Status = EntityStatus.Normal;

        // Override the record's own real spawn position to the same open flat stretch (row 57, sol 80px)
        // every other test in this class uses, so the hand-computed displacement below is independent of
        // which real record happened to match SpriteTableIndex=146.
        proxy.PosX = 564 * 65536;
        proxy.PosY = 920 * 65536;
        proxy.PosZ = 80 * 65536;
        entity.RootComponent!.LocalTransform.Position = new Vector3(564f, 920f, 80f);

        world.AddEntity(entity);
        world.Update(1f / 50f); // register with CharacterMotionSystem before the scripted mover ever runs.

        proxy.TargetAnimationId = 10;
        proxy.TargetDirection = 24; // AnimationTables.CardinalDirectionTable[3] = 0x18, pure +X.

        // Same hand-computed reference as WalkKinematics_Bank146AnimTenRealSpeedAndAcceleration... above
        // (that test's own doc comment explains the one-frame CurrentAnimationId latency and the transient/
        // steady-state derivation in full).
        const int frames = 70;
        var expectedForce = 0;
        var expectedPositionDeltaPixels = 0.0;
        for (var frame = 2; frame <= frames; frame++)
        {
            expectedForce = Math.Min(expectedForce + 1536, 98304);
            expectedPositionDeltaPixels += expectedForce / 65536.0;
        }

        for (var frame = 1; frame <= frames; frame++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(564.0 + expectedPositionDeltaPixels, entity.RootComponent!.Position.X, 2);
    }

    // -----------------------------------------------------------------------------------------
    // (6) E4.d, item 7(1): REAL 0x1F walk - sailor-11's own Tick program (masked index 11, offset 1168,
    // docs/intro-programs-389.txt). The 0x1F occurrence at offset 1238 (params=[24,0] -> threshold 24px)
    // is immediately preceded by the 0x5B at offset 1234 (params=[128,1,67]): TargetAnimationId=1,
    // direction param 67 (0x43) -> high 3 bits = 67>>5 = 2 (cardinal mode) ->
    // AnimationTables.CardinalDirectionTable[67&3] = CardinalDirectionTable[3] = 0x18 (24, pure +X - same
    // "east" direction WalkKinematics' own test above uses). Real bank-146 AnimSets[1]
    // (alundra-project/Data/sprite-records.json, this prefab's own guid): Speed 160, Acceleration 0 (NO
    // ramp - IncrementForce jumps straight to the target force the very first tick it recomputes).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RealWalk_0x1F_SailorElevenProgramOffsetTwelveThirtyEight_CompletesAtHandComputedFrame()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (_, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 920f, 80f), new FakeScriptHost());

        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1; // as if the real 0x5B at offset 1234 had just run.
        proxy.TargetDirection = 24;

        // Real 0x1F occurrence (offset 1238, params=[24,0]), followed by a marker opcode (0x1A SetAnim
        // 254 - unreachable by any real AnimSet index) so completion is directly observable without
        // inspecting interpreter internals.
        WireRealTickProgram(proxy, new[] { 0x1F, 24, 0, 0x1A, 254, 0xFF });

        // Hand-computed reference: the 0x1F's own dispatch at World.Update call N reads PosX as of the
        // END of call N-1's own physics tick (AlundraEntityScriptProxy.Update runs pick/run BEFORE this
        // frame's own AlundraScriptedMotion tick). Call #1's own tick contributes 0 (CurrentAnimationId
        // still the spawn-time bit-complement, AnimSetsByAnim miss - E4.b's own documented one-frame
        // latency). From call #2's own tick on, Acceleration 0 jumps ForceX straight to TargetForceX =
        // OffsetXList[24]*160 = 0x300*160 = 122880 (16.16) EVERY tick (no ramp) = 1.875 px/tick exactly.
        // threshold(24px) <= floor(k*1.875) first holds at k=13 (floor(13*1.875)=floor(24.375)=24); the
        // number of already-applied force ticks by the time call N's own dispatch runs is (N-2) (calls
        // 2..N-1), so N-2>=13 -> N=15 is the first completing call.
        var completionFrame = -1;
        for (var frame = 1; frame <= 30 && completionFrame < 0; frame++)
        {
            world.Update(1f / 50f);
            if (proxy.TargetAnimationId == 254)
            {
                completionFrame = frame;
            }
        }

        Assert.Equal(15, completionFrame);
        Assert.Equal(0, proxy.EventProgramState.Parameters[1]); // resets like the original (generic post-dispatch bookkeeping).
    }

    // -----------------------------------------------------------------------------------------
    // (7) E4.d, item 7(2)/(3): a REAL map-389 wall (cell (24,39), walkability 1 - same cell/mask
    // AlundraCharacterControllerAdoptionTests.Mask_ClassBMaskOnEqualHeightCells_BlocksTheMove already
    // proves blocks under a ClassB mask 0x41) curtails a due-east walk. 0x1E gets a synthetic navigation
    // grid (map 389 itself has 0 blocked cells - E4.a's own finding) with the SAME cell blocked and
    // detours around it without ending; 0x1F has NO grid and ends immediately instead (D5: no detour for
    // 0x1F).
    // -----------------------------------------------------------------------------------------

    private static NavigationGrid2D BuildSyntheticGridMatchingMap389(params (int X, int Y)[] blockedCells)
    {
        var grid = new NavigationGrid2D(52, 60, 1f); // real map 389 dimensions (MapSize 52x60).
        var blocked = new HashSet<(int, int)>(blockedCells);
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 52; x++)
            {
                grid.SetCell(x, y, blocked.Contains((x, y))
                    ? NavigationGridCell.Blocked
                    : new NavigationGridCell(true, 1f, NavigationLayerMask.All));
            }
        }

        return grid;
    }

    [Fact]
    public void Walk0x1E_RealWallCurtailsMovement_NavigationDetourEngagesWithoutEndingTheWalk()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        proxy.Controller!.Settings.WalkabilityMask = 0x41u; // ClassB - cell (24,39) walkability 1 blocks.
        world.Update(1f / 50f);

        var grid = BuildSyntheticGridMatchingMap389((24, 39));
        var worldContext = new FakeNavigationWorldContext { NavigationGrid = grid };
        WireRealTickProgram(proxy, new[] { 0x1E, 24, 0, 0xFF }, worldContext);

        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1;
        proxy.TargetDirection = 24; // due east, straight at the wall.

        var sawRederivedDirection = false;
        for (var frame = 0; frame < 80; frame++)
        {
            world.Update(1f / 50f);
            if (proxy.TargetDirection != 24)
            {
                sawRederivedDirection = true;
            }
        }

        Assert.True(sawRederivedDirection, "TargetDirection should have been re-derived toward a detour waypoint once the wall curtailed movement.");
        Assert.NotNull(proxy.WalkDetourPath);
        _ = entity;
    }

    [Fact]
    public void Walk0x1E_RealWallCurtailsMovement_NoGridInjected_FailsToDetourAndKeepsPushing()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (_, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        proxy.Controller!.Settings.WalkabilityMask = 0x41u;
        world.Update(1f / 50f);

        // No worldContext -> NoOpEntityWorldContext -> NavigationGrid null - degraded mode.
        WireRealTickProgram(proxy, new[] { 0x1E, 24, 0, 0xFF });

        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1;
        proxy.TargetDirection = 24;

        for (var frame = 0; frame < 40; frame++)
        {
            world.Update(1f / 50f);
        }

        Assert.Null(proxy.WalkDetourPath);
        Assert.Equal(24u, proxy.TargetDirection); // unchanged - original "keep pushing" behavior.
    }

    [Fact]
    public void Walk0x1F_RealWallCurtailsMovement_EndsEarlyOnForceAdjustedRatherThanDistance()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        proxy.Controller!.Settings.WalkabilityMask = 0x41u;
        world.Update(1f / 50f);

        // No detour for 0x1F (D5) - no navigation grid needed here.
        WireRealTickProgram(proxy, new[] { 0x1F, 24, 0, 0x1A, 254, 0xFF });

        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1;
        proxy.TargetDirection = 24;

        var completionFrame = -1;
        for (var frame = 1; frame <= 40 && completionFrame < 0; frame++)
        {
            world.Update(1f / 50f);
            if (proxy.TargetAnimationId == 254)
            {
                completionFrame = frame;
            }
        }

        Assert.True(completionFrame > 0, "0x1F should complete once the wall curtails movement.");
        // Cell (24,39) starts at px 576 - only ~12px east of the 564px spawn, well under the 24px
        // distance threshold: this proves ForceAdjusted, not the distance test, ended the walk.
        Assert.True(entity.RootComponent!.Position.X < 564f + 24f);
    }

    // -----------------------------------------------------------------------------------------
    // (8) E4.d, item 7(4): navigation grid construction - real 389 grid via the real path, a synthetic
    // walkability-0x40 cell (not walkable), and the missing-layer degraded case.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void NavigationGrid_RealMap389_BuiltViaTheRealPath_NonNullAndCellEighteenFiftySevenWalkable()
    {
        var projectRoot = FindProjectRoot();
        var tileMapData = projectRoot == null ? null : LoadMap389TileMapData(projectRoot);
        if (tileMapData == null)
        {
            return;
        }

        var tileSets = LoadMap389TileSets(projectRoot!, tileMapData);

        var created = NavigationGrid2D.TryCreateFromTileMap(tileMapData, tileSets, 1f, out var grid);

        Assert.True(created);
        Assert.NotNull(grid);
        Assert.True(grid!.IsCellWalkable(18, 57, new NavigationQuery()));
    }

    [Fact]
    public void NavigationGrid_SyntheticWalkabilityMaskCell_TileWalkableFalse_IsNotWalkable()
    {
        // E4.a's own M=0x40 formula (docs/plan-e4-deplacement-scripte.md E4.a) folds walkability bit 6
        // into navigation.walkable="false" at conversion time - here the DLL side just needs to confirm
        // the engine's own NavigationGridCell.CanEnter honors that tile flag once loaded off a synthetic
        // TileSetData/TileMapData pair (no converter involved).
        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(2, 1) };
        tileMapData.TileSetDataAssetIds.Add(Guid.NewGuid());
        var navigationLayer = new TileMapLayerData { Name = "Navigation" };
        navigationLayer.CustomProperties["navigation.role"] = "grid";
        navigationLayer.tiles.Add(0); // walkable tile id
        navigationLayer.tiles.Add(1); // blocked tile id
        tileMapData.Layers.Add(navigationLayer);

        var tileSet = new TileSetData { TileSize = new CasaEngine.Core.Math.Size(24, 16) };
        var walkableTile = new StaticTileData { Id = 0 };
        walkableTile.CustomProperties["navigation.walkable"] = "true";
        var blockedTile = new StaticTileData { Id = 1 };
        blockedTile.CustomProperties["navigation.walkable"] = "false";
        tileSet.AddTile(walkableTile);
        tileSet.AddTile(blockedTile);

        var created = NavigationGrid2D.TryCreateFromTileMap(tileMapData, new List<TileSetData> { tileSet }, 1f, out var grid);

        Assert.True(created);
        var query = new NavigationQuery();
        Assert.True(grid!.IsCellWalkable(0, 0, query));
        Assert.False(grid.IsCellWalkable(1, 0, query));
    }

    [Fact]
    public void NavigationGrid_NoNavigationLayer_DegradesToFalseNullGrid()
    {
        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(1, 1) };
        tileMapData.Layers.Add(new TileMapLayerData { Name = "Ground" }); // no "navigation.role" property.
        tileMapData.Layers[0].tiles.Add(0);

        var created = NavigationGrid2D.TryCreateFromTileMap(tileMapData, new List<TileSetData>(), 1f, out var grid);

        Assert.False(created);
        Assert.Null(grid);
    }

    // -----------------------------------------------------------------------------------------
    // (9) E4.d, item 7(5): ForceAdjusted unit semantics - cleared once per frame at the top of the
    // scripted-motion pass, set when the controller's own Move returns a curtailed displacement.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ForceAdjusted_ClearedEachFrameTop_SetOnlyWhenARealWallCurtailsTheMove()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        var (_, proxy) = BuildNpcPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        proxy.Controller!.Settings.WalkabilityMask = 0x41u; // ClassB - cell (24,39) walkability 1 blocks.
        world.Update(1f / 50f);

        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 },
        };
        proxy.TargetAnimationId = 1;
        proxy.TargetDirection = 24; // due east, straight at the wall (cell (24,39) starts at px 576).

        var hitFrame = -1;
        for (var frame = 1; frame <= 20 && hitFrame < 0; frame++)
        {
            world.Update(1f / 50f);

            if (proxy.ForceAdjusted != 0)
            {
                hitFrame = frame;
            }
            else
            {
                Assert.Equal(0, proxy.ForceAdjusted); // clear every frame while still moving freely.
            }
        }

        Assert.True(hitFrame > 0, "the wall should eventually curtail the eastward walk.");

        // Turn away from the wall - nothing curtails a westward move here, so ForceAdjusted must clear
        // again at the very next frame's own top-of-tick reset (AlundraScriptedMotion.TickScriptedNpc).
        proxy.TargetDirection = 8; // reverse of 24 ((24+0x10)&0x1f), matching opcode 0x0A's own formula.
        world.Update(1f / 50f);

        Assert.Equal(0, proxy.ForceAdjusted);
    }

    // -----------------------------------------------------------------------------------------
    // (10) E4.d, item 7(6): TileZ deferral fix (E4.c) - refreshed every tick, not just at spawn, for an
    // entity with real vertical motion (0x1B-equivalent SetVerticalVelocity).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TileZ_EntityWithVerticalMotion_TracksPosZShiftedTwentyAcrossFrames()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Cell (18,57): flat, sol 80px (same cell/spawn AlundraNpcCharacterControllerMoverTests' own
        // VerticalImpulse_RealBlockEighteen0x1BValue... test uses) - 50px fall, same constant-velocity
        // shape (0x17 clears gravity: Flags carries no Gravity bit, so ApplyGravitySettingsToController
        // zeroes both Settings.Gravity/MaxFallSpeed - a clean, non-accelerated fall).
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 130f), new FakeScriptHost());
        world.Update(1f / 50f);

        // Bug fix follow-up: EvaluateEntitySupport's own "not found" tail now drives the controller's
        // vertical velocity from FinalForceZ every tick (see that method's own doc) - a bare
        // Controller.SetVerticalVelocity call with no matching ForceZ would be silently overwritten back to
        // 0 on the very next tick. ForceZ=-65536 is the same real 0x1B block-18 impulse value
        // VerticalImpulse_RealBlockEighteen0x1BValue... uses (-50 px/s, no Gravity flag so no decay - a
        // clean, non-accelerated fall, matching this test's own "0x1B-equivalent" doc).
        proxy.ForceZ = -65536;

        for (var frame = 0; frame < 100 && proxy.IsOnGround == 0; frame++)
        {
            world.Update(1f / 50f);

            Assert.Equal(proxy.PosZ >> 20, proxy.TileZ);
        }

        Assert.Equal(1, proxy.IsOnGround);
        Assert.Equal(proxy.PosZ >> 20, proxy.TileZ);
        _ = entity;
    }

    // -----------------------------------------------------------------------------------------
    // E4.f (docs/plan-e4-deplacement-scripte.md §3 E4.f, decision E4-4) acceptance (a)-(e), verifier A5.
    // Real map-389 fixture: record 2 (the platform sailor 11 actually perches on - verifier A6, NOT
    // record 5, which sits one tile row south) and record 11 (sailor 11 itself). Both records share the
    // SAME real XPos/YPos (38,72 -> 468,584 px) - record 2's Height 46 -> PosZ 368px, its own real header
    // (bank PrefabAssetId 09ed197d..., SizeZ 32) gives Depth = 32<<16-1 -> top = 368px - 1/65536 = 399.99998
    // px, STRICTLY below record 11's own real spawn Height 50 -> PosZ 400px exactly - this is the real
    // "off by one 16.16 unit" edge the plan's own E4.f "Pourquoi" note describes, not a synthetic one.
    // -----------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy BuildRealRecordProxy(
        TileMapData tileMapData, SpriteRecordCatalog catalog, int recordIndex)
    {
        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var record = entitiesLayer.Objects.First(
            o => o.CustomProperties.TryGetValue("Index", out var idx) && idx == recordIndex.ToString());

        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        var backingEntity = new Entity();
        proxy.LogicContextEntity = backingEntity;
        // No controller/world needed for a pure candidate/search fixture - ApplySpawnInitialization sets
        // Flags/Width/Height/Depth/Mod*/AnimSetsByAnim from the real header regardless (only the
        // Controller-gated Gravity/MaxFallSpeed/WalkabilityMask override needs a real Controller).
        AlundraWorldProxy.ApplySpawnInitialization(record, backingEntity, proxy, catalog, tileMapData: tileMapData);
        return proxy;
    }

    /// <summary>Real record 11 (sailor 11), spawned through the SAME <c>CreateEntityFromRecord</c> path
    /// test (5)/(6) above already use, under a real controller/world so <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>'s
    /// own controller-pinning half (<c>Settings.Gravity = 0</c>, <c>PushLogicalPositionToRoot</c>) is
    /// exercised for real - not overridden to a synthetic position like test (5)/(6): the real record's
    /// own (468,584,400) spawn is exactly the scenario under test.</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildRealSailor11Pawn(
        World world, TileMapData tileMapData, SpriteRecordCatalog catalog, CharacterControllerSettings settings, IAlundraScriptHost host)
    {
        var entitiesLayer = tileMapData.ObjectLayers.First(l => l.Name == "Entities");
        var record = entitiesLayer.Objects.First(
            o => o.CustomProperties.TryGetValue("Index", out var idx) && idx == "11");

        var prefabRoot = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        prefabRoot.AddChildComponent(collisionComponent);

        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        controllerComponent.SetControlMode(CharacterControlMode.Script);

        var prefab = new Entity
        {
            Name = "Bank146PrefabRecord11",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = prefabRoot,
        };
        prefab.AddComponent(controllerComponent);

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, _ => prefab, catalog, tileMapData: tileMapData);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.IsPlayer = false;
        proxy.ScriptHost = host;
        proxy.Status = EntityStatus.Normal;

        world.AddEntity(entity);
        return (entity, proxy);
    }

    /// <summary>(a) sailor 11 supported at Z ~= 400px from frame 0 INCLUSIVE (no first-frame dip), Gravity
    /// flag set, held for >= 60 frames - the plan's own primary E4.f acceptance test, plus the STRICT
    /// comparator's own real edge (verifier A1/A6): record 2's top sits 1/65536 below record 11's own real
    /// feet, which is exactly what makes the support succeed.</summary>
    [Fact]
    public void Support_SailorElevenOnRealRecordTwoPlatform_HeldAtZApprox400FromFrameZeroInclusiveFor60Frames()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot!)!;
        var catalog = new SpriteRecordCatalog(projectRoot!);
        var platformProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 2);
        Assert.True((platformProxy.Flags & EntityFlags.Collidable) != 0, "record 2's real header should carry Collidable.");

        var world = BuildWorld(field);
        var host = new FakeScriptHost();
        host.Collidables.Add(platformProxy);

        var (entity, proxy) = BuildRealSailor11Pawn(world, tileMapData, catalog, settings, host);
        Assert.True((proxy.Flags & EntityFlags.Gravity) != 0, "record 11's real header should carry Gravity.");

        // Derived from the REAL spawned platform's own fields (not a hand-transcribed literal): record 2's
        // own PosZ already carries ApplySpawnInitialization's real EntityManager.cs:119 spawn offset
        // ("PosZ = PosZ - ModZ + 1") by this point, so candidateTop = platformProxy.PosZ + ModZ + Depth,
        // and the support clamp's own "+1" (PhysicsEngine.cs:219/226/240/247, port verified by
        // TryFindSupport_StrictComparator's own unit test above) gives the exact value sailor 11's PosZ
        // gets pinned to. Comes out to 26214401 (400.0000153px) on the real map-389 export - one 16.16
        // unit above the naive 50<<19 (26214400/400.0px) a caller ignoring the real spawn offset would
        // expect.
        var expectedPosZ = platformProxy.PosZ + platformProxy.ModZ + platformProxy.Depth + 1;
        Assert.InRange(expectedPosZ / 65536.0, 399.99, 400.01); // sanity: still ~400px, real header/spawn numbers.

        // Mirrors AlundraWorldProxy's OWN spawn-time call (map-load loop / SpawnEntityByRecordId) -
        // PushLogicalPositionToRoot first (root already placed by CreateEntityFromPrefab's own spawn
        // write), THEN the one-shot immediate support evaluation - BOTH before this test's first
        // World.Update, matching "frame 0 inclusive".
        proxy.PushLogicalPositionToRoot();
        proxy.EvaluateEntitySupport(host.Collidables, immediateAtSpawn: true);

        // Frame 0 INCLUSIVE - no World.Update has run yet. The DLL's own 16.16 arithmetic is bit-exact
        // here (matches expectedPosZ exactly).
        Assert.Equal(expectedPosZ, proxy.PosZ);
        Assert.Equal(400f, entity.RootComponent!.Position.Z, 2);
        Assert.True(proxy.WasEntitySupportedLastTick);

        // Engine-integration fix (coordinator-dispositioned FIX after the finding this test itself first
        // surfaced): record 2/11's real authored margin is exactly ONE 16.16 unit (the STRICT comparator's
        // own edge, verified above) - below float32's own representable precision at ~400px magnitude
        // (ULP ~1/32 px, i.e. ~2048 16.16 units). Without AlundraEntityScriptProxy.Update's own
        // WasEntitySupportedLastTick pull-preservation (see that field's own doc), the FIRST real
        // World.Update would collapse PosZ from 26214401 to 26214400 by re-quantizing it through the
        // engine's float Vector3 root transform, permanently defeating the strict comparator one tick after
        // the entity settles. With the fix, PosZ stays the DLL's own source of truth (never re-pulled from
        // the float root) for as long as the entity remains supported - bit-exact across REAL engine ticks,
        // not just direct EvaluateEntitySupport calls (see this class' own
        // Support_SailorElevenOnRealRecordTwoPlatform_LogicLevel...  test below for the float-round-trip-
        // free variant kept as a narrower regression net on the port itself).
        for (var frame = 1; frame <= 60; frame++)
        {
            world.Update(1f / 50f);
            Assert.Equal(expectedPosZ, proxy.PosZ);
            Assert.Equal(400f, entity.RootComponent!.Position.Z, 2);
            Assert.True((proxy.Flags & EntityFlags.Gravity) != 0);
            Assert.True(proxy.WasEntitySupportedLastTick);
        }

        // Clean transition off support: 0x17 (Gravity flag cleared) + walk west far enough to leave record
        // 2's own real footprint (same real anim 1, Speed 160/Acceleration 0, threshold well past the
        // combined half-widths - see Support_WalkingWhileSupported's own doc for the exact mechanics).
        // Once support drops, WasEntitySupportedLastTick clears and the float pull resumes for Z; since
        // Gravity is already off (scripted, not the support clamp's own doing), Z simply stays constant
        // (floating) instead of falling - the plan's own (b) shape, verified here as (a)'s own natural
        // conclusion rather than a separate scenario.
        proxy.Flags &= ~EntityFlags.Gravity;
        proxy.ApplyGravitySettingsToController();
        proxy.TargetDirection = 8; // west, real OffsetXList[8] = -768.
        proxy.TargetAnimationId = 1; // real AnimSets[1]: Speed 160, Acceleration 0.

        var lostSupportAtFrame = -1;
        for (var frame = 61; frame <= 100 && lostSupportAtFrame < 0; frame++)
        {
            world.Update(1f / 50f);
            if (!proxy.WasEntitySupportedLastTick)
            {
                lostSupportAtFrame = frame;
            }
        }

        Assert.True(lostSupportAtFrame > 0, "expected the walk to carry sailor 11 off record 2's real footprint within 40 more frames.");

        // WasEntitySupportedLastTick's own one-frame-late transition (documented at its own declaration):
        // the frame that FIRST reads false was itself pulled at the HEAD of that same frame using the
        // PRIOR (still true) flag, so its own PosZ is still the preserved logical value, not yet a real
        // float pull. One more tick lets the pull itself catch up to the now-false flag - THIS is the
        // first frame whose PosZ is an actual float-root read, so it is the right baseline for "stays
        // constant while floating" rather than the still-preserved value one frame before it.
        world.Update(1f / 50f);
        Assert.False(proxy.WasEntitySupportedLastTick);
        var zAfterLosingSupport = proxy.PosZ;

        for (var frame = 0; frame < 10; frame++)
        {
            world.Update(1f / 50f);
            Assert.False(proxy.WasEntitySupportedLastTick); // stays unsupported - it walked away, not back.
            Assert.Equal(zAfterLosingSupport, proxy.PosZ); // floats - Gravity was cleared before the walk.
            Assert.True((proxy.Flags & EntityFlags.Gravity) == 0);
        }
    }

    /// <summary>(a) continued - the SAME claim as
    /// <see cref="Support_SailorElevenOnRealRecordTwoPlatform_HeldAtZApprox400FromFrameZeroInclusiveFor60Frames"/>
    /// but exercised via direct, repeated <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>
    /// calls (bypassing <c>World.Update</c>'s own controller/float round-trip entirely) - a narrower
    /// regression net directly on the port's own 16.16 arithmetic, independent of
    /// <see cref="AlundraEntityScriptProxy.WasEntitySupportedLastTick"/>'s own pull-preservation fix (which
    /// the test above now covers through the real engine frame loop).</summary>
    [Fact]
    public void Support_SailorElevenOnRealRecordTwoPlatform_LogicLevelDirectEvaluateCallsStayBitExact()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot!)!;
        var catalog = new SpriteRecordCatalog(projectRoot!);
        var platformProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 2);

        var world = BuildWorld(field);
        var host = new FakeScriptHost();
        host.Collidables.Add(platformProxy);

        var (_, proxy) = BuildRealSailor11Pawn(world, tileMapData, catalog, settings, host);
        var expectedPosZ = platformProxy.PosZ + platformProxy.ModZ + platformProxy.Depth + 1;

        proxy.PushLogicalPositionToRoot();
        proxy.EvaluateEntitySupport(host.Collidables, immediateAtSpawn: true);
        Assert.Equal(expectedPosZ, proxy.PosZ);

        for (var frame = 1; frame <= 60; frame++)
        {
            proxy.EvaluateEntitySupport(host.Collidables);
            Assert.Equal(expectedPosZ, proxy.PosZ);
            Assert.True((proxy.Flags & EntityFlags.Gravity) != 0);
        }
    }

    /// <summary>(a) continued - the STRICT comparator itself (PhysicsEngine.cs:205, verifier A1): a
    /// candidate whose top sits EXACTLY at the subject's own feet does NOT support (real map-389 body
    /// boxes never actually produce this - their own Depth is always SizeZ&lt;&lt;16-1, one unit short of
    /// a full edge, which is what DOES support, tested second here with the real record 2/11 numbers).</summary>
    [Fact]
    public void TryFindSupport_StrictComparator_ExactTopDoesNotSupport_OneUnitBelowDoes()
    {
        var subject = new AlundraEntityScriptProxy { PosX = 0, PosY = 0, PosZ = 1000, Width = 100, Height = 100 };
        var candidateAtFeet = new AlundraEntityScriptProxy { PosX = 0, PosY = 0, PosZ = 0, Width = 100, Height = 100, Depth = 1000 }; // top == 1000 == subject's feet, exactly.
        var candidateOneUnitBelow = new AlundraEntityScriptProxy { PosX = 0, PosY = 0, PosZ = 0, Width = 100, Height = 100, Depth = 999 }; // top == 999, strictly below.

        Assert.False(EntitySupport.TryFindSupport(subject, new List<AlundraEntityScriptProxy> { candidateAtFeet }, platformTopZSeed: int.MinValue, out _, out _));
        Assert.True(EntitySupport.TryFindSupport(subject, new List<AlundraEntityScriptProxy> { candidateOneUnitBelow }, platformTopZSeed: int.MinValue, out var support, out var supportTopZ));
        Assert.Same(candidateOneUnitBelow, support);
        Assert.Equal(1000, supportTopZ); // candidateTop (999) + 1.

        // The real record 2/11 numbers reproduce the SAME "one unit below" shape, off THEIR OWN raw
        // (pre-spawn-offset) PosZ: record 2's Depth is 32<<16-1 = 2097151, its raw PosZ (368px = 24117248)
        // + Depth = 26214399 - exactly 1 unit below record 11's own raw feet (400px = 26214400). The next
        // test's own expectedPosZ derivation additionally applies ApplySpawnInitialization's real "+1"
        // spawn offset (EntityManager.cs:119) BOTH records receive, which does not change this "one unit
        // below" relationship - it shifts both sides by the same +1.
        const int record2Top = (46 << 19) + (32 << 16) - 1;
        const int record11Feet = 50 << 19;
        Assert.Equal(record11Feet - 1, record2Top);
    }

    /// <summary>(b)/(d) walking while supported: X (and Y) advance normally, Z stays pinned to the real
    /// platform top; once the walk carries it off the platform's own real footprint, with 0x17's own
    /// effect already applied (Gravity flag cleared, matching sailor 11's real program 139 sequence at
    /// offset 1193 - <see cref="AlundraEntityScriptProxy.ApplyGravitySettingsToController"/> zeroes
    /// Settings.Gravity/MaxFallSpeed), Z simply stays constant (floating, no fall) - the same real anim 1
    /// (Speed 160, Acceleration 0) this file's own test (6) already verified end-to-end for 0x1F.</summary>
    [Fact]
    public void Support_WalkingWhileSupported_XYAdvanceZPinned_ThenOffFootprintWithGravityOff_ZFloatsConstant()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot!)!;
        var catalog = new SpriteRecordCatalog(projectRoot!);
        var platformProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 2);

        var world = BuildWorld(field);
        var host = new FakeScriptHost();
        host.Collidables.Add(platformProxy);

        var (entity, proxy) = BuildRealSailor11Pawn(world, tileMapData, catalog, settings, host);
        proxy.PushLogicalPositionToRoot();
        proxy.EvaluateEntitySupport(host.Collidables, immediateAtSpawn: true);
        var pinnedPosZ = proxy.PosZ;
        // See the previous test's own derivation - the support clamp pins to record 2's real
        // PosZ+ModZ+Depth+1 (~400.0000153px, ApplySpawnInitialization's own EntityManager.cs:119 spawn
        // offset already folded into platformProxy.PosZ at this point).
        Assert.Equal(platformProxy.PosZ + platformProxy.ModZ + platformProxy.Depth + 1, pinnedPosZ);

        // 0x09 direction 8 (west, OffsetXList[8] real value -768) + 0x1A anim 1 (real AnimSets[1]: Speed
        // 160, Acceleration 0 - already populated by ApplySpawnInitialization from the real header, same
        // as test (6)'s own doc). No 0x1F threshold this time - this test drives the walk itself for a
        // fixed frame budget instead, so it can observe the support relationship end mid-walk.
        proxy.TargetDirection = 8;
        proxy.TargetAnimationId = 1;

        // 0x17 - clears Gravity so the entity floats once support ends (does not fall to terrain).
        proxy.Flags &= ~EntityFlags.Gravity;
        proxy.ApplyGravitySettingsToController();

        var startX = entity.RootComponent!.Position.X;
        var startY = entity.RootComponent!.Position.Y;
        var lostSupportAtFrame = -1;
        var settledPosZ = -1; // captured after World.Update call #1.

        // Engine-integration fix (WasEntitySupportedLastTick, see its own doc): while the entity remains
        // entity-supported, its logical PosZ is now preserved bit-exact (never re-quantized through the
        // float32 root) - so settledPosZ is expected to equal pinnedPosZ exactly here, unlike before that
        // fix (when the very first World.Update already eroded it by one unit). The pin only actually
        // erodes once support genuinely ends and the float pull resumes - handled below via
        // postSupportPosZ, the SAME one-frame-late-transition shape
        // Support_SailorElevenOnRealRecordTwoPlatform_...'s own "clean transition off support" section
        // documents (WasEntitySupportedLastTick's own flip is read one frame behind the pull that used
        // it).
        var postSupportPosZ = -1;
        var sawUnsupportedFrame = false;

        for (var frame = 1; frame <= 40; frame++)
        {
            world.Update(1f / 50f);

            if (frame == 1)
            {
                settledPosZ = proxy.PosZ;
                Assert.Equal(pinnedPosZ, settledPosZ);
            }

            if (proxy.WasEntitySupportedLastTick)
            {
                // Z stays pinned to the real platform top for as long as the support relationship holds.
                Assert.Equal(settledPosZ, proxy.PosZ);
            }
            else if (!sawUnsupportedFrame)
            {
                // First frame the flag reads false - still the one-frame-late transition (this frame's own
                // pull ran using LAST tick's still-true flag), so PosZ is still the preserved value here,
                // not yet a real float pull.
                Assert.Equal(settledPosZ, proxy.PosZ);
                sawUnsupportedFrame = true;
            }
            else if (postSupportPosZ < 0)
            {
                // First REAL float-pulled frame past the transition - captured as the new baseline (the
                // pull is no longer obligated to reproduce the pre-transition pin bit-for-bit past the
                // engine's own float32 Vector3 pose).
                postSupportPosZ = proxy.PosZ;
            }
            else
            {
                // Stays constant thereafter (0x17 already cleared gravity, so nothing pulls it down).
                Assert.Equal(postSupportPosZ, proxy.PosZ);
            }

            var stillSupported = EntitySupport.IsEligibleSubject(proxy)
                && EntitySupport.TryFindSupport(proxy, host.Collidables, int.MinValue, out _, out _);

            if (!stillSupported && lostSupportAtFrame < 0)
            {
                lostSupportAtFrame = frame;
            }
        }

        // (d) X/Y free while Z stays pinned/constant throughout - direction 8's real OffsetXList entry is
        // pure -X (OffsetYList[8] = 0), so only X actually moves for this specific direction; a real
        // record 11 anim/direction with a non-zero OffsetY (e.g. direction 0, OffsetYList[0] = 0x200) would
        // move Y the same way - the mechanism (TickScriptedNpc's own X/Y integration, entirely unaffected
        // by the Z clamp) is direction-agnostic, so covering X here and citing the real offset table for Y
        // is sufficient - see AnimationTables.OffsetXList/OffsetYList's own real values.
        Assert.True(entity.RootComponent!.Position.X < startX, "walking west should move X.");
        Assert.Equal(startY, entity.RootComponent!.Position.Y, 2);
        Assert.True(lostSupportAtFrame > 0 && lostSupportAtFrame <= 40,
            $"expected the walk to carry sailor 11 off record 2's real footprint within 40 frames; support never ended (or ended before frame 1).");
    }

    /// <summary>(c) 0x1B vertical descent (real value: params [0,255] -&gt; ForceZ = -65536 = -1 px/tick =
    /// -50 px/s at 50 Hz, the SAME real impulse this file's own VerticalImpulse test and the intro trace's
    /// own sailor-11/block-18 derivations already established) reaches the TileZ-12 window (192-207px) at
    /// the hand-computed frame: starting from the real perched height 400px, gravity off (0x17, matching
    /// sailor 11's own real program sequence), falling at exactly 1 px/tick needs 400-207=193 to
    /// 400-192=208 ticks to enter [192,207] - asserted against the REAL per-tick <see cref="World.Update"/>
    /// count, not a synthetic value.</summary>
    [Fact]
    public void Support_VerticalDescentAfterLeavingPlatform_ReachesTileZTwelveWindowAtHandComputedFrame()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot!)!;
        var catalog = new SpriteRecordCatalog(projectRoot!);
        var world = BuildWorld(field);
        var host = new FakeScriptHost(); // no collidables - already off any platform's footprint, by construction.

        var (entity, proxy) = BuildRealSailor11Pawn(world, tileMapData, catalog, settings, host);
        world.Update(1f / 50f); // register with CharacterMotionSystem before manual repositioning below.

        // Start already off the platform footprint (far away in X), at the real perched height (400px) -
        // isolates the descent itself from the walk-off mechanics test (b)/(d) already cover.
        proxy.PosX = 200 * 65536;
        proxy.PosZ = 50 << 19;
        var repositioned = new Vector3(200f, entity.RootComponent!.Position.Y, 400f);
        entity.RootComponent!.LocalTransform.Position = repositioned;
        proxy.Controller!.Teleport(repositioned);

        // 0x17 - clears gravity (real program 139 sequence, offset 1193).
        proxy.Flags &= ~EntityFlags.Gravity;
        proxy.ApplyGravitySettingsToController();

        // 0x1B [0,255] - real value, -1 px/tick.
        proxy.ForceZ = -65536;
        proxy.Controller!.SetVerticalVelocity(-50f);

        var frame = 0;
        while (frame < 300 && entity.RootComponent!.Position.Z >= 208f)
        {
            world.Update(1f / 50f);
            frame++;
        }

        Assert.InRange(frame, 193, 208);
        Assert.InRange(entity.RootComponent!.Position.Z, 192f, 207.999f);
        Assert.Equal(12, (int)entity.RootComponent!.Position.Z / 16);
    }

    /// <summary>(e) searches 5/6 driven by REAL <see cref="AlundraEntityScriptProxy.RidingEntity"/> (real
    /// record 2/11 numbers reproduce <c>CheckRidingEntities</c>'s own EXACT-match test, verifier A1's own
    /// doc on <see cref="EntitySupport.UpdateRidingEntities"/>) + functions 5-11 exclude the player
    /// (verifier: <see cref="EntitySearchService"/>'s own E4.f fix).</summary>
    [Fact]
    public void EntitySearchService_Searches5And6_DrivenByRealRidingEntity_AndPlayerExcludedFromFunctions5To11()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot)!;
        var catalog = new SpriteRecordCatalog(projectRoot);
        var platformProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 2);
        var sailorProxy = BuildRealRecordProxy(tileMapData, catalog, recordIndex: 11);

        var collidables = new List<AlundraEntityScriptProxy> { platformProxy, sailorProxy };
        EntitySupport.UpdateRidingEntities(collidables);

        // Real numbers (verifier A1's own doc): record 2's top + 1 == record 11's own real ModdedPosZ
        // exactly (both ModZ 0 here) - the EXACT-match CheckRidingEntities test.
        Assert.NotNull(sailorProxy.RidingEntity);
        Assert.Same(platformProxy.LogicContextEntity, sailorProxy.RidingEntity);
        Assert.Null(platformProxy.RidingEntity); // platform's own Flags carry no Gravity bit - never a rider itself.

        var playerProxy = new AlundraEntityScriptProxy { IsPlayer = true, Status = EntityStatus.Normal, LogicContextEntity = new Entity() };
        // Coincidentally wire the player into the SAME relationships every function 5-11 below reads, so a
        // failure to exclude it would show up as a false-positive match, not merely an absent one.
        playerProxy.RidingEntity = platformProxy.LogicContextEntity;
        sailorProxy.RidingEntity = platformProxy.LogicContextEntity; // re-affirm after UpdateRidingEntities above.
        platformProxy.RidingEntity = playerProxy.LogicContextEntity;
        sailorProxy.XCollisionEntity = playerProxy.LogicContextEntity;
        playerProxy.XCollisionEntity = sailorProxy.LogicContextEntity;
        sailorProxy.ParentEntity = playerProxy.LogicContextEntity;
        playerProxy.ParentEntity = sailorProxy.LogicContextEntity;
        playerProxy.PlatformEntity = sailorProxy.LogicContextEntity;

        var spawned = new List<AlundraEntityScriptProxy> { platformProxy, sailorProxy, playerProxy };

        // Search 5: entities the owner (platform) is riding on - matches sailorProxy.RidingEntity ==
        // platform.LogicContextEntity... wait, search 5 is FROM the owner's own RidingEntity, so exercise
        // it with owner = sailorProxy (its own RidingEntity == platform.LogicContextEntity).
        var search5 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 5, spawned);
        Assert.Contains(platformProxy, search5);
        Assert.DoesNotContain(playerProxy, search5); // player's own RidingEntity coincidentally matches too - must still be excluded.

        // Search 6: entities riding on the owner (platform) - matches sailorProxy.RidingEntity == platform.
        var search6 = EntitySearchService.GetMatchingEntitiesBySearchType(platformProxy, 0x80 | 6, spawned);
        Assert.Contains(sailorProxy, search6);
        Assert.DoesNotContain(playerProxy, search6);

        var search7 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 7, spawned);
        Assert.DoesNotContain(playerProxy, search7);

        var search8 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 8, spawned);
        Assert.DoesNotContain(playerProxy, search8);

        var search9 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 9, spawned);
        Assert.DoesNotContain(playerProxy, search9);

        var search10 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 10, spawned);
        Assert.DoesNotContain(playerProxy, search10);

        var search11 = EntitySearchService.GetMatchingEntitiesBySearchType(sailorProxy, 0x80 | 11, spawned);
        Assert.DoesNotContain(playerProxy, search11);
    }

    // -----------------------------------------------------------------------------------------
    // Vertical fidelity fix (user-reported runtime timing bug, gull entity 6 of map 389): a controller-
    // driven NPC's vertical is now entirely owned by AlundraEntityScriptProxy.EvaluateEntitySupport's own
    // per-tick decay + SetVerticalVelocity, never by the engine's own continuous Settings.Gravity/
    // MaxFallSpeed (see that method's own doc and ApplyGravitySettingsToController's own doc for the full
    // measured numbers: ~158 px/s vs the faithful 150 px/s, 179.25 px vs 209.25 px westward drift).
    // -----------------------------------------------------------------------------------------

    /// <summary>Re-applies the real block-18-shaped 0x1B [128,3] impulse (gull entity 6's own real Tick
    /// program 134 does this every logic tick via its own Wait(1)-gated loop before the climb apex - see
    /// GullHoverCycle's own doc above) through the REAL <see cref="AlundraEventProgramRunner"/> opcode
    /// dispatch (not a hand-rolled ForceZ/SetVerticalVelocity assignment - same formula, real dispatch
    /// code), once per <see cref="AlundraEntityScriptProxy.Update"/>'s own per-LOGIC-tick pick/run call
    /// (never once per rendered frame - see that method's own E4.b doc): <see cref="AlundraEntityScriptProxy.PickEventTrigger"/>
    /// re-selects slot C (Tick) every logic tick regardless of the previous call's own outcome, and
    /// <see cref="AlundraEntityScriptProxy.RunPickedEvent"/> dispatches through this runner - this is what
    /// makes dt=1/123 vs dt=1/50 behave identically for the real gull scenario below:
    /// <see cref="IAlundraScriptHost.LogicTicksThisFrame"/> decouples this from the render rate exactly
    /// like production (<see cref="AlundraLogicClock"/>'s own class doc).</summary>
    private sealed class GullClimbRunner : IEventProgramRunner
    {
        private static readonly byte[] ClimbBytes = { 0x1B, 128, 3, 0xFF };
        private readonly AlundraEventProgramRunner _opcodeRunner = new(document: null, new AlundraGameState(), worldContext: null);

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (programSlot != ScriptHelper.ProgramCTick)
            {
                return;
            }

            // Fresh throwaway state every call (never resumed) - deliberately: this stand-in for program
            // 134's own real bytecode only needs to dispatch ONE instruction (0x1B) per call, so it always
            // starts back at CodeIndex 0, same shape the non-resuming A/D/E/F slots already use (see
            // AlundraEventProgramRunner.RunScript's own class doc on _slotAScratchState).
            var state = new EventProgramState { Codes = ClimbBytes };
            _opcodeRunner.RunOneScriptCall(entity, state, programSlot);
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Real <see cref="AlundraLogicClock"/>-driven host (NOT <see cref="FakeScriptHost"/>'s own
    /// always-exactly-one-tick clock) - the whole point of the dt=1/123 vs dt=1/50 acceptance below: a
    /// real render-rate-independent accumulator, exactly what production wires up. <see cref="TotalTicks"/>
    /// is the test's own direct, exact observable for "how many real logic ticks have elapsed so far" -
    /// more precise than re-deriving it from a frame counter and dt (which would need to reproduce
    /// <see cref="AlundraScriptedMotion.MaxTicksPerFrame"/>'s own catch-up-cap rounding by hand).</summary>
    private sealed class GullClimbScriptHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; } = new GullClimbRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity => null;
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public List<AlundraEntityScriptProxy> Collidables { get; } = new();
        IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

        public int TotalTicks { get; private set; }

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        private readonly AlundraLogicClock _logicClock = new();
        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _logicClock.TicksThisFrame(elapsedTime);
            _logicClock.CloseFrame();
            TotalTicks += ticks;
            return ticks;
        }
    }

    /// <summary>Real record spawn (bank 146, XPos 66/YPos 71 - "Entity_6", the real gull the bug was
    /// measured on; EventCodesC_TickIndex 134 in the real tileMap confirms it) through
    /// <see cref="AlundraWorldProxy.CreateEntityFromRecord"/>, same pattern as
    /// <c>EndToEndSpawn_RealBank146RecordThroughApplySpawnInitialization_PopulatesAnimSetsByAnimAndDrivesRealWalkKinematics</c>
    /// above. Null when this checkout has no real bank-146 controller settings to load (self-skip, same
    /// convention as every other real-fixture test in this class).</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy)? SpawnRealGullEntitySix(string projectRoot, TileMapData tileMapData)
    {
        var entitiesLayer = tileMapData.ObjectLayers.FirstOrDefault(layer => layer.Name == "Entities");
        Assert.NotNull(entitiesLayer);
        var record = entitiesLayer!.Objects.FirstOrDefault(
            o => o.CustomProperties.TryGetValue("XPos", out var xPos) && xPos == "66"
                && o.CustomProperties.TryGetValue("YPos", out var yPos) && yPos == "71");
        Assert.NotNull(record);

        var settings = LoadBank146ControllerSettings(projectRoot);
        if (settings == null)
        {
            return null;
        }

        var prefabRoot = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        prefabRoot.AddChildComponent(collisionComponent);

        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        controllerComponent.SetControlMode(CharacterControlMode.Script);

        var prefab = new Entity
        {
            Name = "GullSixPrefab",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = prefabRoot,
        };
        prefab.AddComponent(controllerComponent);

        var catalog = new SpriteRecordCatalog(projectRoot);
        var entity = AlundraWorldProxy.CreateEntityFromRecord(record!, _ => prefab, catalog, tileMapData: tileMapData);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.NotNull(proxy.Controller);
        return (entity, proxy);
    }

    /// <summary>
    /// THE fix's own end-to-end acceptance: the real gull entity 6 flight (measured facts) - anim 10 (real
    /// AnimSetsByAnim[10] Speed 128/Acceleration 6, resolved by the real spawn off the real
    /// Data/sprite-records.json header), direction 8 (OffsetXList[8]=-768, pure west), 0x1B [128,3]
    /// re-applied every logic tick, real Gravity flag from the real header (MoreFlags 0x80 | CanPickup
    /// 161&lt;&lt;8 = 0xA100 already carries bit 0x100). Expected per the original: ForceZ after gravity
    /// decay = 229376 - (128&lt;&lt;8) = 196608, exactly 3.0 px/tick (150 px/s) vertical; climb to TileZ 32
    /// takes 171 ticks; total westward drift 209.25 px. Pre-fix, the engine's own continuous
    /// Settings.Gravity integrated the vertical at render rate instead of the 50 Hz tick rate, producing
    /// ~158 px/s and only 179.25 px of drift. Runs the SAME scenario at dt=1/123 (the user's own real frame
    /// rate) AND dt=1/50 (frame-rate independence) - both must land on the same numbers within the
    /// documented +-1 tolerance.
    /// </summary>
    [Theory]
    [InlineData(1f / 123f)]
    [InlineData(1f / 50f)]
    public void GullEntitySix_RealFlightAtRenderRateAndAtFiftyHertz_ClimbsAndDriftsWithFaithfulTickQuantizedPhysics(float dt)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot)!;
        var spawn = SpawnRealGullEntitySix(projectRoot, tileMapData);
        if (spawn == null)
        {
            return;
        }

        var (entity, proxy) = spawn.Value;
        var world = BuildWorld(field);
        var host = new GullClimbScriptHost();
        proxy.ScriptHost = host;
        proxy.Status = EntityStatus.Normal; // skip slot A (Load) - not exercised by this fixture.
        proxy.TargetAnimationId = 10;
        proxy.TargetDirection = 8;

        world.AddEntity(entity);
        world.Update(1f / 50f); // register with CharacterMotionSystem/settle the real terrain ground clamp first.

        // Real header flags (MoreFlags 0x80 | CanPickup 161<<8 = 0xA100 | FlagsPortraitShadowType 3<<16) -
        // confirms this fixture is really carrying the Gravity bit, not a manually-forced one.
        Assert.Equal(EntityFlags.Gravity, proxy.Flags & EntityFlags.Gravity);

        var startTileZ = proxy.TileZ;
        var startX = entity.RootComponent!.Position.X;
        var ticksAtStart = host.TotalTicks;

        var ticksAtClimb = -1;
        for (var frame = 0; frame < 4000 && ticksAtClimb < 0; frame++)
        {
            world.Update(dt);

            if (proxy.TileZ >= startTileZ + 32)
            {
                ticksAtClimb = host.TotalTicks;
            }
        }

        Assert.True(ticksAtClimb >= 0, "expected TileZ to reach the 32-tile climb within this test's own frame budget.");

        var climbLogicTicks = ticksAtClimb - ticksAtStart;
        Assert.InRange(climbLogicTicks, 170, 172); // 171 +-1.

        var drift = startX - entity.RootComponent!.Position.X;
        Assert.InRange(drift, 208.25f, 210.25f); // 209.25 +-1.
    }

    // -----------------------------------------------------------------------------------------
    // Vertical fidelity fix, second acceptance leg: an NPC with the Gravity flag set and NO script
    // impulse falls tick-quantized (per-tick displacement clamped at the terminal ZViscosity-derived rate,
    // never engine-continuous), lands on the real field ground, and clearing the flag (0x17/0x63
    // semantics) freezes its altitude.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GravityFlagged_NoScriptImpulse_FallsTickQuantizedAndLandsOnRealGround_ClearingFlagFreezesAltitude()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadBank146ControllerSettings(projectRoot!);
        if (settings == null)
        {
            return;
        }

        var world = BuildWorld(field);
        // Cell (18,57): flat, sol 80px (same cell every other vertical test in this class uses) - spawn
        // 400px above it so the ramp-up-to-terminal AND the terminal-clamp phase both get exercised.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 480f), new FakeScriptHost());
        world.Update(1f / 50f);

        // Real map 389 raw Gravity/ZViscosity (see GravityToggle test's own doc above) - Gravity flag set,
        // no script impulse (ForceZ starts at 0, decayed by EvaluateEntitySupport alone).
        proxy.Flags = EntityFlags.Gravity;
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;
        Assert.Equal(0, proxy.ForceZ);

        var previousZ = entity.RootComponent!.Position.Z;
        var sawSubTerminalStep = false;
        var sawTerminalStep = false;
        for (var frame = 0; frame < 200 && proxy.IsOnGround == 0; frame++)
        {
            world.Update(1f / 50f);
            var z = entity.RootComponent!.Position.Z;
            var stepPx = previousZ - z; // positive while falling.

            // Terminal velocity = (MapZViscosityRaw << 8) / 65536 px/tick * 50 ticks/s = 800 px/s ->
            // 16 px/tick exactly (see GravityToggle test's own doc for the full formula). Tick-quantized
            // means every single tick's own displacement is a WHOLE-TICK step at some constant velocity
            // (ramping toward, then clamped at, that 16px value) - never a fraction blended in from a
            // higher-frequency continuous integrator (the pre-fix bug's own shape).
            if (stepPx > 0f && stepPx < 15.9f)
            {
                sawSubTerminalStep = true;
            }

            if (Math.Abs(stepPx - 16f) < 0.05f)
            {
                sawTerminalStep = true;
            }

            Assert.True(stepPx <= 16.01f, $"one tick's own displacement {stepPx}px exceeded the terminal 16px/tick clamp.");
            previousZ = z;
        }

        Assert.Equal(1, proxy.IsOnGround);
        Assert.Equal(80f, entity.RootComponent!.Position.Z); // real cell (18,57)'s own real ground height.
        Assert.True(sawSubTerminalStep, "expected the early ramp-up ticks to be strictly below the terminal 16px/tick step.");
        Assert.True(sawTerminalStep, "expected the fall to actually reach the terminal 16px/tick clamp before landing (400px fall).");

        // 0x17/0x63 semantics: clearing Gravity freezes altitude going forward. This lands on the REAL
        // terrain ground-snap (no other collidable entity in this fixture, so EntitySupport's own "found"
        // branch never fires) - the controller's own IsGrounded clamp (ApplyVerticalVelocity) is what
        // actually keeps Z pinned at 80 regardless of whatever this class' own decay still computes; the
        // flag's real effect is that ForceZ itself stops decaying, matching the original's own "keeps
        // whatever ForceZ it has, unaffected by gravity" rule once cleared.
        proxy.Flags &= ~EntityFlags.Gravity;
        var forceZAtClear = proxy.ForceZ;

        var landedZ = entity.RootComponent!.Position.Z;
        for (var frame = 0; frame < 30; frame++)
        {
            world.Update(1f / 50f);
            Assert.Equal(landedZ, entity.RootComponent!.Position.Z);
        }

        Assert.Equal(forceZAtClear, proxy.ForceZ); // decay genuinely stopped, not merely masked by the ground clamp.
    }
}
