#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>The square button the box reads at one pass.</summary>
internal interface IOraclePad
{
    /// <summary>Called at the start of frame <paramref name="frame"/>, before any phase runs.</summary>
    void StartFrame(int frame, TextBoxOracle box);

    /// <summary>Called after the last phase of frame <paramref name="frame"/>.</summary>
    void EndFrame(int frame, TextBoxOracle box);

    /// <summary>(held, pressed) as the box sees them at the pass of frame <paramref name="frame"/>.</summary>
    (bool Held, bool Pressed) ForBox(int frame);
}
