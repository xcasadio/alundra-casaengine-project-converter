using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 9: each map's scrolling background layers (Map.ScrollParameters - see
/// <see cref="BackdropReader"/> and <see cref="BackdropImageBuilder"/> for the derived format).
/// This is the PSX parallax backdrop (open sea, sky...) the original draws behind (or, for
/// Ground-flagged layers, as a near-foreground overlay above) everything else; today it is entirely
/// missing from the converted project, so anything not covered by a cell tile shows the engine's
/// clear color instead.
///
/// Output, only for maps whose ScrollParameters.Infos.Enabled is set:
///  - Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}-layer{N}.png (+.texture wrapper), one per
///    "Tiles"-mode (mode 1) layer that has at least one non-empty tile - via
///    <see cref="TextureAssetWriter"/>, same as every other converter texture.
///  - Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json: a raw companion (not a CasaEngine
///    asset, nothing loads it - same convention as events.json) carrying every layer's parallax
///    factors, auto-scroll speeds/periods, blend mode, ground flag, anim timer, texture reference,
///    the map's full-screen overlay tint (OverlayEnabled/OverlayColorR/G/B - see the class doc on
///    <see cref="Readers.BackdropDocument"/> for the corrected source semantics), and - for Cellular
///    (mode 2) layers - the raw cell/wave parameters whose rendering this converter does not
///    implement (see the deferred-items list on <see cref="Readers.BackdropDocument"/>). Written for
///    every map passing the <c>Infos.Enabled</c> gate, independently of whether any layer exported a
///    texture - this is why the 9 Cellular-only tinted maps (no Tiles layer at all) still get a
///    companion carrying just the tint.
/// </summary>
public static class BackdropWriter
{
    // The plan's D-E9-4 acceptance criterion (docs/plan-e9-backdrops-residus.md), enforced only on
    // a full run - same reasoning as WorldWriter.CheckInvariants: with --maps the totals are a
    // subset by construction. Backdrop.LayersExported is unchanged by this slice (still one texture
    // per exported Tiles layer, frame 0 included); Backdrop.FramesExported additionally counts the
    // 3 extra frames baked for each of the 7 AnimNum=4 Tiles layers (132 + 7 * 3 = 153).
    private const int ExpectedMapCorpusSize = 483;
    private const int ExpectedLayersExported = 132;
    private const int ExpectedFramesExported = 153;

