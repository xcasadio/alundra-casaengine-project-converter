using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.d D1 (docs/plan-e13d-inventaire.md, D-E13D-8, D-E13D-9): <see cref="AlundraTickPad"/>, the port of
/// <c>PadManager.UpdatePad</c> (PadManager.cs:27-74) advanced once per logic tick. The expected sequences
/// come from tracing the original function by hand with its own constants, <c>MaxNbFrameHeld</c> = 20
/// (PadState.cs:25) and <c>RepeatInterval</c> = 0 (GameInitializer.cs:169): an edge on the tick of the
/// press, 20 silent ticks, then an edge on every tick while the state holds.
/// </summary>
public class AlundraTickPadTests
{
    private const uint Right = AlundraPadState.Right;
    private const uint Cross = AlundraPadState.Cross;

    private static List<uint> RunTicks(AlundraTickPad pad, uint hold, int ticks)
    {
        var edges = new List<uint>(ticks);
        for (var i = 0; i < ticks; i++)
        {
            pad.Update(hold);
            edges.Add(pad.ButtonsJustPressedByInterval);
        }

        return edges;
    }

    [Fact]
    public void Constants_AreTheOriginals()
    {
        var pad = new AlundraTickPad();

        Assert.Equal(20u, AlundraTickPad.MaxNbFrameHeld);
        Assert.Equal(0u, pad.RepeatInterval);
    }

    [Fact]
    public void HeldButton_EdgeOnThePressTick_TwentySilentTicks_ThenAnEdgeEveryTick()
    {
        var pad = new AlundraTickPad();

        var edges = RunTicks(pad, Right, 30);

        Assert.Equal(Right, edges[0]);
        Assert.All(edges.Skip(1).Take(20), edge => Assert.Equal(0u, edge));
        Assert.All(edges.Skip(21), edge => Assert.Equal(Right, edge));
    }

    [Fact]
    public void PressEdge_IsReportedOnlyOnThePressTick()
    {
        var pad = new AlundraTickPad();

        pad.Update(Right);
        Assert.Equal(Right, pad.ButtonsJustPressed);

        pad.Update(Right);
        Assert.Equal(0u, pad.ButtonsJustPressed);
    }

    [Fact]
    public void Release_ReportsTheReleasedButton_AndGivesNoEdge()
    {
        var pad = new AlundraTickPad();
        RunTicks(pad, Right, 25);

        pad.Update(0);

        Assert.Equal(Right, pad.ButtonsReleased);
        Assert.Equal(0u, pad.ButtonsJustPressed);
        Assert.Equal(0u, pad.ButtonsJustPressedByInterval);
        Assert.Equal(0u, pad.NumberOfFrameHold);
        Assert.Equal(0u, pad.IsOverThanMaxNbFrameHeld);
    }

    [Fact]
    public void ChangeOfState_RestartsTheDelay_AndReportsOnlyTheNewButton()
    {
        var pad = new AlundraTickPad();
        RunTicks(pad, Right, 25); // already repeating

        var edges = RunTicks(pad, Right | Cross, 22);

        // PadManager.cs:34-39: a changed state resets the counters and reports only what went down.
        Assert.Equal(Cross, edges[0]);
        Assert.All(edges.Skip(1).Take(20), edge => Assert.Equal(0u, edge));
        // PadManager.cs:69-70: a repeat reports the WHOLE held state, not just the last button.
        Assert.Equal(Right | Cross, edges[21]);
    }

    [Fact]
    public void NothingHeld_NeverReportsAnything()
    {
        var pad = new AlundraTickPad();

        var edges = RunTicks(pad, 0, 40);

        Assert.All(edges, edge => Assert.Equal(0u, edge));
        Assert.Equal(0u, pad.NumberOfFrameHold);
    }

    /// <summary>
    /// The point of D-E13D-9: the same held press, sampled once per rendered frame, gives the same edges
    /// per LOGIC tick whatever the number of ticks each rendered frame carries - including frames with zero
    /// and with two ticks. The pass mirrors AlundraWorldProxy.Update's own per-tick pad loop: each frame
    /// samples one hold state, then runs its ticks.
    /// </summary>
    [Theory]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 2, 0 })]
    [InlineData(new[] { 0, 1, 2 })]
    [InlineData(new[] { 3, 0, 0 })]
    public void HeldPress_GivesTheSameEdgesPerTick_WhateverTheTicksPerFrame(int[] ticksPattern)
    {
        const int totalTicks = 30;
        var pad = new AlundraTickPad();
        var edges = new List<uint>();

        for (var frame = 0; edges.Count < totalTicks; frame++)
        {
            var ticksThisFrame = ticksPattern[frame % ticksPattern.Length];
            for (var tick = 0; tick < ticksThisFrame && edges.Count < totalTicks; tick++)
            {
                pad.Update(Right);
                edges.Add(pad.ButtonsJustPressedByInterval);
            }
        }

        Assert.Single(edges.Take(21), edge => edge != 0);
        Assert.Equal(Right, edges[0]);
        Assert.All(edges.Skip(21), edge => Assert.Equal(Right, edge));
    }

    [Fact]
    public void PressSampledOnAZeroTickFrame_IsNotLost_WhileItIsStillHeldAtTheNextTick()
    {
        var pad = new AlundraTickPad();
        var edges = new List<uint>();

        // Frame 1: nothing held, one tick. Frame 2: the press is sampled, but the frame carries no tick.
        // Frame 3: still held, one tick - the edge arrives here, once.
        foreach (var (hold, ticks) in new[] { (0u, 1), (Right, 0), (Right, 1), (Right, 1) })
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                pad.Update(hold);
                edges.Add(pad.ButtonsJustPressedByInterval);
            }
        }

        Assert.Equal(new[] { 0u, Right, 0u }, edges);
    }

    [Fact]
    public void Reset_ReturnsToTheZeroInitialisedState()
    {
        var pad = new AlundraTickPad();
        RunTicks(pad, Right, 25);

        pad.Reset();

        Assert.Equal(0u, pad.ButtonsHold);
        Assert.Equal(0u, pad.ButtonsJustPressed);
        Assert.Equal(0u, pad.ButtonsReleased);
        Assert.Equal(0u, pad.ButtonsJustPressedByInterval);
        Assert.Equal(0u, pad.NumberOfFrameHold);
        Assert.Equal(0u, pad.IsOverThanMaxNbFrameHeld);
    }
}
