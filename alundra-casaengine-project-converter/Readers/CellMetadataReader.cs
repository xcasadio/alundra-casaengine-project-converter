using System.Globalization;
using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Per-cell gameplay metadata for one map, in CellOrder (index = y * width + x), plus sparse wall
/// tile stacks. Columnar (one array per field) rather than one object per cell, to keep the
/// serialized size down once this is embedded as a single TileMapData custom property.
/// </summary>
public sealed class CellMetadataDocument
{
    public int MapIndex { get; set; }
    public int CellCount { get; set; }
    public int[] Walkability { get; set; } = Array.Empty<int>();
    public int[] GroundProperty { get; set; } = Array.Empty<int>();
    public int[] Slope { get; set; } = Array.Empty<int>();
    public int[] Height { get; set; } = Array.Empty<int>();
    public int[] TileId { get; set; } = Array.Empty<int>();
    public int[] Palette { get; set; } = Array.Empty<int>();
    public int[] Tile { get; set; } = Array.Empty<int>();
    public int[] Flags { get; set; } = Array.Empty<int>();
    public int[] WallTilesOffset { get; set; } = Array.Empty<int>();

    // Sparse: only cells with an actual wall tile stack, keyed by cell index (as a string, since
    // JSON object keys must be strings).
    public Dictionary<string, WallTileStack> WallTiles { get; set; } = new();
}

public sealed class WallTileStack
{
    public int Offset { get; set; }
    public int[] Tiles { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Reads Alundra's per-cell gameplay data: Walkability/GroundProperty/Slope/Height/Flags/TileId/
/// Palette/Tile come from the Tiled companion (data/tiled/map_N.alundra.json); the actual wall
/// tile stack contents (only their offset pointer is in the companion) come from the native map
/// dump (data/map_N.json, Map.MapTiles[i].WallTiles). Cell order is index-aligned between the two
/// files (verified against real extracted data before writing this).
/// </summary>
public static class CellMetadataReader
{
    public static CellMetadataDocument Read(string companionFilePath, string nativeMapFilePath, int mapIndex)
    {
        var document = ReadCompanion(companionFilePath, mapIndex);
        MergeWallTiles(document, nativeMapFilePath);
        return document;
    }

    /// <summary>
    /// The map's grid size (in cells), read from the companion's own "Width"/"Height" root
    /// properties - the same dimensions <see cref="Read"/>'s per-cell arrays are laid out against
    /// (index = y * width + x). Used by the wall placement replay, which needs to iterate the grid
    /// the same way the exporter did.
    /// </summary>
    public static (int Width, int Height) ReadMapSize(string companionFilePath)
    {
        using var stream = File.OpenRead(companionFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);
        var width = jsonDocument.RootElement.GetProperty("Width").GetInt32();
        var height = jsonDocument.RootElement.GetProperty("Height").GetInt32();
        return (width, height);
    }

    private static CellMetadataDocument ReadCompanion(string companionFilePath, int mapIndex)
    {
        using var stream = File.OpenRead(companionFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);
        var cellsElement = jsonDocument.RootElement.GetProperty("Cells");
        var cellCount = cellsElement.GetArrayLength();

        var document = new CellMetadataDocument
        {
            MapIndex = mapIndex,
            CellCount = cellCount,
            Walkability = new int[cellCount],
            GroundProperty = new int[cellCount],
            Slope = new int[cellCount],
            Height = new int[cellCount],
            TileId = new int[cellCount],
            Palette = new int[cellCount],
            Tile = new int[cellCount],
            Flags = new int[cellCount],
            WallTilesOffset = new int[cellCount],
        };

        var index = 0;
        foreach (var cellElement in cellsElement.EnumerateArray())
        {
            document.Walkability[index] = cellElement.GetProperty("Walkability").GetInt32();
            document.GroundProperty[index] = cellElement.GetProperty("GroundProperty").GetInt32();
            document.Slope[index] = cellElement.GetProperty("Slope").GetInt32();
            document.Height[index] = cellElement.GetProperty("Height").GetInt32();
            document.TileId[index] = cellElement.GetProperty("TileId").GetInt32();
            document.Palette[index] = cellElement.GetProperty("Palette").GetInt32();
            document.Tile[index] = cellElement.GetProperty("Tile").GetInt32();
            document.Flags[index] = cellElement.GetProperty("Flags").GetInt32();
            document.WallTilesOffset[index] = cellElement.GetProperty("WallTilesOffset").GetInt32();
            index++;
        }

        return document;
    }

    private static void MergeWallTiles(CellMetadataDocument document, string nativeMapFilePath)
    {
        using var stream = File.OpenRead(nativeMapFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);
        var mapTilesElement = jsonDocument.RootElement.GetProperty("Map").GetProperty("MapTiles");

        var index = 0;
        foreach (var tileElement in mapTilesElement.EnumerateArray())
        {
            if (index >= document.CellCount)
            {
                break;
            }

            if (tileElement.TryGetProperty("WallTiles", out var wallTilesElement)
                && wallTilesElement.ValueKind == JsonValueKind.Object)
            {
                var tilesElement = wallTilesElement.GetProperty("Tiles");
                var tiles = new int[tilesElement.GetArrayLength()];
                var tileIndex = 0;
                foreach (var tileIdElement in tilesElement.EnumerateArray())
                {
                    tiles[tileIndex++] = tileIdElement.GetInt32();
                }

                document.WallTiles[index.ToString(CultureInfo.InvariantCulture)] = new WallTileStack
                {
                    Offset = wallTilesElement.GetProperty("Offset").GetInt32(),
                    Tiles = tiles,
                };
            }

            index++;
        }
    }
}
