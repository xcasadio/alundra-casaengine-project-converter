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

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.h4a T1 (docs/plan-e20-heros.md section 1.31.1, the tests (t3a), (t3b) and (t4)): the states of the cast of a tome (<c>0x32</c> and <c>0x33</c>: <c>0x800325A4</c>; <c>0x34</c>: <c>0x80032680</c>) and the guard of the slope
/// (<c>0x800351B8</c>), on the bench of map 14 (<see cref="TomeBench"/>, one tick per frame, the real animations of the hero). The states are entered by the test (the animation posed, a tome in progress by the seams of D-H-4:
/// the driver of the test keeps the object in use or frees it), without the start function, except in (t3b). Every case without effect is paired with a case of the same front that acts.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TomeStatesTests : IDisposable
{
    private const uint Tome = 0x2B;
    private const uint Cast = PlayerManager.TomeCastAnimationId;
    private const uint Hold = PlayerManager.TomeHoldAnimationId;
    private const uint End = PlayerManager.TomeEndAnimationId;

    public TomeStatesTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        PlayerManager.ItemStubCallsForTests = null;
        PlayerManager.TomeTrackCallsForTests = null;
    }

    /// <summary>The state of the test: the driver of the object in use answers 0 ("still in use") until <see cref="Release"/> is set.</summary>
    private sealed class Pose
    {
        public bool Release;
    }

    /// <summary>Poses the animation <paramref name="state"/> and the object <paramref name="inUse"/> in use (0: none), the driver of the test keeping it in use until released.</summary>
    private static Pose Enter(TomeBench bench, uint state, uint inUse = Tome)
    {
        var pose = new Pose();
        PlayerManager.ItemDriverForTests = (_, _) => pose.Release ? 1 : 0;
        PlayerManager.ItemCancelForTests = (_, _) => { };
        bench.Hero.ItemInUse = inUse;
        bench.Hero.ItemUseCounter = 0;
        bench.Hero.ItemUsePhase = 0;
        bench.Hero.TargetAnimationId = state;
        return pose;
    }

    private static List<uint> Run(TomeBench bench, int ticks, uint hold = 0)
    {
        var states = new List<uint>();
        for (var i = 0; i < ticks; i++)
        {
            bench.Tick(hold);
            states.Add(bench.Hero.TargetAnimationId);
        }

        return states;
    }

    // -----------------------------------------------------------------------------------------
    // (t3a) the states.
    // -----------------------------------------------------------------------------------------

    /// <summary>0x32 lasts the 32 ticks of its animation and chains to 0x33 (the animation 50 of the export), then 0x33 is held, the loop of 36 ticks included, as long as a tome is in progress.</summary>
    [Fact]
    public void T3a_TheCast0x32_LastsItsAnimation_ThenHoldsInTheState0x33_WhileATomeIsInProgress()
    {
        using var bench = new TomeBench("t3a-cast", 4, 4);
        var pose = Enter(bench, Cast);

        var states = Run(bench, 150);

        var firstHold = states.IndexOf(Hold) + 1; // 1-based tick
        Assert.Equal(CastTicks, firstHold);
        Assert.All(states.Take(firstHold - 1), s => Assert.Equal(Cast, s));
        Assert.All(states.Skip(firstHold - 1), s => Assert.Equal(Hold, s));
        Assert.Equal(Tome, bench.Hero.ItemInUse);
        Assert.False(pose.Release);
    }

    /// <summary>The 1-based tick at which 0x33 shows when 0x32 is posed before the tick 1: the ticks 1 to 32 are the 32 ticks of the animation 50 (the oracle's tick 33 for a start at the tick 1).</summary>
    private const int CastTicks = 33;

    /// <summary>A tome that stops being in progress (the object is freed, or is another one) sends 0x32, 0x33 to Idle on the ground and to 0x2D in the air, at the tick the object is freed; with a tome in progress they stay (the bound is the 8 tomes 0x2B to 0x32).</summary>
    [Theory]
    [InlineData(Cast, 0x2Bu, true)]
    [InlineData(Cast, 0x32u, true)]
    [InlineData(Hold, 0x2Bu, true)]
    [InlineData(Hold, 0x32u, true)]
    [InlineData(Cast, 0x2Au, false)]
    [InlineData(Cast, 0x33u, false)]
    [InlineData(Cast, 0x23u, false)]
    [InlineData(Hold, 0x2Au, false)]
    [InlineData(Hold, 0x33u, false)]
    [InlineData(Hold, 0x23u, false)]
    [InlineData(Hold, 0u, false)]
    public void T3a_TheState_IsHeldOnlyWhileATomeIsInProgress_OtherwiseItGoesToIdleOnTheGround(uint state, uint inUse, bool held)
    {
        using var bench = new TomeBench("t3a-held-" + state + "-" + inUse, 4, 4);
        Enter(bench, state, inUse);

        bench.Tick();

        Assert.Equal(held ? state : 0u, bench.Hero.TargetAnimationId);
    }

    [Theory]
    [InlineData(Cast)]
    [InlineData(Hold)]
    public void T3a_TheTomeFreed_LeavesTheStateAtTheTickOfTheRelease_IdleOnTheGround_0x2DInTheAir_AndNotBefore(uint state)
    {
        // on the ground
        using (var bench = new TomeBench("t3a-free-ground-" + state, 4, 4))
        {
            var pose = Enter(bench, state);
            Assert.Equal(new[] { state, state, state }, Run(bench, 3));
            pose.Release = true;
            bench.Tick();
            Assert.Equal(0u, bench.Hero.TargetAnimationId);
            Assert.Equal(0u, bench.Hero.ItemInUse);
        }

        // in the air
        using (var bench = new TomeBench("t3a-free-air-" + state, 4, 4))
        {
            bench.Arc.PlaceHero(100, 100, 120);
            var pose = Enter(bench, state);
            pose.Release = true;
            bench.Tick();
            Assert.Equal(0u, bench.Hero.ItemInUse);
            Assert.Equal(0x2Du, bench.Hero.TargetAnimationId);
        }
    }

    /// <summary>0x34 holds without a condition (it only calls TryUseItem), 26 ticks (the animation 52 of the export) then 0; the state does not look at the object in progress, unlike 0x32.</summary>
    [Theory]
    [InlineData(Tome)]
    [InlineData(0u)]
    public void T3a_TheEnd0x34_IsHeld26Ticks_ThenIdle_WhateverTheObjectInProgress(uint inUse)
    {
        using var bench = new TomeBench("t3a-end-" + inUse, 4, 4);
        Enter(bench, End, inUse);

        var states = Run(bench, 40);

        var firstIdle = states.IndexOf(0u) + 1;
        Assert.Equal(EndTicks, firstIdle);
        Assert.All(states.Take(firstIdle - 1), s => Assert.Equal(End, s));
        Assert.Equal(inUse, bench.Hero.ItemInUse);
    }

    /// <summary>The 1-based tick at which Idle shows when 0x34 is posed before the tick 1: the ticks 1 to 26 are the 26 ticks of the animation 52.</summary>
    private const int EndTicks = 27;

    /// <summary>The control of the pair above: the same object freed leaves 0x32 at once but not 0x34.</summary>
    [Fact]
    public void T3a_TheEnd0x34_DoesNotLeaveWhenTheObjectIsFreed_The0x32DoesAtOnce()
    {
        using var bench = new TomeBench("t3a-end-free", 4, 4);
        var pose = Enter(bench, End);
        pose.Release = true;
        bench.Tick();
        Assert.Equal(End, bench.Hero.TargetAnimationId);
        Assert.Equal(0u, bench.Hero.ItemInUse);

        Enter(bench, Cast, 0);
        bench.Tick();
        Assert.Equal(0u, bench.Hero.TargetAnimationId);
    }

    /// <summary>The hero does not move and the pad is not read in the three states (no direction, no Square, no Cross); in Idle the same pad moves him.</summary>
    [Theory]
    [InlineData(Cast)]
    [InlineData(Hold)]
    [InlineData(End)]
    public void T3a_TheCastStates_DoNotMoveTheHero_NorReadThePad_IdleDoes(uint state)
    {
        using var bench = new TomeBench("t3a-pad-" + state, 4, 4);
        Enter(bench, state);
        var x = bench.Hero.PosX;
        var y = bench.Hero.PosY;
        var direction = bench.Hero.TargetDirection;

        var states = Run(bench, 8, PadState.Right | PadState.Up);
        states.AddRange(Run(bench, 1, PadState.Cross));
        states.AddRange(Run(bench, 1, PadState.Square));
        states.AddRange(Run(bench, 1, PadState.Triangle));

        Assert.All(states, s => Assert.Equal(state, s));
        Assert.Equal(x, bench.Hero.PosX);
        Assert.Equal(y, bench.Hero.PosY);
        Assert.Equal(direction, bench.Hero.TargetDirection);

        // the control: Idle with the same pad walks.
        Enter(bench, 0, 0);
        Run(bench, 8, PadState.Right | PadState.Up);
        Assert.NotEqual(x, bench.Hero.PosX);
    }

    // -----------------------------------------------------------------------------------------
    // (t3b) the Circle during a tome.
    // -----------------------------------------------------------------------------------------

    /// <summary>With the start of the tomes installed, a Circle in the cast on the same object goes through the frame (the same object) and the start refuses it (an object is in progress): the sound 3, nothing changes. The same Circle in Idle starts it.</summary>
    [Theory]
    [InlineData(Cast)]
    [InlineData(Hold)]
    [InlineData(End)]
    public void T3b_ACircleDuringTheCast_IsRefusedByTheStart_WithTheSound3_TheSameCircleInIdleStarts(uint state)
    {
        using var bench = new TomeBench("t3b-" + state, 4, 4, (int)Tome);
        var reached = new List<uint>();
        PlayerManager.ItemStartForTests = (hero, item) =>
        {
            reached.Add(item);
            return PlayerManager.TomeStart(hero, item);
        };
        PlayerManager.TomeTrackCallsForTests = new List<(uint, byte)>();
        PlayerManager.SetCurrentItemId(GameState.Instance, Tome);
        Enter(bench, state);

        bench.Tick(TomeBench.Circle);

        Assert.Equal(new[] { Tome }, reached);                  // the frame let the same object through.
        Assert.Equal(new[] { 3 }, bench.Sounds.Requests.ToArray());
        Assert.Equal(state, bench.Hero.TargetAnimationId);
        Assert.Equal(Tome, bench.Hero.ItemInUse);
        Assert.Equal(4, TomeBench.Stats.Mp);

        // the control: nothing in progress, in Idle, the same Circle starts the tome.
        bench.Sounds.Requests.Clear();
        Enter(bench, 0, 0);
        bench.Tick(TomeBench.Circle);
        Assert.Equal(2, reached.Count);
        Assert.Empty(bench.Sounds.Requests);
        Assert.Equal(Tome, bench.Hero.ItemInUse);
        Assert.Equal(3, TomeBench.Stats.Mp);
        Assert.Equal(Cast, bench.Hero.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // (t4) the guard of the slope.
    // -----------------------------------------------------------------------------------------

    /// <summary>The guard <c>0x800351B8</c>: deep water (slope 4) cancels the spell and answers 1; else 0x32 or 0x33 answers 0; else any other animation cancels it and answers 1 (the cancellation is the one of the item in use).</summary>
    [Theory]
    [InlineData(4, 0x32u, 1, true)]
    [InlineData(4, 0x33u, 1, true)]
    [InlineData(4, 0u, 1, true)]
    [InlineData(0, 0x32u, 0, false)]
    [InlineData(0, 0x33u, 0, false)]
    [InlineData(1, 0x33u, 0, false)]
    [InlineData(0, 0x34u, 1, true)]
    [InlineData(0, 0u, 1, true)]
    [InlineData(0, 0x31u, 1, true)]
    [InlineData(0, 0x1Du, 1, true)]
    public void T4_TheGuardOfTheSlope_CancelsInDeepWaterAndOutsideTheCast_AnswersZeroInTheCast(int slope, uint animation, int answer, bool cancelled)
    {
        using var bench = new TomeBench("t4-" + slope + "-" + animation, 4, 4);
        var cancels = new List<(uint Item, uint InUse)>();
        PlayerManager.ItemCancelForTests = (hero, item) => cancels.Add((item, hero.ItemInUse));
        bench.Hero.ItemInUse = Tome;
        bench.Hero.TargetAnimationId = animation;
        bench.Hero.Slope_18c = slope;

        var result = PlayerManager.TomeSlopeGuard(bench.Hero);

        Assert.Equal(answer, result);
        Assert.Equal(cancelled ? new[] { (Tome, Tome) } : Array.Empty<(uint, uint)>(), cancels);
        Assert.Equal(cancelled ? 0u : Tome, bench.Hero.ItemInUse);
    }
}
