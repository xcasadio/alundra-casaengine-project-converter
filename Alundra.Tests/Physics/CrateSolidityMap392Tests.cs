#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Physics;

/// <summary>
/// E20.g3 (docs/plan-e20-heros.md section 1.28, T1, tests (s1), (s2), (s3), (s6)): the soulevables are solid for the hero, as in the original. The real exported map 392 (Ship Klark, night, inner: the 12 crates of the
/// barricade, the real prefabs, the real hero). Every expected value is the one the binary gives (<c>docs/plan-e20-annexe/e20-g3/bin/out/</c>, <c>critic/out/</c>) and the DLL probes of T0 reproduced
/// (<c>docs/plan-e20-annexe/e20-g3/exec/probes/dll-after-line-change.txt</c>); written before the line of <see cref="Alundra.Physics.MovementObstacleProbe"/> that stopped skipping the soulevables.
/// B1 of the map polls the pad every 16 frames from the frame 220 (a direction held with the hero on the ground rolls him): every walk is a window of at most 14 ticks that starts the frame after a poll.
/// The hero is placed on the root, a fractional position being written on the raw fields; the crate records are the numbers of the export (rec5 at y 296 alone, rec6 and rec10 at 312, rec7 and rec11 at 328).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CrateSolidityMap392Tests : IDisposable
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int FirstWindow = 221;

    public CrateSolidityMap392Tests()
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

    private static ArcSpec Spec(string name) => new(
        name, "The Klark", "Ship Klark (night, inner, break)-392", new[] { 1641 }, 29, 13, 4, 6000, RealController: true, Prefabs: true);

    private static ArcRun Open(string name)
    {
        var arc = new ArcRun(Spec(name));
        arc.RunUntil(() => arc.Has(B, 104, 0x11), "B1 executes 0x11 @104 (the hero takes the hand)");
        Assert.Equal(205, arc.Frame);
        while (arc.Frame < FirstWindow)
        {
            arc.OneFrame();
        }

        return arc;
    }

    /// <summary>Lets the hero rest (nothing held) until the frame after the next poll of B1.</summary>
    private static void ToNextWindow(ArcRun arc)
    {
        arc.ReleaseDirections();
        arc.OneFrame();
        while ((arc.Frame - FirstWindow) % 16 != 0)
        {
            arc.OneFrame();
        }
    }

    private readonly record struct Row(int Tick, double X, double Y, int Contact);

    /// <summary>The hero placed at (x, y, z) idle in a window of B1, then <paramref name="ticks"/> ticks with <paramref name="dirs"/> held: one row per tick (the contact is the record of <c>XCollisionEntity</c>, -1 for none).</summary>
    private static List<Row> Walk(ArcRun arc, double x, double y, int z, uint dirs, int ticks, int settleFrames = 0)
    {
        ToNextWindow(arc);
        var start = arc.Frame;
        arc.PlaceHero((int)Math.Floor(x), (int)Math.Floor(y), z);
        arc.Hero.PosX = (int)Math.Round(x * 65536.0);
        arc.Hero.PosY = (int)Math.Round(y * 65536.0);
        arc.Hero.PushLogicalPositionToRoot();
        if (x != Math.Floor(x) || y != Math.Floor(y))
        {
            // the teleport of the root truncates to whole pixels: the fractional position is written on the root itself.
            var root = new Vector3((float)x, (float)y, z);
            arc.HeroEntity.RootComponent!.LocalTransform.Position = root;
            arc.Hero.Controller!.Teleport(root);
        }

        for (var i = 0; i < settleFrames; i++)
        {
            arc.OneFrame();
        }

        var rows = new List<Row>();
        arc.HoldDirections(dirs);
        for (var t = 1; t <= ticks; t++)
        {
            Assert.True(arc.Frame - start < 15, "the window reaches the poll of B1");
            arc.OneFrame();
            var hero = arc.Hero;
            rows.Add(new Row(t, hero.PosX / 65536.0, hero.PosY / 65536.0, hero.XCollisionEntity?.EntityRefId ?? -1));
        }

        arc.ReleaseDirections();
        return rows;
    }

    // ---- (s1) P-G3-1: the hero stops at the faces of rec5, y = 296 --------------------------------------------------------

    [Fact]
    public void S1_AlundraWalkingWestIsStoppedFlushAtTheEastFaceOfRec5_X657_AndTheCrateIsTheContact()
    {
        using var arc = Open("g3-s1-east");

        var rows = Walk(arc, 684, 296, 64, PadState.Left, 14);

        Assert.All(rows, r => Assert.True(r.X >= 657.0, $"tick {r.Tick}: x={r.X} went through the crate"));
        Assert.Equal(657.0, rows[^1].X, 4);
        Assert.Equal(296.0, rows[^1].Y, 4);
        Assert.Equal(5, rows[^1].Contact);
        // The twelfth step is the one shortened to the face: the contact is set on that very tick (the binary's flush tick, P-G3-1).
        Assert.Equal(657.0, rows[11].X, 4);
        Assert.Equal(5, rows[11].Contact);
        Assert.All(rows.Take(11), r => Assert.Equal(-1, r.Contact));
    }

    [Fact]
    public void S1_AlundraWalkingEastIsStoppedFlushAtTheWestFaceOfRec5_X615()
    {
        using var arc = Open("g3-s1-west");

        var rows = Walk(arc, 600, 296, 64, PadState.Right, 12);

        Assert.All(rows, r => Assert.True(r.X <= 615.0, $"tick {r.Tick}: x={r.X} went through the crate"));
        Assert.Equal(615.0, rows[6].X, 4);
        Assert.Equal(5, rows[6].Contact);
        Assert.Equal(615.0, rows[^1].X, 4);
        Assert.Equal(5, rows[^1].Contact);
    }

    // ---- (s2) P-G3-2: the contact of a pile, from the ground and from the top of rec5 -----------------------------------

    [Theory]
    [InlineData(296, 5)] // rec5 alone (16 px high).
    [InlineData(311, 6)] // rec6 under rec10.
    [InlineData(312, 6)]
    [InlineData(327, 7)] // rec7 under rec11.
    [InlineData(328, 7)]
    public void S2_FromTheGround_TheContactOfAPileIsTheLowerCrate_TheFirstOfTheList(int y, int lowerCrate)
    {
        using var arc = Open("g3-s2-ground-" + y);

        var rows = Walk(arc, 684, y, 64, PadState.Left, 14);

        Assert.Equal(657.0, rows[^1].X, 4);
        Assert.Equal(lowerCrate, rows[^1].Contact);
        Assert.Equal(lowerCrate, rows.First(r => r.Contact >= 0).Contact);
        Assert.Equal(12, rows.First(r => r.Contact >= 0).Tick);
    }

    [Fact]
    public void S2_FromTheTopOfRec5_TheNeighbouringCrateOfTheTopLimitsTheStep_AndIsTheContact()
    {
        using var arc = Open("g3-s2-top");

        // The hero stands on rec5 (z 80) and goes down: y is limited to 297.0 (R7 of a3_routes) and the contact is rec10, the crate of the pile beside him. The binary sets it one tick after the step is shortened, the DLL on the tick of the shortening (as on the ground): the identity and the limit are pinned, not the tick (ADR of E20.g3).
        var rows = Walk(arc, 636, 296, 80, PadState.Down, 10, settleFrames: 3);

        Assert.All(rows, r => Assert.True(r.Y <= 297.0 + 1e-9, $"tick {r.Tick}: y={r.Y} went into the pile"));
        Assert.Equal(297.0, rows[^1].Y, 4);
        Assert.Equal(636.0, rows[^1].X, 4);
        Assert.Equal(10, rows[^1].Contact);
    }

    /// <summary>
    /// E20.g1b T2 (D-E20-77, O-G3-3): PINNED, NOT FOLLOWED. Seen from the top of rec5, the DLL sets the contact of the neighbouring crate of the pile (rec10) on the tick that shortens the step; the binary sets it ONE TICK LATER
    /// (the route R7 replayed on the machine of the oracle, <c>docs/plan-e20-annexe/e20-g1b/t0/out/og33-square.txt</c>: t37 y = 296.812 no contact, t38 y = 297.000 no contact, t39 contact rec10; here the tick 1 is t37). The consequence once
    /// <c>PlayerTryAction</c> reads that contact (the activation, T3): a Square pressed on the tick that follows the arrival lifts rec10 in the DLL where the binary swings the sword (a Square at t37, t38 or t39 gives the sword; from t40 the lift).
    /// The value below is the DLL's, with the binary's value beside it: a slice of the engine will correct the contact (the probe of the movement) and then this test is rewritten to the binary's rows, [1] = -1 and [2] = 10.
    /// </summary>
    [Fact]
    public void OG33_FromTheTopOfRec5_TheDllSetsTheContactOfRec10OneTickBeforeTheBinary_PinnedNotFollowed()
    {
        using var arc = Open("g1b-og33");

        var rows = Walk(arc, 636, 296, 80, PadState.Down, 10, settleFrames: 3);

        Assert.Equal(296.8125, rows[0].Y, 4);
        Assert.Equal(-1, rows[0].Contact);   // the binary: no contact at t37 either.
        Assert.Equal(297.0, rows[1].Y, 4);
        Assert.Equal(10, rows[1].Contact);   // THE DLL's VALUE. The binary (t38): no contact (-1).
        Assert.Equal(297.0, rows[2].Y, 4);
        Assert.Equal(10, rows[2].Contact);   // the binary (t39): rec10, from here on both agree.
    }

    // ---- (s3) P-G3-3: the flush case ---------------------------------------------------------------------------------

    [Fact]
    public void S3_TheContactIsSetOnTheTickThatShortensTheStep_ExceptWhenTheFullStepIsAcceptedAndEndsExactlyFlush()
    {
        using var arc = Open("g3-s3");

        // One step period of the walk (2.4375 px = 78 thirty-seconds): x0 = 684 + k / 32 for k = 0..78. 78 starts shorten the step to the face at the flush tick and have the contact on it;
        // the start k = 33 (x0 = 685.03125) is accepted in full by the twelfth step, which ends exactly on the face (657.000): no contact on that tick (the binary: 0 sur 79 contre 78 sur 79,
        // critic/out/w4_contact.txt), the next tick pushes into the face and sets it.
        var contactOnTheFlushTick = 0;
        var contactOneTickLater = new List<int>();
        for (var k = 0; k <= 78; k++)
        {
            var rows = Walk(arc, 684 + k / 32.0, 296, 64, PadState.Left, 14);
            var flush = rows.First(r => r.X <= 657.0 + 1e-9);
            Assert.Equal(657.0, rows[^1].X, 4);
            var firstContact = rows.First(r => r.Contact >= 0);
            Assert.Equal(5, firstContact.Contact);
            if (firstContact.Tick == flush.Tick)
            {
                contactOnTheFlushTick++;
            }
            else
            {
                Assert.Equal(flush.Tick + 1, firstContact.Tick);
                Assert.Equal(-1, flush.Contact);
                contactOneTickLater.Add(k);
            }
        }

        Assert.Equal(78, contactOnTheFlushTick);
        Assert.Equal(new[] { 33 }, contactOneTickLater);
    }

    // ---- (s6) P-G3-4: leaving an overlap ----------------------------------------------------------------------------

    [Theory]
    // The binary's table (bin/out/a1_overlap.txt), the DLL equal to the third decimal: the hero d px inside the west face of rec5 (x = 657 - d), Right held. There is no exit rule in either:
    // from less than 1.3 px inside the first step is taken and no contact is set; from 1.3 px inside the first step is refused (contact rec5), the second is taken from 2 px inside at the latest
    // and from 5 px inside the hero stays stuck for good.
    [InlineData(0.5, 657.719, -1)]
    [InlineData(1.0, 657.219, -1)]
    [InlineData(1.2, 657.019, -1)]
    [InlineData(1.3, 655.700, 5)]
    [InlineData(2.0, 655.000, 5)]
    [InlineData(5.0, 652.000, 5)]
    [InlineData(10.0, 647.000, 5)]
    public void S6_AnOverlapOfTheHeroWithACrate_FirstTickLikeTheBinary_NoExitRule(double depth, double xAtTick1, int contactAtTick1)
    {
        using var arc = Open("g3-s6-" + depth.ToString(CultureInfo.InvariantCulture));

        var rows = Walk(arc, 657 - depth, 296, 64, PadState.Right, 12);

        Assert.Equal(xAtTick1, rows[0].X, 3);
        Assert.Equal(contactAtTick1, rows[0].Contact);
    }

    [Theory]
    // Second tick and last tick (the binary: 658.137 / 657.438 and 682.512 / 681.812, the float resolution of the root makes the third decimal differ by one): the hero that was refused once leaves at the second tick
    // of the walk, from 2 px inside; from 5 px inside he never leaves.
    [InlineData(1.3, 658.138, 682.513)]
    [InlineData(2.0, 657.438, 681.812)]
    [InlineData(5.0, 652.000, 652.000)]
    [InlineData(10.0, 647.000, 647.000)]
    public void S6_AnOverlapOfTheHeroWithACrate_TheTicksThatFollow_TheHeroLeavesOrStaysStuck(double depth, double xAtTick2, double xAtTick12)
    {
        using var arc = Open("g3-s6-then-" + depth.ToString(CultureInfo.InvariantCulture));

        var rows = Walk(arc, 657 - depth, 296, 64, PadState.Right, 12);

        Assert.Equal(xAtTick2, rows[1].X, 2);
        Assert.Equal(xAtTick12, rows[^1].X, 2);
    }
}
