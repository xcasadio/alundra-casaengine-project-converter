#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T2, phase B (docs/plan-e20-heros.md section 1.31.4, tests (p1) to (p4)): the earth tome 2 through the PRODUCTION path, with no seam of the tests: the entries <c>0x2C</c> of the three seams of the item in use in <see cref="PlayerManager"/> (the start of the tomes of h4a,
/// <see cref="EarthTome2Ai.Driver"/>, <see cref="EarthTome2Ai.Cancel"/>) and the (E, 13) of <see cref="NativeSpriteRegistry.CreateForProduction"/>. They are RED before the phase B (the entry of the tome is the stub of h0a: the Circle starts nothing; E13 is not registered) and green after it.
/// (p1) Rond with the tome 44 at the middle of the map: the key of (w1); (p2) the table of slots full: no rock, no exception, the object freed at the tick 309 (the driver runs); (p3) the invariant of D-F3-4: <see cref="PlayerManager.UnhandledWeaponSpriteCodes"/> of {17} is empty with the production registry, and is <c>[(E, 13)]</c> with an empty one;
/// (p4) Rond with the tome 44 interrupted as <c>cancel_2c_pf_t100</c> (R-EL-6): the key, and a sound 45 for each rock cancelled in the air.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome2ProductionTests : IDisposable
{
    private const int Sfx55 = 55;
    private const int Sfx45 = 45;
    private const int HeroHurtSfx = 23;
    private const int FillerType = 5;

    public EarthTome2ProductionTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void P1_RondWithTheTome44_TheKeyOfW1HoldsThroughTheEntriesOfTheObject_FifteenRocks_TheItemFreedAtTheTick309()
    {
        var key = EarthTome2WorldTests.Mid();
        using var run = new EarthTome2Run("p1", 600, 480, production: true);
        Assert.Null(PlayerManager.ItemStartForTests); // no seam: the entries of the object alone.
        Assert.Null(PlayerManager.ItemDriverForTests);
        Assert.Null(PlayerManager.ItemCancelForTests);

        run.Go(330);

        var wrong = EarthTome2WorldTests.Everything(key, run, 330);
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));
        var lives = run.Lives();
        Assert.Equal(15, lives.Count);
        Assert.Equal(new[] { 18, 34, 50, 66, 82, 98, 114, 130, 146, 162, 178, 194, 226, 226, 242 }, lives.Select(l => l.Birth));
        Assert.Equal(lives.Select(l => l.Birth).Order(), EarthTome2WorldTests.SoundTicks(run, Sfx55));
        Assert.Equal(lives.Select(l => l.Impact).Order(), EarthTome2WorldTests.SoundTicks(run, Sfx45));
        Assert.Equal(0x2Cu, run.Hero[308].ItemInUse);
        Assert.Equal(309, Enumerable.Range(1, 330).First(t => run.Hero[t].ItemInUse == 0));
    }

    [Fact]
    public void P2_WithNoFreeSlot_NoRockIsBorn_NoExceptionIsRaised_AndTheObjectIsFreedAtTheTick309_TheDriverRuns()
    {
        using var run = new EarthTome2Run("p2", 600, 480, production: true);

        run.Go(330, 1, null, tick =>
        {
            if (tick < 2 || tick > 260)
            {
                return;
            }

            var world = (IEntityWorldContext)run.Arena.Arc.Proxy;
            while (run.Arena.Arc.Proxy.SlotTable.HeldCount < EntitySlotTable.SlotCount)
            {
                Assert.NotNull(world.SpawnEntityBySpriteIndex(run.Arena.Hero, false, FillerType, 100 << 16, 100 << 16, 0, 0x10));
            }
        });

        Assert.All(Enumerable.Range(1, 330), t => Assert.Empty(run.Rocks[t]));
        Assert.Equal(0x2Cu, run.Hero[308].ItemInUse);
        Assert.Equal(0u, run.Hero[309].ItemInUse); // the driver ran: the counter, the phases, the end of the cast, the release.
        Assert.Equal(309, Enumerable.Range(1, 330).First(t => run.Hero[t].ItemInUse == 0));
    }

    [Fact]
    public void P3_TheProductionRegistryHandlesTheCodesOfTheType17_AnEmptyOneLacksOnlyE13()
    {
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var production = new EventProgramRunner(null, GameState.Instance).NativeSprites;

        Assert.Empty(PlayerManager.UnhandledWeaponSpriteCodes(EarthTome2Ai.SpriteTypes, catalog, production));
        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, EarthTome2Ai.ImpactCode) }, PlayerManager.UnhandledWeaponSpriteCodes(EarthTome2Ai.SpriteTypes, catalog, new NativeSpriteRegistry()).ToArray());
    }

    [Fact]
    public void P4_RondWithTheTome44_InterruptedAtTheTick100_TheKeyHolds_ASound45PerRockCancelledInTheAir_ThroughTheEntriesOfTheObject()
    {
        var key = EarthTome2Key.CancelKey.Load("disc/keys", "cancel_2c_pf_t100", "critic/keys", "cancel_2c_pf_t100_sounds.txt");
        const int at = 100;
        using var run = new EarthTome2Run("p4", 100, 100, production: true);

        run.Go(key.LastTick, 1, at);

        var wrong = EarthTome2Compare.Cancelled(key, run);
        wrong.AddRange(EarthTome2Compare.Sounds(key.Sounds, run, at - 2, key.LastTick));
        Assert.True(wrong.Count == 0, EarthTome2Compare.Show(wrong));
        var inTheAir = key.Ticks[at - 1].Rocks.Values.Count(r => r.Status == 2);
        Assert.True(inTheAir >= 2, "the interruption catches rocks in the air");
        Assert.Equal(inTheAir, run.Heard.Played.Count(p => p.Tick == at && p.Sfx == Sfx45));
        Assert.Equal(new[] { at }, run.Heard.Played.Where(p => p.Sfx == HeroHurtSfx).Select(p => p.Tick));
        Assert.Equal(0x2Cu, run.Hero[at - 1].ItemInUse);
        Assert.Equal(0u, run.Hero[at].ItemInUse);
    }
}
