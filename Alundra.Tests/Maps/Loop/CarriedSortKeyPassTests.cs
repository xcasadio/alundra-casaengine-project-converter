#nullable enable
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;
using static Alundra.Tests.Support.DrawOrderRig;

namespace Alundra.Tests.Maps.Loop;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8, D-G1D-1 to D-G1D-6; (k1) in the pass, (w5), (g1), (e1)): what the sort pass of the frame (<see cref="Alundra.Maps.Loop.FrameSyncPasses.RunWallInterleaveSortKeyPass"/>) writes on the
/// <see cref="CasaEngine.Framework.Scene.Entities.Components.DepthSortable2DComponent"/> of an object in the hands of the hero, of an object he threw while it flies, and of every other entity, on entities without a world (the rendered translation of each set by hand,
/// <see cref="DrawOrderRig"/>). The world tests with the real hero are <c>BombHandsDrawOrderTests</c> and <c>CrateHandsDrawOrderTests</c>.
/// </summary>
public sealed class CarriedSortKeyPassTests
{
    private const int K = EntityDepthKey.HeldDrawOffset;
    private const uint BombFlags = 0x0013234E;  // not a collidable
    private const uint CrateFlags = 0x003323E4; // a collidable

    private static int Hi(uint key) => (int)key >> 16;

    private static (Piece Hero, Piece Bomb) HeroAndHeldBomb(int heroY = 206 * Px + 5898, int heroZ = 13 * Px + Px / 2, int heroIdsv = 4, int bombIdsv = 5, uint bombFlags = BombFlags)
    {
        var hero = Make("hero", heroY, heroZ, heroIdsv, flags: 0x0003118C);
        hero.Proxy.IsPlayer = true;
        var bomb = Make("bomb", heroY, heroZ + 32 * Px, bombIdsv, bombFlags, renderZ: (heroZ + 32 * Px) >> 16);
        bomb.Proxy.PlatformEntity = hero.Entity;
        hero.Proxy.CarriedEntity = bomb.Entity;
        return (hero, bomb);
    }

    // (k1) in the pass: a held object takes the coordinate of its carrier EXACTLY on their rendered translations (the hero's height is fractional, the object's is whole) and the offset +K.
    [Fact]
    public void AHeldObject_IsAtTheSortCoordinateOfItsCarrier_OnTheRenderedTranslations_WithTheOffsetPlusK_AndInFrontOfHim()
    {
        var (hero, bomb) = HeroAndHeldBomb();
        Assert.True(KeyOf(bomb).CompareTo(KeyOf(hero)) < 0, "before the pass the held bomb is behind: that is the defect");

        Pass(hero, bomb);

        Assert.Equal(KeyOf(hero).SortCoordinate, KeyOf(bomb).SortCoordinate);
        Assert.Equal(K, bomb.Depth.LocalSortOffset);
        Assert.Equal(K, KeyOf(bomb).LocalSortOffset);
        Assert.True(KeyOf(bomb).CompareTo(KeyOf(hero)) > 0);
        Assert.Equal(KeyOf(hero).SortCoordinate, bomb.Proxy.DrawnSortCoordinate);
        Assert.Equal(Vector2.Zero, hero.Depth.SortAnchorLocal);
        Assert.Equal(0, hero.Depth.LocalSortOffset);
        Assert.Null(hero.Proxy.DrawnSortCoordinate);
    }

    // D-G1D-2: the Elevation of a held object is at least its carrier's, collidable or not (the binary raises the collidables only; the bomb is not one).
    [Theory]
    [InlineData(BombFlags)]
    [InlineData(CrateFlags)]
    public void AHeldObject_IsRaisedToTheElevationOfItsCarrier_CollidableOrNot(uint flags)
    {
        // the hero shows an IDSV of 6 and the object one of 0: its own bucket is the one below.
        var (hero, obj) = HeroAndHeldBomb(heroY: 107 * Px, heroZ: 5 * Px, heroIdsv: 6, bombIdsv: 0, bombFlags: flags);
        Assert.True(WallPlacementOverlay.ComputeEntityElevation(obj.Proxy.PosY, 0) < WallPlacementOverlay.ComputeEntityElevation(hero.Proxy.PosY, 6));

        Pass(hero, obj);

        Assert.Equal(hero.Depth.Elevation, obj.Depth.Elevation);
        Assert.True(KeyOf(obj).CompareTo(KeyOf(hero)) > 0);
    }

    // D-G1D-5 / (w5): an object that leaves the hands without being thrown (a bomb that explodes there, a wound) finds (0, 0) and 0 at the next frame.
    [Fact]
    public void AnObjectThatLeavesTheHandsWithoutBeingThrown_GetsAZeroAnchorAndOffsetAtTheNextFrame()
    {
        var (hero, bomb) = HeroAndHeldBomb();
        Pass(hero, bomb);
        Assert.NotEqual(0, bomb.Depth.LocalSortOffset);
        Assert.NotEqual(Vector2.Zero, bomb.Depth.SortAnchorLocal);

        bomb.Proxy.PlatformEntity = null;
        hero.Proxy.CarriedEntity = null;
        Pass(hero, bomb);

        Assert.Equal(Vector2.Zero, bomb.Depth.SortAnchorLocal);
        Assert.Equal(0, bomb.Depth.LocalSortOffset);
        Assert.Null(bomb.Proxy.DrawnSortCoordinate);
        Assert.Equal(WallPlacementOverlay.ComputeEntityElevation(bomb.Proxy.PosY, 5), bomb.Depth.Elevation);
    }

