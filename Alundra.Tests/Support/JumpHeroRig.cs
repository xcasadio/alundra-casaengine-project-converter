#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.d2c1 (docs/plan-e19-opcodes.md §1.2h.3.1, C3/C4): the hero montage of the plan (annex A.1): a real <see cref="World"/> on a field of
/// synthetic cells (<see cref="CellsField"/>, height 0 unless a test gives its own), a hero pawn with the REAL exported controller settings
/// (<see cref="HeroWorldFixture.BuildHeroPawn"/>, <c>MapGravity</c> 1250, <c>MapMaxFallSpeed</c> 800, <c>MapGravityRaw</c> 128,
/// <c>MapZViscosityRaw</c> 4096 as <c>AdoptPlayerPawn</c> posts them), <see cref="EntityFlags.Gravity"/> set (BuildHeroPawn leaves the flags at 0 and
/// <c>MovePlayer</c> returns before posting the bit under <c>ControlLocked</c>), and a host that exposes a <see cref="HeroController"/> (without
/// one the tick of the hero does not run). The pad is locked (<c>ControlLocked</c>): the animation and the direction are the test's to write.
/// </summary>
internal sealed class JumpHeroRig
{
    public const int HeroOffsetX = -10;
    public const int HeroOffsetY = -7;
    public const int HeroSizeX = 21;
    public const int HeroSizeY = 15;
    public const float MapGravity = 1250f;
    public const float MapMaxFallSpeed = 800f;

    public required World World { get; init; }

    public required ContactHost Host { get; init; }

    public required EntityScriptProxy Hero { get; init; }

    public required Entity HeroEntity { get; init; }

    public CharacterControllerComponent Controller => Hero.Controller!;

    /// <summary>The pad of the montage (E19.d2c2: only a montage built with <c>freePad</c> reads it; otherwise the pad is locked and empty).</summary>
    public HeroPad Pad { get; init; } = new();

    /// <summary>One update with the buttons <paramref name="hold"/> held and <paramref name="pressed"/> pressed this frame (E19.d2c2).</summary>
    public void Step(uint hold = 0, uint pressed = 0, float elapsed = 0.02f)
    {
        Pad.Hold = hold;
        Pad.Pressed = pressed;
        Update(elapsed);
    }

    /// <summary>The root height of the hero (pixels).</summary>
    public float RootZ => HeroEntity.RootComponent!.LocalTransform.Position.Z;

    /// <summary>One update of the world: the rebuild of the collidable list, the update, the rebuild at the end of the frame.</summary>
    public void Update(float elapsed = 0.02f)
    {
        Host.Rebuild();
        Host.BeginPhysicsPass();
        World.Update(elapsed);
        Host.EndPhysicsPass();
        Host.Rebuild();
    }

    /// <summary>Replaces (or adds) the animation set <paramref name="entry"/> of the hero.</summary>
    public void SetAnimSet(AnimSetEntry entry)
    {
        var sets = new Dictionary<int, AnimSetEntry>(Hero.AnimSetsByAnim!) { [entry.Anim] = entry };
        Hero.AnimSetsByAnim = sets;
    }

    /// <summary>
    /// The montage, with one settling update at rest. <paramref name="field"/> null: a flat field of cells of height 0 (<see cref="FlatCells"/>);
    /// <paramref name="probeFactory"/> builds the movement obstacle probe from the host (null: none); <paramref name="configure"/> runs on the host
    /// and the world before the hero is added (the other entities of the montage); the hero is at the root (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public static JumpHeroRig Build(
        ICollisionField? field = null,
        Func<ContactHost, IMovementObstacleProbe?>? probeFactory = null,
        Action<World, ContactHost>? configure = null,
        float x = 200.25f,
        float y = 100.5f,
        float z = 0f,
        float groundSnapDistance = -1f,
        bool gravityFlag = true,
        bool freePad = false,
        bool attackClock = false,
        Func<uint, HeroController, ContactHost>? hostFactory = null)
    {
        var pad = new HeroPad();
        var controller = new HeroController { PadStateProviderForTests = () => freePad ? pad.State : default };
        var controlFlags = freePad ? 0 : GameState.PlayerControlBits.ControlLocked;
        var host = hostFactory?.Invoke(controlFlags, controller) ?? new ContactHost(playerControlFlags: controlFlags, playerController: controller);
        var world = ContactWorld.BuildWorld(field ?? FlatCells.Create(), probeFactory?.Invoke(host));
        configure?.Invoke(world, host);

        var settings = HeroWorldFixture.LoadHeroControllerSettings(ProjectRootFinder.Find());
        settings.Gravity = MapGravity;
        settings.MaxFallSpeed = MapMaxFallSpeed;
        if (groundSnapDistance >= 0f)
        {
            settings.GroundSnapDistance = groundSnapDistance;
        }

        var (entity, hero) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(x, y, z), host);
        EntitySpawnFactory.SetEntityDimensions(hero, HeroOffsetX, HeroOffsetY, 0, HeroSizeX, HeroSizeY, 32);
        hero.MapGravity = MapGravity;
        hero.MapMaxFallSpeed = MapMaxFallSpeed;
        hero.MapGravityRaw = 128;
        hero.MapZViscosityRaw = 4096;
        hero.Flags |= EntityFlags.Collidable; // the real hero is collidable (without it the probe of obstacles ignores it).
        if (gravityFlag)
        {
            hero.Flags |= EntityFlags.Gravity;
        }

        hero.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 },
            [2] = new AnimSetEntry { Anim = 2, Speed = 208, Acceleration = 1, IsZForceApplied = 1280, Sfx = 10 },
            [43] = new AnimSetEntry { Anim = 43, Speed = 0, Acceleration = 1, IsZForceApplied = 1280, Sfx = 10 },
            [44] = new AnimSetEntry { Anim = 44, Speed = 196, Acceleration = 1 },
            [45] = new AnimSetEntry { Anim = 45, Speed = 0, Acceleration = 1 },
        };
        hero.CurrentAnimationId = 0;
        hero.TargetAnimationId = 0;
        if (attackClock)
        {
            HeroAttackClock.Install(entity, hero);
        }

        host.All.Insert(0, hero);

        var rig = new JumpHeroRig { World = world, Host = host, Hero = hero, HeroEntity = entity, Pad = pad };
        rig.Update(); // the settling update.
        return rig;
    }
}
