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
/// E20.g5b T1 (docs/plan-e20-heros.md section 1.24.6, D-G5B-1), the tests taken from E19.sm4 (docs/plan-e19-opcodes.md section 1.4.5, <c>docs/plan-e19-sm-annexe/sm4/report.md</c> section 3.2) that bear on the opcode
/// <c>0x72</c> (Script_114_072 @ 0x8003FA24, size 1): on the LOGIC entity, <c>LastTargetAnimationId</c> (+0x26C) = <c>TargetAnimationId</c> (+0x88) and <c>LastTargetDirection</c> (+0x270) = <c>TargetDirection</c> (+0x8C);
/// no guard on the status, no search, <c>Result</c> untouched, it never reads the owner. Expected values are the binary's (the real handler in the MIPS interpreter, <c>docs/plan-e19-sm-annexe/sm4/vectors/op72.json</c>).
/// All red at the start HEAD, where the opcode is skipped by its size (1) and nothing is copied.
/// </summary>
public sealed class LastTargetOpcodeTests
{
    private const uint Max = 0xFFFFFFFF;

    private readonly record struct Words(uint Anim, uint Dir, uint LastAnim, uint LastDir);

    private static Words? WordsOf(EntityScriptProxy? e)
        => e is null ? null : new Words(e.TargetAnimationId, e.TargetDirection, e.LastTargetAnimationId, e.LastTargetDirection);

    private static EntityScriptProxy Entity(OpcodeWorld world, uint anim, uint dir, uint lastAnim, uint lastDir, EntityStatus status = EntityStatus.Normal)
    {
        var entity = world.Add(5, status);
        entity.TargetAnimationId = anim;
        entity.TargetDirection = dir;
        entity.LastTargetAnimationId = lastAnim;
        entity.LastTargetDirection = lastDir;
        return entity;
    }

    private static void AssertRan(EventProgramState state, List<EventTraceRecord> trace)
    {
        Assert.Equal(OpcodeWorld.ResultBefore, state.Result);
        Assert.Equal(1, state.CodeIndex);
        OpcodeWorld.AssertImplemented(trace, 0x72, 1);
    }

