using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class EventCodeWriterTests
{
    [Fact]
    public void Read_PreservesTheSixProgramTablesTheRawCodesAndTheHeaderPointers()
    {
        var inputDirectory = CreateTempDirectory();

        try
        {
            WriteEventFixture(inputDirectory, mapIndex: 4);

            var document = EventCodeReader.Read(Path.Combine(inputDirectory, "data", "map_4.json"), mapIndex: 4);

            Assert.Equal(4, document.MapIndex);
            Assert.Equal(new[] { 0, 132 }, document.EventCodesATable);
            Assert.Equal(new[] { 0, 300, 400 }, document.EventCodesBTable);
            Assert.Equal(new[] { 0 }, document.EventCodesCTable);
            Assert.Equal(new[] { -32767 }, document.EventCodesDTable);
            Assert.Equal(new[] { 7 }, document.EventCodesETable);
            Assert.Equal(new[] { 11, 12 }, document.EventCodesFTable);

            // The blob is copied verbatim, order included: it is addressed by offset.
            Assert.Equal(new[] { 1, 2, 3, 255, 0, 128 }, document.Codes);

            // Without these the offsets held by the tables cannot be resolved back into Codes.
            Assert.Equal(1519304, document.Header.EventCodeAddress);
            Assert.Equal(1244, document.Header.EventCodesAPointer);
            Assert.Equal(36, document.Header.EventCodesASize);
            Assert.Equal(1280, document.Header.EventCodesBPointer);
            Assert.Equal(16, document.Header.EventCodesBSize);
            Assert.Equal(1296, document.Header.EventCodesCPointer);
            Assert.Equal(38, document.Header.EventCodesCSize);
            Assert.Equal(1334, document.Header.EventCodesDPointer);
            Assert.Equal(2, document.Header.EventCodesDSize);
            Assert.Equal(1336, document.Header.EventCodesEPointer);
            Assert.Equal(2, document.Header.EventCodesESize);
            Assert.Equal(1338, document.Header.EventCodesFPointer);
            Assert.Equal(38, document.Header.EventCodesFSize);
            Assert.Equal(1614, document.Header.EventCodesFAndRemainingSize);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Read_ForAMapWithoutEventCodes_ReturnsEmptyTablesRatherThanThrowing()
    {
        var inputDirectory = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(inputDirectory, "data"));
            File.WriteAllText(Path.Combine(inputDirectory, "data", "map_0.json"), """{ "SpriteInfo": {} }""");

            var document = EventCodeReader.Read(Path.Combine(inputDirectory, "data", "map_0.json"), mapIndex: 0);

            Assert.Empty(document.Codes);
            Assert.Empty(document.EventCodesATable);
            Assert.Equal(0, document.Header.EventCodeAddress);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertEvents_WritesACompanionKeepingTheSourceFieldNames()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();

        try
        {
            WriteEventFixture(inputDirectory, mapIndex: 4);
            var mapLocations = new Dictionary<int, MapLocation> { [4] = new MapLocation("TestZone", "Test Map-4") };

            var report = new ConversionReport();
            EventCodeWriter.ConvertEvents(inputDirectory, outputDirectory, new[] { 4 }, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Events.Maps"]);
            Assert.Equal(6, report.Counters["Events.CodeWords"]);

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Test Map-4", "events", "Test Map-4.events.json");
            Assert.True(File.Exists(companionPath));

            var document = JObject.Parse(File.ReadAllText(companionPath));
            Assert.Equal(4, (int)document["MapIndex"]!);
            Assert.Equal(new[] { 0, 132 }, document["EventCodesATable"]!.Values<int>());
            Assert.Equal(new[] { 11, 12 }, document["EventCodesFTable"]!.Values<int>());
            Assert.Equal(new[] { 1, 2, 3, 255, 0, 128 }, document["Codes"]!.Values<int>());
            Assert.Equal(1519304, (int)document["Header"]!["EventCodeAddress"]!);
            Assert.Equal(1614, (int)document["Header"]!["EventCodesFAndRemainingSize"]!);
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteEventFixture(string inputDirectory, int mapIndex)
    {
        var dataDirectory = Path.Combine(inputDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        File.WriteAllText(
            Path.Combine(dataDirectory, $"map_{mapIndex}.json"),
            """
            {
                "SpriteInfo": {
                    "Header": {
                        "EventCodeAddress": 1519304,
                        "EventCodesAPointer": 1244, "EventCodesASize": 36,
                        "EventCodesBPointer": 1280, "EventCodesBSize": 16,
                        "EventCodesCPointer": 1296, "EventCodesCSize": 38,
                        "EventCodesDPointer": 1334, "EventCodesDSize": 2,
                        "EventCodesEPointer": 1336, "EventCodesESize": 2,
                        "EventCodesFPointer": 1338, "EventCodesFSize": 38,
                        "EventCodesFAndRemainingSize": 1614,
                        "SpriteTablePointer": 12, "SpriteTableSize": 34
                    },
                    "EventCodes": {
                        "EventCodesATable": [0, 132],
                        "EventCodesBTable": [0, 300, 400],
                        "EventCodesCTable": [0],
                        "EventCodesDTable": [-32767],
                        "EventCodesETable": [7],
                        "EventCodesFTable": [11, 12],
                        "Codes": [1, 2, 3, 255, 0, 128]
                    }
                }
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
