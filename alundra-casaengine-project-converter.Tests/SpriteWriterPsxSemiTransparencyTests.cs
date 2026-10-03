using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Sprites;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.g G2a (ADR-0033 here, ADR-0051 in the engine, D-E19-52): the writer gives each entity <c>.sprite</c> the PSX
/// semi-transparency mode of its quads. The mode is read on <c>SpriteQuad.Spritesheet</c> (bit 3 enables it, bits 4-5 are the
/// ABR), never on the <c>Signature</c>: the synthetic fixtures of this repository carry <c>Spritesheet</c> 0 and arbitrary
/// low bytes of <c>Signature</c>, which must stay <see cref="SpritePsxSemiTransparency.None"/>.
/// </summary>
public class SpriteWriterPsxSemiTransparencyTests
{
    private const string PsxKey = "psx_semi_transparency";

    // Minimal 8-byte PNG signature: enough for the writer's file-copy pipeline, which does not decode image content.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    // ---- synthetic fixtures ----

    // (Signature, Spritesheet, expected mode). The low byte of each Signature is NOT the Spritesheet on purpose:
    //  - 3001 = 0xBBB9: low byte 0xB9 (bit 3 set, ABR 3) but Spritesheet 0x28 (ABR 2): the mode must be 2;
    //  - 3002 = 0x0BBA: low byte 0xBA (bit 3 set) but Spritesheet 0: not semi-transparent;
    //  - 4120 = 0x1018: low byte 0x18 (bit 3 set, ABR 1) but Spritesheet 0: not semi-transparent.
    private static readonly (long Signature, int Spritesheet, SpritePsxSemiTransparency Expected)[] SyntheticQuads =
    {
        (3001, 0x28, SpritePsxSemiTransparency.Mode2),
        (3002, 0x00, SpritePsxSemiTransparency.None),
        (4120, 0x00, SpritePsxSemiTransparency.None),
        (3003, 0x08, SpritePsxSemiTransparency.Mode0),
        (3004, 0x38, SpritePsxSemiTransparency.Mode3),
        (3005, 0x18, SpritePsxSemiTransparency.Mode1),
    };

