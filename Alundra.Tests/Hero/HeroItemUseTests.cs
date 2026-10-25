#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Tests.Audio;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.h0a T1 (docs/plan-e20-heros.md section 1.30.1): the frame of the use of an item, on a bare hero with a host (a <see cref="ContactHost"/> that hands the real item tables and a recording sound player), one
/// <see cref="PlayerManager.MovePlayer"/> call per tick. The start, the driver and the cancellation of an item are the test seams of D-H-4 (<see cref="PlayerManager.ItemStartForTests"/>,
/// <see cref="PlayerManager.ItemDriverForTests"/>, <see cref="PlayerManager.ItemCancelForTests"/>): the items themselves come with their own slices. Every "without effect" case is paired with a case of the same
/// front that does something (the green-and-inert rule). The expectations are the binary's: <c>TryUseItem</c> <c>0x8002ED64</c>, the start <c>0x8003499C</c>, the driver <c>0x8003634C</c>, the cancellation <c>0x800350C0</c>
/// (<c>docs/plan-e20-annexe/e20-h4/report.md</c> section 1; the table of the states in <c>docs/plan-e20-annexe/e20-h0a/results.md</c> section 1).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroItemUseTests : IDisposable
{
    private const uint Circle = PadState.Circle;
    private const uint Right = PadState.Right;

    private const uint Idle = 0;
    private const uint Moving = 1;
    private const uint Bomb = 0x23;
    private const uint Seeds = 0x1F;
    private const int RefusalSound = 3;

    public void Dispose()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TryUseItemResultForTests = null;
    }

    // -----------------------------------------------------------------------------------------
    // The montage.
    // -----------------------------------------------------------------------------------------

    private sealed class Rig
    {
        public required ContactHost Host { get; init; }

        public required EntityScriptProxy Hero { get; init; }

        public required RecordingSoundPlayer Sounds { get; init; }

        public GameState State => Host.GameState;

        /// <summary>The items the start seam was told, in order.</summary>
        public List<uint> Starts { get; } = new();

        /// <summary>The (item, item in use at that moment) pairs the cancel seam was told, in order.</summary>
        public List<(uint Item, uint InUse)> Cancels { get; } = new();

        /// <summary>The (item, counter at that moment) pairs the driver seam was told, in order.</summary>
        public List<(uint Item, int Counter)> Drivers { get; } = new();
    }

    private static ItemTables Tables() => ItemTables.GetOrCreate(ProjectRootFinder.Find());

    private static Rig NewRig(uint state = Idle, int? item = (int)Bomb, int slope = 0)
    {
        var host = new ContactHost { ItemTables = Tables() };
        var sounds = new RecordingSoundPlayer();
        host.SoundPlayer = sounds;
        var hero = new EntityScriptProxy { TargetAnimationId = state, TargetDirection = 0x18, AnimationDirection = 3, IsOnGround = 1, Slope_18c = slope, ScriptHost = host };
        hero.HeroAirborne = false;
        var rig = new Rig { Host = host, Hero = hero, Sounds = sounds };
        if (item is { } id)
        {
            Equip(rig, id);
        }

        return rig;
    }

    /// <summary>Owns <paramref name="item"/> and equips it on the Circle (the item the binary's <c>0x8004E0F8</c> promotes).</summary>
    private static void Equip(Rig rig, int item)
    {
        rig.State.NumberOfItems[item * 2 + 1] = 1;
        rig.State.PlayerStats.ItemId = (short)item;
    }

    /// <summary>The start seam answers <paramref name="answer"/> and journals the item; the cancel and driver seams journal too (the driver answers <paramref name="driver"/>).</summary>
    private static void Seams(Rig rig, int start = 0, Func<int, int>? driverAnswer = null)
    {
        PlayerManager.ItemStartForTests = (_, item) =>
        {
            rig.Starts.Add(item);
            return start;
        };
        PlayerManager.ItemDriverForTests = (hero, item) =>
        {
            rig.Drivers.Add((item, hero.ItemUseCounter));
            return driverAnswer?.Invoke(rig.Drivers.Count) ?? 0;
        };
        PlayerManager.ItemCancelForTests = (hero, item) => rig.Cancels.Add((item, hero.ItemInUse));
    }

    private static void Move(Rig rig, uint hold = 0, uint pressed = 0)
    {
        var pad = new PadState { ButtonsHold = hold, ButtonsJustPressed = pressed };
        PlayerManager.MovePlayer(rig.Hero, in pad, rig.State, rig.Host);
    }

    /// <summary>The edge of the Circle (held and just pressed).</summary>
    private static void Front(Rig rig, uint extraHold = 0) => Move(rig, Circle | extraHold, Circle);

    // -----------------------------------------------------------------------------------------
    // (a1) the front.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A1_TheCircleHeldWithoutAnEdge_TriesNothing_AnEdgeTriesTheItem()
    {
        var rig = NewRig();
        Seams(rig);

        Move(rig, Circle, pressed: 0);
        Assert.Empty(rig.Starts);
        Assert.Equal(0u, rig.Hero.ItemInUse);
        Assert.Empty(rig.Sounds.Requests);

        Front(rig);
        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
        Assert.Empty(rig.Sounds.Requests);
    }

    // -----------------------------------------------------------------------------------------
    // (a2) the lock.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x01u)]
    [InlineData(0x02u)]
    [InlineData(0x08u)]
    [InlineData(0x40u)]
    [InlineData(0x80u)]
    [InlineData(0x100u)]
    [InlineData(0x80000000u)]
    public void A2_AnEdge_UnderAControlBitThatDoesNotLockTheBranch_IsRefusedWithTheSound3_TheSameEdgeWithoutTheBitStartsTheItem(uint bit)
    {
        var rig = NewRig();
        Seams(rig);

        rig.State.PlayerControlFlags = bit;
        Front(rig);
        Assert.Equal(new[] { RefusalSound }, rig.Sounds.Requests);
        Assert.Empty(rig.Starts);
        Assert.Equal(0u, rig.Hero.ItemInUse);

        rig.State.PlayerControlFlags = 0;
        rig.Sounds.Requests.Clear();
        Front(rig);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
    }

    [Theory]
    [InlineData(GameState.PlayerControlBits.ControlLocked)]
    [InlineData(GameState.PlayerControlBits.MessageBox)]
    [InlineData(GameState.PlayerControlBits.ForcedSequence)]
    public void A2_UnderTheLockedBranch_TheEdgeIsNotTried_NoSound_TheSameEdgeFreeIs(uint bit)
    {
        var rig = NewRig();
        Seams(rig);
        PlayerManager.TryUseItemCallCountForTests = 0;

        rig.State.PlayerControlFlags = bit;
        Front(rig);
        Assert.Equal(0, PlayerManager.TryUseItemCallCountForTests);
        Assert.Empty(rig.Starts);
        Assert.Empty(rig.Sounds.Requests);

        rig.State.PlayerControlFlags = 0;
        Front(rig);
        Assert.Equal(1, PlayerManager.TryUseItemCallCountForTests);
        Assert.Equal(new[] { Bomb }, rig.Starts);
    }

    // -----------------------------------------------------------------------------------------
    // (a3) the refusals.
    // -----------------------------------------------------------------------------------------

    /// <summary>-1: no item owned (<c>0x8004E0F8</c> answers -1); 0x1E and 0x33: out of <c>0x1F</c>-<c>0x32</c>; 0x21, 0x22, 0x28, 0x2A: the holes of the table; 0x62: the bound of the first refusal.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0x1E)]
    [InlineData(0x21)]
    [InlineData(0x22)]
    [InlineData(0x28)]
    [InlineData(0x2A)]
    [InlineData(0x33)]
    [InlineData(0x62)]
    public void A3_AnItemThatTheTableRefuses_PlaysTheSound3_AndStartsNothing_TheSameFrontWithAnItemOfTheTableStarts(int item)
    {
        var rig = NewRig(item: item < 0 ? null : item);
        Seams(rig);

        Front(rig);
        Assert.Equal(new[] { RefusalSound }, rig.Sounds.Requests);
        Assert.Empty(rig.Starts);
        Assert.Equal(0u, rig.Hero.ItemInUse);

        rig.Sounds.Requests.Clear();
        Equip(rig, (int)Bomb);
        Front(rig);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
    }

    /// <summary>The items of <c>0x1F</c>-<c>0x32</c> that are not holes all reach their start (the bound of the first refusal is not lowered, no hole is added).</summary>
    [Theory]
    [InlineData(0x1F)]
    [InlineData(0x20)]
    [InlineData(0x23)]
    [InlineData(0x24)]
    [InlineData(0x25)]
    [InlineData(0x26)]
    [InlineData(0x27)]
    [InlineData(0x29)]
    [InlineData(0x2B)]
    [InlineData(0x2C)]
    [InlineData(0x2D)]
    [InlineData(0x2E)]
    [InlineData(0x2F)]
    [InlineData(0x30)]
    [InlineData(0x31)]
    [InlineData(0x32)]
    public void A3_EveryItemOfTheTableThatIsNotAHole_ReachesItsStart_NoSound(int item)
    {
        var rig = NewRig(item: item);
        Seams(rig);

        Front(rig);

        Assert.Equal(new[] { (uint)item }, rig.Starts);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal((uint)item, rig.Hero.ItemInUse);
    }

    [Fact]
    public void A3_AnotherItemInUse_IsRefusedWithTheSound3_TheSameItemInUse_IsStartedAgainWithoutSound_AndRestartsTheCounter()
    {
        var rig = NewRig();
        Seams(rig);

        rig.Hero.ItemInUse = Seeds; // an item in use that is not the promoted one.
        Front(rig);
        Assert.Equal(new[] { RefusalSound }, rig.Sounds.Requests);
        Assert.Empty(rig.Starts);
        Assert.Equal(Seeds, rig.Hero.ItemInUse);

        rig.Sounds.Requests.Clear();
        rig.Hero.ItemInUse = Bomb; // the same item, a few ticks into its use.
        rig.Hero.ItemUseCounter = 5;
        rig.Hero.ItemUsePhase = 3;
        Front(rig);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
        Assert.Equal(0, rig.Hero.ItemUseCounter);
        Assert.Equal(0, rig.Hero.ItemUsePhase);
    }

    // -----------------------------------------------------------------------------------------
    // (a4) the answer of the start.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A4_AStartThatAnswersZero_PutsTheItemInUse_WithTheCounterAndThePhaseAtZero_AndEndsTheTick()
    {
        var rig = NewRig();
        Seams(rig);
        rig.Hero.ItemUseCounter = 9;
        rig.Hero.ItemUsePhase = 4;

        Front(rig, Right);

        Assert.Equal(Bomb, rig.Hero.ItemInUse);
        Assert.Equal(0, rig.Hero.ItemUseCounter);
        Assert.Equal(0, rig.Hero.ItemUsePhase);
        Assert.Equal(Idle, rig.Hero.TargetAnimationId); // TryUseItem answered 0: the state did not go on to Moving.
    }

    [Fact]
    public void A4_AStartThatAnswersNonZero_AnInstantItem_LeavesNothingInUse_AndTheTickGoesOn()
    {
        var rig = NewRig();
        Seams(rig, start: 1);

        Front(rig, Right);

        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(0u, rig.Hero.ItemInUse);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(Moving, rig.Hero.TargetAnimationId);
    }

    [Fact]
    public void A4_TheStubOfAnItemAnotherSliceCarries_StartsNothing_PlaysNoSound_AndJournalsTheCall_TheSeamedStartDoesPutTheItemInUse()
    {
        var rig = NewRig();
        var journal = new List<string>();
        PlayerManager.ItemStubCallsForTests = journal;

        Front(rig, Right);
        Assert.Equal(new[] { "start 0x23" }, journal);
        Assert.Equal(0u, rig.Hero.ItemInUse);
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(Moving, rig.Hero.TargetAnimationId);

        Seams(rig);
        rig.Hero.TargetAnimationId = Idle;
        Front(rig, Right);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
        Assert.Equal(new[] { "start 0x23" }, journal); // the seam replaced the stub.
    }

    // -----------------------------------------------------------------------------------------
    // (a5) the driver.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A5_TheDriverIsCalledOncePerFreeTick_BeforeThePadIsRead_AndNotWithoutAnItemInUse()
    {
        var rig = NewRig();
        uint? animationAtTheCall = null;
        Seams(rig);
        var inner = PlayerManager.ItemDriverForTests!;
        PlayerManager.ItemDriverForTests = (hero, item) =>
        {
            animationAtTheCall = hero.TargetAnimationId;
            return inner(hero, item);
        };

        Move(rig, Right);
        Assert.Empty(rig.Drivers); // nothing in use: the driver of an item is not called.
        Assert.Equal(Moving, rig.Hero.TargetAnimationId);

        rig.Hero.TargetAnimationId = Idle;
        rig.Hero.ItemInUse = Bomb;
        Move(rig, Right);
        Assert.Single(rig.Drivers);
        Assert.Equal(Idle, animationAtTheCall); // before the pad turned Idle into Moving.
        Assert.Equal(Moving, rig.Hero.TargetAnimationId);
    }

    [Fact]
    public void A5_TheDriverIsNotCalledInTheLockedBranch_NorWithBlockedByEntity_ItIsCalledOnTheFreeTick()
    {
        var rig = NewRig();
        Seams(rig);
        rig.Hero.ItemInUse = Bomb;

        rig.State.PlayerControlFlags = GameState.PlayerControlBits.ControlLocked;
        Move(rig);
        Assert.Empty(rig.Drivers);

        rig.State.PlayerControlFlags = 0;
        rig.Hero.ItemInUse = Bomb;
        rig.Hero.BlockedByEntity = new CasaEngine.Framework.Scene.Entities.Entity();
        Move(rig);
        Assert.Empty(rig.Drivers);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);

        rig.Hero.BlockedByEntity = null;
        Move(rig);
        Assert.Single(rig.Drivers);
    }

    [Fact]
    public void A5_TheCounterRisesByOnePerFreeTick_AndIsSaturatedAt7FFFFFFF()
    {
        var rig = NewRig();
        Seams(rig);
        rig.Hero.ItemInUse = Bomb;

        Move(rig);
        Move(rig);
        Move(rig);
        Assert.Equal(3, rig.Hero.ItemUseCounter);

        rig.Hero.ItemUseCounter = int.MaxValue - 1;
        Move(rig);
        Assert.Equal(int.MaxValue, rig.Hero.ItemUseCounter);
        Move(rig);
        Assert.Equal(int.MaxValue, rig.Hero.ItemUseCounter);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
    }

    [Fact]
    public void A5_TheItemStaysInUseWhileTheDriverAnswersZero_AndIsFreedAtTheTickItAnswersNonZero()
    {
        var rig = NewRig();
        Seams(rig, driverAnswer: call => call >= 4 ? 1 : 0);
        rig.Hero.ItemInUse = Bomb;

        for (var tick = 1; tick <= 3; tick++)
        {
            Move(rig);
            Assert.Equal(Bomb, rig.Hero.ItemInUse);
        }

        Move(rig); // the fourth call answers non-zero.
        Assert.Equal(0u, rig.Hero.ItemInUse);
        Assert.Equal(4, rig.Hero.ItemUseCounter);
        Assert.Equal(4, rig.Drivers.Count);

        Move(rig);
        Assert.Equal(4, rig.Drivers.Count); // freed: no more call.
    }

    [Fact]
    public void A5_TheDriverOfTheItemSeesTheCounterAtZeroAtItsFirstCall_TheTickAfterTheStart_AndAtOneAtTheSecond()
    {
        var rig = NewRig();
        Seams(rig);

        Front(rig); // the start: the binary's driver is called before the pad, so not at this tick.
        Assert.Empty(rig.Drivers);
        Assert.Equal(0, rig.Hero.ItemUseCounter);

        Move(rig);
        Move(rig);

        Assert.Equal(new[] { (Bomb, 0), (Bomb, 1) }, rig.Drivers);
        Assert.Equal(2, rig.Hero.ItemUseCounter);
    }

    // -----------------------------------------------------------------------------------------
    // (a6) the cancellation.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A6_TheLockedBranchCancelsTheItem_TheItemSeesItselfInUse_ThenItIsFreed_WithoutAnItemNothingIsCalled()
    {
        var rig = NewRig();
        Seams(rig);
        rig.State.PlayerControlFlags = GameState.PlayerControlBits.ControlLocked;

        Move(rig);
        Assert.Empty(rig.Cancels); // no item in use: nothing to cancel.

        rig.Hero.ItemInUse = Bomb;
        Move(rig);
        Assert.Equal(new[] { (Bomb, Bomb) }, rig.Cancels); // it still sees the item in use...
        Assert.Equal(0u, rig.Hero.ItemInUse); // ...which is freed right after.
        Assert.Empty(rig.Drivers);
    }

    public static IEnumerable<object[]> SkipStates() =>
        new uint[] { 0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D, 0x50, 0x7F }.Select(s => new object[] { s });

    /// <summary>The 15 states without handler and every state from 0x50: the main chain and the swim cancel the item. They are listed by the swim table (<see cref="PlayerManager.SwimHandlerOf"/>), never by a final else.</summary>
    [Theory]
    [MemberData(nameof(SkipStates))]
    public void A6_TheStatesWithoutHandler_CancelTheItem_OnTheGroundAndInTheDeepWater(uint state)
    {
        foreach (var slope in new[] { 0, 4 })
        {
            var rig = NewRig(state, slope: slope);
            Seams(rig);
            rig.Hero.ItemInUse = Bomb;

            Move(rig);

            Assert.Equal(new[] { (Bomb, Bomb) }, rig.Cancels);
            Assert.Equal(0u, rig.Hero.ItemInUse);
            Assert.Single(rig.Drivers); // the free tick ran the driver once before.
        }
    }

    /// <summary>A state the DLL carries (Idle, the hurt state, the run) and the states of the cape (0x20), the tomes (0x32) and the death (0x39), which the DLL does not carry yet and which are not in the table of the states without handler: no cancellation.</summary>
    [Theory]
    [InlineData(0x00u)]
    [InlineData(0x31u)]
    [InlineData(0x03u)]
    [InlineData(0x20u)]
    [InlineData(0x32u)]
    [InlineData(0x39u)]
    public void A6_AStateThatIsNotInTheTableOfTheStatesWithoutHandler_DoesNotCancelTheItem_OnTheGroundAndInTheDeepWater(uint state)
    {
        foreach (var slope in new[] { 0, 4 })
        {
            var rig = NewRig(state, slope: slope);
            Seams(rig);
            rig.Hero.ItemInUse = Bomb;

            Move(rig);

            Assert.Empty(rig.Cancels);
            Assert.Equal(Bomb, rig.Hero.ItemInUse);
        }
    }

    // -----------------------------------------------------------------------------------------
    // (a7) the states.
    // -----------------------------------------------------------------------------------------

    /// <summary>The 51 states of the Circle table (<c>0x80022D3C</c>) that a line of <c>MovePlayer</c> carries (results.md section 1, the lines "DLL").</summary>
    private static readonly uint[] StatesWithAnItemCall =
    {
        0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1C, 0x1D, 0x1F,
        0x25, 0x26, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x35, 0x3A, 0x3B, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x44, 0x46, 0x49, 0x4B,
    };

    /// <summary>The 17 states of the table that do nothing about the Circle (<c>0x80031FF4</c>, <c>0x800325E8</c>, <c>0x80032758</c>) and the states from 0x50 on.</summary>
    private static readonly uint[] StatesWithoutAnItemCall =
    {
        0x03, 0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x36, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D, 0x50, 0x7F,
    };

    public static IEnumerable<object[]> WithCall() => StatesWithAnItemCall.Select(s => new object[] { s });

    public static IEnumerable<object[]> WithoutCall() => StatesWithoutAnItemCall.Select(s => new object[] { s });

    [Fact]
    public void A7_TheTableHasTheFiftyOneStatesOfTheDll_AndTheSeventeenWithoutReaction()
    {
        Assert.Equal(51, StatesWithAnItemCall.Distinct().Count());
        Assert.Equal(StatesWithAnItemCall.Length, StatesWithAnItemCall.Distinct().Count());
        Assert.Empty(StatesWithAnItemCall.Intersect(StatesWithoutAnItemCall));
        Assert.Equal(17, StatesWithoutAnItemCall.Count(s => s < 0x50));
    }

    [Theory]
    [MemberData(nameof(WithCall))]
    public void A7_AnEdgeOfTheCircle_InAStateThatCallsTryUseItem_StartsTheItem(uint state)
    {
        var rig = NewRig(state);
        Seams(rig);

        Front(rig);

        Assert.Equal(new[] { Bomb }, rig.Starts);
        Assert.Equal(Bomb, rig.Hero.ItemInUse);
        Assert.Empty(rig.Sounds.Requests);
    }

    [Theory]
    [MemberData(nameof(WithoutCall))]
    public void A7_AnEdgeOfTheCircle_InAStateWithoutReaction_DoesNotCallTheStart_TheSameEdgeInIdleDoes(uint state)
    {
        var rig = NewRig(state);
        Seams(rig);

        Front(rig);
        Assert.Empty(rig.Starts);
        Assert.Empty(rig.Sounds.Requests);

        rig.Hero.TargetAnimationId = Idle;
        Front(rig);
        Assert.Equal(new[] { Bomb }, rig.Starts);
    }

    // -----------------------------------------------------------------------------------------
    // (a9) the hero rebuilt.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A9_TheHeroOfANewMap_HasNoItemInUse_TheHeroOfTheMapBeforeHadOne()
    {
        var spec = new ArcSpec("E20h0a-a9", "Giles Nightmare", "Giles Nightmare-103", Array.Empty<int>(), 8, 6, 0, 200, RealController: true, Prefabs: true);

        using (var before = new ArcRun(spec))
        {
            before.Hero.ItemInUse = Bomb;
            before.Hero.ItemUseCounter = 7;
            before.Hero.ItemUsePhase = 2;
            Assert.Equal(Bomb, before.Hero.ItemInUse);
        }

        using var after = new ArcRun(spec);

        Assert.Equal(0u, after.Hero.ItemInUse);
        Assert.Equal(0, after.Hero.ItemUseCounter);
        Assert.Equal(0, after.Hero.ItemUsePhase);
    }
}
