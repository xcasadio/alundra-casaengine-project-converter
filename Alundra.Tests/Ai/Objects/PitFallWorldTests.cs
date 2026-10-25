#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2b2 T1 (docs/plan-e20-heros.md section 1.24.5, "E20.g2b2", D-G2B2-1 to D-G2B2-4, D-E20-74): a thrown object that lands on a hole or an exit floor disappears, with the break effect of its header, as in the original. The pick phase of the binary
/// (<c>0x800386D0</c>, <c>0x800387AC</c>) destroys the entity that carries <c>0x200000</c> when the OR of its four corners (<c>+0x180</c>) has a bit of <c>0x8004</c>; the tile words are written by the physics pass of the tick of the landing (T+22), the pick phase destroys one
/// tick later (T+23, status 4 never through 3, the slot free at T+24). Every expected value is the binary's replay of T0 (<c>docs/plan-e20-annexe/e20-g2b2/replay/replay_b.txt</c>; <c>docs/plan-e20-annexe/e20-g2c1-disc/g2c1/out/q3_pit.txt</c> for the objects of C4), the ones the T0 probe of the DLL measured equal
/// (<c>docs/plan-e20-annexe/e20-g2b2/probe-dll/</c>), each object for its own rows (D-G2B2-4). T is the tick of the front of the Square (the tick 1 of the run), K the ticks since T. The tests are run on the real world of <see cref="C4Run"/> (the object put in the hands by <see cref="PlayerManager.PutInHands"/>,
/// thrown by the real hero); (p1), (p2), (p3), (p3b), (p6) and (p8) are red until the hook of f6a feeds the tile words of a marked entity; (p4), (p5) and (p7) are witnesses, green before and after.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PitFallWorldTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const uint Right = PadState.Right;
    private const uint Hole = 0x0004;
    private const uint ExitFloor = 0x8000;

    public PitFallWorldTests()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The ground of a scenario: the field (columns 6 to 9 of 24 px, hero at x = 109, landing at x = 178, column 7, row 4 to 5), and the word the four corners give at the landing: the OR and the AND.</summary>
    private sealed record Ground(string Name, Func<int, int, CutBench.Cell>? Cell, uint Or, uint And);

    private static readonly Ground HoleGround = new("hole", (cx, _) => cx >= 6 && cx <= 9 ? new CutBench.Cell(0x04, 0) : new CutBench.Cell(0, 0), Hole, Hole);
    private static readonly Ground ExitGround = new("exit", (cx, _) => cx >= 6 && cx <= 9 ? new CutBench.Cell(0, 0, null, 0x80) : new CutBench.Cell(0, 0), ExitFloor, ExitFloor);
    private static readonly Ground CornerHole = new("corner", (cx, cy) => cx == 7 && cy == 5 ? new CutBench.Cell(0x04, 0) : new CutBench.Cell(0, 0), Hole, 0);
    private static readonly Ground CornerExit = new("cornerexit", (cx, cy) => cx == 7 && cy == 5 ? new CutBench.Cell(0, 0, null, 0x80) : new CutBench.Cell(0, 0), ExitFloor, 0);

    private static Ground ByName(string name) => name switch
    {
        "hole" => HoleGround,
        "exit" => ExitGround,
        "corner" => CornerHole,
        "cornerexit" => CornerExit,
        _ => throw new ArgumentException(name),
    };

    // The objects of C4 (index of C4WorldTests.Seven): the barrel 0, the stone 1, the star 2, the trunk 6.
    private static C4Run BuildC4(int index, Ground? ground)
    {
        var o = C4WorldTests.Seven[index];
        return new C4Run(o.Name, o.Zone, o.World, 0, cell: ground?.Cell, recordId: o.Record, extraTypes: new[] { o.Type }, contents: 0);
    }

    // The crate (type 292, index 36 of the page of the map 67) and the jar (type 297, index 41 of the map 70) of g2b.
    private static C4Run BuildCrate(Ground? ground) => new("crate36", "Coal Mine", "Coal Mine-67", 36, cell: ground?.Cell, extraTypes: new[] { 292 }, contents: 0);

    private static C4Run BuildJar(Ground? ground) => new("jar41", "Coastal Cave", "Coastal Cave-70", 41, cell: ground?.Cell, extraTypes: new[] { 297 }, contents: 0);

    private static C4Run.CrateTick At(C4Run run, int k) => run.Ticks[k + 1];

    /// <summary>The OR and the AND of the tile words of the object after the tick K+1, by K.</summary>
    private static Dictionary<int, (uint Or, uint And)> Throw(C4Run run, int ticks = 48)
    {
        var words = new Dictionary<int, (uint Or, uint And)>();
        run.Go(1, ticks, tick => tick == 1 ? Square | Right : 0u, tick => words[tick - 1] = (run.Crate.CombinedVramFlagsOR, run.Crate.CombinedVramFlagsAND));
        return words;
    }

    private static List<int> Sfx24(C4Run run) => run.Heard.Played.Where(p => p.Sfx == 0x18).Select(p => p.Tick - 1).ToList();

    /// <summary>
    /// The death on a hole or an exit (replay_b.txt, q3_pit.txt): the words written at T+22 (the OR and the AND of the ground), the status 4 (FlagToDestroy) at T+23 without any state 3, the slot free at T+24, the break effect of the header attached on the stage at the end of the tick 24,
    /// no landing sound (neither the sound 24 nor the sounds 27 and 31 of E2), and E2 never called.
    /// </summary>
    private static void AssertDiesInThePit(string who, C4Run run, Dictionary<int, (uint Or, uint And)> words, Ground ground, int effect)
    {
        for (var k = 0; k <= 21; k++)
        {
            var t = At(run, k);
            Assert.True(t.Present && t.Status == EntityStatus.Normal && t.Target == 1, $"{who} T+{k}: {t}");
            Assert.True(words[k + 1] == (0u, 0u), $"{who} T+{k}: the words are written at the landing only, got {words[k + 1]}");
        }

        var landed = At(run, 22);
        Assert.True(landed.Present && landed.Status == EntityStatus.Normal && landed.OnGround == 1, $"{who} T+22: {landed}");

        var dying = At(run, 23);
        Assert.True(dying.Present && dying.Status == EntityStatus.FlagToDestroy, $"{who} T+23 (the status 4, never through 3): {dying}");
        for (var k = 24; k <= 39; k++)
        {
            Assert.False(At(run, k).Present, $"{who} T+{k}: the slot is free from T+24");
        }

        for (var k = 0; k <= 39; k++)
        {
            Assert.True(At(run, k).E2Calls == 0 && At(run, k).Target != 2, $"{who} T+{k}: E2 is never reached on a hole: {At(run, k)}");
        }

        Assert.Equal((24, effect), run.AnchoredEffects.FirstOrDefault());
        Assert.Equal(new[] { (1, 26) }, run.Heard.Played.ToArray());
        Assert.Equal((ground.Or, ground.And), words[23]);
    }

    // -----------------------------------------------------------------------------------------
    // (p1) the barrel thrown on a hole; (p2) on an exit floor; (p3) the stone, the star and the trunk on a hole; (p3b) the corner cases (a single corner is enough: the OR).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void P1_TheBarrelThrownOnAHole_DisappearsAtT23_WithTheEffect10_AndTheSlotIsFreeAtT24()
    {
        using var run = BuildC4(0, HoleGround);
        var words = Throw(run);
        AssertDiesInThePit("barrel hole", run, words, HoleGround, 10);
    }

    [Fact]
    public void P2_TheBarrelThrownOnAnExitFloor_DisappearsAtT23_WithTheEffect10()
    {
        using var run = BuildC4(0, ExitGround);
        var words = Throw(run);
        AssertDiesInThePit("barrel exit", run, words, ExitGround, 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void P3_TheStoneTheStarAndTheTrunkThrownOnAHole_DisappearAtT23_WithTheEffect10(int index)
    {
        using var run = BuildC4(index, HoleGround);
        var words = Throw(run);
        AssertDiesInThePit(C4WorldTests.Seven[index].Name + " hole", run, words, HoleGround, 10);
    }

    [Theory]
    [InlineData(0, "corner")]
    [InlineData(2, "corner")]
    [InlineData(0, "cornerexit")]
    [InlineData(2, "cornerexit")]
    public void P3b_ASingleCornerOnTheHoleOrTheExit_IsEnough_TheOrOfTheCorners(int index, string ground)
    {
        var g = ByName(ground);
        using var run = BuildC4(index, g);
        var words = Throw(run);
        Assert.Equal(0u, g.And);
        AssertDiesInThePit(C4WorldTests.Seven[index].Name + " " + ground, run, words, g, 10);
    }

    // -----------------------------------------------------------------------------------------
    // (p4) the contrast: the same barrel on a full cell stays posed, the sound 24 at T+23, no effect.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void P4_TheSameBarrelThrownOnAFullCell_StaysPosed_TheSound24AtT23_NoEffect()
    {
        using var run = BuildC4(0, null);
        var words = Throw(run);
        for (var k = 0; k <= 39; k++)
        {
            var t = At(run, k);
            Assert.True(t.Present && t.Status == EntityStatus.Normal, $"T+{k}: {t}");
            Assert.True(words[k + 1] == (0u, 0u), $"T+{k}: {words[k + 1]}");
        }

        Assert.Equal(new[] { 23 }, Sfx24(run));
        Assert.Empty(run.AnchoredEffects);
    }

    // -----------------------------------------------------------------------------------------
    // (p5) D-T-12 kept: an entity placed by the map (a record, never marked) on a hole cell stays alive and its words stay zero.
    // -----------------------------------------------------------------------------------------

    // The barrel (0) and the trunk (6): the trunk is the one placed entity that the slope of the ground feeds (FeedsGroundSlope), and its words stay 0 all the same.
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void P5_AnEntityPlacedByTheMap_OnAHoleCell_StaysAlive_ItsWordsStayZero(int index)
    {
        var o = C4WorldTests.Seven[index];
        using var run = new C4Run(o.Name, o.Zone, o.World, 0, cell: HoleGround.Cell, recordId: o.Record, extraTypes: new[] { o.Type }, carry: false, contents: 0);
        Assert.False(run.Crate.HasLogicalZ);
        run.Crate.PosX = 178 << 16;
        run.Crate.PosY = 100 << 16;
        run.Crate.PosZ = 0;
        run.Crate.PushLogicalPositionToRoot();

        var words = new Dictionary<int, (uint Or, uint And)>();
        run.Go(1, 48, _ => 0u, tick => words[tick - 1] = (run.Crate.CombinedVramFlagsOR, run.Crate.CombinedVramFlagsAND));
        for (var k = 0; k <= 39; k++)
        {
            var t = At(run, k);
            Assert.True(t.Present && t.Status == EntityStatus.Normal, $"T+{k}: {t}");
            Assert.True(words[k + 1] == (0u, 0u), $"T+{k}: {words[k + 1]}");
        }

        Assert.Equal(178, At(run, 39).X >> 16);
    }

    // (p9, T3) a marked entity WITHOUT the gravity (an arrow, a bolt) keeps the words at 0 above a hole: the gravity gate of UpdateVramFlags (the binary's physics pass writes the words for the entities that have the gravity only).
    [Fact]
    public void P9_AMarkedEntityWithoutTheGravity_OnAHoleCell_StaysAlive_ItsWordsStayZero()
    {
        var o = C4WorldTests.Seven[0];
        using var run = new C4Run(o.Name, o.Zone, o.World, 0, cell: HoleGround.Cell, recordId: o.Record, extraTypes: new[] { o.Type }, carry: false, contents: 0);
        run.Crate.HasLogicalZ = true;
        run.Crate.Flags &= ~EntityFlags.Gravity;
        run.Crate.PosX = 178 << 16;
        run.Crate.PosY = 100 << 16;
        run.Crate.PosZ = 0;
        run.Crate.PushLogicalPositionToRoot();

        var words = new Dictionary<int, (uint Or, uint And)>();
        run.Go(1, 48, _ => 0u, tick => words[tick - 1] = (run.Crate.CombinedVramFlagsOR, run.Crate.CombinedVramFlagsAND));
        for (var k = 0; k <= 39; k++)
        {
            var t = At(run, k);
            Assert.True(t.Present && t.Status == EntityStatus.Normal, $"T+{k}: {t}");
            Assert.True(words[k + 1] == (0u, 0u), $"T+{k}: {words[k + 1]}");
        }
    }

    // (p10, T3) each of the four corners of the box reads its own cell: a marked barrel at rest astride the corner of four cells (the cells are 24 px wide and 16 px high; the box 2 x 2 cells), a hole under ONE cell only; the OR has the bit 4 whichever corner it is, the AND stays 0, and the barrel dies.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void P10_EachCornerOfTheBoxReadsItsOwnCell_AHoleUnderOneCornerIsEnough(int corner)
    {
        var o = C4WorldTests.Seven[0];
        int x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        int px = 0, py = 0;

        void Place(C4Run r)
        {
            r.Crate.HasLogicalZ = true;
            px = (192 << 16) - (r.Crate.Width >> 1) - r.Crate.ModX;
            py = (128 << 16) - (r.Crate.Height >> 1) - r.Crate.ModY;
            r.Crate.PosX = px;
            r.Crate.PosY = py;
            r.Crate.PosZ = 0;
            r.Crate.PushLogicalPositionToRoot();
            x1 = (r.Crate.PosX + r.Crate.ModX) >> 16;
            x2 = (r.Crate.PosX + r.Crate.ModX + r.Crate.Width) >> 16;
            y1 = (r.Crate.PosY + r.Crate.ModY) >> 16;
            y2 = (r.Crate.PosY + r.Crate.ModY + r.Crate.Height) >> 16;
        }

        using (var probe = new C4Run(o.Name, o.Zone, o.World, 0, cell: null, recordId: o.Record, extraTypes: new[] { o.Type }, carry: false, contents: 0))
        {
            Place(probe);
        }

        Assert.True(x1 / 24 != x2 / 24 && y1 / 16 != y2 / 16, $"the box must straddle four cells: x {x1}..{x2}, y {y1}..{y2}");
        var cx = (corner & 1) == 0 ? x1 / 24 : x2 / 24;
        var cy = (corner & 2) == 0 ? y1 / 16 : y2 / 16;
        using var run = new C4Run(o.Name, o.Zone, o.World, 0, cell: (x, y) => x == cx && y == cy ? new CutBench.Cell(0x04, 0) : new CutBench.Cell(0, 0), recordId: o.Record, extraTypes: new[] { o.Type }, carry: false, contents: 0);
        Place(run);

        var words = new Dictionary<int, (uint Or, uint And)>();
        run.Go(1, 8, _ => 0u, tick => words[tick - 1] = (run.Crate.CombinedVramFlagsOR, run.Crate.CombinedVramFlagsAND));
        Assert.Equal((Hole, 0u), words[1]);
        Assert.False(At(run, 6).Present, $"corner {corner}: the barrel on one hole cell disappears: {At(run, 6)}");
    }

    // (p11, T3) the words are fed in the status 3 too (the physics list of the binary holds the statuses 2 and 3): a marked barrel in the status Deactivated, at rest on a hole cell, has the words (4, 4) after the first tick.
    [Fact]
    public void P11_AMarkedEntityInTheStatus3_OnAHoleCell_HasItsWordsFed()
    {
        var o = C4WorldTests.Seven[0];
        using var run = new C4Run(o.Name, o.Zone, o.World, 0, cell: HoleGround.Cell, recordId: o.Record, extraTypes: new[] { o.Type }, carry: false, contents: 0);
        run.Crate.HasLogicalZ = true;
        run.Crate.Status = EntityStatus.Deactivated;
        run.Crate.PosX = 178 << 16;
        run.Crate.PosY = 100 << 16;
        run.Crate.PosZ = 0;
        run.Crate.PushLogicalPositionToRoot();

        var words = new Dictionary<int, (uint Or, uint And)>();
        run.Go(1, 4, _ => 0u, tick => words[tick - 1] = (run.Crate.CombinedVramFlagsOR, run.Crate.CombinedVramFlagsAND));
        Assert.True((Hole, Hole) == words[1] || (Hole, Hole) == words[2], $"{string.Join(";", words.Select(w => w.Key + ":" + w.Value))} {At(run, 1)}");
    }

    // -----------------------------------------------------------------------------------------
    // (p6) the crate and the jar thrown on a hole or an exit: the pick phase destroys them at T+23 (the status 4), the slot free at T+24, the effect 10 (crate) or 2 (jar) at the tick 24,
    // E2 never called and no sound 27 or 31 (replay_b.txt); before the line they went through E2 at T+23, the status 4 at T+25, the effect at the tick 26 (g2b).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("hole")]
    [InlineData("exit")]
    [InlineData("corner")]
    [InlineData("cornerexit")]
    public void P6_TheCrateThrownOnAHoleOrAnExit_IsDestroyedByThePickPhaseAtT23_NotByE2(string ground)
    {
        var g = ByName(ground);
        using var run = BuildCrate(g);
        var words = Throw(run);
        AssertDiesInThePit("crate " + ground, run, words, g, 10);
    }

    [Theory]
    [InlineData("hole")]
    [InlineData("exit")]
    [InlineData("corner")]
    [InlineData("cornerexit")]
    public void P6_TheJarThrownOnAHoleOrAnExit_IsDestroyedByThePickPhaseAtT23_NotByE2(string ground)
    {
        var g = ByName(ground);
        using var run = BuildJar(g);
        var words = Throw(run);
        AssertDiesInThePit("jar " + ground, run, words, g, 2);
    }

    // -----------------------------------------------------------------------------------------
    // (p7) the contrast: the crate (and the jar) thrown on a full cell follow the chronology of g2b: E2 at T+23 (the animation 2, the sound 27 or 31), the status 4 at T+25 with the effect, the slot free at T+26.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, 27, 10)]
    [InlineData(true, 31, 2)]
    public void P7_TheCrateAndTheJarThrownOnAFullCell_FollowTheChronologyOfG2b(bool jar, int landingSound, int effect)
    {
        using var run = jar ? BuildJar(null) : BuildCrate(null);
        var words = Throw(run);
        for (var k = 0; k <= 22; k++)
        {
            Assert.True(At(run, k).Present && At(run, k).Status == EntityStatus.Normal && At(run, k).E2Calls == 0, $"T+{k}: {At(run, k)}");
        }

        Assert.True(At(run, 23).Present && At(run, 23).Target == 2 && At(run, 23).E2Calls == 1, $"T+23: {At(run, 23)}");
        Assert.True(At(run, 24).Present && At(run, 24).Status != EntityStatus.FlagToDestroy, $"T+24: {At(run, 24)}");
        Assert.True(At(run, 25).Present && At(run, 25).Status == EntityStatus.FlagToDestroy, $"T+25: {At(run, 25)}");
        Assert.False(At(run, 26).Present);
        Assert.Equal((26, effect), run.AnchoredEffects.FirstOrDefault());
        Assert.Equal(new[] { (1, 26), (24, landingSound) }, run.Heard.Played.ToArray());
        Assert.All(words.Where(w => w.Key <= 39), w => Assert.Equal((0u, 0u), w.Value));
    }

    // -----------------------------------------------------------------------------------------
    // (p8) D-G2B2-3: the branch CombinedVramFlagsAND & 4 of E2 is live for a marked object, and the crate thrown on a hole has the four corners at 4 (the AND) at the landing; on the binary the pick phase kills it
    // before E2 runs (replay_b.txt), and so does the DLL: E2 is never called.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void P8_TheCrateOnAHole_HasTheFourCornersAt4_AndE2IsNeverCalled_AsOnTheBinary()
    {
        using var run = BuildCrate(HoleGround);
        var words = Throw(run);
        Assert.Equal((Hole, Hole), words[23]);
        Assert.True(At(run, 22).E2Calls == 0 && At(run, 23).E2Calls == 0, $"{At(run, 22)} / {At(run, 23)}");
        Assert.All(Enumerable.Range(0, 40), k => Assert.Equal(0, At(run, k).E2Calls));
        Assert.Equal(EntityStatus.FlagToDestroy, At(run, 23).Status);
    }
}
