#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2b T1 (docs/plan-e20-heros.md section 1.24.3, "Valeurs attendues" and (b2) to (b7)): the whole life of a crate or a jar thrown by the hero, in the REAL world of an exported map (<see cref="BreakRun"/>: real prefabs, the effect stage, the sound player, the
/// slot table), at 1, 2 and 3 ticks per frame. K is the number of ticks since T, the tick of the front of the Square (the tick 1 of the run is T). The expected values are the oracle's (<c>g4</c>, <c>g4b</c>, <c>g7</c>, <c>g15</c> of
/// <c>docs/plan-e20-annexe/e20-g/oracle/</c>, the plan's table): the arrival at T+22, E2 at T+23 (the animation 2, the flags <c>0x332344</c>, the sound 27), the status 4 at T+25 with the break effect attached, the slot free at T+26. E2 is registered by the test
/// through <see cref="BreakHook"/> only when the registry of the runner lacks it: the same tests hold before and after phase B. The spark positions of a touch draw random words (D-E0B-8): the tests count the sparks and never read where they fall.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftableBreakWorldTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const uint Right = PadState.Right;
    private const string Mine = "Coal Mine";
    private const string MineMap = "Coal Mine-67";
    private const int CrateIndex = 36;
    private const int JarIndex = 41;
    private const int Px = 65536;

    public LiftableBreakWorldTests()
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

    /// <summary>The tick of the run that ends the frame that holds the tick K + 1 (frames of <paramref name="ticksPerFrame"/> ticks from the tick 1): the first moment the effect stage, which applies its queue at the end of the frame, can show what K made.</summary>
    private static int FrameEndOf(int k, int ticksPerFrame) => ((k + 1 - 1) / ticksPerFrame + 1) * ticksPerFrame;

    private static BreakRun.CrateTick At(BreakRun run, int k) => run.Ticks[k + 1];

    /// <summary>
    /// The K of the destruction of an object whose first call of E2 is at <paramref name="firstK"/>: the oracle's (the binary runs one tick per frame) is <c>firstK + 2</c> - the animation 2 asked at the first call is validated by the clock of the next tick, which raises the end
    /// flag (<c>+0xAC</c>, the engine's end-of-animation event, <c>EntitySpawnFactory.OnAnimationFinished</c>) for the third call. The DLL validates the animations at the END OF THE FRAME (the engine's frame clock), so a first call that is not on the last tick of its frame waits
    /// for that frame end: the destruction is at <c>max(firstK + 2, the last tick of that frame)</c> (a tick later than the oracle at 3 ticks per frame for a first call on the first tick of a frame, equal at 1 and 2). Measured (T1), not a rule of E2, which only reads the flag.
    /// </summary>
    private static int DestroyK(int firstK, int ticksPerFrame) => Math.Max(firstK + 2, FrameEndOf(firstK, ticksPerFrame));

    /// <summary>The life of an object from its first call of E2 at <paramref name="firstK"/>: Deactivated with the calls counted up to the destruction, destroyed (still in the world) at <see cref="DestroyK"/> with the effect attached at the end of that frame, out of the world at the next tick.</summary>
    private static void LifeAfterTheFirstCall(BreakRun run, int firstK, int ticksPerFrame, int effect = 10)
    {
        var destroyK = DestroyK(firstK, ticksPerFrame);
        for (var k = firstK; k < destroyK; k++)
        {
            Assert.Equal((EntityStatus.Deactivated, 2u, k - firstK + 1), (At(run, k).Status, At(run, k).Target, At(run, k).E2Calls));
        }

        var destroyed = At(run, destroyK);
        Assert.Equal((EntityStatus.FlagToDestroy, true, destroyK - firstK + 1), (destroyed.Status, destroyed.Present, destroyed.E2Calls));
        Assert.Equal((false, -1, destroyK - firstK + 1), (At(run, destroyK + 1).Present, At(run, destroyK + 1).Slot, At(run, destroyK + 1).E2Calls));
        var effectFrame = FrameEndOf(destroyK, ticksPerFrame);
        Assert.Contains((effectFrame, effect), run.AnchoredEffects);
        Assert.DoesNotContain(run.AnchoredEffects, e => e.Tick < effectFrame);
    }

    private static void Chronology(BreakRun run, int ticksPerFrame, int effect, int landing = 22)
    {
        // K = landing - 1: still in the air, the program E never ran.
        var air = At(run, landing - 1);
        Assert.True(air.Present && air.Status == EntityStatus.Normal && air.Z > 0 && air.OnGround == 0 && air.E2Calls == 0, $"T+{landing - 1}: {air}");

        // K = landing: on the ground, still Normal, nothing deactivated yet, the flags of the flight.
        var arrival = At(run, landing);
        Assert.Equal((EntityStatus.Normal, 0, 1, 0), (arrival.Status, arrival.Z, arrival.OnGround, arrival.E2Calls));
        Assert.True(arrival.Flags is 0x332374u or 0x232374u, $"flags {arrival.Flags:X}");

        // K = landing + 1: the pick phase deactivated it, E2 ran once: the animation 2 asked, the bit 0x30 cleared and 0x40 raised.
        var first = At(run, landing + 1);
        Assert.Equal((EntityStatus.Deactivated, 2u, 1), (first.Status, first.Target, first.E2Calls));
        Assert.Equal(arrival.Flags & ~0x30u | 0x40u, first.Flags);

        // K = landing + 2: the animation 2 is the validated one and its end is raised (+0xAC = 1); E2 ran again.
        var second = At(run, landing + 2);
        Assert.Equal((EntityStatus.Deactivated, 2u, 2u, 1, 2), (second.Status, second.Target, second.Current, second.ResetFlag, second.E2Calls));

        // K = landing + 3: the third call destroys it (status 4, still in the world); K = landing + 4: the slot is free.
        var destroyed = At(run, landing + 3);
        Assert.Equal((EntityStatus.FlagToDestroy, true, 3), (destroyed.Status, destroyed.Present, destroyed.E2Calls));
        var gone = At(run, landing + 4);
        Assert.Equal((false, -1, 3), (gone.Present, gone.Slot, gone.E2Calls));

        // the break effect of the header is attached at the end of the frame that holds the destruction, and never before; no other effect is attached in the run.
        var effectFrame = FrameEndOf(landing + 3, ticksPerFrame);
        Assert.Contains((effectFrame, effect), run.AnchoredEffects);
        Assert.DoesNotContain(run.AnchoredEffects, e => e.Tick < effectFrame);
        Assert.All(run.AnchoredEffects, e => Assert.Equal(effect, e.Effect));
    }

    // -----------------------------------------------------------------------------------------
    // (b2) the life of a thrown crate and of a thrown jar: the chronology of the table.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B2_ACrateThrownOnFlatGround_ArrivesAtT22_IsDeactivatedAtT23_IsDestroyedAtT25_AndItsSlotIsFreeAtT26_WithTheEffect10AndTheSound27(int ticksPerFrame)
    {
        using var run = new BreakRun("b2-crate", Mine, MineMap, CrateIndex, contents: 0);

        run.Go(ticksPerFrame, 40);

        Chronology(run, ticksPerFrame, effect: 10);
        Assert.Equal(292, run.Crate.SpriteType);
        // the sound of the throw at T, the sound 27 of the animation 2 at T+23 (the tick of the first call of E2); nothing else: a content of 0 drops nothing and plays no 84.
        Assert.Equal(new[] { (1, 26), (24, 27) }, run.Heard.Played.ToArray());
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B2_AJarThrownOnFlatGround_FollowsTheSameChronology_WithTheEffect2(int ticksPerFrame)
    {
        using var run = new BreakRun("b2-jar", "Coastal Cave", "Coastal Cave-70", JarIndex, contents: 0);

        run.Go(ticksPerFrame, 40);

        Chronology(run, ticksPerFrame, effect: 2);
        Assert.Equal(297, run.Crate.SpriteType);
        Assert.Equal(0x232344u, At(run, 23).Flags); // the jar has not the bit 0x100000 of the crate: 0x232374 becomes 0x232344.
        Assert.Equal(2, run.Heard.Played.Count(p => p.Tick is 1 or 24)); // the throw and the sound of the jar's break, nothing between.
        Assert.Equal(26, run.Heard.Played.First().Sfx);
    }

    // -----------------------------------------------------------------------------------------
    // (b3) the touch of a target: the Beannoid at x = 165 is touched at T+21, the one at x = 150 is flown over.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_ACrateThrownWithADirectionOnABeannoidAtX165_TouchesItAtT21_IsDeactivatedAtT22_IsDestroyedAtT24_AndLeavesTheSlotAtT25(int ticksPerFrame)
    {
        using var run = new BreakRun("b3-hit", Mine, MineMap, CrateIndex, targetX: 165, contents: 0);

        run.Go(ticksPerFrame, 40, tick => tick == 1 ? Square | Right : 0u);

        // nothing touched before T+21, the touch at T+21 (the hit counter of the crate, one spark), the program E at T+22 on the strength of that touch alone (the crate is still in the air).
        for (var k = 0; k < 21; k++)
        {
            Assert.True(At(run, k).HitCounter == 0 && run.Sparks[k + 1] == 0 && At(run, k).E2Calls == 0, $"T+{k}");
        }

        var touch = At(run, 21);
        Assert.Equal((1, 1, EntityStatus.Normal, 0), (touch.HitCounter, run.Sparks[22], touch.Status, touch.OnGround));
        Assert.True(touch.Z > 0);
        var first = At(run, 22);
        Assert.Equal((EntityStatus.Deactivated, 2u, 1), (first.Status, first.Target, first.E2Calls));
        Assert.Equal((EntityStatus.Deactivated, 2), (At(run, 23).Status, At(run, 23).E2Calls));
        Assert.Equal((EntityStatus.FlagToDestroy, true, 3), (At(run, 24).Status, At(run, 24).Present, At(run, 24).E2Calls));
        Assert.Equal((false, -1), (At(run, 25).Present, At(run, 25).Slot));
        Assert.Contains((FrameEndOf(24, ticksPerFrame), 10), run.AnchoredEffects);
        Assert.DoesNotContain(run.AnchoredEffects, e => e.Tick < FrameEndOf(24, ticksPerFrame));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_ACrateThrownWithADirectionOverABeannoidAtX150_FliesOverIt_TouchesNothing_AndBreaksAtItsLanding(int ticksPerFrame)
    {
        using var run = new BreakRun("b3-over", Mine, MineMap, CrateIndex, targetX: 150, contents: 0);

        run.Go(ticksPerFrame, 40, tick => tick == 1 ? Square | Right : 0u);

        // flown over (the crate is at a height of 36 to 50 px over it): no hit counter, no spark, in the whole run; the arrival at T+22 and the first call of E2 at T+23, the destruction at T+25.
        for (var k = 0; k < 40; k++)
        {
            Assert.True(At(run, k).HitCounter == 0 && run.Sparks[k + 1] == 0, $"T+{k}");
        }

        Assert.Equal((EntityStatus.Normal, 0, 1, 0), (At(run, 22).Status, At(run, 22).Z, At(run, 22).OnGround, At(run, 22).E2Calls));
        Assert.Equal((EntityStatus.Deactivated, 2u, 1), (At(run, 23).Status, At(run, 23).Target, At(run, 23).E2Calls));
        Assert.Equal((EntityStatus.FlagToDestroy, 3), (At(run, 25).Status, At(run, 25).E2Calls));
        Assert.False(At(run, 26).Present);
    }

    // -----------------------------------------------------------------------------------------
    // (b4) the wall at x = 133 and the steps of 16 and 48 px.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B4_ACrateThrownWithADirectionAgainstAWall_StopsAtX133_RunsE2AtT9_IsDestroyedAtT11_AndLeavesTheSlotAtT12(int ticksPerFrame)
    {
        using var run = new BreakRun("b4-wall", Mine, MineMap, CrateIndex, cell: (cx, _) => new CutBench.Cell(cx == 6 ? 0x40 : 0, 0), contents: 0);

        run.Go(ticksPerFrame, 30, tick => tick == 1 ? Square | Right : 0u);

        Assert.Equal((133 * Px, EntityStatus.Normal, 0), (At(run, 8).X, At(run, 8).Status, At(run, 8).E2Calls)); // in the wall, in the air, still Normal.
        Assert.True(At(run, 8).Z > 0);
        var first = At(run, 9);
        Assert.Equal((133 * Px, EntityStatus.Deactivated, 2u, 1), (first.X, first.Status, first.Target, first.E2Calls));
        LifeAfterTheFirstCall(run, firstK: 9, ticksPerFrame);
        if (ticksPerFrame <= 2)
        {
            Assert.Equal(11, DestroyK(9, ticksPerFrame)); // the plan's table: the status 4 at T+11, the slot free at T+12.
        }
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B4_ACrateThrownWithADirectionOntoAStepOf16px_SettlesOnItsTopAtT20_AndRunsE2AtT21(int ticksPerFrame)
    {
        using var run = new BreakRun("b4-step16", Mine, MineMap, CrateIndex, cell: (cx, _) => new CutBench.Cell(0, cx >= 6 ? 1 : 0), contents: 0);

        run.Go(ticksPerFrame, 30, tick => tick == 1 ? Square | Right : 0u);

        var air = At(run, 19);
        Assert.Equal((EntityStatus.Normal, 0, 0), (air.Status, air.OnGround, air.E2Calls));
        Assert.True(air.Z > 16 * Px);
        var landed = At(run, 20);
        Assert.Equal((16 * Px, 1, EntityStatus.Normal, 0), (landed.Z, landed.OnGround, landed.Status, landed.E2Calls));
        var first = At(run, 21);
        Assert.Equal((16 * Px, EntityStatus.Deactivated, 2u, 1), (first.Z, first.Status, first.Target, first.E2Calls));
        LifeAfterTheFirstCall(run, firstK: 21, ticksPerFrame);
        if (ticksPerFrame <= 2)
        {
            Assert.Equal(23, DestroyK(21, ticksPerFrame));
        }
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B4_ACrateThrownWithADirectionOntoAStepOf48px_SettlesOnItsTop_AndRunsE2TheTickAfterItLanded(int ticksPerFrame)
    {
        using var run = new BreakRun("b4-step48", Mine, MineMap, CrateIndex, cell: (cx, _) => new CutBench.Cell(0, cx >= 6 ? 3 : 0), contents: 0);

        run.Go(ticksPerFrame, 30, tick => tick == 1 ? Square | Right : 0u);

        // the tick it is first on the ground (on the top, 48 px up) is Normal with E2 not called; the next tick E2 ran once; two ticks later it is destroyed. The tick is read from the run, not typed: the plan's table gives the 16 px step only.
        var landed = Enumerable.Range(0, 30).First(k => At(run, k).OnGround == 1);
        Assert.Equal((48 * Px, EntityStatus.Normal, 0), (At(run, landed).Z, At(run, landed).Status, At(run, landed).E2Calls));
        Assert.Equal((48 * Px, EntityStatus.Deactivated, 1), (At(run, landed + 1).Z, At(run, landed + 1).Status, At(run, landed + 1).E2Calls));
        LifeAfterTheFirstCall(run, firstK: landed + 1, ticksPerFrame);
    }

    // -----------------------------------------------------------------------------------------
    // (b5) the loot: the object drawn at the creation (E20.e0c) is born at the break, at the place of the crate; a content of 0 drops nothing.
    // -----------------------------------------------------------------------------------------

    private const int Gildas = 0x45;
    private const int TypeOfItemZero = 0x1E;
    private const int DropSound = 84;

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B5_TheContentsOfTheCrateAreDroppedAtItsBreak_WhereAContentOfZeroDropsNothing(int ticksPerFrame)
    {
        using var full = new BreakRun("b5-gildas", Mine, MineMap, CrateIndex, contents: Gildas);
        full.Go(ticksPerFrame, 40);
        var broken = At(full, 25);
        var born = full.Arc.Entities.Where(e => !full.EntitiesBefore.Contains(e)).ToList();

        // derived from the contents the creation drew (the world's roll): the object of the item at the crate, one raw unit up, the sound 84 at the tick of the destruction and no other.
        var drop = Assert.Single(born);
        Assert.Equal(Gildas + TypeOfItemZero, drop.SpriteType);
        Assert.Equal(((broken.X >> 16) << 16, broken.Y), (drop.PosX, drop.PosY)); // the interface of the apparition takes whole pixels: 143.5 becomes 143.
        Assert.Equal(new[] { (1, 26), (24, 27), (26, DropSound) }, full.Heard.Played.ToArray());
        Assert.Equal((uint)Gildas, full.Crate.ContentsItemId);

        // the pair: a content of 0 drops nothing and plays no 84.
        using var empty = new BreakRun("b5-empty", Mine, MineMap, CrateIndex, contents: 0);
        empty.Go(ticksPerFrame, 40);

        Assert.Empty(empty.Arc.Entities.Where(e => !empty.EntitiesBefore.Contains(e)));
        Assert.DoesNotContain(empty.Heard.Played, p => p.Sfx == DropSound);
        Assert.Equal(EntityStatus.FlagToDestroy, At(empty, 25).Status);
    }

    // -----------------------------------------------------------------------------------------
    // (b6) deep water under the arrival (E20.f6a): the effect 6 instead of the effect 10, and E2 is never reached.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B6_ACrateThatLandsInDeepWater_IsDestroyedWithTheEffect6AtOnce_NextToTheDryLandingThatBreaksWithTheEffect10(int ticksPerFrame)
    {
        using var dry = new BreakRun("b6-dry", Mine, MineMap, CrateIndex, contents: 0);
        dry.Go(ticksPerFrame, 40);
        Assert.Equal(3, At(dry, 30).E2Calls);
        Assert.All(dry.AnchoredEffects, e => Assert.Equal(10, e.Effect));

        using var water = new BreakRun("b6-water", Mine, MineMap, CrateIndex, cell: (cx, _) => new CutBench.Cell(0, 0, null, cx >= 5 ? 8 : 0), contents: 0);
        water.Go(ticksPerFrame, 40);

        // lands at T+22 (Normal), and the tick after it is destroyed by the pick phase itself (the slope 4 of the deep water): the program E never ran, the effect is the 6.
        Assert.Equal((EntityStatus.Normal, 1, 0), (At(water, 22).Status, At(water, 22).OnGround, At(water, 22).E2Calls));
        Assert.Equal((EntityStatus.FlagToDestroy, 0), (At(water, 23).Status, At(water, 23).E2Calls));
        Assert.False(At(water, 24).Present);
        Assert.Equal(0, At(water, 35).E2Calls);
        Assert.NotEmpty(water.AnchoredEffects);
        Assert.All(water.AnchoredEffects, e => Assert.Equal(6, e.Effect));
        Assert.Contains((FrameEndOf(23, ticksPerFrame), 6), water.AnchoredEffects);
    }

    // -----------------------------------------------------------------------------------------
    // (b7) an object at rest never runs E2 (the ten types at rest are (p3)'s, T2); the thrown one does, in the same run.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B7_ACrateAtRestNeverRunsE2_WhileTheCrateThrownNextToItDoes(int ticksPerFrame)
    {
        using var run = new BreakRun("b7-rest", Mine, MineMap, CrateIndex, contents: 0, restAt: (400, 300));
        var rest = run.Rest!;
        var before = (rest.Flags, rest.TargetAnimationId, rest.Status);

        run.Go(ticksPerFrame, 100);

        // the three calls of E2 counted by the runner are the thrown crate's own; the one at rest is Normal with the flags and the animation it had.
        Assert.Equal(3, At(run, 99).E2Calls);
        Assert.Equal((EntityStatus.FlagToDestroy, 3), (At(run, 25).Status, At(run, 25).E2Calls));
        Assert.Equal(before, (rest.Flags, rest.TargetAnimationId, rest.Status));
        Assert.Contains(rest, run.Arc.Entities);
        Assert.Equal(EntityStatus.Normal, rest.Status);
    }
}
