#nullable enable
using System.Collections.Generic;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Dialogues;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.f2a F2A-3: drives the passes of the dialogue box of a test that has no world proxy (<see cref="DialogueDirector.Tick"/>), counting them from
/// the opening of the box (the opening is pass 0, the first pass of the box is pass 1). The square button follows the plan's rule of the tests: the
/// hold written before the call of pass p is recorded by that call and read by the box at pass p + 1, so "a press seen at pass k" is a hold written
/// before pass k - 1 and gone before pass k (docs/plan-e19-f2a-valeurs.md, A.1).
/// </summary>
internal sealed class DialogueBoxPassDriver
{
    private readonly DialogueDirector _director;
    private readonly GameState _gameState;
    private readonly HashSet<int> _holdBefore = new();

    public DialogueBoxPassDriver(DialogueDirector director, GameState gameState)
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
        _gameState.LastPadState = new PadState { ButtonsHold = _holdBefore.Contains(Pass) ? PadState.Square : 0 };
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
