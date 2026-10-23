#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using static Alundra.Tests.Support.StoryChainOpcodeAudit;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5d T1, (s5) (docs/plan-e20-heros.md section 1.24.6, D-G5D-2): the static test of the programs of the pushable objects, on the pattern of <see cref="StoryChainSkippedOpcodesTests"/>: the same walk
/// (<see cref="WalkMap"/>, both branches of every conditional, the programs a <c>0x40</c> sets), the same oracle (<see cref="IsPorted"/>, derived from the real runner), restricted to the programs of the
/// placements of the five families of the export: the pillars (type 430), the statues (298), the blocks (229), the push buttons (345) and the Mini-Nilude blocks (433). The type of a placement is its
/// <c>SpriteTableIndex</c>, plus 0x100 when the bit 0x80 of its <c>SpriteDirection</c> is set (the map page). It fails as soon as a program of one of them reaches an opcode the runner skips by its size.
/// A missing export FAILS the corpus tests naming it, it never skips them.
/// </summary>
public class PushObjectProgramsOpcodesTests
{
    private static readonly int[] Families = { 229, 298, 345, 430, 433 };

    /// <summary>The number of placements of each family in the export (the census of the discovery, 111 for the four first, and the count of the Mini-Nilude blocks measured in T0).</summary>
    private static readonly Dictionary<int, int> ExpectedPlacements = new() { [229] = 9, [298] = 62, [345] = 16, [430] = 24, [433] = 28 };

    private static readonly char[] SlotLetters = { 'A', 'C', 'D', 'E', 'F' };

    private static readonly Lazy<(IReadOnlyList<(int Map, int Type)> Placements, IReadOnlyList<ReachedSite> Reached)> Corpus = new(Load);

    private static (IReadOnlyList<(int Map, int Type)>, IReadOnlyList<ReachedSite>) Load()
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Maps", "world-index.json")));
        var placements = new List<(int, int)>();
        var reached = new List<ReachedSite>();
        foreach (var entry in index.RootElement.EnumerateObject())
        {
            var map = int.Parse(entry.Name);
            var folder = Path.GetDirectoryName(Path.Combine(root, entry.Value.GetString()!))!;
            var tileMaps = Directory.GetFiles(Path.Combine(folder, "tilemap"), "*.tileMap");
            if (tileMaps.Length != 1)
            {
                continue;
            }

            var found = PlacementsOf(tileMaps[0]);
            if (found.Count == 0)
            {
                continue;
            }

            placements.AddRange(found.Select(f => (map, f.Type)));
            var (document, _) = LoadMap(root, map);
            var roots = found.SelectMany(f => RootsOf(document, f.Programs)).Distinct().ToList();
            reached.AddRange(WalkMap(map, document, roots));
        }

