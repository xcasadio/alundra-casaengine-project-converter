#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CasaEngine.Core.Logging;
using Alundra.Entities;

namespace Alundra.Events;

/// <summary>
/// Resolves and loads the current world's event-code document.
///
/// Path resolution: the world's own asset name (<c>World.Name</c>) is set by the converter to
/// <c>MapLocation.FileBaseName</c> ("{Name}-{id}", e.g. "Ship Klark (beginning)-389" - see
/// <c>WorldWriter.ConvertMap</c>: <c>Name = location.FileBaseName</c>), which already embeds the map
/// id as its trailing "-{id}". That id is looked up in <c>Maps/world-index.json</c> (a flat
/// <c>{mapId: "Maps\Zone\Name-id\Name-id.world"}</c> map the converter also writes -
/// <c>WorldWriter</c>'s <c>worldPathsByMapId</c>) to recover the map's own folder without having to
/// separately know or guess its zone - <see cref="MapLocation"/>'s own layout then gives the events
/// file a fixed name relative to that folder: <c>events/{Name}-{id}.events.json</c>.
///
/// Degraded mode (mirrors <see cref="SpriteRecordCatalog"/>'s class doc): a world name with no
/// trailing map id, a missing/unparseable world-index.json, an id absent from it, or a missing/corrupt
/// events file all log exactly one warning and return null - callers treat that as "the interpreter has
/// no program to run" (see <see cref="EventProgramRunner"/>'s own degraded-mode doc).
/// </summary>
public static class MapEventProgramLoader
{
    private const string MapsRootFolder = "Maps";
    private const string WorldIndexFileName = "world-index.json";

    private static readonly Regex TrailingMapIdPattern = new(@"-(\d+)$", RegexOptions.Compiled);

    public static EventProgramDocument? Load(string projectPath, string worldName)
    {
        if (!TryParseMapIndex(worldName, out var mapIndex))
        {
            Logs.WriteWarning(
                $"MapEventProgramLoader: world name '{worldName}' has no trailing '-<mapId>'; event "
                + "interpreter disabled for this world (degraded mode).");
            return null;
        }

        var worldIndexPath = Path.Combine(projectPath, MapsRootFolder, WorldIndexFileName);

        try
        {
            if (!File.Exists(worldIndexPath))
            {
                Logs.WriteWarning(
                    $"MapEventProgramLoader: '{worldIndexPath}' not found; event interpreter disabled "
                    + "(degraded mode).");
                return null;
            }

            var indexJson = File.ReadAllText(worldIndexPath);
            var index = JsonSerializer.Deserialize<Dictionary<string, string>>(indexJson);

            if (index == null || !index.TryGetValue(mapIndex.ToString(), out var worldRelativePath))
            {
                Logs.WriteWarning(
                    $"MapEventProgramLoader: map id {mapIndex} not found in '{worldIndexPath}'; event "
                    + "interpreter disabled (degraded mode).");
                return null;
            }

            var worldFullPath = Path.Combine(projectPath, worldRelativePath);
            var mapFolder = Path.GetDirectoryName(worldFullPath);
            if (mapFolder == null)
            {
                Logs.WriteWarning(
                    $"MapEventProgramLoader: could not resolve a folder from '{worldFullPath}'; event "
                    + "interpreter disabled (degraded mode).");
                return null;
            }

            var eventsPath = Path.Combine(mapFolder, "events", $"{worldName}.events.json");

            if (!File.Exists(eventsPath))
            {
                Logs.WriteWarning(
                    $"MapEventProgramLoader: '{eventsPath}' not found; event interpreter disabled "
                    + "(degraded mode).");
                return null;
            }

            var document = JsonSerializer.Deserialize<EventProgramDocument>(
                File.ReadAllText(eventsPath), SerializerOptions);

            if (document == null)
            {
                Logs.WriteWarning(
                    $"MapEventProgramLoader: '{eventsPath}' parsed to nothing; event interpreter "
                    + "disabled (degraded mode).");
                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"MapEventProgramLoader: failed to load event codes for world '{worldName}' "
                + $"({ex.Message}); event interpreter disabled (degraded mode).");
            return null;
        }
    }

    internal static bool TryParseMapIndex(string worldName, out int mapIndex)
    {
        var match = TrailingMapIdPattern.Match(worldName);
        if (match.Success && int.TryParse(match.Groups[1].Value, out mapIndex))
        {
            return true;
        }

        mapIndex = 0;
        return false;
    }

    // No naming policy: field names match EventCodeReader/EventCodeWriter's own JSON contract exactly.
    private static readonly JsonSerializerOptions SerializerOptions = new();
}
