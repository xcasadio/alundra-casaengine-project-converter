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
/// E19.d2c1 (docs/plan-e19-opcodes.md §1.2h.3.1): shared montages of the jump and landing tests. The NPC montage is the one of the
/// plan (annex A.1 and C.valeurs): a real <see cref="World"/> on a flat field of height 0, one controller-driven NPC (a box of 20 x 14 x 32
/// at (200, 100, 0), offsets -10 -7 0) with <see cref="EntityFlags.Gravity"/> set AFTER <see cref="ContactWorld.AddEntity"/> (which clears
/// it), <c>MapGravityRaw</c> 128, <c>MapZViscosityRaw</c> 4096, one entity per world (the host closes the clock memo at each call).
/// </summary>
internal sealed class JumpNpcRig
{
    public const int ImpulseAnimation = 3;

    public required World World { get; init; }

    public required ContactHost Host { get; init; }

    public required EntityScriptProxy Npc { get; init; }

    /// <summary>Replaces (or adds) the animation set of <paramref name="entry"/> of the NPC (the table is read-only on the proxy).</summary>
    public void SetAnimSet(AnimSetEntry entry)
    {
        var sets = new Dictionary<int, AnimSetEntry>(Npc.AnimSetsByAnim!) { [entry.Anim] = entry };
        Npc.AnimSetsByAnim = sets;
    }

    /// <summary>One update of the world (one logic tick).</summary>
    public void Update(float elapsed = 0.02f)
    {
        Host.Rebuild();
        World.Update(elapsed);
        Host.Rebuild();
    }

    /// <summary>One animation of the sprite variant of the montage: a Loop (or a Once with the end kind) of <paramref name="Seconds"/>.</summary>
    public readonly record struct SpriteAnim(uint Id, AnimationType Type, float Seconds, AnimationEndKind End = AnimationEndKind.Loop, int ChainTo = 0, int Impulse = 0);

    public static SpriteAnim Loop(uint id, float seconds, int impulse = 0) => new(id, AnimationType.Loop, seconds, Impulse: impulse);

    public static SpriteAnim Hold(uint id, float seconds, int impulse = 0) => new(id, AnimationType.Once, seconds, AnimationEndKind.Hold, Impulse: impulse);

    public static SpriteAnim Chain(uint id, float seconds, int to, int impulse = 0) => new(id, AnimationType.Once, seconds, AnimationEndKind.Chain, to, impulse);

