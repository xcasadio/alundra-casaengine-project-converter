#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// The pad of the arcs' helper and of the harness, the same rule (plan, "Manette des arcs"): Square is held at frame f when a box is open and not
/// (the box waits for a press and Square was held at f-1) - held during the typing, then a release of one frame and a press when the box waits.
/// The two differ only by when it is decided: the arcs' helper at the start of the frame f (the box of f+1 reads it: two frames behind the state it
/// read), the harness's callback after the box pass of f (the box of f+1 reads it: one frame behind).
/// </summary>
internal sealed class OracleRulePad : IOraclePad
{
    private readonly bool _decidedAtStartOfFrame;
    private readonly Dictionary<int, bool> _held = new();

    private OracleRulePad(bool decidedAtStartOfFrame) => _decidedAtStartOfFrame = decidedAtStartOfFrame;

    public static OracleRulePad ForArcs() => new(true);

    public static OracleRulePad ForHarness() => new(false);

    private void Decide(int frame, TextBoxOracle box)
    {
        _held.TryGetValue(frame - 1, out var previous);
        _held[frame] = box.IsActive && !(box.WaitingForPress && previous);
    }

    public void StartFrame(int frame, TextBoxOracle box)
    {
        if (_decidedAtStartOfFrame)
        {
            Decide(frame, box);
        }
    }

    public void EndFrame(int frame, TextBoxOracle box)
    {
        if (!_decidedAtStartOfFrame)
        {
            Decide(frame, box);
        }
    }

    public (bool, bool) ForBox(int frame)
    {
        _held.TryGetValue(frame - 1, out var h1);
        _held.TryGetValue(frame - 2, out var h2);
        return (h1, h1 && !h2);
    }

    /// <summary>The frames at which Square is pressed (a rising edge of the held state).</summary>
    public List<int> Presses() => _held.Where(kv => kv.Value && !(_held.TryGetValue(kv.Key - 1, out var p) && p)).Select(kv => kv.Key).OrderBy(f => f).ToList();
}
