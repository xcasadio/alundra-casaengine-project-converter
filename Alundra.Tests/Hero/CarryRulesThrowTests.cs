#nullable enable
using Xunit;
using Alundra.Entities;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g2a T1 (docs/plan-e20-heros.md section 1.24.2, phase A, (t1) and (t2), D-G-10): the writes of the throw of the binary (<c>0x8003237C-0x80032404</c>) called directly on a real hero and a real object, the way the Square branch of the
/// carrying group will call <see cref="CarryRules.ThrowCarried"/> in phase B. The values are those of the table "Valeurs attendues" of the plan (the keys of <c>g4</c>, <c>g4b</c>, <c>g12</c>) and of the disassembly: the object takes the direction
/// word <c>table[+0x98]</c> of the hero, his X and Y, his Z plus <c>0x200000</c>, and <c>+0x24</c> = 1 or 2 on the ground, 2 or 3 in the air (no direction held / a direction held); the hero takes the state 9 or 0x0A. Each "without effect" case is in the same
/// test as the positive case it is the neighbour of.
/// </summary>
public sealed class CarryRulesThrowTests
{
    private const int Px = CarryBench.Px;

    /// <summary>A hero at a position with raw fractions and an object in his hands, somewhere else than the throw puts it (21 px east, 5 px south, 10 px up), the hero's direction of the tick set to <paramref name="boxDirection"/>.</summary>
    private static (EntityScriptProxy Hero, EntityScriptProxy Item) Scene(int boxDirection = 3, uint heroState = CarryRules.CarryStillState)
    {
        var (hero, item) = CarryBench.Pair(new CarryRules.Triple(21 * Px, 0, 32 * Px));
        hero.PosX = 109 * Px + 4321;
        hero.PosY = 100 * Px - 1234;
        hero.PosZ = 3 * Px + 77;
        item.PosX = hero.PosX + 21 * Px;
        item.PosY = hero.PosY + 5 * Px;
        item.PosZ = hero.PosZ + 10 * Px;
        hero.BoxDirection = boxDirection;
        hero.TargetAnimationId = heroState;
        return (hero, item);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t1) the position of the object, its direction, +0x24 and the state of the hero.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T1_TheObjectIsPlacedAtTheHeroXAndY_AndAtHisZPlus0x200000_RawToTheUnit_NextToItsPlaceBeforeTheThrow()
    {
        var (hero, item) = Scene();
        var before = (item.PosX, item.PosY, item.PosZ);

        var thrown = CarryRules.ThrowCarried(hero, ground: true, directionHeld: false);

        Assert.True(thrown);
        Assert.NotEqual((hero.PosX, hero.PosY, hero.PosZ + 0x200000), before);
        Assert.Equal((hero.PosX, hero.PosY, hero.PosZ + 0x200000), (item.PosX, item.PosY, item.PosZ));
        Assert.Equal((109 * Px + 4321, 100 * Px - 1234, 3 * Px + 77 + 32 * Px), (item.PosX, item.PosY, item.PosZ));
        Assert.Equal(CarryRules.HeadHeight, 32 * Px);
    }

    [Theory]
    [InlineData(0, 0u)]     // down.
    [InlineData(1, 0x10u)]  // up.
    [InlineData(2, 8u)]     // left.
    [InlineData(3, 0x18u)]  // right.
    public void T1_TheObjectTakesTheDirectionWordOfTheTable_IndexedByTheHeroesDirection_TheFourIndicesTwoByTwo(int boxDirection, uint word)
    {
        var (hero, item) = Scene(boxDirection);
        item.TargetDirection = 0x55;

        CarryRules.ThrowCarried(hero, ground: true, directionHeld: false);

        Assert.Equal(word, item.TargetDirection);
        Assert.NotEqual(0x55u, item.TargetDirection);

        // the neighbour: the other index of the pair (0 and 1, 2 and 3) gives the other word; the four words are different.
        var (hero2, item2) = Scene(boxDirection ^ 1);
        CarryRules.ThrowCarried(hero2, ground: true, directionHeld: false);
        Assert.NotEqual(word, item2.TargetDirection);
    }

    [Theory]
    [InlineData(true, false, 1, CarryRules.ThrowGroundState)]   // ground, no direction held.
    [InlineData(true, true, 2, CarryRules.ThrowGroundState)]    // ground, a direction held.
    [InlineData(false, false, 2, CarryRules.ThrowAirState)]     // air, no direction held.
    [InlineData(false, true, 3, CarryRules.ThrowAirState)]      // air, a direction held.
    public void T1_TheCodeOfTheThrowAndTheStateOfTheHero_FollowTheGroundAndTheDirectionHeld_TheFourCombinations(bool ground, bool directionHeld, int code, uint state)
    {
        var (hero, item) = Scene();
        Assert.Equal(0, item.Flags2);

        var thrown = CarryRules.ThrowCarried(hero, ground, directionHeld);

        Assert.True(thrown);
        Assert.Equal((code, state), (item.Flags2, hero.TargetAnimationId));
    }

    [Fact]
    public void T1_TheStatesAreTheBinarys_9OnTheGroundAndTenInTheAir()
    {
        Assert.Equal((9u, 10u), (CarryRules.ThrowGroundState, CarryRules.ThrowAirState));
        Assert.NotEqual(CarryRules.CarryStillState, CarryRules.ThrowGroundState);
    }

