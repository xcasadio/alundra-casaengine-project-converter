#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
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
                }
            };

            // B1 walks the hero 48 px east and gives him the hand at the tick 204 (0x11 @104): the pilot starts at the image after it.
            while (_anchor < 0)
            {
                Assert.True(Arc.Frame < 1000, "B1 never executed 0x11 @104");
                Arc.OneFrame(_elapsed);
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

        public double X => Arc.Hero.PosX / 65536.0;

        public double Y => Arc.Hero.PosY / 65536.0;

        public double Z => Arc.Hero.PosZ / 65536.0;

        public bool OnGround => Arc.Hero.IsOnGround != 0;

        /// <summary>One image: the directions and the button are let go in a poll image (rule 2 of the pilot), held otherwise.</summary>
        public void Step(uint directions, bool cross = false)
        {
            if (IsPoll(Arc.Frame))
            {
                directions = 0;
                cross = false;
            }

            Arc.HoldDirections(directions);
            if (cross)
            {
                Arc.Press(PadState.Cross, _elapsed);
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

            r.Step(hold, cross);
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
}
