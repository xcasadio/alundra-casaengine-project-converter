#nullable enable
using Alundra.Scripts;
using CasaEngine.Framework.Rendering.CellularLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.m2 (docs/plan-e19-opcodes.md, §1.2s.3, D-E19-66, ADR-0032) - <see cref="AlundraLibcRandom"/>, the C library
/// <c>rand()</c> of the original executable, pinned against values computed by hand from
/// <c>s = s * 0x41C64E6D + 0x3039</c> (32 bits), result <c>(s &gt;&gt; 16) &amp; 0x7FFF</c>:
///
/// from state 0: 0, 21468, 9988, 22117, 3498 (states 0x00003039, 0xD3DC167E, 0xA70427DF, 0xD6651C2C, 0x0DAA96F5);
/// from 0x12345678: 2929 (state 0x0B719151), 28487, 11805.
///
/// The generator is a process-wide static: this class shares <see cref="AlundraRandomStaticStateCollection"/> with the
/// other tests that touch static random state, so none of them runs in parallel with it, and every test restores the
/// state it found.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraLibcRandomTests : System.IDisposable
{
    private readonly uint _savedState = AlundraLibcRandom.State;

    public void Dispose()
    {
        AlundraLibcRandom.State = _savedState;
    }

    [Fact]
    public void Next_FromStateZero_ReproducesTheFirstFiveValuesAndStates()
    {
        AlundraLibcRandom.State = 0;

        var values = new int[5];
        var states = new uint[5];
        for (var i = 0; i < 5; i++)
        {
            values[i] = AlundraLibcRandom.Next();
            states[i] = AlundraLibcRandom.State;
        }

        Assert.Equal(new[] { 0, 21468, 9988, 22117, 3498 }, values);
        Assert.Equal(new uint[] { 0x00003039, 0xD3DC167E, 0xA70427DF, 0xD6651C2C, 0x0DAA96F5 }, states);
    }

    [Fact]
    public void Next_FromAnArbitraryState_ReproducesTheHandComputedValues()
    {
        AlundraLibcRandom.State = 0x12345678;

        Assert.Equal(2929, AlundraLibcRandom.Next());
        Assert.Equal(0x0B719151u, AlundraLibcRandom.State);
        Assert.Equal(28487, AlundraLibcRandom.Next());
        Assert.Equal(11805, AlundraLibcRandom.Next());
    }

    /// <summary>
    /// Map 391 (the ship in the storm, on the story chain), the real export: its 55 rain cells (type 2) built the way
    /// <c>AlundraBackdropStage</c> builds them, the generator at state 0 and the camera at 0, three ticks one by one.
    /// Tick 1: every cell that has crossed the bottom draws once, in cell order (cells 2, 30 and 47 here), and is drawn
    /// at its position BEFORE the respawn; the respawn abscissa is <c>rand() / 102</c> (0, 210, 97). Self-skips when the
    /// export is absent.
    /// </summary>
    [Fact]
    public void RealMap391_ThreeTicksFromStateZero_RainCellsRespawnAtTheLibcRandOver102()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var document = BackdropLoader.Load(projectRoot, "Ship Klark (night, break, Event)-391");
        if (document == null)
        {
            return; // self-skip: export absent
        }

        var service = new CellularLayerService();
        service.SetLayers(AlundraBackdropStage.BuildCellularDefinitions(document));
        AlundraLibcRandom.State = 0;

        var expected = new[]
        {
            (Cell: 2, Ticks: new[] { (214, 249), (0, -46), (0, -38) }),
            (Cell: 30, Ticks: new[] { (23, 264), (210, -17), (210, 5) }),
            (Cell: 47, Ticks: new[] { (47, 247), (97, -26), (97, 4) }),
        };

        for (var tick = 0; tick < 3; tick++)
        {
            service.SetFrame(0, 0, 1, Vector3.Zero);
            service.Advance(() => (uint)AlundraLibcRandom.Next());

            foreach (var (cell, ticks) in expected)
            {
                Assert.True(service.TryGetCellState(0, cell, out var state));
                Assert.Equal(ticks[tick], (state.DrawX, state.DrawY));
            }

            if (tick == 0)
            {
                Assert.Equal(0xA70427DFu, AlundraLibcRandom.State); // three draws: cells 2, 30 and 47.
            }
        }
    }

    private static string? FindProjectRoot()
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "alundra-project");
            if (System.IO.Directory.Exists(System.IO.Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null; // self-skip: alundra-project/ not present in this checkout
    }
}
