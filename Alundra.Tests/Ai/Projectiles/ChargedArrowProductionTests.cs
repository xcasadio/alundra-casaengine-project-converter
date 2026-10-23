#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Projectiles;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f4 T2 (docs/plan-e20-heros.md section 1.20.1, "Tests de production"): the charge of William through the PRODUCTION path (no birth hook, no test set of wired types: <see cref="PlayerManager.ProductionWiredWeaponTypes"/> alone decides) in the real world of map 14 on the
/// synthetic field of the scenario (<see cref="ChargedArrowRun"/> with <c>production: true</c>), against the keys of the oracle read by key (<see cref="ChargedArrowOracle"/>). They are RED before the line 7 of the production set (T3) and green after it.
/// (p1) the arrow is born at S, daughter of the hero, at the lowest free slot measured before the shot, and its life equals the key; in the same assertion the shot of the start of the hold (type 4) is born if and only if the production set holds 4 (derived, not copied);
/// (p2) a full table of slots: no entity and the pointer cleared; exactly one free slot: one entity; (p3) the entity the target touches is the arrow, at the tick of the touch of the key. The pure part of the damage (c10) is green by construction (as the (p7a) of E20.f3c).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChargedArrowProductionTests : IDisposable
{
    private const int Birth = ChargedArrowOracle.Birth;
    private const int Arrow = 7;
    private const int TrailEffect = 25;
    private const int BreakEffect = 8;
    private const int StartHoldType = 4;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public ChargedArrowProductionTests() => Reset();

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
    }

    private static void Reset()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        PlayerManager.WeaponSpawnCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        ChargedArrowAi.StubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The column 15 is a wall (the walk flag <c>0x40</c>) on every row: the arrow dies against it.</summary>
    private static CutBench.Cell WallColumn15(int column, int row) => column == 15 ? new CutBench.Cell(0x40, 0) : default;

    private static string FxLine((int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz) e) => $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})";

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p1) the charge of William births the arrow of the type 7, daughter of the hero, at the lowest free slot, and its life is the key's; the shot of the start of the hold follows the production set.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P1_TheChargeOfWilliamBirthsTheArrowAtS_DaughterOfTheHeroAtTheLowestFreeSlot_ItsLifeEqualsTheKey_TheShotOfTheStartOfTheHoldIsBornIfAndOnlyIfTheProductionSetHolds4()
    {
        var key = ChargedArrowOracle.Load("east_wall", stepByImage: true);
        using var run = new ChargedArrowRun("p1", WallColumn15, production: true);

        run.Go(1, 170);

        // The shot of the start of the hold (type 4) is asked once and is born if and only if the production set holds 4 (derived from the set, not copied), in the same assertion as the arrow of the type 7.
        Assert.Contains(run.SpawnCalls, c => c.Type == StartHoldType);
        Assert.Equal(
            (PlayerManager.ProductionWiredWeaponTypes.Contains(StartHoldType), PlayerManager.ProductionWiredWeaponTypes.Contains(Arrow)),
            (run.TypesFatheredByTheHero.Contains(StartHoldType), run.Born is not null));

        // The arrow of the type 7: asked at the release, born at S, daughter of the hero, at the lowest free slot measured before the shot.
        Assert.Contains(run.SpawnCalls, c => c.Type == Arrow);
        Assert.NotNull(run.Born);
        Assert.Equal(Birth, run.BirthTick);
        Assert.Equal(Arrow, run.Born!.SpriteType);
        Assert.Same(run.Arena.Arc.HeroEntity, run.Born.ParentEntity);
        Assert.Equal(PlayerManager.ProductionWiredWeaponTypes.Contains(StartHoldType) ? 2 : 1, run.LowestFreeSlotBeforeBirth); // E20.f5: the arrow of the start of the hold (type 4) holds the slot 1 when it is born, so the charged arrow takes the next free one (derived from the production set).
        Assert.Equal(run.LowestFreeSlotBeforeBirth, run.BirthSlot);

        // The line of the arrow at every tick of S to S+44: the status 2 to S+42, the wall flag at S+42, the status 4 at S+43, absent at S+44.
        var expected = Enumerable.Range(0, 45).Select(k => $"{k,3}: {key.Line(Birth + k)}").ToList();
        var actual = Enumerable.Range(0, 45).Select(k => $"{k,3}: {run.Lines[run.BirthTick + k]}").ToList();
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));

        // The trail of the key (ten effects 25 at S+4 ... S+40) and the break effect (8 attached to the arrow at S+43, its anchor where it died); no draw during the whole life.
        var trails = key.Effects(TrailEffect);
        Assert.Equal(10, trails.Count);
        Assert.Equal(trails.Select(e => e.Line(e.Tick - Birth + run.BirthTick)).ToList(), run.Effects.Where(e => e.Id == TrailEffect).Select(e => FxLine(e)).ToList());
        var attached = Assert.Single(key.Attached(BreakEffect));
        Assert.Equal(attached.Tick - Birth + run.BirthTick, run.BreakSeenTick);
        Assert.Equal((349, 100), run.BreakAnchor);
        Assert.All(Enumerable.Range(0, 45), k => Assert.Equal(1, run.StepsDuring[run.BirthTick + k])); // only the step of the end of the frame.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p2) the table of slots: full, the shot is lost (the pointer is cleared all the same); one free slot, one arrow.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P2_WithNoFreeSlotTheShotOfTheArrowCreatesNothingAndClearsThePointer_WithExactlyOneFreeSlotItCreatesOneArrow()
    {
        (int Held, bool Asked, bool Born, uint Pointer) full;
        using (var run = new ChargedArrowRun("p2-full", WallColumn15, production: true, freeSlotsAtBirth: 0))
        {
            run.Go(1, 150);
            full = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == Arrow), run.Born is not null, run.PointerAfter[Birth]);
        }

        (int Held, bool Asked, bool Born, uint Pointer, int Tick, int Type) one;
        using (var run = new ChargedArrowRun("p2-one", WallColumn15, production: true, freeSlotsAtBirth: 1))
        {
            run.Go(1, 150);
            one = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == Arrow), run.Born is not null, run.PointerAfter[Birth], run.BirthTick, run.Born?.SpriteType ?? -1);
        }

        Assert.Equal(
            ((EntitySlotTable.SlotCount, true, false, 0u), (EntitySlotTable.SlotCount - 1, true, true, 0u, Birth, Arrow)),
            ((full.Held, full.Asked, full.Born, full.Pointer), (one.Held, one.Asked, one.Born, one.Pointer, one.Tick, one.Type)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p3) the entity the target touches is the arrow of production, at the tick of the touch of the key (S+29); the same arrow touches nothing without the target.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P3_TheTouchingEntityOfTheTarget_IsTheArrowOfProduction_AtTheTickOfTheTouchOfTheKey_TheArrowAloneTouchesNothing()
    {
        var key = ChargedArrowOracle.Load("vs_target", stepByImage: true);
        int? firstTouch;
        using (var run = new ChargedArrowRun("p3-target", WallColumn15, targets: new List<int[]> { new[] { 300, 104 } }, production: true))
        {
            run.Go(1, 150);
            Assert.NotNull(run.Born);
            Assert.Equal(Birth, run.BirthTick);
            firstTouch = Enumerable.Range(0, 45).Cast<int?>().FirstOrDefault(k => run.TargetTouchedByTheArrow[run.BirthTick + k!.Value]);
            Assert.Equal(1, key.Arrow(Birth + 29)!.Value.GetProperty("hitc").GetInt32()); // the key: the arrow hits once, at S+29.
        }

        using var alone = new ChargedArrowRun("p3-alone", WallColumn15, production: true);
        alone.Go(1, 150);
        Assert.NotNull(alone.Born);

        Assert.Equal((29, false), (firstTouch, Enumerable.Range(0, 45).Any(k => alone.TargetTouchedByTheArrow[alone.BirthTick + k])));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c10) [V] green by construction (as the (p7a) of E20.f3c): the damage of the charged arrow on a Beannoid by the pure function: attribute 131 (0x83), power 5 (the equipment term needs the hero's inventory: not given here).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C10_TheArrowAttack131Power5_OnTheBeannoidSheet_DealsThePowerTimesTheResponseOver16_AtLeastOne_AndALifeBelowTheDamageStopsAtZero()
    {
        var balance = BalanceTables.GetOrCreate(ProjectRootFinder.Find());
        var beannoid = balance.Select(335, 0)!;
        var response = beannoid.ByteAt(2 + (0x83 & 0xF));
        var damage = Math.Max(1, (5 * response) >> 4);

        Assert.Equal(0, response & 0xC0); // a plain multiplier (not a kill, not a block): the formula above is the one that applies.
        Assert.Equal((100 - damage, 0), (Damage.ResolveBalanceTarget(new BalanceAttack(0x83, 5), beannoid, 100, null, 0), Damage.ResolveBalanceTarget(new BalanceAttack(0x83, 5), beannoid, damage - 1, null, 0)));
    }
}
