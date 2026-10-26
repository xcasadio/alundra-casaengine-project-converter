#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// One self-contained vector of the oracle of E20.g2c2 (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine: every data word the function READS, the words it WRITES when they change, the journal of its calls): the 30 vectors of C70 (<c>0x8007B7B0</c>), the 30 of E20
/// (<c>0x8007F6C8</c>) and the 30 of D34 (<c>0x8007E8AC</c>) of <c>docs/plan-e20-annexe/e20-g2c2/vectors/goldens_g2c2.json</c>. The words are keyed by absolute address; <see cref="Read"/>, <see cref="After"/> and <see cref="Wrote"/> give a field by its offset from the entity, <see cref="WordAt"/> a word of another
/// object by its address (the carrier, the contact, the entity the spawn returns).
/// </summary>
internal sealed class TorchVector
{
    private TorchVector(string function, int index, uint entity, IReadOnlyDictionary<uint, uint> reads, IReadOnlyDictionary<uint, uint> writes, IReadOnlyList<(string Kind, uint[] Arguments)> journal, uint spawnReturn)
    {
        Function = function;
        Index = index;
        Entity = entity;
        Reads = reads;
        Writes = writes;
        Journal = journal;
        SpawnReturn = spawnReturn;
    }

    /// <summary>The name of the function: C70, E20 or D34.</summary>
    internal string Function { get; }

    /// <summary>The number of the case in the file.</summary>
    internal int Index { get; }

    /// <summary>The address of the entity (<c>self</c>).</summary>
    internal uint Entity { get; }

    /// <summary>The words the real code read, by address.</summary>
    internal IReadOnlyDictionary<uint, uint> Reads { get; }

    /// <summary>The words the real code wrote and that changed, by address (a byte store lists the word that holds it).</summary>
    internal IReadOnlyDictionary<uint, uint> Writes { get; }

    /// <summary>The calls of the journal in order: <c>spawn</c> (the parent, the page, the type, X, Y, Z, the direction), <c>destroy2</c> (the entity and the effect argument).</summary>
    internal IReadOnlyList<(string Kind, uint[] Arguments)> Journal { get; }

    /// <summary>The address <c>SpawnWarpEntity</c> returned in the machine's run (0: the table was full).</summary>
    internal uint SpawnReturn { get; }

    /// <summary>True when the real code read the word at <paramref name="offset"/> from the entity.</summary>
    internal bool HasRead(uint offset) => Reads.ContainsKey(Entity + offset);

    /// <summary>The word read at <paramref name="offset"/> from the entity, 0 when the real code did not read it (the field is then free).</summary>
    internal uint Read(uint offset) => Reads.TryGetValue(Entity + offset, out var word) ? word : 0;

    /// <summary>True when the real code wrote the word at <paramref name="offset"/> (and it changed).</summary>
    internal bool Wrote(uint offset) => Writes.ContainsKey(Entity + offset);

    /// <summary>The word at <paramref name="offset"/> after the call: the written one, otherwise <paramref name="seed"/> (the value the test posed in a field the vector left free, or the one it read).</summary>
    internal uint After(uint offset, uint seed) => Writes.TryGetValue(Entity + offset, out var word) ? word : seed;

    /// <summary>The word of an absolute address, 0 when the real code did not read it.</summary>
    internal uint WordAt(uint address) => Reads.TryGetValue(address, out var word) ? word : 0;

    /// <summary>The byte at the address <paramref name="pointer"/> in the memory the vector read (little endian words, the byte read where the pointer falls in its word).</summary>
    internal int ByteAt(uint pointer) => (int)((WordAt(pointer & ~3u) >> (int)(8 * (pointer & 3))) & 0xFF);

    /// <summary>The vectors of the function <paramref name="function"/> of <c>vectors/goldens_g2c2.json</c>.</summary>
    internal static IReadOnlyList<TorchVector> Load(string function)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.TorchAnnex("vectors/goldens_g2c2.json")));
        var list = new List<TorchVector>();
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            if (vector.GetProperty("fn").GetString() != function)
            {
                continue;
            }

            var journal = vector.GetProperty("journal").EnumerateArray()
                .Select(j => (j[0].GetString()!, j.EnumerateArray().Skip(1).Select(a => unchecked((uint)a.GetInt64())).ToArray()))
                .ToList();
            list.Add(new TorchVector(function, vector.GetProperty("case").GetInt32(), Hex(vector.GetProperty("self").GetString()!), Words(vector.GetProperty("reads")), Words(vector.GetProperty("writes")), journal, Hex(vector.GetProperty("spawn_ret").GetString()!)));
        }

        return list;
    }

    private static uint Hex(string text) => uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static Dictionary<uint, uint> Words(JsonElement element)
        => element.EnumerateObject().ToDictionary(p => Hex(p.Name), p => unchecked((uint)p.Value.GetInt64()));
}
