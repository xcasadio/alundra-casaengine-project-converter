#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Transitions;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c2 D2 (docs/plan-e19-opcodes.md §1.2h.3.2, S1, S2, S6): the jump of the pad and the falls in the real world, on <see cref="JumpHeroRig"/> with a free pad
/// (the real hero controller settings, a field of synthetic cells, the animation sets of the export: 1 walking 208, 2 walking jump 208 with the impulse 1280,
/// 43 standing jump, 44 in the air 196, 45 in the air still). Values of annex B.1 (tables S-A to S-I) as the plan restates them: one tick per frame unless said.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroFallAndPadJumpTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Cross = PadState.Cross;
    private const uint Right = PadState.Right;
    private const uint Left = PadState.Left;
    private const uint Down = PadState.Down;
    private const int East = 24;

    private static readonly int[] JumpHeights =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
        1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680,
    };

    private static readonly int[] WalkingJumpPosX =
    {
        159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1218048, 1368576, 1519104, 1669632, 1820160, 1970688, 2121216, 2271744, 2422272, 2572800,
        2723328, 2873856, 3024384, 3174912, 3330048, 3489792, 3649536,
    };

    public AlundraHeroFallAndPadJumpTests()
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

    private static JumpHeroRig FreeRig(CellsCollisionField? field = null, float x = 200.25f, float z = 0f, float groundSnapDistance = -1f)
        => JumpHeroRig.Build(field, x: x, z: z, freePad: true, groundSnapDistance: groundSnapDistance);

    /// <summary>The hero walking east in the steady state (Moving, ForceX 159744, the pad on the Right), as the tables of annex B.1 start.</summary>
    private static void SteadyWalk(JumpHeroRig rig, uint direction = East, int forceX = 159744, int forceY = 0)
    {
        rig.Hero.TargetAnimationId = 1;
        rig.Hero.TargetDirection = direction;
        rig.Hero.ForceX = forceX;
        rig.Hero.ForceY = forceY;
    }

    // -----------------------------------------------------------------------------------------
    // UH-1 / SJ-5 - the standing jump.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH1_TheStandingJump_FliesTheListFromTheCrossEdge_AndGoesBackToIdle()
    {
        var rig = FreeRig();
        var hero = rig.Hero;
        var (startX, startY) = (hero.PosX, hero.PosY);
        for (var tick = 1; tick <= 22; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            Assert.True(startX == hero.PosX && startY == hero.PosY, label);
            Assert.Equal(tick == 1 ? 1280 : 0, hero.IsZForceApplied);
            if (tick <= 21)
            {
                Assert.Equal(327680 - 32768 * (tick - 1), hero.ForceZ);
                Assert.Equal(tick == 1 ? 43u : 45u, hero.CurrentAnimationId);
            }

            if (tick <= 20)
            {
                Assert.Equal(JumpHeights[tick - 1], hero.PosZ);
                Assert.Equal(0, hero.IsOnGround);
                Assert.Equal(0, hero.CollidedWithEntityZ);
            }
        }

        // Tick 21 (checked below with the state of tick 22): the foot is at the ground, not landed; tick 22: landed, Idle.
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(0, hero.ForceZ);
        Assert.Equal(1, hero.CollidedWithEntityZ);
        Assert.Equal(1, hero.IsOnGround);
        Assert.Equal(0u, hero.CurrentAnimationId);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
    }

    [Fact]
    public void UH1b_Tick21_TheFootIsAtTheGround_TheStrictTestHasNotLandedYet()
    {
        var rig = FreeRig();
        for (var tick = 1; tick <= 21; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
        }

        Assert.Equal(0, rig.Hero.PosZ);
        Assert.Equal(1, rig.Hero.IsOnGround);
        Assert.Equal(0, rig.Hero.CollidedWithEntityZ);
        Assert.Equal(-327680, rig.Hero.ForceZ);
    }

    [Fact]
    public void SJ5_TheTargetsOfTheFrames_AreTheTakeOffThenTheAirThenIdle()
    {
        var rig = FreeRig();
        for (var tick = 1; tick <= 22; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var expected = tick == 1 ? 43u : tick <= 21 ? 45u : 0u;
            Assert.Equal(expected, rig.Hero.TargetAnimationId);
            if (tick <= 20)
            {
                Assert.Equal(JumpHeights[tick - 1], rig.Hero.PosZ);
            }
        }

        Assert.Equal(1, rig.Hero.CollidedWithEntityZ);
        Assert.Equal(0, rig.Hero.ForceZ);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
    }

    [Fact]
    public void SJ5b_AFrameWithoutTick_BetweenThePressAndTheFirstTick_DoesNotLoseTheJump()
    {
        var rig = FreeRig();
        // E20.a A4 (D-E20-2): MovePlayer runs once per tick and not at all on a frame without tick, on the pad of the tick; the edge is the first tick that sees the Cross held,
        // so the Cross must be held until that tick (a press and a release between two ticks has no edge, the binary's pad is sampled once per tick).
        rig.Step(Cross, Cross, 0.001f); // no logic tick on this frame: nothing runs for the hero.
        Assert.Equal(0u, rig.Hero.TargetAnimationId);
        Assert.Equal(0, rig.Hero.PosZ);

        rig.Step(Cross, 0, 0.02f); // the first tick: the Cross is held, the pad of the tick has its rising edge, the hero takes off and the impulse is taken at once.
        Assert.Equal(43u, rig.Hero.TargetAnimationId);
        Assert.Equal(JumpHeights[0], rig.Hero.PosZ);
    }

    [Fact]
    public void SJ5c_At144Hz_TheTicksComeOnTheThirdAndSixthFrames_TheJumpSurvivesTheFramesBetween()
    {
        var rig = FreeRig();
        var posZ = new List<int>();
        for (var frame = 1; frame <= 6; frame++)
        {
            // E20.a A4: the Cross is held until the first tick (frame 3), like a player's thumb; released after it.
            rig.Step(frame <= 3 ? Cross : 0, frame == 1 ? Cross : 0, 1f / 144f);
            posZ.Add(rig.Hero.PosZ);
        }

        Assert.Equal(new[] { 0, 0, JumpHeights[0], JumpHeights[0], JumpHeights[0], JumpHeights[1] }, posZ);
    }

    [Fact]
    public void SJ6_TheEdgeOnAFrameOfTwoTicks_GivesOneImpulse()
    {
        var rig = FreeRig();
        rig.Step(Cross, Cross, 0.04f);
        Assert.Equal(622592, rig.Hero.PosZ); // one impulse, two ticks of flight (not 655360).
    }

    [Fact]
    public void SJ6b_TheTakeOffTargetIsHeldForTheTwoTicksOfTheFrame_TheWalkingJumpKeepsItsSpeedForTheSecondTick()
    {
        var rig = FreeRig();
        SteadyWalk(rig);
        rig.Step(Right | Cross, Cross, 0.04f);
        // E20.a A4: MovePlayer now runs at each of the two ticks like the binary's: tick 1 takes off (animation 2), tick 2 is in the air and picks 0x2C with a direction held
        // (ForceX 155136, the value of tick 2 pinned by UH2 and the D1 oracle); the old pin (2 and 159744) was the per-frame decision, an accepted step of the plan now gone.
        Assert.Equal(44u, rig.Hero.TargetAnimationId);
        Assert.Equal(622592, rig.Hero.PosZ);
        Assert.Equal(155136, rig.Hero.ForceX);

        var standing = FreeRig();
        standing.Step(Cross, Cross, 0.04f);
        Assert.Equal(45u, standing.Hero.TargetAnimationId); // E20.a A4: tick 2 is in the air with no direction held: 0x2D (SJ5 pins the same state one tick later), not the take-off 43 of the per-frame decision.
        Assert.Equal(622592, standing.Hero.PosZ);
    }

    // -----------------------------------------------------------------------------------------
    // UH-2 / UH-3 / SJ-7 - the walking jump and the control in the air.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH2_TheWalkingJumpEast_KeepsTheSpeedOfTheAirAnimation_AndComesBackToTheWalk()
    {
        var rig = FreeRig();
        SteadyWalk(rig);
        var startX = rig.Hero.PosX;
        var startY = rig.Hero.PosY;
        for (var tick = 1; tick <= 24; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            Assert.True(WalkingJumpPosX[tick - 1] == rig.Hero.PosX - startX, $"{label}: posX {rig.Hero.PosX - startX}");
            Assert.Equal(startY, rig.Hero.PosY);
            var expectedForceX = tick == 1 ? 159744 : tick == 2 ? 155136 : tick <= 21 ? 150528 : tick == 22 ? 155136 : 159744;
            Assert.True(expectedForceX == rig.Hero.ForceX, $"{label}: forceX {rig.Hero.ForceX}");
            var expectedAnimation = tick == 1 ? 2u : tick <= 21 ? 44u : 1u;
            Assert.True(expectedAnimation == rig.Hero.CurrentAnimationId, $"{label}: animation {rig.Hero.CurrentAnimationId}");
            if (tick <= 20)
            {
                Assert.Equal(JumpHeights[tick - 1], rig.Hero.PosZ);
            }
        }
    }

    [Fact]
    public void UH2b_TheWalkingJumpDown_HasTheForcesOfTheYAxis()
    {
        var rig = FreeRig();
        SteadyWalk(rig, direction: 0, forceX: 0, forceY: 106496);
        var forceY = new List<int>();
        for (var tick = 1; tick <= 23; tick++)
        {
            rig.Step(Down | Cross, tick == 1 ? Cross : 0);
            forceY.Add(rig.Hero.ForceY);
        }

        var expected = Enumerable.Range(1, 23).Select(t => t == 1 ? 106496 : t == 2 ? 103424 : t <= 21 ? 100352 : t == 22 ? 103424 : 106496).ToArray();
        Assert.Equal(expected, forceY.ToArray());
    }

    [Fact]
    public void UH3a_TheStickReleasedAtTick8_HalvesTheForceThenStops()
    {
        var rig = FreeRig();
        SteadyWalk(rig);
        var startX = rig.Hero.PosX;
        var forceX = new List<int>();
        var animations = new List<uint>();
        var posX = new List<int>();
        for (var tick = 1; tick <= 14; tick++)
        {
            var hold = tick < 8 ? Right | Cross : Cross;
            rig.Step(hold, tick == 1 ? Cross : 0);
            forceX.Add(rig.Hero.ForceX);
            animations.Add(rig.Hero.CurrentAnimationId);
            posX.Add(rig.Hero.PosX - startX);
        }

        Assert.Equal(new[] { 159744, 155136, 150528, 150528, 150528, 150528, 150528, 75264, 0, 0, 0, 0, 0, 0 }, forceX.ToArray());
        Assert.Equal(new uint[] { 2, 44, 44, 44, 44, 44, 44, 45, 45, 45, 45, 45, 45, 45 }, animations.ToArray());
        Assert.Equal(1067520, posX[6]);
        Assert.All(posX.Skip(7), p => Assert.Equal(1142784, p));
    }

    [Fact]
    public void UH3b_TheStickReversedAtTick6_TurnsAroundInTheAir()
    {
        var rig = FreeRig();
        SteadyWalk(rig);
        var startX = rig.Hero.PosX;
        var forceX = new List<int>();
        var posX = new List<int>();
        for (var tick = 1; tick <= 12; tick++)
        {
            var hold = (tick < 6 ? Right : Left) | Cross;
            rig.Step(hold, tick == 1 ? Cross : 0);
            forceX.Add(rig.Hero.ForceX);
            posX.Add(rig.Hero.PosX - startX);
        }

        Assert.Equal(new[] { 159744, 155136, 150528, 150528, 150528, 0, -150528, -150528, -150528, -150528, -150528, -150528 }, forceX.ToArray());
        Assert.Equal(new[] { 159744, 314880, 465408, 615936, 766464, 766464, 615936, 465408, 314880, 164352, 13824, -136704 }, posX.ToArray());
    }

    [Fact]
    public void UH3c_TheStickPressedInTheAirDuringAStandingJump_AcceleratesTowardsTheAirSpeed()
    {
        var rig = FreeRig();
        var startX = rig.Hero.PosX;
        var forceX = new List<int>();
        var animations = new List<uint>();
        var posX = new List<int>();
        for (var tick = 1; tick <= 10; tick++)
        {
            var hold = tick >= 5 ? Right | Cross : Cross;
            rig.Step(hold, tick == 1 ? Cross : 0);
            forceX.Add(rig.Hero.ForceX);
            animations.Add(rig.Hero.CurrentAnimationId);
            posX.Add(rig.Hero.PosX - startX);
            Assert.Equal(JumpHeights[tick - 1], rig.Hero.PosZ);
        }

        Assert.Equal(new[] { 0, 0, 0, 0, 75264, 150528, 150528, 150528, 150528, 150528 }, forceX.ToArray());
        Assert.Equal(new uint[] { 43, 45, 45, 45, 44, 44, 44, 44, 44, 44 }, animations.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0, 75264, 225792, 376320, 526848, 677376, 827904 }, posX.ToArray());
    }

    [Fact]
    public void SJ7_TheHorizontalForces_FromRest_AndFromTheWalk()
    {
        var rest = FreeRig();
        var forces = new List<int>();
        var targets = new List<uint>();
        for (var frame = 1; frame <= 21; frame++)
        {
            rest.Step(Right | Cross, frame == 1 ? Cross : 0);
            forces.Add(rest.Hero.ForceX);
            targets.Add(rest.Hero.TargetAnimationId);
        }

        Assert.Equal(new[] { 79872, 115200, 150528, 150528 }, forces.Take(4).ToArray());
        Assert.Equal(2u, targets[0]);
        Assert.All(targets.Skip(1), t => Assert.Equal(44u, t));
    }

    // -----------------------------------------------------------------------------------------
    // UH-7 - the fall from a ledge (S2), UH-8 - the water and the ice.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH7_WalkingOffALedgeOf16Pixels_FallsThroughTheAirStateFromTheFirstTrustedHeadPull()
    {
        var field = FlatCells.Create(cell: (cx, _) => (0, cx < 6 ? 1 : 0));
        var rig = FreeRig(field, x: 120f, z: 16f);
        SteadyWalk(rig);
        Assert.Equal(1048576, rig.Hero.PosZ);
        var startX = rig.Hero.PosX;
        var expectedPosZ = new[] { 1015808, 950272, 851968, 720896, 557056, 360448, 131072 };
        var expectedPosX = new[] { 2236416, 2391552, 2542080, 2692608, 2843136, 2993664, 3144192, 3294720, 3445248, 3600384, 3760128 };
        for (var tick = 1; tick <= 24; tick++)
        {
            rig.Step(Right, 0);
            var label = $"tick {tick}";
            var hero = rig.Hero;
            if (tick >= 14)
            {
                Assert.True(expectedPosX[tick - 14] == hero.PosX - startX, $"{label}: posX {hero.PosX - startX}");
            }

            if (tick <= 14)
            {
                Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CurrentAnimationId == 1u, $"{label}: animation {hero.CurrentAnimationId}");
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}"); // after tick 14 too: the head pull sees the position before the tick.
            }
            else if (tick <= 21)
            {
                Assert.True(hero.PosZ == expectedPosZ[tick - 15], $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CurrentAnimationId == 44u, $"{label}: animation {hero.CurrentAnimationId}");
                Assert.True(hero.ForceZ == -32768 * (tick - 14), $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.IsOnGround == 0, $"{label}: onGround {hero.IsOnGround}");
                Assert.Equal(0, hero.CollidedWithEntityZ);
                Assert.True(hero.ForceX == (tick == 15 ? 155136 : 150528), $"{label}: forceX {hero.ForceX}");
            }
            else if (tick == 22)
            {
                Assert.Equal(0, hero.PosZ);
                Assert.Equal(1, hero.CollidedWithEntityZ);
                Assert.Equal(1, hero.IsOnGround);
                Assert.Equal(150528, hero.ForceX);
            }
            else
            {
                Assert.Equal(0, hero.PosZ);
                Assert.True(hero.CurrentAnimationId == 1u, $"{label}: animation {hero.CurrentAnimationId}");
                Assert.True(hero.ForceX == (tick == 23 ? 155136 : 159744), $"{label}: forceX {hero.ForceX}");
            }
        }
    }

    // UH-7b (E19.m10, M-16): a hero WALKING east from a flat cell of 16 px onto a cell of slope kind 1 meets a constant step fixed by its y (the ground of the slope is
    // height * 16 - (y mod 16), without a term in x: AlundraCellsCollisionField.cs:382-391, binary 0x8003722C-0x80037264). The binary glues a descent of 3 px or less back to the
    // ground when Gravity is set, ForceZ is 0 and the XY step is not null (0x80037848, 0x800377A0-0x800377A8); the montage gives the step 3 px at y 107.5 and 5 px at y 109.5.
    // 4 px is never tested: it is the gap O-E19-40 between the engine snap and the binary.
    private static JumpHeroRig DescentRig(int drop, float groundSnapDistance)
    {
        var field = FlatCells.Create(cell: (_, _) => (0, 1), slope: (cx, _) => cx >= 6 ? 1 : 0);
        var rig = JumpHeroRig.Build(field, x: 120f, y: 104.5f + drop, z: 16f, freePad: true, groundSnapDistance: groundSnapDistance);
        SteadyWalk(rig);
        Assert.Equal(1048576, rig.Hero.PosZ);
        Assert.Equal(1048576, rig.Hero.ComputeTerrainHeight()); // the self-check of the terrain: the flat cell, 16 px.
        return rig;
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void UH7b_ADescentOf3PixelsWhileWalking_DoesNotFall_AndAFall5PixelsDoes(int drop)
    {
        var rig = DescentRig(drop, -1f);
        var hero = rig.Hero;
        var groundBeyond = 1048576 - drop * 65536;
        var landedAt = -1;
        for (var tick = 1; tick <= 24; tick++)
        {
            rig.Step(Right, 0);
            var label = $"tick {tick}";
            if (tick == 24)
            {
                Assert.True(hero.ComputeTerrainHeight() == groundBeyond, $"{label}: terrain {hero.ComputeTerrainHeight()}"); // the self-check after the walk.
            }

            if (drop == 3)
            {
                Assert.False(hero.HeroAirborne, label);
                Assert.True(hero.TargetAnimationId == 1u, $"{label}: target {hero.TargetAnimationId}");
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
                Assert.True(hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
                Assert.True(hero.ForceX == 159744, $"{label}: forceX {hero.ForceX}");
                Assert.True(hero.PosZ == (tick <= 14 ? 1048576 : groundBeyond), $"{label}: posZ {hero.PosZ}");
            }
            else if (tick <= 14)
            {
                Assert.True(hero.CurrentAnimationId == 1u && hero.PosZ == 1048576 && hero.IsOnGround == 1, $"{label}: animation {hero.CurrentAnimationId} posZ {hero.PosZ}");
            }
            else
            {
                if (tick == 15)
                {
                    Assert.True(hero.CurrentAnimationId == 44u && hero.IsOnGround == 0 && hero.PosZ < 1048576, $"{label}: animation {hero.CurrentAnimationId} posZ {hero.PosZ}");
                }

                if (landedAt < 0 && hero.PosZ == groundBeyond && hero.IsOnGround == 1 && hero.CollidedWithEntityZ == 1)
                {
                    landedAt = tick;
                }
            }
        }

        if (drop == 5)
        {
            Assert.Equal(19, landedAt);
            Assert.Equal(groundBeyond, hero.PosZ);
        }
    }

    [Fact]
    public void UH7b_Control_TheSame3PixelsWithoutTheEngineSnap_Fall_SoTheGreenCaseComesFromTheSnapAlone()
    {
        var rig = DescentRig(3, 0f);
        var hero = rig.Hero;
        for (var tick = 1; tick <= 14; tick++)
        {
            rig.Step(Right, 0);
            Assert.False(hero.HeroAirborne, $"tick {tick}");
        }

        rig.Step(Right, 0);
        Assert.True(hero.HeroAirborne, "tick 15: airborne");
        Assert.True(hero.CurrentAnimationId == 44u, $"tick 15: animation {hero.CurrentAnimationId}");
        Assert.True(hero.PosZ < 1048576, $"tick 15: posZ {hero.PosZ}");
    }

    [Fact]
    public void UH8_TheStandingJumpFromWater_IsTheLowerJump_BootsGiveTheNormalOne()
    {
        var heights = new[] { 204800, 376832, 516096, 622592, 696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496 };
        var water = FlatCells.Create(cell: (_, _) => (0x18, 0));
        var rig = FreeRig(water);
        Assert.Equal(0x18u, rig.Hero.CombinedVramFlagsOR & 0x18u);
        var vram = new List<uint>();
        for (var tick = 1; tick <= 14; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            if (tick == 1)
            {
                Assert.Equal(204800, rig.Hero.ForceZ);
            }

            if (tick <= 13)
            {
                Assert.Equal(heights[tick - 1], rig.Hero.PosZ);
                Assert.Equal(0, rig.Hero.IsOnGround);
            }

            vram.Add(rig.Hero.CombinedVramFlagsOR & 0x18u);
        }

        Assert.Equal(0, rig.Hero.PosZ);
        Assert.Equal(1, rig.Hero.IsOnGround);
        Assert.Equal(1, rig.Hero.CollidedWithEntityZ);
        Assert.All(vram.Take(13), v => Assert.Equal(0u, v));
        Assert.Equal(0x18u, vram[13]);

        var boots = FreeRig(FlatCells.Create(cell: (_, _) => (0x18, 0)));
        boots.Host.GameState.NumberOfItems[0x1A * 2 + 1] = 1;
        for (var tick = 1; tick <= 20; tick++)
        {
            boots.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            Assert.Equal(JumpHeights[tick - 1], boots.Hero.PosZ);
        }
    }

    [Fact]
    public void UH8b_OnIce_TheWalkAccelerates4992PerTick_AndTheJumpGivesTheNormalAirSpeedAtOnce()
    {
        var ice = FlatCells.Create(cell: (_, _) => (0x20, 0));
        var walk = FreeRig(ice);
        var forces = new List<int>();
        for (var tick = 1; tick <= 32; tick++)
        {
            walk.Step(Right, 0);
            forces.Add(walk.Hero.ForceX);
        }

        Assert.Equal(4992, forces[0]);
        Assert.Equal(154752, forces[30]);
        Assert.Equal(159744, forces[31]);

        // In the air the flags are 0: the air speed is reached by the normal steps (the tick after the take-off).
        var jump = FreeRig(FlatCells.Create(cell: (_, _) => (0x20, 0)));
        SteadyWalk(jump);
        var air = new List<int>();
        for (var tick = 1; tick <= 3; tick++)
        {
            jump.Step(Right | Cross, tick == 1 ? Cross : 0);
            air.Add(jump.Hero.ForceX);
        }

        Assert.Equal(new[] { 159744, 155136, 150528 }, air.ToArray());
    }

    // -----------------------------------------------------------------------------------------
    // SJ-9 - the freeze in the air, SJ-11 - the ramp, SJ-17 - the live gravity.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SJ9_AFreezeInFlight_KeepsTheHeroWhereHeIs_AndTheFlightResumesAtTheNextValue()
    {
        var rig = FreeRig(groundSnapDistance: 64f); // a frame without the external vertical would put the root back on the ground.
        for (var tick = 1; tick <= 8; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
        }

        var hero = rig.Hero;
        Assert.Equal(JumpHeights[7], hero.PosZ);
        var forceZ = hero.ForceZ;

        rig.Host.GameState.PlayerControlFlags = GameState.PlayerControlBits.MenuOpen;
        GameplayFreeze.Apply(hero, gameplayBlocked: true);
        for (var frame = 0; frame < 5; frame++)
        {
            rig.Step();
            Assert.Equal(JumpHeights[7], hero.PosZ);
            Assert.Equal(forceZ, hero.ForceZ);
            Assert.True(rig.Controller.IsVerticalOwnedExternally);
        }

        rig.Host.GameState.PlayerControlFlags = 0;
        GameplayFreeze.Apply(hero, gameplayBlocked: false);
        Assert.True(rig.Controller.IsVerticalOwnedExternally);
        rig.Step();
        Assert.Equal(JumpHeights[8], hero.PosZ);
        Assert.Equal(JumpHeights[8] / 65536f, rig.RootZ, 3);
    }

    [Fact]
    public void SJ11_WalkingDownARamp_At2TicksPerFrame_NeverTurnsIntoAFall()
    {
        // A ramp of 1:1 going down to the south (slope kind 1: the ground falls by one pixel per pixel of y), 16 px per cell.
        var ramp = FlatCells.Create(cell: (_, cy) => (0, Math.Max(1, 30 - cy)), slope: (_, _) => 1);
        var rig = FreeRig(ramp, x: 200.25f, z: 0f);
        var terrain = rig.Hero.ComputeTerrainHeight();
        rig.Hero.PosZ = terrain;
        rig.Hero.PushLogicalPositionToRoot();
        rig.Step();
        rig.Step();
        Assert.Equal(1, rig.Hero.IsOnGround);

        var targets = new List<uint>();
        var startZ = rig.Hero.PosZ;
        for (var frame = 1; frame <= 30; frame++)
        {
            rig.Step(Down, 0, 0.04f);
            targets.Add(rig.Hero.TargetAnimationId);
        }

        Assert.All(targets, t => Assert.Equal(1u, t));
        Assert.True(rig.Hero.PosZ < startZ, "the hero went down the ramp");
        Assert.False(rig.Hero.HeroAirborne);
    }

    [Theory]
    [InlineData(false, 1250f)]
    [InlineData(true, 0f)]
    public void SJ17_TheLiveGravityOfTheEngine_IsGivenBackAfterThePadJump_ALowGravityOpcodeBeforeIncluded(bool lowGravityOpcode, float gravityAfter)
    {
        var rig = FreeRig();
        if (lowGravityOpcode)
        {
            var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x17, 0xFF } };
            var runner = new EventProgramRunner(document, new GameState(), null);
            runner.RunOneScriptCall(rig.Hero, new EventProgramState { Codes = document.CodesAsBytes() });
            Assert.Equal(0f, rig.Controller.Settings.Gravity);
        }

        for (var tick = 1; tick <= 24; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            if (tick <= 21)
            {
                Assert.True(rig.Hero.HeroAirborne, $"tick {tick}: the hero took off and is in the state"); // the precondition of the restitution below.
            }
        }

        Assert.Equal(0, rig.Hero.PosZ);
        Assert.False(rig.Hero.HeroAirborne);
        Assert.Equal(gravityAfter, rig.Controller.Settings.Gravity);
    }

    // -----------------------------------------------------------------------------------------
    // S6 - no step tolerance in the air.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S6_TheStepHeightIsZeroWhileTheForceIsNotZero_AndTheCapturedValueGoesBackAtTheLanding()
    {
        var rig = FreeRig();
        var captured = rig.Controller.Settings.StepHeight;
        Assert.True(captured > 0f);
        for (var tick = 1; tick <= 22; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            if (tick <= 21)
            {
                Assert.Equal(rig.Hero.ForceZ == 0 ? captured : 0f, rig.Controller.Settings.StepHeight);
            }
        }

        Assert.Equal(captured, rig.Controller.Settings.StepHeight);
        Assert.False(rig.Hero.HeroAirborne);
    }

    [Fact]
    public void S6b_TheStepHeightGoesBack_AfterAFreezeAndAThawInTheAirAndTheLandingThatFollows()
    {
        var rig = FreeRig(groundSnapDistance: 64f);
        var captured = rig.Controller.Settings.StepHeight;
        for (var tick = 1; tick <= 5; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
        }

        Assert.Equal(0f, rig.Controller.Settings.StepHeight);
        rig.Host.GameState.PlayerControlFlags = GameState.PlayerControlBits.MenuOpen;
        GameplayFreeze.Apply(rig.Hero, gameplayBlocked: true);
        rig.Step();
        Assert.Equal(0f, rig.Controller.Settings.StepHeight);
        rig.Host.GameState.PlayerControlFlags = 0;
        GameplayFreeze.Apply(rig.Hero, gameplayBlocked: false);
        for (var frame = 0; frame < 40 && rig.Hero.HeroAirborne; frame++)
        {
            rig.Step();
        }

        Assert.False(rig.Hero.HeroAirborne);
        Assert.Equal(captured, rig.Controller.Settings.StepHeight);
    }

    [Fact]
    public void S6c_TheAdoptionOfANewPawnInTheAir_GivesTheEngineValuesBack()
    {
        var spec = new ArcSpec("S6c", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        arc.OneFrame();
        var hero = arc.Hero;
        var captured = hero.Controller!.Settings.StepHeight;
        Assert.True(captured > 0f);

        hero.TargetAnimationId = 43;
        for (var frame = 0; frame < 3; frame++)
        {
            arc.OneFrame();
        }

        Assert.True(hero.HeroAirborne);
        Assert.Equal(0f, hero.Controller!.Settings.StepHeight);

        // The same re-adoption as UJ-7 (the pending arrival consumed by AdoptPlayerPawn of the world).
        WarpDirector.Instance.ResetForTests();
        WarpDirector.Instance.SetPendingArrivalForTests(179, (17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, GameState.ResetAnimationId, 0);
        var tileMapData = (CasaEngine.Framework.Assets.TileMap.TileMapData)typeof(WorldProxy)
            .GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!;
        typeof(WorldProxy).GetMethod("AdoptPlayerPawn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(arc.Proxy, new object[] { arc.RealWorld!, tileMapData });
        WarpDirector.Instance.ResetForTests();

        Assert.False(hero.HeroAirborne);
        Assert.Equal(captured, hero.Controller!.Settings.StepHeight);
        Assert.False(hero.Controller!.IsVerticalOwnedExternally);
    }

    // -----------------------------------------------------------------------------------------
    // E19.d2c2 F4 - the climb takes the vertical from the state held by the tick.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UJClimb_AJumpNorthAgainstALadderWall_EndsTheStateAtTheEntryOfTheClimb_AndTheClimbHoldsTheEngineValuesUntilItsExit()
    {
        // Ladder cells (ground property 12: the slope 6) at height 0 up to row 5, a wall (height 36 cells) from row 4 up: the hero jumps north with Up held, is
        // stopped by the wall in the air, and at tick 21 its foot is exactly at the ground (the strict landing is the next tick) while the ladder slope reads 6.
        var field = FlatCells.Create(cell: (_, cy) => (0, cy <= 4 ? 36 : 0), groundProperty: (_, _) => 12);
        var rig = FreeRig(field);
        var hero = rig.Hero;
        var captured = rig.Controller.Settings.StepHeight;
        Assert.Equal(3f, captured);
        var entryFrame = 0;
        for (var frame = 1; frame <= 40 && entryFrame == 0; frame++)
        {
            rig.Step(PadState.Up | (frame == 1 ? Cross : 0), frame == 1 ? Cross : 0);
            if (hero.TargetAnimationId == PlayerManager.ClimbingAnimationId)
            {
                entryFrame = frame;
            }
        }

        Assert.True(entryFrame > 0, "the climb was never entered");
        Assert.Equal(22, entryFrame);
        Assert.False(hero.HeroAirborne);
        Assert.Equal(0f, rig.Controller.Settings.Gravity);
        Assert.Equal(0f, rig.Controller.Settings.MaxFallSpeed);
        Assert.True(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(captured, rig.Controller.Settings.StepHeight); // no rule of the climb touches it: the captured value.

        for (var frame = 0; frame < 3; frame++)
        {
            rig.Step(PadState.Up, 0);
            Assert.Equal(PlayerManager.ClimbingAnimationId, hero.TargetAnimationId);
            Assert.False(hero.HeroAirborne);
            Assert.True(rig.Controller.IsVerticalOwnedExternally);
        }

        // The lateral exit of the ladder gives the map's gravity and the vertical back.
        rig.Step(Right, 0);
        Assert.Equal(0u, hero.TargetAnimationId);
        Assert.Equal(JumpHeroRig.MapGravity, rig.Controller.Settings.Gravity);
        Assert.Equal(800f, rig.Controller.Settings.MaxFallSpeed);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
        Assert.False(hero.HeroAirborne);
    }
}
