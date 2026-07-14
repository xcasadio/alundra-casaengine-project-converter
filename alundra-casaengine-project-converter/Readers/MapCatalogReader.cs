using System.Text;
using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Where one converted map's files should live: a zone subfolder under Maps/, and a file base
/// name (without extension) that already embeds the map id for uniqueness.
/// </summary>
public sealed record MapLocation(string ZoneFolder, string FileBaseName);

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
