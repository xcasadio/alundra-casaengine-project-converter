#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Rendering.Effects;

namespace Alundra.Tests;

/// <summary>
/// E20.f1b B2 (docs/plan-e20-heros.md section 1.9, ADR-0057 D-E20-28, ADR-0059): the stage lets the hero create its effects AT ONCE and release them at their tick. Alone, on a synthetic bank whose effect 0 of the map
/// table is a loop of 20 one-tick frames, so that <see cref="AlundraEffectSlot.FrameIndex"/> counts the updates a slot received.
/// </summary>
public sealed class AlundraEffectStageNowTests
{
    private sealed class FakeAnchor : IAlundraEffectAnchor
    {
        public int PosX { get; set; } = 3 << 16;
        public int PosY { get; set; } = 4 << 16;
        public int PosZ { get; set; } = 5 << 16;
        public uint DepthKey { get; set; } = 0x00690000;
        public int Status { get; set; } = 2;
    }

    private static AlundraEffectStage Stage()
    {
        var frames = Enumerable.Range(0, 20).Select(i => new[] { 1, i }).ToArray();
        var stage = new AlundraEffectStage();
        stage.Load(EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", frames) }, 20));
        return stage;
    }

    private static readonly bool[] NotFrozen = new bool[8];

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void ASlotCreatedAtOnce_AtTheTickKOfAFrameOfThreeTicks_IsUpdatedFromTheTickK(int tick, int updates)
    {
        var stage = Stage();
        stage.BeginSource(tick, 1);
        var slot = stage.CreateNow(true, 0, 0, 0, 0, 0);
        stage.EndSource();

        Assert.NotNull(slot);
        Assert.True(slot!.Active); // in the reservoir before the frame runs: the hero reads it at its next tick.
        Assert.Equal(0, slot.Index);
        Assert.Equal(0, slot.FrameIndex);

        stage.RunFrame(3, NotFrozen);

        Assert.Equal(updates, slot.FrameIndex);
    }

    [Fact]
    public void TheMarkOfASlotCreatedAtOnce_IsClearedAtTheEndOfTheFrame_EvenOfAFrameWithoutTick()
    {
        var stage = Stage();
        stage.BeginSource(2, 1);
        var slot = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        stage.EndSource();
        Assert.Equal(2, slot.FirstUpdateTick);

        stage.RunFrame(0, NotFrozen);
        Assert.Equal(0, slot.FirstUpdateTick);
        Assert.Equal(0, slot.FrameIndex);

        stage.RunFrame(2, NotFrozen); // the next frame: updated at both of its ticks, not missing the first two.
        Assert.Equal(2, slot.FrameIndex);
    }

    [Fact]
    public void ASlotCreatedAtOnce_AtTheLastTickOfAFrame_DoesNotMissAnUpdateInTheNextFrame()
    {
        var stage = Stage();
        stage.BeginSource(1, 1);
        var slot = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        stage.EndSource();

        stage.RunFrame(2, NotFrozen);
        Assert.Equal(1, slot.FrameIndex);
        stage.RunFrame(3, NotFrozen);
        Assert.Equal(4, slot.FrameIndex);
    }

    [Fact]
    public void CreateAttachedNow_GivesTheSlotOfMode1_FollowingItsAnchor_AndAFullReservoirGivesNull()
    {
        var stage = Stage();
        var anchor = new FakeAnchor();
        var slot = stage.CreateAttachedNow(true, 0, 0, anchor, 1, 0, 0, 0x100000)!;

        Assert.Equal((1, 1, 0x100000), (slot.Mode, slot.DepthOffset, slot.OffsetZ));
        Assert.Same(anchor, slot.Anchor);
        Assert.Equal((anchor.PosX, anchor.PosY, anchor.PosZ), (slot.X, slot.Y, slot.Z));

        for (var i = stage.Reservoir.ActiveCount; i < AlundraEffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(stage.CreateNow(true, 0, 0, 0, 0, 0));
        }

        Assert.Null(stage.CreateNow(true, 0, 0, 0, 0, 0));
        Assert.Null(stage.CreateAttachedNow(true, 0, 0, anchor, 1, 0, 0, 0));
    }

    [Fact]
    public void FreeAtTick_ReleasesTheSlotAtThePositionOfItsTick_NotBefore_SoAnEarlierQueuedCreationCannotTakeIt()
    {
        var stage = Stage();
        var slot = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        var generation = slot.Generation;

        stage.BeginSource(2, 1);
        stage.FreeAtTick(slot, generation);
        stage.EndSource();
        AlundraEffectSlot? atTick1 = null;
        AlundraEffectSlot? atTick3 = null;
        stage.Queue(1, 1, reservoir => atTick1 = reservoir.Create(true, 0, 0, 0, 0, 0));
        stage.Queue(3, 1, reservoir => atTick3 = reservoir.Create(true, 0, 0, 0, 0, 0));

        Assert.True(slot.Active); // the release is an operation of the stage, not an immediate write.

        stage.RunFrame(4, NotFrozen);

        Assert.Equal(1, atTick1!.Index); // the slot 0 was still held at the tick 1: the creation of the earlier tick goes to the next slot.
        Assert.Equal(0, atTick3!.Index); // freed at the tick 2: the creation of the tick 3 takes it.
    }

    [Fact]
    public void FreeAtTick_DoesNotFreeAnEffectThatTookTheSlot_TheGenerationOfTheCallerNoLongerMatches()
    {
        var stage = Stage();
        var aura = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        var generation = aura.Generation;
        stage.Reservoir.Free(aura); // the aura expired.
        var other = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        Assert.Equal(aura.Index, other.Index); // another effect took the slot.
        Assert.NotEqual(generation, other.Generation);

        stage.FreeAtTick(aura, generation);
        stage.RunFrame(1, NotFrozen);

        Assert.True(other.Active);
    }

    [Fact]
    public void TheGenerationOfASlot_ChangesAtEveryCreation_AndNeverIsZeroOnceCreated()
    {
        var stage = Stage();
        var first = stage.CreateNow(true, 0, 0, 0, 0, 0)!;
        var firstGeneration = first.Generation;
        stage.Reservoir.Free(first);
        var second = stage.CreateNow(true, 0, 0, 0, 0, 0)!;

        Assert.Same(first, second);
        Assert.NotEqual(0, firstGeneration);
        Assert.NotEqual(firstGeneration, second.Generation);
        Assert.True(stage.Reservoir.FreeIfGeneration(second, second.Generation));
        Assert.False(stage.Reservoir.FreeIfGeneration(second, second.Generation)); // already free.
    }
}
