#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Assets.Animations;
using Xunit;
using Xunit.Sdk;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.h4 H4-1 (docs/plan-e19-opcodes.md §1.2n.4, rules H4-R1, H4-R2, H4-R4): the slide of the hero along a wall (binary <c>ComputeXYPosition</c> <c>0x80037730</c>, the table
/// <c>0x80023734</c>) and the opcodes <c>0x45</c>/<c>0x46</c> (<c>NoObstacleSlide</c>, bit <c>0x2000</c> of the flags). Montage: <see cref="JumpHeroRig"/> with a free pad on
/// <see cref="FlatCells"/> (one blocking cell of walkability <c>0x40</c>), the walkability mask of the hero set to <c>0x40</c> on the LIVE settings of its controller (the montage
/// leaves it at 0), the box of the hero -10, -7, 21 x 15, Gravity, flat ground. Positions in 16.16; one tick per update. The values are those of the plan (checked against a line by
/// line port of the binary's routine on the same grid).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroSlideTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Up = AlundraPadState.Up;
    private const uint Right = AlundraPadState.Right;
    private const uint Left = AlundraPadState.Left;
    private const int North = 16;
    private const int West = 8;
    private const int NorthEast = 20;

    public AlundraHeroSlideTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private readonly record struct Row(int X, int Y, int ForceAdjusted, int Slides);

    /// <summary>The hero at (<paramref name="x"/>, <paramref name="y"/>) on a field where <paramref name="blocked"/> says which cells (x, y) are of walkability 0x40.</summary>
    private static JumpHeroRig Rig(Func<int, int, bool> blocked, float x, float y, uint extraFlags = 0,
        Func<ContactHost, IMovementObstacleProbe?>? probeFactory = null, Action<World, ContactHost>? configure = null)
    {
        var rig = JumpHeroRig.Build(FlatCells.Create(cell: (cx, cy) => (blocked(cx, cy) ? 0x40 : 0, 0)), probeFactory: probeFactory, configure: configure, x: x, y: y, freePad: true);
        rig.Controller.Settings.WalkabilityMask = 0x40; // the LIVE settings of the controller (the setter of the proxy clones).
        rig.Hero.Flags |= extraFlags;
        return rig;
    }

    private static bool OneCell(int cx, int cy) => cx == 10 && cy == 10; // x 240..263, y 160..175.

    /// <summary>
    /// The walk north of T-SL1 from rest: the pad (Up) holds the walk, and the animation 1 is given the speed 312 so that its target force north is -159744 (the table
    /// of offsets gives -512 for the north: 312 x -512) and the ramp is the plan's -79872 then -159744 (acceleration 1).
    /// </summary>
    private static void ImposeNorthForceProfile(JumpHeroRig rig) => rig.SetAnimSet(new AnimSetEntry { Anim = 1, Speed = 312, Acceleration = 1 });

    /// <summary>The steady walk of annex B.1: the animation 1, the direction and the force the test imposes.</summary>
    private static void SteadyWalk(JumpHeroRig rig, uint direction, int forceX, int forceY)
    {
        rig.Hero.TargetAnimationId = 1;
        rig.Hero.TargetDirection = direction;
        rig.Hero.ForceX = forceX;
        rig.Hero.ForceY = forceY;
    }

    private static List<Row> Run(JumpHeroRig rig, int ticks, uint hold)
    {
        var rows = new List<Row>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            rig.Step(hold);
            rows.Add(new Row(rig.Hero.PosX, rig.Hero.PosY, rig.Hero.ForceAdjusted, rig.Hero.SlideCount));
        }

        return rows;
    }

    private static string Show(IReadOnlyList<Row> rows, int tick) => $"tick {tick}: X {rows[tick - 1].X}, Y {rows[tick - 1].Y}, FA {rows[tick - 1].ForceAdjusted}, slides {rows[tick - 1].Slides}";

    // -----------------------------------------------------------------------------------------
    // T-SL1 / T-SL2 - north against one corner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TSL1_NorthAgainstTheCornerOfOneCell_TheHeroSlidesEastAlongItAndGoesOnNorth()
    {
        var rig = Rig(OneCell, 263f, 200f);
        ImposeNorthForceProfile(rig);
        var rows = Run(rig, 25, Up);

        var y = new[] { 13027328, 12867584, 12707840, 12548096, 12388352, 12228608, 12068864, 11993088 };
        for (var tick = 1; tick <= 8; tick++)
        {
            Assert.True(rows[tick - 1].Y == y[tick - 1] && rows[tick - 1].X == 17235968, Show(rows, tick));
        }

        for (var tick = 9; tick <= 23; tick++)
        {
            Assert.True(rows[tick - 1].X == 17235968 + 49152 * (tick - 8) && rows[tick - 1].Y == 11993088, Show(rows, tick));
        }

        Assert.True(rows[23].Y == 11833344, Show(rows, 24));
        Assert.True(rows[24].Y == 11673600, Show(rows, 25));
        for (var tick = 1; tick <= 25; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 0, Show(rows, tick));
        }

        Assert.Equal(15, rows[24].Slides);
        Assert.Equal(15, rig.Hero.SlideCount);
    }

    [Fact]
    public void TSL2_TheSameWithNoObstacleSlide_TheHeroStaysAgainstTheCornerWithForceAdjusted()
    {
        var rig = Rig(OneCell, 263f, 200f, extraFlags: 0x2000);
        ImposeNorthForceProfile(rig);
        var rows = Run(rig, 25, Up);

        for (var tick = 9; tick <= 25; tick++)
        {
            Assert.True(rows[tick - 1].X == 17235968 && rows[tick - 1].Y == 11993088 && rows[tick - 1].ForceAdjusted == 1, Show(rows, tick));
        }

        Assert.Equal(0, rig.Hero.SlideCount);
    }

    // -----------------------------------------------------------------------------------------
    // T-SL3 - west
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TSL3_WestAgainstTheCornerOfOneCell_TheHeroSlidesSouthAlongItThenGoesOnWest()
    {
        var rig = Rig(OneCell, 300f, 177f);
        SteadyWalk(rig, West, -159744, 0);
        var rows = Run(rig, 24, Left);

        Assert.True(rows[10].X == 17956864 && rows[10].Y == 11599872, Show(rows, 11));
        for (var tick = 12; tick <= 23; tick++)
        {
            Assert.True(rows[tick - 1].X == 17956864 && rows[tick - 1].Y == 11599872 + 32768 * (tick - 11), Show(rows, tick));
        }

        Assert.True(rows[23].X == 17797120 && rows[23].Y == 11993088, Show(rows, 24));
        for (var tick = 1; tick <= 24; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 0, Show(rows, tick));
        }
    }

    // -----------------------------------------------------------------------------------------
    // E19.m6 M-31 - the signs of the south and east slides, the entity gate, the agreement rule (values of the 16.16 model of the DLL slide, written
    // before the first run; the binary agrees except the known residue of its halving on positive directions, O-E19-28 b: the tests pin the DLL, stage 1, D-E19-58)
    // -----------------------------------------------------------------------------------------

    private const uint Down = AlundraPadState.Down;

    [Fact]
    public void TSL7_SouthAgainstTheCornerOfOneCell_TheHeroSlidesEastAlongItAndGoesOnSouth()
    {
        var rig = Rig(OneCell, 263f, 135f);
        rig.SetAnimSet(new AnimSetEntry { Anim = 1, Speed = 312, Acceleration = 1 });
        var rows = Run(rig, 25, Down);

        var y = new[] { 8927232, 9086976, 9246720, 9406464, 9566208, 9725952, 9885696, 9961472 };
        for (var tick = 1; tick <= 8; tick++)
        {
            Assert.True(rows[tick - 1].Y == y[tick - 1] && rows[tick - 1].X == 17235968, Show(rows, tick));
        }

        for (var tick = 9; tick <= 23; tick++)
        {
            Assert.True(rows[tick - 1].X == 17235968 + 49152 * (tick - 8) && rows[tick - 1].Y == 9961472, Show(rows, tick));
        }

        Assert.True(rows[23].Y == 10121216, Show(rows, 24));
        Assert.True(rows[24].Y == 10280960, Show(rows, 25));
        for (var tick = 1; tick <= 25; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 0, Show(rows, tick));
        }

        Assert.Equal(15, rows[24].Slides);
        Assert.Equal(15, rig.Hero.SlideCount);
    }

    [Fact]
    public void TSL8_EastAgainstTheCornerOfOneCell_TheHeroSlidesSouthAlongItThenGoesOnEast()
    {
        var rig = Rig(OneCell, 203f, 177f);
        SteadyWalk(rig, 24, 159744, 0);
        var rows = Run(rig, 24, Right);

        Assert.True(rows[10].X == 15007744 && rows[10].Y == 11599872, Show(rows, 11));
        for (var tick = 12; tick <= 23; tick++)
        {
            Assert.True(rows[tick - 1].X == 15007744 && rows[tick - 1].Y == 11599872 + 32768 * (tick - 11), Show(rows, tick));
        }

        Assert.True(rows[23].X == 15167488 && rows[23].Y == 11993088, Show(rows, 24));
        for (var tick = 1; tick <= 24; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 0, Show(rows, tick));
        }

        Assert.Equal(12, rig.Hero.SlideCount);
    }

    [Fact]
    public void TSL9_ACornerBehindAnEntity_NeverSlides_ForceAdjustedFromTheNextTick()
    {
        AlundraEntityScriptProxy? wall = null;
        var rig = Rig(OneCell, 263f, 200f,
            probeFactory: host => new AlundraMovementObstacleProbe(host),
            configure: (world, host) => wall = ContactWorld.AddEntity(world, host, "Wall", 250, 167, 0, -10, -7, 0, 24, 16, 32));
        ImposeNorthForceProfile(rig);
        var rows = Run(rig, 8, Up);

        var y = new[] { 13027328, 12867584, 12707840, 12548096, 12388352, 12228608, 12068864, 11993088 };
        for (var tick = 1; tick <= 8; tick++)
        {
            Assert.True(rows[tick - 1].Y == y[tick - 1] && rows[tick - 1].X == 17235968, Show(rows, tick));
        }

        Assert.NotNull(wall);
        Assert.Same(wall, rig.Hero.XCollisionEntity); // the entity is the obstacle as of the contact tick.
        rows.AddRange(Run(rig, 4, Up));
        for (var tick = 9; tick <= 12; tick++)
        {
            Assert.True(rows[tick - 1].X == 17235968 && rows[tick - 1].Y == 11993088 && rows[tick - 1].ForceAdjusted == 1 && rows[tick - 1].Slides == 0, Show(rows, tick));
        }
    }

    [Fact]
    public void TSL10_ADirectionThatDisagreesWithTheStep_AgainstAWall_RaisesForceAdjustedAtTheContactTick()
    {
        var rig = Rig((cx, _) => cx <= 9, 250f, 200f);
        SteadyWalk(rig, North, -159744, 0);
        var rows = Run(rig, 2, Up);

        Assert.True(rows[0].X == 16384000 && rows[0].Y == 13053952 && rows[0].ForceAdjusted == 1 && rows[0].Slides == 0, Show(rows, 1));
        Assert.True(rows[1].Y == 12947456 && rows[1].ForceAdjusted == 0, Show(rows, 2));
    }

    // -----------------------------------------------------------------------------------------
    // T-SL4 - oblique: no second step, but no ForceAdjusted either while an axis advances
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TSL4_NorthEastAlongAWall_TheEngineAdvancesTheFreeAxis_ForceAdjustedStaysAtZero()
    {
        var rig = Rig((cx, _) => cx >= 12, 270f, 400f);
        SteadyWalk(rig, NorthEast, 112944, -75296);
        var rows = Run(rig, 12, Right | Up);

        Assert.True(rows[4].Y == 25837920, Show(rows, 5));
        for (var tick = 1; tick <= 12; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 0, Show(rows, tick));
            Assert.True(rows[tick - 1].Slides == 0, Show(rows, tick));
        }
    }

    // -----------------------------------------------------------------------------------------
    // T-SL6 - a mobile marked 0x2000 pushing obliquely keeps ForceAdjusted at 1 (guard)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TSL6_AMobileMarkedNoObstacleSlide_PushingObliquelyAgainstAWall_RaisesForceAdjustedFromTheContactTick()
    {
        var rig = Rig((cx, _) => cx >= 12, 270f, 400f, extraFlags: 0x2000);
        SteadyWalk(rig, NorthEast, 112944, -75296);
        var rows = Run(rig, 12, Right | Up);

        Assert.True(rows[4].ForceAdjusted == 0, Show(rows, 5));
        for (var tick = 6; tick <= 12; tick++)
        {
            Assert.True(rows[tick - 1].ForceAdjusted == 1, Show(rows, tick));
        }

        Assert.Equal(0, rig.Hero.SlideCount);
    }

    // -----------------------------------------------------------------------------------------
    // UO-1 / UO-2 - 0x45 and 0x46
    // -----------------------------------------------------------------------------------------

    private static void RunOpcode(AlundraEntityScriptProxy entity, int opcode, int result, out EventProgramState state)
    {
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { opcode, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        state = new EventProgramState { Codes = document.CodesAsBytes(), Result = result };
        runner.RunOneScriptCall(entity, state);
    }

    [Fact]
    public void UO1_0x46SetsNoObstacleSlide_0x45ClearsIt_SizeOne_ResultUntouched()
    {
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, Flags = 0x100 };

        RunOpcode(entity, 0x46, 7, out var set);
        Assert.Equal(0x2100u, entity.Flags);
        Assert.Equal(1, set.CodeIndex);
        Assert.Equal(7, set.Result);

        RunOpcode(entity, 0x45, 7, out var cleared);
        Assert.Equal(0x100u, entity.Flags);
        Assert.Equal(1, cleared.CodeIndex);
        Assert.Equal(7, cleared.Result);
    }

    [Fact]
    public void UO2_TheScriptDrivesTheSlide_0x46StopsItAtTheCorner_0x45GivesItBack()
    {
        var rig = Rig(OneCell, 263f, 200f);
        ImposeNorthForceProfile(rig);

        // The program of a registration writes the bit of the LOGIC entity: here the hero.
        RunOpcode(rig.Hero, 0x46, 0, out _);
        var rows = Run(rig, 12, Up);
        for (var tick = 9; tick <= 12; tick++)
        {
            Assert.True(rows[tick - 1].X == 17235968 && rows[tick - 1].ForceAdjusted == 1, Show(rows, tick));
        }

        RunOpcode(rig.Hero, 0x45, 0, out _);
        var after = Run(rig, 3, Up);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(after[i].X == 17235968 + 49152 * (i + 1) && after[i].ForceAdjusted == 0, $"after 0x45, tick {i + 1}: X {after[i].X}, FA {after[i].ForceAdjusted}");
        }
    }

    // -----------------------------------------------------------------------------------------
    // The guard ArcsWithoutSlide of the arcs
    // -----------------------------------------------------------------------------------------

    private static ArcSpec GuardedSpec(string name) => new(name, "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
        Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));

    [Fact]
    public void ArcsWithoutSlide_AnArcOfTheRealControllerThatSlid_FailsAtItsDispose_ThePinnedArcsPinTheirCount()
    {
        var arc = new ArcRun(GuardedSpec("A5"));
        arc.OneFrame();
        arc.Hero.SlideCount = 1;

        var failure = Assert.Throws<XunitException>(() => arc.Dispose());
        Assert.Contains("ArcsWithoutSlide", failure.Message);

        var exempt = new ArcRun(GuardedSpec("A10J"));
        exempt.OneFrame();
        exempt.Hero.SlideCount = 1;
        exempt.Dispose(); // A10J pins exactly one slide.

        var twice = new ArcRun(GuardedSpec("A10J"));
        twice.OneFrame();
        twice.Hero.SlideCount = 2;
        Assert.Contains("ArcsWithoutSlide", Assert.Throws<XunitException>(() => twice.Dispose()).Message);
    }
}
