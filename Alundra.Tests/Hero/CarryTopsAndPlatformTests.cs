#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using static Alundra.Tests.Hero.CarryScenes;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, "Valeurs attendues"; (w5)): the hero with the object in his hands lands on the top of an object of 16 px (the model of UH-10: the support, the height of the foot), is stopped by an object of 32 px that his jump
/// does not clear, and rides a platform that moves west (the model of UH-14, in phase 0 since D5b: no tick of delay), at 1, 2 and 3 ticks per frame. Each scene is played next to the same scene without the object, and the support (the carrier relation of the hero),
/// the height and the ground flag are the same at every tick; the object stays 32 px over the hero. Written BEFORE the branchings of the follow and of the carrying group (W3, W4): red on the stubs.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryTopsAndPlatformTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryTopsAndPlatformTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    private sealed record Row(int X, int Z, bool Riding, int Ground, (int X, int Y, int Z) Offset);

    /// <summary>The walking jump onto the object of <paramref name="height"/> px at 144, started <paramref name="offset"/> ticks after the walk: the rows from T on.</summary>
    private static List<Row> JumpOntoTheObject(int ticksPerFrame, int height, int offset, bool carried)
    {
        var from = int.MaxValue;
        var jump = int.MaxValue;
        var rig = Carrying(ticksPerFrame, t => t >= from ? Right | (t == jump ? Cross : 0u) : 0u, probe: true, carry: carried);
        var wall = rig.AddObject("Chest", 144, 92, 24, 16, height);
        StandWithTheObject(rig);
        from = rig.Tick + 1;
        jump = from + offset;
        var hero = rig.Hero;
        var rows = new List<Row>();
        rig.Run(offset + 30, tick =>
        {
            if (tick >= jump)
            {
                rows.Add(new Row(hero.PosX, hero.PosZ, hero.RidingEntity != null && ReferenceEquals(hero.RidingEntity, wall.LogicContextEntity), hero.IsOnGround, carried ? Offset(hero, rig.Crate!) : default));
            }
        });

        return rows;
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W5_TheJumpOntoAnObjectOf16Pixels_LandsOnItAtTheTickOfTheFreeHero_RidesIt_AndTheObjectFollowsTheHero(int ticksPerFrame)
    {
        foreach (var offset in new[] { 0, 3, 6 })
        {
            var carried = JumpOntoTheObject(ticksPerFrame, 16, offset, carried: true);
            var free = JumpOntoTheObject(ticksPerFrame, 16, offset, carried: false);

            // The free hero lands on the top at T + 17 (the foot at 16 px, on the ground) and rides it from T + 18: the pin of UH-10.
            var landing = free.FindIndex(row => row.Ground == 1);
            var riding = free.FindIndex(row => row.Riding);
            Assert.Equal(17, landing);
            Assert.Equal(18, riding);
            Assert.Equal(16 * Px, free[landing].Z);

            // From T to T + 20: the hero walks on the top later (the free hero starts with a ramp, so the two do not leave the top at the same tick).
            for (var k = 0; k <= 20; k++)
            {
                Assert.True(carried[k].Z == free[k].Z, $"offset {offset}, T+{k}: height {carried[k].Z / (double)Px} against {free[k].Z / (double)Px} without the crate");
                Assert.True(carried[k].Riding == free[k].Riding, $"offset {offset}, T+{k}: support {carried[k].Riding} against {free[k].Riding} without the crate");
                Assert.True(carried[k].Ground == free[k].Ground, $"offset {offset}, T+{k}: ground {carried[k].Ground} against {free[k].Ground} without the crate");
                Assert.True(carried[k].Offset == (0, 0, 32), $"offset {offset}, T+{k}: the crate is at {carried[k].Offset}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W5_TheJumpAgainstAnObjectOf32Pixels_IsNeverSupportedByIt_AndTheHeroIsStoppedAt133_WithOrWithoutTheObject(int ticksPerFrame)
    {
        var carried = JumpOntoTheObject(ticksPerFrame, 32, 3, carried: true);
        var free = JumpOntoTheObject(ticksPerFrame, 32, 3, carried: false);

        for (var k = 0; k < 28; k++)
        {
            Assert.False(carried[k].Riding, $"T+{k}: the hero with the object rides the wall");
            Assert.False(free[k].Riding, $"T+{k}: the free hero rides the wall");
            Assert.True(carried[k].Z == free[k].Z, $"T+{k}: height {carried[k].Z / (double)Px} against {free[k].Z / (double)Px}");
            Assert.True(carried[k].X <= 133 * Px + 700 && free[k].X <= 133 * Px + 700, $"T+{k}: x {carried[k].X / (double)Px} / {free[k].X / (double)Px}");
            Assert.True(carried[k].Offset == (0, 0, 32), $"T+{k}: the crate is at {carried[k].Offset}");
        }

        Assert.True(carried.Any(row => row.Z > 0), "the hero jumped");
    }

    /// <summary>
    /// The montage of UH-14: the hero rests on a platform of 16 px under him (its logical box x 144..168, y 92..108, offset from its root so that the physical box never meets the hero's body while it moves west); with <paramref name="carried"/>, a crate stands
    /// on the platform too, is lifted (the tick L) and the hero stands with it over his head.
    /// </summary>
    private static (CarryRig Carry, EntityScriptProxy Platform) PlatformScene(int ticksPerFrame, bool carried)
    {
        var carry = CarryRig.Build(ticksPerFrame, heroX: 156.25f, heroY: 100.5f);
        var platform = carry.AddObject("Platform", 100, 92, 24, 16, 16, offsetX: 44);
        var hero = carry.Hero;
        hero.PosZ = 16 * Px;
        hero.PushLogicalPositionToRoot();
        carry.Run(4);
        Assert.Equal(16 * Px, hero.PosZ);
        Assert.Same(platform.LogicContextEntity, hero.RidingEntity);
        if (carried)
        {
            // The crate stands on the head of the hero (48 px over the ground, 32 over him): the lift gives the carrying stand at once.
            carry.AddCrate(156, 100, 48);
            carry.LiftNow(fromTheHead: true);
            carry.Run(3);
            Assert.Equal((0, 0, 32), Offset(hero, carry.Crate!));
            Assert.Same(platform.LogicContextEntity, hero.RidingEntity);
        }

        return (carry, platform);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W5_ThePlatformThatMovesWest_CarriesTheHeroInPhase0_AndTheObjectFollowsTheHero_AsWithoutTheObject(int ticksPerFrame)
    {
        foreach (var carried in new[] { true, false })
        {
            var (carry, platform) = PlatformScene(ticksPerFrame, carried);
            var hero = carry.Hero;

            // The force of the platform held at 1 px per tick (no speed from the animation sets, so no recomputation of the target).
            platform.TargetDirection = platform.CurrentDirection;
            platform.TargetForceX = -65536;
            platform.ForceX = -65536;
            platform.ForceStepX = 0;
            var heroStart = hero.PosX;
            var platformStart = platform.PosX;
            var start = carry.Tick;
            var failures = new List<string>();
            carry.Run(50, tick =>
            {
                var n = tick - start;
                if (platform.PosX - platformStart != -n * Px)
                {
                    failures.Add($"tick {n}: platform {(platform.PosX - platformStart) / (double)Px}");
                }

                if (hero.PosX - heroStart != -n * Px)
                {
                    failures.Add($"tick {n}: hero {(hero.PosX - heroStart) / (double)Px}");
                }

                if (hero.PosZ != 16 * Px || hero.IsOnGround != 1 || !ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity))
                {
                    failures.Add($"tick {n}: posZ {hero.PosZ / (double)Px}, ground {hero.IsOnGround}, riding {hero.RidingEntity != null}");
                }

                if (carried && Offset(hero, carry.Crate!) != (0, 0, 32))
                {
                    failures.Add($"tick {n}: the crate is at {Offset(hero, carry.Crate!)}");
                }
            });

            Assert.True(failures.Count == 0, $"carried {carried}: " + string.Join("\n", failures.Take(6)));
        }
    }
}
