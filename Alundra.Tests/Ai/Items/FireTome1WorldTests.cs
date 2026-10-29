#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1 bis, phase A2 (docs/plan-e20-heros.md section 1.31.6, the world tests (w1) to (w11); rule R-EL-4): the fire tome 1 in the real world of the map 14 (<see cref="FireTome1Run"/>), the Circle pressed at the tick 1 with the tome 47 given and equipped, the magic 4/4, the generator at the
/// seed of the oracle, the blocks of the sprite 25 (class A, 5000 hit points) at the first free cells. The tome is cast by the three seams of h0a (the start of the tomes of h4a, <see cref="FireTome1Ai.Drive"/>, <see cref="FireTome1Ai.Cancel"/> when the item in use is <c>0x2F</c>); C63 and E3 are
/// registered on the registry of the runner when their key is absent. The expected values are the keys of the real code of the binary with the slot index words written, the generator stepped after each frame (R-EL-1, R-EL-2) and the shortest-path turn NOT applied (the original's, D-E20-57):
/// the keys <c>_pf</c> of <c>disc/keys/</c> for the production settings of the query where the cast is the same (D-H4D2-7: <c>tgt_one</c>, <c>tgt_three</c>, <c>tgt_vanish75</c>, <c>col_hit</c> and the bare cast give exactly the same rows with the three words of the query modified), the keys <c>_qfix</c> where
/// the bound and the sort change the target (<c>tgt_two_wrong_order</c>, <c>tgt_topslot</c>). Every column of the plan is compared at every tick, with no tolerance but R-EL-5 for the walls and R-EL-6 for the interruptions.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome1WorldTests : IDisposable
{
    public FireTome1WorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static void AssertEqual(List<string> wrong) => Assert.True(wrong.Count == 0, FireTome1Compare.Show(wrong));

    private static Func<int, int, CutBench.Cell> Columns(int first, int last, CutBench.Cell cell) => (column, _) => column >= first && column <= last ? cell : default;

    private static readonly int[] Decoy = { 1100, 900 };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) the whole cast, two seeds.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_TheFireTome1_EqualsTheKey_EveryTickEveryColumn_TheColumnAndTheFirstBallAt34_ElevenBallsEveryEightTicks_TheItemFreedAt149()
    {
        var key = FireTome1Key.Load("fire1_cast_pf");
        using var run = new FireTome1Run("w1");

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200));

        // The claims of the plan, read on the DLL next to the key.
        var columns = run.Lives(FireTome1Run.Column);
        var balls = run.Lives(FireTome1Run.Ball);
        Assert.Single(columns);
        Assert.Equal(34, columns[0][0].Tick);
        Assert.Equal(11, balls.Count);
        Assert.Equal(Enumerable.Range(0, 11).Select(k => 34 + 8 * k), balls.Select(b => b[0].Tick));
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 2, 10, 11 }, balls.Select(b => b[0].Slot));
        Assert.Equal(149, Enumerable.Range(1, 200).First(t => run.Hero[t].ItemInUse == 0)); // the object freed at the tick 149.
        Assert.Equal(122, Enumerable.Range(1, 200).First(t => run.Hero[t].AnimationAtEnd == 52 || run.Hero[t].TargetAnimation == 52)); // the animation 0x34 at the tick the column is recycled.
        Assert.Empty(run.Heard.Played); // no sound at all.
    }

    [Fact]
    public void W1_TheFireTome1_WithTheOtherSeed_EqualsItsKey_EveryTickEveryColumn_TheDirectionsAreOtherOnes()
    {
        var key = FireTome1Key.Load("fire1_cast_pf_seedB");
        using var run = new FireTome1Run("w1b");

        run.Go(200, seed: 0xA5A5A5A5);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200));
        Assert.Equal(11, run.Lives(FireTome1Run.Ball).Count);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2), (w3), (w3c) the targets: the query at the settings of production and of the binary.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static FireTome1Run Scenario(string name, bool binary)
    {
        return name switch
        {
            "tgt_one" => new FireTome1Run(name, new List<int[]> { new[] { 140, 100 } }, binary: binary),
            "tgt_three" => new FireTome1Run(name, new List<int[]> { new[] { 140, 100 }, new[] { 100, 140 }, new[] { 180, 100 } }, binary: binary),
            "tgt_vanish75" => new FireTome1Run(name, new List<int[]> { new[] { 140, 100 } }, binary: binary),
            "col_hit" => new FireTome1Run(name, new List<int[]> { new[] { 108, 100 } }, binary: binary),
            "tgt_two_wrong_order" => new FireTome1Run(name, new List<int[]> { new[] { 180, 100 }, new[] { 130, 100 } }, binary: binary),
            "tgt_topslot" => new FireTome1Run(name, Enumerable.Repeat(Decoy, 11).Append(new[] { 140, 100 }).ToList(), binary: binary),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static void Go(FireTome1Run run, string name)
    {
        switch (name)
        {
            case "tgt_vanish75":
                run.Go(200, remove: new Dictionary<int, int[]> { [75] = new[] { 0 } });
                break;
            case "tgt_topslot":
                run.Go(200, remove: new Dictionary<int, int[]> { [1] = Enumerable.Range(0, 11).ToArray() });
                break;
            case "col_hit":
                run.Go(130);
                break;
            default:
                run.Go(200);
                break;
        }
    }

    [Theory]
    [InlineData("tgt_one", false)]
    [InlineData("tgt_one", true)]
    [InlineData("tgt_three", false)]
    [InlineData("tgt_three", true)]
    [InlineData("tgt_vanish75", false)]
    [InlineData("tgt_vanish75", true)]
    [InlineData("col_hit", false)]
    [InlineData("col_hit", true)]
    public void W2_TheTargetScenarios_AtTheSettingsOfProductionAndOfTheBinary_EqualTheirKey_EveryTickEveryColumn_TheBallsTurnByTheNumericDirections(string name, bool binary)
    {
        var key = FireTome1Key.Load("fire1_" + name + "_pf");
        using var run = new FireTome1Run("w2-" + name + (binary ? "-binary" : "-production"), binary: binary, blocks: ScenarioBlocks(name));

        Go(run, name);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, key.LastTick));
    }

    private static List<int[]> ScenarioBlocks(string name) => name switch
    {
        "tgt_one" or "tgt_vanish75" => new List<int[]> { new[] { 140, 100 } },
        "tgt_three" => new List<int[]> { new[] { 140, 100 }, new[] { 100, 140 }, new[] { 180, 100 } },
        "col_hit" => new List<int[]> { new[] { 108, 100 } },
        "tgt_two_wrong_order" => new List<int[]> { new[] { 180, 100 }, new[] { 130, 100 } },
        "tgt_topslot" => Enumerable.Repeat(Decoy, 11).Append(new[] { 140, 100 }).ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    [Theory]
    [InlineData("tgt_two_wrong_order")]
    [InlineData("tgt_topslot")]
    public void W3_TheSortAndTheBound_InTheBinaryMode_EqualTheirKey_TheNearestIsNotAlwaysTheTarget_TheTopSlotIsNotAcquired(string name)
    {
        var key = FireTome1Key.Load("fire1_" + name + "_pf");
        using var run = new FireTome1Run("w3-" + name, binary: true, blocks: ScenarioBlocks(name));

        Go(run, name);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, key.LastTick));
    }

    [Theory]
    [InlineData("tgt_two_wrong_order")]
    [InlineData("tgt_topslot")]
    public void W3c_TheSortAndTheBound_AtTheSettingsOfProduction_EqualTheirQfixKey(string name)
    {
        var key = FireTome1Key.Load("fire1_" + name + "_qfix_pf", "disc/keys");
        using var run = new FireTome1Run("w3c-" + name, binary: false, blocks: ScenarioBlocks(name));

        Go(run, name);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, key.LastTick));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w4) the wall, (w5) the water, (w7) the table.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W4_AWallInTheColumn6_StopsTheBallsInTheAir_ByRElFive()
    {
        var key = FireTome1Key.Load("fire1_wall_col6_pf");
        using var run = new FireTome1Run("w4", cell: Columns(6, 6, new CutBench.Cell(0x40, 0)));

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200, wall: true));

        // The statuses, the forces, the animations and the ticks are exact (the check above has no tolerance on them); the balls that met the wall are in the sample, and the DLL slides past the key's frozen ball on the free axis.
        var stopped = run.Lives(FireTome1Run.Ball).Where(life => life.Any(r => r.PosX is >= 8978431 and <= 8978433)).ToList();
        Assert.Equal(3, stopped.Count);
    }

    [Fact]
    public void W5_DeepWaterInTheColumns6To12_ChangesNothing_TheKeyEqualsTheBareCast_AndTheDllEqualsBoth()
    {
        var water = FireTome1Key.Load("fire1_water_cols6_12_pf");
        var cast = FireTome1Key.Load("fire1_cast_pf");
        using var run = new FireTome1Run("w5", cell: Columns(6, 12, new CutBench.Cell(0, 0, null, 0x08)));

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(water, run, 1, 200));
        AssertEqual(FireTome1Compare.Entities(cast, run, 1, 200));
        Assert.All(run.Entities.Values.SelectMany(e => e.Values).Where(e => e.SpriteType == FireTome1Run.Ball), e => Assert.Equal(0, e.Slope)); // the ball has no gravity: the deep water does not destroy it.
    }

    [Theory]
    [InlineData("table_full", 63, 80)]
    [InlineData("table_one_free", 62, 200)]
    public void W7_TheTableOfTheEntities_FullOrWithOneCellFree_EqualsTheKey(string name, int decoys, int last)
    {
        var key = FireTome1Key.Load("fire1_" + name + "_pf");
        using var run = new FireTome1Run("w7-" + name, Enumerable.Repeat(Decoy, decoys).ToList());

        run.Go(last);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, last));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w6) the ten interruptions, R-EL-6 revised.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(121)]
    [InlineData(122)]
    [InlineData(124)]
    [InlineData(140)]
    public void W6_AnInterruptionAtTheTickT_EqualsTheKey_TheColumnDestroyedWithItsEffect_TheBallsDeactivated_TheItemFreed_TheHeroOfTheKeyAfterT(int t)
    {
        var key = FireTome1Key.Load("fire1_hurt" + t + "_pf");
        using var run = new FireTome1Run("w6-" + t);

        run.Go(t + 75, interruptAt: t);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, t + 75, heroAfterToo: true));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w8) two and three ticks a frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W8_TheCastAt2And3TicksAFrame_HasTheTicksOfTheBirths_TheEndOfTheColumn_TheRelease_TheNumberOfBalls_AndTheNumberOfDraws(int ticksPerFrame)
    {
        var key = FireTome1Key.Load("fire1_cast_pf");
        using var run = new FireTome1Run("w8-" + ticksPerFrame);

        run.Go(200, ticksPerFrame);

        var columns = run.Lives(FireTome1Run.Column);
        var balls = run.Lives(FireTome1Run.Ball);
        Assert.Single(columns);
        Assert.Equal(34, columns[0][0].Tick);
        Assert.Equal(11, balls.Count);
        Assert.Equal(Enumerable.Range(0, 11).Select(k => 34 + 8 * k), balls.Select(b => b[0].Tick));
        var columnEnd = columns[0].First(r => r.AnimationEnd == 1).Tick;
        Assert.Equal(34 + 86, columnEnd); // +0xAC of the column at S + 86.
        var freed = Enumerable.Range(1, run.LastTick).First(t => run.Hero[t].ItemInUse == 0);
        Assert.Equal(149, freed);

        // The draws: the generator steps once at the end of EVERY tick (200 steps over the 200 ticks, whatever the cadence of the frames); what is left of the steps of the frames are the draws of the cast. The key has 115 of them at one tick a frame.
        var draws = Enumerable.Range(0, (run.LastTick + ticksPerFrame - 1) / ticksPerFrame).Sum(f => run.FrameSteps[Math.Min((f + 1) * ticksPerFrame, run.LastTick)]) - run.LastTick;
        Assert.Equal(115, key.Rows.Sum(r => r.Draws));
        var derived = ExpectedDraws(run);
        var airStops = run.Lives(FireTome1Run.Ball).Count(life => life.FirstOrDefault(r => r.Status == EntityStatus.Deactivated) is { Ground: 0 });
        if (ticksPerFrame == 1)
        {
            Assert.Equal(115, draws);
            Assert.Equal(derived, draws);
        }
        else
        {
            // The draws of the waits depend on the lifetimes of the balls, which the directions (the cadence changes the state of the generator at each birth) and the stops in the air by the edge of the map change; a ball stopped in the air by the edge may miss the draw of its last tick (R-EL-5: the flag of a stop is seen a tick early).
            Assert.InRange(draws, 11, derived);
            Assert.True(derived - draws <= airStops, $"derived {derived}, measured {draws}, balls stopped in the air {airStops}");
        }
    }

    /// <summary>
    /// The draws a run of the bare cast must have made, derived from its own rows: one per ball (the direction, drawn by the pilot), and one per tick of each ball at the status 2 whose wait counter was 0 at the end of the tick before and that had no target (C63 asks the query, which finds nobody, and draws the wait below 9).
    /// </summary>
    private static int ExpectedDraws(FireTome1Run run)
    {
        var count = 0;
        foreach (var life in run.Lives(FireTome1Run.Ball))
        {
            count++;
            for (var i = 1; i < life.Count; i++)
            {
                if (life[i - 1].Status == EntityStatus.Normal && life[i - 1].Wait == 0 && life[i - 1].TargetSlot == 0 && life[i].Tick == life[i - 1].Tick + 1)
                {
                    count++;
                }
            }
        }

        return count;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w9) to (w11) the relief.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Plateau(string name, int height)
    {
        var key = FireTome1Key.Load(name, "critic/keys_proposed");
        using var run = new FireTome1Run("w9-" + height, cell: Columns(6, 12, new CutBench.Cell(0, height)));

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200));
        var balls = run.Lives(FireTome1Run.Ball);
        Assert.Equal(11, balls.Count);
    }

    [Fact]
    public void W9_ThePlateauOf16PixelsInTheColumns6To12_EqualsTheKey_TheBallsLandOnItAt16PixelsAndOffItAtZero() => Plateau("fire1_relief_plateau_hb1_cols6_12_pf", 1);

    [Fact]
    public void W10_ThePlateauOf32PixelsInTheColumns6To12_EqualsTheKey_TheBallsLandOnItAt32PixelsAndOffItAtZero() => Plateau("fire1_relief_plateau_hb2_cols6_12_pf", 2);

    [Fact]
    public void W11_TheHeroOnAPlateauOf32Pixels_EqualsTheKey()
    {
        var key = FireTome1Key.Load("fire1_relief_hero_on_plateau_hb2_pf", "critic/keys_proposed");
        using var run = new FireTome1Run("w11", cell: (column, row) => column <= 5 && row >= 2 && row <= 6 ? new CutBench.Cell(0, 2) : default, heroZ: 32);

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200));
    }
}
