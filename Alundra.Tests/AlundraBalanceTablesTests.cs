#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Alundra.Maps.Loop;
using Alundra.Entities;
using Alundra.Combat;

namespace Alundra.Tests;

/// <summary>
/// E20.c1a C1 and C2 (docs/plan-e20-heros.md section 1.5): the balance tables of the export (<c>Data/balance.json</c>: the 512 entries, the remapping of the indexes <c>0x1F</c> to <c>0x7F</c>, the chain of levels up to the threshold
/// <c>D</c> of the map), what an entity gets from them at its creation (the record, the life and the maximum life, the first switch), and what the animation clock reloads at a switch it sees (the current attack, the animation byte
/// <c>+0xB4</c>, the cursor of the frame box), at the tick of the switch and not at the end of the frame.
/// </summary>
public sealed class AlundraBalanceTablesTests
{
    private static AlundraBalanceRecord Record(int response = 16, params (int Attribute, int Power)[] attacks)
    {
        var responses = Enumerable.Repeat((byte)16, 11).ToArray();
        responses[0] = (byte)response;
        return new AlundraBalanceRecord(255, 15 + 2 * attacks.Length, 9, responses, attacks.Select(a => new AlundraBalanceAttack(a.Attribute, a.Power)).ToArray(), null);
    }

    [Fact]
    public void TheCurrentAttackOfAnAnimation_IsTheNextEntryOfTheRecord_ElseTheFirst_ElseNothing()
    {
        var record = Record(16, (0x01, 10), (0x81, 20), (0x82, 30));
        Assert.Equal(20, record.AttackForAnimation(0)!.Power); // Attacks[animation + 1]
        Assert.Equal(30, record.AttackForAnimation(1)!.Power);
        Assert.Equal(10, record.AttackForAnimation(2)!.Power); // 2 + 1 is not below the count: Attacks[0]
        Assert.Equal(10, record.AttackForAnimation(40)!.Power);
        Assert.Null(Record(16).AttackForAnimation(0)); // no attack at all
    }

