#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.c2b T4 (docs/plan-e20-heros.md section 1.12.1, D-C2B-2, test (h1)): the hazard cells on REAL maps (the production path, the real controller and the real cells of the export). A hero who stands on a hazard cell loses
/// the cost of the cell (<c>{0, 1, 2, 3}[(+0x180 &amp; 0x180) &gt;&gt; 7]</c>, the table of the binary: 1 on the map 36, 2 and 3 on the test map 0, T0 (e) of <c>docs/plan-e20-annexe/e20-c2b/results.md</c>) every 120 ticks, the
/// time his invulnerability lasts, turns his back to where he looked (<c>(+0x8C + 0x10) &amp; 0x1F</c>) and is thrown back 33 pixels as by any hurt; the armour does not count; the boots of level 3 spare him, those of level 2 do not.
/// The expected values are the binary's (the vectors of <c>docs/plan-e20-annexe/e20-c2b/oracle/out/vectors_entry.json</c> and the recoil of A1 of the E20.c2 oracle), never computed by the code under test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroHazardCellTests : IDisposable
{
    private const int HurtAnimation = 0x31;
    private const int InvulnerabilityTicks = 120;
    private const int RecoilRaw = 2_168_832; // the sum of the forces of A1 from the hurt to the return to Idle, -2 168 832 raw = 33.09375 px.
    private const int Frames = 300;

    public HeroHazardCellTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private sealed record Frame(int Tick, int Hp, int DamagedTicks, uint Animation, uint Direction, int PosX);

    /// <summary>The hero arrives on the tile (<paramref name="tileX"/>, <paramref name="tileY"/>, level <paramref name="tileZ"/>) of the real map <paramref name="map"/>, facing <paramref name="direction"/>, owning <paramref name="items"/>; one tick per frame.</summary>
    private static List<Frame> Stand(int map, int tileX, int tileY, int tileZ, uint direction, int frames, params int[] items)
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var worldDir = Directory.GetDirectories(Path.Combine(root, "Maps")).SelectMany(Directory.GetDirectories)
            .First(d => Path.GetFileName(d).EndsWith("-" + map, StringComparison.Ordinal));
        using var arc = new ArcRun(new ArcSpec("E20c2b-h1-" + map + "-" + tileX, Path.GetFileName(Path.GetDirectoryName(worldDir)!), Path.GetFileName(worldDir), Array.Empty<int>(), 0, 0, 0, 5000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((tileX * 24 + 12) << 16, (tileY * 16 + 8) << 16, tileZ << 20, GameState.ResetAnimationId, direction)));
        arc.Give(items);
        PlayerManager.SetPlayerHpMax(GameState.Instance, 10);
        PlayerManager.SetPlayerHp(GameState.Instance, 10);
        var trace = new List<Frame>();
        for (var tick = 1; tick <= frames; tick++)
        {
            arc.OneFrame();
            var hero = arc.Hero;
            trace.Add(new Frame(tick, GameState.Instance.PlayerStats.Hp, hero.DamagedTickCounter, hero.TargetAnimationId, hero.TargetDirection, hero.PosX));
        }

        return trace;
    }

    /// <summary>The ticks at which the hero was hurt: the invulnerability counter reads 119 (written 120 by the hurt block, counted down once by the counters pass of the same tick).</summary>
    private static int[] HurtTicks(List<Frame> trace) => trace.Where(f => f.DamagedTicks == InvulnerabilityTicks - 1).Select(f => f.Tick).ToArray();

    [Theory]
    [InlineData(36, 12, 44, 1, 1)]
    [InlineData(0, 4, 53, 0, 2)]
    [InlineData(0, 10, 53, 0, 3)]
    public void ARealHazardCell_CostsItsCostEvery120Ticks_AndTheHeroIsThrownBack33Pixels(int map, int tileX, int tileY, int tileZ, int cost)
    {
        var trace = Stand(map, tileX, tileY, tileZ, 8, Frames);

        var hurt = HurtTicks(trace);
        Assert.Equal(3, hurt.Length);
        Assert.Equal(new[] { hurt[0], hurt[0] + InvulnerabilityTicks, hurt[0] + 2 * InvulnerabilityTicks }, hurt);
        foreach (var (tick, count) in hurt.Select((t, i) => (t, i + 1)))
        {
            Assert.Equal(Math.Max(0, 10 - cost * count), trace[tick - 1].Hp);                         // the cost, floor 0.
            Assert.Equal(Math.Max(0, 10 - cost * (count - 1)), trace[tick - 2].Hp);                   // nothing was lost before the tick.
            Assert.Equal((uint)HurtAnimation, trace[tick - 1].Animation);
        }

        // Between two hurts the life does not move (the hero is invulnerable, then the next hurt is 120 ticks after the last).
        Assert.All(trace.Where(f => f.Tick > hurt[0] && f.Tick < hurt[1]), f => Assert.Equal(10 - cost, f.Hp));

        // The recoil of the hero hurt at rest is the one of A1: -2 168 832 raw along the facing the hurt gave him (here east, so the position grows).
        var start = trace[hurt[0] - 2].PosX;
        Assert.Equal(start + RecoilRaw, trace[hurt[0] + 16].PosX);
    }

    [Theory]
    [InlineData(0u, 16u)]
    [InlineData(8u, 24u)]
    [InlineData(16u, 0u)]
    [InlineData(24u, 8u)]
    public void TheHurtOfACell_TurnsTheHeroBackToWhereHeLooked(uint facing, uint expected)
    {
        var trace = Stand(0, 4, 53, 0, facing, 10);

        var hurt = HurtTicks(trace).First();
        Assert.Equal(expected, trace[hurt - 1].Direction); // (+0x8C + 0x10) & 0x1F, the return of the entry.
    }

    [Fact]
    public void TheArmourIsIgnoredOnACell_TheSilverArmourLosesTheFullCostOf3()
    {
        var trace = Stand(0, 10, 53, 0, 8, 130, 0x12, 0x13, 0x14);

        var hurt = HurtTicks(trace);
        Assert.Equal(2, hurt.Length);
        Assert.Equal(7, trace[hurt[0] - 1].Hp); // 10 - 3: the armour (the silver one makes an orc's blow 1) does not reduce the cell.
        Assert.Equal(4, trace[hurt[1] - 1].Hp);
    }

    [Theory]
    [InlineData(0x19, true)]   // the object 0x19 does not count: level 0.
    [InlineData(0x1A, true)]   // level 1.
    [InlineData(0x1B, true)]   // level 2: hurt (the cell spares the boots of level 3 only).
    [InlineData(0x1C, false)]  // level 3: spared.
    public void TheBootsOfLevel3SpareTheHeroOfACell_ThoseOfLevel2DoNot(int boots, bool hurt)
    {
        var trace = Stand(0, 4, 53, 0, 8, Frames, boots);

        if (hurt)
        {
            Assert.Equal(3, HurtTicks(trace).Length);
            Assert.Equal(4, trace[^1].Hp); // 10 - 3 * 2.
        }
        else
        {
            Assert.Empty(HurtTicks(trace));
            Assert.All(trace, f => Assert.Equal((10, 0), (f.Hp, f.DamagedTicks)));
            Assert.DoesNotContain(trace, f => f.Animation == HurtAnimation);
        }
    }
}
