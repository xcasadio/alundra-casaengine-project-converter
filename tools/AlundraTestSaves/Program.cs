using System.Globalization;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.SaveGames;

namespace AlundraTestSavesTool;

/// <summary>
/// E19.d2a S2 (docs/plan-e19-opcodes.md section 1.2h.1, D-E19-33): writes a preset test save (see
/// <see cref="AlundraTestSaves"/>) into the game's save folder, where F9 of the Debug DLL loads it.
/// <code>AlundraTestSaves &lt;AlundraGame.json&gt; &lt;preset&gt; [--dry-run]</code>
/// <c>--dry-run</c> prints what would be written and writes nothing. Exit code 0 on success, 2 on a usage error,
/// 1 on any refusal (unknown preset, unreadable project, a project name the engine would refuse, item tables not loaded, validation, write) and on any
/// unexpected exception.
/// </summary>
internal static class Program
{
    private const string Usage = "usage: AlundraTestSaves <path to AlundraGame.json> <preset> [--dry-run]";

    private static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        // E19.d2b B7: every refusal is exit code 1, an unexpected exception included (a project file held open by another program, a folder that cannot be
        // read): never the runtime's own crash code.
        try
        {
            return RunCore(args, output, error);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error.WriteLine($"unexpected failure: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static int RunCore(string[] args, TextWriter output, TextWriter error)
    {
        var dryRun = false;
        var positional = new List<string>();
        foreach (var arg in args)
        {
            if (arg == "--dry-run")
            {
                dryRun = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"unknown option '{arg}'. {Usage}");
                return 2;
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (positional.Count != 2)
        {
            error.WriteLine(Usage);
            error.WriteLine("presets: " + string.Join(", ", AlundraTestSaves.Presets.Select(p => p.Name)));
            return 2;
        }

        var projectFile = Path.GetFullPath(positional[0]);
        var presetName = positional[1];

        // 1. The project: its folder and its name (the save folder is named after it).
        if (!File.Exists(projectFile))
        {
            error.WriteLine($"project file not found: {projectFile}");
            return 1;
        }

        string projectName;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(projectFile));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ProjectName", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(nameElement.GetString()))
            {
                error.WriteLine($"'{projectFile}' has no string \"ProjectName\".");
                return 1;
            }

            projectName = nameElement.GetString()!;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"'{projectFile}' is not valid JSON: {ex.Message}");
            return 1;
        }

        // E19.d2b B7: the engine refuses a project name that is not a valid folder name when it writes (ADR-0044); --dry-run says so too, instead of
        // describing a file that the real run could never write.
        var nameRefusal = CheckProjectFolderName(projectName);
        if (nameRefusal != null)
        {
            error.WriteLine($"'{projectFile}': ProjectName \"{projectName}\" is refused: {nameRefusal}");
            return 1;
        }

        var projectDirectory = Path.GetDirectoryName(projectFile)!;
        GameSettings.ProjectSettings.ProjectName = projectName;

        // 2. A console log: without it the warnings of Logs go nowhere.
        Logs.AddLogger(new ConsoleLogger(output, error));

        // 3. The item tables: the loader never throws and answers zeroed tables for an absent file.
        var tables = AlundraItemTables.GetOrCreate(projectDirectory);
        if (!tables.ItemsProperties.Any(value => value != 0) || !tables.DropField3.Any(value => value != 0))
        {
            error.WriteLine($"the item tables of '{projectDirectory}' are not loaded (ItemsProperties or DropField3 is all zero): expected Data/items-properties.json and its siblings.");
            return 1;
        }

        // 4. The rules: the on-disk equivalent of the production asset catalog.
        var rules = new AlundraSaveGameRules(
            projectDirectory,
            path => File.Exists(Path.Combine(projectDirectory, path)),
            tables);

        // 5. The save.
        if (!AlundraTestSaves.TryFind(presetName, out var preset))
        {
            error.WriteLine($"unknown preset '{presetName}'. presets: " + string.Join(", ", AlundraTestSaves.Presets.Select(p => p.Name)));
            return 1;
        }

        if (!AlundraTestSaves.TryBuild(preset!, rules, out var save, out var buildError))
        {
            error.WriteLine(buildError);
            return 1;
        }

        var slotPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            projectName,
            "SaveGames",
            preset!.Slot + ".sav");

