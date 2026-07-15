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

public sealed class SpriteFrame
{
    public int DelayFrames;
    public IReadOnlyList<SpriteQuad> Quads = Array.Empty<SpriteQuad>();
}

public sealed class SpriteAnimation
{
    public IReadOnlyList<SpriteFrame> Frames = Array.Empty<SpriteFrame>();
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
            ReadMapSpriteRecords(heroPath, mapIndex: -1, "map_alundra_spritesheet.png", isHeroBank: true, banksByKey);
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

            ReadMapSpriteRecords(mapPath, mapIndex, $"map_{mapIndex}_spritesheet.png", isHeroBank: false, banksByKey);
        }

        return banksByKey.Values.OrderBy(bank => bank.IsHeroBank).ThenBy(bank => bank.Sector5Id).ToList();
    }

    private static void ReadMapSpriteRecords(
        string mapPath, int mapIndex, string spritesheetFileName, bool isHeroBank,
        Dictionary<string, SpriteBank> banksByKey)
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

            var sector5Id = recordElement.GetProperty("Header").GetProperty("Sector5Id").GetInt32();
            var bankKey = isHeroBank ? $"hero_{sector5Id}" : $"{sector5Id}";
            if (banksByKey.ContainsKey(bankKey))
            {
                continue; // same content across maps (verified); first occurrence is canonical
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
            };
        }
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

            var delay = frameElement.GetProperty("Delay").GetInt32();
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

            frames.Add(new SpriteFrame { DelayFrames = delay, Quads = quads });
        }

        return frames.Count == 0 ? null : new SpriteAnimation { Frames = frames };
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
