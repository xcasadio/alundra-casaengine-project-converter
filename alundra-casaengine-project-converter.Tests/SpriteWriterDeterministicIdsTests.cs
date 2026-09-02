using System.Linq;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// D-N-6 (docs/plan-nettoyage-convertisseur.md): converts the same fixture bank into two fresh
/// output directories and asserts every .entity/.anim2d/.sprite file SpriteWriter wrote is
/// byte-identical across the two runs - nested shape ids (the body box and the per-frame collision
/// box fixtures) included - and that no written file carries the "Object {guid}" default-name leak
/// (fact 7, EditorJsonSaveHelper.cs/ObjectBase.cs) a fixture shape gets when it is never given a
/// stable Name.
///
/// The fixture bank exercises every additive-Guid-ctor construction site D-N-6 lists: the prefab
/// Entity, its TransformComponent/RenderProjectionComponent/AnimatedSpriteComponent/
/// CollisionComponent/DepthSortable2DComponent/CharacterControllerComponent, the body Box fixture,
/// a per-frame collision-keyframe Box fixture (two distinct volumes, so two Box shapes get
/// written), one Animation2dData and one SpriteData.
/// </summary>
public class SpriteWriterDeterministicIdsTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private const string CollisionA =
        """{ "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "Width": 20, "Depth": 14, "Height": 32 }""";

    private const string CollisionB =
        """{ "OffsetX": 0, "OffsetY": 0, "OffsetZ": 4, "Width": 8, "Depth": 8, "Height": 8 }""";

    [Fact]
    public void ConvertSprites_TwiceFromTheSameInput_WritesByteIdenticalEntityAnim2dAndSpriteFiles()
    {
        var inputDirectory = CreateTempDirectory();
        var firstOutputDirectory = CreateTempDirectory();
        var secondOutputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteFixtureInput(inputDirectory);

            ConvertOnce(inputDirectory, firstOutputDirectory);
            ConvertOnce(inputDirectory, secondOutputDirectory);

            var firstFiles = EnumerateConvertedFiles(firstOutputDirectory);
            var secondFiles = EnumerateConvertedFiles(secondOutputDirectory);

            Assert.NotEmpty(firstFiles);
            Assert.Equal(firstFiles.Keys.OrderBy(k => k, StringComparer.Ordinal), secondFiles.Keys.OrderBy(k => k, StringComparer.Ordinal));

            foreach (var relativePath in firstFiles.Keys)
            {
                var firstBytes = File.ReadAllBytes(firstFiles[relativePath]);
                var secondBytes = File.ReadAllBytes(secondFiles[relativePath]);
                Assert.True(
                    firstBytes.AsSpan().SequenceEqual(secondBytes),
                    $"'{relativePath}' differs between the two runs - ids are not fully deterministic.");

                var text = System.Text.Encoding.UTF8.GetString(firstBytes);
                Assert.DoesNotContain("Object ", text);
            }
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(firstOutputDirectory, recursive: true);
            Directory.Delete(secondOutputDirectory, recursive: true);
        }
    }

    private static void ConvertOnce(string inputDirectory, string outputDirectory)
    {
        EngineEnvironment.ProjectPath = outputDirectory;
        EditorAssetCatalogService.Clear();

        var report = new ConversionReport();
        ProjectWriter.CreateEmptyProject(outputDirectory, report);
        SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

        Assert.Empty(report.Errors);
    }

    private static Dictionary<string, string> EnumerateConvertedFiles(string outputDirectory)
    {
        var entitiesDirectory = Path.Combine(outputDirectory, "Entities");
        Assert.True(Directory.Exists(entitiesDirectory), $"'{entitiesDirectory}' was not created.");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var extension in new[] { "*.entity", "*.anim2d", "*.sprite" })
        {
            foreach (var path in Directory.GetFiles(entitiesDirectory, extension, SearchOption.AllDirectories))
            {
                result[Path.GetRelativePath(outputDirectory, path)] = path;
            }
        }

        return result;
    }

    /// <summary>
    /// One hero bank (map_alundra.json, Sector5Id 0 -&gt; CharacterControllerComponent, no body box)
    /// and one regular NPC-shaped bank (map_0.json, a positive body box -&gt; CollisionComponent +
    /// its own CharacterControllerComponent, plus a frame carrying two distinct CollisionData
    /// volumes across its two displayed frames -&gt; two Animation2dCollisionKeyframeData Box
    /// fixtures).
    /// </summary>
    private static void WriteFixtureInput(string inputDirectory)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);

        File.WriteAllText(
            Path.Combine(dataDirectory, "map_alundra.json"),
            """
            {
                "SpriteInfo": {
                    "SpriteRecords": [
                        {
                            "Header": { "Sector5Id": 0 },
                            "AnimSets": [ { "PreloadedAnims": [
                                { "Frames": [
                                    { "Delay": 136, "Images": { "Images": [
                                        { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 100 }
                                    ] } },
                                    { "Delay": 1, "Images": null }
                                ] },
                                null, null, null
                            ] } ]
                        }
                    ],
                    "SpriteEffectRecords": []
                }
            }
            """);

        File.WriteAllText(
            Path.Combine(dataDirectory, "map_0.json"),
            $$"""
            {
                "SpriteInfo": {
                    "SpriteRecords": [
                        {
                            "Header": { "Sector5Id": 60, "OffsetX": -10, "OffsetY": -7, "OffsetZ": 0, "SizeX": 20, "SizeY": 14, "SizeZ": 32 },
                            "AnimSets": [ { "PreloadedAnims": [
                                { "Frames": [
                                    { "Delay": 136, "CollisionData": {{CollisionA}}, "Images": { "Images": [
                                        { "Spritesheet": 0, "Palette": 0, "AtlasX": 0, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 600 }
                                    ] } },
                                    { "Delay": 136, "CollisionData": {{CollisionB}}, "Images": { "Images": [
                                        { "Spritesheet": 0, "Palette": 0, "AtlasX": 20, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": 601 }
                                    ] } },
                                    { "Delay": 1, "Images": null }
                                ] },
                                null, null, null
                            ] } ]
                        }
                    ]
                }
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
