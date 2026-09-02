using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using World = CasaEngine.Framework.Scene.World.World;

namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// Phase 8: the run's safety net. Reads the generated AssetInfos.json back and, for every entry,
/// loads the file through the engine class that owns its format - the same Load() the editor calls,
/// where a malformed asset currently surfaces only as a swallowed "IAssetLoader can't load ..." at
/// runtime.
///
/// Formats with no headless loader (raw .png needs a GraphicsDevice, .wav has no engine loader at
/// all, .fnt and .tmj are companion files) are only checked for existence and non-emptiness, and are
/// counted in their own bucket so a run never reports them as loaded.
/// </summary>
public static class AssetVerifier
{
    /// <summary>
    /// Extensions (lower case, without the dot) whose asset can be materialised without a
    /// GraphicsDevice or a running CasaEngineGame. Verified against AssetLoaderRegistry: every type
    /// here is registered there with a plain AssetLoader&lt;T&gt;, i.e. a JObject-driven Load().
    /// </summary>
    private static readonly Dictionary<string, Action<JObject>> Loaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tilemap"] = element => new TileMapData().Load(element),
        ["tileset"] = element => new TileSetData().Load(element),
        ["sprite"] = element => new SpriteData().Load(element),
        ["anim2d"] = element => new Animation2dData().Load(element),
        ["texture"] = element => new Texture().Load(element),
        // World.Load parses the document headlessly; the entities inside entity_references are
        // materialised later by AssetContentManager, which needs a running game - see
        // WorldWriterTests.
        ["world"] = element => new World().Load(element),
        // Entity.Load builds its components through ElementFactory and attaches the root component,
        // none of which touches Owner.World.Game - only InitializeWithWorld does, and that is not
        // part of Load. WorldWriterTests has been loading entities this way since Phase 6.
        ["entity"] = element => new Entity().Load(element),
        // PlayerStartupSettings.Load and ButtonsMapping.Load are both plain JObject readers with no
        // game/graphics dependency - see PlayerSetupWriter.
        ["gamemode"] = element => new PlayerStartupSettings().Load(element),
        ["buttonsmapping"] = element => new ButtonsMapping().Load(element),
    };

    /// <summary>
    /// <paramref name="isFullRun"/> is CliOptions.IsFullRun (D-N-3): a run with no --maps filter and a
    /// --phase ceiling covering the last phase. Only then does Phase 0's from-scratch catalog rebuild
    /// (fact 3) actually see every map/phase's output, so only then can a coverage gap be trusted as a
    /// real defect rather than a --maps/--phase run skipping work by construction.
    /// </summary>
    public static bool Verify(string outputDirectory, ConversionReport report, bool isFullRun)
    {
        var catalogPath = Path.Combine(outputDirectory, "AssetInfos.json");
        if (!File.Exists(catalogPath))
        {
            report.Errors.Add($"Verify: asset catalog not found at '{catalogPath}'.");
            return false;
        }

        List<AssetInfo> assetInfos;
        try
        {
            assetInfos = ReadCatalog(catalogPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Verify: asset catalog '{catalogPath}' could not be parsed: {exception.Message}");
            return false;
        }

        var errorCountBefore = report.Errors.Count;

        CheckCatalogIntegrity(assetInfos, outputDirectory, report);
        CheckCatalogCoverage(assetInfos, outputDirectory, report, isFullRun);
        CheckTilemapPngWrappers(assetInfos, outputDirectory, report, isFullRun);

        report.Counters["Verify.Assets"] = assetInfos.Count;

        foreach (var assetInfo in assetInfos)
        {
            VerifyAsset(assetInfo, outputDirectory, report);
        }

        return report.Errors.Count == errorCountBefore;
    }

    /// <summary>
    /// The verifier is catalog-driven, so it is blind to anything the catalog does not mention: with
    /// an empty AssetInfos.json it would report a serene PASSED over a directory full of assets. A
    /// safety net that cannot distinguish "nothing failed" from "nothing was checked" is not one, so
    /// the loadable files actually on disk are the floor it is measured against.
    ///
    /// An empty catalog is only legitimate when there is nothing to catalog - Phase 0 on its own
    /// writes exactly that. Files on disk with no catalog entry are reported as an error on a full
    /// run (D-N-3) - Phase 1's purge (D-N-2) now keeps tilemap/ free of the engine Tiled importer's
    /// renamed collisions (map_N_tileset_2.png), so an uncatalogued loadable is a real defect once
    /// every map/phase actually ran. A partial run (--maps/--phase) leaves gaps by construction (a
    /// filtered map's or skipped phase's output was never catalogued this run), so it stays a warning.
    /// </summary>
    private static void CheckCatalogCoverage(
        List<AssetInfo> assetInfos, string outputDirectory, ConversionReport report, bool isFullRun)
    {
        var catalogued = new HashSet<string>(
            assetInfos.Select(assetInfo => NormalizeRelativePath(assetInfo.FileName)),
            StringComparer.OrdinalIgnoreCase);

        var uncatalogued = 0;
        var loadableOnDisk = 0;

        foreach (var filePath in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(filePath).TrimStart('.');
            if (!Loaders.ContainsKey(extension))
            {
                continue;
            }

            loadableOnDisk++;

            var relativePath = NormalizeRelativePath(Path.GetRelativePath(outputDirectory, filePath));
            if (!catalogued.Contains(relativePath))
            {
                uncatalogued++;
            }
        }

        report.Counters["Verify.LoadableFilesOnDisk"] = loadableOnDisk;

        if (assetInfos.Count == 0 && loadableOnDisk > 0)
        {
            report.Errors.Add(
                $"Verify: the asset catalog is empty but {loadableOnDisk} loadable asset file(s) exist under "
                + $"'{outputDirectory}'. Nothing was verified.");
            return;
        }

        if (uncatalogued > 0)
        {
            report.Counters["Verify.UncataloguedFiles"] = uncatalogued;
            var message =
                $"Verify: {uncatalogued} loadable asset file(s) on disk have no catalog entry and were not "
                + "verified; they are most likely stale output from an earlier run. Convert into an empty "
                + "directory to be sure of what the catalog describes.";

            if (isFullRun)
            {
                report.Errors.Add(message);
            }
            else
            {
                report.Warnings.Add(message);
            }
        }
    }

    /// <summary>
    /// D-N-3(b): every tileset PNG the Tiled importer writes under a map's tilemap/ directory must
    /// have a catalogued .texture wrapper next to it (TextureAssetWriter.cs:56 - same relative path,
    /// extension swapped) - a PNG the loader-based coverage check above can never see, since "png" is
    /// not in <see cref="Loaders"/> (fact 5). Same full/partial scoping as (a): an error on a full
    /// run, a warning otherwise.
    /// </summary>
    private static void CheckTilemapPngWrappers(
        IReadOnlyList<AssetInfo> assetInfos, string outputDirectory, ConversionReport report, bool isFullRun)
    {
        var catalogued = new HashSet<string>(
            assetInfos.Select(assetInfo => NormalizeRelativePath(assetInfo.FileName)),
            StringComparer.OrdinalIgnoreCase);

        var missingWrapperCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(outputDirectory, "*.png", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(outputDirectory, filePath));
            if (!IsMapTilemapPng(relativePath))
            {
                continue;
            }

            var wrapperRelativePath = NormalizeRelativePath(Path.ChangeExtension(relativePath, ".texture"));
            if (catalogued.Contains(wrapperRelativePath))
            {
                continue;
            }

            missingWrapperCount++;
            var message =
                $"Verify: '{relativePath}' has no catalogued .texture wrapper ('{wrapperRelativePath}').";

            if (isFullRun)
            {
                report.Errors.Add(message);
            }
            else
            {
                report.Warnings.Add(message);
            }
        }

        if (missingWrapperCount > 0)
        {
            report.Counters["Verify.TilemapPngsMissingWrapper"] = missingWrapperCount;
        }
    }

    /// <summary>Matches "Maps/.../tilemap/*.png" - any depth of zone/map folders in between.</summary>
    private static bool IsMapTilemapPng(string relativePath)
    {
        var segments = relativePath.Split('/');
        return segments.Length > 0
            && string.Equals(segments[0], "Maps", StringComparison.OrdinalIgnoreCase)
            && segments.Contains("tilemap", StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string fileName)
        => fileName.Replace('\\', '/').TrimStart('/');

    private static List<AssetInfo> ReadCatalog(string catalogPath)
    {
        var rootElement = JObject.Parse(File.ReadAllText(catalogPath));
        var assetInfos = new List<AssetInfo>();

        // AssetInfo is the engine's own type, but the catalog is parsed here rather than through
        // AssetCatalog.Load: that one adds into a process-global dictionary and throws on the very
        // first duplicate id, which is one of the defects this pass is meant to report.
        if (rootElement["asset_infos"] is not JArray assetInfoNodes)
        {
            return assetInfos;
        }

        foreach (var assetInfoNode in assetInfoNodes)
        {
            var assetInfo = new AssetInfo();
            assetInfo.Load((JObject)assetInfoNode);
            assetInfos.Add(assetInfo);
        }

        return assetInfos;
    }

    private static void CheckCatalogIntegrity(
        IReadOnlyList<AssetInfo> assetInfos, string outputDirectory, ConversionReport report)
    {
        foreach (var group in assetInfos.GroupBy(assetInfo => assetInfo.Id).Where(group => group.Count() > 1))
        {
            report.Errors.Add(
                $"Verify: duplicate asset id {group.Key} shared by {group.Count()} entries " +
                $"({string.Join(", ", group.Select(assetInfo => assetInfo.FileName).Order(StringComparer.Ordinal))}).");
            report.Increment("Verify.DuplicateIds");
        }

        foreach (var group in assetInfos
                     .GroupBy(assetInfo => assetInfo.FileName, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            report.Errors.Add($"Verify: duplicate asset file name '{group.Key}' in {group.Count()} entries.");
            report.Increment("Verify.DuplicateFileNames");
        }

        foreach (var assetInfo in assetInfos)
        {
            if (!File.Exists(Path.Combine(outputDirectory, assetInfo.FileName)))
            {
                report.Errors.Add($"Verify: catalog entry '{assetInfo.FileName}' points at a file that does not exist.");
                report.Increment("Verify.MissingFiles");
            }
        }
    }

    private static void VerifyAsset(AssetInfo assetInfo, string outputDirectory, ConversionReport report)
    {
        // The extension keeps its authored casing in counters and messages (.tileMap, not .tilemap);
        // the loader lookup is case-insensitive.
        var extension = Path.GetExtension(assetInfo.FileName).TrimStart('.');
        var fullPath = Path.Combine(outputDirectory, assetInfo.FileName);

        if (!Loaders.TryGetValue(extension, out var loader))
        {
            VerifyExistenceOnly(assetInfo, fullPath, extension, report);
            return;
        }

        if (!File.Exists(fullPath))
        {
            // Already reported by the integrity pass; counted here so the buckets stay complete.
            report.Increment("Verify.Failed");
            report.Increment($"Verify.Failed.{extension}");
            return;
        }

        try
        {
            loader(JObject.Parse(File.ReadAllText(fullPath)));
            report.Increment("Verify.Loaded");
            report.Increment($"Verify.Loaded.{extension}");
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Verify: '{assetInfo.FileName}' failed to load as .{extension}: {exception.Message}");
            report.Increment("Verify.Failed");
            report.Increment($"Verify.Failed.{extension}");
        }
    }

    private static void VerifyExistenceOnly(
        AssetInfo assetInfo, string fullPath, string extension, ConversionReport report)
    {
        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                // Already reported by the integrity pass; counted here so the buckets stay complete.
                report.Increment("Verify.Failed");
                report.Increment($"Verify.Failed.{extension}");
                return;
            }

            if (fileInfo.Length == 0)
            {
                report.Errors.Add($"Verify: '{assetInfo.FileName}' exists but is empty (.{extension} has no headless loader).");
                report.Increment("Verify.Failed");
                report.Increment($"Verify.Failed.{extension}");
                return;
            }

            report.Increment("Verify.ExistenceChecked");
            report.Increment($"Verify.ExistenceChecked.{extension}");
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Verify: '{assetInfo.FileName}' could not be inspected on disk: {exception.Message}");
            report.Increment("Verify.Failed");
            report.Increment($"Verify.Failed.{extension}");
        }
    }
}
