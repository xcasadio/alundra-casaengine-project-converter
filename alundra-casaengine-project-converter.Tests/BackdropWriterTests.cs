using System.Drawing;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class BackdropWriterTests
{
    // Layer 0's tile grid puts one non-empty tile at grid cell (0,0): tile index 0x11 selects
    // sheet column (0x11 & 0x0F) << 4 = 16, row 0x11 & 0xF0 = 16 - a 16x16 block at sheet (16,16).
    // Every pixel in that block is packed nibble 2, which PaletteWords[0][2] resolves to a fully
    // opaque bright green (see BuildTileSheet/BuildPaletteWords below) - everywhere else in the
    // tile sheet is nibble 0, i.e. transparent (PaletteWords[0][0] == 0).
    private const int TileIndex = 0x11;
    private const int SheetU = 16;
    private const int SheetV = 16;
    private const int PaletteEntry = 2;
    private const ushort GreenPsxWord = 0x03E0; // FromPsxColor -> (0, 248, 0)

    [Fact]
    public void Build_BakesTheTileGridIntoADeterministic640x480Texture()
    {
        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();
        var tileGrid = BuildTileGrid();

        using var first = BackdropImageBuilder.Build(tileGrid, tileSheet, paletteWords);
        using var second = BackdropImageBuilder.Build(tileGrid, tileSheet, paletteWords);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(640, first!.Width);
        Assert.Equal(480, first.Height);

        // Same input -> same bytes: pixel-for-pixel identical across two independent builds.
        for (var y = 0; y < first.Height; y += 37)
        {
            for (var x = 0; x < first.Width; x += 41)
            {
                Assert.Equal(first.GetPixel(x, y), second!.GetPixel(x, y));
            }
        }

        // The 16x16 block at grid cell (0,0) is opaque bright green...
        Assert.Equal(Color.FromArgb(255, 0, 248, 0), first.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 0, 248, 0), first.GetPixel(15, 15));

        // ...and everywhere else is fully transparent (tile index 0 / untouched tile sheet nibble).
        Assert.Equal(0, first.GetPixel(16, 0).A);
        Assert.Equal(0, first.GetPixel(0, 16).A);
        Assert.Equal(0, first.GetPixel(639, 479).A);
    }

    [Fact]
    public void Build_WithAnAllEmptyGrid_ReturnsNull()
    {
        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();
        var emptyGrid = new byte[BackdropReader.GridRowStride * BackdropReader.GridHeightTiles];

        Assert.Null(BackdropImageBuilder.Build(emptyGrid, tileSheet, paletteWords));
    }

    [Fact]
    public void ExtractTileGrid_WhenOnlyOneLayerIsTiles_BothLayerSlotsReadTheSameFirstHalf()
    {
        var data = new byte[BackdropReader.TileGridBaseOffset + BackdropReader.SecondTileGridOffset + 0x100];
        var grid = BuildTileGrid();
        Array.Copy(grid, 0, data, BackdropReader.TileGridBaseOffset, grid.Length);

        var result = new BackdropReadResult
        {
            Document = new BackdropDocument(),
            Data = data,
            LayerIsTiles = new[] { false, true },
        };

        var layer1Grid = BackdropReader.ExtractTileGrid(result, layerId: 1);

        Assert.NotNull(layer1Grid);
        Assert.Equal(TileIndex, layer1Grid![0]);
    }

    [Fact]
    public void ExtractTileGrid_WhenBothLayersAreTiles_LayerOneReadsTheSecondHalf()
    {
        var data = new byte[BackdropReader.TileGridBaseOffset + 2 * BackdropReader.SecondTileGridOffset];
        var secondGrid = BuildTileGrid();
        Array.Copy(
            secondGrid, 0, data,
            BackdropReader.TileGridBaseOffset + BackdropReader.SecondTileGridOffset, secondGrid.Length);

        var result = new BackdropReadResult
        {
            Document = new BackdropDocument(),
            Data = data,
            LayerIsTiles = new[] { true, true },
        };

        var layer0Grid = BackdropReader.ExtractTileGrid(result, layerId: 0);
        var layer1Grid = BackdropReader.ExtractTileGrid(result, layerId: 1);

        Assert.NotNull(layer0Grid);
        Assert.Equal(0, layer0Grid![0]);
        Assert.NotNull(layer1Grid);
        Assert.Equal(TileIndex, layer1Grid![0]);
    }

    [Fact]
    public void ConvertBackdrops_ForAMap389LikeFixture_WritesTextureAndCompanionWithScrollarValues()
    {
        RunConversion(mapLocations =>
        {
            var mapIndex = 389;
            mapLocations[mapIndex] = new MapLocation("TestZone", "Open Sea-389");
        },
        (outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Backdrop.Maps"]);
            Assert.Equal(1, report.Counters["Backdrop.LayersExported"]);
            Assert.Equal(2, report.Counters["Backdrop.Layers"]);
            Assert.Equal(1, report.Counters["Backdrop.Layers.Tiles"]);
            Assert.Equal(1, report.Counters["Backdrop.Layers.Disabled"]);

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Open Sea-389", "backdrop", "Open Sea-389.backdrop.json");
            Assert.True(File.Exists(companionPath));

            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;
            Assert.Equal(389, document.GetProperty("MapIndex").GetInt32());
            Assert.True(document.GetProperty("Enabled").GetBoolean());

            var layers = document.GetProperty("Layers");
            var layer0 = layers[0];
            Assert.Equal("Tiles", layer0.GetProperty("Mode").GetString());
            Assert.True(layer0.GetProperty("Ground").GetBoolean());
            Assert.Equal(1, layer0.GetProperty("BlendMode").GetInt32());
            Assert.Equal(640, layer0.GetProperty("Width").GetInt32());
            Assert.Equal(480, layer0.GetProperty("Height").GetInt32());
            Assert.False(string.IsNullOrEmpty(layer0.GetProperty("TextureAssetId").GetString()));

            var scrollar = layer0.GetProperty("Scrollar");
            Assert.Equal(1, scrollar.GetProperty("FactorXNum").GetInt32());
            Assert.Equal(1, scrollar.GetProperty("FactorXDenom").GetInt32());
            Assert.Equal(1, scrollar.GetProperty("FactorYNum").GetInt32());
            Assert.Equal(1, scrollar.GetProperty("FactorYDenom").GetInt32());
            Assert.Equal(10, scrollar.GetProperty("ScrollXPeriod").GetInt32());
            Assert.Equal(5, scrollar.GetProperty("ScrollYPeriod").GetInt32());

            var layer1 = layers[1];
            Assert.Equal("Disabled", layer1.GetProperty("Mode").GetString());

            var texturePath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Open Sea-389", "backdrop", "Open Sea-389-layer0.png");
            Assert.True(File.Exists(texturePath));
            var wrapperPath = Path.ChangeExtension(texturePath, ".texture");
            Assert.True(File.Exists(wrapperPath));
        });
    }

    [Fact]
    public void ConvertBackdrops_ForADisabledMap_WritesNoFiles()
    {
        RunConversion(_ => { }, (outputDirectory, report) =>
        {
            Assert.Equal(0, report.Counters.GetValueOrDefault("Backdrop.Maps"));

            var backdropDirectory = Path.Combine(outputDirectory, "Maps", "TestZone", "Disabled Map-4", "backdrop");
            Assert.False(Directory.Exists(backdropDirectory));
        }, includeDisabledMap: true, includeMap389: false);
    }

    private static void RunConversion(
        Action<Dictionary<int, MapLocation>> configureLocations,
        Action<string, ConversionReport> assert,
        bool includeDisabledMap = false,
        bool includeMap389 = true)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            var mapLocations = new Dictionary<int, MapLocation>();
            var mapFilter = new List<int>();

            if (includeMap389)
            {
                WriteMap389Fixture(dataDirectory);
                mapLocations[389] = new MapLocation("TestZone", "Open Sea-389");
                mapFilter.Add(389);
            }

            if (includeDisabledMap)
            {
                WriteDisabledMapFixture(dataDirectory, mapIndex: 4);
                mapLocations[4] = new MapLocation("TestZone", "Disabled Map-4");
                mapFilter.Add(4);
            }

            configureLocations(mapLocations);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            BackdropWriter.ConvertBackdrops(inputDirectory, outputDirectory, mapFilter, mapLocations, report);

            assert(outputDirectory, report);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void WriteMap389Fixture(string dataDirectory)
    {
        var data = BuildDataBlob();
        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();

        using var stream = File.Create(Path.Combine(dataDirectory, "map_389.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("ScrollParameters");
        writer.WriteStartObject();

        writer.WriteNumber("Graphics", 0);
        writer.WriteBoolean("HasGraphics", true);

        writer.WritePropertyName("Infos");
        writer.WriteStartObject();
        writer.WriteNumber("Enabled", 1);
        writer.WriteNumber("AnimNum", 1);
        writer.WritePropertyName("ModeLayer");
        writer.WriteStartArray();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(0);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("LayerInfos");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 1);
        writer.WriteNumber("BlendMode", 1);
        writer.WriteNumber("Ground", 1);
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 0);
        writer.WriteNumber("BlendMode", 0);
        writer.WriteNumber("Ground", 0);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("Scrollars");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("FactorXNum", 1);
        writer.WriteNumber("FactorXDenom", 1);
        writer.WriteNumber("FactorYNum", 1);
        writer.WriteNumber("FactorYDenom", 1);
        writer.WriteNumber("ScrollXSpeed", 0);
        writer.WriteNumber("ScrollXPeriod", 10);
        writer.WriteNumber("ScrollYSpeed", 0);
        writer.WriteNumber("ScrollYPeriod", 5);
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteNumber("FactorXNum", 0);
        writer.WriteNumber("FactorXDenom", 0);
        writer.WriteNumber("FactorYNum", 0);
        writer.WriteNumber("FactorYDenom", 0);
        writer.WriteNumber("ScrollXSpeed", 0);
        writer.WriteNumber("ScrollXPeriod", 0);
        writer.WriteNumber("ScrollYSpeed", 0);
        writer.WriteNumber("ScrollYPeriod", 0);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("Cellulars");
        writer.WriteStartArray();
        for (var i = 0; i < 2; i++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("CountBase", 0);
            writer.WriteNumber("AWaveY", 0);
            writer.WriteNumber("AWavePhase", 0);
            writer.WriteNumber("AWaveAmp", 0);
            writer.WriteNumber("BWaveY", 0);
            writer.WriteNumber("BWavePhase", 0);
            writer.WriteNumber("BWaveWeight", 0);
            writer.WriteNumber("Divisions", 0);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("Cells");
        writer.WriteStartArray();
        writer.WriteStartArray();
        writer.WriteEndArray();
        writer.WriteStartArray();
        writer.WriteEndArray();
        writer.WriteEndArray();

        writer.WritePropertyName("WaveLut");
        writer.WriteStartArray();
        writer.WriteEndArray();

        WriteIntArray(writer, "Data", data);
        WriteIntArray(writer, "TileSheetImageData", tileSheet);

        writer.WritePropertyName("PaletteWords");
        writer.WriteStartArray();
        foreach (var palette in paletteWords)
        {
            writer.WriteStartArray();
            foreach (var word in palette)
            {
                writer.WriteNumberValue(word);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteDisabledMapFixture(string dataDirectory, int mapIndex)
    {
        File.WriteAllText(
            Path.Combine(dataDirectory, $"map_{mapIndex}.json"),
            """{ "ScrollParameters": { "Infos": { "Enabled": 0 } } }""");
    }

    private static void WriteIntArray(Utf8JsonWriter writer, string propertyName, byte[] values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteNumberValue(value);
        }

        writer.WriteEndArray();
    }

    private static byte[] BuildDataBlob()
    {
        var data = new byte[BackdropReader.TileGridBaseOffset + BackdropReader.SecondTileGridOffset];
        var grid = BuildTileGrid();
        Array.Copy(grid, 0, data, BackdropReader.TileGridBaseOffset, grid.Length);
        return data;
    }

    /// <summary>Grid cell (0,0) = tile index <see cref="TileIndex"/>, palette 0; everything else empty.</summary>
    private static byte[] BuildTileGrid()
    {
        var grid = new byte[BackdropReader.GridRowStride * BackdropReader.GridHeightTiles];
        grid[0] = TileIndex;
        grid[1] = 0;
        return grid;
    }

    /// <summary>256x256 4bpp tile sheet, all nibble 0 except the 16x16 block at (<see cref="SheetU"/>,
    /// <see cref="SheetV"/>), which is nibble <see cref="PaletteEntry"/>.</summary>
    private static byte[] BuildTileSheet()
    {
        const int stride = 128; // 256 / 2
        var sheet = new byte[stride * 256];

        for (var y = SheetV; y < SheetV + 16; y++)
        {
            for (var x = SheetU; x < SheetU + 16; x += 2)
            {
                sheet[y * stride + (x >> 1)] = (byte)(PaletteEntry | (PaletteEntry << 4));
            }
        }

        return sheet;
    }

    private static ushort[][] BuildPaletteWords()
    {
        var palettes = new ushort[8][];
        for (var i = 0; i < 8; i++)
        {
            palettes[i] = new ushort[16];
        }

        palettes[0][PaletteEntry] = GreenPsxWord;
        return palettes;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
