#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>A hit-oracle trace (<c>H1</c>, <c>H3</c>...): the start state and one row per tick, as <c>run_c1.py</c> writes them.</summary>
internal sealed record HitOracleTrace(string Name, HitOracleRow Start, IReadOnlyList<HitOracleRow> Rows, IReadOnlyDictionary<int, uint> Holds, int HeroPixelX, int HeroPixelY, IReadOnlyList<int[]> TargetPixels)
{
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c1", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c1/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the hit oracle traces");
    }

    public static HitOracleTrace Load(string name)
    {
        var path = Path.Combine(OutputFolder(), name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the hit oracle trace '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var spec = root.GetProperty("spec");
        var holds = new Dictionary<int, uint>();
        foreach (var hold in spec.GetProperty("holds").EnumerateObject())
        {
            holds[int.Parse(hold.Name, System.Globalization.CultureInfo.InvariantCulture)] = hold.Value.GetUInt32();
        }

        var meta = root.GetProperty("meta");
        var heroPixels = meta.GetProperty("hero_start_px");
        var targets = meta.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("px").EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToList();
        return new HitOracleTrace(
            name, ReadRow(root.GetProperty("pre"), 0), root.GetProperty("rows").EnumerateArray().Select(r => ReadRow(r, r.GetProperty("tick").GetInt32())).ToList(), holds,
            heroPixels[0].GetInt32(), heroPixels[1].GetInt32(), targets);
    }

    private static HitOracleRow ReadRow(JsonElement e, int tick)
    {
        var sparks = new List<(int Id, int X, int Y, int Z)>();
        var handlers = new List<(int Slot, int Letter)>();
        if (e.TryGetProperty("journal_raw", out var journal))
        {
            foreach (var entry in journal.EnumerateArray())
            {
                var kind = entry[0].GetString();
                var args = entry[1];
                if (kind == "effect_3BDD8" && args[1].GetInt32() == 9)
                {
                    sparks.Add((9, args[3].GetInt32(), args[4].GetInt32(), args[5].GetInt32()));
                }
                else if (kind == "handler")
                {
                    handlers.Add((args[0].GetInt32(), args[1].GetInt32()));
                }
            }
        }

        return new HitOracleRow(
            tick, e.GetProperty("hold").GetUInt32(), e.GetProperty("latch_80146DF8").GetUInt32(), new HitOracleEntity(e.GetProperty("hero")),
            e.GetProperty("targets").EnumerateArray().Select(t => new HitOracleEntity(t)).ToList(), sparks, handlers);
    }
}
