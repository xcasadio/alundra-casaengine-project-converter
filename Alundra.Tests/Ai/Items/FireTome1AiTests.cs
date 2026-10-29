#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1, phase A1 (docs/plan-e20-heros.md section 1.31.6, tests (c1), (c2), (e1), (e2), (k1), (k2), (k4), (k5); the 32-bit words (t1) are in <c>TomeWordsTests</c>): the cancellation <c>0x80034D2C</c>, E3 for the type 23 (<c>0x8007EDA0</c>) and C63 (<c>0x8007AC60</c>, the fire ball) of the fire tome 1, on a hero and entities made by hand
/// and on a registry of their own (the registry of production is not touched in the phase A1). The expected values are the vectors of the real code of the binary (319 of the cancellation, 288 of E3, 3 524 of C63), replayed word by word through the adapter of T0 (b) (<c>t0/mapping.md</c>: a pointer is a slot; an identity word read
/// different from the one stored is a DEAD reference, a proxy <see cref="EntityStatus.Destroyed"/> with the slot -1; the Z of the DLL is the binary's minus 1; the output arrays of the query are ignored), and hand cases next to them.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome1AiTests : IDisposable
{
    private const ulong SentinelSeed = 0x5EED5EEDu;
    private const int SentinelForceZ = 0x1234;
    private const uint SentinelDirection = 99;

    public FireTome1AiTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) the cancellation against the 319 comparable vectors; (c2) by hand.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed record Made(int Effect, bool MapTable, int Animation, int X, int Y, int Z);

    private static List<Made> RecordEffects()
    {
        var made = new List<Made>();
        NativeEffects.CreatedForTests = slot => made.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
        return made;
    }

    private static void Settle(BombHandsBench bench) => bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

    [Fact]
    public void C1_TheCancellation_ThreeHundredAndNineteenVectorsOfTheOracle_AreReplayed_TheColumnAtTheStatus2GivesTheEffectAndIsDestroyed_TheBallGoesToTheStatus3AndLosesItsBlocker()
    {
        var all = FireTome1Vectors.Cancel();
        var vectors = all.Where(v => !v.BinaryOnly).ToList();

        Assert.Equal(400, all.Count);
        Assert.Equal(319, vectors.Count);
        var destroyedColumns = 0;
        var cancelledBalls = 0;
        var effects = 0;
        foreach (var v in vectors)
        {
            var bench = BombHandsBench.Create(effectStage: true);
            var made = RecordEffects();
            var entities = new Dictionary<int, EntityScriptProxy>();
            foreach (var slot in v.Reads.Keys.Where(FireTome1Vectors.IsEntityWord).Select(FireTome1Vectors.SlotOf).Distinct().OrderBy(s => s))
            {
                var entity = bench.AddOther(v.ReadInt(FireTome1Vectors.Word(slot, 0x68)));
                entity.Slot = slot;
                entity.Status = (EntityStatus)v.ReadInt(FireTome1Vectors.Word(slot, 0x10));
                entity.PosX = v.ReadInt(FireTome1Vectors.Word(slot, 0x114));
                entity.PosY = v.ReadInt(FireTome1Vectors.Word(slot, 0x118));
                entity.PosZ = v.ReadInt(FireTome1Vectors.Word(slot, 0x11C), 1) - 1; // the DLL's Z is the binary's minus 1.
                entity.BlockedByEntity = bench.Hero.LogicContextEntity;
                entities[slot] = entity;
            }

            FireTome1Ai.Cancel(bench.Hero);
            Settle(bench);

            var label = $"cancellation vector {v.Id}";
            foreach (var (slot, entity) in entities)
            {
                var wroteStatus = v.Wrote(FireTome1Vectors.Word(slot, 0x10));
                var destroyed = wroteStatus && v.WroteInt(FireTome1Vectors.Word(slot, 0x10)) == 4;
                Assert.True(destroyed == bench.Host.DestroyedAlone.Contains(entity), $"{label}: entity {slot} destroyed {destroyed}");
                if (!destroyed)
                {
                    var expected = wroteStatus ? v.WroteInt(FireTome1Vectors.Word(slot, 0x10)) : v.ReadInt(FireTome1Vectors.Word(slot, 0x10));
                    Assert.True(expected == (int)entity.Status, $"{label}: entity {slot} status {entity.Status}");
                    cancelledBalls += wroteStatus ? 1 : 0;
                }
                else
                {
                    destroyedColumns++;
                }

                Assert.True(v.Wrote(FireTome1Vectors.Word(slot, 0x20)) == (entity.BlockedByEntity is null), $"{label}: entity {slot} blocker {(entity.BlockedByEntity is null ? "cleared" : "kept")}");
            }

            Assert.Equal(v.Journal.Count, made.Count);
            for (var k = 0; k < v.Journal.Count; k++)
            {
                Assert.Equal("effect_3BDD8", v.Journal[k].Name);
                var a = v.Journal[k].Arguments; // CreateEffectEntity(0, 0x13, 2, X, Y, Z): the bank, the effect, the animation, the raw position in the binary's frame.
                Assert.True(new Made((int)a[1], a[0] != 0, (int)a[2], unchecked((int)a[3]), unchecked((int)a[4]), unchecked((int)a[5])) == made[k], $"{label}: effect {k} is {made[k]}");
            }

            Assert.Equal(v.Journal.Count, bench.Host.DestroyedAlone.Count);
            Assert.Empty(bench.Host.Destroyed);
            effects += made.Count;
        }

        // Both sides of every front are in the sample: the columns destroyed with their effect, the balls cancelled, the entities left alone.
        Assert.Equal(73, destroyedColumns);
        Assert.Equal(73, effects);
        Assert.Equal(113, cancelledBalls);
        Assert.Contains(vectors, v => v.Journal.Count == 0 && v.Writes.Count == 0);
    }

    private static EntityScriptProxy Make(BombHandsBench bench, int type, EntityStatus status, int x = 100 << 16, int y = 90 << 16, int z = 5)
    {
        var entity = bench.AddOther(type);
        entity.Status = status;
        entity.PosX = x;
        entity.PosY = y;
        entity.PosZ = z;
        entity.BlockedByEntity = bench.Hero.LogicContextEntity;
        return entity;
    }

    [Fact]
    public void C2_TheColumnAtTheStatus2_GivesTheEffect19Animation2AtItsRawPositionInTheBinarysFrameOfZ_AndIsDestroyed_NothingElseMovesAndTheItemIsNotFreed()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        var column = Make(bench, 20, EntityStatus.Normal, 0x00640000 + 5, 0x00370000 + 7, 0x00120000);
        bench.Hero.ItemInUse = 0x2F;
        bench.Hero.ItemUsePhase = 1;
        bench.Hero.ItemUseCounter = 50;

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.Equal(new[] { new Made(19, false, 2, 0x00640000 + 5, 0x00370000 + 7, 0x00120000 + 1) }, made); // the DLL's Z plus one: the binary's frame (ADR-0026).
        Assert.Equal(new[] { column }, bench.Host.DestroyedAlone);
        Assert.Empty(bench.Host.Destroyed); // the destruction is the one-argument one (0x8003A774): no break effect.
        Assert.Equal((0x2Fu, 1, 50), (bench.Hero.ItemInUse, bench.Hero.ItemUsePhase, bench.Hero.ItemUseCounter)); // the item is freed by the general cancellation, not by this one.
        Assert.Equal((0x00640000 + 5, 0x00370000 + 7, 0x00120000), (column.PosX, column.PosY, column.PosZ));
        Assert.Same(bench.Hero.LogicContextEntity, column.BlockedByEntity); // the destruction clears no blocker here.
    }

    [Fact]
    public void C2_TheBallAtTheStatus2_GoesToTheStatus3AndLosesItsBlocker_NoEffectIsCreatedAndNothingIsDestroyed()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        var ball = Make(bench, 23, EntityStatus.Normal);
        var x = ball.PosX;

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.Equal((EntityStatus.Deactivated, true), (ball.Status, ball.BlockedByEntity is null));
        Assert.Empty(made);
        Assert.Empty(bench.Host.DestroyedAlone);
        Assert.Empty(bench.Host.Destroyed);
        Assert.Equal(x, ball.PosX);
    }

    [Fact]
    public void C2_TheStatusesOtherThan2AreLeftAlone_ForTheColumnAndTheBall_EachNextToTheStatus2ThatActs()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        var blocker = bench.Hero.LogicContextEntity;
        var left = new List<(EntityScriptProxy Entity, EntityStatus Status)>();
        foreach (var type in new[] { 20, 23 })
        {
            foreach (var status in new[] { EntityStatus.Destroyed, EntityStatus.Loaded, EntityStatus.Deactivated, EntityStatus.FlagToDestroy, EntityStatus.Unknown })
            {
                left.Add((Make(bench, type, status), status));
            }
        }

        var acted = new[] { Make(bench, 20, EntityStatus.Normal), Make(bench, 23, EntityStatus.Normal) };

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.All(left, e => Assert.Equal((e.Status, blocker), (e.Entity.Status, e.Entity.BlockedByEntity)));
        Assert.Equal(new[] { acted[0] }, bench.Host.DestroyedAlone);
        Assert.Single(made);
        Assert.Equal((EntityStatus.Deactivated, true), (acted[1].Status, acted[1].BlockedByEntity is null));
    }

    [Fact]
    public void C2_TheOtherTypesAndTheEntitiesOfTheMapOfIndex20Or23AreLeftAlone()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        var blocker = bench.Hero.LogicContextEntity;
        // The types 17 (the rock of the earth tome 2), 21 (the column of the fire tome 2), 22, 24, 16, 150, and the entities of the map of index 20 and 23 (the sprite type is 0x100 + index).
        var others = new[] { 17, 21, 22, 24, 16, 150, 0x114, 0x117, 0 }.Select(type => Make(bench, type, EntityStatus.Normal)).ToList();

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.All(others, e => Assert.Equal((EntityStatus.Normal, blocker), (e.Status, e.BlockedByEntity)));
        Assert.Empty(made);
        Assert.Empty(bench.Host.DestroyedAlone);
    }

    [Fact]
    public void C2_SeveralColumnsAndBalls_GiveTheirEffectsInTheOrderOfTheWorld_AndTheHeroIsNeverTouched()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        bench.Hero.Status = EntityStatus.Normal;
        var first = Make(bench, 20, EntityStatus.Normal, 10 << 16, 20 << 16, 3);
        var ball = Make(bench, 23, EntityStatus.Normal);
        var second = Make(bench, 20, EntityStatus.Normal, 30 << 16, 40 << 16, 6);

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.Equal(new[] { new Made(19, false, 2, 10 << 16, 20 << 16, 4), new Made(19, false, 2, 30 << 16, 40 << 16, 7) }, made);
        Assert.Equal(new[] { first, second }, bench.Host.DestroyedAlone);
        Assert.Equal(EntityStatus.Deactivated, ball.Status);
        Assert.Equal(EntityStatus.Normal, bench.Hero.Status);
    }

    [Fact]
    public void C2_AFullReservoir_CreatesNoEffect_ButTheColumnIsDestroyedAllTheSame()
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var made = RecordEffects();
        var column = Make(bench, 20, EntityStatus.Normal);
        while (bench.Host.EffectStage!.Reservoir.Create(false, 14, 0, 0, 0, 0) is not null)
        {
        }

        FireTome1Ai.Cancel(bench.Hero);
        Settle(bench);

        Assert.Empty(made);
        Assert.Equal(new[] { column }, bench.Host.DestroyedAlone);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e1) E3 against the 288 vectors; (e2) the registrations.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E1_E3_TheTwoHundredAndEightyEightVectorsOfTheOracle_AreReplayed_ForTheBallOfTheType23_TheDestructionAtTheEndOfTheAnimation_TheOthersSetTheAnimation1AndTheFlags()
    {
        var vectors = FireTome1Vectors.E3();

        Assert.Equal(288, vectors.Count);
        Assert.All(vectors, v => Assert.False(v.BinaryOnly));
        var destroyedCount = 0;
        foreach (var v in vectors)
        {
            var animationWord = v.Argument + 0x88;
            var endWord = v.Argument + 0xAC;
            var flagsWord = v.Argument + 0x6C;
            var bench = BombHandsBench.Create(effectStage: false);
            var ball = bench.AddOther(23);
            ball.TargetAnimationId = (uint)v.ReadInt(animationWord);
            ball.ForceResetAnimationFlag = v.ReadInt(endWord);
            ball.Flags = (uint)v.ReadInt(flagsWord);
            var flagsBefore = ball.Flags;

            EarthTome1Ai.ImpactDeactivate(ball);

            var label = $"E3 vector {v.Id}";
            Assert.True((v.Wrote(animationWord) ? (uint)v.WroteInt(animationWord) : (uint)v.ReadInt(animationWord)) == ball.TargetAnimationId, $"{label}: animation {ball.TargetAnimationId}");
            Assert.True((v.Wrote(flagsWord) ? (uint)v.WroteInt(flagsWord) : flagsBefore) == ball.Flags, $"{label}: flags {ball.Flags:X}");
            Assert.True(v.ReadInt(endWord) == ball.ForceResetAnimationFlag, $"{label}: end of the animation {ball.ForceResetAnimationFlag}");
            var destroys = v.Journal.Count == 1;
            Assert.True(destroys == (bench.Host.Destroyed.Count == 1), $"{label}: destroyed {bench.Host.Destroyed.Count}");
            if (destroys)
            {
                Assert.Equal("destroy_3A59C", v.Journal[0].Name);
                Assert.Equal(unchecked((long)0xFFFFFFFFu), v.Journal[0].Arguments[1]); // DestroyEntity(self, -1).
                Assert.Equal((ball, -1), bench.Host.Destroyed.Single());
                destroyedCount++;
            }

            Assert.Empty(bench.Host.DestroyedAlone);
        }

        Assert.Equal(12, destroyedCount);
        Assert.Contains(vectors, v => v.Argument != 0 && v.Read(v.Argument + 0x6C) && v.ReadInt(v.Argument + 0x6C) == 0x102074); // the flags of the ball of the sprite 23.
    }

    [Fact]
    public void E2_TheRegistrationsOnAFreshRegistry_E3IsRegisteredForTheTypes16And23Only_C63ForTheType23Only_AndE1IsAbsent()
    {
        var registry = new NativeSpriteRegistry();

        using (EarthTome1Ai.Register(registry))
        using (FireTome1Ai.Register(registry))
        {
            Assert.Equal(EarthTome1Ai.Keys.Count + FireTome1Ai.Keys.Count, registry.Count);
            Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 3) }, EarthTome1Ai.Keys);
            Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 63) }, FireTome1Ai.Keys);
            Assert.Equal(new[] { 16, 23 }, EarthTome1Ai.SpriteTypes);
            Assert.Equal(new[] { 23 }, FireTome1Ai.SpriteTypes);

            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 16, out _));
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 23, out _));
            foreach (var other in new[] { 0, 2, 15, 17, 24, 150, 153, 20, 21, 0x100 + 16, 0x100 + 23, 335 })
            {
                Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, other, out _), $"E3, type {other}");
            }

            Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 63, 23, out _));
            foreach (var other in new[] { 0, 2, 15, 16, 17, 20, 21, 22, 24, 150, 0x100 + 23, 335 })
            {
                Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 63, other, out _), $"C63, type {other}");
            }

            // E1 of the column (sprite 20) is in line in the runner: no key for it, in any slot, for the types 20 and 23.
            foreach (var type in new[] { 20, 23 })
            {
                Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 1, type, out _), $"E1, type {type}");
            }

            // The other slots and codes of the two keys are absent.
            foreach (var slot in new[] { ScriptHelper.ProgramCTick, ScriptHelper.ProgramDTouch })
            {
                Assert.False(registry.TryGet(slot, 3, 23, out _), $"slot {slot} code 3");
            }

            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 63, 23, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 62, 23, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 64, 23, out _));
        }

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 63, 23, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 23, out _));
    }

    [Fact]
    public void E2_TheRegisteredHandlerOfC63_IsTheBallsTick_AndTheRegisteredE3IsTheImpact_ForTheType23()
    {
        var registry = new NativeSpriteRegistry();
        using var rocks = EarthTome1Ai.Register(registry);
        using var balls = FireTome1Ai.Register(registry);
        var bench = BombHandsBench.Create(effectStage: false);
        var ball = bench.AddOther(23);
        ball.Slot = 1;
        ball.TargetAnimationId = 1;
        ball.ForceResetAnimationFlag = 1;

        Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 23, out var impact));
        impact!(ball, bench.Host, GameState.Instance);
        Assert.Equal((ball, -1), Assert.Single(bench.Host.Destroyed)); // E3 at the end of the animation 1.

        var flying = bench.AddOther(23);
        flying.Slot = 2;
        TomeWords.Write274(flying, 5);
        Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 63, 23, out var tick));
        tick!(flying, bench.Host, GameState.Instance);
        Assert.Equal(4, TomeWords.Read274(flying)); // C63: the wait counter loses one.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The adapter of the vectors of C63 (T0 (b)): a ball and the entities the real code read.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class BallCase
    {
        internal required FireTome1Vectors.Vector Vector;
        internal required int BallSlot;
        internal required EntityScriptProxy Ball;
        internal required SortedDictionary<int, EntityScriptProxy> Entities;
        internal required int Count;
        internal required int TargetSlot;
        internal required EntityScriptProxy? SpellTarget;
        internal required int WaitBefore;

        internal List<EntityScriptProxy> Born => Entities.Values.ToList();
    }

    private static BallCase BuildBall(FireTome1Vectors.Vector v)
    {
        var ballSlot = FireTome1Vectors.SlotOf(v.Argument);
        var slots = new SortedSet<int> { ballSlot };
        foreach (var address in v.Reads.Keys.Where(FireTome1Vectors.IsEntityWord))
        {
            slots.Add(FireTome1Vectors.SlotOf(address));
        }

        var targetPointer = v.ReadWord(FireTome1Vectors.Word(ballSlot, 0x278));
        var targetSlot = targetPointer == 0 ? 0 : FireTome1Vectors.SlotOf(targetPointer);
        if (targetSlot != 0)
        {
            Assert.True(FireTome1Vectors.Word(targetSlot, 0) == targetPointer, $"vector {v.Id}: the target pointer is the start of an entity");
            slots.Add(targetSlot);
        }

        // The attack of the ball: the pointer +0x1C8 and the byte at it; the type of the query is its low nibble.
        var attackPointer = v.ReadWord(FireTome1Vectors.Word(ballSlot, 0x1C8));
        var attackRead = attackPointer != 0 && v.Read(attackPointer);
        var type = attackRead ? (int)(v.ReadWord(attackPointer) & 0xF) : 0;

        var entities = new SortedDictionary<int, EntityScriptProxy>();
        foreach (var slot in slots)
        {
            var proxy = new EntityScriptProxy
            {
                Slot = slot,
                Status = (EntityStatus)v.ReadInt(FireTome1Vectors.Word(slot, 0x10)),
                Flags = (uint)v.ReadInt(FireTome1Vectors.Word(slot, 0x6C)),
                HitAnimByte = v.ReadInt(FireTome1Vectors.Word(slot, 0xB4)),
                PosX = v.ReadInt(FireTome1Vectors.Word(slot, 0x114)),
                PosY = v.ReadInt(FireTome1Vectors.Word(slot, 0x118)),
                DamagedTickCounter = v.ReadInt(FireTome1Vectors.Word(slot, 0x1CC)),
                FrameCollisionTickCounter = v.ReadInt(FireTome1Vectors.Word(slot, 0x1D0)),
            };
            if (v.ReadWord(FireTome1Vectors.Word(slot, 0x20)) != 0)
            {
                proxy.BlockedByEntity = new Entity { Name = "blocker" };
            }

            var sheet = v.ReadWord(FireTome1Vectors.Word(slot, 0x1C4));
            if (sheet != 0)
            {
                // The only byte of the sheet the query reads is the one at sheet + type + 2.
                proxy.BalanceRecord = SheetWithByteAt(type + 2, (byte)v.ReadWord(sheet + type + 2));
            }

            entities[slot] = proxy;
        }

        var ball = entities[ballSlot];
        var wait = v.ReadInt(FireTome1Vectors.Word(ballSlot, 0x274));
        TomeWords.Write274(ball, wait);
        ball.ForceZ = SentinelForceZ;
        ball.TargetDirection = v.Read(FireTome1Vectors.Word(ballSlot, 0x8C)) ? (uint)v.ReadInt(FireTome1Vectors.Word(ballSlot, 0x8C)) : SentinelDirection;
        ball.DelayOrAngleOrEntityId = targetSlot;
        if (attackRead)
        {
            ball.CurrentAttack = new BalanceAttack((int)v.ReadWord(attackPointer), 0);
        }

        // The identity: the DLL keeps the target as a reference. An identity read different from the one stored is a dead reference (rule 2 of T0 (b)).
        EntityScriptProxy? spell = null;
        if (targetSlot != 0)
        {
            var stored = FireTome1Vectors.Word(ballSlot, 0x27C);
            var current = FireTome1Vectors.Word(targetSlot, 0x4);
            spell = v.Read(stored) && v.Read(current) && v.ReadWord(stored) != v.ReadWord(current)
                ? new EntityScriptProxy { Status = EntityStatus.Destroyed, Slot = -1 }
                : entities[targetSlot];
        }

        ball.SpellTarget = spell;
        var seed = v.Read(FireTome1Vectors.GeneratorWord) ? (ulong)(uint)v.ReadWord(FireTome1Vectors.GeneratorWord) : SentinelSeed;
        GameRandom.RandomSeed = seed;
        return new BallCase { Vector = v, BallSlot = ballSlot, Ball = ball, Entities = entities, Count = v.ReadInt(FireTome1Vectors.CountWord), TargetSlot = targetSlot, SpellTarget = spell, WaitBefore = wait };
    }

    /// <summary>A sheet whose <see cref="BalanceRecord.ByteAt"/> answers <paramref name="value"/> at <paramref name="offset"/> (2 to 13 are the bytes the query can read with a type of 0 to 11).</summary>
    private static BalanceRecord SheetWithByteAt(int offset, byte value)
    {
        Assert.InRange(offset, 2, 13);
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

    private static void RunAndCompare(FireTome1Vectors.Vector v)
    {
        var c = BuildBall(v);
        var label = $"C63 vector {v.Id} ({v.Kind})";

        FireTome1Ai.BallTick(c.Ball, c.Born, c.Count, inclusiveBound: false, sortThreshold: 3);

        var waitWord = FireTome1Vectors.Word(c.BallSlot, 0x274);
        var expectedWait = v.Wrote(waitWord) ? v.WroteInt(waitWord) : c.WaitBefore;
        Assert.True(expectedWait == TomeWords.Read274(c.Ball), $"{label}: wait {TomeWords.Read274(c.Ball)}, vector {expectedWait}");

        var forceWord = FireTome1Vectors.Word(c.BallSlot, 0xB8);
        Assert.True((v.Wrote(forceWord) ? v.WroteInt(forceWord) : SentinelForceZ) == c.Ball.ForceZ, $"{label}: Z force {c.Ball.ForceZ}");

        var directionWord = FireTome1Vectors.Word(c.BallSlot, 0x8C);
        var expectedDirection = v.Wrote(directionWord) ? (uint)v.WroteInt(directionWord) : v.Read(directionWord) ? (uint)v.ReadInt(directionWord) : SentinelDirection;
        Assert.True(expectedDirection == c.Ball.TargetDirection, $"{label}: direction {c.Ball.TargetDirection}, vector {expectedDirection}");

        var targetWord = FireTome1Vectors.Word(c.BallSlot, 0x278);
        var expectedTargetSlot = v.Wrote(targetWord) ? (v.WroteWord(targetWord) == 0 ? 0 : FireTome1Vectors.SlotOf(v.WroteWord(targetWord))) : c.TargetSlot;
        Assert.True(expectedTargetSlot == c.Ball.DelayOrAngleOrEntityId, $"{label}: target slot {c.Ball.DelayOrAngleOrEntityId}, vector {expectedTargetSlot}");
        if (expectedTargetSlot == 0)
        {
            Assert.True(c.Ball.SpellTarget is null, $"{label}: no target reference");
        }
        else if (v.Wrote(targetWord))
        {
            // Acquired: the reference is the entity of that slot, and its identity is the one the real code stored (+0x27C).
            Assert.True(ReferenceEquals(c.Ball.SpellTarget, c.Entities[expectedTargetSlot]), $"{label}: the reference is the entity of the slot {expectedTargetSlot}");
            Assert.Equal(v.ReadWord(FireTome1Vectors.Word(expectedTargetSlot, 0x4)), v.WroteWord(FireTome1Vectors.Word(c.BallSlot, 0x27C)));
        }
        else
        {
            Assert.True(ReferenceEquals(c.SpellTarget, c.Ball.SpellTarget), $"{label}: the reference is kept");
        }

        var expectedSeed = v.Wrote(FireTome1Vectors.GeneratorWord) ? (uint)v.WroteWord(FireTome1Vectors.GeneratorWord) : (uint)SentinelSeed;
        Assert.True(expectedSeed == unchecked((uint)GameRandom.RandomSeed), $"{label}: generator {unchecked((uint)GameRandom.RandomSeed):X}, vector {expectedSeed:X}"); // the machine has 32 bits of state.

        // Nothing else of the ball or the others moved: the positions, the statuses and the slots.
        foreach (var (slot, entity) in c.Entities)
        {
            Assert.True(v.ReadInt(FireTome1Vectors.Word(slot, 0x10)) == (int)entity.Status, $"{label}: status of {slot}");
            Assert.True(v.ReadInt(FireTome1Vectors.Word(slot, 0x114)) == entity.PosX && v.ReadInt(FireTome1Vectors.Word(slot, 0x118)) == entity.PosY, $"{label}: position of {slot}");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k1) C63 in the binary mode against the 3 524 vectors.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K1_C63_InTheBinaryMode_TheThreeThousandFiveHundredAndTwentyFourVectorsOfTheOracle_AreReplayed_WaitForceTargetIdentityDirectionAndGenerator()
    {
        var vectors = FireTome1Vectors.Ball();

        Assert.Equal(3524, vectors.Count);
        Assert.All(vectors, v => Assert.False(v.BinaryOnly));
        foreach (var v in vectors)
        {
            RunAndCompare(v);
        }

        // Both sides of every front are in the sample (counted on the files, T0).
        bool Active(FireTome1Vectors.Vector v) => v.ReadInt(FireTome1Vectors.Word(FireTome1Vectors.SlotOf(v.Argument), 0x274)) == 0;
        bool Wrote278(FireTome1Vectors.Vector v, bool zero) => v.Wrote(FireTome1Vectors.Word(FireTome1Vectors.SlotOf(v.Argument), 0x278)) && (v.WroteWord(FireTome1Vectors.Word(FireTome1Vectors.SlotOf(v.Argument), 0x278)) == 0) == zero;
        Assert.Equal(313, vectors.Count(v => !Active(v))); // the wait counter loses one and nothing else.
        Assert.Equal(3211, vectors.Count(Active));
        Assert.Equal(660, vectors.Count(v => Active(v) && Wrote278(v, zero: false))); // a target acquired (the first of the query).
        Assert.Equal(591, vectors.Count(v => Active(v) && Wrote278(v, zero: true))); // a target forgotten.
        Assert.Equal(598, vectors.Count(v => v.Wrote(FireTome1Vectors.GeneratorWord))); // the query found nobody: one draw below 9.
        Assert.Equal(1956, vectors.Count(v => v.Wrote(FireTome1Vectors.Word(FireTome1Vectors.SlotOf(v.Argument), 0x8C)))); // a turn.
        Assert.Equal(1024, vectors.Count(v => v.Kind == "turn"));
        Assert.Equal(2500, vectors.Count(v => v.Kind != "turn"));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k2) the turn: the original's, loops included.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K2_TheTurnOnTheGrid_OfThe32x32Directions_IsTheNumericOneOfTheBinary_TheLongSideForTheTwoHundredAndFortyPairsOfNumericGapAbove16()
    {
        var grid = FireTome1Vectors.Ball().Where(v => v.Kind == "turn").ToList();

        Assert.Equal(1024, grid.Count);
        var longSide = 0;
        foreach (var v in grid)
        {
            var slot = FireTome1Vectors.SlotOf(v.Argument);
            var current = v.ReadInt(FireTome1Vectors.Word(slot, 0x8C));
            var wanted = v.TargetDirection;
            var c = BuildBall(v);

            FireTome1Ai.BallTick(c.Ball, c.Born, c.Count, inclusiveBound: false, sortThreshold: 3);

            // By hand: the gap is taken on the numbers, never round the circle: below 4 the ball goes to the wanted direction, otherwise 4 towards the wanted number.
            var gap = Math.Abs(current - wanted);
            var expected = gap == 0 ? current : gap < 4 ? wanted : wanted < current ? current - 4 : current + 4;
            Assert.True(expected == (int)c.Ball.TargetDirection, $"grid {current} to {wanted}: {c.Ball.TargetDirection}, hand {expected}");
            Assert.Equal(gap == 0 ? 0 : 2, TomeWords.Read274(c.Ball));
            if (gap > 16)
            {
                longSide++;
            }
        }

        Assert.Equal(240, longSide);
    }

    /// <summary>An offset (raw 16.16 differences) whose direction by <see cref="ScriptHelper.GetDirectionToTarget"/> is <paramref name="direction"/> (the function is the binary's: probe P15, 1 024 of 1 024).</summary>
    private static (int Dx, int Dy) OffsetFor(int direction)
    {
        for (var radius = 1; radius < 40; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == radius && ScriptHelper.GetDirectionToTarget(dx << 16, dy << 16) == (uint)direction)
                    {
                        return (dx << 16, dy << 16);
                    }
                }
            }
        }

        throw new InvalidOperationException($"no offset gives the direction {direction}");
    }

    private static EntityScriptProxy Enemy(int slot, int x, int y, uint flags = EntityFlags.ClassA)
    {
        var enemy = new EntityScriptProxy
        {
            Slot = slot,
            Status = EntityStatus.Normal,
            Flags = flags,
            PosX = x,
            PosY = y,
            BalanceRecord = new BalanceRecord(0, 15, 0, new byte[11], Array.Empty<BalanceAttack>(), null),
        };
        return enemy;
    }

    /// <summary>A ball in the slot 1 at (100, 100) px looking at <paramref name="current"/>, and an enemy in the slot 2 in the direction <paramref name="wanted"/>, the ball with its target already kept (no query).</summary>
    private static (EntityScriptProxy Ball, EntityScriptProxy Enemy, List<EntityScriptProxy> World) Pair(int current, int wanted)
    {
        var (dx, dy) = OffsetFor(wanted);
        var ball = Enemy(1, 100 << 16, 100 << 16, 0);
        ball.TargetDirection = (uint)current;
        ball.CurrentAttack = new BalanceAttack(0x0A, 32);
        var enemy = Enemy(2, ball.PosX + dx, ball.PosY + dy);
        ball.DelayOrAngleOrEntityId = enemy.Slot;
        ball.SpellTarget = enemy;
        return (ball, enemy, new List<EntityScriptProxy> { ball, enemy });
    }

    [Theory]
    [InlineData(31, 0, 27)] // not 0: no wrap from 31 to 0, the ball turns by the long side.
    [InlineData(0, 31, 4)] // not 31.
    [InlineData(30, 1, 26)]
    [InlineData(2, 30, 6)]
    [InlineData(5, 8, 8)] // a gap of 3: the jump to the wanted direction.
    [InlineData(8, 5, 5)]
    [InlineData(5, 9, 9)] // a gap of 4: a step of 4 (which reaches the wanted direction).
    [InlineData(9, 5, 5)]
    [InlineData(5, 10, 9)] // a gap of 5: a step of 4.
    [InlineData(10, 5, 6)]
    [InlineData(16, 0, 12)]
    [InlineData(0, 16, 4)]
    [InlineData(0, 17, 4)] // a gap of 17 > 16: still the numeric side, towards the greater.
    [InlineData(17, 0, 13)]
    public void K2_TheNamedCases_OfTheTurn(int current, int wanted, int expected)
    {
        var (ball, _, world) = Pair(current, wanted);

        FireTome1Ai.BallTick(ball, world, 2);

        Assert.Equal((uint)expected, ball.TargetDirection);
        Assert.Equal(2, TomeWords.Read274(ball)); // a step every three ticks.
    }

    [Fact]
    public void K2_TheSameDirection_WritesNothing_NeitherTheDirectionNorTheWait()
    {
        var (ball, _, world) = Pair(12, 12);
        TomeWords.Write274(ball, 0);

        FireTome1Ai.BallTick(ball, world, 2);

        Assert.Equal((12u, 0), (ball.TargetDirection, TomeWords.Read274(ball)));
        Assert.Equal(unchecked((int)0xFFFD0000), ball.ForceZ); // the ball still falls.
    }

    [Theory]
    [InlineData(31, 0, new[] { 31, 27, 23, 19, 15, 11, 7, 3, 0 })]
    [InlineData(0, 31, new[] { 0, 4, 8, 12, 16, 20, 24, 28, 31 })]
    public void K2_FromOneEndOfTheNumbersToTheOther_TheBallTurnsInEightStepsOfFourOverTwentyFourTicks_OneStepEveryThreeTicks(int start, int wanted, int[] path)
    {
        var (ball, _, world) = Pair(start, wanted);

        var seen = new List<int> { (int)ball.TargetDirection };
        for (var tick = 0; tick < 24; tick++)
        {
            FireTome1Ai.BallTick(ball, world, 2);
            if (tick % 3 == 0)
            {
                seen.Add((int)ball.TargetDirection);
            }
            else
            {
                Assert.Equal((int)seen[^1], (int)ball.TargetDirection); // two ticks of wait between two steps.
            }
        }

        Assert.Equal(path, seen.Take(path.Length));
        Assert.Equal(8, seen.Zip(seen.Skip(1), (a, b) => a != b).Count(changed => changed));
        Assert.Equal(wanted, (int)ball.TargetDirection);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // C63 by hand: the wait, the fall, the target.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C63_AWaitThatIsNotZero_LosesOneAndNothingElseIsDone_TheFallingForceIncluded()
    {
        var (ball, _, world) = Pair(0, 8);
        ball.ForceZ = 0x80000;
        TomeWords.Write274(ball, 16);
        GameRandom.RandomSeed = SentinelSeed;

        FireTome1Ai.BallTick(ball, world, 2);

        Assert.Equal(15, TomeWords.Read274(ball));
        Assert.Equal(0x80000, ball.ForceZ);
        Assert.Equal(0u, ball.TargetDirection);
        Assert.Equal(SentinelSeed, GameRandom.RandomSeed);

        // The big and the negative values: the word is 32 bits, a wait of 0x01000000 loses one, a negative one too (it is not zero).
        foreach (var (before, after) in new[] { (0x01000000, 0x00FFFFFF), (-1, -2), (256, 255), (int.MinValue, int.MaxValue) })
        {
            TomeWords.Write274(ball, before);
            FireTome1Ai.BallTick(ball, world, 2);
            Assert.Equal(after, TomeWords.Read274(ball));
        }
    }

    [Fact]
    public void C63_TheBallBornAtTheTickSHolds16_AndActsAtTheSeventeenthTick()
    {
        var (ball, _, world) = Pair(0, 8);
        ball.DelayOrAngleOrEntityId = 0;
        ball.SpellTarget = null;
        ball.ForceZ = 0x80000;
        TomeWords.Write274(ball, 16);
        var acted = -1;
        for (var tick = 1; tick <= 20 && acted < 0; tick++)
        {
            var before = ball.ForceZ;
            FireTome1Ai.BallTick(ball, world, 2);
            if (ball.ForceZ != before)
            {
                acted = tick;
            }
        }

        Assert.Equal(17, acted); // 16 decrements, then the first action (the fall, the query).
    }

    [Fact]
    public void C63_ATargetThatIsNotAtTheStatus2Anymore_IsForgottenAtOnce_AndTheQueryRestartsAtTheNextTickWithNoWait()
    {
        foreach (var status in new[] { EntityStatus.Destroyed, EntityStatus.Loaded, EntityStatus.Deactivated, EntityStatus.FlagToDestroy, EntityStatus.Unknown })
        {
            var (ball, enemy, world) = Pair(0, 8);
            enemy.Status = status;

            FireTome1Ai.BallTick(ball, world, 2);

            Assert.Equal((0, (EntityScriptProxy?)null, 0u), (ball.DelayOrAngleOrEntityId, ball.SpellTarget, ball.TargetDirection));
            Assert.Equal(0, TomeWords.Read274(ball)); // no wait: the query runs at the next tick.
            Assert.Equal(unchecked((int)0xFFFD0000), ball.ForceZ);
        }

        // The status 2 keeps the target, and the turn is done in the same tick.
        var (kept, _, keptWorld) = Pair(0, 8);
        FireTome1Ai.BallTick(kept, keptWorld, 2);
        Assert.Equal((2, 4u), (kept.DelayOrAngleOrEntityId, kept.TargetDirection));
    }

    [Fact]
    public void C63_ADeadReference_ARecycledEntityOfTheSameSlot_IsNotTheTarget_EvenAtTheStatus2()
    {
        var (ball, enemy, world) = Pair(0, 8);
        var recycled = Enemy(2, enemy.PosX, enemy.PosY); // another entity in the slot 2 now: the ball kept the old one.
        enemy.Status = EntityStatus.Destroyed;
        enemy.Slot = -1;
        world[1] = recycled;

        FireTome1Ai.BallTick(ball, world, 2);

        Assert.Equal((0, (EntityScriptProxy?)null), (ball.DelayOrAngleOrEntityId, ball.SpellTarget));
    }

    [Fact]
    public void C63_TheQueryTakesTheFirst_AndKeepsItsSlotAndItsReference_ThenTurnsInTheSameTick()
    {
        var ball = Enemy(1, 100 << 16, 100 << 16, 0);
        ball.CurrentAttack = new BalanceAttack(0x0A, 32);
        ball.TargetDirection = 20;
        var (dx, dy) = OffsetFor(8);
        var near = Enemy(2, ball.PosX + dx, ball.PosY + dy);
        var far = Enemy(3, ball.PosX + dx * 3, ball.PosY + dy * 3);
        var world = new List<EntityScriptProxy> { ball, far, near };

        FireTome1Ai.BallTick(ball, world, 3);

        Assert.Same(near, ball.SpellTarget); // the nearest first (the sort runs from two results).
        Assert.Equal(2, ball.DelayOrAngleOrEntityId);
        Assert.Equal(16u, ball.TargetDirection); // 20 to 8: a step of 4 in the same tick.
        Assert.Equal(2, TomeWords.Read274(ball));
    }

    [Fact]
    public void C63_NoEnemy_DrawsTheWaitBelow9_OnceWhateverTheNumberOfCandidates()
    {
        var seen = new HashSet<int>();
        for (var seed = 1u; seed < 200u; seed += 7)
        {
            var ball = Enemy(1, 100 << 16, 100 << 16, 0);
            ball.CurrentAttack = new BalanceAttack(0x0A, 32);
            var notEnemy = Enemy(2, 120 << 16, 100 << 16, 0x4); // no ClassA bit.
            GameRandom.RandomSeed = seed;

            FireTome1Ai.BallTick(ball, new List<EntityScriptProxy> { ball, notEnemy }, 2);

            var wait = TomeWords.Read274(ball);
            Assert.InRange(wait, 0, 8);
            Assert.Equal(1, FireTome2Run.Steps(seed, unchecked((uint)GameRandom.RandomSeed))); // one draw.
            Assert.Equal(0, ball.DelayOrAngleOrEntityId);
            seen.Add(wait);
        }

        Assert.True(seen.Count >= 5, "the draws cover the range");
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k4) the settings of production; (k5) the type and the mask of the query.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K4_TheProductionValuesOfTheTwoSettings_AreTheCorrectedOnes_AndTheDefaultCallOfTheBallUsesThem()
    {
        Assert.True(TargetQuery.InclusiveBound);
        Assert.Equal(2, TargetQuery.SortThreshold);

        // The sort runs from two results: of two enemies the NEAREST in the world's second place is taken (the binary would keep the order of the slots).
        var ball = Enemy(1, 100 << 16, 100 << 16, 0);
        ball.CurrentAttack = new BalanceAttack(0x0A, 32);
        var (dx, dy) = OffsetFor(8);
        var far = Enemy(2, ball.PosX + dx * 4, ball.PosY + dy * 4);
        var near = Enemy(3, ball.PosX + dx, ball.PosY + dy);
        FireTome1Ai.BallTick(ball, new List<EntityScriptProxy> { ball, far, near }, 3);
        Assert.Same(near, ball.SpellTarget);

        // The bound is inclusive: the enemy in the slot of the count is a candidate (the binary misses it).
        var top = Enemy(2, ball.PosX + dx, ball.PosY + dy);
        var ball2 = Enemy(1, 100 << 16, 100 << 16, 0);
        ball2.CurrentAttack = new BalanceAttack(0x0A, 32);
        FireTome1Ai.BallTick(ball2, new List<EntityScriptProxy> { ball2, top }, 2);
        Assert.Same(top, ball2.SpellTarget);

        // The same two worlds in the binary mode, by the arguments of the overload: the farther first (two results are not sorted), and the top slot missed.
        var ball3 = Enemy(1, 100 << 16, 100 << 16, 0);
        ball3.CurrentAttack = new BalanceAttack(0x0A, 32);
        FireTome1Ai.BallTick(ball3, new List<EntityScriptProxy> { ball3, far, near }, 4, inclusiveBound: false, sortThreshold: 3);
        Assert.Same(far, ball3.SpellTarget);
        var ball4 = Enemy(1, 100 << 16, 100 << 16, 0);
        ball4.CurrentAttack = new BalanceAttack(0x0A, 32);
        GameRandom.RandomSeed = SentinelSeed;
        FireTome1Ai.BallTick(ball4, new List<EntityScriptProxy> { ball4, top }, 2, inclusiveBound: false, sortThreshold: 3);
        Assert.Null(ball4.SpellTarget);
    }

    [Fact]
    public void K4_TheRegisteredBallComputesTheCountOfTheWorld_TheHighestSlotNotDestroyedNorToDestroy()
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var ball = bench.AddOther(23);
        ball.Slot = 1;
        ball.Flags = 0;
        ball.CurrentAttack = new BalanceAttack(0x0A, 32);
        var (dx, dy) = OffsetFor(8);
        var enemy = bench.AddOther(150);
        enemy.Slot = 2;
        enemy.Flags = EntityFlags.ClassA;
        enemy.PosX = ball.PosX + dx;
        enemy.PosY = ball.PosY + dy;
        enemy.BalanceRecord = new BalanceRecord(0, 15, 0, new byte[11], Array.Empty<BalanceAttack>(), null);
        var dying = bench.AddOther(150);
        dying.Slot = 9;
        dying.Status = EntityStatus.FlagToDestroy;

        // The count is 2 (the slot 9 is to be destroyed): the inclusive bound reaches the enemy of the slot 2.
        FireTome1Ai.BallTick(ball, bench.Host);

        Assert.Same(enemy, ball.SpellTarget);
        Assert.Equal(2, TargetQuery.StoredCount(bench.Host.SpawnedEntities));
    }

    [Fact]
    public void K5_ABallWhoseAttackByteIs0x8A_AsksForTheType10AndTheMaskClassA()
    {
        var ball = Enemy(1, 100 << 16, 100 << 16, 0);
        ball.CurrentAttack = new BalanceAttack(0x8A, 32);
        var (dx, dy) = OffsetFor(8);

        // The nearest is a ClassA enemy whose response to the type 10 is the 0x80 of the sheet (the query skips it for the type 10 only); then a body without the ClassA bit; then the right one.
        var immune = Enemy(2, ball.PosX + dx, ball.PosY + dy);
        immune.BalanceRecord = SheetWithByteAt(12, 0x80);
        var notClassA = Enemy(3, ball.PosX + 2 * dx, ball.PosY + 2 * dy, 0x2 | 0x4 | 0x100);
        var right = Enemy(4, ball.PosX + 3 * dx, ball.PosY + 3 * dy);
        right.BalanceRecord = SheetWithByteAt(12, 0x40);
        var world = new List<EntityScriptProxy> { ball, immune, notClassA, right };

        FireTome1Ai.BallTick(ball, world, 4);

        Assert.Same(right, ball.SpellTarget);

        // The same world with the byte 0x08 or 0x0A... : the type is the low nibble (0x8A and 0x0A are the same type), the byte 0x88 asks the type 8, for which the immune enemy is not immune.
        var other = Enemy(1, 100 << 16, 100 << 16, 0);
        other.CurrentAttack = new BalanceAttack(0x88, 32);
        FireTome1Ai.BallTick(other, new List<EntityScriptProxy> { other, immune, notClassA, right }, 4);
        Assert.Same(immune, other.SpellTarget);
    }

    [Fact]
    public void K5_ABallWithoutAttack_AsksForTheType0_AndDoesNotThrow()
    {
        var ball = Enemy(1, 100 << 16, 100 << 16, 0);
        var (dx, dy) = OffsetFor(8);
        var enemy = Enemy(2, ball.PosX + dx, ball.PosY + dy);

        FireTome1Ai.BallTick(ball, new List<EntityScriptProxy> { ball, enemy }, 2);

        Assert.Same(enemy, ball.SpellTarget);
    }
}
