#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1, phase A (docs/plan-e20-heros.md section 1.31.4, the world tests (w1) to (w4), (w6), (w8), (s10b) and (w5); rule R-EL-4): the earth tome 2 in the real world of the map 14 (<see cref="EarthTome2Run"/>), the Circle pressed at the tick 1 with the tome 44 given and
/// equipped, the magic 4/4, the generator at the seed of the oracle. The tome is cast by the three seams of h0a (the start of the tomes of h4a, <see cref="EarthTome2Ai.Driver"/>, <see cref="EarthTome2Ai.Cancel"/>); E13 is registered on the registry of the runner when its key
/// is absent. The expected values are the keys of the real code of the binary with the slot index words written (R-EL-1, <c>docs/plan-e20-annexe/e20-h4c2/</c>): the keys of the ORIGINAL (<c>critic/keys/</c>, <c>disc/keys/</c>) for the casts and the interruptions the original
/// and the corrected game share (the mid-map cast, the interruptions at T = 60 and T = 100), and the INDICATIVE keys of the corrected game (<c>rockfix/</c>, D-E20-56, D-H4C2-4: a rock held in the air waits for the ground) for the rest. Every field of every tick is compared without
/// tolerance (R-EL-1), but the position of the rock held against a wall by the corrected game (R-EL-5 extended to the position by (s10a): 715 680 raw units on each axis).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome2WorldTests : IDisposable
{
    private const int Sfx55 = 55;
    private const int Sfx45 = 45;
    private const int HeroHurtSfx = 23;
    private const int WallBound = 715_680;

    private readonly ITestOutputHelper _output;

    public EarthTome2WorldTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        GameState.Instance.ResetForTests();
    }

    private static Func<int, int, CutBench.Cell> From(int column, CutBench.Cell cell) => (c, _) => c >= column ? cell : default;

    private static EarthTome2Key Mid() => EarthTome2Key.Load("critic/keys", "cast_2c_pf_mid_x600y480");

    private static EarthTome2Key Edge() => EarthTome2Key.Load("rockfix", "cast_2c_pf_rockfix");

    /// <summary>Everything the key holds, tick by tick: the rocks, the words of the tick (generator, kick, hero, item, magic), the sounds.</summary>
    private static List<string> Everything(EarthTome2Key key, EarthTome2Run run, int last, int ticksPerFrame = 1, Func<EarthTome2Key.KeyRock, int>? positionBound = null)
    {
        var all = new List<string>();
        all.AddRange(EarthTome2Compare.Rocks(key, run, last, positionBound));
        all.AddRange(EarthTome2Compare.Ticks(key, run, last, ticksPerFrame));
        all.AddRange(EarthTome2Compare.Sounds(key.Sounds, run, 1, last));
        return all;
    }

    private static List<int> SoundTicks(EarthTome2Run run, int sfx) => run.Heard.Played.Where(p => p.Sfx == sfx).Select(p => p.Tick).Order().ToList();

    private static List<int> KickTicks(EarthTome2Run run) => run.Kicks.Where(k => k.Value != 0).Select(k => k.Key).Order().ToList();

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) the mid-map cast, fifteen interior rocks: the whole key without tolerance.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_TheEarthTome2AtTheMiddleOfTheMap_EqualsTheKey_EveryTickEveryField_FifteenRocks_TheKicksAtTheTicksOfTheKey_TheItemFreedAtTheTick309()
    {
        var key = Mid();
        using var run = new EarthTome2Run("w1", 600, 480);

        run.Go(330);

        var wrong = Everything(key, run, 330);
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));

        // The claims of the plan, read on the DLL next to the key: the births, the statuses 3 and the kicks one tick after the landing, the sounds, the magic, the item.
        var lives = run.Lives();
        Assert.Equal(15, lives.Count);
        Assert.Equal(new[] { 18, 34, 50, 66, 82, 98, 114, 130, 146, 162, 178, 194, 226, 226, 242 }, lives.Select(l => l.Birth));
        Assert.All(lives, l => Assert.Equal((l.Ground + 1, l.Birth + 33), (l.Impact, l.Impact))); // E13 at the landing + 1, 33 ticks after the birth.
        Assert.All(lives, l => Assert.Equal((l.Impact + 46, l.Impact + 47), (l.AnimationEnd, l.Death))); // the end of the animation 1 at E13 + 46, the death at E13 + 47.
        Assert.Equal(lives.Select(l => l.Impact).Distinct().Order(), KickTicks(run)); // one kick per tick of E13 (two E13 at the tick 259 make ONE kick: the byte is assigned).
        Assert.Equal(lives.Select(l => l.Birth).Order(), SoundTicks(run, Sfx55));
        Assert.Equal(lives.Select(l => l.Impact).Order(), SoundTicks(run, Sfx45));
        Assert.All(run.Kicks.Values, kick => Assert.Contains(kick, new[] { 0, 2 })); // assigned 2, never 4.
        Assert.All(Enumerable.Range(1, 330), t => Assert.Equal(3, run.Hero[t].Mp)); // 4 -> 3 at the start.
        Assert.Equal(0x2Cu, run.Hero[308].ItemInUse);
        Assert.Equal(0u, run.Hero[309].ItemInUse); // freed at the tick 309.
        Assert.Equal(309, Enumerable.Range(1, 330).First(t => run.Hero[t].ItemInUse == 0));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2) the rock of the edge of the map: it slides along the edge, lands, and acts only on the ground.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W2_TheRockOfTheEdgeOfTheMap_SlidesAlongTheEdge_LandsAt194_AndGivesItsKickAndItsSoundAt195_NotAt163_AsTheCorrectedKey()
    {
        var key = Edge();
        using var run = new EarthTome2Run("w2", 100, 100);

        run.Go(330);

        var wrong = Everything(key, run, 330);
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));

        var lives = run.Lives();
        var edge = lives.Single(l => l.Birth == 162);
        Assert.Equal(655_360, edge.Rows[0].PosX); // born outside the grid (x < 0): placed at 10 px (s3).
        Assert.NotEqual(0, edge.Rows[0].Adjusted);
        Assert.Equal((194, 195, 195), (edge.Ground, edge.Impact, edge.Status3)); // the landing at 194, the impact and the status 3 at 195.
        Assert.Equal(1, run.Heard.Played.Count(p => p.Tick == 163 && p.Sfx == Sfx45)); // the original gave a second kick and a second sound 45 at 163, in the air; the one left is the rock born at 130, which lands.
        Assert.Equal(1, run.Heard.Played.Count(p => p.Tick == 195 && p.Sfx == Sfx45)); // the edge rock's own, at the ground + 1.
        Assert.Equal(2, run.Kicks[163]); // one kick (the byte is assigned) ...
        Assert.Equal(2, run.Kicks[195]); // ... and the edge rock's at 195.
        Assert.Equal(242, edge.Death);

        // Next to it, an interior rock of the same cast has the chronology of the interior rocks of (w1): E13 33 ticks after the birth.
        var interior = lives.First(l => l.Birth == 114);
        Assert.Equal((147, 147 - 114), (interior.Impact, interior.Impact - interior.Birth));
        Assert.Equal(interior.Ground + 1, interior.Impact);
        Assert.All(Enumerable.Range(163, 31), t => Assert.Equal(EntityStatus.Normal, (EntityStatus)edge.At(t)!.Status)); // in the air the status seen at the end of the tick stays 2 (D-H4C2-3).
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w3) and (w3b) the chronology of the rocks at 2 and 3 ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void W3_AnInteriorRock_HasTheSameChronologyRelativeToItsBirth_At2And3TicksPerFrame(int ticksPerFrame)
    {
        using var run = new EarthTome2Run("w3-" + ticksPerFrame, 600, 480);

        run.Go(330, ticksPerFrame);

        var lives = run.Lives();
        Assert.Equal(15, lives.Count);
        Assert.All(lives, l => Assert.Equal((32, 33, 46, 47), (l.Ground - l.Birth, l.Impact - l.Birth, l.AnimationEnd - l.Impact, l.Death - l.Impact)));
        Assert.Equal(lives.Select(l => l.Impact).Distinct().Order(), KickTicks(run)); // the kick of the tick of E13, in the list of its frame.
        Assert.Equal(lives.Select(l => l.Birth).Order(), SoundTicks(run, Sfx55));
        Assert.Equal(lives.Select(l => l.Impact).Order(), SoundTicks(run, Sfx45));
        Assert.All(run.Kicks.Values, kick => Assert.Contains(kick, new[] { 0, 2 }));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void W3b_ARockHeldInTheAirByTheEdgeOfTheMap_WaitsForTheGround_At2And3TicksPerFrame_TheStatusStays2_AndTheImpactIsOneTickAfterTheLanding(int ticksPerFrame)
    {
        using var run = new EarthTome2Run("w3b-" + ticksPerFrame, 40, 100); // close to the west edge: several rocks are clamped in the air whatever the draws.

        run.Go(330, ticksPerFrame);

        var lives = run.Lives();
        var held = lives.Where(l => l.Rows.Any(r => r.Adjusted != 0 && r.Ground == 0)).ToList();
        Assert.NotEmpty(held); // the edge clamps at least one rock in the air (not vacuous).
        foreach (var life in held)
        {
            var label = $"rock born at {life.Birth}";
            Assert.True(life.Impact == life.Ground + 1, $"{label}: impact {life.Impact} for the landing {life.Ground}");
            Assert.All(life.Rows.Where(r => r.Tick < life.Impact), r => Assert.True(r.Status == 2 && r.TargetAnimation != 1 && r.Flags == 0x132014u, $"{label} t{r.Tick}: status {r.Status} animation {r.TargetAnimation} flags {r.Flags:X}")); // nothing of the impact before the landing (the animation asked is the fall's, never the impact's 1).
            Assert.Equal(2, run.Kicks[life.Impact]);
            Assert.Equal((life.Impact + 46, life.Impact + 47), (life.AnimationEnd, life.Death));
        }

        Assert.Equal(lives.Select(l => l.Impact).Distinct().Order(), KickTicks(run));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w4) the interruptions: the rocks in the air go to the status 3 and the animation 1, a sound 45 each; the hero is hurt (E20.c2b).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(60, "disc/keys", "cancel_2c_pf_t60", "critic/keys", "cancel_2c_pf_t60_sounds.txt")]
    [InlineData(100, "disc/keys", "cancel_2c_pf_t100", "critic/keys", "cancel_2c_pf_t100_sounds.txt")]
    [InlineData(170, "rockfix", "cancel_2c_pf_t170_rockfix", "rockfix", "cancel_2c_pf_t170_rockfix_sounds.txt")]
    public void W4_AnInterruption_PutsTheRocksInTheAirAtTheStatus3WithTheAnimation1_ASound45EachAndTheKicksOfTheKey_TheHeroIsHurt(int at, string folder, string name, string soundsFolder, string soundsFile)
    {
        var key = EarthTome2Key.CancelKey.Load(folder, name, soundsFolder, soundsFile);
        using var run = new EarthTome2Run("w4-" + at, 100, 100);

        run.Go(key.LastTick, 1, at);

        // The rocks by slot (status, animation, Z, end of the animation, flags, blocked), the hero (the animation after T is the injury of E20.c2b: R-EL-6 revised), the item, the kicks.
        var wrong = EarthTome2Compare.Cancelled(key, run);
        wrong.AddRange(EarthTome2Compare.Sounds(key.Sounds, run, at - 2, key.LastTick));
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));

        // A sound 45 for each rock cancelled in the air, at the tick T, and the sound 23 of the hero.
        var inTheAir = key.Ticks[at - 1].Rocks.Values.Count(r => r.Status == 2);
        Assert.True(inTheAir >= 2, "the interruption catches rocks in the air");
        Assert.Equal(inTheAir, run.Heard.Played.Count(p => p.Tick == at && p.Sfx == Sfx45));
        Assert.Equal(new[] { at }, run.Heard.Played.Where(p => p.Sfx == HeroHurtSfx).Select(p => p.Tick));
        Assert.Equal(0x2Cu, run.Hero[at - 1].ItemInUse);
        Assert.Equal(0u, run.Hero[at].ItemInUse); // the guard of h4a cancels the item at the tick T itself.
        Assert.All(Enumerable.Range(at, key.LastTick - at + 1), t => Assert.Equal(0u, run.Hero[t].ItemInUse));
        Assert.All(run.Lives().Where(l => l.Birth > at), l => Assert.Fail($"a rock is born at {l.Birth}, after the interruption at {at}"));
        Assert.All(key.Ticks[at].Rocks.Values.Where(r => r.Status == 3), r => Assert.Equal(1u, r.TargetAnimation)); // the cancelled rocks wait for the end of the animation 1.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w6) a band of deep water: the rocks of the type 17 live (no gravity flag); a marked entity dies there (f6a).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W6_ABandOfDeepWater_TheRocksOfTheType17Live_TheyDoNotDrown_AsTheKey()
    {
        var key = Edge();
        using var run = new EarthTome2Run("w6", 100, 100, From(7, new CutBench.Cell(0, 0, null, 0x08)));

        run.Go(330);

        var wrong = Everything(key, run, 330);
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));
        var lives = run.Lives();
        Assert.Equal(15, lives.Count);
        Assert.All(lives, l => Assert.Equal(0, l.Rows.Count(r => r.Slope != 0))); // nothing writes the slope of the type 17 (s7): no drowning.
        Assert.All(lives, l => Assert.Equal(l.Impact + 47, l.Death)); // each rock lives its whole life, the end of its animation 1 included, over the water.
        Assert.Equal(15, SoundTicks(run, Sfx45).Count);
    }

    [Fact]
    public void W6_AnEntityMarkedWithGravity_DiesInTheSameBandOfDeepWater_TheWitnessOfTheRocksThatDoNot()
    {
        using var run = new EarthTome2Run("w6-witness", 100, 100, From(7, new CutBench.Cell(0, 0, null, 0x08)));
        EntityScriptProxy? witness = null;
        var statuses = new Dictionary<int, EntityStatus>();

        run.Go(80, 1, null, tick =>
        {
            if (tick == 3)
            {
                // The rock of the earth tome 1 (type 16) carries the gravity flag: dropped over the band (column 8) it falls into the water.
                witness = ((IEntityWorldContext)run.Arena.Arc.Proxy).SpawnEntityBySpriteIndex(run.Arena.Hero, false, 16, 200 << 16, 100 << 16, 0x500000, 0);
            }

            if (witness is not null)
            {
                statuses[tick] = witness.Status;
            }
        });

        Assert.NotNull(witness);
        Assert.Contains(statuses.Values, s => s is EntityStatus.FlagToDestroy or EntityStatus.Destroyed); // it drowned (f6a), where the rocks of the type 17 live.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w8) the plateaus: the rocks born on a high plateau are kept (byte 16) or destroyed on the spot (byte 17); the rock 8 held against the wall waits for the ground.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(16, "cast_2c_pf_plateau_hb16_rockfix", 147, 194)]
    [InlineData(17, "cast_2c_pf_plateau_hb17_rockfix", 147, 194)]
    public void W8_ThePlateau_TheRocksBornOnItAreKeptAtTheByte16AndDestroyedOnTheSpotAtTheByte17_TheRock8HeldAgainstTheWallWaitsForTheGround(int height, string name, int impact8, int death8)
    {
        var key = EarthTome2Key.Load("rockfix", name);
        using var run = new EarthTome2Run("w8-" + height, 100, 100, From(7, new CutBench.Cell(0, height)));

        run.Go(240);

        var wrong = Everything(key, run, 240, 1, rock => rock.Id == 8 ? WallBound : 0);
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));

        var lives = run.Lives();
        Assert.Equal(14, lives.Count); // the births before the tick 240 (the 15th is at 242).
        Assert.Equal(lives.Select(l => l.Birth).Order(), SoundTicks(run, Sfx55)); // the sound 55 at every birth, the destroyed ones included.
        var onThePlateau = lives.Where(l => key.Rocks.Single(r => r.Birth == l.Birth && r.Slot == l.Slot).Id is 9 or 13 or 14).ToList();
        Assert.Equal(3, onThePlateau.Count);
        if (height == 17)
        {
            // Destroyed on the spot: the status 4 at the tick of the birth, no sound 45, no kick, the draws made (the generator of the key is followed tick by tick above).
            Assert.All(onThePlateau, l => Assert.Equal((l.Birth, 0), (l.Death, l.Impact)));
        }
        else
        {
            Assert.All(onThePlateau, l => Assert.NotEqual(l.Birth, l.Death)); // kept: they live on the plateau.
        }

        var landed = lives.Where(l => l.Impact != 0).ToList();
        Assert.Equal(landed.Select(l => l.Impact).Order(), SoundTicks(run, Sfx45)); // a sound 45 per rock that reached the ground, none for a destroyed one.
        Assert.Equal(landed.Select(l => l.Impact).Distinct().Order(), KickTicks(run));

        // The rock 8 stops against the wall of the plateau at 141: no impact in the air (143), the impact one tick after the landing at 146, the death at 194.
        var eight = lives.Single(l => l.Birth == key.Rocks.Single(r => r.Id == 8).Birth);
        Assert.Equal((146, impact8, impact8, death8), (eight.Ground, eight.Impact, eight.Status3, eight.Death));
        Assert.Equal(0, run.Kicks[143]);
        Assert.Equal(2, run.Kicks[147]);
        Assert.All(eight.Rows.Where(r => r.Tick is >= 141 and <= 146), r => Assert.Equal(2, r.Status));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s10b) the final position of the rock 8 and of the rock of the edge, measured in the DLL and compared with the bounds of (s10a).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("plateau16", 16)]
    [InlineData("plateau17", 17)]
    public void S10b_TheFinalPositionOfTheRock8AtTheWall_IsWithinTheBoundOfS10a_OnEachAxis(string label, int height)
    {
        var key = EarthTome2Key.Load("rockfix", $"cast_2c_pf_plateau_hb{height}_rockfix");
        using var run = new EarthTome2Run("s10b-" + label, 100, 100, From(7, new CutBench.Cell(0, height)));

        run.Go(240);

        var keyRock = key.Rocks.Single(r => r.Id == 8);
        var life = run.Lives().Single(l => l.Birth == keyRock.Birth);
        var ground = keyRock.Track.First(r => r.Ground == 1);
        var row = life.At(ground.Tick)!;
        var delta = (row.PosX - ground.X, row.PosY - ground.Y);
        _output.WriteLine($"{label}: rock 8 at the landing (tick {ground.Tick}): DLL ({row.PosX}, {row.PosY}), key ({ground.X}, {ground.Y}), difference {delta} raw units (bound {WallBound})");
        var worst = life.Rows.Where(r => keyRock.At(r.Tick) is not null).Max(r => Math.Max(Math.Abs((long)r.PosX - keyRock.At(r.Tick)!.X), Math.Abs((long)r.PosY - keyRock.At(r.Tick)!.Y)));
        _output.WriteLine($"{label}: rock 8, largest difference over its life {worst} raw units");
        Assert.InRange(Math.Abs(delta.Item1), 0, WallBound);
        Assert.InRange(Math.Abs(delta.Item2), 0, WallBound);
        Assert.InRange(worst, 0, WallBound);
    }

    [Fact]
    public void S10b_TheFinalPositionOfTheRockOfTheEdge_IsExactlyTheKeys_OnEachAxis()
    {
        var key = Edge();
        using var run = new EarthTome2Run("s10b-edge", 100, 100);

        run.Go(330);

        var keyRock = key.Rocks.Single(r => r.Id == 11);
        var life = run.Lives().Single(l => l.Birth == keyRock.Birth);
        var ground = keyRock.Track.First(r => r.Ground == 1);
        var row = life.At(ground.Tick)!;
        _output.WriteLine($"edge: rock 11 at the landing (tick {ground.Tick}): DLL ({row.PosX}, {row.PosY}), key ({ground.X}, {ground.Y})");
        Assert.Equal((ground.X, ground.Y), (row.PosX, row.PosY));
        Assert.All(life.Rows, r => Assert.Equal((keyRock.At(r.Tick)!.X, keyRock.At(r.Tick)!.Y), (r.PosX, r.PosY)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w5) the touches against six static targets: the tick of each touch is the key's. NOT blocking (the plan): the targets are inert Beannoids of the arena (HP 5000), as the probe (s8), and the touches are
    // read at the cooldown of 25 the touch sets.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W5_TheRocksTouchSixStaticTargets_AtTheTicksOfTheKey_EachTouchSetsTheCooldown25()
    {
        using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(AnnexFiles.EarthTome2Annex("disc/keys/targets_2c_pf_inv0.json")));
        var targets = document.RootElement.GetProperty("targets").EnumerateArray().ToList();
        var pixels = targets.Select(t => new[] { t.GetProperty("x").GetInt32(), t.GetProperty("y").GetInt32() }).ToList();
        var keyHits = targets.Select(t => t.GetProperty("hits").EnumerateArray().Select(h => h[0].GetInt32() - 1).ToList()).ToList(); // the oracle's harness records a touch the tick AFTER the hit pass wrote it (the nine draws are made the tick before, (s8)): the key's tick minus 1 is the tick of the touch.
        using var run = new EarthTome2Run("w5", 100, 100);
        // Six blocks of the global sprite 25 (the "transparent block 1 x 1 x 2" of the oracle), of the class A with 5000 HP, on the flat field.
        foreach (var pixel in pixels)
        {
            var block = ((IEntityWorldContext)run.Arena.Arc.Proxy).SpawnEntityBySpriteIndex(run.Arena.Hero, false, 25, pixel[0] << 16, pixel[1] << 16, 0, 0);
            Assert.NotNull(block);
            block!.Flags |= EntityFlags.ClassA; // the class the rocks (they hit the class A) touch.
            block.Hp = 5000;
            run.Arena.Targets.Add(block);
        }

        run.Go(330);

        for (var i = 0; i < pixels.Count; i++)
        {
            var seen = Enumerable.Range(1, 330).Where(t => run.TargetCounters[t][i] == 25).ToList();
            Assert.True(keyHits[i].Where(t => t <= 330).SequenceEqual(seen), $"target ({pixels[i][0]}, {pixels[i][1]}): touches at [{string.Join(",", seen)}] (key [{string.Join(",", keyHits[i])}])");
        }

        Assert.True(keyHits.Sum(h => h.Count) > 20);
    }
}
