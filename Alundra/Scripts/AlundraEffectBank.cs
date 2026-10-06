#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

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

/// <summary>An image set of an effect table: the depth bias of the sort key and its quads, in the order of the binary.</summary>
public sealed class AlundraEffectImageSet
{
    public int Idsv { get; set; }
    public List<AlundraEffectImage> Images { get; set; } = new();
}

/// <summary>An animation of an effect table: frames <c>[ticks, image set index]</c> and its end (<c>Destroy</c> or <c>Loop</c>).</summary>
public sealed class AlundraEffectAnimation
{
    public int[][] Frames { get; set; } = Array.Empty<int[]>();
    public string End { get; set; } = string.Empty;
}

/// <summary>One effect table (a sprite of the binary's effect bank): its animations and image sets.</summary>
public sealed class AlundraEffectTable
{
    public List<AlundraEffectAnimation> Animations { get; set; } = new();
    public List<AlundraEffectImageSet> ImageSets { get; set; } = new();
}

/// <summary>A map effect record: the tile zone (inclusive), the flags (<c>0x80</c> map table, <c>0x40</c> appears at load), the table index, the position in the binary's units and the animation.</summary>
public sealed class AlundraEffectRecord
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

/// <summary>The effect companion of a map (<c>*.effects.json</c>) or the global one (<c>effects-global.json</c>, records absent), see <c>docs/formats/effects.md</c>.</summary>
public sealed class AlundraEffectDocument
{
    public int? MapIndex { get; set; }
    public string? SheetTextureAssetId { get; set; }
    public List<AlundraEffectRecord> Records { get; set; } = new();
    public List<AlundraEffectTable> Effects { get; set; } = new();
}

/// <summary>The effect data of a world: the map companion (records and map tables) and the global tables.</summary>
public sealed class AlundraEffectBank
{
    public static AlundraEffectBank Empty { get; } = new(null, null);

    public AlundraEffectBank(AlundraEffectDocument? map, AlundraEffectDocument? global)
    {
        throw new NotImplementedException();
    }

    public AlundraEffectDocument? Map => throw new NotImplementedException();
    public AlundraEffectDocument? Global => throw new NotImplementedException();
    public IReadOnlyList<AlundraEffectRecord> Records => throw new NotImplementedException();

    public bool TryGetTable(bool mapTable, int effect, out AlundraEffectTable table) => throw new NotImplementedException();
}

/// <summary>Loads the effect data of a world (see <see cref="AlundraEffectBank"/>).</summary>
public static class AlundraEffectBankLoader
{
    public static AlundraEffectBank Load(string? projectPath, string? worldName) => throw new NotImplementedException();

    public static void ResetForTests() => throw new NotImplementedException();
}
