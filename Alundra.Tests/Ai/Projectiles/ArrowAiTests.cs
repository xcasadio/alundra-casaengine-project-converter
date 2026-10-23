#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Projectiles;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f5 phase A (docs/plan-e20-heros.md section 1.21.1, T1; (e1) to (e5)): the handler E17 (<c>0x8007F420</c>) of the normal arrows (sprite types 3 and 4, and 5 and 6 of the header) on an entity made by hand in the host of <see cref="CutBench"/> (a script host and world context in one,
/// that records <c>DestroyEntity</c>). The arrow carries the stored box of the oracle (24 x 16 x 11 pixels, at Z 16) and the flags <c>0x12074</c>; the target is a body of the Beannoid (20 x 14 x 32 pixels, the Z of the oracle's patched body, flags <c>0x103</c>: the class A the arrow's mask 1 asks for).
/// (e1) the touch destroys the arrow and nothing else lives of the rest of the code, the absence of a touch makes the bounce in the four directions; (e2) one filter per case, each next to the valid target (the same target found, then the same target changed and not found), the limits of the overlap on
/// the three axes; (e3) no filter of status, the last of the entities is searched, a freed one is not; (e4) the phase of the bounce; (e5) the keys of the registry. Each case without effect is set next to a positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ArrowAiTests
{
    private const uint ArrowFlags = 0x12074;
    private const int Px = 1 << 16;

    /// <summary>The arrow of the oracle: the stored box at (300, 100, 16) pixels of 24 x 16 x 11 (the sizes are the pixels minus one raw unit, as the binary holds them), the flags <c>0x12074</c>, the status Deactivated, one touch counted.</summary>
    private sealed class Scene
    {
        internal readonly CutBench Bench = CutBench.Create(frame: false);
        internal readonly EntityScriptProxy Arrow;

        internal Scene(int touches = 1, uint animation = 0, uint flags = ArrowFlags)
        {
            Arrow = Bench.Entity;
            Arrow.SpriteType = 3;
            Arrow.Flags = flags;
            Arrow.Status = EntityStatus.Deactivated;
            Arrow.HitCounter = touches;
            Arrow.TargetAnimationId = animation;
            Arrow.TargetDirection = 24;
            Arrow.HitBoxX = 300 * Px;
            Arrow.HitBoxY = 100 * Px;
            Arrow.HitBoxZ = 16 * Px;
            Arrow.CollisionWidth = (24 * Px) - 1;
            Arrow.CollisionDepth = (16 * Px) - 1;
            Arrow.CollisionHeight = (11 * Px) - 1;
            Bench.Host.Born.Add(Arrow);
        }

        internal CutBench.CutHost Host => Bench.Host;

        /// <summary>A valid target overlapping the box of the arrow on the three axes: the body (290, 97, 0) of 20 x 14 x 32 pixels (the Beannoid's X and Y, the body of 32 of the patched oracle).</summary>
        internal EntityScriptProxy Target(Action<EntityScriptProxy>? change = null)
        {
            var target = new EntityScriptProxy
            {
                SpriteType = 0x100 + 79,
                Flags = 0x103,
                Status = EntityStatus.Normal,
                PosX = 300 * Px,
                PosY = 104 * Px,
                PosZ = 0,
                ModX = -10 * Px,
                ModY = -7 * Px,
                ModZ = 0,
                Width = (20 * Px) - 1,
                Height = (14 * Px) - 1,
                Depth = (32 * Px) - 1,
            };
            change?.Invoke(target);
            Host.Born.Add(target);
            return target;
        }

        /// <summary>One call of E17 on the arrow.</summary>
        internal void Run() => ArrowAi.Deactivate(Arrow, Host);

        internal bool Destroyed => Host.Destroyed.Count == 1 && ReferenceEquals(Host.Destroyed[0].Entity, Arrow) && Host.Destroyed[0].EffectId == -1;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e1) the touch destroys the arrow; the absence of a touch is the bounce, in the four directions.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E1_ATouchWithAValidTarget_DestroysTheArrowWithTheHeadersBreakEffect_NothingElseLives_WithoutAValidTargetTheArrowGoesBackToNormalAndFliesOn()
    {
        var found = new Scene();
        found.Target();
        found.Run();

        Assert.True(found.Destroyed, "DestroyEntity(e, -1): the break effect is the header's (0 for these types)");
        Assert.Equal(EntityStatus.FlagToDestroy, found.Arrow.Status);
        Assert.Equal(0u, found.Arrow.TargetAnimationId); // the writes of the binary's bounce on the dead entity are not made: nothing that lives changes.
        Assert.Equal(ArrowFlags, found.Arrow.Flags);

        // Next to it: a touch whose target was made invulnerable after the hit (no valid target left): no destruction, the status 2, the arrow flies on.
        var notFound = new Scene();
        notFound.Target(t => t.DamagedTickCounter = 0x78);
        notFound.Run();

        Assert.Empty(notFound.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, notFound.Arrow.Status);
        Assert.Equal(0u, notFound.Arrow.TargetAnimationId);
        Assert.Equal(ArrowFlags, notFound.Arrow.Flags);
    }

    [Theory]
    [InlineData(24u, 8u)]
    [InlineData(8u, 24u)]
    [InlineData(16u, 0u)]
    [InlineData(0u, 16u)]
    public void E1_WithoutATouch_TheArrowBounces_TheAnimation1_TheDirectionHalfATurnOn_TheFlags0x12144_TheStatusStays3_NothingIsDestroyed_AWitnessTouchedTheSameTickDies(uint direction, uint expected)
    {
        var scene = new Scene(touches: 0);
        scene.Arrow.TargetDirection = direction;
        scene.Target(); // a valid target is under the arrow: without a touch the search is not even made.

        scene.Run();

        Assert.Equal((1u, expected, 0x12144u, EntityStatus.Deactivated), (scene.Arrow.TargetAnimationId, scene.Arrow.TargetDirection, scene.Arrow.Flags, scene.Arrow.Status));
        Assert.NotEqual(0u, scene.Arrow.Flags & EntityFlags.Gravity); // the bit that does something afterwards: the arrow falls, then comes back.
        Assert.Equal(0u, ArrowFlags & EntityFlags.Gravity);
        Assert.Empty(scene.Host.Destroyed);

        // The same arrow with one touch is destroyed: the counter is what tells the two apart.
        var touched = new Scene(touches: 1);
        touched.Arrow.TargetDirection = direction;
        touched.Target();
        touched.Run();
        Assert.True(touched.Destroyed);
        Assert.Equal(direction, touched.Arrow.TargetDirection);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e2) one filter per case, each next to the valid target; the limits of the overlap.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> Filters()
    {
        // (name, the change that makes the valid target invalid, the change that makes it valid again by the neighbouring value)
        yield return new object[] { "DamagedTickCounter != 0 (+0x1CC)" };
        yield return new object[] { "HitAnimByte & 0x40 (+0xB4)" };
        yield return new object[] { "no class of the arrow's mask (+0x6C)" };
        yield return new object[] { "type 429 (the balloon switch)" };
    }

    [Theory]
    [MemberData(nameof(Filters))]
    public void E2_EachFilterSkipsItsTarget_TheSameTargetWithoutTheMarkIsFound(string filter)
    {
        void Invalidate(EntityScriptProxy t)
        {
            switch (filter)
            {
                case "DamagedTickCounter != 0 (+0x1CC)":
                    t.DamagedTickCounter = 1;
                    break;
                case "HitAnimByte & 0x40 (+0xB4)":
                    t.HitAnimByte = 0x40;
                    break;
                case "no class of the arrow's mask (+0x6C)":
                    t.Flags = 0x102;
                    break;
                default:
                    t.SpriteType = 429;
                    break;
            }
        }

        void Neighbour(EntityScriptProxy t)
        {
            switch (filter)
            {
                case "DamagedTickCounter != 0 (+0x1CC)":
                    t.DamagedTickCounter = 0;
                    break;
                case "HitAnimByte & 0x40 (+0xB4)":
                    t.HitAnimByte = 0x3F; // every other bit of the byte does not make the target untouchable.
                    break;
                case "no class of the arrow's mask (+0x6C)":
                    t.Flags = 0x103;
                    break;
                default:
                    t.SpriteType = 430; // the next type, and the one before it (428), are found.
                    break;
            }
        }

        var valid = new Scene();
        valid.Target(Neighbour);
        valid.Run();
        Assert.True(valid.Destroyed, filter + ": the neighbouring value is a valid target");

        var skipped = new Scene();
        skipped.Target(Invalidate);
        skipped.Run();
        Assert.Empty(skipped.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, skipped.Arrow.Status);

        // Only that target is skipped: with a valid one behind it the arrow is destroyed (the search goes on past a skipped entity).
        var behind = new Scene();
        behind.Target(Invalidate);
        behind.Target();
        behind.Run();
        Assert.True(behind.Destroyed);

        if (filter.StartsWith("type", StringComparison.Ordinal))
        {
            var before = new Scene();
            before.Target(t => t.SpriteType = 428);
            before.Run();
            Assert.True(before.Destroyed);
        }
    }

    [Fact]
    public void E2_TheArrowItselfIsSkipped_NextToAValidTargetFound_AndTheHeroIsOneOfTheEntitiesWithItsOwnClasses()
    {
        // The arrow, given the class A so that nothing but its identity keeps it out, overlaps its own box: alone in the world it finds nothing, next to a valid target it is destroyed.
        var self = new Scene(flags: ArrowFlags | 1);
        self.Arrow.PosX = self.Arrow.HitBoxX;
        self.Arrow.PosY = self.Arrow.HitBoxY;
        self.Arrow.PosZ = self.Arrow.HitBoxZ;
        self.Arrow.Width = (24 * Px) - 1;
        self.Arrow.Height = (16 * Px) - 1;
        self.Arrow.Depth = (11 * Px) - 1;
        self.Run();
        Assert.Empty(self.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, self.Arrow.Status);

        var withTarget = new Scene(flags: ArrowFlags | 1);
        withTarget.Target();
        withTarget.Run();
        Assert.True(withTarget.Destroyed);

        // E20.f5 (e2b): the hero is in the list like any entity (its flags 0x3118C hold none of the classes of the mask 1): it is not found; with the class A posed by the test it is, as the target of (e1): the hero is not excluded as such (marked IsPlayer in both scenes, T4 mutant A07).
        var heroless = new Scene();
        heroless.Target(t => { t.Flags = 0x3118C; t.IsPlayer = true; });
        heroless.Run();
        Assert.Empty(heroless.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, heroless.Arrow.Status);

        var classA = new Scene();
        classA.Target(t => { t.Flags = 0x3118C | 1; t.IsPlayer = true; });
        classA.Run();
        Assert.True(classA.Destroyed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void E2_TheOverlapIsInclusive_OnEachAxis_AtTheContactOfTheHighSideAndOfTheLowSide_AndMissedByOneRawUnitMore(int axis)
    {
        // The pairs of the hit pass: X with (width, Width), Y with (depth, Height), Z with (height, Depth). The sizes in raw units, then the position of the box that sets the distance d = box - body.
        (int BoxSize, int BodySize, int BodyPosition) Axis(Scene s) => axis switch
        {
            0 => (s.Arrow.CollisionWidth, s.Target(t => { }).Width, 290 * Px),
            1 => (s.Arrow.CollisionDepth, s.Target(t => { }).Height, 97 * Px),
            _ => (s.Arrow.CollisionHeight, s.Target(t => { }).Depth, 0),
        };

        void Place(Scene s, int position)
        {
            switch (axis)
            {
                case 0:
                    s.Arrow.HitBoxX = position;
                    break;
                case 1:
                    s.Arrow.HitBoxY = position;
                    break;
                default:
                    s.Arrow.HitBoxZ = position;
                    break;
            }
        }

        var probe = new Scene();
        var (boxSize, bodySize, bodyPosition) = Axis(probe);
        foreach (var (distance, expectedFound) in new[]
        {
            (bodySize, true),        // the box starts exactly where the body ends (d == size): touching, inclusive.
            (bodySize + 1, false),   // one raw unit past.
            (-boxSize, true),        // the box ends exactly where the body starts (-d == size of the box).
            (-boxSize - 1, false),
            (0, true),
        })
        {
            var scene = new Scene();
            Axis(scene); // adds the valid target
            Place(scene, bodyPosition + distance);
            scene.Run();
            Assert.True(expectedFound == scene.Destroyed, $"axis {axis}, d = {distance}: expected {(expectedFound ? "found" : "missed")}");
            Assert.Equal(expectedFound ? EntityStatus.FlagToDestroy : EntityStatus.Normal, scene.Arrow.Status);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e3) no filter of status; the last of the entities is searched; an entity freed is gone.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(EntityStatus.Normal)]
    [InlineData(EntityStatus.Deactivated)]
    [InlineData(EntityStatus.FlagToDestroy)]
    [InlineData(EntityStatus.Loaded)]
    public void E3_NoFilterOfStatus_ATargetInAnyStatusStillInTheWorldIsFound(EntityStatus status)
    {
        var scene = new Scene();
        scene.Target(t => t.Status = status);
        scene.Run();

        Assert.True(scene.Destroyed, $"a target in the status {status} is found (the destroyed-in-the-same-tour target of the oracle)");
    }

    [Fact]
    public void E3_ATargetInTheLastSlotIsFound_ASlotNoLongerInTheWorldIsNot_TheSearchStopsAtNothingBefore()
    {
        const int Slots = 64;
        Scene Fill(bool withTarget)
        {
            var scene = new Scene();
            for (var i = 0; i < Slots - 2; i++)
            {
                scene.Host.Born.Add(new EntityScriptProxy { Flags = 0x103, SpriteType = 0x100 + 79, Width = 1, Height = 1, Depth = 1, PosX = (1000 + i) * Px }); // 62 entities that overlap nothing.
            }

            if (withTarget)
            {
                scene.Target(); // the 64th: the last slot of the table.
            }

            return scene;
        }

        var last = Fill(withTarget: true);
        Assert.Equal(Slots, last.Host.Born.Count);
        last.Run();
        Assert.True(last.Destroyed);

        var freed = Fill(withTarget: false); // the same table, the target's slot freed.
        Assert.Equal(Slots - 1, freed.Host.Born.Count);
        freed.Run();
        Assert.Empty(freed.Host.Destroyed);
        Assert.Equal(EntityStatus.Normal, freed.Arrow.Status);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e4) the phase of the bounce.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E4_InThePhaseOfTheBounce_WithoutTheEndOfTheAnimationNothingHappens_WithItTheArrowIsDestroyed_NoSearchEvenWithATouch()
    {
        // A touch counted and a valid target under the arrow: in the phase of the bounce (animation 1) the counter is not looked at.
        var running = new Scene(touches: 1, animation: 1);
        running.Target();
        running.Arrow.ForceResetAnimationFlag = 0;
        running.Run();
        Assert.Empty(running.Host.Destroyed);
        Assert.Equal((EntityStatus.Deactivated, 1u, 24u, ArrowFlags), (running.Arrow.Status, running.Arrow.TargetAnimationId, running.Arrow.TargetDirection, running.Arrow.Flags)); // no second bounce either.

        var ended = new Scene(touches: 1, animation: 1);
        ended.Arrow.ForceResetAnimationFlag = 1;
        ended.Run();
        Assert.True(ended.Destroyed); // no target needed.

        var endedWithoutTouch = new Scene(touches: 0, animation: 1);
        endedWithoutTouch.Arrow.ForceResetAnimationFlag = 1;
        endedWithoutTouch.Run();
        Assert.True(endedWithoutTouch.Destroyed);

        // The same arrow outside the phase (animation 0) with a touch and a valid target is destroyed by the search, not by the flag: the flag alone does not destroy it.
        var flagOutsideThePhase = new Scene(touches: 0, animation: 0);
        flagOutsideThePhase.Arrow.ForceResetAnimationFlag = 1;
        flagOutsideThePhase.Run();
        Assert.Empty(flagOutsideThePhase.Host.Destroyed);
        Assert.Equal(1u, flagOutsideThePhase.Arrow.TargetAnimationId); // it bounces.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e5) the keys of the registry.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E5_TheProductionRegistryHoldsTheKeyE17ForTheTypes3To6_NotForTheTypes7And11_AndTheKeysOfTheClassAreTheOnlyOnesItAdds()
    {
        var registry = NativeSpriteRegistry.CreateForProduction();

        foreach (var type in new[] { 3, 4, 5, 6 })
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, ArrowAi.DeactivateCode, type, out var handler), $"type {type}");
            Assert.NotNull(handler);
        }

        foreach (var type in new[] { 7, 11, 27, 79 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, ArrowAi.DeactivateCode, type, out _), $"type {type}");
        }

        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 17) }, ArrowAi.Keys.ToArray());
        Assert.Equal(new[] { 3, 4, 5, 6 }, ArrowAi.SpriteTypes);

        // A class registered in an empty registry adds exactly its keys, and the key cannot be registered twice.
        var own = new NativeSpriteRegistry();
        ArrowAi.Register(own);
        Assert.Equal(ArrowAi.Keys.Count, own.Count);
        Assert.Throws<InvalidOperationException>(() => ArrowAi.Register(own));
    }
}
