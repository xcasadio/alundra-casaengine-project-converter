#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f2b1c T2 (docs/plan-e19-opcodes.md, F2B1C-R2): the view model of the text box screen, without any display. For each pass of the six scenarios of the annex
/// (<c>docs/plan-e19-f2b1-annexe/values/S1.csv</c> to <c>S6.csv</c>, the same tables <see cref="AlundraTextBoxDrawnStateTests"/> pins the box to) the properties
/// <see cref="AlundraTextBoxViewModel.Apply"/> writes are the ones the rule deduces from the table: <c>Frame.Top</c> the Y of the frame, <c>Clip</c> the clip of the
/// pass, <c>Row r</c> the text of the row with <c>Left = x - 32</c> and <c>Top = rowY - ClipTop</c> (relative to the clip canvas), the cursor by its sprite name. The
/// pass that ends a scroll is read as F2B1B-1 pins it (the rows after the shift at offset 0, D-E19-83).
/// </summary>
public sealed class AlundraTextBoxViewModelTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;
    private const string Br = "[br trimwhitespace=false/]";

    private static readonly string[] CursorNames = { "wind_150", "wind_173", "wind_201", "wind_228" };

    public AlundraTextBoxViewModelTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        Director.ResetForTests();
        Director.Box.DrawsPreShiftRowsAtScrollEnd = AlundraDialogueBox.ScrollEndDrawsPreShiftRows; // a test of the variant leaves the box's flag on
        Director.AdvanceProviderForTests = null;
        AlundraDialogueCapturePresenter.ResetForTests();
        AlundraFont3Advances.ResetForTests();
    }

    // ---- the tables (a reduced reader of the annex CSV: the columns the rule reads) --------------------------------------------

    private sealed record TableRow(int Pass, bool Drawn, int FrameY, int ClipTop, int ClipHeight, int Offset, bool Ending, (string Text, int X, int Y)[] Rows, int? CursorImage);

    private static string ValuesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f2b1-annexe", "values");
            if (File.Exists(Path.Combine(candidate, "S1.csv")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("docs/plan-e19-f2b1-annexe/values not found above " + AppContext.BaseDirectory);
    }

    private static List<TableRow> LoadTable(string name)
    {
        var rows = new List<TableRow>();
        foreach (var line in File.ReadAllLines(Path.Combine(ValuesDirectory(), name + ".csv")).Skip(1))
        {
            var c = line.Split(';');
            Assert.Equal(25, c.Length);
            var pass = int.Parse(c[0]);
            if (c[2] == "0")
            {
                rows.Add(new TableRow(pass, false, 0, 0, 0, 0, false, Array.Empty<(string, int, int)>(), null));
                continue;
            }

            var cells = new (string Text, int X, int Y)[3];
            for (var r = 0; r < 3; r++)
            {
                cells[r] = (c[12 + 3 * r], int.Parse(c[13 + 3 * r]), int.Parse(c[14 + 3 * r]));
            }

            rows.Add(new TableRow(pass, true, int.Parse(c[4]), int.Parse(c[7]), int.Parse(c[9]), int.Parse(c[10]), c[11] == "1", cells, c[21].Length > 0 ? int.Parse(c[21]) : null));
        }

        return rows;
    }

    // ---- the scenarios (the pages of AlundraTextBoxDrawnStateTests) ---------------------------------------------------------------

    private sealed record Scenario(string[] Pages, Func<int, (bool Held, bool Pressed)> Pad);

    private static (bool, bool) NoButton(int pass) => (false, false);

    private static (bool, bool) EveryPass(int pass) => (true, true);

    private static (bool, bool) PressesAt60And120(int pass) => (pass is 60 or 120, pass is 60 or 120);

    private static Scenario Of(string name) => name switch
    {
        "S1" => new Scenario(new[] { "AB" }, NoButton),
        "S2" => new Scenario(new[] { $"Bonjour, Alundra !{Br}deuxième ligne" }, NoButton),
        "S3" => new Scenario(new[]
        {
            "[voice id=-1 trimwhitespace=false/][center trimwhitespace=false/]Roue de la fortune !" + Br
            + "[center trimwhitespace=false/]Florin[glyph id=21 trimwhitespace=false/]Roulette        [empty trimwhitespace=false/]",
        }, NoButton),
        "S4" => new Scenario(new[] { $"gypjq{Br}deux{Br}trois{Br}quatre" }, NoButton),
        "S5" => new Scenario(new[] { $"l1{Br}l2{Br}gypjq" }, EveryPass),
        "S6" => new Scenario(new[] { "a", "b", "c" }, PressesAt60And120),
        _ => throw new ArgumentException(name),
    };

    public static IEnumerable<object[]> ScenarioNames() => new[] { "S1", "S2", "S3", "S4", "S5", "S6" }.Select(n => new object[] { n });

    private static void OpenTheBox(string name)
    {
        Director.AdvanceProviderForTests = c => AlundraTextBoxOracle.GlyphWidth(OraclePages.ByteOf(c));
        Director.AttachToWorld(new DialogueService(), new AlundraGameState());
        Director.InstallForMapEntry();
        Director.Open(DialogueTestAssets.BuildRaw("vm-" + name, "Start", Of(name).Pages), "Start", controlMode: 1);
    }

    private static void AssertRowsAreSingleLines(AlundraTextBoxViewModel vm, string where)
    {
        for (var r = 0; r < 3; r++)
        {
            var text = vm.Row(r).Text;
            Assert.True(text.IndexOfAny(new[] { '\n', '\r' }) < 0, $"{where}: row {r} holds a line break");
        }
    }

    // ---- the rule against the tables ----------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void TheViewModel_ReadsTheDrawnStateOfEveryPass_AsTheRuleSays(string name)
    {
        var scenario = Of(name);
        var table = LoadTable(name);
        OpenTheBox(name);
        var vm = new AlundraTextBoxViewModel();

        Assert.Equal(Visibility.Collapsed, vm.RootVisibility); // before the first pass, nothing is drawn
        vm.Apply(Director.Box);
        Assert.Equal(Visibility.Collapsed, vm.RootVisibility);

        var cursorsSeen = 0;
        foreach (var t in table.Skip(1))
        {
            var (held, pressed) = scenario.Pad(t.Pass);
            Director.Pass(held, pressed);
            vm.Apply(Director.Box);
            var where = $"{name} pass {t.Pass}";
            if (!t.Drawn)
            {
                Assert.True(vm.RootVisibility == Visibility.Collapsed, $"{where}: the root is {vm.RootVisibility}, the box is not drawn");
                continue;
            }

            Assert.True(vm.RootVisibility == Visibility.Visible, $"{where}: the root is {vm.RootVisibility}, the box is drawn");
            Assert.True(vm.Frame.Top == t.FrameY, $"{where}: Frame.Top {vm.Frame.Top}, table {t.FrameY}");
            Assert.True((vm.Clip.Top, vm.Clip.Height) == (t.ClipTop, t.ClipHeight), $"{where}: clip ({vm.Clip.Top}, {vm.Clip.Height}), table ({t.ClipTop}, {t.ClipHeight})");

            // The pass that ends a scroll: the rows after the shift at offset 0 (the old rows 1 and 2, then an empty one).
            (string Text, int X, int Y)[] rows = t.Ending
                ? new (string Text, int X, int Y)[] { (t.Rows[1].Text, 32, t.FrameY + 5), (t.Rows[2].Text, 32, t.FrameY + 5 + 16), (string.Empty, 32, t.FrameY + 5 + 32) }
                : t.Rows;
            for (var r = 0; r < 3; r++)
            {
                var row = vm.Row(r);
                var expected = (rows[r].Text, rows[r].X - 32, rows[r].Y - t.ClipTop);
                var actual = (Shown(row.Text), row.Left, row.Top);
                Assert.True(expected == actual, $"{where}: row {r} {actual}, rule {expected}");
            }

            AssertRowsAreSingleLines(vm, where);

            if (t.CursorImage is { } image)
            {
                cursorsSeen++;
                Assert.True(vm.Cursor.Visibility == Visibility.Visible, $"{where}: the cursor is {vm.Cursor.Visibility}, the table has image {image}");
                Assert.True(vm.Cursor.SourceName == CursorNames[image], $"{where}: cursor {vm.Cursor.SourceName}, table {CursorNames[image]}");
                Assert.True(vm.Cursor.Top == t.FrameY + 32, $"{where}: cursor top {vm.Cursor.Top}, table {t.FrameY + 32}");
            }
            else
            {
                Assert.True(vm.Cursor.Visibility == Visibility.Collapsed, $"{where}: the cursor is {vm.Cursor.Visibility}, the table has none");
            }
        }

        Assert.Equal(name == "S6", cursorsSeen > 0); // only the \A scenario shows a cursor
    }

    private static string Shown(string text) =>
        string.Concat(text.Select(c => (c >= 0x20 && c < 0x7F) || (c >= 0xA0 && c <= 0xFF) ? c.ToString() : $"<{(int)c}>"));

    // ---- the written values --------------------------------------------------------------------------------------------------------

    private static void RunTo(int pass)
    {
        for (var i = 0; i < pass; i++)
        {
            Director.Pass(false, false);
        }
    }

    [Fact]
    public void S4_OnItsOnlyEndingPass_100_TheRowsAreTheOnesAfterTheShift()
    {
        OpenTheBox("S4");
        RunTo(100);
        var vm = new AlundraTextBoxViewModel();
        vm.Apply(Director.Box);

        Assert.Equal(Visibility.Visible, vm.RootVisibility);
        Assert.Equal(("deux", 0, 1), (vm.Row0.Text, vm.Row0.Left, vm.Row0.Top));
        Assert.Equal(("trois", 0, 17), (vm.Row1.Text, vm.Row1.Left, vm.Row1.Top));
        Assert.Equal((string.Empty, 0, 33), (vm.Row2.Text, vm.Row2.Left, vm.Row2.Top));
    }

    [Fact]
    public void S4_OnItsEndingPass_WithTheVariantOfTheConstant_TheRowsAreTheOnesBeforeTheShift()
    {
        OpenTheBox("S4");
        Director.Box.DrawsPreShiftRowsAtScrollEnd = true;
        try
        {
            RunTo(100);
            var vm = new AlundraTextBoxViewModel();
            vm.Apply(Director.Box);

            Assert.Equal(("gypjq", 0, -15), (vm.Row0.Text, vm.Row0.Left, vm.Row0.Top));
            Assert.Equal(("deux", 0, 1), (vm.Row1.Text, vm.Row1.Left, vm.Row1.Top));
            Assert.Equal(("trois", 0, 17), (vm.Row2.Text, vm.Row2.Left, vm.Row2.Top));
        }
        finally
        {
            Director.Box.DrawsPreShiftRowsAtScrollEnd = AlundraDialogueBox.ScrollEndDrawsPreShiftRows;
        }
    }

    // ---- the cursor, the centred rows, the closed box ---------------------------------------------------------------------------------

    [Fact]
    public void TheFourCursorSprites_AreNamedByTheirImage_AndTheCursorIsCollapsedWhenNoneIsPosed()
    {
        OpenTheBox("S6");
        var vm = new AlundraTextBoxViewModel();
        var seen = new Dictionary<int, string?>();
        for (var pass = 1; pass <= 125; pass++)
        {
            var (held, pressed) = Of("S6").Pad(pass);
            Director.Pass(held, pressed);
            vm.Apply(Director.Box);
            var image = Director.Box.CursorImage;
            Assert.Equal(image >= 0 ? Visibility.Visible : Visibility.Collapsed, vm.Cursor.Visibility);
            if (image >= 0)
            {
                seen[image] = vm.Cursor.SourceName;
            }
        }

        Assert.Equal(new Dictionary<int, string?> { [0] = "wind_150", [1] = "wind_173", [2] = "wind_201", [3] = "wind_228" }, seen);
    }

    [Fact]
    public void ACentredRow_IsPlacedByItsWidth_RelativeToTheClipCanvas()
    {
        OpenTheBox("S3");
        RunTo(195);
        var vm = new AlundraTextBoxViewModel();
        vm.Apply(Director.Box);

        // pixels.md "Centred lines (pass 195)": row 0 at x 104, row 1 at x 97 (the canvas is at x 32).
        Assert.Equal(("Roue de la fortune !", 72, 1), (vm.Row0.Text, vm.Row0.Left, vm.Row0.Top));
        Assert.Equal(65, vm.Row1.Left);
        Assert.Equal(17, vm.Row1.Top);
        Assert.Equal((string.Empty, 0, 33), (vm.Row2.Text, vm.Row2.Left, vm.Row2.Top));
    }

    [Fact]
    public void TheRootIsVisibleExactlyWhileTheBoxIsDrawn_AndTheFirstSlideInPassIsDrawnUnderTheScreen()
    {
        OpenTheBox("S1");
        var vm = new AlundraTextBoxViewModel();
        vm.Apply(Director.Box);
        Assert.False(Director.Box.Drawn);
        Assert.Equal(Visibility.Collapsed, vm.RootVisibility);

        Director.Pass(false, false); // pass 1: the frame is at Y 240, below the screen, and the root is visible
        vm.Apply(Director.Box);
        Assert.True(Director.Box.Drawn);
        Assert.Equal(Visibility.Visible, vm.RootVisibility);
        Assert.Equal(240, vm.Frame.Top);
    }

    // ---- notification ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ApplyingTheSameStateTwice_NotifiesNothingTheSecondTime()
    {
        OpenTheBox("S2");
        RunTo(60);
        var vm = new AlundraTextBoxViewModel();
        vm.Apply(Director.Box);

        var notifications = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => notifications.Add(e.PropertyName);
        foreach (var model in new INotifyPropertyChanged[] { vm, vm.Frame, vm.Clip, vm.Row0, vm.Row1, vm.Row2, vm.Cursor, vm.NameBox, vm.Portrait })
        {
            model.PropertyChanged += handler;
        }

        vm.Apply(Director.Box);
        Assert.Empty(notifications);

        RunTo(4); // a glyph more
        vm.Apply(Director.Box);
        Assert.NotEmpty(notifications);
    }

    // ================================================================================================================================
    // E19.f4c2 (docs/plan-e19-opcodes.md, F4C2-R2 and R3): the speaker. The name box and the portrait of the dialogue are written into the view model by the presenter, from
    // the machines of the director, after each pass; the mapping is checked image by image against the value tables of the f4 annex (S1 to S9) and, for the pinned images, against
    // the states the f4c annex predicted (states.json: Brightness = Rgb / 128 included).
    // ================================================================================================================================

    private sealed class SpeakerScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Modal;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(Microsoft.Xna.Framework.GameTime gameTime) { }
        public IEnumerable<MGWindow> GetWindows() => Array.Empty<MGWindow>();
    }

    private sealed record Dlg(int Opcode, AlundraEntityScriptProxy Owner, int[] Codes);

    private sealed record Scene(string File, string Prefix, Dlg[] Dialogues, int Frames, Action<int>? Move, AlundraEntityScriptProxy Speaker);

    private const string JessPortraitId = "56f5809a-a47b-564e-b7f7-a66be1f98b04";
    private const string MimingPortraitId = "bf75c68e-424f-57c9-8f6a-535f33d769d7";

    private static AlundraEntityScriptProxy Spk(int spriteType, bool portrait, int height = 56, string? id = null)
    {
        var speaker = SpeakerRig.Speaker(spriteType, portrait, height);
        if (portrait)
        {
            speaker.DialoguePortrait = new DialoguePortraitRef(Guid.Parse(id ?? (height == 72 ? MimingPortraitId : JessPortraitId)), 48, height);
        }

        return speaker;
    }

    private static Dlg D0D(AlundraEntityScriptProxy owner, int text = SpeakerRig.TextAb) => new(0x0D, owner, SpeakerRig.Codes0D(text));

    /// <summary>The scenes of the f4 annex; <paramref name="pinned"/>: the scenes as the f4c annex generated its states (S8 is Miming, name 0x17A).</summary>
    private static Scene SceneOf(string name, SpeakerRig rig, bool pinned = false)
    {
        switch (name)
        {
            case "S1":
            {
                var s = Spk(0x104, true);
                return new Scene("values.json", "S1 ", new[] { D0D(s) }, 60, null, s);
            }
            case "S2":
            {
                var s = Spk(0xFF, true);
                return new Scene("values.json", "S2 ", new[] { D0D(s) }, 60, null, s);
            }
            case "S3":
            {
                var s = Spk(0x10C, false);
                return new Scene("values.json", "S3 ", new[] { D0D(s) }, 60, null, s);
            }
            case "S4":
            {
                var s = Spk(0xFF, false);
                return new Scene("values.json", "S4 ", new[] { D0D(s) }, 60, null, s);
            }
            case "S5":
            {
                var s = Spk(0x104, true);
                return new Scene("values.json", "S5 ", new[] { D0D(s), D0D(s, SpeakerRig.TextCd) }, 110, null, s);
            }
            case "S6":
            {
                var s = Spk(0x104, true);
                return new Scene("values.json", "S6 ", new[] { new Dlg(0xC4, s, SpeakerRig.CodesC4(0x80, 0x10C, SpeakerRig.TextAb)) }, 60, null, s);
            }
            case "S7":
            {
                var s = Spk(0x104, true);
                return new Scene("values.json", "S7 ", new[] { new Dlg(0x5C, s, SpeakerRig.Codes5C(0x05, SpeakerRig.TextAb)) }, 60, null, s);
            }
            case "S8":
            {
                var s = Spk(pinned ? 0x17A : 0x104, true, 72);
                return new Scene("values-s8-72.json", "S8 ", new[] { D0D(s) }, 60, null, s);
            }
            case "S9":
            {
                var s = Spk(0x104, true);
                return new Scene("values-s9-moving.json", "S9 ", new[] { D0D(s) }, 60, f =>
                {
                    if (f >= 4)
                    {
                        s.PosX = (200 + 2 * (f - 3)) << 16;
                        rig.Camera = (40 + (f - 3), 20);
                    }
                }, s);
            }
            default:
                throw new ArgumentException(name);
        }
    }

    /// <summary>The loop of the montage: per image, the pass of the director, the tick of the presenter (what the world proxy does after each pass), then the script phase (the opcode of the
    /// pending dialogue on a fresh state, from image 3). <paramref name="afterImage"/> sees the view model as the pass of that image left it.</summary>
    private static void Drive(SpeakerRig rig, AlundraTextBoxPresenter presenter, Scene scene, Action<int> afterImage)
    {
        var next = 0;
        for (var f = 0; f < scene.Frames; f++)
        {
            scene.Move?.Invoke(f);
            rig.Pass();
            presenter.Tick();
            if (next < scene.Dialogues.Length && f >= 3)
            {
                var dialogue = scene.Dialogues[next];
                if (rig.Run(dialogue.Owner, dialogue.Codes))
                {
                    next++;
                }
            }

            afterImage(f);
        }
    }

    private static JsonElement[] Table(string file, string titlePrefix)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SpeakerAnnex.File_(file)));
        var property = document.RootElement.EnumerateObject().Single(p => p.Name.StartsWith(titlePrefix, StringComparison.Ordinal));
        return property.Value.EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    [Theory]
    [InlineData("S1")]
    [InlineData("S2")]
    [InlineData("S3")]
    [InlineData("S4")]
    [InlineData("S5")]
    [InlineData("S6")]
    [InlineData("S7")]
    [InlineData("S8")]
    [InlineData("S9")]
    public void ThePresenter_WritesTheNameAndThePortrait_ImageByImage_AsTheTablesOfTheBinarySay(string name)
    {
        using var rig = new SpeakerRig();
        var scene = SceneOf(name, rig);
        var table = Table(scene.File, scene.Prefix);
        var names = SpeakerAnnex.Names();
        var viewModel = new AlundraTextBoxViewModel();
        var presenter = new AlundraTextBoxPresenter(rig.Director, viewModel, new SpeakerScreen(), null);
        var portraitRef = scene.Speaker.DialoguePortrait;
        var diffs = new List<string>();
        var namesSeen = 0;
        var portraitsSeen = 0;

        Drive(rig, presenter, scene, f =>
        {
            var row = table[f];
            var where = $"{name} image {f} (N{f - 3:+0;-0;+0})";
            var nameRow = row.GetProperty("name");
            var portraitRow = row.GetProperty("portrait");
            var anything = nameRow.ValueKind != JsonValueKind.Null || portraitRow.ValueKind != JsonValueKind.Null || rig.Director.Box.Drawn;

            if (!anything)
            {
                if (viewModel.RootVisibility != Visibility.Collapsed)
                {
                    diffs.Add($"{where}: nothing is drawn, the root is {viewModel.RootVisibility}");
                }

                return;
            }

            if (viewModel.RootVisibility != Visibility.Visible)
            {
                diffs.Add($"{where}: something is drawn, the root is {viewModel.RootVisibility}");
            }

            if (rig.Director.Box.Drawn != (viewModel.Frame.Visibility == Visibility.Visible))
            {
                diffs.Add($"{where}: the box is drawn {rig.Director.Box.Drawn}, Frame.Visibility {viewModel.Frame.Visibility}");
            }

            if (nameRow.ValueKind == JsonValueKind.Null)
            {
                if (viewModel.NameBox.Visibility != Visibility.Collapsed)
                {
                    diffs.Add($"{where}: no name is drawn, NameBox.Visibility {viewModel.NameBox.Visibility}");
                }
            }
            else
            {
                namesSeen++;
                var expected = (nameRow.GetProperty("frame")[0].GetInt32(), nameRow.GetProperty("text")[0].GetInt32(), names[rig.Director.NameBox.NameId].Name, Visibility.Visible);
                var actual = (viewModel.NameBox.Left ?? int.MinValue, viewModel.NameBox.TextLeft ?? int.MinValue, viewModel.NameBox.Text, viewModel.NameBox.Visibility);
                if (expected != actual)
                {
                    diffs.Add($"{where}: name {actual}, table {expected}");
                }
            }

            if (portraitRow.ValueKind == JsonValueKind.Null)
            {
                if (viewModel.Portrait.Visibility != Visibility.Collapsed)
                {
                    diffs.Add($"{where}: no portrait is drawn, Portrait.Visibility {viewModel.Portrait.Visibility}");
                }
            }
            else
            {
                portraitsSeen++;
                var x = portraitRow.GetProperty("x").GetInt32();
                var y = portraitRow.GetProperty("y").GetInt32();
                var w = portraitRow.GetProperty("w").GetInt32();
                var h = portraitRow.GetProperty("h").GetInt32();
                var rgb = portraitRow.GetProperty("rgb").GetInt32();
                var expected = (new Vector2(x - 8, y - 116), new Vector2(w / 48f, h / (float)portraitRef!.Value.Height), rgb / 128f, (string?)portraitRef.Value.SpriteAssetId.ToString("D"), Visibility.Visible);
                var actual = (viewModel.Portrait.Translation, viewModel.Portrait.Scale, viewModel.Portrait.Brightness, viewModel.Portrait.SourceName, viewModel.Portrait.Visibility);
                if (!expected.Equals(actual))
                {
                    diffs.Add($"{where}: portrait {actual}, table {expected}");
                }
            }
        });

        Assert.True(diffs.Count == 0, $"{name}: {diffs.Count} images differ from the table; first ones:\n{string.Join("\n", diffs.Take(8))}");
        if (name is "S1" or "S2" or "S5" or "S6" or "S8" or "S9")
        {
            Assert.True(portraitsSeen > 0, $"{name}: the table draws a portrait, none was seen");
        }

        if (name is "S1" or "S3" or "S5" or "S6" or "S8" or "S9")
        {
            Assert.True(namesSeen > 0, $"{name}: the table draws a name, none was seen");
        }
    }

    [Fact]
    public void ThePinnedImages_HaveTheStatesOfTheAnnex_BrightnessIncluded()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(F4cAnnex.File_("states.json")));
        var states = document.RootElement.EnumerateObject().Select(p => (p.Name, State: p.Value.Clone())).ToList();
        Assert.Equal(53, states.Count);

        foreach (var scenario in states.Select(s => s.State.GetProperty("scenario").GetString()!).Distinct())
        {
            using var rig = new SpeakerRig();
            var scene = SceneOf(scenario, rig, pinned: true);
            var viewModel = new AlundraTextBoxViewModel();
            var presenter = new AlundraTextBoxPresenter(rig.Director, viewModel, new SpeakerScreen(), null);
            var wanted = states.Where(s => s.State.GetProperty("scenario").GetString() == scenario).GroupBy(s => s.State.GetProperty("frame").GetInt32()).ToDictionary(g => g.Key, g => g.First());

            Drive(rig, presenter, scene, f =>
            {
                if (!wanted.TryGetValue(f, out var entry))
                {
                    return;
                }

                AssertState(viewModel, entry.State, $"{scenario} {entry.Name}");
            });
        }
    }

    private static void AssertState(AlundraTextBoxViewModel vm, JsonElement state, string where)
    {
        Assert.True(vm.RootVisibility == (state.GetProperty("root").GetString() == "Visible" ? Visibility.Visible : Visibility.Collapsed), $"{where}: root {vm.RootVisibility}");
        if (state.GetProperty("root").GetString() != "Visible")
        {
            return;
        }

        if (state.GetProperty("textBox").GetBoolean())
        {
            Assert.True(vm.Frame.Visibility == Visibility.Visible && vm.Clip.Visibility == Visibility.Visible, $"{where}: the text box is drawn, Frame {vm.Frame.Visibility}, Clip {vm.Clip.Visibility}");
            Assert.True(vm.Frame.Top == state.GetProperty("frameTop").GetInt32(), $"{where}: Frame.Top {vm.Frame.Top}");
            Assert.True((vm.Clip.Top, vm.Clip.Height) == (state.GetProperty("clipTop").GetInt32(), state.GetProperty("clipHeight").GetInt32()), $"{where}: clip ({vm.Clip.Top}, {vm.Clip.Height})");
            var rows = state.GetProperty("rows");
            for (var r = 0; r < rows.GetArrayLength(); r++)
            {
                var expected = (rows[r].GetProperty("text").GetString(), rows[r].GetProperty("left").GetInt32(), rows[r].GetProperty("top").GetInt32());
                var actual = (vm.Row(r).Text, vm.Row(r).Left ?? int.MinValue, vm.Row(r).Top ?? int.MinValue);
                Assert.True(expected == actual, $"{where}: row {r} {actual}, state {expected}");
            }

            var cursor = state.GetProperty("cursor");
            if (cursor.ValueKind == JsonValueKind.Null)
            {
                Assert.True(vm.Cursor.Visibility == Visibility.Collapsed, $"{where}: cursor {vm.Cursor.Visibility}");
            }
            else
            {
                Assert.True((vm.Cursor.Visibility, vm.Cursor.SourceName, vm.Cursor.Top) == (Visibility.Visible, cursor.GetProperty("src").GetString(), cursor.GetProperty("top").GetInt32()), $"{where}: cursor {vm.Cursor.SourceName}");
            }
        }
        else
        {
            Assert.True(
                (vm.Frame.Visibility, vm.Clip.Visibility, vm.Cursor.Visibility) == (Visibility.Collapsed, Visibility.Collapsed, Visibility.Collapsed),
                $"{where}: only the speaker is drawn, Frame {vm.Frame.Visibility}, Clip {vm.Clip.Visibility}, Cursor {vm.Cursor.Visibility}");
        }

        if (state.TryGetProperty("nameBox", out var name))
        {
            var expected = (name.GetProperty("left").GetInt32(), name.GetProperty("textLeft").GetInt32(), name.GetProperty("text").GetString(), Visibility.Visible);
            var actual = (vm.NameBox.Left ?? int.MinValue, vm.NameBox.TextLeft ?? int.MinValue, vm.NameBox.Text, vm.NameBox.Visibility);
            Assert.True(expected == actual, $"{where}: name {actual}, state {expected}");
        }
        else
        {
            Assert.True(vm.NameBox.Visibility == Visibility.Collapsed, $"{where}: no name, NameBox.Visibility {vm.NameBox.Visibility}");
        }

        if (state.TryGetProperty("portrait", out var portrait))
        {
            Assert.True(vm.Portrait.Visibility == Visibility.Visible, $"{where}: Portrait.Visibility {vm.Portrait.Visibility}");
            Assert.True(vm.Portrait.SourceName == portrait.GetProperty("src").GetString(), $"{where}: source {vm.Portrait.SourceName}");
            AssertNear(vm.Portrait.Translation.X, portrait.GetProperty("tx").GetDouble(), where + " tx");
            AssertNear(vm.Portrait.Translation.Y, portrait.GetProperty("ty").GetDouble(), where + " ty");
            AssertNear(vm.Portrait.Scale.X, portrait.GetProperty("sx").GetDouble(), where + " sx");
            AssertNear(vm.Portrait.Scale.Y, portrait.GetProperty("sy").GetDouble(), where + " sy");
            AssertNear(vm.Portrait.Brightness, portrait.GetProperty("brightness").GetDouble(), where + " brightness");
        }
        else
        {
            Assert.True(vm.Portrait.Visibility == Visibility.Collapsed, $"{where}: no portrait, Portrait.Visibility {vm.Portrait.Visibility}");
        }
    }

    private static void AssertNear(double actual, double expected, string what) => Assert.True(Math.Abs(actual - expected) < 1e-6, $"{what}: {actual}, state {expected}");

    // ---- the pieces ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Brightness_DefaultsToOne_AndNotifiesOnlyOnAChange()
    {
        var portrait = new InventoryPortraitViewModel();
        var notifications = new List<string?>();
        portrait.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        Assert.Equal(1f, portrait.Brightness);
        portrait.Brightness = 1f;
        Assert.Empty(notifications);
        portrait.Brightness = 255f / 128f;
        Assert.Equal(new[] { nameof(InventoryPortraitViewModel.Brightness) }, notifications);
        Assert.Equal(255f / 128f, portrait.Brightness);
        portrait.Brightness = 255f / 128f;
        Assert.Single(notifications);
    }

    [Fact]
    public void ApplyDialogue_WritesTheQuadOfThePassAsTheInventoryDoes_WithTheDialoguesRestAndTheImagesOwnHeight()
    {
        var jess = new DialoguePortraitRef(Guid.Parse(JessPortraitId), 48, 56);
        var portrait = new AlundraInventoryPortrait();
        var vm = new InventoryPortraitViewModel();

        vm.ApplyDialogue(portrait, jess); // idle: nothing drawn
        Assert.Equal(Visibility.Collapsed, vm.Visibility);

        Assert.True(portrait.Start(160, 98, 8, 116, 48, 56));
        portrait.Step(); // the first call of the flight: 0 x 0 at the head, colour 255
        vm.ApplyDialogue(portrait, jess);
        Assert.Equal((Visibility.Visible, JessPortraitId, new Vector2(152, -18), new Vector2(0, 0), 255f / 128f), (vm.Visibility, vm.SourceName, vm.Translation, vm.Scale, vm.Brightness));

        portrait.Step(); // 3 x 3 at (149, 100), colour 246
        vm.ApplyDialogue(portrait, jess);
        Assert.Equal((new Vector2(141, -16), new Vector2(3f / 48f, 3f / 56f), 246f / 128f), (vm.Translation, vm.Scale, vm.Brightness));

        for (var i = 0; i < 20; i++)
        {
            portrait.Step();
        }

        vm.ApplyDialogue(portrait, jess); // at rest: the image as it is
        Assert.Equal((new Vector2(0, 0), new Vector2(1, 1), 1f, Visibility.Visible), (vm.Translation, vm.Scale, vm.Brightness, vm.Visibility));

        vm.ApplyDialogue(portrait, null); // no source: nothing to show
        Assert.Equal(Visibility.Collapsed, vm.Visibility);
    }

    [Fact]
    public void ApplyDialogue_ATallPortrait_KeepsTheCanvasTopOfTheXamlAndPutsTheGapInTheTranslation()
    {
        var miming = new DialoguePortraitRef(Guid.Parse(MimingPortraitId), 48, 72);
        var portrait = new AlundraInventoryPortrait();
        var vm = new InventoryPortraitViewModel();
        Assert.True(portrait.Start(160, 98, 8, 100, 48, 72));
        for (var i = 0; i < 17; i++)
        {
            portrait.Step();
        }

        vm.ApplyDialogue(portrait, miming); // at rest, 48 x 72 at (8, 100): the XAML puts the image at (8, 116)
        Assert.Equal((new Vector2(0, -16), new Vector2(1, 1), Visibility.Visible), (vm.Translation, vm.Scale, vm.Visibility));

        portrait.BeginReturn(160, 98);
        portrait.Step(); // the first pass of the return: the full size, still at the rest position
        vm.ApplyDialogue(portrait, miming);
        Assert.Equal(new Vector2(48f / 48f, 72f / 72f), vm.Scale);
    }

    [Fact]
    public void TheNameViewModel_WritesTheFrameTheTextAndTheName_AndCollapsesWhenNothingIsDrawn()
    {
        var box = new AlundraDialogueNameBox();
        var vm = new TextBoxNameViewModel();
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        vm.Apply(box); // never opened: nothing is drawn
        Assert.Equal(Visibility.Collapsed, vm.Visibility);

        Assert.True(box.TryOpen(0x104, _ => "Jess", _ => 7));
        for (var i = 0; i < 17; i++)
        {
            box.Pass(); // the slide in: the 17th pass is the first at rest, x 64
        }

        vm.Apply(box);
        Assert.Equal((64, 64 + (112 - 28) / 2, "Jess", Visibility.Visible), (vm.Left ?? -1, vm.TextLeft ?? -1, vm.Text, vm.Visibility));

        notifications.Clear();
        vm.Apply(box);
        Assert.Empty(notifications); // the same pass again writes nothing

        box.Reset();
        box.Pass();
        vm.Apply(box);
        Assert.Equal(Visibility.Collapsed, vm.Visibility);
    }

    [Fact]
    public void TheNameBox_KeepsTheTextOfItsOpening_WhateverTheEtcBecomes()
    {
        var box = new AlundraDialogueNameBox();
        var text = "Jess";
        Assert.True(box.TryOpen(0x104, _ => text, _ => 7));
        text = "Someone else";

        Assert.Equal("Jess", box.Text);
    }

    [Fact]
    public void ApplyWithASpeaker_ShowsTheRootAndFoldsTheTextBox_WhenOnlyTheSpeakerIsDrawn()
    {
        OpenTheBox("S1");
        var vm = new AlundraTextBoxViewModel();

        vm.Apply(Director.Box, true); // the box is not drawn yet, the speaker is
        Assert.Equal(1, vm.AppliedCount);
        Assert.Equal(Visibility.Visible, vm.RootVisibility);
        Assert.Equal((Visibility.Collapsed, Visibility.Collapsed, Visibility.Collapsed), (vm.Frame.Visibility, vm.Clip.Visibility, vm.Cursor.Visibility));

        Director.Pass(false, false); // the first slide-in pass: the box is drawn
        vm.Apply(Director.Box, true);
        Assert.Equal((Visibility.Visible, Visibility.Visible), (vm.Frame.Visibility, vm.Clip.Visibility));
        Assert.Equal(240, vm.Frame.Top);

        vm.Apply(Director.Box, false); // the old entry point is the same as no speaker
        Assert.Equal(3, vm.AppliedCount);
        Assert.Equal(Visibility.Visible, vm.RootVisibility);
    }

    // ---- the source is the one of the opening ----------------------------------------------------------------------------------------

    [Fact]
    public void ThePortraitSourceAndTheName_AreLockedAtTheOpening_ASpeakerRecycledMidFlightChangesNothing()
    {
        using var rig = new SpeakerRig();
        var scene = SceneOf("S1", rig);
        var viewModel = new AlundraTextBoxViewModel();
        var presenter = new AlundraTextBoxPresenter(rig.Director, viewModel, new SpeakerScreen(), null);

        Drive(rig, presenter, scene, f =>
        {
            if (f == 12)
            {
                Assert.Equal(JessPortraitId, viewModel.Portrait.SourceName);
                // the speaker is destroyed and its slot recycled by another entity: the live fields now describe something else
                scene.Speaker.DialoguePortrait = new DialoguePortraitRef(Guid.Parse(MimingPortraitId), 48, 72);
                scene.Speaker.SpriteType = 0x17A;
            }

            if (f > 12 && viewModel.Portrait.Visibility == Visibility.Visible)
            {
                Assert.True(viewModel.Portrait.SourceName == JessPortraitId, $"image {f}: the portrait source changed to {viewModel.Portrait.SourceName}");
            }

            if (f > 12 && viewModel.NameBox.Visibility == Visibility.Visible)
            {
                Assert.True(viewModel.NameBox.Text == "Jess", $"image {f}: the name changed to {viewModel.NameBox.Text}");
            }
        });

        Assert.Equal(Visibility.Collapsed, viewModel.Portrait.Visibility); // and the flight back ended
    }

    [Fact]
    public void ThePortraitSource_IsTheAcceptedStartsOnly_AndIsClearedWithTheSatellites()
    {
        using var rig = new SpeakerRig();
        var first = Spk(0x104, true);
        var second = Spk(0x105, true, 72, MimingPortraitId);

        rig.Director.OpenSpeaker(first);
        Assert.Equal(Guid.Parse(JessPortraitId), rig.Director.PortraitSource!.Value.SpriteAssetId);

        rig.Director.OpenSpeaker(second); // the machine is busy: the start is ignored, the source stays the first one
        Assert.Equal(Guid.Parse(JessPortraitId), rig.Director.PortraitSource!.Value.SpriteAssetId);

        rig.Director.NotifyPresenterClosed();
        Assert.Null(rig.Director.PortraitSource);
    }
}

/// <summary>The folder <c>docs/plan-e19-f4c-annexe</c> (the prediction of E19.f4c2: states, classes, references, digests), searched upward from the test binaries.</summary>
internal static class F4cAnnex
{
    public static string Directory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f4c-annexe");
            if (File.Exists(Path.Combine(candidate, "classes.tsv")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("docs/plan-e19-f4c-annexe not found above " + AppContext.BaseDirectory);
    }

    public static string File_(string name) => Path.Combine(Directory(), name);
}
