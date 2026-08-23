using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class AssetVerifierTests
{
    // Minimal 8-byte PNG signature: the tileset importer copies the file without decoding it.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private const int MapIndex = 4;

    [Fact]
    public void Verify_OnAWellFormedProject_LoadsEveryAssetAndPasses()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            var verified = AssetVerifier.Verify(outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.True(verified);

            // Loaded through the engine class that owns the format.
            Assert.Equal(1, report.Counters["Verify.Loaded.tileMap"]);
            Assert.Equal(1, report.Counters["Verify.Loaded.tileset"]);
            Assert.Equal(1, report.Counters["Verify.Loaded.texture"]);
            Assert.Equal(1, report.Counters["Verify.Loaded.world"]);

            Assert.Equal(
                report.Counters["Verify.Assets"],
                report.Counters["Verify.Loaded"] + report.Counters["Verify.ExistenceChecked"]);
            Assert.False(report.Counters.ContainsKey("Verify.Failed"));
        });
    }

    [Fact]
    public void Verify_PutsFormatsWithoutAHeadlessLoaderInTheExistenceCheckedBucket()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            // The .png is registered by Phase 1 next to its .texture; the .wav stands for Phase 4,
            // which registers raw audio the engine has no loader for at all.
            File.WriteAllBytes(Path.Combine(outputDirectory, "Sounds", "sfx_0.wav"), new byte[] { 1, 2, 3, 4 });
            AddCatalogEntry(outputDirectory, Guid.NewGuid(), "sfx_0", Path.Combine("Sounds", "sfx_0.wav"));

            Assert.True(AssetVerifier.Verify(outputDirectory, report));

            Assert.Equal(1, report.Counters["Verify.ExistenceChecked.wav"]);
            Assert.Equal(1, report.Counters["Verify.ExistenceChecked.png"]);
            Assert.False(report.Counters.ContainsKey("Verify.Loaded.wav"));
            Assert.False(report.Counters.ContainsKey("Verify.Loaded.png"));
        });
    }

    [Fact]
    public void Verify_OnATruncatedSprite_FailsAndNamesTheAsset()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            var spriteRelativePath = Path.Combine("Sprites", "broken.sprite");
            File.WriteAllText(Path.Combine(outputDirectory, spriteRelativePath), "{ \"id\": \"1234");
            AddCatalogEntry(outputDirectory, Guid.NewGuid(), "broken", spriteRelativePath);

            Assert.False(AssetVerifier.Verify(outputDirectory, report));

            var error = Assert.Single(report.Errors);
            Assert.Contains(spriteRelativePath, error, StringComparison.Ordinal);
            Assert.Contains("failed to load as .sprite", error, StringComparison.Ordinal);
            Assert.Equal(1, report.Counters["Verify.Failed.sprite"]);
        });
    }

    [Fact]
    public void Verify_OnACatalogEntryWithoutItsFile_FailsAndNamesTheAsset()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            var missingRelativePath = Path.Combine("Maps", "vanished.tileMap");
            AddCatalogEntry(outputDirectory, Guid.NewGuid(), "vanished", missingRelativePath);

            Assert.False(AssetVerifier.Verify(outputDirectory, report));

            var error = Assert.Single(report.Errors);
            Assert.Contains(missingRelativePath, error, StringComparison.Ordinal);
            Assert.Contains("does not exist", error, StringComparison.Ordinal);
            Assert.Equal(1, report.Counters["Verify.MissingFiles"]);
            Assert.Equal(1, report.Counters["Verify.Failed.tileMap"]);
        });
    }

    [Fact]
    public void Verify_OnADuplicateAssetId_FailsAndNamesTheId()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            var duplicatedId = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var firstRelativePath = Path.Combine("Sounds", "first.wav");
            var secondRelativePath = Path.Combine("Sounds", "second.wav");
            File.WriteAllBytes(Path.Combine(outputDirectory, firstRelativePath), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(outputDirectory, secondRelativePath), new byte[] { 1 });
            AddCatalogEntry(outputDirectory, duplicatedId, "first", firstRelativePath);
            AddCatalogEntry(outputDirectory, duplicatedId, "second", secondRelativePath);

            Assert.False(AssetVerifier.Verify(outputDirectory, report));

            var error = Assert.Single(report.Errors);
            Assert.Contains("duplicate asset id", error, StringComparison.Ordinal);
            Assert.Contains(duplicatedId.ToString(), error, StringComparison.Ordinal);
            Assert.Equal(1, report.Counters["Verify.DuplicateIds"]);
        });
    }

    [Fact]
    public void Verify_OnADuplicateFileName_Fails()
    {
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            var relativePath = Path.Combine("Sounds", "shared.wav");
            File.WriteAllBytes(Path.Combine(outputDirectory, relativePath), new byte[] { 1 });
            AddCatalogEntry(outputDirectory, Guid.NewGuid(), "shared", relativePath);
            AddCatalogEntry(outputDirectory, Guid.NewGuid(), "shared-again", relativePath);

            Assert.False(AssetVerifier.Verify(outputDirectory, report));

            var error = Assert.Single(report.Errors);
            Assert.Contains("duplicate asset file name", error, StringComparison.Ordinal);
            Assert.Equal(1, report.Counters["Verify.DuplicateFileNames"]);
        });
    }

    [Fact]
    public void Verify_OnAnEmptyCatalogOverADirectoryFullOfAssets_Fails()
    {
        // The blind spot a catalog-driven verifier has by construction: with nothing to iterate it
        // would otherwise report a serene PASSED over a project it never looked at.
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            File.WriteAllText(Path.Combine(outputDirectory, "AssetInfos.json"), "{ \"asset_infos\": [] }");

            Assert.False(AssetVerifier.Verify(outputDirectory, report));

            Assert.Contains(
                report.Errors,
                error => error.Contains("catalog is empty", StringComparison.Ordinal)
                         && error.Contains("Nothing was verified", StringComparison.Ordinal));
            Assert.True(report.Counters["Verify.LoadableFilesOnDisk"] > 0);
        });
    }

    [Fact]
    public void Verify_OnALoadableFileMissingFromTheCatalog_WarnsWithoutFailing()
    {
        // Stale output from an earlier run into the same directory: worth surfacing, but it does not
        // make this run's own conversion wrong.
        RunOnAGeneratedProject((outputDirectory, report) =>
        {
            File.WriteAllText(
                Path.Combine(outputDirectory, "Maps", "leftover.tileMap"), "{ \"id\": \"whatever\" }");

            Assert.True(AssetVerifier.Verify(outputDirectory, report));

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Verify.UncataloguedFiles"]);
            Assert.Contains(
                report.Warnings,
                warning => warning.Contains("no catalog entry", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Verify_WithoutACatalog_ReportsAnError()
    {
        var outputDirectory = CreateTempDirectory();
        try
        {
            var report = new ConversionReport();

            Assert.False(AssetVerifier.Verify(outputDirectory, report));
            Assert.Contains(report.Errors, error => error.Contains("asset catalog not found", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Builds a real (tiny) generated project - Phase 0, Phase 1 and Phase 6 over one map fixture -
    /// then hands it to the test body. Going through the writers rather than hand-written assets is
    /// the point: the verifier must agree with what the converter actually produces.
    /// </summary>
    private static void RunOnAGeneratedProject(Action<string, ConversionReport> body)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteMapFixture(inputDirectory, MapIndex);
            var mapLocations = new Dictionary<int, MapLocation> { [MapIndex] = new MapLocation("TestZone", "Test Map-4") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var buildReport = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, buildReport);
            TileMapWriter.ConvertMaps(inputDirectory, outputDirectory, mapFilter: null, mapLocations, buildReport);
            WorldWriter.ConvertWorlds(
                inputDirectory, outputDirectory, mapFilter: new[] { MapIndex }, mapLocations, Guid.Empty, buildReport);
            Assert.Empty(buildReport.Errors);

            body(outputDirectory, new ConversionReport());
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Appends an entry straight into AssetInfos.json rather than through
    /// EditorAssetCatalogService, whose dictionaries would reject or collapse the very duplicates
    /// these tests need to produce.
    /// </summary>
    private static void AddCatalogEntry(string outputDirectory, Guid id, string name, string relativeFileName)
    {
        var catalogPath = Path.Combine(outputDirectory, "AssetInfos.json");
        var rootElement = JObject.Parse(File.ReadAllText(catalogPath));
        var assetInfoNodes = (JArray)rootElement["asset_infos"]!;

        assetInfoNodes.Add(new JObject
        {
            ["id"] = id,
            ["name"] = name,
            ["file_name"] = relativeFileName,
            ["asset_type"] = Path.GetExtension(relativeFileName).TrimStart('.').ToLowerInvariant(),
        });

        File.WriteAllText(catalogPath, rootElement.ToString());
    }

    private static void WriteMapFixture(string inputDirectory, int mapIndex)
    {
        var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(tiledDirectory);

        var baseName = $"map_{mapIndex}";
        File.WriteAllBytes(Path.Combine(tiledDirectory, $"{baseName}_tileset.png"), FakePngBytes);

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}_tileset.tsj"),
            """
            {
                "type": "tileset",
                "name": "tileset",
                "tilewidth": 24,
                "tileheight": 16,
                "tilecount": 2,
                "columns": 2,
                "image": "map_INDEX_tileset.png",
                "imagewidth": 48,
                "imageheight": 16
            }
            """.Replace("INDEX", mapIndex.ToString()));

        File.WriteAllText(
            Path.Combine(tiledDirectory, $"{baseName}.tmj"),
            """
            {
                "type": "map",
                "orientation": "orthogonal",
                "infinite": false,
                "width": 2,
                "height": 2,
                "tilewidth": 24,
                "tileheight": 16,
                "tilesets": [ { "firstgid": 1, "source": "map_INDEX_tileset.tsj" } ],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Render_0",
                        "width": 2,
                        "height": 2,
                        "data": [1, 2, 1, 1]
                    }
                ]
            }
            """.Replace("INDEX", mapIndex.ToString()));

        File.WriteAllText(
            Path.Combine(dataDirectory, $"{baseName}.json"),
            """
            {
                "Info": { "Portals": [] },
                "Map": { "MapTiles": [] },
                "SpriteInfo": {
                    "Entities": { "Entities": [] },
                    "MapEvents": { "Records": [] }
                }
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
