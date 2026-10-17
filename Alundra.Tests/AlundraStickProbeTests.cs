using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Alundra.Scripts;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Input;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rendering.Effects;

namespace Alundra.Tests;

/// <summary>
/// E20.d2a (docs/plan-e20-heros.md, D-E20-32): the stick sample travels in the pad (S1) and the probe journals it (S3). <c>ComputePadState</c> is fed by a fake <c>getButtonState</c> that follows the engine's
/// semantics (<c>MoveLeftStick.Value = -MoveRightStick.Value</c>, <c>MoveDownStick.Value = -MoveUpStick.Value</c>, pressed only from the 0.5 dead zone); the tick pad carries the frame's sample in the
/// self-driven mode (here) and in the entity block of a driven world (<see cref="AlundraTickStructureTests"/>); the probe writes nothing without the variable and the written format with it.
/// </summary>
public sealed class AlundraStickProbeTests : IDisposable
{
    private const uint DirBits = AlundraPadState.Up | AlundraPadState.Right | AlundraPadState.Down | AlundraPadState.Left;

    public AlundraStickProbeTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraStickProbe.SetForTests(null, null);
        AlundraEntityScriptProxy.PadOfTickSeenForTests = null;
        AlundraNativeEffects.CreatedForTests = null;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static readonly string[] AllActions =
    {
        "MoveUp", "MoveDown", "MoveLeft", "MoveRight", "MoveUpStick", "MoveDownStick", "MoveRightStick", "MoveLeftStick",
        "Jump", "Attack", "UseItem", "Sprint", "Menu", "L1", "L2", "R1", "R2", "Select",
    };

    private static InputMappingManager Manager()
    {
        var manager = new InputMappingManager();
        foreach (var name in AllActions)
        {
            manager.AddInputMapping(new InputMapping { Name = name });
        }

        return manager;
    }

    /// <summary>The engine's analog semantics for a stick pushed to (x, y), x positive right and y positive up: each half of an axis reads the same axis, inverted; pressed from 0.5 (the DeadZone of the mapping).</summary>
    public static Func<string, ButtonState> FakeStick(float x, float y, params string[] digitalHeld) => name =>
    {
        float? axis = name switch
        {
            "MoveRightStick" => x,
            "MoveLeftStick" => -x,
            "MoveUpStick" => y,
            "MoveDownStick" => -y,
            _ => null,
        };

        return axis is { } v
            ? new ButtonState { Value = v, IsKeyPressed = v >= 0.5f }
            : new ButtonState { IsKeyPressed = digitalHeld.Contains(name) };
    };

    // -----------------------------------------------------------------------------------------
    // S1 - ComputePadState with analog values.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputePadState_A45DegreePush_SetsBothStickBits_TheSampleKeepsBothAxes_AndTheOrIsUnchanged()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(0.7071f, 0.7071f));