    private static Animation2d MakeAnimation(SpriteAnim anim)
    {
        var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var data = new Animation2dData { Name = $"bank_anim{anim.Id}_down", AnimationType = anim.Type };
        data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
        var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(anim.Seconds, spriteId));
        data.Tracks.Add(track);
        return new Animation2d(data);
    }

    /// <summary>
    /// The montage of <see cref="Build"/> with an animated sprite on the logical clock of the entity (the montage of
    /// <c>AnimationClockDriveTests</c>, on the entity of the NPC): the animations <paramref name="anims"/> (each with the impulse it carries),
    /// the animation 0 current and target. The interpreter is <paramref name="runner"/> (null: no program), the tick program (C) enabled when it is given.
    /// One settling update at rest is run.
    /// </summary>
    public static JumpNpcRig BuildWithSprite(SpriteAnim[] anims, IEventProgramRunner? runner = null, bool gravity = true)
    {
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost(runner);

        var root = new TransformComponent();
        root.LocalTransform.Position = new Vector3(200f, 100f, 0f);
        var collision = new CollisionComponent();
        collision.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        collision.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(18f, 12f, 32f) },
            LocalPosition = new Vector3(0f, 0f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collision);
        var sprite = new AnimatedSpriteComponent();
        var endTable = new Dictionary<int, AnimationEndInfo>();
        var sets = new Dictionary<int, AnimSetEntry>();
        foreach (var anim in anims)
        {
            sprite.AddAnimation(MakeAnimation(anim));
            sets[(int)anim.Id] = new AnimSetEntry { Anim = (int)anim.Id, Speed = 0, IsZForceApplied = anim.Impulse };
            if (anim.End != AnimationEndKind.Loop)
            {
                endTable[(int)anim.Id * EntitySpawnFactory.IdsvDirectionStride] = new AnimationEndInfo { Kind = anim.End, ChainTargetAnimationId = anim.ChainTo };
            }
        }

        root.AddChildComponent(sprite);

        var entity = new Entity { Name = "SpriteJumper", RootComponent = root, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        var controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                Radius = 6f, Height = 32f, SkinWidth = 0.5f, StepHeight = 3f, GroundSnapDistance = 4f, Gravity = 0f, MaxFallSpeed = 0f, WalkabilityMask = 0,
            },
        };
        controller.SetControlMode(CharacterControlMode.Script);
        controller.IsVerticalOwnedExternally = true;
        entity.AddComponent(controller);
        entity.Initialize();

        var npc = Assert.IsType<EntityScriptProxy>(entity.GameplayProxy);
        npc.Controller = entity.GetComponent<CharacterControllerComponent>();
        npc.ScriptHost = host;
        npc.Status = EntityStatus.Normal;
        npc.Flags &= ~EntityFlags.Gravity;
        npc.Flags |= EntityFlags.Collidable;
        if (gravity)
        {
            npc.Flags |= EntityFlags.Gravity;
        }

        EntitySpawnFactory.SetEntityDimensions(npc, -10, -7, 0, 20, 14, 32);
        npc.PosX = 200 << 16;
        npc.PosY = 100 << 16;
        npc.PosZ = 0;
        npc.MapGravityRaw = 128;
        npc.MapZViscosityRaw = 4096;
        npc.AnimSetsByAnim = sets;
        npc.AnimationEndByAnimDirection = endTable.Count == 0 ? null : endTable;
        if (runner != null)
        {
            npc.ProgramIndexes[ScriptHelper.ProgramCTick] = 1;
        }

        npc.CurrentAnimationId = 0;
        npc.TargetAnimationId = 0;
        npc.ResyncControllerFromFlags();
        host.All.Add(npc);
        world.AddEntity(entity);
        EntitySpawnFactory.SubscribeAnimationEndBridge(entity);

        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };
        rig.Update(); // the settling update.
        return rig;
    }

    /// <summary>
    /// Builds the NPC montage and runs ONE settling update at rest (animation 0 current and target). <paramref name="impulse"/> is the
    /// <c>IsZForceApplied</c> of the animation <see cref="ImpulseAnimation"/> (animation 0 has speed 0 and no impulse); the animation
    /// sets are the caller's to extend through <see cref="EntityScriptProxy.AnimSetsByAnim"/>.
    /// </summary>
    public static JumpNpcRig Build(int impulse = 1360, bool gravity = true, int x = 200, int y = 100, int z = 0)
    {
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost();
        var npc = ContactWorld.AddEntity(world, host, "Jumper", x, y, z, -10, -7, 0, 20, 14, 32);
        if (gravity)
        {
            npc.Flags |= EntityFlags.Gravity;
        }

        npc.MapGravityRaw = 128;
        npc.MapZViscosityRaw = 4096;
        npc.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [ImpulseAnimation] = new AnimSetEntry { Anim = ImpulseAnimation, Speed = 0, IsZForceApplied = impulse },
        };
        npc.CurrentAnimationId = 0;
        npc.TargetAnimationId = 0;
        npc.ResyncControllerFromFlags();

        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };
        rig.Update(); // the settling update.
        return rig;
    }
}

/// <summary>E19.d2c2: a pad the test writes by hand (held buttons, buttons pressed THIS frame); the hero's controller reads it every frame.</summary>
internal sealed class HeroPad
{
    public uint Hold;

    public uint Pressed;

    public PadState State => new() { ButtonsHold = Hold, ButtonsJustPressed = Pressed };
}

