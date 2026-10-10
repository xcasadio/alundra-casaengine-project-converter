#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Scripts;

namespace Alundra.Tests;

/// <summary>One creation of an effect by the oracle's real <c>0x8003BDD8</c> (the hold spark or an effect of the burst): the effect, whether it was created, and the fields it holds at the end of the logic of the tick.</summary>
/// <param name="SlotIndex">E20.f1b: the index of the slot the binary's pool gave it (<c>(slot - 0x80138608) / 0x80</c>), -1 when not created.</param>
/// <param name="Mode">E20.f1b: the slot's <c>+0x34</c> (1 for the aura attached to the hero), 0 otherwise.</param>
/// <param name="DepthOffset">E20.f1b: the slot's <c>+0x60</c>.</param>
/// <param name="OffsetX">E20.f1b: the slot's <c>+0x48</c>.</param>
/// <param name="OffsetY">E20.f1b: the slot's <c>+0x4C</c>.</param>
/// <param name="OffsetZ">E20.f1b: the slot's <c>+0x50</c>.</param>
internal sealed record ChargeCall(int Effect, bool Created, uint RngEntry, int X, int Y, int Z, int ForceX, int ForceY, int SlotIndex = -1, int Mode = 0, int DepthOffset = 0, int OffsetX = 0, int OffsetY = 0, int OffsetZ = 0);

/// <summary>One call of the shot (<c>0x80039F58</c>) the oracle journalled: type, position and direction, in the binary's frame of Z.</summary>
internal sealed record ChargeSpawn(int Type, int X, int Y, int Z, int Direction);

/// <summary>One row of an E20.f1a charge probe (<c>docs/plan-e20-annexe/e20-f1/oracle/out/C*.txt|json</c> and <c>out_events/</c>): the state AFTER one tick of the real <c>MovePlayer</c> of <c>ALUN_CD.EXE</c> (France).</summary>
/// <param name="Tick">The 1-based tick.</param>
/// <param name="Hold">The pad word held on that tick.</param>
/// <param name="Charge">The charge counter <c>0x80126FFC</c> after the tick.</param>
/// <param name="FrameCounterIn">The counter <c>+0x1C</c> as <c>MovePlayer</c> saw it (before the counters pass of the tick).</param>
/// <param name="RngBefore">The generator word at the head of the tick (the oracle never advances it per frame).</param>
/// <param name="RngAfter">The generator word after the passes of the tick.</param>
/// <param name="AuraHandle">The handle of the aura (<c>0x80126FF8</c>) after the tick, 0 when none.</param>
/// <param name="Events">The ordered events of the tick: <c>c&lt;id&gt;</c> a creation of effect id, <c>s&lt;id&gt;</c> a sound (id 0: "no sound"), <c>x:spawn</c> a shot.</param>
/// <param name="Calls">The creations of the tick with their fields, in order (the aura's included).</param>
internal sealed record ChargeRow(
    int Tick, uint Hold, uint AnimTarget, uint AnimCurrent, int Charge, long RecPtr, int RecCnt, int FrameCounterIn, uint RngBefore, uint RngAfter, long AuraHandle,
    IReadOnlyList<string> Events, IReadOnlyList<ChargeCall> Calls, IReadOnlyList<ChargeSpawn> Spawns)
{
    /// <summary>The sounds asked on the tick, in the binary's order; a call with the id 0 is the function with "no sound", never a sound.</summary>
    public IEnumerable<int> Sounds => Events.Where(e => e[0] == 's').Select(e => int.Parse(e[1..], CultureInfo.InvariantCulture)).Where(id => id > 0);

    /// <summary>The creations of the effect 16 (the hold spark and the burst) that succeeded, in order: what the DLL creates (the aura, effect 3, is the stub of E20.f1a).</summary>
    public IEnumerable<ChargeCall> ChargeEffects => Calls.Where(c => c.Effect == AlundraNativeEffects.ChargeEffect && c.Created);

    /// <summary>The hold sparks of the tick (the creations of the effect 16 before the last 16 when there are 16 or more, as <c>probe_ext.check_effects</c> splits them).</summary>
    public IEnumerable<ChargeCall> Sparks
    {
        get
        {
            var all = Calls.Where(c => c.Effect == AlundraNativeEffects.ChargeEffect).ToList();
            return all.Count >= 16 ? all.Take(all.Count - 16) : all;
        }
    }
}

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
