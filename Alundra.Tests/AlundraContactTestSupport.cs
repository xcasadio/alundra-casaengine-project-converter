#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2b (docs/plan-e19-opcodes.md §1.2h.2): shared support of the entity contact tests - a flat ground field, a script host that
/// rebuilds its collidable list the way the world does at the end of each frame, and a builder of real worlds holding real
/// controller-driven entities (the logical position fields written by hand, the controller and the root those of production).
/// </summary>
internal sealed class FlatGroundField : ICollisionField
{
    public float GroundZ { get; init; }

    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
    {
        sample = new GroundSample(GroundZ, Vector3.UnitZ, true, "flat");
        return true;
    }
}

/// <summary>
/// A script host with a real logic clock (one tick per 0.02 s update), a no-op interpreter (or the one given), the destroy of the world
/// (the status goes to <see cref="EntityStatus.FlagToDestroy"/>) and a collidable list rebuilt by <see cref="Rebuild"/> from
/// <see cref="All"/> with <see cref="EntitySupport.BuildCollidables"/>, as <c>AlundraWorldProxy.RefreshUpdateProxiesAndCollidables</c>.
/// </summary>
internal sealed class ContactHost : IAlundraScriptHost
{
    private readonly AlundraLogicClock _clock = new();

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    public ContactHost(IEventProgramRunner? runner = null, uint playerControlFlags = 0, AlundraPlayerController? playerController = null)
    {
        Runner = runner ?? new NoOpRunner();
        GameState = new AlundraGameState { PlayerControlFlags = playerControlFlags };
        PlayerController = playerController;
    }

    public IEventProgramRunner Runner { get; }

    public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }

    public AlundraGameState GameState { get; }

    public AlundraPlayerController? PlayerController { get; }

    /// <summary>E19.d2c1 R8: the sound player the host hands to the hero's take-off (null: none, as the default member of the interface).</summary>
    public IAlundraSoundPlayer? SoundPlayer { get; set; }

    /// <summary>Every proxy of the world, in world order (the hero first).</summary>
    public List<AlundraEntityScriptProxy> All { get; } = new();

    public List<AlundraEntityScriptProxy> Collidables { get; } = new();

    IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

    public List<(AlundraEntityScriptProxy Entity, int EffectId)> Destroyed { get; } = new();

    public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        Destroyed.Add((entity, effectId));
    }

    public int LogicTicksThisFrame(float elapsedTime)
    {
        var ticks = _clock.TicksThisFrame(elapsedTime);
        _clock.CloseFrame();
        return ticks;
    }

    /// <summary>Rebuilds <see cref="Collidables"/> (the end of the frame of the world).</summary>
    public void Rebuild() => EntitySupport.BuildCollidables(All, Collidables);
}

internal static class ContactWorld
{
    /// <summary>A real <see cref="World"/> with the physics of the headless tests, the field and the probe installed (either may be null).</summary>
    public static World BuildWorld(ICollisionField? field, IMovementObstacleProbe? probe)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(game, new GameComponentCollection());
        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(game, gameManager);
        typeof(World).GetProperty(nameof(World.Game), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(world, game);
        typeof(World).GetProperty(nameof(World.PhysicsWorld), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(world, new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy()));
        world.CollisionField = field;
        world.MovementObstacleProbe = probe;
        return world;
    }

    /// <summary>
    /// A collidable entity with a controller, at the logical position (<paramref name="xPixels"/>, <paramref name="yPixels"/>,
    /// <paramref name="zPixels"/>) - the proxy fields and the root written to the same whole pixels - with the box of the original
    /// (offset, size) given. Added to the world and to <paramref name="host"/> (not yet integrated: call <see cref="Integrate"/> once all
    /// the entities are there).
    /// </summary>
    public static AlundraEntityScriptProxy AddEntity(
        World world, ContactHost host, string name, int xPixels, int yPixels, int zPixels,
        int offsetX, int offsetY, int offsetZ, int sizeX, int sizeY, int sizeZ,
        bool isPlayer = false, bool collidable = true, EntityStatus status = EntityStatus.Normal, bool withController = true)
    {
        var root = new TransformComponent();
        root.LocalTransform.Position = new Vector3(xPixels, yPixels, zPixels);
        var collision = new CollisionComponent();
        collision.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic; // a kinematic ghost body, as the entities of the game: invisible to the rigid sweep.
        collision.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collision);

        var entity = new Entity { Name = name, RootComponent = root, GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        if (withController)
        {
            AddController(entity);
        }

        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        if (withController)
        {
            proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
        }

        proxy.IsPlayer = isPlayer;
        proxy.ScriptHost = host;
        proxy.Status = status;
        proxy.Flags &= ~EntityFlags.Gravity;
        if (collidable)
        {
            proxy.Flags |= EntityFlags.Collidable;
        }

        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, offsetX, offsetY, offsetZ, sizeX, sizeY, sizeZ);
        proxy.PosX = xPixels << 16;
        proxy.PosY = yPixels << 16;
        proxy.PosZ = zPixels << 16;
        proxy.ResyncControllerFromFlags();
        host.All.Add(proxy);
        world.AddEntity(entity);
        return proxy;
    }

    private static void AddController(Entity entity)
    {
        var settings = new CharacterControllerSettings
        {
            Radius = 6f,
            Height = 32f,
            SkinWidth = 0.5f,
            StepHeight = 3f,
            GroundSnapDistance = 4f,
            Gravity = 0f,
            MaxFallSpeed = 0f,
            WalkabilityMask = 0,
        };
        var controller = new CharacterControllerComponent { Settings = settings };
        controller.SetControlMode(CharacterControlMode.Script);
        controller.IsVerticalOwnedExternally = true;
        entity.AddComponent(controller);
    }

    /// <summary>The root position of the entity of <paramref name="proxy"/> (pixels).</summary>
    public static Vector3 Root(AlundraEntityScriptProxy proxy) => proxy.OwnerEntity!.RootComponent!.LocalTransform.Position;

    /// <summary>One update of the world (integrates the entities added so far) then the end-of-frame rebuild of the collidable list.</summary>
    public static void Integrate(World world, ContactHost host)
    {
        host.Rebuild();
        world.Update(0.02f);
        host.Rebuild();
    }
}
