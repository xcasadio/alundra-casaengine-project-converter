#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>One quad of an effect image set: a window of the effect sheet drawn on four free corners (docs/formats/effects.md).
/// Read by the draw (E19.g G3c), not by the reservoir.</summary>
public sealed class AlundraEffectImage
{
    public int U { get; set; }
    public int V { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    /// <summary>The four corners as eight signed bytes <c>X1, Y1, X2, Y2, X3, Y3, X4, Y4</c> (top-left, top-right, bottom-left, bottom-right; y down).</summary>
    public int[] C { get; set; } = Array.Empty<int>();

    public bool Semi { get; set; }
    public int Abr { get; set; }
}

/// <summary>An image set of an effect table: the depth bias of the sort key and its quads, in the order of the binary.</summary>
public sealed class AlundraEffectImageSet
{
    public int Idsv { get; set; }
    public List<AlundraEffectImage> Images { get; set; } = new();
}

/// <summary>An animation of an effect table: frames <c>[ticks, image set index]</c> and its end (<c>Destroy</c> or <c>Loop</c>).</summary>
public sealed class AlundraEffectAnimation
{
    public int[][] Frames { get; set; } = Array.Empty<int[]>();
    public string End { get; set; } = string.Empty;
}

/// <summary>One effect table (a sprite of the binary's effect bank): its animations and image sets.</summary>
public sealed class AlundraEffectTable
{
    public List<AlundraEffectAnimation> Animations { get; set; } = new();
    public List<AlundraEffectImageSet> ImageSets { get; set; } = new();
}

/// <summary>A map effect record: the tile zone (inclusive), the flags (<c>0x80</c> map table, <c>0x40</c> appears at load), the table index, the position in the binary's units and the animation.</summary>
public sealed class AlundraEffectRecord
{
    public int X1 { get; set; }
    public int X2 { get; set; }
    public int Y1 { get; set; }
    public int Y2 { get; set; }
    public int Flags { get; set; }
    public int Effect { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Anim { get; set; }
}

/// <summary>The effect companion of a map (<c>*.effects.json</c>) or the global one (<c>effects-global.json</c>, records absent), see <c>docs/formats/effects.md</c>.</summary>
public sealed class AlundraEffectDocument
{
    public int? MapIndex { get; set; }
    public string? SheetTextureAssetId { get; set; }
    public List<AlundraEffectRecord> Records { get; set; } = new();
    public List<AlundraEffectTable> Effects { get; set; } = new();

    /// <summary>A member written as <c>null</c> reads as an empty list.</summary>
    internal void Normalize()
    {
        Records ??= new();
        Effects ??= new();
        foreach (var table in Effects)
        {
            table.Animations ??= new();
            table.ImageSets ??= new();
            foreach (var animation in table.Animations)
            {
                animation.Frames ??= Array.Empty<int[]>();
                animation.End ??= string.Empty;
            }

            foreach (var set in table.ImageSets)
            {
                set.Images ??= new();
            }
        }
    }
}

/// <summary>
/// The effect data of a world (E19.g G3a, docs/plan-e19-opcodes.md section 1.2o.6): the records and the map table of the map companion, and the global tables
/// (a record's <c>0x80</c> flag selects the map table, otherwise the global one). Data only; the reservoir reads it.
/// </summary>
public sealed class AlundraEffectBank
{
    public static AlundraEffectBank Empty { get; } = new(null, null);

    private readonly AlundraEffectDocument? _map;
    private readonly AlundraEffectDocument? _global;

    public AlundraEffectBank(AlundraEffectDocument? map, AlundraEffectDocument? global)
    {
        _map = map;
        _global = global;
    }

    public AlundraEffectDocument? Map => _map;

    public AlundraEffectDocument? Global => _global;

    public IReadOnlyList<AlundraEffectRecord> Records => (IReadOnlyList<AlundraEffectRecord>?)_map?.Records ?? Array.Empty<AlundraEffectRecord>();

    /// <summary>The table <paramref name="effect"/> of the map table (<paramref name="mapTable"/>) or of the global one; false when there is none.</summary>
    public bool TryGetTable(bool mapTable, int effect, [NotNullWhen(true)] out AlundraEffectTable? table)
    {
        var tables = (mapTable ? _map : _global)?.Effects;
        if (tables != null && effect >= 0 && effect < tables.Count)
        {
            table = tables[effect];
            return true;
        }

        table = null;
        return false;
    }
}

/// <summary>
/// Loads the effect data of a world (see <see cref="AlundraEffectBank"/>, G3A-R1). The map companion is found like the backdrop one, through
/// <c>Maps/world-index.json</c> and the trailing "-{mapId}" of the world name (<see cref="BackdropLoader"/>), at
/// <c>effects/{worldName}.effects.json</c> in the map's folder; the global companion (<c>Data/effects/effects-global.json</c>) is read once per game (static cache, like
/// <c>AlundraEtcStringTable.EnsureLoaded</c>). A map without companion has no effect and no warning; an unreadable companion has one warning and no effect.
/// </summary>
public static class AlundraEffectBankLoader
{
    private static readonly ConcurrentDictionary<string, AlundraEffectDocument?> GlobalCache = new();

    public static AlundraEffectBank Load(string? projectPath, string? worldName)
    {
        if (string.IsNullOrEmpty(projectPath) || worldName == null || !BackdropLoader.TryParseMapIndex(worldName, out var mapIndex))
        {
            return AlundraEffectBank.Empty;
        }

        return new AlundraEffectBank(LoadMap(projectPath, worldName, mapIndex), LoadGlobal(projectPath));
    }

    /// <summary>Forgets the cached global companion (tests).</summary>
    public static void ResetForTests() => GlobalCache.Clear();

    private static AlundraEffectDocument? LoadMap(string projectPath, string worldName, int mapIndex)
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
            Logs.WriteWarning($"AlundraEffectBankLoader: failed to load the effect companion of world '{worldName}'{(path == null ? string.Empty : $" ('{path}')")} ({ex.Message}); no effect in this world.");
            return null;
        }
    }

    private static AlundraEffectDocument? LoadGlobal(string projectPath)
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
                Logs.WriteWarning($"AlundraEffectBankLoader: failed to load the global effect companion '{path}' ({ex.Message}); no global effect.");
                return null;
            }
        });
    }

    private static AlundraEffectDocument Read(string path)
    {
        var document = JsonSerializer.Deserialize<AlundraEffectDocument>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"'{path}' parsed to nothing");
        document.Normalize();
        return document;
    }
}
