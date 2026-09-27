using System.Runtime.CompilerServices;
using System.Text;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Text;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Serialization;
using Newtonsoft.Json.Linq;

// Test-only seam: YarnDialogueWriterTests exercises the compile/catalog half of the pipeline
// (CompileWriteAndCatalog) with a hand-written Yarn source to prove the "compile error -> no
// .dialogue, no catalog entry" path, since YarnTextEmitter's own escaping (T3) makes it impossible to
// reach a real compiler diagnostic through emitted text alone.
[assembly: InternalsVisibleTo("alundra-casaengine-project-converter.Tests")]

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 5 (docs/plan-e15-yarn.md, E15.b task T4): compiles every Alundra text table into a Yarn
/// dialogue asset, next to the raw <c>.strings.json</c> tables <see cref="TextWriter"/> still writes
/// (D-E15-4 removes those in E15.d, not here). One asset per table (E15.b "Contrat des fichiers et des
/// nœuds"):
///  - each map's 128-string table -&gt; <c>{MapLocation.FileBaseName}.yarn</c>/<c>.dialogue</c> in the
///    map's own <see cref="MapLocation.DialoguesDirectory"/>, nodes <c>M{id}_S{nnn}</c>;
///  - the shared <c>map_alundra.json</c> table -&gt; <c>Dialogues/Shared.yarn</c>/<c>.dialogue</c>,
///    nodes <c>Shared_S{nnn}</c>;
///  - the ETC table (<c>EtcIndexTable.csv</c> resolved against <c>data/ETC_RES.R.json</c>) -&gt;
///    <c>Dialogues/Etc.yarn</c>/<c>.dialogue</c>, nodes <c>Etc_{iiii}</c> (decimal index, D-E15-11).
///
/// An empty slot (null or empty string) gets no node (D-E15-10); a table with no non-empty slot at all
/// writes no file (none in the corpus - every map has at least one non-empty string, measured
/// 2026-09-27). Each table is emitted by <see cref="YarnTextEmitter"/>, compiled by
/// <see cref="YarnDialogueCompiler"/> with <see cref="AlundraYarnFunctions.CreateDeclarations"/>, and
/// only catalogued when the compile has no error - a table whose compile fails gets no
/// <c>.dialogue</c> and no catalog entry, even though its <c>.yarn</c> source is still written (E15.b
/// task T4). <see cref="DialogueAssetJsonSerializer.Save"/> serializes the asset with its own random
/// <see cref="CasaEngine.Framework.Common.ObjectBase.Id"/>; that id is overwritten with
/// <see cref="Ids.For"/> before the document is written, exactly like <see cref="PlayerSetupWriter"/>
/// does for its own hand-built documents, so the asset id stays stable across runs and
/// <see cref="EditorAssetWriterService.SaveAsset"/> (which throws for a <see cref="DialogueAsset"/>) is
/// never used.
/// </summary>
public static class YarnDialogueWriter
{
    private const string DialoguesFolder = "Dialogues";
    private const string SharedFileBaseName = "Shared";
    private const string EtcFileBaseName = "Etc";

    public static void ConvertDialogues(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        ConvertMapTables(inputDirectory, outputDirectory, mapFilter, mapLocations, report);
        ConvertSharedTable(inputDirectory, outputDirectory, report);
        ConvertEtcTable(inputDirectory, outputDirectory, report);

        // One save for the whole phase: every table above may have added a catalog entry, and the
        // catalog is only meant to be flushed once all of them are known (E15.b task T4).
        EditorAssetCatalogService.Save();

        report.Messages.Add(
            $"Yarn: {report.Counters.GetValueOrDefault("Yarn.Files")} dialogue asset(s), "
            + $"{report.Counters.GetValueOrDefault("Yarn.Nodes")} node(s), "
            + $"{report.Counters.GetValueOrDefault("Yarn.Lines")} line(s), "
            + $"{report.Counters.GetValueOrDefault("Yarn.EmptySlots")} empty slot(s) with no node.");
    }

    // ---- Map tables -----------------------------------------------------------------------------

