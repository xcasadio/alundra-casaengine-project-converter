#nullable enable
using System;
using System.Globalization;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.g1b T2 (docs/plan-e20-heros.md section 1.24.4): the arcs of the lift on real exported maps, the real controller and the real sheet of the hero, BEFORE the activation (T3): until then the stub <c>PlayerTryAction</c> answers 0 and the
/// production set of the liftable types is empty, so the arcs that expect a lift are RED; the guard of the walk release and the arcs that expect the sword are green before and after.
/// <list type="bullet">
/// <item><description>(g1) the real sheet of the hero makes the half step of the walk release (the idle has the acceleration byte 1): the guard that keeps the rig of the lifting honest (<c>docs/plan-e20-annexe/e20-g1b/walk-release-disc/</c>);</description></item>
/// <item><description>(m3) the genre 3: the very heavy stone (sprite 234, flags <c>0x3427E4</c>) of the Debug zone (map 11, record 27 at (996, 808), flat ground) is lifted only by a hero who owns the object <c>0x3B</c> (<c>TryLift</c>, <c>0x8002EDBC</c>).</description></item>
/// </list>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftArcTests : IDisposable
{
    private const uint LiftOnTheGround = 5;
    private const uint SwordOnTheGround = 16;
    private const int PowerGlove = 0x3B;

    public LiftArcTests()
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

    /// <summary>The Debug zone (map 11): the flat ground of height 1 (16 px) around the very heavy stone, west of it; the hero starts at the tile (38, 50), 72 px west of the stone.</summary>
    private static ArcSpec DebugZone(string name) => new(
        name, "Debug zone", "Debug zone-11", Array.Empty<int>(), 38, 50, 1, 4000, RealController: true, Prefabs: true);

    /// <summary>Lets the hero rest facing east: three ticks of RIGHT, then ten ticks of nothing (the force is gone), the idle with the acceleration byte of the real sheet.</summary>
    private static void FaceEastAtRest(ArcRun arc)
    {
        for (var i = 0; i < 5; i++)
        {
            arc.OneFrame();
        }

        arc.HoldDirections(PadState.Right);
        for (var i = 0; i < 3; i++)
        {
            arc.OneFrame();
        }

        arc.ReleaseDirections();
        for (var i = 0; i < 12; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(0, arc.Hero.ForceX);
    }

    // ---- (g1) the release of the walk on the real sheet -------------------------------------------------------------------------------------------------

    [Fact]
    public void G1_TheReleaseOfTheWalkOnTheRealSheet_MakesTheHalfStepOfTheBinary_TheForceIs1_2188AtTheReleaseTick()
    {
        // The binary (docs/plan-e20-annexe/e20-g1b/t0/out/oracle-release-open-vs-crate.txt, line "in the open"): RIGHT held for the ticks t1 to t9 from rest, released at t10: x moves by 1.21875, 2.4375 x 8 (20.71875 at t9), then
        // by the half step 1.21875 at the release tick (21.9375 at t10), the force 2.4375 -> 1.21875 -> 0. The idle of the real sheet has the acceleration byte 1 (94 animations of 94 equal to the binary); a bench that gives it 0 stops the hero dead.
        using var arc = new ArcRun(DebugZone("g1b-g1"));
        FaceEastAtRest(arc);
        var x0 = arc.Hero.PosX;

        arc.HoldDirections(PadState.Right);
        for (var tick = 1; tick <= 9; tick++)
        {
            arc.OneFrame();
        }

        Assert.Equal(20.71875, (arc.Hero.PosX - x0) / 65536.0);
        Assert.Equal(2.4375, arc.Hero.ForceX / 65536.0);

        arc.ReleaseDirections();
        arc.OneFrame(); // t10, the release tick.
        Assert.Equal(21.9375, (arc.Hero.PosX - x0) / 65536.0);
        Assert.Equal(1.21875, arc.Hero.ForceX / 65536.0, 5);

        arc.OneFrame(); // t11: the force is gone, no more step.
        Assert.Equal(21.9375, (arc.Hero.PosX - x0) / 65536.0);
        Assert.Equal(0, arc.Hero.ForceX);
    }

    // ---- (m3) the genre 3: the very heavy stone, with and without the object 0x3B ---------------------------------------------------------------------------

    private static void PushAgainstTheStoneThenPressSquare(ArcRun arc, EntityScriptProxy stone)
    {
        FaceEastAtRest(arc);
        arc.HoldDirections(PadState.Right);
        var guard = 0;
        while (!ReferenceEquals(arc.Hero.XCollisionEntity, stone))
        {
            arc.OneFrame();
            Assert.True(++guard < 60, "the hero reaches the stone");
        }

        // The tick after the contact: the Square (RIGHT still held) is the front the binary reads with the contact of the previous tick.
        arc.OneFrame();
        arc.Press(PadState.Square);
    }

    [Fact]
    public void M3_TheVeryHeavyStone_GenreThree_IsLiftedByAHeroWhoOwnsTheObject3B()
    {
        using var arc = new ArcRun(DebugZone("g1b-m3-with"));
        arc.Give(PowerGlove);
        Assert.NotEqual(0, PlayerManager.GetNumberOfItem(ArcRun.State, PowerGlove));
        var stone = arc.EntityByRecord(27)!;
        Assert.Equal(234, stone.SpriteType);
        Assert.Equal(3, (int)EntityFlags.GetPickupKind(stone.Flags));

        PushAgainstTheStoneThenPressSquare(arc, stone);

        Assert.Same(stone.LogicContextEntity, arc.Hero.CarriedEntity);
        Assert.Equal(LiftOnTheGround, arc.Hero.TargetAnimationId);
    }

    [Fact]
    public void M3_TheVeryHeavyStone_GenreThree_IsNotLiftedWithoutTheObject3B_TheSwordIsStruckInstead()
    {
        using var arc = new ArcRun(DebugZone("g1b-m3-without"));
        var stone = arc.EntityByRecord(27)!;

        PushAgainstTheStoneThenPressSquare(arc, stone);

        Assert.Null(arc.Hero.CarriedEntity);
        Assert.Equal(SwordOnTheGround, arc.Hero.TargetAnimationId); // the binary (g9 D, t13): anim_t 1 -> 16, the sword of the kit, the stone untouched.
        Assert.Equal(0, stone.PosZ - (16 << 16));
    }
}
