using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// One row of the global string table (data/ETC_RES.R.json). Key is the source key verbatim - a
/// byte offset inside ETC_RES.R written as a decimal string, which is the handle the game itself
/// uses to address a string, so it is preserved as text rather than renumbered.
///
/// Value is nullable on purpose: 562 of the 916 rows are JSON null. A null is data, not a missing
/// row - the slot exists in the file and is empty - so it is carried through instead of dropped.
/// </summary>
public sealed record GlobalStringEntry(string Key, int? Offset, string? Value);

/// <summary>
/// Reads Alundra's two string sources, without touching the strings themselves:
///  - data/ETC_RES.R.json, an object of 916 (key -> string|null) rows: menu/item/chapter texts;
///  - data/map_N.json "Strings", an array of exactly 128 strings per map: the map's dialogue lines.
///
/// Values are returned exactly as they are in the source, including the trailing space padding
/// (ETC_RES rows are fixed-width records: "Un Nouveau Depart              ") and the raw control
/// codes. Trimming would silently change record widths the game's own code may rely on, and
/// rewriting control codes needs a mapping table that does not exist yet (see
/// Dialogues/control-codes.json, which this data feeds).
///
/// The extractor already decoded the text to Unicode, so reading is plain UTF-8 JSON; no CP850
/// conversion happens here (that lives in FontWriter, for the font atlas only).
/// </summary>
public static class StringTableReader
{
    /// <summary>
    /// Rows of ETC_RES.R.json, sorted by the numeric value of their key so "2049" precedes "10240"
    /// (a plain string sort would not) and the output is byte-stable between runs. Keys that are
    /// not numeric - none in the current data - keep their original text and sort last.
    /// </summary>
    public static IReadOnlyList<GlobalStringEntry> ReadGlobalTable(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        var entries = new List<GlobalStringEntry>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var offset = int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;

            entries.Add(new GlobalStringEntry(
                property.Name,
                offset,
                property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString()));
        }

        return entries
            .OrderBy(entry => entry.Offset ?? int.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The "Strings" array of one native map dump, in source order: the index is the identity the
    /// map's scripts use, so nothing is sorted or compacted here.
    /// </summary>
    public static IReadOnlyList<string?> ReadMapStrings(string mapJsonPath)
    {
        using var stream = File.OpenRead(mapJsonPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("Strings", out var stringsElement)
            || stringsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string?>();
        }

        var strings = new List<string?>(stringsElement.GetArrayLength());
        foreach (var element in stringsElement.EnumerateArray())
        {
            strings.Add(element.ValueKind == JsonValueKind.Null ? null : element.GetString());
        }

        return strings;
    }

    /// <summary>
    /// True when the text contains at least one character outside ASCII, i.e. the accented French
    /// survived the extraction and this converter's own reading as real Unicode.
    /// </summary>
    public static bool ContainsNonAscii(string value)
    {
        foreach (var character in value)
        {
            if (character > 0x7F)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the text contains U+FFFD, the replacement character an encoding mismatch leaves
    /// behind. Its presence means the accented text was mangled somewhere upstream.
    /// </summary>
    public static bool ContainsReplacementCharacter(string value)
        => value.Contains('�', StringComparison.Ordinal);

    /// <summary>
    /// A single-line, length-capped rendering of a string, for use in report warnings where the
    /// full padded record would be noise.
    /// </summary>
    public static string Excerpt(string value, int maxLength = 60)
    {
        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value.Trim())
        {
            if (builder.Length >= maxLength)
            {
                builder.Append('…');
                break;
            }

            builder.Append(character is '\r' or '\n' ? ' ' : character);
        }

        return builder.ToString();
    }
}
