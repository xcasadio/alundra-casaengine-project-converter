using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E13.d D5.f (engine ADR-0036, plan D-E13D-18). Found in game (D6): after a map change, the inventory
/// texts fell back to the default TTF font, because the engine rebuilds every UI text engine with each world
/// and the screen had registered font3 once per process. The screen now holds font3 through the game's UI
/// font registry, from its construction to its disposal, and the registry gives it to every text engine.
/// <para/>
/// A real font3 needs a <c>Texture2D</c>, hence a graphics device; the test loader builds the
/// <see cref="BitmapFont"/> on a fixed-size font rasterized on the CPU from a TTF, the substitute the engine's
/// own font tests use. The real font3 is proven by the in-game check (D5.f.3).
/// </summary>
public sealed class AlundraInventoryScreenFontTests
{
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
                $"AlundraInventoryScreenFontTests: no 'CasaEngineMonogame/CasaEngine/Content/Fonts/tahoma.ttf' above '{AppContext.BaseDirectory}'.");
        }
    }

    /// <summary>The screen's envelope, as the screen only needs it held (these tests never build its window).</summary>
    private sealed class ScreenEnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new UIScreenAsset { SourceXamlFile = "InventoryScreen.xaml" };

        public bool IsFileSupported(string fileName) => true;
    }

    private static UIFontRegistry NewFonts(out AssetContentManager assets, out CpuFont3Loader loader, bool withFont3 = true)
    {
        var font3 = new AssetInfo(AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = @"UI\font3.fnt" };
        var screenId = Guid.Parse(AlundraInventoryScreen.ScreenAssetId);
        var screen = new AssetInfo(screenId) { Name = "InventoryScreen", FileName = @"UI\Screens\InventoryScreen.uiscreen" };
        assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(
                null,
                Path.GetTempPath(),
                id => withFont3 && id == AlundraInventoryScreen.Font3FontAssetId ? font3 : id == screenId ? screen : null),
        };

        loader = new CpuFont3Loader();
        assets.RegisterAssetLoader(typeof(BitmapFont), loader);
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());
        return new UIFontRegistry(assets);
    }

    private static bool ResolvesFont3(FontStashSharpTextEngine textEngine)
        => !textEngine.ResolveFont(new FontSpec("font3", 12, CustomFontStyles.Normal)).IsFallback;

    [Fact]
    public void Construction_HoldsFont3_AndDispose_GivesItBack()
    {
        var fonts = NewFonts(out var assets, out var loader);
        var textEngine = new FontStashSharpTextEngine();
        fonts.Attach(textEngine);

        var screen = new AlundraInventoryScreen(assets, fonts);
        Assert.True(ResolvesFont3(textEngine));
        Assert.Equal(1, loader.Loads);

        screen.Dispose();
        screen.Dispose(); // idempotent
        Assert.True(screen.IsDisposed);
        Assert.Equal(2, assets.CollectUnreferenced()); // font3 and the screen's own envelope (parent ADR-0002)
        Assert.False(ResolvesFont3(textEngine));
    }

    /// <summary>The finding itself, at the engine boundary: the 389 screen holds font3; the world change
    /// starts (collection), the 389 world ends (its screen is disposed), the 390 view brings a new text
    /// engine, and the 390 screen takes font3 again - resolvable there, loaded once.</summary>
    [Fact]
    public void AcrossAMapChange_TheNextScreenFindsFont3_OnItsNewTextEngine_LoadedOnce()
    {
        var fonts = NewFonts(out var assets, out var loader);
        var map389TextEngine = new FontStashSharpTextEngine();
        fonts.Attach(map389TextEngine);
        var map389Screen = new AlundraInventoryScreen(assets, fonts);

        assets.CollectUnreferenced();
        map389Screen.Dispose();
        fonts.Detach(map389TextEngine);
        var map390TextEngine = new FontStashSharpTextEngine();
        fonts.Attach(map390TextEngine);
        using var map390Screen = new AlundraInventoryScreen(assets, fonts);

        Assert.True(ResolvesFont3(map390TextEngine));
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void Construction_WithoutFont3InTheExport_Throws_NamingTheAsset()
    {
        var fonts = NewFonts(out var assets, out _, withFont3: false);

        var exception = Assert.Throws<InvalidOperationException>(() => new AlundraInventoryScreen(assets, fonts));

        Assert.Contains("font3", exception.Message);
        Assert.Contains(AlundraInventoryScreen.Font3FontAssetId.ToString(), exception.Message);
    }

    [Fact]
    public void OnEndPlay_DisposesTheInventoryScreen()
    {
        var fonts = NewFonts(out var assets, out _);
        var screen = new AlundraInventoryScreen(assets, fonts);
        var proxy = new AlundraWorldProxy();
        proxy.AttachInventoryScreenForTests(screen);

        proxy.OnEndPlay(null!);

        Assert.True(screen.IsDisposed);
    }
}
