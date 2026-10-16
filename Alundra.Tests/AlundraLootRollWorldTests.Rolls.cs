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

namespace Alundra.Tests;

/// <summary>
/// E20.e0b2 T1, the tests (m1) to (m5) of the loot roll in the real world (see the other part of the class, <c>AlundraLootRollWorldTests.cs</c>): each expects at least one object written or one step of the real stream, so they are
/// RED on the phase A code (whose helper only rolls on the virtual register of the sink) and go green with phase B (T2).
/// </summary>
public sealed partial class AlundraLootRollWorldTests
{
    [Fact]
    public void M1_TheCreationByTheInterface_OnMap14_RollsTheLootLikeTheBinary_NineVectorsOfTheGlobalNumbers254And230AndTheMapNumber79()
    {
        using var arc = Arc14("m1");
        using var document = Read("spawn_vectors.json");
        // The oracle lists the map number 79 twice (as a number that rolls and as a control number): the two blocks of three rows are identical, one block is kept.
        var all = document.RootElement.EnumerateArray()
            .Where(v => (I(v.GetProperty("page")), I(v.GetProperty("index"))) is (0, 254) or (0, 230) or (1, 79))
            .GroupBy(v => (I(v.GetProperty("page")), I(v.GetProperty("index")), v.GetProperty("seed_name").GetString()))
            .ToList();
        Assert.All(all, g => Assert.Single(g.Select(v => (U(v.GetProperty("rng_in")), U(v.GetProperty("item")), U(v.GetProperty("rng_out")))).Distinct()));
        var rows = all.Select(g => g.First()).ToList();
        Assert.Equal(9, rows.Count);

        var observed = new List<string>();
        var expected = new List<string>();
        foreach (var row in rows)
        {
            var page = I(row.GetProperty("page"));
            var index = I(row.GetProperty("index"));
            var seed = U(row.GetProperty("rng_in"));
            expected.Add($"{page}/{index} seed {seed:X8}: item {U(row.GetProperty("item"))} +0x40 {I(row.GetProperty("flag40"))} word {U(row.GetProperty("rng_out")):X8}");

            AlundraRandom.RandomSeed = seed;
            var spawned = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, page == 1, index, HeroX << 16, HeroY << 16, -1, 0x10)!;
            observed.Add($"{page}/{index} seed {seed:X8}: item {spawned.ContentsItemId} +0x40 {spawned.ContentsGameFlag} word {(uint)AlundraRandom.RandomSeed:X8}");
            Recycle(arc, spawned);
        }

