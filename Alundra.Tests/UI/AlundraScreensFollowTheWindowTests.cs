using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.s (plan docs/plan-e19-opcodes.md §1.2q, S-R5; engine ADR-0048): the four full-screen screens (HUD, inventory,
/// sub-inventory, save) used to compute their integer scale once, when their window was built. The engine now fits the
/// 320 x 240 image into the window and tells the XAML screens when the bounds of their desktop change
/// (<see cref="XamlUIScreenBase.NotifyScreenBounds"/>, driven each frame by the engine's <c>UIRoot</c>): each screen
/// redoes the computation of its own <c>OnWindowLoaded</c>. A desktop of 640 x 480 is a window fitted at x2; 960 x 720
/// at x3 (the view of a game, view-local: its bounds start at 0, 0).
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraScreensFollowTheWindowTests : IDisposable
{
    public AlundraScreensFollowTheWindowTests() => AlundraHudDirector.Instance.ResetForTests();

    public void Dispose() => AlundraHudDirector.Instance.ResetForTests();

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

    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "UI", "Screens")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"AlundraScreensFollowTheWindowTests: no versioned 'alundra-project/UI/Screens' above '{AppContext.BaseDirectory}'.");
    }

    private static AssetContentManager NewAssets(string screenAssetId, string screenName)
    {
        var screenInfo = new AssetInfo(Guid.Parse(screenAssetId))
        {
            Name = screenName,
            FileName = Path.Combine("UI", "Screens", screenName + ".uiscreen"),
        };
        var font3Info = new AssetInfo(AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = Path.Combine("UI", "font3.fnt") };
        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(
                null,
                ProjectDirectory(),
                id => id == screenInfo.Id ? screenInfo : id == font3Info.Id ? font3Info : null),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());
        assets.RegisterAssetLoader(typeof(BitmapFont), new AlundraInventoryScreenFontTests.CpuFont3Loader());
        return assets;
    }

    private static MGCanvas RootCanvas(MGWindow window)
    {
        Assert.True(window.TryGetElementByName("RootCanvas", out MGElement element));
        return Assert.IsType<MGCanvas>(element);
    }

    /// <summary>Builds the screen on a 640 x 480 desktop, checks the x2 state, grows the desktop to 960 x 720, tells
    /// the screen, and checks the x3 state; then tells it the same bounds again and checks nothing moved.</summary>
    private static void AssertFollowsTheDesktop(XamlUIScreenBase screen, Func<MGWindow> buildWindow, HeadlessUiTestHarness.HeadlessRuntime runtime, MGDesktop desktop)
    {
        var window = buildWindow();
        var canvas = RootCanvas(window);

        Assert.Equal(new Vector2(2, 2), canvas.RenderTransform.Scale);
        Assert.Equal((0, 0, 640, 480), (window.Left, window.Top, window.WindowWidth, window.WindowHeight));

        runtime.Resize(960, 720);
        screen.NotifyScreenBounds(desktop.ValidScreenBounds);

        Assert.Equal(new Vector2(3, 3), canvas.RenderTransform.Scale);
        Assert.Equal((0, 0, 960, 720), (window.Left, window.Top, window.WindowWidth, window.WindowHeight));

        // The same bounds again is not a change: a scale the screen's own code moved in between stays.
        canvas.RenderTransform.Scale = new Vector2(7, 7);
        screen.NotifyScreenBounds(desktop.ValidScreenBounds);
        Assert.Equal(new Vector2(7, 7), canvas.RenderTransform.Scale);

        runtime.Resize(640, 480);
        screen.NotifyScreenBounds(desktop.ValidScreenBounds);

        Assert.Equal(new Vector2(2, 2), canvas.RenderTransform.Scale);
        Assert.Equal((0, 0, 640, 480), (window.Left, window.Top, window.WindowWidth, window.WindowHeight));
    }

    [Fact]
    public void TheHudScreen_RescalesItsCanvasAndItsViewModel_WhenTheDesktopBoundsChange()
    {
        var assets = NewAssets(AlundraHudScreen.ScreenAssetId, "HudScreen");
        using var screen = new AlundraHudScreen(AlundraHudDirector.Instance, assets);
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop();

        AssertFollowsTheDesktop(screen, () => screen.BuildWindow(desktop), runtime, desktop);

        // The slide of the jauge is scaled by the view model's factor: it has to follow too (back at x2 here).
        Assert.Equal(2, screen.ViewModel.PixelScale);

        runtime.Resize(960, 720);
        screen.NotifyScreenBounds(desktop.ValidScreenBounds);
        Assert.Equal(3, screen.ViewModel.PixelScale);
    }

    [Fact]
    public void TheInventoryScreen_RescalesItsCanvas_WhenTheDesktopBoundsChange()
    {
        var assets = NewAssets(AlundraInventoryScreen.ScreenAssetId, "InventoryScreen");
        using var screen = new AlundraInventoryScreen(assets, new UIFontRegistry(assets));
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop();

        AssertFollowsTheDesktop(screen, () => screen.BuildWindow(desktop), runtime, desktop);
    }

    [Fact]
    public void TheSubInventoryScreen_RescalesItsCanvas_WhenTheDesktopBoundsChange()
    {
        var assets = NewAssets(AlundraSubInventoryScreen.ScreenAssetId, "SubInventoryScreen");
        using var screen = new AlundraSubInventoryScreen(assets, new UIFontRegistry(assets));
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop();

        AssertFollowsTheDesktop(screen, () => screen.BuildWindow(desktop), runtime, desktop);
    }

    [Fact]
    public void TheSaveScreen_RescalesItsCanvas_WhenTheDesktopBoundsChange()
    {
        var assets = NewAssets(AlundraSaveScreen.ScreenAssetId, "SaveScreen");
        using var screen = new AlundraSaveScreen(assets, new UIFontRegistry(assets));
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop();

        AssertFollowsTheDesktop(screen, () => screen.BuildWindow(desktop), runtime, desktop);
    }
}
