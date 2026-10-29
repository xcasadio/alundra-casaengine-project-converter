#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1: the vectors of the oracle of the fire tome 1 (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine, replayed by T0 from copies, identical on two runs): the 400 of the cancellation <c>0x80034D2C</c>, the 288 of E3 <c>0x8007EDA0</c> and the 3 524 of C63 <c>0x8007AC60</c>
/// (<c>e20-h4d2/disc/vec/</c>; the 87 of the pilot are the phase A2's). Read by address: <c>reads</c> (the first read of an address, with its size) and <c>writes</c> (the last value written), as the vectors of the other h4 bricks. The vectors flagged <c>binary_only</c> (a non-zero <c>+0x1B8</c> or <c>+0x28</c>
/// of a destroyed entity: no member of the DLL carries them) are skipped by their flag.
/// </summary>
internal static class FireTome1Vectors
{
    internal const long EntityBase = 0x80127D30;
    internal const long EntityStride = 0x294;
    internal const long CountWord = 0x80134360;
    internal const long GeneratorWord = 0x80098708;

    /// <summary>The address of the word <paramref name="offset"/> of the slot <paramref name="slot"/> of the table of entities.</summary>
    internal static long Word(int slot, int offset) => EntityBase + EntityStride * slot + offset;

    /// <summary>The slot an address of the table belongs to (it must belong to it).</summary>
    internal static int SlotOf(long address) => (int)((address - EntityBase) / EntityStride);

    /// <summary>True when the address is a word of an entity of the table (64 slots).</summary>
    internal static bool IsEntityWord(long address) => address >= EntityBase && address < EntityBase + 64 * EntityStride;

    /// <summary>One vector: the arguments, the words the code read and wrote, the calls to the stubs, and the kind of the C63 vectors (<c>turn</c> for the grid of the 32 x 32 directions).</summary>
    internal sealed record Vector(int Id, string? Kind, bool BinaryOnly, long Argument, IReadOnlyDictionary<long, (int Size, long Value)> Reads, IReadOnlyDictionary<long, (int Size, long Value)> Writes,
        IReadOnlyList<(string Name, IReadOnlyList<long> Arguments)> Journal, int TargetDirection)
    {
        internal bool Read(long address) => Reads.ContainsKey(address);

        internal long ReadWord(long address, long fallback = 0) => Reads.TryGetValue(address, out var read) ? read.Value : fallback;

        internal int ReadInt(long address, int fallback = 0) => unchecked((int)(uint)ReadWord(address, (uint)fallback));

        internal bool Wrote(long address) => Writes.ContainsKey(address);

        internal long WroteWord(long address) => Writes[address].Value;

        internal int WroteInt(long address) => unchecked((int)(uint)Writes[address].Value);
    }

    /// <summary>The 400 vectors of the cancellation (319 comparable).</summary>
    internal static IReadOnlyList<Vector> Cancel() => Load("disc/vec/vec_cancel_fire1_80034D2C.json");

    /// <summary>The 288 vectors of E3 (the entity is the argument; <c>+0x88</c> the animation asked, <c>+0xAC</c> the end of the animation, <c>+0x6C</c> the flags).</summary>
    internal static IReadOnlyList<Vector> E3() => Load("disc/vec/vec_e3_8007EDA0.json");

    /// <summary>The 3 524 vectors of C63 (2 500 random worlds and the 1 024 pairs of directions of the grid <c>turn</c>).</summary>
    internal static IReadOnlyList<Vector> Ball() => Load("disc/vec/vec_c63_8007AC60.json");

    private static IReadOnlyList<Vector> Load(string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.FireTome1Annex(relative)));
        return document.RootElement.GetProperty("vectors").EnumerateArray().Select(v => new Vector(
            v.GetProperty("id").GetInt32(),
            v.TryGetProperty("kind", out var kind) ? kind.GetString() : null,
            v.GetProperty("binary_only").GetBoolean(),
            v.GetProperty("args").GetArrayLength() > 0 ? v.GetProperty("args")[0].GetInt64() : 0,
            ByAddress(v.GetProperty("reads")),
            ByAddress(v.GetProperty("writes")),
            v.TryGetProperty("journal", out var journal)
                ? journal.EnumerateArray().Select(j => (j[1].GetString()!, (IReadOnlyList<long>)j[2].EnumerateArray().Select(a => a.GetInt64()).ToList())).ToList()
                : new List<(string, IReadOnlyList<long>)>(),
            v.TryGetProperty("target_dir", out var direction) ? direction.GetInt32() : -1)).ToList();
    }

    private static Dictionary<long, (int Size, long Value)> ByAddress(JsonElement words)
    {
        var map = new Dictionary<long, (int, long)>();
        foreach (var word in words.EnumerateArray())
        {
            map[word[0].GetInt64()] = (word[1].GetInt32(), word[2].GetInt64());
        }

        return map;
    }
}
