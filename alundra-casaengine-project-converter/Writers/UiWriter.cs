using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 7: UI sprites, the remaining standalone screens, and the BALANCE.BIN table.
///
/// Mapping decisions:
///  - ui/wind.json is a table of 277 crops into the single 256x256 ui/wind.png sheet (window
///    frames, HUD pieces, icons). Each becomes a SpriteData under UI/, all referencing one
///    wind.texture wrapper produced by TextureAssetWriter - the same shape Phase 3 uses for
///    spritesheets. Origin is the crop's own centre (Width/2, Height/2), matching Phase 3, so a
///    flipped UI element mirrors in place.
///  - Sprites are named by their source index (wind_000..wind_276), not by their rectangle:
///    several entries describe the exact same rectangle with the same palette, so a geometry-derived
///    name would collide, and the index is anyway the identity the game itself uses to address a
///    crop. For the same reason no deduplication is done here: two identical rectangles stay two
///    sprites, because a consumer resolving "UI element 42" must find sprite 42.
///  - PaletteIndex has nowhere to live in SpriteData (no custom-properties slot, and the extracted
///    PNG is already colour-resolved RGBA), so the full source table is preserved next to the
///    sprites as UI/wind-sprites.json, keyed by the same index and carrying each sprite's asset id.
///  - All Phase 7 PNGs (wind.png, the 3 memory-card frames, the 13 ending/closing screens and the
///    loading screen) are copied and catalogued under UI/Textures/, keeping the same
///    "<area>/Textures/" convention Phase 3 established with Sprites/Textures/. They get no
///    SpriteData: they are full-screen or full-frame images a UI layer draws directly, and inventing
///    a whole-image sprite for each would add an asset with no information in it.
///  - data/BALANCE.BIN.json (512 per-level records: Hp, the 11 unnamed Values, the NumAnimVals
///    AnimVals pairs, OffsetToNextLevel, Offset, Next) is copied to Data/balance.json with its
///    structure and field names untouched - the fields' meanings are still unknown, so renaming
///    them would destroy the only handle a future gameplay DLL has on them. The single exception is
///    the top-level FileName: it is the absolute path of the .BIN on the extractor's own machine,
///    i.e. provenance of the extraction rather than game data, and keeping it would make the
///    converter's output depend on where the extraction happened.
/// </summary>
public static class UiWriter
{
    private const string UiRelativeDirectory = "UI";
    private const string DataRelativeDirectory = "Data";
    private static readonly string UiTexturesRelativeDirectory = Path.Combine("UI", "Textures");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static void ConvertUi(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var textureAssetIdsBySourcePath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        ConvertWindSprites(inputDirectory, outputDirectory, textureAssetIdsBySourcePath, report);
        CopyStandaloneTextures(inputDirectory, outputDirectory, textureAssetIdsBySourcePath, report);
        ConvertBalance(inputDirectory, outputDirectory, report);

        EditorAssetCatalogService.Save();
        report.Increment("Assets.UiTexture", textureAssetIdsBySourcePath.Count);
    }

