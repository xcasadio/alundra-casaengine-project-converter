using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

public sealed class SpriteQuad
{
    public int Spritesheet;
    public int Palette;
    public int AtlasX;
    public int AtlasY;
    public int Width;
    public int Height;
    public int X1, Y1, X2, Y2, X3, Y3, X4, Y4;
    public long Signature;
}

/// <summary>
/// One frame's 3D collision volume (SiFrame.CollisionData), exactly as the extractor exports it.
/// Offset is the box's MIN CORNER, in pixels, relative to the entity's feet; Width/Depth/Height are
/// its extents along X (east), Y (ground depth) and Z (elevation). Most frames carry none.
/// </summary>
public sealed class SpriteFrameCollision
{
    public int OffsetX;
    public int OffsetY;
    public int OffsetZ;
    public int Width;
    public int Depth;
    public int Height;

    public bool HasSameVolumeAs(SpriteFrameCollision other)
    {
        return OffsetX == other.OffsetX
               && OffsetY == other.OffsetY
               && OffsetZ == other.OffsetZ
               && Width == other.Width
               && Depth == other.Depth
               && Height == other.Height;
    }
}

public sealed class SpriteFrame
{
    /// <summary>
    /// How long this frame is held, in game frames (the source is PAL, so 50 Hz).
    ///
    /// Only the low 7 bits of SiFrame.Delay carry the duration - bit 7 marks a frame that plays
    /// for that long and then advances, and EntityManager.UpdateAnimation reads it as
    /// <c>Delay &amp; 0x7f</c> everywhere. Every one of the 376441 displayable frames in the game
    /// sets that bit, so taking the byte at face value stretched every animation by a factor of
    /// 17 to 129 (a 0x88 walk frame is 8 game frames, not 136).
    /// </summary>
    public int DelayFrames;

    /// <summary>
    /// Raw SiFrame.Delay of the animation's trailing frame, which carries no images and no
    /// duration: it is a control code. 1 loops back to frame 0; anything else (always 0 in the
    /// shipped data) chains to the animation named by the frame's TransformIndexLow. All 92452
    /// animations end with exactly one such frame.
    /// </summary>
    public int TerminatorCode;

    public IReadOnlyList<SpriteQuad> Quads = Array.Empty<SpriteQuad>();

    /// <summary>
    /// The frame's 3D collision volume, or null when the frame declares none (the common case).
    /// </summary>
    public SpriteFrameCollision? Collision;

    public bool IsTerminator => Quads.Count == 0;
}

public sealed class SpriteAnimation
{
    public IReadOnlyList<SpriteFrame> Frames = Array.Empty<SpriteFrame>();
}

/// <summary>
/// The body volume declared by a sprite record's header (SpriteRecord.Header), exactly as the
/// extractor exports it. Unlike <see cref="SpriteFrameCollision"/>, which changes frame by frame,
/// this one belongs to the entity as a whole: Offset is the box's MIN CORNER, in pixels, relative
/// to the entity's feet, and SizeX/SizeY/SizeZ are its extents along X (east), Y (ground depth) and
/// Z (elevation). A record may declare a degenerate box (any size 0), which means "no body".
/// </summary>
public sealed class SpriteBodyBox
{
    public int OffsetX;
    public int OffsetY;
    public int OffsetZ;
    public int SizeX;
    public int SizeY;
    public int SizeZ;

    public bool HasPositiveVolume => SizeX > 0 && SizeY > 0 && SizeZ > 0;

    public bool HasSameVolumeAs(SpriteBodyBox other)
    {
        return OffsetX == other.OffsetX
               && OffsetY == other.OffsetY
               && OffsetZ == other.OffsetZ
               && SizeX == other.SizeX
               && SizeY == other.SizeY
               && SizeZ == other.SizeZ;
    }
}

/// <summary>
/// One deduplicated Alundra sprite bank (Header.Sector5Id). AnimSets[j] holds up to 4 directions
/// (index 0=Down, 1=Up, 2=Left, 3=Right, matching SiAnimDir/AnimationSet.PreloadedAnims in the
/// extractor) - an entry is null if that direction has no frames.
/// </summary>
public sealed class SpriteBank
{
    public int Sector5Id;
    // Sector5Id is only unique within its own source: the hero's SpriteRecords (map_alundra.json)
    // and every regular map's SpriteRecords are separate id spaces that happen to reuse the same
    // small integers (verified against real data: hero id 82 is an unrelated 2-frame bank, not
    // the map_4/map_5 NPC bank also numbered 82). BankKey keeps them from colliding.
    public bool IsHeroBank;
    public int SourceMapIndex; // -1 for the hero bank (map_alundra.json)
    public string SourceSpritesheetFileName = string.Empty;
    public IReadOnlyList<SpriteAnimation?[]> AnimSets = Array.Empty<SpriteAnimation?[]>();

