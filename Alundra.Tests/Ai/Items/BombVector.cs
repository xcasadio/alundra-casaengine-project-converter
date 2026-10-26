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
/// One self-contained vector of the oracle of E20.h3a (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine: every data word the function READS, the words it WRITES when they change, the journal of its calls): the 25 vectors of D30 and the 25 of E12
/// (<c>docs/plan-e20-annexe/e20-h3/critic/results/goldens_critic.json</c>), the 25 of E19 (<c>docs/plan-e20-annexe/e20-h3a/goldens_e19.json</c>). The words are keyed by absolute address; <see cref="Read"/> and <see cref="After"/> give a field by its offset from the entity.
/// </summary>
internal sealed class BombVector
{
    private BombVector(string function, uint entity, IReadOnlyDictionary<uint, uint> reads, IReadOnlyDictionary<uint, uint> writes, IReadOnlyList<(string Kind, uint[] Arguments)> journal)
    {
        Function = function;
        Entity = entity;
        Reads = reads;
        Writes = writes;
        Journal = journal;
    }

    /// <summary>The name of the function: D30, E12 or E19.</summary>
    internal string Function { get; }

    /// <summary>The address of the entity (<c>self</c>).</summary>
    internal uint Entity { get; }

    /// <summary>The words the real code read, by address.</summary>
    internal IReadOnlyDictionary<uint, uint> Reads { get; }

    /// <summary>The words the real code wrote and that changed, by address.</summary>
    internal IReadOnlyDictionary<uint, uint> Writes { get; }

    /// <summary>The calls of the journal in order: <c>sound</c> (the id), <c>cate</c> (the entity), <c>destroy2</c> (the entity and the effect argument).</summary>
    internal IReadOnlyList<(string Kind, uint[] Arguments)> Journal { get; }

    /// <summary>The word read at <paramref name="offset"/> from the entity, 0 when the real code did not read it (the field is then free).</summary>
    internal uint Read(uint offset) => Reads.TryGetValue(Entity + offset, out var word) ? word : 0;

    /// <summary>The word at <paramref name="offset"/> after the call: the written one, otherwise the one read (the oracle lists a write only when the word changed).</summary>
    internal uint After(uint offset) => Writes.TryGetValue(Entity + offset, out var word) ? word : Read(offset);

    /// <summary>True when the real code wrote the word at <paramref name="offset"/> (and it changed).</summary>
    internal bool Wrote(uint offset) => Writes.ContainsKey(Entity + offset);

    /// <summary>The word of an absolute address, 0 when the real code did not read it.</summary>
    internal uint Word(uint address) => Reads.TryGetValue(address, out var word) ? word : 0;

    /// <summary>The vectors of the function <paramref name="function"/> of the file given from <c>docs/plan-e20-annexe/</c>.</summary>
    internal static IReadOnlyList<BombVector> Load(string annexPath, string function)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.BombAnnex(annexPath)));
        var list = new List<BombVector>();
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            if (vector.GetProperty("fn").GetString() != function)
            {
                continue;
            }

            var journal = vector.GetProperty("journal").EnumerateArray()
                .Select(j => (j[0].GetString()!, j.EnumerateArray().Skip(1).Select(a => unchecked((uint)a.GetInt64())).ToArray()))
                .ToList();
            list.Add(new BombVector(function, Hex(vector.GetProperty("self").GetString()!), Words(vector.GetProperty("reads")), Words(vector.GetProperty("writes")), journal));
        }

        return list;
    }

    private static uint Hex(string text) => uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static Dictionary<uint, uint> Words(JsonElement element)
        => element.EnumerateObject().ToDictionary(p => Hex(p.Name), p => unchecked((uint)p.Value.GetInt64()));
}
