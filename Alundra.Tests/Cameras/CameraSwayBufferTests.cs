#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Cameras;
using Alundra.Tests.Ai.Items;

namespace Alundra.Tests.Cameras;

/// <summary>
/// E20.h4d1 T1 (docs/plan-e20-heros.md section 1.31.5, D-H4D1-2, tests (b1) to (b3)): the buffer of the session the pilot of the fire tome 2 keeps for the nine words of the shake block (<c>0x80127138</c>, in the order of the block <c>0x800E4338</c>): the capture
/// copies the nine words, in order, and leaves the block as it was; the restoration copies the buffer to the nine words with no guard; the buffer is zero at the start, kept by the map load and cleared by <see cref="CameraSway.ResetForTests"/>.
/// </summary>
public sealed class CameraSwayBufferTests : IDisposable
{
    public CameraSwayBufferTests() => CameraSway.Instance.ResetForTests();

    public void Dispose() => CameraSway.Instance.ResetForTests();

    private static int[] Block(int start) => Enumerable.Range(0, 9).Select(i => start + 10 * i).ToArray();

    [Fact]
    public void B1_TheCapture_CopiesTheNineWordsInTheOrderOfTheBlock_AndLeavesTheBlockAsItWas()
    {
        var sway = new CameraSway();
        var words = new[] { 1, 5, 6, 7, 8, 3, -2, 1, 0 };
        FireTome2Run.Post(sway, words);

        sway.CaptureToBuffer();

        Assert.Equal(words, sway.Buffer); // Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY.
        Assert.Equal(words, FireTome2Run.Words(sway));

        // Each word goes to its own place (nine distinct values).
        var distinct = Block(11);
        FireTome2Run.Post(sway, distinct);
        sway.CaptureToBuffer();
        Assert.Equal(distinct, sway.Buffer);

        // A second capture overwrites the first.
        FireTome2Run.Post(sway, new int[9]);
        sway.CaptureToBuffer();
        Assert.Equal(new int[9], sway.Buffer);
    }

    [Fact]
    public void B2_TheRestoration_HasNoGuard_AZeroBufferZeroesANonZeroBlock_AStaleBufferIsRestoredAsItIs_TheBufferIsNotCleared()
    {
        var sway = new CameraSway();

        FireTome2Run.Post(sway, Block(1));
        sway.RestoreFromBuffer(); // the buffer is all zero: the block becomes zero, Flag included, whatever it held.
        Assert.Equal(new int[9], FireTome2Run.Words(sway));

        Block(900).CopyTo(sway.Buffer, 0); // a buffer from another cast, long stale.
        FireTome2Run.Post(sway, Block(1));
        sway.RestoreFromBuffer();
        Assert.Equal(Block(900), FireTome2Run.Words(sway));
        Assert.Equal(Block(900), sway.Buffer);

        // Every word is restored, the offsets and the reach included (the step of the shake is not run by the restoration).
        Assert.Equal((900 + 50, 900 + 60, 900 + 70, 900 + 80), (sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY));
        sway.RestoreFromBuffer();
        Assert.Equal(Block(900), FireTome2Run.Words(sway)); // idempotent.
    }

    [Fact]
    public void B3_TheBufferIsZeroAtTheStart_KeptByTheMapLoad_AndClearedByResetForTests()
    {
        var sway = new CameraSway();
        Assert.Equal(new int[9], sway.Buffer);
        Assert.Equal(9, sway.Buffer.Length);

        Block(3).CopyTo(sway.Buffer, 0);
        FireTome2Run.Post(sway, Block(4));

        sway.OnMapLoad(); // Flag back to 0, nothing else: the buffer survives the map load (session scope).
        Assert.Equal(Block(3), sway.Buffer);
        Assert.Equal(0, sway.Flag);

        sway.ResetForTests();
        Assert.Equal(new int[9], sway.Buffer);
        Assert.Equal(new int[9], FireTome2Run.Words(sway));

        // The shared instance is no exception.
        Block(7).CopyTo(CameraSway.Instance.Buffer, 0);
        CameraSway.Instance.ResetForTests();
        Assert.Equal(new int[9], CameraSway.Instance.Buffer);
    }
}