/// <summary>
/// E20.b B5: the sprite clock and the animation-end bridge of the hero montage for the swings. The hero pawn of <see cref="HeroWorldFixture.BuildHeroPawn"/> has an
/// <see cref="AnimatedSpriteComponent"/> with no animation, so its logical clock never runs and no Chain ever fires. <see cref="Install"/> gives it one synthetic animation per
/// (animation, direction) of the swings and of the states around them, of the length of the binary in logic ticks (<c>round(DurationSeconds x 50)</c> is the tick of the end), an
/// <see cref="AnimationEndInfo"/> chain per (animation, direction) like the export's, the animation sets (speed, acceleration, sound) of the table of
/// <c>docs/plan-e20-annexe/e20-b/faits-binaire-dll.md</c> section 3, and the bridge of <see cref="EntitySpawnFactory.SubscribeAnimationEndBridge"/>. The lengths are the binary's
/// frame-delay sums, which the exported <c>.anim2d</c> files reproduce (checked offline for every id and direction of the table, 0 mismatches).
/// </summary>
internal static class HeroAttackClock
{
    /// <summary>One animation of the montage: the animation set (<paramref name="Speed"/>, <paramref name="Acceleration"/>, <paramref name="Sfx"/>), the chain target (-1: none, the animation loops or
    /// holds) and the length in logic ticks, <paramref name="Ticks"/> for the directions down, up and left and <paramref name="TicksRight"/> for the right one. E20.d1 T3: <paramref name="Impulse"/> is the Z impulse of the
    /// animation (0: the one of the base sets, if any) and <paramref name="Hold"/> an animation that ends by holding its last frame (a Once animation with a Hold end, which raises the hold flag <c>+0xAC</c>).</summary>
    public readonly record struct Entry(int Id, int Speed, int Acceleration, int Sfx, int ChainTo, int Ticks, int TicksRight = 0, int Impulse = 0, bool Hold = false);

    /// <summary>The swings of the weapon table (ids and values of <c>faits-binaire-dll.md</c> section 3) and the states around them.</summary>
    public static readonly Entry[] Table =
    {
        new(0x00, 0, 0, 0, -1, 50), new(0x01, 208, 1, 0, -1, 50), new(0x02, 208, 1, 10, -1, 50), new(0x2B, 0, 1, 10, -1, 50), new(0x2C, 196, 1, 0, -1, 50), new(0x2D, 0, 1, 0, -1, 50),
        new(0x10, 0, 1, 29, 0, 21), new(0x11, 0, 1, 37, 0, 31), new(0x12, 0, 1, 35, 0, 31), new(0x13, 0, 1, 32, 0, 23),
        new(0x14, 0, 15, 29, 0x2D, 10), new(0x15, 0, 15, 37, 0x2D, 25, 24), new(0x16, 0, 15, 35, 0x2D, 28, 27), new(0x17, 0, 15, 32, 0x2D, 15, 14),
        new(0x18, 0, 1, 0, 0, 46), new(0x19, 0, 1, 36, 0, 58),
        new(0x3F, 0, 1, 29, 0, 21), new(0x44, 0, 1, 29, 0, 21), new(0x49, 0, 1, 29, 0, 21), new(0x40, 0, 1, 35, 0, 34),
        new(0x41, 0, 15, 29, 0x2D, 10), new(0x46, 0, 15, 29, 0x2D, 10), new(0x4B, 0, 15, 29, 0x2D, 10), new(0x42, 0, 15, 35, 0x2D, 31),
        // E20.f2: the four rebound states of the flails (sound 28, speed and acceleration 0; the ticks of the exported animations, the same in the four directions): 0x08 and 0x25 on the ground (16 ticks, then Idle),
        // 0x1F and 0x26 in the air (11 ticks, then 0x2D).
        new(0x08, 0, 0, 28, 0, 16), new(0x25, 0, 0, 28, 0, 16), new(0x1F, 0, 0, 28, 0x2D, 11), new(0x26, 0, 0, 28, 0x2D, 11),
        // E20.d1 T3: the five states of the run (the hero record of the export, `sprite-records.json`, and the exported animations, the same length in the four directions): the preparation 0x29 (24 ticks, loops),
        // the run 0x03 (speed 336, sound 13, 30 ticks, loops), the slide 0x2A (sound 14, 16 ticks, then Idle), the tackle 0x04 (sound 83, 32 ticks, then Idle) and the bounce 0x3E (speed 256, byte 64, sound 28, impulse 1024,
        // 10 ticks, holds its last frame).
        new(0x29, 0, 0, 0, -1, 24), new(0x03, 336, 2, 13, -1, 30), new(0x2A, 0, 5, 14, 0, 16), new(0x04, 0, 6, 83, 0, 32), new(0x3E, 256, 64, 28, -1, 10, Impulse: 1024, Hold: true),
        // E20.i T3: the five states of the swim (the hero record of the export, `swim-dll.md` section 3; the same length in the four directions): floating 0x1D (36 ticks, loops), swimming 0x0F (speed 144, shift 4, sound 22,
        // 42 ticks, chained to itself), the burst 0x28 (speed 176, sound 21, 42 ticks, then 0x1D), the hurt 0x3A (speed 256, byte 64, sound 23, 4 ticks, then 0x3B) and its end 0x3B (byte 68, 8 ticks, holds its last frame).
        new(0x1D, 0, 3, 0, -1, 36), new(0x0F, 144, 4, 22, 0x0F, 42), new(0x28, 176, 0, 21, 0x1D, 42), new(0x3A, 256, 64, 23, 0x3B, 4), new(0x3B, 0, 68, 0, -1, 8, Hold: true),
    };

