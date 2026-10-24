#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5c T1 (docs/plan-e20-heros.md section 1.24.6, D-G5C-3): the sites of the stop of a sound in the export, read the way the discovery read them (<c>docs/plan-e20-annexe/e20-g5/g5c-disc/sites_stop.py</c>: every program of the six tables of every
/// <c>Maps/*/*/events/*.events.json</c>, swept linearly from its start to the next program start of any table, the sizes those of <see cref="EventOpcodeSizeTable"/>, a program swept up to the first instruction that cannot be sized). The census is
/// 392 instructions <c>0xBE</c> on 119 maps (12 of them on the 4 maps of the story chain that carry one: 10, 135, 178 and 391) and no <c>0x75</c>. Each of the 392 sites is then run through the real <see cref="EventProgramRunner"/> with a recording
/// sound player: it asks the STOP of the id its two bytes give, never a play, and the instruction is 3 bytes long - so the census is the list of the sounds the port now stops, not only a count of the data.
/// </summary>
public sealed class StopSoundSitesTests
{
    /// <summary>The 30 maps of the story chain of the discovery (<c>CHAIN</c> of <c>sites_stop.py</c>).</summary>
    private static readonly HashSet<int> ChainMaps = new()
    {
        389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185,
    };

    private readonly record struct Site(int Map, char Slot, int Pc, int Op, int Id);

    private static string FindMapsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "Maps");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"StopSoundSitesTests: no 'alundra-project/Maps' found above '{AppContext.BaseDirectory}' - the census needs the real export.");
    }

    private static List<Site> ReadSites()
    {
        var sites = new List<Site>();
        foreach (var file in Directory.EnumerateFiles(FindMapsFolder(), "*.events.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            var map = int.Parse(name[(name.LastIndexOf('-') + 1)..]);
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            var codes = root.GetProperty("Codes").EnumerateArray().Select(e => e.GetInt32()).ToArray();

            var programs = new List<(char Slot, int Start)>();
            foreach (var slot in "ABCDEF")
            {
                foreach (var start in root.GetProperty($"EventCodes{slot}Table").EnumerateArray())
                {
                    programs.Add((slot, start.GetInt32()));
                }
            }

            var starts = programs.Select(p => p.Start).Append(codes.Length).Distinct().OrderBy(s => s).ToList();
            var seen = new HashSet<(char, int)>();
            foreach (var (slot, start) in programs)
            {
                if (!seen.Add((slot, start)))
                {
                    continue;
                }

                var next = starts.FirstOrDefault(s => s > start, codes.Length);
                var pc = start;
                if (start < 0 || start >= codes.Length)
                {
                    continue; // a table entry outside Codes: nothing to sweep
                }

                while (pc < next && pc < codes.Length)
                {
                    var op = codes[pc];
                    var size = EventOpcodeSizeTable.Entries.TryGetValue((byte)op, out var entry) ? entry.Size : 0;
                    if (size < 1 || pc + size > codes.Length)
                    {
                        break;
                    }

                    if (op == 0xBE)
                    {
                        sites.Add(new Site(map, slot, pc, op, codes[pc + 1] | (codes[pc + 2] << 8)));
                    }
                    else if (op == 0x75)
                    {
                        sites.Add(new Site(map, slot, pc, op, codes[pc + 1]));
                    }

                    pc += size;
                }
            }
        }

        return sites;
    }

    [Fact]
    public void TheExport_Has392StopSites0xBE_On119Maps_12OnTheStoryChain_AndNone0x75()
    {
        var sites = ReadSites();

        var stops16 = sites.Where(s => s.Op == 0xBE).ToList();
        Assert.Equal(392, stops16.Count);
        Assert.Equal(119, stops16.Select(s => s.Map).Distinct().Count());
        var onTheChain = stops16.Where(s => ChainMaps.Contains(s.Map)).ToList();
        Assert.Equal(12, onTheChain.Count);
        Assert.Equal(new[] { 10, 135, 178, 391 }, onTheChain.Select(s => s.Map).Distinct().OrderBy(m => m).ToArray());
        Assert.Equal(43, stops16.Select(s => s.Id).Distinct().Count());
        Assert.Equal(204, stops16.Count(s => s.Id == 25)); // the stone scraping: the id of the pillars, statues, blocks and buttons
        Assert.DoesNotContain(sites, s => s.Op == 0x75); // the byte-id stop is ported with it but no program of the export uses it
    }

    [Fact]
    public void EveryOne_Of_The392Sites_RunsThroughTheRealRunnerAsTheStopOfItsId_NeverAPlay_AndAdvancesThreeBytes()
    {
        var sites = ReadSites().Where(s => s.Op == 0xBE).ToList();
        Assert.Equal(392, sites.Count); // the positive: the loop below does run on all of them

        foreach (var site in sites)
        {
            var sound = new RecordingSoundPlayer();
            var context = new PickupHost { Sound = sound };
            var document = new EventProgramDocument
            {
                MapIndex = site.Map,
                EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
                Codes = new[] { 0xBE, site.Id & 0xFF, site.Id >> 8, 0xFF },
            };
            var runner = new EventProgramRunner(document, new GameState(), context);
            var state = new EventProgramState { Codes = document.CodesAsBytes() };

            runner.RunOneScriptCall(new EntityScriptProxy(), state);

            Assert.True(state.CodeIndex == 3, $"map {site.Map} {site.Slot}@{site.Pc}: the instruction is 3 bytes long");
            Assert.True(sound.StopRequests.SequenceEqual(new[] { site.Id }), $"map {site.Map} {site.Slot}@{site.Pc}: the stop of the sound {site.Id}");
            Assert.True(sound.Requests.Count == 0, $"map {site.Map} {site.Slot}@{site.Pc}: no play of the sound {site.Id}");
        }

        Assert.Contains(sites, s => s.Id > 255); // some ids need the high byte (353, 341, 349, 360, 316...): the 16-bit derivation is exercised, not only the 8-bit one
    }
}
