#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f4 T1 (docs/plan-e20-heros.md section 1.20, "Revision au HEAD" point 3): the keys of the charged arrow (sprite type 7) of the oracle <c>docs/plan-e20-annexe/e20-f4f6/</c> read BY KEY (never copied): the frozen keys <c>bin/out/json/charged7_*.json</c> and the keys with
/// the step of the generator by image <c>f4/oracle/out/json/charged7_*_pf.json</c> (the suffix <c>_pf</c>), and the 12 starts of the wall <c>bin/out/sweep_wall_charged.txt</c>. The oracle releases the charge at tick R = 96 and the arrow is born at S = R + 9 = 105; the arena of the
/// tests (a hero at (100, 100) on a synthetic field, the pad of the oracle) does the same, so a tick of the oracle is a tick of the test.
/// </summary>
internal sealed class ChargedArrowOracle
{
    /// <summary>The tick at which the oracle releases the charge.</summary>
    internal const int Release = 96;

    /// <summary>The tick of the birth of the arrow in the oracle (and in the test bench: the offset is 0, measured by the T0 probe).</summary>
    internal const int Birth = 105;

    private static readonly Regex FxPattern = new(@"^fx\(id=(?<id>\d+),dir=(?<dir>-?\d+),x=(?<x>-?[\d.]+),y=(?<y>-?[\d.]+),z=(?<z>-?[\d.]+),v=\((?<vx>-?[\d.]+),(?<vy>-?[\d.]+),(?<vz>-?[\d.]+)\)\)$", RegexOptions.CultureInvariant);
    private static readonly Regex AttachPattern = new(@"^attach\(id=(?<id>\d+),target=(?<target>\w+),mode=(?<mode>-?\d+)\)$", RegexOptions.CultureInvariant);

    /// <summary>One effect of the oracle with the numbers as the oracle prints them (pixels with 3 decimals, speeds with 4).</summary>
    internal sealed record Effect(int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz)
    {
        /// <summary>The line the tests compare, with the tick given (the tick of the oracle, or the tick of the end of the frame).</summary>
        internal string Line(int tick) => $"t{tick} id={Id} dir={Direction} x={X} y={Y} z={Z} v=({Vx},{Vy},{Vz})";
    }

    private readonly List<JsonElement> _rows;

    /// <summary>The slot (as the key names it) that holds the arrow of type 7.</summary>
    internal string ArrowSlot { get; }

    private ChargedArrowOracle(List<JsonElement> rows, string arrowSlot)
    {
        _rows = rows;
        ArrowSlot = arrowSlot;
    }

