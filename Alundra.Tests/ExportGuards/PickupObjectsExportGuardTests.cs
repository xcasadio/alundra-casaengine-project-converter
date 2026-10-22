#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Tests.Support;

namespace Alundra.Tests.ExportGuards;

/// <summary>
/// E20.e0c T4 (docs/plan-e20-heros.md section 1.26, (p10), D-E0C-1): the guard of the DATA the pickup handlers rest on, read on the REAL export (<c>alundra-project/</c>, what the DLL reads), never skipped: a missing file fails.
/// The objects placed on the maps (sprite types 31 to 118 but 72, global page, enabled): 152 on 53 types, none with a flag (<c>_10</c>) nor a content; at the slot C of their record, 59 have no entity code (the native C255 serves them: 31 in the
/// Debug zone, 28 on 24 other maps), 36 have the native A and a script of the map at C, 57 have scripts at both slots (the binary and the DLL leave those to the map's script). The sprite records: A255 on 88 types (the chest, type 30, and the 87 of C255),
/// C255 on 87 types (31 to 118 but 72), F255 on the chest alone (the only place a chest object, <c>+0x274 == 2</c>, is made).
/// The counts are those of <c>docs/plan-e20-annexe/e20-e0c/posed-rev3/census_exported.py</c> (2026-10-22).
/// </summary>
public sealed class PickupObjectsExportGuardTests
{
    private const int DebugZoneMap = 11;

    private sealed record Placed(int Map, int Type, int A, int C, int Flag, int Contents);

    private static readonly Lazy<IReadOnlyList<Placed>> PlacedObjects = new(ReadPlaced);

    private static IReadOnlyList<Placed> ReadPlaced()
    {
        var root = ProjectRootFinder.Find();
        var maps = Directory.GetFiles(Path.Combine(root, "Maps"), "*.tmj", SearchOption.AllDirectories)
            .Where(f => Path.GetDirectoryName(f)!.EndsWith("tilemap", StringComparison.Ordinal))
            .ToList();
        Assert.True(maps.Count == 483, $"the export holds 483 maps with a tile map, found {maps.Count}");
        var placed = new List<Placed>();
        foreach (var file in maps)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var map = int.Parse(name[(name.LastIndexOf('-') + 1)..]);
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var layer in document.RootElement.GetProperty("layers").EnumerateArray())
            {
                if (layer.GetProperty("type").GetString() != "objectgroup" || layer.GetProperty("name").GetString() != "Entities")
                {
                    continue;
                }

                foreach (var entity in layer.GetProperty("objects").EnumerateArray())
                {
                    var properties = entity.TryGetProperty("properties", out var list)
                        ? list.EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("value"))
                        : new Dictionary<string, JsonElement>();
                    if (!properties.TryGetValue("SpriteDirection", out var direction) || !properties.TryGetValue("SpriteTableIndex", out var type))
                    {
                        continue;
                    }

                    var enabled = !properties.TryGetValue("IsEnabled", out var isEnabled) || isEnabled.ValueKind switch
                    {
                        JsonValueKind.False => false,
                        JsonValueKind.Number => isEnabled.GetInt32() != 0,
                        _ => true,
                    };
                    if (!enabled || (direction.GetInt32() & 0x80) != 0 || type.GetInt32() is < 31 or > 118 or 72)
                    {
                        continue;
                    }

                    int Of(string key) => properties.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
                    placed.Add(new Placed(map, type.GetInt32(), Of("EventCodesA_LoadIndex") & 0x7F, Of("EventCodesC_TickIndex") & 0x7F, Of("_10"), Of("Contents")));
                }
            }
        }

        return placed;
    }

    [Fact]
    public void ThePlacedObjects_AreOneHundredAndFiftyTwo_OnFiftyThreeTypes_NoneWithAFlagNorAContent()
    {
        var placed = PlacedObjects.Value;

        Assert.Equal((152, 53), (placed.Count, placed.Select(p => p.Type).Distinct().Count()));
        Assert.Equal((0, 0), (placed.Count(p => p.Flag != 0), placed.Count(p => p.Contents != 0))); // the branch of A255 for a flag stays a named stub (D-E0C-3); the placed object holds nothing.
    }

    [Fact]
    public void AtTheSlotC_FiftyNinePlacedObjectsHaveNoEntityCode_ThirtySixHaveTheNativeAAndAScriptAtC_FiftySevenHaveScriptsAtBothSlots()
    {
        var placed = PlacedObjects.Value;
        var native = placed.Where(p => p.C == 0).ToList();

        Assert.Equal(59, native.Count);
        Assert.Equal(59, native.Count(p => p.A == 0));
        Assert.Equal(36, placed.Count(p => p.A == 0 && p.C != 0));
        Assert.Equal(57, placed.Count(p => p.A != 0 && p.C != 0));
        Assert.Equal(152, 59 + 36 + 57); // no placed object has a script at A and none at C.
        Assert.Equal((31, 25), (native.Count(p => p.Map == DebugZoneMap), native.Select(p => p.Map).Distinct().Count())); // the Debug zone and 24 other maps.
    }

    [Fact]
    public void TheSpriteRecords_CarryA255On88Types_C255On87_AndF255OnTheChestAlone()
    {
        var path = Path.Combine(ProjectRootFinder.Find(), "Data", "sprite-records.json");
        Assert.True(File.Exists(path), $"{path} is missing");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var a = new List<int>();
        var c = new List<int>();
        var f = new List<int>();
        var other = 0;
        foreach (var record in document.RootElement.EnumerateObject().Select(p => p.Value))
        {
            var key = record.TryGetProperty("BankKey", out var bank) ? bank.GetString() : null;
            var isGlobal = key != null && key.StartsWith("alundra_", StringComparison.Ordinal);
            var type = isGlobal ? int.Parse(key![8..]) : -1;
            foreach (var (property, list) in new[] { ("ProgramLoad", a), ("ProgramTick", c), ("ProgramInteract", f) })
            {
                if (record.GetProperty(property).GetInt32() == 255)
                {
                    list.Add(type);
                }
            }

            foreach (var property in new[] { "ProgramTouch", "ProgramDeactivate" })
            {
                other += record.GetProperty(property).GetInt32() == 255 ? 1 : 0;
            }
        }

        Assert.Equal((88, 87, 1, 0), (a.Count, c.Count, f.Count, other));
        Assert.DoesNotContain(-1, a.Concat(c).Concat(f)); // only the global page carries them.
        Assert.Equal(PickupAi.SpriteTypes.Order(), c.Order());
        Assert.Equal(PickupAi.SpriteTypes.Append(30).Order(), a.Order());
        Assert.Equal(new[] { 30 }, f);
    }
}
