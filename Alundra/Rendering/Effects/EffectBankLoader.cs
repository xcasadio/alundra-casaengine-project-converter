#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using Alundra.Rendering.Backdrops;

namespace Alundra.Rendering.Effects;

/// <summary>
/// Loads the effect data of a world (see <see cref="EffectBank"/>, G3A-R1). The map companion is found like the backdrop one, through
/// <c>Maps/world-index.json</c> and the trailing "-{mapId}" of the world name (<see cref="BackdropLoader"/>), at
/// <c>effects/{worldName}.effects.json</c> in the map's folder; the global companion (<c>Data/effects/effects-global.json</c>) is read once per game (static cache, like
/// <c>EtcStringTable.EnsureLoaded</c>). A map without companion has no effect and no warning; an unreadable companion has one warning and no effect.
/// </summary>
public static class EffectBankLoader
{
    private static readonly ConcurrentDictionary<string, EffectDocument?> GlobalCache = new();

    public static EffectBank Load(string? projectPath, string? worldName)
    {
        if (string.IsNullOrEmpty(projectPath) || worldName == null || !BackdropLoader.TryParseMapIndex(worldName, out var mapIndex))
        {
            return EffectBank.Empty;
        }

        return new EffectBank(LoadMap(projectPath, worldName, mapIndex), LoadGlobal(projectPath));
    }

    /// <summary>Forgets the cached global companion (tests).</summary>
    public static void ResetForTests() => GlobalCache.Clear();

    private static EffectDocument? LoadMap(string projectPath, string worldName, int mapIndex)
    {
        string? path = null;
        try
        {
            var worldIndexPath = Path.Combine(projectPath, "Maps", "world-index.json");
            if (!File.Exists(worldIndexPath))
            {
                return null;
            }

            var index = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(worldIndexPath));
            if (index == null || !index.TryGetValue(mapIndex.ToString(), out var worldRelativePath))
            {
                return null;
            }

            var mapFolder = Path.GetDirectoryName(Path.Combine(projectPath, worldRelativePath));
            if (mapFolder == null)
            {
                return null;
            }

            path = Path.Combine(mapFolder, "effects", $"{worldName}.effects.json");
            return File.Exists(path) ? Read(path) : null;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"EffectBankLoader: failed to load the effect companion of world '{worldName}'{(path == null ? string.Empty : $" ('{path}')")} ({ex.Message}); no effect in this world.");
            return null;
        }
    }

    private static EffectDocument? LoadGlobal(string projectPath)
    {
        return GlobalCache.GetOrAdd(projectPath, key =>
        {
            var path = Path.Combine(key, "Data", "effects", "effects-global.json");
            try
            {
                return File.Exists(path) ? Read(path) : null;
            }
            catch (Exception ex)
            {
                Logs.WriteWarning($"EffectBankLoader: failed to load the global effect companion '{path}' ({ex.Message}); no global effect.");
                return null;
            }
        });
    }

    private static EffectDocument Read(string path)
    {
        var document = JsonSerializer.Deserialize<EffectDocument>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"'{path}' parsed to nothing");
        document.Normalize();
        return document;
    }
}
