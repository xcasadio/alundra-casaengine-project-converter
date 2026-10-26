#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3a T1, phase A (docs/plan-e20-heros.md section 1.30.2, tests (b1), (b2) and (b4), the bomb half): the native handlers D30 (<c>0x8007E79C</c>, slot D) and E12 (<c>0x8007F27C</c>, slot E) of the bomb, the sprite type 2, on an entity made by hand, and the keys they register, on a registry of
/// their own (the registry of production is not touched: the world tests (w) run on it and stay red until the phase B). The expected values are the 25 vectors of each function that the critic of h3 made from the real code of the binary
/// (<c>docs/plan-e20-annexe/e20-h3/critic/results/goldens_critic.json</c>, replayed in T0 from an empty memory), replayed field by field, and hand cases next to them; each case without effect is set next to a positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombAiTests : IDisposable
{
    private const string Goldens = "e20-h3/critic/results/goldens_critic.json";

    public BombAiTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        BombAi.StubCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    /// <summary>An entity that stands for the toucher: its current attack has the attribute <paramref name="attribute"/> (or none), with or without a frame box.</summary>
    private static Entity Toucher(int? attribute, bool frameBox = false)
    {
        var entity = new Entity { Name = "toucher", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.CurrentAttack = attribute is null ? null : new BalanceAttack(attribute.Value, 1);
        proxy.HasFrameBox = frameBox;
        return entity;
    }

    /// <summary>A bomb: the type 2, in the status Normal, touched by <paramref name="toucher"/>.</summary>
    private static EntityScriptProxy Bomb(Entity? toucher)
        => new() { SpriteType = 2, Status = EntityStatus.Normal, TouchingEntity = toucher };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b1) D30.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The byte at the address <paramref name="pointer"/> in the memory the vector read (little endian words, the byte read where the pointer falls in its word).</summary>
    private static int ByteAt(BombVector vector, uint pointer) => (int)((vector.Word(pointer & ~3u) >> (int)(8 * (pointer & 3))) & 0xFF);

    [Fact]
    public void B1_D30_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_TheStatusBecomes3AtTheNibbles4And6And10Only()
    {
        var vectors = BombVector.Load(Goldens, "D30");

        Assert.Equal(25, vectors.Count);
        var set = 0;
        foreach (var vector in vectors)
        {
            var touching = vector.Read(0x224);
            var attack = touching == 0 ? 0u : vector.Word(touching + 0x1C8);
            var bomb = Bomb(touching == 0 ? null : Toucher(attack == 0 ? null : ByteAt(vector, attack)));

            BombAi.Touch(bomb);

            var expected = vector.Wrote(0x10) ? EntityStatus.Deactivated : EntityStatus.Normal;
            Assert.Equal(expected, bomb.Status);
            set += vector.Wrote(0x10) ? 1 : 0;
        }

        Assert.Equal(4, set); // the positive cases are in the sample (the guard of the tests without effect).
        Assert.Equal(21, vectors.Count - set);
    }

    [Fact]
    public void B1_D30_TheAttributeNibble_4_6_And_10_SetTheStatus3_1To3_5_7To9_11To15_And0_DoNot_TheHighNibbleDoesNotMatter()
    {
        for (var attribute = 0; attribute < 256; attribute++)
        {
            var bomb = Bomb(Toucher(attribute));

            BombAi.Touch(bomb);

            var nibble = attribute & 0xF;
            var sets = nibble is 4 or 6 or 10;
            Assert.True(sets == (bomb.Status == EntityStatus.Deactivated), $"attribute 0x{attribute:X2}: expected {(sets ? "status 3" : "no change")}, got {bomb.Status}");
            if (!sets)
            {
                Assert.Equal(EntityStatus.Normal, bomb.Status);
            }
        }

        // The six cases of the plan, side by side in the same bomb type: the nibbles 4, 6, 10 set it off and 1 to 3, 5, 7 to 9, 11 leave it alone.
        foreach (var (nibble, expected) in new[] { (4, true), (6, true), (10, true), (1, false), (2, false), (3, false), (5, false), (7, false), (8, false), (9, false), (11, false) })
        {
            var bomb = Bomb(Toucher(nibble));
            BombAi.Touch(bomb);
            Assert.Equal(expected ? EntityStatus.Deactivated : EntityStatus.Normal, bomb.Status);
        }
    }

    [Fact]
    public void B1_D30_NeedsNoFrameBoxOfTheToucher_AndNothingChangesWithoutATouchingEntityOrWithoutAnAttack()
    {
        // The binary reads the byte at the pointer: no frame box is tested (the helper of the opcodes does), and the positive case is set next to each negative one.
        foreach (var frameBox in new[] { false, true })
        {
            var hit = Bomb(Toucher(4, frameBox));
            BombAi.Touch(hit);
            Assert.Equal(EntityStatus.Deactivated, hit.Status);
        }

        var alone = Bomb(null);
        BombAi.Touch(alone);
        Assert.Equal(EntityStatus.Normal, alone.Status);

        var unarmed = Bomb(Toucher(null));
        BombAi.Touch(unarmed);
        Assert.Equal(EntityStatus.Normal, unarmed.Status);

        var other = Bomb(Toucher(4));
        other.Flags = 0x132134;
        other.TargetAnimationId = 5;
        BombAi.Touch(other);
        Assert.Equal((EntityStatus.Deactivated, 0x132134u, 5u), (other.Status, other.Flags, other.TargetAnimationId)); // nothing but the status is written.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b2) E12.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A bomb in the status Deactivated with the host of the bench (the destructions and the sounds recorded, a cut of the grass with no box to cut).</summary>
    private static CutBench Exploding(uint flags, uint animationAsked = 0, int endOfAnimation = 0, uint holes = 0)
    {
        var bench = CutBench.Create(frame: false);
        var bomb = bench.Entity;
        bomb.SpriteType = 2;
        bomb.Status = EntityStatus.Deactivated;
        bomb.Flags = flags;
        bomb.TargetAnimationId = animationAsked;
        bomb.ForceResetAnimationFlag = endOfAnimation;
        bomb.CombinedVramFlagsAND = holes;
        return bench;
    }

    [Fact]
    public void B2_E12_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_TheCallOfTheCutTheWritesAndTheDestruction()
    {
        var vectors = BombVector.Load(Goldens, "E12");

        Assert.Equal(25, vectors.Count);
        int cut = 0, destroyed = 0;
        foreach (var vector in vectors)
        {
            var bench = Exploding(vector.Read(0x6C), vector.Read(0x88), unchecked((int)vector.Read(0xAC)), vector.Read(0x184));
            var calls = new List<EntityScriptProxy>();
            BombAi.StubCallsForTests = calls;

            BombAi.Deactivate(bench.Entity);

            BombAi.StubCallsForTests = null;
            var journal = vector.Journal.Single();
            if (journal.Kind == "cate")
            {
                Assert.Equal(new[] { bench.Entity }, calls);
                Assert.Empty(bench.Host.Destroyed);
                cut++;
            }
            else
            {
                Assert.Equal("destroy2", journal.Kind);
                Assert.Equal(uint.MaxValue, journal.Arguments[1]);
                Assert.Empty(calls);
                var gone = Assert.Single(bench.Host.Destroyed);
                Assert.Equal((bench.Entity, -1), (gone.Entity, gone.EffectId));
                destroyed++;
            }

            Assert.Equal(vector.After(0x6C), bench.Entity.Flags);
            Assert.Equal(vector.After(0x88), bench.Entity.TargetAnimationId);
        }

        Assert.Equal((8, 17), (cut, destroyed)); // both branches are in the sample.
    }

    [Theory]
    [InlineData(0x132134u)]
    [InlineData(0x132114u)]
    [InlineData(0xFFFFFFFFu)]
    [InlineData(0x132154u)]
    public void B2_E12_TheFirstCall_AsksTheAnimation1_SetsTheBit0x40_ClearsTheBits0x10And0x20_AndCallsTheCut(uint flags)
    {
        var bench = Exploding(flags);
        var calls = new List<EntityScriptProxy>();
        BombAi.StubCallsForTests = calls;

        BombAi.Deactivate(bench.Entity);

        Assert.Equal(1u, bench.Entity.TargetAnimationId);
        Assert.Equal((flags | 0x40u) & ~0x30u, bench.Entity.Flags);
        Assert.Equal(0x40u, bench.Entity.Flags & 0x70u);
        Assert.Equal(new[] { bench.Entity }, calls);
        Assert.Empty(bench.Host.Destroyed);
        Assert.Equal(EntityStatus.Deactivated, bench.Entity.Status); // E12 never writes the status.
    }

    [Fact]
    public void B2_E12_TheCutIsCalledAtEveryCall_NotOnceOnly_UntilTheAnimation1IsFinished_ThenTheBombIsDestroyedWithoutCutting()
    {
        var bench = Exploding(0x132134);
        var calls = new List<EntityScriptProxy>();
        BombAi.StubCallsForTests = calls;

        for (var call = 1; call <= 5; call++)
        {
            BombAi.Deactivate(bench.Entity);
            Assert.Equal(call, calls.Count); // the first call poses the animation, the next four find it at 1 and not finished.
            Assert.Empty(bench.Host.Destroyed);
        }

        bench.Entity.ForceResetAnimationFlag = 1; // the animation 1 is finished.
        BombAi.Deactivate(bench.Entity);

        Assert.Equal(5, calls.Count); // the destroying call does not cut.
        var gone = Assert.Single(bench.Host.Destroyed);
        Assert.Equal((bench.Entity, -1), (gone.Entity, gone.EffectId));
        Assert.Equal(EntityStatus.FlagToDestroy, bench.Entity.Status); // the world's destruction, as the bench does it.
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(2u, 1)]
    [InlineData(5u, 1)]
    [InlineData(1u, 0)]
    [InlineData(1u, 2)]
    public void B2_E12_AnimationEndsOnlyWhenTheAnimationAskedIs1AndItsEndIs1_Otherwise_TheBombKeepsExploding(uint animationAsked, int endOfAnimation)
    {
        var bench = Exploding(0x132134, animationAsked, endOfAnimation);
        var calls = new List<EntityScriptProxy>();
        BombAi.StubCallsForTests = calls;

        BombAi.Deactivate(bench.Entity);

        Assert.Empty(bench.Host.Destroyed);
        Assert.Single(calls);
        Assert.Equal(1u, bench.Entity.TargetAnimationId);

        // The positive case next to it: the animation 1 and its end at 1.
        var finished = Exploding(0x132134, 1, 1);
        BombAi.Deactivate(finished.Entity);
        Assert.Single(finished.Host.Destroyed);
    }

    [Theory]
    [InlineData(0x04u)]
    [InlineData(0x84u)]
    [InlineData(0xFFFFFFFFu)]
    [InlineData(0x5u)]
    public void B2_E12_ABombOnAHole_IsDestroyedAtOnce_WhateverItsAnimation_WithNoCutAndNoWrite(uint holes)
    {
        var bench = Exploding(0x132134, animationAsked: 0, holes: holes);
        var calls = new List<EntityScriptProxy>();
        BombAi.StubCallsForTests = calls;

        BombAi.Deactivate(bench.Entity);

        var gone = Assert.Single(bench.Host.Destroyed);
        Assert.Equal((bench.Entity, -1), (gone.Entity, gone.EffectId));
        Assert.Empty(calls);
        Assert.Equal((0x132134u, 0u), (bench.Entity.Flags, bench.Entity.TargetAnimationId)); // nothing written.

        // The cases next to it: no hole bit (bit 2 clear) keeps the bomb.
        foreach (var other in new[] { 0x00u, 0x01u, 0x80u, 0xFBu })
        {
            var kept = Exploding(0x132134, holes: other);
            BombAi.Deactivate(kept.Entity);
            Assert.Empty(kept.Host.Destroyed);
        }
    }

    [Fact]
    public void B2_E12_WithoutAHost_NothingHappens()
    {
        var bomb = new EntityScriptProxy { SpriteType = 2, Status = EntityStatus.Deactivated, Flags = 0x132134 };

        BombAi.Deactivate(bomb);

        Assert.Equal((0x132134u, 0u), (bomb.Flags, bomb.TargetAnimationId));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b4) the keys.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheBombRegistersC62D30AndE12_ForTheType2Alone_NoOtherKeyAndNoOtherType()
    {
        var registry = new NativeSpriteRegistry();

        using var registration = BombAi.Register(registry);

        Assert.Equal(3, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 62), (ScriptHelper.ProgramDTouch, 30), (ScriptHelper.ProgramEDeactivate, 12) }, BombAi.Keys);
        Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 62, 2, out var tick));
        Assert.NotNull(tick);
        Assert.True(registry.TryGet(ScriptHelper.ProgramDTouch, 30, 2, out var touch));
        Assert.NotNull(touch);
        Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 12, 2, out var deactivate));
        Assert.NotNull(deactivate);

        foreach (var type in new[] { 0, 1, 3, 4, 241, 243, 258 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 62, type, out _), $"(C, 62) must not be registered for the type {type}");
            Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 30, type, out _), $"(D, 30) must not be registered for the type {type}");
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 12, type, out _), $"(E, 12) must not be registered for the type {type}");
        }

        // The keys are exactly these: the neighbours in the slot, the code in another slot.
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 30, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 12, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 30, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 12, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 19, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 61, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 62, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 62, 2, out _));
    }

    [Fact]
    public void B4_TheRegisteredHandlersAreTheBombs_D30SetsTheStatus3_E12AsksTheAnimation1_AndTheDisposableTakesTheKeysOff()
    {
        var registry = new NativeSpriteRegistry();
        var registration = BombAi.Register(registry);
        Assert.True(registry.TryGet(ScriptHelper.ProgramDTouch, 30, 2, out var touch));
        Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 12, 2, out var deactivate));

        var bench = Exploding(0x132134);
        bench.Entity.Status = EntityStatus.Normal;
        bench.Entity.TouchingEntity = Toucher(4);
        touch!(bench.Entity, bench.Host, bench.Host.GameState);
        Assert.Equal(EntityStatus.Deactivated, bench.Entity.Status);

        deactivate!(bench.Entity, bench.Host, bench.Host.GameState);
        Assert.Equal(1u, bench.Entity.TargetAnimationId);

        registration.Dispose();

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 30, 2, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 12, 2, out _));
    }

    [Fact]
    public void B4_TheBombAndTheLidShareTheSlotE_WithoutAnyConflict_AndASecondRegistrationOfTheBombThrows()
    {
        var registry = new NativeSpriteRegistry();
        using var bomb = BombAi.Register(registry);
        using var lid = Alundra.Ai.Props.BombLidAi.Register(registry);

        Assert.Equal(4, registry.Count);
        Assert.Throws<InvalidOperationException>(() => BombAi.Register(registry));
        Assert.Equal(4, registry.Count); // the failed registration leaves nothing behind (C62 is the first key: it is already taken).
    }
}
