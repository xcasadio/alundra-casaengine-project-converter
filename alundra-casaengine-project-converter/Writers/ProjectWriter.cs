using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Configuration.Project;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 0: bootstraps an empty CasaEngine project (project file + empty asset catalog + content
/// folder layout) that later phases populate.
/// </summary>
public static class ProjectWriter
{
    public const string ProjectName = "AlundraGame";

    private static readonly string[] ContentFolders =
    {
        "Maps",
        "TileSets",
        "Textures",
        "Sprites",
        "Animations",
        "Sounds",
        "Musics",
        "Dialogues",
        "UI",
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
}
