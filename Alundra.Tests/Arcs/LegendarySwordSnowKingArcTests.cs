#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Xunit;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.SaveGames;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.b2 T3 (docs/plan-e20-heros.md section 1.4.1, D-B2-1 to D-B2-3): the scene of the Snow King on the map 7 (the program B[10] of the map, pc 1268 to 1663), from the start of the preset <c>legende-m7</c>
/// (the tile (25, 17), level 7, the death-retry counter at 20, the flag 1787 off), on the real exported map, the real controller and the real programs. The hero faces north under the transparent block of the record 28 and walks north into it: the program F[7] of the
/// block sets the flag <c>[0, 128]</c> and B[10] leaves its wait (T0 (c) 2).
/// <para>The measure of T0 (c) 3 (docs/plan-e20-annexe/e20-b2/results.md): at the HEAD before T2, <c>Result</c> read by the <c>0x03</c> of pc 1510 is 0 for every counter (0, 19, 20, 21): the DLL refused with the counter at 20 where the original gives the item 8. So the counter 20 is the case that was RED
/// before the case <c>0xC2</c> and the counter 19 is the guard, green before and after, killed by the mutant "the flag 1787 set at the refusal".</para>
/// <para>The scene does not replay inside one visit (T0 (c) 4): after its <c>End</c> at pc 1663 B[10] stays on it and nothing clears the flag <c>[0, 128]</c>; "the refusal replays the scene" is read here as what makes it replay at the next entry into the map, the flag 1787 left off.</para>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LegendarySwordSnowKingArcTests : IDisposable
{
    private const string Zone = "Overworld";
    private const string World7 = "Overworld 1,2-7";
    private const uint FlagTheSwordWasGiven = 1787;
    private const int OpcodeGiveItem = 0x82;
    private const int SceneEnd = 1663;
    private const int RefusalDialoguePc = 1513;
    private const int GiveDialoguePc = 1571;
    private const int RefusalGotoPc = 1567;

    public LegendarySwordSnowKingArcTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    private static int Item8() => PlayerManager.GetNumberOfItem(GameState.Instance, 8);

    /// <summary>The arc at the start of <c>legende-m7</c> (the tile and the level of the preset, facing north); the counter is set to <paramref name="counter"/> once the arc is built (the arc loads the map, not a save).</summary>
    private static ArcRun Start(string name, int counter)
    {
        Assert.True(TestSaves.TryFind("legende-m7", out var preset), "the preset legende-m7 exists");
        var p = preset!;
        Assert.Equal(7, p.MapId);
        Assert.Equal(20, p.DeathRetryCount);
        var arc = new ArcRun(LeverArcs.Spec(name, Zone, World7, Array.Empty<int>(), p.TileX * 24 + 12, p.TileY * 16 + 8, p.TileZ, 16, 8000));
        GameState.Instance.DeathRetryCount = (byte)counter;
        return arc;
    }

    /// <summary>Walks north into the block (the Up held 12 frames from the first frame) then lets the scene run, pressing the dialogue button on every dialogue frame, until B[10] reaches its <c>End</c> at pc 1663.</summary>
    private static void PlayTheScene(ArcRun arc)
    {
        arc.OneFrame();
        arc.HoldDirections(PadState.Up);
        for (var i = 0; i < 12; i++)
        {
            arc.OneFrame();
        }

        arc.ReleaseDirections();
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(
            () => arc.Trace.Any(t => t.Slot == ScriptHelper.ProgramBMap && t.Pc == SceneEnd && t.Opcode == 0xFF),
            "the end of the scene of the Snow King (B[10], pc 1663)");
    }

    private static int GiveCount(ArcRun arc) => arc.Trace.Count(t => t.Slot == ScriptHelper.ProgramBMap && t.Pc == 1623 && t.Opcode == OpcodeGiveItem);

    private static bool Ran(ArcRun arc, int pc) => arc.Trace.Any(t => t.Slot == ScriptHelper.ProgramBMap && t.Pc == pc);

    [Fact]
    public void WithTheCounterAt20_TheSceneGivesTheLegendarySwordOnce_SetsTheFlag1787_AndDoesNotReplay()
    {
        using var arc = Start("b2-c20", 20);
        Assert.Equal(0, Item8());
        Assert.False(ArcChecks.IsSet(FlagTheSwordWasGiven));

        PlayTheScene(arc);

        Assert.Equal(1, Item8());
        Assert.True(ArcChecks.IsSet(FlagTheSwordWasGiven));
        Assert.Equal(1, GiveCount(arc));
        Assert.True(Ran(arc, GiveDialoguePc), "the branch of the gift (pc 1571) ran");
        Assert.False(Ran(arc, RefusalGotoPc), "the refusal's own tail did not run");
        Assert.False(LeverArcs.Locked(), "the hero has the control back");
        Assert.False(DialogueDirector.Instance.IsOpen);

        // The scene does not replay: 640 more frames, still one gift, the item counted once, B[10] stays on its End.
        for (var i = 0; i < 640; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(1, Item8());
        Assert.Equal(1, GiveCount(arc));
        Assert.True(ArcChecks.IsSet(FlagTheSwordWasGiven));
    }

    [Fact]
    public void WithTheCounterAt19_TheSceneRefuses_GivesNothing_AndLeavesTheFlag1787Off()
    {
        using var arc = Start("b2-c19", 19);

        PlayTheScene(arc);

        Assert.True(Ran(arc, RefusalDialoguePc), "the refusal's dialogue (pc 1513) ran");
        Assert.False(Ran(arc, GiveDialoguePc), "the gift branch did not run");
        Assert.Equal(0, GiveCount(arc));
        Assert.Equal(0, Item8());
        Assert.False(ArcChecks.IsSet(FlagTheSwordWasGiven), "a refusal leaves the flag 1787 off, so the scene plays again at the next entry into the map");
        Assert.False(LeverArcs.Locked(), "the hero has the control back");
    }

    [Fact]
    public void WithTheCounterAt19_ACopyOfTheRecord31OnTheHero_IsNotPickedUp_NotInTheDllAndNotInTheBinary()
    {
        // T0 (c1): the record 31 is made by 0x8A at pc 1411 and 1483 and destroyed by 0x2E before the hero is free; forced into the world and placed on the hero, 90 frames later nothing is given and no dialogue opened.
        // In the binary the phase 2 of 0x800386D0 runs the map script C[5] (index 133 & 0x7F) instead of the sprite handler that gives a pickup (docs/plan-e20-annexe/e20-b2/results.md (c) 5).
        using var arc = new ArcRun(LeverArcs.Spec("b2-r31", Zone, World7, Array.Empty<int>(), 5 * 24 + 12, 5 * 16 + 8, 0, 0, 8000));
        GameState.Instance.DeathRetryCount = 19;
        arc.OneFrame();
        Assert.Null(arc.EntityByRecord(31));
        var ctx = (Alundra.Maps.IEntityWorldContext)arc.Proxy;
        var copy = ctx.SpawnEntityByRecordId(arc.Hero, 31);
        Assert.NotNull(copy);
        copy!.Flags &= ~EntityFlags.Gravity;
        for (var i = 0; i < 3; i++)
        {
            arc.OneFrame();
        }

        copy.PosX = arc.Hero.PosX + arc.Hero.ModX - copy.ModX;
        copy.PosY = arc.Hero.PosY + arc.Hero.ModY - copy.ModY;
        copy.PosZ = arc.Hero.PosZ + arc.Hero.ModZ - copy.ModZ;
        copy.TileX = (copy.PosX >> 16) / 24;
        copy.TileY = (copy.PosY >> 16) / 16;
        copy.TileZ = copy.PosZ >> 20;
        copy.PushLogicalPositionToRoot();
        for (var i = 0; i < 90; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(0, Item8());
        Assert.False(DialogueDirector.Instance.IsOpen);
        Assert.Equal(0, arc.Trace.Count(t => t.Opcode == OpcodeGiveItem));
        Assert.Equal(19, GameState.Instance.DeathRetryCount);
    }
}
