using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Covers AnimationEndClassifier against real data-extracted content (not synthetic fixtures): the
/// "Alundra's animation is too fast" bug (docs/plan-conversion-totale.md, 2026-08-23 entry) traces
/// to every animation being exported as AnimationType.Loop regardless of what its trailing control
/// frame actually says - these tests pin the classifier's reading of that control frame against
/// three real examples covering all three outcomes.
/// Skips (rather than fails) when data-extracted/ is not present next to the built test binaries -
/// same convention as SpriteBankReaderAnimSetsTests.
/// </summary>
public class AnimationEndClassifierTests
{
    [Fact]
    public void Classify_HeroAnim0Down_IsLoop()
    {
        var mapAlundraPath = FindRealDataFile("map_alundra.json");
        if (mapAlundraPath is null)
        {
            return;
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

            var anim0Down = heroBank.AnimSets[0][0];
            Assert.NotNull(anim0Down);

            var result = AnimationEndClassifier.Classify(anim0Down!, report);

            Assert.Equal(AnimationEndKind.Loop, result.Kind);
            Assert.Equal(0, report.Counters.GetValueOrDefault("Sprites.AnimationsMissingTerminator"));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Classify_HeroAnim54Down_ChainsToAnim0()
    {
        // The reported bug: hero anim 54 (LoadingMap) plays once and hands off to anim 0 (Idle) via
        // its trailing control frame (Delay 0, TransformIndexLow 0). Exported as Loop, it used to
        // cycle every ~0.46s instead of playing once.
        var mapAlundraPath = FindRealDataFile("map_alundra.json");
        if (mapAlundraPath is null)
        {
            return;
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

            var anim54Down = heroBank.AnimSets[54][0];
            Assert.NotNull(anim54Down);

            var result = AnimationEndClassifier.Classify(anim54Down!, report);

            Assert.Equal(AnimationEndKind.Chain, result.Kind);
            Assert.Equal(0, result.ChainTargetAnimationId);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Classify_Map1Sector22Anim6Down_IsHold()
    {
        // A real Hold example found by scanning the corpus: control frame Delay 0,
        // TransformIndexLow 128 (0x80 set, no animation index bits) -> freeze on the last frame.
        var map1Path = FindRealDataFile("map_1.json");
        if (map1Path is null)
        {
            return;
        }

        var inputDirectory = CreateTempDirectory();
        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);
            File.Copy(map1Path, Path.Combine(dataDirectory, "map_1.json"));

            var report = new ConversionReport();
            var banks = SpriteBankReader.ReadAllBanks(inputDirectory, report);
            var bank = Assert.Single(banks, bank => !bank.IsAlundraBank && bank.Sector5Id == 22);

            var anim6Down = bank.AnimSets[6][0];
            Assert.NotNull(anim6Down);

            var result = AnimationEndClassifier.Classify(anim6Down!, report);

            Assert.Equal(AnimationEndKind.Hold, result.Kind);
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
