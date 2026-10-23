#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T3, written in T1 (docs/plan-e20-heros.md section 1.24.6, D-G5B-5): the arcs of the scripted buttons and blocks on the real maps, through <see cref="ScriptedPushRun"/> (real controller, real prefabs, the map's own
/// programs). The values are the DLL's, measured with the three opcodes ported (the discovery's prototype, <c>docs/plan-e20-annexe/e20-g5/g5b-disc/probe_out_proto/</c>, replayed in T0; those of the binary are in the discovery's
/// report). The pad frame <c>n</c> is the <c>n</c>-th frame a direction is held after the hero is placed; the trace frame of an instruction is the pad frame minus one plus the offset of the run. Every test holds at least one
/// positive effect, so it is red while the opcodes are skipped by their size (<c>Result</c> stale, <c>0x72</c> not copying): at the start HEAD a block touched in the repair of Nirude slides by half a pixel and the buttons of Torla do not move.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ButtonAndBlockSitesTests
{
    private const string Lair = "Nirude’s Lair";
    private const string LairWorld = "Nirude’s Lair-40";
    private const string Inner = "Nirude";
    private static readonly int[] TorlaFlags = { 1253, 1254, 1267, 1269 };

    private static void AssertNoneOfTheThreeIsSkipped(ScriptedPushRun run)
        => Assert.DoesNotContain(run.Arc.SkippedOrExceeded, t => t.Opcode is 0x72 or 0xA9 or 0xAA);

    private static (int Contact, int Anim1, int FirstMove, int LastMove, int Anim0, double EndY) Slide(ScriptedPushRun run, int record)
    {
        var contact = run.FirstFrame(s => s.BlockedBy == record);
        var anim1 = run.FirstFrame(s => s.Watched[record].Animation == 1);
        var moves = run.Samples.Skip(1).Where((s, i) => s.Watched[record].Y != run.Samples[i].Watched[record].Y).Select(s => s.Frame).ToList();
        var anim0 = anim1 < 0 ? -1 : run.Samples.Where(s => s.Frame > anim1 && s.Watched[record].Animation == 0).Select(s => s.Frame).DefaultIfEmpty(-1).First();
        return (contact, anim1, moves.Count == 0 ? -1 : moves[0], moves.Count == 0 ? -1 : moves[^1], anim0, run.Samples[^1].Watched[record].Y);
    }

    // ---------------------------------------------------------------------------------------
    // The blocks of the lair of Nirude (map 40): F[2] of the block, 0x72 at @1153
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// ARC-40 (sm4): the block of the record 0 at (108, 840), touched from the north (the hero at (108, 818) holds Down) or from the east (the hero at (144, 840) holds Left), slides to the SOUTH whatever the side (the
    /// program F forces the direction 0, <c>3A 00</c>), by the remainder of the 10-tick wait of its program C: 5.0 px from the north (contact at the pad frame 5, the touch program F at 6, the slide on the frames 7 to 16, 0.5 px
    /// each, the animation back to 0 at 16), 5.5 px from the east (contact 7, F at 16, the slide on 17 to 27); touched from the south (the hero at (108, 862) holds Up) it does not move.
    /// </summary>
    [Fact]
    public void Map40_ABlockTouchedFromTheNorthOrTheEast_SlidesToTheSouth_AndTouchedFromTheSouthItDoesNotMove()
    {
        using (var north = new ScriptedPushRun("n40-north", Lair, LairWorld, Array.Empty<int>(), 108, 818, 32, 0))
        {
            north.Hold(ScriptedPushRun.Pad('D'), 50);

            Assert.Equal((5, 6, 7, 16, 16, 845.0), Slide(north, 0));
            Assert.Equal(108.0, north.Samples[^1].Watched[0].X);
            AssertNoneOfTheThreeIsSkipped(north);
        }

        using (var east = new ScriptedPushRun("n40-east", Lair, LairWorld, Array.Empty<int>(), 144, 840, 32, 0))
        {
            east.Hold(ScriptedPushRun.Pad('L'), 50);

            Assert.Equal((7, 16, 17, 27, 27, 845.5), Slide(east, 0));
            Assert.Equal(108.0, east.Samples[^1].Watched[0].X);
            AssertNoneOfTheThreeIsSkipped(east);
        }

        using (var south = new ScriptedPushRun("n40-south", Lair, LairWorld, Array.Empty<int>(), 108, 862, 32, 0))
        {
            south.Hold(ScriptedPushRun.Pad('U'), 40);

            Assert.Equal((6, 7, -1, -1, 16, 840.0), Slide(south, 0));
            AssertNoneOfTheThreeIsSkipped(south);
        }
    }

    /// <summary>
    /// ARC-40, the dependence on the instant of the contact (the discovery's <c>phase40_summary.txt</c>): the hero starts <c>k</c> pixels higher than (108, 818) and holds Down; the slide is what is left of the 10-tick wait of
    /// the block's program C when the touch program F ends, so it shortens by half a pixel every time the contact comes one frame later: (k, contact frame, half-pixels slid), the slide on the frames contact + 2 to 16.
    /// </summary>
    [Theory]
    [InlineData(0, 5, 10)]
    [InlineData(1, 6, 9)]
    [InlineData(2, 6, 9)]
    [InlineData(3, 7, 8)]
    [InlineData(4, 7, 8)]
    [InlineData(5, 8, 7)]
    [InlineData(6, 9, 6)]
    [InlineData(7, 9, 6)]
    [InlineData(8, 10, 5)]
    [InlineData(9, 11, 4)]
    [InlineData(10, 11, 4)]
    [InlineData(11, 12, 3)]
    public void Map40_TheSlideIsTheRestOfTheWaitOfTheBlock_ItDependsOnTheInstantOfTheContact(int k, int contactFrame, int halfPixels)
    {
        using var run = new ScriptedPushRun("n40-phase" + k, Lair, LairWorld, Array.Empty<int>(), 108, 818 - k, 32, 0);
        run.Hold(ScriptedPushRun.Pad('D'), 40);
        run.Release(40);

        Assert.Equal((contactFrame, contactFrame + 1, contactFrame + 2, 16, 16, 840.0 + halfPixels * 0.5), Slide(run, 0));
        AssertNoneOfTheThreeIsSkipped(run);
    }

    // ---------------------------------------------------------------------------------------
    // The buttons of Torla (map 426): C[13] to C[17], 0xAA [129] at @1484 ... @1752
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The right button of Torla (record 17 at (252, 824)), pushed from the north (the hero at (252, 790) holds Down for 130 frames, the flags 1253, 1254, 1267 and 1269 set as in the save <c>234-torla.sav</c>): the hero is blocked
    /// from the pad frame 12; the program C counts 40 consecutive <c>0xAA</c> that answer 1 (the <c>0x74</c> at @1757 on the frames 13 to 52, 52 <c>0xAA</c> in all), then <c>0x57</c> takes the north entry, the flag 1266 is raised
    /// at the frame 52, the button turns to the animation 1 at 53 and slides 0.5 px per frame on the frames 54 to 213 (160 frames, 80 px, from y 824 to 904), the animation back to 0 at 214; the bridge program B[6] reacts to the flag at
    /// 53 (the hero loses control, <c>0x10</c>) and places four tiles of bridge, one every 41 ticks (<c>0x85</c> at 53, 94, 135 and 176), then gives the control back (<c>0x11</c> at 176). The sound is asked again at the end of the slide by
    /// <c>0xBE</c> (degraded: the stop of the sound is E20.g5c), at the frame 215.
    /// </summary>
    [Fact]
    public void Map426_AFortyTickPushFromTheNorth_SlidesTheRightButtonBy80Pixels_RaisesItsFlag_AndBuildsTheBridge()
    {
        using var run = new ScriptedPushRun("b426-full", Inner, "Nirude (inner)-426", TorlaFlags, 252, 790, 32, 17);
        run.Hold(ScriptedPushRun.Pad('D'), 51);

        Assert.False(ScriptedPushRun.HasFlag(1266));
        run.Hold(ScriptedPushRun.Pad('D'), 1);

        Assert.True(ScriptedPushRun.HasFlag(1266));
        run.Hold(ScriptedPushRun.Pad('D'), 78);
        run.Release(180);

        Assert.Equal((12, 53, 54, 213, 214, 904.0), Slide(run, 17));
        Assert.Equal(252.0, run.Samples[^1].Watched[17].X);
        Assert.True(ScriptedPushRun.HasFlag(1266));
        Assert.Equal(Enumerable.Range(1, 52).ToList(), run.FramesOf(2, 1752, 0xAA));
        Assert.Equal(Enumerable.Range(13, 40).ToList(), run.FramesOf(2, 1757, 0x74));
        Assert.Equal(new[] { 52 }, run.FramesOf(2, 1760, 0x57));
        Assert.Equal(new[] { 52 }, run.FramesOf(2, 1770, 0x05));
        Assert.Equal(new[] { 53 }, run.FramesOf(1, 1105, 0x10));
        Assert.Equal(new[] { 53 }, run.FramesOf(1, 1110, 0x85));
        Assert.Equal(new[] { 94 }, run.FramesOf(1, 1119, 0x85));
        Assert.Equal(new[] { 135 }, run.FramesOf(1, 1128, 0x85));
        Assert.Equal(new[] { 176 }, run.FramesOf(1, 1137, 0x85));
        Assert.Equal(new[] { 176 }, run.FramesOf(1, 1148, 0x11));
        Assert.Equal(new[] { 215 }, run.FramesOf(2, 1787, 0xBE));
        AssertNoneOfTheThreeIsSkipped(run);
    }

    /// <summary>
    /// A push that lets go counts again from zero: Down held 30 frames, released 20, held 30, released 60 moves nothing and raises no flag (the contact is there, the 40 consecutive answers never come); the same push held on
    /// for 60 more frames after that makes the button slide, 40 consecutive ticks after its new start. A push from the west (the hero at (216, 824) holds Right) is on the wrong side: <c>0x57</c> sends the program back to its
    /// reload of the counter and nothing moves, whereas the push from the north on the same button does.
    /// </summary>
    [Fact]
    public void Map426_AnInterruptedPush_StartsOverFromZero_AndThePushFromTheWrongSideDoesNothing()
    {
        using (var interrupted = new ScriptedPushRun("b426-short", Inner, "Nirude (inner)-426", TorlaFlags, 252, 790, 32, 17))
        {
            interrupted.Hold(ScriptedPushRun.Pad('D'), 30);
            interrupted.Release(20);
            interrupted.Hold(ScriptedPushRun.Pad('D'), 30);
            interrupted.Release(60);

            Assert.Equal((12, -1, -1, -1, -1, 824.0), Slide(interrupted, 17));
            Assert.False(ScriptedPushRun.HasFlag(1266));

            interrupted.Hold(ScriptedPushRun.Pad('D'), 70);

            var slide = Slide(interrupted, 17);
            Assert.True(slide.Anim1 > 0 && slide.FirstMove == slide.Anim1 + 1, $"the button slides after the new push: {slide}");
            Assert.True(ScriptedPushRun.HasFlag(1266));
            AssertNoneOfTheThreeIsSkipped(interrupted);
        }

        using (var west = new ScriptedPushRun("b426-west", Inner, "Nirude (inner)-426", TorlaFlags, 216, 824, 32, 17))
        {
            west.Hold(ScriptedPushRun.Pad('R'), 130);
            west.Release(60);

            Assert.Equal((6, -1, -1, -1, -1, 824.0), Slide(west, 17));
            Assert.False(ScriptedPushRun.HasFlag(1266));
            Assert.Empty(west.FramesOf(2, 1770, 0x05));
            Assert.NotEmpty(west.FramesOf(2, 1757, 0x74));
            AssertNoneOfTheThreeIsSkipped(west);
        }
    }

    /// <summary>
    /// The buttons go in a chain from right to left: the second one (record 16 at (204, 824), the hero at (204, 790) holds Down for 90 frames) slides only when the flag of the first (1266) is up - it turns to the animation 1 at
    /// the pad frame 50 and slides from 51 (the right button, already slid, is at 904); without the flag it does not react though the hero is against it.
    /// </summary>
    [Fact]
    public void Map426_TheSecondButtonNeedsTheFlagOfTheFirst()
    {
        using (var with = new ScriptedPushRun("b426-chain", Inner, "Nirude (inner)-426", TorlaFlags.Append(1266).ToArray(), 204, 790, 32, 16, 17))
        {
            with.Hold(ScriptedPushRun.Pad('D'), 90);
            with.Release(20);

            var slide = Slide(with, 16);
            Assert.Equal((50, 51), (slide.Anim1, slide.FirstMove));
            Assert.Equal(824.0 + (110 - 50) * 0.5, with.At(110).Watched[16].Y);
            Assert.Equal(904.0, with.Samples[0].Watched[17].Y);
            AssertNoneOfTheThreeIsSkipped(with);
        }

        using (var without = new ScriptedPushRun("b426-chain-noflag", Inner, "Nirude (inner)-426", TorlaFlags, 204, 790, 32, 16))
        {
            without.Hold(ScriptedPushRun.Pad('D'), 90);
            without.Release(20);

            Assert.Equal((12, -1, -1, -1, -1, 824.0), Slide(without, 16));
            Assert.Empty(without.FramesOf(2, 1687, 0xAA)); // the program waits for the flag of the first button before it asks.
        }
    }

    // ---------------------------------------------------------------------------------------
    // A block of the 424 (E19.sm3, S3-A1b): C[3] @692, 0xAA [129] at @700
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// S3-A1b of E19.sm3 and the same from the south: the block of the record 3 of the map 424 at (912, 448) stands in a pit whose floor is z 16; the hero at (960, 448, 16) holds Left (it steps down to z 16, is blocked from the
    /// pad frame 7) or at (912, 472, 16) holds Up (blocked from the frame 2). The program C[3] counts 40 consecutive <c>0xAA</c> that answer 1 (the <c>0x74</c> at @705), then <c>0x57</c> at @708 takes the entry of the hero's
    /// direction; its table sends three of the four directions back to the reload of the counter (@697) and only a push towards the east (the hero's direction 0x18) goes on to the flag 0x04DB at @717: from the east or the
    /// south nothing is raised and the block does not move. (A push from the west, the push the script is made for, cannot be reached: the floor west of the block is z 48 and the hero stands above it.)
    /// </summary>
    [Fact]
    public void Map424_ABlockPushedFromTheEastOrTheSouth_CountsFortyAnswers_ThenIsSentBackToTheReload()
    {
        using (var east = new ScriptedPushRun("b424-east", Inner, "Nirude (inner)-424", Array.Empty<int>(), 960, 448, 16, 3))
        {
            east.Hold(ScriptedPushRun.Pad('L'), 120);
            east.Release(80);

            Assert.Equal(7, east.FirstFrame(s => s.BlockedBy == 3));
            Assert.Equal((946.0, 448.0, 16.0), (east.Samples[^1].HeroX, east.Samples[^1].HeroY, east.Samples[^1].HeroZ));
            Assert.Equal(Enumerable.Range(8, 40), east.FramesOf(2, 705, 0x74).Take(40));
            Assert.Equal(47, east.FramesOf(2, 708, 0x57).First());
            Assert.Empty(east.FramesOf(2, 717, 0x05));
            Assert.Equal(448.0, east.Samples[^1].Watched[3].Y);
            AssertNoneOfTheThreeIsSkipped(east);
        }

        using (var south = new ScriptedPushRun("b424-south", Inner, "Nirude (inner)-424", Array.Empty<int>(), 912, 472, 16, 3))
        {
            south.Hold(ScriptedPushRun.Pad('U'), 120);
            south.Release(80);

            Assert.Equal(2, south.FirstFrame(s => s.BlockedBy == 3));
            Assert.Equal(Enumerable.Range(3, 40), south.FramesOf(2, 705, 0x74).Take(40));
            Assert.Equal(42, south.FramesOf(2, 708, 0x57).First());
            Assert.Empty(south.FramesOf(2, 717, 0x05));
            Assert.Equal(448.0, south.Samples[^1].Watched[3].Y);
            AssertNoneOfTheThreeIsSkipped(south);
        }
    }
}
