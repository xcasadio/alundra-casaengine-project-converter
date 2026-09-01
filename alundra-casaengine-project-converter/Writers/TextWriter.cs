using System.Text.Encodings.Web;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 5, first half: the string tables. Alundra's text is already decoded to Unicode French by
/// the extractor, but its control codes are raw bytes of a script language whose meaning is only
/// partly known, so this writer moves the text without reinterpreting it.
///
/// Mapping decisions:
///  - Dialogues/global-strings.json is a JSON object mirroring data/ETC_RES.R.json: same keys (the
///    decimal byte offset the game addresses a string by), same values. Nothing is trimmed: the
///    trailing space padding is part of the fixed-width record, and the 562 null rows are kept
///    because an empty slot is data (the slot exists) rather than an absent one. Rows are ordered
///    by the numeric value of their key, so "2049" precedes "10240" and two runs produce
///    byte-identical files.
///  - Per map, Maps/{Zone}/{Name}-{id}/dialogues/{Name}-{id}.strings.json is a plain JSON array of
///    the map's 128 strings. An array, not an object, because the identity of a line is its position
///    in the array - that is the index the map's scripts use - and an array cannot drift from it.
///    The file sits inside the map's own folder, next to its tilemap/, events/ and .world;
///    <see cref="MapLocation"/> owns that layout. Dialogues/ itself keeps only the two tables that
///    belong to no map: global-strings.json and control-codes.json.
///  - Real dialogue graphs (Yarn or otherwise) are not produced: which line is spoken when lives in
///    the map bytecode, which is not decoded yet. A table is what the source is, so a table is what
///    is written.
///  - Dialogues/control-codes.json is an inventory, not a translation: every two-character token
///    starting with '\', '{' or '}' is counted, with one example string, so the mapping table can be
///    built later from real data. The tokenisation follows the game's own reader
///    (AlundraEngine.Text.TextDecoder.TextInterpreter): it dispatches on the single character after
///    '\', and '{'/'}' always consume exactly one following character. Codes that take an argument
///    (\W2, \C#) therefore appear as their two-character head with the argument left inside the
///    example - deliberately, since grouping arguments would need to assume which codes take one.
///    Nothing is filtered out: an unknown-looking escape is exactly what this file exists to
///    surface, so over-reporting beats a clever filter that hides something.
///  - The inventory is a file rather than report.json counters because it is bulky (tens of
///    thousands of occurrences over 27 distinct codes in the full data); report.json only gets the
///    distinct count.
/// </summary>
public static class TextWriter
{
    private const string DialoguesRelativeDirectory = "Dialogues";

    // UnsafeRelaxedJsonEscaping so accented French stays literal UTF-8 in the output instead of
    // being re-escaped to \uXXXX: these files are meant to be read and diffed by hand. It changes
    // nothing for a JSON parser, and backslashes (every control code starts with one) are still
    // escaped as JSON requires.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public static void ConvertText(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var inventory = new ControlCodeInventory();

