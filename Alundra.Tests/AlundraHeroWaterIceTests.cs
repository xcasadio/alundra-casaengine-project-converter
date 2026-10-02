#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 C4 (docs/plan-e19-opcodes.md §1.2h.3.1, R7 and the x160 of R6): the water, the ice and the lower jump of the hero, and the boots level, read
/// from <c>CombinedVramFlagsOR</c> which is refreshed after every tick of the hero. Montage: <see cref="JumpHeroRig"/> on a field of synthetic cells
/// (<see cref="FlatCells"/>: walkability <c>0x18</c> = water and the x160 bit, <c>0x20</c> = ice, height 0 or 16 px), one settling update at rest, the pad
/// locked (the animation and the direction are written by the test).
/// </summary>
public class AlundraHeroWaterIceTests
{
    private const int East = 24;

    private static AlundraCellsCollisionField Cells(int walkability, int height = 0)
        => FlatCells.Create(cell: (_, _) => (walkability, height));

    /// <summary>The <c>ForceX</c> of the hero after each of <paramref name="ticks"/> updates, walking east in the animation 1 (speed 208, acceleration 1).</summary>
    private static int[] WalkEast(JumpHeroRig rig, int ticks)
    {
        rig.Hero.TargetDirection = East;
        rig.Hero.TargetAnimationId = 1;
        var forces = new int[ticks];
        for (var i = 0; i < ticks; i++)
        {
            rig.Update();
            forces[i] = rig.Hero.ForceX;
        }

        return forces;
    }

    [Fact]
    public void UW1_WaterSlowsTheHeroToHalfItsTarget_UntilTheBootsOfLevelOneAreOwned()
    {
        var rig = JumpHeroRig.Build(Cells(0x18));
        Assert.Equal(0x18u, rig.Hero.CombinedVramFlagsOR & 0x18u); // the settling update posted the flags.
        Assert.Equal(new[] { 79872, 79872, 79872, 79872 }, WalkEast(rig, 4));

        // The cache is the one of the animation, untouched by the water.
        Assert.Equal(159744, rig.Hero.TargetForceX);
        Assert.Equal(79872, rig.Hero.ForceStepX);

        var boots = JumpHeroRig.Build(Cells(0x18));
        boots.Host.GameState.NumberOfItems[0x1A * 2 + 1] = 1;
        Assert.Equal(new[] { 79872, 159744, 159744, 159744 }, WalkEast(boots, 4));
    }

