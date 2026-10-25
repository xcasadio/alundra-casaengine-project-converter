#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Projectiles;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f6b T1, phase A (docs/plan-e20-heros.md section 1.22.2, tests (b1) to (b4) and (b7)): the handlers C60 (<c>0x8007A8A0</c>, the trail) and E9 (<c>0x8007EF50</c>, the explosion and the puffs) of the fire and ice balls (sprite types 10 to 15) on an entity made by hand, and the keys of the registry.
/// The expected values are DERIVED, never pasted from the DLL (D-E0B-8): the generator is written out by hand in this file (<c>x' = x * 0x7D2B89DD + 0xE06A02E7</c>, 32 bits), and the same calls are set against the keys of the oracle <c>docs/plan-e20-annexe/e20-f4f6/</c> (the real code of <c>ALUN_CD.EXE</c>
/// in the MIPS machine) read by key (<see cref="BallOracle"/>). Each case without effect is set next to the positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BallAiTests : IDisposable
{
    private const int FireTrail = 17;
    private const int IceTrail = 18;
    private const uint FlagsOfTheBall = 0x132154;

    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public BallAiTests() => Reset();

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
    }

    private static void Reset()
    {
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The bench: the entity of CutBench (a host that is a world context with an effect stage, the destructions recorded) set up as a ball, and the generator written out by hand.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsFire(int type) => type is 11 or 12;

    private static int TrailId(int type) => IsFire(type) ? FireTrail : IceTrail;

    /// <summary>One step of the generator of the game, written out.</summary>
    private static uint Step(uint state) => unchecked(state * 0x7D2B89DDu + 0xE06A02E7u);

    private static uint After(uint seed, int steps)
    {
        var state = seed;
        for (var i = 0; i < steps; i++)
        {
            state = Step(state);
        }

        return state;
    }

    /// <summary>The high word of <c>draw * factor</c> (an unsigned product), as the binary computes the forces of its effects.</summary>
    private static int Hi(uint draw, uint factor) => (int)(((ulong)draw * factor) >> 32);

    private static uint State() => unchecked((uint)GameRandom.RandomSeed);

    /// <summary>A ball of type <paramref name="type"/> at (166, 100, <paramref name="posZ"/>) with the flags of the header (0x132154) and the fields E9 and C60 read.</summary>
    private static CutBench Ball(int type, int counter = 0, int slope = 0, uint animationAsked = 0, int endOfAnimation = 0, int posZ = 4 << 16, EntityStatus status = EntityStatus.Deactivated)
    {
        var bench = CutBench.Create(frame: false);
        bench.Host.EffectStage = new EffectStage();
        var ball = bench.Entity;
        ball.SpriteType = type;
        ball.Status = status;
        ball.FrameCounter = counter;
        ball.TargetAnimationId = animationAsked;
        ball.Slope_18c = slope;
        ball.ForceResetAnimationFlag = endOfAnimation;
        ball.Flags = FlagsOfTheBall;
        ball.PosX = 166 << 16;
        ball.PosY = 100 << 16;
        ball.PosZ = posZ;
        return bench;
    }

    /// <summary>What a created slot held when the creator handed it to the test (the stage updates it, and moves it by its forces, in the same frame).</summary>
    private sealed record Made(int Effect, bool MapTable, int Animation, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ)
    {
        /// <summary>The line of the oracle's format (pixels with 3 decimals, speeds with 4).</summary>
        internal string Line(int tick) => $"t{tick} id={Effect} dir={Animation} x={BallRun.F3(X)} y={BallRun.F3(Y)} z={BallRun.F3(Z)} v=({BallRun.F4(ForceX)},{BallRun.F4(ForceY)},{BallRun.F4(ForceZ)})";
    }

    /// <summary>The call of <paramref name="handler"/> on the ball of the bench, then the frame of the stage (the effects are queued, then created and drawn): the slots created, as they were at their creation.</summary>
    private static List<Made> Call(CutBench bench, Action<EntityScriptProxy> handler)
    {
        var created = new List<Made>();
        NativeEffects.CreatedForTests = slot => created.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ));
        handler(bench.Entity);
        bench.Host.EffectStage!.RunFrame(1, new[] { false });
        NativeEffects.CreatedForTests = null;
        return created;
    }

    private static List<Made> Explode(CutBench bench) => Call(bench, BallAi.Explode);

    private static List<Made> Trail(CutBench bench) => Call(bench, BallAi.Trail);

    /// <summary>The five effects of the first call of E9, derived from the generator: for the effect i the draws 2i and 2i+1, vx = hi(r1 * 0x60001) - 0x30000 and vy = hi(r2 * 0x40001) - 0x20000, no vertical speed.</summary>
    private static List<(int ForceX, int ForceY)> ExpectedExplosionSpeeds(uint seed, int effects = 5)
    {
        var list = new List<(int, int)>();
        var state = seed;
        for (var i = 0; i < effects; i++)
        {
            state = Step(state);
            var r1 = state;
            state = Step(state);
            var r2 = state;
            list.Add((Hi(r1, 0x60001) - 0x30000, Hi(r2, 0x40001) - 0x20000));
        }

        return list;
    }

    private static void Fill(EffectReservoir reservoir, int free)
    {
        for (var i = 0; i < EffectReservoir.SlotCount - free; i++)
        {
            Assert.NotNull(reservoir.Create(false, 14, 0, 0, 0, 0));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b1) E9, the first call (branch B): ten draws in order, five effects at the Z of the ball; the slopes 1 and 4 destroy with the effect 6, with no draw.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(15)]
    public void B1_TheFirstCallOfE9_OnTheFlatGround_MakesFiveEffectsAtTheZOfTheBall_TenDrawsInOrder_AndPosesTheAnimationAndTheFlags(int type)
    {
        const uint seed = 0x12345678;
        GameRandom.RandomSeed = seed;
        var bench = Ball(type);

        var made = Explode(bench);

        Assert.Equal(5, made.Count);
        Assert.All(made, m => Assert.Equal((TrailId(type), false, 0, 166 << 16, 100 << 16), (m.Effect, m.MapTable, m.Animation, m.X, m.Y)));
        Assert.All(made, m => Assert.Equal(unchecked((4 << 16) + 1), m.Z)); // the Z of the ball in the binary's frame (PosZ + 1, ADR-0026), with no offset for the fire either.
        var speeds = ExpectedExplosionSpeeds(seed);
        Assert.Equal(speeds, made.Select(m => (m.ForceX, m.ForceY)).ToList());
        Assert.All(made, m => Assert.Equal(0, m.ForceZ));
        Assert.Equal(After(seed, 10), State()); // ten draws, no more.
        Assert.Equal(1u, bench.Entity.TargetAnimationId);
        Assert.Equal((FlagsOfTheBall | 0x40u) & ~0x30u, bench.Entity.Flags); // 0x132154 becomes 0x132144.
        Assert.Empty(bench.Host.Destroyed);
    }

    [Theory]
    [InlineData("fire11_flat", 11, false)]
    [InlineData("ice14_flat", 14, false)]
    [InlineData("fire12_flat", 12, true)]
    [InlineData("ice15_flat", 15, true)]
    public void B1_TheFiveEffectsAndTheTenDraws_EqualTheKeyOfTheMachine(string name, int type, bool charged)
    {
        var key = charged ? BallOracle.LoadCharged(name) : BallOracle.LoadStepByImage(name);
        var tick = key.FirstTick + 11;
        var expected = key.Effects(TrailId(type)).Where(e => e.Tick == tick).ToList();
        Assert.Equal(5, expected.Count);
        GameRandom.RandomSeed = key.Rng0(tick);
        var bench = Ball(type, posZ: key.Raw(tick - 1, "pz") - 1);
        bench.Entity.PosX = key.Raw(tick - 1, "px");
        bench.Entity.PosY = key.Raw(tick - 1, "py");

        var made = Explode(bench);

        Assert.Equal(expected.Select(e => e.Line(tick)).ToList(), made.Select(m => m.Line(tick)).ToList());
        Assert.Equal(key.Rng1(tick), State());
        Assert.Equal(10, key.Draws(tick));
    }

    [Fact]
    public void B1_TheSlopes1And4_DestroyTheBallWithTheEffect6_NoDrawNoEffect_TheOtherSlopesExplode()
    {
        foreach (var slope in new[] { 0, 1, 2, 3, 4 })
        {
            const uint seed = 0x12345678;
            GameRandom.RandomSeed = seed;
            var bench = Ball(11, slope: slope);

            var made = Explode(bench);

            if (slope is 1 or 4)
            {
                var destroyed = Assert.Single(bench.Host.Destroyed);
                Assert.Same(bench.Entity, destroyed.Entity);
                Assert.Equal(6, destroyed.EffectId);
                Assert.Empty(made);
                Assert.Equal(seed, State()); // not one draw.
                Assert.Equal(0u, bench.Entity.TargetAnimationId); // nothing written: the ball is gone.
                Assert.Equal(FlagsOfTheBall, bench.Entity.Flags);
            }
            else
            {
                Assert.Empty(bench.Host.Destroyed);
                Assert.Equal(5, made.Count);
                Assert.Equal(After(seed, 10), State());
                Assert.Equal(1u, bench.Entity.TargetAnimationId);
            }
        }
    }

    [Fact]
    public void B1_AFullReservoir_SkipsTheWritesAndTheDrawsOfTheEffects_AFreeSlotKeepsItsOwnTwoDraws_TheBallGoesOnAllTheSame()
    {
        const uint seed = 0x12345678;
        GameRandom.RandomSeed = seed;
        var full = Ball(11);
        Fill(full.Host.EffectStage!.Reservoir, free: 0);

        var none = Explode(full);

        Assert.Empty(none);
        Assert.Equal(seed, State()); // no effect created, no number drawn.
        Assert.Equal(1u, full.Entity.TargetAnimationId); // the ball is not stuck on the first call.
        Assert.Equal((FlagsOfTheBall | 0x40u) & ~0x30u, full.Entity.Flags);
        Assert.Empty(full.Host.Destroyed);

        // Two free slots: the first two effects are made (each with its own two draws), the other three are skipped without a draw.
        GameRandom.RandomSeed = seed;
        var few = Ball(11);
        Fill(few.Host.EffectStage!.Reservoir, free: 2);

        var two = Explode(few);

        Assert.Equal(ExpectedExplosionSpeeds(seed, 2), two.Select(m => (m.ForceX, m.ForceY)).ToList());
        Assert.Equal(After(seed, 4), State());
    }

    [Fact]
    public void B1_TheFirstCall_IsEveryCallWhoseAnimationAskedIsNotOne_TheCallWithTheAnimationOneIsNot()
    {
        foreach (var asked in new uint[] { 0, 2, 3 })
        {
            GameRandom.RandomSeed = 0x12345678;
            var first = Explode(Ball(11, animationAsked: asked));
            Assert.Equal(5, first.Count);
        }

        // The ball whose animation asked is 1 (the explosion was made): at the counter 3 nothing is made, and no draw.
        GameRandom.RandomSeed = 0x12345678;
        var again = Ball(11, counter: 3, animationAsked: 1);
        Assert.Empty(Explode(again));
        Assert.Equal(0x12345678u, State());
        Assert.Equal(1u, again.Entity.TargetAnimationId);
    }

    [Fact]
    public void B1_WithoutAWorldContext_NothingIsMadeAndNothingThrows_WithOneTheFiveEffectsAppear()
    {
        var created = new List<EffectSlot>();
        NativeEffects.CreatedForTests = created.Add;
        var alone = new EntityScriptProxy { SpriteType = 11, Status = EntityStatus.Deactivated, Flags = FlagsOfTheBall, PosX = 166 << 16, PosY = 100 << 16 };
        BallAi.Explode(alone);
        BallAi.Trail(new EntityScriptProxy { SpriteType = 11, Status = EntityStatus.Normal, FrameCounter = 4, ForceX = 393216 });
        Assert.Empty(created);

        Assert.Equal(5, Explode(Ball(11)).Count);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b2) E9, the later calls (branch A): a puff every 8 ticks (two draws, the offset of Z for the fire only), the slopes 1 and 4 destroy first and the puff of the same tick follows, the end of the animation destroys without a puff.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(15)]
    public void B2_APuffEvery8Ticks_TwoDraws_TheFireAtZPlus0x80000_TheIceWithNoOffset_AtTheOtherCountersNothingIsMadeAndNothingDrawn(int type)
    {
        foreach (var counter in Enumerable.Range(1, 17))
        {
            const uint seed = 0x12345678;
            GameRandom.RandomSeed = seed;
            var bench = Ball(type, counter, animationAsked: 1);

            var made = Explode(bench);

            if (counter is 8 or 16)
            {
                var puff = Assert.Single(made);
                var r1 = Step(seed);
                var r2 = Step(r1);
                Assert.Equal((TrailId(type), false, 0, 166 << 16, 100 << 16), (puff.Effect, puff.MapTable, puff.Animation, puff.X, puff.Y));
                Assert.Equal(unchecked((4 << 16) + 1 + (IsFire(type) ? 0x80000 : 0)), puff.Z);
                Assert.Equal((Hi(r1, 0x30001) - 0x18000, Hi(r2, 0x20001) - 0x10000, 0x20000), (puff.ForceX, puff.ForceY, puff.ForceZ));
                Assert.Equal(r2, State());
                Assert.Equal(1u, bench.Entity.TargetAnimationId);
            }
            else
            {
                Assert.Empty(made);
                Assert.Equal(seed, State());
            }

            Assert.Empty(bench.Host.Destroyed);
        }
    }

    [Theory]
    [InlineData("fire11_flat", 11, false)]
    [InlineData("ice14_flat", 14, false)]
    [InlineData("fire12_flat", 12, true)]
    [InlineData("ice15_flat", 15, true)]
    public void B2_TheFirstPuff_EqualsTheKeyOfTheMachine_TheEffectAndItsTwoDraws(string name, int type, bool charged)
    {
        var key = charged ? BallOracle.LoadCharged(name) : BallOracle.LoadStepByImage(name);
        var puffs = key.Effects(TrailId(type)).Where(e => e.Tick > key.FirstTick + 11).ToList();
        var first = puffs.First();
        Assert.Equal(key.FirstTick + 16, first.Tick);
        GameRandom.RandomSeed = key.Rng0(first.Tick);
        var bench = Ball(type, counter: key.Raw(first.Tick, "cnt1c") - 1, animationAsked: 1, posZ: key.Raw(first.Tick - 1, "pz") - 1);
        bench.Entity.PosX = key.Raw(first.Tick - 1, "px");
        bench.Entity.PosY = key.Raw(first.Tick - 1, "py");

        var made = Explode(bench);

        Assert.Equal(first.Line(first.Tick), Assert.Single(made).Line(first.Tick));
        Assert.Equal(key.Rng1(first.Tick), State());
        Assert.Equal(2, key.Draws(first.Tick));
    }

    [Fact]
    public void B2_AtASlope1Or4_AtAMultipleOf8_TheDestructionIsFollowedByThePuffOfTheSameTick_AtAnotherTickTheDestructionIsAlone_TheSlope2DestroysNothing()
    {
        foreach (var slope in new[] { 1, 4, 2 })
        {
            foreach (var counter in new[] { 16, 17 })
            {
                const uint seed = 0x12345678;
                GameRandom.RandomSeed = seed;
                var bench = Ball(11, counter, slope, animationAsked: 1);

                var made = Explode(bench);

                var destroyed = bench.Host.Destroyed;
                if (slope is 1 or 4)
                {
                    var one = Assert.Single(destroyed);
                    Assert.Equal((bench.Entity, 6), (one.Entity, one.EffectId));
                }
                else
                {
                    Assert.Empty(destroyed);
                }

                if (counter == 16)
                {
                    Assert.Single(made); // the puff, destroyed or not.
                    Assert.Equal(After(seed, 2), State());
                }
                else
                {
                    Assert.Empty(made);
                    Assert.Equal(seed, State());
                }
            }
        }
    }

    [Fact]
    public void B2_WhenTheAnimationHasEnded_TheBallIsDestroyedWithTheBreakEffectOfItsHeader_NoPuff_EvenAtAMultipleOf8_TheSlope4MakesTwoDestructionsInTheSameTick()
    {
        const uint seed = 0x12345678;

        // At the counter 16 the end of the animation (+0xAC == 1) destroys with -1 (the effect of the header) and makes no puff; without it, the puff is made.
        GameRandom.RandomSeed = seed;
        var ended = Ball(11, counter: 16, animationAsked: 1, endOfAnimation: 1);
        Assert.Empty(Explode(ended));
        var destroyed = Assert.Single(ended.Host.Destroyed);
        Assert.Equal((ended.Entity, -1), (destroyed.Entity, destroyed.EffectId));
        Assert.Equal(seed, State());

        GameRandom.RandomSeed = seed;
        Assert.Single(Explode(Ball(11, counter: 16, animationAsked: 1, endOfAnimation: 0)));

        // The slope 4 at the same tick: the destruction by the slope (6), then the one of the end of the animation (-1), and no puff, no draw.
        GameRandom.RandomSeed = seed;
        var both = Ball(11, counter: 16, slope: 4, animationAsked: 1, endOfAnimation: 1);
        Assert.Empty(Explode(both));
        Assert.Equal(new[] { 6, -1 }, both.Host.Destroyed.Select(d => d.EffectId).ToArray());
        Assert.All(both.Host.Destroyed, d => Assert.Same(both.Entity, d.Entity));
        Assert.Equal(seed, State());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b3) C60: a trail every 4 ticks, the effect 17 (fire) or 18 (ice), the speed opposite to the forces, the offset of Z of the fire only.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> TypesAndForces()
    {
        foreach (var type in new[] { 10, 11, 12, 13, 14, 15 })
        {
            yield return new object[] { type, 393216, 0, -32768 };
            yield return new object[] { type, -393216, 262144, 98304 };
            yield return new object[] { type, 0, -131072, 0 };
        }
    }

    [Theory]
    [MemberData(nameof(TypesAndForces))]
    public void B3_TheTrailAppearsAtTheCounters4And8_TheEffectOfTheTypeAtTheEndOfTickPositionWithTheOppositeForces_AtTheOtherCountersNoneDoes(int type, int forceX, int forceY, int forceZ)
    {
        GameRandom.RandomSeed = 0x12345678;
        var fire = IsFire(type);
        foreach (var counter in Enumerable.Range(1, 9))
        {
            var bench = Ball(type, counter, status: EntityStatus.Normal);
            bench.Entity.ForceX = forceX;
            bench.Entity.ForceY = forceY;
            bench.Entity.ForceZ = forceZ;

            var made = Trail(bench);

            if (counter is 4 or 8)
            {
                var slot = Assert.Single(made);
                Assert.Equal((fire ? FireTrail : IceTrail, false, 0, 166 << 16, 100 << 16), (slot.Effect, slot.MapTable, slot.Animation, slot.X, slot.Y));
                Assert.Equal(unchecked((4 << 16) + 1 + (fire ? 0x80000 : 0)), slot.Z); // the binary's Z, plus 8 pixels for the fire only.
                Assert.Equal((-forceX, -forceY, -forceZ), (slot.ForceX, slot.ForceY, slot.ForceZ));
            }
            else
            {
                Assert.Empty(made);
            }
        }

        Assert.Equal(0x12345678u, State()); // no draw.
    }

    [Theory]
    [InlineData("fire11_flat", 11, false)]
    [InlineData("ice14_flat", 14, false)]
    [InlineData("fire12_flat", 12, true)]
    [InlineData("ice15_flat", 15, true)]
    public void B3_TheTwoFirstTrails_EqualTheKeyOfTheMachine_TheBallAsItWasAtTheEndOfThePreviousTick(string name, int type, bool charged)
    {
        var key = charged ? BallOracle.LoadCharged(name) : BallOracle.LoadStepByImage(name);
        var trails = key.Effects(TrailId(type)).Where(e => e.Tick >= key.FirstTick && e.Tick < key.FirstTick + 11).ToList();
        Assert.Equal(new[] { key.FirstTick + 4, key.FirstTick + 8 }, trails.Select(t => t.Tick).ToArray());
        foreach (var trail in trails)
        {
            var before = trail.Tick - 1;
            var bench = Ball(type, counter: key.Raw(trail.Tick, "cnt1c") - 1, posZ: key.Raw(before, "pz") - 1, status: EntityStatus.Normal);
            bench.Entity.PosX = key.Raw(before, "px");
            bench.Entity.PosY = key.Raw(before, "py");
            bench.Entity.ForceX = key.Raw(before, "fx");
            bench.Entity.ForceY = key.Raw(before, "fy");
            bench.Entity.ForceZ = key.Raw(before, "fz");

            Assert.Equal(trail.Line(trail.Tick), Assert.Single(Trail(bench)).Line(trail.Tick));
        }
    }

    [Fact]
    public void B3_AFullReservoir_CreatesNoTrail_AFreeSlotCreatesOne()
    {
        var full = Ball(11, 4, status: EntityStatus.Normal);
        Fill(full.Host.EffectStage!.Reservoir, free: 0);
        Assert.Empty(Trail(full));
        Assert.DoesNotContain(full.Host.EffectStage!.Reservoir.ActiveSlots, s => s.TargetEffect == FireTrail);

        var free = Ball(11, 4, status: EntityStatus.Normal);
        Fill(free.Host.EffectStage!.Reservoir, free: 1);
        Assert.Single(Trail(free));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b4) the draws are those of the generator at the time the stage applies its queue (D-E0B-3(a)), so the values are a local step of the generator and the ten draws of the five effects are made in the order of the effects.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheDrawsAreMadeWhenTheStageAppliesItsQueue_NotWhenTheHandlerRuns_AndTheyEqualALocalStepOfTheGenerator()
    {
        const uint seed = 0x12345678;
        GameRandom.RandomSeed = seed;
        var bench = Ball(11);

        BallAi.Explode(bench.Entity);

        Assert.Equal(seed, State()); // the handler has drawn nothing yet: the effects are queued.
        Assert.Empty(bench.Host.EffectStage!.Reservoir.ActiveSlots);

        var created = new List<Made>();
        NativeEffects.CreatedForTests = slot => created.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ));
        bench.Host.EffectStage.RunFrame(1, new[] { false });

        Assert.Equal(5, created.Count);
        Assert.Equal(After(seed, 10), State());
        Assert.Equal(ExpectedExplosionSpeeds(seed), created.Select(m => (m.ForceX, m.ForceY)).ToList());

        // A generator moved between the call and the application is the one the draws use: the values follow the state at the application.
        const uint moved = 0xCAFEF00D;
        GameRandom.RandomSeed = seed;
        var late = Ball(11);
        BallAi.Explode(late.Entity);
        GameRandom.RandomSeed = moved;
        created.Clear();
        late.Host.EffectStage!.RunFrame(1, new[] { false });
        Assert.Equal(ExpectedExplosionSpeeds(moved), created.Select(m => (m.ForceX, m.ForceY)).ToList());
        NativeEffects.CreatedForTests = null;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b7) the table of the registry: Register adds the keys (C, 60) and (E, 9) for the types 10 to 15, on a new registry.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B7_Register_AddsTheKeysC60AndE9_ForTheTypes10To15Only_AndTheDisposableTakesThemOff()
    {
        var registry = new NativeSpriteRegistry();
        Assert.Equal(0, registry.Count);

        var registration = BallAi.Register(registry);

        Assert.Equal(2, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 60), (ScriptHelper.ProgramEDeactivate, 9) }, BallAi.Keys.ToArray());
        Assert.Equal(new[] { 10, 11, 12, 13, 14, 15 }, BallAi.SpriteTypes);
        foreach (var type in BallAi.SpriteTypes)
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 60, type, out var tick), $"C60 for {type}");
            Assert.NotNull(tick);
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 9, type, out var deactivate), $"E9 for {type}");
            Assert.NotNull(deactivate);
        }

        foreach (var other in new[] { 3, 4, 7, 9, 16, 17, 27, 335 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 60, other, out _), $"C60 for {other}");
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 9, other, out _), $"E9 for {other}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 60, 11, out _)); // the codes sit in their own slots.
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 9, 11, out _));

        // A second registration of the same keys throws (the original has one function per entry); the disposable frees them.
        Assert.Throws<InvalidOperationException>(() => BallAi.Register(registry));
        registration.Dispose();
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 60, 11, out _));
        using var again = BallAi.Register(registry);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void B7_TheHandlersOfTheKeys_RunThroughTheRunner_TheTrailAtTheCounter4AndTheExplosion_AnotherTypeRunsNeither()
    {
        var runner = new EventProgramRunner(null, GameState.Instance);
        using var hook = BallHook.Install(runner);
        GameRandom.RandomSeed = 0x12345678;
        var created = new List<EffectSlot>();
        NativeEffects.CreatedForTests = created.Add;

        var ball = Ball(11, counter: 4, status: EntityStatus.Normal);
        ball.Entity.EventTrigger = ScriptHelper.ProgramCTick;
        ball.Entity.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = 60;
        ball.Entity.RunPickedEvent(runner);
        ball.Host.EffectStage!.RunFrame(1, new[] { false });
        Assert.Equal(new[] { FireTrail }, created.Select(s => s.TargetEffect).ToArray());

        created.Clear();
        ball.Entity.Status = EntityStatus.Deactivated;
        ball.Entity.EventTrigger = ScriptHelper.ProgramEDeactivate;
        ball.Entity.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = 9;
        ball.Entity.RunPickedEvent(runner);
        ball.Host.EffectStage!.RunFrame(1, new[] { false });
        Assert.Equal(5, created.Count);
        Assert.Equal(2, runner.NativeHandlerRunCount);

        // The same programs on an arrow (type 4) are not the balls': nothing is made.
        created.Clear();
        var arrow = Ball(4, counter: 4, status: EntityStatus.Normal);
        arrow.Entity.EventTrigger = ScriptHelper.ProgramCTick;
        arrow.Entity.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = 60;
        arrow.Entity.RunPickedEvent(runner);
        arrow.Host.EffectStage!.RunFrame(1, new[] { false });
        Assert.Empty(created);
        Assert.Equal(2, runner.NativeHandlerRunCount);
        NativeEffects.CreatedForTests = null;
    }
}
