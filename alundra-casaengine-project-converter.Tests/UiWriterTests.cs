using System.Text.Json;
using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class UiWriterTests
{
    // The writer copies PNGs verbatim and never decodes them: the 8-byte signature is enough.
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void ConvertUi_NamesWindSpritesByIndexSoIdenticalRectanglesDoNotCollide()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteUiFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            UiWriter.ConvertUi(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(3, report.Counters["Assets.UiSprite"]);

            // Entries 0 and 2 of the fixture describe the exact same rectangle and palette, as
            // several real wind.json entries do; naming by index keeps them two distinct assets.
            var firstPath = Path.Combine(outputDirectory, "UI", "wind_000.sprite");
            var duplicatePath = Path.Combine(outputDirectory, "UI", "wind_002.sprite");
            Assert.True(File.Exists(firstPath));
            Assert.True(File.Exists(duplicatePath));

            var second = new SpriteData();
            second.Load(JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "UI", "wind_001.sprite"))));
            Assert.Equal(8, second.PositionInTexture.X);
            Assert.Equal(56, second.PositionInTexture.Y);
            Assert.Equal(9, second.PositionInTexture.Width);
            Assert.Equal(16, second.PositionInTexture.Height);
            Assert.Equal(4, second.Origin.X); // crop centre, integer-truncated like Phase 3
            Assert.Equal(8, second.Origin.Y);

            // Every sprite points at the one wind.texture wrapper, not at the raw PNG entry.
            var wrapperPath = Path.Combine(outputDirectory, "UI", "Textures", "wind.texture");
            Assert.True(File.Exists(wrapperPath));
            var wrapperDocument = JObject.Parse(File.ReadAllText(wrapperPath));
            Assert.Equal(wrapperDocument["id"]!.ToString(), second.SpriteSheetAssetId.ToString());

            var texture = new CasaEngine.Framework.Assets.Textures.Texture();
            texture.Load(wrapperDocument);
            Assert.Equal(SamplerState.PointClamp.Filter, texture.PreferredSamplerState.Filter);

            // PaletteIndex has nowhere to go in SpriteData, so it must survive in the companion.
            using var companion = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "UI", "wind-sprites.json")));
            var rows = companion.RootElement.EnumerateArray().ToArray();
            Assert.Equal(3, rows.Length);
            Assert.Equal(5, rows[0].GetProperty("palette_index").GetInt32());
            Assert.Equal(7, rows[1].GetProperty("palette_index").GetInt32());
            Assert.Equal("wind_002", rows[2].GetProperty("name").GetString());
            Assert.Equal(second.Id, rows[1].GetProperty("asset_id").GetGuid());

            // wind.png + 1 memory card frame + 2 closing screens + the loading screen.
            Assert.Equal(5, report.Counters["Assets.UiTexture"]);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "closing_01.png")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UI", "Textures", "loading_screen.texture")));
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
    public void ConvertUi_WritesTheLoopingUiAnimationsAtTheOriginalCadence()
    {
        RunConvertUi(inputDirectory => WriteUiFixture(inputDirectory, windEntryCount: 240), (outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.Equal(3, report.Counters["Assets.UiAnimation"]);

            // The cursor: 4 x 10 PSX ticks, and the pixel offset of each phase (Y down, screen pixels).
            var cursor = LoadAnimation(outputDirectory, UiAnimationWriter.InventoryCursorName);
            Assert.Equal(UiAnimationWriter.AnimationId(UiAnimationWriter.InventoryCursorName), cursor.Id);
            Assert.Equal(AnimationType.Loop, cursor.AnimationType);
            var part = Assert.Single(cursor.Parts);
            Assert.Equal(UiWriter.WindSpriteId(159), part.DefaultSpriteId);
            AssertSpriteKeyframes(cursor, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f }, new[] { 159, 182, 210, 237, 237 });
            var positions = cursor.Tracks.Single(track => track.Property == Animation2dTrackProperty.Position).PositionKeyframes;
            Assert.Equal(
                new[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(-1, 1), new Vector2(-1, 0), new Vector2(-1, 0) },
                positions.Select(keyframe => keyframe.Value));
            Assert.Equal(0.8f, cursor.GetDurationSeconds(), 5);

            // The magic pip: 4 x 10 ticks, no offset.
            var pip = LoadAnimation(outputDirectory, UiAnimationWriter.MagicPipName);
            AssertSpriteKeyframes(pip, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f }, new[] { 1, 3, 10, 17, 17 });
            Assert.DoesNotContain(pip.Tracks, track => track.Property == Animation2dTrackProperty.Position);

            // The coin: 4 x 6 ticks.
            var coin = LoadAnimation(outputDirectory, UiAnimationWriter.CoinName);
            AssertSpriteKeyframes(coin, new[] { 0f, 0.12f, 0.24f, 0.36f, 0.48f }, new[] { 126, 130, 134, 139, 139 });
            Assert.Equal(0.48f, coin.GetDurationSeconds(), 5);

            // The sprite ids are the ones the Alundra DLL already names (AlundraInventoryScreen.cs,
            // AlundraHudScreen.cs): wind_159 and wind_001.
            Assert.Equal(Guid.Parse("6ed4380a-ba9c-5d0b-84db-22e1ddf61361"), UiWriter.WindSpriteId(159));
            Assert.Equal(Guid.Parse("eb70224b-d6d5-558e-a1ef-438f072135f8"), UiWriter.WindSpriteId(1));

            var catalog = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "AssetInfos.json")));
            Assert.Contains(
                (JArray)catalog["asset_infos"]!,
                node => node["file_name"]!.ToString() == Path.Combine("UI", "Animations", "ui_hud_coin.anim2d"));
        });
    }

    [Fact]
    public void ConvertUi_WritesTheSameUiAnimationsOnEveryRun()
    {
        var first = new Dictionary<string, byte[]>();
        RunConvertUi(inputDirectory => WriteUiFixture(inputDirectory, windEntryCount: 240), (outputDirectory, _) =>
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(outputDirectory, "UI", "Animations")))
            {
                first[Path.GetFileName(path)] = File.ReadAllBytes(path);
            }
        });

        RunConvertUi(inputDirectory => WriteUiFixture(inputDirectory, windEntryCount: 240), (outputDirectory, _) =>
        {
            var files = Directory.EnumerateFiles(Path.Combine(outputDirectory, "UI", "Animations")).ToList();
            Assert.Equal(3, files.Count);
            foreach (var path in files)
            {
                Assert.Equal(first[Path.GetFileName(path)], File.ReadAllBytes(path));
            }
        });
    }

    [Fact]
    public void ConvertUi_SkipsAnAnimationWhoseSpritesWereNotWritten()
    {
        RunConvertUi(inputDirectory => WriteUiFixture(inputDirectory), (outputDirectory, report) =>
        {
            Assert.Empty(report.Errors);
            Assert.Equal(0, report.Counters["Assets.UiAnimation"]);
            Assert.Equal(3, report.Warnings.Count(warning => warning.Contains("skipped: sprite wind_", StringComparison.Ordinal)));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(outputDirectory, "UI", "Animations")));
        });
    }

    [Fact]
    public void ConvertUi_CataloguesVersionedScreensWithTheirOwnIdAndNeverWritesThem()
    {
        var screenId = Guid.Parse("0d6f1a52-3b8e-4c7a-9f21-5e4b7c9a1d30");
        byte[]? envelopeBytes = null;

        RunConvertUi(
            inputDirectory => WriteUiFixture(inputDirectory),
            (outputDirectory, report) =>
            {
                Assert.Equal(1, report.Counters["Assets.UiScreen"]);
                var error = Assert.Single(report.Errors);
                Assert.Contains("NoId.uiscreen", error, StringComparison.Ordinal);

                var catalog = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "AssetInfos.json")));
                var entry = Assert.Single(
                    (JArray)catalog["asset_infos"]!,
                    node => node["id"]!.ToString() == screenId.ToString());
                Assert.Equal(Path.Combine("UI", "Screens", "Inventory", "InventoryScreen.uiscreen"), entry["file_name"]!.ToString());
                Assert.Equal("InventoryScreen", entry["name"]!.ToString());

                // Only the envelope is an asset: the XAML is a file it names.
                Assert.DoesNotContain(
                    (JArray)catalog["asset_infos"]!,
                    node => node["file_name"]!.ToString().EndsWith(".xaml", StringComparison.Ordinal));

                var envelopePath = Path.Combine(outputDirectory, "UI", "Screens", "Inventory", "InventoryScreen.uiscreen");
                Assert.Equal(envelopeBytes, File.ReadAllBytes(envelopePath));
            },
            outputDirectory =>
            {
                var screensDirectory = Path.Combine(outputDirectory, "UI", "Screens", "Inventory");
                Directory.CreateDirectory(screensDirectory);
                var envelope = new JObject
                {
                    ["id"] = screenId.ToString(),
                    ["name"] = "InventoryScreen",
                    ["source_xaml_file"] = "InventoryScreen.xaml",
                };
                envelopeBytes = System.Text.Encoding.UTF8.GetBytes(envelope.ToString());
                File.WriteAllBytes(Path.Combine(screensDirectory, "InventoryScreen.uiscreen"), envelopeBytes);
                File.WriteAllText(Path.Combine(screensDirectory, "InventoryScreen.xaml"), "<Window />");
                File.WriteAllText(Path.Combine(outputDirectory, "UI", "Screens", "NoId.uiscreen"), "{ \"name\": \"NoId\" }");
            });
    }

    /// <summary>The versioned UI/Screens/DialogueScreen.uiscreen replaces the engine's dialogue box: the project
    /// file names it by the id its envelope carries, and leaves every other setting as Phase 0 wrote it.</summary>
    [Fact]
    public void ConvertUi_PointsTheProjectAtTheVersionedDialogueScreen()
    {
        var screenId = Guid.Parse("5e0c9a27-8f14-4b6d-a3c2-71d9e84f0b16");

        RunConvertUi(
            inputDirectory => WriteUiFixture(inputDirectory),
            (outputDirectory, report) =>
            {
                Assert.Empty(report.Errors);
                var project = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, ProjectWriter.ProjectName + ".json")));
                Assert.Equal(screenId.ToString(), project["DialogueScreenAsset"]!.ToString());
                Assert.Equal(ProjectWriter.GameplayDllName, project["GameplayDllName"]!.ToString());
                Assert.Contains(report.Messages, message => message.Contains("DialogueScreenAsset set", StringComparison.Ordinal));
            },
            outputDirectory =>
            {
                var screensDirectory = Path.Combine(outputDirectory, "UI", "Screens");
                Directory.CreateDirectory(screensDirectory);
                var envelope = new JObject
                {
                    ["id"] = screenId.ToString(),
                    ["name"] = "DialogueScreen",
                    ["source_xaml_file"] = "DialogueScreen.xaml",
                };
                File.WriteAllText(Path.Combine(screensDirectory, "DialogueScreen.uiscreen"), envelope.ToString());
            });
    }

    /// <summary>Without a versioned dialogue screen, the project keeps the engine's built-in dialogue box.</summary>
    [Fact]
    public void ConvertUi_WithoutAVersionedDialogueScreen_LeavesTheSettingUnset()
    {
        RunConvertUi(
            inputDirectory => WriteUiFixture(inputDirectory),
            (outputDirectory, _) =>
            {
                var project = JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, ProjectWriter.ProjectName + ".json")));
                Assert.Null(project["DialogueScreenAsset"]);
            });
    }

    [Fact]
    public void ConvertUi_BalanceKeepsUnknownFieldsAndDropsTheExtractorPath()
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            WriteUiFixture(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            UiWriter.ConvertUi(inputDirectory, outputDirectory, report);

            Assert.Empty(report.Errors);
            Assert.Equal(2, report.Counters["Data.BalanceRecords"]);

            using var balance = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "Data", "balance.json")));
            var root = balance.RootElement;

            // FileName is the absolute path of the .BIN on the extractor's machine: provenance, not
            // game data, and keeping it would make the output machine-dependent.
            Assert.False(root.TryGetProperty("FileName", out _));

            // Everything else keeps its original (unknown-meaning) name and structure.
            Assert.True(root.TryGetProperty("Offsets", out var offsets));
            Assert.Equal(2, offsets.GetArrayLength());

            var firstRecord = root.GetProperty("BalanceRecords")[0];
            Assert.Equal(255, firstRecord.GetProperty("Level").GetInt32());
            Assert.Equal(169, firstRecord.GetProperty("OffsetToNextLevel").GetInt32());
            Assert.Equal(3, firstRecord.GetProperty("Values").GetArrayLength());
            Assert.Equal(2, firstRecord.GetProperty("NumAnimVals").GetInt32());
            Assert.Equal(133, firstRecord.GetProperty("AnimVals")[1].GetProperty("Val").GetInt32());
            Assert.Equal(2, firstRecord.GetProperty("AnimVals")[1].GetProperty("U2").GetInt32());

            Assert.Contains(report.Messages, message => message.Contains("FileName", StringComparison.Ordinal));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(inputDirectory, recursive: true);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    /// <summary>Runs ProjectWriter.CreateEmptyProject then UiWriter.ConvertUi on a fresh fixture, and hands the output
    /// directory and report to <paramref name="assert"/>. <paramref name="prepareOutput"/> runs after the project
    /// was created and before the conversion: where a file versioned in the project is put in place.</summary>
    private static void RunConvertUi(
        Action<string> writeInput, Action<string, ConversionReport> assert, Action<string>? prepareOutput = null)
    {
        var inputDirectory = CreateTempDirectory();
        var outputDirectory = CreateTempDirectory();
        var previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            writeInput(inputDirectory);

            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var report = new ConversionReport();
            ProjectWriter.CreateEmptyProject(outputDirectory, report);
            prepareOutput?.Invoke(outputDirectory);
            UiWriter.ConvertUi(inputDirectory, outputDirectory, report);

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

    private static Animation2dData LoadAnimation(string outputDirectory, string name)
    {
        var animation = new Animation2dData();
        animation.Load(JObject.Parse(File.ReadAllText(Path.Combine(outputDirectory, "UI", "Animations", name + ".anim2d"))));
        return animation;
    }

    private static void AssertSpriteKeyframes(Animation2dData animation, float[] expectedTimes, int[] expectedWindIndices)
    {
        var keyframes = animation.Tracks.Single(track => track.Property == Animation2dTrackProperty.Sprite).SpriteKeyframes;
        Assert.Equal(expectedTimes.Length, keyframes.Count);
        for (var i = 0; i < expectedTimes.Length; i++)
        {
            Assert.Equal(expectedTimes[i], keyframes[i].TimeSeconds, 5);
            Assert.Equal(UiWriter.WindSpriteId(expectedWindIndices[i]), keyframes[i].Value);
        }
    }

    /// <summary>The fixture below with a wind.json of <paramref name="windEntryCount"/> distinct entries instead of
    /// three: enough to reach the sprites the UI animations name (up to wind_237).</summary>
    private static void WriteUiFixture(string inputDirectory, int windEntryCount)
    {
        WriteUiFixture(inputDirectory);

        var entries = Enumerable.Range(0, windEntryCount)
            .Select(index => $"{{ \"U0\": {index % 32 * 8}, \"V0\": {index / 32 * 8}, \"Width\": 8, \"Height\": 8, \"PaletteIndex\": 0 }}");
        File.WriteAllText(Path.Combine(inputDirectory, "ui", "wind.json"), "[" + string.Join(",", entries) + "]");
    }

    private static void WriteUiFixture(string inputDirectory)
    {
        var uiDirectory = Path.Combine(inputDirectory, "ui");
        var dataDirectory = Path.Combine(inputDirectory, "data");
        var memoryCardDirectory = Path.Combine(inputDirectory, "memorycard");
        var closingDirectory = Path.Combine(inputDirectory, "closing");
        Directory.CreateDirectory(uiDirectory);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(memoryCardDirectory);
        Directory.CreateDirectory(closingDirectory);

        File.WriteAllBytes(Path.Combine(uiDirectory, "wind.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(dataDirectory, "loading_screen.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(memoryCardDirectory, "memorycardframe1.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(closingDirectory, "closing_00.png"), FakePngBytes);
        File.WriteAllBytes(Path.Combine(closingDirectory, "closing_01.png"), FakePngBytes);

        // Entries 0 and 2 are byte-identical records: the real table does this too.
        File.WriteAllText(
            Path.Combine(uiDirectory, "wind.json"),
            """
            [
                { "U0": 0, "V0": 40, "Width": 8, "Height": 16, "PaletteIndex": 5 },
                { "U0": 8, "V0": 56, "Width": 9, "Height": 16, "PaletteIndex": 7 },
                { "U0": 0, "V0": 40, "Width": 8, "Height": 16, "PaletteIndex": 5 }
            ]
            """);

        File.WriteAllText(
            Path.Combine(dataDirectory, "BALANCE.BIN.json"),
            """
            {
                "FileName": "D:\\development\\repo\\Alundra Remake\\DATA\\BALANCE.BIN",
                "BalanceRecords": [
                    {
                        "Level": 255,
                        "OffsetToNextLevel": 169,
                        "Hp": 10,
                        "Values": [ 0, 0, 0 ],
                        "NumAnimVals": 2,
                        "AnimVals": [ { "Val": 0, "U2": 0 }, { "Val": 133, "U2": 2 } ],
                        "Offset": 0,
                        "Next": 169
                    },
                    {
                        "Level": 1,
                        "OffsetToNextLevel": 42,
                        "Hp": 20,
                        "Values": [ 1, 2, 3 ],
                        "NumAnimVals": 0,
                        "AnimVals": [],
                        "Offset": 169,
                        "Next": 211
                    }
                ],
                "Offsets": [ 0, 169 ]
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
