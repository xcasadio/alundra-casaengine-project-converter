#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Dialogues;
using Alundra.UI.Hud;
using Alundra.UI.Inventory;
using Alundra.UI.Shared;
using Alundra.UI.SubInventory;
using Alundra.Maps;
using Alundra.Tests.Items;
using Alundra.Tests.UI.Hud;
using Alundra.Tests.UI.Speech;

namespace Alundra.Tests.UI.Inventory;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-6): <see cref="InventoryScreen"/> is not
/// constructible headless (same reason <see cref="HudPresenterTests"/> and
/// <see cref="DialoguePresenterWiringTests"/> give their own class docs), so this drives
/// <see cref="InventoryPresenter"/> against a real <see cref="InventoryViewModel"/> (parent ADR-0002:
/// the presenter writes the view model the screen's XAML binds), a recording <see cref="IUIScreen"/> stand-in and a
/// recording <see cref="IUIViewRuntime"/> - the pattern <see cref="HudPresenterTests"/> and
/// <see cref="DialoguePresenterWiringTests"/> already establish.
/// </summary>
public sealed class InventoryPresenterTests : IDisposable
{
    public InventoryPresenterTests()
    {
        InventoryDirector.Instance.ResetForTests();
        SubInventoryDirector.Instance.ResetForTests(); // E13.d SI3: joins the session carriers this class resets.
        InventoryPostProcess.Instance.ResetForTests(); // E13.d SI3: joins the session carriers this class resets.
        HudDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        InventoryDirector.Instance.ResetForTests();
        SubInventoryDirector.Instance.ResetForTests(); // E13.d SI3: joins the session carriers this class resets.
        InventoryPostProcess.Instance.ResetForTests(); // E13.d SI3: joins the session carriers this class resets.
        HudDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
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

    private static void Tick(GameState state, InventoryDirector director, InventoryPresenter presenter, uint hold)
    {
        state.TickPad.Update(hold);
        director.Tick(null);
        presenter.Tick();
    }

    private static (GameState State, InventoryDirector Director, InventoryPresenter Presenter,
        InventoryViewModel View, FakeInventoryScreen Screen, RecordingUIViewRuntime UiView) NewFixture()
    {
        var state = GameState.Instance;
        var itemTables = ItemTablesFixture.LoadReal();
        var director = InventoryDirector.Instance;
        director.AttachToWorld(state, itemTables, null);

        // Every icon 16x16: its size only centres it in its cell.
        var view = new InventoryViewModel(_ => new Point(16, 16));
        var screen = new FakeInventoryScreen();
        var uiView = new RecordingUIViewRuntime();
        var presenter = new InventoryPresenter(director, state, itemTables, view, screen, uiView);

        return (state, director, presenter, view, screen, uiView);
    }

    [Fact]
    public void SetupTick_PushesNothing_RendersNothing()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, PadState.Start); // trigger + setup, same tick.

        Assert.True(director.IsActive);
        Assert.False(director.IsDrawn);
        Assert.Equal(0, view.AppliedModelCount);
        Assert.Empty(uiView.Pushed);
    }

    [Fact]
    public void FirstPerFrameTick_PushesScreenOnce_AndRendersOnce()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, PadState.Start); // setup tick - no push/render.
        Tick(state, director, presenter, 0); // first RunPerFrame tick - IsDrawn now true.

        Assert.True(director.IsDrawn);
        Assert.Single(uiView.Pushed);
        Assert.Same(screen, uiView.Pushed[0]);
        Assert.Equal(1, view.AppliedModelCount);
        Assert.Equal(MGUI.Core.UI.Visibility.Visible, view.RootVisibility);
        Assert.Equal(MGUI.Core.UI.Visibility.Visible, view.Cursor.Visibility);
    }

    [Fact]
    public void WhileDrawn_ExactlyOneRenderPerTick_AndScreenIsNeverPushedTwice()
    {
        var (state, director, presenter, view, screen, uiView) = NewFixture();

        Tick(state, director, presenter, PadState.Start);
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

        Tick(state, director, presenter, PadState.Start); // open, setup tick.
        for (var i = 0; i < 18; i++)
        {
            Tick(state, director, presenter, 0); // settle the opening slide.
        }

        Assert.Empty(uiView.Removed);

        Tick(state, director, presenter, PadState.Start); // Start/L2/R2 while settled -> close setup.
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

        Assert.Equal(0, view.AppliedModelCount);
        Assert.Empty(uiView.Pushed);
        Assert.Empty(uiView.Removed);
    }

    /// <summary>
    /// The production call site: every test above ticks the presenter by hand, so removing
    /// <c>_inventoryPresenter?.Tick()</c> from <see cref="WorldProxy.Update(float)"/>'s loop of the passes (the inventory block at the end of each tick, E19.u)
    /// left them all green (docs/plan-e13d-sous-inventaire.md §6 point 6). This one goes through that real loop -
    /// the main inventory's version of
    /// <c>SubInventoryPresenterTests.WorldUpdate_StartThenR1_PushesTheSubInventoryScreenThroughTheRealLoop</c>.
    /// </summary>
    [Fact]
    public void WorldUpdate_Start_PushesTheInventoryScreenThroughTheRealLoop()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new WorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new EntityScriptProxy();

        var tables = ItemTablesFixture.LoadReal();
        PlayerManager.InitializeNewGameInventory(worldProxy.GameState, tables);
        InventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, null);
        SubInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, null); // the loop ticks it too.

        var view = new InventoryViewModel(_ => new Point(16, 16));
        var screen = new FakeInventoryScreen();
        var uiView = new RecordingUIViewRuntime();
        worldProxy.AttachInventoryPresenterForTests(view, screen, uiView);

        void Frame(uint hold)
        {
            worldProxy.GameState.LastPadState = new PadState { ButtonsHold = hold };
            worldProxy.Update(0.02f);
        }

        Frame(PadState.Start);
        for (var i = 0; i < 20 && !InventoryDirector.Instance.IsDrawn; i++)
        {
            Frame(0);
        }

        Assert.True(InventoryDirector.Instance.IsDrawn);
        Assert.Equal(new IUIScreen[] { screen }, uiView.Pushed);
        Assert.Equal(MGUI.Core.UI.Visibility.Visible, view.RootVisibility);
    }
}
