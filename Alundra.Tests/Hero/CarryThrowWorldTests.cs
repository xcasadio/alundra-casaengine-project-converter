#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CasaEngine.Engine.Physics;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using static Alundra.Tests.Hero.CarryScenes;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g2a T2 (docs/plan-e20-heros.md section 1.24.2, "Valeurs attendues"; (v1) to (v9)): the hero throws the object of his hands on <see cref="CarryRig"/> - the real hero controller, the exported animation sets and chains of the carrying and throwing states,
/// the crate of the original (flags <c>0x3323E4</c>, the real prefab's Kinetic body, the animation sets 0, 1, 3 and 4 of the original with the impulse 1024) and the program C23 that turns the code <c>+0x24</c> into a projectile - at 1, 2 and 3 ticks per frame.
/// T is the tick of the front of the Square, K the number of ticks since T. The expected values are the oracle's (<c>docs/plan-e20-annexe/e20-g/oracle/out/g4-*, g4b-*, g6-*</c>): the hero at (109, 100), the object over his head. Written BEFORE the branch of the Square
/// in the carrying group (phase B): red on the named stub. The oracle's hero is the real one, whose body is a Kinetic ghost: the bench's solid body would refuse the first step of an object born on his very position, so the scenes ask for the real prefab's body.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryThrowWorldTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryThrowWorldTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    public static TheoryData<int, bool> TicksPerFrameAndDirection
    {
        get
        {
            var data = new TheoryData<int, bool>();
            foreach (var ticks in new[] { 1, 2, 3 })
            {
                data.Add(ticks, false);
                data.Add(ticks, true);
            }

            return data;
        }
    }

    public static TheoryData<int, int, bool> Gazes
    {
        get
        {
            var data = new TheoryData<int, int, bool>();
            foreach (var ticks in new[] { 1, 2, 3 })
            {
                for (var gaze = 0; gaze < 4; gaze++)
                {
                    data.Add(ticks, gaze, false);
                    data.Add(ticks, gaze, true);
                }
            }

            return data;
        }
    }

    public static TheoryData<int, int> TicksPerFrameAndStep
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (var ticks in new[] { 1, 2, 3 })
            {
                foreach (var cells in new[] { 1, 2, 3 })
                {
                    data.Add(ticks, cells);
                }
            }

            return data;
        }
    }

    public static TheoryData<int, int> TicksPerFrameAndTarget
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (var ticks in new[] { 1, 2, 3 })
            {
                foreach (var x in new[] { 160, 172, 178, 184 })
                {
                    data.Add(ticks, x);
                }
            }

            return data;
        }
    }

    private const int Px = CarryRig.Px;

    private const uint Square = PadState.Square;

    /// <summary>The raw value (16.16) of a whole or half pixel count.</summary>
    private static int Raw(double pixels) => (int)Math.Round(pixels * Px);

    /// <summary>The height of the object over the ground T to T + 22 (the oracle, hero on the ground): the impulse of 4 px at T, then 0.5 px less every tick, the landing the first tick the step would go under 0.</summary>
    private static readonly double[] GroundArc =
    {
        36, 39.5, 42.5, 45, 47, 48.5, 49.5, 50, 50, 49.5, 48.5, 47, 45, 42.5, 39.5, 36, 32, 27.5, 22.5, 17, 11, 4.5, 0,
    };

    /// <summary>The height K ticks after T of an object thrown from <paramref name="z0"/> px (the hero's height plus 32): the same law as <see cref="GroundArc"/>, 0 from the first tick under it.</summary>
    private static double ArcZ(double z0, int k)
    {
        var z = z0;
        var force = 4.0;
        for (var j = 0; j <= k; j++)
        {
            z += force;
            force -= 0.5;
            if (z < 0)
            {
                return 0;
            }
        }

        return z;
    }

    /// <summary>What a tick leaves: the hero and the object, in raw units.</summary>
    private sealed record Row(
        int K, uint HeroState, bool HeroCarries, int HeroBox, int HeroX, int HeroY, int HeroZ,
        int X, int Y, int Z, uint Anim, uint Validated, int Code, uint Flags, uint Direction, bool Linked, int Ground, EntityStatus Status);

    private sealed class Flight
    {
        public Flight(CarryRig rig, int throwAt, List<Row> rows)
        {
            Rig = rig;
            ThrowAt = throwAt;
            Rows = rows;
        }

        public CarryRig Rig { get; }

        public int ThrowAt { get; }

        public List<Row> Rows { get; }

        public EntityScriptProxy Hero => Rig.Hero;

        public EntityScriptProxy Crate => Rig.Crate!;

        public Row At(int k) => Rows.Single(row => row.K == k);
    }

    private static Row Capture(CarryRig rig, int k)
    {
        var hero = rig.Hero;
        var crate = rig.Crate!;
        return new Row(
            k, hero.TargetAnimationId, hero.CarriedEntity != null, hero.BoxDirection, hero.PosX, hero.PosY, hero.PosZ,
            crate.PosX, crate.PosY, crate.PosZ, crate.TargetAnimationId, crate.CurrentAnimationId, crate.Flags2, crate.Flags, crate.TargetDirection, crate.PlatformEntity != null, crate.IsOnGround, crate.Status);
    }

    /// <summary>
    /// The montage of every scene: the hero at (109, 100) on a flat field (or <paramref name="field"/>), the crate of the original over his head, the animation sets of its flight (0: at rest; 1, 3 and 4: the speeds 256, 128 and 384 of the original and the impulse
    /// 1024) and the gravity of the map in the words of the object.
    /// </summary>
    private static CarryRig Montage(int ticksPerFrame, Func<int, uint> hold, ICollisionField? field, bool probe, float heroZ = 0f)
    {
        var rig = CarryRig.Build(ticksPerFrame, hold, field: field, probeFactory: probe ? host => new MovementObstacleProbe(host) : null, heroZ: heroZ, kineticHero: true);
        var crate = rig.AddCrate(109, 100, (int)heroZ + 32, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, prefabBody: true);
        crate.MapGravityRaw = 128;
        crate.MapZViscosityRaw = 4096;
        crate.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 256, IsZForceApplied = 1024 },
            [3] = new AnimSetEntry { Anim = 3, Speed = 128, IsZForceApplied = 1024 },
            [4] = new AnimSetEntry { Anim = 4, Speed = 384, IsZForceApplied = 1024 },
        };
        return rig;
    }

    /// <summary>
    /// Plays a throw: the hero stands with the object over his head (the settled stand 0x0C), <paramref name="pad"/> gives the pad word of the tick K (negative before T, 0 at T), the flight is recorded from <c>K = -lead</c> to <c>K = after - 1</c>. The program C23
    /// of the object runs from the end of the stand on, as the registry of production would run it.
    /// </summary>
    private static Flight Fly(
        int ticksPerFrame, Func<int, uint> pad, int lead = 0, int after = 40, ICollisionField? field = null, bool probe = false, Action<CarryRig>? configure = null, Action<CarryRig, int>? afterTick = null)
    {
        var throwAt = int.MaxValue;
        var rig = Montage(ticksPerFrame, t => t >= throwAt - lead ? pad(t - throwAt) : 0u, field, probe);
        configure?.Invoke(rig);
        StandWithTheObject(rig);
        rig.RunLiftableProgram = true;
        throwAt = rig.Tick + 1 + lead;
        var rows = new List<Row> { Capture(rig, -lead - 1) }; // the state the previous tick left.
        rig.Run(lead + after, tick =>
        {
            afterTick?.Invoke(rig, tick - throwAt);
            rows.Add(Capture(rig, tick - throwAt));
        });
        return new Flight(rig, throwAt, rows);
    }

    private static Func<int, uint> SquareAtT(uint also = 0) => k => k == 0 ? Square | also : 0u;

    // -----------------------------------------------------------------------------------------
    // (v1) the flight on the ground, no direction held: g4 "still".
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V1_TheThrowOnTheGround_WithoutADirection_FliesAt1Point5PixelsATick_OnTheArcOfTheOriginal(int ticksPerFrame)
    {
        var flight = Fly(ticksPerFrame, SquareAtT());

        // The tick before: the hero stands with the object over his head - the neighbour of the throw.
        var before = flight.At(-1);
        Assert.Equal((12u, true, true), (before.HeroState, before.HeroCarries, before.Linked));
        Assert.Equal((Raw(109), Raw(100), Raw(32)), (before.X, before.Y, before.Z));

        // T: the hero is in the throwing stand 9 and carries nothing; the object has left his hands, took its flight (the animation 3 asked, the program C23 has run in this very tick: +0x24 cleared, flags 0x332374).
        var first = flight.At(0);
        Assert.Equal((9u, false, false), (first.HeroState, first.HeroCarries, first.Linked));
        Assert.Equal(3u, first.Anim);
        Assert.Equal((0, 0x332374u), (first.Code, first.Flags));

        var failures = new List<string>();
        for (var k = 0; k <= 22; k++)
        {
            var row = flight.At(k);
            var expectedX = Raw(110.5) + k * Raw(1.5);
            var expectedZ = Raw(GroundArc[k]);
            if (row.X != expectedX || row.Z != expectedZ || row.Y != Raw(100))
            {
                failures.Add($"T+{k}: x {row.X / (double)Px}, y {row.Y / (double)Px}, z {row.Z / (double)Px}; expected {expectedX / (double)Px}, 100, {GroundArc[k]}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
        Assert.Equal(Raw(143.5), flight.At(22).X);
        Assert.Equal((0, 1), (flight.At(22).Z, flight.At(22).Ground));
        Assert.Equal(0, flight.At(21).Ground);

        // The hero: the stand 9 from T to T + 18, the idle 0 at T + 19.
        for (var k = 0; k <= 18; k++)
        {
            Assert.True(flight.At(k).HeroState == 9u, $"T+{k}: the hero's state {flight.At(k).HeroState}");
        }

        Assert.Equal(0u, flight.At(19).HeroState);
        Assert.All(flight.Rows.Where(row => row.K >= 0), row => Assert.Equal((Raw(109), Raw(100), 0), (row.HeroX, row.HeroY, row.HeroZ)));
    }

    // -----------------------------------------------------------------------------------------
    // (v2) in the walk: a direction held at the tick of the Square, g4 "moving-east".
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V2_TheThrowWithADirectionHeld_FliesAt3PixelsATick_TheAnimation1_NextToTheThrowWithout(int ticksPerFrame)
    {
        var held = Fly(ticksPerFrame, SquareAtT(PadState.Right));
        var free = Fly(ticksPerFrame, SquareAtT());

        Assert.Equal((1u, 3u), (held.At(0).Anim, free.At(0).Anim));
        var failures = new List<string>();
        for (var k = 0; k <= 22; k++)
        {
            var row = held.At(k);
            var expectedX = Raw(112) + k * Raw(3);
            if (row.X != expectedX || row.Z != Raw(GroundArc[k]) || free.At(k).X != Raw(110.5) + k * Raw(1.5))
            {
                failures.Add($"T+{k}: held x {row.X / (double)Px}, z {row.Z / (double)Px}; expected {expectedX / (double)Px}, {GroundArc[k]}; free x {free.At(k).X / (double)Px}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
        Assert.Equal(Raw(178), held.At(22).X);
        Assert.Equal((9u, false, false), (held.At(0).HeroState, held.At(0).HeroCarries, held.At(0).Linked));
        Assert.Equal(0x332374u, held.At(0).Flags);
    }

    // -----------------------------------------------------------------------------------------
    // (v3) in the air: the hero at the apex of his jump (27.5 px), g4b "air-still" and "air-moving-east".
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrameAndDirection))]
    public void V3_TheThrowInTheAir_StartsFrom59Point5_FliesAt3Or4Point5PixelsATick_AndLandsAtTPlus26(int ticksPerFrame, bool directionHeld)
    {
        // The Cross at T - 10 (the jump on the spot 0x2E): the hero is at his apex 27.5 px when the Square is read at T.
        var flight = Fly(ticksPerFrame, k => k == -10 ? Cross : k == 0 ? Square | (directionHeld ? PadState.Right : 0u) : 0u, lead: 10, after: 40);
        var speed = directionHeld ? 4.5 : 3.0;

        Assert.Equal(Raw(27.5), flight.At(-1).HeroZ);
        Assert.Equal(12u, flight.At(-11).HeroState);
        Assert.Equal((Raw(109), Raw(100), Raw(59.5)), (flight.At(-1).X, flight.At(-1).Y, flight.At(-1).Z));

        var first = flight.At(0);
        Assert.Equal((10u, false, false), (first.HeroState, first.HeroCarries, first.Linked));
        Assert.Equal(directionHeld ? 4u : 1u, first.Anim);
        Assert.Equal((0, 0x332374u), (first.Code, first.Flags));

        // The oracle's samples (g4b, every second tick; T = 71): the heights 67.0 at T + 1 ... 1.0 at T + 25 for the still throw.
        var samples = new (int K, double Z)[] { (1, 67.0), (3, 72.5), (5, 76.0), (7, 77.5), (9, 77.0), (11, 74.5), (13, 70.0), (15, 63.5), (17, 55.0), (19, 44.5), (21, 32.0), (23, 17.5), (25, 1.0) };
        Assert.All(samples, sample => Assert.Equal(Raw(sample.Z), flight.At(sample.K).Z));

        var failures = new List<string>();
        for (var k = 0; k <= 26; k++)
        {
            var row = flight.At(k);
            var expectedX = Raw(109 + speed * (k + 1));
            var expectedZ = Raw(ArcZ(59.5, k));
            if (row.X != expectedX || row.Z != expectedZ)
            {
                failures.Add($"T+{k}: x {row.X / (double)Px}, z {row.Z / (double)Px}; expected {expectedX / (double)Px}, {expectedZ / (double)Px}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));

        // The landing at T + 26, not before.
        Assert.Equal((0, 1), (flight.At(26).Z, flight.At(26).Ground));
        Assert.Equal(0, flight.At(25).Ground);

        // The hero: the throwing stand of the air 10 from T, the idle 0 at T + 11 (he lands first; his jump lasts 20 ticks).
        for (var k = 0; k <= 10; k++)
        {
            Assert.True(flight.At(k).HeroState == 10u, $"T+{k}: the hero's state {flight.At(k).HeroState}");
        }

        Assert.Equal(0u, flight.At(11).HeroState);
    }

    // -----------------------------------------------------------------------------------------
    // (v4) the gaze: the direction word of the object is the hero's direction of the PREVIOUS tick; est 24, nord 16, ouest 8, sud 0.
    // -----------------------------------------------------------------------------------------

    private static readonly uint[] GazePads = { PadState.Down, PadState.Up, PadState.Left, PadState.Right };

    private static readonly uint[] GazeWords = { 0u, 0x10u, 8u, 0x18u };

    /// <summary>The step of the object per tick for each gaze index (south, north, west, east) with no direction held: the sheet 3 (speed 128), then the sheet 1 (256) with one held. South and north go at two thirds of the pace of east and west.</summary>
    private static (double X, double Y) Pace(int gaze, bool held)
    {
        var factor = held ? 2.0 : 1.0;
        return gaze switch
        {
            0 => (0, 1.0 * factor),
            1 => (0, -1.0 * factor),
            2 => (-1.5 * factor, 0),
            _ => (1.5 * factor, 0),
        };
    }

    [Theory]
    [MemberData(nameof(Gazes))]
    public void V4_TheObjectFliesTowardsTheGazeOfTheHero_TheFourDirections_AtTheirOwnPace(int ticksPerFrame, int gaze, bool directionHeld)
    {
        // The hero turns by walking sixteen ticks towards the gaze (a whole number of pixels: 39 east and west, 26 north and south), lets go, and stands eight ticks (the stand 0x0C): his direction of the sprite is the gaze.
        var flight = Fly(
            ticksPerFrame,
            k => k >= -24 && k <= -9 ? GazePads[gaze] : k == 0 ? Square | (directionHeld ? GazePads[gaze] : 0u) : 0u,
            lead: 24,
            afterTick: (rig, k) =>
            {
                // The walk leaves the hero on a fraction of a pixel (the object he throws takes the whole pixel of his position: see the README of the slice, finding F1); the scene sets him on a whole one, as the oracle's.
                if (k == -2)
                {
                    rig.Hero.PosX &= ~0xFFFF;
                    rig.Hero.PosY &= ~0xFFFF;
                    rig.Hero.PushLogicalPositionToRoot();
                }
            });

        var before = flight.At(-1);
        Assert.Equal((12u, gaze), (before.HeroState, before.HeroBox));
        Assert.Equal((0, 0), (before.HeroX % Px, before.HeroY % Px));
        var hero = (X: before.HeroX / (double)Px, Y: before.HeroY / (double)Px);

        var first = flight.At(0);
        Assert.Equal((GazeWords[gaze], directionHeld ? 1u : 3u), (first.Direction, first.Anim));
        var pace = Pace(gaze, directionHeld);
        var failures = new List<string>();
        var last = gaze == 2 && directionHeld ? 17 : 21; // west at 3 px a tick from x = 70 meets the left edge of the map (x = 10, the clamp of E20.e0k3) after 20 ticks.
        for (var k = 0; k <= last; k++)
        {
            var row = flight.At(k);
            var expectedX = Raw(hero.X + pace.X * (k + 1));
            var expectedY = Raw(hero.Y + pace.Y * (k + 1));
            if (row.X != expectedX || row.Y != expectedY || row.Z != Raw(GroundArc[k]))
            {
                failures.Add($"T+{k}: ({row.X / (double)Px}, {row.Y / (double)Px}, {row.Z / (double)Px}); expected ({expectedX / (double)Px}, {expectedY / (double)Px}, {GroundArc[k]})");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V4_TurningAndThrowingOnTheSameTick_ThrowsInTheOldGaze(int ticksPerFrame)
    {
        foreach (var turn in new[] { PadState.Up, PadState.Left, PadState.Down })
        {
            var flight = Fly(ticksPerFrame, SquareAtT(turn));

            // The hero faced east (24); the pad says another direction at the very tick of the Square: the object goes east, a direction being held (3 px a tick).
            Assert.Equal(3, flight.At(-1).HeroBox);
            var first = flight.At(0);
            Assert.Equal((0x18u, 1u), (first.Direction, first.Anim));
            Assert.Equal((Raw(112), Raw(100)), (first.X, first.Y));
            Assert.Equal((Raw(115), Raw(100)), (flight.At(1).X, flight.At(1).Y));
        }
    }

    // -----------------------------------------------------------------------------------------
    // (v5) the guards of the ground: 0x2000 forbids the throw, 0x4000 forbids the jump and not the throw.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V5_TheGroundPropertyOf0x2000_ForbidsTheThrow_TheObjectStaysInTheHands_NextToTheGroundWithoutIt(int ticksPerFrame)
    {
        var forbidden = Fly(ticksPerFrame, SquareAtT(), after: 12, field: FlatCells.Create(groundProperty: (_, _) => 0x20));
        var free = Fly(ticksPerFrame, SquareAtT(), after: 12);

        // The positive case: without the property the Square throws at T.
        Assert.Equal((9u, false, false), (free.At(0).HeroState, free.At(0).HeroCarries, free.At(0).Linked));

        // With it, nothing happens and nothing happens next tick either: the state stays, the object stays over his head.
        Assert.All(forbidden.Rows.Where(row => row.K >= -1), row =>
        {
            Assert.Equal((12u, true, true), (row.HeroState, row.HeroCarries, row.Linked));
            Assert.Equal((Raw(109), Raw(100), Raw(32)), (row.X, row.Y, row.Z));
        });
        Assert.Same(forbidden.Hero.LogicContextEntity, forbidden.Crate.PlatformEntity);
        Assert.Same(forbidden.Crate.LogicContextEntity, forbidden.Hero.CarriedEntity);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V5_TheGroundPropertyOf0x4000_ForbidsTheJumpAndNotTheThrow(int ticksPerFrame)
    {
        var field = FlatCells.Create(groundProperty: (_, _) => 0x40);

        // The throw happens, as on the free ground.
        var thrown = Fly(ticksPerFrame, SquareAtT(), field: field, after: 24);
        Assert.Equal((9u, false, false), (thrown.At(0).HeroState, thrown.At(0).HeroCarries, thrown.At(0).Linked));
        Assert.Equal(Raw(36), thrown.At(0).Z);
        Assert.True(thrown.At(22).X > Raw(130) && thrown.At(22).Z == 0, $"T+22: x {thrown.At(22).X / (double)Px}, z {thrown.At(22).Z / (double)Px}"); // the pace itself is (v1)'s and (v7)'s.

        // The jump does not: the Cross leaves the hero on the ground with the object in his hands, next to the free ground where the Cross jumps.
        var stuck = Fly(ticksPerFrame, k => k == 0 ? Cross : 0u, field: field, after: 6);
        var jumps = Fly(ticksPerFrame, k => k == 0 ? Cross : 0u, after: 6);
        Assert.All(stuck.Rows.Where(row => row.K >= 0), row => Assert.Equal((12u, true, 0), (row.HeroState, row.HeroCarries, row.HeroZ)));
        Assert.Contains(jumps.Rows.Where(row => row.K >= 0), row => row.HeroZ > 0);
    }

    // -----------------------------------------------------------------------------------------
    // (v6) the lift gestures: no throw during the gesture 0x05 (ground) or 0x0B (air); the throw is possible in 0x0C.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V6_TheSquareDuringTheLiftGestureOnTheGround_ThrowsNothing_AndThrowsOnceTheHeroStands(int ticksPerFrame)
    {
        var pressAt = int.MaxValue;
        var rig = CarryRig.Build(ticksPerFrame, t => t == pressAt ? Square : 0u, kineticHero: true);
        var crate = rig.AddCrate(130, 100, 0, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, prefabBody: true);
        crate.MapGravityRaw = 128;
        crate.MapZViscosityRaw = 4096;
        crate.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 256, IsZForceApplied = 1024 },
            [3] = new AnimSetEntry { Anim = 3, Speed = 128, IsZForceApplied = 1024 },
            [4] = new AnimSetEntry { Anim = 4, Speed = 384, IsZForceApplied = 1024 },
        };
        rig.Run(3);
        var lift = rig.LiftNow();
        rig.RunLiftableProgram = true;
        var hero = rig.Hero;
        var failures = new List<string>();

        // Squares at L + 10 and L + 20, in the gesture (the state 5 lasts 36 ticks): nothing is thrown, the gesture goes on, the object is still carried.
        pressAt = lift + 10;
        rig.Run(40, tick =>
        {
            var k = tick - lift;
            if (k == 20)
            {
                pressAt = tick + 1; // the second press, one tick later in the loop below.
            }

            var expected = k <= 35 ? 5u : 12u;
            if (hero.TargetAnimationId != expected || hero.CarriedEntity == null || crate.PlatformEntity == null || crate.Flags2 != 0)
            {
                failures.Add($"L+{k}: state {hero.TargetAnimationId} (expected {expected}), carries {hero.CarriedEntity != null}, linked {crate.PlatformEntity != null}, code {crate.Flags2}");
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
        Assert.Equal(12u, hero.TargetAnimationId);

        // The stand 0x0C: the Square throws.
        pressAt = rig.Tick + 1;
        rig.Run(1);
        Assert.Equal((9u, false), (hero.TargetAnimationId, hero.CarriedEntity != null));
        Assert.Null(crate.PlatformEntity);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void V6_TheSquareDuringTheLiftGestureInTheAir_ThrowsNothing_AndThrowsOnceTheHeroStands(int ticksPerFrame)
    {
        var jumpAt = int.MaxValue;
        var pressAt = int.MaxValue;
        var rig = CarryRig.Build(ticksPerFrame, t => t == jumpAt ? Cross : t == pressAt ? Square : 0u, kineticHero: true);
        var crate = rig.AddCrate(130, 100, 0, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, prefabBody: true);
        crate.MapGravityRaw = 128;
        crate.MapZViscosityRaw = 4096;
        crate.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [1] = new AnimSetEntry { Anim = 1, Speed = 256, IsZForceApplied = 1024 },
            [3] = new AnimSetEntry { Anim = 3, Speed = 128, IsZForceApplied = 1024 },
            [4] = new AnimSetEntry { Anim = 4, Speed = 384, IsZForceApplied = 1024 },
        };
        rig.Run(3);
        var hero = rig.Hero;
        jumpAt = rig.Tick + 1;

        // The hero jumps on the spot (the state 0x2E) and lifts the object in the air at J + 3 (where the binary's TryLift is called): the gesture of the air, 0x0B, 28 ticks, then 0x30.
        var lift = jumpAt + 3;
        rig.AfterMovePlayer = tick =>
        {
            if (tick == lift)
            {
                PlayerManager.PutInHands(hero, crate);
            }
        };
        rig.Run(4);
        rig.RunLiftableProgram = true;
        Assert.Equal(CarryRules.LiftAirState, hero.TargetAnimationId);
        Assert.True(hero.PosZ > 0);

        // The Square in the gesture, in the air: nothing is thrown (the hero is still in the air: the jump lasts 20 ticks).
        pressAt = rig.Tick + 1;
        rig.Run(1);
        Assert.True(hero.PosZ > 0);
        Assert.Equal((CarryRules.LiftAirState, true), (hero.TargetAnimationId, hero.CarriedEntity != null));
        Assert.Equal(0, crate.Flags2);

        // Landed: the stand 0x0C, and the Square throws.
        rig.RunUntil(() => hero.TargetAnimationId == 12u, 30);
        Assert.Equal(12u, hero.TargetAnimationId);
        Assert.NotNull(hero.CarriedEntity);
        pressAt = rig.Tick + 1;
        rig.Run(1);
        Assert.Equal((9u, false), (hero.TargetAnimationId, hero.CarriedEntity != null));
    }

    // -----------------------------------------------------------------------------------------
    // (v7) the tick of the throw: X + 1.5 from T (the rule of the speed key).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrameAndDirection))]
    public void V7_TheObjectMovesFromTheTickOfTheThrow_TheSpeedKeyIsTheAnimationAsked_NotTheOneValidatedAtTheFrameEnd(int ticksPerFrame, bool directionHeld)
    {
        var flight = Fly(ticksPerFrame, SquareAtT(directionHeld ? PadState.Right : 0u), after: 4);

        // At T the animation validated by the frame is still the old one (0); the object asked for 3 (or 1) and already took the step of that sheet: 109 + 1.5 (or + 3.0).
        var first = flight.At(0);
        Assert.Equal(directionHeld ? 1u : 3u, first.Anim);
        Assert.Equal(directionHeld ? Raw(112) : Raw(110.5), first.X);
        Assert.Equal(0u, first.Validated);
        Assert.Equal(first.X + (directionHeld ? Raw(3) : Raw(1.5)), flight.At(1).X);
    }

    // -----------------------------------------------------------------------------------------
    // (v8) the wall and the steps of 16, 32 and 48 px: the object stops at the wall (X = 133), settles on the step, and stays - nothing destroys it (E2 is g2b's).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrameAndDirection))]
    public void V8_TheObjectThrownAgainstAWall_StopsAtX133_KeepsItsArc_AndStaysWhereItLands(int ticksPerFrame, bool directionHeld)
    {
        var field = FlatCells.Create(cell: (cx, _) => (cx == 6 ? 0x40 : 0, 0));
        var flight = Fly(ticksPerFrame, SquareAtT(directionHeld ? PadState.Right : 0u), field: field, after: 50);

        // The object never goes into the wall (x = 133 is the contact of a body 21 wide with the cell at x = 144), reaches it (T+7 held, T+15 not, a tick or three later if its first steps were lost), and from the contact on does not move on X, whatever its arc does.
        var reached = -1;
        for (var k = 0; k < 50; k++)
        {
            Assert.True(flight.At(k).X <= Raw(133) + 700, $"T+{k}: x {flight.At(k).X / (double)Px} in the wall");
            Assert.Equal(Raw(ArcZ(32, k)), flight.At(k).Z);
            if (reached < 0 && Math.Abs(flight.At(k).X - Raw(133)) < 700)
            {
                reached = k;
            }
        }

        Assert.InRange(reached, directionHeld ? 7 : 15, 30);
        for (var k = reached; k < 50; k++)
        {
            Assert.True(Math.Abs(flight.At(k).X - Raw(133)) < 700, $"T+{k}: x {flight.At(k).X / (double)Px}");
        }

        // Nothing destroys it: it stays at the wall for the rest of the run.
        Assert.Equal(0, flight.Rig.Host.SearchDestroyed.Count);
        Assert.NotEqual(EntityStatus.FlagToDestroy, flight.Crate.Status);
        Assert.Equal((0, 1), (flight.At(49).Z, flight.At(49).Ground));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrameAndStep))]
    public void V8_TheObjectThrownOntoAStep_SettlesOnItsTop_AndStaysThere(int ticksPerFrame, int cells)
    {
        var field = FlatCells.Create(cell: (cx, _) => (0, cx >= 6 ? cells : 0));
        var flight = Fly(ticksPerFrame, SquareAtT(PadState.Right), field: field, after: 60);

        // On the top of the step (16, 32 or 48 px) from some tick on, and it never leaves it: the height is the top's for the last twenty ticks (without E2, g2b's, nothing stops the object's glide on the top).
        var last = flight.At(59);
        Assert.Equal((Raw(16 * cells), 1), (last.Z, last.Ground));
        for (var k = 40; k < 60; k++)
        {
            Assert.Equal((Raw(16 * cells), 1), (flight.At(k).Z, flight.At(k).Ground));
        }

        // It came over the edge of the cell 6 (x = 144) - the front of its body, not its middle, meets the step.
        Assert.True(last.X > Raw(144));
        Assert.Equal(0, flight.Rig.Host.SearchDestroyed.Count);
        Assert.NotEqual(EntityStatus.FlagToDestroy, flight.Crate.Status);
    }

    // -----------------------------------------------------------------------------------------
    // (v9) the object thrown goes through another crate (g6): it is not a subject of the support and no longer collidable.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrameAndTarget))]
    public void V9_TheObjectThrownGoesThroughAnotherCrate_WhichKeepsItsPlace_AndItsFlightIsTheFlightWithoutIt(int ticksPerFrame, int targetX)
    {
        var free = Fly(ticksPerFrame, SquareAtT(PadState.Right), after: 30, probe: true);
        EntityScriptProxy? target = null;
        var flight = Fly(
            ticksPerFrame,
            SquareAtT(PadState.Right),
            after: 30,
            probe: true,
            configure: rig => target = rig.AddCrate(targetX, 100, 0, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, name: "target", prefabBody: true));

        Assert.NotNull(target);
        for (var k = 0; k <= 22; k++)
        {
            var row = flight.At(k);
            Assert.True(row.X == free.At(k).X && row.Y == free.At(k).Y && row.Z == free.At(k).Z, $"T+{k}: x {row.X / (double)Px}, z {row.Z / (double)Px} against {free.At(k).X / (double)Px}, {free.At(k).Z / (double)Px} without the target");
            Assert.True(row.Z == Raw(GroundArc[k]), $"T+{k}: z {row.Z / (double)Px}");
        }

        Assert.Equal(free.At(22).X, flight.At(22).X);
        Assert.Equal((0, 1), (flight.At(22).Z, flight.At(22).Ground)); // the object lands at the place of the target, through it.
        Assert.Equal((Raw(targetX), Raw(100), 0, EntityStatus.Normal), (target!.PosX, target.PosY, target.PosZ, target.Status));
        Assert.Null(flight.Crate.RidingEntity);
    }
}
