#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Cameras;
using Alundra.Tests.Ai.Items;

namespace Alundra.Tests.Cameras;

/// <summary>
/// E20.h4c2 T1 (docs/plan-e20-heros.md section 1.31.4, D-H4C2-2, tests (k1)): the camera kick of E13 in the pure camera step <see cref="CameraMath.AdvanceCameraSmoothing"/>. The binary adds the kick, a byte written by E13 (<c>[0x800DD874] = 2</c>), to the scroll of
/// the step (<c>scrollY += ((targetY - (scrollY + 0x88)) &gt;&gt; 4) + OffsetY + kick</c>, <c>0x8002CDA0</c>), clears it at every step, and does not apply it at the first step after a load (the snap). It is NOT a bump that comes back: for a rest gap d the kick leaves a lasting shift
/// (d = 1: +1, d &gt;= 2: +2) and successive kicks add up until they use the gap up. The expected sequences below are the binary's formula in the scroll space (an independent model, <c>docs/plan-e20-annexe/e20-h4c2/t1/camera_kick_model.py</c>); the 100 vectors of the binary's
/// camera step (<c>vec_camera_8002CDA0.json</c>) are replayed, the 22 that read the kick in X (<c>[0x800DD870]</c>, no member in the DLL, nothing writes it: O-H4C2-1) being classed "binary only" and skipped by their flag (D-H4C2-5).
/// </summary>
public sealed class CameraKickTests
{
    private const int MapWidth = 1248;
    private const int MapHeight = 960;
    private const int ScrollX = 300;

    /// <summary>The render-space target for a scroll (the original's top-left of the 320 x 240 view): render X = scroll X + 160, render Y = -scroll Y - 120.</summary>
    private static Vector3 At(int scrollX, int scrollY) => new(scrollX + 160, -scrollY - 120, 0f);

    private static int Y(Vector3 state) => CameraMath.ToOriginalScrollSpace(state).Y;

    /// <summary>One <c>AdvanceCameraSmoothing</c> call per tick, the kick of the tick in its own list; the scroll Y after each tick.</summary>
    private static List<int> Run(int startY, int targetY, int[] kicks, bool snapFirst = false, bool withMap = true)
    {
        var state = At(ScrollX, startY);
        var ys = new List<int>();
        for (var i = 0; i < kicks.Length; i++)
        {
            state = CameraMath.AdvanceCameraSmoothing(state, snapFirst && i == 0, At(ScrollX, targetY), 1, withMap ? MapWidth : null, withMap ? MapHeight : null, null, new[] { kicks[i] });
            ys.Add(Y(state));
        }

        return ys;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The rest gaps: the kick is not a bump that returns.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(100, new[] { 102, 101, 100, 100, 100, 100 })] // d = 0: the kick comes back to the target.
    [InlineData(101, new[] { 102, 101, 101, 101, 101, 101 })] // d = 1: a lasting shift of +1 from the start.
    [InlineData(102, new[] { 102, 102, 102, 102, 102, 102 })] // d = 2: +2.
    [InlineData(115, new[] { 102, 102, 102, 102, 102, 102 })] // d = 15: +2 (the gap is in the dead zone of the shift by 4).
    [InlineData(116, new[] { 103, 103, 103, 103, 103, 103 })] // d = 16: the camera was already moving.
    [InlineData(99, new[] { 101, 100, 99, 99, 99, 99 })] // d = -1.
    public void K1_AKickOf2_LeavesALastingShiftWhichDependsOnTheGapAtRest(int targetY, int[] expected)
    {
        Assert.Equal(expected, Run(100, targetY, new[] { 2, 0, 0, 0, 0, 0 }));
    }

    [Fact]
    public void K1_NoKick_ChangesNothing_TheCameraAtRestStaysAtRest()
    {
        Assert.Equal(new[] { 100, 100, 100, 100 }, Run(100, 100, new[] { 0, 0, 0, 0 }));
        Assert.Equal(new[] { 101, 101, 101, 101 }, Run(101, 101, new[] { 0, 0, 0, 0 }));
    }

