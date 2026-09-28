using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.FontStashSharp;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c, T1 (docs/plan-e15-yarn.md, "E15.c - La DLL lit le Yarn", contract item 4; ADR-0008): proves that
/// the dialogue box's OWN text path - <c>DialogueScreen</c>'s embedded markup, <c>lblLine</c> (an
/// <c>MGTextBlock</c> with <c>WrapText="True"</c>, <see cref="DialogueScreen"/>'s constructor wiring
/// <c>FontFamily = "font3"</c>, exactly <c>CasaEngineMonogame/CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs:358-367</c>'s
/// <c>SetText(text, MGTextInvalidationMode.ReflowLocal)</c> with inline-markup parsing on - keeps, measures
/// and does not mis-treat font3 glyphs 16-29 (the ones ADR-0006/ADR-0008 draw for <c>\W</c> codes and the raw
/// ETC bytes <c>0x1A</c>/<c>0x1C</c>), using the REAL exported <c>alundra-project/UI/font3.fnt</c> +
/// <c>Textures/font3.png</c>, registered on MGUI exactly as <c>UIFontRegistry</c> registers it for the
/// inventory (<c>FontStashSharpTextEngine.AddStaticFont</c>) and as ADR-0008 has the dialogue box do.
/// <para/>
/// <b>Why a real <see cref="GraphicsDevice"/>, not the fake text engine <c>HeadlessUiTestHarness</c> (a
/// <c>CasaEngine.Tests</c>-internal type, unreachable from here) uses:</b>
/// <c>FontStashSharp.StaticSpriteFont.FromBMFont</c> - the same call
/// <c>CasaEngine.Framework.Assets.Loaders.BitmapFontLoader</c> makes for every real bitmap font, including
/// font3 - needs a non-null <see cref="Texture2D"/> for its page (<c>CasaEngine.Tests</c>'s own
/// <c>FontStashSharpBitmapFontRegistrationTests</c> confirmed this by reflecting FontStashSharp.MonoGame
/// 1.5.6), and a <see cref="Texture2D"/> needs a live device. This test mirrors
/// <c>MGUI.Tests.Integration.GpuDeviceHost</c>'s technique (that class is <c>internal</c> to
/// <c>MGUI.Tests</c>, also unreachable here): a non-visible <see cref="Game"/> whose
/// <see cref="Game.RunOneFrame"/> creates a device without ever calling <see cref="Game.Run()"/>
/// (MonoGame's SDL platform only shows the window from the run loop), on one dedicated background thread
/// that every MonoGame call in this file uses, because <c>Microsoft.Xna.Framework.Threading</c> records
/// whichever thread first touches it as "the UI thread" and throws off it.
/// <para/>
/// <b>Test with an <see cref="MGDesktop"/>, never a <c>UIRoot</c></b> (project rule: <c>UIRoot</c> is sealed
/// and only buildable from a live <c>CasaEngineGame</c>): this file hand-builds a minimal
/// <see cref="IUIDesktopRuntime"/> - the same seam <c>HeadlessUiTestHarness</c> uses - whose
/// <see cref="ITextMeasurementEngine"/> is a REAL <c>MGUI.FontStashSharp.FontStashSharpTextEngine</c>
/// (not the fake, deterministic measuring engine that harness uses for other tests), so glyph advances come
/// from font3 itself, not a placeholder metric.
/// <para/>
/// E15.e, T1 (docs/plan-e15-yarn.md, "E15.e - Les accents de font3", D-E15-16) adds one more case:
/// <see cref="AccentedGlyph_MeasuredAdvance_MatchesTheExportedXAdvance"/> measures 'é' the same way,
/// through <c>alundra-project/UI/font3.fnt</c>. The preferred way to prove D-E15-16 immediately - build
/// font3 itself by calling <c>FontWriter.ConvertFont</c> straight from <c>data-extracted/</c> into a
/// throwaway temp project - is NOT used here: <c>FontWriter</c> lives in the
/// <c>alundra-casaengine-project-converter</c> project, which this test project (<c>Alundra.Tests</c>)
/// does not reference, and this task's scope is limited to
/// <c>alundra-casaengine-project-converter/Writers/FontWriter.cs</c>,
/// <c>alundra-casaengine-project-converter.Tests/FontWriterTests.cs</c> and this file - it excludes
/// adding a <c>ProjectReference</c> to <c>Alundra.Tests.csproj</c>. This test instead reads the checked-in
/// export, exactly like every other test in this file, and is marked <c>Skip</c> (naming this task's T2,
/// docs/plan-e15-yarn.md's E15.e) because that export was confirmed STILL STALE (the old CP850 mapping:
/// <c>char id=233</c> currently sits at x=32,y=128, not D-E15-16's x=144,y=224) when this test was
/// written - T2 has not run yet. Remove the <c>Skip</c> once T2's export lands.
/// </summary>
public sealed class AlundraFont3GlyphTests
{
    // font3 glyphs 16-29 (ADR-0006 §Context / ADR-0008 §Context): bullet, quotes, star, arrows, buttons.
    // 28 and 29 are the ones the plan calls out by name: U+001C and U+001D are control characters some
    // libraries class as separators, so they are the likeliest to be dropped or treated as a break
    // (on .NET 9, char.IsWhiteSpace is false for both; what matters here is what MGUI does with them).
    private static readonly int[] GlyphCodes = { 16, 18, 22, 26, 28, 29 };

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "UI", "font3.fnt")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraFont3GlyphTests: no 'alundra-project/UI/font3.fnt' found above '{AppContext.BaseDirectory}' - "
            + "this test needs the real converter export of font3 (docs/plan-e15-yarn.md, E15.c, T1) and cannot "
            + "self-skip without one.");
    }

    /// <summary>Reads <c>xadvance</c> straight out of the real exported <c>.fnt</c> text for a given character
    /// id, so the expected value in this test is font3's own declared advance, never a value re-typed by hand.</summary>
    private static Dictionary<int, int> ReadXAdvances(string fntPath, IEnumerable<int> ids)
    {
        var wanted = new HashSet<int>(ids);
        var result = new Dictionary<int, int>();
        var charLine = new Regex(@"^char id=(?<id>\d+)\s.*\sxadvance=(?<adv>-?\d+)\s", RegexOptions.Compiled);

        foreach (var line in File.ReadLines(fntPath))
        {
            var match = charLine.Match(line);
            if (!match.Success)
            {
                continue;
            }

            int id = int.Parse(match.Groups["id"].Value);
            if (wanted.Contains(id))
            {
                result[id] = int.Parse(match.Groups["adv"].Value);
            }
        }

        foreach (int id in wanted)
        {
            Assert.True(result.ContainsKey(id), $"font3.fnt has no 'char id={id}' line - the test's own assumption about the exported font is wrong.");
        }

        return result;
    }

    // ── A dedicated, single-threaded, real (but never shown) GraphicsDevice ─────────────────────────────
    // Mirrors MGUI.Tests.Integration.GpuDeviceHost (internal to MGUI.Tests, so re-implemented here rather
    // than referenced) purely to satisfy StaticSpriteFont.FromBMFont's non-null-Texture2D requirement.

    private sealed class HiddenGame : Game
    {
        public readonly GraphicsDeviceManager Gdm;

        public HiddenGame()
        {
            Gdm = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 128,
                PreferredBackBufferHeight = 128,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
                SynchronizeWithVerticalRetrace = false,
            };
            IsFixedTimeStep = false;
        }
    }

    private sealed class GpuThread
    {
        public static readonly GpuThread Instance = new();

        private readonly BlockingCollection<Action> _work = new();
        private readonly Thread _thread;
        private HiddenGame? _game;

        private GpuThread()
        {
            _thread = new Thread(RunLoop) { IsBackground = true, Name = "Alundra-Font3-Test-GPU-Thread" };
            _thread.Start();
        }

        public GraphicsDevice GraphicsDevice => _game!.GraphicsDevice;

        public void EnsureDevice()
        {
            Invoke(() =>
            {
                if (_game == null)
                {
                    _game = new HiddenGame();
                    _game.RunOneFrame();
                    if (_game.GraphicsDevice == null)
                    {
                        throw new InvalidOperationException($"{nameof(HiddenGame)}.GraphicsDevice was still null after RunOneFrame().");
                    }
                }
            });
        }

        public void Invoke(Action action)
        {
            // Re-entrant: work queued from the GPU thread itself would wait on a queue only this thread
            // drains - a deadlock - so it runs inline instead.
            if (Thread.CurrentThread == _thread)
            {
                action();
                return;
            }

            ExceptionDispatchInfo? capturedError = null;
            using ManualResetEventSlim done = new(false);

            _work.Add(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedError = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    done.Set();
                }
            });

            done.Wait();
            capturedError?.Throw();
        }

        public T Invoke<T>(Func<T> func)
        {
            T result = default!;
            Invoke(() => { result = func(); });
            return result;
        }

        private void RunLoop()
        {
            foreach (Action action in _work.GetConsumingEnumerable())
            {
                action();
            }
        }
    }

    // ── Minimal IUIDesktopRuntime carrying a REAL FontStashSharpTextEngine (no CasaEngine game/world) ────

    private sealed class RealFontRuntime : IUIDesktopRuntime
    {
        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "font3";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; } = new PlaceholderAssetProvider();
        public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

        private ITextMeasurementEngine _textEngine;
        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                var previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new EventArgs<ITextMeasurementEngine>(previous, _textEngine));
            }
        }

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;

        public event EventHandler<EventArgs> EndUpdate
        {
            add { }
            remove { }
        }

        public RealFontRuntime(Rectangle bounds, FontStashSharpTextEngine engine)
        {
            Surface = new PlaceholderSurface(bounds, new PlaceholderRenderTarget(bounds.Width, bounds.Height));
            _textEngine = engine;
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => throw new NotSupportedException($"{nameof(AlundraFont3GlyphTests)} never draws.");

        public void RegisterView(IUIView view)
        {
        }
    }

    private sealed class PlaceholderSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public PlaceholderSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;
        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private class PlaceholderImageResource : IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public PlaceholderImageResource(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    private sealed class PlaceholderRenderTarget : PlaceholderImageResource, IUIRenderTarget
    {
        public PlaceholderRenderTarget(int width, int height) : base(width, height)
        {
        }
    }

    private sealed class PlaceholderAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, PlaceholderImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out var image))
            {
                image = new PlaceholderImageResource(16, 16);
                _images[assetName] = image;
            }

            return image;
        }

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = LoadImage(assetName);
            return true;
        }
    }

    /// <summary>Builds a desktop whose text engine is a real <see cref="FontStashSharpTextEngine"/> with the
    /// real exported font3 registered under family "font3" (<see cref="FontStashSharpTextEngine.AddStaticFont"/>,
    /// exactly what <c>CasaEngine.Framework.UI.UIFontRegistry.Register</c> does for the inventory screen and
    /// what ADR-0008 has the dialogue presenter do), on the dedicated GPU thread.</summary>
    private static (MGDesktop Desktop, string FontFamily) NewFont3Desktop(int surfaceWidth, int surfaceHeight, string fntPath, string pagePath)
    {
        GpuThread.Instance.EnsureDevice();

        return GpuThread.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuThread.Instance.GraphicsDevice;

            byte[] pageBytes = File.ReadAllBytes(pagePath);
            Texture2D pageTexture;
            using (var stream = new MemoryStream(pageBytes))
            {
                pageTexture = Texture2D.FromStream(device, stream);
            }

            string fntText = File.ReadAllText(fntPath);
            SpriteFontBase font3 = StaticSpriteFont.FromBMFont(fntText, _ => new TextureWithOffset(pageTexture));

            var engine = new FontStashSharpTextEngine();
            engine.AddStaticFont("font3", CustomFontStyles.Normal, font3);

            var runtime = new RealFontRuntime(new Rectangle(0, 0, surfaceWidth, surfaceHeight), engine);
            var desktop = new MGDesktop(runtime);
            desktop.LoadDefaultResources();

            return (desktop, "font3");
        });
    }

    /// <summary>Builds the REAL dialogue box (embedded <c>DialogueScreen.xaml</c>, <c>lblLine</c> wired to
    /// font3) and shows <paramref name="text"/> through it, exactly along <c>DialogueScreen.cs:358-367</c>'s
    /// <c>RefreshLine</c> -&gt; <c>SetText(text, MGTextInvalidationMode.ReflowLocal)</c> path -&gt;
    /// <c>ResizeToFitContent</c> -&gt; <c>MGWindow.ApplySizeToContent</c>, which lays the window out
    /// synchronously (no desktop.Update() loop needed), so <c>lblLine.Lines</c> reflects a real wrap pass at
    /// the window's real width.</summary>
    private static MGTextBlock ShowLine(MGDesktop desktop, string fontFamily, string text)
    {
        return GpuThread.Instance.Invoke(() =>
        {
            var service = new DialogueService();
            var screen = new DialogueScreen(service, static () => { }, fontFamily);
            MGWindow window = screen.BuildWindow(desktop);
            screen.Show();
            service.ShowLine(new DialogueLine(text));

            Assert.True(window.TryGetElementByName("lblLine", out MGTextBlock line));
            Assert.Equal(fontFamily, line.FontFamily);
            Assert.True(line.WrapText);
            return line;
        });
    }

    private static string ProjectRoot => FindProjectRoot();
    private static string Font3FntPath => Path.Combine(ProjectRoot, "UI", "font3.fnt");
    private static string Font3PagePath => Path.Combine(ProjectRoot, "UI", "Textures", "font3.png");

    /// <summary>Concatenates every <see cref="MGTextRunText"/> in the parsed (pre-wrap) <see cref="MGTextBlock.Runs"/>,
    /// which is what proves a character was neither dropped nor replaced by <c>SetText</c>'s markup parsing -
    /// <see cref="MGTextBlock.Lines"/> alone would not, since wrapping can split (but must not otherwise alter)
    /// a run's text.</summary>
    private static string ReconstructText(MGTextBlock line)
        => string.Concat(line.Runs.OfType<MGTextRunText>().Select(r => r.Text));

    [Fact]
    public void Font3Glyphs16To29_AreMeasuredAsTheFontsOwnGlyph_ThroughTheDialogueBoxsRealTextEngine()
    {
        var xadvances = ReadXAdvances(Font3FntPath, GlyphCodes);
        (MGDesktop desktop, string fontFamily) = NewFont3Desktop(640, 480, Font3FntPath, Font3PagePath);

        GpuThread.Instance.Invoke(() =>
        {
            MGTextBlock line = ShowLine(desktop, fontFamily, "seed");

            foreach (int code in GlyphCodes)
            {
                char glyph = (char)code;
                string with = "a" + glyph + "b";
                string without = "ab";

                float widthWith = line.MeasureText(with, false, false).X;
                float widthWithout = line.MeasureText(without, false, false).X;
                float measuredAdvance = widthWith - widthWithout;

                Assert.True(
                    Math.Abs(measuredAdvance - xadvances[code]) < 0.5f,
                    $"font3 code {code}: measured advance {measuredAdvance} does not match font3's own xadvance {xadvances[code]} " +
                    $"(\"a{{(char){code}}}b\"={widthWith} vs \"ab\"={widthWithout}) - MGUI is not measuring it as the font's own glyph.");

                // Not collapsible whitespace: a genuinely dropped/zero-width character would make this delta 0.
                Assert.True(measuredAdvance > 0.5f, $"font3 code {code}: measured advance is ~0 - the character reads as dropped or zero-width, not as a glyph.");
            }
        });
    }

    [Theory]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(22)]
    [InlineData(26)]
    [InlineData(28)] // U+001C, a control character: the plan's specific worry.
    [InlineData(29)] // U+001D, a control character: the plan's specific worry.
    public void Font3Glyph_IsKeptVerbatim_ThroughTheDialogueBoxsMarkupParsing_AtStartMiddleAndEnd(int code)
    {
        (MGDesktop desktop, string fontFamily) = NewFont3Desktop(640, 480, Font3FntPath, Font3PagePath);
        char glyph = (char)code;

        GpuThread.Instance.Invoke(() =>
        {
            string text = glyph + "Bonjour" + glyph + "monde" + glyph;
            MGTextBlock line = ShowLine(desktop, fontFamily, text);

            // Not dropped, not replaced: the parsed (pre-wrap) runs reconstruct the exact original string,
            // glyph included at every position (leading, embedded, trailing).
            Assert.Equal(text, ReconstructText(line));

            // Not collapsed into nothing / not treated as an empty line: with plenty of width, this short
            // line-let stays a single visual line.
            Assert.Single(line.Lines);
            MGTextLine only = line.Lines[0];
            Assert.False(only.EndsInLinebreakCharacter);

            // Not treated as a line-break character: reconstructing the WRAPPED line's own runs (post-layout)
            // still contains the glyph at start and end, i.e. it was not consumed as if it split lines.
            string wrappedText = string.Concat(only.Runs.OfType<MGTextRunText>().Select(r => r.Text));
            Assert.Equal(text, wrappedText);
        });
    }

    [Fact]
    public void Font3Glyph_BetweenTwoWords_DoesNotIntroduceAWrapPoint_UnlikeARealSpace()
    {
        (MGDesktop desktop, string fontFamily) = NewFont3Desktop(400, 480, Font3FntPath, Font3PagePath);

        GpuThread.Instance.Invoke(() =>
        {
            // 'w' is one of font3's widest lowercase letters (xadvance=11 per font3.fnt). On a 400 px surface
            // the dialogue window's text area is 284 px wide (DialogueScreen.cs:148, window 320 px minus 36):
            // one 16-letter word (176 px) fits, two of them with a space between (356 px) cannot.
            const string wordA = "wwwwwwwwwwwwwwww"; // 16 chars
            const string wordB = "wwwwwwwwwwwwwwww"; // 16 chars

            // Control: a REAL space between the two words is a valid wrap point (MGUI's default WordDelimiters
            // are ' ' and '-') - proves this desktop/width combination is actually capable of wrapping here.
            MGTextBlock spaced = ShowLine(desktop, fontFamily, wordA + " " + wordB);
            Assert.True(spaced.Lines.Count >= 2, "test premise: this width was expected to force a wrap at the real space between the two words.");

            // The glyph in the exact same position instead of the space must not act as a delimiter: the
            // combined text is not split into "wordA" / glyph+"wordB" the way the space split it above.
            MGTextBlock glyphed = ShowLine(desktop, fontFamily, wordA + (char)18 + wordB);
            string reconstructed = ReconstructText(glyphed);
            Assert.Equal(wordA + (char)18 + wordB, reconstructed);

            bool anyLineStartsRightAfterTheGlyph = glyphed.Lines
                .Skip(1)
                .Any(l => l.Runs.OfType<MGTextRunText>().Any(r => r.Text.Length > 0 && r.Text[0] == wordB[0]) &&
                          l.Runs.OfType<MGTextRunText>().First().Text == wordB);
            Assert.False(
                anyLineStartsRightAfterTheGlyph,
                "the font3 glyph acted as a word-wrap delimiter exactly like the control space did - it must not.");
        });
    }

    /// <summary>
    /// E15.e, T1 (docs/plan-e15-yarn.md, "E15.e - Les accents de font3"; D-E15-16; ADR-0009): 'é' (U+00E9,
    /// raw code 0xE9 = 233 under D-E15-16, cell x=144,y=224) must measure, through the REAL dialogue box
    /// text path, to exactly font3.fnt's own declared xadvance for that cell - not a value re-typed by
    /// hand. <c>Skip</c>ped (see the class doc comment): the checked-in <c>alundra-project/UI/font3.fnt</c>
    /// was confirmed still built by the OLD CP850 mapping when this test was written (T2,
    /// docs/plan-e15-yarn.md's E15.e, has not exported yet) - remove the <c>Skip</c> once it has.
    /// </summary>
    [Fact(Skip = "docs/plan-e15-yarn.md E15.e T2 has not exported font3.fnt with D-E15-16's mapping yet " +
        "(alundra-project/UI/font3.fnt still has 'é' at the old CP850 cell, not x=144,y=224); remove this " +
        "Skip once T2 lands.")]
    public void AccentedGlyph_MeasuredAdvance_MatchesTheExportedXAdvance()
    {
        const int codepoint = 0xE9; // 'é', D-E15-16: raw code 0xE9 -> codepoint U+00E9 (identity).

        // The exported .fnt must draw 'é' from cell 233 (x=144, y=224), where the original's '}Y' draws
        // it - not from CP850's cell 130 (x=32, y=128), a comma-like glyph.
        var cellLine = File.ReadLines(Font3FntPath).Single(l => l.StartsWith($"char id={codepoint} ", StringComparison.Ordinal));
        Assert.Contains(" x=144 ", cellLine, StringComparison.Ordinal);
        Assert.Contains(" y=224 ", cellLine, StringComparison.Ordinal);

        var xadvances = ReadXAdvances(Font3FntPath, new[] { codepoint });
        (MGDesktop desktop, string fontFamily) = NewFont3Desktop(640, 480, Font3FntPath, Font3PagePath);

        GpuThread.Instance.Invoke(() =>
        {
            MGTextBlock line = ShowLine(desktop, fontFamily, "seed");

            char glyph = (char)codepoint;
            string with = "a" + glyph + "b";
            string without = "ab";

            float widthWith = line.MeasureText(with, false, false).X;
            float widthWithout = line.MeasureText(without, false, false).X;
            float measuredAdvance = widthWith - widthWithout;

            Assert.True(
                Math.Abs(measuredAdvance - xadvances[codepoint]) < 0.5f,
                $"'é' (cell {codepoint}): measured advance {measuredAdvance} does not match font3.fnt's own "
                + $"xadvance {xadvances[codepoint]} for that cell "
                + $"(\"a{{(char){codepoint}}}b\"={widthWith} vs \"ab\"={widthWithout}).");

            Assert.True(measuredAdvance > 0.5f, "'é' measured advance is ~0 - it reads as dropped or zero-width.");
        });
    }
}
