#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using static Alundra.Tests.Hero.CarryScenes;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, "Valeurs attendues"; (w3) and (w4)): the hero walks and jumps with the object in his hands, at 1, 2 and 3 ticks per frame, on <see cref="CarryRig"/>. The walk starts at full speed (the sheet 7 has no
/// acceleration: the first step is 2.4375 px) and keeps the pace of the free walk; it is stopped at 133.0 against a wall of 16, 32 or 48 px with or without the object; the standing jump goes through the states 46, 48 and 12, the walking one through 6, 47 and 7,
/// with the heights of the sheet (the same as the jumps without an object) and the object 32 px over the hero at every tick. Written BEFORE the branching of the carrying group (W4): red on the stubs.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryWalkAndJumpTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryWalkAndJumpTests()
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

    public static TheoryData<int, int> WallCases
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (var ticks in new[] { 1, 2, 3 })
            {
                foreach (var height in new[] { 16, 32, 48 })
                {
                    data.Add(ticks, height);
                }
            }

            return data;
        }
    }

    // -----------------------------------------------------------------------------------------
    // (w3) the walk.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W3_TheWalkWithAnObject_StartsAtFullSpeed_ThenKeepsThePaceOfTheFreeWalk_AndTheObjectStaysOverTheHead(int ticksPerFrame)
    {
        var from = int.MaxValue;
        var rig = Carrying(ticksPerFrame, t => t >= from ? Right : 0u);
        StandWithTheObject(rig);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        from = rig.Tick + 1;
        var start = hero.PosX;
        var failures = new List<string>();
        rig.Run(14, tick =>
        {
            var walked = tick - from + 1;
            if (hero.PosX - start != walked * StepPerTick)
            {
                failures.Add($"tick {walked}: walked {(hero.PosX - start) / (double)Px} px, expected {walked * StepPerTick / (double)Px}");
            }

            if (hero.TargetAnimationId != 7u)
            {
                failures.Add($"tick {walked}: animation {hero.TargetAnimationId}, expected 7");
            }

            if (Offset(hero, crate) != (0, 0, 32))
            {
                failures.Add($"tick {walked}: the crate is at {Offset(hero, crate)}");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));

        // The free walk next to it (nothing in the hands): the ramp of the sheet 1 (acceleration 1) first, the same pace after it.
        var freeFrom = int.MaxValue;
        var free = Carrying(ticksPerFrame, t => t >= freeFrom ? Right : 0u, carry: false);
        StandWithTheObject(free);
        freeFrom = free.Tick + 1;
        var previous = free.Hero.PosX;
        var steps = new List<int>();
        free.Run(14, _ =>
        {
            steps.Add(free.Hero.PosX - previous);
            previous = free.Hero.PosX;
        });

        Assert.True(steps[0] < StepPerTick, $"the free walk starts with a ramp: {steps[0]}");
        Assert.All(steps.Skip(8), step => Assert.Equal(StepPerTick, step));
    }

    private static (double X, int Adjusted, (int X, int Y, int Z) Offset) WalkIntoAWall(int ticksPerFrame, int height, bool carried)
    {
        var from = int.MaxValue;
        var rig = Carrying(ticksPerFrame, t => t >= from ? Right : 0u, probe: true, carry: carried);
        rig.AddObject("Wall", 144, 92, 24, 16, height);
        StandWithTheObject(rig);
        from = rig.Tick + 1;
        rig.Run(30);
        return (rig.Hero.PosX / (double)Px, rig.Hero.ForceAdjusted, carried ? Offset(rig.Hero, rig.Crate!) : default);
    }

    [Theory]
    [MemberData(nameof(WallCases))]
    public void W3_TheWalkIntoAWallOf16_32Or48Pixels_IsStoppedAt133_WithOrWithoutTheObject(int ticksPerFrame, int height)
    {
        var carried = WalkIntoAWall(ticksPerFrame, height, carried: true);
        var free = WalkIntoAWall(ticksPerFrame, height, carried: false);

        Assert.True(Math.Abs(free.X - 133.0) < 0.01, $"the free hero stops at {free.X}");
        Assert.True(Math.Abs(carried.X - 133.0) < 0.01, $"the hero with the object stops at {carried.X}");
        Assert.Equal(1, free.Adjusted);
        Assert.Equal(1, carried.Adjusted);
        Assert.Equal((0, 0, 32), carried.Offset);
    }

    // -----------------------------------------------------------------------------------------
    // (w4) the jumps.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W4_TheStandingJumpWithAnObject_FollowsTheSheet_AndTheObjectStaysOver32PixelsOverTheHero(int ticksPerFrame)
    {
        var jump = int.MaxValue;
        var rig = Carrying(ticksPerFrame, t => t == jump ? Cross : 0u);
        StandWithTheObject(rig);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        jump = rig.Tick + 1;
        var x0 = hero.PosX;
        var failures = new List<string>();
        rig.Run(23, tick =>
        {
            var k = tick - jump;
            var expectedAnimation = k == 0 ? 46u : k <= 20 ? 48u : 12u;
            var expectedHeight = k <= 19 ? JumpHeights[k] : 0;
            if (hero.TargetAnimationId != expectedAnimation)
            {
                failures.Add($"T+{k}: animation {hero.TargetAnimationId}, expected {expectedAnimation}");
            }

            if (hero.PosZ != expectedHeight)
            {
                failures.Add($"T+{k}: height {hero.PosZ / (double)Px}, expected {expectedHeight / (double)Px}");
            }

            if (crate.PosZ - hero.PosZ != 32 * Px || crate.PosX != hero.PosX || crate.PosY != hero.PosY)
            {
                failures.Add($"T+{k}: the crate is at {Offset(hero, crate)}");
            }

            if (hero.PosX != x0)
            {
                failures.Add($"T+{k}: the hero moved to {(hero.PosX - x0) / (double)Px}");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(8)));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void W4_TheWalkingJumpWithAnObject_FollowsTheSheet_KeepsThePaceOfTheWalk_AndTheObjectStaysOver32PixelsOverTheHero(int ticksPerFrame)
    {
        var from = int.MaxValue;
        var jump = int.MaxValue;
        var rig = Carrying(ticksPerFrame, t => t >= from ? Right | (t == jump ? Cross : 0u) : 0u);
        StandWithTheObject(rig);
        var hero = rig.Hero;
        var crate = rig.Crate!;
        from = rig.Tick + 1;
        jump = from + 6;
        rig.Run(6);
        var previousX = hero.PosX;
        var failures = new List<string>();
        rig.Run(23, tick =>
        {
            var k = tick - jump;
            var expectedAnimation = k == 0 ? 6u : k <= 20 ? 47u : 7u;
            var expectedHeight = k <= 19 ? JumpHeights[k] : 0;
            if (hero.TargetAnimationId != expectedAnimation)
            {
                failures.Add($"T+{k}: animation {hero.TargetAnimationId}, expected {expectedAnimation}");
            }

            if (hero.PosZ != expectedHeight)
            {
                failures.Add($"T+{k}: height {hero.PosZ / (double)Px}, expected {expectedHeight / (double)Px}");
            }

            if (hero.PosX - previousX != StepPerTick)
            {
                failures.Add($"T+{k}: step {(hero.PosX - previousX) / (double)Px} px, expected {StepPerTick / (double)Px}");
            }

            previousX = hero.PosX;

            if (crate.PosZ - hero.PosZ != 32 * Px || crate.PosX != hero.PosX || crate.PosY != hero.PosY)
            {
                failures.Add($"T+{k}: the crate is at {Offset(hero, crate)}");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(8)));
    }
}