    [Fact]
    public void T1_TheThrowTouchesNeitherTheLinksNorTheFlagsNorTheMarkerNorTheOffsetNorTheCounter_TheProgramOfTheObjectDoesThat()
    {
        var (hero, item) = Scene();
        item.Flags = 0x3323E4;
        var link = hero.CarriedEntity;
        var carrier = item.PlatformEntity;
        var offset = (hero.RelativeWarpOffsetX, hero.RelativeWarpOffsetY, hero.RelativeWarpOffsetZ);
        var counter = hero.CarryLockCounter;
        var marker = item.HasLogicalZ;

        CarryRules.ThrowCarried(hero, ground: true, directionHeld: true);

        Assert.NotNull(link);
        Assert.Same(link, hero.CarriedEntity);
        Assert.Same(carrier, item.PlatformEntity);
        Assert.Equal(0x3323E4u, item.Flags);
        Assert.Equal(marker, item.HasLogicalZ);
        Assert.Equal(offset, (hero.RelativeWarpOffsetX, hero.RelativeWarpOffsetY, hero.RelativeWarpOffsetZ));
        Assert.Equal(counter, hero.CarryLockCounter);
        Assert.Equal(2, item.Flags2); // and the throw itself did write something (the neighbour of the "touches nothing" cases).
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t1) the guard 0x2000 and the cases with nothing to throw.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x2000u, false)]    // the ground property of 0x20: the throw is forbidden, nothing is written.
    [InlineData(0x6000u, false)]    // together with the property of 0x40.
    [InlineData(0u, true)]          // the neighbour: no property, the throw is made.
    [InlineData(0x4000u, true)]     // the property of 0x40 forbids the jump, never the throw.
    [InlineData(0x1FFFu, true)]     // every other bit below.
    public void T1_TheGroundPropertyOf0x20ForbidsTheThrow_AndWritesNothing_TheOneOf0x40AndTheOtherBitsDoNot(uint vramFlags, bool thrown)
    {
        var (hero, item) = Scene();
        hero.CombinedVramFlagsOR = vramFlags;
        item.TargetDirection = 0x55;
        var itemBefore = (item.PosX, item.PosY, item.PosZ, item.Flags2, item.TargetDirection);
        var heroBefore = (hero.TargetAnimationId, hero.PosX, hero.PosY, hero.PosZ);

        var result = CarryRules.ThrowCarried(hero, ground: true, directionHeld: true);

        Assert.Equal(thrown, result);
        if (thrown)
        {
            Assert.NotEqual(itemBefore, (item.PosX, item.PosY, item.PosZ, item.Flags2, item.TargetDirection));
            Assert.Equal(CarryRules.ThrowGroundState, hero.TargetAnimationId);
        }
        else
        {
            Assert.Equal(itemBefore, (item.PosX, item.PosY, item.PosZ, item.Flags2, item.TargetDirection));
            Assert.Equal(heroBefore, (hero.TargetAnimationId, hero.PosX, hero.PosY, hero.PosZ));
        }
    }

    [Fact]
    public void T1_NothingInTheHands_ThrowsNothingAndWritesNothing_NextToAnObjectThatIsThrown()
    {
        var (hero, item) = Scene();
        var empty = CarryBench.Make("empty-hero", 5 * Px, 6 * Px, 7);
        empty.IsPlayer = true;
        empty.TargetAnimationId = CarryRules.CarryStillState;

        Assert.False(CarryRules.ThrowCarried(empty, ground: true, directionHeld: true));
        Assert.Equal((CarryRules.CarryStillState, 5 * Px, 6 * Px, 7, 0), (empty.TargetAnimationId, empty.PosX, empty.PosY, empty.PosZ, item.Flags2));

        Assert.True(CarryRules.ThrowCarried(hero, ground: true, directionHeld: true));
        Assert.Equal(2, item.Flags2);
    }

    [Fact]
    public void T1_AnObjectOfTheHandsWithoutAProxy_IsLeftAlone_NextToTheOneWithAProxy()
    {
        var (hero, item) = Scene();
        var bare = new CasaEngine.Framework.Scene.Entities.Entity { Name = "bare" };
        hero.CarriedEntity = bare;

        Assert.False(CarryRules.ThrowCarried(hero, ground: true, directionHeld: false));
        Assert.Equal(CarryRules.CarryStillState, hero.TargetAnimationId);
        Assert.Same(bare, hero.CarriedEntity);
        Assert.Equal(0, item.Flags2);

        hero.CarriedEntity = item.LogicContextEntity;
        Assert.True(CarryRules.ThrowCarried(hero, ground: true, directionHeld: false));
        Assert.Equal(1, item.Flags2);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (t2) the look of the PREVIOUS tick, not the look of the end of the tick (D-F3-2, D-G-10; probe P-G-2).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T2_TheDirectionOfTheObjectIsTheOneOfTheTickBefore_BoxDirection_NotTheEndOfFrameAnimationDirection_NorTheDirectionOfThePadOfThisTick()
    {
        // the hero looked east (3) at the tick before; the pad of this tick turned him west: the direction word he has written (+0x8C) is already west, his animation direction (the end of frame) lags or leads.
        var (hero, item) = Scene(boxDirection: 3);
        hero.AnimationDirection = 2;
        hero.TargetDirection = 8;

        CarryRules.ThrowCarried(hero, ground: true, directionHeld: true);

        Assert.Equal(0x18u, item.TargetDirection); // east: BoxDirection.

        // the opposite: the hero looked west at the tick before and the pad of this tick turns him east.
        var (hero2, item2) = Scene(boxDirection: 2);
        hero2.AnimationDirection = 3;
        hero2.TargetDirection = 0x18;

        CarryRules.ThrowCarried(hero2, ground: true, directionHeld: true);

        Assert.Equal(8u, item2.TargetDirection); // west: BoxDirection.

        // the hero's own direction word is not written by the throw.
        Assert.Equal(8u, hero.TargetDirection);
        Assert.Equal(0x18u, hero2.TargetDirection);
    }
}
