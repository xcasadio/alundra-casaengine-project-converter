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
    private const int DisplayedFrameFlag = 0x80;
    private const int TicksMask = 0x7F;

    public static EffectBank Read(string nativeMapPath)
    {
        using var stream = File.OpenRead(nativeMapPath);
        using var document = JsonDocument.Parse(stream);
        return Read(document.RootElement);
    }

    public static EffectBank Read(JsonElement mapRoot)
    {
        var bank = new EffectBank();

        if (!mapRoot.TryGetProperty("SpriteInfo", out var spriteInfo) || spriteInfo.ValueKind != JsonValueKind.Object)
        {
            return bank;
        }

        if (TryGetArray(spriteInfo, "MapEffectRecords", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                bank.Records.Add(new EffectRecord(
                    Int(record, "X1"), Int(record, "X2"), Int(record, "Y1"), Int(record, "Y2"),
                    Int(record, "Flags"), Int(record, "EffectId"),
                    Int(record, "X"), Int(record, "Y"), Int(record, "Z"), Int(record, "AnimId")));
            }
        }

        if (TryGetArray(spriteInfo, "SpriteEffectRecords", out var tables))
        {
            var tableIndex = 0;
            foreach (var table in tables.EnumerateArray())
            {
                bank.Tables.Add(ReadTable(table, tableIndex, bank));
                tableIndex++;
            }
        }

        return bank;
    }

    private static EffectTable ReadTable(JsonElement table, int tableIndex, EffectBank bank)
    {
        var animations = new List<EffectAnimation>();
        var imageSets = new List<EffectImageSet>();

        if (table.ValueKind != JsonValueKind.Object
            || !TryGetArray(table, "AnimationOffsets", out var offsets)
            || !TryGetArray(table, "PreloadedAnims", out var preloaded))
        {
            return new EffectTable(animations, imageSets);
        }

        // The cases of a table are AnimationOffsets[0 .. AnimationCount): the array is longer and the
        // entries beyond the count are not cases. Case i is PreloadedAnims[i].
        var count = Math.Min(Int(table, "AnimationCount"), offsets.GetArrayLength());
        var offsetValues = offsets.EnumerateArray().Take(count).Select(offset => offset.GetInt32()).ToArray();
        var preloadedAnims = preloaded.EnumerateArray().ToArray();

        var realCount = 0;
        var seenPadding = false;
        for (var slot = 0; slot < count; slot++)
        {
            if (offsetValues[slot] == 0)
            {
                seenPadding = true;
            }
            else if (seenPadding)
            {
                bank.Errors.Add($"table {tableIndex}: animation slot {slot} follows a zero offset (hole in the case table).");
                realCount = -1;
                break;
            }
            else
            {
                realCount = slot + 1;
            }
        }

        if (realCount < 0)
        {
            return new EffectTable(animations, imageSets);
        }

        bank.AnimationSlotsDropped += count - realCount;

        // Image sets are de-duplicated per table on ImageSetPointer (a table-relative offset), in order of first use.
        var setIndexByPointer = new Dictionary<int, int>();

        for (var slot = 0; slot < realCount; slot++)
        {
            if (slot >= preloadedAnims.Length
                || !TryGetArray(preloadedAnims[slot], "Frames", out var frames)
                || frames.GetArrayLength() == 0)
            {
                bank.Errors.Add($"table {tableIndex}: animation {slot} has no frames.");
                animations.Add(new EffectAnimation(Array.Empty<int[]>(), "Destroy"));
                continue;
            }

            var frameElements = frames.EnumerateArray().ToArray();
            var outputFrames = new List<int[]>(frameElements.Length - 1);

            // The last entry is the end pseudo-frame: raw delay 0 destroys the effect, raw delay 1 loops it.
            var endDelay = Int(frameElements[^1], "Delay");
            string end;
            switch (endDelay)
            {
                case 0:
                    end = "Destroy";
                    break;
                case 1:
                    end = "Loop";
                    break;
                default:
                    bank.Errors.Add($"table {tableIndex}: animation {slot} ends with the unknown delay {endDelay}.");
                    end = "Destroy";
                    break;
            }

            for (var index = 0; index < frameElements.Length - 1; index++)
            {
                var frame = frameElements[index];
                var delay = Int(frame, "Delay");
                var ticks = delay & TicksMask;
                if ((delay & DisplayedFrameFlag) == 0 || ticks == 0)
                {
                    bank.Errors.Add($"table {tableIndex}: animation {slot} frame {index} has the raw delay {delay} (expected 0x80 | 1..127).");
                    continue;
                }

                var pointer = Int(frame, "ImageSetPointer");
                if (!setIndexByPointer.TryGetValue(pointer, out var setIndex))
                {
                    setIndex = imageSets.Count;
                    setIndexByPointer[pointer] = setIndex;
                    imageSets.Add(ReadImageSet(frame, bank));
                }

                outputFrames.Add(new[] { ticks, setIndex });
                bank.Frames++;
            }

            animations.Add(new EffectAnimation(outputFrames, end));
        }

        return new EffectTable(animations, imageSets);
    }

    private static EffectImageSet ReadImageSet(JsonElement frame, EffectBank bank)
    {
        var images = new List<EffectImage>();
        var idsv = 0;

        if (frame.TryGetProperty("Images", out var set) && set.ValueKind == JsonValueKind.Object)
        {
            idsv = Int(set, "DepthSortValue");

            if (TryGetArray(set, "Images", out var quads))
            {
                foreach (var quad in quads.EnumerateArray())
                {
                    var width = Int(quad, "Swidth");
                    var height = Int(quad, "Sheight");
                    var corners = new[]
                    {
                        Int(quad, "X1"), Int(quad, "Y1"), Int(quad, "X2"), Int(quad, "Y2"),
                        Int(quad, "X3"), Int(quad, "Y3"), Int(quad, "X4"), Int(quad, "Y4"),
                    };

                    if (width == 0 && height == 0 && corners.All(corner => corner == 0))
                    {
                        bank.ImagesDegenerateDropped++;
                        continue;
                    }

                    var spritesheet = Int(quad, "Spritesheet");
                    images.Add(new EffectImage(
                        Int(quad, "AtlasX"), Int(quad, "AtlasY"), width, height, corners,
                        (spritesheet & 0x08) != 0, (spritesheet >> 4) & 0x03));
                    bank.Images++;
                }
            }
        }

        return new EffectImageSet(idsv, images);
    }

    private static bool TryGetArray(JsonElement parent, string name, out JsonElement array)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static int Int(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object
           && parent.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
