using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Builds the raw PSX tile id -&gt; Tiled gid mapping the analyser's TiledMapExporter baked into a
/// map's exported tileset, by reading it back from the tileset it actually produced rather than
/// re-deriving it. TiledMapExporter.CreateTilesetJson (analyser sources,
/// AlundraTools/AlundraDataExtractor/TiledMapExporter.cs:119-166) writes each local tile's original
/// raw id as a "TileId" custom property (int) on that tile, and every tileset entry's gid is always
/// its local id + firstgid (TiledTilesetLayoutEntry construction, TiledMapExporter.cs:1004,1038:
/// `index + 1` / `localTileId + 1`, with firstgid itself fixed at 1 - TiledMapExporter.cs:258).
/// Reading these back is layout-mode-agnostic (works for both "original" and "compact"
/// TilesetLayoutMode) and avoids re-implementing gid assignment, which also depends on synthetic
/// animation-frame tile ids that never appear as raw MapTiles ids and are irrelevant to placement
/// replay.
/// </summary>
public static class TileSetGidMapReader
{
    public static (IReadOnlyDictionary<int, int> GidByRawTileId, int FirstGid) Read(string tilesetFilePath, string mapFilePath)
    {
        var firstGid = ReadFirstGid(mapFilePath);
        var gidByRawTileId = new Dictionary<int, int>();

        using var stream = File.OpenRead(tilesetFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);

        if (!jsonDocument.RootElement.TryGetProperty("tiles", out var tilesElement))
        {
            return (gidByRawTileId, firstGid);
        }

        foreach (var tileElement in tilesElement.EnumerateArray())
        {
            var localTileId = tileElement.GetProperty("id").GetInt32();

            if (!tileElement.TryGetProperty("properties", out var propertiesElement))
            {
                continue;
            }

            foreach (var propertyElement in propertiesElement.EnumerateArray())
            {
                if (propertyElement.GetProperty("name").GetString() != "TileId")
                {
                    continue;
                }

                var rawTileId = propertyElement.GetProperty("value").GetInt32();
                gidByRawTileId[rawTileId] = firstGid + localTileId;
                break;
            }
        }

        return (gidByRawTileId, firstGid);
    }

    private static int ReadFirstGid(string mapFilePath)
    {
        using var stream = File.OpenRead(mapFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);

        foreach (var tilesetElement in jsonDocument.RootElement.GetProperty("tilesets").EnumerateArray())
        {
            return tilesetElement.GetProperty("firstgid").GetInt32();
        }

        return 1;
    }
}