    private static Animation2d Make(int id, string direction, float seconds, AnimationType type)
    {
        var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var data = new Animation2dData { Name = $"bank_anim{id}_{direction}", AnimationType = type };
        data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
        var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(seconds, spriteId));
        data.Tracks.Add(track);
        return new Animation2d(data);
    }

    /// <summary>Gives <paramref name="hero"/> the animations, the end table, the animation sets and the bridge of <see cref="Table"/>, and settles it facing east with a stable first switch.</summary>
    public static void Install(Entity entity, EntityScriptProxy hero)
    {
        var sprite = entity.GetComponent<AnimatedSpriteComponent>()!;
        var sets = new Dictionary<int, AnimSetEntry>(hero.AnimSetsByAnim!);
        var endTable = new Dictionary<int, AnimationEndInfo>();
        foreach (var entry in Table)
        {
            sets[entry.Id] = new AnimSetEntry { Anim = entry.Id, Speed = entry.Speed, Acceleration = entry.Acceleration, Sfx = entry.Sfx, IsZForceApplied = entry.Impulse != 0 ? entry.Impulse : sets.TryGetValue(entry.Id, out var existing) ? existing.IsZForceApplied : 0 };
            for (var direction = 0; direction < AnimationTables.DirectionNames.Length; direction++)
            {
                var ticks = direction == 3 && entry.TicksRight != 0 ? entry.TicksRight : entry.Ticks;
                var once = entry.ChainTo >= 0 || entry.Hold;
                sprite.AddAnimation(Make(entry.Id, AnimationTables.DirectionNames[direction], ticks * 0.02f, once ? AnimationType.Once : AnimationType.Loop));
                if (entry.Hold)
                {
                    endTable[entry.Id * EntitySpawnFactory.IdsvDirectionStride + direction] = new AnimationEndInfo { Kind = AnimationEndKind.Hold };
                }
                else if (once)
                {
                    endTable[entry.Id * EntitySpawnFactory.IdsvDirectionStride + direction] = new AnimationEndInfo { Kind = AnimationEndKind.Chain, ChainTargetAnimationId = entry.ChainTo };
                }
            }
        }

        hero.AnimSetsByAnim = sets;
        hero.AnimationEndByAnimDirection = endTable;
        EntitySpawnFactory.SubscribeAnimationEndBridge(entity);
    }
}
