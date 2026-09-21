#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-6): <see cref="AlundraInventoryScreen"/> is not
/// constructible headless (same reason <see cref="AlundraHudPresenterTests"/> and
/// <see cref="AlundraDialoguePresenterWiringTests"/> give their own class docs), so this drives
/// <see cref="AlundraInventoryPresenter"/> against a recording <see cref="IAlundraInventoryView"/>, a
/// recording <see cref="IUIScreen"/> stand-in and a recording <see cref="IUIViewRuntime"/> - the value
/// the presenter itself pushed/pushed-a-screen-for, exactly the pattern <see cref="AlundraHudPresenterTests"/>
/// and <see cref="AlundraDialoguePresenterWiringTests"/> already establish.
/// </summary>
public sealed class AlundraInventoryPresenterTests : IDisposable
{
    public AlundraInventoryPresenterTests()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
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
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private sealed class RecordingInventoryView : IAlundraInventoryView
    {
        public readonly List<InventoryDisplayModel> RenderCalls = new();
        public void Render(InventoryDisplayModel model) => RenderCalls.Add(model);
    }

    private sealed class FakeInventoryScreen : IUIScreen
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

    private static void Tick(AlundraGameState state, AlundraInventoryDirector director, AlundraInventoryPresenter presenter, uint hold)
    {
        state.TickPad.Update(hold);
        director.Tick(null);
        presenter.Tick();
    }

    private static (AlundraGameState State, AlundraInventoryDirector Director, AlundraInventoryPresenter Presenter,
        RecordingInventoryView View, FakeInventoryScreen Screen, RecordingUIViewRuntime UiView) NewFixture()
    {
        var state = AlundraGameState.Instance;
        var itemTables = ItemTablesFixture.LoadReal();
        var director = AlundraInventoryDirector.Instance;
        director.AttachToWorld(state, itemTables, null);

        var view = new RecordingInventoryView();
        var screen = new FakeInventoryScreen();
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraInventoryPresenter(director, state, itemTables, view, screen, uiView);

        return (state, director, presenter, view, screen, uiView);
    }

    [Fact]
    public void SetupTick_PushesNothing_RendersNothing()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, AlundraPadState.Start); // trigger + setup, same tick.

        Assert.True(director.IsActive);
        Assert.False(director.IsDrawn);
        Assert.Empty(view.RenderCalls);
        Assert.Empty(uiView.Pushed);
    }

    [Fact]
    public void FirstPerFrameTick_PushesScreenOnce_AndRendersOnce()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, AlundraPadState.Start); // setup tick - no push/render.
        Tick(state, director, presenter, 0); // first RunPerFrame tick - IsDrawn now true.

        Assert.True(director.IsDrawn);
        Assert.Single(uiView.Pushed);
        Assert.Same(screen, uiView.Pushed[0]);
        Assert.Single(view.RenderCalls);
        Assert.True(view.RenderCalls[0].Visible);
    }

    [Fact]
    public void WhileDrawn_ExactlyOneRenderPerTick_AndScreenIsNeverPushedTwice()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, AlundraPadState.Start);
        for (var i = 0; i < 10; i++)
        {
            Tick(state, director, presenter, 0);
        }

        Assert.Equal(10, view.RenderCalls.Count);
        Assert.Single(uiView.Pushed); // never pushed again once already up.
    }

    [Fact]
    public void Close_RemovesTheScreen_ExactlyOnTheDirectorsLastActiveTick()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, AlundraPadState.Start); // open, setup tick.
        for (var i = 0; i < 18; i++)
        {
            Tick(state, director, presenter, 0); // settle the opening slide.
        }

        Assert.Empty(uiView.Removed);

        Tick(state, director, presenter, AlundraPadState.Start); // Start/L2/R2 while settled -> close setup.
        for (var i = 0; i < 18 && director.IsActive; i++)
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

        Assert.Empty(view.RenderCalls);
        Assert.Empty(uiView.Pushed);
        Assert.Empty(uiView.Removed);
    }
}
