#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d2b B6 (docs/plan-e19-opcodes.md section 1.2h.2, section 1.3, D-E19-27 to D-E19-37, ADR-0022): the four scenes that the entity
/// contacts of the binary make play to their end, as arcs on the real exported maps, with the export's real prefabs and the real hero.
/// Without contacts between entities these scenes stall in the wall of the cell model (the "red" the plan writes down); with them they
/// go to their end. Every expected value is written by hand before the code, in the plan: contacts in exact relations (edge against edge,
/// to one float ULP of the coordinate), transition positions within 2.5 px where the plan says so, images never absolute. A value that the
/// measure contradicts is a stop, never a re-pin. Every position is read at the first instruction that follows the walk it belongs to
/// (<see cref="ArcSamples"/>): the effects of the walk are then applied, as the script sees them.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraEntityContactArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private const int Px = 65536;

    private static void AssertWithin(int actual, int expected, int tolerance, string what)
        => Assert.True(Math.Abs(actual - expected) <= tolerance, $"{what}: {actual / 65536.0} px, {expected / 65536.0} px +/- {tolerance / 65536.0} expected");

    // ----------------------------------------------------------------------------------------------------------
    // T-A19 - map 185, the meeting of the villagers (day 4)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A19Spec => new(
        "T-A19", "Inoa", "Inoa (inner)-185", new[] { 204, 120, 121, 122, 123 }, 0, 0, 0, 1700,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(60555264, 24641536, 0, AlundraGameState.ResetAnimationId, 16));

    /// <summary>
    /// T-A19 (G204, G120 to G123): the hero arrives by portal 183.6 and is placed at (132, 296, 16), the tile (5, 18) of the zone of B[1]. B[1]
    /// (program @48) spawns Septimus (record 6, <c>0x2D @84</c>); the hero walks west to the wall (<c>0x24 @95</c>). Septimus (C program @452)
    /// walks down his column against the block rec7 (<c>0x24 @458</c>), then east (<c>0x24 @463</c>), destroys rec7 (<c>0x2E @466</c>) and
    /// walks on (<c>0x0B @506</c>), while the four villagers talk. B[2] (program @112) then walks the hero against rec7 (reappeared) and to the
    /// wall, and leaves for map 362 (<c>0x53 @147</c>). Without the contacts Septimus walks THROUGH rec7 to the wall of the cell model and
    /// the scene stalls on <c>0x0B @506</c> (T70 never set).
    /// </summary>
    [Fact]
    public void TA19_TheMeetingOnMap185_SeptimusStopsAtTheBlock_TheSceneReachesMap362()
    {
        using var arc = new ArcRun(A19Spec);
        var s = new ArcSamples(arc, new[] { 6, 7 }, new uint[] { 10, 30, 40, 50, 60, 70 },
            (B, 48, 95), (C, 452, 458), (C, 452, 463), (C, 452, 506), (B, 112, 120), (B, 112, 125));
        AlundraEntityScriptProxy? rec7First = null;
        int destroyFrame = -1;
        var rec7InList = new Dictionary<int, bool>();
        var previousOnInstruction = arc.OnInstruction;
        arc.OnInstruction = t =>
        {
            previousOnInstruction!(t);
            if (t.Slot == C && t.Pc == 466 && destroyFrame < 0)
            {
                destroyFrame = t.Frame;
                rec7First = arc.Entities.First(e => e.EntityRefId == 7);
            }
        };
        var previousOnFrame = arc.OnFrame;
        arc.OnFrame = () =>
        {
            previousOnFrame!();
            if (rec7First != null)
            {
                rec7InList[arc.Frame] = ((IAlundraScriptHost)arc.Proxy).Collidables.Contains(rec7First);
            }
        };

        // The hero arrives at the portal, then is placed in the zone of B[1].
        arc.OneFrame();
        arc.OneFrame();
        arc.PlaceHero(132, 296, 16);

        // 1. The end signal.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 147, 0x53), "B[2] executes 0x53 @147 towards map 362");

        // 2. Only the instructions the plan allows are skipped.
        AssertSkippedWithin(arc, new HashSet<(int, int)> { (0x45, 108), (0x46, 92) });

        // 3. The rest, in the order of the plan.
        Assert.Equal((6946816, 19398656), (s[B, 48, 95].Hero.X, s[B, 48, 95].Hero.Y));

        var at458 = s[C, 452, 458];
        Assert.Equal((8650752, 21626880), (at458.Rec(6).X, at458.Rec(6).Y)); // against rec7, in his column.
        Assert.Equal((7864320, 22544384), (at458.Rec(7).X, at458.Rec(7).Y));
        Assert.Equal(at458.Rec(6).Y + 6 * Px, at458.Rec(7).Y - 8 * Px); // the contact, edge against edge.
        Assert.Equal(11927552, s[C, 452, 463].Rec(6).X);

        // rec7 destroyed by 0x2E @466, and out of the list of obstacles when that frame ends. The samples are keyed by the frame counter AFTER the
        // frame (OneFrame increments it before OnFrame), so the end of the frame of 0x2E @466 is keyed destroyFrame + 1: the exact frame that follows.
        Assert.True(destroyFrame >= 0, "0x2E @466 never ran");
        Assert.False(rec7InList[destroyFrame + 1], "rec7 is still an obstacle at the end of the frame of its destruction");

        Assert.True(s[C, 452, 506].Rec(6).Y >= 22413312, "Septimus ends 0x0B @506 at 342 px or more");

        // T10, T30, T40, T50, T60 and T70 are set in this order.
        foreach (var n in new uint[] { 10, 30, 40, 50, 60, 70 })
        {
            Assert.True(s.FirstSet.ContainsKey(n), $"T{n} never set");
        }

        var order = new uint[] { 10, 30, 40, 50, 60, 70 }.Select(n => s.FirstSet[n]).ToArray();
        for (var i = 0; i + 1 < order.Length; i++)
        {
            Assert.True(order[i] < order[i + 1], $"the temporary flags are not set in the order T10, T30, T40, T50, T60, T70: frames {string.Join(", ", order)}");
        }

        // The hero against rec7 (reappeared), then to the wall.
        var at120 = s[B, 112, 120];
        Assert.Equal((6946816, 21495808), (at120.Hero.X, at120.Hero.Y));
        Assert.Equal(at120.Hero.Y + 8 * Px, at120.Rec(7).Y - 8 * Px);
        Assert.Equal((11862016, 21495808), (s[B, 112, 125].Hero.X, s[B, 112, 125].Hero.Y));

        // 0x53 @147: map 362, (16515072, 17301504, 2097152), effect 4.
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(362u, arrival.MapIndex);
        Assert.Equal((16515072, 17301504, 2097152), (arrival.PosX, arrival.PosY, arrival.PosZ));
        Assert.Equal(4, arrival.EffectId);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // T-A10v - map 10, B[14], the villagers (chapter 18)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A10vSpec => new(
        "T-A10v", "Overworld", "Overworld 2,1-10", new[] { 218, 456 }, 41, 29, 3, 6500, RealController: true, Prefabs: true);

    /// <summary>
    /// T-A10v (G218, G456): B[14] places the hero at (996, 472, 48) and the villagers (<c>0x8A</c>): Meade (record 71), Rumi (72), Bergus (73),
    /// Nestus (74). Nestus walks north to Meade, who stands still (<c>0x24 @5163</c>), east (<c>0x0B @5166</c>) and north (<c>0x0B @5172</c>) and sets
    /// T666; Bergus walks to Rumi (<c>0x24 @5061</c>) and east (<c>0x0B @5071</c>); T674 is set then cleared; B[14] ends with <c>0x53 @2003</c>.
    /// Without the contacts Nestus walks through Meade to the wall of the cell model (359 px) and <c>0x0B @5172</c> never ends.
    /// </summary>
    [Fact]
    public void TA10v_TheVillagersOnMap10_NestusStopsAtMeade_TheSceneReachesItsEnd()
    {
        using var arc = new ArcRun(A10vSpec);
        var s = new ArcSamples(arc, new[] { 71, 72, 73, 74 }, new uint[] { 666, 668, 674 },
            (C, 5156, 5163), (C, 5156, 5166), (C, 5156, 5172), (C, 5012, 5061), (C, 5012, 5071));

        // E19.d2c1 TR-V: the two bouquets (records 69 and 81, no gravity) are dropped by `1B [128,255]` (C[44] @4738 and C[54] @5542, -32768 per
        // tick) and each waits for the landing with `0x25` (@4741, @5545) before `1B [0,0]`. Sampled at the end of every frame.
        var bouquets = new Dictionary<int, List<(int Frame, int PosZ, int ForceZ, int IsOnGround)>> { [69] = new(), [81] = new() };
        var sampleEachFrame = arc.OnFrame!;
        arc.OnFrame = () =>
        {
            sampleEachFrame();
            foreach (var (record, list) in bouquets)
            {
                if (arc.EntityByRecord(record) is { } bouquet)
                {
                    list.Add((arc.Frame, bouquet.PosZ, bouquet.ForceZ, bouquet.IsOnGround));
                }
            }
        };

        // 1. The end signal: 0x53 @2003 of B[14].
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 2003, 0x53), "B[14] executes 0x53 @2003");

        // 2. Only the instructions the measure shows are skipped.
        // 0x58, 0x90, 0x95 and 0x2B (not ported, no effect on the scene); the two 0x25 of the bouquets are ported (E19.d2c1 R4).
        AssertSkippedWithin(arc, new HashSet<(int, int)>
        {
            (0x58, 5470), (0x90, 2689), (0x2B, 6413), (0x95, 6418), (0x90, 2828), (0x58, 4666),
        });

        // 3. The rest, in the order of the plan.
        var n5163 = s[C, 5156, 5163];
        Assert.Equal((62881792, 32997376), (n5163.Rec(71).X, n5163.Rec(71).Y)); // Meade at rest.
        Assert.Equal(33914880, n5163.Rec(74).Y);
        Assert.Equal(n5163.Rec(74).Y - 7 * Px, n5163.Rec(71).Y + 7 * Px); // Nestus against Meade, edge against edge.

        var n5166 = s[C, 5156, 5166];
        Assert.InRange(n5166.Rec(74).X, 980 * Px, 981 * Px);

        var n5172 = s[C, 5156, 5172];
        Assert.True(n5172.Rec(74).Y <= 32112640, $"Nestus ends 0x0B @5172 at y = {n5172.Rec(74).Y / 65536.0} px, 490 or less expected");
        Assert.True(s.FirstSet.ContainsKey(666), "T666 never set");

        var b5061 = s[C, 5012, 5061];
        Assert.Equal(63963136, b5061.Rec(73).X);
        Assert.Equal(20 * Px, b5061.Rec(72).X - b5061.Rec(73).X); // Rumi.PosX - Bergus.PosX = 20 px.
        Assert.True(s.FirstSet.ContainsKey(668), "T668 never set");

        var b5071 = s[C, 5012, 5071];
        AssertWithin(b5071.Rec(73).X, 67108864, 163840, "Bergus ends 0x0B @5071 (x)");
        AssertWithin(b5071.Rec(73).Y, 36175872, 163840, "Bergus ends 0x0B @5071 (y)");

        Assert.True(s.FirstSet.ContainsKey(674), "T674 never set");
        Assert.True(s.Cleared.ContainsKey(674), "T674 never cleared");

        // In this order.
        var ordered = new[]
        {
            n5163.Frame, n5166.Frame, n5172.Frame, s.FirstSet[666], b5061.Frame, s.FirstSet[668], b5071.Frame, s.FirstSet[674], s.Cleared[674], FrameOf(arc, B, 2003),
        };
        for (var i = 0; i + 1 < ordered.Length; i++)
        {
            Assert.True(ordered[i] <= ordered[i + 1], $"the events are not in the order of the plan: frames {string.Join(", ", ordered)}");
        }

        // TR-V (E19.d2c1 C1): each bouquet falls 32768 units per tick from the drop (the tick of `1B [128,255]`, t), its 0x25 returns 1 between
        // t+8 (the 4 px magnet of the engine, IsOnGround 1) and t+17 (the landing of the DLL on the strict test of R5), the `1B [0,0]` that
        // follows sets ForceZ to 0 and the bouquet stays at its rest height: none is left in the air at the end of the arc.
        foreach (var (record, dropPc, waitPc, stopPc) in new[] { (69, 4738, 4741, 4742), (81, 5542, 5545, 5546) })
        {
            var samples = bouquets[record];
            var drop = FrameOf(arc, C, dropPc);
            var wait = FramesOf(arc, C, waitPc, 0x25);
            var detail = $"bouquet {record}: drop frame {drop}, 0x25 frames [{string.Join(",", wait)}], samples {string.Join(" ", samples.Where(x => x.Frame >= drop && x.Frame <= drop + 20).Select(x => $"{x.Frame}:{x.PosZ}/{x.ForceZ}/{x.IsOnGround}"))}";
            Assert.True(wait.Count > 0, detail);
            Assert.InRange(wait[^1] - drop, 8, 17);
            Assert.Equal(wait[^1], FrameOf(arc, C, stopPc)); // the wait returns 1 and `1B [0,0]` runs in the same call.
            var rest = samples[^1].PosZ;
            var afterDrop = samples.Where(x => x.Frame > drop && x.Frame <= drop + 4).ToList(); // the first ticks, above the magnet.
            Assert.True(afterDrop.Count == 4 && afterDrop.All(x => x.ForceZ == -32768), detail);
            for (var i = 1; i < afterDrop.Count; i++)
            {
                Assert.True(afterDrop[i].PosZ - afterDrop[i - 1].PosZ == -32768, detail);
            }

            var after = samples.Where(x => x.Frame > wait[^1]).ToList();
            Assert.True(after.Count > 0 && after.All(x => x.ForceZ == 0 && x.PosZ == rest), detail);
            Assert.True(rest < afterDrop[0].PosZ, detail);
            Assert.Equal(1, samples[^1].IsOnGround);
        }

        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // T-B9 - map 10, B[9], Septimus and the block (chapter 16), and the counter-proof of O-E19-24
    // ----------------------------------------------------------------------------------------------------------

    private static readonly int[] B9Flags = { 216, 485, 1666, 1667, 1668, 1669 };

    private static ArcSpec B9Spec(int frameLimit) => new(
        "T-B9", "Overworld", "Overworld 2,1-10", B9Flags, 37, 46, 1, frameLimit, RealController: true, Prefabs: true);

    /// <summary>
    /// T-B9 (G216, G485, G1666 to G1669, G482 off): the hero is placed at (898, 736, 16) in the tile (37, 46). B[9]'s hero program (@1132) walks
    /// him south then west and parks him; Septimus (record 39, C program @3636) walks up the column to the block rec41 (<c>0x24 @3670</c>); the hero then
    /// walks north against rec41 (<c>0x24 @1212</c>), west, and destroys it (<c>0x2E @1226</c>); the scene ends with G482 and <c>0x11 @3738</c>.
    /// Without the contacts both walk through rec41 to the wall of the cell model (<c>0x0B @1215</c> and <c>0x0B @3673</c> never end).
    /// </summary>
    [Fact]
    public void TB9_SeptimusAndTheBlockOnMap10_BothStopAtRec41_TheSceneSetsG482()
    {
        using var arc = new ArcRun(B9Spec(3200));
        var s = new ArcSamples(arc, new[] { 39, 41 }, new uint[] { 510, 511, 512, 514, 515 },
            (C, 3636, 3670), (B, 1132, 1212), (C, 3636, 3673), (B, 1132, 1215), (C, 3636, 3693), (C, 3636, 3725), (C, 3636, 3731), (C, 3636, 3734));
        AlundraEntityScriptProxy? rec41 = null;
        int destroyFrame = -1;
        var inList = new Dictionary<int, bool>();
        var previousOnInstruction = arc.OnInstruction;
        arc.OnInstruction = t =>
        {
            previousOnInstruction!(t);
            if (t.Slot == B && t.Pc == 1226 && destroyFrame < 0)
            {
                destroyFrame = t.Frame;
                rec41 = arc.Entities.First(e => e.EntityRefId == 41);
            }
        };
        var previousOnFrame = arc.OnFrame;
        arc.OnFrame = () =>
        {
            previousOnFrame!();
            if (rec41 != null)
            {
                inList[arc.Frame] = ((IAlundraScriptHost)arc.Proxy).Collidables.Contains(rec41);
            }
        };
        arc.PlaceHero(898, 736, 16);

        // 1. The end signal.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 3738, 0x11), "C[39] executes 0x11 @3738");

        // 2. Only the instructions the measure shows are skipped.
        AssertSkippedWithin(arc, new HashSet<(int, int)>
        {
            (0x90, 2689), (0x2B, 6413), (0x95, 6418), (0x90, 2828), (0x4C, 3833), (0x4C, 3847), (0x4D, 3849),
        });

        // 3. The rest, in the order of the plan.
        var at3670 = s[C, 3636, 3670];
        Assert.Equal((59006976, 39190528), (at3670.Rec(39).X, at3670.Rec(39).Y)); // Septimus against rec41.
        Assert.Equal(at3670.Rec(39).Y - 6 * Px, at3670.Rec(41).Y + 8 * Px);

        var at1212 = s[B, 1132, 1212];
        Assert.Equal((58851328, 39256064), (at1212.Hero.X, at1212.Hero.Y)); // the hero against rec41.
        Assert.Equal(at1212.Hero.Y - 7 * Px, at1212.Rec(41).Y + 8 * Px);
        Assert.Equal(898 * Px, at1212.Hero.X);

        AssertWithin(s[C, 3636, 3673].Rec(39).X, 54214656, 163840, "Septimus ends 0x0B @3673 (x)");
        var x1215 = s[B, 1132, 1215].Hero.X;
        Assert.True(x1215 >= 898 * Px - 52 * Px - Px / 2 && x1215 <= 898 * Px - 48 * Px - Px / 2, $"the hero ends 0x0B @1215 at x = {x1215 / 65536.0} px, 845.5 to 849.5 expected");

        // rec41 destroyed by 0x2E @1226, and out of the list of obstacles when that frame ends (samples keyed by the counter after the frame:
        // destroyFrame + 1 is the end of the frame of the destruction, the exact frame that follows).
        Assert.True(destroyFrame >= 0, "0x2E @1226 never ran");
        Assert.False(inList[destroyFrame + 1], "rec41 is still an obstacle at the end of the frame of its destruction");

        foreach (var n in new uint[] { 511, 512, 514, 515 })
        {
            Assert.True(s.FirstSet.ContainsKey(n), $"T{n} never set");
        }

        foreach (var pc in new[] { 3693, 3725, 3731, 3734 })
        {
            Assert.True(s.After.ContainsKey((C, 3636, pc)), $"0x0B/0x24 @{pc} never ended");
        }

        Assert.True(IsSet(482), "G482");
        AssertNoUnexpectedError(arc);
    }

    /// <summary>
    /// The counter-proof of D-E19-37 (O-E19-24): with the hero placed at (898, 744, 16), the south half of the tile (37, 46), his two walks park him in
    /// the way of Septimus, who stops against him and never sets T510: the scene stalls, as in the original emulated. Reproduced and noted, to be reopened
    /// when chapter 16 is playable.
    /// </summary>
    [Fact]
    public void TB9_CounterProof_AHeroPlacedInTheSouthHalfOfTheTileParksInTheWayOfSeptimus_TheSceneStalls()
    {
        using var arc = new ArcRun(B9Spec(1500));
        var s = new ArcSamples(arc, new[] { 39 }, new uint[] { 510 });
        arc.PlaceHero(898, 744, 16);

        while (arc.Frame < 1400)
        {
            if (AlundraDialogueDirector.Instance.IsOpen)
            {
                arc.Press(AlundraPadState.Square);
            }
            else
            {
                arc.OneFrame();
            }
        }

        var septimus = arc.EntityByRecord(39)!;
        Assert.False(s.FirstSet.ContainsKey(510), "T510 was set: the scene did not stall");
        Assert.Equal((57409536, 51912704), (septimus.PosX, septimus.PosY));
        Assert.Equal(septimus.PosY - 6 * Px, arc.Hero.PosY + 8 * Px); // against the parked hero, edge against edge.
        AssertWithin(arc.Hero.PosX, 57094144, 163840, "the parked hero (x)");
        AssertWithin(arc.Hero.PosY, 50995200, 163840, "the parked hero (y)");
        Assert.False(IsSet(482), "G482");
    }

    // ----------------------------------------------------------------------------------------------------------
    // TN-3 - map 346, the platform against the iron-ball wall: the one faithful block of the sites the contacts would close (D-E19-34, D-E19-35)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec N3Spec => new(
        "TN-3", "Lake Shrine", "Lake Shrine (inner)-346", Array.Empty<int>(), 0, 0, 0, 1200, RealController: true, Prefabs: true,
        Arrival: new ArcArrival((6 * 24 + 12) << 16, (50 * 16 + 8) << 16, 2 << 20, AlundraGameState.ResetAnimationId, 16));

    /// <summary>
    /// TN-3 (G1022 off): the hero arrives in the zone (tiles x 0 to 15, y 40 to 59), which spawns the platform (record 7, the large staircase trap). Its C program
    /// (@936) walks it south, <c>0x1E @943</c> from (96, 752). The iron-ball wall rec10 (84, 824) is solid until E14 (D-E19-34) and the binary blocks there too:
    /// the platform stops at (96, 800) against it and does not move for 500 ticks, <c>ForceAdjusted</c> raised, no navigation detour engaged or latched (an
    /// entity is in contact, D-E19-35), and <c>0x1E</c> never ends.
    /// </summary>
    [Fact]
    public void TN3_ThePlatformOfMap346_StopsAtTheIronBallWall_AndStaysThere_WithoutDetour()
    {
        using var arc = new ArcRun(N3Spec);
        (int X, int Y)? start = null;
        arc.OnInstruction = t =>
        {
            if (t.Slot == C && t.Pc == 943 && t.Opcode == 0x1E && start == null)
            {
                var platform = arc.EntityByRecord(7)!;
                start = (platform.PosX, platform.PosY);
            }
        };

        // The end signal: the platform at the wall, then 500 ticks without a move.
        arc.RunUntil(() => arc.EntityByRecord(7) is { } p && p.PosY >= 800 * Px, "the platform reaches the wall rec10");
        Assert.Equal((96 * Px, 752 * Px), start);

        var platform = arc.EntityByRecord(7)!;
        var wall = arc.EntityByRecord(10)!;
        Assert.Equal((96 * Px, 800 * Px), (platform.PosX, platform.PosY));
        Assert.Equal(platform.PosY + 16 * Px, wall.PosY - 8 * Px); // edge against edge.

        for (var tick = 0; tick < 500; tick++)
        {
            arc.OneFrame();
            Assert.Equal((96 * Px, 800 * Px), (platform.PosX, platform.PosY));
            Assert.Equal(1, platform.ForceAdjusted);
            Assert.Same(wall, platform.XCollisionEntity);
            Assert.Null(platform.WalkDetourPath);
            Assert.False(platform.WalkDetourAttempted);
        }

        Assert.DoesNotContain(arc.Trace, t => t.Slot == C && t.ProgramStart == 936 && t.Pc > 943); // 0x1E @943 never ends.
        AssertSkippedWithin(arc, new HashSet<(int, int)> { (0x3F, 805), (0x3F, 981) });
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // TH4 - the arrivals of the chain overlap no entity (D-E19-36)
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec ArrivalSpec(string name) => name switch
    {
        "A20" => new ArcSpec("TH4-A20", "Inoa", "Inoa-162", Array.Empty<int>(), 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(47972352, 27787264, 0, AlundraGameState.ResetAnimationId, 0)),
        "A10" => new ArcSpec("TH4-A10", "Inoa", "Inoa (inner)-165", Array.Empty<int>(), 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(19660800, 23592960, 0, AlundraGameState.ResetAnimationId, 16)),
        "T-A19" => new ArcSpec("TH4-A19", "Inoa", "Inoa (inner)-185", new[] { 204, 120, 121, 122, 123 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(60555264, 24641536, 0, AlundraGameState.ResetAnimationId, 16)),

        // The preset day3-after-dream (docs/test-saves.md): map 179, tile (17, 7), z 1, flags G203, G1651 and G1660.
        "day3-after-dream" => new ArcSpec("TH4-day3-after-dream", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0)),

        // E19.e E4: the other arrivals of the day 3 chain. The first five are the `0x53` of the previous arc (the direction is the operand's), the sixth
        // the portal 135.0 and the seventh the portal 176.7 (a portal's direction is the hero's when he walks in: not a plan value, the box does not rotate).
        "A13" => new ArcSpec("TH4-A13", "Inoa", "Inoa-176", new[] { 203, 1651, 1652, 1660 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(11796480, 36175872, 10485760, AlundraGameState.ResetAnimationId, 24)),
        "A14" => new ArcSpec("TH4-A14", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1652, 1653, 1660 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(27525120, 7864320, 1048576, AlundraGameState.ResetAnimationId, 0)),
        "A15" => new ArcSpec("TH4-A15", "Inoa", "Inoa-176", new[] { 203, 1651, 1652, 1653, 1654, 1660 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(8650752, 35127296, 10485760, AlundraGameState.ResetAnimationId, 0)),
        "A10J" => new ArcSpec("TH4-A10J", "Overworld", "Overworld 2,1-10", new[] { 1654, 203 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(16515072, 61341696, 0, AlundraGameState.ResetAnimationId, 16)),
        "A17" => new ArcSpec("TH4-A17", "Church", "Church-135", new[] { 203, 1654 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(30670848, 54001664, 1048576, AlundraGameState.ResetAnimationId, 16)),
        "135-to-10" => new ArcSpec("TH4-135-to-10", "Overworld", "Overworld 2,1-10", new[] { 203, 1654, 1655, 14 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(47972352, 50855936, 0, AlundraGameState.ResetAnimationId, 0)),
        "A18" => new ArcSpec("TH4-A18", "Inoa", "Inoa (inner)-178", new[] { 203, 1654, 1655 }, 0, 0, 0, 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(60555264, 26738688, 0, AlundraGameState.ResetAnimationId, 16)),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>
    /// TH4 (D-E19-36): a mover that already overlaps an entity stays blocked, as in the binary, with no exit rule: the arrivals of the chain must therefore
    /// overlap nothing. At the first frame after the adoption, the rule of the binary finds no obstacle for the hero of the arrivals of A20 (162), A10 (165),
    /// T-A19 (185), of the preset <c>day3-after-dream</c> (179) and (E19.e E4) of the seven other arrivals of the day 3 chain: A13 (176), A14 (179), A15 (176),
    /// A10J (10), A17 (135), the portal 135.0 (10) and the portal 176.7 (178).
    /// </summary>
    [Theory]
    [InlineData("A20")]
    [InlineData("A10")]
    [InlineData("T-A19")]
    [InlineData("day3-after-dream")]
    [InlineData("A13")]
    [InlineData("A14")]
    [InlineData("A15")]
    [InlineData("A10J")]
    [InlineData("A17")]
    [InlineData("135-to-10")]
    [InlineData("A18")]
    public void TH4_TheArrivalOfTheChainOverlapsNoEntity(string arrival)
    {
        using var arc = new ArcRun(ArrivalSpec(arrival));
        arc.OneFrame();

        var collidables = ((IAlundraScriptHost)arc.Proxy).Collidables;
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(arc.Hero, collidables));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(arc.Hero, arc.Hero.PosX, arc.Hero.PosY, arc.Hero.PosZ, collidables, EntityFlags.PickupKindMask));
    }

    // ----------------------------------------------------------------------------------------------------------
    // T-C61 - map 61, B[6], the rails of the mine cart
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec C61Spec => new(
        "T-C61", "Coal Mine", "Coal Mine (First Entrance)-61", new[] { 784 }, 0, 0, 0, 700, RealController: true, Prefabs: true,
        Arrival: new ArcArrival(588 << 16, 648 << 16, 2 << 20, AlundraGameState.ResetAnimationId, 0));

    /// <summary>
    /// T-C61 (G784 set, G670 off): the hero arrives by portal 61.1 at (588, 648). B[6] (program @872) walks him between the blocks rec32 to rec35
    /// (<c>0x24 @929</c>, @934, <c>0x1E @937</c>, <c>@943</c>, <c>0x24 @959</c>, @964, @977, <c>0x1E @980</c>) and sets G672. The arc stops there: what follows
    /// depends on the edge of the map and on the bits of class of the walker (O-E19-17, O-E19-26), which the DLL does not carry.
    /// </summary>
    [Fact]
    public void TC61_TheCartRailsOnMap61_TheHeroStopsAtEachBlock_TheSceneSetsG672()
    {
        using var arc = new ArcRun(C61Spec);
        var s = new ArcSamples(arc, new[] { 32, 33, 34, 35 }, Array.Empty<uint>(),
            (B, 872, 929), (B, 872, 934), (B, 872, 959), (B, 872, 964), (B, 872, 977), (B, 872, 980));

        // E19.d2c1 C2: the iron grid rec9 (prefab 396c008e, appears at the load, animation 0 of impulse -32768, no gravity) keeps IsZForceApplied 0 and ForceZ 0
        // and stays at its height at every frame of the arc: no impulse at its appearance (R2) and the stop marker 0x8000 would hold it anyway (R3).
        var gridSamples = new List<(int Frame, int PosZ, int ForceZ, int IsZForceApplied)>();
        var sampleEachFrame = arc.OnFrame!;
        arc.OnFrame = () =>
        {
            sampleEachFrame();
            if (arc.EntityByRecord(9) is { } grid)
            {
                gridSamples.Add((arc.Frame, grid.PosZ, grid.ForceZ, grid.IsZForceApplied));
            }
        };

        // 1. The end signal: the instruction that ends the scene, 0x30 @983 (it tests G672, in the frame of the end of 0x1E @980; 0x05 @990 is the one that sets it).
        arc.RunUntil(() => arc.Has(B, 983, 0x30), "B[6] executes 0x30 @983 (G672) after 0x1E @980");

        // 2. Only the instructions the measure shows are skipped (the rest of B[6] is not played).
        AssertSkippedWithin(arc, new HashSet<(int, int)> { (0x5D, 988), (0x52, 994), (0x29, 882), (0x2B, 1958) });

        // 3. The rest, in the order of the plan.
        var at929 = s[B, 872, 929];
        Assert.Equal((588 * Px, 36110336), (at929.Hero.X, at929.Hero.Y));
        Assert.Equal(at929.Hero.Y - 7 * Px, at929.Rec(32).Y + 8 * Px);

        var at934 = s[B, 872, 934];
        Assert.Equal((27394048, 36110336), (at934.Hero.X, at934.Hero.Y));
        Assert.Equal(at934.Hero.X - 10 * Px, at934.Rec(33).X + 12 * Px);

        var at959 = s[B, 872, 959];
        Assert.Equal((17956864, 45547520), (at959.Hero.X, at959.Hero.Y));
        Assert.Equal(at959.Hero.X - 10 * Px, at959.Rec(34).X + 12 * Px);

        var at964 = s[B, 872, 964];
        Assert.Equal((17956864, 50855936), (at964.Hero.X, at964.Hero.Y));
        Assert.Equal(at964.Hero.Y + 8 * Px, at964.Rec(35).Y - 8 * Px);

        var at977 = s[B, 872, 977]; // against rec34, moved by 0x64 @965 to (156, 776).
        Assert.Equal((11665408, 50855936), (at977.Hero.X, at977.Hero.Y));
        Assert.Equal(at977.Hero.X - 10 * Px, at977.Rec(34).X + 12 * Px);

        var at980 = s[B, 872, 980];
        Assert.Equal((11665408, 55050240), (at980.Hero.X, at980.Hero.Y));
        Assert.True(IsSet(672), "G672");

        Assert.True(gridSamples.Count > 100, "the grid rec9 was sampled at every frame of the arc");
        Assert.All(gridSamples, g =>
        {
            Assert.Equal(0, g.IsZForceApplied);
            Assert.Equal(0, g.ForceZ);
            Assert.Equal(gridSamples[0].PosZ, g.PosZ);
        });
        AssertNoUnexpectedError(arc);
    }
}
