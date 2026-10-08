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
/// E19.m8 M-07 / O-E19-14 / O-E19-79 (docs/plan-e19-opcodes.md section 1.2s.9, M8-b then M8-d): the static guard on the corpus of the finding of E19.c2,
/// aware of the logic-entity retarget. Over EVERY map of the export, every entity of the <c>Entities</c> layer walks its own programs (slots A, C, D, E and
/// F, plus the roots the <c>0x40</c> it reaches add). A site is an <c>0x1A n</c> (set animation) immediately followed (pc + 2) by a <c>0x1C</c> or
/// <c>0x1D</c> (wait for the animation) where <c>n</c> is at least the number of animation sets of a prefab the instruction can act on: the binary's
/// <c>0x1A</c> acts on the LOGIC entity (owner + 0x230, read before every instruction, <c>0x80042284</c>), which is the owner until <c>0x42</c> or <c>0x43</c>
/// retargets it. The set of possible logic entities is therefore propagated forward over the program's control flow (join = union, entry = the owner):
/// <c>0x42</c> and <c>0x43 [0x81]</c> give the hero, <c>0x43 [0x80]</c> changes nothing, <c>0x43 [n &lt; 0x80]</c> gives the object of the <c>Entities</c>
/// layer whose <c>Index</c> is n, any other search is a named failure. The behaviour is pinned by T-D16 (AlundraAnimationClockDriveTests); this pins the
/// extent. A site is keyed by (map, pc of the wait opcode). A missing export FAILS the test naming it, never skips it. A change of the measured set is
/// settled by evidence, never by re-pinning the list. Limit: the word owner + 0x230 is shared by the slots A to F and never reset (plan section 0.2.3), so
/// a retarget made by another program could reach a <c>0x1A</c> with no retarget visible in its own program; the census assumes a program starts on its
/// owner. That is verified for the Flames, not for the whole corpus.
/// </summary>
public class AlundraMissingAnimationWaitCorpusTests
{
    private sealed record Site(int Map, int Pc, int Animation, int SetCount, string Prefab, string Entity);

    /// <summary>An entity a program's logic context can point at: its prefab (short id), its label, and the number of animation sets of the prefab.</summary>
    internal sealed record CensusTarget(string Prefab, string Entity, int SetCount);

    /// <summary>A site of one program: the pc of the wait opcode, the animation asked, and the target that lacks it.</summary>
    internal sealed record CensusSite(int Pc, int Animation, CensusTarget Target);

    /// <summary>The census of one program: its sites, its named failures, the pcs the flow reached and the programs a <c>0x40</c> starts.</summary>
    internal sealed record CensusResult(
        IReadOnlyList<CensusSite> Sites,
        IReadOnlyList<string> Failures,
        IReadOnlySet<int> Reached,
        IReadOnlyList<(int Slot, int Index)> Spawned);

    private const string Slots = "ABCDEF";

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

