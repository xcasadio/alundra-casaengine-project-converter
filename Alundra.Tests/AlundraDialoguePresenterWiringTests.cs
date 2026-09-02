using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// ③bis (docs/plan-e12-dialogues.md, slice E12.a) - the UI wiring link: <see cref="AlundraDialoguePresenter"/>
/// must push its own <see cref="CasaEngine.Framework.Dialogue.UI.DialogueScreen"/> onto the ATTACHED
/// <see cref="IUIViewRuntime"/> the moment a line/choice opens the dialogue, and remove it the moment it
/// closes. A real <c>ScreenStack</c>/<c>UIRoot</c> is NOT constructible headless (its <c>Push</c>
/// initializes the pushed screen against a live graphics stack - the relecture correction this test's own
/// class doc documents), so this test drives a RECORDING double of <see cref="IUIViewRuntime"/> instead -
/// the SAME interface the engine's own <c>UIOverlayDemo</c> consumes for this exact push/remove pair.
///
/// Mutation (plan §4, ③bis): skipping the push on open, or the remove on close, is exactly what this
/// test's own assertions are built to catch.
/// </summary>
public class AlundraDialoguePresenterWiringTests : IDisposable
{
    public AlundraDialoguePresenterWiringTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
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

    [Fact]
    public void ShowLine_TransitioningClosedToOpen_PushesTheScreenExactlyOnce()
    {
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraDialoguePresenter(uiView);

        presenter.ShowLine(new DialogueLine("hello"));

        Assert.Single(uiView.Pushed);
        Assert.Empty(uiView.Removed);
    }

    [Fact]
    public void ShowLine_WhileAlreadyOpen_DoesNotPushASecondTime()
    {
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraDialoguePresenter(uiView);

        presenter.ShowLine(new DialogueLine("first"));
        presenter.ShowLine(new DialogueLine("second")); // still open - just updates the line.

        Assert.Single(uiView.Pushed);
    }

    [Fact]
    public void Close_RemovesTheScreenExactlyOnce()
    {
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraDialoguePresenter(uiView);

        presenter.ShowLine(new DialogueLine("hello"));
        var closed = presenter.Close();

        Assert.True(closed);
        Assert.Single(uiView.Removed);
        Assert.Same(uiView.Pushed[0], uiView.Removed[0]); // the SAME screen instance that was pushed.
    }

    [Fact]
    public void ShowChoices_TransitioningClosedToOpen_AlsoPushesTheScreen()
    {
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraDialoguePresenter(uiView);

        presenter.ShowChoices(new[] { "OUI", "NON" });

        Assert.Single(uiView.Pushed);
    }

    [Fact]
    public void CloseWhileNeverOpened_DoesNotRemoveAnything()
    {
        var uiView = new RecordingUIViewRuntime();
        var presenter = new AlundraDialoguePresenter(uiView);

        var closed = presenter.Close();

        Assert.False(closed);
        Assert.Empty(uiView.Removed);
    }