    /// <summary>
    /// The header's body volume, or null when the record declares none. Read from the first record
    /// this bank was found in, like every other field of the bank.
    /// </summary>
    public SpriteBodyBox? BodyBox;

    public string BankKey => IsHeroBank ? $"hero_{Sector5Id}" : $"{Sector5Id}";
}

/// <summary>
/// Reads every unique sprite bank across data-extracted. The same Sector5Id bank is duplicated
/// verbatim in every regular map that uses it (verified against real data before relying on it),
/// so the first map a bank is found in becomes its canonical source for both animation data and
/// the spritesheet texture it references. The hero bank(s) from map_alundra.json are read
/// separately since they are not part of the same id space (see SpriteBank.BankKey).
/// </summary>
public static class SpriteBankReader
{
    public static IReadOnlyList<SpriteBank> ReadAllBanks(string inputDirectory, ConversionReport report)
    {
        var banksByKey = new Dictionary<string, SpriteBank>();

        var heroPath = Path.Combine(inputDirectory, "data", "map_alundra.json");
        if (File.Exists(heroPath))
        {
            ReadMapSpriteRecords(heroPath, mapIndex: -1, "map_alundra_spritesheet.png", isHeroBank: true, banksByKey, report);
        }
        else
        {
            report.Warnings.Add($"Hero bank source not found at '{heroPath}'.");
        }

        foreach (var mapIndex in DiscoverNativeMapIndices(inputDirectory))
        {
            var mapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
            if (!File.Exists(mapPath))
            {
                report.Warnings.Add($"map_{mapIndex}: native map file not found at '{mapPath}'.");
                continue;
            }

            ReadMapSpriteRecords(mapPath, mapIndex, $"map_{mapIndex}_spritesheet.png", isHeroBank: false, banksByKey, report);
        }

        return banksByKey.Values.OrderBy(bank => bank.IsHeroBank).ThenBy(bank => bank.Sector5Id).ToList();
    }

