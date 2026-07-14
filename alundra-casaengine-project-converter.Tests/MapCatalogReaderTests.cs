using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class MapCatalogReaderTests
{
    [Fact]
    public void Read_AlwaysSuffixesTheIdRegardlessOfCollisions()
    {
        var path = WriteFixture(
            """
            [
                {
                    "Overworld": [
                        { "id": 1, "name": "Overworld 0,0" },
                        { "id": 2, "name": "Overworld 0,1" }
                    ]
                },
                {
                    "Inoa": [
                        { "id": 10, "name": "Inoa (inner)" },
                        { "id": 11, "name": "Inoa (inner)" }
                    ]
                }
            ]
            """);

        try
        {
            var result = MapCatalogReader.Read(path);

            Assert.Equal("Overworld", result.Locations[1].ZoneFolder);
            Assert.Equal("Overworld 0,0-1", result.Locations[1].FileBaseName);
            Assert.Equal("Overworld 0,1-2", result.Locations[2].FileBaseName);

            // Same display name, same zone: both keep their own id suffix, no collision.
            Assert.Equal("Inoa (inner)-10", result.Locations[10].FileBaseName);
            Assert.Equal("Inoa (inner)-11", result.Locations[11].FileBaseName);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_SanitizesInvalidWindowsFileNameCharacters()
    {
        var path = WriteFixture(
            """
            [
                {
                    "Coal Mine / Olen's Dream": [
                        { "id": 5, "name": "Coal Mine (unused?)" }
                    ]
                }
            ]
            """);

        try
        {
            var result = MapCatalogReader.Read(path);

            Assert.DoesNotContain('/', result.Locations[5].ZoneFolder);
            Assert.DoesNotContain('?', result.Locations[5].FileBaseName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WithIdClaimedByTwoZones_KeepsFirstAndWarns()
    {
        var path = WriteFixture(
            """
            [
                {
                    "The Klark": [
                        { "id": 387, "name": "The Klark" }
                    ]
                },
                {
                    "Inoa": [
                        { "id": 387, "name": "Inoa (inner)" }
                    ]
                }
            ]
            """);

        try
        {
            var result = MapCatalogReader.Read(path);

            Assert.Equal("The Klark", result.Locations[387].ZoneFolder);
            Assert.Contains(result.Warnings, warning => warning.Contains("id 387", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteFixture(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"maps-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
