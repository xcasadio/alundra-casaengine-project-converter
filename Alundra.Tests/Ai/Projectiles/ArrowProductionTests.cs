#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f5 T2 (docs/plan-e20-heros.md section 1.21.1, "Tests de production", and the "Revision au HEAD" bullets "Le banc de (p1) et (p2)" and "(p3) et la preuve de retrait"): the normal arrows (types 3 and 4) through the PRODUCTION path (no birth hook, no test set of wired types:
/// <see cref="PlayerManager.ProductionWiredWeaponTypes"/> alone decides) in the real world of map 14 on the synthetic field of the scenario (<see cref="ArrowRun"/> with <c>production: true</c>), against the keys of the oracle read by key (<see cref="ArrowOracle"/>).
/// They are RED before the set {3, 4} of the production set (T3) and green after it.
/// (p1) the arrow of the hunter (type 3) and the normal arrow of William (type 4) are born at S = P + 9 = 10, daughters of the hero, at the lowest free slot measured before the shot, and their life (status, position, forces, wall flag, bounce, end, absent at S+61) equals the key of their type;
/// (p2) a full table of slots: no entity; exactly one free slot: one arrow. The negative witnesses of (p3) (the empty set of the test) are the rewrites of phase A (S5, the first block of S1): green before and after T3, outside this file.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ArrowProductionTests : IDisposable
{
    private const int Birth = ArrowOracle.Birth;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public ArrowProductionTests() => Reset();

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
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The column 15 is a wall (the walk flag <c>0x40</c>) on every row: the arrow dies against it.</summary>
    private static CutBench.Cell WallColumn15(int column, int row) => column == 15 ? new CutBench.Cell(0x40, 0) : default;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p1) the shot of the hunter (type 3) and the normal shot of William (type 4): born at S, daughter of the hero, at the lowest free slot; the life equals the key of the type.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 3, "arrow3_east_wall")]
    [InlineData(6, 4, "arrow4_east_wall")]
    public void P1_TheNormalShotBirthsTheArrowAtS_DaughterOfTheHeroAtTheLowestFreeSlot_ItsLifeEqualsTheKey_IfAndOnlyIfTheProductionSetHoldsItsType(int item, int type, string name)
    {
        var key = ArrowOracle.Load(name, stepByImage: true);
        using var run = new ArrowRun("p1-" + name, item, WallColumn15, production: true);

        run.Go(1, 80);

        // The shot is asked once with the type of the bow, and the arrow is born if and only if the production set holds that type (derived from the set, not copied), in the same assertion as the birth.
        Assert.Contains(run.SpawnCalls, c => c.Type == type);
        Assert.Equal(PlayerManager.ProductionWiredWeaponTypes.Contains(type), run.Born is not null);

        // The arrow: born at S = P + 9, of the type of the bow, daughter of the hero, at the lowest free slot measured before the shot, with the statics of the fiche (flags, attack 131 and the power of the type, the box 24 x 16 x 11 at the Z 16).
        Assert.NotNull(run.Born);
        Assert.Equal(Birth, run.BirthTick);
        Assert.Equal(type, run.Born!.SpriteType);
        Assert.Same(run.Arena.Arc.HeroEntity, run.Born.ParentEntity);
        Assert.Equal(1, run.LowestFreeSlotBeforeBirth);
        Assert.Equal(run.LowestFreeSlotBeforeBirth, run.BirthSlot);
        Assert.Equal($"flags=0x12074 atk=(131,{(type == 3 ? 1 : 2)}) box=(1572863,1048575,720895) hbz=16", run.BirthStatics);

        // The line of the arrow at every tick of S to S+62: the status 2 to S+42 (the wall flag at S+42), the bounce at S+43, the end at S+60, absent at S+61.
        var expected = Enumerable.Range(0, 63).Select(k => $"{k,3}: {key.Line(key.FirstTick + k)}").ToList();
        var actual = Enumerable.Range(0, 63).Select(k => $"{k,3}: {run.Line(run.BirthTick + k)}").ToList();
        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
        Assert.Equal("absent", run.Line(run.BirthTick + 61));
        Assert.Equal(1, run.Ticks[run.BirthTick + 42].Adjusted);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p2) the table of slots: full, the shot is lost; one free slot, one arrow (the hunter's and William's).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    public void P2_WithNoFreeSlotTheShotOfTheArrowCreatesNothing_WithExactlyOneFreeSlotItCreatesOneArrow(int item, int type)
    {
        (int Held, bool Asked, bool Born) full;
        using (var run = new ArrowRun("p2-full" + type, item, WallColumn15, production: true, freeSlotsAtBirth: 0))
        {
            run.Go(1, 80);
            full = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == type), run.Born is not null);
        }

        (int Held, bool Asked, bool Born, int Tick, int Type, int Parent) one;
        using (var run = new ArrowRun("p2-one" + type, item, WallColumn15, production: true, freeSlotsAtBirth: 1))
        {
            run.Go(1, 80);
            one = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == type), run.Born is not null, run.BirthTick, run.Born?.SpriteType ?? -1, ReferenceEquals(run.Born?.ParentEntity, run.Arena.Arc.HeroEntity) ? 1 : 0);
        }

        Assert.Equal(
            ((EntitySlotTable.SlotCount, true, false), (EntitySlotTable.SlotCount - 1, true, true, Birth, type, 1)),
            ((full.Held, full.Asked, full.Born), (one.Held, one.Asked, one.Born, one.Tick, one.Type, one.Parent)));
    }
}
