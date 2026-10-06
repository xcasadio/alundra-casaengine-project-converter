#nullable enable
using System;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// E19.f2b1c F2B1C-R4 (docs/plan-e19-opcodes.md): reads the drawn state of the dialogue box after a pass (<see cref="AlundraDialogueDirector.Box"/>) and, since E19.f4c2, of the
/// speaker's name box and portrait (<see cref="AlundraDialogueDirector.NameBox"/>, <see cref="AlundraDialogueDirector.Portrait"/>), and writes it
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
    /// Pushes the screen at the first pass where the box, the speaker's name or the speaker's portrait is drawn (the pass of the opening is not drawn; the first slide-in pass is, the frame
    /// still under the screen), writes the view model once per tick, and removes the screen the first pass where none of the three is drawn - the release of the last box, but also
    /// the out-of-band close and the map entry, which bring everything down between two passes. The union (E19.f4c2) keeps the screen up on the pass where only the speaker of a second
    /// dialogue is drawn (its box releases the pass before it draws) and for a portrait left at rest with no box, as the binary keeps drawing it until a map entry or a close.
    /// </summary>
    public void Tick()
    {
        var box = _director.Box;
        var speakerDrawn = _director.NameBox.Drawn is not null || _director.Portrait.DrawnThisStep;
        if (box.Drawn || speakerDrawn)
        {
            if (!_pushed)
            {
                _uiView?.PushScreen(_screen);
                _pushed = true;
            }

            Write(box, speakerDrawn);
            return;
        }

        if (_pushed)
        {
            Write(box, false);
            _uiView?.RemoveScreen(_screen);
            _pushed = false;
        }
    }

    /// <summary>The speaker first (its two view models), then the box: one <see cref="AlundraTextBoxViewModel.Apply(AlundraDialogueBox, bool)"/> per tick.</summary>
    private void Write(AlundraDialogueBox box, bool speakerDrawn)
    {
        _viewModel.NameBox.Apply(_director.NameBox);
        _viewModel.Portrait.ApplyDialogue(_director.Portrait, _director.PortraitSource);
        _viewModel.Apply(box, speakerDrawn);
    }
}
