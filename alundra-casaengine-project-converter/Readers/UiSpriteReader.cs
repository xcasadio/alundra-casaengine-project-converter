using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// One entry of ui/wind.json: a rectangle inside the 256x256 ui/wind.png window/HUD sheet, plus
/// the CLUT the game used to draw it. Field names are the extractor's (U0/V0 are the PSX texture
/// page coordinates of the crop's top-left corner).
///
/// Index is not in the source file - it is the entry's position in the array. It is kept because
/// several entries describe the exact same rectangle with the same palette (the game addresses
/// these crops by index, not by geometry), so the index is the only stable identity an entry has.
/// </summary>
public sealed class UiSpriteEntry
{
    public int Index { get; set; }
    public int U0 { get; set; }
    public int V0 { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int PaletteIndex { get; set; }
}

/// <summary>
/// Reads the UI sprite rectangle table (ui/wind.json). Order is the source array order, which is
/// the index the game uses, so it is preserved as-is rather than sorted.
/// </summary>
public static class UiSpriteReader
{
    public static List<UiSpriteEntry> Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        var entries = new List<UiSpriteEntry>();
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            entries.Add(new UiSpriteEntry
            {
                Index = index++,
                U0 = GetInt32(element, "U0"),
                V0 = GetInt32(element, "V0"),
                Width = GetInt32(element, "Width"),
                Height = GetInt32(element, "Height"),
                PaletteIndex = GetInt32(element, "PaletteIndex"),
            });
        }

        return entries;
    }

    private static int GetInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
