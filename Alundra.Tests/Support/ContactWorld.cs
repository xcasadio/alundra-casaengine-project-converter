#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Audio;
using Alundra.Hero;

namespace Alundra.Tests.Support;

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
    public static EntityScriptProxy AddEntity(
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

        var entity = new Entity { Name = name, RootComponent = root, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        if (withController)
        {
            AddController(entity);
        }

        entity.Initialize();

        var proxy = Assert.IsType<EntityScriptProxy>(entity.GameplayProxy);
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

        EntitySpawnFactory.SetEntityDimensions(proxy, offsetX, offsetY, offsetZ, sizeX, sizeY, sizeZ);
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
    public static Vector3 Root(EntityScriptProxy proxy) => proxy.OwnerEntity!.RootComponent!.LocalTransform.Position;

    /// <summary>One update of the world (integrates the entities added so far) then the end-of-frame rebuild of the collidable list.</summary>
    public static void Integrate(World world, ContactHost host)
    {
        host.Rebuild();
        host.BeginPhysicsPass();
        world.Update(0.02f);
        host.Rebuild();
    }
}