        Assert.Equal(AlundraPadState.Up | AlundraPadState.Right, s.StickBitsHold);
        Assert.Equal(0u, s.DigitalDirectionBitsHold);
        Assert.Equal(AlundraPadState.Up | AlundraPadState.Right, s.ButtonsHold);
        Assert.Equal(0.7071f, s.StickX, 4);
        Assert.Equal(0.7071f, s.StickY, 4);
        Assert.Equal(Math.Abs(s.StickX), Math.Abs(s.StickY), 4);
    }

    [Fact]
    public void ComputePadState_HalfWayUnderTheDeadZone_LogsXAndY_ButSetsNoStickBit()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(0.4f, 0.3f));

        Assert.Equal(0u, s.StickBitsHold);
        Assert.Equal(0u, s.DigitalDirectionBitsHold);
        Assert.Equal(0u, s.ButtonsHold & DirBits);
        Assert.Equal(0.4f, s.StickX, 4);
        Assert.Equal(0.3f, s.StickY, 4);
    }

    [Fact]
    public void ComputePadState_TheNegativeQuadrant_GivesNegativeSamples_AndLeftDownBits()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(-0.6f, -0.8f));

        Assert.Equal(AlundraPadState.Left | AlundraPadState.Down, s.StickBitsHold);
        Assert.Equal(AlundraPadState.Left | AlundraPadState.Down, s.ButtonsHold);
        Assert.Equal(-0.6f, s.StickX, 4);
        Assert.Equal(-0.8f, s.StickY, 4);
    }

    [Fact]
    public void ComputePadState_FullRight_GivesOne_NotTwo()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(1f, 0f));

        Assert.Equal(1f, s.StickX); // MoveRightStick.Value alone: the half-axis difference (1 - (-1)) would give 2.
        Assert.Equal(0f, s.StickY);
        Assert.Equal(AlundraPadState.Right, s.StickBitsHold);
    }

    [Fact]
    public void ComputePadState_FullDownLeft_GivesMinusOneAndMinusOne()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(-1f, -1f));

        Assert.Equal(-1f, s.StickX);
        Assert.Equal(-1f, s.StickY);
    }

    [Fact]
    public void ComputePadState_KeyboardAndDPad_AreDNotS_AndTheStickSampleStaysZero()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(0f, 0f, "MoveUp", "MoveRight", "Sprint"));

        Assert.Equal(AlundraPadState.Up | AlundraPadState.Right, s.DigitalDirectionBitsHold);
        Assert.Equal(0u, s.StickBitsHold);
        Assert.Equal(AlundraPadState.Up | AlundraPadState.Right | AlundraPadState.Triangle, s.ButtonsHold);
        Assert.Equal(0f, s.StickX);
        Assert.Equal(0f, s.StickY);
    }

    [Fact]
    public void ComputePadState_KeyboardAndStickTogether_KeepTheOrInButtonsHold_AndEachItsOwnBits()
    {
        var s = AlundraPlayerController.ComputePadState(Manager(), FakeStick(-1f, 0f, "MoveUp"));

        Assert.Equal(AlundraPadState.Up, s.DigitalDirectionBitsHold);
        Assert.Equal(AlundraPadState.Left, s.StickBitsHold);
        Assert.Equal(AlundraPadState.Up | AlundraPadState.Left, s.ButtonsHold);
    }

    [Fact]
    public void AnInitializerThatDoesNotKnowTheNewFields_KeepsTheirMeaning_ZeroByDefault()
    {
        var s = new AlundraPadState { ButtonsHold = AlundraPadState.Right };

        Assert.Equal(0u, s.DigitalDirectionBitsHold);
        Assert.Equal(0u, s.StickBitsHold);
        Assert.Equal(0f, s.StickX);
        Assert.Equal(0f, s.StickY);
    }

    // -----------------------------------------------------------------------------------------
    // S1 - the pad of the tick carries the frame's sample.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void FromTickPad_CopiesTheFramesSample_AndTheTickPadItselfIsUnchanged()
    {
        var tickPad = new AlundraTickPad();
        tickPad.Update(AlundraPadState.Right | AlundraPadState.Triangle);
        var sample = new AlundraPadState { ButtonsHold = AlundraPadState.Up, DigitalDirectionBitsHold = AlundraPadState.Up, StickBitsHold = AlundraPadState.Left, StickX = -0.9f, StickY = 0.2f };

        var pad = AlundraPadState.FromTickPad(tickPad, in sample);

        Assert.Equal(tickPad.ButtonsHold, pad.ButtonsHold); // the hold of the tick pad, not the sample's.
        Assert.Equal(tickPad.ButtonsJustPressed, pad.ButtonsJustPressed);
        Assert.Equal(tickPad.ButtonsReleased, pad.ButtonsReleased);
        Assert.Equal(AlundraPadState.Up, pad.DigitalDirectionBitsHold);
        Assert.Equal(AlundraPadState.Left, pad.StickBitsHold);
        Assert.Equal(-0.9f, pad.StickX);
        Assert.Equal(0.2f, pad.StickY);

        var bare = AlundraPadState.FromTickPad(tickPad);
        Assert.Equal(0u, bare.StickBitsHold);
        Assert.Equal(0f, bare.StickX);
    }

    [Fact]
    public void TheSelfDrivenMode_HandsTheFramesSampleToMovePlayer_AtEachTick()
    {
        var montage = RunMontage.Start(RunOracleTrace.Load("g1/R1-T+R"));
        var controller = montage.Rig.Host.PlayerController!;
        var seen = new List<AlundraPadState>();
        AlundraEntityScriptProxy.PadOfTickSeenForTests = seen.Add;
        controller.PadStateProviderForTests = () => new AlundraPadState
        {
            ButtonsHold = AlundraPadState.Right, StickBitsHold = AlundraPadState.Right, StickX = 0.8f, StickY = -0.25f,
        };

        montage.Rig.Step(AlundraPadState.Right, elapsed: 0.02f);
        montage.Rig.Step(AlundraPadState.Right, elapsed: 0.04f); // a frame of two ticks: both carry the sample.

        Assert.Equal(3, seen.Count);
        Assert.All(seen, p =>
        {
            Assert.Equal(0.8f, p.StickX);
            Assert.Equal(-0.25f, p.StickY);
            Assert.Equal(AlundraPadState.Right, p.StickBitsHold);
        });
    }

    // -----------------------------------------------------------------------------------------
    // S3 - the probe.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TheProbe_WritesTheWrittenFormat_WithTheInvariantDecimalPoint_AndHexBits()
    {
        var lines = new List<string>();
        AlundraStickProbe.SetForTests(true, lines.Add);
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            AlundraStickProbe.LogFrame(new AlundraPadState { StickX = 0.7071f, StickY = 0.7071f, StickBitsHold = AlundraPadState.Up | AlundraPadState.Right });
            AlundraStickProbe.LogFrame(new AlundraPadState { StickX = -0.6f, StickY = -0.8f, DigitalDirectionBitsHold = AlundraPadState.Left, StickBitsHold = AlundraPadState.Left | AlundraPadState.Down });
            AlundraStickProbe.LogStartTableRead(0x29, 0x2);
            AlundraStickProbe.LogStartTableRead(0x2A, 0xA);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }

        Assert.Equal(
            new[] { "StickProbe 0.7071 0.7071 0 3000", "StickProbe -0.6000 -0.8000 8000 C000", "StickProbe start 29 2", "StickProbe start 2A A" },
            lines);
    }

    [Fact]
    public void WithoutTheVariable_NothingIsJournalled_EvenOverAFullRun()
    {
        var lines = new List<string>();
        AlundraStickProbe.SetForTests(false, lines.Add);
        AlundraStickProbe.LogFrame(new AlundraPadState { StickX = 1f });
        AlundraStickProbe.LogStartTableRead(0x29, 2);

        var montage = RunMontage.Start(RunOracleTrace.Load("g1/R1-T+R"));
        for (var t = 1; t <= 30; t++)
        {
            montage.Tick((uint)montage.Trace.Hold[t]);
        }

        Assert.Empty(lines);
        Assert.Equal(0x03u, montage.Hero.TargetAnimationId); // the run started: the table was read, silently.
    }

    [Fact]
    public void WithTheVariable_ARunJournalsOneLinePerFrame_AndOneLineAtEachReadOfTheStartTable()
    {
        var lines = new List<string>();
        AlundraStickProbe.SetForTests(true, lines.Add);
        var montage = RunMontage.Start(RunOracleTrace.Load("g1/R1-T+R"));
        lines.Clear(); // the settling steps of the montage's start are frames too.
        var firstStartLine = -1;
        var frames = 0;
        for (var t = 1; t <= 30; t++)
        {
            montage.Tick((uint)montage.Trace.Hold[t]);
            frames++;
            if (firstStartLine < 0 && lines.Any(l => l.StartsWith("StickProbe start", StringComparison.Ordinal)))
            {
                firstStartLine = frames;
            }
        }

        var frameLines = lines.Where(l => !l.StartsWith("StickProbe start", StringComparison.Ordinal)).ToList();
        Assert.Equal(30, frameLines.Count);
        Assert.All(frameLines, l => Assert.Equal("StickProbe 0.0000 0.0000 0 0", l)); // the montage's pad carries no stick sample.
        Assert.Equal(26, firstStartLine); // the preparation lasts 25 ticks: the table is first read at the 26th.
        Assert.Contains("StickProbe start 29 2", lines); // 0x29, the nibble of Triangle + Right (ButtonsHold >> 12).
    }

    [Fact]
    public void WithTheVariable_TheResumeInTheSlideReadsTheTableToo_StateTwoA()
    {
        var lines = new List<string>();
        AlundraStickProbe.SetForTests(true, lines.Add);
        var montage = RunMontage.Start(RunOracleTrace.Load("g4/R4-rel12-R"));
        for (var t = 1; t <= montage.Trace.Ticks; t++)
        {
            montage.Tick((uint)montage.Trace.Hold[t]);
        }

        Assert.Contains("StickProbe start 29 2", lines);
        Assert.Contains("StickProbe start 2A 2", lines);
    }
}
