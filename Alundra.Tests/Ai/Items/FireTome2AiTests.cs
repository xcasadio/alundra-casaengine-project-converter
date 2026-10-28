#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Cameras;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1, phase A (docs/plan-e20-heros.md section 1.31.5, tests (e1) to (e6), (d1) to (d6), (c1), (c2); the buffer of the sway (b1) to (b3) is in <c>CameraSwayBufferTests</c>): the pilot <c>0x80035C64</c>, E28 (<c>0x8007FB58</c>, slot E) and the cancellation <c>0x80034E08</c> of the fire tome 2,
/// on a hero and entities made by hand and on a registry of their own (the registry of production is not touched in the phase A). The expected values are the vectors of the real code of the binary, replayed field by field (the 60 vectors of the pilot and the one of the cancellation, the 203 of E28),
/// and hand cases next to them.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome2AiTests : IDisposable
{
    private const uint Tome = 0x30;
    private const uint Hold = PlayerManager.TomeHoldAnimationId;
    private const uint Cast = PlayerManager.TomeCastAnimationId;
    private const uint End = PlayerManager.TomeEndAnimationId;
    private const int Column = 21;

    public FireTome2AiTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    /// <summary>The cancellation of h0a for the tests: the one of the fire tome 2 only when the item in use is <c>0x30</c>, neutral otherwise, as the dispatcher of the binary (<c>0x800350C0</c>).</summary>
    private static void SeamCancel()
    {
        PlayerManager.ItemCancelForTests = (hero, item) =>
        {
            if (item == Tome)
            {
                FireTome2Ai.Cancel(hero);
            }
        };
    }

    private static int[] Pattern(int start) => Enumerable.Range(0, 9).Select(i => start + i).ToArray();

    private static EntityScriptProxy Spawned(int slot = 5, EntityStatus status = EntityStatus.Normal)
    {
        var entity = CarryBench.Make("column");
        entity.SpriteType = Column;
        entity.Slot = slot;
        entity.Status = status;
        return entity;
    }

    /// <summary>A newborn of the bench is an entity of the table (the world gives it a slot and a status of the living; the bare proxy of the host has neither).</summary>
    private static void Alive(EntityScriptProxy born)
    {
        born.Slot = 5;
        born.Status = EntityStatus.Normal;
    }

    private sealed record World(BombHandsBench Bench, CameraSway Sway, List<EntityScriptProxy> Others)
    {
        internal EntityScriptProxy Hero => Bench.Hero;
    }

    /// <summary>A hero in the phase <paramref name="phase"/> at the counter <paramref name="counter"/>, with the animation <paramref name="animation"/>, the item <c>0x30</c> in use and a sway of its own (a block at the pattern 100 and a buffer at the pattern 500).</summary>
    private static World Make(int phase, int counter, uint animation, int slope = 0, uint item = Tome)
    {
        var bench = BombHandsBench.Create(effectStage: false);
        var sway = new CameraSway();
        bench.Host.CameraSway = sway;
        FireTome2Run.Post(sway, Pattern(100));
        Pattern(500).CopyTo(sway.Buffer, 0);
        var hero = bench.Hero;
        hero.PosX = 100 << 16;
        hero.PosY = 100 << 16;
        hero.PosZ = 0;
        hero.TargetAnimationId = animation;
        hero.Slope_18c = slope;
        hero.ItemInUse = item;
        hero.ItemUsePhase = phase;
        hero.ItemUseCounter = counter;
        bench.Host.OnBirth = Alive;
        SeamCancel();
        return new World(bench, sway, new List<EntityScriptProxy>());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d1) the pilot and the cancellation against the 61 vectors of the oracle.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ReplayPilot(FireTome2Vectors.Vector v)
    {
        var label = $"pilot vector {v.Id}";
        var phase = v.ReadInt(FireTome2Vectors.Phase);
        var bench = BombHandsBench.Create(effectStage: false);
        var sway = new CameraSway();
        bench.Host.CameraSway = sway;
        var hero = bench.Hero;
        hero.PosX = v.ReadInt(FireTome2Vectors.Word(0, 0x114), 100 << 16);
        hero.PosY = v.ReadInt(FireTome2Vectors.Word(0, 0x118), 100 << 16);
        hero.PosZ = v.ReadInt(FireTome2Vectors.Word(0, 0x11C), 1) - 1; // the DLL's Z is the binary's minus 1.
        hero.TargetAnimationId = (uint)v.ReadInt(FireTome2Vectors.Word(0, 0x88));
        hero.Slope_18c = v.ReadInt(FireTome2Vectors.Word(0, 0x18C));
        var itemBefore = v.Read(FireTome2Vectors.ItemInUse) ? (uint)v.ReadInt(FireTome2Vectors.ItemInUse) : Tome;
        hero.ItemInUse = itemBefore;
        hero.ItemUsePhase = phase;
        hero.ItemUseCounter = v.ReadInt(FireTome2Vectors.Counter);
        SeamCancel();

        // The block and the buffer: the words the code read are the vector's; the others are patterns, so that a word the code wrote shows against the one it left.
        var blockBefore = Enumerable.Range(0, 9).Select(i => v.Read(FireTome2Vectors.BlockBase + 4 * i) ? v.ReadInt(FireTome2Vectors.BlockBase + 4 * i) : 7000 + i).ToArray();
        var bufferBefore = Enumerable.Range(0, 9).Select(i => v.Read(FireTome2Vectors.BufferBase + 4 * i) ? v.ReadInt(FireTome2Vectors.BufferBase + 4 * i) : 5000 + i).ToArray();
        FireTome2Run.Post(sway, blockBefore);
        bufferBefore.CopyTo(sway.Buffer, 0);

        // The born entity of the phase 1 (the identity word read in the entity is the one kept at its birth, or it is not).
        EntityScriptProxy? keptBefore = null;
        if (v.Read(FireTome2Vectors.SpawnedPointer))
        {
            keptBefore = Spawned();
            if (v.ReadWord(FireTome2Vectors.SpawnedIdentity) != v.ReadWord(FireTome2Vectors.Word(7, 0xC8)))
            {
                // The identity changed: the slot was recycled (Status Destroyed) or taken off the table (Slot -1): both are read.
                if (v.Id % 2 == 0)
                {
                    keptBefore.Slot = -1;
                }
                else
                {
                    keptBefore.Status = EntityStatus.Destroyed;
                }
            }

            hero.ItemSpawnedEntity = keptBefore;
        }

        // The entities of the table (1 to 5) the code read: the type and, for the type 21, the status; every one is blocked by an entity (+0x20 non-zero).
        var others = new List<EntityScriptProxy>();
        for (var slot = 1; slot <= 5; slot++)
        {
            if (!v.Read(FireTome2Vectors.Word(slot, 0x68)))
            {
                continue;
            }

            var other = bench.AddOther(v.ReadInt(FireTome2Vectors.Word(slot, 0x68)));
            other.Status = (EntityStatus)v.ReadInt(FireTome2Vectors.Word(slot, 0x10));
            other.BlockedByEntity = hero.LogicContextEntity;
            others.Add(other);
        }

        bench.Host.RefuseCall = call => call == 0 && v.SpawnReturn == 0;
        bench.Host.OnBirth = Alive;

        var answer = FireTome2Ai.Driver(hero);

        Assert.True(v.Return == answer, $"{label}: answer {answer}, vector {v.Return}");
        Assert.True((v.Wrote(FireTome2Vectors.Phase) ? v.WroteInt(FireTome2Vectors.Phase) : phase) == hero.ItemUsePhase, $"{label}: phase {hero.ItemUsePhase}");
        var animationWord = FireTome2Vectors.Word(0, 0x88);
        Assert.True((v.Wrote(animationWord) ? (uint)v.WroteInt(animationWord) : (uint)v.ReadInt(animationWord)) == hero.TargetAnimationId, $"{label}: animation {hero.TargetAnimationId:X}");
        Assert.True((v.Wrote(FireTome2Vectors.ItemInUse) ? (uint)v.WroteInt(FireTome2Vectors.ItemInUse) : itemBefore) == hero.ItemInUse, $"{label}: item {hero.ItemInUse:X}");
        Assert.Equal(v.ReadInt(FireTome2Vectors.Counter), hero.ItemUseCounter); // the wrapper increments it, not the pilot.

        for (var i = 0; i < 9; i++)
        {
            Assert.True((v.Wrote(FireTome2Vectors.BlockBase + 4 * i) ? v.WroteInt(FireTome2Vectors.BlockBase + 4 * i) : blockBefore[i]) == Words(sway)[i], $"{label}: block word {i} is {Words(sway)[i]}");
            Assert.True((v.Wrote(FireTome2Vectors.BufferBase + 4 * i) ? v.WroteInt(FireTome2Vectors.BufferBase + 4 * i) : bufferBefore[i]) == sway.Buffer[i], $"{label}: buffer word {i} is {sway.Buffer[i]}");
        }

        // The births asked: SpawnWarpEntity(hero, global page, 21, x, y, z, 0), refused or not; the Z of the vector is the binary's.
        Assert.Equal(v.SpawnCalls.Count, bench.Host.SpawnCalls.Count);
        for (var k = 0; k < v.SpawnCalls.Count; k++)
        {
            var c = v.SpawnCalls[k];
            Assert.True((hero, false, (int)c[2], unchecked((int)c[3]), unchecked((int)c[4]), unchecked((int)c[5]) - 1, (int)c[6]) == bench.Host.SpawnCalls[k], $"{label}: birth {k} is {bench.Host.SpawnCalls[k]}");
        }

        // The pointer kept: written before the test of nullity (null for a refused birth), the entity the world gave otherwise; untouched when the code did not write it.
        var born = bench.Host.Born.Where(b => b.SpriteType == Column && !others.Contains(b)).ToList();
        if (v.Wrote(FireTome2Vectors.SpawnedPointer))
        {
            if (v.Writes[FireTome2Vectors.SpawnedPointer] == 0)
            {
                Assert.Null(hero.ItemSpawnedEntity);
            }
            else
            {
                Assert.Same(born.Single(), hero.ItemSpawnedEntity);
            }
        }
        else
        {
            Assert.Same(keptBefore, hero.ItemSpawnedEntity);
        }

        // The other entities of the table after the cancellation: the type 21 at the status 2 go to the status 3 and lose their blocker; nothing else moves.
        for (var k = 0; k < others.Count; k++)
        {
            var slot = Enumerable.Range(1, 5).Where(s => v.Read(FireTome2Vectors.Word(s, 0x68))).ElementAt(k);
            var statusWord = FireTome2Vectors.Word(slot, 0x10);
            var blockerWord = FireTome2Vectors.Word(slot, 0x20);
            var statusBefore = v.ReadInt(statusWord);
            var wroteStatus = v.Wrote(statusWord);
            Assert.True((wroteStatus ? v.WroteInt(statusWord) : statusBefore) == (int)others[k].Status, $"{label}: entity {slot} status {others[k].Status}");
            Assert.True(v.Wrote(blockerWord) == (others[k].BlockedByEntity is null), $"{label}: entity {slot} blocker {(others[k].BlockedByEntity is null ? "cleared" : "kept")}");
        }

        // Nothing else was born and no entity was destroyed by the pilot.
        Assert.Empty(bench.Host.Destroyed);
        Assert.Empty(bench.Host.DestroyedAlone);
    }

    private static int[] Words(CameraSway sway) => FireTome2Run.Words(sway);

    [Fact]
    public void D1_ThePilotAndTheCancellation_TheSixtyVectorsAndTheOneOfTheCancellationOfTheOracle_AreReplayed_AnswerPhaseAnimationItemPointerBlockBufferBirthsAndStatuses()
    {
        var vectors = FireTome2Vectors.Pilot();
        var cancel = FireTome2Vectors.Cancel();

        Assert.Equal(60, vectors.Count);
        Assert.Single(cancel);
        foreach (var v in vectors)
        {
            ReplayPilot(v);
            PlayerManager.ItemCancelForTests = null;
        }

        // Both sides of every front are in the sample: a birth and none, the three phases and the others, the guard acting and not, the identity kept and changed, the cancellation of the item 48 and the item 43 and 0 (neutral), the type 21 at the status 2 and at the others.
        Assert.Equal(3, vectors.Count(v => v.Wrote(FireTome2Vectors.SpawnedPointer) && v.Writes[FireTome2Vectors.SpawnedPointer] != 0)); // the births given.
        Assert.Equal(4, vectors.Count(v => v.Wrote(FireTome2Vectors.SpawnedPointer) && v.Writes[FireTome2Vectors.SpawnedPointer] == 0)); // the refused births.
        Assert.Contains(vectors, v => v.ReadInt(FireTome2Vectors.Phase) == 3);
        Assert.Contains(vectors, v => v.ReadInt(FireTome2Vectors.Phase) == 2 && v.Return == 0);
        Assert.Contains(vectors, v => v.ReadInt(FireTome2Vectors.Phase) == 2 && v.Return == 1);
        Assert.Contains(vectors, v => v.Wrote(FireTome2Vectors.Word(0, 0x88))); // the animation 0x34 of the end of the burning.
        Assert.Contains(vectors, v => v.ReadInt(FireTome2Vectors.ItemInUse) == 43);
        Assert.Contains(vectors, v => v.ReadInt(FireTome2Vectors.ItemInUse) == 48 && Enumerable.Range(1, 5).Any(s => v.Wrote(FireTome2Vectors.Word(s, 0x10))));
        Assert.Contains(vectors, v => v.Wrote(FireTome2Vectors.BlockBase) && v.WroteInt(FireTome2Vectors.BlockBase) == 1 && !v.Wrote(FireTome2Vectors.BlockBase + 20)); // the five words of a burning tick, the offsets untouched.

        // The cancellation of the critic: the zero buffer is copied to the block, the type 21 at the status 2 go to 3.
        var c = cancel[0];
        var world = Make(1, 0, 0);
        var others = new[] { world.Bench.AddOther(21), world.Bench.AddOther(21) };
        foreach (var other in others)
        {
            other.Status = EntityStatus.Normal;
            other.BlockedByEntity = world.Hero.LogicContextEntity;
        }

        Array.Clear(world.Sway.Buffer);
        FireTome2Ai.Cancel(world.Hero);

        Assert.All(c.Writes.Where(w => w.Key is >= FireTome2Vectors.BlockBase and < FireTome2Vectors.BlockBase + 36), w => Assert.Equal((int)w.Value, Words(world.Sway)[(int)(w.Key - FireTome2Vectors.BlockBase) / 4]));
        Assert.Equal(new int[9], Words(world.Sway));
        Assert.All(others, o => Assert.Equal((EntityStatus.Deactivated, true), (o.Status, o.BlockedByEntity is null)));
        Assert.Equal(Tome, world.Hero.ItemInUse); // the item is freed by the general cancellation, not by this one.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d2) to (d6) the pilot by hand.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void D2_APhaseAbove2OrNegative_AnswersZeroAndWritesNothing()
    {
        foreach (var phase in new[] { 3, 4, 100, int.MaxValue, -1, int.MinValue })
        {
            var world = Make(phase, 32, Hold);
            var entity = world.Bench.AddOther(Column);
            entity.Status = EntityStatus.Normal;
            var kept = Spawned();
            world.Hero.ItemSpawnedEntity = kept;

            var answer = FireTome2Ai.Driver(world.Hero);

            Assert.True(answer == 0, $"phase {phase}");
            Assert.Equal((Tome, phase, 32, Hold), (world.Hero.ItemInUse, world.Hero.ItemUsePhase, world.Hero.ItemUseCounter, world.Hero.TargetAnimationId));
            Assert.Equal(Pattern(100), Words(world.Sway));
            Assert.Equal(Pattern(500), world.Sway.Buffer);
            Assert.Same(kept, world.Hero.ItemSpawnedEntity);
            Assert.Equal(EntityStatus.Normal, entity.Status);
            Assert.Empty(world.Bench.Host.SpawnCalls);
        }
    }

    [Fact]
    public void D3_ARefusedBirth_KeepsTheNullPointer_AnswersOne_AndCapturesNothing()
    {
        var world = Make(0, 32, Hold);
        world.Hero.ItemSpawnedEntity = Spawned(); // a stale pointer: the refused birth overwrites it with null (written before the test of nullity).
        world.Bench.Host.RefuseCall = _ => true;

        var answer = FireTome2Ai.Driver(world.Hero);

        Assert.Equal(1, answer);
        Assert.Null(world.Hero.ItemSpawnedEntity);
        Assert.Single(world.Bench.Host.SpawnCalls);
        Assert.Equal(Pattern(500), world.Sway.Buffer); // no capture.
        Assert.Equal(Pattern(100), Words(world.Sway)); // the block is not written.
        Assert.Equal((0, Hold, Tome), (world.Hero.ItemUsePhase, world.Hero.TargetAnimationId, world.Hero.ItemInUse)); // the wrapper frees the item, not the pilot.
    }

    [Fact]
    public void D3b_TheBirthWaitsForTheAnimation0x33_AndTheGuardAnswers0InTheCastAnimations()
    {
        foreach (var animation in new[] { Cast, 0x34u, 0x31u, 0u })
        {
            var world = Make(0, 5, animation);
            var answer = FireTome2Ai.Driver(world.Hero);
            var guarded = animation != Cast; // any animation but the cast ones cancels the item (the guard answers 1) ...
            Assert.True(answer == (guarded ? 1 : 0), $"animation {animation:X}");
            Assert.Empty(world.Bench.Host.SpawnCalls); // ... and the cast animation 0x32 waits for the 0x33: no birth.
            Assert.Equal(0, world.Hero.ItemUsePhase);
        }

        var born = Make(0, 5, Hold);
        Assert.Equal(0, FireTome2Ai.Driver(born.Hero));
        Assert.Single(born.Bench.Host.SpawnCalls);
        Assert.Equal((false, 21), (born.Bench.Host.SpawnCalls[0].IsMapPage, born.Bench.Host.SpawnCalls[0].Index));
        Assert.Equal((born.Hero.PosX, born.Hero.PosY, born.Hero.PosZ, 0), (born.Bench.Host.SpawnCalls[0].X, born.Bench.Host.SpawnCalls[0].Y, born.Bench.Host.SpawnCalls[0].Z, born.Bench.Host.SpawnCalls[0].Direction));
    }

    [Fact]
    public void D4_TheCapturePrecedesTheWriteOfFlag_TheBlockOfTheTickOfTheBirthIs_1_0_2_0_2_AtTheCounter32_WithTheOffsetsAndTheReachLeftAlone()
    {
        var world = Make(0, 32, Hold);
        var preset = new[] { 1, 5, 6, 7, 8, 3, -2, 1, 1 };
        FireTome2Run.Post(world.Sway, preset);

        var answer = FireTome2Ai.Driver(world.Hero);

        Assert.Equal(0, answer);
        Assert.Equal(preset, world.Sway.Buffer); // the words of the block as they were BEFORE the pilot wrote them.
        Assert.Equal(new[] { 1, 0, 2, 0, 2, 3, -2, 1, 1 }, Words(world.Sway)); // Flag 1, LimitY = SpeedY = 32 >> 4, LimitX = SpeedX = 32 >> 6, the offsets and the reach as they were.
        Assert.Equal(1, world.Hero.ItemUsePhase);
        var column = Assert.Single(world.Bench.Host.Born.Where(b => b.SpriteType == Column));
        Assert.Same(column, world.Hero.ItemSpawnedEntity);

        // The zero block of the key at the tick of the birth.
        var zero = Make(0, 32, Hold);
        FireTome2Run.Post(zero.Sway, new int[9]);
        FireTome2Ai.Driver(zero.Hero);
        Assert.Equal(new[] { 1, 0, 2, 0, 2, 0, 0, 0, 0 }, Words(zero.Sway));
        Assert.Equal(new int[9], zero.Sway.Buffer);
    }

    [Fact]
    public void D5_AnEntityToDestroyThatIsNotRecycledYetIsAlive_RecycledOrTakenOffTheTableTheBurningEnds()
    {
        foreach (var status in new[] { EntityStatus.FlagToDestroy, EntityStatus.Normal, EntityStatus.Deactivated, EntityStatus.Loaded })
        {
            var world = Make(1, 153, Hold);
            world.Hero.ItemSpawnedEntity = Spawned(5, status);

            var answer = FireTome2Ai.Driver(world.Hero);

            Assert.True(answer == 0, status.ToString());
            Assert.Equal(new[] { 1, 2, 9, 2, 9, 105, 106, 107, 108 }, Words(world.Sway)); // 153 >> 4 = 9 and 153 >> 6 = 2: the five words, the offsets and the reach left.
            Assert.Equal((1, Hold), (world.Hero.ItemUsePhase, world.Hero.TargetAnimationId));
        }

        // Recycled (the status 0), or taken off the table (the slot -1): the buffer comes back, the animation 0x34, the phase 2.
        foreach (var (status, slot) in new[] { (EntityStatus.Destroyed, 5), (EntityStatus.Normal, -1), (EntityStatus.Destroyed, -1) })
        {
            var world = Make(1, 154, Hold);
            world.Hero.ItemSpawnedEntity = Spawned(slot, status);

            var answer = FireTome2Ai.Driver(world.Hero);

            Assert.True(answer == 0, $"{status} {slot}");
            Assert.Equal(Pattern(500), Words(world.Sway));
            Assert.Equal(Pattern(500), world.Sway.Buffer); // the buffer is not cleared.
            Assert.Equal((2, End), (world.Hero.ItemUsePhase, world.Hero.TargetAnimationId));
            Assert.Equal(Tome, world.Hero.ItemInUse);
        }

        // The phase 2 waits for the end of the animation 0x34 and then frees the item (the answer 1), with no guard.
        foreach (var (animation, expected) in new[] { (End, 0), (0u, 1), (Hold, 1), (0x31u, 1) })
        {
            var world = Make(2, 181, animation, slope: 4);
            Assert.Equal(expected, FireTome2Ai.Driver(world.Hero));
            Assert.Equal(Pattern(100), Words(world.Sway));
        }
    }

    [Theory]
    [InlineData(15, 0, 0)]
    [InlineData(16, 1, 0)]
    [InlineData(63, 3, 0)]
    [InlineData(64, 4, 1)]
    [InlineData(127, 7, 1)]
    [InlineData(128, 8, 2)]
    public void D6_TheSteps_LimitYAndSpeedYAreTheCounterShiftedBy4_LimitXAndSpeedXTheCounterShiftedBy6(int counter, int y, int x)
    {
        var world = Make(1, counter, Hold);
        world.Hero.ItemSpawnedEntity = Spawned();

        Assert.Equal(0, FireTome2Ai.Driver(world.Hero));

        Assert.Equal(new[] { 1, x, y, x, y }, Words(world.Sway).Take(5));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c1) and (c2) the cancellation.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C1_TheCancellation_RestoresTheBufferWithNoGuard_PutsTheColumnsAtTheStatus2AtTheStatus3AndClearsTheirBlocker_NothingElseMoves()
    {
        var world = Make(1, 50, Hold);
        var blocker = world.Hero.LogicContextEntity;
        EntityScriptProxy Entity(int type, EntityStatus status)
        {
            var e = world.Bench.AddOther(type);
            e.Status = status;
            e.BlockedByEntity = blocker;
            return e;
        }

        var normal = Entity(21, EntityStatus.Normal);
        var loaded = Entity(21, EntityStatus.Loaded);
        var deactivated = Entity(21, EntityStatus.Deactivated);
        var destroying = Entity(21, EntityStatus.FlagToDestroy);
        var free = Entity(21, EntityStatus.Destroyed);
        var second = Entity(21, EntityStatus.Normal);
        var others = new[] { 20, 22, 23, 24, 17, 0x115 }.Select(type => Entity(type, EntityStatus.Normal)).ToList(); // the other types, and the entity of the map of index 21 (the type 0x115).

        FireTome2Ai.Cancel(world.Hero);

        Assert.All(new[] { normal, second }, e => Assert.Equal((EntityStatus.Deactivated, true), (e.Status, e.BlockedByEntity is null)));
        Assert.Equal((EntityStatus.Loaded, EntityStatus.Deactivated, EntityStatus.FlagToDestroy, EntityStatus.Destroyed), (loaded.Status, deactivated.Status, destroying.Status, free.Status));
        Assert.All(new[] { loaded, deactivated, destroying, free }, e => Assert.Same(blocker, e.BlockedByEntity));
        Assert.All(others, e => Assert.Equal((EntityStatus.Normal, blocker), (e.Status, e.BlockedByEntity)));
        Assert.Equal(Pattern(500), Words(world.Sway)); // the nine words of the buffer, the offsets and the reach included.
        Assert.Equal((Tome, 1, 50, Hold), (world.Hero.ItemInUse, world.Hero.ItemUsePhase, world.Hero.ItemUseCounter, world.Hero.TargetAnimationId));

        // With no guard: a zero buffer overwrites a non-zero block, a stale one is restored as it is.
        var zero = Make(0, 3, Hold);
        Array.Clear(zero.Sway.Buffer);
        FireTome2Ai.Cancel(zero.Hero);
        Assert.Equal(new int[9], Words(zero.Sway));
    }

    [Fact]
    public void C2_TheGuardInThePhase1_RestoresTheBuffer_AnswersOne_AndLeavesThePhaseAndThePointer_WhateverTheItemInUse()
    {
        foreach (var (item, slope, animation) in new[] { (Tome, 4, Hold), (Tome, 0, 0x31u), (Tome, 0, End), (0x2Bu, 0, 0x31u), (0u, 0, 0u) })
        {
            var world = Make(1, 77, animation, slope, item);
            var kept = Spawned();
            world.Hero.ItemSpawnedEntity = kept;
            var column = world.Bench.AddOther(21);
            column.Status = EntityStatus.Normal;

            var answer = FireTome2Ai.Driver(world.Hero);

            var label = $"item {item:X} slope {slope} animation {animation:X}";
            Assert.True(answer == 1, label);
            Assert.True(Pattern(500).SequenceEqual(Words(world.Sway)), $"{label}: block [{string.Join(",", Words(world.Sway))}]");
            Assert.True(world.Hero.ItemUsePhase == 1, label);
            Assert.Same(kept, world.Hero.ItemSpawnedEntity);
            Assert.True(world.Hero.ItemInUse == 0, label); // the general cancellation frees the item.
            Assert.True(column.Status == (item == Tome ? EntityStatus.Deactivated : EntityStatus.Normal), $"{label}: column {column.Status}"); // the 0x80034E08 only when the item is 0x30.
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e1) to (e6) E28.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed record Made(int Effect, bool MapTable, int Animation, int X, int Y, int Z);

    private static (BombHandsBench Bench, EntityScriptProxy Column, List<Made> Made) ColumnWorld(int x, int y, int zRaw, bool fullReservoir = false)
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var column = bench.AddOther(Column);
        column.Status = EntityStatus.Deactivated;
        column.PosX = x;
        column.PosY = y;
        column.PosZ = zRaw - 1; // the DLL's Z is the binary's minus 1.
        var made = new List<Made>();
        NativeEffects.CreatedForTests = slot => made.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
        if (fullReservoir)
        {
            while (bench.Host.EffectStage!.Reservoir.Create(false, 14, 0, 0, 0, 0) is not null)
            {
            }
        }

        return (bench, column, made);
    }

    [Fact]
    public void E1_E28_TheTwoHundredAndThreeVectorsOfTheOracle_AreReplayed_EightEffects19Animation2_TheGeneratorAndTheDestruction()
    {
        var vectors = FireTome2Vectors.Column();

        Assert.Equal(203, vectors.Count);
        foreach (var v in vectors)
        {
            var (bench, column, made) = ColumnWorld(v.X, v.Y, v.ZRaw);
            GameRandom.RandomSeed = v.SeedBefore;

            FireTome2Ai.ColumnEnds(column);
            bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

            var label = $"E28 vector {v.Id}";
            Assert.True(v.SeedAfter == unchecked((uint)GameRandom.RandomSeed), $"{label}: generator {unchecked((uint)GameRandom.RandomSeed)}, vector {v.SeedAfter}");
            Assert.Equal(v.Effects.Select(e => new Made(e.Effect, false, e.Animation, e.X, e.Y, e.Z)), made); // the eight effects, in order, at the binary's Z.
            Assert.Equal(new[] { (column, -1) }, bench.Host.Destroyed); // DestroyEntity(self, -1).
            Assert.Equal(EntityStatus.FlagToDestroy, column.Status);
            Assert.Equal((v.X, v.Y, v.ZRaw - 1), (column.PosX, column.PosY, column.PosZ));
        }

        Assert.All(vectors, v => Assert.Equal((8, 1, 0), (v.Effects.Count, v.Destructions, v.Effects[0].Bank)));
        Assert.Contains(vectors, v => v.Id == 202 && v.SeedBefore == 0xA468A517u); // the seed of the faithful key at its tick 154.
    }

    [Fact]
    public void E2_AFullReservoir_CreatesNoEffect_ButDrawsTheEightNumbersAllTheSame_AndDestroys()
    {
        var (bench, column, made) = ColumnWorld(100 << 16, 100 << 16, 2, fullReservoir: true);
        GameRandom.RandomSeed = 0xA468A517u;
        var before = unchecked((uint)GameRandom.RandomSeed);

        FireTome2Ai.ColumnEnds(column);
        bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

        Assert.Empty(made);
        Assert.Equal(8, FireTome2Run.Steps(before, unchecked((uint)GameRandom.RandomSeed)));
        Assert.Equal(new[] { (column, -1) }, bench.Host.Destroyed);
        Assert.Equal(128, bench.Host.EffectStage.Reservoir.ActiveSlots.Count()); // nothing was added, nothing freed.
    }

    [Fact]
    public void E3_TheDestructionWithMinusOne_ReadsTheBreakEffectOfTheHeader_TheEffectAttachedIs8OnAnEntityThatHasIt_AndNoneOnTheRealSprite21()
    {
        using var run = new FireTome2Run("e3");
        var proxy = run.Arena.Arc.Proxy;
        var world = (IEntityWorldContext)proxy;

        EntityScriptProxy Born(int x)
        {
            var e = world.SpawnEntityBySpriteIndex(run.Arena.Hero, false, Column, x << 16, 100 << 16, 0, 0);
            Assert.NotNull(e);
            e!.Status = EntityStatus.Deactivated;
            return e;
        }

        var real = Born(120);
        Assert.Equal(0, real.BreakEffect); // the real header of the sprite 21 has no break effect.
        var marked = Born(140);
        marked.BreakEffect = 8;

        FireTome2Ai.ColumnEnds(real);
        FireTome2Ai.ColumnEnds(marked);
        proxy.EffectStage!.RunFrame(0, Array.Empty<bool>());

        var attached = proxy.EffectStage.Reservoir.ActiveSlots.Where(s => s.Mode == 1).ToList();
        var attachedSlot = Assert.Single(attached);
        Assert.Equal(8, attachedSlot.TargetEffect);
        Assert.Equal(16, proxy.EffectStage.Reservoir.ActiveSlots.Count(s => s.Mode == 0 && s.TargetEffect == 19)); // the 2 x 8 explosions.
        Assert.Equal((EntityStatus.FlagToDestroy, EntityStatus.FlagToDestroy), (real.Status, marked.Status));
    }

    [Fact]
    public void E4_TheGeneratorMakesExactlyEightDraws()
    {
        foreach (var seed in new[] { 0x12345678u, 0u, 0xFFFFFFFFu, 0xA468A517u })
        {
            var (bench, column, made) = ColumnWorld(100 << 16, 100 << 16, 2);
            GameRandom.RandomSeed = seed;

            FireTome2Ai.ColumnEnds(column);
            bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

            Assert.Equal(8, FireTome2Run.Steps(seed, unchecked((uint)GameRandom.RandomSeed)));
            Assert.Equal(8, made.Count);
        }
    }

    [Fact]
    public void E5_TheKey_E28IsRegisteredForTheType21Alone_OnARegistryOfItsOwn()
    {
        var registry = new NativeSpriteRegistry();
        using (FireTome2Ai.Register(registry))
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 28, 21, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 28, 0x115, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 28, 20, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 28, 17, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 27, 21, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 29, 21, out _));
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate - 1, 28, 21, out _));
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 28, 21, out _)); // the disposable takes it off.
        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 28) }, FireTome2Ai.Keys);
        Assert.Equal(new[] { 21 }, FireTome2Ai.SpriteTypes);
    }

    [Fact]
    public void E6_TheTables_ThePositionsOfTheEffectsAreTheEntries4kOfTheTwo32DirectionTables_ByHand()
    {
        var x = new[] { 0, -543, -768, -543, 0, 543, 768, 543 };
        var y = new[] { 512, 362, 0, -362, -512, -362, 0, 362 };
        Assert.Equal(x, Enumerable.Range(0, 8).Select(k => (int)AnimationTables.OffsetXList[4 * k]));
        Assert.Equal(y, Enumerable.Range(0, 8).Select(k => (int)AnimationTables.OffsetYList[4 * k]));

        // By hand, at the seed of the key: the first explosion is at (X + 0 * r, Y + 512 * r), r = hi(s * 0x1801) + 0x320.
        var (bench, column, made) = ColumnWorld(100 << 16, 100 << 16, 2);
        GameRandom.RandomSeed = 0xA468A517u;
        var s = 0xA468A517u;
        var radii = new int[8];
        for (var k = 0; k < 8; k++)
        {
            s = unchecked(s * 0x7D2B89DDu + 0xE06A02E7u);
            radii[k] = (int)(((ulong)s * 0x1801) >> 32) + 0x320;
        }

        FireTome2Ai.ColumnEnds(column);
        bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

        Assert.Equal(Enumerable.Range(0, 8).Select(k => (100 * 65536 + x[k] * radii[k], 100 * 65536 + y[k] * radii[k])), made.Select(m => (m.X, m.Y)));
        Assert.All(made, m => Assert.Equal((19, false, 2, 2), (m.Effect, m.MapTable, m.Animation, m.Z)));
    }
}
