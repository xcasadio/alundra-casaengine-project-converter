#nullable enable
using System;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Hero;

namespace Alundra.Tests;

/// <summary>
/// E20.f0 T4 (docs/plan-e20-heros.md section 1.8): the inventory helpers of <see cref="ArcRun"/> - <c>Give</c>, <c>Equip</c>, <c>SetMp</c>, <c>SetItem</c> - go through the DLL's
/// real setters. Values are written by hand: the Hunter's Bow is item 5 (slot 2, rank 0, maximum count 1), its counter index is 11, and an arc with an arrival starts with an
/// empty inventory and no weapon (docs/plan-e20-annexe/e20-armes/test-means.md section 0.4).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraArcInventoryHelpersTests
{
    /// <summary>The overworld map 10 from a portal arrival (the arc of the hero attack tests): the hero stands free on open ground, with an empty inventory.</summary>
    private static ArcSpec Spec(string name) => new(
        name, "Overworld", "Overworld 2,1-10", Array.Empty<int>(), 0, 0, 0, 400, RealController: true, Prefabs: true,
        Arrival: new ArcArrival(16515072, 61341696, 0, GameState.ResetAnimationId, 16));

    private static void WaitUntilTheHeroIsFree(ArcRun arc)
    {
        for (var frame = 0; frame < 300 && !(arc.Hero.TargetAnimationId == 0u && arc.Hero.CurrentAnimationId == 0u && ArcRun.State.PlayerControlFlags == 0); frame++)
        {
            arc.OneFrame();
        }

        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
    }

    [Fact]
    public void GiveThenEquip_TheHuntersBow_MakesItTheCurrentWeapon_AndTheHeroDrawsIt()
    {
        using var arc = new ArcRun(Spec("E20f0-bow"));
        var tables = ((IScriptHost)arc.Proxy).ItemTables!;
        WaitUntilTheHeroIsFree(arc);
        Assert.Equal(0, ArcRun.State.NumberOfItems[11]); // nothing owned before.
        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(ArcRun.State, tables));

        arc.Give(5);
        arc.Equip(2);

        Assert.Equal(1, ArcRun.State.NumberOfItems[11]);
        Assert.Equal((short)2, ArcRun.State.PlayerStats.WeaponId);
        Assert.Equal(5u, PlayerManager.GetItemIdFromCurrentWeapon(ArcRun.State, tables));

        // The tick reads the equipped item: Square draws the bow (animation 0x13 of the oracle's Hunter's Bow).
        arc.Press(PadState.Square);
        Assert.Equal(0x13u, arc.Hero.TargetAnimationId);
    }

    [Fact]
    public void Give_SeveralItems_GivesOneOfEach_AndAGivenWeaponStaysAtItsMaximumOfOne()
    {
        using var arc = new ArcRun(Spec("E20f0-give"));

        arc.Give(5, 9, 14);
        arc.Give(5);

        Assert.Equal(1, ArcRun.State.NumberOfItems[11]); // item 5.
        Assert.Equal(1, ArcRun.State.NumberOfItems[19]); // item 9.
        Assert.Equal(1, ArcRun.State.NumberOfItems[29]); // item 14.
        Assert.Equal(0, ArcRun.State.NumberOfItems[33]); // item 16: not given.
    }

    [Fact]
    public void Equip_ASlotWithNoOwnedItem_ResolvesToNoItem_AndMinusOneUnequips()
    {
        using var arc = new ArcRun(Spec("E20f0-equip"));
        var tables = ((IScriptHost)arc.Proxy).ItemTables!;

        arc.Equip(4);

        Assert.Equal((short)4, ArcRun.State.PlayerStats.WeaponId);
        Assert.Equal(PlayerManager.NoItem, PlayerManager.GetItemIdFromCurrentWeapon(ArcRun.State, tables));

        arc.Equip(-1);

        Assert.Equal((short)-1, ArcRun.State.PlayerStats.WeaponId);
    }

    [Fact]
    public void SetMp_AppliesTheCeilingOf4_AndClampsTheMagicToItsMaximum()
    {
        using var arc = new ArcRun(Spec("E20f0-mp"));

        arc.SetMp(4, 3);
        Assert.Equal(((short)4, (short)3), (ArcRun.State.PlayerStats.MpMax, ArcRun.State.PlayerStats.Mp));

        arc.SetMp(9, 9);
        Assert.Equal(((short)4, (short)4), (ArcRun.State.PlayerStats.MpMax, ArcRun.State.PlayerStats.Mp));

        arc.SetMp(2, 4);
        Assert.Equal(((short)2, (short)2), (ArcRun.State.PlayerStats.MpMax, ArcRun.State.PlayerStats.Mp));
    }

    [Fact]
    public void SetItem_PosesTheCircleItem_AndRefusesAnIdPastTheTable()
    {
        using var arc = new ArcRun(Spec("E20f0-item"));

        arc.SetItem(36);
        Assert.Equal((short)36, ArcRun.State.PlayerStats.ItemId);

        arc.SetItem(99); // outside 0..98: the setter logs and keeps the previous item.
        Assert.Equal((short)36, ArcRun.State.PlayerStats.ItemId);
    }
}
