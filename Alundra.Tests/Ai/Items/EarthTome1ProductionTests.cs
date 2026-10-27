#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T2, phase B (docs/plan-e20-heros.md section 1.31.3, tests (p1) to (p4)): the earth tome 1 through the PRODUCTION path, with no seam of the tests: the entries <c>0x2B</c> of the three seams of the item in use in <see cref="PlayerManager"/> (the start of the tomes of h4a,
/// <see cref="EarthTome1Ai.Driver"/>, <see cref="EarthTome1Ai.Cancel"/>) and the (E, 3) of <see cref="NativeSpriteRegistry.CreateForProduction"/>. They are RED before the phase B (the entry of the tome is the stub of h0a: the Circle starts nothing; E3 is not registered) and green after it.
/// (p1) Rond with the tome 43 alone, magic 4/4: the type 16 is born at the tick 22 and the key of (w1) holds; (p2) the table of slots full: no rock, no exception, the object freed at the tick 129 (the driver runs); (p3) the four rocks of the map 388 (records 18 to 21, made by the opcode 0x2D: the map load skips them, their direction byte has the bit 0x40 clear): E3 ran (status 3 then 4, the sound 46, no exception), with
/// no exact tick (D-E20-37), and they are not marked <c>HasLogicalZ</c>; (p4) the invariant of D-F3-4: <see cref="PlayerManager.UnhandledWeaponSpriteCodes"/> of {16} is empty with the production registry, and is <c>[(E, 3)]</c> with an empty one.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class EarthTome1ProductionTests : IDisposable
{
    private const int Sfx46 = 46;
    private const int FillerType = 5;

    public EarthTome1ProductionTests() => Reset();

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
    public void P1_RondWithTheTome43_BornsTheRocksAtTheTick22_AndTheKeyOfW1HoldsThroughTheEntriesOfTheObject()
    {
        var key = EarthTome1Key.Load("earth1_flat_east");
        using var run = new EarthTome1Run("p1", 24, 100, null, production: true);
        Assert.Null(PlayerManager.ItemStartForTests); // no seam: the entries of the object alone.
        Assert.Null(PlayerManager.ItemDriverForTests);
        Assert.Null(PlayerManager.ItemCancelForTests);

        run.Go(140);

        var wrong = EarthTome1WorldTests.Everything(key, run, 140);
        Assert.True(wrong.Count == 0, EarthTome1Compare.Show(wrong));
        var lives = EarthTome1WorldTests.Lives(run, 140);
        Assert.Equal(32, lives.Count);
        Assert.Equal(22, lives.Min(l => l.Birth));
        Assert.Equal(97, lives.Max(l => l.Birth));
        Assert.Equal(32, run.Heard.Played.Count(p => p.Sfx == Sfx46));
        Assert.Equal(129, Enumerable.Range(1, 140).First(t => run.Hero[t].ItemInUse == 0));
    }

    [Fact]
    public void P2_WithNoFreeSlot_NoRockIsBorn_NoExceptionIsRaised_AndTheObjectIsFreedAtTheTick129_TheDriverRuns()
    {
        using var run = new EarthTome1Run("p2", 24, 100, null, production: true);

        run.Go(140, null, tick =>
        {
            if (tick < 2 || tick > 97)
            {
                return;
            }

            var world = (IEntityWorldContext)run.Arena.Arc.Proxy;
            while (run.Arena.Arc.Proxy.SlotTable.HeldCount < EntitySlotTable.SlotCount)
            {
                Assert.NotNull(world.SpawnEntityBySpriteIndex(run.Arena.Hero, false, FillerType, 100 << 16, 100 << 16, 0, 0x10));
            }
        });

        Assert.All(Enumerable.Range(1, 140), t => Assert.Empty(run.Rocks[t]));
        Assert.Equal(0x2Bu, run.Hero[128].ItemInUse);
        Assert.Equal(0u, run.Hero[129].ItemInUse); // the driver ran: the counter, the phases, the end of the cast, the release.
        Assert.Equal(129, Enumerable.Range(1, 140).First(t => run.Hero[t].ItemInUse == 0));
    }

    [Fact]
    public void P3_TheFourRocksPlacedByTheMap388_RunE3_TheStatus3ThenTheStatus4_TheSound46_NoExactTick_NotMarkedHasLogicalZ()
    {
        var spec = new ArcSpec("E20h4c1-p3", "The Klark", "The Klark - inner-388", Array.Empty<int>(), 24, 36, 0, 2000, RealController: true, Prefabs: true);
        using var arc = new ArcRun(spec);
        var heard = new EarthTome1Run.Sounds();
        typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(arc.Proxy, heard);
        var rocks = new List<EntityScriptProxy>();
        arc.OneFrame();

        // The records 18 to 21 of the map carry the direction 0 (bit 0x40 clear): the map load does not make them, the opcode 0x2D (a script) does, with no zone check.
        var world = (IEntityWorldContext)arc.Proxy;
        for (var record = 18; record <= 21; record++)
        {
            Assert.NotNull(world.SpawnEntityByRecordId(arc.Hero, record));
        }

        var seen3 = new HashSet<EntityScriptProxy>();
        var seen4 = new HashSet<EntityScriptProxy>();
        for (var frame = 1; frame <= 300; frame++)
        {
            heard.Tick = frame;
            arc.HoldDirections(0);
            arc.OneFrame();
            foreach (var rock in arc.Entities.Where(e => e.SpriteType == EarthTome1Run.Rock))
            {
                if (!rocks.Contains(rock))
                {
                    rocks.Add(rock);
                }

                if (rock.Status == EntityStatus.Deactivated)
                {
                    seen3.Add(rock);
                }

                if (rock.Status == EntityStatus.FlagToDestroy)
                {
                    seen4.Add(rock);
                }
            }
        }

       Assert.Equal(4, rocks.Count);
        Assert.Equal(4, seen3.Count);
        Assert.Equal(4, seen4.Count);
        Assert.Equal(4, heard.Played.Count(p => p.Sfx == Sfx46));
        Assert.All(rocks, r => Assert.False(r.HasLogicalZ));
    }

    [Fact]
    public void P4_TheProductionRegistryHandlesTheCodesOfTheType16_AnEmptyOneLacksOnlyE3()
    {
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var production = new EventProgramRunner(null, GameState.Instance).NativeSprites;

        Assert.Empty(PlayerManager.UnhandledWeaponSpriteCodes(EarthTome1Ai.SpriteTypes, catalog, production));
        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, EarthTome1Ai.ImpactCode) }, PlayerManager.UnhandledWeaponSpriteCodes(EarthTome1Ai.SpriteTypes, catalog, new NativeSpriteRegistry()).ToArray());
    }
}
