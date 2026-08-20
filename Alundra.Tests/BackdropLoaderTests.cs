using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="BackdropLoader"/>'s path resolution and degraded-mode contract (see its class
/// doc) using a temporary fixture directory instead of a real converted project - mirrors
/// <see cref="MapEventProgramLoaderTests"/>'s own shape.
/// </summary>
public class BackdropLoaderTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraBackdropLoaderTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    [Fact]
    public void Load_WorldNameWithNoMapId_ReturnsNull()
    {
        var document = BackdropLoader.Load(_projectPath, "no id here");

        Assert.Null(document);
    }

    [Fact]
    public void Load_NoWorldIndexFile_ReturnsNull_DegradedMode()
    {
        var document = BackdropLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_MapIdMissingFromIndex_ReturnsNull_DegradedMode()
    {
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(Path.Combine(_projectPath, "Maps", "world-index.json"), "{\"1\":\"Maps\\\\Zone\\\\Other-1\\\\Other-1.world\"}");

        var document = BackdropLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_BackdropFileMissing_ReturnsNull_NoWarningExpected()
    {
        // The common case: most maps have no scrolling background at all, so BackdropWriter never
        // wrote a companion file - this must degrade silently to "nothing to draw", not an error path.
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(
            Path.Combine(_projectPath, "Maps", "world-index.json"),
            "{\"5\":\"Maps\\\\Zone\\\\Some Map-5\\\\Some Map-5.world\"}");

        var document = BackdropLoader.Load(_projectPath, "Some Map-5");

        Assert.Null(document);
    }

    [Fact]
    public void Load_WellFormedDocument_Parses()
    {
        var mapFolder = Path.Combine(_projectPath, "Maps", "Zone", "Some Map-5");
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        Directory.CreateDirectory(Path.Combine(mapFolder, "backdrop"));

        File.WriteAllText(
            Path.Combine(_projectPath, "Maps", "world-index.json"),
            "{\"5\":\"Maps\\\\Zone\\\\Some Map-5\\\\Some Map-5.world\"}");

        File.WriteAllText(
            Path.Combine(mapFolder, "backdrop", "Some Map-5.backdrop.json"),
            "{\n"
            + "  \"MapIndex\": 5,\n"
            + "  \"Enabled\": true,\n"
            + "  \"AnimNum\": 2,\n"
            + "  \"Layers\": [\n"
            + "    {\n"
            + "      \"LayerId\": 0,\n"
            + "      \"Mode\": \"Tiles\",\n"
            + "      \"DepthOrder\": 1,\n"
            + "      \"Ground\": true,\n"
            + "      \"BlendMode\": 1,\n"
            + "      \"AnimTimer\": 1,\n"
            + "      \"TextureAssetId\": \"5b3f5e6a-3f0e-4d3d-9f4a-1a2b3c4d5e6f\",\n"
            + "      \"Width\": 640,\n"
            + "      \"Height\": 480,\n"
            + "      \"Scrollar\": {\n"
            + "        \"FactorXNum\": 1, \"FactorXDenom\": 1,\n"
            + "        \"FactorYNum\": 1, \"FactorYDenom\": 1,\n"
            + "        \"ScrollXSpeed\": 0, \"ScrollXPeriod\": 10,\n"
            + "        \"ScrollYSpeed\": 0, \"ScrollYPeriod\": 5\n"
            + "      }\n"
            + "    },\n"
            + "    { \"LayerId\": 1, \"Mode\": \"Disabled\", \"DepthOrder\": 0, \"Ground\": false, \"BlendMode\": 0, \"AnimTimer\": 0 }\n"
            + "  ]\n"
            + "}");

        var document = BackdropLoader.Load(_projectPath, "Some Map-5");

        Assert.NotNull(document);
        Assert.Equal(5, document!.MapIndex);
        Assert.True(document.Enabled);
        Assert.Equal(2, document.Layers.Count);

        var layer0 = document.Layers[0];
        Assert.Equal("Tiles", layer0.Mode);
        Assert.True(layer0.Ground);
        Assert.Equal(1, layer0.BlendMode);
        Assert.NotNull(layer0.Scrollar);
        Assert.Equal(10, layer0.Scrollar!.ScrollXPeriod);
        Assert.Equal(640, layer0.Width);

        Assert.Equal("Disabled", document.Layers[1].Mode);
        Assert.Null(document.Layers[1].Scrollar);
    }
}
