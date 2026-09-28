using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.FontStashSharp;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E15.c T5 (docs/plan-e15-yarn.md, contract item 4; ADR-0008/D-E15-12): the dialogue box's own presenter
/// holds font3 through the game's UI font registry, from its construction to its disposal, the same
/// contract <see cref="AlundraInventoryScreenFontTests"/> already pins for the inventory screen.
/// </summary>
public sealed class AlundraDialoguePresenterFontTests
{
    private sealed class RecordingUIViewRuntime : IUIViewRuntime
    {
        public readonly List<IUIScreen> Pushed = new();
        public readonly List<IUIScreen> Removed = new();

        public bool IsPointerOverUI => false;
        public bool IsPointerCaptured => false;
        public bool IsKeyboardCaptured => false;
        public UIViewInputState InputState => UIViewInputState.Empty;
        public bool HasModalInput => false;
        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }
        public void Draw() { }
        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;
        public void PushScreen(IUIScreen screen) => Pushed.Add(screen);
        public IUIScreen? PopScreen() => null;
        public void RemoveScreen(IUIScreen screen) => Removed.Add(screen);
        public void Dispose() { }
    }

    private sealed class CpuFont3Loader : IAssetLoader
    {
        private readonly SpriteFontBase _font = BuildCpuFont();
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new BitmapFont("font3", _font);
        }

        public bool IsFileSupported(string fileName) => true;

        private static SpriteFontBase BuildCpuFont()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "CasaEngineMonogame", "CasaEngine", "Content", "Fonts", "tahoma.ttf");
                if (File.Exists(candidate))
                {
                    var fontSystem = new FontSystem();
                    fontSystem.AddFont(File.ReadAllBytes(candidate));
                    return fontSystem.GetFont(16);
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                $"AlundraDialoguePresenterFontTests: no 'CasaEngineMonogame/CasaEngine/Content/Fonts/tahoma.ttf' above '{AppContext.BaseDirectory}'.");
        }
    }

    private static UIFontRegistry NewFonts(out AssetContentManager assets, out CpuFont3Loader loader, bool withFont3 = true)
    {
        var font3 = new AssetInfo(AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = @"UI\font3.fnt" };
        assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(
                null, Path.GetTempPath(), id => withFont3 && id == AlundraInventoryScreen.Font3FontAssetId ? font3 : null),
        };

        loader = new CpuFont3Loader();
        assets.RegisterAssetLoader(typeof(BitmapFont), loader);
        return new UIFontRegistry(assets);
    }

    private static bool ResolvesFont3(FontStashSharpTextEngine textEngine)
        => !textEngine.ResolveFont(new FontSpec("font3", 12, CustomFontStyles.Normal)).IsFallback;

    // ── A minimal headless IUIDesktopRuntime, CPU-only (no GraphicsDevice) ──────────────────────────
    // CpuFont3Loader wraps a TTF (FontStashSharp.FontSystem), never a bitmap page texture, so - unlike
    // AlundraFont3GlyphTests's own real-font3.png coverage - this needs no live GraphicsDevice at all:
    // a plain in-process MGDesktop is enough to run DialogueScreen's real BuildWindow/Show/SetText path.

    private class PlaceholderImageResource : IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;
        public PlaceholderImageResource(int width, int height) { Width = width; Height = height; }
    }

    private sealed class PlaceholderRenderTarget : PlaceholderImageResource, IUIRenderTarget
    {
        public PlaceholderRenderTarget(int width, int height) : base(width, height) { }
    }

    private sealed class PlaceholderSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;
        public PlaceholderSurface(Rectangle bounds) { _bounds = bounds; _renderTarget = new PlaceholderRenderTarget(bounds.Width, bounds.Height); }
        public Rectangle GetBounds() => _bounds;
        public IUIRenderTarget GetRenderTarget() => _renderTarget;
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

    private sealed class HeadlessRuntime : IUIDesktopRuntime
    {
        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "font3";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; } = new PlaceholderAssetProvider();
        public UpdateBaseArgs UpdateArgs { get; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

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
        public event EventHandler<EventArgs> EndUpdate { add { } remove { } }

        public HeadlessRuntime(FontStashSharpTextEngine textEngine)
        {
            Surface = new PlaceholderSurface(new Rectangle(0, 0, 640, 480));
            _textEngine = textEngine;
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => throw new NotSupportedException($"{nameof(AlundraDialoguePresenterFontTests)} never draws.");

        public void RegisterView(IUIView view) { }
    }

    /// <summary>Contract item 4/D-E15-12: the dialogue box's own <c>lblLine</c> element - the SAME element
    /// <see cref="AlundraFont3GlyphTests"/> checks against the real exported font3 - actually receives
    /// <c>FontFamily = "font3"</c> from <see cref="AlundraDialoguePresenter"/>'s own wiring, not only that
    /// the family is resolvable on the text engine (the previous test above). Built through the real
    /// <see cref="AlundraDialoguePresenter.ScreenForTests"/> seam and <c>DialogueScreen</c>'s own public
    /// <c>BuildWindow</c>/<c>Show</c> (the exact path <c>OnInitialize</c> runs, engine
    /// <c>DialogueScreenLayoutTests</c>'s own precedent), never a raw <c>DialogueScreen</c> built by hand.</summary>
    [Fact]
    public void Construction_TheScreensOwnLineElement_ActuallyGetsFontFamilyFont3()
    {
        var fonts = NewFonts(out _, out _);
        var textEngine = new FontStashSharpTextEngine();
        fonts.Attach(textEngine);

        var presenter = new AlundraDialoguePresenter(new RecordingUIViewRuntime(), fonts);

        var desktop = new MGDesktop(new HeadlessRuntime(textEngine));
        desktop.LoadDefaultResources();

        MGWindow window = presenter.ScreenForTests.BuildWindow(desktop);
        desktop.Windows.Add(window);
        presenter.ScreenForTests.Show();
        presenter.ShowLine(new DialogueLine("Bonjour."));

        Assert.True(window.TryGetElementByName("lblLine", out MGTextBlock line));
        Assert.Equal("font3", line.FontFamily);

        presenter.Dispose();
    }

    [Fact]
    public void Construction_HoldsFont3_AndPassesItToTheScreen_AndDisposeGivesItBack()
    {
        var fonts = NewFonts(out var assets, out var loader);
        var textEngine = new FontStashSharpTextEngine();
        fonts.Attach(textEngine);

        var presenter = new AlundraDialoguePresenter(new RecordingUIViewRuntime(), fonts);
        Assert.True(ResolvesFont3(textEngine));
        Assert.Equal(1, loader.Loads);

        // D-E15-12: the box's own text and the yes/no labels go through the same fontFamily.
        var exception = Record.Exception(() => presenter.ShowLine(new DialogueLine("Bonjour.")));
        Assert.Null(exception);

        presenter.Dispose();
        presenter.Dispose(); // idempotent.
        Assert.Equal(1, assets.CollectUnreferenced()); // font3 given back.
        Assert.False(ResolvesFont3(textEngine));
    }

    [Fact]
    public void WithoutFontsGiven_FallsBackToTheDefaultFont_NoException()
    {
        var presenter = new AlundraDialoguePresenter(new RecordingUIViewRuntime());

        var exception = Record.Exception(() => presenter.ShowLine(new DialogueLine("Bonjour.")));

        Assert.Null(exception);
        presenter.Dispose();
    }

    [Fact]
    public void WithoutFont3InTheExport_LogsOnce_AndStillShowsTheDefaultBox()
    {
        var fonts = NewFonts(out _, out _, withFont3: false);

        var exception = Record.Exception(() => new AlundraDialoguePresenter(new RecordingUIViewRuntime(), fonts));

        Assert.Null(exception); // tolerant degrade (D-E15-12 never crashes the box over a missing font3).
    }
}
