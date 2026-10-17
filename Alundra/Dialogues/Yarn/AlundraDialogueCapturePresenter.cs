#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;

namespace Alundra.Scripts;

/// <summary>
/// E15.c T5 (docs/plan-e15-yarn.md, E15.c contract items 2/4; ADR-0008): the presenter
/// <see cref="AlundraDialogueDirector"/>'s own <c>YarnDialogueRunner</c> is built on - never the world's
/// own presenter (<see cref="AlundraDialoguePresenter"/>). The runner calls <see cref="ShowLine"/>/
/// <see cref="Close"/> on THIS presenter itself (<c>YarnDialogueRunner.Start</c> calls <c>Stop</c> first,
/// which closes whatever presenter it was built on - engine <c>YarnDialogueRunner.cs:130-138</c>,
/// <c>:163-167</c>), while the world's own presenter is reattached on every
/// <see cref="AlundraDialogueDirector.AttachToWorld"/>: this capture presenter and the runner it backs
/// persist across such a re-point (<see cref="WorldPresenter"/> is the only thing that changes), so an
/// open dialogue's Yarn state survives it exactly as the raw-text page state used to.
/// <para/>
/// Turns every line it captures into <c>font3</c> text (item 4) before handing it to
/// <see cref="WorldPresenter"/>: <c>[br/]</c> becomes a line break and <c>[glyph id=N/]</c> becomes
/// character <c>N</c> of font3, both inserted from the END of the attribute list so earlier positions
/// stay valid. <c>voice</c>/<c>center</c>/<c>slow</c>/<c>empty</c> are left in the data for the later
/// dialogue-fidelity step (E12.c) and ignored here. Until E19.f2a it also set the flag of every
/// <c>[flag id=N/]</c> marker of the line (E19.f0, D-E19-48) at the display of the page. A non-empty <see cref="DialogueLine.Speaker"/> is
/// logged once - E15.b's corpus proof (<c>YarnCorpusEquivalenceTests</c>) found none on the real export.
/// <para/>
/// E19.f2a (docs/plan-e19-opcodes.md, F2-R3, F2-R6): the director now cuts each page into the steps of its box, sets the flags of a page at their
/// glyph and sends the world presenter only the page typed so far. So when a <see cref="LineSink"/> is given, a line goes to it instead of to
/// <see cref="WorldPresenter"/>, and the runner's closes (a <c>Stop</c> or the end of the dialogue) are swallowed: the director owns the lifetime
/// of the presenter. No flag is set here any more.
/// </summary>
public sealed class AlundraDialogueCapturePresenter : IDialoguePresenter
{
    private static bool _loggedSpeakerOnce;

    /// <param name="worldPresenter">Where every transformed line is forwarded (unless a <see cref="LineSink"/> takes it).</param>
    public AlundraDialogueCapturePresenter(IDialoguePresenter worldPresenter)
    {
        ArgumentNullException.ThrowIfNull(worldPresenter);
        WorldPresenter = worldPresenter;
    }

    /// <summary>E19.f2a: where each line the runner delivers goes INSTEAD of <see cref="WorldPresenter"/> - the director, which cuts it into the steps of its
    /// box. While it is set, <see cref="Close"/> is swallowed too: the director closes the world's presenter itself, at the release of the box.</summary>
    public Action<DialogueLine>? LineSink { get; set; }

    /// <summary>The presenter every transformed line/choice/close is forwarded to - re-pointed by
    /// <see cref="AlundraDialogueDirector.AttachToWorld"/> without rebuilding this instance or the
    /// <c>YarnDialogueRunner</c> it backs, so a dialogue already running keeps its Yarn state.</summary>
    public IDialoguePresenter WorldPresenter { get; set; }

    public DialogueRuntimeState State => WorldPresenter.State;
    public DialogueLine CurrentLine => WorldPresenter.CurrentLine;
    public bool IsOpen => WorldPresenter.IsOpen;
    public IReadOnlyList<string> Choices => WorldPresenter.Choices;
    public bool HasChoices => WorldPresenter.HasChoices;

    // Nobody outside AlundraDialogueDirector observes this capture presenter directly - the world
    // presenter raises its own PresentationChanged/ChoiceSelected for its own subscribers (the engine's
    // DialogueScreen), so these are never actually raised.
    public event EventHandler<DialoguePresentationChangedEventArgs>? PresentationChanged { add { } remove { } }
    public event EventHandler<DialogueChoiceSelectedEventArgs>? ChoiceSelected { add { } remove { } }

    public bool ShowLine(DialogueLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Speaker.Length > 0 && !_loggedSpeakerOnce)
        {
            _loggedSpeakerOnce = true;
            Logs.WriteWarning(
                $"AlundraDialogueCapturePresenter: a Yarn line carried a non-empty Speaker "
                + $"('{line.Speaker}') - E15.b's corpus proof found none; shown without a speaker prefix.");
        }

        if (LineSink != null)
        {
            LineSink(line);
            return true;
        }

        return WorldPresenter.ShowLine(new DialogueLine(ToFont3Text(line)));
    }

    public bool ShowChoices(IReadOnlyList<string> labels) => WorldPresenter.ShowChoices(labels);

    public bool SelectChoice(int index) => WorldPresenter.SelectChoice(index);

    public bool Close() => LineSink != null || WorldPresenter.Close();

    /// <summary>
    /// Item 4: <c>[br/]</c> -&gt; <c>'\n'</c>, <c>[glyph id=N/]</c> -&gt; character <c>N</c> of font3,
    /// both inserted from the end of <see cref="DialogueLine.Attributes"/> so earlier positions are
    /// never shifted by a later insertion; every other attribute (<c>voice</c>, <c>center</c>,
    /// <c>slow</c>, <c>empty</c>) is left alone (E12.c owns their rendering).
    /// </summary>
    internal static string ToFont3Text(DialogueLine line)
    {
        var builder = new StringBuilder(line.Text);

        for (var i = line.Attributes.Count - 1; i >= 0; i--)
        {
            var attribute = line.Attributes[i];
            switch (attribute.Name)
            {
                case "br":
                    builder.Insert(attribute.Position, '\n');
                    break;
                case "glyph":
                    if (attribute.Properties.TryGetValue("id", out var idValue))
                    {
                        var glyphId = Convert.ToInt32(idValue);
                        builder.Insert(attribute.Position, (char)glyphId);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Test-only: clears the "already logged a speaker once" latch (same shape as every other
    /// session-wide "logged once" flag in this DLL) so tests do not leak into each other.</summary>
    internal static void ResetForTests() => _loggedSpeakerOnce = false;
}
