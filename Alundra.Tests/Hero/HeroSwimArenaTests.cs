#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.i T4 (docs/plan-e20-heros.md section 1.14): the swim on the REAL world, through the production path (<see cref="HitArena"/>: the arcs' real map, the real prefabs, the real exported animations, the world's own
/// <c>Update</c>), where <see cref="HeroSwimTests"/> uses the montage on a synthetic field. One arc, on the pond of map 14 (<c>Overworld 3,1-14</c>): 81 deep cells at the tiles x 40 to 46, y 43 to 59, height 0,
/// the shores at height 1. The hero stands on the shore at tile (40, 48), height 1, walks south, falls the 16 pixels of the bank into the water, floats one tick, swims, bursts with the Triangle (42 ticks, 90112 of force
/// south), swims on and stops against the bank at the south end of the pond. Written before the first run (the numbers are the oracle's: the swim 73728 south, the burst 90112, the float then the swim after a fall).
/// The same arc without boots (E20.i2a): the original stops a hero without the swim boots at the edge of the deep water (O-E19-22), this one too.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroSwimArenaTests : IDisposable
{
    private const uint Triangle = PadState.Triangle;
    private const uint Down = PadState.Down;
    private const int BootsLevel2Item = 0x1B;

    /// <summary>The shore at tile (40, 48): the centre of the tile in pixels, height 1 (16 pixels).</summary>
    private const int ShoreX = 40 * 24 + 12;
    private const int ShoreY = 48 * 16 + 8;
    private const int ShoreZ = 16;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public HeroSwimArenaTests()
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

    private sealed record Tick(int Number, uint Anim, uint Dir, int ForceX, int ForceY, int PosX, int PosY, int PosZ, int Slope, bool Adjusted, int OnGround);

    private static List<Tick> Play(bool boots, int ticks, Func<int, uint> pad)
    {
        using var arena = new HitArena(boots ? "swim-boots" : "swim-noboots", ShoreX, ShoreY, targetPixels: null, heroDirection: 0, heroPixelZ: ShoreZ);
        if (boots)
        {
            arena.Arc.Give(BootsLevel2Item);
        }

        var rows = new List<Tick>();
        arena.Begin();
        for (var tick = 1; tick <= ticks; tick++)
        {
            arena.Frame(pad(tick), 1);
            var h = arena.Hero;
            rows.Add(new Tick(tick, h.TargetAnimationId, h.TargetDirection, h.ForceX, h.ForceY, h.PosX, h.PosY, h.PosZ, h.Slope_18c, h.ForceAdjusted != 0, h.IsOnGround));
        }

        return rows;
    }

    private static int First(List<Tick> rows, Func<Tick, bool> predicate, int from = 1)
    {
        var row = rows.FirstOrDefault(r => r.Number >= from && predicate(r));
        return row?.Number ?? -1;
    }

    /// <summary>The row of the tick <paramref name="tick"/> (the first tick is 1).</summary>
    private static Tick At(List<Tick> rows, int tick) => rows[tick - 1];

    /// <summary>Down held all along, the Triangle pressed at tick <paramref name="dive"/> (and released at once).</summary>
    private static uint Pad(int tick, int dive) => tick == dive ? Down | Triangle : Down;

    [Fact]
    public void TheHeroFallsFromTheBank_Floats_Swims_Bursts_AndStopsAgainstTheBank()
    {
        // The dive tick is chosen after the landing and a few ticks of swimming: found from the run itself below (the arc is deterministic), so two passes: the first finds the tick of the first 0x0F, the second dives 6 ticks later.
        var probe = Play(boots: true, 200, tick => Down);
        var firstSwim = First(probe, r => r.Anim == 0x0F);
        Assert.True(firstSwim > 0, "the hero never swims");
        var dive = firstSwim + 6;

        var rows = Play(boots: true, 200, tick => Pad(tick, dive));

        // The way in: the bank is walked (Moving, 106496 of force), the 16 pixels are fallen (0x2C), the slope 4 is probed at the end of the landing tick, the next tick floats (0x1D: 106496 - 100352 >> 3... the force of the
        // fall, 100352, loses its eighth: 87808), the next one swims (0x0F), the force falling by 880 a tick ((87808 - 73728) >> 4) to the 73728 of the swim; the run is never prepared.
        var air = First(rows, r => r.Anim is 0x2C or 0x2D);
        Assert.True(air > 0 && air < firstSwim, "the hero falls into the water");
        var landing = First(rows, r => r.Slope == 4);
        Assert.True(landing > air);
        Assert.Equal(0x2Cu, At(rows, landing).Anim); // the slope is probed at the end of the tick the hero lands on
        Assert.Equal(0x1Du, At(rows, landing + 1).Anim);
        Assert.Equal(100352 - (100352 >> 3), At(rows, landing + 1).ForceY);
        Assert.Equal(0x0Fu, At(rows, landing + 2).Anim);
        Assert.Equal(firstSwim, landing + 2);
        Assert.DoesNotContain(rows, r => r.Anim is 0x29 or 0x03 or 0x2A or 0x04 or 0x3E);
        Assert.Equal(0u, At(rows, firstSwim).Dir);
        Assert.Equal(0, At(rows, firstSwim).ForceX);
        for (var t = firstSwim; t < dive; t++)
        {
            Assert.Equal(87808 - 880 * (t - firstSwim + 1), At(rows, t).ForceY);
        }

        for (var t = firstSwim; t < firstSwim + 16; t++) // the same ramp without the burst: the 73728 of the swim 15 ticks after the first swim tick
        {
            Assert.Equal(87808 - 880 * (t - firstSwim + 1), At(probe, t).ForceY);
        }

        Assert.Equal(73728, At(probe, firstSwim + 15).ForceY);

        // The burst: the Triangle edge gives 0x28 at once with the force of 90112 south, for 42 ticks, then the float, then the swim again (the held Down).
        Assert.Equal(0x28u, At(rows, dive).Anim);
        Assert.Equal(90112, At(rows, dive).ForceY);
        for (var t = dive; t < dive + 42; t++)
        {
            Assert.Equal(0x28u, At(rows, t).Anim);
            Assert.Equal(90112, At(rows, t).ForceY);
        }

        Assert.Equal(0x1Du, At(rows, dive + 42).Anim);
        Assert.Equal(0x0Fu, At(rows, dive + 43).Anim);

        // The south end of the pond is a bank: the hero stops against it, still swimming, the mover curtailed.
        var last = rows[^1];
        Assert.Equal(0x0Fu, last.Anim);
        Assert.True(rows.Skip(dive + 43).Any(r => r.Adjusted), "the hero meets the bank");
        Assert.Equal(4, last.Slope);
        Assert.Equal(0, rows[^1].PosZ);
        Assert.True(rows[^1].PosY == rows[^2].PosY, "the hero does not move against the bank");
    }

    [Fact]
    public void WithoutTheBoots_TheHeroStopsAtTheEdgeOfTheDeepWater_O_E19_22()
    {
        // E20.i2a (D-I2A-1): the original stops a hero without boots of level 2 at the edge of the deep water (the wall of 0x80037504, O-E19-22): this one walks to the bank and stays there, where with the boots it falls in and swims.
        var with = Play(boots: true, 120, tick => Down);
        var without = Play(boots: false, 120, tick => Down);

        Assert.Contains(with, r => r.Anim == 0x0F);
        Assert.DoesNotContain(without, r => r.Anim is 0x0F or 0x1D or 0x2C or 0x2D);
        Assert.DoesNotContain(without, r => r.Slope == 4);
        const int bankEdgeY = 49 * 16 << 16; // the first row of the deep water (tile y 49)
        Assert.All(without, r => Assert.True(r.PosY < bankEdgeY, $"tick {r.Number}: the hero is at y {r.PosY / 65536.0}, in the water"));
        Assert.All(without, r => Assert.Equal(ShoreZ << 16, r.PosZ));
        Assert.Equal(without[^2].PosY, without[^1].PosY); // stopped against the edge
        Assert.True(without.Skip(1).Any(r => r.Adjusted), "the hero meets the edge");
        Assert.True(with[^1].PosY > without[^1].PosY);
    }
}
