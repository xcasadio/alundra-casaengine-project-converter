using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class EntityNameCatalogReaderTests
{
    // jp;fr;en, one header row, French column taken. Row 0 is the hero, rows 0x100+ are the
    // per-map banks - the two pages the game itself indexes (see EntityNameCatalogReader).
    private static string WriteCsv(params (int Index, string French)[] entries)
    {
        var rows = new string[513];
        rows[0] = "text_jp;text_fr;text_en";
        for (var index = 0; index < 512; index++)
        {
            rows[index + 1] = "(空き);(vide);(empty)";
        }

        foreach (var (index, french) in entries)
        {
            rows[index + 1] = $"jp;{french};en";
        }

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, string.Join('\n', rows), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static SpriteBank Bank(int sector5Id, bool isHero = false)
        => new() { Sector5Id = sector5Id, IsHeroBank = isHero };

    [Fact]
    public void Read_NamesTheHeroBankFromThePageZeroRow()
    {
        var csvPath = WriteCsv((0, "Alundra"), (0x100, "Beaumont (chef du village)"));
        try
        {
            var result = EntityNameCatalogReader.Read(csvPath, new[] { Bank(0, isHero: true), Bank(0) });

            // Hero bank 0 reads row 0; map bank 0 reads row 0x100 - the game adds that page offset
            // for sprites coming from the current map rather than the global Alundra table.
            Assert.Equal("Alundra", result.FolderNamesByBankKey["hero_0"]);
            Assert.Equal("Beaumont (chef du village)", result.FolderNamesByBankKey["0"]);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void Read_KeepsTheBankKeyWhenTheNameIsAPlaceholder()
    {
        var csvPath = WriteCsv((0x100, "Beaumont (chef du village)"));
        try
        {
            var result = EntityNameCatalogReader.Read(csvPath, new[] { Bank(0), Bank(5, isHero: true) });

            Assert.Equal("bank_hero_5", result.FolderNamesByBankKey["hero_5"]);
            Assert.Contains(result.Warnings, w => w.Contains("hero_5"));
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void Read_SuffixesEveryBankSharingAName()
    {
        // The real table reuses one name across several banks - five carry the projectile name -
        // so the suffix has to go on all of them, not just the ones that come second.
        var csvPath = WriteCsv((0x100 + 116, "Projectile Niv.1"), (0x100 + 155, "Projectile Niv.1"));
        try
        {
            var result = EntityNameCatalogReader.Read(csvPath, new[] { Bank(116), Bank(155) });

            Assert.Equal("Projectile Niv.1-116", result.FolderNamesByBankKey["116"]);
            Assert.Equal("Projectile Niv.1-155", result.FolderNamesByBankKey["155"]);
            Assert.Contains(result.Warnings, w => w.Contains("Projectile Niv.1"));
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void Read_ReplacesCharactersThatCannotBeInAPath()
    {
        // Real names carry these: "Bloc à pousser/attaquer", "Laser (?)".
        var csvPath = WriteCsv((0x100, "Bloc à pousser/attaquer"), (0x101, "Laser (?)"));
        try
        {
            var result = EntityNameCatalogReader.Read(csvPath, new[] { Bank(0), Bank(1) });

            Assert.Equal("Bloc à pousser_attaquer", result.FolderNamesByBankKey["0"]);
            Assert.Equal("Laser (_)", result.FolderNamesByBankKey["1"]);

            // Accents are kept - stripping them would mangle most of the French table.
            Assert.Contains('à', result.FolderNamesByBankKey["0"]);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }
}
