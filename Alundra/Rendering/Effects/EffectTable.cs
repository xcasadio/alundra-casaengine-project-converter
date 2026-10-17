#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>One effect table (a sprite of the binary's effect bank): its animations and image sets.</summary>
public sealed class EffectTable
{
    public List<EffectAnimation> Animations { get; set; } = new();
    public List<EffectImageSet> ImageSets { get; set; } = new();
}