    public static void ConvertBackdrops(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var discoveredMapIndices = MapDiscovery.DiscoverMapIndices(inputDirectory);
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : discoveredMapIndices;
        var isFullRun = discoveredMapIndices.Count == ExpectedMapCorpusSize
                        && discoveredMapIndices.All(mapIndices.Contains);

        var textureCache = new Dictionary<string, Guid>();

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, mapLocations, textureCache, report);
        }

        // The layer textures were registered through EditorAssetCatalogService.Add (via
        // TextureAssetWriter.EnsureTexture), but only Save() persists the catalog to
        // AssetInfos.json - and Phase 9 runs after every other writer's own Save(), so
        // skipping it here silently dropped all 264 backdrop entries: the runtime then
        // resolved none of the textures and every layer was skipped at world load.
        EditorAssetCatalogService.Save();

        if (isFullRun)
        {
            CheckInvariants(report);
        }
    }

    private static void CheckInvariants(ConversionReport report)
    {
        CheckInvariant(report, "Backdrop.LayersExported", ExpectedLayersExported);
        CheckInvariant(report, "Backdrop.FramesExported", ExpectedFramesExported);
    }

    private static void CheckInvariant(ConversionReport report, string counterName, int expected)
    {
        var actual = report.Counters.GetValueOrDefault(counterName);
        if (actual != expected)
        {
            report.Errors.Add($"Backdrop: invariant '{counterName}' is {actual}, expected {expected}.");
        }
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        Dictionary<string, Guid> textureCache,
        ConversionReport report)
    {
        var nativeMapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
        if (!File.Exists(nativeMapPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{nativeMapPath}'.");
            return;
        }

        BackdropReadResult result;
        try
        {
            result = BackdropReader.Read(nativeMapPath, mapIndex);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read backdrop - {exception.Message}");
            return;
        }

        if (!result.Document.Enabled)
        {
            return;
        }

        if (result.Document.OverlayEnabled)
        {
            // Companion emission below is unconditional once Enabled is true - it does not depend on
            // any layer having exported a texture, so the 9 Cellular-only tinted maps still get one.
            report.Increment("Backdrop.OverlayTints");
        }

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineBackdropBake", Guid.NewGuid().ToString("N"));

        try
        {
            foreach (var layer in result.Document.Layers)
            {
                report.Increment("Backdrop.Layers");
                report.Increment($"Backdrop.Layers.{layer.Mode}");

                if (layer.Mode != "Tiles")
                {
                    continue;
                }

                var tileGrid = BackdropReader.ExtractTileGrid(result, layer.LayerId);
                if (tileGrid is null)
                {
                    report.Errors.Add(
                        $"map_{mapIndex}: backdrop layer {layer.LayerId} has graphics but its tile grid offset " +
                        "falls outside the Data blob.");
                    continue;
                }

                using var bitmap = BackdropImageBuilder.Build(tileGrid, result.TileSheetImageData, result.PaletteWords);
                if (bitmap is null)
                {
                    // Every tile in the grid is empty - nothing to draw for this layer.
                    layer.Width = 0;
                    layer.Height = 0;
                    continue;
                }

                Directory.CreateDirectory(tempDirectory);
                var tempPngPath = Path.Combine(tempDirectory, location.BackdropLayerTextureFileName(layer.LayerId));
                bitmap.Save(tempPngPath, ImageFormat.Png);

                var textureAssetId = TextureAssetWriter.EnsureTexture(
                    tempPngPath, location.BackdropDirectory, outputDirectory, textureCache);

                layer.TextureAssetId = textureAssetId.ToString();
                report.Increment("Backdrop.LayersExported");
                report.Increment("Backdrop.FramesExported");

                // D-E9-2/D-E9-3/D-E9-4 (docs/plan-e9-backdrops-residus.md): only maps with AnimNum >
                // 1 get extra frames - a non-animated map (AnimNum <= 1) leaves FrameTextureAssetIds
                // null, producing exactly what this method produced before this slice.
                var animNum = result.Document.AnimNum;
                if (animNum > 1)
                {
                    var frameTextureAssetIds = new string[animNum];
                    frameTextureAssetIds[0] = textureAssetId.ToString();

                    for (var frame = 1; frame < animNum; frame++)
                    {
                        var vAnim = (frame << 8) / animNum;
                        using var frameBitmap =
                            BackdropImageBuilder.Build(tileGrid, result.TileSheetImageData, result.PaletteWords, vAnim)
                            ?? BackdropImageBuilder.CreateTransparentFrame();

                        var frameFileName = location.BackdropLayerFrameTextureFileName(layer.LayerId, frame);
                        var frameTempPath = Path.Combine(tempDirectory, frameFileName);
                        frameBitmap.Save(frameTempPath, ImageFormat.Png);

                        var frameTextureAssetId = TextureAssetWriter.EnsureTexture(
                            frameTempPath, location.BackdropDirectory, outputDirectory, textureCache);

                        frameTextureAssetIds[frame] = frameTextureAssetId.ToString();
                        report.Increment("Backdrop.FramesExported");
                    }

                    layer.FrameTextureAssetIds = frameTextureAssetIds;
                }
            }

            var cellularPalDexes = result.Document.Layers
                .Where(layer => layer.Mode == "Cellular" && layer.Cellular is not null)
                .SelectMany(layer => layer.Cellular!.Cells)
                .Select(cell => cell.PalDex)
                .Distinct()
                .OrderBy(palDex => palDex)
                .ToList();

            if (cellularPalDexes.Count > 0)
            {
                report.Increment("Backdrop.CellularMapsHandled");

                var sheetTextureAssetIds = new string?[8];
                var wroteAnySheet = false;

                foreach (var palDex in cellularPalDexes)
                {
                    if ((uint)palDex >= (uint)result.PaletteWords.Length)
                    {
                        continue;
                    }

                    using var sheetBitmap = BackdropImageBuilder.BuildTileSheet(
                        result.TileSheetImageData, result.PaletteWords[palDex]);
                    if (sheetBitmap is null)
                    {
                        continue;
                    }

                    Directory.CreateDirectory(tempDirectory);
                    var tempSheetPath = Path.Combine(tempDirectory, location.BackdropCellularSheetFileName(palDex));
                    sheetBitmap.Save(tempSheetPath, ImageFormat.Png);

                    var sheetTextureAssetId = TextureAssetWriter.EnsureTexture(
                        tempSheetPath, location.BackdropDirectory, outputDirectory, textureCache);

                    sheetTextureAssetIds[palDex] = sheetTextureAssetId.ToString();
                    wroteAnySheet = true;
                    report.Increment("Backdrop.CellularSheetsExported");
                }

                if (wroteAnySheet)
                {
                    result.Document.CellularSheetTextureAssetIds = sheetTextureAssetIds;
                }
            }

            var companionPath = Path.Combine(outputDirectory, location.BackdropRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(companionPath)!);
            File.WriteAllText(companionPath, JsonSerializer.Serialize(result.Document, SerializerOptions));

            report.Increment("Backdrop.Maps");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };
}
