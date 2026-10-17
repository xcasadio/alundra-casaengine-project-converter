#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using static Alundra.Tests.AlundraLootOracle;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.e0b2 (docs/plan-e20-heros.md section 1.17.2, T1): the loot roll of a creation in the REAL world (<see cref="AlundraWorldProxy"/>), against the oracle of the binary
/// (<c>docs/plan-e20-annexe/e20-e0b/oracle/out</c>). The tests (m1) to (m5) are written after the phase A code and are RED until phase B (the phase A helper only rolls on the virtual register of the measuring sink: no
/// <c>ContentsItemId</c>, no <c>ContentsGameFlag</c>, no draw of the real stream): each of them expects at least one object written or one step of the real stream. Two tests are green from phase A on: the word of the stream
/// right after a load that rolls nothing, and the inertness of the helper without the variable of the sink. Every expected value is a row of the oracle; nothing is derived from the DLL.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed partial class AlundraLootRollWorldTests : IDisposable
{
    private const uint InitialSeed = 0xB017C93D;
    private const int HeroX = 150;
    private const int HeroY = 100;

    public AlundraLootRollWorldTests()
    {
        AlundraRandom.Reset();
    }

    public void Dispose()
    {
        AlundraWorldProxy.CreationFaultHookForTests = null;
        AlundraRandom.Reset();
    }

    private static ArcRun Arc14(string name) => new(new ArcSpec("E20e0b2-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    /// <summary>The arrival records of the pairs of V8 that have one (index of the pair in <c>v8.json</c> -> record); the others are loaded without arrival (the New Game tile). The values are the ones of the arcs of the suite.</summary>
    private static readonly Dictionary<int, ArcArrival> Arrivals = new()
    {
        [6] = Arrival(84 << 16, 120 << 16, 1 << 20, 8),                                                    // 115, LeverArcs.Spec(E20c1b2-A115)
        [7] = Arrival(84 << 16, 328 << 16, 0, 8),                                                          // 116, E20c1b2-B116
        [8] = Arrival(500 << 16, 192 << 16, 1 << 20, 24),                                                  // 117, E20c1b2-C117
        [9] = Arrival(72 << 16, 640 << 16, 2 << 20, 24),                                                   // 117, E20c1b2-D117
        [10] = Arrival(120 << 16, 440 << 16, 8 << 20, 24),                                                 // 44, E20c1b2-smoke44
        [11] = Arrival(47972352, 27787264, 0, 0),                                                          // 162, TH4-A20
        [12] = Arrival(19660800, 23592960, 0, 16),                                                         // 165, TH4-A10
        [13] = Arrival(60555264, 24641536, 0, 16),                                                         // 185, TH4-A19
        [14] = Arrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, 0),                              // 179, day3-after-dream
        [15] = Arrival(11796480, 36175872, 10485760, 24),                                                  // 176, TH4-A13
        [16] = Arrival(16515072, 61341696, 0, 16),                                                         // 10, TH4-A10J
        [17] = Arrival(30670848, 54001664, 1048576, 16),                                                   // 135, TH4-A17
        [18] = Arrival(60555264, 26738688, 0, 16),                                                         // 178, TH4-A18
    };

    private static ArcArrival Arrival(int x, int y, int z, uint direction) => new(x, y, z, AlundraGameState.ResetAnimationId, direction);

    private static ArcSpec PairSpec(int index, LoadPair pair)
        => new($"E20e0b2-V8-{index}-{pair.Map}", pair.Zone, pair.World, Array.Empty<int>(), 0, 0, 0, 10, RealController: true, Prefabs: true, Arrival: Arrivals.GetValueOrDefault(index));

    private static string Structure(IEnumerable<(int Record, int Slot, int Sprite)> entities) => string.Join(",", entities.Select(e => $"{e.Record}:{e.Slot}:{e.Sprite}"));

    private static string Loot(IEnumerable<(int Record, uint Item, int Flag)> entities) => string.Join(",", entities.Select(e => $"{e.Record}:{e.Item}:{e.Flag}"));

    private static void Recycle(ArcRun arc, AlundraEntityScriptProxy spawned)
    {
        spawned.Status = EntityStatus.FlagToDestroy;
        arc.Proxy.RecycleDestroyedEntities();
    }

    [Fact]
    public void M0_ThePairsOfV8_AreTwentyWithTheLoadTileOfTheirArrival_AndTheNamedMapsAreAmongThem()
    {
        var pairs = Pairs();
        Assert.Equal(20, pairs.Count);
        Assert.Contains(14, pairs.Select(p => p.Map));
        Assert.Contains(390, pairs.Select(p => p.Map));
        Assert.Contains(392, pairs.Select(p => p.Map));
        Assert.Contains(115, pairs.Select(p => p.Map));
        Assert.Contains(116, pairs.Select(p => p.Map));
        Assert.Contains(167, pairs.Select(p => p.Map));
        for (var i = 0; i < pairs.Count; i++)
        {
            // The tile that the load loop reads: the New Game tile without arrival, else the arrival record's ((PosX >> 16) / 24, (PosY >> 16) / 16, AdoptPlayerPawn).
            var (x, y) = Arrivals.TryGetValue(i, out var a) ? ((a.PosX >> 16) / 24, (a.PosY >> 16) / 16) : (AlundraGameState.CameraTileX, AlundraGameState.CameraTileY);
            Assert.True(pairs[i].TileX == x && pairs[i].TileY == y, $"pair {i} (map {pairs[i].Map}): oracle tile ({pairs[i].TileX}, {pairs[i].TileY}), the arc's ({x}, {y})");
        }
    }

    [Fact]
    public void TheWordAfterTheLoad_IsTheSeed_OnTheMapsWhoseLoadRollsNothing_SevenOfTheTwentyPairs()
    {
        var pairs = Pairs();
        var quiet = Enumerable.Range(0, pairs.Count).Where(i => !pairs[i].Rolls).ToList();
        Assert.Equal(7, quiet.Count); // 389, 391, 478, 162, 185, 176, 135 (the critic counts seven).
        foreach (var i in quiet)
        {
            using var arc = new ArcRun(PairSpec(i, pairs[i]));
            Assert.True(InitialSeed == (uint)arc.SeedAfterTheLoad, $"pair {i} (map {pairs[i].Map}): the word after the load is {(uint)arc.SeedAfterTheLoad:X8}");
            Assert.True(InitialSeed == (uint)AlundraRandom.RandomSeed, $"pair {i} (map {pairs[i].Map}): the word after the construction is {(uint)AlundraRandom.RandomSeed:X8}");
            Assert.Equal((pairs[i].TileX, pairs[i].TileY), arc.HeroTileAtLoad);
        }
    }

    [Fact]
    public void WithoutTheVariableOfTheSink_TheHelperOfTheCreation_WritesNoFile_AndMovesNeitherTheVirtualWordNorTheCounters()
    {
        if (AlundraRandomShadow.Active)
        {
            return; // the measuring run: the sink is on on purpose.
        }

        using var arc = Arc14("inert");
        var word = AlundraRandomShadow.VirtualWord;
        var events = AlundraRandomShadow.EventCount;
        var lines = AlundraRandomShadow.LinesWritten;

        // A creation that would roll (global 254, header code with bit 0x80) and one that cannot (global 3).
        Recycle(arc, Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 254, HeroX << 16, HeroY << 16, -1, 0x10)!);
        Recycle(arc, Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 3, HeroX << 16, HeroY << 16, -1, 0x10)!);

        Assert.Equal(word, AlundraRandomShadow.VirtualWord);
        Assert.Equal(events, AlundraRandomShadow.EventCount);
        Assert.Equal(lines, AlundraRandomShadow.LinesWritten);
        Assert.Equal(0, lines);
        Assert.Equal(0, events);
    }
}
