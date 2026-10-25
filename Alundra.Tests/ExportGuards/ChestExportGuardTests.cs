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
/// E20.e0d T1 (docs/plan-e20-heros.md section 1.26.2, (a2), D-E0D-3, D-E0D-7): the guard of the DATA the chest handlers rest on, read on the REAL export (<c>alundra-project/</c>, what the DLL reads), never skipped: a missing file fails.
/// The chests are the records of the sprite type 30 on the global page (the bit 7 of <c>SpriteDirection</c> clear) that are enabled: 420 outside the map 0 on 170 maps; 400 hold contents (158 maps), 20 hold none (16 with a script at F, 4 native
/// on the map 325); 404 have the native F255 (no script at F) and 16 a script of the map; 4 hold contents and no flag (the maps 11, 161, 362, 398: they are refilled at every entry, D-E0D-3); no flag is in the temporary bank (bit 15) nor
/// at 2048 or above; no contents reach the random table (<c>0x62</c> and above). The map 0 (the test map) holds 98 chests, one object per index, 32 of them appearing at the load (the bit 6 of the direction): none of those holds the
/// contents 0 or 42 (the type 72 has neither A255 nor C255) or 89 and above (no such object), so no chest of the map 0 that is there at the load opens on a degenerate object. The counts are those of
/// <c>docs/plan-e20-annexe/e20-e0d-disc/t7_census_chests.py</c> (2026-10-24), read again by T0.
/// </summary>
public sealed class ChestExportGuardTests
{
    private const int TestMap = 0;

    private sealed record Chest(int Map, int Direction, int F, int Flag, int Contents, bool Enabled);

    private static readonly Lazy<IReadOnlyList<Chest>> Chests = new(ReadChests);

    private static IReadOnlyList<Chest> ReadChests()
    {
        var root = ProjectRootFinder.Find();
        var maps = Directory.GetFiles(Path.Combine(root, "Maps"), "*.tmj", SearchOption.AllDirectories)
            .Where(f => Path.GetDirectoryName(f)!.EndsWith("tilemap", StringComparison.Ordinal))
            .ToList();
        Assert.True(maps.Count == 483, $"the export holds 483 maps with a tile map, found {maps.Count}");
        var chests = new List<Chest>();
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
                    if (!properties.TryGetValue("SpriteDirection", out var direction) || !properties.TryGetValue("SpriteTableIndex", out var type)
                        || type.GetInt32() != 30 || (direction.GetInt32() & 0x80) != 0)
                    {
                        continue;
                    }

                    var enabled = !properties.TryGetValue("IsEnabled", out var isEnabled) || isEnabled.ValueKind switch
                    {
                        JsonValueKind.False => false,
                        JsonValueKind.Number => isEnabled.GetInt32() != 0,
                        _ => true,
                    };
                    int Of(string key) => properties.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
                    chests.Add(new Chest(map, direction.GetInt32(), Of("EventCodesF_InteractIndex") & 0x7F, Of("_10"), Of("Contents"), enabled));
                }
            }
        }

        return chests;
    }

    private static IReadOnlyList<Chest> OutsideTheTestMap => Chests.Value.Where(c => c.Enabled && c.Map != TestMap).ToList();

    [Fact]
    public void A2_FourHundredAndTwentyChests_OnOneHundredAndSeventyMaps_FourHundredHoldContents_TwentyHoldNone()
    {
        var chests = OutsideTheTestMap;

        Assert.Equal((420, 170), (chests.Count, chests.Select(c => c.Map).Distinct().Count()));
        Assert.Equal((400, 158), (chests.Count(c => c.Contents != 0), chests.Where(c => c.Contents != 0).Select(c => c.Map).Distinct().Count()));
        Assert.Equal(20, chests.Count(c => c.Contents == 0));
        Assert.Equal(20, chests.Count(c => c.Contents == 0 && (c.F != 0 || c.Map == 325)));  // 16 with a script at F, 4 native on the map 325.
        Assert.Equal(0, Chests.Value.Count(c => !c.Enabled));
    }

    [Fact]
    public void A2_FourHundredAndFourHaveTheNativeF255_SixteenHaveAScriptAtF()
    {
        var chests = OutsideTheTestMap;

        Assert.Equal((404, 16), (chests.Count(c => c.F == 0), chests.Count(c => c.F != 0)));
        Assert.Equal((400, 4), (chests.Count(c => c.F == 0 && c.Contents != 0), chests.Count(c => c.F == 0 && c.Contents == 0)));
        Assert.Equal(16, chests.Count(c => c.F != 0 && c.Contents == 0)); // a scripted chest holds nothing: its script gives the object.
    }

    [Fact]
    public void A2_FourChestsHoldContentsAndNoFlag_NoFlagIsInTheTemporaryBankNorAt2048OrAbove_NoContentsReachesTheRandomTable()
    {
        var chests = OutsideTheTestMap;
        var withContents = chests.Where(c => c.Contents != 0).ToList();

        Assert.Equal(new[] { 11, 161, 362, 398 }, withContents.Where(c => c.Flag == 0).Select(c => c.Map).Order().ToArray());
        Assert.Equal(0, chests.Count(c => (c.Flag & 0x8000) != 0));
        Assert.Equal(0, chests.Count(c => (c.Flag & 0x7FFF) >= 0x800));
        Assert.Equal(367, withContents.Where(c => c.Flag != 0).Select(c => c.Flag).Distinct().Count());
        Assert.Equal(0, chests.Count(c => c.Contents >= 0x62));
        Assert.Equal(342, withContents.Count(c => (c.Direction & 0x40) != 0)); // present at the load; the 58 others are made later by a script.
    }

    [Fact]
    public void A2_EveryContentsOfARealChest_IsAnObjectThatHasC255()
    {
        var types = PickupAi.SpriteTypes.ToHashSet();

        foreach (var chest in OutsideTheTestMap.Where(c => c.Contents != 0))
        {
            Assert.True(types.Contains(chest.Contents + 0x1E), $"the chest of the map {chest.Map} holds the contents {chest.Contents}, whose object (type {chest.Contents + 0x1E}) has no C255");
        }
    }

    [Fact]
    public void A2_TheTestMap_HoldsNinetyEightChests_NoneOfTheThirtyTwoThereAtTheLoadHoldsADegenerateObject()
    {
        var chests = Chests.Value.Where(c => c.Map == TestMap).ToList();
        var present = chests.Where(c => (c.Direction & 0x40) != 0).ToList();

        Assert.Equal((98, 32), (chests.Count, present.Count));
        Assert.Equal(0, chests.Count(c => c.Flag != 0 || c.F != 0));
        Assert.Equal(new[] { 0, 42, 89, 90, 91, 92, 93, 94, 95, 96, 97 }, chests.Where(c => !PickupAi.SpriteTypes.Contains(c.Contents + 0x1E)).Select(c => c.Contents).Order().ToArray());
        Assert.Empty(present.Where(c => !PickupAi.SpriteTypes.Contains(c.Contents + 0x1E)));
    }
}
