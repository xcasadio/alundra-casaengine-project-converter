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
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d D2 (docs/plan-e19-opcodes.md §1.2g): the landing defect of <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>. An
/// entity WITHOUT gravity that rests on the ground keeps its negative <c>ForceZ</c> (the binary's <c>ComputeZPosition</c> zeroes it only
/// with gravity), and the landing branch used to take it for "not landed yet": every tick it pushed its position to the root, which
/// truncates X and Y to whole pixels, so a walk of less than one pixel per tick never advanced (the camera block of map 391).
/// <para>
/// The walk is 0.5 px per tick south (animation of speed 64, acceleration 0, direction 0 - the animation 1 of the block of map 391), on a
/// flat field at height 0, under a real <see cref="World"/> (one <c>World.Update(0.02)</c> is one logic tick). PosY is read in the
/// script host's <c>LogicTicksThisFrame</c>, which runs right AFTER the head-of-frame pull of the root into the logical position: it
/// is the value the next tick starts from, the one the defect erased.
/// </para>
/// </summary>
public sealed class AlundraNoGravityLandingTests
{
    private const int WalkAnimation = 5;

    /// <summary>A field whose ground is 0 everywhere, or drops to <see cref="DropTo"/> east of <see cref="LedgeX"/>.</summary>
    private sealed class LedgeField : ICollisionField
    {
        public float LedgeX { get; set; } = float.MaxValue;

        public float DropTo { get; set; } = -1000f;

        public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
        {
            sample = new GroundSample(worldPosition.X > LedgeX ? DropTo : 0f, Vector3.UnitZ, true, "ledge");
            return true;
        }
    }

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>A script host with a real logic clock (one tick per 0.02 s update) and a hook at the head of each entity update.</summary>
    private sealed class Host : IAlundraScriptHost
    {
        private readonly AlundraLogicClock _clock = new();

        public IEventProgramRunner Runner { get; } = new NoOpRunner();

        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }

        public AlundraGameState GameState { get; } = new();

        public AlundraPlayerController? PlayerController => null;

        public List<AlundraEntityScriptProxy> Collidables { get; } = new();

        IReadOnlyList<AlundraEntityScriptProxy> IAlundraScriptHost.Collidables => Collidables;