    [Fact]
    public void ConvertSprites_WritesTheModeOfTheSpritesheetByte_NeverOfTheSignature()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_0_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);
            File.WriteAllText(Path.Combine(dataDirectory, "map_0.json"), BuildMapJson());
            File.WriteAllText(
                Path.Combine(dataDirectory, "map_alundra.json"),
                """{ "SpriteInfo": { "SpriteRecords": [], "SpriteEffectRecords": [] } }""");

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(SyntheticQuads.Length, report.Counters["Sprites.QuadsConverted"]);

            foreach (var (signature, _, expected) in SyntheticQuads)
            {
                var path = FindEntityAsset(outputDirectory, $"sprite_{signature}.sprite");
                var document = JObject.Parse(File.ReadAllText(path));

                var sprite = new SpriteData();
                sprite.Load(document);
                Assert.True(expected == sprite.PsxSemiTransparency,
                    $"sprite_{signature}: expected {expected}, read {sprite.PsxSemiTransparency}.");

                // A sprite without a mode carries no field at all, so its file is the one written before the field existed.
                Assert.Equal(expected != SpritePsxSemiTransparency.None, document.ContainsKey(PsxKey));
                if (expected != SpritePsxSemiTransparency.None)
                {
                    Assert.Equal(expected.ToString(), document[PsxKey]?.Value<string>());
                }
            }
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string BuildMapJson()
    {
        var frames = new List<string>();
        for (var index = 0; index < SyntheticQuads.Length; index++)
        {
            var (signature, spritesheet, _) = SyntheticQuads[index];
            frames.Add(
                $$"""
                { "Delay": 10, "Images": { "Images": [
                    { "Spritesheet": {{spritesheet}}, "Palette": 0, "AtlasX": {{index * 20}}, "AtlasY": 0, "Swidth": 16, "Sheight": 16, "X1": -8, "Y1": -16, "X2": 8, "Y2": -16, "X3": -8, "Y3": 0, "X4": 8, "Y4": 0, "Signature": {{signature}} }
                ] } }
                """);
        }

        return
            $$"""
            { "SpriteInfo": { "SpriteRecords": [ {
                "Header": { "Sector5Id": 5 },
                "AnimSets": [ { "PreloadedAnims": [ { "Frames": [ {{string.Join(",", frames)}} ] }, null, null, null ] } ]
            } ] } }
            """;
    }

    // ---- real data ----

    // Real quads of data-extracted/data (the audit list of the 1836 semi-transparent entity sprites): the hero bank holds
    // sprite_16561416966684 (ABR 1, Entities/Alundra) and sprite_17694743594812 (ABR 3, Entities/Nuage de sable); map_100 holds
    // sprite_17696611633418 (ABR 0, Entities/◆Homme-ombre Niv.1). Skips (returns) when data-extracted/ is not next to the binaries.
    [Fact]
    public void ConvertSprites_OnRealData_WritesModes0And1And3_AndLeavesTheOthersWithoutField()
    {
        var mapAlundraPath = FindRealDataFile("map_alundra.json");
        var map100Path = FindRealDataFile("map_100.json");
        if (mapAlundraPath is null || map100Path is null)
        {
            return;
        }

        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.Copy(mapAlundraPath, Path.Combine(dataDirectory, "map_alundra.json"));
            File.Copy(map100Path, Path.Combine(dataDirectory, "map_100.json"));
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_alundra_spritesheet.png"), FakePngBytes);
            File.WriteAllBytes(Path.Combine(dataDirectory, "map_100_spritesheet.png"), FakePngBytes);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            SpriteWriter.ConvertSprites(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);

            AssertMode(outputDirectory, "Alundra", 16561416966684, SpritePsxSemiTransparency.Mode1);
            AssertMode(outputDirectory, "Nuage de sable", 17694743594812, SpritePsxSemiTransparency.Mode3);
            AssertMode(outputDirectory, "◆Homme-ombre Niv.1", 17696611633418, SpritePsxSemiTransparency.Mode0);

            // Every written sprite: the mode is the one of the Spritesheet byte of its quad, which on real data is also the
            // low byte of its Signature (160 355 of 160 355 quads in the audit) - a sprite whose low byte has bit 3 clear has
            // no field, and the file of a sprite without a mode has no key.
            var spriteFiles = Directory.GetFiles(Path.Combine(outputDirectory, "Entities"), "sprite_*.sprite", SearchOption.AllDirectories);
            Assert.NotEmpty(spriteFiles);
            foreach (var file in spriteFiles)
            {
                var signature = long.Parse(Path.GetFileNameWithoutExtension(file)["sprite_".Length..]);
                var spritesheetByte = (int)(signature & 0xFF);
                var document = JObject.Parse(File.ReadAllText(file));

                if ((spritesheetByte & 0x08) == 0)
                {
                    Assert.False(document.ContainsKey(PsxKey), $"{file}: a sprite without bit 3 has no field.");
                }
                else
                {
                    var expected = Enum.Parse<SpritePsxSemiTransparency>($"Mode{(spritesheetByte >> 4) & 3}");
                    Assert.Equal(expected.ToString(), document[PsxKey]?.Value<string>());
                }
            }
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void AssertMode(string outputDirectory, string entityFolder, long signature, SpritePsxSemiTransparency expected)
    {
        var path = Path.Combine(outputDirectory, "Entities", entityFolder, $"sprite_{signature}.sprite");
        Assert.True(File.Exists(path), $"'{path}' was not written.");

        var sprite = new SpriteData();
        sprite.Load(JObject.Parse(File.ReadAllText(path)));
        Assert.True(expected == sprite.PsxSemiTransparency,
            $"sprite_{signature}: expected {expected}, read {sprite.PsxSemiTransparency}.");
    }

    // ---- helpers ----

    private static string FindEntityAsset(string outputDirectory, string fileName)
    {
        var entitiesDirectory = Path.Combine(outputDirectory, "Entities");
        Assert.True(Directory.Exists(entitiesDirectory), $"'{entitiesDirectory}' was not created.");

        var matches = Directory.GetFiles(entitiesDirectory, fileName, SearchOption.AllDirectories);
        Assert.True(matches.Length == 1, $"Expected exactly one '{fileName}' under Entities/, found {matches.Length}.");
        return matches[0];
    }

    private static string? FindRealDataFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "data", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
