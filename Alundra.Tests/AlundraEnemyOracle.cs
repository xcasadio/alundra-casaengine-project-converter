#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Alundra.Tests;

/// <summary>One state of a helper run in the E14.a0b oracle (<c>docs/plan-e14-annexe/e14a/oracle/out/h1/&lt;helper&gt;.txt.gz</c>): the inputs and the outputs, signed 32-bit words, as the emulated <c>ALUN_CD.EXE</c> (France) left them.</summary>
internal sealed record EnemyHelperRow(int[] In, int[] Out);

/// <summary>One row of the ground oracle (<c>out/h2/map_&lt;N&gt;.txt.gz</c>): a body (<see cref="E"/>) and the hero (<see cref="H"/>) after the creation (variant 0), a physics step (1) or a displacement of the body (2), and the relative position the real <c>0x8007FE20</c> gave for the pair (<see cref="R"/>).
/// Both blocks: posx posy posz modx mody modz w h d tilex tiley terrain floor c0 c1 c2 c3 (raw 16.16 words, the BINARY's values).</summary>
internal sealed record EnemyGroundRow(int Map, int Record, int Variant, int HeroDx, int HeroDy, int SweepDx, int SweepDy, int[] E, int[] H, int[] R)
{
    public const int PosX = 0, PosY = 1, PosZ = 2, ModX = 3, ModY = 4, ModZ = 5, Width = 6, Height = 7, Depth = 8, TileX = 9, TileY = 10, Terrain = 11, Floor = 12, Corner0 = 13;
}

/// <summary>Reader of the E14.a0b oracle outputs (the rows are the expected values; nothing here computes one).</summary>
internal static class AlundraEnemyOracle
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
