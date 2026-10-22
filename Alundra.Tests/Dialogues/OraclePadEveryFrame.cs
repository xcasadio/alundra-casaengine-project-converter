#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>The binary model's pad A: held and just pressed on every pass. Impossible on a real pad; only the values of the discovery's selftest use it.</summary>
internal sealed class OraclePadEveryFrame : IOraclePad
{
    public void StartFrame(int frame, TextBoxOracle box) { }
    public void EndFrame(int frame, TextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => (true, true);
}
