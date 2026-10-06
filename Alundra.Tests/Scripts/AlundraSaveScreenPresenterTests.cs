#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.SaveGames;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests.Scripts;

/// <summary>
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5): <see cref="AlundraSaveScreen"/> is not constructible headless (it
/// needs the game's asset manager and fonts), so this drives <see cref="AlundraSaveScreenPresenter"/> over the real
/// <see cref="AlundraSaveScreenDirector"/>, a real <see cref="AlundraSaveScreenViewModel"/>, a recording
/// <see cref="IUIScreen"/> stand-in on <see cref="AlundraSaveScreen.ScreenLayer"/>, and ONE recording
/// <see cref="IUIViewRuntime"/> shared with the real dialogue presenter - so the order of the two screens on the
/// view is observed. The save service is a fake (D-E16-31).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveScreenPresenterTests : IDisposable
{
    private readonly AlundraSaveBookTests.RecordingUIViewRuntime _uiView = new();
    private readonly AlundraDialoguePresenter _dialoguePresenter;
    private readonly FakeSaveScreen _screen = new();
    private readonly FakeSaveScreen _choiceScreen = new();
    private readonly AlundraSaveScreenViewModel _viewModel = new();
    private readonly AlundraSaveScreenPresenter _presenter;
    private readonly AlundraChoicePresenter _choicePresenter;
    private readonly AlundraSaveScreenDirectorTests.SlotsBySlot _slots = new();

    public AlundraSaveScreenPresenterTests()
    {
        ResetAll();
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        _dialoguePresenter = new AlundraDialoguePresenter(_uiView);
        AlundraDialogueDirector.Instance.AttachToWorld(_dialoguePresenter, State);
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = RealRules;
        AlundraSaveGameDirector.Instance.SaveSlots = _slots;
        Director.AttachToWorld(State, null);
        _presenter = new AlundraSaveScreenPresenter(Director, _viewModel, _screen, _uiView);
        _choicePresenter = new AlundraChoicePresenter(AlundraDialogueDirector.Instance, new AlundraChoiceViewModel(), _choiceScreen, _uiView); // E19.f3b: on the same view
    }

    public void Dispose() => ResetAll();

    private static void ResetAll()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
    }

    private static AlundraGameState State => AlundraGameState.Instance;

    private static AlundraSaveScreenDirector Director => AlundraSaveScreenDirector.Instance;

    private void Tick(int count = 1, uint hold = 0)
    {
        for (var i = 0; i < count; i++)
        {
            State.TickPad.Update(hold);
            AlundraDialogueDirector.Instance.Tick(); // E19.f3c (D-E19-93): the pass of the dialogue director (its choice box) first, the order of the world proxy
            _choicePresenter.Tick(); // E19.f3b: right after the pass, as the choice screen is pushed above the save screen's
            Director.Tick();
            _presenter.Tick();
        }
    }

    private void TickUntilIdle()
    {
        for (var i = 0; i < 600 && Director.IsActive; i++)
        {
            Tick();
        }

        Tick();
    }

    [Fact]
    public void Screen_IsPushedAtActivation_AndRemovedAtTheEnd()
    {
        Tick(5);
        Assert.Empty(_uiView.Pushed);

        Assert.True(Director.StartFailure());
        Tick();
        Assert.Equal(new IUIScreen[] { _screen }, _uiView.Pushed);
        Assert.Equal(Visibility.Visible, _viewModel.RootVisibility);

        Tick(100);
        Assert.Empty(_uiView.Removed); // the failure message waits for Square.

        Tick(hold: AlundraPadState.Square);
        TickUntilIdle();
        Assert.Equal(new IUIScreen[] { _screen }, _uiView.Removed);
        Assert.False(_presenter.IsPushedForTests);
        Assert.Equal(Visibility.Collapsed, _viewModel.RootVisibility);
    }

    /// <summary>The view model follows the director: positions, tints, texts, and what is not drawn this tick.</summary>
    [Fact]
    public void ViewModel_FollowsTheDirector()
    {
        _slots.Answers["slot1"] = (SaveGameLoadStatus.Loaded, ValidSave());
        Assert.True(Director.Start(ValidSave()));

        Tick();
        Assert.Equal(Visibility.Visible, _viewModel.MessageBox.Visibility);
        Assert.Equal((16, 240), (_viewModel.MessageBox.Left, _viewModel.MessageBox.Top));
        Assert.Equal("Examen de la Carte Mémoire . . .", _viewModel.MessageLine0.Text);
        Assert.Equal((32, 252), (_viewModel.MessageLine0.Left, _viewModel.MessageLine0.Top));

        Tick(59); // the settled picker (1 + 20 + 20 + 1 + 18 ticks).
        Assert.Equal("Sélectionne une fente pour l'enregistrement.", _viewModel.MessageLine0.Text);
        Assert.Equal((16, 168), (_viewModel.MessageBox.Left, _viewModel.MessageBox.Top));
        Assert.Equal(Visibility.Collapsed, _viewModel.RecordBox0.Visibility);
        Assert.Equal(string.Empty, _viewModel.Record0Line0.Text);
        Assert.Equal(Visibility.Visible, _viewModel.RecordBox1.Visibility);
        Assert.Equal((16, 64), (_viewModel.RecordBox1.Left, _viewModel.RecordBox1.Top));
        Assert.Equal(new Color(255, 255, 255, 255), _viewModel.RecordBox1.TextureColor);
        Assert.Equal(new Color(128, 128, 128, 255), _viewModel.RecordBox2.TextureColor);
        Assert.Equal("Un Nouveau Départ", _viewModel.Record1Line0.Text);
        Assert.Equal((32, 72), (_viewModel.Record1Line0.Left, _viewModel.Record1Line0.Top));
        Assert.Equal("  HP 10       TIME 00:00:00   ", _viewModel.Record1Line1.Text);
        Assert.Equal(string.Empty, _viewModel.Record2Line0.Text); // slot2 is empty: an empty box.
        Assert.Equal(Visibility.Visible, _viewModel.RecordBox2.Visibility);

        Tick(hold: AlundraPadState.Down);
        Tick(17);
        Assert.Equal((16, 0), (_viewModel.RecordBox1.Left, _viewModel.RecordBox1.Top));
        Assert.Equal(new Color(128, 128, 128, 255), _viewModel.RecordBox1.TextureColor);
        Assert.Equal(new Color(255, 255, 255, 255), _viewModel.RecordBox2.TextureColor);
        Assert.Equal(Visibility.Visible, _viewModel.RecordBox3.Visibility);
    }

    /// <summary>L5: during OUI/NON the choice screen (Modal, E19.f3b) is pushed over the save screen (Menu), which stays;
    /// at the close pass of the choice box the choice screen alone goes, and the save screen stays pushed until its end.</summary>
    [Fact]
    public void Question_TheDialogueScreenGoesOverTheSaveScreen_AndLeavesAlone()
    {
        Assert.True(Director.Start(ValidSave()));
        Tick(60);
        Assert.Equal(new IUIScreen[] { _screen }, _uiView.Pushed);

        Tick(hold: AlundraPadState.Cross);
        Assert.Equal(new IUIScreen[] { _screen }, _uiView.Pushed); // E19.f3a: the engine's dialogue screen is gone: only the save screen is on the view
        Assert.Empty(_uiView.Removed);
        Assert.Equal(AlundraSaveScreen.ScreenLayer, _screen.Layer);

        Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(1));
        var taken = 0;
        while (AlundraDialogueDirector.Instance.IsAwaitingChoice && taken < 150)
        {
            Tick();
            taken++;
        }

        Assert.Equal(38, taken); // NON, armed in the tick of the Cross: taken 38 ticks later
        Assert.Equal(new IUIScreen[] { _screen, _choiceScreen }, _uiView.Pushed); // E19.f3b: the choice screen went up at N+2, above the save screen
        Assert.Equal(new IUIScreen[] { _choiceScreen }, _uiView.Removed); // and went at its close pass, the tick the save screen read the answer (D-E19-93)
        Assert.True(_presenter.IsPushedForTests);

        TickUntilIdle();
        Assert.Equal(new IUIScreen[] { _choiceScreen, _screen }, _uiView.Removed);
    }

    /// <summary>The production call site: the director and the presenter tick inside
    /// <see cref="AlundraWorldProxy.Update(float)"/>'s per-tick loop - removing either call fails this test.</summary>
    [Fact]
    public void WorldUpdate_TicksTheDirectorAndPushesTheScreenThroughTheRealLoop()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new AlundraEntityScriptProxy();
        Director.AttachToWorld(worldProxy.GameState, null);

        var screen = new FakeSaveScreen();
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        worldProxy.AttachSaveScreenPresenterForTests(new AlundraSaveScreenViewModel(), screen, uiView);

        Assert.True(Director.Start(ValidSave()));
        worldProxy.GameState.LastPadState = new AlundraPadState();
        worldProxy.Update(0.02f);

        Assert.Equal(AlundraSaveScreenDirector.StateExamineWait, Director.State);
        Assert.Equal(new IUIScreen[] { screen }, uiView.Pushed);
    }

    private sealed class FakeSaveScreen : IUIScreen
    {
        public UILayer Layer => AlundraSaveScreen.ScreenLayer;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGWindow> GetWindows() => Array.Empty<MGWindow>();
    }
}
