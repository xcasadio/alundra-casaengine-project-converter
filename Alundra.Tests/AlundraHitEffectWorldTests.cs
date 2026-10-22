#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Combat;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.f3b T2 (docs/plan-e20-heros.md section 1.19.2, D-F3-9, D-F3-10): the effects 4 and 5 of the hit pass on the REAL world of map 14 (<see cref="HitArena"/>: the production path, the world's own hit pass and effect stage,
/// frames of 1, 2 or 3 logic ticks). A Beannoid of the map is carried onto the hero and its <c>CurrentAttack</c> is set to (attribute, power 1): its class mask reaches the hero (flags <c>0x103</c>), so the hero is the target
/// (the sheet 0 of the hero has null responses, D-F3-10). The Beannoid's attack is cleared right after the first hit, so that only one hit happens. The reservoir is read after each frame.
/// <para>
/// Written after the pure logic of the pass (phase A of f3b: the world passes no callback): each test asserts at least one effect slot, so none can pass before the wiring of phase B (T3). Observations are those of the frame
/// boundary: the stage applies the operations a tick queued at the end of the frame that contains it, so a slot is first SEEN at the end of the frame that contains the hit tick.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHitEffectWorldTests : IDisposable
{
    private const ulong Seed = 0x2468ACE0UL;
    private const int TotalTicks = 132; // a multiple of 1, 2 and 3.

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public AlundraHitEffectWorldTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private sealed record SlotView(int Effect, int Mode, IEffectAnchor? Anchor, int DepthOffset, int Index);

    private sealed record Frame(int Ticks, IReadOnlyList<SlotView> Effects, ulong SeedAfter);

    private sealed class Outcome
    {
        public EntityScriptProxy Hero = null!;
        public int HitTick = -1;
        public List<Frame> Frames = new();

        /// <summary>The slots of the effects 4 and 5 (the only ones this slice creates) seen at the end of each frame.</summary>
        public IEnumerable<(int Ticks, SlotView Slot)> Seen(int effect) => Frames.SelectMany(f => f.Effects.Where(e => e.Effect == effect).Select(e => (f.Ticks, e)));

        public int? FirstSeenTick(int effect) => Seen(effect).Select(s => (int?)s.Ticks).FirstOrDefault();

        /// <summary>The tick count at the end of the first frame, after the first sighting, that holds no slot of the effect (null when it is still there at the end of the run).</summary>
        public int? FirstAbsentTick(int effect)
        {
            var first = FirstSeenTick(effect);
            return first == null ? null : Frames.Where(f => f.Ticks > first && f.Effects.All(e => e.Effect != effect)).Select(f => (int?)f.Ticks).FirstOrDefault();
        }
    }

    /// <summary>The scenario: the hero is hit once by a Beannoid whose attack attribute is <paramref name="attribute"/>; <paramref name="grouping"/> ticks per frame; the stream starts from <see cref="Seed"/> at the first tick.</summary>
    private static Outcome Run(int attribute, int grouping)
    {
        using var arena = new HitArena($"f3b-{attribute}-{grouping}", 212, 96, null, 24, 0, naturalTargetCount: 1);
        var attacker = arena.Targets[0];
        HitArena.Place(attacker, 212, 96);
        attacker.CurrentAttack = new BalanceAttack(attribute, 1);
        var outcome = new Outcome { Hero = arena.Hero };

        arena.BeforeTick = tick =>
        {
            if (tick == 1)
            {
                GameRandom.RandomSeed = Seed;
            }
        };
        arena.AfterTick = tick =>
        {
            if (outcome.HitTick < 0 && attacker.HitCounter >= 1)
            {
                outcome.HitTick = tick;
            }

            if (outcome.HitTick >= 0)
            {
                attacker.CurrentAttack = null;
            }
        };

        arena.Begin();
        var reservoir = arena.Arc.Proxy.EffectStage!.Reservoir;
        while (arena.Samples.Count < TotalTicks)
        {
            arena.Frame(0, grouping);
            var views = reservoir.ActiveSlots
                .Where(s => s.TargetEffect is 4 or 5)
                .Select(s => new SlotView(s.TargetEffect, s.Mode, s.Anchor, s.DepthOffset, s.Index))
                .ToList();
            outcome.Frames.Add(new Frame(arena.Samples.Count, views, GameRandom.RandomSeed));
        }

        return outcome;
    }

    private static ulong Step(ulong seed, int times)
    {
        for (var i = 0; i < times; i++)
        {
            seed = seed * 0x7d2b89dd + 0xe06a02e7;
        }

        return seed;
    }

    private static void AssertEffectSlot(Outcome outcome, int effect)
    {
        var seen = outcome.Seen(effect).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, s =>
        {
            Assert.Equal(1, s.Slot.Mode);
            Assert.Same(outcome.Hero, s.Slot.Anchor);
            Assert.Equal(1, s.Slot.DepthOffset);
        });
    }

    // (w1) The attribute 6 attaches the effect 4 to the hero for 40 ticks, the attribute 7 the effect 5 for 95 (the animation durations S; the slot is freed on its tick S + 1, and its tick 0 is the tick of the hit: the stage applies the operations of a tick before that tick's update of the reservoir, as for the sparks); the attribute 1 attaches nothing (the case "without effect" is in the same assertion).
    [Fact]
    public void W1_AHitOfTheNibble6Or7_AttachesTheEffect4Or5_ToTheHero_ForItsDuration()
    {
        var plain = Run(1, 1);
        Assert.True(plain.HitTick > 0, "the Beannoid hits the hero with the nibble 1");

        var fire = Run(6, 1);
        Assert.True(fire.HitTick > 0);
        AssertEffectSlot(fire, 4);
        Assert.Equal(fire.HitTick, fire.FirstSeenTick(4));
        Assert.Equal(fire.HitTick + 40 + 1, fire.FirstAbsentTick(4)); // 20 images x 2 ticks = 40, drawn on the ticks 0 to 40 and freed on the tick 41 (the reservoir's rule); the tick 0 of the slot is the hit tick (see below).
        Assert.Empty(fire.Seen(5));

        var ice = Run(7, 1);
        Assert.True(ice.HitTick > 0);
        AssertEffectSlot(ice, 5);
        Assert.Equal(ice.HitTick, ice.FirstSeenTick(5));
        Assert.Equal(ice.HitTick + 95 + 1, ice.FirstAbsentTick(5)); // 19 images x 5 ticks = 95, freed on the tick 96.
        Assert.Empty(ice.Seen(4));

        Assert.Empty(plain.Seen(4));
        Assert.Empty(plain.Seen(5));
    }

    // (w2) No number is drawn by the effect: the word of the stream after the hit is the one of a hit of the nibble 1, which a local step (one word per logic tick, three words per spark, three sparks) derives.
    // The effect slot of (w1) is asserted in the same test.
    [Fact]
    public void W2_TheEffectDrawsNoRandomWord()
    {
        var plain = Run(1, 1);
        var fire = Run(6, 1);
        AssertEffectSlot(fire, 4);

        foreach (var frame in plain.Frames.Where(f => f.Ticks > plain.HitTick))
        {
            var expected = Step(Seed, frame.Ticks + (3 * 3));
            var match = fire.Frames.Single(f => f.Ticks == frame.Ticks);
            Assert.Equal(expected, frame.SeedAfter); // the local step explains the baseline.
            Assert.Equal(frame.SeedAfter, match.SeedAfter);
        }
    }

    // (w3) Frames of 1, 2 and 3 ticks: in each grouping the same slot exists (effect 4, mode 1, anchor the hero, depth offset 1), first seen at the end of the frame that contains the same hit tick.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W3_TheSlotExists_InEveryGrouping_AtTheSameHitTick(int grouping)
    {
        var reference = Run(6, 1);
        var grouped = Run(6, grouping);
        Assert.True(reference.HitTick > 0);
        Assert.Equal(reference.HitTick, grouped.HitTick);
        AssertEffectSlot(grouped, 4);
        var endOfFrame = (grouped.HitTick + grouping - 1) / grouping * grouping;
        Assert.Equal(endOfFrame, grouped.FirstSeenTick(4));
    }
}
