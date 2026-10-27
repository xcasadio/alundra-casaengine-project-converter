#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.h4a T1 (docs/plan-e20-heros.md section 1.31.1, the tests (t1), (t2), (t5), (t6) and (o2)): the start of the tomes <c>0x800348E0</c> on the bench of map 14 (<see cref="TomeBench"/>, one tick per frame), installed by the seam
/// <see cref="PlayerManager.ItemStartForTests"/> (the production table keeps the tomes as stubs, D-H-3). The refusals are in the binary's order (the object in the hands, the animation, the item in use, the magic), every refusal is
/// paired with the start that succeeds; the track of the music is told to <see cref="PlayerManager.TomeTrackCallsForTests"/> (the stub of <c>0x8005A9E0</c>, Q-H4-1). The expectations are those of
/// <c>docs/plan-e20-annexe/e20-h4/work/results/staff_check.txt</c> and <c>vec_start.txt</c>, replayed in <c>docs/plan-e20-annexe/e20-h4a/</c>.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TomeStartTests : IDisposable
{
    private const int RefusalSound = 3;
    private const uint FirstTome = 0x2B;
    private const uint Staff = 7;

    public TomeStartTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
    }

    private static List<(uint Item, byte Track)> Install()
    {
        var calls = new List<(uint Item, byte Track)>();
        PlayerManager.TomeTrackCallsForTests = calls;
        PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
        return calls;
    }

    // -----------------------------------------------------------------------------------------
    // (t1) the start.
    // -----------------------------------------------------------------------------------------

    /// <summary>The octet <c>0x80023365 + object</c> of the tracks, for <c>0x2B</c> to <c>0x32</c> (<c>report.md</c> section 8).</summary>
    [Theory]
    [InlineData(0x2Bu, (byte)0)]
    [InlineData(0x2Cu, (byte)1)]
    [InlineData(0x2Du, (byte)6)]
    [InlineData(0x2Eu, (byte)6)]
    [InlineData(0x2Fu, (byte)2)]
    [InlineData(0x30u, (byte)3)]
    [InlineData(0x31u, (byte)4)]
    [InlineData(0x32u, (byte)5)]
    public void T1_TheStartOfATome_OnTheCircle_TakesOneMagic_PutsTheHeroInTheCast_LogsTheTrack_AndKeepsTheTome(uint item, byte track)
    {
        using var bench = new TomeBench("t1-ok-" + item, 4, 4, (int)item);
        var calls = Install();
        PlayerManager.SetCurrentItemId(GameState.Instance, item);
        var owned = GameState.Instance.NumberOfItems[(int)item * 2 + 1];
        Assert.Equal(1, owned);

        bench.Tick(TomeBench.Circle);

        Assert.Equal(item, bench.Hero.ItemInUse);
        Assert.Equal(0, bench.Hero.ItemUseCounter);
        Assert.Equal(0, bench.Hero.ItemUsePhase);
        Assert.Equal(3, TomeBench.Stats.Mp);
        Assert.Equal(PlayerManager.TomeCastAnimationId, bench.Hero.TargetAnimationId);
        Assert.Equal(new[] { (item, track) }, calls);
        Assert.Empty(bench.Sounds.Requests);
        Assert.Equal(owned, GameState.Instance.NumberOfItems[(int)item * 2 + 1]); // the tomes are not consumed.
    }

    [Fact]
    public void T1_TheHeroInTheHands_Refuses_WithTheSound3_TheSameStartWithEmptyHandsSucceeds()
    {
        using var bench = new TomeBench("t1-carried", 4, 4, (int)FirstTome);
        var calls = Install();

        bench.Hero.CarriedEntity = new CasaEngine.Framework.Scene.Entities.Entity();
        Assert.Equal(1, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Equal(new[] { RefusalSound }, bench.Sounds.Requests);
        Assert.Equal(4, TomeBench.Stats.Mp);
        Assert.Equal(0u, bench.Hero.TargetAnimationId);
        Assert.Empty(calls);

        bench.Hero.CarriedEntity = null;
        bench.Sounds.Requests.Clear();
        Assert.Equal(0, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Empty(bench.Sounds.Requests);
        Assert.Equal(3, TomeBench.Stats.Mp);
        Assert.Equal(PlayerManager.TomeCastAnimationId, bench.Hero.TargetAnimationId);
        Assert.Single(calls);
    }

    /// <summary>The animation must be Idle (0) or Moving (1): 2 is refused (the bound is "&lt; 2"), as the swim state 0x1D or an attack.</summary>
    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(0x1Du)]
    [InlineData(0x32u)]
    public void T1_AnAnimationOtherThanIdleAndMoving_Refuses_WithTheSound3_IdleAndMovingStart(uint state)
    {
        using var bench = new TomeBench("t1-anim-" + state, 4, 4, (int)FirstTome);
        var calls = Install();

        bench.Hero.TargetAnimationId = state;
        Assert.Equal(1, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Equal(new[] { RefusalSound }, bench.Sounds.Requests);
        Assert.Equal(4, TomeBench.Stats.Mp);
        Assert.Equal(state, bench.Hero.TargetAnimationId);
        Assert.Empty(calls);

        foreach (var ok in new[] { 0u, 1u })
        {
            bench.Sounds.Requests.Clear();
            TomeBench.Stats.Mp = 4;
            bench.Hero.TargetAnimationId = ok;
            Assert.Equal(0, PlayerManager.TomeStart(bench.Hero, FirstTome));
            Assert.Empty(bench.Sounds.Requests);
            Assert.Equal(3, TomeBench.Stats.Mp);
            Assert.Equal(PlayerManager.TomeCastAnimationId, bench.Hero.TargetAnimationId);
        }
    }

    /// <summary>Any item in use refuses, the same tome included (the frame lets the same object through, the start refuses it, fact 1).</summary>
    [Theory]
    [InlineData(0x2Bu)]
    [InlineData(0x23u)]
    public void T1_AnItemInUse_Refuses_WithTheSound3_NoItemInUseStarts(uint inUse)
    {
        using var bench = new TomeBench("t1-inuse-" + inUse, 4, 4, (int)FirstTome);
        var calls = Install();

        bench.Hero.ItemInUse = inUse;
        Assert.Equal(1, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Equal(new[] { RefusalSound }, bench.Sounds.Requests);
        Assert.Equal(4, TomeBench.Stats.Mp);
        Assert.Equal(0u, bench.Hero.TargetAnimationId);
        Assert.Empty(calls);

        bench.Hero.ItemInUse = 0;
        bench.Sounds.Requests.Clear();
        Assert.Equal(0, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Empty(bench.Sounds.Requests);
        Assert.Equal(3, TomeBench.Stats.Mp);
        Assert.Single(calls);
    }

    /// <summary>Without the staff, 0 magic refuses (the decompilation starts the spell at 0 magic: D1, the binary decides); 1 magic starts and leaves 0.</summary>
    [Fact]
    public void T1_NoMagic_WithTheSword_Refuses_WithTheSound3_OneMagicStartsAndLeavesNone()
    {
        using var bench = new TomeBench("t1-nomp", 4, 0, (int)FirstTome);
        var calls = Install();
        Assert.Equal(1u, bench.WeaponId);

        Assert.Equal(1, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Equal(new[] { RefusalSound }, bench.Sounds.Requests);
        Assert.Equal(0, TomeBench.Stats.Mp);
        Assert.Equal(0u, bench.Hero.TargetAnimationId);
        Assert.Empty(calls);

        bench.Sounds.Requests.Clear();
        TomeBench.Stats.Mp = 1;
        Assert.Equal(0, PlayerManager.TomeStart(bench.Hero, FirstTome));
        Assert.Empty(bench.Sounds.Requests);
        Assert.Equal(0, TomeBench.Stats.Mp);
        Assert.Equal(PlayerManager.TomeCastAnimationId, bench.Hero.TargetAnimationId);
        Assert.Single(calls);
    }

    /// <summary>The staff (the object 7 owned and the weapon of the slot 6 equipped, <c>staff_check.txt:1</c>): neither the requirement nor the cost of the magic; the sword beside it pays one.</summary>
    [Theory]
    [InlineData(true, 0, 0, true)]
    [InlineData(false, 0, 0, false)]
    [InlineData(false, 2, 1, true)]
    [InlineData(true, 2, 2, true)]
    public void T1_TheStaff_NeitherNeedsNorTakesTheMagic_TheSwordDoes(bool staff, int mp, int mpAfter, bool started)
    {
        using var bench = new TomeBench("t1-staff-" + staff + mp, 4, mp, (int)FirstTome, (int)Staff);
        var calls = Install();
        if (staff)
        {
            bench.Arc.Equip(6);
        }

        Assert.Equal(staff ? Staff : 1u, bench.WeaponId);

        var answer = PlayerManager.TomeStart(bench.Hero, FirstTome);

        Assert.Equal(started ? 0 : 1, answer);
        Assert.Equal(mpAfter, TomeBench.Stats.Mp);
        Assert.Equal(started ? PlayerManager.TomeCastAnimationId : 0u, bench.Hero.TargetAnimationId);
        Assert.Equal(started ? new[] { (FirstTome, (byte)0) } : Array.Empty<(uint, byte)>(), calls);
        Assert.Equal(started ? Array.Empty<int>() : new[] { RefusalSound }, bench.Sounds.Requests);
    }

    // -----------------------------------------------------------------------------------------
    // (t2) DecreaseMp.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void T2_DecreaseMp_IsBoundedToZeroAndTheMaximum_AMagicInTheRangeIsExact()
    {
        var state = new GameState();
        PlayerManager.SetPlayerMpMax(state, 4);
        PlayerManager.SetPlayerMp(state, 3);

        Assert.Equal(2, PlayerManager.DecreaseMp(state, 1));
        Assert.Equal(2, state.PlayerStats.Mp);
        Assert.Equal(1, PlayerManager.DecreaseMp(state, 1));
        PlayerManager.SetPlayerMp(state, 0);
        Assert.Equal(0, PlayerManager.DecreaseMp(state, 1));   // the floor
        Assert.Equal(0, state.PlayerStats.Mp);
        PlayerManager.SetPlayerMp(state, 1);
        Assert.Equal(4, PlayerManager.DecreaseMp(state, -9));  // the ceiling (the maximum)
        Assert.Equal(4, state.PlayerStats.Mp);
        Assert.Equal(1, PlayerManager.DecreaseMp(state, 3));
        Assert.Equal(1, state.PlayerStats.Mp);
    }

    // -----------------------------------------------------------------------------------------
    // (t5) the promotion.
    // -----------------------------------------------------------------------------------------

    /// <summary>With the tomes 43 and 44 owned and the 43 on the Circle: at the first press without a promotion before it the start receives 43 and the Circle becomes 44; a tick before the press (the end of the tick promotes) it receives 44.</summary>
    [Fact]
    public void T5_TheStartReceivesTheObjectTheCircleWasOn_TheTickBeforeThePromotionHasMadeItTheBetterTome()
    {
        using (var bench = new TomeBench("t5-first", 4, 4, 43, 44))
        {
            var calls = Install();
            PlayerManager.SetCurrentItemId(GameState.Instance, 43);

            bench.Tick(TomeBench.Circle);

            Assert.Equal(43u, bench.Hero.ItemInUse);
            Assert.Equal(new[] { (43u, (byte)0) }, calls);
            Assert.Equal(44, GameState.Instance.PlayerStats.ItemId);
        }

        using (var bench = new TomeBench("t5-promoted", 4, 4, 43, 44))
        {
            var calls = Install();
            PlayerManager.SetCurrentItemId(GameState.Instance, 43);
            bench.Tick();
            Assert.Equal(44, GameState.Instance.PlayerStats.ItemId);
            Assert.Empty(calls);

            bench.Tick(TomeBench.Circle);

            Assert.Equal(44u, bench.Hero.ItemInUse);
            Assert.Equal(new[] { (44u, (byte)1) }, calls);
        }
    }

    // -----------------------------------------------------------------------------------------
    // (t6) the calendar.
    // -----------------------------------------------------------------------------------------

    /// <summary>After a real start the driver is called for the first time at the tick that follows, with the counter at 0 (fact 3); before the start it is not called.</summary>
    [Fact]
    public void T6_TheDriver_IsFirstCalledAtTheTickAfterTheStart_WithTheCounterAtZero()
    {
        using var bench = new TomeBench("t6", 4, 4, (int)FirstTome);
        Install();
        var drivers = new List<(uint Item, int Counter)>();
        PlayerManager.ItemDriverForTests = (hero, item) =>
        {
            drivers.Add((item, hero.ItemUseCounter));
            return 0;
        };
        PlayerManager.SetCurrentItemId(GameState.Instance, FirstTome);

        bench.Frames(3);
        Assert.Empty(drivers); // nothing in use: no driver.

        bench.Tick(TomeBench.Circle);
        Assert.Equal(FirstTome, bench.Hero.ItemInUse);
        Assert.Empty(drivers); // the start tick: the driver ran at its head, before the start.

        bench.Tick();
        Assert.Equal(new[] { (FirstTome, 0) }, drivers);

        bench.Tick();
        Assert.Equal(new[] { (FirstTome, 0), (FirstTome, 1) }, drivers);
    }

    // -----------------------------------------------------------------------------------------
    // (o2) the four cases of the staff, through the frame.
    // -----------------------------------------------------------------------------------------

    /// <summary><c>staff_check.txt</c>, the four lines: weapon id, magic before to after, whether the cast started, the animation of the hero, the sounds.</summary>
    [Theory]
    [InlineData(true, 0u, 7u, 0, 0, true, 0x32u, new int[0])]
    [InlineData(false, 0u, 1u, 0, 0, false, 0u, new[] { 3 })]
    [InlineData(false, 2u, 1u, 2, 1, true, 0x32u, new int[0])]
    [InlineData(true, 2u, 7u, 2, 2, true, 0x32u, new int[0])]
    public void O2_TheFourCasesOfTheStaff_AreTheOracle(bool staff, uint mp, uint weapon, int mpBefore, int mpAfter, bool started, uint animation, int[] sounds)
    {
        using var bench = new TomeBench("o2-" + staff + mp, 4, (int)mp, (int)FirstTome, (int)Staff);
        Install();
        if (staff)
        {
            bench.Arc.Equip(6);
        }

        PlayerManager.SetCurrentItemId(GameState.Instance, FirstTome);
        Assert.Equal(weapon, bench.WeaponId);
        Assert.Equal(mpBefore, TomeBench.Stats.Mp);

        bench.Tick(TomeBench.Circle);

        Assert.Equal(started, bench.Hero.ItemInUse == FirstTome);
        Assert.Equal(mpAfter, TomeBench.Stats.Mp);
        Assert.Equal(animation, bench.Hero.TargetAnimationId);
        Assert.Equal(sounds, bench.Sounds.Requests.ToArray());
    }
}
