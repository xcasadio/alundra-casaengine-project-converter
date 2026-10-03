#nullable enable
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.r R3-2 (docs/plan-e19-opcodes.md section 1.2p, D-E19-45, ADR-0024): the scene of the three Murggs in front of Tarn's manor, map 15, as an arc on
/// the real exported map with the export's prefabs and the real hero. The scene needs no combat: <c>B[1]</c> (program @72) waits at <c>@108</c>-<c>@111</c>
/// (<c>0x2C [2]</c>: no entity of record 2) for the Murgg of record 2 to be gone, and that Murgg destroys itself at the end of its script
/// (<c>C[8] 0x2E [0x80] @890</c>). Without the recycling of the destroyed entities the corpse stays found by the search and the scene never ends.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraTarnMurggArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private static ArcSpec R32Spec => new(
        "R3-2", "Overworld", "Overworld 3,2-15", System.Array.Empty<int>(), 26, 21, 7, 1500,
        RealController: true, Prefabs: true);

    /// <summary>
    /// R3-2 (G1650 clear): the hero stands on the tile (26, 21), z 7, the courtyard gate. B[1] takes the control (<c>0x10 @88</c>), activates the three Murggs
    /// (records 0 to 2) and a transparent block (<c>0x8B</c> of record 24), then waits for record 2. The three Murggs end their scripts with <c>0x2E</c>
    /// (C[6] <c>@712</c>, C[7] <c>@777</c>, C[8] <c>@890</c>); the recycling takes the corpse of record 2 away, <c>0x2C [2]</c> answers true, B[1] sets G1650
    /// (<c>0x05 @114</c>), destroys the block (<c>0x2E [24] @117</c>) and gives the control back (<c>0x11 @119</c>).
    /// </summary>
    [Fact]
    public void R32_TheMurggsOfTarnsManor_TheScriptsEnd_TheCorpseOfRecord2IsRecycled_TheControlComesBack()
    {
        using var arc = new ArcRun(R32Spec);
        Assert.False(IsSet(1650), "G1650 is not set by the arc's start");

        // 1. The end signal: 0x11 @119 of B[1].
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 119, 0x11), "B[1] executes 0x11 @119");

        // 2. The scene ran in order: control taken, the three Murgg scripts reached their self-destruction, then the end of B[1].
        Assert.True(arc.Has(B, 88, 0x10), "0x10 @88 never ran");
        Assert.True(arc.Has(C, 712, 0x2E), "C[6] 0x2E @712 never ran");
        Assert.True(arc.Has(C, 777, 0x2E), "C[7] 0x2E @777 never ran");
        Assert.True(arc.Has(C, 890, 0x2E), "C[8] 0x2E @890 never ran");
        Assert.True(arc.Has(B, 114, 0x05), "B[1] 0x05 @114 never ran");
        Assert.True(arc.Has(B, 117, 0x2E), "B[1] 0x2E @117 never ran");
        Assert.True(FrameOf(arc, C, 890) <= FrameOf(arc, B, 114), "the wait of B[1] ends after the Murgg of record 2 destroyed itself");
        Assert.True(FrameOf(arc, B, 114) <= FrameOf(arc, B, 117) && FrameOf(arc, B, 117) <= FrameOf(arc, B, 119), "the order @114, @117, @119");

        // 3. G1650 is set, the control is back (bit 0x04 of PlayerControlFlags cleared by 0x11).
        Assert.True(IsSet(1650), "G1650");
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags & 0x04u);

        // 4. The three Murggs and the block are recycled: none is listed any more (their records are free).
        Assert.Null(arc.EntityByRecord(0));
        Assert.Null(arc.EntityByRecord(1));
        Assert.Null(arc.EntityByRecord(2));
        Assert.Null(arc.EntityByRecord(24));
        AssertNoUnexpectedError(arc);
    }
}
