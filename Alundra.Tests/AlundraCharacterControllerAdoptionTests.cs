#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
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
/// Covers E3.d "Branchement Alundra" Acceptation 3 (docs/plan-e3-collisions.md): a hand-built hero pawn
/// (root TransformComponent -&gt; RenderProjectionComponent -&gt; AnimatedSpriteComponent with no
/// animation asset, sibling CollisionComponent Box 21x15x32 local_position (0.5,0.5,16), and a
/// CharacterControllerComponent whose settings are loaded from the real converter export's own
/// Alundra.entity "settings" node) under a real headless <see cref="World"/> (TopDownElevation policy,
/// same montage as <see cref="AlundraEntityLogicalRenderPoseTests"/>'s own BuildScene) with the REAL map
/// 389 <see cref="AlundraCellsCollisionField"/> installed as <c>World.CollisionField</c> - same pattern as
/// <see cref="AlundraCellsCollisionFieldTests"/>/<see cref="SpriteRecordCatalogTests"/>. Self-skips every
/// [Fact] when <c>alundra-project/</c> is not present in this checkout (same convention as
/// <see cref="Map389LoadProgramsTests"/>).
/// </summary>
public class AlundraCharacterControllerAdoptionTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // -----------------------------------------------------------------------------------------
    // Fixture plumbing
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
    /// Reads the "settings" node of the CharacterControllerComponent the converter now writes into the
    /// real exported hero prefab (E3.d, SpriteWriter.WriteEntityPrefab) and loads it exactly the way
    /// CharacterControllerComponent.Load does (CharacterControllerSettings.Load), same raw-JSON pattern
    /// as SpriteRecordCatalogTests own file-backed reads.
    /// </summary>
    private static CharacterControllerSettings? LoadHeroControllerSettings(string projectRoot)
    {
        var heroEntityPath = Path.Combine(projectRoot, "Entities", "Alundra", "Alundra.entity");
        if (!File.Exists(heroEntityPath))
        {
            return null;
        }

        var document = JObject.Parse(File.ReadAllText(heroEntityPath));
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

    /// <summary>Same headless montage as AlundraEntityLogicalRenderPoseTests.BuildScene: a real
    /// <see cref="World"/> under <see cref="TopDownElevationSimulationSpacePolicy"/> with a
    /// <see cref="PhysicsWorld"/> wired (CharacterControllerComponent requires one,
    /// TryResolveCollisionDependencies), plus the real map 389 field installed as
    /// <see cref="World.CollisionField"/>.</summary>
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

    /// <summary>Hand-built hero pawn per the plan own Acceptation 3 recipe (see class doc). Added to
    /// <paramref name="world"/> (queued - the caller first <see cref="World.Update"/> registers it with
    /// CharacterMotionSystem, which always runs before this same frame GameplayProxy.Update - see
    /// AlundraEntityScriptProxy.Update own E3.d doc).</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildHeroPawn(
        World world, CharacterControllerSettings settings, Vector3 startPosition, IAlundraScriptHost scriptHost)
    {
        var root = new TransformComponent();
        root.LocalTransform.Position = startPosition;

        var projection = new RenderProjectionComponent();
        root.AddChildComponent(projection);
        var sprite = new AnimatedSpriteComponent();
        projection.AddChildComponent(sprite);

        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(21f, 15f, 32f) },
            LocalPosition = new Vector3(0.5f, 0.5f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collisionComponent);

        var entity = new Entity
        {
            Name = "HeroTestPawn",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };

        var controllerComponent = new CharacterControllerComponent { Settings = settings };
        entity.AddComponent(controllerComponent);

        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
        Assert.NotNull(proxy.Controller);
        proxy.IsPlayer = true;
        proxy.ScriptHost = scriptHost;
        proxy.PosX = (int)Math.Round((double)startPosition.X * 65536.0);
        proxy.PosY = (int)Math.Round((double)startPosition.Y * 65536.0);
        proxy.PosZ = (int)Math.Round((double)startPosition.Z * 65536.0);
        proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();
        proxy.RenderProjection?.UpdateProjection();

        world.AddEntity(entity);
        return (entity, proxy);
    }

    /// <summary>No-op <see cref="IEventProgramRunner"/> - the pick/run half never fires here
    /// (<c>IsPlayer</c> excludes it, same as the real hero).</summary>
    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>Same shape as AlundraEntityLogicalRenderPoseTests own FakeScriptHost -
    /// <see cref="PlayerController"/> null means <see cref="AlundraEntityScriptProxy.Update"/> own
    /// player branch (MovePlayer/Tick) never runs, so a direct <see cref="CharacterControllerComponent.Move"/>
    /// call from the test is the only source of horizontal motion, matching how E3.c own acceptance
    /// tests drive the mover ("Move(...) puis exactement un Update(1/50)").</summary>
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

    // -----------------------------------------------------------------------------------------
    // Settings loaded from the real export (StepHeight/Radius come from the file).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void HeroSettings_LoadedFromRealExport_StepHeightAndRadiusComeFromTheFile()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var settings = LoadHeroControllerSettings(projectRoot);
        Assert.NotNull(settings);
        Assert.Equal(3f, settings!.StepHeight);
        Assert.Equal(7.5f, settings.Radius);
        Assert.Equal(32f, settings.Height);
        Assert.Equal(0.5f, settings.SkinWidth);
        Assert.Equal(4f, settings.GroundSnapDistance);
        // Exported as 0 - runtime-overridden by AlundraWorldProxy.AdoptPlayerPawn (see next test).
        Assert.Equal(0f, settings.Gravity);
        Assert.Equal(0f, settings.MaxFallSpeed);
        Assert.Equal(0u, settings.WalkabilityMask);
    }

    // -----------------------------------------------------------------------------------------
    // Overrides (headless equivalent of AlundraWorldProxy.AdoptPlayerPawn own override block -
    // AdoptPlayerPawn itself needs a live World.PlayerControllers/AlundraPlayerController possessing a
    // pawn, which this headless harness cannot construct any more than AlundraPlayerControllerTests can
    // build a live BuildPadState - see that file own doc).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Overrides_RealMap389GravityAndZViscosity_ProduceTheDocumentedGravityAndMaxFallSpeed()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var tileMapData = LoadMap389TileMapData(projectRoot);
        Assert.NotNull(tileMapData);
        tileMapData!.CustomProperties.TryGetValue("Gravity", out var gravityRaw);
        int.TryParse(gravityRaw, out var mapGravity);
        tileMapData.CustomProperties.TryGetValue("ZViscosity", out var zViscosityRaw);
        int.TryParse(zViscosityRaw, out var mapZViscosity);
        Assert.Equal(128, mapGravity);
        Assert.Equal(4096, mapZViscosity);

        var settings = LoadHeroControllerSettings(projectRoot)!;
        var flags = EntityFlags.ClassB; // nonzero, exercises WalkabilityMaskFor's real folding.

        // Same formula as AlundraWorldProxy.AdoptPlayerPawn own override block, applied AFTER Flags -
        // docs/plan-e3-collisions.md "DLL - adoption".
        settings.Gravity = mapGravity * 256f / 65536f * 2500f;
        settings.MaxFallSpeed = mapZViscosity * 256f / 65536f * 50f;
        settings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(flags);

        Assert.NotEqual(0u, (uint)flags);
        Assert.Equal(1250f, settings.Gravity);
        Assert.Equal(800f, settings.MaxFallSpeed);
        Assert.Equal(AlundraCellsCollisionField.WalkabilityMaskFor(flags), settings.WalkabilityMask);
        Assert.NotEqual(0u, settings.WalkabilityMask);
    }

    // -----------------------------------------------------------------------------------------
    // Fall from +100.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Fall_FromOneHundredPixelsAboveGround_LandsAtCellHeightAndBecomesGrounded()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        settings.Gravity = 1250f;
        settings.MaxFallSpeed = 800f;

        var world = BuildWorld(field);
        // Cell (18,57): flat, height 5 -> ground 80px. Spawn 100px above it.
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(444f, 920f, 180f), new FakeScriptHost());

        for (var i = 0; i < 100 && entity.RootComponent!.Position.Z > 80f; i++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(80f, entity.RootComponent!.Position.Z);
        Assert.Equal(1, proxy.IsOnGround);
    }

    // -----------------------------------------------------------------------------------------
    // Pose au sol / clamp scripte.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ClampScripted_FlatCellCentre_SnapsToEightyAndStaysStableOverTenUpdates()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, Vector3.Zero, new FakeScriptHost());
        world.Update(1f / 50f); // register with CharacterMotionSystem before the scripted write.

        // 0x64-style scripted write: (444, 920, 0), centre of (18,57) - sol 80.
        proxy.PosX = 444 * 65536;
        proxy.PosY = 920 * 65536;
        proxy.PosZ = 0;
        proxy.PushLogicalPositionToRoot();

        Assert.Equal(80, proxy.PosZ >> 16); // ClampToGround already applied synchronously.

        for (var i = 0; i < 10; i++)
        {
            world.Update(1f / 50f);
            Assert.Equal(80f, entity.RootComponent!.Position.Z);
            Assert.Equal(1, proxy.IsOnGround);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Clamp a cheval.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ClampScripted_StraddlingTwoRows_SnapsToTheHigherOneHundredTwelve()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, Vector3.Zero, new FakeScriptHost());
        world.Update(1f / 50f);

        // 0x64-style scripted write: (444, 924, 0) - footprint straddles row 57 (sol 80) and row 58 (sol 112).
        proxy.PosX = 444 * 65536;
        proxy.PosY = 924 * 65536;
        proxy.PosZ = 0;
        proxy.PushLogicalPositionToRoot();

        Assert.Equal(112, proxy.PosZ >> 16);

        for (var i = 0; i < 10; i++)
        {
            world.Update(1f / 50f);
            Assert.Equal(112f, entity.RootComponent!.Position.Z);
            Assert.Equal(1, proxy.IsOnGround);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Masque seul (hauteurs egales).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Mask_ClassBMaskOnEqualHeightCells_BlocksTheMove()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        settings.WalkabilityMask = 0x41u; // base 0x40 | ClassB 0x01 - cell (24,39) carries walkability 1.
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Controller!.Move(new Vector3(24f, 0f, 0f));
        world.Update(1f / 50f);

        Assert.Equal(new Vector3(564f, 632f, 80f), entity.RootComponent!.Position);
    }

    [Fact]
    public void Mask_BaseMaskOnEqualHeightCells_AdvancesToTheTargetCell()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        settings.WalkabilityMask = 0x40u; // base only - cell (24,39) walkability bit 0 is not in this mask.
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(564f, 632f, 80f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Controller!.Move(new Vector3(24f, 0f, 0f));
        world.Update(1f / 50f);

        Assert.Equal(new Vector3(588f, 632f, 80f), entity.RootComponent!.Position);
    }

    // -----------------------------------------------------------------------------------------
    // Falaise (hauteur seule).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Cliff_HeightAboveStepHeight_BlocksTheMoveRegardlessOfMask()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        // (16,16): flat, height 0 -> sol 0. Target (17,16): flat, height 12 -> sol 192, way above StepHeight 3.
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(396f, 264f, 0f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Controller!.Move(new Vector3(24f, 0f, 0f));
        world.Update(1f / 50f);

        Assert.Equal(new Vector3(396f, 264f, 0f), entity.RootComponent!.Position);
    }

    // -----------------------------------------------------------------------------------------
    // Suivi d'escalier.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Stairs_SteppingDownTheSlope_FollowsEachStepInTurn()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        // (13,27): slope 5 (stairs), height 10 - top-corner sol at y=439 is 160.
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(324f, 439f, 160f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Controller!.Move(new Vector3(0f, 4f, 0f));
        world.Update(1f / 50f);
        Assert.Equal(new Vector3(324f, 443f, 156f), entity.RootComponent!.Position);
        Assert.Equal(1, proxy.IsOnGround);

        proxy.Controller!.Move(new Vector3(0f, 4f, 0f));
        world.Update(1f / 50f);
        Assert.Equal(new Vector3(324f, 447f, 152f), entity.RootComponent!.Position);
    }

    // -----------------------------------------------------------------------------------------
    // Propriete de la racine.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RootOwnership_OneFrameWithMove_RootAndPosAgreeOnTheMoverResolvedPosition()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        // Flat row 57, columns 23+ (height 0) - see class doc.
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(564f, 920f, 0f), new FakeScriptHost());
        world.Update(1f / 50f);

        proxy.Controller!.Move(new Vector3(5f, 0f, 0f));
        world.Update(1f / 50f); // SyncTransform must NOT revert the mover resolved root this same frame.

        var root = entity.RootComponent!.Position;
        Assert.Equal(569f, root.X);
        Assert.Equal(569, proxy.PosX >> 16);
        Assert.Equal((int)Math.Round((double)root.X * 65536.0), proxy.PosX);
        Assert.Equal((int)Math.Round((double)root.Y * 65536.0), proxy.PosY);
        Assert.Equal((int)Math.Round((double)root.Z * 65536.0), proxy.PosZ);
    }

    [Fact]
    public void RootOwnership_OneHundredFramesFlatWalk_PosDriftFromPureIntegrationStaysBoundedAndDoesNotGrowMonotonically()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(564f, 920f, 0f), new FakeScriptHost());
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

        // "sans croissance monotone": the drift sequence must not be a strictly increasing run all the
        // way to frame 100 - it fluctuates within the bound above (quantization), it does not accumulate.
        var strictlyIncreasingRun = true;
        for (var i = 1; i < diffs.Length && strictlyIncreasingRun; i++)
        {
            strictlyIncreasingRun = diffs[i] > diffs[i - 1];
        }

        Assert.False(strictlyIncreasingRun, "the Pos*-vs-pure-integration drift grew monotonically over 100 frames.");
    }

    // -----------------------------------------------------------------------------------------
    // Teleport scripte.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ScriptedTeleport_PushLogicalPositionToRoot_RootPosAndVelocityAreCoherentAndDoNotSnapBack()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(444f, 920f, 80f), new FakeScriptHost());
        world.Update(1f / 50f);

        // 0x64-style scripted write: (804, 872, 0).
        proxy.PosX = 804 * 65536;
        proxy.PosY = 872 * 65536;
        proxy.PosZ = 0;
        proxy.PushLogicalPositionToRoot();

        var rootAfterTeleport = entity.RootComponent!.Position;
        Assert.Equal(804f, rootAfterTeleport.X);
        Assert.Equal(872f, rootAfterTeleport.Y);
        Assert.Equal(804, proxy.PosX >> 16);
        Assert.Equal(872, proxy.PosY >> 16);
        Assert.Equal(Vector3.Zero, proxy.Controller!.Velocity);

        world.Update(1f / 50f);

        // The following frame does not snap the hero back to (444, 920, *).
        Assert.Equal(804f, entity.RootComponent!.Position.X);
        Assert.Equal(872f, entity.RootComponent!.Position.Y);
    }

    // -----------------------------------------------------------------------------------------
    // Sans Move.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void WithoutMove_WorldUpdateAlone_DoesNotMoveTheHeroHorizontally()
    {
        var projectRoot = FindProjectRoot();
        var field = projectRoot == null ? null : LoadMap389Field(projectRoot);
        if (field == null)
        {
            return;
        }

        var settings = LoadHeroControllerSettings(projectRoot!)!;
        var world = BuildWorld(field);
        var (entity, proxy) = BuildHeroPawn(world, settings, new Vector3(444f, 920f, 80f), new FakeScriptHost());

        for (var i = 0; i < 5; i++)
        {
            world.Update(1f / 50f);
        }

        Assert.Equal(444f, entity.RootComponent!.Position.X);
        Assert.Equal(920f, entity.RootComponent!.Position.Y);
    }
}
