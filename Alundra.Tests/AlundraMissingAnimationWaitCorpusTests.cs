#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.AlundraStoryChainOpcodeAudit;

namespace Alundra.Tests;

/// <summary>
/// E19.m8 M-07 / O-E19-14 (docs/plan-e19-opcodes.md section 1.2s.9): the static guard on the corpus of the finding of E19.c2. Over EVERY map of the export, every
/// entity of the <c>Entities</c> layer walks its own programs (slots A, C, D, E and F, plus the roots the <c>0x40</c> it reaches add), and a site is an
/// <c>0x1A n</c> (set animation) immediately followed (pc + 2) by a <c>0x1C</c> or <c>0x1D</c> (wait for the animation) where <c>n</c> is at least the number of
/// animation sets of the entity's prefab: an animation the prefab does not have. The behaviour is pinned by T-D16 (AlundraAnimationClockDriveTests); this pins
/// the extent. A site is keyed by (map, pc of the wait opcode). A missing export FAILS the test naming it, never skips it. A change of the measured set is
/// settled by evidence, never by re-pinning the list.
/// </summary>
public class AlundraMissingAnimationWaitCorpusTests
{
    private sealed record Site(int Map, int Pc, int Animation, int SetCount, string Prefab, string Entity);

    private static readonly (string Property, char Slot)[] SlotProperties =
    {
        ("EventCodesA_LoadIndex", 'A'),
        ("EventCodesC_TickIndex", 'C'),
        ("EventCodesD_TouchIndex", 'D'),
        ("EventCodesE_DeactivateIndex", 'E'),
        ("EventCodesF_InteractIndex", 'F'),
    };

    private static int ReadInt(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? int.Parse(value.GetString()!) : value.GetInt32();

    /// <summary>The sites of one map.</summary>
    private static List<Site> SitesOfMap(string root, SpriteRecordCatalog catalog, int map)
    {
        var (document, tileMap) = LoadMap(root, map);
        var found = new List<Site>();
        using var json = JsonDocument.Parse(File.ReadAllText(tileMap));
        foreach (var layer in json.RootElement.GetProperty("object_layers").EnumerateArray())
        {
            if (layer.GetProperty("name").GetString() != "Entities")
            {
                continue;
            }

            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
            {
                var props = obj.GetProperty("custom_properties");
                if (!props.TryGetProperty("PrefabAssetId", out var prefabProperty)
                    || !Guid.TryParse(prefabProperty.GetString(), out var prefabId)
                    || !catalog.TryGet(prefabId, out var header)
                    || header.AnimSets is null)
                {
                    continue;
                }

                var roots = new List<(char, int)>();
                foreach (var (property, slot) in SlotProperties)
                {
                    var index = ReadInt(props.GetProperty(property)) & 0x7F;
                    var table = document.TableFor("ABCDEF".IndexOf(slot));
                    if (index != 0 && index < table.Length && table[index] != 0)
                    {
                        roots.Add((slot, index));
                    }
                }

                var entity = props.TryGetProperty("Index", out var entityIndex) ? $"{entityIndex.GetString()} {props.GetProperty("EntityName").GetString()}" : "?";
                foreach (var reached in WalkMap(map, document, roots))
                {
                    var pc = reached.Site.Pc;
                    if (reached.Opcode == 0x1A && pc + 2 < document.Codes.Length && document.Codes[pc + 2] is 0x1C or 0x1D
                        && document.Codes[pc + 1] >= header.AnimSets.Count)
                    {
                        found.Add(new Site(map, pc + 2, document.Codes[pc + 1], header.AnimSets.Count, prefabId.ToString("D")[..8], entity));
                    }
                }
            }
        }

        return found;
    }

    private static List<Site> Corpus()
    {
        var root = ProjectRootFinder.Find();
        var catalog = new SpriteRecordCatalog(root);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Maps", "world-index.json")));
        return index.RootElement.EnumerateObject()
            .Select(p => int.Parse(p.Name))
            .OrderBy(m => m)
            .SelectMany(m => SitesOfMap(root, catalog, m))
            .ToList();
    }

    private static string Show(IEnumerable<Site> sites) =>
        string.Join("; ", sites.Select(s => $"map {s.Map} wait@{s.Pc} 0x1A[{s.Animation}] of {s.SetCount} sets, prefab {s.Prefab}, entity {s.Entity}"));

    [Fact]
    public void M07_TheWaitsOnAnAnimationThePrefabLacks_AreTheThreeFlamesOfTheLairOfNirude_AndTwoAnimation80Sites()
    {
        var sites = Corpus();

        // E19.c2 (plan section 1.2) names the Flames of the lair of Nirude (prefab 0edffd14, 11 entities) at maps 35 @1228, 38 @850 and 39 @726: these are the
        // pcs of the wait opcode (the 0x1A is two bytes before), animation 9 of a bank of 9 sets (0..8). E19.m8 measured two more sites, both off the chain:
        // map 240 @535 (Giles, prefab 9b582582, 19 sets) and map 292 @429 (Nestus, prefab bbeba3db, 15 sets) ask animation 80. The binary's 0x1A stores v[1]
        // unchecked (0x8003D7B4) and UpdateAnimation indexes the table without a bound; what the original shows that far past the table is not established
        // (O-E19-79), the DLL takes the same rule as for the Flames.
        var distinct = sites.Select(s => (s.Map, s.Pc)).Distinct().OrderBy(k => k).ToList();
        Assert.True(
            distinct.SequenceEqual(new[] { (35, 1228), (38, 850), (39, 726), (240, 535), (292, 429) }),
            $"expected the five measured sites, measured {distinct.Count}: {Show(sites)}");
        Assert.All(sites.Where(s => s.Map is 35 or 38 or 39), s =>
        {
            Assert.Equal("0edffd14", s.Prefab);
            Assert.Equal(9, s.Animation);
            Assert.Equal(9, s.SetCount);
        });
        Assert.Equal(11, sites.Count(s => s.Map is 35 or 38 or 39));
        Assert.Equal(new[] { (240, "9b582582", 80, 19), (292, "bbeba3db", 80, 15) },
            sites.Where(s => s.Map is 240 or 292).Select(s => (s.Map, s.Prefab, s.Animation, s.SetCount)).Distinct().OrderBy(k => k.Map).ToArray());
    }
}
