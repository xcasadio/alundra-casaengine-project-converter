#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Xunit;
using Alundra.Physics;

namespace Alundra.Tests.Physics;

/// <summary>
/// E20.e0k5 T1 (docs/plan-e20-heros.md section 1.18.5, phase A, D-E0K5-1): the pure halving search of the binary (<c>ComputeXYPosition</c> <c>0x80037730</c>: the whole vector tried, then halved by <c>sra 1</c> on X and Y together; an accepted
/// sub-step starts from the new position with the half of the rest; the first whole step accepted ends; the loop ends when the halves are both zero (cardinal direction) or either is zero (otherwise)) against the hand model
/// <c>docs/plan-e20-annexe/e20-step-search/work/xymodel.py</c> (replayed against the real code of ALUN_CD.EXE (France): 55 000 vectors, 0 gap, 449 of 449 instructions). The vectors are the model's own output
/// (<c>docs/plan-e20-annexe/e20-e0k5/step-vectors/</c>, <c>gen_vectors.py</c> and <c>gen_named.py</c>): 20 000 random cases (the generator of the real-code replay, a non-hero subject with NoObstacleSlide, no ground snapping, no entity candidate, half of them with one wall
/// placed so that the halving converges) and the named cases (the wall column 5 of the point-blank reading). Each case records the queries of the model (the offset tried from the start, in order, and the verdict of the wall test): the search must make
/// exactly those queries, in that order (a query that is not recorded fails the case), and return the offset and the accepted flag of the model.
/// </summary>
public sealed class HalvingStepSearchTests
{
    private const int MinimumFuzzCases = 20000;
    private const int WallThreshold = 7143424; // 109 pixels: the box of the named world is blocked above it

    private sealed record Case(string Name, bool Cardinal, int ForceX, int ForceY, int ResultX, int ResultY, bool Accepted, (int Dx, int Dy, bool Blocked)[] Queries);

