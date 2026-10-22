#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.e0a1 (docs/plan-e20-heros.md section 1.16.1): the reader of the outputs of the apparition oracle (<c>docs/plan-e20-annexe/e20-e0a/oracle/out</c>, found above the test binaries): <c>v1.json</c> (151 global
/// prefabs), <c>v2.json</c> (244 numbers of the map page), <c>v3.json</c> (geometry), <c>v7.json</c> (the AT9 spawn calls), <c>v4-pairs.txt</c> (the valid indices of the 483 maps). Every number is read as 64-bit; each file is
/// parsed once. E20.e0b2 reads two more fields: <c>loot</c> of the projection (<c>+0x3C</c>, <c>+0x40</c>) and <c>rng_out</c> of the vector (the word after the call, from the seed <c>0x12345678</c> the oracle writes before each call). Nothing here computes an expected value: the rows are the expected values.
/// </summary>
internal static class SpawnIndexOracle
{
    private static readonly ConcurrentDictionary<string, object> Cache = new();

    internal static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-e0a", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-e0a/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the E20.e0a1 oracle");
    }

    internal static IReadOnlyList<SpawnVector> Vectors(string name)
        => (IReadOnlyList<SpawnVector>)Cache.GetOrAdd(name, _ => Read(Path.Combine(OutputFolder(), name + ".json")));

    /// <summary>The valid indices of each map (0 to 482) as the real binary answered them, <c>v4-pairs.txt</c> (<c>map:i,j,k</c>, an empty list for an empty map).</summary>
    internal static IReadOnlyDictionary<int, IReadOnlyList<int>> ValidIndicesByMap()
        => (IReadOnlyDictionary<int, IReadOnlyList<int>>)Cache.GetOrAdd("v4", _ =>
        {
            var result = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var line in File.ReadAllLines(Path.Combine(OutputFolder(), "v4-pairs.txt")).Where(l => l.Length > 0))
            {
                var colon = line.IndexOf(':');
                result[int.Parse(line[..colon], System.Globalization.CultureInfo.InvariantCulture)] =
                    line[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            }

            return result;
        });

    private static int I(JsonElement e) => unchecked((int)e.GetInt64());

    private static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    private static IReadOnlyList<int> Ints(JsonElement e) => e.EnumerateArray().Select(I).ToList();

    private static IReadOnlyList<SpawnVector> Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var vectors = new List<SpawnVector>();
        foreach (var v in document.RootElement.EnumerateArray())
        {
            SpawnProjection? projection = null;
            if (v.TryGetProperty("p", out var p))
            {
                projection = new SpawnProjection(
                    I(p.GetProperty("hp")), I(p.GetProperty("hpmax")), I(p.GetProperty("ref")), I(p.GetProperty("id")), U(p.GetProperty("flags")), Ints(p.GetProperty("sprog")), Ints(p.GetProperty("prog")),
                    U(p.GetProperty("anim_t")), U(p.GetProperty("dir_t")), I(p.GetProperty("px")), I(p.GetProperty("py")), I(p.GetProperty("pz")), I(p.GetProperty("modx")), I(p.GetProperty("mody")), I(p.GetProperty("modz")),
                    I(p.GetProperty("mx")), I(p.GetProperty("my")), I(p.GetProperty("mz")), I(p.GetProperty("w")), I(p.GetProperty("h")), I(p.GetProperty("d")), I(p.GetProperty("tilex")), I(p.GetProperty("tiley")),
                    I(p.GetProperty("terrain")), I(p.GetProperty("floor")), Ints(p.GetProperty("corners")), p.GetProperty("root").GetString()!, p.GetProperty("parent").GetString()!,
                    I(p.GetProperty("has_attack")), I(p.GetProperty("anim_flags")), Ints(p.GetProperty("loot")));
            }

            vectors.Add(new SpawnVector(
                v.GetProperty("id").GetString()!, I(v.GetProperty("page")), I(v.GetProperty("index")), v.GetProperty("map").ValueKind == JsonValueKind.Null ? null : I(v.GetProperty("map")),
                v.GetProperty("parent").GetString()!, I(v.GetProperty("x")), I(v.GetProperty("y")), I(v.GetProperty("z")), I(v.GetProperty("dir")), v.GetProperty("world").GetString()!, I(v.GetProperty("ret")),
                v.TryGetProperty("rolls", out var rolls) && I(rolls) != 0, Ints(v.GetProperty("sounds")).Where(s => s != 0).ToList(), projection, U(v.GetProperty("rng_out"))));
        }

        return vectors;
    }
}
