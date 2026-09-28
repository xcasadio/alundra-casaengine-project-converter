#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c T5 (docs/plan-e15-yarn.md, contract item 4): unit coverage of
/// <see cref="AlundraDialogueCapturePresenter.ToFont3Text"/> itself, built directly from a
/// <see cref="DialogueLine"/> with hand-placed <see cref="DialogueMarkupAttribute"/> entries - never
/// through a compiled Yarn asset (<see cref="AlundraDialogueYarnRenderingTests"/> covers the
/// end-to-end glyph/br rendering through the real director).
/// </summary>
public sealed class AlundraDialogueCapturePresenterTests
{
    private static DialogueMarkupAttribute Glyph(int position, int id) =>
        new("glyph", position, length: 0, new Dictionary<string, object> { ["id"] = id });

    private static DialogueMarkupAttribute Br(int position) =>
        new("br", position, length: 0, new Dictionary<string, object>());

    /// <summary>
    /// The real corpus shape of M135_S090_p3: "Mais[slow/][glyph id=18/][slow/][glyph id=18/][slow/]il"
    /// plus a later "[br/]" - TWO glyphs and a br share ONE position (right after "Mais", the "slow"
    /// markers carry zero width and are ignored here), and another glyph sits at a LATER position. Since
    /// <see cref="AlundraDialogueCapturePresenter.ToFont3Text"/> inserts from the END of the attribute
    /// list backwards, every insertion at a smaller position must still land BEFORE the text that comes
    /// after it, and same-position insertions must keep their OWN source (attribute-list) order rather
    /// than being reversed by the end-first walk.
    /// </summary>
    [Fact]
    public void EndFirstInsertion_KeepsSourceOrder_ForSharedAndLaterPositions()
    {
        // "Mais" + "il" = text with the two same-position glyphs and the br both inserted at position 4,
        // in source order glyph(18), glyph(18), br - plus a third glyph at position 6 (inside "il").
        var text = "Maisil";
        var attributes = new List<DialogueMarkupAttribute>
        {
            Glyph(4, 18),
            Glyph(4, 18),
            Br(4),
            Glyph(6, 20),
        };
        var line = new DialogueLine(text, string.Empty, attributes);

        var result = AlundraDialogueCapturePresenter.ToFont3Text(line);

        // Position 4 gets, in source order: glyph 18, glyph 18, then '\n' - all still before "il". Position
        // 6 is the end of "il" (text length is 6), so glyph(6, 20) appends after "il" entirely.
        var expected = "Mais" + (char)18 + (char)18 + '\n' + "il" + (char)20;

        Assert.Equal(expected, result);
    }

    /// <summary>A single <c>[br/]</c> elsewhere in the same line, past every glyph, still becomes its own
    /// line break at its own position - the shape M135_S090_p3 actually has (a br later in the page, not
    /// only the shared one above).</summary>
    [Fact]
    public void BrAfterGlyphs_AtALaterPosition_BecomesItsOwnLineBreak()
    {
        var text = "Maisil suite";
        var attributes = new List<DialogueMarkupAttribute>
        {
            Glyph(4, 18),
            Glyph(4, 18),
            Br(9), // right after "Maisil su" (9 chars) -> before "ite".
        };
        var line = new DialogueLine(text, string.Empty, attributes);

        var result = AlundraDialogueCapturePresenter.ToFont3Text(line);

        Assert.Equal("Mais" + (char)18 + (char)18 + "il su\nite", result);
    }
}
