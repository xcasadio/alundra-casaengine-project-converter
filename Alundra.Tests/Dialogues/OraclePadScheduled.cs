#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>An explicit pad: the (held, pressed) the box sees at each pass.</summary>
internal sealed class OraclePadScheduled : IOraclePad
{
    private readonly Dictionary<int, (bool Held, bool Pressed)> _seen;

    public OraclePadScheduled(Dictionary<int, (bool Held, bool Pressed)> seen) => _seen = seen;
    public void StartFrame(int frame, TextBoxOracle box) { }
    public void EndFrame(int frame, TextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => _seen.TryGetValue(frame, out var v) ? v : (false, false);
}
