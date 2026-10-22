#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.e0a5 (docs/plan-e20-heros.md section 1.16.5, D-E0A5-1 to D-E0A5-4, D-E0A5-7): the oracle of the clock of an entity born by the interface, read BY KEY (never pasted): <c>docs/plan-e20-annexe/e20-e0a5/oracle/out/birth_clock.json</c>
/// (rows <c>WAVE</c>, <c>SW0</c> ... <c>SPR251</c>, <c>SPR3</c>; keys <c>cnt1c</c>, <c>end_AC</c>, <c>loops_B0</c>) and the <c>reset_AC</c> of <c>docs/plan-e20-annexe/e20-f3/oracle/out/wave_*.json</c>. S is the first tick of the entity.
/// </summary>
internal static class BirthClockOracle
{
    internal static string Find(params string[] relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(new[] { directory.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no '{string.Join('/', relative)}' found above '{AppContext.BaseDirectory}'");
    }

    private static JsonElement Row(string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Find("docs", "plan-e20-annexe", "e20-e0a5", "oracle", "out", "birth_clock.json")));
        return document.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("label").GetString() == label).Clone();
    }

    internal static IEnumerable<JsonElement> Ticks(string label) => Row(label).GetProperty("ticks").EnumerateArray();

    /// <summary>The first tick (relative to S) at which the end-of-animation flag of the row is raised.</summary>
    internal static int FirstEnd(string label) => Ticks(label).First(t => t.GetProperty("end_AC").GetInt32() == 1).GetProperty("k").GetInt32();

    /// <summary>The box cursor (<c>cnt1c</c>) of the row at S + <paramref name="k"/>.</summary>
    internal static int Cursor(string label, int k) => Ticks(label).First(t => t.GetProperty("k").GetInt32() == k).GetProperty("cnt1c").GetInt32();

    /// <summary>The first tick (relative to S) at which the loop counter (<c>loops_B0</c>) of the row reaches <paramref name="count"/>.</summary>
    internal static int FirstLoop(string label, int count) => Ticks(label).First(t => t.GetProperty("loops_B0").GetInt32() >= count).GetProperty("k").GetInt32();

    /// <summary>The first tick (relative to S) at which the <c>reset_AC</c> of the wave of the E20.f3 oracle file <paramref name="file"/> (<c>wave_steel_east.json</c>) is raised.</summary>
    internal static int FirstWaveFlag(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Find("docs", "plan-e20-annexe", "e20-f3", "oracle", "out", file)));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray()
            .Where(r => r.GetProperty("slots").TryGetProperty("1", out var one) && one.ValueKind == JsonValueKind.Object)
            .Select(r => (Tick: r.GetProperty("tick").GetInt32(), Flag: r.GetProperty("slots").GetProperty("1").GetProperty("reset_AC").GetInt32()))
            .ToList();
        return rows.First(r => r.Flag == 1).Tick - rows[0].Tick;
    }
}
