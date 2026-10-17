#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>An animation of an effect table: frames <c>[ticks, image set index]</c> and its end (<c>Destroy</c> or <c>Loop</c>).</summary>
public sealed class EffectAnimation
{
    public int[][] Frames { get; set; } = Array.Empty<int[]>();
    public string End { get; set; } = string.Empty;
}
