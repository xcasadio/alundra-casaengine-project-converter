#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>An image set of an effect table: the depth bias of the sort key and its quads, in the order of the binary.</summary>
public sealed class AlundraEffectImageSet
{
    public int Idsv { get; set; }
    public List<AlundraEffectImage> Images { get; set; } = new();
}
