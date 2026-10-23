#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Enemies;
using Alundra.Ai.Objects;
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
/// E20.f4 T1 (docs/plan-e20-heros.md section 1.20.1, (c1) and (c2)): the handler C94 (<c>0x8007C0D8</c>) of the charged arrow of William (sprite type 7) on an entity made by hand, and the key of the registry. (c1) At the counter <c>+0x1C</c> = 4 the trail appears
/// (the global effect 25 of the animation <c>+0x98</c>, at the position of the end of the previous tick, Z + <c>0x80000</c>, forces <c>(-F) &gt;&gt; 1</c>), at the counters 3 and 5 nothing does; the named stub of the cut of the grass is called at every call, a full reservoir
/// creates nothing and breaks nothing, the word of the generator never moves. The entity of (c1) is the one of <see cref="CutBench"/> (a host that is a world context with an effect stage, no frame box: the cut itself does nothing, <c>TileEffect</c> being proved elsewhere).
/// Each case without effect is set next to the positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChargedArrowAiTests : IDisposable
{
    private const int TrailEffect = 25;

    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public ChargedArrowAiTests() => Reset();

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
    }

    private static void Reset()
    {
        ChargedArrowAi.StubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    /// <summary>A bench whose entity is an arrow of type 7 in the status <paramref name="status"/> with the counter, the direction word, the position and the forces given; its host has a bank-less effect stage.</summary>
    private static CutBench Arrow(int counter, uint direction, int forceX, int forceY, EntityStatus status = EntityStatus.Normal)
    {
        var bench = CutBench.Create(frame: false);
        bench.Host.EffectStage = new EffectStage();
        var arrow = bench.Entity;
        arrow.SpriteType = 7;
        arrow.Status = status;
        arrow.FrameCounter = counter;
        arrow.TargetDirection = direction;
        arrow.PosX = 124 << 16;
        arrow.PosY = 100 << 16;
        arrow.PosZ = 8 << 16;
        arrow.ForceX = forceX;
        arrow.ForceY = forceY;
        return bench;
    }

    /// <summary>What a created slot held when the creator handed it to the test (the stage updates it, and moves it by its forces, in the same frame).</summary>
    private sealed record Trail(int Effect, bool MapTable, int Animation, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ);

    /// <summary>One call of C94 followed by the frame of the stage (the trail is queued, then created): the slots created, as they were at their creation.</summary>
    private static List<Trail> Call(CutBench bench)
    {
        var created = new List<Trail>();
        NativeEffects.CreatedForTests = slot => created.Add(new Trail(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ));
        ChargedArrowAi.Tick(bench.Entity);
        bench.Host.EffectStage!.RunFrame(1, new[] { false });
        NativeEffects.CreatedForTests = null;
        return created;
    }

    public static IEnumerable<object[]> DirectionsAndForces()
    {
        // the four words of the table 0x80023644 and the indexes they give (down, up, left, right); the forces: the speed of the arrow along its direction (393216 = 6 px), and 1 (the shift of -1 gives -1).
        foreach (var (word, index) in new (uint, int)[] { (0u, 0), (0x10u, 1), (8u, 2), (0x18u, 3) })
        {
            yield return new object[] { word, index, 393216, 0 };
            yield return new object[] { word, index, -393216, 262144 };
            yield return new object[] { word, index, 1, 1 };
        }
    }

    [Theory]
    [MemberData(nameof(DirectionsAndForces))]
    public void C1_AtTheCounter4_TheTrailAppears_WithTheIndexOfTheDirectionTheEndOfTickPositionZPlus0x80000AndHalfTheOppositeForces_AtTheCounters3And5_NoneDoes(uint word, int index, int forceX, int forceY)
    {
        GameRandom.RandomSeed = 0x12345678;
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var bench = Arrow(counter: 4, word, forceX, forceY);

        var created = Call(bench);

        var slot = Assert.Single(created);
        Assert.Equal((TrailEffect, false, index, 124 << 16, 100 << 16), (slot.Effect, slot.MapTable, slot.Animation, slot.X, slot.Y));
        Assert.Equal(unchecked((8 << 16) + 1 + 0x80000), slot.Z); // the binary's Z (PosZ + 1, ADR-0026) plus 8 pixels.
        Assert.Equal((-forceX >> 1, -forceY >> 1, 0), (slot.ForceX, slot.ForceY, slot.ForceZ));

        // The counters 3 and 5 (and 6, 7) create nothing; 8 creates again.
        foreach (var (counter, expected) in new[] { (3, 0), (5, 0), (6, 0), (7, 0), (8, 1) })
        {
            bench.Entity.FrameCounter = counter;
            Assert.Equal(expected, Call(bench).Count);
        }

        // The stub is called at every call (the 6 calls), for this entity, and the word of the generator is untouched.
        Assert.Equal(6, journal.Count);
        Assert.All(journal, e => Assert.Same(bench.Entity, e));
        Assert.Equal(0x12345678ul, GameRandom.RandomSeed & 0xFFFFFFFF);
    }

    [Fact]
    public void C1_TheStubIsCalledAtTheThreeTicksOfTheCounters3To5_TheTrailOnlyAtTheCounter4()
    {
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var bench = Arrow(counter: 3, 0x18, 393216, 0);
        var trails = new List<int>();
        for (var counter = 3; counter <= 5; counter++)
        {
            bench.Entity.FrameCounter = counter;
            trails.Add(Call(bench).Count);
        }

        Assert.Equal(new[] { 0, 1, 0 }, trails);
        Assert.Equal(3, journal.Count);
    }

    [Theory]
    [InlineData(EntityStatus.Normal, 1)]
    [InlineData(EntityStatus.Deactivated, 0)]
    [InlineData(EntityStatus.FlagToDestroy, 0)]
    public void C1_TheTrailIsMadeInTheStatusNormalOnly_TheStubIsCalledAllTheSame(EntityStatus status, int expectedTrails)
    {
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var bench = Arrow(counter: 4, 0x18, 393216, 0, status);

        Assert.Equal(expectedTrails, Call(bench).Count);
        Assert.Single(journal);
    }

    [Fact]
    public void C1_AFullReservoir_CreatesNoTrail_ButTheStubIsCalledAndNothingThrows_AndAFreeSlotCreatesOne()
    {
        GameRandom.RandomSeed = 0x12345678;
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var bench = Arrow(counter: 4, 0x18, 393216, 0);
        var stage = bench.Host.EffectStage!;
        for (var i = 0; i < EffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(stage.Reservoir.Create(false, 14, 0, 0, 0, 0));
        }

        var full = Call(bench);

        Assert.Empty(full);
        Assert.Single(journal);
        Assert.Equal(0x12345678ul, GameRandom.RandomSeed & 0xFFFFFFFF);
        Assert.DoesNotContain(stage.Reservoir.ActiveSlots, s => s.TargetEffect == TrailEffect);

        // A slot freed (the stage is a fresh one holding one free slot): the same call creates the trail.
        var freeBench = Arrow(counter: 4, 0x18, 393216, 0);
        for (var i = 0; i < EffectReservoir.SlotCount - 1; i++)
        {
            Assert.NotNull(freeBench.Host.EffectStage!.Reservoir.Create(false, 14, 0, 0, 0, 0));
        }

        Assert.Single(Call(freeBench));
    }

    [Fact]
    public void C1_WithoutAWorldContext_TheStubIsCalledAndNothingThrows_NoEffectIsMade()
    {
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var arrow = new EntityScriptProxy { SpriteType = 7, Status = EntityStatus.Normal, FrameCounter = 4, TargetDirection = 0x18, ForceX = 393216 };
        var created = new List<EffectSlot>();
        NativeEffects.CreatedForTests = created.Add;

        ChargedArrowAi.Tick(arrow);

        Assert.Same(arrow, Assert.Single(journal));
        Assert.Empty(created);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c2) the key (C, 94) of the registry: the type 7 only; the production registry holds it; the pin follows the keys of the classes.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C2_TheKeyC94_IsRegisteredForTheType7Only_AndTheKeysListsIt()
    {
        var registry = new NativeSpriteRegistry();
        ChargedArrowAi.Register(registry);

        Assert.Equal(1, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 94) }, ChargedArrowAi.Keys.ToArray());
        Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 94, 7, out var handler));
        Assert.NotNull(handler);
        foreach (var other in new[] { 3, 4, 11, 27, 335 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 94, other, out _), $"type {other}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 94, 7, out _)); // E1 is handled in line by the runner, not by this class.
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 61, 7, out _));
    }

    [Fact]
    public void C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses()
    {
        var production = new EventProgramRunner(null, GameState.Instance).NativeSprites;
        var sum = new NativeSpriteRegistry();
        BeannoideAi.Register(sum);
        MurutaAi.Register(sum);
        WaveSwordAi.Register(sum);
        PickupAi.Register(sum);
        ChargedArrowAi.Register(sum);
        ArrowAi.Register(sum); // E20.f5

        Assert.True(production.TryGet(ScriptHelper.ProgramCTick, 94, 7, out _));
        Assert.False(production.TryGet(ScriptHelper.ProgramCTick, 94, 3, out _));
        Assert.Equal(sum.Count, production.Count);
        Assert.Equal(3 + 3 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count, production.Count);
    }

    [Fact]
    public void C2_TheHandlerOfTheKey_CallsTheStubForTheEntityOfTheCall_AndCreatesTheTrailThroughTheRunner()
    {
        var runner = new EventProgramRunner(null, GameState.Instance);
        var journal = new List<EntityScriptProxy>();
        ChargedArrowAi.StubCallsForTests = journal;
        var bench = Arrow(counter: 4, 0x18, 393216, 0);
        bench.Entity.EventTrigger = ScriptHelper.ProgramCTick;
        bench.Entity.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = 94;
        var created = new List<EffectSlot>();
        NativeEffects.CreatedForTests = created.Add;

        bench.Entity.RunPickedEvent(runner);
        bench.Host.EffectStage!.RunFrame(1, new[] { false });

        Assert.Same(bench.Entity, Assert.Single(journal));
        Assert.Equal(1, runner.NativeHandlerRunCount);
        Assert.Equal(3, Assert.Single(created.Where(s => s.TargetEffect == TrailEffect)).TargetAnimation);

        // The same entity of another type does not run it.
        bench.Entity.SpriteType = 4;
        bench.Entity.RunPickedEvent(runner);
        Assert.Single(journal);
    }
}
