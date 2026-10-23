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
/// E20.f5 T1 (docs/plan-e20-heros.md section 1.21.1, "Revision au HEAD" point 3): the keys of the normal arrows (sprite types 3 and 4) of the oracle <c>docs/plan-e20-annexe/e20-f4f6/</c> read BY KEY (never copied): the frozen keys <c>bin/out/json/arrow*.json</c>, the new keys of E20.f5
/// <c>f5/oracle/out/json/arrow*.json</c> and the keys with the step of the generator by image (suffix <c>_pf</c>, all in <c>f5/oracle/out/json</c>), and the 12 starts of the wall <c>bin/out/sweep_wall.txt</c>. The oracle presses Square at tick P = 1 and the arrow is born at S = P + 9 = 10 on the ground (P + 1 for the
/// hunter in the air); the bench of the tests (a hero at (100, 100) on a synthetic field, the pad of the oracle) does the same, so a tick of the oracle is a tick of the test. The Z of the DLL is the oracle's minus one raw unit (ADR-0026).
/// </summary>
internal sealed class ArrowOracle
{
    /// <summary>The tick of the press of the oracle.</summary>
    internal const int Press = 1;

    /// <summary>The tick of the birth of the arrow on the ground (the delay 8 of the fiches w8 and w9, plus the tick of the press, plus one).</summary>
    internal const int Birth = 10;

    /// <summary>The unit of raw Z that the binary's Z has over the DLL's (ADR-0026).</summary>
    internal const int RawZUnit = 1;

    private static readonly Regex FxPattern = new(@"^fx\(id=(?<id>\d+),dir=(?<dir>-?\d+),x=(?<x>-?[\d.]+),y=(?<y>-?[\d.]+),z=(?<z>-?[\d.]+),v=\((?<vx>-?[\d.]+),(?<vy>-?[\d.]+),(?<vz>-?[\d.]+)\)\)$", RegexOptions.CultureInvariant);

    private readonly List<JsonElement> _rows;

    /// <summary>The slot (as the key names it) that holds the arrow, and the tick of its first row.</summary>
    internal string ArrowSlot { get; }

    internal int FirstTick { get; }

