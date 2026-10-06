#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;
using static Alundra.Tests.UI.TextBoxGpu;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.f4c2 T5 (docs/plan-e19-opcodes.md, F4C2-1): the pixels of the speaker's name box and portrait on a real GPU. The real <see cref="AlundraTextBoxScreen"/> (its versioned XAML) is
/// written by the real <see cref="AlundraTextBoxPresenter"/> from the real director on the scenes of the f4 annex (S1 Jess, S2, S3, S5, S8 Miming 48 x 72), image by image, drawn by an
/// <c>MGDesktop</c> into a render target at the integer scale k (and in the view offset of a window that is not 4:3), read back and compared with the references of the f4c annex
/// (<c>docs/plan-e19-f4c-annexe/refs/*.png</c>, produced by <c>gen_cases.py</c> from the binary's value model, never from the DLL), each image in its class (<c>classes.tsv</c>):
/// <b>exact</b> (Rgb 128, the portrait at rest; or no portrait) and <b>tint8</b> (Rgb other than 128: every pixel outside the portrait's quad exact, inside it each channel within 8/255 of
/// the reference, the rows and columns on a tie of the centre rule excluded; D-E19-100, D-E19-101). The PS1's own texel rule is not a reference. Skipped without a GPU
/// (<see cref="AlundraGpuFactAttribute"/>): running them on a machine with a GPU is the evidence. Needs the exported <c>alundra-project</c> (<c>UI/Portraits</c>).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSpeakerPixelTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;

    private const string JessPortraitId = "56f5809a-a47b-564e-b7f7-a66be1f98b04";
    private const string MimingPortraitId = "bf75c68e-424f-57c9-8f6a-535f33d769d7";

    private readonly ITestOutputHelper _output;

    public AlundraSpeakerPixelTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        Director.ResetForTests();
        Director.Box.DrawsPreShiftRowsAtScrollEnd = AlundraDialogueBox.ScrollEndDrawsPreShiftRows;
        Director.AdvanceProviderForTests = null;
        AlundraDialogueCapturePresenter.ResetForTests();
        AlundraFont3Advances.ResetForTests();
        AlundraEtcStringTable.ResetForTests();
    }

    // ---- the scenes of the annex ----------------------------------------------------------------------------------------------------------------

    private sealed record Pin(string Name, string Scenario, int Frame, int Rel, int K, int Ox, int Oy, string Rgb, string Class, string Quad, string Reference);

    private static List<Pin> Pins() =>
        File.ReadAllLines(F4cAnnex.File_("classes.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'))
            .Select(c => new Pin(c[0], c[1], int.Parse(c[2]), int.Parse(c[3]), int.Parse(c[4]), int.Parse(c[5]), int.Parse(c[6]), c[7], c[8], c[9], c[10])).ToList();

    private static AlundraEntityScriptProxy Spk(int spriteType, bool portrait, int height = 56, string? id = null)
    {
        var speaker = SpeakerRig.Speaker(spriteType, portrait, height);
        if (portrait)
        {
            speaker.DialoguePortrait = new DialoguePortraitRef(Guid.Parse(id ?? (height == 72 ? MimingPortraitId : JessPortraitId)), 48, height);
        }

        return speaker;
    }

    /// <summary>The dialogues of a scene of the f4c annex: S1 Jess (0x104, entity (200, 150, 0), camera (40, 20)), S2 portrait only (0xFF), S3 name only (Septimus 0x10C), S5 two in a row, S8 Miming (0x17A, 48 x 72).</summary>
    private static (AlundraEntityScriptProxy Speaker, int[][] Codes) SceneOf(string scenario) => scenario switch
    {
        "S1" => (Spk(0x104, true), new[] { SpeakerRig.Codes0D(SpeakerRig.TextAb) }),
        "S2" => (Spk(0xFF, true), new[] { SpeakerRig.Codes0D(SpeakerRig.TextAb) }),
        "S3" => (Spk(0x10C, false), new[] { SpeakerRig.Codes0D(SpeakerRig.TextAb) }),
        "S5" => (Spk(0x104, true), new[] { SpeakerRig.Codes0D(SpeakerRig.TextAb), SpeakerRig.Codes0D(SpeakerRig.TextCd) }),
        "S8" => (Spk(0x17A, true, 72), new[] { SpeakerRig.Codes0D(SpeakerRig.TextAb) }),
        _ => throw new ArgumentException(scenario),
    };

    /// <summary>The real screen at the scale k, its view model written by the real presenter after each pass of the real director up to image <paramref name="frame"/> (the montage of the f4 annex:
    /// the pass, the presenter's tick, then the script phase), drawn into a target (the view at (ox, oy)). <paramref name="adjust"/> may change the view model before the draw.</summary>
    private static Color[] DrawImage(string scenario, int frame, int k, int ox = 0, int oy = 0, Action<AlundraTextBoxViewModel>? adjust = null)
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

            using var rig = new SpeakerRig();
            var (speaker, codes) = SceneOf(scenario);
            var presenter = new AlundraTextBoxPresenter(rig.Director, screen.ViewModel, screen, null);
            var next = 0;
            for (var f = 0; f <= frame; f++)
            {
                rig.Pass();
                presenter.Tick();
                if (next < codes.Length && f >= 3 && rig.Run(speaker, codes[next]))
                {
                    next++;
                }
            }

            adjust?.Invoke(screen.ViewModel);
            return session.Draw(desktop, 320 * k, 240 * k, ox, oy);
        });
    }

    /// <summary>A hand-built state of the screen (nothing driven by the director): for the names and the portraits at rest.</summary>
    private static Color[] DrawState(Action<AlundraTextBoxViewModel> setState)
    {
        return Invoke(() =>
        {
            var session = GetSession();
            var assets = TextBoxScreenAssets.New();
            using var screen = new AlundraTextBoxScreen(assets, new UIFontRegistry(assets));
            var desktop = session.NewDesktop();
            session.Host.Bounds = new Rectangle(0, 0, 320, 240);
            var window = screen.BuildWindow(desktop);
            desktop.Windows.Add(window);
            setState(screen.ViewModel);
            return session.Draw(desktop, 320, 240);
        });
    }

    private static string ReferencePath(string reference) => Path.Combine(F4cAnnex.Directory(), "refs", reference);

    // ---- the comparison classes ------------------------------------------------------------------------------------------------------------------

    private static bool OnATie(int index, int destination, int source) => ((2 * index + 1) * source) % (2 * destination) == 0;

    /// <summary>Null when <paramref name="got"/> is the reference in the class of the pin; else what differs.</summary>
    private static string? Compare(Pin pin, Color[] got, Color[] want, int width, int height)
    {
        var quad = pin.Quad == "-" ? null : pin.Quad.Split(',').Select(int.Parse).ToArray();
        var sourceHeight = pin.Scenario == "S8" ? 72 : 56;
        var exactOnly = pin.Class == "exact" || quad is null;
        var x0 = quad is null ? 0 : quad[0] * pin.K + pin.Ox;
        var y0 = quad is null ? 0 : quad[1] * pin.K + pin.Oy;
        var w = quad is null ? 0 : quad[2] * pin.K;
        var h = quad is null ? 0 : quad[3] * pin.K;
        var count = 0;
        var first = string.Empty;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = got[y * width + x];
                var b = want[y * width + x];
                if (a.R == b.R && a.G == b.G && a.B == b.B)
                {
                    continue;
                }

                var inside = !exactOnly && x >= x0 && x < x0 + w && y >= y0 && y < y0 + h;
                if (inside
                    && Math.Abs(a.R - b.R) <= 8 && Math.Abs(a.G - b.G) <= 8 && Math.Abs(a.B - b.B) <= 8)
                {
                    continue;
                }

                if (inside && (OnATie(x - x0, w, 48) || OnATie(y - y0, h, sourceHeight)))
                {
                    continue;
                }

                if (count++ == 0)
                {
                    first = $"first at ({x}, {y}) got ({a.R}, {a.G}, {a.B}) want ({b.R}, {b.G}, {b.B}){(inside ? " inside the quad" : string.Empty)}";
                }
            }
        }

        return count == 0 ? null : $"{count} pixels, {first}";
    }

    private List<string> CompareClass(string klass)
    {
        var failures = new List<string>();
        var pins = Pins().Where(p => p.Class == klass).ToList();
        Assert.NotEmpty(pins);
        foreach (var pin in pins)
        {
            var width = 320 * pin.K + 2 * pin.Ox;
            var height = 240 * pin.K + 2 * pin.Oy;
            var got = DrawImage(pin.Scenario, pin.Frame, pin.K, pin.Ox, pin.Oy);
            int wantWidth = 0, wantHeight = 0;
            var want = Invoke(() =>
            {
                var pixels = GetSession().ReadPng(ReferencePath(pin.Reference), out var rw, out var rh);
                wantWidth = rw;
                wantHeight = rh;
                return pixels;
            });
            Assert.Equal((width, height), (wantWidth, wantHeight));
            var diff = Compare(pin, got, want, width, height);
            if (diff is not null)
            {
                failures.Add($"{pin.Name} (rgb {pin.Rgb}, N+{pin.Rel}, x{pin.K}): {diff}");
            }
        }

        _output.WriteLine($"{pins.Count} images of the class {klass}, {failures.Count} differ");
        return failures;
    }

    [AlundraGpuFact]
    public void TheExactImages_AreTheirReferencesPixelForPixel_AtRestAndWhereNoPortraitIsDrawn()
    {
        var failures = CompareClass("exact");
        Assert.True(failures.Count == 0, $"{failures.Count} exact images differ:\n{string.Join("\n", failures.Take(6))}");
    }

    [AlundraGpuFact]
    public void TheTintedImages_AreTheirReferences_OutsideThePortraitExactly_InsideItWithin8Of255()
    {
        var failures = CompareClass("tint8");
        Assert.True(failures.Count == 0, $"{failures.Count} tinted images differ:\n{string.Join("\n", failures.Take(6))}");
    }

    // ---- the order, the control, the first ink -----------------------------------------------------------------------------------------------

    private static (int R, int G, int B) At(Color[] pixels, int x, int y) => (pixels[y * 320 + x].R, pixels[y * 320 + x].G, pixels[y * 320 + x].B);

    /// <summary>S1 at N+16 (image 19): the portrait is over the text frame, the name frame over it, the name text over its frame; the same points with the portrait and the name hidden are
    /// the frame's own colours (the control: the order points move when the elements are shown).</summary>
    [AlundraGpuFact]
    public void TheOrder_PortraitOverTheTextFrame_NameFrameOverIt_NameTextOverTheNameFrame()
    {
        var shown = DrawImage("S1", 19, 1);
        Assert.Equal((72, 48, 32), At(shown, 19, 168));    // the portrait ...
        Assert.Equal((72, 64, 56), At(shown, 71, 171));    // the name frame ...
        Assert.Equal((41, 49, 16), At(shown, 110, 149));   // the first ink texel of "Jess"
        Assert.Equal((100, 149, 237), At(shown, 64, 140)); // the transparent corner cell of the baked name frame shows the clear colour

        var hidden = DrawImage("S1", 19, 1, adjust: vm =>
        {
            vm.Portrait.Visibility = MGUI.Core.UI.Visibility.Collapsed;
            vm.NameBox.Visibility = MGUI.Core.UI.Visibility.Collapsed;
        });
        Assert.Equal((88, 96, 72), At(hidden, 19, 168));    // ... over the text frame's (88, 96, 72)
        Assert.Equal((144, 136, 112), At(hidden, 71, 171)); // ... over the text frame's (144, 136, 112)
    }

    // ---- the sixty names ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>The name box alone, 60 names at x 64, 303 and 150 (the rest, the slide's tail, the middle of the slide), 0 pixel off the SHA-1 of the image the binary's glyph table gives.</summary>
    [AlundraGpuFact]
    public void TheSixtyNames_AtThreePositions_AreTheBinarysGlyphsToThePixel()
    {
        var rows = File.ReadAllLines(F4cAnnex.File_("names-digests.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
        Assert.Equal(180, rows.Count);
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var (name, x, textX, digest) = (row[1], int.Parse(row[3]), int.Parse(row[4]), row[5]);
            var pixels = DrawState(vm =>
            {
                vm.RootVisibility = MGUI.Core.UI.Visibility.Visible;
                vm.Frame.Visibility = MGUI.Core.UI.Visibility.Collapsed;
                vm.Clip.Visibility = MGUI.Core.UI.Visibility.Collapsed;
                vm.NameBox.Left = x;
                vm.NameBox.TextLeft = textX;
                vm.NameBox.Text = name;
                vm.NameBox.Visibility = MGUI.Core.UI.Visibility.Visible;
            });
            var got = Sha1OfRgb(pixels);
            if (got != digest)
            {
                failures.Add($"{row[0]} {name} at x {x}: sha1 {got}, annex {digest}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of 180 name images differ:\n{string.Join("\n", failures.Take(6))}");
    }

    // ---- the twenty-five portraits ------------------------------------------------------------------------------------------------------------------

    /// <summary>Every portrait by its sprite id (docs/plan-e19-f4-annexe/portraits_table.tsv; the one of bank 15 lives under Entities/), at rest at (8, 172 - h) through the view model: the
    /// sprite's own texels, 0 pixel off.</summary>
    [AlundraGpuFact]
    public void EveryPortrait_ByItsSpriteId_IsDrawnAtRestAsItsOwnTexels()
    {
        var rows = File.ReadAllLines(Path.Combine(SpeakerAnnex.Directory(), "portraits_table.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
        Assert.Equal(25, rows.Count);
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var (id, width, height) = (row[3], int.Parse(row[4]), int.Parse(row[5]));
            var pixels = DrawState(vm =>
            {
                vm.RootVisibility = MGUI.Core.UI.Visibility.Visible;
                vm.Frame.Visibility = MGUI.Core.UI.Visibility.Collapsed;
                vm.Clip.Visibility = MGUI.Core.UI.Visibility.Collapsed;
                vm.Portrait.SourceName = id;
                vm.Portrait.Translation = new Vector2(0, 172 - height - 116);
                vm.Portrait.Scale = Vector2.One;
                vm.Portrait.Brightness = 1f;
                vm.Portrait.Visibility = MGUI.Core.UI.Visibility.Visible;
            });

            var want = Invoke(() =>
            {
                var (texture, rect) = GetSession().Assets.ResolveSprite(id);
                Assert.Equal((width, height), (rect.Width, rect.Height));
                var sheet = new Img(texture);
                var compositor = new Compositor(320, 240);
                compositor.Blit(sheet, rect, 8, 172 - height);
                return compositor.P;
            });

            var differing = 0;
            for (var i = 0; i < want.Length; i++)
            {
                if (pixels[i].R != want[i].R || pixels[i].G != want[i].G || pixels[i].B != want[i].B)
                {
                    differing++;
                }
            }

            if (differing > 0)
            {
                failures.Add($"{row[9]} ({id}, {width} x {height}): {differing} pixels differ");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of 25 portraits differ:\n{string.Join("\n", failures)}");
    }

    // ---- the preload ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>F4C2-1: the cost of the first use of a portrait sheet (decode and upload of its PNG, the way the project assets stand-in loads it): the three largest sheets of the 13 are
    /// measured; above 20 ms (about one tick) the plan stops and a preload is to be decided. The best of three is taken for each (the first run pays the JIT).</summary>
    [AlundraGpuFact]
    public void TheThreeLargestPortraitSheets_LoadInUnderOneTick()
    {
        var rows = File.ReadAllLines(Path.Combine(SpeakerAnnex.Directory(), "portraits_table.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
        var results = Invoke(() =>
        {
            var session = GetSession();
            var files = new Dictionary<string, (int W, int H)>();
            foreach (var row in rows)
            {
                var file = SheetFileOf(session.ProjectRoot, row[3]);
                if (!files.ContainsKey(file))
                {
                    using var stream = File.OpenRead(Path.Combine(session.ProjectRoot, file));
                    using var texture = Texture2D.FromStream(Device, stream);
                    files[file] = (texture.Width, texture.Height);
                }
            }

            var timings = new List<(string File, int W, int H, double Ms)>();
            foreach (var (file, size) in files.OrderByDescending(f => (long)f.Value.W * f.Value.H).Take(3))
            {
                var best = double.MaxValue;
                for (var run = 0; run < 3; run++)
                {
                    var watch = Stopwatch.StartNew();
                    using var stream = File.OpenRead(Path.Combine(session.ProjectRoot, file));
                    using var texture = Texture2D.FromStream(Device, stream);
                    watch.Stop();
                    best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
                }

                timings.Add((file, size.W, size.H, best));
            }

            return (Distinct: files.Count, Timings: timings);
        });

        foreach (var t in results.Timings)
        {
            _output.WriteLine($"{t.File} {t.W} x {t.H}: {t.Ms:F1} ms (best of 3)");
        }

        _output.WriteLine($"{results.Distinct} distinct portrait sheets");
        Assert.All(results.Timings, t => Assert.True(t.Ms < 20, $"{t.File} {t.W} x {t.H} takes {t.Ms:F1} ms to load: a preload is to be decided (plan F4C2-1)"));
    }

    /// <summary>The texture file of the sheet of a sprite: sprite, sprite sheet, texture (the three asset files of the export).</summary>
    private static string SheetFileOf(string root, string spriteId)
    {
        using var infos = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "AssetInfos.json")));
        var files = infos.RootElement.GetProperty("asset_infos").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("file_name").GetString()!);
        using var sprite = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, files[spriteId])));
        using var sheet = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, files[sprite.RootElement.GetProperty("sprite_sheet_asset_id").GetString()!])));
        return files[sheet.RootElement.GetProperty("texture_asset_id").GetString()!];
    }
}