    // -----------------------------------------------------------------------------------------
    // The census of one program (pure)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The retarget-aware census of the program starting at <paramref name="entry"/>. The control flow follows the rules of
    /// <see cref="AlundraStoryChainOpcodeAudit"/>'s walk (jumps 0x02, conditionals 0x03/0x04/0x30/0x31/0x74, the directional 0x57/0x58 that never fall
    /// through, the return marks of 0x78/0x79/0x7B/0x7C, the roots of 0x40, a stop at 0xFF or at a zero or unknown size); on top of it each pc carries the
    /// set of logic entities the instruction can act on, joined by union. <paramref name="byIndex"/> resolves <c>0x43 [n]</c> for n below 0x80 (null: no
    /// such entity).
    /// </summary>
    internal static CensusResult Census(int[] codes, int entry, CensusTarget owner, CensusTarget hero, Func<int, CensusTarget?> byIndex)
    {
        var count = codes.Length;
        var inState = new Dictionary<int, HashSet<CensusTarget>>();
        var outState = new Dictionary<int, HashSet<CensusTarget>>();
        var marks = new HashSet<int>();
        var returnUsers = new HashSet<int>();
        var work = new Queue<int>();
        var failures = new List<string>();
        var spawned = new List<(int, int)>();
        int Byte(int index) => index >= 0 && index < count ? codes[index] : 0;
        int S16(int lo, int hi) => (short)(Byte(lo) | (Byte(hi) << 8));

        // The join: a pc takes the union of what reaches it.
        void Flow(int pc, HashSet<CensusTarget> state)
        {
            if (pc < 0 || pc >= count)
            {
                return;
            }

            if (!inState.TryGetValue(pc, out var current))
            {
                inState[pc] = new HashSet<CensusTarget>(state);
                work.Enqueue(pc);
                return;
            }

            var before = current.Count;
            current.UnionWith(state);
            if (current.Count > before)
            {
                work.Enqueue(pc);
            }
        }

        void AddMark(int mark)
        {
            if (marks.Add(mark))
            {
                foreach (var user in returnUsers)
                {
                    if (outState.TryGetValue(user, out var userState))
                    {
                        Flow(mark, userState);
                    }
                }
            }
        }

        HashSet<CensusTarget> Transfer(int op, int pc, HashSet<CensusTarget> state)
        {
            switch (op)
            {
                case 0x42:
                    return new HashSet<CensusTarget> { hero };
                case 0x43:
                {
                    var arg = Byte(pc + 1);
                    if (arg == 0x80)
                    {
                        return state;
                    }

                    if (arg == 0x81)
                    {
                        return new HashSet<CensusTarget> { hero };
                    }

                    if (arg >= 0x80)
                    {
                        failures.Add($"unresolved search 0x{arg:X2} of 0x43 at pc {pc}");
                        return state;
                    }

                    var found = byIndex(arg);
                    if (found is null)
                    {
                        failures.Add($"no entity of Index {arg} for the 0x43 at pc {pc}");
                        return state;
                    }

                    return new HashSet<CensusTarget> { found };
                }
                default:
                    return state;
            }
        }

        Flow(entry, new HashSet<CensusTarget> { owner });
        while (work.Count > 0)
        {
            var pc = work.Dequeue();
            var op = codes[pc];
            if (!EventOpcodeSizeTable.Entries.TryGetValue((byte)op, out var info) || info.Size == 0 || op == 0xFF)
            {
                continue;
            }

            var next = Transfer(op, pc, inState[pc]);
            outState[pc] = next;
            var fallsThrough = true;
            switch (op)
            {
                case 0x02:
                    Flow(pc + S16(pc + 1, pc + 2), next);
                    fallsThrough = false;
                    break;
                case 0x03:
                case 0x04:
                case 0x74:
                    Flow(pc + S16(pc + 1, pc + 2), next);
                    break;
                case 0x30:
                case 0x31:
                    Flow(pc + S16(pc + 3, pc + 4), next);
                    break;
                case 0x57:
                case 0x58:
                    for (var direction = 0; direction < 4; direction++)
                    {
                        Flow(pc + S16(pc + 1 + 2 * direction, pc + 2 + 2 * direction), next);
                    }

                    fallsThrough = false;
                    break;
                case 0x78:
                    AddMark(pc + 3);
                    Flow(pc + S16(pc + 1, pc + 2), next);
                    fallsThrough = false;
                    break;
                case 0x79:
                    AddMark(pc + 3);
                    Flow(pc + S16(pc + 1, pc + 2), next);
                    break;
                case 0x7B:
                case 0x7C:
                    AddMark(pc + 5);
                    Flow(pc + S16(pc + 3, pc + 4), next);
                    break;
                case 0x7D:
                    fallsThrough = false;
                    break;
                case 0x49:
                    Flow(entry, next);
                    fallsThrough = false;
                    break;
                case 0x4B:
                    Flow(entry, next);
                    break;
                case 0x40:
                    if (Byte(pc + 1) is >= 2 and <= 5)
                    {
                        spawned.Add((Byte(pc + 1), Byte(pc + 2) & 0x7F));
                    }

                    break;
            }

            // The return of a call reaches every mark (the walk pushes the marks once a return was seen).
            if (op is 0x7D or 0x7E or 0x7F or 0x80 or 0x81)
            {
                returnUsers.Add(pc);
                foreach (var mark in marks)
                {
                    Flow(mark, next);
                }
            }

            if (fallsThrough)
            {
                Flow(pc + info.Size, next);
            }
        }

        var sites = new List<CensusSite>();
        foreach (var pc in inState.Keys.OrderBy(p => p))
        {
            if (codes[pc] == 0x1A && pc + 2 < count && codes[pc + 2] is 0x1C or 0x1D)
            {
                foreach (var target in inState[pc].OrderBy(t => t.Entity, StringComparer.Ordinal))
                {
                    if (codes[pc + 1] >= target.SetCount)
                    {
                        sites.Add(new CensusSite(pc + 2, codes[pc + 1], target));
                    }
                }
            }
        }

        return new CensusResult(sites, failures.Distinct().ToList(), inState.Keys.ToHashSet(), spawned);
    }

