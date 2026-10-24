#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.g4a T1 (docs/plan-e20-heros.md section 1.29.1): the barricade of the ship Klark (map 392) crossed by the jump, under B1 (the hand given to the hero at <c>0x11 @104</c>, the roll
/// whenever a direction is held at a poll of <c>0x2F @107</c>), with the 12 records of the map, at 1, 2 and 3 logic ticks per image. The pilot never writes the game after the start of the arc: it
/// reads the state before each image and holds the pad (docs/plan-e20-annexe/e20-g3/g4a-route/pilot-rules.md). Every expected value is the one measured at T0 (README.md of that folder: the cadence of
/// B1, the route of the binary, the slides law, the window of the jump), written by hand before the code.
/// <list type="bullet">
/// <item><description>a1: the calage against the north wall of the door (the hero ends at <c>PosY = 295 + ((y0 - 295) mod 0.5)</c>, by second steps of half a pixel along the wall);</description></item>
/// <item><description>a2: the whole route, calage, jump, landing on the low crate, the crate in the way, the mouth of the corridor, the portal 0, with no roll;</description></item>
/// <item><description>a3: an entry at y &gt; 297 refuses the jump: the hero stays flush east of the column.</description></item>
/// </list>
/// The arcs touch the crates, so they are not in <c>ArcsWithoutEntityContact</c>; their slides are pinned in <c>ArcsThatSlide</c> (one name per cadence).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ShipBarricadeArcTests
{
    private const int B = ScriptHelper.ProgramBMap;

    private static ArcSpec Spec(string name) => new(
        name, "The Klark", "Ship Klark (night, inner, break)-392", new[] { 1641 }, 29, 13, 4, 100_000, RealController: true, Prefabs: true);

    /// <summary>The window of the jump measured at T0 (f): the intersection of the three cadences and of y = 295, 296, 297, inside the binary's [657.0 ; 694.4].</summary>
    private const double WindowLow = 657.0, WindowHigh = 687.5;

    private readonly record struct Change(int Frame, string To, double X, double Y, double Z);

    /// <summary>The pilot of the arcs: an arc of the real map 392 stepped in images of K ticks, the pad written for a whole image from the state read before it.</summary>
    private sealed class Pilot : IDisposable
    {
        public readonly ArcRun Arc;
        public readonly int K;
        private readonly float _elapsed;
        private int _anchor = -1;
        public int Tick = -1;
        public int Rolls;
        private readonly List<int> _actualPolls = new();
        public Action<int>? AfterTick;

        public Pilot(int k, string name)
        {
            K = k;
            _elapsed = k / 50f;
            Arc = new ArcRun(Spec(name));
            Arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "pad")
                {
                    Tick++;
                }
                else if (pass == "inventory")
                {
                    AfterTick?.Invoke(Tick);
                }
            };
            Arc.OnInstruction = t =>
            {
                if (t.Slot != B)
                {
                    return;
                }

                if (t.Pc == 104 && t.Opcode == 0x11 && _anchor < 0)
                {
                    _anchor = t.Frame;
                }

                if (t.Pc == 107 && t.Opcode == 0x2F)
                {
                    _actualPolls.Add(t.Frame);
                }

                if (t.Pc == 118 && t.Opcode == 0x10)
                {
                    Rolls++;
                    RollTicks.Add(Tick);
                }
            };

            // B1 walks the hero 48 px east and gives him the hand at the tick 204 (0x11 @104): the pilot starts at the image after it.
            while (_anchor < 0)
            {
                Assert.True(Arc.Frame < 1000, "B1 never executed 0x11 @104");
                Arc.OneFrame(_elapsed);
            }

            for (var n = 0; n < 12; n++)
            {
                _recs[n] = Arc.EntityByRecord(n)!;
            }
        }

        /// <summary>The images where B1 polls (0x2F @107): the anchor image + floor(16 m / K), m &gt;= 1 (T0 (a): the wait 0x37 [15] counts ticks).</summary>
        private bool IsPoll(int image)
        {
            if (image <= _anchor)
            {
                return false;
            }

            for (var m = 1; ; m++)
            {
                var poll = _anchor + 16 * m / K;
                if (poll == image)
                {
                    return true;
                }

                if (poll > image)
                {
                    return false;
                }
            }
        }

        public bool PreviousWasPoll => IsPoll(Arc.Frame - 1);

        public bool InPoll => IsPoll(Arc.Frame);

        public double X => Arc.Hero.PosX / 65536.0;

        public double Y => Arc.Hero.PosY / 65536.0;

        public double Z => Arc.Hero.PosZ / 65536.0;

        public bool OnGround => Arc.Hero.IsOnGround != 0;

        /// <summary>The ticks at which B1 rolled (<c>0x10 @118</c>).</summary>
        public readonly List<int> RollTicks = new();

        private readonly EntityScriptProxy[] _recs = new EntityScriptProxy[12];

        /// <summary>The entity of a record of the map (the 12 records of the 392 all spawn).</summary>
        public EntityScriptProxy Rec(int record) => _recs[record];

        /// <summary>The hero's <c>XCollisionEntity</c> (the contact of the last tick of the previous image) is this record.</summary>
        public bool Touches(int record) => ReferenceEquals(Arc.Hero.XCollisionEntity, Rec(record));

        /// <summary>The record of the carried entity, null when the hands are free, -1 for something that is not one of the 12.</summary>
        public int? Carried()
        {
            var c = Arc.Hero.CarriedEntity;
            if (c == null)
            {
                return null;
            }

            for (var r = 0; r < 12; r++)
            {
                if (ReferenceEquals(c, _recs[r].LogicContextEntity))
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>
        /// A start of (b2) to (b6): the hero is posed (fractional position and height written on the proxy, the root and the controller) by the test in the image that follows the anchor image, before
        /// the scenario and never during it (pilot-rules.md rule 1 of g4b: the ticks of a start are counted from the pose).
        /// </summary>
        public void Place(double x, double y, int z)
        {
            Arc.PlaceHero((int)Math.Floor(x), (int)Math.Floor(y), z);
            Arc.Hero.PosX = (int)Math.Round(x * 65536.0);
            Arc.Hero.PosY = (int)Math.Round(y * 65536.0);
            Arc.Hero.PushLogicalPositionToRoot();
            var root = new Microsoft.Xna.Framework.Vector3((float)x, (float)y, z);
            Arc.HeroEntity.RootComponent!.LocalTransform.Position = root;
            Arc.Hero.Controller!.Teleport(root);
        }

        /// <summary>
        /// One image: the directions and the button are let go in a poll image (rule 2 of the pilot), held otherwise. <paramref name="obey"/> false breaks that rule on purpose (the roll of (b6) only).
        /// </summary>
        public void Step(uint directions, uint button = 0, bool obey = true)
        {
            if (obey && IsPoll(Arc.Frame))
            {
                directions = 0;
                button = 0;
            }

            Arc.HoldDirections(directions);
            if (button != 0)
            {
                Arc.Press(button, _elapsed);
            }
            else
            {
                Arc.OneFrame(_elapsed);
            }
        }

        /// <summary>The polls that B1 made are exactly those the pilot's rule predicts (a wrong prediction would mean the pilot held a direction at a poll).</summary>
        public void AssertThePollsWerePredicted()
        {
            var predicted = Enumerable.Range(_anchor + 1, Math.Max(0, Arc.Frame - _anchor)).Where(IsPoll).ToList();
            Assert.NotEmpty(_actualPolls);
            Assert.Equal(predicted.Take(_actualPolls.Count), _actualPolls);
        }

        public void Dispose() => Arc.Dispose();
    }

    private sealed class Route
    {
        public readonly List<Change> Changes = new();
        public string Phase = "down";
        public int JumpTick = -1;
        public int LandingTicksAfterTheCross = -1;
        public double LandingZ;
        public double CalageStartY = double.NaN;
        public double CalageEndY = double.NaN;
        public double FirstSlideY = double.NaN;
        public double HighestZ;
        public double FinalX, FinalY, FinalZ;
        public bool Jumped;
        public bool Ready;
        public bool MissedTheWindow;
    }

    /// <summary>
    /// The pilot of the plan (pilot-rules.md, rules 3 to 5). Mode "a1" stops where the jump would start; "a2" goes to the portal; "a3" presses Cross at the first opportunity wherever <c>PosY</c> is
    /// (an entry below 297.0), then holds Left for 70 ticks. <paramref name="doorY"/> is the <c>PosY</c> from which the hero turns west (the entry y0 of the door is read, not written).
    /// </summary>
    private static Route Fly(Pilot r, string mode, double doorY)
    {
        var route = new Route();
        var lastSlides = 0;
        r.AfterTick = tick =>
        {
            var slides = r.Arc.TotalSlideCount;
            if (slides != lastSlides && double.IsNaN(route.FirstSlideY))
            {
                route.FirstSlideY = r.Y - 0.5; // the sample is read after the first half-pixel step: y0 is half a pixel above.
            }

            lastSlides = slides;
            route.HighestZ = Math.Max(route.HighestZ, r.Z);

            // The landing on the low crate (z 80, on the ground), in ticks counted from the one before the image of the Cross.
            if (route.JumpTick >= 0 && route.LandingTicksAfterTheCross < 0 && Math.Abs(r.Z - 80.0) < 1e-6 && r.OnGround)
            {
                route.LandingTicksAfterTheCross = tick - (route.JumpTick - 1);
                route.LandingZ = r.Z;
            }
        };

        var attemptTick = int.MaxValue / 2;
        for (var images = 0; images < 5000; images++)
        {
            if (WarpDirector.Instance.HasPendingArrival || r.Rolls != 0)
            {
                break;
            }

            var x = r.X;
            var y = r.Y;

            void Go(string to)
            {
                route.Changes.Add(new Change(r.Arc.Frame, to, x, y, r.Z));
                route.Phase = to;
            }

            if (route.Phase == "south" && y >= 343.5)
            {
                Go("west");
            }

            if (route.Phase == "west" && x <= 541.0)
            {
                Go("down2");
            }

            if (route.Phase == "after_jump" && x <= 600.0)
            {
                Go("south");
            }

            if (route.Phase == "down" && y >= doorY)
            {
                Go("left");
            }

            var hold = 0u;
            var cross = false;
            switch (route.Phase)
            {
                case "down" or "south" or "down2":
                    hold = PadState.Down;
                    break;
                case "west" or "after_jump":
                    hold = PadState.Left;
                    break;
                case "after_attempt":
                    hold = PadState.Left;
                    if (r.Tick - attemptTick > 70)
                    {
                        route.FinalX = x;
                        route.FinalY = y;
                        route.FinalZ = r.Z;
                        return route;
                    }

                    break;
                case "left":
                    hold = PadState.Left;
                    var opportunity = !route.Jumped && r.PreviousWasPoll && r.OnGround && x >= WindowLow && x <= WindowHigh;
                    var inBand = y >= 295.0 && y <= 297.0;
                    if (opportunity && mode == "a1" && inBand)
                    {
                        route.Ready = true;
                        route.CalageEndY = y;
                        route.FinalX = x;
                        route.FinalY = y;
                        route.FinalZ = r.Z;
                        return route;
                    }

                    if (opportunity && (mode == "a3" || inBand))
                    {
                        cross = true;
                        route.Jumped = true;
                        Go(mode == "a3" ? "after_attempt" : "after_jump");
                        attemptTick = r.Tick + 1;
                        route.JumpTick = r.Tick + 1;
                    }
                    else if (!route.Jumped && x < WindowLow)
                    {
                        route.MissedTheWindow = true;
                        return route;
                    }

                    break;
            }

            r.Step(hold, cross ? PadState.Cross : 0u);
        }

        route.FinalX = r.X;
        route.FinalY = r.Y;
        route.FinalZ = r.Z;
        return route;
    }

    // ---- the values measured at T0, written by hand (README.md and out/ of docs/plan-e20-annexe/e20-g3/g4a-route/) ----

    /// <summary>a1: (frame of the first image after a poll with the jump opportunity, x, y at that image, slides, y0 at the door). Calage law: y = 295 + ((y0 - 295) mod 0.5), (y - y0) / 0.5 slides.</summary>
    public static TheoryData<int, int, double, double, int, double> CalageRows => new()
    {
        { 1, 317, 657.0, 295.34375, 10, 290.34375 },
        { 2, 159, 670.65625, 295.25, 9, 290.75 },
        { 3, 111, 663.34375, 295.125, 12, 289.125 },
    };

    [Theory]
    [MemberData(nameof(CalageRows))]
    public void A1_AgainstTheNorthWallOfTheDoor_TheHeroIsAlignedOnTheLawOfTheCalage_AndIsReadyToJump(int k, int frame, double x, double y, int slides, double y0)
    {
        using var pilot = new Pilot(k, "A5j-a1-k" + k);

        var route = Fly(pilot, "a1", 288.0);

        Assert.True(route.Ready, "the opportunity of the jump never came");
        Assert.Equal(0, pilot.Rolls);
        Assert.Equal(frame, pilot.Arc.Frame);
        Assert.Equal((x, y, 64.0), (route.FinalX, route.FinalY, route.FinalZ));

        // The law of the calage and the number of second steps along the wall (the guard of Dispose also pins the count by name).
        Assert.Equal(y0, route.FirstSlideY);
        Assert.Equal(295.0 + ((((y0 - 295.0) % 0.5) + 0.5) % 0.5), route.CalageEndY); // the mathematical mod: y0 is below 295.
        Assert.Equal(slides, pilot.Arc.TotalSlideCount);
        Assert.Equal((int)((route.CalageEndY - y0) / 0.5), pilot.Arc.TotalSlideCount);
        Assert.InRange(route.CalageEndY, 295.0, 297.0);
        Assert.False(WarpDirector.Instance.HasPendingArrival);
        pilot.AssertThePollsWerePredicted();
    }

    /// <summary>a2: the phase changes (image frame, x, y, z read before the image) and the end: (final frame, final x, y), the slides.</summary>
    private static readonly Dictionary<int, (Change[] Changes, int FinalFrame, double FinalX, double FinalY, int Slides)> RouteRows = new()
    {
        [1] = (
            [
                new(252, "left", 775.125, 289.125, 64.0),
                new(317, "after_jump", 657.0, 295.34375, 64.0),
                new(346, "south", 598.44140625, 295.34375, 66.0),
                new(379, "west", 597.0, 344.0, 64.0),
                new(405, "down2", 539.4140625, 344.0, 64.0),
            ],
            427, 538.1953125, 376.0, 10),
        [2] = (
            [
                new(129, "left", 775.125, 289.9375, 64.0),
                new(159, "after_jump", 670.65625, 295.25, 64.0),
                new(177, "south", 597.4375, 295.25, 64.0),
                new(195, "west", 597.0, 344.0, 64.0),
                new(209, "down2", 539.71875, 344.0, 64.0),
            ],
            221, 538.5, 376.0, 9),
        [3] = (
            [
                new(87, "left", 775.125, 288.3125, 64.0),
                new(111, "after_jump", 663.34375, 295.125, 64.0),
                new(123, "south", 597.0, 295.125, 64.0),
                new(136, "west", 597.0, 344.0, 64.0),
                new(146, "down2", 539.71875, 344.0, 64.0),
            ],
            155, 538.5, 376.0, 12),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A2_TheJumpRoute_FromTheArrivalToThePortal0_WithoutAnyRoll(int k)
    {
        var expected = RouteRows[k];
        using var pilot = new Pilot(k, "A5j-a2-k" + k);

        var route = Fly(pilot, "a2", 288.0);

        // No roll: the pilot let go of every direction at every poll of B1, and B1 polled when the pilot predicted it.
        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();
        Assert.False(route.MissedTheWindow);

        // The phases, in order, at the frame, the place and the height read before the image that changes the phase: the jump from the face of the crates, the stop by the crate rec9 at x = 597,
        // the south as far as the row of the mouth, the west to the mouth of the corridor, the portal.
        Assert.Equal(expected.Changes, route.Changes);

        // The jump: 18 ticks after the Cross the hero is on the low crate (z 80, on the ground); he never went above it.
        Assert.Equal(18, route.LandingTicksAfterTheCross);
        Assert.Equal(80.0, route.LandingZ);
        Assert.True(route.HighestZ >= 80.0);

        // The departure through the portal 0 (cell (22, 23)), towards the map 391, in the image where the pilot stopped.
        Assert.True(WarpDirector.Instance.HasPendingArrival);
        Assert.Equal(391u, WarpDirector.Instance.ArrivalRecordForTests.MapIndex);
        Assert.Equal(expected.FinalFrame, pilot.Arc.Frame);
        Assert.Equal((expected.FinalX, expected.FinalY, 64.0), (pilot.X, pilot.Y, pilot.Z));
        Assert.Equal(expected.Slides, pilot.Arc.TotalSlideCount);
        Assert.Empty(pilot.Arc.SkippedOrExceeded);
    }

    /// <summary>a3: the entry below y = 297 (YD 304): the frame, x and y of the attempt (Cross + Left at the first opportunity), the final y (the hero flush east of the column, x = 657).</summary>
    public static TheoryData<int, int, double, double> RefusalRows => new()
    {
        { 1, 301, 686.15625, 304.96875 },
        { 2, 159, 672.75, 307.0 },
        { 3, 111, 665.4375, 308.625 },
    };

    [Theory]
    [MemberData(nameof(RefusalRows))]
    public void A3_AnEntryBelowY297_RefusesTheJump_TheHeroStaysFlushEastOfTheColumn(int k, int frame, double attemptX, double y)
    {
        using var pilot = new Pilot(k, "A5j-a3-k" + k);

        var route = Fly(pilot, "a3", 304.0);

        Assert.Equal(0, pilot.Rolls);
        Assert.True(route.Jumped, "the pilot never pressed the Cross");
        Assert.Equal(frame, route.Changes.Single(c => c.To == "after_attempt").Frame);
        Assert.Equal((attemptX, y), (route.Changes.Single(c => c.To == "after_attempt").X, route.Changes.Single(c => c.To == "after_attempt").Y));
        Assert.True(y > 297.0, "the entry is not below 297.0");

        // The jump is refused: the hero never lands on the low crate (z 80), he is on the ground at his height, flush against the crates (x = 657.0) at the y he entered, with no slide.
        Assert.Equal(-1, route.LandingTicksAfterTheCross);
        Assert.Equal((657.0, y, 64.0), (route.FinalX, route.FinalY, route.FinalZ));
        Assert.Equal(0, pilot.Arc.TotalSlideCount);
        Assert.False(WarpDirector.Instance.HasPendingArrival);
        pilot.AssertThePollsWerePredicted();
    }

    // =================================================================================================================================================================================
    // E20.g4b T1 (docs/plan-e20-heros.md section 1.29.2): the barricade crossed by lifting, under B1, the 12 records, at 1, 2 and 3 ticks per image. The pilot is the one of g4a completed by
    // docs/plan-e20-annexe/e20-g3/g4b-route/pilot-rules.md; every expected value is the one measured at T0 (README.md of that folder, dll-probes/*.txt), written by hand before the code.
    // The values read with four decimals at T0 are compared to 6e-5.
    // =================================================================================================================================================================================

    private static void AssertNear(double expected, double actual, string what) => Assert.True(Math.Abs(expected - actual) <= 6e-5, $"{what}: expected {expected}, got {actual}");

    private static void AssertChanges(Change[] expected, List<Change> actual)
    {
        Assert.Equal(expected.Select(c => (c.Frame, c.To)), actual.Select(c => (c.Frame, c.To)));
        for (var i = 0; i < expected.Length; i++)
        {
            var label = $"phase {expected[i].To} (image {expected[i].Frame})";
            AssertNear(expected[i].X, actual[i].X, label + " x");
            AssertNear(expected[i].Y, actual[i].Y, label + " y");
            AssertNear(expected[i].Z, actual[i].Z, label + " z");
        }
    }

    private sealed class LiftRun
    {
        public readonly List<Change> Changes = new();
        public int LiftImage = -1, LiftFirstTick = -1, CarriedTick = -1, StandTick = -1, ThrowImage = -1, FreeTick = -1, DestroyedTick = -1;
    }

    /// <summary>
    /// (b1), the route "lift" (pilot-rules.md rule 3): down to <c>PosY &gt;= 288</c>, left to the contact rec5, Square (rule 2: the poll images let go of the pad), the carry stand, left with the crate to
    /// <c>PosX &lt;= 615</c>, Square (the throw), left to <c>PosX &lt;= 600</c> (rec9 stops the hero at 597), then the phases south, west, down2 of g4a, to the portal 0.
    /// </summary>
    private static LiftRun LiftRoute(Pilot r)
    {
        var run = new LiftRun();
        var phase = "down";
        r.AfterTick = tick =>
        {
            var carried = r.Carried();
            if (run.CarriedTick < 0 && carried == 5)
            {
                run.CarriedTick = tick;
            }

            if (run.StandTick < 0 && carried == 5 && r.Arc.Hero.TargetAnimationId == 12)
            {
                run.StandTick = tick;
            }

            if (run.FreeTick < 0 && run.ThrowImage >= 0 && carried == null)
            {
                run.FreeTick = tick;
            }

            if (run.DestroyedTick < 0 && run.ThrowImage >= 0 && r.Rec(5).Status == EntityStatus.Destroyed)
            {
                run.DestroyedTick = tick;
            }
        };

        for (var images = 0; images < 1500 && !WarpDirector.Instance.HasPendingArrival && r.Rolls == 0; images++)
        {
            var x = r.X;
            var y = r.Y;

            void Go(string to)
            {
                run.Changes.Add(new Change(r.Arc.Frame, to, x, y, r.Z));
                phase = to;
            }

            if (phase == "down" && y >= 288.0)
            {
                Go("left");
            }

            if (phase == "south" && y >= 343.5)
            {
                Go("west");
            }

            if (phase == "west" && x <= 541.0)
            {
                Go("down2");
            }

            if (phase == "after_throw" && x <= 600.0)
            {
                Go("south");
            }

            var hold = 0u;
            var button = 0u;
            switch (phase)
            {
                case "down" or "south" or "down2":
                    hold = PadState.Down;
                    break;
                case "west" or "after_throw":
                    hold = PadState.Left;
                    break;
                case "left":
                    hold = PadState.Left;
                    if (r.Touches(5) && !r.InPoll)
                    {
                        button = PadState.Square;
                        run.LiftImage = r.Arc.Frame;
                        run.LiftFirstTick = r.Tick + 1;
                        Go("gesture");
                    }

                    break;
                case "gesture":
                    hold = PadState.Left;
                    if (r.Carried() == 5 && r.Arc.Hero.TargetAnimationId == 12)
                    {
                        Go("carry");
                        goto case "carry";
                    }

                    break;
                case "carry":
                    hold = PadState.Left;
                    if (x <= 615.0 && !r.InPoll)
                    {
                        button = PadState.Square;
                        run.ThrowImage = r.Arc.Frame;
                        Go("after_throw");
                    }

                    break;
            }

            r.Step(hold, button);
        }

        return run;
    }

    /// <summary>b1 per cadence: the phases (image, x, y, z read before the image), the images of the lift and of the throw, the ticks (carried, carry stand, hands free, rec5 destroyed), the departure image and place.</summary>
    private static readonly Dictionary<int, (Change[] Changes, int LiftImage, int StandTick, int ThrowImage, int FreeTick, int DestroyedTick, int FinalFrame, int FinalTick, double FinalX, int Slides)> LiftRows = new()
    {
        [1] = (
            [
                new(252, "left", 775.125, 289.125, 64.0),
                new(314, "gesture", 657.0, 295.3438, 64.0),
                new(351, "carry", 657.0, 295.3438, 64.0),
                new(369, "after_throw", 614.3438, 295.3438, 64.0),
                new(396, "south", 598.5, 295.3438, 64.0),
                new(428, "west", 597.0, 343.6875, 64.0),
                new(454, "down2", 539.1094, 344.0, 64.0),
            ],
            314, 350, 369, 369, 393, 478, 477, 538.6406, 11),
        [2] = (
            [
                new(129, "left", 775.125, 289.9375, 64.0),
                new(163, "gesture", 657.0, 295.25, 64.0),
                new(183, "carry", 653.3438, 295.25, 64.0),
                new(192, "after_throw", 613.125, 295.25, 64.0),
                new(205, "south", 599.7188, 295.25, 64.0),
                new(223, "west", 598.5, 344.0, 64.0),
                new(237, "down2", 536.3438, 344.0, 64.0),
            ],
            163, 362, 192, 384, 408, 252, 503, 538.125, 13),
        [3] = (
            [
                new(87, "left", 775.125, 288.3125, 64.0),
                new(113, "gesture", 657.0, 295.125, 64.0),
                new(127, "carry", 650.9062, 295.125, 64.0),
                new(133, "after_throw", 613.125, 295.125, 64.0),
                new(142, "south", 597.2812, 295.125, 64.0),
                new(156, "west", 597.0, 344.0, 64.0),
                new(166, "down2", 539.7188, 344.0, 64.0),
            ],
            113, 375, 133, 399, 424, 174, 521, 538.5, 12),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void B1_TheLiftRoute_FromTheArrivalToThePortal0_RecFiveIsCarriedFromTheFirstTickOfTheSquareImage_ThrownAndBroken_WithoutAnyRoll(int k)
    {
        var expected = LiftRows[k];
        using var pilot = new Pilot(k, "A5l-b1-k" + k);

        var run = LiftRoute(pilot);

        // No roll: the pilot let go of every direction at every poll of B1, and B1 polled when the pilot predicted it.
        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();

        // The phases, in order, with the frame and the place read before the image that changes the phase.
        AssertChanges(expected.Changes, run.Changes);

        // The lift: at the image F of the Square, rec5 (the contact of the end of the previous tick) is carried from the very first tick of F, then 36 ticks of gesture to the carry stand.
        Assert.Equal(expected.LiftImage, run.LiftImage);
        Assert.Equal(run.LiftFirstTick, run.CarriedTick);
        Assert.Equal(expected.StandTick, run.StandTick);
        Assert.Equal(36, run.StandTick - run.CarriedTick);

        // The throw: the hands are free in the very tick of the Square; rec5 breaks 24 ticks later (the binary: 24; at 3 ticks per image the tick of the status change read at the end of a tick is the 25th, T0 flagged).
        Assert.Equal(expected.ThrowImage, run.ThrowImage);
        Assert.Equal(expected.FreeTick, run.FreeTick);
        Assert.Equal(expected.DestroyedTick, run.DestroyedTick);
        Assert.Equal(k == 3 ? 25 : 24, run.DestroyedTick - run.FreeTick);

        // The departure through the portal 0 (cell (22, 23)), towards the map 391, in the image where the pilot stopped.
        Assert.True(WarpDirector.Instance.HasPendingArrival);
        Assert.Equal(391u, WarpDirector.Instance.ArrivalRecordForTests.MapIndex);
        Assert.Equal(expected.FinalFrame, pilot.Arc.Frame);
        Assert.Equal(expected.FinalTick, pilot.Tick);
        AssertNear(expected.FinalX, pilot.X, "final x");
        Assert.Equal((376.0, 64.0), (pilot.Y, pilot.Z));
        Assert.Equal(expected.Slides, pilot.Arc.TotalSlideCount);
        Assert.Empty(pilot.Arc.SkippedOrExceeded);
    }

    // ---- the lifts of the starts (b2) to (b5): the pose of the start, one direction held, the Square at the first non-poll image whose start state has the expected contact ----

    private sealed class StartRun
    {
        public int ContactImage = -1, CrossImage = -1, CrossFirstTick = -1, SquareImage = -1, SquareFirstTick = -1, PoseTick, TickAfterTheSquare;
        public int? CarriedRightAfter;
        public double SquareX, SquareY, SquareZ;
        public readonly List<(int Tick, double Z10, double Z6)> Series = new();
    }

    /// <summary>
    /// Poses the hero at (x, y, z) in the image after the anchor, holds <paramref name="direction"/> and presses Square at the first non-poll image whose start state has the contact
    /// <paramref name="record"/> (pilot-rules.md rule 2 and 4), then lets go of the pad until the carry stand. <paramref name="inAir"/> (b4) is the rule 5 instead: the Cross at the first non-poll image
    /// whose first tick since the pose is &gt;= 19, then the Square at the first non-poll image start in the air with the contact <paramref name="record"/>.
    /// </summary>
    private static StartRun Lift(Pilot r, double x, double y, int z, uint direction, int record, bool inAir = false)
    {
        var run = new StartRun();
        r.Place(x, y, z);
        run.PoseTick = r.Tick;
        r.AfterTick = tick => run.Series.Add((tick, r.Rec(10).PosZ / 65536.0, r.Rec(6).PosZ / 65536.0));
        var crossed = false;
        for (var images = 0; images < 400 && run.SquareImage < 0; images++)
        {
            var firstTick = r.Tick + 1 - run.PoseTick;
            if (run.ContactImage < 0 && r.Touches(record))
            {
                run.ContactImage = r.Arc.Frame;
            }

            uint button = 0;
            if (inAir)
            {
                if (!crossed && !r.InPoll && firstTick >= 19)
                {
                    button = PadState.Cross;
                    crossed = true;
                    run.CrossImage = r.Arc.Frame;
                    run.CrossFirstTick = firstTick;
                }
                else if (crossed && !r.InPoll && !r.OnGround && r.Touches(record))
                {
                    button = PadState.Square;
                    Mark(r, run);
                }
            }
            else if (r.Touches(record) && !r.InPoll)
            {
                button = PadState.Square;
                Mark(r, run);
            }

            r.Step(direction, button);
        }

        run.TickAfterTheSquare = r.Tick - run.PoseTick;
        run.CarriedRightAfter = r.Carried();
        for (var guard = 0; r.Arc.Hero.TargetAnimationId != 12 && guard < 100; guard++)
        {
            r.Step(0);
        }

        r.AfterTick = null;
        return run;
    }

    private static void Mark(Pilot r, StartRun run)
    {
        run.SquareImage = r.Arc.Frame;
        run.SquareFirstTick = r.Tick + 1 - run.PoseTick;
        run.SquareX = r.X;
        run.SquareY = r.Y;
        run.SquareZ = r.Z;
    }

    // (b2): L = the tick where rec6 is first carried; the series of rec10 (and of rec6) at the end of every tick, read by the hook of the "inventory" pass.
    private static readonly double[] BinaryFallOfRec10 = [80.0, 79.5, 78.5, 77.0, 75.0, 72.5, 69.5, 66.0, 64.0, 64.0, 64.0];

    [Theory]
    [InlineData(1, 211, 211)]
    [InlineData(2, 106, 212)]
    [InlineData(3, 71, 213)]
    public void B2_FromTheFaceOfRecSix_RecSixIsCarried_AndTheCrateAboveItHasFallenToTheGround(int k, int squareImage, int liftTick)
    {
        using var pilot = new Pilot(k, "A5l-b2-k" + k);

        var run = Lift(pilot, 670.0, 316.0, 64, PadState.Left, 6);

        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();

        // The contact rec6 (y in [311 ; 327[) is there at the first image start after the pose, the Square follows in the same image, 7 ticks after the pose; rec6 is carried at its first tick.
        Assert.Equal(squareImage, run.SquareImage);
        Assert.Equal(run.SquareImage, run.ContactImage);
        Assert.Equal(7, run.SquareFirstTick);
        Assert.Equal(6, run.CarriedRightAfter);
        Assert.Equal(liftTick, run.PoseTick + run.SquareFirstTick);
        Assert.Equal(6, pilot.Carried());

        // The hero is flush on the face of the column and stands carrying; the pile above the lifted crate has fallen to the ground and rests there (the binary and the DLL agree from L + 7).
        Assert.Equal((657.0, 316.0, 64.0), (pilot.X, pilot.Y, pilot.Z));
        Assert.Equal(12u, pilot.Arc.Hero.TargetAnimationId);
        Assert.Equal(64.0, pilot.Rec(10).PosZ / 65536.0);
        Assert.Equal(BinaryFallOfRec10[8], run.Series.First(s => s.Tick == liftTick + 7).Z10);
    }

    /// <summary>
    /// The gap Q-G3-c (A), pinned (docs/plan-e20-annexe/e20-g3/g4b-route/README.md (c)): when rec6 is lifted, the crate above it (rec10) falls on the DLL by the old rule of the entities of the map
    /// (D-E20-37) <b>without the half pixel of the first tick</b>: z = 80 at L (the binary: 79.5), then 78.5, 77, 75, 72.5, 69.5, 66, 64 from L + 1 to L + 7, as the binary. The three cadences give the same series.
    /// A change of that rule (or the day the fall follows the binary) makes this test red on purpose.
    /// </summary>
    [Theory]
    [InlineData(1, 211)]
    [InlineData(2, 212)]
    [InlineData(3, 213)]
    public void QG3c_TheCrateAboveALiftedCrate_FallsWithoutTheHalfPixelOfTheFirstTick_TheGapToTheBinary(int k, int liftTick)
    {
        using var pilot = new Pilot(k, "A5l-qg3c-k" + k);

        var run = Lift(pilot, 670.0, 316.0, 64, PadState.Left, 6);

        double[] dll = [80.0, 80.0, 78.5, 77.0, 75.0, 72.5, 69.5, 66.0, 64.0, 64.0, 64.0];
        var window = run.Series.Where(s => s.Tick >= liftTick - 1 && s.Tick <= liftTick + 9).ToList();
        Assert.Equal(11, window.Count);
        Assert.Equal(dll, window.Select(s => s.Z10).ToArray());

        // The only tick that differs from the binary's series is L (index 1): the DLL has 80, the binary 79.5.
        var different = Enumerable.Range(0, 11).Where(i => dll[i] != BinaryFallOfRec10[i]).ToList();
        Assert.Equal(new[] { 1 }, different);

        // rec6 rises one pixel per tick from L + 1, as the binary.
        Assert.Equal(new[] { 64.0, 64.0, 65.0, 66.0, 67.0, 68.0, 69.0, 70.0, 71.0, 72.0, 73.0 }, window.Select(s => s.Z6).ToArray());
        Assert.Equal(0, pilot.Rolls);
    }

    [Theory]
    [InlineData(1, 207)]
    [InlineData(2, 104)]
    [InlineData(3, 70)]
    public void B3_OnTopOfRecFive_PushingSouthAgainstThePile_RecTenIsCarried_AtY297(int k, int squareImage)
    {
        using var pilot = new Pilot(k, "A5l-b3-k" + k);

        var run = Lift(pilot, 636.0, 296.0, 80, PadState.Down, 10);

        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();

        // Y is limited to 297.0 by the pile, the contact rec10 is there at the first image start, the Square follows in it.
        Assert.Equal(squareImage, run.SquareImage);
        Assert.Equal(run.SquareImage, run.ContactImage);
        Assert.Equal(297.0, run.SquareY);
        Assert.Equal(10, run.CarriedRightAfter);
        Assert.Equal(10, pilot.Carried());
        Assert.Equal(12u, pilot.Arc.Hero.TargetAnimationId);
        Assert.Equal((636.0, 297.0), (pilot.X, pilot.Y));
        AssertNear(80.0, pilot.Z, "hero z");
    }

    /// <summary>b4 per cadence: (Cross image, Square image, Square first tick, z at the Square, the tick after the Square, x at the end of the carry stand).</summary>
    [Theory]
    [InlineData(1, 223, 233, 29, 91.5, 29, 637.4790)]
    [InlineData(2, 112, 120, 35, 84.0, 36, 651.2581)]
    [InlineData(3, 75, 80, 34, 86.5, 36, 648.9614)]
    public void B4_InTheAir_RecTenIsCarried_TheLiftInTheAirOfG1b(int k, int crossImage, int squareImage, int squareFirstTick, double squareZ, int tickAfter, double endX)
    {
        using var pilot = new Pilot(k, "A5l-b4-k" + k);

        var run = Lift(pilot, 720.0, 298.0, 64, PadState.Left, 10, inAir: true);

        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();

        // The Cross at the first non-poll image whose first tick is >= 19 (rule 5: the plan's 12 does not work under the polls at 2 and 3 ticks per image, T0), the Square in the air at the first
        // non-poll image start with the contact rec10 (the gate of the oracle), at z 91.5, 84.0, 86.5 (z >= 80: the sprite of rec10 is touched above the top of the pile).
        Assert.Equal(crossImage, run.CrossImage);
        Assert.Equal(19, run.CrossFirstTick);
        Assert.Equal(squareImage, run.SquareImage);
        Assert.Equal(squareFirstTick, run.SquareFirstTick);
        AssertNear(squareZ, run.SquareZ, "z at the Square");
        Assert.True(run.SquareZ >= 80.0);
        Assert.Equal(tickAfter, run.TickAfterTheSquare);
        Assert.Equal(10, run.CarriedRightAfter);
        Assert.Equal(10, pilot.Carried());
        AssertNear(endX, pilot.X, "x at the end of the carry stand");
        Assert.Equal(298.0, pilot.Y);
        AssertNear(80.0, pilot.Z, "hero z");
    }

    [Theory]
    [InlineData(1, 217, 217)]
    [InlineData(2, 109, 109)]
    [InlineData(3, 73, 75)]
    public void B5_CaseC_StuckAgainstTheNorthWallOfTheDoor_RecFiveIsCarried_AtY295(int k, int contactImage, int squareImage)
    {
        using var pilot = new Pilot(k, "A5l-b5-k" + k);

        var run = Lift(pilot, 657.0, 312.0, 64, PadState.Up, 5);

        Assert.Equal(0, pilot.Rolls);
        pilot.AssertThePollsWerePredicted();

        // The contact rec5 appears at the first blocked tick at the north wall; at 3 ticks per image a poll image falls between the contact and the Square (rule 2: Up let go, then held again).
        Assert.Equal(contactImage, run.ContactImage);
        Assert.Equal(squareImage, run.SquareImage);
        Assert.Equal(295.0, run.SquareY);
        Assert.Equal(5, run.CarriedRightAfter);
        Assert.Equal(5, pilot.Carried());
        Assert.Equal(12u, pilot.Arc.Hero.TargetAnimationId);
        Assert.Equal((657.0, 295.0, 64.0), (pilot.X, pilot.Y, pilot.Z));
    }

    /// <summary>
    /// (b6) the roll while carrying: (b5) up to the carry stand, then, in the first poll image, Left held on purpose (the one place where rule 2 is broken): B1 locks the control and pushes the hero
    /// in a random cardinal direction. The hero is held still 5 ticks (k = 1, 2: the lock of A5r, state 12) then walks; at k = 3 the random draw moves him at once; in the three cases rec5 stays in the hands
    /// at the hero's height + 32 up to roll + 12 (T0: 13 ticks read), the roll count is 1. A lost or lowered object is a stop and a question.
    /// </summary>
    [Theory]
    [InlineData(1, 217, 253, 268)]
    [InlineData(2, 109, 255, 134)]
    [InlineData(3, 75, 263, 89)]
    public void B6_TheRollWhileCarrying_RecFiveStaysInTheHands_TheMeasuredPath(int k, int liftImage, int standTick, int pollImage)
    {
        using var pilot = new Pilot(k, "A5l-b6-k" + k);

        var run = Lift(pilot, 657.0, 312.0, 64, PadState.Up, 5);
        Assert.Equal(liftImage, run.SquareImage);
        Assert.Equal(standTick, pilot.Tick);

        var rows = new List<(int Offset, double X, double Y, double Z, int Anim, int? Carried, double CrateZ)>();
        var rollTick = -1;
        pilot.AfterTick = tick =>
        {
            if (rollTick < 0 && pilot.Rolls > 0)
            {
                rollTick = tick;
            }

            if (rollTick >= 0 && tick <= rollTick + 12)
            {
                rows.Add((tick - rollTick, pilot.X, pilot.Y, pilot.Z, (int)pilot.Arc.Hero.TargetAnimationId, pilot.Carried(), pilot.Rec(5).PosZ / 65536.0));
            }
        };

        for (var guard = 0; !pilot.InPoll && guard < 200; guard++)
        {
            pilot.Step(0);
        }

        Assert.Equal(pollImage, pilot.Arc.Frame);
        pilot.Step(PadState.Left, 0, obey: false);
        for (var guard = 0; (rollTick < 0 || pilot.Tick <= rollTick + 12) && guard < 100; guard++)
        {
            pilot.Step(PadState.Left);
        }

        Assert.Equal(1, pilot.Rolls);
        Assert.Equal(new[] { 268 }, pilot.RollTicks);
        Assert.Equal(268, rollTick);
        Assert.Equal(13, rows.Count);

        // The object stays in the hands, at the hero's height + 32, at every one of the 13 ticks.
        Assert.All(rows, row => Assert.Equal(5, row.Carried));
        Assert.All(rows, row => Assert.Equal(96.0, row.CrateZ));
        Assert.All(rows, row => Assert.Equal(64.0, row.Z));
        Assert.Equal(EntityStatus.Normal, pilot.Rec(5).Status);

        // The measured path of the hero (the value pinned: the lock is 5 ticks at k = 1, 2; the random draw of the roll moves the hero at k = 3).
        if (k == 3)
        {
            double[] xs = [653.6294, 652.6963, 651.7632, 650.8301, 649.8970, 647.4595, 645.0220, 642.5845, 640.1470, 637.7095, 635.2720, 632.8345, 630.3970];
            double[] ys = [296.5012, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0, 297.0];
            for (var i = 0; i < 13; i++)
            {
                AssertNear(xs[i], rows[i].X, $"roll+{i} x");
                AssertNear(ys[i], rows[i].Y, $"roll+{i} y");
                Assert.Equal(7, rows[i].Anim);
            }
        }
        else
        {
            double[] xs = [657.0, 657.0, 657.0, 657.0, 657.0, 654.5625, 652.125, 649.6875, 647.25, 644.8125, 642.375, 639.9375, 637.5];
            for (var i = 0; i < 13; i++)
            {
                AssertNear(xs[i], rows[i].X, $"roll+{i} x");
                Assert.Equal(295.0, rows[i].Y);
                Assert.Equal(i < 5 ? 12 : 7, rows[i].Anim);
            }
        }

        Assert.Equal(5, pilot.Carried());
        AssertNear(k == 3 ? 627.9595 : 635.0625, pilot.X, "end x");
        Assert.Equal(k == 3 ? 297.0 : 295.0, pilot.Y);
    }
}
