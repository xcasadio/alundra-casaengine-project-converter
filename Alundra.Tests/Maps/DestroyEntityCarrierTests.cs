#nullable enable
using System;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;
using Alundra.Tests.Rng;

namespace Alundra.Tests.Maps;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, D-G-11; (w9)): the two overloads of <c>WorldProxy.DestroyEntity</c> end by clearing the carrier of the destroyed entity (<c>[+0x28] + 0x2C = 0</c>, <c>0x8003A62C</c> and <c>0x8003A74C</c>), whatever the carrier
/// holds (the binary does not compare the identity), and do nothing for an entity nobody carries or whose carrier has no proxy (the bare entity of a test montage). The destroyed entity keeps its own link to the carrier (the binary does not touch it). Written BEFORE
/// the clearing (W5): red on the named stub. Shares the generator's collection like the other tests of the destruction (it must draw no number).
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class DestroyEntityCarrierTests
{
    private static EntityScriptProxy Hero() => CarryBench.Make("hero");

    private static EntityScriptProxy Object(string name, EntityScriptProxy? carrier = null)
    {
        var item = CarryBench.Make(name);
        item.Flags = CarryRig.CrateFlags;
        if (carrier != null)
        {
            carrier.CarriedEntity = item.LogicContextEntity;
            item.PlatformEntity = carrier.LogicContextEntity;
        }

        return item;
    }

    public static TheoryData<bool> Overloads => new() { true, false };

    private static void Destroy(WorldProxy world, EntityScriptProxy entity, bool twoArguments)
    {
        if (twoArguments)
        {
            world.DestroyEntity(entity, -1);
        }
        else
        {
            world.DestroyEntity(entity);
        }
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public void TheDestructionOfTheObjectInTheHands_ClearsTheLinkOfTheCarrier_ButNotTheLinkOfTheObject(bool twoArguments)
    {
        var world = new WorldProxy();
        var hero = Hero();
        var item = Object("item", hero);
        Assert.Same(item.LogicContextEntity, hero.CarriedEntity);

        Destroy(world, item, twoArguments);

        Assert.Equal(EntityStatus.FlagToDestroy, item.Status);
        Assert.Null(hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, item.PlatformEntity);
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public void TheClearing_HasNoTestOfIdentity_TheCarrierHoldingAnotherObjectLosesItToo(bool twoArguments)
    {
        var world = new WorldProxy();
        var hero = Hero();
        var held = Object("held");
        var stale = Object("stale");
        hero.CarriedEntity = held.LogicContextEntity;
        stale.PlatformEntity = hero.LogicContextEntity; // an object that still names the hero as its carrier although the hero holds another one.

        Destroy(world, stale, twoArguments);

        Assert.Null(hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, stale.PlatformEntity);
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public void ACarrierWithoutAProxy_IsLeftAlone_WithoutAnException(bool twoArguments)
    {
        var world = new WorldProxy();
        var bare = new Entity { Name = "platform" };
        var item = Object("item");
        item.PlatformEntity = bare;

        Destroy(world, item, twoArguments);

        Assert.Equal(EntityStatus.FlagToDestroy, item.Status);
        Assert.Same(bare, item.PlatformEntity);
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public void AnObjectNobodyCarries_LeavesTheLinkOfAHeroWhoCarriesAnotherOne(bool twoArguments)
    {
        var world = new WorldProxy();
        var hero = Hero();
        var held = Object("held", hero);
        var loose = Object("loose");

        Destroy(world, loose, twoArguments);

        Assert.Equal(EntityStatus.FlagToDestroy, loose.Status);
        Assert.Same(held.LogicContextEntity, hero.CarriedEntity);
        Assert.Same(hero.LogicContextEntity, held.PlatformEntity);
    }
}