        Assert.Equal(expected, observed);
    }

    [Fact]
    public void M2_TheMapLoad_ByTheLoadTileOfTheHero_CreatesTheEntitiesOfV8_WithTheirLoot_AndLeavesTheWordOfV8_ForTwentyPairs()
    {
        var pairs = Pairs();
        var observedStructure = new List<string>();
        var expectedStructure = new List<string>();
        var observedLoot = new List<string>();
        var expectedLoot = new List<string>();
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            using var arc = new ArcRun(PairSpec(i, pair));

            expectedStructure.Add($"#{i} map {pair.Map} tile ({pair.TileX}, {pair.TileY}) [{Structure(pair.Entities.Select(e => (e.Record, e.Slot, e.Sprite)))}]");
            var created = arc.Entities.Where(e => e.EntityRefId >= 0).OrderBy(e => e.Slot).ToList();
            observedStructure.Add($"#{i} map {pair.Map} tile ({arc.HeroTileAtLoad.X}, {arc.HeroTileAtLoad.Y}) [{Structure(created.Select(e => (e.EntityRefId, e.Slot, e.SpriteType)))}]");

            expectedLoot.Add($"#{i} map {pair.Map} [{Loot(pair.Entities.Select(e => (e.Record, e.Item, e.Flag40)))}] word {pair.FinalWord:X8}");
            observedLoot.Add($"#{i} map {pair.Map} [{Loot(created.Select(e => (e.EntityRefId, e.ContentsItemId, e.ContentsGameFlag)))}] word {(uint)arc.SeedAfterTheLoad:X8}");
        }

        // The load loop of the DLL creates, at the load tile of the oracle, the entities of the binary's loop, in its slots (green from phase A on) ...
        Assert.Equal(expectedStructure, observedStructure);

        // ... and rolls their loot like the binary: the pairs that draw are in the same assertion as the pairs that do not (red until phase B).
        Assert.Equal(expectedLoot, observedLoot);
    }

    /// <summary>The five flag cases of the record (plan m3), each a row of <c>roll_vectors.json</c> (a record code 0x82 on the record, so the header is irrelevant): label prefix and what it is.</summary>
    private static readonly string[] FlagCases =
    {
        "R3 record flag=0x0001 bit not set",   // persistent bank, bit not raised: rolled, +0x40 = 1
        "R4 flag=0x0001 bit SET",              // persistent bank, bit raised: no draw, code 0
        "R4 flag=0x8001 bit SET",              // temporary bank, bit raised: no draw
        "R5 flag=0x8001 bit set in the wrong bank", // the bit is raised in the persistent bank only: counts as not raised, rolled
        "R3 record flag=0x0800 bit not set",   // 0x0800 is brought back to 0: rolled, +0x40 = 0
    };

    [Fact]
    public void M3_TheFlagsOfTheRecord_FiveCases_AreTheRowsOfTheBinary_TheRaisedOnesAlongsideTheOnesThatRoll()
    {
        using var arc = new ArcRun(new ArcSpec("E20e0b2-m3", "The Klark", "Ship Klark (inner)-390", Array.Empty<int>(), 0, 0, 0, 10, RealController: true, Prefabs: true));
        var world = arc.RealWorld!;
        var records = world.Entities.OfType<Entity>().Select(e => e.RootComponent).OfType<TileMapComponent>().Single().TileMapData!.ObjectLayers.Single(l => l.Name == "Entities").Objects;
        var record = records.Single(r => r.CustomProperties["Index"] == "4"); // a record that rolls at the load (V8: sprite 292).
        using var document = Read("roll_vectors.json");
        var rows = document.RootElement.EnumerateArray().ToList();

        var observed = new List<string>();
        var expected = new List<string>();
        foreach (var prefix in FlagCases)
        {
            var row = rows.Single(v => v.GetProperty("label").GetString()!.StartsWith(prefix, StringComparison.Ordinal));
            var flag = I(row.GetProperty("flag"));
            var seed = U(row.GetProperty("rng_in"));
            expected.Add($"{prefix}: item {I(row.GetProperty("item"))} +0x40 {I(row.GetProperty("flag_out"))} word {U(row.GetProperty("rng_out")):X8}");

            Array.Clear(ArcRun.State.GameFlags);
            Array.Clear(ArcRun.State.TemporaryFlags);
            foreach (var bank in row.GetProperty("banks").EnumerateArray())
            {
                (U(bank[0]) == 0x801EBA40 ? ArcRun.State.TemporaryFlags : ArcRun.State.GameFlags)[I(bank[1])] = U(bank[2]);
            }

            record.CustomProperties["_10"] = flag.ToString(System.Globalization.CultureInfo.InvariantCulture);
            record.CustomProperties["Contents"] = I(row.GetProperty("rec_code")).ToString(System.Globalization.CultureInfo.InvariantCulture);
            AlundraRandom.RandomSeed = seed;
            var spawned = Context(arc).SpawnEntityByRecordId(arc.Hero, 4)!;
            observed.Add($"{prefix}: item {spawned.ContentsItemId} +0x40 {spawned.ContentsGameFlag} word {(uint)AlundraRandom.RandomSeed:X8}");
            Recycle(arc, spawned);
        }

        Array.Clear(ArcRun.State.GameFlags);
        Array.Clear(ArcRun.State.TemporaryFlags);
        Assert.Equal(expected, observed);
    }

    [Fact]
    public void M4_AnAbortedCreation_RollsNothing_TheNextOneRollsExactlyItsOwnSteps()
    {
        using var arc = Arc14("m4");
        using var document = Read("spawn_vectors.json");
        var row = document.RootElement.EnumerateArray().Single(v => (I(v.GetProperty("page")), I(v.GetProperty("index")), v.GetProperty("seed_name").GetString()) == (0, 254, "S0"));
        var seed = U(row.GetProperty("rng_in"));

        var armed = true;
        AlundraWorldProxy.CreationFaultHookForTests = (site, _) =>
        {
            if (armed && site == "sprite-index")
            {
                armed = false;
                throw new InvalidOperationException("fault after AddEntity");
            }
        };

        AlundraRandom.RandomSeed = seed;
        var aborted = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 254, HeroX << 16, HeroY << 16, -1, 0x10);
        var wordAfterAbort = (uint)AlundraRandom.RandomSeed;
        var second = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 254, HeroX << 16, HeroY << 16, -1, 0x10)!;

        Assert.Equal(
            $"aborted null, word {seed:X8}; second item {U(row.GetProperty("item"))} word {U(row.GetProperty("rng_out")):X8}",
            $"aborted {(aborted is null ? "null" : "created")}, word {wordAfterAbort:X8}; second item {second.ContentsItemId} word {(uint)AlundraRandom.RandomSeed:X8}");
    }

    [Fact]
    public void M5_TheHeroRollsNothing_AloneOnTheStream_AlongsideACreationThatRolls()
    {
        using var arc = new ArcRun(new ArcSpec("E20e0b2-m5", "Inoa", "Inoa (Vision Event from Lars and Melzas cutscene)-478", Array.Empty<int>(), 0, 0, 0, 10, RealController: true, Prefabs: true));
        using var document = Read("spawn_vectors.json");
        var row = document.RootElement.EnumerateArray().Single(v => (I(v.GetProperty("page")), I(v.GetProperty("index")), v.GetProperty("seed_name").GetString()) == (0, 254, "S0"));

        // The load of the map 478 rolls nothing in the binary (V8: 22 entities, 0 draws): the word is still the seed after the adoption of the hero, who has no loot code of his own.
        var heroText = $"hero item {arc.Hero.ContentsItemId} +0x40 {arc.Hero.ContentsGameFlag} header {arc.Hero.HeaderContents}, word after the load {arc.SeedAfterTheLoad:X8}";
        AlundraRandom.RandomSeed = U(row.GetProperty("rng_in"));
        var spawned = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, 254, HeroX << 16, HeroY << 16, -1, 0x10)!;

        Assert.Equal(
            $"hero item 0 +0x40 0 header 0, word after the load {InitialSeed:X8}; creation item {U(row.GetProperty("item"))} word {U(row.GetProperty("rng_out")):X8}",
            $"{heroText}; creation item {spawned.ContentsItemId} word {(uint)AlundraRandom.RandomSeed:X8}");
    }
}
