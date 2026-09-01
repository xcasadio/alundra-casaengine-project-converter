#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// Resolves and loads the current world's own local dialogue-string table (docs/plan-e12-dialogues.md
/// §1.5): <c>dialogues/{Name}-{id}.strings.json</c>, a flat array of 128 positions indexed directly by
/// opcode 0x0D/0x5C's own <c>textId &amp; 0x7F</c> (see <see cref="AlundraEventProgramRunner"/>'s own
/// dispatch case 0x0D). Path resolution mirrors <see cref="MapEventProgramLoader"/> exactly (same
/// <c>Maps/world-index.json</c> lookup, same trailing "-{mapId}" world-name convention) - see that
/// class's own doc for why the world's own name is enough to find the right map folder without knowing
/// its zone.
///
/// Degraded mode (mirrors <see cref="MapEventProgramLoader"/>/<see cref="BackdropLoader"/>): a world name
/// with no trailing map id, a missing/unparseable world-index.json, an id absent from it, or a missing/
/// corrupt strings file all log exactly one warning and return null - <see cref="AlundraEventProgramRunner"/>
/// then resolves every local textId to null (its own degraded "text unavailable" fallback).
/// </summary>
public static class AlundraDialogueStringsLoader
{
    private const string MapsRootFolder = "Maps";
    private const string WorldIndexFileName = "world-index.json";

    public static IReadOnlyList<string>? Load(string projectPath, string worldName)
    {
        if (!MapEventProgramLoader.TryParseMapIndex(worldName, out var mapIndex))
        {
            Logs.WriteWarning(
                $"AlundraDialogueStringsLoader: world name '{worldName}' has no trailing '-<mapId>'; local "
                + "dialogue strings disabled for this world (degraded mode).");
            return null;
        }

        var worldIndexPath = Path.Combine(projectPath, MapsRootFolder, WorldIndexFileName);

        try
        {
            if (!File.Exists(worldIndexPath))
            {
                Logs.WriteWarning(
                    $"AlundraDialogueStringsLoader: '{worldIndexPath}' not found; local dialogue strings "
                    + "disabled (degraded mode).");
                return null;
            }

            var indexJson = File.ReadAllText(worldIndexPath);
            var index = JsonSerializer.Deserialize<Dictionary<string, string>>(indexJson);

            if (index == null || !index.TryGetValue(mapIndex.ToString(), out var worldRelativePath))
            {
                Logs.WriteWarning(
                    $"AlundraDialogueStringsLoader: map id {mapIndex} not found in '{worldIndexPath}'; "
                    + "local dialogue strings disabled (degraded mode).");
                return null;
            }

            var worldFullPath = Path.Combine(projectPath, worldRelativePath);
            var mapFolder = Path.GetDirectoryName(worldFullPath);
            if (mapFolder == null)
            {
                Logs.WriteWarning(
                    $"AlundraDialogueStringsLoader: could not resolve a folder from '{worldFullPath}'; "
                    + "local dialogue strings disabled (degraded mode).");
                return null;
            }

            var stringsPath = Path.Combine(mapFolder, "dialogues", $"{worldName}.strings.json");

            if (!File.Exists(stringsPath))
            {
                Logs.WriteWarning(
                    $"AlundraDialogueStringsLoader: '{stringsPath}' not found; local dialogue strings "
                    + "disabled (degraded mode).");
                return null;
            }

            var strings = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(stringsPath));
            if (strings == null)
            {
                Logs.WriteWarning(
                    $"AlundraDialogueStringsLoader: '{stringsPath}' parsed to nothing; local dialogue "
                    + "strings disabled (degraded mode).");
                return null;
            }

            return strings;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraDialogueStringsLoader: failed to load local dialogue strings for world "
                + $"'{worldName}' ({ex.Message}); disabled (degraded mode).");
            return null;
        }
    }
}
