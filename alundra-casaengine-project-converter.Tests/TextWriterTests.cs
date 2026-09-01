using System.Globalization;
using System.Text;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using Xunit;
using TextWriter = AlundraCasaEngineProjectConverter.Writers.TextWriter;

namespace AlundraCasaEngineProjectConverter.Tests;

public class TextWriterTests
{
    private static readonly IReadOnlyDictionary<int, MapLocation> MapLocations = new Dictionary<int, MapLocation>
    {
        [0] = new("Inoa", "Inoa (inner)-0"),
        [4] = new("Inoa", "Inoa (outer)-4"),
    };

    [Fact]
    public void ConvertText_GlobalTableKeepsPaddingNullsAndNumericKeyOrder()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteTextFixture(inputDirectory);

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(4, report.Counters["Text.GlobalStrings"]);
            Assert.Equal(2, report.Counters["Text.GlobalStringsEmpty"]);

            var path = Path.Combine(outputDirectory, "Dialogues", "global-strings.json");
            var raw = File.ReadAllText(path, Encoding.UTF8);

            // Sorted by the NUMERIC value of the key: a string sort would put "10240" first.
            Assert.True(raw.IndexOf("\"2049\"", StringComparison.Ordinal) < raw.IndexOf("\"10240\"", StringComparison.Ordinal));

            // Accented French stays literal UTF-8 in the file, not re-escaped to \uXXXX.
            Assert.Contains("Un Nouveau Départ", raw, StringComparison.Ordinal);

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            // The trailing padding is part of the fixed-width record and is not trimmed.
            Assert.Equal("Un Nouveau Départ              ", root.GetProperty("2049").GetString());

            // A null slot exists in the source, so it exists in the output.
            Assert.Equal(JsonValueKind.Null, root.GetProperty("2048").ValueKind);

            // Control codes are carried through verbatim.
            Assert.Equal("\\C#Le Livre d'Elna\\N", root.GetProperty("10240").GetString());

