#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2b T2 (docs/plan-e20-heros.md section 1.24.3, (p1) to (p3)): the production tests of the break, with the NULL hook: nothing is registered by the test (<c>hook: false</c> of <see cref="BreakRun"/>), the registry of the production runner alone decides.
/// They are red while <see cref="NativeSpriteRegistry.CreateForProduction"/> does not register E2 (phase B) and green once it does:
/// <list type="bullet">
/// <item><description>(p1) an object thrown by the real hero (<c>PutInHands</c> then <c>CarryRules.ThrowCarried</c>) breaks in production at T+23 / T+25 / T+26 with the effect of its header;</description></item>
/// <item><description>(p2) the registry of production holds (E, 2) for the ten types of the export and for no other;</description></item>
/// <item><description>(p3) the ten types at rest never call E2 over 100 ticks, on a set of real maps that together hold at rest an entity of each of the ten, next to a thrown object that does call it.</description></item>
/// </list>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftableBreakProductionTests : IDisposable
{
    private const string Mine = "Coal Mine";
    private const string MineMap = "Coal Mine-67";
    public LiftableBreakProductionTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    private static int FrameEndOf(int k, int ticksPerFrame) => ((k + 1 - 1) / ticksPerFrame + 1) * ticksPerFrame;

    private static BreakRun.CrateTick At(BreakRun run, int k) => run.Ticks[k + 1];

    // -----------------------------------------------------------------------------------------
    // (p1) the thrown object breaks in production.
    // -----------------------------------------------------------------------------------------

    private static void BreaksAtT23(BreakRun run, int ticksPerFrame, int effect)
    {
        // the registry of the run is the production one (the hook installed nothing): C23 is there, so the crate is released and thrown.
        Assert.True(run.Arc.Runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, LiftableAi.Code, run.Crate.SpriteType, out _));

        Assert.Equal((EntityStatus.Normal, 0, 0), (At(run, 22).Status, At(run, 22).E2Calls, At(run, 22).Z)); // arrival at T+22: still Normal, E2 never ran.

        var first = At(run, 23);
        Assert.Equal((EntityStatus.Deactivated, 2u, 1), (first.Status, first.Target, first.E2Calls)); // T+23: the first call of E2 asked the animation 2.

        var destroyed = At(run, 25);
        Assert.Equal((EntityStatus.FlagToDestroy, true, 3), (destroyed.Status, destroyed.Present, destroyed.E2Calls)); // T+25: the third call destroys it.
        Assert.Equal((false, -1), (At(run, 26).Present, At(run, 26).Slot)); // T+26: the slot is free.

        var effectFrame = FrameEndOf(25, ticksPerFrame);
        Assert.Contains((effectFrame, effect), run.AnchoredEffects);
        Assert.All(run.AnchoredEffects, e => Assert.Equal(effect, e.Effect));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void P1_ACrateThrownByTheHero_BreaksInProduction_AtT23_WithTheEffect10(int ticksPerFrame)
    {
        using var run = new BreakRun("p1-crate", Mine, MineMap, 36, contents: 0, hook: false);

        run.Go(ticksPerFrame, 40);

        BreaksAtT23(run, ticksPerFrame, effect: 10);
        Assert.Equal(292, run.Crate.SpriteType);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void P1_AJarThrownByTheHero_BreaksInProduction_AtT23_WithTheEffect2(int ticksPerFrame)
    {
        using var run = new BreakRun("p1-jar", "Coastal Cave", "Coastal Cave-70", 41, contents: 0, hook: false);

        run.Go(ticksPerFrame, 40);

        BreaksAtT23(run, ticksPerFrame, effect: 2);
        Assert.Equal(297, run.Crate.SpriteType);
    }

    // -----------------------------------------------------------------------------------------
    // (p2) the pin of the production registry.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void P2_TheProductionRegistryHoldsTheKeyE2_ForTheTenTypesOfTheExport_AndForNoOther()
    {
        var registry = new EventProgramRunner(null, GameState.Instance).NativeSprites;

        // the ten types, read from the export as the DLL reads it (every header whose slot E holds the code 2).
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var exported = new List<int>();
        foreach (var isMapPage in new[] { false, true })
        {
            for (var index = 0; index < 256; index++)
            {
                if (catalog.TryGetPrefab(isMapPage, index, out var prefab) && catalog.TryGet(prefab, out var header) && header.ProgramDeactivate == LiftableAi.BreakCode)
                {
                    exported.Add((isMapPage ? 0x100 : 0) + index);
                }
            }
        }

        Assert.Equal(10, exported.Count);
        foreach (var type in exported)
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode, type, out var handler), $"type {type}");
            Assert.NotNull(handler);
        }

        // every other type of the two pages has no (E, 2).
        for (var type = 0; type < 512; type++)
        {
            Assert.Equal(exported.Contains(type), registry.TryGet(ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode, type, out _));
        }

        // only the slot E and the code 2: the same types hold no other code in the slot E through this registration.
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 1, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, LiftableAi.BreakCode, 292, out _));
    }

    // -----------------------------------------------------------------------------------------
    // (p3) the ten types at rest never call E2.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The set of real maps that together hold, at rest, an entity of each of the ten types, with the tile where the hero arrives (a real portal arrival: the load loop reads the tile of the hero) so that the entities are born (the map-load pass spawns a record only when <c>SpriteDirection &amp; 0x40</c> is set and the tile of the hero is in
    /// its box <c>XMin..XMax</c> x <c>YMin..YMax</c>). Census of the export (<c>data-extracted/data/map_N.json</c>: the enabled entities, type = <c>SpriteTableIndex</c> plus <c>0x100</c> when <c>SpriteDirection &amp; 0x80</c>); the table type -> map:
    /// 234 on map 4 (the stone, also on 3, 7 and 11), 290 on 38 and 67, 291 on 24 and 124, 292 on 67, 293 on 24, 294 on 380, 297 on 91, 304 on 124, 373 and 438 on 70.
    /// Columns: the map, its folder, its world, the tile X and Y of the hero, the types expected at rest.
    /// </summary>
    public static TheoryData<int, string, string, int, int, string> RestMaps => new()
    {
        { 4, "Overworld", "Overworld 0,3-4", 8, 6, "234" },
        { 24, "Lars' Crypt", "Lars' Crypt-24", 28, 29, "291,293" },
        { 38, "Nirude’s Lair", "Nirude’s Lair-38", 30, 45, "290" },
        { 67, Mine, MineMap, 35, 43, "290,292" },
        { 70, "Coastal Cave", "Coastal Cave-70", 22, 0, "373,438" },
        { 91, "Magyscar", "Magyscar-91", 0, 0, "297" },
        { 124, "Kline's Nightmare", "Kline's Nightmare-124", 8, 6, "291,304" },
        { 380, "Deep Cave with Mimics", "Deep Cave with Mimics-380", 8, 6, "294" },
    };

    [Fact]
    public void P3_TheSetOfMapsHoldsAnEntityAtRestOfEachOfTheTenTypes()
    {
        var types = RestMaps.SelectMany(row => ((string)row[5]).Split(',')).Select(int.Parse).Distinct().ToArray();

        // the table covers the ten types and no other; that each map really holds them at rest is checked on the loaded world, in the test below.
        Assert.Equal(LiftableAi.BreakSpriteTypes.OrderBy(t => t).ToArray(), types.OrderBy(t => t).ToArray());
    }

    [Theory]
    [MemberData(nameof(RestMaps))]
    public void P3_TheTypesAtRestOfARealMap_NeverCallE2_In100Ticks_WhileAThrownObjectCallsItInTheSameCounter(int map, string zone, string world, int tileX, int tileY, string typeList)
    {
        var expected = typeList.Split(',').Select(int.Parse).ToArray();

        // the rest: the real map, the production registry and the native handlers on, 100 frames of one tick, nothing pressed.
        using (var arc = new ArcRun(new ArcSpec("E20g2b-p3-" + map, zone, world, Array.Empty<int>(), tileX, tileY, 0, 2000, RealController: true, Prefabs: true, Arrival: new ArcArrival((tileX * 24 + 12) << 16, (tileY * 16 + 8) << 16, 0, GameState.ResetAnimationId, 0))))
        {
            for (var i = 0; i < 4; i++)
            {
                arc.OneFrame();
            }

            var atRest = arc.Entities.Where(e => LiftableAi.BreakSpriteTypes.Contains(e.SpriteType)).ToList();
            foreach (var type in expected)
            {
                Assert.True(atRest.Any(e => e.SpriteType == type && e.Status == EntityStatus.Normal), $"map {map} type {type}: entities {arc.Entities.Count}, types {string.Join(',', arc.Entities.Select(e => e.SpriteType).Distinct())}");
            }

            var before = atRest.ToDictionary(e => e, e => (e.Flags, e.TargetAnimationId));
            for (var i = 0; i < 100; i++)
            {
                arc.OneFrame();
                Assert.All(atRest, e => Assert.Equal(EntityStatus.Normal, e.Status));
            }

            var counts = arc.Runner.NativeHandlerRunCounts;
            Assert.Equal(0, counts.GetValueOrDefault((ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode)));
            Assert.All(expected, type => Assert.True(arc.Runner.NativeSprites.TryGet(ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode, type, out _), $"type {type}: E2 is registered in the production registry, so a zero is a measure and not an absence"));
            Assert.All(atRest, e => Assert.Equal(before[e], (e.Flags, e.TargetAnimationId)));
        }

        // the positive: a crate thrown by the hero, with the same null hook, makes the same counter reach three calls.
        using var run = new BreakRun("p3-control-" + map, Mine, MineMap, 36, contents: 0, hook: false);
        run.Go(1, 40);
        Assert.Equal(3, At(run, 39).E2Calls);
    }
}
