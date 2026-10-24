#nullable enable
using Xunit;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1, (c1) and (c2), the pure half): <c>StepTowards</c> (<c>0x8002F854</c>) and the three decisions of <c>UpdatePlayerCarriedEntity</c> (<c>0x8002F884</c>) as functions of the three differences
/// (object minus hero). Every value is a vector of the binary (<c>docs/plan-e20-annexe/e20-g1a/critic-scenarios/c1_equality.py</c>, <c>work/g1a_models.py</c>, replayed by the critic with no deviation); each "without effect" case is in the same assertion as the case it is the neighbour of.
/// </summary>
public sealed class CarryRulesStepTests
{
    private const int Px = CarryBench.Px;

    private static CarryRules.Triple T(int x, int y, int z) => new(x, y, z);

    [Fact]
    public void StepTowards_AGapUnderTheStepGivesTheTarget_TheStepOrMoreMovesByTheStep_TheGapEqualToTheStepIsNotUnder()
    {
        // under the step: the target (both senses, and the gap 0).
        Assert.Equal((100, 100, 100, 7), (CarryRules.StepTowards(95, 100, 10), CarryRules.StepTowards(105, 100, 10), CarryRules.StepTowards(100, 100, 10), CarryRules.StepTowards(7, 7, 10)));

        // the neighbours: the gap equal to the step moves by the step (and lands on the target), one more moves by the step (and does not land).
        Assert.Equal((100, 100, 91, 109), (CarryRules.StepTowards(90, 100, 10), CarryRules.StepTowards(110, 100, 10), CarryRules.StepTowards(81, 100, 10), CarryRules.StepTowards(119, 100, 10)));
    }

    [Fact]
    public void StepTowards_TheGapIsTheUnsignedMagnitudeOfTheWrappedDifference()
    {
        // the 32-bit machine: 0x80000000 - 0x7FFFFFFF wraps to 1, which is under the step, so the target is reached (in both senses); a gap of the whole range is not under the step and moves by it.
        Assert.Equal((int.MinValue, int.MaxValue), (CarryRules.StepTowards(int.MaxValue, int.MinValue, 0x10000), CarryRules.StepTowards(int.MinValue, int.MaxValue, 0x10000)));
        Assert.Equal(int.MaxValue - 0x10000, CarryRules.StepTowards(int.MaxValue, 0, 0x10000));
    }

    [Fact]
    public void FreeOffset_TheEqualityOfTheTwoErrorsMovesTheThreeAxes_OneRawUnitLessInTheHorizontalErrorMovesOnlyZ()
    {
        // (21 px, 0, 11 px): the larger horizontal gap is 21 px and the error of Z is |11 - 32| = 21 px: equal, so X, Y and Z advance by 1 px each.
        Assert.Equal(T(20 * Px, 0, 12 * Px), CarryRules.FreeOffset(T(21 * Px, 0, 11 * Px)));

        // the neighbour, one raw unit less in X: the horizontal gap is smaller than the error, only Z advances.
        Assert.Equal(T(21 * Px - 1, 0, 12 * Px), CarryRules.FreeOffset(T(21 * Px - 1, 0, 11 * Px)));

        // the neighbour, one raw unit more: the three advance.
        Assert.Equal(T(20 * Px + 1, 0, 12 * Px), CarryRules.FreeOffset(T(21 * Px + 1, 0, 11 * Px)));
    }

    [Fact]
    public void FreeOffset_TheLargerOfTheTwoHorizontalGapsDecides_EitherSign_AndZGoesTowardsTheHead()
    {
        // (0, -21 px, 11 px): Y is the larger gap (21 px = the error): the three advance, Y towards 0 (from below), Z up.
        Assert.Equal(T(0, -20 * Px, 12 * Px), CarryRules.FreeOffset(T(0, -21 * Px, 11 * Px)));

        // (5 px, 0, 37 px): the error is |37 - 32| = 5 px = the gap: the three advance, Z DOWN towards 32 px.
        Assert.Equal(T(4 * Px, 0, 36 * Px), CarryRules.FreeOffset(T(5 * Px, 0, 37 * Px)));

        // the neighbour, 5 px minus a raw unit: only Z advances (down).
        Assert.Equal(T(5 * Px - 1, 0, 36 * Px), CarryRules.FreeOffset(T(5 * Px - 1, 0, 37 * Px)));

        // the object on the head of the hero (0, 0, 32 px) stays; one raw unit beside it, the error of Z is zero and the three advance (X to 0, Z unchanged).
        Assert.Equal(T(0, 0, 32 * Px), CarryRules.FreeOffset(T(0, 0, 32 * Px)));
        Assert.Equal(T(0, 0, 32 * Px), CarryRules.FreeOffset(T(1, 0, 32 * Px)));
    }

    [Fact]
    public void FreeOffset_TheAxesThatAreUnderTheStepGoStraightToTheirTarget()
    {
        // 0.5 px from the target on every axis, equality of the errors (0.5 px): the three reach it at once.
        Assert.Equal(T(0, 0, 32 * Px), CarryRules.FreeOffset(T(Px / 2, -Px / 2, 32 * Px - Px / 2)));
    }

    [Fact]
    public void LockedPull_EachAxisAdvancesTowards0ByTwoPixels_AndTheStepEqualToTheGapStillReaches0()
    {
        Assert.Equal(T(3 * Px, 0, -3 * Px), CarryRules.LockedPull(T(5 * Px, 0, -5 * Px)));

        // the gap of exactly 2 px is not under the step, it moves by the step and lands on 0 all the same; one raw unit less is under it (the target).
        Assert.Equal(T(0, 0, 0), CarryRules.LockedPull(T(0x20000, 0, 0)));
        Assert.Equal(T(0, 0, 0), CarryRules.LockedPull(T(-0x20000, 0, 0x1FFFF)));

        // an axis alone does not move the others: 3 px in Y, 0 elsewhere.
        Assert.Equal(T(0, Px, 0), CarryRules.LockedPull(T(0, 3 * Px, 0)));
    }

    [Fact]
    public void CanDestroyLocked_TheThreeGapsAtMost0x1FFFF_AnyOneAt0x20000IsNotEnough_EitherSign()
    {
        Assert.True(CarryRules.CanDestroyLocked(T(0x1FFFF, 0, 0)));
        Assert.True(CarryRules.CanDestroyLocked(T(0, 0x1FFFF, 0x1FFFF)));
        Assert.True(CarryRules.CanDestroyLocked(T(-0x1FFFF, -0x1FFFF, -0x1FFFF)));
        Assert.True(CarryRules.CanDestroyLocked(T(0, 0, 0)));

        Assert.False(CarryRules.CanDestroyLocked(T(0x20000, 0, 0)));
        Assert.False(CarryRules.CanDestroyLocked(T(0, 0, 0x20000)));
        Assert.False(CarryRules.CanDestroyLocked(T(-0x20000, 0, 0)));
        Assert.False(CarryRules.CanDestroyLocked(T(0x1FFFF, 0x1FFFF, -0x20000)));
    }
}
