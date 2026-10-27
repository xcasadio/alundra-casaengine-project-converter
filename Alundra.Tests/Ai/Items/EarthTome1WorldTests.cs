#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T1, phase A (docs/plan-e20-heros.md section 1.31.3, the world tests (w1) to (w6) and (c1); rule R-EL-4): the earth tome 1 in the real world of the map 14 (<see cref="EarthTome1Run"/>), the Circle pressed at the tick 1 with the tome 43 given and equipped, the
/// magic 4/4, the generator at the seed of the oracle, one tick per frame. The tome is cast by the three seams of h0a (the start of the tomes of h4a, <see cref="EarthTome1Ai.Driver"/>, <see cref="EarthTome1Ai.Cancel"/>); E3 is registered on the registry of the runner when its
/// key is absent. The expected values are the keys of the real code of the binary with the slot index words written (R-EL-1, <c>docs/plan-e20-annexe/e20-h4c1/disc/keys/</c>, the relief keys of the critic): the tests are green in phase A and the production tests (p) of the phase B
/// are the same keys through the entries of the object. The animation row and the current animation of a rock are compared from the tick after its birth (D-H4C1-3); every other field, every tick, without tolerance, except against an obstacle (w3, R-EL-5).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome1WorldTests : IDisposable
{
    private const int Sfx46 = 46;
    private const int HeroHurtSfx = 23;

    public EarthTome1WorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static Func<int, int, CutBench.Cell> Columns(int first, int last, CutBench.Cell cell) => (c, _) => c >= first && c <= last ? cell : default;

    private static Func<int, int, CutBench.Cell>? Field(string kind) => kind switch
    {
        "flat" => null,
        "water" => Columns(7, 9, new CutBench.Cell(0, 0, null, 0x08)),
        "wall9" => Columns(9, 9, new CutBench.Cell(0x40, 0)),
        "wall5" => Columns(5, 5, new CutBench.Cell(0x40, 0)),
        "slope" => Columns(7, 9, new CutBench.Cell(0, 0, null, 0x02)),
        "step16" => Columns(7, 9, new CutBench.Cell(0, 1)),
        "step32" => Columns(7, 9, new CutBench.Cell(0, 2)),
        _ => throw new ArgumentException(kind),
    };

    /// <summary>Everything the key holds, tick by tick, without tolerance: the rocks, the item and the hero, the sounds, the draws and the effects.</summary>
    private static List<string> Everything(EarthTome1Key key, EarthTome1Run run, int last, bool hero = true, params int[] ignoredSounds)
    {
        var all = new List<string>();
        all.AddRange(EarthTome1Compare.Rocks(key, run, 1, last));
        all.AddRange(EarthTome1Compare.Item(key, run, 1, last, hero));
        all.AddRange(EarthTome1Compare.Sounds(key, run, 1, last, ignoredSounds));
        all.AddRange(EarthTome1Compare.DrawsAndEffects(key, run, 1, last));
        return all;
    }

    /// <summary>The lives of the rocks the DLL run saw: (slot, tick of birth, first tick of the status 3 or 0, last tick), in the order of the births.</summary>
    private static List<(int Slot, int Birth, int Status3, int Last)> Lives(EarthTome1Run run, int last)
    {
        var lives = new List<(int, int, int, int)>();
        for (var tick = 1; tick <= last; tick++)
        {
            foreach (var slot in run.Rocks[tick].Keys.OrderBy(k => k))
            {
                var born = tick == 1 || !run.Rocks[tick - 1].TryGetValue(slot, out var before) || before.Status == 4;
                if (!born)
                {
                    continue;
                }

                var end = tick;
                var status3 = 0;
                while (end <= last && run.Rocks[end].TryGetValue(slot, out var row))
                {
                    status3 = status3 == 0 && row.Status == 3 ? end : status3;
                    if (row.Status == 4)
                    {
                        break;
                    }

                    end++;
                }

                lives.Add((slot, tick, status3, Math.Min(end, last)));
            }
        }

        return lives.OrderBy(l => l.Item2).ThenBy(l => l.Item1).ToList();
    }

    private static List<int> SoundTicks(EarthTome1Run run, int sfx) => run.Heard.Played.Where(p => p.Sfx == sfx).Select(p => p.Tick).Order().ToList();

    /// <summary>The ticks at which a rock of the key is first at the status 3.</summary>
    private static List<(int Tick, int Slot)> KeyStatus3(EarthTome1Key key, int last)
        => Enumerable.Range(1, last).SelectMany(t => key.At(t).Rocks.Where(r => r.Value.Status == 3 && (t == 1 || !key.At(t - 1).Rocks.TryGetValue(r.Key, out var before) || before.Status != 3)).Select(r => (Tick: t, Slot: r.Key))).ToList();

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) the flat field, towards the east: the whole key without tolerance.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_TheEarthTome1OnTheFlatFieldToTheEast_EqualsTheKey_EveryTickEveryField_ThirtyTwoRocksTwoEveryFiveTicks_TheItemFreedAtTheTick129()
    {
        var key = EarthTome1Key.Load("earth1_flat_east");
        using var run = new EarthTome1Run("w1", 24, 100, null);

        run.Go(140);

        var wrong = Everything(key, run, 140);
        Assert.True(wrong.Count == 0, EarthTome1Compare.Show(wrong));

        // The claims of the plan, read on the DLL next to the key: the births, the sounds 46 at the tick of E3, the item, no step at E3, no draw and no effect.
        var lives = Lives(run, 140);
        Assert.Equal(32, lives.Count);
        Assert.Equal(EarthTome1Ai.Table.Select(e => e.Tick + 2).Order(), lives.Select(l => l.Birth).Order()); // the driver is first called the tick after the start: the counter 20 is the tick 22, the last birth the tick 97.
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3, 4, 13, 14, 5, 6 }, lives.Select(l => l.Slot)); // the slots of the births: the cases of first use (1 to 14) act at their birth tick.
        Assert.All(lives, l => Assert.NotEqual(0, l.Status3));
        Assert.Equal(lives.Select(l => l.Status3).Order(), SoundTicks(run, Sfx46)); // one sound 46 per rock, at the tick of E3.
        Assert.Equal(32, run.Heard.Played.Count(p => p.Sfx == Sfx46));
        foreach (var life in lives)
        {
            var landed = run.Rocks[life.Status3 - 1][life.Slot];
            var impact = run.Rocks[life.Status3][life.Slot];
            Assert.True((landed.PosX, landed.PosY) == (impact.PosX, impact.PosY), $"slot {life.Slot} born at {life.Birth}: a step at the tick of E3"); // P1: f5 (D-F5-5).
            Assert.Equal(life.Status3 + 13, life.Last); // the death at E3 + 13.
        }

        Assert.Equal(0x2Bu, run.Hero[128].ItemInUse);
        Assert.Equal(0u, run.Hero[129].ItemInUse);
        Assert.Equal(129, Enumerable.Range(1, 140).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 140), t => Assert.Equal(1, run.FrameSteps[t])); // no draw but the step of the end of the frame.
        Assert.All(Enumerable.Range(1, 140), t => Assert.Empty(run.Effects[t]));
        Assert.All(Enumerable.Range(1, 140), t => Assert.Empty(run.Sparks[t]));
    }

    [Fact]
    public void W1_TheRocksOfTheWestEdge_ThreeOfThirtyTwo_AreAtTheStatus3_ThreeOrFourTicksBeforeTheirLanding_AtTheTicks94_103_And109()
    {
        // P5: the rocks born at 77, 87 and 92 in the slots 12, 3 and 14 (directions 6, 8, 10) reach the west edge of the map (X 0) and are at the status 3 at 94, 103 and 109, as the key.
        var key = EarthTome1Key.Load("earth1_flat_east");
        using var run = new EarthTome1Run("w1-edge", 24, 100, null);

        run.Go(140);

        var lives = Lives(run, 140);
        var edge = lives.Where(l => (l.Birth, l.Slot) is (77, 12) or (87, 3) or (92, 14)).ToList();
        Assert.Equal(3, edge.Count);
        Assert.Equal(new[] { 94, 103, 109 }, edge.Select(l => l.Status3).Order());
        Assert.Equal(new[] { 94, 103, 109 }, KeyStatus3(key, 140).Where(k => edge.Any(l => l.Slot == k.Slot && k.Tick > l.Birth && k.Tick <= l.Birth + 20)).Select(k => k.Tick).Order());
        Assert.Equal(new[] { 17, 16, 17 }, edge.OrderBy(l => l.Birth).Select(l => l.Status3 - l.Birth)); // 3 or 4 ticks before the 20 of the normal landing of those directions.
        Assert.All(lives.Where(l => !edge.Contains(l)), l => Assert.Contains(l.Status3 - l.Birth, new[] { 16, 17, 20 }));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2) the three other orientations and the hero at X 200.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("earth1_flat_south", 0u, 100)]
    [InlineData("earth1_flat_west", 8u, 100)]
    [InlineData("earth1_flat_north", 16u, 100)]
    [InlineData("earth1_flat_east_x200", 24u, 200)]
    public void W2_TheOtherOrientationsAndTheHeroAtX200_EqualTheirKeys_EveryTickEveryField(string name, uint direction, int heroX)
    {
        var key = EarthTome1Key.Load(name);
        using var run = new EarthTome1Run("w2-" + name, direction, heroX, null);

        run.Go(140);

        var wrong = Everything(key, run, 140);
        Assert.True(wrong.Count == 0, name + " " + EarthTome1Compare.Show(wrong));
        var lives = Lives(run, 140);
        Assert.Equal(32, lives.Count);
        Assert.Equal(EarthTome1Ai.Table.Select(e => e.Tick + 2).Order(), lives.Select(l => l.Birth).Order());
        Assert.Equal(lives.Select(l => l.Status3).Order(), SoundTicks(run, Sfx46)); // every rock reaches the status 3 and plays its sound 46.
        Assert.Equal(129, Enumerable.Range(1, 140).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 140), t => Assert.Equal(1, run.FrameSteps[t]));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w3) the wall and the relief (R-EL-5): the keys of the discovery and of the critic.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("earth1_wall_col9", "disc/keys", "wall9", 140)]
    [InlineData("crit_pointblank_wall_col5", "critic/work/fcopy/_crit_out", "wall5", 140)]
    [InlineData("crit_step16_cols7_9", "critic/work/fcopy/_crit_out", "step16", 140)]
    [InlineData("crit_step32_cols7_9", "critic/work/fcopy/_crit_out", "step32", 140)]
    public void W3_TheWallAndTheSteps_FollowTheKeyWithTheStatus3AtMostOneTickEarly_TheChronologyReadFromTheObservedStatus3(string name, string folder, string field, int last)
    {
        var key = EarthTome1Key.Load(name, folder);
        using var run = new EarthTome1Run("w3-" + name, 24, 100, Field(field));

        run.Go(last);

        var wrong = EarthTome1Compare.Obstacle(key, run, 1, last, maxLead: 1);
        wrong.AddRange(EarthTome1Compare.Item(key, run, 1, last));
        Assert.True(wrong.Count == 0, name + " " + EarthTome1Compare.Show(wrong));

        // The item and the births are those of the key (the cast does not depend on the field), the sounds 46 are at the statuses 3 of each side, and each rock of the DLL is at the status 3 at the key's tick or one earlier.
        var lives = Lives(run, last);
        Assert.Equal(32, lives.Count);
        Assert.Equal(lives.Select(l => l.Status3).Order(), SoundTicks(run, Sfx46));
        var keyS3 = KeyStatus3(key, last).Select(k => k.Tick).Order().ToList();
        Assert.Equal(keyS3, Enumerable.Range(1, last).SelectMany(t => key.At(t).Sounds.Where(s => s == Sfx46).Select(_ => t)).Order().ToList());
        Assert.Equal(32, keyS3.Count);
        Assert.All(keyS3.Zip(lives.Select(l => l.Status3).Order(), (k, d) => k - d), lead => Assert.InRange(lead, 0, 1));
    }

    [Fact]
    public void W3_ThePointBlankWall_HasTheStatus3OneTickEarlyForTenLivesOfThirtyTwo_AndNoneLate()
    {
        // The window of R-EL-5 is used by the point-blank wall: ten of the lives are at the status 3 one tick before the key's, none is late.
        var key = EarthTome1Key.Load("crit_pointblank_wall_col5", "critic/work/fcopy/_crit_out");
        using var run = new EarthTome1Run("w3-window", 24, 100, Field("wall5"));

        run.Go(140);

        var leads = Lives(run, 140).Select(l => KeyStatus3(key, 140).Single(k => k.Slot == l.Slot && k.Tick >= l.Status3 && k.Tick <= l.Status3 + 1).Tick - l.Status3).ToList();
        Assert.Equal(32, leads.Count);
        Assert.Equal(10, leads.Count(lead => lead == 1));
        Assert.Equal(22, leads.Count(lead => lead == 0));
    }

    [Theory]
    [InlineData("crit_flat", "critic/work/fcopy/_crit_out", "flat")]
    [InlineData("crit_slope_gp2_cols7_9", "critic/work/fcopy/_crit_out", "slope")]
    public void W3_TheFlatKeyOfTheCriticAndTheBandOfSlope1_EqualTheirKeys_EveryTickEveryField(string name, string folder, string field)
    {
        var key = EarthTome1Key.Load(name, folder);
        using var run = new EarthTome1Run("w3-" + name, 24, 100, Field(field));

        run.Go(140);

        var wrong = Everything(key, run, 140);
        Assert.True(wrong.Count == 0, name + " " + EarthTome1Compare.Show(wrong));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w4) the deep water: the death at the landing + 1 with the effect 6, no E3, no sound 46.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W4_TheDeepWater_KillsTheRocksThatLandInIt_AtTheLandingPlus1_WithTheEffect6_WithoutE3_WithoutTheSound46()
    {
        var key = EarthTome1Key.Load("earth1_deepwater_cols7_9");
        using var run = new EarthTome1Run("w4", 24, 100, Field("water"));

        run.Go(140);

        var wrong = Everything(key, run, 140);
        Assert.True(wrong.Count == 0, EarthTome1Compare.Show(wrong));

        var lives = Lives(run, 140);
        var drowned = lives.Where(l => l.Status3 == 0).ToList();
        Assert.Equal(14, drowned.Count); // the rocks that landed on the columns 7 to 9.
        Assert.Equal(18, lives.Count - drowned.Count);
        Assert.Equal(18, run.Heard.Played.Count(p => p.Sfx == Sfx46)); // no sound 46 for a drowned rock: E3 did not run.
        Assert.Equal(drowned.Count, run.Effects.Values.SelectMany(e => e).Count(e => e.StartsWith("attach(id=6,", StringComparison.Ordinal))); // one effect 6 per drowned rock.
        foreach (var life in drowned)
        {
            var row = run.Rocks[life.Last][life.Slot];
            Assert.Equal((4, 4, 0, 0u), (row.Status, row.Slope, row.AnimationEnd, row.TargetAnimation)); // the status 4 at the landing + 1, on the slope 4, with no E3.
            Assert.Contains(run.Effects[life.Last], e => e.StartsWith("attach(id=6,", StringComparison.Ordinal));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w5) the touches against an inert Beannoid: the tick, the shooter, the nine draws, the cooldown 25, the rock goes on.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(132, 104, 160)]
    [InlineData(108, 136, 160)]
    [InlineData(180, 104, 160)]
    [InlineData(108, 56, 160)]
    [InlineData(60, 104, 160)]
    [InlineData(132, 136, 160)]
    [InlineData(204, 104, 160)]
    [InlineData(84, 104, 127)] // cut before the tick 128: the Beannoid then hurts the hero in the key.
    public void W5_TheRocksTouchAnInertBeannoid_TheTickTheShooterTheNineDrawsAndTheCooldown25_AreTheKey_TheRockGoesOn(int x, int y, int last)
    {
        var key = EarthTome1Key.Load($"earth1_vs_beannoid_{x}_{y}");
        using var run = new EarthTome1Run("w5", 24, 100, null, new List<int[]> { new[] { x, y } });

        run.Go(last);

        var wrong = Everything(key, run, last);
        wrong.AddRange(EarthTome1Compare.Target(key, run, 1, last));
        Assert.True(wrong.Count == 0, $"{x}_{y} " + EarthTome1Compare.Show(wrong));

        // The touches: at the ticks the key writes, three sparks and nine draws (and the step of the end of the frame), the cooldown of the target 25, the shooter the key's rock; the rock continues.
        var hits = Enumerable.Range(1, last).Where(t => key.At(t).Draws == 9).ToList();
        Assert.NotEmpty(hits);
        foreach (var tick in hits)
        {
            Assert.Equal(10, run.FrameSteps[tick]);
            Assert.Equal(3, run.Effects[tick].Count(e => e.StartsWith("fx(id=9,", StringComparison.Ordinal)));
            Assert.Equal(25, run.Target[tick].Counter);
            Assert.NotNull(run.Target[tick].TouchedBy);
            Assert.Equal(key.At(tick).Others.Single().Value.TouchSlot, run.Target[tick].TouchedBy);
            Assert.True(run.Rocks[tick].ContainsKey(run.Target[tick].TouchedBy!.Value), $"the shooter of the tick {tick} is a rock");
            Assert.Equal(2, run.Rocks[tick][run.Target[tick].TouchedBy!.Value].Status); // the rock goes on: it is not stopped, nor destroyed, by the touch.
        }

        Assert.Equal(hits.Count, Enumerable.Range(1, last).Count(t => run.FrameSteps[t] == 10));
        Assert.All(Enumerable.Range(1, last), t => Assert.Equal(2, run.Target[t].Status)); // the Beannoid stays inert and alive.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w6) the interruptions: the object is freed at the tick T, there is no birth after it, the rocks born finish alone.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(98)]
    [InlineData(101)]
    public void W6_AnInterruption_FreesTheObjectAtTheTickT_NoBirthAfterIt_TheRocksAlreadyBornFinishAlone(int at)
    {
        var key = EarthTome1Key.Load("earth1_interrupt_" + at);
        using var run = new EarthTome1Run("w6", 24, 100, null);
        var last = key.Rows.Count;

        run.Go(last, at);

        // The object, the births, the rocks and their sounds are compared; the hero after T is the injury of E20.c2b (its animation, its place and its sound 23 are not compared: R-EL-6).
        var wrong = Everything(key, run, last, false, HeroHurtSfx);
        Assert.True(wrong.Count == 0, EarthTome1Compare.Show(wrong));

        Assert.Equal(0x2Bu, run.Hero[at - 1].ItemInUse);
        Assert.Equal(0u, run.Hero[at].ItemInUse); // the guard of h4a cancels the item at the tick T itself.
        Assert.All(Enumerable.Range(at, last - at + 1), t => Assert.Equal(0u, run.Hero[t].ItemInUse));
        var lives = Lives(run, last);
        Assert.All(lives, l => Assert.True(l.Birth <= at, $"a rock is born at {l.Birth}, after the interruption at {at}"));
        Assert.Equal(Enumerable.Range(1, last).Sum(t => key.At(t).Rocks.Keys.Count(k => t == 1 || !key.At(t - 1).Rocks.TryGetValue(k, out var before) || before.Status == 4)), lives.Count);
        Assert.All(lives, l => Assert.Equal(l.Status3 + 13, l.Last)); // the rocks finish alone: E3 at the landing, the death 13 ticks later.
        Assert.Equal(lives.Select(l => l.Status3).Order(), SoundTicks(run, Sfx46));
        Assert.Equal(at - 1, run.Hero[at].Counter); // the counter no longer moves.
        Assert.Equal(at - 1, run.Hero[last].Counter);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) the neutral cancellation: the rocks stay, the object is freed.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C1_TheNeutralCancellation_LeavesTheRocksAsTheyAre_AndFreesTheObject()
    {
        using var run = new EarthTome1Run("c1", 24, 100, null);
        run.Go(60);
        var hero = run.Arena.Hero;
        var rocks = run.Arena.Arc.Entities.Where(e => e.SpriteType == EarthTome1Run.Rock).OrderBy(e => e.Slot).ToList();
        var before = rocks.Select(r => (r.Slot, r.Status, r.PosX, r.PosY, r.PosZ, r.ForceX, r.ForceY, r.ForceZ, r.TargetAnimationId, r.Flags)).ToList();
        Assert.Equal(0x2Bu, hero.ItemInUse);
        Assert.True(rocks.Count >= 8, rocks.Count.ToString()); // rocks are in flight (the cancellation is not tested on an empty world).

        PlayerManager.CancelItemMode(hero);

        Assert.Equal(0u, hero.ItemInUse);
        Assert.Equal(before, run.Arena.Arc.Entities.Where(e => e.SpriteType == EarthTome1Run.Rock).OrderBy(e => e.Slot).Select(r => (r.Slot, r.Status, r.PosX, r.PosY, r.PosZ, r.ForceX, r.ForceY, r.ForceZ, r.TargetAnimationId, r.Flags)).ToList());
        Assert.All(rocks, r => Assert.NotEqual(EntityStatus.FlagToDestroy, r.Status));

        // Without an item in use the cancellation does nothing at all (the rocks neither).
        PlayerManager.CancelItemMode(hero);
        Assert.Equal(0u, hero.ItemInUse);
    }
}
