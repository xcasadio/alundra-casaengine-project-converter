using System.Linq;
#nullable enable
using System.Collections.Generic;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1b T1, phase A (docs/plan-e20-heros.md section 1.24.4): <c>TryLift</c> (<c>0x8002EDBC</c>) and <c>PlayerTryAction</c> (<c>0x8002EEAC</c>) as pure functions over an explicit context (the contact, the entities, the ownership of the object
/// <c>0x3B</c>, the wired set). The expected values are the binary's facts of the plan (genre 0 refuses, genre 1 lifts, genres 2 and 3 lift only with the object <c>0x3B</c>; carried: 1 and the state; contact and front: the answer of <c>TryLift</c> even 0;
/// otherwise the loop of the supports, with no button), never the output of the DLL. Production does not change in this phase: the stub still answers 0 and the production set is empty.
/// </summary>
public sealed class LiftPureTests
{
    private const int Px = CarryBench.Px;
    private const int WiredType = 36;
    private static readonly IReadOnlySet<int> Wired = new HashSet<int> { WiredType };
    private static readonly IReadOnlySet<int> NoneWired = new HashSet<int>();

    private static EntityScriptProxy Hero(bool air = false)
    {
        var hero = CarryBench.Make("hero", 100 * Px, 100 * Px, 0);
        hero.IsPlayer = true;
        hero.HeroAirborne = air;
        return hero;
    }

    /// <summary>An object of the given genre (the 2 bits of the flags at the shift 9) and type, 21 px east of the hero.</summary>
    private static EntityScriptProxy Item(EntityScriptProxy hero, int genre, int type = WiredType, EntityStatus status = EntityStatus.Normal)
    {
        var item = CarryBench.Make("item", hero.PosX + 21 * Px, hero.PosY, hero.PosZ);
        item.Flags = (uint)genre << 9;
        item.SpriteType = type;
        item.Status = status;
        return item;
    }

    private static PadState Pad(uint pressed = 0, uint hold = 0) => new() { ButtonsJustPressed = pressed, ButtonsHold = hold };

    private static void AssertLifted(EntityScriptProxy hero, EntityScriptProxy item, uint state)
    {
        Assert.Same(item.LogicContextEntity, hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, item.PlatformEntity);
        Assert.Equal(state, hero.TargetAnimationId);
    }

    private static void AssertUntouched(EntityScriptProxy hero, EntityScriptProxy item)
    {
        Assert.Null(hero.CarriedEntity);
        Assert.Null(item.PlatformEntity);
    }

    // ---- (l1) TryLift ----

    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(0, true, 0)]
    [InlineData(1, false, 1)]
    [InlineData(1, true, 1)]
    [InlineData(2, false, 0)]
    [InlineData(2, true, 1)]
    [InlineData(3, false, 0)]
    [InlineData(3, true, 1)]
    public void TryLift_TheEightCases_GenreByObject3B(int genre, bool owns, int expected)
    {
        var hero = Hero();
        var item = Item(hero, genre);

        var result = PlayerManager.TryLift(hero, item, owns, Wired);

        Assert.Equal(expected, result);
        if (expected == 1)
        {
            AssertLifted(hero, item, CarryRules.LiftGroundState);
        }
        else
        {
            AssertUntouched(hero, item);
        }
    }

    [Fact]
    public void TryLift_CallsPutInHands_TheOffsetIsTheObjectMinusTheHeroOnTheThreeAxes()
    {
        var hero = Hero();
        var item = Item(hero, 1);
        item.PosZ = hero.PosZ + 3 * Px;
        item.PosY = hero.PosY - 15 * Px;

        Assert.Equal(1, PlayerManager.TryLift(hero, item, false, Wired));

        Assert.Equal(new CarryRules.Triple(21 * Px, -15 * Px, 3 * Px), CarryBench.OffsetOf(hero));
        Assert.True(item.HasLogicalZ);
    }

    [Theory]
    [InlineData(false, false, CarryRules.LiftGroundState)]
    [InlineData(true, false, CarryRules.LiftAirState)]
    [InlineData(false, true, CarryRules.CarryStillState)]
    [InlineData(true, true, CarryRules.AirStillState)]
    public void TryLift_TheTargetAnimation_FollowsTheGroundAndTheObjectOnTheHead(bool air, bool onHead, uint state)
    {
        var hero = Hero(air);
        var item = Item(hero, 1);
        if (onHead)
        {
            item.RidingEntity = hero.LogicContextEntity;
        }

        Assert.Equal(1, PlayerManager.TryLift(hero, item, false, Wired));

        AssertLifted(hero, item, state);
        Assert.Null(item.RidingEntity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryLift_ATypeOutsideTheWiredSet_Gives0_NextToTheSameObjectInTheSet(int genre)
    {
        var hero = Hero();
        var outside = Item(hero, genre, type: WiredType + 1);

        Assert.Equal(0, PlayerManager.TryLift(hero, outside, true, Wired));
        AssertUntouched(hero, outside);

        var inside = Item(hero, genre);
        Assert.Equal(1, PlayerManager.TryLift(hero, inside, true, Wired));
        AssertLifted(hero, inside, CarryRules.LiftGroundState);
    }

    [Fact]
    public void TheProductionSet_IsTheSevenTypesOfC23_AndTheTestSeamReplacesIt()
    {
        // E20.g1b phase B: the activation wires the seven types of C23 (binary: the sprites whose header holds C23), no more.
        Assert.Equal(new[] { 234, 291, 292, 294, 297, 304, 438 }, PlayerManager.ProductionWiredLiftableTypes.OrderBy(t => t));
        Assert.Same(PlayerManager.ProductionWiredLiftableTypes, PlayerManager.WiredLiftableTypes);
        try
        {
            PlayerManager.WiredLiftableTypesForTests = Wired;
            Assert.Same(Wired, PlayerManager.WiredLiftableTypes);
        }
        finally
        {
            PlayerManager.WiredLiftableTypesForTests = null;
        }
    }

    // ---- (l2) PlayerTryAction ----

    [Theory]
    [InlineData(false, CarryRules.CarryStillState)]
    [InlineData(true, CarryRules.AirStillState)]
    public void PlayerTryAction_Carrying_Gives1_AndTheCarryingState(bool air, uint state)
    {
        var hero = Hero(air);
        var held = Item(hero, 1);
        PlayerManager.PutInHands(hero, held);
        hero.TargetAnimationId = 0x99;
        var other = Item(hero, 1);
        hero.XCollisionEntity = other;

        var result = PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Square), new[] { hero, held, other }, true, Wired);

        Assert.Equal(1, result);
        Assert.Equal(state, hero.TargetAnimationId);
        Assert.Same(held.LogicContextEntity, hero.CarriedEntity);
        Assert.Null(other.PlatformEntity);
    }

    [Fact]
    public void PlayerTryAction_ContactAndFront_ReturnsTheAnswerOfTryLift()
    {
        var hero = Hero();
        var contact = Item(hero, 1);
        hero.XCollisionEntity = contact;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Square), new[] { hero, contact }, false, Wired));
        AssertLifted(hero, contact, CarryRules.LiftGroundState);
    }

    [Fact]
    public void PlayerTryAction_ContactAndFront_ReturnsTheZeroOfTryLiftDirectly_WithoutTheLoop()
    {
        var hero = Hero();
        var contact = Item(hero, 0);
        var onHead = Item(hero, 1);
        onHead.RidingEntity = hero.LogicContextEntity;
        hero.XCollisionEntity = contact;

        var result = PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Square), new[] { hero, contact, onHead }, false, Wired);

        Assert.Equal(0, result);
        AssertUntouched(hero, onHead);
        AssertUntouched(hero, contact);
    }

    [Fact]
    public void PlayerTryAction_AContactOutsideTheWiredSet_WithTheFront_Gives0()
    {
        var hero = Hero();
        var contact = Item(hero, 1, type: WiredType + 1);
        hero.XCollisionEntity = contact;

        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Square), new[] { hero, contact }, true, Wired));
        AssertUntouched(hero, contact);
    }

    [Fact]
    public void PlayerTryAction_ContactWithoutFront_GoesToTheLoop_AndNeverLiftsTheContact()
    {
        var hero = Hero();
        var contact = Item(hero, 1);
        hero.XCollisionEntity = contact;
        var onHead = Item(hero, 1);
        onHead.RidingEntity = hero.LogicContextEntity;

        // The Square is held but did not just go down: no front.
        var result = PlayerManager.PlayerTryActionCore(hero, Pad(pressed: 0, hold: PadState.Square), new[] { hero, contact, onHead }, false, Wired);

        Assert.Equal(1, result);
        AssertLifted(hero, onHead, CarryRules.CarryStillState);
        Assert.Null(contact.PlatformEntity);

        // Without the object on the head, the same pad lifts nothing: the contact is not lifted without the front.
        var hero2 = Hero();
        var contact2 = Item(hero2, 1);
        hero2.XCollisionEntity = contact2;
        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero2, Pad(pressed: 0, hold: PadState.Square), new[] { hero2, contact2 }, false, Wired));
        AssertUntouched(hero2, contact2);
    }

    [Fact]
    public void PlayerTryAction_TheFrontIsTheSquareOnly()
    {
        var hero = Hero();
        var contact = Item(hero, 1);
        hero.XCollisionEntity = contact;

        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Cross | PadState.Circle | PadState.Triangle), new[] { hero, contact }, false, Wired));
        AssertUntouched(hero, contact);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_LiftsTheObjectOnTheHeadWithNoButton()
    {
        var hero = Hero();
        var onHead = Item(hero, 1);
        onHead.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, false, Wired));

        AssertLifted(hero, onHead, CarryRules.CarryStillState);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_InTheAir_GivesTheAirCarryingState()
    {
        var hero = Hero(air: true);
        var onHead = Item(hero, 1);
        onHead.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, false, Wired));

        AssertLifted(hero, onHead, CarryRules.AirStillState);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_AcceptsTheStatus3()
    {
        var hero = Hero();
        var onHead = Item(hero, 1, status: EntityStatus.Deactivated);
        onHead.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, false, Wired));
        AssertLifted(hero, onHead, CarryRules.CarryStillState);
    }

    [Theory]
    [InlineData(EntityStatus.Destroyed)]
    [InlineData(EntityStatus.Loaded)]
    [InlineData(EntityStatus.FlagToDestroy)]
    [InlineData(EntityStatus.Unknown)]
    public void PlayerTryAction_TheLoop_IgnoresAStatusOutsideTwoAndThree(EntityStatus status)
    {
        var hero = Hero();
        var onHead = Item(hero, 1, status: status);
        onHead.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, false, Wired));
        AssertUntouched(hero, onHead);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_IgnoresABlockedObject_NextToTheFreeOne()
    {
        var hero = Hero();
        var blocked = Item(hero, 1);
        blocked.RidingEntity = hero.LogicContextEntity;
        blocked.BlockedByEntity = CarryBench.Make("blocker").LogicContextEntity;
        var free = Item(hero, 1);
        free.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, blocked, free }, false, Wired));

        Assert.Null(blocked.PlatformEntity);
        AssertLifted(hero, free, CarryRules.CarryStillState);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_IgnoresAnObjectRidingAnotherEntity_AndAnObjectRidingNothing()
    {
        var hero = Hero();
        var otherSupport = CarryBench.Make("support");
        var riding = Item(hero, 1);
        riding.RidingEntity = otherSupport.LogicContextEntity;
        var free = Item(hero, 1);
        free.RidingEntity = null;

        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, riding, free }, false, Wired));
        AssertUntouched(hero, riding);
        AssertUntouched(hero, free);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_TheFirstLiftWins_AndAnEarlierZeroIsSkipped()
    {
        var hero = Hero();
        var unwired = Item(hero, 1, type: WiredType + 1);
        unwired.RidingEntity = hero.LogicContextEntity;
        var first = Item(hero, 1);
        first.RidingEntity = hero.LogicContextEntity;
        var second = Item(hero, 1);
        second.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, unwired, first, second }, false, Wired));

        Assert.Null(unwired.PlatformEntity);
        AssertLifted(hero, first, CarryRules.CarryStillState);
        Assert.Null(second.PlatformEntity);
        Assert.Same(hero.LogicContextEntity, second.RidingEntity);
    }

    [Fact]
    public void PlayerTryAction_TheLoop_AGenre2OnTheHead_NeedsTheObject3B()
    {
        var hero = Hero();
        var onHead = Item(hero, 2);
        onHead.RidingEntity = hero.LogicContextEntity;

        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, false, Wired));
        AssertUntouched(hero, onHead);
        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, onHead }, true, Wired));
        AssertLifted(hero, onHead, CarryRules.CarryStillState);
    }

    [Fact]
    public void PlayerTryAction_AHeroWithoutALogicContext_LiftsNothing_AndTheSameHeroWithOneDoes()
    {
        var hero = Hero();
        var nothingRides = Item(hero, 1);
        nothingRides.RidingEntity = null;
        var logic = hero.LogicContextEntity;
        hero.LogicContextEntity = null!;

        // The trap "null equals null": an object that rides nothing must not be taken for one that rides a hero that has no logic entity.
        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, nothingRides }, false, Wired));
        Assert.Null(hero.CarriedEntity);
        Assert.Null(nothingRides.PlatformEntity);

        hero.LogicContextEntity = logic;
        var onHead = Item(hero, 1);
        onHead.RidingEntity = logic;
        Assert.Equal(1, PlayerManager.PlayerTryActionCore(hero, Pad(), new[] { hero, nothingRides, onHead }, false, Wired));
        AssertLifted(hero, onHead, CarryRules.CarryStillState);
        Assert.Null(nothingRides.PlatformEntity);
    }

    [Fact]
    public void PlayerTryAction_NothingToDo_Gives0()
    {
        var hero = Hero();
        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(PadState.Square), new[] { hero }, true, Wired));
        Assert.Equal(0, PlayerManager.PlayerTryActionCore(hero, Pad(), System.Array.Empty<EntityScriptProxy>(), true, Wired));
        Assert.Null(hero.CarriedEntity);
    }
}
