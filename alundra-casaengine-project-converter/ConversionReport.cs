using System.Text.Json;

namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// Collects counters and messages produced while converting Alundra data to CasaEngine assets,
/// then dumps them to report.json so a run's results can be inspected and diffed.
/// </summary>
public sealed class ConversionReport
{
    public Dictionary<string, int> Counters { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Messages { get; } = new();

    public void Increment(string counterName, int amount = 1)
    {
        Counters[counterName] = Counters.GetValueOrDefault(counterName) + amount;
    }

    public void Save(string filePath)
    {
        var payload = new
        {
            Counters,
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
        }

        if (Errors.Count > 0)
        {
            Console.WriteLine($"  Errors: {Errors.Count}");
        }
    }
}
