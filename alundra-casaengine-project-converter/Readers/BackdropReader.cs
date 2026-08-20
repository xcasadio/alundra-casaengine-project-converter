using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// One cell of a Cellular-mode (mode 2) scroll layer: a small independently-moving sprite (a bird,
/// a cloud, foam...) cut from the layer's tile sheet at (U0,V0)-(U1,V1). Field names and meaning
/// mirror AlundraEngine.DatasBin.Cell (alundra-datas-analyser/AlundraTools/AlundraEngine/DatasBin/
/// ScrollScreen.cs) exactly; Unused0/Unused1 are dropped. Rendering these cells (camera-relative
/// motion, periodic drift, the WaveX sine-driven track) is not implemented by this converter - see
/// <see cref="BackdropCellularDocument"/>.
/// </summary>
public sealed class BackdropCellDocument
{
    public int PalDex { get; set; }
    public int U0 { get; set; }
    public int V0 { get; set; }
    public int U1 { get; set; }
    public int V1 { get; set; }
    public int Type { get; set; }
    public int X0 { get; set; }
    public int Y0 { get; set; }
    public int CamXNum { get; set; }
    public int CamXDen { get; set; }
    public int CamYNum { get; set; }
    public int CamYDen { get; set; }
    public int DX { get; set; }
    public int PeriodX { get; set; }
    public int DY { get; set; }
    public int PeriodY { get; set; }
}

/// <summary>
/// A mode-1 ("Tiles") layer's parallax/auto-scroll parameters - AlundraEngine.DatasBin.ScrollScreen
/// (the "Scrollar" struct), copied field for field. GraphicManager.RenderLayerToBuffer (@
/// 0x8005B848) computes screen-space camera parallax as
/// <c>cameraX * FactorXNum / FactorXDenom</c> (and the Y equivalent), and advances an independent
/// auto-scroll offset by ScrollXSpeed every tick, stepping it by one extra pixel every
/// |ScrollXPeriod| ticks (direction from the sign of Speed XOR the sign of Period) - both offsets
/// wrap the tile grid, which is a fixed 640x480 canvas (40x30 16px tiles).
/// </summary>
public sealed class BackdropScrollarDocument
{
    public int FactorXNum { get; set; }
    public int FactorXDenom { get; set; }
    public int FactorYNum { get; set; }
    public int FactorYDenom { get; set; }
    public int ScrollXSpeed { get; set; }
    public int ScrollXPeriod { get; set; }
    public int ScrollYSpeed { get; set; }
    public int ScrollYPeriod { get; set; }
}

/// <summary>
/// A mode-2 ("Cellular") layer's shared wave-shimmer coefficients (AlundraEngine.DatasBin.Cellular)
/// plus its cells. AWave*/BWave* feed the WaveX cell type's sine-like displacement, which also
/// consumes the map-level <see cref="BackdropDocument.WaveLut"/> table
/// (GraphicManager.RenderLayerToBuffer's CellType.WaveX case, @ 0x8005B848). Exported as raw
/// parameters only - see the class doc on <see cref="BackdropDocument"/> for why rendering is
/// deferred.
/// </summary>
public sealed class BackdropCellularDocument
{
    public int CountBase { get; set; }
    public int AWaveY { get; set; }
    public int AWavePhase { get; set; }
    public int AWaveAmp { get; set; }
    public int BWaveY { get; set; }
    public int BWavePhase { get; set; }
    public int BWaveWeight { get; set; }
    public int Divisions { get; set; }
    public List<BackdropCellDocument> Cells { get; set; } = new();
}

