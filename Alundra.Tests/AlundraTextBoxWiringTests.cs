#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using Alundra.Tests.UI;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.f2b1c T4 (docs/plan-e19-opcodes.md, F2B1C-R4 and R5): the wiring of the text box screen. The presenter pushes the screen at the first drawn pass of the box, writes the
/// view model after each pass and removes the screen when the box is no longer drawn (the release, the out-of-band close, the map entry); the world proxy ticks it right
/// after each pass of the box, whether or not the hero exists; the screen is given back at the end of the world; the retry builds it once the view appears; with a choice, the
/// text box screen is pushed first and the choice screen (E19.f3b) above it, which goes when the answer is taken.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraTextBoxWiringTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;

    public AlundraTextBoxWiringTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        Director.ResetForTests();
        Director.Box.DrawsPreShiftRowsAtScrollEnd = AlundraDialogueBox.ScrollEndDrawsPreShiftRows; // a test of the variant leaves the box's flag on
        Director.AdvanceProviderForTests = null;
        AlundraDialogueCapturePresenter.ResetForTests();
        AlundraFont3Advances.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private sealed class FakeTextBoxScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Modal;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGWindow> GetWindows() => Array.Empty<MGWindow>();
    }

    private static DialogueAsset Bonjour() => DialogueTestAssets.SinglePage("Bonjour", "bonjour");

    // ---- the presenter ------------------------------------------------------------------------------------------------------------

    private sealed class Rig
    {
        public readonly AlundraSaveBookTests.RecordingUIViewRuntime UiView = new();
        public readonly FakeTextBoxScreen Screen = new();
        public readonly AlundraTextBoxViewModel ViewModel = new();
        public readonly AlundraTextBoxPresenter Presenter;
        public readonly AlundraGameState State = new();

        public Rig(IDialoguePresenter? presenter = null)
        {
            Director.AttachToWorld(presenter ?? new DialogueService(), State);
            Director.InstallForMapEntry();
            Presenter = new AlundraTextBoxPresenter(Director, ViewModel, Screen, UiView);
        }

        /// <summary>One pass of the box then the tick of the presenter, as the world proxy does after each pass.</summary>
        public void Pass(bool held = false, bool pressed = false)
        {
            Director.Pass(held, pressed);
            Presenter.Tick();
        }

        public void Passes(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Pass();
            }
        }
    }

    [Fact]
    public void ThePresenter_PushesTheScreenAtTheFirstDrawnPass_AppliesEachPass_AndRemovesItOnTheReleasePass()
    {
        var rig = new Rig();
        Director.Open(DialogueTestAssets.BuildRaw("l1l2", "Start", "l1[br trimwhitespace=false/]l2[br trimwhitespace=false/]gypjq"), "Start", 1);
        rig.Presenter.Tick();
        Assert.Empty(rig.UiView.Pushed); // the opening is not a drawn pass
        Assert.False(rig.Presenter.IsPushedForTests);

        rig.Pass(); // pass 1: slide-in, the frame is at Y 240, below the screen: drawn
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Pushed);
        Assert.True(rig.Presenter.IsPushedForTests);
        Assert.Equal(Visibility.Visible, rig.ViewModel.RootVisibility);
        Assert.Equal(240, rig.ViewModel.Frame.Top);

        // The square held and pressed at every pass types the text then closes the box (S5): the release pass at 49 draws nothing.
        for (var pass = 2; pass <= 48; pass++)
        {
            rig.Pass(true, true);
            Assert.True(Director.Box.Drawn, $"pass {pass} is drawn");
            Assert.Single(rig.UiView.Pushed);
            Assert.Empty(rig.UiView.Removed);
        }

        Assert.Equal("l1", rig.ViewModel.Row0.Text);
        Assert.Equal("gypjq", rig.ViewModel.Row2.Text);

        rig.Pass(true, true); // pass 49: DialogClosed
        Assert.False(Director.Box.Drawn);
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Removed);
        Assert.False(rig.Presenter.IsPushedForTests);
        Assert.Equal(Visibility.Collapsed, rig.ViewModel.RootVisibility);

        // A second box pushes it again.
        Director.Open(Bonjour(), "Start", 1);
        rig.Pass();
        Assert.Equal(new IUIScreen[] { rig.Screen, rig.Screen }, rig.UiView.Pushed);
    }

    [Fact]
    public void ThePresenter_RemovesTheScreen_WhenTheBoxIsBroughtDownOutOfBand()
    {
        var rig = new Rig();
        Director.Open(Bonjour(), "Start", 1);
        rig.Passes(30);
        Assert.Single(rig.UiView.Pushed);

        Director.NotifyPresenterClosed(); // the window closed by its own control: the box is down at once
        Assert.False(Director.Box.IsActive);
        rig.Pass();
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Removed);
        Assert.Equal(Visibility.Collapsed, rig.ViewModel.RootVisibility);
    }

    [Fact]
    public void ThePresenter_RemovesTheScreen_WhenTheMapEntryResetsTheBox()
    {
        var rig = new Rig();
        Director.Open(Bonjour(), "Start", 1);
        rig.Passes(30);
        Assert.Single(rig.UiView.Pushed);

        Director.InstallForMapEntry();
        rig.Pass();
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Removed);
        Assert.False(rig.Presenter.IsPushedForTests);
    }

    // ---- the order with a choice ----------------------------------------------------------------------------------------------------

    [Fact]
    public void WithAChoice_TheTextBoxIsPushedFirst_TheChoiceScreenAboveIt_AndTheAnswerRemovesOnlyTheChoiceScreen()
    {
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var rig = new Rig();
        // The rig's own view is not the one shared here: share one view so the order of the two screens is observed.
        var textBox = new AlundraTextBoxPresenter(Director, rig.ViewModel, rig.Screen, uiView);
        var choiceScreen = new FakeTextBoxScreen();
        var choice = new AlundraChoicePresenter(Director, new AlundraChoiceViewModel(), choiceScreen, uiView);
        Director.Open(Bonjour(), "Start", 1);
        for (var i = 0; i < 25; i++)
        {
            Director.Pass(false, false);
            textBox.Tick();
            choice.Tick();
        }

        Assert.Equal(new IUIScreen[] { rig.Screen }, uiView.Pushed); // opening the box pushed nothing of the choice's

        Director.OpenChoice(new[] { "OUI", "NON" });
        Assert.Equal(new IUIScreen[] { rig.Screen }, uiView.Pushed); // the opener's tick draws nothing
        Assert.Equal(UILayer.Modal, rig.Screen.Layer);

        Assert.True(Director.SelectChoiceForTests(0));
        int? answer = null;
        var passes = 0;
        while ((answer = Director.TakeChoiceResult()) == null && passes < 150)
        {
            Director.Pass(false, false);
            textBox.Tick();
            choice.Tick();
            passes++;
            if (passes == 1)
            {
                Assert.Equal(new IUIScreen[] { rig.Screen }, uiView.Pushed); // N+1, the init pass: nothing drawn
            }

            if (passes == 2)
            {
                Assert.Equal(new IUIScreen[] { rig.Screen, choiceScreen }, uiView.Pushed); // N+2: the first drawn pass, the text box first, the choice above it
            }
        }

        Assert.Equal(37, passes);
        Assert.Equal(1, answer);
        Assert.Equal(new IUIScreen[] { choiceScreen }, uiView.Removed); // the answer removes only the choice screen
        Assert.True(textBox.IsPushedForTests);
    }

    [Fact]
    public void AChoiceWithoutABox_StillClosesThroughTheStandaloneRoute()
    {
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        _ = new Rig();
        var choiceScreen = new FakeTextBoxScreen();
        var choice = new AlundraChoicePresenter(Director, new AlundraChoiceViewModel(), choiceScreen, uiView);

        Director.OpenChoice(new[] { "OUI", "NON" });
        for (var i = 0; i < 3; i++)
        {
            Director.Pass(false, false);
            choice.Tick();
        }

        Assert.Equal(new IUIScreen[] { choiceScreen }, uiView.Pushed); // a lone choice pushes its screen alone, at N+2
        Assert.True(Director.CloseStandaloneChoice());
        Assert.Empty(uiView.Removed); // the screen goes at the next tick of the presenter
        Assert.False(Director.IsAwaitingChoice);

        choice.Tick();
        Assert.Equal(new IUIScreen[] { choiceScreen }, uiView.Removed);
        Assert.False(choice.IsPushedForTests);
    }

    // ---- the choice presenter ------------------------------------------------------------------------------------------------------

    private sealed class ChoiceRig
    {
        public readonly AlundraSaveBookTests.RecordingUIViewRuntime UiView = new();
        public readonly FakeTextBoxScreen Screen = new();
        public readonly AlundraChoiceViewModel ViewModel = new();
        public readonly AlundraChoicePresenter Presenter;

        public ChoiceRig()
        {
            Director.AttachToWorld(new DialogueService(), new AlundraGameState());
            Director.InstallForMapEntry();
            Presenter = new AlundraChoicePresenter(Director, ViewModel, Screen, UiView);
        }

        /// <summary>One pass of the director (which runs the choice box last) then the tick of the presenter, as the world proxy does after each pass.</summary>
        public void Pass()
        {
            Director.Pass(false, false);
            Presenter.Tick();
        }
    }

    [Theory]
    [InlineData(0, 37)]
    [InlineData(1, 38)]
    public void TheChoicePresenter_PushesAtNPlus2_AppliesEachPass_AndRemovesAtTheClosePass(int option, int closePass)
    {
        var rig = new ChoiceRig();
        Director.OpenChoice(new[] { "OUI", "NON" });
        rig.Presenter.Tick();
        Assert.Empty(rig.UiView.Pushed); // the opener's tick draws nothing
        Assert.Equal(Visibility.Collapsed, rig.ViewModel.RootVisibility);

        Assert.True(Director.SelectChoiceForTests(option));
        for (var pass = 1; pass < closePass; pass++)
        {
            rig.Pass();
            Assert.True(
                (pass < 2 ? 0 : 1) == rig.UiView.Pushed.Count,
                $"pass N+{pass}: pushed {rig.UiView.Pushed.Count}");
            Assert.True(0 == rig.UiView.Removed.Count, $"pass N+{pass}: removed {rig.UiView.Removed.Count}");
        }

        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Pushed);
        Assert.Equal(Visibility.Visible, rig.ViewModel.RootVisibility);
        Assert.Equal(closePass - 2, rig.ViewModel.AppliedCount); // applied at each pass from the push (N+2) to the one before the close pass

        rig.Pass(); // the close pass writes the result and draws nothing
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Removed);
        Assert.False(rig.Presenter.IsPushedForTests);
        Assert.Equal(Visibility.Collapsed, rig.ViewModel.RootVisibility);
        Assert.Equal(option == 0 ? 1 : 0, Director.TakeChoiceResult());
    }

    [Fact]
    public void TheChoicePresenter_PushesTheScreenAgainForASecondChoice()
    {
        var rig = new ChoiceRig();
        for (var round = 0; round < 2; round++)
        {
            Director.OpenChoice(new[] { "OUI", "NON" });
            Assert.True(Director.SelectChoiceForTests(0));
            for (var pass = 1; pass <= 37; pass++)
            {
                rig.Pass();
            }

            Assert.NotNull(Director.TakeChoiceResult());
        }

        Assert.Equal(new IUIScreen[] { rig.Screen, rig.Screen }, rig.UiView.Pushed);
        Assert.Equal(new IUIScreen[] { rig.Screen, rig.Screen }, rig.UiView.Removed);
    }

    [Fact]
    public void TheChoicePresenter_RemovesTheScreen_WhenTheMapEntryResetsTheMachine()
    {
        var rig = new ChoiceRig();
        Director.OpenChoice(new[] { "OUI", "NON" });
        for (var pass = 1; pass <= 10; pass++)
        {
            rig.Pass();
        }

        Assert.Single(rig.UiView.Pushed);

        Director.InstallForMapEntry();
        rig.Presenter.Tick();
        Assert.Equal(new IUIScreen[] { rig.Screen }, rig.UiView.Removed);
        Assert.False(rig.Presenter.IsPushedForTests);
        Assert.Equal(Visibility.Collapsed, rig.ViewModel.RootVisibility);
    }

    // ---- the world proxy ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWorldProxy_TicksThePresenterRightAfterEachPassOfTheBox_AndTheViewModelFollowsTheBox()
    {
        using var montage = new DialogueBoxMontage();
        var view = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var screen = new FakeTextBoxScreen();
        var viewModel = new AlundraTextBoxViewModel();
        montage.Proxy.AttachTextBoxPresenterForTests(viewModel, screen, view);
        montage.EntityScript = frame =>
        {
            if (frame == 3)
            {
                montage.Director.Open(Bonjour(), "Start", 1);
            }
        };

        montage.RunFrames(3);
        Assert.Empty(view.Pushed);
        Assert.Equal(0, viewModel.AppliedCount);

        // Frame 3: the script of an entity opens the box, then the proxy's update runs the first pass of the box of that very frame, and the presenter reads it.
        montage.RunFrame();
        Assert.Equal(new IUIScreen[] { screen }, view.Pushed);
        Assert.Equal(1, viewModel.AppliedCount);

        montage.RunFrames(60);
        Assert.Equal(montage.Director.PassCountForTests - 3, viewModel.AppliedCount); // one application per pass of the box since its opening (3 passes ran with the box closed)
        var box = montage.Director.Box;
        Assert.Equal(box.Row(0), viewModel.Row0.Text);
        Assert.Equal("bonjour", viewModel.Row0.Text);
        Assert.Equal(box.Y, viewModel.Frame.Top);
        Assert.Equal((box.ClipTop, box.ClipHeight), (viewModel.Clip.Top, viewModel.Clip.Height));
    }

    [Fact]
    public void TheWorldProxy_TicksThePresenterAfterEachPassOfACatchUpFrame_AndEvenWithoutAHero()
    {
        using var montage = new DialogueBoxMontage();
        var view = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var viewModel = new AlundraTextBoxViewModel();
        montage.Proxy.AttachTextBoxPresenterForTests(viewModel, new FakeTextBoxScreen(), view);
        montage.Proxy.PlayerEntity = null; // the pass of the box runs without a hero, so does the presenter
        montage.Director.Open(Bonjour(), "Start", 1);

        montage.RunFrame(0.06f); // three logic ticks: three passes, each followed by the presenter
        Assert.Equal(3, montage.Director.PassCountForTests);
        Assert.Equal(3, viewModel.AppliedCount);
        Assert.Single(view.Pushed);
    }

    [Fact]
    public void TheWorldProxy_RemovesTheScreen_OnAMapEntryReset()
    {
        using var montage = new DialogueBoxMontage();
        var view = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var screen = new FakeTextBoxScreen();
        montage.Proxy.AttachTextBoxPresenterForTests(new AlundraTextBoxViewModel(), screen, view);
        montage.Director.Open(Bonjour(), "Start", 1);
        montage.RunFrames(10);
        Assert.Single(view.Pushed);

        montage.Director.InstallForMapEntry();
        montage.RunFrame();
        Assert.Equal(new IUIScreen[] { screen }, view.Removed);
    }

    [Fact]
    public void TheWorldProxy_TicksTheChoicePresenterAfterTheTextBoxPresenter_AndEvenWithoutAHero()
    {
        using var montage = new DialogueBoxMontage();
        var view = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var textBoxScreen = new FakeTextBoxScreen();
        var choiceScreen = new FakeTextBoxScreen();
        var choiceViewModel = new AlundraChoiceViewModel();
        montage.Proxy.PlayerEntity = null; // the passes run without a hero, so do the presenters

        // The box and the choice are drawn before any presenter exists (the passes of the proxy run them): the first tick of the two presenters is the same tick.
        montage.Director.Open(Bonjour(), "Start", 1);
        montage.RunFrames(25);
        montage.Director.OpenChoice(new[] { "OUI", "NON" });
        montage.RunFrames(2); // N+1 (init), N+2 (first drawn pass)
        Assert.True(montage.Director.ChoiceBox.Drawn is not null);
        Assert.True(montage.Director.Box.Drawn);

        // Attached in the reverse order on purpose: only the proxy's own order decides which one pushes first.
        montage.Proxy.AttachChoicePresenterForTests(choiceViewModel, choiceScreen, view);
        montage.Proxy.AttachTextBoxPresenterForTests(new AlundraTextBoxViewModel(), textBoxScreen, view);
        montage.RunFrame();

        Assert.Equal(new IUIScreen[] { textBoxScreen, choiceScreen }, view.Pushed); // the text box first, the choice above it
        Assert.Equal(1, choiceViewModel.AppliedCount);

        montage.RunFrame(0.06f); // three logic ticks: three passes, each followed by the presenters
        Assert.Equal(4, choiceViewModel.AppliedCount);
    }

    [Fact]
    public void TheWorldProxy_RemovesTheChoiceScreen_OnAMapEntryReset()
    {
        using var montage = new DialogueBoxMontage();
        var view = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var screen = new FakeTextBoxScreen();
        montage.Proxy.AttachChoicePresenterForTests(new AlundraChoiceViewModel(), screen, view);
        montage.Director.OpenChoice(new[] { "OUI", "NON" });
        montage.RunFrames(10);
        Assert.Single(view.Pushed);

        montage.Director.InstallForMapEntry();
        montage.RunFrame();
        Assert.Equal(new IUIScreen[] { screen }, view.Removed);
    }

    // ---- the end of the world and the retry ---------------------------------------------------------------------------------------------

    [Fact]
    public void OnEndPlay_GivesTheTextBoxScreenBack()
    {
        var assets = TextBoxScreenAssets.New();
        var screen = new AlundraTextBoxScreen(assets, new UIFontRegistry(assets));
        var proxy = new AlundraWorldProxy();
        proxy.AttachTextBoxScreenForTests(screen);
        Assert.Equal(0, assets.CollectUnreferenced()); // held by the screen

        proxy.OnEndPlay(null!);

        Assert.True(assets.CollectUnreferenced() >= 1, "the envelope of the screen is no longer held");
        proxy.OnEndPlay(null!); // idempotent
    }

    [Fact]
    public void OnEndPlay_GivesTheChoiceScreenBack()
    {
        var assets = TextBoxScreenAssets.New();
        var screen = new AlundraChoiceScreen(assets, new UIFontRegistry(assets));
        var proxy = new AlundraWorldProxy();
        proxy.AttachChoiceScreenForTests(screen);
        Assert.Equal(0, assets.CollectUnreferenced()); // held by the screen

        proxy.OnEndPlay(null!);

        Assert.True(assets.CollectUnreferenced() >= 1, "the envelope of the choice screen is no longer held");
        Assert.Null(proxy.ChoiceScreenForTests);
        proxy.OnEndPlay(null!); // idempotent
    }

    /// <summary>A world whose game has a view manager with an active view carrying <paramref name="uiView"/> (when not null), the asset manager and the font registry the
    /// retry needs: the reflected game of the wiring tests of the dialogue presenter.</summary>
    private static World NewWorld(IUIViewRuntime? uiView, AssetContentManager? assets)
    {
        var world = new World { Name = "TestWorld" };
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new GameComponentCollection());
        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManager = new ViewManager();
        typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(gameManager, viewManager);
        typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, gameManager);
        if (assets != null)
        {
            typeof(CasaEngineGame).GetField("<AssetContentManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, assets);
            typeof(CasaEngineGame).GetField("<UIFonts>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new UIFontRegistry(assets));
        }

        if (uiView != null)
        {
            var view = (RenderView)RuntimeHelpers.GetUninitializedObject(typeof(RenderView));
            view.UIView = uiView;
            view.Enabled = true;
            view.IsVisible = true;
            viewManager.Add(view);
            viewManager.SetActive(view);
        }

        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);
        return world;
    }

    [Fact]
    public void TheRetry_BuildsTheScreenOnceTheViewAppears_AndTheScreenIsGivenBackAtTheEnd()
    {
        var assets = TextBoxScreenAssets.New();
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var world = NewWorld(uiView: null, assets);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        proxy.TryWireTextBoxScreenOnce();
        Assert.Null(proxy.TextBoxScreenForTests); // no view yet: retried

        var view = (RenderView)RuntimeHelpers.GetUninitializedObject(typeof(RenderView));
        view.UIView = uiView;
        view.Enabled = true;
        view.IsVisible = true;
        world.Game.GameManager.ViewManager.Add(view);
        world.Game.GameManager.ViewManager.SetActive(view);

        proxy.TryWireTextBoxScreenOnce();
        Assert.NotNull(proxy.TextBoxScreenForTests);
        var wired = proxy.TextBoxScreenForTests;
        proxy.TryWireTextBoxScreenOnce();
        Assert.Same(wired, proxy.TextBoxScreenForTests); // wired once

        proxy.OnEndPlay(world);
        Assert.Null(proxy.TextBoxScreenForTests);
    }

    [Fact]
    public void TheRetry_LogsAndDoesNotThrow_WhenTheExportHasNoTextBoxScreen()
    {
        var assets = TextBoxScreenAssets.New(withTheScreen: false);
        var world = NewWorld(new AlundraSaveBookTests.RecordingUIViewRuntime(), assets);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        proxy.TryWireTextBoxScreenOnce();
        proxy.TryWireTextBoxScreenOnce();

        Assert.Null(proxy.TextBoxScreenForTests);
    }

    [Fact]
    public void TheChoiceRetry_BuildsTheScreenOnceTheViewAppears_AndTheScreenIsGivenBackAtTheEnd()
    {
        var assets = TextBoxScreenAssets.New();
        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var world = NewWorld(uiView: null, assets);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        proxy.TryWireChoiceScreenOnce();
        Assert.Null(proxy.ChoiceScreenForTests); // no view yet: retried

        var view = (RenderView)RuntimeHelpers.GetUninitializedObject(typeof(RenderView));
        view.UIView = uiView;
        view.Enabled = true;
        view.IsVisible = true;
        world.Game.GameManager.ViewManager.Add(view);
        world.Game.GameManager.ViewManager.SetActive(view);

        proxy.TryWireChoiceScreenOnce();
        Assert.NotNull(proxy.ChoiceScreenForTests);
        var wired = proxy.ChoiceScreenForTests;
        proxy.TryWireChoiceScreenOnce();
        Assert.Same(wired, proxy.ChoiceScreenForTests); // wired once

        proxy.OnEndPlay(world);
        Assert.Null(proxy.ChoiceScreenForTests);
    }

    [Fact]
    public void TheChoiceRetry_LogsAndDoesNotThrow_WhenTheExportHasNoChoiceScreen()
    {
        var assets = TextBoxScreenAssets.New(withTheScreen: false);
        var world = NewWorld(new AlundraSaveBookTests.RecordingUIViewRuntime(), assets);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        proxy.TryWireChoiceScreenOnce();
        proxy.TryWireChoiceScreenOnce();

        Assert.Null(proxy.ChoiceScreenForTests);
    }
}
