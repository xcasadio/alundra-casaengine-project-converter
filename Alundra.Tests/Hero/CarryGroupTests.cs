#nullable enable
using Xunit;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1, (c6)): the table of the carrying group of <c>MovePlayer</c> (<c>0x8003232C</c>: the states 0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30) and of the two lift gestures (0x05 at <c>0x800322F0</c>, 0x0B at
/// <c>0x800322B4</c>), pure: the input is what the binary reads (an object or none, its kind, the ground, a held direction, the edges of the Square and the Cross, the two ground properties of 0x20 and 0x40), the output the next state or "keep" or
/// "throw" (the Square branch, which g2a replaces; here a named stub). The rows are those of <c>work/g1a_models.py</c> (<c>carry_group</c>). Every row is next to its inverse.
/// </summary>
public sealed class CarryGroupTests
{
    private static CarryRules.CarryInput Input(bool hasObject = true, bool heavy = false, bool ground = true, bool direction = false, bool square = false, bool cross = false, bool noThrow = false, bool noJump = false)
        => new(hasObject, heavy, ground, direction, square, cross, noThrow, noJump);

    private static CarryRules.CarryStep Set(uint state) => new(state, false);

    private static readonly CarryRules.CarryStep Keep = new(null, false);

    private static readonly CarryRules.CarryStep Throw = new(null, true);

    [Fact]
    public void C6_TheGroupIsTheSixStatesOfTheBinary_AndNoneOfTheirNeighbours()
    {
        foreach (var state in new uint[] { 0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30 })
        {
            Assert.True(CarryRules.IsCarryGroupState(state), $"state {state:X2}");
        }

        // the neighbours: the lift gestures (their own rows below), the attack and throw gestures, the plain states, the jumps without an object.
        foreach (var state in new uint[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x08, 0x09, 0x0A, 0x0B, 0x0D, 0x0E, 0x2B, 0x2C, 0x2D, 0x31, 0x35 })
        {
            Assert.False(CarryRules.IsCarryGroupState(state), $"state {state:X2}");
        }
    }

    [Fact]
    public void C6_NoObject_OnTheGroundIdle_InTheAirTheFallState_AndWithAnObjectNeitherOfTheTwo()
    {
        Assert.Equal((Set(0x00), Set(0x2D)), (CarryRules.CarryGroup(Input(hasObject: false, ground: true)), CarryRules.CarryGroup(Input(hasObject: false, ground: false))));

        // the inverse: with an object, the same ground gives a carrying state, never the idle one.
        Assert.NotEqual(Set(0x00), CarryRules.CarryGroup(Input(ground: true)));
        Assert.NotEqual(Set(0x2D), CarryRules.CarryGroup(Input(ground: false)));

        // no object: the Square, the Cross and the direction change nothing.
        Assert.Equal(Set(0x00), CarryRules.CarryGroup(Input(hasObject: false, ground: true, direction: true, square: true, cross: true)));
    }

    [Fact]
    public void C6_TheSquareThrows_TheGroundPropertyOf0x20ForbidsItAndNothingElseHappens_ThePropertyAloneForbidsNothing()
    {
        Assert.Equal((Throw, Throw), (CarryRules.CarryGroup(Input(ground: true, square: true)), CarryRules.CarryGroup(Input(ground: false, square: true))));

        // the property of 0x20 under the feet: the tick ends there (keep), whatever the Cross or the direction say.
        Assert.Equal((Keep, Keep), (CarryRules.CarryGroup(Input(square: true, noThrow: true, cross: true, direction: true)), CarryRules.CarryGroup(Input(ground: false, square: true, noThrow: true))));

        // the property without the Square does not stop the rest: the carrying state is chosen as usual.
        Assert.Equal(Set(0x0C), CarryRules.CarryGroup(Input(noThrow: true)));
    }

    [Fact]
    public void C6_TheSquareComesBeforeTheKind_AHeavyObjectIsThrownTooWhenTheSquareIsPressed_AndNotOtherwise()
    {
        Assert.Equal((Throw, Set(0x0C), Set(0x30)), (CarryRules.CarryGroup(Input(heavy: true, square: true)), CarryRules.CarryGroup(Input(heavy: true, ground: true)), CarryRules.CarryGroup(Input(heavy: true, ground: false))));
    }

    [Fact]
    public void C6_AHeavyObjectKeepsTheCarryingStateOnly_TheCrossAndTheDirectionAreIgnored_NextToTheOrdinaryObjectThatMoves()
    {
        // the heavy one: 0x0C on the ground, 0x30 in the air, with or without the Cross and the direction.
        foreach (var cross in new[] { false, true })
        {
            foreach (var direction in new[] { false, true })
            {
                Assert.Equal((Set(0x0C), Set(0x30)), (CarryRules.CarryGroup(Input(heavy: true, ground: true, cross: cross, direction: direction)), CarryRules.CarryGroup(Input(heavy: true, ground: false, cross: cross, direction: direction))));
            }
        }

        // the inverse: the ordinary object with the same inputs walks (0x07) and jumps (0x06).
        Assert.Equal((Set(0x07), Set(0x06)), (CarryRules.CarryGroup(Input(direction: true)), CarryRules.CarryGroup(Input(direction: true, cross: true))));
    }

    [Fact]
    public void C6_OnTheGround_WalkOrStandWithoutTheCross_JumpWithTheCross_TheDirectionPicksTheMovingVariant()
    {
        Assert.Equal((Set(0x0C), Set(0x07)), (CarryRules.CarryGroup(Input(direction: false)), CarryRules.CarryGroup(Input(direction: true))));
        Assert.Equal((Set(0x2E), Set(0x06)), (CarryRules.CarryGroup(Input(cross: true, direction: false)), CarryRules.CarryGroup(Input(cross: true, direction: true))));
    }

    [Fact]
    public void C6_TheGroundPropertyOf0x40ForbidsTheJumpAndKeeps_TheOtherRowsAreUnchangedByIt()
    {
        Assert.Equal((Keep, Keep), (CarryRules.CarryGroup(Input(cross: true, noJump: true)), CarryRules.CarryGroup(Input(cross: true, direction: true, noJump: true))));

        // the property without the Cross changes nothing (walking and standing), and the Cross without the property jumps.
        Assert.Equal((Set(0x0C), Set(0x07), Set(0x2E)), (CarryRules.CarryGroup(Input(noJump: true)), CarryRules.CarryGroup(Input(noJump: true, direction: true)), CarryRules.CarryGroup(Input(cross: true))));

        // in the air the property of 0x40 is not read.
        Assert.Equal(Set(0x30), CarryRules.CarryGroup(Input(ground: false, cross: true, noJump: true)));
    }

    [Fact]
    public void C6_InTheAir_AirWalkWithADirection_AirStillWithout_TheCrossIsNotRead()
    {
        Assert.Equal((Set(0x30), Set(0x2F)), (CarryRules.CarryGroup(Input(ground: false)), CarryRules.CarryGroup(Input(ground: false, direction: true))));
        Assert.Equal((Set(0x30), Set(0x2F)), (CarryRules.CarryGroup(Input(ground: false, cross: true)), CarryRules.CarryGroup(Input(ground: false, cross: true, direction: true))));
    }

    [Fact]
    public void C6_TheGroupDoesNotDependOnWhichOfItsSixStatesRunsIt_BecauseTheStateIsNotAnInput()
    {
        // the six states share one body (0x8003232C): the rule has no state parameter; a state outside the group is refused by IsCarryGroupState, not by the table.
        var input = Input(direction: true, cross: true);
        Assert.Equal(Set(0x06), CarryRules.CarryGroup(input));
    }

    [Fact]
    public void C6_TheLiftGestureOfTheGround_0x05_StaysWhileOnTheGround_FallsWithoutObject_BecomesAirCarryInTheAir()
    {
        Assert.Equal((null, 0x2Du, 0x30u), (CarryRules.LiftGestureNext(0x05, hasObject: true, ground: true), CarryRules.LiftGestureNext(0x05, hasObject: false, ground: true), CarryRules.LiftGestureNext(0x05, hasObject: true, ground: false)));
        Assert.Equal(0x2Du, CarryRules.LiftGestureNext(0x05, hasObject: false, ground: false));
    }

    [Fact]
    public void C6_TheLiftGestureOfTheAir_0x0B_StaysWhileInTheAir_IdleWithoutObject_BecomesGroundCarryOnTheGround()
    {
        Assert.Equal((null, 0x00u, 0x0Cu), (CarryRules.LiftGestureNext(0x0B, hasObject: true, ground: false), CarryRules.LiftGestureNext(0x0B, hasObject: false, ground: false), CarryRules.LiftGestureNext(0x0B, hasObject: true, ground: true)));
        Assert.Equal(0x00u, CarryRules.LiftGestureNext(0x0B, hasObject: false, ground: true));
    }

    [Fact]
    public void C6_TheLiftGestureRuleIsForTheTwoGesturesOnly_AnyOtherStateGivesNothing()
    {
        foreach (var state in new uint[] { 0x00, 0x06, 0x07, 0x0C, 0x2D, 0x2E, 0x2F, 0x30 })
        {
            Assert.Null(CarryRules.LiftGestureNext(state, hasObject: false, ground: true));
            Assert.Null(CarryRules.LiftGestureNext(state, hasObject: true, ground: false));
        }
    }

    [Fact]
    public void C6_TheAnimationOfTheLift_OnTheHeadGroundOrAirCarry_ElseTheGestureOfTheGroundOrOfTheAir()
    {
        Assert.Equal((0x0Cu, 0x30u, 0x05u, 0x0Bu), (CarryRules.LiftAnimation(wasOnHead: true, ground: true), CarryRules.LiftAnimation(wasOnHead: true, ground: false), CarryRules.LiftAnimation(wasOnHead: false, ground: true), CarryRules.LiftAnimation(wasOnHead: false, ground: false)));
    }
}
