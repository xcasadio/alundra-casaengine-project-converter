using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
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
}
