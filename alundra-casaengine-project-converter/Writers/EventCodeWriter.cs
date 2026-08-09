using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 6, second half (plan item 5): the map event bytecode, written as a raw companion next to
/// the world it belongs to.
///
/// Mapping decisions:
///  - Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json: the map's own folder, next to its
///    tilemap/, dialogues/ and .world. <see cref="MapLocation"/> owns that layout.
///  - The file is a plain JSON companion, not a CasaEngine asset, and is therefore NOT registered
///    in the asset catalog: there is no engine type that could load it. It is data for the future
///    gameplay DLL's interpreter, exactly like Dialogues/*.strings.json is data for a future
///    dialogue system.
///  - Nothing is interpreted. Alundra's event programs A-F are a bytecode whose opcodes are not
///    decoded yet; the six tables and the Codes blob are copied with their source field names and
///    source order. Imposing structure now would bake a guess into the converted project, and the
///    raw form is losslessly re-derivable.
///  - SpriteInfo.Header's EventCodes pointer/size fields travel with the tables (see
///    EventCodeHeader): the tables hold offsets relative to the blob's PSX load address, so
///    without EventCodeAddress and the per-program pointer/size pairs the offsets cannot be
///    resolved back to indices in Codes.
///  - Codes entries are bytes in the source data (observed range 0-255 over all 483 maps) but are
///    written as JSON numbers in an array, not as a base64 blob, so the file stays greppable and
///    diffable while the bytecode is being reverse engineered. The Events.CodeWords counter
///    therefore counts one entry per raw code byte.
/// </summary>
public static class EventCodeWriter
{
    // No naming policy: the point of this file is that the field names are the source's own.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static void ConvertEvents(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, mapLocations, report);
        }
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var nativeMapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");
        if (!File.Exists(nativeMapPath))
        {
            report.Warnings.Add($"map_{mapIndex}: native map file not found at '{nativeMapPath}'.");
            return;
        }

        EventCodeDocument eventCodes;
        try
        {
            eventCodes = EventCodeReader.Read(nativeMapPath, mapIndex);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read event codes - {exception.Message}");
            return;
        }

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var targetPath = Path.Combine(outputDirectory, location.EventsRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        File.WriteAllText(targetPath, JsonSerializer.Serialize(eventCodes, SerializerOptions));

        report.Increment("Events.Maps");
        report.Increment("Events.CodeWords", eventCodes.Codes.Length);
    }
}
