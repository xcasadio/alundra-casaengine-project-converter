#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1 bis, phase A2 (docs/plan-e20-heros.md section 1.31.6, tests (d1), (d2), (c3)): the pilot <c>0x80035A84</c> of the fire tome 1 (<see cref="FireTome1Ai.Drive"/>) on a hero and entities made by hand. The expected values of (d1) are the 3 183 comparable vectors of the real code of the binary (the guard,
/// the general cancellation, the cancellation of the fire tome 1 and <c>DestroyEntityNoFx</c> being the real code, the births and the effect stubs journalled), replayed word by word through the adapter of T0 (b) (<c>t0/mapping.md</c>: a pointer is a slot; an identity word read different from the one stored is a DEAD
/// reference, a proxy <see cref="EntityStatus.Destroyed"/> with the slot -1; the Z of the DLL is the binary's minus 1; the vectors flagged <c>binary_only</c> are skipped). The guard reaches the cancellation by <see cref="PlayerManager.ItemCancelForTests"/> = <see cref="FireTome1Ai.Cancel"/> ONLY when the
/// item in use is <c>0x2F</c>, neutral otherwise (R-EL-4: the dispatcher of the binary).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome1DriverTests : IDisposable
{
    private const uint Tome = 0x2F;
    private const uint Hold = PlayerManager.TomeHoldAnimationId;
    private const uint End = PlayerManager.TomeEndAnimationId;
    private const ulong SentinelSeed = 0x5EED5EEDu;
    private const uint SentinelAnimation = 0x7777;

    public FireTome1DriverTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
    }

    /// <summary>The cancellation of h0a for the tests: the one of the fire tome 1 only when the item in use is <c>0x2F</c>, neutral otherwise, as the dispatcher of the binary (<c>0x800350C0</c>).</summary>
    private static void SeamCancel()
    {
        PlayerManager.ItemCancelForTests = (hero, item) =>
        {
            if (item == Tome)
            {
                FireTome1Ai.Cancel(hero);
            }
        };
    }

    private sealed record Made(int Effect, bool MapTable, int Animation, int X, int Y, int Z);

    /// <summary>A newborn of the bench is an entity of the table: the world gives it a slot (the pointer the stub of the binary answered) and a status of the living.</summary>
    private static void Alive(EntityScriptProxy born, int slot)
    {
        born.Slot = slot;
        born.Status = EntityStatus.Normal;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d1) the pilot against the 3 183 comparable vectors.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (int Type, long Pointer)[] StubbedBirths(FireTome1Vectors.Vector v)
        => v.Journal.Where(j => j.Name == "spawn_39F58").Select(j => ((int)j.Arguments[2], v.SpawnReturns![(int)j.Arguments[2]])).ToArray();

    private static void Replay(FireTome1Vectors.Vector v, ref int births, ref int refused, ref int guardCancelled, ref int destroyedColumns)
    {
        var label = $"pilot vector {v.Id}";
        var bench = BombHandsBench.Create(effectStage: true);
        var made = new List<Made>();
        NativeEffects.CreatedForTests = slot => made.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
        var hero = bench.Hero;
        hero.PosX = v.ReadInt(FireTome1Vectors.Word(0, 0x114), 100 << 16);
        hero.PosY = v.ReadInt(FireTome1Vectors.Word(0, 0x118), 100 << 16);
        hero.PosZ = v.ReadInt(FireTome1Vectors.Word(0, 0x11C), 1) - 1; // the DLL's Z is the binary's minus 1.
        hero.TargetAnimationId = v.Read(FireTome1Vectors.Word(0, 0x88)) ? (uint)v.ReadInt(FireTome1Vectors.Word(0, 0x88)) : SentinelAnimation;
        hero.Slope_18c = v.ReadInt(FireTome1Vectors.Word(0, 0x18C));
        var itemBefore = v.Read(FireTome1Vectors.ItemInUse) ? (uint)v.ReadInt(FireTome1Vectors.ItemInUse) : Tome;
        var phase = v.ReadInt(FireTome1Vectors.Phase);
        hero.ItemInUse = itemBefore;
        hero.ItemUsePhase = phase;
        hero.ItemUseCounter = v.ReadInt(FireTome1Vectors.Counter);
        SeamCancel();
        GameRandom.RandomSeed = v.Read(FireTome1Vectors.GeneratorWord) ? (ulong)(uint)v.ReadWord(FireTome1Vectors.GeneratorWord) : SentinelSeed;

        // The entities of the table the code read (the cancellation walks them): the type, the status, the place; every one is blocked by an entity (+0x20 non-zero).
        var table = new SortedDictionary<int, EntityScriptProxy>();
        foreach (var slot in v.Reads.Keys.Where(FireTome1Vectors.IsEntityWord).Select(FireTome1Vectors.SlotOf).Distinct().Where(s => s >= 1).OrderBy(s => s))
        {
            if (!v.Read(FireTome1Vectors.Word(slot, 0x68)) && !v.Read(FireTome1Vectors.Word(slot, 0x10)))
            {
                continue;
            }

            var entity = bench.AddOther(v.ReadInt(FireTome1Vectors.Word(slot, 0x68)));
            entity.Slot = slot;
            entity.Status = (EntityStatus)v.ReadInt(FireTome1Vectors.Word(slot, 0x10));
            entity.PosX = v.ReadInt(FireTome1Vectors.Word(slot, 0x114));
            entity.PosY = v.ReadInt(FireTome1Vectors.Word(slot, 0x118));
            entity.PosZ = v.ReadInt(FireTome1Vectors.Word(slot, 0x11C), 1) - 1;
            entity.BlockedByEntity = hero.LogicContextEntity;
            table[slot] = entity;
        }

        // The column kept at its birth (the phase 1): the pointer, and the identity word read in the entity against the one kept. A different identity is a dead reference (rule 2 of T0 (b)).
        EntityScriptProxy? keptBefore = null;
        if (v.Read(FireTome1Vectors.SpawnedPointer) && v.ReadWord(FireTome1Vectors.SpawnedPointer) != 0)
        {
            var pointer = v.ReadWord(FireTome1Vectors.SpawnedPointer);
            var keptSlot = FireTome1Vectors.SlotOf(pointer);
            Assert.True(FireTome1Vectors.Word(keptSlot, 0) == pointer, $"{label}: the pointer kept is the start of an entity");
            var identityChanged = v.Read(FireTome1Vectors.SpawnedIdentity) && v.Read(pointer + 4) && v.ReadWord(FireTome1Vectors.SpawnedIdentity) != v.ReadWord(pointer + 4);
            keptBefore = identityChanged
                ? new EntityScriptProxy { Status = EntityStatus.Destroyed, Slot = -1, SpriteType = 20 }
                : table.TryGetValue(keptSlot, out var inTable) ? inTable : new EntityScriptProxy { Status = EntityStatus.Normal, Slot = keptSlot, SpriteType = 20 };
            hero.ItemSpawnedEntity = keptBefore;
        }

        // The births: the stub of the binary answered a pointer by type (0 refuses), in the order of the journal.
        var stubbed = StubbedBirths(v);
        bench.Host.RefuseCall = call => call < stubbed.Length && stubbed[call].Pointer == 0;
        var nextBirth = 0;
        bench.Host.OnBirth = born =>
        {
            Alive(born, FireTome1Vectors.SlotOf(stubbed[nextBirth].Pointer));
            nextBirth++;
        };

        var answer = FireTome1Ai.Drive(hero);
        bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

        Assert.True(v.Return == answer, $"{label}: answer {answer}, vector {v.Return}");
        Assert.True((v.Wrote(FireTome1Vectors.Phase) ? v.WroteInt(FireTome1Vectors.Phase) : phase) == hero.ItemUsePhase, $"{label}: phase {hero.ItemUsePhase}");
        var animationWord = FireTome1Vectors.Word(0, 0x88);
        var animationBefore = v.Read(animationWord) ? (uint)v.ReadInt(animationWord) : SentinelAnimation;
        Assert.True((v.Wrote(animationWord) ? (uint)v.WroteInt(animationWord) : animationBefore) == hero.TargetAnimationId, $"{label}: animation {hero.TargetAnimationId:X}");
        Assert.True((v.Wrote(FireTome1Vectors.ItemInUse) ? (uint)v.WroteInt(FireTome1Vectors.ItemInUse) : itemBefore) == hero.ItemInUse, $"{label}: item {hero.ItemInUse:X}");
        Assert.True(v.ReadInt(FireTome1Vectors.Counter) == hero.ItemUseCounter, $"{label}: the counter {hero.ItemUseCounter} is the wrapper's, not the pilot's");
        Assert.True(!v.Wrote(FireTome1Vectors.Word(0, 0x114)) && !v.Wrote(FireTome1Vectors.Word(0, 0x11C)), $"{label}: the hero is not moved");

        // The generator: the machine has 32 bits of state; the pilot draws once for a ball (the direction), refused or not.
        var expectedSeed = v.Wrote(FireTome1Vectors.GeneratorWord) ? (uint)v.WroteWord(FireTome1Vectors.GeneratorWord) : (uint)(v.Read(FireTome1Vectors.GeneratorWord) ? v.ReadWord(FireTome1Vectors.GeneratorWord) : (long)SentinelSeed);
        Assert.True(expectedSeed == unchecked((uint)GameRandom.RandomSeed), $"{label}: generator {unchecked((uint)GameRandom.RandomSeed):X}, vector {expectedSeed:X}");

        // The births asked: SpawnWarpEntity(hero, global page, type, x, y, z, direction); the Z of the vector is the binary's.
        var calls = v.Journal.Where(j => j.Name == "spawn_39F58").ToList();
        Assert.True(calls.Count == bench.Host.SpawnCalls.Count, $"{label}: {bench.Host.SpawnCalls.Count} births asked (vector {calls.Count})");
        for (var k = 0; k < calls.Count; k++)
        {
            var c = calls[k].Arguments;
            Assert.True((hero, false, (int)c[2], unchecked((int)c[3]), unchecked((int)c[4]), unchecked((int)c[5]) - 1, (int)c[6]) == bench.Host.SpawnCalls[k], $"{label}: birth {k} is {bench.Host.SpawnCalls[k]}");
        }

        // The pointer kept: written before the test of nullity (null for a refused birth), the entity the world gave otherwise; untouched when the code did not write it.
        var bornEntities = bench.Host.Born.Where(b => b != hero && !table.ContainsValue(b)).ToList();
        if (v.Wrote(FireTome1Vectors.SpawnedPointer))
        {
            if (v.WroteWord(FireTome1Vectors.SpawnedPointer) == 0)
            {
                Assert.Null(hero.ItemSpawnedEntity);
                refused++;
            }
            else
            {
                var column = Assert.Single(bornEntities, b => b.SpriteType == 20);
                Assert.Same(column, hero.ItemSpawnedEntity);
                Assert.Equal(FireTome1Vectors.SlotOf(v.WroteWord(FireTome1Vectors.SpawnedPointer)), column.Slot);
                births++;
            }
        }
        else
        {
            Assert.Same(keptBefore, hero.ItemSpawnedEntity);
        }

        // The ball born: the force of fall up (0x80000), the wait 0x10, no target; the others of the vector's journal are the column and the refusals.
        foreach (var (type, pointer) in stubbed.Where(s => s.Type == 23))
        {
            var ballSlot = pointer == 0 ? 0 : FireTome1Vectors.SlotOf(pointer);
            var ball = bornEntities.SingleOrDefault(b => b.SpriteType == 23);
            if (pointer == 0 || !v.Wrote(FireTome1Vectors.Word(ballSlot, 0xB8)))
            {
                Assert.Null(ball);
                continue;
            }

            Assert.NotNull(ball);
            Assert.True(v.WroteInt(FireTome1Vectors.Word(ballSlot, 0xB8)) == ball!.ForceZ, $"{label}: the force of the ball {ball.ForceZ}");
            Assert.True(v.WroteInt(FireTome1Vectors.Word(ballSlot, 0x274)) == TomeWords.Read274(ball), $"{label}: the wait of the ball {TomeWords.Read274(ball)}");
            Assert.True(v.WroteInt(FireTome1Vectors.Word(ballSlot, 0x278)) == ball.DelayOrAngleOrEntityId, $"{label}: the target of the ball {ball.DelayOrAngleOrEntityId}");
            Assert.Null(ball.SpellTarget);
            Assert.Equal(ballSlot, ball.Slot);
        }

        // The cancellation by the guard (the real code in the vector): the statuses of the table, the blockers, the effects (the Z in the binary's frame) and the destructions.
        var journaledEffects = v.Journal.Where(j => j.Name == "effect_3BDD8").ToList();
        foreach (var (slot, entity) in table)
        {
            var statusWord = FireTome1Vectors.Word(slot, 0x10);
            var wroteStatus = v.Wrote(statusWord);
            var destroyed = wroteStatus && v.WroteInt(statusWord) == 4;
            Assert.True(destroyed == bench.Host.DestroyedAlone.Contains(entity), $"{label}: entity {slot} destroyed {destroyed}");
            if (!destroyed)
            {
                var expected = wroteStatus ? v.WroteInt(statusWord) : v.ReadInt(statusWord);
                Assert.True(expected == (int)entity.Status, $"{label}: entity {slot} status {entity.Status}");
            }
            else
            {
                destroyedColumns++;
            }

            Assert.True(v.Wrote(FireTome1Vectors.Word(slot, 0x20)) == (entity.BlockedByEntity is null), $"{label}: entity {slot} blocker {(entity.BlockedByEntity is null ? "cleared" : "kept")}");
        }

        Assert.Equal(journaledEffects.Count, made.Count);
        for (var k = 0; k < journaledEffects.Count; k++)
        {
            var a = journaledEffects[k].Arguments; // CreateEffectEntity(0, 0x13, 2, X, Y, Z): the bank, the effect, the animation, the raw position in the binary's frame.
            Assert.True(new Made((int)a[1], a[0] != 0, (int)a[2], unchecked((int)a[3]), unchecked((int)a[4]), unchecked((int)a[5])) == made[k], $"{label}: effect {k} is {made[k]}");
        }

        Assert.True(journaledEffects.Count == bench.Host.DestroyedAlone.Count, $"{label}: {bench.Host.DestroyedAlone.Count} destructions");
        Assert.Empty(bench.Host.Destroyed);
        if (v.Return == 1 && !v.Wrote(FireTome1Vectors.SpawnedPointer) && v.ReadInt(FireTome1Vectors.ItemInUse) != 0 && phase is 0 or 1)
        {
            guardCancelled++;
        }
    }

    [Fact]
    public void D1_ThePilot_ThreeThousandOneHundredAndEightyThreeVectorsOfTheOracle_AreReplayed_AnswerPhaseAnimationItemPointerBirthsGeneratorAndTheCancellationOfTheGuard()
    {
        var all = FireTome1Vectors.Driver();
        var vectors = all.Where(v => !v.BinaryOnly).ToList();

        Assert.Equal(3270, all.Count);
        Assert.Equal(3183, vectors.Count);
        var births = 0;
        var refused = 0;
        var guardCancelled = 0;
        var destroyedColumns = 0;
        foreach (var v in vectors)
        {
            Replay(v, ref births, ref refused, ref guardCancelled, ref destroyedColumns);
            PlayerManager.ItemCancelForTests = null;
        }

        // Both sides of every front are in the sample: a column born and refused, a ball born and refused, the three phases and the others, the identity kept and changed, the guard acting and not, the cancellation of the item 0x2F and of the others (neutral), a column destroyed with its effect.
        Assert.True(births > 0 && refused > 0, $"columns born {births}, refused {refused}");
        Assert.True(guardCancelled > 0, "the guard cancels");
        Assert.True(destroyedColumns > 0, "a column destroyed by the cancellation");
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) == 0 && v.Wrote(FireTome1Vectors.Phase));
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) == 1 && v.Journal.Any(j => j.Name == "spawn_39F58" && j.Arguments[2] == 23));
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) == 1 && v.Wrote(FireTome1Vectors.Word(0, 0x88)) && v.WroteInt(FireTome1Vectors.Word(0, 0x88)) == End);
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) == 2 && v.Return == 0);
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) == 2 && v.Return == 1);
        Assert.Contains(vectors, v => v.ReadInt(FireTome1Vectors.Phase) is < 0 or > 2 && v.Return == 0);
        Assert.Contains(vectors, v => v.Journal.Any(j => j.Name == "spawn_39F58" && j.Arguments[2] == 23) && v.SpawnReturns![23] == 0); // a ball refused: the draw is made all the same.
        Assert.Contains(vectors, v => v.Read(FireTome1Vectors.ItemInUse) && v.ReadInt(FireTome1Vectors.ItemInUse) == 43 && v.Return == 1);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d2) by hand: the refusals; (c3) the cancellation by the guard.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static BombHandsBench Hero(int phase, int counter, uint animation, int slope = 0, uint item = Tome)
    {
        var bench = BombHandsBench.Create(effectStage: true);
        var hero = bench.Hero;
        hero.PosX = 100 << 16;
        hero.PosY = 100 << 16;
        hero.PosZ = 0;
        hero.TargetAnimationId = animation;
        hero.Slope_18c = slope;
        hero.ItemInUse = item;
        hero.ItemUsePhase = phase;
        hero.ItemUseCounter = counter;
        bench.Host.OnBirth = born => Alive(born, 5);
        SeamCancel();
        return bench;
    }

    [Fact]
    public void D2_TheColumnRefused_StoresTheNullPointer_AnswersOne_AndWritesNothingElse()
    {
        var bench = Hero(0, 32, Hold);
        var stale = new EntityScriptProxy { Slot = 5, Status = EntityStatus.Normal, SpriteType = 20 };
        bench.Hero.ItemSpawnedEntity = stale; // a stale pointer: the refused birth overwrites it with null (written before the test of nullity).
        bench.Host.RefuseCall = _ => true;
        GameRandom.RandomSeed = SentinelSeed;

        var answer = FireTome1Ai.Drive(bench.Hero);

        Assert.Equal(1, answer);
        Assert.Null(bench.Hero.ItemSpawnedEntity);
        Assert.Single(bench.Host.SpawnCalls);
        Assert.Equal((false, 20), (bench.Host.SpawnCalls[0].IsMapPage, bench.Host.SpawnCalls[0].Index));
        Assert.Equal((0, Hold, Tome, 32), (bench.Hero.ItemUsePhase, bench.Hero.TargetAnimationId, bench.Hero.ItemInUse, bench.Hero.ItemUseCounter)); // the wrapper frees the item, not the pilot; the phase does not advance.
        Assert.Equal(SentinelSeed, GameRandom.RandomSeed); // no number drawn.
        Assert.Empty(bench.Host.Born.Where(b => b != bench.Hero));
    }

    [Fact]
    public void D2_TheBirthOfTheColumn_WaitsForTheAnimation0x33_AtTheHeroPlace_InTheSameTickThePhase1RunsAndNoBallIsBornAtTheCounter33()
    {
        foreach (var animation in new[] { PlayerManager.TomeCastAnimationId, 0u })
        {
            var waiting = Hero(0, 5, animation);
            var answer = FireTome1Ai.Drive(waiting.Hero);
            Assert.True(answer == (animation == PlayerManager.TomeCastAnimationId ? 0 : 1), $"animation {animation:X}");
            Assert.Empty(waiting.Host.SpawnCalls);
            Assert.Equal(0, waiting.Hero.ItemUsePhase);
        }

        // Born at the hero's X, Y and Z, direction 0; the phase 1 runs in the same tick: the counter 33 is not a multiple of 8 for the tick of the birth, so no ball.
        var born = Hero(0, 33, Hold);
        born.Hero.PosZ = 7;
        GameRandom.RandomSeed = SentinelSeed;
        Assert.Equal(0, FireTome1Ai.Drive(born.Hero));
        var call = Assert.Single(born.Host.SpawnCalls);
        Assert.Equal((born.Hero, false, 20, born.Hero.PosX, born.Hero.PosY, 7, 0), call);
        Assert.Equal(1, born.Hero.ItemUsePhase);
        Assert.Same(Assert.Single(born.Host.Born, b => b.SpriteType == 20), born.Hero.ItemSpawnedEntity);
        Assert.Equal(SentinelSeed, GameRandom.RandomSeed);

        // The same tick with the counter 32 (a multiple of 8): the ball is born at once.
        var both = Hero(0, 32, Hold);
        Assert.Equal(0, FireTome1Ai.Drive(both.Hero));
        Assert.Equal(new[] { 20, 23 }, both.Host.SpawnCalls.Select(c => c.Index));
    }

    [Fact]
    public void D2_TheBallRefused_WritesNothing_ButTheDrawIsConsumed_TheBallBorn_FliesUp16PixelsAboveInZ_WithTheWaitAndNoTarget()
    {
        var refused = Hero(1, 8, Hold);
        refused.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = 1, Status = EntityStatus.Normal, SpriteType = 20 };
        refused.Host.RefuseCall = _ => true;
        GameRandom.RandomSeed = SentinelSeed;

        Assert.Equal(0, FireTome1Ai.Drive(refused.Hero));

        Assert.Single(refused.Host.SpawnCalls);
        Assert.Empty(refused.Host.Born.Where(b => b != refused.Hero));
        Assert.NotEqual(SentinelSeed, GameRandom.RandomSeed); // the draw of the direction is consumed.

        var born = Hero(1, 16, Hold);
        born.Hero.PosZ = 3;
        born.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = 1, Status = EntityStatus.Normal, SpriteType = 20 };
        GameRandom.RandomSeed = SentinelSeed;
        var expectedDirection = EnemyHelpers.Below(32); // the same draw, from the same state.
        GameRandom.RandomSeed = SentinelSeed;

        Assert.Equal(0, FireTome1Ai.Drive(born.Hero));

        var call = Assert.Single(born.Host.SpawnCalls);
        Assert.Equal((born.Hero, false, 23, born.Hero.PosX, born.Hero.PosY, 3 + 0x100000, expectedDirection), call); // the 16 pixels are in Z, the ball is born at the hero's X and Y.
        var ball = Assert.Single(born.Host.Born, b => b.SpriteType == 23);
        Assert.Equal((0x80000, 0x10, 0), (ball.ForceZ, TomeWords.Read274(ball), ball.DelayOrAngleOrEntityId));
        Assert.Null(ball.SpellTarget);
    }

    [Fact]
    public void D2_TheBallIsBornOnlyWhenTheCounterIsAMultipleOf8_TheCounterReadIsTheOneBeforeTheIncrement_ThePhase2AndAboveAreAsTheBinaryDoes()
    {
        foreach (var counter in new[] { 1, 7, 9, 31, 33, 100 })
        {
            var bench = Hero(1, counter, Hold);
            bench.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = 1, Status = EntityStatus.Normal, SpriteType = 20 };
            GameRandom.RandomSeed = SentinelSeed;
            Assert.Equal(0, FireTome1Ai.Drive(bench.Hero));
            Assert.Empty(bench.Host.SpawnCalls);
            Assert.Equal(SentinelSeed, GameRandom.RandomSeed);
        }

        // The column gone (recycled, then taken off the table): the animation 0x34 and the phase 2, the answer 0, nothing drawn; the phase 2 waits for the end of the animation 0x34.
        foreach (var gone in new[] { EntityStatus.Destroyed, EntityStatus.Normal })
        {
            var bench = Hero(1, 8, Hold);
            bench.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = gone == EntityStatus.Destroyed ? 1 : -1, Status = gone, SpriteType = 20 };
            Assert.Equal(0, FireTome1Ai.Drive(bench.Hero));
            Assert.Equal((End, 2), (bench.Hero.TargetAnimationId, bench.Hero.ItemUsePhase));
            Assert.Empty(bench.Host.SpawnCalls);
        }

        // An entity to destroy that is not recycled yet is still the column.
        var dying = Hero(1, 9, Hold);
        dying.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = 1, Status = EntityStatus.FlagToDestroy, SpriteType = 20 };
        Assert.Equal(0, FireTome1Ai.Drive(dying.Hero));
        Assert.Equal((Hold, 1), (dying.Hero.TargetAnimationId, dying.Hero.ItemUsePhase));

        Assert.Equal(0, FireTome1Ai.Drive(Hero(2, 0, End).Hero));
        Assert.Equal(1, FireTome1Ai.Drive(Hero(2, 0, 0).Hero)); // the animation changed: the item is freed by the wrapper.
        foreach (var phase in new[] { 3, 4, 100, int.MaxValue, -1, int.MinValue })
        {
            var bench = Hero(phase, 32, Hold);
            Assert.True(FireTome1Ai.Drive(bench.Hero) == 0, $"phase {phase}");
            Assert.Equal((Tome, phase, 32, Hold), (bench.Hero.ItemInUse, bench.Hero.ItemUsePhase, bench.Hero.ItemUseCounter, bench.Hero.TargetAnimationId));
            Assert.Empty(bench.Host.SpawnCalls);
        }
    }

    [Fact]
    public void C3_TheGuard_InTheDeepWaterOrInAnotherAnimation_CancelsTheSpell_TheColumnIsDestroyedWithItsEffect_TheBallsAreDeactivated_TheItemIsFreed_TheAnswerIsOne()
    {
        foreach (var (phase, animation, slope) in new[] { (0, Hold, 4), (1, Hold, 4), (1, 0x31u, 0), (0, 0x31u, 0) })
        {
            var bench = Hero(phase, 16, animation, slope);
            var made = new List<Made>();
            NativeEffects.CreatedForTests = slot => made.Add(new Made(slot.TargetEffect, slot.TargetMapTable, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
            var column = bench.AddOther(20);
            column.Slot = 1;
            column.Status = EntityStatus.Normal;
            column.PosX = 0x00640000;
            column.PosY = 0x00370000;
            column.PosZ = 5;
            var ball = bench.AddOther(23);
            ball.Slot = 2;
            ball.Status = EntityStatus.Normal;
            ball.BlockedByEntity = bench.Hero.LogicContextEntity;
            bench.Hero.ItemSpawnedEntity = column;
            var keptPointer = bench.Hero.ItemSpawnedEntity;
            GameRandom.RandomSeed = SentinelSeed;

            var answer = FireTome1Ai.Drive(bench.Hero);
            bench.Host.EffectStage!.RunFrame(0, Array.Empty<bool>());

            var label = $"phase {phase}, animation {animation:X}, slope {slope}";
            Assert.True(answer == 1, label);
            Assert.True(bench.Hero.ItemInUse == 0, label); // freed by the general cancellation, after the cancellation of the tome.
            Assert.True(bench.Host.DestroyedAlone.SequenceEqual(new[] { column }), label);
            Assert.Empty(bench.Host.Destroyed);
            Assert.True(made.SequenceEqual(new[] { new Made(19, false, 2, 0x00640000, 0x00370000, 5 + 1) }), label);
            Assert.True((ball.Status, ball.BlockedByEntity is null) == (EntityStatus.Deactivated, true), label);
            Assert.True(bench.Hero.ItemUsePhase == phase && ReferenceEquals(bench.Hero.ItemSpawnedEntity, keptPointer), label); // the pointer and the phase are left as they are.
            Assert.True(bench.Host.SpawnCalls.Count == 0 && SentinelSeed == GameRandom.RandomSeed, label);
        }

        // Another item in use (the earth tome 1, 0x2B): the cancellation of the dispatcher is neutral for the fire entities, the item is freed all the same.
        var other = Hero(1, 16, 0x31, item: 0x2B);
        var bystander = other.AddOther(23);
        bystander.Slot = 2;
        bystander.Status = EntityStatus.Normal;
        other.Hero.ItemSpawnedEntity = new EntityScriptProxy { Slot = 1, Status = EntityStatus.Normal, SpriteType = 20 };
        Assert.Equal(1, FireTome1Ai.Drive(other.Hero));
        Assert.Equal(0u, other.Hero.ItemInUse);
        Assert.Equal(EntityStatus.Normal, bystander.Status);
    }
}
