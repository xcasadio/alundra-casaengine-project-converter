#nullable enable
using System;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.e0b1 T2 (docs/plan-e20-heros.md section 1.17.1, annex docs/plan-e20-annexe/e20-e0b/README.md): the shared random stream advances once per logic tick, at the end of the tick, as the binary's frame function does
/// (<c>0x8002BD0C-0x8002BD34</c>). This file holds the tests that pin the WIRING of the callback and are green from phase A on: <see cref="AlundraEffectStage.RunFrame"/> calls the optional callback exactly once per tick,
/// after the update of the reservoir, on a frozen tick too, never for a frame of 0 tick (b), and a direct call without callback advances nothing (e). The tests of the advance itself (the six words, the world, the order in the tick)
/// are in the other part of the class (<c>AlundraRandomPerTickTests.Advance.cs</c>), written red before phase B.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed partial class AlundraRandomPerTickTests : IDisposable
{
    public AlundraRandomPerTickTests()
    {
        AlundraRandom.Reset();
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        AlundraRandom.Reset();
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    /// <summary>One step of the stream, written here on purpose (never the production expression): <c>s * 0x7D2B89DD + 0xE06A02E7 (mod 2^32)</c>.</summary>
    private static uint Step(uint s) => unchecked((s * 0x7D2B89DDu) + 0xE06A02E7u);

    private static uint StepN(uint s, int n)
    {
        for (var i = 0; i < n; i++)
        {
            s = Step(s);
        }

        return s;
    }

    private static uint Word => (uint)AlundraRandom.RandomSeed;

    /// <summary>A stage holding one effect that alternates between the image sets 0 and 1 at each update of the reservoir (-1 before the first update): the set tells how many updates the callback has seen.</summary>
    private static AlundraEffectStage StageWithOneAlternatingEffect(out AlundraEffectSlot slot)
    {
        var stage = new AlundraEffectStage();
        stage.Load(EffectGolden.Synthetic(new[] { EffectGolden.Anim("Loop", new[] { 1, 0 }, new[] { 1, 1 }) }, 2, Array.Empty<AlundraEffectRecord>()));
        stage.CaptureHeroTile(0, 0);
        slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;
        return stage;
    }

    /// <summary>
    /// (b) D-E0B-1: the callback is called exactly <c>ticks</c> times (0, 1, 2, 4), AFTER the update of the reservoir of its tick (the image set it sees is the one the update just left), also on a frozen tick (the reservoir did not
    /// update: the set is unchanged) and never for 0 tick.
    /// </summary>
    [Fact]
    public void RunFrame_CallsTheCallbackOncePerTick_AfterTheReservoirTick_AlsoOnAFrozenTick_AndNeverForZeroTicks()
    {
        var cases = new (bool[] Frozen, int[] SetsSeen)[]
        {
            (Array.Empty<bool>(), Array.Empty<int>()),
            (new[] { false }, new[] { 0 }),
            (new[] { true }, new[] { -1 }),
            (new[] { false, false }, new[] { 0, 1 }),
            (new[] { false, true, false, false }, new[] { 0, 0, 1, 0 }),
        };

        foreach (var (frozen, expected) in cases)
        {
            var stage = StageWithOneAlternatingEffect(out var slot);
            var seen = new System.Collections.Generic.List<int>();

            stage.RunFrame(frozen.Length, frozen, () => seen.Add(slot.CurrentImageSetIndex));

            Assert.Equal(frozen.Length, seen.Count);
            Assert.Equal(expected, seen);
        }
    }

    /// <summary>(b) the callback comes after the operations queued for the tick as well as after the update: a creation queued for tick 1 exists, updated once, when the callback of tick 1 runs.</summary>
    [Fact]
    public void RunFrame_CallsTheCallbackAfterTheQueuedOperationsAndTheUpdateOfTheirTick()
    {
        var stage = StageWithOneAlternatingEffect(out _);
        AlundraEffectSlot? created = null;
        stage.Queue(1, 0, r => created = r.Create(true, 0, 0, 1, 2, 3));
        var seenAtTick = new System.Collections.Generic.List<int?>();

        stage.RunFrame(2, new[] { false, false }, () => seenAtTick.Add(created?.CurrentImageSetIndex));

        Assert.Equal(new int?[] { null, 0 }, seenAtTick); // not yet created at the callback of tick 0; created and updated once at the callback of tick 1.
    }

    /// <summary>(e) A direct call of the stage without callback (the 70 calls of the stage tests) advances nothing and calls nothing: the real state of the stream is untouched, with or without ticks, frozen or not.</summary>
    [Fact]
    public void RunFrame_WithoutACallback_AdvancesNothing()
    {
        const uint seed = 0x12345678;
        var stage = StageWithOneAlternatingEffect(out var slot);
        AlundraRandom.RandomSeed = seed;

        stage.RunFrame(4, new[] { false, true, false, false });
        stage.RunFrame(2, new[] { true, true }, null);
        stage.RunFrame(0, Array.Empty<bool>());

        Assert.Equal(seed, Word);
        Assert.Equal(0, slot.CurrentImageSetIndex); // the stage did run its ticks, three of them updating (-1 -> 0 -> 1 -> 0): only the callback was absent.
    }
}
