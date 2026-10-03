#nullable enable
using System;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.a T5 (docs/plan-e19-opcodes.md §1.2 T5, §1.3): the arcs of the ship chain that E19.a owns, on the real
/// exported map 390 (Ship Klark, inner). Each arc has an end signal proved by the trace and a frame limit past which
/// it FAILS naming where each program stopped (<see cref="ArcRun"/>). Expected values are written by hand from the
/// decoded bytecode of map 390 before the code that satisfies them.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraShipArcTests
{
    private const string Zone = "The Klark";
    private const string Map390 = "Ship Klark (inner)-390";

    private const int BProgram = ScriptHelper.ProgramBMap;

    /// <summary>The persistent flags G866 (the sailor with the grog was spoken to) and G869 (the captain was spoken
    /// to), the way the cutscene of B1 is armed: word 27, mask 36.</summary>
    private static readonly int[] GrogAndCaptainSpoken = { 866, 869 };

    private static ArcSpec A0Spec => new("A0", Zone, Map390, GrogAndCaptainSpoken, 30, 57, 4, 800);

    /// <summary>
    /// A0: the hero is in room 1, outside the push zone. The captain (record 2) walks east under B1, then
    /// <c>0x43 [2]</c> (@534) makes him the logic entity, <c>1E [80,0]</c> (@540) walks HIM 80 px north, <c>0x42</c>
    /// (@543) gives the logic entity back to the hero, <c>0x2E [2]</c> destroys the captain and <c>0x11</c> (@547)
    /// gives back the control, then the program ends (<c>0xFF @548</c>).
    /// </summary>
    [Fact]
    public void A0_TheCaptainWalksTheStairs_TheHeroStaysPut_ThenTheControlComesBack()
    {
        using var arc = new ArcRun(A0Spec);
        var captain = arc.EntityByRecord(2) ?? throw new Xunit.Sdk.XunitException("record 2 (the captain) did not spawn");
        Assert.Equal(0u, ArcRun.State.GetFlag(870) & (1u << 6));

        (int X, int Y)? heroAtWalkStart = null, heroAtWalkEnd = null, captainAtWalkStart = null, captainAtWalkEnd = null;
        arc.OnInstruction = t =>
        {
            if (t.Slot == BProgram && t.Pc == 540 && t.Opcode == 0x1E && heroAtWalkStart == null)
            {
                heroAtWalkStart = (arc.Hero.PosX, arc.Hero.PosY);
                captainAtWalkStart = (captain.PosX, captain.PosY);
            }

            if (t.Slot == BProgram && t.Pc == 543 && t.Opcode == 0x42)
            {
                heroAtWalkEnd = (arc.Hero.PosX, arc.Hero.PosY);
                captainAtWalkEnd = (captain.PosX, captain.PosY);
            }
        };

        arc.RunUntil(
            () => arc.FirstIndexOf(BProgram, 547, 0x11) is var gain and >= 0 && arc.HasAfter(gain, BProgram, 548, 0xFF),
            "B1 executes 0x11 @547 then 0xFF @548");

        // While 1E @540 waited, it was the captain who advanced at least 80 px to the north, not the hero.
        Assert.NotNull(heroAtWalkStart);
        Assert.NotNull(heroAtWalkEnd);
        Assert.Equal(heroAtWalkStart, heroAtWalkEnd);
        Assert.True(
            captainAtWalkStart!.Value.Y - captainAtWalkEnd!.Value.Y >= 80 << 16,
            $"the captain advanced {(captainAtWalkStart.Value.Y - captainAtWalkEnd.Value.Y) / 65536.0} px to the north, 80 expected");

        Assert.NotEqual(0u, ArcRun.State.GetFlag(870) & (1u << 6));
        // E19.r R3: B[1] destroys the captain by 0x2E [2] @545 (a map program) and the recycling of the same image puts it back to the template.
        Assert.Equal(EntityStatus.Destroyed, captain.Status);
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.Empty(arc.SkippedOrExceeded.ToList());
        Assert.True(arc.Frame < 800);
    }

    /// <summary>
    /// A0b: the hero stands in the push zone (<c>3B [36,40,48,50,4,8]</c> @482): B1 first walks him aside with two
    /// <c>0x1F</c> (@496, @507) before the captain's own walk, then the same end as A0.
    /// </summary>
    [Fact]
    public void A0b_TheHeroInThePushZone_IsWalkedAside_ThenTheCaptainLeavesAndTheControlComesBack()
    {
        using var arc = new ArcRun(new ArcSpec("A0b", Zone, Map390, GrogAndCaptainSpoken, 38, 49, 4, 800));
        var captain = arc.EntityByRecord(2) ?? throw new Xunit.Sdk.XunitException("record 2 (the captain) did not spawn");
        var heroStart = (arc.Hero.PosX, arc.Hero.PosY);
        (int X, int Y)? heroAfterPush = null;
        arc.OnInstruction = t =>
        {
            if (t.Slot == BProgram && t.Pc == 507 && t.Opcode == 0x1F && heroAfterPush == null)
            {
                heroAfterPush = (arc.Hero.PosX, arc.Hero.PosY);
            }
        };

        arc.RunUntil(
            () => arc.FirstIndexOf(BProgram, 547, 0x11) is var gain and >= 0 && arc.HasAfter(gain, BProgram, 548, 0xFF),
            "B1 executes 0x11 @547 then 0xFF @548, after the two 0x1F of the push branch");

        // The hero was walked aside by the push branch (two 0x1F) before the captain's walk.
        Assert.NotNull(heroAfterPush);
        Assert.NotEqual(heroStart, heroAfterPush);

        Assert.True(arc.Has(BProgram, 496, 0x1F), "the first push walk (0x1F @496) never ran");
        Assert.True(arc.Has(BProgram, 507, 0x1F), "the second push walk (0x1F @507) never ran");
        Assert.True(arc.FirstIndexOf(BProgram, 507, 0x1F) < arc.FirstIndexOf(BProgram, 534, 0x43));

        Assert.NotEqual(0u, ArcRun.State.GetFlag(870) & (1u << 6));
        // E19.r R3: B[1] destroys the captain by 0x2E [2] @545 (a map program) and the recycling of the same image puts it back to the template.
        Assert.Equal(EntityStatus.Destroyed, captain.Status);
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
        Assert.Empty(arc.SkippedOrExceeded.ToList());
        Assert.True(arc.Frame < 800);
    }

    /// <summary>
    /// A1: the hero arrives from door 3 of the deck at (44,23,4) with G870 and G871 already set. At load the second
    /// captain (record 3) exists and the first (record 2) is destroyed; B3 opens its dialogue (<c>0x0D [248,0]</c>
    /// @724), which the arc closes with the button like the player; B2 then puts the hero to sleep
    /// (<c>G1640</c>, @685) and changes map with <c>0x53 [220,1,0,0,3,4,73]</c> @688. The second <c>0x53</c> of the
    /// program (@698, towards map 412) is never reached: the warp freezes the map events.
    /// </summary>
    [Fact]
    public void A1_TheCabin_TheDialogueCloses_TheHeroSleeps_ThenTheWarpToMap476Departs()
    {
        var flags = new[] { 866, 869, 870, 871 };
        using var arc = new ArcRun(new ArcSpec("A1", Zone, Map390, flags, 44, 23, 4, 900));
        Assert.Equal(228u, ArcRun.State.GameFlags[27]);

        // E19.r R3: the first captain destroys itself in its load program (A[2] @433, G870 set), during the entity pass of the first image, and is recycled
        // in that same image (the box of B3 opens at the image 4 at the earliest): its proxy is captured before the image, then is Destroyed and unlisted.
        var captain1 = arc.EntityByRecord(2) ?? throw new Xunit.Sdk.XunitException("record 2 did not spawn");
        arc.OneFrame(); // the load programs of the first frame.
        var captain2 = arc.EntityByRecord(3) ?? throw new Xunit.Sdk.XunitException("record 3 (the second captain) did not spawn");
        Assert.Equal(EntityStatus.Destroyed, captain1.Status);
        Assert.Null(arc.EntityByRecord(2));
        Assert.NotEqual(EntityStatus.FlagToDestroy, captain2.Status);

        arc.RunUntil(() => AlundraDialogueDirector.Instance.IsOpen, "the dialogue of B3 opens (0x0D @724)");
        Assert.True(arc.Has(BProgram, 724, 0x0D));
        arc.CloseDialogueWithTheButton("the dialogue of B3 closes with the button");
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);

        arc.RunUntil(() => arc.Has(BProgram, 688, 0x53), "B2 executes 0x53 @688 towards map 476");

        Assert.NotEqual(0u, ArcRun.State.GetFlag(1640) & (1u << (1640 & 0x1f)));
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(476u, arrival.MapIndex);
        Assert.Equal(12 << 16, arrival.PosX);
        Assert.Equal(8 << 16, arrival.PosY);
        Assert.Equal(3 << 20, arrival.PosZ);
        Assert.Equal(4, arrival.EffectId);

        // The warp freezes the map events from the instruction after @688: the second 0x53 (@698, map 412) never runs.
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Assert.False(arc.Has(BProgram, 698, 0x53));
        Assert.Empty(arc.SkippedOrExceeded.ToList());
        Assert.True(arc.Frame < 900);
    }

    /// <summary>A failing <see cref="ArcRun"/> constructor never reaches <c>Dispose</c>: it restores the global state
    /// itself, or a failing arc would dirty the tests that follow (the P3 deferred by the E19.a verification).</summary>
    [Fact]
    public void ArcRun_AConstructorThatFails_RestoresTheGlobalState()
    {
        var projectPathBefore = EngineEnvironment.ProjectPath;
        var cameraOptionBefore = AlundraWorldProxy.DebugCameraPanEnabledForTests;
        var missingMap = new ArcSpec("missing", Zone, "No such map-0", Array.Empty<int>(), 0, 0, 0, 10);

        Assert.ThrowsAny<Exception>(() => new ArcRun(missingMap));

        Assert.Equal(projectPathBefore, EngineEnvironment.ProjectPath);
        Assert.Equal(cameraOptionBefore, AlundraWorldProxy.DebugCameraPanEnabledForTests); // the arc forces it on.
    }

    /// <summary>
    /// A1c (E19.a2 T0/T3, docs/plan-e19-opcodes.md §1.2b): the arc A1 again, but with a REAL hero controller and the
    /// world's own <c>Update</c>. B2 walks the hero 80, 48, 80 then 32 px between the rails it lays with the fifteen
    /// <c>0x54</c>; the second north walk ends against the rail (42,12) and its <c>0x1E @658</c> waits for the
    /// 80 px. On a controller that rejects the blocked step whole, the hero stops 78.8 px away and the wait never ends;
    /// with the step advanced to contact (y = 215.0) the sleep starts and <c>0x53 @688</c> departs towards 476.
    /// </summary>
    [Fact]
    public void A1c_TheCabinWithARealController_TheSecondNorthWalkReachesItsContact_ThenTheWarpToMap476Departs()
    {
        var flags = new[] { 866, 869, 870, 871 };
        using var arc = new ArcRun(new ArcSpec("A1c", Zone, Map390, flags, 44, 23, 4, 900, RealController: true));
        Assert.Equal(228u, ArcRun.State.GameFlags[27]);

        arc.RunUntil(() => AlundraDialogueDirector.Instance.IsOpen, "the dialogue of B3 opens (0x0D @724)");
        Assert.True(arc.Has(BProgram, 724, 0x0D));
        arc.CloseDialogueWithTheButton("the dialogue of B3 closes with the button");

        arc.RunUntil(() => arc.Has(BProgram, 688, 0x53), "B2 executes 0x53 @688 towards map 476 (the walk of 0x1E @658 ends at its contact)");

        Assert.NotEqual(0u, ArcRun.State.GetFlag(1640) & (1u << (1640 & 0x1f)));
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        Assert.Equal(476u, AlundraWarpDirector.Instance.ArrivalRecordForTests.MapIndex);
        Assert.Empty(arc.SkippedOrExceeded.ToList());
        Assert.True(arc.Frame < 900);
    }
}