    [Fact]
    public void K1_SuccessiveKicks_AddUp_UntilTheyUseTheGapUp()
    {
        Assert.Equal(new[] { 102, 104, 106, 108, 110, 112, 114, 116, 117, 118 }, Run(100, 115, Enumerable.Repeat(2, 10).ToArray())); // d = 15, a kick a tick.
        Assert.Equal(new[] { 102, 104, 105, 104, 103, 102 }, Run(100, 102, new[] { 2, 2, 2, 0, 0, 0 })); // d = 2: it passes the target and comes back to +2.
        Assert.Equal(new[] { 102, 103, 102, 101, 101 }, Run(100, 101, new[] { 2, 2, 0, 0, 0 })); // d = 1.
        Assert.Equal(new[] { 102, 102, 104, 104, 106, 106, 108, 108, 110, 110, 112, 112, 114, 114, 114 }, Run(100, 115, new[] { 2, 0, 2, 0, 2, 0, 2, 0, 2, 0, 2, 0, 2, 0, 0 })); // a kick every other tick.
    }

    [Fact]
    public void K1_TheKickRaisesTheScrollOfTheOriginal_ItLowersTheRenderYOfTheTarget_AtTheSignOfTheSway()
    {
        var withKick = CameraMath.AdvanceCameraSmoothing(At(ScrollX, 100), false, At(ScrollX, 100), 1, MapWidth, MapHeight, null, new[] { 2 });
        var without = CameraMath.AdvanceCameraSmoothing(At(ScrollX, 100), false, At(ScrollX, 100), 1, MapWidth, MapHeight);

        Assert.Equal(without.Y - 2f, withKick.Y);
        Assert.Equal(without.X, withKick.X);

        // It adds to the offset of the sway, the same sign: with the same sway running, the kick moves the scroll by 2 more.
        var sway = new CameraSway();
        sway.Start(1, 1, 3, 2);
        var swayAlone = CameraMath.AdvanceCameraSmoothing(At(ScrollX, 100), false, At(ScrollX, 100), 1, MapWidth, MapHeight, Clone(sway));
        var swayAndKick = CameraMath.AdvanceCameraSmoothing(At(ScrollX, 100), false, At(ScrollX, 100), 1, MapWidth, MapHeight, Clone(sway), new[] { 2 });
        Assert.Equal(swayAlone.Y - 2f, swayAndKick.Y);
    }

