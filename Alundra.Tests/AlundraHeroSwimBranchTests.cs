#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.i T4 (docs/plan-e20-heros.md section 1.14): the branches of the swim that the traces of the oracle cannot reach or that a bare hero shows best, on a bare hero (no controller: the air predicate reads
/// <see cref="EntityScriptProxy.HeroAirborne"/>), one <see cref="PlayerManager.MovePlayer"/> call per case, in the order of the binary's handlers (<c>0x80032674</c>, <c>0x80032658</c>, <c>0x80032680</c>,
/// <c>0x80032690</c>, <c>0x800326FC</c>, <c>0x80032758</c>, the land exits <c>0x80031DD0</c> and <c>0x80031DB4</c>). The 240 traces of the group W8 (80 states, three pads, three ticks) are replayed state by state: the state
/// the oracle holds at the end of tick t-1 is posed, one <c>MovePlayer</c> runs with the pad of tick t, and what it writes (the target animation, the direction) is the oracle's of tick t.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraHeroSwimBranchTests : IDisposable
{
    private const uint Triangle = PadState.Triangle;
    private const uint Square = PadState.Square;
    private const uint Cross = PadState.Cross;
    private const uint Circle = PadState.Circle;
    private const uint Up = PadState.Up;
    private const uint Right = PadState.Right;
    private const uint Down = PadState.Down;
    private const uint Left = PadState.Left;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint AirStill = 0x2D;
    private const uint Swim = 0x0F;
    private const uint Float = 0x1D;
    private const uint Dive = 0x28;
    private const uint SwimHurt = 0x3A;
    private const uint SwimHurtEnd = 0x3B;

    public void Dispose() => PlayerManager.TryUseItemResultForTests = null;

    private static EntityScriptProxy Hero(uint animation, uint direction = 0x18, bool air = false, int slope = 4)
    {
        var hero = new EntityScriptProxy { TargetAnimationId = animation, TargetDirection = direction, AnimationDirection = 3, IsOnGround = air ? 0 : 1, Slope_18c = slope };
        hero.HeroAirborne = air;
        return hero;
    }

    private static void Move(EntityScriptProxy hero, uint hold, uint pressed = 0)
    {
        var pad = new PadState { ButtonsHold = hold, ButtonsJustPressed = pressed };
        PlayerManager.MovePlayer(hero, in pad, new GameState(), host: null);
    }

    // -----------------------------------------------------------------------------------------
    // W8 - the table, the 240 traces.
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> W8() => RunOracleTrace.NamesOf("w8").Select(n => new object[] { n });

    [Fact]
    public void W8_HasTheEightyStatesInThreePads()
    {
        Assert.Equal(240, RunOracleTrace.NamesOf("w8").Count());
    }

    [Theory]
    [MemberData(nameof(W8))]
    public void W8_EveryStateOfTheTable_WritesTheOraclesStateAndDirection_TicksOneToThree(string name)
    {
        var trace = RunOracleTrace.Load(name);
        Assert.Equal(3, trace.Ticks);

        for (var tick = 1; tick <= 3; tick++)
        {
            var hero = Hero((uint)trace.Col("anim_target", tick - 1), (uint)trace.Col("dir_target", tick - 1));
            hero.ForceResetAnimationFlag = (int)trace.Col("anim_end_AC", tick - 1);
            hero.WeaponChargeTimer = 7;
            hero.WeaponRecordPointer = 5;

            var hold = (uint)trace.Hold[tick];
            var pressed = hold & ~(uint)trace.Hold[tick - 1];
            Move(hero, hold, pressed);

            Assert.True(
                trace.Col("anim_target", tick) == hero.TargetAnimationId, $"{name} tick {tick}: the state posed was 0x{trace.Col("anim_target", tick - 1):X2}, the DLL writes 0x{hero.TargetAnimationId:X2}, the oracle 0x{trace.Col("anim_target", tick):X2}");
            Assert.True(
                trace.Col("dir_target", tick) == hero.TargetDirection, $"{name} tick {tick}: the DLL writes the direction 0x{hero.TargetDirection:X2}, the oracle 0x{trace.Col("dir_target", tick):X2}");
            Assert.Equal(0, hero.WeaponChargeTimer);
            Assert.Equal(0u, hero.WeaponRecordPointer);
        }
    }

    // -----------------------------------------------------------------------------------------
    // The six handlers.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Idle)]
    [InlineData(Moving)]
    [InlineData(0x2B)]
    [InlineData(0x2C)]
    [InlineData(0x29)]
    [InlineData(0x03)]
    [InlineData(0x10)]
    [InlineData(0x0E)]
    [InlineData(0x36)]
    [InlineData(0x39)]
    public void Float_EveryOtherState_BecomesTheFloat_TheDirectionAndThePadAreNotRead(uint state)
    {
        var hero = Hero(state, direction: 0x08);

        Move(hero, Right | Triangle, pressed: Triangle);

        Assert.Equal(Float, hero.TargetAnimationId);
        Assert.Equal(0x08u, hero.TargetDirection);
    }

    [Theory]
    [InlineData(0x1C, 0x18u, 0x08u)]
    [InlineData(0x31, 0x08u, 0x18u)]
    [InlineData(0x3E, 0x04u, 0x14u)]
    public void FlipThenFloat_TheDirectionTurnsBy0x10_ThenTheFloat(uint state, uint direction, uint expected)
    {
        var hero = Hero(state, direction);

        Move(hero, Left | Square, pressed: Square);

        Assert.Equal(Float, hero.TargetAnimationId);
        Assert.Equal(expected, hero.TargetDirection);
    }

    [Theory]
    [InlineData(0x0D)]
    [InlineData(0x1A)]
    [InlineData(0x1B)]
    [InlineData(0x1E)]
    [InlineData(0x27)]
    [InlineData(0x37)]
    [InlineData(0x38)]
    [InlineData(0x3D)]
    [InlineData(0x43)]
    [InlineData(0x45)]
    [InlineData(0x47)]
    [InlineData(0x48)]
    [InlineData(0x4A)]
    [InlineData(0x4C)]
    [InlineData(0x4D)]
    [InlineData(0x50)]
    [InlineData(0x7F)]
    public void Skip_TheFifteenStatesAndTheOnesFrom0x50_StayAsTheyAre_DirectionToo(uint state)
    {
        var hero = Hero(state, direction: 0x10);

        Move(hero, Right | Triangle, pressed: Triangle);

        Assert.Equal(state, hero.TargetAnimationId);
        Assert.Equal(0x10u, hero.TargetDirection);
    }

    [Theory]
    [InlineData(Dive)]
    [InlineData(SwimHurt)]
    public void ItemOnly_TheBurstAndTheHurt_ReadNeitherThePadNorTheDirection(uint state)
    {
        var hero = Hero(state, direction: 0x10);

        Move(hero, Right | Triangle | Cross, pressed: Triangle | Cross);

        Assert.Equal(state, hero.TargetAnimationId);
        Assert.Equal(0x10u, hero.TargetDirection);
    }

    [Theory]
    [InlineData(Swim)]
    [InlineData(Float)]
    public void Steer_TheDirectionOfThePadIsWrittenBeforeTheItemDriver_EvenWhenTheDriverAnswers0(uint state)
    {
        PlayerManager.TryUseItemResultForTests = 0;
        var hero = Hero(state, direction: 0x18);

        Move(hero, Left | Triangle, pressed: Triangle);

        Assert.Equal(0x08u, hero.TargetDirection); // written in the delay slot of the call
        Assert.Equal(state, hero.TargetAnimationId); // the driver at 0 ends the tick: no burst, no change
    }

    [Theory]
    [InlineData(Triangle)]
    [InlineData(Cross)]
    [InlineData(Square)]
    public void Steer_AnEdgeOfTheTriangleTheCrossOrTheSquare_Bursts_WithOrWithoutADirection(uint button)
    {
        foreach (var start in new[] { Swim, Float })
        {
            var withDirection = Hero(start, direction: 0x18);
            Move(withDirection, Down | button, pressed: button);
            Assert.Equal(Dive, withDirection.TargetAnimationId);
            Assert.Equal(0u, withDirection.TargetDirection);

            var without = Hero(start, direction: 0x18);
            Move(without, button, pressed: button);
            Assert.Equal(Dive, without.TargetAnimationId);
            Assert.Equal(0x18u, without.TargetDirection); // the direction falls back to the current one
        }
    }

    [Fact]
    public void Steer_TheCircleDoesNotBurst_ARelease_AHeldButton_DoNot()
    {
        var circle = Hero(Float);
        Move(circle, Circle, pressed: Circle);
        Assert.Equal(Float, circle.TargetAnimationId);

        var held = Hero(Float);
        Move(held, Triangle); // held, no edge
        Assert.Equal(Float, held.TargetAnimationId);

        var released = Hero(Swim);
        Move(released, Right);
        Assert.Equal(Swim, released.TargetAnimationId);
    }

    [Theory]
    [InlineData(Up | Down)]
    [InlineData(Left | Right)]
    [InlineData(Up | Down | Left | Right)]
    public void Steer_AContradictoryNibble_Swims_AndKeepsTheDirection(uint hold)
    {
        var hero = Hero(Float, direction: 0x18);

        Move(hero, hold);

        Assert.Equal(Swim, hero.TargetAnimationId);
        Assert.Equal(0x18u, hero.TargetDirection);
    }

    [Fact]
    public void Steer_NoDirection_Floats_ADirection_Swims()
    {
        var rest = Hero(Swim);
        Move(rest, 0);
        Assert.Equal(Float, rest.TargetAnimationId);

        var go = Hero(Float);
        Move(go, Up);
        Assert.Equal(Swim, go.TargetAnimationId);
        Assert.Equal(0x10u, go.TargetDirection);
    }

    [Fact]
    public void Steer_TheBurstOfTheTriangleAndTheCross_OnTheSameTick_IsOneBurst()
    {
        var hero = Hero(Swim);

        Move(hero, Right | Triangle | Cross, pressed: Triangle | Cross);

        Assert.Equal(Dive, hero.TargetAnimationId);
    }

    [Fact]
    public void HurtEnd_WaitsForTheHoldFlag_ThenFloatsAndTurnsBack_TheItemDriverAt0TurnsTheDirectionAndEndsTheTick()
    {
        var waiting = Hero(SwimHurtEnd, direction: 0x08);
        Move(waiting, Right | Triangle, pressed: Triangle);
        Assert.Equal(SwimHurtEnd, waiting.TargetAnimationId);
        Assert.Equal(0x08u, waiting.TargetDirection);

        var done = Hero(SwimHurtEnd, direction: 0x08);
        done.ForceResetAnimationFlag = 1;
        Move(done, Right);
        Assert.Equal(Float, done.TargetAnimationId);
        Assert.Equal(0x18u, done.TargetDirection);

        PlayerManager.TryUseItemResultForTests = 0;
        var driver = Hero(SwimHurtEnd, direction: 0x08);
        driver.ForceResetAnimationFlag = 1;
        Move(driver, 0);
        Assert.Equal(SwimHurtEnd, driver.TargetAnimationId); // the answer 0 ends the tick before the flag is read
        Assert.Equal(0x18u, driver.TargetDirection);
    }

    // -----------------------------------------------------------------------------------------
    // The branch itself: what it skips, what it zeroes.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheBranch_ZeroesTheChargeAndThePendingShot_NeverCountsTheCharge_AndNeverSwings()
    {
        var hero = Hero(Float);
        hero.WeaponChargeTimer = 40;
        hero.WeaponRecordPointer = 0x80028C14;

        Move(hero, Square, pressed: Square);

        Assert.Equal(0, hero.WeaponChargeTimer);
        Assert.Equal(0u, hero.WeaponRecordPointer);
        Assert.Equal(Dive, hero.TargetAnimationId); // the Square is the burst's button here, not a swing
    }

    [Fact]
    public void TheBranch_IsTakenOnSlope4Only()
    {
        var land = Hero(Idle, slope: 0);
        Move(land, Square, pressed: Square);
        Assert.NotEqual(Float, land.TargetAnimationId); // the swing of the land

        var shallow = Hero(Idle, slope: 2);
        Move(shallow, Right);
        Assert.Equal(Moving, shallow.TargetAnimationId);

        var deep = Hero(Idle, slope: 4);
        Move(deep, Right);
        Assert.Equal(Float, deep.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // The exits to the land (W9).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Swim)]
    [InlineData(Float)]
    [InlineData(Dive)]
    public void Land_TheSwimStates_GoToIdleOnTheGround_To0x2DInTheAir_TheDirectionIsKept(uint state)
    {
        var ground = Hero(state, direction: 0x10, slope: 0);
        Move(ground, Right);
        Assert.Equal(Idle, ground.TargetAnimationId);
        Assert.Equal(0x10u, ground.TargetDirection);

        var air = Hero(state, direction: 0x10, air: true, slope: 0);
        Move(air, Right);
        Assert.Equal(AirStill, air.TargetAnimationId);
        Assert.Equal(0x10u, air.TargetDirection);
    }

    [Theory]
    [InlineData(SwimHurt)]
    [InlineData(SwimHurtEnd)]
    public void Land_TheHurtStates_TurnTheDirectionFirst_ThenIdleOrTheAir(uint state)
    {
        var ground = Hero(state, direction: 0x08, slope: 0);
        Move(ground, Right);
        Assert.Equal(Idle, ground.TargetAnimationId);
        Assert.Equal(0x18u, ground.TargetDirection);

        var air = Hero(state, direction: 0x18, air: true, slope: 0);
        Move(air, 0);
        Assert.Equal(AirStill, air.TargetAnimationId);
        Assert.Equal(0x08u, air.TargetDirection);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Land_TheExit_IsReachedOnEverySlopeThatIsNot4(int slope)
    {
        var hero = Hero(Swim, slope: slope);

        Move(hero, Right);

        Assert.Equal(Idle, hero.TargetAnimationId);
    }
}
