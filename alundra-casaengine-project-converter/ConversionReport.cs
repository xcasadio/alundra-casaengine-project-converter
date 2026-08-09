using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// Collects counters and messages produced while converting Alundra data to CasaEngine assets,
/// then dumps them to report.json so a run's results can be inspected and diffed.
/// </summary>
public sealed class ConversionReport
{
    private const int MaxExamplesPerWarningCategory = 3;

    private static readonly Regex DigitRunPattern = new(@"\d+", RegexOptions.Compiled);

    public Dictionary<string, int> Counters { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Messages { get; } = new();

    /// <summary>Wall-clock duration of each named phase, in the order they ran.</summary>
    public Dictionary<string, double> PhaseDurationsSeconds { get; } = new();

    public double TotalDurationSeconds { get; set; }
    public long OutputSizeBytes { get; set; }
    public int OutputFileCount { get; set; }

    public void Increment(string counterName, int amount = 1)
    {
        Counters[counterName] = Counters.GetValueOrDefault(counterName) + amount;
    }

    /// <summary>
    /// Runs one conversion phase and records its wall-clock duration under <paramref name="phaseName"/>.
    /// </summary>
    public void RunPhase(string phaseName, Action phase)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            phase();
        }
        finally
        {
            stopwatch.Stop();
            PhaseDurationsSeconds[phaseName] =
                PhaseDurationsSeconds.GetValueOrDefault(phaseName) + Round(stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>
    /// Walks the produced project and records its size on disk and its file count. Called once the
    /// last phase has written everything, so report.json itself is not counted.
    /// </summary>
    public void MeasureOutput(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            OutputSizeBytes = 0;
            OutputFileCount = 0;
            return;
        }

        long totalSize = 0;
        var fileCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            totalSize += new FileInfo(filePath).Length;
            fileCount++;
        }

        OutputSizeBytes = totalSize;
        OutputFileCount = fileCount;
    }

    /// <summary>
    /// Groups the free-text warnings by the prefix they start with ("map_4: ...", "Font: ..."),
    /// with digit runs normalised so map_4 and map_311 land in the same bucket. This is a summary:
    /// the raw <see cref="Warnings"/> list is saved as well and stays authoritative.
    /// </summary>
    public IReadOnlyList<WarningCategory> GroupWarnings()
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var warning in Warnings)
        {
            var category = CategorizeWarning(warning);
            if (!groups.TryGetValue(category, out var examples))
            {
                examples = new List<string>();
                groups.Add(category, examples);
            }

            examples.Add(warning);
        }

        return groups
            .OrderByDescending(group => group.Value.Count)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new WarningCategory(
                group.Key,
                group.Value.Count,
                group.Value.Take(MaxExamplesPerWarningCategory).ToList()))
            .ToList();
    }

    public static string CategorizeWarning(string warning)
    {
        var separatorIndex = warning.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return "(uncategorized)";
        }

        var prefix = warning[..separatorIndex].Trim();

        // Guards against a warning whose first colon belongs to its payload (a Windows path, a time,
        // a sentence) rather than to a real prefix.
        if (prefix.Length == 0 || prefix.Length > 60)
        {
            return "(uncategorized)";
        }

        return DigitRunPattern.Replace(prefix, "#");
    }

    public void Save(string filePath)
    {
        var payload = new
        {
            Counters,
            Metrics = new
            {
                TotalDurationSeconds = Round(TotalDurationSeconds),
                PhaseDurationsSeconds,
                OutputSizeBytes,
                OutputSizeMegabytes = Round(OutputSizeBytes / (1024d * 1024d)),
                OutputFileCount,
            },
            WarningsByCategory = GroupWarnings(),
            Warnings,
            Errors,
            Messages,
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Conversion summary ===");

        foreach (var (name, value) in Counters.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {name}: {value}");
        }

        if (Warnings.Count > 0)
        {
            Console.WriteLine($"  Warnings: {Warnings.Count}");
            foreach (var category in GroupWarnings())
            {
                Console.WriteLine($"    {category.Category}: {category.Count}");
            }
        }

        if (Errors.Count > 0)
        {
            Console.WriteLine($"  Errors: {Errors.Count}");
        }

        Console.WriteLine($"  Duration: {Round(TotalDurationSeconds):0.###} s");
        foreach (var (phaseName, seconds) in PhaseDurationsSeconds)
        {
            Console.WriteLine($"    {phaseName}: {seconds:0.###} s");
        }

        Console.WriteLine(
            $"  Output: {OutputFileCount} files, {Round(OutputSizeBytes / (1024d * 1024d)):0.##} MB");
    }

    private static double Round(double value) => Math.Round(value, 3);

    public sealed record WarningCategory(string Category, int Count, IReadOnlyList<string> Examples);
}
