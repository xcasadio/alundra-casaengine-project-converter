using System.Diagnostics;
using System.Globalization;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Dialogue.Assets;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers <see cref="YarnDialogueWriter"/> (docs/plan-e15-yarn.md, E15.b task T4): the writer and
/// catalogue half of the pipeline built on top of <c>YarnTextEmitter</c> (T3) - one <c>.yarn</c> +
/// <c>.dialogue</c> per table, catalogued with a deterministic id, following the "Contrat des fichiers
/// et des nœuds" table.
/// </summary>
public class YarnDialogueWriterTests
{
    // ---- Fixture: two small maps, the shared table, and a slice of the real ETC index table --------

    [Fact]
    public void ConvertDialogues_Fixture_WritesTheContractShapedFilesAndNodes()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteFixture(inputDirectory);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [4] = new("TestZone", "Test Map-4"),
                [5] = new("TestZone", "Test Map-5"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            YarnDialogueWriter.ConvertDialogues(inputDirectory, outputDirectory, mapFilter: null, mapLocations, report);

            Assert.Empty(report.Errors);

            // ---- Map 4: node M4_S000 present (index 0), M4_S003 present (#Disuse), the rest absent ----
            var map4YarnPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-4", "dialogues", "Test Map-4.yarn");
            var map4DialoguePath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-4", "dialogues", "Test Map-4.dialogue");
            Assert.True(File.Exists(map4YarnPath));
            Assert.True(File.Exists(map4DialoguePath));

            var map4Yarn = File.ReadAllText(map4YarnPath);
            Assert.DoesNotContain("\r", map4Yarn, StringComparison.Ordinal);
            // Exact node set: index 0 ("Bonjour !"), index 3 (#Disuse, D-E15-7) and index 4 ("Au
            // revoir.") are non-empty; indices 1-2 (null / "") get no node.
            Assert.Equal(
                new[] { "M4_S000", "M4_S003", "M4_S004" },
                TitlesOf(map4Yarn));

            var map4AssetId = Ids.For("dialogue:map:4");
            var map4CatalogEntry = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.FileName == RelativePath(outputDirectory, map4DialoguePath));
            Assert.Equal(map4AssetId, map4CatalogEntry.Id);
            Assert.Equal("dialogue_4", map4CatalogEntry.Name);

            var map4Document = JObject.Parse(File.ReadAllText(map4DialoguePath));
            Assert.Equal(map4AssetId.ToString(), (string?)map4Document["id"]);

            var map4Asset = new DialogueAsset();
            map4Asset.Load(map4Document);
            Assert.True(map4Asset.HasCompiledProgram);
            Assert.True(map4Asset.TryGetLineText("line:M4_S000_p0", out _));
            Assert.True(map4Asset.TryGetLineText("line:M4_S003_p0", out _));

            // ---- Map 5: exists too (no --maps filter here) ----
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-5", "dialogues", "Test Map-5.dialogue")));