    private ArrowOracle(List<JsonElement> rows, string arrowSlot, int firstTick)
    {
        _rows = rows;
        ArrowSlot = arrowSlot;
        FirstTick = firstTick;
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

    /// <summary>
    /// The key <paramref name="name"/> (<c>arrow3_east_wall</c>, ...): the frozen one (<c>bin/out/json</c>), or the key of E20.f5 (<c>f5/oracle/out/json</c>) when the name is one of the new scenarios, or, with <paramref name="stepByImage"/>, the key with the step of the generator
    /// by image (<c>f5/oracle/out/json/{name}_pf.json</c>, every key has one).
    /// </summary>
    internal static ArrowOracle Load(string name, bool stepByImage = false)
    {
        var inF5 = $"f5/oracle/out/json/{name}{(stepByImage ? "_pf" : string.Empty)}.json";
        var path = stepByImage ? Annex(inF5) : TryAnnex(inF5) ?? Annex($"bin/out/json/{name}.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = document.RootElement.EnumerateArray().Select(r => r.Clone()).ToList();
        foreach (var row in rows)
        {
            foreach (var slot in row.GetProperty("slots").EnumerateObject())
            {
                if (slot.Value.GetProperty("type68").GetInt32() is 3 or 4)
                {
                    return new ArrowOracle(rows, slot.Name, row.GetProperty("tick").GetInt32());
                }
            }
        }

        throw new InvalidOperationException($"the key {name} holds no entity of type 3 or 4");
    }

    private static string? TryAnnex(string relative)
    {
        try
        {
            return Annex(relative);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The number of ticks of the key.</summary>
    internal int Ticks => _rows.Count;

    internal JsonElement Row(int tick) => _rows[tick - 1];

    /// <summary>The slot of the arrow at <paramref name="tick"/>, or null when the arrow is not in the table.</summary>
    internal JsonElement? Arrow(int tick) => Slot(tick, ArrowSlot);

    /// <summary>Another slot of the key (the target), or null.</summary>
    internal JsonElement? Slot(int tick, string slot) => Row(tick).GetProperty("slots").TryGetProperty(slot, out var element) && element.ValueKind == JsonValueKind.Object ? element : null;

    /// <summary>The slot of the key other than the arrow's that holds an entity at <paramref name="tick"/> (the first target), or null.</summary>
    internal JsonElement? Target(int tick)
        => Row(tick).GetProperty("slots").EnumerateObject().Where(s => s.Name != ArrowSlot && s.Value.ValueKind == JsonValueKind.Object).Select(s => (JsonElement?)s.Value).FirstOrDefault();

    /// <summary>The line of the arrow as the tests compare it, in the frame of the DLL (the Z minus one raw unit): the status, the position, the forces, the wall flag, the direction and the animation asked, the flags, the end-of-animation flag (not for a destroyed arrow); or "absent".</summary>
    internal string Line(int tick)
    {
        if (Arrow(tick) is not { } a)
        {
            return "absent";
        }

        int I(string key) => a.GetProperty(key).GetInt32();
        var common = string.Create(CultureInfo.InvariantCulture, $"st={I("status")} x={I("px")} y={I("py")} z={I("pz") - RawZUnit} fx={I("fx")} fy={I("fy")} fz={I("fz")} adj={I("adj_13C")}");

        // The status 4 line stops at the wall flag: the binary falls from the destruction into the writes of the bounce on the entity it has just destroyed (direction, animation and flags of the dead arrow), which nothing reads (D-F5-3).
        return I("status") == 4
            ? common
            : string.Create(CultureInfo.InvariantCulture, $"{common} dir={I("dir_t")} anim={I("anim_t")} flags=0x{I("flags6C"):X} reset={I("reset_AC")}");
    }

    /// <summary>The generator word at the head of the tick (<c>rng0</c>).</summary>
    internal uint Rng0(int tick) => Row(tick).GetProperty("rng0").GetUInt32();

    /// <summary>The draws of the tick (the key does not count the step of the end of the frame).</summary>
    internal int Draws(int tick) => Row(tick).GetProperty("draws").GetInt32();

    /// <summary>The ticks (and the number of effects <c>fx(id=...)</c> each) at which the key creates the effect <paramref name="id"/>.</summary>
    internal IReadOnlyList<(int Tick, int Count)> EffectTicks(int id)
    {
        var list = new List<(int, int)>();
        foreach (var row in _rows)
        {
            var count = row.GetProperty("effects").EnumerateArray().Select(e => FxPattern.Match(e.GetString()!)).Count(m => m.Success && int.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture) == id);
            if (count > 0)
            {
                list.Add((row.GetProperty("tick").GetInt32(), count));
            }
        }

        return list;
    }

    /// <summary>The effects the key creates with an attached form (<c>attach(...)</c>), as texts, with their tick.</summary>
    internal IReadOnlyList<(int Tick, string Text)> Attached()
        => _rows.SelectMany(row => row.GetProperty("effects").EnumerateArray().Select(e => (Tick: row.GetProperty("tick").GetInt32(), Text: e.GetString()!)))
            .Where(x => x.Text.StartsWith("attach(", StringComparison.Ordinal)).ToList();

    /// <summary>The sounds of the key (<c>snd(N)</c> of the journal) from the tick <paramref name="from"/> to <paramref name="to"/>, in order, WITHOUT the identifier 0 (the machine journals a <c>snd(0)</c> at some absolute ticks of every key: it is not the arrow's).</summary>
    internal IReadOnlyList<(int Tick, int Sfx)> Sounds(int from, int to)
        => _rows.Where(r => r.GetProperty("tick").GetInt32() >= from && r.GetProperty("tick").GetInt32() <= to)
            .SelectMany(r => r.GetProperty("journal").EnumerateArray().Select(j => (Tick: r.GetProperty("tick").GetInt32(), Text: j.GetString()!)))
            .Where(j => j.Text.StartsWith("snd(", StringComparison.Ordinal) && j.Text != "snd(0)")
            .Select(j => (j.Tick, int.Parse(j.Text[4..^1], CultureInfo.InvariantCulture))).ToList();

    /// <summary>The first tick (relative to the birth) at which a field of the arrow is non-zero, or -1.</summary>
    internal int FirstTickWhere(string field)
        => Enumerable.Range(FirstTick, Ticks - FirstTick + 1).Where(t => Arrow(t) is { } a && a.GetProperty(field).GetInt32() != 0).Select(t => t - FirstTick).DefaultIfEmpty(-1).First();

    /// <summary>The last tick (relative to the birth) at which the arrow is in the table; the arrow is absent from the next one.</summary>
    internal int LastTickPresent()
        => Enumerable.Range(FirstTick, Ticks - FirstTick + 1).Where(t => Arrow(t) is not null).Select(t => t - FirstTick).DefaultIfEmpty(-1).Max();

    /// <summary>The 12 starts of the wall of the hunter's arrow (<c>sweep_wall.txt</c>, first block): for the hero's x0 (96 to 107) the tick of the flag, relative to S.</summary>
    internal static IReadOnlyDictionary<int, int> WallStarts()
    {
        var pattern = new Regex(@"^\s*hero x0=(?<x0>\d+) : S=10 ForceAdjusted at S\+(?<flag>\d+)\s+\(x then = 349\.000\)$", RegexOptions.CultureInvariant);
        var starts = new Dictionary<int, int>();
        var inFirstBlock = false;
        foreach (var line in File.ReadLines(Annex("bin/out/sweep_wall.txt")))
        {
            if (line.StartsWith("== ", StringComparison.Ordinal))
            {
                inFirstBlock = line.StartsWith("== arrow3", StringComparison.Ordinal);
                continue;
            }

            var m = pattern.Match(line);
            if (inFirstBlock && m.Success)
            {
                starts[int.Parse(m.Groups["x0"].Value, CultureInfo.InvariantCulture)] = int.Parse(m.Groups["flag"].Value, CultureInfo.InvariantCulture);
            }
        }

        return starts;
    }
}