/// <summary>
/// One of a map's (up to) two scrolling background layers.
///
/// <see cref="Mode"/> "Tiles" (LiningInfos.ModeLayer[LayerId] == 1) is the only mode this converter
/// pre-renders: <see cref="TextureAssetId"/> points at a <see cref="Width"/>x<see cref="Height"/>
/// PNG (always 640x480 - the layer's full 40x30 tile grid, the space it wraps within) baked from
/// the map's own tile sheet/palettes, registered like any other converter texture. "Cellular"
/// layers (mode 2) and "Disabled" layers (mode 0, or the layer slot unused) carry only their raw
/// parameters and no texture.
///
/// <see cref="DepthOrder"/> is GraphicManager.RenderLayerToBuffer's layerOrderOffset (1 for layer
/// 0, 0 for layer 1): within the same <see cref="Ground"/> bucket, the layer with the larger value
/// paints later - i.e. layer 0 sits above layer 1 whenever both are active and share a bucket.
/// </summary>
public sealed class BackdropLayerDocument
{
    public int LayerId { get; set; }
    public string Mode { get; set; } = "Disabled";
    public int DepthOrder { get; set; }
    public bool Ground { get; set; }
    public int BlendMode { get; set; }
    public int AnimTimer { get; set; }
    public BackdropScrollarDocument? Scrollar { get; set; }
    public BackdropCellularDocument? Cellular { get; set; }
    public string? TextureAssetId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// One map's scrolling background (the PSX parallax backdrop drawn by GraphicManager.
/// RenderAllTileLayers, @ 0x8005B670) - see AlundraEngine.DatasBin.ScrollParameters for the source
/// model this mirrors.
///
/// Draw order/depth (RenderLayerToBuffer, @ 0x8005B848): a layer's LayerInfos.Ground flag picks one
/// of two very different depth buckets, not merely a background/foreground toggle -
/// <c>Ground == 0</c> layers get depth <c>-0x10000000 + DepthOrder</c> (far below every floor/wall/
/// entity depth, i.e. true background, painted first); <c>Ground != 0</c> layers get depth
/// <c>SpriteDepth.BackgroundUI - 1000 + DepthOrder</c>, which - despite the "BackgroundUI" name -
/// is a huge value close to int.MaxValue, painted after every floor/wall/entity and only below the
/// actual UI: an overlay layer (observed on map 389's sea layer, Ground=1, BlendMode=Average),
/// not a backdrop. Both are exported identically; which bucket a layer belongs to is the consuming
/// renderer's job, driven by <see cref="BackdropLayerDocument.Ground"/>.
///
/// Deferred (raw parameters exported, rendering not implemented here):
///  - WaveX cell tracks (Cellulars + <see cref="WaveLut"/>): a per-tick sine-like displacement
///    table indexed by AWaveY/AWavePhase/AWaveAmp/BWaveY/BWavePhase/BWaveWeight.
///  - Cellular (mode 2) layers entirely: independently-moving sprite cells, not a tile grid.
///  - Per-map screen tint overlay (LiningInfos.BGColorR/G/B/A, RenderTileOverlayLayer @
///    0x8005BA40): a full-screen alpha-blended rectangle/gradient, unrelated to the tile layers.
///  - Tile animation (LayerInfos.AnimTimer + the per-tile AnimFrameCounter that shifts sampled V):
///    the exported texture is a single static frame (AnimFrameCounter == 0).
/// </summary>
public sealed class BackdropDocument
{
    public int MapIndex { get; set; }
    public bool Enabled { get; set; }
    public int AnimNum { get; set; }
    public int[]? WaveLut { get; set; }
    public List<BackdropLayerDocument> Layers { get; set; } = new();
}

/// <summary>
/// Everything <see cref="BackdropDocument"/> needs plus the raw bytes a mode-1 layer's tile grid is
/// baked from: the tile sheet (already unzipped 4bpp, 256x256) and its 8 palettes, and the layer's
/// slice of the Data blob's tile grid (see <see cref="BackdropReader.ExtractTileGrid"/>).
/// </summary>
public sealed class BackdropReadResult
{
    public required BackdropDocument Document { get; init; }
    public byte[] TileSheetImageData { get; init; } = Array.Empty<byte>();
    public ushort[][] PaletteWords { get; init; } = Array.Empty<ushort[]>();
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public bool HasGraphics { get; init; }
    public long Graphics { get; init; }
    public bool[] LayerIsTiles { get; init; } = new bool[2];
}

/// <summary>
/// Reads Map.ScrollParameters out of the native map dump (data/map_N.json). Unlike most readers
/// here, most of the source struct is already decoded by the analyser (LayerInfos, Scrollars,
/// Cellulars, Cells, PaletteWords, TileSheetImageData) and is copied straight across; only the tile
/// grid is not exposed as its own field and must still be sliced out of the raw Data blob (see
/// <see cref="ExtractTileGrid"/>), mirroring GraphicManager.RenderLayerToBuffer's baseMapOffset
/// computation (@ 0x8005B848).
/// </summary>
public static class BackdropReader
{
    public const int TileSize = 16;
    public const int GridWidthTiles = 0x28;
    public const int GridHeightTiles = 0x1E;
    public const int GridRowStride = 0x50;
    public const int TileGridBaseOffset = 0x8100;
    public const int SecondTileGridOffset = 0x960;
    public const int CanvasWidth = GridWidthTiles * TileSize;
    public const int CanvasHeight = GridHeightTiles * TileSize;

    public static BackdropReadResult Read(string nativeMapFilePath, int mapIndex)
    {
        using var stream = File.OpenRead(nativeMapFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);
        return Read(jsonDocument.RootElement, mapIndex);
    }

