#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1): the montage of the direct tests of the carrying rules - a hero and an object that are two real proxies of two real engine entities (<see cref="EntityScriptProxy.LogicContextEntity"/>
/// set, the way the world does it), a balance sheet with a chosen number of attacks, and a world that only records the single-argument <c>DestroyEntity</c>.
/// </summary>
internal static class CarryBench
{
    /// <summary>One pixel in raw units (16.16).</summary>
    internal const int Px = 1 << 16;

    /// <summary>The value the tests pose in the hero's offset and counter before a call, to see whether the call wrote them.</summary>
    internal const int Sentinel = 0x5A5A5A;

    /// <summary>A proxy of a real engine entity, in status Normal, with its logic entity (itself) set.</summary>
    internal static EntityScriptProxy Make(string name, int x = 0, int y = 0, int z = 0)
    {
        var entity = new Entity { Name = name, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.LogicContextEntity = entity;
        proxy.Status = EntityStatus.Normal;
        proxy.PosX = x;
        proxy.PosY = y;
        proxy.PosZ = z;
        return proxy;
    }

    /// <summary>A balance sheet with <paramref name="attackCount"/> attacks (the byte <c>0xE</c> of the binary's sheet).</summary>
    internal static BalanceRecord Sheet(int attackCount)
    {
        var attacks = new BalanceAttack[attackCount];
        for (var i = 0; i < attackCount; i++)
        {
            attacks[i] = new BalanceAttack(1, 128);
        }

        return new BalanceRecord(255, 15 + 2 * attackCount, 9, new byte[11], attacks, null);
    }

    /// <summary>The hero at (100, 100, 0) px and an object <paramref name="offset"/> raw units away from him, linked the way <c>PutInHands</c> does (without its other effects); the sentinel is in the hero's offset and counter.</summary>
    internal static (EntityScriptProxy Hero, EntityScriptProxy Item) Pair(CarryRules.Triple offset, int attackCount = 1, int counter = Sentinel)
    {
        var hero = Make("hero", 100 * Px, 100 * Px, 0);
        hero.IsPlayer = true;
        var item = Make("item", hero.PosX + offset.X, hero.PosY + offset.Y, hero.PosZ + offset.Z);
        item.BalanceRecord = Sheet(attackCount);
        hero.CarriedEntity = item.LogicContextEntity;
        item.PlatformEntity = hero.LogicContextEntity;
        hero.RelativeWarpOffsetX = Sentinel;
        hero.RelativeWarpOffsetY = Sentinel;
        hero.RelativeWarpOffsetZ = Sentinel;
        hero.CarryLockCounter = counter;
        return (hero, item);
    }

    /// <summary>The offset the call wrote in the hero (the binary's <c>+0x30/+0x34/+0x38</c>).</summary>
    internal static CarryRules.Triple OffsetOf(EntityScriptProxy hero) => new(hero.RelativeWarpOffsetX, hero.RelativeWarpOffsetY, hero.RelativeWarpOffsetZ);

    /// <summary>True when the call wrote none of the three words of the hero's offset.</summary>
    internal static bool OffsetUntouched(EntityScriptProxy hero) => OffsetOf(hero) == new CarryRules.Triple(Sentinel, Sentinel, Sentinel);

    /// <summary>A world that records the single-argument <c>DestroyEntity</c> and does nothing else.</summary>
    internal sealed class RecordingWorld : IEntityWorldContext
    {
        public List<EntityScriptProxy> Destroyed { get; } = new();
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => System.Array.Empty<EntityScriptProxy>();
        public EntityScriptProxy? PlayerEntity => null;
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) => Destroyed.Add(entity);
        public NavigationGrid2D? NavigationGrid => null;
    }
}
