#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.c1 T2 (docs/plan-e19-opcodes.md §1.2e, §1.3): the arcs of the vision of Lars and Melzas on map 478 (A3), of the
/// walk of Jess on map 416 (A7) and A4 on the real prefabs (A4p, in <see cref="AlundraVisionArcTests"/>). A3 and A7 run
/// on the export's real prefabs (<see cref="ArcSpec.Prefabs"/>, D-E19-14): the NPCs have their real controller, so the
/// rise of the camera block and the walks meet the production code. Every expected value is written by hand before the
/// code (two independent models of the plan), and includes the kept animation lag (D-E19-13): a contradicted value is a
/// stop, never a re-pin.
/// <para>
/// Order of the checks (T2): the end signal (<see cref="ArcRun.RunUntil"/>, which names the last instruction of every
/// program when it fails); then the set of skipped or exceeded opcodes, which must be empty; then the rest in the order of
/// the plan. Samples taken at one instruction are recorded DURING the run (<see cref="ArcRun.OnInstruction"/> sees the
/// instruction after its effects) and asserted after the end signal. The end of a <c>0x0B</c> is read at the NEXT
/// instruction: the trace carries no handler result and the first execution of a <c>0x0B</c> is its suspended call.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraVisionAndCoastArcTests
{
    private const int BProgram = ScriptHelper.ProgramBMap;
    private const int CProgram = ScriptHelper.ProgramCTick;

    private static ArcSpec A3Spec => new(
        "A3", "Inoa", "Inoa (Vision Event from Lars and Melzas cutscene)-478", new[] { 1641 }, 22, 57, 1, 2700,
        RealController: true, Prefabs: true);

    private static ArcSpec A7Spec => new(
        "A7", "Coast", "Coast beginning-416", Array.Empty<int>(), 41, 49, 1, 2200, RealController: true, Prefabs: true);

    private static int FrameOf(ArcRun arc, int slot, int pc, int opcode)
    {
        var index = arc.FirstIndexOf(slot, pc, opcode);
        Assert.True(index >= 0, $"0x{opcode:X2} @{pc} (slot {slot}) never ran");
        return arc.Trace[index].Frame;
    }

    private static void AssertFrame(ArcRun arc, int slot, int pc, int opcode, int expected)
    {
        var frame = FrameOf(arc, slot, pc, opcode);
        Assert.True(Math.Abs(frame - expected) <= 3, $"0x{opcode:X2} @{pc} first ran at frame {frame}, {expected} (+/- 3) expected");
    }

    private static void AssertNothingSkippedOrExceeded(ArcRun arc)
    {
        var offenders = arc.SkippedOrExceeded.Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})").Distinct().ToList();
        Assert.True(offenders.Count == 0, "skipped or exceeded instructions: " + string.Join("; ", offenders));
    }

    private static void AssertNoUnexpectedError(ArcRun arc)
    {
        var unexpected = arc.Log!.Errors
            .Where(e => !e.StartsWith("AnimatedSpriteComponent : can't resolve sprite", StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, "errors logged: " + string.Join(" | ", unexpected.Take(3)));
        Assert.DoesNotContain(arc.Log.Warnings, m => m.Contains("falling back to a bare entity"));
    }

    // ----------------------------------------------------------------------------------------------------------
    // A3
    // ----------------------------------------------------------------------------------------------------------

    private sealed record BlockAtFlag(int TileZ, int PosX, int PosY, int PosZ);

    private sealed class A3Samples
    {
        public readonly Dictionary<int, BlockAtFlag> BlockAtFirstFlag = new();
        public uint? BlockFlagsAfter63;
        public int? BlockForceZAfter5E;
        public readonly Dictionary<int, (int X, int Y)> HeroAtTeleport = new();
        public uint? YuriDirectionAfterFirstLoop;
        public uint? YuriDirectionAfterSecondLoop;
        public int DogSamples;
        public int DogEqualsBlock11;

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot == CProgram && t.Opcode == 0x05 && t.Pc is 437 or 442 or 447 or 452 or 457 && !BlockAtFirstFlag.ContainsKey(t.Pc))
            {
                var block = arc.EntityByRecord(0);
                if (block != null)
                {
                    BlockAtFirstFlag[t.Pc] = new BlockAtFlag(block.TileZ, block.PosX, block.PosY, block.PosZ);
                }
            }

            if (t.Slot == CProgram && t.Pc == 359 && t.Opcode == 0x63 && BlockFlagsAfter63 == null)
            {
                BlockFlagsAfter63 = arc.EntityByRecord(0)?.Flags;
            }

            if (t.Slot == CProgram && t.Pc == 363 && t.Opcode == 0x5E && BlockForceZAfter5E == null)
            {
                BlockForceZAfter5E = arc.EntityByRecord(0)?.ForceZ;
            }

            if (t.Slot == BProgram && t.Pc is 166 or 183 or 194 or 205 or 216 && t.Opcode == 0x64 && !HeroAtTeleport.ContainsKey(t.Pc))
            {
                HeroAtTeleport[t.Pc] = (arc.Hero.PosX, arc.Hero.PosY);
            }

            // Yuri (record 8): the first instruction after each turning loop of C[8] (the Wait that follows its 0x74).
            if (t.Slot == CProgram && t.Pc == 552 && YuriDirectionAfterFirstLoop == null)
            {
                YuriDirectionAfterFirstLoop = arc.EntityByRecord(8)?.TargetDirection;
            }

            if (t.Slot == CProgram && t.Pc == 581 && YuriDirectionAfterSecondLoop == null)
            {
                YuriDirectionAfterSecondLoop = arc.EntityByRecord(8)?.TargetDirection;
            }

            if (t.Slot == CProgram && t.Pc == 836 && t.Opcode == 0x89)
            {
                var dog = arc.EntityByRecord(13);
                var block11 = arc.EntityByRecord(11);
                DogSamples++;
                if (dog != null && block11 != null && dog.PosX == block11.PosX && dog.PosY == block11.PosY)
                {
                    DogEqualsBlock11++;
                }
            }
        }
    }

    /// <summary>
    /// A3 (map 478, <c>G1641</c>): the dog of record 5 gives the camera block (record 0) a rise of 0.375 px per tick
    /// (<c>0x63 @359</c>, <c>0x5E [0,96,0] @363</c>), polls its <c>TileZ</c> every frame (<c>0x07</c> from @377) and raises
    /// T20 to T60, which B1 (@164) waits for while it teleports the hero (@166 to @216); then the white fade, the two waits,
    /// <c>G1641</c> (@242) and <c>0x53 [220,1,0,0,0,0,0] @245</c> back to map 476.
    /// </summary>
    [Fact]
    public void A3_TheVisionOnMap478_TheBlockRises_T20ToT60Fall_ThenTheWarpBackToMap476Departs()
    {
        using var arc = new ArcRun(A3Spec);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new A3Samples();
        arc.OnInstruction = t => samples.Take(arc, t);

        // 1. The end signal.
        arc.RunUntil(() => arc.Has(BProgram, 245, 0x53), "B1 executes 0x53 @245 towards map 476");

        // 2. Nothing skipped, nothing cut off by the loop guard.
        AssertNothingSkippedOrExceeded(arc);

        // 3. The rest, in the order of the plan.
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(476u, arrival.MapIndex);
        Assert.Equal(786432, arrival.PosX);
        Assert.Equal(524288, arrival.PosY);
        Assert.Equal(0, arrival.PosZ);
        Assert.Equal(0, arrival.EffectId);
        Assert.True(arc.Has(BProgram, 242, 0x05), "0x05 @242 (G1641) never ran: G1641 is set from the start, its flag would prove nothing");

        // The first execution of every 0x05: T0, T10, T20, T30, T40, T50, T60, then G1641 right before the 0x53.
        var firstFlags = arc.Trace.Where(t => t.Opcode == 0x05 && t.Kind == EventTraceKind.Implemented)
            .Select(t => (t.Slot, t.Pc)).Distinct().ToArray();
        Assert.Equal(
            new[] { (BProgram, 177), (CProgram, 370), (CProgram, 437), (CProgram, 442), (CProgram, 447), (CProgram, 452), (CProgram, 457), (BProgram, 242) },
            firstFlags);

        var t20 = FrameOf(arc, CProgram, 437, 0x05);
        var t30 = FrameOf(arc, CProgram, 442, 0x05);
        var t40 = FrameOf(arc, CProgram, 447, 0x05);
        var t50 = FrameOf(arc, CProgram, 452, 0x05);
        var t60 = FrameOf(arc, CProgram, 457, 0x05);
        Assert.Equal(new[] { 555, 512, 256, 384 }, new[] { t30 - t20, t40 - t30, t50 - t40, t60 - t50 });
        AssertFrame(arc, CProgram, 437, 0x05, 301);
        AssertFrame(arc, CProgram, 442, 0x05, 856);
        AssertFrame(arc, CProgram, 447, 0x05, 1368);
        AssertFrame(arc, CProgram, 452, 0x05, 1624);
        AssertFrame(arc, CProgram, 457, 0x05, 2008);
        AssertFrame(arc, BProgram, 245, 0x53, 2262);

        // The block at the first execution of each flag (TileZ, position, PosZ: whole pixels since E19.h1b1, the spawn adds no "+1").
        Assert.Equal(
            new[] { 8, 21, 33, 39, 48 },
            new[] { 437, 442, 447, 452, 457 }.Select(pc => samples.BlockAtFirstFlag[pc].TileZ).ToArray());
        foreach (var pc in new[] { 437, 442, 447, 452, 457 })
        {
            Assert.Equal(42467328, samples.BlockAtFirstFlag[pc].PosX);
            Assert.Equal(60293120, samples.BlockAtFirstFlag[pc].PosY);
        }

        Assert.Equal(
            new[] { 8421376, 22061056, 34643968, 40935424, 50372608 },
            new[] { 437, 442, 447, 452, 457 }.Select(pc => samples.BlockAtFirstFlag[pc].PosZ).ToArray());

        Assert.Equal(0x6000u, samples.BlockFlagsAfter63);
        Assert.Equal(24576, samples.BlockForceZAfter5E);

        // The hero, read at the teleports of B1 (X and Y only: Z depends on the controller).
        Assert.Equal((35389440, 60293120), samples.HeroAtTeleport[166]);
        Assert.Equal((36962304, 49807360), samples.HeroAtTeleport[183]);
        Assert.Equal((38535168, 38273024), samples.HeroAtTeleport[194]);
        Assert.Equal((47972352, 33030144), samples.HeroAtTeleport[205]);
        Assert.Equal((35389440, 26738688), samples.HeroAtTeleport[216]);

        // The cosmetic programs: C[11] and C[12] leave their laps after T50, Yuri turns, the dog follows block 11, Ronan's
        // animation (a Hold) ends through the real sprite.
        var afterT50 = arc.Trace.Select((t, i) => (t, i)).First(x => x.t.Slot == CProgram && x.t.Pc == 452 && x.t.Opcode == 0x05).i;
        Assert.True(arc.HasAfter(afterT50, CProgram, 725, 0x54), "C[11] never left its lap (0x54 @725)");
        Assert.True(arc.HasAfter(afterT50, CProgram, 802, 0x0B), "C[12] never left its lap (0x0B @802)");
        foreach (var pc in new[] { 547, 576 })
        {
            Assert.Equal(16, arc.Trace.Count(t => t.Slot == CProgram && t.Pc == pc && t.Opcode == 0x08));
        }

        Assert.Equal(16u, samples.YuriDirectionAfterFirstLoop);
        Assert.Equal(0u, samples.YuriDirectionAfterSecondLoop);
        Assert.True(samples.DogSamples > 0, "0x89 @836 never ran");
        Assert.Equal(samples.DogSamples, samples.DogEqualsBlock11);
        Assert.True(arc.Trace.Any(t => t.Slot == CProgram && t.Pc == 856), "Ronan's program never passed 0x1C @854");

        // E19.c2: Ronan's laps (1A 06 @852; 1C [1] @854; 1A 00 @856; Wait 41 ticks) on the exact end of his Hold of 24 ticks. The
        // first 0x1A @852 runs at frame 1; in the binary the wait ends 25 ticks after it (the end of the Hold is seen one tick after
        // it happens), so a lap is 25 + 41 = 66 frames. The three checks, in this order.
        var ronanStarts = arc.Trace.Where(t => t.Slot == CProgram && t.Pc == 852 && t.Opcode == 0x1A).Select(t => t.Frame).ToList();
        var ronanRestarts = arc.Trace.Where(t => t.Slot == CProgram && t.Pc == 856 && t.Opcode == 0x1A).Select(t => t.Frame).ToList();

        // (1) the first 0x1A @856 runs at frame 26.
        Assert.Equal(26, ronanRestarts.First());

        // (2) every 0x1A @856 runs exactly 25 frames after the 0x1A @852 that precedes it.
        Assert.Equal(
            Enumerable.Repeat(25, ronanRestarts.Count),
            ronanRestarts.Select(restart => restart - ronanStarts.Last(start => start < restart)).ToList());

        // (3) two successive 0x1A @852 are 66 frames apart, up to T60.
        var lapStarts = ronanStarts.Where(start => start <= t60).ToList();
        Assert.True(lapStarts.Count > 1, "Ronan did not start two laps before T60");
        Assert.Equal(
            Enumerable.Repeat(66, lapStarts.Count - 1),
            lapStarts.Zip(lapStarts.Skip(1), (first, next) => next - first).ToList());

        // The end: no box, the hero still locked.
        Assert.Equal(0u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);
        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, ArcRun.State.PlayerControlFlags);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A7
    // ----------------------------------------------------------------------------------------------------------

    private sealed class A7Samples
    {
        public (int X, int Y)? HeroAt421;
        public uint? HeroAnimAt429;
        public uint? HeroDirectionAt429;
        public (int X, int Y)? BlockAt663;
        public int EntitiesOfRecord1At671;
        public (int X, int Y, int Z)? JessAt671;
        public uint? JessDirectionAt671;
        public bool JessHasControllerAt671;
        public readonly Dictionary<int, (int X, int Y, int ForceAdjusted)> JessAfterWalk = new();
        public (int X, int Y)? JessAt53;
        public (int X, int Y)? BlockAt53;
        public int FramesWithTheHeroChecked;

        /// <summary>E19.c2 (hygiene of E19.c1): from the frame of <c>0x64 @421</c> to the end the hero stays where B1 put him, checked at the end of every frame.</summary>
        public void AssertTheHeroDidNotMove(ArcRun arc)
        {
            if (HeroAt421 is not { } expected)
            {
                return;
            }

            FramesWithTheHeroChecked++;
            Assert.True((arc.Hero.PosX, arc.Hero.PosY) == expected, $"the hero moved at frame {arc.Frame}: ({arc.Hero.PosX}, {arc.Hero.PosY}), expected {expected}");
        }

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot == BProgram && t.Pc == 421 && t.Opcode == 0x64 && HeroAt421 == null)
            {
                HeroAt421 = (arc.Hero.PosX, arc.Hero.PosY);
            }

            if (t.Slot == BProgram && t.Pc == 429 && t.Opcode == 0x5B && HeroAnimAt429 == null)
            {
                HeroAnimAt429 = arc.Hero.TargetAnimationId;
                HeroDirectionAt429 = arc.Hero.TargetDirection;
            }

            if (t.Slot != CProgram)
            {
                return;
            }

            if (t.Pc == 663 && t.Opcode == 0x1A && BlockAt663 == null)
            {
                var block = arc.EntityByRecord(0);
                BlockAt663 = block == null ? null : (block.PosX, block.PosY);
            }

            if (t.Pc == 671 && t.Opcode == 0x2D && JessAt671 == null)
            {
                EntitiesOfRecord1At671 = arc.Entities.Count(e => e.EntityRefId == 1);
                var jess = arc.EntityByRecord(1);
                if (jess != null)
                {
                    JessAt671 = (jess.PosX, jess.PosY, jess.PosZ);
                    JessDirectionAt671 = jess.TargetDirection;
                    JessHasControllerAt671 = jess.Controller != null;
                }
            }

            // The end of each walk of Jess, read at the instruction that follows it.
            if (t.Pc is 576 or 590 or 598 or 612 && !JessAfterWalk.ContainsKey(t.Pc))
            {
                var jess = arc.EntityByRecord(1);
                if (jess != null)
                {
                    JessAfterWalk[t.Pc] = (jess.PosX, jess.PosY, jess.ForceAdjusted);
                }
            }

            if (t.Pc == 640 && t.Opcode == 0x53)
            {
                var jess = arc.EntityByRecord(1);
                var block = arc.EntityByRecord(0);
                JessAt53 = jess == null ? null : (jess.PosX, jess.PosY);
                BlockAt53 = block == null ? null : (block.PosX, block.PosY);
            }
        }
    }

    /// <summary>
    /// A7 (map 416, entered by 391's <c>0x53 @540</c>): B1 lays the hero down and sets T0 (@506); the block (C[100]) walks
    /// 192 px north (<c>0x0B @659</c>), then <c>0x2D [1] @671</c> makes Jess appear; Jess walks four legs (<c>0x0B</c> @572,
    /// @586, @594, @608) and <c>0x53 [163,0,40,9,2,2,0] @640</c> loads map 163.
    /// </summary>
    [Fact]
    public void A7_TheCoastOnMap416_TheBlockWalksNorth_JessAppearsAndWalks_ThenTheWarpToMap163Departs()
    {
        using var arc = new ArcRun(A7Spec);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new A7Samples();
        arc.OnInstruction = t => samples.Take(arc, t);

        // 1. The end signal.
        arc.RunUntil(() =>
        {
            samples.AssertTheHeroDidNotMove(arc);
            return arc.Has(CProgram, 640, 0x53);
        }, "Jess (C[1]) executes 0x53 @640 towards map 163");

        // 2. Nothing skipped, nothing cut off by the loop guard.
        AssertNothingSkippedOrExceeded(arc);

        // 3. The rest, in the order of the plan.
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(163u, arrival.MapIndex);
        Assert.Equal(63700992, arrival.PosX);
        Assert.Equal(9961472, arrival.PosY);
        Assert.Equal(2097152, arrival.PosZ);
        Assert.Equal(2, arrival.EffectId);
        AssertFrame(arc, CProgram, 640, 0x53, 1759);

        // B1: T0 at 493, the hero where 0x64 @421 put him, turned by 0x5B @429.
        AssertFrame(arc, BProgram, 506, 0x05, 493);
        Assert.Equal((65273856, 49807360), samples.HeroAt421);
        Assert.Equal((65273856, 49807360), (arc.Hero.PosX, arc.Hero.PosY));
        Assert.True(samples.FramesWithTheHeroChecked > 1000, $"the hero was checked on {samples.FramesWithTheHeroChecked} frames only");
        Assert.Equal(78u, samples.HeroAnimAt429);
        Assert.Equal(16u, samples.HeroDirectionAt429);

        // The block: first call of 0x0B @659 at 494, its end (read at 0x1A @663) 385 frames later, at (1020, 760) px.
        AssertFrame(arc, CProgram, 659, 0x0B, 494);
        var walkStart = FrameOf(arc, CProgram, 659, 0x0B);
        var walkEnd = FrameOf(arc, CProgram, 663, 0x1A);
        Assert.Equal(385, walkEnd - walkStart);
        Assert.Equal((66846720, 49807360), samples.BlockAt663);

        // 0x2D @671, 183 frames after the end of the block's walk: Jess, once.
        Assert.Equal(183, FrameOf(arc, CProgram, 671, 0x2D) - walkEnd);
        Assert.Equal(1, samples.EntitiesOfRecord1At671);
        Assert.Equal((57409536, 42991616), (samples.JessAt671!.Value.X, samples.JessAt671.Value.Y));
        Assert.Equal(16, samples.JessAt671.Value.Z >> 16);
        Assert.Equal(24u, samples.JessDirectionAt671);
        Assert.True(samples.JessHasControllerAt671, "Jess spawned by 0x2D has no controller");

        // The four walks of Jess finish in order, ForceAdjusted back to 0.
        Assert.Equal(62128128, samples.JessAfterWalk[576].X);
        AssertFrame(arc, CProgram, 576, 0x5B, 1161);
        Assert.Equal(47251456, samples.JessAfterWalk[590].Y);
        AssertFrame(arc, CProgram, 590, 0x5B, 1288);
        Assert.Equal((65323008, 47284224), (samples.JessAfterWalk[598].X, samples.JessAfterWalk[598].Y));
        AssertFrame(arc, CProgram, 598, 0x5B, 1321);
        Assert.Equal((65372160, 48365568), (samples.JessAfterWalk[612].X, samples.JessAfterWalk[612].Y));
        AssertFrame(arc, CProgram, 612, 0x1A, 1385);
        foreach (var pc in new[] { 576, 590, 598, 612 })
        {
            Assert.Equal(0, samples.JessAfterWalk[pc].ForceAdjusted);
        }

        // At the warp: Jess and the block (the extra half pixel is the kept animation lag, D-E19-13).
        Assert.Equal((65372160, 48398336), samples.JessAt53);
        Assert.Equal((66846720, 49774592), samples.BlockAt53);

        // The end: no box, the hero still locked.
        Assert.Equal(0u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);
        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, ArcRun.State.PlayerControlFlags);
        AssertNoUnexpectedError(arc);
    }

    // ----------------------------------------------------------------------------------------------------------
    // A9
    // ----------------------------------------------------------------------------------------------------------

    private const int FProgram = ScriptHelper.ProgramFInteract;

    private static ArcSpec A9Spec => new(
        "A9", "Inoa", "Inoa (inner)-172", Array.Empty<int>(), 36, 18, 2, 400, RealController: true, Prefabs: true);

    private sealed class A9Samples
    {
        public uint? WendellTargetAfter537;
        public uint? FlagsAfter534;
        public int? WendellCounterAfter541;
        public uint? WendellTargetAfter541;

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot != CProgram)
            {
                return;
            }

            if (t.Pc == 534 && t.Opcode == 0x0D && FlagsAfter534 == null)
            {
                FlagsAfter534 = ArcRun.State.PlayerControlFlags;
            }

            if (t.Pc == 537 && t.Opcode == 0x1A && WendellTargetAfter537 == null)
            {
                WendellTargetAfter537 = arc.EntityByRecord(6)?.TargetAnimationId;
            }

            if (t.Pc == 541 && t.Opcode == 0x1A && WendellCounterAfter541 == null)
            {
                WendellCounterAfter541 = arc.EntityByRecord(6)?.AnimCompleteCounter;
                WendellTargetAfter541 = arc.EntityByRecord(6)?.TargetAnimationId;
            }
        }
    }

    /// <summary>
    /// A9 (map 172, Inoa, no flag): the player talks to Wendell through the invisible trigger of record 4 (the production seam of the
    /// interaction, <see cref="IAlundraScriptHost.ActiveCollisionEntity"/>): its slot F program sets T0 (<c>0x05 @1840</c>), which releases
    /// Wendell's tick program C[6] (@504). It locks the player (<c>0x10 @530</c>), opens the box (<c>0x0D @534</c>), plays Wendell's
    /// animation 11, a Loop of 90 ticks (<c>0x1A @537</c>), and waits for one turn of it (<c>0x1C [1] @539</c>), which the binary sees 91
    /// ticks after the <c>0x1A</c>; then <c>0x1A [10] @541</c>, <c>0x39 @543</c>, <c>0x06 @544</c> clears T0 and <c>0x11 @547</c> gives the hand
    /// back. Before E19.c2 the wait never saw the turn and the player stayed locked for ever (the P1 introduced by E19.c1).
    /// </summary>
    [Fact]
    public void A9_WendellOnMap172_TheWaitOnALoopSeesItsTurn_ThePlayerGetsTheHandBack()
    {
        using var arc = new ArcRun(A9Spec);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new A9Samples();
        arc.OnInstruction = t => samples.Take(arc, t);

        // 1. The end signal: the interaction, the lock and the box (closed at once, one press per page), then the unlock.
        arc.OneFrame();
        arc.OneFrame();
        ((IAlundraScriptHost)arc.Proxy).ActiveCollisionEntity = arc.EntityByRecord(4);
        arc.RunUntil(() => arc.Has(CProgram, 530, 0x10), "Wendell (C[6]) executes 0x10 @530 after the interaction");
        var f530 = FrameOf(arc, CProgram, 530, 0x10);
        arc.CloseDialogueWithTheButton("the box of Wendell closes");
        var frameAfterTheBoxClosed = arc.Frame;
        arc.RunUntil(() => arc.Has(CProgram, 547, 0x11), "Wendell executes 0x11 @547");

        // 2. Nothing skipped, nothing cut off by the loop guard.
        AssertNothingSkippedOrExceeded(arc);

        // 3. The rest, in the order of the plan.
        Assert.Equal(1, arc.Trace.Count(t => t.Slot == FProgram && t.Pc == 1840 && t.Opcode == 0x05));
        Assert.InRange(f530, 1, 10);
        Assert.Equal(f530, FrameOf(arc, CProgram, 534, 0x0D));
        Assert.Equal(f530, FrameOf(arc, CProgram, 537, 0x1A));
        Assert.Equal(f530, FrameOf(arc, CProgram, 539, 0x1C));
        Assert.Equal(11u, samples.WendellTargetAfter537);
        Assert.Equal(0x14u, samples.FlagsAfter534);

        // The wait ends 91 frames after the 0x1A (a Loop of 90 ticks, seen one tick after its turn).
        Assert.Equal(f530 + 91, FrameOf(arc, CProgram, 541, 0x1A));
        Assert.Equal(0, samples.WendellCounterAfter541);
        Assert.Equal(10u, samples.WendellTargetAfter541);

        // E19.f2a: the box of Wendell is a script-entity box typed at the binary's pace (first glyph at f530 + 18, the typing done at f530 + 98, the close
        // triggered by the helper's presses at f530 + 100, the release at f530 + 118) and Wendell's 0x39 waits for it: the wait of 91 frames ends BEFORE the
        // box is released, so the rest of the program comes at f530 + 119, one tick after the release (an entity script sees it a tick late, F2-R1). The 0x39
        // is first executed at f530 + 91 (FrameOf gives its first execution); it blocks until the release.
        Assert.Equal(f530 + 119, frameAfterTheBoxClosed);
        Assert.Equal(f530 + 91, FrameOf(arc, CProgram, 543, 0x39));
        Assert.Equal(f530 + 119, FrameOf(arc, CProgram, 544, 0x06));
        Assert.Equal(f530 + 119, FrameOf(arc, CProgram, 547, 0x11));

        // The end: the hand is back, T0 is clear, one box opened.
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.Equal(0u, ArcRun.State.GetFlag(0x8000) & 1u);
        Assert.Equal(1u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        AssertNoUnexpectedError(arc);
    }
}
