#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T1: the value vectors of the opcodes <c>0xA9</c> and <c>0xAA</c>, 202 lines taken from <c>docs/plan-e19-sm-annexe/sm3/vectors/sm3-vectors.jsonl</c> (E19.sm3, rule R-SM-5) and kept in
/// <c>docs/plan-e20-annexe/e20-g5/g5b/vectors/sm3-a9-aa-vectors.jsonl</c>. Each line is one execution of the REAL handler of <c>ALUN_CD.EXE</c> (France) in the MIPS interpreter of the annex, on an explicit world:
/// the slots, the instruction bytes, the logic entity, the <c>Result</c> before it, and what the binary left in <c>Result</c> and in the length. The file is pinned by its SHA-256 after the line endings are normalised.
/// </summary>
internal static class ContactVectors
{
    public const string Sha256 = "67c8a4264744265b67ddf94b4de4dfc01e63297cf1a11845f872c87d544e6643";

    /// <summary>One vector: the world to build, the instruction, and the binary's answer.</summary>
    internal sealed record Vector(string Id, int Opcode, int[] Instruction, int LogicSlot, int ResultBefore, int ExpectResult, int ExpectLength, JsonElement Slots);

    private static readonly Lazy<IReadOnlyList<Vector>> All = new(Load);

    public static IReadOnlyList<Vector> Vectors => All.Value;

    public static Vector Get(string id) => Vectors.Single(v => v.Id == id);

    private static IReadOnlyList<Vector> Load()
    {
        var text = File.ReadAllText(AnnexFiles.ButtonAnnex("vectors/sm3-a9-aa-vectors.jsonl")).Replace("\r\n", "\n");
        Assert.Equal(Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());

        var list = new List<Vector>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Cloned out of the document: the slots stay readable after it is disposed.
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var instruction = root.GetProperty("instr").EnumerateArray().Select(e => Convert.ToInt32(e.GetString(), 16)).ToArray();
            list.Add(new Vector(
                root.GetProperty("id").GetString()!,
                instruction[0],
                instruction,
                root.GetProperty("ref").GetInt32(),
                root.GetProperty("result_before").GetInt32(),
                root.GetProperty("expect").GetProperty("result").GetInt32(),
                root.GetProperty("expect").GetProperty("v0").GetInt32(),
                root.GetProperty("slots").Clone()));
        }

        return list;
    }

    /// <summary>
    /// Builds the world of a vector (the montage M1): slot 0 is the hero; a slot of status 0 is free (nothing is created); the others are proxies in slot order with their status, <c>EntityRefId</c>, flags, animation flags,
    /// <c>ForceZ</c> and target animation; the pointers are set in a second pass: <c>XCollisionEntity</c> is the proxy of the designated slot, <c>PlatformEntity</c>, <c>RidingEntity</c> and <c>ParentEntity</c> are the
    /// <see cref="EntityScriptProxy.LogicContextEntity"/> of the designated slot.
    /// </summary>
    public static OpcodeWorld Build(Vector vector)
    {
        var world = new OpcodeWorld();
        var slots = vector.Slots.EnumerateArray().ToList();
        for (var i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            var status = s.GetProperty("status").GetInt32();
            EntityScriptProxy? proxy;
            if (i == 0)
            {
                proxy = world.Hero;
            }
            else if (status == 0)
            {
                world.AddHole();
                continue;
            }
            else
            {
                proxy = world.Add(s.GetProperty("ref_id").GetInt32());
            }

            proxy.Status = (EntityStatus)status;
            proxy.EntityRefId = s.GetProperty("ref_id").GetInt32();
            proxy.Flags = unchecked((uint)s.GetProperty("flags").GetInt64());
            proxy.AnimFlags = s.GetProperty("anim_flags").GetInt32();
            proxy.ForceZ = s.GetProperty("force_z").GetInt32();
            proxy.TargetAnimationId = unchecked((uint)s.GetProperty("target_anim").GetInt64());
        }

        for (var i = 0; i < slots.Count; i++)
        {
            if (world.Slots[i] is not { } proxy)
            {
                continue;
            }

            var s = slots[i];
            if (Pointer(s, "xcoll") is { } xc)
            {
                proxy.XCollisionEntity = world.Slots[xc];
            }

            if (Pointer(s, "platform") is { } platform)
            {
                proxy.PlatformEntity = world.Slots[platform]!.LogicContextEntity;
            }

            if (Pointer(s, "riding") is { } riding)
            {
                proxy.RidingEntity = world.Slots[riding]!.LogicContextEntity;
            }

            if (Pointer(s, "parent") is { } parent)
            {
                proxy.ParentEntity = world.Slots[parent]!.LogicContextEntity;
            }
        }

        return world;
    }

    private static int? Pointer(JsonElement slot, string name)
        => slot.GetProperty(name).ValueKind == JsonValueKind.Null ? null : slot.GetProperty(name).GetInt32();

    /// <summary>
    /// Plays a vector on the DLL (the owner is the logic entity, <c>LogicEntity</c> unset) and checks what the binary did: <c>Result</c> equals the vector's, the length is the table size, the trace shows the opcode
    /// run as implemented, and nothing else but <c>Result</c> moved. <paramref name="expected"/> is the value the test writes from the tests table; the vector's own answer must equal it.
    /// </summary>
    public static void Check(string id, int expected)
    {
        var vector = Get(id);
        Assert.Equal(expected, vector.ExpectResult);

        var world = Build(vector);
        var owner = world.Slots[vector.LogicSlot]!;
        var before = world.Snapshot();
        var (state, trace) = world.Run(owner, vector.ResultBefore, vector.Instruction);

        Assert.Equal(expected, state.Result);
        Assert.Equal(vector.ExpectLength, state.CodeIndex);
        OpcodeWorld.AssertImplemented(trace, vector.Opcode, vector.ExpectLength);
        Assert.Equal(before, world.Snapshot());
        Assert.All(state.Parameters.Skip(2), p => Assert.Equal(0, p));
    }
}
