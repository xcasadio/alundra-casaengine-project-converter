#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.f1a (docs/plan-e20-heros.md section 1.9): the charge of the Square, the hold spark, the release and its burst, WITHOUT the aura (its call is the named stub <see cref="AlundraAuraStub"/> until E20.f1b).
/// The montage is the hero rig of E20.b (<see cref="JumpHeroRig"/> with the sprite clock of the swings) with an effect stage over the bank of map 163 (the real global companion: effects 3 and 16), one tick per
/// frame; after each step the harness runs the stage's frame (<see cref="AlundraEffectStage.RunFrame"/>: the queued operation of the tick is applied, then the reservoir is updated), then does what the counters pass
/// of the world does (the montage has none): <c>FrameCounter++</c>. The probes are the five charge probes of the extended oracle (<c>docs/plan-e20-annexe/e20-f1/oracle/out/</c>: the real code of the binary, the real
/// effect creators over a 128-slot reservoir, the generator word before and after each tick, the fields of each created effect at the end of the logic of the tick).
/// <para>
/// The effects are read through the test seam of the native effects (<see cref="AlundraNativeEffects.CreatedForTests"/>): told each charge effect once created and written, before the update of its tick, which is
/// the oracle's "fields at the end of the logic". The slot indices are not compared (the oracle's aura occupies a slot the DLL does not occupy yet). The generator is posed on the oracle's entry word of each tick
/// (the oracle does not advance it per frame, nor does the DLL), and its word after the tick is compared: the (entry, exit) pairs.
/// </para>
/// <para>
/// THE SOUND ORDER RULE (plan section 1.9): the DLL plays the sound of the hold spark (428, 429) when the stage applies the queued creation, after the sounds played at once during the tick (the sound 43 of the release,
/// the sound of the animation switch); the binary plays it before. At the ticks that carry a spark and other sounds, the expected DLL list is the oracle's with the spark's sound last.
/// </para>
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraHeroChargeTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = AlundraRandom.RandomSeed;

    private const uint Square = AlundraPadState.Square;

    public AlundraHeroChargeTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraNativeEffects.CreatedForTests = null;
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;
        AlundraRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // The montage.
    // -----------------------------------------------------------------------------------------

    private static AlundraItemTables Tables() => AlundraItemTables.GetOrCreate(ProjectRootFinder.Find());

    /// <summary>One charge effect as the montage reads it: after its creation and the writes that follow, before its first update.</summary>
    private readonly record struct Effect(int Id, bool MapTable, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, int Mode, int Index = -1);

    private sealed class Montage
    {
        public required JumpHeroRig Rig { get; init; }

        public required RecordingSoundPlayer Sounds { get; init; }

        public required AlundraEffectStage Stage { get; init; }

        public List<Effect> Created { get; } = new();

        public AlundraEntityScriptProxy Hero => Rig.Hero;

        /// <summary>
        /// One tick: the sounds and the effects read are those of this tick alone. The generator is posed on <paramref name="seed"/> when given (the oracle's entry word of the tick). The frame of the stage runs after the
        /// step, then the counters pass of the world (<c>FrameCounter++</c>, the hit counter cleared).
        /// </summary>
        public void Tick(uint hold, uint? seed = null)
        {
            Sounds.Requests.Clear();
            Created.Clear();
            if (seed is { } word)
            {
                AlundraRandom.RandomSeed = word;
            }

            Rig.Step(hold);
            Stage.RunFrame(1, new[] { false });
            Hero.HitCounter = 0;
            Hero.FrameCounter++;
        }
    }

    /// <summary>The hero montage of E20.b at the oracle's start (pixel (100, 100), on the ground, facing east, counter <c>+0x1C</c> at 0) with the weapon <paramref name="item"/> equipped and an effect stage.</summary>
    private static Montage Start(int item, bool withStage = true, float x = 100f, float y = 100f)
    {
        var rig = JumpHeroRig.Build(x: x, y: y, z: 0f, freePad: true, attackClock: true);
        var sounds = new RecordingSoundPlayer();
        rig.Host.SoundPlayer = sounds;
        var tables = Tables();
        rig.Host.ItemTables = tables;
        AlundraPlayerManager.InitializeNewGameInventory(rig.Host.GameState, tables);
        rig.Host.GameState.NumberOfItems[item * 2 + 1] = 1;
        AlundraPlayerManager.SetPlayerWeaponId(rig.Host.GameState, tables, tables.ItemsProperties[item * 5]);

        var stage = new AlundraEffectStage();
        stage.Load(EffectGolden.BankOf(Root, 163));
        if (withStage)
        {
            rig.Host.EffectStage = stage;
        }

        var hero = rig.Hero;
        hero.TargetDirection = 24;
        hero.AnimationDirection = 3; // right: east.
        hero.CurrentAnimationId = ~hero.TargetAnimationId;
        rig.Step(); // the first switch (the sprite takes the animation 0, right) is resolved: nothing pending at tick 1.
        sounds.Requests.Clear();
        hero.FrameCounter = 0;
        hero.HitCounter = 0;
        hero.WeaponChargeTimer = 0;
        var montage = new Montage { Rig = rig, Sounds = sounds, Stage = stage };
        AlundraNativeEffects.CreatedForTests = slot => montage.Created.Add(
            new Effect(slot.TargetEffect, slot.TargetMapTable, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ, slot.Mode, slot.Index));
        return montage;
    }

    /// <summary>The expected DLL sounds of a row: the oracle's list, the spark's sound (428 or 429) last when the tick carries it and other sounds (the rule in the class summary).</summary>
    private static int[] ExpectedSounds(ChargeRow row)
    {
        var sounds = row.Sounds.ToList();
        if (row.Sparks.Any(c => c.Created) && sounds.Count > 1)
        {
            var spark = sounds.Single(s => s is AlundraNativeEffects.ChargeSparkEvenSfx or AlundraNativeEffects.ChargeSparkOddSfx);
            sounds.Remove(spark);
            sounds.Add(spark);
        }

        return sounds.ToArray();
    }

    /// <summary>
    /// Steps the montage over the rows of <paramref name="rows"/> up to <paramref name="lastTick"/> and returns every difference with the oracle as "tick N field: dll X, oracle Y": the counter, both animations,
    /// the pending pointer and its counter, the sounds (the order rule), the aura stub (alive, the calls of <c>Check</c> so far, creations and releases), the charge effects created on the tick (identifier, X, Y, Z, forces,
    /// in order) and the generator word after the tick.
    /// </summary>
    private static List<string> Compare(Montage m, IReadOnlyList<ChargeRow> rows, int lastTick)
    {
        var diffs = new List<string>();
        var hero = m.Hero;
        var checks = 0;
        var creations = 0;
        var releases = 0;
        long previousAura = 0;
        var spawns = new List<AlundraPlayerManager.WeaponSpawnCall>();
        AlundraPlayerManager.WeaponSpawnCallsForTests = spawns;
        foreach (var row in rows.TakeWhile(r => r.Tick <= lastTick))
        {
            spawns.Clear();
            m.Tick(row.Hold, row.RngBefore);

            void Check(string field, long dll, long oracle)
            {
                if (dll != oracle)
                {
                    diffs.Add($"tick {row.Tick} {field}: dll {dll}, oracle {oracle}");
                }
            }

            Check("anim_target", hero.TargetAnimationId, row.AnimTarget);
            Check("anim_cur", hero.CurrentAnimationId, row.AnimCurrent);
            Check("charge", hero.WeaponChargeTimer, row.Charge);
            Check("rec_ptr", hero.WeaponRecordPointer, row.RecPtr);
            Check("rec_cnt", hero.WeaponStepCounter, row.RecCnt);

            var dllSounds = string.Join(",", m.Sounds.Requests);
            var oracleSounds = string.Join(",", ExpectedSounds(row));
            if (dllSounds != oracleSounds)
            {
                diffs.Add($"tick {row.Tick} sounds: dll [{dllSounds}], oracle (spark last) [{oracleSounds}]");
            }

            // The aura stub: alive exactly while the oracle's handle is non-null; Check at every tick of the charge counter 11 or more; one creation per null handle, one release per non-null one.
            if (row.Charge >= AlundraPlayerManager.AuraChargeTimer)
            {
                checks++;
            }

            if (previousAura == 0 && row.AuraHandle != 0)
            {
                creations++;
            }

            if (previousAura != 0 && row.AuraHandle == 0)
            {
                releases++;
            }

            previousAura = row.AuraHandle;
            Check("aura_alive", hero.AuraStub.Alive ? 1 : 0, row.AuraHandle != 0 ? 1 : 0);
            Check("aura_checks", hero.AuraStub.CheckCallsForTests, checks);
            Check("aura_creations", hero.AuraStub.Creations, creations);
            Check("aura_releases", hero.AuraStub.Releases, releases);

            var expected = row.ChargeEffects.ToList();
            Check("effects_created", m.Created.Count, expected.Count);
            for (var i = 0; i < Math.Min(m.Created.Count, expected.Count); i++)
            {
                var dll = m.Created[i];
                var oracle = expected[i];
                if (dll.Id != oracle.Effect || dll.MapTable || dll.Mode != 0 || (dll.X, dll.Y, dll.Z, dll.ForceX, dll.ForceY, dll.ForceZ) != (oracle.X, oracle.Y, oracle.Z, oracle.ForceX, oracle.ForceY, 0))
                {
                    diffs.Add($"tick {row.Tick} effect {i}: dll {dll}, oracle {oracle}");
                }
            }

            Check("rng_after", (uint)AlundraRandom.RandomSeed, row.RngAfter);

            // The shot stub (E20.e0a and E20.f3 make it real): the same calls at the same ticks, with the oracle's arguments; the oracle's Z is the DLL's plus one raw unit (ADR-0026).
            Check("spawn_calls", spawns.Count, row.Spawns.Count);
            for (var i = 0; i < Math.Min(spawns.Count, row.Spawns.Count); i++)
            {
                var dll = spawns[i];
                var oracle = row.Spawns[i];
                if ((dll.Type, dll.X, dll.Y, dll.Z + 1, dll.Direction) != (oracle.Type, oracle.X, oracle.Y, oracle.Z, oracle.Direction))
                {
                    diffs.Add($"tick {row.Tick} spawn {i}: dll {dll} (Z + 1 = {dll.Z + 1}), oracle {oracle}");
                }
            }
        }

        return diffs;
    }

    private static void AssertEqualToOracle(List<string> diffs)
        => Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the oracle; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(12))}");

    // item, the tick of the release of the Square (the first tick with the pad at 0), the first tick of the aura's creation.
    public static IEnumerable<object[]> Probes() => new[]
    {
        new object[] { "C2-steel", 2, 91, 33 },
        new object[] { "C6-william", 6, 91, 35 },
        new object[] { "C10-crusher", 10, 101, 46 },
        new object[] { "C14-ice", 14, 101, 43 },
        new object[] { "C16-fire", 16, 101, 43 },
    };

    // -----------------------------------------------------------------------------------------
    // T2: the charge.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Probes))]
    public void TheChargeProbes_MatchTheOracleTickByTick_UntilTheTickBeforeTheRelease(string name, int item, int releaseTick, int auraTick)
    {
        var rows = ChargeOracle.Load(name);
        Assert.Equal(0u, rows[releaseTick - 1].Hold); // the release: the first tick with the Square up.
        Assert.Equal(Square, rows[releaseTick - 2].Hold);
        Assert.Equal(auraTick, rows.First(r => r.AuraHandle != 0).Tick);
        using var seed = new SeedScope();
        var m = Start(item);

        AssertEqualToOracle(Compare(m, rows, releaseTick - 1));
    }

    private sealed class SeedScope : IDisposable
    {
        private readonly ulong _seed = AlundraRandom.RandomSeed;

        public void Dispose() => AlundraRandom.RandomSeed = _seed;
    }

    /// <summary>
    /// The values of the plan for C2-steel (the steel sword, item 2), written by hand from the disassembly BEFORE the run and checked against both the oracle rows and the DLL: the counter is 1 at tick 1 (the Square is held,
    /// the animation Idle counts the charge), 0 at tick 2 (the swing 0x3F does not), 1 at tick 23 (the swing is over), 11 at tick 33, 60 at tick 82; the stub's first Check and only creation are at tick 33; the sound 0x2A
    /// every 4 ticks from 33 to 81; the first spark at tick 85 (the generator 12345678 to 24D2C78A, the position and forces below, the sound 428), the second at tick 89 (24D2C78A to AFBE9EAC, the sound 429).
    /// </summary>
    [Fact]
    public void C2Steel_TheWorkedExample_CounterAuraStubSoundsAndSparks()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2);
        var hero = m.Hero;
        var chargeAt = new Dictionary<int, int>();
        var checksAt = new Dictionary<int, int>();
        var soundsAt = new Dictionary<int, int[]>();
        var effectsAt = new Dictionary<int, List<Effect>>();
        var wordsAt = new Dictionary<int, (uint Entry, uint Exit)>();
        foreach (var row in rows.TakeWhile(r => r.Tick <= 90))
        {
            var entry = row.RngBefore;
            m.Tick(row.Hold, entry);
            chargeAt[row.Tick] = hero.WeaponChargeTimer;
            checksAt[row.Tick] = hero.AuraStub.CheckCallsForTests;
            soundsAt[row.Tick] = m.Sounds.Requests.ToArray();
            effectsAt[row.Tick] = m.Created.ToList();
            wordsAt[row.Tick] = (entry, (uint)AlundraRandom.RandomSeed);
        }

        Assert.Equal(1, chargeAt[1]);
        Assert.Equal(0, chargeAt[2]);
        Assert.Equal(1, chargeAt[23]);
        Assert.Equal(11, chargeAt[33]);
        Assert.Equal(60, chargeAt[82]);
        Assert.Equal(60, chargeAt[90]);

        Assert.Equal(0, checksAt[32]);
        Assert.Equal(1, checksAt[33]); // the first Check, and the only creation of the stub, at tick 33.
        Assert.Equal(58, checksAt[90]); // ticks 33 to 90.
        Assert.Equal(1, hero.AuraStub.Creations);
        Assert.Equal(0, hero.AuraStub.Releases);
        Assert.True(hero.AuraStub.Alive);

        var chargeSoundTicks = soundsAt.Where(p => p.Value.Contains(AlundraPlayerManager.ChargeSfx)).Select(p => p.Key).OrderBy(t => t).ToArray();
        Assert.Equal(Enumerable.Range(0, 13).Select(i => 33 + 4 * i).ToArray(), chargeSoundTicks); // 33, 37, ... 81.

        var sparkTicks = effectsAt.Where(p => p.Value.Count > 0).Select(p => p.Key).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { 85, 89 }, sparkTicks);
        Assert.Equal(new[] { 428 }, soundsAt[85]);
        Assert.Equal(new[] { 429 }, soundsAt[89]);
        Assert.Equal((0x12345678u, 0x24D2C78Au), wordsAt[85]);
        Assert.Equal((0x24D2C78Au, 0xAFBE9EACu), wordsAt[89]);
        Assert.Equal(new Effect(16, false, 8274740, 4686300, 1048577, -86057, 93365, 0, 0), effectsAt[85].Single() with { Index = -1 });
        Assert.Equal(new Effect(16, false, 5488860, 7531420, 1048577, 53237, -48891, 0, 0), effectsAt[89].Single() with { Index = -1 });
        Assert.All(wordsAt.Where(p => p.Key is not (85 or 89)), p => Assert.Equal(p.Value.Entry, p.Value.Exit)); // no number is drawn on the other ticks.
    }

    [Fact]
    public void AHostWithoutAStage_StillCountsAndPlaysTheSoundsThatDoNotDependOnACreation_AndCreatesNothing()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2, withStage: false);
        var hero = m.Hero;
        foreach (var row in rows.TakeWhile(r => r.Tick <= 90))
        {
            m.Tick(row.Hold, row.RngBefore);
            Assert.Empty(m.Created);
            Assert.Equal(row.Charge, hero.WeaponChargeTimer);
            Assert.Equal(row.RngBefore, (uint)AlundraRandom.RandomSeed); // the spark of the stage never ran: no number drawn.
        }

        Assert.Equal(58, hero.AuraStub.CheckCallsForTests);
    }

    // -----------------------------------------------------------------------------------------
    // T3: the release.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Probes))]
    public void TheChargeProbes_MatchTheOracleTickByTick_ThroughTheReleaseTheChargedSwingAndTheShot(string name, int item, int releaseTick, int auraTick)
    {
        var rows = ChargeOracle.Load(name);
        Assert.Equal(auraTick, rows.First(r => r.AuraHandle != 0).Tick);
        using var seed = new SeedScope();
        var m = Start(item);

        AssertEqualToOracle(Compare(m, rows, rows.Count));

        // The release tick and the tick after it, from the oracle itself: the charged animation, the pointer and its counter, the aura freed one tick later.
        Assert.Equal(60, rows[releaseTick - 1].Charge);
        Assert.Equal(0, rows[releaseTick].Charge);
        Assert.NotEqual(0L, rows[releaseTick - 1].AuraHandle);
        Assert.Equal(0L, rows[releaseTick].AuraHandle);
        Assert.Equal(1, m.Hero.AuraStub.Creations);
        Assert.Equal(1, m.Hero.AuraStub.Releases);
    }

    // The 16 effects of the burst at the hero (100, 100) = (6553600, 6553600), Z = 1 + 0x100000: (X, Y, ForceX, ForceY), written by hand from the two tables (BurstTableX[2i] and BurstTableY[2i] times 3072 for the
    // position, BurstTableX[(2i + 24) & 31] and BurstTableY[(2i + 24) & 31] times 448 for the forces; docs/plan-e20-annexe/e20-f1/oracle/summary.txt section 'C2-steel release tick').
    private static readonly (int X, int Y, int ForceX, int ForceY)[] Burst =
    {
        (6553600, 8126464, 344064, 0), (5650432, 8006656, 318080, 87808), (4885504, 7665664, 243264, 162176), (4372480, 7155712, 131712, 211904),
        (4194304, 6553600, 0, 229376), (4372480, 5951488, -131712, 211904), (4885504, 5441536, -243264, 162176), (5650432, 5100544, -318080, 87808),
        (6553600, 4980736, -344064, 0), (7456768, 5100544, -318080, -87808), (8221696, 5441536, -243264, -162176), (8734720, 5951488, -131712, -211904),
        (8912896, 6553600, 0, -229376), (8734720, 7155712, 131712, -211904), (8221696, 7665664, 243264, -162176), (7456768, 8006656, 318080, -87808),
    };

    private static void AssertTheBurst(IEnumerable<Effect> effects, int count = 16)
    {
        var list = effects.ToList();
        Assert.Equal(count, list.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(new Effect(16, false, Burst[i].X, Burst[i].Y, 1048577, Burst[i].ForceX, Burst[i].ForceY, 0, 0), list[i] with { Index = -1 });
        }
    }

    /// <summary>
    /// C2-steel (the steel sword) from the plan, written before the run: the Square is released at tick 91 (the first tick of the pad at 0), the counter is 60 (S + D + 60 with S = 1 and D = 21, the swing 0x3F), so the
    /// charged attack starts: animation 0x18 (target and current), the pointer 0x80028C8E (0x80028C79 + 13 + 8) with its counter at 0 (1 at tick 92), the sound 43 and the 16 effects of the burst, no number drawn
    /// (the generator stays AFBE9EAC), no spark on this tick (+0x1C = 90, bit 1 set). The aura stub has been checked 59 times (ticks 33 to 91) and is released at tick 92. The shot of the type 27 is asked at tick 112,
    /// in the binary's Z of 1 (0 in the DLL's), the direction 0x18.
    /// </summary>
    [Fact]
    public void C2Steel_TheRelease_AtTick91_StartsTheChargedSwing_16Effects_Sound43_ThenTheShotAtTick112()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2);
        var hero = m.Hero;
        var spawns = new List<AlundraPlayerManager.WeaponSpawnCall>();
        AlundraPlayerManager.WeaponSpawnCallsForTests = spawns;
        var spawnTicks = new List<(int Tick, AlundraPlayerManager.WeaponSpawnCall Call)>();
        foreach (var row in rows)
        {
            spawns.Clear();
            m.Tick(row.Hold, row.RngBefore);
            spawnTicks.AddRange(spawns.Select(s => (row.Tick, s)));
            if (row.Tick == 90)
            {
                Assert.Equal(0u, hero.TargetAnimationId);
                Assert.Equal(0u, hero.WeaponRecordPointer);
                Assert.Equal(58, hero.AuraStub.CheckCallsForTests);
            }
            else if (row.Tick == 91)
            {
                Assert.Equal(0x18u, hero.TargetAnimationId);
                Assert.Equal(0x18u, hero.CurrentAnimationId);
                Assert.Equal(0x80028C8Eu, hero.WeaponRecordPointer);
                Assert.Equal(0, hero.WeaponStepCounter);
                Assert.Equal(60, hero.WeaponChargeTimer);
                Assert.Equal(new[] { 43 }, m.Sounds.Requests.ToArray());
                AssertTheBurst(m.Created);
                Assert.Equal(0xAFBE9EACu, (uint)AlundraRandom.RandomSeed);
                Assert.Equal(59, hero.AuraStub.CheckCallsForTests);
                Assert.True(hero.AuraStub.Alive);
            }
            else if (row.Tick == 92)
            {
                Assert.Equal(1, hero.WeaponStepCounter);
                Assert.Equal(0, hero.WeaponChargeTimer);
                Assert.Equal(1, hero.AuraStub.Releases);
                Assert.False(hero.AuraStub.Alive);
                Assert.Empty(m.Created);
            }
        }

        // The shot: one call in the whole probe, at tick 112, with the arguments of the binary (the oracle's Z of 1 is the DLL's 0).
        var shot = Assert.Single(spawnTicks);
        Assert.Equal(112, shot.Tick);
        Assert.Equal(new AlundraPlayerManager.WeaponSpawnCall(27, 6553600, 6553600, 0, 0x18), shot.Call);
    }

    /// <summary>C10-crusher at tick 101: the hold spark and then the burst on the release tick: 17 creations in the oracle's order; sounds 43, 36 (the animation set of 0x19), then the spark's 428 (the order rule).</summary>
    [Fact]
    public void C10Crusher_TheReleaseTick101_ASparkThenTheBurst_17Creations_SoundsFollowTheRule()
    {
        var rows = ChargeOracle.Load("C10-crusher");
        using var seed = new SeedScope();
        var m = Start(10);
        foreach (var row in rows.TakeWhile(r => r.Tick <= 100))
        {
            m.Tick(row.Hold, row.RngBefore);
        }

        var entry = rows[100].RngBefore;
        Assert.Equal(0x24D2C78Au, entry);
        m.Tick(rows[100].Hold, entry);

        Assert.Equal(17, m.Created.Count);
        Assert.Equal(new Effect(16, false, 5488860, 7531420, 1048577, 53237, -48891, 0, 0), m.Created[0] with { Index = -1 }); // the spark.
        AssertTheBurst(m.Created.Skip(1));
        Assert.Equal(0x19u, m.Hero.TargetAnimationId);
        Assert.Equal(0x80028CDCu, m.Hero.WeaponRecordPointer);
        Assert.Equal(new[] { 43, 36, 428 }, m.Sounds.Requests.ToArray()); // the oracle's order is 428, 43, 36; the spark's sound comes last in the DLL.
        Assert.Equal(0xAFBE9EACu, (uint)AlundraRandom.RandomSeed);
        Assert.Equal(new[] { 428, 43, 36 }, rows[100].Sounds.ToArray());
    }

    /// <summary>The release at S + D + 59 is lost, at S + D + 60 it succeeds (S = 1, D = 21 for the swing 0x3F: the counter is 59 at tick 81 and 60 at tick 82).</summary>
    [Theory]
    [InlineData(81, false)]
    [InlineData(82, true)]
    public void TheRelease_FailsWithACounterOf59_AndSucceedsWithACounterOf60(int releaseTick, bool succeeds)
    {
        using var seed = new SeedScope();
        AlundraRandom.RandomSeed = 0x12345678;
        var m = Start(2);
        var hero = m.Hero;
        for (var tick = 1; tick < releaseTick; tick++)
        {
            m.Tick(Square);
        }

        m.Tick(0); // the first tick of the pad at 0: the release edge.

        Assert.Equal(succeeds ? 60 : 59, hero.WeaponChargeTimer);
        Assert.Equal(succeeds, hero.TargetAnimationId == 0x18);
        Assert.Equal(succeeds ? 0x80028C8Eu : 0u, hero.WeaponRecordPointer);
        Assert.Equal(succeeds, m.Sounds.Requests.Contains(43));
        Assert.Equal(succeeds ? 16 : 0, m.Created.Count);
        m.Tick(0);
        Assert.Equal(0, hero.WeaponChargeTimer); // the next tick: no charge any more, in both cases.
        Assert.False(hero.AuraStub.Alive);
    }

    /// <summary>A release in the air is lost (D-E20-9): the counter is 60 and the Square is released, but the hero is not on the ground: no burst, no 0x2B, no pointer, no charged animation; the control on the ground succeeds.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void AReleaseInTheAir_IsLost_OnTheGroundItSucceeds(int onGround, bool succeeds)
    {
        var m = Start(2);
        var hero = m.Hero;
        hero.WeaponChargeTimer = 60;
        hero.FrameCounter = 1; // no hold spark on this tick (+0x1C & 3 is not 0).
        hero.TargetAnimationId = 0;
        hero.CurrentAnimationId = 0;
        hero.IsOnGround = onGround;
        m.Sounds.Requests.Clear();
        m.Created.Clear();
        var pad = new AlundraPadState { ButtonsReleased = Square };

        AlundraPlayerManager.MovePlayer(hero, in pad, m.Rig.Host.GameState, m.Rig.Host);
        m.Stage.RunFrame(1, new[] { false });

        Assert.Equal(succeeds ? 0x18u : AirStillAnimation, hero.TargetAnimationId);
        Assert.Equal(succeeds ? 0x80028C8Eu : 0u, hero.WeaponRecordPointer);
        Assert.Equal(succeeds, m.Sounds.Requests.Contains(43));
        Assert.Equal(succeeds ? 16 : 0, m.Created.Count);
    }

    private const uint AirStillAnimation = 0x2D;

    /// <summary>A full reservoir at the tick of the first spark (C2-steel, tick 85): the effect is not created, so there is no sound 428 and no number drawn; the next spark (tick 89), once room is made, starts from the unchanged word.</summary>
    [Fact]
    public void AFullReservoir_AtTheSparkTick_PlaysNoSoundAndDrawsNothing_TheNextSparkStartsFromTheUnchangedWord()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2);
        foreach (var row in rows.TakeWhile(r => r.Tick <= 84))
        {
            m.Tick(row.Hold, row.RngBefore);
        }

        var fillers = Fill(m, AlundraEffectReservoir.SlotCount);
        Assert.Equal(AlundraEffectReservoir.SlotCount, m.Stage.Reservoir.ActiveCount);
        AlundraRandom.RandomSeed = 0x12345678;
        m.Tick(rows[84].Hold); // tick 85
        Assert.Empty(m.Created);
        Assert.Empty(m.Sounds.Requests);
        Assert.Equal(0x12345678u, (uint)AlundraRandom.RandomSeed);

        for (var tick = 86; tick <= 88; tick++)
        {
            m.Tick(rows[tick - 1].Hold);
        }

        foreach (var slot in fillers)
        {
            m.Stage.Reservoir.Free(slot);
        }

        m.Tick(rows[88].Hold); // tick 89: the spark finds room and starts from the word the full reservoir left.
        var effect = Assert.Single(m.Created);
        Assert.Equal(new Effect(16, false, 8274740, 4686300, 1048577, -86057, 93365, 0, 0, 0), effect); // the tuple of the tick 85 of the oracle (the same entry word), in the first slot.
        Assert.Equal(new[] { 429 }, m.Sounds.Requests.ToArray()); // +0x1C is 88 here: the sound of the cadence of 8.
        Assert.Equal(0x24D2C78Au, (uint)AlundraRandom.RandomSeed);
    }

    /// <summary>Five free slots at the release (C2-steel, tick 91): the first five effects of the burst are created, in the slots 123 to 127 like the oracle's P2, the 11 others are skipped without shifting those; the sound 43, the pointer and 0x18 are there.</summary>
    [Fact]
    public void FiveFreeSlots_AtTheRelease_CreateTheFirstFiveEffectsOfTheBurst_AndTheReleaseStillCompletes()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2);
        foreach (var row in rows.TakeWhile(r => r.Tick <= 90))
        {
            m.Tick(row.Hold, row.RngBefore);
        }

        Fill(m, AlundraEffectReservoir.SlotCount - 5 - m.Stage.Reservoir.ActiveCount);
        Assert.Equal(AlundraEffectReservoir.SlotCount - 5, m.Stage.Reservoir.ActiveCount);
        Assert.Equal(new[] { 123, 124, 125, 126, 127 }, m.Stage.Reservoir.Slots.Where(s => !s.Active).Select(s => s.Index).ToArray());
        m.Tick(rows[90].Hold, rows[90].RngBefore);

        Assert.Equal(5, m.Created.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(new Effect(16, false, Burst[i].X, Burst[i].Y, 1048577, Burst[i].ForceX, Burst[i].ForceY, 0, 0, 123 + i), m.Created[i]);
        }

        Assert.Equal(new[] { 43 }, m.Sounds.Requests.ToArray());
        Assert.Equal(0x80028C8Eu, m.Hero.WeaponRecordPointer);
        Assert.Equal(0x18u, m.Hero.TargetAnimationId);
        Assert.Equal(0xAFBE9EACu, (uint)AlundraRandom.RandomSeed);
    }

    /// <summary>Fills the first free slots of the reservoir with effects that never end (the aura's looping animation 1 of the global effect 3); returns them.</summary>
    private static List<AlundraEffectSlot> Fill(Montage m, int count)
    {
        var slots = new List<AlundraEffectSlot>();
        for (var i = 0; i < count; i++)
        {
            slots.Add(m.Stage.Reservoir.Create(false, 3, 1, 0, 0, 0)!);
        }

        return slots;
    }
}
