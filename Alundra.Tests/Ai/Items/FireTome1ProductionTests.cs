#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T2, phase B (docs/plan-e20-heros.md section 1.31.6, tests (p1) to (p4)): the fire tome 1 through the PRODUCTION path, with no seam of the tests: the entries <c>0x2F</c> of the three seams of the item in use in <see cref="PlayerManager"/> (the start of the tomes of h4a,
/// <see cref="FireTome1Ai.Drive"/>, <see cref="FireTome1Ai.Cancel"/>) and the (C, 63) of <see cref="NativeSpriteRegistry.CreateForProduction"/> (the (E, 3) of the type 23 came with the phase A1). They are RED before the phase B (the entry of the tome is the stub of h0a: the Circle starts nothing; C63 is not
/// registered) and green after it.
/// (p1) Rond with the tome 47 alone, magic 4/4: the key of (w1) holds; (p2) the table of slots full: the magic is lost, the item is freed at the tick 34, nothing is born (<c>table_full</c>); (p3) Rond then the interruption of the tick 60 (R-EL-6): <c>hurt60</c> and the effect <c>(19, 2)</c>; (p4) the invariant of D-F3-4:
/// <see cref="PlayerManager.UnhandledWeaponSpriteCodes"/> of {20, 23} is empty with the production registry, and is <c>[(C, 63)]</c> before the phase B.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class FireTome1ProductionTests : IDisposable
{
    private const uint Tome = 0x2F;
    private static readonly int[] Decoy = { 1100, 900 };

    public FireTome1ProductionTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static void AssertEqual(System.Collections.Generic.List<string> wrong) => Assert.True(wrong.Count == 0, FireTome1Compare.Show(wrong));

    [Fact]
    public void P1_RondWithTheTome47_TheKeyOfW1HoldsThroughTheEntriesOfTheObject_TheColumnAndTheFirstBallAt34_ElevenBalls_TheItemFreedAt149()
    {
        var key = FireTome1Key.Load("fire1_cast_pf");
        using var run = new FireTome1Run("p1", production: true);
        Assert.Null(PlayerManager.ItemStartForTests); // no seam: the entries of the object alone.
        Assert.Null(PlayerManager.ItemDriverForTests);
        Assert.Null(PlayerManager.ItemCancelForTests);

        run.Go(200);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 200));
        var columns = run.Lives(FireTome1Run.Column);
        var balls = run.Lives(FireTome1Run.Ball);
        Assert.Single(columns);
        Assert.Equal(34, columns[0][0].Tick);
        Assert.Equal(Enumerable.Range(0, 11).Select(k => 34 + 8 * k), balls.Select(b => b[0].Tick));
        Assert.Equal(Tome, run.Hero[148].ItemInUse);
        Assert.Equal(149, Enumerable.Range(1, 200).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 200), t => Assert.Equal(3, run.Hero[t].Mp)); // 4 -> 3 at the start.
    }

    [Fact]
    public void P2_WithNoFreeSlot_NothingIsBorn_TheMagicIsLost_TheItemIsFreedAtTheTick34_TheKeyOfTableFullHolds()
    {
        var key = FireTome1Key.Load("fire1_table_full_pf");
        using var run = new FireTome1Run("p2", Enumerable.Repeat(Decoy, 63).ToList(), production: true);

        run.Go(80);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 80));
        Assert.Empty(run.Lives(FireTome1Run.Column));
        Assert.Empty(run.Lives(FireTome1Run.Ball));
        Assert.Equal(Tome, run.Hero[33].ItemInUse);
        Assert.Equal(0u, run.Hero[34].ItemInUse); // the driver ran and answered 1 at the refused birth: freed.
        Assert.Equal(34, Enumerable.Range(1, 80).First(t => run.Hero[t].ItemInUse == 0));
        Assert.All(Enumerable.Range(1, 80), t => Assert.Equal(3, run.Hero[t].Mp)); // 4 -> 3 at the start, never given back.
    }

    [Fact]
    public void P3_RondWithTheTome47_InterruptedAtTheTick60_TheKeyOfHurt60Holds_TheEffect19_2AtTheColumn_TheBallsDeactivated_TheItemFreed()
    {
        var key = FireTome1Key.Load("fire1_hurt60_pf");
        using var run = new FireTome1Run("p3", production: true);

        run.Go(135, interruptAt: 60);

        AssertEqual(FireTome1Compare.Everything(key, run, 1, 135, heroAfterToo: true));
        Assert.Contains("fx(19,2,6553600,6553600,1179650)", run.Events[60]); // the effect at the raw position of the column, Z in the binary's frame.
        Assert.Equal(Tome, run.Hero[59].ItemInUse);
        Assert.Equal(0u, run.Hero[60].ItemInUse);
        Assert.Equal(1, run.Lives(FireTome1Run.Column).Count);
        Assert.Equal(4, run.Lives(FireTome1Run.Ball).Count); // born at 34, 42, 50, 58; none afterwards.
    }

    [Fact]
    public void P4_TheProductionRegistryHandlesTheCodesOfTheTypes20And23_AnEmptyOneLacksC63AndE3()
    {
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var production = new EventProgramRunner(null, GameState.Instance).NativeSprites;
        var types = new[] { 20, 23 };

        Assert.Empty(PlayerManager.UnhandledWeaponSpriteCodes(types, catalog, production));
        var empty = PlayerManager.UnhandledWeaponSpriteCodes(types, catalog, new NativeSpriteRegistry()).ToArray();
        Assert.Contains((ScriptHelper.ProgramCTick, FireTome1Ai.BallCode), empty);
        Assert.Contains((ScriptHelper.ProgramEDeactivate, EarthTome1Ai.ImpactCode), empty);
    }
}
