#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T1, phase A, (a3) (docs/plan-e20-heros.md section 1.24.5, D-G2C3-1, D-G2C3-3): the state 3 of C71 on entities made by hand. The advance and the removal of <c>Collidable</c> come first and do not depend on the test of the torch; the torch is the object the HERO carries when the support is the hero,
/// otherwise the support itself; it is put out (status 3, mark <c>Bytes[0] = 1</c>) only when its type is the torch's (393), whatever its status. The goldens of the same state are in <see cref="WaterDropAiGoldenTests"/>.
/// </summary>
public sealed class WaterDropAiTargetTests
{
    private const uint Flags = 0x32180;

    private static EntityScriptProxy Torch(string name, int type = TorchAi.SpriteType, EntityStatus status = EntityStatus.Normal)
    {
        var torch = CarryBench.Make(name);
        torch.SpriteType = type;
        torch.Status = status;
        torch.Bytes[0] = 0;
        return torch;
    }

    private static (EntityScriptProxy Drop, EntityScriptProxy Hero, PickupHost Host) Setup()
    {
        var hero = CarryBench.Make("hero");
        hero.SpriteType = 0;
        var drop = CarryBench.Make("drop");
        drop.SpriteType = WaterDropAi.SpriteType;
        drop.Bytes[0] = 3;
        drop.Flags = Flags;
        return (drop, hero, new PickupHost { Hero = hero });
    }

    private static void AssertPutOut(EntityScriptProxy torch)
    {
        Assert.Equal((EntityStatus.Deactivated, (byte)1), (torch.Status, torch.Bytes[0]));
    }

    private static void AssertLeftAlone(EntityScriptProxy entity, EntityStatus status = EntityStatus.Normal)
    {
        Assert.Equal((status, (byte)0), (entity.Status, entity.Bytes[0]));
    }

    private static void AssertAdvanced(EntityScriptProxy drop)
    {
        Assert.Equal((4, Flags & ~0x80u), (drop.Bytes[0], drop.Flags));
    }

    [Fact]
    public void A3_TheHeroCarryingATorch_UnderTheDrop_HasItsTorchPutOut_StatusAndMark()
    {
        var (drop, hero, host) = Setup();
        var torch = Torch("torch");
        hero.CarriedEntity = torch.LogicContextEntity;
        drop.RidingEntity = hero.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertPutOut(torch);
        AssertAdvanced(drop);
        AssertLeftAlone(hero);
    }

    [Fact]
    public void A3_TheHeroCarryingAnObjectThatIsNotATorch_ChangesNothing_YetTheDropAdvances()
    {
        var (drop, hero, host) = Setup();
        var crate = Torch("crate", type: 392);
        hero.CarriedEntity = crate.LogicContextEntity;
        drop.RidingEntity = hero.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertLeftAlone(crate);
        AssertAdvanced(drop);
    }

    [Fact]
    public void A3_TheHeroWithNothingInHisHands_ChangesNothing_AndNeverPutsHimselfOut_EvenWithATorchSpriteType()
    {
        var (drop, hero, host) = Setup();
        hero.SpriteType = TorchAi.SpriteType;
        hero.Bytes[0] = 0;
        drop.RidingEntity = hero.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertLeftAlone(hero);
        AssertAdvanced(drop);
    }

    [Fact]
    public void A3_ATorchStandingOnTheGround_UnderTheDrop_IsPutOut()
    {
        var (drop, _, host) = Setup();
        var torch = Torch("posed");
        drop.RidingEntity = torch.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertPutOut(torch);
        AssertAdvanced(drop);
    }

    [Theory]
    [InlineData(EntityStatus.FlagToDestroy)]
    [InlineData(EntityStatus.Deactivated)]
    [InlineData(EntityStatus.Loaded)]
    public void A3_ATorchInAnyStatus_IsWritten_ThereIsNoConditionOnItsStatus(EntityStatus status)
    {
        var (drop, _, host) = Setup();
        var torch = Torch("torch", status: status);
        drop.RidingEntity = torch.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertPutOut(torch);
    }

    [Fact]
    public void A3_AnythingElseUnderTheDrop_ThatIsNotATorch_IsLeftAlone_AndNoSupportIsLeftAlone()
    {
        var (drop, _, host) = Setup();
        var stone = Torch("stone", type: 394);
        drop.RidingEntity = stone.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertLeftAlone(stone);
        AssertAdvanced(drop);

        var (bare, _, bareHost) = Setup();

        WaterDropAi.Tick(bare, bareHost);

        AssertAdvanced(bare);
    }

    [Fact]
    public void A3_TheHeroOfTheWorldIsTheOnlyOneWhoseHandsAreLooked_ATorchCarriedByAnotherEntity_IsNotPutOutByTheSupportOfThatEntity()
    {
        var (drop, _, host) = Setup();
        var carrier = Torch("carrier", type: 394);
        var torch = Torch("torch");
        carrier.CarriedEntity = torch.LogicContextEntity;
        drop.RidingEntity = carrier.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        AssertLeftAlone(torch);
        AssertLeftAlone(carrier);
        AssertAdvanced(drop);
    }

    [Fact]
    public void A3_TheDrop_AsksNeitherASoundNorItsStop_TheSoundOfTheSplashIsTheAnimations()
    {
        var (drop, hero, host) = Setup();
        var sound = new RecordingSoundPlayer();
        host.Sound = sound;
        var torch = Torch("torch");
        hero.CarriedEntity = torch.LogicContextEntity;
        drop.RidingEntity = hero.LogicContextEntity;

        WaterDropAi.Tick(drop, host);

        Assert.Empty(sound.Requests);
        Assert.Empty(sound.StopRequests);
    }
}
