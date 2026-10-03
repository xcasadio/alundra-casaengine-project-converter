#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c2 D3 (docs/plan-e19-opcodes.md §1.2h.3.2, S3, S4, S6): the hero lands on the tops of objects and rides what carries it, on
/// <see cref="JumpHeroRig"/> with a free pad (the real hero controller settings, a field of synthetic cells, the animation sets of the export). Values of
/// annex B.1 (table S-C, UH-6, UH-10, UH-11) and B.2.2 (SJ-12, SJ-13) as the plan restates them (C.saut-binaire and C.saut-dll win); UH-14 is the
/// plan's own. One tick per frame. The entities of the montages carry their <see cref="AlundraEntityScriptProxy.LogicContextEntity"/> (what
/// <c>RidingEntity</c> holds, S4).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroObjectTopsTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Cross = AlundraPadState.Cross;
    private const uint Right = AlundraPadState.Right;
    private const int East = 24;

    private static readonly int[] JumpHeights =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
        1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680,
    };

    // Table S-C: PosX minus the start, ticks 1 to 26 (the same for the chest of UH-10).
    private static readonly int[] StepJumpPosX =
    {
        159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1218048, 1368576, 1519104, 1669632, 1820160, 1970688, 2121216, 2271744, 2422272, 2572800,
        2723328, 2878464, 3038208, 3197952, 3357696, 3517440, 3677184, 3836928, 3996672,
    };

    public AlundraHeroObjectTopsTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    /// <summary>Terrain cells of 16 px from x = 144 px (the cells are 24 px wide: from the sixth), flat 0 before.</summary>
    private static AlundraCellsCollisionField StepField() => FlatCells.Create(cell: (cx, _) => (0, cx >= 6 ? 1 : 0));

    private static void SteadyWalk(JumpHeroRig rig)
    {
        rig.Hero.TargetAnimationId = 1;
        rig.Hero.TargetDirection = East;
        rig.Hero.ForceX = 159744;
        rig.Hero.ForceY = 0;
    }

    /// <summary>A collidable object of the montage (the box of the original given by offset and size), with its logic entity set.</summary>
    private static AlundraEntityScriptProxy AddObject(World world, ContactHost host, string name, int x, int y, int sizeX, int sizeY, int sizeZ, int offsetX = 0, int offsetY = 0)
    {
        var obj = ContactWorld.AddEntity(world, host, name, x, y, 0, offsetX, offsetY, 0, sizeX, sizeY, sizeZ);
        obj.LogicContextEntity = obj.OwnerEntity!;
        return obj;
    }

    private sealed class PlayerContext : IEntityWorldContext
    {
        public required AlundraEntityScriptProxy Player { get; init; }

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public AlundraEntityScriptProxy? PlayerEntity => Player;

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }

        public NavigationGrid2D? NavigationGrid => null;
    }

    // -----------------------------------------------------------------------------------------
    // UH-6 - the jump onto a step of the terrain of 16 px (table S-C), and the variants of the front edge (S6).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH6_TheWalkingJumpOntoAStepOf16Pixels_LandsAtTick18_ThenWalksOnIt()
    {
        var rig = JumpHeroRig.Build(StepField(), x: 120f, freePad: true);
        SteadyWalk(rig);
        var hero = rig.Hero;
        var startX = hero.PosX;
        var forceX = new List<int>();
        for (var tick = 1; tick <= 26; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            Assert.True(StepJumpPosX[tick - 1] == hero.PosX - startX, $"{label}: posX {hero.PosX - startX}");
            forceX.Add(hero.ForceX);
            if (tick <= 17)
            {
                Assert.True(JumpHeights[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.IsOnGround == 0, $"{label}: onGround {hero.IsOnGround}"); // never a grounded frame in the flight.
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else
            {
                Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
            }

            if (tick == 1)
            {
                Assert.Equal(2u, hero.CurrentAnimationId);
            }
            else if (tick <= 18)
            {
                Assert.True(hero.CurrentAnimationId == 44u, $"{label}: animation {hero.CurrentAnimationId}"); // 44 up to the tick of the landing included.
            }
            else
            {
                Assert.True(hero.CurrentAnimationId == 1u, $"{label}: animation {hero.CurrentAnimationId}");
            }

            if (tick == 6)
            {
                Assert.Equal(1474560, hero.PosZ); // the front edge enters the cells at this tick, 22.5 px high.
            }
        }

        Assert.Equal(0, hero.ForceZ);
        var expectedForceX = Enumerable.Range(1, 26).Select(t => t == 1 ? 159744 : t == 2 ? 155136 : t <= 18 ? 150528 : t == 19 ? 155136 : 159744).ToArray();
        Assert.Equal(expectedForceX, forceX.ToArray());
    }

    [Fact]
    public void UH6_TheLandingTick_HasTheForceAtZero_TheEntityFlagAtOne_AndTheNextTicksStayOnTheStep()
    {
        var rig = JumpHeroRig.Build(StepField(), x: 120f, freePad: true);
        SteadyWalk(rig);
        for (var tick = 1; tick <= 18; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
        }

        Assert.Equal(1048576, rig.Hero.PosZ);
        Assert.Equal(0, rig.Hero.ForceZ);
        Assert.Equal(1, rig.Hero.CollidedWithEntityZ);
        Assert.Equal(1, rig.Hero.IsOnGround);
        for (var tick = 19; tick <= 24; tick++)
        {
            rig.Step(Right | Cross, 0);
            Assert.Equal(1048576, rig.Hero.PosZ);
        }
    }

    [Theory]
    [InlineData(125f, new int[0], 195.734)]
    [InlineData(126f, new[] { 3 }, 196.633)]
    [InlineData(127f, new[] { 3 }, 196.633)]
    [InlineData(128f, new[] { 3 }, 196.633)]
    [InlineData(129f, new[] { 2, 3 }, 196.633)]
    [InlineData(130f, new[] { 2, 3 }, 196.633)]
    // 131 to 133: the table of the annex says {2, 3} without saying what "blocked" means (no definition reconciles all its rows). At x0 >= 131 the front edge is 2 px or
    // less from the step: the first step of tick 1 (foot at 5 px) already reaches the contact (x = 133.0, advance to the contact of E19.a2) or starts from it (133), and the
    // rule of the binary (cell above the foot, S6) blocks that tick necessarily. The DLL's measure is pinned: {1, 2, 3}; the final position is the same. To check against the
    // binary with E19.h.
    [InlineData(131f, new[] { 1, 2, 3 }, 196.633)]
    [InlineData(132f, new[] { 1, 2, 3 }, 196.633)]
    [InlineData(133f, new[] { 1, 2, 3 }, 196.633)]
    public void UH6_TheFrontEdgeEnteringTheStepBelowItsHeight_IsStopped_AtTheTicksOfTheTable_AndTheHeroEndsAtTheSamePlace(float x0, int[] blockedTicks, double finalX)
    {
        var rig = JumpHeroRig.Build(StepField(), x: x0, freePad: true);
        SteadyWalk(rig);
        var hero = rig.Hero;
        var blocked = new List<int>();
        var previousX = hero.PosX;
        for (var tick = 1; tick <= 30; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            if (tick <= 17)
            {
                // The invariant of S6: in flight never grounded, and the front edge is not in the 16 px cells (x >= 144) while the foot is below the step.
                Assert.True(hero.IsOnGround == 0, $"tick {tick}: onGround {hero.IsOnGround}");
                var frontEdgePixel = (hero.PosX + hero.ModX + hero.Width) >> 16;
                Assert.True(hero.PosZ >= 1048576 || frontEdgePixel < 144, $"tick {tick}: front edge {frontEdgePixel} at posZ {hero.PosZ}");
            }

            if (tick <= 6 && hero.PosX - previousX < hero.ForceX)
            {
                blocked.Add(tick);
                Assert.True(hero.PosZ < 1048576, $"tick {tick}: stopped only while the foot is below the step");
            }

            previousX = hero.PosX;
        }

        Assert.Equal(blockedTicks, blocked.ToArray());
        Assert.True(Math.Abs(hero.PosX / 65536.0 - finalX) <= 0.05, $"x at tick 30: {hero.PosX / 65536.0}");
    }

    // -----------------------------------------------------------------------------------------
    // UH-10 / UH-11 - the jump onto a chest (S3, S4).
    // -----------------------------------------------------------------------------------------

    private static (JumpHeroRig Rig, AlundraEntityScriptProxy Chest) ChestRig()
    {
        AlundraEntityScriptProxy? chest = null;
        var rig = JumpHeroRig.Build(
            probeFactory: host => new AlundraMovementObstacleProbe(host),
            configure: (world, host) => chest = AddObject(world, host, "Chest", 144, 92, 24, 16, 16),
            x: 120f,
            freePad: true);
        SteadyWalk(rig);
        return (rig, chest!);
    }

    [Fact]
    public void UH10_TheWalkingJumpOntoAChest_LandsAtTick18_RestsOnItUntilTick24_AndFallsOffItAtTick26()
    {
        var (rig, chest) = ChestRig();
        var hero = rig.Hero;
        var startX = hero.PosX;
        for (var tick = 1; tick <= 26; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick <= 20)
            {
                Assert.True(StepJumpPosX[tick - 1] == hero.PosX - startX, $"{label}: posX {hero.PosX - startX}");
            }

            if (tick <= 17)
            {
                Assert.True(JumpHeights[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.IsOnGround == 0, $"{label}: onGround {hero.IsOnGround}");
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}");
            }
            else if (tick == 18)
            {
                Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
                Assert.Equal(0, hero.ForceZ);
                Assert.Equal(1, hero.CollidedWithEntityZ);
                Assert.Equal(1, hero.IsOnGround);
                Assert.Equal(44u, hero.CurrentAnimationId);
            }
            else if (tick <= 24)
            {
                Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: collided {hero.CollidedWithEntityZ}"); // carried, not landed again.
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
                Assert.True(hero.CurrentAnimationId == 1u, $"{label}: animation {hero.CurrentAnimationId}");
            }

            if (tick >= 19 && tick <= 25)
            {
                Assert.Same(chest.LogicContextEntity, hero.RidingEntity);
            }
        }

        // Tick 25: the hero's box has left the chest (IsOnGround 0), tick 26: the fall of 16 px starts.
        Assert.Null(hero.RidingEntity);
        Assert.Equal(44u, hero.CurrentAnimationId);
        Assert.Equal(-32768, hero.ForceZ);
        Assert.Equal(1015808, hero.PosZ);
        Assert.Equal(0, hero.IsOnGround);
    }

    [Fact]
    public void UH10_Tick25_TheHeroLeavesTheChest_IsOnGroundIsZero_AndTheFallLandsOnTheTerrain()
    {
        var (rig, _) = ChestRig();
        var hero = rig.Hero;
        for (var tick = 1; tick <= 25; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
        }

        Assert.Equal(1048576, hero.PosZ);
        Assert.Equal(0, hero.IsOnGround);
        for (var tick = 26; tick <= 40 && hero.HeroAirborne; tick++)
        {
            rig.Step(Right | Cross, 0);
        }

        // The support is the terrain again: the hero is out of the state held by the tick, on the ground.
        Assert.False(hero.HeroAirborne);
        Assert.Equal(0, hero.PosZ);
        Assert.Equal(1, hero.IsOnGround);
        Assert.False(rig.Controller.IsVerticalOwnedExternally);
        Assert.Equal(3f, rig.Controller.Settings.StepHeight);
    }

    [Fact]
    public void UH10_WithoutTheJump_TheHeroIsStoppedAgainstTheChest_AtX133()
    {
        var (rig, chest) = ChestRig();
        var hero = rig.Hero;
        for (var tick = 1; tick <= 12; tick++)
        {
            rig.Step(Right, 0);
            if (tick >= 6)
            {
                Assert.Same(chest, hero.XCollisionEntity);
                Assert.True(Math.Abs(hero.PosX / 65536.0 - 133.0) < 0.01, $"tick {tick}: x {hero.PosX / 65536.0}");
            }
        }

        Assert.Equal(0, hero.PosZ);
    }

    [Fact]
    public void UH11_TheHeroRestingOnTheChest_RidesIt_AndTheOpcode0x3EAnswersOne_ItDoesNotAnswerOneForAnotherEntity()
    {
        var (rig, chest) = ChestRig();
        var hero = rig.Hero;
        for (var tick = 1; tick <= 20; tick++)
        {
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
        }

        Assert.Same(chest.LogicContextEntity, hero.RidingEntity);
        var context = new PlayerContext { Player = hero };
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x3E, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);

        var onChest = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };
        runner.RunOneScriptCall(chest, onChest);
        Assert.Equal(1, onChest.Result);

        var other = new EventProgramState { Codes = document.CodesAsBytes(), Result = 1 };
        runner.RunOneScriptCall(rig.Hero, other); // the hero's own logic entity is not what it rides.
        Assert.Equal(0, other.Result);
    }

    // -----------------------------------------------------------------------------------------
    // SJ-12 / SJ-13 - the support of an object under the foot, from the jump of the pad.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SJ12_AStandingJumpInsideTheFootprintOfACrateOf16Pixels_LandsOnItsTop_AndFallsWhenMovedOutOfItsFootprint()
    {
        var rig = JumpHeroRig.Build(configure: (world, host) => AddObject(world, host, "Crate", 190, 90, 30, 24, 16), freePad: true);
        var hero = rig.Hero;
        for (var tick = 1; tick <= 26; tick++)
        {
            rig.Step(tick == 1 ? Cross : 0, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick <= 17)
            {
                Assert.True(JumpHeights[tick - 1] == hero.PosZ, $"{label}: posZ {hero.PosZ}");
            }
            else
            {
                Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
                Assert.True(hero.CollidedWithEntityZ == (tick == 18 ? 1 : 0), $"{label}: collided {hero.CollidedWithEntityZ}");
                Assert.True(tick == 18 ? hero.ForceZ == 0 : hero.ForceZ == 0, $"{label}: forceZ {hero.ForceZ}");
            }
        }

        // The hero is moved out of the footprint after tick 26: the fall of the next ticks is the one of the gravity from rest.
        rig.Controller.Move(new Vector3(100f, 0f, 0f));
        var expected = new[] { 1015808, 950272, 851968, 720896 };
        for (var tick = 27; tick <= 30; tick++)
        {
            rig.Step();
            Assert.True(expected[tick - 27] == hero.PosZ, $"tick {tick}: posZ {hero.PosZ}");
        }
    }

    [Fact]
    public void SJ13_AWalkingJumpAgainstATombOf25Pixels_PassesOverItWhileTheFootIsHighEnough_AndLandsOnItAtTick14()
    {
        AlundraEntityScriptProxy? tomb = null;
        var rig = JumpHeroRig.Build(
            probeFactory: host => new AlundraMovementObstacleProbe(host),
            configure: (world, host) => tomb = AddObject(world, host, "Tomb", 140, 100, 20, 14, 25, offsetX: -10, offsetY: -7),
            x: 100.25f,
            freePad: true);
        var hero = rig.Hero;
        for (var update = 0; update < 60 && !ReferenceEquals(hero.XCollisionEntity, tomb); update++)
        {
            rig.Step(Right, 0);
        }

        Assert.Same(tomb, hero.XCollisionEntity);
        rig.Step(Right, 0);
        rig.Step(Right, 0);

        for (var tick = 1; tick <= 19; tick++)
        {
            var before = hero.PosX;
            rig.Step(Right | Cross, tick == 1 ? Cross : 0);
            var label = $"tick {tick}";
            if (tick <= 7)
            {
                Assert.True(hero.PosX - before < 4096, $"{label}: blocked, the step is at most the contact ({hero.PosX - before})");
                Assert.True(hero.PosZ < 1638400, $"{label}: posZ {hero.PosZ}");
            }
            else if (tick <= 13)
            {
                Assert.True(hero.PosZ >= 1638400, $"{label}: posZ {hero.PosZ}");
                Assert.True(hero.PosX - before > 140000, $"{label}: free, the step is the force of the tick ({hero.PosX - before})");
            }
            else if (tick == 14)
            {
                Assert.True(hero.PosZ == 1638400, $"{label}: posZ {hero.PosZ}"); // 25 px, not the 1605632 of the free flight.
                Assert.Equal(0, hero.ForceZ);
                Assert.Equal(1, hero.CollidedWithEntityZ);
                Assert.Equal(1, hero.IsOnGround);
            }
            else
            {
                Assert.True(hero.PosZ == 1638400, $"{label}: posZ {hero.PosZ}"); // five ticks on it.
                Assert.Same(tomb!.LogicContextEntity, hero.RidingEntity);
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // UH-14 - a platform that moves by 1 px per tick carries the hero (S4).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UH14_APlatformMovingByOnePixelPerTick_CarriesTheHeroOneTickOfPhaseLater_AndDoesNotLetItSlideOffIn50Ticks()
    {
        var rig = JumpHeroRig.Build(x: 156.25f, freePad: true);
        // The platform joins the world after the hero (as in production: the hero is updated before it), under the hero: the top of 16 px. Its logical box
        // (x 144..168, y 92..108) is offset from its root so that the physical box of the montage (18 x 12 x 32 around the root) never meets the hero's body
        // while it moves west.
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 24, 16, 16, offsetX: 44);
        var hero = rig.Hero;
        hero.PosZ = 1048576;
        hero.PushLogicalPositionToRoot();
        for (var settle = 0; settle < 4; settle++)
        {
            rig.Step();
        }

        Assert.Equal(1048576, hero.PosZ);
        Assert.Same(platform.LogicContextEntity, hero.RidingEntity);

        // The force of the platform held at 1 px per tick (no speed from the animation sets, so no recomputation of the target).
        platform.TargetDirection = platform.CurrentDirection;
        platform.TargetForceX = -65536;
        platform.ForceX = -65536;
        platform.ForceStepX = 0;
        var heroStart = hero.PosX;
        var platformStart = platform.PosX;
        for (var tick = 1; tick <= 50; tick++)
        {
            rig.Step();
            var label = $"tick {tick}";
            Assert.True(platform.PosX - platformStart == -tick * 65536, $"{label}: platform {platform.PosX - platformStart}");
            Assert.True(hero.PosX - heroStart == -(tick - 1) * 65536, $"{label}: hero {hero.PosX - heroStart}");
            Assert.True(hero.PosZ == 1048576, $"{label}: posZ {hero.PosZ}");
            Assert.True(hero.IsOnGround == 1, $"{label}: onGround {hero.IsOnGround}");
            Assert.True(ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity), $"{label}: riding");
        }
    }
}
