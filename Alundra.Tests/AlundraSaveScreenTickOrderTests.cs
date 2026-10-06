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
/// in the second loop, on the pad words of its own tick.
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

    /// <summary>The scenario of <c>DownDuringTheQuestion</c> through the proxy: Cross at the first tick (the opener), the hook armed after
    /// <paramref name="armAfterTicks"/> ticks, then the number of FRAMES (of <paramref name="ticksPerFrame"/> ticks) until the screen takes the answer.</summary>
    private int FramesToTheAnswer(int ticksPerFrame, int armAfterTicks, int choice)
    {
        StartAndSettlePicker();
        Frame(1, AlundraPadState.Cross);
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