        /// <summary>Called after the head-of-frame pull of the root, before the tick (see the class doc).</summary>
        public Action? AfterPull { get; set; }

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime)
        {
            AfterPull?.Invoke();
            var ticks = _clock.TicksThisFrame(elapsedTime);
            _clock.CloseFrame();
            return ticks;
        }
    }

    private sealed class Fixture
    {
        public required World World { get; init; }

        public required AlundraEntityScriptProxy Proxy { get; init; }

        public required Host Host { get; init; }

        public void Tick() => World.Update(0.02f);
    }

    private static World BuildWorld(ICollisionField field)
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
        return world;
    }

    /// <summary>A scripted NPC with a controller, at (200, 100, 0), walking in the direction <paramref name="direction"/> at 0.5 px per
    /// tick on the animation <see cref="WalkAnimation"/>, already the current one (the animation lag of D-E19-13 would otherwise cost the
    /// first tick).</summary>
    private static Fixture BuildWalker(ICollisionField field, bool gravity, uint direction, int forceZ)
    {
        var world = BuildWorld(field);
        var host = new Host();

        var root = new TransformComponent();
        root.LocalTransform.Position = new Vector3(200f, 100f, 0f);
        var collision = new CollisionComponent();
        collision.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collision);

        var entity = new Entity { Name = "LandingWalker", RootComponent = root, GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
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
        entity.Initialize();

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
        proxy.IsPlayer = false;
        proxy.ScriptHost = host;
        proxy.Status = EntityStatus.Normal;
        proxy.PosX = 200 << 16;
        proxy.PosY = 100 << 16;
        proxy.PosZ = 0;
        proxy.TargetDirection = direction;
        proxy.CurrentAnimationId = WalkAnimation;
        proxy.TargetAnimationId = WalkAnimation;
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [WalkAnimation] = new AnimSetEntry { Anim = WalkAnimation, Speed = 64, Acceleration = 0 } };
        proxy.ForceZ = forceZ;
        if (gravity)
        {
            proxy.Flags |= EntityFlags.Gravity;
        }
        else
        {
            proxy.Flags &= ~EntityFlags.Gravity;
        }

        proxy.ResyncControllerFromFlags();
        world.AddEntity(entity);
        return new Fixture { World = world, Proxy = proxy, Host = host };
    }

    [Fact]
    public void ANoGravityEntityResting_WithANegativeForceZ_WalksItsHalfPixelsWithoutLosingThemToTheRootTruncation()
    {
        var f = BuildWalker(new LedgeField(), gravity: false, direction: 0, forceZ: -65536);
        var startY = 100 << 16;
        var observed = new List<int>();
        f.Host.AfterPull = () => observed.Add(f.Proxy.PosY);

        for (var tick = 0; tick < 64; tick++)
        {
            f.Tick();
        }

        f.Tick(); // the 65th update: its head-of-frame pull is the reading point, after the 64 ticks.
        Assert.Equal(startY + 2097152, observed[^1]);
        Assert.Equal(0, f.Proxy.PosZ);
        Assert.Equal(-65536, f.Proxy.ForceZ);
    }

    [Fact]
    public void AGravityEntity_LandsLikeBefore_ForceZBackToZero_AndIsNotPushedOnceLanded()
    {
        // Twin of the main test: with gravity the landing zeroes ForceZ, and a landed entity is not pushed to the root again (its
        // half pixels accumulate), as before the fix.
        var f = BuildWalker(new LedgeField(), gravity: true, direction: 0, forceZ: 0);
        f.Proxy.MapGravityRaw = 256; // 1 px per tick per tick (16.16 << 8 per tick).
        f.Proxy.MapZViscosityRaw = 1024;
        f.Proxy.PosZ = 3 << 16;
        f.Proxy.Controller!.Teleport(new Vector3(200f, 100f, 3f));
        f.Proxy.PushLogicalPositionToRoot();

        var observedY = new List<int>();
        f.Host.AfterPull = () => observedY.Add(f.Proxy.PosY);
        var landedAfter = -1;
        for (var tick = 0; tick < 60 && landedAfter < 0; tick++)
        {
            f.Tick();
            if (f.Proxy.PosZ == 0 && f.Proxy.ForceZ == 0)
            {
                landedAfter = tick + 1;
            }
        }

        Assert.True(landedAfter > 0, $"the entity never landed: PosZ {f.Proxy.PosZ}, ForceZ {f.Proxy.ForceZ}");
        Assert.Equal(0, f.Proxy.ForceZ);

        observedY.Clear();
        for (var tick = 0; tick < 21; tick++)
        {
            f.Tick();
        }

        Assert.Equal(0, f.Proxy.PosZ);
        Assert.Equal(0, f.Proxy.ForceZ);
        for (var i = 1; i < observedY.Count; i++)
        {
            Assert.True(Math.Abs(observedY[i] - observedY[i - 1] - 32768) <= 2, $"tick {i}: PosY moved by {observedY[i] - observedY[i - 1]} instead of 32768");
        }
    }

    [Fact]
    public void ANoGravityEntityWalkingOffALedge_StillDescendsOnePixelPerTick()
    {
        // Twin: the fix does not hold a floating entity: without terrain under it, ForceZ -1 px per tick moves it down 1 px per tick.
        var f = BuildWalker(new LedgeField { LedgeX = 220f }, gravity: false, direction: 24, forceZ: -65536);
        var observed = new List<(int X, int Z)>();
        f.Host.AfterPull = () => observed.Add((f.Proxy.PosX, f.Proxy.PosZ));

        for (var tick = 0; tick < 80; tick++)
        {
            f.Tick();
        }

        // Once it has left the ledge (PosZ below the ground), every tick lowers PosZ by exactly 1 px.
        var past = observed.FindIndex(o => o.Z < 0);
        Assert.True(past >= 0 && past + 10 < observed.Count, $"the entity never left the ledge: x {observed[^1].X >> 16}");
        for (var i = past + 1; i < past + 10; i++)
        {
            Assert.True(observed[i].Z - observed[i - 1].Z == -65536, $"sample {i}: PosZ {observed[i - 1].Z} -> {observed[i].Z}");
        }
    }
}
