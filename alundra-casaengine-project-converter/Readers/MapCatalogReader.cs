using System.Text;
using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Where one converted map's files should live: a zone subfolder under Maps/, and a file base
/// name (without extension) that already embeds the map id for uniqueness.
///
/// This record is the ONLY authority on a map's output layout. Every converted file belonging to
/// a map - tilemap assets, dialogue table, event bytecode, world - lives in one folder,
/// <c>Maps/{Zone}/{Name}-{id}/</c>, and the members below are the sole way to name it. No writer
/// may concatenate a map path of its own: six of them used to, and the copies were free to drift.
/// Change the layout here and the whole converter follows.
///
/// Layout produced (paths are relative to the generated project's root):
/// <code>
/// Maps/{Zone}/{Name}-{id}/
///     tilemap/    {Name}-{id}.tileMap, .tileset, .tmj, map_{id}_tileset.png, .texture
///     dialogues/  {Name}-{id}.strings.json
///     events/     {Name}-{id}.events.json
///     {Name}-{id}.world
/// </code>
/// The tilemap companions (.tileset, .texture and the tileset PNG) are not named here because the
/// engine's importer derives them itself from the destination .tmj's folder and base name; passing
/// it <see cref="TiledMapRelativePath"/> is what puts them all in <c>tilemap/</c>.
/// </summary>
public sealed record MapLocation(string ZoneFolder, string FileBaseName)
{
    /// <summary>Root of everything this map owns: <c>Maps/{Zone}/{Name}-{id}</c>.</summary>
    public string MapFolder => Path.Combine(MapsRootFolder, ZoneFolder, FileBaseName);

    public string TileMapDirectory => Path.Combine(MapFolder, "tilemap");

    public string DialoguesDirectory => Path.Combine(MapFolder, "dialogues");

    public string EventsDirectory => Path.Combine(MapFolder, "events");

    public string TileMapRelativePath => Path.Combine(TileMapDirectory, $"{FileBaseName}.tileMap");

    public string TiledMapRelativePath => Path.Combine(TileMapDirectory, $"{FileBaseName}.tmj");

    public string StringsRelativePath => Path.Combine(DialoguesDirectory, $"{FileBaseName}.strings.json");

    public string EventsRelativePath => Path.Combine(EventsDirectory, $"{FileBaseName}.events.json");

    public string WorldRelativePath => Path.Combine(MapFolder, $"{FileBaseName}.world");

    /// <summary>
    /// The single top-level content folder every map lives under. world-index.json sits directly in
    /// it, because it indexes worlds that now live below it.
    /// </summary>
    public const string MapsRootFolder = "Maps";
}

public sealed record MapCatalogReadResult(IReadOnlyDictionary<int, MapLocation> Locations, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads maps.json, a hand-curated file (bundled with the converter, not part of data-extracted)
/// that groups Alundra map ids into named zones for a friendlier Maps/ folder layout. Format:
/// <c>[ { "ZoneName": [ { "id": 1, "name": "Overworld 0,0" }, ... ] }, ... ]</c>.
/// </summary>
public static class MapCatalogReader
{
    public static MapCatalogReadResult Read(string mapsJsonPath)
    {
        var locations = new Dictionary<int, MapLocation>();
        var warnings = new List<string>();

        using var stream = File.OpenRead(mapsJsonPath);
        using var document = JsonDocument.Parse(stream);

        foreach (var zoneEntry in document.RootElement.EnumerateArray())
        {
            foreach (var zoneProperty in zoneEntry.EnumerateObject())
            {
                var zoneFolder = SanitizeFileNamePart(zoneProperty.Name);

                foreach (var mapElement in zoneProperty.Value.EnumerateArray())
                {
                    var id = mapElement.GetProperty("id").GetInt32();
                    var name = mapElement.GetProperty("name").GetString() ?? $"map_{id}";
                    var fileBaseName = $"{SanitizeFileNamePart(name)}-{id}";

                    if (locations.TryGetValue(id, out var existing))
                    {
                        warnings.Add(
                            $"maps.json: id {id} is listed in both '{existing.ZoneFolder}' and '{zoneFolder}'; keeping '{existing.ZoneFolder}'.");
                        continue;
                    }

                    locations[id] = new MapLocation(zoneFolder, fileBaseName);
                }
            }
        }

        return new MapCatalogReadResult(locations, warnings);
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Array.IndexOf(invalidChars, character) >= 0 ? '_' : character);
        }

        return builder.ToString().Trim(' ', '.');
    }
}
