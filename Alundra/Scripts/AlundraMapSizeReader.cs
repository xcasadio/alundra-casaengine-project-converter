#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Alundra.Scripts;

/// <summary>
/// E16.c C1/C4 (docs/plan-e16-etat-partie.md, D-E16-32, F3): reads a map's size in tiles, <c>map_size</c>
/// (<c>w</c>, <c>h</c>) of its <c>tilemap/&lt;name&gt;.tileMap</c>, before the map's world is loaded - what the
/// save-game validation needs to bound <c>CameraTileX/Y</c> (C3).
///
/// <para>The path is derived from the world's <c>world-index.json</c> value exactly like
/// <see cref="MapEventProgramLoader"/> (<c>EventProgramDocument.cs:107-117</c>): the <c>.world</c>'s folder, its
/// <c>tilemap</c> sub-folder, the same file name. The caller passes a path read from <c>world-index.json</c>,
/// never one read from a save (SC6: a save only gives an integer key). All 483 maps of the export have their
/// <c>.tileMap</c> there, at 52 x 60 (F3).</para>
///
/// <para>Never throws: a missing file, an unreadable one (a folder, a denied access), invalid JSON, a missing
/// <c>map_size</c> or a <c>w</c>/<c>h</c> that is not a JSON integer fitting an <see cref="int"/> (a float, a
/// string, an overflow) all return <c>false</c> with the reason; the integers are read through
/// <see cref="JsonElement.TryGetInt32"/>, never a conversion that throws (C4, SC6). The width and height are
/// returned as read; the caller checks they are at least 1.</para>
/// </summary>
public static class AlundraMapSizeReader
{
    private const string TileMapFolderName = "tilemap";
    private const string TileMapExtension = ".tileMap";

    /// <summary>The <c>.tileMap</c> path of the world at <paramref name="worldRelativePath"/> (a
    /// <c>world-index.json</c> value) under <paramref name="projectPath"/>, or null when it has no folder.</summary>
    public static string? GetTileMapPath(string projectPath, string worldRelativePath)
    {
        var worldFullPath = Path.Combine(projectPath, worldRelativePath);
        var mapFolder = Path.GetDirectoryName(worldFullPath);
        if (string.IsNullOrEmpty(mapFolder))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(worldFullPath);
        return Path.Combine(mapFolder, TileMapFolderName, name + TileMapExtension);
    }

    /// <summary>
    /// Reads <c>map_size</c> of the world at <paramref name="worldRelativePath"/>. Returns <c>false</c>, without
    /// throwing, with <paramref name="error"/> naming the file and the reason, when the size cannot be read.
    /// </summary>
    public static bool TryRead(
        string projectPath,
        string worldRelativePath,
        out int width,
        out int height,
        out string error)
    {
        width = 0;
        height = 0;

        string? tileMapPath = null;
        try
        {
            tileMapPath = GetTileMapPath(projectPath, worldRelativePath);
            if (tileMapPath == null)
            {
                error = $"no map folder can be derived from '{worldRelativePath}'";
                return false;
            }

            if (!File.Exists(tileMapPath))
            {
                error = $"'{tileMapPath}' not found";
                return false;
            }

            using var stream = File.OpenRead(tileMapPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("map_size", out var mapSize)
                || mapSize.ValueKind != JsonValueKind.Object)
            {
                error = $"'{tileMapPath}' has no map_size object";
                return false;
            }

            if (!TryGetInt32(mapSize, "w", out var w) || !TryGetInt32(mapSize, "h", out var h))
            {
                error = $"'{tileMapPath}' has a map_size whose w or h is not an integer";
                return false;
            }

            width = w;
            height = h;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            // I/O, access and JSON errors alike (a folder in place of the file, a denied access, invalid JSON):
            // the caller turns them into a refusal (C4, SC6).
            width = 0;
            height = 0;
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"'{tileMapPath ?? worldRelativePath}' could not be read ({ex.GetType().Name}: {ex.Message})");
            return false;
        }
    }

    private static bool TryGetInt32(JsonElement mapSize, string name, out int value)
    {
        value = 0;
        return mapSize.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }
}
