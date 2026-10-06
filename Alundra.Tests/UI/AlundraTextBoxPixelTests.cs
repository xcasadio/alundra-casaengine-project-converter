#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;
using static Alundra.Tests.UI.TextBoxGpu;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.f2b1c T5 (docs/plan-e19-opcodes.md, F2B1C-R3/R4): the pixels of the text box screen on a real GPU. The real <see cref="AlundraTextBoxScreen"/> (its versioned XAML, its
/// view model written by <see cref="AlundraTextBoxViewModel.Apply"/> from the real dialogue box at the pass of each scenario) is drawn by an <c>MGDesktop</c> into a render target,
/// read back, and compared texel for texel with the images composed from the binary's model (<c>docs/plan-e19-f2b1-annexe/pixels/*.png</c>, produced by
/// <c>scripts/refcompose2.py</c>, which reads the binary outside the repository and is not rerun here) and with the points of <c>pixels.md</c>; the synthetic states (the cut
/// at 255 pixels, the text over the cursor, the view offset of a window that is not 4:3) against a small independent compositor. At x1 and x2. Skipped without a GPU
/// (<see cref="AlundraGpuFactAttribute"/>). The harness is <see cref="TextBoxGpu"/>; the caveats of the discovery stand (a stand-in asset provider, one GPU, the production
/// <c>UIRoot</c> chain only proved by the recipe).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraTextBoxPixelTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;
    private const string Br = "[br trimwhitespace=false/]";

    private static readonly (int R, int G, int B) Ink = (41, 49, 16);
    private static readonly (int R, int G, int B) FrameBeige = (184, 176, 144);
    private static readonly (int R, int G, int B) Background = (ClearR, ClearG, ClearB);

    public AlundraTextBoxPixelTests()
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

    // ---- driving the real box ----------------------------------------------------------------------------------------------------

    private static void OpenTheBox(string[] pages)
    {
        Director.AdvanceProviderForTests = c => AlundraTextBoxOracle.GlyphWidth(OraclePages.ByteOf(c));
        Director.AttachToWorld(new DialogueService(), new AlundraGameState());
        Director.InstallForMapEntry();
        Director.Open(DialogueTestAssets.BuildRaw("pixels", "Start", pages), "Start", controlMode: 1);
    }

    private static void RunTo(int pass, Func<int, (bool Held, bool Pressed)>? pad = null)
    {
        for (var i = 1; i <= pass; i++)
        {
            var (held, pressed) = pad?.Invoke(i) ?? (false, false);
            Director.Pass(held, pressed);
        }
    }

    // S2 with the \A of a page boundary, at the pass its cursor first shows image 0 (pixels.md: "pass 151 ... cursor image 0")
    private static void DriveRest()
    {
        OpenTheBox(new[] { $"Bonjour, Alundra !{Br}deuxième ligne", "suite" });
        var pass = 0;
        while (Director.Box.CursorImage != 0 && pass < 400)
        {
            Director.Pass(false, false);
            pass++;
        }

        Assert.Equal(151, pass);
        Assert.Equal(0, Director.Box.CursorImage);
    }

    // S3 with a page after it: the 44 steps of the text end at pass 191, the cursor of the page boundary is the step of pass 195 (pixels.md: "pass 195", the picture shows it).
    private static void DriveCentred()
    {
        OpenTheBox(new[]
        {
            "[voice id=-1 trimwhitespace=false/][center trimwhitespace=false/]Roue de la fortune !" + Br
            + "[center trimwhitespace=false/]Florin[glyph id=21 trimwhitespace=false/]Roulette        [empty trimwhitespace=false/]",
            "suite",
        });
        RunTo(195);
        Assert.Equal(0, Director.Box.CursorImage);
    }

    private static void DriveScroll(int pass, bool preShiftVariant = false)
    {
        OpenTheBox(new[] { $"gypjq{Br}deux{Br}trois{Br}quatre" });
        Director.Box.DrawsPreShiftRowsAtScrollEnd = preShiftVariant;
        RunTo(pass);
    }

    private static void DriveSlideOut182()
    {
        OpenTheBox(new[] { $"l1{Br}l2{Br}gypjq" });
        RunTo(35, _ => (true, true));
    }

    // ---- drawing and comparing --------------------------------------------------------------------------------------------------------

    private static string ReferenceImage(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f2b1-annexe", "pixels", name + ".png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("docs/plan-e19-f2b1-annexe/pixels/" + name + ".png not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Builds the real screen on a fresh desktop at integer scale <paramref name="k"/>, writes the view model from the box as it stands, draws and reads back.</summary>
    private static Color[] DrawTheBox(int k, Action<AlundraTextBoxViewModel>? setState = null, int ox = 0, int oy = 0)
    {
        return Invoke(() =>
        {
            var session = GetSession();
            var assets = TextBoxScreenAssets.New();
            using var screen = new AlundraTextBoxScreen(assets, new UIFontRegistry(assets));
            var desktop = session.NewDesktop();
            session.Host.Bounds = new Rectangle(0, 0, 320 * k, 240 * k);
            var window = screen.BuildWindow(desktop);
            desktop.Windows.Add(window);
            if (setState is null)
            {
                screen.ViewModel.Apply(Director.Box);
            }
            else
            {
                setState(screen.ViewModel);
            }

            return session.Draw(desktop, 320 * k, 240 * k, ox, oy);
        });
    }

    private static string Differences(Color[] got, Color[] want, int width, int height)
    {
        var count = 0;
        var first = string.Empty;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = got[y * width + x];
                var b = want[y * width + x];
                if (a.R != b.R || a.G != b.G || a.B != b.B)
                {
                    if (count == 0)
                    {
                        first = $"first at ({x}, {y}) got ({a.R}, {a.G}, {a.B}) want ({b.R}, {b.G}, {b.B})";
                    }

                    count++;
                }
            }
        }

        return count == 0 ? "0" : $"{count} texels, {first}";
    }

    private static void AssertPoint(Color[] pixels, int k, int x, int y, (int R, int G, int B) expected, string why)
    {
        for (var dy = 0; dy < k; dy++)
        {
            for (var dx = 0; dx < k; dx++)
            {
                var c = pixels[(y * k + dy) * 320 * k + x * k + dx];
                Assert.True((c.R, c.G, c.B) == expected, $"x{k} ({x}, {y}) texel ({dx}, {dy}): got ({c.R}, {c.G}, {c.B}), want {expected} - {why}");
            }
        }
    }

    /// <summary>Draws the box as it stands at x1 and x2, compares each with its versioned image (the x2 one is the nearest upscale of the x1 image), then runs <paramref name="points"/>.</summary>
    private static void AssertTheImage(string imageName, params (int X, int Y, (int R, int G, int B) Color, string Why)[] points)
    {
        foreach (var k in new[] { 1, 2 })
        {
            var got = DrawTheBox(k);
            var want = Invoke(() => GetSession().ReadPng(ReferenceImage(k == 1 ? imageName : imageName + "_x2"), out _, out _));
            Assert.Equal("0", Differences(got, want, 320 * k, 240 * k));
            foreach (var (x, y, color, why) in points)
            {
                AssertPoint(got, k, x, y, color, why);
            }
        }
    }

    // ---- the versioned images -----------------------------------------------------------------------------------------------------------

    [AlundraGpuFact]
    public void TheRestState_IsTheVersionedImage_AtX1AndX2()
    {
        DriveRest();
        AssertTheImage(
            "rest",
            (16, 168, Background, "top-left corner of the frame: a transparent texel shows the background"),
            (17, 170, (88, 96, 72), "frame texel near the top-left"),
            (160, 196, (152, 152, 112), "frame interior, no text"),
            (31, 175, FrameBeige, "frame interior one pixel left of the clip and of the text: the frame is never clipped"),
            (32, 175, Ink, "first ink texel of row 0 ('B')"),
            (32, 174, FrameBeige, "the texel above it"),
            (136, 175, (99, 107, 74), "rightmost ink texel of row 0 (the '!')"),
            (32, 196, Ink, "first ink texel of row 1 ('d')"),
            (288, 200, (168, 168, 136), "cursor top-left texel (image 0, wind_150)"),
            (295, 207, (192, 192, 192), "cursor interior texel"));
    }

    [AlundraGpuFact]
    public void ACentredLine_IsTheVersionedImage_AtX1AndX2()
    {
        DriveCentred();
        AssertTheImage(
            "centred",
            (104, 175, Ink, "row 0 leftmost ink texel, the band at x 104 (width 111)"),
            (103, 175, FrameBeige, "row 0 the texel left of it"),
            (97, 191, Ink, "row 1 leftmost ink texel, the band at x 97 (width 125)"),
            (96, 191, (152, 152, 112), "row 1 the texel left of it"));
    }

    [AlundraGpuFact]
    public void AScrollAtOffset6_IsTheVersionedImage_AtX1AndX2()
    {
        DriveScroll(95);
        Assert.Equal(6, Director.Box.RowOffset);
        AssertTheImage(
            "scroll6",
            (34, 172, Ink, "row 0 pixel row 5 is the clip top: ink kept"),
            (45, 171, (88, 96, 72), "row 0 pixel row 4: ink in the band but above the clip, the frame shows"),
            (52, 170, Background, "row 0 pixel row 3: above the clip too (the frame is transparent there)"),
            (33, 182, Ink, "row 0 pixel row 15, a descender texel"),
            (36, 185, Ink, "row 1 pixel row 2"),
            (49, 201, Ink, "row 2 pixel row 2"),
            (20, 171, FrameBeige, "frame left of the clip: never clipped"));
    }

    [AlundraGpuFact]
    public void TheScrollEndingPass_ShowsTheRowsAfterTheShift_AsTheVersionedImage_AtX1AndX2()
    {
        DriveScroll(100);
        Assert.False(AlundraDialogueBox.ScrollEndDrawsPreShiftRows);
        AssertTheImage(
            "scroll_ending_visible",
            (33, 172, FrameBeige, "the band of the old top row is wiped before the ordering table is drawn (D-E19-83): the frame colour"),
            (36, 175, Ink, "old row 1 ('deux') pixel row 2, where it will sit after the shift"),
            (49, 191, Ink, "old row 2 ('trois') pixel row 2"));
    }

    [AlundraGpuFact]
    public void TheScrollEndingPass_WithTheVariantOfTheConstant_ShowsTheCpuDrawList_AtX1AndX2()
    {
        DriveScroll(100, preShiftVariant: true);
        AssertTheImage(
            "scroll_ending_cpu_list",
            (33, 172, Ink, "the CPU draw list holds the old top row's descender ink at y 172"));
    }

    [AlundraGpuFact]
    public void ASlideOutAtY182_ClipsFromThePreviousY_AsTheVersionedImage_AtX1AndX2()
    {
        DriveSlideOut182();
        Assert.Equal((182, 181, 50), (Director.Box.Y, Director.Box.ClipTop, Director.Box.ClipHeight));
        AssertTheImage(
            "slideout182",
            (33, 230, Ink, "row 2 pixel row 11 is the last row inside the lagged clip"),
            (34, 231, FrameBeige, "row 2 pixel row 12: ink in the band, below the lagged clip, cut (a clip from the current Y would keep it)"),
            (32, 232, (88, 96, 72), "row 2 pixel row 13: cut"),
            (33, 234, (72, 64, 56), "row 2 pixel row 15: cut (descender)"),
            (33, 189, Ink, "row 0 pixel row 2: well inside the clip"),
            (20, 183, FrameBeige, "frame texel at the current Y: the frame is not lagged"),
            (20, 181, Background, "one above the frame top"));
    }

    // ---- the synthetic states -------------------------------------------------------------------------------------------------------------

    private static Compositor Compose(AlundraTextBoxViewModel vm, bool cursorOverText = false)
    {
        var session = GetSession();
        var compositor = new Compositor(320, 240);
        var fnt = Fnt.Parse(File.ReadAllText(Path.Combine(session.ProjectRoot, "UI", "font3.fnt")));
        var page = new Img(session.Assets.LoadPng(Path.Combine("UI", "Textures", "font3.png")));
        var (frameTexture, frameRect) = session.Assets.ResolveSprite("973a9208-c867-57fe-bee3-cf30237221ef");
        compositor.Blit(new Img(frameTexture), frameRect, 16, vm.Frame.Top!.Value);

        void DrawCursor()
        {
            if (vm.Cursor.Visibility == MGUI.Core.UI.Visibility.Visible)
            {
                var (cursorTexture, cursorRect) = session.Assets.ResolveSprite(vm.Cursor.SourceName!);
                compositor.Blit(new Img(cursorTexture), cursorRect, 288, vm.Cursor.Top!.Value);
            }
        }

        if (!cursorOverText)
        {
            DrawCursor();
        }

        compositor.Clip = new Rectangle(32, vm.Clip.Top!.Value, 258, vm.Clip.Height!.Value);
        for (var r = 0; r < 3; r++)
        {
            var row = vm.Row(r);
            compositor.DrawText(page, fnt, row.Text, 32 + row.Left!.Value, vm.Clip.Top!.Value + row.Top!.Value, 255);
        }

        compositor.Clip = null;
        if (cursorOverText)
        {
            DrawCursor();
        }

        return compositor;
    }

    /// <summary>The box at rest (Y 168, clip 172 / 50) with <paramref name="text"/> on row <paramref name="row"/> at <paramref name="rowLeft"/> (relative to the clip canvas).</summary>
    private static void RestState(AlundraTextBoxViewModel vm, string text, int rowLeft, string? cursor, int row = 0)
    {
        vm.RootVisibility = MGUI.Core.UI.Visibility.Visible;
        vm.Frame.Top = 168;
        vm.Clip.Top = 172;
        vm.Clip.Height = 50;
        for (var r = 0; r < 3; r++)
        {
            vm.Row(r).Text = r == row ? text : string.Empty;
            vm.Row(r).Left = r == row ? rowLeft : 0;
            vm.Row(r).Top = 1 + 16 * r;
        }

        vm.Cursor.SourceName = cursor;
        vm.Cursor.Top = 200;
        vm.Cursor.Visibility = cursor is null ? MGUI.Core.UI.Visibility.Collapsed : MGUI.Core.UI.Visibility.Visible;
    }

    /// <summary>A row of 25 'm' (glyph 109, 11 pixels each: 275) is cut at 255 pixels: the last column of the band the binary draws (254) is ink, the next one is not.</summary>
    [AlundraGpuFact]
    public void ALineLongerThan255Pixels_IsCutAtTheBand_AtX1AndX2()
    {
        var row = new string('m', 25);
        foreach (var k in new[] { 1, 2 })
        {
            var got = DrawTheBox(k, vm => RestState(vm, row, 0, null));
            AssertPoint(got, k, 286, 179, Ink, "band column 254, the last one the 255-wide band shows");
            AssertPoint(got, k, 287, 179, (168, 168, 136), "band column 255: ink in a 256-wide band, but the band is 255 wide: cut, the frame shows");

            var vmForReference = new AlundraTextBoxViewModel();
            RestState(vmForReference, row, 0, null);
            Assert.Equal("0", Differences(got, Invoke(() => Compose(vmForReference).Scaled(k)), 320 * k, 240 * k));
        }
    }

    /// <summary>The text is drawn over the cursor (the binary's slot 0 holds the frame then the cursor, slot 2 the text): 'K' x 60 from x 39 on row 1 (y 189 to 204) with the
    /// cursor wind_201 (y 200 to 215) overlaps it on the columns 288 and 289, 3 texels at x1 (view-verify.md:78); the pixels there are the text's.</summary>
    [AlundraGpuFact]
    public void TheTextIsDrawnOverTheCursor_AtX1AndX2()
    {
        var row = new string('K', 60);
        var vmForReference = new AlundraTextBoxViewModel();
        RestState(vmForReference, row, 7, "wind_201", row: 1);

        // The scene pins the order: the picture with the cursor drawn over the text differs from the one with the text over the cursor.
        var textOverCursor = Invoke(() => Compose(vmForReference).P);
        var cursorOverText = Invoke(() => Compose(vmForReference, cursorOverText: true).P);
        var overlapping = Differences(textOverCursor, cursorOverText, 320, 240);
        Assert.StartsWith("3 texels", overlapping);

        foreach (var k in new[] { 1, 2 })
        {
            var got = DrawTheBox(k, vm => RestState(vm, row, 7, "wind_201", row: 1));
            Assert.Equal("0", Differences(got, Invoke(() => Compose(vmForReference).Scaled(k)), 320 * k, 240 * k));
        }
    }

    /// <summary>The engine fits the 320 x 240 image in a window that is not 4:3 and shifts the view (engine ADR-0054): the host's bounds are view-local and the viewport is
    /// offset. A state whose frame is at Y >= 232 (the end of a slide-out: 235, then 240) draws nothing outside the view: not below the 320 x 240 canvas, not in the margin.
    /// Inside the view the picture is the one drawn without an offset.</summary>
    [AlundraGpuFact]
    public void AStateWithTheFrameAtYOver232_InAViewThatIsOffset_DrawsNothingOutsideTheView()
    {
        foreach (var pass in new[] { 46, 47 })
        {
            Reset();
            DriveSlideOut(pass);
            Assert.True(Director.Box.Y >= 232, $"pass {pass}: Y {Director.Box.Y}");
            foreach (var k in new[] { 2, 3 })
            {
                var plain = DrawTheBox(k);
                const int ox = 97;
                const int oy = 41;
                var shifted = DrawTheBox(k, null, ox, oy);
                var w = 320 * k;
                var h = 240 * k;
                var tw = w + 2 * ox;
                var outside = 0;
                var inside = 0;
                for (var y = 0; y < h + 2 * oy; y++)
                {
                    for (var x = 0; x < tw; x++)
                    {
                        var c = shifted[y * tw + x];
                        var inView = x >= ox && x < ox + w && y >= oy && y < oy + h;
                        if (inView)
                        {
                            var p = plain[(y - oy) * w + (x - ox)];
                            if (c.R != p.R || c.G != p.G || c.B != p.B)
                            {
                                inside++;
                            }
                        }
                        else if (c.R != ClearR || c.G != ClearG || c.B != ClearB)
                        {
                            outside++;
                        }
                    }
                }

                Assert.True(inside == 0, $"pass {pass} x{k}: {inside} texels of the view differ from the picture without an offset");
                Assert.True(outside == 0, $"pass {pass} x{k}: {outside} texels were drawn outside the view");
            }
        }
    }

    private static void DriveSlideOut(int pass)
    {
        OpenTheBox(new[] { $"l1{Br}l2{Br}gypjq" });
        RunTo(pass, _ => (true, true));
    }
}
