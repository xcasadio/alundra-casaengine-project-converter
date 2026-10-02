#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d D0/D7 (docs/plan-e19-opcodes.md §1.2g, §1.3): arc A8 on the real exported map 163 (Inoa, inner), with the export's real
/// prefabs and the real hero. Map event 0 (B[1] @60, the hero owns it) plays the whole awakening: the hero's two animations
/// (Hold 32 and Hold 40, waited on by <c>0x1C</c>), the switch of the logic entity to Jess (<c>0x43 [0]</c>), five boxes closed
/// by the button (one press per frame), Jess walking south to the wall (<c>0x24 @183</c>), her destruction, the hero's own walk,
/// and <c>0x11 @201</c>. The shop's objects (records 3, 4, 5, 6 and 8) cut their own script with <c>0x40</c> and <c>0x41</c>. Every
/// expected value is written by hand before the code: a contradicted value is a stop, never a re-pin.
/// <para>
/// Order of the checks: the end signal, the skipped or exceeded set (empty), then the rest in the order of the plan. Samples
/// taken at one instruction are recorded during the run (<see cref="ArcRun.OnInstruction"/>) and asserted after the end signal.
/// The frames depend on the protocol of the boxes (pages, one press per frame): the typewriter (E12.c) or the name box (E19.f)
/// will shift them, not the gaps.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraInoaAwakeningArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private static readonly int[] ShopRecords = { 3, 4, 5, 6, 8 };

    private static ArcSpec A8Spec => new(
        "A8", "Inoa", "Inoa (inner)-163", Array.Empty<int>(), 40, 9, 2, 1300, RealController: true, Prefabs: true);

    private sealed class Samples
    {
        public (int X, int Y, int Z)? HeroAt73;
        public (int X, int Y)? HeroAt73Pose;
        public bool HeroWalkStarted;
        public readonly List<string> HeroMovedWhileWaiting = new();
        public uint? HeroAnimAfter98;
        public uint? HeroAnimAfter106;
        public bool? LogicIsJessAfter115;
        public bool? LogicIsHeroAfter184;
        public uint? JessAnimAfter135;
        public int? JessAnimCompleteAt139;
        public (int X, int Y, int Z, int TileZ, uint Mask)? JessAt143;
        public (int X, int Y, int ForceAdjusted)? JessAt147;
        public int? JessPosYAt155;
        public (int X, int Y, int ForceAdjusted)? JessAt161;
        public int? JessPosYAt181;
        public (int X, int Y, int ForceAdjusted)? JessAt184;
        public EntityStatus? JessStatusAfter185;
        public (int X, int Y)? HeroAt190;
        public (uint Anim, uint Direction)? HeroTargetAfter190;
        public (int X, int Y)? HeroAt194;
        public int? FrameOfFirstShopCheck;
        public readonly List<string> ShopFailures = new();
        public bool ShopChecked;

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot != B)
            {
                return;
            }

            switch (t.Pc)
            {
                case 73 when HeroAt73 == null:
                    HeroAt73 = (arc.Hero.PosX, arc.Hero.PosY, arc.Hero.PosZ);
                    HeroAt73Pose = (arc.Hero.PosX, arc.Hero.PosY);
                    break;
                case 98 when HeroAnimAfter98 == null:
                    HeroAnimAfter98 = arc.Hero.TargetAnimationId;
                    break;
                case 106 when HeroAnimAfter106 == null:
                    HeroAnimAfter106 = arc.Hero.TargetAnimationId;
                    break;
                case 115 when LogicIsJessAfter115 == null:
                    LogicIsJessAfter115 = ReferenceEquals(arc.Hero.LogicEntity, arc.EntityByRecord(0)) && arc.EntityByRecord(0) != null;
                    break;
                case 135 when JessAnimAfter135 == null:
                    JessAnimAfter135 = arc.EntityByRecord(0)?.TargetAnimationId;
                    break;
                case 139 when JessAnimCompleteAt139 == null:
                    JessAnimCompleteAt139 = arc.EntityByRecord(0)?.AnimCompleteCounter;
                    break;
                case 143 when JessAt143 == null:
                    var j143 = arc.EntityByRecord(0);
                    JessAt143 = j143 == null ? null : (j143.PosX, j143.PosY, j143.PosZ, j143.TileZ, j143.Controller?.Settings.WalkabilityMask ?? uint.MaxValue);
                    break;
                case 147 when JessAt147 == null:
                    var j147 = arc.EntityByRecord(0);
                    JessAt147 = j147 == null ? null : (j147.PosX, j147.PosY, j147.ForceAdjusted);
                    break;
                case 155 when JessPosYAt155 == null:
                    JessPosYAt155 = arc.EntityByRecord(0)?.PosY;
                    break;
                case 161 when JessAt161 == null:
                    var j161 = arc.EntityByRecord(0);
                    JessAt161 = j161 == null ? null : (j161.PosX, j161.PosY, j161.ForceAdjusted);
                    break;
                case 181 when JessPosYAt181 == null:
                    JessPosYAt181 = arc.EntityByRecord(0)?.PosY;
                    break;
                case 184 when JessAt184 == null:
                    var j184 = arc.EntityByRecord(0);
                    JessAt184 = j184 == null ? null : (j184.PosX, j184.PosY, j184.ForceAdjusted);
                    LogicIsHeroAfter184 = ReferenceEquals(arc.Hero.LogicEntity ?? arc.Hero, arc.Hero);
                    break;
                case 185 when JessStatusAfter185 == null:
                    JessStatusAfter185 = arc.EntityByRecord(0)?.Status;
                    break;
                case 190 when HeroAt190 == null:
                    HeroAt190 = (arc.Hero.PosX, arc.Hero.PosY);
                    HeroTargetAfter190 = (arc.Hero.TargetAnimationId, arc.Hero.TargetDirection);
                    HeroWalkStarted = true;
                    break;
                case 194 when HeroAt194 == null:
                    HeroAt194 = (arc.Hero.PosX, arc.Hero.PosY);
                    break;
            }
        }

        /// <summary>Per-frame invariants, at the end of every frame: the hero does not move from <c>@73</c> to the first call of
        /// <c>@190</c>; the shop's objects have cut their scripts (once the five have run <c>(2, 734, 0xFF)</c>).</summary>
        public void TakeFrame(ArcRun arc)
        {
            if (HeroAt73Pose is { } expected && !HeroWalkStarted && (arc.Hero.PosX, arc.Hero.PosY) != expected)
            {
                HeroMovedWhileWaiting.Add($"frame {arc.Frame}: ({arc.Hero.PosX}, {arc.Hero.PosY}), expected {expected}");
            }

            if (!ShopChecked && arc.Trace.Count(t => t.Slot == C && t.Pc == 734 && t.Opcode == 0xFF) >= 5)
            {
                ShopChecked = true;
                FrameOfFirstShopCheck = arc.Frame;
                CheckShop(arc);
            }
        }

        private void CheckShop(ArcRun arc)
        {
            foreach (var record in ShopRecords)
            {
                var shop = arc.EntityByRecord(record);
                if (shop == null)
                {
                    ShopFailures.Add($"record {record}: absent");
                    continue;
                }

                var failures = new List<string>();
                if (shop.ProgramIndexes[C] != 0)
                {
                    failures.Add($"ProgramIndexes[2] = {shop.ProgramIndexes[C]}");
                }

                if (shop.SpriteProgramIndexes[0] != 0)
                {
                    failures.Add($"SpriteProgramIndexes[0] = {shop.SpriteProgramIndexes[0]}");
                }

                if (shop.SpriteProgramIndexes[2] != 4)
                {
                    failures.Add($"SpriteProgramIndexes[2] = {shop.SpriteProgramIndexes[2]}");
                }

                if (shop.EventProgramState.Codes != null)
                {
                    failures.Add("EventProgramState.Codes not null");
                }

                if (failures.Count > 0)
                {
                    ShopFailures.Add($"record {record}: " + string.Join(", ", failures));
                }
            }
        }
    }

    [Fact]
    public void A8_TheAwakeningOnMap163_JessWalksToTheWall_TheHeroTakesBackTheHand()
    {
        using var arc = new ArcRun(A8Spec);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new Samples();
        arc.OnInstruction = t => samples.Take(arc, t);
        arc.OnFrame = () => samples.TakeFrame(arc);

        // 1. The end signal (the five boxes are closed by one press of Square per frame).
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 201, 0x11), "B[1] executes 0x11 @201");

        // 2. Nothing skipped or exceeded.
        AssertNothingSkippedOrExceeded(arc);

        // 3. The rest, in the order of the plan.
        AssertFrame(arc, B, 201, 1025);
        var endFrame = FrameOf(arc, B, 201);
        foreach (var (pc, opcode) in new[] { (198, 0x05), (202, 0x06), (205, 0x05), (208, 0x05) })
        {
            Assert.Equal(new[] { endFrame }, FramesOf(arc, B, pc, opcode).ToArray());
        }

        Assert.True(IsSet(0), "G0");
        Assert.True(IsSet(201), "G201");
        Assert.True(IsSet(1662), "G1662");
        Assert.False(IsSet(200), "G200");
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.Equal(5u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);

        // The book: record 7, present from frame 0.
        var book = arc.EntityByRecord(7);
        Assert.NotNull(book);
        Assert.Equal(237, book!.SpriteType);
        Assert.Equal(EntityStatus.Normal, book.Status);
        Assert.Equal(72, book.SpriteProgramIndexes[C]);
        Assert.True(arc.Runner.SaveBookEventRunCount > 0, "the save book never ran");

        // The hero during the waits.
        Assert.Equal((62914560, 9961472, 2359297), samples.HeroAt73);
        Assert.True(samples.HeroMovedWhileWaiting.Count == 0, "the hero moved before @190: " + string.Join("; ", samples.HeroMovedWhileWaiting.Take(3)));
        AssertGap(arc, B, 98, 102, 99);
        AssertGap(arc, B, 106, 110, 41);
        Assert.Equal(83u, samples.HeroAnimAfter98);
        Assert.Equal(85u, samples.HeroAnimAfter106);

        // The logic entity: Jess after @115, the hero after @184.
        Assert.True(samples.LogicIsJessAfter115, "after 0x43 @115 the logic entity of the hero is not record 0");
        Assert.True(samples.LogicIsHeroAfter184, "after 0x42 @184 the logic entity is not the hero");

        // Jess's first animation, then her two walks.
        AssertGap(arc, B, 135, 139, 25);
        Assert.Equal(12u, samples.JessAnimAfter135);
        Assert.Equal(0, samples.JessAnimCompleteAt139);
        Assert.Equal((60555264, 8912896, 2097152, 2, 0x40u), samples.JessAt143);
        Assert.Equal((60555264, 15237120, 0), samples.JessAt147);
        AssertGap(arc, B, 143, 147, 98);
        Assert.Equal(15302656, samples.JessPosYAt155);
        Assert.Equal((60555264, 14221312, 0), samples.JessAt161);
        AssertGap(arc, B, 157, 161, 18);
        Assert.Equal(14155776, samples.JessPosYAt181);

        // 0x24: Jess meets the wall at PosY 297.0 px, 84 frames after the first call.
        Assert.Equal((60555264, 19464192, 1), samples.JessAt184);
        AssertGap(arc, B, 183, 184, 84);
        Assert.Equal(85, FramesOf(arc, B, 183, 0x24).Count);
        Assert.Equal(EntityStatus.FlagToDestroy, samples.JessStatusAfter185);

        // The hero's own walk.
        Assert.Equal((62914560, 9961472), samples.HeroAt190);
        Assert.Equal((1u, 8u), samples.HeroTargetAfter190);
        Assert.Equal((58681344, 9961472), samples.HeroAt194);
        AssertGap(arc, B, 190, 194, 27);

        // The boxes (pages + 1 frames from the opening to the next instruction) and the waits.
        AssertGap(arc, B, 117, 121, 3);
        AssertGap(arc, B, 123, 127, 4);
        AssertGap(arc, B, 129, 133, 4);
        AssertGap(arc, B, 163, 167, 4);
        AssertGap(arc, B, 169, 173, 3);
        AssertGap(arc, B, 121, 123, 46);
        AssertGap(arc, B, 127, 129, 46);
        AssertGap(arc, B, 133, 135, 46);
        AssertGap(arc, B, 167, 169, 31);
        AssertGap(arc, B, 173, 175, 31);
        AssertGap(arc, B, 177, 179, 11);
        AssertGap(arc, B, 96, 98, 241);

        // The absolute frames.
        foreach (var (pc, frame) in new[]
        {
            (98, 253), (106, 413), (115, 516), (123, 565), (129, 615), (135, 665), (143, 690), (157, 815),
            (163, 833), (169, 868), (181, 913), (184, 997), (190, 998),
        })
        {
            AssertFrame(arc, B, pc, frame);
        }

        // The shop's objects cut their own script (0x40, 0x41), at the first tick of their C program, before frame 132.
        Assert.True(samples.ShopChecked, "the five shop programs never all ended");
        Assert.True(samples.FrameOfFirstShopCheck < 132, $"the shop programs ended at frame {samples.FrameOfFirstShopCheck}, before 132 expected");
        Assert.True(samples.ShopFailures.Count == 0, string.Join("; ", samples.ShopFailures));
        foreach (var (slot, pc, opcode) in new[] { (0, 56, 0x41), (C, 724, 0x62), (C, 728, 0x41), (C, 731, 0x40), (C, 734, 0xFF) })
        {
            Assert.Equal(5, FramesOf(arc, slot, pc, opcode).Count);
        }

        AssertNoUnexpectedError(arc);
    }
}