    public static BackdropReadResult Read(JsonElement mapRootElement, int mapIndex)
    {
        var document = new BackdropDocument { MapIndex = mapIndex };

        if (!mapRootElement.TryGetProperty("ScrollParameters", out var scrollElement)
            || scrollElement.ValueKind != JsonValueKind.Object)
        {
            return new BackdropReadResult { Document = document };
        }

        var enabled = GetInt(scrollElement, "Infos", "Enabled") != 0;
        document.Enabled = enabled;
        if (!enabled)
        {
            return new BackdropReadResult { Document = document };
        }

        document.AnimNum = GetInt(scrollElement, "Infos", "AnimNum");

        var hasGraphics = scrollElement.TryGetProperty("HasGraphics", out var hasGraphicsElement)
            && hasGraphicsElement.ValueKind == JsonValueKind.True;
        var graphics = GetLong(scrollElement, "Graphics");
        var data = ReadByteArray(scrollElement, "Data");
        var tileSheetImageData = ReadByteArray(scrollElement, "TileSheetImageData");
        var paletteWords = ReadPaletteWords(scrollElement);
        var waveLut = ReadIntArray(scrollElement, "WaveLut");
        document.WaveLut = waveLut.Length > 0 ? waveLut : null;

        var modeLayer = ReadModeLayer(scrollElement);
        var layerIsTiles = new bool[2];

        scrollElement.TryGetProperty("LayerInfos", out var layerInfosElement);
        scrollElement.TryGetProperty("Scrollars", out var scrollarsElement);
        scrollElement.TryGetProperty("Cellulars", out var cellularsElement);
        scrollElement.TryGetProperty("Cells", out var cellsElement);

        for (var layerId = 0; layerId < 2; layerId++)
        {
            var mode = modeLayer[layerId];
            var layer = new BackdropLayerDocument
            {
                LayerId = layerId,
                DepthOrder = layerId == 0 ? 1 : 0,
                Mode = mode switch { 1 => "Tiles", 2 => "Cellular", _ => "Disabled" },
            };

            if (TryGetIndex(layerInfosElement, layerId, out var layerInfoElement))
            {
                layer.AnimTimer = GetInt(layerInfoElement, "AnimTimer");
                layer.BlendMode = GetInt(layerInfoElement, "BlendMode");
                layer.Ground = GetInt(layerInfoElement, "Ground") != 0;
            }

            if (mode == 1 && hasGraphics)
            {
                layerIsTiles[layerId] = true;
                layer.Width = CanvasWidth;
                layer.Height = CanvasHeight;

                if (TryGetIndex(scrollarsElement, layerId, out var scrollarElement))
                {
                    layer.Scrollar = new BackdropScrollarDocument
                    {
                        FactorXNum = GetInt(scrollarElement, "FactorXNum"),
                        FactorXDenom = GetInt(scrollarElement, "FactorXDenom"),
                        FactorYNum = GetInt(scrollarElement, "FactorYNum"),
                        FactorYDenom = GetInt(scrollarElement, "FactorYDenom"),
                        ScrollXSpeed = GetInt(scrollarElement, "ScrollXSpeed"),
                        ScrollXPeriod = GetInt(scrollarElement, "ScrollXPeriod"),
                        ScrollYSpeed = GetInt(scrollarElement, "ScrollYSpeed"),
                        ScrollYPeriod = GetInt(scrollarElement, "ScrollYPeriod"),
                    };
                }
            }
            else if (mode == 2)
            {
                if (TryGetIndex(cellularsElement, layerId, out var cellularElement))
                {
                    var cellular = new BackdropCellularDocument
                    {
                        CountBase = GetInt(cellularElement, "CountBase"),
                        AWaveY = GetInt(cellularElement, "AWaveY"),
                        AWavePhase = GetInt(cellularElement, "AWavePhase"),
                        AWaveAmp = GetInt(cellularElement, "AWaveAmp"),
                        BWaveY = GetInt(cellularElement, "BWaveY"),
                        BWavePhase = GetInt(cellularElement, "BWavePhase"),
                        BWaveWeight = GetInt(cellularElement, "BWaveWeight"),
                        Divisions = GetInt(cellularElement, "Divisions"),
                    };

                    if (TryGetIndex(cellsElement, layerId, out var cellsForLayerElement)
                        && cellsForLayerElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cellElement in cellsForLayerElement.EnumerateArray())
                        {
                            cellular.Cells.Add(new BackdropCellDocument
                            {
                                PalDex = GetInt(cellElement, "PalDex"),
                                U0 = GetInt(cellElement, "U0"),
                                V0 = GetInt(cellElement, "V0"),
                                U1 = GetInt(cellElement, "U1"),
                                V1 = GetInt(cellElement, "V1"),
                                Type = GetInt(cellElement, "Type"),
                                X0 = GetInt(cellElement, "X0"),
                                Y0 = GetInt(cellElement, "Y0"),
                                CamXNum = GetInt(cellElement, "CamXNum"),
                                CamXDen = GetInt(cellElement, "CamXDen"),
                                CamYNum = GetInt(cellElement, "CamYNum"),
                                CamYDen = GetInt(cellElement, "CamYDen"),
                                DX = GetInt(cellElement, "DX"),
                                PeriodX = GetInt(cellElement, "PeriodX"),
                                DY = GetInt(cellElement, "DY"),
                                PeriodY = GetInt(cellElement, "PeriodY"),
                            });
                        }
                    }

                    layer.Cellular = cellular;
                }
            }

            document.Layers.Add(layer);
        }

