#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E14.a0b T4 (docs/plan-e14-ia-native.md section 1.2): the skeleton of the hit handlers D1 (Beannoid, animation 5) and D9 (Muruta, animation 2) (<see cref="AlundraEnemyHelpers.HitReaction"/>) against the real code of <c>ALUN_CD.EXE</c> (France)
/// run in the emulator on 10,000 random states each (<c>out/h1/d1.txt.gz</c>, <c>d9.txt.gz</c>; <c>ComputeNewHp</c> stubbed there, its result the state's <c>killed</c>), and the hero's animation latch on the entity context
/// (<see cref="IEntityWorldContext.HeroAnimationLatch"/>) that the damage calculation reads.
/// </summary>
public sealed class AlundraEnemyHitReactionTests
{
    private sealed class HitContext : IEntityWorldContext
    {
        public HitContext(AlundraEntityScriptProxy? hero) => PlayerEntity = hero;

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity { get; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public AlundraEquipmentRecords? EquipmentRecords { get; set; }
        public uint HeroAnimationLatch { get; set; }
    }

    private static Entity Attacker(int attribute, int power)
    {
        var entity = new Entity { Name = "attacker", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.CurrentAttack = new AlundraBalanceAttack(attribute, power);
        return entity;
    }

    /// <summary>A record whose response to the attack nibble 1 is 0x10 (damage = power): the natural response of the Beannoid and of the Muruta.</summary>
    private static AlundraBalanceRecord Sheet() => new(255, 15, 9, Enumerable.Repeat((byte)16, 11).ToArray(), Array.Empty<AlundraBalanceAttack>(), null);

    /// <summary>The entity of an oracle state: attack (attribute 1, power 1) against a sheet of response 0x10, so one point of life is lost; <paramref name="killed"/> chooses the life that makes <c>ComputeNewHp</c> return true (1) or false (100).</summary>
    private static AlundraEntityScriptProxy Body(int x, int y, bool killed, byte oldByte, uint oldAnimation, uint oldDirection)
    {
        var body = new AlundraEntityScriptProxy
        {
            PosX = x, PosY = y, BalanceRecord = Sheet(), Hp = killed ? 1 : 100, TouchingEntity = Attacker(1, 1),
            TargetAnimationId = oldAnimation, TargetDirection = oldDirection,
        };
        body.Bytes[3] = oldByte;
        return body;
    }

    [Theory]
    [InlineData("d1", 5u)]
    [InlineData("d9", 2u)]
    public void HitReaction_EqualsTheBinarysHandler_OnEveryState(string helper, uint animation)
    {
        var rows = AlundraEnemyOracle.Helper(helper);
        Assert.True(rows.Count >= 10000);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var killed = row.In[4] != 0;
            var where = $"{helper} state {i} in [{string.Join(' ', row.In)}] expected [{string.Join(' ', row.Out)}]";
            var hero = new AlundraEntityScriptProxy { PosX = row.In[2], PosY = row.In[3] };
            var body = Body(row.In[0], row.In[1], killed, (byte)row.In[5], 0x7777, 0x5555);

            AlundraEnemyHelpers.HitReaction(body, animation, new HitContext(hero));

            // the outputs in the oracle's order: +0x277, +0x88, +0x8C
            Assert.True(row.Out[0] == body.Bytes[3], $"{where}: Bytes[3] is {body.Bytes[3]}");
            Assert.True(row.Out[1] == (int)body.TargetAnimationId, $"{where}: animation is {body.TargetAnimationId}");
            Assert.True(row.Out[2] == (int)body.TargetDirection, $"{where}: direction is {body.TargetDirection}");

            // ComputeNewHp ran once on this entity: one point of life lost, or the last one
            Assert.True(body.Hp == (killed ? 0 : 99), $"{where}: life {body.Hp}");
        }
    }

    [Fact]
    public void HitReaction_WithoutAHero_LeavesTheDirection_AndStillAppliesTheDamage()
    {
        var body = Body(1000, 2000, killed: true, oldByte: 0, oldAnimation: 0, oldDirection: 9);

        AlundraEnemyHelpers.HitReaction(body, 5, new HitContext(null));

        Assert.Equal(1, body.Bytes[3]);
        Assert.Equal(5u, body.TargetAnimationId);
        Assert.Equal(9u, body.TargetDirection);
    }

    [Fact]
    public void HitReaction_ReadsTheEquipmentAndTheLatchOfTheContext()
    {
        // K2 of the E20.c1b oracle: an attack with the bit 0x80 adds the power of Attacks[g + 1 < count ? g + 1 : 0] of each equipment record, g = the latch as a signed number
        var records = new AlundraEquipmentRecords();
        records.Set(AlundraEquipmentRecords.Weapon, new AlundraBalanceRecord(255, 21, 0, new byte[11], new[] { new AlundraBalanceAttack(1, 2), new AlundraBalanceAttack(1, 3), new AlundraBalanceAttack(1, 5) }, null), 4);
        var expected = new Dictionary<uint, int> { [1] = 100 - 6, [0] = 100 - 4, [16] = 100 - 3 };      // (1 + Attacks[2].Power 5), (1 + Attacks[1] 3), (1 + Attacks[0] 2) at response 0x10
        foreach (var (latch, hp) in expected)
        {
            var body = new AlundraEntityScriptProxy { BalanceRecord = Sheet(), Hp = 100, TouchingEntity = Attacker(0x81, 1) };

            AlundraEnemyHelpers.HitReaction(body, 5, new HitContext(new AlundraEntityScriptProxy()) { EquipmentRecords = records, HeroAnimationLatch = latch });

            Assert.Equal(hp, body.Hp);
        }
    }

    [Fact]
    public void HeroAnimationLatch_OfTheContext_DefaultsToZero_AndTheWorldReturnsItsHitPassLatch()
    {
        IEntityWorldContext noOp = NoOpEntityWorldContext.Instance;
        Assert.Equal(0u, noOp.HeroAnimationLatch);

        using var arena = new HitArena("E14a0b-latch", 212, 96, null, 24, 0, naturalTargetCount: 1);
        IEntityWorldContext context = arena.Arc.Proxy;
        Assert.Equal(arena.Arc.Proxy.HeroAnimationLatch, context.HeroAnimationLatch);
    }
}
