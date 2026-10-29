#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1 bis: a world key of the fire tome 1, the rows of the real code of <c>ALUN_CD.EXE</c> in the MIPS machine with the slot index words written and the generator stepped after each frame (R-EL-1, R-EL-2): the 30 keys of the discovery
/// (<c>docs/plan-e20-annexe/e20-h4d2/disc/keys/</c>, replayed twice by T0), the <c>_qfix</c> keys of the reading of 2026-10-25 (<c>disc/out_qfix/</c>) and the three relief keys of the critic (<c>critic/keys_proposed/</c>). A row is the state at the END of a tick: the hero, the item in use, the pointer kept
/// by the driver, the generator at the head of the tick (<c>rng0</c>) and before its frame-end step (<c>rng1</c>), the draws of the tick, the entities by slot (the column, the balls and the targets) and the events (effects, attached effects, sounds). The words the plan never compares (<c>id</c> but for the relation
/// "same entity", <c>b27c</c>, <c>count</c>, <c>ident</c>) are read only for that relation.
/// </summary>
internal sealed class FireTome1Key
{
    private FireTome1Key(string name, IReadOnlyList<Row> rows)
    {
        Name = name;
        Rows = rows;
    }

    internal string Name { get; }

    /// <summary>The rows, tick 1 first.</summary>
    internal IReadOnlyList<Row> Rows { get; }

    internal Row At(int tick) => Rows[tick - 1];

    internal int LastTick => Rows.Count;

    /// <summary>The hero as the key sees it at the end of a tick (<c>Z</c> is the binary's, the DLL's plus 1).</summary>
    internal sealed record KeyHero(uint AnimT, uint AnimC, int X, int Y, int Z);

    internal sealed record KeyItem(int Counter, int InUse, int Mp, int Phase);

    /// <summary>One entity of a row. The targets (the blocks of the sprite 25) hold only the status, the type, the place, the two counters, the touch and the hit points: the other fields are null.</summary>
    internal sealed record KeyEntity(int Slot, int Id, int Type, int Status, int X, int Y, int Z, int C1cc, int C1d0, int? Touch, int? Hp,
        uint? Flags, int? Fx, int? Fy, int? Fz, uint? AnimT, uint? AnimC, uint? DirT, uint? DirC, int? Row, int? AnimEnd, int? B274, int? B278Slot, int? C20, int? Slope, int? Ground, int? Adj13c, int[]? Hb, int? AtkAttr, int? AtkPow)
    {
        /// <summary>True for a column or a ball: the entities that hold the whole set of fields.</summary>
        internal bool Full => Flags.HasValue;
    }

    internal sealed record Row(int Tick, KeyHero Hero, KeyItem Item, bool HasPointer, int? PtrSlot, uint Rng0, uint Rng1, int Draws, IReadOnlyDictionary<int, KeyEntity> Ents, IReadOnlyList<string> Events, IReadOnlyList<int> Hits);