    private static CameraSway Clone(CameraSway s) => new() { Flag = s.Flag, LimitX = s.LimitX, LimitY = s.LimitY, SpeedX = s.SpeedX, SpeedY = s.SpeedY, OffsetX = s.OffsetX, OffsetY = s.OffsetY, ReachX = s.ReachX, ReachY = s.ReachY };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The frames of several ticks, the snap tick, the bounds, no list.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 0, 0, 121)] // the binary's formula in the scroll space (the model of the annex): 109, 115, 121.
    [InlineData(0, 2, 0, 122)] // 107, 116, 122.
    [InlineData(0, 0, 2, 122)] // 107, 114, 122.
    [InlineData(2, 3, 4, 128)] // 109, 118, 128.
    [InlineData(2, 0, 2, 123)] // 109, 115, 123.
    [InlineData(0, 0, 0, 120)] // 107, 114, 120: no kick.
    public void K1_AFrameOfThreeTicks_AppliesTheKickOfEachTickAtItsOwnStep_AsThreeFramesOfOneTick(int a, int b, int c, int expectedY)
    {
        var start = At(ScrollX, 100);
        var target = At(ScrollX, 220);

        var frame = CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { a, b, c });

        var state = start;
        foreach (var kick in new[] { a, b, c })
        {
            state = CameraMath.AdvanceCameraSmoothing(state, false, target, 1, MapWidth, MapHeight, null, new[] { kick });
        }

        Assert.Equal(state, frame);
        Assert.Equal(expectedY, Y(frame));
    }

    [Fact]
    public void K1_ThePositionOfTheKickInTheListIsTheTick_AKickAtTheFirstStepOfAFrameOfThreeIsNotTheKickAtTheLast()
    {
        var start = At(ScrollX, 100);
        var target = At(ScrollX, 100);

        var first = CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { 2, 0, 0 });
        var last = CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { 0, 0, 2 });

        Assert.Equal(100, Y(first)); // 102, 101, 100: the kick of the first step comes back.
        Assert.Equal(102, Y(last)); // 100, 100, 102.
    }

    [Fact]
    public void K1_ASnapFrame_LosesTheKickOfItsFirstTick_AndAppliesTheOthers()
    {
        var start = At(ScrollX, 100);
        var target = At(ScrollX, 220);

        // The snap tick (the first of the frame) is not a follow step: its kick is cleared and not applied.
        var snapAlone = CameraMath.AdvanceCameraSmoothing(start, true, target, 1, MapWidth, MapHeight, null, new[] { 5 });
        Assert.Equal(CameraMath.AdvanceCameraSmoothing(start, true, target, 1, MapWidth, MapHeight), snapAlone);
        Assert.Equal(220, Y(snapAlone));

        var frame = CameraMath.AdvanceCameraSmoothing(start, true, target, 3, MapWidth, MapHeight, null, new[] { 5, 2, 3 });
        var expected = CameraMath.AdvanceCameraSmoothing(snapAlone, false, target, 1, MapWidth, MapHeight, null, new[] { 2 });
        expected = CameraMath.AdvanceCameraSmoothing(expected, false, target, 1, MapWidth, MapHeight, null, new[] { 3 });
        Assert.Equal(expected, frame);
        Assert.Equal(frame, CameraMath.AdvanceCameraSmoothing(start, true, target, 3, MapWidth, MapHeight, null, new[] { 0, 2, 3 })); // the first kick is lost, whatever it is.
        Assert.Equal(224, Y(frame)); // 220 (the snap), 222, 224: the kicks of the next two ticks only.
    }

    [Fact]
    public void K1_AShortListIsZeroForTheTicksItDoesNotCover_ALongListIsCutAtTheNumberOfTicks_ZeroTicksLeaveTheStateAlone()
    {
        var start = At(ScrollX, 100);
        var target = At(ScrollX, 220);

        Assert.Equal(CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { 2, 0, 0 }), CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { 2 }));
        Assert.Equal(121, Y(CameraMath.AdvanceCameraSmoothing(start, false, target, 3, MapWidth, MapHeight, null, new[] { 2 }))); // 109, 115, 121.
        Assert.Equal(CameraMath.AdvanceCameraSmoothing(start, false, target, 1, MapWidth, MapHeight, null, new[] { 2 }), CameraMath.AdvanceCameraSmoothing(start, false, target, 1, MapWidth, MapHeight, null, new[] { 2, 9, 9 }));
        Assert.Equal(start, CameraMath.AdvanceCameraSmoothing(start, false, target, 0, MapWidth, MapHeight, null, new[] { 7, 7 }));
    }

    [Fact]
    public void K1_TheMapBoundsClampTheKick_AtTheBottomAndAtTheTop()
    {
        // The scroll Y of the original goes from 0 to 719 (map 1248 x 960): a kick at the bounds is clamped, as the binary's clamp after the step.
        Assert.Equal(new[] { 719, 719 }, Run(719, 719, new[] { 2, 0 }));
        Assert.Equal(new[] { 0, 0 }, Run(0, 0, new[] { -2, 0 }));
        Assert.Equal(new[] { 719, 719 }, Run(718, 719, new[] { 2, 0 })); // one pixel from the bound: 720 clamped to 719.
        Assert.Equal(new[] { 802, 801 }, Run(800, 800, new[] { 2, 0 }, withMap: false)); // without a map, nothing clamps (the gap -2 then pulls back by one).
    }

    [Fact]
    public void K1_WithoutAListOrWithZeros_TheStepIsToday_ForEveryGapAndEverySway()
    {
        foreach (var targetY in new[] { 0, 5, 100, 140, 400, 719 })
        {
            foreach (var withSway in new[] { false, true })
            {
                var swayA = new CameraSway();
                var swayB = new CameraSway();
                var swayC = new CameraSway();
                var swayD = new CameraSway();
                if (withSway)
                {
                    foreach (var s in new[] { swayA, swayB, swayC, swayD })
                    {
                        s.Start(1, 2, 3, 4);
                    }
                }

                var start = At(ScrollX, 100);
                var target = At(ScrollX, targetY);
                var today = CameraMath.AdvanceCameraSmoothing(start, false, target, 5, MapWidth, MapHeight, withSway ? swayA : null);
                Assert.Equal(today, CameraMath.AdvanceCameraSmoothing(start, false, target, 5, MapWidth, MapHeight, withSway ? swayB : null, null));
                Assert.Equal(today, CameraMath.AdvanceCameraSmoothing(start, false, target, 5, MapWidth, MapHeight, withSway ? swayC : null, Array.Empty<int>()));
                Assert.Equal(today, CameraMath.AdvanceCameraSmoothing(start, false, target, 5, MapWidth, MapHeight, withSway ? swayD : null, new[] { 0, 0, 0, 0, 0 }));
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The 100 vectors of the binary's camera step.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const long FirstCall = 0x800E42B8;
    private const long SwayBlock = 0x800E4338;
    private const long ScrollXWord = 0x800E4328;
    private const long ScrollYWord = 0x800E432C;
    private const long KickXWord = 0x800DD870;
    private const long KickYWord = 0x800DD874;

    /// <summary>The kick in X is "binary only": the DLL has no member for it and nothing in the game writes it (a single store of 0 clears it).</summary>
    private static bool BinaryOnly(EarthTome2Vectors.Vector v) => v.ReadInt(KickXWord) != 0;

    [Fact]
    public void K1_TheCameraVectorsOfTheBinary_AreReplayed_TheTwentyTwoThatReadTheKickInXAreBinaryOnlyAndSkipped()
    {
        var vectors = EarthTome2Vectors.Vectors("vec_camera_8002CDA0.json");

        Assert.Equal(100, vectors.Count);
        var skipped = vectors.Where(BinaryOnly).ToList();
        var compared = vectors.Where(v => !BinaryOnly(v)).ToList();
        Assert.Equal(22, skipped.Count);
        Assert.Equal(78, compared.Count);
        Assert.Equal(new[] { 3, 4, 8, 12, 16, 24, 32, 34, 39, 40, 44, 45, 47, 48, 57, 64, 69, 72, 74, 83, 87, 91 }, skipped.Select(v => v.Id));

        foreach (var v in compared)
        {
            var sway = new CameraSway
            {
                Flag = v.ReadInt(SwayBlock), LimitX = v.ReadInt(SwayBlock + 4), LimitY = v.ReadInt(SwayBlock + 8), SpeedX = v.ReadInt(SwayBlock + 12), SpeedY = v.ReadInt(SwayBlock + 16),
                OffsetX = v.ReadInt(SwayBlock + 20), OffsetY = v.ReadInt(SwayBlock + 24), ReachX = v.ReadInt(SwayBlock + 28), ReachY = v.ReadInt(SwayBlock + 32),
            };
            var snap = v.Read(FirstCall) != 0;
            var look = CameraMath.ComputeCameraLookAtRenderPosition((int)v.Args[1], (int)v.Args[2], (int)v.Args[3]);

            var state = CameraMath.AdvanceCameraSmoothing(At(v.ReadInt(ScrollXWord), v.ReadInt(ScrollYWord)), snap, look, 1, MapWidth, MapHeight, sway, new[] { v.ReadInt(KickYWord) });

            var label = $"vector {v.Id}";
            var (x, y) = CameraMath.ToOriginalScrollSpace(state);
            Assert.True((v.WroteInt(ScrollXWord), v.WroteInt(ScrollYWord)) == (x, y), $"{label}: scroll ({x}, {y}), binary ({v.WroteInt(ScrollXWord)}, {v.WroteInt(ScrollYWord)})");
            int After(int offset) => v.Wrote(SwayBlock + offset) ? v.WroteInt(SwayBlock + offset) : v.ReadInt(SwayBlock + offset); // a word the step did not write keeps the value it read.
            Assert.True((After(0), After(4), After(8), After(12), After(16), After(20), After(24), After(28), After(32))
                == (sway.Flag, sway.LimitX, sway.LimitY, sway.SpeedX, sway.SpeedY, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY), $"{label}: the sway block after the step is not the binary's");
        }

        // Both sides of the fronts are in the compared sample: a kick and none, a snap and none, a sway running and not.
        Assert.Contains(compared, v => v.ReadInt(KickYWord) != 0 && v.Read(FirstCall) == 0);
        Assert.Contains(compared, v => v.ReadInt(KickYWord) == 0);
        Assert.Contains(compared, v => v.Read(FirstCall) != 0);
        Assert.Contains(compared, v => v.ReadInt(SwayBlock) != 0);
    }
}
