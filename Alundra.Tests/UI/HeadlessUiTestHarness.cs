using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Alundra.Tests.UI;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-17, engine ADR-0001): a REDUCED copy of
/// <c>CasaEngine.Tests.UI.HeadlessUiTestHarness</c> (<c>CasaEngineMonogame/CasaEngine.Tests/UI/HeadlessUiTestHarness.cs</c>),
/// which is internal to that assembly and not referenced by <c>Alundra.Tests</c>. Stands up an
/// <see cref="MGDesktop"/> with no graphics device, for tests that build or inspect a real MGUI tree
/// (<see cref="AlundraInventoryScreen"/>'s own XAML) but never draw one.
/// <para/>
/// Kept intentionally small: only what loading <c>InventoryScreen.xaml</c> and reading its elements by
/// name needs. A divergence from the engine's own copy is possible if that harness evolves (ADR-0001's
/// own documented consequence) - this is not a shared, refactored-out utility on purpose.
/// </summary>
internal static class HeadlessUiTestHarness
{
    public const int DefaultSurfaceWidth = 640;
    public const int DefaultSurfaceHeight = 480;

    /// <param name="imageSizes">E19.f2b1c: the size of the images a test measures (by asset name or id), the others being 16 x 16.</param>
    public static (MGDesktop Desktop, HeadlessRuntime Runtime) NewDesktop(
        int width = DefaultSurfaceWidth,
        int height = DefaultSurfaceHeight,
        IReadOnlyDictionary<string, (int Width, int Height)>? imageSizes = null)
    {
        HeadlessRuntime runtime = new(new Rectangle(0, 0, width, height), imageSizes);
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();
        return (desktop, runtime);
    }

    internal sealed class HeadlessRuntime : IUIDesktopRuntime
    {
        private ITextMeasurementEngine _textEngine;

        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "TestSans";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;

        public event EventHandler<EventArgs>? EndUpdate
        {
            add { }
            remove { }
        }

        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                ITextMeasurementEngine previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new(previous, _textEngine));
            }
        }

        public HeadlessRuntime(Rectangle surfaceBounds, IReadOnlyDictionary<string, (int Width, int Height)>? imageSizes = null)
        {
            Surface = new HeadlessSurface(surfaceBounds, new HeadlessRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new HeadlessAssetProvider(imageSizes);
            _textEngine = new MeasuringTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => throw new NotSupportedException($"{nameof(HeadlessUiTestHarness)} never draws.");

        /// <summary>Gives the surface new bounds, as a window resize gives a game's view a new rectangle (view-local,
        /// so the origin stays 0, 0): <see cref="MGDesktop.ValidScreenBounds"/> reads them on every call.</summary>
        public void Resize(int width, int height) => ((HeadlessSurface)Surface).Bounds = new Rectangle(0, 0, width, height);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView view)
        {
        }
    }

    private sealed class HeadlessSurface : IUISurface
    {
        private readonly IUIRenderTarget _renderTarget;

        public HeadlessSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            Bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle Bounds { get; set; }

        public Rectangle GetBounds() => Bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class HeadlessAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, HeadlessImageResource> _images = new(StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyDictionary<string, (int Width, int Height)>? _sizes;

        public HeadlessAssetProvider(IReadOnlyDictionary<string, (int Width, int Height)>? sizes) => _sizes = sizes;

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out HeadlessImageResource? image))
            {
                var (width, height) = _sizes != null && _sizes.TryGetValue(assetName, out var size) ? size : (16, 16);
                image = new HeadlessImageResource(assetName, width, height);
                _images[assetName] = image;
            }

            return image;
        }

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = LoadImage(assetName);
            return true;
        }

        /// <summary>E19.f2b1c: a name a test gave a size to is an image of that size (a sprite, as the host resolves a catalogued name); any other name resolves nothing, as before.</summary>
        public bool TryResolveImage(string name, out IUIImageResource? image, out Rectangle? sourceRect)
        {
            sourceRect = null;
            image = null;
            if (_sizes == null || !_sizes.ContainsKey(name))
            {
                return false;
            }

            image = LoadImage(name);
            return true;
        }
    }

    private class HeadlessImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public HeadlessImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class HeadlessRenderTarget : HeadlessImageResource, IUIRenderTarget
    {
        public HeadlessRenderTarget(int width, int height)
            : base("headless-ui-test-render-target", width, height)
        {
        }
    }

    private sealed class MeasuringTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFamily;

        public MeasuringTextEngine(string defaultFamily)
        {
            _defaultFamily = defaultFamily;
        }

        public ResolvedFont ResolveFont(FontSpec spec)
        {
            int size = Math.Max(1, spec.Size);
            FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family)
                ? FontSpec.Normal(_defaultFamily, size)
                : spec;

            return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size, Math.Max(1.0f, size * 0.5f), Vector2.Zero, false, new object());
        }

        public Vector2 MeasureText(ResolvedFont font, string text)
        {
            float width = (text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
            return new Vector2(width, font.LineHeight);
        }

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char character)
            => new(0.0f, Math.Max(font.SpaceWidth, 1.0f), 0.0f, font.LineHeight);

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void InvalidateCache()
        {
        }
    }
}