    /// <summary>The key <paramref name="name"/> of the folder <paramref name="folder"/> (relative to the annex of h4d2: <c>disc/keys</c>, <c>disc/out_qfix</c>, <c>critic/keys_proposed</c>).</summary>
    internal static FireTome1Key Load(string name, string folder = "disc/keys")
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.FireTome1Annex(folder + "/" + name + ".json")));
        var rows = new List<Row>();
        foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
        {
            var hero = row.GetProperty("hero");
            var item = row.GetProperty("item");
            rows.Add(new Row(
                row.GetProperty("t").GetInt32(),
                new KeyHero(U(hero.GetProperty("anim_t")), U(hero.GetProperty("anim_c")), I(hero.GetProperty("x")), I(hero.GetProperty("y")), I(hero.GetProperty("z"))),
                new KeyItem(I(item.GetProperty("counter")), I(item.GetProperty("inuse")), I(item.GetProperty("mp")), I(item.GetProperty("phase"))),
                row.TryGetProperty("ptr_slot", out _),
                row.TryGetProperty("ptr_slot", out var pointer) ? OptInt(pointer) : null,
                U(row.GetProperty("rng0")),
                U(row.GetProperty("rng1")),
                row.GetProperty("draws").GetInt32(),
                row.GetProperty("ents").EnumerateObject().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => Entity(int.Parse(p.Name, CultureInfo.InvariantCulture), p.Value)),
                row.GetProperty("events").EnumerateArray().Select(e => e.GetString()!).ToList(),
                row.GetProperty("hits").EnumerateArray().Select(h => h.GetInt32()).ToList()));
        }

        return new FireTome1Key(name, rows);
    }

    private static KeyEntity Entity(int slot, JsonElement e)
    {
        int? Opt(string field) => e.TryGetProperty(field, out var v) ? OptInt(v) : null;
        uint? OptU(string field) => e.TryGetProperty(field, out var v) && v.ValueKind != JsonValueKind.Null ? U(v) : null;
        return new KeyEntity(slot, I(e.GetProperty("id")), I(e.GetProperty("type")), I(e.GetProperty("status")), I(e.GetProperty("x")), I(e.GetProperty("y")), I(e.GetProperty("z")), I(e.GetProperty("c1cc")), I(e.GetProperty("c1d0")),
            OptInt(e.GetProperty("touch")), Opt("hp"), OptU("flags"), Opt("fx"), Opt("fy"), Opt("fz"), OptU("anim_t"), OptU("anim_c"), OptU("dir_t"), OptU("dir_c"), Opt("row"), Opt("animend"), Opt("b274"), Opt("b278_slot"), Opt("c20"), Opt("slope"), Opt("ground"), Opt("adj13c"),
            e.TryGetProperty("hb", out var hb) ? hb.EnumerateArray().Select(I).ToArray() : null, Opt("atk_attr"), Opt("atk_pow"));
    }

    private static int I(JsonElement element) => unchecked((int)element.GetInt64());

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());

    private static int? OptInt(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : I(element);

    /// <summary>The slot an attached effect's anchor (<c>a3</c>, a pointer into the table of entities at <c>0x80127D30</c>, stride <c>0x294</c>) is.</summary>
    internal static int SlotOfPointer(long pointer) => (int)((pointer - FireTome1Vectors.EntityBase) / FireTome1Vectors.EntityStride);

    private static readonly Regex Attach = new(@"^attach\(id=(\d+),a2=\d+,a3=0x([0-9a-f]+),a4=\d+\)$", RegexOptions.Compiled);
    private static readonly Regex Fx = new(@"^fx\(id=(\d+),a2=(\d+),x=(-?\d+),y=(-?\d+),z=(-?\d+)\)$", RegexOptions.Compiled);

    /// <summary>The events of a row in the normal form the DLL's run writes too: <c>fx(id,animation,x,y,z)</c> (an effect created at a raw position), <c>attach(id,slot)</c> (an effect attached to the entity of a slot) and <c>snd(id)</c> (a sound; the silent sound 0 is left out).</summary>
    internal static List<string> NormalEvents(Row row)
    {
        var events = new List<string>();
        foreach (var e in row.Events)
        {
            var attach = Attach.Match(e);
            if (attach.Success)
            {
                events.Add($"attach({attach.Groups[1].Value},{SlotOfPointer(Convert.ToInt64(attach.Groups[2].Value, 16))})");
                continue;
            }

            var fx = Fx.Match(e);
            if (fx.Success)
            {
                events.Add($"fx({fx.Groups[1].Value},{fx.Groups[2].Value},{fx.Groups[3].Value},{fx.Groups[4].Value},{fx.Groups[5].Value})");
                continue;
            }

            if (e == "snd(0)")
            {
                continue;
            }

            events.Add(e);
        }

        events.Sort(StringComparer.Ordinal);
        return events;
    }
}