    private static string AnnexFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-e0k5/step-vectors", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"HalvingStepSearchTests: no 'docs/plan-e20-annexe/e20-e0k5/step-vectors/{name}' found above '{AppContext.BaseDirectory}'.");
    }

    private static (int, int, bool)[] ParseQueries(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<(int, int, bool)>();
        }

        var parts = text.Split(';');
        var queries = new (int, int, bool)[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var c = parts[i].Split(',');
            queries[i] = (int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture), c[2] == "1");
        }

        return queries;
    }

    private static int N(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static IEnumerable<Case> ReadFuzz()
    {
        using var stream = new GZipStream(File.OpenRead(AnnexFile("halving-vectors.tsv.gz")), CompressionMode.Decompress);
        using var reader = new StreamReader(stream);
        var index = 0;
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var c = line.Split('\t');
            yield return new Case($"fuzz#{index++}", c[0] == "1", N(c[1]), N(c[2]), N(c[3]), N(c[4]), c[5] == "1", ParseQueries(c[6]));
        }
    }

    private static IReadOnlyDictionary<string, Case> ReadNamed()
    {
        var cases = new Dictionary<string, Case>();
        foreach (var line in File.ReadAllLines(AnnexFile("named-cases.tsv")))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var c = line.Split('\t');
            cases[c[0]] = new Case(c[0], c[1] == "1", N(c[2]), N(c[3]), N(c[6]), N(c[7]), c[8] == "1", ParseQueries(c[9]));
        }

        return cases;
    }

    /// <summary>Runs the search with a predicate answered from the recorded queries; returns null when the case is replayed exactly, otherwise the first gap.</summary>
    private static string? Replay(Case c)
    {
        var next = 0;
        string? gap = null;
        var result = HalvingStepSearch.Search(c.ForceX, c.ForceY, c.Cardinal, (dx, dy) =>
        {
            if (gap != null)
            {
                return true;
            }

            if (next >= c.Queries.Length)
            {
                gap = $"query #{next} ({dx},{dy}) beyond the {c.Queries.Length} of the model";
                return true;
            }

            var expected = c.Queries[next];
            if (expected.Dx != dx || expected.Dy != dy)
            {
                gap = $"query #{next}: ({dx},{dy}) but the model tried ({expected.Dx},{expected.Dy})";
                return true;
            }

            next++;
            return expected.Blocked;
        });

        if (gap != null)
        {
            return gap;
        }

        if (next != c.Queries.Length)
        {
            return $"{next} queries but the model made {c.Queries.Length}";
        }

        if (result.X != c.ResultX || result.Y != c.ResultY || result.Accepted != c.Accepted)
        {
            return $"result ({result.X},{result.Y},{result.Accepted}) but the model gave ({c.ResultX},{c.ResultY},{c.Accepted})";
        }

        return null;
    }

    [Fact]
    public void Fuzz_AgainstTheModel_ZeroGap()
    {
        var count = 0;
        var failed = 0;
        var gaps = new List<string>();
        foreach (var c in ReadFuzz())
        {
            count++;
            if (Replay(c) is { } gap)
            {
                failed++;
                if (gaps.Count < 10)
                {
                    gaps.Add($"{c.Name} (force {c.ForceX},{c.ForceY} cardinal {c.Cardinal}): {gap}");
                }
            }
        }

        Assert.True(count >= MinimumFuzzCases, $"only {count} cases in the vector file");
        Assert.True(failed == 0, $"{failed} gaps against the model out of {count} (first ones): " + string.Join(" | ", gaps));
    }

    [Fact]
    public void Fuzz_File_HasPartialRefusedZeroAndFullCases()
    {
        int partial = 0, refused = 0, zero = 0, full = 0;
        foreach (var c in ReadFuzz())
        {
            if (c.Queries.Length == 0)
            {
                zero++;
            }
            else if (!c.Accepted)
            {
                refused++;
            }
            else if (c.ResultX != c.ForceX || c.ResultY != c.ForceY)
            {
                partial++;
            }
            else
            {
                full++;
            }
        }

        Assert.True(partial >= 1000 && refused >= 1000 && zero >= 100 && full >= 1000, $"partial {partial}, refused {refused}, zero {zero}, full {full}");
    }

    public static IEnumerable<object[]> NamedNames()
    {
        foreach (var name in ReadNamed().Keys)
        {
            yield return new object[] { name };
        }
    }

    [Theory]
    [MemberData(nameof(NamedNames))]
    public void NamedCase_AgainstTheModel(string name)
    {
        Assert.Null(Replay(ReadNamed()[name]));
    }

    /// <summary>The wall of the point-blank reading as a predicate of the absolute position: blocked above 109 pixels.</summary>
    private static HalvingStepResult EastAgainstTheWall(int startX, int stepPixels)
        => HalvingStepSearch.Search(stepPixels << 16, 0, true, (dx, _) => startX + dx > WallThreshold);

    [Theory]
    [InlineData(107, 3)]
    [InlineData(104, 6)]
    [InlineData(107, 6)]
    public void Halving_StopsOneRawUnitBeforeTheWall_NotOnIt(int startPixel, int stepPixels)
    {
        var result = EastAgainstTheWall(startPixel << 16, stepPixels);
        Assert.True(result.Accepted);
        Assert.Equal(WallThreshold - 1, (startPixel << 16) + result.X);
    }

    [Fact]
    public void Halving_FreeStep_IsTheWholeStepInOneQuery()
    {
        var queries = 0;
        var result = HalvingStepSearch.Search(3 << 16, 0, true, (_, _) =>
        {
            queries++;
            return false;
        });
        Assert.Equal(1, queries);
        Assert.Equal(new HalvingStepResult(3 << 16, 0, true), result);
    }

    [Fact]
    public void Halving_TheLargestFreePositionRuleIsWrongInThreeOfTenCells()
    {
        // The critic of the reading: the simple rule 'the largest free position' disagrees with the binary by one raw unit on 3 of these 10 cells.
        var differing = 0;
        var cells = new (int Pixel, int Step)[] { (105, 3), (106, 3), (107, 3), (108, 3), (109, 3), (104, 6), (105, 6), (107, 6), (108, 6), (109, 6) };
        foreach (var (pixel, step) in cells)
        {
            var start = pixel << 16;
            var simple = Math.Max(0, Math.Min(step << 16, WallThreshold - start));
            var result = EastAgainstTheWall(start, step);
            if (result.X != simple)
            {
                differing++;
                Assert.Equal(simple - 1, result.X);
            }
        }

        Assert.Equal(3, differing);
    }

    [Fact]
    public void Halving_Flush_NothingAccepted()
    {
        var result = EastAgainstTheWall(109 << 16, 3);
        Assert.False(result.Accepted);
        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
    }

    [Fact]
    public void Halving_ZeroVector_MakesNoQueryAndAcceptsNothing()
    {
        var queries = 0;
        var result = HalvingStepSearch.Search(0, 0, true, (_, _) =>
        {
            queries++;
            return false;
        });
        Assert.Equal(0, queries);
        Assert.Equal(new HalvingStepResult(0, 0, false), result);
    }
}
