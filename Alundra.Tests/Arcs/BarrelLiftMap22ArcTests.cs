#nullable enable
using System;
using System.Globalization;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.SaveGames;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.g2c1 T3 (docs/plan-e20-heros.md section 1.24.5, phase C, (a1)): the activation. Alundra pressing the Square against a barrel of the map 22 (Lars' Crypt, record 21 at (888, 464), type 290, native C = C4) lifts it: before the phase C the
/// barrel is outside the wired set of the lift (<see cref="PlayerManager.ProductionWiredLiftableTypes"/>) and the Square swings the sword (the animation 16); after it, the lift gesture (the animation 5) and the barrel in the hands. The real exported map, the real
/// controller, the real programs of the map; the start is the arrival of the preset <c>c4-m22</c> moved next to the barrel (the preset itself is the author's start of the recipe, far from it).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BarrelLiftMap22ArcTests : IDisposable
{
    private const uint LiftOnTheGround = 5;
    private const uint SwordOnTheGround = 16;
    private const int BarrelType = 290;
    private const int BarrelRecord = 21;

    public BarrelLiftMap22ArcTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static double X(EntityScriptProxy e) => e.PosX / 65536.0;

    private static double Y(EntityScriptProxy e) => e.PosY / 65536.0;

    private static double Z(EntityScriptProxy e) => e.PosZ / 65536.0;

    /// <summary>The start next to the barrel of the record 21 (tile (37, 27), level 1: 16 px, the floor of the barrel), 38 px north of it.</summary>
    private static ArcSpec Spec(string name) => new(
        name, "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 3000, RealController: true, Prefabs: true,
        Arrival: new ArcArrival((37 * 24 + 12) << 16, (27 * 16 + 8) << 16, 1 << 20, GameState.ResetAnimationId, 0));

    [Fact]
    public void A1_TheSquareAgainstABarrelOfTheMap22_LiftsIt()
    {
        using var arc = new ArcRun(Spec("g2c1-a1"));
        var hero = arc.Hero;
        var barrel = arc.EntityByRecord(BarrelRecord)!;
        Assert.Equal(BarrelType, barrel.SpriteType);

        // Walk south into the barrel (the contact is the one of the tick before the Square), then the Square.
        arc.HoldDirections(PadState.Down);
        var played = 0;
        while (!ReferenceEquals(hero.XCollisionEntity, barrel))
        {
            Assert.True(played++ < 60, $"the hero reaches the barrel (hero at {X(hero):F2}, {Y(hero):F2}, {Z(hero):F2})");
            arc.OneFrame();
        }

        arc.ReleaseDirections();
        arc.Press(PadState.Square);

        Assert.NotNull(hero.CarriedEntity);
        Assert.Same(barrel.LogicContextEntity, hero.CarriedEntity);
        Assert.Equal(LiftOnTheGround, hero.TargetAnimationId);
        Assert.NotEqual(SwordOnTheGround, hero.TargetAnimationId);
        Assert.Same(hero.LogicContextEntity, barrel.PlatformEntity);
    }
}