        if (dryRun)
        {
            Describe(output, preset, save!, slotPath);
            output.WriteLine("dry run: nothing written.");
            return 0;
        }

        var result = GameSettings.SaveGames.Save(preset.Slot, save!, SaveGameFormat.Json, save!.BuildMetadata());
        output.WriteLine($"save of slot '{preset.Slot}': {result}");
        if (!result.IsSuccess)
        {
            return 1;
        }

        output.WriteLine($"written: {slotPath}");
        return 0;
    }

    // Copy of the project folder name rule of the engine's ADR-0044 (SaveGameNames.ProjectFolderNameRegex and its reserved names, internal to the engine,
    // and the default name that SaveGameFileStorage refuses): 1 to 64 ASCII letters, digits, spaces, '_' and '-', starting with a letter or a digit, not
    // ending with a space, and not a Windows reserved device name.
    private static readonly System.Text.RegularExpressions.Regex ProjectFolderNameRule =
        new(@"\A[A-Za-z0-9](?:[A-Za-z0-9 _-]{0,62}[A-Za-z0-9_-])?\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Null when the engine would accept <paramref name="projectName"/> as the name of the save folder, else the reason it would refuse.</summary>
    internal static string? CheckProjectFolderName(string projectName)
    {
        if (string.Equals(projectName, "Project name undefined", StringComparison.Ordinal))
        {
            return "it is the default name of a project that never set its own.";
        }

        if (!ProjectFolderNameRule.IsMatch(projectName))
        {
            return "expected 1 to 64 ASCII letters, digits, spaces, '_' and '-', starting with a letter or a digit and not ending with a space.";
        }

        var dot = projectName.IndexOf('.');
        var stem = dot >= 0 ? projectName[..dot] : projectName;
        if (WindowsReservedNames.Any(reserved => string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase)))
        {
            return "it is a Windows reserved device name.";
        }

        return null;
    }

    private static void Describe(TextWriter output, AlundraTestSaves.Preset preset, AlundraSaveGame save, string slotPath)
    {
        output.WriteLine($"preset: {preset.Name}");
        output.WriteLine($"map: {save.InitialMapId}, tile ({save.CameraTileX}, {save.CameraTileY}), z {save.CameraTileZ}");
        output.WriteLine("flags set: " + FormatFlags(preset.FlagsToSet, save));
        output.WriteLine("flags cleared: " + FormatFlags(preset.FlagsToClear, save));
        output.WriteLine("map table: " + (preset.MapTableEntries.Count == 0
            ? "(identity)"
            : string.Join(", ", preset.MapTableEntries.Select(e => $"[{e.Index}] = {e.Value}"))));
        output.WriteLine("item counters: " + (preset.ItemCounts.Count == 0
            ? "(new game only)"
            : string.Join(", ", preset.ItemCounts.Select(e => $"[{e.Index}] = {e.Count}"))));
        output.WriteLine($"slot: {preset.Slot}");
        output.WriteLine($"file: {slotPath}");
    }

    private static string FormatFlags(IReadOnlyList<uint> flags, AlundraSaveGame save)
    {
        if (flags.Count == 0)
        {
            return "(none)";
        }

        var words = flags.Select(f => (int)(f >> 5)).Distinct().OrderBy(w => w);
        var text = string.Join(", ", flags.Select(f => $"G{f}"));
        var values = string.Join(", ", words.Select(w => $"GameFlags[{w}] = 0x{save.GameFlags[w].ToString("X8", CultureInfo.InvariantCulture)}"));
        return text + " (" + values + ")";
    }

    private sealed class ConsoleLogger : ILogger
    {
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        public ConsoleLogger(TextWriter output, TextWriter error)
        {
            _output = output;
            _error = error;
        }

        public void Close()
        {
        }

        public void WriteTrace(string msg) => _output.WriteLine("[trace] " + msg);

        public void WriteDebug(string msg) => _output.WriteLine("[debug] " + msg);

        public void WriteInfo(string msg) => _output.WriteLine("[info] " + msg);

        public void WriteWarning(string msg) => _error.WriteLine("[warning] " + msg);

        public void WriteError(string msg) => _error.WriteLine("[error] " + msg);
    }
}
