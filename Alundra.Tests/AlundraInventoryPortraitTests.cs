#nullable enable
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// docs/plan-portrait-inventaire.md PI7: <see cref="AlundraInventoryPortrait"/> against the tables measured in the
/// France executable (§1.1 of the plan, cross-checked by PI1). The tables are written out literally here, not
/// recomputed from the formulas the class uses, so a wrong formula cannot agree with itself.
/// </summary>
public sealed class AlundraInventoryPortraitTests
{
    // Opening, per call 1..16: the size drawn (the first call is 0x0, the 16th reaches the rest size).
    private static readonly (int W, int H)[] OpeningSizes =
    {
        (0, 0), (3, 3), (6, 7), (9, 11), (12, 14), (16, 18), (19, 22), (22, 26),
        (25, 29), (28, 33), (32, 37), (35, 41), (38, 44), (41, 48), (44, 52), (48, 56),
    };

    // Return, per call 1..16: the size drawn (the first call is the full portrait, the 16th is 0x0).
    private static readonly (int W, int H)[] ReturnSizes =
    {
        (48, 56), (44, 52), (41, 48), (38, 44), (35, 41), (32, 37), (28, 33), (25, 29),
        (22, 26), (19, 22), (16, 18), (12, 14), (9, 11), (6, 7), (3, 3), (0, 0),
    };

    [Fact]
    public void Opening_DrawsTheMeasuredSizes_ThenStaysAtRest()
    {
        var portrait = new AlundraInventoryPortrait();
        portrait.Start(160, 120);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, portrait.State);

        for (var call = 0; call < OpeningSizes.Length; call++)
        {
            portrait.Step();
            Assert.Equal(OpeningSizes[call], (portrait.DrawnWidth, portrait.DrawnHeight));
        }

        Assert.Equal(AlundraInventoryPortrait.StateAtRest, portrait.State);
        for (var call = 0; call < 100; call++)
        {
            portrait.Step();
            Assert.Equal((248, 104, 48, 56), (portrait.X, portrait.Y, portrait.DrawnWidth, portrait.DrawnHeight));
            Assert.Equal(AlundraInventoryPortrait.StateAtRest, portrait.State);
        }
    }

    [Fact]
    public void Opening_FliesFromTheHead_WithAPositiveSpan()
    {
        // Head (160, 120): span (-88, 16) from the rest position.
        var portrait = new AlundraInventoryPortrait();
        portrait.Start(160, 120);

        portrait.Step(); // s = 15: at the head.
        Assert.Equal((160, 120), (portrait.X, portrait.Y));
        Assert.False(portrait.IsVisible); // 0x0

        portrait.Step(); // s = 14: X = 248 + trunc(-1232 / 15) = 248 - 82; Y = 104 + trunc(224 / 15) = 104 + 14.
        Assert.Equal((166, 118), (portrait.X, portrait.Y));
        Assert.True(portrait.IsVisible);

        for (var call = 3; call <= 15; call++)
        {
            portrait.Step();
        }

        // s = 1: X = 248 + trunc(-88 / 15) = 248 - 5; Y = 104 + trunc(16 / 15) = 105.
        Assert.Equal((243, 105), (portrait.X, portrait.Y));

        portrait.Step(); // s = 0: rest.
        Assert.Equal((248, 104), (portrait.X, portrait.Y));
    }

    [Fact]
    public void Opening_TruncatesTowardZero_WithANegativeSpan()
    {
        // Head (300, 60): span (52, -44). s = 14: Y = 104 + trunc(-616 / 15) = 104 - 41 = 63 (a floor would give 62).
        var portrait = new AlundraInventoryPortrait();
        portrait.Start(300, 60);

        portrait.Step();
        Assert.Equal((300, 60), (portrait.X, portrait.Y));
        portrait.Step();
        Assert.Equal((296, 63), (portrait.X, portrait.Y));
    }

    [Fact]
    public void Return_ShrinksToTheHeadReadAtExit_ThenGoesIdle()
    {
        var portrait = OpenedToRest();

        // The head has moved since the opening: the return targets the point read NOW (0x80057b84 re-reads it).
        portrait.BeginReturn(160, 120);
        Assert.Equal(AlundraInventoryPortrait.StateReturning, portrait.State);

        for (var call = 0; call < ReturnSizes.Length; call++)
        {
            portrait.Step();
            Assert.Equal(ReturnSizes[call], (portrait.DrawnWidth, portrait.DrawnHeight));
            if (call == 0)
            {
                Assert.Equal((248, 104), (portrait.X, portrait.Y)); // s = 15 starts at the rest position
            }
            else if (call == 1)
            {
                // s = 14: X = 160 + trunc(88 * 14 / 15) = 242; Y = 120 + trunc(-16 * 14 / 15) = 106.
                Assert.Equal((242, 106), (portrait.X, portrait.Y));
            }
            else if (call == 14)
            {
                // s = 1: X = 160 + trunc(88 / 15) = 165; Y = 120 + trunc(-16 / 15) = 119.
                Assert.Equal((165, 119), (portrait.X, portrait.Y));
            }
        }

        Assert.Equal(AlundraInventoryPortrait.StateIdle, portrait.State);
        Assert.False(portrait.IsVisible);

        portrait.Step();
        Assert.False(portrait.IsVisible);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, portrait.State);
    }

    [Fact]
    public void Start_IsIgnoredUnlessIdle()
    {
        var portrait = OpenedToRest();
        portrait.Start(0, 0);
        Assert.Equal(AlundraInventoryPortrait.StateAtRest, portrait.State);

        portrait.BeginReturn(160, 120);
        portrait.Step();
        portrait.Start(0, 0); // a start while the return still flies is dropped, as by the original's guard
        Assert.Equal(AlundraInventoryPortrait.StateReturning, portrait.State);

        for (var call = 0; call < 15; call++)
        {
            portrait.Step();
        }

        Assert.Equal(AlundraInventoryPortrait.StateIdle, portrait.State);
        portrait.Start(160, 120);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, portrait.State);
    }

    [Fact]
    public void Return_IsIgnoredWhileIdle_AndNothingIsDrawn()
    {
        var portrait = new AlundraInventoryPortrait();
        portrait.BeginReturn(160, 120);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, portrait.State);
        portrait.Step();
        Assert.False(portrait.IsVisible);
    }

    [Fact]
    public void HeadPoint_ReadsTheIntegerPartsWithoutAnyPixelOffset()
    {
        // PosX/PosY/PosZ are 16.16: integer parts 300, 200 and 16, plus fractions that must be dropped.
        var head = AlundraInventoryPortrait.ComputeHeadPoint(
            (300 << 16) | 0xffff, (200 << 16) | 0x8000, 16 << 16, scrollX: 100, scrollY: 50);

        // X = 300 - 100 (no "+2"); Y = 200 - 50 - 16 - 32.
        Assert.Equal((200, 102), head);
    }

    private static AlundraInventoryPortrait OpenedToRest()
    {
        var portrait = new AlundraInventoryPortrait();
        portrait.Start(200, 180);
        for (var call = 0; call < 16; call++)
        {
            portrait.Step();
        }

        Assert.Equal(AlundraInventoryPortrait.StateAtRest, portrait.State);
        return portrait;
    }
}