    private static void ReadMapSpriteRecords(
        string mapPath, int mapIndex, string spritesheetFileName, bool isHeroBank,
        Dictionary<string, SpriteBank> banksByKey,
        ConversionReport report)
    {
        using var stream = File.OpenRead(mapPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("SpriteInfo", out var spriteInfoElement)
            || !spriteInfoElement.TryGetProperty("SpriteRecords", out var recordsElement)
            || recordsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var recordElement in recordsElement.EnumerateArray())
        {
            if (recordElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var headerElement = recordElement.GetProperty("Header");
            var sector5Id = headerElement.GetProperty("Sector5Id").GetInt32();
            var bankKey = isHeroBank ? $"hero_{sector5Id}" : $"{sector5Id}";
            if (banksByKey.TryGetValue(bankKey, out var existingBank))
            {
                // Same content across maps (verified); first occurrence is canonical. The body box
                // is checked rather than assumed: a later map disagreeing about it is ignored, but
                // counted, so the assumption stays measurable instead of silent.
                var duplicateBodyBox = ReadBodyBox(headerElement);
                if (!SameBodyBox(existingBank.BodyBox, duplicateBodyBox))
                {
                    report.Increment("Sprites.BodyBoxConflicts");
                }

                continue;
            }

            if (!recordElement.TryGetProperty("AnimSets", out var animSetsElement)
                || animSetsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var animSets = new List<SpriteAnimation?[]>();
            foreach (var animSetElement in animSetsElement.EnumerateArray())
            {
                animSets.Add(ReadAnimSet(animSetElement));
            }

            banksByKey[bankKey] = new SpriteBank
            {
                Sector5Id = sector5Id,
                IsHeroBank = isHeroBank,
                SourceMapIndex = mapIndex,
                SourceSpritesheetFileName = spritesheetFileName,
                AnimSets = animSets,
                BodyBox = ReadBodyBox(headerElement),
            };
        }
    }

    /// <summary>
    /// Reads SpriteRecord.Header's body volume. The six fields always exist in the export, so a
    /// missing one means the record is not the shape this converter expects and the bank simply
    /// gets no body.
    /// </summary>
    private static SpriteBodyBox? ReadBodyBox(JsonElement headerElement)
    {
        if (!headerElement.TryGetProperty("OffsetX", out var offsetX)
            || !headerElement.TryGetProperty("OffsetY", out var offsetY)
            || !headerElement.TryGetProperty("OffsetZ", out var offsetZ)
            || !headerElement.TryGetProperty("SizeX", out var sizeX)
            || !headerElement.TryGetProperty("SizeY", out var sizeY)
            || !headerElement.TryGetProperty("SizeZ", out var sizeZ))
        {
            return null;
        }

        return new SpriteBodyBox
        {
            OffsetX = offsetX.GetInt32(),
            OffsetY = offsetY.GetInt32(),
            OffsetZ = offsetZ.GetInt32(),
            SizeX = sizeX.GetInt32(),
            SizeY = sizeY.GetInt32(),
            SizeZ = sizeZ.GetInt32(),
        };
    }

    private static bool SameBodyBox(SpriteBodyBox? left, SpriteBodyBox? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return left.HasSameVolumeAs(right);
    }

    private static SpriteAnimation?[] ReadAnimSet(JsonElement animSetElement)
    {
        var directions = new SpriteAnimation?[4];

        if (animSetElement.ValueKind != JsonValueKind.Object
            || !animSetElement.TryGetProperty("PreloadedAnims", out var preloadedAnimsElement)
            || preloadedAnimsElement.ValueKind != JsonValueKind.Array)
        {
            return directions;
        }

        var directionIndex = 0;
        foreach (var animElement in preloadedAnimsElement.EnumerateArray())
        {
            if (directionIndex >= directions.Length)
            {
                break;
            }

            directions[directionIndex] = ReadAnimation(animElement);
            directionIndex++;
        }

        return directions;
    }

    private static SpriteAnimation? ReadAnimation(JsonElement animElement)
    {
        if (animElement.ValueKind != JsonValueKind.Object
            || !animElement.TryGetProperty("Frames", out var framesElement)
            || framesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var frames = new List<SpriteFrame>();
        foreach (var frameElement in framesElement.EnumerateArray())
        {
            if (frameElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var rawDelay = frameElement.GetProperty("Delay").GetInt32();
            var quads = new List<SpriteQuad>();

            if (frameElement.TryGetProperty("Images", out var imagesElement)
                && imagesElement.ValueKind == JsonValueKind.Object
                && imagesElement.TryGetProperty("Images", out var quadsElement)
                && quadsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var quadElement in quadsElement.EnumerateArray())
                {
                    quads.Add(ReadQuad(quadElement));
                }
            }

            frames.Add(new SpriteFrame
            {
                DelayFrames = rawDelay & 0x7f,
                TerminatorCode = rawDelay,
                Quads = quads,
                Collision = ReadCollision(frameElement),
            });
        }

        return frames.Count == 0 ? null : new SpriteAnimation { Frames = frames };
    }

    private static SpriteFrameCollision? ReadCollision(JsonElement frameElement)
    {
        if (!frameElement.TryGetProperty("CollisionData", out var collisionElement)
            || collisionElement.ValueKind != JsonValueKind.Object)
        {
            return null; // absent or explicitly null on most frames
        }

        return new SpriteFrameCollision
        {
            OffsetX = collisionElement.GetProperty("OffsetX").GetInt32(),
            OffsetY = collisionElement.GetProperty("OffsetY").GetInt32(),
            OffsetZ = collisionElement.GetProperty("OffsetZ").GetInt32(),
            Width = collisionElement.GetProperty("Width").GetInt32(),
            Depth = collisionElement.GetProperty("Depth").GetInt32(),
            Height = collisionElement.GetProperty("Height").GetInt32(),
        };
    }

    private static SpriteQuad ReadQuad(JsonElement quadElement)
    {
        return new SpriteQuad
        {
            Spritesheet = quadElement.GetProperty("Spritesheet").GetInt32(),
            Palette = quadElement.GetProperty("Palette").GetInt32(),
            AtlasX = quadElement.GetProperty("AtlasX").GetInt32(),
            AtlasY = quadElement.GetProperty("AtlasY").GetInt32(),
            Width = quadElement.GetProperty("Swidth").GetInt32(),
            Height = quadElement.GetProperty("Sheight").GetInt32(),
            X1 = quadElement.GetProperty("X1").GetInt32(),
            Y1 = quadElement.GetProperty("Y1").GetInt32(),
            X2 = quadElement.GetProperty("X2").GetInt32(),
            Y2 = quadElement.GetProperty("Y2").GetInt32(),
            X3 = quadElement.GetProperty("X3").GetInt32(),
            Y3 = quadElement.GetProperty("Y3").GetInt32(),
            X4 = quadElement.GetProperty("X4").GetInt32(),
            Y4 = quadElement.GetProperty("Y4").GetInt32(),
            Signature = quadElement.GetProperty("Signature").GetInt64(),
        };
    }

    private static IReadOnlyList<int> DiscoverNativeMapIndices(string inputDirectory)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        if (!Directory.Exists(dataDirectory))
        {
            return Array.Empty<int>();
        }

        var indices = new List<int>();
        foreach (var filePath in Directory.EnumerateFiles(dataDirectory, "map_*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.StartsWith("map_", StringComparison.Ordinal)
                && int.TryParse(fileName.AsSpan(4), out var mapIndex))
            {
                indices.Add(mapIndex);
            }
        }

        indices.Sort();
        return indices;
    }
}
