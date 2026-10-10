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
    private readonly record struct Effect(int Id, bool MapTable, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, int Mode);

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
            new Effect(slot.TargetEffect, slot.TargetMapTable, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ, slot.Mode));
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
        foreach (var row in rows.TakeWhile(r => r.Tick <= lastTick))
        {
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
        Assert.Equal(new Effect(16, false, 8274740, 4686300, 1048577, -86057, 93365, 0, 0), effectsAt[85].Single());
        Assert.Equal(new Effect(16, false, 5488860, 7531420, 1048577, 53237, -48891, 0, 0), effectsAt[89].Single());
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
}
