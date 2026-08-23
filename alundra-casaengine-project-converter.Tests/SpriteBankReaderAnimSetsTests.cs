using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers SpriteBankReader.AnimSetHeaders on real data-extracted content (not a synthetic fixture):
/// docs/plan-conversion-totale.md E2 requires the hero's walk speed to come from the original
/// AnimSets[].Speed rather than be invented, so this asserts the reader actually surfaces it and
/// keeps assumptions honest against what the extractor really wrote.
/// Skips (rather than fails) when data-extracted/ is not present next to the built test binaries -
/// same convention as FontWriterTests.FindRealFile.
/// </summary>
public class SpriteBankReaderAnimSetsTests
{
    [Fact]
    public void ReadAllBanks_OnTheRealHeroBank_ExposesAnim1AndAnim54HeaderFields()
    {
        var mapAlundraPath = FindRealDataFile("map_alundra.json");
        if (mapAlundraPath is null)
        {
            return; // data-extracted/ not present in this environment; nothing to assert against.
        }

        var inputDirectory = CreateTempDirectory();
        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.Copy(mapAlundraPath, Path.Combine(dataDirectory, "map_alundra.json"));

            var report = new ConversionReport();
            var banks = SpriteBankReader.ReadAllBanks(inputDirectory, report);

            var heroBank = Assert.Single(banks, bank => bank.IsAlundraBank && bank.Sector5Id == 0);
            Assert.True(heroBank.AnimSetHeaders.Count > 54, "hero bank should declare at least 55 anim sets");

            // anim 54: read from the real data, not guessed - Speed 0, Acceleration 64.
            var anim54 = heroBank.AnimSetHeaders[54];
            Assert.Equal(0, anim54.Speed);
            Assert.Equal(64, anim54.Acceleration);
            // IsZForceApplied must agree with (Flags << 8) | Unknown, whether read straight from the
            // JSON or recomputed - see SpriteBankReader.ReadAnimSetHeader.
            Assert.Equal((short)((anim54.Flags << 8) | anim54.Unknown), anim54.IsZForceApplied);

            // anim 1 (Moving): whatever the data says, asserted rather than assumed.
            var anim1 = heroBank.AnimSetHeaders[1];
            Assert.Equal(208, anim1.Speed);
            Assert.Equal(1, anim1.Acceleration);
            Assert.Equal((short)((anim1.Flags << 8) | anim1.Unknown), anim1.IsZForceApplied);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ReadAllBanks_OnARealMapBank_ExposesItsAnimSetHeaderFields()
    {
        var map389Path = FindRealDataFile("map_389.json");
        if (map389Path is null)
        {
            return; // data-extracted/ not present in this environment; nothing to assert against.
        }

        var inputDirectory = CreateTempDirectory();
        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.Copy(map389Path, Path.Combine(dataDirectory, "map_389.json"));

            var report = new ConversionReport();
            var banks = SpriteBankReader.ReadAllBanks(inputDirectory, report);

            var mapBank = Assert.Single(banks, bank => !bank.IsAlundraBank && bank.Sector5Id == 146);
            Assert.True(mapBank.AnimSetHeaders.Count > 0);

            var anim0 = mapBank.AnimSetHeaders[0];
            Assert.Equal(0, anim0.Speed);
            Assert.Equal(0, anim0.Acceleration);
            Assert.Equal((short)((anim0.Flags << 8) | anim0.Unknown), anim0.IsZForceApplied);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
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
