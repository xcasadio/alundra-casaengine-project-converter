#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using static Alundra.Tests.AlundraStoryChainOpcodeAudit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.e E2 and E3 (docs/plan-e19-opcodes.md section 1.2i): the scripted scenes of the day 3 that need no combat, as arcs on the real exported maps with the
/// export's prefabs and the real hero: A13 (176 <c>B[6]</c>), A14 (179 <c>B[3]</c>), A15 (176 <c>B[7]</c>), A17 (135) and A18 (178). Pinning rules of the plan:
/// exact the ends of the scenes (instruction), the flags set and cleared (with their pc), the destinations of <c>0x53</c> and the contact relations (the
/// semi-open rule of E19.d2b); +-2.5 px the final positions of the walks, measured by the emulation of the binary before E19.d2c; the absolute images are not
/// pinned, only the order of the events and the frame limit. The opcodes the interpreter skips in each arc must all be lines of the closed list of E19.e E1
/// (rule 5, <see cref="AlundraStoryChainOpcodeAudit.Rule5UnlistedArcSites"/>). A value that the measure contradicts is a stop, never a re-pin.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraDay3SceneArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private const int Px = 65536;
    private const int Tolerance = 163840; // 2.5 px.

    private static void AssertWithin(int actual, int expected, string what)
        => Assert.True(Math.Abs(actual - expected) <= Tolerance, $"{what}: {actual / 65536.0} px, {expected / 65536.0} px +/- 2.5 expected");

    /// <summary>Rule 5 of E19.e E1: every <c>UnknownSkipped</c> entry of the trace, on the map of the arc, is a line of the closed list, and the two other
    /// failure kinds (the loop guard, a size that is missing) never appear.</summary>
    private static void AssertSkippedAreListed(ArcRun arc, int map)
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), map);
        var list = ParseList(File.ReadAllLines(FindListPath()));
        var unlisted = Rule5UnlistedArcSites(map, document, arc.Trace, list)
            .Select(t => $"0x{t.Opcode:X2} @{t.Pc} (slot {t.Slot}, program @{t.ProgramStart})").Distinct().ToList();
        Assert.True(unlisted.Count == 0, $"skipped opcodes of the arc {arc.Spec.Name} that are not lines of the closed list: {string.Join("; ", unlisted)}");
        var failures = arc.SkippedOrExceeded.Where(t => t.Kind != EventTraceKind.UnknownSkipped)
            .Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})").Distinct().ToList();
        Assert.True(failures.Count == 0, $"loop guard or unsized instruction in the arc {arc.Spec.Name}: {string.Join("; ", failures)}");
    }

    private static void AssertPendingArrival(uint map, int x, int y, int z)
    {
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(map, arrival.MapIndex);
        Assert.Equal((x, y, z), (arrival.PosX, arrival.PosY, arrival.PosZ));
    }

    // ----------------------------------------------------------------------------------------------------------
    // A13 - map 176, B[6] (program @468), the first walk of Giles (day 3)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A13Spec => new(
        "A13", "Inoa", "Inoa-176", new[] { 203, 1651, 1652, 1660 }, 0, 0, 0, 1500,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(11796480, 36175872, 10485760, AlundraGameState.ResetAnimationId, 24));

    /// <summary>
    /// A13 (G203, G1651, G1652, G1660): the hero arrives on map 176 by the <c>0x53 @451</c> of the map 179 (A12), at (180, 552, 160). B[6] (program @468)
    /// walks Giles (record 4, <c>0x24 @526</c>) to (494, 552), sets G1653 (<c>0x05 @530</c>) and leaves for map 179 (<c>0x53 @533</c>).
    /// </summary>
    [Fact]
    public void A13_GilesWalksOnMap176_TheSceneSetsG1653_AndLeavesFor179()
    {
        using var arc = new ArcRun(A13Spec);
        var s = new ArcSamples(arc, new[] { 4 }, Array.Empty<uint>(), (B, 468, 526));
        Assert.False(IsSet(1653), "G1653 is not set by the arc's start");

        // 1. The end signal.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 533, 0x53), "B[6] executes 0x53 @533");

        // 2. Only the instructions of the closed list are skipped.
        AssertSkippedAreListed(arc, 176);

        // 3. Giles ends 0x24 @526 near (494, 552); G1653 is set by 0x05 @530, before the 0x53.
        var giles = s[B, 468, 526].Rec(4);
        AssertWithin(giles.X, 494 * Px, "Giles ends 0x24 @526 (x)");
        AssertWithin(giles.Y, 552 * Px, "Giles ends 0x24 @526 (y)");
        Assert.True(arc.Has(B, 530, 0x05), "0x05 @530 never ran");
        Assert.True(FrameOf(arc, B, 526) <= FrameOf(arc, B, 530) && FrameOf(arc, B, 530) <= FrameOf(arc, B, 533), "the order @526, @530, @533");
        Assert.True(IsSet(1653), "G1653");

        // 4. 0x53 @533: map 179, (27525120, 7864320, 1048576).
        AssertPendingArrival(179, 27525120, 7864320, 1048576);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A14 - map 179, B[3] (program @464), the villagers and Septimus
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A14Spec => new(
        "A14", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1652, 1653, 1660 }, 0, 0, 0, 1000,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(27525120, 7864320, 1048576, AlundraGameState.ResetAnimationId, 0));

    /// <summary>
    /// A14 (G203, G1651 to G1653, G1660): the hero arrives on map 179 by the <c>0x53 @533</c> of the map 176 (A13). B[3] (program @464) sets T800
    /// (<c>0x05 @528</c>) and T801 (<c>0x05 @542</c>), which Nestus (record 9, C program @1128) clears (<c>0x06 @1149</c>, <c>@1160</c>); the hero walks to Septimus
    /// (record 0, <c>0x24 @564</c>), G1654 is set (<c>0x05 @565</c>) and <c>0x53 @568</c> leaves for map 176.
    /// </summary>
    [Fact]
    public void A14_TheVillagersOnMap179_NestusClearsT800AndT801_TheHeroStopsAtSeptimus_AndLeavesFor176()
    {
        using var arc = new ArcRun(A14Spec);
        var s = new ArcSamples(arc, new[] { 0 }, new uint[] { 800, 801 }, (B, 464, 564));

        // 1. The end signal.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 568, 0x53), "B[3] executes 0x53 @568");

        // 2. Only the instructions of the closed list are skipped.
        AssertSkippedAreListed(arc, 179);

        // 3. T800 and T801 are set by B[3] and cleared by Nestus, each set before its clearing.
        Assert.True(arc.Has(B, 528, 0x05) && arc.Has(B, 542, 0x05), "0x05 @528 and @542 never ran");
        Assert.True(arc.Has(C, 1149, 0x06) && arc.Has(C, 1160, 0x06), "0x06 @1149 and @1160 of Nestus never ran");
        Assert.True(s.FirstSet.ContainsKey(800) && s.FirstSet.ContainsKey(801), "T800 and T801 were set");
        Assert.True(s.Cleared.ContainsKey(800) && s.Cleared.ContainsKey(801), "T800 and T801 were cleared");
        Assert.True(s.FirstSet[800] < s.Cleared[800] && s.FirstSet[801] < s.Cleared[801], "each T is set before it is cleared");
        Assert.True(s.FirstSet[800] <= s.FirstSet[801], "T800 is set before T801");
        Assert.False(IsTemporarySet(800) || IsTemporarySet(801), "T800 and T801 end cleared");
        Assert.True(FrameOf(arc, B, 528) <= FrameOf(arc, C, 1149) && FrameOf(arc, B, 542) <= FrameOf(arc, C, 1160), "Nestus clears after B[3] sets");

        // 4. The hero ends 0x24 @564 against Septimus (record 0), near (420, 332): the contact, edge against edge (the hero's south edge at y + 8, Septimus's
        // north edge at y - 6).
        var at564 = s[B, 464, 564];
        AssertWithin(at564.Hero.X, 27525120, "the hero ends 0x24 @564 (x)");
        AssertWithin(at564.Hero.Y, 21757952, "the hero ends 0x24 @564 (y)");
        Assert.Equal(at564.Hero.Y + 8 * Px, at564.Rec(0).Y - 6 * Px);

        // 5. G1654 is set by 0x05 @565, then 0x53 @568: map 176, (8650752, 35127296, 10485760).
        Assert.True(arc.Has(B, 565, 0x05), "0x05 @565 never ran");
        Assert.True(IsSet(1654), "G1654");
        AssertPendingArrival(176, 8650752, 35127296, 10485760);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A15 - map 176, B[7] (program @544), the walk of Septimus and Giles
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A15Spec => new(
        "A15", "Inoa", "Inoa-176", new[] { 203, 1651, 1652, 1653, 1654, 1660 }, 0, 0, 0, 2500,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(8650752, 35127296, 10485760, AlundraGameState.ResetAnimationId, 0));

    /// <summary>
    /// A15 (G203, G1651 to G1654, G1660): the hero arrives on map 176 by the <c>0x53 @568</c> of the map 179 (A14). B[7] (program @544) sets T0 (<c>0x05 @561</c>);
    /// Septimus (record 5, C program @776) waits for T0 (<c>0x36 @776</c>), walks, waits for T1 (<c>@792</c>) and sets T2 (<c>0x05 @812</c>); Giles (record 6,
    /// C program @828) sets T1 (<c>0x05 @850</c>), waits for T2 (<c>@853</c>) and sets T3 (<c>0x05 @860</c>), which B[7] waits for (<c>@588</c>). No program clears
    /// them (they fall at the arrival on map 10). The hero ends 0x24 @599 near (266, 231) and <c>0x53 @600</c> leaves for map 10.
    /// </summary>
    [Fact]
    public void A15_SeptimusAndGilesOnMap176_TheFourTemporaryFlagsAreSetInOrder_TheSceneLeavesFor10()
    {
        using var arc = new ArcRun(A15Spec);
        var s = new ArcSamples(arc, new[] { 5, 6 }, new uint[] { 0, 1, 2, 3 }, (B, 544, 599));

        // 1. The end signal.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 600, 0x53), "B[7] executes 0x53 @600");

        // 2. Only the instructions of the closed list are skipped.
        AssertSkippedAreListed(arc, 176);

        // 3. The four flags: set by their pc, each before the wait that needs it passes, none cleared.
        Assert.True(arc.Has(B, 561, 0x05) && arc.Has(C, 812, 0x05) && arc.Has(C, 850, 0x05) && arc.Has(C, 860, 0x05), "a setter of T0 to T3 never ran");
        foreach (var n in new uint[] { 0, 1, 2, 3 })
        {
            Assert.True(s.FirstSet.ContainsKey(n), $"T{n} never set");
            Assert.False(s.Cleared.ContainsKey(n), $"T{n} was cleared");
            Assert.True(IsTemporarySet(n), $"T{n} falls only at the arrival on the next map");
        }

        Assert.True(FrameOf(arc, B, 561) <= FrameOf(arc, C, 779), "Septimus's wait @776 for T0 ends after B[7] @561 sets it");
        Assert.True(FrameOf(arc, C, 850) <= FrameOf(arc, C, 795), "Septimus's wait @792 for T1 ends after Giles @850 sets it");
        Assert.True(FrameOf(arc, C, 812) <= FrameOf(arc, C, 856), "Giles's wait @853 for T2 ends after Septimus @812 sets it");
        Assert.True(FrameOf(arc, C, 860) <= FrameOf(arc, B, 591), "B[7]'s wait @588 for T3 ends after Giles @860 sets it");
        Assert.True(s.FirstSet[0] <= s.FirstSet[1] && s.FirstSet[1] <= s.FirstSet[2] && s.FirstSet[2] <= s.FirstSet[3], "T0, T1, T2, T3 are set in this order");

        // 4. The hero ends 0x24 @599 near (266, 231).
        var at599 = s[B, 544, 599];
        AssertWithin(at599.Hero.X, 266 * Px, "the hero ends 0x24 @599 (x)");
        AssertWithin(at599.Hero.Y, 231 * Px, "the hero ends 0x24 @599 (y)");

        // 5. The hero passes close to Giles (0.625 px, the plan's risk on O-E19-29): no entity shortened or cancelled a single step of the arc, the hero's included.
        Assert.Equal(0, arc.TotalEntityBlockCount);

        // 6. 0x53 @600: map 10, (16515072, 61341696, 0).
        AssertPendingArrival(10, 16515072, 61341696, 0);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A17 - map 135, the church, Ronan (day 3)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A17Spec => new(
        "A17", "Church", "Church-135", new[] { 203, 1654 }, 0, 0, 0, 2500,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(30670848, 54001664, 1048576, AlundraGameState.ResetAnimationId, 16));

    /// <summary>Frames until <paramref name="done"/>, with the button of the arcs' rule on the frames that start with a dialogue open (E19.f2a: held while the box
    /// types, released then pressed when it waits for a press), and answering the choice of a box the first option (OUI) at the start of the frame, as a player
    /// who accepts. Fails at the arc's frame limit.</summary>
    private static void RunAcceptingTheChoice(ArcRun arc, Func<bool> done, string signal)
    {
        while (!done())
        {
            if (arc.Frame >= arc.Spec.FrameLimit)
            {
                Assert.Fail(arc.StuckMessage(signal));
            }

            if (AlundraDialogueDirector.Instance.IsAwaitingChoice)
            {
                Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(0));
            }

            arc.OneFrameWithTheDialogueButton();
        }

        arc.LetGoOfTheDialogueButton();
    }

    /// <summary>
    /// A17 (G203, G1654): the hero arrives on map 135 by the <c>0x53 @2463</c> of the map 10 (A10J). B[1] (program @136) activates Ronan (record 0) under G203
    /// and makes Giles (record 28) appear (<c>0x8A @159</c>). B[14] (program @912) walks the hero to Ronan (<c>0x24 @942</c>), sets T1 (<c>0x05 @945</c>) and, after
    /// the box of Ronan, G1655 (<c>0x05 @953</c>). Ronan's program C[1] (@960) opens the box 129 (<c>0x0D @1048</c>), waits for T0, which Yarn sets after the first
    /// page (<c>0x36 @1053</c>), asks the choice (<c>0x44 @1056</c>) - accepted here -, ends with <c>0x11 @1115</c> and sets G14 (<c>0x05 @1120</c>). The blocks
    /// of records 8 to 10 are destroyed.
    /// </summary>
    [Fact]
    public void A17_TheChurchOnMap135_TheHeroStopsAtRonan_TheChoiceIsAccepted_TheSceneSetsG1655AndG14()
    {
        using var arc = new ArcRun(A17Spec);
        var s = new ArcSamples(arc, new[] { 0 }, new uint[] { 0, 1 }, (B, 912, 942));
        Assert.False(IsSet(1655) || IsSet(14), "G1655 and G14 are not set by the arc's start");

        // E19.r R3: the blocks of the records 8 to 10 exist after B[2] 0x2D @219-@223 and are captured at the trace of 0x2E @272, all three still listed and
        // flagged for destruction by B[2] (0x2E @268, @270, @272); the recycling of the same tick takes them away.
        List<AlundraEntityScriptProxy>? blocks = null;
        var chained = arc.OnInstruction!;
        arc.OnInstruction = t =>
        {
            chained(t);
            if (t.Slot == B && t.Pc == 272 && t.Opcode == 0x2E)
            {
                blocks = arc.Entities.Where(e => e.EntityRefId is >= 8 and <= 10).ToList();
            }
        };

        // 1. The end signal: 0x11 @1115 of C[1], then G14 by 0x05 @1120.
        RunAcceptingTheChoice(arc, () => arc.Has(C, 1115, 0x11), "C[1] executes 0x11 @1115");
        RunAcceptingTheChoice(arc, () => arc.Has(C, 1120, 0x05), "C[1] executes 0x05 @1120");

        // 2. Only the instructions of the closed list are skipped.
        AssertSkippedAreListed(arc, 135);

        // 3. B[1]: Ronan is active (record 0) and Giles (record 28) appeared by 0x8A @159.
        Assert.True(arc.Has(B, 159, 0x8A), "0x8A @159 never ran");
        Assert.NotNull(arc.EntityByRecord(28));
        Assert.Equal(EntityStatus.Normal, arc.EntityByRecord(0)!.Status);

        // 4. The hero ends 0x24 @942 against Ronan, edge against edge (the hero's east edge at x + 11, Ronan's west edge at x - 10).
        var at942 = s[B, 912, 942];
        Assert.Equal(at942.Hero.X + 11 * Px, at942.Rec(0).X - 10 * Px);

        // 5. T1 is set (0x05 @945) before G1655 (0x05 @953); T0 is set by Yarn during the box, before the choice (0x44 @1056); the choice is accepted
        // (the branch of 0x03 @1059 that goes to 0x05 @1072).
        Assert.True(arc.Has(B, 945, 0x05) && arc.Has(B, 953, 0x05), "0x05 @945 and @953 never ran");
        Assert.True(FrameOf(arc, B, 945) <= FrameOf(arc, B, 953), "T1 (@945) is set before G1655 (@953)");
        Assert.True(s.FirstSet.ContainsKey(1), "T1 never set");
        Assert.True(IsSet(1655), "G1655");
        Assert.True(s.FirstSet.ContainsKey(0), "T0 (set by Yarn) never set");
        Assert.True(s.FirstSet[0] <= FrameOf(arc, C, 1056) + 1, "T0 is set before the choice");
        Assert.True(arc.Has(C, 1056, 0x44) && arc.Has(C, 1072, 0x05), "the choice (0x44 @1056) is accepted: 0x05 @1072 runs");

        // 6. G14 (0x05 @1120), and the blocks of the records 8 to 10 are destroyed.
        Assert.True(IsSet(14), "G14");
        Assert.NotNull(blocks);
        Assert.Equal(3, blocks!.Count);
        Assert.All(blocks, b => Assert.Equal(EntityStatus.Destroyed, b.Status));
        Assert.All(blocks, b => Assert.DoesNotContain(b, arc.Entities));
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A18 - map 178, the house, two phases (day 3)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A18Spec => new(
        "A18", "Inoa", "Inoa (inner)-178", new[] { 203, 1654, 1655 }, 0, 0, 0, 4000,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(60555264, 26738688, 0, AlundraGameState.ResetAnimationId, 16));

    /// <summary>
    /// A18 (G203, G1654, G1655): the hero arrives on map 178 by the portal 176.7. Septimus (record 15, C program @512) talks to the hero and the first phase ends
    /// with <c>0x11 @543</c>; the program then waits for the hero in the box of <c>0x3B @576 [39,40,13,15,1,1]</c>: the arc places him at (948, 232, 16), the tile
    /// (39, 14) of height 1, an entry chosen in the box and not a measure. The second phase ends with <c>0x38 @708</c> and <c>@713</c> (the tables [176] and
    /// [162] now say 183), G203 cleared (<c>0x06 @718</c>), G204 set (<c>0x05 @721</c>) and <c>0x11 @724</c>. The z of the arrival comes from the record of the portal (0)
    /// and <c>ClampToGround</c> raises it: it is not pinned. <c>@694</c> and <c>@697</c> (a margin of zero) are not pinned either.
    /// </summary>
    [Fact]
    public void A18_TheHouseOnMap178_TwoPhases_TheTablesSay183_G203IsClearedAndG204Set()
    {
        using var arc = new ArcRun(A18Spec);
        Assert.Equal((ushort)176, ArcRun.State.MapIdToInternalMapIndexTable[176]);
        Assert.Equal((ushort)162, ArcRun.State.MapIdToInternalMapIndexTable[162]);

        // E19.k1 K3: the camera sway of Septimus' earthquake, sampled right after 0x8E @608, 0x8E @618 and 0x8F @683 (C[6]), and at
        // the end of the frame of @683.
        var sway = AlundraCameraSway.Instance;
        (int SpeedX, int SpeedY, int LimitX, int LimitY)? swayAt608 = null, swayAt618 = null;
        (int Flag, int SpeedX, int SpeedY, int LimitX, int LimitY, int OffsetX, int OffsetY)? swayAt683 = null, swayAfterTheFrameOf683 = null;
        var frameOf683 = -1;
        arc.OnInstruction = t =>
        {
            if (t.Slot != C)
            {
                return;
            }

            if (t.Opcode == 0x8E && t.Pc == 608)
            {
                swayAt608 = (sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY);
            }
            else if (t.Opcode == 0x8E && t.Pc == 618)
            {
                swayAt618 = (sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY);
            }
            else if (t.Opcode == 0x8F && t.Pc == 683)
            {
                frameOf683 = t.Frame;
                swayAt683 = (sway.Flag, sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY, sway.OffsetX, sway.OffsetY);
            }
        };
        arc.OnFrame = () =>
        {
            if (frameOf683 == arc.Frame - 1 && swayAfterTheFrameOf683 == null)
            {
                swayAfterTheFrameOf683 = (sway.Flag, sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY, sway.OffsetX, sway.OffsetY);
            }
        };

        // 1. Phase 1: the end 0x11 @543.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 543, 0x11), "C[6] executes 0x11 @543");
        Assert.False(arc.Has(C, 587, 0x30), "the second phase does not start before the hero is in the box");

        // 2. Phase 2: the hero in the box of 0x3B @576, then the end 0x11 @724.
        arc.PlaceHero(948, 232, 16);
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 724, 0x11), "C[6] executes 0x11 @724");

        // 3. Only the instructions of the closed list are skipped.
        AssertSkippedAreListed(arc, 178);

        // The camera sway (E19.k1): [5,3,7,5] after @608, [13,11,1,2] after @618, flag 0 after @683, then at the next step Limit, Speed and
        // Offset at 0 (Reach is not pinned: its value depends on the number of steps since @618).
        Assert.Equal((5, 3, 7, 5), swayAt608);
        Assert.Equal((13, 11, 1, 2), swayAt618);
        Assert.Equal(0, swayAt683?.Flag);
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), swayAfterTheFrameOf683);

        // 4. The end: the two 0x38, G203 cleared by 0x06 @718, G204 set by 0x05 @721, in this order.
        Assert.True(arc.Has(C, 708, 0x38) && arc.Has(C, 713, 0x38), "0x38 @708 and @713 never ran");
        Assert.Equal((ushort)183, ArcRun.State.MapIdToInternalMapIndexTable[176]);
        Assert.Equal((ushort)183, ArcRun.State.MapIdToInternalMapIndexTable[162]);
        Assert.True(arc.Has(C, 718, 0x06) && arc.Has(C, 721, 0x05), "0x06 @718 and 0x05 @721 never ran");
        Assert.True(FrameOf(arc, C, 718) <= FrameOf(arc, C, 721) && FrameOf(arc, C, 721) <= FrameOf(arc, C, 724), "the order @718, @721, @724");
        Assert.False(IsSet(203), "G203 is cleared");
        Assert.True(IsSet(204), "G204 is set");
        AssertNoUnexpectedError(arc);
    }
}