            // ---- Shared: 128 nodes, all non-empty ----
            var sharedDialoguePath = Path.Combine(outputDirectory, "Dialogues", "Shared.dialogue");
            Assert.True(File.Exists(sharedDialoguePath));
            var sharedAssetId = Ids.For("dialogue:shared");
            var sharedCatalogEntry = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Name == "dialogue_shared");
            Assert.Equal(sharedAssetId, sharedCatalogEntry.Id);
            Assert.Equal(Path.Combine("Dialogues", "Shared.dialogue"), sharedCatalogEntry.FileName);
            var sharedAsset = new DialogueAsset();
            var sharedYarn = File.ReadAllText(Path.Combine(outputDirectory, "Dialogues", "Shared.yarn"));
            sharedAsset.Load(JObject.Parse(File.ReadAllText(sharedDialoguePath)));
            for (var i = 0; i < 128; i++)
            {
                Assert.True(sharedAsset.TryGetLineText($"line:Shared_S{i:000}_p0", out _), $"Shared_S{i:000} missing");
            }

            // Exact node set: all 128 entries of map_alundra are non-empty (D-E15-3).
            Assert.Equal(
                Enumerable.Range(0, 128).Select(i => $"Shared_S{i:000}").ToArray(),
                TitlesOf(sharedYarn));

            // ---- ETC: 67 (offset 3656 -> "OUI"), 68 (offset 3660 -> "NON") and 514 (offset 6184) exist;
            // 896 and 897 share offset 2048, whose value is null -> neither gets a node ----
            var etcDialoguePath = Path.Combine(outputDirectory, "Dialogues", "Etc.dialogue");
            Assert.True(File.Exists(etcDialoguePath));
            var etcAssetId = Ids.For("dialogue:etc");
            var etcCatalogEntry = Assert.Single(EditorAssetCatalogService.AssetInfos, info => info.Name == "dialogue_etc");
            Assert.Equal(etcAssetId, etcCatalogEntry.Id);
            Assert.Equal(Path.Combine("Dialogues", "Etc.dialogue"), etcCatalogEntry.FileName);

            var etcAsset = new DialogueAsset();
            etcAsset.Load(JObject.Parse(File.ReadAllText(etcDialoguePath)));
            Assert.True(etcAsset.TryGetLineText("line:Etc_0067_p0", out var ouiText));
            Assert.Equal("OUI", ouiText);
            Assert.True(etcAsset.TryGetLineText("line:Etc_0068_p0", out var nonText));
            Assert.Equal("NON", nonText);
            Assert.True(etcAsset.TryGetLineText("line:Etc_0514_p0", out var etc514Text));
            Assert.Equal("Objet ETC 514", etc514Text);
            Assert.False(etcAsset.TryGetLineText("line:Etc_0896_p0", out _));
            Assert.False(etcAsset.TryGetLineText("line:Etc_0897_p0", out _));

            // Exact node set: only offsets 3656/3660/6184 resolve to a non-null value in the fixture's
            // ETC_RES.R.json, and EtcIndexTable.csv maps each of those offsets to exactly one index
            // (67, 68, 514) - verified against the shipped CSV, no duplicate index shares them.
            var etcYarn = File.ReadAllText(Path.Combine(outputDirectory, "Dialogues", "Etc.yarn"));
            Assert.Equal(new[] { "Etc_0067", "Etc_0068", "Etc_0514" }, TitlesOf(etcYarn));

            // ---- Counters ----
            // Nodes/lines: 3 (map 4: S000, S003, S004) + 1 (map 5: S000) + 128 (Shared) + 3 (Etc) = 135;
            // no '\A' in any fixture string, so every node is exactly one page (Lines == Nodes).
            Assert.Equal(4, report.Counters["Yarn.Files"]); // map 4, map 5, Shared, Etc
            Assert.Equal(135, report.Counters["Yarn.Nodes"]);
            Assert.Equal(135, report.Counters["Yarn.Lines"]);
            Assert.Equal(0, report.Counters.GetValueOrDefault("Yarn.EmptyPages"));
            Assert.Equal(0, report.Counters.GetValueOrDefault("Yarn.GlyphMarkers"));
            Assert.Equal(0, report.Counters.GetValueOrDefault("Yarn.FlagCommands"));
            Assert.Equal(0, report.Counters.GetValueOrDefault("Yarn.FalconUpdateCommands"));
            Assert.Equal(0, report.Counters.GetValueOrDefault("Yarn.FunctionCalls"));
            // Empty slots: map 4 (indices 1-2, 2) + map 5 (none) + Shared (none) + Etc (1024 - 3 = 1021).
            Assert.Equal(1023, report.Counters.GetValueOrDefault("Yarn.EmptySlots"));
            // Code inventory: the fixture's only control code is the single '\C' (voice) in M4_S003's
            // "\C#Disuse" source.
            Assert.Equal(1, report.Counters.GetValueOrDefault("Yarn.Code.\\C"));

            Assert.Contains(
                report.Messages,
                message => message.Contains("4 dialogue asset(s)", StringComparison.Ordinal)
                    && message.Contains("135 node(s)", StringComparison.Ordinal)
                    && message.Contains("135 line(s)", StringComparison.Ordinal)
                    && message.Contains("1023 empty slot(s)", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertDialogues_RunTwice_ProducesByteIdenticalFiles()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory1 = CreateTempDirectory();
        var outputDirectory2 = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteFixture(inputDirectory);
            var mapLocations = new Dictionary<int, MapLocation>
            {
                [4] = new("TestZone", "Test Map-4"),
                [5] = new("TestZone", "Test Map-5"),
            };

            EngineEnvironment.ProjectPath = outputDirectory1;
            EditorAssetCatalogService.Clear();
            YarnDialogueWriter.ConvertDialogues(inputDirectory, outputDirectory1, null, mapLocations, new ConversionReport());

            EngineEnvironment.ProjectPath = outputDirectory2;
            EditorAssetCatalogService.Clear();
            YarnDialogueWriter.ConvertDialogues(inputDirectory, outputDirectory2, null, mapLocations, new ConversionReport());

            var relativePaths = new[]
            {
                Path.Combine("Maps", "TestZone", "Test Map-4", "dialogues", "Test Map-4.yarn"),
                Path.Combine("Maps", "TestZone", "Test Map-4", "dialogues", "Test Map-4.dialogue"),
                Path.Combine("Dialogues", "Shared.yarn"),
                Path.Combine("Dialogues", "Shared.dialogue"),
                Path.Combine("Dialogues", "Etc.yarn"),
                Path.Combine("Dialogues", "Etc.dialogue"),
            };

            foreach (var relativePath in relativePaths)
            {
                var bytes1 = File.ReadAllBytes(Path.Combine(outputDirectory1, relativePath));
                var bytes2 = File.ReadAllBytes(Path.Combine(outputDirectory2, relativePath));
                Assert.True(bytes1.AsSpan().SequenceEqual(bytes2), $"{relativePath} differs between the two runs");
            }
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory1, recursive: true);
            Directory.Delete(outputDirectory2, recursive: true);
        }
    }

    [Fact]
    public void ConvertDialogues_HonoursTheMapsFilter_SharedAndEtcAlwaysWritten()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteFixture(inputDirectory);
            var mapLocations = new Dictionary<int, MapLocation>
            {
                [4] = new("TestZone", "Test Map-4"),
                [5] = new("TestZone", "Test Map-5"),
            };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            YarnDialogueWriter.ConvertDialogues(inputDirectory, outputDirectory, new[] { 4 }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-4", "dialogues", "Test Map-4.dialogue")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-5", "dialogues", "Test Map-5.dialogue")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Dialogues", "Shared.dialogue")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Dialogues", "Etc.dialogue")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertDialogues_MapWithAnEmitError_ReportsTheErrorAndLeavesTheNodeOut()
    {
        // \M is not a code YarnTextEmitter understands (docs/plan-e15-yarn.md §1): the whole entry is
        // reported as an emission error and its node never appears in the .yarn source, but the map's
        // other, valid string still gets its node.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            var tiledDirectory = Path.Combine(dataDirectory, "tiled");
            Directory.CreateDirectory(tiledDirectory);
            File.WriteAllText(Path.Combine(tiledDirectory, "map_9.tmj"), "{}");
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_9.json"),
                """{ "Strings": [ "Bonjour", "Cass\\Mure", null ] }""");
            WriteEmptyMapAlundraAndEtc(dataDirectory);

            var mapLocations = new Dictionary<int, MapLocation> { [9] = new("TestZone", "Test Map-9") };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            YarnDialogueWriter.ConvertDialogues(inputDirectory, outputDirectory, new[] { 9 }, mapLocations, report);

            Assert.Contains(report.Errors, error => error.Contains("M9_S001", StringComparison.Ordinal));
            Assert.Contains(report.Errors, error => error.Contains("Test Map-9.yarn", StringComparison.Ordinal));

            var yarnPath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-9", "dialogues", "Test Map-9.yarn");
            var source = File.ReadAllText(yarnPath);
            Assert.Contains("title: M9_S000", source, StringComparison.Ordinal);
            Assert.DoesNotContain("title: M9_S001", source, StringComparison.Ordinal);

            var dialoguePath = Path.Combine(outputDirectory, "Maps", "TestZone", "Test Map-9", "dialogues", "Test Map-9.dialogue");
            Assert.True(File.Exists(dialoguePath)); // the map's remaining valid node still compiles and is written
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // ---- Compile-error path: CompileWriteAndCatalog is exercised directly with a hand-written,
    // deliberately invalid Yarn source, since YarnTextEmitter's own escaping (T3, proven against the
    // whole corpus with zero diagnostics) makes it impossible to reach a real compiler error through
    // emitted Alundra text alone. ----

    [Fact]
    public void CompileWriteAndCatalog_InvalidYarnSource_ReportsAnErrorAndWritesNoDialogueOrCatalogEntry()
    {
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            // A dangling "->" with no destination is a real Yarn Spinner compiler error (a shortcut
            // option needs a following jump or node body), not something YarnTextEmitter could ever
            // produce (§1's ForbiddenLinePrefixes reject exactly this shape at emission time).
            const string brokenSource = "title: Broken\n---\n-> \n===\n";

            var report = new ConversionReport();
            var wrote = YarnDialogueWriter.CompileWriteAndCatalog(
                brokenSource,
                relativeYarnPath: "Dialogues/Broken.yarn",
                relativeDialoguePath: "Dialogues/Broken.dialogue",
                startNodeTitle: "Broken",
                catalogName: "dialogue_broken",
                idsKey: "dialogue:broken-test",
                contextLabel: "broken test table",
                report);

            Assert.False(wrote);
            Assert.NotEmpty(report.Errors);
            // Names the .yarn file (relative path, forward slashes, like the emit-error path below) and
            // carries the compiler's own diagnostic message, not just the writer's summary line.
            Assert.Contains(report.Errors, error => error.Contains("Broken.yarn", StringComparison.Ordinal));
            Assert.Contains(
                report.Errors,
                error => error.Contains(
                    "Unexpected \"===\" while reading a shortcut option statement", StringComparison.Ordinal));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Dialogues", "Broken.dialogue")));
            Assert.Empty(EditorAssetCatalogService.AssetInfos);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // ---- Real-data corpus (skips silently when data-extracted/ is absent, same convention as
    // AnimationEndClassifierTests.FindRealDataFile) --------------------------------------------------

    [Fact]
    public void ConvertDialogues_RealCorpus_MatchesTheMeasuredCountersAndEveryDialogueLoadsBack()
    {
        var dataExtractedDirectory = FindRealDataExtractedDirectory();
        if (dataExtractedDirectory is null)
        {
            return;
        }

        var mapsJsonPath = Path.Combine(AppContext.BaseDirectory, "maps.json");
        if (!File.Exists(mapsJsonPath))
        {
            return;
        }

        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            // maps.json itself carries a couple of pre-existing duplicate-id warnings (unrelated to
            // this writer); only mapCatalog.Locations matters here.
            var mapCatalog = MapCatalogReader.Read(mapsJsonPath);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            var stopwatch = Stopwatch.StartNew();
            YarnDialogueWriter.ConvertDialogues(dataExtractedDirectory, outputDirectory, null, mapCatalog.Locations, report);
            stopwatch.Stop();

            Assert.Empty(report.Errors);
            Assert.Empty(report.Warnings);

            Assert.Equal(485, report.Counters["Yarn.Files"]);
            Assert.Equal(24784, report.Counters["Yarn.Nodes"]);
            Assert.Equal(31757, report.Counters["Yarn.Lines"]);
            Assert.Equal(95, report.Counters["Yarn.EmptyPages"]);
            Assert.Equal(11182, report.Counters["Yarn.GlyphMarkers"]);
            Assert.Equal(932, report.Counters["Yarn.FlagCommands"]);
            Assert.Equal(7, report.Counters["Yarn.FalconUpdateCommands"]);
            Assert.Equal(20, report.Counters["Yarn.FunctionCalls"]);
            Assert.Equal(38192, report.Counters["Yarn.EmptySlots"]);

            var dialogueEntries = EditorAssetCatalogService.AssetInfos
                .Where(info => info.FileName.EndsWith(Constants.FileNameExtensions.Dialogue, StringComparison.Ordinal))
                .ToList();
            Assert.Equal(485, dialogueEntries.Count);

            foreach (var entry in dialogueEntries)
            {
                var fullPath = Path.Combine(outputDirectory, entry.FileName);
                Assert.True(File.Exists(fullPath), $"{entry.FileName} is catalogued but missing on disk");

                var document = JObject.Parse(File.ReadAllText(fullPath));
                Assert.Equal(entry.Id.ToString(), (string?)document["id"]);

                var asset = new DialogueAsset();
                asset.Load(document);
                Assert.True(asset.HasCompiledProgram, $"{entry.FileName} has no compiled program");
                Assert.NotEmpty(asset.LineTexts);
            }

            report.Messages.Add($"YarnDialogueWriterTests real-corpus run: {stopwatch.Elapsed.TotalSeconds:0.###} s.");
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // ---- Fixture data --------------------------------------------------------------------------

    private static void WriteFixture(string inputDirectory)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        var tiledDirectory = Path.Combine(dataDirectory, "tiled");
        Directory.CreateDirectory(tiledDirectory);

        // Map 4: index 0 non-empty, indices 1-2 empty (null/empty string), index 3 "#Disuse" (kept,
        // D-E15-7), the rest padded out to 5 slots.
        File.WriteAllText(Path.Combine(tiledDirectory, "map_4.tmj"), "{}");
        File.WriteAllText(
            Path.Combine(dataDirectory, "map_4.json"),
            """{ "Strings": [ "Bonjour !", null, "", "\\C#Disuse", "Au revoir." ] }""");

        // Map 5: a single non-empty slot, just enough to prove it also gets converted (and, in the
        // --maps-filtered test, that it is skipped).
        File.WriteAllText(Path.Combine(tiledDirectory, "map_5.tmj"), "{}");
        File.WriteAllText(Path.Combine(dataDirectory, "map_5.json"), """{ "Strings": [ "Salut" ] }""");

        // map_alundra.json: 128 non-empty entries (D-E15-3/E15.b contract: all 128 are non-empty in the
        // real corpus too).
        var sharedStrings = string.Join(
            ",", Enumerable.Range(0, 128).Select(i => $"\"Shared text {i.ToString(CultureInfo.InvariantCulture)}\""));
        File.WriteAllText(Path.Combine(dataDirectory, "map_alundra.json"), $$"""{ "Strings": [ {{sharedStrings}} ] }""");

        // ETC_RES.R.json: the real EtcIndexTable.csv resolves index 67 -> offset 3656, 68 -> offset
        // 3660, and 512+2=514 -> offset 6184 (verified against the shipped CSV); indices 896-1022 all
        // share offset 2048, whose value is null here, so none of them gets a node.
        File.WriteAllText(
            Path.Combine(dataDirectory, "ETC_RES.R.json"),
            """
            {
              "3656": "OUI",
              "3660": "NON",
              "6184": "Objet ETC 514",
              "2048": null
            }
            """);
    }

    private static void WriteEmptyMapAlundraAndEtc(string dataDirectory)
    {
        var sharedStrings = string.Join(
            ",", Enumerable.Range(0, 128).Select(i => $"\"Shared text {i.ToString(CultureInfo.InvariantCulture)}\""));
        File.WriteAllText(Path.Combine(dataDirectory, "map_alundra.json"), $$"""{ "Strings": [ {{sharedStrings}} ] }""");
        File.WriteAllText(Path.Combine(dataDirectory, "ETC_RES.R.json"), """{ "3656": "OUI" }""");
    }

    private static string RelativePath(string root, string fullPath)
        => Path.GetRelativePath(root, fullPath);

    /// <summary>The exact node set of a written .yarn source, in file order: every "title: X" line.</summary>
    private static string[] TitlesOf(string yarnSource)
        => yarnSource
            .Split('\n')
            .Where(line => line.StartsWith("title: ", StringComparison.Ordinal))
            .Select(line => line["title: ".Length..])
            .ToArray();

    private static string? FindRealDataExtractedDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted");
            if (File.Exists(Path.Combine(candidate, "data", "ETC_RES.R.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