        ConvertGlobalTable(inputDirectory, outputDirectory, inventory, report);
        ConvertMapTables(inputDirectory, outputDirectory, mapFilter, mapLocations, inventory, report);
        WriteControlCodes(outputDirectory, inventory, report);
        WriteEtcIndex(outputDirectory, report);
    }

    private static void ConvertGlobalTable(
        string inputDirectory,
        string outputDirectory,
        ControlCodeInventory inventory,
        ConversionReport report)
    {
        var sourcePath = Path.Combine(inputDirectory, "data", "ETC_RES.R.json");
        if (!File.Exists(sourcePath))
        {
            report.Warnings.Add($"Text: '{sourcePath}' not found; global string table skipped.");
            return;
        }

        IReadOnlyList<GlobalStringEntry> entries;
        try
        {
            entries = StringTableReader.ReadGlobalTable(sourcePath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Text: failed to read '{sourcePath}' - {exception.Message}");
            return;
        }

        var targetDirectory = Path.Combine(outputDirectory, DialoguesRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        using (var stream = File.Create(Path.Combine(targetDirectory, "global-strings.json")))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var entry in entries)
            {
                if (entry.Value is null)
                {
                    writer.WriteNull(entry.Key);
                }
                else
                {
                    writer.WriteString(entry.Key, entry.Value);
                }
            }

            writer.WriteEndObject();
        }

        var emptyCount = 0;
        string? accentedSample = null;
        var mangledCount = 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                emptyCount++;
                continue;
            }

            inventory.Scan(entry.Value);

            if (StringTableReader.ContainsReplacementCharacter(entry.Value))
            {
                mangledCount++;
            }
            else if (accentedSample is null && StringTableReader.ContainsNonAscii(entry.Value))
            {
                accentedSample = entry.Value;
            }
        }

        report.Increment("Text.GlobalStrings", entries.Count);
        report.Increment("Text.GlobalStringsEmpty", emptyCount);

        // Assert-by-warning that the accented French survived as UTF-8 end to end: the source is
        // known to contain rows such as "Un Nouveau Départ", so a table with no non-ASCII character
        // at all, or one carrying U+FFFD, means an encoding was lost somewhere.
        if (accentedSample is null)
        {
            report.Warnings.Add(
                "Text: no accented character found in the global string table; the source contains "
                + "rows such as \"Un Nouveau Départ\", so the text was probably read with the wrong encoding.");
        }
        else
        {
            report.Messages.Add(
                $"Text: accented French read as UTF-8, e.g. \"{StringTableReader.Excerpt(accentedSample)}\".");
        }

        if (mangledCount > 0)
        {
            report.Warnings.Add(
                $"Text: {mangledCount} global string(s) contain U+FFFD (replacement character); "
                + "their original characters were already lost upstream.");
        }
    }

    private static void ConvertMapTables(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ControlCodeInventory inventory,
        ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMapTable(inputDirectory, outputDirectory, mapIndex, mapLocations, inventory, report);
        }
    }

    private static void ConvertMapTable(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ControlCodeInventory inventory,
        ConversionReport report)
    {
        var mapJsonPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
        if (!File.Exists(mapJsonPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{mapJsonPath}'.");
            return;
        }

        IReadOnlyList<string?> strings;
        try
        {
            strings = StringTableReader.ReadMapStrings(mapJsonPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read strings - {exception.Message}");
            return;
        }

        if (strings.Count == 0)
        {
            report.Warnings.Add($"map_{mapIndex}: no 'Strings' array in '{mapJsonPath}'.");
            return;
        }

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var targetPath = Path.Combine(outputDirectory, location.StringsRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        using (var stream = File.Create(targetPath))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartArray();
            foreach (var value in strings)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(value);
                }
            }

            writer.WriteEndArray();
        }

        foreach (var value in strings)
        {
            if (!string.IsNullOrEmpty(value))
            {
                inventory.Scan(value);
            }
        }

        report.Increment("Text.MapStringTables");
        report.Increment("Text.MapStrings", strings.Count);
    }

    private static void WriteControlCodes(string outputDirectory, ControlCodeInventory inventory, ConversionReport report)
    {
        var targetDirectory = Path.Combine(outputDirectory, DialoguesRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var rows = inventory.ToRows();
        File.WriteAllText(
            Path.Combine(targetDirectory, "control-codes.json"),
            JsonSerializer.Serialize(rows, SerializerOptions),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        report.Increment("Text.ControlCodesDistinct", rows.Count);
        report.Messages.Add(
            $"Text: {rows.Count} distinct control codes inventoried in Dialogues/control-codes.json; "
            + "they are kept raw in the string tables, no mapping table exists yet.");
    }

    // docs/plan-e12-dialogues.md, slice E12.b, D-E12-6: the ETC index table - GetEtcString(id) =
    // StringByIndex[IndexTable[id]], and IndexTable is the first 1024 int16 of the game's own
    // ETC_RES.R file, never derivable from decompiled code. EtcIndexTable.csv ships with the
    // converter (see alundra-casaengine-project-converter.csproj) exactly like FontCharWidths.csv
    // above; this just republishes the same 1024 raw values as flat JSON so the DLL can resolve, for
    // example, the OUI/NON labels at index 0x43/0x44 against Dialogues/global-strings.json.
    private static void WriteEtcIndex(string outputDirectory, ConversionReport report)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "EtcIndexTable.csv");
        if (!File.Exists(csvPath))
        {
            report.Errors.Add($"Text: EtcIndexTable.csv not found at '{csvPath}'; Dialogues/etc-index.json not written.");
            return;
        }

        var result = EtcIndexCatalogReader.Read(csvPath);
        foreach (var warning in result.Warnings)
        {
            report.Warnings.Add(warning);
        }

        if (result.ValueByIndex.Count != 1024)
        {
            report.Errors.Add(
                $"Text: EtcIndexTable.csv has {result.ValueByIndex.Count} entries, expected 1024; "
                + "Dialogues/etc-index.json not written.");
            return;
        }

        var targetDirectory = Path.Combine(outputDirectory, DialoguesRelativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        using (var stream = File.Create(Path.Combine(targetDirectory, "etc-index.json")))
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartArray();
            foreach (var value in result.ValueByIndex)
            {
                writer.WriteNumberValue(value);
            }

            writer.WriteEndArray();
        }

        report.Increment("Text.EtcIndexEntries", result.ValueByIndex.Count);
    }

    /// <summary>
    /// Counts the two-character control tokens found in the strings and remembers one example of
    /// each. Tokenisation mirrors the game's reader: '\', '{' and '}' each swallow exactly one
    /// following character.
    /// </summary>
    private sealed class ControlCodeInventory
    {
        private readonly Dictionary<string, ControlCodeRow> _rows = new(StringComparer.Ordinal);

        public void Scan(string value)
        {
            var index = 0;
            while (index < value.Length)
            {
                var character = value[index];
                if (character is not ('\\' or '{' or '}'))
                {
                    index++;
                    continue;
                }

                // An introducer in the very last position has no argument to swallow. It is still a
                // control code and still worth inventorying - this table exists to surface what is
                // out there, so a truncated one must not vanish just because it sits at the end.
                var code = index + 1 < value.Length ? value.Substring(index, 2) : character.ToString();
                if (_rows.TryGetValue(code, out var row))
                {
                    row.Count++;
                }
                else
                {
                    _rows[code] = new ControlCodeRow { Code = code, Count = 1, Example = value };
                }

                index += 2;
            }
        }

        public IReadOnlyList<ControlCodeRow> ToRows()
            => _rows.Values.OrderBy(row => row.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// One row of Dialogues/control-codes.json: the raw token, how many times it occurs across the
    /// converted strings, and one whole string containing it (whole, not excerpted, so the token's
    /// surroundings can be studied when guessing its meaning).
    /// </summary>
    private sealed class ControlCodeRow
    {
        public string Code { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Example { get; set; } = string.Empty;
    }
}
