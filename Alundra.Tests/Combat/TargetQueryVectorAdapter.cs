#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Combat;
using Alundra.Entities;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.h4b1 (docs/plan-e20-heros.md section 1.31.2, D-H4B1-3): loads the replayable vectors of the target query <c>0x8003AF70</c> (the files of <c>docs/plan-e20-annexe/e20-h4b/</c>) and rebuilds each one as proxies. The reads of
/// a vector fall in classes: (i) the inputs of the call (the count word <c>[0x80134360]</c>, the fifth argument), (ii) the entity bytes (base <c>0x80127D30</c>, stride <c>0x294</c>) and the bytes of the balance records, each carried by
/// a member of the DLL; anything else is a failure of the adapter (it throws). An entity slot the vector never reads has no proxy (a status 0 for the query); the caller always has one.
/// </summary>
internal static class TargetQueryVectorAdapter
{
    private const long EntityBase = 0x80127D30;
    private const long EntityStride = 0x294;
    private const long CountWord = 0x80134360;
    private const long FifthArgument = 0x801FFF10;
    private const long OutputPointers = 0x800CC058;

    internal sealed class Vector
    {
        public string File = "";
        public int Index;
        public string? Case;
        public int Count;
        public int CallerSlot;
        public uint Mask;
        public int Type;
        public EntityScriptProxy Caller = null!;

        /// <summary>The proxies in the order of the slots (the compact list of the born entities).</summary>
        public List<EntityScriptProxy> Entities = new();

        /// <summary>The slots the real code wrote in the pointer array, in order.</summary>
        public List<int> RealSlots = new();
    }

    internal static string FindUp(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("not found above the test binaries: " + relativePath);
    }

    internal static readonly string[] Files =
    {
        "docs/plan-e20-annexe/e20-h4b/bin/vec/vec_query_8003AF70.json",
        "docs/plan-e20-annexe/e20-h4b/critic/vec_extra/vec_extra_query.json",
    };

    internal static List<Vector> LoadAll()
    {
        var all = new List<Vector>();
        foreach (var file in Files)
        {
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(FindUp(file.Replace('/', Path.DirectorySeparatorChar))));
            var index = 0;
            foreach (var element in document.RootElement.GetProperty("vectors").EnumerateArray())
            {
                all.Add(Build(Path.GetFileName(file), index++, element));
            }
        }

        return all;
    }

    private static Vector Build(string file, int index, JsonElement element)
    {
        var bytes = new Dictionary<long, byte>();
        var sheetBytes = new HashSet<long>();
        var entitySlots = new SortedSet<int>();
        foreach (var read in element.GetProperty("reads").EnumerateArray())
        {
            var address = read[0].GetInt64();
            var size = read[1].GetInt32();
            var value = read[2].GetInt64();
            for (var i = 0; i < size; i++)
            {
                bytes[address + i] = (byte)(value >> (8 * i));
            }

            if (address >= EntityBase && address < EntityBase + 64 * EntityStride)
            {
                entitySlots.Add((int)((address - EntityBase) / EntityStride));
            }
            else if (address != CountWord && address != FifthArgument)
            {
                if (size != 1)
                {
                    throw new InvalidOperationException($"{file} #{index}: read of {size} bytes at 0x{address:X} in no class");
                }

                sheetBytes.Add(address);
            }
        }

        uint Word(long address)
        {
            uint word = 0;
            for (var i = 0; i < 4; i++)
            {
                word |= (uint)(bytes.TryGetValue(address + i, out var b) ? b : 0) << (8 * i);
            }

            return word;
        }

        var args = element.GetProperty("args");
        var callerAddress = args[0].GetInt64();
        var callerSlot = (int)((callerAddress - EntityBase) / EntityStride);
        var type = (int)args[2].GetInt64();
        entitySlots.Add(callerSlot);

        var vector = new Vector
        {
            File = file,
            Index = index,
            Case = element.TryGetProperty("case", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
            Count = (int)Word(CountWord),
            CallerSlot = callerSlot,
            Mask = (uint)args[1].GetInt64(),
            Type = type,
        };

        foreach (var slot in entitySlots)
        {
            var baseAddress = EntityBase + slot * EntityStride;
            var proxy = new EntityScriptProxy
            {
                Slot = slot,
                Status = (EntityStatus)(int)Word(baseAddress + 0x10),
                Flags = Word(baseAddress + 0x6C),
                HitAnimByte = (int)Word(baseAddress + 0xB4),
                PosX = (int)Word(baseAddress + 0x114),
                PosY = (int)Word(baseAddress + 0x118),
                DamagedTickCounter = (int)Word(baseAddress + 0x1CC),
                FrameCollisionTickCounter = (int)Word(baseAddress + 0x1D0),
            };
            if (Word(baseAddress + 0x20) != 0)
            {
                proxy.BlockedByEntity = new Entity { Name = "blocker" };
            }

            var sheet = Word(baseAddress + 0x1C4);
            if (sheet != 0)
            {
                // The only byte of the sheet the query reads is the one at sheet + type + 2 (the offset type + 2 of the record).
                var responseByte = bytes.TryGetValue(sheet + type + 2, out var b) ? b : (byte)0;
                sheetBytes.Remove(sheet + type + 2);
                proxy.BalanceRecord = SheetWithByteAt(type + 2, responseByte);
            }

            vector.Entities.Add(proxy);
            if (slot == callerSlot)
            {
                vector.Caller = proxy;
            }
        }

        if (sheetBytes.Count != 0)
        {
            throw new InvalidOperationException($"{file} #{index}: a byte read outside the entities is not the sheet byte at sheet + type + 2: 0x{sheetBytes.First():X}");
        }

        var writes = element.GetProperty("writes").EnumerateArray().ToDictionary(w => (w[0].GetInt64(), w[1].GetInt32()), w => w[2].GetInt64());
        var returned = (int)element.GetProperty("ret").GetInt64();
        for (var k = 0; k < returned; k++)
        {
            vector.RealSlots.Add((int)((writes[(OutputPointers + 4 * k, 4)] - EntityBase) / EntityStride));
        }

        return vector;
    }

    /// <summary>A sheet whose <see cref="BalanceRecord.ByteAt"/> answers <paramref name="value"/> at <paramref name="offset"/> (2 to 13 are the bytes the query can read with a type of 0 to 11).</summary>
    private static BalanceRecord SheetWithByteAt(int offset, byte value)
    {
        if (offset < 2 || offset > 13)
        {
            throw new InvalidOperationException($"the offset {offset} of the sheet has no member in the record");
        }

        var responses = new byte[11];
        var maxHp = 0;
        if (offset == 2)
        {
            maxHp = value;
        }
        else
        {
            responses[offset - 3] = value;
        }

        return new BalanceRecord(0, 15, maxHp, responses, Array.Empty<BalanceAttack>(), null);
    }
}
