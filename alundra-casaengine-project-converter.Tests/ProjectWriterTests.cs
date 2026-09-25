using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class ProjectWriterTests
{
    [Fact]
    public void CreateEmptyProject_ProducesAProjectThatReloadsWithoutError()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);

            Assert.True(File.Exists(Path.Combine(outputDirectory, "AlundraGame.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "AssetInfos.json")));

            ProjectSettingsHelper.Load(Path.Combine(outputDirectory, "AlundraGame.json"));

            Assert.Equal("AlundraGame", GameSettings.ProjectSettings.ProjectName);
            Assert.Equal("Alundra.dll", GameSettings.ProjectSettings.GameplayDllName);
            Assert.True(AssetCatalog.IsLoaded);
            Assert.Empty(AssetCatalog.AssetInfos);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    // ADR-0040 (engine) / parent plan P6: IsAudioMuted is set by hand in the project file and must survive
    // the phase 0 rewrite of that file.
    [Fact]
    public void CreateEmptyProject_KeepsAMuteSetByHandInTheExistingProjectFile()
    {
        RunInFreshOutputDirectory(outputDirectory =>
        {
            var projectFilePath = Path.Combine(outputDirectory, "AlundraGame.json");
            File.WriteAllText(projectFilePath, new JObject { ["ProjectName"] = "AlundraGame", ["IsAudioMuted"] = true }.ToString());

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);

            var rootElement = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.True(rootElement["IsAudioMuted"]?.Value<bool>());
            Assert.Equal("Alundra.dll", rootElement["GameplayDllName"]?.Value<string>());
            Assert.Empty(report.Warnings);
        });
    }

    [Fact]
    public void CreateEmptyProject_WithoutAMutedProjectFile_DoesNotWriteTheKey()
    {
        RunInFreshOutputDirectory(outputDirectory =>
        {
            var projectFilePath = Path.Combine(outputDirectory, "AlundraGame.json");

            ProjectWriter.CreateEmptyProject(outputDirectory, new ConversionReport());
            Assert.Null(JObject.Parse(File.ReadAllText(projectFilePath))["IsAudioMuted"]);

            // Second run over the file the first one wrote, which has no key either.
            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            Assert.Null(JObject.Parse(File.ReadAllText(projectFilePath))["IsAudioMuted"]);
            Assert.Empty(report.Warnings);
        });
    }

    [Fact]
    public void CreateEmptyProject_WithAnUnreadableProjectFile_RecreatesItWithoutTheKeyAndWarns()
    {
        RunInFreshOutputDirectory(outputDirectory =>
        {
            var projectFilePath = Path.Combine(outputDirectory, "AlundraGame.json");
            File.WriteAllText(projectFilePath, "{ this is not json");

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);

            var rootElement = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Null(rootElement["IsAudioMuted"]);
            Assert.Equal("AlundraGame", rootElement["ProjectName"]?.Value<string>());
            Assert.Contains(report.Warnings, warning => warning.Contains("IsAudioMuted", StringComparison.Ordinal));
        });
    }

    private static void RunInFreshOutputDirectory(Action<string> test)
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        var previousProjectPath = EngineEnvironment.ProjectPath;
        Directory.CreateDirectory(outputDirectory);

        try
        {
            test(outputDirectory);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
