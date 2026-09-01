#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// docs/plan-e12-dialogues.md, slice E12.a, item ③bis - THE UI WIRING LINK (blocking P2 of relecture: the
/// "jamais dessiné" family already caught two prior slices, fondu and backdrops). Wraps the engine's own
/// generic mechanism (<see cref="DialogueService"/> + <see cref="DialogueScreen"/>, D-E12-5) and pushes/
/// removes that screen on the ACTIVE UI view (<see cref="CasaEngine.Framework.Application.GameManager.ViewManager"/>'s
/// own <c>GetActiveUIView()</c> - the SAME route <c>UIOverlayDemo</c> itself uses) the moment a dialogue
/// opens/closes, so <see cref="AlundraDialogueDirector"/> never has to know anything about
/// <see cref="IUIViewRuntime"/> at all - it only ever sees the generic <see cref="IDialoguePresenter"/>
/// surface.
///
/// <b>Why <see cref="IUIViewRuntime"/>, not a live <c>ScreenStack</c>/<c>UIRoot"</c> (relecture correction,
/// closing round)</b>: an earlier draft of this class's own test claimed a real <c>ScreenStack</c> could
/// be driven headlessly - false: <c>ScreenStack.Push</c> initializes the pushed screen against a
/// <c>UIRoot</c> that requires the live graphics stack. <see cref="IUIViewRuntime"/> is the interface the
/// engine's OWN demo (<c>UIOverlayDemo</c>) already consumes for exactly this push/remove pair, so a
/// recording test double of THIS interface (not of <c>ScreenStack</c>) is what proves the wiring without
/// needing a live graphics device - see <c>AlundraDialoguePresenterWiringTests</c>.
///
/// Constructed and wired ENTIRELY inside <see cref="AlundraWorldProxy.InstallDialogueSystems"/> (no
/// separable call site, the M16 lesson every install method in this DLL already follows) - never held
/// directly by <see cref="AlundraEventProgramRunner"/>, which only ever sees it through the generic
/// <see cref="IDialoguePresenter"/> reference <see cref="AlundraDialogueDirector"/> was attached to.
/// </summary>
public sealed class AlundraDialoguePresenter : IDialoguePresenter
{
    private readonly DialogueService _service = new();
    private readonly IUIViewRuntime _uiView;
    private readonly DialogueScreen _screen;
    private bool _pushed;

    public AlundraDialoguePresenter(IUIViewRuntime uiView, string? fontFamily = null)
    {
        ArgumentNullException.ThrowIfNull(uiView);
        _uiView = uiView;
        _screen = new DialogueScreen(_service, RequestClose, fontFamily!);
    }

    public DialogueRuntimeState State => _service.State;
    public DialogueLine CurrentLine => _service.CurrentLine;
    public bool IsOpen => _service.IsOpen;
    public IReadOnlyList<string> Choices => _service.Choices;
    public bool HasChoices => _service.HasChoices;

    public event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged
    {
        add => _service.PresentationChanged += value;
        remove => _service.PresentationChanged -= value;
    }

    public event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected
    {
        add => _service.ChoiceSelected += value;
        remove => _service.ChoiceSelected -= value;
    }

    /// <summary>Pushes <see cref="_screen"/> the moment the wrapped <see cref="DialogueService"/>
    /// transitions Closed -&gt; Open (mutation target: skip this push and the wiring test dies).</summary>
    public bool ShowLine(DialogueLine line)
    {
        var wasOpen = _service.IsOpen;
        var result = _service.ShowLine(line);

        if (!wasOpen && _service.IsOpen)
        {
            PushScreenIfNeeded();
        }

        return result;
    }

    public bool ShowChoices(IReadOnlyList<string> labels)
    {
        var wasOpen = _service.IsOpen;
        var result = _service.ShowChoices(labels);

        if (!wasOpen && _service.IsOpen)
        {
            PushScreenIfNeeded();
        }

        return result;
    }

    public bool SelectChoice(int index) => _service.SelectChoice(index);

    /// <summary>Removes <see cref="_screen"/> the moment the box actually closes (mutation target: skip
    /// this remove and the wiring test dies).</summary>
    public bool Close()
    {
        var result = _service.Close();
        RemoveScreenIfPushed();
        return result;
    }

    private void PushScreenIfNeeded()
    {
        if (_pushed)
        {
            return;
        }

        _uiView.PushScreen(_screen);
        _pushed = true;
    }

    private void RemoveScreenIfPushed()
    {
        if (!_pushed)
        {
            return;
        }

        _uiView.RemoveScreen(_screen);
        _pushed = false;
    }

    /// <summary>The MGUI window's own close control (e.g. a title-bar X) - not part of Alundra's own
    /// control scheme (the interact button drives every real close, via <see cref="AlundraDialogueDirector"/>),
    /// but <see cref="DialogueScreen"/>'s constructor requires an <see cref="Action"/> regardless; wired to
    /// this presenter's own <see cref="Close"/> so the UI stays consistent with itself if it is ever
    /// triggered.</summary>
    private void RequestClose() => Close();
}
