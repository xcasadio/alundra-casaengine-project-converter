#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Ai.Projectiles;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;

namespace Alundra.Tests;

/// <summary>
/// E20.f3c phase B (docs/plan-e20-heros.md section 1.19.3, T3): the sword wave born by the PRODUCTION seam (no hook, no test set), in the real world of map 14 (<see cref="HitArena"/>, the native handlers off as the arena
/// keeps them; <see cref="ArcRun"/> for the shots). Labels: [R] red before the wiring AND under the removal (the production set emptied), [V] green by construction. Every [R] test here is built on
/// <c>WiredWeaponTypes =&gt; WiredWeaponTypesForTests ?? ProductionWiredWeaponTypes</c>: with the production set empty the shot of a sword creates nothing, so each of them fails on a missing entity.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraWaveSwordWiringTests : IDisposable
{
    private const int SteelItem = 2;
    private const int DemonItem = 3;
    private const int HolyItem = 4;
    private const int Arrow = 3;
    private const uint East = 24;
    private const int BirthTick = WaveOracle.Birth;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = AlundraRandom.RandomSeed;
    private readonly ITestOutputHelper _output;

    public AlundraWaveSwordWiringTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose() => Reset();

    private void Reset()
    {
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
        AlundraPlayerManager.WiredWeaponTypesForTests = null;
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;
        AlundraWaveSwordAi.StubCallsForTests = null;
        AlundraNativeEffects.CreatedForTests = null;
        AlundraRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static ArcRun Arc(string name) => new(new ArcSpec("E20f3c-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (p1) the charge arena (release at 91, S = 112) with the PRODUCTION seam: a sprite 27 (28, 29) daughter of the hero, at the lowest free slot measured before the shot, equal to the keys of the oracle at every tick.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("steel", "east", SteelItem, 27, East)]
    [InlineData("steel", "north", SteelItem, 27, 16u)]
    [InlineData("demon", "east", DemonItem, 28, East)]
    [InlineData("holy", "east", HolyItem, 29, East)]
    public void P1_R_TheChargeOfTheSword_BirthsTheWaveOfItsType_DaughterOfTheHero_AtTheLowestFreeSlot_AndItsLifeEqualsTheKeys(string sword, string direction, int item, int type, uint word)
    {
        var oracle = WaveOracle.Rows(sword, direction);
        using var run = new WaveRun($"p1-{sword}-{direction}", item, word);
        var slotDuringTheLife = -2; // read while the wave is alive: once destroyed its slot is given back (Slot = -1).

        run.Go(1, useHook: false, afterTick: t =>
        {
            if (t == BirthTick && run.Born is not null)
            {
                slotDuringTheLife = run.Born.Slot;
            }
        });

        var born = run.Born;
        Assert.NotNull(born);
        var expected = Enumerable.Range(WaveOracle.FirstTick, WaveOracle.LastTick - WaveOracle.FirstTick + 1).Select(t => $"{t - WaveOracle.Birth,4}: {WaveOracle.Describe(oracle[t])}");
        var actual = Enumerable.Range(WaveOracle.FirstTick, WaveOracle.LastTick - WaveOracle.FirstTick + 1).Select(t => $"{t - WaveOracle.Birth,4}: {run.Lines[t]}");
        Assert.Equal(
            (type, true, run.LowestFreeSlotBeforeBirth, BirthTick, string.Join("\n", expected)),
            (born!.SpriteType, ReferenceEquals(born.ParentEntity, run.Arena.Arc.HeroEntity), slotDuringTheLife, run.FirstTick, string.Join("\n", actual)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The reach sweep: one Beannoid east of the hero at the relative X of the key (oracle rel y -4 = the arena's y 92 for the hero at y 96). What the arena reports is the first touch at or after tick 100 (the charge)
    // and who touched: the wave (the entity daughter of the hero, sprite 27 to 29) or the hero.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed record ReachResult(int? Tick, string? Attacker, bool HeroEverHit, bool WaveTouchedTheHero, bool TargetTouchingIsTheWave, int? WaveAttackAttribute, int? WaveAttackPower, WaveRun Run);

    private ReachResult Reach(int relX, string name)
    {
        var run = new WaveRun("reach-" + name + relX, SteelItem, East, new List<int[]> { new[] { 212 + relX, 92 } });
        int? hitTick = null;
        string? attacker = null;
        var heroEverHit = false;
        var waveTouchedTheHero = false;
        var touchingIsTheWave = false;
        int? attr = null;
        int? power = null;
        run.Go(1, useHook: false, afterTick: tick =>
        {
            var sample = run.Arena.Samples[^1];
            heroEverHit |= sample.Hero.CooldownTicks != 0;
            if (run.Born is { } wave && run.Arena.Hero.TouchingEntity is { } heroTouching && ReferenceEquals(heroTouching, wave.SelfEntity))
            {
                waveTouchedTheHero = true;
            }

            if (hitTick is null && tick >= 100 && sample.Targets[0].CooldownTicks == 25)
            {
                var target = run.Arena.Targets[0];
                hitTick = tick;
                if (run.Born is { } w && ReferenceEquals(target.TouchingEntity, w.SelfEntity))
                {
                    attacker = "wave";
                    touchingIsTheWave = true;
                    attr = w.CurrentAttack?.Attribute;
                    power = w.CurrentAttack?.Power;
                }
                else if (ReferenceEquals(target.TouchingEntity, run.Arena.Hero.SelfEntity))
                {
                    attacker = "hero";
                }
                else
                {
                    attacker = "other";
                }
            }
        });
        _output.WriteLine($"rel {relX}: tick {hitTick} by {attacker}");
        return new ReachResult(hitTick, attacker, heroEverHit, waveTouchedTheHero, touchingIsTheWave, attr, power, run);
    }

    // [R] (p2) the targets at +56, +68, +80 are touched by the wave at S+5, S+7, S+9 (key wave_reach_sweep); the target at +44 by the hero (the wave does not pass in front of the hero): the positive and the negative cases in the SAME assertion,
    // so that the case of the hero alone (green without the wave) cannot make the test pass.
    [Fact]
    public void P2_R_TheWaveTouchesTheTargetsBeyondTheHeroReach_AtSPlus5_7_9_AndTheHeroTheOneInFrontOfHim()
    {
        var expected = new List<string>();
        var actual = new List<string>();
        foreach (var relX in new[] { 44, 56, 68, 80 })
        {
            var key = WaveOracle.ReachFirstHit(relX);
            Assert.NotNull(key);
            expected.Add($"{relX}: {key!.Value.Tick} {key.Value.Attacker}");
            var reach = Reach(relX, "p2-");
            using (reach.Run)
            {
                actual.Add($"{relX}: {(reach.Tick is { } t ? t.ToString() : "none")} {reach.Attacker ?? "none"}");
            }
        }

        Assert.Equal(string.Join("; ", expected), string.Join("; ", actual));
    }

    // [R] (p3) the wave never touches the hero: his counter of hit is never set and his TouchingEntity is never the wave, in the same run where the target is touched by the wave.
    [Fact]
    public void P3_R_TheWaveNeverTouchesTheHero_InTheSameRunWhereTheTargetIsTouchedByTheWave()
    {
        var reach = Reach(68, "p3-");
        using var _ = reach.Run;

        Assert.Equal((BirthTick + 7, "wave", false, false), (reach.Tick, reach.Attacker, reach.HeroEverHit, reach.WaveTouchedTheHero));
    }

    // [R] (p7b) in the world, the TouchingEntity of the target is the production wave at the tick of the touch (S+9 for the target at +80, which only the wave reaches), with its attack 129 and the power of the steel sword; the damage end to end stays for the recette.
    [Fact]
    public void P7b_R_TheTouchingEntityOfTheTarget_IsTheWaveOfProduction_AtTheTickOfTheTouch_WithItsAttack()
    {
        var reach = Reach(80, "p7b-");
        using var _ = reach.Run;

        Assert.Equal((BirthTick + 9, "wave", true, 129, 10), (reach.Tick, reach.Attacker, reach.TargetTouchingIsTheWave, reach.WaveAttackAttribute, reach.WaveAttackPower));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (p4) in the same assertion: the wave of production exists in the charge arena and dies at S+26 (E10 in line), AND the pin of the arena stays at 2 contacts (key arena_probe, run "control"): the mutant "E10 in the
    // registry" gives 3 at tick 138.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P4_R_TheWaveOfProductionExistsInTheChargeArena_DiesAtSPlus26_AndThePinOfTheArenaStaysAtTwoContacts()
    {
        using var run = new WaveRun("p4", SteelItem, East, new List<int[]> { new[] { 212 + 40, 96 } });
        var contacts = new List<(int Tick, string Attacker)>();
        var status = new Dictionary<int, string>();
        run.Go(1, useHook: false, lastTick: 140, afterTick: tick =>
        {
            if (run.Arena.Samples[^1].Targets[0].CooldownTicks == 25)
            {
                var target = run.Arena.Targets[0];
                contacts.Add((tick, run.Born is { } w && ReferenceEquals(target.TouchingEntity, w.SelfEntity) ? "wave" : ReferenceEquals(target.TouchingEntity, run.Arena.Hero.SelfEntity) ? "hero" : "other"));
            }

            if (tick is >= BirthTick - 1 and <= BirthTick + 27)
            {
                status[tick] = run.Born is null ? "none" : run.Arena.Arc.Entities.Contains(run.Born) ? ((int)run.Born.Status).ToString() : "free";
            }
        });

        // The key lists the contacts by oracle tick minus 5; the first is the press swing, which both the oracle and the arena play from the tick 1 (the same tick, 5, in both): it is not shifted, the contact of the charge is.
        var key = WaveOracle.ArenaContacts("control").Select((k, i) => i == 0 ? (k.Tick + WaveOracle.ArenaOffset, k.Attacker) : k).ToList();
        _output.WriteLine("contacts: " + string.Join(" ", contacts) + " | key: " + string.Join(" ", key));
        Assert.Equal(
            ("none", "2", "4", "free", string.Join(" ", key.Select(k => k.ToString()))),
            (status[BirthTick - 1], status[BirthTick], status[BirthTick + 26], status[BirthTick + 27], string.Join(" ", contacts.Select(c => c.ToString()))));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The shots of the ArcRun: the pointer armed so that the next tick reaches the spawn.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ArmBow(AlundraEntityScriptProxy hero)
    {
        hero.WeaponRecordPointer = AlundraWeaponTables.AnimationAddress(8, air: false);
        hero.WeaponStepCounter = AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 2);
        hero.TargetAnimationId = 0x13;
    }

    /// <summary>The release of a charge of the steel sword (record 1): the pointer of the charged animation 0x18 (type 27, delay 20), the counter at the delay so that the next tick shoots.</summary>
    private static void ArmSteelSword(AlundraEntityScriptProxy hero)
    {
        hero.WeaponRecordPointer = AlundraWeaponTables.ChargeAnimationAddress(1);
        Assert.Equal(27, AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 1));
        hero.WeaponStepCounter = AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 2);
        hero.TargetAnimationId = 0x18;
    }

    // [R] (p5) the negative witness, with an EXPLICIT test set {27, 28, 29} (it stays true when f5 wires the bow): a shot of Hunter's Bow gives counter 1, one call of the journal and ZERO entity; next to it, in the same assertion,
    // a shot of the steel sword with the PRODUCTION set (test set back to null): one entity.
    [Fact]
    public void P5_R_TheBowShotCreatesNothingUnderTheSetOfTheSwords_TheSwordShotWithTheProductionSetCreatesOneEntity()
    {
        int bowCounter;
        int bowJournal;
        int bowBorn;
        using (var arc = Arc("p5-bow"))
        {
            AlundraPlayerManager.WiredWeaponTypesForTests = new HashSet<int> { 27, 28, 29 };
            arc.OneFrame();
            var before = arc.Entities.ToList();
            var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
            var journal = new List<AlundraPlayerManager.WeaponSpawnCall>();
            AlundraPlayerManager.WeaponSpawnCallsForTests = journal;
            ArmBow(arc.Hero);
            arc.OneFrame();
            AlundraPlayerManager.WeaponSpawnCallsForTests = null;
            bowCounter = AlundraPlayerManager.WeaponSpawnCallCountForTests - counter;
            bowJournal = journal.Count(c => c.Type == Arrow);
            bowBorn = arc.Entities.Count(e => !before.Contains(e));
        }

        AlundraPlayerManager.WiredWeaponTypesForTests = null;
        int swordCounter;
        int swordBornType;
        int swordBorn;
        using (var arc = Arc("p5-sword"))
        {
            arc.OneFrame();
            var before = arc.Entities.ToList();
            var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
            ArmSteelSword(arc.Hero);
            arc.OneFrame();
            swordCounter = AlundraPlayerManager.WeaponSpawnCallCountForTests - counter;
            var born = arc.Entities.Where(e => !before.Contains(e)).ToList();
            swordBorn = born.Count;
            swordBornType = born.Count == 1 ? born[0].SpriteType : -1;
        }

        Assert.Equal((1, 1, 0, 1, 1, 27), (bowCounter, bowJournal, bowBorn, swordCounter, swordBorn, swordBornType));
    }

    private static void FillSlots(ArcRun arc, int free)
    {
        while (AlundraEntitySlotTable.SlotCount - arc.Proxy.SlotTable.HeldCount > free)
        {
            Assert.NotNull(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 100 << 16, 100 << 16, 0, 0x10));
        }
    }

    // [R] (p6) the full table by its STATE (no free slot: HeldCount == SlotCount before the shot): the shot of the wave creates nothing and the pointer is cleared; with exactly one free slot (FillSlots(free: 1)): one entity.
    [Fact]
    public void P6_R_WithNoFreeSlotTheShotOfTheWaveCreatesNothingAndClearsThePointer_WithExactlyOneFreeSlotItCreatesOneEntity()
    {
        (int HeldBefore, int Counter, int Born, uint Pointer) full;
        using (var arc = Arc("p6-full"))
        {
            arc.OneFrame();
            FillSlots(arc, free: 0);
            var held = arc.Proxy.SlotTable.HeldCount;
            var before = arc.Entities.ToList();
            var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
            ArmSteelSword(arc.Hero);
            arc.OneFrame();
            full = (held, AlundraPlayerManager.WeaponSpawnCallCountForTests - counter, arc.Entities.Count(e => !before.Contains(e)), arc.Hero.WeaponRecordPointer);
        }

        (int HeldBefore, int Counter, int Born, uint Pointer, int Type) one;
        using (var arc = Arc("p6-one"))
        {
            arc.OneFrame();
            FillSlots(arc, free: 1);
            var held = arc.Proxy.SlotTable.HeldCount;
            var before = arc.Entities.ToList();
            var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
            ArmSteelSword(arc.Hero);
            arc.OneFrame();
            var born = arc.Entities.Where(e => !before.Contains(e)).ToList();
            one = (held, AlundraPlayerManager.WeaponSpawnCallCountForTests - counter, born.Count, arc.Hero.WeaponRecordPointer, born.Count == 1 ? born[0].SpriteType : -1);
        }

        Assert.Equal(
            ((AlundraEntitySlotTable.SlotCount, 1, 0, 0u), (AlundraEntitySlotTable.SlotCount - 1, 1, 1, 0u, 27)),
            ((full.HeldBefore, full.Counter, full.Born, full.Pointer), (one.HeldBefore, one.Counter, one.Born, one.Pointer, one.Type)));
    }
}
