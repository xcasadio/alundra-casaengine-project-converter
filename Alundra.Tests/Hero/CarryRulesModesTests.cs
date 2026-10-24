#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1, (c1) to (c3)): the three modes of <c>UpdatePlayerCarriedEntity</c> (<c>0x8002F884</c>) called directly on a real hero and a real object, the way <see cref="CarryRules.UpdateCarried"/> is
/// called from <c>PlayerManager.UpdatePlayerCarriedEntity</c> once phase B wires it. The vectors are those of <c>c1_equality.py</c> and of <c>work/g1a_models.py</c> (<c>upce</c>). Each "without effect" case is next to the positive case it is the neighbour of.
/// </summary>
public sealed class CarryRulesModesTests
{
    private const int Px = CarryBench.Px;

    private static CarryRules.Triple T(int x, int y, int z) => new(x, y, z);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) mode 0: the free tick.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(21 * Px, 0, 11 * Px, 20 * Px, 0, 12 * Px)]                   // equality: the three advance (c1_equality: (21, 0, 11) gives (20, 0, 12)).
    [InlineData(21 * Px - 1, 0, 11 * Px, 21 * Px - 1, 0, 12 * Px)]           // one raw unit less: only Z.
    [InlineData(21 * Px + 1, 0, 11 * Px, 20 * Px + 1, 0, 12 * Px)]           // one raw unit more: the three.
    [InlineData(0, -21 * Px, 11 * Px, 0, -20 * Px, 12 * Px)]                 // Y is the larger gap.
    [InlineData(5 * Px, 0, 37 * Px, 4 * Px, 0, 36 * Px)]                     // above the head: Z comes down.
    [InlineData(5 * Px - 1, 0, 37 * Px, 5 * Px - 1, 0, 36 * Px)]             // one raw unit less: only Z.
    [InlineData(0, 0, 32 * Px, 0, 0, 32 * Px)]                               // on the head: unchanged.
    public void C1_TheOffsetIsReadOnThePositions_TheOldOffsetOfTheHeroIsIgnored_AndTheCounterGoesBackTo0(int dx, int dy, int dz, int ex, int ey, int ez)
    {
        var (hero, item) = CarryBench.Pair(T(dx, dy, dz), counter: 4);

        CarryRules.UpdateCarried(hero, 0, null);

        Assert.Equal((T(ex, ey, ez), 0), (CarryBench.OffsetOf(hero), hero.CarryLockCounter));
        Assert.Equal((100 * Px + dx, 100 * Px + dy, dz), (item.PosX, item.PosY, item.PosZ)); // the object is not moved by the rule: the follow branch of the tick does that (phase B).
    }

    [Fact]
    public void C1_TheTraceFrom21PxEastAtTheHeroesFeetTo0_0_32Px_Takes32Calls_XStartsAtTheTwelfth()
    {
        var (hero, item) = CarryBench.Pair(T(21 * Px, 0, 0));
        var firstXMove = 0;

        for (var call = 1; call <= 32; call++)
        {
            CarryRules.UpdateCarried(hero, 0, null);
            var offset = CarryBench.OffsetOf(hero);

            // the closed form of the trace: Z climbs 1 px per call, X comes down 1 px per call from the twelfth call on.
            var expected = call <= 11 ? T(21 * Px, 0, call * Px) : T((21 - (call - 11)) * Px, 0, call * Px);
            Assert.Equal(expected, offset);
            if (firstXMove == 0 && offset.X != 21 * Px)
            {
                firstXMove = call;
            }

            // the follow branch of the tick: the object is at the hero plus the offset.
            item.PosX = hero.PosX + offset.X;
            item.PosY = hero.PosY + offset.Y;
            item.PosZ = hero.PosZ + offset.Z;
        }

        Assert.Equal((12, T(0, 0, 32 * Px)), (firstXMove, CarryBench.OffsetOf(hero)));

        // the 33rd call changes nothing.
        CarryRules.UpdateCarried(hero, 0, null);
        Assert.Equal(T(0, 0, 32 * Px), CarryBench.OffsetOf(hero));
    }

    [Fact]
    public void C1_NothingCarried_OnlyTheCounterIsReset_AndTheHeroOffsetIsNotWritten_NextToTheCarriedCaseThatWritesIt()
    {
        var (hero, _) = CarryBench.Pair(T(21 * Px, 0, 11 * Px), counter: 4);
        var (empty, _) = CarryBench.Pair(T(21 * Px, 0, 11 * Px), counter: 4);
        empty.CarriedEntity = null;

        CarryRules.UpdateCarried(hero, 0, null);
        CarryRules.UpdateCarried(empty, 0, null);

        Assert.Equal((false, 0, true, 0), (CarryBench.OffsetUntouched(hero), hero.CarryLockCounter, CarryBench.OffsetUntouched(empty), empty.CarryLockCounter));
    }

    [Fact]
    public void C1_ACarriedEntityWithoutProxy_NothingHappensAndNothingThrows_NextToTheEntityWithOneThatMoves()
    {
        var (hero, _) = CarryBench.Pair(T(21 * Px, 0, 11 * Px));
        var (bare, _) = CarryBench.Pair(T(21 * Px, 0, 11 * Px));
        bare.CarriedEntity = new Entity { Name = "bare entity" }; // what AlundraLadderClimbTests puts there: an engine entity whose proxy is not an EntityScriptProxy.

        CarryRules.UpdateCarried(hero, 0, null);
        var exception = Record.Exception(() => CarryRules.UpdateCarried(bare, 0, null));

        Assert.Equal((null, false, true), (exception, CarryBench.OffsetUntouched(hero), CarryBench.OffsetUntouched(bare)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c2) mode 1: the locked branch.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(-3)]
    public void C2_UnderFiveCalls_TheCounterGoesUpByOne_AndNothingIsWritten_NextToTheFifthCallThatPullsTheObject(int counter)
    {
        var (hero, _) = CarryBench.Pair(T(10 * Px, 0, 0), counter: counter);
        var world = new CarryBench.RecordingWorld();

        CarryRules.UpdateCarried(hero, 1, world);

        // without effect: the counter alone moves, the offset is not written, nothing is destroyed.
        Assert.Equal((counter + 1, true, 0), (hero.CarryLockCounter, CarryBench.OffsetUntouched(hero), world.Destroyed.Count));
    }

    [Fact]
    public void C2_FromTheCounter5_TheThreeGapsAreDrawnTowards0ByTwoPixels_AndTheCounterIsKept_AtAnyValueFrom5()
    {
        foreach (var counter in new[] { 5, 6, 40 })
        {
            var (hero, item) = CarryBench.Pair(T(10 * Px, -7 * Px, 3 * Px), counter: counter);
            var world = new CarryBench.RecordingWorld();

            CarryRules.UpdateCarried(hero, 1, world);

            Assert.Equal((T(8 * Px, -5 * Px, Px), counter, 0), (CarryBench.OffsetOf(hero), hero.CarryLockCounter, world.Destroyed.Count));
            Assert.Equal(100 * Px + 10 * Px, item.PosX); // the object itself does not move in the rule.
        }
    }

    [Theory]
    [InlineData(0x1FFFF, 0, 0)]
    [InlineData(0, 0x1FFFF, 0x1FFFF)]
    [InlineData(-0x1FFFF, -0x1FFFF, -0x1FFFF)]
    [InlineData(0, 0, 0)]
    public void C2_AtTheThreshold_TheObjectIsDestroyedByTheSingleArgumentCall_TheCounterIsKept_AndTheOffsetOfTheHeroIsNotRewritten(int dx, int dy, int dz)
    {
        var (hero, item) = CarryBench.Pair(T(dx, dy, dz), counter: 5);
        var world = new CarryBench.RecordingWorld();

        CarryRules.UpdateCarried(hero, 1, world);

        Assert.Equal((true, 5, 1), (CarryBench.OffsetUntouched(hero), hero.CarryLockCounter, world.Destroyed.Count));
        Assert.Same(item, world.Destroyed[0]);
    }

    [Theory]
    [InlineData(0x20000, 0, 0)]
    [InlineData(0, 0, 0x20000)]
    [InlineData(-0x20000, 0, 0)]
    [InlineData(0x30000, 0x1FFFF, 0)]
    public void C2_OneStepAbove_NothingIsDestroyed_AndTheObjectIsDrawnInInstead(int dx, int dy, int dz)
    {
        var (hero, _) = CarryBench.Pair(T(dx, dy, dz), counter: 5);
        var world = new CarryBench.RecordingWorld();

        CarryRules.UpdateCarried(hero, 1, world);

        // each axis comes 2 px (0x20000) nearer to 0, or reaches it when under that; the counter is kept.
        static int Pull(int v) => System.Math.Abs((long)v) < 0x20000 ? 0 : v - System.Math.Sign(v) * 0x20000;
        Assert.Equal((T(Pull(dx), Pull(dy), Pull(dz)), 5, 0), (CarryBench.OffsetOf(hero), hero.CarryLockCounter, world.Destroyed.Count));
    }

    [Fact]
    public void C2_ASheetWithoutAttack_TakesMode1ForMode0_ASheetWithOneAttackDoesNot()
    {
        // the object 0x1FFFF away, the counter at 5: with an attack it is destroyed (mode 1); without, the free tick of mode 0 runs (the three advance, the counter goes back to 0, nothing is destroyed).
        var (noAttack, _) = CarryBench.Pair(T(0x1FFFF, 0, 0), attackCount: 0, counter: 5);
        var (oneAttack, _) = CarryBench.Pair(T(0x1FFFF, 0, 0), attackCount: 1, counter: 5);
        var worldA = new CarryBench.RecordingWorld();
        var worldB = new CarryBench.RecordingWorld();

        CarryRules.UpdateCarried(noAttack, 1, worldA);
        CarryRules.UpdateCarried(oneAttack, 1, worldB);

        Assert.Equal((T(0x1FFFF, 0, Px), 0, 0), (CarryBench.OffsetOf(noAttack), noAttack.CarryLockCounter, worldA.Destroyed.Count));
        Assert.Equal((true, 5, 1), (CarryBench.OffsetUntouched(oneAttack), oneAttack.CarryLockCounter, worldB.Destroyed.Count));
    }

    [Fact]
    public void C2_ASheetWithoutAttackAtTheCounter0_RunsMode0NotTheCount_NextToTheSheetWithAnAttackThatCounts()
    {
        var (noAttack, _) = CarryBench.Pair(T(10 * Px, 0, 0), attackCount: 0, counter: 0);
        var (oneAttack, _) = CarryBench.Pair(T(10 * Px, 0, 0), attackCount: 1, counter: 0);

        CarryRules.UpdateCarried(noAttack, 1, null);
        CarryRules.UpdateCarried(oneAttack, 1, null);

        Assert.Equal((false, 0, true, 1), (CarryBench.OffsetUntouched(noAttack), noAttack.CarryLockCounter, CarryBench.OffsetUntouched(oneAttack), oneAttack.CarryLockCounter));
    }

    [Fact]
    public void C2_WithoutAWorld_TheDestroyIsNotMade_TheCounterAndTheOffsetStayAsTheThresholdLeavesThem_AndNothingThrows()
    {
        var (counting, _) = CarryBench.Pair(T(0x1FFFF, 0, 0), counter: 5);

        var exception = Record.Exception(() => CarryRules.UpdateCarried(counting, 1, null));

        Assert.Equal((null, true, 5), (exception, CarryBench.OffsetUntouched(counting), counting.CarryLockCounter));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c3) mode 2: the object is let go.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void C3_TheObjectIsReleasedWithMinus1_TheDirectionOfTheHero_AndTheHeadOfTheHero_TheLinkIsKept_TheCounterIs0(int mode)
    {
        var (hero, item) = CarryBench.Pair(T(21 * Px, 3 * Px, 11 * Px), counter: 4);
        hero.TargetDirection = 0x18;
        item.TargetDirection = 0x08;
        item.Flags2 = 0;

        CarryRules.UpdateCarried(hero, mode, null);

        Assert.Equal((-1, 0x18u, hero.PosX, hero.PosY, hero.PosZ + 32 * Px), (item.Flags2, item.TargetDirection, item.PosX, item.PosY, item.PosZ));
        Assert.Equal((0, true, true), (hero.CarryLockCounter, ReferenceEquals(hero.CarriedEntity, item.LogicContextEntity), ReferenceEquals(item.PlatformEntity, hero.LogicContextEntity)));
        Assert.True(CarryBench.OffsetUntouched(hero)); // the offset words are the follow branch's, not written by the release.
    }

    [Fact]
    public void C3_NothingCarried_NoObjectIsTouched_NextToTheCarriedCaseThatReleases()
    {
        var (carrying, itemA) = CarryBench.Pair(T(21 * Px, 0, 0), counter: 4);
        var (empty, itemB) = CarryBench.Pair(T(21 * Px, 0, 0), counter: 4);
        empty.CarriedEntity = null; // the object B is still linked from its side, but the hero carries nothing: the rule reads the hero only.

        CarryRules.UpdateCarried(carrying, 2, null);
        CarryRules.UpdateCarried(empty, 2, null);

        Assert.Equal((-1, 0, 0), (itemA.Flags2, carrying.CarryLockCounter, empty.CarryLockCounter));
        Assert.Equal((0, 21 * Px, 0), (itemB.Flags2, itemB.PosX - empty.PosX, itemB.PosZ));
    }

    [Fact]
    public void C3_TheDestroyCallIsNeverMadeByTheRelease_NextToMode1ThatMakesIt()
    {
        var (released, _) = CarryBench.Pair(T(0, 0, 0), counter: 5);
        var (drawn, _) = CarryBench.Pair(T(0, 0, 0), counter: 5);
        var world = new CarryBench.RecordingWorld();
        var worldOfMode1 = new CarryBench.RecordingWorld();

        CarryRules.UpdateCarried(released, 2, world);
        CarryRules.UpdateCarried(drawn, 1, worldOfMode1);

        Assert.Equal((0, 1), (world.Destroyed.Count, worldOfMode1.Destroyed.Count));
    }
}
