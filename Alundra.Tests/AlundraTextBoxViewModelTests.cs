#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using MGUI.Core.UI;
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
        foreach (var model in new INotifyPropertyChanged[] { vm, vm.Frame, vm.Clip, vm.Row0, vm.Row1, vm.Row2, vm.Cursor })
        {
            model.PropertyChanged += handler;
        }

        vm.Apply(Director.Box);
        Assert.Empty(notifications);

        RunTo(4); // a glyph more
        vm.Apply(Director.Box);
        Assert.NotEmpty(notifications);
    }
}
