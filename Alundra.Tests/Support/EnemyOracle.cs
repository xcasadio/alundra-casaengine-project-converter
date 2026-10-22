#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Alundra.Tests.Support;

/// <summary>Reader of the E14.a0b oracle outputs (the rows are the expected values; nothing here computes one).</summary>
internal static class EnemyOracle
{
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e14-annexe", "e14a", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e14-annexe/e14a/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the E14.a0b oracle");
    }

    private static IEnumerable<string> Lines(string relative)
    {
        var path = Path.Combine(OutputFolder(), relative);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the oracle file '{relative}' is missing", path);
        }

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var all = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && line[0] != '#')
            {
                all.Add(line);
            }
        }

        return all;
    }

    private static int[] Ints(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();

    /// <summary>The states of a helper: <c>relpos</c>, <c>relpos2</c>, <c>tap2</c>, <c>tapfront</c>, <c>had</c>, <c>startwalk</c>, <c>atan</c>, <c>d1</c>, <c>d9</c>.</summary>
    public static List<EnemyHelperRow> Helper(string name)
    {
        var rows = new List<EnemyHelperRow>();
        foreach (var line in Lines(Path.Combine("h1", name + ".txt.gz")))
        {
            var parts = line.Split('|');
            rows.Add(new EnemyHelperRow(Ints(parts[0]), Ints(parts[1])));
        }

        return rows;
    }

    public static List<EnemyGroundRow> Ground(int map)
    {
        var rows = new List<EnemyGroundRow>();
        foreach (var line in Lines(Path.Combine("h2", $"map_{map}.txt.gz")))
        {
            var parts = line.Split('|');
            var head = Ints(parts[0]);
            rows.Add(new EnemyGroundRow(head[0], head[1], head[2], head[3], head[4], head[5], head[6], Ints(parts[1]), Ints(parts[2]), Ints(parts[3])));
        }

        return rows;
    }
}
