#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Tests.Support;
using Alundra.Tests.Rng;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>
/// E14.a1b T3 (docs/plan-e14-ia-native.md section 1.3b, D-E14-9, ADR-0060): <c>DestroyEntity(e, fx)</c> (<c>0x8003A59C</c>), in the order of the binary, for ALL its callers: the status 4, <c>EventTrigger = -1</c>, and the break effect
/// (<c>fx = -1</c> reads the byte of the sprite header, the slope destruction passes 6) as a mode 1 effect attached to the frozen anchor of the entity. The release of the attached effect (E20.e) and the clearing
/// of the carrier (E20.g) are named stubs; the loot (E20.e0c, phase C) is its first act. Shares the generator's collection: the destruction must draw no number.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class DestroyEntityBreakEffectTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private readonly ulong _seed = GameRandom.RandomSeed;

    public void Dispose() => GameRandom.RandomSeed = _seed;

    private static WorldProxy World()
    {
        var world = new WorldProxy();
        world.EffectStage!.Load(EffectGolden.BankOf(Root, 14));
        return world;
    }

    private static EntityScriptProxy Victim(int breakEffect = 8)
        => new()
        {
            PosX = 0x00A40000, PosY = 0x01E00000, PosZ = 0x00080000, Status = EntityStatus.Normal, EventTrigger = ScriptHelper.ProgramEDeactivate, BreakEffect = breakEffect,
        };

    private static void RunFrame(WorldProxy world, int ticks) => world.EffectStage!.RunFrame(ticks, Enumerable.Repeat(false, ticks).ToList());

    private static EffectSlot[] Slots(WorldProxy world) => world.EffectStage!.Reservoir.ActiveSlots.ToArray();

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
        var key = EntityDepthKey.Compute(victim);

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
        var key = EntityDepthKey.Compute(victim);

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
        var key = EntityDepthKey.Compute(other);
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
        for (var i = 0; i < EffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(reservoir.Create(false, 9, 0, 0, 0, 0));
        }

        var victim = Victim();
        world.DestroyEntity(victim, -1);
        RunFrame(world, 0);

        Assert.Equal(EffectReservoir.SlotCount, Slots(world).Length);
        Assert.DoesNotContain(Slots(world), s => s.Mode == 1);
        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
        Assert.Equal(-1, victim.EventTrigger);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // E20.e0c T3 (docs/plan-e20-heros.md section 1.26, phase C, (d1) and (d3)): the loot is the first act of DestroyEntity(e, fx) (0x80032B90 at 0x8003A59C); the light destruction (0x8003A774) drops nothing.
    // An object appears only in an initialised world with the catalogue: these tests run in the REAL world of the map 14 (real prefabs), emptied of every entity but the hero.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const int Gildas = 0x45;
    private const int TypeOfItemZero = 0x1E;
    private const int MagicExtract = 0x26;

    private static List<EntityScriptProxy> Born(PickupDropRun run, HashSet<EntityScriptProxy> before)
        => run.Context.SpawnedEntities.Where(e => !before.Contains(e)).ToList();

    private static HashSet<EntityScriptProxy> Snapshot(PickupDropRun run)
    {
        run.Sounds.Requests.Clear();
        return new HashSet<EntityScriptProxy>(run.Context.SpawnedEntities, ReferenceEqualityComparer.Instance);
    }

    [Fact]
    public void TheDestruction_DropsTheContents_AtTheEntity_DrawsNoNumber_AndPlaysTheSound84_WhileAContentOfZeroDropsNothing()
    {
        using var run = PickupDropRun.OnMap14("destroy-contents");
        var world = run.Arc.Proxy;
        GameRandom.RandomSeed = 0x12345678;
        var victim = Victim();
        victim.PosZ = 6 << 20; // above the floor of the map at this place, so that the apparition does not lift the object.
        victim.ContentsItemId = Gildas;
        var before = Snapshot(run);

        world.DestroyEntity(victim, -1);

        var drop = Assert.Single(Born(run, before));
        Assert.Equal(Gildas + TypeOfItemZero, drop.SpriteType);
        Assert.Equal((victim.PosX, victim.PosY, unchecked(victim.PosZ + 1)), (drop.PosX, drop.PosY, drop.PosZ)); // at the entity, one raw unit up (D-E0A-1).
        Assert.Equal(1u, PickupAi.ReadWord274(drop));
        Assert.Equal(0, drop.ItemState);
        Assert.Equal(600, drop.DelayOrAngleOrEntityId);
        Assert.Equal(84, run.Sounds.Requests[^1]);
        Assert.Equal(0x12345678UL, GameRandom.RandomSeed);
        Assert.Equal((uint)Gildas, victim.ContentsItemId);
        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
        Assert.Equal(-1, victim.EventTrigger);

        // The pair: the same destruction with nothing to drop drops nothing and plays nothing.
        var empty = Victim();
        empty.ContentsItemId = 0;
        before = Snapshot(run);

        world.DestroyEntity(empty, -1);

        Assert.Empty(Born(run, before));
        Assert.Empty(run.Sounds.Requests);
        Assert.Equal(0x12345678UL, GameRandom.RandomSeed);
        Assert.Equal(EntityStatus.FlagToDestroy, empty.Status);
    }

    [Fact]
    public void TheDestruction_DropsTheObjectsOfTheMagic_OnlyWhenTheHeroHasMagic()
    {
        using var run = PickupDropRun.OnMap14("destroy-veto");
        var world = run.Arc.Proxy;
        var victim = Victim();
        victim.ContentsItemId = MagicExtract;

        GameState.Instance.PlayerStats.MpMax = 0;
        var before = Snapshot(run);
        world.DestroyEntity(victim, -1);
        Assert.Empty(Born(run, before));
        Assert.Empty(run.Sounds.Requests);

        var other = Victim();
        other.ContentsItemId = MagicExtract;
        GameState.Instance.PlayerStats.MpMax = 5;
        before = Snapshot(run);
        world.DestroyEntity(other, -1);
        var drop = Assert.Single(Born(run, before));
        Assert.Equal(MagicExtract + TypeOfItemZero, drop.SpriteType);
        Assert.Equal(84, run.Sounds.Requests[^1]);
    }

    [Fact]
    public void TheDrop_IsTheFirstAct_TheEntityStillHasItsStatusAndItsLetterWhenTheObjectAppears()
    {
        using var run = PickupDropRun.OnMap14("destroy-order");
        var world = run.Arc.Proxy;
        var victim = Victim();
        victim.ContentsItemId = Gildas;
        (EntityStatus Status, int Letter)? seen = null;
        WorldProxy.CreationFaultHookForTests = (_, _) => seen ??= (victim.Status, victim.EventTrigger);
        try
        {
            world.DestroyEntity(victim, -1);
        }
        finally
        {
            WorldProxy.CreationFaultHookForTests = null;
        }

        Assert.Equal((EntityStatus.Normal, ScriptHelper.ProgramEDeactivate), seen);
        Assert.Equal(EntityStatus.FlagToDestroy, victim.Status);
    }

    [Fact]
    public void TheLightDestruction_DropsNothing_WhileTheTwoArgumentDestructionOfTheSameContentsDropsIt()
    {
        using var run = PickupDropRun.OnMap14("destroy-light");
        var world = run.Arc.Proxy;
        GameRandom.RandomSeed = 0x12345678;
        var light = Victim();
        light.ContentsItemId = Gildas;
        var before = Snapshot(run);

        world.DestroyEntity(light);

        Assert.Empty(Born(run, before));
        Assert.Empty(run.Sounds.Requests);
        Assert.Equal(EntityStatus.FlagToDestroy, light.Status);

        // The pair: the same contents through the two-argument destruction.
        var full = Victim();
        full.ContentsItemId = Gildas;
        world.DestroyEntity(full, -1);
        Assert.Single(Born(run, before));
        Assert.Equal(0x12345678UL, GameRandom.RandomSeed);
    }

    [Fact]
    public void E1_ByRunPickedEvent_DestroysAndCreatesTheEffect_AndTheLetterIsMinus1Afterwards()
    {
        var world = World();
        var runner = new EventProgramRunner(null, new GameState(), world);
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
