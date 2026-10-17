#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>The effect companion of a map (<c>*.effects.json</c>) or the global one (<c>effects-global.json</c>, records absent), see <c>docs/formats/effects.md</c>.</summary>
public sealed class AlundraEffectDocument
{
    public int? MapIndex { get; set; }
    public string? SheetTextureAssetId { get; set; }
    public List<AlundraEffectRecord> Records { get; set; } = new();
    public List<AlundraEffectTable> Effects { get; set; } = new();

    /// <summary>A member written as <c>null</c> reads as an empty list.</summary>
    internal void Normalize()
    {
        Records ??= new();
        Effects ??= new();
        foreach (var table in Effects)
        {
            table.Animations ??= new();
            table.ImageSets ??= new();
            foreach (var animation in table.Animations)
            {
                animation.Frames ??= Array.Empty<int[]>();
                animation.End ??= string.Empty;
            }

            foreach (var set in table.ImageSets)
            {
                set.Images ??= new();
            }
        }
    }
}