    [Fact]
    public void UW2_IceDividesTheStepBySixteen_TheHeroSlidesToAHaltAndTheCachesAreKept()
    {
        var rig = JumpHeroRig.Build(Cells(0x20));
        rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 1 });

        var forces = WalkEast(rig, 32);
        Assert.Equal(Enumerable.Range(1, 32).Select(k => 4992 * k).ToArray(), forces);
        Assert.Equal(159744, forces[31]);
        Assert.Equal(159744, rig.Hero.TargetForceX);
        Assert.Equal(79872, rig.Hero.ForceStepX);

        rig.Hero.TargetAnimationId = 0;
        var stopping = new List<int>();
        for (var i = 0; i < 32; i++)
        {
            rig.Update();
            stopping.Add(rig.Hero.ForceX);
        }

        Assert.Equal(Enumerable.Range(1, 31).Select(k => 159744 - 4992 * k).Append(0).ToArray(), stopping);
        Assert.Equal(31, stopping.Count(f => f != 0));
        Assert.Equal(37.78125, stopping.Sum() / 65536.0, 6); // the slide, in pixels.
    }

    [Fact]
    public void UW3_ANpcOnWaterOrIceHasTheForcesOfTheSameNpcOnFlatGround_TickForTick()
    {
        static int[] Walk(int vramFlags)
        {
            var rig = JumpNpcRig.Build();
            rig.SetAnimSet(new AnimSetEntry { Anim = 4, Speed = 208, Acceleration = 1 });
            rig.Npc.CombinedVramFlagsOR = (uint)vramFlags;
            rig.Npc.TargetDirection = East;
            rig.Npc.TargetAnimationId = 4;
            var forces = new int[8];
            for (var i = 0; i < forces.Length; i++)
            {
                rig.Update();
                forces[i] = rig.Npc.ForceX;
            }

            return forces;
        }

        var flat = Walk(0);
        Assert.Equal(159744, flat[^1]);
        Assert.Equal(flat, Walk(0x08 | 0x10));
        Assert.Equal(flat, Walk(0x20));
        Assert.Equal(flat, Walk(0x28));
    }

    [Fact]
    public void UW4_TheLowerJump_OnWaterWithoutBoots_IsTheX160Impulse_AndBootsGiveTheNormalOne()
    {
        var heights = new[] { 204800, 376832, 516096, 622592, 696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496 };

        var rig = JumpHeroRig.Build(Cells(0x18));
        rig.SetAnimSet(new AnimSetEntry { Anim = 2, Speed = 0, Acceleration = 1, IsZForceApplied = 1280, Sfx = 10 });
        rig.Hero.TargetAnimationId = 2;
        rig.Update();
        Assert.Equal(204800, rig.Hero.ForceZ);
        Assert.Equal(heights[0], rig.Hero.PosZ);
        for (var update = 2; update <= 13; update++)
        {
            rig.Update();
            Assert.Equal(heights[update - 1], rig.Hero.PosZ);
            Assert.Equal(0, rig.Hero.IsOnGround);
        }

        rig.Update(); // update 14: landed.
        Assert.Equal(0, rig.Hero.PosZ);
        Assert.Equal(1, rig.Hero.CollidedWithEntityZ);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);

        // Boots of level 1: the impulse of dry ground (the values of UJ-3).
        var boots = JumpHeroRig.Build(Cells(0x18));
        boots.Host.GameState.NumberOfItems[0x1A * 2 + 1] = 1;
        boots.SetAnimSet(new AnimSetEntry { Anim = 2, Speed = 0, Acceleration = 1, IsZForceApplied = 1280, Sfx = 10 });
        boots.Hero.TargetAnimationId = 2;
        boots.Update();
        Assert.Equal(327680, boots.Hero.ForceZ);
        Assert.Equal(327680, boots.Hero.PosZ);
    }

    [Fact]
    public void UW5_ABankNextToWater_SlowsNothing_AndInTheAirTheFlagsAreZero()
    {
        // The hero stands on cells of 16 px (cells x <= 8); the water is at 0 px, from x = 9 (216 px): its corners over the water are not at the height of the
        // hero's feet and do not qualify.
        var field = FlatCells.Create(cell: (cx, _) => cx <= 8 ? (0, 1) : (0x18, 0));
        var rig = JumpHeroRig.Build(field, x: 204.25f, z: 16f);
        Assert.Equal(16 << 16, rig.Hero.PosZ);
        Assert.Equal(0u, rig.Hero.CombinedVramFlagsOR);
        Assert.Equal(new[] { 79872, 159744 }, WalkEast(rig, 2));
        Assert.True(rig.Hero.PosX + rig.Hero.ModX + rig.Hero.Width >= 216 << 16, "the east corners are over the water");

        // In the air the flags are 0 (no corner is at the height of the feet), whatever the cells.
        var jump = JumpHeroRig.Build(Cells(0x18));
        Assert.NotEqual(0u, jump.Hero.CombinedVramFlagsOR);
        jump.Hero.TargetAnimationId = 43;
        jump.Update();
        Assert.True(jump.Hero.PosZ > 0);
        Assert.Equal(0u, jump.Hero.CombinedVramFlagsOR);
    }

    [Fact]
    public void UW6_TheFlagsAreRefreshedAfterEveryTickOfTheHero_NotOnlyAtTheEndOfTheFrame()
    {
        // Dry cells up to x = 215, water from x = 216. The hero walks east and his east corners enter the water at the second tick of a frame of three ticks: the
        // third tick of that same frame already reads the water (the flags are those of the position after the tick before).
        var field = FlatCells.Create(cell: (cx, _) => cx <= 8 ? (0, 0) : (0x18, 0));
        var rig = JumpHeroRig.Build(field, x: 203f);
        Assert.Equal(0u, rig.Hero.CombinedVramFlagsOR);
        rig.Hero.TargetDirection = East;
        rig.Hero.TargetAnimationId = 1;
        rig.Update(0.06f); // three logic ticks.
        Assert.Equal(79872, rig.Hero.ForceX);
        Assert.Equal(0x18u, rig.Hero.CombinedVramFlagsOR & 0x18u);
    }

    [Theory]
    [InlineData(0x08u, new[] { 79872, 79872 })]
    [InlineData(0u, new[] { 79872, 159744 })]
    public void TRB_ABareProxyWithNoScriptHost_ReadsTheBootsLevelWithoutException(uint vramOr, int[] expected)
    {
        var hero = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            ScriptHost = null,
            CombinedVramFlagsOR = vramOr,
            AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 } },
            TargetDirection = East,
            TargetAnimationId = 1,
        };

        var forces = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            AlundraPlayerManager.Tick(hero, 1);
            forces.Add(hero.ForceX);
        }

        Assert.Equal(expected, forces.ToArray());
    }
}
