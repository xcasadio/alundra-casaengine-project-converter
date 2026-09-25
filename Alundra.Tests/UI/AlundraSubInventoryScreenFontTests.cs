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
/// E13.d SI4 (engine ADR-0036, plan D-E13D-18): same font-registry contract as
/// <see cref="AlundraInventoryScreenFontTests"/> - the sub-inventory screen holds font3 through the game's
/// UI font registry, from its construction to its disposal, so texts stay in font3 across a map change
/// (D5.f's own finding, which applies identically here).
/// <para/>
/// A real font3 needs a <c>Texture2D</c>, hence a graphics device; the test loader builds the
/// <see cref="BitmapFont"/> on a fixed-size font rasterized on the CPU from a TTF, the substitute the engine's
/// own font tests use.
/// </summary>
public sealed class AlundraSubInventoryScreenFontTests
{
    internal sealed class CpuFont3Loader : IAssetLoader
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
                $"AlundraSubInventoryScreenFontTests: no 'CasaEngineMonogame/CasaEngine/Content/Fonts/tahoma.ttf' above '{AppContext.BaseDirectory}'.");
        }
    }

    /// <summary>The screen's envelope, as the screen only needs it held (these tests never build its window).</summary>
    private sealed class ScreenEnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new UIScreenAsset { SourceXamlFile = "SubInventoryScreen.xaml" };

        public bool IsFileSupported(string fileName) => true;
    }

    private static UIFontRegistry NewFonts(out AssetContentManager assets, out CpuFont3Loader loader, bool withFont3 = true)
    {
        var font3 = new AssetInfo(AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = @"UI\font3.fnt" };
        var screenId = Guid.Parse(AlundraSubInventoryScreen.ScreenAssetId);
        var screen = new AssetInfo(screenId) { Name = "SubInventoryScreen", FileName = @"UI\Screens\SubInventoryScreen.uiscreen" };
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

        var screen = new AlundraSubInventoryScreen(assets, fonts);
        Assert.True(ResolvesFont3(textEngine));
        Assert.Equal(1, loader.Loads);

        screen.Dispose();
        screen.Dispose(); // idempotent
        Assert.True(screen.IsDisposed);
        Assert.Equal(2, assets.CollectUnreferenced()); // font3 and the screen's own envelope (parent ADR-0002)
        Assert.False(ResolvesFont3(textEngine));
    }

    /// <summary>Same map-change finding D5.f already proves for the main inventory's own screen.</summary>
    [Fact]
    public void AcrossAMapChange_TheNextScreenFindsFont3_OnItsNewTextEngine_LoadedOnce()
    {
        var fonts = NewFonts(out var assets, out var loader);
        var map389TextEngine = new FontStashSharpTextEngine();
        fonts.Attach(map389TextEngine);
        var map389Screen = new AlundraSubInventoryScreen(assets, fonts);

        assets.CollectUnreferenced();
        map389Screen.Dispose();
        fonts.Detach(map389TextEngine);
        var map390TextEngine = new FontStashSharpTextEngine();
        fonts.Attach(map390TextEngine);
        using var map390Screen = new AlundraSubInventoryScreen(assets, fonts);

        Assert.True(ResolvesFont3(map390TextEngine));
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void Construction_WithoutFont3InTheExport_Throws_NamingTheAsset()
    {
        var fonts = NewFonts(out var assets, out _, withFont3: false);

        var exception = Assert.Throws<InvalidOperationException>(() => new AlundraSubInventoryScreen(assets, fonts));

        Assert.Contains("font3", exception.Message);
        Assert.Contains(AlundraInventoryScreen.Font3FontAssetId.ToString(), exception.Message);
    }

    [Fact]
    public void OnEndPlay_DisposesTheSubInventoryScreen()
    {
        var fonts = NewFonts(out var assets, out _);
        var screen = new AlundraSubInventoryScreen(assets, fonts);
        var proxy = new AlundraWorldProxy();
        proxy.AttachSubInventoryScreenForTests(screen);

        proxy.OnEndPlay(null!);

        Assert.True(screen.IsDisposed);
    }
}
