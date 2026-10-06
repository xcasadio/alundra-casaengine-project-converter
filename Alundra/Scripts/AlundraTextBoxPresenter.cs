#nullable enable
using System;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// E19.f2b1c F2B1C-R4 (docs/plan-e19-opcodes.md): reads the drawn state of the dialogue box after a pass (<see cref="AlundraDialogueDirector.Box"/>) and writes it
/// into the screen's <see cref="AlundraTextBoxViewModel"/>, which <c>TextBoxScreen.xaml</c> binds - one <see cref="Tick"/> right after each pass of the box, from
/// <see cref="AlundraWorldProxy.Update(float)"/>.
/// </summary>
public sealed class AlundraTextBoxPresenter
{
    private readonly AlundraDialogueDirector _director;
    private readonly AlundraTextBoxViewModel _viewModel;
    private readonly IUIScreen _screen;
    private readonly IUIViewRuntime? _uiView;
    private bool _pushed;

    public AlundraTextBoxPresenter(
        AlundraDialogueDirector director,
        AlundraTextBoxViewModel viewModel,
        IUIScreen screen,
        IUIViewRuntime? uiView)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(screen);

        _director = director;
        _viewModel = viewModel;
        _screen = screen;
        _uiView = uiView;
    }

    /// <summary>Test-only seam: whether this presenter believes its screen is pushed.</summary>
    internal bool IsPushedForTests => _pushed;

    /// <summary>
    /// Pushes the screen at the first drawn pass of the box (the pass of the opening is not drawn; the first slide-in pass is, the frame still under the screen), writes the
    /// view model, and removes the screen the pass the box is no longer drawn - the release, but also the out-of-band close and the map entry, which bring the box down
    /// between two passes.
    /// </summary>
    public void Tick()
    {
        var box = _director.Box;
        if (box.Drawn)
        {
            if (!_pushed)
            {
                _uiView?.PushScreen(_screen);
                _pushed = true;
            }

            _viewModel.Apply(box);
            return;
        }

        if (_pushed)
        {
            _viewModel.Apply(box);
            _uiView?.RemoveScreen(_screen);
            _pushed = false;
        }
    }
}
