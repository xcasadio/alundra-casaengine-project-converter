#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests;

/// <summary>
/// E19.f3c (docs/plan-e19-opcodes.md, D-E19-93; annex docs/plan-e19-save-order-annexe/): the order of the save screen inside
/// <see cref="AlundraWorldProxy.Update"/>, through the REAL proxy. The binary's RenderScene runs the callback dispatcher (slot 3, the
/// choice box) then slot 10 (the file menu, the save screen's question) in the same frame: the screen ticks after the dialogue pass,
/// in the second loop. D-E19-99: on the pad words of the tick BEFORE (the last tick of the previous frame at tick 0), like the pass: slots 0, 3 and 10
/// and the memory-card process all run in RenderScene, before the pad sampler of the frame's Update(0), and read the previous frame's sample. The helpers of the
/// director tests (<c>AlundraSaveScreenDirectorTests</c>) keep presenting their words at age 0 by design: the age is proved here, at the proxy.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveScreenTickOrderTests : IDisposable
{
    private readonly AlundraSaveBookTests.RecordingUIViewRuntime _uiView = new();
    private readonly AlundraSaveScreenDirectorTests.SlotsBySlot _slots = new();
    private readonly AlundraWorldProxy _proxy;

    public AlundraSaveScreenTickOrderTests()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
        AlundraSaveScreenDirector.Instance.ResetForTests();
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        _proxy = new AlundraWorldProxy();
        _proxy.InitializeWithWorld(world);
        _proxy.PlayerEntity = new AlundraEntityScriptProxy();
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        AlundraDialogueDirector.Instance.AttachToWorld(new AlundraDialoguePresenter(_uiView), _proxy.GameState);
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = RealRules;
        AlundraSaveGameDirector.Instance.SaveSlots = _slots;
        AlundraSaveScreenDirector.Instance.AttachToWorld(_proxy.GameState, null);
    }

    public void Dispose()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
        AlundraSaveScreenDirector.Instance.ResetForTests();
    }

    private static AlundraSaveScreenDirector Director => AlundraSaveScreenDirector.Instance;

    private static AlundraDialogueDirector Dialogue => AlundraDialogueDirector.Instance;

    /// <summary>One frame of <paramref name="ticks"/> logic ticks holding <paramref name="hold"/>.</summary>
    private void Frame(int ticks, uint hold = 0)
    {
        _proxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = hold };
        _proxy.Update(0.02f * ticks);
    }

    private void StartAndSettlePicker()
    {
        Assert.True(Director.Start(ValidSave()));
        for (var i = 0; i < 60; i++)
        {
            Frame(1);
        }

        Assert.True(Director.IsPickerActive);
    }

    /// <summary>The scenario of <c>DownDuringTheQuestion</c> through the proxy: Cross at the first tick, read by the screen one tick later (the opener, D-E19-99), the hook armed
    /// after <paramref name="armAfterTicks"/> ticks counted from the OPENER, then the number of FRAMES (of <paramref name="ticksPerFrame"/> ticks) until the screen takes the answer.</summary>
    private int FramesToTheAnswer(int ticksPerFrame, int armAfterTicks, int choice)
    {
        StartAndSettlePicker();
        Frame(1, AlundraPadState.Cross);
        Assert.False(Director.IsQuestionPending); // the screen reads the pad of the tick before: the Cross is read by the next tick
        Frame(1); // the opener
        Assert.True(Director.IsQuestionPending);
        Assert.True(Dialogue.IsAwaitingChoice);
        for (var t = 1; t <= armAfterTicks; t += ticksPerFrame)
        {
            Frame(ticksPerFrame);
        }

        Assert.True(Dialogue.SelectChoiceForTests(choice));
        var frames = 0;
        while (Director.IsQuestionPending && frames < 150)
        {
            Frame(ticksPerFrame);
            frames++;
        }

        return frames;
    }

    // ticksPerFrame, armAfterTicks, choice (0 = OUI, 1 = NON), frames to the answer in the binary's order (the target).
    [Theory]
    [InlineData(1, 0, 0, 37)]
    [InlineData(1, 1, 1, 37)]
    [InlineData(1, 17, 0, 20)]
    [InlineData(1, 18, 0, 19)]
    [InlineData(1, 26, 0, 19)]
    [InlineData(1, 26, 1, 20)]
    [InlineData(2, 0, 0, 19)]
    [InlineData(2, 2, 1, 18)]
    [InlineData(2, 18, 0, 10)]
    [InlineData(2, 26, 0, 10)]
    [InlineData(2, 26, 1, 10)]
    [InlineData(2, 28, 0, 10)]
    public void TheAnswer_IsTakenInTheTickOfTheClosePass_NotOneTickLater(int ticksPerFrame, int armAfterTicks, int choice, int expectedFrames)
    {
        Assert.Equal(expectedFrames, FramesToTheAnswer(ticksPerFrame, armAfterTicks, choice));
    }

    /// <summary>A frame of two ticks holding Cross: the press edge belongs to the FIRST tick only; the screen must still open the question
    /// (a screen ticked with <c>TickPad</c> alone, which holds the last tick's edges, would miss it).</summary>
    [Fact]
    public void TwoTickFrame_CrossHeldFromItsFirstTick_StillOpensTheQuestion()
    {
        StartAndSettlePicker();
        Frame(2, AlundraPadState.Cross);
        Assert.True(Director.IsQuestionPending);
        Assert.True(Dialogue.IsAwaitingChoice);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MultiTickFrame_SquareHeldFromItsFirstTick_LeavesTheWaitSquareState(int ticksPerFrame)
    {
        Assert.True(Director.StartFailure());
        var guard = 0;
        while (Director.State != AlundraSaveScreenDirector.StateWaitSquare && guard++ < 400)
        {
            Frame(1);
        }

        Assert.Equal(AlundraSaveScreenDirector.StateWaitSquare, Director.State);
        Frame(ticksPerFrame, AlundraPadState.Square);
        for (var i = 0; i < 400 && Director.State == AlundraSaveScreenDirector.StateWaitSquare; i++)
        {
            Frame(1, AlundraPadState.Square);
        }

        Assert.NotEqual(AlundraSaveScreenDirector.StateWaitSquare, Director.State);
    }

    /// <summary>D-E19-99: the question opens one tick after the Cross whatever the grouping of the ticks into frames: in the next frame when the Cross frame has one tick, in the same frame
    /// (its tick 1) from two ticks.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void TheCross_IsReadByTheScreenOneTickAfterItsPress(int ticksPerFrame, bool openedInTheCrossFrame)
    {
        StartAndSettlePicker();
        Frame(ticksPerFrame, AlundraPadState.Cross);
        Assert.Equal(openedInTheCrossFrame, Director.IsQuestionPending);
        Frame(1);
        Assert.True(Director.IsQuestionPending);
        Assert.True(Dialogue.IsAwaitingChoice);
    }

    /// <summary>Answers the question with the pad alone, one tick per frame: the Cross that opens it at tick 0, then OUI (Cross at <paramref name="q"/>) or NON (Right at q, Cross at q + 1).
    /// Returns the tick at the end of which the screen has taken the answer (-1: never) and whether the slot was written (OUI).</summary>
    private (int Taken, bool Saved) AnswerByThePad(int q, bool non)
    {
        StartAndSettlePicker();
        var opened = false;
        var taken = -1;
        for (var tick = 0; tick < q + 160; tick++)
        {
            uint hold = 0;
            if (tick == 0)
            {
                hold = AlundraPadState.Cross;
            }
            else if (tick == q)
            {
                hold = non ? AlundraPadState.Right : AlundraPadState.Cross;
            }
            else if (non && tick == q + 1)
            {
                hold = AlundraPadState.Cross;
            }

            Frame(1, hold);
            if (!opened && Director.IsQuestionPending)
            {
                opened = true;
            }
            else if (opened && taken < 0 && !Director.IsQuestionPending)
            {
                taken = tick;
            }
        }

        return (taken, _slots.SaveCalls.Count == 1);
    }

    /// <summary>D-E19-99, the age of the pad word: the binary counts 19 pad samples between the press that opens the question and the earliest press the choice box takes (the screen reads the opening
    /// press one sample late, the choice box opens, slides in for 18 passes, and reads from its 19th pass). A press at the 18th sample is dropped (the box still slides in); the Right of a NON must
    /// also come at the 19th sample or later. Ticks are counted from the opening press (tick 0).</summary>
    [Theory]
    [InlineData(false, 18, -1, false)]
    [InlineData(false, 19, 38, true)]
    [InlineData(false, 20, 39, true)]
    [InlineData(true, 18, 38, true)] // the Right at 18 is dropped: the Cross at 19 answers OUI
    [InlineData(true, 19, 39, false)]
    [InlineData(true, 20, 40, false)]
    public void ThePadAnswer_IsTakenFromTheNineteenthSampleAfterTheOpeningPress(bool non, int q, int expectedTaken, bool expectedSaved)
    {
        Assert.Equal((expectedTaken, expectedSaved), AnswerByThePad(q, non));
    }

    /// <summary>D-E19-99: the Square of the wait state (the binary's state 0x3F5 of the memory-card process, in RenderScene) is read one tick after its press: frames from the press frame until the
    /// screen leaves <see cref="AlundraSaveScreenDirector.StateWaitSquare"/>.</summary>
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 10)]
    [InlineData(3, 6)]
    public void TheSquareOfTheWait_IsReadOneTickAfterItsPress(int ticksPerFrame, int expectedFrames)
    {
        Assert.True(Director.StartFailure());
        var guard = 0;
        while (Director.State != AlundraSaveScreenDirector.StateWaitSquare && guard++ < 400)
        {
            Frame(1);
        }

        Assert.Equal(AlundraSaveScreenDirector.StateWaitSquare, Director.State);
        for (var i = 0; i < 30; i++)
        {
            Frame(1);
        }

        Frame(ticksPerFrame, AlundraPadState.Square);
        Assert.Equal(AlundraSaveScreenDirector.StateWaitSquare, Director.State);
        var frames = 0;
        while (Director.State == AlundraSaveScreenDirector.StateWaitSquare && frames < 80)
        {
            Frame(ticksPerFrame);
            frames++;
        }

        Assert.Equal(expectedFrames, frames);
    }

    /// <summary>D-E19-99: the Down of the picker (the binary's slot 10, by-interval word) is read one tick after its press: the selection moves in the next frame with one tick per frame, in the
    /// same frame (its tick 1) with two.</summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void TheDownOfThePicker_IsReadOneTickAfterItsPress(int ticksPerFrame, int selectionAfterThePressFrame)
    {
        StartAndSettlePicker();
        Assert.Equal(0, Director.Selection);
        Frame(ticksPerFrame, AlundraPadState.Down);
        Assert.Equal(selectionAfterThePressFrame, Director.Selection);
        Frame(ticksPerFrame);
        Assert.Equal(1, Director.Selection);
    }

    private sealed class CountingRunner : IEventProgramRunner
    {
        public int ScriptRuns;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => ScriptRuns++;

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>The screen ticks BEFORE the map events of its tick (the freeze of MenuOpen, posted by the screen's first tick, applies in the same
    /// tick): a map event that ran on the frame before does not run on the frame the screen starts, with 1 or 2 ticks.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheScreenStarted_FreezesTheMapEventsOfTheSameFrame(int ticksPerFrame)
    {
        var hero = HeroAt();
        hero.Status = EntityStatus.Normal;
        _proxy.PlayerEntity = hero;
        var record = new TileMapObjectData();
        record.CustomProperties["EventCodesBIndex"] = "129";
        record.CustomProperties["Index"] = "1";
        record.CustomProperties["X1"] = "0";
        record.CustomProperties["Y1"] = "0";
        record.CustomProperties["X2"] = "100";
        record.CustomProperties["Y2"] = "100";
        var layer = new TileMapObjectLayerData();
        layer.Objects.Add(record);
        _proxy.BuildMapEvents(layer);
        var runner = new CountingRunner();
        _proxy.EventProgramRunner = runner;

        Frame(ticksPerFrame);
        var before = runner.ScriptRuns;
        Assert.True(before > 0); // the event runs on a frame without the screen.

        Assert.True(Director.Start(ValidSave()));
        Frame(ticksPerFrame);

        Assert.Equal(before, runner.ScriptRuns);
    }
}