    /// <summary>U72-1: the logic entity is the owner, Target (5, 9), Last (1, 2): Last becomes (5, 9), Target is left as it is.</summary>
    [Fact]
    public void SetLastTarget_0x72_CopiesTheTargetOntoLastTarget()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, 5, 9, 1, 2);

        var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, 0x72);

        Assert.Equal((5u, 9u), (entity.LastTargetAnimationId, entity.LastTargetDirection));
        Assert.Equal((5u, 9u), (entity.TargetAnimationId, entity.TargetDirection));
        AssertRan(state, trace);
    }

    /// <summary>U72-2: the owner is the hero, the logic entity an NPC with Target (3, 21), Last (99, 98): the NPC's Last becomes (3, 21), the hero's Last stays (0, 0).</summary>
    [Fact]
    public void SetLastTarget_0x72_ActsOnTheLogicEntity_NotOnTheOwner()
    {
        var world = new OpcodeWorld();
        var npc = Entity(world, 3, 21, 99, 98);
        world.Hero.TargetAnimationId = 40;
        world.Hero.TargetDirection = 41;
        world.Hero.LogicEntity = npc;

        var (state, trace) = world.Run(world.Hero, OpcodeWorld.ResultBefore, 0x72);

        Assert.Equal((3u, 21u), (npc.LastTargetAnimationId, npc.LastTargetDirection));
        Assert.Equal((0u, 0u), (world.Hero.LastTargetAnimationId, world.Hero.LastTargetDirection));
        AssertRan(state, trace);
    }

    /// <summary>U72-3: Target (0xFFFFFFFF, 0xFFFFFFFF), Last (0, 0): the values are unsigned 32-bit words, copied verbatim.</summary>
    [Fact]
    public void SetLastTarget_0x72_CopiesMaxUnsignedValuesVerbatim()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, Max, Max, 0, 0);

        var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, 0x72);

        Assert.Equal((Max, Max), (entity.LastTargetAnimationId, entity.LastTargetDirection));
        AssertRan(state, trace);
    }

    /// <summary>U72-4: Target (0, 0), Last (0x41, 0x1F): zero is a value, Last becomes (0, 0).</summary>
    [Fact]
    public void SetLastTarget_0x72_ZeroTargetOverwritesANonZeroLast()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, 0, 0, 0x41, 0x1F);

        var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, 0x72);

        Assert.Equal((0u, 0u), (entity.LastTargetAnimationId, entity.LastTargetDirection));
        AssertRan(state, trace);
    }

    /// <summary>U72-5: the logic entity is <c>FlagToDestroy</c>, Target (8, 2), Last (0, 0): Last becomes (8, 2), the opcode has no guard on the status.</summary>
    [Fact]
    public void SetLastTarget_0x72_HasNoStatusGuard()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, 8, 2, 0, 0, EntityStatus.FlagToDestroy);

        var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, 0x72);

        Assert.Equal((8u, 2u), (entity.LastTargetAnimationId, entity.LastTargetDirection));
        AssertRan(state, trace);
    }

    /// <summary>
    /// U72-6: a program C <c>[00, FF]</c> that waits (the Break keeps it resumable) and a program F <c>[09 0A, 1A 07, 72, FF]</c> (the tables C = [0], F = [2]); the calls RunScript(C), RunScript(F), RunScript(C) with Target (3, 5) at
    /// the start. The departure of the program F saves Target in Last (3, 5); F sets Target (7, 10) and its 0x72 copies it into Last; the resumed C restores Target from Last: (7, 10). Without the opcode the resume gives back
    /// the pre-F Target (3, 5).
    /// </summary>
    [Fact]
    public void SetLastTarget_0x72_ATouchProgramEndingWithIt_SurvivesTheResumeOfTheTickProgram()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, 3, 5, 0, 0);
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x80;
        entity.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x80;
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new int[6],
            EventCodesCTable = new[] { 0 },
            EventCodesFTable = new[] { 2 },
            Codes = new[] { 0x00, 0xFF, 0x09, 0x0A, 0x1A, 0x07, 0x72, 0xFF },
        };
        var runner = new EventProgramRunner(document, world.GameState, world.Ctx);

        runner.RunScript(entity, ScriptHelper.ProgramCTick);
        runner.RunScript(entity, ScriptHelper.ProgramFInteract);

        Assert.Equal((7u, 10u), (entity.LastTargetAnimationId, entity.LastTargetDirection));

        runner.RunScript(entity, ScriptHelper.ProgramCTick);

        Assert.Equal((7u, 10u), (entity.TargetAnimationId, entity.TargetDirection));
    }

    /// <summary>
    /// New (the pattern of U-ALL-01): the size of the binary is 1 (the next instruction, <c>1A 09</c>, is reached) and <c>Result</c> is left as it was; the trace shows the opcode run as implemented, not skipped, and the
    /// copy is done (Target (4, 6), Last (0, 0) before).
    /// </summary>
    [Fact]
    public void SetLastTarget_0x72_ReturnsItsTableSize1_AndLeavesResultAlone()
    {
        var world = new OpcodeWorld();
        var entity = Entity(world, 4, 6, 0, 0);

        var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, 0x72, 0x1A, 9);

        Assert.Equal(9u, entity.TargetAnimationId);
        Assert.Equal(OpcodeWorld.ResultBefore, state.Result);
        OpcodeWorld.AssertImplemented(trace, 0x72, 1);
        Assert.Equal((4u, 6u), (entity.LastTargetAnimationId, entity.LastTargetDirection));
    }

    /// <summary>
    /// V-72 (sm4): the 67 vectors of <c>0x72</c> of the annex (7 by hand, 60 seeded random worlds), <c>docs/plan-e20-annexe/e20-g5/g5b/vectors/sm4-op72.json</c>, each one execution of the real handler: the fields it wrote
    /// (the final values of <c>slotWrites</c>), the length returned (1) and no write to <c>Result</c>; every other field of the world is unchanged.
    /// </summary>
    [Fact]
    public void TheVectorsOfTheOpcode_0x72_ReplayOnTheDll()
    {
        var text = File.ReadAllText(AnnexFiles.ButtonAnnex("vectors/sm4-op72.json")).Replace("\r\n", "\n");
        Assert.Equal("0158fd87cc773f71ed1cba911c6cf4dd17b6a5db3f8eb4bfa9bfd6d3b5c972e9", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        using var document = JsonDocument.Parse(text);
        var vectors = document.RootElement.GetProperty("vectors").EnumerateArray().ToList();
        Assert.Equal(67, vectors.Count);

        foreach (var vector in vectors)
        {
            var id = vector.GetProperty("id").GetString()!;
            var world = new OpcodeWorld();
            var slots = vector.GetProperty("world").GetProperty("slots").EnumerateArray().ToList();
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var status = s.GetProperty("status").GetInt32();
                EntityScriptProxy proxy;
                if (i == 0)
                {
                    proxy = world.Hero;
                }
                else
                {
                    // A slot of status 0 is memory in the binary and a Destroyed proxy here: the opcode writes through the logic entity whatever its status.
                    proxy = world.Add(s.GetProperty("refId").GetInt32());
                }

                proxy.Status = (EntityStatus)status;
                proxy.EntityRefId = s.GetProperty("refId").GetInt32();
                proxy.Flags = unchecked((uint)s.GetProperty("flags").GetInt64());
                proxy.TargetAnimationId = unchecked((uint)s.GetProperty("targetAnimId").GetInt64());
                proxy.TargetDirection = unchecked((uint)s.GetProperty("targetDir").GetInt64());
                proxy.LastTargetAnimationId = unchecked((uint)s.GetProperty("lastTargetAnimId").GetInt64());
                proxy.LastTargetDirection = unchecked((uint)s.GetProperty("lastTargetDir").GetInt64());
            }

            var logic = world.Slots[vector.GetProperty("logicSlot").GetInt32()]!;
            var owner = world.Slots[vector.GetProperty("ownerSlot").GetInt32()]!;
            if (!ReferenceEquals(owner, logic))
            {
                owner.LogicEntity = logic;
            }

            var expected = world.Slots.Select(WordsOf).ToList();
            foreach (var write in vector.GetProperty("expect").GetProperty("slotWrites").EnumerateArray())
            {
                var slot = write.GetProperty("slot").GetInt32();
                var value = unchecked((uint)write.GetProperty("value").GetInt64());
                var current = expected[slot]!.Value;
                expected[slot] = write.GetProperty("field").GetString() switch
                {
                    "lastTargetAnimId" => current with { LastAnim = value },
                    "lastTargetDir" => current with { LastDir = value },
                    var other => throw new InvalidOperationException("vector " + id + ": unexpected written field " + other),
                };
            }

            var (state, trace) = world.Run(owner, OpcodeWorld.ResultBefore, 0x72);

            var actual = world.Slots.Select(WordsOf).ToList();
            Assert.True(expected.SequenceEqual(actual), "vector " + id + ": the fields written by the binary are not the DLL's");
            Assert.False(vector.GetProperty("expect").GetProperty("resultWritten").GetBoolean());
            Assert.Equal(OpcodeWorld.ResultBefore, state.Result);
            Assert.Equal(vector.GetProperty("expect").GetProperty("returned").GetInt32(), state.CodeIndex);
            OpcodeWorld.AssertImplemented(trace, 0x72, 1);
        }
    }
}
