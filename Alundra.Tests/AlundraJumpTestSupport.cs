#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

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

    public required AlundraEntityScriptProxy Npc { get; init; }

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
    /// <c>AlundraAnimationClockDriveTests</c>, on the entity of the NPC): the animations <paramref name="anims"/> (each with the impulse it carries),
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
                endTable[(int)anim.Id * AlundraEntitySpawnFactory.IdsvDirectionStride] = new AnimationEndInfo { Kind = anim.End, ChainTargetAnimationId = anim.ChainTo };
            }
        }

        root.AddChildComponent(sprite);

        var entity = new Entity { Name = "SpriteJumper", RootComponent = root, GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
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

        var npc = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        npc.Controller = entity.GetComponent<CharacterControllerComponent>();
        npc.ScriptHost = host;
        npc.Status = EntityStatus.Normal;
        npc.Flags &= ~EntityFlags.Gravity;
        npc.Flags |= EntityFlags.Collidable;
        if (gravity)
        {
            npc.Flags |= EntityFlags.Gravity;
        }

        AlundraEntitySpawnFactory.SetEntityDimensions(npc, -10, -7, 0, 20, 14, 32);
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
        AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(entity);

        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };
        rig.Update(); // the settling update.
        return rig;
    }

    /// <summary>
    /// Builds the NPC montage and runs ONE settling update at rest (animation 0 current and target). <paramref name="impulse"/> is the
    /// <c>IsZForceApplied</c> of the animation <see cref="ImpulseAnimation"/> (animation 0 has speed 0 and no impulse); the animation
    /// sets are the caller's to extend through <see cref="AlundraEntityScriptProxy.AnimSetsByAnim"/>.
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

internal static class ProjectRootFinder
{
    /// <summary>The <c>alundra-project</c> folder of the export, found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string Find()
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

        throw new InvalidOperationException($"no 'alundra-project/Maps' directory found above '{AppContext.BaseDirectory}': this test needs the real export");
    }
}

/// <summary>A synthetic flat field of cells (walkability, ground property and height 0) for the hero montages.</summary>
internal static class FlatCells
{
    /// <summary>A field of <paramref name="width"/> x <paramref name="height"/> cells, every one of walkability 0 and height 0.</summary>
    public static AlundraCellsCollisionField Create(int width = 40, int height = 40)
    {
        var count = width * height;
        string Zeros()
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "0" : ",0");
            }

            return sb.Append(']').ToString();
        }

        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(width, height) };
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":" + count + ",\"walkability\":" + Zeros() + ",\"ground_property\":" + Zeros()
            + ",\"slope\":" + Zeros() + ",\"height\":" + Zeros() + ",\"tile_id\":" + Zeros() + ",\"wall_tiles_offset\":" + Zeros()
            + ",\"wall_tiles\":{}}";
        Assert.True(AlundraCellsCollisionField.TryCreate(tileMapData, "flat_cells", out var field));
        return field!;
    }
}
