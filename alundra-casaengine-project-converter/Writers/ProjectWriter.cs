using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 0: bootstraps an empty CasaEngine project (project file + empty asset catalog + content
/// folder layout) that later phases populate.
/// </summary>
public static class ProjectWriter
{
    public const string ProjectName = "AlundraGame";

    // No "Worlds" and no "Events": a map's world and event bytecode live inside that map's own
    // folder under Maps/ (see MapLocation), so those top-level trees would only ever stay empty.
    private static readonly string[] ContentFolders =
    {
        MapLocation.MapsRootFolder,
        "TileSets",
        "Textures",
        "Entities",
        "Sprites",
        "Animations",
        "Sounds",
        "Musics",
        "Dialogues",
        "UI",
        "Data",
    };

    public static void CreateEmptyProject(string outputDirectory, ConversionReport report)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (var folder in ContentFolders)
        {
            Directory.CreateDirectory(Path.Combine(outputDirectory, folder));
        }

        EngineEnvironment.ProjectPath = outputDirectory;

        var projectSettings = new ProjectSettings
        {
            WindowTitle = "Alundra",
            ProjectName = ProjectName,
            FirstScreenName = string.Empty,
            AllowUserResizing = true,
            IsFixedTimeStep = false,
            IsMouseVisible = true,
            FirstWorldLoaded = string.Empty,
            GameplayDllName = string.Empty,
            ExternalToolsDirectory = "ExternalTools",
        };

        ProjectSettingsHelper.Save(Path.Combine(outputDirectory, $"{ProjectName}.json"), projectSettings);
        report.Counters["ProjectFiles"] = 1;
        report.Messages.Add($"Created project file '{ProjectName}.json' in '{outputDirectory}'.");

        EditorAssetCatalogService.Clear();
        EditorAssetCatalogService.Save();
        report.Counters["Assets"] = 0;
        report.Messages.Add("Created empty AssetInfos.json.");
    }

    /// <summary>
    /// Points the generated project at its entry world. Separate from
    /// <see cref="CreateEmptyProject"/> because that runs in Phase 0, long before any .world
    /// exists: the value can only be known once Phase 6 has actually written the worlds, and it
    /// must be the very path Phase 6 registered in the asset catalog (GameManager resolves
    /// FirstWorldLoaded through AssetCatalog.GetByFileName, an ordinal lookup).
    ///
    /// Only FirstWorldLoaded is touched; every other setting written in Phase 0 is left as is.
    /// GameplayDllName stays empty on purpose - that DLL is out of this converter's scope
    /// (plan section 6).
    /// </summary>
    public static void SetFirstWorldLoaded(string outputDirectory, string worldRelativePath, ConversionReport report)
    {
        var projectFilePath = Path.Combine(outputDirectory, $"{ProjectName}.json");
        if (!File.Exists(projectFilePath))
        {
            report.Warnings.Add(
                $"Project: '{projectFilePath}' not found; FirstWorldLoaded not set (run Phase 0 first).");
            return;
        }

        // Read/modify/write the JObject rather than ProjectSettingsHelper.Load(): Load() also
        // resolves EngineEnvironment.ProjectPath, reloads AssetInfos.json into the process-global
        // AssetCatalog and would try to load the gameplay DLL - all side effects the converter
        // must not trigger in the middle of a run.
        var rootElement = JObject.Parse(File.ReadAllText(projectFilePath));
        rootElement["FirstWorldLoaded"] = worldRelativePath;
        File.WriteAllText(projectFilePath, rootElement.ToString());

        report.Messages.Add($"Project: FirstWorldLoaded set to '{worldRelativePath}'.");
    }
}
