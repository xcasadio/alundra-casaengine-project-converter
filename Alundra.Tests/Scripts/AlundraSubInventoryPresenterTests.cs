#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests.Scripts;

/// <summary>
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md): <see cref="AlundraSubInventoryScreen"/> is not
/// constructible headless (same reason <see cref="AlundraInventoryPresenterTests"/> gives its own class
/// doc), so this drives <see cref="AlundraSubInventoryPresenter"/> against a real
/// <see cref="AlundraSubInventoryViewModel"/>, a recording <see cref="IUIScreen"/> stand-in and a recording
/// <see cref="IUIViewRuntime"/> - the same pattern.
/// </summary>
public sealed class AlundraSubInventoryPresenterTests : IDisposable
{
    public AlundraSubInventoryPresenterTests()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private sealed class FakeSubInventoryScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Menu;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGUI.Core.UI.MGWindow> GetWindows() => Array.Empty<MGUI.Core.UI.MGWindow>();
    }

    private sealed class RecordingUIViewRuntime : IUIViewRuntime
    {
        public readonly List<IUIScreen> Pushed = new();
        public readonly List<IUIScreen> Removed = new();

        public bool IsPointerOverUI => false;
        public bool IsPointerCaptured => false;
        public bool IsKeyboardCaptured => false;
        public UIViewInputState InputState => UIViewInputState.Empty;
        public bool HasModalInput => false;
        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }
        public void Draw() { }
        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;
        public void PushScreen(IUIScreen screen) => Pushed.Add(screen);
        public IUIScreen? PopScreen() => null;
        public void RemoveScreen(IUIScreen screen) => Removed.Add(screen);
        public void Dispose() { }
    }

    private static void Tick(AlundraGameState state, AlundraSubInventoryDirector director, AlundraSubInventoryPresenter presenter, uint hold)
    {
        state.TickPad.Update(hold);
        director.Tick();
        presenter.Tick();
    }

    private static (AlundraGameState State, AlundraSubInventoryDirector Director, AlundraSubInventoryPresenter Presenter,
        AlundraSubInventoryViewModel View, FakeSubInventoryScreen Screen, RecordingUIViewRuntime UiView) NewFixture()
    {
        var state = AlundraGameState.Instance;
        var itemTables = ItemTablesFixture.LoadReal();
        var director = AlundraSubInventoryDirector.Instance;
        director.AttachToWorld(state, itemTables, null);

        // Every icon 16x16: its size only centres it in its cell.
        var view = new AlundraSubInventoryViewModel(_ => new Point(16, 16));
        var screen = new FakeSubInventoryScreen();
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraSubInventoryPresenter(director, state, itemTables, view, screen, uiView);

        return (state, director, presenter, view, screen, uiView);
    }

    [Fact]
    public void SetupTick_PushesNothing_RendersNothing()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        director.OpenFromPostProcess(); // the only trigger this director has - never its own pad read.
        presenter.Tick();

        Assert.True(director.IsActive);
        Assert.False(director.IsDrawn);
        Assert.Equal(0, view.AppliedModelCount);
        Assert.Empty(uiView.Pushed);
    }

    [Fact]
    public void FirstPerFrameTick_PushesScreenOnce_AndRendersOnce()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        director.OpenFromPostProcess();
        presenter.Tick(); // setup tick - no push/render.
        Tick(state, director, presenter, 0); // first Tick() - IsDrawn now true.

        Assert.True(director.IsDrawn);
        Assert.Single(uiView.Pushed);
        Assert.Same(screen, uiView.Pushed[0]);
        Assert.Equal(1, view.AppliedModelCount);
        Assert.Equal(MGUI.Core.UI.Visibility.Visible, view.RootVisibility);
    }

    [Fact]
    public void WhileDrawn_ExactlyOneRenderPerTick_AndScreenIsNeverPushedTwice()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        director.OpenFromPostProcess();
        presenter.Tick();
        for (var i = 0; i < 10; i++)
        {
            Tick(state, director, presenter, 0);
        }

        Assert.Equal(10, view.AppliedModelCount);
        Assert.Single(uiView.Pushed); // never pushed again once already up.
    }

    [Fact]
    public void Close_RemovesTheScreen_ExactlyOnTheDirectorsLastActiveTick()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        director.OpenFromPostProcess();
        presenter.Tick();
        for (var i = 0; i < 20; i++)
        {
            Tick(state, director, presenter, 0); // settle the opening slide.
        }

        Assert.Empty(uiView.Removed);

        Tick(state, director, presenter, AlundraPadState.Start); // Start/Triangle/L2/R2 -> close setup.
        for (var i = 0; i < 20 && director.IsActive; i++)
        {
            Tick(state, director, presenter, 0); // drive the closing slide to completion.
        }

        Assert.False(director.IsActive);
        Assert.Single(uiView.Removed);
        Assert.Same(screen, uiView.Removed[0]);
    }

    [Fact]
    public void NothingDrawn_ModelIsInvisible_AndNeverPushedOrRendered()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        // Never opened at all - presenter must stay entirely quiet.
        for (var i = 0; i < 5; i++)
        {
            Tick(state, director, presenter, 0);
        }

        Assert.Equal(0, view.AppliedModelCount);
        Assert.Empty(uiView.Pushed);
        Assert.Empty(uiView.Removed);
    }

    /// <summary>The two screens are never pushed together (D-E13D-26) - a real round trip through both real
    /// directors and the real post-process, both presenters' own <see cref="AlundraSubInventoryPresenter.IsPushedForTests"/>/
    /// <see cref="AlundraInventoryPresenter.IsPushedForTests"/> recorded every tick.</summary>
    [Fact]
    public void MainAndSubScreens_AreNeverBothPushed_DuringARoundTrip()
    {
        var state = AlundraGameState.Instance;
        var itemTables = ItemTablesFixture.LoadReal();

        var mainDirector = AlundraInventoryDirector.Instance;
        var subDirector = AlundraSubInventoryDirector.Instance;
        mainDirector.AttachToWorld(state, itemTables, null);
        subDirector.AttachToWorld(state, itemTables, null);

        var mainView = new AlundraInventoryViewModel(_ => new Point(16, 16));
        var mainScreen = new FakeSubInventoryScreenForMain();
        var mainUiView = new RecordingUIViewRuntime();
        var mainPresenter = new AlundraInventoryPresenter(mainDirector, state, itemTables, mainView, mainScreen, mainUiView);

        var subView = new AlundraSubInventoryViewModel(_ => new Point(16, 16));
        var subScreen = new FakeSubInventoryScreen();
        var subUiView = new RecordingUIViewRuntime();
        var subPresenter = new AlundraSubInventoryPresenter(subDirector, state, itemTables, subView, subScreen, subUiView);

        void OneTick(uint hold)
        {
            state.TickPad.Update(hold);
            mainDirector.Tick(null);
            subDirector.Tick();
            AlundraInventoryPostProcess.Instance.Run();
            mainPresenter.Tick();
            subPresenter.Tick();
            Assert.False(mainPresenter.IsPushedForTests && subPresenter.IsPushedForTests, "both screens pushed on the same tick");
        }

        // Each switch needs its own slide fully SETTLED before the next button press is read at all (plan
        // §1.4/SI3 verifier: a switch requested mid opening-slide is ignored, like the original) - the
        // close slide (18 ticks) plus the Tc/Tc+1 handoff plus the new inventory's own opening slide
        // (another 18), well inside 40.
        const int SettleTicks = 40;

        OneTick(AlundraPadState.Start); // open the main inventory.
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0); // settle the main's opening slide.
        }

        Assert.True(mainPresenter.IsPushedForTests);

        OneTick(AlundraPadState.R1); // main -> sub.
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0);
        }

        Assert.True(subPresenter.IsPushedForTests);
        Assert.False(mainPresenter.IsPushedForTests);

        OneTick(AlundraPadState.R1); // sub -> main.
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0);
        }

        Assert.True(mainPresenter.IsPushedForTests);
        Assert.False(subPresenter.IsPushedForTests);
    }

    /// <summary>The production call site: the REAL <see cref="AlundraWorldProxy.Update(float)"/> ticks the
    /// sub-inventory's presenter inside its per-tick pad loop, so Start then R1 through the real loop end with the
    /// sub-inventory screen pushed and its view model written. Without the presenter's call in that loop the screen
    /// would never appear in game (a mutation removing it fails this test).</summary>
    [Fact]
    public void WorldUpdate_StartThenR1_PushesTheSubInventoryScreenThroughTheRealLoop()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new AlundraEntityScriptProxy();

        var tables = ItemTablesFixture.LoadReal();
        AlundraPlayerManager.InitializeNewGameInventory(worldProxy.GameState, tables);
        AlundraInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, null);
        AlundraSubInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, null);

        var view = new AlundraSubInventoryViewModel(_ => new Point(16, 16));
        var screen = new FakeSubInventoryScreen();
        var ui = new RecordingUIViewRuntime();
        worldProxy.AttachSubInventoryPresenterForTests(view, screen, ui);

        void Frame(uint hold)
        {
            worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = hold };
            worldProxy.Update(0.02f);
        }

        Frame(AlundraPadState.Start);
        for (var i = 0; i < 20; i++)
        {
            Frame(0);
        }

        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        Frame(AlundraPadState.R1);
        for (var i = 0; i < 40 && !AlundraSubInventoryDirector.Instance.IsDrawn; i++)
        {
            Frame(0);
        }

        Frame(0); // one tick with the sub-inventory drawn, through the real loop.

        Assert.True(AlundraSubInventoryDirector.Instance.IsDrawn);
        Assert.Equal(new IUIScreen[] { screen }, ui.Pushed);
        Assert.Equal(MGUI.Core.UI.Visibility.Visible, view.RootVisibility);
    }

    private sealed class FakeSubInventoryScreenForMain : IUIScreen
    {
        public UILayer Layer => UILayer.Menu;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGUI.Core.UI.MGWindow> GetWindows() => Array.Empty<MGUI.Core.UI.MGWindow>();
    }
}
