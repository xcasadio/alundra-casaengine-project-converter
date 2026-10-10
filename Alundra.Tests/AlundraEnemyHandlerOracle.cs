#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests;

/// <summary>One word the handler changed in the oracle (<c>changed</c>): its name and its value before and after (raw words, 64-bit as read).</summary>
internal sealed record HandlerChange(string Name, long Before, long After);

/// <summary>One entry of the oracle's call journal (<c>spawn_39F58</c>, <c>sound</c>, <c>effect_3BE74</c>): its kind and its arguments (a sound has its number as the only argument).</summary>
internal sealed record HandlerJournalEntry(string Kind, long[] Arguments);

/// <summary>
/// One single-call vector of <c>vectors-beannoide.json</c> / <c>vectors-muruta.json</c> (E14.a1 oracle): the handler, the generator's word in and out, the 27 decoded fields of the entity and 7 of the hero before the call, the
/// attack of the toucher, what changed, the journal, and of the inputs only the break-effect byte (<see cref="InputFx"/>, null for the sprite's own), the platform flag and the active-effect flag.
/// </summary>
internal sealed record HandlerVector(
    string Handler, long RngIn, long RngOut, int Draws, long Latch,
    IReadOnlyDictionary<string, long> Entity, IReadOnlyDictionary<string, long> Hero, long[]? HeroAttack, long[]? AttackerAttack,
    IReadOnlyList<HandlerChange> Changed, IReadOnlyList<HandlerJournalEntry> Journal, long? InputFx, long InputPlat, long InputAe);

/// <summary>One call of a scenario (<c>calls[]</c>): the entity before and after (53 decoded fields each), the hero's seven fields, the generator, the latch, what changed and the journal of THIS call.</summary>
internal sealed record HandlerCall(
    int Tick, string Handler, int Slot, long Letter, long RngIn, long RngOut, long Latch,
    IReadOnlyDictionary<string, long> Before, IReadOnlyDictionary<string, long> After, IReadOnlyDictionary<string, long> Hero,
    IReadOnlyList<HandlerChange> Changed, IReadOnlyList<HandlerJournalEntry> Journal);

/// <summary>One scenario file of the oracle: its name and its calls (the per-tick columns are not read).</summary>
internal sealed record HandlerScenario(string Name, IReadOnlyList<HandlerCall> Calls);

/// <summary>
/// E14.a1b T4 (docs/plan-e14-ia-native.md section 1.3b, "Lecteur de l'oracle"): the reader of the outputs of the E14.a1 oracle (<c>docs/plan-e14-annexe/e14a/oracle-a1/out</c>, found above the test binaries). Every number is
/// read as a 64-bit integer (pointers and the generator's word exceed <see cref="int"/>); each file is parsed once. Nothing here computes an expected value: the rows are the expected values.
/// </summary>
internal static class AlundraEnemyHandlerOracle
{
    /// <summary>The 16 names a Beannoid handler may change (plan section 1.3b, "Clôture"); the Muruta's set adds <c>ai0_280</c> and <c>b0_274</c>.</summary>
    internal static readonly IReadOnlySet<string> BeannoideClosure = new HashSet<string>
    {
        "activeeffect_1B8", "ai1_282", "anim_target88", "b1_275", "b3_277", "dir_target8C", "flags6C", "forcex_C4", "forcey_C8", "fstepx_D4", "fstepy_D8", "hp", "letter_228", "status", "tforcex_BC", "tforcey_C0",
    };

    private static readonly ConcurrentDictionary<string, object> Cache = new();

    internal static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e14-annexe", "e14a", "oracle-a1", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e14-annexe/e14a/oracle-a1/out' directory found above '{AppContext.BaseDirectory}': this test needs the E14.a1 oracle");
    }

    /// <summary>The vectors of <c>vectors-beannoide.json</c> (2,052) or <c>vectors-muruta.json</c>.</summary>
    internal static IReadOnlyList<HandlerVector> Vectors(string species)
        => (IReadOnlyList<HandlerVector>)Cache.GetOrAdd("vectors-" + species, _ => ReadVectors(Path.Combine(OutputFolder(), $"vectors-{species}.json")));

    /// <summary>The 21 scenarios of the oracle, by name (<c>BN-death</c>, <c>MU-hall-kill</c>, <c>ARENA-H3-live</c>, ...).</summary>
    internal static IReadOnlyList<HandlerScenario> Scenarios()
        => (IReadOnlyList<HandlerScenario>)Cache.GetOrAdd("scenarios", _ => Directory.GetFiles(OutputFolder(), "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("vectors-", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => ReadScenario(f))
            .ToList());

    private static long Number(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetInt64(),
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.Null => 0,
        _ => throw new InvalidOperationException($"a number was expected, found {value.ValueKind}"),
    };

    private static IReadOnlyDictionary<string, long> Words(JsonElement element)
        => element.EnumerateObject().ToDictionary(p => p.Name, p => Number(p.Value), StringComparer.Ordinal);

    private static IReadOnlyList<HandlerChange> Changes(JsonElement element)
        => element.EnumerateArray().Select(c => new HandlerChange(c[0].GetString()!, Number(c[1]), Number(c[2]))).ToList();

    private static IReadOnlyList<HandlerJournalEntry> Journal(JsonElement element)
        => element.EnumerateArray().Select(j => new HandlerJournalEntry(
            j[0].GetString()!,
            j[1].ValueKind == JsonValueKind.Array ? j[1].EnumerateArray().Select(Number).ToArray() : new[] { Number(j[1]) })).ToList();

    private static long[]? Pair(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.EnumerateArray().Select(Number).ToArray();

    private static IReadOnlyList<HandlerVector> ReadVectors(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var vectors = new List<HandlerVector>();
        foreach (var v in document.RootElement.EnumerateArray())
        {
            var inputs = v.GetProperty("inputs");
            long? fx = inputs.TryGetProperty("fx", out var fxElement) && fxElement.ValueKind == JsonValueKind.Number ? fxElement.GetInt64() : null;
            vectors.Add(new HandlerVector(
                v.GetProperty("handler").GetString()!,
                Number(v.GetProperty("rng_in")), Number(v.GetProperty("rng_out")), (int)Number(v.GetProperty("draws")), Number(v.GetProperty("latch")),
                Words(v.GetProperty("entity_before")), Words(v.GetProperty("hero_before")), Pair(v.GetProperty("hero_attack")), Pair(v.GetProperty("attacker_attack")),
                Changes(v.GetProperty("changed")), Journal(v.GetProperty("journal")),
                fx, inputs.TryGetProperty("plat", out var plat) ? Number(plat) : 0, inputs.TryGetProperty("ae", out var ae) ? Number(ae) : 0));
        }

        return vectors;
    }

    private static HandlerScenario ReadScenario(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var calls = new List<HandlerCall>();
        foreach (var c in document.RootElement.GetProperty("calls").EnumerateArray())
        {
            calls.Add(new HandlerCall(
                (int)Number(c.GetProperty("tick")), c.GetProperty("handler").GetString()!, (int)Number(c.GetProperty("slot")), Number(c.GetProperty("letter")),
                Number(c.GetProperty("rng_in")), Number(c.GetProperty("rng_out")), Number(c.GetProperty("latch")),
                Words(c.GetProperty("before")), Words(c.GetProperty("after")), Words(c.GetProperty("hero")),
                Changes(c.GetProperty("changed")), Journal(c.GetProperty("journal"))));
        }

        return new HandlerScenario(Path.GetFileNameWithoutExtension(path), calls);
    }
}