            Assert.Contains(report.Messages, message => message.Contains("Un Nouveau Départ", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertText_WritesOneStringTablePerMapInsideThatMapsOwnFolder()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteTextFixture(inputDirectory);

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Text.MapStringTables"]);
            Assert.Equal(5, report.Counters["Text.MapStrings"]);

            // Inside the map's own folder under Maps/, next to its tilemap/, events/ and .world.
            var path = Path.Combine(
                outputDirectory, "Maps", "Inoa", "Inoa (inner)-0", "dialogues", "Inoa (inner)-0.strings.json");
            Assert.True(File.Exists(path));

            using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var rows = document.RootElement.EnumerateArray().ToArray();

            // An array, so a line's index - the identity the scripts use - cannot drift.
            Assert.Equal(3, rows.Length);
            Assert.Equal("\\C#Disuse", rows[0].GetString());
            Assert.Equal("Bonjour }Yric !\\N", rows[1].GetString());
            Assert.Equal(JsonValueKind.Null, rows[2].ValueKind);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertText_InventoriesControlCodesFromBothSources()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteTextFixture(inputDirectory);

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Dialogues", "control-codes.json"), Encoding.UTF8));

            var rows = document.RootElement.EnumerateArray()
                .ToDictionary(row => row.GetProperty("code").GetString()!, row => row);

            // \C appears once in the global table and twice in the map tables: both are scanned.
            Assert.Equal(3, rows["\\C"].GetProperty("count").GetInt32());
            Assert.Equal(3, rows["\\N"].GetProperty("count").GetInt32());

            // '{' and '}' tokens are two characters, exactly as the game's reader consumes them.
            Assert.Equal(1, rows["}Y"].GetProperty("count").GetInt32());
            Assert.Equal(1, rows["{L"].GetProperty("count").GetInt32());

            // Every example is a whole source string containing the code.
            Assert.Contains("}Y", rows["}Y"].GetProperty("example").GetString()!, StringComparison.Ordinal);

            Assert.Equal(rows.Count, report.Counters["Text.ControlCodesDistinct"]);
            Assert.Equal(4, rows.Count);

            // Codes are sorted so the file is stable between runs.
            var codes = document.RootElement.EnumerateArray()
                .Select(row => row.GetProperty("code").GetString()!)
                .ToArray();
            Assert.Equal(codes.OrderBy(code => code, StringComparer.Ordinal).ToArray(), codes);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertText_WarnsWhenTheGlobalTableLostItsAccents()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteTextFixture(inputDirectory);

            // Same table, read back through a byte-mangling round trip: this is what a wrong
            // encoding anywhere in the chain looks like.
            File.WriteAllText(
                Path.Combine(inputDirectory, "data", "ETC_RES.R.json"),
                """
                {
                  "2049": "Un Nouveau Depart",
                  "2048": null
                }
                """,
                Encoding.UTF8);

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            Assert.Contains(report.Warnings, warning => warning.Contains("accented", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertText_HonoursTheMapFilter()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteTextFixture(inputDirectory);

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, new[] { 4 }, MapLocations, report);

            Assert.Equal(1, report.Counters["Text.MapStringTables"]);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Maps", "Inoa", "Inoa (outer)-4", "dialogues", "Inoa (outer)-4.strings.json")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "Maps", "Inoa", "Inoa (inner)-0", "dialogues", "Inoa (inner)-0.strings.json")));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertText_WritesEtcIndexAndItsChoiceLabelsResolveToOuiNon()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            var tiledDirectory = Path.Combine(dataDirectory, "tiled");
            Directory.CreateDirectory(tiledDirectory);

            // docs/plan-e12-dialogues.md, D-E12-6: EtcIndexTable.csv (the real, shipped table) has
            // index 0x43 (67) -> offset 3656 and index 0x44 (68) -> offset 3660. This fixture's
            // global-strings source supplies exactly those two real offsets with the two labels
            // 0x44's OUI/NON choice actually shows in game, so the test proves the whole chain -
            // etc-index.json's raw values resolving, through global-strings.json, to real strings -
            // not just that some string exists at some offset.
            File.WriteAllText(
                Path.Combine(dataDirectory, "ETC_RES.R.json"),
                """
                {
                  "3656": "OUI",
                  "3660": "NON"
                }
                """,
                Encoding.UTF8);
            WriteMapFixture(dataDirectory, tiledDirectory, 0, """[ "line" ]""");

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1024, report.Counters["Text.EtcIndexEntries"]);

            var etcIndexPath = Path.Combine(outputDirectory, "Dialogues", "etc-index.json");
            Assert.True(File.Exists(etcIndexPath));

            using var etcIndexDocument = JsonDocument.Parse(File.ReadAllText(etcIndexPath, Encoding.UTF8));
            var etcIndex = etcIndexDocument.RootElement.EnumerateArray().Select(e => e.GetInt32()).ToArray();

            // Flat array of 1024 raw values - the structure IS the identity: etc-index[id] is the
            // offset GetEtcString(id) looks up.
            Assert.Equal(1024, etcIndex.Length);

            var yesOffset = etcIndex[0x43];
            var noOffset = etcIndex[0x44];

            using var globalStringsDocument = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Dialogues", "global-strings.json"), Encoding.UTF8));
            var globalStrings = globalStringsDocument.RootElement;

            // The offsets etc-index.json names for 0x43/0x44 must be keys ACTUALLY PRESENT in
            // global-strings.json, resolving to the exact OUI/NON strings the game shows.
            Assert.True(globalStrings.TryGetProperty(yesOffset.ToString(CultureInfo.InvariantCulture), out var yesElement));
            Assert.Equal("OUI", yesElement.GetString());
            Assert.True(globalStrings.TryGetProperty(noOffset.ToString(CultureInfo.InvariantCulture), out var noElement));
            Assert.Equal("NON", noElement.GetString());
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteTextFixture(string inputDirectory)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        var tiledDirectory = Path.Combine(dataDirectory, "tiled");
        Directory.CreateDirectory(tiledDirectory);

        // Keys deliberately out of numeric order in the source, with the padded records and the
        // null slots the real ETC_RES.R.json has.
        File.WriteAllText(
            Path.Combine(dataDirectory, "ETC_RES.R.json"),
            """
            {
              "10240": "\\C#Le Livre d'Elna\\N",
              "2048": null,
              "2049": "Un Nouveau Départ              ",
              "2082": "   "
            }
            """,
            Encoding.UTF8);

        WriteMapFixture(dataDirectory, tiledDirectory, 0, """
            [ "\\C#Disuse", "Bonjour }Yric !\\N", null ]
            """);
        WriteMapFixture(dataDirectory, tiledDirectory, 4, """
            [ "{Lune\\N", "\\C#Fuite" ]
            """);
    }

    [Fact]
    public void ConvertText_InventoriesAnIntroducerInTheFinalCharacterPosition()
    {
        // A '\' with nothing after it: the inventory exists to surface what is out there, so a
        // truncated code must not vanish just because it sits at the end of the string.
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            var tiledDirectory = Path.Combine(dataDirectory, "tiled");
            Directory.CreateDirectory(tiledDirectory);

            File.WriteAllText(
                Path.Combine(dataDirectory, "ETC_RES.R.json"), """{ "1": "fin de ligne\\" }""", Encoding.UTF8);
            WriteMapFixture(dataDirectory, tiledDirectory, 0, """[ "coupe {" ]""");

            var report = new ConversionReport();
            TextWriter.ConvertText(inputDirectory, outputDirectory, null, MapLocations, report);

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Dialogues", "control-codes.json"), Encoding.UTF8));

            var codes = document.RootElement.EnumerateArray()
                .Select(row => row.GetProperty("code").GetString()!)
                .ToArray();

            Assert.Contains("\\", codes);
            Assert.Contains("{", codes);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteMapFixture(string dataDirectory, string tiledDirectory, int mapIndex, string stringsJson)
    {
        File.WriteAllText(
            Path.Combine(dataDirectory, $"map_{mapIndex}.json"),
            $$"""
            { "Offset": 0, "Strings": {{stringsJson}} }
            """,
            Encoding.UTF8);

        // MapDiscovery lists maps from the Tiled export; its contents are irrelevant here.
        File.WriteAllText(Path.Combine(tiledDirectory, $"map_{mapIndex}.tmj"), "{}", Encoding.UTF8);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "alundra-text-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
