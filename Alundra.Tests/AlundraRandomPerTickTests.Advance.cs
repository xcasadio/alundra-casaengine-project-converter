#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.e0b1 T2, the tests of the advance itself (red until phase B, T3): (a) the six words of the advance, (c) the world drives one step per logic tick, (d) the order in the tick. Every test here expects at least one
/// REAL step: written red on the phase A code (where <see cref="GameRandom.AdvanceFrame"/> only feeds the virtual word of the measuring sink), green once phase B makes it write the real state. Each expected word is
/// computed by a step written locally (<see cref="Step"/>) from the seed and the counts of ticks and draws, never pasted.
/// </summary>
public sealed partial class AlundraRandomPerTickTests
{
    private const uint Square = PadState.Square;

    // ---------------------------------------------------------------------------------------------------------------------
    // (a) the six words of the advance (0x8002BD0C-0x8002BD34, the oracle's advance_words.json)
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x00000000u, 1, 0xE06A02E7u)]
    [InlineData(0xFFFFFFFFu, 1, 0x633E790Au)]
    [InlineData(0x80000000u, 1, 0x606A02E7u)]
    [InlineData(0xB017C93Du, 1, 0x35E36190u)]
    [InlineData(0x12345678u, 1, 0xB806E07Fu)]
    [InlineData(0x12345678u, 4, 0xAFBE9EACu)]
    public void AdvanceFrame_StepsTheStreamOncePerCall_ToTheWordsOfTheBinary(uint seed, int calls, uint expected)
    {
        GameRandom.RandomSeed = seed;

        for (var i = 0; i < calls; i++)
        {
            GameRandom.AdvanceFrame();
        }

        Assert.Equal(expected, Word);
        Assert.Equal(StepN(seed, calls), Word); // the local step agrees with the binary's words.
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // (c) the world: one step per logic tick
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The headless world of the effect-stage tests: a proxy attached to a world without game (no hero, no entity: nothing draws but the advance).</summary>
    private static WorldProxy HeadlessProxy()
    {
        var world = new World { Name = "TestWorld" };
        world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        return proxy;
    }

    private static WorldProxy WarmProxy()
    {
        var proxy = HeadlessProxy();
        proxy.Update(0.02f); // the first frame of a world: the sticky floor of one tick.
        return proxy;
    }

    [Fact]
    public void AWorld_StepsTheStreamOncePerTick_OneTickThenTwo()
    {
        const uint seed = 0x12345678;
        var proxy = WarmProxy();
        GameRandom.RandomSeed = seed;

        proxy.Update(0.02f);
        Assert.Equal(StepN(seed, 1), Word);

        proxy.Update(0.04f);
        Assert.Equal(StepN(seed, 3), Word);
    }

    [Fact]
    public void AWorld_UnderTheMenuGate0x48_StillStepsOncePerTick_AsTheBinarysFrameFunctionDoes()
    {
        const uint seed = 0x0BADF00D;
        var proxy = WarmProxy();
        proxy.GameState.PlayerControlFlags = GameState.PlayerControlBits.MenuOpen;
        GameRandom.RandomSeed = seed;

        proxy.Update(0.04f);
        proxy.Update(0.02f);

        Assert.Equal(StepN(seed, 3), Word);
    }

    [Fact]
    public void TheFirstFrameOfAWorld_CountsAtLeastOneTick_EvenWithNoElapsedTime()
    {
        const uint seed = 0xCAFEBABE;
        var proxy = HeadlessProxy();
        GameRandom.RandomSeed = seed;

        proxy.Update(0.0f);
        Assert.Equal(StepN(seed, 1), Word); // the floor: one tick, one step, like the post-load call a0 = 1 of the binary.

        proxy.Update(0.0f);
        Assert.Equal(StepN(seed, 1), Word); // the second frame has no tick: no step.
    }

    [Fact]
    public void TwoWorldsInARow_StepTheStreamOncePerTickEach_WithoutCountingAnyTickTwice()
    {
        const uint seed = 0x13579BDF;
        GameRandom.RandomSeed = seed;

        var first = HeadlessProxy();
        first.Update(0.02f); // its first frame: 1 tick.
        first.Update(0.02f); // 1 tick.
        Assert.Equal(StepN(seed, 2), Word);

        var second = HeadlessProxy();
        second.Update(0.02f); // the first frame of the second world: 1 tick (its own floor), not 0 and not more.
        Assert.Equal(StepN(seed, 3), Word);
    }

    /// <summary>The "0 step" case never stands alone: a frame without tick, then a frame of one tick, make exactly ONE step in all.</summary>
    [Fact]
    public void AFrameWithoutTick_ThenAFrameOfOneTick_MakeExactlyOneStepInAll()
    {
        const uint seed = 0xA5A5A5A5;
        var proxy = WarmProxy();
        GameRandom.RandomSeed = seed;

        proxy.Update(0.0f);
        proxy.Update(0.02f);

        Assert.Equal(StepN(seed, 1), Word);
    }

    /// <summary>The other "0 step" case, the departure of a warp (the stage receives 0 tick), between two normal frames of one tick: exactly TWO steps in all.</summary>
    [Fact]
    public void AFrameThatDepartsToAWarp_BetweenTwoFramesOfOneTick_MakeExactlyTwoStepsInAll()
    {
        const uint seed = 0x5A5A5A5A;
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        BackdropWarpDepartureSupport.SetPrivateField(proxy._backdropStage, "_clearColorApplied", true); // the headless montage has no view for the clear colour.
        proxy.Update(0.02f); // the priming frame.
        GameRandom.RandomSeed = seed;

        proxy.Update(0.02f); // a normal frame of one tick: step 1.
        Assert.Equal(StepN(seed, 1), Word);

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);
        for (var frame = 0; frame < 3; frame++)
        {
            proxy.Update(0.02f);
            Assert.True(WarpDirector.Instance.IsTransitionInProgress, $"F{frame}");
            Assert.Equal(StepN(seed, 1), Word); // the departure frames give the stage 0 tick: no step.
        }

        GC.KeepAlive(player);
        WarpDirector.Instance.InstallForMapEntry();
        proxy.Update(0.02f); // the first normal frame after the departure: step 2.

        Assert.Equal(StepN(seed, 2), Word);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // (d) the order in the tick: the charge sparks draw BEFORE the advance of their tick
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The steel sword held 90 ticks on the real world of map 14 (<see cref="HitArena"/>, the scenario of <c>AlundraHeroChargeArenaTests</c>): the hold sparks of the cadence of 4 (measured: ticks 83, 87 and 91 of the scenario, the
    /// last one on the tick of the release, which also creates the burst of 16 effects, no number drawn) each draw two numbers when the stage applies their operation, at their tick, BEFORE the advance of that tick (the advance ends
    /// the tick). So, from a seed set after the construction, the stream stands after the second draw of the spark of tick T, the k-th, at (T - 1) advances + 2k draws, and at the end of the 96 ticks at 96 advances + 6 draws;
    /// the same in frames of 1, 2 and 3 ticks (the grouping changes neither the order of the draws nor that of the advances).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheChargeSparks_DrawBeforeTheAdvanceOfTheirTick_WhateverTheGrouping(int ticksPerFrame)
    {
        const uint seed = 0x12345678;
        var sparkTicks = new[] { 83, 87, 91 };
        var previousProject = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        using var arena = new HitArena("e0b1-" + ticksPerFrame, 212, 96, new List<int[]> { new[] { 212 + 100, 96 } });
        try
        {
            arena.Arc.Give(2);
            arena.Arc.Equip(1);
            GameRandom.RandomSeed = seed;
            var wordAfterCreation = new List<uint>();
            var tickOfCreation = new List<int>();
            var tick = 0;
            arena.BeforeTick = t => tick = t;
            NativeEffects.CreatedForTests = _ =>
            {
                wordAfterCreation.Add(Word);
                tickOfCreation.Add(tick);
            };

            arena.Begin();
            for (var t = 1; t <= 96; t += ticksPerFrame)
            {
                arena.Frame(t <= 90 ? Square : 0, ticksPerFrame);
            }

            Assert.Equal(96, arena.Samples.Count);
            Assert.True(wordAfterCreation.Count == 19, "creations: " + wordAfterCreation.Count + " at ticks " + string.Join(",", tickOfCreation)); // three hold sparks, then the burst of 16 effects.
            if (ticksPerFrame == 1)
            {
                Assert.Equal(sparkTicks, tickOfCreation.GetRange(0, 3)); // the tick of the operation is only readable at one tick per frame.
            }

            for (var k = 0; k < sparkTicks.Length; k++)
            {
                Assert.Equal(StepN(seed, (sparkTicks[k] - 1) + (2 * (k + 1))), wordAfterCreation[k]);
            }

            Assert.Equal(StepN(seed, 96 + 6), Word);
        }
        finally
        {
            NativeEffects.CreatedForTests = null;
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProject;
        }
    }
}
