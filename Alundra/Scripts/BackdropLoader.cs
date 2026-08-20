#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// Resolves and loads the current world's scrolling-background companion file (see
/// <see cref="BackdropDocument"/>'s class doc and <c>docs/formats/backdrops.md</c>).
///
/// Path resolution mirrors <see cref="MapEventProgramLoader"/> exactly (same
/// <c>Maps/world-index.json</c> lookup keyed off the trailing "-{mapId}" of <c>World.Name</c>), just
/// with the events-file leaf swapped for <c>backdrop/{Name}-{id}.backdrop.json</c> - see
/// <see cref="MapEventProgramLoader"/>'s class doc for why the world's own name is enough to find the
/// map's folder without separately knowing its zone.
///
/// Degraded mode (mirrors <see cref="MapEventProgramLoader"/>/<see cref="SpriteRecordCatalog"/>): a
/// world name with no trailing map id, a missing/unparseable world-index.json, an id absent from it,
/// or a missing/corrupt backdrop file all log exactly one warning and return null - callers treat
/// that as "this world has no scrolling background to draw" (most maps: <c>ScrollParameters.Infos
/// .Enabled</c> was false, so the converter never wrote a companion file at all - the common,
/// unremarkable case, not itself worth a warning; only I/O once a companion file was expected but the
/// path resolution fails logs one).
/// </summary>
public static class BackdropLoader
{
    private const string MapsRootFolder = "Maps";
    private const string WorldIndexFileName = "world-index.json";
    private const string BackdropFolder = "backdrop";

    private static readonly Regex TrailingMapIdPattern = new(@"-(\d+)$", RegexOptions.Compiled);

    public static BackdropDocument? Load(string projectPath, string worldName)
    {
        if (!TryParseMapIndex(worldName, out var mapIndex))
        {
            // No trailing map id: not a converted Alundra map world - nothing to warn about.
            return null;
        }

        var worldIndexPath = Path.Combine(projectPath, MapsRootFolder, WorldIndexFileName);

        try
        {
            if (!File.Exists(worldIndexPath))
            {
                Logs.WriteWarning(
                    $"BackdropLoader: '{worldIndexPath}' not found; scrolling background disabled "
                    + "for this world (degraded mode).");
                return null;
            }

            var indexJson = File.ReadAllText(worldIndexPath);
            var index = JsonSerializer.Deserialize<Dictionary<string, string>>(indexJson);

            if (index == null || !index.TryGetValue(mapIndex.ToString(), out var worldRelativePath))
            {
                Logs.WriteWarning(
                    $"BackdropLoader: map id {mapIndex} not found in '{worldIndexPath}'; scrolling "
                    + "background disabled (degraded mode).");
                return null;
            }

            var worldFullPath = Path.Combine(projectPath, worldRelativePath);
            var mapFolder = Path.GetDirectoryName(worldFullPath);
            if (mapFolder == null)
            {
                Logs.WriteWarning(
                    $"BackdropLoader: could not resolve a folder from '{worldFullPath}'; scrolling "
                    + "background disabled (degraded mode).");
                return null;
            }

            var backdropPath = Path.Combine(mapFolder, BackdropFolder, $"{worldName}.backdrop.json");

            if (!File.Exists(backdropPath))
            {
                // The common case: most maps have no scrolling background at all (ScrollParameters
                // .Infos.Enabled was false), so BackdropWriter never wrote a companion file for them.
                // Not worth a warning.
                return null;
            }

            var document = JsonSerializer.Deserialize<BackdropDocument>(
                File.ReadAllText(backdropPath), SerializerOptions);

            if (document == null)
            {
                Logs.WriteWarning(
                    $"BackdropLoader: '{backdropPath}' parsed to nothing; scrolling background "
                    + "disabled (degraded mode).");
                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"BackdropLoader: failed to load scrolling background for world '{worldName}' "
                + $"({ex.Message}); scrolling background disabled (degraded mode).");
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

    // No naming policy: field names match BackdropWriter/BackdropReader's own JSON contract exactly.
    private static readonly JsonSerializerOptions SerializerOptions = new();
}
