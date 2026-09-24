using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// The HUD as a project asset (bound screens program, parent ADR-0002): loads
/// <c>alundra-project/UI/Screens/HudScreen.xaml</c> through the production pipeline on the headless harness, checks its
/// elements against the view model and the composer, its bindings, and the screen's own glue (scale and slide).
/// Loading it creates MGUI bindings: the class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraHudScreenXamlTests : IDisposable
{
    private static readonly string[] TileNames =
    {
        "HpMaxSlash", "HpMaxTens", "HpMaxUnits",
        "LifeBig0", "LifeBig1", "LifeBig2", "LifeBig3",
        "LifeSmall0", "LifeSmall1", "LifeSmall2", "LifeSmall3", "LifeSmall4",
        "LifeSmall5", "LifeSmall6", "LifeSmall7", "LifeSmall8", "LifeSmall9",
        "MagicPip0", "MagicPip1", "MagicPip2", "MagicPip3",
        "MoneyDigit0", "MoneyDigit1", "MoneyDigit2", "MoneyDigit3", "Coin",
    };

    public AlundraHudScreenXamlTests() => AlundraHudDirector.Instance.ResetForTests();

    public void Dispose() => AlundraHudDirector.Instance.ResetForTests();

    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "UI", "Screens", "HudScreen.xaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraHudScreenXamlTests: no versioned 'alundra-project/UI/Screens/HudScreen.xaml' above '{AppContext.BaseDirectory}'.");
    }

    private static string ScreensDirectory() => Path.Combine(ProjectDirectory(), "UI", "Screens");

    private static MGWindow LoadWindow(out MGDesktop desktop)
    {
        (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var source = XamlDocumentSource.FromString(File.ReadAllText(Path.Combine(ScreensDirectory(), "HudScreen.xaml")), "HudScreen.xaml");
        var window = UIScreenLoader.Load(desktop, source);
        desktop.Windows.Add(window);
        return window;
    }

    private static T Element<T>(MGWindow window, string name) where T : MGElement
    {
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        return Assert.IsType<T>(element);
    }

    [Fact]
    public void Envelope_CarriesTheScreenIdTheDllLoads_AndNamesFilesThatExist()
    {
        var directory = ScreensDirectory();
        var envelope = new UIScreenAsset();
        envelope.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, "HudScreen.uiscreen"))));

        Assert.Equal(Guid.Parse(AlundraHudScreen.ScreenAssetId), envelope.Id);
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    [Fact]
    public void DesignTimeData_PopulatesAHudViewModel()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "HudScreen.design.json")));
        Assert.Equal(nameof(AlundraHudViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraHudViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(Visibility.Visible, viewModel.HpMaxSlash.Visibility);
        Assert.NotNull(viewModel.MoneyDigit0.SourceName);
    }

    /// <summary>One element per tile role, as many as the composer can emit at once, plus the two equipment icons.</summary>
    [Fact]
    public void DeclaresOneImagePerTileRole_AndTheTwoEquipmentIcons()
    {
        var window = LoadWindow(out _);

        foreach (var name in TileNames.Append("EquipmentIcon0").Append("EquipmentIcon1"))
        {
            Element<MGImage>(window, name);
        }

        Assert.Equal(AlundraHudScreen.MaxTileCount, TileNames.Length);
        Assert.Equal(TileNames.Length + 2, Element<MGCanvas>(window, "RootCanvas").TraverseVisualTree().OfType<MGImage>().Count());
        Assert.Equal(AlundraHudViewModel.CoinAnimationId, Element<MGImage>(window, "Coin").SourceName);
    }

    /// <summary>The two equipment background quads are declared in the XAML with the composer's own values.</summary>
    [Fact]
    public void EquipmentBackgrounds_MatchTheComposersQuads()
    {
        var window = LoadWindow(out _);
        var quads = AlundraHudComposer.ComposeEquipmentBackgrounds();
        var rectangles = new[] { Element<MGRectangle>(window, "WeaponBoxBackground"), Element<MGRectangle>(window, "AccessoryBoxBackground") };

        for (var i = 0; i < quads.Count; i++)
        {
            var quad = quads[i];
            var rectangle = rectangles[i];
            Assert.Equal(quad.NativeX, rectangle.CanvasLeft);
            Assert.Equal(quad.NativeY, rectangle.CanvasTop);
            Assert.Equal(quad.NativeWidth, rectangle.Width);
            Assert.Equal(quad.NativeHeight, rectangle.Height);
            Assert.Equal(quad.Alpha, rectangle.Opacity);

            var fill = Assert.IsType<MGGradientFillBrush>(rectangle.Fill);
            Assert.Equal(new Color(quad.TopLeftColor.R, quad.TopLeftColor.G, quad.TopLeftColor.B), fill.TopLeftColor);
            Assert.Equal(new Color(quad.TopRightColor.R, quad.TopRightColor.G, quad.TopRightColor.B), fill.TopRightColor);
            Assert.Equal(new Color(quad.BottomLeftColor.R, quad.BottomLeftColor.G, quad.BottomLeftColor.B), fill.BottomLeftColor);
            Assert.Equal(new Color(quad.BottomRightColor.R, quad.BottomRightColor.G, quad.BottomRightColor.B), fill.BottomRightColor);
        }
    }

    /// <summary>What the presenter writes reaches the elements, animation parameters included.</summary>
    [Fact]
    public void Bindings_PushTheViewModelIntoTheElements()
    {
        var window = LoadWindow(out var desktop);
        var viewModel = new AlundraHudViewModel();
        window.WindowDataContext = viewModel;
        IAlundraHudView view = viewModel;

        view.SetVisible(true);
        view.SetAnimationClock(37, moneyRolling: true);
        var tiles = AlundraHudComposer.Compose(true, 9, 12, 12, false, 2, 4, false, 1234, 0, new[] { 3, 0, 1, 2 });
        view.SetTiles(tiles);
        desktop.Update();

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);

        var slash = Element<MGImage>(window, "HpMaxSlash");
        Assert.Equal(viewModel.HpMaxSlash.SourceName, slash.SourceName);
        Assert.Equal(tiles[0].NativeX, slash.CanvasLeft);
        Assert.Equal(tiles[0].NativeY, slash.CanvasTop);

        var pip = Element<MGImage>(window, "MagicPip0");
        Assert.Equal(AlundraHudViewModel.MagicPipAnimationId, pip.SourceName);
        Assert.Equal(TimeSpan.FromMilliseconds((3 * 10 + 7) * 20), pip.AnimationStartOffset);
        Assert.True(pip.IsAnimationPlaying);
        Assert.Equal("ace49f58-4b1c-54c1-8a08-52ecec193305", Element<MGImage>(window, "MagicPip2").SourceName); // wind_104

        var coin = Element<MGImage>(window, "Coin");
        Assert.Equal(Visibility.Visible, coin.Visibility);
        Assert.True(coin.IsAnimationPlaying);
        Assert.Equal(viewModel.Coin.AnimationStartOffset, coin.AnimationStartOffset);

        view.SetVisible(false);
        desktop.Update();
        Assert.Equal(Visibility.Collapsed, Element<MGCanvas>(window, "RootCanvas").Visibility);
    }

    private sealed class ScreenEnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = new UIScreenAsset();
            asset.Load(JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    /// <summary>The screen's own glue: it scales the canvas once by the integer pixel factor, and applies the view
    /// model's slide to the canvas's render transform and the icons' sub-pixel offsets to theirs, which MGUI's XAML
    /// cannot bind (gap G9).</summary>
    [Fact]
    public void TheScreen_ScalesItsCanvas_AndAppliesTheSlide()
    {
        var screenId = Guid.Parse(AlundraHudScreen.ScreenAssetId);
        var info = new AssetInfo(screenId) { Name = "HudScreen", FileName = Path.Combine("UI", "Screens", "HudScreen.uiscreen") };
        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, ProjectDirectory(), id => id == screenId ? info : null),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());

        using var screen = new AlundraHudScreen(AlundraHudDirector.Instance, assets);
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(); // 640 wide: pixel scale 2
        var window = screen.BuildWindow(desktop);
        var canvas = Element<MGCanvas>(window, "RootCanvas");

        Assert.Equal(new Vector2(2, 2), canvas.RenderTransform.Scale);
        Assert.Equal(2, screen.ViewModel.PixelScale);
        Assert.Same(screen.ViewModel, window.WindowDataContext);

        ((IAlundraHudView)screen.ViewModel).SetTranslation(new Vector2(0, -40));
        Assert.Equal(new Vector2(0, -40), canvas.RenderTransform.Translation);

        screen.ViewModel.EquipmentIcon1.SubPixelOffset = new Vector2(0.5f, 0f);
        Assert.Equal(new Vector2(0.5f, 0f), Element<MGImage>(window, "EquipmentIcon1").RenderTransform.Translation);
    }
}
