#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Cameras;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1, phase A (docs/plan-e20-heros.md section 1.31.5, the world tests (w1) to (w7); rule R-EL-4): the fire tome 2 in the real world of the map 14 (<see cref="FireTome2Run"/>), the Circle pressed at the tick 1 with the tome 48 given and equipped, the magic 4/4, the generator
/// at the seed of the oracle. The tome is cast by the three seams of h0a (the start of the tomes of h4a, <see cref="FireTome2Ai.Driver"/>, <see cref="FireTome2Ai.Cancel"/> when the item in use is <c>0x30</c>); E28 is registered on the registry of the runner when its key is absent.
/// The expected values are the FAITHFUL keys of the real code of the binary with the slot index words written (R-EL-1, <c>docs/plan-e20-annexe/e20-h4d1/critic/keys_faithful/</c>, replayed twice by T0). The key's bench does not run the step of the shake, the DLL does (R-EL-7): the
/// block is compared by its five words, and by its nine only where the step leaves them as they are. The interruptions are posed as the oracle's poke (R-EL-6: the animation 0x31 on the hero before the tick T): the item, its phase and its counter, the column, its eight effects and the
/// generator are compared at every tick; the hero up to T - 1 (the injury after T is E20.c2b's, not compared by this slice).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome2WorldTests : IDisposable
{
    public FireTome2WorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static void AssertEqual(List<string> wrong) => Assert.True(wrong.Count == 0, FireTome2Compare.Show(wrong));

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) the whole cast.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_TheFireTome2_EqualsTheKey_EveryTickEveryField_TheColumnBornAt34_ItsEightEffectsAt154_TheBufferRestoredAt155_TheItemFreedAt182()
    {
        var key = FireTome2Key.Load("fire2_cast_pf");
        using var run = new FireTome2Run("w1");

        run.Go(200);

        // The nine words of the block are compared where the step of the shake leaves them as the key has them: before the tick 34 and from the tick 155 (the block is the buffer, all zero, with the flag at 0).
        AssertEqual(FireTome2Compare.Everything(key, run, 200, 200, t => t < 34 || t >= 155));

        // The claims of the plan, read on the DLL next to the key.
        Assert.Equal(34, run.Columns.Keys.Min());
        Assert.Equal(154, run.Columns.Keys.Max()); // the column is recycled before the pilot of the tick 155.
        Assert.Equal(Enumerable.Range(34, 121), run.Columns.Keys.Order());
        Assert.Equal(Enumerable.Repeat((int)EntityStatus.Normal, 120), Enumerable.Range(34, 120).Select(t => run.Columns[t].Status)); // the status 2 from the end of the tick 34 to the tick 153.
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.Columns[154].Status);
        Assert.Equal(0, run.Columns[152].AnimationEnd);
        Assert.Equal(1, run.Columns[153].AnimationEnd); // the end of the animation (+0xAC) at S + 119.
        Assert.All(run.Columns.Values, c => Assert.Equal((6553600, 6553600, 1, 0, 0, 0), (c.PosX, c.PosY, c.PosZ, c.ForceX, c.ForceY, c.ForceZ))); // at the hero's X and Y, one raw unit above his Z, never moved.
        Assert.Equal(Enumerable.Repeat(154, 8), run.ColumnBlasts().Select(e => e.Tick));
        Assert.Equal(new[] { 6553600, 3421033, 3294976, 3333067, 6553600, 7846483, 10161664, 7978432 }, run.ColumnBlasts().Select(e => e.X));
        Assert.Equal(new[] { 9383424, 8641978, 6553600, 4406578, 5288960, 5691678, 6553600, 7503488 }, run.ColumnBlasts().Select(e => e.Y));
        Assert.All(run.ColumnBlasts(), e => Assert.Equal((19, 2, 2), (e.Effect, e.Animation, e.Z)));
        Assert.Equal(new[] { 1, 0, 2, 0, 2, 0, 0, 0, 0 }, run.Blocks[34]); // the block of the tick of the birth: 32 >> 4 and 32 >> 6.
        Assert.Equal(new int[9], run.Buffers[34]); // the buffer is the block as it was before: all zero.
        Assert.Equal(new int[9], run.Blocks[155]); // restored at the tick 155.
        Assert.Equal((52u, 2), (run.Hero[155].Animation, run.Hero[155].Phase)); // the animation 0x34 and the phase 2.
        Assert.Equal(0u, run.Hero[181].Animation);
        Assert.Equal(Tome, run.Hero[181].ItemInUse);
        Assert.Equal(0u, run.Hero[182].ItemInUse); // freed at the tick 182.
        Assert.Equal(182, Enumerable.Range(1, 200).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 200), t => Assert.Equal(3, run.Hero[t].Mp)); // 4 -> 3 at the start.
        Assert.Empty(run.Heard.Played); // no sound at all (the key's sound 0 is the silent id).
    }

    private const uint Tome = 0x30;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2) to (w5) the interruptions.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W2_AnInterruptionAtTheTick60_PutsTheColumnAtTheStatus3_ItsEightEffectsAndTheDestructionAtTheTickItself_TheBufferIsRestored(int ticksPerFrame)
    {
        var key = FireTome2Key.Load("fire2_hurt60");
        using var run = new FireTome2Run("w2-" + ticksPerFrame);

        run.Go(120, ticksPerFrame, 60);

        if (ticksPerFrame == 1)
        {
            AssertEqual(FireTome2Compare.Everything(key, run, 120, 59));
        }

        // The column dies at the tick of the interruption (the pilot cancels at the head of the entities, the column's own pass follows in the same tick), the item is freed, the block is the buffer.
        Assert.Equal(34, run.Columns.Keys.Min());
        Assert.Equal(60, run.Columns.Keys.Max());
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.Columns[60].Status);
        Assert.Equal(8, run.ColumnBlasts().Count);
        Assert.Equal(Tome, run.Hero[59].ItemInUse);
        Assert.Equal(0u, run.Hero[60].ItemInUse);
        Assert.Equal(1, run.Hero[60].Phase);
        Assert.Equal(new int[9], run.Blocks[60]);
        Assert.Equal(key.Ticks[60].Effects.Select(e => (e.X, e.Y, e.Z)), run.ColumnBlasts().Select(e => (e.X, e.Y, e.Z)));
        Assert.Equal(key.Ticks[60].Effects.Count, run.ColumnBlasts().Count);
        Assert.Equal(key.Ticks[60].Counter, run.Hero[60].Counter);
    }

    [Theory]
    [InlineData("fire2_hurt20", null, null)]
    [InlineData("fire2_hurt20_zerobuf", new[] { 1, 5, 6, 7, 8, 3, -2, 1, 1 }, null)]
    [InlineData("fire2_hurt20_stale", new[] { 1, 5, 6, 7, 8, 3, -2, 1, 1 }, new[] { 9, 9, 9, 9, 9, 9, 9, 9, 9 })]
    public void W3_AnInterruptionBeforeTheBirth_BornNothing_NoEffectNoDraw_TheBlockIsTheBufferWhateverItWas(string name, int[]? block, int[]? buffer)
    {
        var key = FireTome2Key.Load(name);
        using var run = new FireTome2Run("w3-" + name, null, block, buffer);

        run.Go(60, 1, 20);

        AssertEqual(FireTome2Compare.Everything(key, run, 60, 19));
        Assert.Empty(run.Columns);
        Assert.Empty(run.Effects);
        Assert.Equal(0u, run.Hero[20].ItemInUse);
    }

    [Fact]
    public void W4_ThePresetWithZeroLimits_GoesAndComesBack_TheBufferEqualsThePresetAtTheTick34_TheBlockEqualsItFromTheTick155_ReachIncluded()
    {
        var preset = new[] { 1, 0, 0, 7, 8, 0, 0, 1, 1 };
        var key = FireTome2Key.Load("fire2_cast_preset_limit0_pf");
        using var run = new FireTome2Run("w4", null, preset);

        run.Go(200);

        AssertEqual(FireTome2Compare.Everything(key, run, 200, 200, t => t < 34 || t >= 155));
        Assert.Equal(preset, run.Buffers[34]); // the words as they were, before the pilot wrote them.
        Assert.Equal(preset, run.Buffers[200]);
        Assert.All(Enumerable.Range(155, 46), t => Assert.Equal(preset, run.Blocks[t])); // the nine words, Reach included.
        Assert.Equal(new[] { 1, 0, 2, 0, 2, 0, 0, 1, 1 }, run.Blocks[34]);
    }

    [Fact]
    public void W4b_ThePresetBlock_TheBufferIsThePresetFromTheTick34_TheFiveWordsComeBack()
    {
        var preset = new[] { 1, 5, 6, 7, 8, 3, -2, 1, 0 };
        var key = FireTome2Key.Load("fire2_cast_preset_block");
        using var run = new FireTome2Run("w4b", null, preset);

        run.Go(200);

        AssertEqual(FireTome2Compare.Everything(key, run, 200, 200));
        Assert.All(Enumerable.Range(34, 167), t => Assert.Equal(preset, run.Buffers[t]));
        Assert.Equal(preset.Take(5), run.Blocks[155].Take(5));
    }

    [Fact]
    public void W5_AnInterruptionAtTheTick170_AfterTheBurning_TheBufferIsAlreadyRestored_NoColumn_NoEffect_TheItemIsFreed()
    {
        var key = FireTome2Key.Load("fire2_hurt170");
        using var run = new FireTome2Run("w5");

        run.Go(200, 1, 170);

        AssertEqual(FireTome2Compare.Everything(key, run, 200, 169));
        Assert.Equal(0u, run.Hero[170].ItemInUse);
        Assert.Equal(Tome, run.Hero[169].ItemInUse);
        Assert.Equal((2, 2), (run.Hero[169].Phase, run.Hero[170].Phase));
        Assert.Equal(8, run.ColumnBlasts().Count); // the eight of the tick 154 only.
        Assert.Equal(Enumerable.Repeat(154, 8), run.ColumnBlasts().Select(e => e.Tick));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w6) the targets.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("fire2_targets_pf")]
    [InlineData("fire2_targets_edge_pf")]
    public void W6_TheColumnBurnsWhatItCovers_EveryTargetIsHitAtTheTicksOfTheKey_EveryTwentyFiveTicks_TheHeroIsNeverHit_TheGeneratorFollows(string name)
    {
        var key = FireTome2Key.TargetKey.Load(name);
        using var run = new FireTome2Run("w6-" + name, key.Targets);

        run.Go(200);

        for (var i = 0; i < key.Targets.Count; i++)
        {
            var expected = key.Hits.TryGetValue(i, out var ticks) ? ticks : Array.Empty<int>();
            Assert.True(expected.SequenceEqual(run.HitTicks(i)), $"target {i} at ({key.Targets[i][0]}, {key.Targets[i][1]}): hit at [{string.Join(",", run.HitTicks(i))}] (key [{string.Join(",", expected)}])");
        }

        Assert.All(Enumerable.Range(1, 200), t => Assert.Equal(0, run.Hero[t].HitCounter)); // the hero is never touched.
        AssertEqual(FireTome2Compare.Generator(key.Generator.Select(g => (g.Key, g.Value.Rng0, g.Value.Rng1)), run, 200));
    }

    [Fact]
    public void W6b_TheBoxOfTheColumnGrows_ThePairsOfTheEdge_200Touched201Never_186Touched187Never()
    {
        var key = FireTome2Key.TargetKey.Load("fire2_targets_edge_pf");
        using var run = new FireTome2Run("w6b", key.Targets);

        run.Go(150);

        Assert.Equal(new[] { 87, 112, 137 }, run.HitTicks(0)); // (200, 100)
        Assert.Empty(run.HitTicks(1)); // (201, 100)
        Assert.Equal(new[] { 87, 112, 137 }, run.HitTicks(2)); // (100, 186)
        Assert.Empty(run.HitTicks(3)); // (100, 187)
        // The box grows from 30 x 18 pixels at the birth to 178 x 158 by the image 18, then stays.
        Assert.InRange(run.Columns[34].Width, (30 << 16) - 2, (30 << 16) + 2); // 30 x 18 pixels, to the unit the box is evaluated with.
        Assert.InRange(run.Columns[34].Depth, (18 << 16) - 2, (18 << 16) + 2);
        Assert.Equal((10, 128), (run.Columns[34].Attribute, run.Columns[34].Power));
        Assert.InRange(run.Columns[100].Width, (178 << 16) - 2, (178 << 16) + 2);
        Assert.InRange(run.Columns[100].Depth, (158 << 16) - 2, (158 << 16) + 2);
        Assert.Equal(run.Columns[100].Width, run.Columns[150].Width);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w7) the cadences.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void W7_AtTwoAndThreeTicksAFrame_TheColumnIsBornAt34_ItsAnimationEndsAt153_ItDiesAt154_TheItemIsFreedAt182_EightEffectsAndEightDraws(int ticksPerFrame)
    {
        using var run = new FireTome2Run("w7-" + ticksPerFrame);

        run.Go(200, ticksPerFrame);

        Assert.Equal(34, run.Columns.Keys.Min());
        Assert.Equal(154, run.Columns.Keys.Max());
        Assert.Equal(1, run.Columns[153].AnimationEnd);
        Assert.Equal(0, run.Columns[152].AnimationEnd);
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.Columns[154].Status);
        Assert.Equal(182, Enumerable.Range(1, 200).First(t => run.Hero[t].ItemInUse == 0));
        Assert.Equal(8, run.ColumnBlasts().Count);
        Assert.Equal(200 + 8, run.FrameSteps.Values.Sum()); // a step at the end of each tick and the eight draws of E28.
        Assert.Equal(new[] { 1, 0, 2, 0, 2, 0, 0, 0, 0 }, run.Blocks[34]);
        Assert.Equal(new int[9], run.Blocks[155]);
        Assert.Equal((52u, 2), (run.Hero[155].Animation, run.Hero[155].Phase));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void W7b_AnInterruptionAtTheTick60_AtTwoAndThreeTicksAFrame_ThePhaseThePointerAndTheItemAreThoseOfOneTickAFrame(int ticksPerFrame)
    {
        using var run = new FireTome2Run("w7b-" + ticksPerFrame);

        run.Go(120, ticksPerFrame, 60);

        Assert.Equal(60, run.Columns.Keys.Max());
        Assert.Equal((int)EntityStatus.FlagToDestroy, run.Columns[60].Status);
        Assert.Equal(0u, run.Hero[60].ItemInUse);
        Assert.Equal(8, run.ColumnBlasts().Count);
        Assert.Equal(new int[9], run.Blocks[60]);
    }
}
