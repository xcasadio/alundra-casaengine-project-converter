#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.d1 T3: a trace of the run oracle (<c>docs/plan-e20-annexe/e20-d/oracle/out/g*/</c>, written by <c>rscen.py</c>: the real code of <c>ALUN_CD.EXE</c> (France) in the MIPS emulator, column-major JSON). Row 0 is the start
/// state, row <c>t</c> the state AFTER the passes of logic tick <c>t</c>; <see cref="Hold"/> at <c>t</c> is the pad word held on that tick.
/// </summary>
internal sealed class RunOracleTrace
{
    private readonly Dictionary<string, long[]> _hero = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<RunJournalItem>> _journal = new();
    private readonly List<Dictionary<string, long[]>> _targets = new();

    public string Name { get; private init; } = string.Empty;

    /// <summary>The number of logic ticks of the trace (rows 1 to <see cref="Ticks"/>).</summary>
    public int Ticks { get; private init; }

    public long[] Hold { get; private init; } = Array.Empty<long>();

    public int HeroPxX { get; private init; }

    public int HeroPxY { get; private init; }

    public string Inventory { get; private init; } = "kit";

    public IReadOnlyList<RunLayoutRect> Layout { get; private init; } = Array.Empty<RunLayoutRect>();

    public int TargetCount { get; private init; }

    public long Col(string column, int tick) => _hero[column][tick];

    /// <summary>A column of the target <paramref name="target"/> (0 for the first one) at <paramref name="tick"/>; a null cell (no entity touched) reads as -1.</summary>
    public long TargetCol(int target, string column, int tick) => _targets[target][column][tick];

    public IReadOnlyList<RunJournalItem> Journal(int tick) => _journal.TryGetValue(tick, out var items) ? items : Array.Empty<RunJournalItem>();

    /// <summary>The folder of the oracle outputs (<c>out</c>, or <c>out-swim</c> for the swim groups W1 to W10, E20.i), found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string OutputFolder(string folder = "out")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-d", "oracle", folder);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-d/oracle/{folder}' directory found above '{AppContext.BaseDirectory}': this test needs the run oracle traces");
    }

    /// <summary>The output folder of a trace name: the swim groups (<c>w1/...</c> to <c>w10/...</c>) are in <c>out-swim</c>, the run groups (<c>g1/...</c>) in <c>out</c>.</summary>
    private static string FolderOf(string name) => name.Length > 1 && name[0] == 'w' && char.IsDigit(name[1]) ? "out-swim" : "out";

    /// <summary>Every trace of a group folder (<c>g1</c>, ...), as names such as <c>g1/R1-T+D</c>, in order.</summary>
    public static IEnumerable<string> NamesOf(string group)
    {
        var folder = Path.Combine(OutputFolder(FolderOf(group)), group);
        return Directory.GetFiles(folder, "*.json").Select(f => group + "/" + Path.GetFileNameWithoutExtension(f)).OrderBy(n => n, StringComparer.Ordinal);
    }

    /// <summary>Loads <c>&lt;name&gt;.json</c> (a name such as <c>g1/R1-T+D</c>).</summary>
    public static RunOracleTrace Load(string name)
    {
        var path = Path.Combine(OutputFolder(FolderOf(name)), name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the run oracle trace '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var spec = root.GetProperty("spec");
        var ticks = root.GetProperty("tick").GetArrayLength() - 1;
        var layout = new List<RunLayoutRect>();
        if (spec.TryGetProperty("layout", out var rects))
        {
            foreach (var r in rects.EnumerateArray())
            {
                layout.Add(new RunLayoutRect(
                    r.GetProperty("c0").GetInt32(), r.GetProperty("r0").GetInt32(), r.GetProperty("c1").GetInt32(), r.GetProperty("r1").GetInt32(),
                    Nullable(r, "height_byte"), Nullable(r, "walk"), Nullable(r, "gprop")));
            }
        }

        var trace = new RunOracleTrace
        {
            Name = name,
            Ticks = ticks,
            Hold = root.GetProperty("hold").EnumerateArray().Select(e => e.GetInt64()).ToArray(),
            HeroPxX = spec.GetProperty("hero_px")[0].GetInt32(),
            HeroPxY = spec.GetProperty("hero_px")[1].GetInt32(),
            Inventory = spec.TryGetProperty("inv", out var inv) ? inv.GetString() ?? "kit" : "kit",
            Layout = layout,
            TargetCount = root.GetProperty("targets").GetArrayLength(),
        };

        foreach (var column in root.GetProperty("hero").EnumerateObject())
        {
            trace._hero[column.Name] = column.Value.EnumerateArray().Select(e => e.GetInt64()).ToArray();
        }

        foreach (var target in root.GetProperty("targets").EnumerateArray())
        {
            var columns = new Dictionary<string, long[]>(StringComparer.Ordinal);
            foreach (var column in target.EnumerateObject())
            {
                columns[column.Name] = column.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64() : -1).ToArray();
            }

            trace._targets.Add(columns);
        }

        foreach (var entry in root.GetProperty("journal").EnumerateArray())
        {
            var tick = entry[0].GetInt32();
            var items = new List<RunJournalItem>();
            foreach (var item in entry[1].EnumerateArray())
            {
                var kind = item[0].GetString() ?? string.Empty;
                if (kind == "sound")
                {
                    items.Add(new RunJournalItem(kind, item[1][0].GetInt32(), 0, 0, 0));
                }
                else if (kind == "effect")
                {
                    var e = item[1];
                    items.Add(new RunJournalItem(kind, e.GetProperty("id").GetInt32(), e.GetProperty("cx").GetDouble(), e.GetProperty("cy").GetDouble(), e.GetProperty("cz").GetDouble()));
                }
            }

            trace._journal[tick] = items;
        }

        return trace;
    }

    private static int? Nullable(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    /// <summary>The first tick (from <paramref name="from"/>) at which <paramref name="column"/> equals <paramref name="value"/>, or -1.</summary>
    public int FirstTick(string column, long value, int from = 1)
    {
        for (var t = Math.Max(from, 0); t <= Ticks; t++)
        {
            if (Col(column, t) == value)
            {
                return t;
            }
        }

        return -1;
    }

    /// <summary>The cell (walkability, ground property, height byte) the layout gives at (<paramref name="column"/>, <paramref name="row"/>), the rectangles applied in order.</summary>
    public (int Walkability, int GroundProperty, int Height) CellAt(int column, int row)
    {
        int wk = 0, gp = 0, hb = 0;
        foreach (var r in Layout)
        {
            if (column >= r.C0 && column <= r.C1 && row >= r.R0 && row <= r.R1)
            {
                hb = r.HeightByte ?? hb;
                wk = r.Walkability ?? wk;
                gp = r.GroundProperty ?? gp;
            }
        }

        return (wk, gp, hb);
    }
}
