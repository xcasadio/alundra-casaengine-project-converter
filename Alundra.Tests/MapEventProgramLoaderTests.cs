using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="MapEventProgramLoader"/>'s path resolution and degraded-mode contract (see its
/// class doc) using a temporary fixture directory instead of a real converted project.
/// </summary>
public class MapEventProgramLoaderTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraMapEventProgramLoaderTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("Ship Klark (beginning)-389", true, 389)]
    [InlineData("Test Map-0", true, 0)]
    [InlineData("No trailing id", false, 0)]
    [InlineData("", false, 0)]
    public void TryParseMapIndex_ParsesTrailingDashId(string worldName, bool expectSuccess, int expectedIndex)
    {
        var success = MapEventProgramLoader.TryParseMapIndex(worldName, out var mapIndex);

        Assert.Equal(expectSuccess, success);
        if (expectSuccess)
        {
            Assert.Equal(expectedIndex, mapIndex);
        }
    }

    [Fact]
    public void Load_NoWorldIndexFile_ReturnsNull_DegradedMode()
    {
        var document = MapEventProgramLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_WorldNameWithNoMapId_ReturnsNull_DegradedMode()
    {
        var document = MapEventProgramLoader.Load(_projectPath, "no id here");

        Assert.Null(document);
    }

    [Fact]
    public void Load_MapIdMissingFromIndex_ReturnsNull_DegradedMode()
    {
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(Path.Combine(_projectPath, "Maps", "world-index.json"), "{\"1\":\"Maps\\\\Zone\\\\Other-1\\\\Other-1.world\"}");

        var document = MapEventProgramLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_EventsFileMissing_ReturnsNull_DegradedMode()
    {
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(
            Path.Combine(_projectPath, "Maps", "world-index.json"),
            "{\"5\":\"Maps\\\\Zone\\\\Some Map-5\\\\Some Map-5.world\"}");

        var document = MapEventProgramLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_WellFormedDocument_Parses()
    {
        var mapFolder = Path.Combine(_projectPath, "Maps", "Zone", "Some Map-5");
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        Directory.CreateDirectory(Path.Combine(mapFolder, "events"));

        File.WriteAllText(
            Path.Combine(_projectPath, "Maps", "world-index.json"),
            "{\"5\":\"Maps\\\\Zone\\\\Some Map-5\\\\Some Map-5.world\"}");

        File.WriteAllText(
            Path.Combine(mapFolder, "events", "Some Map-5.events.json"),
            "{\n"
            + "  \"MapIndex\": 5,\n"
            + "  \"EventCodesATable\": [0, 132],\n"
            + "  \"EventCodesBTable\": [0],\n"
            + "  \"EventCodesCTable\": [0],\n"
            + "  \"EventCodesDTable\": [0],\n"
            + "  \"EventCodesETable\": [0],\n"
            + "  \"EventCodesFTable\": [0],\n"
            + "  \"Codes\": [26, 10, 255]\n"
            + "}");

        var document = MapEventProgramLoader.Load(_projectPath, "Some Map-5");

        Assert.NotNull(document);
        Assert.Equal(5, document!.MapIndex);
        Assert.Equal(new[] { 0, 132 }, document.EventCodesATable);
        Assert.Equal(new byte[] { 26, 10, 255 }, document.CodesAsBytes());
    }
}
