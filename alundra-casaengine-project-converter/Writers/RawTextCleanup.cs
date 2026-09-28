using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Deletes the raw text-table files a previous export left behind (docs/plan-e15-yarn.md, E15.d
/// contract item 2). <c>TextWriter</c> used to write four file families; E15.d removes it, and
/// nothing writes them any more, so a project exported before E15.d and then re-exported after it
/// would otherwise keep those files forever, stale and uncatalogued.
///
/// The list is closed on purpose - never a wildcard, never a directory, never anything else:
///  - <c>Dialogues/global-strings.json</c>, <c>Dialogues/etc-index.json</c> and
///    <c>Dialogues/control-codes.json</c>, every run;
///  - <c>{MapLocation.DialoguesDirectory}/{FileBaseName}.strings.json</c>, for each map this run
///    processes (the <c>--maps</c> filter applies exactly like it did to the writer that used to
///    produce these files).
///
/// An absent file is not an error - most exports after the first will not have one. A failed
/// deletion (the file is locked, for example) is reported as an error, not silently skipped.
/// <c>Yarn.RawTextFilesRemoved</c> is always written, even when nothing was removed, so report.json
/// always shows whether this step ran.
/// </summary>
internal static class RawTextCleanup
{
    private const string DialoguesFolder = "Dialogues";

    public static void RemoveDialoguesFolderFiles(string outputDirectory, ConversionReport report)
    {
        // Ensures the counter exists even when every RemoveIfPresent call below is a no-op.
        report.Increment("Yarn.RawTextFilesRemoved", 0);

        RemoveIfPresent(Path.Combine(outputDirectory, DialoguesFolder, "global-strings.json"), report);
        RemoveIfPresent(Path.Combine(outputDirectory, DialoguesFolder, "etc-index.json"), report);
        RemoveIfPresent(Path.Combine(outputDirectory, DialoguesFolder, "control-codes.json"), report);
    }

    public static void RemoveMapStringsFile(string outputDirectory, MapLocation location, ConversionReport report)
    {
        var path = Path.Combine(outputDirectory, location.DialoguesDirectory, $"{location.FileBaseName}.strings.json");
        RemoveIfPresent(path, report);
    }

    private static void RemoveIfPresent(string path, ConversionReport report)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
            report.Increment("Yarn.RawTextFilesRemoved");
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Yarn: failed to remove previous export's '{path}' - {exception.Message}");
        }
    }
}
