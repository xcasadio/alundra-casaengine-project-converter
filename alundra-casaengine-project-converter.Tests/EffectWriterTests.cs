using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// E19.g G1 (docs/plan-e19-opcodes.md, "E19.g G1"): the effect records and effect tables of each map and of
/// the global table, exported as raw companions plus their effect sheets. Synthetic fixtures first, then the
/// real values of the plan (maps 476, 391, 163 and 161; skipped when data-extracted/ is not next to the binaries)
/// and the guard of the full-corpus invariants.
/// </summary>
public class EffectWriterTests
{
    // ---------------------------------------------------------------- reader, synthetic

    [Fact]
    public void Read_DropsTheTrailingPaddingCases_AndKeepsTheAnimationIndices()
    {
        // AnimationCount 4 over a longer offsets array: cases 0 and 1 are real, cases 2 and 3 are padding
        // (offset 0) whose frames are junk; the entries beyond the count are not cases at all.
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8, 12, 0, 0, 77, 88, 99 }, 4,
                Anim(Frame(0x8A, 20, 0, Image(5, 6, 7, 8, Corners)), End(1)),
                Anim(Frame(0x81, 30, 0, Image(1, 2, 3, 4, Corners)), End(0)),
                Anim(Frame(0x85, 40, 0, Image(9, 9, 9, 9, Corners)), End(0)),
                Anim(Frame(0x85, 50, 0, Image(9, 9, 9, 9, Corners)), End(0))));

        var bank = Read(json);

        var table = Assert.Single(bank.Tables);
        Assert.Equal(2, table.Animations.Count);
        Assert.Equal(2, bank.AnimationSlotsDropped);
        Assert.Equal(2, table.ImageSets.Count); // the padding cases contribute no image set
        Assert.Empty(bank.Errors);
    }

    [Fact]
    public void Read_ExportsTheTicksOfEachDisplayedFrame_AndTheKindOfEnd()
    {
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8, 12 }, 2,
                // raw delay 0x80 | 10 -> 10 ticks, then 0x80 | 127 -> 127 ticks; the end pseudo-frame loops
                Anim(Frame(0x8A, 20, 0, Image(0, 0, 4, 4, Corners)), Frame(0xFF, 30, 0, Image(0, 0, 4, 4, Corners)), End(1)),
                // raw delay 0 on the end pseudo-frame destroys the effect
                Anim(Frame(0x81, 20, 0, Image(0, 0, 4, 4, Corners)), End(0))));

        var table = Assert.Single(Read(json).Tables);

        Assert.Equal(new[] { new[] { 10, 0 }, new[] { 127, 1 } }, table.Animations[0].Frames);
        Assert.Equal("Loop", table.Animations[0].End);
        Assert.Equal(new[] { new[] { 1, 0 } }, table.Animations[1].Frames);
        Assert.Equal("Destroy", table.Animations[1].End);
    }

    [Fact]
    public void Read_SharesAnImageSetPerTable_InOrderOfFirstUse_WithItsDepthBias()
    {
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8, 12 }, 2,
                Anim(Frame(0x82, 20, 52, Image(1, 1, 2, 2, Corners)), Frame(0x82, 40, 7, Image(3, 3, 2, 2, Corners)), End(1)),
                Anim(Frame(0x82, 40, 7, Image(3, 3, 2, 2, Corners)), Frame(0x82, 20, 52, Image(1, 1, 2, 2, Corners)), End(1))),
            // a second table reuses the same pointer value: pointers are table-relative, the sets are per table
            Table(new[] { 8 }, 1, Anim(Frame(0x82, 20, 3, Image(9, 9, 2, 2, Corners)), End(0))));

        var bank = Read(json);

        Assert.Equal(2, bank.Tables.Count);
        Assert.Equal(new[] { 52, 7 }, bank.Tables[0].ImageSets.Select(set => set.Idsv).ToArray());
        Assert.Equal(new[] { new[] { 2, 1 }, new[] { 2, 0 } }, bank.Tables[0].Animations[1].Frames);
        var second = Assert.Single(bank.Tables[1].ImageSets);
        Assert.Equal(3, second.Idsv);
        Assert.Equal(9, second.Images[0].U);
    }

    [Fact]
    public void Read_DropsTheDegenerateImages_AndKeepsTheEmptyImageSet()
    {
        var degenerate = Image(0, 0, 0, 0, new[] { 0, 0, 0, 0, 0, 0, 0, 0 });
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8, 12 }, 2,
                Anim(Frame(0x82, 20, 0, degenerate, Image(4, 4, 8, 8, Corners)), End(1)),
                Anim(Frame(0x82, 40, 0, degenerate), End(1))));

        var bank = Read(json);

        var table = Assert.Single(bank.Tables);
        Assert.Equal(2, bank.ImagesDegenerateDropped);
        Assert.Equal(1, bank.Images);
        Assert.Single(table.ImageSets[0].Images);
        Assert.Empty(table.ImageSets[1].Images); // an emptied set stays: the indices are stable
    }

    [Fact]
    public void Read_KeepsTheCornerOrderOfAMirroredQuad_AndTheModeBits()
    {
        // A mirror in X swaps the left and right corners: X1 > X2. The corners are exported as they are.
        var mirrored = new[] { 12, -40, -12, -40, 12, -9, -12, -9 };
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8 }, 1,
                Anim(
                    Frame(
                        0x82, 20, 0,
                        Image(25, 3, 24, 31, mirrored, spritesheet: 0x18),   // semi-transparent, ABR 1
                        Image(0, 0, 8, 8, Corners, spritesheet: 0x00),       // opaque
                        Image(1, 1, 8, 8, Corners, spritesheet: 0x2A)),      // semi-transparent, ABR 2, page 2
                    End(1))));

        var images = Read(json).Tables[0].ImageSets[0].Images;

        Assert.Equal(25, images[0].U);
        Assert.Equal(3, images[0].V);
        Assert.Equal(24, images[0].W);
        Assert.Equal(31, images[0].H);
        Assert.Equal(mirrored, images[0].C);
        Assert.True(images[0].Semi);
        Assert.Equal(1, images[0].Abr);
        Assert.False(images[1].Semi);
        Assert.Equal(0, images[1].Abr);
        Assert.True(images[2].Semi);
        Assert.Equal(2, images[2].Abr);
    }

    [Fact]
    public void Read_ReportsAHoleInTheCaseTable()
    {
        var json = MapJson(
            Array.Empty<object>(),
            Table(
                new[] { 8, 0, 12 }, 3,
                Anim(Frame(0x82, 20, 0, Image(0, 0, 4, 4, Corners)), End(1)),
                Anim(Frame(0x82, 20, 0, Image(0, 0, 4, 4, Corners)), End(1)),
                Anim(Frame(0x82, 20, 0, Image(0, 0, 4, 4, Corners)), End(1))));

        var bank = Read(json);

        Assert.Contains(bank.Errors, error => error.Contains("hole", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_ReportsAnUnknownEndDelay()
    {
        var json = MapJson(
            Array.Empty<object>(),
            Table(new[] { 8 }, 1, Anim(Frame(0x82, 20, 0, Image(0, 0, 4, 4, Corners)), End(5))));

        var bank = Read(json);

        Assert.Contains(bank.Errors, error => error.Contains("unknown delay 5", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_ToleratesReducedFixtures()
    {
        Assert.Empty(Read("{}").Tables);
        Assert.Empty(Read("{ \"SpriteInfo\": {} }").Records);
        Assert.Empty(Read("{ \"SpriteInfo\": { \"MapEffectRecords\": null, \"SpriteEffectRecords\": null } }").Tables);

        var reduced = Read("{ \"SpriteInfo\": { \"SpriteEffectRecords\": [ { \"EffectId\": 1 } ] } }");
        var table = Assert.Single(reduced.Tables);
        Assert.Empty(table.Animations);
        Assert.Empty(reduced.Errors);
    }

    // ---------------------------------------------------------------- writer, synthetic

    [Fact]
    public void ConvertEffects_WritesTheCompanionTheGlobalCompanionAndTheSheets()
    {
        RunConversion((inputDirectory, mapLocations) =>
            {
                var dataDirectory = Path.Combine(inputDirectory, "data");
                mapLocations[990] = new MapLocation("TestZone", "Effects Map-990");
                File.WriteAllText(
                    Path.Combine(dataDirectory, "map_990.json"),
                    MapJson(
                        new object[]
                        {
                            Record(0x80, 0, 80, 22, 6, 1),   // the map table, animation 1
                            Record(0x40, 0, 10, 12, 0, 0),   // the global table, spawns at load
                        },
                        Table(
                            new[] { 8, 12, 0, 0 }, 4,
                            Anim(Frame(0x8A, 20, 52, Image(0, 0, 8, 8, Corners)), End(1)),
                            Anim(Frame(0x82, 40, 52, Image(8, 0, 8, 8, Corners)), Frame(0x82, 20, 52, Image(0, 0, 8, 8, Corners)), End(0)),
                            Anim(Frame(0x82, 20, 0, Image(0, 0, 8, 8, Corners)), End(0)),
                            Anim(Frame(0x82, 20, 0, Image(0, 0, 8, 8, Corners)), End(0)))));
                WriteSheet(Path.Combine(dataDirectory, "map_990_effectsheet.png"), 64, 16);

                File.WriteAllText(
                    Path.Combine(dataDirectory, "map_alundra.json"),
                    MapJson(
                        Array.Empty<object>(),
                        Table(new[] { 8 }, 1, Anim(Frame(0x83, 20, 0, Image(0, 0, 8, 8, Corners)), End(1)))));
                WriteSheet(Path.Combine(dataDirectory, "map_alundra_effectsheet.png"), 32, 8);

                return new List<int> { 990 };
            },
            (outputDirectory, report) =>
            {
                Assert.Empty(report.Errors);

                var companionPath = Path.Combine(
                    outputDirectory, "Maps", "TestZone", "Effects Map-990", "effects", "Effects Map-990.effects.json");
                Assert.True(File.Exists(companionPath));
                var companion = JsonDocument.Parse(File.ReadAllText(companionPath)).RootElement;

                Assert.Equal(990, companion.GetProperty("MapIndex").GetInt32());
                var records = companion.GetProperty("Records");
                Assert.Equal(2, records.GetArrayLength());
                Assert.Equal(0x80, records[0].GetProperty("Flags").GetInt32());
                Assert.Equal(0, records[0].GetProperty("Effect").GetInt32());
                Assert.Equal(80, records[0].GetProperty("X").GetInt32());
                Assert.Equal(22, records[0].GetProperty("Y").GetInt32());
                Assert.Equal(6, records[0].GetProperty("Z").GetInt32());
                Assert.Equal(1, records[0].GetProperty("Anim").GetInt32());
                Assert.Equal(51, records[0].GetProperty("X2").GetInt32());
                Assert.False(records[0].TryGetProperty("U1", out _));

                var table = Assert.Single(companion.GetProperty("Effects").EnumerateArray());
                Assert.Equal(2, table.GetProperty("Animations").GetArrayLength()); // the 2 padding cases are gone
                Assert.Equal("Loop", table.GetProperty("Animations")[0].GetProperty("End").GetString());
                Assert.Equal("Destroy", table.GetProperty("Animations")[1].GetProperty("End").GetString());
                Assert.Equal(10, table.GetProperty("Animations")[0].GetProperty("Frames")[0][0].GetInt32());
                var image = table.GetProperty("ImageSets")[0].GetProperty("Images")[0];
                Assert.Equal(52, table.GetProperty("ImageSets")[0].GetProperty("Idsv").GetInt32());
                Assert.Equal(8, image.GetProperty("W").GetInt32());
                Assert.Equal(8, image.GetProperty("C").GetArrayLength());
                Assert.True(image.GetProperty("Semi").GetBoolean());
                Assert.Equal(1, image.GetProperty("Abr").GetInt32());

                // The sheet is a texture asset, resolved through the saved catalog.
                var sheetId = companion.GetProperty("SheetTextureAssetId").GetString();
                Assert.False(string.IsNullOrEmpty(sheetId));
                var catalogText = File.ReadAllText(Path.Combine(outputDirectory, "AssetInfos.json"));
                Assert.Contains(sheetId!, catalogText);
                var effectsDirectory = Path.GetDirectoryName(companionPath)!;
                Assert.True(File.Exists(Path.Combine(effectsDirectory, "map_990_effectsheet.png")));
                Assert.True(File.Exists(Path.Combine(effectsDirectory, "map_990_effectsheet.texture")));

                // The global table: its own companion and sheet, outside any map folder.
                var globalPath = Path.Combine(outputDirectory, "Data", "effects", "effects-global.json");
                Assert.True(File.Exists(globalPath));
                var global = JsonDocument.Parse(File.ReadAllText(globalPath)).RootElement;
                Assert.False(string.IsNullOrEmpty(global.GetProperty("SheetTextureAssetId").GetString()));
                Assert.Single(global.GetProperty("Effects").EnumerateArray());
                Assert.True(File.Exists(Path.Combine(outputDirectory, "Data", "effects", "map_alundra_effectsheet.png")));
                Assert.True(File.Exists(Path.Combine(outputDirectory, "Data", "effects", "map_alundra_effectsheet.texture")));

                Assert.Equal(2, report.Counters["Effects.Records"]);
                Assert.Equal(1, report.Counters["Effects.RecordsSpawnAtLoad"]);
                Assert.Equal(1, report.Counters["Effects.RecordsMapTable"]);
                Assert.Equal(1, report.Counters["Effects.RecordsGlobalTable"]);
                Assert.Equal(2, report.Counters["Effects.Tables"]);
                Assert.Equal(3, report.Counters["Effects.Animations"]);
                Assert.Equal(2, report.Counters["Effects.AnimationSlotsDropped"]);
                Assert.Equal(4, report.Counters["Effects.Frames"]);
                Assert.Equal(3, report.Counters["Effects.ImageSets"]);
                Assert.Equal(3, report.Counters["Effects.Images"]);
                Assert.Equal(0, report.Counters["Effects.ImagesDegenerateDropped"]);
                Assert.Equal(1, report.Counters["Effects.Companions"]);
                Assert.Equal(2, report.Counters["Effects.Sheets"]);
                Assert.Equal(0, report.Counters["Effects.UnresolvedRecords"]);
                Assert.Equal(0, report.Counters.GetValueOrDefault("Assets.Texture"));
            });
    }

    [Fact]
    public void ConvertEffects_ForARecordThatNamesAnAbsentTableOrAnimation_ReportsAnError()
    {
        RunConversion((inputDirectory, mapLocations) =>
            {
                var dataDirectory = Path.Combine(inputDirectory, "data");
                mapLocations[991] = new MapLocation("TestZone", "Unresolved-991");
                File.WriteAllText(
                    Path.Combine(dataDirectory, "map_991.json"),
                    MapJson(
                        new object[]
                        {
                            Record(0x80, 3, 0, 0, 0, 0),   // no such table
                            Record(0x80, 0, 0, 0, 0, 9),   // no such animation
                            Record(0x00, 0, 0, 0, 0, 0),   // no global table at all in this fixture
                        },
                        Table(new[] { 8 }, 1, Anim(Frame(0x82, 20, 0, Image(0, 0, 8, 8, Corners)), End(1)))));
                WriteSheet(Path.Combine(dataDirectory, "map_991_effectsheet.png"), 16, 16);
                return new List<int> { 991 };
            },
            (outputDirectory, report) =>
            {
                Assert.Equal(3, report.Counters["Effects.UnresolvedRecords"]);
                Assert.Equal(3, report.Errors.Count(error => error.Contains("991", StringComparison.Ordinal)));
            });
    }

    [Fact]
    public void ConvertEffects_ForAMapWithoutEffects_WritesNothing()
    {
        RunConversion((inputDirectory, mapLocations) =>
            {
                mapLocations[992] = new MapLocation("TestZone", "Nothing-992");
                File.WriteAllText(
                    Path.Combine(inputDirectory, "data", "map_992.json"),
                    MapJson(Array.Empty<object>()));
                return new List<int> { 992 };
            },
            (outputDirectory, report) =>
            {
                Assert.Empty(report.Errors);
                Assert.Equal(0, report.Counters.GetValueOrDefault("Effects.Companions"));
                Assert.False(Directory.Exists(Path.Combine(outputDirectory, "Maps", "TestZone", "Nothing-992", "effects")));
                Assert.False(File.Exists(Path.Combine(outputDirectory, "Data", "effects", "effects-global.json")));
            });
    }

    [Fact]
    public void ConvertEffects_IsDeterministic()
    {
        string? first = null;
        for (var run = 0; run < 2; run++)
        {
            RunConversion((inputDirectory, mapLocations) =>
                {
                    var dataDirectory = Path.Combine(inputDirectory, "data");
                    mapLocations[990] = new MapLocation("TestZone", "Effects Map-990");
                    File.WriteAllText(
                        Path.Combine(dataDirectory, "map_990.json"),
                        MapJson(
                            new object[] { Record(0x80, 0, 80, 22, 6, 0) },
                            Table(new[] { 8 }, 1, Anim(Frame(0x8A, 20, 52, Image(0, 0, 8, 8, Corners)), End(1)))));
                    WriteSheet(Path.Combine(dataDirectory, "map_990_effectsheet.png"), 16, 16);
                    return new List<int> { 990 };
                },
                (outputDirectory, report) =>
                {
                    var text = File.ReadAllText(Path.Combine(
                        outputDirectory, "Maps", "TestZone", "Effects Map-990", "effects", "Effects Map-990.effects.json"));
                    first ??= text;
                    Assert.Equal(first, text);
                });
        }
    }

    // ---------------------------------------------------------------- real data (skipped when data-extracted/ is absent)

    [Fact]
    public void Read_Map476_HasTheAuraRecordAndFourAnimations()
    {
        var path = FindRealDataFile("map_476.json");
        if (path is null)
        {
            return;
        }

        var bank = EffectBankReader.Read(path);

        var record = Assert.Single(bank.Records);
        Assert.Equal((0x80, 0, 1), (record.Flags, record.Effect, record.Anim));
        Assert.Equal((80, 22, 6), (record.X, record.Y, record.Z));
        var table = Assert.Single(bank.Tables);
        Assert.Equal(new[] { 111, 16, 21, 32 }, table.Animations.Select(animation => animation.Frames.Count).ToArray());
        Assert.Equal(new[] { "Destroy", "Loop", "Destroy", "Loop" }, table.Animations.Select(animation => animation.End).ToArray());
        Assert.Equal(
            new[] { 222, 32, 34, 64 },
            table.Animations.Select(animation => animation.Frames.Sum(frame => frame[0])).ToArray());
        Assert.All(table.ImageSets, set => Assert.Equal(52, set.Idsv));
        Assert.Empty(bank.Errors);
    }

    [Fact]
    public void Read_Map391_HasFiveRecordsAndFourOneFrameLoops()
    {
        var path = FindRealDataFile("map_391.json");
        if (path is null)
        {
            return;
        }

        var bank = EffectBankReader.Read(path);

        Assert.Equal(5, bank.Records.Count);
        Assert.All(bank.Records, record => Assert.Equal(0xC0, record.Flags));
        var table = Assert.Single(bank.Tables);
        Assert.Equal(4, table.Animations.Count);
        Assert.All(table.Animations, animation =>
        {
            Assert.Equal("Loop", animation.End);
            var frame = Assert.Single(animation.Frames);
            Assert.Equal(10, frame[0]);
        });
    }

    [Fact]
    public void Read_Map163_HasThreeAnimationsAndOnePaddingCase()
    {
        var path = FindRealDataFile("map_163.json");
        if (path is null)
        {
            return;
        }

        var bank = EffectBankReader.Read(path);

        Assert.Equal(4, bank.Records.Count);
        Assert.All(bank.Records, record => Assert.Equal((0xC0, 0, 1), (record.Flags, record.Effect, record.Anim)));
        var table = Assert.Single(bank.Tables);
        Assert.Equal(new[] { "Loop", "Destroy", "Destroy" }, table.Animations.Select(animation => animation.End).ToArray());
        Assert.All(table.Animations, animation => Assert.Equal(10, Assert.Single(animation.Frames)[0]));
        Assert.Equal(1, bank.AnimationSlotsDropped);
    }

    [Fact]
    public void Read_Map161_DropsTheDegenerateImages()
    {
        var path = FindRealDataFile("map_161.json");
        if (path is null)
        {
            return;
        }

        var bank = EffectBankReader.Read(path);

        Assert.Equal(23, bank.ImagesDegenerateDropped);
        Assert.All(
            bank.Tables.SelectMany(table => table.ImageSets).SelectMany(set => set.Images),
            image => Assert.True(image.W > 0 && image.H > 0));
    }

    [Fact]
    public void ConvertEffects_OverTheWholeCorpus_MeetsTheInvariants()
    {
        var dataDirectory = FindRealDataFile("map_476.json");
        var mapsJson = FindRepositoryFile(Path.Combine("alundra-casaengine-project-converter", "maps.json"));
        if (dataDirectory is null || mapsJson is null)
        {
            return;
        }

        var inputDirectory = Path.GetDirectoryName(Path.GetDirectoryName(dataDirectory))!;
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            var mapLocations = MapCatalogReader.Read(mapsJson).Locations;
            EffectWriter.ConvertEffects(inputDirectory, outputDirectory, null, mapLocations, report);

            Assert.Empty(report.Errors);
            Assert.Equal(544, report.Counters["Effects.Records"]);
            Assert.Equal(251, report.Counters["Effects.RecordsSpawnAtLoad"]);
            Assert.Equal(350, report.Counters["Effects.RecordsMapTable"]);
            Assert.Equal(194, report.Counters["Effects.RecordsGlobalTable"]);
            Assert.Equal(165, report.Counters["Effects.Tables"]);
            Assert.Equal(363, report.Counters["Effects.Animations"]);
            Assert.Equal(83, report.Counters["Effects.AnimationSlotsDropped"]);
            Assert.Equal(5148, report.Counters["Effects.Frames"]);
            Assert.Equal(2832, report.Counters["Effects.ImageSets"]);
            Assert.Equal(12307, report.Counters["Effects.Images"]);
            Assert.Equal(23, report.Counters["Effects.ImagesDegenerateDropped"]);
            Assert.Equal(157, report.Counters["Effects.Companions"]);
            Assert.Equal(87, report.Counters["Effects.Sheets"]);
            Assert.Equal(0, report.Counters["Effects.UnresolvedRecords"]);
            Assert.Equal(0, report.Counters.GetValueOrDefault("Assets.Texture"));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // ---------------------------------------------------------------- fixtures

    private static readonly int[] Corners = { -4, -8, 4, -8, -4, 0, 4, 0 };

    private static EffectBank Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return EffectBankReader.Read(document.RootElement);
    }

    private static string MapJson(object[] records, params object[] tables)
        => JsonSerializer.Serialize(new
        {
            SpriteInfo = new { MapEffectRecords = records, SpriteEffectRecords = tables },
        });

    private static object Record(int flags, int effect, int x, int y, int z, int anim) => new
    {
        X1 = 0, X2 = 51, Y1 = 0, Y2 = 59, Flags = flags, EffectId = effect, X = x, Y = y, Z = z, AnimId = anim, U1 = 0, U2 = 0,
    };

    private static object Table(int[] offsets, int count, params object[] anims) => new
    {
        BinOffset = 12345,
        AnimationOffsets = offsets,
        AnimationCount = count,
        EffectId = 32768,
        PreloadedAnims = anims,
    };

    private static object Anim(params object[] frames) => new { NumberOfFrames = frames.Length, Frames = frames };

    private static object Frame(int delay, int pointer, int idsv, params object[] images) => new
    {
        Delay = delay,
        ImageSetPointer = pointer,
        Images = new { ImageSetId = 140737488355348L + pointer, DepthSortValue = idsv, NumberOfImages = images.Length, Images = images },
    };

    private static object End(int delay) => new { Delay = delay, ImageSetPointer = -1, Images = (object?)null };

    private static object Image(int atlasX, int atlasY, int width, int height, int[] corners, int spritesheet = 0x18) => new
    {
        Spritesheet = spritesheet,
        Palette = 0,
        Sx = atlasX,
        Sy = atlasY,
        Swidth = width,
        Sheight = height,
        X1 = corners[0], Y1 = corners[1], X2 = corners[2], Y2 = corners[3],
        X3 = corners[4], Y3 = corners[5], X4 = corners[6], Y4 = corners[7],
        Signature = 34191701442584L,
        AtlasX = atlasX,
        AtlasY = atlasY,
    };

    private static void WriteSheet(string path, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void RunConversion(
        Func<string, Dictionary<int, MapLocation>, List<int>> setup,
        Action<string, ConversionReport> assert)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            Directory.CreateDirectory(Path.Combine(inputDirectory, "data"));
            var mapLocations = new Dictionary<int, MapLocation>();
            var mapFilter = setup(inputDirectory, mapLocations);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            EffectWriter.ConvertEffects(inputDirectory, outputDirectory, mapFilter, mapLocations, report);

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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string? FindRealDataFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "data", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
