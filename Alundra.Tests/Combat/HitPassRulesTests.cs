#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Combat;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.c1a C5 (docs/plan-e20-heros.md section 1.5): the rules of the hit pass on small hand-made sets of entities, the validation cases of the D2 oracle (<c>docs/plan-e20-annexe/d2-oracle-coups/</c>, six cases whose
/// expected values were written by hand from the plan and run on the real code of the binary) that belong to the pass: 1 the reordered class mask, 2 the overlap in raw 16.16, 5 the first attacker, 6 several targets, and the part
/// of 3 on <c>TouchingEntity</c>, the <c>+0x1D0</c> timer and the attacker's counter (the case 4 and the life of the case 3 are E20.c1b). The expectations below are the ones of <c>tools/cases.py</c>, not computed by the
/// code under test. Plus the latch of the hero's animation. (The balance tables and the reload at a switch are in <see cref="BalanceTablesTests"/>.)
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HitPassRulesTests
{
    private const uint ClassA = EntityFlags.ClassA;
    private const uint ClassB = EntityFlags.ClassB;
    private const uint ClassC = EntityFlags.ClassC;
    private const uint HitsA = EntityFlags.HitsClassA;
    private const uint HitsB = EntityFlags.HitsClassB;
    private const uint HitsC = EntityFlags.HitsClassC;

    // -----------------------------------------------------------------------------------------
    // Hand-made entities.
    // -----------------------------------------------------------------------------------------

    /// <summary>A bare entity with a sprite whose animation 0 (down) carries one box keyframe (or none), a balance record, flags, a position and a body.</summary>
    private sealed class Actor
    {
        public readonly Entity Entity;
        public readonly EntityScriptProxy Proxy;

        public Actor(
            uint flags, int x, int y, int z, BalanceRecord? record = null, (int Ox, int Oy, int Oz, int W, int D, int H)? box = null, (int W, int D, int H)? body = null, int animationByte = 0)
        {
            var sprite = new AnimatedSpriteComponent();
            var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
            var data = new Animation2dData { Name = "bank_anim0_down", AnimationType = AnimationType.Loop };
            data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
            var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(1f, spriteId));
            data.Tracks.Add(track);
            var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = 0f };
            if (box is { } b)
            {
                keyframe.Fixtures.Add(new ColliderFixture
                {
                    Shape = new Box { Size = new Vector3(b.W, b.D, b.H) },
                    LocalPosition = new Vector3(b.Ox + b.W / 2f, b.Oy + b.D / 2f, b.Oz + b.H / 2f),
                });
            }

            data.CollisionKeyframes.Add(keyframe);
            sprite.AddAnimation(new Animation2d(data));

            Entity = new Entity { Name = "actor", GameplayProxyClassName = typeof(EntityScriptProxy).FullName!, RootComponent = sprite };
            Entity.Initialize();
            Proxy = (EntityScriptProxy)Entity.GameplayProxy;
            Proxy.LogicContextEntity = Entity;
            Proxy.Status = EntityStatus.Normal;
            Proxy.Flags = flags;
            Proxy.PosX = x << 16;
            Proxy.PosY = y << 16;
            Proxy.PosZ = z << 16;
            var size = body ?? (8, 8, 8);
            EntitySpawnFactory.SetEntityDimensions(Proxy, 0, 0, 0, size.W, size.D, size.H);
            Proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [0] = new AnimSetEntry { Anim = 0, Acceleration = animationByte } };
            Proxy.BalanceRecord = record;
            Proxy.LoadAnimationSwitch(0, 0, fromClock: false);
        }

        public static implicit operator EntityScriptProxy(Actor actor) => actor.Proxy;
    }

    /// <summary>A record with the given responses (the first, for the attack nibble 1, is <paramref name="response"/>; the rest 16) and the given attacks.</summary>
    private static BalanceRecord Record(int response = 16, params (int Attribute, int Power)[] attacks)
    {
        var responses = Enumerable.Repeat((byte)16, 11).ToArray();
        responses[0] = (byte)response;
        return new BalanceRecord(255, 15 + 2 * attacks.Length, 9, responses, attacks.Select(a => new BalanceAttack(a.Attribute, a.Power)).ToArray(), null);
    }

    private static BalanceRecord SwordRecord() => Record(16, (0x81, 3));

    private static BalanceRecord NeutralRecord() => Record(16);

    private static readonly (int Ox, int Oy, int Oz, int W, int D, int H) Box8 = (0, 0, 0, 8, 8, 8);

    private static void Run(params Actor[] actors) => RunAll(actors, out _);

    private static HitPass RunAll(IReadOnlyList<Actor> actors, out List<(int X, int Y, int Z)> sparks)
    {
        var pass = new HitPass();
        var centres = new List<(int, int, int)>();
        pass.Run(actors.Select(a => a.Proxy).ToList(), actors.Count > 0 ? actors[0].Proxy : null, (x, y, z) => centres.Add((x, y, z)));
        sparks = centres;
        return pass;
    }

    private static Entity? Touching(Actor actor) => actor.Proxy.TouchingEntity;

    // -----------------------------------------------------------------------------------------
    // D2 case 1: the reordered class mask.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Case1_AnAttackerThatOnlyHitsClassA_HitsAClassATarget_AndNotAClassBOne()
    {
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        var classA = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        var classB = new Actor(ClassB, 100, 100, 0, NeutralRecord());
        Run(attacker, classA, classB);
        Assert.Equal(1, attacker.Proxy.HitCounter);
        Assert.Same(attacker.Entity, Touching(classA));
        Assert.Equal(0x19, classA.Proxy.FrameCollisionTickCounter);
        Assert.Null(Touching(classB));
        Assert.Equal(0, classB.Proxy.FrameCollisionTickCounter);
    }

    [Fact]
    public void Case1_AnAttackerThatOnlyHitsClassB_HitsAClassBTarget_AndNotAClassAOne()
    {
        var attacker = new Actor(HitsB, 100, 100, 0, SwordRecord(), Box8);
        var classA = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        var classB = new Actor(ClassB, 100, 100, 0, NeutralRecord());
        Run(attacker, classA, classB);
        Assert.Equal(1, attacker.Proxy.HitCounter);
        Assert.Same(attacker.Entity, Touching(classB));
        Assert.Null(Touching(classA));
        Assert.Equal(0, classA.Proxy.FrameCollisionTickCounter);
    }

    [Fact]
    public void Case1_TheClassCBitsAreReorderedToo_AndAnAttackerWithNoClassBitHitsNothing()
    {
        var attacker = new Actor(HitsC, 100, 100, 0, SwordRecord(), Box8);
        var classC = new Actor(ClassC, 100, 100, 0, NeutralRecord());
        var classA = new Actor(ClassA | ClassB, 100, 100, 0, NeutralRecord());
        Run(attacker, classC, classA);
        Assert.Same(attacker.Entity, Touching(classC));
        Assert.Null(Touching(classA));

        var harmless = new Actor(0, 100, 100, 0, SwordRecord(), Box8);
        var victim = new Actor(ClassA | ClassB | ClassC, 100, 100, 0, NeutralRecord());
        Run(harmless, victim);
        Assert.Equal(0, harmless.Proxy.HitCounter);
        Assert.Null(Touching(victim));

        Assert.Equal(1u, HitPass.ClassMaskOf(HitsA));
        Assert.Equal(8u, HitPass.ClassMaskOf(HitsB));
        Assert.Equal(0x800u, HitPass.ClassMaskOf(HitsC));
        Assert.Equal(0u, HitPass.ClassMaskOf(ClassA | ClassB | ClassC | EntityFlags.Collidable));
    }

    // -----------------------------------------------------------------------------------------
    // D2 case 2: the overlap, in raw 16.16, widened by one unit.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Case2_TheOverlapIsTestedInRawUnits_OnEachAxis_AtTheBoundaryOfTheBodyAndOfTheBox()
    {
        foreach (var axis in new[] { 0, 1, 2 })
        {
            foreach (var (n, m) in new[] { (1, 1), (5, 4), (16, 12) })
            {
                // d = attacker's HitBox - target's ModdedPos on the axis; the two other axes at 0.
                foreach (var (d, expectHit, label) in new[]
                {
                    ((n << 16) - 1, true, "d = (n << 16) - 1"),
                    (n << 16, false, "d = n << 16"),
                    (-((m << 16) - 1), true, "d = -((m << 16) - 1)"),
                    (-(m << 16), false, "d = -(m << 16)"),
                })
                {
                    var target = new Actor(ClassA, 100, 100, 0, NeutralRecord(), body: (n, n, n));
                    var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), (0, 0, 0, m, m, m));
                    switch (axis)
                    {
                        case 0: attacker.Proxy.PosX += d; break;
                        case 1: attacker.Proxy.PosY += d; break;
                        default: attacker.Proxy.PosZ += d; break;
                    }

                    Run(attacker, target);
                    Assert.True(
                        expectHit == (attacker.Proxy.HitCounter == 1),
                        $"axis {axis}, target {n} px, box {m} px, {label} = {d}: expected {(expectHit ? "HIT" : "MISS")}");
                    Assert.Equal(expectHit, Touching(target) != null);
                    Assert.Equal(expectHit ? 0x19 : 0, target.Proxy.FrameCollisionTickCounter);
                }
            }
        }
    }

    [Fact]
    public void Case2_ASizeOfZeroPixels_IsOnePointOnlyForABody_AndMinusOneForABox()
    {
        // N3 of the D2 oracle: a body of 0 pixels is stored 0 (it is hit at d = 0 only), a box of 0 pixels is stored -1 (it reaches nothing at d < 0 and the body only from d >= 0).
        var target = new Actor(ClassA, 100, 100, 0, NeutralRecord(), body: (0, 8, 8));
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        Run(attacker, target);
        Assert.Equal(1, attacker.Proxy.HitCounter);
        Assert.Equal(0, target.Proxy.Width);

        var shifted = new Actor(ClassA, 100, 100, 0, NeutralRecord(), body: (0, 8, 8));
        var attacker2 = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        attacker2.Proxy.PosX += 1;
        Run(attacker2, shifted);
        Assert.Equal(0, attacker2.Proxy.HitCounter);

        var emptyBox = new Actor(HitsA, 100, 100, 0, SwordRecord(), (0, 0, 0, 0, 8, 8));
        var body = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        Run(emptyBox, body);
        Assert.Equal(-1, emptyBox.Proxy.CollisionWidth);
        Assert.Equal(1, emptyBox.Proxy.HitCounter); // d = 0: the body starts there.
        var behind = new Actor(HitsA, 100, 100, 0, SwordRecord(), (0, 0, 0, 0, 8, 8));
        behind.Proxy.PosX -= 1;
        var body2 = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        Run(behind, body2);
        Assert.Equal(0, behind.Proxy.HitCounter);
    }

    [Fact]
    public void TheBoxIsReadFromHitBox_PositionPlusOffset_AndTheBodyFromPositionPlusMod()
    {
        // The box at offset 20, the body at mod -4: the box (HitBox = Pos + offset) meets the body (ModdedPos = Pos + Mod) only when the positions line up that way.
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), (20, 0, 0, 8, 8, 8));
        var target = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        EntitySpawnFactory.SetEntityDimensions(target.Proxy, -4, 0, 0, 8, 8, 8);
        target.Proxy.PosX = 124 << 16; // ModdedPos 120 .. 128 against HitBox 120 .. 128.
        Run(attacker, target);
        Assert.Equal(1, attacker.Proxy.HitCounter);
        Assert.Equal(120 << 16, attacker.Proxy.HitBoxX);

        var late = new Actor(HitsA, 100, 100, 0, SwordRecord(), (20, 0, 0, 8, 8, 8));
        var target2 = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        EntitySpawnFactory.SetEntityDimensions(target2.Proxy, -4, 0, 0, 8, 8, 8);
        target2.Proxy.PosX = (124 << 16) + (9 << 16); // the body starts one pixel after the box ends.
        Run(late, target2);
        Assert.Equal(0, late.Proxy.HitCounter);
    }

    // -----------------------------------------------------------------------------------------
    // D2 case 3 (the part that belongs to the pass): the response of the target.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Case3_AResponseOf0x80_GivesNoTouchingEntity_ButTheCooldown_TheAttackersCounter_AndTheSparks()
    {
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        var immune = new Actor(ClassA, 100, 100, 0, Record(0x80));
        var pass = RunAll(new[] { attacker, immune }, out var sparks);
        Assert.Null(Touching(immune));
        Assert.Equal(0x19, immune.Proxy.FrameCollisionTickCounter);
        Assert.Equal(1, attacker.Proxy.HitCounter);
        Assert.Single(sparks);
        Assert.Equal(1, pass.HitsWritten);
        Assert.Equal(0, immune.Proxy.Hp); // the pass never writes a life (E20.c1b): the hand-made entity keeps the 0 it was built with.
    }

    [Fact]
    public void Case3_AResponseOf0xC0_IsTouched_LikeANeutralOne()
    {
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        var half = new Actor(ClassA, 100, 100, 0, Record(0xC0));
        Run(attacker, half);
        Assert.Same(attacker.Entity, Touching(half));
        Assert.Equal(0x19, half.Proxy.FrameCollisionTickCounter);
    }

    [Fact]
    public void TheResponseIsTheByteAtTheNibblePlus2_OfTheTargetsRecord()
    {
        // The attack 0x83 (nibble 3) reads the response byte 5 = DamageResponses[2]; the nibble 1 response is neutral here.
        var attacker = new Actor(HitsA, 100, 100, 0, Record(16, (0x83, 1)), Box8);
        var responses = Enumerable.Repeat((byte)16, 11).ToArray();
        responses[2] = 0x80;
        var target = new Actor(ClassA, 100, 100, 0, new BalanceRecord(255, 15, 9, responses, Array.Empty<BalanceAttack>(), null));
        Run(attacker, target);
        Assert.Null(Touching(target));
        Assert.Equal(0x19, target.Proxy.FrameCollisionTickCounter);
    }

    // -----------------------------------------------------------------------------------------
    // D2 case 5: the first attacker of the list wins. Case 6: several targets.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Case5_TwoAttackersOnOneTarget_OnlyTheFirstOfTheListTouchesIt_WhateverTheOrder()
    {
        foreach (var order in new[] { "A B T", "B A T", "A T B", "T A B", "B T A" })
        {
            var a = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
            var b = new Actor(HitsA, 100, 100, 0, Record(16, (0x01, 1)), Box8);
            var t = new Actor(ClassA, 100, 100, 0, NeutralRecord());
            var byName = new Dictionary<string, Actor> { ["A"] = a, ["B"] = b, ["T"] = t };
            var list = order.Split(' ').Select(name => byName[name]).ToArray();
            Run(list);
            var first = list.First(actor => actor != t);
            var second = list.Last(actor => actor != t);
            Assert.True(ReferenceEquals(first.Entity, Touching(t)), $"order {order}: the target is touched by the first attacker of the list");
            Assert.Equal(1, first.Proxy.HitCounter);
            Assert.Equal(0, second.Proxy.HitCounter);
            Assert.Equal(0x19, t.Proxy.FrameCollisionTickCounter);
        }
    }

    [Fact]
    public void Case6_OneAttackerOnTwoTargets_TouchesBoth_AndCountsTwo_AThirdOutOfRangeIsUntouched()
    {
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
        var t1 = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        var t2 = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        var far = new Actor(ClassA, 140, 100, 0, NeutralRecord());
        var pass = RunAll(new[] { attacker, t1, t2, far }, out var sparks);
        Assert.Same(attacker.Entity, Touching(t1));
        Assert.Same(attacker.Entity, Touching(t2));
        Assert.Null(Touching(far));
        Assert.Equal(2, attacker.Proxy.HitCounter);
        Assert.Equal(0x19, t1.Proxy.FrameCollisionTickCounter);
        Assert.Equal(0, far.Proxy.FrameCollisionTickCounter);
        Assert.Equal(2, sparks.Count);
        Assert.Equal(2, pass.HitsWritten);
    }

    // -----------------------------------------------------------------------------------------
    // The conditions that skip a pair.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ATargetWithACooldown_ADamagedCounter_OrTheAnimationByte0x40_IsSkipped_AndATargetWithNoneIsHit()
    {
        foreach (var (name, setup, hit) in new (string, Action<EntityScriptProxy>, bool)[]
        {
            ("control", _ => { }, true),
            ("+0x1D0", t => t.FrameCollisionTickCounter = 5, false),
            ("+0x1CC", t => t.DamagedTickCounter = 7, false),
            ("byte 0x40", t => t.HitAnimByte = 0x40, false),
            ("byte 0x41", t => t.HitAnimByte = 0x41, false),
            ("byte 0x3F", t => t.HitAnimByte = 0x3F, true),
        })
        {
            var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), Box8);
            var target = new Actor(ClassA, 100, 100, 0, NeutralRecord());
            setup(target.Proxy);
            Run(attacker, target);
            Assert.True(hit == (attacker.Proxy.HitCounter == 1), $"{name}: expected {(hit ? "a hit" : "no hit")}");
        }
    }

    [Fact]
    public void AnAttackerWithNoAttack_ANullAttribute_NoBox_OrNoRecord_HitsNothing()
    {
        var target = new Actor(ClassA, 100, 100, 0, NeutralRecord());
        var noAttack = new Actor(HitsA, 100, 100, 0, Record(16), Box8);
        var zeroAttribute = new Actor(HitsA, 100, 100, 0, Record(16, (0x00, 5)), Box8);
        var noBox = new Actor(HitsA, 100, 100, 0, SwordRecord(), null);
        var noRecord = new Actor(HitsA, 100, 100, 0, null, Box8);
        Run(noAttack, target);
        Run(zeroAttribute, target);
        Run(noBox, target);
        Run(noRecord, target);
        Assert.Equal(0, noAttack.Proxy.HitCounter + zeroAttribute.Proxy.HitCounter + noBox.Proxy.HitCounter + noRecord.Proxy.HitCounter);
        Assert.Null(Touching(target));
        Assert.False(noBox.Proxy.EvaluateFrameBox());
    }

    [Fact]
    public void AnEntityNeverHitsItself_AndATargetMayBeAnAttackerToo()
    {
        var a = new Actor(HitsA | ClassA, 100, 100, 0, SwordRecord(), Box8);
        var b = new Actor(HitsA | ClassA, 100, 100, 0, SwordRecord(), Box8);
        Run(a, b);
        Assert.Equal(1, a.Proxy.HitCounter);
        Assert.Equal(1, b.Proxy.HitCounter);
        Assert.Same(a.Entity, Touching(b));
        Assert.Same(b.Entity, Touching(a));
        Assert.Equal(0x19, a.Proxy.FrameCollisionTickCounter);
    }

    // -----------------------------------------------------------------------------------------
    // The sparks.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheSparksAreAtTheCentreOfTheOverlap_TruncatedTowardZero()
    {
        var attacker = new Actor(HitsA, 100, 100, 0, SwordRecord(), (0, 0, 0, 8, 8, 8));
        var target = new Actor(ClassA, 104, 100, 0, NeutralRecord(), body: (8, 8, 8));
        RunAll(new[] { attacker, target }, out var sparks);
        // X: box 100 .. 108 - 1 unit, body 104 .. 112 - 1: [104, 108) overlap, centre (104 + 108 - 1) / 2; Y and Z: the same extents.
        var expectedX = ((104 << 16) + (108 << 16) - 1) / 2;
        var expectedY = ((100 << 16) + (108 << 16) - 1) / 2;
        var expectedZ = (0 + (8 << 16) - 1) / 2;
        Assert.Equal((expectedX, expectedY, expectedZ), Assert.Single(sparks));

        Assert.Equal(-1, HitPass.AxisCentre(-3, 3, -3, 3)); // (-3 + 0) / 2 truncates toward zero (a shift would give -2).
        Assert.Equal(1, HitPass.AxisCentre(0, 3, 0, 3)); // (0 + 3) / 2.
    }

    [Fact]
    public void AHitMakesThreeSparksOfEffect9_AtTheCentre_ThreeDrawsEach_AllThreeAxesWithTheFactor0x20001()
    {
        var bank = EffectBankLoader.Load(ProjectRootFinder.Find(), "Overworld 3,1-14");
        var reservoir = new EffectReservoir(bank, _ => { });
        GameRandom.RandomSeed = 0x12345678;
        NativeEffects.CreateHitSparks(reservoir, 1000, 2000, 3000);

        var sparks = reservoir.Slots.Where(slot => slot.Active).ToList();
        Assert.Equal(3, sparks.Count);

        // The generator written out by hand: x' = x * 0x7D2B89DD + 0xE06A02E7 (32 bits), three draws a spark, force = high word of (draw * 0x20001) - 0x10000 on each axis.
        var state = 0x12345678u;
        uint Draw()
        {
            state = unchecked(state * 0x7D2B89DDu + 0xE06A02E7u);
            return state;
        }

        static int Force(uint draw) => (int)(((ulong)draw * 0x20001UL) >> 32) - 0x10000;
        foreach (var spark in sparks)
        {
            Assert.Equal(9, spark.TargetEffect);
            Assert.False(spark.TargetMapTable);
            Assert.Equal((1000, 2000, 3000), (spark.X, spark.Y, spark.Z));
            Assert.Equal(Force(Draw()), spark.ForceX);
            Assert.Equal(Force(Draw()), spark.ForceY);
            Assert.Equal(Force(Draw()), spark.ForceZ);
        }

        Assert.Equal(state, (uint)GameRandom.RandomSeed);
        GameRandom.Reset();

        // A nearly full reservoir (two slots left) creates two sparks, draws six numbers, and none for the one it could not create.
        var nearlyFull = new EffectReservoir(bank, _ => { });
        for (var i = 0; i < 126; i++)
        {
            Assert.NotNull(nearlyFull.Create(false, 9, 0, 0, 0, 0));
        }

        GameRandom.RandomSeed = 0x12345678;
        NativeEffects.CreateHitSparks(nearlyFull, 1, 2, 3);
        Assert.Equal(128, nearlyFull.Slots.Count(slot => slot.Active));
        state = 0x12345678u;
        for (var i = 0; i < 6; i++)
        {
            Draw();
        }

        Assert.Equal(state, (uint)GameRandom.RandomSeed);
        GameRandom.Reset();
    }

    // -----------------------------------------------------------------------------------------
    // The latch of the hero's animation.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheHeroAnimationIsLatchedEvenWithAnEmptyList_AsTheTargetAnimation_NotTheCurrentOne()
    {
        var hero = new Actor(0, 100, 100, 0, NeutralRecord());
        hero.Proxy.TargetAnimationId = 0x31;
        hero.Proxy.CurrentAnimationId = 0x10;
        var pass = new HitPass();
        pass.Run(Array.Empty<EntityScriptProxy>(), hero.Proxy, null);
        Assert.Equal(0x31u, pass.HeroAnimationLatch);
        hero.Proxy.TargetAnimationId = 0x04;
        pass.Run(new List<EntityScriptProxy> { hero.Proxy }, hero.Proxy, null);
        Assert.Equal(0x04u, pass.HeroAnimationLatch);
    }
}
