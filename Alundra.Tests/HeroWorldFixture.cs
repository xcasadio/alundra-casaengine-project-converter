#nullable enable
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Shared headless hero-pawn/world montage, extracted from <see cref="AlundraCharacterControllerAdoptionTests"/>
/// (its own former <c>BuildWorld</c>/<c>BuildHeroPawn</c>, docs/plan-oracle-heros.md §2.1) so
/// <see cref="HeroTraceHarnessTests"/> can reuse the EXACT same real-<see cref="World"/>/real-
/// <see cref="PhysicsWorld"/>/real-<see cref="CharacterControllerComponent"/> montage without duplicating
/// it - both callers build a real headless <see cref="World"/> (TopDownElevation policy) with a real map
/// 389 <see cref="AlundraCellsCollisionField"/> installed as <see cref="World.CollisionField"/>, and a
/// hand-built hero pawn (root <c>TransformComponent</c> -&gt; <c>RenderProjectionComponent</c> -&gt;
/// <c>AnimatedSpriteComponent</c>, sibling <c>CollisionComponent</c> Box 21x15x32 local_position
/// (0.5,0.5,16), and a <see cref="CharacterControllerComponent"/> whose <see cref="CharacterControllerSettings"/>
/// come from the real converter export's own <c>Alundra.entity</c> "settings" node).
/// </summary>
internal static class HeroWorldFixture
{
    /// <summary>Same headless montage as <see cref="AlundraEntityLogicalRenderPoseTests"/>'s own
    /// BuildScene: a real <see cref="World"/> under <see cref="TopDownElevationSimulationSpacePolicy"/>
    /// with a <see cref="PhysicsWorld"/> wired (<see cref="CharacterControllerComponent"/> requires one,
    /// <c>TryResolveCollisionDependencies</c>), plus the real map 389 field installed as
    /// <see cref="World.CollisionField"/>.</summary>
    internal static World BuildWorld(AlundraCellsCollisionField field)
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

    internal static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>Hand-built hero pawn per the plan's own Acceptation 3 recipe (see class doc). Added to
    /// <paramref name="world"/> (queued - the caller's first <see cref="World.Update"/> registers it with
    /// CharacterMotionSystem, which always runs before this same frame's GameplayProxy.Update - see
    /// AlundraEntityScriptProxy.Update's own E3.d doc).</summary>
    internal static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildHeroPawn(
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
}
