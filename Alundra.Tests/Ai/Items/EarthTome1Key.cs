#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T1: a world key of the earth tome 1, the rows of the real code of <c>ALUN_CD.EXE</c> in the MIPS machine with the slot index words written (<c>docs/plan-e20-annexe/e20-h4c1/disc/keys/</c>, the relief keys of the critic
/// <c>critic/work/fcopy/_crit_out/</c>): per tick, the rocks (the entities of the sprite type 16, by slot), the other entities (the Beannoids of a target key), the hero, the item in use, the draws of the generator and the sounds.
/// </summary>
internal sealed class EarthTome1Key
{
    private EarthTome1Key(string name, int[] hero, IReadOnlyList<KeyRow> rows)
    {
        Name = name;
        Hero = hero;
        Rows = rows;
    }

    internal string Name { get; }

    /// <summary>The pixel (X, Y) of the hero in the key.</summary>
    internal int[] Hero { get; }

    /// <summary>The rows, tick 1 first.</summary>
    internal IReadOnlyList<KeyRow> Rows { get; }

    internal KeyRow At(int tick) => Rows[tick - 1];

    /// <summary>One entity of a row, the fields the key holds (the words of the entity slot of the machine).</summary>
    internal sealed record Entity(int Status, int Type, int Px, int Py, int Pz, int Fx, int Fy, int Fz, uint AnimT, uint AnimC, uint DirT, int Adir, int ResetAc, uint Flags, int Adj, int Ground, int Slope, int Cnt1C,
        int Bw, int Bd, int Bh, int? TouchSlot, int C1D0);

    internal sealed record Item(int Counter, uint HeroAnim, uint ItemInUse, int Phase);

    internal sealed record KeyRow(int Tick, int Draws, IReadOnlyDictionary<int, Entity> Rocks, IReadOnlyDictionary<int, Entity> Others, IReadOnlyList<int> Sounds, IReadOnlyList<string> Effects, Item Item, (uint AnimT, int Px, int Py, int Pz) Hero);

    /// <summary>The key <paramref name="name"/> of the folder <paramref name="folder"/> (relative to the annex of h4c1).</summary>
    internal static EarthTome1Key Load(string name, string folder = "disc/keys")
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome1Annex(folder + "/" + name + ".json")));
        var root = document.RootElement;
        var hero = root.GetProperty("meta").GetProperty("hero").EnumerateArray().Select(h => h.GetInt32()).ToArray();
        var rows = new List<KeyRow>();
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            var item = row.GetProperty("item");
            var h = row.GetProperty("hero");
            rows.Add(new KeyRow(
                row.GetProperty("tick").GetInt32(),
                row.GetProperty("draws").GetInt32(),
                Entities(row.GetProperty("rocks")),
                Entities(row.GetProperty("others")),
                row.GetProperty("sounds").EnumerateArray().Select(s => int.Parse(s.GetString()!.Substring(4, s.GetString()!.Length - 5), CultureInfo.InvariantCulture)).ToList(),
                row.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!).ToList(),
                new Item(item.GetProperty("counter").GetInt32(), U(item.GetProperty("hero_anim")), U(item.GetProperty("item_in_use")), item.GetProperty("phase").GetInt32()),
                h.ValueKind == JsonValueKind.Null ? (0u, 0, 0, 0) : (U(h.GetProperty("anim_t")), h.GetProperty("px").GetInt32(), h.GetProperty("py").GetInt32(), h.GetProperty("pz").GetInt32())));
        }

        return new EarthTome1Key(name, hero, rows);
    }

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());

    private static Dictionary<int, Entity> Entities(JsonElement element)
        => element.EnumerateObject().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p =>
        {
            var e = p.Value;
            int I(string key) => e.GetProperty(key).GetInt32();
            return new Entity(I("status"), I("type"), I("px"), I("py"), I("pz"), I("fx"), I("fy"), I("fz"), U(e.GetProperty("anim_t")), U(e.GetProperty("anim_c")), U(e.GetProperty("dir_t")), I("adir"), I("reset_AC"), U(e.GetProperty("flags6C")),
                I("adj_13C"), I("ground_144"), I("slope_18C"), I("cnt1c"), I("bw"), I("bd"), I("bh"), e.GetProperty("touch_slot").ValueKind == JsonValueKind.Null ? null : e.GetProperty("touch_slot").GetInt32(), I("c1d0"));
        });
}
