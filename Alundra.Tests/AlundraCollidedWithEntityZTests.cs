#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 R5 (docs/plan-e19-opcodes.md §1.2h.3.1), test UJ-4: <c>CollidedWithEntityZ</c> (+0x140) is cleared at the head of every motion
/// tick of every entity and raised by the terrain landing only when the binary's STRICT test holds on the force of the tick
/// (<c>moddedPosZ + F &lt; terrainHeight</c>, F read before the grounded reset), by an entity support, and, for the hero outside the
/// airborne state, on every tick of its rest with gravity.
/// </summary>
public sealed class AlundraCollidedWithEntityZTests
{
    [Fact]
    public void UJ4_ANpcAtRestWithGravity_HasItRaisedAfterEveryUpdate()
    {
        var rig = JumpNpcRig.Build();

        for (var update = 1; update <= 10; update++)
        {
            rig.Update();
            Assert.Equal(1, rig.Npc.CollidedWithEntityZ);
            Assert.Equal(1, rig.Npc.IsOnGround);
            Assert.Equal(0, rig.Npc.PosZ);
        }
    }

    [Fact]
    public void UJ4_ANpcAtRestWithoutGravity_HasItAtZero_AndIsOnGround()
    {
        // F == 0: the strict test does not hold (0 < 0), the landing branch still lands the entity.
        var rig = JumpNpcRig.Build(gravity: false);

        for (var update = 1; update <= 10; update++)
        {
            rig.Update();
            Assert.Equal(0, rig.Npc.CollidedWithEntityZ);
            Assert.Equal(1, rig.Npc.IsOnGround);
        }
    }

    [Fact]
    public void UJ4_ANpcInTheAir_HasItAtZero()
    {
        var rig = JumpNpcRig.Build(gravity: false);
        rig.Npc.ForceZ = 65536; // a scripted rise of 1 px per tick (0x1B), no gravity: it never comes back.

        for (var update = 1; update <= 5; update++)
        {
            rig.Update();
            Assert.Equal(update * 65536, rig.Npc.PosZ);
            Assert.Equal(0, rig.Npc.CollidedWithEntityZ);
            Assert.Equal(0, rig.Npc.IsOnGround);
        }
    }

    [Fact]
    public void UJ4_ANpcSupportedByAnEntityAtN_ThenInTheAirAtNPlus1_HasItAtZeroAtNPlus1()
    {
        // A platform (no controller) of 32 px under an NPC that rests on its top, one 16.16 unit above it as the spawn leaves it.
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost();
        var platform = ContactWorld.AddEntity(world, host, "Platform", 200, 100, 0, -10, -7, 0, 40, 28, 32, withController: false);
        var npc = ContactWorld.AddEntity(world, host, "Sailor", 200, 100, 32, -10, -7, 0, 20, 14, 32);
        npc.Flags |= EntityFlags.Gravity;
        npc.MapGravityRaw = 128;
        npc.MapZViscosityRaw = 4096;
        npc.ResyncControllerFromFlags();
        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };

        for (var update = 1; update <= 3; update++)
        {
            rig.Update();
            Assert.True(npc.WasEntitySupportedLastTick, $"update {update}: supported");
            Assert.Equal(1, npc.CollidedWithEntityZ);
        }

        // The platform goes: the NPC is in the air on the next update (F < 0, far above the terrain, no support).
        platform.Status = EntityStatus.FlagToDestroy;
        rig.Update();
        Assert.False(npc.WasEntitySupportedLastTick);
        Assert.Equal(0, npc.CollidedWithEntityZ);
        Assert.Equal(0, npc.IsOnGround);
    }

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

    [Fact]
    public void UJ4_TheHeroAtRestOutsideTheAirborneState_HasItRaisedAfterEveryUpdate_WithGravity_AndNotWithout()
    {
        var projectRoot = FindProjectRoot();
        Assert.NotNull(projectRoot);

        foreach (var gravity in new[] { true, false })
        {
            var world = HeroWorldFixture.BuildWorld(FlatCells.Create());
            var controller = new AlundraPlayerController { PadStateProviderForTests = () => default };
            var host = new ContactHost(null, AlundraGameState.PlayerControlBits.ControlLocked, controller);
            var settings = HeroWorldFixture.LoadHeroControllerSettings(projectRoot!);
            settings.Gravity = 1250f;
            settings.MaxFallSpeed = 800f;
            var (_, hero) = HeroWorldFixture.BuildHeroPawn(world, settings, new Vector3(310f, 207f, 0f), host);
            AlundraEntitySpawnFactory.SetEntityDimensions(hero, -10, -7, 0, 21, 15, 32);
            hero.MapGravity = 1250f;
            hero.MapMaxFallSpeed = 800f;
            if (gravity)
            {
                hero.Flags |= EntityFlags.Gravity; // BuildHeroPawn leaves Flags at 0 (under ControlLocked, MovePlayer never raises the bit).
            }

            world.Update(0.02f); // settling.
            for (var update = 1; update <= 5; update++)
            {
                world.Update(0.02f);
                Assert.Equal(1, hero.IsOnGround);
                Assert.Equal(gravity ? 1 : 0, hero.CollidedWithEntityZ);
            }
        }
    }
}
