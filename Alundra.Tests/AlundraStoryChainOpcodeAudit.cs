#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;

namespace Alundra.Tests;

/// <summary>A program site: the map, the slot letter (A to F), the program index of that slot (<c>index &amp; 0x7F</c>, the key
/// of the program table) and the pc, an index into the map's <c>Codes</c>. Two programs can share a pc (they share code), so the
/// program is part of the key.</summary>
internal readonly record struct ProgramSite(int Map, char Slot, int Program, int Pc);

/// <summary>One instruction the walk reached.</summary>
internal readonly record struct ReachedSite(ProgramSite Site, int Opcode);

/// <summary>One line of the closed list (<c>Data/story-chain-skipped-opcodes.tsv</c>): the level is S (scene), A (ambient), O
/// (optional) or X (off the chain).</summary>
internal sealed record ListedSite(ProgramSite Site, int Opcode, string Class, string Slice, char Tier);

/// <summary>
/// E19.e E1 (docs/plan-e19-opcodes.md §1.2i): the static audit of the opcodes the interpreter skips by their size, on the programs
/// of the 30 maps of the story chain. Three parts, each testable on its own:
/// <list type="bullet">
/// <item><description><see cref="IsPorted"/>, the oracle: the real runner, not a hand-written mirror;</description></item>
/// <item><description><see cref="WalkMap"/>, a port of the discovery's control-flow walk (a superset: both branches of every
/// conditional);</description></item>
/// <item><description>the rules (<see cref="Rule1UnlistedSites"/> to <see cref="Rule5UnlistedArcSites"/>), plain functions on
/// plain data so they are tested on made-up input, then applied to the corpus.</description></item>
/// </list>
/// </summary>
internal static class AlundraStoryChainOpcodeAudit
{
    /// <summary>The 30 maps of the chain, outside the combat maps (the plan's order).</summary>
    public static readonly int[] ChainMaps =
    {
        389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181,
        182, 135, 183, 184, 185,
    };

    /// <summary>The seven combat maps, exempt (E14 owns them).</summary>
    public static readonly int[] ExemptCombatMaps = { 14, 15, 44, 115, 116, 117, 362 };

    /// <summary>The fifteen chain maps with no skipped site (the 392 joined them with E19.k1, its one site being the camera sway 0x8E;
    /// the 165, 172, 180 and 182 with E19.l1, whose only sites were the directional branch 0x58).</summary>
    public static readonly int[] MapsWithoutSkippedSite = { 165, 170, 171, 172, 173, 175, 177, 180, 182, 184, 389, 390, 392, 416, 478 };

    /// <summary>Wait opcodes (rule 3): an instruction that waits must never be skipped.</summary>
    public static readonly int[] WaitOpcodes = { 0x20, 0x21, 0x22, 0x23, 0x26, 0x47, 0x48, 0x5F, 0x9F };

    /// <summary>The closed set of rule 4: the predicates and branches that write <c>Result</c> or jump.</summary>
    public static readonly int[] PredicateOpcodes = { 0x52, 0x58, 0x82, 0x84, 0x87, 0x95, 0x99, 0x9A };

    /// <summary>The two known level-S sites of rule 4 (the two 164 sites of the directional branch 0x58 left with E19.l1).</summary>
    public static readonly ProgramSite[] KnownScenePredicateSites =
    {
        new(179, 'B', 1, 183), new(10, 'C', 75, 6418),
    };

    private const string SlotLetters = "ABCDEF";

    // -----------------------------------------------------------------------------------------
    // Oracle
    // -----------------------------------------------------------------------------------------

    private static readonly Lazy<bool[]> PortedTable = new(BuildPortedTable);

    /// <summary>True when the real runner does not skip (by size) and does not terminate (for want of a size) at this opcode: the
    /// program <c>[op, 0..., 0xFF]</c> is run on a bare host and its first instruction is read off the trace. 0x00 and 0xFF are the
    /// loop's own and count as ported. An opcode whose handler needs a host that is missing here throws or degrades: it is
    /// ported too.</summary>
    public static bool IsPorted(int opcode) => PortedTable.Value[opcode & 0xFF];

    private static bool[] BuildPortedTable()
    {
        var table = new bool[256];
        for (var op = 0; op < 256; op++)
        {
            table[op] = ProbeOpcode(op);
        }

        return table;
    }

    private static bool ProbeOpcode(int op)
    {
        if (op is 0x00 or 0xFF)
        {
            return true;
        }

        EventOpcodeSizeTable.Entries.TryGetValue((byte)op, out var entry);
        var size = Math.Max(entry?.Size ?? 0, 1);
        var codes = new int[size + 1];
        codes[0] = op;
        codes[size] = 0xFF;
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new int[6], Codes = codes };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        EventTraceRecord? first = null;
        runner.TraceSink = record =>
        {
            if (first == null && record.CodeIndex == 0)
            {
                first = record;
            }
        };

