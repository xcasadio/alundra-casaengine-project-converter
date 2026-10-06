#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;
using static Alundra.Tests.UI.TextBoxGpu;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.f3b F3B-1 (docs/plan-e19-opcodes.md, F3B-R1 to R3): the pixels of the choice screen on a real GPU. The real <see cref="AlundraChoiceScreen"/> (its versioned XAML, its view
/// model written by <see cref="AlundraChoiceViewModel.Apply"/> from the real <see cref="AlundraChoiceBox"/> at the pass of each scenario of
/// <c>docs/plan-e19-f3-annexe/values.json</c>) is drawn by an <c>MGDesktop</c> into a render target, read back, and compared texel for texel with the 19 images of
/// <c>docs/plan-e19-f3-annexe/view/pixels/</c> (composed by the independent <c>view/compositor/ref.py</c> from the exported PNGs and the binary's tables, not rerun here) and
/// with the points of <c>view/points.md</c>, at x1 and x2; over the text box screen (its window first, the choice above it) at rest and with a full first row, with the control of the
/// inverse order; and in a view that is offset (the window is not 4:3). Skipped without a GPU (<see cref="AlundraGpuFactAttribute"/>); the harness is <see cref="TextBoxGpu"/>
/// and the caveats of the discovery stand (a stand-in asset provider, one GPU, the production <c>UIRoot</c> chain only proved by the recipe).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraChoicePixelTests
{
    private static readonly (int R, int G, int B) Background = (ClearR, ClearG, ClearB);

    private static string ViewDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f3-annexe");
            if (File.Exists(Path.Combine(candidate, "values.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("docs/plan-e19-f3-annexe not found above " + AppContext.BaseDirectory);
    }

    private static string ReferenceImage(string name) => Path.Combine(ViewDirectory(), "view", "pixels", name + ".png");

    // ---- driving the real machine ----------------------------------------------------------------------------------------------------

    /// <summary>The real box after <paramref name="pass"/> passes of scenario <paramref name="index"/> of <c>values.json</c> (0 = V1, 1 = V2, 5 = V6), the pad words listed there.</summary>
    private static AlundraChoiceBox BoxAt(int index, int pass)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ViewDirectory(), "values.json")));
        var scenario = document.RootElement.GetProperty("bare").EnumerateArray().ElementAt(index);
        var raw = new Dictionary<int, uint>();
        foreach (var entry in scenario.GetProperty("pad").EnumerateObject())
        {
            raw[int.Parse(entry.Name.Substring(2), CultureInfo.InvariantCulture)] = Convert.ToUInt32(entry.Value.GetString(), 16);
        }

        var box = new AlundraChoiceBox();
        box.SeedCursorTickForTests(scenario.GetProperty("anim0").GetInt32());
        var pad = new AlundraTickPad();
        for (var t = 0; t <= pass; t++)
        {
            pad.Update(raw.GetValueOrDefault(t));
            if (t == 0)
            {
                box.Open("OUI", "NON", scenario.GetProperty("default_sel").GetInt32());
                continue;
            }

            box.Pass(pad.ButtonsJustPressed, pad.ButtonsJustPressedByInterval);
        }

        return box;
    }

    /// <summary>The text box view model at the state of <c>ref.py</c>'s <c>TEXTBOX_STATES</c>: <c>rest</c> (the design rest state) or <c>mrow</c> (a first row of 18 glyphs 0x1A, 14 pixels
    /// wide with ink on their first rows, so the text passes under the choice frame, and <c>NON OUI</c> on the second).</summary>
    private static void TextBoxState(AlundraTextBoxViewModel vm, string name)
    {
        vm.RootVisibility = MGUI.Core.UI.Visibility.Visible;
        vm.Frame.Top = 168;
        vm.Clip.Top = 172;
        vm.Clip.Height = 50;
        var rows = name == "rest"
            ? new[] { "Bonjour, Alundra !", "deuxième ligne", string.Empty }
            : new[] { new string((char)26, 18), "NON OUI", string.Empty };
        for (var r = 0; r < 3; r++)
        {
            vm.Row(r).Text = rows[r];
            vm.Row(r).Left = 0;
            vm.Row(r).Top = 1 + 16 * r;
        }

        vm.Cursor.SourceName = "wind_150";
        vm.Cursor.Top = 200;
        vm.Cursor.Visibility = MGUI.Core.UI.Visibility.Visible;
    }

    // ---- drawing and comparing -------------------------------------------------------------------------------------------------------

    /// <summary>Builds the real screen(s) on a fresh desktop at integer scale <paramref name="k"/>, writes the view model(s) from <paramref name="box"/>, draws and reads back.
    /// With an <paramref name="underlay"/> the text box window is added to the desktop first and the choice above it (the order of the presenters, F3B-R3), or the other way round
    /// when <paramref name="choiceFirst"/> (the control).</summary>
    private static Color[] Draw(int k, AlundraChoiceBox box, string? underlay = null, bool choiceFirst = false, int ox = 0, int oy = 0)
    {
        return Invoke(() =>
        {
            var session = GetSession();
            var assets = TextBoxScreenAssets.New();
            using var choice = new AlundraChoiceScreen(assets, new UIFontRegistry(assets));
            using var textBox = underlay is null ? null : new AlundraTextBoxScreen(assets, new UIFontRegistry(assets));
            var desktop = session.NewDesktop();
            session.Host.Bounds = new Rectangle(0, 0, 320 * k, 240 * k);
            var choiceWindow = choice.BuildWindow(desktop);
            choice.ViewModel.Apply(box);
            MGUI.Core.UI.MGWindow? textBoxWindow = null;
            if (textBox is not null)
            {
                textBoxWindow = textBox.BuildWindow(desktop);
                TextBoxState(textBox.ViewModel, underlay!);
            }

            if (choiceFirst)
            {
                desktop.Windows.Add(choiceWindow);
            }

            if (textBoxWindow is not null)
            {
                desktop.Windows.Add(textBoxWindow);
            }

            if (!choiceFirst)
            {
                desktop.Windows.Add(choiceWindow);
            }

            return session.Draw(desktop, 320 * k, 240 * k, ox, oy);
        });
    }

    private static int DifferenceCount(Color[] got, Color[] want, out string first)
    {
        Assert.Equal(want.Length, got.Length);
        var count = 0;
        first = string.Empty;
        for (var i = 0; i < got.Length; i++)
        {
            var a = got[i];
            var b = want[i];
            if (a.R != b.R || a.G != b.G || a.B != b.B)
            {
                if (count == 0)
                {
                    first = $"first at index {i} got ({a.R}, {a.G}, {a.B}) want ({b.R}, {b.G}, {b.B})";
                }

                count++;
            }
        }

        return count;
    }

    private static string Differences(Color[] got, Color[] want)
    {
        var count = DifferenceCount(got, want, out var first);
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

    // ---- the points of view/points.md (the values written in advance) -------------------------------------------------------------------

    private static readonly Regex PointLine = new(
        @"^\| \((?<x>\d+), (?<y>\d+)\) \| \((?<r>\d+), (?<g>\d+), (?<b>\d+), 255\) \| [^|]*\| (?<why>[^|]*)\|", RegexOptions.Compiled);

    /// <summary>The points of one section of <c>points.md</c>, the section found by the start of its title.</summary>
    private static (int X, int Y, (int R, int G, int B) Color, string Why)[] PointsOf(string sectionStart)
    {
        var points = new List<(int, int, (int, int, int), string)>();
        var inSection = false;
        foreach (var line in File.ReadAllLines(Path.Combine(ViewDirectory(), "view", "points.md")))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.StartsWith("## " + sectionStart, StringComparison.Ordinal);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            var m = PointLine.Match(line);
            if (m.Success)
            {
                points.Add((
                    int.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture),
                    (int.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture)),
                    "points.md: " + sectionStart));
            }
        }

        Assert.NotEmpty(points);
        return points.ToArray();
    }

    /// <summary>Draws the box as it stands at x1 (and x2 when the image has one), compares each with its versioned image, then runs <paramref name="sectionOfPoints"/> at each scale.</summary>
    private static void AssertTheImage(string imageName, AlundraChoiceBox box, string? underlay = null, string? sectionOfPoints = null, bool hasX2 = true)
    {
        foreach (var k in hasX2 ? new[] { 1, 2 } : new[] { 1 })
        {
            var got = Draw(k, box, underlay);
            var want = Invoke(() => GetSession().ReadPng(ReferenceImage($"{imageName}_x{k}"), out _, out _));
            Assert.Equal("0", Differences(got, want));
            if (sectionOfPoints is not null)
            {
                foreach (var (x, y, color, why) in PointsOf(sectionOfPoints))
                {
                    AssertPoint(got, k, x, y, color, why);
                }
            }
        }
    }

    // ---- the choice alone: the versioned images ----------------------------------------------------------------------------------------

    [AlundraGpuFact]
    public void TheRestStateOnOui_IsTheVersionedImage_AtX1AndX2_AndTheirPoints()
    {
        AssertTheImage("choice_V1_t19_rest_oui", BoxAt(0, 19), sectionOfPoints: "choice rest OUI");
    }

    [AlundraGpuFact]
    public void TheRestStateOnNon_IsTheVersionedImage_AtX1AndX2_AndTheirPoints()
    {
        AssertTheImage("choice_V2_t22_rest_non", BoxAt(1, 22), sectionOfPoints: "choice rest NON");
    }

    [AlundraGpuFact]
    public void TheFirstDrawnPass_IsTheVersionedImage_AtX1AndX2()
    {
        AssertTheImage("choice_V1_t02_first_drawn", BoxAt(0, 2));
    }

    [AlundraGpuFact]
    public void ASlideIn_IsTheVersionedImage_AtX1AndX2_AndItsPoints()
    {
        AssertTheImage("choice_V1_t05_slide_in", BoxAt(0, 5), sectionOfPoints: "choice slide-in");
    }

    [AlundraGpuFact]
    public void ASlideOut_IsTheVersionedImage_AtX1AndX2_AndItsPoints()
    {
        AssertTheImage("choice_V1_t25_slide_out", BoxAt(0, 25), sectionOfPoints: "choice slide-out");
    }

    [AlundraGpuFact]
    public void TheInitPass_TheEdgeOfTheExit_TheOffScreenPassAndTheClosePass_AreTheVersionedImages_AtX1()
    {
        // N+1: nothing drawn; N+34: the frame at x 310, the cursor and NON beyond the right edge; N+36: the frame at 320, off screen; N+37: the close pass, nothing drawn.
        AssertTheImage("choice_V1_t01_init", BoxAt(0, 1), hasX2: false);
        AssertTheImage("choice_V1_t34_slide_out_edge", BoxAt(0, 34), hasX2: false);
        AssertTheImage("choice_V1_t36_off_screen", BoxAt(0, 36), hasX2: false);
        AssertTheImage("choice_V1_t37_closed", BoxAt(0, 37), hasX2: false);
    }

    // ---- over the text box ---------------------------------------------------------------------------------------------------------------

    [AlundraGpuFact]
    public void TheChoice_OverTheTextBoxAtRest_IsTheVersionedImage_AtX1AndX2_AndTheOrderPoints()
    {
        AssertTheImage("overlap_rest_V1_t19", BoxAt(0, 19), underlay: "rest", sectionOfPoints: "choice over the text box at rest");
    }

    [AlundraGpuFact]
    public void TheChoice_OverAFullFirstRow_IsTheVersionedImage_AtX1AndX2_AndTheOrderPoints()
    {
        AssertTheImage("overlap_mrow_V1_t19", BoxAt(0, 19), underlay: "mrow", sectionOfPoints: "choice over a full first text row");
        AssertTheImage("overlap_mrow_V2_t22", BoxAt(1, 22), underlay: "mrow", hasX2: false);
    }

    /// <summary>The control: with the windows added the other way round (the text box above the choice) the same states differ from the versioned images; the order is what is
    /// measured (338 to 345 texels at x1, the discovery's figures, view-notes.md section 3).</summary>
    [AlundraGpuFact]
    public void TheInverseOrderOfTheWindows_DiffersFromTheVersionedImages()
    {
        foreach (var (image, index, pass, underlay) in new[]
        {
            ("overlap_rest_V1_t19", 0, 19, "rest"),
            ("overlap_mrow_V1_t19", 0, 19, "mrow"),
            ("overlap_mrow_V2_t22", 1, 22, "mrow"),
        })
        {
            var wrong = Draw(1, BoxAt(index, pass), underlay, choiceFirst: true);
            var want = Invoke(() => GetSession().ReadPng(ReferenceImage(image + "_x1"), out _, out _));
            var count = DifferenceCount(wrong, want, out var first);
            Assert.True(count is >= 338 and <= 345, $"{image}: the inverse order differs by {count} texels ({first}), the discovery measured 338 to 345");
        }
    }

    // ---- a view that is offset -----------------------------------------------------------------------------------------------------------

    /// <summary>The engine fits the 320 x 240 image in a window that is not 4:3 and shifts the view (engine ADR-0054). States whose frame, labels and cursor leave the image on the right
    /// (the first slide-in passes, frame at x 311 with the cursor at 331 or 379 on V6 whose default is NON, and the end of the exit at 320) draw nothing outside the view: not right of
    /// the 320 x 240 canvas, not in the margin. Inside the view the picture is the one drawn without an offset.</summary>
    [AlundraGpuFact]
    public void AStateBeyondTheRightEdge_InAViewThatIsOffset_DrawsNothingOutsideTheView()
    {
        foreach (var (index, pass) in new[] { (0, 2), (0, 3), (5, 2), (0, 35) })
        {
            foreach (var k in new[] { 2, 3 })
            {
                var plain = Draw(k, BoxAt(index, pass));
                const int ox = 97;
                const int oy = 41;
                var shifted = Draw(k, BoxAt(index, pass), null, false, ox, oy);
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

                Assert.True(inside == 0, $"scenario {index}, pass {pass}, x{k}: {inside} texels of the view differ from the picture without an offset");
                Assert.True(outside == 0, $"scenario {index}, pass {pass}, x{k}: {outside} texels were drawn outside the view");
            }
        }
    }

    /// <summary>A sanity of the harness for the reader: the plain pictures at the offset states are not empty (the frame is on screen from x 311 on).</summary>
    [AlundraGpuFact]
    public void TheOffsetStates_ShowSomethingInsideTheView()
    {
        var picture = Draw(2, BoxAt(0, 3));
        Assert.True(picture.Any(c => (c.R, c.G, c.B) != Background), "pass N+3 of V1 draws nothing");
    }
}
