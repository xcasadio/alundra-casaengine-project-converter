#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>A map effect record: the tile zone (inclusive), the flags (<c>0x80</c> map table, <c>0x40</c> appears at load), the table index, the position in the binary's units and the animation.</summary>
public sealed class EffectRecord
{
    public int X1 { get; set; }
    public int X2 { get; set; }
    public int Y1 { get; set; }
    public int Y2 { get; set; }
    public int Flags { get; set; }
    public int Effect { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Anim { get; set; }
}
