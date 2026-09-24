using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// Bound screens slice B4 (engine T6.1): Alundra's dialogue box is the project asset
/// <c>alundra-project/UI/Screens/DialogueScreen.*</c>, which the converter names in the project's
/// DialogueScreenAsset setting. The presenter builds the engine's dialogue screen with the game's asset manager, so
/// the screen takes that markup; the versioned markup must satisfy the engine screen's element contract, or the
/// engine would fall back to its built-in box. The window's name, <c>AlundraDialogue</c>, tells the two apart.
/// </summary>
public sealed class AlundraDialogueScreenAssetTests
{
    private const string AlundraWindowName = "AlundraDialogue";

    private sealed class NullUIViewRuntime : IUIViewRuntime
    {
        public bool IsPointerOverUI => false;
        public bool IsPointerCaptured => false;
        public bool IsKeyboardCaptured => false;
        public UIViewInputState InputState => UIViewInputState.Empty;
        public bool HasModalInput => false;
        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }
        public void Draw() { }
        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;
        public void PushScreen(IUIScreen screen) { }
        public IUIScreen? PopScreen() => null;
        public void RemoveScreen(IUIScreen screen) { }
        public void Dispose() { }
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

    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "UI", "Screens", "DialogueScreen.uiscreen")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraDialogueScreenAssetTests: no versioned 'alundra-project/UI/Screens/DialogueScreen.uiscreen' above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>A manager over the real project directory, whose settings name the versioned dialogue screen the
    /// way the converter writes AlundraGame.json.</summary>
    private static AssetContentManager NewAssets()
    {
        var project = ProjectDirectory();
        var relativePath = Path.Combine("UI", "Screens", "DialogueScreen.uiscreen");
        var envelope = JObject.Parse(File.ReadAllText(Path.Combine(project, relativePath)));
        var screenId = Guid.Parse(envelope["id"]!.ToString());
        var info = new AssetInfo(screenId) { Name = "DialogueScreen", FileName = relativePath };

        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(
                new ProjectSettings { DialogueScreenAsset = screenId.ToString() }, project, id => id == screenId ? info : null),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());
        return assets;
    }

    private static MGWindow BuildWindow(AlundraDialoguePresenter presenter)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        return presenter.ScreenForTests.BuildWindow(desktop);
    }

    [Fact]
    public void Envelope_NamesAXamlFileThatExists_AndNoDesignTimeData()
    {
        var directory = Path.Combine(ProjectDirectory(), "UI", "Screens");
        var envelope = new UIScreenAsset();
        envelope.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, "DialogueScreen.uiscreen"))));

        Assert.NotEqual(Guid.Empty, envelope.Id);
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(string.IsNullOrEmpty(envelope.DesignTimeDataFile)); // no view model to populate: the code fills the box
    }

    [Fact]
    public void WithTheGamesAssets_ThePresenterShowsTheProjectMarkup_AndDrivesItsLine()
    {
        var presenter = new AlundraDialoguePresenter(new NullUIViewRuntime(), assetContentManager: NewAssets());
        var window = BuildWindow(presenter);
        presenter.ScreenForTests.Show();

        presenter.ShowLine(new DialogueLine("Bonjour."));

        Assert.Equal(AlundraWindowName, window.Name);
        Assert.True(window.TryGetElementByName("lblLine", out MGTextBlock line));
        Assert.Contains("Bonjour.", line.Text);
        Assert.True(window.TryGetElementByName("pnlChoices", out MGStackPanel _));
        Assert.False(window.TryGetElementByName("btnClose", out MGElement _)); // Alundra's boxes have no close button
    }

    [Fact]
    public void WithoutTheGamesAssets_ThePresenterKeepsTheBuiltInBox()
    {
        var presenter = new AlundraDialoguePresenter(new NullUIViewRuntime());

        Assert.NotEqual(AlundraWindowName, BuildWindow(presenter).Name);
    }

    [Fact]
    public void Dispose_GivesTheProjectMarkupBack()
    {
        var assets = NewAssets();
        var presenter = new AlundraDialoguePresenter(new NullUIViewRuntime(), assetContentManager: assets);
        Assert.Equal(0, assets.CollectUnreferenced());

        presenter.Dispose();
        presenter.Dispose(); // idempotent

        Assert.Equal(1, assets.CollectUnreferenced());
    }

    [Fact]
    public void OnEndPlay_DisposesTheDialoguePresenter()
    {
        var assets = NewAssets();
        var proxy = new AlundraWorldProxy();
        proxy.AttachDialoguePresenterForTests(new AlundraDialoguePresenter(new NullUIViewRuntime(), assetContentManager: assets));

        proxy.OnEndPlay(null!);

        Assert.Equal(1, assets.CollectUnreferenced());
    }
}
