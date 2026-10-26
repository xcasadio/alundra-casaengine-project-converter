#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Hero;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3b T1, phase A (docs/plan-e20-heros.md section 1.30.3, tests (c1) to (c3)): the start of the bomb (<c>0x8003453C</c>), its driver (<c>0x800352C4</c>) and its cancellation (<c>0x80034B54</c>), called directly on a hero and a world made by hand
/// (<see cref="BombHandsBench"/>). The expected values are the 25 vectors of each function that the critic of h3 made from the real code of the binary (<c>docs/plan-e20-annexe/e20-h3/critic/results/goldens_critic.json</c>, replayed in T0 from an empty memory), replayed
/// field by field, and hand cases next to them; each case without effect is set next to a positive case in the same test. No production path calls these functions yet (the entries <c>0x23</c> of the tables are the phase B).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombHandsTests : IDisposable
{
    private const string Goldens = "e20-h3/critic/results/goldens_critic.json";
    private const uint HeroBase = 0x80127D30;
    private const uint ItemInUseAddress = 0x80127164;
    private const uint FirstSlotType = 0x80127D98;
    private const uint SlotStride = 0x294;
    private const int BombItem = 0x23;
    private const int Px = CarryBench.Px;

    public BombHandsTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static int Raw(BombVector vector, uint address) => unchecked((int)vector.Word(address));

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) the start.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A hero and a world that hold the words one vector of the start read: the hero's position, ground, slope, item in use and carried entity, the 64 slots' types, and the birth the stub handed back.</summary>
    private static (BombHandsBench Bench, EntityScriptProxy? Born) StartWorld(BombVector vector)
    {
        var bench = BombHandsBench.Create();
        var hero = bench.Hero;
        hero.PosX = Raw(vector, HeroBase + 0x114);
        hero.PosY = Raw(vector, HeroBase + 0x118);
        hero.PosZ = Raw(vector, HeroBase + 0x11C) - 1; // the DLL's frame of Z is one raw unit under the binary's.
        hero.HeroAirborne = vector.Word(HeroBase + 0x144) == 0;
        hero.Slope_18c = Raw(vector, HeroBase + 0x18C);
        hero.ItemInUse = vector.Word(ItemInUseAddress);
        hero.SpriteType = Raw(vector, FirstSlotType);
        if (vector.Word(HeroBase + 0x2C) != 0)
        {
            hero.CarriedEntity = new Entity { Name = "held" };
        }

        for (var slot = 1; slot < 64; slot++)
        {
            bench.AddOther(Raw(vector, FirstSlotType + (uint)slot * SlotStride));
        }

        if (vector.SpawnReturn == 0)
        {
            bench.Host.TableIsFull = true;
            return (bench, null);
        }

        EntityScriptProxy? born = null;
        bench.Host.OnBirth = entity =>
        {
            entity.LogicContextEntity = new Entity { Name = "born bomb" };
            entity.PosX = Raw(vector, vector.SpawnReturn + 0x114);
            entity.PosY = Raw(vector, vector.SpawnReturn + 0x118);
            entity.PosZ = Raw(vector, vector.SpawnReturn + 0x11C) - 1;
            BombAi.SetFuse(entity, 0x1234);
            born = entity;
        };
        return (bench, born);
    }

    [Fact]
    public void H1_Start_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_TheRefusalsTheNullBirthAndTheStart()
    {
        var vectors = BombVector.Load(Goldens, "start_bomb");

        Assert.Equal(25, vectors.Count);
        int started = 0, refused = 0, nullBirth = 0;
        foreach (var vector in vectors)
        {
            var (bench, _) = StartWorld(vector);
            var hero = bench.Hero;
            var heldBefore = hero.CarriedEntity;
            var spawned = vector.Journal.SingleOrDefault(j => j.Kind == "spawn");

            var answer = BombAi.Start(hero);

            var bornEntity = bench.Host.Born.Skip(64).SingleOrDefault();
            Assert.Equal((int)vector.Return!.Value, answer);
            if (vector.Journal.Any(j => j.Kind == "sound"))
            {
                // a refusal: the sound 3, no birth, nothing written.
                Assert.Equal(new[] { 3 }, bench.Sounds);
                Assert.Empty(bench.Host.SpawnCalls);
                Assert.Same(heldBefore, hero.CarriedEntity);
                Assert.Equal(99u, hero.TargetAnimationId);
                refused++;
                continue;
            }

            Assert.Empty(bench.Sounds); // a birth, successful or not, is silent.
            var call = Assert.Single(bench.Host.SpawnCalls);
            Assert.Equal((hero, false, 2, hero.PosX, hero.PosY, hero.PosZ, 0), call);
            Assert.Equal((unchecked((int)spawned.Arguments[3]), unchecked((int)spawned.Arguments[4]), unchecked((int)spawned.Arguments[5]) - 1), (call.X, call.Y, call.Z));
            if (answer == 1)
            {
                // the table was full: nothing more happens.
                Assert.Null(bornEntity);
                Assert.Same(heldBefore, hero.CarriedEntity);
                Assert.Equal(99u, hero.TargetAnimationId);
                nullBirth++;
                continue;
            }

            Assert.NotNull(bornEntity);
            Assert.Same(bornEntity!.LogicContextEntity, hero.CarriedEntity);
            Assert.Same(hero.LogicContextEntity, bornEntity.PlatformEntity);
            Assert.Equal(0xF0, BombAi.Fuse(bornEntity)); // the real code writes 0xF0 after the birth (the word is listed only when it changed).
            Assert.Equal(unchecked(vector.Word(vector.SpawnReturn + 0x114) - vector.Word(HeroBase + 0x114)), unchecked((uint)hero.RelativeWarpOffsetX));
            Assert.Equal(unchecked(vector.Word(vector.SpawnReturn + 0x118) - vector.Word(HeroBase + 0x118)), unchecked((uint)hero.RelativeWarpOffsetY));
            Assert.Equal(unchecked(vector.Word(vector.SpawnReturn + 0x11C) - vector.Word(HeroBase + 0x11C)), unchecked((uint)hero.RelativeWarpOffsetZ));
            foreach (var (address, actual) in new[] { (HeroBase + 0x30, hero.RelativeWarpOffsetX), (HeroBase + 0x34, hero.RelativeWarpOffsetY), (HeroBase + 0x38, hero.RelativeWarpOffsetZ) })
            {
                if (vector.Writes.TryGetValue(address, out var written))
                {
                    Assert.Equal(written, unchecked((uint)actual));
                }
            }

            Assert.Equal(vector.Word(HeroBase + 0x144) != 0 ? 5u : 0xBu, hero.TargetAnimationId);
            if (vector.Writes.TryGetValue(HeroBase + 0x88, out var animation))
            {
                Assert.Equal(animation, hero.TargetAnimationId);
            }

            started++;
        }

        Assert.Equal((4, 18, 3), (started, refused, nullBirth)); // the three outcomes are in the sample.
    }

    private static int Start(BombHandsBench bench) => BombAi.Start(bench.Hero);

    [Fact]
    public void H1_Start_ARefusal_PlaysTheSound3_BornNothing_AndLeavesTheHeroAlone_NextToTheStartThatSucceeds()
    {
        // the positive case: nothing in the hands, slope 0, no item in use, no bomb: a birth, no sound.
        var fine = BombHandsBench.Create();
        fine.BornAtAnOffset();
        Assert.Equal(0, Start(fine));
        Assert.Empty(fine.Sounds);
        Assert.Single(fine.Host.SpawnCalls);

        var cases = new (string Name, Action<BombHandsBench> Arrange)[]
        {
            ("already carrying", b => b.Carry(spriteType: 1)),
            ("slope 4", b => b.Hero.Slope_18c = 4),
            ("an item in use", b => b.Hero.ItemInUse = 0x25),
            ("the bomb itself in use", b => b.Hero.ItemInUse = BombItem),
        };
        foreach (var (name, arrange) in cases)
        {
            var bench = BombHandsBench.Create();
            bench.BornAtAnOffset();
            arrange(bench);
            var held = bench.Hero.CarriedEntity;

            Assert.True(1 == Start(bench), name);

            Assert.Equal(new[] { 3 }, bench.Sounds);
            Assert.Empty(bench.Host.SpawnCalls);
            Assert.Same(held, bench.Hero.CarriedEntity);
            Assert.Equal(99u, bench.Hero.TargetAnimationId);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void H1_Start_AnySlopeButTheDeepWater4_Starts(int slope)
    {
        var bench = BombHandsBench.Create();
        bench.BornAtAnOffset();
        bench.Hero.Slope_18c = slope;

        Assert.Equal(0, Start(bench));
        Assert.Empty(bench.Sounds);
    }

    [Fact]
    public void H1_Start_TheLimitIsThreeEntitiesOfTheType2_WhateverTheirStatus_ADestroyedBombCountsUntilItsSlotIsRecycled_OtherTypesDoNot()
    {
        foreach (var status in new[] { EntityStatus.Normal, EntityStatus.Deactivated, EntityStatus.FlagToDestroy })
        {
            // two bombs and a crowd of other types: the third is accepted.
            var accepted = BombHandsBench.Create();
            accepted.BornAtAnOffset();
            accepted.AddBomb(status);
            accepted.AddBomb(status);
            foreach (var type in new[] { 1, 3, 4, 27, 153, 291 })
            {
                accepted.AddOther(type);
            }

            Assert.True(0 == Start(accepted), $"two bombs of status {status} and others");
            Assert.Empty(accepted.Sounds);

            // three bombs: the fourth is refused with the sound 3 (the bombs are not touched, nothing is born).
            var refused = BombHandsBench.Create();
            refused.BornAtAnOffset();
            refused.AddBomb(status);
            refused.AddBomb(status);
            refused.AddBomb(status);

            Assert.True(1 == Start(refused), $"three bombs of status {status}");
            Assert.Equal(new[] { 3 }, refused.Sounds);
            Assert.Empty(refused.Host.SpawnCalls);
        }

        // three bombs of three different statuses count together; a bomb out of the list (recycled) does not.
        var mixed = BombHandsBench.Create();
        mixed.BornAtAnOffset();
        mixed.AddBomb(EntityStatus.Normal);
        mixed.AddBomb(EntityStatus.Deactivated);
        var destroyed = mixed.AddBomb(EntityStatus.FlagToDestroy);
        Assert.Equal(1, Start(mixed));
        mixed.Host.Born.Remove(destroyed); // the recycling: the slot is free again.
        Assert.Equal(0, Start(mixed));
    }

    [Fact]
    public void H1_Start_ANullBirth_Answers1_WithNoSound_NoLink_NoFuse_AndTheHeroDoesNotMove()
    {
        var bench = BombHandsBench.Create();
        bench.Host.TableIsFull = true;

        Assert.Equal(1, Start(bench));

        Assert.Empty(bench.Sounds);
        Assert.Null(bench.Hero.CarriedEntity);
        Assert.Equal(99u, bench.Hero.TargetAnimationId);
        var call = Assert.Single(bench.Host.SpawnCalls);
        Assert.Equal((2, false), (call.Index, call.IsMapPage));
        Assert.Empty(bench.Host.Born.Where(e => e != bench.Hero));
    }

    [Fact]
    public void H1_Start_BirthsTheBombByTheInterfaceAtTheHero_PutsTheFuseAt0xF0AfterTheBirth_AndLinksBothWays()
    {
        var bench = BombHandsBench.Create();
        bench.BornAtAnOffset();
        var heroLogic = bench.Hero.LogicContextEntity;

        Assert.Equal(0, Start(bench));

        var call = Assert.Single(bench.Host.SpawnCalls);
        Assert.Equal((bench.Hero, false, 2, bench.Hero.PosX, bench.Hero.PosY, bench.Hero.PosZ, 0), call);
        var bomb = Assert.Single(bench.Host.Born, e => e != bench.Hero);
        Assert.Equal(0xF0, BombAi.Fuse(bomb));
        Assert.Same(bomb.LogicContextEntity, bench.Hero.CarriedEntity);
        Assert.Same(heroLogic, bomb.PlatformEntity);
        Assert.Equal((3 * Px, -5 * Px, 7 * Px), (bench.Hero.RelativeWarpOffsetX, bench.Hero.RelativeWarpOffsetY, bench.Hero.RelativeWarpOffsetZ));
        Assert.Equal(0, bomb.Flags2); // not thrown.
    }

    [Fact]
    public void H1_Start_TheHeroTakesTheLiftAnimation5OnTheGround_AndTheAirOne0x0B_InTheAir()
    {
        var ground = BombHandsBench.Create();
        ground.BornAtAnOffset();
        ground.Hero.HeroAirborne = false;
        Assert.Equal(0, Start(ground));
        Assert.Equal(5u, ground.Hero.TargetAnimationId);

        var air = BombHandsBench.Create();
        air.BornAtAnOffset();
        air.Hero.HeroAirborne = true;
        Assert.Equal(0, Start(air));
        Assert.Equal(0x0Bu, air.Hero.TargetAnimationId);
    }

    [Fact]
    public void H1_Start_ThroughTheStartSeamOfTheFrame_ThePilotAndTheCancellationAreTheOnesOfTheBomb()
    {
        // the functions have the shape of the seams of the frame (the start answers 0 to put the item in use).
        var bench = BombHandsBench.Create();
        bench.BornAtAnOffset();
        PlayerManager.ItemStartForTests = (hero, item) => BombAi.Start(hero);

        var answer = PlayerManager.ItemStartForTests(bench.Hero, BombItem);

        Assert.Equal(0, answer);
        Assert.NotNull(bench.Hero.CarriedEntity);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c2) the driver.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H2_Drive_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_Answer1OrWhileTheBombIsCarried0()
    {
        var vectors = BombVector.Load(Goldens, "drv_bomb");

        Assert.Equal(25, vectors.Count);
        int finished = 0, running = 0;
        foreach (var vector in vectors)
        {
            var bench = BombHandsBench.Create();
            var carried = vector.Word(HeroBase + 0x2C);
            EntityScriptProxy? bomb = null;
            if (carried != 0)
            {
                bomb = bench.Carry(Raw(vector, carried + 0x68));
            }

            bench.Hero.Slope_18c = Raw(vector, HeroBase + 0x18C);
            bench.Hero.ItemInUse = BombItem;
            var cancelled = new List<uint>();
            PlayerManager.ItemCancelForTests = (hero, item) => cancelled.Add(item);

            var answer = BombAi.Drive(bench.Hero);

            Assert.Equal((int)vector.Return!.Value, answer);
            Assert.Empty(cancelled); // no vector of the sample reaches the deep water.
            Assert.Empty(bench.Host.DestroyedAlone);
            Assert.Equal(bomb?.LogicContextEntity, bench.Hero.CarriedEntity);
            Assert.Equal(BombItem, (int)bench.Hero.ItemInUse);
            Assert.Empty(bench.Sounds);
            if (answer == 0)
            {
                running++;
            }
            else
            {
                finished++;
            }
        }

        Assert.Equal((24, 1), (finished, running)); // both answers are in the sample.
    }

    [Fact]
    public void H2_Drive_WithNothingInTheHandsOrAnotherTypeInThem_Answers1_AndDoesNothing_EvenInTheDeepWater()
    {
        foreach (var slope in new[] { 0, 4 })
        {
            var nothing = BombHandsBench.Create();
            nothing.Hero.Slope_18c = slope;
            nothing.Hero.ItemInUse = BombItem;
            var journal = new List<uint>();
            PlayerManager.ItemCancelForTests = (hero, item) => journal.Add(item);
            Assert.True(1 == BombAi.Drive(nothing.Hero), $"nothing, slope {slope}");

            var beans = BombHandsBench.Create();
            var bean = beans.Carry(spriteType: 1);
            beans.Hero.Slope_18c = slope;
            beans.Hero.ItemInUse = BombItem;
            Assert.True(1 == BombAi.Drive(beans.Hero), $"beans, slope {slope}");
            Assert.Same(bean.LogicContextEntity, beans.Hero.CarriedEntity);
            Assert.Empty(beans.Host.DestroyedAlone);
            Assert.Equal(BombItem, (int)beans.Hero.ItemInUse);
            Assert.Empty(journal);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void H2_Drive_ABombCarriedOutOfTheDeepWater_Answers0_ItIsStillInUse(int slope)
    {
        var bench = BombHandsBench.Create();
        var bomb = bench.Carry();
        bench.Hero.Slope_18c = slope;
        bench.Hero.ItemInUse = BombItem;
        var journal = new List<uint>();
        PlayerManager.ItemCancelForTests = (hero, item) => journal.Add(item);

        Assert.Equal(0, BombAi.Drive(bench.Hero));

        Assert.Same(bomb.LogicContextEntity, bench.Hero.CarriedEntity);
        Assert.Equal(BombItem, (int)bench.Hero.ItemInUse);
        Assert.Empty(journal);
        Assert.Empty(bench.Host.DestroyedAlone);
    }

    [Fact]
    public void H2_Drive_ABombCarriedIntoTheDeepWater4_CancelsTheItem_Answers1_TheBombIsDestroyedWithTheEffect12_TheItemIsFreed()
    {
        var bench = BombHandsBench.Create();
        var bomb = bench.Carry();
        bomb.PosX = 7 * Px;
        bomb.PosY = 8 * Px;
        bomb.PosZ = 9 * Px;
        bench.Hero.Slope_18c = 4;
        bench.Hero.ItemInUse = BombItem;
        var journal = new List<(uint Item, bool StillInUse)>();
        PlayerManager.ItemCancelForTests = (hero, item) =>
        {
            journal.Add((item, hero.ItemInUse != 0));
            BombAi.Cancel(hero);
        };

        Assert.Equal(1, BombAi.Drive(bench.Hero));

        Assert.Equal(new[] { ((uint)BombItem, true) }, journal); // the cancellation of the item runs while it is still in use, then it is freed.
        Assert.Equal(0u, bench.Hero.ItemInUse);
        Assert.Null(bench.Hero.CarriedEntity);
        Assert.Equal(new[] { bomb }, bench.Host.DestroyedAlone);
        var effect = Assert.Single(bench.Effects());
        Assert.Equal((false, 12, 0, 7 * Px, 8 * Px, 9 * Px + 1), (effect.TargetMapTable, effect.TargetEffect, effect.TargetAnimation, effect.X, effect.Y, effect.Z));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c3) the cancellation.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H3_Cancel_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_TheLinkTheEffect12AndTheSingleArgumentDestruction()
    {
        var vectors = BombVector.Load(Goldens, "cancel_bomb");

        Assert.Equal(25, vectors.Count);
        int cancelled = 0, kept = 0;
        foreach (var vector in vectors)
        {
            var bench = BombHandsBench.Create();
            var carried = vector.Word(HeroBase + 0x2C);
            EntityScriptProxy? bomb = null;
            if (carried != 0)
            {
                bomb = bench.Carry(Raw(vector, carried + 0x68));
                bomb.Flags2 = Raw(vector, carried + 0x24);
                bomb.PosX = Raw(vector, carried + 0x114);
                bomb.PosY = Raw(vector, carried + 0x118);
                bomb.PosZ = Raw(vector, carried + 0x11C) - 1;
            }

            BombAi.Cancel(bench.Hero);

            var effects = bench.Effects();
            if (vector.Journal.Any(j => j.Kind == "destroy1"))
            {
                var effect = vector.Journal.Single(j => j.Kind == "effect");
                Assert.Null(bench.Hero.CarriedEntity);
                Assert.Equal(new[] { bomb! }, bench.Host.DestroyedAlone);
                var slot = Assert.Single(effects);
                Assert.Equal((false, (int)effect.Arguments[1], (int)effect.Arguments[2], unchecked((int)effect.Arguments[3]), unchecked((int)effect.Arguments[4]), unchecked((int)effect.Arguments[5])), (slot.TargetMapTable, slot.TargetEffect, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
                Assert.Equal(unchecked((uint)(bomb!.PosZ + 1)), effect.Arguments[5]); // the effect is at the bomb, in the binary's frame of Z.
                Assert.Equal(12, slot.TargetEffect);
                cancelled++;
            }
            else
            {
                Assert.Equal(bomb?.LogicContextEntity, bench.Hero.CarriedEntity);
                Assert.Empty(bench.Host.DestroyedAlone);
                Assert.Empty(effects);
                kept++;
            }

            Assert.Empty(bench.Sounds);
        }

        Assert.Equal((2, 23), (cancelled, kept)); // both outcomes are in the sample.
    }

    [Fact]
    public void H3_Cancel_ABombInTheHandsWithNoCodeOfThrow_ClearsTheHerosLink_PutsTheEffect12AtTheBomb_AndDestroysItWithOneArgument()
    {
        var bench = BombHandsBench.Create();
        var bomb = bench.Carry();
        bomb.PosX = 31 * Px;
        bomb.PosY = 41 * Px;
        bomb.PosZ = 5 * Px;
        bomb.Status = EntityStatus.Normal;

        BombAi.Cancel(bench.Hero);

        Assert.Null(bench.Hero.CarriedEntity);
        Assert.Equal(new[] { bomb }, bench.Host.DestroyedAlone);
        Assert.Empty(bench.Host.Destroyed); // the two-argument destruction (the loot, the break effect) is not the one called.
        Assert.Same(bench.Hero.LogicContextEntity, bomb.PlatformEntity); // the bomb's own link is NOT cleared here (the binary's one-argument destruction does not clear it either).
        var effect = Assert.Single(bench.Effects());
        Assert.Equal((12, 0, 31 * Px, 41 * Px, 5 * Px + 1), (effect.TargetEffect, effect.TargetAnimation, effect.X, effect.Y, effect.Z));
    }

    [Fact]
    public void H3_Cancel_DoesNothing_WithoutABombInTheHands_OnAnotherType_OrWhenTheBombHasACodeOfThrow()
    {
        var empty = BombHandsBench.Create();
        BombAi.Cancel(empty.Hero);
        Assert.Empty(empty.Host.DestroyedAlone);
        Assert.Empty(empty.Effects());

        var beans = BombHandsBench.Create();
        var bean = beans.Carry(spriteType: 1);
        BombAi.Cancel(beans.Hero);
        Assert.Same(bean.LogicContextEntity, beans.Hero.CarriedEntity);
        Assert.Empty(beans.Host.DestroyedAlone);
        Assert.Empty(beans.Effects());

        foreach (var code in new[] { 1, 2, 3, -1, 0x55 })
        {
            var thrown = BombHandsBench.Create();
            var bomb = thrown.Carry();
            bomb.Flags2 = code;
            BombAi.Cancel(thrown.Hero);
            Assert.Same(bomb.LogicContextEntity, thrown.Hero.CarriedEntity);
            Assert.Empty(thrown.Host.DestroyedAlone);
            Assert.Empty(thrown.Effects());
        }

        // next to them, the same bomb with the code at 0 is cancelled.
        var released = BombHandsBench.Create();
        var carried = released.Carry();
        carried.Flags2 = 0;
        BombAi.Cancel(released.Hero);
        Assert.Equal(new[] { carried }, released.Host.DestroyedAlone);
    }

    [Fact]
    public void H3_Cancel_ReadsTheCodeOfThrowAt0x24_NotTheBlockerAt0x20_TheBinaryAgainstTheDecompilation()
    {
        // the decompilation tests +0x20 (D1); the binary tests +0x24. A blocker with no code of throw: the bomb is cancelled; a code with no blocker: it is not.
        var blocked = BombHandsBench.Create();
        var bomb = blocked.Carry();
        bomb.BlockedByEntity = new Entity { Name = "blocker" };
        bomb.Flags2 = 0;
        BombAi.Cancel(blocked.Hero);
        Assert.Equal(new[] { bomb }, blocked.Host.DestroyedAlone);

        var thrown = BombHandsBench.Create();
        var other = thrown.Carry();
        other.BlockedByEntity = null;
        other.Flags2 = 2;
        BombAi.Cancel(thrown.Hero);
        Assert.Empty(thrown.Host.DestroyedAlone);
        Assert.Same(other.LogicContextEntity, thrown.Hero.CarriedEntity);
    }

    [Fact]
    public void H3_Cancel_WithoutAWorld_ClearsTheLinkAndHasNothingToDestroy()
    {
        var bench = BombHandsBench.Create();
        var bomb = bench.Carry();
        bench.Hero.ScriptHost = null;

        BombAi.Cancel(bench.Hero);

        Assert.Null(bench.Hero.CarriedEntity);
        Assert.Empty(bench.Host.DestroyedAlone);
        Assert.Same(bench.Hero.LogicContextEntity, bomb.PlatformEntity);
    }
}
