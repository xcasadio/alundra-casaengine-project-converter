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

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
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

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
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
        // Cell (18,57): flat, sol 80px. Spawn well above it (400px) so MaxFallSpeed clamp is exercised for
        // several ticks before landing.
        var (entity, proxy) = BuildNpcPawn(world, settings, new Vector3(444f, 920f, 480f), new FakeScriptHost());
        world.Update(1f / 50f);

        // 0x17 semantics: Flags has no Gravity bit -> ApplyGravitySettingsToController zeroes both.
        proxy.MapGravity = 1250f;
        proxy.MapMaxFallSpeed = 800f;
        proxy.ApplyGravitySettingsToController();
        Assert.Equal(0u, proxy.Flags & EntityFlags.Gravity);
        Assert.Equal(0f, proxy.Controller!.Settings.Gravity);
        Assert.Equal(0f, proxy.Controller.Settings.MaxFallSpeed);

        for (var frame = 0; frame < 25; frame++)
        {
            world.Update(1f / 50f);
            Assert.Equal(480f, entity.RootComponent!.Position.Z);
            Assert.Equal(0, proxy.IsOnGround);
        }

        // 0x16 semantics: Flags |= Gravity -> real map values restored.
        proxy.Flags |= EntityFlags.Gravity;
        proxy.ApplyGravitySettingsToController();
        Assert.Equal(1250f, proxy.Controller.Settings.Gravity);
        Assert.Equal(800f, proxy.Controller.Settings.MaxFallSpeed);

        var maxDownwardSpeed = 0f;
        for (var i = 0; i < 200 && entity.RootComponent!.Position.Z > 80f; i++)
        {
            world.Update(1f / 50f);
            maxDownwardSpeed = Math.Max(maxDownwardSpeed, -proxy.Controller.Velocity.Z);
        }

        Assert.Equal(80f, entity.RootComponent!.Position.Z);
        Assert.Equal(1, proxy.IsOnGround);
        Assert.True(maxDownwardSpeed <= 800f, $"fall speed {maxDownwardSpeed} exceeded MaxFallSpeed 800.");
        Assert.True(maxDownwardSpeed > 700f, $"fall speed {maxDownwardSpeed} never got close to the 800 clamp over a 400px fall.");
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

        proxy.MapGravity = 1250f;
        proxy.MapMaxFallSpeed = 800f;
        proxy.ApplyGravitySettingsToController();
        proxy.Controller!.SetVerticalVelocity(-50f);

        for (var frame = 0; frame < 100 && proxy.IsOnGround == 0; frame++)
        {
            world.Update(1f / 50f);

            Assert.Equal(proxy.PosZ >> 20, proxy.TileZ);
        }

        Assert.Equal(1, proxy.IsOnGround);
        Assert.Equal(proxy.PosZ >> 20, proxy.TileZ);
        _ = entity;
    }
}
