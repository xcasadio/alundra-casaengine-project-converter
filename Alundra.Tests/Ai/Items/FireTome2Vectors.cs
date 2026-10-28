#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1: the vectors of the oracle of the fire tome 2 (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine, replayed in T0 from copies, 0 failure): the 60 vectors of the pilot <c>0x80035C64</c> and the vector of its cancellation <c>0x80034E08</c>
/// (<c>e20-h4b/bin/vec/vec_fire2_80035C64.json</c>, <c>e20-h4b/critic/vec_extra/vec_extra_cancel_fire2.json</c>), the 203 vectors of E28 <c>0x8007FB58</c> (<c>t0/vec/vec_e28_8007FB58_203.json</c>: the 202 of the discovery and the seed of the faithful key).
/// Read by address: <c>reads</c> (the first read of an address) and <c>writes</c> (the last value written), as the vectors of the other h4 bricks.
/// </summary>
internal static class FireTome2Vectors
{
    internal const long Phase = 0x80127160;
    internal const long Counter = 0x8012715C;
    internal const long ItemInUse = 0x80127164;
    internal const long SpawnedPointer = 0x80127124;
    internal const long SpawnedIdentity = 0x80127128;
    internal const long BufferBase = 0x80127138;
    internal const long BlockBase = 0x800E4338;
    internal const long SlotCount = 0x80134360;
    internal const long SlotBase = 0x80127D30;
    internal const long SlotSize = 0x294;
    internal const long GeneratorWord = 0x80098708;

    /// <summary>The address of the word <paramref name="offset"/> of the slot <paramref name="slot"/> of the table of entities.</summary>
    internal static long Word(int slot, int offset) => SlotBase + SlotSize * slot + offset;

    /// <summary>One vector: the arguments, the words the code read and wrote, its answer; <c>SpawnReturn</c> and <c>SpawnCalls</c> are the stub of <c>SpawnWarpEntity</c> (the pilot).</summary>
    internal sealed record Vector(int Id, IReadOnlyDictionary<long, long> Reads, IReadOnlyDictionary<long, long> Writes, int Return, long SpawnReturn, IReadOnlyList<IReadOnlyList<long>> SpawnCalls)
    {
        internal bool Read(long address) => Reads.ContainsKey(address);

        internal long ReadWord(long address, long fallback = 0) => Reads.TryGetValue(address, out var value) ? value : fallback;

        internal int ReadInt(long address, int fallback = 0) => unchecked((int)(uint)ReadWord(address, (uint)fallback));

        internal bool Wrote(long address) => Writes.ContainsKey(address);

        internal int WroteInt(long address) => unchecked((int)(uint)Writes[address]);
    }

    /// <summary>The 60 vectors of the pilot.</summary>
    internal static IReadOnlyList<Vector> Pilot() => Load(AnnexFiles.H4bVector("vec_fire2_80035C64.json"));

    /// <summary>The vector of the critic for the cancellation 0x80034E08 at the zero buffer.</summary>
    internal static IReadOnlyList<Vector> Cancel() => Load(AnnexFiles.WaterDropAnnex("e20-h4b/critic/vec_extra/vec_extra_cancel_fire2.json"));

    private static IReadOnlyList<Vector> Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("vectors").EnumerateArray().Select((v, index) => new Vector(
            v.TryGetProperty("id", out var id) ? id.GetInt32() : index,
            ByAddress(v.GetProperty("reads")),
            ByAddress(v.GetProperty("writes")),
            v.GetProperty("ret").GetInt32(),
            v.TryGetProperty("stub_spawn_ret", out var spawn) ? spawn.GetInt64() : 0,
            v.TryGetProperty("spawn_calls", out var calls) ? calls.EnumerateArray().Select(c => (IReadOnlyList<long>)c.EnumerateArray().Select(a => a.GetInt64()).ToList()).ToList() : new List<IReadOnlyList<long>>())).ToList();
    }

    /// <summary>One vector of E28: the words read and written, the effects created and the destructions asked, as the stubs journalled them.</summary>
    internal sealed record ColumnVector(int Id, uint SeedBefore, uint SeedAfter, int X, int Y, int ZRaw, IReadOnlyList<(int Bank, int Effect, int Animation, int X, int Y, int Z)> Effects, int Destructions, long DestroyArgument);

    /// <summary>The 203 vectors of E28. The column is the slot 5 of the table of the machine.</summary>
    internal static IReadOnlyList<ColumnVector> Column()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.FireTome2Annex("t0/vec/vec_e28_8007FB58_203.json")));
        return document.RootElement.GetProperty("vectors").EnumerateArray().Select(v =>
        {
            var reads = ByAddress(v.GetProperty("reads"));
            var writes = ByAddress(v.GetProperty("writes"));
            var destroys = v.GetProperty("destroy_calls").EnumerateArray().ToList();
            return new ColumnVector(
                v.GetProperty("id").GetInt32(),
                (uint)reads[GeneratorWord], (uint)writes[GeneratorWord],
                unchecked((int)reads[Word(5, 0x114)]), unchecked((int)reads[Word(5, 0x118)]), unchecked((int)reads[Word(5, 0x11C)]),
                v.GetProperty("effect_calls").EnumerateArray().Select(c => (unchecked((int)c[0].GetInt64()), unchecked((int)c[1].GetInt64()), unchecked((int)c[2].GetInt64()), unchecked((int)c[3].GetInt64()), unchecked((int)c[4].GetInt64()), unchecked((int)c[5].GetInt64()))).ToList(),
                destroys.Count, destroys.Count == 0 ? 0 : destroys[0][1].GetInt64());
        }).ToList();
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
}
