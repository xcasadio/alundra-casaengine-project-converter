#nullable enable
using System;
using System.Globalization;
using System.Linq;
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
/// E20.g1b T2 (docs/plan-e20-heros.md section 1.24.4, D-E20-71): the arc of the recipe of the lifting on the map 72 (Coastal Cave), from the start of the preset <c>porter-m72</c>, on the real exported map, the real controller, the real sheet of the hero and the
/// real programs of the map, BEFORE the activation (T3): until then the stub <c>PlayerTryAction</c> answers 0 and the production set of the liftable types is empty, so the lift never happens and the arc is RED at its first lift.
/// <para>The recipe, in the order of the author's (docs/plan-e20-heros.md, "Recette de l'auteur"): (1) walk into the jar of the record 37 and press the Square (36 images of lifting, the gesture of the original); (2) walk with the jar; (3) jump onto the barrel of the
/// record 1, a top of 16 px; (4) ride the platform of the record 2, which shuttles 96 px east and west by itself over the lagoon (its program C[3] has no test at all), with the jar in the hands; (5) throw the jar with the Square: it flies and breaks.
/// The peninsula of 80 px that touches the west end of the platform is 32 px over the bank (more than a jump of 27.5 px): the hero is put there by the arc, with the jar over his head, as the only teleport of the recipe (the bench of the arcs puts the carried object with him).</para>
/// <para>The expected values are the binary's, written before the code: the gesture is 36 ticks (t11 to t47 of the oracle <c>g1_lift</c>), the carrying stand 12, the throw state 9, the barrel top at 16 px over the bank (48 + 16 = 64), the platform top at 64 + 8 = 72.</para>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftMap72ArcTests : IDisposable
{
    private const uint LiftOnTheGround = 5;
    private const uint CarryStand = 12;
    private const uint ThrowOnTheGround = 9;
    private const uint SwordOnTheGround = 16;
    private const int LiftGestureTicks = 36;
    private const int BarrelTopZ = 64;
    private const int PlatformTopZ = 72;
    private const int PeninsulaZ = 80;
    private const int JarType = 291;
    private const int BarrelType = 290;

    /// <summary>E20.g2c1 phase C wired the barrel (290) with the types of C4: the witness (m5) derives the barrel of the record 1 on the torch (393, g2c2), a type that stays outside the set.</summary>
    private const int TorchType = 393;

    public LiftMap72ArcTests()
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

    /// <summary>The start of the preset <c>porter-m72</c> (tile (10, 12), level 3), as a portal arrival: the map spawns the entities whose zone contains the tile of the hero at the load, as the game loading the save.</summary>
    private static ArcSpec Spec(string name, string presetName = "porter-m72")
    {
        Assert.True(TestSaves.TryFind(presetName, out var preset), $"the preset {presetName} exists");
        var p = preset!;
        Assert.Equal(72, p.MapId);
        return new ArcSpec(
            name, "Coastal Cave", "Coastal Cave-72", Array.Empty<int>(), 0, 0, 0, 3000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((p.TileX * 24 + 12) << 16, (p.TileY * 16 + 8) << 16, p.TileZ << 20, GameState.ResetAnimationId, 0));
    }

    private static double X(EntityScriptProxy e) => e.PosX / 65536.0;

    private static double Y(EntityScriptProxy e) => e.PosY / 65536.0;

    private static double Z(EntityScriptProxy e) => e.PosZ / 65536.0;

    /// <summary>Frames with <paramref name="directions"/> held until <paramref name="done"/> holds right after a frame (at most <paramref name="limit"/> frames); returns the frames played.</summary>
    private static int Until(ArcRun arc, uint directions, Func<bool> done, int limit, string signal)
    {
        arc.HoldDirections(directions);
        var played = 0;
        while (!done())
        {
            Assert.True(played < limit, $"{signal} (frame {arc.Frame}, hero at {X(arc.Hero):F2}, {Y(arc.Hero):F2}, {Z(arc.Hero):F2}, animation {arc.Hero.TargetAnimationId})");
            arc.OneFrame();
            played++;
        }

        arc.ReleaseDirections();
        return played;
    }

    private static bool Touches(ArcRun arc, EntityScriptProxy entity) => ReferenceEquals(arc.Hero.XCollisionEntity, entity);

    private static bool Carries(ArcRun arc, EntityScriptProxy item) => arc.Hero.CarriedEntity != null && ReferenceEquals(arc.Hero.CarriedEntity, item.LogicContextEntity);

    // ---- (m2) the recipe ---------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void M2_TheRecipe_LiftTheJar_CarryIt_JumpOntoTheBarrel_RideThePlatform_ThrowItAndItBreaks()
    {
        using var arc = new ArcRun(Spec("g1b-m2"));
        var hero = arc.Hero;
        var jar = arc.EntityByRecord(37)!;
        var barrel = arc.EntityByRecord(1)!;
        var platform = arc.EntityByRecord(2)!;
        Assert.Equal(JarType, jar.SpriteType);
        Assert.Equal(BarrelType, barrel.SpriteType);

        // (1) Lift: walk west into the jar of the record 37 (flush at x = 225.0: the jar at 204, 10.5 px of half box on each side), then the Square with the contact of the previous tick.
        Until(arc, PadState.Left, () => Touches(arc, jar), 40, "the hero reaches the jar");
        Assert.Equal(225.0, X(hero), 4);
        arc.Press(PadState.Square);
        Assert.True(Carries(arc, jar), "the Square against the jar lifts it");
        Assert.Equal(LiftOnTheGround, hero.TargetAnimationId);
        Assert.Same(hero.LogicContextEntity, jar.PlatformEntity);
        var lifted = arc.Frame;

        // The gesture of the original lasts 36 ticks (t11 to t47 of g1_lift), then the carrying stand.
        var gesture = Until(arc, 0, () => hero.TargetAnimationId == CarryStand, 60, "the lift gesture ends");
        Assert.Equal(LiftGestureTicks, arc.Frame - lifted);
        Assert.True(gesture > 0);
        Assert.True(Carries(arc, jar));

        // (2) Walk with the jar: west, to the barrel of the record 1 (flush at x = 201.0), the jar over his head.
        Until(arc, PadState.Left, () => Touches(arc, barrel), 40, "the hero carries the jar to the barrel");
        Assert.Equal(201.0, X(hero), 4);
        Assert.True(Carries(arc, jar));
        Assert.Equal(X(hero), X(jar), 3);
        Assert.Equal(Z(hero) + 32.0, Z(jar), 3);

        // (3) Jump onto the top of 16 px: Cross + Left, land on the barrel (48 + 16 = 64), carried object still in the hands.
        arc.HoldDirections(PadState.Left);
        arc.Press(PadState.Cross);
        Until(arc, PadState.Left, () => arc.Hero.IsOnGround != 0 && ReferenceEquals(arc.Hero.RidingEntity, barrel.LogicContextEntity), 60, "the hero lands on the barrel");
        Assert.Equal(BarrelTopZ, Z(hero), 3);
        Assert.True(Carries(arc, jar));

        // (4) The platform: put the hero on the peninsula that touches its west end, jar over his head (the one teleport of the recipe), wait for the platform to be at the west end of its shuttle and walk onto it.
        Teleport(arc, 540, 248, PeninsulaZ);
        Assert.True(Carries(arc, jar));
        Until(arc, 0, () => X(platform) <= 578.0, 400, "the platform comes back to the west end of its shuttle");
        Until(arc, PadState.Right, () => arc.Hero.IsOnGround != 0 && ReferenceEquals(arc.Hero.RidingEntity, platform.LogicContextEntity), 80, "the hero walks onto the platform");
        Assert.Equal(PlatformTopZ, Z(hero), 3);
        Assert.True(Carries(arc, jar));

        // The platform moves by itself and takes the hero and the jar with it, to the pixel.
        for (var settle = 0; settle < 12; settle++)
        {
            arc.OneFrame();
        }

        var platformX0 = X(platform);
        var relative = X(hero) - platformX0;
        for (var i = 0; i < 24; i++)
        {
            arc.OneFrame();
            Assert.Equal(relative, X(hero) - X(platform), 3);
            Assert.Equal(X(hero), X(jar), 3);
            Assert.Equal(Z(hero) + 32.0, Z(jar), 3);
            Assert.True(ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity), $"frame {arc.Frame}: still on the platform");
        }

        Assert.True(Math.Abs(X(platform) - platformX0) > 20.0, "the platform moved by itself");

        // (5) Throw: the Square in the carrying stand. The hero takes the throw state, the jar leaves his hands, flies and breaks.
        arc.Press(PadState.Square);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(ThrowOnTheGround, hero.TargetAnimationId);
        var thrownAt = arc.Frame;
        Until(arc, 0, () => jar.Status == EntityStatus.Destroyed, 100, "the jar breaks");
        Assert.InRange(arc.Frame - thrownAt, 20, 60);
    }

    /// <summary>The hero put at (x, y, z) with the object in his hands over his head (32 px): what the end of the lift gesture leaves, so that the follow of the object does not have to cross the map.</summary>
    private static void Teleport(ArcRun arc, int x, int y, int z)
    {
        arc.PlaceHero(x, y, z);
        arc.Hero.RelativeWarpOffsetX = 0;
        arc.Hero.RelativeWarpOffsetY = 0;
        arc.Hero.RelativeWarpOffsetZ = 32 << 16;
        if (arc.Hero.CarriedEntity?.GameplayProxy is EntityScriptProxy item)
        {
            item.PosX = arc.Hero.PosX;
            item.PosY = arc.Hero.PosY;
            item.PosZ = arc.Hero.PosZ + (32 << 16);
            item.PushLogicalPositionToRoot();
        }
    }

    // ---- (m9) D-E20-79: the platform step from the preset of the peninsula --------------------------------------------------------------------------------------

    [Fact]
    public void M9_FromThePeninsulaPreset_TheHeroWaitsForThePlatformAtItsWestEndAndWalksOntoIt()
    {
        using var arc = new ArcRun(Spec("g1b-m9", "porter-plateforme-m72"));
        var hero = arc.Hero;
        var platform = arc.EntityByRecord(2)!;

        // The start of the preset: the peninsula (80 px high), at the pixel where the arc (m2) teleports, a portal arrival like the game loading the save.
        Assert.Equal(540.0, X(hero), 3);
        Assert.Equal(248.0, Y(hero), 3);
        Assert.Equal(PeninsulaZ, Z(hero), 3);
        Assert.Equal(1, hero.IsOnGround);
        Assert.Null(hero.RidingEntity);

        // Wait for the platform to be at the west end of its shuttle, then walk east onto it: the platform becomes the support of the hero, at its top (72).
        Until(arc, 0, () => X(platform) <= 578.0, 400, "the platform comes back to the west end of its shuttle");
        Until(arc, PadState.Right, () => arc.Hero.IsOnGround != 0 && ReferenceEquals(arc.Hero.RidingEntity, platform.LogicContextEntity), 80, "the hero walks onto the platform");
        Assert.Equal(PlatformTopZ, Z(hero), 3);

        // The platform takes her with it, to the pixel.
        for (var settle = 0; settle < 12; settle++)
        {
            arc.OneFrame();
        }

        var platformX0 = X(platform);
        var relative = X(hero) - platformX0;
        for (var i = 0; i < 24; i++)
        {
            arc.OneFrame();
            Assert.Equal(relative, X(hero) - X(platform), 3);
            Assert.True(ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity), $"frame {arc.Frame}: still on the platform");
        }

        Assert.True(Math.Abs(X(platform) - platformX0) > 20.0, "the platform moved by itself");
    }

    // ---- (m5) an object outside the set gives the sword, the jar of the same scene lifts ----------------------------------------------------------------------------------------

    [Fact]
    public void M5_AnObjectOutsideTheSet_GivesTheSword_TheJarOfTheSameSceneLifts()
    {
        using var arc = new ArcRun(Spec("g1b-m5"));
        arc.Give(1);
        arc.Equip(1);
        var hero = arc.Hero;
        var jar = arc.EntityByRecord(37)!;
        var barrel = arc.EntityByRecord(1)!;

        // Derived on a type that stays outside the set: the barrel of the record 1 (genre 1, a top of 16 px) takes the sprite type of the torch.
        barrel.SpriteType = TorchType;
        Assert.Equal(1, (int)EntityFlags.GetPickupKind(barrel.Flags)); // the object is genre 1 like a jar: only the set tells them apart.
        Assert.NotEqual(JarType, barrel.SpriteType);

        // Down 24 px, west under the jar and the barrel, then north into the barrel from the south.
        Until(arc, PadState.Down, () => Y(hero) >= 224.0, 40, "the hero goes down");
        Until(arc, PadState.Left, () => X(hero) <= 180.0, 80, "the hero goes below the barrel");
        Until(arc, PadState.Up, () => Touches(arc, barrel), 40, "the hero reaches the barrel");

        // The Square against the barrel (genre 1, but outside the set): the sword, nothing in the hands.
        arc.Press(PadState.Square);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(SwordOnTheGround, hero.TargetAnimationId);
        Until(arc, 0, () => hero.TargetAnimationId == 0, 80, "the swing ends");

        // The jar of the same scene: round the barrel by the east, west into the jar, the Square: lifted.
        Until(arc, PadState.Right, () => X(hero) >= 232.0, 40, "the hero goes east");
        Until(arc, PadState.Up, () => Y(hero) <= 200.0, 40, "the hero goes up");
        Until(arc, PadState.Left, () => Touches(arc, jar), 40, "the hero reaches the jar");
        arc.Press(PadState.Square);
        Assert.True(Carries(arc, jar));
        Assert.Equal(LiftOnTheGround, hero.TargetAnimationId);
    }
}
