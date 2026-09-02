#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// User-reported defect (2026-09-02, while validating T2's world freeze): closing a dialogue box through
/// the window's own control made the box vanish but left the world frozen - NPCs immobile and Alundra
/// uncontrollable - until the interact button was pressed as well.
///
/// Cause: the link was asymmetric. <see cref="AlundraDialogueDirector"/> told the presenter when it
/// closed, but a presenter closing on its own told nobody, so the director kept the box open with its
/// MenuOpen flag posted - and MenuOpen is exactly what T2 freezes the entity pass behind.
///
/// Two pins: Alundra opts out of the engine screen's generic "Close" button (that affordance is not part
/// of the game's control scheme), and any close that does come from the window brings the logical box
/// down with it.
/// </summary>
public sealed class AlundraDialogueOutOfBandCloseTests : IDisposable
{
    public AlundraDialogueOutOfBandCloseTests()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
    }

    [Fact]
    public void ThePresenterOptsOutOfTheEngineScreensCloseButton()
    {
        var presenter = new AlundraDialoguePresenter(new RecordingUIViewRuntime());

        Assert.False(presenter.ScreenForTests.ShowCloseButton);
    }

    [Fact]
    public void AWindowDrivenClose_BringsTheLogicalBoxDown_AndClearsTheFreezeFlag()
    {
        var presenter = new AlundraDialoguePresenter(new RecordingUIViewRuntime());
        var state = new AlundraGameState();
        AlundraDialogueDirector.Instance.AttachToWorld(presenter, state);

        // controlMode 0 is the "world closes" box: it posts MenuOpen, which T2 freezes the entity pass
        // behind (AlundraDialogueDirector.SetControlFlags).
        AlundraDialogueDirector.Instance.Open("Bonjour.", controlMode: 0);
        Assert.True(AlundraDialogueDirector.Instance.IsOpen);
        Assert.NotEqual(0u, state.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask);

        // The window's own close control fires - the exact production wiring, not a mirror.
        var requestClose = typeof(AlundraDialoguePresenter)
            .GetMethod("RequestClose", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(requestClose);
        requestClose!.Invoke(presenter, null);

        Assert.False(AlundraDialogueDirector.Instance.IsOpen);
        Assert.Equal(0u, state.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask);
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
}
