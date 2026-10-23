#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5d T1, (s1) to (s4) and (s6) (docs/plan-e20-heros.md section 1.24.6, D-G5D-1 to D-G5D-4): the scripted pushable objects on the real maps, through <see cref="ScriptedPushRun"/> (real controller, real prefabs, the
/// map's own programs). No production code is behind them: the event runner already plays these scripts, the tests pin what the DLL does (D-G5D-1: the binary does not run map scripts on the machine, so it has no
/// reference value for them). The values are the discovery's probes, replayed on the start HEAD in T0 (<c>docs/plan-e20-annexe/e20-g5/g5d/README.md</c>). The pad frame <c>n</c> is the <c>n</c>-th frame a direction is
/// held after the hero is placed. Each test holds its positive effect beside the case that would make it red (no push, no hit).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ScriptedPushObjectsTests
{
    private static readonly int[] NoFlags = Array.Empty<int>();

    /// <summary>The flag a statue's or a block's program sets once it walks: the number the scripts read (low byte, then the bank in the high byte).</summary>
    private static uint Flag(int low, int high) => (uint)(low | (high << 8));

    private static (int Contact, int Anim1, int FirstMove, int LastMove, int Anim0, double EndX) Slide(ScriptedPushRun run, int record, int animWhileMoving)
    {
        var contact = run.FirstFrame(s => s.BlockedBy == record);
        var anim = run.FirstFrame(s => s.Watched[record].Animation == (uint)animWhileMoving);
        var moves = run.Samples.Skip(1).Where((s, i) => s.Watched[record].X != run.Samples[i].Watched[record].X).Select(s => s.Frame).ToList();
        var anim0 = anim < 0 ? -1 : run.Samples.Where(s => s.Frame > anim && s.Watched[record].Animation == 0).Select(s => s.Frame).DefaultIfEmpty(-1).First();
        return (contact, anim, moves.Count == 0 ? -1 : moves[0], moves.Count == 0 ? -1 : moves[^1], anim0, run.Samples[^1].Watched[record].X);
    }

    private static void AssertNothingSkipped(ScriptedPushRun run) => Assert.Empty(run.Arc.SkippedOrExceeded);

    // ---------------------------------------------------------------------------------------
    // (s1) The statue of the map 2: record 9, programs A[10], C[10], F[10]
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// (s1) The statue of the map 2 (record 9 at (420, 392, 208)), pushed by the hero from the west (at (396, 392, 208) holding Right): the hero is blocked by it from the pad frame 2; the program F (<c>0x05</c> at @1392) raises
    /// the transient flag 0x8064 on the frame 3; the program C (@780) takes it on the frame 4 (<c>0x06</c>, the zone test <c>0x3B</c>, then <c>0x05</c> of the flag 889 at @794), sets the animation 1 and walks: 17 steps of
    /// 1.5 px on the frames 5 to 21, x from 420 to 445.5 (the script asks for 24 px; the last step overshoots), the animation back to 0 on the frame 21. The hero stays at 399. Without the push (the hero at 380, nothing held) nothing moves
    /// for 60 frames, the flag 889 is not raised and the program F does not run.
    /// </summary>
    [Fact]
    public void Map2_AStatuePushedFromTheWest_WalksSeventeenStepsOfOnePointFivePixels_AndRaisesItsFlag_WithoutThePushNothingMoves()
    {
        using (var pushed = new ScriptedPushRun("s1-pushed", "Overworld", "Overworld 0,1-2", NoFlags, 396, 392, 208, 9))
        {
            pushed.Hold(ScriptedPushRun.Pad('R'), 60);

            Assert.Equal((2, 4, 5, 21, 21, 445.5), Slide(pushed, 9, 1));
            Assert.Equal(new[] { 3 }, pushed.FramesOf(5, 1392, 0x05));
            Assert.Equal(new[] { 4 }, pushed.FramesOf(2, 794, 0x05));
            Assert.Equal(420.0, pushed.At(4).Watched[9].X);
            for (var frame = 5; frame <= 21; frame++)
            {
                Assert.Equal(420.0 + 1.5 * (frame - 4), pushed.At(frame).Watched[9].X);
            }

            Assert.Equal(392.0, pushed.Samples[^1].Watched[9].Y);
            Assert.True(ScriptedPushRun.HasFlag(Flag(121, 3)));
            Assert.False(ScriptedPushRun.HasFlag(Flag(100, 128))); // the transient flag was taken by the program C.
            Assert.Equal(399.0, pushed.Samples[^1].HeroX);
            AssertNothingSkipped(pushed);
        }

        using (var idle = new ScriptedPushRun("s1-idle", "Overworld", "Overworld 0,1-2", NoFlags, 380, 392, 208, 9))
        {
            idle.Release(60);

            Assert.Equal((-1, -1, -1, -1, -1, 420.0), Slide(idle, 9, 1));
            Assert.Empty(idle.FramesOf(5, 1392, 0x05));
            Assert.False(ScriptedPushRun.HasFlag(Flag(121, 3)));
            AssertNothingSkipped(idle);
        }
    }

    // ---------------------------------------------------------------------------------------
    // (s2) The statue of the map 352: record 8, programs A[9], C[9], F[9]
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// (s2) The statue of the map 352 (record 8 at (204, 808, 32)), pushed from the west by the hero (at (168, 808, 32) holds Right 50 frames, idles 3, holds Right again): the hero walks to the statue and is blocked from the
    /// frame 7; the program F (@1900) raises the flag on the frame 8; the program C (@972) takes it, tests the zone (<c>0x3B</c>), the ground (<c>0x70</c>) and the Right key of the cross (<c>0x2F</c>), and walks: the animation 1 on the
    /// frame 9, 17 steps of 1.5 px on the frames 10 to 26, x from 204 to 229.5, the animation back to 0 on the frame 26, then the flag 230 + (3 &lt;&lt; 8). Without the push (the hero idle at 168) nothing moves and no flag is raised.
    /// </summary>
    [Fact]
    public void Map352_AStatuePushedFromTheWestWithTheCrossHeld_WalksTo229Point5_AndRaisesItsFlag_WithoutThePushNothingMoves()
    {
        using (var pushed = new ScriptedPushRun("s2-pushed", "Lake Shrine", "Lake Shrine (inner)-352", NoFlags, 168, 808, 32, 8))
        {
            pushed.Hold(ScriptedPushRun.Pad('R'), 50);
            pushed.Release(3);
            pushed.Hold(ScriptedPushRun.Pad('R'), 120);

            Assert.Equal((7, 9, 10, 26, 26, 229.5), Slide(pushed, 8, 1));
            Assert.Equal(new[] { 8, 65 }, pushed.FramesOf(5, 1900, 0x05)); // the second at 65: the hero, who followed the statue after the release, touches it again.
            Assert.Equal(204.0, pushed.At(9).Watched[8].X);
            for (var frame = 10; frame <= 26; frame++)
            {
                Assert.Equal(204.0 + 1.5 * (frame - 9), pushed.At(frame).Watched[8].X);
            }

            Assert.Equal(808.0, pushed.Samples[^1].Watched[8].Y);
            Assert.True(ScriptedPushRun.HasFlag(Flag(230, 3)));
            AssertNothingSkipped(pushed);
        }

        using (var idle = new ScriptedPushRun("s2-idle", "Lake Shrine", "Lake Shrine (inner)-352", NoFlags, 168, 808, 32, 8))
        {
            idle.Release(120);

            Assert.Equal((-1, -1, -1, -1, -1, 204.0), Slide(idle, 8, 1));
            Assert.Empty(idle.FramesOf(5, 1900, 0x05));
            Assert.False(ScriptedPushRun.HasFlag(Flag(230, 3)));
            AssertNothingSkipped(idle);
        }
    }

    // ---------------------------------------------------------------------------------------
    // (s3) The block of the map 23: record 12, programs A[2], C[11], F[1]
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// (s3) The block of the map 23 (record 12 at (768, 832, 32)), pushed from the east (the hero at (800, 832, 32) holds Left): he is blocked from the frame 1, the program F (@1336) raises the flag 20 + (128 &lt;&lt; 8) on the
    /// frame 2, the program C (@908) sets the animation 3 on the frame 3 and walks 24 steps of 2.0625 px on the frames 4 to 27 (x from 768 to 720), the animation back to 0 on the frame 28; it then falls from z 32 to z 16 in the
    /// frames 28 to 35 (31.5, 30.5, 29, 27, 24.5, 21.5, 18, 16: on the ground again at the frame 35) and raises the flag 133. The same push after 30 idle frames starts 30 frames later and goes the same way. Without the push the block
    /// stays.
    /// </summary>
    [Fact]
    public void Map23_ABlockPushedFromTheEast_SlidesFortyEightPixelsWest_ThenFallsOneFloor_AndTheContactInstantOnlyShiftsIt_WithoutThePushItStays()
    {
        var fall = new List<double> { 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 16 };
        using (var pushed = new ScriptedPushRun("s3-pushed", "Lars' Crypt", "Lars' Crypt-23", NoFlags, 800, 832, 32, 12))
        {
            pushed.Hold(ScriptedPushRun.Pad('L'), 26);

            var block = pushed.Record(12);
            var heights = new List<double>();
            var onGround = new List<bool>();
            for (var frame = 27; frame <= 36; frame++)
            {
                pushed.Hold(ScriptedPushRun.Pad('L'), 1);
                heights.Add(block.PosZ / 65536.0);
                onGround.Add(block.IsOnGround != 0);
            }

            pushed.Hold(ScriptedPushRun.Pad('L'), 24);

            Assert.Equal((1, 3, 4, 27, 28, 720.0), Slide(pushed, 12, 3));
            Assert.Equal(new[] { 2 }, pushed.FramesOf(5, 1336, 0x05));
            Assert.Equal(768.0, pushed.At(3).Watched[12].X);
            for (var frame = 4; frame <= 26; frame++)
            {
                Assert.Equal(768.0 - 2.0625 * (frame - 3), pushed.At(frame).Watched[12].X);
            }

            Assert.Equal(832.0, pushed.Samples[^1].Watched[12].Y);
            Assert.Equal(new[] { 32.0 }.Concat(fall).Concat(new[] { 16.0 }), heights); // frame 27 still at z 32, the eight steps of the fall, then at rest.
            Assert.Equal(new[] { false, false, false, false, false, false, false, false, true, true }, onGround); // in the air from the frame 27 to 34, on the ground from 35.
            Assert.True(ScriptedPushRun.HasFlag(Flag(133, 0)));
            Assert.True(ScriptedPushRun.HasFlag(Flag(20, 128)));
            AssertNothingSkipped(pushed);
        }

        using (var waited = new ScriptedPushRun("s3-waited", "Lars' Crypt", "Lars' Crypt-23", NoFlags, 800, 832, 32, 12))
        {
            waited.Release(30);
            waited.Hold(ScriptedPushRun.Pad('L'), 60);

            Assert.Equal((31, 33, 34, 57, 58, 720.0), Slide(waited, 12, 3));
            Assert.Equal(new[] { 32 }, waited.FramesOf(5, 1336, 0x05));
            Assert.Equal(16.0, waited.Record(12).PosZ / 65536.0);
            AssertNothingSkipped(waited);
        }

        using (var idle = new ScriptedPushRun("s3-idle", "Lars' Crypt", "Lars' Crypt-23", NoFlags, 850, 832, 32, 12))
        {
            idle.Release(60);

            Assert.Equal((-1, -1, -1, -1, -1, 768.0), Slide(idle, 12, 3));
            Assert.Equal(32.0, idle.Record(12).PosZ / 65536.0);
            Assert.False(ScriptedPushRun.HasFlag(Flag(133, 0)));
            Assert.Empty(idle.FramesOf(5, 1336, 0x05));
            AssertNothingSkipped(idle);
        }
    }

    // ---------------------------------------------------------------------------------------
    // (s4) The freeze of the hero after a program F (Q-G5-A2, D-G5D-3)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// (s4) The start of a program F zeroes the four force words of the hero (<c>0x80042150</c> in the binary: <c>+0xD8</c>, <c>+0xD4</c>, <c>+0xC8</c>, <c>+0xC4</c>), and the DLL does the same: on the statue of the map 2, the hero who holds
    /// Right after the contact (frame 2, at x 399) does not advance for the 38 frames Right is still held (frames 3 to 40), the statue having stopped at the frame 21; released for 5 frames then held again from the frame 46, he
    /// leaves at once (400.21875 at the frame 46, 402.65625 at 47, then 2.4375 px per frame), walks to 424.5 (21 px from the statue, as at the first contact) and stays there. The freeze is pinned as it is (D-G5D-3, Q-G5-A2): if the
    /// author asks to correct it, this test moves with the correction.
    /// </summary>
    [Fact]
    public void Map2_TheHeroWhoKeepsPushingAfterTheContactIsFrozen_UntilHeReleasesAndPressesAgain()
    {
        using var run = new ScriptedPushRun("s4-freeze", "Overworld", "Overworld 0,1-2", NoFlags, 396, 392, 208, 9);
        run.Hold(ScriptedPushRun.Pad('R'), 40);
        run.Release(5);
        run.Hold(ScriptedPushRun.Pad('R'), 30);

        Assert.Equal(2, run.FirstFrame(s => s.BlockedBy == 9));
        for (var frame = 2; frame <= 45; frame++)
        {
            Assert.Equal(399.0, run.At(frame).HeroX); // frozen while Right is held, and idle while it is not.
        }

        Assert.Equal(445.5, run.At(40).Watched[9].X); // the statue is done long before the end of the first hold.
        Assert.Equal(400.21875, run.At(46).HeroX);
        Assert.Equal(402.65625, run.At(47).HeroX);
        Assert.Equal(405.09375, run.At(48).HeroX);
        Assert.Equal(424.5, run.At(66).HeroX);
        Assert.Equal(424.5, run.At(75).HeroX);
        Assert.Equal(Enumerable.Range(46, 20).Select(f => run.At(f).HeroX).OrderBy(x => x), Enumerable.Range(46, 20).Select(f => run.At(f).HeroX)); // he only goes forward.
        AssertNothingSkipped(run);
    }

    // ---------------------------------------------------------------------------------------
    // (s6) The blocks hit by an attack of index 1: maps 102 and 382 (D-G5D-4, the montage of the hit pass)
    // ---------------------------------------------------------------------------------------

    private readonly record struct Pose(int Frame, double X, double Y, double Z, uint Animation, bool ClassA);

    /// <summary>
    /// The montage: a made-up attacker (a frame box and an attack of attribute 0x02, whose nibble is the one the type index 1 of <c>0x87</c> asks for) is written into the block's <c>TouchingEntity</c> by the test seam of the hit
    /// pass for ONE tick (the production counters pass clears it after the programs), as the real hit pass does; the block's program D then runs once, on the next tick. Five idle frames first, the hit on the frame 6, the program D on
    /// the frame 7. Returns the pose of the block after each of the <paramref name="frames"/> following frames.
    /// </summary>
    private static List<Pose> Hit(ScriptedPushRun run, int record, bool hit, int frames = 300)
    {
        var block = run.Record(record);
        var attackerEntity = new Entity { Name = "attacker", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        attackerEntity.Initialize();
        var attacker = (EntityScriptProxy)attackerEntity.GameplayProxy;
        attacker.CurrentAttack = new BalanceAttack(0x02, 1);
        attacker.HasFrameBox = true;
        var fire = false;
        run.Arc.Proxy.HitPassHookForTests = () =>
        {
            if (fire)
            {
                block.TouchingEntity = attacker.SelfEntity;
                fire = false;
            }
        };

        run.Release(5);
        fire = hit;
        var poses = new List<Pose>();
        for (var i = 0; i < frames; i++)
        {
            run.Release(1);
            poses.Add(new Pose(run.Frame, block.PosX / 65536.0, block.PosY / 65536.0, block.PosZ / 65536.0, block.TargetAnimationId, (block.Flags & EntityFlags.ClassA) != 0));
        }

        return poses;
    }

    private static int FirstChange(List<Pose> poses, Func<Pose, double> axis)
        => poses.Skip(1).Where((p, i) => axis(p) != axis(poses[i])).Select(p => p.Frame).DefaultIfEmpty(-1).First();

    private static int LastChange(List<Pose> poses, Func<Pose, double> axis)
        => poses.Skip(1).Where((p, i) => axis(p) != axis(poses[i])).Select(p => p.Frame).DefaultIfEmpty(-1).Last();

    /// <summary>
    /// (s6) The blocks of the Nightmare of Giles (map 102, records 0 at (360, 792, 64) and 9 at (600, 792, 64)): the program D of each is <c>0x87 [128, 1]</c> then, if touched, clears <c>ClassA</c> and raises the flag 10 + (128 &lt;&lt; 8),
    /// which the program C waits for; C then looks where the hero stands. The hit lands on the frame 6, D runs on the frame 7 (<c>ClassA</c> cleared from the frame 7). The block of the record 0 with the hero in the east zone
    /// (456, 792, 64) turns to the animation 2 on the frame 8, walks west on the frames 9 to 41 (33 steps of 1.5 px, x from 360 to 310.5) and falls from z 64 to z 0 on the frames 41 to 56; with the hero in the north zone
    /// (372, 744, 64) it walks south on the frames 9 to 81 (y from 792 to 865) and falls to z 32 on the frames 81 to 91; with the hero in no zone (456, 700) D runs, C sets <c>ClassA</c> again on the frame 8 and nothing moves. The
    /// block of the record 9 with the hero at (456, 792, 64) walks east, x from 600 to 649.5. Without the hit nothing happens.
    /// </summary>
    [Fact]
    public void Map102_ABlockHitByAnAttackOfIndexOne_WalksAccordingToTheHeroZone_AndWithoutTheHitNothingHappens()
    {
        const string zone = "Giles Nightmare";
        const string world = "Giles Nightmare-102";

        using (var east = new ScriptedPushRun("s6-102-east", zone, world, NoFlags, 456, 792, 64, 0))
        {
            var poses = Hit(east, 0, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.DoesNotContain(poses.Where(p => p.Frame >= 7), p => p.ClassA); // never set again within 300 frames.
            Assert.Equal(8, poses.First(p => p.Animation == 2).Frame);
            Assert.Equal((9, 41), (FirstChange(poses, p => p.X), LastChange(poses, p => p.X)));
            Assert.Equal(-1, FirstChange(poses, p => p.Y));
            Assert.Equal((41, 56), (FirstChange(poses, p => p.Z), LastChange(poses, p => p.Z)));
            Assert.Equal(360.0, poses.First(p => p.Frame == 8).X);
            Assert.Equal(358.5, poses.First(p => p.Frame == 9).X);
            Assert.Equal(357.0, poses.First(p => p.Frame == 10).X);
            Assert.Equal((310.5, 792.0, 0.0), (poses[^1].X, poses[^1].Y, poses[^1].Z));
            AssertNothingSkipped(east);
        }

        using (var north = new ScriptedPushRun("s6-102-north", zone, world, NoFlags, 372, 744, 64, 0))
        {
            var poses = Hit(north, 0, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.Equal(-1, FirstChange(poses, p => p.X));
            Assert.Equal((9, 81), (FirstChange(poses, p => p.Y), LastChange(poses, p => p.Y)));
            Assert.Equal((81, 91), (FirstChange(poses, p => p.Z), LastChange(poses, p => p.Z)));
            Assert.Equal((360.0, 865.0, 32.0), (poses[^1].X, poses[^1].Y, poses[^1].Z));
            AssertNothingSkipped(north);
        }

        using (var none = new ScriptedPushRun("s6-102-none", zone, world, NoFlags, 456, 700, 64, 0))
        {
            var poses = Hit(none, 0, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.Equal(8, poses.First(p => p.Frame > 7 && p.ClassA).Frame); // C sets it again at once.
            Assert.All(poses, p => Assert.Equal((360.0, 792.0, 64.0), (p.X, p.Y, p.Z)));
            AssertNothingSkipped(none);
        }

        using (var other = new ScriptedPushRun("s6-102-other", zone, world, NoFlags, 456, 792, 64, 9))
        {
            var poses = Hit(other, 9, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.Equal((9, 41), (FirstChange(poses, p => p.X), LastChange(poses, p => p.X)));
            Assert.Equal((649.5, 792.0, 0.0), (poses[^1].X, poses[^1].Y, poses[^1].Z));
            AssertNothingSkipped(other);
        }

        using (var unhit = new ScriptedPushRun("s6-102-unhit", zone, world, NoFlags, 456, 792, 64, 0))
        {
            var poses = Hit(unhit, 0, hit: false);

            Assert.All(poses, p => Assert.True(p.ClassA));
            Assert.All(poses, p => Assert.Equal((360.0, 792.0, 64.0), (p.X, p.Y, p.Z)));
            AssertNothingSkipped(unhit);
        }
    }

    /// <summary>
    /// (s6) The block of the Murgg caves (map 382, record 7 at (720, 704, 48)): the program D is the same (<c>0x87 [128, 1]</c>, <c>ClassA</c> cleared, the flag 80 + (128 &lt;&lt; 8)), the program C then tests the zone
    /// (tiles 29-30 x 40-41): with the hero inside, at (710, 655, 48), the block turns to the animation 2 on the frame 8 and slides south on the frames 9 to 41 (y from 704 to 737, one pixel per frame), then raises the flag 79 + (2 &lt;&lt; 8);
    /// with the hero outside, at (696, 700, 80), D runs, C sets <c>ClassA</c> again on the frame 8 and nothing moves. Without the hit nothing happens.
    /// </summary>
    [Fact]
    public void Map382_ABlockHitByAnAttackOfIndexOne_SlidesSouthWithTheHeroInTheZone_AndWithoutTheHitOrOutsideTheZoneItStays()
    {
        const string zone = "Murgg Caves";
        const string world = "Murgg Caves-382";

        using (var inside = new ScriptedPushRun("s6-382-in", zone, world, NoFlags, 710, 655, 48, 7))
        {
            var poses = Hit(inside, 7, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.DoesNotContain(poses.Where(p => p.Frame >= 7), p => p.ClassA);
            Assert.Equal(8, poses.First(p => p.Animation == 2).Frame);
            Assert.Equal(-1, FirstChange(poses, p => p.X));
            Assert.Equal((9, 41), (FirstChange(poses, p => p.Y), LastChange(poses, p => p.Y)));
            Assert.Equal(705.0, poses.First(p => p.Frame == 9).Y);
            Assert.Equal(706.0, poses.First(p => p.Frame == 10).Y);
            Assert.Equal((720.0, 737.0, 48.0), (poses[^1].X, poses[^1].Y, poses[^1].Z));
            Assert.True(ScriptedPushRun.HasFlag(Flag(79, 2)));
            AssertNothingSkipped(inside);
        }

        using (var outside = new ScriptedPushRun("s6-382-out", zone, world, NoFlags, 696, 700, 96, 7))
        {
            var poses = Hit(outside, 7, hit: true);

            Assert.Equal(7, poses.First(p => !p.ClassA).Frame);
            Assert.Equal(8, poses.First(p => p.Frame > 7 && p.ClassA).Frame);
            Assert.All(poses, p => Assert.Equal((720.0, 704.0, 48.0), (p.X, p.Y, p.Z)));
            Assert.False(ScriptedPushRun.HasFlag(Flag(79, 2)));
            AssertNothingSkipped(outside);
        }

        using (var unhit = new ScriptedPushRun("s6-382-unhit", zone, world, NoFlags, 710, 655, 48, 7))
        {
            var poses = Hit(unhit, 7, hit: false);

            Assert.All(poses, p => Assert.True(p.ClassA));
            Assert.All(poses, p => Assert.Equal((720.0, 704.0, 48.0), (p.X, p.Y, p.Z)));
            Assert.False(ScriptedPushRun.HasFlag(Flag(79, 2)));
            AssertNothingSkipped(unhit);
        }
    }
}
