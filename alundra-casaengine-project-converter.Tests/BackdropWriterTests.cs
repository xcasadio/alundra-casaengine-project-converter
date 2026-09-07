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
    public void BuildTileSheet_BakesTheWholeSheetAtItsOwnCoordinates()
    {
        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();

        using var sheet = BackdropImageBuilder.BuildTileSheet(tileSheet, paletteWords[0]);

        Assert.NotNull(sheet);
        Assert.Equal(256, sheet!.Width);
        Assert.Equal(256, sheet.Height);

        // The 16x16 block at sheet (SheetU, SheetV) is opaque bright green, at its OWN sheet
        // coordinates this time (BuildTileSheet draws at destX=0/destY=0, sheetU=0/sheetV=0, so
        // sheet pixels land at the same coordinates they occupy in the source sheet).
        Assert.Equal(Color.FromArgb(255, 0, 248, 0), sheet.GetPixel(SheetU, SheetV));
        Assert.Equal(Color.FromArgb(255, 0, 248, 0), sheet.GetPixel(SheetU + 15, SheetV + 15));

        // Everywhere else in the sheet is untouched nibble 0 -> transparent black.
        Assert.Equal(0, sheet.GetPixel(0, 0).A);
        Assert.Equal(0, sheet.GetPixel(255, 255).A);
    }

    [Fact]
    public void BuildTileSheet_WithAPaletteThatDecodesToNothing_ReturnsNull()
    {
        var tileSheet = BuildTileSheet();
        var emptyPalette = new ushort[16]; // every entry 0 -> transparent black everywhere

        Assert.Null(BackdropImageBuilder.BuildTileSheet(tileSheet, emptyPalette));
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
            Assert.Equal(1, report.Counters["Backdrop.FramesExported"]);
            Assert.Equal(2, report.Counters["Backdrop.Layers"]);
            Assert.Equal(1, report.Counters["Backdrop.Layers.Tiles"]);
            Assert.Equal(1, report.Counters["Backdrop.Layers.Disabled"]);

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Open Sea-389", "backdrop", "Open Sea-389.backdrop.json");
            Assert.True(File.Exists(companionPath));

            var companionText = File.ReadAllText(companionPath);

            // AnimNum <= 1 (D-E9-2/D-E9-3): the companion must serialize exactly as it did before
            // this slice - no new property, not even present as "null" (JsonIgnoreCondition.
            // WhenWritingNull), or all the other non-animated companions would move too (§1.2.g).
            Assert.DoesNotContain("FrameTextureAssetIds", companionText);

            var document = JsonDocument.Parse(companionText).RootElement;
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

            // AnimNum <= 1: no new frame file, of any number, ever appears.
            var backdropDirectory = Path.GetDirectoryName(texturePath)!;
            Assert.DoesNotContain(
                Directory.GetFiles(backdropDirectory), path => Path.GetFileName(path).Contains("-frame"));

            // The texture must also be PERSISTED into AssetInfos.json: the in-memory catalog dies
            // with the process, and a texture the runtime cannot resolve through the catalog makes
            // the whole layer silently unrenderable (this exact gap shipped once - the runtime
            // skipped all 132 exported layers because ConvertBackdrops never called Save()).
            var catalogPath = Path.Combine(outputDirectory, "AssetInfos.json");
            Assert.True(File.Exists(catalogPath));
            var catalogText = File.ReadAllText(catalogPath);
            var textureAssetId = layer0.GetProperty("TextureAssetId").GetString();
            Assert.Contains(textureAssetId!, catalogText);
        });
    }

    [Fact]
    public void ConvertBackdrops_ForAnAnimNum4Fixture_ExportsFourFramesWithTheShiftedVBand()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            const int mapIndex = 990;
            const int animNum = 4;
            WriteAnimatedMapFixture(dataDirectory, mapIndex, animNum);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [mapIndex] = new MapLocation("TestZone", "Anim Layer-990"),
            };
            var mapFilter = new List<int> { mapIndex };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            BackdropWriter.ConvertBackdrops(inputDirectory, outputDirectory, mapFilter, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Backdrop.LayersExported"]);
            Assert.Equal(animNum, report.Counters["Backdrop.FramesExported"]);

            var backdropDirectory = Path.Combine(outputDirectory, "Maps", "TestZone", "Anim Layer-990", "backdrop");
            var companionPath = Path.Combine(backdropDirectory, "Anim Layer-990.backdrop.json");
            Assert.True(File.Exists(companionPath));

            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;
            var layer0 = document.GetProperty("Layers")[0];
            var textureAssetId = layer0.GetProperty("TextureAssetId").GetString();
            Assert.False(string.IsNullOrEmpty(textureAssetId));

            var frameIds = layer0.GetProperty("FrameTextureAssetIds");
            Assert.Equal(JsonValueKind.Array, frameIds.ValueKind);
            Assert.Equal(animNum, frameIds.GetArrayLength());
            Assert.Equal(textureAssetId, frameIds[0].GetString());
            for (var frame = 1; frame < animNum; frame++)
            {
                Assert.False(string.IsNullOrEmpty(frameIds[frame].GetString()));
            }

            // Frame 0 keeps its pre-existing, un-suffixed name and id (D-E9-2) - nothing renamed.
            var frame0Path = Path.Combine(backdropDirectory, "Anim Layer-990-layer0.png");
            Assert.True(File.Exists(frame0Path));

            // Frames 1..3 are new files.
            var frame1Path = Path.Combine(backdropDirectory, "Anim Layer-990-layer0-frame1.png");
            var frame2Path = Path.Combine(backdropDirectory, "Anim Layer-990-layer0-frame2.png");
            var frame3Path = Path.Combine(backdropDirectory, "Anim Layer-990-layer0-frame3.png");
            Assert.True(File.Exists(frame1Path));
            Assert.True(File.Exists(frame2Path));
            Assert.True(File.Exists(frame3Path));

            // Pixel (§1.2.b, D-E9-2): frame 0 samples the tile sheet's V band [0, 64) -> green;
            // frame 1's vAnim = (1 << 8) / 4 = 64 shifts the very same tile into band [64, 128) ->
            // blue.
            using (var frame0 = new Bitmap(frame0Path))
            using (var frame1 = new Bitmap(frame1Path))
            {
                Assert.Equal(Color.FromArgb(255, 0, 248, 0), frame0.GetPixel(0, 0));
                Assert.Equal(Color.FromArgb(255, 0, 0, 248), frame1.GetPixel(0, 0));
            }

            // Empty frame (D-E9-4): frames 2 and 3 (vAnim 128 and 192) land on an all-transparent
            // quarter of the sheet, so BackdropImageBuilder.Build returns null for them - the
            // converter must still emit a fully transparent 640x480 texture for each, rather than
            // leave a hole in the frame array.
            using (var frame2 = new Bitmap(frame2Path))
            using (var frame3 = new Bitmap(frame3Path))
            {
                Assert.Equal(640, frame2.Width);
                Assert.Equal(480, frame2.Height);
                Assert.Equal(0, frame2.GetPixel(0, 0).A);
                Assert.Equal(0, frame3.GetPixel(0, 0).A);
            }
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
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

    [Fact]
    public void ConvertBackdrops_ForAMapWithTheOverlayGate_RoundTripsTheTintFields()
    {
        RunConversion(mapLocations =>
        {
            var mapIndex = 389;
            mapLocations[mapIndex] = new MapLocation("TestZone", "Open Sea-389");
        },
        (outputDirectory, report) =>
        {
            Assert.Equal(1, report.Counters["Backdrop.OverlayTints"]);

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Open Sea-389", "backdrop", "Open Sea-389.backdrop.json");
            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;

            Assert.True(document.GetProperty("OverlayEnabled").GetBoolean());
            Assert.Equal(84, document.GetProperty("OverlayColorR").GetInt32());
            Assert.Equal(75, document.GetProperty("OverlayColorG").GetInt32());
            Assert.Equal(52, document.GetProperty("OverlayColorB").GetInt32());
        },
        bgColorA: 1,
        overlayColorBytes: new byte[] { 84, 75, 52 });
    }

    [Fact]
    public void ConvertBackdrops_ForACellularOnlyFixture_StillEmitsACompanionCarryingTheTint()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            const int mapIndex = 96;
            WriteCellularOnlyMapFixture(dataDirectory, mapIndex, bgColorA: 1, overlayColorBytes: new byte[] { 40, 40, 40 });

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [mapIndex] = new MapLocation("TestZone", "Cellular Only-96"),
            };
            var mapFilter = new List<int> { mapIndex };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            BackdropWriter.ConvertBackdrops(inputDirectory, outputDirectory, mapFilter, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Backdrop.Maps"]);
            Assert.Equal(1, report.Counters["Backdrop.OverlayTints"]);
            Assert.Equal(0, report.Counters.GetValueOrDefault("Backdrop.LayersExported"));

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Cellular Only-96", "backdrop", "Cellular Only-96.backdrop.json");
            Assert.True(File.Exists(companionPath));

            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;
            Assert.True(document.GetProperty("OverlayEnabled").GetBoolean());
            Assert.Equal(40, document.GetProperty("OverlayColorR").GetInt32());
            Assert.Equal(40, document.GetProperty("OverlayColorG").GetInt32());
            Assert.Equal(40, document.GetProperty("OverlayColorB").GetInt32());

            var layers = document.GetProperty("Layers");
            Assert.Equal("Disabled", layers[0].GetProperty("Mode").GetString());
            Assert.Equal("Cellular", layers[1].GetProperty("Mode").GetString());

            // No Tiles layer means no texture directory at all.
            var backdropDirectory = Path.Combine(outputDirectory, "Maps", "TestZone", "Cellular Only-96", "backdrop");
            Assert.Empty(Directory.GetFiles(backdropDirectory, "*.png"));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConvertBackdrops_WithBGColorAZero_EmitsOverlayEnabledFalse()
    {
        RunConversion(mapLocations =>
        {
            var mapIndex = 389;
            mapLocations[mapIndex] = new MapLocation("TestZone", "Open Sea-389");
        },
        (outputDirectory, report) =>
        {
            Assert.Equal(0, report.Counters.GetValueOrDefault("Backdrop.OverlayTints"));

            var companionPath = Path.Combine(
                outputDirectory, "Maps", "TestZone", "Open Sea-389", "backdrop", "Open Sea-389.backdrop.json");
            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;

            Assert.False(document.GetProperty("OverlayEnabled").GetBoolean());
            Assert.Equal(0, document.GetProperty("OverlayColorR").GetInt32());
        },
        bgColorA: 0);
    }

    [Fact]
    public void ConvertBackdrops_ForACellularMapWithCells_ExportsOneSheetPerUsedPalette()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            var dataDirectory = Path.Combine(inputDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            const int mapIndex = 271;
            WriteCellularWithCellsMapFixture(dataDirectory, mapIndex);

            var mapLocations = new Dictionary<int, MapLocation>
            {
                [mapIndex] = new MapLocation("TestZone", "Cellular Cells-271"),
            };
            var mapFilter = new List<int> { mapIndex };

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            BackdropWriter.ConvertBackdrops(inputDirectory, outputDirectory, mapFilter, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(1, report.Counters["Backdrop.Maps"]);
            Assert.Equal(1, report.Counters["Backdrop.CellularMapsHandled"]);

            // Cells reference PalDex 0 twice and PalDex CellularSecondPalDex once: only the two
            // DISTINCT palettes actually used ever get baked, never one sheet per cell.
            Assert.Equal(2, report.Counters["Backdrop.CellularSheetsExported"]);

            var backdropDirectory = Path.Combine(outputDirectory, "Maps", "TestZone", "Cellular Cells-271", "backdrop");
            var companionPath = Path.Combine(backdropDirectory, "Cellular Cells-271.backdrop.json");
            Assert.True(File.Exists(companionPath));

            var document = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;
            var sheetIds = document.GetProperty("CellularSheetTextureAssetIds");
            Assert.Equal(JsonValueKind.Array, sheetIds.ValueKind);
            Assert.Equal(8, sheetIds.GetArrayLength());

            Assert.False(string.IsNullOrEmpty(sheetIds[0].GetString()));
            Assert.Equal(JsonValueKind.Null, sheetIds[1].ValueKind);
            Assert.False(string.IsNullOrEmpty(sheetIds[CellularSecondPalDex].GetString()));

            var sheet0Path = Path.Combine(backdropDirectory, "Cellular Cells-271-cellsheet0.png");
            var sheet3Path = Path.Combine(
                backdropDirectory, $"Cellular Cells-271-cellsheet{CellularSecondPalDex}.png");
            Assert.True(File.Exists(sheet0Path));
            Assert.True(File.Exists(sheet3Path));

            using (var sheet0 = new Bitmap(sheet0Path))
            using (var sheet3 = new Bitmap(sheet3Path))
            {
                Assert.Equal(256, sheet0.Width);
                Assert.Equal(256, sheet0.Height);
                Assert.Equal(Color.FromArgb(255, 0, 248, 0), sheet0.GetPixel(SheetU, SheetV));
                Assert.Equal(Color.FromArgb(255, 0, 0, 248), sheet3.GetPixel(SheetU, SheetV));
            }

            // The catalog persisted both new sheet textures - same requirement as the layer textures.
            var catalogPath = Path.Combine(outputDirectory, "AssetInfos.json");
            var catalogText = File.ReadAllText(catalogPath);
            Assert.Contains(sheetIds[0].GetString()!, catalogText);
            Assert.Contains(sheetIds[CellularSecondPalDex].GetString()!, catalogText);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static void RunConversion(
        Action<Dictionary<int, MapLocation>> configureLocations,
        Action<string, ConversionReport> assert,
        bool includeDisabledMap = false,
        bool includeMap389 = true,
        int bgColorA = 0,
        byte[]? overlayColorBytes = null)
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
                WriteMap389Fixture(dataDirectory, bgColorA, overlayColorBytes);
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

    // Overlay tint bytes live well before TileGridBaseOffset, in the zero-padding BuildDataBlob
    // otherwise leaves untouched.
    private const int OverlayTestPointer = 100;

    private static void WriteMap389Fixture(string dataDirectory, int bgColorA = 0, byte[]? overlayColorBytes = null)
    {
        var data = BuildDataBlob();
        if (overlayColorBytes != null)
        {
            Array.Copy(overlayColorBytes, 0, data, OverlayTestPointer, overlayColorBytes.Length);
        }

        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();

        using var stream = File.Create(Path.Combine(dataDirectory, "map_389.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("ScrollParameters");
        writer.WriteStartObject();

        writer.WriteNumber("Graphics", 0);
        writer.WriteBoolean("HasGraphics", true);
        writer.WriteNumber("Overlay", OverlayTestPointer);

        writer.WritePropertyName("Infos");
        writer.WriteStartObject();
        writer.WriteNumber("Enabled", 1);
        writer.WriteNumber("AnimNum", 1);
        writer.WriteNumber("BGColorA", bgColorA);
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

    /// <summary>Mirrors the corpus's 9 Cellular-only tinted maps (e.g. map 96): layer 1 is
    /// <c>Mode 2</c> ("Cellular"), layer 0 is disabled, and no layer has graphics to bake into a
    /// texture - only the overlay tint is exportable.</summary>
    private static void WriteCellularOnlyMapFixture(
        string dataDirectory, int mapIndex, int bgColorA, byte[] overlayColorBytes)
    {
        var data = new byte[OverlayTestPointer + overlayColorBytes.Length + 16];
        Array.Copy(overlayColorBytes, 0, data, OverlayTestPointer, overlayColorBytes.Length);

        using var stream = File.Create(Path.Combine(dataDirectory, $"map_{mapIndex}.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("ScrollParameters");
        writer.WriteStartObject();

        writer.WriteNumber("Graphics", 0);
        writer.WriteBoolean("HasGraphics", true);
        writer.WriteNumber("Overlay", OverlayTestPointer);

        writer.WritePropertyName("Infos");
        writer.WriteStartObject();
        writer.WriteNumber("Enabled", 1);
        writer.WriteNumber("AnimNum", 1);
        writer.WriteNumber("BGColorA", bgColorA);
        writer.WritePropertyName("ModeLayer");
        writer.WriteStartArray();
        writer.WriteNumberValue(0);
        writer.WriteNumberValue(2);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("LayerInfos");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 0);
        writer.WriteNumber("BlendMode", 0);
        writer.WriteNumber("Ground", 0);
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 0);
        writer.WriteNumber("BlendMode", 0);
        writer.WriteNumber("Ground", 0);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("Scrollars");
        writer.WriteStartArray();
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
        WriteIntArray(writer, "TileSheetImageData", Array.Empty<byte>());

        writer.WritePropertyName("PaletteWords");
        writer.WriteStartArray();
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Mirrors the 7 AnimNum=4 Tiles-mode maps of the corpus (§1.2.d): one grid cell holds a tile
    /// whose base V band is [0, 64) (green, see <see cref="BuildAnimatedTileSheet"/>); the tile
    /// sheet's [64, 128) band is a different colour (blue) so frame 1 (vAnim = 64) is distinguishable
    /// pixel-for-pixel from frame 0, and bands [128, 256) are left all-zero (transparent) so frames 2
    /// and 3 (vAnim 128 and 192) exercise the D-E9-4 "Build returns null" path.
    /// </summary>
    // PalDex used by the second cell of WriteCellularWithCellsMapFixture, distinct from PalDex 0.
    private const int CellularSecondPalDex = 3;

    /// <summary>
    /// A Cellular (mode 2) layer whose 3 cells reference PalDex 0 twice and
    /// <see cref="CellularSecondPalDex"/> once, backed by the same tile sheet/palette shape as
    /// <see cref="BuildTileSheet"/> - palette <see cref="CellularSecondPalDex"/> resolves the sheet's
    /// nibble <see cref="PaletteEntry"/> block to blue instead of green, so the two baked sheets are
    /// pixel-distinguishable.
    /// </summary>
    private static void WriteCellularWithCellsMapFixture(string dataDirectory, int mapIndex)
    {
        var tileSheet = BuildTileSheet();
        var paletteWords = BuildPaletteWords();
        paletteWords[CellularSecondPalDex][PaletteEntry] = BluePsxWord;

        using var stream = File.Create(Path.Combine(dataDirectory, $"map_{mapIndex}.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("ScrollParameters");
        writer.WriteStartObject();

        writer.WriteNumber("Graphics", 0);
        writer.WriteBoolean("HasGraphics", false);
        writer.WriteNumber("Overlay", OverlayTestPointer);

        writer.WritePropertyName("Infos");
        writer.WriteStartObject();
        writer.WriteNumber("Enabled", 1);
        writer.WriteNumber("AnimNum", 1);
        writer.WriteNumber("BGColorA", 0);
        writer.WritePropertyName("ModeLayer");
        writer.WriteStartArray();
        writer.WriteNumberValue(0);
        writer.WriteNumberValue(2);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("LayerInfos");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 0);
        writer.WriteNumber("BlendMode", 0);
        writer.WriteNumber("Ground", 0);
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 0);
        writer.WriteNumber("BlendMode", 0);
        writer.WriteNumber("Ground", 0);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("Scrollars");
        writer.WriteStartArray();
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
        writer.WriteStartArray(); // layer 0: no cells
        writer.WriteEndArray();
        writer.WriteStartArray(); // layer 1: 3 cells, PalDex 0, CellularSecondPalDex, 0
        WriteCell(writer, palDex: 0);
        WriteCell(writer, palDex: CellularSecondPalDex);
        WriteCell(writer, palDex: 0);
        writer.WriteEndArray();
        writer.WriteEndArray();

        writer.WritePropertyName("WaveLut");
        writer.WriteStartArray();
        writer.WriteEndArray();

        WriteIntArray(writer, "Data", Array.Empty<byte>());
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

    private static void WriteCell(Utf8JsonWriter writer, int palDex)
    {
        writer.WriteStartObject();
        writer.WriteNumber("PalDex", palDex);
        writer.WriteNumber("U0", 0);
        writer.WriteNumber("V0", 0);
        writer.WriteNumber("U1", 16);
        writer.WriteNumber("V1", 16);
        writer.WriteNumber("Type", 0);
        writer.WriteNumber("X0", 0);
        writer.WriteNumber("Y0", 0);
        writer.WriteNumber("CamXNum", 1);
        writer.WriteNumber("CamXDen", 1);
        writer.WriteNumber("CamYNum", 1);
        writer.WriteNumber("CamYDen", 1);
        writer.WriteNumber("DX", 0);
        writer.WriteNumber("PeriodX", 0);
        writer.WriteNumber("DY", 0);
        writer.WriteNumber("PeriodY", 0);
        writer.WriteEndObject();
    }

    private static void WriteAnimatedMapFixture(string dataDirectory, int mapIndex, int animNum)
    {
        var data = BuildAnimatedDataBlob();
        var tileSheet = BuildAnimatedTileSheet();
        var paletteWords = BuildAnimatedPaletteWords();

        using var stream = File.Create(Path.Combine(dataDirectory, $"map_{mapIndex}.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("ScrollParameters");
        writer.WriteStartObject();

        writer.WriteNumber("Graphics", 0);
        writer.WriteBoolean("HasGraphics", true);
        writer.WriteNumber("Overlay", OverlayTestPointer);

        writer.WritePropertyName("Infos");
        writer.WriteStartObject();
        writer.WriteNumber("Enabled", 1);
        writer.WriteNumber("AnimNum", animNum);
        writer.WriteNumber("BGColorA", 0);
        writer.WritePropertyName("ModeLayer");
        writer.WriteStartArray();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(0);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("LayerInfos");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("AnimTimer", 4);
        writer.WriteNumber("BlendMode", 1);
        writer.WriteNumber("Ground", 0);
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
        writer.WriteNumber("ScrollXPeriod", 0);
        writer.WriteNumber("ScrollYSpeed", 0);
        writer.WriteNumber("ScrollYPeriod", 0);
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

    // Tile index 0x01: low nibble (U) = 1 -> sheetU = 16; high nibble (V) = 0 -> base sheetV = 0.
    private const int AnimatedTileIndex = 0x01;
    private const int AnimatedSheetU = 16;
    private const int AnimatedPaletteEntryLowBand = 2; // V band [0, 64) -> green
    private const int AnimatedPaletteEntryHighBand = 3; // V band [64, 128) -> blue
    private const ushort BluePsxWord = 0x7C00; // FromPsxColor -> (0, 0, 248)

    private static byte[] BuildAnimatedDataBlob()
    {
        var data = new byte[BackdropReader.TileGridBaseOffset + BackdropReader.SecondTileGridOffset];
        var grid = BuildAnimatedTileGrid();
        Array.Copy(grid, 0, data, BackdropReader.TileGridBaseOffset, grid.Length);
        return data;
    }

    /// <summary>Grid cell (0,0) = tile index <see cref="AnimatedTileIndex"/>, palette 0; everything
    /// else empty.</summary>
    private static byte[] BuildAnimatedTileGrid()
    {
        var grid = new byte[BackdropReader.GridRowStride * BackdropReader.GridHeightTiles];
        grid[0] = AnimatedTileIndex;
        grid[1] = 0;
        return grid;
    }

    /// <summary>
    /// 256x256 4bpp tile sheet: the 16px-wide column at <see cref="AnimatedSheetU"/> is nibble
    /// <see cref="AnimatedPaletteEntryLowBand"/> for rows [0, 64), nibble
    /// <see cref="AnimatedPaletteEntryHighBand"/> for rows [64, 128), and nibble 0 (transparent) for
    /// rows [128, 256) - deliberately, so a frame whose vAnim lands in that range bakes to an
    /// all-transparent (null) result. Everywhere else is nibble 0 too.
    /// </summary>
    private static byte[] BuildAnimatedTileSheet()
    {
        const int stride = 128; // 256 / 2
        var sheet = new byte[stride * 256];

        for (var y = 0; y < 64; y++)
        {
            for (var x = AnimatedSheetU; x < AnimatedSheetU + 16; x += 2)
            {
                sheet[y * stride + (x >> 1)] = (byte)(AnimatedPaletteEntryLowBand | (AnimatedPaletteEntryLowBand << 4));
            }
        }

        for (var y = 64; y < 128; y++)
        {
            for (var x = AnimatedSheetU; x < AnimatedSheetU + 16; x += 2)
            {
                sheet[y * stride + (x >> 1)] = (byte)(AnimatedPaletteEntryHighBand | (AnimatedPaletteEntryHighBand << 4));
            }
        }

        return sheet;
    }

    private static ushort[][] BuildAnimatedPaletteWords()
    {
        var palettes = new ushort[8][];
        for (var i = 0; i < 8; i++)
        {
            palettes[i] = new ushort[16];
        }

        palettes[0][AnimatedPaletteEntryLowBand] = GreenPsxWord;
        palettes[0][AnimatedPaletteEntryHighBand] = BluePsxWord;
        return palettes;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
