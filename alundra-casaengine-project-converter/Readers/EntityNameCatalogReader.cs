using System.Text;

namespace AlundraCasaEngineProjectConverter.Readers;

public sealed record EntityNameCatalogReadResult(
    IReadOnlyDictionary<string, string> FolderNamesByBankKey,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Resolves each sprite bank to the entity name the game itself uses, so converted animations land
/// under Entities/&lt;Name&gt;/ instead of an opaque bank number.
///
/// The table is EntityNames.csv, which ships with the analyser (the game engine loads the very same
/// file through AlundraEngine.EntityNames) and is linked into this converter's output rather than
/// copied, so the two cannot drift. Columns are jp;fr;en after one header row; the French column is
/// taken, matching every EntityNames.Load call in the analyser.
///
/// Indexing follows the game. A sprite table index names a row directly, and GameEngine.SpawnWarpEntity
/// / SpawnChildEntity add 0x100 before handing it to EntityNames.GetName when the sprite comes from
/// the current map rather than from the global Alundra table. So the table is two pages: rows 0-255
/// are the global bank (map_alundra.json - row 0 is the hero, "Alundra", then items and projectiles)
/// and rows 256-511 are the per-map banks (NPCs, monsters, scenery). A record's index in
/// SpriteInfo.SpriteRecords is its Sector5Id - verified on all 2809 records in the shipped data - so
/// the bank key is all that is needed to look a name up.
/// </summary>
public static class EntityNameCatalogReader
{
    private const int MapSpritePageOffset = 0x100;

    // The rows the game leaves unused; they are placeholders, not names.
    private static readonly string[] PlaceholderNames = { "(vide)", "(empty)", "(空き)" };

    public static EntityNameCatalogReadResult Read(string csvPath, IEnumerable<SpriteBank> banks)
    {
        var warnings = new List<string>();
        var names = ReadFrenchColumn(csvPath);

        // Resolve every bank first, then hand out folder names: a name shared by several banks has
        // to be disambiguated on all of them, not just the ones that happen to come second.
        var bankKeysByName = new Dictionary<string, List<SpriteBank>>(StringComparer.Ordinal);
        var unnamedBanks = new List<SpriteBank>();

        foreach (var bank in banks)
        {
            var nameIndex = bank.IsHeroBank ? bank.Sector5Id : MapSpritePageOffset + bank.Sector5Id;
            var name = nameIndex >= 0 && nameIndex < names.Count ? names[nameIndex] : null;

            if (string.IsNullOrWhiteSpace(name) || PlaceholderNames.Contains(name.Trim()))
            {
                unnamedBanks.Add(bank);
                continue;
            }

            var sanitized = SanitizeFileNamePart(name);
            if (sanitized.Length == 0)
            {
                unnamedBanks.Add(bank);
                continue;
            }

            if (!bankKeysByName.TryGetValue(sanitized, out var sharing))
            {
                sharing = new List<SpriteBank>();
                bankKeysByName[sanitized] = sharing;
            }

            sharing.Add(bank);
        }

        var folderNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, sharing) in bankKeysByName)
        {
            foreach (var bank in sharing)
            {
                // Same suffix shape as the Maps/ layout: "{Name}-{id}".
                folderNames[bank.BankKey] = sharing.Count == 1 ? name : $"{name}-{bank.BankKey}";
            }

            if (sharing.Count > 1)
            {
                warnings.Add(
                    $"EntityNames.csv: '{name}' names {sharing.Count} banks "
                    + $"({string.Join(", ", sharing.Select(bank => bank.BankKey))}); each keeps its bank key as a suffix.");
            }
        }

        foreach (var bank in unnamedBanks)
        {
            // No name to use, so the bank key stays the folder - still unique, still traceable.
            folderNames[bank.BankKey] = $"bank_{bank.BankKey}";
        }

        if (unnamedBanks.Count > 0)
        {
            warnings.Add(
                $"EntityNames.csv: {unnamedBanks.Count} bank(s) have no name and keep their bank key as a folder "
                + $"({string.Join(", ", unnamedBanks.Select(bank => bank.BankKey))}).");
        }

        return new EntityNameCatalogReadResult(folderNames, warnings);
    }

    private static IReadOnlyList<string> ReadFrenchColumn(string csvPath)
    {
        // UTF-8 with a BOM, as the analyser reads it.
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        var names = new List<string>(lines.Length);

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var columns = lines[lineIndex].Split(';');
            names.Add(columns.Length > 1 ? columns[1].Trim() : string.Empty);
        }

        return names;
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Array.IndexOf(invalidChars, character) >= 0 ? '_' : character);
        }

        // Windows silently drops a trailing dot or space from a directory name, which would make
        // the written path and the recorded asset path disagree.
        return builder.ToString().Trim(' ', '.');
    }
}