        return (placements, reached);
    }

    /// <summary>The placements of the five families of a tile map: their type and their A, C, D, E and F program words.</summary>
    internal static List<(int Type, int[] Programs)> PlacementsOf(string tileMapPath)
    {
        var found = new List<(int, int[])>();
        using var json = JsonDocument.Parse(File.ReadAllText(tileMapPath));
        foreach (var layer in json.RootElement.GetProperty("object_layers").EnumerateArray())
        {
            if (layer.GetProperty("name").GetString() != "Entities")
            {
                continue;
            }

            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
            {
                var props = obj.GetProperty("custom_properties");
                var type = Number(props, "SpriteTableIndex") | ((Number(props, "SpriteDirection") & 0x80) != 0 ? 0x100 : 0);
                if (Array.IndexOf(Families, type) >= 0)
                {
                    found.Add((type, new[]
                    {
                        Number(props, "EventCodesA_LoadIndex"), Number(props, "EventCodesC_TickIndex"), Number(props, "EventCodesD_TouchIndex"),
                        Number(props, "EventCodesE_DeactivateIndex"), Number(props, "EventCodesF_InteractIndex"),
                    }));
                }
            }
        }

        return found;
    }

    private static int Number(JsonElement props, string name)
    {
        var value = props.GetProperty(name);
        return value.ValueKind == JsonValueKind.String ? int.Parse(value.GetString()!) : value.GetInt32();
    }

    /// <summary>The roots of one placement: for each of its programs A, C, D, E, F, the index (<c>word &amp; 0x7F</c>, nonzero, inside its table, pointing at code).</summary>
    internal static IEnumerable<(char Slot, int Index)> RootsOf(EventProgramDocument document, int[] programs)
    {
        for (var i = 0; i < SlotLetters.Length; i++)
        {
            var slot = SlotLetters[i];
            var index = programs[i] & 0x7F;
            var table = document.TableFor("ABCDEF".IndexOf(slot));
            if (index != 0 && index < table.Length && table[index] != 0)
            {
                yield return (slot, index);
            }
        }
    }

    /// <summary>The rule: the sites reached whose opcode the oracle says is skipped.</summary>
    internal static IReadOnlyList<ReachedSite> Unported(IEnumerable<ReachedSite> reached, Func<int, bool> isPorted) => reached.Where(r => !isPorted(r.Opcode)).ToList();

    private static string Show(IEnumerable<ReachedSite> sites) => string.Join("; ", sites.Select(s => $"{Describe(s.Site)} 0x{s.Opcode:X2}"));

    // -----------------------------------------------------------------------------------------
    // The corpus
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EveryProgramOfAPushableObject_ReachesOnlyOpcodesTheRunnerPorts()
    {
        var skipped = Unported(Corpus.Value.Reached, IsPorted);

        Assert.True(skipped.Count == 0, $"{skipped.Count} site(s) of the programs of the pushable objects are skipped by their size: {Show(skipped)}");
    }

    [Fact]
    public void TheCorpusIsTheFiveFamilies_TheCountsOfTheDiscoveryPlusTheMiniNiludeBlocks_AndItWalksProgramsOnTheirMaps()
    {
        var (placements, reached) = Corpus.Value;

        foreach (var (type, expected) in ExpectedPlacements)
        {
            Assert.Equal(expected, placements.Count(p => p.Type == type));
        }

        Assert.Equal(111, placements.Count(p => p.Type != 433));
        // 26 maps hold a placement; 19 of them have a program on one (the other seven hold only placements without a program of their own).
        Assert.Empty(reached.Select(r => r.Site.Map).Distinct().Except(placements.Select(p => p.Map)));
        Assert.Equal(26, placements.Select(p => p.Map).Distinct().Count());
        Assert.Equal(19, reached.Select(r => r.Site.Map).Distinct().Count());
        Assert.Equal(75, reached.Select(r => (r.Site.Map, r.Site.Slot, r.Site.Program)).Distinct().Count());
        Assert.Contains(reached, r => r.Opcode == 0x1E); // a walk of a statue or a block
        Assert.Contains(reached, r => r.Opcode == 0xAA); // the push buttons of the map 426 (E20.g5b)
    }

    // -----------------------------------------------------------------------------------------
    // The rule and the placements, on made-up input
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheRule_FlagsASiteWhoseOpcodeTheOracleSkips_AndOnlyThat()
    {
        var sites = new[]
        {
            new ReachedSite(new ProgramSite(1, 'C', 2, 10), 0x1E),
            new ReachedSite(new ProgramSite(1, 'C', 2, 13), 0x72),
            new ReachedSite(new ProgramSite(2, 'F', 3, 7), 0xAA),
        };

        Assert.Empty(Unported(sites, _ => true));
        Assert.Equal(new[] { 0x72 }, Unported(sites, op => op != 0x72).Select(s => s.Opcode));
        Assert.Equal(3, Unported(sites, _ => false).Count);
    }

    [Fact]
    public void ThePlacementsOfATileMap_AreRecognisedByTheirTypeAndTheMapPageBit()
    {
        var path = Path.Combine(Path.GetTempPath(), "g5d-" + Guid.NewGuid().ToString("N") + ".tileMap");
        try
        {
            File.WriteAllText(path, """
                {"object_layers":[{"name":"Entities","objects":[
                  {"custom_properties":{"SpriteTableIndex":"42","SpriteDirection":"128","EventCodesA_LoadIndex":"138","EventCodesC_TickIndex":"138","EventCodesD_TouchIndex":"0","EventCodesE_DeactivateIndex":"0","EventCodesF_InteractIndex":"138"}},
                  {"custom_properties":{"SpriteTableIndex":"42","SpriteDirection":"64","EventCodesA_LoadIndex":"1","EventCodesC_TickIndex":"1","EventCodesD_TouchIndex":"1","EventCodesE_DeactivateIndex":"1","EventCodesF_InteractIndex":"1"}},
                  {"custom_properties":{"SpriteTableIndex":"229","SpriteDirection":"64","EventCodesA_LoadIndex":0,"EventCodesC_TickIndex":131,"EventCodesD_TouchIndex":0,"EventCodesE_DeactivateIndex":0,"EventCodesF_InteractIndex":0}}]}]}
                """);

            var found = PlacementsOf(path);

            Assert.Equal(new[] { 298, 229 }, found.Select(f => f.Type)); // 42 + 0x100 on the map page; 42 on the global page is another sprite.
            Assert.Equal(new[] { 138, 138, 0, 0, 138 }, found[0].Programs);
            Assert.Equal(new[] { 0, 131, 0, 0, 0 }, found[1].Programs);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
