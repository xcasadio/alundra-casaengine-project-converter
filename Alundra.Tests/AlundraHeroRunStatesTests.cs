#nullable enable
using System;
using Alundra.Scripts;
using Xunit;
using Alundra.Session;
using Alundra.Hero;

namespace Alundra.Tests;

/// <summary>
/// E20.d1 T4 (docs/plan-e20-heros.md section 1.13): the branches of the states of the run that the oracle's traces cannot reach, on a bare hero (no controller: the air predicate reads
/// <see cref="AlundraEntityScriptProxy.HeroAirborne"/>), one <see cref="AlundraPlayerManager.MovePlayer"/> call per case, in the order of the binary's handlers (<c>0x80031F54</c> the preparation, <c>0x80031FF4</c> the run,
/// <c>0x80032118</c> the slide, <c>0x80032558</c> the bounce, the common tail <c>0x80031EA8</c>). The answer 0 of the item driver (<see cref="AlundraPlayerManager.TryUseItemResultForTests"/>) is the one thing the
/// stubs of E20.a cannot give. The lines of the plan's "facts" section are the expectations.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraHeroRunStatesTests : IDisposable
{
    private const uint Triangle = AlundraPadState.Triangle;
    private const uint Square = AlundraPadState.Square;
    private const uint Cross = AlundraPadState.Cross;
    private const uint Circle = AlundraPadState.Circle;
    private const uint Up = AlundraPadState.Up;
    private const uint Right = AlundraPadState.Right;
    private const uint Down = AlundraPadState.Down;
    private const uint Left = AlundraPadState.Left;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint AirStill = 0x2D;
    private const uint Prepare = 0x29;
    private const uint Sprint = 3;
    private const uint Stop = 0x2A;
    private const uint Tackle = 4;
    private const uint Bounce = 0x3E;
    private const uint HurtBounce = 0x1C;

    public void Dispose() => AlundraPlayerManager.TryUseItemResultForTests = null;

    private static AlundraEntityScriptProxy Hero(uint animation, uint direction = 0x18, int facing = 3, bool air = false)
    {
        var hero = new AlundraEntityScriptProxy { TargetAnimationId = animation, TargetDirection = direction, AnimationDirection = facing, IsOnGround = air ? 0 : 1 };
        hero.HeroAirborne = air;
        return hero;
    }

    private static void Move(AlundraEntityScriptProxy hero, uint hold, uint pressed = 0, uint released = 0)
    {
        var pad = new AlundraPadState { ButtonsHold = hold, ButtonsJustPressed = pressed, ButtonsReleased = released };
        AlundraPlayerManager.MovePlayer(hero, in pad, new GameState(), host: null);
    }

    // -----------------------------------------------------------------------------------------
    // The entry: the common tail.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Idle)]
    [InlineData(Moving)]
    public void Tail_ATriangleHeld_StartsThePreparation_WithTheDirectionOfTheHero_AndTheEndCounterAtZero(uint start)
    {
        var hero = Hero(start, direction: 0x10);
        hero.AnimCompleteCounter = 7;

        Move(hero, Triangle | Right);

        Assert.Equal(Prepare, hero.TargetAnimationId);
        Assert.Equal(0, hero.AnimCompleteCounter);
        Assert.Equal(0x18u, hero.TargetDirection);
    }

    [Fact]
    public void Tail_TheSquareComesBeforeTheTriangle_TheCrossBeforeToo_TheForbiddenCrossEndsTheTick()
    {
        var swing = Hero(Idle);
        Move(swing, Triangle | Square, pressed: Square);
        Assert.Equal(0x10u, swing.TargetAnimationId);

        var jump = Hero(Idle);
        Move(jump, Triangle | Cross, pressed: Cross);
        Assert.Equal(0x2Bu, jump.TargetAnimationId);

        // VramOR & 0x4000: the case ends before the Triangle test (0x80031EF0): nothing changes.
        var forbidden = Hero(Idle);
        forbidden.CombinedVramFlagsOR = 0x4000;
        Move(forbidden, Triangle | Cross, pressed: Cross);
        Assert.Equal(Idle, forbidden.TargetAnimationId);
    }

    [Fact]
    public void Tail_ATriangleReleased_DoesNotStartTheRun_AndInTheAirTheAirStateWins()
    {
        var edge = Hero(Idle);
        Move(edge, Right); // Triangle not held.
        Assert.Equal(Moving, edge.TargetAnimationId);

        var air = Hero(Moving, air: true);
        Move(air, Triangle | Right);
        Assert.Equal(0x2Cu, air.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // 0x29 - the preparation.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Prepare_TryUseItemAtZero_EndsTheTick_NothingIsWritten()
    {
        AlundraPlayerManager.TryUseItemResultForTests = 0;
        var hero = Hero(Prepare, direction: 0x10);
        hero.AnimCompleteCounter = 1;

        Move(hero, Triangle | Right);

        Assert.Equal(Prepare, hero.TargetAnimationId);
        Assert.Equal(0x10u, hero.TargetDirection);
    }

    [Fact]
    public void Prepare_TheDirectionIsWrittenEveryTick_EvenInTheAir_WhichGives0x2D()
    {
        var ground = Hero(Prepare, direction: 0x10);
        Move(ground, Triangle | Right | Up); // a diagonal: nothing starts, the direction is the diagonal's.
        Assert.Equal(Prepare, ground.TargetAnimationId);
        Assert.Equal(0x14u, ground.TargetDirection);

        var air = Hero(Prepare, direction: 0x10, air: true);
        Move(air, Triangle | Right);
        Assert.Equal(AirStill, air.TargetAnimationId);
        Assert.Equal(0x18u, air.TargetDirection);
    }

    [Fact]
    public void Prepare_TheTriangleReleased_IsIdle_BeforeTheEndCounterIsLooked_At()
    {
        var hero = Hero(Prepare);
        hero.AnimCompleteCounter = 0;

        Move(hero, Right);

        Assert.Equal(Idle, hero.TargetAnimationId);
    }

    [Theory]
    [InlineData(0u, Right, 0x18u)]
    [InlineData(1u, Up, 0x10u)]
    [InlineData(5u, Down, 0x00u)]
    [InlineData(2u, Left, 0x08u)]
    public void Prepare_TheRunStartsOnlyOnceTheFirstLoopHasTurned_InTheDirectionOfTheDashTable(uint counter, uint pad, uint direction)
    {
        var hero = Hero(Prepare, direction: 0x1C);
        hero.AnimCompleteCounter = (int)counter == 0 ? 0 : (int)counter;

        Move(hero, Triangle | pad);

        if (counter == 0)
        {
            Assert.Equal(Prepare, hero.TargetAnimationId);
            Assert.Equal(pad == Right ? 0x18u : pad == Up ? 0x10u : pad == Down ? 0u : 8u, hero.TargetDirection);
        }
        else
        {
            Assert.Equal(Sprint, hero.TargetAnimationId);
            Assert.Equal(direction, hero.TargetDirection);
        }
    }

    [Theory]
    [InlineData(Up | Right)]
    [InlineData(Left | Right)]
    [InlineData(Up | Down)]
    [InlineData(Up | Right | Down)]
    [InlineData(0u)]
    public void Prepare_AnythingButASingleCardinalBit_DoesNotStartTheRun(uint pad)
    {
        var hero = Hero(Prepare);
        hero.AnimCompleteCounter = 3;

        Move(hero, Triangle | pad);

        Assert.Equal(Prepare, hero.TargetAnimationId);
    }

    [Fact]
    public void Prepare_TheSquare_SwingsTheSword_TheSecondCallOfPlayerTryAttack()
    {
        var hero = Hero(Prepare);
        hero.AnimCompleteCounter = 3;

        Move(hero, Triangle | Square | Right, pressed: Square);

        Assert.Equal(0x10u, hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // 0x03 - the run.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Sprint_DoesNotCallTryUseItem_TheCircleAtZeroDoesNotStopIt()
    {
        AlundraPlayerManager.TryUseItemResultForTests = 0;
        var hero = Hero(Sprint);

        Move(hero, Triangle | Right);

        Assert.Equal(Sprint, hero.TargetAnimationId);
    }

    [Fact]
    public void Sprint_InTheAir_Gives0x2D_TheDirectionIsNotWritten()
    {
        var hero = Hero(Sprint, direction: 0x18, air: true);

        Move(hero, Triangle | Up);

        Assert.Equal(AirStill, hero.TargetAnimationId);
        Assert.Equal(0x18u, hero.TargetDirection);
    }

    [Theory]
    [InlineData(0x18u, 0x08u)]
    [InlineData(0x08u, 0x18u)]
    [InlineData(0x10u, 0x00u)]
    [InlineData(0x00u, 0x10u)]
    public void Sprint_TheCurtailedStep_Bounces_TheDirectionTurnsAroundBy0x10(uint direction, uint bounced)
    {
        var hero = Hero(Sprint, direction: direction);
        hero.ForceAdjusted = 1;
        hero.AnimationDirection = direction switch { 0x18 => 3, 0x08 => 2, 0x10 => 1, _ => 0 };

        Move(hero, Triangle | Right);

        Assert.Equal(Bounce, hero.TargetAnimationId);
        Assert.Equal(bounced, hero.TargetDirection);
    }

    [Fact]
    public void Sprint_TheBounceComesBeforeTheEndOfTheRun_EvenWithTheTriangleReleasedAndNoDirection()
    {
        var hero = Hero(Sprint);
        hero.ForceAdjusted = 1;

        Move(hero, 0);

        Assert.Equal(Bounce, hero.TargetAnimationId);
        Assert.Equal(0, hero.StopSprintCounter);
    }

    [Fact]
    public void Sprint_NoDirection_Slides_CounterOne_TheTriangleReleasedWithADirection_Slides_CounterZero()
    {
        var none = Hero(Sprint);
        Move(none, Triangle);
        Assert.Equal(Stop, none.TargetAnimationId);
        Assert.Equal(1, none.StopSprintCounter);

        var noneAndTriangleReleased = Hero(Sprint);
        Move(noneAndTriangleReleased, 0);
        Assert.Equal(Stop, noneAndTriangleReleased.TargetAnimationId);
        Assert.Equal(1, noneAndTriangleReleased.StopSprintCounter);

        var released = Hero(Sprint);
        released.StopSprintCounter = 9;
        Move(released, Right);
        Assert.Equal(Stop, released.TargetAnimationId);
        Assert.Equal(0, released.StopSprintCounter);
    }

    [Theory]
    [InlineData(3, 0x18u, Right, true)]
    [InlineData(3, 0x18u, Right | Up, true)]
    [InlineData(3, 0x18u, Right | Down, true)]
    [InlineData(3, 0x18u, Up, false)]
    [InlineData(3, 0x18u, Left, false)]
    [InlineData(3, 0x18u, Left | Up, false)]
    [InlineData(1, 0x10u, Up | Left, true)]
    [InlineData(1, 0x10u, Right, false)]
    [InlineData(0, 0x00u, Down | Right, true)]
    [InlineData(2, 0x08u, Down | Left, true)]
    [InlineData(2, 0x08u, Down | Right, false)]
    public void Sprint_TheCone_IsTheFacingAndItsTwoNeighbours(int facing, uint direction, uint pad, bool continues)
    {
        var hero = Hero(Sprint, direction: direction, facing: facing);

        Move(hero, Triangle | pad);

        Assert.Equal(continues ? Sprint : Stop, hero.TargetAnimationId);
        Assert.Equal(0, hero.StopSprintCounter);
    }

    [Fact]
    public void Sprint_TheCone_ReadsTheFacingOfTheTick_NotTheOneOfTheFrame()
    {
        // The frame's facing is still east (3) while the direction written by the previous tick is north: the table gives north (1), whose cone holds the north.
        var hero = Hero(Sprint, direction: 0x10, facing: 3);

        Move(hero, Triangle | Up);

        Assert.Equal(Sprint, hero.TargetAnimationId);
        Assert.Equal(3, hero.AnimationDirection); // the field itself is not written by the tick.
    }

    [Theory]
    [InlineData(Square)]
    [InlineData(Cross)]
    [InlineData(Circle)]
    public void Sprint_ASquareACrossOrACircleHeld_Tackles(uint button)
    {
        var hero = Hero(Sprint);

        Move(hero, Triangle | Right | button);

        Assert.Equal(Tackle, hero.TargetAnimationId);
    }

    [Fact]
    public void Sprint_TheLadderGate_WinsOverTheBounce()
    {
        var hero = Hero(Sprint, direction: 0x10, facing: 1);
        hero.Slope_18c = 6;
        hero.ForceAdjusted = 1;

        Move(hero, Triangle | Up);

        Assert.Equal(0x0Eu, hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // 0x2A - the slide.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Stop_TryUseItemAtZero_EndsTheTick_TheCounterIsNotTouched()
    {
        AlundraPlayerManager.TryUseItemResultForTests = 0;
        var hero = Hero(Stop);
        hero.StopSprintCounter = 5;

        Move(hero, Triangle);

        Assert.Equal(5, hero.StopSprintCounter);
    }

    [Fact]
    public void Stop_InTheAir_Gives0x2D()
    {
        var hero = Hero(Stop, air: true);
        hero.StopSprintCounter = 5;

        Move(hero, Triangle | Right);

        Assert.Equal(AirStill, hero.TargetAnimationId);
    }

    [Fact]
    public void Stop_ACounterAtZero_EndsTheTick_EvenWithADirection()
    {
        var hero = Hero(Stop);

        Move(hero, Triangle | Right);

        Assert.Equal(Stop, hero.TargetAnimationId);
        Assert.Equal(0, hero.StopSprintCounter);
    }

    [Fact]
    public void Stop_TheCurtailedStep_ClearsTheCounter_TheTriangleReleasedToo()
    {
        var curtailed = Hero(Stop);
        curtailed.StopSprintCounter = 12;
        curtailed.ForceAdjusted = 1;
        Move(curtailed, Triangle | Right);
        Assert.Equal(0, curtailed.StopSprintCounter);
        Assert.Equal(Stop, curtailed.TargetAnimationId);

        var released = Hero(Stop);
        released.StopSprintCounter = 12;
        Move(released, Right);
        Assert.Equal(0, released.StopSprintCounter);
        Assert.Equal(Stop, released.TargetAnimationId);
    }

    [Theory]
    [InlineData(1, true)] // the counter becomes 2
    [InlineData(9, true)]
    [InlineData(10, false)] // the counter becomes 11: the window opens
    [InlineData(12, false)]
    public void Stop_BeforeTheWindow_ADirectionBitClearsTheCounter_NoBitKeepsCounting(int counter, bool lost)
    {
        var hero = Hero(Stop);
        hero.StopSprintCounter = counter;
        Move(hero, Triangle);
        Assert.Equal(counter + 1, hero.StopSprintCounter);
        Assert.Equal(Stop, hero.TargetAnimationId);

        var pressed = Hero(Stop);
        pressed.StopSprintCounter = counter;
        Move(pressed, Triangle | Right);
        if (lost)
        {
            Assert.Equal(0, pressed.StopSprintCounter);
            Assert.Equal(Stop, pressed.TargetAnimationId);
        }
        else
        {
            Assert.Equal(Sprint, pressed.TargetAnimationId);
            Assert.Equal(0x18u, pressed.TargetDirection);
        }
    }

    [Fact]
    public void Stop_FromTheWindow_ADiagonalKeepsTheCounter_ACardinalResumes()
    {
        var diagonal = Hero(Stop);
        diagonal.StopSprintCounter = 12;
        Move(diagonal, Triangle | Up | Right);
        Assert.Equal(13, diagonal.StopSprintCounter);
        Assert.Equal(Stop, diagonal.TargetAnimationId);

        var up = Hero(Stop);
        up.StopSprintCounter = 12;
        Move(up, Triangle | Up);
        Assert.Equal(Sprint, up.TargetAnimationId);
        Assert.Equal(0x10u, up.TargetDirection);
        Assert.Equal(13, up.StopSprintCounter); // the counter is a global the run state resets itself.
    }

    // -----------------------------------------------------------------------------------------
    // 0x3E and 0x1C - the bounce.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Bounce)]
    [InlineData(HurtBounce)]
    public void Bounce_TryUseItemAtZero_FlipsTheDirection_AndEndsTheTick(uint state)
    {
        AlundraPlayerManager.TryUseItemResultForTests = 0;
        var hero = Hero(state, direction: 0x08);
        hero.ForceResetAnimationFlag = 1;

        Move(hero, 0);

        Assert.Equal(state, hero.TargetAnimationId);
        Assert.Equal(0x18u, hero.TargetDirection);
    }

    [Theory]
    [InlineData(Bounce)]
    [InlineData(HurtBounce)]
    public void Bounce_WaitsForTheHold_ThenIdleOnTheGround_MovingInTheAir_WithTheDirectionFlipped(uint state)
    {
        var waiting = Hero(state, direction: 0x08);
        Move(waiting, Triangle | Right);
        Assert.Equal(state, waiting.TargetAnimationId);
        Assert.Equal(0x08u, waiting.TargetDirection);

        var ground = Hero(state, direction: 0x08);
        ground.ForceResetAnimationFlag = 1;
        Move(ground, 0);
        Assert.Equal(Idle, ground.TargetAnimationId);
        Assert.Equal(0x18u, ground.TargetDirection);

        var air = Hero(state, direction: 0x18, air: true);
        air.ForceResetAnimationFlag = 1;
        Move(air, 0);
        Assert.Equal(Moving, air.TargetAnimationId);
        Assert.Equal(0x08u, air.TargetDirection);
    }

    [Fact]
    public void Bounce_ThePadIsNotRead_NoTackleNoSwing()
    {
        var hero = Hero(Bounce, direction: 0x08);

        Move(hero, Triangle | Square | Cross | Circle, pressed: Square | Cross | Circle);

        Assert.Equal(Bounce, hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // The deep water (E20.i: the swim branch replaced the stand-in guard of E20.d1; the swim itself is in AlundraHeroSwimTests).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Prepare, 0x18u, 0x18u)]
    [InlineData(Sprint, 0x18u, 0x18u)]
    [InlineData(Stop, 0x18u, 0x18u)]
    [InlineData(Tackle, 0x18u, 0x18u)]
    [InlineData(Bounce, 0x08u, 0x18u)]
    [InlineData(HurtBounce, 0x18u, 0x08u)]
    public void DeepWater_EveryStateOfTheRun_GoesToTheFloat_ThenTheTickEnds_OnlyTheBouncesFlipTheDirectionFirst(uint state, uint direction, uint expected)
    {
        var hero = Hero(state, direction: direction);
        hero.Slope_18c = 4;
        hero.AnimCompleteCounter = 3;

        Move(hero, Triangle | Right);

        Assert.Equal(0x1Du, hero.TargetAnimationId);
        Assert.Equal(expected, hero.TargetDirection);
    }

    [Fact]
    public void DeepWater_TheTailDoesNotStartTheRun_TheHeroFloats_ElsewhereTheStatesOfTheRunAreUntouched()
    {
        var idle = Hero(Idle);
        idle.Slope_18c = 4;
        Move(idle, Triangle | Right);
        Assert.Equal(0x1Du, idle.TargetAnimationId);

        var dry = Hero(Sprint);
        dry.Slope_18c = 0;
        Move(dry, Triangle | Right);
        Assert.Equal(Sprint, dry.TargetAnimationId);

        var shallow = Hero(Sprint);
        shallow.Slope_18c = 2;
        Move(shallow, Triangle | Right);
        Assert.Equal(Sprint, shallow.TargetAnimationId);
    }
}
