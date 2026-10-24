#nullable enable
using Xunit;
using Alundra.Entities;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1, (c5), D-G-2, D-G-12, D-G-13): the single seam <see cref="PlayerManager.PutInHands"/> (the core of <c>TryLift</c> <c>0x8002EE0C-0x8002EE1C</c>, which the seeds and the bomb of E20.h3 will call too): the two links,
/// the raw offset on the three axes, the marker of the logical Z, the object stopped from riding, and the four animations of the hero. No production path calls it yet.
/// </summary>
public sealed class PutInHandsTests
{
    private const int Px = CarryBench.Px;

    private static (EntityScriptProxy Hero, EntityScriptProxy Item) Bench(bool air = false)
    {
        var hero = CarryBench.Make("hero", 100 * Px, 100 * Px, 0);
        var item = CarryBench.Make("item", 130 * Px, 96 * Px, 0);
        hero.HeroAirborne = air;
        return (hero, item);
    }

    [Fact]
    public void C5_TheLinksTheRawOffsetAndTheMarker_AreWrittenOnTheTwoSides_AndAreAbsentBefore()
    {
        var (hero, item) = Bench();
        item.PosZ = 7 * Px + 123;
        var before = (hero.CarriedEntity, item.PlatformEntity, item.HasLogicalZ);

        PlayerManager.PutInHands(hero, item);

        Assert.Equal((null, null, false), before);
        Assert.Same(item.LogicContextEntity, hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, item.PlatformEntity);
        Assert.Equal(new CarryRules.Triple(item.PosX - hero.PosX, item.PosY - hero.PosY, item.PosZ - hero.PosZ), CarryBench.OffsetOf(hero));
        Assert.Equal(new CarryRules.Triple(30 * Px, -4 * Px, 7 * Px + 123), CarryBench.OffsetOf(hero));
        Assert.True(item.HasLogicalZ);
        Assert.False(hero.HasLogicalZ); // the marker is the object's.
    }

    [Fact]
    public void C5_TheObjectStopsRidingWhatItRode_AnotherEntityOrTheHero_NextToTheHeroWhoseRidingIsLeftAlone()
    {
        var (hero, item) = Bench();
        var other = CarryBench.Make("other");
        hero.RidingEntity = other.LogicContextEntity;
        item.RidingEntity = other.LogicContextEntity;

        PlayerManager.PutInHands(hero, item);

        Assert.Equal((null, true), (item.RidingEntity, ReferenceEquals(hero.RidingEntity, other.LogicContextEntity)));

        var (hero2, item2) = Bench();
        item2.RidingEntity = hero2.LogicContextEntity;

        PlayerManager.PutInHands(hero2, item2);

        Assert.Null(item2.RidingEntity);
    }

    [Fact]
    public void C5_TheFourAnimations_TheGestureOfTheGroundOrOfTheAir_ThePlainCarryWhenTheObjectWasOnTheHeroesHead()
    {
        // the object not on his head: 5 on the ground, 0x0B in the air.
        var (ground, itemGround) = Bench(air: false);
        var (air, itemAir) = Bench(air: true);
        PlayerManager.PutInHands(ground, itemGround);
        PlayerManager.PutInHands(air, itemAir);
        Assert.Equal((0x05u, 0x0Bu), (ground.TargetAnimationId, air.TargetAnimationId));

        // the object on his head (its RidingEntity is the hero): 0x0C on the ground, 0x30 in the air.
        var (groundHead, itemGroundHead) = Bench(air: false);
        var (airHead, itemAirHead) = Bench(air: true);
        itemGroundHead.RidingEntity = groundHead.LogicContextEntity;
        itemAirHead.RidingEntity = airHead.LogicContextEntity;
        PlayerManager.PutInHands(groundHead, itemGroundHead);
        PlayerManager.PutInHands(airHead, itemAirHead);
        Assert.Equal((0x0Cu, 0x30u), (groundHead.TargetAnimationId, airHead.TargetAnimationId));
    }

    [Fact]
    public void C5_AnObjectRidingAnotherEntityIsNotOnTheHead_AndTwoNullsAreNotTheSameEntity()
    {
        // riding someone else: the gesture, not the plain carry (next to the head case above).
        var (hero, item) = Bench();
        item.RidingEntity = CarryBench.Make("other").LogicContextEntity;
        PlayerManager.PutInHands(hero, item);
        Assert.Equal(0x05u, hero.TargetAnimationId);

        // a hero without a logic entity (a bare montage) and an object that rides nothing: both null, and that is not "on his head".
        var bareHero = new EntityScriptProxy { PosX = 100 * Px, PosY = 100 * Px };
        var free = CarryBench.Make("free", 130 * Px, 100 * Px, 0);
        PlayerManager.PutInHands(bareHero, free);
        Assert.Equal(0x05u, bareHero.TargetAnimationId);

        // and the same bare hero, with an object that does ride something non-null: still the gesture.
        var bareHero2 = new EntityScriptProxy { PosX = 100 * Px, PosY = 100 * Px };
        var riding = CarryBench.Make("riding", 130 * Px, 100 * Px, 0);
        riding.RidingEntity = CarryBench.Make("floor").LogicContextEntity;
        PlayerManager.PutInHands(bareHero2, riding);
        Assert.Equal(0x05u, bareHero2.TargetAnimationId);
    }

    [Fact]
    public void C5_TheOffsetIsTheDifferenceOfTheRawPositions_OnTheHeadItIs0_0_32Px_AndTheSecondCallRewritesIt()
    {
        var (hero, item) = Bench();
        item.PosX = hero.PosX;
        item.PosY = hero.PosY;
        item.PosZ = hero.PosZ + 32 * Px;
        item.RidingEntity = hero.LogicContextEntity;

        PlayerManager.PutInHands(hero, item);
        Assert.Equal(new CarryRules.Triple(0, 0, 32 * Px), CarryBench.OffsetOf(hero));

        // a later call (another object, other positions) rewrites the three words and the links: nothing of the first call survives in the offset.
        var next = CarryBench.Make("next", 90 * Px, 105 * Px, 2 * Px);
        PlayerManager.PutInHands(hero, next);
        Assert.Equal(new CarryRules.Triple(-10 * Px, 5 * Px, 2 * Px), CarryBench.OffsetOf(hero));
        Assert.Same(next.LogicContextEntity, hero.CarriedEntity);
    }
}