    // D-G1D-5: neither held nor thrown: (0, 0) and 0 whatever was there.
    [Fact]
    public void AnEntityNeitherHeldNorThrown_IsWrittenAZeroAnchorAndAZeroOffset()
    {
        var other = Make("other", 100 * Px, idsv: 5);
        other.Depth.SortAnchorLocal = new Vector2(0f, 9f);
        other.Depth.LocalSortOffset = 3;

        Pass(other);

        Assert.Equal(Vector2.Zero, other.Depth.SortAnchorLocal);
        Assert.Equal(0, other.Depth.LocalSortOffset);
        Assert.Null(other.Proxy.DrawnSortCoordinate);
    }

    // (g1) an entity flagged to be destroyed stays ignored by the pass, held or thrown.
    [Fact]
    public void AnEntityFlaggedToDestroy_StaysIgnored_HeldOrThrown()
    {
        var (hero, bomb) = HeroAndHeldBomb();
        bomb.Proxy.Status = EntityStatus.FlagToDestroy;
        bomb.Depth.SortAnchorLocal = new Vector2(0f, 7f);
        bomb.Depth.LocalSortOffset = 3;
        bomb.Depth.Elevation = 1234;

        Pass(hero, bomb);

        Assert.Equal(new Vector2(0f, 7f), bomb.Depth.SortAnchorLocal);
        Assert.Equal(3, bomb.Depth.LocalSortOffset);
        Assert.Equal(1234, bomb.Depth.Elevation);

        var thrown = Make("thrown", 100 * Px, 20 * Px, 5, status: EntityStatus.FlagToDestroy);
        thrown.Proxy.ThrownBy = hero.Proxy;
        thrown.Depth.LocalSortOffset = 3;
        Pass(hero, thrown);
        Assert.Equal(3, thrown.Depth.LocalSortOffset);
    }

    // (k1) in the pass, an object in flight: the coordinate of the thrower plus 100 a pixel of the difference of the high halves of the two keys of the binary; its own Elevation.
    [Fact]
    public void AnObjectInFlight_IsDeltaPixelsFromItsThrower_AndKeepsItsOwnElevation()
    {
        // the row of throw_up, tick 50: the hero at 195.9375 (IDSV 2), the bomb a pixel above and 36 up (IDSV 5): keys 00C50000 and 00C70024.
        var hero = Make("hero", (int)(195.9375 * Px), 0, 2, 0x0003118C);
        hero.Proxy.IsPlayer = true;
        var bomb = Make("bomb", (int)(194.9375 * Px), 36 * Px, 5, 0x0013234E, renderZ: 36);
        bomb.Proxy.ThrownBy = hero.Proxy;

        Pass(hero, bomb);

        var heroKey = EntityDepthKey.Compute(hero.Proxy);
        var bombKey = EntityDepthKey.Compute(bomb.Proxy);
        var delta = Hi(bombKey) - Hi(heroKey);
        Assert.Equal(2, delta);
        Assert.Equal(KeyOf(hero).SortCoordinate + 100 * delta, KeyOf(bomb).SortCoordinate);
        Assert.Equal(0, bomb.Depth.LocalSortOffset);
        Assert.Equal(WallPlacementOverlay.ComputeEntityElevation(bomb.Proxy.PosY, 5), bomb.Depth.Elevation);
        Assert.Equal(KeyOf(bomb).SortCoordinate, bomb.Proxy.DrawnSortCoordinate);
    }

    // (k1) in the pass: at the same pixel of the keys the offset is +K above the thrower's key, -K below and 0 at equality.
    [Theory]
    [InlineData(+1)]
    [InlineData(-1)]
    [InlineData(0)]
    public void AnObjectInFlight_AtTheSamePixelAsItsThrower_GetsTheOffsetOfTheSignOfItsKey(int sign)
    {
        var hero = Make("hero", 200 * Px, 0, 5, 0x0003118C);
        hero.Proxy.IsPlayer = true;
        var bomb = Make("bomb", 200 * Px, sign == 0 ? 0 : sign > 0 ? 32 * Px : 0, 5, 0x0013234E, renderZ: sign > 0 ? 32 : 0);
        if (sign < 0)
        {
            hero.Proxy.PosZ = 32 * Px; // the thrower is the higher of the two: his key is the larger
            Place(hero);
        }

        bomb.Proxy.ThrownBy = hero.Proxy;

        Pass(hero, bomb);

        Assert.Equal(KeyOf(hero).SortCoordinate, KeyOf(bomb).SortCoordinate);
        Assert.Equal(sign * K, bomb.Depth.LocalSortOffset);
    }

