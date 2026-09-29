#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.a2 T3 (docs/plan-e19-opcodes.md §1.2b, ADR-0016 D-E19-8): the four walks of the cabin of map 390 (B2, 80 px
/// north, 48 west, 80 north, 32 west) played by the real hero controller on the real cells of the map, with the real
/// header and controller settings of the hero and the fifteen rails B2 lays with its <c>0x54</c> (<c>W |= 1</c>). The
/// second north walk ends against the rail (42,12); the original advances the blocked step to the contact
/// (<c>ComputeXYPosition</c>, <c>0x80037730</c>) and finishes at y = 215.0. A controller that rejects the whole
/// blocked step stops 78.8 px from its start and the script's <c>0x1E @658</c> waits forever. The arcs of
/// <see cref="ArcRun"/> and the A1c arc test the same scene through the script; this one isolates the physics.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraCabinWalkTests
{
    private const string WorldName = "Ship Klark (inner)-390";

    /// <summary>The fifteen cells of the <c>0x54</c> of B2 (<c>@558</c> to <c>@628</c>), each given <c>W |= 1</c>.</summary>
    private static readonly (int X, int Y)[] Rails =
    {
        (42, 12), (43, 13), (43, 14), (43, 15), (43, 16), (43, 17), (41, 15), (41, 16), (41, 17), (41, 18), (41, 19),
        (42, 19), (43, 19), (43, 20), (45, 20),
    };

    private const uint West = 0x08;
    private const uint North = 0x10;

    [Theory]
    [InlineData(0.02f)]
    [InlineData(1f / 60f)]
    public void TheFourWalks_ReachTheirDistance_TheSecondNorthWalkEndsExactlyOnTheContact(float dt)
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var tileMapPath = Path.Combine(root, "Maps", "The Klark", WorldName, "tilemap", WorldName + ".tileMap");
        Assert.True(File.Exists(tileMapPath), $"the real export of map '{WorldName}' is missing: '{tileMapPath}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        Assert.True(AlundraCellsCollisionField.TryCreate(tileMapData, WorldName, out var field, out var records));
        foreach (var (x, y) in Rails)
        {
            records!.Walkability[y * tileMapData.MapSize.Width + x] |= 1;
        }

        var world = HeroWorldFixture.BuildWorld(field!);
        world.RuntimeSystems.CharacterMotion.MaxStepsPerFrame = 4;
        var start = new Vector3(44 * 24 + 12, 23 * 16 + 8, 64f);
        var (_, hero) = HeroWorldFixture.BuildHeroPawn(world, HeroWorldFixture.LoadHeroControllerSettings(root), start, new Host());

        // The hero's flags, animation sets and body box from the export's header, then the mask derived from its flags
        // (ClassB: 0x41), as production does after the header.
        var header = HeroWorldFixture.LoadHeroHeader(root);
        hero.TargetAnimationId = 0;
        hero.TargetDirection = 0;
        hero.CurrentAnimationId = ~0u;
        hero.IsOnGround = 1;
        hero.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));
        hero.AnimSetsByAnim = header.AnimSets;
        AlundraEntitySpawnFactory.SetEntityDimensions(hero, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);
        tileMapData.CustomProperties.TryGetValue("Gravity", out var gravityRaw);
        tileMapData.CustomProperties.TryGetValue("ZViscosity", out var viscosityRaw);
        hero.MapGravityRaw = int.Parse(gravityRaw!);
        hero.MapZViscosityRaw = int.Parse(viscosityRaw!);
        hero.Controller!.Settings.Gravity = hero.MapGravityRaw * 256f / 65536f * 2500f;
        hero.Controller.Settings.MaxFallSpeed = hero.MapZViscosityRaw * 256f / 65536f * 50f;
        hero.ResyncControllerFromFlags();
        Assert.Equal(0x41u, hero.Controller.Settings.WalkabilityMask);

        hero.ClampToGround();
        Settle(world, 5);

        var walks = new (uint Direction, int Distance, string Name)[]
        {
            (North, 80, "north 80"), (West, 48, "west 48"), (North, 80, "north 80 (second)"), (West, 32, "west 32"),
        };
        for (var i = 0; i < walks.Length; i++)
        {
            var (direction, distance, name) = walks[i];
            Walk(world, hero, direction, distance, name, dt);

            if (i == 2)
            {
                // The last tick of the third walk crosses the contact with the rail (42,12): the near corner of the
                // footprint (root - 7) rests on the frontier y = 208 of row 12, so root.y = 215.0 exactly.
                Assert.Equal(215 << 16, hero.PosY);
            }

            hero.TargetAnimationId = 0;
            Settle(world, 2);
        }
    }

    /// <summary>One walk like a scripted <c>0x1E</c>: the direction and the walk animation held until the hero has
    /// covered <paramref name="distance"/> px on the walked axis (whole pixels), or fails after 400 frames or 30 frames
    /// without any motion, naming where the hero stopped.</summary>
    private static void Walk(World world, AlundraEntityScriptProxy hero, uint direction, int distance, string name, float dt)
    {
        hero.TargetDirection = direction;
        hero.TargetAnimationId = 1;
        var x0 = hero.PosX;
        var y0 = hero.PosY;
        var lastPosition = (hero.PosX, hero.PosY);
        var stuckFrames = 0;
        for (var frame = 1; frame <= 400; frame++)
        {
            world.Update(dt);
            var dx = Math.Abs(hero.PosX - x0) >> 16;
            var dy = Math.Abs(hero.PosY - y0) >> 16;
            if (distance <= dx || distance <= dy)
            {
                return;
            }

            stuckFrames = (hero.PosX, hero.PosY) == lastPosition ? stuckFrames + 1 : 0;
            Assert.True(
                stuckFrames <= 30,
                $"walk '{name}' (dt {dt}) is stuck at ({hero.PosX / 65536.0:F2}, {hero.PosY / 65536.0:F2}) after {frame} frames, "
                + $"{Math.Max(dx, dy)} of {distance} px covered, forceAdjusted={hero.ForceAdjusted}");
            lastPosition = (hero.PosX, hero.PosY);
        }

        Assert.Fail($"walk '{name}' (dt {dt}) did not cover {distance} px within 400 frames: at ({hero.PosX / 65536.0:F2}, {hero.PosY / 65536.0:F2})");
    }

    private static void Settle(World world, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            world.Update(0.02f);
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

    /// <summary>A script host whose control is locked (a cutscene): the hero walks by its <c>TargetDirection</c>.</summary>
    private sealed class Host : IAlundraScriptHost
    {
        private readonly AlundraLogicClock _clock = new();

        public IEventProgramRunner Runner { get; } = new NoOpRunner();

        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }

        public AlundraGameState GameState { get; } = new() { PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked };

        public AlundraPlayerController? PlayerController { get; init; } = new AlundraPlayerController();

        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime)
        {
            var ticks = _clock.TicksThisFrame(elapsedTime);
            _clock.CloseFrame();
            return ticks;
        }
    }
}
