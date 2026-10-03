#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 A12 (docs/plan-e19-opcodes.md section 1.2h.3.1): map 179, B[2] (program @328), the first scripted jump of the day 3 chain -
/// Bergus (record 8) is told to jump (<c>1A [2] @411</c>, an animation of impulse), waits for the animation (<c>37 [3] @413</c>) and for the landing
/// (<c>0x25 @415</c>), then comes back (<c>1A [0] @416</c>); the boxes then close and <c>0x53 @451</c> leaves the map. The arc starts like the
/// preset <c>day3-after-dream</c> (G203, G1651, G1660, arrival at the tile (17, 7), z 1).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraBergusJumpArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private static ArcSpec A12Spec => new(
        "A12", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 2500,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));

    /// <summary>The state of Bergus (the entity of the record) at the end of every frame, from the first frame the program is seen.</summary>
    private sealed record Sample(int Frame, int PosZ, int ForceZ, int IsOnGround, int CollidedWithEntityZ, int IsZForceApplied);

    /// <summary>Drives the arc (created by the test under a <c>using</c>, so a blocked arc still restores the global state) to the end signal.</summary>
    private static List<Sample> Run(ArcRun arc)
    {
        var samples = new List<Sample>();
        var previous = arc.OnFrame;
        arc.OnFrame = () =>
        {
            previous?.Invoke();
            if (arc.EntityByRecord(8) is { } bergus)
            {
                samples.Add(new Sample(arc.Frame, bergus.PosZ, bergus.ForceZ, bergus.IsOnGround, bergus.CollidedWithEntityZ, bergus.IsZForceApplied));
            }
        };

        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 451, 0x53), "B[2] executes 0x53 @451");
        return samples;
    }

    [Fact]
    public void A12_BergusJumpsOnMap179_TheWaitForTheLandingEnds_TheSceneLeavesByThe0x53()
    {
        using var arc = new ArcRun(A12Spec); // under `using` before the run: an arc blocked in it must not leave the global state dirty.
        var samples = Run(arc);

        // 1. The end signal (0x53 @451, reached by the run above), then nothing skipped, nothing cut off by the loop guard: the base of C0, measured
        // on the DLL before C1 (0x25 skipped), skipped exactly (0x25, 415); R4 ports it.
        AssertNothingSkippedOrExceeded(arc);

        // 2. The frames. F is the image of `1A [2] @411`: 287, as in the base measured on the DLL before C1. With the impulse (E19.d2c1 C2) the wait of the landing
        // lasts: `37 [3] @413` returns at F+4, `0x25 @415` executes from F+4 to F+23 (20 executions) and returns at F+23, `1A [0] @416` runs in the same call, and
        // `0x53 @451` comes 19 images later than the base (359): at 378.
        const int f = 287;
        Assert.Equal(f, FrameOf(arc, B, 411));
        Assert.Equal(f + 4, FramesOf(arc, B, 413, 0x37)[^1]);
        Assert.Equal(Enumerable.Range(f + 4, 20).ToList(), FramesOf(arc, B, 415, 0x25));
        Assert.Equal(f + 23, FrameOf(arc, B, 416));
        Assert.Equal(359 + 19, FrameOf(arc, B, 451));

        // 3. Bergus (record 8): above his rest at the images F+1 to F+22 (the state at the end of the image) by the flight list of UJ-1, at rest from F+23.
        int[] flight =
        {
            348160, 663552, 946176, 1196032, 1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400,
            1462272, 1253376, 1011712, 737280, 430080, 90112,
        };
        int HeightAfterImage(int image) => samples.Single(s => s.Frame == image + 1).PosZ;
        var rest = HeightAfterImage(f);
        for (var i = 0; i < flight.Length; i++)
        {
            Assert.Equal(rest + flight[i], HeightAfterImage(f + 1 + i));
        }

        Assert.Equal(rest, HeightAfterImage(f + 23));
        Assert.Equal(rest, HeightAfterImage(f + 24));

        AssertNoUnexpectedError(arc);
    }
}
