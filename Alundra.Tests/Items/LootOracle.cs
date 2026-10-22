#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Items;

/// <summary>One entity that the load loop of the binary creates in a pair of V8: the record, the slot, the sprite number (<c>+0x68</c>), and what the roll left (<c>+0x3C</c>, <c>+0x40</c>, steps drawn).</summary>
internal sealed record LoadedEntity(int Record, int Slot, int Sprite, uint Item, int Flag40, int Draws);

/// <summary>
/// E20.e0b2: the reader of the outputs of the loot-roll oracle (<c>docs/plan-e20-annexe/e20-e0b/oracle/out</c>, found above the test binaries). Nothing here computes an expected value: the rows are the expected values.
/// </summary>
internal static class LootOracle
{
    internal static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-e0b", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-e0b/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the E20.e0b2 oracle");
    }

    internal static JsonDocument Read(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(OutputFolder(), name)));

    internal static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    internal static int I(JsonElement e) => unchecked((int)e.GetInt64());

    private static IReadOnlyList<LoadPair>? _pairs;

    /// <summary>The pairs of V8, in the order of <c>v8.json</c>.</summary>
    internal static IReadOnlyList<LoadPair> Pairs()
    {
        if (_pairs != null)
        {
            return _pairs;
        }

        using var document = Read("v8.json");
        _pairs = document.RootElement.GetProperty("pairs").EnumerateArray().Select(p =>
        {
            var tile = p.GetProperty("tile");
            var entities = p.GetProperty("entities").EnumerateArray()
                .Select(e => new LoadedEntity(I(e.GetProperty("record")), I(e.GetProperty("slot")), I(e.GetProperty("sprite")), U(e.GetProperty("item")), I(e.GetProperty("flag40")), I(e.GetProperty("draws"))))
                .ToList();
            return new LoadPair(
                I(p.GetProperty("map")), p.GetProperty("zone").GetString()!, p.GetProperty("world").GetString()!, I(tile[0]), I(tile[1]), I(p.GetProperty("records_called")), entities,
                I(p.GetProperty("draws")), Convert.ToUInt32(p.GetProperty("final_word").GetString(), 16));
        }).ToList();
        return _pairs;
    }
}
