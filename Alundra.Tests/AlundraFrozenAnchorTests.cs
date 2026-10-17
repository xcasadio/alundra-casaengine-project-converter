#nullable enable
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Alundra.Entities;
using Alundra.Rendering.Effects;

namespace Alundra.Tests;

/// <summary>
/// E14.a1b T2 (docs/plan-e14-ia-native.md section 1.3b): the frozen anchor of the break effect (<see cref="FrozenEffectAnchor"/>) is a snapshot of the destroyed entity (status 4, X, Y, <c>PosZ + 1</c>, the depth key of
/// E20.f1b), and an effect created from it in a frame of two or three ticks keeps the position and the key of the entity after the entity was recycled (status 0) and moved.
/// </summary>
public sealed class AlundraFrozenAnchorTests
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    private static AlundraEntityScriptProxy Destroyed()
    {
        var entity = new AlundraEntityScriptProxy { PosX = 0x00A40000, PosY = 0x01E00000, PosZ = 0x00080000, Status = EntityStatus.Normal };
        entity.Status = EntityStatus.FlagToDestroy;
        return entity;
    }

    [Fact]
    public void TheSnapshot_HoldsTheStatus4_ThePositionWithTheBinarysZ_AndTheDepthKeyOfTheEntity()
    {
        var entity = Destroyed();
        IEffectAnchor live = entity;

        var frozen = FrozenEffectAnchor.Of(entity);

        Assert.Equal(4, frozen.Status);
        Assert.Equal(0x00A40000, frozen.PosX);
        Assert.Equal(0x01E00000, frozen.PosY);
        Assert.Equal(0x00080001, frozen.PosZ);
        Assert.Equal(EntityDepthKey.Compute(entity), frozen.DepthKey);
        Assert.Equal(live.SortCoordinate, frozen.SortCoordinate);
    }

    [Fact]
    public void TheSnapshot_DoesNotFollowTheEntity()
    {
        var entity = Destroyed();
        var frozen = FrozenEffectAnchor.Of(entity);
        var key = frozen.DepthKey;

        entity.Status = EntityStatus.Destroyed;
        entity.PosX = 0x7000000;
        entity.PosY = 0x7000000;
        entity.PosZ = 0x7000000;

        Assert.Equal(4, frozen.Status);
        Assert.Equal(0x00A40000, frozen.PosX);
        Assert.Equal(0x01E00000, frozen.PosY);
        Assert.Equal(0x00080001, frozen.PosZ);
        Assert.Equal(key, frozen.DepthKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AnEffectCreatedInAFrameOfSeveralTicks_KeepsThePositionAndTheKeyOfTheRecycledEntity(int ticks)
    {
        var stage = new EffectStage();
        stage.Load(EffectGolden.BankOf(Root, 14));
        var entity = Destroyed();
        var expectedKey = EntityDepthKey.Compute(entity);
        var frozen = FrozenEffectAnchor.Of(entity);
        stage.BeginSource(0, 1);
        stage.Emit(reservoir => NativeEffects.CreateBreakEffect(reservoir, 8, frozen));
        stage.EndSource();

        // The entity is recycled and released before the frame that applies the operation runs, as in a frame of several ticks.
        entity.Status = EntityStatus.Destroyed;
        entity.PosX = 0x7000000;
        entity.PosY = 0x7000000;

        stage.RunFrame(ticks, Enumerable.Repeat(false, ticks).ToList());

        var slot = Assert.Single(stage.Reservoir.ActiveSlots);
        Assert.Equal(2, slot.Mode);
        Assert.Equal(0x00A40000, slot.X);
        Assert.Equal(0x01E00000, slot.Y);
        Assert.Equal(0x00080001, slot.Z);
        Assert.Equal(expectedKey + 1u, slot.DepthKey);
    }
}