    /// <summary>
    /// Pins the PRODUCTION INSTALL DECISION itself - found by a main-session mutation on the delivered
    /// slice: with <see cref="AlundraWorldProxy.InstallDialogueSystems"/> gutted to never construct the
    /// presenter (AttachToWorld(null, ...)), all 687 tests stayed green while no dialogue box could
    /// ever appear in the game. Fourth occurrence of the green-and-inert wiring family (fade trigger,
    /// audio install, backdrop textures, now this). The wiring tests above drive the presenter
    /// DIRECTLY; this one drives the real install path: a reflected game whose REAL ViewManager holds
    /// an active RenderView carrying a recording UI runtime - the exact chain
    /// world.Game.GameManager.ViewManager.GetActiveUIView() traverses.
    /// </summary>
    [Fact]
    public void InstallDialogueSystems_WithAnActiveUiView_WiresAPresenterThatPushesOnOpen()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        try
        {
            var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
            var game = (CasaEngine.Framework.Application.CasaEngineGame)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Application.CasaEngineGame));
            var componentsField = typeof(Microsoft.Xna.Framework.Game)
                .GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

            var gameManager = (CasaEngine.Framework.Application.GameManager)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Application.GameManager));
            var viewManager = new CasaEngine.Framework.Rendering.ViewManager();
            typeof(CasaEngine.Framework.Application.GameManager)
                .GetField("<ViewManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(gameManager, viewManager);
            typeof(CasaEngine.Framework.Application.CasaEngineGame)
                .GetField("<GameManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(game, gameManager);

            var recorder = new RecordingUIViewRuntime();
            var view = (CasaEngine.Framework.Rendering.RenderView)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Rendering.RenderView));
            view.UIView = recorder;
            view.Enabled = true;
            view.IsVisible = true;
            viewManager.Add(view);
            viewManager.SetActive(view);

            HeroWorldFixture.SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);

            var proxy = new AlundraWorldProxy();
            proxy.InstallDialogueSystems(world);

            Assert.True(AlundraDialogueDirector.Instance.HasPresenter,
                "the real install path must construct and attach the UI presenter when an active UI view exists.");

            AlundraDialogueDirector.Instance.Open("bonjour", 1);
            Assert.True(recorder.Pushed.Count > 0,
                "opening a dialogue after the real install must push the screen on the game's active UI view.");
        }
        finally
        {
            AlundraDialogueDirector.Instance.ResetForTests();
        }
    }

    /// <summary>
    /// The REAL GAME's wiring route (user-reported in-game failure: no dialogue box ever appeared).
    /// In a real run the engine's boot order guarantees GetActiveUIView() is null during
    /// InitializeWithWorld (GameManager.cs:93-108: ViewManager.Clear -> World.LoadContent [which runs
    /// the install] -> BootstrapViews [which creates the view+UIView]), so the eager install above can
    /// never wire anything there - the per-frame retry at the head of AlundraWorldProxy.Update
    /// (TryWireDialoguePresenterOnce) is what actually wires the game. This test reproduces that exact
    /// timing: install first with NO view (presenter null, degraded), THEN the view appears, then one
    /// real Update - the presenter must come live and push on open. Deleting the Update call site (or
    /// regressing to the one-shot-at-first-try lookup shape) fails this test.
    /// </summary>
    [Fact]
    public void Update_WiresThePresenter_OnceTheViewAppearsAfterWorldInit()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        try
        {
            var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
            var game = (CasaEngine.Framework.Application.CasaEngineGame)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Application.CasaEngineGame));
            var componentsField = typeof(Microsoft.Xna.Framework.Game)
                .GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

            var gameManager = (CasaEngine.Framework.Application.GameManager)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Application.GameManager));
            var viewManager = new CasaEngine.Framework.Rendering.ViewManager();
            typeof(CasaEngine.Framework.Application.GameManager)
                .GetField("<ViewManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(gameManager, viewManager);
            typeof(CasaEngine.Framework.Application.CasaEngineGame)
                .GetField("<GameManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(game, gameManager);

            HeroWorldFixture.SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);

            // Phase 1 - the LoadContent window: InitializeWithWorld runs while the ViewManager is
            // still empty (the real game's exact state). The install's eager lookup must find nothing.
            var proxy = new AlundraWorldProxy();
            proxy.InitializeWithWorld(world);
            proxy.InstallDialogueSystems(world); // "TestWorld" has no tileMap, so InitializeWithWorld
                                                 // early-returns before the real install - drive it
                                                 // explicitly in the same empty-ViewManager state.
            Assert.False(AlundraDialogueDirector.Instance.HasPresenter,
                "montage error: no view exists yet, the eager install cannot have wired a presenter.");

            // Even a frame BEFORE the view exists must not wedge the retry (the clear-color shape:
            // guard only set on success - a one-shot lookup would make this miss permanent).
            proxy.Update(1f / 50f);
            Assert.False(AlundraDialogueDirector.Instance.HasPresenter);

            // Phase 2 - BootstrapViews' equivalent: the view (with its UI runtime) appears.
            var recorder = new RecordingUIViewRuntime();
            var view = (CasaEngine.Framework.Rendering.RenderView)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(CasaEngine.Framework.Rendering.RenderView));
            view.UIView = recorder;
            view.Enabled = true;
            view.IsVisible = true;
            viewManager.Add(view);
            viewManager.SetActive(view);

            // Phase 3 - the next real Update wires the presenter and dialogue becomes visible.
            proxy.Update(1f / 50f);

            Assert.True(AlundraDialogueDirector.Instance.HasPresenter,
                "Update's per-frame retry must wire the presenter once the bootstrapped view exists.");

            AlundraDialogueDirector.Instance.Open("bonjour", 1);
            Assert.True(recorder.Pushed.Count > 0,
                "opening a dialogue after the late wiring must push the screen on the game's UI view.");
        }
        finally
        {
            AlundraDialogueDirector.Instance.ResetForTests();
            AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        }
    }
}