    private static void ConvertWindSprites(
        string inputDirectory,
        string outputDirectory,
        Dictionary<string, Guid> textureAssetIdsBySourcePath,
        ConversionReport report)
    {
        var windJsonPath = Path.Combine(inputDirectory, "ui", "wind.json");
        var windPngPath = Path.Combine(inputDirectory, "ui", "wind.png");

        if (!File.Exists(windJsonPath))
        {
            report.Warnings.Add($"UI: '{windJsonPath}' not found; UI sprites skipped.");
            return;
        }

        Guid textureAssetId;
        try
        {
            textureAssetId = TextureAssetWriter.EnsureTexture(
                windPngPath, UiTexturesRelativeDirectory, outputDirectory, textureAssetIdsBySourcePath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"UI: failed to import wind.png - {exception.Message}");
            return;
        }

        var entries = UiSpriteReader.Read(windJsonPath);
        Directory.CreateDirectory(Path.Combine(outputDirectory, UiRelativeDirectory));

        var companionEntries = new List<UiSpriteManifestEntry>(entries.Count);

        foreach (var entry in entries)
        {
            var spriteData = new SpriteData
            {
                SpriteSheetAssetId = textureAssetId,
                PositionInTexture = new Rectangle(entry.U0, entry.V0, entry.Width, entry.Height),
                Origin = new Point(entry.Width / 2, entry.Height / 2),
                Name = $"wind_{entry.Index:D3}",
            };
            spriteData.FileName = Path.Combine(UiRelativeDirectory, $"{spriteData.Name}.sprite");

            EditorAssetWriterService.SaveAsset(spriteData.FileName, spriteData);
            EditorAssetCatalogService.Add(new AssetInfo(spriteData.Id)
            {
                Name = spriteData.Name,
                FileName = spriteData.FileName,
            });

            companionEntries.Add(new UiSpriteManifestEntry
            {
                Index = entry.Index,
                Name = spriteData.Name,
                AssetId = spriteData.Id,
                U0 = entry.U0,
                V0 = entry.V0,
                Width = entry.Width,
                Height = entry.Height,
                PaletteIndex = entry.PaletteIndex,
            });
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, UiRelativeDirectory, "wind-sprites.json"),
            JsonSerializer.Serialize(companionEntries, SerializerOptions));

        report.Increment("Assets.UiSprite", entries.Count);
    }

    private static void CopyStandaloneTextures(
        string inputDirectory,
        string outputDirectory,
        Dictionary<string, Guid> textureAssetIdsBySourcePath,
        ConversionReport report)
    {
        var sourcePaths = new List<string>();
        sourcePaths.AddRange(EnumeratePngs(Path.Combine(inputDirectory, "memorycard"), report));
        sourcePaths.AddRange(EnumeratePngs(Path.Combine(inputDirectory, "closing"), report));

        var loadingScreenPath = Path.Combine(inputDirectory, "data", "loading_screen.png");
        if (File.Exists(loadingScreenPath))
        {
            sourcePaths.Add(loadingScreenPath);
        }
        else
        {
            report.Warnings.Add($"UI: '{loadingScreenPath}' not found.");
        }

        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                TextureAssetWriter.EnsureTexture(
                    sourcePath, UiTexturesRelativeDirectory, outputDirectory, textureAssetIdsBySourcePath);
            }
            catch (Exception exception)
            {
                report.Errors.Add($"UI: failed to import '{sourcePath}' - {exception.Message}");
            }
        }
    }

    private static IEnumerable<string> EnumeratePngs(string directory, ConversionReport report)
    {
        if (!Directory.Exists(directory))
        {
            report.Warnings.Add($"UI: directory '{directory}' not found.");
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory, "*.png").OrderBy(path => path, StringComparer.Ordinal);
    }

    private static void ConvertBalance(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var balancePath = Path.Combine(inputDirectory, "data", "BALANCE.BIN.json");
        if (!File.Exists(balancePath))
        {
            report.Warnings.Add($"UI: '{balancePath}' not found; balance table skipped.");
            return;
        }

        using var stream = File.OpenRead(balancePath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var targetDirectory = Path.Combine(outputDirectory, DataRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        using (var outputStream = File.Create(Path.Combine(targetDirectory, "balance.json")))
        using (var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.NameEquals("FileName"))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var recordCount = root.TryGetProperty("BalanceRecords", out var recordsElement)
                          && recordsElement.ValueKind == JsonValueKind.Array
            ? recordsElement.GetArrayLength()
            : 0;

        report.Increment("Data.BalanceRecords", recordCount);
        report.Messages.Add(
            "Data/balance.json is a structured recopy of BALANCE.BIN.json with unknown field names kept; "
            + "only the top-level 'FileName' was dropped (an absolute path from the extractor's machine, "
            + "which would make the output machine-dependent).");
    }

    /// <summary>
    /// One row of UI/wind-sprites.json: the source record plus the sprite it produced, so a
    /// consumer can go from a game UI index to both the asset and the data SpriteData cannot hold.
    /// </summary>
    private sealed class UiSpriteManifestEntry
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid AssetId { get; set; }
        public int U0 { get; set; }
        public int V0 { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PaletteIndex { get; set; }
    }
}
