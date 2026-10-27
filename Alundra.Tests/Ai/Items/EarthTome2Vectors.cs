#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1: the cases and the vectors of the oracle of the earth tome 2 (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine, replayed in T0 from copies): the 609 cases of the driver <c>0x800354D0</c>
/// (<c>disc/keys/driver_2c_cases.json</c>, with the slot index words written and the per-frame step of the generator) and the 40 vectors of E13 <c>0x8007F30C</c> (<c>e20-h4b/bin/vec/vec_e13_8007F30C.json</c>), read by
/// address like the vectors of the h4b bricks (<c>reads</c>, <c>writes</c>).
/// </summary>
internal static class EarthTome2Vectors
{
    /// <summary>The state of the entity in the table of the cases: 1 loaded, 2 normal, 3 deactivated, 4 to destroy (the values of <c>EntityStatus</c>).</summary>
    internal sealed record Other(int Slot, int Status, int Type);

    internal sealed record OtherAfter(int Slot, int Status, int B20, int Animation);

    /// <summary>One birth the driver asked for: the call of <c>SpawnWarpEntity</c> as the machine's stub journalled it (the Z is the binary's, the DLL's plus 1).</summary>
    internal sealed record Birth(int Type, int X, int Y, int Z, int Direction);

    /// <summary>The entity the stub gave back (<c>pool</c>): its status after the driver (4 when destroyed) and the Z force written on it.</summary>
    internal sealed record Born(int Status, uint ForceZ);

    internal sealed record DriverCase(int Index, int Phase, int Counter, uint HeroAnimation, int Slope, int HeroX, int HeroY, int HeroZRaw, uint Seed, IReadOnlyList<string> Spawns, string Terrain, IReadOnlyList<Other> Others,
        int Return, int PhaseAfter, int CounterAfter, uint ItemAfter, uint HeroAnimationAfter, uint SeedAfter, IReadOnlyList<Birth> Births, IReadOnlyList<Born> Pool, IReadOnlyList<OtherAfter> OthersAfter);

    internal static IReadOnlyList<DriverCase> DriverCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome2Annex("disc/keys/driver_2c_cases.json")));
        return document.RootElement.EnumerateArray().Select((c, index) =>
        {
            var i = c.GetProperty("inputs");
            return new DriverCase(
                index,
                i.GetProperty("phase").GetInt32(), i.GetProperty("counter").GetInt32(), U(i.GetProperty("hero_anim")), i.GetProperty("slope").GetInt32(),
                I(i.GetProperty("hero_x")), I(i.GetProperty("hero_y")), I(i.GetProperty("hero_z_raw")), U(i.GetProperty("seed")),
                i.GetProperty("spawns").EnumerateArray().Select(s => s.GetString()!).ToList(), i.GetProperty("terrain").GetString()!,
                i.GetProperty("others").EnumerateArray().Select(o => new Other(o[0].GetInt32(), o[1].GetInt32(), o[2].GetInt32())).ToList(),
                c.GetProperty("ret").GetInt32(), c.GetProperty("phase_after").GetInt32(), c.GetProperty("counter_after").GetInt32(), U(c.GetProperty("item_after")), U(c.GetProperty("hero_anim_after")), U(c.GetProperty("rng_after")),
                c.GetProperty("journal").EnumerateArray().Select(j => new Birth(j[3].GetInt32(), I(j[4]), I(j[5]), I(j[6]), j[7].GetInt32())).ToList(),
                c.GetProperty("pool").EnumerateArray().Select(p => new Born(p.GetProperty("status").GetInt32(), U(p.GetProperty("force_z")))).ToList(),
                c.GetProperty("others_after").EnumerateArray().Select(o => new OtherAfter(o.GetProperty("slot").GetInt32(), o.GetProperty("status").GetInt32(), o.GetProperty("b20").GetInt32(), o.GetProperty("anim").GetInt32())).ToList());
        }).ToList();
    }

    /// <summary>One vector of the h4b bricks: the arguments and the words the code read (first read of an address) and wrote (last value of an address), by address.</summary>
    internal sealed record Vector(int Id, IReadOnlyList<long> Args, IReadOnlyDictionary<long, long> Reads, IReadOnlyDictionary<long, long> Writes, bool JournalDestroy)
    {
        internal long Read(long address, long fallback = 0) => Reads.TryGetValue(address, out var value) ? value : fallback;

        internal int ReadInt(long address) => unchecked((int)(uint)Read(address));

        internal bool Wrote(long address) => Writes.ContainsKey(address);

        internal int WroteInt(long address) => unchecked((int)(uint)Writes[address]);
    }

    internal static IReadOnlyList<Vector> Vectors(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.H4bVector(file)));
        return document.RootElement.GetProperty("vectors").EnumerateArray().Select(v => new Vector(
            v.GetProperty("id").GetInt32(),
            v.GetProperty("args").EnumerateArray().Select(a => a.GetInt64()).ToList(),
            ByAddress(v.GetProperty("reads")),
            ByAddress(v.GetProperty("writes")),
            v.TryGetProperty("journal_destroy", out var destroy) && destroy.GetBoolean())).ToList();
    }

    private static Dictionary<long, long> ByAddress(JsonElement words)
    {
        var map = new Dictionary<long, long>();
        foreach (var word in words.EnumerateArray())
        {
            map[word[0].GetInt64()] = word[2].GetInt64();
        }

        return map;
    }

    private static int I(JsonElement element) => unchecked((int)element.GetInt64());

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());
}
