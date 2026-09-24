using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// Engine task T5.2 (gap G10): the inventory and the HUD are rebuilt at every world change, and the previous screen
/// is disposed. Their windows' bindings must leave MGUI's static registry with them, or every world change would
/// keep the previous windows, images and view models reachable. Both screens are built from their versioned assets,
/// so the class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraScreenBindingReleaseTests : IDisposable
{
    public AlundraScreenBindingReleaseTests() => AlundraHudDirector.Instance.ResetForTests();

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
            $"AlundraScreenBindingReleaseTests: no versioned 'alundra-project/UI/Screens' above '{AppContext.BaseDirectory}'.");
    }

    private static AssetContentManager NewAssets(params AssetInfo[] infos)
    {
        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, ProjectDirectory(), id => infos.FirstOrDefault(info => info.Id == id)),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());
        return assets;
    }

    private static int BindingsOn(HashSet<MGElement> tree)
        => DataBindingManager.Bindings.Count(binding => binding.TargetObject is MGElement element && tree.Contains(element));

    [Fact]
    public void ADisposedInventoryScreen_LeavesNoBindingOnItsWindow()
    {
        var screenInfo = new AssetInfo(Guid.Parse(AlundraInventoryScreen.ScreenAssetId))
        {
            Name = "InventoryScreen",
            FileName = Path.Combine("UI", "Screens", "InventoryScreen.uiscreen"),
        };
        var font3Info = new AssetInfo(AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = Path.Combine("UI", "font3.fnt") };
        var assets = NewAssets(screenInfo, font3Info);
        assets.RegisterAssetLoader(typeof(BitmapFont), new AlundraInventoryScreenFontTests.CpuFont3Loader());

        var screen = new AlundraInventoryScreen(assets, new UIFontRegistry(assets));
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var tree = screen.BuildWindow(desktop).TraverseVisualTree().ToHashSet();
        Assert.NotEqual(0, BindingsOn(tree));

        screen.Dispose();

        Assert.Equal(0, BindingsOn(tree));
    }

    [Fact]
    public void ADisposedHudScreen_LeavesNoBindingOnItsWindow()
    {
        var screenInfo = new AssetInfo(Guid.Parse(AlundraHudScreen.ScreenAssetId))
        {
            Name = "HudScreen",
            FileName = Path.Combine("UI", "Screens", "HudScreen.uiscreen"),
        };
        var assets = NewAssets(screenInfo);

        var screen = new AlundraHudScreen(AlundraHudDirector.Instance, assets);
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var tree = screen.BuildWindow(desktop).TraverseVisualTree().ToHashSet();
        Assert.NotEqual(0, BindingsOn(tree));

        screen.Dispose();

        Assert.Equal(0, BindingsOn(tree));
    }
}