        return new BackdropReadResult
        {
            Document = document,
            TileSheetImageData = tileSheetImageData,
            PaletteWords = paletteWords,
            Data = data,
            HasGraphics = hasGraphics,
            Graphics = graphics,
            LayerIsTiles = layerIsTiles,
        };
    }

    /// <summary>
    /// Slices layer <paramref name="layerId"/>'s 40x30 tile grid (2 bytes/tile: tile index, palette
    /// index) out of the Data blob, mirroring RenderLayerToBuffer's baseMapOffset - the grid lives
    /// at Graphics+0x8100, and layer 1 only gets its own second half (+0x960) when BOTH layers are
    /// in Tiles mode simultaneously; otherwise (a lone Tiles layer in slot 1) it reads the same
    /// first half layer 0 would have used. Returns null if the offset falls outside Data (matching
    /// the engine's own bounds check, which simply skips rendering that frame).
    /// </summary>
    public static byte[]? ExtractTileGrid(BackdropReadResult result, int layerId)
    {
        var secondScroll = layerId != 0 && result.LayerIsTiles[0] && result.LayerIsTiles[1];
        var baseOffset = result.Graphics + TileGridBaseOffset + (secondScroll ? SecondTileGridOffset : 0);
        const int gridSize = GridRowStride * GridHeightTiles;

        if (baseOffset < 0 || baseOffset + gridSize > result.Data.Length)
        {
            return null;
        }

        var grid = new byte[gridSize];
        Array.Copy(result.Data, baseOffset, grid, 0, gridSize);
        return grid;
    }

    private static int[] ReadModeLayer(JsonElement scrollElement)
    {
        var modeLayer = new int[2];
        if (scrollElement.TryGetProperty("Infos", out var infosElement)
            && infosElement.TryGetProperty("ModeLayer", out var modeLayerElement)
            && modeLayerElement.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var element in modeLayerElement.EnumerateArray())
            {
                if (index >= 2)
                {
                    break;
                }

                modeLayer[index++] = element.GetInt32();
            }
        }

        return modeLayer;
    }

    private static ushort[][] ReadPaletteWords(JsonElement scrollElement)
    {
        if (!scrollElement.TryGetProperty("PaletteWords", out var paletteWordsElement)
            || paletteWordsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ushort[]>();
        }

        var palettes = new List<ushort[]>();
        foreach (var paletteElement in paletteWordsElement.EnumerateArray())
        {
            var colors = new List<ushort>();
            foreach (var colorElement in paletteElement.EnumerateArray())
            {
                colors.Add((ushort)colorElement.GetInt32());
            }

            palettes.Add(colors.ToArray());
        }

        return palettes.ToArray();
    }

    private static byte[] ReadByteArray(JsonElement scrollElement, string propertyName)
    {
        if (!scrollElement.TryGetProperty(propertyName, out var arrayElement)
            || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[arrayElement.GetArrayLength()];
        var index = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            bytes[index++] = (byte)element.GetInt32();
        }

        return bytes;
    }

    private static int[] ReadIntArray(JsonElement scrollElement, string propertyName)
    {
        if (!scrollElement.TryGetProperty(propertyName, out var arrayElement)
            || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var values = new int[arrayElement.GetArrayLength()];
        var index = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            values[index++] = element.GetInt32();
        }

        return values;
    }

    private static bool TryGetIndex(JsonElement arrayElement, int index, out JsonElement result)
    {
        result = default;
        if (arrayElement.ValueKind != JsonValueKind.Array || index >= arrayElement.GetArrayLength())
        {
            return false;
        }

        var current = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            if (current == index)
            {
                result = element;
                return true;
            }

            current++;
        }

        return false;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static int GetInt(JsonElement element, string objectName, string propertyName)
    {
        return element.TryGetProperty(objectName, out var objectElement)
            ? GetInt(objectElement, propertyName)
            : 0;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;
    }
}
