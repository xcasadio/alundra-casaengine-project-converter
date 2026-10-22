#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>No button at all.</summary>
internal sealed class OraclePadNone : IOraclePad
{
    public void StartFrame(int frame, TextBoxOracle box) { }
    public void EndFrame(int frame, TextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => (false, false);
}
