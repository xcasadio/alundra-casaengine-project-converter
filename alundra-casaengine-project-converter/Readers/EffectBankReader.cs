using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>One map effect record, in the order of the map (its index is the one the script opcodes name).</summary>
public sealed record EffectRecord(
    int X1, int X2, int Y1, int Y2, int Flags, int Effect, int X, int Y, int Z, int Anim);

/// <summary>
/// One image of an effect image set: the window of the effect sheet and the four corners of the quad.
/// <c>C</c> holds X1, Y1, X2, Y2, X3, Y3, X4, Y4 as signed bytes, in the order of the PS1 (top-left,
/// top-right, bottom-left, bottom-right, y down); a mirrored quad is carried by that order.
/// </summary>
public sealed record EffectImage(int U, int V, int W, int H, int[] C, bool Semi, int Abr);

/// <summary>An image set shared by one or more frames of one table; <c>Idsv</c> is the depth bias of the sort key (shifted left by 16).</summary>
public sealed record EffectImageSet(int Idsv, IReadOnlyList<EffectImage> Images);

/// <summary>One animation: displayed frames as [ticks, image set index], and how it ends ("Destroy" or "Loop").</summary>
public sealed record EffectAnimation(IReadOnlyList<int[]> Frames, string End);

/// <summary>One effect table: animations by case index, image sets by order of first use.</summary>
public sealed record EffectTable(IReadOnlyList<EffectAnimation> Animations, IReadOnlyList<EffectImageSet> ImageSets);

/// <summary>What one map JSON holds about effects (<c>SpriteInfo.MapEffectRecords</c> and <c>SpriteInfo.SpriteEffectRecords</c>).</summary>
public sealed class EffectBank
{
    public List<EffectRecord> Records { get; } = new();

    public List<EffectTable> Tables { get; } = new();

    /// <summary>Trailing zero-offset cases of the animation tables, removed (they are never named by a record or a script).</summary>
    public int AnimationSlotsDropped { get; set; }

    /// <summary>All-zero images (window 0 x 0, corners all 0) removed from their set.</summary>
    public int ImagesDegenerateDropped { get; set; }

    public int Frames { get; set; }

    public int Images { get; set; }

    public List<string> Errors { get; } = new();
}

/// <summary>
/// Reads the effect records and the effect tables of one map JSON (docs/formats/effects.md, rules G1-R1 to G1-R4).
/// It does not reuse <see cref="SpriteBankReader"/>: its de-duplication and its fields are entity-oriented.
/// </summary>
public static class EffectBankReader
{
    public static EffectBank Read(string nativeMapPath) => throw new NotImplementedException();

    public static EffectBank Read(JsonElement mapRoot) => throw new NotImplementedException();
}
