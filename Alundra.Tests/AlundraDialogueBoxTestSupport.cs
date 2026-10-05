#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;

namespace Alundra.Tests;

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
    public uint HoldForThisFrame(AlundraDialogueDirector director)
    {
        var hold = director.IsOpen && !(director.Box.IsWaitingForPress && _heldAtThePreviousFrame);
        _heldAtThePreviousFrame = hold;
        return hold ? AlundraPadState.Square : 0;
    }
}

/// <summary>
/// E19.f2a F2A-3: drives the passes of the dialogue box of a test that has no world proxy (<see cref="AlundraDialogueDirector.Tick"/>), counting them from
/// the opening of the box (the opening is pass 0, the first pass of the box is pass 1). The square button follows the plan's rule of the tests: the
/// hold written before the call of pass p is recorded by that call and read by the box at pass p + 1, so "a press seen at pass k" is a hold written
/// before pass k - 1 and gone before pass k (docs/plan-e19-f2a-valeurs.md, A.1).
/// </summary>
internal sealed class DialogueBoxPassDriver
{
    private readonly AlundraDialogueDirector _director;
    private readonly AlundraGameState _gameState;
    private readonly HashSet<int> _holdBefore = new();

    public DialogueBoxPassDriver(AlundraDialogueDirector director, AlundraGameState gameState)
    {
        _director = director;
        _gameState = gameState;
    }

    /// <summary>The passes run so far.</summary>
    public int Pass { get; private set; }

    /// <summary>A press the box sees at pass <paramref name="pass"/>: the square is held for the call of pass <paramref name="pass"/> - 1 only.</summary>
    public void PressSeenAt(int pass) => _holdBefore.Add(pass - 1);

    /// <summary>The square held for the calls of the passes <paramref name="from"/> to <paramref name="to"/>.</summary>
    public void HoldBetween(int from, int to)
    {
        for (var pass = from; pass <= to; pass++)
        {
            _holdBefore.Add(pass);
        }
    }

    /// <summary>One pass.</summary>
    public void Advance()
    {
        Pass++;
        _gameState.LastPadState = new AlundraPadState { ButtonsHold = _holdBefore.Contains(Pass) ? AlundraPadState.Square : 0 };
        _director.Tick();
    }

    /// <summary>Runs the passes up to and including pass <paramref name="pass"/>.</summary>
    public void RunTo(int pass)
    {
        while (Pass < pass)
        {
            Advance();
        }
    }
}
