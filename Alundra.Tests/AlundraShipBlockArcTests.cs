#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d D0/D7 (docs/plan-e19-opcodes.md §1.2g, §1.3): arc A6 on the real exported map 391 (Ship Klark, night, break, Event), with
/// the export's real prefabs and the real hero. The camera block (record 0, spawned by <c>0x2D @265</c>, program C[100] @764)
/// lands without gravity at 144 px, then walks south by 0.5 px per tick (<c>0x5B @470</c>) - which the landing defect of D2 stopped
/// - until B1 sees it at TileY 44 (<c>0x07 @480</c>) and leaves for map 416 (<c>0x53 @540</c>). Every expected value is written by
/// hand before the code (docs/plan-e19-opcodes.md): a contradicted value is a stop, never a re-pin.
/// <para>
/// Order of the checks: the end signal, the skipped or exceeded set, then the rest in the order of the plan. Samples taken at one
/// instruction are recorded DURING the run (<see cref="ArcRun.OnInstruction"/>, after the instruction's effects) and asserted after
/// the end signal. The frames follow the current order of the <c>TileZ</c> computation: they move back by one frame with E19.h
/// (D-E19-15).
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraShipBlockArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private static ArcSpec A6Spec => new(
        "A6", "The Klark", "Ship Klark (night, break, Event)-391", new[] { 1641 }, 15, 28, 7, 1500, RealController: true, Prefabs: true);

    /// <summary>The instructions the map still skips: none of them suspends (effects E19.g, camera sway E19.k, typewriter E12.c).</summary>
    private static readonly HashSet<(int Opcode, int Pc)> AllowedSkipped = new()
    {
        (0xA2, 228), (0xA2, 236), (0xA2, 244), (0xA2, 252), (0xA2, 408),
        (0x8E, 260), (0x8E, 335), (0x8E, 342), (0x8E, 721),
        (0x94, 417), (0x94, 425), (0x94, 433), (0x94, 441), (0x94, 503),
        (0x4C, 706),
    };

    private sealed class Samples
    {
        public (int X, int Y, int Z)? BlockAfter265;
        public (uint Anim, uint Direction)? HeroAfter270;
        public (int X, int Y)? HeroAt275;
        public int? BlockPosZHighAfter764;
        public bool CameraOnBlockAfter773;
        public int? BlockForceZAfter775;
        public (int TileX, int TileY, int TileZ)? BlockAt291;
        public readonly Dictionary<int, (int Record, int ForceZ)> ForceZAfter5E = new();
        public int? ResultAt388;
        public int? ResultAt390;
        public (int X, int Y, int Z)? Record4After449;
        public uint? Record4FlagsAfter457;
        public (uint Anim, uint Direction)? BlockAt470;
        public (int PosY, int TileY)? BlockAt491;
        public int? Record4PosYAt528;

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot == B)
            {
                TakeB(arc, t);
            }
            else if (t.Slot == C)
            {
                TakeC(arc, t);
            }
        }

        private void TakeB(ArcRun arc, ArcInstruction t)
        {
            switch (t.Pc)
            {
                case 265 when t.Opcode == 0x2D && BlockAfter265 == null:
                    var block = arc.EntityByRecord(0);
                    BlockAfter265 = block == null ? null : (block.PosX, block.PosY, block.PosZ);
                    break;
                case 270 when HeroAfter270 == null:
                    HeroAfter270 = (arc.Hero.TargetAnimationId, arc.Hero.TargetDirection);
                    break;
                case 275 when HeroAt275 == null:
                    HeroAt275 = (arc.Hero.PosX, arc.Hero.PosY);
                    break;
                case 291 when t.Opcode == 0x00 && BlockAt291 == null:
                    var b291 = arc.EntityByRecord(0);
                    BlockAt291 = b291 == null ? null : (b291.TileX, b291.TileY, b291.TileZ);
                    break;
                case 321 or 368 or 377 or 497 when t.Opcode == 0x5E && !ForceZAfter5E.ContainsKey(t.Pc):
                    var record = t.Pc switch { 321 => 6, 368 => 2, 377 => 3, _ => 0 };
                    ForceZAfter5E[t.Pc] = (record, arc.EntityByRecord(record)?.ForceZ ?? int.MinValue);
                    break;
                case 388 when ResultAt388 == null:
                    ResultAt388 = arc.Hero.EventProgramState.Result;
                    break;
                case 390 when ResultAt390 == null:
                    ResultAt390 = arc.Hero.EventProgramState.Result;
                    break;
                case 449 when t.Opcode == 0x64 && Record4After449 == null:
                    var r4 = arc.EntityByRecord(4);
                    Record4After449 = r4 == null ? null : (r4.PosX, r4.PosY, r4.PosZ);
                    break;
                case 457 when t.Opcode == 0x63 && Record4FlagsAfter457 == null:
                    Record4FlagsAfter457 = (uint?)arc.EntityByRecord(4)?.Flags;
                    break;
                case 470 when t.Opcode == 0x5B && BlockAt470 == null:
                    var b470 = arc.EntityByRecord(0);
                    BlockAt470 = b470 == null ? null : (b470.TargetAnimationId, b470.TargetDirection);
                    break;
                case 491 when t.Opcode == 0x5B && BlockAt491 == null:
                    var b491 = arc.EntityByRecord(0);
                    BlockAt491 = b491 == null ? null : (b491.PosY, b491.TileY);
                    break;
                case 528 when t.Opcode == 0x00 && Record4PosYAt528 == null:
                    Record4PosYAt528 = arc.EntityByRecord(4)?.PosY;
                    break;
            }
        }

        private void TakeC(ArcRun arc, ArcInstruction t)
        {
            if (t.Pc == 764 && BlockPosZHighAfter764 == null)
            {
                BlockPosZHighAfter764 = arc.EntityByRecord(0)?.PosZ >> 16;
            }

            if (t.Pc == 773 && !CameraOnBlockAfter773)
            {
                var block = arc.EntityByRecord(0);
                CameraOnBlockAfter773 = block != null && ReferenceEquals(arc.Proxy.EntityFollowedByCamera, block);
            }

            if (t.Pc == 775 && BlockForceZAfter775 == null)
            {
                BlockForceZAfter775 = arc.EntityByRecord(0)?.ForceZ;
            }
        }
    }

    [Fact]
    public void A6_TheCameraBlockLandsAndWalksSouth_ThenTheWarpToMap416Departs()
    {
        using var arc = new ArcRun(A6Spec);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new Samples();
        arc.OnInstruction = t => samples.Take(arc, t);

        // 1. The end signal.
        arc.RunUntil(() => arc.Has(B, 540, 0x53), "B1 executes 0x53 @540 towards map 416");

        // 2. Only the expected instructions skipped, none cut off by the loop guard; 0x4C @706 three times.
        AssertSkippedWithin(arc, AllowedSkipped);
        Assert.Equal(3, arc.SkippedOrExceeded.Count(t => t.Opcode == 0x4C && t.Pc == 706));

        // 3. The rest, in the order of the plan.
        AssertFrame(arc, B, 540, 1149);
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(416u, arrival.MapIndex);
        Assert.Equal(65273856, arrival.PosX);
        Assert.Equal(51904512, arrival.PosY);
        Assert.Equal(1048576, arrival.PosZ);
        Assert.Equal(2, arrival.EffectId);
        Assert.Equal(0u, arrival.AnimationId);
        Assert.Equal(0u, arrival.DirectionId);

        // Frame 0: the block spawned by 0x2D @265 rests one frame on the sailor 4 (spawn support without reach, O-E19-15).
        Assert.Equal((29097984, 44040192, 7340032), samples.BlockAfter265);
        Assert.Equal(0, FrameOf(arc, B, 265));
        Assert.Equal((1u, 0u), samples.HeroAfter270);

        // C[100] first runs at frame 2: the block is lifted to 336 px, follows itself with the camera, flies down at -1 px per tick.
        AssertFrame(arc, C, 764, 2);
        Assert.Equal(336, samples.BlockPosZHighAfter764);
        Assert.True(samples.CameraOnBlockAfter773, "0x67 @773 did not put the camera on the block");
        Assert.Equal(-65536, samples.BlockForceZAfter775);

        // The hero's walk (0x1F @272) ends at frame 21.
        Assert.Equal((24379392, 32067584), samples.HeroAt275);
        AssertFrame(arc, B, 275, 21);

        // The first 0x00 @291 at frame 179: the block has come down to row 42, level 9.
        AssertFrame(arc, B, 291, 179);
        Assert.Equal((18, 42, 9), samples.BlockAt291);

        // The three boxes (script-closed): opened at 180, 273 and 444, closed by 0x51 @739 exactly 61 frames later.
        Assert.Equal(new[] { 180, 273, 444 }, new[] { 295, 306, 327 }.Select(pc => FrameOf(arc, B, pc)).ToArray());
        Assert.Equal(new[] { 241, 334, 505 }, FramesOf(arc, B, 739, 0x51));
        Assert.Equal(new[] { 61, 61, 61 }, new[] { 180, 273, 444 }.Zip(FramesOf(arc, B, 739, 0x51), (open, close) => close - open).ToArray());
        Assert.Equal(3u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);

        // 0x5E: the sailors' lifts, read after the instruction.
        AssertFrame(arc, B, 321, 413);
        Assert.Equal((6, 393216), samples.ForceZAfter5E[321]);
        AssertFrame(arc, B, 368, 619);
        Assert.Equal((2, 524288), samples.ForceZAfter5E[368]);
        AssertFrame(arc, B, 377, 640);
        Assert.Equal((3, 393216), samples.ForceZAfter5E[377]);
        AssertFrame(arc, B, 497, 854);
        Assert.Equal((0, 24576), samples.ForceZAfter5E[497]);

        AssertFrame(arc, B, 347, 568); // T0 set
        AssertFrame(arc, B, 388, 702);
        AssertFrame(arc, B, 390, 702);
        Assert.Equal(1, samples.ResultAt388);
        Assert.Equal(1, samples.ResultAt390);
        AssertFrame(arc, B, 449, 703);
        Assert.Equal((29097984, 46661632, 3145729), samples.Record4After449);
        Assert.Equal(0u, samples.Record4FlagsAfter457 & 0x100u);

        // The block walks south at 0.5 px per tick from frame 728, and reaches row 44 at frame 793.
        AssertFrame(arc, B, 470, 728);
        Assert.Equal((1u, 0u), samples.BlockAt470);
        AssertFrame(arc, B, 491, 793);
        Assert.True(samples.BlockAt491 is { PosY: >= 46137344, TileY: 44 }, $"the block at 0x5B @491: {samples.BlockAt491}");

        // The loop and the end: 40 steps of record 4, then the wait of 120 frames.
        AssertFrame(arc, B, 514, 987);
        Assert.Equal(40, FramesOf(arc, B, 517, 0x65).Count);
        Assert.Equal(40, FramesOf(arc, B, 525, 0x74).Count);
        Assert.Equal(Enumerable.Range(988, 40).ToArray(), FramesOf(arc, B, 517, 0x65).ToArray());
        Assert.Equal(51904512, samples.Record4PosYAt528);
        AssertFrame(arc, B, 528, 1027);
        AssertFrame(arc, B, 529, 1028);
        Assert.Equal(121, FrameOf(arc, B, 540) - FrameOf(arc, B, 529));

        // B2 (program @552): the flashes and the end.
        Assert.Equal(
            new[] { 62, 184, 206, 448, 510 },
            new[] { 581, 599, 617, 635, 653 }.Select(pc => FrameOf(arc, B, pc)).ToArray());
        AssertFrame(arc, B, 761, 633);

        // The end state.
        Assert.Equal(0x04u, ArcRun.State.PlayerControlFlags);
        Assert.True(IsTemporarySet(0), "T0");
        Assert.False(IsTemporarySet(999), "T999");
        Assert.False(IsTemporarySet(1000), "T1000");
        Assert.Equal(512u, ArcRun.State.GameFlags[51]);
        AssertNoUnexpectedError(arc);
    }
}
