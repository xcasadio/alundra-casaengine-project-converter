#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.c2b T2: a run of the hurt-hero oracle of E20.c2 (<c>docs/plan-e20-annexe/e20-c2/oracle/out/A1.json</c>, ...: the real code of <c>ALUN_CD.EXE</c> (France) in the MIPS emulator, column-major JSON, one file per group and
/// several runs per file). Row 0 is the start state, row <c>t</c> the state AFTER the passes of logic tick <c>t</c>; <see cref="Hold"/> at <c>t</c> is the pad word held on that tick. The hero columns are those of the
/// hit oracle (<c>anim_target</c>, <c>dir_target</c>, <c>hp</c>, <c>c1cc</c>, <c>c1d0</c>, <c>touching_slot</c>, <c>force_x</c>, <c>pos_x</c>, <c>visible</c>...), <c>g</c> holds the globals (<c>stats_hp</c>...).
/// </summary>
internal sealed class HurtOracleTrace
{
    private static readonly Regex SoundItem = new(@"^sound\((\d+)\)$", RegexOptions.Compiled);

    private readonly Dictionary<string, long[]> _hero = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long[]> _globals = new(StringComparer.Ordinal);
    private readonly List<Dictionary<string, long[]>> _targets = new();
    private readonly List<string[]> _journal = new();

    public string Name { get; private init; } = string.Empty;

    /// <summary>The number of logic ticks of the run (rows 1 to <see cref="Ticks"/>).</summary>
    public int Ticks { get; private init; }

    public long[] Hold { get; private init; } = Array.Empty<long>();

    public int HeroPixelX { get; private init; }

    public int HeroPixelY { get; private init; }

    /// <summary>The pixel of each target (the Beannoid of map 14).</summary>
    public IReadOnlyList<int[]> TargetPixels { get; private init; } = Array.Empty<int[]>();

    public long Col(string column, int tick) => _hero[column][tick];

    public long Global(string column, int tick) => _globals[column][tick];

    /// <summary>A column of the target <paramref name="target"/> at <paramref name="tick"/>; a null cell (nothing touched) reads as -1.</summary>
    public long TargetCol(int target, string column, int tick) => _targets[target][column][tick];

    /// <summary>The sounds of the journal of <paramref name="tick"/>, in order.</summary>
    public IEnumerable<int> Sounds(int tick)
        => _journal[tick].Select(j => SoundItem.Match(j)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The first tick (from 1) at which the hero column <paramref name="column"/> has <paramref name="value"/>, or -1.</summary>
    public int FirstTick(string column, long value)
    {
        for (var t = 1; t <= Ticks; t++)
        {
            if (Col(column, t) == value)
            {
                return t;
            }
        }

        return -1;
    }

    /// <summary>The folder of the outputs of the annex <paramref name="annex"/> (<c>e20-c2</c>, the oracle of E20.c2, or <c>e20-c2b</c>, its new scenarios A3, A5 and C-carried).</summary>
    public static string OutputFolder(string annex = "e20-c2")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", annex, "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/{annex}/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the hurt-hero oracle");
    }

    /// <summary>Loads the run <paramref name="run"/> of <paramref name="file"/> (<c>A1.json</c>) of the annex <paramref name="annex"/>.</summary>
    public static HurtOracleTrace Load(string file, string run, string annex = "e20-c2")
    {
        var path = Path.Combine(OutputFolder(annex), file);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the hurt-hero oracle file '{file}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var element = document.RootElement.GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("spec").GetProperty("name").GetString() == run);
        var spec = element.GetProperty("spec");
        var meta = element.GetProperty("meta");
        var trace = new HurtOracleTrace
        {
            Name = run,
            Ticks = element.GetProperty("tick").GetArrayLength() - 1,
            Hold = element.GetProperty("hold").EnumerateArray().Select(e => e.GetInt64()).ToArray(),
            HeroPixelX = spec.GetProperty("hero_px")[0].GetInt32(),
            HeroPixelY = spec.GetProperty("hero_px")[1].GetInt32(),
            TargetPixels = meta.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("px").EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToList(),
        };

        Fill(element.GetProperty("hero"), trace._hero);
        Fill(element.GetProperty("g"), trace._globals);
        foreach (var target in element.GetProperty("targets").EnumerateArray())
        {
            var columns = new Dictionary<string, long[]>(StringComparer.Ordinal);
            Fill(target, columns);
            trace._targets.Add(columns);
        }

        foreach (var entry in element.GetProperty("journal").EnumerateArray())
        {
            trace._journal.Add(entry.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray());
        }

        return trace;
    }

    private static void Fill(JsonElement columns, Dictionary<string, long[]> into)
    {
        foreach (var column in columns.EnumerateObject())
        {
            if (column.Value.ValueKind == JsonValueKind.Array)
            {
                into[column.Name] = column.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64() : -1).ToArray();
            }
        }
    }
}