    // -----------------------------------------------------------------------------------------
    // The corpus
    // -----------------------------------------------------------------------------------------

    private static CensusTarget? TargetOf(JsonElement props, SpriteRecordCatalog catalog)
    {
        if (!props.TryGetProperty("PrefabAssetId", out var prefabProperty)
            || !Guid.TryParse(prefabProperty.GetString(), out var prefabId)
            || !catalog.TryGet(prefabId, out var header)
            || header.AnimSets is null)
        {
            return null;
        }

        var entity = props.TryGetProperty("Index", out var entityIndex) ? $"{entityIndex.GetString()} {props.GetProperty("EntityName").GetString()}" : "?";
        return new CensusTarget(prefabId.ToString("D")[..8], entity, header.AnimSets.Count);
    }

    /// <summary>The hero, resolved the way <c>AlundraArcSupport</c> resolves its prefab: the entry named 'Alundra' of type entity of AssetInfos.json.</summary>
    private static CensusTarget HeroTarget(string root, SpriteRecordCatalog catalog)
    {
        using var infos = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "AssetInfos.json")));
        var entry = infos.RootElement.GetProperty("asset_infos").EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "Alundra" && e.GetProperty("asset_type").GetString() == "entity");
        var id = Guid.Parse(entry.GetProperty("id").GetString()!);
        Assert.True(catalog.TryGet(id, out var header) && header.AnimSets is not null, "the hero prefab has no sprite record with animation sets");
        return new CensusTarget(id.ToString("D")[..8], "hero", header.AnimSets!.Count);
    }

    /// <summary>The sites of one map; the failures (named) are appended to <paramref name="failures"/>.</summary>
    private static List<Site> SitesOfMap(string root, SpriteRecordCatalog catalog, CensusTarget hero, int map, List<string> failures)
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

            // 0x43 [n] with n below 0x80 searches the entities by record id: the object whose Index is n (the last one found, as the binary).
            var byIndex = new Dictionary<int, CensusTarget>();
            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
            {
                var props = obj.GetProperty("custom_properties");
                if (props.TryGetProperty("Index", out var indexProperty) && TargetOf(props, catalog) is { } target)
                {
                    byIndex[ReadInt(indexProperty)] = target;
                }
            }

            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
            {
                var props = obj.GetProperty("custom_properties");
                if (TargetOf(props, catalog) is not { } owner)
                {
                    continue;
                }

                var roots = new List<(char, int)>();
                foreach (var (property, slot) in SlotProperties)
                {
                    var index = ReadInt(props.GetProperty(property)) & 0x7F;
                    var table = document.TableFor(Slots.IndexOf(slot));
                    if (index != 0 && index < table.Length && table[index] != 0)
                    {
                        roots.Add((slot, index));
                    }
                }

                // The census walks the programs the way WalkMap does (the roots, then the roots of every 0x40 reached).
                var mine = new Dictionary<(char, int), IReadOnlySet<int>>();
                var done = new HashSet<(char, int)>();
                var todo = new Queue<(char Slot, int Index)>(roots);
                while (todo.Count > 0)
                {
                    var (slot, index) = todo.Dequeue();
                    if (!done.Add((slot, index)))
                    {
                        continue;
                    }

                    var result = Census(document.Codes, document.TableFor(Slots.IndexOf(slot))[index], owner, hero, n => byIndex.GetValueOrDefault(n));
                    if (result.Reached.Count > 0)
                    {
                        mine[(slot, index)] = result.Reached; // a program whose entry is outside the code reaches nothing, WalkMap lists nothing.
                    }

                    failures.AddRange(result.Failures.Select(f => $"map {map} entity {owner.Entity} program {slot}{index}: {f}"));
                    foreach (var site in result.Sites)
                    {
                        found.Add(new Site(map, site.Pc, site.Animation, site.Target.SetCount, site.Target.Prefab, site.Target.Entity));
                    }

                    foreach (var (otherSlot, otherIndex) in result.Spawned)
                    {
                        var table = document.TableFor(otherSlot);
                        if (!done.Contains((Slots[otherSlot], otherIndex)) && otherIndex < table.Length && table[otherIndex] != 0)
                        {
                            todo.Enqueue((Slots[otherSlot], otherIndex));
                        }
                    }
                }

                // The flow reaches exactly the instructions the audit's walk reaches, program by program.
                var walked = WalkMap(map, document, roots)
                    .GroupBy(r => (r.Site.Slot, r.Site.Program))
                    .ToDictionary(g => g.Key, g => g.Select(r => r.Site.Pc).ToHashSet());
                Assert.True(
                    walked.Keys.OrderBy(k => k).SequenceEqual(mine.Keys.OrderBy(k => k)),
                    $"map {map} entity {owner.Entity}: the census walked other programs than WalkMap");
                foreach (var (key, pcs) in walked)
                {
                    Assert.True(pcs.SetEquals(mine[key]), $"map {map} entity {owner.Entity} program {key}: the census reached other pcs than WalkMap");
                }
            }
        }

        return found;
    }

    private static string Show(IEnumerable<Site> sites) =>
        string.Join("; ", sites.Select(s => $"map {s.Map} wait@{s.Pc} 0x1A[{s.Animation}] of {s.SetCount} sets, prefab {s.Prefab}, entity {s.Entity}"));

    [Fact]
    public void M07_TheWaitsOnAnAnimationTheLogicEntityLacks_AreTheThreeFlamesOfTheLairOfNirude_FollowingTheRetarget()
    {
        var root = ProjectRootFinder.Find();
        var catalog = new SpriteRecordCatalog(root);
        var hero = HeroTarget(root, catalog);
        var failures = new List<string>();
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Maps", "world-index.json")));
        var sites = index.RootElement.EnumerateObject()
            .Select(p => int.Parse(p.Name))
            .OrderBy(m => m)
            .SelectMany(m => SitesOfMap(root, catalog, hero, m, failures))
            .ToList();

        Assert.True(failures.Count == 0, $"the census could not resolve {failures.Count} retarget(s): {string.Join("; ", failures)}");

        // E19.c2 (plan section 1.2) names the Flames of the lair of Nirude (prefab 0edffd14, 11 entities) at maps 35 @1228, 38 @850 and 39 @726: these are the
        // pcs of the wait opcode (the 0x1A is two bytes before), animation 9 of a bank of 9 sets (0..8). E19.m8 M8-b also counted map 240 @535 (Giles) and
        // map 292 @429 (Nestus), both asking animation 80; M8-d (O-E19-79) showed those two act on the HERO: 0x43 [0x81] (240 @529, 292 @423) makes the
        // hero the logic entity before the 0x1A, and the hero has 94 sets. Nothing reads past a table.
        var distinct = sites.Select(s => (s.Map, s.Pc)).Distinct().OrderBy(k => k).ToList();
        Assert.True(
            distinct.SequenceEqual(new[] { (35, 1228), (38, 850), (39, 726) }),
            $"expected the three Flames, measured {distinct.Count}: {Show(sites)}");
        Assert.All(sites, s =>
        {
            Assert.Equal("0edffd14", s.Prefab);
            Assert.Equal(9, s.Animation);
            Assert.Equal(9, s.SetCount);
        });
        Assert.Equal(11, sites.Count);
    }

    // -----------------------------------------------------------------------------------------
    // The census on made-up programs
    // -----------------------------------------------------------------------------------------

    private static readonly CensusTarget Owner = new("aaaaaaaa", "owner", 19);
    private static readonly CensusTarget Hero = new("bbbbbbbb", "hero", 94);
    private static readonly CensusTarget Small = new("cccccccc", "small", 4);

    private static CensusResult Run(int[] codes, Func<int, CensusTarget?>? byIndex = null) =>
        Census(codes, 0, Owner, Hero, byIndex ?? (_ => null));

    [Fact]
    public void Census_a_ARetargetToTheHero_ClearsTheSite()
    {
        var result = Run(new[] { 0x43, 0x81, 0x1A, 80, 0x1C, 1, 0xFF });

        Assert.Empty(result.Sites);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Census_b_WithoutARetarget_TheSiteStays()
    {
        var result = Run(new[] { 0x1A, 80, 0x1C, 1, 0xFF });

        var site = Assert.Single(result.Sites);
        Assert.Equal(new CensusSite(2, 80, Owner), site);
    }

    [Fact]
    public void Census_c_A0x43ToAnEntityOfFourSets_JudgesTheSiteAgainstItsFourSets()
    {
        var result = Run(new[] { 0x43, 0x05, 0x1A, 4, 0x1C, 1, 0xFF }, n => n == 5 ? Small : null);

        var site = Assert.Single(result.Sites);
        Assert.Equal(new CensusSite(4, 4, Small), site);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Census_d_A0x43OfAnUnresolvedSearch_IsANamedFailure()
    {
        var result = Run(new[] { 0x43, 0x83, 0x1A, 80, 0x1C, 1, 0xFF });

        var failure = Assert.Single(result.Failures);
        Assert.Contains("unresolved", failure);
        Assert.Contains("0x83", failure);
    }

    [Fact]
    public void Census_e_A0x43ToAnAbsentIndex_IsANamedFailure()
    {
        var result = Run(new[] { 0x43, 0x07, 0x1A, 1, 0x1C, 1, 0xFF }, n => n == 5 ? Small : null);

        var failure = Assert.Single(result.Failures);
        Assert.Contains("Index 7", failure);
    }

    [Fact]
    public void Census_f_AJumpThatBypassesThe0x43_KeepsTheSite_TheJoinIsTheUnion()
    {
        // pc 0: 0x03 +5 -> pc 5; pc 3: 0x43 [0x81]; pc 5: 0x1A [80]; pc 7: 0x1C. Reaching pc 5 by the jump keeps the owner among the targets.
        var result = Run(new[] { 0x03, 5, 0, 0x43, 0x81, 0x1A, 80, 0x1C, 1, 0xFF });

        var site = Assert.Single(result.Sites);
        Assert.Equal(new CensusSite(7, 80, Owner), site);
    }

    [Fact]
    public void Census_g_ARetargetThenABackToTheOwner_KeepsTheSite()
    {
        var result = Run(new[] { 0x43, 0x81, 0x43, 0x00, 0x1A, 80, 0x1C, 1, 0xFF }, n => n == 0 ? Owner : null);

        var site = Assert.Single(result.Sites);
        Assert.Equal(new CensusSite(6, 80, Owner), site);
    }
}