    // D-G1D-3: the mark falls when the object is on the ground, no longer in the normal status, or is held again.
    [Fact]
    public void TheMarkOfTheThrow_FallsAtTheGround_AtAnotherStatus_AndWhenTheObjectIsHeldAgain()
    {
        Piece Flying(out Piece hero)
        {
            hero = Make("hero", 200 * Px, 0, 5, 0x0003118C);
            hero.Proxy.IsPlayer = true;
            var bomb = Make("bomb", 190 * Px, 20 * Px, 5, 0x0013234E, renderZ: 20);
            bomb.Proxy.ThrownBy = hero.Proxy;
            return bomb;
        }

        var flying = Flying(out var h1);
        Pass(h1, flying);
        Assert.Same(h1.Proxy, flying.Proxy.ThrownBy);
        Assert.NotEqual(Vector2.Zero, flying.Depth.SortAnchorLocal);

        flying.Proxy.IsOnGround = 1;
        Pass(h1, flying);
        Assert.Null(flying.Proxy.ThrownBy);
        Assert.Equal(Vector2.Zero, flying.Depth.SortAnchorLocal);
        Assert.Equal(0, flying.Depth.LocalSortOffset);
        Assert.Null(flying.Proxy.DrawnSortCoordinate);

        var other = Flying(out var h2);
        other.Proxy.Status = EntityStatus.Deactivated;
        Pass(h2, other);
        Assert.Null(other.Proxy.ThrownBy);
        Assert.Equal(0, other.Depth.LocalSortOffset);

        var held = Flying(out var h3);
        held.Proxy.PlatformEntity = h3.Entity;
        Pass(h3, held);
        Assert.Null(held.Proxy.ThrownBy);
        Assert.Equal(K, held.Depth.LocalSortOffset); // and it is drawn as a held object

        // a throw not yet consumed by the program of the object (the code of the throw, Flags2, still set): the mark waits for the release.
        var pending = Flying(out var h4);
        pending.Proxy.PlatformEntity = h4.Entity;
        pending.Proxy.Flags2 = 2;
        Pass(h4, pending);
        Assert.Same(h4.Proxy, pending.Proxy.ThrownBy);
    }

    // D-G1D-3: ThrowCarried marks the object with its thrower; a refused throw marks nothing.
    [Fact]
    public void ThrowCarried_MarksTheObjectWithItsThrower_AndARefusedThrowDoesNot()
    {
        var (hero, item) = CarryBench.Pair(new CarryRules.Triple(0, 0, 32 * Px));
        Assert.Null(item.ThrownBy);

        Assert.True(CarryRules.ThrowCarried(hero, ground: true, directionHeld: false));

        Assert.Same(hero, item.ThrownBy);

        var (hero2, item2) = CarryBench.Pair(new CarryRules.Triple(0, 0, 32 * Px));
        hero2.CombinedVramFlagsOR = PlayerManager.NoAttackVramFlag;
        Assert.False(CarryRules.ThrowCarried(hero2, ground: true, directionHeld: false));
        Assert.Null(item2.ThrownBy);
    }

    // (e1) the effect anchored to a held object (the break of a crate in the hands) sorts on the coordinate the pass gave to the object, also frozen; a loose one keeps the formula of the position.
    [Fact]
    public void TheEffectAnchoredToAHeldOrThrownObject_SortsOnTheCoordinateOfThePass()
    {
        var (hero, crate) = HeroAndHeldBomb(bombFlags: CrateFlags);
        var loose = Make("loose", 150 * Px, 4 * Px, 7, CrateFlags);

        Pass(hero, crate, loose);

        IEffectAnchor held = crate.Proxy;
        Assert.Equal(KeyOf(hero).SortCoordinate, held.SortCoordinate);
        Assert.Equal(KeyOf(hero).SortCoordinate, FrozenEffectAnchor.Of(crate.Proxy).SortCoordinate);
        Assert.NotEqual(100 * ((crate.Proxy.PosY >> 16) - (crate.Proxy.PosZ >> 16)), held.SortCoordinate);

        IEffectAnchor looseAnchor = loose.Proxy;
        Assert.Equal(100 * ((loose.Proxy.PosY >> 16) - (loose.Proxy.PosZ >> 16)), looseAnchor.SortCoordinate);

        // thrown
        var thrown = Make("thrown", 190 * Px, 20 * Px, 5, BombFlags, renderZ: 20);
        thrown.Proxy.ThrownBy = hero.Proxy;
        Pass(hero, thrown);
        Assert.Equal(KeyOf(thrown).SortCoordinate, ((IEffectAnchor)thrown.Proxy).SortCoordinate);

        // and back to the formula once it is let go
        crate.Proxy.PlatformEntity = null;
        Pass(hero, crate);
        Assert.Equal(100 * ((crate.Proxy.PosY >> 16) - (crate.Proxy.PosZ >> 16)), ((IEffectAnchor)crate.Proxy).SortCoordinate);
    }
}
