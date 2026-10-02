#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d2a S4 (docs/plan-e19-opcodes.md section 1.2h.1, section 1.3): the arcs of the first day after the book, on the real
/// exported maps 162, 165 and 164 of Inoa, with the export's real prefabs and the real hero. A20 (Sybill, 162) and A10 (first
/// visit to Meade, 165) start from a real portal arrival (S3, U3); A11 (Septimus, 164) places the hero as the plan says. Every
/// expected value is written by hand before the code; a contradicted value is a stop, never a re-pin. The frame limits and the
/// pinned frames (+/- 3) come from the first measurement. Order of the checks: the end signal, the skipped or exceeded set, then
/// the rest in the order of the plan. The positions that depend on contacts between entities are not pinned here (E19.d2b).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraInoaDayOneArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int F = ScriptHelper.ProgramFInteract;

    // ----------------------------------------------------------------------------------------------------------
    // A20 - map 162, Sybill
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A20Spec => new(
        "A20", "Inoa", "Inoa-162", Array.Empty<int>(), 0, 0, 0, 160,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(47972352, 27787264, 0, AlundraGameState.ResetAnimationId, 0));

    /// <summary>
    /// A20 (no flag): the hero arrives by portal 163.0 at (732, 424). The map event B[7] (<c>@572</c>) locks the player, makes Sybill
    /// (record 2, <c>0x43 [2]</c>) the logic entity, walks her to the right (<c>0x0B @584 [1, 192, 0]</c>) from her start (540, 472),
    /// opens a box, then <c>0x11 @596</c> gives the hand back and <c>0x05 @597</c> sets G1659 in the same call. B[9] only holds eleven
    /// <c>0xA2</c> (effects, not ported), each skipped once at the first tick. The walk overshoots its target: the first pass of
    /// <c>@588</c> reads the final X, measured here (E19.d2b re-measures it).
    /// </summary>
    [Fact]
    public void A20_SybillOnMap162_WalksToTheHero_ThePlayerGetsTheHandBack()
    {
        using var arc = new ArcRun(A20Spec);
        (int X, int Y)? sybillAt584 = null;
        (int X, int Y)? sybillAt588 = null;
        arc.OnInstruction = t =>
        {
            if (t.Slot != B)
            {
                return;
            }

            if (t.Pc == 584 && sybillAt584 == null)
            {
                var s = arc.EntityByRecord(2)!;
                sybillAt584 = (s.PosX, s.PosY);
            }

            if (t.Pc == 588 && sybillAt588 == null)
            {
                var s = arc.EntityByRecord(2)!;
                sybillAt588 = (s.PosX, s.PosY);
            }
        };

        // 1. The end signal (the box is closed by one press of Square per frame).
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 596, 0x11), "B[7] executes 0x11 @596");

        // 2. Exactly the eleven 0xA2 of B[9] skipped, nothing else, nothing cut off by the loop guard.
        var effects = Enumerable.Range(0, 11).Select(i => 608 + 8 * i).ToArray();
        AssertSkippedWithin(arc, effects.Select(pc => (0xA2, pc)).ToHashSet());
        foreach (var pc in effects)
        {
            // E19.d2b B7: counted among the SKIPPED instructions (the trace also holds the executions, which this never asked): once each, at the first tick.
            Assert.Equal(new[] { 0 }, arc.SkippedOrExceeded.Where(t => t.Opcode == 0xA2 && t.Pc == pc).Select(t => t.Frame).ToArray());
        }

        // 3. The rest, in the order of the plan.
        Assert.Equal((540 << 16, 472 << 16), sybillAt584);
        Assert.NotNull(sybillAt588);
        Assert.True(sybillAt588!.Value.X >= 732 << 16, $"Sybill ends her walk at x = {sybillAt588.Value.X / 65536.0} px, 732 or more expected");
        Assert.Equal((48021504, 472 << 16), sybillAt588); // measured on the first pass: 732.75 px (overshoot of 0.75 px).
        AssertFrame(arc, B, 596, 134);
        var endFrame = FrameOf(arc, B, 596);
        Assert.Equal(new[] { endFrame }, FramesOf(arc, B, 597, 0x05).ToArray()); // G1659 in the same call.
        Assert.True(IsSet(1659), "G1659");
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A10 - map 165, first visit
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A10Spec => new(
        "A10", "Inoa", "Inoa (inner)-165", Array.Empty<int>(), 0, 0, 0, 1000,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(19660800, 23592960, 0, AlundraGameState.ResetAnimationId, 16));

    private sealed class A10Samples
    {
        public readonly Dictionary<int, (int X, int Y)> RecordAfter0x64 = new();
        public string? CellsAtStart;
        public string? CellsAtFirstTickOfRec5;
        public string? CellsAfter721;
        public readonly List<(uint Flag, int Set, int Cleared)> FlagEdges = new();
        private readonly Dictionary<uint, int> _openSince = new();
        private readonly Dictionary<uint, bool> _was = new();

        /// <summary>Samples the five flags at the end of every frame: rising and falling edges, in order.</summary>
        public void TakeFrame(ArcRun arc)
        {
            for (uint number = 101; number <= 105; number++)
            {
                var on = IsTemporarySet(number);
                _was.TryGetValue(number, out var was);
                if (on && !was)
                {
                    _openSince[number] = arc.Frame;
                }
                else if (!on && was)
                {
                    FlagEdges.Add((number, _openSince[number], arc.Frame));
                }

                _was[number] = on;
            }
        }
    }

    /// <summary>
    /// A10 (no flag): the hero arrives by portal 162.3 on map 165 (the first visit to Meade's house). B[1] sees the hero at tile (12, 22)
    /// (height 1: U3), places five actors, sets T100; B[2] then plays the visit, handing the scene to the actors one temporary flag at
    /// a time (T101, T102, T103, T104, T102, T105), each cleared by its own actor; <c>0x11 @354</c> ends it with G3 set. The two
    /// <c>0x25</c> of Bergus (<c>@838</c>, <c>@843</c>) are executed since E19.d2c1 C1 (R4): each returns 1 at its first call, Bergus resting on
    /// the ground; the jumps themselves (the impulse) come with C2, which re-pins the frames of the arc.
    /// </summary>
    [Fact]
    public void A10_TheFirstVisitOnMap165_TheActorsPassTheSceneOnByFlags_ThePlayerGetsTheHandBack()
    {
        using var arc = new ArcRun(A10Spec);
        var store = (AlundraCellStore)arc.Proxy.CellMutator!;
        string Cells() => string.Join(",", Enumerable.Range(23, 4).Select(y => store.GetWalkability(12, y).ToString("X2")));
        var samples = new A10Samples { CellsAtStart = Cells() };
        arc.OnFrame = () => samples.TakeFrame(arc);
        arc.OnInstruction = t =>
        {
            if (t.Slot == B && t.ProgramStart == 96)
            {
                var record = t.Pc switch { 140 => 0, 148 => 1, 156 => 2, 164 => 3, 172 => 5, _ => -1 };
                if (record >= 0 && !samples.RecordAfter0x64.ContainsKey(record))
                {
                    var e = arc.EntityByRecord(record)!;
                    samples.RecordAfter0x64[record] = (e.PosX, e.PosY);
                }
            }

            if (t.Slot == C && t.ProgramStart == 648)
            {
                if (t.Pc == 668 && samples.CellsAtFirstTickOfRec5 == null)
                {
                    samples.CellsAtFirstTickOfRec5 = Cells();
                }

                if (t.Pc == 739 && samples.CellsAfter721 == null)
                {
                    samples.CellsAfter721 = Cells();
                }
            }
        };

        // 1. The end signal: 0x11 @354 of the program @236 (slot B); the boxes are closed by one press per frame.
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 354, 0x11), "B[2] executes 0x11 @354");

        // 2. Nothing skipped, nothing cut off by the loop guard (E19.d2c1 R4: 0x25 is ported). Each 0x25 of Bergus executes once and returns 1
        // at its first call: Bergus does not leave the ground yet (no impulse before the next task), so he rests with CollidedWithEntityZ raised
        // (R5 a) and the instruction after it runs in the same call.
        AssertNothingSkippedOrExceeded(arc);
        foreach (var pc in new[] { 838, 843 })
        {
            Assert.Equal(1, FramesOf(arc, C, pc, 0x25).Count);
            Assert.Equal(FramesOf(arc, C, pc, 0x25)[0], FrameOf(arc, C, pc + 1));
        }

        // 3. The rest, in the order of the plan.
        AssertFrame(arc, B, 354, 922);
        Assert.Equal(236, arc.Trace.First(t => t.Slot == B && t.Pc == 354 && t.Opcode == 0x11).ProgramStart); // E19.d2b B7: the program @236, as the plan says.
        var endFrame = FrameOf(arc, B, 354);
        Assert.Equal(new[] { endFrame }, FramesOf(arc, B, 351, 0x05).ToArray()); // G3 in the same image.
        Assert.True(IsSet(3), "G3");
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen, "a box is still open at the end");

        // First call of B[1]: 0x3B @106 returns 1 (0x00 @121 closes the call, the program goes on at @116 and not at @232),
        // 0x69 @122 is reached at the second call, the 0x64 writes at the third.
        var first = FrameOf(arc, B, 106);
        Assert.True(arc.Has(B, 116, 0x30), "B[1] did not go on to @116 after 0x3B @106");
        Assert.DoesNotContain(arc.Trace, t => t.Slot == B && t.Pc == 232 && t.Frame == first); // not the end at @232 on the first call.
        Assert.Equal(first, FrameOf(arc, B, 121));
        Assert.Equal(first + 1, FrameOf(arc, B, 122));
        Assert.Equal(first + 1, FrameOf(arc, B, 139));
        Assert.Equal(first + 2, FrameOf(arc, B, 140));
        Assert.Equal(first + 2, FrameOf(arc, B, 180));

        // The positions written by 0x64 (@140-@172), exact in X and Y; T100 set @180.
        Assert.Equal((29884416, 7864320), samples.RecordAfter0x64[0]);
        Assert.Equal((11796480, 16252928), samples.RecordAfter0x64[1]);
        Assert.Equal((27525120, 6815744), samples.RecordAfter0x64[2]);
        Assert.Equal((25952256, 8912896), samples.RecordAfter0x64[3]);
        Assert.Equal((19660800, 28049408), samples.RecordAfter0x64[5]);
        Assert.True(arc.Has(B, 180, 0x05), "T100 never set by 0x05 @180");

        // The cells (12, 23) to (12, 26): 0x41 before, 0x00 at the first tick of rec5 C[1] (after its four 0x55), 0x41 again after @721.
        Assert.Equal("41,41,41,41", samples.CellsAtStart);
        Assert.Equal("00,00,00,00", samples.CellsAtFirstTickOfRec5);
        Assert.Equal("41,41,41,41", samples.CellsAfter721);
        Assert.True(arc.Has(C, 721, 0x54), "0x54 @721 never ran");

        // The temporary flags, in this order, each cleared by its own actor before the next is set.
        Assert.Equal(new uint[] { 101, 102, 103, 104, 102, 105 }, samples.FlagEdges.Select(e => e.Flag).ToArray());
        for (var i = 0; i + 1 < samples.FlagEdges.Count; i++)
        {
            Assert.True(
                samples.FlagEdges[i].Set < samples.FlagEdges[i].Cleared && samples.FlagEdges[i].Cleared <= samples.FlagEdges[i + 1].Set,
                $"T{samples.FlagEdges[i].Flag}: set at frame {samples.FlagEdges[i].Set}, cleared at {samples.FlagEdges[i].Cleared}, next flag set at {samples.FlagEdges[i + 1].Set}");
        }

        // The actors (slot C, the clearing 0x06): rec5 @703 (program @648), Wendell @774 (program @740, twice), Nestus @944 (program
        // @880), Bergus @847 (program @804), Meade @1026 (program @984). The frame of each edge is the frame after the 0x06.
        var actors = new (uint Flag, int Program, int Pc, int Occurrence)[]
        {
            (101, 648, 703, 0), (102, 740, 774, 0), (103, 880, 944, 0), (104, 804, 847, 0), (102, 740, 774, 1), (105, 984, 1026, 0),
        };
        for (var i = 0; i < actors.Length; i++)
        {
            var (flag, program, pc, occurrence) = actors[i];
            var runs = arc.Trace.Where(t => t.Slot == C && t.ProgramStart == program && t.Pc == pc && t.Opcode == 0x06).ToList();
            Assert.True(runs.Count > occurrence, $"T{flag}: 0x06 @{pc} of the program @{program} ran {runs.Count} time(s)");
            Assert.Equal(runs[occurrence].Frame + 1, samples.FlagEdges[i].Cleared);
        }

        Assert.Equal(2, arc.Trace.Count(t => t.Slot == C && t.ProgramStart == 740 && t.Pc == 774 && t.Opcode == 0x06));
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A11 - map 164, Septimus
    // ----------------------------------------------------------------------------------------------------------

    private static ArcSpec A11Spec => new(
        "A11", "Inoa", "Inoa (inner)-164", new[] { 3, 201 }, 44, 7, 1, 240, RealController: true, Prefabs: true);

    /// <summary>
    /// A11 (G3 and G201 set): the hero touches Septimus (record 1, at (1092, 120)); the contact entity is record 1 (as in A9) and the
    /// player presses Square. Its F program sets T0; C[2] (<c>@240</c>) opens box 130 (closed by presses), sets T6 then T2; the hero
    /// placed at (996, 120, 16), tile (41, 7), enters the zone of B[1], which sets T1 (<c>@83</c>) and spawns record 4 next to the hero
    /// (<c>0x8B</c>). C[2] then opens box 131 (<c>0x0D @325</c>, which sets T200), waits for the page to turn (T201, set by the node
    /// <c>M164_S003</c> at the first press: the director never turns a page alone, <c>0x4D @350</c> being skipped), plays the scene,
    /// opens box 132 and ends with <c>0x38 @437</c>.
    /// <para>
    /// E19.d2b (B3, D-E19-27): re-pinned under the entity contacts. Septimus (record 1) is stopped by the entities of his path: <c>0x24 @341</c> (south)
    /// ends against record 4 (spawned at (1092, 136)), <c>0x24 @346</c> (west) ends against the HERO, parked in the zone at (996, 120) (his place in
    /// the zone decides the x of this contact: the relation is pinned, not the number's origin), and <c>0x0B @386</c> (east, 24 px) ends where the
    /// retard of D-E19-13 leaves it. Everything that follows <c>@341</c> comes 19 frames earlier (Septimus no longer walks to the wall of the cell model),
    /// and record 4, deactivated by C[3], is destroyed by the native E handler the frame after (D-E19-29). The frames before <c>@341</c> do not move.
    /// </para>
    /// </summary>
    [Fact]
    public void A11_SeptimusOnMap164_TheScenePlaysToItsEnd_TheTableAndTheFlagsAreSet()
    {
        using var arc = new ArcRun(A11Spec);
        (int X, int Y, int Z)? heroAtSpawn = null;
        (int X, int Y, int Z)? record4AtSpawn = null;
        var samples = new ArcSamples(arc, new[] { 1, 4 }, new uint[] { 200, 201 }, (C, 240, 341), (C, 240, 346), (C, 240, 386));
        var sampleOnInstruction = arc.OnInstruction!;
        arc.OnInstruction = t =>
        {
            sampleOnInstruction(t);
            if (t.Slot == B && t.Pc == 95 && record4AtSpawn == null && arc.EntityByRecord(4) is { } r4)
            {
                heroAtSpawn = (arc.Hero.PosX, arc.Hero.PosY, arc.Hero.PosZ);
                record4AtSpawn = (r4.PosX, r4.PosY, r4.PosZ);
            }
        };

        // The contacts of Septimus (record 1): every entity that shortened one of his steps, read from the report of his controller at the end of each frame.
        var sampleOnFrame = arc.OnFrame!;
        var septimusContacts = new HashSet<string>();
        arc.OnFrame = () =>
        {
            sampleOnFrame();
            var contact = arc.EntityByRecord(1)?.Controller?.LastContact;
            foreach (var obstacle in new[] { contact?.H1Obstacle, contact?.H2Obstacle })
            {
                if (obstacle?.GameplayProxy is AlundraEntityScriptProxy proxy)
                {
                    septimusContacts.Add(ReferenceEquals(proxy, arc.Hero) ? "hero" : $"rec{proxy.EntityRefId}");
                }
            }
        };

        // 1. The end signal. The contact entity is record 1 (the production seam), then Square; box 130 is closed by presses and
        // sets T6 then T2; the hero is then placed in the zone of B[1].
        arc.OneFrame();
        arc.OneFrame();
        ((IAlundraScriptHost)arc.Proxy).ActiveCollisionEntity = arc.EntityByRecord(1);
        arc.Press(AlundraPadState.Square);
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => IsTemporarySet(2), "T2 set by C[2] @289");
        arc.PlaceHero(996, 120, 16);
        Assert.Equal((41, 7, 1), (arc.Hero.TileX, arc.Hero.TileY, arc.Hero.TileZ));
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 437, 0x38), "C[2] executes 0x38 @437");

        // 2. Skipped: the four 0x4C and the nine 0x4D of the plan, once each; 0x58 @110 (the C programs of Beaumont and Thyea, through
        // @100) as many times as it takes while the hero is not near them - never pinned. Nothing else, nothing cut off by the guard.
        var textOnce = new HashSet<(int, int)>
        {
            (0x4C, 331), (0x4C, 351), (0x4C, 356), (0x4C, 402),
            (0x4D, 350), (0x4D, 361), (0x4D, 373), (0x4D, 376), (0x4D, 379), (0x4D, 382), (0x4D, 392), (0x4D, 395), (0x4D, 398),
        };
        AssertSkippedWithin(arc, textOnce.Append((0x58, 110)).ToHashSet());
        foreach (var (opcode, pc) in textOnce)
        {
            Assert.Equal(1, arc.SkippedOrExceeded.Count(t => t.Opcode == opcode && t.Pc == pc));
        }

        Assert.True(arc.SkippedOrExceeded.Count(t => t.Opcode == 0x58 && t.Pc == 110) > 0, "0x58 @110 never skipped");

        // 3. The rest, in the order of the plan.
        // The interaction: record 1's F program ran once (T0).
        Assert.Equal(1, arc.Trace.Count(t => t.Slot == F && t.Pc == 576 && t.Opcode == 0x05));

        // Box 130 closed by presses; T6 then T2 set; hero placed at (996, 120, 16), tile (41, 7); T1 set @83.
        Assert.True(arc.Trace.Count(t => t.Slot == C && t.Pc == 276 && t.Opcode == 0x0D) == 1, "box 130 (0x0D @276) not opened exactly once");
        Assert.True(arc.FirstIndexOf(C, 286, 0x05) < arc.FirstIndexOf(C, 289, 0x05), "T6 (@286) must be set before T2 (@289)");
        Assert.True(arc.Has(B, 83, 0x05), "T1 never set by 0x05 @83");

        // Record 4 appeared next to the hero (0x8B [129, 4, 96, 0, 16, 0, 0, 0]: the match is the hero, offset (96, 16, 0) px).
        Assert.NotNull(heroAtSpawn);
        Assert.Equal((heroAtSpawn!.Value.X + (96 << 16), heroAtSpawn.Value.Y + (16 << 16), heroAtSpawn.Value.Z), record4AtSpawn);
        Assert.Equal((996 << 16, 120 << 16, 16 << 16), heroAtSpawn);

        // T200 set at the opening of box 131 (0x0D @325); T201 at the first press that turns the page, before the wait @353 ends.
        Assert.Equal(1, arc.Trace.Count(t => t.Slot == C && t.Pc == 325 && t.Opcode == 0x0D));
        Assert.True(arc.Has(C, 353, 0x36), "C[2] never reached the wait for T201 (0x36 @353)");
        Assert.Equal(1, arc.Trace.Count(t => t.Slot == C && t.Pc == 417 && t.Opcode == 0x0D)); // box 132.

        // The end: 0x38 @437 of C[2] (program @240); G4, G8, G202 set, G201 cleared, [162] = 169, no lock, T3 set, record 4 deactivated.
        Assert.Equal(240, arc.Trace.First(t => t.Slot == C && t.Pc == 437 && t.Opcode == 0x38).ProgramStart);
        AssertFrame(arc, C, 437, 189);
        Assert.True(IsSet(4), "G4");
        Assert.True(IsSet(8), "G8");
        Assert.True(IsSet(202), "G202");
        Assert.False(IsSet(201), "G201");
        Assert.Equal((ushort)169, ArcRun.State.MapIdToInternalMapIndexTable[162]);
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);
        Assert.True(IsTemporarySet(3), "T3");
        Assert.True(arc.Has(C, 451, 0x19), "C[3] never deactivated record 4 (0x19 @451)");
        AssertFrame(arc, C, 451, 189);
        Assert.Equal(EntityStatus.Deactivated, arc.EntityByRecord(4)!.Status); // at the frame of the end ...
        arc.OneFrame();
        Assert.Equal(EntityStatus.FlagToDestroy, arc.EntityByRecord(4)!.Status); // ... and destroyed by the native E handler the frame after (D-E19-29).

        // The contacts of Septimus, in the order of the plan: record 4 and the hero, nothing else.
        Assert.Equal(new[] { "hero", "rec4" }, septimusContacts.OrderBy(c => c).ToArray());
        var at341 = samples[C, 240, 341];
        Assert.Equal((71565312, 7995392), (at341.Rec(1).X, at341.Rec(1).Y)); // south, against record 4.
        Assert.Equal(at341.Rec(1).Y + 6 * 65536, at341.Rec(4).Y - 8 * 65536);
        var at346 = samples[C, 240, 346];
        Assert.Equal((66650112, 7995392), (at346.Rec(1).X, at346.Rec(1).Y)); // west, against the hero.
        Assert.Equal(1, at346.Rec(1).ForceAdjusted);
        Assert.Equal(at346.Rec(1).X - 10 * 65536, at346.Hero.X + 11 * 65536); // edge against edge, whatever the place of the hero in the zone.
        Assert.True(samples[C, 240, 386].Rec(1).X >= 68222976, "Septimus ends 0x0B @386 at x = 1041 px or more");

        // T200 is seen set at the frame of the opening of box 131, T201 at the first press (the frames 12 and 13).
        Assert.Equal(FrameOf(arc, C, 325) + 1, samples.FirstSet[200]);
        Assert.Equal(samples.FirstSet[200] + 1, samples.FirstSet[201]);

        // The absolute frames (first measurement, +/- 3).
        foreach (var (slot, pc, frame) in new[] { (C, 276, 5), (C, 289, 9), (B, 86, 10), (C, 325, 11), (C, 353, 48), (C, 417, 175) })
        {
            AssertFrame(arc, slot, pc, frame);
        }

        AssertNoUnexpectedError(arc);
    }
}