        try
        {
            runner.RunOneScriptCall(new AlundraEntityScriptProxy(), new EventProgramState { Codes = document.CodesAsBytes() });
        }
        catch (Exception)
        {
            return true; // the handler ran far enough to need a host: it exists.
        }

        return first == null || first.Value.Kind is not (EventTraceKind.UnknownSkipped or EventTraceKind.UnknownNoSizeTerminated);
    }

    // -----------------------------------------------------------------------------------------
    // Walk (port of the discovery's cfg.py)
    // -----------------------------------------------------------------------------------------

    /// <summary>Every instruction reachable from the given roots (slot letter, program index), the roots of every <c>0x40</c> reached
    /// included, following the jumps the runner follows and both branches of every conditional. A program stops at 0xFF, at a
    /// zero or unknown size, or when it falls on code already seen.</summary>
    public static IReadOnlyList<ReachedSite> WalkMap(int map, EventProgramDocument document, IEnumerable<(char Slot, int Index)> roots)
    {
        var reached = new List<ReachedSite>();
        var done = new HashSet<(char, int)>();
        var todo = new Queue<(char Slot, int Index)>(roots);
        while (todo.Count > 0)
        {
            var (slot, index) = todo.Dequeue();
            if (!done.Add((slot, index)))
            {
                continue;
            }

            var extra = new HashSet<(char, int)>();
            var seen = WalkProgram(document, document.TableFor(SlotLetters.IndexOf(slot))[index], extra);
            foreach (var (pc, op) in seen.OrderBy(pair => pair.Key))
            {
                reached.Add(new ReachedSite(new ProgramSite(map, slot, index, pc), op));
            }

            foreach (var (otherSlot, otherIndex) in extra)
            {
                var table = document.TableFor(SlotLetters.IndexOf(otherSlot));
                if (!done.Contains((otherSlot, otherIndex)) && otherIndex < table.Length && table[otherIndex] != 0)
                {
                    todo.Enqueue((otherSlot, otherIndex));
                }
            }
        }

        return reached;
    }

    private static Dictionary<int, int> WalkProgram(EventProgramDocument document, int entry, HashSet<(char, int)> extraRoots)
    {
        var codes = document.Codes;
        var count = codes.Length;
        var seen = new Dictionary<int, int>();
        var marks = new HashSet<int>();
        var returnUsers = new HashSet<int>();
        var work = new Stack<int>();
        work.Push(entry);
        int Byte(int index) => index >= 0 && index < count ? codes[index] : 0;
        int S16(int lo, int hi) => (short)(Byte(lo) | (Byte(hi) << 8));

        while (true)
        {
            while (work.Count > 0)
            {
                var pc = work.Pop();
                while (pc >= 0 && pc < count && !seen.ContainsKey(pc))
                {
                    var op = codes[pc];
                    seen[pc] = op;
                    if (!EventOpcodeSizeTable.Entries.TryGetValue((byte)op, out var entryInfo) || entryInfo.Size == 0 || op == 0xFF)
                    {
                        break;
                    }

                    var size = entryInfo.Size;
                    var jumped = false;
                    var stop = false;
                    switch (op)
                    {
                        case 0x02:
                            pc += S16(pc + 1, pc + 2);
                            jumped = true;
                            break;
                        case 0x03:
                        case 0x04:
                            work.Push(pc + S16(pc + 1, pc + 2));
                            break;
                        case 0x30:
                        case 0x31:
                            work.Push(pc + S16(pc + 3, pc + 4));
                            break;
                        case 0x74:
                            work.Push(pc + S16(pc + 1, pc + 2));
                            break;
                        case 0x57:
                        case 0x58:
                            // E19.l1: the directional branch takes one of four targets (the offset read at 1 + 2d) and never
                            // falls through to pc + 9.
                            for (var direction = 0; direction < 4; direction++)
                            {
                                work.Push(pc + S16(pc + 1 + 2 * direction, pc + 2 + 2 * direction));
                            }

                            stop = true;
                            break;
                        case 0x78:
                            marks.Add(pc + 3);
                            pc += S16(pc + 1, pc + 2);
                            jumped = true;
                            break;
                        case 0x79:
                            marks.Add(pc + 3);
                            work.Push(pc + S16(pc + 1, pc + 2));
                            break;
                        case 0x7B:
                        case 0x7C:
                            marks.Add(pc + 5);
                            work.Push(pc + S16(pc + 3, pc + 4));
                            break;
                        case 0x7D:
                            returnUsers.Add(pc);
                            stop = true;
                            break;
                        case 0x7E:
                        case 0x7F:
                        case 0x80:
                        case 0x81:
                            returnUsers.Add(pc);
                            break;
                        case 0x49:
                            pc = entry;
                            jumped = true;
                            break;
                        case 0x4B:
                            work.Push(entry);
                            break;
                        case 0x40:
                            if (Byte(pc + 1) is >= 2 and <= 5)
                            {
                                extraRoots.Add((SlotLetters[Byte(pc + 1)], Byte(pc + 2) & 0x7F));
                            }

                            break;
                    }

                    if (stop)
                    {
                        break;
                    }

                    if (!jumped)
                    {
                        pc += size;
                    }
                }
            }

            if (returnUsers.Count > 0)
            {
                var fresh = marks.Where(mark => !seen.ContainsKey(mark)).ToList();
                if (fresh.Count > 0)
                {
                    foreach (var mark in fresh)
                    {
                        work.Push(mark);
                    }

                    continue;
                }
            }

            break;
        }

        return seen;
    }

    /// <summary>The roots of a map: the B program of every map event, then the A, C, D, E and F programs of every record
    /// (<c>index &amp; 0x7F</c>, nonzero, inside its table, pointing at code).</summary>
    public static IReadOnlyList<(char Slot, int Index)> ReadRoots(string tileMapPath, EventProgramDocument document)
    {
        var roots = new List<(char, int)>();
        using var json = JsonDocument.Parse(File.ReadAllText(tileMapPath));
        foreach (var layer in json.RootElement.GetProperty("object_layers").EnumerateArray())
        {
            var layerName = layer.GetProperty("name").GetString();
            if (layerName == "MapEvents")
            {
                foreach (var obj in layer.GetProperty("objects").EnumerateArray())
                {
                    AddRoot(roots, document, 'B', obj.GetProperty("custom_properties").GetProperty("EventCodesBIndex"));
                }
            }
        }

        foreach (var layer in json.RootElement.GetProperty("object_layers").EnumerateArray())
        {
            if (layer.GetProperty("name").GetString() != "Entities")
            {
                continue;
            }

            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
            {
                var props = obj.GetProperty("custom_properties");
                AddRoot(roots, document, 'A', props.GetProperty("EventCodesA_LoadIndex"));
                AddRoot(roots, document, 'C', props.GetProperty("EventCodesC_TickIndex"));
                AddRoot(roots, document, 'D', props.GetProperty("EventCodesD_TouchIndex"));
                AddRoot(roots, document, 'E', props.GetProperty("EventCodesE_DeactivateIndex"));
                AddRoot(roots, document, 'F', props.GetProperty("EventCodesF_InteractIndex"));
            }
        }

        return roots;
    }

    private static void AddRoot(List<(char, int)> roots, EventProgramDocument document, char slot, JsonElement value)
    {
        var raw = value.ValueKind == JsonValueKind.String ? int.Parse(value.GetString()!) : value.GetInt32();
        var index = raw & 0x7F;
        var table = document.TableFor(SlotLetters.IndexOf(slot));
        if (index != 0 && index < table.Length && table[index] != 0)
        {
            roots.Add((slot, index));
        }
    }

    /// <summary>Loads a chain map from the export: its program document (through the production loader) and its tile map file.</summary>
    public static (EventProgramDocument Document, string TileMapPath) LoadMap(string projectRoot, int map)
    {
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "Maps", "world-index.json")));
        var relative = index.RootElement.GetProperty(map.ToString()).GetString()!;
        var folder = Path.GetDirectoryName(Path.Combine(projectRoot, relative))!;
        var worldName = Path.GetFileNameWithoutExtension(relative);
        var document = MapEventProgramLoader.Load(projectRoot, worldName)
            ?? throw new InvalidOperationException($"map {map} ('{worldName}'): the event programs did not load.");
        var tileMap = Directory.GetFiles(Path.Combine(folder, "tilemap"), "*.tileMap").Single();
        return (document, tileMap);
    }

    /// <summary>Every instruction reachable on a chain map.</summary>
    public static IReadOnlyList<ReachedSite> WalkExportedMap(string projectRoot, int map)
    {
        var (document, tileMap) = LoadMap(projectRoot, map);
        return WalkMap(map, document, ReadRoots(tileMap, document));
    }

    // -----------------------------------------------------------------------------------------
    // The closed list
    // -----------------------------------------------------------------------------------------

    /// <summary>The versioned list, found above the test binaries (<c>Alundra.Tests/Data/story-chain-skipped-opcodes.tsv</c>).</summary>
    public static string FindListPath()
    {
        const string relative = "Data/story-chain-skipped-opcodes.tsv";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var candidate in new[] { Path.Combine(directory.FullName, relative), Path.Combine(directory.FullName, "Alundra.Tests", relative) })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"'{relative}' not found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>Parses the list: a header line, then map, slot, program, label, pc, opcode, name, class, slice, level (and the
    /// discovery's last column, ignored).</summary>
    public static IReadOnlyList<ListedSite> ParseList(IEnumerable<string> lines)
    {
        var sites = new List<ListedSite>();
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
        {
            var cells = line.Split('\t');
            sites.Add(new ListedSite(
                new ProgramSite(int.Parse(cells[0]), cells[1][0], int.Parse(cells[2]), int.Parse(cells[4])),
                Convert.ToInt32(cells[5], 16),
                cells[7],
                cells[8],
                cells[9][0]));
        }

        return sites;
    }

    // -----------------------------------------------------------------------------------------
    // Rules
    // -----------------------------------------------------------------------------------------

    /// <summary>Rule 1: every site reached and skipped is a line of the list (a missing line, or a line for another opcode).</summary>
    public static IReadOnlyList<ReachedSite> Rule1UnlistedSites(IEnumerable<ReachedSite> skipped, IEnumerable<ListedSite> list)
    {
        var listed = list.Select(l => (l.Site, l.Opcode)).ToHashSet();
        return skipped.Where(s => !listed.Contains((s.Site, s.Opcode))).ToList();
    }

    /// <summary>Rule 2: every line of the list is a site reached and skipped (a stale line: the opcode became ported, or the site
    /// became unreachable).</summary>
    public static IReadOnlyList<ListedSite> Rule2StaleLines(IEnumerable<ListedSite> list, IEnumerable<ReachedSite> skipped)
    {
        var reached = skipped.Select(s => (s.Site, s.Opcode)).ToHashSet();
        return list.Where(l => !reached.Contains((l.Site, l.Opcode))).ToList();
    }

    /// <summary>Rule 3: no instruction that waits is skipped, and none has a zero or unknown size, at any level. Takes every
    /// reached site and the oracle.</summary>
    public static IReadOnlyList<ReachedSite> Rule3WaitsOrUnsized(IEnumerable<ReachedSite> reached, Func<int, bool> isPorted)
    {
        return reached.Where(r => !isPorted(r.Opcode)
            && (Array.IndexOf(WaitOpcodes, r.Opcode) >= 0
                || !EventOpcodeSizeTable.Entries.TryGetValue((byte)r.Opcode, out var entry)
                || entry.Size <= 0)).ToList();
    }

    /// <summary>Rule 4: among the level-S lines, the predicates and branches of <see cref="PredicateOpcodes"/> are only at the
    /// two known sites.</summary>
    public static IReadOnlyList<ListedSite> Rule4ScenePredicates(IEnumerable<ListedSite> list)
    {
        return list.Where(l => l.Tier == 'S'
            && Array.IndexOf(PredicateOpcodes, l.Opcode) >= 0
            && Array.IndexOf(KnownScenePredicateSites, l.Site) < 0).ToList();
    }

    /// <summary>Rule 5, for the arcs that wire it: every <see cref="EventTraceKind.UnknownSkipped"/> entry of an arc trace, on the
    /// arc's map, brought back to (map, slot, program index found by its start, pc), is a line of the list. The two other
    /// failure kinds are not this rule's: they are ignored here. A start shared by several program indexes of the slot matches
    /// any of them.</summary>
    public static IReadOnlyList<ArcInstruction> Rule5UnlistedArcSites(
        int map, EventProgramDocument document, IEnumerable<ArcInstruction> trace, IEnumerable<ListedSite> list)
    {
        var listed = list.Select(l => (l.Site, l.Opcode)).ToHashSet();
        var unlisted = new List<ArcInstruction>();
        foreach (var entry in trace.Where(t => t.Kind == EventTraceKind.UnknownSkipped))
        {
            var table = document.TableFor(entry.Slot);
            var found = false;
            for (var index = 0; index < table.Length && !found; index++)
            {
                found = index != 0 && table[index] == entry.ProgramStart
                    && listed.Contains((new ProgramSite(map, SlotLetters[entry.Slot], index, entry.Pc), entry.Opcode));
            }

            if (!found)
            {
                unlisted.Add(entry);
            }
        }

        return unlisted;
    }

    public static string Describe(ProgramSite site) => $"map {site.Map} {site.Slot}[{site.Program}] @{site.Pc}";
}