    [Fact]
    public void AClockTickThatSeesASwitch_LoadsTheAttackTheByteAndTheBoxCursor_AndTheTicksThatDoNot_ChangeNone()
    {
        var record = Record(16, (0x01, 10), (0x81, 20), (0x82, 30));
        var proxy = new AlundraEntityScriptProxy { BalanceRecord = record };
        proxy.LoadAnimationSwitch(0, 0, fromClock: false);
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Acceleration = 0 },
            [1] = new AnimSetEntry { Anim = 1, Acceleration = 0x45 },
        };
        proxy.CurrentAnimationId = 0;
        proxy.AnimationDirection = 0;
        proxy.TargetDirection = 0;
        Assert.Equal(20, proxy.CurrentAttack!.Power);

        FrameSyncPasses.StepAnimationClock(proxy); // nothing pending: nothing reloaded.
        Assert.Equal(20, proxy.CurrentAttack!.Power);
        Assert.Equal(0, proxy.HitAnimByte);

        proxy.TargetAnimationId = 1;
        FrameSyncPasses.StepAnimationClock(proxy); // the switch tick: the attack, the byte and the cursor of the animation 1, while CurrentAnimationId still says 0 (the end of the frame validates it).
        Assert.Equal(30, proxy.CurrentAttack!.Power);
        Assert.Equal(0x45, proxy.HitAnimByte);
        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(1u, proxy.BoxAnimationId);
        Assert.Equal(0, proxy.BoxTicks);
        Assert.True(proxy.BoxSwitchLoaded);

        FrameSyncPasses.StepAnimationClock(proxy); // a later tick of the same frame: still pending for SyncAnimation, but no new switch: the cursor counts.
        Assert.Equal(30, proxy.CurrentAttack!.Power);
        Assert.Equal(1, proxy.BoxTicks);
    }

    // -----------------------------------------------------------------------------------------
    // The balance tables.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheBalanceTables_OfTheExport_HoldTheRecordsTheOracleUses()
    {
        var tables = AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find());
        Assert.Same(tables, AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()));

        var hero = tables.Select(0, 0)!;
        Assert.Equal(10, hero.MaxHp);
        Assert.Equal(77, hero.Attacks.Count);
        Assert.All(hero.DamageResponses, response => Assert.Equal(0, response));
        Assert.Equal(0x81, hero.AttackForAnimation(0x10)!.Attribute); // the basic sword: Attacks[0x11] = (0x81, 1)
        Assert.Equal(1, hero.AttackForAnimation(0x10)!.Power);

        var beannoid = tables.Select(79 | 0x100, 0)!;
        Assert.Equal(9, beannoid.MaxHp);
        Assert.Equal(new byte[] { 16, 16, 16, 64, 16, 16, 16, 16, 16, 16, 16 }, beannoid.DamageResponses.ToArray());
        Assert.Equal(1, beannoid.AttackForAnimation(0)!.Attribute);
        Assert.Equal(16, beannoid.ByteAt(3 + 0)); // the response of the nibble 1 is the byte 3, DamageResponses[0].
        Assert.Equal(64, beannoid.ByteAt(1 + 2 + 3)); // nibble 4 -> byte 6 -> DamageResponses[3].
        Assert.Equal(9, beannoid.ByteAt(0 + 2)); // the nibble 0 reads the maximum life.

        var immune = tables.Select(382, 0)!;
        Assert.Equal(1, immune.MaxHp);
        Assert.Equal(0x80, immune.ByteAt(3));
    }

    /// <summary>
    /// E14.a1b: the hit pass reads the byte <c>2 + (attribute &amp; 0xF)</c> of the sheet whatever its size, so the attribute nibbles 14 and 15 of a 17-byte record (the Beannoid's) read the first bytes of the record stored after it
    /// in <c>BALANCE.BIN</c> (the D1 vectors of the E14.a1 oracle with the attribute 15). The records are contiguous: the follower of the Beannoid (entry 335) is the entry 336.
    /// </summary>
    [Fact]
    public void AByteBeyondTheRecord_IsReadFromTheNextRecordOfTheFile()
    {
        var tables = AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find());
        var beannoid = tables.Select(335, 0)!;
        var next = tables.Select(336, 0)!;
        Assert.Equal(17, beannoid.RecordSize);

        Assert.Equal(next.Level, beannoid.ByteAt(17));
        Assert.Equal(next.RecordSize, beannoid.ByteAt(18));
        Assert.Equal(next.MaxHp, beannoid.ByteAt(19));
        Assert.Equal(beannoid.Attacks[0].Power, beannoid.ByteAt(16)); // inside the record: unchanged.
        Assert.Equal(0, new AlundraBalanceRecord(255, 15, 9, new byte[11], Array.Empty<AlundraBalanceAttack>(), null).ByteAt(40)); // no follower (a record written by hand): 0, as before.
    }

    [Fact]
    public void TheBalanceIndexIn0x1FTo0x7F_IsReadAs0x1E_AndTheChainAdvancesWhileTheLevelIsBelowTheThreshold()
    {
        var tables = AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find());
        foreach (var index in new[] { 0x1F, 0x31, 0x7F })
        {
            Assert.Same(tables.Select(0x1E, 0), tables.Select(index, 0));
        }

        Assert.NotSame(tables.Select(0x1E, 0), tables.Select(0x80, 0));

        // An entry whose chain has levels 3 and 7: the threshold picks the first record whose level is not below it (the last one when none is).
        var last = new AlundraBalanceRecord(255, 15, 30, new byte[11], Array.Empty<AlundraBalanceAttack>(), null);
        var middle = new AlundraBalanceRecord(7, 15, 20, new byte[11], Array.Empty<AlundraBalanceAttack>(), last);
        var first = new AlundraBalanceRecord(3, 15, 10, new byte[11], Array.Empty<AlundraBalanceAttack>(), middle);
        var chain = new AlundraBalanceTables(new Dictionary<int, AlundraBalanceRecord> { [200] = first });
        Assert.Equal(10, chain.Select(200, 0)!.MaxHp);
        Assert.Equal(10, chain.Select(200, 3)!.MaxHp);
        Assert.Equal(20, chain.Select(200, 4)!.MaxHp);
        Assert.Equal(20, chain.Select(200, 7)!.MaxHp);
        Assert.Equal(30, chain.Select(200, 8)!.MaxHp);
        Assert.Equal(30, chain.Select(200, 11)!.MaxHp);
        Assert.Null(chain.Select(201, 0));
        Assert.Null(chain.Select(-1, 0));
        Assert.Null(chain.Select(512, 0));
    }

    [Fact]
    public void TheThresholdIsThePropertyDOfTheMap_AsAString_ZeroWhenMissing()
    {
        Assert.Equal(0, AlundraBalanceTables.ResolveThreshold(null));
        var map = new CasaEngine.Framework.Assets.TileMap.TileMapData();
        Assert.Equal(0, AlundraBalanceTables.ResolveThreshold(map));
        map.CustomProperties["D"] = "4";
        map.CustomProperties["BalanceLevel"] = "2"; // the byte 9 of the header, NOT the threshold.
        Assert.Equal(4, AlundraBalanceTables.ResolveThreshold(map));
        map.CustomProperties["D"] = "not a number";
        Assert.Equal(0, AlundraBalanceTables.ResolveThreshold(map));
    }

    [Fact]
    public void AnEntityGetsItsRecord_ItsLifeAndMaxLife_AndTheAttackOfItsFirstAnimation_AtItsCreation()
    {
        var tables = AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find());
        var proxy = new AlundraEntityScriptProxy();
        proxy.SpriteType = 79 | 0x100;
        proxy.TargetAnimationId = 0;
        proxy.CurrentAnimationId = ~0u; // as a creation leaves it
        proxy.InitializeBalance(tables, 0);
        Assert.Same(tables.Select(335, 0), proxy.BalanceRecord);
        Assert.Equal(9, proxy.Hp);
        Assert.Equal(9, proxy.HpMax);
        Assert.Equal(1, proxy.CurrentAttack!.Attribute);

        var degraded = new AlundraEntityScriptProxy { SpriteType = -1 }; // a spawn whose sprite header did not resolve: nothing is loaded.
        degraded.InitializeBalance(tables, 0);
        Assert.Null(degraded.BalanceRecord);
        Assert.Equal(0, degraded.Hp);
        degraded.SpriteType = 79 | 0x100;
        degraded.InitializeBalance(null, 0);
        Assert.Null(degraded.BalanceRecord);
    }
}
