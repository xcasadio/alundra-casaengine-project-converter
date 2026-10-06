#nullable enable
using System;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// E19.f3b (docs/plan-e19-opcodes.md): reads the drawn state of the choice box after a pass (<see cref="AlundraDialogueDirector.ChoiceBox"/>) and writes it into the screen's
/// <see cref="AlundraChoiceViewModel"/>, which <c>ChoiceScreen.xaml</c> binds - one <see cref="Tick"/> right after each pass of the director (which runs the choice pass last),
/// from <see cref="AlundraWorldProxy.Update(float)"/>, after the text box presenter's.
/// </summary>
public sealed class AlundraChoicePresenter
{
    private readonly AlundraDialogueDirector _director;
    private readonly AlundraChoiceViewModel _viewModel;
    private readonly IUIScreen _screen;
    private readonly IUIViewRuntime? _uiView;
    private bool _pushed;

    public AlundraChoicePresenter(
        AlundraDialogueDirector director,
        AlundraChoiceViewModel viewModel,
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
    /// Pushes the screen at the first drawn pass of the choice box (the opener's tick and the init pass draw nothing; the first slide-in pass does, the frame still at the right
    /// edge), writes the view model, and removes the screen the pass the box draws nothing again - the close pass that writes the result, but also an abandoned flow, another box
    /// opened and the map entry, which bring the machine down between two passes. The screen goes up after the text box's (the presenters tick in that order), so it is drawn above it.
    /// </summary>
    public void Tick()
    {
        var choice = _director.ChoiceBox;
        if (choice.Drawn is not null)
        {
            if (!_pushed)
            {
                _uiView?.PushScreen(_screen);
                _pushed = true;
            }

            _viewModel.Apply(choice);
            return;
        }

        if (_pushed)
        {
            _viewModel.Apply(choice);
            _uiView?.RemoveScreen(_screen);
            _pushed = false;
        }
    }
}
