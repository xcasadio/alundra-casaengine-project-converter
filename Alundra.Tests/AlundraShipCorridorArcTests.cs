#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d D3 (docs/plan-e19-opcodes.md §1.2g, §1.3, D-E19-23): arcs A5 and A5r on the real exported map 392 (Ship Klark, night, inner),
/// with the export's real prefabs and the real hero. B1 puts the hero at (720, 216, 64) px, walks him 48 px east, gives him the hand
/// (<c>0x11 @104</c>), then rolls him in a random direction whenever a direction is held (the ship's roll, <c>0x0C</c>, the stream of
/// <see cref="AlundraRandom"/> reset by the arc). The only exit is the portal 0 at tile (22, 23): the player holds Down, facing down,
/// with the control free. A5 puts the hero back in the corridor of the portal at frame 205 (D-E19-23: the crates between the hero and
/// the portal do not block in the DLL, a known gap kept for E14), then holds Down; A5r holds Down in place first, so that the roll
/// plays once, then does the same. The end signal is the departure through the portal, which is not an instruction: the arrival record
/// of the warp director is the exception to the rule of the trace (§1.3). Every expected value is written by hand before the code.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraShipCorridorArcTests
{
    private const int B = ScriptHelper.ProgramBMap;

    private static ArcSpec Spec(string name) => new(
        name, "The Klark", "Ship Klark (night, inner, break)-392", new[] { 1641 }, 29, 13, 4, 400, RealController: true, Prefabs: true);

    private sealed class Samples
    {
        public (int X, int Y, int Z)? HeroAfter50;
        public (uint Anim, uint Direction)? HeroAfter58;
        public (uint Anim, uint Direction)? HeroAfter64;
        public readonly Dictionary<int, (int X, int Y)> HeroAt = new();
        public (int X, int Y, int TileX, int TileY, int TileZ, uint Flags)? HeroAt104;
        public readonly List<int> ResultsAt133 = new();
        public int? ResultAt107;
        public int? ResultAt114;
        public uint? DirectionAfter120;

        /// <summary>E19.k1 K3: the camera sway after every frame (the arc has no camera, so only the state of the sway).</summary>
        public readonly Dictionary<int, (int Flag, int SpeedX, int SpeedY, int LimitX, int LimitY, int OffsetX, int OffsetY, int ReachX, int ReachY)> SwayAfterFrame = new();

        public void TakeFrame(ArcRun arc)
        {
            var sway = AlundraCameraSway.Instance;
            SwayAfterFrame[arc.Frame - 1] = (sway.Flag, sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY);
        }

        public void Take(ArcRun arc, ArcInstruction t)
        {
            if (t.Slot != B)
            {
                return;
            }

            switch (t.Pc)
            {
                case 50 when t.Opcode == 0x64 && HeroAfter50 == null:
                    HeroAfter50 = (arc.Hero.PosX, arc.Hero.PosY, arc.Hero.PosZ);
                    break;
                case 58 when HeroAfter58 == null:
                    HeroAfter58 = (arc.Hero.TargetAnimationId, arc.Hero.TargetDirection);
                    break;
                case 64 when HeroAfter64 == null:
                    HeroAfter64 = (arc.Hero.TargetAnimationId, arc.Hero.TargetDirection);
                    break;
                case 71 or 72 when !HeroAt.ContainsKey(t.Pc):
                    HeroAt[t.Pc] = (arc.Hero.PosX, arc.Hero.PosY);
                    break;
                case 104 when t.Opcode == 0x11 && HeroAt104 == null:
                    HeroAt104 = (arc.Hero.PosX, arc.Hero.PosY, arc.Hero.TileX, arc.Hero.TileY, arc.Hero.TileZ, ArcRun.State.PlayerControlFlags);
                    break;
                case 133 when t.Opcode == 0x3B:
                    ResultsAt133.Add(arc.Hero.EventProgramState.Result);
                    break;
                case 107 when t.Opcode == 0x2F && ResultAt107 == null:
                    ResultAt107 = arc.Hero.EventProgramState.Result;
                    break;
                case 114 when t.Opcode == 0x70 && ResultAt114 == null:
                    ResultAt114 = arc.Hero.EventProgramState.Result;
                    break;
                case 120 when t.Opcode == 0x08 && DirectionAfter120 == null:
                    DirectionAfter120 = arc.Hero.TargetDirection;
                    break;
            }
        }
    }

    /// <summary>Part 1, shared by A5 and A5r: from the arrival to <c>0x11 @104</c> (frame 204), the hero is walked by B1 alone.</summary>
    private static void RunPart1(ArcRun arc, Samples samples)
    {
        arc.OnInstruction = t => samples.Take(arc, t);
        arc.OnFrame = () => samples.TakeFrame(arc);
        arc.RunUntil(() => arc.Has(B, 104, 0x11), "B1 executes 0x11 @104 (the hero takes the hand)");
    }

    private static void AssertPart1(ArcRun arc, Samples samples)
    {
        // Frame 0: the camera sway (0x8E @20, executed, E19.k1), the lock (0x10 @49), the placement of the hero (0x64 @50).
        Assert.Contains(arc.Trace, t => t.Opcode == 0x8E && t.Pc == 20 && t.Frame == 0 && t.Kind == EventTraceKind.Implemented);
        AssertTheSway(samples);
        Assert.Equal(0, FrameOf(arc, B, 49));
        Assert.Equal((47185920, 14155776, 4194305), samples.HeroAfter50);
        Assert.Equal((13u, 0u), samples.HeroAfter58);

        // The walk east: 0x5B @64 at frame 121 (animation 3, direction 24), 0x1F @68, then the turns and the hand.
        AssertFrame(arc, B, 64, 121);
        Assert.Equal((3u, 24u), samples.HeroAfter64);
        AssertFrame(arc, B, 71, 135);
        Assert.Equal((50411520, 14155776), samples.HeroAt[71]);
        AssertFrame(arc, B, 72, 136);
        Assert.Equal(50669568, samples.HeroAt[72].X);
        AssertFrame(arc, B, 78, 152);
        AssertFrame(arc, B, 84, 168);
        AssertFrame(arc, B, 90, 184);
        foreach (var pc in new[] { 96, 97, 98, 99 })
        {
            AssertFrame(arc, B, pc, 200 + pc - 96);
        }
        AssertFrame(arc, B, 104, 204);
        Assert.Equal((50798592, 14155776, 32, 13, 4, 0u), samples.HeroAt104);
    }

    /// <summary>E19.k1 K3: <c>0x8E [1,1,3,2]</c> at frame 0 arms the sway, one step per tick (the arc has no camera), the flag stays
    /// 1 until the departure.</summary>
    private static void AssertTheSway(Samples samples)
    {
        var s = samples.SwayAfterFrame;
        Assert.Equal((1, 1, 1, 3, 2), (s[0].Flag, s[0].SpeedX, s[0].SpeedY, s[0].LimitX, s[0].LimitY));
        Assert.Equal((-1, -1), (s[0].OffsetX, s[0].OffsetY)); // after frame 0.
        Assert.Equal((-2, 1), (s[1].OffsetY, s[1].ReachY)); // after frame 1.
        Assert.Equal((-3, 1), (s[2].OffsetX, s[2].ReachX)); // after frame 2.
        Assert.All(s, frame => Assert.Equal(1, frame.Value.Flag)); // armed until the departure.
    }

    private static void AssertTheDepartureToMap391()
    {
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(391u, arrival.MapIndex);
        Assert.Equal(24379392, arrival.PosX);
        Assert.Equal(29884416, arrival.PosY);
        Assert.Equal(0, arrival.PosZ);
        Assert.Equal(0x36u, arrival.AnimationId);
        Assert.Equal(0u, arrival.DirectionId);
        Assert.Equal(0, arrival.EffectId);
    }

    [Fact]
    public void A5_TheHeroTakesTheHand_IsPlacedInTheCorridor_ThenTheDownPushedPortalLeadsToMap391()
    {
        using var arc = new ArcRun(Spec("A5"));
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;
        var samples = new Samples();

        // 1. Part 1, then part 2: the end signal is the departure (the arrival record of the warp director), never an instruction.
        RunPart1(arc, samples);
        Assert.Equal(205, arc.Frame);
        arc.PlaceHero(540, 360, 64);
        arc.HoldDirections(AlundraPadState.Down);
        arc.RunUntil(() => AlundraWarpDirector.Instance.HasPendingArrival, "the portal (22, 23) of map 392 departs towards map 391");

        // 2. Nothing skipped (0x8E @20 is executed, once).
        AssertNothingSkippedOrExceeded(arc);
        Assert.Equal(1, arc.Trace.Count(t => t.Opcode == 0x8E && t.Pc == 20));

        // 3. The rest, in the order of the plan.
        AssertPart1(arc, samples);
        Assert.Equal(216, arc.Frame); // the departure happened during frame 215.
        AssertTheDepartureToMap391();
        Assert.False(arc.Has(B, 107, 0x2F), "0x2F @107 ran before the departure");
        Assert.All(samples.ResultsAt133, r => Assert.Equal(0, r));
        Assert.Equal(0u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        Assert.False(arc.Trace.Any(t => t.Opcode == 0x53));
        AssertNoUnexpectedError(arc);
    }

    [Fact]
    public void A5r_HoldingDownInPlace_TheShipRollsTheHeroOnce_ThenTheDownPushedPortalLeadsToMap391()
    {
        using var arc = new ArcRun(Spec("A5r"));
        var samples = new Samples();

        RunPart1(arc, samples);
        Assert.Equal(205, arc.Frame);

        // From frame 205 Down is held in place; the roll plays at its first poll.
        arc.HoldDirections(AlundraPadState.Down);
        var flagsAtTheEndOfFrame = new Dictionary<int, uint>();
        var heroAtTheEndOfFrame = new Dictionary<int, (int X, int Y)>();
        arc.OnFrame = () =>
        {
            samples.TakeFrame(arc);
            flagsAtTheEndOfFrame[arc.Frame - 1] = ArcRun.State.PlayerControlFlags;
            heroAtTheEndOfFrame[arc.Frame - 1] = (arc.Hero.PosX, arc.Hero.PosY);
        };
        arc.RunUntil(() => arc.Has(B, 124, 0x11), "B1 executes 0x11 @124 (the end of the roll)");
        Assert.Equal(226, arc.Frame);

        // Released for frames 226 and 227, then back in the corridor holding Down.
        arc.ReleaseDirections();
        arc.OneFrame();
        arc.OneFrame();
        Assert.Equal(228, arc.Frame);
        arc.PlaceHero(540, 360, 64);
        arc.HoldDirections(AlundraPadState.Down);
        arc.RunUntil(() => AlundraWarpDirector.Instance.HasPendingArrival, "the portal (22, 23) of map 392 departs towards map 391");

        AssertNothingSkippedOrExceeded(arc);
        Assert.Equal(1, arc.Trace.Count(t => t.Opcode == 0x8E && t.Pc == 20));

        AssertPart1(arc, samples);
        AssertFrame(arc, B, 107, 220);
        Assert.Equal(1, samples.ResultAt107);
        Assert.Equal(1, samples.ResultAt114);
        Assert.True(arc.Has(B, 118, 0x10));
        Assert.Equal(0x02u, samples.DirectionAfter120);
        AssertFrame(arc, B, 124, 225);

        // Frames 221 to 225: the control is locked (0x04, read at the end of the frame before: the 0x11 of frame 225 comes after the
        // push of that frame) and the roll moves the hero (X decreases, Y increases).
        for (var frame = 221; frame <= 225; frame++)
        {
            Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, flagsAtTheEndOfFrame[frame - 1]);
            Assert.True(heroAtTheEndOfFrame[frame].X < heroAtTheEndOfFrame[frame - 1].X, $"frame {frame}: PosX does not decrease");
            Assert.True(heroAtTheEndOfFrame[frame].Y > heroAtTheEndOfFrame[frame - 1].Y, $"frame {frame}: PosY does not grow");
        }

        Assert.Equal((50523408, 16302440), heroAtTheEndOfFrame[225]);

        Assert.Equal(239, arc.Frame); // the departure happened during frame 238, before the next poll (241).
        Assert.Equal(1, arc.Trace.Count(t => t.Slot == B && t.Pc == 107 && t.Opcode == 0x2F));
        AssertTheDepartureToMap391();
        AssertNoUnexpectedError(arc);
    }
}
