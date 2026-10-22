#nullable enable
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Dialogues;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.f2a F2-R1, rule of the buttons of the tests of the intro harness (docs/plan-e19-f2a-valeurs.md, A.2): the callback of a frame, after the pass of the box,
/// holds Square for that frame when a box is open and not (the box waits for a press and Square was held at the frame before); the pass of the next frame reads
/// it. So the square is held while the box types, released for one frame when the box waits (the cursor of <c>\A</c>, or the typing done with the button allowed to
/// close) and pressed again.
/// </summary>
internal sealed class DialogueHarnessButton
{
    private bool _heldAtThePreviousFrame;

    /// <summary>The pad to write for this frame (hold only, no just-pressed: the box reads the hold of the frame before and derives its own edge).</summary>
    public uint HoldForThisFrame(DialogueDirector director)
    {
        var hold = director.IsOpen && !(director.Box.IsWaitingForPress && _heldAtThePreviousFrame);
        _heldAtThePreviousFrame = hold;
        return hold ? PadState.Square : 0;
    }
}