    private static string Annex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f4f6", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-f4f6/{relative}' found above '{AppContext.BaseDirectory}'");
    }

    /// <summary>The key <paramref name="name"/> (<c>east_wall</c>, <c>grass</c>, ...): the frozen one, or the one with the step by image when <paramref name="stepByImage"/>.</summary>
    internal static ChargedArrowOracle Load(string name, bool stepByImage = false)
    {
        var path = stepByImage ? Annex($"f4/oracle/out/json/charged7_{name}_pf.json") : Annex($"bin/out/json/charged7_{name}.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = document.RootElement.EnumerateArray().Select(r => r.Clone()).ToList();
        foreach (var row in rows)
        {
            if (row.GetProperty("tick").GetInt32() < Release)
            {
                continue;
            }

            foreach (var slot in row.GetProperty("slots").EnumerateObject())
            {
                if (slot.Value.GetProperty("type68").GetInt32() == 7)
                {
                    return new ChargedArrowOracle(rows, slot.Name);
                }
            }
        }

        throw new InvalidOperationException($"the key {name} holds no entity of type 7");
    }

    /// <summary>The number of ticks of the key.</summary>
    internal int Ticks => _rows.Count;

    internal JsonElement Row(int tick) => _rows[tick - 1];

    /// <summary>The slot of the arrow at <paramref name="tick"/>, or null when the arrow is not in the table.</summary>
    internal JsonElement? Arrow(int tick) => Slot(tick, ArrowSlot);

    /// <summary>Another slot of the key (the target of <c>charged7_vs_target</c>), or null.</summary>
    internal JsonElement? Slot(int tick, string slot) => Row(tick).GetProperty("slots").TryGetProperty(slot, out var element) && element.ValueKind == JsonValueKind.Object ? element : null;

    /// <summary>The line of the arrow as the tests compare it: the status, the position in 16.16, the pixel of Z, the horizontal force and the wall flag, or "absent".</summary>
    internal string Line(int tick)
    {
        if (Arrow(tick) is not { } a)
        {
            return "absent";
        }

        int I(string key) => a.GetProperty(key).GetInt32();
        return $"st={I("status")} x={I("px")} y={I("py")} zpx={I("pz") >> 16} fx={I("fx")} adj={I("adj_13C")}";
    }

    /// <summary>The generator word at the head of the tick (<c>rng0</c>).</summary>
    internal uint Rng0(int tick) => Row(tick).GetProperty("rng0").GetUInt32();

    /// <summary>The draws of the tick (the key does not count the step of the end of the frame).</summary>
    internal int Draws(int tick) => Row(tick).GetProperty("draws").GetInt32();

    /// <summary>The effects <c>fx(id=...)</c> of the key with the identifier <paramref name="id"/>, in order, with the tick of the oracle.</summary>
    internal IReadOnlyList<Effect> Effects(int id)
    {
        var list = new List<Effect>();
        foreach (var row in _rows)
        {
            var tick = row.GetProperty("tick").GetInt32();
            foreach (var text in row.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!))
            {
                var m = FxPattern.Match(text);
                if (m.Success && int.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture) == id)
                {
                    list.Add(new Effect(tick, id, int.Parse(m.Groups["dir"].Value, CultureInfo.InvariantCulture), m.Groups["x"].Value, m.Groups["y"].Value, m.Groups["z"].Value, m.Groups["vx"].Value, m.Groups["vy"].Value, m.Groups["vz"].Value));
                }
            }
        }

        return list;
    }

    /// <summary>The ticks at which the key attaches the effect <paramref name="id"/> (<c>attach(id=...)</c>) with their mode.</summary>
    internal IReadOnlyList<(int Tick, int Mode)> Attached(int id)
        => _rows.SelectMany(row => row.GetProperty("effects").EnumerateArray().Select(e => (Tick: row.GetProperty("tick").GetInt32(), Match: AttachPattern.Match(e.GetString()!))))
            .Where(x => x.Match.Success && int.Parse(x.Match.Groups["id"].Value, CultureInfo.InvariantCulture) == id)
            .Select(x => (x.Tick, int.Parse(x.Match.Groups["mode"].Value, CultureInfo.InvariantCulture))).ToList();

    /// <summary>
    /// The sounds of the key (<c>snd(N)</c> of the journal) from the tick <paramref name="from"/> to <paramref name="to"/>, in order, WITHOUT the identifier 0 (no sound: the machine journals a <c>snd(0)</c> at the absolute ticks 10, 24 and 119 of every key of the folder,
    /// the 13 of them, whatever the arrow does or whether it exists, so it is not the arrow's; the DLL plays no sound 0).
    /// </summary>
    internal IReadOnlyList<(int Tick, int Sfx)> Sounds(int from, int to)
        => _rows.Where(r => r.GetProperty("tick").GetInt32() >= from && r.GetProperty("tick").GetInt32() <= to)
            .SelectMany(r => r.GetProperty("journal").EnumerateArray().Select(j => (Tick: r.GetProperty("tick").GetInt32(), Text: j.GetString()!)))
            .Where(j => j.Text.StartsWith("snd(", StringComparison.Ordinal) && j.Text != "snd(0)")
            .Select(j => (j.Tick, int.Parse(j.Text[4..^1], CultureInfo.InvariantCulture))).ToList();

    /// <summary>The 12 starts of the wall (<c>sweep_wall_charged.txt</c>): for the hero's x0 (96 to 107) the tick of the flag and the tick of the status 4, both relative to S.</summary>
    internal static IReadOnlyDictionary<int, (int Flag, int Status4)> WallStarts()
    {
        var pattern = new Regex(@"^\s*hero x0=(?<x0>\d+) : S=105 ForceAdjusted at S\+(?<flag>\d+) \(x=349\.000\), status 4 at S\+(?<st4>\d+)$", RegexOptions.CultureInvariant);
        var starts = new Dictionary<int, (int, int)>();
        foreach (var line in File.ReadLines(Annex("bin/out/sweep_wall_charged.txt")))
        {
            var m = pattern.Match(line);
            if (m.Success)
            {
                starts[int.Parse(m.Groups["x0"].Value, CultureInfo.InvariantCulture)] = (int.Parse(m.Groups["flag"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["st4"].Value, CultureInfo.InvariantCulture));
            }
        }

        return starts;
    }
}
