#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>The probes of the charge and the release (<c>C2-steel</c>, <c>C6-william</c>, <c>C10-crusher</c>, <c>C14-ice</c>, <c>C16-fire</c>): Square held from tick 1, then released, one tick per row.</summary>
internal static class ChargeOracle
{
    private static readonly Regex RowLine = new(
        @"^t\s*(\d+) hold=([0-9A-F]+) anim_t=([0-9A-F]+) anim_c=([0-9A-F]+) fx=\s*(-?\d+) fy=\s*(-?\d+) fz=\s*(-?\d+) pz=\s*(-?\d+) gnd=(-?\d+) chg=\s*(-?\d+) ptr=([0-9A-F]+) cnt=\s*(-?\d+) hc=(\d+) b0=(\d+) fc=(\d+)>(\d+) rng=([0-9A-F]+)>([0-9A-F]+)>([0-9A-F]+) aura=(\S+)",
        RegexOptions.Compiled);

    /// <summary>The base of the binary's effect pool (<c>0x80138608</c>) and the size of a slot (<c>0x80</c>).</summary>
    private const long PoolBase = 0x80138608;

    private const long PoolSlotSize = 0x80;

    private static readonly Regex SpawnCall = new(@"spawn\(owner=[0-9A-F]+,table=\d+,type=(\d+),x=(-?\d+),y=(-?\d+),z=(-?\d+),dir=0x([0-9A-F]+)\)", RegexOptions.Compiled);

    /// <summary>The folder of the oracle (<c>docs/plan-e20-annexe/e20-f1/oracle</c>), found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string Folder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f1", "oracle");
            if (Directory.Exists(Path.Combine(candidate, "out")) && Directory.Exists(Path.Combine(candidate, "out_events")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-f1/oracle' directory (with out/ and out_events/) found above '{AppContext.BaseDirectory}': this test needs the charge oracle");
    }

    /// <summary>Loads a probe: the rows of <c>out/&lt;name&gt;.txt</c>, the creations of <c>out/&lt;name&gt;.json</c> and the ordered events of <c>out_events/&lt;name&gt;.events.json</c>.</summary>
    public static IReadOnlyList<ChargeRow> Load(string name)
    {
        var folder = Folder();
        var lines = File.ReadAllLines(Path.Combine(folder, "out", name + ".txt")).Where(l => l.StartsWith('t')).ToList();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "out", name + ".json")));
        using var events = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "out_events", name + ".events.json")));
        var jsonRows = json.RootElement.EnumerateArray().ToList();
        var eventRows = events.RootElement.EnumerateArray().ToList();
        if (lines.Count != jsonRows.Count || lines.Count != eventRows.Count)
        {
            throw new InvalidDataException($"{name}: the three files of the probe do not have the same number of ticks ({lines.Count}, {jsonRows.Count}, {eventRows.Count})");
        }

        var rows = new List<ChargeRow>();
        for (var i = 0; i < lines.Count; i++)
        {
            var m = RowLine.Match(lines[i]);
            if (!m.Success)
            {
                throw new InvalidDataException($"{name}: a row of the probe does not parse: {lines[i]}");
            }

            var calls = new List<ChargeCall>();
            foreach (var c in jsonRows[i].GetProperty("calls").EnumerateArray())
            {
                var created = c.GetProperty("slot").GetInt64() != 0;
                var f = c.GetProperty("f");
                calls.Add(new ChargeCall(
                    c.GetProperty("args")[1].GetInt32(), created, (uint)c.GetProperty("rng_entry").GetInt64(),
                    created ? f.GetProperty("x").GetInt32() : 0, created ? f.GetProperty("y").GetInt32() : 0, created ? f.GetProperty("z").GetInt32() : 0,
                    created ? f.GetProperty("fx").GetInt32() : 0, created ? f.GetProperty("fy").GetInt32() : 0,
                    created ? (int)((c.GetProperty("slot").GetInt64() - PoolBase) / PoolSlotSize) : -1,
                    created ? f.GetProperty("s34").GetInt32() : 0, created ? f.GetProperty("s60").GetInt32() : 0,
                    created ? f.GetProperty("s48").GetInt32() : 0, created ? f.GetProperty("s4c").GetInt32() : 0, created ? f.GetProperty("s50").GetInt32() : 0));
            }

            var spawns = SpawnCall.Matches(lines[i]).Select(s => new ChargeSpawn(
                int.Parse(s.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(s.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(s.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(s.Groups[4].Value, CultureInfo.InvariantCulture), int.Parse(s.Groups[5].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))).ToList();
            var aura = m.Groups[20].Value;
            rows.Add(new ChargeRow(
                Tick: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                Hold: uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                AnimTarget: uint.Parse(m.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                AnimCurrent: uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                Charge: int.Parse(m.Groups[10].Value, CultureInfo.InvariantCulture),
                RecPtr: long.Parse(m.Groups[11].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                RecCnt: int.Parse(m.Groups[12].Value, CultureInfo.InvariantCulture),
                FrameCounterIn: int.Parse(m.Groups[15].Value, CultureInfo.InvariantCulture),
                RngBefore: uint.Parse(m.Groups[17].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                RngAfter: uint.Parse(m.Groups[18].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                AuraHandle: aura.StartsWith('-') ? 0 : long.Parse(aura[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                Events: eventRows[i].GetProperty("events").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList(),
                Calls: calls,
                Spawns: spawns));
        }

        return rows;
    }
}