    private static void ConvertMapTables(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMapTable(inputDirectory, outputDirectory, mapIndex, mapLocations, report);
        }
    }

    private static void ConvertMapTable(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var mapJsonPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
        if (!File.Exists(mapJsonPath))
        {
            report.Warnings.Add($"Yarn: map_{mapIndex}: native map file not found at '{mapJsonPath}'.");
            return;
        }

        IReadOnlyList<string?> strings;
        try
        {
            strings = StringTableReader.ReadMapStrings(mapJsonPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Yarn: map_{mapIndex}: failed to read strings - {exception.Message}");
            return;
        }

        if (strings.Count == 0)
        {
            report.Warnings.Add($"Yarn: map_{mapIndex}: no 'Strings' array in '{mapJsonPath}'.");
            return;
        }

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var entries = BuildEntries(strings, index => $"M{mapIndex}_S{index:000}");
        report.Increment("Yarn.EmptySlots", strings.Count - entries.Count);

        var relativeYarnPath = Path.Combine(location.DialoguesDirectory, $"{location.FileBaseName}.yarn");
        var relativeDialoguePath = Path.Combine(
            location.DialoguesDirectory, $"{location.FileBaseName}{Constants.FileNameExtensions.Dialogue}");

        WriteAsset(
            outputDirectory,
            relativeYarnPath,
            relativeDialoguePath,
            entries,
            catalogName: $"dialogue_{mapIndex}",
            idsKey: $"dialogue:map:{mapIndex}",
            contextLabel: $"map_{mapIndex}",
            report);
    }

    // ---- Shared table (map_alundra.json) ---------------------------------------------------------

    private static void ConvertSharedTable(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var mapAlundraPath = Path.Combine(inputDirectory, "data", "map_alundra.json");
        if (!File.Exists(mapAlundraPath))
        {
            report.Errors.Add($"Yarn: shared table not found at '{mapAlundraPath}'; Dialogues/Shared not written.");
            return;
        }

        IReadOnlyList<string?> strings;
        try
        {
            strings = StringTableReader.ReadMapStrings(mapAlundraPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Yarn: shared table: failed to read strings - {exception.Message}");
            return;
        }

        var entries = BuildEntries(strings, index => $"Shared_S{index:000}");
        report.Increment("Yarn.EmptySlots", strings.Count - entries.Count);

        var relativeYarnPath = Path.Combine(DialoguesFolder, $"{SharedFileBaseName}.yarn");
        var relativeDialoguePath = Path.Combine(DialoguesFolder, $"{SharedFileBaseName}{Constants.FileNameExtensions.Dialogue}");

        WriteAsset(
            outputDirectory,
            relativeYarnPath,
            relativeDialoguePath,
            entries,
            catalogName: "dialogue_shared",
            idsKey: "dialogue:shared",
            contextLabel: "shared table",
            report);
    }

    // ---- ETC table (EtcIndexTable.csv -> data/ETC_RES.R.json) --------------------------------------

    private static void ConvertEtcTable(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "EtcIndexTable.csv");
        if (!File.Exists(csvPath))
        {
            report.Errors.Add($"Yarn: EtcIndexTable.csv not found at '{csvPath}'; Dialogues/Etc not written.");
            return;
        }

        var indexResult = EtcIndexCatalogReader.Read(csvPath);
        // EtcIndexTable.csv's own warnings (duplicate/invalid rows) are also reported by TextWriter,
        // which reads the same CSV; E15.d removes TextWriter's copy, so this duplication is temporary.
        foreach (var warning in indexResult.Warnings)
        {
            report.Warnings.Add(warning);
        }

        if (indexResult.ValueByIndex.Count != 1024)
        {
            report.Errors.Add(
                $"Yarn: EtcIndexTable.csv has {indexResult.ValueByIndex.Count} entries, expected 1024; "
                + "Dialogues/Etc not written.");
            return;
        }

        var etcJsonPath = Path.Combine(inputDirectory, "data", "ETC_RES.R.json");
        if (!File.Exists(etcJsonPath))
        {
            report.Errors.Add($"Yarn: '{etcJsonPath}' not found; Dialogues/Etc not written.");
            return;
        }

        IReadOnlyList<GlobalStringEntry> globalEntries;
        try
        {
            globalEntries = StringTableReader.ReadGlobalTable(etcJsonPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Yarn: failed to read '{etcJsonPath}' - {exception.Message}");
            return;
        }

        var valueByOffset = new Dictionary<int, string?>();
        foreach (var entry in globalEntries)
        {
            if (entry.Offset is int offset)
            {
                valueByOffset[offset] = entry.Value;
            }
        }

        var entries = new List<YarnTextEntry>();
        for (var index = 0; index < indexResult.ValueByIndex.Count; index++)
        {
            var offset = indexResult.ValueByIndex[index];
            if (valueByOffset.TryGetValue(offset, out var value) && !string.IsNullOrEmpty(value))
            {
                entries.Add(new YarnTextEntry($"Etc_{index:0000}", value));
            }
        }

        report.Increment("Yarn.EmptySlots", indexResult.ValueByIndex.Count - entries.Count);

        var relativeYarnPath = Path.Combine(DialoguesFolder, $"{EtcFileBaseName}.yarn");
        var relativeDialoguePath = Path.Combine(DialoguesFolder, $"{EtcFileBaseName}{Constants.FileNameExtensions.Dialogue}");

        WriteAsset(
            outputDirectory,
            relativeYarnPath,
            relativeDialoguePath,
            entries,
            catalogName: "dialogue_etc",
            idsKey: "dialogue:etc",
            contextLabel: "ETC table",
            report);
    }

    // ---- Shared per-asset pipeline ----------------------------------------------------------------

    private static List<YarnTextEntry> BuildEntries(IReadOnlyList<string?> strings, Func<int, string> titleForIndex)
    {
        var entries = new List<YarnTextEntry>();
        for (var index = 0; index < strings.Count; index++)
        {
            var value = strings[index];
            if (!string.IsNullOrEmpty(value))
            {
                entries.Add(new YarnTextEntry(titleForIndex(index), value));
            }
        }

        return entries;
    }

    /// <summary>
    /// Emits, writes, compiles and catalogues one text table's Yarn dialogue asset
    /// (docs/plan-e15-yarn.md, E15.b task T4, steps 1-4). Writes <paramref name="relativeYarnPath"/>
    /// unconditionally once there is at least one non-empty slot; writes and catalogues
    /// <paramref name="relativeDialoguePath"/> only when the compile has no error.
    /// </summary>
    private static void WriteAsset(
        string outputDirectory,
        string relativeYarnPath,
        string relativeDialoguePath,
        IReadOnlyList<YarnTextEntry> entries,
        string catalogName,
        string idsKey,
        string contextLabel,
        ConversionReport report)
    {
        if (entries.Count == 0)
        {
            report.Warnings.Add($"Yarn: {contextLabel} has no non-empty string; no file written.");
            return;
        }

        var emitResult = YarnTextEmitter.Emit(entries);
        // Relative .yarn path with forward slashes, exactly as CompileWriteAndCatalog's own compile
        // diagnostics name the file below, so an emit error and a compile error are equally locatable.
        var yarnFilePath = relativeYarnPath.Replace('\\', '/');
        foreach (var error in emitResult.Errors)
        {
            report.Errors.Add(
                $"Yarn: {contextLabel} ({yarnFilePath}) node '{error.Title}' page {error.PageIndex}: {error.Message}");
        }

        foreach (var (code, count) in emitResult.Statistics.CodeCounts)
        {
            report.Increment($"Yarn.Code.{code}", count);
        }

        // Written exactly as emitted: UTF-8 without a byte-order mark, LF line endings (the emitter
        // never produces '\r').
        var yarnFullPath = Path.Combine(outputDirectory, relativeYarnPath);
        Directory.CreateDirectory(Path.GetDirectoryName(yarnFullPath)!);
        File.WriteAllText(yarnFullPath, emitResult.Source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // A node the emitter reported an error for is already left out of emitResult.Source; the first
        // node actually present in the source is the first entry whose title is not among those errors.
        var failedTitles = new HashSet<string>(emitResult.Errors.Select(error => error.Title), StringComparer.Ordinal);
        var startNodeTitle = entries.Select(entry => entry.Title).FirstOrDefault(title => !failedTitles.Contains(title));
        if (startNodeTitle is null)
        {
            report.Warnings.Add($"Yarn: {contextLabel} has no node left after emission errors; no .dialogue written.");
            return;
        }

        var wrote = CompileWriteAndCatalog(
            emitResult.Source, relativeYarnPath, relativeDialoguePath, startNodeTitle, catalogName, idsKey, contextLabel, report);

        if (!wrote)
        {
            return;
        }

        report.Increment("Yarn.Files");
        report.Increment("Yarn.Nodes", emitResult.Statistics.Nodes);
        report.Increment("Yarn.Lines", emitResult.Statistics.Lines);
        report.Increment("Yarn.EmptyPages", emitResult.Statistics.EmptyPages);
        report.Increment("Yarn.GlyphMarkers", emitResult.Statistics.GlyphMarkers);
        report.Increment("Yarn.FlagCommands", emitResult.Statistics.FlagCommands);
        report.Increment("Yarn.FalconUpdateCommands", emitResult.Statistics.FalconUpdateCommands);
        report.Increment("Yarn.FunctionCalls", emitResult.Statistics.FunctionCalls);
    }

    /// <summary>
    /// Compiles <paramref name="yarnSource"/> (already written to <paramref name="relativeYarnPath"/> by
    /// the caller) and, only when the compile has no error, writes the <c>.dialogue</c> document and
    /// catalogues it. Every diagnostic of <c>Error</c> severity, or a compile that
    /// <see cref="YarnDialogueCompilationResult.ContainsErrors"/>, goes to <paramref name="report"/>'s
    /// errors and leaves no <c>.dialogue</c> and no catalog entry for this table; other diagnostics go to
    /// its warnings. Returns whether the asset was written and catalogued.
    ///
    /// <c>internal</c> only so <c>YarnDialogueWriterTests</c> can drive this half of the pipeline with a
    /// hand-written, deliberately invalid Yarn source: <see cref="YarnTextEmitter"/>'s own escaping (T3)
    /// makes it impossible to reach a real compiler diagnostic through emitted Alundra text alone, so the
    /// "compile error" path needs this seam to be exercised at all.
    /// </summary>
    internal static bool CompileWriteAndCatalog(
        string yarnSource,
        string relativeYarnPath,
        string relativeDialoguePath,
        string startNodeTitle,
        string catalogName,
        string idsKey,
        string contextLabel,
        ConversionReport report)
    {
        var compiler = new YarnDialogueCompiler();
        var compileFileName = relativeYarnPath.Replace('\\', '/');
        var compileResult = compiler.CompileString(yarnSource, compileFileName, AlundraYarnFunctions.CreateDeclarations());

        foreach (var diagnostic in compileResult.Diagnostics)
        {
            var message =
                $"Yarn: {contextLabel} ({diagnostic.FileName}:{diagnostic.Line}) {diagnostic.Code}: {diagnostic.Message}";
            if (string.Equals(diagnostic.Severity, "Error", StringComparison.OrdinalIgnoreCase))
            {
                report.Errors.Add(message);
            }
            else
            {
                report.Warnings.Add(message);
            }
        }

        if (compileResult.ContainsErrors)
        {
            report.Errors.Add($"Yarn: {contextLabel} failed to compile; no .dialogue written, no catalog entry.");
            return false;
        }

        var assetId = Ids.For(idsKey);
        var asset = DialogueAsset.FromCompiledProgram(catalogName, startNodeTitle, compileResult.ProgramBytes, compileResult.LineTexts);

        var node = new JObject();
        DialogueAssetJsonSerializer.Save(asset, node);
        // DialogueAssetJsonSerializer.Save writes the asset's own random Id; it is overwritten here so
        // the file's "id" and the catalog entry's id both carry the deterministic Ids.For(idsKey)
        // (same pattern as PlayerSetupWriter's hand-built documents).
        node["id"] = assetId.ToString();

        EditorAssetWriterService.SaveDocument(relativeDialoguePath, node);
        EditorAssetCatalogService.Add(new AssetInfo(assetId)
        {
            Name = catalogName,
            FileName = relativeDialoguePath,
        });

        return true;
    }
}
