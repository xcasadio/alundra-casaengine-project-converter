#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>One quad of an effect image set: a window of the effect sheet drawn on four free corners (docs/formats/effects.md).
/// Read by the draw (E19.g G3c), not by the reservoir.</summary>
public sealed class AlundraEffectImage
{
    public int U { get; set; }
    public int V { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    /// <summary>The four corners as eight signed bytes <c>X1, Y1, X2, Y2, X3, Y3, X4, Y4</c> (top-left, top-right, bottom-left, bottom-right; y down).</summary>
    public int[] C { get; set; } = Array.Empty<int>();

    public bool Semi { get; set; }
    public int Abr { get; set; }
}
