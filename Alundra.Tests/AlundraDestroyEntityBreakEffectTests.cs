#nullable enable
using System;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>
/// E14.a1b T3 (docs/plan-e14-ia-native.md section 1.3b, D-E14-9, ADR-0060): <c>DestroyEntity(e, fx)</c> (<c>0x8003A59C</c>), in the order of the binary, for ALL its callers: the status 4, <c>EventTrigger = -1</c>, and the break effect
/// (<c>fx = -1</c> reads the byte of the sprite header, the slope destruction passes 6) as a mode 1 effect attached to the frozen anchor of the entity. The loot (E20.e0), the release of the attached effect (E20.e) and the clearing
/// of the carrier (E20.g) are named stubs. Shares the generator's collection: the destruction must draw no number.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraDestroyEntityBreakEffectTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private readonly ulong _seed = AlundraRandom.RandomSeed;

    public void Dispose() => AlundraRandom.RandomSeed = _seed;

    private static AlundraWorldProxy World()
    {
        var world = new AlundraWorldProxy();
        world.EffectStage!.Load(EffectGolden.BankOf(Root, 14));
        return world;
    }

    private static AlundraEntityScriptProxy Victim(int breakEffect = 8)
        => new()
        {
            PosX = 0x00A40000, PosY = 0x01E00000, PosZ = 0x00080000, Status = EntityStatus.Normal, EventTrigger = ScriptHelper.ProgramEDeactivate, BreakEffect = breakEffect,
        };

    private static void RunFrame(AlundraWorldProxy world, int ticks) => world.EffectStage!.RunFrame(ticks, Enumerable.Repeat(false, ticks).ToList());

    private static AlundraEffectSlot[] Slots(AlundraWorldProxy world) => world.EffectStage!.Reservoir.ActiveSlots.ToArray();

    [Fact]
    public void TheTwoArgumentOverload_WritesTheStatus4_AndTheLetterMinus1()
    {
        var world = World();
        var victim = Victim(0);

        world.DestroyEntity(victim, -1);

        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
        Assert.Equal(-1, victim.EventTrigger);
    }

    [Fact]
    public void AByteOfZero_CreatesNoEffect()
    {
        var world = World();

        world.DestroyEntity(Victim(0), -1);
        RunFrame(world, 1);

        Assert.Empty(Slots(world));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    public void TheVramDestruction_AndE1_TakeTheByteOfTheHeader(int breakEffect)
    {
        var world = World();
        var victim = Victim(breakEffect);

        world.DestroyEntity(victim, -1);
        RunFrame(world, 0);

        var slot = Assert.Single(Slots(world));
        Assert.Equal(breakEffect, slot.TargetEffect);
        Assert.False(slot.TargetMapTable);
        Assert.Equal(0, slot.TargetAnimation);
        Assert.Equal(-1, slot.RecordIndex);
    }

    [Fact]
    public void TheSlopeDestruction_PassesTheEffect6_WhateverTheByteOfTheHeader()
    {
        var world = World();

        world.DestroyEntity(Victim(0), 6);
        world.DestroyEntity(Victim(8), 6);
        RunFrame(world, 0);

        var slots = Slots(world);
        Assert.Equal(2, slots.Length);
        Assert.All(slots, s => Assert.Equal(6, s.TargetEffect));
    }

    [Fact]
    public void TheEffect_IsAMode1SlotOfTheFrozenAnchor_AtTheEntity_BeforeItsFirstUpdate()
    {
        var world = World();
        var victim = Victim();
        var key = AlundraEntityDepthKey.Compute(victim);

        world.DestroyEntity(victim, -1);
        RunFrame(world, 0);

        var slot = Assert.Single(Slots(world));
        Assert.Equal(1, slot.Mode);
        Assert.NotNull(slot.Anchor);
        Assert.Equal(4, slot.Anchor!.Status);
        Assert.Equal(victim.PosX, slot.X);
        Assert.Equal(victim.PosY, slot.Y);
        Assert.Equal(victim.PosZ + 1, slot.Z);
        Assert.Equal((0, 0, 0), (slot.OffsetX, slot.OffsetY, slot.OffsetZ));
        Assert.Equal(1, slot.DepthOffset);
        Assert.Equal(key, slot.Anchor.DepthKey);
    }

    [Fact]
    public void AfterItsFirstUpdate_TheSlotIsFrozenAtTheEntityWithTheKeyPlus1()
    {
        var world = World();
        var victim = Victim();
        var key = AlundraEntityDepthKey.Compute(victim);

        world.DestroyEntity(victim, -1);
        RunFrame(world, 1);

        var slot = Assert.Single(Slots(world));
        Assert.Equal(2, slot.Mode);
        Assert.Equal((victim.PosX, victim.PosY, victim.PosZ + 1), (slot.X, slot.Y, slot.Z));
        Assert.Equal(key + 1u, slot.DepthKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheEffect_TakesTheSameSlotAndTheSameState_InAFrameOfOneTwoOrThreeTicks(int ticks)
    {
        var reference = World();
        var victim = Victim();
        reference.DestroyEntity(victim, -1);
        RunFrame(reference, 1);
        var expected = Slots(reference).Single();

        var world = World();
        var other = Victim();
        world.DestroyEntity(other, -1);
        // The entity is recycled and moved before the frame runs, as in a frame of several ticks (RecycleDestroyedEntities).
        var key = AlundraEntityDepthKey.Compute(other);
        other.Status = EntityStatus.Destroyed;
        other.PosX = 0x7000000;
        other.PosY = 0x7000000;
        other.PosZ = 0x7000000;
        RunFrame(world, ticks);

        var slot = Assert.Single(Slots(world));
        Assert.Equal(expected.Index, slot.Index);
        Assert.Equal(2, slot.Mode);
        Assert.Equal((expected.X, expected.Y, expected.Z), (slot.X, slot.Y, slot.Z));
        Assert.Equal(expected.DepthKey, slot.DepthKey);
        Assert.Equal(key + 1u, slot.DepthKey);
    }

    [Fact]
    public void ThreeOccupiedSlots_GiveTheEffectTheSlot3()
    {
        var world = World();
        var reservoir = world.EffectStage!.Reservoir;
        for (var i = 0; i < 3; i++)
        {
            Assert.NotNull(reservoir.Create(false, 9, 0, 0, 0, 0));
        }

        world.DestroyEntity(Victim(), -1);
        RunFrame(world, 0);

        Assert.Equal(3, Slots(world).Single(s => s.Mode == 1).Index);
    }

    [Fact]
    public void AFullReservoir_CreatesNothing_AndTheStatusAndTheLetterAreWrittenAllTheSame()
    {
        var world = World();
        var reservoir = world.EffectStage!.Reservoir;
        for (var i = 0; i < AlundraEffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(reservoir.Create(false, 9, 0, 0, 0, 0));
        }

        var victim = Victim();
        world.DestroyEntity(victim, -1);
        RunFrame(world, 0);

        Assert.Equal(AlundraEffectReservoir.SlotCount, Slots(world).Length);
        Assert.DoesNotContain(Slots(world), s => s.Mode == 1);
        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
        Assert.Equal(-1, victim.EventTrigger);
    }

    [Fact]
    public void TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents()
    {
        var world = World();
        AlundraRandom.RandomSeed = 0x12345678;
        var victim = Victim();
        victim.ContentsItemId = 69;

        world.DestroyEntity(victim, -1);
        RunFrame(world, 2);

        Assert.Equal(0x12345678UL, AlundraRandom.RandomSeed);
        Assert.Empty(((IEntityWorldContext)world).SpawnedEntities);
        Assert.Equal(69u, victim.ContentsItemId);
        Assert.Single(Slots(world));
    }

    [Fact]
    public void E1_ByRunPickedEvent_DestroysAndCreatesTheEffect_AndTheLetterIsMinus1Afterwards()
    {
        var world = World();
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), world);
        var victim = Victim();
        victim.ScriptHost = world;
        victim.Status = EntityStatus.Deactivated;
        victim.EventTrigger = ScriptHelper.ProgramEDeactivate;
        victim.ProgramIndexes[ScriptHelper.ProgramEDeactivate] = 0;
        victim.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = 1;

        victim.RunPickedEvent(runner);
        RunFrame(world, 0);

        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
        Assert.Equal(-1, victim.EventTrigger);
        Assert.Equal(8, Assert.Single(Slots(world)).TargetEffect);
    }

    [Fact]
    public void ThePickPhase_CallsTheSlopeAndTheVramDestruction_WithTheirEffects()
    {
        var world = World();
        var slope = Victim(0);
        slope.ScriptHost = world;
        slope.Flags = EntityFlags.DestroyOnSlidingSlope;
        slope.Slope_18c = 4;
        var vram = Victim(12);
        vram.ScriptHost = world;
        vram.Flags = EntityFlags.DestroyOnVramFlags;
        vram.CombinedVramFlagsOR = 0x8004U;

        slope.PickEventTrigger();
        vram.PickEventTrigger();
        RunFrame(world, 0);

        Assert.Equal(EntityStatus.FlagToDestroy, slope.Status);
        Assert.Equal(EntityStatus.FlagToDestroy, vram.Status);
        Assert.Equal(new[] { 6, 12 }, Slots(world).Select(s => s.TargetEffect).OrderBy(e => e).ToArray());
    }
}
