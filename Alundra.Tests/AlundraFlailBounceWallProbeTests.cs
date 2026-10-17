#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Cells;
using Alundra.Transitions;

namespace Alundra.Tests;

/// <summary>
/// E20.f2 T2 (docs/plan-e20-heros.md section 1.10), the wall probes of the oracle on the REAL world: the hero montage has no frame box (its sprite is synthetic), so the probes whose bounce comes from a wall under
/// a corner of the box (<c>F-iron-wall1</c>, <c>F-iron-wall2</c>, <c>F-iron-wk40</c>, <c>F-steel-wall2</c>) and the flat control (<c>F-iron-nohit</c>) run in the arena of the hit-pass tests
/// (<see cref="HitArena"/>: the real world of map 14, the real hero prefab, the real sprite clock and frame boxes), with the hero where the oracle puts him (pixel (100, 100), facing east, Square at tick 1).
/// The oracle's field has a wall in every cell from the column 5 east; the real map 14 is flat there, so the cells (5, 5) and (5, 6) - the ones under the east corners of the box of the swing, whose first frame
/// stands at X 107 to 128 and Y 94 to 104 - are rewritten through the store of the world (the opcode 0x85 and 0x54 primitives, E7) with the same properties: a copy of a real cell of height 1 (33, 41) or 2 (34, 48) with
/// walkability 0 from the same map, and the bit 0x40 set on a flat cell. The animation the DLL gives is compared with the oracle's at every tick, and no hit is ever registered: the bounces here are the walls'.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraFlailBounceWallProbeTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const int IronFlail = 9;
    private const int SteelFlail = 10;
    private const int FlailSlot = 3;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public AlundraFlailBounceWallProbeTests()
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

    private sealed record Tick(uint Target, int HitCounter, bool HasBox);

    private static List<Tick> Run(int item, Action<ICellMutator>? walls, int ticks)
    {
        using var arena = new HitArena("f2-wall", 100, 100, null);
        arena.Arc.Give(item);
        arena.Arc.Equip(FlailSlot);
        walls?.Invoke(arena.Arc.Proxy.CellMutator ?? throw new InvalidOperationException("the world has no cell store"));

        var rows = new List<Tick>();
        arena.AfterTick = _ => rows.Add(new Tick(arena.Hero.TargetAnimationId, arena.Hero.HitCounter, arena.Hero.HasFrameBox));
        arena.Begin();
        arena.Frame(Square, 1);
        for (var i = 1; i < ticks; i++)
        {
            arena.Frame(0, 1);
        }

        return rows;
    }

    private static void AssertTargetsEqualTheOracle(string probe, List<Tick> dll)
    {
        var oracle = FlailProbe.Load(probe);
        Assert.Equal(oracle.Count, dll.Count);
        var diffs = oracle.Where((row, i) => dll[i].Target != row.AnimTarget).Select((row, _) => row.Tick).ToList();
        var detail = string.Join(", ", oracle.Where((row, i) => dll[i].Target != row.AnimTarget).Take(8).Select(row => $"tick {row.Tick}: dll 0x{dll[row.Tick - 1].Target:X2}, oracle 0x{row.AnimTarget:X2}"));
        Assert.True(diffs.Count == 0, $"{probe}: {diffs.Count} ticks differ from the oracle; the first ones: {detail}");
        Assert.All(dll, t => Assert.Equal(0, t.HitCounter)); // no target in the arena: every bounce is the wall's.
    }

    // The cell (5, 5) and (5, 6) rewritten from real cells of the map (height 1: (33, 41) over two rows; height 2: (34, 48) over two rows).
    private static void WallOfHeight1(ICellMutator cells) => cells.CopyCellRectangle(33, 41, 1, 2, 5, 5);

    private static void WallOfHeight2(ICellMutator cells) => cells.CopyCellRectangle(34, 48, 1, 2, 5, 5);

    private static void WallOfBit40(ICellMutator cells)
    {
        cells.SetCellBits(5, 5, 0x40, 0);
        cells.SetCellBits(5, 6, 0x40, 0);
    }

    [Fact]
    public void FIronWall2_AWallOfHeight2UnderTheBoxCorner_BouncesTheIronFlailAtTick10_ReturnsToIdleAtTick26()
    {
        var dll = Run(IronFlail, WallOfHeight2, 40);

        AssertTargetsEqualTheOracle("F-iron-wall2", dll);
        Assert.False(dll[7].HasBox); // tick 8: no box yet,
        Assert.True(dll[8].HasBox); // tick 9: the box of the swing appears (the oracle: from tick 9),
        Assert.Equal(0x12u, dll[8].Target);
        Assert.Equal(0x08u, dll[9].Target); // and the bounce comes at tick 10.
        Assert.Equal(0x00u, dll[25].Target); // tick 26.
    }

    [Fact]
    public void FIronWall1_AWallOfHeight1_BouncesAtTick10()
    {
        var dll = Run(IronFlail, WallOfHeight1, 40);

        AssertTargetsEqualTheOracle("F-iron-wall1", dll);
        Assert.Equal(0x08u, dll[9].Target);
    }

    [Fact]
    public void FIronWk40_AWalkabilityBit0x40AtHeight0_BouncesAtTick10()
    {
        var dll = Run(IronFlail, WallOfBit40, 40);

        AssertTargetsEqualTheOracle("F-iron-wk40", dll);
        Assert.Equal(0x08u, dll[9].Target);
    }

    [Fact]
    public void FSteelWall2_TheSteelFlailBouncesTo0x25AtTick10()
    {
        var dll = Run(SteelFlail, WallOfHeight2, 50);

        AssertTargetsEqualTheOracle("F-steel-wall2", dll);
        Assert.Equal(0x25u, dll[9].Target);
    }

    [Fact]
    public void FIronNoHit_OnAFlatField_NothingBounces_TheSwingEndsAtTick32()
    {
        var dll = Run(IronFlail, null, 40);

        AssertTargetsEqualTheOracle("F-iron-nohit", dll);
        Assert.True(dll[8].HasBox); // the box exists,
        Assert.All(dll.Take(31), t => Assert.Equal(0x12u, t.Target)); // and the swing runs its 31 ticks,
        Assert.Equal(0x00u, dll[31].Target); // then Idle at tick 32.
    }
}
